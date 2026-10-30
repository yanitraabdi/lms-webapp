import { expect, test as base, type Locator, type Page } from "@playwright/test";
import { api, login } from "./api";
import { userId, verifyEmail } from "./db";

export const LEARNER_PASSWORD = "E2e-Learner-1!";

export async function newLearner() {
  const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@test.local`;
  const name = "Siswa E2E";
  await api("/auth/register", { method: "POST", body: { name, email, password: LEARNER_PASSWORD } });
  verifyEmail(email);
  const token = await login(email, LEARNER_PASSWORD);
  await api("/onboarding/tour", { method: "POST", token, body: { tourKey: "dashboard_first_run", status: "Completed" } });
  return { email, password: LEARNER_PASSWORD, name, id: userId(email) };
}

/** Refresh tokens rotate with reuse detection: a goto that aborts an in-flight refresh revokes the family. */
export async function gotoReady(page: Page, path: string, ready: Locator) {
  await page.goto(path);
  await ready.waitFor();
}

export async function signIn(page: Page, email: string, password: string) {
  await gotoReady(page, "/login", page.getByLabel("Kata sandi", { exact: true }));
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Kata sandi", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Masuk", exact: true }).click();
  await page.waitForURL("**/app/dashboard");
}

/** Pre-sets cookie consent so the fixed banner never covers buttons or skews axe. */
export const test = base.extend({
  page: async ({ page }, provide) => {
    await page.addInitScript(() =>
      localStorage.setItem("academy_cookie_consent", JSON.stringify({ essential: true, analytics: false, marketing: false, ts: 0 })),
    );
    await provide(page);
  },
});
export { expect };
