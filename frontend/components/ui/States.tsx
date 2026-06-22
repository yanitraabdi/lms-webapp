import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { SearchIcon, AlertTriangleIcon } from "./icons";

export interface StatePanelProps {
  icon?: ReactNode;
  title: ReactNode;
  message?: ReactNode;
  /** Action element (e.g. a Button). */
  action?: ReactNode;
  className?: string;
}

function Panel({ icon, title, message, action, iconClass, className }: StatePanelProps & { iconClass: string }) {
  return (
    <div
      className={cn(
        "flex min-h-[172px] flex-col items-center justify-center gap-2.5 rounded-base border border-border p-4 text-center",
        className
      )}
    >
      <span className={cn("inline-flex h-12 w-12 items-center justify-center rounded-xl", iconClass)}>{icon}</span>
      <div className="text-sm font-bold text-ink">{title}</div>
      {message && <div className="max-w-xs text-xs leading-relaxed text-ink-muted">{message}</div>}
      {action}
    </div>
  );
}

export function EmptyState({ icon, ...props }: StatePanelProps) {
  return <Panel {...props} icon={icon ?? <SearchIcon size={22} />} iconClass="bg-surface-2 text-ink-subtle" />;
}

export function ErrorState({ icon, ...props }: StatePanelProps) {
  return <Panel {...props} icon={icon ?? <AlertTriangleIcon size={22} />} iconClass="bg-danger-soft text-danger" />;
}
