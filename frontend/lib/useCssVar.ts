"use client";
import { useEffect, useState } from "react";

/** A design token's resolved value, for libraries (Recharts) that set SVG attributes, where
 *  `var(--x)` is not resolved. Read after mount so SSR and the first paint use the fallback. */
export function useCssVar(name: string, fallback = "#7F00FF"): string {
  const [value, setValue] = useState(fallback);
  useEffect(() => {
    const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    if (v) setValue(v);
  }, [name]);
  return value;
}

/** True when the user asked the OS for reduced motion; charts then skip their entry animation. */
export function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false);
  useEffect(() => {
    const mq = window.matchMedia("(prefers-reduced-motion: reduce)");
    setReduced(mq.matches);
    const on = (e: MediaQueryListEvent) => setReduced(e.matches);
    mq.addEventListener("change", on);
    return () => mq.removeEventListener("change", on);
  }, []);
  return reduced;
}
