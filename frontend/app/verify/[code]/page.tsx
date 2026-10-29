import type { Metadata } from "next";
import Link from "next/link";
import { Footnote, SHEET_CLASSES, PageTitle, RunningHead } from "@/components/ui";
import { verifyCertificate, num, type CertificateVerification } from "@/lib/sessions";
import { fmtDateTime } from "@/lib/programs";
import { Logo } from "@/components/Logo";

// Public certificate verification: SSR per request (live validity).
export const dynamic = "force-dynamic";

export async function generateMetadata({ params }: { params: Promise<{ code: string }> }): Promise<Metadata> {
  const { code } = await params;
  return {
    title: `Verifikasi sertifikat ${code}`,
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
  const sections = Object.entries(result?.scaledScores ?? {});

  return (
    <div className="flex min-h-screen flex-col bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex h-[58px] max-w-3xl items-center gap-2.5 px-6">
          <Link href="/">
            <Logo />
          </Link>
        </div>
      </header>

      <main className="flex-1 px-3 py-4 sm:px-7 sm:py-8">
        <div className={`mx-auto w-full max-w-[760px] ${SHEET_CLASSES}`}>
          <RunningHead>Verifikasi sertifikat</RunningHead>
          {valid && result ? (
            <>
              <PageTitle>{result.recipientName}</PageTitle>
              <p className="mt-2 text-ink-muted">{result.programName}</p>
              <p className="mt-1 text-[13.5px] font-bold text-success">Sertifikat terverifikasi</p>

              <table className="data-table mt-6">
                <thead>
                  <tr>
                    <th>Rincian</th>
                    <th className="num">Hasil</th>
                  </tr>
                </thead>
                <tbody>
                  {result.totalScore != null && (
                    <tr>
                      <td>Skor prediksi TOEFL</td>
                      <td className="num text-[20px] font-extrabold text-primary">
                        {num(result.totalScore)}
                        {result.predictedBand && (
                          <span className="ml-2 text-[13px] font-normal text-ink-muted">{result.predictedBand}</span>
                        )}
                      </td>
                    </tr>
                  )}
                  {sections.map(([name, value]) => (
                    <tr key={name}>
                      <td>{name}</td>
                      <td className="num font-bold">{num(value)}</td>
                    </tr>
                  ))}
                  <tr>
                    <td>Kode verifikasi</td>
                    <td className="num font-bold">{result.code}</td>
                  </tr>
                  {result.issuedAt && (
                    <tr>
                      <td>Tanggal terbit</td>
                      <td className="num">{fmtDateTime(result.issuedAt)}</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </>
          ) : (
            <>
              <PageTitle>Sertifikat tidak ditemukan</PageTitle>
              <p className="mt-3 max-w-[60ch] text-ink-muted">
                Kode <strong className="text-ink">{code}</strong> tidak cocok dengan sertifikat mana pun.
                Periksa kembali penulisannya.
              </p>
            </>
          )}

          {/* Mandatory on every certificate surface (GR-14). */}
          <Footnote>
            {result?.disclaimer ??
              "Skor ini adalah PREDIKSI yang diterbitkan INVERTA berdasarkan simulasi internal. Ini BUKAN skor TOEFL resmi dan tidak diterbitkan oleh ETS. TOEFL adalah merek dagang terdaftar milik Educational Testing Service (ETS)."}
          </Footnote>
        </div>
      </main>
    </div>
  );
}
