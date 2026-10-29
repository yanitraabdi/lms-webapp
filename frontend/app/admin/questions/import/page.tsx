"use client";

// Bulk import. The screen's whole job is to make a rejection legible: every error names its row
// and column, and nothing is written until the file is clean, so "fix the sheet and upload it
// again" is always the next step.

import { useState } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Spinner } from "@/components/ui";
import { Chapter, PageTitle } from "@/components/ui/Page";
import {
  downloadImportTemplate, previewQuestionImport, commitQuestionImport, uploadAudioBulk,
  QUESTION_BANKS, BANK_LABEL, type ImportResult, type BulkAudioResult, type QuestionBankName,
} from "@/lib/sessions";

export default function QuestionImportPage() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();

  const [bank, setBank] = useState<QuestionBankName | null>(null);
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

  function chooseBank(next: QuestionBankName) {
    setBank(next);
    setResult(null);      // a preview must never be committed into a different bank
    setError(null);
  }

  async function run(step: "preview" | "commit") {
    if (!token || !file || !bank) return;
    setBusy(step);
    setError(null);
    try {
      const next = step === "preview"
        ? await previewQuestionImport(token, file, bank)
        : await commitQuestionImport(token, file, bank);
      setResult(next);
      if (next.committed) {
        qc.invalidateQueries({ queryKey: ["admin-questions"] });
        qc.invalidateQueries({ queryKey: ["question-bank-counts"] });
        qc.invalidateQueries({ queryKey: ["picker-questions"] });
      }
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
      <div>
        <Link href="/admin/questions" className="text-[12.5px] font-bold text-primary hover:underline">
          Kembali ke bank soal
        </Link>
        <PageTitle size="compact">Impor soal</PageTitle>
        <p className="mt-2 text-sm text-ink-muted">
          Impor banyak soal sekaligus dari berkas Excel. Isi bank soal, tes akhir tetap disusun di halaman Program.
        </p>
      </div>

      {error && (
        <div className="rounded-base bg-danger-soft px-4 py-3 text-[13px] font-semibold text-danger">{error}</div>
      )}

      {/* ---- 1. template ---- */}
      <Chapter title="1. Unduh template">
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
      </Chapter>

      {/* ---- 2. sheet ---- */}
      <Chapter title="2. Unggah berkas soal">
        <p className="mt-1 text-[13px] text-ink-muted">
          Berkas diperiksa seluruhnya dulu. Jika ada satu kesalahan, tidak ada soal yang tersimpan.
        </p>

        <fieldset className="mt-3">
          <legend className="text-[13px] font-bold text-ink">
            Impor ke bank: <span className="text-danger" aria-hidden="true">*</span>
          </legend>
          <div className="mt-1.5 flex flex-wrap gap-4">
            {QUESTION_BANKS.map((b) => (
              <label key={b} className="flex items-center gap-2 text-[13px]">
                <input
                  type="radio"
                  name="bank"
                  value={b}
                  required
                  checked={bank === b}
                  onChange={() => chooseBank(b)}
                  disabled={busy !== null}
                  className="accent-primary"
                />
                {BANK_LABEL[b]}
              </label>
            ))}
          </div>
          {!bank && <p className="mt-1.5 text-[12.5px] text-ink-muted">Pilih bank tujuan terlebih dahulu.</p>}
        </fieldset>

        <div className="mt-3 flex flex-wrap items-center gap-2">
          <input
            type="file"
            accept=".xlsx"
            aria-label="Berkas soal"
            disabled={!bank}
            onChange={(e) => pick(e.target.files?.[0] ?? null)}
            className="text-[13px] file:mr-3 file:rounded-sm file:border-0 file:bg-surface-2 file:px-3 file:py-2 file:text-[13px] file:font-bold"
          />
          <Button size="sm" variant="neutral" disabled={!bank || !file || busy !== null} onClick={() => run("preview")}>
            {busy === "preview" ? "Memeriksa…" : "Periksa"}
          </Button>
          <Button size="sm" disabled={!bank || !clean || busy !== null} onClick={() => run("commit")}>
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
              <table className="data-table">
                <thead className="sticky top-0 bg-surface">
                  <tr>
                    <th>Baris</th>
                    <th>Kolom</th>
                    <th>Masalah</th>
                  </tr>
                </thead>
                <tbody>
                  {errors.map((e, i) => (
                    <tr key={i}>
                      <td className="tabular-nums font-semibold">{e.row}</td>
                      <td className="font-mono text-[12px]">{e.column}</td>
                      <td>{e.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {result && bank && (
          <p className="mt-4 text-[13px] font-bold text-ink">Bank tujuan: {BANK_LABEL[bank]}</p>
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
              <table className="data-table">
                <thead className="sticky top-0 bg-surface">
                  <tr>
                    <th>Id</th>
                    <th>Bagian</th>
                    <th>Pertanyaan</th>
                    <th>Aksi</th>
                  </tr>
                </thead>
                <tbody>
                  {(result.items ?? []).map((item) => (
                    <tr key={item.externalId}>
                      <td className="font-mono text-[12px] font-semibold">{item.externalId}</td>
                      <td>{item.section}</td>
                      <td className="max-w-[420px] truncate">{item.prompt}</td>
                      <td>
                        {/* An id reused by mistake shows up HERE as "Perbarui", the one place a
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
      </Chapter>

      {/* ---- 3. audio ---- */}
      <Chapter title="3. Unggah audio listening">
        <p className="mt-1 text-[13px] text-ink-muted">
          Nama berkas harus sama dengan kolom <code className="font-mono">audio_file</code> di sheet, misalnya L01.mp3.
          Urutannya bebas, impor dulu atau unggah audio dulu.
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
      </Chapter>
    </div>
  );
}
