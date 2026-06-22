import { forwardRef, type InputHTMLAttributes, type ReactNode } from "react";
import { cn } from "@/lib/cn";

export interface RadioProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "type"> {
  label?: ReactNode;
}

export const Radio = forwardRef<HTMLInputElement, RadioProps>(function Radio(
  { label, className, ...props },
  ref
) {
  return (
    <label className={cn("inline-flex cursor-pointer select-none items-center gap-2 text-sm text-ink", className)}>
      <span className="relative inline-flex h-[18px] w-[18px] shrink-0">
        <input
          ref={ref}
          type="radio"
          className="peer absolute inset-0 m-0 cursor-pointer appearance-none rounded-full border-2 border-border bg-surface outline-none transition-colors checked:border-primary focus-visible:ring-[3px] focus-visible:ring-primary-soft"
          {...props}
        />
        <span className="pointer-events-none absolute inset-0 m-auto hidden h-2 w-2 rounded-full bg-primary peer-checked:block" />
      </span>
      {label}
    </label>
  );
});
