import { forwardRef, type InputHTMLAttributes, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { CheckIcon } from "./icons";

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "type"> {
  label?: ReactNode;
}

export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(function Checkbox(
  { label, className, ...props },
  ref
) {
  return (
    <label className={cn("inline-flex cursor-pointer select-none items-center gap-2 text-sm text-ink", className)}>
      <span className="relative inline-flex h-[18px] w-[18px] shrink-0">
        <input
          ref={ref}
          type="checkbox"
          className="peer absolute inset-0 m-0 cursor-pointer appearance-none rounded-[5px] border border-border bg-surface outline-none transition-colors checked:border-primary checked:bg-primary focus-visible:ring-[3px] focus-visible:ring-primary-soft"
          {...props}
        />
        <CheckIcon
          size={12}
          strokeWidth={3}
          className="pointer-events-none absolute inset-0 m-auto hidden text-white peer-checked:block"
        />
      </span>
      {label}
    </label>
  );
});
