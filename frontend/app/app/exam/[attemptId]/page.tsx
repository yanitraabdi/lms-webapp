"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { notFound, useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, Spinner, ErrorState } from "@/components/ui";
import { ProctorWatcher } from "@/components/learn/ProctorWatcher";
import { Sitting, WarningOverlay } from "@/components/exam/Sitting";
import { ExamResult } from "@/components/exam/ExamResult";
import {
  getAttemptStateOrNull, getAttemptState, getFinalResult, saveSectionAnswers, advanceSection,
  num, type AttemptState, type ProctorState,
} from "@/lib/sessions";

const fullSpinner = <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;

/** A thrown TypeError is the browser's own (English) network failure; problem() only localises HTTP errors. */
function loadMessage(e: unknown, fallback: string): string {
  if (e instanceof TypeError) return `${fallback} Periksa koneksi Anda.`;
  return e instanceof Error ? e.message : fallback;
}

/**
 * One final-exam sitting, addressed by its attempt. A reload resumes it on the server's clock; once
 * it has ended the same URL shows its result, read-only. Another learner's attempt is a 404.
 */
export default function ExamPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();
  const attemptId = useParams<{ attemptId: string }>().attemptId;

  useEffect(() => {
    if (status === "unauthenticated") router.replace(`/login?next=/app/exam/${attemptId}`);
  }, [status, attemptId, router]);

  if (status !== "authenticated" || !accessToken) return fullSpinner;
  return <Exam token={accessToken} attemptId={attemptId} />;
}

function Exam({ token, attemptId }: { token: string; attemptId: string }) {
  const [state, setState] = useState<AttemptState | null>(null);
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [warning, setWarning] = useState<ProctorState | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [remaining, setRemaining] = useState(0);

  // Fetched once per mount. Any refetch would re-run applyState and overwrite unsaved answers, so it
  // must never go stale; gcTime 0 so a remount reads the server's clock afresh.
  const load = useQuery({
    queryKey: ["attempt-state", attemptId],
    queryFn: () => getAttemptStateOrNull(token, attemptId),
    retry: false,
    staleTime: Infinity,
    gcTime: 0,
  });

  const applyState = useCallback((s: AttemptState) => {
    setState(s);
    setAnswers(Object.fromEntries(Object.entries(s.answers).map(([k, v]) => [k, num(v)])));
    setRemaining(num(s.secondsRemaining));
  }, []);

  useEffect(() => {
    if (load.data) applyState(load.data);
  }, [load.data, applyState]);

  const running = state?.status === "InProgress";

  // However the sitting ended — reload, timer, last section, proctor — read its result from the server.
  const resultQuery = useQuery({
    queryKey: ["attempt-result", attemptId],
    queryFn: () => getFinalResult(token, attemptId),
    enabled: state?.status === "Submitted",
    retry: 1,
    staleTime: Infinity,
    gcTime: 0,
  });
  const shown = resultQuery.data ?? null;

  // Local countdown for display only — the server is the authority and is re-checked on every call.
  useEffect(() => {
    if (!running || remaining <= 0) return;
    const t = setInterval(() => setRemaining((r) => Math.max(0, r - 1)), 1000);
    return () => clearInterval(t);
  }, [running, remaining]);

  // When the local clock hits zero, ask the server what actually happened.
  useEffect(() => {
    if (!running || remaining > 0 || !state) return;
    setBusy(true);
    void (async () => {
      try {
        applyState(await advanceSection(token, attemptId, answers, num(state.sectionIndex)));
      } catch { /* the next poll corrects it */ } finally { setBusy(false); }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remaining, running, attemptId]);

  // When the sitting ends IN this tab, the focused button unmounts: move focus and scroll to the
  // result's heading so the end (or an auto-submit) is announced. A cold reload keeps focus alone.
  const wasRunning = useRef(false);
  useEffect(() => {
    if (running) { wasRunning.current = true; return; }
    if (wasRunning.current && shown) {
      window.scrollTo(0, 0);
      document.getElementById("exam-result-title")?.focus();
    }
  }, [running, shown]);

  async function next() {
    if (!state) return;
    setBusy(true); setError(null);
    try {
      applyState(await advanceSection(token, attemptId, answers, num(state.sectionIndex)));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal melanjutkan.");
    } finally {
      setBusy(false);
    }
  }

  async function onProctor(p: ProctorState) {
    setWarning(p);
    if (p.action === "autoSubmit") {
      try {
        applyState(await getAttemptState(token, attemptId));
      } catch { /* state refresh will catch up */ }
    }
  }

  const toDashboard = <Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Ke dasbor</Link>;

  if (load.isPending) return fullSpinner;
  if (load.isError) {
    // The server clock keeps running, so a failed reload must be retryable in place.
    return (
      <div className="mx-auto max-w-md px-6 py-20">
        <ErrorState
          title="Tes tidak tersedia"
          message={loadMessage(load.error, "Gagal memuat tes.")}
          action={
            <div className="flex items-center gap-4">
              <Button size="sm" onClick={() => load.refetch()}>Coba lagi</Button>
              {toDashboard}
            </div>
          }
        />
      </div>
    );
  }
  if (load.data === null) notFound();
  if (!state) return fullSpinner;

  return (
    <div className="min-h-screen bg-bg">
      {running && state.proctoringEnabled && (
        <ProctorWatcher token={token} attemptId={attemptId} active onState={onProctor} />
      )}

      {warning?.action === "warn" && (
        <WarningOverlay strikes={num(warning.strikes)} limit={num(warning.strikeLimit)} onClose={() => setWarning(null)} />
      )}

      <div className="mx-auto max-w-3xl px-6 py-8">
        {running ? (
          <Sitting
            token={token}
            state={state}
            answers={answers}
            remaining={remaining}
            busy={busy}
            error={error}
            onAnswer={(qid, choice) => setAnswers((a) => ({ ...a, [qid]: choice }))}
            onSave={async () => {
              try { applyState(await saveSectionAnswers(token, attemptId, answers)); } catch { /* retried */ }
            }}
            onNext={next}
          />
        ) : shown ? (
          <ExamResult result={shown} sessionId={state.sessionId ?? null} />
        ) : resultQuery.isError ? (
          <ErrorState
            title="Gagal memuat hasil"
            message={loadMessage(resultQuery.error, "Gagal memuat hasil.")}
            action={
              <div className="flex items-center gap-4">
                <Button size="sm" onClick={() => resultQuery.refetch()}>Coba lagi</Button>
                {toDashboard}
              </div>
            }
          />
        ) : (
          <div className="flex justify-center py-20"><Spinner size={24} /></div>
        )}
      </div>
    </div>
  );
}
