"use client";

import { useState } from "react";
import { Button, Modal } from "@/components/ui";
import { Field, inputBlockCls } from "@/components/admin/fields";
import { SessionPartsEditor } from "@/components/admin/SessionPartsEditor";
import {
  createSession, updateSession, num,
  type AdminSession, type UpsertSession,
} from "@/lib/programs";

export type SessionKind = "Video" | "Live" | "FinalAssessment";

const TYPE_LABEL: Record<SessionKind, string> = {
  Video: "Video & tes (beberapa bagian)",
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
  const [scheduledAt, setScheduledAt] = useState(toLocalInput(session?.scheduledAt));
  const [joinUrl, setJoinUrl] = useState(session?.joinUrl ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [partsDirty, setPartsDirty] = useState(false);

  function close() {
    if (partsDirty && !confirm("Perubahan pada daftar bagian belum disimpan. Tutup tanpa menyimpan?")) return;
    onClose();
  }

  async function save() {
    setBusy(true); setError(null);
    try {
      const body: UpsertSession = {
        type,
        title: title.trim(),
        description: session?.description ?? null,
        orderIndex: session ? num(session.orderIndex) : nextOrder,
        // A video session's videos and tests live in its parts (SessionPartsEditor).
        providerAssetId: null,
        durationSeconds: null,
        scheduledAt:
          type !== "Live" || !scheduledAt
            ? null
            : session?.scheduledAt && scheduledAt === toLocalInput(session.scheduledAt)
              ? session.scheduledAt // untouched: re-parsing would drop the seconds
              : new Date(scheduledAt).toISOString(),
        liveMode: type === "Live" ? (session?.liveMode ?? "Zoom") : null,
        joinUrl: type === "Live" ? joinUrl.trim() || null : null,
        location: session?.location ?? null,
        assessmentId: session?.assessmentId ?? null,
      };
      if (session) await updateSession(token, session.id, body);
      else await createSession(token, programId, body);
      close();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan sesi.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={close}
      title={editing ? "Ubah sesi" : "Sesi baru"}
      className={type === "Video" && editing ? "max-h-[92vh] max-w-2xl overflow-y-auto" : "max-w-lg"}
      footer={
        <div className="flex w-full justify-end gap-2">
          <Button variant="neutral" size="sm" onClick={close}>Batal</Button>
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

        {type === "Video" && (editing ? (
          <SessionPartsEditor token={token} sessionId={session.id} onDirtyChange={setPartsDirty} />
        ) : (
          <p className="rounded-base bg-surface-2 px-3 py-2 text-[12.5px] text-ink-muted">
            Simpan sesi terlebih dahulu, lalu tambahkan video dan tes pada daftar bagian.
          </p>
        ))}

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
