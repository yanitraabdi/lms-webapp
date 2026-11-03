"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui";

/** Animation B, "the app peek": plays its three scenes once when it scrolls into view, then rests on the certificate. */
export function AppPeekAnimation({ lessonTitle }: { lessonTitle?: string }) {
  const ref = useRef<HTMLElement>(null);
  const [run, setRun] = useState(0); // bumping the key remounts the scenes, which restarts their CSS animations
  const [playing, setPlaying] = useState(false);
  const [paused, setPaused] = useState(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const io = new IntersectionObserver(([e]) => {
      if (e.isIntersecting) { setPlaying(true); io.disconnect(); }
    }, { threshold: 0.4 });
    io.observe(el);
    return () => io.disconnect();
  }, []);

  const active = playing && !paused && !done;
  function toggle() {
    if (active) { setPaused(true); return; }
    setRun((r) => r + 1);
    setPlaying(true);
    setPaused(false);
    setDone(false);
  }

  return (
    <figure ref={ref} data-anim="peek" data-playing={playing || undefined} data-paused={paused || undefined} className="peek m-0">
      <div
        key={run}
        aria-hidden="true"
        onAnimationEnd={(e) => { if (e.animationName === "peek-enter") setDone(true); }}
        className="relative h-[264px] overflow-hidden rounded-lg bg-bg"
      >
        <div className="absolute inset-x-4 inset-y-4 overflow-hidden rounded-base border border-border bg-surface shadow-lg min-[560px]:inset-x-8">
          <div className="flex h-6 items-center gap-1.5 border-b border-border bg-bg px-3">
            <i className="h-2 w-2 rounded-full bg-border" /><i className="h-2 w-2 rounded-full bg-border" /><i className="h-2 w-2 rounded-full bg-border" />
          </div>
          <div className="relative h-[calc(100%-24px)]">
            <div className="peek-anim peek-scene-1 absolute inset-0 p-4">
              <div className="flex h-[104px] items-center justify-center rounded-sm bg-ink">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="currentColor" className="text-white"><path d="M7 4.5v15l12.5-7.5z" /></svg>
              </div>
              <div className="mt-3 h-1 overflow-hidden rounded-full bg-border">
                <div className="peek-anim peek-progress h-full origin-left rounded-full bg-primary" />
              </div>
              <p className="mt-2.5 truncate text-[13px] font-bold text-ink">{lessonTitle ?? "Video materi"}</p>
            </div>
            <div className="peek-anim peek-scene-2 absolute inset-0 p-4">
              <p className="text-[13px] font-bold text-ink">The list of items ___ on the desk.</p>
              <div className="mt-2.5 flex flex-col gap-1.5 text-[12.5px] text-ink">
                <span className="rounded-sm border border-border px-2.5 py-1">are</span>
                <span className="peek-anim peek-choice rounded-sm border border-primary bg-primary-soft px-2.5 py-1 font-bold">is</span>
                <span className="rounded-sm border border-border px-2.5 py-1">were being</span>
              </div>
              <span className="peek-anim peek-pass mt-3 inline-block rounded-full bg-success-soft px-2.5 py-0.5 text-[12px] font-extrabold text-[#166534]">
                Lulus · Sesi berikutnya terbuka
              </span>
            </div>
            <div className="peek-anim peek-scene-3 absolute inset-0 p-4">
              <div className="flex h-full flex-col items-center justify-center rounded-base border-2 border-primary px-3 text-center">
                <p className="text-[11px] font-bold uppercase tracking-[0.08em] text-ink-muted">Sertifikat prediksi skor</p>
                <p className="text-[34px] font-black leading-none tracking-tight text-primary tabular-nums">520</p>
                <p className="mt-1.5 text-[12px] text-ink-muted tabular-nums">Listening 51 · Structure 53 · Reading 52</p>
                <p className="mt-2 text-[11.5px] font-semibold text-ink">Contoh. Prediksi INVERTA, bukan skor TOEFL resmi</p>
              </div>
            </div>
          </div>
        </div>
      </div>
      <Button variant="ghost" size="sm" onClick={toggle} className="peek-toggle mt-2">
        {active ? "Jeda" : "Putar ulang"}
      </Button>
      <figcaption className="sr-only">
        Contoh tampilan aplikasi: video materi, tes sesi yang lulus, lalu sertifikat prediksi skor.
      </figcaption>
    </figure>
  );
}
