"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import {
  Badge,
  Button,
  Modal,
  Spinner,
  ErrorState,
  InfoIcon,
  type Tier,
} from "@/components/ui";
import {
  cancelSubscription,
  downgrade,
  formatIdr,
  getBillingHistory,
  getMySubscription,
  getPlans,
  num,
  previewUpgrade,
  upgrade,
  STATUS_LABEL,
  type MySubscription,
  type PlanDto,
  type UpgradePreview,
} from "@/lib/billing";

const TIER_BADGE: Record<number, Tier> = { 0: "free", 1: "beginner", 2: "intermediate", 3: "advanced" };

function fmtDate(iso: string): string {
  return new Date(iso).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" });
}

export default function AccountPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login?next=/app/account");
  }, [status, router]);

  if (status !== "authenticated" || !accessToken) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-bg">
        <Spinner size={24} />
      </div>
    );
  }
  return <BillingScreen token={accessToken} />;
}

function BillingScreen({ token }: { token: string }) {
  const qc = useQueryClient();
  const sub = useQuery({ queryKey: ["me-sub"], queryFn: () => getMySubscription(token) });
  const history = useQuery({ queryKey: ["billing-history"], queryFn: () => getBillingHistory(token) });
  const plans = useQuery({ queryKey: ["plans"], queryFn: getPlans });

  const [cancelOpen, setCancelOpen] = useState(false);
  const [changeMode, setChangeMode] = useState<"upgrade" | "downgrade" | null>(null);
  const [error, setError] = useState<string | null>(null);

  function refetch() {
    qc.invalidateQueries({ queryKey: ["me-sub"] });
    qc.invalidateQueries({ queryKey: ["billing-history"] });
  }

  return (
    <div className="min-h-screen bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex h-[58px] max-w-5xl items-center justify-between px-6">
          <Link href="/" className="flex items-center gap-2.5">
            <span className="flex h-[30px] w-[30px] items-center justify-center rounded-lg bg-primary text-[15px] font-extrabold text-primary-ink">A</span>
            <span className="text-[15px] font-extrabold">AI Productivity Academy</span>
          </Link>
          <Link href="/catalog" className="text-[13px] font-bold text-ink-muted hover:text-ink">
            Katalog
          </Link>
        </div>
      </header>

      <div className="mx-auto max-w-5xl px-6 pb-16 pt-7">
        <div className="mb-[22px] flex flex-col gap-1.5">
          <h1 className="text-[26px] font-extrabold tracking-tight">Langganan &amp; tagihan</h1>
          <p className="text-sm text-ink-muted">Kelola paket dan lihat riwayat tagihan Anda.</p>
        </div>

        {error && (
          <p className="mb-5 rounded-base border border-danger/30 bg-danger-soft px-4 py-2.5 text-sm text-danger">{error}</p>
        )}

        {sub.isPending || plans.isPending ? (
          <div className="flex min-h-[200px] items-center justify-center"><Spinner size={24} /></div>
        ) : sub.isError ? (
          <ErrorState title="Gagal memuat langganan" action={<Button variant="neutral" size="sm" onClick={() => sub.refetch()}>Muat ulang</Button>} />
        ) : (
          <div className="grid grid-cols-1 gap-6 md:grid-cols-[1fr_320px]">
            <div className="flex flex-col gap-[22px]">
              {sub.data ? (
                <CurrentPlanCard
                  sub={sub.data}
                  plans={plans.data ?? []}
                  onUpgrade={() => setChangeMode("upgrade")}
                  onDowngrade={() => setChangeMode("downgrade")}
                  onCancel={() => setCancelOpen(true)}
                />
              ) : (
                <FreePlanCard />
              )}
              <BillingHistoryCard
                items={(history.data ?? []).map((h) => ({
                  id: h.id,
                  date: fmtDate(h.createdAt),
                  amount: formatIdr(h.amountIdr),
                  kind: h.kind === "ProrationUpgrade" ? "Upgrade (prorata)" : "Langganan",
                  status: h.status,
                }))}
                loading={history.isPending}
              />
            </div>

            <aside className="flex flex-col gap-4">
              <div className="flex flex-col gap-3.5 rounded-lg border border-border bg-surface p-5 shadow-sm">
                <span className="text-xs font-bold uppercase tracking-wide text-ink-subtle">Metode pembayaran</span>
                <p className="text-[13px] leading-relaxed text-ink-muted">
                  Metode pembayaran dikelola oleh penyedia pembayaran saat checkout.
                </p>
              </div>
              <div className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-5 shadow-sm">
                <span className="text-[13px] font-extrabold">Butuh bantuan?</span>
                <p className="text-[12.5px] leading-relaxed text-ink-muted">
                  Pertanyaan soal tagihan atau pengembalian dana? Tim kami siap membantu.
                </p>
                <Link href="/help" className="text-[12.5px] font-bold text-primary hover:underline">Hubungi dukungan →</Link>
              </div>
            </aside>
          </div>
        )}
      </div>

      {/* Cancel confirmation */}
      <Modal
        open={cancelOpen}
        onClose={() => setCancelOpen(false)}
        title="Batalkan langganan?"
        footer={
          <>
            <Button variant="neutral" fullWidth onClick={() => setCancelOpen(false)}>Tetap berlangganan</Button>
            <Button
              variant="danger"
              fullWidth
              onClick={async () => {
                setError(null);
                try {
                  await cancelSubscription(token);
                  setCancelOpen(false);
                  refetch();
                } catch (e) {
                  setError(e instanceof Error ? e.message : "Gagal membatalkan.");
                }
              }}
            >
              Ya, batalkan
            </Button>
          </>
        }
      >
        Anda tetap memiliki akses hingga{" "}
        <strong className="text-ink">{sub.data ? fmtDate(sub.data.currentPeriodEnd) : "akhir periode"}</strong>. Setelah
        itu modul berbayar terkunci. Sertifikat yang sudah terbit tetap berlaku selamanya.
      </Modal>

      {/* Upgrade / downgrade */}
      {changeMode && sub.data && (
        <ChangePlanModal
          mode={changeMode}
          token={token}
          sub={sub.data}
          plans={plans.data ?? []}
          onClose={() => setChangeMode(null)}
          onDone={() => {
            setChangeMode(null);
            refetch();
          }}
          onError={(m) => setError(m)}
        />
      )}
    </div>
  );
}

