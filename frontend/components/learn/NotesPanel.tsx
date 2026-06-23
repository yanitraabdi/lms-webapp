"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Spinner, XIcon } from "@/components/ui";
import { listNotes, createNote, deleteNote, clockLabel, num } from "@/lib/engagement";

/** Notes & bookmarks for the current module. `getPosition` returns the live playback second. */
export function NotesPanel({ token, moduleId, getPosition }: { token: string; moduleId: string; getPosition: () => number }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["notes", moduleId], queryFn: () => listNotes(token, moduleId) });
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);

  async function add(type: "Note" | "Bookmark") {
    setBusy(true);
    try {
      await createNote(token, moduleId, {
        timestampSeconds: Math.round(getPosition()),
        type,
        text: type === "Note" ? text.trim() || null : null,
      });
      if (type === "Note") setText("");
      qc.invalidateQueries({ queryKey: ["notes", moduleId] });
    } finally {
      setBusy(false);
    }
  }

  async function remove(id: string) {
    await deleteNote(token, id).catch(() => {});
    qc.invalidateQueries({ queryKey: ["notes", moduleId] });
  }

  const notes = q.data ?? [];

  return (
    <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
      <div className="border-b border-border px-[18px] py-4">
        <span className="text-sm font-extrabold">Catatan & penanda</span>
      </div>
      <div className="p-[18px]">
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          maxLength={1000}
          rows={2}
          placeholder="Tulis catatan untuk posisi saat ini…"
          className="w-full resize-none rounded-base border border-border bg-surface px-3 py-2 text-[13px] outline-none focus:border-primary"
        />
        <div className="mt-2 flex flex-wrap gap-2">
          <Button size="sm" onClick={() => add("Note")} loading={busy} disabled={!text.trim()}>Simpan catatan</Button>
          <Button size="sm" variant="neutral" onClick={() => add("Bookmark")} loading={busy}>★ Tandai posisi</Button>
        </div>

        {q.isPending ? (
          <div className="flex min-h-[60px] items-center justify-center"><Spinner size={18} /></div>
        ) : notes.length === 0 ? (
          <p className="mt-4 text-[12.5px] text-ink-subtle">Belum ada catatan untuk modul ini.</p>
        ) : (
          <ul className="mt-4 flex flex-col gap-2">
            {notes.map((n) => (
              <li key={n.id} className="group flex items-start gap-2 rounded-base border border-border px-3 py-2">
                <span className="mt-0.5 shrink-0 rounded bg-primary-soft px-1.5 py-0.5 text-[11px] font-bold text-primary">
                  {clockLabel(num(n.timestampSeconds))}
                </span>
                <span className="min-w-0 flex-1 text-[12.5px] leading-snug text-ink">
                  {n.type === "Bookmark" ? <em className="text-ink-muted">Penanda</em> : n.text}
                </span>
                <button
                  type="button"
                  onClick={() => remove(n.id)}
                  aria-label="Hapus catatan"
                  className="shrink-0 text-ink-subtle transition-opacity hover:text-danger md:opacity-0 md:group-hover:opacity-100"
                >
                  <XIcon size={14} />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
