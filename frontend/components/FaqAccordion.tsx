"use client";

import { useMemo, useState } from "react";
import { SearchIcon } from "@/components/ui";
import type { FaqItem } from "@/lib/content";

export function FaqAccordion({ items, searchable = true }: { items: FaqItem[]; searchable?: boolean }) {
  const [q, setQ] = useState("");
  const [openId, setOpenId] = useState<string | null>(null);

  const filtered = useMemo(() => {
    const needle = q.trim().toLowerCase();
    if (!searchable || !needle) return items;
    return items.filter((i) => i.question.toLowerCase().includes(needle) || i.answer.toLowerCase().includes(needle));
  }, [q, items, searchable]);

  return (
    <div className="flex flex-col gap-4">
      {searchable && <div className="relative max-w-md">
        <SearchIcon size={18} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-ink-subtle" />
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="Cari pertanyaan…"
          aria-label="Cari FAQ"
          className="w-full rounded-sm border border-border bg-surface py-2.5 pl-10 pr-3.5 text-sm outline-none focus:border-primary"
        />
      </div>}

      {filtered.length === 0 ? (
        <p className="text-sm text-ink-muted">Tidak ada pertanyaan yang cocok.</p>
      ) : (
        <div>
          {filtered.map((item) => {
            const open = openId === item.id;
            return (
              <div key={item.id} className="border-b border-surface-2">
                <button
                  type="button"
                  aria-expanded={open}
                  aria-controls={`faq-${item.id}`}
                  onClick={() => setOpenId(open ? null : item.id)}
                  className="flex w-full items-center justify-between gap-4 py-4 text-left text-[16px] font-bold text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                >
                  {item.question}
                  <span className={"shrink-0 text-ink-subtle transition-transform " + (open ? "rotate-90" : "")}>›</span>
                </button>
                <p id={`faq-${item.id}`} hidden={!open} className="max-w-[65ch] whitespace-pre-line pb-4 leading-relaxed text-ink-muted">{item.answer}</p>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