function CurrentPlanCard({
  sub,
  plans,
  onUpgrade,
  onDowngrade,
  onCancel,
}: {
  sub: MySubscription;
  plans: PlanDto[];
  onUpgrade: () => void;
  onDowngrade: () => void;
  onCancel: () => void;
}) {
  const tier = num(sub.tierLevel);
  const monthly = sub.billingCycle === "Annual" ? num(sub.priceLockedIdr) / 12 : num(sub.priceLockedIdr);
  const canUpgrade = plans.some((p) => num(p.tierLevel) > tier);
  const canDowngrade = plans.some((p) => num(p.tierLevel) >= 1 && num(p.tierLevel) < tier);
  const plannedName = sub.plannedPlanName;

  return (
    <div className="flex flex-col gap-5 rounded-lg border border-border bg-surface p-6 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-2">
          <div className="flex items-center gap-2.5">
            <Badge tier={TIER_BADGE[tier] ?? "beginner"}>{sub.planName}</Badge>
            <Badge status={sub.status === "Active" ? "in-progress" : sub.status === "Expired" ? "locked" : "not-started"}>
              {STATUS_LABEL[sub.status]}
            </Badge>
          </div>
          <div className="flex items-baseline gap-1.5">
            <span className="text-[28px] font-extrabold tracking-tight">{formatIdr(sub.priceLockedIdr)}</span>
            <span className="text-[13px] text-ink-muted">/ {sub.billingCycle === "Annual" ? "tahun" : "bulan"}</span>
          </div>
        </div>
        {canUpgrade && <Button onClick={onUpgrade}>Tingkatkan paket</Button>}
      </div>

      <div className="flex flex-wrap gap-6 border-t border-surface-2 pt-4">
        <Fact label="Tagihan berikutnya" value={fmtDate(sub.currentPeriodEnd)} />
        <Fact label="Siklus" value={sub.billingCycle === "Annual" ? "Tahunan" : "Bulanan"} />
        <Fact label="Harga terkunci" value={`${formatIdr(Math.round(monthly))} / bulan`} />
      </div>

      {plannedName && (
        <div className="flex gap-2.5 rounded-base border border-[#C9DBFF] bg-primary-soft px-3.5 py-3 text-[12.5px] text-ink">
          <InfoIcon size={18} className="shrink-0 text-primary" />
          <span>
            Dijadwalkan turun ke <strong>{plannedName}</strong> mulai {fmtDate(sub.currentPeriodEnd)}.
          </span>
        </div>
      )}

      <div className="flex gap-2.5 rounded-base border border-[#C9DBFF] bg-primary-soft px-3.5 py-3 text-[12.5px] text-ink">
        <InfoIcon size={18} className="shrink-0 text-primary" />
        <span>
          <strong>Upgrade langsung aktif</strong> dan ditagih prorata. <strong>Downgrade</strong> berlaku pada periode
          tagihan berikutnya.
        </span>
      </div>

      <div className="flex flex-wrap gap-2.5">
        {canDowngrade && (
          <Button variant="neutral" size="sm" onClick={onDowngrade}>Turunkan paket</Button>
        )}
        <Button variant="neutral" size="sm" className="border-[#F1C9C9] text-danger hover:bg-danger-soft" onClick={onCancel}>
          Batalkan langganan
        </Button>
      </div>
    </div>
  );
}

