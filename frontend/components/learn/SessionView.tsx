"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Badge, Button, Spinner, ErrorState, ChevronRightIcon } from "@/components/ui";
import { VideoPlayer } from "@/components/learn/VideoPlayer";
import { GatingTest } from "@/components/learn/GatingTest";
import {
  getSessionContext, getPlayback, saveSessionProgress, num,
  type SessionContext,
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
        {s.progress.completed && <Badge status="completed" className="px-2.5 py-0.5" />}
      </div>

      {s.type === "Video" ? (
        <VideoSection token={token} session={s} onProgressChanged={refresh} />
      ) : s.type === "Live" ? (
        <LiveSection session={s} />
      ) : (
        <FinalSection sessionId={sessionId} completed={s.progress.completed} />
      )}

      {s.type === "Video" && (
        <GatingTest
          token={token}
          sessionId={sessionId}
          watchThresholdMet={s.progress.watchThresholdMet}
          onPassed={refresh}
        />
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

function VideoSection({
  token, session, onProgressChanged,
}: { token: string; session: SessionContext; onProgressChanged: () => void }) {
  const ticket = useQuery({
    queryKey: ["session-playback", session.id],
    queryFn: () => getPlayback(token, session.id),
  });

  const [pct, setPct] = useState(num(session.progress.percentComplete));
  const saving = useRef(false);
  const thresholdMet = useRef(session.progress.watchThresholdMet);

  async function onProgress(position: number, percent: number) {
    setPct((p) => Math.max(p, percent));
    if (saving.current) return;
    saving.current = true;
    try {
      const saved = await saveSessionProgress(token, session.id, position, percent);
      // Refresh once the threshold is first crossed so the gating test unlocks.
      if (saved.watchThresholdMet && !thresholdMet.current) {
        thresholdMet.current = true;
        onProgressChanged();
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

  const display = Math.round(Math.max(pct, session.progress.completed ? 100 : 0));

  return (
    <>
      <div className="overflow-hidden rounded-lg shadow-sm">
        <VideoPlayer
          src={ticket.data.url}
          captionsSrc={ticket.data.captionsUrl}
          resumeSeconds={num(session.progress.resumePositionSeconds)}
          onProgress={onProgress}
        />
      </div>
      <div className="flex flex-col gap-2">
        <div className="flex items-center justify-between text-[12.5px]">
          <span className="text-ink-muted">
            {session.durationSeconds != null && minutesLabel(session.durationSeconds)}
          </span>
          <span className="font-bold text-primary">{display}%</span>
        </div>
        <div className="h-[7px] overflow-hidden rounded-full bg-surface-2">
          <div
            className={"h-full rounded-full " + (session.progress.completed ? "bg-success" : "bg-primary")}
            style={{ width: `${display}%` }}
          />
        </div>
        <span className="text-[11.5px] text-ink-subtle">
          Tes sesi terbuka setelah Anda menonton ~90% video.
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
