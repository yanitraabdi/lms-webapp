import type { Metadata } from "next";
import Link from "next/link";
import { PublicShell } from "@/components/PublicShell";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Tentang — AI Productivity Academy",
  description:
    "Kami membantu profesional Indonesia menguasai AI praktis lewat video singkat berbahasa Indonesia, latihan langsung, dan sertifikat yang dapat diverifikasi.",
};

const stats = [
  { value: "12.000+", label: "Peserta belajar" },
  { value: "55+", label: "Modul video" },
  { value: "4,8★", label: "Rata-rata penilaian" },
];

export default function AboutPage() {
  return (
    <PublicShell>
      <section className="px-6 pb-10 pt-14">
        <div className="mx-auto flex max-w-3xl flex-col items-center gap-4 text-center">
          <span className="text-xs font-bold uppercase tracking-widest text-primary">Tentang kami</span>
          <h1 className="text-4xl font-extrabold tracking-tight">AI praktis, dalam Bahasa Indonesia</h1>
          <p className="max-w-2xl text-[16.5px] leading-relaxed text-ink-muted">
            AI Productivity Academy membantu profesional Indonesia menerapkan AI dalam pekerjaan sehari-hari —
            tanpa jargon, langsung bisa dipraktikkan, dan diakhiri sertifikat yang bisa diverifikasi.
          </p>
        </div>
      </section>

      <section className="px-6 pb-14">
        <div className="mx-auto grid max-w-4xl gap-4 sm:grid-cols-3">
          {stats.map((s) => (
            <div key={s.label} className="flex flex-col items-center gap-1 rounded-lg border border-border bg-surface p-6 text-center shadow-sm">
              <span className="text-3xl font-extrabold tracking-tight text-primary">{s.value}</span>
              <span className="text-sm text-ink-muted">{s.label}</span>
            </div>
          ))}
        </div>
      </section>

      <section className="border-t border-border bg-surface px-6 py-14">
        <div className="mx-auto flex max-w-3xl flex-col gap-4">
          <h2 className="text-2xl font-extrabold tracking-tight">Misi kami</h2>
          <p className="text-[15px] leading-relaxed text-ink-muted">
            Banyak materi AI berkualitas hanya tersedia dalam bahasa Inggris dan ditujukan untuk teknisi. Kami percaya
            setiap profesional — dari pemasaran hingga operasional — berhak belajar AI dengan bahasa yang mereka pahami
            dan contoh yang relevan dengan pekerjaan mereka.
          </p>
          <p className="text-[15px] leading-relaxed text-ink-muted">
            Karena itu kami membangun jalur belajar yang jelas, bertahap dari Basic ke Advanced, dengan video singkat
            yang ringan untuk koneksi 4G dan dapat ditonton kapan saja.
          </p>
          <Link href="/pricing" className="mt-2 w-fit rounded-sm bg-primary px-5 py-3 text-sm font-bold text-primary-ink hover:bg-primary-hover">
            Lihat paket belajar
          </Link>
        </div>
      </section>
    </PublicShell>
  );
}
