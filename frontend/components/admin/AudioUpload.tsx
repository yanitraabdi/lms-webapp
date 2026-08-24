"use client";

import { useRef, useState } from "react";
import { Button } from "@/components/ui";
import { uploadAudio } from "@/lib/sessions";

/** Uploads one audio file and reports its storage key. Uploading replaces whatever the
 *  owning question or section currently points at. */
export function AudioUpload({
  token, value, onChange,
}: { token: string; value: string | null; onChange: (key: string | null) => void }) {
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  async function pick(file: File) {
    setBusy(true);
    setErr(null);
    try {
      const { key } = await uploadAudio(token, file);
      onChange(key);
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Unggah audio gagal.");
    } finally {
      setBusy(false);
      if (input.current) input.current.value = "";
    }
  }

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center gap-2">
        <input
          ref={input}
          type="file"
          accept="audio/mpeg,audio/mp4,audio/x-m4a,audio/wav,audio/ogg"
          className="hidden"
          onChange={(e) => { const f = e.target.files?.[0]; if (f) void pick(f); }}
        />
        <Button size="sm" variant="secondary" disabled={busy} onClick={() => input.current?.click()}>
          {busy ? "Mengunggah…" : value ? "Ganti audio" : "Unggah audio"}
        </Button>
        {value && (
          <button
            type="button"
            onClick={() => onChange(null)}
            className="text-[12.5px] font-bold text-danger hover:underline"
          >
            Hapus
          </button>
        )}
      </div>
      {value && <p className="truncate text-[12px] text-ink-muted">{value}</p>}
      {!value && <p className="text-[12px] text-ink-subtle">Belum ada audio. MP3, M4A, WAV, atau OGG.</p>}
      {err && <p className="text-[12px] text-danger">{err}</p>}
    </div>
  );
}
