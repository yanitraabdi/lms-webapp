import { cn } from "@/lib/cn";

export interface SpinnerProps {
  /** Diameter in px. */
  size?: number;
  className?: string;
}

/** Indeterminate ring spinner (used in Button loading state). */
export function Spinner({ size = 16, className }: SpinnerProps) {
  return (
    <span
      role="status"
      aria-label="Memuat"
      className={cn("inline-block animate-spin rounded-full border-2 border-current border-t-transparent", className)}
      style={{ width: size, height: size }}
    />
  );
}
