import type { Metadata } from "next";
import Link from "next/link";
import { PublicShell } from "@/components/PublicShell";
import { Band } from "@/components/program/Band";
import { Footnote, Lead, PageTitle } from "@/components/ui";

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
    <PublicShell bare>
      <Band eyebrow="Tentang kami" pad="py-14 md:py-20">
        <PageTitle>Persiapan TOEFL yang terarah, dalam Bahasa Indonesia</PageTitle>
        <Lead>
          INVERTA membantu pelajar dan profesional Indonesia bersiap menghadapi TOEFL ITP: materi
          dijelaskan dalam Bahasa Indonesia, dipelajari berurutan, dan ditutup dengan simulasi tes
          yang memberi prediksi skor.
        </Lead>

        <dl className="mt-10 grid gap-4 sm:grid-cols-3">
          {stats.map((s) => (
            <div key={s.label}>
              <dt className="text-sm text-ink-muted">{s.label}</dt>
              <dd className="text-2xl font-extrabold tracking-tight text-ink">{s.value}</dd>
            </div>
          ))}
        </dl>
      </Band>

      <Band title="Misi kami" tint>
        <div className="mt-4 flex max-w-[62ch] flex-col gap-4 leading-relaxed text-ink-muted">
          <p>
            Banyak orang Indonesia membutuhkan skor TOEFL untuk beasiswa, studi lanjut, atau pekerjaan, tetapi
            sebagian besar materi persiapannya berbahasa Inggris dan tidak memberi tahu harus mulai dari mana. Kami
            ingin persiapan itu terasa jelas: tahu apa yang dipelajari hari ini, dan tahu kapan Anda sudah siap.
          </p>
          <p>
            Karena itu program kami disusun berurutan. Setiap sesi berisi video materi dan tes singkat yang wajib
            lulus sebelum sesi berikutnya terbuka, ditambah kelas live bersama pengajar. Di akhir, simulasi tes
            berformat TOEFL ITP (Listening, Structure, dan Reading, berwaktu) memberi Anda prediksi skor dan
            sertifikat yang dapat diverifikasi.
          </p>
        </div>
        {/* Prediction disclaimer: GR-14, non-negotiable wherever a score is mentioned. */}
        <Footnote>
          <strong className="text-ink">Catatan penting.</strong> Skor yang dihasilkan adalah{" "}
          <strong className="text-ink">prediksi INVERTA</strong> berdasarkan simulasi internal,{" "}
          <strong className="text-ink">bukan skor TOEFL resmi</strong> dan tidak diterbitkan oleh ETS. TOEFL
          adalah merek dagang terdaftar milik ETS.
        </Footnote>
        <Link href="/program/toefl-preparation" className="mt-6 inline-block rounded-sm bg-primary px-5 py-3 text-sm font-bold text-primary-ink hover:bg-primary-hover focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus">
          Lihat program TOEFL
        </Link>
      </Band>
    </PublicShell>
  );
}
