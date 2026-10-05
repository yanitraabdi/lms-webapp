"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui";
import { clock } from "@/lib/sessions";

/**
 * The Listening section's single recording: started once, played through without pausing, never
 * replayed.
 *
 * Position comes from the SERVER, never from this element: playback always sits at
 * `serverNow - startedAt`. The first start therefore begins at zero, a reload resumes where the
 * recording has reached, and pausing — media keys, headphones, the OS — only snaps it back to the
 * live position. Buffering on a slow connection does the same when it recovers, as a real sitting
 * would: the recording does not wait.
 *
 * No native controls, so there is no seek bar or replay button to reach for. That is deterrence
 * rather than enforcement — the signed URL serves the whole file to anyone who edits the page — and
 * it must never be described to learners as tamper-proof (GR-14).
 */
export function LiveAudioPlayer({
  start, startedAt, autoBegin = false, endedLabel = "Audio bagian ini sudah selesai diputar.",
  intro = "Satu rekaman untuk seluruh bagian ini. Audio diputar satu kali tanpa jeda dan tidak dapat diulang — pastikan suara perangkat Anda aktif.",
}: {
  start: () => Promise<{ url: string; startedAt: string; serverNow: string }>;
  startedAt: string | null;
  /** Begin on mount — for a mount that a learner's tap just caused (a replay). */
  autoBegin?: boolean;
  endedLabel?: string;
  intro?: string;
}) {
  type Phase = "idle" | "loading" | "blocked" | "playing" | "ended" | "error";
  const el = useRef<HTMLAudioElement | null>(null);
  const serverStart = useRef<number | null>(null);
  const skew = useRef(0);                              // server clock minus this device's clock, ms
  const [phase, setPhase] = useState<Phase>("idle");
  const [err, setErr] = useState<string | null>(null);
  const [pos, setPos] = useState({ at: 0, duration: 0 });
  const gone = useRef(false);

  // Leaving the section unmounts this player. A browser pauses a media element as it is removed,
  // and without this guard that pause would reach the resume-on-pause handler below and restart
  // the Listening recording, unseen, underneath the Structure section.
  useEffect(() => () => {
    gone.current = true;
    el.current?.pause();
  }, []);

  // Mount-only on purpose: begin() once. If the browser refuses sound, "blocked" asks for a tap.
  useEffect(() => {
    if (autoBegin) void begin();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /** Where the recording IS right now, by the server's clock. */
  const live = () =>
    serverStart.current == null ? 0 : Math.max(0, (Date.now() + skew.current - serverStart.current) / 1000);

  async function resumeAtLive() {
    const audio = el.current;
    if (gone.current || !audio) return;
    const at = live();
    if (Number.isFinite(audio.duration) && at >= audio.duration) { setPhase("ended"); return; }
    audio.currentTime = at;
    try {
      await audio.play();
      setPhase("playing");
    } catch (e) {
      // Browsers can refuse sound that did not follow a tap closely enough (Safari especially,
      // after the network round trip). One more tap, and it plays from the live position.
      if (e instanceof DOMException && e.name === "NotAllowedError") setPhase("blocked");
      else { setPhase("error"); setErr("Audio tidak dapat diputar. Coba lagi."); }
    }
  }

  async function begin() {
    setPhase("loading"); setErr(null);
    try {
      const r = await start();
      skew.current = Date.parse(r.serverNow) - Date.now();
      serverStart.current = Date.parse(r.startedAt);
      const audio = el.current!;
      if (audio.src !== r.url) {
        audio.src = r.url;
        await new Promise<void>((resolve, reject) => {
          audio.addEventListener("loadedmetadata", () => resolve(), { once: true });
          audio.addEventListener("error", () => reject(new Error("load")), { once: true });
        });
      }
      await resumeAtLive();
    } catch (e) {
      setPhase("error");
      setErr(e instanceof Error && e.message !== "load" ? e.message : "Audio tidak dapat dimuat. Coba lagi.");
    }
  }

  const resumed = startedAt != null;

  return (
    <div className="mt-3 rounded-base border border-border bg-surface-2 px-4 py-3">
      <audio
        ref={el}
        preload="none"
        onTimeUpdate={(e) => setPos({ at: e.currentTarget.currentTime, duration: e.currentTarget.duration || 0 })}
        onPause={(e) => {
          // Nonstop: a pause from anywhere outside the page puts it straight back, at the live
          // position rather than where it stopped.
          if (phase === "playing" && !e.currentTarget.ended) void resumeAtLive();
        }}
        onPlaying={(e) => {
          // After a stall, jump to where the recording has reached if it has fallen behind.
          if (Math.abs(e.currentTarget.currentTime - live()) > 2) e.currentTarget.currentTime = live();
        }}
        onEnded={() => setPhase("ended")}
        onError={() => {
          // A dropped connection mid-recording. "Lanjutkan" mints a fresh URL and rejoins at the
          // live position — what was missed stays missed, as it would in the room.
          if (phase === "playing") { setPhase("error"); setErr("Koneksi audio terputus. Lanjutkan untuk bergabung kembali."); }
        }}
      />

      {phase === "playing" ? (
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center justify-between text-[12.5px] font-bold">
            <span className="text-primary">● Audio sedang diputar</span>
            <span className="tabular-nums text-ink-muted">
              {clock(Math.floor(pos.at))} / {pos.duration ? clock(Math.floor(pos.duration)) : "--:--"}
            </span>
          </div>
          <div className="h-1.5 overflow-hidden rounded-full bg-border" aria-hidden>
            <div
              className="h-full bg-primary transition-[width] duration-500"
              style={{ width: `${pos.duration ? Math.min(100, (pos.at / pos.duration) * 100) : 0}%` }}
            />
          </div>
        </div>
      ) : phase === "ended" ? (
        <p className="text-[13px] font-bold text-ink-muted">{endedLabel}</p>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="max-w-md text-[12.5px] leading-snug text-ink-muted">
            {phase === "blocked"
              ? "Peramban meminta satu ketukan lagi untuk memutar suara."
              : resumed
                ? "Audio sudah berjalan dan akan dilanjutkan dari posisinya saat ini, bukan dari awal."
                : intro}
          </p>
          <Button
            size="sm"
            onClick={phase === "blocked" ? () => void resumeAtLive() : () => void begin()}
            loading={phase === "loading"}
          >
            {phase === "blocked" ? "▶ Putar" : resumed || phase === "error" ? "▶ Lanjutkan audio" : "▶ Mulai audio"}
          </Button>
        </div>
      )}
      {err && <p className="mt-1.5 text-[12px] font-semibold text-danger">{err}</p>}
    </div>
  );
}
