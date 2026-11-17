"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Spinner, ErrorState, Button, Badge } from "@/components/ui";
import { Chapter, PageTitle } from "@/components/ui/Page";
import { getAnalytics, getEmailStatus, sendTestEmail, num } from "@/lib/admin";

// Enrollment status is the INVERTA lifecycle. Replaces a plan-tier map that named the archived
// subscription product's levels.
const ENROLLMENT_STATUS_LABEL: Record<string, string> = {
  PendingPayment: "Awaiting payment",
  Active: "Active",
  Completed: "Completed",
  Revoked: "Revoked",
};

export default function AdminAnalyticsPage() {
  const token = useAuth().accessToken;
  const q = useQuery({ queryKey: ["admin-analytics"], queryFn: () => getAnalytics(token!), enabled: !!token });

  if (!token || q.isPending) return <div className="flex min-h-[200px] items-center justify-center"><Spinner size={24} /></div>;
  if (q.isError) return <ErrorState title="Couldn't load analytics" action={<Button variant="neutral" size="sm" onClick={() => q.refetch()}>Reload</Button>} />;

  const a = q.data;
  const kpis = [
    { label: "Total users", value: num(a.totalUsers) },
    { label: "New accounts (30 days)", value: num(a.signupsLast30Days) },
    { label: "Active learners", value: num(a.activeEnrollments) },
    { label: "Sessions completed (30 days)", value: num(a.sessionCompletionsLast30Days) },
    { label: "Certificates issued", value: num(a.certificatesIssued) },
  ];

  return (
    <div>
      <PageTitle size="compact">Analytics</PageTitle>
      <div className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
        {kpis.map((k) => (
          <div key={k.label} className="flex flex-col gap-1">
            <span className="text-[28px] font-extrabold tracking-tight text-ink">{k.value.toLocaleString("id-ID")}</span>
            <span className="text-[12.5px] text-ink-muted">{k.label}</span>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-x-8 lg:grid-cols-2">
        <Chapter title="Enrollments by status">
          {a.enrollmentsByStatus.length === 0 ? (
            <p className="mt-3 text-sm text-ink-muted">No enrollments yet.</p>
          ) : (
            <ul className="mt-3 flex flex-col gap-2">
              {a.enrollmentsByStatus.map((e) => (
                <li key={e.status} className="flex items-center justify-between text-sm">
                  <span className="text-ink-muted">{ENROLLMENT_STATUS_LABEL[e.status] ?? e.status}</span>
                  <span className="font-bold text-ink">{num(e.count)}</span>
                </li>
              ))}
            </ul>
          )}
        </Chapter>

        <Chapter title="Most-watched sessions">
          {a.mostWatched.length === 0 ? (
            <p className="mt-3 text-sm text-ink-muted">No viewing data yet.</p>
          ) : (
            <ul className="mt-3 flex flex-col gap-2">
              {a.mostWatched.map((m, i) => (
                <li key={i} className="flex items-center justify-between gap-3 text-sm">
                  <span className="truncate text-ink-muted">{m.title}</span>
                  <span className="shrink-0 font-bold text-ink">{num(m.viewers)} viewers</span>
                </li>
              ))}
            </ul>
          )}
        </Chapter>
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
        ["Sender", status.data.fromAddress ? `${status.data.fromName ?? ""} <${status.data.fromAddress}>`.trim() : ""],
        ["Reply-to", status.data.replyTo ?? ""],
      ].filter(([, v]) => v)
    : [];

  return (
    <Chapter title="Email" className="flex flex-col gap-3">
      {status.data && <div><Badge tone={isSmtp ? "neutral" : "warning"}>{isSmtp ? "SMTP" : "Dev, log only"}</Badge></div>}
      {status.isPending && <Spinner size={18} />}
      {status.isError && <p className="text-sm text-danger">Couldn&apos;t load email status.</p>}
      {status.data && (
        <>
          {!isSmtp && <p className="text-sm text-ink-muted">Emails are not actually sent. Set EMAIL_PROVIDER=smtp to send them.</p>}
          <dl className="flex flex-col gap-1 text-sm">
            {rows.map(([k, v]) => (
              <div key={k} className="flex gap-3">
                <dt className="w-24 shrink-0 text-ink-muted">{k}</dt>
                <dd className="break-all font-semibold text-ink">{v}</dd>
              </div>
            ))}
          </dl>
          <p className="text-xs text-ink-muted">Sent to {user?.email}</p>
          <div>
            <Button size="sm" loading={test.isPending} onClick={() => test.mutate()}>Send test email</Button>
          </div>
        </>
      )}
      <p aria-live="polite" className={test.isError ? "text-sm text-danger" : test.data && !test.data.sent ? "text-sm text-ink-muted" : "text-sm text-ink"}>
        {test.isError ? test.error.message : test.data?.message}
      </p>
      <p className="text-xs text-ink-muted">Email settings live in the server .env (EMAIL_PROVIDER, SMTP_*). Restart the API after changing them.</p>
    </Chapter>
  );
}
