"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { cn } from "@/lib/cn";
import { Badge, CheckIcon, XIcon } from "@/components/ui";
import { PLANS, formatIdr, type PlanTier } from "@/lib/pricing";
import { useAuth } from "@/components/auth/AuthProvider";
import { checkout, getPlans } from "@/lib/billing";

const ctaCls: Record<"primary" | "secondary" | "advanced", string> = {
  primary: "bg-primary text-primary-ink hover:bg-primary-hover",
  secondary: "border-[1.5px] border-primary bg-surface text-primary hover:bg-primary-soft",
  advanced: "bg-[#6D28D9] text-white hover:bg-[#5b21b6]",
};

const TIER_LEVEL: Record<PlanTier["key"], number> = { free: 0, beginner: 1, intermediate: 2, advanced: 3 };

export function PricingTiers() {
  const router = useRouter();
  const { status, accessToken } = useAuth();
  const [annual, setAnnual] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // DB-backed plans give us the real plan ids (display copy stays in PLANS).
  const { data: apiPlans } = useQuery({ queryKey: ["plans"], queryFn: getPlans, staleTime: 5 * 60 * 1000 });

  function planIdFor(key: PlanTier["key"]): string | undefined {
    return apiPlans?.find((p) => p.tierLevel === TIER_LEVEL[key])?.id;
  }

  async function onCta(plan: PlanTier) {
    setError(null);
    if (plan.key === "free") {
      router.push(status === "authenticated" ? "/app/dashboard" : "/register");
      return;
    }
    if (status !== "authenticated" || !accessToken) {
      router.push(`/login?next=${encodeURIComponent("/pricing")}`);
      return;
    }
    const planId = planIdFor(plan.key);
    if (!planId) {
      setError("Paket belum tersedia. Coba lagi sebentar lagi.");
      return;
    }
    setBusy(plan.key);
    try {
      const session = await checkout(accessToken, planId, annual ? "Annual" : "Monthly");
      window.location.href = session.checkoutUrl;
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Gagal memulai checkout.";
      // Already subscribed → send them to manage their plan instead.
      if (msg.toLowerCase().includes("langganan aktif")) router.push("/app/account");
      else setError(msg);
      setBusy(null);
    }
  }

  return (
    <div className="flex flex-col gap-10">
      {/* Toggle */}
      <div className="flex flex-wrap items-center justify-center gap-3.5">
        <span className={cn("text-sm font-bold", annual ? "text-ink-subtle" : "text-ink")}>Bulanan</span>
        <button
          type="button"
          role="switch"
          aria-checked={annual}
          aria-label="Beralih ke tagihan tahunan"
          onClick={() => setAnnual((a) => !a)}
          className={cn(
            "relative h-[30px] w-[54px] rounded-full outline-none transition-colors focus-visible:ring-[3px] focus-visible:ring-primary-soft",
            annual ? "bg-primary" : "bg-[#C9D4E2]"
          )}
        >
          <span
            className={cn(
              "absolute top-[3px] h-6 w-6 rounded-full bg-white shadow transition-all",
              annual ? "left-[27px]" : "left-[3px]"
            )}
          />
        </button>
        <span className={cn("text-sm font-bold", annual ? "text-ink" : "text-ink-subtle")}>Tahunan</span>
        <Badge tone="success">Hemat 2 bulan</Badge>
      </div>

      {error && (
        <p className="mx-auto rounded-base border border-danger/30 bg-danger-soft px-4 py-2.5 text-center text-sm text-danger">
          {error}
        </p>
      )}

      {/* Tier cards */}
      <div className="grid items-start gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {PLANS.map((plan) => {
          const isFree = plan.monthly == null;
          const price = isFree ? "Rp 0" : formatIdr((annual ? plan.annualMonthly : plan.monthly) as number);
          const per = isFree ? "/ selamanya" : annual ? "/ bulan, ditagih tahunan" : "/ bulan";

          return (
            <article
              key={plan.key}
              className={cn(
                "relative flex flex-col gap-[18px] rounded-lg border bg-surface p-6",
                plan.popular ? "border-2 border-primary shadow-lg" : "border-border shadow-sm"
              )}
            >
              {plan.popular && <Badge promo="popular" className="absolute -top-3 left-1/2 -translate-x-1/2 shadow-sm" />}

              <div className="flex flex-col gap-2">
                <Badge tier={plan.badge} className="w-fit" />
                <h3 className="text-xl font-extrabold text-ink">{plan.name}</h3>
                <p className="min-h-[38px] text-[13px] leading-snug text-ink-muted">{plan.tagline}</p>
              </div>

              <div className="flex flex-wrap items-baseline gap-x-1.5">
                <span className="whitespace-nowrap text-[32px] font-extrabold leading-tight tracking-tight">{price}</span>
                <span className="whitespace-nowrap text-[13px] text-ink-muted">{per}</span>
              </div>

              <button
                type="button"
                onClick={() => onCta(plan)}
                disabled={busy === plan.key}
                className={cn(
                  "rounded-sm px-3 py-3 text-center text-sm font-bold transition-colors disabled:opacity-60",
                  ctaCls[plan.ctaVariant]
                )}
              >
                {busy === plan.key ? "Memproses…" : plan.cta}
              </button>

              <ul className="flex flex-col gap-2.5">
                {plan.features.map((f) => (
                  <li key={f.text} className="flex gap-2.5 text-[13.5px] leading-snug">
                    {f.included ? (
                      <CheckIcon
                        size={18}
                        strokeWidth={2.4}
                        className={cn(
                          "mt-0.5 shrink-0",
                          f.lead ? (f.accent === "advanced" ? "text-[#6D28D9]" : "text-primary") : "text-success"
                        )}
                      />
                    ) : (
                      <XIcon size={18} strokeWidth={2.2} className="mt-0.5 shrink-0 text-ink-subtle" />
                    )}
                    <span
                      className={cn(
                        f.lead
                          ? f.accent === "advanced"
                            ? "font-bold text-[#6D28D9]"
                            : "font-bold text-primary"
                          : f.strong
                            ? "font-semibold text-ink"
                            : f.included
                              ? "text-ink-muted"
                              : "text-ink-subtle"
                      )}
                    >
                      {f.text}
                    </span>
                  </li>
                ))}
              </ul>
            </article>
          );
        })}
      </div>
    </div>
  );
}
