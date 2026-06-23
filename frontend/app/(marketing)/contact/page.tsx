import type { Metadata } from "next";
import { PublicShell } from "@/components/PublicShell";
import { ContactForm } from "./ContactForm";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Kontak — AI Productivity Academy",
  description: "Hubungi tim AI Productivity Academy untuk pertanyaan tentang langganan, tagihan, atau kerja sama.",
};

export default function ContactPage() {
  return (
    <PublicShell>
      <section className="px-6 py-14">
        <div className="mx-auto max-w-xl">
          <div className="mb-6 flex flex-col gap-2 text-center">
            <h1 className="text-3xl font-extrabold tracking-tight">Hubungi kami</h1>
            <p className="text-[15px] text-ink-muted">Pertanyaan soal langganan, tagihan, atau kebutuhan tim? Kirim pesan.</p>
          </div>
          <ContactForm />
        </div>
      </section>
    </PublicShell>
  );
}
