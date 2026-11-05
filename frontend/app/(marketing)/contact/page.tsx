import type { Metadata } from "next";
import { PublicShell } from "@/components/PublicShell";
import { Lead, PageTitle, RunningHead } from "@/components/ui";
import { ContactForm } from "./ContactForm";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Kontak",
  description: "Hubungi tim INVERTA untuk pertanyaan tentang program, pembayaran, atau kerja sama.",
};

export default function ContactPage() {
  return (
    <PublicShell>
      <div className="grid gap-8 md:grid-cols-2 md:gap-12">
        <div>
          <RunningHead>Kontak</RunningHead>
          <PageTitle>Hubungi kami</PageTitle>
          <Lead>Pertanyaan soal program, pembayaran, atau kebutuhan tim? Kirim pesan.</Lead>
        </div>
        <div className="max-w-xl md:pt-2">
          <ContactForm />
        </div>
      </div>
    </PublicShell>
  );
}
