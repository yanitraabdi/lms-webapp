import Link from "next/link";

/** The one thing to do next, dark so it reads first. Pure presentation: the href comes from nextHref(). */
export function NextUpPanel({ title, href }: { title: string; href: string }) {
  return (
    <section className="bg-ink px-5 py-5 text-white" aria-label="Berikutnya">
      <p className="text-[12px] font-bold uppercase tracking-[0.08em] text-white/70">Berikutnya</p>
      <p className="mt-2 break-words text-[18px] font-extrabold leading-snug">{title}</p>
      <Link
        href={href}
        className="mt-4 inline-flex h-11 items-center justify-center rounded-sm bg-primary px-5 text-[14px] font-bold text-primary-ink hover:bg-primary-hover focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus"
      >
        Lanjutkan belajar
      </Link>
    </section>
  );
}
