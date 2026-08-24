"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, Spinner, ErrorState, ChevronRightIcon } from "@/components/ui";
import { ScoreBandPaste, type ParsedBand } from "@/components/admin/ScoreBandPaste";
import { listScoreBands, replaceScoreBands, num } from "@/lib/sessions";

export default function ScoreBandsPage() {
  const token = useAuth().accessToken;
  const programId = useParams<{ id: string }>().id;
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const q = useQuery({
    queryKey: ["score-bands", programId],
    queryFn: () => listScoreBands(token!, programId),
    enabled: !!token,
  });

  async function apply(rows: ParsedBand[]) {
    const existing = q.data?.length ?? 0;
    if (!confirm(
      `Ganti tabel konversi dengan ${rows.length} baris?` +
      (existing ? `\n\n${existing} baris yang ada akan ditimpa.` : ""))) return;

    setBusy(true); setError(null); setSaved(false);
    try {
      await replaceScoreBands(token!, programId, rows.map((r) => ({
        id: "00000000-0000-0000-0000-000000000000",
        section: r.section,
        minRaw: r.minRaw,
        maxRaw: r.maxRaw,
        scaledScore: r.scaledScore,
        predictedBand: r.predictedBand,
      })));
      await q.refetch();
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan tabel.");
    } finally {
      setBusy(false);
    }
  }

  /** Round-trip: export what is stored so it can be corrected in a spreadsheet and pasted back. */
  function download() {
    const tsv = (q.data ?? [])
      .map((b) => [b.section, num(b.minRaw), num(b.maxRaw), num(b.scaledScore), b.predictedBand ?? ""].join("\t"))
      .join("\n");
    const url = URL.createObjectURL(new Blob([tsv], { type: "text/tab-separated-values" }));
    const a = document.createElement("a");
    a.href = url;
    a.download = `score-bands-${programId}.tsv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  if (!token || q.isPending) {
    return <div className="flex min-h-[300px] items-center justify-center"><Spinner size={24} /></div>;
  }
  if (q.isError) {
    return <ErrorState title="Gagal memuat tabel konversi" />;
  }

  return (
    <div className="flex flex-col gap-4">
      <div>
        <Link href="/admin/programs" className="inline-flex items-center gap-1.5 text-[12.5px] font-bold text-ink-muted hover:text-ink">
          <ChevronRightIcon size={15} className="rotate-180" /> Program
        </Link>
        <h1 className="mt-1 text-xl font-extrabold tracking-tight">Tabel konversi skor</h1>
        <p className="mt-1 text-[13px] text-ink-muted">
          Setiap skor mentah harus dipetakan tepat satu kali. Tanpa tabel yang lengkap, sertifikat
          tidak dapat diterbitkan dan program tidak dapat diterbitkan.
        </p>
      </div>

      {error && <div className="rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>}
      {saved && <div className="rounded-base bg-success-soft px-4 py-3 text-sm font-semibold text-success">Tabel tersimpan ✓</div>}

      <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
        <ScoreBandPaste onParsed={apply} />
        {busy && <p className="mt-2 text-[12.5px] text-ink-muted">Menyimpan…</p>}
      </div>

      <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-base font-extrabold">Tersimpan saat ini</h2>
          <div className="flex items-center gap-3">
            <span className="text-[12.5px] text-ink-muted">{q.data.length} baris</span>
            <Button variant="neutral" size="sm" onClick={download} disabled={q.data.length === 0}>
              Unduh (.tsv)
            </Button>
          </div>
        </div>

        {q.data.length === 0 ? (
          <p className="py-6 text-center text-sm text-ink-muted">Belum ada tabel konversi.</p>
        ) : (
          <div className="max-h-[320px] overflow-y-auto">
            <table className="w-full border-collapse text-[13px]">
              <thead className="sticky top-0 bg-surface-2">
                <tr className="text-left text-xs font-bold text-ink-muted">
                  <th className="px-3 py-2">Bagian</th>
                  <th className="px-3 py-2">Min</th>
                  <th className="px-3 py-2">Max</th>
                  <th className="px-3 py-2">Skala</th>
                  <th className="px-3 py-2">Label</th>
                </tr>
              </thead>
              <tbody>
                {q.data.map((b) => (
                  <tr key={b.id} className="border-t border-border">
                    <td className="px-3 py-1.5">{b.section}</td>
                    <td className="px-3 py-1.5">{num(b.minRaw)}</td>
                    <td className="px-3 py-1.5">{num(b.maxRaw)}</td>
                    <td className="px-3 py-1.5 font-semibold">{num(b.scaledScore)}</td>
                    <td className="px-3 py-1.5 text-ink-muted">{b.predictedBand ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
