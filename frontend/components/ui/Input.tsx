import { forwardRef, type InputHTMLAttributes } from "react";
import { cn } from "@/lib/cn";

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { invalid = false, className, ...props },
  ref
) {
  return (
    <input
      ref={ref}
      aria-invalid={invalid || undefined}
      className={cn(
        "w-full rounded-sm border bg-surface px-3 py-2.5 text-sm text-ink",
        "placeholder:text-ink-subtle outline-none transition-colors focus:ring-[3px]",
        invalid
          ? "border-danger focus:border-danger focus:ring-danger-soft"
          : "border-border focus:border-primary focus:ring-primary-soft",
        "disabled:cursor-not-allowed disabled:bg-surface-2 disabled:text-ink-subtle",
        className
      )}
      {...props}
    />
  );
});
