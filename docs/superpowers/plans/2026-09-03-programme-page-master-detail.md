# Programme Page 30/70 Master-Detail Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a learner move between sessions without leaving the programme page — session list on the left, the session itself on the right.

**Architecture:** `SessionView` already exists inside the session route as a self-contained component that fetches its own context. Task 1 moves it to `components/learn/` and gives it two props; Task 2 renders it in the right pane of a two-column programme page with the selection in the URL. Entirely frontend — `StudentProgramDto` already returns everything needed.

**Tech Stack:** Next.js App Router (client components, `useSearchParams`), TanStack Query, Tailwind.

**Source spec:** `docs/superpowers/specs/2026-09-03-programme-page-master-detail-design.md` — read it before Task 1.

## Global Constraints

- **This is a layout change.** No change to playback, watch progress, gating tests, live details, scoring or the linear lock. If you find yourself editing a backend file or a test that guards those, stop — something has gone wrong.
- **No backend work at all.** `StudentProgramDto` already returns `NextSessionId` and per-session lock state. Do not add an endpoint or a DTO field.
- **The final assessment never renders inline.** A `FinalAssessment` session navigates to `/app/assessment/{sessionId}`.
- **`/app/session/{id}` must keep working.** It is linked from the dashboard, from the assessment page's back-link, and from learners' bookmarks.
- **The server is the gate (GR-1).** The client may hide a locked session, but must never be relied on to enforce it.
- The frontend has **no test runner**. Verification is `npx tsc --noEmit && npm run build` plus opening the pages.
- Lint treats unused imports as errors — remove an import when you remove its last use.
- All user-facing strings are Bahasa Indonesia.

---

## File Structure

**Create:**

| File | Responsibility |
|---|---|
| `frontend/components/learn/SessionView.tsx` | The session body: video / live / final / gating test. Used by both the standalone route and the programme page's right pane. |

**Modify:**

| File | Change |
|---|---|
| `frontend/app/app/session/[id]/page.tsx` | Becomes a thin route that renders `<SessionView>`. |
| `frontend/app/app/program/[id]/page.tsx` | Two-column layout, URL-synced selection, right pane. |

---

## Task 1: Extract `SessionView`

A pure move plus two props. No visual change — after this task both pages must look exactly as they do now. Doing it separately means a reviewer can tell a refactor from a feature.

**Files:**
- Create: `frontend/components/learn/SessionView.tsx`
- Modify: `frontend/app/app/session/[id]/page.tsx`

**Interfaces:**
- Produces: `<SessionView token={string} sessionId={string} embedded?={boolean} onSessionChanged?={() => void} />`

- [ ] **Step 1: Create the component**

Create `frontend/components/learn/SessionView.tsx` with the entire content below. This is `SessionView`, `VideoSection`, `LiveSection` and `FinalSection` moved verbatim out of the route, plus the two new props.

