// Catalog data layer. Types come from the generated OpenAPI client
// (`@/api-client/schema`) — do NOT hand-write request/response shapes here.
// This is a thin, typed fetch wrapper over the public .NET catalog endpoints.
import type { components } from "@/api-client/schema";

export type ModuleAccess = components["schemas"]["ModuleAccess"];
export type ModuleSummary = components["schemas"]["ModuleSummaryDto"];
export type ModuleDetail = components["schemas"]["ModuleDetailDto"];
export type CatalogPage = components["schemas"]["CatalogPageDto"];
export type CatalogFacets = components["schemas"]["CatalogFacetsDto"];
export type FacetLevel = components["schemas"]["FacetLevel"];
export type FacetCategory = components["schemas"]["FacetCategory"];
export type FacetTag = components["schemas"]["FacetTag"];
export type ResourceDto = components["schemas"]["ResourceDto"];

// On the server (SSR/ISR proxy-fetch) reach the API over the internal network;
// in the browser use the public origin. Mirrors the auth BFF (lib/auth/bff.ts).
function baseUrl(): string {
  if (typeof window === "undefined") {
    return (
      process.env.API_INTERNAL_URL ??
      process.env.NEXT_PUBLIC_API_BASE_URL ??
      "http://localhost:8080"
    );
  }
  return process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
}

export interface CatalogQuery {
  levels?: string[];
  categories?: string[];
  tags?: string[];
  search?: string;
  sort?: string;
  skip?: number;
  take?: number;
}

export interface FetchOpts {
  /** Bearer access token — when present, per-module access reflects entitlement. */
  token?: string | null;
  signal?: AbortSignal;
  /** ISR revalidate window (seconds) for server fetches. Omit for no-store. */
  revalidate?: number;
}

function toInit({ token, signal, revalidate }: FetchOpts = {}): RequestInit {
  const init: RequestInit & { next?: { revalidate: number } } = {};
  if (token) init.headers = { Authorization: `Bearer ${token}` };
  if (signal) init.signal = signal;
  if (typeof revalidate === "number") init.next = { revalidate };
  else init.cache = "no-store";
  return init;
}

function queryString(q: CatalogQuery): string {
  const p = new URLSearchParams();
  q.levels?.forEach((v) => p.append("level", v));
  q.categories?.forEach((v) => p.append("category", v));
  q.tags?.forEach((v) => p.append("tag", v));
  if (q.search) p.set("search", q.search);
  if (q.sort) p.set("sort", q.sort);
  if (q.skip != null) p.set("skip", String(q.skip));
  if (q.take != null) p.set("take", String(q.take));
  const s = p.toString();
  return s ? `?${s}` : "";
}

export async function fetchCatalog(q: CatalogQuery = {}, opts?: FetchOpts): Promise<CatalogPage> {
  const res = await fetch(`${baseUrl()}/api/catalog${queryString(q)}`, toInit(opts));
  if (!res.ok) throw new Error(`Gagal memuat katalog (${res.status})`);
  return (await res.json()) as CatalogPage;
}

export async function fetchFacets(opts?: FetchOpts): Promise<CatalogFacets> {
  const res = await fetch(`${baseUrl()}/api/catalog/facets`, toInit(opts));
  if (!res.ok) throw new Error(`Gagal memuat filter (${res.status})`);
  return (await res.json()) as CatalogFacets;
}

export async function fetchModule(slug: string, opts?: FetchOpts): Promise<ModuleDetail | null> {
  const res = await fetch(`${baseUrl()}/api/modules/${encodeURIComponent(slug)}`, toInit(opts));
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Gagal memuat modul (${res.status})`);
  return (await res.json()) as ModuleDetail;
}

// ---- display helpers ----

/** OpenAPI int32 fields are typed `number | string`; coerce safely. */
export function num(v: number | string): number {
  return typeof v === "string" ? Number(v) : v;
}

export function minutesLabel(seconds: number | string): string {
  const m = Math.max(1, Math.round(num(seconds) / 60));
  return `${m} mnt`;
}

export type LevelTier = "beginner" | "intermediate" | "advanced";

export function tierForLevelSlug(slug: string): LevelTier {
  if (slug.includes("inter")) return "intermediate";
  if (slug.includes("adv")) return "advanced";
  return "beginner";
}
