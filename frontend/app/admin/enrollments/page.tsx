"use client";

import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { PageTitle } from "@/components/ui/Page";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Modal, Spinner, ErrorState, SearchIcon } from "@/components/ui";
import {
  listAdminEnrollments, grantEnrollment, revokeEnrollment, listPrograms,
  formatIdr, fmtDateTime, num, type AdminEnrollment,
} from "@/lib/programs";

const STATUSES = ["", "PendingPayment", "Active", "Completed", "Revoked"] as const;
const STATUS_LABEL: Record<string, string> = {
  PendingPayment: "Awaiting payment", Active: "Active", Completed: "Completed", Revoked: "Revoked",
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
    if (!token || !confirm(`Revoke ${e.userName}'s access to ${e.programName}?\n\nThe learner's progress is NOT deleted.`)) return;
    try {
      await revokeEnrollment(token, e.id);
      qc.invalidateQueries({ queryKey: ["admin-enrollments"] });
    } catch (err) {
      alert(err instanceof Error ? err.message : "Couldn't revoke access.");
    }
  }

  // Revoking must be reversible — an admin can revoke the wrong learner, and a payment dispute can
  // be resolved in the learner's favour. This reuses the grant endpoint, which returns the SAME
  // enrollment to Active rather than creating a second one, so the learner's progress is unchanged.
  async function restore(e: AdminEnrollment) {
    if (!token || !confirm(`Restore ${e.userName}'s access to ${e.programName}?`)) return;
    try {
      await grantEnrollment(token, e.userEmail, e.programId);
      qc.invalidateQueries({ queryKey: ["admin-enrollments"] });
    } catch (err) {
      alert(err instanceof Error ? err.message : "Couldn't restore access.");
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <PageTitle size="compact">Enrollments</PageTitle>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-ink-muted">
          Learner enrollments. Revoking access does not delete progress, attempts, or certificates.
        </p>
        <div className="flex flex-wrap items-center gap-2">
          <select aria-label="Program" value={programId} onChange={(e) => setProgramId(e.target.value)} className={selectCls}>
            <option value="">All programs</option>
            {(programs.data ?? []).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
          <select aria-label="Status" value={status} onChange={(e) => setStatus(e.target.value)} className={selectCls}>
            {STATUSES.map((s) => <option key={s} value={s}>{s ? STATUS_LABEL[s] : "All statuses"}</option>)}
          </select>
          <div className="relative">
            <SearchIcon size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-subtle" />
            <input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Search name/email…"
              className="w-[190px] rounded-sm border border-border bg-surface py-2 pl-8 pr-3 text-[13px] outline-none focus:border-primary"
            />
          </div>
          <Button size="sm" onClick={() => setGranting(true)}>+ Grant access</Button>
        </div>
      </div>

      <div>
        {!token || enrollments.isPending ? (
          <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
        ) : enrollments.isError ? (
          <div className="p-6">
            <ErrorState title="Couldn't load enrollments"
              action={<Button variant="neutral" size="sm" onClick={() => enrollments.refetch()}>Reload</Button>} />
          </div>
        ) : enrollments.data.items.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">No enrollments.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="data-table min-w-[720px]">
              <thead>
                <tr>
                  <th>Learner</th>
                  <th>Program</th>
                  <th>Status</th>
                  <th>Progress</th>
                  <th>Paid</th>
                  <th className="num">Actions</th>
                </tr>
              </thead>
              <tbody className="text-[13.5px]">
                {enrollments.data.items.map((e) => (
                  <tr key={e.id}>
                    <td>
                      <div className="font-semibold">{e.userName}</div>
                      <div className="text-[11.5px] text-ink-muted">{e.userEmail}</div>
                    </td>
                    <td>{e.programName}</td>
                    <td>
                      <Badge
                        tone={e.status === "Active" || e.status === "Completed" ? "success"
                            : e.status === "Revoked" ? "danger" : "neutral"}
                        className="px-2.5 py-0.5 text-[11.5px]"
                      >
                        {STATUS_LABEL[e.status] ?? e.status}
                      </Badge>
                    </td>
                    <td>
                      {num(e.completedSessions)}/{num(e.totalSessions)} sessions
                    </td>
                    <td>
                      {num(e.amountPaidIdr) > 0 ? formatIdr(e.amountPaidIdr) : <span className="text-ink-muted">None</span>}
                      {e.enrolledAt && <div className="text-[11px] text-ink-muted">{fmtDateTime(e.enrolledAt, "en-GB")}</div>}
                    </td>
                    <td>
                      <div className="flex justify-end">
                        {e.status === "Revoked" ? (
                          <Button variant="neutral" size="sm" onClick={() => restore(e)}>Restore</Button>
                        ) : (
                          <Button variant="neutral" size="sm" onClick={() => revoke(e)}>Revoke</Button>
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
      setError(e instanceof Error ? e.message : "Couldn't grant access.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Grant access manually"
      className="max-w-md"
      footer={
        <div className="flex w-full justify-end gap-2">
          <Button variant="neutral" size="sm" onClick={onClose}>Cancel</Button>
          <Button size="sm" onClick={save} loading={busy} disabled={!email.trim() || !programId}>Grant access</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}
        <p className="rounded-base bg-surface-2 px-3 py-2 text-[12.5px] leading-relaxed text-ink-muted">
          Support path: grants access without payment (compensation or refund correction).
          This action is recorded in the audit log.
        </p>
        <label className="flex flex-col gap-1">
          <span className="text-[12px] font-bold text-ink-muted">Learner email</span>
          <input value={email} onChange={(e) => setEmail(e.target.value)} className={inputCls} placeholder="name@email.com" />
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
