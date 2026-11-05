"use client";

import { useEffect } from "react";
import dynamic from "next/dynamic";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { LearnerShell } from "@/components/app/LearnerShell";
import { OnboardingFlow } from "@/components/onboarding/OnboardingFlow";
import { Button, Spinner, ErrorState, EmptyState, PageTitle, Chapter, Toc, TocItem } from "@/components/ui";
import { ProgressHeader, nextHref } from "@/components/dashboard/ProgressHeader";
import { NextUpPanel } from "@/components/dashboard/NextUpPanel";
import { FinalResultCard } from "@/components/dashboard/FinalResultCard";
import { CertificateCards } from "@/components/dashboard/CertificateCards";
import { listMyEnrollments, getStudentProgram, num, SESSION_TYPE_LABEL, type Enrollment } from "@/lib/programs";
import { listMyCertificates } from "@/lib/sessions";
import { tocState, tocMeta } from "@/lib/toc";

const SessionTestHistory = dynamic(() => import("@/components/dashboard/SessionTestHistory"), {
  ssr: false,
  loading: () => <div className="mt-12 h-[200px]" aria-busy="true" />,
});

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

  const firstName = (user?.name ?? "").split(" ")[0] || "";
  const active = (enrollments.data ?? []).filter(
    (e) => e.status === "Active" || e.status === "Completed");

  return (
    <LearnerShell>
      <OnboardingFlow token={accessToken} />
      <div className="mx-auto max-w-6xl px-4 py-6 sm:px-8">
        <PageTitle>{`Selamat datang kembali${firstName ? `, ${firstName}` : ""}`}</PageTitle>

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
          <ProgramDashboard token={accessToken} enrollment={active[0]} />
        )}

        {/* Pending payments surface here so a learner is never left wondering. */}
        {(enrollments.data ?? []).some((e) => e.status === "PendingPayment") && (
          <div className="mt-8 rounded-base border border-[#F5D9A8] bg-warning-soft px-5 py-4">
            <p className="text-[13px] leading-relaxed text-ink">
              <strong>Pembayaran sedang diproses.</strong> Akses terbuka otomatis setelah pembayaran
              dikonfirmasi. Jika sudah membayar dan program belum terbuka, hubungi kami lewat{" "}
              <Link href="/contact" className="font-bold text-primary hover:underline">halaman kontak</Link>.
            </p>
          </div>
        )}
      </div>
    </LearnerShell>
  );
}

/** Dashboard blocks for one programme. ponytail: one programme exists; per-programme sections when a second one ships. */
function ProgramDashboard({ token, enrollment }: { token: string; enrollment: Enrollment }) {
  const program = useQuery({
    queryKey: ["student-program", enrollment.programId],
    queryFn: () => getStudentProgram(token, enrollment.programId),
    retry: false,
  });
  const certs = useQuery({
    queryKey: ["my-certificates"],
    queryFn: () => listMyCertificates(token),
  });

  if (program.isPending) {
    return <div className="flex min-h-[200px] items-center justify-center"><Spinner size={24} /></div>;
  }
  if (program.isError) {
    return (
      <Chapter title={enrollment.programName}>
        <p className="mt-3 text-ink-muted">Program tidak dapat dimuat saat ini.</p>
      </Chapter>
    );
  }

  const data = program.data;
  const completed = num(data.completedCount);
  const total = num(data.sessionCount);
  // Newest first (ISO timestamps sort lexically).
  const programCerts = (certs.data ?? [])
    .filter((c) => c.programId === data.programId)
    .sort((a, b) => b.issuedAt.localeCompare(a.issuedAt));
  const next = data.sessions.find((s) => s.id === data.nextSessionId);

  return (
    <div className="flex flex-col lg:grid lg:grid-cols-[minmax(0,1fr)_280px] lg:grid-rows-[auto_1fr] lg:gap-x-8">
      {/* DOM order = phone reading order (next-up first); from lg the grid moves it into the right column. */}
      {next && (
        <div className="mt-6 lg:col-start-2 lg:row-start-1 lg:mt-8">
          <NextUpPanel
            title={next.title}
            href={nextHref(next, data.programId)}
            type={next.type}
            partsDone={data.nextSessionPartsDone == null ? null : num(data.nextSessionPartsDone)}
            partCount={data.nextSessionPartCount == null ? null : num(data.nextSessionPartCount)}
          />
        </div>
      )}
      <div className="lg:col-start-1 lg:row-span-2 lg:row-start-1">
        <ProgressHeader program={data} />
        <Chapter title="Daftar isi">
          <Toc label="Daftar sesi">
            {data.sessions.map((s, i) => (
              <TocItem
                key={s.id}
                n={i + 1}
                title={s.title}
                kind={SESSION_TYPE_LABEL[s.type]}
                state={tocState(s, data.nextSessionId)}
                meta={tocMeta(s, data.nextSessionId)}
                href={s.state === "Locked" ? undefined : nextHref(s, data.programId)}
              />
            ))}
          </Toc>
        </Chapter>
      </div>
      <aside className="lg:col-start-2 lg:row-start-2">
        <SessionTestHistory token={token} programId={data.programId} continueHref={next ? nextHref(next, data.programId) : null} />
        <FinalResultCard certificate={programCerts[0]} sessionsRemaining={total - completed} status={certs.status} />
        <CertificateCards certificates={programCerts} />
      </aside>
    </div>
  );
}
