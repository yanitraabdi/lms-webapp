"use client";

import { cn } from "@/lib/cn";
import { useUiStrings } from "./locale";

export interface SpinnerProps {
  /** Diameter in px. */
  size?: number;
  className?: string;
}

/** Indeterminate ring spinner (used in Button loading state). */
export function Spinner({ size = 16, className }: SpinnerProps) {
  const t = useUiStrings();
  return (
    <span
      role="status"
      aria-label={t.loading}
      className={cn("inline-block animate-spin rounded-full border-2 border-current border-t-transparent", className)}
      style={{ width: size, height: size }}
    />
  );
}
