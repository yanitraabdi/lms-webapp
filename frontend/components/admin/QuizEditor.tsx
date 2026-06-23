"use client";

import { useEffect, useState } from "react";
import { Modal, Button, Switch, Spinner, XIcon } from "@/components/ui";
import { getAdminQuiz, upsertQuiz, deleteAdminQuiz, num, type QuizQuestionInput } from "@/lib/admin";

type Draft = { prompt: string; choices: string[]; correctIndex: number };

const emptyQuestion = (): Draft => ({ prompt: "", choices: ["", ""], correctIndex: 0 });

export function QuizEditor({
  token, moduleId, moduleTitle, open, onClose,
}: { token: string; moduleId: string; moduleTitle: string; open: boolean; onClose: () => void }) {
  const [loading, setLoading] = useState(true);
  const [isActive, setIsActive] = useState(true);
  const [passThreshold, setPassThreshold] = useState(1);
  const [questions, setQuestions] = useState<Draft[]>([emptyQuestion()]);
  const [existing, setExisting] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    getAdminQuiz(token, moduleId)
      .then((quiz) => {
        if (cancelled) return;
        if (quiz) {
          setExisting(true);
          setIsActive(quiz.isActive);
          setPassThreshold(num(quiz.passThreshold));
          setQuestions(
            quiz.questions.length
              ? quiz.questions.map((q) => ({ prompt: q.prompt, choices: [...q.choices], correctIndex: num(q.correctIndex) }))
              : [emptyQuestion()]
          );
        } else {
          setExisting(false);
          setIsActive(true);
          setPassThreshold(1);
          setQuestions([emptyQuestion()]);
        }
      })
      .catch((e) => !cancelled && setError(e instanceof Error ? e.message : "Gagal memuat kuis."))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [open, token, moduleId]);

  function patch(i: number, p: Partial<Draft>) {
    setQuestions((qs) => qs.map((q, idx) => (idx === i ? { ...q, ...p } : q)));
  }
  function patchChoice(qi: number, ci: number, value: string) {
    setQuestions((qs) => qs.map((q, idx) => (idx === qi ? { ...q, choices: q.choices.map((c, j) => (j === ci ? value : c)) } : q)));
  }
  function addChoice(qi: number) {
    setQuestions((qs) => qs.map((q, idx) => (idx === qi ? { ...q, choices: [...q.choices, ""] } : q)));
  }
  function removeChoice(qi: number, ci: number) {
    setQuestions((qs) => qs.map((q, idx) => {
      if (idx !== qi || q.choices.length <= 2) return q;
      const choices = q.choices.filter((_, j) => j !== ci);
      const correctIndex = q.correctIndex >= choices.length ? choices.length - 1 : q.correctIndex;
      return { ...q, choices, correctIndex };
    }));
  }

  const valid =
    questions.length > 0 &&
    questions.every((q) => q.prompt.trim() && q.choices.length >= 2 && q.choices.every((c) => c.trim())) &&
    passThreshold >= 1 && passThreshold <= questions.length;

  async function save() {
    if (!valid) return;
    setBusy(true);
    setError(null);
    try {
      const payload: { passThreshold: number; isActive: boolean; questions: QuizQuestionInput[] } = {
        passThreshold,
        isActive,
        questions: questions.map((q) => ({ prompt: q.prompt.trim(), choices: q.choices.map((c) => c.trim()), correctIndex: q.correctIndex })),
      };
      await upsertQuiz(token, moduleId, payload);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan kuis.");
    } finally {
      setBusy(false);
    }
  }

  async function destroy() {
    if (!confirm("Hapus kuis untuk modul ini?")) return;
    setBusy(true);
    setError(null);
    try {
      await deleteAdminQuiz(token, moduleId);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menghapus kuis.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={`Kuis — ${moduleTitle}`}
      className="max-w-2xl"
      footer={
        <div className="flex w-full items-center justify-between gap-2">
          {existing ? (
            <Button variant="danger" size="sm" onClick={destroy} loading={busy}>Hapus kuis</Button>
          ) : <span />}
          <div className="flex gap-2">
            <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
            <Button size="sm" onClick={save} loading={busy} disabled={!valid || loading}>Simpan</Button>
          </div>
        </div>
      }
    >
      {loading ? (
        <div className="flex min-h-[160px] items-center justify-center"><Spinner size={22} /></div>
      ) : (
        <div className="flex flex-col gap-4">
          {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}

          <div className="flex flex-wrap items-center gap-x-6 gap-y-3">
            <label className="flex items-center gap-2 text-[13px] font-semibold">
              <Switch checked={isActive} onCheckedChange={setIsActive} aria-label="Kuis aktif" />
              Aktif (gerbang penyelesaian)
            </label>
            <label className="flex items-center gap-2 text-[13px] font-semibold">
              Skor lulus
              <input
                type="number"
                min={1}
                max={questions.length}
                value={passThreshold}
                onChange={(e) => setPassThreshold(Math.max(1, Math.min(questions.length, Number(e.target.value) || 1)))}
                className="w-16 rounded-sm border border-border bg-surface px-2 py-1 text-[13px] outline-none focus:border-primary"
              />
              <span className="text-ink-subtle">/ {questions.length}</span>
            </label>
          </div>

          {questions.map((q, qi) => (
            <div key={qi} className="rounded-base border border-border p-3.5">
              <div className="flex items-start gap-2">
                <span className="mt-2 text-[12px] font-bold text-ink-subtle">{qi + 1}.</span>
                <input
                  value={q.prompt}
                  onChange={(e) => patch(qi, { prompt: e.target.value })}
                  placeholder="Pertanyaan"
                  className="flex-1 rounded-sm border border-border bg-surface px-3 py-2 text-[13.5px] font-semibold outline-none focus:border-primary"
                />
                {questions.length > 1 && (
                  <button type="button" onClick={() => setQuestions((qs) => qs.filter((_, i) => i !== qi))} aria-label="Hapus pertanyaan" className="mt-1.5 text-ink-subtle hover:text-danger">
                    <XIcon size={16} />
                  </button>
                )}
              </div>

              <div className="mt-2.5 flex flex-col gap-1.5 pl-5">
                {q.choices.map((c, ci) => (
                  <div key={ci} className="flex items-center gap-2">
                    <input
                      type="radio"
                      name={`correct-${qi}`}
                      checked={q.correctIndex === ci}
                      onChange={() => patch(qi, { correctIndex: ci })}
                      aria-label="Jawaban benar"
                      className="accent-primary"
                    />
                    <input
                      value={c}
                      onChange={(e) => patchChoice(qi, ci, e.target.value)}
                      placeholder={`Pilihan ${ci + 1}`}
                      className="flex-1 rounded-sm border border-border bg-surface px-2.5 py-1.5 text-[13px] outline-none focus:border-primary"
                    />
                    {q.choices.length > 2 && (
                      <button type="button" onClick={() => removeChoice(qi, ci)} aria-label="Hapus pilihan" className="text-ink-subtle hover:text-danger">
                        <XIcon size={14} />
                      </button>
                    )}
                  </div>
                ))}
                <button type="button" onClick={() => addChoice(qi)} className="self-start text-[12px] font-bold text-primary hover:underline">
                  + Tambah pilihan
                </button>
                <span className="text-[11.5px] text-ink-subtle">Tandai radio di pilihan yang benar.</span>
              </div>
            </div>
          ))}

          <Button variant="neutral" size="sm" onClick={() => setQuestions((qs) => [...qs, emptyQuestion()])} className="self-start">
            + Tambah pertanyaan
          </Button>
        </div>
      )}
    </Modal>
  );
}
