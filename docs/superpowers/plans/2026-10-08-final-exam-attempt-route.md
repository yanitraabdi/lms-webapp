# Final exam on a per-attempt route — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** the final exam runs at `/app/exam/{attemptId}`. That URL survives a reload mid-sitting, shows the read-only result once the sitting ends, and is a 404 for anyone else's attempt.

**Architecture:**
- **Backend.** It already keys every sitting call by attempt and already 404s another learner's attempt. It gains three additive pieces:
  - `StudentAssessmentDto.OpenAttemptId`;
  - `AttemptStateDto.SessionId`;
  - a read-only `GET /api/attempts/{id}/final-result`.
- **Frontend.** The 482-line `app/app/assessment/[id]/page.tsx` is split:
  - the intro stays at `/app/assessment/{sessionId}`;
  - the sitting and the result move to `components/exam/`;
  - a new `app/app/exam/[attemptId]/page.tsx` drives them from the server's state.

**Tech Stack:** .NET 10 minimal APIs (TypedResults), EF Core + Npgsql, xUnit + Testcontainers, Next.js 15 App Router (client pages), TanStack Query, openapi-typescript generated client.

**Spec:** `docs/superpowers/specs/2026-10-07-final-exam-attempt-route-design.md`

## Global Constraints

- **Routes.**
  - `/app/assessment/{sessionId}` is the intro. Its URL is unchanged; the dashboard, `SessionView` and the programme page link to it.
  - `/app/exam/{attemptId}` is the sitting, then its result.
- **Intro → attempt.** Navigation uses `router.replace`, never `push`.
  - With `OpenAttemptId` set, the intro redirects immediately.
  - Otherwise "Mulai tes" calls `POST /api/assessments/{id}/attempts`, then redirects.
- **Attempt page.** It switches on `GET /api/attempts/{id}/state`:
  - `InProgress` → the sitting;
  - `Submitted` → the read-only result from `GET /api/attempts/{id}/final-result`;
  - 404 → `notFound()`. Never distinguish "not yours" from "does not exist";
  - any other error → `ErrorState` with a "Ke dasbor" link.
- **DTO additions** are the LAST positional field: `StudentAssessmentDto(..., Guid? OpenAttemptId)` and `AttemptStateDto(..., Guid? SessionId)`.
- **`final-result`:**
  - loading: `LoadOwnedAsync` (owner + gate), then `EnforceDeadlinesAsync`;
  - still in progress → 409 `"Tes ini belum selesai."`;
  - never finalises an in-progress attempt;
  - no rate limit;
  - no answer key (GR-11).
- **No behaviour change** to the sitting: timer, 15 s autosave, proctoring, audio, warnings.
- **Client:** the generated API client is regenerated, never hand-edited.
- **Copy:** strings stay in Bahasa Indonesia.
- **GR-14:** the result keeps the "prediksi INVERTA, bukan skor TOEFL resmi" note.
- **Out of scope:** gating tests on video sessions; proctoring, scoring, retake caps and certificates; listing past attempts.

## File map

| File | Change |
|---|---|
| `backend/src/Application/Assessments/AssessmentContracts.cs` | `StudentAssessmentDto` + `Guid? OpenAttemptId` |
| `backend/src/Application/Assessments/FinalAssessmentContracts.cs` | `AttemptStateDto` + `Guid? SessionId`; `IFinalAssessmentService.GetResultAsync` |
| `backend/src/Infrastructure/Assessments/AssessmentService.cs` | `BuildStudentViewAsync` fills `OpenAttemptId` |
| `backend/src/Infrastructure/Assessments/FinalAssessmentService.cs` | `LoadOwnedAsync` returns the session id; state/result builders take it; `GetResultAsync` |
| `backend/src/Api/Endpoints/FinalAssessmentEndpoints.cs` | `GET /api/attempts/{id}/final-result` |
| `backend/tests/Integration.Tests/FinalAssessmentTests.cs` | 6 new tests |
| `frontend/api-client/schema.ts` | regenerated |
| `frontend/lib/sessions.ts` | `getAttemptStateOrNull`, `getFinalResult` |
| `frontend/components/exam/Sitting.tsx` | new — `Sitting`, `AudioButton`, `WarningOverlay` moved verbatim |
| `frontend/components/exam/ExamResult.tsx` | new — the old `Result`, `sessionId` now optional |
| `frontend/app/app/assessment/[id]/page.tsx` | intro only |
| `frontend/app/app/exam/[attemptId]/page.tsx` | new — the attempt page |

