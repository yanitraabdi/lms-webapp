"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { Button, Spinner, ErrorState, EmptyState } from "@/components/ui";
import { listMyCertificates, downloadCertificate, num, type ProgramCertificate } from "@/lib/sessions";
import { fmtDateTime } from "@/lib/programs";

export default function CertificatesPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login?next=/app/certificates");
  }, [status, router]);

  const q = useQuery({
    queryKey: ["my-certificates"],
    queryFn: () => listMyCertificates(accessToken!),
    enabled: status === "authenticated" && !!accessToken,
  });

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }

  return (
    <div className="min-h-screen bg-bg">
      <AppHeader />
      <div className="mx-auto max-w-3xl px-6 pb-16 pt-7">
        <h1 className="mb-1 text-[24px] font-extrabold tracking-tight">Sertifikat</h1>
        <p className="mb-6 text-sm text-ink-muted">
          Sertifikat prediksi TOEFL Anda. Setiap sertifikat dapat diverifikasi secara publik.
        </p>

        {q.isPending ? (
          <div className="flex min-h-[180px] items-center justify-center"><Spinner size={24} /></div>
        ) : q.isError ? (
          <ErrorState title="Gagal memuat sertifikat"
            action={<Button variant="neutral" size="sm" onClick={() => q.refetch()}>Muat ulang</Button>} />
        ) : q.data.length === 0 ? (
          <EmptyState
            title="Belum ada sertifikat"
            message="Sertifikat terbit otomatis setelah Anda menyelesaikan tes akhir."
            action={<Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Ke dasbor</Link>}
          />
        ) : (
          <div className="flex flex-col gap-4">
            {q.data.map((c) => <CertificateCard key={c.id} cert={c} token={accessToken} />)}
          </div>
        )}

        <div className="mt-8 rounded-base border border-border bg-surface-2 px-5 py-4">
          <p className="text-[12px] leading-relaxed text-ink-muted">
            <strong className="text-ink">Penting.</strong> Skor pada sertifikat ini adalah{" "}
            <strong className="text-ink">prediksi INVERTA</strong> berdasarkan simulasi internal —{" "}
            <strong className="text-ink">bukan skor TOEFL resmi</strong> dan tidak diterbitkan oleh ETS.
            TOEFL adalah merek dagang terdaftar milik ETS.
          </p>
        </div>
      </div>
    </div>
  );
}

function CertificateCard({ cert, token }: { cert: ProgramCertificate; token: string }) {
  const [busy, setBusy] = useState(false);
  const sections = Object.entries(cert.sectionScores ?? {});

  async function download() {
    setBusy(true);
    try {
      await downloadCertificate(token, cert.id, `sertifikat-${cert.verificationCode}.pdf`);
    } catch {
      /* surfaced by the browser */
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">Sertifikat prediksi</span>
          <h2 className="text-lg font-extrabold">{cert.programName}</h2>
          <span className="text-[12.5px] text-ink-muted">Terbit {fmtDateTime(cert.issuedAt)}</span>
        </div>
        {cert.totalScore != null && (
          <div className="text-right">
            <div className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">Skor prediksi</div>
            <div className="text-[34px] font-extrabold leading-none text-primary">{num(cert.totalScore)}</div>
            {cert.predictedBand && <div className="text-[12px] text-ink-muted">{cert.predictedBand}</div>}
          </div>
        )}
      </div>

      {sections.length > 0 && (
        <ul className="mt-4 flex flex-wrap gap-x-6 gap-y-1.5 border-t border-border pt-3.5">
          {sections.map(([name, value]) => (
            <li key={name} className="text-[12.5px] text-ink-muted">
              {name}: <strong className="text-ink">{num(value)}</strong>
            </li>
          ))}
        </ul>
      )}

      <div className="mt-4 flex flex-wrap items-center gap-3">
        <Button size="sm" onClick={download} loading={busy}>Unduh PDF</Button>
        <Link
          href={`/verify/${cert.verificationCode}`}
          className="text-[12.5px] font-bold text-primary hover:underline"
        >
          Halaman verifikasi →
        </Link>
        <span className="text-[11.5px] text-ink-subtle">Kode: {cert.verificationCode}</span>
      </div>
    </div>
  );
}
