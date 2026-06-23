import type { Metadata } from "next";
import Link from "next/link";
import { PublicShell } from "@/components/PublicShell";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Cara Kerja — AI Productivity Academy",
  description: "Empat langkah sederhana: daftar gratis, coba pratinjau, berlangganan, dan lulus dengan sertifikat.",
};

const steps = [
  { n: 1, title: "Daftar gratis", body: "Buat akun dalam satu menit dan verifikasi email Anda. Tanpa kartu kredit." },
  { n: 2, title: "Coba pratinjau", body: "Tonton modul pratinjau gratis untuk merasakan gaya belajar kami." },
  { n: 3, title: "Berlangganan", body: "Pilih paket yang sesuai. Akses kumulatif — paket lebih tinggi membuka semua level di bawahnya." },
  { n: 4, title: "Lulus & dapat sertifikat", body: "Selesaikan semua modul di sebuah level untuk memperoleh sertifikat yang dapat diverifikasi." },
];

export default function HowItWorksPage() {
  return (
    <PublicShell>
      <section className="px-6 pb-8 pt-14">
        <div className="mx-auto flex max-w-3xl flex-col items-center gap-4 text-center">
          <span className="text-xs font-bold uppercase tracking-widest text-primary">Cara kerja</span>
          <h1 className="text-4xl font-extrabold tracking-tight">Mulai belajar dalam 4 langkah</h1>
          <p className="max-w-2xl text-[16.5px] leading-relaxed text-ink-muted">
            Dari pendaftaran hingga sertifikat — alurnya jelas dan bisa Anda jalani dengan kecepatan sendiri.
          </p>
        </div>
      </section>

      <section className="px-6 pb-14">
        <div className="mx-auto grid max-w-4xl gap-4 sm:grid-cols-2">
          {steps.map((s) => (
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
