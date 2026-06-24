"use client";

import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Modal, Spinner, ErrorState, SearchIcon } from "@/components/ui";
import {
  getAdminPlans,
  getUser,
  grantPlan,
  listUsers,
  num,
  revokePlan,
  setUserRole,
  setUserStatus,
} from "@/lib/admin";

const TIER_NAME: Record<number, string> = { 0: "Gratis", 1: "Basic", 2: "Intermediate", 3: "Advanced" };
const fmtDate = (iso: string) => new Date(iso).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" });

export default function AdminUsersPage() {
  const { accessToken: token, user } = useAuth();
  const isSuper = user?.role === "SuperAdmin";
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [selected, setSelected] = useState<string | null>(null);

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  const users = useQuery({
    queryKey: ["admin-users", search, status],
    queryFn: () => listUsers(token!, { search, status: status || undefined, take: 50 }),
    enabled: !!token,
  });

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative">
          <SearchIcon size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-subtle" />
          <input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Cari email atau nama…"
            className="w-[240px] rounded-sm border border-border bg-surface py-2 pl-8 pr-3 text-[13px] outline-none focus:border-primary"
          />
        </div>
        <select value={status} onChange={(e) => setStatus(e.target.value)} className="rounded-sm border border-border bg-surface px-3 py-2 text-[13px] font-semibold outline-none focus:border-primary">
          <option value="">Semua status</option>
          <option value="Active">Aktif</option>
          <option value="Suspended">Ditangguhkan</option>
        </select>
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
        {!token || users.isPending ? (
          <div className="flex min-h-[160px] items-center justify-center"><Spinner size={22} /></div>
        ) : users.isError ? (
          <div className="p-6"><ErrorState title="Gagal memuat pengguna" action={<Button variant="neutral" size="sm" onClick={() => users.refetch()}>Muat ulang</Button>} /></div>
        ) : users.data.users.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">Tidak ada pengguna.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] border-collapse">
              <thead>
                <tr className="bg-surface-2 text-left text-xs font-bold text-ink-muted">
                  <th className="px-5 py-3">Pengguna</th>
                  <th className="px-3 py-3">Peran</th>
                  <th className="px-3 py-3">Status</th>
                  <th className="px-3 py-3">Paket</th>
                  <th className="px-5 py-3 text-right">Aksi</th>
                </tr>
              </thead>
              <tbody className="text-[13.5px]">
                {users.data.users.map((u) => (
                  <tr key={u.id} className="border-t border-border">
                    <td className="px-5 py-3">
                      <div className="font-semibold">{u.name}</div>
                      <div className="text-[12px] text-ink-muted">{u.email}</div>
                    </td>
                    <td className="px-3 py-3">{u.role}</td>
                    <td className="px-3 py-3">
                      <Badge tone={u.status === "Active" ? "success" : "warning"} className="px-2.5 py-0.5 text-[11.5px]">
                        {u.status === "Active" ? "Aktif" : "Ditangguhkan"}
                      </Badge>
                    </td>
                    <td className="px-3 py-3 text-ink-muted">{u.activeTier == null ? "Gratis" : TIER_NAME[num(u.activeTier)]}</td>
                    <td className="px-5 py-3 text-right">
                      <button type="button" onClick={() => setSelected(u.id)} className="text-[12.5px] font-bold text-primary hover:underline">Kelola</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {selected && token && (
        <UserDetail token={token} userId={selected} isSuper={isSuper} onClose={() => setSelected(null)} />
      )}
    </div>
  );
}

function UserDetail({ token, userId, isSuper, onClose }: { token: string; userId: string; isSuper: boolean; onClose: () => void }) {
  const qc = useQueryClient();
  const detail = useQuery({ queryKey: ["admin-user", userId], queryFn: () => getUser(token, userId) });
  const plans = useQuery({ queryKey: ["admin-plans"], queryFn: () => getAdminPlans(token) });
  const [planId, setPlanId] = useState("");
  const [days, setDays] = useState(30);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function act(fn: () => Promise<void>) {
    setError(null);
    setBusy(true);
    try {
      await fn();
      await qc.invalidateQueries({ queryKey: ["admin-user", userId] });
      await qc.invalidateQueries({ queryKey: ["admin-users"] });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal.");
    } finally {
      setBusy(false);
    }
  }

  const d = detail.data;
  const paid = (plans.data ?? []).filter((p) => num(p.tierLevel) >= 1);

  return (
    <Modal open onClose={onClose} title="Kelola pengguna" className="max-w-lg">
      {detail.isPending || !d ? (
        <div className="flex min-h-[160px] items-center justify-center"><Spinner size={20} /></div>
      ) : (
        <div className="flex flex-col gap-4 text-ink">
          {error && <p className="rounded-base bg-danger-soft px-3 py-2 text-[12.5px] text-danger">{error}</p>}
          <div>
            <div className="text-base font-extrabold">{d.name}</div>
            <div className="text-[12.5px] text-ink-muted">{d.email} · bergabung {fmtDate(d.createdAt)}</div>
          </div>

          <div className="grid grid-cols-2 gap-3 rounded-base bg-surface-2 p-3 text-[12.5px]">
            <Fact label="Peran" value={d.role} />
            <Fact label="Status" value={d.status === "Active" ? "Aktif" : "Ditangguhkan"} />
            <Fact label="Paket" value={d.planName ?? "Gratis"} />
            <Fact label="Langganan" value={d.subscriptionStatus ?? "—"} />
            <Fact label="Modul selesai" value={String(num(d.completedModules))} />
            <Fact label="Sertifikat" value={String(num(d.certificateCount))} />
          </div>

          {/* status */}
          <div className="flex items-center justify-between gap-2 border-t border-surface-2 pt-3">
            <span className="text-[13px] font-bold">Status akun</span>
            {d.status === "Active" ? (
              <Button size="sm" variant="neutral" className="border-[#F1C9C9] text-danger" loading={busy} onClick={() => act(() => setUserStatus(token, userId, "Suspended"))}>Tangguhkan</Button>
            ) : (
              <Button size="sm" loading={busy} onClick={() => act(() => setUserStatus(token, userId, "Active"))}>Aktifkan</Button>
            )}
          </div>

          {/* role (superadmin) */}
          {isSuper && (
            <div className="flex items-center justify-between gap-2 border-t border-surface-2 pt-3">
              <span className="text-[13px] font-bold">Peran</span>
              <select
                value={d.role}
                disabled={busy}
                onChange={(e) => act(() => setUserRole(token, userId, e.target.value))}
                className="rounded-sm border border-border bg-surface px-3 py-1.5 text-[13px] font-semibold outline-none focus:border-primary"
              >
                <option value="User">User</option>
                <option value="Admin">Admin</option>
                <option value="SuperAdmin">SuperAdmin</option>
              </select>
            </div>
          )}

          {/* grant / revoke */}
          <div className="flex flex-col gap-2 border-t border-surface-2 pt-3">
            <span className="text-[13px] font-bold">Beri / cabut paket (komplimen)</span>
            <div className="flex flex-wrap items-center gap-2">
              <select value={planId} onChange={(e) => setPlanId(e.target.value)} className="rounded-sm border border-border bg-surface px-3 py-1.5 text-[13px] outline-none focus:border-primary">
                <option value="">Pilih paket…</option>
                {paid.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
              <input type="number" min={1} value={days} onChange={(e) => setDays(Number(e.target.value))} className="w-20 rounded-sm border border-border bg-surface px-2 py-1.5 text-[13px] outline-none focus:border-primary" />
              <span className="text-[12px] text-ink-muted">hari</span>
              <Button size="sm" disabled={!planId || busy} onClick={() => act(() => grantPlan(token, userId, planId, days))}>Beri</Button>
              <Button size="sm" variant="neutral" disabled={busy} onClick={() => act(() => revokePlan(token, userId))}>Cabut</Button>
            </div>
          </div>
        </div>
      )}
    </Modal>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-[11px] text-ink-subtle">{label}</div>
      <div className="font-bold text-ink">{value}</div>
    </div>
  );
}
