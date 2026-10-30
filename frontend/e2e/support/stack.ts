// The API migrates and seeds before it listens, and api/frontend have no healthcheck: poll until ready.
export default async function globalSetup() {
  const base = process.env.BASE_URL ?? "http://localhost:3100";
  const deadline = Date.now() + 240_000;
  while (Date.now() < deadline) {
    try {
      if ((await fetch(`${base}/api/programs/toefl-preparation`)).status === 200) return;
    } catch {
      /* not up yet */
    }
    await new Promise((r) => setTimeout(r, 2000));
  }
  throw new Error("E2E stack not ready after 240s (run `npm run e2e:up`)");
}
