# Attempt optimistic concurrency — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** two requests writing the same exam attempt at nearly the same moment never silently overwrite each other's changes. Today an autosave racing an advance, or two autosaves, can drop a learner's answers, because each request saves the whole JSON (`attempts.answers` and `attempts.state`) it read earlier: last writer wins.

**Architecture:**
- **The token:** PostgreSQL's `xmin` system column becomes a concurrency token on `Attempt`, configured as a shadow property so Domain stays free of EF. EF then adds `WHERE xmin = @read` to every attempt UPDATE, and throws `DbUpdateConcurrencyException` when the row changed after it was read.
- **The retry:** every service operation that writes an attempt runs inside a small retry. On a conflict it clears the change tracker and re-runs the whole operation on fresh data, at most 3 times; after that it surfaces 409 "Coba lagi". Each operation is already idempotent on fresh data:
  - answers merge;
  - advance carries its expected section index;
  - deadline enforcement re-derives from server timestamps;
  - finalisation is a conditional write;
  - audio counters re-check the limit.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql 10, xUnit + Testcontainers.

**Branch:** `fix/attempt-concurrency`.

## Global Constraints

- **GR-12:** deadlines are still enforced before any read or write. A retry repeats the enforcement; it never skips it.
- **GR-6/GR-7:** nothing is deleted. Certificates stay one per attempt (the existing unique index and conditional finalise are unchanged).
- **No user transactions** (`EnableRetryOnFailure` is on). The retry wraps the operation, not a transaction. Do not call `BeginTransaction`.
- **Migrations:** generated with `~/.dotnet/tools/dotnet-ef migrations add <Name> --project backend/src/Infrastructure --startup-project backend/src/Api`, never hand-edited.
- **Warnings:** `TreatWarningsAsErrors` is on.

---

### Task 1: `xmin` token on `Attempt`, and conflict-retry on every attempt write

**Files:**
- Modify: `backend/src/Infrastructure/Persistence/InvertaConfiguration.cs` (`AttemptConfig`, ~line 122)
- Create: `backend/src/Infrastructure/Persistence/ConcurrencyRetry.cs`
- Modify: `backend/src/Infrastructure/Assessments/FinalAssessmentService.cs` (public write operations)
- Modify: `backend/src/Infrastructure/Assessments/AssessmentService.cs` (`StartTestAudioAsync`, `StartAttemptAsync`, `SaveAnswersAsync`, `SubmitAsync`; delete the `ponytail:` comment at ~line 91 that names this exact fix)
- Modify: `backend/src/Infrastructure/Assessments/ProctorService.cs` (`ReportAsync`, `ReinstateAsync`)
- Create: one migration (generated)
- Test: `backend/tests/Integration.Tests/FinalAssessmentTests.cs`

- [ ] **Step 1: The failing test.** Add it to `FinalAssessmentTests.cs`, using the class helpers:

```csharp
    [Fact]
    public async Task Concurrent_answer_saves_on_one_attempt_lose_nothing()
    {
        var c = await SetUp();
        var state = await Start(c);
        var listening = c.Key[QuestionSection.Listening];   // (Id, Correct) per question

        // Each request saves ONE different answer; all of them must survive.
        await Task.WhenAll(listening.Select(q =>
            SaveAnswers(c, state.AttemptId, new Dictionary<string, int> { [q.Id.ToString()] = q.Correct })));

        var after = await GetState(c, state.AttemptId);
        Assert.Equal(listening.Count, after.Answers.Count);
        Assert.All(listening, q => Assert.Equal(q.Correct, after.Answers[q.Id.ToString()]));
    }
```

Run it: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~lose_nothing"`.

Expected: FAIL, because some answers are missing (lost updates). The race is probabilistic, so run it three times and record the results. If it passes every time before the fix, add a second, deterministic test:
1. Load the attempt in two `AppDbContext` scopes from `factory.Services`.
2. Modify `Answers` in both.
3. Save the first, then save the second.
4. Assert that the second throws `DbUpdateConcurrencyException` once the token exists.

Before the token exists, that second save succeeds and overwrites, which is red.

- [ ] **Step 2: The token.** In `AttemptConfig.Configure`:

```csharp
        // Optimistic concurrency on the attempt row (PostgreSQL's xmin): a request that read the
        // row before another request wrote it gets DbUpdateConcurrencyException instead of
        // silently overwriting answers/state. Callers retry via ConcurrencyRetry.
        e.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
```

Generate the migration (`AttemptXminConcurrency`) and read its `Up`. `xmin` is a system column, so the migration must not add a real column.
- If it comes out empty, or only touches the model snapshot, keep it.
- If it tries `AddColumn("xmin")`, stop and report, rather than editing it by hand. Check whether Npgsql needs the property named differently (the documented pattern is `.IsRowVersion()` on a `uint`), and say what you found.

Run `dotnet ef migrations script --idempotent` for the new migration and include the SQL in the report.

- [ ] **Step 3: The retry helper.** Create `ConcurrencyRetry.cs`:

