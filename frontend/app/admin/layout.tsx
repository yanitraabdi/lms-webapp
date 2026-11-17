"use client";

import { useEffect, type ReactNode } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Spinner, UiLocaleProvider } from "@/components/ui";
import { isAdminRole } from "@/lib/admin";
import { cn } from "@/lib/cn";
import { Logo } from "@/components/Logo";

// INVERTA nav. Curriculum/Modules/Pricing belong to the archived catalog product and stay
// reachable by URL, but are unlinked here (KAK §9.15).
const NAV = [
  { href: "/admin", label: "Analytics", exact: true },
  { href: "/admin/programs", label: "Programs" },
  { href: "/admin/questions", label: "Question bank" },
  { href: "/admin/enrollments", label: "Enrollments" },
  { href: "/admin/attempts", label: "Attempts" },
  { href: "/admin/users", label: "Users" },
];

// The admin is English (DECISIONS 2026-11-17); learner-facing pages stay Bahasa Indonesia.
export default function AdminLayout({ children }: { children: ReactNode }) {
  return (
    <UiLocaleProvider value="en">
      <div lang="en">
        <AdminShell>{children}</AdminShell>
      </div>
    </UiLocaleProvider>
  );
}

function AdminShell({ children }: { children: ReactNode }) {
  const { status, user, logout } = useAuth();
  const router = useRouter();
  const path = usePathname();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login?next=/admin");
  }, [status, router]);

  if (status !== "authenticated") {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  if (!isAdminRole(user?.role)) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-bg px-6 text-center">
        <h1 className="text-xl font-extrabold">Access denied</h1>
        <p className="text-sm text-ink-muted">This page is for admins only.</p>
        <Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Go to dashboard</Link>
      </div>
    );
  }

  const active = NAV.find((n) => (n.exact ? path === n.href : path === n.href || path.startsWith(n.href + "/"))) ?? NAV[0];

  return (
    <div className="grid min-h-screen grid-cols-1 bg-bg md:grid-cols-[220px_1fr]">
      <aside className="hidden flex-col gap-1.5 bg-ink p-3.5 md:flex">
        <div className="flex items-center gap-2.5 px-2 pb-4 pt-1.5">
          <Logo variant="white" height="h-6" />
          <div className="text-[10px] text-white/55">Admin</div>
        </div>
        {NAV.map((n) => {
          const isActive = n === active;
          return (
            <Link
              key={n.href}
              href={n.href}
              className={cn(
                "rounded-lg px-3 py-2.5 text-[13.5px] font-bold transition-colors",
                isActive ? "bg-white/10 text-white" : "text-white/70 hover:bg-white/5"
              )}
            >
              {n.label}
            </Link>
          );
        })}
        <div className="flex-1" />
        <Link href="/app/account" className="rounded-lg px-3 py-2 text-[12.5px] font-semibold text-white/70 hover:bg-white/5">
          Account &amp; password
        </Link>
        <button
          type="button"
          onClick={logout}
          className="rounded-lg bg-white/[0.06] px-3 py-2.5 text-left text-[12.5px] font-semibold text-white/80 hover:bg-white/10"
        >
          Sign out
        </button>
      </aside>

      <div className="flex min-w-0 flex-col bg-surface">
        <header className="flex h-[58px] items-center justify-between border-b border-border bg-surface px-6">
          <span className="text-base font-extrabold">{active.label}</span>
          <Link href="/" className="text-[12.5px] font-bold text-ink-muted hover:text-ink">View site</Link>
        </header>
        <div className="mx-auto w-full max-w-6xl px-6 py-8">{children}</div>
      </div>
    </div>
  );
}
