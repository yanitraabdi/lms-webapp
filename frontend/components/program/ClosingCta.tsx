import { EnrollCta } from "@/components/program/EnrollCta";

export function ClosingCta({ programId }: { programId: string }) {
  return (
    <section className="mt-12 border-t border-ink pt-6">
      <p className="max-w-[60ch] text-[18px] font-semibold text-ink">Siap mulai persiapan TOEFL Anda?</p>
      <EnrollCta programId={programId} className="mt-4 max-w-xs" />
    </section>
  );
}
