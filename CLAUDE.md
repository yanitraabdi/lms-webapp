# CLAUDE.md — INVERTA (TOEFL Preparation Program)

Project context and rules for Claude Code. **The FSD (`docs/FSD_INVERTA_TOEFL_v0.1.md`) and the TSD-delta (`docs/TSD_Delta_INVERTA_v0.1.md`) are the source of truth.** When this file and the specs disagree, the specs win — and stop and flag the conflict rather than guessing.

> **Pivot (2026-08-14):** this product was previously "AI Productivity Academy" (subscription video LMS). Those specs are **paused and archived** at `docs/archive/ai-academy/` — retained, not deleted, for a possible later revival. They remain authoritative for the **platform** (stack, auth TSD §7, deployment §13, DB conventions); the FSD supersedes them for **product behaviour**.

---

## What this is
A paid, **linear** TOEFL-preparation program for Indonesian learners, in Bahasa Indonesia. .NET 10 API + Next.js (App Router) frontend + PostgreSQL. Video on Bunny Stream; payments via Xendit; email via SES.

**One-time purchase** (not a subscription). A student enrolls, then works through an ordered sequence of sessions — video lessons each gated by a **required short test**, at least one **live session**, and a final **multi-section proctored assessment** that yields an instant score and an emailed **TOEFL-prediction certificate**.

---

## Golden rules (invariants — never violate)
1. **Access is server-side only.** `canAccess(user, session)` = `isEnrolled(user, program) && sessionUnlocked(user, session)`, evaluated in the .NET API on every protected resource. Never infer or grant access from the client.
2. **Enrollment changes *only* via verified Xendit webhooks.** The checkout success page is informational; it never grants access. Webhooks must be signature-verified and idempotent (`webhook_events.external_id`).
3. **Signed playback per session.** Video *and listening-audio* URLs are minted server-side, short-TTL, after an access check, via `IVideoProvider`/`IObjectStorage`. No public/persisted media URLs, ever.
4. **No business logic in Next.js.** Next route handlers are allowed *only* for the auth BFF (cookie ↔ token, silent refresh), the same-origin `/api/*` proxy, and trivial SSR proxy-fetches. No DB access, no Bunny/Xendit/SES secrets, no access/scoring logic in Next. If a route handler is about to do anything else, that's a design smell — stop.
5. **Tokens:** access JWT in browser memory only (never localStorage/sessionStorage); refresh token in an httpOnly/Secure/SameSite=Lax cookie, rotating, with reuse detection.
6. **Certificates are immutable and score-based.** A certificate is issued from a submitted **attempt** and is never mutated. A granted retake issues a **new** certificate; it never overwrites or invalidates the old one.
7. **Progress and attempts are never hard-deleted.** Revoking an enrollment changes access only; `watch_progress`, `attempts`, `proctor_events`, and `certificates` persist indefinitely.
8. **Linear lock is earned, never skipped.** Session *k+1* unlocks only when *k* is complete (video: watch ≥ threshold **AND** gating test passed; live: admin-marked attended; final: submitted). Completion is idempotent and **non-retroactive** — it never un-completes.
9. **Secrets** live in server-side config/secret store only — never in the repo, never in the Next.js client bundle.
10. **Account deletion** = soft-delete + scheduled anonymization (UU PDP); financial/audit rows retained (anonymized link).

### Assessment integrity (rules 1–10 apply; these are the specifics)
11. **The answer key never leaves the server.** Student-facing question DTOs omit the correct answers. Scoring happens server-side only; the client never computes or posts a score.
12. **Timers and retake caps are server-authoritative.** `started_at` is server-stamped; a submit past the limit is auto-submitted and scored as-of expiry. Never trust a client clock.
13. **Proctor events are advisory data, not authority.** The client reports; the *server* counts strikes and decides warn / auto-submit. Every event is stored for dispute review; admin can reinstate a false positive without erasing the trail.
14. **"TOEFL Prediction", never an official score.** TOEFL is an ETS trademark. The certificate, result screen, and verify page must state plainly that the score is an INVERTA prediction, not an official ETS result. Do not describe soft proctoring as tamper-proof — it is deterrence, not exam security.

---

## Architecture
**Backend — .NET 10, pragmatic clean architecture (one DbContext, CQRS-lite handlers, no microservices):**
```
Api            minimal-API endpoints, auth, OpenAPI, webhook endpoints, rate limiting,
               RFC-7807 problem-details, model binding.
Application    use-case handlers, DTOs, FluentValidation, interface ports:
               IVideoProvider, IPaymentGateway, IEmailSender, IObjectStorage.
Domain         entities, enums, value objects, access rule (enrolled + unlocked),
               completion policy, scoring. NO EF / HTTP dependencies here.
Infrastructure EF Core (PostgreSQL/Npgsql), BunnyVideoProvider, XenditGateway, SesEmailSender,
               R2 object storage, background jobs.
```
Key services: `ISessionAccessService` (**the gate** — every protected resource calls it), `ISessionCompletionService` (the only thing that completes a session / advances the lock), `IAssessmentService`, `IProctorService`, `IScoreBandService`, `ICertificateService`.

**Frontend — Next.js App Router (single codebase):** program landing + `/verify/{code}` are SSG/ISR for SEO; the student app and admin are client-rendered. Tailwind, TanStack Query, `next-intl` (id default), driver.js onboarding tour. Light mode only. WCAG 2.1 AA. **The API client is generated from the .NET OpenAPI spec — do not hand-write API types.**

