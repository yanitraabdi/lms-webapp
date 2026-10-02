"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Spinner } from "@/components/ui";
import { inputBlockCls } from "@/components/admin/fields";
import { listVideoLibrary, minutesLabel, num, type VideoLibraryItem } from "@/lib/programs";

/**
 * Picks a session's video from the Bunny library, by title. Choosing one hands back its id AND its
 * length, so the admin never pastes a GUID or types a duration.
 *
 * Only "Finished" videos are pickable. A video still encoding has no playlist yet, so attaching it
 * gives learners a broken player; it is shown, with its progress, so the admin knows to wait.
 *
 * When there is no library to list (no API key, dev provider, Bunny unreachable) the server says
 * why, and that reason is shown instead. The form's manual id field stays usable either way.
 */
export function VideoPicker({
  token, value, onPick,
}: {
  token: string;
  value: string;
  onPick: (video: { id: string; lengthSeconds: number }) => void;
}) {
  const [input, setInput] = useState("");
  const [search, setSearch] = useState("");

  // The list is searched as the admin types; wait for a pause rather than calling Bunny per key.
  useEffect(() => {
    const t = setTimeout(() => setSearch(input.trim()), 400);
    return () => clearTimeout(t);
  }, [input]);

  const q = useQuery({
    queryKey: ["video-library", search],
    queryFn: () => listVideoLibrary(token, search),
    staleTime: 30_000,
  });

  if (q.data?.unavailable) {
    return (
      <p className="rounded-base bg-surface-2 px-3 py-2 text-[12.5px] leading-snug text-ink-muted">
        {q.data.unavailable}
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      <input
        value={input}
        onChange={(e) => setInput(e.target.value)}
        placeholder="Cari judul video…"
        aria-label="Cari video di pustaka"
        className={inputBlockCls}
      />
      <div className="max-h-56 overflow-y-auto rounded-base border border-border">
        {q.isPending ? (
          <div className="flex min-h-[80px] items-center justify-center"><Spinner size={18} /></div>
        ) : q.isError ? (
          <p className="px-3 py-3 text-[12.5px] text-danger">Daftar video gagal dimuat.</p>
        ) : (q.data?.items.length ?? 0) === 0 ? (
          <p className="px-3 py-3 text-[12.5px] text-ink-muted">Tidak ada video yang cocok.</p>
        ) : (
          <ul>
            {q.data!.items.map((v) => (
              <VideoRow key={v.id} video={v} selected={v.id === value} onPick={onPick} />
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function VideoRow({
  video, selected, onPick,
}: {
  video: VideoLibraryItem;
  selected: boolean;
  onPick: (video: { id: string; lengthSeconds: number }) => void;
}) {
  const ready = video.status === "Finished";
  const note = ready
    ? minutesLabel(video.lengthSeconds)
    : video.status === "Error" || video.status === "UploadFailed"
      ? "Gagal diproses"
      : `Masih diproses (${num(video.encodeProgress)}%)`;

  return (
    <li>
      <button
        type="button"
        disabled={!ready}
        onClick={() => onPick({ id: video.id, lengthSeconds: num(video.lengthSeconds) })}
        className={
          "flex w-full items-center justify-between gap-3 border-b border-border px-3 py-2 text-left text-[13px] last:border-b-0 " +
          (selected ? "bg-primary-soft/60 font-semibold" : ready ? "hover:bg-surface-2" : "cursor-not-allowed opacity-50")
        }
      >
        <span className="min-w-0 truncate">{video.title || video.id}</span>
        <span className="shrink-0 text-[11.5px] text-ink-subtle">{selected ? "Terpilih ✓" : note}</span>
      </button>
    </li>
  );
}
