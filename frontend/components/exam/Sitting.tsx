"use client";

import { useEffect, useRef, useState } from "react";
import { Button, AlertTriangleIcon } from "@/components/ui";
import { LiveAudioPlayer } from "@/components/learn/LiveAudioPlayer";
import { prefersReducedMotion } from "@/lib/useCssVar";
import { getAudioUrl, startSectionAudio, clock, num, type AttemptState } from "@/lib/sessions";

export function Sitting({
  token, state, answers, remaining, busy, error, leaving, onAnswer, onSave, onNext,
}: {
  token: string;
  state: AttemptState;
  answers: Record<string, number>;
  remaining: number;
  busy: boolean;
  error: string | null;
  /** True for the moment between the server accepting /advance and the next section being shown. */
  leaving: boolean;
  onAnswer: (qid: string, choice: number) => void;
  /** Resolves true when the server stored the answers. */
  onSave: () => Promise<boolean>;
  onNext: () => void;
}) {
  const answered = state.questions.filter((q) => answers[q.id] !== undefined).length;
  const isLast = num(state.sectionIndex) >= num(state.sectionCount) - 1;
  const low = remaining <= 60;

  // Persist answers periodically so a crashed tab never loses the sitting.
  const [savedAt, setSavedAt] = useState<Date | null>(null);
  const saveRef = useRef(onSave);
  saveRef.current = onSave;
  useEffect(() => {
    const t = setInterval(() => void saveRef.current().then((ok) => ok && setSavedAt(new Date())), 15000);
    return () => clearInterval(t);
  }, []);

  // A new section: start the learner at its top, with focus on its name. The first section on
  // mount (including a resume) does not animate in.
  const firstSection = useRef(state.sectionIndex);
  const sectionChanged = state.sectionIndex !== firstSection.current;
  const sectionTitle = useRef<HTMLSpanElement>(null);
  useEffect(() => {
    if (!sectionChanged) return;
    window.scrollTo(0, 0);
    sectionTitle.current?.focus({ preventScroll: true });
  }, [state.sectionIndex, sectionChanged]);

  return (
    <>
      <div className="sticky top-0 z-30 -mx-6 mb-5 border-b border-border bg-surface/95 px-6 py-3 backdrop-blur">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-col">
            <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">
              Bagian {num(state.sectionIndex) + 1} dari {num(state.sectionCount)}
            </span>
            <span ref={sectionTitle} tabIndex={-1} className="text-[15px] font-extrabold focus:outline-none">
              {state.currentSection}
            </span>
          </div>
          <div className="flex items-center gap-4">
            {state.proctoringEnabled && num(state.strikes) > 0 && (
              <span className="text-[12px] font-bold text-warning">
                Peringatan {num(state.strikes)}/{num(state.strikeLimit)}
              </span>
            )}
            {low && (
              <span className="exam-fade-in rounded-full bg-danger-soft px-2.5 py-0.5 text-[11.5px] font-semibold text-danger">
                Kurang dari 1 menit
              </span>
            )}
            <span className={"exam-timer tabular-nums text-[20px] font-extrabold " + (low ? "text-danger" : "text-ink")}>
              {clock(remaining)}
            </span>
            {/* Announced once when the last minute starts, never every second. */}
            <span role="status" className="sr-only">{low ? "Sisa waktu kurang dari 1 menit" : ""}</span>
          </div>
        </div>
        {/* Inside the sticky header so it stays in view — and stays MOUNTED — while the learner
            scrolls through the section's questions. Keyed by section so the next section never
            inherits this one's player. */}
        {state.sectionHasAudio && (
          <LiveAudioPlayer
            key={`${state.attemptId}:${state.currentSection}`}
            start={() => startSectionAudio(token, state.attemptId)}
            startedAt={state.sectionAudioStartedAt ?? null}
          />
        )}
      </div>

      <ol
        key={state.sectionIndex}
        className={"flex flex-col gap-6 " + (leaving ? "exam-section-leave" : sectionChanged ? "exam-section-enter" : "")}
      >
        {state.questions.map((q, i) => (
          <li key={q.id}>
            <p className="text-sm font-bold text-ink">{i + 1}. {q.prompt}</p>
            {q.passageRef && (
              <p className="mt-1.5 rounded-base bg-surface-2 px-3.5 py-2.5 text-[13px] leading-relaxed text-ink-muted">
                {q.passageRef}
              </p>
            )}
            {q.hasAudio && (
              <AudioButton
                token={token}
                attemptId={state.attemptId}
                questionId={q.id}
                playsLeft={num(state.audioPlaysLeft[q.id] ?? 0)}
                hasLimit={q.id in state.audioPlaysLeft}
              />
            )}
            <div className="mt-2 flex flex-col gap-1.5">
              {q.choices.map((choice, ci) => {
                const selected = answers[q.id] === ci;
                return (
                  <label
                    key={ci}
                    className={
                      "exam-option flex cursor-pointer items-center gap-2.5 rounded-base border px-3 py-2 text-[13.5px] " +
                      (selected ? "border-primary bg-primary-soft/50 font-semibold text-ink" : "border-border hover:bg-surface-2")
                    }
                  >
                    <input
                      type="radio"
                      name={`q-${q.id}`}
                      checked={selected}
                      onChange={() => onAnswer(q.id, ci)}
                      className="accent-primary"
                    />
                    {choice}
                  </label>
                );
              })}
            </div>
          </li>
        ))}
      </ol>

      {error && (
        <div className="mt-5 rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>
      )}

      <div className="mt-6 flex items-center justify-between gap-3 border-t border-border pt-5">
        <span className="flex flex-wrap items-center gap-3 text-[12.5px] text-ink-subtle">
          {answered}/{state.questions.length} terjawab
          {savedAt && (
            <span key={savedAt.getTime()} className="exam-fade-in font-semibold text-ink-muted">
              <span aria-hidden="true" className="text-success">✓</span> Tersimpan {savedAt.toLocaleTimeString("id-ID", { hour: "2-digit", minute: "2-digit" })}
            </span>
          )}
        </span>
        <Button onClick={onNext} loading={busy}>
          {isLast ? "Selesaikan tes" : "Bagian berikutnya →"}
        </Button>
      </div>
      <p className="mt-3 text-[11.5px] text-ink-subtle">
        Setelah melanjutkan, Anda tidak dapat kembali ke bagian ini.
      </p>
    </>
  );
}

