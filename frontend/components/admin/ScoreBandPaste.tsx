"use client";

import { useState } from "react";
import { Button } from "@/components/ui";

export type ParsedBand = {
  section: string;
  minRaw: number;
  maxRaw: number;
  scaledScore: number;
  predictedBand: string | null;
};

const SECTIONS = ["Listening", "Structure", "Reading", "Vocabulary", "General"];

/** ITP limits: max raw score, and the highest legal scaled score. Reading tops out one lower. */
const LIMITS: Record<string, [number, number]> = {
  Listening: [50, 68],
  Structure: [40, 68],
  Reading: [50, 67],
};
const SCALED_MIN = 31;

/** Strict integer cell: rejects "", "1e2", "0x10", "5.5", " 5 " (already trimmed) — anything Number() would coerce. */
const INT_RE = /^-?\d+$/;

/** Parses `section, minRaw, maxRaw, scaled[, band]` rows. Tab-separated if the line has a tab, else comma-separated. */
export function parseBands(text: string): { rows: ParsedBand[]; errors: string[] } {
  const rows: ParsedBand[] = [];
  const errors: string[] = [];

  text.split(/\r?\n/).forEach((line, i) => {
    const trimmed = line.trim();
    if (!trimmed) return;

    // Split on tab when present so a comma inside a band label (tab-separated paste) survives intact.
    const cells = (trimmed.includes("\t") ? trimmed.split("\t") : trimmed.split(",")).map((c) => c.trim());
    if (i === 0 && /^section$/i.test(cells[0])) return;          // spreadsheet header row

    const lineNo = i + 1;
    if (cells.length < 4) {
      errors.push(`Baris ${lineNo}: butuh 4 kolom (bagian, min, max, skala).`);
      return;
    }
    if (cells.length > 5) {
      errors.push(`Baris ${lineNo}: terlalu banyak kolom (maks. 5 — bagian, min, max, skala, label).`);
      return;
    }

    const [rawSection, min, max, scaled, band] = cells;
    const section = SECTIONS.find((s) => s.toLowerCase() === rawSection.toLowerCase());
    if (!section) {
      errors.push(`Baris ${lineNo}: bagian "${rawSection}" tidak dikenal.`);
      return;
    }

    if (![min, max, scaled].every((v) => INT_RE.test(v))) {
      errors.push(`Baris ${lineNo}: min, max dan skala harus bilangan bulat (tidak boleh kosong).`);
      return;
    }

    const [minRaw, maxRaw, scaledScore] = [min, max, scaled].map(Number);
    if (minRaw > maxRaw) {
      errors.push(`Baris ${lineNo}: min (${minRaw}) lebih besar dari max (${maxRaw}).`);
      return;
    }

    const limit = LIMITS[section];
    if (limit && maxRaw > limit[0]) {
      errors.push(`Baris ${lineNo}: max (${maxRaw}) melebihi batas skor mentah ${section} (${limit[0]}).`);
      return;
    }

    rows.push({ section, minRaw, maxRaw, scaledScore, predictedBand: band?.trim() || null });
  });

  return { rows, errors };
}

export type CoverageRow = {
  section: string;
  maxRaw: number;
  missing: number[];
  duplicated: number[];
  outOfRange: number;
  complete: boolean;
};

/** Every raw score 0..max must be covered exactly once, with a scaled value in range. */
export function coverage(rows: ParsedBand[]): CoverageRow[] {
  return Object.entries(LIMITS).map(([section, [maxRaw, scaledMax]]) => {
    const mine = rows.filter((r) => r.section === section);
    const missing: number[] = [];
    const duplicated: number[] = [];

    for (let raw = 0; raw <= maxRaw; raw++) {
      const hits = mine.filter((r) => raw >= r.minRaw && raw <= r.maxRaw).length;
      if (hits === 0) missing.push(raw);
      else if (hits > 1) duplicated.push(raw);
    }

    const outOfRange = mine.filter((r) => r.scaledScore < SCALED_MIN || r.scaledScore > scaledMax).length;
    return {
      section, maxRaw, missing, duplicated, outOfRange,
      complete: missing.length === 0 && duplicated.length === 0 && outOfRange === 0,
    };
  });
}

