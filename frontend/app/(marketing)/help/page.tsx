import type { Metadata } from "next";
import Link from "next/link";
import { PublicShell } from "@/components/PublicShell";
import { getFaq } from "@/lib/content";
import { Chapter, Lead, PageTitle, RunningHead } from "@/components/ui";
import { FaqAccordion } from "@/components/FaqAccordion";

// ISR: FAQ is admin-editable later; revalidate keeps it fresh.
export const revalidate = 3600;

export const metadata: Metadata = {
  title: "Bantuan & FAQ",
  description: "Pertanyaan yang sering diajukan tentang akun, pendaftaran program, sertifikat, dan pembayaran.",
};

const categories = [
  { title: "Memulai", body: "Daftar, verifikasi email, dan mulai dari sesi pertama." },
  { title: "Pendaftaran & pembayaran", body: "Sekali bayar untuk seluruh program, tanpa perpanjangan otomatis." },
  { title: "Tes & sertifikat", body: "Tes per sesi, simulasi TOEFL ITP, dan cara memverifikasi sertifikat Anda." },
];

export default async function HelpPage() {
  const faq = await getFaq({ revalidate: 3600 }).catch(() => []);

  return (
    <PublicShell>
      <RunningHead>Bantuan</RunningHead>
      <PageTitle>Pusat bantuan &amp; FAQ</PageTitle>
      <Lead>
        Temukan jawaban cepat. Tidak menemukan yang Anda cari? <Link href="/contact" className="font-bold text-primary hover:underline">Hubungi kami</Link>.
      </Lead>

      <dl className="mt-8 flex flex-col gap-3">
          {categories.map((c) => (
            <div key={c.title}>
              <dt className="font-bold text-ink">{c.title}</dt>
              <dd className="max-w-[60ch] text-ink-muted">{c.body}</dd>
            </div>
          ))}
        </dl>

      <Chapter title="Pertanyaan umum">
        <div className="mt-4">
          <FaqAccordion items={faq} />
        </div>
      </Chapter>
    </PublicShell>
  );
}
