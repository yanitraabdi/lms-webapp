"use client";

import { useEffect, useState } from "react";
import { Button, Modal, Spinner } from "@/components/ui";
import { inputCls } from "@/components/admin/fields";
import { QuestionPicker } from "@/components/admin/QuestionPicker";
import {
  getAssessment, createAssessment, updateAssessment,
  setAssessmentQuestions, attachAssessment, num,
  type AssessmentConfig,
} from "@/lib/sessions";

type SectionRow = {
  section: NonNullable<NonNullable<AssessmentConfig["sections"]>[number]["section"]>;
  questions: number;
  minutes: number;
  /** Whole-section recording, set on /admin/assessments/{id}. Carried through untouched here —
   *  the whole config is replaced on save, so dropping it would delete the recording. */
  audioRef: string | null;
};

/** ITP layout (KAK §9.7.1) — the starting point for a new final assessment. */
const DEFAULT_SECTIONS: SectionRow[] = [
  { section: "Listening", questions: 50, minutes: 35, audioRef: null },
  { section: "Structure", questions: 40, minutes: 25, audioRef: null },
  { section: "Reading", questions: 50, minutes: 55, audioRef: null },
];

/**
 * Create, edit, attach or detach the test a session needs: the short gating test of a video
 * session (kind "Gating", questions picked here), or the final assessment of a final session
 * (kind "Final" — only the section layout is set here; the questions are composed on
 * /admin/assessments/{id}).
 */
