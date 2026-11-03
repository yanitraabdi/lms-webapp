import { expect, newLearner, signIn, test } from "./support/learner";

let l: Awaited<ReturnType<typeof newLearner>>;
test.beforeAll(async () => { l = await newLearner(); });

test("desktop: sidebar nav with active item, no top menu", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await signIn(page, l.email, l.password);
  const nav = page.getByRole("navigation", { name: "Menu utama" });
  await expect(nav.getByRole("link", { name: "Dasbor" })).toHaveAttribute("aria-current", "page");
  await nav.getByRole("link", { name: "Sertifikat" }).click();
  await expect(nav.getByRole("link", { name: "Sertifikat" })).toHaveAttribute("aria-current", "page");
  await expect(page.getByRole("button", { name: /^Menu akun / })).toBeVisible();
});

test("phone: drawer opens, Escape closes and returns focus", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(page, l.email, l.password);
  const open = page.getByRole("button", { name: "Buka menu" });
  await open.click();
  await expect(page.getByRole("dialog", { name: "Menu" })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog", { name: "Menu" })).toHaveCount(0);
  await expect(open).toBeFocused();
  await open.click();
  await page.getByRole("dialog", { name: "Menu" }).getByRole("link", { name: "Sertifikat" }).click();
  await page.waitForURL("**/app/certificates");
  await expect(page.getByRole("dialog", { name: "Menu" })).toHaveCount(0);
});

test("phone: tapping the current page's link closes the drawer", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(page, l.email, l.password);
  await page.waitForURL("**/app/dashboard");
  await page.getByRole("button", { name: "Buka menu" }).click();
  const dialog = page.getByRole("dialog", { name: "Menu" });
  await dialog.getByRole("link", { name: "Dasbor" }).click();
  await expect(dialog).toHaveCount(0);
});

test("long name does not overflow at 375px", async ({ page }) => {
  const long = await newLearner("Muhammad Rizky Pratama Wibowo Santoso");
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(page, long.email, long.password);
  await page.getByRole("button", { name: "Buka menu" }).click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(375);
});
