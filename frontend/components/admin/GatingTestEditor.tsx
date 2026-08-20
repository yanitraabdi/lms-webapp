"use client";

import { useEffect, useState } from "react";
import { Button, Modal, Spinner } from "@/components/ui";
import { QuestionPicker } from "@/components/admin/QuestionPicker";
import {
  getAssessment, createAssessment, updateAssessment,
  setAssessmentQuestions, attachAssessment, num,
} from "@/lib/sessions";

/** Create, edit, attach or detach the short test that gates a video session. */
export function GatingTestEditor({
  token, sessionId, assessmentId, onClose,
}: { token: string; sessionId: string; assessmentId: string | null; onClose: () => void }) {
  const [loading, setLoading] = useState(!!assessmentId);
  const [title, setTitle] = useState("Tes sesi");
  const [passThreshold, setPassThreshold] = useState(1);
  const [retakeCap, setRetakeCap] = useState("");        // blank = unlimited
  const [selected, setSelected] = useState<string[]>([]);
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
      })
      .catch((e) => !cancelled && setError(e instanceof Error ? e.message : "Gagal memuat tes."))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [token, assessmentId]);

  const valid = title.trim() && selected.length > 0 && passThreshold >= 1 && passThreshold <= selected.length;

  async function save() {
    if (!valid) return;
    setBusy(true); setError(null);
    try {
      const config = {
        passThreshold,
        retakeCap: retakeCap.trim() === "" ? null : Number(retakeCap),
        proctoringEnabled: false,
        audioPlayLimit: null,
        sections: [],
        timeLimitMinutes: null,
      };
      let id = assessmentId;
      if (id) {
        await updateAssessment(token, id, { kind: "Gating", title: title.trim(), config });
      } else {
        id = (await createAssessment(token, { kind: "Gating", title: title.trim(), config })).id;
      }
      await setAssessmentQuestions(token, id, selected);
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
      title="Tes sesi"
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
            <label className="flex flex-col gap-1">
              <span className="text-[12px] font-bold text-ink-muted">Skor lulus</span>
              <input
                type="number" min={1} max={Math.max(1, selected.length)} value={passThreshold}
                onChange={(e) => setPassThreshold(Number(e.target.value) || 1)}
                className={inputCls + " w-24"}
              />
            </label>
            <label className="flex flex-col gap-1">
              <span className="text-[12px] font-bold text-ink-muted">Batas percobaan</span>
              <input
                type="number" min={1} value={retakeCap} placeholder="tanpa batas"
                onChange={(e) => setRetakeCap(e.target.value)}
                className={inputCls + " w-36"}
              />
            </label>
          </div>

          {selected.length > 0 && passThreshold > selected.length && (
            <p className="text-[12.5px] font-semibold text-danger">
              Skor lulus tidak boleh melebihi jumlah soal ({selected.length}).
            </p>
          )}

          <div>
            <span className="text-[12px] font-bold text-ink-muted">Soal</span>
            <div className="mt-1">
              <QuestionPicker token={token} selected={selected} onChange={setSelected} />
            </div>
          </div>
        </div>
      )}
    </Modal>
  );
}

const inputCls =
  "rounded-sm border border-border bg-surface px-3 py-2 text-[13.5px] outline-none focus:border-primary";
