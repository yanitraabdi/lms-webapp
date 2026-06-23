"use client";

import { useMemo, useState } from "react";
import { SearchIcon } from "@/components/ui";
import type { FaqItem } from "@/lib/content";

export function FaqAccordion({ items }: { items: FaqItem[] }) {
  const [q, setQ] = useState("");
  const [openId, setOpenId] = useState<string | null>(null);

  const filtered = useMemo(() => {
    const needle = q.trim().toLowerCase();
    if (!needle) return items;
    return items.filter((i) => i.question.toLowerCase().includes(needle) || i.answer.toLowerCase().includes(needle));
  }, [q, items]);

  return (
    <div className="flex flex-col gap-4">
      <div className="relative max-w-md">
        <SearchIcon size={18} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-ink-subtle" />
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="Cari pertanyaan…"
          aria-label="Cari FAQ"
          className="w-full rounded-sm border border-border bg-surface py-2.5 pl-10 pr-3.5 text-sm outline-none focus:border-primary"
        />
      </div>

      {filtered.length === 0 ? (
        <p className="text-sm text-ink-muted">Tidak ada pertanyaan yang cocok.</p>
      ) : (
        <div className="overflow-hidden rounded-lg border border-border bg-surface">
          {filtered.map((item) => {
            const open = openId === item.id;
            return (
              <div key={item.id} className="border-b border-border last:border-0">
                <button
                  type="button"
                  aria-expanded={open}
                  onClick={() => setOpenId(open ? null : item.id)}
                  className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left text-[14.5px] font-bold text-ink hover:bg-surface-2"
                >
                  {item.question}
                  <span className={"shrink-0 text-ink-subtle transition-transform " + (open ? "rotate-90" : "")}>›</span>
                </button>
                {open && <p className="px-5 pb-4 text-[14px] leading-relaxed text-ink-muted">{item.answer}</p>}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
