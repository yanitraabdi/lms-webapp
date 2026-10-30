import { api, ADMIN, login } from "./support/api";
import { correctAnswers, psql } from "./support/db";
import { expect, gotoReady, newLearner, signIn, test } from "./support/learner";
import type { Page } from "@playwright/test";

const SLUG = "toefl-preparation";
const NOT_OFFICIAL = /bukan skor TOEFL resmi/i;

const sessionIds = () =>
  psql(`SELECT s.id FROM program_sessions s JOIN programs p ON p.id = s.program_id WHERE p.slug = '${SLUG}' ORDER BY s.order_index`).split("\n");
const part = (sid: string, kind: string) =>
  psql(`SELECT id || '|' || coalesce(assessment_id::text,'') FROM session_parts WHERE session_id = '${sid}' AND kind = '${kind}'`).split("|");
const enrollmentStatus = (userId: string) => psql(`SELECT status FROM enrollments WHERE user_id = '${userId}'`);

const tocItems = (page: Page) => page.getByRole("list", { name: "Daftar sesi" }).getByRole("listitem");
const dashboard = async (page: Page) => { await gotoReady(page, "/app/dashboard", tocItems(page).first()); };
async function expectToc(page: Page, done: number, current: number) {
  await dashboard(page);
  await expect(tocItems(page).filter({ hasText: "✓" })).toHaveCount(done);
  await expect(page.locator('[aria-current="step"]')).toContainText(new RegExp(`^${current}`));
}

/** Picks the DB-key choice for every question rendered on the page (radios are named q-{id}). */
async function answerFromKey(page: Page, key: Record<string, number>) {
  for (const [qid, idx] of Object.entries(key)) await page.locator(`input[name="q-${qid}"]`).nth(idx).check();
}

