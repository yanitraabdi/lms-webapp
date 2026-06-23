"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { OnboardingFlow } from "@/components/onboarding/OnboardingFlow";
import { Badge, Button, Spinner, ErrorState, LockIcon, PlayIcon, type Tier } from "@/components/ui";
import { getDashboard, minutesLabel, num, type ContinueModule, type LevelProgress } from "@/lib/learning";

const TIER_BADGE: Record<number, Tier> = { 0: "free", 1: "beginner", 2: "intermediate", 3: "advanced" };

export default function DashboardPage() {
  const { status, accessToken, user } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login?next=/app/dashboard");
  }, [status, router]);

  const dash = useQuery({
    queryKey: ["dashboard"],
    queryFn: () => getDashboard(accessToken!),
    enabled: status === "authenticated" && !!accessToken,
  });

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }

  const firstName = (user?.name ?? "").split(" ")[0] || "kembali";
  const tier = dash.data?.activeTier == null ? 0 : num(dash.data.activeTier);

  return (
    <div className="min-h-screen bg-bg">
      <AppHeader />
      <OnboardingFlow token={accessToken} />
      <div className="mx-auto max-w-6xl px-6 pb-16 pt-7">
        {/* greeting + plan */}
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <div className="flex flex-col gap-1">
            <h1 className="text-[24px] font-extrabold tracking-tight">Selamat datang kembali, {firstName} 👋</h1>
            <p className="text-sm text-ink-muted">Lanjutkan dari tempat Anda berhenti.</p>
          </div>
          <div className="flex items-center gap-3 rounded-lg border border-border bg-surface px-3.5 py-2.5 shadow-sm">
            <Badge tier={TIER_BADGE[tier]} />
            {tier < 3 && <Link href={tier === 0 ? "/pricing" : "/app/account"} className="text-[12.5px] font-bold text-primary hover:underline">Tingkatkan →</Link>}
          </div>
        </div>

        {dash.isPending ? (
          <div className="flex min-h-[200px] items-center justify-center"><Spinner size={24} /></div>
        ) : dash.isError ? (
          <ErrorState title="Gagal memuat dasbor" action={<Button variant="neutral" size="sm" onClick={() => dash.refetch()}>Muat ulang</Button>} />
        ) : (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px] lg:items-start">
            {/* left */}
            <div className="flex flex-col gap-7">
              {dash.data.continueLearning.length > 0 && (
                <section className="flex flex-col gap-3">
                  <h2 className="text-base font-extrabold">Lanjutkan menonton</h2>
                  {dash.data.continueLearning.map((m) => <ContinueCard key={m.moduleId} m={m} wide />)}
                </section>
              )}

              {dash.data.recommendedNext.length > 0 && (
                <section className="flex flex-col gap-3">
                  <div className="flex items-center gap-2">
                    <h2 className="text-base font-extrabold">Rekomendasi berikutnya</h2>
                    <span className="text-[11px] text-ink-subtle">· modul belum selesai berikutnya</span>
                  </div>
                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    {dash.data.recommendedNext.map((m) => <ContinueCard key={m.moduleId} m={m} />)}
                  </div>
                </section>
              )}

              {dash.data.continueLearning.length === 0 && dash.data.recommendedNext.length === 0 && (
                <div className="rounded-lg border border-border bg-surface p-8 text-center shadow-sm">
                  <p className="text-sm text-ink-muted">
                    {tier === 0 ? "Berlangganan untuk mulai belajar." : "Anda telah menyelesaikan semua modul yang tersedia. 🎉"}
                  </p>
                  <Link href={tier === 0 ? "/pricing" : "/catalog"} className="mt-3 inline-block text-sm font-bold text-primary hover:underline">
                    {tier === 0 ? "Lihat paket" : "Jelajahi katalog"}
                  </Link>
                </div>
              )}
            </div>

            {/* right: progress rollups */}
            <aside className="flex flex-col gap-4">
              <div data-tour="overall-progress" className="flex flex-col items-center gap-3.5 rounded-lg border border-border bg-surface p-5 text-center shadow-sm">
                <span className="text-xs font-bold uppercase tracking-wide text-ink-subtle">Progres keseluruhan</span>
                <Donut percent={num(dash.data.overall.percent)} label={`${num(dash.data.overall.completedCount)}/${num(dash.data.overall.totalCount)} modul`} />
              </div>
              <div className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-5 shadow-sm">
                <span className="text-xs font-bold uppercase tracking-wide text-ink-subtle">Progres per Level</span>
                {dash.data.levels.map((l) => <LevelRow key={l.levelId} l={l} />)}
              </div>
            </aside>
          </div>
        )}
      </div>
    </div>
  );
}

