import { Chapter } from "@/components/ui";
import { HOW_IT_WORKS_STEPS } from "./howItWorksSteps";

export function HowItWorks() {
  return (
    <Chapter title="Cara kerja">
      <ol className="mt-4 flex list-decimal flex-col gap-4 pl-6 marker:font-extrabold marker:text-primary">
        {HOW_IT_WORKS_STEPS.map((s) => (
          <li key={s.n} className="pl-1">
            <span className="block font-bold text-ink">{s.title}</span>
            <span className="block max-w-[60ch] text-ink-muted">{s.body}</span>
          </li>
        ))}
      </ol>
    </Chapter>
  );
}
