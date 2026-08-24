<div align="center">

# KERANGKA ACUAN KERJA (KAK)

# INVERTA — TOEFL PREPARATION PROGRAM
## FUNCTIONAL & TECHNICAL SPECIFICATION DOCUMENT

<br>

| | |
|---|---|
| **PRODUCT** | : INVERTA — Online TOEFL Preparation Program |
| **PLATFORM** | : Web Application (.NET 10 API + Next.js) |
| **DOCUMENT TYPE** | : Combined FSD + TSD (implementation-complete) |
| **VERSION** | : 1.0 |

<br>

**FISCAL YEAR 2026**

</div>

---

## DOCUMENT CONTROL

| Field | Value |
|---|---|
| **Document ID** | KAK-INVERTA-TOEFL-v1.0 |
| **Supersedes** | `FSD_INVERTA_TOEFL_v0.1.md`, `TSD_Delta_INVERTA_v0.1.md` (both remain valid; this document consolidates and completes them) |
| **Archived predecessor** | AI Productivity Academy PRD v2.5 / TSD v0.4 — `docs/archive/ai-academy/` (paused, retained) |
| **Authoritative for** | Product behaviour AND technical implementation |
| **Intended reader** | An engineer or an LLM implementing the system **without further clarification** |
| **Language** | English (UI strings are Bahasa Indonesia — see §10) |

### Reading rule
Where this document and any other document disagree, **this document wins**. Items explicitly
marked **`[PLACEHOLDER]`** are the only values that may be changed without a spec revision; every
other value is binding. Items marked **`[OWED BY BUSINESS]`** block *launch*, not *implementation* —
build the mechanism, ship the marked placeholder.

---

# 1. LATAR BELAKANG (BACKGROUND)

1. English proficiency certification, particularly TOEFL, is a prerequisite for higher education,
   scholarships, and professional advancement in Indonesia. Preparation options are dominated by
   offline classes with fixed schedules and by unstructured self-study material of uneven quality;

2. Learners who self-study report two consistent failures: (a) no reliable way to measure readiness
   before paying for the official test, and (b) no enforced progression, causing them to skip
   fundamentals and plateau;

3. An organisation (hereafter "the Company") operates an existing, fully-delivered learning platform
   originally built as a subscription video LMS ("AI Productivity Academy"). That platform provides
   authentication, video delivery, payments, email, storage, certification and deployment
   infrastructure that are all directly reusable;

4. Rather than build a new system, the Company pivots the existing platform to a **paid, linear,
   test-gated TOEFL preparation program**. This reuses roughly 60% of delivered engineering
   (authentication, payment rails, video pipeline, certificate generation, deployment topology) and
   requires new work only in program/enrollment modelling and the assessment engine;

5. The commercial model changes from a recurring tiered subscription to a **single one-time program
   purchase**. This removes recurring-billing complexity (proration, grandfathering, dunning) and
   matches how test-preparation programs are actually bought in this market;

6. The pedagogical model changes from free browsing to **earned progression**: each video lesson is
   followed by a mandatory short test that must be passed before the next session unlocks. This
   directly addresses failure (b) above;

7. The certification model changes from completion-based to **score-based**: the program terminates
   in a multi-section, timed, soft-proctored assessment that produces an instant score and a
   **TOEFL-prediction certificate**, directly addressing failure (a) above.

**Consequence.** This document specifies the pivoted product in full. It is written so that
implementation can proceed end-to-end without further requirements clarification.

---

# 2. MAKSUD DAN TUJUAN (PURPOSE AND OBJECTIVES)

### a. Maksud (Purpose)
To deliver a production-ready, publicly sellable online TOEFL preparation program on the Company's
existing platform, in which a learner pays once, progresses through a mandatory ordered curriculum,
sits a proctored final assessment, and receives an instant predicted TOEFL score with a verifiable
certificate.

### b. Tujuan (Objectives)
1. **Enforce earned progression** — make it structurally impossible to reach the final assessment
   without completing every preceding session (§9.3);
2. **Produce a defensible score prediction** — a multi-section, timed assessment following the
   TOEFL ITP format, scored server-side with a documented conversion method (§9.9);
3. **Protect assessment integrity proportionately** — deter casual cheating via soft proctoring,
   while being explicit that this is deterrence and not exam security (§9.8);
4. **Monetise per program, not per month** — a single verified payment grants durable access (§9.4);
5. **Preserve every platform invariant** already established — server-side access control,
   webhook-only entitlement, signed media, immutable certificates, retained learner data (§7);
6. **Remain legally honest** — never represent the output as an official ETS TOEFL score (§9.9.4).

---

# 3. TARGET / SASARAN (TARGETS)

| # | Target | Measurable acceptance |
|---|---|---|
| 1 | **A purchasable program exists** | A public landing page renders a published program with syllabus and price; an authenticated, email-verified user can complete payment and receive access. |
| 2 | **Access is provably earned** | Automated test proves session *k+1* returns HTTP 403 until session *k* is complete; watching 100% of a video with an unpassed gating test does **not** complete it. |
| 3 | **Assessment engine operational** | A learner can sit a 3-section timed assessment, with server-authoritative timers, and receive an instant per-section and total score. |
| 4 | **Soft proctoring operational** | Two qualifying focus-loss strikes auto-submit and flag the attempt; every event is persisted; an admin can reinstate a false positive without erasing the trail. |
| 5 | **Certificate issued and verifiable** | On submission, an immutable certificate is created, a PDF is generated, an email is sent, and `/verify/{code}` publicly resolves it. |
| 6 | **Admin can operate the product** | An administrator can create/reorder/publish programs and sessions, author tests from a question bank, mark live attendance, review flagged attempts, and maintain the score conversion table — with no engineer involvement. |
| 7 | **Platform invariants intact** | The full automated suite passes, including retention, immutability, webhook-idempotency and answer-key-secrecy tests (§16.3). |

---

# 4. MANFAAT YANG DIPEROLEH (BENEFITS)

a. **For the learner** — a structured path with objective feedback, and a readiness estimate before
   committing to the cost of an official test;
b. **For the Company** — higher realised revenue per learner than a monthly subscription for a
   short program, with no churn management or recurring-billing surface;
c. **For engineering** — retires the entire recurring-billing subsystem (proration, grandfathering,
   dunning, plan management) from the active codebase while preserving it for possible revival;
d. **For operations** — a self-service admin surface removes the need for engineering involvement in
   content, scheduling, and dispute handling;
e. **Strategic** — the assessment engine is generic (sections, timers, question bank, proctoring) and
   can serve future certification products without redesign.

---

# 5. NAMA ORGANISASI (OWNING ORGANIZATION)

*(Adapted from procurement: the internal owners of this build.)*

