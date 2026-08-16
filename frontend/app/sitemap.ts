import type { MetadataRoute } from "next";

const BASE = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3001";

// Public, indexable INVERTA pages. Archived-product routes (catalog, pricing, for-business)
// are deliberately excluded — they are unlinked and must not be indexed.
export default function sitemap(): MetadataRoute.Sitemap {
  const paths = [
    "",
    "/program/toefl-preparation",
    "/about",
    "/how-it-works",
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
