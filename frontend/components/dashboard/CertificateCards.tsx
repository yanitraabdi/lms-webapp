import Link from "next/link";
import { num } from "@/lib/programs";
import type { ProgramCertificate } from "@/lib/sessions";

/** Block 4: one card per issued certificate. Omitted entirely when there are none. */
export function CertificateCards({ certificates }: { certificates: ProgramCertificate[] }) {
  if (certificates.length === 0) return null;

  return (
    <section className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-5 shadow-sm">
      <div className="flex flex-col gap-1">
        <h2 className="text-lg font-extrabold">Sertifikat</h2>
        <p className="text-[12px] text-ink-muted">Skor prediksi INVERTA, bukan skor resmi TOEFL dari ETS.</p>
      </div>
      <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {certificates.map((c) => (
          <li key={c.id} className="flex flex-col gap-2 rounded-base border border-border p-4">
            <span className="text-[14px] font-bold text-ink">{c.programName}</span>
            {c.totalScore != null && (
              <span className="flex items-baseline gap-1.5">
                <span className="text-[24px] font-extrabold leading-none text-primary">{num(c.totalScore)}</span>
                <span className="text-[12px] text-ink-muted">skor prediksi</span>
              </span>
            )}
            <span className="text-[12px] text-ink-muted">
              {new Date(c.issuedAt).toLocaleDateString("id-ID", { dateStyle: "long" })}
            </span>
            <span className="break-all font-mono text-[12px] text-ink">{c.verificationCode}</span>
            <span className="flex gap-4">
              <Link href={`/verify/${c.verificationCode}`} className="text-[13px] font-bold text-primary hover:underline">
                Verifikasi →
              </Link>
              <Link href="/app/certificates" className="text-[13px] font-bold text-primary hover:underline">
                Unduh →
              </Link>
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