| Function | Owner | Responsibility |
|---|---|---|
| **Product ownership** | Product Owner | Approves scope; owns every `[OWED BY BUSINESS]` item in §17. |
| **Engineering** | Platform engineering | Implements this document; owns all technical decisions inside it. |
| **Content** | Academic/content team | Supplies syllabus, video assets, question bank, score conversion values. |
| **Operations** | Program administration | Day-to-day admin: batches, live sessions, attendance, dispute review. |

**Escalation rule.** If implementation reveals a conflict between this document and the codebase,
engineering must **stop and flag**, never silently choose (per `CLAUDE.md`).

---

# 6. SUMBER PENDANAAN DAN PERKIRAAN BIAYA (COST MODEL AND ESTIMATE)

*(Adapted from procurement: recurring run-cost of the system, not a tender budget.)*

### a. Revenue model
Single one-time purchase per learner per program. **No recurring billing of any kind.**

| Item | Value |
|---|---|
| Program price | **Rp 1.500.000** **`[PLACEHOLDER]`** |
| Currency/precision | IDR, `decimal(18,2)`, rounded to whole rupiah at charge time |
| Billing frequency | Once, at enrollment |
| Refund policy | **`[OWED BY BUSINESS]`** — see §17 |

> **Single point of change.** The price lives in `programs.price_idr` (database), seeded by
> `ProgramSeeder`. Changing it requires **no code change** — edit via the admin UI. No price may be
> hard-coded in the frontend.

### b. Recurring infrastructure cost drivers
Sized for the stated early scale of **20–50 enrollments/month**.

| Component | Provider | Cost driver | Notes at target scale |
|---|---|---|---|
| Application hosting | Self-hosted Docker + Cloudflare Tunnel | Fixed | Single node is sufficient; see `DEPLOY.md`. |
| Database | PostgreSQL (self-hosted container) | Fixed | Single instance; volume-backed. |
| Video delivery | Bunny Stream | GB streamed | Dominant variable cost. ~15 min/session × session count. |
| Listening audio | Cloudflare R2 | GB stored + egress | Small; audio files are short. |
| Payments | Xendit | % per transaction | Charged per successful invoice only. |
| Email | Amazon SES | per message | Transactional only: verification, reset, receipt, certificate, live reminder. |

### c. Cost-control requirements (binding)
1. Media is **never** served from a public URL — all access is via short-TTL signed URLs, preventing
   hotlinking and uncontrolled egress (§7 GR-3);
2. Payment provider costs accrue only on successful charges;
3. Email is transactional only — no marketing/notification volume in v1.

---

# 7. MEKANISME PENGADAAN (GOVERNANCE MECHANISM)

*(Adapted: the rules under which this work is authorised and constrained.)*

Implementation is governed by the invariants in `CLAUDE.md`. These are **non-negotiable** and any
change to them requires a spec revision, not a code review.

### Golden Rules

| # | Rule |
|---|---|
| **GR-1** | **Access is server-side only.** `canAccess(user, session) = isEnrolled(user, program) && sessionUnlocked(user, session)`, evaluated in the API on every protected resource. Never inferred or granted client-side. |
| **GR-2** | **Enrollment changes only via verified payment webhooks.** The checkout success page is informational and grants nothing. Webhooks are signature-verified and idempotent via `webhook_events.external_id`. |
| **GR-3** | **Signed media only.** Video and listening-audio URLs are minted server-side, short-TTL, after an access check. No public or persisted media URLs, ever. |
| **GR-4** | **No business logic in Next.js.** Route handlers are permitted only for the auth BFF, the same-origin `/api/*` proxy, and trivial SSR proxy-fetches. No DB access, no provider secrets, no access/scoring logic. |
| **GR-5** | **Token handling.** Access JWT in browser memory only (never `localStorage`/`sessionStorage`); refresh token in an httpOnly/Secure/SameSite=Lax cookie, rotating, with reuse detection. |
| **GR-6** | **Certificates are immutable and score-based.** Issued from a submitted attempt; never mutated. A granted retake issues a **new** certificate. |
| **GR-7** | **Progress and attempts are never hard-deleted.** Revoking enrollment changes access only; `watch_progress`, `attempts`, `proctor_events`, `certificates` persist indefinitely. |
| **GR-8** | **Linear lock is earned, never skipped.** Completion is idempotent and non-retroactive — it never un-completes. |
| **GR-9** | **Secrets** live only in server-side configuration. Never in the repository, never in the client bundle. |
| **GR-10** | **Account deletion** = soft-delete + scheduled anonymisation (UU PDP); financial/audit rows retained with anonymised links. |
| **GR-11** | **The answer key never leaves the server.** Student-facing DTOs omit correct answers. Scoring is server-side only. |
| **GR-12** | **Timers and retake caps are server-authoritative.** `started_at` is server-stamped; a late submit is auto-submitted and scored as-of expiry. Client clocks are never trusted. |
| **GR-13** | **Proctor events are advisory data, not authority.** The client reports; the server counts strikes and decides. |
| **GR-14** | **"TOEFL Prediction", never an official score.** TOEFL is an ETS trademark. Never describe soft proctoring as tamper-proof. |

---

# 8. METODE PENGADAAN DAN METODE EVALUASI (DELIVERY AND EVALUATION METHOD)

### a. Delivery method
Incremental milestone delivery (§12). Each milestone is independently verifiable and leaves the
system in a working, deployable state. Backend precedes frontend within each milestone.

**Engineering constraints (binding):**
- Backend: .NET 10, 4-layer clean architecture, `Nullable` enabled, `TreatWarningsAsErrors` on;
- Layer dependency direction: Domain depends on nothing; Application depends on Domain;
  Infrastructure and Api depend inward. **Never** the reverse;
- Minimal APIs use `TypedResults` so the OpenAPI document carries response schemas;
- The frontend API client is **generated** from the OpenAPI document — never hand-written;
- Migrations are generated by `dotnet ef` and never hand-edited to diverge from the model snapshot.

### b. Evaluation method (acceptance)
A milestone is accepted only when **all** of the following hold:

1. `dotnet build` completes with **0 warnings, 0 errors**;
2. `dotnet test` passes **100%** — no skipped or quarantined tests;
3. The frontend type-checks (`tsc --noEmit`) and builds;
4. Every acceptance criterion listed for that milestone in §12 is demonstrated on a running
   Docker deployment, not merely in unit tests;
5. `dotnet ef migrations has-pending-model-changes` reports no drift;
6. No golden rule (§7) is violated.

---

# 9. RUANG LINGKUP PEKERJAAN (SCOPE OF WORK)

> This is the normative section. Everything required to build the system is specified here.

## 9.1 System architecture

```
Browser ──HTTPS──▶ Cloudflare Tunnel ──▶ Next.js (frontend, single public origin)
                                            │
                    /api/auth/*, /api/revalidate ── handled by Next (auth BFF)
                    all other /api/*             ── proxied ▼
                                            └──▶ .NET 10 API ──▶ PostgreSQL
                                                     │
                                     Bunny Stream · Xendit · SES · R2
```

