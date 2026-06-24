import type { Tier } from "@/components/ui";

export interface PlanFeature {
  text: string;
  included: boolean;
  /** "Semua di X, plus:" lead row — colored + bold. */
  lead?: boolean;
  /** emphasize an included headline feature (ink, semibold). */
  strong?: boolean;
  accent?: "primary" | "advanced";
}

export interface PlanTier {
  key: "free" | "beginner" | "intermediate" | "advanced";
  badge: Tier;
  name: string;
  tagline: string;
  /** monthly price (IDR); null = free. */
  monthly: number | null;
  /** effective monthly price when billed annually (IDR). */
  annualMonthly: number | null;
  cta: string;
  ctaVariant: "primary" | "secondary" | "advanced";
  popular?: boolean;
  features: PlanFeature[];
}

// PLACEHOLDER pricing/content. In M3 this is replaced by the .NET `/api/plans`
// endpoint (admin-editable, DB-backed); final pricing is still open (TSD §16.5).
export const PLANS: PlanTier[] = [
  {
    key: "free",
    badge: "free",
    name: "Pratinjau",
    tagline: "Coba dulu sebelum berlangganan.",
    monthly: null,
    annualMonthly: null,
    cta: "Daftar gratis",
    ctaVariant: "secondary",
    features: [
      { text: "Modul pratinjau gratis terpilih", included: true },
      { text: "Akses katalog penuh (lihat)", included: true },
      { text: "Tanpa sertifikat", included: false },
    ],
  },
  {
    key: "beginner",
    badge: "beginner",
    name: "Basic",
    tagline: "Fondasi AI untuk pekerjaan harian.",
    monthly: 149000,
    annualMonthly: 124000,
    cta: "Pilih Basic",
    ctaVariant: "primary",
    features: [
      { text: "Semua modul Level Basic", included: true, strong: true },
      { text: "Sertifikat Level Basic", included: true },
      { text: "Materi unduhan", included: true },
    ],
  },
  {
    key: "intermediate",
    badge: "intermediate",
    name: "Intermediate",
    tagline: "Workflow & tools AI tingkat lanjut.",
    monthly: 249000,
    annualMonthly: 207000,
    cta: "Pilih Intermediate",
    ctaVariant: "primary",
    popular: true,
    features: [
      { text: "Semua di Basic, plus:", included: true, lead: true, accent: "primary" },
      { text: "Semua modul Level Intermediate", included: true, strong: true },
      { text: "2 sertifikat (Basic + Intermediate)", included: true },
      { text: "Catatan & bookmark berwaktu", included: true },
    ],
  },
  {
    key: "advanced",
    badge: "advanced",
    name: "Advanced",
    tagline: "Kuasai AI end-to-end & untuk tim.",
    monthly: 349000,
    annualMonthly: 290000,
    cta: "Pilih Advanced",
    ctaVariant: "advanced",
    features: [
      { text: "Semua di Intermediate, plus:", included: true, lead: true, accent: "advanced" },
      { text: "Semua modul Level Advanced", included: true, strong: true },
      { text: "3 sertifikat lengkap", included: true },
      { text: "Akses paling awal ke modul baru", included: true },
    ],
  },
];

export function formatIdr(n: number): string {
  return "Rp " + n.toLocaleString("id-ID");
}
