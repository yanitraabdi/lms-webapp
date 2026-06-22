import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";

// next-intl: point the plugin at our request config (default locale `id`, M0 has no
// locale-prefixed routing — single locale only). See i18n/request.ts.
const withNextIntl = createNextIntlPlugin("./i18n/request.ts");

const nextConfig: NextConfig = {
  // Runs via `next start` in Docker (full node_modules) for parity with local.
  // Note: "standalone" output was dropped — its bundle resolved a duplicate React
  // context instance, breaking client context (useAuth) under SSR.
};

export default withNextIntl(nextConfig);
