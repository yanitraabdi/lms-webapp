// Billing client. Types come from the generated OpenAPI client (do not hand-write).
// Authenticated calls go browser → .NET API directly with the in-memory access token.
import type { components } from "@/api-client/schema";

export type PlanDto = components["schemas"]["PlanDto"];
export type MySubscription = components["schemas"]["MySubscriptionDto"];
export type CheckoutSession = components["schemas"]["CheckoutSession"];
export type UpgradePreview = components["schemas"]["UpgradePreviewDto"];
export type BillingHistoryItem = components["schemas"]["BillingHistoryItemDto"];
export type BillingCycle = components["schemas"]["BillingCycle"];
export type SubscriptionStatus = components["schemas"]["SubscriptionStatus"];

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

async function problem(res: Response, fallback: string): Promise<Error> {
  const body = await res.json().catch(() => null);
  const title =
    body && typeof body === "object" && typeof (body as Record<string, unknown>).title === "string"
      ? (body as Record<string, string>).title
      : fallback;
  return new Error(title);
}

function auth(token: string): HeadersInit {
  return { Authorization: `Bearer ${token}`, "content-type": "application/json" };
}

export async function getPlans(): Promise<PlanDto[]> {
  const res = await fetch(`${API}/api/plans`, { cache: "no-store" });
  if (!res.ok) throw await problem(res, "Gagal memuat paket.");
  return res.json();
}

export async function getMySubscription(token: string): Promise<MySubscription | null> {
  const res = await fetch(`${API}/api/subscriptions/me`, { headers: auth(token), cache: "no-store" });
  if (res.status === 204) return null;
  if (!res.ok) throw await problem(res, "Gagal memuat langganan.");
  return res.json();
}

export async function getBillingHistory(token: string): Promise<BillingHistoryItem[]> {
  const res = await fetch(`${API}/api/subscriptions/billing-history`, { headers: auth(token), cache: "no-store" });
  if (!res.ok) throw await problem(res, "Gagal memuat riwayat tagihan.");
  return res.json();
}

export async function checkout(token: string, planId: string, billingCycle: BillingCycle): Promise<CheckoutSession> {
  const res = await fetch(`${API}/api/subscriptions/checkout`, {
    method: "POST",
    headers: auth(token),
    body: JSON.stringify({ planId, billingCycle }),
  });
  if (!res.ok) throw await problem(res, "Gagal memulai checkout.");
  return res.json();
}

export async function previewUpgrade(token: string, newPlanId: string): Promise<UpgradePreview> {
  const res = await fetch(`${API}/api/subscriptions/upgrade-preview?newPlanId=${encodeURIComponent(newPlanId)}`, {
    headers: auth(token),
    cache: "no-store",
  });
  if (!res.ok) throw await problem(res, "Gagal menghitung biaya upgrade.");
  return res.json();
}

export async function upgrade(token: string, newPlanId: string): Promise<CheckoutSession> {
  const res = await fetch(`${API}/api/subscriptions/upgrade`, {
    method: "POST",
    headers: auth(token),
    body: JSON.stringify({ newPlanId }),
  });
  if (!res.ok) throw await problem(res, "Gagal memulai upgrade.");
  return res.json();
}

export async function downgrade(token: string, newPlanId: string): Promise<void> {
  const res = await fetch(`${API}/api/subscriptions/downgrade`, {
    method: "POST",
    headers: auth(token),
    body: JSON.stringify({ newPlanId }),
  });
  if (!res.ok) throw await problem(res, "Gagal menjadwalkan downgrade.");
}

export async function cancelSubscription(token: string): Promise<void> {
  const res = await fetch(`${API}/api/subscriptions/cancel`, { method: "POST", headers: auth(token) });
  if (!res.ok) throw await problem(res, "Gagal membatalkan langganan.");
}

/** Dev-only: simulate the payment provider completing a checkout (Billing:Provider=dev). */
export async function simulateDevPayment(providerRef: string, succeed: boolean): Promise<void> {
  const action = succeed ? "succeed" : "fail";
  const res = await fetch(`${API}/api/dev/payments/${encodeURIComponent(providerRef)}/${action}`, { method: "POST" });
  if (!res.ok) throw await problem(res, "Simulasi pembayaran gagal.");
}

// ---- helpers ----

export function num(v: number | string): number {
  return typeof v === "string" ? Number(v) : v;
}

export function formatIdr(v: number | string): string {
  return "Rp " + num(v).toLocaleString("id-ID");
}

export const STATUS_LABEL: Record<SubscriptionStatus, string> = {
  Active: "Aktif",
  PastDue: "Tertunggak",
  Grace: "Masa tenggang",
  Canceled: "Dibatalkan",
  Expired: "Berakhir",
};
