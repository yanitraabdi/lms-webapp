import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { PublicNav } from "@/components/PublicNav";
import { SiteFooter } from "@/components/SiteFooter";
import { Footnote, Toc, TocItem } from "@/components/ui";
import { Band } from "@/components/program/Band";
import { EnrollCta } from "@/components/program/EnrollCta";
import { BENEFITS } from "@/components/program/benefits";
import { SyllabusParts } from "@/components/program/SyllabusParts";
import { ProgramFaq } from "@/components/program/ProgramFaq";
import { ProgramJsonLd } from "@/components/program/ProgramJsonLd";
import { JourneyAnimation } from "@/components/program/JourneyAnimation";
import { AppPeekAnimation } from "@/components/program/AppPeekAnimation";
import { getFaq } from "@/lib/content";
import { ITP_FORMAT, ITP_TOTAL_MINUTES, ITP_TOTAL_QUESTIONS } from "@/lib/itpFormat";
import {
  getPublicProgram, minutesLabel, totalDurationLabel, fmtDateTime,
  formatIdr, num, type PublicSession,
} from "@/lib/programs";

export const revalidate = 300;

// No build-time fetch (API may be unreachable in the Docker build); pages are generated on first visit and ISR-cached.
export function generateStaticParams() { return []; }

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const program = await getPublicProgram(slug).catch(() => null);
  if (!program) return { title: "Program tidak ditemukan" };
  return {
    // The root layout's template appends " — INVERTA" to every title. This one appended it too,
    // and the seeded programme is NAMED "INVERTA — Persiapan TOEFL", so the tab read the brand
    // three times. A name that already carries the brand is used as-is; otherwise the template
    // adds it. Admins can rename the programme, so this cannot assume either form.
    title: program.name.includes("INVERTA") ? { absolute: program.name } : program.name,
    description: program.summary || program.description.slice(0, 160),
  };
}

