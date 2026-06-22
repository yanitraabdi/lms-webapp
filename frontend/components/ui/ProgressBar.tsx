import { cn } from "@/lib/cn";

export interface ProgressBarProps {
  /** 0..100 */
  value: number;
  label?: string;
  valueLabel?: string;
  tone?: "primary" | "success";
  size?: "sm" | "md";
  className?: string;
}

export function ProgressBar({
  value,
  label,
  valueLabel,
  tone = "primary",
  size = "md",
  className,
}: ProgressBarProps) {
  const pct = Math.max(0, Math.min(100, value));
  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      {(label || valueLabel) && (
        <div className="flex justify-between text-xs text-ink-muted">
          {label && <span>{label}</span>}
          {valueLabel && <span className="font-bold text-ink">{valueLabel}</span>}
        </div>
      )}
      <div className={cn("overflow-hidden rounded-full bg-surface-2", size === "sm" ? "h-1.5" : "h-2.5")}>
        <div
          className={cn("h-full rounded-full", tone === "success" ? "bg-success" : "bg-primary")}
          style={{ width: `${pct}%` }}
          role="progressbar"
          aria-valuenow={pct}
          aria-valuemin={0}
          aria-valuemax={100}
        />
      </div>
    </div>
  );
}
