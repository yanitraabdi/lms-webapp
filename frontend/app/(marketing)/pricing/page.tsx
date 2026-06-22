import type { Metadata } from "next";
import { PublicNav } from "@/components/PublicNav";
import { SiteFooter } from "@/components/SiteFooter";
import { PricingTiers } from "@/components/marketing/PricingTiers";
import { InfoIcon } from "@/components/ui";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Harga — AI Productivity Academy",
  description:
    "Paket berlangganan kumulatif: Gratis, Beginner, Intermediate, Advanced. Mulai gratis, tingkatkan kapan saja.",
};

const comparison: Array<{ feature: string; cells: string[] }> = [
  { feature: "Modul Level Basic", cells: ["Pratinjau", "✓", "✓", "✓"] },
  { feature: "Modul Level Intermediate", cells: ["—", "—", "✓", "✓"] },
  { feature: "Modul Level Advanced", cells: ["—", "—", "—", "✓"] },
  { feature: "Sertifikat kelulusan", cells: ["—", "1", "2", "3"] },
  { feature: "Materi unduhan", cells: ["—", "✓", "✓", "✓"] },
  { feature: "Catatan & bookmark berwaktu", cells: ["—", "✓", "✓", "✓"] },
];

const faqs: Array<{ q: string; a: string }> = [
  {
    q: "Apakah bisa upgrade di tengah jalan?",
    a: "Bisa. Upgrade langsung aktif dan biaya dihitung prorata. Downgrade berlaku pada periode tagihan berikutnya.",
  },
  {
    q: "Apakah perlu kartu kredit untuk coba gratis?",
    a: "Tidak. Daftar gratis dan tonton modul pratinjau tanpa memasukkan metode pembayaran.",
  },
  {
    q: "Apakah sertifikat saya tetap berlaku jika berhenti berlangganan?",
    a: "Ya. Sertifikat yang sudah terbit bersifat permanen dan tetap dapat diverifikasi lewat halaman publik.",
  },
];

export default function PricingPage() {
  return (
    <>
      <PublicNav />

      <main>
        {/* Header */}
        <section className="px-6 pb-8 pt-14">
          <div className="mx-auto flex max-w-3xl flex-col items-center gap-4 text-center">
            <span className="text-xs font-bold uppercase tracking-widest text-primary">Harga</span>
            <h1 className="text-4xl font-extrabold tracking-tight">Pilih paket yang sesuai langkah Anda</h1>
            <p className="max-w-2xl text-[16.5px] leading-relaxed text-ink-muted">
              Paket bersifat <strong className="text-ink">kumulatif</strong> — setiap paket membuka semua level di
              bawahnya. Mulai gratis, tingkatkan kapan saja.
            </p>
          </div>
        </section>

        {/* Tiers (interactive monthly/annual) */}
        <section className="px-6 pb-14">
          <div className="mx-auto max-w-6xl">
            <PricingTiers />

            <div className="mt-6 flex items-center gap-3 rounded-base border border-[#C9DBFF] bg-primary-soft px-5 py-4">
              <InfoIcon size={22} className="shrink-0 text-primary" />
              <p className="text-sm leading-relaxed text-ink">
                <strong>Kumulatif:</strong> memilih paket lebih tinggi otomatis membuka semua level di bawahnya. Sudah
                berlangganan? Tingkatkan kapan saja — perubahan langsung aktif dan tagihan disesuaikan secara prorata.
              </p>
            </div>
          </div>
        </section>

        {/* Comparison table */}
        <section className="px-6 pb-16">
          <div className="mx-auto flex max-w-6xl flex-col gap-5">
            <h2 className="text-center text-2xl font-extrabold tracking-tight">Perbandingan lengkap</h2>
            <div className="overflow-x-auto rounded-lg border border-border bg-surface">
              <table className="w-full min-w-[680px] border-collapse">
                <thead>
                  <tr className="bg-surface-2">
                    <th className="px-[18px] py-3.5 text-left text-[13px] font-bold text-ink-muted">Fitur</th>
                    <th className="px-3 py-3.5 text-[13px] font-bold">Gratis</th>
                    <th className="px-3 py-3.5 text-[13px] font-bold">Beginner</th>
                    <th className="px-3 py-3.5 text-[13px] font-bold text-primary">Intermediate</th>
                    <th className="px-3 py-3.5 text-[13px] font-bold">Advanced</th>
                  </tr>
                </thead>
                <tbody className="text-[13.5px]">
                  {comparison.map((row, i) => (
                    <tr key={row.feature} className={"border-t border-border" + (i % 2 === 1 ? " bg-[#FBFCFE]" : "")}>
                      <td className="px-[18px] py-3.5 text-ink-muted">{row.feature}</td>
                      {row.cells.map((c, ci) => (
                        <td key={ci} className={"px-3 py-3.5 text-center " + (c === "—" ? "text-ink-subtle" : "text-ink")}>
                          {c}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </section>

        {/* FAQ */}
        <section className="border-t border-border bg-surface px-6 py-14">
          <div className="mx-auto flex max-w-3xl flex-col gap-3.5">
            <h2 className="mb-2 text-center text-2xl font-extrabold tracking-tight">Pertanyaan umum</h2>
            {faqs.map((item) => (
              <div key={item.q} className="rounded-base border border-border bg-surface px-5 py-[18px]">
                <div className="mb-1.5 text-[15px] font-bold">{item.q}</div>
                <p className="text-sm leading-relaxed text-ink-muted">{item.a}</p>
              </div>
            ))}
          </div>
        </section>
      </main>

      <SiteFooter />
    </>
  );
}
