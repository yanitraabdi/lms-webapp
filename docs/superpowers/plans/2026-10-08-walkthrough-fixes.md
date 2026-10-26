# Walkthrough fixes (#1–#5) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** fix the five issues found in the 2026-10-08 browser walkthrough of the local stack.

**Architecture:** small, local changes. One backend addition (`AttemptResultDto.ScaledScores`); everything else is frontend.

**Tech Stack:** Next.js 15, Tailwind v4, Recharts, .NET 10, xUnit + Testcontainers, openapi-typescript client.

**Branch:** `fix/walkthrough-findings`.

## Global Constraints

- **Copy:** strings in Bahasa Indonesia.
- **GR-14:** score copy says "prediksi INVERTA", never an official ETS score.
- **GR-11:** no answer key in any student DTO.
- **API client:** regenerated, never hand-edited. Commands:

  ```bash
  dotnet build backend/Academy.slnx
  cd backend/src/Api
  ASPNETCORE_ENVIRONMENT=Development RunMigrations=false SeedSampleData=false dotnet run --no-build --urls http://localhost:8091 > /tmp/api-openapi.log 2>&1 &
  until curl -sf http://localhost:8091/openapi/v1.json -o /tmp/openapi.json; do sleep 2; done
  pkill -f "urls http://localhost:8091"
  cd ../../../frontend
  npx openapi-typescript /tmp/openapi.json -o api-client/schema.ts
  ```

  If `/tmp` is not writable, use a scratch directory.
- **Dormant AI-Academy code** (onboarding survey API, tables) stays in the backend. Do not delete it there; only stop the INVERTA frontend from showing it.
- **Verification:** `npm run lint` and `npm run build` in `frontend/`; `dotnet test backend/Academy.slnx` for backend changes, with 0 warnings.

---

### Task 1: Admin — a part's video can always be set by hand, and the edit dialog is as wide as it asks to be

**Walkthrough finding #1.**
- The video row in the session parts editor only offers `VideoPicker`. When the Bunny library is unavailable (dev provider, no `BUNNY_API_KEY`, Bunny unreachable), the picker shows a message saying "Isi ID video secara manual", but there is no field to type into. The current video ID is not shown either.
- The "Ubah sesi" dialog is also narrower than intended, which squeezes the part title input to about 100px.

**Root cause of the width:** `lib/cn.ts` only joins class names; it doesn't merge them. `components/ui/Modal.tsx:54` always adds `max-w-sm`, so a caller's `max-w-2xl` (`SessionForm.tsx:107`) competes with it, and `max-w-sm` wins in the generated CSS. Every `Modal` caller that passes a wider `max-w-*` is affected.

**Files:**
- Modify: `frontend/components/ui/Modal.tsx`
- Modify: `frontend/components/admin/SessionPartsEditor.tsx` (video-row branch, ~lines 220-232)

- [ ] **Step 1: Modal width.** Apply the default `max-w-sm` only when the caller passes no `max-w-*` class of its own:

  ```tsx
  className={cn("w-full rounded-base bg-surface p-5 shadow-lg", !/(^|\s)max-w-/.test(className ?? "") && "max-w-sm", className)}
  ```

  Keep any other classes on that element exactly as they are. Read the line first, and reproduce its current class list minus `max-w-sm`.

  Then list every `<Modal` caller (`grep -rn "<Modal" frontend/app frontend/components`). For each one that passes `className`, check that the result is the width it asked for, and report them.

- [ ] **Step 2: Manual video ID and duration.** In `SessionPartsEditor`, inside the video-row branch, render this under `<VideoPicker …/>` and above the duration label:

  ```tsx
  <div className="flex flex-wrap items-center gap-2">
    <label className="flex min-w-0 flex-1 items-center gap-2 text-[12px] text-ink-muted">
      ID video
      <input
        value={r.providerAssetId ?? ""}
        onChange={(e) => update(i, { providerAssetId: e.target.value.trim() || undefined })}
        placeholder="mis. 3f1c…"
        className={inputBlockCls + " min-w-0 flex-1 font-mono text-[12px]"}
      />
    </label>
    <label className="flex items-center gap-2 text-[12px] text-ink-muted">
      Durasi (menit)
      <input
        type="number"
        min={1}
        value={r.durationSeconds != null ? Math.round(r.durationSeconds / 60) : ""}
        onChange={(e) => update(i, { durationSeconds: e.target.value === "" ? undefined : Math.max(1, Number(e.target.value)) * 60 })}
        className={inputBlockCls + " w-20"}
      />
    </label>
  </div>
  ```

  - Picking from the library still fills both fields, because both read the same row state.
  - Use the row type's actual field types. If `durationSeconds` is `number | null` rather than optional, use `null` instead of `undefined`, and do the same for `providerAssetId`. Report which you used.
  - Remove the now-redundant `minutesLabel(r.durationSeconds)` span, since the number input shows the value. If `minutesLabel` becomes unused, delete it.
  - Do not change `VideoPicker` itself. Its doc comment already promises "The form's manual id field stays usable either way", and this field is that.

