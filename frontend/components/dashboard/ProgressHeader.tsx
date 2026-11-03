import Link from "next/link";
import { cn } from "@/lib/cn";
import { num, type StudentProgram, type StudentSession } from "@/lib/programs";

const LINK_BTN =
  "mt-5 inline-flex h-11 items-center justify-center rounded-sm bg-primary px-5 text-[14px] font-bold text-primary-ink hover:bg-primary-hover focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus";

/** Overall progress: a count and a segmented meter. "What's next" lives in NextUpPanel. */
export function ProgressHeader({ program }: { program: StudentProgram }) {
  const completed = num(program.completedCount);
  const total = num(program.sessionCount);
  const next = program.sessions.find((s) => s.id === program.nextSessionId);

  return (
    <section className="mt-8" data-tour="overall-progress">
      {next ? (
        <p className="text-[18px] font-extrabold leading-snug text-ink">{`${completed} dari ${total} sesi selesai`}</p>
      ) : (
        <>
          <p className="text-[30px] font-extrabold leading-tight tracking-[-0.015em] text-ink">Semua sesi selesai</p>
          <p className="mt-2 max-w-[60ch] text-ink-muted">Sertifikat Anda tersedia di halaman Sertifikat.</p>
        </>
      )}
      <div
        role="img"
        aria-label={`${completed} dari ${total} sesi selesai`}
        className="mt-5 flex flex-wrap gap-1"
      >
        {program.sessions.map((s) => (
          <span
            key={s.id}
            className={cn("h-1.5 w-[26px] rounded-[3px]", s.state === "Completed" ? "bg-primary" : "bg-border")}
          />
        ))}
      </div>
      {!next && (
        <Link href="/app/certificates" className={LINK_BTN}>Lihat sertifikat</Link>
      )}
    </section>
  );
}

/**
 * The final assessment has its own runner. Everything else opens the programme page with the
 * session preselected, so "Lanjutkan" lands on the same master-detail view the session list uses.
 */
export function nextHref(session: StudentSession, programId: string): string {
  return session.type === "FinalAssessment"
    ? `/app/assessment/${session.id}`
    : `/app/program/${programId}?session=${session.id}`;
}
