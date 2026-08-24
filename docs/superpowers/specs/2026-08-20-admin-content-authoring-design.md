# Design — Admin content authoring (INVERTA A3)

| | |
|---|---|
| **Date** | 2026-08-20 |
| **Status** | Approved, ready for implementation planning |
| **Scope** | Sub-project A3 of the INVERTA post-M6 plan |
| **Governing spec** | [`docs/KAK_INVERTA_TOEFL_v1.0.md`](../../KAK_INVERTA_TOEFL_v1.0.md) |

---

## 1. Why this exists

M0–M6 delivered the INVERTA product and the KAK §9.12 table lists the full administrative surface as
delivered. **It is not.** An audit of which admin client functions have a page behind them found:

| Capability | API | Admin UI |
|---|---|---|
| Question bank CRUD | ✅ | ✅ `/admin/questions` |
| Program / session CRUD | ✅ | ✅ `/admin/programs` |
| Create & configure an assessment | ✅ | ❌ none |
| Put questions into an assessment | ✅ | ❌ none |
| Attach an assessment to a session | ✅ | ❌ none |
| Edit the score→band table | ✅ | ❌ none |

`listScoreBands`, `replaceScoreBands`, `setAssessmentQuestions`, `getAssessment`, `listAssessments`,
`createAssessment` and `attachAssessment` all exist in `frontend/lib/sessions.ts`; **no page imports
any of them.** Those paths were verified over HTTP during milestone testing, which proved the API and
silently skipped the interface.

**Consequence:** an administrator using a browser today cannot create a gating test, compose the final
assessment, attach a test to a session, or enter the score→band values. The claim that "operations can
run the product without engineering" is false until this is closed.

This design closes that gap. It does not address question-bank bulk import (deferred, §3).

### The failure mode this prevents

A program can currently be published with an incomplete score→band table. Learners sit the entire
final assessment, and only at scoring does it fail: the server logs loudly and issues no certificate
(KAK §9.9.4), but the learner sees "conversion unavailable". Operations discovers the problem only
after someone has finished a 115-minute test. Readiness enforcement (§5) turns that into an obvious
pre-publish failure.

---

## 2. Decisions

Confirmed with the product owner before design:

| # | Decision | Rationale |
|---|---|---|
| D1 | **Scope A3 to closing the UI gap.** Bulk import of the question bank is a separate, later decision. | Authoring is currently impossible; efficiency at scale is a second-order problem. |
| D2 | **Operator profile: non-technical operations staff, ongoing use.** | Drives guardrails: inline validation, explicit errors, confirmation on destructive actions, readiness checking. |
| D3 | **Block publish behind a readiness checklist.** | Converts a silent post-test failure into a visible pre-launch one. |
| D4 | **Session-centric hybrid IA** (see §4). | Matches how operators think ("session 3 needs a test") without library indirection. |
| D5 | **Score bands entered by pasting a table**, with validation and preview. | 143 rows per program. Row-by-row entry is not a workable interface and invites exactly the silent errors D3 exists to prevent. Distinct from D1's bulk import: this is the only sane way to enter this particular table. |

---

## 3. Scope

**In scope**

1. Gating-test authoring (create, configure, compose, attach, detach) from the session row.
2. Final-assessment authoring on a dedicated page with per-section composition.
3. Score→band editor with paste, validation, preview, row-level correction, and download of the
   current table so operators can round-trip through a spreadsheet.
4. Program readiness checking, surfaced in the UI and enforced at publish.
5. Targeted component extraction from `app/admin/programs/page.tsx` (§7).

**Out of scope** — do not build in this project

Question-bank bulk import · randomised or adaptive test assembly · editing question *content* from the
composer (link to the bank instead) · free-text/essay grading · a frontend test harness (§9).

---

## 4. Architecture and information architecture

Each surface is sized to its job.

| Surface | Location | Rationale |
|---|---|---|
| **Gating test** (≈5 questions) | Modal from the session row in `/admin/programs` | Small enough for a modal; keeps the operator in the program context |
| **Final assessment** (140 questions, 3 sections) | New page `app/admin/assessments/[id]/page.tsx` | Needs space a modal cannot give |
| **Score→band table** (143 rows) | New page `app/admin/programs/[id]/score-bands/page.tsx` | Paste + preview + validation needs a page |
| **Readiness** | Panel on `/admin/programs`, enforced at publish | Where publishing happens |

