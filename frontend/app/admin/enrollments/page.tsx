"use client";

import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Modal, Spinner, ErrorState, SearchIcon } from "@/components/ui";
import {
  listAdminEnrollments, grantEnrollment, revokeEnrollment, listPrograms,
  formatIdr, fmtDateTime, num, type AdminEnrollment,
} from "@/lib/programs";

const STATUSES = ["", "PendingPayment", "Active", "Completed", "Revoked"] as const;
const STATUS_LABEL: Record<string, string> = {
  PendingPayment: "Menunggu bayar", Active: "Aktif", Completed: "Selesai", Revoked: "Dicabut",
};

export default function AdminEnrollmentsPage() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [programId, setProgramId] = useState("");
  const [granting, setGranting] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  const programs = useQuery({
    queryKey: ["admin-programs"],
    queryFn: () => listPrograms(token!),
    enabled: !!token,
  });

  const enrollments = useQuery({
    queryKey: ["admin-enrollments", search, status, programId],
    queryFn: () => listAdminEnrollments(token!, {
      search: search || undefined,
      status: status || undefined,
      programId: programId || undefined,
    }),
    enabled: !!token,
  });

  async function revoke(e: AdminEnrollment) {
    if (!token || !confirm(`Cabut akses ${e.userName} dari ${e.programName}?\n\nProgres peserta TIDAK dihapus.`)) return;
    try {
      await revokeEnrollment(token, e.id);
      qc.invalidateQueries({ queryKey: ["admin-enrollments"] });
    } catch (err) {
      alert(err instanceof Error ? err.message : "Gagal mencabut akses.");
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-ink-subtle">
          Pendaftaran peserta. Mencabut akses tidak menghapus progres, percobaan, atau sertifikat.
        </p>
        <div className="flex flex-wrap items-center gap-2">
          <select value={programId} onChange={(e) => setProgramId(e.target.value)} className={selectCls}>
            <option value="">Semua program</option>
            {(programs.data ?? []).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
          <select value={status} onChange={(e) => setStatus(e.target.value)} className={selectCls}>
            {STATUSES.map((s) => <option key={s} value={s}>{s ? STATUS_LABEL[s] : "Semua status"}</option>)}
          </select>
          <div className="relative">
            <SearchIcon size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-subtle" />
            <input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Cari nama/email…"
              className="w-[190px] rounded-sm border border-border bg-surface py-2 pl-8 pr-3 text-[13px] outline-none focus:border-primary"
            />
          </div>
          <Button size="sm" onClick={() => setGranting(true)}>+ Beri akses</Button>
        </div>
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
        {!token || enrollments.isPending ? (
          <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
        ) : enrollments.isError ? (
          <div className="p-6">
            <ErrorState title="Gagal memuat pendaftaran"
              action={<Button variant="neutral" size="sm" onClick={() => enrollments.refetch()}>Muat ulang</Button>} />
          </div>
        ) : enrollments.data.items.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">Tidak ada pendaftaran.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] border-collapse">
              <thead>
                <tr className="bg-surface-2 text-left text-xs font-bold text-ink-muted">
                  <th className="px-5 py-3">Peserta</th>
                  <th className="px-3 py-3">Program</th>
                  <th className="px-3 py-3">Status</th>
                  <th className="px-3 py-3">Progres</th>
                  <th className="px-3 py-3">Dibayar</th>
                  <th className="px-5 py-3 text-right">Aksi</th>
                </tr>
              </thead>
              <tbody className="text-[13.5px]">
                {enrollments.data.items.map((e) => (
                  <tr key={e.id} className="border-t border-border">
                    <td className="px-5 py-3.5">
                      <div className="font-semibold">{e.userName}</div>
                      <div className="text-[11.5px] text-ink-subtle">{e.userEmail}</div>
                    </td>
                    <td className="px-3 py-3.5">{e.programName}</td>
                    <td className="px-3 py-3.5">
                      <Badge
                        tone={e.status === "Active" || e.status === "Completed" ? "success"
                            : e.status === "Revoked" ? "danger" : "neutral"}
                        className="px-2.5 py-0.5 text-[11.5px]"
                      >
                        {STATUS_LABEL[e.status] ?? e.status}
                      </Badge>
                    </td>
                    <td className="px-3 py-3.5">
                      {num(e.completedSessions)}/{num(e.totalSessions)} sesi
                    </td>
                    <td className="px-3 py-3.5">
                      {num(e.amountPaidIdr) > 0 ? formatIdr(e.amountPaidIdr) : <span className="text-ink-subtle">—</span>}
                      {e.enrolledAt && <div className="text-[11px] text-ink-subtle">{fmtDateTime(e.enrolledAt)}</div>}
                    </td>
                    <td className="px-5 py-3.5">
                      <div className="flex justify-end">
                        {e.status !== "Revoked" && (
                          <Button variant="neutral" size="sm" onClick={() => revoke(e)}>Cabut</Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {granting && token && (
        <GrantModal
          token={token}
          programs={(programs.data ?? []).map((p) => ({ id: p.id, name: p.name }))}
          onClose={() => { setGranting(false); qc.invalidateQueries({ queryKey: ["admin-enrollments"] }); }}
        />
      )}
    </div>
  );
}

function GrantModal({
  token, programs, onClose,
}: { token: string; programs: { id: string; name: string }[]; onClose: () => void }) {
  const [email, setEmail] = useState("");
  const [programId, setProgramId] = useState(programs[0]?.id ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setBusy(true); setError(null);
    try {
      await grantEnrollment(token, email.trim(), programId);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal memberi akses.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Beri akses manual"
      className="max-w-md"
      footer={
        <div className="flex w-full justify-end gap-2">
          <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
          <Button size="sm" onClick={save} loading={busy} disabled={!email.trim() || !programId}>Beri akses</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}
        <p className="rounded-base bg-surface-2 px-3 py-2 text-[12.5px] leading-relaxed text-ink-muted">
          Jalur dukungan: memberi akses tanpa pembayaran (kompensasi/koreksi refund).
          Tindakan ini tercatat di audit log.
        </p>
        <label className="flex flex-col gap-1">
          <span className="text-[12px] font-bold text-ink-muted">Email peserta</span>
          <input value={email} onChange={(e) => setEmail(e.target.value)} className={inputCls} placeholder="nama@email.com" />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-[12px] font-bold text-ink-muted">Program</span>
          <select value={programId} onChange={(e) => setProgramId(e.target.value)} className={inputCls}>
            {programs.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
        </label>
      </div>
    </Modal>
  );
}

const inputCls =
  "w-full rounded-sm border border-border bg-surface px-3 py-2 text-[13.5px] outline-none focus:border-primary";
const selectCls =
  "rounded-sm border border-border bg-surface px-2.5 py-2 text-[13px] outline-none focus:border-primary";
