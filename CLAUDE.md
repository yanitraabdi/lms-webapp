# CLAUDE.md — AI Productivity Academy

Project context and rules for Claude Code. **The PRD (`PRD_LMS_AI_Curriculum_Detailed_v2.5.md`) and TSD (`TSD_LMS_AI_Curriculum_v0.4.md`) are the source of truth.** When this file and the specs disagree, the specs win — and stop and flag the conflict rather than guessing.

---

## What this is
A subscription video LMS teaching AI skills to Indonesian professionals, in Bahasa Indonesia. .NET 10 API + Next.js (App Router) frontend + PostgreSQL. Video on Bunny Stream; payments via Xendit. Tiered, cumulative subscriptions (Free preview → Beginner → Intermediate → Advanced).

---

## Golden rules (invariants — never violate)
1. **Entitlement is server-side only.** `canAccess(user, module)` is evaluated in the .NET API on every protected resource. Never infer or grant access from the client.
2. **Entitlements change *only* via verified Xendit webhooks.** The checkout success page is informational; it never grants access. Webhooks must be signature-verified and idempotent (`webhook_events.external_id`).
3. **Signed playback per session.** Video URLs are minted server-side, short-TTL, after an entitlement check, via `IVideoProvider`. No public/persisted video URLs, ever.
4. **No business logic in Next.js.** Next route handlers are allowed *only* for the auth BFF (cookie ↔ token, silent refresh) and trivial SSR proxy-fetches to the .NET API. No DB access, no Bunny/Xendit/SES secrets, no entitlement/pricing logic in Next. If a route handler is about to do anything else, that's a design smell — stop.
5. **Tokens:** access JWT in browser memory only (never localStorage/sessionStorage); refresh token in an httpOnly/Secure/SameSite=Lax cookie, rotating, with reuse detection.
6. **Certificates are immutable.** Once a `certificates` row exists for (user, level), that level is never re-evaluated. New modules surface as "bonus/Baru", never as un-completion. Graduated levels always display 100%.
7. **Progress is never hard-deleted.** Downgrade/expiry changes entitlement only; `watch_progress` and `certificates` persist indefinitely.
8. **Grandfathering:** billing charges `subscriptions.price_locked_idr`, not live `plans.price`. Price edits affect new subscribers immediately, existing ones at next renewal.
9. **Secrets** live in server-side config/secret store only — never in the repo, never in the Next.js client bundle.
10. **Account deletion** = soft-delete + scheduled anonymization (UU PDP); financial/audit rows retained (anonymized link).

---

## Architecture
**Backend — .NET 10, pragmatic clean architecture (one DbContext, CQRS-lite handlers, no microservices):**
```
Api            controllers/minimal-API, auth, OpenAPI, webhook endpoints, rate limiting,
               RFC-7807 problem-details, model binding.
Application    use-case handlers, DTOs, FluentValidation, interface ports:
               IVideoProvider, IPaymentGateway, IEmailSender, IObjectStorage, INotificationSender.
Domain         entities, enums, value objects, entitlement rule, subscription state machine.
               NO EF / HTTP dependencies here.
Infrastructure EF Core (PostgreSQL/Npgsql), BunnyVideoProvider, XenditGateway, SesEmailSender,
               R2 object storage, background jobs, outbox.
```
**Frontend — Next.js App Router (single codebase):** public pages (marketing/catalog/module/verify) SSG/ISR for SEO; authenticated app + admin client-rendered. Tailwind, TanStack Query for server state, `next-intl` (id default), driver.js onboarding tour. Light mode only. WCAG 2.1 AA. **The API client is generated from the .NET OpenAPI spec — do not hand-write API types.**

**External (all behind abstractions):** Bunny Stream (video), Xendit (payments), Amazon SES (email), Cloudflare R2 (object storage). Certificate PDF via PDFsharp/MigraDoc (MIT). See TSD §3/§13 for the per-route rendering matrix and deployment topology.

---

## Repository structure (target)
```
/                      docker-compose.yml, README, CLAUDE.md, /docs (PRD, TSD, specs)
/backend               .NET solution
  /src
    Api
    Application
    Domain
    Infrastructure
  /tests
    Api.Tests · Application.Tests · Domain.Tests · Integration.Tests
/frontend              Next.js App Router app
  /app  /components  /lib  /messages (i18n)  /api-client (generated)
/.github/workflows     CI
```

---

## Conventions
**C#:** `Nullable` enabled, `TreatWarningsAsErrors` on; respect layer boundaries (Domain depends on nothing; Application depends on Domain; Infrastructure/Api depend inward). Validation via FluentValidation. Errors via problem-details (no stack traces to clients). Async all I/O.

**Database:** PostgreSQL, **snake_case** (via `EFCore.NamingConventions`), `uuid` v7 PKs (app-assigned `Guid.CreateVersion7()`, configured `ValueGeneratedNever`), `timestamptz` for all timestamps (store UTC), enums stored **as strings**, money as `decimal(18,2)` (IDR rounded to whole rupiah at charge time), JSON bags as `jsonb`. Soft-delete query filter on `User`.

**Migrations:** generated by `dotnet ef`, never hand-edited to diverge from the snapshot. One migration per logical change. Review SQL with `dotnet ef migrations script`.

**API:** REST, versioned (`/api/...`), OpenAPI emitted at build. Webhooks under `/api/webhooks/{source}`. Rate-limit auth/payment/playback endpoints.

**Frontend:** server state via TanStack Query (incl. optimistic progress saves); minimal global client state; all strings externalized to `/messages` (Bahasa Indonesia first); generated API client only; accessible components (focus states, keyboard nav, captions).

---

## Commands (intended)
```
# Backend
dotnet build backend
dotnet test backend
dotnet run --project backend/src/Api
dotnet ef migrations add <Name> --project backend/src/Infrastructure --startup-project backend/src/Api
dotnet ef database update --project backend/src/Infrastructure --startup-project backend/src/Api
dotnet ef migrations script --idempotent    # review SQL

# Frontend
cd frontend && npm install && npm run dev
npm run generate:api      # regenerate client from backend OpenAPI

# Local full stack
docker compose up
```

---

## Testing (critical paths — TSD §14)
Cover: auth (Google SSO, email-verify-before-purchase, change/reset password, logout-all, refresh rotation + **reuse detection**), webhook idempotency, app-side prorated upgrade, entitlement/signed-playback (locked vs preview vs entitled), progress→certificate, **progress retention across downgrade/expiry**, **certificate immutability**, account-deletion anonymization, notification-preference honoring, webhook reconciliation. E2E: purchase → watch → complete → certificate.

---

## Do NOT
- Put tokens in localStorage/sessionStorage; put business logic, secrets, or DB access in Next.js.
- Grant entitlement anywhere but a verified webhook handler; infer access on the client.
- Hard-delete `watch_progress` or `certificates`; re-evaluate a certified level.
- Hand-write/edit migrations so they drift from the model snapshot.
- Serve a public/persisted video URL; skip the per-session entitlement check before minting one.
- Reproduce copyrighted curriculum text beyond what's needed; commit secrets.

---

## Milestones
M0 Foundation (this package) → M1 Auth → M2 Catalog+entitlement → M3 Payments → **M4 Player+progress+certificates (first sellable slice)** → M5 Admin → M6 Static/onboarding → M7 Engagement (notifications, feedback, notes, quizzes-P1). See TSD §15 and each milestone spec.
