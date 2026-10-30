import { expectNoSeriousA11y } from "./support/axe";
import { ADMIN } from "./support/api";
import { expect, newLearner, signIn, test } from "./support/learner";
import type { Page } from "@playwright/test";

type Row = [path: string, finalPath?: string];

async function checkPage(page: Page, [path, finalPath = path]: Row) {
  const errors: string[] = [];
  // Anonymous visitors get one expected 401 from the silent session-refresh bootstrap; nothing else is tolerated.
  const bootstrap401 = "Failed to load resource: the server responded with a status of 401 (Unauthorized)";
  page.on("console", (m) => m.type() === "error" && m.text() !== bootstrap401 && errors.push(m.text()));
  const res = await page.goto(path);
  expect(res?.status()).toBeLessThan(400);
  await page.locator("h1").first().waitFor();
  expect(new URL(page.url()).pathname).toBe(finalPath);
  await expect(page.locator("h1")).toHaveCount(1);
  expect(errors).toEqual([]);
  await expectNoSeriousA11y(page);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}

const PUBLIC: Row[] = [
  ["/", "/program/toefl-preparation"],
  ["/program/toefl-preparation"],
  ["/about"],
  ["/how-it-works"],
  ["/help"],
  ["/contact"],
  ["/login"],
  ["/register"],
  ["/forgot-password"],
  ["/verify/UNKNOWN-CODE"],
  ...["terms", "privacy", "refund", "accessibility", "cookies"].map((k): Row => [`/legal/${k}`]),
];
const LEARNER: Row[] = [["/app/dashboard"], ["/app/certificates"], ["/app/account"], ["/app/notifications"]];
const ADMIN_PAGES: Row[] = [
  ["/admin"],
  ["/admin/programs"],
  ["/admin/questions"],
  ["/admin/questions/import"],
  ["/admin/enrollments"],
  ["/admin/attempts"],
  ["/admin/users"],
];

test.describe("public", () => {
  for (const row of PUBLIC) test(row[0], ({ page }) => checkPage(page, row));
});

test.describe("learner", () => {
  let l: Awaited<ReturnType<typeof newLearner>>;
  test.beforeAll(async () => { l = await newLearner(); });
  test.beforeEach(({ page }) => signIn(page, l.email, l.password));
  for (const row of LEARNER) test(row[0], ({ page }) => checkPage(page, row));
});

test.describe("admin", () => {
  test.beforeEach(({ page }) => signIn(page, ADMIN.email, ADMIN.password));
  for (const row of ADMIN_PAGES) test(row[0], ({ page }) => checkPage(page, row));
});
