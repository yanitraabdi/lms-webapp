"use client";

import { useEffect, useRef, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { XIcon } from "./icons";

export interface ModalProps {
  open: boolean;
  onClose: () => void;
  title?: ReactNode;
  icon?: ReactNode;
  children?: ReactNode;
  /** Footer actions row (e.g. two Buttons). */
  footer?: ReactNode;
  className?: string;
}

/** Open modals, innermost last: Escape closes only the top one, not every modal it is nested in. */
const openStack: symbol[] = [];

export function Modal({ open, onClose, title, icon, children, footer, className }: ModalProps) {
  // Read through a ref so a new onClose each render does not re-register (and re-stack) the modal.
  const onCloseRef = useRef(onClose);
  useEffect(() => { onCloseRef.current = onClose; });

  useEffect(() => {
    if (!open) return;
    const id = Symbol("modal");
    openStack.push(id);
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" && openStack[openStack.length - 1] === id) onCloseRef.current();
    };
    document.addEventListener("keydown", onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      openStack.splice(openStack.indexOf(id), 1);
      document.body.style.overflow = prev;
    };
  }, [open]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-[rgba(17,37,63,0.34)] p-4"
      onClick={onClose}
      role="presentation"
    >
      <div
        role="dialog"
        aria-modal="true"
        className={cn("w-full max-w-sm rounded-base bg-surface p-5 shadow-lg", className)}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between">
          {icon && (
            <span className="inline-flex h-9 w-9 items-center justify-center rounded-base bg-primary-soft text-primary">
              {icon}
            </span>
          )}
          <button
            type="button"
            onClick={onClose}
            aria-label="Tutup"
            className="ml-auto rounded-sm p-1 text-ink-subtle hover:bg-surface-2"
          >
            <XIcon size={18} />
          </button>
        </div>
        {title && <h2 className="mt-2 text-lg font-bold text-ink">{title}</h2>}
        {children && <div className="mt-2 text-sm leading-relaxed text-ink-muted">{children}</div>}
        {footer && <div className="mt-4 flex gap-2.5">{footer}</div>}
      </div>
    </div>
  );
}
