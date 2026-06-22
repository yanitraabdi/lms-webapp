"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { AuthCard, AuthDivider, AuthError, LogoMark } from "@/components/auth/AuthCard";
import { GoogleButton } from "@/components/auth/GoogleButton";
import { Button, Input } from "@/components/ui";

export default function LoginPage() {
  const router = useRouter();
  const { login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login(email, password);
      router.push("/app/dashboard");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Gagal masuk.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthCard>
      <div className="flex flex-col items-center gap-2 text-center">
        <LogoMark />
        <h1 className="text-[22px] font-extrabold tracking-tight">Masuk ke akun Anda</h1>
        <p className="text-[13.5px] text-ink-muted">Lanjutkan perjalanan belajar Anda.</p>
      </div>

      <GoogleButton label="Lanjutkan dengan Google" />
      <AuthDivider />

      {error && <AuthError message={error} />}

      <form onSubmit={onSubmit} className="flex flex-col gap-[18px]">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="email" className="text-[13px] font-semibold">Email</label>
          <Input id="email" type="email" autoComplete="email" required value={email}
            onChange={(e) => setEmail(e.target.value)} invalid={!!error} placeholder="nama@perusahaan.com" />
        </div>
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center justify-between">
            <label htmlFor="password" className="text-[13px] font-semibold">Kata sandi</label>
            <Link href="/forgot-password" className="text-[12.5px] font-semibold text-primary">Lupa sandi?</Link>
          </div>
          <Input id="password" type="password" autoComplete="current-password" required value={password}
            onChange={(e) => setPassword(e.target.value)} invalid={!!error} />
        </div>
        <Button type="submit" fullWidth loading={loading}>Masuk</Button>
      </form>

      <p className="text-center text-[13px] text-ink-muted">
        Belum punya akun? <Link href="/register" className="font-bold text-primary">Daftar gratis</Link>
      </p>
    </AuthCard>
  );
}
