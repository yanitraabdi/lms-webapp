"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, Spinner, ErrorState, ChevronRightIcon } from "@/components/ui";
import { Chapter, PageTitle } from "@/components/ui/Page";
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
      `Replace the conversion table with ${rows.length} rows?` +
      (existing ? `\n\n${existing} existing rows will be overwritten.` : ""))) return;

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
      setError(e instanceof Error ? e.message : "Couldn't save the table.");
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
    return <ErrorState title="Couldn't load the conversion table" />;
  }

  return (
    <div className="flex flex-col gap-4">
      <div>
        <Link href="/admin/programs" className="inline-flex items-center gap-1.5 text-[12.5px] font-bold text-ink-muted hover:text-ink">
          <ChevronRightIcon size={15} className="rotate-180" /> Program
        </Link>
        <PageTitle size="compact">Score conversion table</PageTitle>
        <p className="mt-1 text-[13px] text-ink-muted">
          Every raw score must be mapped exactly once. Without a complete table, certificates
          cannot be issued and the program cannot be published.
        </p>
      </div>

      {error && <div className="rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>}
      {saved && <div className="rounded-base bg-success-soft px-4 py-3 text-sm font-semibold text-success">Table saved</div>}

      <div>
        <ScoreBandPaste onParsed={apply} />
        {busy && <p className="mt-2 text-[12.5px] text-ink-muted">Saving…</p>}
      </div>

      <Chapter title="Currently saved">
        <div className="mb-3 mt-1 flex flex-wrap items-center justify-end gap-2">
          <div className="flex items-center gap-3">
            <span className="text-[12.5px] text-ink-muted">{q.data.length} rows</span>
            <Button variant="neutral" size="sm" onClick={download} disabled={q.data.length === 0}>
              Download (.tsv)
            </Button>
          </div>
        </div>

        {q.data.length === 0 ? (
          <p className="py-6 text-center text-sm text-ink-muted">No conversion table yet.</p>
        ) : (
          <div className="max-h-[320px] overflow-auto">
            <table className="data-table">
              <thead className="sticky top-0 bg-surface">
                <tr>
                  <th>Section</th>
                  <th>Min</th>
                  <th>Max</th>
                  <th>Scaled</th>
                  <th>Label</th>
                </tr>
              </thead>
              <tbody>
                {q.data.map((b) => (
                  <tr key={b.id}>
                    <td>{b.section}</td>
                    <td>{num(b.minRaw)}</td>
                    <td>{num(b.maxRaw)}</td>
                    <td className="font-semibold">{num(b.scaledScore)}</td>
                    <td className="text-ink-muted">{b.predictedBand ?? "None"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Chapter>
    </div>
  );
}