```tsx
"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Badge, Button, Spinner, ErrorState, ChevronRightIcon } from "@/components/ui";
import { VideoPlayer } from "@/components/learn/VideoPlayer";
import { GatingTest } from "@/components/learn/GatingTest";
import {
  getSessionContext, getPlayback, saveSessionProgress, num,
  type SessionContext,
} from "@/lib/sessions";
import { fmtDateTime, minutesLabel } from "@/lib/programs";

/**
 * One session, in full: the video and its progress, or live details, or the final-exam entry —
 * plus the gating test where there is one.
 *
 * Rendered in two places. `/app/session/{id}` shows it as a standalone page; the programme page
 * shows it in the right pane of a master-detail layout. Same component either way, so the two can
 * never drift.
 */
export function SessionView({
  token,
  sessionId,
  embedded = false,
  onSessionChanged,
}: {
  token: string;
  sessionId: string;
  /** Drop the standalone page's own header and page chrome — the host already provides them. */
  embedded?: boolean;
  /**
   * Fired when something happens that changes what the SESSION LIST should show: a passed gating
   * test, or watch progress crossing the completion threshold. Standalone, the learner navigates
   * back and the list refetches on its own; embedded, nothing navigates, so without this the list
   * keeps showing the next session as locked after the learner has just unlocked it.
   */
  onSessionChanged?: () => void;
}) {
  const qc = useQueryClient();
  const router = useRouter();

  const ctx = useQuery({
    queryKey: ["session-context", sessionId],
    queryFn: () => getSessionContext(token, sessionId),
    retry: false,
  });

  if (ctx.isPending) {
    return (
      <div className={"flex items-center justify-center " + (embedded ? "min-h-[320px]" : "min-h-screen bg-bg")}>
        <Spinner size={24} />
      </div>
    );
  }
  if (ctx.isError) {
    return (
      <div className={embedded ? "py-10" : "mx-auto max-w-md px-6 py-20"}>
        <ErrorState
          title="Sesi terkunci"
          message="Selesaikan sesi sebelumnya terlebih dahulu, atau pastikan pendaftaran Anda aktif."
          action={
            embedded ? undefined : (
              <Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">
                Kembali ke dasbor
              </Link>
            )
          }
        />
      </div>
    );
  }

  const s = ctx.data;
  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["session-context", sessionId] });
    onSessionChanged?.();
  };

  const body = (
    <>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h1 className="text-[23px] font-extrabold tracking-tight">{s.title}</h1>
          {s.description && <p className="text-[14px] text-ink-muted">{s.description}</p>}
        </div>
        {s.progress.completed && <Badge status="completed" className="px-2.5 py-0.5" />}
      </div>

      {s.type === "Video" ? (
        <VideoSection token={token} session={s} onProgressChanged={refresh} />
      ) : s.type === "Live" ? (
        <LiveSection session={s} />
      ) : (
        <FinalSection sessionId={sessionId} completed={s.progress.completed} />
      )}

      {s.type === "Video" && (
        <GatingTest
          token={token}
          sessionId={sessionId}
          watchThresholdMet={s.progress.watchThresholdMet}
          onPassed={refresh}
        />
      )}

      {/* Embedded, the list is right there — a "next session" button would be a second way to do
          the same thing, and the one that goes stale. The sentence still earns its place. */}
      {s.nextSessionId && (
        <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-surface px-5 py-4">
          <span className="text-[13.5px] text-ink-muted">
            {s.nextSessionUnlocked ? "Sesi berikutnya sudah terbuka." : "Selesaikan sesi ini untuk membuka sesi berikutnya."}
          </span>
          {!embedded && (
            <Button
              size="sm"
              disabled={!s.nextSessionUnlocked}
              onClick={() => router.push(`/app/session/${s.nextSessionId}`)}
            >
              Sesi berikutnya →
            </Button>
          )}
        </div>
      )}
    </>
  );

  if (embedded) return <div className="flex flex-col gap-5">{body}</div>;

  return (
    <div className="min-h-screen bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex h-[58px] max-w-4xl items-center justify-between gap-3 px-6">
          <Link
            href={`/app/program/${s.programId}`}
            className="inline-flex items-center gap-1.5 text-[13px] font-bold text-ink-muted hover:text-ink"
          >
            <ChevronRightIcon size={16} className="rotate-180" /> {s.programName}
          </Link>
          <span className="text-[12.5px] text-ink-subtle">Sesi {num(s.orderIndex)}</span>
        </div>
      </header>
      <main className="mx-auto flex max-w-4xl flex-col gap-5 px-6 py-6">{body}</main>
    </div>
  );
}

function VideoSection({
  token, session, onProgressChanged,
}: { token: string; session: SessionContext; onProgressChanged: () => void }) {
  const ticket = useQuery({
    queryKey: ["session-playback", session.id],
    queryFn: () => getPlayback(token, session.id),
  });

  const [pct, setPct] = useState(num(session.progress.percentComplete));
  const saving = useRef(false);
  const thresholdMet = useRef(session.progress.watchThresholdMet);

  async function onProgress(position: number, percent: number) {
    setPct((p) => Math.max(p, percent));
    if (saving.current) return;
    saving.current = true;
    try {
      const saved = await saveSessionProgress(token, session.id, position, percent);
      // Refresh once the threshold is first crossed so the gating test unlocks.
      if (saved.watchThresholdMet && !thresholdMet.current) {
        thresholdMet.current = true;
        onProgressChanged();
      }
    } catch {
      /* transient — the next tick retries */
    } finally {
      saving.current = false;
    }
  }

  if (ticket.isPending) {
    return <div className="flex min-h-[220px] items-center justify-center rounded-lg border border-border bg-surface"><Spinner size={22} /></div>;
  }
  if (ticket.isError) {
    return <ErrorState title="Video tidak dapat diputar" message="Coba muat ulang halaman." />;
  }

  const display = Math.round(Math.max(pct, session.progress.completed ? 100 : 0));

  return (
    <>
      <div className="overflow-hidden rounded-lg shadow-sm">
        <VideoPlayer
          src={ticket.data.url}
          captionsSrc={ticket.data.captionsUrl}
          resumeSeconds={num(session.progress.resumePositionSeconds)}
          onProgress={onProgress}
        />
      </div>
      <div className="flex flex-col gap-2">
        <div className="flex items-center justify-between text-[12.5px]">
          <span className="text-ink-muted">
            {session.durationSeconds != null && minutesLabel(session.durationSeconds)}
          </span>
          <span className="font-bold text-primary">{display}%</span>
        </div>
        <div className="h-[7px] overflow-hidden rounded-full bg-surface-2">
          <div
            className={"h-full rounded-full " + (session.progress.completed ? "bg-success" : "bg-primary")}
            style={{ width: `${display}%` }}
          />
        </div>
        <span className="text-[11.5px] text-ink-subtle">
          Tes sesi terbuka setelah Anda menonton ~90% video.
        </span>
      </div>
    </>
  );
}

function LiveSection({ session }: { session: SessionContext }) {
  return (
    <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <h3 className="text-base font-extrabold">Sesi live</h3>
      {session.scheduledAt && (
        <p className="mt-1 text-[13.5px] text-ink-muted">Jadwal: {fmtDateTime(session.scheduledAt)}</p>
      )}
      {session.location && <p className="mt-1 text-[13.5px] text-ink-muted">Lokasi: {session.location}</p>}
      {session.joinUrl && (
        <a
          href={session.joinUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-3 inline-block text-[13.5px] font-bold text-primary hover:underline"
        >
          Buka tautan sesi live →
        </a>
      )}
      <p className="mt-4 text-[12.5px] leading-relaxed text-ink-subtle">
        Kehadiran ditandai oleh admin setelah sesi berlangsung. Sesi berikutnya terbuka setelah
        kehadiran Anda dicatat.
      </p>
    </div>
  );
}

function FinalSection({ sessionId, completed }: { sessionId: string; completed: boolean }) {
  return (
    <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <h3 className="text-base font-extrabold">Tes akhir</h3>
      <p className="mt-1 text-[13.5px] leading-relaxed text-ink-muted">
        Simulasi TOEFL ITP berwaktu dengan tiga bagian (Listening, Structure, Reading) dan
        pengawasan lunak. Waktu berjalan di server dan tidak dapat dijeda.
      </p>
      <Link
        href={`/app/assessment/${sessionId}`}
        className="mt-4 inline-flex h-10 items-center justify-center rounded-sm bg-primary px-4 text-[13px] font-bold text-primary-ink hover:bg-primary-hover"
      >
        {completed ? "Lihat hasil tes" : "Mulai tes akhir"}
      </Link>
      <p className="mt-3 text-[12px] leading-relaxed text-ink-subtle">
        Skor yang dihasilkan adalah prediksi INVERTA, bukan skor TOEFL resmi dari ETS.
      </p>
    </div>
  );
}
```

