// INVERTA M3 — session playback/progress + gating tests. Types from the generated OpenAPI client.
import type { components } from "@/api-client/schema";
import { apiFetch } from "@/lib/auth/session";

export type SessionContext = components["schemas"]["SessionContextDto"];
export type SessionPlayback = components["schemas"]["SessionPlaybackDto"];
export type SessionProgress = components["schemas"]["SessionProgressDto"];
export type StudentAssessment = components["schemas"]["StudentAssessmentDto"];
export type StudentQuestion = components["schemas"]["StudentQuestionDto"];
export type Attempt = components["schemas"]["AttemptDto"];
export type AttemptResult = components["schemas"]["AttemptResultDto"];

export type AdminQuestion = components["schemas"]["AdminQuestionDto"];
export type AdminAssessment = components["schemas"]["AdminAssessmentDto"];
export type AssessmentConfig = components["schemas"]["AssessmentConfig"];
export type UpsertQuestion = components["schemas"]["UpsertQuestionRequest"];

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

async function problem(res: Response, fallback: string): Promise<Error> {
  const body = await res.json().catch(() => null);
  const title =
    body && typeof body === "object" && typeof (body as Record<string, unknown>).title === "string"
      ? (body as Record<string, string>).title
      : fallback;
  return new Error(title);
}

