import { ITP_FORMAT, ITP_TOTAL_MINUTES, ITP_TOTAL_QUESTIONS } from "@/lib/itpFormat";

const SECTIONS = [ITP_FORMAT.listening, ITP_FORMAT.structure, ITP_FORMAT.reading];

export function ItpFormat() {
  return (
    <section className="border-y border-border bg-surface px-6 py-12">
      <div className="mx-auto max-w-5xl">
        <h2 className="mb-1 text-2xl font-extrabold tracking-tight">Format tes ITP</h2>
        <p className="mb-5 text-sm text-ink-muted">Tes akhir program ini mengikuti format TOEFL ITP.</p>
        <div className="grid gap-4 sm:grid-cols-3">
          {SECTIONS.map((s) => (
            <div key={s.label} className="rounded-lg border border-border bg-surface p-5 shadow-sm">
              <h3 className="text-base font-extrabold">{s.label}</h3>
              <p className="mt-1 text-[13.5px] text-ink-muted">{s.questions} soal</p>
              <p className="text-[13.5px] text-ink-muted">{s.minutes} menit</p>
            </div>
          ))}
        </div>
        <p className="mt-4 text-[13.5px] text-ink-muted">
          Total {ITP_TOTAL_QUESTIONS} soal · {ITP_TOTAL_MINUTES} menit · skor prediksi {ITP_FORMAT.totalMin}–{ITP_FORMAT.totalMax}
        </p>
      </div>
    </section>
  );
}
