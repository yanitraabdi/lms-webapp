"use client";

import Link from "next/link";

export default function Error({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-bg px-6 text-center">
      <span className="flex h-16 w-16 items-center justify-center rounded-full bg-danger-soft text-danger">
        <svg width="34" height="34" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
          <path d="M12 3 2 20h20L12 3z" />
          <path d="M12 10v4M12 17.5v.5" />
        </svg>
      </span>
      <h1 className="text-2xl font-extrabold">Terjadi kesalahan</h1>
      <p className="max-w-sm text-sm text-ink-muted">
        Maaf, sesuatu tidak berjalan semestinya. Silakan coba lagi.
      </p>
      <div className="mt-2 flex gap-2.5">
        <button onClick={reset} className="rounded-sm bg-primary px-5 py-2.5 text-sm font-bold text-primary-ink hover:bg-primary-hover">
          Coba lagi
        </button>
        <Link href="/contact" className="rounded-sm border border-border bg-surface px-5 py-2.5 text-sm font-bold text-ink hover:bg-surface-2">
          Hubungi dukungan
        </Link>
      </div>
    </div>
  );
}
