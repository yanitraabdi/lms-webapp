"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckCircleIcon, AlertTriangleIcon, PageTitle, SHEET_CLASSES } from "@/components/ui";

export function SuccessClient() {
  const failed = useSearchParams().get("status") === "failed";

  return (
    <div className="flex min-h-screen items-center justify-center bg-bg px-3 py-8 sm:px-7">
      <div className={`w-full max-w-[520px] ${SHEET_CLASSES}`}>
        <span className={failed ? "text-danger" : "text-success"}>
          {failed ? <AlertTriangleIcon size={28} /> : <CheckCircleIcon size={30} />}
        </span>

        {failed ? (
          <>
            <PageTitle size="compact">Pembayaran gagal</PageTitle>
            <p className="mt-3 max-w-[60ch] leading-relaxed text-ink-muted">
              Pembayaran tidak dapat diproses. Anda belum dikenai biaya. Silakan coba lagi atau gunakan metode lain.
            </p>
          </>
        ) : (
          <>
            <PageTitle size="compact">Terima kasih! Pembayaran sedang diproses</PageTitle>
            <p className="mt-3 max-w-[60ch] leading-relaxed text-ink-muted">
              Kami sedang memverifikasi pembayaran Anda. <strong className="text-ink">Akses akan aktif otomatis</strong>{" "}
              begitu pembayaran terkonfirmasi, dan Anda akan menerima email konfirmasi. Halaman ini hanya informasi; akses
              diaktifkan oleh sistem pembayaran, bukan halaman ini.
            </p>
          </>
        )}

        <div className="mt-6 flex flex-col gap-2.5">
          {/* INVERTA is a ONE-TIME purchase. This page previously said "Lihat langganan saya" and
              offered the archived AI catalogue, telling a learner who had just bought a TOEFL
              programme that they held a subscription to something else. */}
          <Link
            href="/app/dashboard"
            className="rounded-sm bg-primary px-4 py-2.5 text-center text-sm font-bold text-primary-ink transition-colors hover:bg-primary-hover"
          >
            Ke dasbor saya
          </Link>
          <Link
            href="/app/account"
            className="rounded-sm border border-border bg-surface px-4 py-2.5 text-center text-sm font-bold text-ink transition-colors hover:bg-surface-2"
          >
            Lihat detail pembelian
          </Link>
        </div>
      </div>
    </div>
  );
}
