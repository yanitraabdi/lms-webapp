# Build decisions (this repository)

Decisions made while building, beyond what the specs already lock. The specs remain the source of
truth; this records repo-level choices and their rationale.

---

## PIVOT — INVERTA TOEFL Preparation Program (2026-08-14)

The product pivoted from "AI Productivity Academy" (tiered subscription AI-skills LMS) to
**INVERTA — a one-time-purchase, linear, test-gated TOEFL-prep program**. See
[`FSD_INVERTA_TOEFL_v0.1.md`](FSD_INVERTA_TOEFL_v0.1.md) and
[`TSD_Delta_INVERTA_v0.1.md`](TSD_Delta_INVERTA_v0.1.md).

### Confirmed by the product owner (resolving FSD §10 blockers)
- **Approach: specs first, then build.** Archive the AI Academy specs, write the TSD-delta + revised
  CLAUDE.md golden rules + M2 spec, *then* implement. Keeps a source-of-truth trail.
- **Q2 Cohorts → optional batches.** `program_batches` exists and `enrollments.batch_id` is
  **nullable**, so a program can run as a dated cohort *or* accept continuous self-paced enrollment.
  Chosen to support both without a later migration.
- **Q4 Retakes → final assessment 1 attempt** (admin-grantable retake issues a **new** certificate,
  never mutating the old); **gating tests unlimited**, admin-cappable per test. Balances prediction
  credibility against not blocking students who have already paid.
- **Q1 Syllabus → configurable + placeholder seed.** Structure stays admin-configurable; a
  placeholder program is seeded to unblock the build. **Real syllabus still owed by the business.**
- **Q10 AI Academy assets → archived, not deleted.** Specs moved to `docs/archive/ai-academy/`;
  subscription/catalog tables and services remain in the codebase as dormant code.

### Implementation choices (mine, pivot)
- **`watch_progress` is reused, not replaced** (FSD §9 intent): `module_id` becomes nullable, a
  nullable `session_id` FK is added, with a CHECK that exactly one is set. Rationale: the delivered
  player/resume/≥90%-completion code all operates on `watch_progress`; repointing keeps M4's player
  intact and honors "never delete progress". The alternative (a parallel `session_progress` table)
  would fork the player logic.
- **`certificates` becomes score-based**: `level_id` nullable (dormant), plus nullable `program_id`,
  `attempt_id`, `section_scores` jsonb, `predicted_band`. **No unique on (user_id, program_id)** —
  a granted retake legitimately inserts a *second* certificate; `attempt_id` is the immutability
  anchor.
- **M7 quiz tables go dormant, not reused.** `assessments`/`questions`/`attempts` supersede
  `quizzes`/`quiz_questions`/`quiz_attempts` because the new engine needs sections, per-section
  timers, audio/passage attachments, proctoring, and per-section scoring. Force-fitting the old
  3-column shape would be worse than a clean parallel model.
- **Migration is additive only** — no DROPs, so the archived product can be revived.

### Gotcha: `Guid.CreateVersion7()` is NOT monotonic within a millisecond
Ordering rows by a uuid-v7 primary key does **not** reproduce insertion order — the sub-millisecond
bits are random, so rows created in the same millisecond sort arbitrarily (but *stably*).
- Discovered via an intermittent (~1-in-3) failure of `Quiz_gates_completion_and_passing_unlocks_it`:
  the dormant quiz path orders questions by `Id` for **both** serving and scoring, so the system is
  self-consistent, but the *test* hard-coded answers assuming authoring order. Fixed by deriving the
  answers from the **served** order, as a real client does.
- **Rule going forward:** any user-visible sequence needs an **explicit `order_index`**, never an id
  or timestamp. The new assessment engine already does this (`assessment_questions.order_index`,
  `program_sessions.order_index`), so it is immune.
- Known caveat, accepted: the **dormant** quiz feature does not preserve admin authoring order. Not
  worth a migration on superseded code (see the M7-tables decision above).

### Gotcha: a clean local build does NOT mean a clean Docker build (NuGet audit)
`dotnet build` locally reused a warm NuGet cache and skipped the vulnerability audit; the Docker
image does a **fresh `dotnet restore`**, which runs the audit and — with `TreatWarningsAsErrors` —
fails on `NU1903`. Two live advisories surfaced this way during M2 and were fixed by pinning the
patched versions directly (CPM `PackageVersion` + a direct `PackageReference` so the pin beats the
transitive resolution):
- `Microsoft.OpenApi` 2.0.0 (via `Microsoft.AspNetCore.OpenApi` 10.0.5) — GHSA-v5pm-xwqc-g5wc,
  vulnerable `<= 2.7.4` → pinned **2.7.5**. (2.0.1 is *also* vulnerable — verified empirically
  rather than assumed; OpenAPI generation re-verified afterwards: 83 paths, 104 schemas.)
