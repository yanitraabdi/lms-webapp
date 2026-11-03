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
