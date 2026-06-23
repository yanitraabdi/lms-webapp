import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { PublicShell } from "@/components/PublicShell";
import { AlertTriangleIcon } from "@/components/ui";

export const dynamic = "force-static";

interface LegalDoc {
  title: string;
  updated: string;
  intro: string;
  sections: { heading: string; body: string }[];
}

// PLACEHOLDER content — structure is real, copy must be replaced with lawyer-reviewed
// text before launch (see the banner). Bahasa Indonesia, UU PDP-aware.
const DOCS: Record<string, LegalDoc> = {
  terms: {
    title: "Ketentuan Layanan",
    updated: "23 Juni 2026",
    intro: "Ketentuan ini mengatur penggunaan Anda atas platform AI Productivity Academy.",
    sections: [
      { heading: "1. Penerimaan ketentuan", body: "Dengan membuat akun atau menggunakan layanan, Anda menyetujui ketentuan ini. Jika tidak setuju, mohon untuk tidak menggunakan layanan." },
      { heading: "2. Akun Anda", body: "Anda bertanggung jawab menjaga kerahasiaan kredensial dan seluruh aktivitas pada akun Anda. Verifikasi email diperlukan sebelum berlangganan." },
      { heading: "3. Langganan & pembayaran", body: "Pembayaran diproses melalui Xendit. Langganan bersifat berulang hingga dibatalkan. Upgrade berlaku prorata; downgrade berlaku pada perpanjangan berikutnya." },
      { heading: "4. Konten & sertifikat", body: "Materi pembelajaran dilindungi hak cipta. Sertifikat yang terbit bersifat permanen dan dapat diverifikasi publik." },
    ],
  },
  privacy: {
    title: "Kebijakan Privasi",
    updated: "23 Juni 2026",
    intro: "Kebijakan ini menjelaskan bagaimana kami mengumpulkan dan menggunakan data Anda, selaras dengan UU PDP.",
    sections: [
      { heading: "1. Data yang dikumpulkan", body: "Kami mengumpulkan data akun (nama, email), data langganan & pembayaran, serta progres belajar Anda." },
      { heading: "2. Penggunaan data", body: "Data digunakan untuk menyediakan layanan, memproses pembayaran, dan meningkatkan pengalaman belajar. Kami tidak menjual data Anda." },
      { heading: "3. Pihak ketiga", body: "Kami menggunakan penyedia tepercaya (mis. Xendit untuk pembayaran, penyedia video & email). Mereka memproses data sesuai instruksi kami." },
      { heading: "4. Keamanan & hak Anda", body: "Anda berhak mengakses, memperbaiki, dan menghapus data Anda. Penghapusan akun memicu anonimisasi terjadwal; data finansial/audit dipertahankan secara anonim." },
    ],
  },
  refund: {
    title: "Kebijakan Pengembalian Dana",
    updated: "23 Juni 2026",
    intro: "Ketentuan dan proses pengajuan pengembalian dana.",
    sections: [
      { heading: "1. Kelayakan", body: "Permintaan pengembalian dapat diajukan dalam 7 hari sejak pembayaran pertama, selama penggunaan masih wajar." },
      { heading: "2. Cara mengajukan", body: "Hubungi tim dukungan melalui halaman Kontak dengan menyertakan kode transaksi Anda." },
      { heading: "3. Proses & tenggat", body: "Pengembalian yang disetujui diproses dalam 7–14 hari kerja ke metode pembayaran asal." },
      { heading: "4. Pengecualian", body: "Sertifikat yang sudah terbit dan perpanjangan setelah 7 hari tidak memenuhi syarat pengembalian." },
    ],
  },
  accessibility: {
    title: "Pernyataan Aksesibilitas",
    updated: "23 Juni 2026",
    intro: "Komitmen kami terhadap aksesibilitas untuk semua pengguna.",
    sections: [
      { heading: "1. Komitmen kami", body: "Kami berupaya memenuhi standar WCAG 2.1 AA agar layanan dapat digunakan semua orang." },
      { heading: "2. Fitur aksesibilitas", body: "Caption Bahasa Indonesia pada video, navigasi keyboard, indikator fokus yang jelas, dan kontras warna yang memadai." },
      { heading: "3. Umpan balik", body: "Menemui hambatan aksesibilitas? Beri tahu kami melalui halaman Kontak agar dapat kami perbaiki." },
    ],
  },
  cookies: {
    title: "Kebijakan Cookie",
    updated: "23 Juni 2026",
    intro: "Bagaimana kami menggunakan cookie dan pilihan yang Anda miliki.",
    sections: [
      { heading: "1. Cookie esensial", body: "Diperlukan untuk fungsi inti seperti sesi login dan keamanan. Selalu aktif." },
      { heading: "2. Cookie analitik", body: "Membantu kami memahami penggunaan untuk meningkatkan layanan. Bersifat opsional (perlu persetujuan)." },
      { heading: "3. Pilihan Anda", body: "Anda dapat mengatur preferensi cookie melalui banner saat kunjungan pertama, dan mengubahnya kapan saja." },
    ],
  },
};

