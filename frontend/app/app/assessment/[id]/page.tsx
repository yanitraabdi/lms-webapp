"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, Spinner, ErrorState, AlertTriangleIcon, CheckIcon } from "@/components/ui";
import { ProctorWatcher } from "@/components/learn/ProctorWatcher";
import {
  getSessionAssessment, startAttempt, getAttemptState, saveSectionAnswers,
  advanceSection, finishAttempt, getAudioUrl, clock, num,
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
        {phase === "done" && <Result result={result} sessionId={sessionId} />}
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

function Sitting({
  token, state, answers, remaining, busy, error, onAnswer, onSave, onNext,
}: {
  token: string;
  state: AttemptState;
  answers: Record<string, number>;
  remaining: number;
  busy: boolean;
  error: string | null;
  onAnswer: (qid: string, choice: number) => void;
  onSave: () => void;
  onNext: () => void;
}) {
  const answered = state.questions.filter((q) => answers[q.id] !== undefined).length;
  const isLast = num(state.sectionIndex) >= num(state.sectionCount) - 1;
  const low = remaining <= 60;

  // Persist answers periodically so a crashed tab never loses the sitting.
  const saveRef = useRef(onSave);
  saveRef.current = onSave;
  useEffect(() => {
    const t = setInterval(() => saveRef.current(), 15000);
    return () => clearInterval(t);
  }, []);

  return (
    <>
      <div className="sticky top-0 z-30 -mx-6 mb-5 border-b border-border bg-surface/95 px-6 py-3 backdrop-blur">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-col">
            <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">
              Bagian {num(state.sectionIndex) + 1} dari {num(state.sectionCount)}
            </span>
            <span className="text-[15px] font-extrabold">{state.currentSection}</span>
          </div>
          <div className="flex items-center gap-4">
            {state.proctoringEnabled && num(state.strikes) > 0 && (
              <span className="text-[12px] font-bold text-warning">
                Peringatan {num(state.strikes)}/{num(state.strikeLimit)}
              </span>
            )}
            <span className={"tabular-nums text-[20px] font-extrabold " + (low ? "text-danger" : "text-ink")}>
              {clock(remaining)}
            </span>
          </div>
        </div>
      </div>

      <ol className="flex flex-col gap-6">
        {state.questions.map((q, i) => (
          <li key={q.id}>
            <p className="text-sm font-bold text-ink">{i + 1}. {q.prompt}</p>
            {q.passageRef && (
              <p className="mt-1.5 rounded-base bg-surface-2 px-3.5 py-2.5 text-[13px] leading-relaxed text-ink-muted">
                {q.passageRef}
              </p>
            )}
            {q.hasAudio && (
              <AudioButton
                token={token}
                attemptId={state.attemptId}
                questionId={q.id}
                playsLeft={num(state.audioPlaysLeft[q.id] ?? 0)}
                hasLimit={q.id in state.audioPlaysLeft}
              />
            )}
            <div className="mt-2 flex flex-col gap-1.5">
              {q.choices.map((choice, ci) => {
                const selected = answers[q.id] === ci;
                return (
                  <label
                    key={ci}
                    className={
                      "flex cursor-pointer items-center gap-2.5 rounded-base border px-3 py-2 text-[13.5px] transition-colors " +
                      (selected ? "border-primary bg-primary-soft/50 font-semibold text-ink" : "border-border hover:bg-surface-2")
                    }
                  >
                    <input
                      type="radio"
                      name={`q-${q.id}`}
                      checked={selected}
                      onChange={() => onAnswer(q.id, ci)}
                      className="accent-primary"
                    />
                    {choice}
                  </label>
                );
              })}
            </div>
          </li>
        ))}
      </ol>

      {error && (
        <div className="mt-5 rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>
      )}

      <div className="mt-6 flex items-center justify-between gap-3 border-t border-border pt-5">
        <span className="text-[12.5px] text-ink-subtle">{answered}/{state.questions.length} terjawab</span>
        <Button onClick={onNext} loading={busy}>
          {isLast ? "Selesaikan tes" : "Bagian berikutnya →"}
        </Button>
      </div>
      <p className="mt-3 text-[11.5px] text-ink-subtle">
        Setelah melanjutkan, Anda tidak dapat kembali ke bagian ini.
      </p>
    </>
  );
}

function AudioButton({
  token, attemptId, questionId, playsLeft, hasLimit,
}: { token: string; attemptId: string; questionId: string; playsLeft: number; hasLimit: boolean }) {
  const [url, setUrl] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  async function play() {
    setBusy(true); setErr(null);
    try {
      const r = await getAudioUrl(token, attemptId, questionId);
      setUrl(r.url);
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Audio tidak dapat diputar.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-2">
      {url ? (
        <audio controls autoPlay src={url} className="w-full max-w-sm" />
      ) : (
        <Button size="sm" variant="neutral" onClick={play} loading={busy} disabled={hasLimit && playsLeft <= 0}>
          ▶ Putar audio{hasLimit && ` (sisa ${playsLeft})`}
        </Button>
      )}
      {err && <p className="mt-1 text-[12px] font-semibold text-danger">{err}</p>}
    </div>
  );
}

function WarningOverlay({ strikes, limit, onClose }: { strikes: number; limit: number; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-[rgba(17,37,63,0.55)] p-6">
      <div className="max-w-md rounded-lg bg-surface p-6 text-center shadow-lg">
        <AlertTriangleIcon size={34} className="mx-auto text-warning" />
        <h2 className="mt-3 text-lg font-extrabold">Jangan tinggalkan halaman tes</h2>
        <p className="mt-2 text-[13.5px] leading-relaxed text-ink-muted">
          Sistem mencatat bahwa Anda meninggalkan halaman. Ini adalah peringatan
          <strong className="text-ink"> {strikes} dari {limit}</strong>. Jika terjadi lagi, tes akan
          dikirim otomatis dan ditandai untuk ditinjau.
        </p>
        <Button className="mt-4" fullWidth onClick={onClose}>Lanjutkan tes</Button>
      </div>
    </div>
  );
}

function Result({ result, sessionId }: { result: AttemptResult | null; sessionId: string }) {
  if (!result) {
    return (
      <div className="rounded-lg border border-border bg-surface p-6 text-center shadow-sm">
        <h1 className="text-xl font-extrabold">Tes selesai</h1>
        <p className="mt-2 text-[13.5px] text-ink-muted">Hasil Anda sedang diproses.</p>
        <Link href="/app/certificates" className="mt-4 inline-block text-sm font-bold text-primary hover:underline">
          Lihat sertifikat →
        </Link>
      </div>
    );
  }

  const sections = Object.entries(result.sectionScores);

  return (
    <div className="flex flex-col gap-5">
      <div className="rounded-lg border border-border bg-surface p-6 text-center shadow-sm">
        <h1 className="text-xl font-extrabold">Tes selesai</h1>

        {result.totalScaledScore != null ? (
          <>
            <p className="mt-4 text-[12px] font-bold uppercase tracking-wide text-ink-subtle">
              Skor prediksi TOEFL
            </p>
            <p className="text-[52px] font-extrabold leading-none tracking-tight text-primary">
              {num(result.totalScaledScore)}
            </p>
            {result.predictedBand && (
              <p className="mt-1 text-[13.5px] font-semibold text-ink-muted">{result.predictedBand}</p>
            )}
          </>
        ) : (
          <div className="mt-4 rounded-base bg-warning-soft px-4 py-3 text-[13px] text-ink">
            Skor mentah Anda tersimpan, namun konversi ke skor prediksi belum tersedia.
            Tim kami akan menerbitkan sertifikat Anda setelah tabel konversi dilengkapi.
          </div>
        )}

        <p className="mt-4 text-[13px] text-ink-muted">
          Jawaban benar: <strong className="text-ink">{num(result.score)}</strong> dari {num(result.maxScore)}
        </p>

        {result.autoSubmitted && (
          <p className="mt-2 text-[12.5px] font-semibold text-warning">
            Tes ini dikirim otomatis (waktu habis atau pengawasan).
          </p>
        )}
        {result.proctorFlagged && (
          <p className="mt-1 text-[12.5px] font-semibold text-danger">
            Ditandai untuk ditinjau. Hubungi admin jika Anda merasa ini keliru.
          </p>
        )}
      </div>

      {sections.length > 0 && (
        <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
          <h2 className="text-sm font-extrabold">Rincian per bagian</h2>
          <ul className="mt-3 flex flex-col gap-2">
            {sections.map(([name, value]) => (
              <li key={name} className="flex items-center justify-between text-[13.5px]">
                <span className="text-ink-muted">{name}</span>
                <span className="font-bold text-ink">{num(value)}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="rounded-base border border-border bg-surface-2 px-5 py-4">
        <p className="text-[12px] leading-relaxed text-ink-muted">
          <strong className="text-ink">Penting.</strong> Skor ini adalah <strong className="text-ink">prediksi
          INVERTA</strong> berdasarkan simulasi internal — <strong className="text-ink">bukan skor TOEFL
          resmi</strong> dan tidak diterbitkan oleh ETS. TOEFL adalah merek dagang terdaftar milik ETS.
        </p>
      </div>

      <div className="flex flex-wrap gap-3">
        <Link
          href="/app/certificates"
          className="inline-flex h-10 items-center justify-center rounded-sm bg-primary px-4 text-[13px] font-bold text-primary-ink hover:bg-primary-hover"
        >
          Lihat sertifikat
        </Link>
        <Link
          href={`/app/session/${sessionId}`}
          className="inline-flex h-10 items-center justify-center rounded-sm border border-border bg-surface px-4 text-[13px] font-bold text-ink hover:bg-surface-2"
        >
          Kembali ke sesi
        </Link>
      </div>
    </div>
  );
}
