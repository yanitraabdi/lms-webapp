"use client";

// Bulk import. The screen's whole job is to make a rejection legible: every error names its row
// and column, and nothing is written until the file is clean, so "fix the sheet and upload it
// again" is always the next step.

import { useState } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Spinner } from "@/components/ui";
import {
  downloadImportTemplate, previewQuestionImport, commitQuestionImport, uploadAudioBulk,
  type ImportResult, type BulkAudioResult,
} from "@/lib/sessions";

const card = "rounded-lg border border-border bg-surface p-5 shadow-sm";

export default function QuestionImportPage() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();

  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [busy, setBusy] = useState<"preview" | "commit" | "audio" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [audio, setAudio] = useState<BulkAudioResult | null>(null);

  function pick(next: File | null) {
    setFile(next);
    setResult(null);      // a new file invalidates the previous plan
    setError(null);
  }

  async function run(step: "preview" | "commit") {
    if (!token || !file) return;
    setBusy(step);
    setError(null);
    try {
      const next = step === "preview"
        ? await previewQuestionImport(token, file)
        : await commitQuestionImport(token, file);
      setResult(next);
      if (next.committed) qc.invalidateQueries({ queryKey: ["admin-questions"] });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal memproses berkas.");
    } finally {
      setBusy(null);
    }
  }

  async function sendAudio(files: FileList | null) {
    if (!token || !files?.length) return;
    setBusy("audio");
    setError(null);
    try {
      setAudio(await uploadAudioBulk(token, Array.from(files)));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal mengunggah audio.");
    } finally {
      setBusy(null);
    }
  }

  const errors = result?.errors ?? [];
  const clean = !!result && errors.length === 0 && !result.committed;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-ink-subtle">
          Impor banyak soal sekaligus dari berkas Excel. Isi bank soal — tes akhir tetap disusun di halaman Program.
        </p>
        <Link href="/admin/questions" className="text-[12.5px] font-bold text-primary hover:underline">
          ← Kembali ke bank soal
        </Link>
      </div>

      {error && (
        <div className="rounded-base bg-danger-soft px-4 py-3 text-[13px] font-semibold text-danger">{error}</div>
      )}

      {/* ---- 1. template ---- */}
      <section className={card}>
        <h2 className="text-[15px] font-extrabold text-ink">1. Unduh template</h2>
        <p className="mt-1 text-[13px] text-ink-muted">
          Template berisi contoh soal untuk setiap bagian dan sheet petunjuk pengisian.
        </p>
        <Button
          size="sm"
          variant="neutral"
          className="mt-3"
          onClick={() => token && downloadImportTemplate(token).catch(
            (e) => setError(e instanceof Error ? e.message : "Gagal mengunduh template."))}
        >
          Unduh template (.xlsx)
        </Button>
      </section>

      {/* ---- 2. sheet ---- */}
      <section className={card}>
        <h2 className="text-[15px] font-extrabold text-ink">2. Unggah berkas soal</h2>
        <p className="mt-1 text-[13px] text-ink-muted">
          Berkas diperiksa seluruhnya dulu. Jika ada satu kesalahan, tidak ada soal yang tersimpan.
        </p>

        <div className="mt-3 flex flex-wrap items-center gap-2">
          <input
            type="file"
            accept=".xlsx"
            aria-label="Berkas soal"
            onChange={(e) => pick(e.target.files?.[0] ?? null)}
            className="text-[13px] file:mr-3 file:rounded-sm file:border-0 file:bg-surface-2 file:px-3 file:py-2 file:text-[13px] file:font-bold"
          />
          <Button size="sm" variant="neutral" disabled={!file || busy !== null} onClick={() => run("preview")}>
            {busy === "preview" ? "Memeriksa…" : "Periksa"}
          </Button>
          <Button size="sm" disabled={!clean || busy !== null} onClick={() => run("commit")}>
            {busy === "commit" ? "Mengimpor…" : "Impor sekarang"}
          </Button>
          {busy !== null && <Spinner size={16} />}
        </div>

        {result?.committed && (
          <p className="mt-3 rounded-base bg-success-soft px-3 py-2 text-[13px] font-semibold text-success">
            Berhasil. {result.createCount} soal baru, {result.updateCount} soal diperbarui.
          </p>
        )}

        {errors.length > 0 && (
          <div className="mt-4">
            <p className="text-[13px] font-bold text-danger">
              {errors.length} kesalahan. Perbaiki di Excel lalu unggah ulang berkas yang sama.
            </p>
            <div className="mt-2 max-h-[320px] overflow-auto rounded-sm border border-border">
              <table className="w-full border-collapse text-[13px]">
                <thead className="sticky top-0 bg-surface-2 text-left text-[11.5px] font-bold text-ink-muted">
                  <tr>
                    <th className="px-3 py-2">Baris</th>
                    <th className="px-3 py-2">Kolom</th>
                    <th className="px-3 py-2">Masalah</th>
                  </tr>
                </thead>
                <tbody>
                  {errors.map((e, i) => (
                    <tr key={i} className="border-t border-border">
                      <td className="px-3 py-2 tabular-nums font-semibold">{e.row}</td>
                      <td className="px-3 py-2 font-mono text-[12px]">{e.column}</td>
                      <td className="px-3 py-2">{e.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {result && errors.length === 0 && (
          <div className="mt-4">
            <div className="flex flex-wrap items-center gap-2 text-[13px]">
              <Badge tone="success">{result.createCount} baru</Badge>
              <Badge tone="neutral">{result.updateCount} diperbarui</Badge>
              {Object.entries(result.perSection ?? {}).map(([section, count]) => (
                <Badge key={section} tone="neutral">{section}: {count}</Badge>
              ))}
            </div>
            <div className="mt-2 max-h-[320px] overflow-auto rounded-sm border border-border">
              <table className="w-full border-collapse text-[13px]">
                <thead className="sticky top-0 bg-surface-2 text-left text-[11.5px] font-bold text-ink-muted">
                  <tr>
                    <th className="px-3 py-2">Id</th>
                    <th className="px-3 py-2">Bagian</th>
                    <th className="px-3 py-2">Pertanyaan</th>
                    <th className="px-3 py-2">Aksi</th>
                  </tr>
                </thead>
                <tbody>
                  {(result.items ?? []).map((item) => (
                    <tr key={item.externalId} className="border-t border-border">
                      <td className="px-3 py-2 font-mono text-[12px] font-semibold">{item.externalId}</td>
                      <td className="px-3 py-2">{item.section}</td>
                      <td className="max-w-[420px] truncate px-3 py-2">{item.prompt}</td>
                      <td className="px-3 py-2">
                        {/* An id reused by mistake shows up HERE as "Perbarui" — the one place a
                            human can catch it before an existing question is overwritten. */}
                        <Badge tone={item.isUpdate ? "warning" : "success"}>
                          {item.isUpdate ? "Perbarui" : "Baru"}
                        </Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </section>

      {/* ---- 3. audio ---- */}
      <section className={card}>
        <h2 className="text-[15px] font-extrabold text-ink">3. Unggah audio listening</h2>
        <p className="mt-1 text-[13px] text-ink-muted">
          Nama berkas harus sama dengan kolom <code className="font-mono">audio_file</code> di sheet, misalnya L01.mp3.
          Urutannya bebas — impor dulu atau unggah audio dulu.
        </p>

        <input
          type="file"
          accept="audio/*"
          multiple
          aria-label="Berkas audio"
          disabled={busy !== null}
          onChange={(e) => sendAudio(e.target.files)}
          className="mt-3 text-[13px] file:mr-3 file:rounded-sm file:border-0 file:bg-surface-2 file:px-3 file:py-2 file:text-[13px] file:font-bold"
        />

        {audio && (
          <ul className="mt-3 flex flex-col gap-1 text-[13px]">
            {audio.items.map((item) => (
              <li key={item.filename} className="flex flex-wrap items-center gap-2">
                <span className="font-mono text-[12px]">{item.filename}</span>
                {item.key
                  ? <Badge tone="success">tersimpan sebagai {item.key}</Badge>
                  : <Badge tone="danger">{item.error}</Badge>}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
