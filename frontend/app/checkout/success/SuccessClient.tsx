"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckCircleIcon, AlertTriangleIcon } from "@/components/ui";

export function SuccessClient() {
  const failed = useSearchParams().get("status") === "failed";

  return (
    <div className="flex min-h-screen items-center justify-center bg-bg px-4">
      <div className="w-full max-w-md rounded-lg border border-border bg-surface p-8 text-center shadow-lg">
        <span
          className={
            "mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full " +
            (failed ? "bg-danger-soft text-danger" : "bg-success-soft text-success")
          }
        >
          {failed ? <AlertTriangleIcon size={28} /> : <CheckCircleIcon size={30} />}
        </span>

        {failed ? (
          <>
            <h1 className="text-xl font-extrabold text-ink">Pembayaran gagal</h1>
            <p className="mt-2 text-sm leading-relaxed text-ink-muted">
              Pembayaran tidak dapat diproses. Anda belum dikenai biaya. Silakan coba lagi atau gunakan metode lain.
            </p>
          </>
        ) : (
          <>
            <h1 className="text-xl font-extrabold text-ink">Terima kasih! Pembayaran sedang diproses</h1>
            <p className="mt-2 text-sm leading-relaxed text-ink-muted">
              Kami sedang memverifikasi pembayaran Anda. <strong className="text-ink">Akses akan aktif otomatis</strong>{" "}
              begitu pembayaran terkonfirmasi — Anda akan menerima email konfirmasi. Halaman ini hanya informasi; akses
              diaktifkan oleh sistem pembayaran, bukan halaman ini.
            </p>
          </>
        )}

        <div className="mt-6 flex flex-col gap-2.5">
          {/* INVERTA is a ONE-TIME purchase. This page previously said "Lihat langganan saya" and
              offered the archived AI catalogue — telling a learner who had just bought a TOEFL
              programme that they held a subscription to something else. */}
          <Link
            href="/app/dashboard"
            className="rounded-sm bg-primary px-4 py-2.5 text-sm font-bold text-primary-ink transition-colors hover:bg-primary-hover"
          >
            Ke dasbor saya
          </Link>
          <Link
            href="/app/account"
            className="rounded-sm border border-border bg-surface px-4 py-2.5 text-sm font-bold text-ink transition-colors hover:bg-surface-2"
          >
            Lihat detail pembelian
          </Link>
        </div>
      </div>
    </div>
  );
}
