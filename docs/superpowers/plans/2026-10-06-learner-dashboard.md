# Learner Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn `/app/dashboard` into four blocks that show the learner where they are and how they score:
1. a progress header;
2. the final-exam predicted score;
3. the session-test history;
4. certificates.

**Architecture:**
- **New endpoint:** `GET /api/me/session-results` returns the caller's submitted session-test attempts, one row per attempt of a Test part.
- **Progress fields:** `StudentProgramDto` gains part progress for the next session.
- **Frontend:** the page is rebuilt as a two-column grid. The two charts use Recharts, and every colour is read from the CSS tokens at runtime.

**Tech Stack:** .NET 10 minimal APIs (TypedResults), EF Core, xUnit with Testcontainers, Next.js App Router, TanStack Query, Recharts (new), openapi-typescript client.

**Spec:** `docs/superpowers/specs/2026-09-03-learner-dashboard-design.md` (revised 2026-10-06)

## Global Constraints

- **Results route.** `GET /api/me/session-results` requires authentication. It is scoped to `user.UserId()` and **never** takes a `userId` parameter. It returns scores only: no answers, no question text, nothing named `correct`, `answerKey` or `isCorrect` (GR-11). It returns session-test (Gating) attempts only, never the final.
- **DTO:** `SessionAttemptDto(Guid AttemptId, Guid ProgramId, Guid SessionId, string SessionTitle, int SessionOrderIndex, Guid PartId, string PartTitle, int PartOrderIndex, DateTimeOffset SubmittedAt, int Score, int MaxScore, bool Passed)`, ordered by `SubmittedAt` ascending.
- **Unattached tests.** Attempts on a test no longer attached to any Test part are omitted.
- **Part progress.** `StudentProgramDto` gains trailing `int? NextSessionPartsDone, int? NextSessionPartCount`. They are null when there is no next session or it is not a Video session.
- **GR-14 copy.** Block 2 always shows, directly under the predicted total, `Skor prediksi INVERTA, bukan skor resmi TOEFL dari ETS.`
- **Layout.** On desktop, block 1 is full width, blocks 2 and 3 sit side by side, and block 4 is full width. Under 768px they stack in 1-2-3-4 order.
- **Empty states:**
  - block 2, no certificate: what the final exam is, that it unlocks after every session, that it takes 115 minutes, and how many sessions remain;
  - block 3, no attempts: a line saying session tests open the next part or session, plus the continue-CTA;
  - block 4, no certificates: the block is omitted.
- **Chart shape.** A test part with exactly one attempt renders as a single bar, never a one-point line.
- **Chart colours** come from CSS custom properties (`--color-primary`, `--color-success`, `--color-danger`, `--color-border`, `--color-ink-muted`), read at runtime. No hard-coded hex.
- **Phone width.** Both charts are readable at 375px, and each has a text alternative for screen readers.
- **Onboarding.** The `data-tour="overall-progress"` and `data-tour="nav-certificates"` anchors are kept, because the onboarding tour targets them.
- **Commits** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Backend checks:** `dotnet build backend/Academy.slnx` with 0 warnings and `dotnet test backend/Academy.slnx` green.
- **Frontend checks:** `cd frontend && npm run lint && npm run build` green.
- **Never** read, print or edit `.env`.

## File map

| File | Task | Responsibility |
|---|---|---|
| `backend/src/Application/Programs/StudentResultsContracts.cs` (new) | 1 | DTO + `IStudentResultsService` |
| `backend/src/Infrastructure/Programs/StudentResultsService.cs` (new), `DependencyInjection.cs` | 1 | Query |
| `backend/src/Api/Endpoints/ProgramEndpoints.cs` | 1 | Route |
| `backend/tests/Integration.Tests/StudentResultsTests.cs` (new) | 1 | Tests |
| `backend/src/Application/Programs/ProgramContracts.cs`, `backend/src/Infrastructure/Programs/ProgramService.cs` | 2 | Part progress |
| `backend/tests/Integration.Tests/StudentResultsTests.cs` | 2 | Tests |
| `frontend/package.json`, `api-client/schema.ts`, `lib/programs.ts`, `lib/useCssVar.ts` (new) | 3 | Dependency, client, colour hook |
| `frontend/components/dashboard/ProgressHeader.tsx`, `FinalResultCard.tsx` (new), `app/app/dashboard/page.tsx` | 3 | Layout + blocks 1–2 |
| `frontend/components/dashboard/SessionTestHistory.tsx`, `CertificateCards.tsx` (new), `app/app/dashboard/page.tsx` | 4 | Blocks 3–4 |

