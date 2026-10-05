# Session parts — design

**Date:** 2026-10-05 · **Status:** approved in brainstorming, awaiting spec review
**Changes product behaviour:** FSD §5 (video sessions) and §6.1 (gating tests); CLAUDE.md GR-8.
Both are updated in the same change (see §9).

## 1. Why

Business feedback (2026-10-04): one session is several steps, not one video plus one test.
Example flow:

> lesson video → listening test (one audio for all its questions, played once) →
> discussion video ("pembahasan") → …

The admin must choose how many steps a session has and in what order, including more than one
test per session.

## 2. Decisions (from the PO, 2026-10-04/05)

| # | Decision |
|---|---|
| D1 | A **video session** is an ordered list of **parts**. Live and final-assessment sessions stay single-step. |
| D2 | Part types: **lesson video**, **test**, **discussion video**. Any number, any order, more than one test allowed. |
| D3 | A discussion video belongs to the test **directly before it**; at most one per test; optional per test. |
| D4 | The discussion opens when its test is **passed**, **or** the learner has **failed it N times** (N set per test). |
| D5 | The discussion is **required**: the part after a test+discussion pair opens only when the test is passed **and** the discussion is watched. A learner who reached it through the limit must still retake until they pass. |
| D6 | Tests are still retaken **without limit** (FSD §6.1, PO 2026-09-04). N does not lock anyone out; it only opens the discussion early. |
| D7 | A test may have **one shared audio** for all its questions, playable **K times per attempt** (per-test setting, default 1). A retake gets fresh plays. |

## 3. Learner rules

- **Order.** Parts open one after another within a session. Part *p+1* opens only when part *p* is done
  (D5 makes a discussion part "done" a precondition of whatever follows it).
- **Exception (D4).** A discussion part opens when its test is passed **or** the failed attempts on it
  are ≥ N. If no N is set, only a pass opens it.
