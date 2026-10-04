"use client";

import { useEffect, useState } from "react";
import { Toast } from "@/components/ui";

/**
 * Set by logout just before it navigates home, and read once here on whatever page that lands on.
 *
 * Session storage rather than a query parameter because home ("/") redirects server-side to the
 * programme landing, and that redirect drops the query string. This is a one-shot UI flag, not a
 * credential — tokens never go in web storage (GR-5).
 */
export const SIGNED_OUT_FLAG = "inverta:signed-out";

/** "Anda berhasil keluar." — shown once, at the top of the first page after logout. */
export function SignedOutNotice() {
  const [show, setShow] = useState(false);

  useEffect(() => {
    try {
      if (sessionStorage.getItem(SIGNED_OUT_FLAG) !== "1") return;
      sessionStorage.removeItem(SIGNED_OUT_FLAG);   // once only: a refresh must not repeat it
    } catch {
      return;                                      // storage blocked: signed out, just no notice
    }
    setShow(true);
    const t = setTimeout(() => setShow(false), 6000);
    return () => clearTimeout(t);
  }, []);

  if (!show) return null;

  return (
    <div className="fixed inset-x-0 top-4 z-50 flex justify-center px-4">
      <Toast
        tone="success"
        className="w-full max-w-sm"
        title="Anda berhasil keluar."
        description="Sampai jumpa lagi. Masuk kembali kapan saja untuk melanjutkan belajar."
        onClose={() => setShow(false)}
      />
    </div>
  );
}
