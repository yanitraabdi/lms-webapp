# Final exam on a per-attempt route — design

**Date:** 2026-10-07 · **Status:** approved in brainstorming, awaiting spec review
**Sub-project 5 of 6** in the learner-dashboard spec §8.

## 1. Why

**Today.** The final exam runs at `/app/assessment/{sessionId}` (`frontend/app/app/assessment/[id]/page.tsx`, 482 lines), and the attempt lives only in React state.

**What goes wrong:**
- **Reload mid-exam.** The learner lands back on the intro and has to press "Mulai tes" again. `StartAttempt` reuses the open attempt, so no retake is burned, but the sitting is invisible in the URL.
- **Reload on the result screen.** It loses the result.

**What already holds on the backend:**
- every final-exam call is keyed by attempt (`/api/attempts/{id}/…`);
- another learner's attempt is a **404** (`FinalAssessmentService.LoadOwnedAsync`, `ProctorService`);
- `GET …/state` enforces deadlines on every read (GR-12);
- `GET …/state` serialises no questions once the attempt is submitted (GR-11).

This sub-project is therefore mostly frontend.

**Decision (PO, 2026-10-07):** once a sitting ends, its URL shows the **result, read-only**. The questions are never shown again. The URL is not a 404 and does not redirect.

## 2. Routes

| Route | Role |
|---|---|
| `/app/assessment/{sessionId}` | **Intro** (unchanged URL — the dashboard, `SessionView` and the programme page link here). Shows the rules, the proctoring notice and the GR-14 note. |
| `/app/exam/{attemptId}` | **The sitting**, then its **result**. New. |

**Intro behaviour:**
- If `OpenAttemptId` is set on the assessment, the intro immediately calls `router.replace("/app/exam/{OpenAttemptId}")`.
- Otherwise "Mulai tes" calls `POST /api/assessments/{id}/attempts` (it already reuses an open attempt), then `router.replace("/app/exam/{attempt.id}")`.
- `replace`, not `push`, so the back button does not return to an intro that would immediately bounce the learner forward again.

**Attempt page behaviour.** It loads `GET /api/attempts/{attemptId}/state`, then:
- **`InProgress`** → the sitting, resumed at the current section with the server's `SecondsRemaining`.
- **`Submitted`** (finished, timed out, or auto-submitted by proctoring) → `GET /api/attempts/{attemptId}/final-result`, rendered as the read-only result.
- **404** (not yours, or unknown) → `notFound()`. The page must not distinguish "exists but not yours" from "does not exist".
- **Any other error** (e.g. a revoked enrollment, 403) → the existing `ErrorState` with a link to the dashboard.

**When the sitting ends in the tab** (last section advanced, timer expiry, proctor auto-submit), the page switches to the result in place. The URL stays the same, so a reload shows the same result.

## 3. Backend

All changes are additive, and the endpoints use TypedResults.

### 3.1 `StudentAssessmentDto.OpenAttemptId`
- Add `Guid? OpenAttemptId` as the last field. It is the caller's attempt on this assessment with `SubmittedAt == null`, or null.
- Gating tests get the field too. It is harmless there and unused.

### 3.2 `AttemptStateDto.SessionId`
- Add `Guid SessionId`, the session that owns the attempt's assessment.
- `LoadOwnedAsync` already looks it up to re-check the gate; reuse that value rather than querying again.
- The result view uses it for "Kembali ke sesi".

### 3.3 `GET /api/attempts/{id}/final-result`
- **Contract:** `IFinalAssessmentService.GetResultAsync(userId, attemptId)` returns `AttemptResultDto`. It has the same shape `POST /finish` returns: section scores, `TotalScaledScore`, `PredictedBand`, `SessionCompleted`.
- **Loading:** `LoadOwnedAsync` (owner check, gate re-check; another learner's attempt is a 404), then `EnforceDeadlinesAsync`, so an expired attempt is finalised exactly as on any other read.
- **Errors:** still `InProgress` after that → **409** "Tes ini belum selesai.".
- **Side effects:** none beyond deadline enforcement. Never finalises an in-progress attempt (that stays `POST /finish`).
- **Rate limit:** none. It is a cheap read, like `state`.
- **Answer key:** none (GR-11). `AttemptResultDto` carries no answers.

## 4. Frontend

**Split the 482-line page:**
- `app/app/assessment/[id]/page.tsx` → **intro only**: meta query, the `OpenAttemptId` redirect, "Mulai tes".
- `components/exam/Sitting.tsx` → moved as-is: sections, countdown, autosave every 15s, audio, proctor watcher.
- `components/exam/ExamResult.tsx` → the existing `Result`, taking `AttemptResult` + `sessionId`.
- `app/app/exam/[attemptId]/page.tsx` → auth guard (`/login?next=/app/exam/{attemptId}`), state query, and the `InProgress` / `Submitted` / 404 switch.

**Constraints:**
- No behaviour change to the sitting: the timer, autosave, proctoring and audio stay as they are.
- The generated API client is regenerated, never hand-edited.
- Strings stay in Bahasa Indonesia.
- The GR-14 prediction note stays on the result.

## 5. Tests

**Backend (Integration.Tests):**
- `final-result` on another learner's attempt → 404.
- `final-result` on an in-progress attempt → 409.
- `final-result` after `finish` → band and scaled score present; the serialised body contains no `correct`, `answerKey` or `isCorrect`.
- `final-result` on an attempt past its deadline → 200 and `AutoSubmitted` (deadline enforced on read).
- `OpenAttemptId` → set while an attempt is open, null after it is submitted.
- `state` → carries `SessionId`; a submitted attempt's `state` has no questions.

**Frontend:**
- lint + build (`/app/exam/[attemptId]` builds).
- **Manual:**
  - start → URL becomes `/app/exam/{id}`;
  - reload mid-section → same section, clock continues;
  - finish → result;
  - reload → same result;
  - another account's attempt URL → 404;
  - the intro with an open attempt → redirects.

## 6. Out of scope

- Gating tests on video sessions; they already run per part inside `SessionView`.
- Changes to proctoring, scoring, retake caps or certificates.
- Listing past attempts. Certificates already do that at `/app/certificates`.
