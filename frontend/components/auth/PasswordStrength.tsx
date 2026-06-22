"use client";

import { cn } from "@/lib/cn";

function score(pw: string): number {
  let s = 0;
  if (pw.length >= 8) s++;
  if (/[a-z]/.test(pw) && /[A-Z]/.test(pw)) s++;
  if (/\d/.test(pw)) s++;
  if (/[^A-Za-z0-9]/.test(pw)) s++;
  return s; // 0..4
}

const labels = ["Terlalu lemah", "Lemah", "Cukup", "Kuat", "Kuat — bagus!"];

export function PasswordStrength({ value }: { value: string }) {
  if (!value) return null;
  const s = score(value);
  const fill = s >= 3 ? "bg-success" : s === 2 ? "bg-warning" : "bg-danger";
  const text = s >= 3 ? "text-success" : s === 2 ? "text-warning" : "text-danger";
  return (
    <div className="mt-1 flex flex-col gap-1">
      <div className="flex gap-1.5">
        {[0, 1, 2, 3].map((i) => (
          <div key={i} className={cn("h-[5px] flex-1 rounded-full", i < s ? fill : "bg-surface-2")} />
        ))}
      </div>
      <span className={cn("text-[11.5px] font-semibold", text)}>{labels[s]}</span>
    </div>
  );
}
