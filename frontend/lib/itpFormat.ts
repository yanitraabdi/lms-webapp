/**
 * The TOEFL ITP format shown on the programme page. Mirrors backend/src/Domain/ToeflScoring.cs —
 * the backend test ItpFormatConstantsTests reads THIS file and fails if the numbers drift.
 * Keep the shape (one `questions:` and `minutes:` per section, `totalMin:`, `totalMax:`) parseable.
 */
export const ITP_FORMAT = {
  listening: { label: "Listening", questions: 50, minutes: 35 },
  structure: { label: "Structure & Written Expression", questions: 40, minutes: 25 },
  reading: { label: "Reading", questions: 50, minutes: 55 },
  totalMin: 310,
  totalMax: 677,
} as const;

export const ITP_TOTAL_QUESTIONS =
  ITP_FORMAT.listening.questions + ITP_FORMAT.structure.questions + ITP_FORMAT.reading.questions;
export const ITP_TOTAL_MINUTES =
  ITP_FORMAT.listening.minutes + ITP_FORMAT.structure.minutes + ITP_FORMAT.reading.minutes;
