# TSD Delta — INVERTA TOEFL Preparation Program (v0.1)

Technical delta from **TSD v0.4** (archived at [`docs/archive/ai-academy/`](archive/ai-academy/README.md))
to the product described in [`FSD_INVERTA_TOEFL_v0.1.md`](FSD_INVERTA_TOEFL_v0.1.md).

**Scope of this document:** *what changes technically.* Everything not mentioned here is inherited
unchanged from TSD v0.4 — stack, 4-layer architecture, auth (§7), provider abstractions, deployment
topology (§13), DB conventions, FK policy.

---

## 1. The one rule that changes everything

The access predicate is replaced end-to-end:

```
OLD (subscription):  canAccess(user, module)   = tier(activeSubscriptions) >= module.requiredPlanTier
                                                  || module.isPreview

NEW (enrollment):    canAccess(user, session)  = isEnrolled(user, session.programId)
                                                  && sessionUnlocked(user, session)
```

Where `sessionUnlocked` implements the **linear lock** (FSD §3 R2):

```
sessionUnlocked(user, session) =
    session.orderIndex == firstOrderIndex(program)
    || isCompleted(user, previousSession(session))

isCompleted(user, session) = match session.type:
    video            → watchProgress.percent >= threshold AND gatingTest passed
    live             → live_attendance.attended == true       (admin-marked)
    final_assessment → an attempt exists with submitted_at != null
```

Both remain **server-side only** (golden rule 1). `sessionUnlocked` is evaluated in the API on every
protected resource — playback minting, gating-test fetch/submit, assessment start/submit.

**Deliberately preserved from the old design:** locked sessions are *visible but not playable*
(FSD §3 R4) — the list endpoint returns every session with a lock state; only the resource endpoints
enforce.

---

## 2. Domain model delta (concrete EF entities)

New `Domain/Entities/Programs.cs` and `Domain/Entities/Assessments.cs`. All follow existing
conventions: `Entity` base (uuid v7 app-assigned, CreatedAt/UpdatedAt), enums-as-text, jsonb bags,
`decimal(18,2)` money, timestamptz.

```csharp
// ---- Programs.cs ----
enum ProgramStatus      { Draft, Published, Archived }
enum SessionType        { Video, Live, FinalAssessment }
enum LiveMode           { Zoom, Offline }
enum EnrollmentStatus   { PendingPayment, Active, Completed, Revoked }
enum BatchStatus        { Upcoming, Running, Finished }
enum CompletionMethod   { WatchAndTest, Attended, Submitted }

Program            { Id, Name, Slug(UNIQUE), Description, Summary?, PriceIdr(decimal),
                     Status, PublishedAt? }
ProgramBatch       { Id, ProgramId→Program, Name, StartDate, Status }
ProgramSession     { Id, ProgramId→Program, OrderIndex, Type, Title, Description?,
                     // video fields (Type=Video)
                     ProviderAssetId?, DurationSeconds?,
                     // live fields (Type=Live)
                     ScheduledAt?, LiveMode?, JoinUrl?, Location?,
                     // gating/assessment link
                     AssessmentId?→Assessment }
Enrollment         { Id, UserId→User, ProgramId→Program, BatchId?→ProgramBatch,
                     Status, AmountPaidIdr(decimal), ProviderRef?, EnrolledAt? }
SessionCompletion  { Id, UserId→User, SessionId→ProgramSession, CompletedAt, Method }
LiveAttendance     { Id, SessionId→ProgramSession, UserId→User, Attended, MarkedByUserId?→User }

// ---- Assessments.cs ----
enum AssessmentKind { Gating, Final }
enum QuestionSection{ Listening, Reading, Vocabulary, Structure, General }
enum QuestionType   { Mcq }                       // extensible
enum ProctorEventKind { VisibilityHidden, WindowBlur, FullscreenExit, Warned, AutoSubmitted }

Assessment         { Id, Kind, Title, Config(jsonb: sections[], perSectionTimeLimits,
                     passThreshold, retakeCap?, proctoringEnabled, playLimit) }
Question           { Id, Section, Type, Prompt, Choices(jsonb string[]),
                     Correct(jsonb int[]), AudioRef?, PassageRef?, Tags(jsonb string[]) }
AssessmentQuestion { Id, AssessmentId→Assessment, QuestionId→Question, OrderIndex }
Attempt            { Id, UserId→User, AssessmentId→Assessment, StartedAt, SubmittedAt?,
                     AutoSubmitted, ProctorFlagged, Reinstated,
                     Answers(jsonb), SectionScores(jsonb), TotalScore, MaxScore, Passed }
ProctorEvent       { Id, AttemptId→Attempt, Kind, OccurredAt, ClientMeta(jsonb) }
ScoreBandMapping   { Id, ProgramId→Program, MinRaw, MaxRaw, PredictedBand }
```

