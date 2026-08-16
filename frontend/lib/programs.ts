// INVERTA program & enrollment client (M2). Types come from the generated OpenAPI client.
import type { components } from "@/api-client/schema";

export type PublicProgram = components["schemas"]["PublicProgramDto"];
export type PublicSession = components["schemas"]["PublicSessionDto"];
export type StudentProgram = components["schemas"]["StudentProgramDto"];
export type StudentSession = components["schemas"]["StudentSessionDto"];
export type SessionState = components["schemas"]["SessionState"];
export type Enrollment = components["schemas"]["EnrollmentDto"];
export type CheckoutSession = components["schemas"]["CheckoutSession"];

export type AdminProgram = components["schemas"]["AdminProgramDto"];
export type AdminSession = components["schemas"]["AdminSessionDto"];
export type AdminBatch = components["schemas"]["AdminBatchDto"];
export type UpsertProgram = components["schemas"]["UpsertProgramRequest"];
export type UpsertSession = components["schemas"]["UpsertSessionRequest"];
export type UpsertBatch = components["schemas"]["UpsertBatchRequest"];

/** Server-side (SSR/SSG) calls go over the internal network; the browser uses the same origin. */
function baseUrl(): string {
  if (typeof window === "undefined") {
    return process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
  }
  return process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
}

function auth(token: string): HeadersInit {
  return { Authorization: `Bearer ${token}`, "content-type": "application/json" };
}

async function problem(res: Response, fallback: string): Promise<Error> {
  const body = await res.json().catch(() => null);
  const title =
    body && typeof body === "object" && typeof (body as Record<string, unknown>).title === "string"
      ? (body as Record<string, string>).title
      : fallback;
  return new Error(title);
}

async function api<T>(method: string, path: string, token: string, body?: unknown): Promise<T> {
  const res = await fetch(`${baseUrl()}${path}`, {
    method,
    headers: auth(token),
    body: body === undefined ? undefined : JSON.stringify(body),
    cache: "no-store",
  });
  if (!res.ok) throw await problem(res, "Operasi gagal.");
  return (res.status === 204 ? undefined : await res.json()) as T;
}

// ---- public (landing) ----

/** Published program by slug. Returns null when absent so the page can render notFound(). */
export async function getPublicProgram(slug: string): Promise<PublicProgram | null> {
  const res = await fetch(`${baseUrl()}/api/programs/${encodeURIComponent(slug)}`, {
    next: { revalidate: 300 },
  });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`program ${res.status}`);
  return res.json();
}

// ---- student ----

export const enroll = (t: string, programId: string, batchId?: string | null) =>
  api<CheckoutSession>("POST", `/api/programs/${programId}/enroll`, t, { batchId: batchId ?? null });

export const listMyEnrollments = (t: string) =>
  api<Enrollment[]>("GET", "/api/me/enrollments", t);

export const getStudentProgram = (t: string, programId: string) =>
  api<StudentProgram>("GET", `/api/me/programs/${programId}`, t);

// ---- admin ----

export const listPrograms = (t: string) => api<AdminProgram[]>("GET", "/api/admin/programs", t);
export const getAdminProgram = (t: string, id: string) => api<AdminProgram>("GET", `/api/admin/programs/${id}`, t);
export const createProgram = (t: string, b: UpsertProgram) => api<AdminProgram>("POST", "/api/admin/programs", t, b);
export const updateProgram = (t: string, id: string, b: UpsertProgram) => api<void>("PUT", `/api/admin/programs/${id}`, t, b);
export const deleteProgram = (t: string, id: string) => api<void>("DELETE", `/api/admin/programs/${id}`, t);

export const listSessions = (t: string, programId: string) =>
  api<AdminSession[]>("GET", `/api/admin/programs/${programId}/sessions`, t);
export const createSession = (t: string, programId: string, b: UpsertSession) =>
  api<AdminSession>("POST", `/api/admin/programs/${programId}/sessions`, t, b);
export const updateSession = (t: string, id: string, b: UpsertSession) =>
  api<void>("PUT", `/api/admin/sessions/${id}`, t, b);
export const deleteSession = (t: string, id: string) => api<void>("DELETE", `/api/admin/sessions/${id}`, t);
export const reorderSessions = (t: string, programId: string, sessionIdsInOrder: string[]) =>
  api<void>("POST", `/api/admin/programs/${programId}/sessions/reorder`, t, { sessionIdsInOrder });

