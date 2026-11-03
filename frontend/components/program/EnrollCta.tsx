"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button } from "@/components/ui";
import { cn } from "@/lib/cn";
import { enroll } from "@/lib/programs";

/**
 * Enrollment CTA. Sends the learner to the hosted invoice; it NEVER grants access;
 * only the verified payment webhook does that (GR-2).
 */
export function EnrollCta({ programId, className, size = "md" }: { programId: string; className?: string; size?: "md" | "sm" }) {
  const { status, user, accessToken } = useAuth();
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onClick() {
    setError(null);

    if (status !== "authenticated" || !accessToken) {
      // Come back to this same landing page after signing in.
      const next = typeof window === "undefined" ? "/" : window.location.pathname;
      router.push(`/login?next=${encodeURIComponent(next)}`);
      return;
    }
    // Email verification is required before purchase — surface it before the API 403s.
    if (user && !user.emailVerified) {
      router.push("/verify-email");
      return;
    }

    setBusy(true);
    try {
      const session = await enroll(accessToken, programId);
      window.location.href = session.checkoutUrl;
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Gagal memulai pendaftaran.";
      // Already enrolled → send them to the program instead of showing an error.
      if (msg.toLowerCase().includes("sudah terdaftar")) router.push(`/app/program/${programId}`);
      else setError(msg);
      setBusy(false);
    }
  }

  return (
    // "sm" sits in the nav's flex row: its error floats below the button so it can't break the row.
    <div className={cn(size === "sm" && "relative", className)}>
      {/* "sm" is the nav button: Button's sm is h-8, the nav's ctaBase is h-9 (! beats it). */}
      <Button
        fullWidth={size === "md"}
        size={size}
        className={size === "sm" ? "h-9! px-3.5" : undefined}
        onClick={onClick}
        loading={busy}
      >
        Daftar sekarang
      </Button>
      {error && (
        <p
          role="alert"
          className={cn(
            "mt-2 rounded-base border border-danger/30 bg-danger-soft px-3 py-2 text-[12.5px] text-danger",
            size === "sm" && "absolute right-0 top-full z-10 w-64"
          )}
        >
          {error}
        </p>
      )}
    </div>
  );
}
