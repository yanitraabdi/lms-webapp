import Link from "next/link";
import { Chapter } from "@/components/ui";
import { FaqAccordion } from "@/components/FaqAccordion";
import type { FaqItem } from "@/lib/content";

export function ProgramFaq({ items, hasMore }: { items: FaqItem[]; hasMore: boolean }) {
  if (items.length === 0) return null;
  return (
    <Chapter title="Pertanyaan umum">
      <div className="mt-3">
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
    </Chapter>
  );
}
