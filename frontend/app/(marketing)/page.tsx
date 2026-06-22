import type { Metadata } from "next";
import Link from "next/link";
import { PublicNav } from "@/components/PublicNav";
import { SiteFooter } from "@/components/SiteFooter";
import { Badge, CheckIcon, ChevronRightIcon, LockIcon, PlayIcon, StarIcon } from "@/components/ui";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "AI Productivity Academy — Belajar AI dalam Bahasa Indonesia",
  description:
    "Jalur belajar AI yang jelas untuk profesional Indonesia. Video singkat, praktik langsung, dan sertifikat yang bisa diverifikasi.",
};

const features = [
  {
    title: "Jalur belajar yang jelas",
    body: "Dari Basic ke Advanced, langkah demi langkah. Anda selalu tahu apa yang harus dipelajari berikutnya.",
    cls: "bg-primary-soft text-primary",
    icon: (
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
        <path d="M3 6h18M3 12h18M3 18h12" />
      </svg>
    ),
  },
  {
    title: "Sertifikat yang diakui",
    body: "Selesaikan satu level, dapatkan sertifikat dengan kode verifikasi. Tunjukkan ke atasan atau bagikan di LinkedIn.",
    cls: "bg-accent-soft text-accent",
    icon: (
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
        <circle cx="12" cy="8" r="6" />
        <path d="M9 14l-1 7 4-2.5L16 21l-1-7" />
      </svg>
    ),
  },
  {
    title: "Ringan untuk koneksi 4G",
    body: "Pilih kualitas video sendiri dan lihat estimasi pemakaian data. Belajar nyaman dari HP, kapan pun.",
    cls: "bg-success-soft text-success",
    icon: (
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
        <rect x="2" y="6" width="14" height="12" rx="2" />
        <path d="M16 10l6-3v10l-6-3" />
      </svg>
    ),
  },
];

const steps = [
  { n: "1", title: "Daftar & coba gratis", body: "Buat akun dengan email atau Google. Tonton modul pratinjau gratis tanpa perlu bayar." },
  { n: "2", title: "Pilih paket & belajar", body: "Berlangganan sesuai level Anda. Paket lebih tinggi otomatis membuka semua level di bawahnya." },
  { n: "3", title: "Selesaikan & bersertifikat", body: "Tonton hingga selesai, progres tersimpan otomatis. Tuntaskan level untuk dapat sertifikat." },
];

const featured = [
  {
    kind: "preview" as const,
    levelLabel: "Basic",
    levelCls: "bg-surface-2 text-ink-muted",
    title: "Apa itu AI? Penjelasan tanpa istilah rumit",
    rating: "4,9",
    learners: "8,4rb peserta",
    duration: "6 mnt",
    cta: "Tonton gratis",
    ctaCls: "border-[1.5px] border-primary bg-surface text-primary",
  },
  {
    kind: "locked" as const,
    levelLabel: "Beginner",
    levelCls: "bg-[#DCFCE7] text-[#166534]",
    title: "Otomatiskan email harian dengan ChatGPT",
    rating: "4,8",
    learners: "5,2rb peserta",
    duration: "10 mnt",
    cta: "Tingkatkan ke Beginner",
    ctaCls: "bg-[#DCFCE7] text-[#166534]",
  },
  {
    kind: "locked" as const,
    levelLabel: "Intermediate",
    levelCls: "bg-[#DBEAFE] text-[#1D4ED8]",
    title: "Membangun workflow tim dengan Claude Projects",
    rating: "4,7",
    learners: "2,9rb peserta",
    duration: "12 mnt",
    cta: "Tingkatkan ke Intermediate",
    ctaCls: "bg-[#DBEAFE] text-[#1D4ED8]",
  },
];

