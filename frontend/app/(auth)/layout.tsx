import type { ReactNode } from "react";
import { SHEET_CLASSES } from "@/components/ui";

// Auth screens are per-request (they read query params + use the auth context),
// so they're rendered dynamically rather than statically prerendered at build.
export const dynamic = "force-dynamic";

export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-bg px-3 py-8 sm:px-7">
      <div className={`w-full max-w-[440px] ${SHEET_CLASSES}`}>
        {children}
      </div>
    </div>
  );
}
