"use client";

import { useEffect, useRef } from "react";

interface Props {
  src: string;
  captionsSrc?: string | null;
  resumeSeconds?: number;
  /** Throttled progress callback (every ~5s + on pause/hide/ended). */
  onProgress: (positionSeconds: number, percent: number) => void;
}

/// HLS via hls.js (Chrome/Firefox) or native (Safari); falls back to direct src for MP4.
export function VideoPlayer({ src, captionsSrc, resumeSeconds = 0, onProgress }: Props) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const lastSave = useRef(0);
  const resumed = useRef(false);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;
    const isHls = src.includes(".m3u8");
    const native = video.canPlayType("application/vnd.apple.mpegurl");

    if (isHls && !native) {
      let cancelled = false;
      let hls: { destroy: () => void } | undefined;
      import("hls.js").then(({ default: Hls }) => {
        if (cancelled) return;
        if (Hls.isSupported()) {
          const h = new Hls();
          h.loadSource(src);
          h.attachMedia(video);
          hls = h;
        } else {
          video.src = src;
        }
      });
      return () => {
        cancelled = true;
        hls?.destroy();
      };
    }

    video.src = src;
  }, [src]);

  function report(force: boolean) {
    const v = videoRef.current;
    if (!v || !v.duration || Number.isNaN(v.duration)) return;
    const now = Date.now();
    if (!force && now - lastSave.current < 5000) return;
    lastSave.current = now;
    onProgress(v.currentTime, Math.min(100, (v.currentTime / v.duration) * 100));
  }

  // Persist promptly when the tab is hidden.
  useEffect(() => {
    const onHide = () => {
      if (document.visibilityState === "hidden") report(true);
    };
    document.addEventListener("visibilitychange", onHide);
    return () => document.removeEventListener("visibilitychange", onHide);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <video
      ref={videoRef}
      controls
      playsInline
      crossOrigin="anonymous"
      className="aspect-video w-full bg-black"
      onLoadedMetadata={(e) => {
        const v = e.currentTarget;
        if (!resumed.current && resumeSeconds > 0 && resumeSeconds < v.duration - 2) v.currentTime = resumeSeconds;
        resumed.current = true;
      }}
      onTimeUpdate={() => report(false)}
      onPause={() => report(true)}
      onEnded={() => report(true)}
    >
      {captionsSrc && <track kind="subtitles" src={captionsSrc} srcLang="id" label="Bahasa Indonesia" default />}
    </video>
  );
}
