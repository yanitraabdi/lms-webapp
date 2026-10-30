import { expect, newLearner, signIn, test } from "./support/learner";

test("a fresh learner can sign in through the UI", async ({ page }) => {
  const l = await newLearner();
  await signIn(page, l.email, l.password);
  await expect(page).toHaveURL(/\/app\/dashboard/);
  await expect(page.getByRole("heading", { name: /Selamat datang kembali/ })).toBeVisible();
});
