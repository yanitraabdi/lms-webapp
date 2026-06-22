import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Badge, ChevronRightIcon, LockIcon, PlayIcon } from "@/components/ui";
import { fetchModule, minutesLabel, tierForLevelSlug } from "@/lib/catalog";
import { ModuleAccessCta } from "./ModuleAccessCta";

// /modules/[slug] — public, SEO. Metadata + content always render (anonymous,
// ISR); per-user access for the CTA is resolved client-side.
export const revalidate = 3600;
export const dynamicParams = true;

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const m = await fetchModule(slug, { revalidate }).catch(() => null);
  if (!m) return { title: "Modul tidak ditemukan — AI Productivity Academy" };
  const description = m.summary ?? m.description.slice(0, 160);
  return {
    title: `${m.title} — AI Productivity Academy`,
    description,
    openGraph: {
      title: m.title,
      description,
      type: "website",
      images: m.thumbnailUrl ? [m.thumbnailUrl] : undefined,
    },
  };
}

export default async function ModuleDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const m = await fetchModule(slug, { revalidate }).catch(() => null);
  if (!m) notFound();

  const locked = m.access === "Locked";
  const duration = minutesLabel(m.durationSeconds);

  return (
    <div className="mx-auto max-w-5xl px-6 pb-16 pt-5">
      {/* breadcrumb */}
      <nav className="mb-[22px] flex flex-wrap items-center gap-2 text-[12.5px] text-ink-muted">
        <Link href="/catalog" className="hover:text-ink">
          Katalog
        </Link>
        <span className="text-ink-subtle">/</span>
        <Link href={`/catalog/${m.levelSlug}`} className="hover:text-ink">
          Level {m.levelName}
        </Link>
        <span className="text-ink-subtle">/</span>
        <span className="text-ink-muted">Track: {m.trackName}</span>
        <span className="text-ink-subtle">/</span>
        <span className="font-semibold text-ink">{m.title}</span>
      </nav>

      <div className="grid grid-cols-1 items-start gap-8 lg:grid-cols-[1fr_360px]">
        {/* main */}
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-3.5">
            <div className="flex flex-wrap gap-2">
              <Badge tier={tierForLevelSlug(m.levelSlug)} className="rounded-md px-2.5 py-1 text-[11.5px]">
                {m.levelName}
              </Badge>
              <Badge tone="neutral" className="rounded-md px-2.5 py-1 text-[11.5px]">
                {duration}
              </Badge>
              <Badge tone="neutral" className="rounded-md px-2.5 py-1 text-[11.5px]">
                Track: {m.trackName}
              </Badge>
            </div>
            <h1 className="text-[32px] font-extrabold leading-tight tracking-tight text-ink">{m.title}</h1>
            {m.summary && <p className="text-[15px] leading-relaxed text-ink-muted">{m.summary}</p>}
          </div>

          {/* player / thumbnail */}
          <div className="relative flex aspect-video items-center justify-center overflow-hidden rounded-lg bg-[linear-gradient(135deg,#11253F,#1E3A5F)] shadow-sm">
            {locked ? (
              <span className="flex h-[72px] w-[72px] items-center justify-center rounded-full border-2 border-white/50 bg-white/[0.18] text-white">
                <LockIcon size={30} strokeWidth={1.8} />
              </span>
            ) : (
              <span className="flex h-[72px] w-[72px] items-center justify-center rounded-full bg-white/95 text-primary shadow-lg">
                <PlayIcon size={28} />
              </span>
            )}
            <span className="absolute bottom-3 right-3 rounded-md bg-black/55 px-2.5 py-1 text-xs font-semibold text-white">
              {duration}
            </span>
            {locked && (
              <span className="absolute left-3 top-3 inline-flex items-center gap-1.5 rounded-full bg-black/50 px-2.5 py-1 text-[11.5px] font-bold text-white">
                <LockIcon size={12} />
                Terkunci · Pratinjau tidak tersedia
              </span>
            )}
          </div>

          {/* description */}
          <div className="flex flex-col gap-3">
            <h2 className="text-[19px] font-extrabold text-ink">Tentang modul ini</h2>
            {m.description.split(/\n{2,}/).map((para, i) => (
              <p key={i} className="text-[15px] leading-relaxed text-ink-muted">
                {para}
              </p>
            ))}
          </div>

          {/* resources */}
          {m.resources.length > 0 && (
            <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-6">
              <h2 className="text-[17px] font-extrabold text-ink">Materi modul</h2>
              <ul className="flex flex-col gap-2.5">
                {m.resources.map((r, i) => (
                  <li key={i} className="flex items-center gap-2.5 text-sm text-ink-muted">
                    <ChevronRightIcon size={15} className="text-ink-subtle" />
                    <span className="text-ink">{r.title}</span>
                    <span className="text-[11.5px] uppercase tracking-wide text-ink-subtle">{r.type}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* tags */}
          {m.tags.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {m.tags.map((t) => (
                <span
                  key={t}
                  className="rounded-full bg-surface-2 px-3 py-1.5 text-[12.5px] font-semibold text-ink-muted"
                >
                  #{t}
                </span>
              ))}
            </div>
          )}
        </div>

        {/* sticky CTA sidebar */}
        <aside className="lg:sticky lg:top-5">
          <ModuleAccessCta
            slug={m.slug}
            moduleId={m.id}
            initialAccess={m.access}
            levelName={m.levelName}
            durationLabel={duration}
            resourceCount={m.resources.length}
          />
        </aside>
      </div>
    </div>
  );
}
