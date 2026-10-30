import type { ReactNode } from "react";
import Link from "next/link";
import { cn } from "@/lib/cn";

export const SHEET_CLASSES =
  "rounded-lg bg-surface px-5 py-8 sm:px-11 sm:py-10 shadow-[0_1px_0_var(--color-border),0_10px_30px_-18px_rgba(17,37,63,0.2)]";

/** Course-book page: a white sheet on the tinted background. No overflow clipping (sticky children). */
export function SheetPage({ children, wide = false }: { children: ReactNode; wide?: boolean }) {
  return (
    <div className="min-h-[calc(100vh-58px)] bg-bg px-3 py-4 sm:px-7 sm:py-8">
      <div
        className={cn(
          "mx-auto w-full",
          SHEET_CLASSES,
          wide ? "max-w-6xl" : "max-w-[760px]"
        )}
      >
        {children}
      </div>
    </div>
  );
}

export function RunningHead({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <p className={cn("text-[13.5px] font-semibold tabular-nums text-ink-muted", className)}>{children}</p>
  );
}

export interface PageTitleProps {
  children: ReactNode;
  id?: string;
  tabIndex?: number;
  className?: string;
  /** "page" = display size; "compact" = 30px (auth, admin). */
  size?: "page" | "compact";
  as?: "h1" | "h2";
}

export function PageTitle({ children, id, tabIndex, className, size = "page", as: Tag = "h1" }: PageTitleProps) {
  return (
    <Tag
      id={id}
      tabIndex={tabIndex}
      className={cn(
        size === "page"
          ? "text-[clamp(32px,5vw,46px)] leading-[1.08]"
          : "text-[30px] leading-tight",
        "mt-2 text-balance font-extrabold tracking-[-0.025em] text-ink",
        tabIndex !== undefined && "focus:outline-none",
        className
      )}
    >
      {children}
    </Tag>
  );
}

export function Lead({ children }: { children: ReactNode }) {
  return <p className="mt-4 max-w-[58ch] text-[18px] leading-relaxed text-ink-muted">{children}</p>;
}

export function Chapter({
  title,
  intro,
  id,
  children,
  className,
}: {
  title: ReactNode;
  intro?: ReactNode;
  id?: string;
  children: ReactNode;
  className?: string;
}) {
  const headingId = id ? `${id}-heading` : undefined;
  return (
    <section id={id} aria-labelledby={headingId} className={cn("mt-12 scroll-mt-20 border-t border-ink pt-4", className)}>
      <h2 id={headingId} className="text-[26px] font-extrabold tracking-[-0.015em] text-ink">
        {title}
      </h2>
      {intro && <p className="mt-2 max-w-[60ch] text-ink-muted">{intro}</p>}
      {children}
    </section>
  );
}

export function Footnote({ children }: { children: ReactNode }) {
  return <p className="mt-4 max-w-[66ch] text-[13px] text-ink-muted">{children}</p>;
}

export function Toc({ children, label }: { children: ReactNode; label: string }) {
  return (
    <ol aria-label={label} className="mt-4 flex flex-col">
      {children}
    </ol>
  );
}

export interface TocItemProps {
  n: number;
  title: string;
  kind?: string;
  meta: ReactNode;
  note?: ReactNode;
  state: "done" | "current" | "open" | "locked";
  href?: string;
}

export function TocItem({ n, title, kind, meta, note, state, href }: TocItemProps) {
  const locked = state === "locked";
  const current = state === "current";
  const titleCls = cn("min-w-0 break-words", locked ? "font-semibold text-ink-muted" : "font-bold text-ink");
  return (
    <li
      aria-current={current ? "step" : undefined}
      className={cn(
        "grid grid-cols-[2.2em_minmax(0,1fr)_fit-content(40%)] items-baseline gap-2.5 py-[11px]",
        current ? "-mx-3 rounded-sm border-b border-transparent bg-primary-soft px-3" : "border-b border-surface-2"
      )}
    >
      <span
        className={cn(
          "font-bold tabular-nums",
          state === "done" ? "text-success" : current ? "text-primary" : "text-ink-subtle"
        )}
      >
        {state === "done" ? <span aria-hidden="true">✓</span> : n}
        {state === "done" && <span className="sr-only">{n}, selesai</span>}
      </span>
      <span className="min-w-0 break-words">
        {href && !locked ? (
          <Link
            href={href}
            className={cn(titleCls, "rounded-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus")}
          >
            {title}
          </Link>
        ) : (
          <span className={titleCls}>{title}</span>
        )}
        {kind && (
          <span className="ml-2 rounded-full bg-accent-soft px-2 text-[11.5px] font-bold text-accent">{kind}</span>
        )}
        {note && <span className="mt-0.5 block text-[13px] font-medium text-ink-muted">{note}</span>}
      </span>
      <span className="text-right text-[13px] tabular-nums text-ink-muted">{meta}</span>
    </li>
  );
}
