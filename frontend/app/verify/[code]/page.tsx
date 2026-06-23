import type { Metadata } from "next";
import Link from "next/link";
import { AlertTriangleIcon, CheckCircleIcon } from "@/components/ui";
import { fmtDate, verifyCertificate, type CertificateVerify } from "@/lib/learning";

// Public certificate verification — SSR per request (live validity).
export const dynamic = "force-dynamic";

export async function generateMetadata({ params }: { params: Promise<{ code: string }> }): Promise<Metadata> {
  const { code } = await params;
  return {
    title: `Verifikasi sertifikat ${code} — AI Productivity Academy`,
    description: "Verifikasi keaslian sertifikat AI Productivity Academy.",
  };
}

export default async function VerifyPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  let result: CertificateVerify | null = null;
  try {
    result = await verifyCertificate(code);
  } catch {
    result = null;
  }
  const valid = result?.valid ?? false;

  return (
    <div className="flex min-h-screen flex-col bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex h-[58px] max-w-3xl items-center gap-2.5 px-6">
          <span className="flex h-[26px] w-[26px] items-center justify-center rounded-md bg-primary text-[13px] font-extrabold text-primary-ink">A</span>
          <span className="text-[13px] font-extrabold">AI Productivity Academy</span>
        </div>
      </header>

      <main className="flex flex-1 items-center justify-center px-6 py-12">
        <div className="w-full max-w-md">
          {valid && result ? (
            <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
              <div className="flex flex-col items-center gap-4 px-6 py-8 text-center">
                <span className="flex h-[60px] w-[60px] items-center justify-center rounded-full bg-success-soft text-success">
                  <CheckCircleIcon size={30} strokeWidth={2.2} />
                </span>
                <div className="flex flex-col gap-1.5">
                  <span className="text-[13px] font-bold text-success">Sertifikat terverifikasi ✓</span>
                  <h1 className="text-[22px] font-extrabold tracking-tight">{result.recipientName ?? "Peserta"}</h1>
                  <span className="text-sm text-ink-muted">Level {result.levelName}</span>
                </div>
                <dl className="w-full rounded-lg bg-surface-2 p-4 text-[13px]">
                  <Row label="Kode verifikasi" value={<span className="font-mono font-bold">{result.verificationCode}</span>} />
                  <Row label="Tanggal terbit" value={result.issuedAt ? fmtDate(result.issuedAt) : "—"} />
                  <Row label="Status" value={<span className="font-bold text-success">Aktif &amp; sah</span>} />
                  <Row label="Penerbit" value={result.issuer} last />
                </dl>
                <p className="text-[11.5px] leading-relaxed text-ink-subtle">
                  Halaman ini dapat dibuka siapa saja untuk memastikan keaslian sertifikat.
                </p>
              </div>
            </div>
          ) : (
            <div className="flex items-start gap-3 rounded-lg border border-border border-l-[3px] border-l-danger bg-surface p-5 shadow-sm">
              <AlertTriangleIcon size={22} className="shrink-0 text-danger" />
              <div>
                <div className="mb-1 text-sm font-bold">Kode tidak ditemukan</div>
                <p className="text-[13px] leading-relaxed text-ink-muted">
                  Kode <span className="font-mono">{code}</span> tidak cocok dengan sertifikat mana pun. Periksa kembali kode Anda.
                </p>
              </div>
            </div>
          )}

          <div className="mt-5 text-center">
            <Link href="/" className="text-[13px] font-bold text-primary hover:underline">← Kembali ke beranda</Link>
          </div>
        </div>
      </main>
    </div>
  );
}

function Row({ label, value, last }: { label: string; value: React.ReactNode; last?: boolean }) {
  return (
    <div className={"flex items-center justify-between " + (last ? "" : "mb-2.5")}>
      <dt className="text-ink-muted">{label}</dt>
      <dd className="text-ink">{value}</dd>
    </div>
  );
}
