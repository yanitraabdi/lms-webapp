import type { CSSProperties } from "react";

const STEPS = [
  { label: "Video", sub: "materi" },
  { label: "Tes sesi", sub: "lulus = lanjut" },
  { label: "Kelas live", sub: "bersama pengajar" },
  { label: "Tes akhir", sub: "format ITP" },
  { label: "Sertifikat", sub: "prediksi skor" },
];

// Line runs dot-centre to dot-centre: vertical under 560px (rows), horizontal above (columns).
const line =
  "absolute left-[15px] top-[10%] bottom-[10%] w-[2px] rounded-full " +
  "min-[560px]:left-[10%] min-[560px]:right-[10%] min-[560px]:top-[15px] min-[560px]:bottom-auto min-[560px]:h-[2px] min-[560px]:w-auto";

/** Animation A, "the path": plays once on load (CSS in globals.css), then rests fully lit. */
export function JourneyAnimation() {
  return (
    <figure data-anim="journey" className="m-0">
      <div aria-hidden="true" className="flex h-[280px] items-center rounded-lg bg-bg px-6 min-[560px]:h-[184px]">
        <div className="relative h-[240px] w-full min-[560px]:h-[120px]">
          <div className={`${line} bg-border`} />
          <div className={`${line} journey-fill bg-primary`} />
          <div className="relative grid h-full grid-rows-5 min-[560px]:grid-cols-5 min-[560px]:grid-rows-none">
            {STEPS.map((s, i) => (
              <div key={s.label} className="flex items-center gap-3 min-[560px]:flex-col min-[560px]:justify-start min-[560px]:gap-2 min-[560px]:text-center">
                <span
                  className="journey-dot flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2 border-primary bg-primary-soft text-[13px] font-extrabold tabular-nums text-primary"
                  style={{ "--i": i } as CSSProperties}
                >
                  {i + 1}
                </span>
                <span className="leading-tight">
                  <span className="block text-[13.5px] font-bold text-ink">{s.label}</span>
                  <span className="block text-[12px] text-ink-muted">{s.sub}</span>
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>
      <figcaption className="sr-only">Alur program: video, tes sesi, kelas live, tes akhir, sertifikat.</figcaption>
    </figure>
  );
}
