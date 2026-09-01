"use client";

// INVERTA account page. Replaces the archived AI-Academy subscription screen (tiers, upgrade,
// downgrade), which described a product that is no longer sold — INVERTA is a one-time purchase.
// Profile and purchase live together because there is exactly one purchase per learner.

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { Badge, Button, Spinner, ErrorState } from "@/components/ui";
import { listMyEnrollments, updateProfile, changePassword, num, type Enrollment } from "@/lib/programs";

const inputCls =
  "w-full rounded-base border border-border bg-surface px-3 py-2 text-[13.5px] outline-none focus:border-primary";

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-[12.5px] font-bold text-ink">{label}</span>
      {children}
      {hint && <span className="mt-1 block text-[12px] text-ink-muted">{hint}</span>}
    </label>
  );
}

function money(v: number | string | null | undefined) {
  return `Rp ${num(v ?? 0).toLocaleString("id-ID")}`;
}

function date(iso: string | null | undefined) {
  if (!iso) return "—";
  return new Date(iso).toLocaleDateString("id-ID", { day: "numeric", month: "long", year: "numeric" });
}

export default function AccountPage() {
  const { status, accessToken, user, refresh } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login?next=/app/account");
  }, [status, router]);

  const enrollments = useQuery({
    queryKey: ["my-enrollments"],
    queryFn: () => listMyEnrollments(accessToken!),
    enabled: status === "authenticated" && !!accessToken,
  });

  const [name, setName] = useState("");
  const [savingName, setSavingName] = useState(false);
  const [nameMsg, setNameMsg] = useState<string | null>(null);
  const [nameErr, setNameErr] = useState<string | null>(null);

  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [savingPw, setSavingPw] = useState(false);
  const [pwMsg, setPwMsg] = useState<string | null>(null);
  const [pwErr, setPwErr] = useState<string | null>(null);

  useEffect(() => { if (user?.name) setName(user.name); }, [user?.name]);

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }

  async function saveName() {
    setSavingName(true); setNameMsg(null); setNameErr(null);
    try {
      await updateProfile(accessToken!, name.trim());
      // Re-read the session so the header initials and greeting update immediately.
      await refresh();
      setNameMsg("Nama tersimpan.");
    } catch (e) {
      setNameErr(e instanceof Error ? e.message : "Gagal menyimpan nama.");
    } finally { setSavingName(false); }
  }

  async function savePassword() {
    setSavingPw(true); setPwMsg(null); setPwErr(null);
    try {
      await changePassword(accessToken!, current, next);
      // The server rotates refresh tokens on a password change; re-read so this tab stays signed in.
      await refresh();
      setCurrent(""); setNext(""); setConfirm("");
      setPwMsg("Kata sandi diperbarui. Perangkat lain akan diminta masuk kembali.");
    } catch (e) {
      setPwErr(e instanceof Error ? e.message : "Gagal mengubah kata sandi.");
    } finally { setSavingPw(false); }
  }

  const nameValid = name.trim().length >= 2 && name.trim() !== user?.name;
  const pwValid = current.length > 0 && next.length >= 8 && next === confirm;

  return (
    <div className="min-h-screen bg-bg">
      <AppHeader />
      <main className="mx-auto max-w-3xl px-6 py-8">
        <h1 className="text-xl font-extrabold text-ink">Profil & tagihan</h1>
        <p className="mt-1 text-[13.5px] text-ink-muted">Kelola data akun dan lihat pembelian Anda.</p>

        {/* ---- profile ---- */}
        <section className="mt-6 rounded-base border border-border bg-surface p-5">
          <h2 className="text-[15px] font-extrabold text-ink">Data akun</h2>

          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <Field label="Nama lengkap">
              <input className={inputCls} value={name} onChange={(e) => setName(e.target.value)} maxLength={120} />
            </Field>
            <Field label="Email" hint="Email tidak dapat diubah karena dipakai untuk masuk.">
              <input className={inputCls + " bg-surface-2 text-ink-muted"} value={user?.email ?? ""} disabled />
            </Field>
          </div>

          <div className="mt-3 flex items-center gap-3">
            <Button size="sm" disabled={!nameValid || savingName} onClick={saveName}>
              {savingName ? "Menyimpan…" : "Simpan nama"}
            </Button>
            {nameMsg && <span className="text-[12.5px] font-semibold text-success">{nameMsg}</span>}
            {nameErr && <span className="text-[12.5px] font-semibold text-danger">{nameErr}</span>}
          </div>

          <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-border pt-4 text-[12.5px] text-ink-muted">
            <span>Status email:</span>
            {user?.emailVerified
              ? <Badge tone="success">Terverifikasi</Badge>
              : <Badge tone="warning">Belum terverifikasi</Badge>}
          </div>
        </section>

        {/* ---- password ---- */}
        <section className="mt-5 rounded-base border border-border bg-surface p-5">
          <h2 className="text-[15px] font-extrabold text-ink">Ubah kata sandi</h2>

          <div className="mt-4 grid gap-4 sm:grid-cols-3">
            <Field label="Kata sandi saat ini">
              <input type="password" className={inputCls} value={current} onChange={(e) => setCurrent(e.target.value)} />
            </Field>
            <Field label="Kata sandi baru" hint="Minimal 8 karakter.">
              <input type="password" className={inputCls} value={next} onChange={(e) => setNext(e.target.value)} />
            </Field>
            <Field label="Ulangi kata sandi baru">
              <input type="password" className={inputCls} value={confirm} onChange={(e) => setConfirm(e.target.value)} />
            </Field>
          </div>

          {next.length > 0 && confirm.length > 0 && next !== confirm && (
            <p className="mt-2 text-[12.5px] font-semibold text-danger">Kedua kata sandi baru belum sama.</p>
          )}

          <div className="mt-3 flex items-center gap-3">
            <Button size="sm" disabled={!pwValid || savingPw} onClick={savePassword}>
              {savingPw ? "Menyimpan…" : "Ubah kata sandi"}
            </Button>
            {pwMsg && <span className="text-[12.5px] font-semibold text-success">{pwMsg}</span>}
            {pwErr && <span className="text-[12.5px] font-semibold text-danger">{pwErr}</span>}
          </div>
        </section>

        {/* ---- purchase ---- */}
        <section className="mt-5 rounded-base border border-border bg-surface p-5">
          <h2 className="text-[15px] font-extrabold text-ink">Pembelian</h2>
          <p className="mt-1 text-[12.5px] text-ink-muted">
            INVERTA dibayar sekali untuk seluruh program — bukan langganan bulanan.
          </p>

          {enrollments.isPending ? (
            <div className="flex min-h-[110px] items-center justify-center"><Spinner size={20} /></div>
          ) : enrollments.isError ? (
            <ErrorState
              title="Gagal memuat data pembelian."
              action={<Button size="sm" variant="secondary" onClick={() => void enrollments.refetch()}>Muat ulang</Button>}
            />
          ) : (enrollments.data ?? []).length === 0 ? (
            <p className="mt-4 text-[13.5px] text-ink-muted">Belum ada pembelian.</p>
          ) : (
            <div className="mt-4 overflow-x-auto">
              <table className="w-full text-left text-[13px]">
                <thead className="text-[12px] uppercase tracking-wide text-ink-subtle">
                  <tr>
                    <th className="pb-2 pr-3 font-bold">Program</th>
                    <th className="pb-2 pr-3 font-bold">Status</th>
                    <th className="pb-2 pr-3 font-bold">Jumlah</th>
                    <th className="pb-2 font-bold">Tanggal</th>
                  </tr>
                </thead>
                <tbody>
                  {(enrollments.data ?? []).map((e: Enrollment) => (
                    <tr key={e.id} className="border-t border-border">
                      <td className="py-2.5 pr-3 font-semibold text-ink">{e.programName}</td>
                      <td className="py-2.5 pr-3">
                        <Badge tone={e.status === "Active" || e.status === "Completed" ? "success" : "neutral"}>
                          {e.status}
                        </Badge>
                      </td>
                      <td className="py-2.5 pr-3 tabular-nums">{money(e.amountPaidIdr)}</td>
                      <td className="py-2.5 text-ink-muted">{date(e.enrolledAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