- [ ] **Step 3: Verify.** Run `cd frontend && npm run lint && npm run build`.

- [ ] **Step 4: Commit:**

  ```text
  fix: a session part's video can be set by hand, and modals honour their requested width
  ```

  with the trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

---

### Task 2: Learner dashboard — no AI-Academy survey, session titles instead of positions, minimum scores visible

**Walkthrough findings #2, #3, #4.**

**Files:**
- Modify: `frontend/components/onboarding/OnboardingFlow.tsx`
- Modify: `frontend/components/dashboard/ProgressHeader.tsx` (~lines 50-56)
- Modify: `frontend/components/dashboard/SessionTestHistory.tsx` (~line 63)
- Modify: `frontend/components/dashboard/FinalResultChart.tsx`

- [ ] **Step 1: #2, the survey.**
  - **What's wrong:** `OnboardingFlow` shows the archived AI-Academy survey ("Peran Anda", "Alat AI yang dipakai: ChatGPT, Claude, Gemini") to every new INVERTA learner.
  - **The fix:**
    - Delete `SurveyModal`, the `ROLES`/`GOALS`/`TOOLS` constants, `finishSurvey`, the `showSurvey` state and the `saveSurvey` import.
    - On mount, if `!state.tourCompleted`, start the tour after a short delay (`setTimeout(startTour, 250)`), so the dashboard's tour targets are mounted. Otherwise do nothing.
    - The component then returns `null`.
    - Keep the driver.js tour and its INVERTA copy unchanged.
    - Remove imports that become unused (`Modal`, `Button`, `cn`), and drop `useState` if it is unused too.
  - **Leave alone:** the backend survey endpoint and `saveSurvey` in `lib/content.ts` stay as dormant code.

- [ ] **Step 2: #3, numbering.**
  - **What's wrong:** session titles already carry their own number ("Sesi 1: Mengenal Format…"). The dashboard also prints `Sesi {orderIndex}`, the position counting the live session, so the same session shows two different numbers.
  - **The fix:**
    - **`ProgressHeader`'s subline:** for a session with parts, show `` `bagian ${Math.min(partsDone + 1, partCount)} dari ${partCount}` ``. Otherwise render no subline at all; remove the `<span>` rather than leaving it empty. The title line above already names the session.
    - **`SessionTestHistory`'s heading:** use `{first.sessionTitle} · {first.partTitle}`. `sessionTitle` is already on the DTO.
    - Check that `orderIndex` and `sessionOrderIndex` are still used for sorting where they were. Only the displayed label changes.

- [ ] **Step 3: #4, the chart floor.**
  - **What's wrong:** `FinalResultChart`'s axis domain is `[31, 68]`. A section at the ITP minimum (31) has a zero-length bar, and Recharts then draws no label, so the learner sees nothing for that section.
  - **The fix:**
    - Set `domain={[30, 68]}` and `ticks={[31, 40, 50, 60, 68]}`, so 31 has a visible bar and its label, and the axis still reads in ITP terms.
    - Add a one-line comment explaining why the floor is 30.

- [ ] **Step 4: Verify.** Run `cd frontend && npm run lint && npm run build`.

- [ ] **Step 5: Commit:**

  ```text
  fix: dashboard drops the AI-Academy survey, names sessions by title, shows minimum section scores
  ```

  with the same trailer.

---

### Task 3: Exam result — scaled section scores in ITP order, and an accurate auto-submit note

**Walkthrough finding #5.**
- "Rincian per bagian" on the exam result shows raw counts (0 / 1 / 0), while the certificate shows scaled scores (31 / 32 / 31).
- The sections are listed Reading, Listening, Structure.
- The page says "Tes ini dikirim otomatis" when one section merely timed out and the learner finished the rest. `AttemptResultDto.AutoSubmitted` is set when any section is closed by its timer, or when proctoring submits.

