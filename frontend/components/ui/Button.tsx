import { forwardRef, type ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/cn";
import { Spinner } from "./Spinner";

type Variant = "primary" | "secondary" | "ghost" | "danger" | "neutral";
type Size = "sm" | "md" | "lg";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
  fullWidth?: boolean;
}

// Colors map to the Design Foundation tokens (Tailwind v4 utilities backed by the
// @theme block in globals.css). Focus ring uses the `focus` token.
const base =
  "inline-flex items-center justify-center font-bold rounded-base " +
  "transition-colors select-none outline-none whitespace-nowrap " +
  "focus-visible:ring-[3px] focus-visible:ring-primary-soft focus-visible:ring-offset-0 " +
  "disabled:opacity-50 disabled:pointer-events-none";

const variants: Record<Variant, string> = {
  primary: "bg-primary text-primary-ink hover:bg-primary-hover",
  // Design "Sekunder": white surface, primary text + primary outline.
  secondary: "bg-surface text-primary border-[1.5px] border-primary hover:bg-primary-soft",
  ghost: "bg-transparent text-primary hover:bg-surface-2",
  danger: "bg-danger text-white hover:opacity-90",
  // Neutral outline (e.g. modal "cancel", reset filters).
  neutral: "bg-surface text-ink border border-border hover:bg-surface-2",
};

const sizes: Record<Size, string> = {
  sm: "h-8 px-3 text-[13px] gap-1.5",
  md: "h-10 px-[18px] text-sm gap-2",
  lg: "h-12 px-6 text-[15px] gap-2",
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  {
    variant = "primary",
    size = "md",
    loading = false,
    fullWidth = false,
    disabled,
    className,
    type = "button",
    children,
    ...props
  },
  ref
) {
  return (
    <button
      ref={ref}
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={cn(base, variants[variant], sizes[size], fullWidth && "w-full", className)}
      {...props}
    >
      {loading && <Spinner size={size === "lg" ? 18 : 15} />}
      {children}
    </button>
  );
});