- [ ] **Step 2: Reduce the route to a shell**

Replace the entire contents of `frontend/app/app/session/[id]/page.tsx` with:

```tsx
"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@/components/auth/AuthProvider";
import { Spinner } from "@/components/ui";
import { SessionView } from "@/components/learn/SessionView";

/**
 * The standalone session page. Kept as its own route because the dashboard, the assessment page's
 * back-link and learners' bookmarks all point at it — and because it is what a phone uses, where a
 * side-by-side layout has no room.
 */
export default function SessionPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();
  const id = useParams<{ id: string }>().id;

  useEffect(() => {
    if (status === "unauthenticated") router.replace(`/login?next=/app/session/${id}`);
  }, [status, id, router]);

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  return <SessionView token={accessToken} sessionId={id} />;
}
```

- [ ] **Step 3: Verify it compiles and nothing is orphaned**

```bash
cd "frontend" && npx tsc --noEmit && npm run build
```

Expected: no type errors, build succeeds. Lint fails on an unused import, so a leftover `Badge`, `Button` or `fmtDateTime` in the route file surfaces here.

- [ ] **Step 4: Look at the page — a build passing only proves it compiles**

Start the app and open a session:

```bash
docker compose -f docker-compose.tunnel.yml up -d --build
```

Open `https://inverta.recosta.id/app/session/{id}` for a video session and confirm it looks **exactly as it did before this task**: the header with the programme name and "Sesi N", the title, the video player, the progress bar, the gating test, and the next-session button.

