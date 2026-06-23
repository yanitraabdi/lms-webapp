import type { Metadata } from "next";
import Link from "next/link";
import { PublicShell } from "@/components/PublicShell";
import { CheckIcon } from "@/components/ui";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Untuk Bisnis — AI Productivity Academy",
  description: "Tingkatkan keterampilan AI seluruh tim Anda dengan kursi tim, dasbor progres, dan penagihan terpusat.",
};

const features = [
  "Kursi tim dengan satu langganan",
  "Dasbor progres untuk seluruh anggota",
  "Penagihan terpusat & faktur perusahaan",
  "Sertifikat tim yang dapat diverifikasi",
];

export default function ForBusinessPage() {
  return (
    <PublicShell>
      <section className="px-6 py-14">
        <div className="mx-auto grid max-w-5xl items-center gap-10 md:grid-cols-2">
          <div className="flex flex-col gap-4">
            <span className="text-xs font-bold uppercase tracking-widest text-primary">Untuk bisnis</span>
            <h1 className="text-4xl font-extrabold tracking-tight">Naikkan kemampuan AI seluruh tim</h1>
            <p className="text-[15.5px] leading-relaxed text-ink-muted">
              Bawa pelatihan AI praktis ke tim Anda dengan kurikulum berbahasa Indonesia, progres yang terukur, dan
              administrasi yang sederhana.
            </p>
            <div className="mt-2 flex flex-wrap gap-3">
              <Link href="/contact" className="rounded-sm bg-primary px-5 py-3 text-sm font-bold text-primary-ink hover:bg-primary-hover">
                Hubungi sales
              </Link>
              <Link href="/pricing" className="rounded-sm border border-border bg-surface px-5 py-3 text-sm font-bold text-ink hover:bg-surface-2">
                Lihat paket
              </Link>
            </div>
          </div>

          <div className="flex flex-col gap-4 rounded-lg bg-ink p-7 text-white shadow-lg">
            <h2 className="text-lg font-extrabold">Yang termasuk untuk tim</h2>
            <ul className="flex flex-col gap-3">
              {features.map((f) => (
                <li key={f} className="flex items-start gap-2.5 text-sm text-white/85">
                  <CheckIcon size={18} strokeWidth={2.4} className="mt-0.5 shrink-0 text-success" />
                  {f}
                </li>
              ))}
            </ul>
            <p className="mt-2 text-[12.5px] text-white/55">Manajemen kursi B2B penuh hadir pada fase berikutnya.</p>
          </div>
        </div>
      </section>
    </PublicShell>
  );
}
