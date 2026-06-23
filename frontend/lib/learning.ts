// Learning/player/certificate client. Types come from the generated OpenAPI client.
import type { components } from "@/api-client/schema";

export type PlaybackTicket = components["schemas"]["PlaybackTicketDto"];
export type PlayerContext = components["schemas"]["PlayerContextDto"];
export type PlaylistItem = components["schemas"]["PlaylistItemDto"];
export type ModuleProgress = components["schemas"]["ModuleProgressDto"];
export type Dashboard = components["schemas"]["DashboardDto"];
export type LevelProgress = components["schemas"]["LevelProgressDto"];
export type ContinueModule = components["schemas"]["ContinueModuleDto"];
export type Certificate = components["schemas"]["CertificateDto"];
export type CertificateVerify = components["schemas"]["CertificateVerifyDto"];

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

export async function getPlayback(token: string, moduleId: string): Promise<PlaybackTicket> {
  const res = await fetch(`${baseUrl()}/api/modules/${moduleId}/playback`, { method: "POST", headers: auth(token) });
  if (!res.ok) throw await problem(res, "Gagal memutar video.");
  return res.json();
}

export async function getPlayerContext(token: string, moduleId: string): Promise<PlayerContext> {
  const res = await fetch(`${baseUrl()}/api/modules/${moduleId}/player`, { headers: auth(token), cache: "no-store" });
  if (!res.ok) throw await problem(res, "Gagal memuat modul.");
  return res.json();
}

export async function getProgress(token: string, moduleId: string): Promise<ModuleProgress | null> {
  const res = await fetch(`${baseUrl()}/api/modules/${moduleId}/progress`, { headers: auth(token), cache: "no-store" });
  if (res.status === 204) return null;
  if (!res.ok) throw await problem(res, "Gagal memuat progres.");
  return res.json();
}

export async function saveProgress(token: string, moduleId: string, positionSeconds: number, percent: number): Promise<ModuleProgress> {
  const res = await fetch(`${baseUrl()}/api/modules/${moduleId}/progress`, {
    method: "PUT",
    headers: auth(token),
    body: JSON.stringify({ positionSeconds: Math.round(positionSeconds), percent }),
  });
  if (!res.ok) throw await problem(res, "Gagal menyimpan progres.");
  return res.json();
}

export async function getDashboard(token: string): Promise<Dashboard> {
  const res = await fetch(`${baseUrl()}/api/me/dashboard`, { headers: auth(token), cache: "no-store" });
  if (!res.ok) throw await problem(res, "Gagal memuat dasbor.");
  return res.json();
}

export async function getMyCertificates(token: string): Promise<Certificate[]> {
  const res = await fetch(`${baseUrl()}/api/me/certificates`, { headers: auth(token), cache: "no-store" });
  if (!res.ok) throw await problem(res, "Gagal memuat sertifikat.");
  return res.json();
}

/** Public — used by the SSR verify page (no auth). */
export async function verifyCertificate(code: string): Promise<CertificateVerify> {
  const res = await fetch(`${baseUrl()}/api/certificates/verify/${encodeURIComponent(code)}`, { cache: "no-store" });
  if (!res.ok) throw new Error(`verify ${res.status}`);
  return res.json();
}

export async function downloadCertificatePdf(token: string, certId: string, fileName: string): Promise<void> {
  const res = await fetch(`${baseUrl()}/api/certificates/${certId}/pdf`, { headers: { Authorization: `Bearer ${token}` } });
  if (!res.ok) throw await problem(res, "Gagal mengunduh PDF.");
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

// ---- helpers ----

export function num(v: number | string): number {
  return typeof v === "string" ? Number(v) : v;
}

export function minutesLabel(seconds: number | string): string {
  return `${Math.max(1, Math.round(num(seconds) / 60))} mnt`;
}

export function fmtDate(iso: string): string {
  return new Date(iso).toLocaleDateString("id-ID", { day: "numeric", month: "long", year: "numeric" });
}
