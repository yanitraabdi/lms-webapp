"use client";

import { useEffect, useState } from "react";
import { Button, Spinner, XIcon } from "@/components/ui";
import { inputBlockCls } from "@/components/admin/fields";
import { VideoPicker } from "@/components/admin/VideoPicker";
import { GatingTestEditor } from "@/components/admin/GatingTestEditor";
import {
  listSessionParts, saveSessionParts, minutesLabel, num,
  type AdminSessionPart,
} from "@/lib/programs";

type PartKind = "LessonVideo" | "Test" | "Discussion";

type Row = {
  id?: string;
  kind: PartKind;
  title: string;
  providerAssetId?: string;
  durationSeconds?: number;
  assessmentId?: string;
  assessmentTitle?: string;
  hasLearnerData: boolean;
  /** React key for a row not yet saved; never sent to the API. */
  tempKey?: string;
};

const KIND_LABEL: Record<PartKind, string> = {
  LessonVideo: "Video materi",
  Test: "Tes",
  Discussion: "Video pembahasan",
};

const iconBtn =
  "rounded px-1.5 py-1 text-ink-muted outline-none hover:bg-surface-2 " +
  "focus-visible:ring-[3px] focus-visible:ring-primary-soft disabled:opacity-30";

function toRows(parts: AdminSessionPart[]): Row[] {
  return [...parts]
    .sort((a, b) => num(a.orderIndex) - num(b.orderIndex))
    .map((p) => ({
      id: p.id,
      kind: p.kind as PartKind,
      title: p.title,
      providerAssetId: p.providerAssetId ?? undefined,
      durationSeconds: p.durationSeconds == null ? undefined : num(p.durationSeconds),
      assessmentId: p.assessmentId ?? undefined,
      assessmentTitle: p.assessmentTitle ?? undefined,
      hasLearnerData: p.hasLearnerData,
    }));
}

/** What the server stores — the comparison basis for "unsaved changes". */
const toInput = (rows: Row[]) =>
  rows.map(({ id, kind, title, providerAssetId, durationSeconds, assessmentId }) => ({
    id: id ?? null,
    kind,
    title,
    providerAssetId: providerAssetId ?? null,
    durationSeconds: durationSeconds ?? null,
    assessmentId: assessmentId ?? null,
  }));

/**
 * The ordered parts of a video session: lesson videos, tests and discussion videos. Edited as a
 * local list and saved whole. A test is edited INLINE (not a stacked modal), so the unsaved list
 * survives the round trip.
 */