Score bands belong to the **program**, not the assessment: `score_band_mappings.program_id` is where
the FK lives, and the ITP total is defined per program.

Nested route `app/admin/programs/[id]/score-bands/page.tsx` coexists with the existing
`app/admin/programs/page.tsx` list page without conflict in the App Router.

---

## 5. Backend design

Everything needed for authoring already exists. The single new backend capability is readiness.

### 5.1 `IProgramReadinessService`

```
Task<ProgramReadinessDto> GetAsync(Guid programId, CancellationToken ct = default);

ProgramReadinessDto  { bool Ready, IReadOnlyList<ReadinessCheckDto> Checks }
ReadinessCheckDto    { string Key, string Title, bool Passed, bool Blocking, string? Detail }
```

Checks:

| Key | Passes when | Blocking |
|---|---|---|
| `has_sessions` | The program has ≥ 1 session | yes |
| `gating_tests_populated` | Every session with an attached assessment has ≥ 1 question | yes |
| `final_assessment_present` | A `FinalAssessment` session exists and has an assessment attached | yes |
| `final_sections_populated` | For each section in the assessment config, the attached question count equals the configured count | yes |
| `score_bands_complete` | For each ITP section, every raw score `0..max` is covered exactly once — no gaps, no overlaps — and every scaled value is within that section's bounds | yes |
| `price_set` | `price_idr > 0` | no (informational) |

`score_bands_complete` uses the section maxima from `ToeflScoring` (Listening 50, Structure 40,
Reading 50) and the scaled bounds (`ScaledMin` 31; max 68/68/67).

### 5.2 Publish gating

`ProgramAdminService.CreateAsync` and `UpdateAsync` refuse `Published = true` when readiness has any
failing blocking check, throwing `ProgramException(…, 409)` whose message names the failures. The
existing `AuthExceptionHandler` maps it to RFC-7807 problem details.

Deliberately **not** gated: saving a draft, unpublishing, and editing an already-published program's
non-publish fields. Operators must be able to stage incomplete work.

### 5.3 Validation split — staging vs completeness

`PUT /api/admin/programs/{id}/score-bands` keeps its current behaviour: it validates section names,
`minRaw <= maxRaw`, and per-section overlap, and **accepts a partial table**. Coverage completeness is
checked by readiness, not by the PUT.

This is intentional. Operators paste sections incrementally; blocking the save would force
all-or-nothing entry. Publishing is where completeness is enforced.

### 5.4 New endpoint

```
GET /api/admin/programs/{id}/readiness → ProgramReadinessDto     [Admin]
```

No other API changes. Assessment create/compose/attach and score-band read/replace already exist.

---

## 6. Frontend design

### 6.1 Gating test editor (modal)

Opened from a Video session row in `/admin/programs`.

- **No assessment attached:** operator sets title, pass threshold, retake cap (blank = unlimited),
  picks questions from the bank, saves. Sequence: `createAssessment` (kind `Gating`) →
  `setAssessmentQuestions` → `attachAssessment`.
- **Assessment attached:** loads via `getAssessment`; edits config and question selection; saves via
  `updateAssessment` + `setAssessmentQuestions`.
- **Detach:** `attachAssessment(sessionId, null)`. The assessment itself is not deleted — attempts
  against it are retained learner records (GR-7).

Question picker: bank list filtered by section and search, multi-select, shows current selection with
order and a remove control.

### 6.2 Final assessment composer (page)

`/admin/assessments/[id]`.

- Reads the assessment config to learn its sections and required counts.
- One panel per section (Listening, Structure, Reading) showing **selected vs required**, e.g. `48 / 50`.
- Questions are picked per section from the bank, filtered to that section.
- **On save, the per-section selections flatten into one ordered list** — Listening, then Structure,
  then Reading — and go to the existing `PUT /api/admin/assessments/{id}/questions`, which replaces
  the whole set. This matches how `FinalAssessmentService` filters by section at runtime, so no API
  change is needed.
- Config editing (per-section time limits, retake cap, proctoring, audio play limit) lives on the same
  page.

### 6.3 Score band editor (page)

`/admin/programs/[id]/score-bands`.

Flow:

