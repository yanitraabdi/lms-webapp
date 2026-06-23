"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Spinner, CheckIcon } from "@/components/ui";
import { getQuiz, submitQuiz, num, type QuizResult } from "@/lib/engagement";

/** Learner-facing quiz. Renders nothing when the module has no active quiz. */
export function QuizPanel({ token, moduleId, onCompleted }: { token: string; moduleId: string; onCompleted?: () => void }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["quiz", moduleId], queryFn: () => getQuiz(token, moduleId) });
  const [answers, setAnswers] = useState<Record<number, number>>({});
  const [result, setResult] = useState<QuizResult | null>(null);
  const [busy, setBusy] = useState(false);

  if (q.isPending) {
    return <div className="flex min-h-[100px] items-center justify-center rounded-lg border border-border bg-surface"><Spinner size={20} /></div>;
  }
  if (!q.data) return null;

  const quiz = q.data;
  const total = quiz.questions.length;
  const answered = Object.keys(answers).length;
  const alreadyPassed = quiz.passed || result?.passed;

  async function submit() {
    setBusy(true);
    try {
      const arr = quiz.questions.map((_, i) => answers[i] ?? -1);
      const r = await submitQuiz(token, moduleId, arr);
      setResult(r);
      qc.invalidateQueries({ queryKey: ["quiz", moduleId] });
      if (r.moduleCompleted) onCompleted?.();
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <h3 className="text-base font-extrabold">Kuis modul</h3>
        {alreadyPassed ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-success-soft px-2.5 py-1 text-[12px] font-bold text-success">
            <CheckIcon size={13} strokeWidth={3} /> Lulus
          </span>
        ) : (
          <span className="text-[12px] text-ink-muted">Skor lulus: {num(quiz.passThreshold)}/{total}</span>
        )}
      </div>
      <p className="mt-1 text-[13px] text-ink-muted">Jawab pertanyaan berikut untuk menyelesaikan modul ini.</p>

      <ol className="mt-4 flex flex-col gap-5">
        {quiz.questions.map((qq, qi) => (
          <li key={qq.id}>
            <p className="text-sm font-bold text-ink">{qi + 1}. {qq.prompt}</p>
            <div className="mt-2 flex flex-col gap-1.5">
              {qq.choices.map((choice, ci) => {
                const selected = answers[qi] === ci;
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
                      name={`q-${qq.id}`}
                      checked={selected}
                      onChange={() => setAnswers((a) => ({ ...a, [qi]: ci }))}
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

      {result && (
        <div
          className={
            "mt-4 rounded-base px-4 py-3 text-sm font-semibold " +
            (result.passed ? "bg-success-soft text-success" : "bg-danger-soft text-danger")
          }
        >
          {result.passed
            ? `Selamat! Anda lulus dengan skor ${result.score}/${result.total}.`
            : `Skor Anda ${result.score}/${result.total}. Coba lagi untuk lulus.`}
        </div>
      )}

      <div className="mt-4 flex items-center justify-between">
        <span className="text-[12.5px] text-ink-subtle">{answered}/{total} terjawab</span>
        <Button onClick={submit} loading={busy} disabled={answered < total}>
          {alreadyPassed ? "Kirim ulang" : "Kirim jawaban"}
        </Button>
      </div>
    </div>
  );
}
