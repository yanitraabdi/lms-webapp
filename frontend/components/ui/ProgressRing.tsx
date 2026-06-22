import { cn } from "@/lib/cn";

export interface ProgressRingProps {
  /** 0..100 */
  value: number;
  /** outer diameter px */
  size?: number;
  label?: string;
  className?: string;
}

/** Conic-gradient progress ring with a centered label. */
export function ProgressRing({ value, size = 62, label, className }: ProgressRingProps) {
  const pct = Math.max(0, Math.min(100, Math.round(value)));
  const inner = Math.round(size * 0.74);
  return (
    <div
      className={cn("relative inline-flex items-center justify-center rounded-full", className)}
      style={{
        width: size,
        height: size,
        background: `conic-gradient(var(--ds-color-primary) 0 ${pct}%, var(--ds-color-surface-2) ${pct}% 100%)`,
      }}
      role="progressbar"
      aria-valuenow={pct}
      aria-valuemin={0}
      aria-valuemax={100}
    >
      <div
        className="flex items-center justify-center rounded-full bg-surface text-[13px] font-extrabold text-ink"
        style={{ width: inner, height: inner }}
      >
        {label ?? `${pct}%`}
      </div>
    </div>
  );
}
