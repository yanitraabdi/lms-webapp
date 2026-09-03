import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";

// next-intl: point the plugin at our request config (default locale `id`, M0 has no
// locale-prefixed routing — single locale only). See i18n/request.ts.
const withNextIntl = createNextIntlPlugin("./i18n/request.ts");

// Same-origin API proxy (tunnel / single-hostname deploys). When the frontend is fronted by a
// single public hostname (e.g. a Cloudflare tunnel), the browser must NOT call the .NET API at a
// baked localhost/origin — it can't reach it. Instead we build the client with
// NEXT_PUBLIC_API_BASE_URL="" so all browser API calls are relative (`/api/...`), and Next proxies
// them to the .NET API over the internal Docker network here.
//
// These are `afterFiles` rewrites, so they run AFTER filesystem routes: the auth BFF
// (/api/auth/login|logout|refresh|register) and /api/revalidate stay handled by Next, while every
// other /api/* (catalog, modules, me, notifications, auth/verify-email, …) is proxied to the API.
// In the default dev build NEXT_PUBLIC_API_BASE_URL is "http://localhost:8080", so the browser
// calls the API directly and these rewrites are simply never exercised.
const apiTarget = process.env.API_INTERNAL_URL ?? "http://api:8080";

// The archived AI-Academy marketing surface. INVERTA sells ONE programme, so a catalogue, a
// subscription price list and a B2B page describe a product that is no longer sold — yet all of
// them still served "AI Productivity Academy" to anyone with a stale link or bookmark.
//
// Redirects rather than deletions: CLAUDE.md keeps the archived product in-repo ("do not delete
// them"), and doing this in config touches none of those page files. Reviving the catalogue later
// is deleting this array, nothing more.
//
// Permanent (308) because the move is not provisional — the pivot happened.
const ARCHIVED_ROUTES = [
  "/catalog",
  "/catalog/:level",
  "/modules/:slug",
  "/pricing",
  "/for-business",
];

const nextConfig: NextConfig = {
  // Runs via `next start` in Docker (full node_modules) for parity with local.
  // Note: "standalone" output was dropped — its bundle resolved a duplicate React
  // context instance, breaking client context (useAuth) under SSR.
  async rewrites() {
    return {
      beforeFiles: [],
      afterFiles: [{ source: "/api/:path*", destination: `${apiTarget}/api/:path*` }],
      fallback: [],
    };
  },
  async redirects() {
    return ARCHIVED_ROUTES.map((source) => ({
      source,
      destination: "/program/toefl-preparation",
      permanent: true,
    }));
  },
};

export default withNextIntl(nextConfig);
