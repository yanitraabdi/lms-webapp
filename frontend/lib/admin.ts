// Admin client. Types from the generated OpenAPI client.
import type { components } from "@/api-client/schema";

export type AdminModule = components["schemas"]["AdminModuleDto"];
export type AdminPlan = components["schemas"]["AdminPlanDto"];

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

export async function getAdminModules(token: string, search?: string): Promise<AdminModule[]> {
  const q = search ? `?search=${encodeURIComponent(search)}` : "";
  const res = await fetch(`${API}/api/admin/modules${q}`, { headers: auth(token), cache: "no-store" });
  if (!res.ok) throw await problem(res, "Gagal memuat modul.");
  return res.json();
}

export async function setModulePublished(token: string, moduleId: string, published: boolean): Promise<void> {
  const res = await fetch(`${API}/api/admin/modules/${moduleId}/published`, {
    method: "PUT",
    headers: auth(token),
    body: JSON.stringify({ published }),
  });
  if (!res.ok) throw await problem(res, "Gagal memperbarui status.");
}

export async function getAdminPlans(token: string): Promise<AdminPlan[]> {
  const res = await fetch(`${API}/api/admin/plans`, { headers: auth(token), cache: "no-store" });
  if (!res.ok) throw await problem(res, "Gagal memuat paket.");
  return res.json();
}

export interface PlanPriceUpdate {
  planId: string;
  priceMonthly: number;
  priceAnnual: number;
}

export async function updatePlanPrices(token: string, items: PlanPriceUpdate[]): Promise<void> {
  const res = await fetch(`${API}/api/admin/plans/prices`, {
    method: "PUT",
    headers: auth(token),
    body: JSON.stringify({ items }),
  });
  if (!res.ok) throw await problem(res, "Gagal menyimpan harga.");
}

export function num(v: number | string): number {
  return typeof v === "string" ? Number(v) : v;
}

export function formatIdr(v: number | string): string {
  return num(v).toLocaleString("id-ID");
}

export function isAdminRole(role?: string | null): boolean {
  return role === "Admin" || role === "SuperAdmin";
}

// ============================ full admin CRUD ============================

export type Level = components["schemas"]["LevelDto"];
export type Track = components["schemas"]["TrackDto"];
export type Category = components["schemas"]["CategoryDto"];
export type Tag = components["schemas"]["TagDto"];
export type ModuleDetail = components["schemas"]["AdminModuleDetailDto"];
export type AdminResource = components["schemas"]["AdminResourceDto"];
export type AdminUserItem = components["schemas"]["AdminUserListItemDto"];
export type AdminUserList = components["schemas"]["AdminUserListDto"];
export type AdminUserDetail = components["schemas"]["AdminUserDetailDto"];
export type Analytics = components["schemas"]["AdminAnalyticsDto"];

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

// ---- levels ----
export const listLevels = (t: string) => api<Level[]>("GET", "/api/admin/levels", t);
export const createLevel = (t: string, b: UpsertLevel) => api<Level>("POST", "/api/admin/levels", t, b);
export const updateLevel = (t: string, id: string, b: UpsertLevel) => api<void>("PUT", `/api/admin/levels/${id}`, t, b);
export const deleteLevel = (t: string, id: string) => api<void>("DELETE", `/api/admin/levels/${id}`, t);

// ---- tracks ----
export const listTracks = (t: string, levelId?: string) =>
  api<Track[]>("GET", `/api/admin/tracks${levelId ? `?levelId=${levelId}` : ""}`, t);
export const createTrack = (t: string, b: UpsertTrack) => api<Track>("POST", "/api/admin/tracks", t, b);
export const updateTrack = (t: string, id: string, b: UpsertTrack) => api<void>("PUT", `/api/admin/tracks/${id}`, t, b);
export const deleteTrack = (t: string, id: string) => api<void>("DELETE", `/api/admin/tracks/${id}`, t);

// ---- modules ----
export const getModule = (t: string, id: string) => api<ModuleDetail>("GET", `/api/admin/modules/${id}`, t);
export const createModule = (t: string, b: ModuleInput) => api<ModuleDetail>("POST", "/api/admin/modules", t, b);
export const updateModule = (t: string, id: string, b: ModuleInput) => api<void>("PUT", `/api/admin/modules/${id}`, t, b);
export const deleteModule = (t: string, id: string) => api<void>("DELETE", `/api/admin/modules/${id}`, t);

