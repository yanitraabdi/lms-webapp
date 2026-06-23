"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { NotificationBell } from "@/components/app/NotificationBell";
import { cn } from "@/lib/cn";

const links: Array<{ href: string; label: string; tour?: string }> = [
  { href: "/app/dashboard", label: "Dasbor" },
  { href: "/catalog", label: "Katalog", tour: "nav-catalog" },
  { href: "/app/certificates", label: "Sertifikat", tour: "nav-certificates" },
  { href: "/app/account", label: "Langganan" },
];

export function AppHeader() {
  const path = usePathname();
  const { user } = useAuth();
  const initials = (user?.name ?? "?")
    .split(" ")
    .map((s) => s[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-surface/90 backdrop-blur">
      <div className="mx-auto flex h-[58px] max-w-6xl items-center justify-between gap-4 px-6">
        <div className="flex items-center gap-6">
          <Link href="/app/dashboard" className="flex items-center gap-2.5">
            <span className="flex h-[30px] w-[30px] items-center justify-center rounded-lg bg-primary text-[15px] font-extrabold text-primary-ink">A</span>
            <span className="hidden text-sm font-extrabold sm:inline">AI Productivity Academy</span>
          </Link>
          <nav className="flex items-center gap-1">
            {links.map((l) => {
              const active = path === l.href || (l.href !== "/catalog" && path.startsWith(l.href));
              return (
                <Link
                  key={l.href}
                  href={l.href}
                  data-tour={l.tour}
                  className={cn(
                    "rounded-md px-3 py-1.5 text-[13px] font-bold transition-colors",
                    active ? "bg-primary-soft text-primary" : "text-ink-muted hover:text-ink"
                  )}
                >
                  {l.label}
                </Link>
              );
            })}
          </nav>
        </div>
        <div className="flex items-center gap-1.5">
          <NotificationBell />
          <span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary-soft text-[13px] font-extrabold text-primary">
            {initials}
          </span>
        </div>
      </div>
    </header>
  );
}
