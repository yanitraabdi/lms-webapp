"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Modal, Spinner, XIcon } from "@/components/ui";
import { AttendanceModal } from "@/components/admin/AttendanceModal";
import { SessionForm } from "@/components/admin/SessionForm";
import { GatingTestEditor } from "@/components/admin/GatingTestEditor";
import {
  listSessions, deleteSession, reorderSessions,
  minutesLabel, num,
  type AdminProgram, type AdminSession,
} from "@/lib/programs";

export function SessionManager({ token, program, onClose }: { token: string; program: AdminProgram; onClose: () => void }) {
  const qc = useQueryClient();
  const key = ["admin-sessions", program.id];
  const q = useQuery({ queryKey: key, queryFn: () => listSessions(token, program.id) });
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);
  const [attendanceFor, setAttendanceFor] = useState<string | null>(null);
  const [testFor, setTestFor] = useState<{ sessionId: string; assessmentId: string | null } | null>(null);

  const sessions = [...(q.data ?? [])].sort((a, b) => num(a.orderIndex) - num(b.orderIndex));

  async function move(index: number, delta: number) {
    const next = [...sessions];
    const target = index + delta;
    if (target < 0 || target >= next.length) return;
    [next[index], next[target]] = [next[target], next[index]];
    setBusy(true);
    try {
      await reorderSessions(token, program.id, next.map((s) => s.id));
      await qc.invalidateQueries({ queryKey: key });
    } finally { setBusy(false); }
  }

  async function remove(s: AdminSession) {
    if (!confirm(`Hapus sesi "${s.title}"?`)) return;
    setBusy(true);
    try {
      await deleteSession(token, s.id);
      await qc.invalidateQueries({ queryKey: key });
    } catch (e) {
      alert(e instanceof Error ? e.message : "Gagal menghapus.");
    } finally { setBusy(false); }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={`Sesi — ${program.name}`}
      className="max-w-2xl"
      footer={
        <div className="flex w-full items-center justify-between gap-2">
          <Button variant="neutral" size="sm" onClick={() => setAdding(true)}>+ Tambah sesi</Button>
          <Button size="sm" onClick={onClose}>Selesai</Button>
        </div>
      }
    >
      {q.isPending ? (
        <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
      ) : sessions.length === 0 ? (
        <p className="py-6 text-center text-sm text-ink-muted">Belum ada sesi. Tambahkan sesi pertama.</p>
      ) : (
        <ol className="flex flex-col gap-2">
          {sessions.map((s, i) => (
            <li key={s.id} className="flex items-center gap-3 rounded-base border border-border px-3.5 py-2.5">
              <span className="w-6 shrink-0 text-[12px] font-bold text-ink-subtle">{i + 1}</span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[13.5px] font-semibold text-ink">{s.title}</span>
                <span className="block text-[11.5px] text-ink-subtle">
                  {s.type}{s.durationSeconds != null && ` · ${minutesLabel(s.durationSeconds)}`}
                </span>
              </span>
              <div className="flex shrink-0 items-center gap-1">
                {s.type === "Live" && (
                  <button
                    type="button"
                    onClick={() => setAttendanceFor(s.id)}
                    className="rounded px-2 py-1 text-[11.5px] font-bold text-primary hover:bg-primary-soft"
                  >
                    Kehadiran
                  </button>
                )}
                {s.type === "Video" && (
                  <button
                    type="button"
                    onClick={() => setTestFor({ sessionId: s.id, assessmentId: s.assessmentId ?? null })}
                    className="rounded px-2 py-1 text-[11.5px] font-bold text-primary hover:bg-primary-soft"
                  >
                    {s.assessmentId ? "Tes ✓" : "Tes"}
                  </button>
                )}
                {s.type === "FinalAssessment" && s.assessmentId && (
                  <Link
                    href={`/admin/assessments/${s.assessmentId}`}
                    className="rounded px-2 py-1 text-[11.5px] font-bold text-primary hover:bg-primary-soft"
                  >
                    Susun soal
                  </Link>
                )}
                <button type="button" disabled={busy || i === 0} onClick={() => move(i, -1)}
                  aria-label="Naikkan" className="rounded px-1.5 py-1 text-ink-muted hover:bg-surface-2 disabled:opacity-30">↑</button>
                <button type="button" disabled={busy || i === sessions.length - 1} onClick={() => move(i, 1)}
                  aria-label="Turunkan" className="rounded px-1.5 py-1 text-ink-muted hover:bg-surface-2 disabled:opacity-30">↓</button>
                <button type="button" disabled={busy} onClick={() => remove(s)}
                  aria-label="Hapus" className="rounded px-1.5 py-1 text-ink-subtle hover:text-danger disabled:opacity-30">
                  <XIcon size={14} />
                </button>
              </div>
            </li>
          ))}
        </ol>
      )}

      {attendanceFor && (
        <AttendanceModal
          token={token}
          sessionId={attendanceFor}
          onClose={() => setAttendanceFor(null)}
        />
      )}

      {testFor && (
        <GatingTestEditor
          token={token}
          sessionId={testFor.sessionId}
          assessmentId={testFor.assessmentId}
          onClose={async () => { setTestFor(null); await qc.invalidateQueries({ queryKey: key }); }}
        />
      )}

      {adding && (
        <SessionForm
          token={token}
          programId={program.id}
          nextOrder={sessions.length + 1}
          onClose={async () => { setAdding(false); await qc.invalidateQueries({ queryKey: key }); }}
        />
      )}
    </Modal>
  );
}
