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
  // `npm run e2e:dev`: serve the frontend with `next dev` instead of a built image, so test rounds
  // never trigger Docker builds (the Docker VM is shared with other projects). Reuses a running one.
  webServer: process.env.E2E_DEV
    ? {
        command: "./node_modules/.bin/next dev --port 3100",
        url: "http://localhost:3100/program/toefl-preparation",
        reuseExistingServer: true,
        timeout: 240_000,
        env: { API_INTERNAL_URL: "http://localhost:8180", NEXT_PUBLIC_API_BASE_URL: "", REVALIDATE_SECRET: "dev-revalidate-secret" },
      }
    : undefined,
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
