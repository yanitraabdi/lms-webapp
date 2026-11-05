import type { ReactNode } from "react";
import { AuthBrandPanel } from "@/components/auth/AuthBrandPanel";

// Auth screens are per-request (they read query params + use the auth context),
// so they're rendered dynamically rather than statically prerendered at build.
export const dynamic = "force-dynamic";

export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen lg:grid lg:grid-cols-2">
      <aside className="hidden bg-ink px-12 lg:flex lg:items-center lg:justify-center">
        <AuthBrandPanel />
      </aside>
      <main className="flex min-h-screen items-center justify-center bg-surface px-5 py-10 sm:px-8">
        <div className="w-full max-w-[400px]">{children}</div>
      </main>
    </div>
  );
}
