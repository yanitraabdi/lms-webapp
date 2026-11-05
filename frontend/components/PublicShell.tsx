import type { ReactNode } from "react";
import { PublicNav } from "@/components/PublicNav";
import { SiteFooter } from "@/components/SiteFooter";

/// Public page chrome: auth-aware nav + footer, content area in between.
/// `bare` = no container, so a page can compose full-width `Band`s itself.
export function PublicShell({ children, bare = false }: { children: ReactNode; bare?: boolean }) {
  return (
    <div className="flex min-h-screen flex-col">
      <PublicNav />
      <main className="flex-1 bg-surface">
        {bare ? children : <div className="mx-auto w-full max-w-6xl px-6 py-8 sm:py-10">{children}</div>}
      </main>
      <SiteFooter />
    </div>
  );
}
