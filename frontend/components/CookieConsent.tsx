"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Modal, Button, Switch } from "@/components/ui";

const KEY = "academy_cookie_consent";

export function CookieConsent() {
  const [show, setShow] = useState(false);
  const [manage, setManage] = useState(false);
  const [analytics, setAnalytics] = useState(false);
  const [marketing, setMarketing] = useState(false);

  useEffect(() => {
    try {
      if (!localStorage.getItem(KEY)) setShow(true);
    } catch {
      /* localStorage unavailable — don't block the page */
    }
  }, []);

  function persist(a: boolean, m: boolean) {
    try {
      localStorage.setItem(KEY, JSON.stringify({ essential: true, analytics: a, marketing: m, ts: Date.now() }));
    } catch {
      /* ignore */
    }
    setShow(false);
    setManage(false);
  }

  if (!show) return null;

  return (
    <>
      <div className="fixed inset-x-0 bottom-0 z-50 border-t border-border bg-surface px-6 py-4 shadow-lg">
        <div className="mx-auto flex max-w-5xl flex-col items-center gap-3 sm:flex-row sm:justify-between">
          <p className="text-[13px] leading-relaxed text-ink-muted">
            Kami menggunakan cookie 🍪 untuk menjalankan layanan dan meningkatkan pengalaman Anda. Lihat{" "}
            <Link href="/legal/cookies" className="font-bold text-primary hover:underline">Kebijakan Cookie</Link>.
          </p>
          <div className="flex shrink-0 gap-2">
            <Button variant="neutral" size="sm" onClick={() => setManage(true)}>Atur preferensi</Button>
            <Button variant="neutral" size="sm" onClick={() => persist(false, false)}>Tolak opsional</Button>
            <Button size="sm" onClick={() => persist(true, true)}>Terima semua</Button>
          </div>
        </div>
      </div>

      <Modal
        open={manage}
        onClose={() => setManage(false)}
        title="Preferensi cookie"
        footer={<Button fullWidth onClick={() => persist(analytics, marketing)}>Simpan preferensi</Button>}
      >
        <div className="flex flex-col gap-3 text-ink">
          <Row label="Esensial" desc="Diperlukan untuk login & keamanan. Selalu aktif." control={<Switch checked disabled aria-label="Esensial" />} />
          <Row label="Analitik" desc="Membantu kami memahami penggunaan." control={<Switch checked={analytics} onCheckedChange={setAnalytics} aria-label="Analitik" />} />
          <Row label="Marketing" desc="Penawaran yang dipersonalisasi." control={<Switch checked={marketing} onCheckedChange={setMarketing} aria-label="Marketing" />} />
        </div>
      </Modal>
    </>
  );
}

function Row({ label, desc, control }: { label: string; desc: string; control: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-base border border-border p-3">
      <div>
        <div className="text-[13.5px] font-bold text-ink">{label}</div>
        <div className="text-[12px] text-ink-muted">{desc}</div>
      </div>
      {control}
    </div>
  );
}
