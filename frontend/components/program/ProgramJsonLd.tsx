import type { FaqItem } from "@/lib/content";
import { num, type PublicProgram } from "@/lib/programs";

export function ProgramJsonLd({ program, faq }: { program: PublicProgram; faq: FaqItem[] }) {
  const data: object[] = [{
    "@context": "https://schema.org", "@type": "Course",
    name: program.name, description: program.summary ?? program.description,
    provider: { "@type": "Organization", name: "INVERTA" },
    offers: { "@type": "Offer", price: num(program.priceIdr), priceCurrency: "IDR" },
  }];
  if (faq.length > 0) data.push({
    "@context": "https://schema.org", "@type": "FAQPage",
    mainEntity: faq.map((f) => ({ "@type": "Question", name: f.question,
      acceptedAnswer: { "@type": "Answer", text: f.answer } })),
  });
  // `<` escaped so a "</script>" inside content cannot close the tag early.
  const json = JSON.stringify(data).replace(/</g, "\\u003c");
  return <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: json }} />;
}
