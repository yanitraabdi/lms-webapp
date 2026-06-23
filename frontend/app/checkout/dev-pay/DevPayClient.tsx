"use client";

import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/components/ui";
import { formatIdr, simulateDevPayment } from "@/lib/billing";

/// Local stand-in for the Xendit hosted-checkout page. Completing it fires a signed
/// webhook through the real backend (which is what actually grants entitlement).
export function DevPayClient() {
  const router = useRouter();
  const params = useSearchParams();
  const providerRef = params.get("ref") ?? "";
  const amount = params.get("amount") ?? "0";
  const kind = params.get("kind") ?? "Cycle";
  const [busy, setBusy] = useState(false);

  async function pay(succeed: boolean) {
    if (!providerRef) return;
    setBusy(true);
    try {
      await simulateDevPayment(providerRef, succeed);
    } catch {
      /* fall through to the success page in failed state */
    }
    router.push(`/checkout/success?status=${succeed ? "ok" : "failed"}`);
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-bg px-4">
      <div className="w-full max-w-md rounded-lg border border-border bg-surface p-7 shadow-lg">
        <div className="mb-1 flex items-center gap-2">
          <span className="flex h-7 w-7 items-center justify-center rounded-[7px] bg-primary text-sm font-extrabold text-primary-ink">A</span>
          <span className="text-sm font-bold">Pembayaran (Simulasi Dev)</span>
        </div>
        <p className="mb-5 text-[13px] text-ink-muted">
          Gateway pembayaran berjalan dalam mode simulasi. Pilih hasil pembayaran untuk menguji alur.
        </p>

        <div className="mb-6 flex flex-col gap-2.5 rounded-base border border-border bg-bg p-4 text-sm">
          <Row label="Jumlah" value={formatIdr(Number(amount))} strong />
          <Row label="Jenis" value={kind === "ProrationUpgrade" ? "Upgrade (prorata)" : "Langganan"} />
        </div>

        <div className="flex flex-col gap-2.5">
          <Button fullWidth loading={busy} onClick={() => pay(true)}>
            Bayar sekarang (berhasil)
          </Button>
          <Button variant="neutral" fullWidth disabled={busy} onClick={() => pay(false)}>
            Simulasikan pembayaran gagal
          </Button>
        </div>
      </div>
    </div>
  );
}

function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-ink-muted">{label}</span>
      <span className={strong ? "text-base font-extrabold text-ink" : "font-semibold text-ink"}>{value}</span>
    </div>
  );
}
