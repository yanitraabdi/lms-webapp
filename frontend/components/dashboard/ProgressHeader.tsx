import Link from "next/link";
import { num, type StudentProgram, type StudentSession } from "@/lib/programs";

const R = 42;
const C = 2 * Math.PI * R;

/** Block 1: greeting, overall progress ring, and where to pick up. */
export function ProgressHeader({ program, firstName }: { program: StudentProgram; firstName: string }) {
  const completed = num(program.completedCount);
  const total = num(program.sessionCount);
  const ratio = total > 0 ? completed / total : 0;
  const next = program.sessions.find((s) => s.id === program.nextSessionId);
  const partsDone = program.nextSessionPartsDone == null ? null : num(program.nextSessionPartsDone);
  const partCount = program.nextSessionPartCount == null ? null : num(program.nextSessionPartCount);

  return (
    <section
      className="flex flex-col gap-5 rounded-lg border border-border bg-surface p-5 shadow-sm sm:flex-row sm:items-center"
      data-tour="overall-progress"
    >
      <svg
        viewBox="0 0 100 100"
        className="h-28 w-28 shrink-0 self-center"
        role="img"
        aria-label={`${completed} dari ${total} sesi selesai`}
      >
        <circle cx="50" cy="50" r={R} fill="none" strokeWidth="9" className="stroke-surface-2" />
        <circle
          cx="50" cy="50" r={R} fill="none" strokeWidth="9" strokeLinecap="round"
          className="stroke-primary"
          strokeDasharray={`${C * ratio} ${C}`}
          transform="rotate(-90 50 50)"
        />
        <text x="50" y="50" textAnchor="middle" className="fill-ink text-[20px] font-extrabold">
          {completed}/{total}
        </text>
        <text x="50" y="66" textAnchor="middle" className="fill-ink-muted text-[9px] font-semibold">
          sesi selesai
        </text>
      </svg>

      <div className="flex min-w-0 flex-1 flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-[24px] font-extrabold tracking-tight">Halo, {firstName} 👋</h1>
          <p className="text-sm text-ink-muted">Lanjutkan persiapan TOEFL Anda.</p>
        </div>

        {next ? (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-base border border-primary bg-primary-soft/50 px-4 py-3">
            <div className="flex min-w-0 flex-col">
              <span className="truncate text-[13.5px] font-bold text-ink">{next.title}</span>
              <span className="text-[12px] text-ink-muted">
                {partsDone != null && partCount != null
                  ? `Sesi ${num(next.orderIndex)} · bagian ${partsDone + 1} dari ${partCount}`
                  : `Sesi ${num(next.orderIndex)}`}
              </span>
            </div>
            <Link
              href={nextHref(next, program.programId)}
              className="inline-flex h-9 shrink-0 items-center justify-center rounded-sm bg-primary px-3.5 text-[13px] font-bold text-primary-ink hover:bg-primary-hover"
            >
              Buka →
            </Link>
          </div>
        ) : (
          <p className="rounded-base bg-success-soft px-4 py-3 text-[13px] font-semibold text-success">
            Semua sesi selesai. Sertifikat Anda tersedia di halaman Sertifikat.
          </p>
        )}
      </div>
    </section>
  );
}

/**
 * The final assessment has its own runner. Everything else opens the programme page with the
 * session preselected, so "Lanjutkan" lands on the same master-detail view the session list uses.
 */
function nextHref(session: StudentSession, programId: string): string {
  return session.type === "FinalAssessment"
    ? `/app/assessment/${session.id}`
    : `/app/program/${programId}?session=${session.id}`;
}