test("learner journey: enrol, learn, final assessment, verified certificate", async ({ page }) => {
  test.setTimeout(300_000);
  const learner = await newLearner();
  let ids: string[] = [];
  let token = "";
  let code = "";

  await test.step("1 enrol through the webhook path (GR-2)", async () => {
    await signIn(page, learner.email, learner.password);
    await gotoReady(page, `/program/${SLUG}`, page.getByRole("button", { name: "Daftar sekarang" }).first());
    await page.getByRole("button", { name: "Daftar sekarang" }).first().click();
    await page.waitForURL("**/checkout/dev-pay**");
    expect(enrollmentStatus(learner.id)).not.toBe("Active"); // PendingPayment or absent
    await page.getByRole("button", { name: "Bayar sekarang (berhasil)" }).click();
    await page.waitForURL("**/checkout/success**");
    await expect.poll(() => enrollmentStatus(learner.id), { timeout: 30_000 }).toBe("Active");
    ids = sessionIds();
    expect(ids).toHaveLength(8);
    await expectToc(page, 0, 1);
  });

  await test.step("2 linear lock (GR-8)", async () => {
    token = await login(learner.email, learner.password);
    const [lesson2] = part(ids[1], "LessonVideo");
    const res = await fetch(`${process.env.BASE_URL ?? "http://localhost:3100"}/api/sessions/${ids[1]}/parts/${lesson2}/playback`, {
      method: "POST", headers: { authorization: `Bearer ${token}` },
    });
    expect(res.status).toBe(403);
    await gotoReady(page, `/app/session/${ids[1]}`, page.getByText("Sesi terkunci"));
    await expect(page.getByRole("button", { name: "Mulai tes" })).toHaveCount(0);
  });

  await test.step("3 watching alone does not complete; no answer key served (GR-11)", async () => {
    const [lesson1] = part(ids[0], "LessonVideo");
    const [testPart1, assessment1] = part(ids[0], "Test");
    await api(`/sessions/${ids[0]}/parts/${lesson1}/progress`, { method: "PUT", token, body: { positionSeconds: 60, percent: 100 } });
    await expectToc(page, 0, 1);
    const body = await api(`/sessions/${ids[0]}/parts/${testPart1}/assessment`, { token });
    expect(body.id).toBe(assessment1);
    expect(JSON.stringify(body).toLowerCase()).not.toContain('"correct');
  });

  await test.step("4 pass session 1's test in the UI", async () => {
    const [, assessment1] = part(ids[0], "Test");
    await gotoReady(page, `/app/session/${ids[0]}`, page.getByRole("heading", { name: "Tes sesi" }));
    const start = page.getByRole("button", { name: "Mulai tes" });
    if (await start.count()) await start.click();
    await answerFromKey(page, correctAnswers(assessment1));
    await page.getByRole("button", { name: "Kirim jawaban" }).click();
    await expect(page.getByText(/Selamat! Skor/)).toBeVisible();
    await expectToc(page, 1, 2);
  });

  await test.step("5 sessions 2-6 via the API", async () => {
    for (const sid of ids.slice(1, 6)) {
      const [lesson] = part(sid, "LessonVideo");
      const [, assessmentId] = part(sid, "Test");
      await api(`/sessions/${sid}/parts/${lesson}/progress`, { method: "PUT", token, body: { positionSeconds: 60, percent: 100 } });
      const attempt = await api(`/assessments/${assessmentId}/attempts`, { method: "POST", token });
      const r = await api(`/attempts/${attempt.id}/submit`, { method: "POST", token, body: { answers: correctAnswers(assessmentId) } });
      expect(r.passed).toBe(true);
    }
    await expectToc(page, 6, 7);
  });

  await test.step("6 live session via admin attendance", async () => {
    const admin = await login(ADMIN.email, ADMIN.password);
    await api(`/admin/sessions/${ids[6]}/attendance`, { method: "POST", token: admin, body: { userIds: [learner.id], attended: true } });
    await expectToc(page, 7, 8);
  });

  await test.step("7 final assessment in the UI (GR-11, GR-12, GR-14)", async () => {
    await gotoReady(page, `/app/session/${ids[7]}`, page.getByRole("link", { name: "Mulai tes akhir" }));
    const finalMeta = await api(`/sessions/${ids[7]}/assessment`, { token });
    expect(JSON.stringify(finalMeta).toLowerCase()).not.toContain('"correct');
    await page.getByRole("link", { name: "Mulai tes akhir" }).click();
    await page.waitForURL(`**/app/assessment/${ids[7]}`);
    await page.getByRole("button", { name: "Mulai tes", exact: true }).click();
    await page.waitForURL("**/app/exam/*");
    const attemptId = new URL(page.url()).pathname.split("/").pop()!;
    const state = await api(`/attempts/${attemptId}/state`, { token });
    expect(JSON.stringify(state).toLowerCase()).not.toContain('"correct');

    for (let section = 1; section <= 3; section++) {
      await expect(page.getByText(`Bagian ${section} dari 3`)).toBeVisible();
      const radios = page.locator('input[type="radio"]');
      await radios.first().waitFor();
      const names = [...new Set(await radios.evaluateAll((els) => els.map((e) => (e as HTMLInputElement).name)))].slice(0, 3);
      for (const n of names) await page.locator(`input[name="${n}"]`).first().check();
      const last = section === 3;
      await page.getByRole("button", { name: last ? "Selesaikan tes" : "Bagian berikutnya" }).click();
      if (!last) await expect(page.getByText(`Bagian ${section + 1} dari 3`)).toBeVisible({ timeout: 30_000 });
    }
    await expect(page.locator("#exam-result-title")).toHaveText("Tes selesai", { timeout: 60_000 });
    await expect(page.getByText("Skor prediksi TOEFL")).toBeVisible();
    await expect(page.locator("main, body").getByText(NOT_OFFICIAL).first()).toBeVisible();
    await expect(page.locator("p.sr-only").first()).toHaveText(/^\d+$/);
  });

  await test.step("8 certificate and verify page", async () => {
    await gotoReady(page, "/app/certificates", page.getByRole("heading", { name: "Sertifikat", exact: true }));
    await expect(page.getByText("Sertifikat prediksi", { exact: true })).toHaveCount(1);
    code = (await page.getByText(/^Kode: /).innerText()).replace("Kode: ", "").trim();
    expect(code).toBeTruthy();
    await expect(page.getByText(NOT_OFFICIAL).first()).toBeVisible();

    await page.goto(`/verify/${code}`);
    await expect(page.getByText("Sertifikat terverifikasi")).toBeVisible();
    await expect(page.getByRole("heading", { name: learner.name })).toBeVisible();
    await expect(page.getByText(NOT_OFFICIAL).first()).toBeVisible();

    await page.goto("/verify/NOPE-0000");
    await expect(page.getByRole("heading", { name: "Sertifikat tidak ditemukan" })).toBeVisible();
  });
});
