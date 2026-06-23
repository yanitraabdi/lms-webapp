"use client";

import { useState, type FormEvent } from "react";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, CheckCircleIcon } from "@/components/ui";
import { cn } from "@/lib/cn";
import { submitFeedback } from "@/lib/content";

const TYPES = ["Saran", "Masalah", "Pujian"];

export function FeedbackForm() {
  const { accessToken } = useAuth();
  const [type, setType] = useState("Saran");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await submitFeedback(accessToken ?? null, { message, context: type });
      setDone(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Gagal mengirim masukan.");
    } finally {
      setLoading(false);
    }
  }

  if (done) {
    return (
      <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface p-8 text-center shadow-sm">
        <span className="flex h-14 w-14 items-center justify-center rounded-full bg-success-soft text-success">
          <CheckCircleIcon size={30} strokeWidth={2.2} />
        </span>
        <h2 className="text-lg font-extrabold">Terima kasih atas masukannya!</h2>
        <p className="text-sm text-ink-muted">Masukan Anda membantu kami menjadi lebih baik.</p>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-6 shadow-sm">
      {error && <p className="rounded-base border border-danger/30 bg-danger-soft px-4 py-2.5 text-sm text-danger">{error}</p>}
      <div className="flex flex-col gap-1.5">
        <span className="text-[13px] font-semibold">Jenis masukan</span>
        <div className="flex gap-2">
          {TYPES.map((t) => (
            <button
              key={t}
              type="button"
              onClick={() => setType(t)}
              className={cn(
                "rounded-full px-4 py-1.5 text-[13px] font-bold transition-colors",
                type === t ? "bg-primary text-primary-ink" : "bg-surface-2 text-ink-muted hover:bg-border"
              )}
            >
              {t}
            </button>
          ))}
        </div>
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="message" className="text-[13px] font-semibold">Pesan</label>
        <textarea
          id="message"
          required
          rows={5}
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          placeholder="Ceritakan ide, kendala, atau apa yang Anda sukai…"
          className="rounded-sm border border-border bg-surface px-3 py-2.5 text-sm outline-none focus:border-primary"
        />
      </div>
      <Button type="submit" fullWidth loading={loading}>Kirim masukan</Button>
    </form>
  );
}