---

### Task 1: `GET /api/me/session-results`

**Files:**
- Create: `backend/src/Application/Programs/StudentResultsContracts.cs`
- Create: `backend/src/Infrastructure/Programs/StudentResultsService.cs`
- Modify: `backend/src/Infrastructure/DependencyInjection.cs` (register it as scoped, next to the other program services)
- Modify: `backend/src/Api/Endpoints/ProgramEndpoints.cs`
- Test: `backend/tests/Integration.Tests/StudentResultsTests.cs`

**Interfaces:**
- Produces:
  - `record SessionAttemptDto(...)`, as in Global Constraints;
  - `IStudentResultsService.ListSessionAttemptsAsync(Guid userId, CancellationToken ct = default) : Task<IReadOnlyList<SessionAttemptDto>>`;
  - the route `GET /api/me/session-results`.

- [ ] **Step 1: Write the failing tests**

Create `StudentResultsTests.cs` using `AuthApiFactory`.
- **Helpers:** copy the program, session and learner helpers from `SessionPartLearnerTests.cs` / `SessionPartAdminTests.cs`. These are: an admin token; creating a program with a Video session; PUT `/api/admin/sessions/{id}/parts` to compose parts; enrolling a learner; watching a lesson via PUT `.../parts/{part}/progress`; and taking a test via POST `/api/assessments/{aid}/attempts` then POST `/api/attempts/{id}/submit`.
- **Fixture:** a program whose Session 1 has parts `[Lesson, Test A (2 questions), Test B (2 questions)]`.

Tests:
1. `Anonymous_is_refused`: GET `/api/me/session-results` with no token returns 401.
2. `A_learner_with_no_attempts_gets_an_empty_list`: returns 200 with `[]`.
3. `Each_learner_sees_only_their_own_attempts`:
   - Learners X and Y each watch the lesson, then submit Test A once.
   - Each one's list has exactly one row, and that row's `AttemptId` is the attempt they made.
4. `Rows_carry_the_test_part_and_are_ordered_by_submission`:
   - Learner X fails Test A, passes Test A, then passes Test B.
   - That gives 3 rows in submission order.
   - Rows 1–2 have Test A's `PartId` and `PartTitle`; row 3 has Test B's.
   - All rows have Session 1's `SessionId`, `SessionTitle`, `SessionOrderIndex` and the program's `ProgramId`.
   - `Score` and `MaxScore` match, with `Passed` false then true.
5. `No_answer_key_reaches_the_client`: the raw JSON of the response doesn't contain `correct`, `answerKey` or `isCorrect` (case-insensitive).
6. `An_attempt_on_a_detached_test_is_omitted`:
   - X submits Test B.
   - The admin then replaces Test B's assessment with a new one via PUT parts, if allowed. Alternatively, remove the part directly through `db.SessionParts`. Removing it through the API is refused while the part has attempts, so use the db for the test.
   - X's list no longer includes the Test B attempt.
7. `The_final_assessment_is_never_listed`: insert directly via db a submitted `Attempt` on a `Kind = Final` assessment owned by a FinalAssessment session for learner X. It doesn't appear.

Run: `dotnet test backend/tests/Integration.Tests --filter StudentResultsTests`. Expected: compile failure or 404s.

- [ ] **Step 2: Implement**

`StudentResultsContracts.cs`:

