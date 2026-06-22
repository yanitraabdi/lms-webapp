"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Spinner } from "@/components/ui";

// Client-side guard for the authenticated app. Redirects to /login when there's
// no session (server-side enforcement is the .NET API's job on every request).
export function AppGuard({ children }: { children: ReactNode }) {
  const { status } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login");
  }, [status, router]);

  if (status !== "authenticated") {
    return (
      <div className="flex min-h-screen items-center justify-center text-primary">
        <Spinner size={28} />
      </div>
    );
  }
  return <>{children}</>;
}