```csharp
using Academy.Application.Assessments;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Persistence;

/// <summary>
/// Re-runs an operation that read-modify-writes a row with a concurrency token, on fresh data,
/// when another request wrote the row first. The operation must be safe to repeat from scratch:
/// it re-reads everything it needs (it is not handed tracked entities from outside).
/// </summary>
public static class ConcurrencyRetry
{
    public static async Task<T> RunAsync<T>(AppDbContext db, Func<Task<T>> operation, int attempts = 3)
    {
        for (var i = 1; ; i++)
        {
            try { return await operation(); }
            catch (DbUpdateConcurrencyException) when (i < attempts)
            {
                db.ChangeTracker.Clear();      // drop the stale tracked rows; the retry re-reads them
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                throw new AssessmentException("Tes sedang diperbarui dari permintaan lain. Coba lagi.", 409);
            }
        }
    }

    public static Task RunAsync(AppDbContext db, Func<Task> operation, int attempts = 3)
        => RunAsync(db, async () => { await operation(); return true; }, attempts);
}
```

Check that `AssessmentException(string, int)` lives where the `using` says. Adjust the namespace if it doesn't.

- [ ] **Step 4: Wrap every attempt-writing public operation.** Wrap each body in `ConcurrencyRetry.RunAsync(db, async () => { … })`, keeping the body as it is:
  - **`FinalAssessmentService`:** `GetStateAsync` (it can enforce deadlines, which is a write), `SaveAnswersAsync`, `AdvanceSectionAsync`, `SubmitAsync`, `GetResultAsync`, `GetAudioUrlAsync`, `StartSectionAudioAsync`.
  - **`AssessmentService`:** `StartTestAudioAsync`, `StartAttemptAsync`, `SaveAnswersAsync`, `SubmitAsync`.
  - **`ProctorService`:** `ReportAsync`, `ReinstateAsync`.

**Rules:**
- **No nesting.** A wrapped operation must not call another wrapped public operation of the same service. The inner retry's `ChangeTracker.Clear()` would detach the outer operation's tracked entities.
  - Where a wrapped method calls another public one, call a private unwrapped core instead. Split each public method into `public X() => ConcurrencyRetry.RunAsync(db, () => XCoreAsync(...))` plus `private XCoreAsync(...)`.
  - Known call chains:
    - `AdvanceSectionAsync` already calls the private `SaveCurrentSectionAnswersAsync`, which is fine.
    - `ProctorService.ReportAsync` → `finals.SubmitAsync`: a cross-service call, so it is wrapped on its own. That is acceptable only if `ReportAsync` does nothing with its tracked `attempt` after that call. It doesn't today: it returns a DTO built from values it already holds. Keep it that way, and note it in the code with one line.
- **Re-read inside the operation.** Each retried body must load everything it writes inside the lambda (all of these already start with `LoadOwnedAsync`, `LoadOwnedAttemptAsync` or `FirstOrDefaultAsync`). Do not hoist a load outside the retry.
- **No double side effects on retry.** On a conflict, a single `SaveChanges` commits nothing, so a retry cannot double-insert.
  - Operations with several `SaveChanges` calls (`ProctorService.ReportAsync`, which inserts the event and then possibly a decision; `EnforceDeadlinesAsync` followed by a later save) must not double-insert a `ProctorEvent` when a later save in the same operation conflicts.
  - Check this for `ReportAsync`. If its later save can conflict after the first insert committed, restructure so the conflict-prone attempt write (`ProctorFlagged = true`) happens in the first `SaveChanges` together with the event insert, or make the retry skip re-inserting. Report what you did.
  - `ProctorEvent` inserts themselves never conflict: new rows have no token.
- **The `FinalizeAsync` conditional `ExecuteUpdateAsync` stays as it is.** It bypasses the token by design. The `db.Entry(attempt).ReloadAsync` that follows refreshes `xmin` too.

- [ ] **Step 5: Run the tests.**
  - Run the Step 1 test(s) three times. All must PASS.
  - Run `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~FinalAssessmentTests|FullyQualifiedName~SessionGatingTests|FullyQualifiedName~TestAudioTests|FullyQualifiedName~GatingAudioTests|FullyQualifiedName~SessionPartLearnerTests"`.
  - Then run the full `dotnet test backend/Academy.slnx`, which must be green with 0 warnings.
  - The host's Docker is heavily loaded. A setup failure with "Connection reset by peer" is environmental: rerun, and note it.

- [ ] **Step 6: Commit.**

  ```text
  fix: attempts carry an xmin concurrency token; attempt writes retry on conflict instead of losing answers
  ```

  with the trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Outcome (2026-10-28)

**Done:** `7edb487` and `984b96f`. The review said ready to merge, and the fix round (an `xmin` guard on finalise, so an answer saved while the exam is being finished is either scored or refused, never lost) was re-reviewed clean.

**Deviations from this plan, accepted in review:**
- **The 50-way test now checks "200 or 409, never silently lost".** Three retries can't absorb 50 simultaneous writers. A separate 3-way test requires every save to succeed.
- **The migration's `AddColumn("xmin")` emits no SQL in either direction.** Npgsql skips system columns; the rollback script was checked.
- **`ProctorService.ReportAsync` is only partly wrapped.** The event insert stays outside the retry, so it is never recorded twice.

**Still open (non-blocking):**
- **A strike-2 report can end in 409 without ending the sitting.** That needs 3 straight conflicts. The next counted strike retries the auto-submit.
- **Two simultaneous starts can create two open attempts.** This was already possible. Fix: a partial unique index on `(user_id, assessment_id) WHERE submitted_at IS NULL`.
- **An exhausted retry on "Bagian berikutnya" shows "Coba lagi".** The learner clicks again.