---

### Task 1: Backend — open attempt, state session, read-only final result

**Files:**
- Modify: `backend/src/Application/Assessments/AssessmentContracts.cs` (record at ~line 62)
- Modify: `backend/src/Application/Assessments/FinalAssessmentContracts.cs` (`AttemptStateDto` ~line 10; `IFinalAssessmentService` ~line 44)
- Modify: `backend/src/Infrastructure/Assessments/AssessmentService.cs` (`BuildStudentViewAsync` ~line 234)
- Modify: `backend/src/Infrastructure/Assessments/FinalAssessmentService.cs` (lines ~27–160 call sites, `BuildStateAsync` ~285, `BuildResultAsync` ~360, `LoadOwnedAsync` ~388)
- Modify: `backend/src/Api/Endpoints/FinalAssessmentEndpoints.cs`
- Test: `backend/tests/Integration.Tests/FinalAssessmentTests.cs`
- Regenerate: `frontend/api-client/schema.ts`

**Interfaces:**
- Produces:
  - `StudentAssessmentDto.OpenAttemptId : Guid?` (TS: `openAttemptId?: string | null`);
  - `AttemptStateDto.SessionId : Guid?` (TS: `sessionId?: string | null`);
  - `IFinalAssessmentService.GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct = default) : Task<AttemptResultDto>`;
  - endpoint `GET /api/attempts/{id}/final-result` → `AttemptResultDto`.

- [ ] **Step 1: Write the failing tests**

Add this region to `FinalAssessmentTests.cs`, just before `// ---- the final's student route (FINAL sessions only) ----`. It uses the class's existing helpers: `SetUp`, `Start`, `GetState`, `FinishAllSections`, `AnswerEverythingCorrectly`, `ExpireAllSectionsAsync`, `VerifiedUser`, `Authed`, `AuthedGet`, `Json`.

```csharp
    // ---- the per-attempt route (sub-project 5) ----

    [Fact]
    public async Task The_intro_names_the_open_attempt_until_it_is_submitted()
    {
        var c = await SetUp();
        var url = $"/api/sessions/{c.SessionId}/assessment";
        Assert.Null((await AuthedGet<StudentAssessmentDto>(url, c.Token)).OpenAttemptId);

        var state = await Start(c);
        Assert.Equal(state.AttemptId, (await AuthedGet<StudentAssessmentDto>(url, c.Token)).OpenAttemptId);

        await FinishAllSections(c, state.AttemptId);
        Assert.Null((await AuthedGet<StudentAssessmentDto>(url, c.Token)).OpenAttemptId);
    }

    [Fact]
    public async Task State_names_its_session_and_serves_no_questions_once_submitted()
    {
        var c = await SetUp();
        var state = await Start(c);
        Assert.Equal(c.SessionId, state.SessionId);

        await FinishAllSections(c, state.AttemptId);
        var after = await GetState(c, state.AttemptId);

        Assert.Equal("Submitted", after.Status);
        Assert.Equal(c.SessionId, after.SessionId);
        Assert.Empty(after.Questions);                     // GR-11: nothing to re-read once it is over
    }

    [Fact]
    public async Task Final_result_is_404_for_another_learners_attempt()
    {
        var c = await SetUp();
        var state = await Start(c);
        await FinishAllSections(c, state.AttemptId);
        var (otherToken, _) = await VerifiedUser();

        Assert.Equal(HttpStatusCode.NotFound,
            (await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/final-result", otherToken)).StatusCode);
    }

    [Fact]
    public async Task Final_result_is_409_while_the_sitting_is_in_progress_and_does_not_end_it()
    {
        var c = await SetUp();
        var state = await Start(c);

        var res = await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/final-result", c.Token);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("InProgress", (await GetState(c, state.AttemptId)).Status);   // reading never finalises
    }

    [Fact]
    public async Task Final_result_rereads_a_finished_sitting_without_the_answer_key()
    {
        var c = await SetUp();
        var finished = await AnswerEverythingCorrectly(c, (await Start(c)).AttemptId);

        var res = await Authed(HttpMethod.Get, $"/api/attempts/{finished.AttemptId}/final-result", c.Token);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("correct", raw, StringComparison.OrdinalIgnoreCase);    // GR-11 (covers isCorrect)
        Assert.DoesNotContain("answerKey", raw, StringComparison.OrdinalIgnoreCase);
        var result = JsonSerializer.Deserialize<AttemptResultDto>(raw, Json)!;
        Assert.NotNull(result.TotalScaledScore);
        Assert.Equal(finished.TotalScaledScore, result.TotalScaledScore);
        Assert.Equal(finished.PredictedBand, result.PredictedBand);
        Assert.Equal(finished.Score, result.Score);
        Assert.True(result.SessionCompleted);
    }

    [Fact]
    public async Task Final_result_finalises_an_expired_sitting_on_read()
    {
        var c = await SetUp();
        var state = await Start(c);
        await ExpireAllSectionsAsync(state.AttemptId);

        var result = await AuthedGet<AttemptResultDto>($"/api/attempts/{state.AttemptId}/final-result", c.Token);

        Assert.True(result.AutoSubmitted);                // the deadline is enforced on this read too (GR-12)
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~FinalAssessmentTests"`
Expected: a BUILD failure. `StudentAssessmentDto` has no `OpenAttemptId`, and `AttemptStateDto` has no `SessionId`.

