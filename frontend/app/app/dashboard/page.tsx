"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { OnboardingFlow } from "@/components/onboarding/OnboardingFlow";
import { Button, Spinner, ErrorState, EmptyState } from "@/components/ui";
import { ProgressHeader } from "@/components/dashboard/ProgressHeader";
import { FinalResultCard } from "@/components/dashboard/FinalResultCard";
import { listMyEnrollments, getStudentProgram, num, type Enrollment } from "@/lib/programs";
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
      <div className="mx-auto max-w-5xl px-4 pb-16 pt-7 sm:px-6">

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
          <ProgramDashboard token={accessToken} enrollment={active[0]} firstName={firstName} />
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

/** Blocks 1–2 for one programme. ponytail: one programme exists; per-programme sections when a second one ships. */
function ProgramDashboard({ token, enrollment, firstName }: { token: string; enrollment: Enrollment; firstName: string }) {
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
      <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
        <h2 className="text-lg font-extrabold">{enrollment.programName}</h2>
        <p className="mt-1 text-[13px] text-ink-muted">Program tidak dapat dimuat saat ini.</p>
      </div>
    );
  }

  const data = program.data;
  const completed = num(data.completedCount);
  const total = num(data.sessionCount);
  // Newest certificate for this programme (ISO timestamps sort lexically).
  const cert = (certs.data ?? [])
    .filter((c) => c.programId === data.programId)
    .sort((a, b) => b.issuedAt.localeCompare(a.issuedAt))[0];

  return (
    <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
      <div className="md:col-span-2"><ProgressHeader program={data} firstName={firstName} /></div>
      <FinalResultCard certificate={cert} sessionsRemaining={total - completed} status={certs.status} />
      {/* Task 4: <SessionTestHistory … /> */}
      {/* Task 4: <div className="md:col-span-2"><CertificateCards … /></div> */}
    </div>
  );
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
