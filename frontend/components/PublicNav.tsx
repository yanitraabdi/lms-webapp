"use client";

import Link from "next/link";
import { cn } from "@/lib/cn";
import { useAuth } from "@/components/auth/AuthProvider";

const links = [
  { href: "/catalog", label: "Katalog" },
  { href: "/pricing", label: "Harga" },
  { href: "/for-business", label: "Untuk Bisnis" },
  { href: "/how-it-works", label: "Cara Kerja" },
];

// CTAs are anchors styled as buttons (avoid invalid <a><button> nesting).
const ctaBase =
  "inline-flex h-9 items-center justify-center rounded-sm px-3.5 text-[13px] font-bold transition-colors";

export function PublicNav({ className }: { className?: string }) {
  const { status, user } = useAuth();
  const authed = status === "authenticated";
  const initials = (user?.name ?? "?")
    .split(" ")
    .map((s) => s[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();

  return (
    <header className={cn("sticky top-0 z-40 border-b border-border bg-surface/90 backdrop-blur", className)}>
      <nav className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-6 py-3">
        <Link href="/" className="flex items-center gap-2.5">
          <span className="flex h-7 w-7 items-center justify-center rounded-[7px] bg-primary text-sm font-extrabold text-primary-ink">
            A
          </span>
          <span className="text-sm font-bold text-ink">AI Productivity Academy</span>
        </Link>

        <div className="hidden items-center gap-5 md:flex">
          {links.map((l) => (
            <Link key={l.href} href={l.href} className="text-[13px] text-ink-muted transition-colors hover:text-ink">
              {l.label}
            </Link>
          ))}
        </div>

        {/* Auth-aware: signed-in users get a way back into the app; visitors get sign-in CTAs. */}
        {authed ? (
          <div className="flex items-center gap-2">
            <Link href="/app/dashboard" className={cn(ctaBase, "text-ink hover:bg-surface-2")}>
              Dasbor
            </Link>
            <Link
              href="/app/account"
              aria-label="Akun saya"
              title={user?.name ?? "Akun saya"}
              className="flex h-8 w-8 items-center justify-center rounded-full bg-primary-soft text-[13px] font-extrabold text-primary"
            >
              {initials}
            </Link>
          </div>
        ) : (
          <div className="flex items-center gap-2">
            <Link href="/login" className={cn(ctaBase, "text-ink hover:bg-surface-2")}>
              Masuk
            </Link>
            <Link href="/register" className={cn(ctaBase, "bg-primary text-primary-ink hover:bg-primary-hover")}>
              Daftar gratis
            </Link>
          </div>
        )}
      </nav>
    </header>
  );
}
