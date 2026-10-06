"use client";
import { Bar, BarChart, LabelList, ResponsiveContainer, XAxis, YAxis } from "recharts";
import { useCssVar, usePrefersReducedMotion } from "@/lib/useCssVar";

const SECTIONS = ["Listening", "Structure", "Reading"] as const;

/** Per-section scaled predicted scores on the ITP section scale (31–68). Loaded via next/dynamic. */
export default function FinalResultChart({ scores }: { scores: Record<string, number> }) {
  const primary = useCssVar("--color-primary", "#7F00FF");
  const muted = useCssVar("--color-ink-muted", "#51607a");
  const reduced = usePrefersReducedMotion();
  const data = SECTIONS.filter((s) => scores[s] != null).map((section) => ({ section, score: scores[section] }));

  return (
    <div aria-hidden="true">
      <ResponsiveContainer width="100%" height={160}>
        <BarChart layout="vertical" data={data} margin={{ top: 4, right: 28, bottom: 4, left: 0 }}>
          <XAxis type="number" domain={[31, 68]} tick={{ fill: muted, fontSize: 11 }} />
          <YAxis type="category" dataKey="section" width={80} tick={{ fill: muted, fontSize: 12 }} />
          <Bar dataKey="score" fill={primary} radius={[0, 4, 4, 0]} isAnimationActive={!reduced}>
            <LabelList dataKey="score" position="right" fill={muted} fontSize={12} />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
