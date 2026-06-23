"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { Button, Spinner, ErrorState } from "@/components/ui";
import {
  downloadCertificatePdf,
  fmtDate,
  getDashboard,
  getMyCertificates,
  num,
  type Certificate,
} from "@/lib/learning";

function AwardIcon({ className }: { className?: string }) {
  return (
    <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.9} className={className} aria-hidden>
      <circle cx="12" cy="8" r="5" />
      <path d="M9 13l-1 8 4-3 4 3-1-8" />
    </svg>
  );
}

export default function CertificatesPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login?next=/app/certificates");
  }, [status, router]);

  const certs = useQuery({
    queryKey: ["certificates"],
    queryFn: () => getMyCertificates(accessToken!),
    enabled: status === "authenticated" && !!accessToken,
  });
  const dash = useQuery({
    queryKey: ["dashboard"],
    queryFn: () => getDashboard(accessToken!),
    enabled: status === "authenticated" && !!accessToken,
  });

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }

  const inProgress = (dash.data?.levels ?? []).filter((l) => l.unlocked && !l.certified && num(l.publishedCount) > 0);

  return (
    <div className="min-h-screen bg-bg">
      <AppHeader />
      <div className="mx-auto max-w-3xl px-6 pb-16 pt-7">
        <div className="mb-5 flex flex-col gap-1.5">
          <h1 className="text-[26px] font-extrabold tracking-tight">Sertifikat saya</h1>
          <p className="text-sm text-ink-muted">Sertifikat bersifat permanen dan dapat diverifikasi publik.</p>
        </div>

        {certs.isPending ? (
          <div className="flex min-h-[160px] items-center justify-center"><Spinner size={24} /></div>
        ) : certs.isError ? (
          <ErrorState title="Gagal memuat sertifikat" action={<Button variant="neutral" size="sm" onClick={() => certs.refetch()}>Muat ulang</Button>} />
        ) : (
          <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
            {certs.data.length === 0 && inProgress.length === 0 && (
              <p className="px-6 py-10 text-center text-sm text-ink-muted">
                Belum ada sertifikat. Selesaikan semua modul di sebuah level untuk mendapatkannya.
              </p>
            )}

            {certs.data.map((c) => <EarnedRow key={c.id} cert={c} token={accessToken} />)}

            {inProgress.map((l) => (
              <div key={l.levelId} className="flex items-center gap-4 border-t border-surface-2 px-[22px] py-[18px] first:border-0">
                <span className="flex h-[52px] w-[52px] shrink-0 items-center justify-center rounded-xl bg-surface-2 text-ink-subtle"><AwardIcon /></span>
                <div className="min-w-0 flex-1">
                  <h3 className="text-[15px] font-bold text-ink-muted">Level {l.name}</h3>
                  <div className="mt-2 h-1.5 max-w-[240px] overflow-hidden rounded-full bg-surface-2">
                    <div className="h-full bg-primary" style={{ width: `${num(l.percent)}%` }} />
                  </div>
                  <div className="mt-1.5 text-xs text-ink-subtle">Selesaikan level untuk membuka sertifikat · {num(l.percent)}%</div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function EarnedRow({ cert, token }: { cert: Certificate; token: string }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function download() {
    setError(null);
    setBusy(true);
    try {
      await downloadCertificatePdf(token, cert.id, `sertifikat-${cert.verificationCode}.pdf`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal mengunduh.");
    } finally {
      setBusy(false);
    }
  }

  function shareLinkedIn() {
    const url = `${window.location.origin}/verify/${cert.verificationCode}`;
    window.open(`https://www.linkedin.com/sharing/share-offsite/?url=${encodeURIComponent(url)}`, "_blank", "noopener");
  }

  return (
    <div className="flex flex-wrap items-center gap-4 border-t border-surface-2 px-[22px] py-[18px] first:border-0">
      <span className="flex h-[52px] w-[52px] shrink-0 items-center justify-center rounded-xl bg-accent-soft text-accent"><AwardIcon /></span>
      <div className="min-w-0 flex-1">
        <h3 className="text-[15px] font-bold">Level {cert.levelName}</h3>
        <div className="mt-0.5 text-[12.5px] text-ink-muted">
          Terbit {fmtDate(cert.issuedAt)} · Kode <span className="font-mono font-semibold text-ink">{cert.verificationCode}</span>
        </div>
        {error && <div className="mt-1 text-xs text-danger">{error}</div>}
      </div>
      <div className="flex shrink-0 gap-2">
        <Link
          href={`/verify/${cert.verificationCode}`}
          className="rounded-md border border-border bg-surface px-3 py-2 text-[12.5px] font-bold text-primary hover:bg-surface-2"
        >
          Verifikasi
        </Link>
        <Button size="sm" variant="neutral" loading={busy} onClick={download}>Unduh PDF</Button>
        <button
          type="button"
          onClick={shareLinkedIn}
          className="rounded-md bg-[#0A66C2] px-3 py-2 text-[12.5px] font-bold text-white hover:opacity-90"
        >
          Bagikan
        </button>
      </div>
    </div>
  );
}
