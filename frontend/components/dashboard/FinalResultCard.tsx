"use client";
import dynamic from "next/dynamic";
import Link from "next/link";
import { num } from "@/lib/programs";
import type { ProgramCertificate } from "@/lib/sessions";

const FinalResultChart = dynamic(() => import("./FinalResultChart"), {
  ssr: false,
  loading: () => <div className="h-[160px]" />,
});

const SECTIONS = ["Listening", "Structure", "Reading"];

/** Block 2: the final exam's predicted score, or what the final exam is when it is not done yet. */
export function FinalResultCard({
  certificate, sessionsRemaining, status = "success",
}: { certificate?: ProgramCertificate; sessionsRemaining: number; status?: "pending" | "error" | "success" }) {
  if (status === "pending") {
    return <section className="min-h-[200px] rounded-lg border border-border bg-surface shadow-sm" aria-busy="true" />;
  }
  if (status === "error") {
    return (
      <section className="rounded-lg border border-border bg-surface p-5 shadow-sm">
        <p className="text-[13px] text-ink-muted">Hasil tes akhir tidak dapat dimuat.</p>
      </section>
    );
  }
  if (!certificate) {
    return (
      <section className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-5 shadow-sm">
        <h2 className="text-lg font-extrabold">Tes akhir</h2>
        <p className="text-[13px] leading-relaxed text-ink-muted">
          Simulasi TOEFL ITP (Listening, Structure, Reading) dengan durasi 115 menit. Terbuka setelah semua sesi selesai.
        </p>
        {sessionsRemaining > 0 && (
          <p className="text-[13px] font-bold text-ink">
            {sessionsRemaining > 1 ? `Sisa ${sessionsRemaining} sesi lagi.` : "Tinggal tes akhir."}
          </p>
        )}
        {sessionsRemaining === 0 && (
          <p className="text-[13px] font-bold text-ink">
            Tes akhir sudah dikerjakan. Sertifikat sedang diproses — jika belum muncul, hubungi kami lewat{" "}
            <Link href="/contact" className="text-primary hover:underline">halaman kontak</Link>.
          </p>
        )}
      </section>
    );
  }

  const scores: Record<string, number> = {};
  for (const [k, v] of Object.entries(certificate.scaledScores ?? {})) scores[k] = num(v);

  return (
    <section className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-5 shadow-sm">
      <h2 className="text-lg font-extrabold">Hasil tes akhir</h2>
      <div className="flex flex-col gap-1">
        <div className="flex flex-wrap items-baseline gap-2">
          {certificate.totalScore != null && (
            <span className="text-[40px] font-extrabold leading-none text-primary">{num(certificate.totalScore)}</span>
          )}
          {certificate.predictedBand && (
            <span className="text-[13px] font-bold text-ink-muted">{certificate.predictedBand}</span>
          )}
        </div>
        <p className="text-[12px] text-ink-muted">Skor prediksi INVERTA, bukan skor resmi TOEFL dari ETS.</p>
      </div>

      <FinalResultChart scores={scores} />
      <ul className="sr-only">
        {SECTIONS.filter((s) => scores[s] != null).map((s) => <li key={s}>{s}: {scores[s]}</li>)}
      </ul>

      <Link href="/app/certificates" className="self-start text-[13px] font-bold text-primary hover:underline">
        Lihat sertifikat →
      </Link>
    </section>
  );
}
