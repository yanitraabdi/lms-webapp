"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { AuthCard, AuthError } from "@/components/auth/AuthCard";
import { Button, CheckCircleIcon, Input } from "@/components/ui";
import { postJson, problemMessage } from "@/lib/auth/client";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    const res = await postJson("/api/auth/forgot-password", { email });
    setLoading(false);
    if (res.ok) setSent(true);
    else setError(problemMessage(res.data, "Gagal mengirim tautan."));
  }

  if (sent) {
    return (
      <AuthCard className="flex-row items-start gap-3 border-l-[3px] border-l-success">
        <CheckCircleIcon size={22} strokeWidth={2.2} className="shrink-0 text-success" />
        <div>
          <div className="mb-1 text-sm font-bold">Tautan terkirim</div>
          <p className="text-[13px] leading-relaxed text-ink-muted">
            Jika email terdaftar, Anda akan menerima tautan reset dalam beberapa menit. Cek juga folder spam.
          </p>
          <Link href="/login" className="mt-3 inline-block text-[13px] font-semibold text-primary">Kembali ke Masuk</Link>
        </div>
      </AuthCard>
    );
  }

  return (
    <AuthCard>
      <h1 className="text-xl font-extrabold tracking-tight">Reset kata sandi</h1>
      <p className="text-[13.5px] leading-relaxed text-ink-muted">
        Masukkan email Anda. Kami akan mengirim tautan untuk membuat sandi baru.
      </p>
      {error && <AuthError message={error} />}
      <form onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="email" className="text-[13px] font-semibold">Email</label>
          <Input id="email" type="email" autoComplete="email" required value={email}
            onChange={(e) => setEmail(e.target.value)} placeholder="nama@perusahaan.com" />
        </div>
        <Button type="submit" fullWidth loading={loading}>Kirim tautan reset</Button>
      </form>
      <Link href="/login" className="text-center text-[13px] font-semibold text-primary">Kembali ke Masuk</Link>
    </AuthCard>
  );
}
