"use client";

import { useEffect, useId, useRef, useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { Upload } from "tus-js-client";
import { Button, Spinner } from "@/components/ui";
import { inputBlockCls } from "@/components/admin/fields";
import {
  listVideoLibrary, minutesLabel, num, renewVideoUpload, startVideoUpload,
  type UploadTicket, type VideoLibraryItem,
} from "@/lib/programs";

const FINAL = ["Finished", "Error", "UploadFailed"];
const POLL_CAP_MS = 30 * 60_000;

/**
 * Picks a session's video from the Bunny library, by title. Choosing one hands back its id AND its
 * length, so the admin never pastes a GUID or types a duration.
 *
 * Only "Finished" videos are pickable. A video still encoding has no playlist yet, so attaching it
 * gives learners a broken player; it is shown, with its progress, so the admin knows to wait.
 *
 * When there is no library to list (no API key, dev provider, Bunny unreachable) the server says
 * why, and that reason is shown instead. The form's manual id field stays usable either way.
 *
 * A new video can be uploaded from here, straight from the browser to Bunny (never through our
 * API). The list then polls until Bunny has encoded it; picking it is still the admin's click.
 */
export function VideoPicker({
  token, value, onPick, onBusyChange,
}: {
  token: string;
  value: string;
  onPick: (video: { id: string; lengthSeconds: number }) => void;
  /** True while an upload runs here, so the surrounding form can warn before closing. */
  onBusyChange?: (busy: boolean) => void;
}) {
  const [input, setInput] = useState("");
  const [search, setSearch] = useState("");
  const [pendingIds, setPendingIds] = useState<string[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  // Polling stops 30 minutes after the picker opened or the last upload finished, whichever is later.
  const pollFrom = useRef(0);
  useEffect(() => { pollFrom.current = Date.now(); }, []);

  const busyCb = useRef(onBusyChange);
  useEffect(() => { busyCb.current = onBusyChange; });
  const busy = activeId !== null;
  useEffect(() => {
    if (!busy) return;
    busyCb.current?.(true);
    return () => busyCb.current?.(false);
  }, [busy]);

  // The list is searched as the admin types; wait for a pause rather than calling Bunny per key.
  useEffect(() => {
    const t = setTimeout(() => setSearch(input.trim()), 400);
    return () => clearTimeout(t);
  }, [input]);

  const q = useQuery({
    queryKey: ["video-library", search],
    queryFn: () => listVideoLibrary(token, search),
    staleTime: 30_000,
    // Keep the last list on screen while a new search loads, so the uploader (rendered only with
    // data) stays mounted: unmounting it would abort a running upload.
    placeholderData: keepPreviousData,
    refetchInterval: (query) => {
      if (Date.now() - pollFrom.current > POLL_CAP_MS) return false;
      const items = query.state.data?.items ?? [];
      // The row being uploaded right now cannot change until its upload ends (then it is pending).
      // A "Created" row nobody here is uploading is an abandoned upload: it never changes either,
      // so it must not keep every open form polling Bunny.
      const encoding = items.some((v) =>
        v.id !== activeId &&
        !FINAL.includes(v.status) &&
        (v.status !== "Created" || pendingIds.includes(v.id)));
      const waiting = pendingIds.some((id) => !items.some((v) => v.id === id && v.status === "Finished"));
      return encoding || waiting ? 10_000 : false;
    },
  });

  function onUploaded(videoId: string, title: string) {
    setPendingIds((ids) => [...ids, videoId]);
    pollFrom.current = Date.now();
    // The list is paged and title-ordered: searching for the title keeps the new video on page one.
    setInput(title);
    setSearch(title);
  }

  // Once the library has been listed, the uploader stays mounted in the same place whatever the
  // list does next (a new search, a transient Bunny failure): unmounting it would abort an upload.
  const [librarySeen, setLibrarySeen] = useState(false);
  if (!librarySeen && q.data && !q.data.unavailable) setLibrarySeen(true);

  return (
    <div className="flex flex-col gap-2">
      {librarySeen && (
        <VideoUploader token={token} onActiveChange={setActiveId} onUploaded={onUploaded} />
      )}
      {q.data?.unavailable ? (
        <p className="rounded-base bg-surface-2 px-3 py-2 text-[12.5px] leading-snug text-ink-muted">
          {q.data.unavailable}
        </p>
      ) : (
        <>
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
                  <VideoRow
                    key={v.id}
                    video={v}
                    selected={v.id === value}
                    uploading={v.id === activeId || pendingIds.includes(v.id)}
                    onPick={onPick}
                  />
                ))}
              </ul>
            )}
          </div>
        </>
      )}
    </div>
  );
}

