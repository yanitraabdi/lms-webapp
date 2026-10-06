"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Spinner, ErrorState, Button, Badge } from "@/components/ui";
import { getAnalytics, getEmailStatus, sendTestEmail, num } from "@/lib/admin";

// Enrollment status is the INVERTA lifecycle. Replaces a plan-tier map that named the archived
// subscription product's levels.
const ENROLLMENT_STATUS_LABEL: Record<string, string> = {
  PendingPayment: "Menunggu bayar",
  Active: "Aktif",
  Completed: "Selesai",
  Revoked: "Dicabut",
};

export default function AdminAnalyticsPage() {
  const token = useAuth().accessToken;
  const q = useQuery({ queryKey: ["admin-analytics"], queryFn: () => getAnalytics(token!), enabled: !!token });

  if (!token || q.isPending) return <div className="flex min-h-[200px] items-center justify-center"><Spinner size={24} /></div>;
  if (q.isError) return <ErrorState title="Gagal memuat analitik" action={<Button variant="neutral" size="sm" onClick={() => q.refetch()}>Muat ulang</Button>} />;

  const a = q.data;
  const kpis = [
    { label: "Total pengguna", value: num(a.totalUsers) },
    { label: "Akun baru (30 hari)", value: num(a.signupsLast30Days) },
    { label: "Peserta aktif", value: num(a.activeEnrollments) },
    { label: "Sesi selesai (30 hari)", value: num(a.sessionCompletionsLast30Days) },
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
          <h2 className="mb-3 text-sm font-extrabold">Pendaftaran per status</h2>
          {a.enrollmentsByStatus.length === 0 ? (
            <p className="text-sm text-ink-muted">Belum ada pendaftaran.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {a.enrollmentsByStatus.map((e) => (
                <li key={e.status} className="flex items-center justify-between text-sm">
                  <span className="text-ink-muted">{ENROLLMENT_STATUS_LABEL[e.status] ?? e.status}</span>
                  <span className="font-bold text-ink">{num(e.count)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
          <h2 className="mb-3 text-sm font-extrabold">Sesi paling banyak ditonton</h2>
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

      <EmailCard />
    </div>
  );
}

function EmailCard() {
  const { accessToken: token, user } = useAuth();
  const status = useQuery({ queryKey: ["admin-email-status"], queryFn: () => getEmailStatus(token!), enabled: !!token });
  const test = useMutation({ mutationFn: () => sendTestEmail(token!) });
  const isSmtp = status.data?.provider === "smtp";
  const rows = status.data
    ? [
        ["Server", status.data.host ? `${status.data.host}:${status.data.port}` : ""],
        ["Pengirim", status.data.fromAddress ? `${status.data.fromName ?? ""} <${status.data.fromAddress}>`.trim() : ""],
        ["Balasan ke", status.data.replyTo ?? ""],
      ].filter(([, v]) => v)
    : [];

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-5 shadow-sm">
      <div className="flex items-center gap-2">
        <h2 className="text-sm font-extrabold">Email</h2>
        {status.data && <Badge tone={isSmtp ? "neutral" : "warning"}>{isSmtp ? "SMTP" : "Dev — hanya log"}</Badge>}
      </div>
      {status.isPending && <Spinner size={18} />}
      {status.isError && <p className="text-sm text-danger">Gagal memuat status email.</p>}
      {status.data && (
        <>
          {!isSmtp && <p className="text-sm text-ink-muted">Email belum benar-benar dikirim. Atur EMAIL_PROVIDER=smtp untuk mengirim.</p>}
          <dl className="flex flex-col gap-1 text-sm">
            {rows.map(([k, v]) => (
              <div key={k} className="flex gap-3">
                <dt className="w-24 shrink-0 text-ink-muted">{k}</dt>
                <dd className="break-all font-semibold text-ink">{v}</dd>
              </div>
            ))}
          </dl>
          <p className="text-xs text-ink-muted">Dikirim ke {user?.email}</p>
          <div>
            <Button size="sm" loading={test.isPending} onClick={() => test.mutate()}>Kirim email uji</Button>
          </div>
        </>
      )}
      <p aria-live="polite" className={test.isError ? "text-sm text-danger" : test.data && !test.data.sent ? "text-sm text-ink-muted" : "text-sm text-ink"}>
        {test.isError ? test.error.message : test.data?.message}
      </p>
      <p className="text-xs text-ink-muted">Pengaturan email ada di .env server (EMAIL_PROVIDER, SMTP_*). Setelah mengubahnya, restart API.</p>
    </div>
  );
}
