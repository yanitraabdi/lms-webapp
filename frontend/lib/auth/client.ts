// Client helpers for the public (no-cookie) auth endpoints that call the .NET API
// directly: email verification, resend, forgot/reset password.
export const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

export interface JsonResult {
  ok: boolean;
  status: number;
  data: unknown;
}

export async function postJson(path: string, body: unknown): Promise<JsonResult> {
  const res = await fetch(`${apiBaseUrl}${path}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });
  const data = await res.json().catch(() => null);
  return { ok: res.ok, status: res.status, data };
}

/** Pull a human message out of an RFC-7807 problem-details body. */
export function problemMessage(data: unknown, fallback: string): string {
  if (data && typeof data === "object") {
    const title = (data as Record<string, unknown>).title;
    if (typeof title === "string") return title;
  }
  return fallback;
}
