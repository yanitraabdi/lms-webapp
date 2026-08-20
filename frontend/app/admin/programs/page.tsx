"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Spinner, ErrorState } from "@/components/ui";
import { ProgramForm } from "@/components/admin/ProgramForm";
import { SessionManager } from "@/components/admin/SessionManager";
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

  const programs = useQuery({
    queryKey: ["admin-programs"],
    queryFn: () => listPrograms(token!),
    enabled: !!token,
  });

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs text-ink-subtle">
          Kelola program, sesi, dan urutannya. Urutan sesi menentukan penguncian bertahap.
        </p>
        <Button size="sm" onClick={() => setCreating(true)}>+ Program baru</Button>
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
        {!token || programs.isPending ? (
          <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
        ) : programs.isError ? (
          <div className="p-6">
            <ErrorState title="Gagal memuat program"
              action={<Button variant="neutral" size="sm" onClick={() => programs.refetch()}>Muat ulang</Button>} />
          </div>
        ) : programs.data.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">Belum ada program.</p>
        ) : (
          <table className="w-full min-w-[640px] border-collapse">
            <thead>
              <tr className="bg-surface-2 text-left text-xs font-bold text-ink-muted">
                <th className="px-5 py-3">Program</th>
                <th className="px-3 py-3">Harga</th>
                <th className="px-3 py-3">Sesi</th>
                <th className="px-3 py-3">Peserta</th>
                <th className="px-3 py-3">Status</th>
                <th className="px-5 py-3 text-right">Aksi</th>
              </tr>
            </thead>
            <tbody className="text-[13.5px]">
              {programs.data.map((p) => (
                <tr key={p.id} className="border-t border-border">
                  <td className="px-5 py-3.5">
                    <div className="font-semibold">{p.name}</div>
                    <div className="text-[11.5px] text-ink-subtle">/{p.slug}</div>
                  </td>
                  <td className="px-3 py-3.5">{formatIdr(p.priceIdr)}</td>
                  <td className="px-3 py-3.5">{num(p.sessionCount)}</td>
                  <td className="px-3 py-3.5">{num(p.enrollmentCount)}</td>
                  <td className="px-3 py-3.5">
                    <Badge tone={p.status === "Published" ? "success" : "neutral"} className="px-2.5 py-0.5 text-[11.5px]">
                      {p.status === "Published" ? "Terbit" : p.status === "Draft" ? "Draf" : "Arsip"}
                    </Badge>
                  </td>
                  <td className="px-5 py-3.5">
                    <div className="flex justify-end gap-2">
                      <Button variant="neutral" size="sm" onClick={() => setSessionsFor(p)}>Sesi</Button>
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
