"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button } from "@/components/ui";
import { enroll } from "@/lib/programs";

/**
 * Enrollment CTA. Sends the learner to the hosted invoice — it NEVER grants access;
 * only the verified payment webhook does that (GR-2).
 */
export function EnrollCta({ programId, className }: { programId: string; className?: string }) {
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
    <div className={className}>
      <Button fullWidth onClick={onClick} loading={busy}>
        {status === "authenticated" ? "Daftar sekarang" : "Masuk & daftar"}
      </Button>
      {error && (
        <p className="mt-2 rounded-base border border-danger/30 bg-danger-soft px-3 py-2 text-[12.5px] text-danger">
          {error}
        </p>
      )}
    </div>
  );
}
