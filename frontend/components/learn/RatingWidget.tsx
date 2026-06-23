"use client";

import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, StarIcon } from "@/components/ui";
import { getFeedback, rateModule, num } from "@/lib/engagement";

export function RatingWidget({ token, moduleId }: { token: string; moduleId: string }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["feedback", moduleId], queryFn: () => getFeedback(token, moduleId) });
  const [hover, setHover] = useState(0);
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (q.data?.myComment != null) setComment(q.data.myComment);
  }, [q.data?.myComment]);

  const my = q.data?.myRating ? num(q.data.myRating) : 0;
  const avg = q.data ? num(q.data.averageRating) : 0;
  const count = q.data ? num(q.data.count) : 0;

  async function save(rating: number, withComment: boolean) {
    setBusy(true);
    setSaved(false);
    try {
      await rateModule(token, moduleId, rating, withComment ? comment.trim() || null : comment.trim() || null);
      qc.invalidateQueries({ queryKey: ["feedback", moduleId] });
      if (withComment) setSaved(true);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-lg border border-border bg-surface p-4 shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <span className="text-sm font-extrabold">Nilai modul ini</span>
        {count > 0 && <span className="text-[12px] text-ink-subtle">{avg.toFixed(1)} ★ · {count} penilaian</span>}
      </div>

      <div className="mt-2 flex items-center gap-1" onMouseLeave={() => setHover(0)}>
        {[1, 2, 3, 4, 5].map((r) => (
          <button
            key={r}
            type="button"
            disabled={busy}
            aria-label={`Beri ${r} bintang`}
            onMouseEnter={() => setHover(r)}
            onClick={() => save(r, false)}
            className="p-0.5"
          >
            <StarIcon size={26} className={(hover ? r <= hover : r <= my) ? "text-warning" : "text-border"} />
          </button>
        ))}
        {my > 0 && <span className="ml-2 text-[12.5px] text-ink-muted">Penilaian Anda: {my}/5</span>}
      </div>

      {my > 0 && (
        <div className="mt-3">
          <textarea
            value={comment}
            onChange={(e) => { setComment(e.target.value); setSaved(false); }}
            maxLength={1000}
            rows={2}
            placeholder="Bagikan masukan (opsional)…"
            className="w-full resize-none rounded-base border border-border bg-surface px-3 py-2 text-[13px] outline-none focus:border-primary"
          />
          <div className="mt-2 flex items-center justify-end gap-3">
            {saved && <span className="text-[12.5px] font-bold text-success">Tersimpan ✓</span>}
            <Button size="sm" variant="neutral" onClick={() => save(my, true)} loading={busy}>Simpan masukan</Button>
          </div>
        </div>
      )}
    </div>
  );
}