```csharp
// Learner dashboard (spec 2026-09-03, revised 2026-10-06): the caller's own session-test results.
namespace Academy.Application.Programs;

/// <summary>One submitted attempt of a session test (a Test part). Scores only — never answers or
/// the key (GR-11), and never the final assessment, which the certificate represents.</summary>
public record SessionAttemptDto(
    Guid AttemptId, Guid ProgramId,
    Guid SessionId, string SessionTitle, int SessionOrderIndex,
    Guid PartId, string PartTitle, int PartOrderIndex,
    DateTimeOffset SubmittedAt, int Score, int MaxScore, bool Passed);

public interface IStudentResultsService
{
    /// <summary>The caller's submitted session-test attempts, oldest first. The user is always the
    /// caller — there is deliberately no way to ask for someone else's.</summary>
    Task<IReadOnlyList<SessionAttemptDto>> ListSessionAttemptsAsync(Guid userId, CancellationToken ct = default);
}
```

`StudentResultsService.cs`:

```csharp
using Academy.Application.Programs;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

public class StudentResultsService(AppDbContext db) : IStudentResultsService
{
    public async Task<IReadOnlyList<SessionAttemptDto>> ListSessionAttemptsAsync(Guid userId, CancellationToken ct = default)
        // Inner join on the owning Test part: an attempt on a test no longer attached to any part
        // has no row and is omitted (spec §4.1). Gating only — the final is the certificate's.
        => await (
            from a in db.Attempts
            where a.UserId == userId && a.SubmittedAt != null && a.Assessment.Kind == AssessmentKind.Gating
            join p in db.SessionParts on a.AssessmentId equals p.AssessmentId
            orderby a.SubmittedAt
            select new SessionAttemptDto(
                a.Id, p.Session.ProgramId,
                p.SessionId, p.Session.Title, p.Session.OrderIndex,
                p.Id, p.Title, p.OrderIndex,
                a.SubmittedAt!.Value, a.TotalScore, a.MaxScore, a.Passed))
            .ToListAsync(ct);
}
```

`SessionParts.AssessmentId` is `Guid?` while `Attempt.AssessmentId` is `Guid`. If the join's key types don't match, compare `(Guid?)a.AssessmentId equals p.AssessmentId`.

Route, in `ProgramEndpoints.cs` next to `/api/me/enrollments`:

```csharp
        // The caller's own session-test scores for the dashboard (GR-11: scores only).
        app.MapGet("/api/me/session-results", async Task<Ok<IReadOnlyList<SessionAttemptDto>>> (
                ClaimsPrincipal u, IStudentResultsService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListSessionAttemptsAsync(u.UserId(), ct)))
            .RequireAuthorization()
            .WithTags("Programs");
```

Register it with `services.AddScoped<IStudentResultsService, StudentResultsService>();`.

- [ ] **Step 3: Run and commit**

Run: the new tests, then the 0-warning build and the full suite. Expected: all pass.

```bash
git add backend
git commit -m "feat: learners can read their own session-test results

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Part progress in the programme view

**Files:**
- Modify: `backend/src/Application/Programs/ProgramContracts.cs` (`StudentProgramDto`)
- Modify: `backend/src/Infrastructure/Programs/ProgramService.cs` (`GetForStudentAsync`; inject `SessionPartStates`)
- Test: `backend/tests/Integration.Tests/StudentResultsTests.cs`

**Interfaces:**
- Consumes: the existing `SessionPartStates.LoadAsync(userId, sessionId, ct)`, which returns `IReadOnlyList<PartState>`. Each `PartState` carries `Status` (`PartStatus.Done`).
- Produces: `StudentProgramDto` with trailing `int? NextSessionPartsDone, int? NextSessionPartCount`.

- [ ] **Step 1: Write the failing tests** (add to `StudentResultsTests`)

1. `Program_view_reports_part_progress_for_the_next_video_session`: with Session 1 parts `[Lesson, Test A, Test B]` and the learner having watched the lesson, GET `/api/me/programs/{id}` returns `NextSessionId == Session1`, `NextSessionPartsDone == 1` and `NextSessionPartCount == 3`.
2. `Part_progress_is_null_when_the_next_session_is_not_video`: a program whose first session is a Live session, scheduled in the future and not yet attended, gives `NextSessionId` pointing at it, with both fields null.

Run the filter. Expected: compile failure.

- [ ] **Step 2: Implement**

Append `int? NextSessionPartsDone, int? NextSessionPartCount` to `StudentProgramDto`. Add `SessionPartStates partStates` to `ProgramService`'s primary constructor. Before the `return`:

```csharp
        // Where the learner is inside the session they will continue (spec §4.2). One reader call.
        int? partsDone = null, partCount = null;
        if (nextSessionId is Guid nextId && rows.First(r => r.Id == nextId).Type == SessionType.Video)
        {
            var states = await partStates.LoadAsync(userId, nextId, ct);
            partsDone = states.Count(st => st.Status == PartStatus.Done);
            partCount = states.Count;
        }
