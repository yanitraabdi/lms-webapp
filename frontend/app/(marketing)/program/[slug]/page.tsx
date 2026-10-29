import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PublicNav } from "@/components/PublicNav";
import { SiteFooter } from "@/components/SiteFooter";
import { SheetPage, RunningHead, PageTitle, Lead, Chapter, Footnote, Toc, TocItem } from "@/components/ui";
import { EnrollCta } from "@/components/program/EnrollCta";
import { HowItWorks } from "@/components/program/HowItWorks";
import { SyllabusParts } from "@/components/program/SyllabusParts";
import { ItpFormat } from "@/components/program/ItpFormat";
import { ProgramFaq } from "@/components/program/ProgramFaq";
import { ClosingCta } from "@/components/program/ClosingCta";
import { ProgramJsonLd } from "@/components/program/ProgramJsonLd";
import { getFaq } from "@/lib/content";
import {
  getPublicProgram, minutesLabel, totalDurationLabel, fmtDateTime,
  num, type PublicSession,
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
    description: program.summary ?? program.description.slice(0, 160),
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

  const facts: { dt: string; dd: string }[] = [
    { dt: "Sesi terstruktur", dd: String(program.sessionCount) },
    ...(videoCount > 0 ? [{ dt: "Video pembelajaran", dd: String(videoCount) }] : []),
    ...(liveCount > 0 ? [{ dt: "Sesi live", dd: String(liveCount) }] : []),
    { dt: "Total materi", dd: totalDurationLabel(program.totalDurationSeconds) },
  ];

  return (
    <>
      <PublicNav />
      <main>
        <ProgramJsonLd program={program} faq={faqShown} />
        <SheetPage>
          <div className="min-[860px]:grid min-[860px]:grid-cols-[minmax(0,1fr)_300px] min-[860px]:gap-14">
            <div className="min-w-0">
              <RunningHead>Program persiapan TOEFL ITP</RunningHead>
              <PageTitle>{program.name}</PageTitle>
              {program.summary && <Lead>{program.summary}</Lead>}
              <dl className="mt-6 grid grid-cols-2 gap-x-6 gap-y-4 min-[560px]:grid-cols-4">
                {facts.map((f) => (
                  <div key={f.dt}>
                    <dt className="text-[12.5px] text-ink-muted">{f.dt}</dt>
                    <dd className="text-[20px] font-extrabold tabular-nums text-ink">{f.dd}</dd>
                  </div>
                ))}
              </dl>

              <div className="mt-8 min-[860px]:hidden">
                <EnrollCta programId={program.id} priceIdr={program.priceIdr} />
              </div>

              <Chapter title="Tentang program ini">
                <p className="mt-3 max-w-[65ch] whitespace-pre-line leading-relaxed text-ink-muted">{program.description}</p>
              </Chapter>

              <Chapter
                title="Daftar isi"
                intro="Sesi terbuka berurutan. Sesi berikutnya terbuka setelah Anda lulus tes di sesi sebelumnya."
              >
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
              </Chapter>

              <ItpFormat />

              <HowItWorks />

              <ProgramFaq items={faqShown} hasMore={faqHasMore} />

              <ClosingCta programId={program.id} />

              {/* Prediction disclaimer: KAK 9.9.6, non-negotiable */}
              <Footnote>
                <strong className="text-ink">Catatan penting.</strong> Skor yang dihasilkan program ini
                adalah <strong className="text-ink">prediksi INVERTA</strong> berdasarkan simulasi
                internal, <strong className="text-ink">bukan skor TOEFL resmi</strong> dan tidak
                diterbitkan oleh ETS. TOEFL adalah merek dagang terdaftar milik ETS.
              </Footnote>
            </div>

            <aside className="mt-10 hidden min-[860px]:mt-0 min-[860px]:block">
              <EnrollCta programId={program.id} priceIdr={program.priceIdr} />
            </aside>
          </div>
        </SheetPage>
      </main>
      <SiteFooter />
    </>
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
