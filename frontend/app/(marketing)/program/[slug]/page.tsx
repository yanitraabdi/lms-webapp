import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PublicNav } from "@/components/PublicNav";
import { SiteFooter } from "@/components/SiteFooter";
import { CheckIcon, LockIcon, PlayIcon } from "@/components/ui";
import { EnrollCta } from "@/components/program/EnrollCta";
import {
  getPublicProgram, formatIdr, minutesLabel, totalDurationLabel, fmtDateTime,
  SESSION_TYPE_LABEL, num, type PublicSession,
} from "@/lib/programs";

export const revalidate = 300;

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const program = await getPublicProgram(slug).catch(() => null);
  if (!program) return { title: "Program tidak ditemukan" };
  return {
    title: `${program.name} — INVERTA`,
    description: program.summary ?? program.description.slice(0, 160),
  };
}

export default async function ProgramPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const program = await getPublicProgram(slug).catch(() => null);
  if (!program) notFound();

  const sessions = [...program.sessions].sort((a, b) => num(a.orderIndex) - num(b.orderIndex));
  const videoCount = sessions.filter((s) => s.type === "Video").length;
  const liveCount = sessions.filter((s) => s.type === "Live").length;

  return (
    <>
      <PublicNav />
      <main>
        {/* Hero */}
        <section className="border-b border-border bg-surface px-6 py-14">
          <div className="mx-auto grid max-w-6xl gap-10 lg:grid-cols-[1fr_340px] lg:items-start">
            <div className="flex flex-col gap-4">
              <span className="text-xs font-bold uppercase tracking-widest text-primary">Program Persiapan</span>
              <h1 className="text-4xl font-extrabold leading-tight tracking-tight">{program.name}</h1>
              {program.summary && (
                <p className="max-w-2xl text-[16.5px] leading-relaxed text-ink-muted">{program.summary}</p>
              )}
              <div className="mt-1 flex flex-wrap gap-x-6 gap-y-2 text-[13.5px] text-ink-muted">
                <span><strong className="text-ink">{program.sessionCount}</strong> sesi terstruktur</span>
                {videoCount > 0 && <span><strong className="text-ink">{videoCount}</strong> video pembelajaran</span>}
                {liveCount > 0 && <span><strong className="text-ink">{liveCount}</strong> sesi live</span>}
                <span>Total <strong className="text-ink">{totalDurationLabel(program.totalDurationSeconds)}</strong> materi</span>
              </div>
            </div>

            {/* Price card */}
            <aside className="rounded-lg border border-border bg-surface p-6 shadow-sm lg:sticky lg:top-6">
              <span className="text-[13px] text-ink-muted">Biaya program</span>
              <div className="mt-1 flex flex-wrap items-baseline gap-x-2">
                <span className="whitespace-nowrap text-[32px] font-extrabold leading-tight tracking-tight">
                  {formatIdr(program.priceIdr)}
                </span>
                <span className="whitespace-nowrap text-[13px] text-ink-muted">/ sekali bayar</span>
              </div>
              <p className="mt-2 text-[12.5px] leading-snug text-ink-subtle">
                Sekali bayar untuk seluruh program — bukan langganan bulanan.
              </p>

              <EnrollCta programId={program.id} className="mt-4" />

              <ul className="mt-5 flex flex-col gap-2.5 border-t border-border pt-4">
                {[
                  "Akses seluruh sesi video",
                  "Tes singkat di setiap sesi",
                  "Sesi live bersama pengajar",
                  "Simulasi tes akhir + prediksi skor",
                  "Sertifikat yang dapat diverifikasi",
                ].map((f) => (
                  <li key={f} className="flex gap-2.5 text-[13.5px] leading-snug text-ink-muted">
                    <CheckIcon size={18} strokeWidth={2.4} className="mt-0.5 shrink-0 text-success" />
                    {f}
                  </li>
                ))}
              </ul>
            </aside>
          </div>
        </section>

        {/* About */}
        <section className="px-6 py-12">
          <div className="mx-auto max-w-3xl">
            <h2 className="mb-3 text-2xl font-extrabold tracking-tight">Tentang program ini</h2>
            <p className="whitespace-pre-line text-[15px] leading-relaxed text-ink-muted">{program.description}</p>
          </div>
        </section>

        {/* Syllabus */}
        <section className="border-t border-border px-6 py-12">
          <div className="mx-auto max-w-3xl">
            <h2 className="mb-1 text-2xl font-extrabold tracking-tight">Silabus</h2>
            <p className="mb-5 text-sm text-ink-muted">
              Program bersifat <strong className="text-ink">berurutan</strong> — setiap sesi terbuka
              setelah sesi sebelumnya diselesaikan.
            </p>
            <ol className="flex flex-col gap-2.5">
              {sessions.map((s, i) => <SyllabusRow key={s.id} session={s} index={i} />)}
            </ol>
          </div>
        </section>

        {/* Prediction disclaimer — KAK §9.9.6, non-negotiable */}
        <section className="px-6 pb-16">
          <div className="mx-auto max-w-3xl rounded-base border border-border bg-surface-2 px-5 py-4">
            <p className="text-[12.5px] leading-relaxed text-ink-muted">
              <strong className="text-ink">Catatan penting.</strong> Skor yang dihasilkan program ini
              adalah <strong className="text-ink">prediksi INVERTA</strong> berdasarkan simulasi
              internal — <strong className="text-ink">bukan skor TOEFL resmi</strong> dan tidak
              diterbitkan oleh ETS. TOEFL adalah merek dagang terdaftar milik ETS.
            </p>
          </div>
        </section>
      </main>
      <SiteFooter />
    </>
  );
}

function SyllabusRow({ session, index }: { session: PublicSession; index: number }) {
  const isLive = session.type === "Live";
  const isFinal = session.type === "FinalAssessment";
  return (
    <li className="flex items-start gap-3.5 rounded-base border border-border bg-surface px-4 py-3.5">
      <span
        className={
          "mt-0.5 inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-[12px] font-bold " +
          (index === 0 ? "bg-primary text-primary-ink" : "bg-surface-2 text-ink-muted")
        }
      >
        {index === 0 ? <PlayIcon size={12} /> : index + 1}
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-2">
          <span className="text-[14.5px] font-bold text-ink">{session.title}</span>
          {(isLive || isFinal) && (
            <span className="rounded bg-primary-soft px-2 py-0.5 text-[10.5px] font-bold uppercase tracking-wide text-primary">
              {SESSION_TYPE_LABEL[session.type] ?? session.type}
            </span>
          )}
        </span>
        {session.description && (
          <span className="mt-0.5 block text-[13px] leading-snug text-ink-muted">{session.description}</span>
        )}
        <span className="mt-1 block text-[11.5px] text-ink-subtle">
          {session.durationSeconds != null && minutesLabel(session.durationSeconds)}
          {session.scheduledAt && `Dijadwalkan ${fmtDateTime(session.scheduledAt)}`}
          {session.durationSeconds == null && !session.scheduledAt && "Tes berwaktu"}
        </span>
      </span>
      {index > 0 && <LockIcon size={15} className="mt-1 shrink-0 text-ink-subtle" />}
    </li>
  );
}
