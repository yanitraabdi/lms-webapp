import type { ReactNode } from "react";
import { PublicNav } from "@/components/PublicNav";
import { SheetPage } from "@/components/ui";
import { SiteFooter } from "@/components/SiteFooter";

/// Public page chrome: auth-aware nav + footer, content area in between.
/// `bare` = no container, so a page can compose full-width `Band`s itself.
export function PublicShell({ children, bare = false }: { children: ReactNode; bare?: boolean }) {
  return (
    <div className="flex min-h-screen flex-col bg-bg">
      <PublicNav />
      <main className="flex-1 bg-surface">
        {bare ? children : <SheetPage>{children}</SheetPage>}
      </main>
      <SiteFooter />
    </div>
  );
}
