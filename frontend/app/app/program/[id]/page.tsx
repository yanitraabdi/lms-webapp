"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { AppHeader } from "@/components/app/AppHeader";
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
      <div className="mx-auto max-w-3xl px-6 pb-16 pt-7">
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
          <ProgramBody data={q.data} />
        )}
      </div>
    </div>
  );
}

function ProgramBody({ data }: { data: NonNullable<Awaited<ReturnType<typeof getStudentProgram>>> }) {
  const router = useRouter();
  const sessions = [...data.sessions].sort((a, b) => num(a.orderIndex) - num(b.orderIndex));
  const completed = num(data.completedCount);
  const total = num(data.sessionCount);
  const percent = total > 0 ? Math.round((completed / total) * 100) : 0;

  return (
    <>
      <div className="mb-6 flex flex-col gap-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex flex-col gap-1">
            <h1 className="text-[24px] font-extrabold tracking-tight">{data.name}</h1>
            {data.batchName && (
              <p className="text-[13px] text-ink-muted">
                Batch {data.batchName}
                {data.batchStartDate && ` · mulai ${fmtDateTime(data.batchStartDate)}`}
              </p>
            )}
          </div>
          <Badge tone={data.enrollmentStatus === "Active" ? "success" : "neutral"} className="px-2.5 py-1">
            {data.enrollmentStatus === "Active" ? "Terdaftar" : data.enrollmentStatus}
          </Badge>
        </div>

        <div className="flex items-center gap-3">
          <div className="h-[7px] flex-1 overflow-hidden rounded-full bg-surface-2">
            <div className="h-full rounded-full bg-primary" style={{ width: `${percent}%` }} />
          </div>
          <span className="shrink-0 text-[12.5px] font-bold text-ink-muted">{completed}/{total} sesi</span>
        </div>
      </div>

      {data.nextSessionId && (
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-primary bg-primary-soft/50 px-5 py-4">
          <div className="flex flex-col">
            <span className="text-[12px] font-bold uppercase tracking-wide text-primary">Lanjutkan</span>
            <span className="text-sm font-bold text-ink">
              {sessions.find((s) => s.id === data.nextSessionId)?.title}
            </span>
          </div>
          <Button size="sm" onClick={() => router.push(`/app/session/${data.nextSessionId}`)}>
            Buka sesi →
          </Button>
        </div>
      )}

      <ol className="flex flex-col gap-2.5">
        {sessions.map((s) => <SessionRow key={s.id} session={s} />)}
      </ol>

      <p className="mt-6 text-[12px] leading-relaxed text-ink-subtle">
        Sesi terbuka secara berurutan: sesi berikutnya aktif setelah sesi sebelumnya selesai.
      </p>
    </>
  );
}

function SessionRow({ session }: { session: StudentSession }) {
  const router = useRouter();
  const locked = session.state === "Locked";
  const done = session.state === "Completed";
  const pct = num(session.percentComplete);

  return (
    <li
      className={
        "flex items-start gap-3.5 rounded-base border bg-surface px-4 py-3.5 " +
        (locked ? "border-border opacity-70" : "border-border")
      }
    >
      <span
        className={
          "mt-0.5 inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-[12px] font-bold " +
          (done ? "bg-success text-white"
                : locked ? "border-2 border-border text-ink-subtle"
                : "bg-primary text-primary-ink")
        }
      >
        {done ? <CheckIcon size={14} strokeWidth={3} /> : locked ? <LockIcon size={13} /> : <PlayIcon size={12} />}
      </span>

      <span className="min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-2">
          <span className={"text-[14.5px] font-bold " + (locked ? "text-ink-muted" : "text-ink")}>
            {session.title}
          </span>
          {session.type !== "Video" && (
            <span className="rounded bg-primary-soft px-2 py-0.5 text-[10.5px] font-bold uppercase tracking-wide text-primary">
              {SESSION_TYPE_LABEL[session.type] ?? session.type}
            </span>
          )}
          {session.hasAssessment && session.type === "Video" && (
            <span className="rounded bg-surface-2 px-2 py-0.5 text-[10.5px] font-bold uppercase tracking-wide text-ink-muted">
              + Tes
            </span>
          )}
        </span>

        {session.description && !locked && (
          <span className="mt-0.5 block text-[13px] leading-snug text-ink-muted">{session.description}</span>
        )}

        <span className="mt-1 block text-[11.5px] text-ink-subtle">
          {session.durationSeconds != null && `${minutesLabel(session.durationSeconds)} · `}
          {session.scheduledAt && `${fmtDateTime(session.scheduledAt)} · `}
          {done ? "Selesai" : locked ? "Terkunci" : pct > 0 ? `${Math.round(pct)}% ditonton` : "Siap dimulai"}
        </span>

        {/* Live details appear only once unlocked (withheld server-side while locked). */}
        {session.joinUrl && (
          <a
            href={session.joinUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="mt-1.5 inline-block text-[12.5px] font-bold text-primary hover:underline"
          >
            Buka tautan sesi live →
          </a>
        )}
        {session.location && (
          <span className="mt-1 block text-[12.5px] text-ink-muted">Lokasi: {session.location}</span>
        )}
      </span>

      {!locked && (
        <Button
          size="sm"
          variant={done ? "neutral" : "primary"}
          className="mt-0.5 shrink-0"
          onClick={() => router.push(`/app/session/${session.id}`)}
        >
          {done ? "Tinjau" : "Buka"}
        </Button>
      )}
    </li>
  );
}