- `SSH.NET` 2025.1.0 (via Testcontainers, test-only) — GHSA-q939-rpr3-3284, vulnerable `<= 2025.1.0`
  → pinned **2026.0.0**.
**Rule going forward:** never trust `| tail` on a `docker compose build` to report success — it
masks the exit code. Check for the `Image ... Built` line or the container's image age.

### Still owed by the business (blocking launch, not the build)
Real syllabus · **score→TOEFL-band mapping** (Q6 — ships with a clearly-marked placeholder; the
headline "prediction" feature has no valid content without it) · **legal review of TOEFL
naming/claims** (Q7 — ETS trademark) · refund policy for a one-time purchase (Q8) · final price
(Q9) · whether live attendance must be automated (Q3) · confirm MCQ-only written section (Q5).

---

## Confirmed by the product owner (original, AI Academy — retained for platform context)
- **Sequencing: milestones, foundation first** (TSD §15). M0 = skeleton + data layer only; the
  16 design screens are implemented per-milestone (M1 auth → … → M7), wired to the real API as
  each backend lands. The full design system (component kit) is built immediately after M0.
- **Schema: full FKs per the TSD DDL.** Every cross-aggregate reference is a real DB foreign key
  (39 FK constraints in `InitialCreate`), with `OnDelete` chosen so erasure never destroys
  retained data:
  - **Restrict** — retained / financial / integrity rows: subscriptions, payment_transactions,
    watch_progress, certificates, capstone_submissions, quiz_attempts, and module/level/user
    references from those. A module/level/user with retained rows cannot be hard-deleted
    (unpublish instead) — consistent with GR-6/GR-7/GR-10 (immutable certs, never-delete
    progress, soft-delete + anonymize).
  - **Cascade** — ephemeral user-scoped engagement (notifications, preferences, onboarding
    survey, tour state, video notes, module feedback) and intra-aggregate children (tracks,
    modules, resources, module_tags, subscription_events, quiz questions).
  - **SetNull** — optional/nullable actor links (audit_logs.actor_user_id,
    feedback_submissions.user_id, org_seats.user_id).

## Implementation choices (mine, M0)
- **Enum-as-text** is done with a real value converter — `ConfigureConventions →
  Properties<Enum>().HaveConversion<string>()` — not the metadata `SetProviderClrType` hint from
  the schema package draft (which registers no converter). Verified: enum columns emit as `text`.
- **`Enums.cs` split** from the combined draft file into `Domain/Common/Entity.cs` +
  `Domain/Enums.cs` (the draft mixed file-scoped and block-scoped namespaces and would not compile).
- **UUID v7 PKs app-assigned** (`Guid.CreateVersion7()`, `ValueGenerated.Never`) — verified no DB
  default on `id`.
- **Central Package Management** (`Directory.Packages.props`) + shared `Directory.Build.props`
  (Nullable, ImplicitUsings, `TreatWarningsAsErrors`). Build is warnings-clean.
- **Startup migration** is guarded by `RunMigrations=true` (set on the docker-compose `api`
  service). Integration tests leave it off, so `/health` needs no database.
- **Solution format**: `.slnx` (the .NET 10 default).

## Pinned dependency versions (.NET 10, June 2026)
- EF Core / Npgsql provider `10.0.2`, `EFCore.NamingConventions 10.0.1`,
  `Microsoft.EntityFrameworkCore.Design 10.0.9`, `Microsoft.AspNetCore.OpenApi 10.0.5`.
- Tests: `xunit 2.9.3`, `Microsoft.NET.Test.Sdk 17.14.1`, `Microsoft.AspNetCore.Mvc.Testing 10.0.5`.
- Frontend: Next.js 15, React 19, Tailwind **v4** (tokens via CSS `@theme` + raw `--ds-*` vars).

## Open / to confirm (carried from the doc reviews)
- **LICENSE** is a proprietary placeholder — confirm intended license + legal entity.
- Frontend UI shows illustrative prices (Rp 149k/249k/349k); real prices come from the DB and
  final pricing is still open (TSD §16.5).
- External verifications still pending: Xendit Subscriptions generation (M3), on-demand ISR on
  the chosen host, Bunny/Postgres UU PDP data-residency posture.
- Google Fonts → **self-hosted** via `next/font` (privacy); QR + LinkedIn share on certificates
  are in-scope enhancements for the certificate milestone.