// ---- categories / tags ----
export const listCategories = (t: string) => api<Category[]>("GET", "/api/admin/categories", t);
export const createCategory = (t: string, b: { name: string; slug?: string }) => api<Category>("POST", "/api/admin/categories", t, b);
export const updateCategory = (t: string, id: string, b: { name: string; slug?: string }) => api<void>("PUT", `/api/admin/categories/${id}`, t, b);
export const deleteCategory = (t: string, id: string) => api<void>("DELETE", `/api/admin/categories/${id}`, t);
export const listTags = (t: string) => api<Tag[]>("GET", "/api/admin/tags", t);
export const createTag = (t: string, b: { name: string; slug?: string }) => api<Tag>("POST", "/api/admin/tags", t, b);
export const updateTag = (t: string, id: string, b: { name: string; slug?: string }) => api<void>("PUT", `/api/admin/tags/${id}`, t, b);
export const deleteTag = (t: string, id: string) => api<void>("DELETE", `/api/admin/tags/${id}`, t);

// ---- resources ----
export const listResources = (t: string, moduleId: string) => api<AdminResource[]>("GET", `/api/admin/modules/${moduleId}/resources`, t);
export const createResource = (t: string, moduleId: string, b: { type: string; ref: string; title: string }) =>
  api<AdminResource>("POST", `/api/admin/modules/${moduleId}/resources`, t, b);
export const deleteResource = (t: string, id: string) => api<void>("DELETE", `/api/admin/resources/${id}`, t);

// ---- users ----
export const listUsers = (t: string, q: { search?: string; status?: string; tier?: number; skip?: number; take?: number } = {}) => {
  const p = new URLSearchParams();
  if (q.search) p.set("search", q.search);
  if (q.status) p.set("status", q.status);
  if (q.tier != null) p.set("tier", String(q.tier));
  p.set("skip", String(q.skip ?? 0));
  p.set("take", String(q.take ?? 25));
  return api<AdminUserList>("GET", `/api/admin/users?${p}`, t);
};
export const getUser = (t: string, id: string) => api<AdminUserDetail>("GET", `/api/admin/users/${id}`, t);
export const setUserStatus = (t: string, id: string, status: string) => api<void>("PUT", `/api/admin/users/${id}/status`, t, { status });
export const setUserRole = (t: string, id: string, role: string) => api<void>("PUT", `/api/admin/users/${id}/role`, t, { role });
export const grantPlan = (t: string, id: string, planId: string, days: number) => api<void>("POST", `/api/admin/users/${id}/grant`, t, { planId, days });
export const revokePlan = (t: string, id: string) => api<void>("POST", `/api/admin/users/${id}/revoke`, t);

// ---- analytics ----
export const getAnalytics = (t: string) => api<Analytics>("GET", "/api/admin/analytics", t);

// ---- quiz authoring (M7) ----
export type AdminQuiz = components["schemas"]["AdminQuizDto"];
export type AdminQuizQuestion = components["schemas"]["AdminQuizQuestionDto"];
export type QuizQuestionInput = components["schemas"]["QuizQuestionInput"];
export interface UpsertQuiz { passThreshold: number; isActive: boolean; questions: QuizQuestionInput[]; }

export async function getAdminQuiz(t: string, moduleId: string): Promise<AdminQuiz | null> {
  const res = await fetch(`${API}/api/admin/modules/${moduleId}/quiz`, { headers: auth(t), cache: "no-store" });
  if (res.status === 204) return null;
  if (!res.ok) throw await problem(res, "Gagal memuat kuis.");
  return res.json();
}
export const upsertQuiz = (t: string, moduleId: string, b: UpsertQuiz) => api<void>("PUT", `/api/admin/modules/${moduleId}/quiz`, t, b);
export const deleteAdminQuiz = (t: string, moduleId: string) => api<void>("DELETE", `/api/admin/modules/${moduleId}/quiz`, t);

export interface UpsertLevel { name: string; slug?: string; requiredPlanTier: number; orderIndex: number; published: boolean; }
export interface UpsertTrack { levelId: string; name: string; slug?: string; orderIndex: number; }
export interface ModuleInput {
  trackId: string; categoryId?: string | null; title: string; slug?: string; description: string; summary?: string | null;
  durationSeconds: number; providerAssetId?: string | null; thumbnailUrl?: string | null; orderIndex: number;
  isPreview: boolean; requiredPlanTier: number; published: boolean; tagIds?: string[];
}
