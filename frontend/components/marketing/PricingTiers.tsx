"use client";

import { useState } from "react";
import Link from "next/link";
import { cn } from "@/lib/cn";
import { Badge, CheckIcon, XIcon } from "@/components/ui";
import { PLANS, formatIdr } from "@/lib/pricing";

const ctaCls: Record<"primary" | "secondary" | "advanced", string> = {
  primary: "bg-primary text-primary-ink hover:bg-primary-hover",
  secondary: "border-[1.5px] border-primary bg-surface text-primary hover:bg-primary-soft",
  advanced: "bg-[#6D28D9] text-white hover:bg-[#5b21b6]",
};

export function PricingTiers() {
  const [annual, setAnnual] = useState(false);

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

              <div className="flex items-baseline gap-1">
                <span className="text-[34px] font-extrabold tracking-tight">{price}</span>
                <span className="text-[13px] text-ink-muted">{per}</span>
              </div>

              <Link
                href="/register"
                className={cn("rounded-sm px-3 py-3 text-center text-sm font-bold transition-colors", ctaCls[plan.ctaVariant])}
              >
                {plan.cta}
              </Link>

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