export default async function ProgramPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  // No catch: an API outage throws so ISR keeps serving the stale page; only a real 404 → null.
  const program = await getPublicProgram(slug);
  if (!program) notFound();

  const faq = await getFaq({ revalidate: 300 }).catch(() => []);
  const faqShown = faq.slice(0, 6); // API already orders by OrderIndex
  const faqHasMore = faq.length > faqShown.length;
  const sessions = [...program.sessions].sort((a, b) => num(a.orderIndex) - num(b.orderIndex));
  const videoCount = sessions.filter((s) => s.type === "Video").length;
  const liveCount = sessions.filter((s) => s.type === "Live").length;

  const facts = [
    `${program.sessionCount} sesi`,
    videoCount > 0 && `${videoCount} video`,
    liveCount > 0 && `${liveCount} kelas live`,
    `${totalDurationLabel(program.totalDurationSeconds)} materi`,
  ].filter(Boolean).join(" · ");

  const parts = [
    { title: "Video materi", body: `${videoCount} video · ${totalDurationLabel(program.totalDurationSeconds)}` },
    { title: "Tes per sesi", body: "Wajib lulus untuk membuka sesi berikutnya" },
    ...(liveCount > 0 ? [{ title: "Kelas live", body: `${liveCount} kelas live terjadwal` }] : []),
    { title: "Tes akhir", body: `${ITP_TOTAL_QUESTIONS} soal · ${ITP_TOTAL_MINUTES} menit · skor prediksi` },
  ];

  const different = [
    { title: "Tidak bisa loncat", body: "Tes di setiap sesi wajib lulus sebelum sesi berikutnya terbuka, jadi dasar Anda kuat sebelum lanjut." },
    {
      title: "Format seperti TOEFL ITP",
      body: `Listening ${ITP_FORMAT.listening.questions} · Structure ${ITP_FORMAT.structure.questions} · Reading ${ITP_FORMAT.reading.questions} soal, berwaktu.`,
    },
    { title: "Hasil yang bisa dibuktikan", body: "Sertifikat prediksi skor dengan kode verifikasi yang bisa dicek siapa saja." },
  ];

  return (
    <>
      <PublicNav programId={program.id} />
      <main>
        <Band eyebrow="Persiapan TOEFL ITP · Bahasa Indonesia" pad="py-16 md:py-24">
          {/* Full-width headline: at 64px a half column fits ~15 characters a line (5 lines); this keeps it to 3. */}
          <h1 className="mt-3 max-w-[22ch] text-[clamp(40px,5vw,64px)] font-extrabold leading-[1.04] tracking-[-0.025em] text-ink">
            Belajar TOEFL langkah demi langkah, sampai tahu prediksi skor Anda.
          </h1>
          <div className="mt-6 grid items-center gap-10 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)] lg:gap-12">
            <div>
              <p className="max-w-[52ch] text-[17px] leading-relaxed text-ink-muted">
                {program.summary || "Video materi, tes singkat di setiap sesi, kelas live bersama pengajar, lalu simulasi tes akhir berformat ITP dengan sertifikat prediksi skor."}
              </p>
              <div className="mt-7 flex flex-wrap items-center gap-x-6 gap-y-3">
                <EnrollCta programId={program.id} className="w-full sm:w-56" />
                <Link
                  href="#daftar-isi"
                  className="rounded-sm font-bold text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                >
                  Lihat isi program
                </Link>
              </div>
              <p className="mt-3 text-[13.5px] text-ink-muted">Sekali bayar · akses seluruh program</p>
            </div>
            <JourneyAnimation />
          </div>
        </Band>

        <Band id="apa-itu" title="Apa itu INVERTA" tint pad="py-10 md:py-12">
          {program.description && (
            <p className="mt-4 max-w-[65ch] whitespace-pre-line text-[16.5px] leading-relaxed text-ink-muted">{program.description}</p>
          )}
          <Columns items={parts} className="sm:grid-cols-2 lg:grid-cols-4" />
        </Band>

        <Band id="daftar-isi" title="Daftar isi program" pad="py-14 md:py-20">
          <p className="mt-2 text-[15px] tabular-nums text-ink-muted">{facts}</p>
          <div className="mt-6 grid gap-12 lg:grid-cols-[minmax(0,1fr)_400px] lg:items-start">
            <Toc label="Daftar sesi">
              {sessions.map((s, i) => (
                <TocItem
                  key={s.id}
                  n={i + 1}
                  title={s.title}
                  state="open"
                  kind={s.type === "Live" ? "Live" : s.type === "FinalAssessment" ? "Tes akhir" : undefined}
                  note={s.description || undefined}
                  meta={<SessionMeta session={s} />}
                />
              ))}
            </Toc>
            <div className="lg:sticky lg:top-24">
              <AppPeekAnimation lessonTitle={sessions.find((s) => s.type === "Video")?.title} />
            </div>
          </div>
        </Band>

        <Band title="Yang membedakan" tint pad="py-10 md:py-12">
          <Columns items={different} className="md:grid-cols-3" />
        </Band>

        <Band title="Biaya" className="text-center">
          <div className="mx-auto max-w-xl">
            <p className="mt-4 text-[clamp(40px,5vw,56px)] font-extrabold leading-none tracking-[-0.025em] tabular-nums text-ink">
              {formatIdr(program.priceIdr)}
            </p>
            <p className="mt-3 text-[16px] text-ink-muted">Sekali bayar, bukan langganan</p>
            <ul className="mx-auto mt-7 inline-flex flex-col gap-2.5 text-left">
              {BENEFITS.map((b) => (
                <li key={b} className="flex gap-2.5 text-[16px] text-ink">
                  <span aria-hidden="true" className="font-bold text-success">✓</span>
                  {b}
                </li>
              ))}
            </ul>
            <EnrollCta programId={program.id} className="mx-auto mt-8 max-w-xs" />
          </div>
        </Band>

        <ProgramFaq items={faqShown} hasMore={faqHasMore} />

        {/* Prediction disclaimer: KAK 9.9.6, non-negotiable */}
        <div className="border-t border-border bg-surface">
          <div className="mx-auto max-w-6xl px-6 pb-10 pt-6">
            <Footnote>
              <strong className="text-ink">Catatan penting.</strong> Skor yang dihasilkan program ini
              adalah <strong className="text-ink">prediksi INVERTA</strong> berdasarkan simulasi
              internal, <strong className="text-ink">bukan skor TOEFL resmi</strong> dan tidak
              diterbitkan oleh ETS. TOEFL adalah merek dagang terdaftar milik ETS.
            </Footnote>
          </div>
        </div>

        <ProgramJsonLd program={program} faq={faqShown} />
      </main>
      <SiteFooter />
    </>
  );
}

/** A row of titled columns, each under a heavy rule: a rule, never a box. */
function Columns({ items, className }: { items: { title: string; body: string }[]; className: string }) {
  return (
    <ul className={`mt-8 grid gap-x-10 gap-y-8 ${className}`}>
      {items.map((it) => (
        <li key={it.title} className="border-t-2 border-ink pt-4">
          <h3 className="text-[17px] font-extrabold text-ink">{it.title}</h3>
          <p className="mt-1.5 text-[15.5px] leading-relaxed text-ink-muted">{it.body}</p>
        </li>
      ))}
    </ul>
  );
}

function SessionMeta({ session }: { session: PublicSession }) {
  const isFinal = session.type === "FinalAssessment";
  const line = [
    session.durationSeconds != null ? minutesLabel(session.durationSeconds) : null,
    session.scheduledAt ? `Dijadwalkan ${fmtDateTime(session.scheduledAt)}` : null,
    session.durationSeconds == null && !session.scheduledAt && !isFinal ? "Tes berwaktu" : null,
  ].filter(Boolean).join(" · ");
  return (
    <>
      {line}
      <SyllabusParts session={session} />
    </>
  );
}
