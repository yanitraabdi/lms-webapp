import Link from "next/link";
import { Logo } from "@/components/Logo";

const columns: Array<{ heading: string; links: Array<{ label: string; href: string }> }> = [
  {
    heading: "Produk",
    links: [
      // Was Katalog / Harga / Untuk Bisnis (all three archived AI-Academy surfaces) linked from
      // every public page while a code comment elsewhere claimed they were unlinked.
      { label: "Program TOEFL", href: "/program/toefl-preparation" },
      { label: "Cara Kerja", href: "/how-it-works" },
    ],
  },
  {
    heading: "Perusahaan",
    links: [
      { label: "Tentang", href: "/about" },
      { label: "Pusat Bantuan", href: "/help" },
    ],
  },
  {
    heading: "Legal",
    links: [
      { label: "Ketentuan", href: "/legal/terms" },
      { label: "Privasi", href: "/legal/privacy" },
      { label: "Pengembalian Dana", href: "/legal/refund" },
    ],
  },
];

export function SiteFooter() {
  return (
    <footer className="bg-bg pb-7 pt-6 text-ink-muted">
      <div className="mx-auto grid max-w-[760px] gap-8 px-5 text-[13px] sm:px-7 md:grid-cols-[1.4fr_1fr_1fr_1fr] min-[820px]:px-0">
        <div className="flex flex-col gap-3">
          <Logo />
          <p className="max-w-[280px] leading-relaxed">
            Persiapan TOEFL terstruktur dalam Bahasa Indonesia. Untuk pelajar dan profesional yang
            mengejar skor terbaik.
          </p>
        </div>
        {columns.map((col) => (
          <div key={col.heading} className="flex flex-col gap-2">
            <span className="font-bold text-ink">{col.heading}</span>
            {col.links.map((l) => (
              <Link key={l.href} href={l.href} className="rounded-sm hover:text-ink hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus">
                {l.label}
              </Link>
            ))}
          </div>
        ))}
      </div>
      <div className="mx-auto mt-8 flex max-w-[760px] flex-wrap justify-between gap-3 border-t border-border px-5 pt-5 text-[13px] sm:px-7 min-[820px]:px-0">
        <span>© 2026 INVERTA. Hak cipta dilindungi.</span>
        <span>Dibuat di Indonesia</span>
      </div>
    </footer>
  );
}
