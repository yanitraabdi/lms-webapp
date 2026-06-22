"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import {
  Badge,
  Button,
  EmptyState,
  ErrorState,
  ModuleCard,
  Skeleton,
  SearchIcon,
  XIcon,
  ChevronRightIcon,
  type ModuleCardState,
} from "@/components/ui";
import {
  fetchCatalog,
  minutesLabel,
  num,
  tierForLevelSlug,
  type CatalogFacets,
  type CatalogPage,
  type ModuleAccess,
  type ModuleSummary,
} from "@/lib/catalog";

const PAGE_SIZE = 12;
const NO_MODULES: ModuleSummary[] = [];

const SORTS = [
  { value: "curriculum", label: "Urutkan: Kurikulum" },
  { value: "newest", label: "Terbaru" },
  { value: "duration", label: "Durasi terpendek" },
];

interface CatalogClientProps {
  initialFacets: CatalogFacets;
  initialModules: ModuleSummary[];
  initialTotal: number;
  initialLevels?: string[];
}

function cardState(access: ModuleAccess): ModuleCardState {
  return access === "Entitled" ? "entitled" : access === "Preview" ? "preview" : "locked";
}

function toggle(list: string[], value: string): string[] {
  return list.includes(value) ? list.filter((v) => v !== value) : [...list, value];
}

