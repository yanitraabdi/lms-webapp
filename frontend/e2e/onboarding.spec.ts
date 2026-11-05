import { expect, test, signIn, LEARNER_PASSWORD } from "./support/learner";
import { api } from "./support/api";
import { verifyEmail } from "./support/db";

/** A learner who has not seen the tour yet (newLearner() marks it completed). */
async function freshLearner() {
  const email = `e2e-tour-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@test.local`;
  await api("/auth/register", { method: "POST", body: { name: "Siswa Tur", email, password: LEARNER_PASSWORD } });
  verifyEmail(email);
  return email;
}

test("first-run tour: sidebar links on desktop, one menu step on a phone", async ({ page }) => {
  const title = page.locator(".driver-popover-title");

  await page.setViewportSize({ width: 1280, height: 800 });
  await signIn(page, await freshLearner(), LEARNER_PASSWORD);
  await expect(title).toHaveText("Program Anda");
  await page.keyboard.press("Escape");

  await page.context().clearCookies();
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(page, await freshLearner(), LEARNER_PASSWORD);
  await expect(title).toHaveText("Menu");
  await expect(page.locator(".driver-active-element")).toHaveAttribute("data-tour", "nav-menu");
});
