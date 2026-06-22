import Link from "next/link";

const columns: Array<{ heading: string; links: Array<{ label: string; href: string }> }> = [
  {
    heading: "Produk",
    links: [
      { label: "Katalog", href: "/catalog" },
      { label: "Harga", href: "/pricing" },
      { label: "Untuk Bisnis", href: "/for-business" },
    ],
  },
  {
    heading: "Perusahaan",
    links: [
      { label: "Tentang", href: "/about" },
      { label: "Cara Kerja", href: "/how-it-works" },
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
    <footer className="bg-ink pb-7 pt-[52px] text-white">
      <div className="mx-auto grid max-w-6xl gap-8 px-6 md:grid-cols-[1.4fr_1fr_1fr_1fr]">
        <div className="flex flex-col gap-3">
          <div className="flex items-center gap-2.5">
            <span className="flex h-[30px] w-[30px] items-center justify-center rounded-lg bg-primary text-[15px] font-extrabold text-primary-ink">
              A
            </span>
            <span className="text-[15px] font-extrabold">AI Productivity Academy</span>
          </div>
          <p className="max-w-[280px] text-[13.5px] leading-relaxed text-white/60">
            Belajar AI praktis dalam Bahasa Indonesia. Untuk profesional yang ingin lebih produktif.
          </p>
        </div>
        {columns.map((col) => (
          <div key={col.heading} className="flex flex-col gap-2.5">
            <span className="text-[12.5px] font-bold uppercase tracking-wide text-white/50">{col.heading}</span>
            {col.links.map((l) => (
              <Link key={l.href} href={l.href} className="text-[13.5px] text-white/80 transition-colors hover:text-white">
                {l.label}
              </Link>
            ))}
          </div>
        ))}
      </div>
      <div className="mx-auto mt-9 flex max-w-6xl flex-wrap justify-between gap-3 border-t border-white/10 px-6 pt-5 text-[12.5px] text-white/55">
        <span>© 2025 AI Productivity Academy. Hak cipta dilindungi.</span>
        <span>Dibuat di Indonesia 🇮🇩</span>
      </div>
    </footer>
  );
}
