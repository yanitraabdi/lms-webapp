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
};

export default withNextIntl(nextConfig);
