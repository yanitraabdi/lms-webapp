import { expect, test } from "./support/learner";

const SLUG = "toefl-preparation";

test("footer logo keeps its aspect ratio", async ({ page }) => {
  await page.goto(`/program/${SLUG}`);
  const img = page.locator("footer img[alt='INVERTA']");
  const { w, h, nw, nh } = await img.evaluate((el: HTMLImageElement) =>
    ({ w: el.clientWidth, h: el.clientHeight, nw: el.naturalWidth, nh: el.naturalHeight }));
  expect(Math.abs(w / h - nw / nh)).toBeLessThan(0.05);
});

test("anonymous visitor sees one enrol label in nav and hero, nav enrol goes to login", async ({ page }) => {
  await page.goto(`/program/${SLUG}`);
  await expect(page.getByRole("button", { name: "Masuk & daftar" })).toHaveCount(0);
  await expect(page.getByRole("banner").getByRole("button", { name: "Daftar sekarang" })).toBeVisible();
  await page.getByRole("banner").getByRole("button", { name: "Daftar sekarang" }).click();
  await page.waitForURL(/\/login\?next=%2Fprogram%2Ftoefl-preparation/);
});

test("other public pages keep Daftar gratis", async ({ page }) => {
  await page.goto("/about");
  await expect(page.getByRole("banner").getByRole("link", { name: "Daftar gratis" })).toBeVisible();
});

const running = (el: Element) => el.getAnimations({ subtree: true }).filter((a) => a.playState === "running").length;

test("animations have text equivalents and stop under reduced motion", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto(`/program/${SLUG}`);
  for (const name of ["journey", "peek"]) {
    const fig = page.locator(`figure[data-anim="${name}"]`);
    await expect(fig.locator("figcaption.sr-only")).not.toBeEmpty();
    expect(await fig.evaluate(running)).toBe(0);
  }
  await expect(page.locator('figure[data-anim="peek"]')).toContainText("bukan skor TOEFL resmi");
});

test("the path plays once and stops; the peek plays on view, pauses and replays", async ({ page }) => {
  await page.goto(`/program/${SLUG}`);
  const journey = page.locator('figure[data-anim="journey"]');
  await journey.scrollIntoViewIfNeeded();
  await expect(journey).toHaveAttribute("data-playing", "true");
  await expect.poll(() => journey.evaluate(running), { timeout: 6_000 }).toBe(0); // WCAG 2.2.2: ≤ 5s, no loop
  const peek = page.locator('figure[data-anim="peek"]');
  await peek.scrollIntoViewIfNeeded();
  await expect(peek.getByRole("button", { name: "Jeda" })).toBeVisible();
  await peek.getByRole("button", { name: "Jeda" }).click();
  expect(await peek.evaluate(running)).toBe(0);
  await expect.poll(() => peek.getByRole("button", { name: "Putar ulang" }).count(), { timeout: 12_000 }).toBe(1);
});

test("bands in order, hero enrol above the fold on desktop and phone", async ({ page }) => {
  for (const size of [{ width: 1280, height: 720 }, { width: 375, height: 812 }]) {
    await page.setViewportSize(size);
    await page.goto(`/program/${SLUG}`);
    await expect(page.locator("h1")).toHaveText("Belajar TOEFL langkah demi langkah, sampai tahu prediksi skor Anda.");
    await expect(page.getByRole("main").getByRole("button", { name: "Daftar sekarang" }).first()).toBeInViewport();
  }
  const h2 = await page.locator("main h2").allInnerTexts();
  expect(h2.slice(0, 5)).toEqual(["Apa itu INVERTA", "Daftar isi program", "Yang membedakan", "Biaya", "Pertanyaan umum"]);
  await expect(page.getByRole("main")).not.toContainText(/Kenapa berhasil|tes asli/);
  await page.getByRole("link", { name: "Lihat isi program" }).click();
  await expect(page.getByRole("list", { name: "Daftar sesi" })).toBeInViewport();
  await expect(page.getByRole("main")).toContainText("Rp");
  await expect(page.getByRole("main")).toContainText("bukan skor TOEFL resmi");
  await expect(page.getByRole("list", { name: "Daftar sesi" }).getByRole("listitem")).toHaveCount(8);
});

test("on a phone the path waits below the fold, then plays once in view", async ({ page }) => {
  // 375x667 (iPhone SE): A is below the fold. At 812 tall the hero is short enough that A is already in view.
  await page.setViewportSize({ width: 375, height: 667 });
  await page.goto(`/program/${SLUG}`);
  const journey = page.locator('figure[data-anim="journey"]');
  await expect(page.locator("h1")).toBeVisible();
  await expect(journey).not.toBeInViewport({ ratio: 0.4 });
  await page.waitForTimeout(1_000); // give a wrongly on-load animation time to start
  expect(await journey.evaluate(running)).toBe(0);
  await expect(journey).not.toHaveAttribute("data-playing");
  await journey.scrollIntoViewIfNeeded();
  await expect.poll(() => journey.evaluate(running)).toBeGreaterThan(0);
  await expect.poll(() => journey.evaluate(running), { timeout: 6_000 }).toBe(0);
});

test("legal pages have a contents list on desktop and none on phones", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto("/legal/terms");
  const toc = page.getByRole("navigation", { name: "Daftar isi" });
  await expect(toc).toBeVisible();
  const first = toc.getByRole("link").first();
  const href = await first.getAttribute("href");
  await first.click();
  await expect(page.locator(href!)).toBeInViewport();
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(toc).toBeHidden();
});
