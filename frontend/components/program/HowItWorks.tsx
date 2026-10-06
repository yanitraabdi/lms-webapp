import { HOW_IT_WORKS_STEPS } from "./howItWorksSteps";

export function HowItWorks() {
  return (
    <section className="border-b border-border bg-surface px-6 py-12">
      <div className="mx-auto max-w-5xl">
        <h2 className="mb-6 text-2xl font-extrabold tracking-tight">Cara kerja</h2>
        <ol className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {HOW_IT_WORKS_STEPS.map((s) => (
            <li key={s.n} className="flex flex-col gap-2">
              <span className="flex h-10 w-10 items-center justify-center rounded-full bg-primary-soft text-base font-extrabold text-primary">
                {s.n}
              </span>
              <span className="text-base font-extrabold">{s.title}</span>
              <span className="text-[13.5px] text-ink-muted">{s.body}</span>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}
