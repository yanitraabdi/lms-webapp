// Engagement client (M7): notifications + preferences, notes/bookmarks, quizzes, module ratings.
// Types come from the generated OpenAPI client.
import type { components } from "@/api-client/schema";

export type Notification = components["schemas"]["NotificationDto"];
export type NotificationList = components["schemas"]["NotificationListDto"];
export type NotificationPref = components["schemas"]["NotificationPrefDto"];
export type VideoNote = components["schemas"]["VideoNoteDto"];
export type ModuleFeedback = components["schemas"]["ModuleFeedbackDto"];
export type Quiz = components["schemas"]["QuizDto"];
export type QuizQuestion = components["schemas"]["QuizQuestionDto"];
export type QuizResult = components["schemas"]["QuizResultDto"];

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

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
  const res = await fetch(`${API}${path}`, {
    method,
    headers: auth(token),
    body: body === undefined ? undefined : JSON.stringify(body),
    cache: "no-store",
  });
  if (!res.ok) throw await problem(res, "Operasi gagal.");
  return (res.status === 204 ? undefined : await res.json()) as T;
}

// ---- notifications ----
export const listNotifications = (t: string, unreadOnly = false) =>
  api<NotificationList>("GET", `/api/notifications${unreadOnly ? "?unreadOnly=true" : ""}`, t);
export const markNotificationRead = (t: string, id: string) =>
  api<void>("POST", `/api/notifications/${id}/read`, t);
export const markAllNotificationsRead = (t: string) =>
  api<void>("POST", "/api/notifications/read-all", t);
export const getPreferences = (t: string) =>
  api<NotificationPref[]>("GET", "/api/notifications/preferences", t);
export const updatePreferences = (t: string, preferences: NotificationPref[]) =>
  api<void>("PUT", "/api/notifications/preferences", t, { preferences });

// ---- notes & bookmarks ----
export const listNotes = (t: string, moduleId: string) =>
  api<VideoNote[]>("GET", `/api/modules/${moduleId}/notes`, t);
export const createNote = (t: string, moduleId: string, b: { timestampSeconds: number; type: "Note" | "Bookmark"; text?: string | null }) =>
  api<VideoNote>("POST", `/api/modules/${moduleId}/notes`, t, b);
export const deleteNote = (t: string, noteId: string) =>
  api<void>("DELETE", `/api/notes/${noteId}`, t);

// ---- quiz (learner) ----
export async function getQuiz(t: string, moduleId: string): Promise<Quiz | null> {
  const res = await fetch(`${API}/api/modules/${moduleId}/quiz`, { headers: auth(t), cache: "no-store" });
  if (res.status === 204) return null;
  if (!res.ok) throw await problem(res, "Gagal memuat kuis.");
  return res.json();
}
export const submitQuiz = (t: string, moduleId: string, answers: number[]) =>
  api<QuizResult>("POST", `/api/modules/${moduleId}/quiz/attempt`, t, { answers });

// ---- module ratings ----
export const getFeedback = (t: string, moduleId: string) =>
  api<ModuleFeedback>("GET", `/api/modules/${moduleId}/feedback`, t);
export const rateModule = (t: string, moduleId: string, rating: number, comment?: string | null) =>
  api<void>("PUT", `/api/modules/${moduleId}/feedback`, t, { rating, comment });

// ---- labels (mirror of server-side NotificationCategories) ----
export const CATEGORY_LABELS: Record<string, string> = {
  progress: "Progres & sertifikat",
  content: "Modul & konten baru",
  billing: "Tagihan & langganan",
  promo: "Promosi & tips",
};
export const CATEGORY_ORDER = ["progress", "content", "billing", "promo"];
export const CHANNEL_LABELS: Record<string, string> = { InApp: "Dalam aplikasi", Email: "Email" };

export function num(v: number | string): number {
  return typeof v === "string" ? Number(v) : v;
}

export function timeAgo(iso: string): string {
  const diff = (Date.now() - new Date(iso).getTime()) / 1000;
  if (diff < 60) return "baru saja";
  if (diff < 3600) return `${Math.floor(diff / 60)} mnt lalu`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} jam lalu`;
  return new Date(iso).toLocaleDateString("id-ID", { day: "numeric", month: "short" });
}

export function clockLabel(seconds: number | string): string {
  const s = Math.max(0, Math.round(num(seconds)));
  const m = Math.floor(s / 60);
  const r = s % 60;
  return `${m}:${r.toString().padStart(2, "0")}`;
}