- [ ] **Step 3: Add the DTO fields and the interface method**

In `AssessmentContracts.cs`, `StudentAssessmentDto` gains a last field:

```csharp
public record StudentAssessmentDto(
    Guid Id, Guid? SessionId, string Kind, string Title,
    int QuestionCount, int PassThreshold,
    int? RetakeCap, int AttemptsUsed, bool CanAttempt,
    bool Passed, int? BestScore,
    bool ProctoringEnabled, int? TimeLimitMinutes,
    IReadOnlyList<StudentQuestionDto> Questions,
    bool HasTestAudio, int? TestAudioPlayLimit,
    Guid? OpenAttemptId);   // the caller's unsubmitted attempt, so a sitting is resumed, not restarted
```

In `FinalAssessmentContracts.cs`, `AttemptStateDto` gains a last field after `ServerNow`:

```csharp
    DateTimeOffset ServerNow,                        // so the client never positions audio by its own clock
    Guid? SessionId);                                // the owning session, for the result's way back
```

Add to `IFinalAssessmentService`, after `SubmitAsync`:

```csharp
    /// <summary>The result of a FINISHED sitting, read-only. Enforces elapsed deadlines like every
    /// other read; an attempt still in progress is a 409, never finalised here.</summary>
    Task<AttemptResultDto> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct = default);
```

- [ ] **Step 4: Fill `OpenAttemptId`**

In `AssessmentService.BuildStudentViewAsync`, after the `attempts` query:

```csharp
        var openAttemptId = await db.Attempts
            .Where(a => a.UserId == userId && a.AssessmentId == assessmentId && a.SubmittedAt == null)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);
```

Then pass `openAttemptId` as the new last constructor argument of `new StudentAssessmentDto(...)`, after the `TestAudioPlayLimit` argument.

- [ ] **Step 5: Thread the session id through `FinalAssessmentService`**

`LoadOwnedAsync` already looks up the owning session. Return it rather than querying again:

```csharp
    private async Task<(Attempt Attempt, Guid? SessionId)> LoadOwnedAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.Attempts.FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct)
            ?? throw new AssessmentException("Percobaan tidak ditemukan.", 404);

        var sessionId = await db.ProgramSessions
            .Where(s => s.AssessmentId == attempt.AssessmentId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        if (sessionId is Guid sid) await access.EnsureAccessAsync(userId, sid, ct);

        return (attempt, sessionId);
    }
```

Keep its existing `<summary>`.

Update the call sites:
- `GetStateAsync`, `SaveAnswersAsync`, `AdvanceSectionAsync` and `SubmitAsync` use `var (attempt, sessionId) = await LoadOwnedAsync(userId, attemptId, ct);`.
- `GetAudioUrlAsync` and `StartSectionAudioAsync` use `var (attempt, _) = ...`.

Change both builders to take the id:
- `BuildStateAsync(Attempt attempt, Guid? sessionId, CancellationToken ct)`: pass `SessionId: sessionId` as the last argument of `new AttemptStateDto(...)`.
- `BuildResultAsync(Attempt attempt, Guid? sessionId, CancellationToken ct)`: delete its own `var sessionId = await db.ProgramSessions…` query and use the parameter.