export function SessionPartsEditor({
  token, sessionId, onDirtyChange,
}: {
  token: string;
  sessionId: string;
  onDirtyChange?: (dirty: boolean) => void;
}) {
  const [rows, setRows] = useState<Row[]>([]);
  const [saved, setSaved] = useState<Row[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editingTestRow, setEditingTestRow] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    listSessionParts(token, sessionId)
      .then((p) => { if (!cancelled) { const r = toRows(p); setRows(r); setSaved(r); } })
      .catch((e) => !cancelled && setError(e instanceof Error ? e.message : "Gagal memuat bagian."))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [token, sessionId]);

  const dirty = JSON.stringify(toInput(rows)) !== JSON.stringify(toInput(saved));
  useEffect(() => { onDirtyChange?.(dirty); }, [dirty, onDirtyChange]);

  const update = (i: number, patch: Partial<Row>) =>
    setRows((rs) => rs.map((r, j) => (j === i ? { ...r, ...patch } : r)));

  function move(i: number, delta: number) {
    const t = i + delta;
    if (t < 0 || t >= rows.length) return;
    setRows((rs) => {
      const next = [...rs];
      [next[i], next[t]] = [next[t], next[i]];
      return next;
    });
  }

  const add = (kind: PartKind) =>
    setRows((rs) => [...rs, { kind, title: KIND_LABEL[kind], hasLearnerData: false, tempKey: crypto.randomUUID() }]);

  const incomplete = rows.some((r) =>
    r.kind === "Test" ? !r.assessmentId : !r.providerAssetId);

  async function save() {
    setBusy(true); setError(null);
    try {
      const r = toRows(await saveSessionParts(token, sessionId, toInput(rows)));
      setRows(r); setSaved(r);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan bagian.");
    } finally { setBusy(false); }
  }

  if (loading) {
    return <div className="flex min-h-[120px] items-center justify-center"><Spinner size={20} /></div>;
  }

  if (editingTestRow !== null) {
    const row = rows[editingTestRow];
    return (
      <div className="flex flex-col gap-3">
        <div>
          <Button variant="ghost" size="sm" onClick={() => setEditingTestRow(null)}>
            ← Kembali ke daftar bagian
          </Button>
        </div>
        <GatingTestEditor
          inline
          token={token}
          assessmentId={row.assessmentId ?? null}
          onSaved={(id) => update(editingTestRow, { assessmentId: id })}
          onClose={() => setEditingTestRow(null)}
        />
      </div>
    );
  }

  const last = rows[rows.length - 1];

  return (
    <div className="flex flex-col gap-3" role="group" aria-labelledby="parts-caption">
      <span id="parts-caption" className="text-[12px] font-bold text-ink-muted">Daftar bagian</span>

      {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}

      {rows.length === 0 && (
        <p className="text-[12.5px] text-ink-muted">Belum ada bagian.</p>
      )}

      <ol className="flex flex-col gap-2">
        {rows.map((r, i) => (
          <li key={r.id ?? r.tempKey} className="flex flex-col gap-2 rounded-base border border-border px-3 py-2.5">
            <div className="flex items-center gap-2">
              <span className="w-5 shrink-0 text-[12px] font-bold text-ink-subtle">{i + 1}</span>
              <span className="shrink-0 text-[12px] font-bold text-ink-muted">{KIND_LABEL[r.kind]}</span>
              <input
                value={r.title}
                onChange={(e) => update(i, { title: e.target.value })}
                aria-label={`Judul bagian ${i + 1}`}
                className={inputBlockCls + " min-w-0 flex-1"}
              />
              <button type="button" disabled={busy || i === 0} onClick={() => move(i, -1)}
                aria-label="Naikkan" className={iconBtn}>↑</button>
              <button type="button" disabled={busy || i === rows.length - 1} onClick={() => move(i, 1)}
                aria-label="Turunkan" className={iconBtn}>↓</button>
              <button
                type="button"
                disabled={busy || r.hasLearnerData}
                title={r.hasLearnerData ? "Sudah ada progres peserta" : undefined}
                onClick={() => setRows((rs) => rs.filter((_, j) => j !== i))}
                aria-label="Hapus"
                className={iconBtn + " hover:text-danger"}
              >
                <XIcon size={14} />
              </button>
            </div>

            {r.kind === "Test" ? (
              <div className="flex items-center gap-2 pl-7">
                <span className="min-w-0 flex-1 truncate text-[13px] text-ink">
                  {r.assessmentTitle ?? (r.assessmentId ? "Tes tersimpan" : "Belum ada tes")}
                </span>
                <Button variant="neutral" size="sm" onClick={() => setEditingTestRow(i)}>Atur tes</Button>
              </div>
            ) : (
              <div className="flex flex-col gap-1 pl-7">
                <VideoPicker
                  token={token}
                  value={r.providerAssetId ?? ""}
                  onPick={(v) => update(i, { providerAssetId: v.id, durationSeconds: v.lengthSeconds })}
                />
                {r.durationSeconds != null && (
                  <span className="text-[11.5px] text-ink-subtle">{minutesLabel(r.durationSeconds)}</span>
                )}
              </div>
            )}
          </li>
        ))}
      </ol>

      <div className="flex flex-wrap gap-2">
        <Button variant="neutral" size="sm" onClick={() => add("LessonVideo")}>+ Video materi</Button>
        <Button variant="neutral" size="sm" onClick={() => add("Test")}>+ Tes</Button>
        <Button variant="neutral" size="sm" disabled={last?.kind !== "Test"} onClick={() => add("Discussion")}>
          + Video pembahasan
        </Button>
      </div>

      <div className="flex items-center justify-end gap-3">
        {incomplete && (
          <p className="text-[12.5px] font-semibold text-warning">Lengkapi video dan tes setiap bagian.</p>
        )}
        <Button size="sm" onClick={save} loading={busy} disabled={incomplete}>Simpan bagian</Button>
      </div>
    </div>
  );
}
