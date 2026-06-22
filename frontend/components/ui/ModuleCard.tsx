import { cn } from "@/lib/cn";
import { Badge, TagChip, type Status } from "./Badge";
import { Button } from "./Button";
import { ProgressBar } from "./ProgressBar";
import { PlayIcon, LockIcon, StarIcon } from "./icons";

export type ModuleCardState = "entitled" | "preview" | "locked";

export interface ModuleCardProps {
  title: string;
  levelLabel: string;
  state: ModuleCardState;
  durationLabel: string;
  rating?: number;
  learnersLabel?: string;
  /** 0..100 — shows a progress bar when set. */
  progress?: number;
  status?: Extract<Status, "in-progress" | "completed">;
  tags?: string[];
  ctaLabel: string;
  onCta?: () => void;
  className?: string;
}

const thumbBg: Record<ModuleCardState, string> = {
  entitled: "bg-[linear-gradient(135deg,#0050E6,#2A6BFF)]",
  preview: "bg-[linear-gradient(135deg,#64748B,#475569)]",
  locked: "bg-surface-2",
};

export function ModuleCard({
  title,
  levelLabel,
  state,
  durationLabel,
  rating,
  learnersLabel,
  progress,
  status,
  tags,
  ctaLabel,
  onCta,
  className,
}: ModuleCardProps) {
  return (
    <article
      className={cn(
        "flex flex-col overflow-hidden rounded-base border border-border bg-surface shadow-sm",
        className
      )}
    >
      {/* Thumbnail */}
      <div className={cn("relative flex aspect-video items-center justify-center", thumbBg[state])}>
        {state === "locked" ? (
          <span className="inline-flex h-[42px] w-[42px] items-center justify-center rounded-full border border-border bg-surface text-ink-subtle">
            <LockIcon size={18} />
          </span>
        ) : (
          <span className="inline-flex h-[42px] w-[42px] items-center justify-center rounded-full bg-white/95 text-primary">
            <PlayIcon size={18} />
          </span>
        )}

        {state === "preview" && (
          <span className="absolute left-2 top-2 rounded-sm bg-[#DCFCE7] px-2 py-1 text-[10.5px] font-bold text-[#166534]">
            Pratinjau gratis
          </span>
        )}

        <span
          className={cn(
            "absolute bottom-2 right-2 rounded-sm px-1.5 py-0.5 text-[11px] font-semibold",
            state === "locked" ? "bg-surface text-ink-muted" : "bg-black/55 text-white"
          )}
        >
          {durationLabel}
        </span>
      </div>

      {/* Body */}
      <div className="flex flex-col gap-2 p-3.5">
        <div className="flex items-center gap-1.5">
          <TagChip>{levelLabel}</TagChip>
          {status && <Badge status={status} className="px-2 py-0.5 text-[10.5px]" />}
        </div>

        <h4 className="text-[15px] font-bold leading-snug text-ink">{title}</h4>

        {rating != null ? (
          <div className="flex items-center gap-1.5 text-[11px] text-ink-muted">
            <span className="inline-flex items-center gap-1 font-bold text-accent">
              <StarIcon size={12} />
              {rating.toLocaleString("id-ID")}
            </span>
            {learnersLabel && (
              <>
                <span>·</span>
                <span>{learnersLabel}</span>
              </>
            )}
          </div>
        ) : tags && tags.length > 0 ? (
          <div className="flex flex-wrap gap-1.5">
            {tags.map((tag) => (
              <span key={tag} className="text-[10.5px] text-ink-subtle">
                #{tag}
              </span>
            ))}
          </div>
        ) : null}

        {progress != null && <ProgressBar value={progress} size="sm" />}

        {state === "locked" ? (
          <button
            type="button"
            onClick={onCta}
            className="mt-0.5 inline-flex w-full items-center justify-center gap-2 rounded-sm bg-info-soft px-3 py-2.5 text-[13px] font-bold text-info"
          >
            <LockIcon size={13} />
            {ctaLabel}
          </button>
        ) : (
          <Button
            variant={state === "entitled" ? "primary" : "secondary"}
            fullWidth
            onClick={onCta}
            className="mt-0.5"
          >
            {ctaLabel}
          </Button>
        )}
      </div>
    </article>
  );
}
