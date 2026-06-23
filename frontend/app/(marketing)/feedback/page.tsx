import type { Metadata } from "next";
import { PublicShell } from "@/components/PublicShell";
import { FeedbackForm } from "./FeedbackForm";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Masukan — AI Productivity Academy",
  description: "Bagikan saran, laporkan masalah, atau beri tahu kami apa yang Anda sukai.",
};

export default function FeedbackPage() {
  return (
    <PublicShell>
      <section className="px-6 py-14">
        <div className="mx-auto max-w-xl">
          <div className="mb-6 flex flex-col gap-2 text-center">
            <h1 className="text-3xl font-extrabold tracking-tight">Beri masukan</h1>
            <p className="text-[15px] text-ink-muted">Pendapat Anda membantu kami memperbaiki produk.</p>
          </div>
          <FeedbackForm />
        </div>
      </section>
    </PublicShell>
  );
}