export function GatingTestEditor({
  token, sessionId, assessmentId, kind = "Gating", onClose,
}: {
  token: string;
  sessionId: string;
  assessmentId: string | null;
  kind?: "Gating" | "Final";
  onClose: () => void;
}) {
  const isFinal = kind === "Final";
  const [loading, setLoading] = useState(!!assessmentId);
  const [title, setTitle] = useState(isFinal ? "Simulasi TOEFL ITP — Tes Akhir" : "Tes sesi");
  const [passThreshold, setPassThreshold] = useState(1);
  const [retakeCap, setRetakeCap] = useState(isFinal ? "1" : "");   // blank = unlimited
  const [selected, setSelected] = useState<string[]>([]);
  const [sections, setSections] = useState<SectionRow[]>(DEFAULT_SECTIONS);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!assessmentId) return;
    let cancelled = false;
    getAssessment(token, assessmentId)
      .then((a) => {
        if (cancelled) return;
        setTitle(a.title);
        setPassThreshold(num(a.config.passThreshold ?? 1));
        setRetakeCap(a.config.retakeCap == null ? "" : String(num(a.config.retakeCap)));
        setSelected(a.questions.map((q) => q.id));
        const saved = a.config.sections ?? [];
        if (saved.length) {
          // The composer only knows the three ITP sections. Read each one's saved numbers by
          // name; never invent a section (a "General" fallback would be written straight back
          // into the config as if an operator had chosen it).
          setSections(DEFAULT_SECTIONS.map((d) => {
            const found = saved.find((s) => s.section === d.section);
            return found
              ? {
                  ...d,
                  questions: num(found.questions ?? 0),
                  minutes: num(found.minutes ?? 0),
                  audioRef: found.audioRef ?? null,
                }
              : d;
          }));
          const stray = saved
            .filter((s) => !DEFAULT_SECTIONS.some((d) => d.section === s.section))
            .map((s) => s.section ?? "tanpa nama");
          if (stray.length) {
            setError(
              `Konfigurasi memuat bagian di luar format TOEFL ITP (${stray.join(", ")}). ` +
              "Bagian tersebut akan dihapus jika tes ini disimpan."
            );
          }
        }
      })
      .catch((e) => !cancelled && setError(e instanceof Error ? e.message : "Gagal memuat tes."))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [token, assessmentId]);

  // Blank means unlimited; 0 would make the very first attempt exceed the cap, locking the
  // session — and the whole linear program — for every learner.
  const capOk = retakeCap.trim() === "" || Number(retakeCap) >= 1;
  const itpMismatch = isFinal
    ? DEFAULT_SECTIONS.filter((d, i) => sections[i]?.questions !== d.questions)
    : [];

  const valid = capOk && (isFinal
    ? !!title.trim() && sections.every((s) => s.questions >= 1 && s.minutes >= 1)
    : !!title.trim() && selected.length > 0 && passThreshold >= 1 && passThreshold <= selected.length);

  async function save() {
    if (!valid) return;
    setBusy(true); setError(null);
    try {
      const cap = retakeCap.trim() === "" ? null : Number(retakeCap);

      // The API merges the config by key presence, so send ONLY what this editor actually models.
      // Sending a hard-coded value for a field the editor does not expose would reset it on every
      // save — which is how audioPlayLimit was being forced back to 1 on each layout edit.
      const updateConfig = isFinal
        ? { retakeCap: cap, sections }
        : { passThreshold, retakeCap: cap, sections: [] };

      // On create there is nothing to merge over, so the defaults must be stated in full.
      const createConfig = isFinal
        ? { ...updateConfig, passThreshold: null, proctoringEnabled: true, audioPlayLimit: 1, timeLimitMinutes: null }
        : { ...updateConfig, proctoringEnabled: false, audioPlayLimit: null, timeLimitMinutes: null };

      const config = assessmentId ? updateConfig : createConfig;
      let id = assessmentId;
      if (id) {
        await updateAssessment(token, id, { kind, title: title.trim(), config });
      } else {
        id = (await createAssessment(token, { kind, title: title.trim(), config })).id;
      }
      if (!isFinal) await setAssessmentQuestions(token, id, selected);
      if (!assessmentId) await attachAssessment(token, sessionId, id);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan tes.");
      setBusy(false);
    }
  }

  async function detach() {
    if (!confirm("Lepas tes dari sesi ini?\n\nTes dan percobaan peserta tidak dihapus.")) return;
    setBusy(true);
    try {
      await attachAssessment(token, sessionId, null);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal melepas tes.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={isFinal ? "Tes akhir" : "Tes sesi"}
      className="max-w-2xl"
      footer={
        <div className="flex w-full items-center justify-between gap-2">
          {assessmentId
            ? <Button variant="neutral" size="sm" onClick={detach} loading={busy}>Lepas dari sesi</Button>
            : <span />}
          <div className="flex gap-2">
            <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
            <Button size="sm" onClick={save} loading={busy} disabled={!valid}>Simpan</Button>
          </div>
        </div>
      }
    >
      {loading ? (
        <div className="flex min-h-[200px] items-center justify-center"><Spinner size={22} /></div>
      ) : (
        <div className="flex flex-col gap-3">
          {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}

          <label className="flex flex-col gap-1">
            <span className="text-[12px] font-bold text-ink-muted">Judul</span>
            <input value={title} onChange={(e) => setTitle(e.target.value)} className={inputCls} />
          </label>

          <div className="flex flex-wrap gap-4">
            {!isFinal && (
              <label className="flex flex-col gap-1">
                <span className="text-[12px] font-bold text-ink-muted">Skor lulus</span>
                <input
                  type="number" min={1} max={Math.max(1, selected.length)} value={passThreshold}
                  onChange={(e) => setPassThreshold(Number(e.target.value) || 1)}
                  className={inputCls + " w-24"}
                />
              </label>
            )}
            <label className="flex flex-col gap-1">
              <span className="text-[12px] font-bold text-ink-muted">Batas percobaan</span>
              <input
                type="number" min={1} value={retakeCap} placeholder="tanpa batas"
                onChange={(e) => setRetakeCap(e.target.value)}
                className={inputCls + " w-36"}
              />
            </label>
          </div>

          {!capOk && (
            <p className="text-[12.5px] font-semibold text-danger">
              Batas percobaan minimal 1. Kosongkan untuk tanpa batas.
            </p>
          )}

          {!isFinal && selected.length > 0 && passThreshold > selected.length && (
            <p className="text-[12.5px] font-semibold text-danger">
              Skor lulus tidak boleh melebihi jumlah soal ({selected.length}).
            </p>
          )}

          {isFinal ? (
            <div className="flex flex-col gap-2">
              <span className="text-[12px] font-bold text-ink-muted">Bagian tes</span>
              {sections.map((s, i) => (
                <div key={s.section} className="flex flex-wrap items-center gap-3 rounded-base border border-border px-3.5 py-2.5">
                  <span className="w-24 text-[13.5px] font-semibold text-ink">{s.section}</span>
                  <label className="flex items-center gap-1.5 text-[12px] font-bold text-ink-muted">
                    Jumlah soal
                    <input
                      type="number" min={1} value={s.questions}
                      onChange={(e) => setSections((rows) => rows.map((r, j) =>
                        j === i ? { ...r, questions: Number(e.target.value) || 0 } : r))}
                      className={inputCls + " w-20"}
                    />
                  </label>
                  <label className="flex items-center gap-1.5 text-[12px] font-bold text-ink-muted">
                    Menit
                    <input
                      type="number" min={1} value={s.minutes}
                      onChange={(e) => setSections((rows) => rows.map((r, j) =>
                        j === i ? { ...r, minutes: Number(e.target.value) || 0 } : r))}
                      className={inputCls + " w-20"}
                    />
                  </label>
                </div>
              ))}
              {itpMismatch.length > 0 && (
                <p className="text-[12.5px] font-semibold text-warning">
                  Format TOEFL ITP wajib{" "}
                  {DEFAULT_SECTIONS.map((d) => `${d.questions} ${d.section}`).join(" / ")} soal.
                  Program tidak bisa diterbitkan selama jumlahnya berbeda.
                </p>
              )}
              <p className="text-[12.5px] text-ink-muted">
                Soal untuk tiap bagian dipilih di halaman “Susun soal” setelah tes akhir tersimpan.
                Proktoring dan batas pemutaran audio aktif untuk tes akhir.
              </p>
            </div>
          ) : (
            <div>
              <span className="text-[12px] font-bold text-ink-muted">Soal</span>
              <div className="mt-1">
                <QuestionPicker token={token} selected={selected} onChange={setSelected} />
              </div>
            </div>
          )}
        </div>
      )}
    </Modal>
  );
}

