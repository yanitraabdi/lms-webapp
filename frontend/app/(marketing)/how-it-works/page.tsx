import type { Metadata } from "next";
import Link from "next/link";
import { PublicShell } from "@/components/PublicShell";
import { Lead, PageTitle, RunningHead } from "@/components/ui";
import { HOW_IT_WORKS_STEPS } from "@/components/program/howItWorksSteps";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Cara Kerja",
  description: "Empat langkah: daftar akun, bayar sekali, belajar berurutan, lalu ikuti tes akhir dan terima prediksi skor TOEFL.",
};

export default function HowItWorksPage() {
  return (
    <PublicShell>
      <RunningHead>Cara kerja</RunningHead>
      <PageTitle>Mulai belajar dalam 4 langkah</PageTitle>
      <Lead>
        Dari pendaftaran hingga sertifikat prediksi, alurnya berurutan dan setiap langkah harus diselesaikan sebelum lanjut.
      </Lead>

      <ol className="mt-8 flex list-decimal flex-col gap-4 pl-6 marker:font-extrabold marker:text-primary">
        {HOW_IT_WORKS_STEPS.map((s) => (
          <li key={s.n} className="pl-1">
            <span className="block font-bold text-ink">{s.title}</span>
            <span className="block max-w-[60ch] text-ink-muted">{s.body}</span>
          </li>
        ))}
      </ol>
      <Link href="/register" className="mt-6 inline-block rounded-sm bg-primary px-5 py-3 text-sm font-bold text-primary-ink hover:bg-primary-hover focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus">
        Daftar gratis
      </Link>
    </PublicShell>
  );
}