This is a move, so any visual difference is a defect. A dropped prop compiles fine and renders wrong.

- [ ] **Step 5: Commit**

```bash
git add frontend/components/learn/SessionView.tsx "frontend/app/app/session/[id]/page.tsx"
git commit -m "refactor: extract SessionView so it can be embedded"
```

---

## Task 2: The two-column programme page

**Files:**
- Modify: `frontend/app/app/program/[id]/page.tsx`

**Interfaces:**
- Consumes: `<SessionView token sessionId embedded onSessionChanged />` from Task 1.

- [ ] **Step 1: Rewrite the page**

Replace the entire contents of `frontend/app/app/program/[id]/page.tsx` with:

```tsx
"use client";

import { useEffect, useMemo } from "react";
import Link from "next/link";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { SessionView } from "@/components/learn/SessionView";
import { Badge, Button, Spinner, ErrorState, CheckIcon, LockIcon, PlayIcon } from "@/components/ui";
import {
  getStudentProgram, minutesLabel, fmtDateTime, num,
  SESSION_TYPE_LABEL, type StudentSession,
} from "@/lib/programs";

export default function StudentProgramPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();
  const id = useParams<{ id: string }>().id;

  useEffect(() => {
    if (status === "unauthenticated") router.replace(`/login?next=/app/program/${id}`);
  }, [status, id, router]);

  const q = useQuery({
    queryKey: ["student-program", id],
    queryFn: () => getStudentProgram(accessToken!, id),
    enabled: status === "authenticated" && !!accessToken,
    retry: false,
  });

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }

  return (
    <div className="min-h-screen bg-bg">
      <AppHeader />
      <div className="mx-auto max-w-[1400px] px-6 pb-16 pt-7">
        {q.isPending ? (
          <div className="flex min-h-[240px] items-center justify-center"><Spinner size={24} /></div>
        ) : q.isError ? (
          <ErrorState
            title="Program tidak dapat dibuka"
            message="Pendaftaran Anda mungkin belum aktif. Jika Anda baru saja membayar, tunggu beberapa saat lalu muat ulang."
            action={
              <div className="flex gap-2">
                <Button variant="neutral" size="sm" onClick={() => q.refetch()}>Muat ulang</Button>
                <Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Ke dasbor</Link>
              </div>
            }
          />
        ) : (
          <ProgramBody token={accessToken} programId={id} data={q.data} />
        )}
      </div>
    </div>
  );
}

/**
 * A session is selectable only if it is unlocked AND renders inline. The final assessment is
 * neither: it is a 115-minute proctored sitting with one attempt, so it gets its own page, where
 * a session list beside it cannot tempt a learner into a proctor event.
 *
 * Module-level on purpose — defined inside the component it would be a new function every render
 * and therefore a changing dependency of the selection useMemo.
 */
const openable = (s: StudentSession) => s.state !== "Locked" && s.type !== "FinalAssessment";

function ProgramBody({
  token, programId, data,
}: {
  token: string;
  programId: string;
  data: NonNullable<Awaited<ReturnType<typeof getStudentProgram>>>;
}) {
  const router = useRouter();
  const params = useSearchParams();
  const qc = useQueryClient();

  const sessions = useMemo(
    () => [...data.sessions].sort((a, b) => num(a.orderIndex) - num(b.orderIndex)),
    [data.sessions],
  );
  const completed = num(data.completedCount);
  const total = num(data.sessionCount);
  const percent = total > 0 ? Math.round((completed / total) * 100) : 0;

  const requested = params.get("session");
  const selected = useMemo(() => {
    const wanted = sessions.find((s) => s.id === requested && openable(s));
    if (wanted) return wanted;
    // No parameter, or a stale/locked one: continue where the learner left off. The server refuses
    // locked content regardless, so falling back beats erroring on a shared or bookmarked link.
    const next = sessions.find((s) => s.id === data.nextSessionId && openable(s));
    return next ?? sessions.find(openable) ?? null;
  }, [sessions, requested, data.nextSessionId]);

  function select(s: StudentSession) {
    if (s.type === "FinalAssessment") {
      router.push(`/app/assessment/${s.id}`);
      return;
    }
    // replace, not push: stepping through sessions should not bury the dashboard under eight
    // history entries, but the URL still survives a reload and can be shared.
    router.replace(`/app/program/${programId}?session=${s.id}`, { scroll: false });
  }

  return (
    <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:gap-8">
      {/* ---- left: the list. Full width on a phone, 30% from lg up. ---- */}
      <aside className="w-full lg:sticky lg:top-[78px] lg:w-[30%] lg:min-w-[280px]">
        <div className="mb-4 flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <h1 className="text-[22px] font-extrabold tracking-tight">{data.name}</h1>
            {data.batchName && (
              <p className="text-[13px] text-ink-muted">
                Batch {data.batchName}
                {data.batchStartDate && ` · mulai ${fmtDateTime(data.batchStartDate)}`}
              </p>
            )}
          </div>
          <div className="flex items-center gap-2.5">
            <Badge tone={data.enrollmentStatus === "Active" ? "success" : "neutral"} className="px-2.5 py-1">
              {data.enrollmentStatus}
            </Badge>
            <span className="text-[12.5px] text-ink-muted">{completed}/{total} sesi</span>
          </div>
          <div className="h-[7px] overflow-hidden rounded-full bg-surface-2">
            <div className="h-full rounded-full bg-primary" style={{ width: `${percent}%` }} />
          </div>
        </div>

        <ol className="flex flex-col gap-2 lg:max-h-[calc(100vh-220px)] lg:overflow-y-auto lg:pr-1">
          {sessions.map((s) => (
            <SessionRow
              key={s.id}
              session={s}
              selected={selected?.id === s.id}
              onSelect={() => select(s)}
            />
          ))}
        </ol>

        <p className="mt-4 text-[12px] leading-relaxed text-ink-subtle">
          Sesi terbuka secara berurutan: sesi berikutnya aktif setelah sesi sebelumnya selesai.
        </p>
      </aside>

      {/* ---- right: the session. Hidden below lg, where the row links out instead. ---- */}
      <section className="hidden min-w-0 flex-1 lg:block">
        {selected ? (
          <SessionView
            key={selected.id}
            token={token}
            sessionId={selected.id}
            embedded
            onSessionChanged={() => qc.invalidateQueries({ queryKey: ["student-program", programId] })}
          />
        ) : (
          <div className="rounded-lg border border-border bg-surface px-6 py-14 text-center">
            <p className="text-[13.5px] text-ink-muted">Pilih sesi di sebelah kiri untuk mulai.</p>
          </div>
        )}
      </section>
    </div>
  );
}

function SessionRow({
  session, selected, onSelect,
}: { session: StudentSession; selected: boolean; onSelect: () => void }) {
  const locked = session.state === "Locked";
  const done = session.state === "Completed";
  const pct = num(session.percentComplete);

  const inner = (
    <>
      <span
        className={
          "mt-0.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-[11px] font-bold " +
          (done ? "bg-success text-white"
                : locked ? "border-2 border-border text-ink-subtle"
                : "bg-primary text-primary-ink")
        }
      >
        {done ? <CheckIcon size={12} strokeWidth={3} /> : locked ? <LockIcon size={11} /> : <PlayIcon size={10} />}
      </span>

      <span className="min-w-0 flex-1 text-left">
        <span className="flex flex-wrap items-center gap-1.5">
          <span className={"text-[13.5px] font-bold " + (locked ? "text-ink-muted" : "text-ink")}>
            {session.title}
          </span>
          {session.type !== "Video" && (
            <span className="rounded bg-primary-soft px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide text-primary">
              {SESSION_TYPE_LABEL[session.type] ?? session.type}
            </span>
          )}
        </span>
        <span className="mt-0.5 block text-[11.5px] text-ink-subtle">
          {session.durationSeconds != null && `${minutesLabel(session.durationSeconds)} · `}
          {done ? "Selesai" : locked ? "Terkunci" : pct > 0 ? `${Math.round(pct)}% ditonton` : "Siap dimulai"}
        </span>
      </span>
    </>
  );

  const shell =
    "flex w-full items-start gap-3 rounded-base border px-3.5 py-3 text-left transition-colors " +
    (locked
      ? "border-border opacity-70"
      : selected
        ? "border-primary bg-primary-soft/40"
        : "border-border bg-surface hover:bg-surface-2");

  if (locked) {
    return <li className={shell}>{inner}</li>;
  }

  return (
    <li>
      {/* Below lg the right pane is hidden, so the row must navigate. Above it, the button swaps
          the pane in place. A link that also has an onClick would do both on desktop. */}
      <Link href={`/app/session/${session.id}`} className={shell + " lg:hidden"}>
        {inner}
      </Link>
      <button type="button" onClick={onSelect} className={shell + " hidden lg:flex"}>
        {inner}
      </button>
    </li>
  );
}
```