**FK `OnDelete` per the M0 policy** (`docs/DECISIONS.md`):
- **Restrict** — retained/financial/integrity: `Enrollment.UserId`, `Enrollment.ProgramId`,
  `Attempt.UserId`, `Attempt.AssessmentId`, `SessionCompletion.*`, `Certificate.*`.
- **Cascade** — intra-aggregate children: `ProgramSession`→Program, `ProgramBatch`→Program,
  `AssessmentQuestion`→Assessment, `ProctorEvent`→Attempt, `ScoreBandMapping`→Program.
- **SetNull** — optional actor/links: `LiveAttendance.MarkedByUserId`, `Enrollment.BatchId`,
  `ProgramSession.AssessmentId`.

---

## 3. ⚠ Two reconciliations where the FSD and the existing schema disagree

The FSD §9 says `watch_progress` and `certificates` are "reused as-is". **They cannot be, literally** —
both carry FKs to the old catalog aggregate. Flagging rather than guessing (per CLAUDE.md):

### 3.1 `watch_progress` — reuse the table, repoint the target
`watch_progress.module_id` is a non-null FK → `modules`. INVERTA video progress belongs to a
**`program_sessions`** row, not a module.

**Decision: reuse the table** (as the FSD intends) by making the target polymorphic-by-nullability:
```
watch_progress.module_id   → becomes NULLABLE (FK modules, Restrict)      -- dormant AI Academy rows
watch_progress.session_id  → NEW, NULLABLE   (FK program_sessions, Restrict)
CHECK ( (module_id IS NULL) <> (session_id IS NULL) )   -- exactly one target
```
*Why this over a new `session_progress` table:* the inherited player/progress service, the resume
logic, and the ≥90% completion policy all operate on `watch_progress` — repointing keeps M4's
delivered player code intact, and honors golden rule 7 (existing progress rows are never destroyed).

### 3.2 `certificates` — level-based → score-based
`certificates.level_id` is a non-null FK → `levels`, with a UNIQUE (user_id, level_id) that encodes
"one cert per level".

**Decision:**
```
certificates.level_id      → becomes NULLABLE                             -- dormant
certificates.program_id    → NEW, NULLABLE (FK programs, Restrict)
certificates.attempt_id    → NEW, NULLABLE (FK attempts, Restrict)
certificates.section_scores→ NEW jsonb
certificates.predicted_band→ NEW text
UNIQUE(user_id, level_id)  → kept (partial, level_id NOT NULL)
                             NO unique on (user_id, program_id) — a granted retake
                             legitimately issues a SECOND certificate (FSD §12).
```
**Immutability is preserved and strengthened:** issuance is now keyed to an **attempt**, and a
retake never mutates an existing row — it inserts a new one. `attempt_id` is the immutability anchor.

---

## 4. Application ports & services

**Unchanged ports:** `IVideoProvider`, `IPaymentGateway`, `IEmailSender`, `IObjectStorage`.
`IEmailSender` gains `SendEnrollmentReceiptAsync` and `SendCertificateAsync` (+ live reminder in M5).

**New Application services:**