**Single-origin rule.** The browser only ever addresses the frontend origin. The frontend is built
with `NEXT_PUBLIC_API_BASE_URL=""` so client API calls are relative, and Next proxies `/api/*` to the
API over the internal network. No public API hostname; no CORS surface.

### Backend layers
| Layer | Contents |
|---|---|
| **Api** | Minimal-API endpoints, authentication, OpenAPI, webhook endpoints, rate limiting, RFC-7807 problem details. |
| **Application** | Use-case services, DTOs, FluentValidation validators, port interfaces: `IVideoProvider`, `IPaymentGateway`, `IEmailSender`, `IObjectStorage`. |
| **Domain** | Entities, enums, access rule, completion policy, scoring rules, state machines. **No EF or HTTP dependencies.** |
| **Infrastructure** | EF Core (PostgreSQL/Npgsql), provider adapters, background jobs. |

### Key services
| Service | Responsibility |
|---|---|
| `ISessionAccessService` | **The gate.** Every protected resource calls it. Enrolled AND unlocked. |
| `ISessionCompletionService` | The **only** component that completes a session and advances the lock. Idempotent, non-retroactive. |
| `IEnrollmentService` | Checkout, enrollment lookup, `IsEnrolledAsync`. |
| `IProgramService` | Public program view; student program view with per-session lock state. |
| `IAssessmentService` | Attempt lifecycle, question serving (without answers), submission, scoring. |
| `IProctorService` | Event ingestion, grace rules, strike counting, auto-submit, admin reinstate. |
| `IScoreConversionService` | Raw → scaled section scores → total → predicted band. |
| `ICertificateService` | Score-based issuance, PDF, email, public verification. |
| `IProgramAdminService`, `IQuestionBankService`, `IAttendanceService` | Admin operations. |

### Dormant subsystems (retained, not deleted)
`ICatalogService`, `IEntitlementService`, `ISubscriptionService`, the quiz services, and notification
preferences remain compiled but unreachable from product flows. Their tables are **never dropped**.
Do not extend them.

## 9.2 Authentication (inherited unchanged)

Delivered and in production. Specified here only so no assumption is left implicit.

1. Email/password registration and login; Google SSO wired but disabled pending credentials;
2. **Email verification is mandatory before purchase** — enforced by the `EmailVerified` policy on
   the enrollment endpoint;
3. Access token: JWT, browser memory only. Refresh token: httpOnly/Secure/SameSite=Lax cookie,
   **rotating**, with **reuse detection** (a reused token invalidates the family);
4. Password change/reset via single-use tokens; logout-all;
5. Roles: `User`, `Admin`, `SuperAdmin`. Policy `Admin` accepts Admin+SuperAdmin; policy `SuperAdmin`
   is required only for changing another user's role;
6. Account deletion: soft-delete + scheduled anonymisation (UU PDP compliance).

## 9.3 Program structure and the linear lock

### 9.3.1 Structure
```
Program
 ├── ProgramBatch (optional, dated cohort)
 └── ProgramSession 1..N  (ordered by order_index, unique per program)
      ├── type = Video            → Bunny video + optional gating Assessment
      ├── type = Live             → scheduled meeting + admin-marked attendance
      └── type = FinalAssessment  → the 3-section proctored assessment
```

**R1.** A program is an ordered list of sessions. Count and type mix are **fully admin-configurable**
— no session count is hard-coded anywhere.

**R2 — Linear lock.** Session *k+1* is unlocked only when session *k* is complete:

| Session type | Complete when |
|---|---|
| `Video` | `watch_percent >= 90` **AND** (no gating assessment **OR** a passing attempt exists) |
| `Live` | An admin has marked the learner attended |
| `FinalAssessment` | An attempt has been submitted |

**R3 — Batches are optional.** `enrollments.batch_id` is **nullable**. A program may run as a dated
cohort (shared live schedule) or accept continuous self-paced enrollment. Both are supported
simultaneously.

**R4 — Locked sessions are visible but not actionable.** The list endpoint returns every session with
its lock state; only resource endpoints enforce access.

**R5 — Completion is idempotent and non-retroactive.** Attaching a gating test to an
already-completed session must never un-complete it.

### 9.3.2 Reference implementation (binding)
```csharp
canAccess(user, session) = isEnrolled(user, session.ProgramId)
                        && (session.OrderIndex == firstOrderIndex
                            || isCompleted(user, previousSession))
```
`SessionAccess.CanAccess` and `SessionAccess.IsSessionComplete` are pure Domain functions with no EF
or HTTP dependency, and are unit-tested exhaustively.

## 9.4 Enrollment and payment

1. **Landing** — a public, statically-generated program page presenting syllabus, schedule, price and
   outcomes, with a "Daftar" call to action;
2. **Precondition** — purchase requires an authenticated account with a **verified email**;
3. **Checkout** — `POST /api/programs/{id}/enroll` creates:
   - an `enrollments` row with status `PendingPayment` (**grants nothing**),
   - a `payment_transactions` row with kind `ProgramPurchase`,
   - a **single** Xendit invoice via `IPaymentGateway`,
   and returns the hosted invoice URL;
4. **Grant** — enrollment becomes `Active` **only** in the verified webhook handler (GR-2), which
   also stamps `enrolled_at` and `amount_paid_idr` and sends the receipt email;
5. **Idempotency** — webhook replay is deduplicated on `webhook_events.external_id`. A replay must
   produce exactly one enrollment and exactly one receipt email;
6. **Success page** — informational only; states that access appears once payment is confirmed;
7. **Revocation** — an admin may revoke an enrollment. This removes access and **deletes nothing**.

### Enrollment state machine (binding)
```
PendingPayment ──(verified webhook)──▶ Active ──(program finished)──▶ Completed
       │                                  │                              │
       └──────────────▶ Revoked ◀─────────┴──────────────────────────────┘
```
Only `Active` and `Completed` grant access. There is **no** transition out of `Revoked`.

## 9.5 Video sessions

1. Playback uses the delivered player: signed Bunny HLS, resume position, quality selector, Bahasa
   Indonesia captions;
2. `POST /api/sessions/{id}/playback` mints a short-TTL signed URL **after** `ISessionAccessService`
   approves. Default TTL **1800 seconds**;
3. Progress is persisted in `watch_progress` keyed by `session_id`, saved on a throttle (every 5s of
   playback, on pause, and on page-hide);
4. `percent_complete` is **monotonic** — seeking backwards never reduces it;
5. On reaching the 90% threshold, the gating test (if any) unlocks **on the same page**;
6. `provider_asset_id` is **never** included in any student-facing DTO.

