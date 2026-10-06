import { EnrollCta } from "@/components/program/EnrollCta";
import { formatIdr } from "@/lib/programs";

export function ClosingCta({ programId, priceIdr }: { programId: string; priceIdr: number | string }) {
  return (
    <section className="border-y border-border bg-surface px-6 py-12">
      <div className="mx-auto flex max-w-xl flex-col items-center rounded-lg border border-border bg-surface p-8 text-center shadow-sm">
        <h2 className="text-2xl font-extrabold tracking-tight">Siap mulai persiapan TOEFL Anda?</h2>
        <p className="mt-2 text-[15px] text-ink-muted">{formatIdr(priceIdr)} · sekali bayar</p>
        <EnrollCta programId={programId} className="mt-4" />
      </div>
    </section>
  );
}
