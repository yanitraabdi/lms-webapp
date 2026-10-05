"use client";

import { useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Spinner, CheckIcon } from "@/components/ui";
import { LiveAudioPlayer } from "@/components/learn/LiveAudioPlayer";
import {
  getPartAssessment, getGatingAudioUrl, startAttempt, startTestAudio, submitAttempt, num,
  type Attempt, type AttemptResult, type SessionPart, type StudentAssessment, type TestAudio,
} from "@/lib/sessions";

/**
 * The test of one Test part. The part list only opens it once the parts before it are done —
 * but the real gate is server-side: the session only completes when this is passed (GR-8).
 */
export function GatingTest({
  token, sessionId, part, onChanged, onNext, morePartsFollow,
}: {
  token: string;
  sessionId: string;
  part: SessionPart;
  onChanged: () => void;
  /** Selects the next open part; set only when a later part is open. */
  onNext?: () => void;
  /** A later part exists in this session; session-level messages stay with SessionView. */
  morePartsFollow: boolean;
}) {
  const qc = useQueryClient();
  const q = useQuery({
    queryKey: ["part-assessment", part.id],
    queryFn: () => getPartAssessment(token, sessionId, part.id),
  });

  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [result, setResult] = useState<AttemptResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Shared-audio tests only: the attempt is opened up front so the recording's plays are charged
  // against it; the latest audio ticket carries plays used/limit; the key remounts the player.
  const [attempt, setAttempt] = useState<Attempt | null>(null);
  const [audio, setAudio] = useState<TestAudio | null>(null);
  const [audioKey, setAudioKey] = useState(0);
  // True between a "Putar ulang" tap and the remounted player's first start: that first call is
  // the one that spends the replay; any later one ("Lanjutkan" after a drop) only resumes it.
  const replayPending = useRef(false);

  if (q.isPending) {
    return (
      <div className="flex min-h-[110px] items-center justify-center rounded-lg border border-border bg-surface">
        <Spinner size={20} />
      </div>
    );
  }
  if (!q.data) return null;              // no gating test on this session

  const test: StudentAssessment = q.data;
  const alreadyPassed = test.passed || part.passed || result?.passed === true;
  const shared = test.hasTestAudio;
  const failed = num(part.failedAttempts);
  const playLimit = num(test.testAudioPlayLimit ?? 1);
  const playsLeft = audio ? num(audio.playLimit) - num(audio.playsUsed) : 0;
  const total = test.questions.length;
  const answered = Object.keys(answers).length;

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const open = shared && attempt ? attempt : await startAttempt(token, test.id);
      const r = await submitAttempt(token, open.id, answers);
      setResult(r);
      await qc.invalidateQueries({ queryKey: ["part-assessment", part.id] });
      // Pass or fail, the part list changes (failures can open the discussion), so refetch it.
      onChanged();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal mengirim jawaban.");
    } finally {
      setBusy(false);
    }
  }

  function retry() {
    setResult(null);
    setAnswers({});
    // A new attempt starts with fresh plays, so drop the old one and its audio.
    setAttempt(null);
    setAudio(null);
    setAudioKey(0); // the next attempt's player waits for a tap again
  }

  async function begin() {
    setBusy(true);
    setError(null);
    try {
      setAttempt(await startAttempt(token, test.id)); // returns the open attempt on a reload
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal memulai tes.");
    } finally {
      setBusy(false);
    }
  }

  // The remounted player auto-begins, so the tap itself starts the replay; its first start()
  // spends the play, later ones resume. Errors (e.g. the limit) surface in the error line.
  function replay() {
    setError(null);
    replayPending.current = true;
    setAudioKey((k) => k + 1);
  }

  async function startAudio() {
    const isReplay = replayPending.current;
    replayPending.current = false;
    try {
      const r = await startTestAudio(token, sessionId, part.id, isReplay);
      setAudio(r);
      return r;
    } catch (e) {
      if (isReplay) setError(e instanceof Error ? e.message : "Audio tidak dapat diputar ulang.");
      throw e;
    }
  }

  return (
    <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h3 className="text-base font-extrabold">Tes sesi</h3>
        {alreadyPassed ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-success-soft px-2.5 py-1 text-[12px] font-bold text-success">
            <CheckIcon size={13} strokeWidth={3} /> Lulus
          </span>
        ) : (
          <span className="text-[12px] text-ink-muted">
            Skor lulus: {num(test.passThreshold)}/{total} · dapat diulang sampai lulus
          </span>
        )}
      </div>

      <p className="mt-1 text-[13px] text-ink-muted">
        {alreadyPassed
          ? (morePartsFollow ? "Anda sudah lulus tes ini. Bagian berikutnya sudah terbuka." : "Anda sudah lulus tes ini.")
          : morePartsFollow
            ? "Jawab semua pertanyaan untuk membuka bagian berikutnya."
            : "Jawab semua pertanyaan untuk menyelesaikan sesi ini."}
      </p>

      {!alreadyPassed && shared && !attempt && (
        <>
          {error && (
            <div className="mt-4 rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>
          )}
          <div className="mt-4">
            <Button onClick={begin} loading={busy}>Mulai tes</Button>
          </div>
        </>
      )}

      {!alreadyPassed && (!shared || attempt) && (
        <>
          {shared && !result && (
            <div>
              <LiveAudioPlayer
                key={audioKey}
                start={startAudio}
                startedAt={null}
                autoBegin={audioKey > 0}
                intro="Satu rekaman untuk semua soal tes ini. Audio diputar tanpa jeda dan tanpa mundur — pastikan suara perangkat Anda aktif."
                endedLabel="Audio tes sudah selesai diputar."
              />
              {playLimit > 1 && audio && playsLeft > 0 && (
                <Button size="sm" variant="neutral" className="mt-2" onClick={replay}>
                  Putar ulang (sisa {playsLeft}×)
                </Button>
              )}
            </div>
          )}
          <ol className="mt-4 flex flex-col gap-5">
            {test.questions.map((question, qi) => (
              <li key={question.id}>
                <p className="text-sm font-bold text-ink">{qi + 1}. {question.prompt}</p>
                {question.hasAudio && (
                  <QuestionAudio token={token} sessionId={sessionId} partId={part.id} questionId={question.id} />
                )}
                {question.passageRef && (
                  <p className="mt-1 rounded-base bg-surface-2 px-3 py-2 text-[12.5px] leading-relaxed text-ink-muted">
                    {question.passageRef}
                  </p>
                )}
                <div className="mt-2 flex flex-col gap-1.5">
                  {question.choices.map((choice, ci) => {
                    const selected = answers[question.id] === ci;
                    return (
                      <label
                        key={ci}
                        className={
                          "flex cursor-pointer items-center gap-2.5 rounded-base border px-3 py-2 text-[13.5px] transition-colors " +
                          (selected
                            ? "border-primary bg-primary-soft/50 font-semibold text-ink"
                            : "border-border hover:bg-surface-2")
                        }
                      >
                        <input
                          type="radio"
                          name={`q-${question.id}`}
                          checked={selected}
                          disabled={busy}
                          onChange={() => setAnswers((a) => ({ ...a, [question.id]: ci }))}
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

          {result && !result.passed && (
            <div className="mt-4 rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">
              Skor Anda {result.score}/{result.maxScore}. Belum mencapai batas lulus — coba lagi.
            </div>
          )}
          {failed > 0 && (
            <div className="mt-2 text-[13px] text-ink-muted">
              <p>Gagal {failed} kali.</p>
              {part.discussionAfterFailures != null && failed >= num(part.discussionAfterFailures) && (
                <p>Video pembahasan sudah terbuka. Tonton, lalu ulangi tes sampai lulus.</p>
              )}
            </div>
          )}
          {error && (
            <div className="mt-4 rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>
          )}

          <div className="mt-4 flex items-center justify-between gap-3">
            <span className="text-[12.5px] text-ink-subtle">{answered}/{total} terjawab</span>
            {result && !result.passed ? (
              // Always offered: a gating test is retried until passed, so there is no capped-out
              // state to render here (the server ignores any stored cap on a Gating assessment).
              <Button onClick={retry}>Coba lagi</Button>
            ) : (
              <Button onClick={submit} loading={busy} disabled={answered < total}>Kirim jawaban</Button>
            )}
          </div>
        </>
      )}

      {result?.passed && (
        <div className="mt-4 rounded-base bg-success-soft px-4 py-3 text-sm font-semibold text-success">
          Selamat! Skor {result.score}/{result.maxScore}.{morePartsFollow && " Bagian berikutnya sudah terbuka."}
        </div>
      )}
      {alreadyPassed && onNext && (
        <div className="mt-4">
          <Button onClick={onNext}>Lanjut ke bagian berikutnya</Button>
        </div>
      )}
    </div>
  );
}

/**
 * The audio control for a Listening question in a session test.
 *
 * The URL is minted per play and signed with a short TTL (GR-3), so it is fetched on demand rather
 * than embedded in the question payload — a URL sitting in the DTO would outlive its access check.
 *
 * There is no play limit here: a gating test creates its attempt at submit, so while answering
 * there is nothing to charge a play against, and unlimited retakes would make a cap meaningless.
 */
function QuestionAudio({
  token, sessionId, partId, questionId,
}: { token: string; sessionId: string; partId: string; questionId: string }) {
  const [url, setUrl] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setBusy(true);
    setError(null);
    try {
      setUrl((await getGatingAudioUrl(token, sessionId, partId, questionId)).url);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Audio tidak dapat dimuat.");
    } finally {
      setBusy(false);
    }
  }

  if (url) {
    // autoPlay because the learner already pressed a button to get here; a second click to start
    // playback would be a click for nothing.
    return <audio src={url} controls autoPlay className="mt-2 w-full max-w-md" />;
  }

  return (
    <div className="mt-2 flex flex-wrap items-center gap-2">
      <Button size="sm" variant="neutral" onClick={load} loading={busy}>
        ▶ Putar audio
      </Button>
      {error && <span className="text-[12px] font-semibold text-danger">{error}</span>}
    </div>
  );
}

