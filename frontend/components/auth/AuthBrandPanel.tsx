import { Logo } from "@/components/Logo";
import { JOURNEY_STEPS } from "@/components/program/journey-steps";

/** Brand side of the split sign-in screen. No heading: each form page owns the single h1. */
export function AuthBrandPanel() {
  return (
    <div className="flex h-full w-full max-w-[460px] flex-col justify-center gap-10 text-white">
      <Logo variant="white" height="h-9" className="self-start" />
      <p className="text-[32px] font-extrabold leading-[1.15] tracking-[-0.02em]">
        Belajar TOEFL langkah demi langkah, sampai tahu prediksi skor Anda.
      </p>
      <ol className="flex flex-col gap-4">
        {JOURNEY_STEPS.map((s, i) => (
          <li key={s.label} className="flex items-center gap-3">
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2 border-white/60 text-[13px] font-extrabold tabular-nums">
              {i + 1}
            </span>
            <span className="leading-tight">
              <span className="block text-[14px] font-bold">{s.label}</span>
              <span className="block text-[12.5px] text-white/75">{s.sub}</span>
            </span>
          </li>
        ))}
      </ol>
    </div>
  );
}
