"use client";

import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Spinner, ErrorState, Button } from "@/components/ui";
import { getAnalytics, num } from "@/lib/admin";

const TIER_NAME: Record<number, string> = { 0: "Gratis", 1: "Basic", 2: "Intermediate", 3: "Advanced" };

export default function AdminAnalyticsPage() {
  const token = useAuth().accessToken;
  const q = useQuery({ queryKey: ["admin-analytics"], queryFn: () => getAnalytics(token!), enabled: !!token });

  if (!token || q.isPending) return <div className="flex min-h-[200px] items-center justify-center"><Spinner size={24} /></div>;
  if (q.isError) return <ErrorState title="Gagal memuat analitik" action={<Button variant="neutral" size="sm" onClick={() => q.refetch()}>Muat ulang</Button>} />;

  const a = q.data;
  const kpis = [
    { label: "Total pengguna", value: num(a.totalUsers) },
    { label: "Pendaftaran (30 hari)", value: num(a.signupsLast30Days) },
    { label: "Langganan aktif", value: num(a.activeSubscriptions) },
    { label: "Penyelesaian (30 hari)", value: num(a.completionsLast30Days) },
    { label: "Sertifikat terbit", value: num(a.certificatesIssued) },
  ];

  return (
    <div className="flex flex-col gap-6">
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
        {kpis.map((k) => (
          <div key={k.label} className="flex flex-col gap-1 rounded-lg border border-border bg-surface p-5 shadow-sm">
            <span className="text-[28px] font-extrabold tracking-tight text-ink">{k.value.toLocaleString("id-ID")}</span>
            <span className="text-[12.5px] text-ink-muted">{k.label}</span>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
          <h2 className="mb-3 text-sm font-extrabold">Langganan aktif per tier</h2>
          {a.activeByTier.length === 0 ? (
            <p className="text-sm text-ink-muted">Belum ada langganan aktif.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {a.activeByTier.map((t) => (
                <li key={num(t.tier)} className="flex items-center justify-between text-sm">
                  <span className="text-ink-muted">{TIER_NAME[num(t.tier)] ?? t.name}</span>
                  <span className="font-bold text-ink">{num(t.count)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
          <h2 className="mb-3 text-sm font-extrabold">Modul paling banyak ditonton</h2>
          {a.mostWatched.length === 0 ? (
            <p className="text-sm text-ink-muted">Belum ada data tontonan.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {a.mostWatched.map((m, i) => (
                <li key={i} className="flex items-center justify-between gap-3 text-sm">
                  <span className="truncate text-ink-muted">{m.title}</span>
                  <span className="shrink-0 font-bold text-ink">{num(m.viewers)} penonton</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
