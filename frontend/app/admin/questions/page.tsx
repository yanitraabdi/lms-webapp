"use client";

import { Suspense, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Modal, Spinner, ErrorState, SearchIcon, XIcon } from "@/components/ui";
import { AudioUpload } from "@/components/admin/AudioUpload";
import {
  listQuestions, createQuestion, updateQuestion, deleteQuestion, moveQuestion, getQuestionBankCounts,
  QUESTION_SECTIONS, QUESTION_BANKS, BANK_LABEL, num,
  type AdminQuestion, type UpsertQuestion, type QuestionBankName,
} from "@/lib/sessions";

export default function AdminQuestionsPage() {
  // useSearchParams needs a Suspense boundary for the static build.
  return <Suspense><QuestionBank /></Suspense>;
}

function QuestionBank() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();
  const router = useRouter();
  const param = useSearchParams().get("bank");
  const bank: QuestionBankName = param === "Simulation" ? "Simulation" : "SessionTest";
  const other: QuestionBankName = bank === "Simulation" ? "SessionTest" : "Simulation";
  const [pageError, setPageError] = useState<string | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [section, setSection] = useState("");
  const [editing, setEditing] = useState<AdminQuestion | null>(null);
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  const questions = useQuery({
    queryKey: ["admin-questions", bank, section, search],
    queryFn: () => listQuestions(token!, { bank, section: section || undefined, search: search || undefined }),
    enabled: !!token,
  });
  // Counts come from their own endpoint: the list caps at 500 rows, so it can't be counted.
  const counts = useQuery({
    queryKey: ["question-bank-counts"],
    queryFn: () => getQuestionBankCounts(token!),
    enabled: !!token,
  });
  const countOf = (b: QuestionBankName) =>
    counts.data ? num(b === "Simulation" ? counts.data.simulation : counts.data.sessionTest) : null;

  function refresh() {
    qc.invalidateQueries({ queryKey: ["admin-questions"] });
    qc.invalidateQueries({ queryKey: ["question-bank-counts"] });
  }

  async function remove(q: AdminQuestion) {
    if (!token || !confirm(`Hapus soal "${q.prompt.slice(0, 50)}…"?`)) return;
    setPageError(null);
    try {
      await deleteQuestion(token, q.id);
      refresh();
    } catch (e) {
      setPageError(e instanceof Error ? e.message : "Gagal menghapus.");
    }
  }

  async function move(q: AdminQuestion) {
    if (!token || !confirm(`Pindahkan soal ini ke bank ${BANK_LABEL[other]}?`)) return;
    setPageError(null);
    try {
      await moveQuestion(token, q.id, other);
      refresh();
    } catch (e) {
      setPageError(e instanceof Error ? e.message : "Gagal memindahkan soal.");
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <div role="group" aria-label="Bank soal" className="flex flex-wrap gap-2">
        {QUESTION_BANKS.map((b) => (
          <button
            key={b}
            type="button"
            aria-pressed={b === bank}
            onClick={() => { setPageError(null); router.replace(`?bank=${b}`); }}
            className={`rounded-sm border px-3 py-2 text-[13px] font-bold outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 ${
              b === bank ? "border-primary bg-primary text-primary-ink" : "border-border bg-surface text-ink hover:bg-surface-2"
            }`}
          >
            {BANK_LABEL[b]} ({countOf(b) ?? "…"})
          </button>
        ))}
      </div>

      {pageError && (
        <div role="alert" className="rounded-base bg-danger-soft px-4 py-3 text-[13px] font-semibold text-danger">{pageError}</div>
      )}

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-ink-subtle">
          Bank soal untuk tes sesi dan tes akhir. Soal disusun menjadi tes di halaman Program.
        </p>
        <div className="flex items-center gap-2">
          <select
            value={section}
            onChange={(e) => setSection(e.target.value)}
            className="rounded-sm border border-border bg-surface px-2.5 py-2 text-[13px] outline-none focus:border-primary"
          >
            <option value="">Semua bagian</option>
            {QUESTION_SECTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <div className="relative">
            <SearchIcon size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-subtle" />
            <input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Cari soal…"
              className="w-[200px] rounded-sm border border-border bg-surface py-2 pl-8 pr-3 text-[13px] outline-none focus:border-primary"
            />
          </div>
          <Link
            href="/admin/questions/import"
            className="rounded-sm border border-border px-3 py-2 text-[13px] font-bold text-ink hover:bg-surface-2"
          >
            Impor massal
          </Link>
          <Button size="sm" onClick={() => setCreating(true)}>+ Soal baru</Button>
        </div>
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
        {!token || questions.isPending ? (
          <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
        ) : questions.isError ? (
          <div className="p-6">
            <ErrorState title="Gagal memuat bank soal"
              action={<Button variant="neutral" size="sm" onClick={() => questions.refetch()}>Muat ulang</Button>} />
          </div>
        ) : questions.data.length === 0 && !section && !search ? (
          <div className="flex flex-col items-center gap-3 px-6 py-8 text-center">
            <p className="text-sm text-ink-muted">Belum ada soal di bank {BANK_LABEL[bank]}.</p>
            <div className="flex gap-2">
              <Button size="sm" onClick={() => setCreating(true)}>Tambah soal</Button>
              <Link
                href="/admin/questions/import"
                className="rounded-sm border border-border px-3 py-2 text-[13px] font-bold text-ink hover:bg-surface-2"
              >
                Impor dari Excel
              </Link>
            </div>
          </div>
        ) : questions.data.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">Belum ada soal.</p>
        ) : (
          <table className="w-full min-w-[640px] border-collapse">
            <thead>
              <tr className="bg-surface-2 text-left text-xs font-bold text-ink-muted">
                <th className="px-5 py-3">Pertanyaan</th>
                <th className="px-3 py-3">Bagian</th>
                <th className="px-3 py-3">Pilihan</th>
                <th className="px-3 py-3">Dipakai</th>
                <th className="px-5 py-3 text-right">Aksi</th>
              </tr>
            </thead>
            <tbody className="text-[13.5px]">
              {questions.data.map((q) => (
                <tr key={q.id} className="border-t border-border">
                  <td className="max-w-[380px] px-5 py-3.5">
                    <div className="truncate font-semibold">{q.prompt}</div>
                    <div className="truncate text-[11.5px] text-ink-subtle">
                      Jawaban benar: {q.correct.map((i) => q.choices[num(i)] ?? "?").join(", ")}
                    </div>
                  </td>
                  <td className="px-3 py-3.5">
                    <Badge tone="neutral" className="px-2 py-0.5 text-[10.5px]">{q.section}</Badge>
                  </td>
                  <td className="px-3 py-3.5">{q.choices.length}</td>
                  <td className="px-3 py-3.5">{num(q.usedInAssessments)}</td>
                  <td className="px-5 py-3.5">
                    <div className="flex justify-end gap-2">
                      <Button variant="neutral" size="sm" onClick={() => setEditing(q)}>Edit</Button>
                      <Button variant="neutral" size="sm" onClick={() => move(q)}>Pindahkan ke bank lain</Button>
                      <button
                        type="button"
                        onClick={() => remove(q)}
                        aria-label="Hapus soal"
                        className="rounded px-1.5 text-ink-subtle hover:text-danger"
                      >
                        <XIcon size={15} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {(creating || editing) && token && (
        <QuestionForm
          token={token}
          question={editing}
          bank={bank}
          onClose={() => {
            setCreating(false);
            setEditing(null);
            refresh();
          }}
        />
      )}
    </div>
  );
}

function QuestionForm({
  token, question, bank, onClose,
}: { token: string; question: AdminQuestion | null; bank: QuestionBankName; onClose: () => void }) {
  const [section, setSection] = useState(question?.section ?? "Reading");
  const [prompt, setPrompt] = useState(question?.prompt ?? "");
  const [choices, setChoices] = useState<string[]>(question?.choices ?? ["", ""]);
  const [correct, setCorrect] = useState<number>(question ? num(question.correct[0] ?? 0) : 0);
  const [passage, setPassage] = useState(question?.passageRef ?? "");
  const [audioRef, setAudioRef] = useState<string | null>(question?.audioRef ?? null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const valid = prompt.trim() && choices.length >= 2 && choices.every((c) => c.trim());

  async function save() {
    if (!valid) return;
    setBusy(true);
    setError(null);
    try {
      const body: UpsertQuestion = {
        section,
        prompt: prompt.trim(),
        choices: choices.map((c) => c.trim()),
        correct: [correct],
        audioRef: section === "Listening" ? audioRef : null,
        passageRef: passage.trim() || null,
        tags: null,
      };
      if (question) await updateQuestion(token, question.id, body);
      else await createQuestion(token, { ...body, bank });
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan soal.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={question ? "Edit soal" : "Soal baru"}
      className="max-w-xl"
      footer={
        <div className="flex w-full justify-end gap-2">
          <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
          <Button size="sm" onClick={save} loading={busy} disabled={!valid}>Simpan</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}

        <p className="text-[12px] font-bold text-ink-muted">
          Bank: {BANK_LABEL[(question?.bank ?? bank) as QuestionBankName] ?? question?.bank}
        </p>

        <label className="flex flex-col gap-1">
          <span className="text-[12px] font-bold text-ink-muted">Bagian</span>
          <select value={section} onChange={(e) => setSection(e.target.value)} className={inputCls}>
            {QUESTION_SECTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </label>

        <label className="flex flex-col gap-1">
          <span className="text-[12px] font-bold text-ink-muted">Pertanyaan</span>
          <textarea value={prompt} onChange={(e) => setPrompt(e.target.value)} rows={2} className={inputCls} />
        </label>

        {section === "Listening" && (
          <div className="flex flex-col gap-1">
            <span className="text-[12px] font-bold text-ink-muted">Audio (Listening)</span>
            <AudioUpload token={token} value={audioRef} onChange={setAudioRef} />
          </div>
        )}

        <label className="flex flex-col gap-1">
          <span className="text-[12px] font-bold text-ink-muted">Teks bacaan (opsional)</span>
          <textarea value={passage} onChange={(e) => setPassage(e.target.value)} rows={2} className={inputCls} />
        </label>

        <div className="flex flex-col gap-1.5">
          <span className="text-[12px] font-bold text-ink-muted">Pilihan jawaban — tandai yang benar</span>
          {choices.map((c, i) => (
            <div key={i} className="flex items-center gap-2">
              <input
                type="radio"
                name="correct"
                checked={correct === i}
                onChange={() => setCorrect(i)}
                aria-label={`Pilihan ${i + 1} benar`}
                className="accent-primary"
              />
              <input
                value={c}
                onChange={(e) => setChoices((cs) => cs.map((x, j) => (j === i ? e.target.value : x)))}
                placeholder={`Pilihan ${i + 1}`}
                className={inputCls}
              />
              {choices.length > 2 && (
                <button
                  type="button"
                  onClick={() => {
                    setChoices((cs) => cs.filter((_, j) => j !== i));
                    if (correct >= choices.length - 1) setCorrect(0);
                  }}
                  aria-label="Hapus pilihan"
                  className="text-ink-subtle hover:text-danger"
                >
                  <XIcon size={14} />
                </button>
              )}
            </div>
          ))}
          <button
            type="button"
            onClick={() => setChoices((cs) => [...cs, ""])}
            className="self-start text-[12px] font-bold text-primary hover:underline"
          >
            + Tambah pilihan
          </button>
        </div>
      </div>
    </Modal>
  );
}

const inputCls =
  "w-full rounded-sm border border-border bg-surface px-3 py-2 text-[13.5px] outline-none focus:border-primary";