- **Done:**
  - lesson video and discussion video: watch progress ≥ the existing watch threshold;
  - test: a submitted, passing attempt exists (an assessment with no questions counts as passed,
    matching today's rule).
- **Session complete** = every part done. It is recorded only by `ISessionCompletionService`, idempotent
  and non-retroactive (GR-8). Session *k+1* still opens only when session *k* is complete; that rule is
  unchanged.
- **Session with no parts.** A video session that was just created has no parts yet. Learners see it
  as "Belum tersedia": it never completes and nothing after it opens. Saving an empty parts list is
  refused (§6), so once a session has parts it always keeps at least one.

## 4. Data model (additive)

**New table `session_parts`** (uuid v7 PK, snake_case):

| column | type | notes |
|---|---|---|
| `session_id` | uuid FK → `program_sessions` | Cascade (intra-aggregate) |
| `order_index` | int | unique per session |
| `kind` | text enum | `LessonVideo` \| `Test` \| `Discussion` |
| `title` | text | |
| `provider_asset_id` | text null | video kinds only; never sent to students |
| `duration_seconds` | int null | video kinds only |
| `assessment_id` | uuid null FK → `assessments` | `Test` only; Restrict |

CHECK constraints: video kinds have an asset and no assessment; `Test` has an assessment and no asset.
The "discussion directly follows a test" rule is order-dependent, so it is validated in the application
on save, not in the database.

**`watch_progress`.** Add nullable `part_id` (FK → `session_parts`, Restrict, since progress is retained
under GR-7). Replace the unique index `(user_id, session_id)` with `(user_id, part_id)` where `part_id`
is not null. `session_id` stays set alongside it.

**Test settings** (`assessments.config` jsonb, so no migration is needed):
- `audioRef`: one shared recording for the whole test (an R2 key, signed on serve; GR-3);
- `audioPlayLimit`: plays per attempt. The field already exists; it defaults to 1 for a test with
  shared audio;
- `discussionAfterFailures`: N (int, ≥ 1, null meaning only a pass opens the discussion).

The gating `RetakeCap = null` override in `AssessmentService` stays (D6).

**Part "done" is derived, not stored.** It comes from `watch_progress` and `attempts`, both retained
forever. Only session completion is written, to the existing `session_completions` table.

**Conversion (one migration with a data step).** For each `Video` session:
1. Insert a `LessonVideo` part from the session's `provider_asset_id` and `duration_seconds`.
2. If the session has an `assessment_id`, insert a `Test` part after it.
3. Set `watch_progress.part_id` to the new lesson part for that session's rows.

Existing `session_completions` are untouched, so completed sessions stay complete. After the migration,
parts are the source of truth. The old columns on `program_sessions` are kept (additive rule) but are
no longer read for video sessions.

## 5. Access and API

- **One rule, in `ISessionAccessService`.** It gains `CanAccessPartAsync(user, partId)`, which means
  the session is accessible **and** the part is open per §3. The pure ordering and discussion rule
  lives in `Domain/SessionAccess.cs`, next to the existing rule, with unit tests.
- **Learner endpoints**, all under `/api/sessions/{id}`:
  - `GET` returns the session context, now including `parts[]` with `{id, kind, title, status}`, where
    status is Locked, Open or Done. For a test part it also returns attempts used, N, and whether the
    discussion is open.
  - `POST /parts/{partId}/playback` returns a signed video URL for a video part.
  - `GET` and `PUT /parts/{partId}/progress` read and save watch progress.
  - `GET /parts/{partId}/assessment` returns the test without the answer key (GR-11).
  - `POST /parts/{partId}/audio` starts or resumes the shared audio for the current attempt.

  These replace the session-level playback, progress and assessment routes, which are removed in the
  same change along with their frontend callers.
- **Shared audio.** This reuses the final assessment's section-audio mechanism: the first play is
  stamped on the attempt's `state`, a reload resumes at the live position, and the audio is never
  rewound. A start beyond `audioPlayLimit` for that attempt is refused (409, "Audio sudah diputar
  sebanyak batas yang diizinkan."). The client player hides pause and seek. This is deterrence, not a
  guarantee (GR-14 spirit).
- **Starting an attempt** on a test part checks part access first.
- **After a submit,** completion is re-evaluated, as today.
- **Errors.** A locked part returns the existing "Sesi ini masih terkunci" problem. A discussion before
  a pass or before N failures is treated as locked.

## 6. Admin

- **Session form (Video type).** The single video, ID field and test selector are replaced by a
  **parts editor**:
  - Add a part: lesson video, test, or discussion video. Discussion is offered only directly after a
    test that has none.
  - Reorder with up and down buttons; edit; remove.
  - Video parts use the existing `VideoPicker`, showing the title only. There is no raw ID field, and
    the duration is filled from the picked video.
  - Test parts: choose an existing gating assessment, or create a new empty one. A link opens the
    existing test editor.
- **Test editor (`/admin/assessments/[id]`).** It adds the shared audio upload, using the existing
  `POST /api/admin/media/audio`, plus fields for plays per attempt and "discussion opens after N failed
  attempts".
- **Endpoints** (admin-only, audited like the other session edits):
  - `GET /api/admin/sessions/{id}/parts`;
  - `PUT /api/admin/sessions/{id}/parts`, which saves the whole ordered list in one call (adds,
    removes, reorders and edits) and is validated as one unit.
- **Removal guard.** Removing a part that has learner `watch_progress` or `attempts` is refused (409,
  with a reason). Editing its title or video is allowed.
- **Adding a part to a session in use.** Completed learners stay complete (non-retroactive). Learners
  who are not yet complete must do the new part.
- **Validation on save:**
  - at least one part;
  - each discussion directly follows a test;
  - at most one discussion per test;
  - each test part has an assessment of kind `Gating`;
  - the same assessment is not used twice;
  - video parts have a valid asset (the existing Bunny GUID check).

## 7. Learner UI (`/app/session/[id]`)

- Parts are listed in order with a status chip: Terkunci, Tersedia or Selesai. The open part is shown
  below the list.
- Video parts use the existing player and progress saving, keyed by part.
- Test parts use the existing test screen. With shared audio, a single player with no pause and no
  seek is shown above the questions. After a failed attempt the screen shows "Percobaan ke-x". When
  x reaches N it also shows: "Video pembahasan sudah terbuka. Tonton, lalu ulangi tes sampai lulus."
- When every part is done, the existing "session complete" state shows and the next session opens.

## 8. Tests

- **Domain:**
  - part ordering;
  - the discussion opens on a pass, at exactly N failures, and not at N−1;
  - the discussion stays closed when N is null and there is no pass;
  - the part after a pair needs both the pass and the discussion watched;
  - a session is complete only when every part is done.
- **Integration:**
  - the conversion keeps progress and completions;
  - locked-part access is refused for playback, progress, assessment and audio;
  - the audio play limit per attempt, with fresh plays on a retake;
  - removing a part with progress is refused;
  - the save-validation cases;
  - the answer key is still absent and no media URL is persisted;
  - completion is non-retroactive after a part is added.
- **End to end:** lesson video → test failed N times → discussion opens → discussion watched → test
  passed → session complete → next session open.

## 9. Docs updated in the same change

- **FSD §5:** a video session is an ordered list of parts.
- **FSD §6.1:** the per-test shared audio and play limit, and the discussion that opens after a pass
  or N failures.
- **FSD §9:** the data-model delta.
- **CLAUDE.md GR-8:** "video: every part done, where a test part means passed and a video part means
  watch ≥ threshold".

## 10. Out of scope (separate pieces, in this order)

1. Separate question banks for the TOEFL simulation and session tests.
2. Uploading video to Bunny from the admin screen.
3. Removing the raw video ID field elsewhere. The parts editor ships without it.
