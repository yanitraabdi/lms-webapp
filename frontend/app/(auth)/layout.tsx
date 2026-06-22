import type { ReactNode } from "react";

// Auth screens are per-request (they read query params + use the auth context),
// so they're rendered dynamically rather than statically prerendered at build.
export const dynamic = "force-dynamic";

export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-[#E9EEF5] px-4 py-10">
      <div className="w-full max-w-sm">{children}</div>
    </div>
  );
}