function AudioButton({
  token, attemptId, questionId, playsLeft, hasLimit,
}: { token: string; attemptId: string; questionId: string; playsLeft: number; hasLimit: boolean }) {
  const [url, setUrl] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  async function play() {
    setBusy(true); setErr(null);
    try {
      const r = await getAudioUrl(token, attemptId, questionId);
      setUrl(r.url);
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Audio tidak dapat diputar.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-2">
      {url ? (
        <audio controls autoPlay src={url} className="w-full max-w-sm" />
      ) : (
        <Button size="sm" variant="neutral" onClick={play} loading={busy} disabled={hasLimit && playsLeft <= 0}>
          ▶ Putar audio{hasLimit && ` (sisa ${playsLeft})`}
        </Button>
      )}
      {err && <p className="mt-1 text-[12px] font-semibold text-danger">{err}</p>}
    </div>
  );
}

export function WarningOverlay({ strikes, limit, onClose }: { strikes: number; limit: number; onClose: () => void }) {
  const [closing, setClosing] = useState(false);
  function close() {
    setClosing(true);
    setTimeout(onClose, prefersReducedMotion() ? 0 : 150); // let the exit fade finish
  }

  return (
    <div
      className="exam-scrim fixed inset-0 z-50 flex items-center justify-center bg-[rgba(17,37,63,0.55)] p-6"
      data-closing={closing || undefined}
    >
      <div role="alertdialog" aria-modal="true" aria-labelledby="proctor-warning-title"
        className="max-w-md rounded-lg bg-surface p-6 text-center shadow-lg">
        <AlertTriangleIcon size={34} className="mx-auto text-warning" />
        <h2 id="proctor-warning-title" className="mt-3 text-lg font-extrabold">Jangan tinggalkan halaman tes</h2>
        <p className="mt-2 text-[13.5px] leading-relaxed text-ink-muted">
          Sistem mencatat bahwa Anda meninggalkan halaman. Ini adalah peringatan
          <strong className="text-ink"> {strikes} dari {limit}</strong>. Jika terjadi lagi, tes akan
          dikirim otomatis dan ditandai untuk ditinjau.
        </p>
        <Button className="mt-4" fullWidth autoFocus onClick={close}>Lanjutkan tes</Button>
      </div>
    </div>
  );
}
