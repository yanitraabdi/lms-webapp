"use client";

import { useEffect, useState } from "react";

/**
 * True once the viewport is at Tailwind's `lg` breakpoint or wider.
 *
 * Starts false, including on the server and the first client paint, so the two agree and
 * hydration cannot mismatch. Anything gated on this must therefore be safe to render as nothing
 * for one frame.
 */
export function useIsDesktop(): boolean {
  const [isDesktop, setIsDesktop] = useState(false);

  useEffect(() => {
    const mq = window.matchMedia("(min-width: 1024px)");
    const sync = () => setIsDesktop(mq.matches);
    sync();
    mq.addEventListener("change", sync);
    return () => mq.removeEventListener("change", sync);
  }, []);

  return isDesktop;
}
