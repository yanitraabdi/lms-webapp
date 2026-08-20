"use client";

import { useState } from "react";
import { Button, Modal } from "@/components/ui";
import { createProgram, updateProgram, num, type AdminProgram } from "@/lib/programs";

export function ProgramForm({ token, program, onClose }: { token: string; program: AdminProgram | null; onClose: () => void }) {
  const [name, setName] = useState(program?.name ?? "");
  const [slug, setSlug] = useState(program?.slug ?? "");
  const [summary, setSummary] = useState(program?.summary ?? "");
  const [description, setDescription] = useState(program?.description ?? "");
  const [price, setPrice] = useState(String(num(program?.priceIdr ?? 0)));
  const [published, setPublished] = useState(program?.status === "Published");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setBusy(true); setError(null);
    try {
      const body = {
        name: name.trim(),
        slug: slug.trim() || null,
        description: description.trim(),
        summary: summary.trim() || null,
        priceIdr: Number(price) || 0,
        published,
      };
      if (program) await updateProgram(token, program.id, body);
      else await createProgram(token, body);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={program ? `Edit — ${program.name}` : "Program baru"}
      className="max-w-xl"
      footer={
        <div className="flex w-full justify-end gap-2">
          <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
          <Button size="sm" onClick={save} loading={busy} disabled={!name.trim() || !description.trim()}>Simpan</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}
        <Field label="Nama program"><input value={name} onChange={(e) => setName(e.target.value)} className={inputCls} /></Field>
        <Field label="Slug (opsional)"><input value={slug} onChange={(e) => setSlug(e.target.value)} placeholder="otomatis dari nama" className={inputCls} /></Field>
        <Field label="Ringkasan"><input value={summary} onChange={(e) => setSummary(e.target.value)} className={inputCls} /></Field>
        <Field label="Deskripsi">
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={4} className={inputCls} />
        </Field>
        <Field label="Harga (IDR)">
          <input type="number" min={0} value={price} onChange={(e) => setPrice(e.target.value)} className={inputCls} />
        </Field>
        <label className="flex items-center gap-2 text-[13px] font-semibold">
          <input type="checkbox" checked={published} onChange={(e) => setPublished(e.target.checked)} className="accent-primary" />
          Terbitkan (tampil di halaman publik)
        </label>
      </div>
    </Modal>
  );
}

const inputCls =
  "w-full rounded-sm border border-border bg-surface px-3 py-2 text-[13.5px] outline-none focus:border-primary";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-[12px] font-bold text-ink-muted">{label}</span>
      {children}
    </label>
  );
}
