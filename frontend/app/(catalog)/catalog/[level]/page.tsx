import type { Metadata } from "next";
import { fetchCatalog, fetchFacets, num, type CatalogFacets, type CatalogPage } from "@/lib/catalog";
import { CatalogClient } from "../CatalogClient";

// /catalog/[level] — server-rendered per request, level-scoped (e.g.
// /catalog/intermediate) for SEO. Dynamic (not ISR) for the same reason as
// /catalog: never serve a stale-empty grid to crawlers.
export const dynamic = "force-dynamic";

const PAGE_SIZE = 12;
const EMPTY_FACETS: CatalogFacets = { levels: [], categories: [], tags: [] };
const EMPTY_PAGE: CatalogPage = { modules: [], total: 0 };

export async function generateMetadata({
  params,
}: {
  params: Promise<{ level: string }>;
}): Promise<Metadata> {
  const { level } = await params;
  const name = level.charAt(0).toUpperCase() + level.slice(1);
  return {
    title: `Katalog Level ${name} — AI Productivity Academy`,
    description: `Modul AI untuk level ${name} dalam Bahasa Indonesia.`,
  };
}

export default async function CatalogLevelPage({
  params,
}: {
  params: Promise<{ level: string }>;
}) {
  const { level } = await params;
  const [facets, page] = await Promise.all([
    fetchFacets().catch(() => EMPTY_FACETS),
    fetchCatalog({ levels: [level], take: PAGE_SIZE }).catch(() => EMPTY_PAGE),
  ]);

  return (
    <CatalogClient
      initialFacets={facets}
      initialModules={page.modules}
      initialTotal={num(page.total)}
      initialLevels={[level]}
    />
  );
}