function ContinueCard({ m, wide }: { m: ContinueModule; wide?: boolean }) {
  const pct = Math.round(num(m.percentComplete));
  if (wide) {
    return (
      <div className="flex items-center gap-4 rounded-lg border border-border bg-surface p-4 shadow-sm">
        <Link href={`/app/learn/${m.moduleId}`} className="relative flex aspect-video w-[150px] shrink-0 items-center justify-center rounded-lg bg-[linear-gradient(135deg,#0050E6,#2A6BFF)]">
          <span className="flex h-9 w-9 items-center justify-center rounded-full bg-white/95 text-primary"><PlayIcon size={15} /></span>
        </Link>
        <div className="flex min-w-0 flex-1 flex-col gap-2">
          <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">{m.levelName} · {m.trackName}</span>
          <h3 className="truncate text-base font-bold">{m.title}</h3>
          <div className="h-1.5 overflow-hidden rounded-full bg-surface-2"><div className="h-full bg-primary" style={{ width: `${pct}%` }} /></div>
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-ink-muted">{pct}% · {minutesLabel(m.durationSeconds)}</span>
            <Link href={`/app/learn/${m.moduleId}`} className="rounded-sm bg-primary px-4 py-2 text-[13px] font-bold text-primary-ink hover:bg-primary-hover">Lanjutkan</Link>
          </div>
        </div>
      </div>
    );
  }
  return (
    <article className="flex flex-col overflow-hidden rounded-base border border-border bg-surface shadow-sm">
      <Link href={`/app/learn/${m.moduleId}`} className="relative flex aspect-video items-center justify-center bg-[linear-gradient(135deg,#0050E6,#2A6BFF)]">
        <span className="flex h-9 w-9 items-center justify-center rounded-full bg-white/95 text-primary"><PlayIcon size={14} /></span>
        <span className="absolute bottom-2 right-2 rounded-sm bg-black/55 px-1.5 py-0.5 text-[11px] font-semibold text-white">{minutesLabel(m.durationSeconds)}</span>
      </Link>
      <div className="flex flex-col gap-2 p-3.5">
        <span className="w-fit rounded-sm bg-surface-2 px-2 py-1 text-[10px] font-semibold text-ink-muted">{m.levelName}</span>
        <h4 className="text-sm font-bold leading-snug">{m.title}</h4>
        <Link href={`/app/learn/${m.moduleId}`} className="mt-0.5 rounded-sm bg-primary py-2 text-center text-[12.5px] font-bold text-primary-ink hover:bg-primary-hover">
          {pct > 0 ? "Lanjutkan" : "Mulai modul"}
        </Link>
      </div>
    </article>
  );
}

function LevelRow({ l }: { l: LevelProgress }) {
  const pct = num(l.percent);
  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <span className="inline-flex items-center gap-2 text-[13px] font-bold">
          <Badge tier={TIER_BADGE[num(l.tierLevel)]} className="px-2 py-0.5 text-[10px]">{l.name}</Badge>
          {!l.unlocked && <LockIcon size={12} className="text-ink-subtle" />}
          {l.certified && <span className="text-[10px] font-extrabold text-success">Lulus</span>}
        </span>
        {l.unlocked ? (
          <span className={"text-[12.5px] font-bold " + (l.certified ? "text-success" : "text-ink")}>{num(l.completedCount)}/{num(l.publishedCount)} · {pct}%</span>
        ) : (
          <Link href="/pricing" className="text-[11.5px] font-bold text-primary hover:underline">Buka</Link>
        )}
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-surface-2">
        <div className={"h-full " + (l.certified ? "bg-success" : "bg-primary")} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

function Donut({ percent, label }: { percent: number; label: string }) {
  return (
    <div
      className="flex h-[120px] w-[120px] items-center justify-center rounded-full"
      style={{ background: `conic-gradient(var(--ds-color-primary) 0 ${percent}%, var(--ds-color-surface-2) ${percent}% 100%)` }}
    >
      <div className="flex h-[92px] w-[92px] flex-col items-center justify-center rounded-full bg-surface">
        <span className="text-[28px] font-extrabold text-ink">{percent}%</span>
        <span className="text-[11px] text-ink-muted">{label}</span>
      </div>
    </div>
  );
}