- [ ] **Step 2: Verify it compiles**

```bash
cd "frontend" && npx tsc --noEmit && npm run build
```

Expected: no type errors, build succeeds.

**If the build fails with a `useSearchParams` error about a missing Suspense boundary**, that is Next's rule for statically-rendered pages. This page is client-rendered behind an auth check, so wrap the `<ProgramBody>` render in `<Suspense fallback={null}>` rather than changing the selection mechanism.

- [ ] **Step 3: Deploy and walk the checklist**

```bash
docker compose -f docker-compose.tunnel.yml up -d --build
```

Open `https://inverta.recosta.id/app/program/{id}` signed in as a learner with an active enrolment, and check each of these. They are the spec's §9 list; a build passing proves none of them.

| Check | Expected |
|---|---|
| Desktop layout | List left, session right, roughly 30/70 |
| Click a video session | Player, progress bar and gating test appear on the right without the page navigating |
| Click a live session | Schedule, location and join link appear |
| Click the final assessment | Navigates to `/app/assessment/{id}` — does NOT render inline |
| Locked session | Not clickable, shown dimmed with a padlock |
| Reload the page | The same session is still selected |
| Browser back | Steps between sessions, not out of the programme |
| `?session=` with a made-up id | Falls back to the next unlocked session, no error |
| Resize to 375px | Only the list shows; tapping a row opens `/app/session/{id}` |

