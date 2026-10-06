"use client";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Bar, BarChart, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { listMySessionResults, num, type SessionAttempt } from "@/lib/programs";
import { useCssVar, usePrefersReducedMotion } from "@/lib/useCssVar";

/** Block 3: session-test attempts, one small bar chart per test part. Loaded via next/dynamic. */
export default function SessionTestHistory({
  token, programId, continueHref,
}: { token: string; programId: string; continueHref: string | null }) {
  const q = useQuery({ queryKey: ["my-session-results"], queryFn: () => listMySessionResults(token) });
  const success = useCssVar("--color-success", "#0e8a4f");
  const primary = useCssVar("--color-primary", "#7F00FF");
  const muted = useCssVar("--color-ink-muted", "#51607a");
  const reduced = usePrefersReducedMotion();

  if (q.isPending) {
    return <section className="min-h-[200px] rounded-lg border border-border bg-surface shadow-sm" aria-busy="true" />;
  }
  if (q.isError) {
    return (
      <section className="rounded-lg border border-border bg-surface p-5 shadow-sm">
        <p className="text-[13px] text-ink-muted">Riwayat tes sesi tidak dapat dimuat.</p>
      </section>
    );
  }

  // Rows arrive ordered by submittedAt, so each group's attempts stay chronological.
  const groups = new Map<string, SessionAttempt[]>();
  for (const a of q.data.filter((r) => r.programId === programId)) {
    groups.set(a.partId, [...(groups.get(a.partId) ?? []), a]);
  }
  const ordered = [...groups.values()].sort((x, y) =>
    num(x[0].sessionOrderIndex) - num(y[0].sessionOrderIndex) || num(x[0].partOrderIndex) - num(y[0].partOrderIndex));

  return (
    <section className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-5 shadow-sm">
      <h2 className="text-lg font-extrabold">Riwayat tes sesi</h2>
      {ordered.length === 0 ? (
        <div className="flex flex-col gap-2">
          <p className="text-[13px] leading-relaxed text-ink-muted">
            Tes sesi membuka bagian atau sesi berikutnya. Hasilnya akan tampil di sini.
          </p>
          {continueHref && (
            <Link href={continueHref} className="self-start text-[13px] font-bold text-primary hover:underline">
              Mulai belajar →
            </Link>
          )}
        </div>
      ) : (
        ordered.map((attempts) => {
          const first = attempts[0];
          const last = attempts[attempts.length - 1];
          const data = attempts.map((a, i) => {
            const score = num(a.score);
            const max = num(a.maxScore);
            return { label: `#${i + 1}`, pct: max > 0 ? Math.round((score / max) * 100) : 0, score, max, passed: a.passed };
          });
          return (
            <div key={first.partId} className="flex flex-col gap-1">
              <h3 className="text-[13px] font-bold text-ink">
                Sesi {num(first.sessionOrderIndex)} · {first.partTitle}
              </h3>
              <div aria-hidden="true">
                <ResponsiveContainer width="100%" height={110}>
                  <BarChart data={data} margin={{ top: 4, right: 4, bottom: 0, left: 4 }}>
                    <XAxis dataKey="label" tick={{ fill: muted, fontSize: 11 }} />
                    <YAxis domain={[0, 100]} hide />
                    <Tooltip
                      cursor={false}
                      formatter={(_v, _n, item) => [`${item.payload.score}/${item.payload.max}`, "Skor"]}
                    />
                    <Bar dataKey="pct" maxBarSize={40} radius={[4, 4, 0, 0]} isAnimationActive={!reduced}>
                      {data.map((d) => <Cell key={d.label} fill={d.passed ? success : primary} />)}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
              <ul className="sr-only">
                {data.map((d, i) => (
                  <li key={d.label}>
                    Percobaan {i + 1}: {d.score} dari {d.max}, {d.passed ? "lulus" : "belum lulus"}
                  </li>
                ))}
              </ul>
              <p className="text-[12px] text-ink-muted">
                <span className={last.passed ? "font-bold text-success" : "font-bold text-ink"}>
                  {last.passed ? "Lulus" : "Belum lulus"}
                </span>
                {" · "}{attempts.length} percobaan
              </p>
            </div>
          );
        })
      )}
    </section>
  );
}
