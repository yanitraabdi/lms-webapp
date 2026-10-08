"use client";

import { useEffect, useRef } from "react";
import { driver } from "driver.js";
import "driver.js/dist/driver.css";
import { completeTour, getOnboardingState } from "@/lib/content";

const TOUR_KEY = "dashboard_first_run";

export function OnboardingFlow({ token }: { token: string }) {
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    (async () => {
      try {
        const state = await getOnboardingState(token);
        if (!state.tourCompleted) setTimeout(startTour, 250); // let the dashboard's tour targets mount
      } catch {
        /* onboarding is best-effort; never block the dashboard */
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function startTour() {
    const steps = [
      { element: '[data-tour="nav-program"]', title: "Program Anda", description: "Semua sesi program tampil di sini secara berurutan." },
      { element: '[data-tour="nav-certificates"]', title: "Sertifikat prediksi", description: "Sertifikat prediksi TOEFL Anda tampil dan dapat diunduh di sini." },
      { element: '[data-tour="overall-progress"]', title: "Progres berurutan", description: "Sesi berikutnya terbuka setelah sesi sebelumnya Anda selesaikan." },
    ].filter((s) => typeof document !== "undefined" && document.querySelector(s.element));

    if (steps.length === 0) {
      void completeTour(token, TOUR_KEY, "Completed").catch(() => {});
      return;
    }

    const d = driver({
      showProgress: true,
      nextBtnText: "Lanjut",
      prevBtnText: "Kembali",
      doneBtnText: "Selesai",
      steps: steps.map((s) => ({ element: s.element, popover: { title: s.title, description: s.description } })),
      onDestroyed: () => void completeTour(token, TOUR_KEY, "Completed").catch(() => {}),
    });
    d.drive();
  }

  return null;
}
