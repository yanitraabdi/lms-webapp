// Content + onboarding client. Types from the generated OpenAPI client.
import type { components } from "@/api-client/schema";

export type FaqItem = components["schemas"]["FaqItemDto"];
export type OnboardingState = components["schemas"]["OnboardingStateDto"];

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

export async function getFaq(opts?: { revalidate?: number }): Promise<FaqItem[]> {
  const init: RequestInit & { next?: { revalidate: number } } = {};
  if (opts?.revalidate != null) init.next = { revalidate: opts.revalidate };
  else init.cache = "no-store";
  const res = await fetch(`${baseUrl()}/api/faq`, init);
  if (!res.ok) throw await problem(res, "Gagal memuat FAQ.");
  return res.json();
}

export async function submitContact(req: { name: string; email: string; message: string }): Promise<void> {
  const res = await fetch(`${baseUrl()}/api/contact`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(req),
  });
  if (!res.ok) throw await problem(res, "Gagal mengirim pesan.");
}

export async function submitFeedback(token: string | null, req: { message: string; context?: string }): Promise<void> {
  const headers: HeadersInit = { "content-type": "application/json" };
  if (token) (headers as Record<string, string>).Authorization = `Bearer ${token}`;
  const res = await fetch(`${baseUrl()}/api/feedback`, { method: "POST", headers, body: JSON.stringify(req) });
  if (!res.ok) throw await problem(res, "Gagal mengirim masukan.");
}

export async function getOnboardingState(token: string): Promise<OnboardingState> {
  const res = await fetch(`${baseUrl()}/api/onboarding`, { headers: auth(token), cache: "no-store" });
  if (!res.ok) throw await problem(res, "Gagal memuat status onboarding.");
  return res.json();
}

export async function completeTour(token: string, tourKey: string, status: "Completed" | "Skipped"): Promise<void> {
  const res = await fetch(`${baseUrl()}/api/onboarding/tour`, {
    method: "POST",
    headers: auth(token),
    body: JSON.stringify({ tourKey, status }),
  });
  if (!res.ok) throw await problem(res, "Gagal menyimpan status tur.");
}

export async function saveSurvey(
  token: string,
  body: { role?: string; goals: string[]; preferredTools: string[] }
): Promise<void> {
  const res = await fetch(`${baseUrl()}/api/onboarding/survey`, {
    method: "POST",
    headers: auth(token),
    body: JSON.stringify(body),
  });
  if (!res.ok) throw await problem(res, "Gagal menyimpan survei.");
}
