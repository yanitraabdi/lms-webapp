import type { HTMLAttributes } from "react";
import { cn } from "@/lib/cn";

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** Remove default padding (for cards with custom internal layout). */
  flush?: boolean;
}

/** Object box: white, hairline border, lg radius. */
export function Card({ flush = false, className, ...props }: CardProps) {
  return (
    <div
      className={cn(
        "rounded-lg border border-border bg-surface",
        !flush && "p-5",
        className
      )}
      {...props}
    />
  );
}
