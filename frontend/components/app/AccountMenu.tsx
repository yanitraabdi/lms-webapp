"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/components/auth/AuthProvider";

/**
 * Account menu. The learner app had no sign-out control at all — "Keluar" existed only in the
 * admin layout — so a learner could sign in and never sign out. AuthProvider.logout() was already
 * wired and working; nothing rendered it.
 */
export function AccountMenu({ initials }: { initials: string }) {
  const { user, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const wrap = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onPointer = (e: MouseEvent) => {
      if (!wrap.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={wrap} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Menu akun"
        className="flex h-8 w-8 items-center justify-center rounded-full bg-primary-soft text-[13px] font-extrabold text-primary transition-colors hover:bg-primary/15 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2"
      >
        {initials}
      </button>

      {open && (
        <div
          role="menu"
          className="absolute right-0 top-[calc(100%+8px)] z-50 w-60 overflow-hidden rounded-base border border-border bg-surface shadow-lg"
        >
          <div className="border-b border-border px-3.5 py-3">
            <p className="truncate text-[13.5px] font-bold text-ink">{user?.name}</p>
            <p className="truncate text-[12px] text-ink-muted">{user?.email}</p>
          </div>
          <Link
            href="/app/account"
            role="menuitem"
            onClick={() => setOpen(false)}
            className="block w-full px-3.5 py-2.5 text-left text-[13px] font-semibold text-ink transition-colors hover:bg-ink/5 focus:outline-none focus-visible:bg-ink/5"
          >
            Profil & tagihan
          </Link>
          <button
            type="button"
            role="menuitem"
            onClick={() => { setOpen(false); void logout(); }}
            className="block w-full border-t border-border px-3.5 py-2.5 text-left text-[13px] font-semibold text-ink transition-colors hover:bg-ink/5 focus:outline-none focus-visible:bg-ink/5"
          >
            Keluar
          </button>
        </div>
      )}
    </div>
  );
}

