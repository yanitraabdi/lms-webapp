import type { Metadata } from "next";
import Link from "next/link";
import { AlertTriangleIcon, CheckCircleIcon } from "@/components/ui";
import { verifyCertificate, num, type CertificateVerification } from "@/lib/sessions";
import { fmtDateTime } from "@/lib/programs";

// Public certificate verification — SSR per request (live validity).
export const dynamic = "force-dynamic";

export async function generateMetadata({ params }: { params: Promise<{ code: string }> }): Promise<Metadata> {
  const { code } = await params;
  return {
    title: `Verifikasi sertifikat ${code} — INVERTA`,
    description: "Verifikasi keaslian sertifikat prediksi TOEFL INVERTA.",
  };
}

export default async function VerifyPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  let result: CertificateVerification | null = null;
  try {
    result = await verifyCertificate(code);
  } catch {
    result = null;
  }
  const valid = result?.valid ?? false;
  const sections = Object.entries(result?.sectionScores ?? {});

  return (
    <div className="flex min-h-screen flex-col bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex h-[58px] max-w-3xl items-center gap-2.5 px-6">
          <span className="flex h-[26px] w-[26px] items-center justify-center rounded-md bg-primary text-[13px] font-extrabold text-primary-ink">I</span>
          <Link href="/" className="text-[13px] font-extrabold hover:underline">INVERTA</Link>
        </div>
      </header>

      <main className="flex flex-1 items-start justify-center px-6 py-12">
        <div className="w-full max-w-md">
          {valid && result ? (
            <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
              <div className="flex flex-col items-center gap-4 px-6 py-8 text-center">
                <span className="flex h-[60px] w-[60px] items-center justify-center rounded-full bg-success-soft text-success">
                  <CheckCircleIcon size={30} strokeWidth={2.2} />
                </span>
                <div className="flex flex-col gap-1.5">
                  <span className="text-[13px] font-bold text-success">Sertifikat terverifikasi ✓</span>
                  <span className="text-[22px] font-extrabold tracking-tight">{result.recipientName}</span>
                  <span className="text-[14px] text-ink-muted">{result.programName}</span>
                </div>

                {result.totalScore != null && (
                  <div className="mt-1 flex flex-col items-center">
                    <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">
                      Skor prediksi TOEFL
                    </span>
                    <span className="text-[44px] font-extrabold leading-none tracking-tight text-primary">
                      {num(result.totalScore)}
                    </span>
                    {result.predictedBand && (
                      <span className="mt-1 text-[13px] text-ink-muted">{result.predictedBand}</span>
                    )}
                  </div>
                )}
              </div>

              {sections.length > 0 && (
                <div className="border-t border-border px-6 py-4">
                  <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">
                    Rincian per bagian
                  </span>
                  <ul className="mt-2 flex flex-col gap-1.5">
                    {sections.map(([name, value]) => (
                      <li key={name} className="flex items-center justify-between text-[13px]">
                        <span className="text-ink-muted">{name}</span>
                        <span className="font-bold text-ink">{num(value)}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              <div className="border-t border-border px-6 py-4 text-[12.5px] text-ink-muted">
                <div className="flex justify-between gap-3">
                  <span>Kode verifikasi</span>
                  <span className="font-bold text-ink">{result.code}</span>
                </div>
                {result.issuedAt && (
                  <div className="mt-1.5 flex justify-between gap-3">
                    <span>Tanggal terbit</span>
                    <span className="text-ink">{fmtDateTime(result.issuedAt)}</span>
                  </div>
                )}
              </div>
            </div>
          ) : (
            <div className="flex flex-col items-center gap-4 rounded-lg border border-border bg-surface px-6 py-10 text-center shadow-sm">
              <span className="flex h-[60px] w-[60px] items-center justify-center rounded-full bg-danger-soft text-danger">
                <AlertTriangleIcon size={30} strokeWidth={2.2} />
              </span>
              <div className="flex flex-col gap-1.5">
                <span className="text-[15px] font-extrabold">Sertifikat tidak ditemukan</span>
                <span className="text-[13.5px] text-ink-muted">
                  Kode <strong className="text-ink">{code}</strong> tidak cocok dengan sertifikat mana pun.
                  Periksa kembali penulisannya.
                </span>
              </div>
            </div>
          )}

          {/* Mandatory on every certificate surface (GR-14). */}
          <div className="mt-5 rounded-base border border-border bg-surface-2 px-5 py-4">
            <p className="text-[12px] leading-relaxed text-ink-muted">
              {result?.disclaimer ??
                "Skor ini adalah PREDIKSI yang diterbitkan INVERTA berdasarkan simulasi internal. Ini BUKAN skor TOEFL resmi dan tidak diterbitkan oleh ETS. TOEFL adalah merek dagang terdaftar milik Educational Testing Service (ETS)."}
            </p>
          </div>
        </div>
      </main>
    </div>
  );
}
