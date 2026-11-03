import Link from "next/link";
import { SESSION_TYPE_LABEL } from "@/lib/programs";

const UNLOCKS: Record<string, string> = {
  Video: "Selesaikan video dan tes singkatnya untuk membuka sesi berikutnya.",
  Live: "Hadiri sesi live ini untuk membuka sesi berikutnya.",
  FinalAssessment: "Kerjakan tes akhir untuk mendapatkan skor prediksi dan sertifikat Anda.",
};

/** The one thing to do next, dark so it reads first. Pure presentation: the href comes from nextHref(). */
export function NextUpPanel({
  title, href, type, partsDone, partCount,
}: { title: string; href: string; type: string; partsDone: number | null; partCount: number | null }) {
  const hint =
    (UNLOCKS[type] ?? SESSION_TYPE_LABEL[type] ?? "") +
    (partsDone != null && partCount != null && partCount > 0
      ? ` Bagian ${Math.min(partsDone + 1, partCount)} dari ${partCount}.`
      : "");
  return (
    <section className="bg-ink px-5 py-5 text-white" aria-label="Berikutnya">
      <p className="text-[12px] font-bold uppercase tracking-[0.08em] text-white/70">Berikutnya</p>
      <p className="mt-2 break-words text-[18px] font-extrabold leading-snug">{title}</p>
      <p className="mt-2 text-[14px] text-white/80">{hint}</p>
      <Link
        href={href}
        className="mt-4 inline-flex h-11 items-center justify-center rounded-sm bg-primary px-5 text-[14px] font-bold text-primary-ink hover:bg-primary-hover focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
      >
        Lanjutkan belajar
      </Link>
    </section>
  );
}