## 9.6 Gating tests

1. A gating test is an `Assessment` with `kind = Gating`, linked from a video session;
2. **Fully admin-configurable via the admin UI** — the administrator selects questions from the bank
   and sets the pass threshold and retake cap per test. **No question count is fixed in code**;
3. **Seed defaults** (used by `ProgramSeeder`, changeable in admin without a deployment):

   | Setting | Seed default |
   |---|---|
   | Questions per gating test | 5 |
   | Pass threshold | 4 of 5 |
   | Retake cap | Unlimited (`null`) |
   | Time limit | None |
   | Proctoring | Disabled |

4. Scoring is instant and server-side. A passing attempt satisfies the gating half of §9.3 R2;
5. Every attempt is recorded (score, timestamps, submitted answers) and retained (GR-7);
6. Rationale for unlimited retakes by default: the learner has already paid; a hard block converts
   directly into a support burden. An administrator may still cap any individual test.

## 9.7 Final assessment — TOEFL ITP format

### 9.7.1 Structure (binding)
The final assessment follows the **TOEFL ITP Level 1** format:

| # | Section | Questions | Time limit | Scaled range |
|---|---|---|---|---|
| 1 | Listening Comprehension | 50 | 35 minutes | 31–68 |
| 2 | Structure and Written Expression | 40 | 25 minutes | 31–68 |
| 3 | Reading Comprehension | 50 | 55 minutes | 31–67 |
| | **Total** | **140** | **115 minutes** | **310–677** |

> **Reconciliation.** This supersedes the earlier 4-section draft (which split Vocabulary out).
> Vocabulary is assessed **within Reading Comprehension**, as in the real ITP. `QuestionSection`
> retains a `Vocabulary` member for tagging bank items, but it is **not** a separately scored section.

**All question types are multiple choice.** "Written Expression" in the ITP sense is
error-identification MCQ — it is *not* free-text. There is no essay in v1, which is what preserves
instant scoring.

### 9.7.2 Rules (binding)
1. **One attempt** by default. `retake_cap = 1`. An administrator may grant a retake, which creates a
   **new** attempt and, on submission, a **new** certificate (GR-6);
2. **Per-section timers**, server-authoritative. `started_at` is stamped by the server on attempt
   creation. A submission arriving after expiry is accepted but **auto-submitted and scored as-of
   expiry** (`auto_submitted = true`);
3. **Sections are sequential and non-returnable** — once a section is submitted or its timer expires,
   the learner advances and cannot go back;
4. **Incremental answer saving** — `POST /api/attempts/{id}/answers` persists answers as the learner
   works, so a browser crash or tab close does not lose the sitting;
5. **Listening audio** is served via short-TTL signed URL from R2, with an admin-set per-question
   play limit (**seed default: 1 play**), enforced server-side by counting play grants;
6. **Proctoring is enabled** for the final assessment (§9.8) and disabled for gating tests by default.

## 9.8 Soft proctoring

### 9.8.1 Mechanism
1. The client listens for `visibilitychange` (Page Visibility API) and `window.blur`;
2. **Grace rule:** focus losses shorter than **2 seconds** are suppressed and never reported — this
   absorbs OS notifications and accidental focus changes;
3. Qualifying events are POSTed to `/api/attempts/{id}/proctor-events`;
4. The **server** timestamps, persists, and counts strikes, then returns the authoritative state:

   | Strike | Server action | Learner sees |
   |---|---|---|
   | 1 | Record, return `warn` | Full-screen warning overlay: *"Jangan tinggalkan halaman tes."* |
   | 2 | Record, set `auto_submitted = true`, `proctor_flagged = true`, finalise scoring | Attempt ends; score screen shown |

5. Before the assessment starts, the learner is shown the rule and the strike count **explicitly**;
6. Fullscreen is **requested but not enforced** — enforcement is unreliable across browsers and
   creates false positives.

### 9.8.2 Dispute handling
1. Every event is retained with its timestamp and client metadata (GR-7);
2. An administrator can **reinstate** a flagged attempt (`POST /api/admin/attempts/{id}/reinstate`),
   which sets `reinstated = true`, clears the flag, and is audit-logged — **the event trail is never
   erased**.

### 9.8.3 Stated limitation (must appear in UI copy and admin documentation)
This is **deterrence, not exam security**. It is client-side, defeatable with a second device, and
cannot *prevent* a learner from leaving the page — only detect it. It must **never** be described as
tamper-proof or as equivalent to supervised examination conditions (GR-14).

## 9.9 Scoring and TOEFL prediction

### 9.9.1 Raw scoring
Each question is worth 1 point; there is no negative marking. Unanswered questions score 0. Raw
section scores are therefore Listening 0–50, Structure 0–40, Reading 0–50.

### 9.9.2 Scaled conversion
Each raw section score converts to a scaled score via an **admin-maintained conversion table**:

```
score_band_mappings(program_id, section, min_raw, max_raw, scaled_score)
```

### 9.9.3 Total (binding formula)
Per the TOEFL ITP convention:

```
Total = ROUND( (Listening_scaled + Structure_scaled + Reading_scaled) × 10 / 3 )
```
Valid total range: **310–677**.

### 9.9.4 Failure behaviour (binding)
If any raw score falls outside every configured mapping row, the system **must fail loudly** — log an
error and surface an explicit "score conversion unavailable" state to admin. It must **never** emit a
blank, zero, or guessed band. A certificate must **not** be issued in this state.

### 9.9.5 Content dependency
The conversion values are **`[OWED BY BUSINESS]`**. ETS's actual conversion tables are proprietary;
the Company must derive and be able to defend its own approximation. Engineering builds the
mechanism and seeds a **clearly-marked placeholder** table. **The prediction feature has no valid
content until this is supplied** — this blocks launch, not implementation.

### 9.9.6 Mandatory disclaimer
The result screen, the certificate PDF, and the public verification page must each state plainly
that the score is an **INVERTA prediction, not an official ETS TOEFL score**. TOEFL is a registered
trademark of ETS. Legal review of naming and claims is **`[OWED BY BUSINESS]`** before public launch.

## 9.10 Certificate and verification

1. **Trigger** — successful submission of the final assessment (subject to §9.9.4);
2. **Contents** — learner name, program name, issue date, per-section scaled scores, total score,
   predicted band, unique verification code, and the §9.9.6 disclaimer;
3. **Generation** — PDF via PDFsharp/MigraDoc with **embedded DejaVu fonts** (required: the Linux
   container has no system fonts);
4. **Delivery** — emailed automatically via SES and downloadable in-app;
5. **Verification code** — format `INV-XXXXXXXX`, cryptographically random, from an unambiguous
   alphabet (no `0`/`O`, `1`/`I`);
6. **Public verification** — `/verify/{code}` server-rendered, no authentication, showing validity,
   name, program, scores, band, and the disclaimer. An unknown code returns a valid-`false` response,
   never an error page;
