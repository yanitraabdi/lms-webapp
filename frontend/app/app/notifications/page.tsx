"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { Button, Spinner, ErrorState, Switch } from "@/components/ui";
import {
  listNotifications,
  markNotificationRead,
  markAllNotificationsRead,
  getPreferences,
  updatePreferences,
  CATEGORY_LABELS,
  CATEGORY_ORDER,
  CHANNEL_LABELS,
  num,
  timeAgo,
  type Notification,
  type NotificationPref,
} from "@/lib/engagement";

export default function NotificationsPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login?next=/app/notifications");
  }, [status, router]);

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  return (
    <div className="min-h-screen bg-bg">
      <AppHeader />
      <div className="mx-auto max-w-3xl px-6 pb-16 pt-7">
        <h1 className="mb-6 text-[24px] font-extrabold tracking-tight">Notifikasi</h1>
        <NotificationList token={accessToken} />
        <Preferences token={accessToken} />
      </div>
    </div>
  );
}

function NotificationList({ token }: { token: string }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["notifications-page"], queryFn: () => listNotifications(token) });

  async function markOne(n: Notification) {
    if (n.read) return;
    await markNotificationRead(token, n.id).catch(() => {});
    qc.invalidateQueries({ queryKey: ["notifications-page"] });
    qc.invalidateQueries({ queryKey: ["notifications-bell"] });
  }
  async function markAll() {
    await markAllNotificationsRead(token).catch(() => {});
    qc.invalidateQueries({ queryKey: ["notifications-page"] });
    qc.invalidateQueries({ queryKey: ["notifications-bell"] });
  }

  return (
    <section className="mb-10">
      <div className="mb-3 flex items-center justify-between">
        <span className="text-xs font-bold uppercase tracking-wide text-ink-subtle">Terbaru</span>
        {num(q.data?.unreadCount ?? 0) > 0 && (
          <button type="button" onClick={markAll} className="text-[12.5px] font-bold text-primary hover:underline">
            Tandai semua dibaca
          </button>
        )}
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
        {q.isPending ? (
          <div className="flex min-h-[120px] items-center justify-center"><Spinner size={20} /></div>
        ) : q.isError ? (
          <div className="p-6"><ErrorState title="Gagal memuat notifikasi" action={<Button variant="neutral" size="sm" onClick={() => q.refetch()}>Muat ulang</Button>} /></div>
        ) : q.data.items.length === 0 ? (
          <p className="px-6 py-10 text-center text-sm text-ink-muted">Belum ada notifikasi.</p>
        ) : (
          q.data.items.map((n) => (
            <button
              key={n.id}
              type="button"
              onClick={() => markOne(n)}
              className={
                "flex w-full items-start gap-3 border-b border-surface-2 px-5 py-4 text-left last:border-0 hover:bg-surface-2 " +
                (n.read ? "" : "bg-primary-soft/40")
              }
            >
              <span className={"mt-1.5 h-2 w-2 shrink-0 rounded-full " + (n.read ? "bg-transparent" : "bg-primary")} />
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-bold text-ink">{n.title}</span>
                {n.body && <span className="mt-0.5 block text-[13px] leading-snug text-ink-muted">{n.body}</span>}
                <span className="mt-1 block text-[11.5px] text-ink-subtle">{timeAgo(n.createdAt)}</span>
              </span>
            </button>
          ))
        )}
      </div>
    </section>
  );
}

function Preferences({ token }: { token: string }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["notification-prefs"], queryFn: () => getPreferences(token) });
  const [draft, setDraft] = useState<NotificationPref[] | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (q.data) setDraft(q.data);
  }, [q.data]);

  const byKey = useMemo(() => {
    const m = new Map<string, boolean>();
    (draft ?? []).forEach((p) => m.set(`${p.category}|${p.channel}`, p.enabled));
    return m;
  }, [draft]);

  function toggle(category: string, channel: string) {
    setSaved(false);
    setDraft((d) =>
      (d ?? []).map((p) => (p.category === category && p.channel === channel ? { ...p, enabled: !p.enabled } : p)));
  }

  async function save() {
    if (!draft) return;
    setSaving(true);
    try {
      await updatePreferences(token, draft);
      qc.invalidateQueries({ queryKey: ["notification-prefs"] });
      setSaved(true);
    } finally {
      setSaving(false);
    }
  }

  const channels = ["InApp", "Email"];

  return (
    <section>
      <h2 className="mb-1 text-lg font-extrabold tracking-tight">Pengaturan notifikasi</h2>
      <p className="mb-4 text-[13px] text-ink-muted">Pilih bagaimana Anda ingin diberi tahu.</p>

      <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
        {q.isPending || !draft ? (
          <div className="flex min-h-[120px] items-center justify-center"><Spinner size={20} /></div>
        ) : (
          <>
            <div className="grid grid-cols-[1fr_auto_auto] items-center gap-x-6 border-b border-border bg-surface-2 px-5 py-2.5 text-[11px] font-bold uppercase tracking-wide text-ink-subtle">
              <span>Kategori</span>
              {channels.map((c) => <span key={c} className="w-[88px] text-center">{CHANNEL_LABELS[c]}</span>)}
            </div>
            {CATEGORY_ORDER.map((cat) => (
              <div key={cat} className="grid grid-cols-[1fr_auto_auto] items-center gap-x-6 border-b border-surface-2 px-5 py-3.5 last:border-0">
                <span className="text-sm font-semibold text-ink">{CATEGORY_LABELS[cat]}</span>
                {channels.map((ch) => (
                  <div key={ch} className="flex w-[88px] justify-center">
                    <Switch
                      checked={byKey.get(`${cat}|${ch}`) ?? false}
                      onCheckedChange={() => toggle(cat, ch)}
                      aria-label={`${CATEGORY_LABELS[cat]} — ${CHANNEL_LABELS[ch]}`}
                    />
                  </div>
                ))}
              </div>
            ))}
          </>
        )}
      </div>

      <div className="mt-4 flex items-center gap-3">
        <Button onClick={save} loading={saving} disabled={!draft}>Simpan pengaturan</Button>
        {saved && <span className="text-[13px] font-bold text-success">Tersimpan ✓</span>}
      </div>
    </section>
  );
}
