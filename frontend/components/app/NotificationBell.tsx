"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { BellIcon } from "@/components/ui";
import {
  listNotifications,
  markNotificationRead,
  markAllNotificationsRead,
  num,
  timeAgo,
  type Notification,
} from "@/lib/engagement";

export function NotificationBell() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const q = useQuery({
    queryKey: ["notifications-bell"],
    queryFn: () => listNotifications(token!),
    enabled: !!token,
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
  });

  useEffect(() => {
    if (!open) return;
    function onClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onClick);
    return () => document.removeEventListener("mousedown", onClick);
  }, [open]);

  if (!token) return null;

  const unread = num(q.data?.unreadCount ?? 0);
  const items = q.data?.items ?? [];

  async function onItemClick(n: Notification) {
    if (!n.read && token) {
      await markNotificationRead(token, n.id).catch(() => {});
      qc.invalidateQueries({ queryKey: ["notifications-bell"] });
      qc.invalidateQueries({ queryKey: ["notifications-page"] });
    }
  }

  async function onMarkAll() {
    if (!token) return;
    await markAllNotificationsRead(token).catch(() => {});
    qc.invalidateQueries({ queryKey: ["notifications-bell"] });
    qc.invalidateQueries({ queryKey: ["notifications-page"] });
  }

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        aria-label="Notifikasi"
        onClick={() => setOpen((o) => !o)}
        className="relative inline-flex h-9 w-9 items-center justify-center rounded-full text-ink-muted hover:bg-surface-2 hover:text-ink"
      >
        <BellIcon size={19} />
        {unread > 0 && (
          <span className="absolute right-0.5 top-0.5 inline-flex min-w-[16px] items-center justify-center rounded-full bg-danger px-1 text-[10px] font-extrabold leading-[15px] text-white">
            {unread > 9 ? "9+" : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-11 z-50 w-[340px] overflow-hidden rounded-lg border border-border bg-surface shadow-lg">
          <div className="flex items-center justify-between border-b border-border px-4 py-3">
            <span className="text-sm font-extrabold">Notifikasi</span>
            {unread > 0 && (
              <button type="button" onClick={onMarkAll} className="text-[12px] font-bold text-primary hover:underline">
                Tandai semua dibaca
              </button>
            )}
          </div>

          <div className="max-h-[380px] overflow-y-auto">
            {items.length === 0 ? (
              <p className="px-4 py-8 text-center text-[13px] text-ink-muted">Belum ada notifikasi.</p>
            ) : (
              items.slice(0, 12).map((n) => (
                <button
                  key={n.id}
                  type="button"
                  onClick={() => onItemClick(n)}
                  className={
                    "flex w-full items-start gap-2.5 border-b border-surface-2 px-4 py-3 text-left last:border-0 hover:bg-surface-2 " +
                    (n.read ? "" : "bg-primary-soft/40")
                  }
                >
                  <span className={"mt-1.5 h-2 w-2 shrink-0 rounded-full " + (n.read ? "bg-transparent" : "bg-primary")} />
                  <span className="min-w-0 flex-1">
                    <span className="block text-[13px] font-bold text-ink">{n.title}</span>
                    {n.body && <span className="mt-0.5 block text-[12.5px] leading-snug text-ink-muted">{n.body}</span>}
                    <span className="mt-1 block text-[11px] text-ink-subtle">{timeAgo(n.createdAt)}</span>
                  </span>
                </button>
              ))
            )}
          </div>

          <Link
            href="/app/notifications"
            onClick={() => setOpen(false)}
            className="block border-t border-border px-4 py-2.5 text-center text-[12.5px] font-bold text-primary hover:bg-surface-2"
          >
            Lihat semua & pengaturan
          </Link>
        </div>
      )}
    </div>
  );
}
