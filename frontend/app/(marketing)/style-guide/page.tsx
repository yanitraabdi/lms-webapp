"use client";

import { useState } from "react";
import { PublicNav } from "@/components/PublicNav";
import {
  Badge,
  Button,
  Card,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  Input,
  Modal,
  ModuleCard,
  ProgressBar,
  ProgressRing,
  Radio,
  Skeleton,
  Switch,
  TagChip,
  Toast,
} from "@/components/ui";

const colors: Array<{ name: string; hex: string; cls: string; border?: boolean }> = [
  { name: "primary", hex: "#0050E6", cls: "bg-primary" },
  { name: "primary-hover", hex: "#0040C0", cls: "bg-primary-hover" },
  { name: "primary-soft", hex: "#E5EDFF", cls: "bg-primary-soft", border: true },
  { name: "accent-strong", hex: "#FFB81C", cls: "bg-accent-strong" },
  { name: "accent", hex: "#C2710C", cls: "bg-accent" },
  { name: "accent-soft", hex: "#FEF1D2", cls: "bg-accent-soft", border: true },
  { name: "ink", hex: "#11253F", cls: "bg-ink" },
  { name: "ink-muted", hex: "#51607A", cls: "bg-ink-muted" },
  { name: "ink-subtle", hex: "#8A99AE", cls: "bg-ink-subtle" },
  { name: "success", hex: "#0E8A4F", cls: "bg-success" },
  { name: "warning", hex: "#B45309", cls: "bg-warning" },
  { name: "danger", hex: "#C62828", cls: "bg-danger" },
  { name: "surface-2", hex: "#EEF3F9", cls: "bg-surface-2", border: true },
  { name: "bg", hex: "#F4F7FB", cls: "bg-bg", border: true },
];

function Section({ title, eyebrow, children }: { title: string; eyebrow?: string; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-4">
      <div className="flex items-baseline gap-3">
        {eyebrow && (
          <span className="text-xs font-bold uppercase tracking-widest text-primary">{eyebrow}</span>
        )}
        <h2 className="text-xl font-bold tracking-tight text-ink">{title}</h2>
      </div>
      {children}
    </section>
  );
}

