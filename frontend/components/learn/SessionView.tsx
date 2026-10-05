"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Badge, Button, Spinner, ErrorState, ChevronRightIcon, LockIcon } from "@/components/ui";
import { VideoPlayer } from "@/components/learn/VideoPlayer";
import { GatingTest } from "@/components/learn/GatingTest";
import {
  getSessionContext, getPartPlayback, savePartProgress, num,
  type SessionContext, type SessionPart,
} from "@/lib/sessions";
import { fmtDateTime, minutesLabel } from "@/lib/programs";

/**
 * One session, in full: the video and its progress, or live details, or the final-exam entry —
 * plus the gating test where there is one.
 *
 * Rendered in two places. `/app/session/{id}` shows it as a standalone page; the programme page
 * shows it in the right pane of a master-detail layout. Same component either way, so the two can
 * never drift.
 */
export function SessionView({
  token,
  sessionId,
  embedded = false,
  onSessionChanged,
}: {
  token: string;
  sessionId: string;
  /** Drop the standalone page's own header and page chrome — the host already provides them. */
  embedded?: boolean;
  /**
   * Fired when something happens that changes what the SESSION LIST should show: a passed gating
   * test, or watch progress crossing the completion threshold. Standalone, the learner navigates
   * back and the list refetches on its own; embedded, nothing navigates, so without this the list
   * keeps showing the next session as locked after the learner has just unlocked it.
   */
  onSessionChanged?: () => void;
}) {
  const qc = useQueryClient();
  const router = useRouter();

  const ctx = useQuery({
    queryKey: ["session-context", sessionId],
    queryFn: () => getSessionContext(token, sessionId),
    retry: false,
  });

  if (ctx.isPending) {
    return (
      <div className={"flex items-center justify-center " + (embedded ? "min-h-[320px]" : "min-h-screen bg-bg")}>
        <Spinner size={24} />
      </div>
    );
  }
  if (ctx.isError) {
    return (
      <div className={embedded ? "py-10" : "mx-auto max-w-md px-6 py-20"}>
        <ErrorState
          title="Sesi terkunci"
          message="Selesaikan sesi sebelumnya terlebih dahulu, atau pastikan pendaftaran Anda aktif."
          action={
            embedded ? undefined : (
              <Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">
                Kembali ke dasbor
              </Link>
            )
          }
        />
      </div>
    );
  }

  const s = ctx.data;
  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["session-context", sessionId] });
    onSessionChanged?.();
  };

  const body = (
    <>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h1 className="text-[23px] font-extrabold tracking-tight">{s.title}</h1>
          {s.description && <p className="text-[14px] text-ink-muted">{s.description}</p>}
        </div>
        {s.completed && <Badge status="completed" className="px-2.5 py-0.5" />}
      </div>

      {s.type === "Video" ? (
        <PartsSection token={token} session={s} onChanged={refresh} />
      ) : s.type === "Live" ? (
        <LiveSection session={s} />
      ) : (
        <FinalSection sessionId={sessionId} completed={s.completed} />
      )}

      {/* Embedded, the list is right there — a "next session" button would be a second way to do
          the same thing, and the one that goes stale. The sentence still earns its place. */}
      {s.nextSessionId && (
        <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-surface px-5 py-4">
          <span className="text-[13.5px] text-ink-muted">
            {s.nextSessionUnlocked ? "Sesi berikutnya sudah terbuka." : "Selesaikan sesi ini untuk membuka sesi berikutnya."}
          </span>
          {!embedded && (
            <Button
              size="sm"
              disabled={!s.nextSessionUnlocked}
              onClick={() => router.push(`/app/session/${s.nextSessionId}`)}
            >
              Sesi berikutnya →
            </Button>
          )}
        </div>
      )}
    </>
  );

  if (embedded) return <div className="flex flex-col gap-5">{body}</div>;

  return (
    <div className="min-h-screen bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex h-[58px] max-w-4xl items-center justify-between gap-3 px-6">
          <Link
            href={`/app/program/${s.programId}`}
            className="inline-flex items-center gap-1.5 text-[13px] font-bold text-ink-muted hover:text-ink"
          >
            <ChevronRightIcon size={16} className="rotate-180" /> {s.programName}
          </Link>
          <span className="text-[12.5px] text-ink-subtle">Sesi {num(s.orderIndex)}</span>
        </div>
      </header>
      <main className="mx-auto flex max-w-4xl flex-col gap-5 px-6 py-6">{body}</main>
    </div>
  );
}

