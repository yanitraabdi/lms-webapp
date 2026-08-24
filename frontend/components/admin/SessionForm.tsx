"use client";

import { useState } from "react";
import { Button, Modal } from "@/components/ui";
import { Field, inputBlockCls } from "@/components/admin/fields";
import { createSession, type UpsertSession } from "@/lib/programs";

export type SessionKind = "Video" | "Live" | "FinalAssessment";

export function SessionForm({
  token, programId, nextOrder, onClose,
}: { token: string; programId: string; nextOrder: number; onClose: () => void }) {
  const [type, setType] = useState<SessionKind>("Video");
  const [title, setTitle] = useState("");
  const [minutes, setMinutes] = useState("15");
  const [assetId, setAssetId] = useState("sample");
  const [scheduledAt, setScheduledAt] = useState("");
  const [joinUrl, setJoinUrl] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setBusy(true); setError(null);
    try {
      const body: UpsertSession = {
        type,
        title: title.trim(),
        description: null,
        orderIndex: nextOrder,
        providerAssetId: type === "Video" ? assetId.trim() || null : null,
        durationSeconds: type === "Video" ? (Number(minutes) || 0) * 60 : null,
        scheduledAt: type === "Live" && scheduledAt ? new Date(scheduledAt).toISOString() : null,
        liveMode: type === "Live" ? "Zoom" : null,
        joinUrl: type === "Live" ? joinUrl.trim() || null : null,
        location: null,
        assessmentId: null,
      };
      await createSession(token, programId, body);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan sesi.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Sesi baru"
      className="max-w-lg"
      footer={
        <div className="flex w-full justify-end gap-2">
          <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
          <Button size="sm" onClick={save} loading={busy} disabled={!title.trim()}>Simpan</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}
        <Field label="Tipe sesi">
          <select value={type} onChange={(e) => setType(e.target.value as typeof type)} className={inputBlockCls}>
            <option value="Video">Video + tes</option>
            <option value="Live">Sesi Live</option>
            <option value="FinalAssessment">Tes Akhir</option>
          </select>
        </Field>
        <Field label="Judul"><input value={title} onChange={(e) => setTitle(e.target.value)} className={inputBlockCls} /></Field>

        {type === "Video" && (
          <>
            <Field label="Durasi (menit)">
              <input type="number" min={1} value={minutes} onChange={(e) => setMinutes(e.target.value)} className={inputBlockCls} />
            </Field>
            <Field label="Bunny asset id"><input value={assetId} onChange={(e) => setAssetId(e.target.value)} className={inputBlockCls} /></Field>
          </>
        )}

        {type === "Live" && (
          <>
            <Field label="Jadwal">
              <input type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} className={inputBlockCls} />
            </Field>
            <Field label="Tautan Zoom"><input value={joinUrl} onChange={(e) => setJoinUrl(e.target.value)} className={inputBlockCls} /></Field>
          </>
        )}

        {type === "FinalAssessment" && (
          <p className="rounded-base bg-surface-2 px-3 py-2 text-[12.5px] text-ink-muted">
            Setelah sesi tersimpan, gunakan tombol “Buat tes akhir” pada baris sesi untuk
            menyusun bagian dan soalnya.
          </p>
        )}
      </div>
    </Modal>
  );
}