```

Pass `partsDone, partCount` as the last two constructor arguments. Add `using Academy.Domain;` for `PartStatus` if it's needed. Fix any other `new StudentProgramDto(` call (`grep -rn "new StudentProgramDto" backend`).

- [ ] **Step 3: Run and commit**

Run: the tests, then the 0-warning build and the full suite. Expected: all pass.

```bash
git add backend
git commit -m "feat: programme view says how far the learner is inside the next session

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Recharts, layout, progress header and final result

**Files:**
- Modify: `frontend/package.json` and the lockfile (`npm install recharts`)
- Modify: `frontend/api-client/schema.ts` (regenerated)
- Modify: `frontend/lib/programs.ts`
- Create: `frontend/lib/useCssVar.ts`
- Create: `frontend/components/dashboard/ProgressHeader.tsx`, `frontend/components/dashboard/FinalResultCard.tsx`
- Modify: `frontend/app/app/dashboard/page.tsx`

**Interfaces:**
- Consumes: Tasks 1–2's routes and fields.
- Produces:
  - `listMySessionResults(t) : Promise<SessionAttempt[]>`;
  - `useCssVar(name: string, fallback?: string) : string`;
  - `<ProgressHeader token program />`;
  - `<FinalResultCard certificate sessionsRemaining />`.

- [ ] **Step 1: Dependency and client**

```bash
cd frontend && npm install recharts
```

Regenerate the client:

```bash
dotnet build backend/Academy.slnx
cd backend/src/Api
ASPNETCORE_ENVIRONMENT=Development RunMigrations=false SeedSampleData=false \
  dotnet run --no-build --urls http://localhost:8091 > /tmp/api-openapi.log 2>&1 &
until curl -sf http://localhost:8091/openapi/v1.json -o /tmp/openapi.json; do sleep 2; done
pkill -f "urls http://localhost:8091"
cd ../../../frontend
npx openapi-typescript /tmp/openapi.json -o api-client/schema.ts
grep -c "SessionAttemptDto\|nextSessionPartsDone" api-client/schema.ts
```

Expected: the count is at least 2.

`lib/programs.ts`, using its existing `api` helper:

```ts
export type SessionAttempt = components["schemas"]["SessionAttemptDto"];
export const listMySessionResults = (t: string) =>
  api<SessionAttempt[]>("GET", "/api/me/session-results", t);
```

`lib/useCssVar.ts`:

```ts
"use client";
import { useEffect, useState } from "react";

/** A design token's resolved value, for libraries (Recharts) that set SVG attributes, where
 *  `var(--x)` is not resolved. Read after mount so SSR and the first paint use the fallback. */
export function useCssVar(name: string, fallback = "#7F00FF"): string {
  const [value, setValue] = useState(fallback);
  useEffect(() => {
    const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    if (v) setValue(v);
  }, [name]);
  return value;
}
```

- [ ] **Step 2: ProgressHeader (block 1)**

`components/dashboard/ProgressHeader.tsx` takes `{ program: StudentProgram; firstName: string }` and has `data-tour="overall-progress"` on its root. It contains:
- **Greeting:** `Halo, {firstName} 👋` and `Lanjutkan persiapan TOEFL Anda.`
- **Ring:** an SVG progress ring (two circles with `stroke-dasharray`; the colours come from Tailwind classes `stroke-primary`/`stroke-surface-2`, so no Recharts is needed here) showing `{completed}/{total}` and `sesi selesai`. The SVG has `role="img"` and `aria-label="{completed} dari {total} sesi selesai"`.
- **Next session,** when `nextSessionId` is set:
  - the title, and a line `Sesi {orderIndex} · bagian {done+1} dari {count}` when both part fields are non-null (otherwise `Sesi {orderIndex}`);
  - the existing `Buka →` CTA. Move the current `nextHref` helper into this file.
- **All done:** `Semua sesi selesai. Sertifikat Anda tersedia di halaman Sertifikat.`, styled as today.

- [ ] **Step 3: FinalResultCard (block 2)**

`components/dashboard/FinalResultCard.tsx` takes `{ certificate?: ProgramCertificate; sessionsRemaining: number }`.

**With a certificate:**
- a title `Hasil tes akhir`;
- the large predicted total `{totalScore}` and the band `{predictedBand}` when set;
- directly beneath them, always: `Skor prediksi INVERTA, bukan skor resmi TOEFL dari ETS.` (muted, small);
- a Recharts horizontal bar chart:
  - `ResponsiveContainer` width 100%, height 160, `BarChart layout="vertical"`;
  - data `[{section:"Listening"…},{section:"Structure"…},{section:"Reading"…}]` from `certificate.sectionScores`;
  - `XAxis type="number" domain={[31, 68]}`, `YAxis type="category" dataKey="section" width={80}`;
  - a `Bar` with `fill={useCssVar("--color-primary")}` and a value `LabelList`;
  - axis tick colour from `useCssVar("--color-ink-muted", "#51607a")`;
- a visually hidden list (`sr-only`) giving each section's score as text;
- a link `Lihat sertifikat →` to `/app/certificates`.

**Without a certificate (the empty state):**
- `Tes akhir`;
- `Simulasi TOEFL ITP (Listening, Structure, Reading) dengan durasi 115 menit. Terbuka setelah semua sesi selesai.`;
- `Sisa {sessionsRemaining} sesi lagi.` when there is more than one session left; otherwise `Tinggal tes akhir.`

- [ ] **Step 4: Page layout**

In `app/app/dashboard/page.tsx`:
- Keep the auth redirect, `AppHeader`, `OnboardingFlow`, the enrollment loading, error and empty states, and the pending-payment notice.
- Widen the container to `max-w-5xl`.
- For the **first** active enrollment (`ponytail: one programme exists; per-programme sections when a second one ships`), fetch `getStudentProgram` and `listMyCertificates`, then render:

```tsx
<div className="grid grid-cols-1 gap-5 md:grid-cols-2">
  <div className="md:col-span-2"><ProgressHeader program={program} firstName={firstName} /></div>
  <FinalResultCard certificate={certFor(program.programId)} sessionsRemaining={total - completed} />
  {/* Task 4: <SessionTestHistory … /> */}
  {/* Task 4: <div className="md:col-span-2"><CertificateCards … /></div> */}
</div>
```

`certFor` picks the newest certificate whose `programId` matches. Remove the old `ProgramCard`, `SessionLine` and the greeting block, which ProgressHeader now owns. Keep `CertificatesTeaser` until Task 4 replaces it, so `data-tour="nav-certificates"` still exists.

- [ ] **Step 5: Lint, build, commit**

Run: `cd frontend && npm run lint && npm run build`. Expected: both pass.

```bash
git add frontend
git commit -m "feat: dashboard progress header and final-exam result with Recharts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Session-test history and certificate cards

**Files:**
- Create: `frontend/components/dashboard/SessionTestHistory.tsx`, `frontend/components/dashboard/CertificateCards.tsx`
- Modify: `frontend/app/app/dashboard/page.tsx`

**Interfaces:**
- Consumes:
  - Task 3's `listMySessionResults`, `useCssVar` and the grid;
  - `listMyCertificates`, `ProgramCertificate` from `lib/sessions.ts`.

- [ ] **Step 1: SessionTestHistory (block 3)**

`components/dashboard/SessionTestHistory.tsx` takes `{ token: string; programId: string; continueHref: string | null }`.
- **Data:** `useQuery(["my-session-results"], listMySessionResults)`, filtered to `programId`.
- **Grouping:** group by `partId`, and order the groups by `(sessionOrderIndex, partOrderIndex)`.
- **Title:** `Riwayat tes sesi`.
- **One small chart per group,** with a label `Sesi {sessionOrderIndex} · {partTitle}` and a Recharts `BarChart`:
  - `ResponsiveContainer` height 110;
  - data `attempts.map((a, i) => ({ label: \`#${i + 1}\`, pct: Math.round(a.score / a.maxScore * 100), score: a.score, max: a.maxScore, passed: a.passed }))`;
  - `XAxis dataKey="label"`, `YAxis domain={[0, 100]} hide`;
  - one `<Bar dataKey="pct">` with a `<Cell>` per bar, filled with the success colour when `passed` and the primary colour otherwise (via `useCssVar`);
  - a `Tooltip` showing `{score}/{max}`.

  A group with one attempt naturally renders one bar (Global Constraints). Under the chart: `Lulus` or `Belum lulus` from the last attempt, plus `{n} percobaan`.
- **Text alternative:** a visually hidden `<ul className="sr-only">` per group, with items like `Percobaan 1: 1 dari 2, belum lulus`.
- **Empty state** (no rows): `Tes sesi membuka bagian atau sesi berikutnya. Hasilnya akan tampil di sini.`, plus a `Mulai belajar →` link to `continueHref` when set.

- [ ] **Step 2: CertificateCards (block 4)**

`components/dashboard/CertificateCards.tsx` takes `{ certificates: ProgramCertificate[] }` and returns `null` when the list is empty, so the block is omitted. Otherwise it renders a root with `data-tour="nav-certificates"`, a title `Sertifikat`, and one card per certificate:
- the programme name;
- the predicted score (`{totalScore}` and `skor prediksi`);
- the issue date in `id-ID` long format;
- the verification code in monospace;
- the links `Verifikasi →` (`/verify/{verificationCode}`) and `Unduh →` (`/app/certificates`).

- [ ] **Step 3: Wire into the page**

Replace the two Task 3 placeholders:

```tsx
  <SessionTestHistory token={accessToken} programId={program.programId} continueHref={nextHrefOrNull} />
  <div className="md:col-span-2"><CertificateCards certificates={certsFor(program.programId)} /></div>
```

Remove `CertificatesTeaser`. `CertificateCards` now carries `data-tour="nav-certificates"`.

- [ ] **Step 4: Lint, build, commit**

Run: `cd frontend && npm run lint && npm run build`. Expected: both pass.

```bash
git add frontend
git commit -m "feat: dashboard session-test history and certificate cards

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: Browser check (controller)**

Load `/app/dashboard` at 375px and at desktop width:
- as a learner with test attempts and no certificate, check the empty block 2 and the charts in block 3;
- as a new learner with nothing, check the empty states and that block 4 is absent;
- check the onboarding tour still finds both anchors.

---

## Self-review notes

- **Spec §5's block 1 ring** is plain SVG rather than Recharts. A two-circle ring doesn't need a charting library; the two real charts (blocks 2 and 3) use Recharts, as the PO chose.
- **Multiple enrollments.** The page shows only the first active enrollment. Only one programme exists, and this is noted with a `ponytail:` comment.
- **Detached tests.** Task 1 test 6 removes the part directly in the db, because the admin API refuses to remove a part whose test has attempts (GR-7).