- [ ] **Step 4: Verify the list updates without a manual refresh**

This is the §8 problem and the one thing most likely to be wrong.

On a video session whose gating test you can pass: watch past ~90% so the test unlocks, pass it, and **without reloading** look at the left column. The session you just completed must turn green, and the next one must stop showing a padlock.

If it does not, `onSessionChanged` is not reaching the page — check that `refresh` in `SessionView` calls it and that the query key matches `["student-program", programId]` exactly.

- [ ] **Step 5: Commit**

```bash
git add "frontend/app/app/program/[id]/page.tsx"
git commit -m "feat: programme page as 30/70 master-detail"
```

---

## Done

```bash
cd "frontend" && npx tsc --noEmit && npm run build
```

```bash
cd "backend" && dotnet test Academy.slnx
```

The backend suite should be **unchanged at 400 passing**. This plan touches no backend file — if that number moved, something outside the plan's scope was edited.

Then use `superpowers:finishing-a-development-branch`.

**What this deliberately does not do:**

- It does not embed the final assessment. That is a 115-minute proctored sitting with one attempt by default; a session list beside it invites the clicks that become proctor strikes.
- It does not build a phone layout. Below `lg` the list navigates to the standalone route that already exists and already works.
- It does not touch the linear lock, completion rules or the access gate. The server still decides what a learner may open; the client only stops offering it.