function FreePlanCard() {
  return (
    <div className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-6 shadow-sm">
      <div className="flex items-center gap-2.5">
        <Badge tier="free" />
        <span className="text-sm text-ink-muted">Anda belum berlangganan paket berbayar.</span>
      </div>
      <p className="text-sm leading-relaxed text-ink-muted">
        Tingkatkan untuk membuka seluruh modul dan mendapatkan sertifikat.
      </p>
      <Link
        href="/pricing"
        className="w-fit rounded-sm bg-primary px-4 py-2.5 text-sm font-bold text-primary-ink transition-colors hover:bg-primary-hover"
      >
        Lihat paket
      </Link>
    </div>
  );
}

function BillingHistoryCard({
  items,
  loading,
}: {
  items: Array<{ id: string; date: string; amount: string; kind: string; status: string }>;
  loading: boolean;
}) {
  return (
    <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
      <div className="border-b border-border px-[22px] py-[18px]">
        <h2 className="text-base font-extrabold">Riwayat tagihan</h2>
      </div>
      {loading ? (
        <div className="flex min-h-[120px] items-center justify-center"><Spinner size={20} /></div>
      ) : items.length === 0 ? (
        <p className="px-[22px] py-8 text-center text-sm text-ink-muted">Belum ada tagihan.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[480px] border-collapse">
            <thead>
              <tr className="bg-surface-2 text-left text-xs font-bold text-ink-muted">
                <th className="px-[22px] py-3">Tanggal</th>
                <th className="px-3 py-3">Keterangan</th>
                <th className="px-3 py-3">Jumlah</th>
                <th className="px-3 py-3">Status</th>
              </tr>
            </thead>
            <tbody className="text-[13.5px]">
              {items.map((it, i) => (
                <tr key={it.id} className={"border-t border-border" + (i % 2 ? " bg-[#FBFCFE]" : "")}>
                  <td className="px-[22px] py-3.5">{it.date}</td>
                  <td className="px-3 py-3.5 text-ink-muted">{it.kind}</td>
                  <td className="px-3 py-3.5 font-semibold">{it.amount}</td>
                  <td className="px-3 py-3.5">
                    <Badge
                      tone={it.status === "Paid" ? "success" : it.status === "Failed" ? "danger" : "warning"}
                      className="px-2.5 py-0.5 text-[11.5px]"
                    >
                      {it.status === "Paid" ? "Lunas" : it.status === "Failed" ? "Gagal" : "Menunggu"}
                    </Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function ChangePlanModal({
  mode,
  token,
  sub,
  plans,
  onClose,
  onDone,
  onError,
}: {
  mode: "upgrade" | "downgrade";
  token: string;
  sub: MySubscription;
  plans: PlanDto[];
  onClose: () => void;
  onDone: () => void;
  onError: (m: string) => void;
}) {
  const tier = num(sub.tierLevel);
  const targets = plans
    .filter((p) => (mode === "upgrade" ? num(p.tierLevel) > tier : num(p.tierLevel) >= 1 && num(p.tierLevel) < tier))
    .sort((a, b) => num(a.tierLevel) - num(b.tierLevel));

  const [selected, setSelected] = useState<PlanDto | null>(null);
  const [preview, setPreview] = useState<UpgradePreview | null>(null);
  const [busy, setBusy] = useState(false);

  async function pick(plan: PlanDto) {
    setSelected(plan);
    setPreview(null);
    if (mode === "upgrade") {
      setBusy(true);
      try {
        setPreview(await previewUpgrade(token, plan.id));
      } catch (e) {
        onError(e instanceof Error ? e.message : "Gagal menghitung biaya.");
      } finally {
        setBusy(false);
      }
    }
  }

  async function confirm() {
    if (!selected) return;
    setBusy(true);
    try {
      if (mode === "upgrade") {
        const session = await upgrade(token, selected.id);
        window.location.href = session.checkoutUrl; // pay the prorated delta
      } else {
        await downgrade(token, selected.id);
        onDone();
      }
    } catch (e) {
      onError(e instanceof Error ? e.message : "Gagal memproses.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={mode === "upgrade" ? "Tingkatkan paket" : "Turunkan paket"}
      className="max-w-md"
      footer={
        <>
          <Button variant="neutral" fullWidth onClick={onClose}>Batal</Button>
          <Button fullWidth disabled={!selected || busy} loading={busy} onClick={confirm}>
            {mode === "upgrade" ? "Bayar & upgrade" : "Jadwalkan downgrade"}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-2">
        {targets.length === 0 && <p>Tidak ada paket yang tersedia.</p>}
        {targets.map((p) => (
          <button
            key={p.id}
            type="button"
            onClick={() => pick(p)}
            className={
              "flex items-center justify-between rounded-base border px-3.5 py-3 text-left transition-colors " +
              (selected?.id === p.id ? "border-primary bg-primary-soft" : "border-border hover:bg-surface-2")
            }
          >
            <span className="font-bold text-ink">{p.name}</span>
            <span className="text-[13px] text-ink-muted">{formatIdr(p.priceMonthly)} / bulan</span>
          </button>
        ))}

        {mode === "upgrade" && preview && (
          <div className="mt-1 rounded-base bg-surface-2 px-3.5 py-3 text-ink">
            Ditagih prorata sekarang:{" "}
            <strong className="text-ink">{formatIdr(preview.deltaIdr)}</strong>{" "}
            <span className="text-ink-subtle">({num(preview.remainingDays)} hari tersisa)</span>
          </div>
        )}
        {mode === "downgrade" && selected && (
          <p className="mt-1 text-[12.5px] text-ink-muted">
            Berlaku mulai {fmtDate(sub.currentPeriodEnd)}. Akses paket saat ini tetap aktif hingga tanggal tersebut.
          </p>
        )}
      </div>
    </Modal>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-[11.5px] text-ink-subtle">{label}</span>
      <span className="text-sm font-bold text-ink">{value}</span>
    </div>
  );
}
