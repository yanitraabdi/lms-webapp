import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/cn";
import { CheckIcon, LockIcon, StarIcon } from "./icons";

export type Tier = "free" | "beginner" | "intermediate" | "advanced";
export type Status = "not-started" | "in-progress" | "completed" | "locked";
export type Promo = "popular" | "new";
export type Tone = "neutral" | "success" | "warning" | "danger" | "info";

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tier?: Tier;
  status?: Status;
  promo?: Promo;
  tone?: Tone;
}

const pill = "inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold border";

// Tier & status use fixed semantic colors (per the Design Foundation note) so they
// stay consistent across the product, independent of the --ds theme tokens.
const tierStyles: Record<Tier, string> = {
  free: "text-[#57534E] bg-[#F0EFEC] border-[#E2E0DB]",
  beginner: "text-[#166534] bg-[#DCFCE7] border-[#BBF7D0]",
  intermediate: "text-[#1D4ED8] bg-[#DBEAFE] border-[#BFDBFE]",
  advanced: "text-[#6D28D9] bg-[#EDE4FF] border-[#DDD0FB]",
};
const tierLabels: Record<Tier, string> = {
  free: "Gratis",
  beginner: "Basic",
  intermediate: "Intermediate",
  advanced: "Advanced",
};

const statusStyles: Record<Status, string> = {
  "not-started": "text-[#57534E] bg-[#F0EFEC] border-transparent",
  "in-progress": "text-[#92400E] bg-[#FEF3C7] border-transparent",
  completed: "text-[#166534] bg-[#DCFCE7] border-transparent",
  locked: "text-[#64748B] bg-[#EEF3F9] border-transparent",
};
const statusLabels: Record<Status, string> = {
  "not-started": "Belum dimulai",
  "in-progress": "Sedang berjalan",
  completed: "Selesai",
  locked: "Terkunci",
};
const statusDot: Record<Exclude<Status, "completed" | "locked">, string> = {
  "not-started": "bg-[#A8A29E]",
  "in-progress": "bg-[#D97706]",
};

const toneStyles: Record<Tone, string> = {
  neutral: "text-ink-muted bg-surface-2 border-transparent",
  success: "text-success bg-success-soft border-transparent",
  warning: "text-warning bg-warning-soft border-transparent",
  danger: "text-danger bg-danger-soft border-transparent",
  info: "text-info bg-info-soft border-transparent",
};

export function Badge({ tier, status, promo, tone, className, children, ...props }: BadgeProps) {
  let palette = "text-ink-muted bg-surface-2 border-transparent";
  let label: ReactNode = children;
  let leading: ReactNode = null;

  if (status) {
    palette = statusStyles[status];
    label ??= statusLabels[status];
    if (status === "completed") leading = <CheckIcon size={13} strokeWidth={3} />;
    else if (status === "locked") leading = <LockIcon size={12} />;
    else leading = <span className={cn("h-2 w-2 rounded-full", statusDot[status])} />;
  } else if (promo === "popular") {
    palette = "text-[#11253F] bg-accent-strong border-transparent";
    label ??= "Paling diminati";
    leading = <StarIcon size={11} className="text-[#11253F]" />;
  } else if (promo === "new") {
    palette = "text-accent bg-accent-soft border-transparent";
    label ??= "Baru";
  } else if (tier) {
    palette = tierStyles[tier];
    label ??= tierLabels[tier];
  } else if (tone) {
    palette = toneStyles[tone];
  }

  return (
    <span className={cn(pill, palette, className)} {...props}>
      {leading}
      {label}
    </span>
  );
}

/** Small square label chip (e.g. a level tag "Basic" on a card). */
export function TagChip({ className, ...props }: HTMLAttributes<HTMLSpanElement>) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-sm bg-surface-2 px-2 py-1 text-[11px] font-semibold text-ink-muted",
        className
      )}
      {...props}
    />
  );
}