const PART_LABEL: Record<string, string> = { LessonVideo: "Video materi", Test: "Tes", Discussion: "Video pembahasan" };

function PartsSection({ token, session, onChanged }: { token: string; session: SessionContext; onChanged: () => void }) {
  const parts = session.parts;
  const nextOpen = parts.find((p) => p.status === "Open");
  const [activeId, setActiveId] = useState<string | undefined>(nextOpen?.id ?? parts.at(-1)?.id);

  // Move forward on its own only after a VIDEO part plays to its end AND the data shows it Done
  // (either can come first). Never at the ~90% save, which would cut the lesson off, and never
  // from a test — its result stays on screen with a button to go on.
  const [advanceFrom, setAdvanceFrom] = useState<string | null>(null);
  useEffect(() => {
    if (!advanceFrom || advanceFrom !== activeId) return;
    const current = parts.find((p) => p.id === advanceFrom);
    if (current?.status === "Done" && nextOpen) {
      setAdvanceFrom(null);
      setActiveId(nextOpen.id);
    }
  }, [advanceFrom, parts, activeId, nextOpen]);

  const active = parts.find((p) => p.id === activeId && p.status !== "Locked") ?? nextOpen;

  if (parts.length === 0) {
    // Not an error: the admin has not added content yet.
    return (
      <div className="rounded-lg border border-border bg-surface p-5 text-[13.5px] text-ink-muted">
        Materi sesi ini sedang disiapkan.
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <ol className="flex flex-col divide-y divide-border rounded-lg border border-border bg-surface">
        {parts.map((p, i) => {
          const label = `${i + 1}. ${PART_LABEL[p.kind] ?? p.kind} — ${p.title}`;
          // Locked rows are plain text at full contrast with a lock and the Badge's locked style —
          // never a faded control (WCAG AA). Open/Done rows are buttons.
          return (
            <li key={p.id}>
              {p.status === "Locked" ? (
                <div className="flex items-center justify-between gap-3 px-4 py-3 text-[13.5px] text-ink-muted">
                  <span className="flex min-w-0 items-center gap-2"><LockIcon size={14} className="shrink-0" /><span className="truncate">{label}</span></span>
                  <Badge status="locked" />
                </div>
              ) : (
                <button
                  type="button"
                  onClick={() => setActiveId(p.id)}
                  aria-current={active?.id === p.id ? "step" : undefined}
                  className={
                    "flex w-full items-center justify-between gap-3 px-4 py-3 text-left text-[13.5px] focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary " +
                    (active?.id === p.id ? "bg-primary-soft/60 font-bold" : "hover:bg-surface-2")
                  }
                >
                  <span className="min-w-0 truncate">{label}</span>
                  {p.status === "Done" ? <Badge status="completed" /> : <span className="shrink-0 text-[11.5px] font-bold text-primary">Tersedia</span>}
                </button>
              )}
            </li>
          );
        })}
      </ol>
      {active && (active.kind === "Test"
        ? <GatingTest key={active.id} token={token} sessionId={session.id} part={active} onChanged={onChanged}
            morePartsFollow={parts.indexOf(active) < parts.length - 1}
            onNext={nextOpen && parts.indexOf(nextOpen) > parts.indexOf(active) ? () => setActiveId(nextOpen.id) : undefined} />
        : <VideoPart key={active.id} token={token} sessionId={session.id} part={active} onChanged={onChanged}
            onEnded={() => setAdvanceFrom(active.id)} />)}
    </div>
  );
}

function VideoPart({
  token, sessionId, part, onChanged, onEnded,
}: { token: string; sessionId: string; part: SessionPart; onChanged: () => void; onEnded: () => void }) {
  const ticket = useQuery({
    queryKey: ["part-playback", part.id],
    queryFn: () => getPartPlayback(token, sessionId, part.id),
  });

  const [pct, setPct] = useState(num(part.percentComplete));
  const saving = useRef(false);
  const done = useRef(part.status === "Done");

  async function onProgress(position: number, percent: number) {
    setPct((p) => Math.max(p, percent));
    if (saving.current) return;
    saving.current = true;
    try {
      const saved = await savePartProgress(token, sessionId, part.id, position, percent);
      // Refresh once the part first turns done so the next part unlocks.
      if (saved.done && !done.current) {
        done.current = true;
        onChanged();
      }
    } catch {
      /* transient — the next tick retries */
    } finally {
      saving.current = false;
    }
  }

  if (ticket.isPending) {
    return <div className="flex min-h-[220px] items-center justify-center rounded-lg border border-border bg-surface"><Spinner size={22} /></div>;
  }
  if (ticket.isError) {
    return <ErrorState title="Video tidak dapat diputar" message="Coba muat ulang halaman." />;
  }

  const finished = part.status === "Done";
  const display = Math.round(Math.max(pct, finished ? 100 : 0));

  return (
    <>
      <div className="overflow-hidden rounded-lg shadow-sm">
        <VideoPlayer
          src={ticket.data.url}
          captionsSrc={ticket.data.captionsUrl}
          resumeSeconds={num(part.resumePositionSeconds)}
          onProgress={onProgress}
          onEnded={onEnded}
        />
      </div>
      <div className="flex flex-col gap-2">
        <div className="flex items-center justify-between text-[12.5px]">
          <span className="text-ink-muted">
            {part.durationSeconds != null && minutesLabel(part.durationSeconds)}
          </span>
          <span className="font-bold text-primary">{display}%</span>
        </div>
        <div className="h-[7px] overflow-hidden rounded-full bg-surface-2">
          <div
            className={"h-full rounded-full " + (finished ? "bg-success" : "bg-primary")}
            style={{ width: `${display}%` }}
          />
        </div>
        <span className="text-[11.5px] text-ink-subtle">
          Bagian berikutnya terbuka setelah Anda menonton ~90% video.
        </span>
      </div>
    </>
  );
}

function LiveSection({ session }: { session: SessionContext }) {
  return (
    <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <h3 className="text-base font-extrabold">Sesi live</h3>
      {session.scheduledAt && (
        <p className="mt-1 text-[13.5px] text-ink-muted">Jadwal: {fmtDateTime(session.scheduledAt)}</p>
      )}
      {session.location && <p className="mt-1 text-[13.5px] text-ink-muted">Lokasi: {session.location}</p>}
      {session.joinUrl && (
        <a
          href={session.joinUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-3 inline-block text-[13.5px] font-bold text-primary hover:underline"
        >
          Buka tautan sesi live →
        </a>
      )}
      <p className="mt-4 text-[12.5px] leading-relaxed text-ink-subtle">
        Kehadiran ditandai oleh admin setelah sesi berlangsung. Sesi berikutnya terbuka setelah
        kehadiran Anda dicatat.
      </p>
    </div>
  );
}

function FinalSection({ sessionId, completed }: { sessionId: string; completed: boolean }) {
  return (
    <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <h3 className="text-base font-extrabold">Tes akhir</h3>
      <p className="mt-1 text-[13.5px] leading-relaxed text-ink-muted">
        Simulasi TOEFL ITP berwaktu dengan tiga bagian (Listening, Structure, Reading) dan
        pengawasan lunak. Waktu berjalan di server dan tidak dapat dijeda.
      </p>
      <Link
        href={`/app/assessment/${sessionId}`}
        className="mt-4 inline-flex h-10 items-center justify-center rounded-sm bg-primary px-4 text-[13px] font-bold text-primary-ink hover:bg-primary-hover"
      >
        {completed ? "Lihat hasil tes" : "Mulai tes akhir"}
      </Link>
      <p className="mt-3 text-[12px] leading-relaxed text-ink-subtle">
        Skor yang dihasilkan adalah prediksi INVERTA, bukan skor TOEFL resmi dari ETS.
      </p>
    </div>
  );
}
