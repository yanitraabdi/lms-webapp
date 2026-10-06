import Link from "next/link";
import { FaqAccordion } from "@/components/FaqAccordion";
import type { FaqItem } from "@/lib/content";

export function ProgramFaq({ items, hasMore }: { items: FaqItem[]; hasMore: boolean }) {
  if (items.length === 0) return null;
  return (
    <section className="px-6 py-12">
      <div className="mx-auto max-w-3xl">
        <h2 className="mb-5 text-2xl font-extrabold tracking-tight">Pertanyaan yang sering diajukan</h2>
        <FaqAccordion items={items} searchable={false} />
        {hasMore && (
          <Link
            href="/help"
            className="mt-4 inline-block rounded-sm text-sm font-bold text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          >
            Lihat semua pertanyaan →
          </Link>
        )}
      </div>
    </section>
  );
}
