"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Spinner, ErrorState, CheckIcon, ChevronRightIcon, PlayIcon } from "@/components/ui";
import { VideoPlayer } from "@/components/learn/VideoPlayer";
import { QuizPanel } from "@/components/learn/QuizPanel";
import { NotesPanel } from "@/components/learn/NotesPanel";
import { RatingWidget } from "@/components/learn/RatingWidget";
import {
  getPlayback,
  getPlayerContext,
  getProgress,
  saveProgress,
  minutesLabel,
  num,
} from "@/lib/learning";

export default function LearnPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();
  const id = useParams<{ id: string }>().id;

  useEffect(() => {
    if (status === "unauthenticated") router.replace(`/login?next=/app/learn/${id}`);
  }, [status, id, router]);

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  return <Player token={accessToken} moduleId={id} />;
}

function Player({ token, moduleId }: { token: string; moduleId: string }) {
  const router = useRouter();
  const qc = useQueryClient();
  const ctx = useQuery({ queryKey: ["player", moduleId], queryFn: () => getPlayerContext(token, moduleId) });
  const ticket = useQuery({ queryKey: ["playback", moduleId], queryFn: () => getPlayback(token, moduleId) });
  const progress0 = useQuery({ queryKey: ["progress", moduleId], queryFn: () => getProgress(token, moduleId) });

  const [pct, setPct] = useState(0);
  const [completed, setCompleted] = useState(false);
  const [tab, setTab] = useState<"about" | "materi">("about");
  const saving = useRef(false);
  const positionRef = useRef(0);

  function onModuleCompleted() {
    setCompleted(true);
    qc.invalidateQueries({ queryKey: ["progress", moduleId] });
    qc.invalidateQueries({ queryKey: ["player", moduleId] });
  }

  useEffect(() => {
    if (progress0.data) {
      setPct(num(progress0.data.percentComplete));
      setCompleted(progress0.data.completed);
    }
  }, [progress0.data]);

  async function onProgress(position: number, percent: number) {
    positionRef.current = position;
    setPct((p) => Math.max(p, percent));
    if (saving.current) return;
    saving.current = true;
    try {
      const saved = await saveProgress(token, moduleId, position, percent);
      if (saved.completed) setCompleted(true);
    } catch {
      /* transient — next tick retries */
    } finally {
      saving.current = false;
    }
  }

  if (ctx.isPending || ticket.isPending || progress0.isPending) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  if (ctx.isError || ticket.isError) {
    return (
      <div className="mx-auto max-w-md px-6 py-20">
        <ErrorState
          title="Gagal memuat modul"
          message="Anda mungkin belum memiliki akses, atau terjadi kesalahan."
          action={<Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Kembali ke dasbor</Link>}
        />
      </div>
    );
  }

  const c = ctx.data;
  const resume = progress0.data ? num(progress0.data.resumePositionSeconds) : 0;
  const displayPct = Math.round(Math.max(pct, completed ? 100 : 0));

  return (
    <div className="min-h-screen bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex h-[58px] max-w-6xl items-center justify-between gap-3 px-6">
          <div className="flex min-w-0 items-center gap-3">
            <Link href="/app/dashboard" className="inline-flex items-center gap-1.5 text-[13px] font-bold text-ink-muted hover:text-ink">
              <ChevronRightIcon size={16} className="rotate-180" /> Kembali
            </Link>
            <span className="h-5 w-px bg-border" />
            <span className="truncate text-sm font-bold">{c.title}</span>
          </div>
        </div>
      </header>

      <div className="mx-auto grid max-w-6xl grid-cols-1 items-start gap-6 px-6 py-6 lg:grid-cols-[1fr_340px]">
        <main className="flex flex-col gap-5">
          <div className="overflow-hidden rounded-lg shadow-sm">
            <VideoPlayer
              src={ticket.data.url}
              captionsSrc={ticket.data.captionsUrl}
              resumeSeconds={resume}
              onProgress={onProgress}
            />
          </div>

          <div className="flex flex-col gap-3">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="flex flex-col gap-1.5">
                <span className="text-xs font-bold uppercase tracking-wide text-ink-subtle">
                  {c.levelName} · {c.trackName} · Modul {c.moduleNumber}/{c.trackCount}
                </span>
                <h1 className="text-[23px] font-extrabold tracking-tight">{c.title}</h1>
              </div>
              <div className="flex items-center gap-2.5 rounded-lg bg-primary-soft px-3.5 py-2">
                {completed ? (
                  <Badge status="completed" className="px-2.5 py-0.5" />
                ) : (
                  <>
                    <span className="text-[12.5px] text-ink-muted">Progres modul</span>
                    <span className="text-[15px] font-extrabold text-primary">{displayPct}%</span>
                  </>
                )}
              </div>
            </div>
            <div className="h-[7px] overflow-hidden rounded-full bg-surface-2">
              <div
                className={"h-full rounded-full " + (completed ? "bg-success" : "bg-primary")}
                style={{ width: `${displayPct}%` }}
              />
            </div>
            <span className="text-xs text-ink-subtle">Modul ditandai selesai otomatis saat Anda menonton ~90%.</span>
          </div>

          {/* tabs */}
          <div className="flex gap-1 border-b border-border">
            <TabButton active={tab === "about"} onClick={() => setTab("about")}>Tentang</TabButton>
            <TabButton active={tab === "materi"} onClick={() => setTab("materi")}>
              Materi unduhan {c.resources.length > 0 && `(${c.resources.length})`}
            </TabButton>
          </div>

          {tab === "about" ? (
            <p className="text-[14.5px] leading-relaxed text-ink-muted">{c.description}</p>
          ) : c.resources.length === 0 ? (
            <p className="text-sm text-ink-muted">Belum ada materi unduhan untuk modul ini.</p>
          ) : (
            <ul className="flex flex-col gap-2.5">
              {c.resources.map((r, i) => (
                <li key={i} className="flex items-center gap-3 rounded-base border border-border bg-surface p-3.5">
                  <span className="rounded-md bg-surface-2 px-2 py-1 text-[10.5px] font-bold uppercase text-ink-muted">{r.type}</span>
                  <span className="text-sm font-semibold text-ink">{r.title}</span>
                </li>
              ))}
            </ul>
          )}

          {/* quiz (renders only if the module has an active quiz) */}
          <QuizPanel token={token} moduleId={moduleId} onCompleted={onModuleCompleted} />

          {/* rate this module */}
          <RatingWidget token={token} moduleId={moduleId} />
        </main>

        {/* playlist + next */}
        <aside className="flex flex-col gap-4">
          <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
            <div className="border-b border-border px-[18px] py-4">
              <div className="flex items-center justify-between">
                <span className="text-sm font-extrabold">Track: {c.trackName}</span>
                <span className="text-xs text-ink-muted">{c.completedInTrack}/{c.trackCount} selesai</span>
              </div>
              <div className="mt-2.5 h-1.5 overflow-hidden rounded-full bg-surface-2">
                <div className="h-full bg-primary" style={{ width: `${num(c.trackCount) ? (num(c.completedInTrack) / num(c.trackCount)) * 100 : 0}%` }} />
              </div>
            </div>
            <ul>
              {c.playlist.map((item, i) => (
                <li key={item.id}>
                  <button
                    type="button"
                    onClick={() => !item.isCurrent && router.push(`/app/learn/${item.id}`)}
                    className={
                      "flex w-full items-center gap-3 border-b border-surface-2 px-[18px] py-3.5 text-left last:border-0 " +
                      (item.isCurrent ? "bg-primary-soft" : "hover:bg-surface-2")
                    }
                  >
                    <span
                      className={
                        "inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-[11px] font-bold " +
                        (item.completed
                          ? "bg-success text-white"
                          : item.isCurrent
                            ? "bg-primary text-white"
                            : "border-2 border-border text-ink-subtle")
                      }
                    >
                      {item.completed ? <CheckIcon size={13} strokeWidth={3} /> : item.isCurrent ? <PlayIcon size={11} /> : i + 1}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className={"block truncate text-[13px] " + (item.isCurrent ? "font-extrabold text-ink" : "font-semibold text-ink-muted")}>
                        {item.title}
                      </span>
                      <span className="block text-[11.5px] text-ink-subtle">
                        {minutesLabel(item.durationSeconds)}{item.completed ? " · Selesai" : item.isCurrent ? " · Sedang diputar" : ""}
                      </span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          </div>

          {c.nextModuleId && (
            <Button fullWidth onClick={() => router.push(`/app/learn/${c.nextModuleId}`)}>
              Modul berikutnya →
            </Button>
          )}

          <NotesPanel token={token} moduleId={moduleId} getPosition={() => positionRef.current} />
        </aside>
      </div>
    </div>
  );
}

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={
        "-mb-px border-b-2 px-3.5 py-2.5 text-[13.5px] font-bold " +
        (active ? "border-primary text-primary" : "border-transparent text-ink-muted hover:text-ink")
      }
    >
      {children}
    </button>
  );
}
