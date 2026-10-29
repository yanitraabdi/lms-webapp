import type { Metadata } from "next";
import { PageTitle } from "@/components/ui";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Sedang pemeliharaan",
  robots: { index: false },
};

export default function MaintenancePage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-bg px-3 py-8 sm:px-7">
      <div className="w-full max-w-[520px] rounded-lg bg-surface px-5 py-8 shadow-[0_1px_0_var(--color-border),0_10px_30px_-18px_rgba(17,37,63,0.2)] sm:px-9 sm:py-10">
        <PageTitle size="compact">Sedang dalam pemeliharaan</PageTitle>
        <p className="mt-3 max-w-[60ch] leading-relaxed text-ink-muted">
          Kami sedang melakukan peningkatan singkat. Silakan kembali beberapa saat lagi.
        </p>
      </div>
    </div>
  );
}