function list(values: number[]): string {
  return values.length <= 6 ? values.join(", ") : `${values[0]}–${values[values.length - 1]} (${values.length} nilai)`;
}

/** Paste box plus per-section coverage summary. Nothing leaves here until the input parses. */
export function ScoreBandPaste({ onParsed }: { onParsed: (rows: ParsedBand[]) => void }) {
  const [text, setText] = useState("");
  const parsed = text.trim() ? parseBands(text) : null;
  const cover = parsed && parsed.errors.length === 0 ? coverage(parsed.rows) : null;

  return (
    <div className="flex flex-col gap-3">
      <label className="flex flex-col gap-1">
        <span className="text-[12px] font-bold text-ink-muted">
          Tempel tabel dari spreadsheet — kolom: bagian, min, max, skala, label (opsional)
        </span>
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          rows={8}
          placeholder={"Listening\t0\t0\t31\nListening\t1\t1\t32"}
          className="w-full rounded-sm border border-border bg-surface px-3 py-2 font-mono text-[12.5px] outline-none focus:border-primary"
        />
      </label>

      {parsed && parsed.errors.length > 0 && (
        <div className="rounded-base bg-danger-soft px-4 py-3 text-[13px] text-danger">
          <p className="font-bold">{parsed.errors.length} baris bermasalah — tidak ada yang disimpan:</p>
          <ul className="mt-1 list-inside list-disc">
            {parsed.errors.slice(0, 8).map((e) => <li key={e}>{e}</li>)}
          </ul>
          {parsed.errors.length > 8 && <p className="mt-1">…dan {parsed.errors.length - 8} lainnya.</p>}
        </div>
      )}

      {cover && (
        <div className="rounded-base border border-border bg-surface-2 px-4 py-3 text-[12.5px]">
          <p className="font-bold text-ink">{parsed!.rows.length} baris terbaca</p>
          <ul className="mt-1.5 flex flex-col gap-1">
            {cover.map((c) => (
              <li key={c.section} className={c.complete ? "text-success" : "text-warning"}>
                <strong>{c.section}</strong>{" "}
                {c.complete
                  ? `lengkap (0–${c.maxRaw})`
                  : [
                      c.missing.length ? `belum dipetakan: ${list(c.missing)}` : null,
                      c.duplicated.length ? `tumpang tindih: ${list(c.duplicated)}` : null,
                      c.outOfRange ? `${c.outOfRange} skala di luar rentang` : null,
                    ].filter(Boolean).join(" · ")}
              </li>
            ))}
          </ul>

          {/* Safety net: the exact payload that will be sent, before it is sent. */}
          <details className="mt-2.5">
            <summary className="cursor-pointer text-[12px] font-bold text-ink-muted hover:text-ink">
              Lihat {parsed!.rows.length} baris yang akan disimpan
            </summary>
            <div className="mt-1.5 max-h-[220px] overflow-y-auto rounded-sm border border-border">
              <table className="w-full border-collapse text-[12px]">
                <thead className="sticky top-0 bg-surface">
                  <tr className="text-left font-bold text-ink-muted">
                    <th className="px-2 py-1">Bagian</th>
                    <th className="px-2 py-1">Min</th>
                    <th className="px-2 py-1">Max</th>
                    <th className="px-2 py-1">Skala</th>
                    <th className="px-2 py-1">Label</th>
                  </tr>
                </thead>
                <tbody>
                  {parsed!.rows.map((r, idx) => (
                    <tr key={idx} className="border-t border-border">
                      <td className="px-2 py-1">{r.section}</td>
                      <td className="px-2 py-1">{r.minRaw}</td>
                      <td className="px-2 py-1">{r.maxRaw}</td>
                      <td className="px-2 py-1 font-semibold">{r.scaledScore}</td>
                      <td className="px-2 py-1 text-ink-muted">{r.predictedBand ?? "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </details>
        </div>
      )}

      <Button
        size="sm"
        className="self-start"
        disabled={!parsed || parsed.errors.length > 0 || parsed.rows.length === 0}
        onClick={() => parsed && onParsed(parsed.rows)}
      >
        Gunakan tabel ini
      </Button>
    </div>
  );
}
