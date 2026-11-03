import Link from "next/link";
import { Band } from "@/components/program/Band";
import { FaqAccordion } from "@/components/FaqAccordion";
import type { FaqItem } from "@/lib/content";

export function ProgramFaq({ items, hasMore }: { items: FaqItem[]; hasMore: boolean }) {
  if (items.length === 0) return null;
  return (
    <Band tint title="Pertanyaan umum">
      <div className="mt-6 max-w-3xl">
        <FaqAccordion items={items} searchable={false} />
      </div>
      {hasMore && (
        <Link
          href="/help"
          className="mt-4 inline-block rounded-sm text-sm font-bold text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
        >
          Lihat semua pertanyaan
        </Link>
      )}
    </Band>
  );
}
