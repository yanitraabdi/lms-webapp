"use client";

import { useEffect } from "react";
import dynamic from "next/dynamic";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { OnboardingFlow } from "@/components/onboarding/OnboardingFlow";
import { Button, Spinner, ErrorState, EmptyState, SheetPage, RunningHead, PageTitle, Chapter, Toc, TocItem } from "@/components/ui";
import { ProgressHeader, nextHref } from "@/components/dashboard/ProgressHeader";
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

  const firstName = (user?.name ?? "").split(" ")[0] || "kembali";
  const active = (enrollments.data ?? []).filter(
    (e) => e.status === "Active" || e.status === "Completed");

  return (
    <div className="min-h-screen bg-bg">
      <AppHeader />
      <OnboardingFlow token={accessToken} />
      <SheetPage>
        {active[0] && <RunningHead>{active[0].programName}</RunningHead>}
        <PageTitle>{`Selamat datang kembali, ${firstName}`}</PageTitle>

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
      </SheetPage>
    </div>
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
    <>
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
      <FinalResultCard certificate={programCerts[0]} sessionsRemaining={total - completed} status={certs.status} />
      <SessionTestHistory token={token} programId={data.programId} continueHref={next ? nextHref(next, data.programId) : null} />
      <CertificateCards certificates={programCerts} />
    </>
  );
}
