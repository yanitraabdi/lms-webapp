"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, Spinner, ErrorState, AlertTriangleIcon, CheckIcon } from "@/components/ui";
import { ProctorWatcher } from "@/components/learn/ProctorWatcher";
import { Sitting, WarningOverlay } from "@/components/exam/Sitting";
import { ExamResult } from "@/components/exam/ExamResult";
import {
  getSessionAssessment, startAttempt, getAttemptState, saveSectionAnswers,
  advanceSection, finishAttempt, num,
  type AttemptState, type AttemptResult, type ProctorState, type StudentAssessment,
} from "@/lib/sessions";

type Phase = "intro" | "running" | "done";

export default function AssessmentPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();
  const sessionId = useParams<{ id: string }>().id;

  useEffect(() => {
    if (status === "unauthenticated") router.replace(`/login?next=/app/assessment/${sessionId}`);
  }, [status, sessionId, router]);

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  return <Runner token={accessToken} sessionId={sessionId} />;
}

function Runner({ token, sessionId }: { token: string; sessionId: string }) {
  const [phase, setPhase] = useState<Phase>("intro");
  const [state, setState] = useState<AttemptState | null>(null);
  const [result, setResult] = useState<AttemptResult | null>(null);
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [warning, setWarning] = useState<ProctorState | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [remaining, setRemaining] = useState(0);

  const meta = useQuery({
    queryKey: ["assessment-meta", sessionId],
    queryFn: () => getSessionAssessment(token, sessionId),
    retry: false,
  });

  const applyState = useCallback((s: AttemptState) => {
    setState(s);
    setAnswers(Object.fromEntries(Object.entries(s.answers).map(([k, v]) => [k, num(v)])));
    setRemaining(num(s.secondsRemaining));
    if (s.status === "Submitted") setPhase("done");
  }, []);

  // Local countdown for display only — the server is the authority and is re-checked on every call.
  useEffect(() => {
    if (phase !== "running" || remaining <= 0) return;
    const t = setInterval(() => setRemaining((r) => Math.max(0, r - 1)), 1000);
    return () => clearInterval(t);
  }, [phase, remaining]);

  // When the local clock hits zero, ask the server what actually happened.
  const attemptId = state?.attemptId;
  useEffect(() => {
    if (phase !== "running" || remaining > 0 || !attemptId) return;
    void (async () => {
      try {
        applyState(await advanceSection(token, attemptId, answers));
      } catch { /* the next poll corrects it */ }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remaining, phase, attemptId]);

  async function begin() {
    if (!meta.data) return;
    setBusy(true); setError(null);
    try {
      const attempt = await startAttempt(token, meta.data.id);
      applyState(await getAttemptState(token, attempt.id));
      setPhase("running");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal memulai tes.");
    } finally {
      setBusy(false);
    }
  }

  async function next() {
    if (!state) return;
    setBusy(true); setError(null);
    try {
      const s = await advanceSection(token, state.attemptId, answers);
      if (s.status === "Submitted") setResult(await finishAttempt(token, state.attemptId));
      applyState(s);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal melanjutkan.");
    } finally {
      setBusy(false);
    }
  }

  async function onProctor(p: ProctorState) {
    setWarning(p);
    if (p.action === "autoSubmit" && state) {
      try {
        setResult(await finishAttempt(token, state.attemptId));
        applyState(await getAttemptState(token, state.attemptId));
      } catch { /* state refresh will catch up */ }
    }
  }

  if (meta.isPending) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  if (meta.isError || !meta.data) {
    return (
      <div className="mx-auto max-w-md px-6 py-20">
        <ErrorState
          title="Tes tidak tersedia"
          message="Selesaikan sesi sebelumnya terlebih dahulu."
          action={<Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Ke dasbor</Link>}
        />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-bg">
      {state && phase === "running" && state.proctoringEnabled && (
        <ProctorWatcher token={token} attemptId={state.attemptId} active onState={onProctor} />
      )}

      {warning?.action === "warn" && (
        <WarningOverlay strikes={num(warning.strikes)} limit={num(warning.strikeLimit)} onClose={() => setWarning(null)} />
      )}

      <div className="mx-auto max-w-3xl px-6 py-8">
        {phase === "intro" && <Intro test={meta.data} onBegin={begin} busy={busy} error={error} />}
        {phase === "running" && state && (
          <Sitting
            token={token}
            state={state}
            answers={answers}
            remaining={remaining}
            busy={busy}
            error={error}
            onAnswer={(qid, choice) => setAnswers((a) => ({ ...a, [qid]: choice }))}
            onSave={async () => {
              if (!state) return;
              try { applyState(await saveSectionAnswers(token, state.attemptId, answers)); } catch { /* retried */ }
            }}
            onNext={next}
          />
        )}
        {phase === "done" && <ExamResult result={result} sessionId={sessionId} />}
      </div>
    </div>
  );
}

function Intro({
  test, onBegin, busy, error,
}: { test: StudentAssessment; onBegin: () => void; busy: boolean; error: string | null }) {
  return (
    <div className="rounded-lg border border-border bg-surface p-6 shadow-sm">
      <h1 className="text-[23px] font-extrabold tracking-tight">{test.title}</h1>
      <p className="mt-2 text-[14px] leading-relaxed text-ink-muted">
        Tes ini terdiri dari beberapa bagian berwaktu. Waktu setiap bagian berjalan di server dan
        tidak dapat dijeda. Setelah sebuah bagian selesai, Anda <strong className="text-ink">tidak
        dapat kembali</strong> ke bagian tersebut.
      </p>

      <ul className="mt-4 flex flex-col gap-2 text-[13.5px] text-ink-muted">
        <li className="flex gap-2.5"><CheckIcon size={17} className="mt-0.5 shrink-0 text-success" /> Jumlah soal: {num(test.questionCount)}</li>
        {test.timeLimitMinutes != null && (
          <li className="flex gap-2.5"><CheckIcon size={17} className="mt-0.5 shrink-0 text-success" /> Total waktu: {num(test.timeLimitMinutes)} menit</li>
        )}
        <li className="flex gap-2.5">
          <CheckIcon size={17} className="mt-0.5 shrink-0 text-success" />
          Kesempatan: {test.retakeCap == null ? "tidak dibatasi" : `${num(test.retakeCap)}× (terpakai ${num(test.attemptsUsed)})`}
        </li>
      </ul>

      {test.proctoringEnabled && (
        <div className="mt-5 rounded-base border border-[#F5D9A8] bg-warning-soft px-4 py-3.5">
          <div className="flex items-start gap-2.5">
            <AlertTriangleIcon size={18} className="mt-0.5 shrink-0 text-warning" />
            <div className="text-[13px] leading-relaxed text-ink">
              <strong>Pengawasan aktif.</strong> Sistem mendeteksi jika Anda meninggalkan halaman tes.
              Peringatan diberikan pada pelanggaran pertama; pada pelanggaran kedua tes
              <strong> dikirim otomatis</strong> dan ditandai untuk ditinjau.
              <div className="mt-1.5 text-[12px] text-ink-muted">
                Perpindahan fokus yang sangat singkat (di bawah 2 detik) diabaikan. Pengawasan ini
                bersifat pencegahan, bukan pengawasan ujian penuh.
              </div>
            </div>
          </div>
        </div>
      )}

      <p className="mt-5 text-[12px] leading-relaxed text-ink-subtle">
        Skor yang dihasilkan adalah <strong className="text-ink-muted">prediksi INVERTA</strong>,
        bukan skor TOEFL resmi dan tidak diterbitkan oleh ETS.
      </p>

      {error && (
        <div className="mt-4 rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>
      )}

      <Button className="mt-5" fullWidth onClick={onBegin} loading={busy} disabled={!test.canAttempt}>
        {test.canAttempt ? "Mulai tes" : "Batas percobaan tercapai"}
      </Button>
    </div>
  );
}
