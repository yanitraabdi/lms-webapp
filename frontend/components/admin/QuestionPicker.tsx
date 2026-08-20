"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ErrorState, Spinner, SearchIcon } from "@/components/ui";
import { listQuestions, QUESTION_SECTIONS } from "@/lib/sessions";

/** Bank picker. Selection order is preserved — it becomes the question order in the test. */
export function QuestionPicker({
  token, section, selected, onChange,
}: {
  token: string;
  section?: string;
  selected: string[];
  onChange: (ids: string[]) => void;
}) {
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState(section ?? "");

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  const q = useQuery({
    queryKey: ["picker-questions", filter, search],
    queryFn: () => listQuestions(token, { section: filter || undefined, search: search || undefined }),
  });

  function toggle(id: string) {
    onChange(selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id]);
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-2">
        {!section && (
          <select
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            className="rounded-sm border border-border bg-surface px-2.5 py-2 text-[13px] outline-none focus:border-primary"
          >
            <option value="">Semua bagian</option>
            {QUESTION_SECTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        )}
        <div className="relative flex-1">
          <SearchIcon size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-subtle" />
          <input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Cari soal…"
            className="w-full rounded-sm border border-border bg-surface py-2 pl-8 pr-3 text-[13px] outline-none focus:border-primary"
          />
        </div>
        <span className="shrink-0 text-[12px] font-bold text-ink-muted">{selected.length} dipilih</span>
      </div>

      {q.isPending ? (
        <div className="flex min-h-[120px] items-center justify-center"><Spinner size={20} /></div>
      ) : q.isError ? (
        // A failed bank query is NOT an empty bank — saying "add some questions first" here
        // sends the operator off to author content that already exists.
        <ErrorState
          title="Bank soal gagal dimuat"
          message="Coba muat ulang halaman. Soal yang sudah ada tidak hilang."
        />
      ) : (q.data ?? []).length === 0 ? (
        <p className="py-6 text-center text-sm text-ink-muted">
          Tidak ada soal. Tambahkan di halaman Bank Soal terlebih dahulu.
        </p>
      ) : (
        <ul className="flex max-h-[320px] flex-col gap-1 overflow-y-auto">
          {(q.data ?? []).map((item) => {
            const on = selected.includes(item.id);
            const order = selected.indexOf(item.id) + 1;
            return (
              <li key={item.id}>
                <label
                  className={
                    "flex cursor-pointer items-start gap-2.5 rounded-base border px-3 py-2 text-[13px] " +
                    (on ? "border-primary bg-primary-soft/40" : "border-border hover:bg-surface-2")
                  }
                >
                  <input type="checkbox" checked={on} onChange={() => toggle(item.id)} className="mt-0.5 accent-primary" />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-semibold text-ink">{item.prompt}</span>
                    <span className="block text-[11px] text-ink-subtle">
                      {item.section} · {item.choices.length} pilihan
                      {on && ` · urutan ${order}`}
                    </span>
                  </span>
                </label>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
