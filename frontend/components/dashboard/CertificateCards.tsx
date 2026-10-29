import Link from "next/link";
import { Chapter, Footnote, RunningHead } from "@/components/ui";
import { num } from "@/lib/programs";
import type { ProgramCertificate } from "@/lib/sessions";

const LINK = "text-[14px] font-bold text-primary underline underline-offset-2";

/** One object box per issued certificate. Omitted entirely when there are none. */
export function CertificateCards({ certificates }: { certificates: ProgramCertificate[] }) {
  if (certificates.length === 0) return null;

  return (
    <Chapter title="Sertifikat">
      <ul className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
        {certificates.map((c) => (
          <li key={c.id} className="flex flex-col gap-1.5 rounded-lg border border-border bg-surface p-4">
            <span className="font-bold text-ink">{c.programName}</span>
            <RunningHead>
              {c.totalScore != null && `Skor prediksi ${num(c.totalScore)}, `}
              {new Date(c.issuedAt).toLocaleDateString("id-ID", { dateStyle: "long" })}
            </RunningHead>
            <span className="break-all font-mono text-[13px] text-ink">{c.verificationCode}</span>
            <span className="mt-1 flex gap-4">
              <Link href="/app/certificates" className={LINK}>Unduh PDF</Link>
              <Link href={`/verify/${c.verificationCode}`} className={LINK}>Halaman verifikasi</Link>
            </span>
          </li>
        ))}
      </ul>
      <Footnote>Skor prediksi INVERTA, bukan skor resmi TOEFL dari ETS.</Footnote>
    </Chapter>
  );
}