| Service | Responsibility |
|---|---|
| `IProgramService` | Public program + syllabus (landing/SSG), student program view with per-session lock state. |
| `IEnrollmentService` | Checkout (one-time invoice), enrollment lookup, `IsEnrolledAsync`. |
| `ISessionAccessService` | **The gate.** `CanAccessAsync(user, session)` = enrolled && unlocked. Single source of truth; every protected resource calls it. |
| `ISessionCompletionService` | Replaces M7's `IModuleCompletionService`. Decides completion per session type; idempotent, non-retroactive; advances the linear lock. |
| `IAssessmentService` | Start attempt (server-stamped `started_at`), serve questions **without the answer key**, submit + auto-score, enforce timers/retake cap server-side. |
| `IProctorService` | Ingest client events, apply grace rules, strike counting, auto-submit trigger, admin reinstate. |
| `IScoreBandService` | raw → predicted band via `score_band_mappings` (admin-maintained). |
| `ICertificateService` | **Modified** — score-based issuance from an attempt; PDF + email + verify. |
| `IProgramAdminService`, `IQuestionBankService`, `IAttendanceService` | Admin CRUD (M3/M5). |

**Dormant (kept, unregistered from product flows):** `ICatalogService`, `IEntitlementService`,
`ISubscriptionService`, M7 `IQuizService`/`IQuizAdminService`, notification preference machinery.

> **M7 quiz tables:** `quizzes`/`quiz_questions`/`quiz_attempts` are superseded by
> `assessments`/`questions`/`attempts` (which add sections, timers, audio/passages, proctoring,
> per-section scoring). They go **dormant, not dropped** — force-fitting the richer engine onto the
> old 3-column shape would be worse than a clean parallel model.

---

## 5. Endpoint surface (delta)

```
PUBLIC
  GET  /api/programs/{slug}                 program + syllabus (SSG/ISR landing)
  GET  /api/certificates/verify/{code}      unchanged (now returns band + section scores)

STUDENT (auth + enrollment-gated)
  POST /api/programs/{id}/enroll            → Xendit invoice (one-time). NEVER grants access.
  GET  /api/me/enrollments
  GET  /api/me/programs/{id}                sessions + per-session lock state + progress
  POST /api/sessions/{id}/playback          signed URL — after CanAccessAsync
  GET|PUT /api/sessions/{id}/progress       reuses watch_progress (session_id)
  GET  /api/sessions/{id}/assessment        questions WITHOUT correct answers
  POST /api/assessments/{id}/attempts       start (server stamps started_at)
  POST /api/attempts/{id}/answers           incremental save (resilience against tab-close)
  POST /api/attempts/{id}/submit            auto-score → unlock / issue certificate
  POST /api/attempts/{id}/proctor-events    client emits; server timestamps + stores
  GET  /api/attempts/{id}/result            section scores + total + predicted band

WEBHOOK
  POST /api/webhooks/xendit                 unchanged plumbing; handler now creates ENROLLMENT

ADMIN
  CRUD /api/admin/programs|sessions|batches
  CRUD /api/admin/questions|assessments
       /api/admin/programs/{id}/score-bands
  POST /api/admin/sessions/{id}/attendance  bulk mark attended
  POST /api/admin/attempts/{id}/reinstate   clear proctor flag (false-positive support path)
  GET  /api/admin/enrollments|attempts      dashboards (M5)
```

