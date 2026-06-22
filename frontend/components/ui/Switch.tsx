"use client";

import { cn } from "@/lib/cn";

export interface SwitchProps {
  checked: boolean;
  onCheckedChange?: (checked: boolean) => void;
  disabled?: boolean;
  id?: string;
  "aria-label"?: string;
  className?: string;
}

/** Controlled toggle (role="switch"). Parent owns the boolean state. */
export function Switch({ checked, onCheckedChange, disabled, className, ...props }: SwitchProps) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => onCheckedChange?.(!checked)}
      className={cn(
        "relative inline-flex h-[22px] w-[38px] shrink-0 items-center rounded-full outline-none transition-colors",
        "focus-visible:ring-[3px] focus-visible:ring-primary-soft disabled:pointer-events-none disabled:opacity-50",
        checked ? "bg-primary" : "border border-border bg-surface-2",
        className
      )}
      {...props}
    >
      <span
        className={cn(
          "inline-block h-[18px] w-[18px] rounded-full bg-white shadow-sm transition-transform",
          checked ? "translate-x-[18px]" : "translate-x-[2px]"
        )}
      />
    </button>
  );
}
