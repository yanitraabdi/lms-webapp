"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { AuthCard, AuthDivider, AuthError, LogoMark } from "@/components/auth/AuthCard";
import { GoogleButton } from "@/components/auth/GoogleButton";
import { PasswordStrength } from "@/components/auth/PasswordStrength";
import { Button, Input } from "@/components/ui";

export default function RegisterPage() {
  const router = useRouter();
  const { register } = useAuth();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await register(name, email, password);
      router.push(`/verify-email?email=${encodeURIComponent(email)}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Gagal membuat akun.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthCard>
      <div className="flex flex-col items-center gap-2 text-center">
        <LogoMark />
        <h1 className="text-[22px] font-extrabold tracking-tight">Buat akun gratis</h1>
        <p className="text-[13.5px] text-ink-muted">Mulai belajar AI dalam hitungan menit.</p>
      </div>

      <GoogleButton label="Lanjutkan dengan Google" />
      <AuthDivider />

      {error && <AuthError message={error} />}

      <form onSubmit={onSubmit} className="flex flex-col gap-[18px]">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="name" className="text-[13px] font-semibold">Nama lengkap</label>
          <Input id="name" autoComplete="name" required value={name}
            onChange={(e) => setName(e.target.value)} placeholder="Budi Santoso" />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="email" className="text-[13px] font-semibold">Email kerja</label>
          <Input id="email" type="email" autoComplete="email" required value={email}
            onChange={(e) => setEmail(e.target.value)} placeholder="nama@perusahaan.com" />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="password" className="text-[13px] font-semibold">Kata sandi</label>
          <Input id="password" type="password" autoComplete="new-password" required minLength={8} value={password}
            onChange={(e) => setPassword(e.target.value)} placeholder="Minimal 8 karakter" />
          <PasswordStrength value={password} />
        </div>
        <Button type="submit" fullWidth loading={loading}>Buat akun</Button>
      </form>

      <p className="text-center text-[11.5px] leading-relaxed text-ink-subtle">
        Dengan mendaftar, Anda menyetujui{" "}
        <Link href="/legal/terms" className="text-primary">Ketentuan</Link> &amp;{" "}
        <Link href="/legal/privacy" className="text-primary">Kebijakan Privasi</Link>.
      </p>
      <p className="text-center text-[13px] text-ink-muted">
        Sudah punya akun? <Link href="/login" className="font-bold text-primary">Masuk</Link>
      </p>
    </AuthCard>
  );
}
