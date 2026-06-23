import type { ReactNode } from "react";
import { PublicNav } from "@/components/PublicNav";
import { SiteFooter } from "@/components/SiteFooter";

/// Public page chrome: auth-aware nav + footer, content area in between.
export function PublicShell({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col bg-bg">
      <PublicNav />
      <main className="flex-1">{children}</main>
      <SiteFooter />
    </div>
  );
}
