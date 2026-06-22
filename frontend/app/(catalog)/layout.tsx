import type { ReactNode } from "react";
import { PublicNav } from "@/components/PublicNav";
import { SiteFooter } from "@/components/SiteFooter";

// Public, SEO-facing surfaces (catalog browse + module landing). Server-rendered
// shell with the marketing nav/footer; per-page entitlement is resolved client-side.
export default function CatalogLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col bg-bg">
      <PublicNav />
      <main className="flex-1">{children}</main>
      <SiteFooter />
    </div>
  );
}
