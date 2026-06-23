"use client";

import { useEffect, type ReactNode } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Spinner } from "@/components/ui";
import { isAdminRole } from "@/lib/admin";
import { cn } from "@/lib/cn";

const NAV = [
  { href: "/admin", label: "Analitik", exact: true },
  { href: "/admin/curriculum", label: "Kurikulum" },
  { href: "/admin/modules", label: "Modul" },
  { href: "/admin/users", label: "Pengguna" },
  { href: "/admin/pricing", label: "Harga" },
];

export default function AdminLayout({ children }: { children: ReactNode }) {
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
        <h1 className="text-xl font-extrabold">Akses ditolak</h1>
        <p className="text-sm text-ink-muted">Halaman ini hanya untuk admin.</p>
        <Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Ke dasbor</Link>
      </div>
    );
  }

  const active = NAV.find((n) => (n.exact ? path === n.href : path === n.href || path.startsWith(n.href + "/"))) ?? NAV[0];

  return (
    <div className="grid min-h-screen grid-cols-1 bg-bg md:grid-cols-[220px_1fr]">
      <aside className="hidden flex-col gap-1.5 bg-ink p-3.5 md:flex">
        <div className="flex items-center gap-2.5 px-2 pb-4 pt-1.5">
          <span className="flex h-[30px] w-[30px] items-center justify-center rounded-lg bg-primary text-[15px] font-extrabold text-primary-ink">A</span>
          <div className="leading-tight">
            <div className="text-[13px] font-extrabold text-white">Academy</div>
            <div className="text-[10px] text-white/55">Admin</div>
          </div>
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
        <button
          type="button"
          onClick={logout}
          className="rounded-lg bg-white/[0.06] px-3 py-2.5 text-left text-[12.5px] font-semibold text-white/80 hover:bg-white/10"
        >
          Keluar
        </button>
      </aside>

      <div className="flex flex-col">
        <header className="flex h-[58px] items-center justify-between border-b border-border bg-surface px-6">
          <span className="text-base font-extrabold">{active.label}</span>
          <Link href="/" className="text-[12.5px] font-bold text-ink-muted hover:text-ink">Lihat situs →</Link>
        </header>
        <div className="p-6">{children}</div>
      </div>
    </div>
  );
}
