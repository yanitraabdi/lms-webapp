"use client";

import { useState, type FormEvent } from "react";
import { Button, Input, CheckCircleIcon } from "@/components/ui";
import { submitContact } from "@/lib/content";

export function ContactForm() {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await submitContact({ name, email, message });
      setDone(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Gagal mengirim pesan.");
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
        <h2 className="text-lg font-extrabold">Pesan terkirim</h2>
        <p className="text-sm text-ink-muted">Terima kasih. Tim kami akan menghubungi Anda secepatnya.</p>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-6 shadow-sm">
      {error && <p className="rounded-base border border-danger/30 bg-danger-soft px-4 py-2.5 text-sm text-danger">{error}</p>}
      <div className="flex flex-col gap-1.5">
        <label htmlFor="name" className="text-[13px] font-semibold">Nama</label>
        <Input id="name" required value={name} onChange={(e) => setName(e.target.value)} placeholder="Nama Anda" />
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="email" className="text-[13px] font-semibold">Email</label>
        <Input id="email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="nama@perusahaan.com" />
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="message" className="text-[13px] font-semibold">Pesan</label>
        <textarea
          id="message"
          required
          rows={5}
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          placeholder="Apa yang bisa kami bantu?"
          className="rounded-sm border border-border bg-surface px-3 py-2.5 text-sm outline-none focus:border-primary"
        />
      </div>
      <Button type="submit" fullWidth loading={loading}>Kirim pesan</Button>
    </form>
  );
}
