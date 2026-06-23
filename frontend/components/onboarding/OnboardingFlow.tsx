"use client";

import { useEffect, useRef, useState } from "react";
import { driver } from "driver.js";
import "driver.js/dist/driver.css";
import { Modal, Button } from "@/components/ui";
import { cn } from "@/lib/cn";
import { completeTour, getOnboardingState, saveSurvey } from "@/lib/content";

const TOUR_KEY = "dashboard_first_run";

const ROLES = [
  { v: "ops", label: "Operasional" },
  { v: "marketing", label: "Pemasaran/Konten" },
  { v: "business", label: "Bisnis/Manajemen" },
  { v: "developer", label: "Teknis/Developer" },
  { v: "other", label: "Lainnya" },
];
const GOALS = ["Produktivitas harian", "Penulisan & konten", "Analisis data", "Otomasi", "Workflow tim"];
const TOOLS = ["ChatGPT", "Claude", "Gemini", "Lainnya"];

export function OnboardingFlow({ token }: { token: string }) {
  const [showSurvey, setShowSurvey] = useState(false);
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    (async () => {
      try {
        const state = await getOnboardingState(token);
        if (!state.surveyCompleted) setShowSurvey(true);
        else if (!state.tourCompleted) startTour();
      } catch {
        /* onboarding is best-effort; never block the dashboard */
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function startTour() {
    const steps = [
      { element: '[data-tour="nav-catalog"]', title: "Jelajahi katalog", description: "Temukan semua modul pembelajaran di sini." },
      { element: '[data-tour="nav-certificates"]', title: "Sertifikat Anda", description: "Sertifikat yang Anda peroleh tampil di sini." },
      { element: '[data-tour="overall-progress"]', title: "Progres belajar", description: "Pantau kemajuan Anda menuju sertifikat." },
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

  async function finishSurvey(role: string | undefined, goals: string[], tools: string[]) {
    try {
      await saveSurvey(token, { role, goals, preferredTools: tools });
    } catch {
      /* ignore */
    }
    setShowSurvey(false);
    setTimeout(startTour, 250); // let the modal unmount so tour targets are visible
  }

  if (!showSurvey) return null;
  return <SurveyModal onSubmit={finishSurvey} onSkip={() => finishSurvey(undefined, [], [])} />;
}

function SurveyModal({
  onSubmit,
  onSkip,
}: {
  onSubmit: (role: string | undefined, goals: string[], tools: string[]) => void;
  onSkip: () => void;
}) {
  const [role, setRole] = useState<string | undefined>();
  const [goals, setGoals] = useState<string[]>([]);
  const [tools, setTools] = useState<string[]>([]);

  const toggle = (list: string[], v: string) => (list.includes(v) ? list.filter((x) => x !== v) : [...list, v]);

  return (
    <Modal
      open
      onClose={onSkip}
      title="Bantu kami menyesuaikan rekomendasi"
      className="max-w-md"
      footer={
        <>
          <Button variant="neutral" fullWidth onClick={onSkip}>Lewati</Button>
          <Button fullWidth onClick={() => onSubmit(role, goals, tools)}>Simpan</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <Field label="Peran Anda">
          <div className="flex flex-wrap gap-2">
            {ROLES.map((r) => (
              <Chip key={r.v} active={role === r.v} onClick={() => setRole(role === r.v ? undefined : r.v)}>{r.label}</Chip>
            ))}
          </div>
        </Field>
        <Field label="Tujuan belajar (pilih beberapa)">
          <div className="flex flex-wrap gap-2">
            {GOALS.map((g) => (
              <Chip key={g} active={goals.includes(g)} onClick={() => setGoals((s) => toggle(s, g))}>{g}</Chip>
            ))}
          </div>
        </Field>
        <Field label="Alat AI yang dipakai (pilih beberapa)">
          <div className="flex flex-wrap gap-2">
            {TOOLS.map((t) => (
              <Chip key={t} active={tools.includes(t)} onClick={() => setTools((s) => toggle(s, t))}>{t}</Chip>
            ))}
          </div>
        </Field>
      </div>
    </Modal>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-2">
      <span className="text-[12.5px] font-bold text-ink">{label}</span>
      {children}
    </div>
  );
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "rounded-full px-3 py-1.5 text-[12.5px] font-semibold transition-colors",
        active ? "bg-primary text-primary-ink" : "bg-surface-2 text-ink-muted hover:bg-border"
      )}
    >
      {children}
    </button>
  );
}