Every `BuildStateAsync(attempt, ct)` call becomes `BuildStateAsync(attempt, sessionId, ct)`, and `BuildResultAsync(attempt, ct)` becomes `BuildResultAsync(attempt, sessionId, ct)`.

If any other caller of `BuildStateAsync` or `BuildResultAsync` exists, check with `grep -n "BuildStateAsync\|BuildResultAsync\|LoadOwnedAsync" backend/src/Infrastructure/Assessments/FinalAssessmentService.cs`. Such a caller has its attempt from `LoadOwnedAsync` in scope, so pass that `sessionId`.

- [ ] **Step 6: Implement `GetResultAsync`**

Add it after `SubmitAsync`:

```csharp
    public async Task<AttemptResultDto> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct = default)
    {
        var (attempt, sessionId) = await LoadOwnedAsync(userId, attemptId, ct);
        await EnforceDeadlinesAsync(attempt, ct);

        if (attempt.SubmittedAt is null)
            throw new AssessmentException("Tes ini belum selesai.", 409);

        return await BuildResultAsync(attempt, sessionId, ct);
    }
```

- [ ] **Step 7: Map the endpoint**

In `FinalAssessmentEndpoints.cs`, after the `/{id:guid}/finish` mapping:

```csharp
        // The result of a FINISHED sitting, read-only — what /app/exam/{id} shows after a reload.
        // An attempt still in progress is a 409; only /finish ends a sitting early.
        g.MapGet("/{id:guid}/final-result", async Task<Ok<AttemptResultDto>> (
                Guid id, ClaimsPrincipal u, IFinalAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetResultAsync(u.UserId(), id, ct)));
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~FinalAssessmentTests"`
Expected: PASS, the 6 new tests plus every existing one.

Then run the whole suite: `dotnet test backend/Academy.slnx`.
Expected: all green. `TreatWarningsAsErrors` means 0 warnings.

- [ ] **Step 9: Regenerate the client**

```bash
dotnet build backend/Academy.slnx
cd backend/src/Api
ASPNETCORE_ENVIRONMENT=Development RunMigrations=false SeedSampleData=false \
  dotnet run --no-build --urls http://localhost:8091 > /tmp/api-openapi.log 2>&1 &
until curl -sf http://localhost:8091/openapi/v1.json -o /tmp/openapi.json; do sleep 2; done
pkill -f "urls http://localhost:8091"
cd ../../../frontend
npx openapi-typescript /tmp/openapi.json -o api-client/schema.ts
grep -c "openAttemptId\|final-result" api-client/schema.ts
npm run build
```

Expected:
- the grep count is at least 2;
- the build succeeds, since both new fields are optional additions and nothing reads them yet.

- [ ] **Step 10: Commit**

```bash
git add backend/src backend/tests frontend/api-client/schema.ts
git commit -m "feat: open attempt on the final intro, session on attempt state, read-only final result

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Frontend — extract the sitting and the result (no behaviour change)

**Files:**
- Create: `frontend/components/exam/Sitting.tsx`
- Create: `frontend/components/exam/ExamResult.tsx`
- Modify: `frontend/app/app/assessment/[id]/page.tsx`

**Interfaces:**
- Consumes: none.
- Produces:
  - `export function Sitting(props: { token: string; state: AttemptState; answers: Record<string, number>; remaining: number; busy: boolean; error: string | null; onAnswer: (qid: string, choice: number) => void; onSave: () => void; onNext: () => void })`;
  - `export function WarningOverlay(props: { strikes: number; limit: number; onClose: () => void })`;
  - `export function ExamResult(props: { result: AttemptResult | null; sessionId?: string | null })`.

This is a pure move, so the page behaves exactly as before afterwards.

- [ ] **Step 1: Create `components/exam/Sitting.tsx`**

Start the file with:

```tsx
"use client";