function VideoRow({
  video, selected, uploading, onPick,
}: {
  video: VideoLibraryItem;
  selected: boolean;
  /** This row is being (or was just) uploaded in this tab, so "Created" is not an abandoned upload. */
  uploading: boolean;
  onPick: (video: { id: string; lengthSeconds: number }) => void;
}) {
  const ready = video.status === "Finished";
  const note = ready
    ? minutesLabel(video.lengthSeconds)
    : video.status === "Error" || video.status === "UploadFailed"
      ? "Gagal diproses"
      : video.status === "Created" && !uploading
        ? "Unggahan tidak selesai"
        : `Masih diproses (${num(video.encodeProgress)}%)`;

  return (
    <li>
      <button
        type="button"
        disabled={!ready}
        onClick={() => onPick({ id: video.id, lengthSeconds: num(video.lengthSeconds) })}
        className={
          "flex w-full items-center justify-between gap-3 border-b border-border px-3 py-2 text-left text-[13px] outline-none last:border-b-0 focus-visible:ring-[3px] focus-visible:ring-inset focus-visible:ring-primary-soft " +
          (selected ? "bg-primary-soft/60 font-semibold" : ready ? "hover:bg-surface-2" : "cursor-not-allowed opacity-50")
        }
      >
        <span className="min-w-0 truncate">{video.title || video.id}</span>
        <span className="shrink-0 text-[11.5px] text-ink-subtle">{selected ? "Terpilih ✓" : note}</span>
      </button>
    </li>
  );
}

// One endpoint for both the resume probe and the upload: tus's fingerprint includes it, so any
// difference would silently stop resuming.
const TUS_ENDPOINT = "https://video.bunnycdn.com/tusupload";
type PreviousUpload = Awaited<ReturnType<Upload["findPreviousUploads"]>>[number];

/** The unfinished upload of this file we can resume (tagged with our videoId), if any. */
async function findResumable(file: File): Promise<PreviousUpload | null> {
  try {
    const all = await new Upload(file, { endpoint: TUS_ENDPOINT }).findPreviousUploads();
    return all.find((p) => p.metadata?.videoId) ?? null;
  } catch {
    return null; // storage unavailable: nothing to resume
  }
}
const mb = (n: number) => `${Math.round(n / 1048576)} MB`;

const headersOf = (t: UploadTicket) => ({
  AuthorizationSignature: t.signature,
  AuthorizationExpire: String(t.expiresAt),
  VideoId: t.videoId,
  LibraryId: t.libraryId,
});

/**
 * Uploads one file from the browser to Bunny over TUS. The signed ticket comes from our API; the
 * bytes never pass through it. An interrupted upload resumes into the SAME Bunny video when the
 * same file is chosen again (tus-js-client remembers the upload URL and our videoId metadata in
 * localStorage; that is not a credential, Bunny still requires the signed headers).
 */
