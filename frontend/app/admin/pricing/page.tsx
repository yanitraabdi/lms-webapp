"use client";

import { useMemo, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Spinner, ErrorState, CheckIcon, type Tier } from "@/components/ui";
import { formatIdr, getAdminPlans, num, updatePlanPrices } from "@/lib/admin";

const TIER_BADGE: Record<number, Tier> = { 0: "free", 1: "beginner", 2: "intermediate", 3: "advanced" };

export default function AdminPricingPage() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();
  const plans = useQuery({ queryKey: ["admin-plans"], queryFn: () => getAdminPlans(token!), enabled: !!token });
  const [edits, setEdits] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const paid = useMemo(
    () => (plans.data ?? []).filter((p) => num(p.tierLevel) >= 1).sort((a, b) => num(a.tierLevel) - num(b.tierLevel)),
    [plans.data]
  );

  async function save() {
    if (!token) return;
    setError(null);
    setSaving(true);
    try {
      const items = paid.map((p) => ({
        planId: p.id,
        priceMonthly: edits[p.id] == null ? num(p.priceMonthly) : Number(edits[p.id].replace(/\D/g, "")),
        priceAnnual: num(p.priceAnnual),
      }));
      await updatePlanPrices(token, items);
      setEdits({});
      await qc.invalidateQueries({ queryKey: ["admin-plans"] });
      setSaved(true);
      setTimeout(() => setSaved(false), 2200);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Couldn't save.");
    } finally {
      setSaving(false);
    }
  }

  if (!token || plans.isPending) return <div className="flex min-h-[160px] items-center justify-center"><Spinner size={22} /></div>;
  if (plans.isError) return <ErrorState title="Couldn't load plans" action={<Button variant="neutral" size="sm" onClick={() => plans.refetch()}>Reload</Button>} />;

  return (
    <div className="flex flex-col gap-4">
      <p className="text-xs text-ink-subtle">Changes apply to new subscriptions &amp; renewals (existing subscribers keep their locked price until renewal).</p>
      <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-3">
        {paid.map((p) => (
          <div key={p.id} className="flex flex-col gap-2 rounded-base border border-border bg-surface p-4">
            <Badge tier={TIER_BADGE[num(p.tierLevel)]} className="w-fit" />
            <label className="text-[11.5px] text-ink-muted">Price / month (Rp)</label>
            <input
              inputMode="numeric"
              value={edits[p.id] ?? formatIdr(p.priceMonthly)}
              onChange={(e) => setEdits((s) => ({ ...s, [p.id]: e.target.value }))}
              className="rounded-sm border border-border bg-surface px-3 py-2.5 text-[15px] font-bold text-ink outline-none focus:border-primary"
            />
          </div>
        ))}
      </div>
      <div className="flex items-center gap-3">
        <Button loading={saving} onClick={save}>Save prices</Button>
        {saved && <span className="inline-flex items-center gap-1.5 text-[12.5px] font-bold text-success"><CheckIcon size={15} strokeWidth={2.4} /> Prices saved</span>}
        {error && <span className="text-[12.5px] text-danger">{error}</span>}
      </div>
    </div>
  );
}
