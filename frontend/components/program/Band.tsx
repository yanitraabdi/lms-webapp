import { useId, type ReactNode } from "react";
import { cn } from "@/lib/cn";

/** A full-width band of the programme page: a 1px rule on top (none on the first), surface or tinted background. */
export function Band({
  id,
  title,
  eyebrow,
  tint = false,
  className,
  pad = "py-12 md:py-16",
  children,
}: {
  id?: string;
  title?: ReactNode;
  eyebrow?: ReactNode;
  tint?: boolean;
  className?: string;
  /** Vertical padding; replaces the default (cn doesn't merge), so bands can vary the rhythm. */
  pad?: string;
  children: ReactNode;
}) {
  const headingId = useId();
  return (
    <section
      id={id}
      aria-labelledby={title ? headingId : undefined}
      className={cn("scroll-mt-16 border-t border-border first:border-t-0", tint ? "bg-bg" : "bg-surface", className)}
    >
      <div className={cn("mx-auto max-w-6xl px-6", pad)}>
        {eyebrow && <p className="text-[13px] font-extrabold uppercase tracking-[0.08em] text-primary">{eyebrow}</p>}
        {title && (
          <h2 id={headingId} className={cn("text-[28px] font-extrabold leading-tight tracking-[-0.02em] text-ink md:text-[32px]", !!eyebrow && "mt-2")}>
            {title}
          </h2>
        )}
        {children}
      </div>
    </section>
  );
}
