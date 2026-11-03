import { expect, test } from "./support/learner";

const SLUG = "toefl-preparation";

test("footer logo keeps its aspect ratio", async ({ page }) => {
  await page.goto(`/program/${SLUG}`);
  const img = page.locator("footer img[alt='INVERTA']");
  const { w, h, nw, nh } = await img.evaluate((el: HTMLImageElement) =>
    ({ w: el.clientWidth, h: el.clientHeight, nw: el.naturalWidth, nh: el.naturalHeight }));
  expect(Math.abs(w / h - nw / nh)).toBeLessThan(0.05);
});
