import { ITP_TOTAL_MINUTES, ITP_TOTAL_QUESTIONS } from "@/lib/itpFormat";
import type { PublicSession } from "@/lib/programs";

const PART_LABEL: Record<string, string> = { LessonVideo: "Video materi", Test: "Tes", Discussion: "Video pembahasan" };

/** The parts of a session as a single muted line; renders nothing when there are none. */
export function SyllabusParts({ session }: { session: PublicSession }) {
  if (session.type === "FinalAssessment")
    return <span className="mt-1 block text-[12.5px] text-ink-subtle">{ITP_TOTAL_QUESTIONS} soal · {ITP_TOTAL_MINUTES} menit</span>;
  const parts = session.parts ?? [];
  if (parts.length === 0) return null;
  return <span className="mt-1 block text-[12.5px] text-ink-subtle">{parts.map((p) => PART_LABEL[p.kind] ?? p.kind).join(" · ")}</span>;
}