async function api<T>(method: string, path: string, token: string, body?: unknown): Promise<T> {
  // Goes through apiFetch so an expired access token is refreshed and the call retried
  // once, instead of surfacing a bare 401 the user can only clear by reloading.
  const res = await apiFetch(`${API}${path}`, {
    method,
    headers: { "content-type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    cache: "no-store",
  }, token);
  if (!res.ok) throw await problem(res, "Operasi gagal.");
  return (res.status === 204 ? undefined : await res.json()) as T;
}

// ---- session ----

export const getSessionContext = (t: string, id: string) =>
  api<SessionContext>("GET", `/api/sessions/${id}`, t);

export const getPlayback = (t: string, id: string) =>
  api<SessionPlayback>("POST", `/api/sessions/${id}/playback`, t);

export const getSessionProgress = (t: string, id: string) =>
  api<SessionProgress>("GET", `/api/sessions/${id}/progress`, t);

export const saveSessionProgress = (t: string, id: string, positionSeconds: number, percent: number) =>
  api<SessionProgress>("PUT", `/api/sessions/${id}/progress`, t, {
    positionSeconds: Math.round(positionSeconds),
    percent,
  });

/** The session's gating test, WITHOUT answers. Null when the session has none (204). */
export async function getSessionAssessment(t: string, id: string): Promise<StudentAssessment | null> {
  const res = await apiFetch(`${API}/api/sessions/${id}/assessment`, { cache: "no-store" }, t);
  if (res.status === 204) return null;
  if (!res.ok) throw await problem(res, "Gagal memuat tes.");
  return res.json();
}

// ---- attempts ----

export const startAttempt = (t: string, assessmentId: string) =>
  api<Attempt>("POST", `/api/assessments/${assessmentId}/attempts`, t);

export const saveAnswers = (t: string, attemptId: string, answers: Record<string, number>) =>
  api<void>("POST", `/api/attempts/${attemptId}/answers`, t, { answers });

export const submitAttempt = (t: string, attemptId: string, answers: Record<string, number>) =>
  api<AttemptResult>("POST", `/api/attempts/${attemptId}/submit`, t, { answers });

export const getAttemptResult = (t: string, attemptId: string) =>
  api<AttemptResult>("GET", `/api/attempts/${attemptId}/result`, t);

// ---- admin: question bank & assessments ----

export const listQuestions = (t: string, q: { section?: string; search?: string } = {}) => {
  const p = new URLSearchParams();
  if (q.section) p.set("section", q.section);
  if (q.search) p.set("search", q.search);
  return api<AdminQuestion[]>("GET", `/api/admin/questions?${p}`, t);
};
export const createQuestion = (t: string, b: UpsertQuestion) =>
  api<AdminQuestion>("POST", "/api/admin/questions", t, b);
export const updateQuestion = (t: string, id: string, b: UpsertQuestion) =>
  api<void>("PUT", `/api/admin/questions/${id}`, t, b);
export const deleteQuestion = (t: string, id: string) =>
  api<void>("DELETE", `/api/admin/questions/${id}`, t);

export const listAssessments = (t: string) => api<AdminAssessment[]>("GET", "/api/admin/assessments", t);
export const getAssessment = (t: string, id: string) => api<AdminAssessment>("GET", `/api/admin/assessments/${id}`, t);
export const createAssessment = (t: string, b: { kind: string; title: string; config: AssessmentConfig }) =>
  api<AdminAssessment>("POST", "/api/admin/assessments", t, b);
export const updateAssessment = (t: string, id: string, b: { kind: string; title: string; config: AssessmentConfig }) =>
  api<void>("PUT", `/api/admin/assessments/${id}`, t, b);
export const deleteAssessment = (t: string, id: string) => api<void>("DELETE", `/api/admin/assessments/${id}`, t);
export const setAssessmentQuestions = (t: string, id: string, questionIdsInOrder: string[]) =>
  api<void>("PUT", `/api/admin/assessments/${id}/questions`, t, { questionIdsInOrder });
/** Multipart — cannot use api(), which JSON-stringifies its body and sets a JSON content type. */
export async function uploadAudio(t: string, file: File): Promise<{ key: string }> {
  const form = new FormData();
  form.append("file", file);
  const res = await fetch(`${API}/api/admin/media/audio`, {
    method: "POST",
    headers: { Authorization: `Bearer ${t}` }, // let the browser set the multipart boundary
    body: form,
    cache: "no-store",
  });
  if (!res.ok) throw await problem(res, "Unggah audio gagal.");
  return await res.json();
}

export const attachAssessment = (t: string, sessionId: string, assessmentId: string | null) =>
  api<void>("PUT", `/api/admin/sessions/${sessionId}/assessment`, t, { assessmentId });

export type ImportResult = components["schemas"]["ImportResultDto"];
export type BulkAudioResult = components["schemas"]["BulkAudioResponse"];

/** Multipart — cannot use api(), which JSON-stringifies its body. apiFetch still applies the
 *  token and retries once after a refresh, and deliberately does NOT set a content type, so the
 *  browser writes its own multipart boundary. */
async function upload<T>(path: string, token: string, form: FormData, fallback: string): Promise<T> {
  const res = await apiFetch(`${API}${path}`, { method: "POST", body: form, cache: "no-store" }, token);
  if (!res.ok) throw await problem(res, fallback);
  return res.json() as Promise<T>;
}

export async function downloadImportTemplate(token: string): Promise<void> {
  const res = await apiFetch(`${API}/api/admin/questions/import/template`, {}, token);
  if (!res.ok) throw await problem(res, "Gagal mengunduh template.");
  const url = URL.createObjectURL(await res.blob());
  const a = document.createElement("a");
  a.href = url;
  a.download = "inverta-bank-soal-template.xlsx";
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

export function previewQuestionImport(token: string, file: File): Promise<ImportResult> {
  const form = new FormData();
  form.append("file", file);
  return upload<ImportResult>("/api/admin/questions/import/preview", token, form, "Gagal membaca berkas.");
}

export function commitQuestionImport(token: string, file: File): Promise<ImportResult> {
  const form = new FormData();
  form.append("file", file);
  return upload<ImportResult>("/api/admin/questions/import", token, form, "Gagal mengimpor soal.");
}

export function uploadAudioBulk(token: string, files: File[]): Promise<BulkAudioResult> {
  const form = new FormData();
  for (const file of files) form.append("files", file);
  return upload<BulkAudioResult>("/api/admin/media/audio/bulk", token, form, "Gagal mengunggah audio.");
}

export const QUESTION_SECTIONS = ["Listening", "Reading", "Vocabulary", "Structure", "General"] as const;

export function num(v: number | string): number {
  return typeof v === "string" ? Number(v) : v;
}

// ---------------------------------------------------------------- M4: final assessment

export type AttemptState = components["schemas"]["AttemptStateDto"];
export type ProctorState = components["schemas"]["ProctorStateDto"];
export type ProgramCertificate = components["schemas"]["ProgramCertificateDto"];
export type CertificateVerification = components["schemas"]["CertificateVerificationDto"];
export type ScoreBand = components["schemas"]["ScoreBandDto"];

export const getAttemptState = (t: string, attemptId: string) =>
  api<AttemptState>("GET", `/api/attempts/${attemptId}/state`, t);

export const saveSectionAnswers = (t: string, attemptId: string, answers: Record<string, number>) =>
  api<AttemptState>("POST", `/api/attempts/${attemptId}/section-answers`, t, { answers });

export const advanceSection = (t: string, attemptId: string, answers?: Record<string, number>) =>
  api<AttemptState>("POST", `/api/attempts/${attemptId}/advance`, t, { answers: answers ?? null });

export const finishAttempt = (t: string, attemptId: string) =>
  api<AttemptResult>("POST", `/api/attempts/${attemptId}/finish`, t);

/** Report a focus-loss event. The SERVER decides warn vs auto-submit (GR-13). */
export const reportProctorEvent = (t: string, attemptId: string, kind: string, durationMs?: number) =>
  api<ProctorState>("POST", `/api/attempts/${attemptId}/proctor-events`, t, { kind, durationMs: durationMs ?? null });

export const getAudioUrl = (t: string, attemptId: string, questionId: string) =>
  api<{ url: string }>("GET", `/api/attempts/${attemptId}/audio/${questionId}`, t);

export const listMyCertificates = (t: string) =>
  api<ProgramCertificate[]>("GET", "/api/me/program-certificates", t);

export const listScoreBands = (t: string, programId: string) =>
  api<ScoreBand[]>("GET", `/api/admin/programs/${programId}/score-bands`, t);

export const replaceScoreBands = (t: string, programId: string, bands: ScoreBand[]) =>
  api<void>("PUT", `/api/admin/programs/${programId}/score-bands`, t, { bands });

export const reinstateAttempt = (t: string, attemptId: string) =>
  api<void>("POST", `/api/admin/attempts/${attemptId}/reinstate`, t);

export async function downloadCertificate(t: string, certId: string, fileName: string): Promise<void> {
  const res = await apiFetch(`${API}/api/program-certificates/${certId}/pdf`, {}, t);
  if (!res.ok) throw await problem(res, "Gagal mengunduh sertifikat.");
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

/** Public — used by the SSR verify page (no auth). */
export async function verifyCertificate(code: string): Promise<CertificateVerification> {
  const base = typeof window === "undefined"
    ? (process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080")
    : API;
  const res = await fetch(`${base}/api/program-certificates/verify/${encodeURIComponent(code)}`, {
    cache: "no-store",
  });
  if (!res.ok) throw new Error(`verify ${res.status}`);
  return res.json();
}

export function clock(seconds: number): string {
  const s = Math.max(0, Math.floor(seconds));
  const m = Math.floor(s / 60);
  const r = s % 60;
  return `${m}:${r.toString().padStart(2, "0")}`;
}
