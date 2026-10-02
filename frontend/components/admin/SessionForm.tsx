"use client";

import { useState } from "react";
import { Button, Modal } from "@/components/ui";
import { Field, inputBlockCls } from "@/components/admin/fields";
import { VideoPicker } from "@/components/admin/VideoPicker";
import {
  createSession, updateSession, num,
  type AdminSession, type UpsertSession,
} from "@/lib/programs";

export type SessionKind = "Video" | "Live" | "FinalAssessment";

const TYPE_LABEL: Record<SessionKind, string> = {
  Video: "Video + tes",
  Live: "Sesi Live",
  FinalAssessment: "Tes Akhir",
};

/** ISO instant → the local "YYYY-MM-DDTHH:mm" a datetime-local input expects. */
function toLocalInput(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

/**
 * Creates a session, or — given `session` — edits one.
 *
 * In edit mode the type is fixed: the server keeps the stored type whatever is sent, because a
 * video that learners have watched must not turn into a live session. The quiz and the order are
 * likewise owned by their own controls, and the server ignores them here.
 *
 * Fields this form does not show (description, location) are sent back as they were, because the
 * update replaces the session's content wholesale and would otherwise blank them.
 */
export function SessionForm({
  token, programId, nextOrder, session, onClose,
}: {
  token: string;
  programId: string;
  nextOrder: number;
  session?: AdminSession;
  onClose: () => void;
}) {
  const editing = session !== undefined;
  const [type, setType] = useState<SessionKind>((session?.type as SessionKind) ?? "Video");
  const [title, setTitle] = useState(session?.title ?? "");
  const [seconds, setSeconds] = useState<number>(session?.durationSeconds != null ? num(session.durationSeconds) : 900);
  // Empty, not "sample": under Bunny the server refuses anything that is not a real video id.
  const [assetId, setAssetId] = useState(session?.providerAssetId ?? "");
  const [scheduledAt, setScheduledAt] = useState(toLocalInput(session?.scheduledAt));
  const [joinUrl, setJoinUrl] = useState(session?.joinUrl ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setBusy(true); setError(null);
    try {
      const body: UpsertSession = {
        type,
        title: title.trim(),
        description: session?.description ?? null,
        orderIndex: session ? num(session.orderIndex) : nextOrder,
        providerAssetId: type === "Video" ? assetId.trim() || null : null,
        durationSeconds: type === "Video" ? seconds : null,
        scheduledAt: type === "Live" && scheduledAt ? new Date(scheduledAt).toISOString() : null,
        liveMode: type === "Live" ? (session?.liveMode ?? "Zoom") : null,
        joinUrl: type === "Live" ? joinUrl.trim() || null : null,
        location: session?.location ?? null,
        assessmentId: session?.assessmentId ?? null,
      };
      if (session) await updateSession(token, session.id, body);
      else await createSession(token, programId, body);
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
      title={editing ? "Ubah sesi" : "Sesi baru"}
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
          {editing ? (
            <p className="rounded-sm bg-surface-2 px-3 py-2 text-[13.5px] text-ink-muted">
              {TYPE_LABEL[type]} — tipe tidak dapat diubah setelah sesi dibuat.
            </p>
          ) : (
            <select value={type} onChange={(e) => setType(e.target.value as SessionKind)} className={inputBlockCls}>
              <option value="Video">{TYPE_LABEL.Video}</option>
              <option value="Live">{TYPE_LABEL.Live}</option>
              <option value="FinalAssessment">{TYPE_LABEL.FinalAssessment}</option>
            </select>
          )}
        </Field>

        <Field label="Judul">
          <input value={title} onChange={(e) => setTitle(e.target.value)} className={inputBlockCls} />
        </Field>

        {type === "Video" && (
          <>
            <div className="flex flex-col gap-1">
              <span className="text-[12px] font-bold text-ink-muted">Video</span>
              <VideoPicker
                token={token}
                value={assetId}
                onPick={(v) => { setAssetId(v.id); setSeconds(v.lengthSeconds); }}
              />
            </div>
            <Field label="ID video Bunny">
              <input
                value={assetId}
                onChange={(e) => setAssetId(e.target.value)}
                placeholder="Terisi otomatis saat memilih video, atau tempel manual"
                className={inputBlockCls}
              />
            </Field>
            <Field label="Durasi (menit)">
              <input
                type="number"
                min={1}
                value={Math.max(1, Math.round(seconds / 60))}
                onChange={(e) => setSeconds((Number(e.target.value) || 0) * 60)}
                className={inputBlockCls}
              />
            </Field>
          </>
        )}

        {type === "Live" && (
          <>
            <Field label="Jadwal">
              <input type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} className={inputBlockCls} />
            </Field>
            <Field label="Tautan Zoom">
              <input value={joinUrl} onChange={(e) => setJoinUrl(e.target.value)} className={inputBlockCls} />
            </Field>
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