import { useEffect, useRef, useState } from "react";
import { Button, AlertTriangleIcon } from "@/components/ui";
import { LiveAudioPlayer } from "@/components/learn/LiveAudioPlayer";
import { getAudioUrl, startSectionAudio, clock, num, type AttemptState } from "@/lib/sessions";
```

Drop any import the moved code does not use; lint will name them.

Then move, **verbatim**, these three functions from `app/app/assessment/[id]/page.tsx`:
- `Sitting` (starts at `function Sitting({`, ~line 219), exported;
- `AudioButton` (~line 339), not exported;
- `WarningOverlay` (~line 372), exported.

Prefix `Sitting` and `WarningOverlay` with `export`. Do not change their bodies.

- [ ] **Step 2: Create `components/exam/ExamResult.tsx`**

Move `Result` (~line 389 to the end of the file) here, renamed to `ExamResult`, with these imports:

```tsx
import Link from "next/link";
import { num, type AttemptResult } from "@/lib/sessions";
```

Its signature becomes:

```tsx
export function ExamResult({ result, sessionId }: { result: AttemptResult | null; sessionId?: string | null }) {
```

Render the "Kembali ke sesi" `<Link>` only when `sessionId` is truthy (`{sessionId && (<Link …>Kembali ke sesi</Link>)}`). Everything else, including the GR-14 note, is unchanged.

The file has no hooks, so it needs no `"use client"`.

- [ ] **Step 3: Point the page at the moved code**

In `app/app/assessment/[id]/page.tsx`:
- delete the moved functions;
- import `Sitting` and `WarningOverlay` from `@/components/exam/Sitting`, and `ExamResult` from `@/components/exam/ExamResult`;
- render `<ExamResult result={result} sessionId={sessionId} />` where it rendered `<Result … />`;
- remove the imports that are no longer used: `LiveAudioPlayer`, `getAudioUrl`, `startSectionAudio`, `clock`. Keep `AlertTriangleIcon` and `CheckIcon`, which `Intro` uses. Lint names anything else left over.

- [ ] **Step 4: Verify**

Run: `cd frontend && npm run lint && npm run build`
Expected: no lint errors; the build lists `/app/assessment/[id]`.

`wc -l "app/app/assessment/[id]/page.tsx"` should now be about 220 lines.

- [ ] **Step 5: Commit**

```bash
git add frontend/components/exam frontend/app/app/assessment
git commit -m "refactor: move the final exam sitting and result into components/exam

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Frontend — `/app/exam/{attemptId}` and the intro redirect

**Files:**
- Modify: `frontend/lib/sessions.ts` (M4 block, after `finishAttempt`)
- Create: `frontend/app/app/exam/[attemptId]/page.tsx`
- Modify: `frontend/app/app/assessment/[id]/page.tsx`

**Interfaces:**
- Consumes:
  - Task 1's `openAttemptId`, `sessionId` and `/final-result`;
  - Task 2's `Sitting`, `WarningOverlay` and `ExamResult`.
- Produces:
  - `getAttemptStateOrNull(t: string, attemptId: string): Promise<AttemptState | null>`;
  - `getFinalResult(t: string, attemptId: string): Promise<AttemptResult>`.

- [ ] **Step 1: API helpers**

In `lib/sessions.ts`, after `finishAttempt`:

```ts
/** A sitting's state, or null when it is not the caller's or does not exist — the server answers
 *  both with 404, so the page cannot tell them apart either. */
export async function getAttemptStateOrNull(t: string, attemptId: string): Promise<AttemptState | null> {
  const res = await apiFetch(`${API}/api/attempts/${attemptId}/state`, { cache: "no-store" }, t);
  if (res.status === 404) return null;
  if (!res.ok) throw await problem(res, "Gagal memuat tes.");
  return res.json();
}

/** The result of a FINISHED sitting, read-only (409 while it is still running). */
export const getFinalResult = (t: string, attemptId: string) =>
  api<AttemptResult>("GET", `/api/attempts/${attemptId}/final-result`, t);
```

- [ ] **Step 2: The attempt page**

Create `app/app/exam/[attemptId]/page.tsx`. The sitting logic is the old `Runner`'s: `applyState`, the countdown, the expiry advance, `next`, `onProctor` and `onSave`. The changes are:
- it starts from the server's state instead of an intro;
- "running" is derived from `state.status`;
- a submitted state fetches its result.

```tsx
"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { notFound, useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Spinner, ErrorState } from "@/components/ui";
import { ProctorWatcher } from "@/components/learn/ProctorWatcher";
import { Sitting, WarningOverlay } from "@/components/exam/Sitting";
import { ExamResult } from "@/components/exam/ExamResult";
import {
  getAttemptStateOrNull, getAttemptState, getFinalResult, saveSectionAnswers, advanceSection,
  finishAttempt, num, type AttemptState, type AttemptResult, type ProctorState,
} from "@/lib/sessions";

const fullSpinner = <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;

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
  const [result, setResult] = useState<AttemptResult | null>(null);
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [warning, setWarning] = useState<ProctorState | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [remaining, setRemaining] = useState(0);

  // Loaded once and never cached: a cached state would resume a section with a stale clock.
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

  // Local countdown for display only — the server is the authority and is re-checked on every call.
  useEffect(() => {
    if (!running || remaining <= 0) return;
    const t = setInterval(() => setRemaining((r) => Math.max(0, r - 1)), 1000);
    return () => clearInterval(t);
  }, [running, remaining]);

  // When the local clock hits zero, ask the server what actually happened.
  useEffect(() => {
    if (!running || remaining > 0) return;
    void (async () => {
      try {
        applyState(await advanceSection(token, attemptId, answers));
      } catch { /* the next poll corrects it */ }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remaining, running, attemptId]);

  // However the sitting ended — reload, timer, last section, proctoring — show its result once.
  useEffect(() => {
    if (state?.status !== "Submitted" || result) return;
    getFinalResult(token, attemptId).then(setResult).catch(() => { /* ExamResult shows "sedang diproses" */ });
  }, [state?.status, result, token, attemptId]);

  async function next() {
    if (!state) return;
    setBusy(true); setError(null);
    try {
      const s = await advanceSection(token, attemptId, answers);
      if (s.status === "Submitted") setResult(await finishAttempt(token, attemptId));
      applyState(s);
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
        setResult(await finishAttempt(token, attemptId));
        applyState(await getAttemptState(token, attemptId));
      } catch { /* state refresh will catch up */ }
    }
  }

  if (load.isPending) return fullSpinner;
  if (load.isError) {
    return (
      <div className="mx-auto max-w-md px-6 py-20">
        <ErrorState
          title="Tes tidak tersedia"
          message={load.error instanceof Error ? load.error.message : "Gagal memuat tes."}
          action={<Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Ke dasbor</Link>}
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
        ) : (
          <ExamResult result={result} sessionId={state.sessionId ?? null} />
        )}
      </div>
    </div>
  );
}
```

**Before writing:** compare this body with `Runner`'s `next`, `onProctor`, `onSave` and effects, which are still in `app/app/assessment/[id]/page.tsx` at this point (Step 3 removes them). If they differ in anything other than the three changes listed above, keep the old behaviour and note it in the report.

- [ ] **Step 3: Cut the intro down to the intro**

Rewrite `app/app/assessment/[id]/page.tsx` so it only shows the intro and hands off. Keep:
- the existing `AssessmentPage` auth wrapper;
- the `Intro` component, unchanged.

Replace `Runner` with:

```tsx
function IntroPage({ token, sessionId }: { token: string; sessionId: string }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Never served from cache: a stale openAttemptId would bounce a learner into a finished sitting.
  const meta = useQuery({
    queryKey: ["assessment-meta", sessionId],
    queryFn: () => getSessionAssessment(token, sessionId),
    retry: false,
    gcTime: 0,
  });
  const openAttemptId = meta.data?.openAttemptId;

  // A sitting already under way is resumed, never restarted from the intro.
  useEffect(() => {
    if (openAttemptId) router.replace(`/app/exam/${openAttemptId}`);
  }, [openAttemptId, router]);

  async function begin() {
    if (!meta.data) return;
    setBusy(true); setError(null);
    try {
      const attempt = await startAttempt(token, meta.data.id);
      router.replace(`/app/exam/${attempt.id}`);       // busy stays on while the page changes
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal memulai tes.");
      setBusy(false);
    }
  }

  if (meta.isPending || openAttemptId) {
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
      <div className="mx-auto max-w-3xl px-6 py-8">
        <Intro test={meta.data} onBegin={begin} busy={busy} error={error} />
      </div>
    </div>
  );
}
```

`AssessmentPage` renders `<IntroPage token={accessToken} sessionId={sessionId} />`.

Delete everything the intro no longer uses:
- the `Phase` type;
- the sitting state and effects;
- the `Sitting`, `WarningOverlay`, `ExamResult` and `ProctorWatcher` imports;
- the unused `lib/sessions` imports.

Lint will list any left over.

- [ ] **Step 4: Verify**

Run: `cd frontend && npm run lint && npm run build`
Expected: no lint errors. The route list includes `/app/exam/[attemptId]` and `/app/assessment/[id]`.

`grep -rn "app/assessment/" app components lib` should still show only the three existing links: `app/app/program/[id]/page.tsx`, `components/learn/SessionView.tsx` and `components/dashboard/ProgressHeader.tsx`, plus the page's own login `next`.

- [ ] **Step 5: Commit**

```bash
git add frontend/lib/sessions.ts frontend/app/app/exam frontend/app/app/assessment
git commit -m "feat: final exam runs at /app/exam/{attemptId}, resumes on reload, shows its result after

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Browser check (controller)**

Rebuild with `docker compose up -d --build api frontend`, then at `http://localhost:6300`:
1. On the final's intro, press "Mulai tes". The URL becomes `/app/exam/{id}`.
2. Reload mid-section. It is the same section and the clock carries on.
3. Go back to the intro URL. It redirects to the same attempt.
4. Finish. The result is shown; reload and the same result is shown.
5. Open the attempt URL as a different account. It is a 404.

---

### Task 4: Backend — the final's intro serves the count, not the paper

**Why:** `GET /api/sessions/{id}/assessment` returns every question of every section of a final to the intro, before any clock runs. The answer key is not included, but a learner can still read all three sections ahead of the sitting, which defeats per-section timing (GR-12). The intro uses only `questionCount`. Questions reach the sitting section by section through `/api/attempts/{id}/state`.

**Files:**
- Modify: `backend/src/Infrastructure/Assessments/AssessmentService.cs` (`BuildStudentViewAsync`)
- Test: `backend/tests/Integration.Tests/FinalAssessmentTests.cs`

**Interfaces:**
- Consumes: none.
- Produces: for a `Final`, `StudentAssessmentDto.Questions` is always empty, and `QuestionCount` is still the real count. Gating tests are unchanged.

- [ ] **Step 1: Write the failing test**

Add it next to `The_final_assessment_is_served_without_the_answer_key`:

```csharp
    [Fact]
    public async Task The_final_intro_gives_the_question_count_but_none_of_the_questions()
    {
        var c = await SetUp();

        var intro = await AuthedGet<StudentAssessmentDto>($"/api/sessions/{c.SessionId}/assessment", c.Token);

        Assert.Empty(intro.Questions);                                   // nothing to read before the clock runs
        Assert.Equal(c.Key.Values.Sum(k => k.Count), intro.QuestionCount);
    }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~The_final_intro_gives"`
Expected: FAIL, because `Questions` is not empty.

- [ ] **Step 3: Implement**

In `BuildStudentViewAsync`, replace the `questions` query with:

```csharp
        // A final's questions are served section by section once its clock runs (GR-12); its intro
        // gets the count, never the paper. A gating test is untimed and shows its questions up front.
        var isFinal = assessment.Kind == AssessmentKind.Final;
        // NOTE: `Correct` is deliberately absent from this projection — the answer key must not
        // leave the server (GR-11). StudentQuestionDto has no field for it either.
        List<StudentQuestionDto> questions = isFinal ? [] : await db.AssessmentQuestions
            .Where(q => q.AssessmentId == assessmentId)
            .OrderBy(q => q.OrderIndex)
            .Select(q => new StudentQuestionDto(
                q.QuestionId,
                q.Question.Section.ToString(),
                q.Question.Prompt,
                ParseChoices(q.Question.Choices),
                q.Question.PassageRef,
                q.Question.AudioRef != null))
            .ToListAsync(ct);
        var questionCount = isFinal
            ? await db.AssessmentQuestions.CountAsync(q => q.AssessmentId == assessmentId, ct)
            : questions.Count;
```

In the `new StudentAssessmentDto(...)` call, replace both uses of `questions.Count` with `questionCount`. Those are `QuestionCount` and the `PassThreshold` fallback. `questions` stays as the `Questions` argument.

Keep the projection exactly as it is today; only the `isFinal ? [] :` guard is new.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~FinalAssessmentTests|FullyQualifiedName~SessionGatingTests|FullyQualifiedName~TestAudioTests|FullyQualifiedName~SessionPartLearnerTests"`
Expected: PASS. The final tests pass, and the gating-test suites still see their questions.

Then run the full suite: `dotnet test backend/Academy.slnx`. Expected: green, with 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Infrastructure/Assessments/AssessmentService.cs backend/tests/Integration.Tests/FinalAssessmentTests.cs
git commit -m "fix: the final's intro no longer serves its questions before the clock runs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

No client regeneration is needed, because the DTO shape is unchanged.
