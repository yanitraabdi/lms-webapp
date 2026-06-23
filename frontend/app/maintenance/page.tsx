import type { Metadata } from "next";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Sedang pemeliharaan — AI Productivity Academy",
  robots: { index: false },
};

export default function MaintenancePage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-bg px-6 text-center">
      <span className="flex h-16 w-16 items-center justify-center rounded-full bg-primary-soft text-primary">
        <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
          <path d="M14.7 6.3a4 4 0 0 1-5 5L4 17v3h3l5.7-5.7a4 4 0 0 0 5-5l-2.3 2.3-2.4-.6-.6-2.4 2.3-2.3z" />
        </svg>
      </span>
      <h1 className="text-2xl font-extrabold">Sedang dalam pemeliharaan</h1>
      <p className="max-w-sm text-sm text-ink-muted">
        Kami sedang melakukan peningkatan singkat. Silakan kembali beberapa saat lagi.
      </p>
    </div>
  );
}
