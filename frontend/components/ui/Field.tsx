import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { AlertCircleIcon } from "./icons";

export interface FieldProps {
  label?: ReactNode;
  /** id of the control this label points at. */
  htmlFor?: string;
  hint?: ReactNode;
  error?: ReactNode;
  required?: boolean;
  className?: string;
  children: ReactNode;
}

/** Label + help-text/error wrapper around a form control. */
export function Field({ label, htmlFor, hint, error, required, className, children }: FieldProps) {
  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      {label && (
        <label htmlFor={htmlFor} className={cn("text-[13px] font-semibold", error ? "text-danger" : "text-ink")}>
          {label}
          {required && <span className="text-danger"> *</span>}
        </label>
      )}
      {children}
      {error ? (
        <span className="inline-flex items-center gap-1.5 text-xs text-danger">
          <AlertCircleIcon size={13} strokeWidth={2.2} />
          {error}
        </span>
      ) : hint ? (
        <span className="text-xs text-ink-muted">{hint}</span>
      ) : null}
    </div>
  );
}
