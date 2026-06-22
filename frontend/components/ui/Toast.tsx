import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { AlertCircleIcon, AlertTriangleIcon, CheckCircleIcon, InfoIcon, XIcon } from "./icons";

export type ToastTone = "success" | "warning" | "danger" | "info";

export interface ToastProps {
  tone?: ToastTone;
  title: ReactNode;
  description?: ReactNode;
  /** Override the default tone icon. */
  icon?: ReactNode;
  onClose?: () => void;
  className?: string;
}

const toneBar: Record<ToastTone, string> = {
  success: "bg-success",
  warning: "bg-accent-strong",
  danger: "bg-danger",
  info: "bg-primary",
};
const toneIcon: Record<ToastTone, string> = {
  success: "text-success",
  warning: "text-accent",
  danger: "text-danger",
  info: "text-primary",
};
const defaultIcon: Record<ToastTone, ReactNode> = {
  success: <CheckCircleIcon size={18} />,
  warning: <AlertTriangleIcon size={18} />,
  danger: <AlertCircleIcon size={18} />,
  info: <InfoIcon size={18} />,
};

export function Toast({ tone = "info", title, description, icon, onClose, className }: ToastProps) {
  return (
    <div
      role="status"
      className={cn(
        "relative flex items-start gap-3 overflow-hidden rounded-sm border border-border bg-surface p-3.5 pl-4 shadow-sm",
        className
      )}
    >
      <span className={cn("absolute left-0 top-0 h-full w-[3px]", toneBar[tone])} />
      <span className={cn("mt-0.5 shrink-0", toneIcon[tone])}>{icon ?? defaultIcon[tone]}</span>
      <div className="flex-1">
        <div className="text-[13px] font-bold text-ink">{title}</div>
        {description && <div className="text-[11.5px] text-ink-muted">{description}</div>}
      </div>
      {onClose && (
        <button type="button" onClick={onClose} aria-label="Tutup" className="text-ink-subtle hover:text-ink">
          <XIcon size={15} />
        </button>
      )}
    </div>
  );
}
