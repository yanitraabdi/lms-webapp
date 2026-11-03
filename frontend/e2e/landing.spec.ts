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
  await expect.poll(() => journey.evaluate(running), { timeout: 6_000 }).toBe(0); // WCAG 2.2.2: ≤ 5s, no loop
  const peek = page.locator('figure[data-anim="peek"]');
  await peek.scrollIntoViewIfNeeded();
  await expect(peek.getByRole("button", { name: "Jeda" })).toBeVisible();
  await peek.getByRole("button", { name: "Jeda" }).click();
  expect(await peek.evaluate(running)).toBe(0);
  await expect.poll(() => peek.getByRole("button", { name: "Putar ulang" }).count(), { timeout: 12_000 }).toBe(1);
});