**External (all behind abstractions):** Bunny Stream (video), Xendit (payments), Amazon SES (email), Cloudflare R2 (object storage/listening audio). Certificate PDF via PDFsharp/MigraDoc (MIT). Dev-sim adapters are selected by config; real providers are a config swap.

---

## Repository structure
```
/                      docker-compose.yml, docker-compose.tunnel.yml, DEPLOY.md, README, CLAUDE.md
/docs                  FSD, TSD-delta, DECISIONS, milestone specs, design-handoff
  /archive/ai-academy  paused AI Academy PRD/TSD (retained, not deleted)
/backend               .NET solution
  /src                 Api · Application · Domain · Infrastructure
  /tests               Api.Tests · Application.Tests · Domain.Tests · Integration.Tests
/frontend              Next.js App Router app
  /app  /components  /lib  /messages (i18n)  /api-client (generated)
/.github/workflows     CI
```

---

## Conventions
**C#:** `Nullable` enabled, `TreatWarningsAsErrors` on; respect layer boundaries (Domain depends on nothing; Application depends on Domain; Infrastructure/Api depend inward). Validation via FluentValidation. Errors via problem-details (no stack traces to clients). Async all I/O. Minimal APIs use **TypedResults** so OpenAPI carries response schemas.

**Database:** PostgreSQL, **snake_case** (via `EFCore.NamingConventions`), `uuid` v7 PKs (app-assigned `Guid.CreateVersion7()`, `ValueGeneratedNever`), `timestamptz` for all timestamps (store UTC), enums stored **as strings**, money as `decimal(18,2)` (IDR rounded to whole rupiah at charge time), JSON bags as `jsonb`. Soft-delete query filter on `User`. Full FKs (`Restrict` on retained/financial, `Cascade` on intra-aggregate children, `SetNull` on optional actor links) — see `docs/DECISIONS.md`.

**Migrations:** generated by `dotnet ef`, never hand-edited to diverge from the snapshot. One migration per logical change. Review SQL with `dotnet ef migrations script`. **Additive only** for the pivot — the archived product's tables stay.

**API:** REST, versioned (`/api/...`), OpenAPI emitted at build. Webhooks under `/api/webhooks/{source}`. Rate-limit auth/payment/playback/assessment endpoints.

**Frontend:** server state via TanStack Query (incl. optimistic progress saves); minimal global client state; strings in Bahasa Indonesia; generated API client only; accessible components (focus states, keyboard nav, captions, audio controls).

---

## Commands
```
# Backend
dotnet build backend/Academy.slnx
dotnet test backend/Academy.slnx
dotnet ef migrations add <Name> --project backend/src/Infrastructure --startup-project backend/src/Api
dotnet ef migrations script --idempotent    # review SQL

# Frontend
cd frontend && npm install && npm run dev
npm run generate:api      # regenerate client from backend OpenAPI

# Local full stack
docker compose up -d --build
# Tunnel-ready deploy (single origin) — see DEPLOY.md
docker compose -f docker-compose.tunnel.yml up -d --build
```

---

## Testing (critical paths)
Inherited: auth (Google SSO, email-verify-before-purchase, change/reset password, logout-all, refresh rotation + **reuse detection**), webhook idempotency/signature, account-deletion anonymization.

New (TSD-delta §10): **enrollment only via verified webhook**; **linear lock** (k+1 blocked until k completes); **gating test truly gates** (100% watch alone doesn't complete); **answer key never served**; **server-authoritative timer** (late submit auto-submitted); **retake cap** + retake issues a *new* certificate (immutability); **proctoring** (2 strikes auto-submit + flag, <2s blur ignored, reinstate keeps the trail); **score→band** boundaries (unmapped score fails loudly); **retention** across enrollment revoke. E2E: enroll → watch → pass gating test → final assessment → score → certificate.

---

## Do NOT
- Grant enrollment anywhere but a verified webhook handler; infer access on the client.
- Serve correct answers to a student endpoint; score on the client; trust a client-supplied timer or score.
- Skip `ISessionAccessService` on any protected resource (playback, audio, assessment, progress).
- Hard-delete `watch_progress`, `attempts`, `proctor_events`, or `certificates`; mutate an issued certificate.
- Put tokens in localStorage/sessionStorage; put business logic, secrets, or DB access in Next.js.
- Hand-write/edit migrations so they drift from the model snapshot; DROP the archived product's tables.
- Serve a public/persisted video or audio URL.
- Imply the predicted score is an official ETS TOEFL result, or that soft proctoring is tamper-proof.
- Commit secrets.

---

## Milestones (FSD §11)
M0 Foundation ✅ · M1 Auth ✅ (both inherited, delivered) → **M2 Program & enrollment** → M3 Video sessions + gating tests → **M4 Final assessment + proctoring + certificate (sellable program complete)** → M5 Live sessions + admin polish → M6 Static/legal + onboarding.

**Dormant (archived product, kept in-repo):** subscriptions/plans/proration, catalog browsing + tier entitlement, module feedback & notification preferences, M7 quiz tables (superseded by the assessment engine). Do not extend these; do not delete them.

## Owed by the business (blocking launch, not the build)
Real syllabus content · **score→TOEFL-band mapping** (ships with a marked placeholder) · legal review of TOEFL naming/claims · refund policy for a one-time purchase · final price · whether live attendance must be automated.
