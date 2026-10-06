import type { Metadata } from "next";
import Link from "next/link";
import { PublicShell } from "@/components/PublicShell";
import { HOW_IT_WORKS_STEPS } from "@/components/program/howItWorksSteps";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Cara Kerja",
  description: "Empat langkah: daftar akun, bayar sekali, belajar berurutan, lalu ikuti tes akhir dan terima prediksi skor TOEFL.",
};


export default function HowItWorksPage() {
  return (
    <PublicShell>
      <section className="px-6 pb-8 pt-14">
        <div className="mx-auto flex max-w-3xl flex-col items-center gap-4 text-center">
          <span className="text-xs font-bold uppercase tracking-widest text-primary">Cara kerja</span>
          <h1 className="text-4xl font-extrabold tracking-tight">Mulai belajar dalam 4 langkah</h1>
          <p className="max-w-2xl text-[16.5px] leading-relaxed text-ink-muted">
            Dari pendaftaran hingga sertifikat prediksi — alurnya berurutan dan setiap langkah harus diselesaikan sebelum lanjut.
          </p>
        </div>
      </section>

      <section className="px-6 pb-14">
        <div className="mx-auto grid max-w-4xl gap-4 sm:grid-cols-2">
          {HOW_IT_WORKS_STEPS.map((s) => (
            <div key={s.n} className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-6 shadow-sm">
              <span className="flex h-10 w-10 items-center justify-center rounded-full bg-primary-soft text-base font-extrabold text-primary">
                {s.n}
              </span>
              <h3 className="text-lg font-extrabold">{s.title}</h3>
              <p className="text-sm leading-relaxed text-ink-muted">{s.body}</p>
            </div>
          ))}
        </div>
        <div className="mx-auto mt-8 flex max-w-4xl justify-center">
          <Link href="/register" className="rounded-sm bg-primary px-6 py-3 text-sm font-bold text-primary-ink hover:bg-primary-hover">
            Daftar gratis
          </Link>
        </div>
      </section>
    </PublicShell>
  );
}
