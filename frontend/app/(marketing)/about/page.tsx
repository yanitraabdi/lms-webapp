import type { Metadata } from "next";
import Link from "next/link";
import { PublicShell } from "@/components/PublicShell";
import { Chapter, Lead, PageTitle, RunningHead } from "@/components/ui";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Tentang",
  description:
    "INVERTA membantu peserta Indonesia bersiap menghadapi TOEFL lewat program terstruktur berbahasa Indonesia, tes bertahap, dan prediksi skor sebelum tes resmi.",
};

const stats = [
  { value: "3 bagian", label: "Format TOEFL ITP" },
  { value: "Sekali bayar", label: "Bukan langganan" },
  { value: "Instan", label: "Prediksi skor" },
];

export default function AboutPage() {
  return (
    <PublicShell>
      <RunningHead>Tentang kami</RunningHead>
      <PageTitle>AI praktis, dalam Bahasa Indonesia</PageTitle>
      <Lead>
        INVERTA membantu profesional Indonesia menerapkan AI dalam pekerjaan sehari-hari,
        tanpa jargon, langsung bisa dipraktikkan, dan diakhiri sertifikat yang bisa diverifikasi.
      </Lead>

      <Chapter title="Sekilas">
        <dl className="mt-4 grid gap-4 sm:grid-cols-3">
          {stats.map((s) => (
            <div key={s.label}>
              <dt className="text-sm text-ink-muted">{s.label}</dt>
              <dd className="text-2xl font-extrabold tracking-tight text-ink">{s.value}</dd>
            </div>
          ))}
        </dl>
      </Chapter>

      <Chapter title="Misi kami">
        <div className="mt-4 flex max-w-[62ch] flex-col gap-4 leading-relaxed text-ink-muted">
          <p>
            Banyak materi AI berkualitas hanya tersedia dalam bahasa Inggris dan ditujukan untuk teknisi. Kami percaya
            setiap profesional, dari pemasaran hingga operasional, berhak belajar AI dengan bahasa yang mereka pahami
            dan contoh yang relevan dengan pekerjaan mereka.
          </p>
          <p>
            Karena itu kami membangun jalur belajar yang jelas, bertahap dari Basic ke Advanced, dengan video singkat
            yang ringan untuk koneksi 4G dan dapat ditonton kapan saja.
          </p>
        </div>
        <Link href="/program/toefl-preparation" className="mt-6 inline-block rounded-sm bg-primary px-5 py-3 text-sm font-bold text-primary-ink hover:bg-primary-hover focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus">
          Lihat program TOEFL
        </Link>
      </Chapter>
    </PublicShell>
  );
}