7. **Immutability** — anchored to `attempt_id`. A granted retake inserts a **new** certificate row;
   the earlier certificate is never modified or invalidated. There is deliberately **no** unique
   constraint on `(user_id, program_id)`.

## 9.11 Live sessions

Deliberately thin in v1.

1. A live session stores: scheduled datetime, mode (`Zoom`/`Offline`), join URL or physical location;
2. It appears in the learner's program view with its schedule;
3. A reminder email is sent **H-1** (24 hours before);
4. **No Zoom API integration.** The join URL is a link;
5. **Attendance is admin-marked**, individually or in bulk. Marking attendance completes the session
   and advances the lock;
6. An administrator may waive attendance (mark all) to unblock a cohort.

> Automated attendance capture is **`[OWED BY BUSINESS]`** as a decision (§17); if required, it is a
> post-v1 enhancement and does not change this data model.

## 9.12 Administration

All administrative capability is required in v1 — operations must not depend on engineering.

| Area | Capability |
|---|---|
| **Programs** | Create, edit, publish/unpublish, archive; set price; set slug. |
| **Sessions** | Create, edit, **reorder**, delete; set type and per-type fields; attach an assessment. |
| **Batches** | Create dated cohorts; set status. |
| **Question bank** | Full CRUD; tag by section and skill; attach listening audio and reading passages; import is out of scope for v1. |
| **Assessments** | Compose from the bank; set order; set pass threshold, retake cap, per-section time limits, proctoring on/off, audio play limit. |
| **Score conversion** | Maintain the per-section raw→scaled table (§9.9.2). |
| **Attendance** | Mark attendance for a live session, individually or in bulk. |
| **Enrollments** | List, filter, inspect; revoke; manually grant (support/refund path). |
| **Attempts** | List, filter by flagged; inspect answers and the proctor event trail; **reinstate**. |
| **Audit** | Every administrative mutation writes an `audit_logs` row with actor, action, target, metadata. |

> **Delivered 2026-08-20 (A3).** Assessment authoring and score-conversion editing originally
> shipped as API-only — the admin UI for them was added by the A3 project, together with a
> publish-blocking readiness check. See
> `docs/superpowers/specs/2026-08-20-admin-content-authoring-design.md`.
>
> Still API-only, and not editable from any admin screen: the proctoring on/off flag and the
> audio play limit (both written as fixed values when an assessment is created). Listening
> audio is no longer among them — it is uploaded from the question form and from the
> assessment composer's Listening section, and a Listening section with no playable audio now
> blocks publishing. The table rows above describe the capability, not the surface that
> exposes it.

Publishing a program triggers on-demand revalidation of its static landing page.

## 9.13 Data model

Conventions: PostgreSQL, `snake_case`, `uuid` v7 application-assigned primary keys, `timestamptz`
(UTC), enums stored **as text**, money `decimal(18,2)`, JSON bags `jsonb`.

### 9.13.1 New tables
```
programs(id, name, slug UNIQUE, description, summary, price_idr, status,
         published_at, created_at, updated_at)

program_batches(id, program_id→programs CASCADE, name, start_date, status, ...)

program_sessions(id, program_id→programs CASCADE, order_index, type, title, description,
         provider_asset_id, duration_seconds,                    -- type=Video
         scheduled_at, live_mode, join_url, location,            -- type=Live
         assessment_id→assessments SET NULL,
         UNIQUE(program_id, order_index))

enrollments(id, user_id→users RESTRICT, program_id→programs RESTRICT,
         batch_id→program_batches SET NULL, status, amount_paid_idr,
         provider_ref, enrolled_at, UNIQUE(user_id, program_id))

session_completions(id, user_id→users RESTRICT, session_id→program_sessions RESTRICT,
         completed_at, method, UNIQUE(user_id, session_id))

live_attendances(id, session_id→program_sessions RESTRICT, user_id→users RESTRICT,
         attended, marked_by_user_id→users SET NULL, UNIQUE(session_id, user_id))

assessments(id, kind, title, config jsonb)

questions(id, section, type, prompt, choices jsonb, correct jsonb,
         audio_ref, passage_ref, tags jsonb)

assessment_questions(id, assessment_id→assessments CASCADE,
         question_id→questions RESTRICT, order_index,
         UNIQUE(assessment_id, order_index))

attempts(id, user_id→users RESTRICT, assessment_id→assessments RESTRICT,
         started_at, submitted_at, auto_submitted, proctor_flagged, reinstated,
         answers jsonb, section_scores jsonb, total_score, max_score, passed)

proctor_events(id, attempt_id→attempts CASCADE, kind, occurred_at, client_meta jsonb)

score_band_mappings(id, program_id→programs CASCADE, section, min_raw, max_raw,
         scaled_score, predicted_band)
```

### 9.13.2 Altered tables (additive only)
```
watch_progress:  module_id  → NULLABLE            (dormant catalog target)
                 session_id → NEW, NULLABLE, FK program_sessions RESTRICT
                 CHECK ( (module_id IS NULL) <> (session_id IS NULL) )
                 UNIQUE(user_id, module_id)  WHERE module_id  IS NOT NULL
                 UNIQUE(user_id, session_id) WHERE session_id IS NOT NULL

certificates:    level_id       → NULLABLE        (dormant)
                 program_id     → NEW, NULLABLE, FK programs RESTRICT
                 attempt_id     → NEW, NULLABLE, FK attempts RESTRICT
                 section_scores → NEW jsonb
                 predicted_band → NEW text
                 UNIQUE(user_id, level_id) WHERE level_id IS NOT NULL
                 -- deliberately NO unique on (user_id, program_id)

payment_transactions.kind: gains ProgramPurchase
```

**Rationale for reusing `watch_progress`** rather than creating a parallel table: the delivered
player, resume logic, and completion policy all operate on it. Repointing preserves that code and
honours GR-7.

### 9.13.3 Foreign-key policy (binding)
| Behaviour | Applies to |
|---|---|
| **RESTRICT** | Retained / financial / integrity rows: enrollments, attempts, session completions, certificates, attendance, and their user references. |
| **CASCADE** | Intra-aggregate children: sessions and batches under a program, assessment questions, proctor events, score mappings. |
| **SET NULL** | Optional actor and optional links: `marked_by_user_id`, `batch_id`, `assessment_id`. |

**No table belonging to the archived product may be dropped.** Migrations are additive only.

## 9.14 API surface

