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
    updated: "14 Agustus 2026",
    intro: "Ketentuan ini mengatur penggunaan Anda atas program persiapan TOEFL INVERTA.",
    sections: [
      { heading: "1. Penerimaan ketentuan", body: "Dengan membuat akun atau mendaftar program, Anda menyetujui ketentuan ini. Jika tidak setuju, mohon untuk tidak menggunakan layanan." },
      { heading: "2. Akun Anda", body: "Anda bertanggung jawab menjaga kerahasiaan kredensial dan seluruh aktivitas pada akun Anda. Verifikasi email diperlukan sebelum melakukan pendaftaran berbayar." },
      { heading: "3. Pendaftaran & pembayaran", body: "Program dibeli SEKALI BAYAR untuk satu kali pendaftaran — bukan langganan berulang. Pembayaran diproses melalui Xendit. Akses program terbuka setelah pembayaran terverifikasi." },
      { heading: "4. Struktur program", body: "Program bersifat berurutan: setiap sesi terbuka setelah sesi sebelumnya diselesaikan (menonton video dan lulus tes sesi, kehadiran sesi live ditandai admin, atau tes akhir dikirim). Penyelesaian tidak dapat dilewati." },
      { heading: "5. Tes akhir & pengawasan", body: "Tes akhir berwaktu dan diawasi secara lunak: sistem mendeteksi jika Anda meninggalkan halaman tes. Pelanggaran pertama diberi peringatan; pelanggaran kedua membuat tes dikirim otomatis dan ditandai untuk ditinjau. Anda dapat mengajukan peninjauan bila terjadi salah deteksi." },
      { heading: "6. Skor & sertifikat — bukan skor resmi ETS", body: "Skor dan sertifikat yang diterbitkan adalah PREDIKSI internal INVERTA berdasarkan simulasi, BUKAN skor TOEFL resmi dan tidak diterbitkan oleh ETS. TOEFL adalah merek dagang terdaftar milik Educational Testing Service (ETS). Sertifikat prediksi tidak dapat digunakan sebagai pengganti hasil tes resmi untuk keperluan akademik atau imigrasi." },
      { heading: "7. Konten & hak cipta", body: "Materi pembelajaran, soal, dan audio dilindungi hak cipta. Dilarang merekam, menyalin, menyebarluaskan, atau memperjualbelikan materi tanpa izin tertulis." },
      { heading: "8. Sertifikat bersifat permanen", body: "Sertifikat yang telah terbit tidak diubah. Jika kesempatan mengulang diberikan, sertifikat BARU akan diterbitkan tanpa membatalkan yang lama." },
    ],
  },
  privacy: {
    title: "Kebijakan Privasi",
    updated: "14 Agustus 2026",
    intro: "Kebijakan ini menjelaskan bagaimana kami mengumpulkan dan menggunakan data Anda, selaras dengan UU Perlindungan Data Pribadi (UU PDP).",
    sections: [
      { heading: "1. Data yang dikumpulkan", body: "Data akun (nama, email), data pendaftaran & pembayaran, progres belajar, jawaban dan skor tes, serta catatan kejadian pengawasan selama tes akhir." },
      { heading: "2. Data pengawasan tes", body: "Selama tes akhir, sistem mencatat kejadian berpindahnya fokus dari halaman tes beserta waktunya. Data ini digunakan semata untuk menjaga integritas tes dan meninjau sengketa, disimpan bersama percobaan tes Anda, dan dapat Anda minta untuk ditinjau." },
      { heading: "3. Penggunaan data", body: "Data digunakan untuk menyediakan layanan, memproses pembayaran, menilai tes, menerbitkan sertifikat, dan meningkatkan program. Kami tidak menjual data Anda." },
      { heading: "4. Pihak ketiga", body: "Kami menggunakan penyedia tepercaya (Xendit untuk pembayaran, penyedia video, penyimpanan berkas, dan pengiriman email). Mereka memproses data sesuai instruksi kami." },
      { heading: "5. Retensi", body: "Progres belajar, percobaan tes, catatan pengawasan, dan sertifikat disimpan secara permanen agar sertifikat tetap dapat diverifikasi. Pencabutan akses tidak menghapus data ini." },
      { heading: "6. Hak Anda", body: "Anda berhak mengakses, memperbaiki, dan meminta penghapusan data. Penghapusan akun memicu anonimisasi terjadwal; data finansial dan audit dipertahankan dalam bentuk anonim sesuai kewajiban hukum." },
    ],
  },
  refund: {
    title: "Kebijakan Pengembalian Dana",
    updated: "14 Agustus 2026",
    intro: "Program INVERTA dibeli sekali bayar. Ketentuan berikut mengatur pengembalian dana untuk pembelian satu kali tersebut.",
    sections: [
      { heading: "1. Sifat pembelian", body: "Pendaftaran adalah pembelian SATU KALI untuk keseluruhan program — bukan langganan. Tidak ada penagihan berulang dan tidak ada perpanjangan otomatis." },
      { heading: "2. Masa tenggang (cooling-off)", body: "[PLACEHOLDER — perlu keputusan bisnis] Usulan: permintaan pengembalian penuh dapat diajukan dalam 7 hari sejak pembayaran SELAMA Anda belum menyelesaikan sesi pertama." },
      { heading: "3. Setelah program dimulai", body: "[PLACEHOLDER — perlu keputusan bisnis] Usulan: setelah sesi pertama diselesaikan, pengembalian bersifat prorata atau tidak tersedia, mengingat materi sudah dapat diakses." },
      { heading: "4. Tidak memenuhi syarat", body: "[PLACEHOLDER — perlu keputusan bisnis] Usulan: pengembalian tidak tersedia setelah tes akhir dikerjakan atau sertifikat prediksi diterbitkan." },
      { heading: "5. Cara mengajukan", body: "Hubungi tim dukungan melalui halaman Kontak dengan menyertakan email akun dan bukti pembayaran. Kami akan menanggapi dalam 3 hari kerja." },
      { heading: "6. Proses & tenggat", body: "Pengembalian yang disetujui diproses dalam 7–14 hari kerja ke metode pembayaran asal. Akses program dicabut saat pengembalian disetujui; progres belajar Anda tetap tersimpan." },
    ],
  },
  accessibility: {
    title: "Pernyataan Aksesibilitas",
    updated: "14 Agustus 2026",
    intro: "Komitmen kami terhadap aksesibilitas untuk semua peserta.",
    sections: [
      { heading: "1. Komitmen kami", body: "Kami berupaya memenuhi standar WCAG 2.1 AA agar layanan dapat digunakan semua orang." },
      { heading: "2. Fitur aksesibilitas", body: "Caption Bahasa Indonesia pada video, kontrol audio yang dapat diakses pada bagian Listening, navigasi keyboard penuh, indikator fokus yang jelas, dan kontras warna yang memadai." },
      { heading: "3. Akomodasi tes", body: "Jika Anda memerlukan penyesuaian waktu atau bantuan lain untuk mengerjakan tes akhir, hubungi kami sebelum memulai tes agar dapat kami atur." },
      { heading: "4. Umpan balik", body: "Menemui hambatan aksesibilitas? Beri tahu kami melalui halaman Kontak agar dapat kami perbaiki." },
    ],
  },
  cookies: {
    title: "Kebijakan Cookie",
    updated: "14 Agustus 2026",
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
  return { title: d ? `${d.title} — INVERTA` : "Dokumen tidak ditemukan" };
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
          <span>Konten contoh — perlu tinjauan hukum sebelum peluncuran, khususnya penamaan/klaim terkait TOEFL (merek dagang ETS) dan bagian bertanda [PLACEHOLDER] pada Kebijakan Pengembalian Dana.</span>
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
