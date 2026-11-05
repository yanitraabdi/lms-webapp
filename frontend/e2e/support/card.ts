import { expect, type Page } from "@playwright/test";

/** The old floating "sheet" card (its -18px shadow) must not appear on the current page. */
export async function expectNoCard(page: Page) {
  const cards = await page.evaluate(() =>
    [...document.querySelectorAll("body *")].filter((el) => getComputedStyle(el).boxShadow.includes("-18px")).length);
  expect(cards).toBe(0);
}