export function CatalogClient({
  initialFacets,
  initialModules,
  initialTotal,
  initialLevels = [],
}: CatalogClientProps) {
  const router = useRouter();
  const { accessToken } = useAuth();

  const [levels, setLevels] = useState<string[]>(initialLevels);
  const [categories, setCategories] = useState<string[]>([]);
  const [tags, setTags] = useState<string[]>([]);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [sort, setSort] = useState("curriculum");
  const [take, setTake] = useState(PAGE_SIZE);

  // Debounce the free-text search.
  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  // Reset paging whenever a filter narrows/changes the result set.
  useEffect(() => {
    setTake(PAGE_SIZE);
  }, [levels, categories, tags, search, sort]);

  // The initial server fetch was anonymous + default filters; reuse it as seed
  // data only while those still hold and we have no token yet (avoids a flash).
  const isInitialView =
    !accessToken &&
    take === PAGE_SIZE &&
    sort === "curriculum" &&
    search === "" &&
    categories.length === 0 &&
    tags.length === 0 &&
    levels.length === initialLevels.length &&
    levels.every((l) => initialLevels.includes(l));

  const query = useQuery({
    queryKey: ["catalog", { levels, categories, tags, search, sort, take }, !!accessToken],
    queryFn: ({ signal }) =>
      fetchCatalog({ levels, categories, tags, search, sort, take }, { token: accessToken, signal }),
    placeholderData: keepPreviousData,
    initialData: isInitialView
      ? ({ modules: initialModules, total: initialTotal } as CatalogPage)
      : undefined,
  });

  const modules = query.data?.modules ?? NO_MODULES;
  const total = num(query.data?.total ?? 0);

  // Group results by Level → Track (modules arrive in curriculum order).
  const groups = useMemo(() => {
    const byLevel = new Map<
      string,
      { levelName: string; levelSlug: string; tracks: Map<string, ModuleSummary[]> }
    >();
    for (const m of modules) {
      let lvl = byLevel.get(m.levelSlug);
      if (!lvl) {
        lvl = { levelName: m.levelName, levelSlug: m.levelSlug, tracks: new Map() };
        byLevel.set(m.levelSlug, lvl);
      }
      const track = lvl.tracks.get(m.trackName) ?? [];
      track.push(m);
      lvl.tracks.set(m.trackName, track);
    }
    return Array.from(byLevel.values()).map((l) => ({
      levelName: l.levelName,
      levelSlug: l.levelSlug,
      locked: Array.from(l.tracks.values()).flat().every((m) => m.access === "Locked"),
      tracks: Array.from(l.tracks.entries()).map(([name, mods]) => ({ name, mods })),
    }));
  }, [modules]);

  const activeChips = [
    ...levels.map((v) => ({ kind: "level" as const, value: v })),
    ...categories.map((v) => ({ kind: "category" as const, value: v })),
    ...tags.map((v) => ({ kind: "tag" as const, value: v })),
  ];
  const hasFilters = activeChips.length > 0 || search !== "";

  function labelFor(kind: "level" | "category" | "tag", value: string): string {
    if (kind === "level") return initialFacets.levels.find((l) => l.slug === value)?.name ?? value;
    if (kind === "category")
      return initialFacets.categories.find((c) => c.slug === value)?.name ?? value;
    return `#${initialFacets.tags.find((t) => t.slug === value)?.name ?? value}`;
  }

  function removeChip(kind: "level" | "category" | "tag", value: string) {
    if (kind === "level") setLevels((v) => v.filter((x) => x !== value));
    else if (kind === "category") setCategories((v) => v.filter((x) => x !== value));
    else setTags((v) => v.filter((x) => x !== value));
  }

  function resetFilters() {
    setLevels([]);
    setCategories([]);
    setTags([]);
    setSearchInput("");
    setSearch("");
    setSort("curriculum");
  }

  function goToModule(m: ModuleSummary) {
    if (m.access === "Locked") router.push("/pricing");
    else router.push(`/modules/${m.slug}`);
  }

  function ctaLabel(m: ModuleSummary): string {
    if (m.access === "Entitled") return "Mulai";
    if (m.access === "Preview") return "Tonton pratinjau";
    return `Tingkatkan ke ${m.levelName}`;
  }

  const showSkeleton = query.isPending && modules.length === 0;
  const showEmpty = !query.isPending && !query.isError && modules.length === 0;

  return (
    <div className="mx-auto max-w-6xl px-6 pb-16 pt-7">
      {/* Header + search/sort */}
      <div className="mb-6 flex flex-col gap-[18px]">
        <div className="flex flex-col gap-1.5">
          <h1 className="text-[28px] font-extrabold tracking-tight text-ink">Katalog modul</h1>
          <p className="text-[14.5px] text-ink-muted">
            Jelajahi seluruh kurikulum. Modul terkunci tetap terlihat — tingkatkan paket untuk membukanya.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <div className="relative min-w-[240px] flex-1">
            <SearchIcon
              size={18}
              className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-ink-subtle"
            />
            <input
              type="text"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Cari modul, tools, atau topik…"
              aria-label="Cari modul"
              className="w-full rounded-sm border border-border bg-surface py-[11px] pl-10 pr-3.5 text-sm text-ink outline-none placeholder:text-ink-subtle focus:border-primary"
            />
          </div>
          <select
            value={sort}
            onChange={(e) => setSort(e.target.value)}
            aria-label="Urutkan"
            className="rounded-sm border border-border bg-surface px-3.5 py-[11px] text-[13.5px] font-semibold text-ink outline-none focus:border-primary"
          >
            {SORTS.map((s) => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-7 md:grid-cols-[260px_1fr]">
        {/* Filter sidebar */}
        <aside className="hidden md:block">
          <div className="sticky top-[86px] flex flex-col gap-5 rounded-lg border border-border bg-surface p-5 shadow-sm">
            <div className="flex items-center justify-between">
              <span className="text-sm font-extrabold text-ink">Filter</span>
              {hasFilters && (
                <button
                  type="button"
                  onClick={resetFilters}
                  className="text-xs font-bold text-primary hover:underline"
                >
                  Atur ulang
                </button>
              )}
            </div>

            <FilterGroup label="Level">
              {initialFacets.levels.map((l) => (
                <CheckRow
                  key={l.slug}
                  checked={levels.includes(l.slug)}
                  onChange={() => setLevels((v) => toggle(v, l.slug))}
                  label={l.name}
                  count={num(l.count)}
                />
              ))}
            </FilterGroup>

            {initialFacets.categories.length > 0 && (
              <FilterGroup label="Kategori" bordered>
                {initialFacets.categories.map((c) => (
                  <CheckRow
                    key={c.slug}
                    checked={categories.includes(c.slug)}
                    onChange={() => setCategories((v) => toggle(v, c.slug))}
                    label={c.name}
                    count={num(c.count)}
                  />
                ))}
              </FilterGroup>
            )}

            {initialFacets.tags.length > 0 && (
              <FilterGroup label="Tag (pilih beberapa)" bordered>
                <div className="flex flex-wrap gap-1.5">
                  {initialFacets.tags.map((t) => {
                    const on = tags.includes(t.slug);
                    return (
                      <button
                        key={t.slug}
                        type="button"
                        onClick={() => setTags((v) => toggle(v, t.slug))}
                        aria-pressed={on}
                        className={
                          "inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold transition-colors " +
                          (on
                            ? "bg-primary text-primary-ink"
                            : "bg-surface-2 text-ink-muted hover:bg-border")
                        }
                      >
                        #{t.name}
                        {on && <XIcon size={11} />}
                      </button>
                    );
                  })}
                </div>
              </FilterGroup>
            )}
          </div>
        </aside>

        {/* Results */}
        <div className="flex flex-col gap-7">
          {/* count + active chips */}
          <div className="flex flex-wrap items-center gap-2.5">
            <span className="text-[13.5px] text-ink-muted">
              <strong className="text-ink">{total} modul</strong>
              {levels.length === 1 && ` di Level ${labelFor("level", levels[0])}`}
            </span>
            {activeChips.map((c) => (
              <button
                key={`${c.kind}:${c.value}`}
                type="button"
                onClick={() => removeChip(c.kind, c.value)}
                className="inline-flex items-center gap-1.5 rounded-full bg-primary-soft px-2.5 py-1 text-xs font-bold text-primary hover:bg-primary-soft/70"
              >
                {labelFor(c.kind, c.value)}
                <XIcon size={11} />
              </button>
            ))}
          </div>

          {query.isError ? (
            <ErrorState
              title="Gagal memuat katalog"
              message="Terjadi kesalahan saat memuat modul. Coba muat ulang."
              action={
                <Button variant="neutral" size="sm" onClick={() => query.refetch()}>
                  Muat ulang
                </Button>
              }
            />
          ) : showSkeleton ? (
            <div className="grid grid-cols-1 gap-[18px] sm:grid-cols-2 lg:grid-cols-3">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-[260px] rounded-base" />
              ))}
            </div>
          ) : showEmpty ? (
            <EmptyState
              title="Tidak ada modul"
              message="Tidak ada modul yang cocok dengan filter Anda. Coba ubah atau atur ulang filter."
              action={
                hasFilters ? (
                  <Button variant="neutral" size="sm" onClick={resetFilters}>
                    Atur ulang filter
                  </Button>
                ) : undefined
              }
            />
          ) : (
            <>
              {groups.map((g) => (
                <div key={g.levelSlug} className="flex flex-col gap-4">
                  <div className="flex flex-wrap items-center gap-3">
                    <Badge tier={tierForLevelSlug(g.levelSlug)} className="rounded-md px-2.5 py-1 text-[11.5px]">
                      Level {g.levelName}
                    </Badge>
                    {g.locked && <Badge status="locked" className="px-2.5 py-1 text-[11.5px]" />}
                  </div>
                  {g.tracks.map((t) => (
                    <div key={t.name} className="flex flex-col gap-4">
                      <div className="flex items-center gap-2 text-ink-subtle">
                        <ChevronRightIcon size={15} />
                        <h2 className="text-[17px] font-extrabold text-ink">Track: {t.name}</h2>
                        <span className="text-[12.5px] text-ink-muted">· {t.mods.length} modul</span>
                      </div>
                      <div className="grid grid-cols-1 gap-[18px] sm:grid-cols-2 lg:grid-cols-3">
                        {t.mods.map((m) => (
                          <ModuleCard
                            key={m.id}
                            title={m.title}
                            levelLabel={m.levelName}
                            state={cardState(m.access)}
                            durationLabel={minutesLabel(m.durationSeconds)}
                            tags={m.tags}
                            ctaLabel={ctaLabel(m)}
                            onCta={() => goToModule(m)}
                          />
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              ))}

              {/* lazy-load */}
              <div className="flex flex-col items-center gap-2.5 pt-2">
                {modules.length < total && (
                  <Button
                    variant="neutral"
                    loading={query.isFetching}
                    onClick={() => setTake((t) => t + PAGE_SIZE)}
                  >
                    Muat lebih banyak
                  </Button>
                )}
                <span className="text-[12.5px] text-ink-subtle">
                  Menampilkan {modules.length} dari {total} modul
                </span>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

function FilterGroup({
  label,
  bordered,
  children,
}: {
  label: string;
  bordered?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div
      className={
        "flex flex-col gap-2.5 " + (bordered ? "border-t border-surface-2 pt-4" : "")
      }
    >
      <span className="text-[11.5px] font-bold uppercase tracking-wide text-ink-subtle">{label}</span>
      {children}
    </div>
  );
}

function CheckRow({
  checked,
  onChange,
  label,
  count,
}: {
  checked: boolean;
  onChange: () => void;
  label: string;
  count: number;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2.5 text-[13.5px] text-ink">
      <input type="checkbox" checked={checked} onChange={onChange} className="sr-only" />
      <span
        aria-hidden
        className={
          "inline-flex h-[17px] w-[17px] items-center justify-center rounded-[4px] " +
          (checked ? "bg-primary text-primary-ink" : "border-[1.5px] border-border")
        }
      >
        {checked && (
          <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={3.2} strokeLinecap="round" strokeLinejoin="round">
            <path d="M20 6 9 17l-5-5" />
          </svg>
        )}
      </span>
      {label} <span className="text-ink-subtle">({count})</span>
    </label>
  );
}