1. Operator pastes tab- or comma-separated rows: `section, minRaw, maxRaw, scaledScore[, band]`.
2. **Client parses** and reports per-line errors with line numbers. Nothing is sent while invalid.
3. Preview table plus a per-section coverage summary (covered `0..max`, gaps listed, overlaps listed,
   out-of-range scaled values listed).
4. Save replaces the table via the existing `PUT`, after a confirmation naming what will be overwritten.
5. **The server re-validates** — client validation is never authority.
6. Existing rows remain editable individually for corrections.

A "download current table" action is included so operators can round-trip through a spreadsheet.

### 6.4 Readiness panel

On `/admin/programs`, each program row shows a readiness badge (Ready / *n* items missing). Opening it
lists every check with pass/fail and a deep link to the surface that fixes it. Attempting to publish a
program that is not ready surfaces the same list from the 409 response.

---

## 7. Code health

`app/admin/programs/page.tsx` is 374 lines today, holding the page, program form, session manager and
session form. This work would push it past 500.

Extract, without changing behaviour:

- `components/admin/ProgramForm.tsx`
- `components/admin/SessionManager.tsx`
- `components/admin/SessionForm.tsx`

Then add:

- `components/admin/GatingTestEditor.tsx`
- `components/admin/ReadinessPanel.tsx`
- `components/admin/QuestionPicker.tsx` (shared by the gating editor and the section composer)
- `components/admin/SectionComposer.tsx`
- `components/admin/ScoreBandPaste.tsx`

No unrelated refactoring. `AttendanceModal.tsx` is already extracted and stays as is.

---

## 8. Error handling

- All mutations surface the RFC-7807 `title` through the existing `problem()` helper in
  `lib/programs.ts` / `lib/sessions.ts`.
- Paste parse failures are listed per line with line numbers; nothing is saved until the input is valid.
- A blocked publish shows the failing readiness checks with links to fix each.
- Destructive replacements (question set, score-band table) require confirmation that names what will
  be overwritten and how many rows or questions are affected.
- Detaching an assessment explains that existing attempts are retained.

---

## 9. Testing and acceptance

### Backend integration tests

1. Each readiness check fails independently when its precondition is unmet, and the program reports
   `Ready = false`.
2. A fully-configured program reports `Ready = true`.
3. Publishing an unready program returns **409** naming the failing checks.
4. Publishing a ready program succeeds.
5. Saving a **draft**, unpublishing, and editing a published program's other fields are **not** blocked.
6. Score bands with a gap fail `score_bands_complete`; with an overlap the PUT itself rejects; a
   complete table passes.
7. A partial score-band table still saves (staging) but leaves the program unready.
8. Existing assessment and score-band endpoint tests continue to pass.

### Frontend verification

This repository has **no frontend test harness**; every milestone so far verified the UI with
`tsc --noEmit`, `next build`, and a live walkthrough on the running deployment. This project keeps
that approach. Standing up a component-test stack is its own piece of work and is out of scope.

Live walkthrough acceptance — performed in a browser, not with `curl`, because a browser-only gap is
precisely what this project exists to close:

- Create a gating test on a video session, attach it, and confirm a learner sees it.
- Compose the final assessment to full section counts.
- Paste a complete score-band table, see the preview, save it.
- Publish is refused while anything is missing, and the checklist says what.
- Publish succeeds once readiness passes.

---

## 10. Risks

| Risk | Mitigation |
|---|---|
| Operators paste a table with a subtly wrong scaled column (e.g. shifted by one row) | Preview shows the parsed table before saving; coverage summary makes gaps and duplicates visible; round-trip download lets them diff against their spreadsheet |
| The 140-question composer is slow to use even with multi-select | Per-section filtering plus selected/required counters; if it proves painful in practice, question-bank import (deferred) is the follow-up |
| Readiness gating frustrates staged content work | Only *publish* is gated; drafts and partial saves stay legal |
| Extraction of existing components introduces regressions | Pure move, no behaviour change; existing program/session integration tests cover the endpoints beneath them |

---

## 11. Follow-ups this design deliberately leaves open

- **Question-bank bulk import** — revisit once it is clear how content actually arrives.
- **KAK correction** — §9.12 currently overstates the admin surface. It should be corrected to match
  reality when this project lands, so the spec and the code agree.