Authentication column: `—` public · `A` authenticated · `E` authenticated + email-verified ·
`N` enrolled + unlocked (via `ISessionAccessService`) · `ADM` admin role.

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/api/programs/{slug}` | — | Public program + syllabus (landing) |
| POST | `/api/programs/{id}/enroll` | E | Create invoice. **Grants nothing.** Rate-limited. |
| GET | `/api/me/enrollments` | A | Learner's enrollments |
| GET | `/api/me/programs/{id}` | A | Sessions with per-session lock state and progress |
| POST | `/api/sessions/{id}/playback` | N | Mint signed video URL. Rate-limited. |
| GET | `/api/sessions/{id}/progress` | N | Read progress |
| PUT | `/api/sessions/{id}/progress` | N | Save progress (monotonic percent) |
| GET | `/api/sessions/{id}/assessment` | N | Questions **without** correct answers |
| POST | `/api/assessments/{id}/attempts` | N | Start attempt (server stamps `started_at`) |
| POST | `/api/attempts/{id}/answers` | N | Incremental answer save |
| POST | `/api/attempts/{id}/submit` | N | Submit → score → unlock / issue certificate |
| POST | `/api/attempts/{id}/proctor-events` | N | Report event; returns authoritative strike state |
| GET | `/api/attempts/{id}/result` | N | Section scores, total, predicted band |
| GET | `/api/attempts/{id}/audio/{questionId}` | N | Signed listening-audio URL; enforces play limit |
| GET | `/api/me/certificates` | A | Learner's certificates |
| GET | `/api/certificates/{id}/pdf` | A | Certificate PDF (owner only) |
| GET | `/api/certificates/verify/{code}` | — | Public verification |
| POST | `/api/webhooks/xendit` | — | Signature-verified, idempotent. **Sole grantor of enrollment.** |
| CRUD | `/api/admin/programs`, `/sessions`, `/batches` | ADM | Program authoring |
| CRUD | `/api/admin/questions`, `/assessments` | ADM | Question bank and test composition |
| CRUD | `/api/admin/programs/{id}/score-bands` | ADM | Score conversion table |
| POST | `/api/admin/sessions/{id}/attendance` | ADM | Mark attendance (bulk supported) |
| GET | `/api/admin/enrollments`, `/attempts` | ADM | Operational dashboards |
| POST | `/api/admin/attempts/{id}/reinstate` | ADM | Clear proctor flag; trail retained |

**Error format:** RFC-7807 problem details. No stack traces are ever returned to a client.
**Rate limiting:** authentication, enrollment/payment, playback, and assessment-submission endpoints
are rate-limited per client IP.

## 9.15 Frontend

| Route | Rendering | Purpose |
|---|---|---|
| `/` | SSG | Program-led landing page |
| `/program/{slug}` | SSG/ISR | Full syllabus, schedule, price, CTA. Revalidated on publish. |
| `/checkout/success` | CSR | Informational only. **Grants nothing.** |
| `/verify/{code}` | SSR | Public certificate verification |
| `/app/program/{id}` | CSR | Ordered sessions with lock states; next-step CTA |
| `/app/session/{id}` | CSR | Player + gating test on one page |
| `/app/assessment/{id}` | CSR | Timed sectional runner + proctor listener |
| `/app/certificates` | CSR | Certificate list and download |
| `/admin/*` | CSR | All administration (§9.12) |

**Requirements:** all UI strings in Bahasa Indonesia; light mode only; WCAG 2.1 AA (visible focus,
keyboard navigation, captions, accessible audio controls); server state via TanStack Query;
the generated API client only. The assessment runner must **never** prefetch or cache answer data.

**Dormant routes** (`/catalog`, `/pricing`, subscription screens) remain in the repository but are
removed from navigation.

## 9.16 Non-functional requirements

| Attribute | Requirement |
|---|---|
| **Scale** | 20–50 enrollments/month; design target 100 concurrent assessment sittings. |
| **Availability** | Best-effort single-node. Assessment submission must survive an API restart — answers are persisted incrementally (§9.7.2). |
| **Performance** | Program view and assessment question load < 1s at target scale. Signed URL minting < 300 ms. |
| **Data residency / privacy** | UU PDP: soft-delete + scheduled anonymisation; financial and audit rows retained with anonymised links. |
| **Security** | No secrets in the repository or client bundle; media only via short-TTL signed URLs; answer keys server-side only; all access checks server-side. |
| **Backup** | Database volume backed up before every migration. Attempts and certificates are irreplaceable. |
| **Observability** | Structured logs for: enrollment grants, webhook processing, attempt lifecycle, proctor decisions, score-conversion failures, certificate issuance. |
| **Localisation** | UI Bahasa Indonesia; assessment content English (it is an English test). |

## 9.17 Explicitly out of scope for v1

Free-text/essay grading · adaptive or randomised test assembly · Zoom API attendance · question-bank
bulk import · mobile native applications · offline mode · multi-language UI · in-app notifications
and preference management · recurring billing of any kind · discount codes and referrals ·
learner-to-learner or forum features · AI tutoring or feedback.

---

# 10. SPESIFIKASI TEKNIS (TECHNICAL SPECIFICATION)

| No | Item | Specification |
|---|---|---|
| 1 | Backend runtime | .NET 10, ASP.NET Core Minimal APIs, 4-layer clean architecture |
| 2 | Frontend runtime | Next.js (App Router), React, TypeScript, Tailwind |
| 3 | Database | PostgreSQL 17, EF Core 10 + Npgsql, `snake_case`, uuid v7 PKs |
| 4 | Video delivery | Bunny Stream via `IVideoProvider`; signed HLS; TTL 1800s |
| 5 | Audio delivery | Cloudflare R2 via `IObjectStorage`; signed URL; server-enforced play limit |
| 6 | Payments | Xendit via `IPaymentGateway`; single invoice; **webhook-only** entitlement |
| 7 | Email | Amazon SES via `IEmailSender`; transactional only |
| 8 | Certificate PDF | PDFsharp/MigraDoc with embedded DejaVu fonts |
| 9 | Authentication | JWT access (memory) + rotating refresh cookie with reuse detection; BFF pattern |
| 10 | Authorization | Policies: `EmailVerified`, `Admin`, `SuperAdmin`; plus `ISessionAccessService` on every protected resource |
| 11 | Gating test | Admin-configurable question count and pass threshold; seed default 5 questions, pass 4, unlimited retakes, no timer |
| 12 | Final assessment | 3 sections — Listening 50Q/35min, Structure 40Q/25min, Reading 50Q/55min; 140Q; 115min; 1 attempt |
| 13 | Score range | Section scaled 31–68 / 31–68 / 31–67; Total = (L+S+R)×10÷3; range 310–677 |
| 14 | Question types | Multiple choice only (v1) |
| 15 | Timer authority | **Server** — `started_at` server-stamped; late submit auto-submitted, scored as-of expiry |
| 16 | Proctoring | Page Visibility API + window blur; <2s suppressed; strike 1 warn, strike 2 auto-submit + flag; server-authoritative |
| 17 | Retry / lock | Gating: unlimited (admin-cappable). Final: 1 attempt; admin-granted retake issues a **new** certificate |
| 18 | Certificate code | `INV-XXXXXXXX`, cryptographically random, unambiguous alphabet, UNIQUE |
| 19 | Data retention | `watch_progress`, `attempts`, `proctor_events`, `certificates` retained indefinitely; never hard-deleted |
| 20 | Migrations | `dotnet ef` generated; additive only; no DROP of archived-product tables |
| 21 | API documentation | OpenAPI emitted at build; frontend client generated from it |
| 22 | Error format | RFC-7807 problem details; no stack traces to clients |
| 23 | Deployment | Docker Compose; single public origin via Cloudflare Tunnel; API and database not published to host |
| 24 | Environments | Local development, and a tunnel-fronted deployment (staging/production topology identical) |
| 25 | Accessibility | WCAG 2.1 AA; light mode only |
| 26 | UI language | Bahasa Indonesia |

---

# 11. PRODUK YANG DIHASILKAN (DELIVERABLES)

1. **Running system** — the pivoted application deployed on Docker behind the existing tunnel,
   comprising the program/enrollment subsystem, the assessment engine, proctoring, scoring,
   certification, and the administration surface;
2. **Database migration** — additive, reversible, applied and verified against a database containing
   pre-existing rows;
3. **Automated test suite** — covering every critical path in §16.3, passing 100%;
4. **Generated API client + OpenAPI document** — the frontend's only source of API types;
5. **Administration capability** — sufficient for operations to run the product without engineering;
6. **Seed data** — a placeholder program, a placeholder score-conversion table, and a development
   administrator account, all clearly marked;
7. **Documentation** — this document, the deployment guide (`DEPLOY.md`), and the decision log
   (`docs/DECISIONS.md`);
8. **Verification evidence** — a demonstrated end-to-end run: enroll → watch → pass gating test →
   sit final assessment → receive score → receive certificate → verify publicly.

---

# 12. WAKTU PELAKSANAAN (IMPLEMENTATION MILESTONES)

Delivery is milestone-based rather than date-based; each milestone is independently acceptable and
leaves a working system.

| M | Milestone | Contents | Acceptance |
|---|---|---|---|
| **M0** | Foundation | *Delivered.* Schema delta applied as an additive migration. | No model drift; pre-existing rows survive; no archived table dropped. |
| **M1** | Authentication | *Delivered, inherited in full.* | Existing auth tests pass. |
| **M2** | Program & enrollment | Program/session/batch model, admin CRUD, SSG landing, one-time invoice, webhook→enrollment, receipt email, student program view with lock states. | §3 targets 1 and 2; unpaid user blocked; webhook replay idempotent. |
| **M3** | Video sessions & gating tests | Playback + progress on sessions, question bank CRUD, gating test authoring, instant scoring, unlock. | Watching 100% with an unpassed test does **not** unlock; passing does. |
| **M4** | Final assessment, proctoring & certificate | 3-section timed engine, listening audio, proctoring + event log + reinstate, score conversion, certificate PDF + email + verification. **← the program becomes sellable.** | §3 targets 3, 4, 5. Late submit auto-submitted; 2 strikes auto-submit. |
| **M5** | Live sessions & admin polish | Live schedule, H-1 reminder, attendance marking, enrollment/attempt dashboards, revoke and manual-grant. | §3 target 6. |
| **M6** | Static, legal & onboarding | Terms, privacy, **refund policy for a one-time purchase**, contact, FAQ, onboarding tour. | All legal pages present and consistent with a one-time purchase. |

### Delivery status (2026-08-14)

**All milestones M0–M6 are delivered and verified on a running deployment.** 193 automated tests
pass. What remains before launch is **content and legal sign-off, not engineering** — see §17.

| M | Status | Evidence |
|---|---|---|
| M0–M1 | ✅ Inherited | Existing auth/foundation tests pass. |
| M2 | ✅ Delivered | Unpaid → 403; verified webhook → Active; replay idempotent; session 1 available, rest locked. |
| M3 | ✅ Delivered | Answer key absent from payload; 100% watch alone does not complete a gated session; passing unlocks exactly the next. |
| M4 | ✅ Delivered | Sectional timers enforced server-side; 2 strikes auto-submit + flag; reinstate keeps trail; 3/3 → scaled 510 → certificate → PDF → public verify. |
| M5 | ✅ Delivered | Attendance completes the live session and unlocks the next; unmark does not un-complete; reminder sent once across repeated sweeps; manual grant audited. |
| M6 | ✅ Delivered | All five legal pages consistent with a one-time purchase; FAQ states "sekali bayar" and "bukan skor TOEFL resmi"; archived routes excluded from sitemap and blocked in robots. |

**Critical path:** M2 → M3 → M4. The product cannot be sold before M4 completes.

---

# 13. PERSYARATAN (PREREQUISITES)

*(Adapted from vendor requirements: what must exist for implementation to proceed.)*

### a. Platform prerequisites — satisfied
1. .NET 10 SDK, Node.js, Docker with Compose;
2. PostgreSQL 17 container with a persistent volume;
3. Delivered and working: authentication, payment webhook plumbing with idempotency ledger, video
   provider abstraction, email abstraction, certificate PDF generation with embedded fonts, and the
   single-origin tunnel deployment.

### b. External service prerequisites — required before **production launch**
| Service | Required for | Status |
|---|---|---|
| Xendit production credentials + webhook secret | Real payments | Currently a development simulator |
| Bunny Stream account + uploaded video assets | Real lessons | Currently a development simulator |
| Amazon SES production access (out of sandbox) | Learner emails | Currently logged to console |
| Cloudflare R2 bucket + credentials | Listening audio | Not yet configured |

> **All four are configuration swaps, not code changes.** Each provider sits behind an interface
> with a development simulator selected by configuration. Implementation is **not blocked** by their
> absence.

### c. Content prerequisites — **`[OWED BY BUSINESS]`**
Real syllabus and video assets · question bank (140 items minimum for one complete final assessment,
plus gating-test items per lesson) · listening audio files · reading passages · the score conversion
table (§9.9.5).

---

# 14. TENAGA AHLI YANG DIBUTUHKAN (ROLES REQUIRED)

*(Adapted: capability required to implement, not personnel to be procured.)*

| Role | Responsibility | Required capability |
|---|---|---|
| **Backend engineer** | Domain rules, assessment engine, scoring, access control, migrations | .NET 10, EF Core, PostgreSQL, clean architecture, payment webhook handling |
| **Frontend engineer** | Program view, player integration, assessment runner, proctor listener, admin UI | Next.js App Router, TypeScript, TanStack Query, accessibility (WCAG 2.1 AA) |
| **QA** | Verification of every §16.3 critical path; adversarial testing of the access gate and timers | Integration testing, API testing, negative-path and boundary testing |
| **Content/academic** | Syllabus, question bank, score conversion values | TOEFL ITP format expertise; test-item writing |
| **Operations** | Programs, batches, live sessions, attendance, dispute review | Uses the admin surface; requires no engineering skill |

---

# 15. TAHAPAN PENERIMAAN (RELEASE GATES AND ACCEPTANCE)

*(Adapted from contract type and payment method: the gates at which work is accepted.)*

| Gate | Corresponds to | Accepted when |
|---|---|---|
| **Gate I** | M2 complete | A learner can purchase the program and see the correctly-locked syllabus. Enrollment is provably impossible without a verified webhook. |
| **Gate II** | M3 complete | A learner can watch a lesson, take its gating test, and unlock the next session — and provably cannot skip it. |
| **Gate III** | M4 complete | A learner can sit the final assessment under proctoring, receive an instant score, and receive a verifiable certificate. **The product is sellable.** |
| **Gate IV** | M5 + M6 complete | Operations can run the product unaided; all legal pages are consistent with a one-time purchase. |

At every gate, the §8.b evaluation criteria must hold in full.

---

# 16. SLA, QUALITY GATES AND DEFINITION OF DONE

*(Adapted from sanctions and penalties: the quality bar and how it is measured.)*

## 16.1 Service level targets

| Aspect | Target | Measurement |
|---|---|---|
| Application availability | 99.0% monthly (single-node best effort) | Uptime of the public origin |
| Assessment submission success | **100%** — an in-progress sitting must never be lost | Incremental answer persistence; verified by restart testing |
| Signed URL minting | < 300 ms (p90) | Server-side timing |
| Score → certificate latency | < 30 seconds from submission | End-to-end timing |
| Payment webhook processing | Idempotent, < 5 s | Webhook ledger |

**Availability formula** (measurement period = 1 calendar month):
```
Availability = (Total Service Time − Effective Downtime) / Total Service Time × 100%
```

## 16.2 Incident priority

| Priority | Definition | Response | Resolution |
|---|---|---|---|
| **High** | Learners cannot enroll, cannot sit an assessment, or an in-progress sitting is lost; scoring or certificate issuance is wrong. | 15 minutes | 1 hour |
| **Medium** | A session fails to unlock; playback degraded; admin function unavailable. | 15 minutes | 4 hours |
| **Low** | Cosmetic or non-blocking defect. | 15 minutes | 24 hours |

> A **wrong score or wrongly-issued certificate is always High** — it damages the credibility the
> product exists to provide.

## 16.3 Definition of Done — mandatory automated tests

A milestone is **not** done until its tests pass. These are binding.

**Access and enrollment**
1. Enrollment is granted **only** by a verified webhook; the success page grants nothing;
2. An unpaid or pending user receives 403 on every protected resource;
3. Webhook replay is idempotent — exactly one enrollment, exactly one receipt;
4. Email verification is required before purchase.

**Linear lock**
5. Session *k+1* returns 403 until session *k* completes;
6. Watching 100% of a video with an **unpassed** gating test does **not** complete the session;
7. Passing the gating test completes it and unlocks the next session;
8. Attaching a gating test to an already-completed session does **not** un-complete it.

**Assessment integrity**
9. The student question DTO contains **no** correct-answer field;
10. A submission after timer expiry is auto-submitted and scored as-of expiry;
11. A second final attempt is refused when the cap is reached;
12. An admin-granted retake issues a **new** certificate and leaves the first untouched.

**Proctoring**
13. Two qualifying strikes auto-submit and flag the attempt;
14. A focus loss under 2 seconds is ignored;
15. Reinstating clears the flag but **retains** the full event trail.

**Scoring and certification**
16. Section conversion boundaries convert correctly at every edge;
17. An unmapped raw score **fails loudly** and issues no certificate;
18. The total formula matches `(L+S+R)×10÷3` and stays within 310–677;
19. A certificate verifies publicly; an unknown code returns valid-`false`, not an error.

**Retention and privacy**
20. Revoking an enrollment removes access but deletes no `watch_progress`, `attempts`,
    `proctor_events`, or `certificates`;
21. Account deletion anonymises while retaining financial and audit rows.

## 16.4 Definition of Done — per change
1. Build: 0 warnings, 0 errors; 2. All tests pass; 3. Frontend type-checks and builds;
4. No model drift; 5. No golden rule violated; 6. Verified on a running deployment, not only in tests.

---

# 17. OPEN ITEMS (`[OWED BY BUSINESS]`)

Implementation proceeds without these. Each blocks **launch**, not the build.

| # | Item | Impact if unresolved | Interim behaviour |
|---|---|---|---|
| 1 | **Score → scaled conversion table** (§9.9.5) | **Critical.** The headline prediction feature has no valid content. | Clearly-marked placeholder table; mechanism fully built. |
| 2 | **Legal review of TOEFL naming and claims** (§9.9.6) | **Critical.** Trademark and misrepresentation exposure. | Explicit "prediction, not an official ETS score" disclaimers everywhere. |
| 3 | **Real syllabus and video assets** | Product has no content. | Placeholder program seeded; structure is admin-configurable. |
| 4 | **Question bank content** (≥140 final items + gating items) | Assessment cannot run for real. | Placeholder items; full authoring UI delivered. |
| 5 | **Final program price** | Cannot transact for real. | Rp 1.500.000 marked `[PLACEHOLDER]`; editable in admin. |
| 6 | **Refund policy for a one-time purchase** | Legal page incomplete; support has no rule. | Manual revoke + manual grant paths exist for support. |
| 7 | **Automated live attendance** — required or not | None for v1. | Admin-marked attendance (recommended). |
| 8 | **Production credentials** (Xendit, Bunny, SES, R2) | Cannot go live. | Development simulators; configuration swap only. |

---

# 18. PENUTUP (CLOSING)

This document consolidates the functional and technical specification for INVERTA into a single
implementation-complete reference. It supersedes the earlier draft FSD and TSD-delta for any point of
disagreement, and supersedes the archived AI Productivity Academy specifications for all product
behaviour while inheriting their platform foundations.

**Three properties are treated as non-negotiable throughout**, because the product's value depends
entirely on them:

1. **Progression must be genuinely earned** — if the linear lock can be bypassed, the pedagogy is
   theatre;
2. **The score must be produced honestly and defensibly** — server-side, from a documented
   conversion, with a loud failure rather than a guessed output, and never represented as an official
   ETS result;
3. **Learner records are permanent** — progress, attempts, proctoring evidence and certificates are
   retained indefinitely and never mutated after issuance.

Any proposed change that weakens one of these requires a revision of this document, not a code
review.

<div align="center">

---

**END OF DOCUMENT**

KAK-INVERTA-TOEFL-v1.0 · Fiscal Year 2026

</div>
