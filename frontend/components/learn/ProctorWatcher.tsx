"use client";

import { useEffect, useRef } from "react";
import { reportProctorEvent, type ProctorState } from "@/lib/sessions";

/**
 * Reports focus loss during a proctored sitting (KAK §9.8).
 *
 * The client only REPORTS — the server counts strikes and decides warn vs auto-submit (GR-13).
 * Losses shorter than the grace window are never sent, matching the server's own rule, so an OS
 * notification or an accidental alt-tab does not cost the learner a strike.
 *
 * Deliberately honest: this is deterrence, not exam security. It cannot prevent a second device.
 */
const GRACE_MS = 2000;

export function ProctorWatcher({
  token, attemptId, active, onState,
}: {
  token: string;
  attemptId: string;
  active: boolean;
  onState: (s: ProctorState) => void;
}) {
  const awaySince = useRef<number | null>(null);
  const onStateRef = useRef(onState);
  onStateRef.current = onState;

  useEffect(() => {
    if (!active) return;

    async function report(kind: string, durationMs: number) {
      // Mirror the server grace rule so we don't spam it with non-events.
      if (durationMs < GRACE_MS) return;
      try {
        onStateRef.current(await reportProctorEvent(token, attemptId, kind, durationMs));
      } catch {
        /* reporting must never break the sitting */
      }
    }

    function leave() {
      awaySince.current ??= Date.now();
    }

    function returned(kind: string) {
      if (awaySince.current === null) return;
      const elapsed = Date.now() - awaySince.current;
      awaySince.current = null;
      void report(kind, elapsed);
    }

    function onVisibility() {
      if (document.hidden) leave();
      else returned("VisibilityHidden");
    }

    function onBlur() { leave(); }
    function onFocus() { returned("WindowBlur"); }

    document.addEventListener("visibilitychange", onVisibility);
    window.addEventListener("blur", onBlur);
    window.addEventListener("focus", onFocus);
    return () => {
      document.removeEventListener("visibilitychange", onVisibility);
      window.removeEventListener("blur", onBlur);
      window.removeEventListener("focus", onFocus);
    };
  }, [token, attemptId, active]);

  return null;
}
