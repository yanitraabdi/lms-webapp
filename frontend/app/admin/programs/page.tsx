"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { PageTitle } from "@/components/ui/Page";
import { Badge, Button, Spinner, ErrorState } from "@/components/ui";
import { ProgramForm } from "@/components/admin/ProgramForm";
import { SessionManager } from "@/components/admin/SessionManager";
import { ReadinessPanel } from "@/components/admin/ReadinessPanel";
import {
  listPrograms,
  formatIdr, num,
  type AdminProgram,
} from "@/lib/programs";

export default function AdminProgramsPage() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();
  const [editing, setEditing] = useState<AdminProgram | null>(null);
  const [creating, setCreating] = useState(false);
  const [sessionsFor, setSessionsFor] = useState<AdminProgram | null>(null);
  const [readinessFor, setReadinessFor] = useState<AdminProgram | null>(null);

  const programs = useQuery({
    queryKey: ["admin-programs"],
    queryFn: () => listPrograms(token!),
    enabled: !!token,
  });

  return (
    <div>
      <PageTitle size="compact">Program</PageTitle>
      <div className="mt-3 flex items-center justify-between gap-2">
        <p className="text-sm text-ink-muted">
          Manage programs, their sessions, and session order. Session order determines the step-by-step lock.
        </p>
        <Button size="sm" onClick={() => setCreating(true)}>+ New program</Button>
      </div>

      <div className="mt-6 overflow-x-auto">
        {!token || programs.isPending ? (
          <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
        ) : programs.isError ? (
          <div className="p-6">
            <ErrorState title="Couldn't load programs"
              action={<Button variant="neutral" size="sm" onClick={() => programs.refetch()}>Reload</Button>} />
          </div>
        ) : programs.data.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">No programs yet.</p>
        ) : (
          <table className="data-table min-w-[640px]">
            <thead>
              <tr>
                <th>Program</th>
                <th>Price</th>
                <th>Sessions</th>
                <th>Learners</th>
                <th>Status</th>
                <th>Readiness</th>
                <th className="num">Actions</th>
              </tr>
            </thead>
            <tbody className="text-[13.5px]">
              {programs.data.map((p) => (
                <tr key={p.id}>
                  <td>
                    <div className="font-semibold">{p.name}</div>
                    <div className="text-[11.5px] text-ink-muted">/{p.slug}</div>
                  </td>
                  <td>{formatIdr(p.priceIdr)}</td>
                  <td>{num(p.sessionCount)}</td>
                  <td>{num(p.enrollmentCount)}</td>
                  <td>
                    <Badge tone={p.status === "Published" ? "success" : "neutral"} className="px-2.5 py-0.5 text-[11.5px]">
                      {p.status === "Published" ? "Published" : p.status === "Draft" ? "Draft" : "Archived"}
                    </Badge>
                  </td>
                  <td>
                    <button
                      type="button"
                      onClick={() => setReadinessFor(p)}
                      className="text-[12.5px] font-bold text-primary hover:underline"
                    >
                      Check
                    </button>
                  </td>
                  <td>
                    <div className="flex justify-end gap-2">
                      <Button variant="neutral" size="sm" onClick={() => setSessionsFor(p)}>Sessions</Button>
                      <Button variant="neutral" size="sm" onClick={() => setEditing(p)}>Edit</Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {(creating || editing) && token && (
        <ProgramForm
          token={token}
          program={editing}
          onClose={() => { setCreating(false); setEditing(null); qc.invalidateQueries({ queryKey: ["admin-programs"] }); }}
        />
      )}

      {readinessFor && token && (
        <ReadinessPanel token={token} program={readinessFor} onClose={() => setReadinessFor(null)} />
      )}

      {sessionsFor && token && (
        <SessionManager
          token={token}
          program={sessionsFor}
          onClose={() => { setSessionsFor(null); qc.invalidateQueries({ queryKey: ["admin-programs"] }); }}
        />
      )}
    </div>
  );
}