export default function StyleGuidePage() {
  const [modalOpen, setModalOpen] = useState(false);
  const [toggles, setToggles] = useState({ a: true, b: false });

  return (
    <>
      <PublicNav />
      <main className="mx-auto flex max-w-5xl flex-col gap-12 px-6 py-12">
        <header className="flex flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="flex h-8 w-8 items-center justify-center rounded-[9px] bg-primary text-base font-extrabold text-primary-ink">
              A
            </span>
            <span className="text-sm font-bold">AI Productivity Academy</span>
            <Badge promo="new">Style guide</Badge>
          </div>
          <h1 className="text-3xl font-extrabold tracking-tight text-ink">Fondasi sistem desain &amp; kit komponen</h1>
          <p className="max-w-2xl text-[15px] leading-relaxed text-ink-muted">
            Komponen yang dapat dipakai ulang, dipetakan ke token <code className="font-mono text-[13px]">--ds-*</code>.
            Light-mode, WCAG 2.1 AA, Bahasa Indonesia.
          </p>
        </header>

        {/* Colors */}
        <Section eyebrow="Fondasi" title="Token warna">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4">
            {colors.map((c) => (
              <div key={c.name} className="flex items-center gap-2.5">
                <span className={`h-9 w-9 rounded-base ${c.cls} ${c.border ? "border border-border" : ""}`} />
                <span className="flex flex-col">
                  <span className="text-xs font-semibold text-ink">{c.name}</span>
                  <span className="font-mono text-[11px] text-ink-subtle">{c.hex}</span>
                </span>
              </div>
            ))}
          </div>
        </Section>

        {/* Buttons */}
        <Section eyebrow="Kit" title="Tombol">
          <Card className="flex flex-col gap-4">
            <div className="flex flex-wrap items-center gap-2.5">
              <Button>Primary</Button>
              <Button variant="secondary">Sekunder</Button>
              <Button variant="ghost">Ghost</Button>
              <Button variant="neutral">Netral</Button>
              <Button variant="danger">Hapus</Button>
            </div>
            <div className="flex flex-wrap items-center gap-2.5 border-t border-surface-2 pt-4">
              <Button size="sm">Kecil</Button>
              <Button size="md">Sedang</Button>
              <Button size="lg">Besar (CTA)</Button>
              <Button loading>Memproses…</Button>
              <Button disabled>Nonaktif</Button>
            </div>
          </Card>
        </Section>

        {/* Forms */}
        <Section eyebrow="Kit" title="Input &amp; formulir">
          <div className="grid gap-4 md:grid-cols-2">
            <Card className="flex flex-col gap-4">
              <Field label="Email kerja" htmlFor="sg-email" hint="Gunakan email kerja untuk sertifikat Anda.">
                <Input id="sg-email" placeholder="nama@perusahaan.com" />
              </Field>
              <Field label="Kata sandi" htmlFor="sg-pass">
                <Input id="sg-pass" type="password" placeholder="Minimal 8 karakter" />
              </Field>
              <Field label="Email" htmlFor="sg-bad" error="Format email tidak valid.">
                <Input id="sg-bad" defaultValue="budi@@mail" invalid />
              </Field>
            </Card>
            <Card className="flex flex-col gap-4">
              <div className="flex flex-wrap items-center gap-5">
                <Checkbox label="Checkbox" defaultChecked />
                <Radio name="sg-radio" label="Radio" defaultChecked />
              </div>
              <div className="flex items-center gap-3 border-t border-surface-2 pt-4">
                <Switch checked={toggles.a} onCheckedChange={(v) => setToggles((t) => ({ ...t, a: v }))} aria-label="Toggle A" />
                <span className="text-sm text-ink-muted">Toggle aktif</span>
              </div>
              <div className="flex items-center gap-3">
                <Switch checked={toggles.b} onCheckedChange={(v) => setToggles((t) => ({ ...t, b: v }))} aria-label="Toggle B" />
                <span className="text-sm text-ink-muted">Toggle nonaktif</span>
              </div>
            </Card>
          </div>
        </Section>

        {/* Badges */}
        <Section eyebrow="Kit" title="Badge">
          <Card className="flex flex-col gap-4">
            <div className="flex flex-wrap gap-2.5">
              <Badge tier="free" />
              <Badge tier="beginner" />
              <Badge tier="intermediate" />
              <Badge tier="advanced" />
            </div>
            <div className="flex flex-wrap gap-2.5 border-t border-surface-2 pt-4">
              <Badge status="not-started" />
              <Badge status="in-progress" />
              <Badge status="completed" />
              <Badge status="locked" />
            </div>
            <div className="flex flex-wrap items-center gap-2.5 border-t border-surface-2 pt-4">
              <Badge promo="popular" />
              <Badge promo="new" />
              <TagChip>Basic</TagChip>
              <TagChip>Advanced</TagChip>
            </div>
          </Card>
        </Section>

        {/* Progress */}
        <Section eyebrow="Kit" title="Progres">
          <Card className="flex flex-col gap-5">
            <ProgressBar label="Level Basic" valueLabel="8/12 · 67%" value={67} />
            <div className="flex items-center gap-5">
              <ProgressRing value={67} />
              <div className="flex flex-1 flex-col gap-2.5">
                <ProgressBar label="Prompting" value={100} tone="success" size="sm" />
                <ProgressBar label="Tool Landscape" value={50} size="sm" />
                <ProgressBar label="Quick Wins" value={0} size="sm" />
              </div>
            </div>
          </Card>
        </Section>

        {/* Module cards */}
        <Section eyebrow="Kit" title="Kartu modul">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <ModuleCard
              title="Menulis prompt pertama Anda"
              levelLabel="Basic"
              state="entitled"
              status="in-progress"
              durationLabel="8 mnt"
              rating={4.8}
              learnersLabel="2,1rb peserta"
              progress={45}
              ctaLabel="Lanjutkan · 45%"
            />
            <ModuleCard
              title="Apa itu AI? Penjelasan tanpa istilah rumit"
              levelLabel="Basic"
              state="preview"
              durationLabel="7 mnt"
              rating={4.9}
              learnersLabel="8,4rb peserta"
              ctaLabel="Tonton gratis"
            />
            <ModuleCard
              title="Membangun workflow tim dengan Claude Projects"
              levelLabel="Intermediate"
              state="locked"
              durationLabel="9 mnt"
              tags={["claude", "workflow"]}
              ctaLabel="Tingkatkan ke Intermediate"
            />
          </div>
        </Section>

        {/* Modal + toasts */}
        <Section eyebrow="Kit" title="Modal &amp; toast">
          <div className="grid gap-4 md:grid-cols-2">
            <Card className="flex items-center">
              <Button onClick={() => setModalOpen(true)}>Buka modal</Button>
            </Card>
            <div className="flex flex-col gap-2.5">
              <Toast tone="success" title="Progres tersimpan" description="Lanjutkan kapan saja dari posisi terakhir." />
              <Toast tone="warning" title="Sertifikat Anda telah terbit 🎉" description="Level Basic selesai — bagikan ke LinkedIn." />
              <Toast tone="danger" title="Pembayaran gagal" description="Periksa metode pembayaran lalu coba lagi." />
            </div>
          </div>
        </Section>

        {/* States */}
        <Section eyebrow="Kit" title="Status per layar">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Card className="flex flex-col gap-3">
              <Skeleton className="aspect-video w-full" />
              <Skeleton className="h-3 w-4/5" />
              <Skeleton className="h-3 w-2/4" />
            </Card>
            <EmptyState
              title="Tidak ada modul ditemukan"
              message="Coba ubah filter atau kata kunci pencarian Anda."
              action={<Button variant="neutral" size="sm">Atur ulang filter</Button>}
            />
            <ErrorState
              title="Gagal memuat konten"
              message="Periksa koneksi Anda lalu coba lagi."
              action={<Button size="sm">Coba lagi</Button>}
            />
          </div>
        </Section>
      </main>

      <Modal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        title="Buka modul ini"
        footer={
          <>
            <Button variant="neutral" fullWidth onClick={() => setModalOpen(false)}>
              Nanti saja
            </Button>
            <Button fullWidth onClick={() => setModalOpen(false)}>
              Lihat paket
            </Button>
          </>
        }
      >
        Modul ini termasuk paket Intermediate. Tingkatkan langganan untuk menonton dan mendapatkan
        sertifikat.
      </Modal>
    </>
  );
}
