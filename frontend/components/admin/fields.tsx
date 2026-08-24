import type { ReactNode } from "react";

/** Shared admin form primitives — used by ProgramForm, SessionForm and GatingTestEditor. */

/** Base input styling. Pair with an explicit width utility (e.g. `w-24`). */
export const inputCls =
  "rounded-sm border border-border bg-surface px-3 py-2 text-[13.5px] outline-none focus:border-primary";

/** Full-width variant, for inputs that fill their form row. */
export const inputBlockCls = `w-full ${inputCls}`;

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-[12px] font-bold text-ink-muted">{label}</span>
      {children}
    </label>
  );
}