const previewPlans = [
  { label: "Gratis", badgeCls: "text-[#57534E] bg-[#F0EFEC] border-[#E2E0DB]", price: "Rp 0", per: "", desc: "Modul pratinjau gratis & akses lihat katalog penuh.", cta: "Daftar gratis", ctaCls: "border-[1.5px] border-primary bg-surface text-primary", popular: false },
  { label: "Beginner", badgeCls: "text-[#166534] bg-[#DCFCE7] border-[#BBF7D0]", price: "Rp 149rb", per: "/bln", desc: "Semua modul Level Basic + sertifikat Basic.", cta: "Pilih Beginner", ctaCls: "bg-primary text-primary-ink", popular: false },
  { label: "Intermediate", badgeCls: "text-[#1D4ED8] bg-[#DBEAFE] border-[#BFDBFE]", price: "Rp 249rb", per: "/bln", desc: "Basic + Intermediate, 2 sertifikat, catatan berwaktu.", cta: "Pilih Intermediate", ctaCls: "bg-primary text-primary-ink", popular: true },
  { label: "Advanced", badgeCls: "text-[#6D28D9] bg-[#EDE4FF] border-[#DDD0FB]", price: "Rp 349rb", per: "/bln", desc: "Seluruh level, 3 sertifikat lengkap, akses paling awal.", cta: "Pilih Advanced", ctaCls: "bg-[#6D28D9] text-white", popular: false },
];

