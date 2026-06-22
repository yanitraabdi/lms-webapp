"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { AuthCard } from "@/components/auth/AuthCard";
import { AlertCircleIcon, Button, CheckCircleIcon, Spinner } from "@/components/ui";
import { postJson, problemMessage } from "@/lib/auth/client";

type State = "checking" | "verified" | "invalid" | "prompt";

export function VerifyEmailClient() {
  const params = useSearchParams();
  const token = params.get("token");
  const email = params.get("email") ?? "";

  const [state, setState] = useState<State>(token ? "checking" : "prompt");
  const [cooldown, setCooldown] = useState(0);
  const [resendMsg, setResendMsg] = useState<string | null>(null);
  const ran = useRef(false);

  useEffect(() => {
    if (!token || ran.current) return;
    ran.current = true;
    void (async () => {
      const res = await postJson("/api/auth/verify-email", { token });
      setState(res.ok ? "verified" : "invalid");
    })();
  }, [token]);

  useEffect(() => {
    if (cooldown <= 0) return;
    const id = setInterval(() => setCooldown((c) => c - 1), 1000);
    return () => clearInterval(id);
  }, [cooldown]);

  async function resend() {
    setResendMsg(null);
    const res = await postJson("/api/auth/resend-verification", { email });
    if (res.ok) {
      setCooldown(45);
      setResendMsg("Email verifikasi telah dikirim ulang.");
    } else {
      setResendMsg(problemMessage(res.data, "Gagal mengirim ulang."));
    }
  }

  if (state === "checking") {
    return (
      <AuthCard className="items-center text-center">
        <Spinner size={28} className="text-primary" />
        <p className="text-sm text-ink-muted">Memverifikasi email Anda…</p>
      </AuthCard>
    );
  }

  if (state === "verified") {
    return (
      <AuthCard className="items-center text-center">
        <span className="flex h-[60px] w-[60px] items-center justify-center rounded-full bg-success-soft text-success">
          <CheckCircleIcon size={28} />
        </span>
        <h1 className="text-[22px] font-extrabold tracking-tight">Email terverifikasi</h1>
        <p className="text-sm leading-relaxed text-ink-muted">
          Akun Anda sudah aktif. Anda kini dapat berlangganan dan mulai belajar.
        </p>
        <Link href="/app/dashboard" className="w-full">
          <Button fullWidth>Ke Dasbor</Button>
        </Link>
      </AuthCard>
    );
  }

  if (state === "invalid") {
    return (
      <AuthCard className="items-center text-center">
        <span className="flex h-[60px] w-[60px] items-center justify-center rounded-full bg-danger-soft text-danger">
          <AlertCircleIcon size={28} />
        </span>
        <h1 className="text-[22px] font-extrabold tracking-tight">Tautan tidak valid</h1>
        <p className="text-sm leading-relaxed text-ink-muted">
          Tautan verifikasi tidak valid atau sudah kedaluwarsa. Masuk untuk meminta tautan baru.
        </p>
        <Link href="/login" className="w-full">
          <Button variant="secondary" fullWidth>Kembali ke Masuk</Button>
        </Link>
      </AuthCard>
    );
  }

  // prompt (post-register)
  return (
    <AuthCard className="items-center text-center">
      <span className="flex h-[60px] w-[60px] items-center justify-center rounded-full bg-primary-soft text-primary">
        <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8}>
          <rect x="3" y="5" width="18" height="14" rx="2" />
          <path d="m3 7 9 6 9-6" />
        </svg>
      </span>
      <h1 className="text-[22px] font-extrabold tracking-tight">Cek email Anda</h1>
      <p className="text-sm leading-relaxed text-ink-muted">
        Kami mengirim tautan verifikasi{email ? " ke " : "."}
        {email && <strong className="text-ink">{email}</strong>}. Klik tautan tersebut untuk mengaktifkan akun.
      </p>
      <div className="flex w-full items-start gap-2.5 rounded-sm border border-[#F4DDB0] bg-accent-soft px-3 py-2.5 text-left">
        <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.2} className="mt-0.5 shrink-0 text-accent">
          <circle cx="12" cy="12" r="9" />
          <path d="M12 8v5M12 16.5v.5" />
        </svg>
        <span className="text-[12.5px] leading-snug text-[#8A5208]">
          Verifikasi email diperlukan sebelum Anda dapat berlangganan paket berbayar.
        </span>
      </div>
      <Button variant="secondary" fullWidth disabled={cooldown > 0 || !email} onClick={resend}>
        {cooldown > 0 ? `Kirim ulang dalam ${cooldown}s` : "Kirim ulang email"}
      </Button>
      {resendMsg && <p className="text-[12.5px] text-ink-muted">{resendMsg}</p>}
      <Link href="/app/dashboard" className="text-[13px] font-semibold text-primary">Lewati ke Dasbor</Link>
    </AuthCard>
  );
}
