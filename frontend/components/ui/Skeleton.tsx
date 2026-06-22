import { cn } from "@/lib/cn";

export interface SkeletonProps {
  className?: string;
}

/** Pulsing placeholder block for loading states. */
export function Skeleton({ className }: SkeletonProps) {
  return <div className={cn("animate-pulse rounded bg-surface-2", className)} />;
}