export default function HomePage() {
  return (
    <>
      <PublicNav />

      {/* HERO */}
      <section className="bg-[linear-gradient(180deg,#FFFFFF_0%,var(--ds-color-bg)_100%)] px-6 py-14">
        <div className="mx-auto grid max-w-6xl items-center gap-12 md:grid-cols-[1.05fr_.95fr]">
          <div className="flex flex-col gap-5">
            <span className="inline-flex w-fit items-center gap-2 rounded-full bg-primary-soft px-3.5 py-1.5 text-[12.5px] font-bold text-primary">
              <span className="h-1.5 w-1.5 rounded-full bg-primary" />
              Belajar AI dalam Bahasa Indonesia
            </span>
            <h1 className="text-[46px] font-extrabold leading-[1.08] tracking-tight">
              Kuasai AI untuk kerja Anda — <span className="text-primary">tanpa istilah rumit</span>
            </h1>
            <p className="max-w-lg text-[17px] leading-relaxed text-ink-muted">
              Jalur belajar yang jelas dan terstruktur untuk profesional Indonesia. Tonton video singkat, praktik
              langsung, dan dapatkan sertifikat yang bisa Anda tunjukkan ke atasan dan di LinkedIn.
            </p>
            <div className="flex flex-wrap items-center gap-3">
              <Link
                href="/register"
                className="rounded-base bg-primary px-6 py-3.5 text-[15px] font-bold text-primary-ink shadow-sm transition-colors hover:bg-primary-hover"
              >
                Mulai gratis hari ini
              </Link>
              <Link
                href="/catalog"
                className="inline-flex items-center gap-2 rounded-base border-[1.5px] border-border bg-surface px-5 py-3 text-[15px] font-bold text-ink"
              >
                <PlayIcon size={18} className="text-primary" />
                Tonton pratinjau
              </Link>
            </div>
            <div className="mt-1 flex flex-wrap items-center gap-[18px]">
              <div className="flex items-center gap-1.5">
                <span className="inline-flex items-center gap-0.5 text-[15px] font-extrabold text-accent">
                  <StarIcon size={15} />
                  4,8
                </span>
                <span className="text-[13px] text-ink-muted">dari 3.200+ ulasan</span>
              </div>
              <span className="h-[18px] w-px bg-border" />
              <span className="text-[13px] text-ink-muted">
                <strong className="text-ink">12.000+</strong> profesional sudah belajar
              </span>
            </div>
          </div>

          {/* Hero visual: course card + floating cert badge */}
          <div className="relative">
            <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-lg">
              <div className="relative flex aspect-[16/10] items-center justify-center bg-[linear-gradient(135deg,#0050E6,#2A6BFF)]">
                <span className="flex h-[62px] w-[62px] items-center justify-center rounded-full bg-white/95 text-primary shadow-lg">
                  <PlayIcon size={26} />
                </span>
                <span className="absolute left-3 top-3 rounded-full bg-[#DCFCE7] px-2.5 py-1 text-[11px] font-bold text-[#166534]">
                  Pratinjau gratis
                </span>
                <span className="absolute bottom-3 right-3 rounded-base bg-black/55 px-2 py-1 text-xs font-semibold text-white">
                  6 mnt
                </span>
              </div>
              <div className="flex flex-col gap-2.5 p-[18px]">
                <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">Level Basic · Modul 1</span>
                <h3 className="text-lg font-bold">Apa itu AI? Penjelasan tanpa istilah rumit</h3>
                <div className="h-[7px] overflow-hidden rounded-full bg-surface-2">
                  <div className="h-full w-[38%] rounded-full bg-primary" />
                </div>
                <span className="text-[12.5px] text-ink-muted">Lanjutkan menonton · 38%</span>
              </div>
            </div>
            <div className="absolute -bottom-4 -right-4 flex items-center gap-3 rounded-base border border-border bg-surface px-4 py-3 shadow-lg">
              <span className="flex h-9 w-9 items-center justify-center rounded-[9px] bg-accent-soft text-accent">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
                  <circle cx="12" cy="8" r="5" />
                  <path d="M9 13l-1 8 4-3 4 3-1-8" />
                </svg>
              </span>
              <div>
                <div className="text-[13px] font-bold">Sertifikat resmi</div>
                <div className="text-[11.5px] text-ink-muted">Bisa dibagikan ke LinkedIn</div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* TRUST STRIP */}
      <section className="border-y border-border bg-surface px-6 py-[22px]">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-center gap-x-10 gap-y-3.5">
          <span className="text-[12.5px] font-semibold text-ink-subtle">Dipercaya tim di</span>
          {["Telkom", "Bank Mandiri", "Gojek", "Bukalapak", "Pertamina"].map((c) => (
            <span key={c} className="text-[17px] font-extrabold tracking-tight text-[#B7C2D2]">
              {c}
            </span>
          ))}
        </div>
      </section>

      {/* FEATURES */}
      <section className="px-6 py-16">
        <div className="mx-auto flex max-w-6xl flex-col gap-10">
          <div className="mx-auto flex max-w-2xl flex-col gap-3 text-center">
            <span className="text-xs font-bold uppercase tracking-widest text-primary">Dirancang untuk pemula</span>
            <h2 className="text-[32px] font-extrabold tracking-tight">Dibuat khusus untuk yang baru memulai dengan AI</h2>
            <p className="text-base leading-relaxed text-ink-muted">
              Tidak perlu latar belakang teknis. Setiap modul singkat, praktis, dan langsung bisa Anda terapkan di
              pekerjaan sehari-hari.
            </p>
          </div>
          <div className="grid gap-5 md:grid-cols-3">
            {features.map((f) => (
              <div key={f.title} className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-6 shadow-sm">
                <span className={`inline-flex h-12 w-12 items-center justify-center rounded-xl ${f.cls}`}>{f.icon}</span>
                <h3 className="text-lg font-bold">{f.title}</h3>
                <p className="text-[14.5px] leading-relaxed text-ink-muted">{f.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* HOW IT WORKS */}
      <section className="border-y border-border bg-surface px-6 py-16">
        <div className="mx-auto flex max-w-6xl flex-col gap-10">
          <div className="flex max-w-xl flex-col gap-2.5">
            <span className="text-xs font-bold uppercase tracking-widest text-primary">Cara kerja</span>
            <h2 className="text-[32px] font-extrabold tracking-tight">Mulai dalam 3 langkah</h2>
          </div>
          <div className="grid gap-5 md:grid-cols-3">
            {steps.map((s) => (
              <div key={s.n} className="flex flex-col gap-3">
                <span className="flex h-10 w-10 items-center justify-center rounded-full bg-primary text-[17px] font-extrabold text-primary-ink">
                  {s.n}
                </span>
                <h3 className="text-lg font-bold">{s.title}</h3>
                <p className="text-[14.5px] leading-relaxed text-ink-muted">{s.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* FEATURED MODULES */}
      <section className="px-6 py-16">
        <div className="mx-auto flex max-w-6xl flex-col gap-8">
          <div className="flex flex-wrap items-end justify-between gap-4">
            <div className="flex flex-col gap-2.5">
              <span className="text-xs font-bold uppercase tracking-widest text-primary">Cuplikan kurikulum</span>
              <h2 className="text-[32px] font-extrabold tracking-tight">Lihat apa yang akan Anda pelajari</h2>
            </div>
            <Link href="/catalog" className="inline-flex items-center gap-1.5 text-sm font-bold text-primary">
              Jelajahi katalog
              <ChevronRightIcon size={16} strokeWidth={2.2} />
            </Link>
          </div>
          <div className="grid gap-[18px] md:grid-cols-3">
            {featured.map((m) => (
              <article key={m.title} className="flex flex-col overflow-hidden rounded-base border border-border bg-surface shadow-sm">
                <div className={`relative flex aspect-video items-center justify-center ${m.kind === "preview" ? "bg-[linear-gradient(135deg,#0050E6,#2A6BFF)]" : "bg-surface-2"}`}>
                  {m.kind === "preview" ? (
                    <span className="flex h-[44px] w-[44px] items-center justify-center rounded-full bg-white/95 text-primary">
                      <PlayIcon size={18} />
                    </span>
                  ) : (
                    <span className="flex h-[42px] w-[42px] items-center justify-center rounded-full border border-border bg-surface text-ink-subtle">
                      <LockIcon size={18} />
                    </span>
                  )}
                  {m.kind === "preview" && (
                    <span className="absolute left-2 top-2 rounded-sm bg-[#DCFCE7] px-2 py-1 text-[10.5px] font-bold text-[#166534]">
                      Pratinjau gratis
                    </span>
                  )}
                  <span className={`absolute bottom-2 right-2 rounded-sm px-1.5 py-0.5 text-[11px] font-semibold ${m.kind === "preview" ? "bg-black/55 text-white" : "bg-surface text-ink-muted"}`}>
                    {m.duration}
                  </span>
                </div>
                <div className="flex flex-col gap-2.5 p-[15px]">
                  <span className={`w-fit rounded-sm px-2 py-1 text-[10.5px] font-bold ${m.levelCls}`}>{m.levelLabel}</span>
                  <h4 className="text-[15.5px] font-bold leading-snug">{m.title}</h4>
                  <div className="flex items-center gap-1.5 text-[11.5px] text-ink-muted">
                    <span className="inline-flex items-center gap-1 font-bold text-accent">
                      <StarIcon size={12} />
                      {m.rating}
                    </span>
                    <span>·</span>
                    <span>{m.learners}</span>
                  </div>
                  <Link
                    href={m.kind === "preview" ? "/register" : "/pricing"}
                    className={`mt-0.5 inline-flex items-center justify-center gap-2 rounded-sm px-3 py-2.5 text-center text-[13px] font-bold ${m.ctaCls}`}
                  >
                    {m.kind === "locked" && <LockIcon size={13} />}
                    {m.cta}
                  </Link>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      {/* CERTIFICATE ANGLE */}
      <section className="px-6 pb-16">
        <div className="mx-auto max-w-6xl">
          <div className="grid items-center gap-10 overflow-hidden rounded-[20px] bg-[linear-gradient(135deg,#11253F,#1E3A5F)] p-12 text-white md:grid-cols-[1.1fr_.9fr]">
            <div className="flex flex-col gap-4">
              <span className="w-fit rounded-full bg-[rgba(255,184,28,.16)] px-3.5 py-1.5 text-[12.5px] font-bold text-[#FFD874]">
                Bukti kompetensi
              </span>
              <h2 className="text-[30px] font-extrabold leading-tight tracking-tight">Sertifikat yang bisa Anda banggakan</h2>
              <p className="text-[15.5px] leading-relaxed text-white/80">
                Setiap sertifikat punya kode verifikasi unik dan halaman publik yang bisa dibuka siapa saja. Tambahkan ke
                profil LinkedIn dengan satu klik — atasan dan rekruter bisa memverifikasi keasliannya langsung.
              </p>
              <div className="mt-1 flex flex-wrap gap-5">
                {["Kode verifikasi unik", "Bisa dibagikan ke LinkedIn"].map((t) => (
                  <div key={t} className="flex items-center gap-2.5">
                    <CheckIcon size={20} strokeWidth={2.4} className="text-[#5EE6A8]" />
                    <span className="text-sm font-semibold">{t}</span>
                  </div>
                ))}
              </div>
            </div>
            <div className="rounded-base bg-surface p-[22px] text-ink shadow-lg">
              <div className="flex flex-col gap-3 rounded-base border-2 border-accent-strong p-[22px] text-center">
                <span className="text-[11px] font-bold uppercase tracking-widest text-ink-subtle">Sertifikat Kelulusan</span>
                <span className="mx-auto flex h-[46px] w-[46px] items-center justify-center rounded-full bg-accent-soft text-accent">
                  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
                    <circle cx="12" cy="8" r="5" />
                    <path d="M9 13l-1 8 4-3 4 3-1-8" />
                  </svg>
                </span>
                <div className="text-[13px] text-ink-muted">Diberikan kepada</div>
                <div className="text-xl font-extrabold tracking-tight">Budi Santoso</div>
                <div className="text-[13.5px] text-ink-muted">Level Basic — AI untuk Produktivitas</div>
                <div className="mt-1 flex items-center justify-between border-t border-dashed border-border pt-3">
                  <span className="text-[11px] text-ink-subtle">
                    Kode: <strong className="font-mono text-ink">AIPA-7K2D9X</strong>
                  </span>
                  <span className="h-[34px] w-[34px] rounded bg-[linear-gradient(135deg,#11253F_25%,#fff_25%_50%,#11253F_50%_75%,#fff_75%)] [background-size:8px_8px]" />
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* TESTIMONIAL */}
      <section className="border-y border-border bg-surface px-6 py-14">
        <div className="mx-auto flex max-w-3xl flex-col gap-5 text-center">
          <div className="flex justify-center gap-1 text-accent">
            {Array.from({ length: 5 }).map((_, i) => (
              <StarIcon key={i} size={22} />
            ))}
          </div>
          <p className="text-[23px] font-semibold leading-relaxed tracking-tight">
            &ldquo;Saya tidak punya latar belakang teknis. Dalam dua minggu saya sudah pakai AI untuk bikin laporan dan
            balas email. Sertifikatnya saya tunjukkan ke atasan — langsung diapresiasi.&rdquo;
          </p>
          <div className="flex items-center justify-center gap-3">
            <span className="flex h-[46px] w-[46px] items-center justify-center rounded-full bg-primary-soft font-extrabold text-primary">
              BS
            </span>
            <div className="text-left">
              <div className="text-[14.5px] font-bold">Budi Santoso</div>
              <div className="text-[13px] text-ink-muted">Staf Operasional, Jakarta</div>
            </div>
          </div>
        </div>
      </section>

      {/* PRICING PREVIEW */}
      <section className="border-y border-border bg-surface px-6 py-16">
        <div className="mx-auto flex max-w-6xl flex-col gap-9">
          <div className="mx-auto flex max-w-xl flex-col gap-3 text-center">
            <span className="text-xs font-bold uppercase tracking-widest text-primary">Harga</span>
            <h2 className="text-[32px] font-extrabold tracking-tight">Satu langganan, semua yang Anda butuh</h2>
            <p className="text-base leading-relaxed text-ink-muted">
              Paket bertingkat dan <strong className="text-ink">kumulatif</strong> — paket lebih tinggi otomatis membuka
              semua level di bawahnya. Mulai gratis, tingkatkan kapan saja.
            </p>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {previewPlans.map((p) => (
              <article
                key={p.label}
                className={`relative flex flex-col gap-3.5 rounded-lg bg-surface p-[22px] ${p.popular ? "border-2 border-primary shadow-lg" : "border border-border shadow-sm"}`}
              >
                {p.popular && <Badge promo="popular" className="absolute -top-3 left-1/2 -translate-x-1/2 shadow-sm" />}
                <span className={`w-fit rounded-full border px-3 py-1 text-xs font-bold ${p.badgeCls}`}>{p.label}</span>
                <div className="flex items-baseline gap-1">
                  <span className="text-[28px] font-extrabold tracking-tight">{p.price}</span>
                  {p.per && <span className="text-[12.5px] text-ink-muted">{p.per}</span>}
                </div>
                <p className="flex-1 text-[13px] leading-snug text-ink-muted">{p.desc}</p>
                <Link href="/register" className={`rounded-sm px-3 py-2.5 text-center text-[13.5px] font-bold ${p.ctaCls}`}>
                  {p.cta}
                </Link>
              </article>
            ))}
          </div>
          <div className="flex flex-wrap items-center justify-center gap-3.5">
            <Link href="/pricing" className="inline-flex items-center gap-1.5 text-[14.5px] font-bold text-primary">
              Lihat perbandingan lengkap
              <ChevronRightIcon size={16} strokeWidth={2.2} />
            </Link>
            <span className="text-[13px] text-ink-subtle">·</span>
            <span className="text-[13px] text-ink-muted">Hemat 2 bulan dengan tagihan tahunan</span>
          </div>
        </div>
      </section>

      <SiteFooter />
    </>
  );
}