### 5.1 Anti-cheat invariants (server-side, non-negotiable)
1. **The answer key never leaves the server.** Question DTOs for students omit `Correct`. (Same
   pattern already proven in M7's `QuizDto`.)
2. **Timers are server-authoritative.** `started_at` is server-stamped; submit past the limit is
   auto-submitted and scored as-of expiry. A client clock is never trusted.
3. **Retake cap enforced server-side** (final = 1 attempt by default, FSD §12).
4. **Scoring happens server-side only**; the client never computes or posts a score.
5. **Proctor events are advisory data, not authority** — the *server* decides strike 2 → auto-submit.

---

## 6. Proctoring contract

```
client → POST /api/attempts/{id}/proctor-events  { kind, occurredAt, clientMeta }
```
- Client detects `visibilitychange` (Page Visibility API) + `window.blur`; **suppresses events < 2s**
  (OS notification grace, FSD §7).
- Server stores every event, maintains the strike count, and returns the authoritative state
  (`{ strikes, action: none|warn|autoSubmit }`).
- Strike 1 → `warn`; strike 2 → server marks the attempt `auto_submitted=true`,
  `proctor_flagged=true` and finalizes scoring.
- Admin `reinstate` sets `Reinstated=true` and clears the flag (audit-logged), leaving the event
  trail intact for dispute review.

**Documented limitation (carried into the UI copy and the admin docs):** client-side detection is
**deterrence, not exam security** — it cannot prevent a second device and must not be marketed as
tamper-proof (FSD §7).

---

## 7. Certificate & TOEFL-prediction claims

- Issuance trigger: **final-assessment submit** → score → band → immutable `certificates` row →
  PDF (PDFsharp/MigraDoc, embedded DejaVu fonts — already working in Linux containers) → SES email →
  downloadable + publicly verifiable at `/verify/{code}`.
- **Mandatory disclaimer on the certificate, the result screen, and the verify page:** the score is
  an INVERTA **prediction**, *not* an official ETS TOEFL score. TOEFL is a registered ETS trademark.
- **Content dependency (not engineering):** `score_band_mappings` ships with a clearly-marked
  **placeholder** map. The business must supply a pedagogically valid mapping before launch
  (FSD §10 Q6), and should obtain a legal sanity-check on the naming/claims (Q7). Engineering builds
  the mechanism; it cannot validate the pedagogy.

---

## 8. Frontend delta (rendering matrix)

| Route | Rendering | Note |
|---|---|---|
| `/` , `/program/{slug}` | **SSG/ISR** | Program landing replaces the catalog as primary navigation. |
| `/verify/{code}` | SSR | Unchanged; now shows band + section scores. |
| `/app/program/{id}` | CSR | Student program view — ordered sessions with lock states. |
| `/app/session/{id}` | CSR | Player + gating test on the same page (test unlocks at threshold). |
| `/app/assessment/{id}` | CSR | Timed multi-section runner + proctor listener. **No prefetch of answers.** |
| `/admin/*` | CSR | Programs, sessions, question bank, attendance, attempts/flags, score bands. |

**Dormant frontend:** `/catalog`, `/pricing` (tiered), subscription/billing screens. Kept in-repo,
unlinked from navigation.

---

## 9. Migration strategy

**One migration, additive only** (`InvertaProgramsAndAssessments`):
- CREATE the new tables (§2).
- ALTER `watch_progress`: `module_id` → nullable, ADD `session_id` + CHECK (§3.1).
- ALTER `certificates`: `level_id` → nullable, ADD `program_id`/`attempt_id`/`section_scores`/
  `predicted_band` (§3.2).
- ALTER `payment_transactions`: kind gains `ProgramPurchase`.
- **No DROPs.** Subscription/catalog tables stay for the archived product (FSD Q10).

Generated with `dotnet ef`, never hand-edited to diverge from the snapshot; reviewed with
`dotnet ef migrations script` before applying (CLAUDE.md convention).

---

## 10. Testing delta (critical paths)

Inherited auth/webhook/anonymization tests stay. New must-cover:

1. **Enrollment only via verified webhook** — success page grants nothing; unpaid user is blocked.
2. **Linear lock** — session k+1 returns 403 until k completes; completing k unlocks k+1.
3. **Gating test gates** — watch 100% alone does NOT complete a video session; passing the test does.
4. **Answer key never served** — student question DTO has no `correct` field.
5. **Server-authoritative timer** — submit after expiry is auto-submitted/scored as-of expiry.
6. **Retake cap** — a second final attempt is refused (default); an admin-granted retake issues a
   **new** certificate and leaves the first untouched (immutability).
7. **Proctoring** — 2 strikes auto-submits + flags; <2s blur is ignored; admin reinstate clears the
   flag but keeps the event trail.
8. **Score → band** — mapping boundaries; unmapped raw score fails loudly rather than emitting a
   blank band.
9. **Progress/attempt retention** — revoking an enrollment removes access but never deletes
   `watch_progress`, `attempts`, or `certificates` (golden rule 7).

---

## 11. What this delta does NOT change

Stack · 4-layer boundaries · auth in full (TSD §7) · JWT/refresh/BFF · secrets policy ·
UU PDP soft-delete + anonymization · provider abstractions and their dev-sim adapters ·
deployment topology (single-origin behind Cloudflare Tunnel, see `DEPLOY.md`) · DB conventions ·
FK policy · OpenAPI-generated frontend client.

---

*Drives: revised `CLAUDE.md` golden rules, `M2_Program_Enrollment_Spec.md`, and the
`InvertaProgramsAndAssessments` migration.*
