"use client";

import { AudioUpload } from "@/components/admin/AudioUpload";
import { QuestionPicker } from "@/components/admin/QuestionPicker";

/** One section of the final assessment: how many questions it needs, and which are chosen. */
export function SectionComposer({
  token, section, required, selected, onChange, audioRef, onAudioChange,
}: {
  token: string;
  section: string;
  required: number;
  selected: string[];
  onChange: (ids: string[]) => void;
  /** Whole-section recording. Omit both to hide the control (non-Listening sections). */
  audioRef?: string | null;
  onAudioChange?: (key: string | null) => void;
}) {
  const complete = selected.length === required;

  return (
    <section className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-base font-extrabold">{section}</h2>
        <span
          className={
            "rounded-full px-2.5 py-1 text-[12px] font-bold " +
            (complete ? "bg-success-soft text-success" : "bg-warning-soft text-warning")
          }
        >
          {selected.length} / {required} soal
        </span>
      </div>
      {onAudioChange && (
        <div className="mb-4 flex flex-col gap-1">
          <span className="text-[12px] font-bold text-ink-muted">Rekaman satu bagian (opsional)</span>
          <p className="text-[12px] text-ink-subtle">Dipakai untuk soal yang tidak punya audio sendiri.</p>
          <AudioUpload token={token} value={audioRef ?? null} onChange={onAudioChange} />
        </div>
      )}
      <QuestionPicker token={token} section={section} selected={selected} onChange={onChange} />
    </section>
  );
}
