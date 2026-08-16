"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Spinner, CheckIcon, LockIcon } from "@/components/ui";
import {
  getSessionAssessment, startAttempt, submitAttempt, num,
  type AttemptResult, type StudentAssessment,
} from "@/lib/sessions";

/**
 * The gating test shown under the player. It unlocks once the watch threshold is met —
 * but the real gate is server-side: the session only completes when this is passed (GR-8).
 */
export function GatingTest({
  token, sessionId, watchThresholdMet, onPassed,
}: {
  token: string;
  sessionId: string;
  watchThresholdMet: boolean;
  onPassed: () => void;
}) {
  const qc = useQueryClient();
  const q = useQuery({
    queryKey: ["session-assessment", sessionId],
    queryFn: () => getSessionAssessment(token, sessionId),
  });

  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [result, setResult] = useState<AttemptResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (q.isPending) {
    return (
      <div className="flex min-h-[110px] items-center justify-center rounded-lg border border-border bg-surface">
        <Spinner size={20} />
      </div>
    );
  }
  if (!q.data) return null;              // no gating test on this session

  const test: StudentAssessment = q.data;
  const alreadyPassed = test.passed || result?.passed === true;
  const total = test.questions.length;
  const answered = Object.keys(answers).length;

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const attempt = await startAttempt(token, test.id);
      const r = await submitAttempt(token, attempt.id, answers);
      setResult(r);
      await qc.invalidateQueries({ queryKey: ["session-assessment", sessionId] });
      if (r.sessionCompleted) onPassed();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal mengirim jawaban.");
    } finally {
      setBusy(false);
    }
  }

  function retry() {
    setResult(null);
    setAnswers({});
  }

  // Locked until the learner has actually watched the video.
  if (!watchThresholdMet && !alreadyPassed) {
    return (
      <div className="flex items-start gap-3 rounded-lg border border-border bg-surface p-5 shadow-sm">
        <LockIcon size={18} className="mt-0.5 shrink-0 text-ink-subtle" />
        <div>
          <h3 className="text-base font-extrabold">Tes sesi</h3>
          <p className="mt-1 text-[13px] leading-snug text-ink-muted">
            Tonton video sampai selesai untuk membuka tes. Lulus tes ini membuka sesi berikutnya.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h3 className="text-base font-extrabold">Tes sesi</h3>
        {alreadyPassed ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-success-soft px-2.5 py-1 text-[12px] font-bold text-success">
            <CheckIcon size={13} strokeWidth={3} /> Lulus
          </span>
        ) : (
          <span className="text-[12px] text-ink-muted">
            Skor lulus: {num(test.passThreshold)}/{total}
            {test.retakeCap != null && ` · sisa ${Math.max(0, num(test.retakeCap) - num(test.attemptsUsed))} percobaan`}
          </span>
        )}
      </div>

      <p className="mt-1 text-[13px] text-ink-muted">
        {alreadyPassed
          ? "Anda sudah lulus tes ini. Sesi berikutnya sudah terbuka."
          : "Jawab semua pertanyaan untuk menyelesaikan sesi ini."}
      </p>

      {!alreadyPassed && (
        <>
          <ol className="mt-4 flex flex-col gap-5">
            {test.questions.map((question, qi) => (
              <li key={question.id}>
                <p className="text-sm font-bold text-ink">{qi + 1}. {question.prompt}</p>
                {question.passageRef && (
                  <p className="mt-1 rounded-base bg-surface-2 px-3 py-2 text-[12.5px] leading-relaxed text-ink-muted">
                    {question.passageRef}
                  </p>
                )}
                <div className="mt-2 flex flex-col gap-1.5">
                  {question.choices.map((choice, ci) => {
                    const selected = answers[question.id] === ci;
                    return (
                      <label
                        key={ci}
                        className={
                          "flex cursor-pointer items-center gap-2.5 rounded-base border px-3 py-2 text-[13.5px] transition-colors " +
                          (selected
                            ? "border-primary bg-primary-soft/50 font-semibold text-ink"
                            : "border-border hover:bg-surface-2")
                        }
                      >
                        <input
                          type="radio"
                          name={`q-${question.id}`}
                          checked={selected}
                          disabled={busy}
                          onChange={() => setAnswers((a) => ({ ...a, [question.id]: ci }))}
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

          {result && !result.passed && (
            <div className="mt-4 rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">
              Skor Anda {result.score}/{result.maxScore}. Belum mencapai batas lulus — coba lagi.
            </div>
          )}
          {error && (
            <div className="mt-4 rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>
          )}

          <div className="mt-4 flex items-center justify-between gap-3">
            <span className="text-[12.5px] text-ink-subtle">{answered}/{total} terjawab</span>
            {result && !result.passed ? (
              <Button onClick={retry} disabled={!test.canAttempt}>
                {test.canAttempt ? "Coba lagi" : "Batas percobaan tercapai"}
              </Button>
            ) : (
              <Button onClick={submit} loading={busy} disabled={answered < total}>Kirim jawaban</Button>
            )}
          </div>
        </>
      )}

      {result?.passed && (
        <div className="mt-4 rounded-base bg-success-soft px-4 py-3 text-sm font-semibold text-success">
          Selamat! Skor {result.score}/{result.maxScore}. Sesi berikutnya sudah terbuka.
        </div>
      )}
    </div>
  );
}
