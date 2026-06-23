import { Suspense } from "react";
import type { Metadata } from "next";
import { SuccessClient } from "./SuccessClient";

export const dynamic = "force-dynamic";

// Informational only — entitlement is granted by the verified webhook, never here (GR-2).
export const metadata: Metadata = { title: "Status pembayaran — AI Productivity Academy" };

export default function CheckoutSuccessPage() {
  return (
    <Suspense fallback={null}>
      <SuccessClient />
    </Suspense>
  );
}
