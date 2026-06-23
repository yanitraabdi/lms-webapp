import type { MetadataRoute } from "next";

const BASE = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3001";

// Public, indexable pages. Dynamic catalog/module URLs are intentionally omitted in v1.
export default function sitemap(): MetadataRoute.Sitemap {
  const paths = [
    "",
    "/catalog",
    "/pricing",
    "/about",
    "/how-it-works",
    "/for-business",
    "/help",
    "/contact",
    "/legal/terms",
    "/legal/privacy",
    "/legal/refund",
    "/legal/accessibility",
    "/legal/cookies",
  ];
  return paths.map((p) => ({ url: `${BASE}${p}`, changeFrequency: "weekly", priority: p === "" ? 1 : 0.7 }));
}