export function generateStaticParams() {
  return Object.keys(DOCS).map((doc) => ({ doc }));
}

export async function generateMetadata({ params }: { params: Promise<{ doc: string }> }): Promise<Metadata> {
  const { doc } = await params;
  const d = DOCS[doc];
  return { title: d ? `${d.title} — AI Productivity Academy` : "Dokumen tidak ditemukan" };
}

export default async function LegalDocPage({ params }: { params: Promise<{ doc: string }> }) {
  const { doc } = await params;
  const d = DOCS[doc];
  if (!d) notFound();

  return (
    <PublicShell>
      <div className="mx-auto max-w-4xl px-6 py-12">
        <div className="mb-6 flex items-start gap-3 rounded-base border border-warning/30 bg-warning-soft px-4 py-3 text-[13px] text-ink">
          <AlertTriangleIcon size={18} className="mt-0.5 shrink-0 text-warning" />
          <span>Konten contoh — perlu tinjauan hukum sebelum peluncuran. Teks final akan menggantikan halaman ini.</span>
        </div>

        <h1 className="text-3xl font-extrabold tracking-tight">{d.title}</h1>
        <p className="mt-1 text-[13px] text-ink-subtle">Terakhir diperbarui: {d.updated}</p>
        <p className="mt-4 text-[15px] leading-relaxed text-ink-muted">{d.intro}</p>

        <div className="mt-8 grid gap-8 md:grid-cols-[200px_1fr]">
          <aside className="hidden md:block">
            <nav className="sticky top-20 flex flex-col gap-2 text-[13px]">
              <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">Daftar isi</span>
              {d.sections.map((s, i) => (
                <a key={i} href={`#sec-${i}`} className="text-ink-muted hover:text-primary">{s.heading}</a>
              ))}
            </nav>
          </aside>

          <article className="flex flex-col gap-6">
            {d.sections.map((s, i) => (
              <section key={i} id={`sec-${i}`} className="flex flex-col gap-2">
                <h2 className="text-lg font-extrabold">{s.heading}</h2>
                <p className="text-[14.5px] leading-relaxed text-ink-muted">{s.body}</p>
              </section>
            ))}
          </article>
        </div>

        <div className="mt-10 flex flex-wrap gap-2 border-t border-border pt-6 text-[13px]">
          {Object.entries(DOCS).map(([slug, dd]) => (
            <Link
              key={slug}
              href={`/legal/${slug}`}
              className={"rounded-sm px-3 py-1.5 font-semibold " + (slug === doc ? "bg-primary-soft text-primary" : "text-ink-muted hover:bg-surface-2")}
            >
              {dd.title}
            </Link>
          ))}
        </div>
      </div>
    </PublicShell>
  );
}
