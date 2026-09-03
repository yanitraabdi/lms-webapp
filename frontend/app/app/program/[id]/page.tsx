"use client";

import { useEffect, useMemo } from "react";
import Link from "next/link";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
import { SessionView } from "@/components/learn/SessionView";
import { Badge, Button, Spinner, ErrorState, CheckIcon, LockIcon, PlayIcon } from "@/components/ui";
import { useIsDesktop } from "@/lib/useIsDesktop";
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
  const isDesktop = useIsDesktop();

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

  // Pin the URL to the resolved fallback exactly once. Without this, `selected` keeps recomputing
  // its fallback on every refetch (e.g. after completing a session), which changes `nextSessionId`
  // and yanks the pane away from whatever the learner is actually looking at (finding C1).
  useEffect(() => {
    if (requested || !selected) return;
    router.replace(`/app/program/${programId}?session=${selected.id}`, { scroll: false });
  }, [requested, selected, programId, router]);

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
              {data.enrollmentStatus === "Active" ? "Terdaftar" : data.enrollmentStatus}
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
        {selected && isDesktop ? (
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