function VideoUploader({
  token, onActiveChange, onUploaded,
}: {
  token: string;
  onActiveChange: (videoId: string | null) => void;
  onUploaded: (videoId: string, title: string) => void;
}) {
  const [phase, setPhase] = useState<"idle" | "preparing" | "uploading">("idle");
  const [file, setFile] = useState<File | null>(null);
  const [previous, setPrevious] = useState<PreviousUpload | null>(null);
  const hintId = useId();
  const [title, setTitle] = useState("");
  const [starting, setStarting] = useState(false);
  const [progress, setProgress] = useState({ sent: 0, total: 0 });
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const uploadRef = useRef<Upload | null>(null);
  const renewed = useRef(false);
  const alive = useRef(true);

  // Closing the form (unmount) stops the upload rather than letting it run on invisibly.
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
      void uploadRef.current?.abort();
    };
  }, []);

  useEffect(() => {
    if (phase !== "uploading") return;
    const warn = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = ""; // Safari and older browsers only prompt when this is set
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [phase]);

  function finish(message: string) {
    uploadRef.current = null;
    onActiveChange(null);
    setPhase("idle");
    setFile(null);
    setStatus(message);
  }

  async function choose(f: File | undefined) {
    if (!f) return;
    // A resume continues the SAME Bunny video, which keeps its original title; it cannot be renamed.
    const prev = await findResumable(f);
    if (!alive.current) return;
    setPrevious(prev);
    setFile(f);
    setTitle(prev?.metadata.title ?? f.name.replace(/\.[^.]+$/, ""));
    setStatus(null);
    setError(null);
    setPhase("preparing");
  }

  async function start() {
    if (!file) return;
    const name = title.trim();
    setStarting(true);
    setError(null);
    renewed.current = false;
    try {
      // Resume first, create only when there is nothing to resume: a new video's signature does
      // not fit an old video's TUS URL, and every retry would leave an empty video in the library.
      const ticket = previous
        ? await renewVideoUpload(token, previous.metadata.videoId)
        : await startVideoUpload(token, name);
      if (!alive.current) return;

      const upload: Upload = new Upload(file, {
        endpoint: TUS_ENDPOINT,
        chunkSize: 50 * 1024 * 1024,
        retryDelays: [0, 3000, 10000, 30000],
        // A finished upload must not be "resumed" later: choosing the same file again is a new video.
        removeFingerprintOnSuccess: true,
        headers: headersOf(ticket),
        metadata: { filetype: file.type, title: name, videoId: ticket.videoId },
        onProgress: (sent, total) => setProgress({ sent, total }),
        // Progress since the last renewal: a later expiry may be renewed again. Without progress a
        // second 401/403 in a row still ends the upload instead of looping.
        onChunkComplete: () => { renewed.current = false; },
        onSuccess: () => {
          finish(`“${name}” terunggah. Video bisa dipilih setelah Bunny selesai memprosesnya.`);
          onUploaded(ticket.videoId, name);
        },
        onError: async (err) => {
          // An upload that outlived its ticket: renew once and resume.
          const code = (err as { originalResponse?: { getStatus(): number } | null }).originalResponse?.getStatus();
          if ((code === 401 || code === 403) && !renewed.current) {
            renewed.current = true;
            try {
              const fresh = await renewVideoUpload(token, ticket.videoId);
              if (!alive.current || uploadRef.current !== upload) return;
              upload.options.headers = headersOf(fresh);
              upload.start();
              return;
            } catch (e) {
              setError(e instanceof Error ? e.message : null);
            }
          }
          if (uploadRef.current === upload) finish("Unggahan terputus. Pilih file yang sama untuk melanjutkan.");
        },
      });
      uploadRef.current = upload;
      if (previous) upload.resumeFromPreviousUpload(previous);
      setProgress({ sent: 0, total: file.size });
      onActiveChange(ticket.videoId);
      setPhase("uploading");
      upload.start();
    } catch (e) {
      if (alive.current) setError(e instanceof Error ? e.message : "Operasi gagal.");
    } finally {
      if (alive.current) setStarting(false);
    }
  }

  function stop() {
    const upload = uploadRef.current;
    uploadRef.current = null;
    void upload?.abort();
    onActiveChange(null);
    setPhase("idle");
    setFile(null);
  }

  const pct = progress.total ? Math.floor((progress.sent / progress.total) * 100) : 0;
  const titleOk = title.trim().length > 0 && title.trim().length <= 200;

  return (
    <div className="flex flex-col gap-2 rounded-base border border-border px-3 py-2">
      <input
        ref={fileInput}
        type="file"
        accept="video/*"
        hidden
        onChange={(e) => { void choose(e.target.files?.[0]); e.target.value = ""; }}
      />

      {phase === "idle" && (
        <div>
          <Button variant="neutral" size="sm" onClick={() => fileInput.current?.click()}>
            Unggah video baru
          </Button>
        </div>
      )}

      {phase === "preparing" && file && (
        <div className="flex flex-col gap-2">
          <span className="truncate text-[12.5px] text-ink-muted">{file.name}</span>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            maxLength={200}
            readOnly={previous !== null}
            aria-label="Judul video"
            aria-describedby={previous ? hintId : undefined}
            className={inputBlockCls}
          />
          {previous && (
            <span id={hintId} className="text-[11.5px] text-ink-subtle">
              Melanjutkan unggahan sebelumnya.
            </span>
          )}
          <div className="flex gap-2">
            <Button size="sm" disabled={!titleOk} loading={starting} onClick={start}>Mulai unggah</Button>
            <Button variant="neutral" size="sm" disabled={starting} onClick={() => { setPhase("idle"); setFile(null); }}>
              Batal
            </Button>
          </div>
        </div>
      )}

      {phase === "uploading" && (
        <div className="flex flex-col gap-2">
          <progress
            max={progress.total || 1}
            value={progress.sent}
            aria-label="Progres unggah video"
            className="h-2 w-full"
          />
          <div className="flex items-center justify-between gap-3">
            <span className="text-[12.5px] text-ink-muted">
              Mengunggah {pct}% · {mb(progress.sent)} dari {mb(progress.total)}
            </span>
            <Button variant="neutral" size="sm" onClick={stop}>Hentikan unggahan</Button>
          </div>
        </div>
      )}

      <div aria-live="polite" className="text-[12.5px] leading-snug">
        {status && <p className="text-ink-muted">{status}</p>}
        {error && <p className="text-danger">{error}</p>}
      </div>
    </div>
  );
}
