import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  workers: 1,
  fullyParallel: false,
  timeout: 60_000,
  retries: process.env.CI ? 1 : 0,
  failOnFlakyTests: !!process.env.CI, // a retry may rescue infra hiccups, never hide a flaky assertion
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",
  globalSetup: "./e2e/support/stack.ts",
  use: {
    baseURL: process.env.BASE_URL ?? "http://localhost:3100",
    trace: "retain-on-failure",
    locale: "id-ID",
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"] } },
    { name: "mobile", use: { ...devices["Pixel 5"], viewport: { width: 375, height: 812 } }, testMatch: /smoke\.spec\.ts/ },
  ],
});
