"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { AuthCard, AuthError } from "@/components/auth/AuthCard";
import { PasswordStrength } from "@/components/auth/PasswordStrength";
import { Button, CheckCircleIcon, Input } from "@/components/ui";
import { postJson, problemMessage } from "@/lib/auth/client";

export function ResetPasswordClient() {
  const token = useSearchParams().get("token");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (password.length < 8) return setError("Kata sandi minimal 8 karakter.");
    if (password !== confirm) return setError("Konfirmasi sandi tidak cocok.");
    setLoading(true);
    const res = await postJson("/api/auth/reset-password", { token, newPassword: password });
    setLoading(false);
    if (res.ok) setDone(true);
    else setError(problemMessage(res.data, "Gagal menyetel sandi baru."));
  }

  if (!token) {
    return (
      <AuthCard className="items-center text-center">
        <h1 className="text-xl font-extrabold tracking-tight">Tautan tidak valid</h1>
        <p className="text-sm leading-relaxed text-ink-muted">Tautan reset tidak lengkap. Mulai ulang dari halaman lupa sandi.</p>
        <Link href="/forgot-password" className="w-full"><Button variant="secondary" fullWidth>Minta tautan baru</Button></Link>
      </AuthCard>
    );
  }

  if (done) {
    return (
      <AuthCard className="items-center text-center">
        <span className="flex h-[60px] w-[60px] items-center justify-center rounded-full bg-success-soft text-success">
          <CheckCircleIcon size={28} />
        </span>
        <h1 className="text-[22px] font-extrabold tracking-tight">Sandi diperbarui</h1>
        <p className="text-sm leading-relaxed text-ink-muted">Silakan masuk dengan kata sandi baru Anda.</p>
        <Link href="/login" className="w-full"><Button fullWidth>Masuk</Button></Link>
      </AuthCard>
    );
  }

  return (
    <AuthCard>
      <h1 className="text-xl font-extrabold tracking-tight">Buat sandi baru</h1>
      {error && <AuthError message={error} />}
      <form onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="password" className="text-[13px] font-semibold">Sandi baru</label>
          <Input id="password" type="password" autoComplete="new-password" required minLength={8} value={password}
            onChange={(e) => setPassword(e.target.value)} placeholder="Minimal 8 karakter" />
          <PasswordStrength value={password} />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="confirm" className="text-[13px] font-semibold">Ulangi sandi baru</label>
          <Input id="confirm" type="password" autoComplete="new-password" required value={confirm}
            onChange={(e) => setConfirm(e.target.value)} invalid={!!confirm && confirm !== password} />
        </div>
        <Button type="submit" fullWidth loading={loading}>Simpan sandi baru</Button>
      </form>
      <Link href="/login" className="text-center text-[13px] font-semibold text-primary">Kembali ke Masuk</Link>
    </AuthCard>
  );
}
