"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { OnboardingFlow } from "@/components/onboarding/OnboardingFlow";
import { Badge, Button, Spinner, ErrorState, EmptyState, CheckIcon, LockIcon, PlayIcon } from "@/components/ui";
import {
  listMyEnrollments, getStudentProgram, num, SESSION_TYPE_LABEL,
  type Enrollment, type StudentSession,
} from "@/lib/programs";
import { listMyCertificates } from "@/lib/sessions";

export default function DashboardPage() {
  const { status, accessToken, user } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login?next=/app/dashboard");
  }, [status, router]);

  const enrollments = useQuery({
    queryKey: ["my-enrollments"],
    queryFn: () => listMyEnrollments(accessToken!),
    enabled: status === "authenticated" && !!accessToken,
  });

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }

  const firstName = (user?.name ?? "").split(" ")[0] || "kembali";
  const active = (enrollments.data ?? []).filter(
    (e) => e.status === "Active" || e.status === "Completed");

  return (
    <div className="min-h-screen bg-bg">
      <AppHeader />
      <OnboardingFlow token={accessToken} />
      <div className="mx-auto max-w-3xl px-6 pb-16 pt-7">
        <div className="mb-6 flex flex-col gap-1">
          <h1 className="text-[24px] font-extrabold tracking-tight">Halo, {firstName} 👋</h1>
          <p className="text-sm text-ink-muted">Lanjutkan persiapan TOEFL Anda.</p>
        </div>

        {enrollments.isPending ? (
          <div className="flex min-h-[200px] items-center justify-center"><Spinner size={24} /></div>
        ) : enrollments.isError ? (
          <ErrorState
            title="Gagal memuat program"
            action={<Button variant="neutral" size="sm" onClick={() => enrollments.refetch()}>Muat ulang</Button>}
          />
        ) : active.length === 0 ? (
          <EmptyState
            title="Belum ada program aktif"
            message="Daftar program persiapan TOEFL untuk mulai belajar."
            action={
              <Link
                href="/program/toefl-preparation"
                className="inline-flex h-10 items-center justify-center rounded-sm bg-primary px-4 text-[13px] font-bold text-primary-ink hover:bg-primary-hover"
              >
                Lihat program
              </Link>
            }
          />
        ) : (
          <div className="flex flex-col gap-5" data-tour="overall-progress">
            {active.map((e) => <ProgramCard key={e.id} token={accessToken} enrollment={e} />)}
          </div>
        )}

        <CertificatesTeaser token={accessToken} />

        {/* Pending payments surface here so a learner is never left wondering. */}
        {(enrollments.data ?? []).some((e) => e.status === "PendingPayment") && (
          <div className="mt-6 rounded-base border border-[#F5D9A8] bg-warning-soft px-5 py-4">
            <p className="text-[13px] leading-relaxed text-ink">
              <strong>Pembayaran sedang diproses.</strong> Akses terbuka otomatis setelah pembayaran
              dikonfirmasi. Jika sudah membayar dan program belum terbuka, hubungi kami lewat{" "}
              <Link href="/contact" className="font-bold text-primary hover:underline">halaman kontak</Link>.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}

function ProgramCard({ token, enrollment }: { token: string; enrollment: Enrollment }) {
  const q = useQuery({
    queryKey: ["student-program", enrollment.programId],
    queryFn: () => getStudentProgram(token, enrollment.programId),
    retry: false,
  });

  if (q.isPending) {
    return (
      <div className="flex min-h-[140px] items-center justify-center rounded-lg border border-border bg-surface">
        <Spinner size={20} />
      </div>
    );
  }
  if (q.isError) {
    return (
      <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
        <h2 className="text-lg font-extrabold">{enrollment.programName}</h2>
        <p className="mt-1 text-[13px] text-ink-muted">Program tidak dapat dimuat saat ini.</p>
      </div>
    );
  }

  const data = q.data;
  const sessions = [...data.sessions].sort((a, b) => num(a.orderIndex) - num(b.orderIndex));
  const completed = num(data.completedCount);
  const total = num(data.sessionCount);
  const percent = total > 0 ? Math.round((completed / total) * 100) : 0;
  const next = sessions.find((s) => s.id === data.nextSessionId);

  return (
    <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h2 className="text-lg font-extrabold">{data.name}</h2>
          {data.batchName && <span className="text-[12.5px] text-ink-muted">Batch {data.batchName}</span>}
        </div>
        {completed === total ? (
          <Badge tone="success" className="px-2.5 py-1">Selesai</Badge>
        ) : (
          <Badge tone="neutral" className="px-2.5 py-1">{percent}%</Badge>
        )}
      </div>

      <div className="mt-3 flex items-center gap-3">
        <div className="h-[7px] flex-1 overflow-hidden rounded-full bg-surface-2">
          <div
            className={"h-full rounded-full " + (completed === total ? "bg-success" : "bg-primary")}
            style={{ width: `${percent}%` }}
          />
        </div>
        <span className="shrink-0 text-[12.5px] font-bold text-ink-muted">{completed}/{total} sesi</span>
      </div>

      {next ? (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-base border border-primary bg-primary-soft/50 px-4 py-3">
          <div className="flex min-w-0 flex-col">
            <span className="text-[11px] font-bold uppercase tracking-wide text-primary">Lanjutkan</span>
            <span className="truncate text-[13.5px] font-bold text-ink">{next.title}</span>
          </div>
          <Link
            href={nextHref(next, data.programId)}
            className="inline-flex h-9 shrink-0 items-center justify-center rounded-sm bg-primary px-3.5 text-[13px] font-bold text-primary-ink hover:bg-primary-hover"
          >
            Buka →
          </Link>
        </div>
      ) : (
        <p className="mt-4 rounded-base bg-success-soft px-4 py-3 text-[13px] font-semibold text-success">
          Semua sesi selesai. Sertifikat Anda tersedia di halaman Sertifikat.
        </p>
      )}

      <ul className="mt-4 flex flex-col gap-1.5 border-t border-border pt-3.5">
        {sessions.slice(0, 4).map((s) => <SessionLine key={s.id} session={s} />)}
        {sessions.length > 4 && (
          <li className="pt-1">
            <Link
              href={`/app/program/${data.programId}`}
              className="text-[12.5px] font-bold text-primary hover:underline"
            >
              Lihat semua {sessions.length} sesi →
            </Link>
          </li>
        )}
      </ul>
    </div>
  );
}

function SessionLine({ session }: { session: StudentSession }) {
  const done = session.state === "Completed";
  const locked = session.state === "Locked";
  return (
    <li className="flex items-center gap-2.5 text-[13px]">
      <span
        className={
          "inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-[10px] font-bold " +
          (done ? "bg-success text-white" : locked ? "border-2 border-border text-ink-subtle" : "bg-primary text-white")
        }
      >
        {done ? <CheckIcon size={11} strokeWidth={3} /> : locked ? <LockIcon size={10} /> : <PlayIcon size={9} />}
      </span>
      <span className={"min-w-0 flex-1 truncate " + (locked ? "text-ink-subtle" : "text-ink-muted")}>
        {session.title}
      </span>
      {session.type !== "Video" && (
        <span className="shrink-0 text-[10.5px] font-bold uppercase tracking-wide text-ink-subtle">
          {SESSION_TYPE_LABEL[session.type] ?? session.type}
        </span>
      )}
    </li>
  );
}

/**
 * The final assessment has its own runner. Everything else opens the programme page with the
 * session preselected, so "Lanjutkan" lands on the same master-detail view the session list uses.
 */
function nextHref(session: StudentSession, programId: string): string {
  return session.type === "FinalAssessment"
    ? `/app/assessment/${session.id}`
    : `/app/program/${programId}?session=${session.id}`;
}

function CertificatesTeaser({ token }: { token: string }) {
  const q = useQuery({
    queryKey: ["my-certificates"],
    queryFn: () => listMyCertificates(token),
  });

  if (!q.data || q.data.length === 0) return null;

  return (
    <div className="mt-6 rounded-lg border border-border bg-surface p-5 shadow-sm" data-tour="nav-certificates">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-col">
          <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">Sertifikat</span>
          <span className="text-[14px] font-bold text-ink">
            {q.data.length} sertifikat prediksi tersedia
          </span>
        </div>
        <Link href="/app/certificates" className="text-[13px] font-bold text-primary hover:underline">
          Lihat & unduh →
        </Link>
      </div>
    </div>
  );
}
