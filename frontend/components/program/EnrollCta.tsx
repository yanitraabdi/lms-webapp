"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button } from "@/components/ui";
import { cn } from "@/lib/cn";
import { enroll, formatIdr } from "@/lib/programs";

/**
 * Enrollment CTA. Sends the learner to the hosted invoice — it NEVER grants access;
 * only the verified payment webhook does that (GR-2).
 */
const BENEFITS = [
  "Akses seluruh sesi video",
  "Tes singkat di setiap sesi",
  "Sesi live bersama pengajar",
  "Simulasi tes akhir + prediksi skor",
  "Sertifikat yang dapat diverifikasi",
];

/** With `priceIdr` it renders the price/enrol box; without, just the button (closing paragraph). */
export function EnrollCta({ programId, priceIdr, className }: { programId: string; priceIdr?: number | string; className?: string }) {
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

  const action = (
    <>
      <Button fullWidth onClick={onClick} loading={busy}>
        {status === "authenticated" ? "Daftar sekarang" : "Masuk & daftar"}
      </Button>
      {error && (
        <p className="mt-2 rounded-base border border-danger/30 bg-danger-soft px-3 py-2 text-[12.5px] text-danger">
          {error}
        </p>
      )}
    </>
  );

  if (priceIdr === undefined) return <div className={className}>{action}</div>;

  // top-[81px]: PublicNav is 36px content + py-3 + 1px border = 61px, plus 20px.
  return (
    <div className={cn("rounded-lg border border-border bg-surface p-[22px] min-[860px]:sticky min-[860px]:top-[81px]", className)}>
      <span className="text-[13px] text-ink-muted">Biaya program</span>
      <p className="mt-1 text-[30px] font-extrabold leading-tight tabular-nums text-ink">{formatIdr(priceIdr)}</p>
      <p className="mt-1 text-[13px] text-ink-muted">sekali bayar, bukan langganan</p>
      <div className="mt-4">{action}</div>
      <ul className="mt-5 flex flex-col gap-2 border-t border-border pt-4">
        {BENEFITS.map((f) => (
          <li key={f} className="flex gap-2 text-[13.5px] leading-snug text-ink-muted">
            <span aria-hidden="true" className="font-bold text-success">✓</span>
            {f}
          </li>
        ))}
      </ul>
      <p className="mt-4 text-[12px] leading-snug text-ink-subtle">
        Pembayaran diproses aman. Akses terbuka otomatis setelah pembayaran terkonfirmasi.
      </p>
    </div>
  );
}
