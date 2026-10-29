import type { ReactNode } from "react";

// Auth screens are per-request (they read query params + use the auth context),
// so they're rendered dynamically rather than statically prerendered at build.
export const dynamic = "force-dynamic";

export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-bg px-3 py-8 sm:px-7">
      <div className="w-full max-w-[440px] rounded-lg bg-surface px-5 py-8 sm:px-9 sm:py-10 shadow-[0_1px_0_var(--color-border),0_10px_30px_-18px_rgba(17,37,63,0.2)]">
        {children}
      </div>
    </div>
  );
}
