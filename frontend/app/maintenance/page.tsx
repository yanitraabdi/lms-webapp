import type { Metadata } from "next";
import { PageTitle, SHEET_CLASSES } from "@/components/ui";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Sedang pemeliharaan",
  robots: { index: false },
};

export default function MaintenancePage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-bg px-3 py-8 sm:px-7">
      <div className={`w-full max-w-[520px] ${SHEET_CLASSES}`}>
        <PageTitle size="compact">Sedang dalam pemeliharaan</PageTitle>
        <p className="mt-3 max-w-[60ch] leading-relaxed text-ink-muted">
          Kami sedang melakukan peningkatan singkat. Silakan kembali beberapa saat lagi.
        </p>
      </div>
    </div>
  );
}