**Files:**
- Modify: `backend/src/Application/Assessments/AssessmentContracts.cs` (`AttemptResultDto`)
- Modify: `backend/src/Infrastructure/Assessments/FinalAssessmentService.cs` (`BuildResultAsync`, ~line 444)
- Modify: `backend/src/Infrastructure/Assessments/AssessmentService.cs` (~line 307)
- Modify: `frontend/components/exam/ExamResult.tsx`
- Regenerate: `frontend/api-client/schema.ts`
- Test: `backend/tests/Integration.Tests/FinalAssessmentTests.cs`

- [ ] **Step 1: Failing test.** Add it to `FinalAssessmentTests.cs`:

  ```csharp
  [Fact]
  public async Task The_final_result_carries_the_certificates_scaled_section_scores()
  {
      var c = await SetUp();
      var result = await AnswerEverythingCorrectly(c, (await Start(c)).AttemptId);
      var cert = Assert.Single(await AuthedGet<List<ProgramCertificateDto>>("/api/me/program-certificates", c.Token));

      Assert.NotNull(result.ScaledScores);
      Assert.Equal(cert.ScaledScores, result.ScaledScores);
      var reread = await AuthedGet<AttemptResultDto>($"/api/attempts/{result.AttemptId}/final-result", c.Token);
      Assert.Equal(cert.ScaledScores, reread.ScaledScores);
  }
  ```

  Run it and see it fail to build: `ScaledScores` does not exist yet.

- [ ] **Step 2: The DTO.** Append to `AttemptResultDto`:

  ```csharp
  IReadOnlyDictionary<string, int>? ScaledScores   // final only: the certificate's scaled section scores; null without one
  ```

  - The gating `AssessmentService` site passes `null`.
  - In `FinalAssessmentService.BuildResultAsync`, add `c.ScaledScores` to the certificate projection, and pass `cert?.ScaledScores is { } s ? ParseInts(s) : null`. `ParseInts` already exists in that class.
  - Find every construction site with `grep -rn "new AttemptResultDto(" backend/src backend/tests`.

- [ ] **Step 3: Run the tests.** Run `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~FinalAssessmentTests"`, then `dotnet test backend/Academy.slnx`. Both must be green with 0 warnings.

  This host's Docker is heavily loaded. A setup failure with "Connection reset by peer" is environmental: rerun the filter and say so in the report.

- [ ] **Step 4: Regenerate the client** using the Global Constraints commands. Check that `grep -c "scaledScores" api-client/schema.ts` increased.

- [ ] **Step 5: `ExamResult`.**
  - **Section breakdown:** iterate a fixed `["Listening", "Structure", "Reading"]`, skipping sections absent from `sectionScores`. For each row, show the scaled score (`result.scaledScores?.[s]`) as the bold value, with `{raw} benar` in muted text beside it.
  - **Without a scaled score:** when `scaledScores` is null (incomplete score map), show only `{raw} benar`.
  - **Labels:** "Listening", "Structure", "Reading", as on the certificate page. Check `app/app/certificates/page.tsx`, and reuse its labels if it maps the names differently.
  - **The auto-submit line:** replace the text with `Sebagian atau seluruh tes ditutup otomatis (waktu habis atau pengawasan).` Keep its styling.
  - **Keep:** the GR-14 note, the heading's `id="exam-result-title" tabIndex={-1}`, and the rest unchanged.

- [ ] **Step 6: Verify.** Run `cd frontend && npm run lint && npm run build`.

- [ ] **Step 7: Commit:**

  ```text
  fix: exam result shows scaled section scores in ITP order and an accurate auto-submit note
  ```

  with the same trailer.

## Outcome and deferred minors

All five findings are fixed. The final review said it was ready to merge, and its accessibility minor was fixed in `6d37f26` (the result rows now say what each number is).

**Before merge:** the backend tests for Task 3 have not run yet, because the Docker daemon hung during the session. Run `dotnet test backend/Academy.slnx`, which includes FinalAssessmentTests, SessionGatingTests and SessionPartLearnerTests.

Still open (non-blocking):
- **Admin parts editor:** the duration input rounds decimals and 0 to whole minutes (minimum 1), and the manual video ID is not validated.
- **`Modal` width check:** its `max-w-*` test ignores variant-prefixed classes such as `sm:max-w-lg`. No caller uses one.
- **Dashboard tour:** the `setTimeout` that starts it is not cleared on unmount (a 250 ms window).
- **Exam result:** a final made of non-ITP sections (Vocabulary or General) would not be listed under "Rincian per bagian". Finals are ITP-only.
