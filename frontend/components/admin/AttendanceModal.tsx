"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Badge, Button, Modal, Spinner, CheckIcon } from "@/components/ui";
import { getAttendanceRoster, markAttendance, markAllAttendance, fmtDateTime, num } from "@/lib/programs";

/**
 * Admin-marked attendance for a live session (KAK §9.11). Marking attended is what completes the
 * session and unlocks the next one — there is no Zoom integration in v1, this is the human step.
 */
export function AttendanceModal({
  token, sessionId, onClose,
}: { token: string; sessionId: string; onClose: () => void }) {
  const [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["attendance", sessionId],
    queryFn: () => getAttendanceRoster(token, sessionId),
    retry: false,
  });

  async function toggle(userId: string, attended: boolean) {
    setBusy(true);
    try {
      await markAttendance(token, sessionId, [userId], attended);
      await q.refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Gagal menyimpan kehadiran.");
    } finally {
      setBusy(false);
    }
  }

  async function markAll() {
    if (!confirm("Tandai semua peserta hadir?")) return;
    setBusy(true);
    try {
      await markAllAttendance(token, sessionId);
      await q.refetch();
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Kehadiran sesi live"
      className="max-w-2xl"
      footer={
        <div className="flex w-full items-center justify-between gap-2">
          <Button variant="neutral" size="sm" onClick={markAll} loading={busy}>Tandai semua hadir</Button>
          <Button size="sm" onClick={onClose}>Selesai</Button>
        </div>
      }
    >
      {q.isPending ? (
        <div className="flex min-h-[160px] items-center justify-center"><Spinner size={22} /></div>
      ) : q.isError ? (
        <p className="py-6 text-center text-sm text-ink-muted">
          Kehadiran hanya berlaku untuk sesi bertipe Live.
        </p>
      ) : (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <div className="text-[14px] font-bold">{q.data.sessionTitle}</div>
              {q.data.scheduledAt && (
                <div className="text-[12px] text-ink-muted">{fmtDateTime(q.data.scheduledAt)}</div>
              )}
            </div>
            <Badge tone="neutral" className="px-2.5 py-1 text-[12px]">
              {num(q.data.attendedCount)}/{num(q.data.enrolledCount)} hadir
            </Badge>
          </div>

          {q.data.rows.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-muted">Belum ada peserta terdaftar.</p>
          ) : (
            <ul className="flex max-h-[360px] flex-col gap-1.5 overflow-y-auto">
              {q.data.rows.map((r) => (
                <li key={r.userId} className="flex items-center gap-3 rounded-base border border-border px-3.5 py-2.5">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-[13.5px] font-semibold text-ink">{r.name}</span>
                    <span className="block truncate text-[11.5px] text-ink-subtle">{r.email}</span>
                  </span>
                  {r.sessionCompleted && (
                    <span className="inline-flex items-center gap-1 text-[11px] font-bold text-success">
                      <CheckIcon size={12} strokeWidth={3} /> Selesai
                    </span>
                  )}
                  <button
                    type="button"
                    role="switch"
                    aria-checked={r.attended}
                    aria-label={r.attended ? `Batalkan kehadiran ${r.name}` : `Tandai ${r.name} hadir`}
                    disabled={busy}
                    onClick={() => toggle(r.userId, !r.attended)}
                    className={"relative h-[24px] w-[42px] shrink-0 rounded-full transition-colors disabled:opacity-50 " +
                      (r.attended ? "bg-primary" : "bg-[#C9D4E2]")}
                  >
                    <span className={"absolute top-[3px] h-[18px] w-[18px] rounded-full bg-white shadow transition-all " +
                      (r.attended ? "left-[21px]" : "left-[3px]")} />
                  </button>
                </li>
              ))}
            </ul>
          )}

          <p className="text-[11.5px] leading-relaxed text-ink-subtle">
            Menandai hadir akan menyelesaikan sesi ini dan membuka sesi berikutnya. Membatalkan
            tanda hanya menghapus status kehadiran — progres yang sudah diperoleh tidak dicabut.
          </p>
        </div>
      )}
    </Modal>
  );
}
