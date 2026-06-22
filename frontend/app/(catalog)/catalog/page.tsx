import type { Metadata } from "next";
import { fetchCatalog, fetchFacets, num, type CatalogFacets, type CatalogPage } from "@/lib/catalog";
import { CatalogClient } from "./CatalogClient";

// /catalog — server-rendered per request. The anonymous first page + facets are
// fetched server-side so crawlers get a fully-populated catalog (SEO); the client
// then refetches (with the bearer token when signed in) to resolve per-module
// access state. Dynamic rather than ISR so the SEO HTML never serves stale-empty
// (an ISR build with the API unreachable would bake an empty grid).
export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Katalog modul — AI Productivity Academy",
  description:
    "Jelajahi seluruh kurikulum AI dalam Bahasa Indonesia, dari Basic hingga Advanced. Modul terkunci tetap terlihat — tingkatkan paket untuk membukanya.",
};

const PAGE_SIZE = 12;

const EMPTY_FACETS: CatalogFacets = { levels: [], categories: [], tags: [] };
const EMPTY_PAGE: CatalogPage = { modules: [], total: 0 };

export default async function CatalogPage() {
  const [facets, page] = await Promise.all([
    fetchFacets().catch(() => EMPTY_FACETS),
    fetchCatalog({ take: PAGE_SIZE }).catch(() => EMPTY_PAGE),
  ]);

  return (
    <CatalogClient
      initialFacets={facets}
      initialModules={page.modules}
      initialTotal={num(page.total)}
    />
  );
}