export const listBatches = (t: string, programId: string) =>
  api<AdminBatch[]>("GET", `/api/admin/programs/${programId}/batches`, t);
export const createBatch = (t: string, programId: string, b: UpsertBatch) =>
  api<AdminBatch>("POST", `/api/admin/programs/${programId}/batches`, t, b);
export const revokeEnrollment = (t: string, enrollmentId: string) =>
  api<void>("POST", `/api/admin/enrollments/${enrollmentId}/revoke`, t);

// ---- helpers ----

export function num(v: number | string): number {
  return typeof v === "string" ? Number(v) : v;
}

export function formatIdr(v: number | string): string {
  return "Rp " + num(v).toLocaleString("id-ID");
}

export function minutesLabel(seconds: number | string | null | undefined): string {
  if (seconds == null) return "—";
  return `${Math.max(1, Math.round(num(seconds) / 60))} mnt`;
}

export function totalDurationLabel(seconds: number | string): string {
  const mins = Math.round(num(seconds) / 60);
  if (mins < 60) return `${mins} menit`;
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  return m === 0 ? `${h} jam` : `${h} jam ${m} menit`;
}

export function fmtDateTime(iso: string): string {
  return new Date(iso).toLocaleString("id-ID", {
    day: "numeric", month: "long", year: "numeric", hour: "2-digit", minute: "2-digit",
  });
}

export const SESSION_TYPE_LABEL: Record<string, string> = {
  Video: "Video",
  Live: "Sesi Live",
  FinalAssessment: "Tes Akhir",
};

// ---------------------------------------------------------------- M5: attendance & operations

export type AttendanceRoster = components["schemas"]["AttendanceRosterDto"];
export type AttendanceRow = components["schemas"]["AttendanceRowDto"];
export type AdminEnrollmentList = components["schemas"]["AdminEnrollmentListDto"];
export type AdminEnrollment = components["schemas"]["AdminEnrollmentDto"];
export type AdminAttemptList = components["schemas"]["AdminAttemptListDto"];
export type AdminAttempt = components["schemas"]["AdminAttemptDto"];
export type AdminAttemptDetail = components["schemas"]["AdminAttemptDetailDto"];

export const getAttendanceRoster = (t: string, sessionId: string) =>
  api<AttendanceRoster>("GET", `/api/admin/sessions/${sessionId}/attendance`, t);

export const markAttendance = (t: string, sessionId: string, userIds: string[], attended: boolean) =>
  api<void>("POST", `/api/admin/sessions/${sessionId}/attendance`, t, { userIds, attended });

export const markAllAttendance = (t: string, sessionId: string) =>
  api<void>("POST", `/api/admin/sessions/${sessionId}/attendance/all`, t);

export const listAdminEnrollments = (
  t: string,
  q: { search?: string; status?: string; programId?: string; skip?: number; take?: number } = {},
) => {
  const p = new URLSearchParams();
  if (q.search) p.set("search", q.search);
  if (q.status) p.set("status", q.status);
  if (q.programId) p.set("programId", q.programId);
  p.set("skip", String(q.skip ?? 0));
  p.set("take", String(q.take ?? 25));
  return api<AdminEnrollmentList>("GET", `/api/admin/enrollments?${p}`, t);
};

export const grantEnrollment = (t: string, email: string, programId: string) =>
  api<void>("POST", "/api/admin/enrollments/grant", t, { email, programId });

export const listAdminAttempts = (
  t: string,
  q: { flaggedOnly?: boolean; programId?: string; skip?: number; take?: number } = {},
) => {
  const p = new URLSearchParams();
  if (q.flaggedOnly) p.set("flaggedOnly", "true");
  if (q.programId) p.set("programId", q.programId);
  p.set("skip", String(q.skip ?? 0));
  p.set("take", String(q.take ?? 25));
  return api<AdminAttemptList>("GET", `/api/admin/attempts?${p}`, t);
};

export const getAdminAttempt = (t: string, attemptId: string) =>
  api<AdminAttemptDetail>("GET", `/api/admin/attempts/${attemptId}`, t);

export const runLiveReminders = (t: string) =>
  api<{ emailsSent: number }>("POST", "/api/admin/live-reminders/run", t, {});
