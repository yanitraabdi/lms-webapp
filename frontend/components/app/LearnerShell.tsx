"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { NotificationBell, useUnreadCount } from "@/components/app/NotificationBell";
import { AccountMenu } from "@/components/app/AccountMenu";
import { WrenchIcon, XIcon } from "@/components/ui";
import { isAdminRole } from "@/lib/admin";
import { cn } from "@/lib/cn";
import { Logo } from "@/components/Logo";

// INVERTA nav. Catalog/subscription belong to the archived product and are unlinked (KAK §9.15).
const links: Array<{ href: string; label: string; tour?: string }> = [
  { href: "/app/dashboard", label: "Dasbor", tour: "nav-program" },
  { href: "/app/certificates", label: "Sertifikat", tour: "nav-certificates" },
  { href: "/app/notifications", label: "Notifikasi" },
  { href: "/app/account", label: "Profil & tagihan" },
  { href: "/help", label: "Bantuan" },
];

const item = "flex items-center gap-2 rounded-sm px-3 py-2 text-[13.5px] font-bold transition-colors";

/**
 * Learner sidebar (md and up) or top bar + drawer (phones), wrapping a learner page's content.
 * The drawer is a native modal <dialog>: the browser traps focus, closes on Escape and hands
 * focus back to the menu button, which the house Modal does not do.
 */
export function LearnerShell({ children }: { children: ReactNode }) {
  const path = usePathname();
  const { user } = useAuth();
  const unread = useUnreadCount();
  const drawer = useRef<HTMLDialogElement>(null);
  const [open, setOpen] = useState(false);
  const initials = (user?.name ?? "?")
    .split(" ")
    .map((s) => s[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();

  useEffect(() => drawer.current?.close(), [path]);

  const panel = (
    <div className="flex h-full flex-col">
      <Link href="/app/dashboard" className="flex h-[58px] shrink-0 items-center px-5">
        <Logo />
      </Link>
      <nav aria-label="Menu utama" className="flex flex-col gap-0.5 px-3 py-2">
        {links.map((l) => {
          const active = path === l.href || path.startsWith(l.href) || (l.href === "/app/dashboard" && path.startsWith("/app/program/"));
          return (
            <Link
              key={l.href}
              href={l.href}
              data-tour={l.tour}
              aria-current={active ? "page" : undefined}
              className={cn(item, active ? "bg-primary-soft text-primary" : "text-ink-muted hover:bg-surface-2 hover:text-ink")}
            >
              {l.label}
              {l.href === "/app/notifications" && unread > 0 && (
                <>
                  <span aria-hidden className="ml-auto inline-flex min-w-[18px] items-center justify-center rounded-full bg-danger px-1.5 text-[10.5px] font-extrabold leading-[17px] text-white">
                    {unread > 9 ? "9+" : unread}
                  </span>
                  <span className="sr-only">, {unread} belum dibaca</span>
                </>
              )}
            </Link>
          );
        })}
        {isAdminRole(user?.role) && (
          <Link href="/admin" className={cn(item, "text-ink-muted hover:bg-surface-2 hover:text-ink")}>
            <WrenchIcon size={14} /> Admin
          </Link>
        )}
      </nav>
      <div className="mt-auto flex min-w-0 items-center gap-2.5 border-t border-border px-4 py-3">
        <AccountMenu initials={initials} up />
        <span className="min-w-0 truncate text-[13px] font-bold text-ink">{user?.name}</span>
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-bg md:grid md:grid-cols-[220px_minmax(0,1fr)]">
      <aside className="sticky top-0 hidden h-screen border-r border-border bg-surface md:block">{panel}</aside>

      <header className="sticky top-0 z-40 flex h-[58px] items-center justify-between gap-3 border-b border-border bg-surface px-4 md:hidden">
        <Link href="/app/dashboard" className="flex shrink-0 items-center">
          <Logo />
        </Link>
        <div className="flex shrink-0 items-center gap-1.5">
          <NotificationBell />
          <button
            type="button"
            aria-label="Buka menu"
            aria-expanded={open}
            aria-controls="learner-drawer"
            data-tour="nav-menu"
            onClick={() => { drawer.current?.showModal(); setOpen(true); }}
            className="inline-flex h-9 w-9 items-center justify-center rounded-full text-ink-muted hover:bg-surface-2 hover:text-ink"
          >
            <svg aria-hidden width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
              <path d="M4 6h16M4 12h16M4 18h16" />
            </svg>
          </button>
        </div>
      </header>

      <dialog
        id="learner-drawer"
        ref={drawer}
        aria-label="Menu"
        onClose={() => setOpen(false)}
        // The panel fills the dialog, so a click landing on the dialog itself is on the backdrop.
        onClick={(e) => e.target === e.currentTarget && e.currentTarget.close()}
        className="m-0 h-full max-h-none w-[260px] max-w-[85vw] bg-surface p-0 backdrop:bg-ink/40"
      >
        <button
          type="button"
          aria-label="Tutup menu"
          onClick={() => drawer.current?.close()}
          className="absolute right-3 top-3 inline-flex h-9 w-9 items-center justify-center rounded-full text-ink-muted hover:bg-surface-2 hover:text-ink"
        >
          <XIcon size={18} />
        </button>
        {panel}
      </dialog>

      <main className="min-w-0">{children}</main>
    </div>
  );
}
