"use client";

import { useEffect, useRef, useState } from "react";
import { Button, AlertTriangleIcon } from "@/components/ui";
import { LiveAudioPlayer } from "@/components/learn/LiveAudioPlayer";
import { getAudioUrl, startSectionAudio, clock, num, type AttemptState } from "@/lib/sessions";

export function Sitting({
  token, state, answers, remaining, busy, error, onAnswer, onSave, onNext,
}: {
  token: string;
  state: AttemptState;
  answers: Record<string, number>;
  remaining: number;
  busy: boolean;
  error: string | null;
  onAnswer: (qid: string, choice: number) => void;
  onSave: () => void;
  onNext: () => void;
}) {
  const answered = state.questions.filter((q) => answers[q.id] !== undefined).length;
  const isLast = num(state.sectionIndex) >= num(state.sectionCount) - 1;
  const low = remaining <= 60;

  // Persist answers periodically so a crashed tab never loses the sitting.
  const saveRef = useRef(onSave);
  saveRef.current = onSave;
  useEffect(() => {
    const t = setInterval(() => saveRef.current(), 15000);
    return () => clearInterval(t);
  }, []);

  return (
    <>
      <div className="sticky top-0 z-30 -mx-6 mb-5 border-b border-border bg-surface/95 px-6 py-3 backdrop-blur">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-col">
            <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">
              Bagian {num(state.sectionIndex) + 1} dari {num(state.sectionCount)}
            </span>
            <span className="text-[15px] font-extrabold">{state.currentSection}</span>
          </div>
          <div className="flex items-center gap-4">
            {state.proctoringEnabled && num(state.strikes) > 0 && (
              <span className="text-[12px] font-bold text-warning">
                Peringatan {num(state.strikes)}/{num(state.strikeLimit)}
              </span>
            )}
            <span className={"tabular-nums text-[20px] font-extrabold " + (low ? "text-danger" : "text-ink")}>
              {clock(remaining)}
            </span>
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

      <ol className="flex flex-col gap-6">
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
                      "flex cursor-pointer items-center gap-2.5 rounded-base border px-3 py-2 text-[13.5px] transition-colors " +
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
        <span className="text-[12.5px] text-ink-subtle">{answered}/{state.questions.length} terjawab</span>
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
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-[rgba(17,37,63,0.55)] p-6">
      <div className="max-w-md rounded-lg bg-surface p-6 text-center shadow-lg">
        <AlertTriangleIcon size={34} className="mx-auto text-warning" />
        <h2 className="mt-3 text-lg font-extrabold">Jangan tinggalkan halaman tes</h2>
        <p className="mt-2 text-[13.5px] leading-relaxed text-ink-muted">
          Sistem mencatat bahwa Anda meninggalkan halaman. Ini adalah peringatan
          <strong className="text-ink"> {strikes} dari {limit}</strong>. Jika terjadi lagi, tes akan
          dikirim otomatis dan ditandai untuk ditinjau.
        </p>
        <Button className="mt-4" fullWidth onClick={onClose}>Lanjutkan tes</Button>
      </div>
    </div>
  );
}
