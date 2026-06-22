import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { AlertCircleIcon } from "@/components/ui";

export function AuthCard({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={cn("flex flex-col gap-[18px] rounded-lg bg-surface p-[30px] shadow-sm", className)}>
      {children}
    </div>
  );
}

export function LogoMark() {
  return (
    <span className="flex h-[38px] w-[38px] items-center justify-center rounded-base bg-primary text-lg font-extrabold text-primary-ink">
      A
    </span>
  );
}

export function AuthDivider() {
  return (
    <div className="flex items-center gap-3">
      <div className="h-px flex-1 bg-border" />
      <span className="text-xs text-ink-subtle">atau</span>
      <div className="h-px flex-1 bg-border" />
    </div>
  );
}

export function AuthError({ message }: { message: string }) {
  return (
    <div className="flex items-center gap-2.5 rounded-sm border border-[#F3C9C9] bg-danger-soft px-3 py-2.5">
      <AlertCircleIcon size={17} strokeWidth={2.2} className="shrink-0 text-danger" />
      <span className="text-[12.5px] font-semibold text-danger">{message}</span>
    </div>
  );
}
