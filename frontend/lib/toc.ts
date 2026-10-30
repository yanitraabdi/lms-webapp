import type { StudentSession } from "@/lib/programs";

export function tocState(s: StudentSession, nextId: string | null): "done" | "current" | "open" | "locked" {
  if (s.state === "Completed") return "done";
  if (s.state === "Locked") return "locked";
  return s.id === nextId ? "current" : "open";
}

// Per-test scores are not on the programme DTO, so a finished video session reads "Selesai".
export function tocMeta(s: StudentSession, nextId: string | null): string {
  if (s.state === "Locked") return "Terkunci";
  if (s.state === "Completed") return "Selesai";
  return s.id === nextId ? "Berikutnya" : "Terbuka";
}
