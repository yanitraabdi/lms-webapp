"use client";
import Link from "next/link";
import { Chapter, Footnote } from "@/components/ui";
import { num } from "@/lib/programs";
import type { ProgramCertificate } from "@/lib/sessions";

const SECTIONS = ["Listening", "Structure", "Reading"];

/** Final exam score report, or what the final exam is when it is not done yet. */
export function FinalResultCard({
  certificate, sessionsRemaining, status = "success",
}: { certificate?: ProgramCertificate; sessionsRemaining: number; status?: "pending" | "error" | "success" }) {
  if (status === "pending") {
    return <Chapter title="Hasil tes akhir"><div className="mt-4 min-h-[120px]" aria-busy="true" /></Chapter>;
  }
  if (status === "error") {
    return (
      <Chapter title="Hasil tes akhir">
        <p className="mt-3 text-ink-muted">Hasil tes akhir tidak dapat dimuat.</p>
      </Chapter>
    );
  }
  if (!certificate) {
    return (
      <Chapter title="Hasil tes akhir">
        {sessionsRemaining > 0 && <p className="mt-3 max-w-[60ch] text-ink-muted">Belum ada hasil tes akhir.</p>}
        <p className="mt-2 max-w-[60ch] text-ink-muted">
          Simulasi TOEFL ITP (Listening, Structure, Reading) dengan durasi 115 menit. Terbuka setelah semua sesi selesai.
        </p>
        {sessionsRemaining > 0 && (
          <p className="mt-2 font-bold text-ink">
            {sessionsRemaining > 1 ? `Sisa ${sessionsRemaining} sesi lagi.` : "Tinggal tes akhir."}
          </p>
        )}
        {sessionsRemaining === 0 && (
          <p className="mt-2 font-bold text-ink">
            Tes akhir sudah dikerjakan. Sertifikat sedang diproses, jika belum muncul, hubungi kami lewat{" "}
            <Link href="/contact" className="text-primary underline underline-offset-2">halaman kontak</Link>.
          </p>
        )}
      </Chapter>
    );
  }

  const scaled = certificate.scaledScores ?? {};
  const raw = certificate.sectionScores ?? {};
  const rows = SECTIONS.filter((s) => scaled[s] != null || raw[s] != null);

  return (
    <Chapter title="Hasil tes akhir">
      <div className="mt-4">
        {certificate.totalScore != null && (
          <>
            <p className="text-[60px] font-extrabold leading-none tabular-nums text-primary">{num(certificate.totalScore)}</p>
            <p className="mt-2 text-[13.5px] font-semibold text-ink-muted">
              Skor prediksi<sup>1</sup>
              {certificate.predictedBand && <span>{`, ${certificate.predictedBand}`}</span>}
            </p>
          </>
        )}
      </div>
      {rows.length > 0 && (
        <table className="data-table mt-5">
          <thead>
            <tr>
              <th scope="col">Bagian</th>
              <th scope="col" className="num">Benar</th>
              <th scope="col" className="num">Skor</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((s) => (
              <tr key={s}>
                <td className="font-bold">{s}</td>
                <td className="num">{raw[s] != null ? num(raw[s]) : ""}</td>
                <td className="num font-bold">{scaled[s] != null ? num(scaled[s]) : ""}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Footnote><sup>1</sup> Skor prediksi INVERTA, bukan skor TOEFL resmi dari ETS.</Footnote>
      <Link href="/app/certificates" className="mt-3 inline-block text-[14px] font-bold text-primary underline underline-offset-2">
        Lihat sertifikat
      </Link>
    </Chapter>
  );
}
