import type { Metadata } from "next";
import Link from "next/link";
import { PublicShell } from "@/components/PublicShell";
import { getFaq } from "@/lib/content";
import { FaqAccordion } from "./FaqAccordion";

// ISR — FAQ is admin-editable later; revalidate keeps it fresh.
export const revalidate = 3600;

export const metadata: Metadata = {
  title: "Bantuan & FAQ — AI Productivity Academy",
  description: "Pertanyaan yang sering diajukan tentang akun, langganan, sertifikat, dan pembayaran.",
};

const categories = [
  { title: "Memulai", body: "Daftar, verifikasi email, dan mulai dari modul pratinjau." },
  { title: "Langganan & tagihan", body: "Paket kumulatif, upgrade/downgrade, dan pembayaran." },
  { title: "Sertifikat", body: "Cara lulus level dan memverifikasi sertifikat Anda." },
];

export default async function HelpPage() {
  const faq = await getFaq({ revalidate: 3600 }).catch(() => []);

  return (
    <PublicShell>
      <section className="px-6 pb-8 pt-14">
        <div className="mx-auto flex max-w-3xl flex-col items-center gap-4 text-center">
          <span className="text-xs font-bold uppercase tracking-widest text-primary">Bantuan</span>
          <h1 className="text-4xl font-extrabold tracking-tight">Pusat bantuan &amp; FAQ</h1>
          <p className="max-w-2xl text-[16.5px] leading-relaxed text-ink-muted">
            Temukan jawaban cepat. Tidak menemukan yang Anda cari? <Link href="/contact" className="font-bold text-primary hover:underline">Hubungi kami</Link>.
          </p>
        </div>
      </section>

      <section className="px-6 pb-10">
        <div className="mx-auto grid max-w-4xl gap-4 sm:grid-cols-3">
          {categories.map((c) => (
            <div key={c.title} className="flex flex-col gap-1.5 rounded-lg border border-border bg-surface p-5 shadow-sm">
              <h3 className="text-[15px] font-extrabold">{c.title}</h3>
              <p className="text-[13px] leading-relaxed text-ink-muted">{c.body}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="px-6 pb-16">
        <div className="mx-auto max-w-3xl">
          <h2 className="mb-4 text-2xl font-extrabold tracking-tight">Pertanyaan umum</h2>
          <FaqAccordion items={faq} />
        </div>
      </section>
    </PublicShell>
  );
}
