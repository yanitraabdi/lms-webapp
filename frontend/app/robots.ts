import type { MetadataRoute } from "next";

const BASE = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3001";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      // Private surfaces plus the archived-product routes, which remain reachable by URL
      // but must not be indexed for INVERTA (KAK §9.15).
      disallow: ["/app/", "/admin", "/checkout/", "/api/", "/catalog", "/pricing", "/for-business", "/modules/"],
    },
    sitemap: `${BASE}/sitemap.xml`,
  };
}
