"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Button, Modal, Spinner, CheckIcon, AlertTriangleIcon } from "@/components/ui";
import { getProgramReadiness, type AdminProgram } from "@/lib/programs";

/** Where to go to fix each failing check. */
const FIX: Record<string, (p: AdminProgram) => { href: string; label: string } | null> = {
  score_bands_complete: (p) => ({ href: `/admin/programs/${p.id}/score-bands`, label: "Buka tabel konversi" }),
  gating_tests_populated: () => null,
  final_sections_populated: () => null,
  has_sessions: () => null,
  final_assessment_present: () => null,
  price_set: () => null,
};

export function ReadinessPanel({
  token, program, onClose,
}: { token: string; program: AdminProgram; onClose: () => void }) {
  const q = useQuery({
    queryKey: ["readiness", program.id],
    queryFn: () => getProgramReadiness(token, program.id),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title={`Kesiapan — ${program.name}`}
      className="max-w-lg"
      footer={<div className="flex w-full justify-end"><Button size="sm" onClick={onClose}>Tutup</Button></div>}
    >
      {q.isPending ? (
        <div className="flex min-h-[160px] items-center justify-center"><Spinner size={22} /></div>
      ) : q.isError ? (
        <p className="py-6 text-center text-sm text-ink-muted">Gagal memuat status kesiapan.</p>
      ) : (
        <div className="flex flex-col gap-3">
          <div
            className={
              "rounded-base px-4 py-3 text-[13px] font-semibold " +
              (q.data.ready ? "bg-success-soft text-success" : "bg-warning-soft text-warning")
            }
          >
            {q.data.ready
              ? "Program siap diterbitkan."
              : "Program belum bisa diterbitkan. Lengkapi item bertanda di bawah."}
          </div>

          <ul className="flex flex-col gap-2">
            {q.data.checks.map((c) => {
              const fix = !c.passed ? FIX[c.key]?.(program) : null;
              return (
                <li key={c.key} className="flex items-start gap-2.5 rounded-base border border-border px-3.5 py-2.5">
                  <span className={"mt-0.5 shrink-0 " + (c.passed ? "text-success" : c.blocking ? "text-danger" : "text-ink-subtle")}>
                    {c.passed ? <CheckIcon size={16} strokeWidth={3} /> : <AlertTriangleIcon size={16} />}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block text-[13.5px] font-semibold text-ink">
                      {c.title}
                      {!c.blocking && <span className="ml-1.5 text-[11px] font-normal text-ink-subtle">(tidak wajib)</span>}
                    </span>
                    {c.detail && <span className="mt-0.5 block text-[12.5px] leading-snug text-ink-muted">{c.detail}</span>}
                    {fix && (
                      <Link href={fix.href} className="mt-1 inline-block text-[12.5px] font-bold text-primary hover:underline">
                        {fix.label} →
                      </Link>
                    )}
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </Modal>
  );
}
