# Technical Specification Document (TSD)
## AI Productivity Academy — Subscription Video LMS

| | |
|---|---|
| **Document version** | 0.4 (Draft — for review) |
| **Date** | June 2026 |
| **Companion PRD** | `PRD_LMS_AI_Curriculum_Detailed_v2.5.md` |
| **Status** | Draft — frontend/SSR decision is the gating decision (see ADR-01) |
| **Stack** | .NET 10 API · PostgreSQL · Next.js (App Router) frontend · Bunny Stream · Xendit |
| **Audience** | Engineering (incl. Claude Code execution), reviewer/PO |

> **How to read this.** §2 is the decision record (ADRs) — read it first; everything else implements those decisions. §4 (frontend/SSR) and §7 (auth/session) are deliberately the deepest sections because that is where the consequential, hard-to-reverse choices live. This TSD is the "commit" artifact; once approved, the execution package (`CLAUDE.md` + EF Core schema/migrations + per-milestone specs) is generated from it.

> **v0.2 resolutions (from the ambiguity log):** capstones are non-gating in v1; certificates are immutable once issued; `watch_progress` retained indefinitely; account deletion is P0/M1; transactional email is P0 (notification center P1); grandfather-until-renewal with a `price_locked` snapshot; `/verify/{code}`; non-retroactive quiz-gating; watermarking Phase 4; providers chosen — **SES** (email), **Cloudflare R2** (object storage), **PDFsharp/MigraDoc** (cert PDF, MIT); host = **Cloud Run / Azure Container Apps** running Next as a Node server. Residual TODOs: Xendit proration sandbox check, and on-demand ISR confirmation on the chosen host (§16).

> **v0.3 additions:** recommended-next-module P0 fallback (§6.6); graduate-display rule for certified levels (§6.6); grandfathering renewal-rollover mechanics (§9.6); and a **phased deployment topology — self-host-first → managed cloud (§13.1)**.

> **v0.4 additions:** Cloudflare Tunnel documented as the reachability mechanism with a clear local-PC-vs-VPS boundary for paid traffic (§13.1); a **webhook-reconciliation job** added (§5.3, §9.3) to repair events missed during origin downtime.

---

## 1. Scope & non-goals

**In scope (this TSD):** system + frontend + backend architecture, data model, auth/session/security, entitlement engine, payments/subscriptions (incl. app-side proration), Bunny video pipeline, cross-cutting product features (notifications, feedback, survey, tour, what's-new, notes), NFRs, environments, CI/CD, testing, and a milestone slicing aimed at a thin sellable first release.

**Non-goals:** B2B seat-management UX (data model is B2B-ready; UX is Phase 2+), community/forum, native mobile, 2FA, Microsoft/LinkedIn SSO, in-browser labs, PPN invoicing. These are designed-around, not designed-in.

**Confidence & verification.** External-dependency claims are labelled. Items needing live verification before build are collected in §16.

---

## 2. Key technical decisions (ADRs)

| # | Decision | Why | Reversibility |
|---|----------|-----|---------------|
| **ADR-01** | **Next.js (App Router) single frontend codebase.** Public routes (marketing, catalog, module landing, certificate verify) are SSG/ISR; the authenticated app (player, dashboard, admin) is client-rendered. **.NET 10 stays the only business API** — Next is rendering + routing + a thin auth BFF, never a second business layer. | One deploy, one design system, SEO where it matters, and Next gives a natural place to hold the session cookie. A split SPA+static-site doubles the ops and design surface for a small team. | **Hard.** Commit now (§4). |
| **ADR-02** | **Auth = short-lived JWT access token (in browser memory) + rotating refresh token in an httpOnly/Secure/SameSite cookie.** Refresh handled via a Next route handler (BFF) so the cookie is same-origin and SSR can bootstrap session. | Keeps a long-lived secret out of JS-readable storage; the BFF removes the SSR-auth awkwardness that the ADR-01 choice would otherwise create. | Medium (§7). |
| **ADR-03** | **Video behind `IVideoProvider` (Bunny adapter).** Signed, per-session, short-TTL playback URLs minted server-side after an entitlement check. | Vendor swap = one adapter; no public video URLs ever. | Easy (adapter). |
| **ADR-04** | **Entitlement is evaluated server-side on every protected resource** and **changes only via verified Xendit webhooks.** Client is never trusted. | Prevents paywall bypass and webhook spoofing. | Hard (core invariant). |
| **ADR-05** | **Payments via Xendit Subscriptions; proration computed app-side** (Xendit has no native proration). Webhooks idempotent + signature-verified. | Matches PRD §5.2; Xendit only charges a defined per-cycle amount. [High confidence — Xendit API docs] | Medium. |
| **ADR-06** | **PostgreSQL + EF Core; pragmatic clean architecture** (Api / Application / Domain / Infrastructure). | Testable core, swappable infra, not over-engineered. | Medium. |
| **ADR-07** | **Soft-delete + anonymization** for account erasure; financial/audit records retained. | UU PDP erasure vs. accounting integrity. | Medium. |
| **ADR-08** | **i18n from day one, `id` default.** All user-facing strings via resource bundles; DB content carries a locale where relevant. | PRD requires ID now, EN later. | Easy. |

---

## 3. System architecture

```
                         ┌────────────────────────────────────────────┐
  Browser  ──────────▶   │  Next.js (App Router)                       │
                         │  • SSG/ISR public pages (SEO)               │
                         │  • CSR authenticated app                    │
                         │  • /api/auth/* route handlers (BFF: cookie) │
                         └───────────────┬────────────────────────────┘
                                         │  Bearer access token (per request)
                                         ▼
                         ┌────────────────────────────────────────────┐
                         │  .NET 10 API (ASP.NET Core)                 │
                         │  Api → Application → Domain → Infrastructure│
                         └───┬───────┬───────┬───────┬───────┬─────────┘
                             │       │       │       │       │
                  PostgreSQL │  Bunny│  Xendit│ SES   │ Cloudflare R2 (PDF/cert/thumb)
                 (EF Core)   │ (signed│(webhooks│(txn) │ (S3/GCS-compatible)
                             │  HLS)  │ +MIT)  │       │
        Webhooks IN:  /api/webhooks/xendit   /api/webhooks/bunny  (signed, idempotent)
```

**Trust boundary:** only the .NET API touches the DB, Bunny secrets, and Xendit secrets. Next never holds business secrets; it holds only the session cookie and forwards a per-request access token.

---

## 4. Frontend architecture & the SSR decision (consequential)

### 4.1 The decision
**Adopt Next.js (App Router) as a single frontend.** The alternative — a Vite React SPA for the app plus a separate static site (Astro/Next-static) for marketing — was rejected for this team size. Rationale and honest tradeoffs:

| Criterion | Next.js single codebase (chosen) | Split SPA + static marketing (rejected) |
|---|---|---|
| SEO for marketing/catalog/module/verify pages | Native SSG/ISR | Native (static site) |
| Ops / deploys | **One** | Two pipelines, two hosts |
| Design system | **One** | Duplicated or shared-package overhead |
| Auth in SSR context | Solved via BFF route handlers (ADR-02) | Trivial in SPA, but marketing rarely needs auth anyway |
| App bundle for behind-auth routes | Slightly heavier framework | Leaner SPA |
| Risk of business logic leaking into frontend | Real — **must be policed** (see guardrail) | Lower |

**Guardrail (non-negotiable):** Next.js route handlers are allowed **only** for (a) the auth BFF (cookie ↔ token exchange, silent refresh) and (b) trivial SSR data fetches that call the .NET API. **No business logic, no DB access, no Bunny/Xendit secrets in Next.** Entitlement, pricing, playback signing, and webhooks live exclusively in .NET. If a Next route handler is about to do anything other than auth-cookie handling or proxy-fetch, that is a design smell.

### 4.2 Rendering strategy per route (the heart of the SSR decision)

| Route group | Examples | Rendering | Auth | Notes |
|---|---|---|---|---|
| Marketing | `/`, `/how-it-works`, `/for-business`, `/pricing`, `/blog/*`, `/about`, legal pages | **SSG** (blog: ISR) | Public | Pricing reads plans at build + revalidate; or ISR. |
| Catalog (public shell) | `/catalog`, `/catalog/[level]` | **ISR** | Public | Shows locked + preview modules; revalidate on admin publish (on-demand revalidation webhook from .NET). |
| Module landing | `/modules/[slug]` | **ISR** | Public | SEO-rich (title, desc, thumbnail, "Upgrade to watch"); player mounts client-side only if entitled. |
| Certificate verify | `/verify/[code]` | **SSR** (or ISR) | Public | Must reflect live validity; SSR fetch to `/api/verify/{code}`. |
| Authenticated app | `/app/dashboard`, `/app/learn/[id]`, `/app/account`, `/app/certificates` | **CSR** | Required | No SEO value; client-rendered; data via authed fetches. |
| Admin | `/admin/*` | **CSR** | Admin role | Same as app; role-gated in middleware + server. |

**On-demand ISR invalidation:** when an admin publishes/edits a module or changes pricing, the .NET API calls a Next revalidation endpoint (shared secret) to refresh the affected static pages. This keeps SEO pages fresh without rebuilds. Host = Cloud Run / Azure Container Apps running Next as a Node server; **multi-instance requires a shared ISR cache handler (Redis-backed)** so revalidation propagates across instances. *(Confirm on-demand ISR on the chosen host — §16.)*

### 4.3 Frontend conventions
- **TypeScript strict.** Data layer via a typed API client generated from the .NET OpenAPI spec (single source of truth for contracts).
- **State/data:** TanStack Query for server state (caching, retries, optimistic progress saves); minimal global client state.
- **Design system:** Tailwind + a component library; tokens documented. **Dark mode deferred post-v1 (light-only)** — removes the driver.js spotlight dark-mode caveat from v1 scope.
- **Onboarding tour:** **driver.js (MIT)**; tour steps localized; replayable from Help; per-user `TourState` persisted (§11.4).
- **i18n:** `next-intl` or equivalent; `id` default; all strings externalized.
- **Accessibility:** WCAG 2.1 AA target — keyboard nav, captions in player, focus management in tour and modals.
- **Player:** HLS player (e.g. hls.js / Bunny embed) with custom controls: play/pause, seek, speed, volume, fullscreen, captions (WebVTT, ID), and a **manual quality selector** with a data-usage hint (PRD §5.4 AC8).

---

## 5. Backend architecture (.NET 10)

### 5.1 Layering
```
Api            ASP.NET Core Web API: controllers/minimal-API, auth, model binding,
               OpenAPI, webhook endpoints, rate limiting, problem-details errors.
Application    Use-cases (CQRS-lite handlers), DTOs, validation (FluentValidation),
               IVideoProvider / IPaymentGateway / INotificationSender interfaces.
Domain         Entities, value objects, the entitlement rule, subscription state machine,
               invariants. No EF/HTTP here.
Infrastructure EF Core (PostgreSQL), BunnyVideoProvider, XenditGateway, email sender,
               object storage, background jobs, outbox.
```
Keep it pragmatic: CQRS-lite (handlers, not full event sourcing). One DbContext. No microservices.

### 5.2 Key backend modules
- **Identity & auth** (ASP.NET Core Identity + Google external login; JWT issuance; refresh rotation; lockout/rate-limit).
- **Catalog** (Level/Track/Category/Tag/Module CRUD, publish, ordering, `is_preview`).
- **Entitlement** (`canAccess`, evaluated everywhere protected — §8).
- **Subscriptions & payments** (Xendit, webhook handler, state machine, app-side proration — §9).
- **Playback** (mint signed Bunny URLs after entitlement check — §10).
- **Progress** (save/resume, rollups, retention on downgrade/expiry).
- **Certificates** (issue on level completion, PDF + verification code).
- **Notifications** (outbox → in-app + email — §11.1).
- **Feedback/ratings, onboarding survey, notes, tour state, what's-new** (§11).
- **Admin** (users, pricing, curriculum, analytics, audit log).

### 5.3 Cross-cutting
- **Background jobs:** dunning/retries, certificate generation, notification dispatch, ISR-revalidation calls, scheduled anonymization of expired soft-deletes, **webhook reconciliation** (periodic pull of Xendit subscription/payment status to repair events missed during origin downtime). (Hangfire or hosted `BackgroundService` + a jobs table.)
- **Outbox pattern** for webhook-driven side effects (entitlement change → notification/email) so they are reliable and idempotent.
- **Errors:** RFC-7807 problem-details; no stack traces to clients.
- **Observability:** structured logging (Serilog), OpenTelemetry traces, error tracking (Sentry/equiv), alerts on payment/webhook/playback failures.

---

## 6. Data model (PostgreSQL, B2B-ready)

Naming: `snake_case` tables, `uuid` PKs (v7 preferred for index locality), `timestamptz` everywhere, soft-delete via `deleted_at` where applicable. DDL below is indicative (EF Core migrations are the source of truth in the execution package).

### 6.1 Identity & org (B2B-ready now, UX later)
```
users(id, email UNIQUE, password_hash NULL, name, role,
      email_verified bool, status,            -- active|suspended|deleted
      deleted_at NULL, anonymized_at NULL, created_at, updated_at)
user_external_logins(id, user_id FK, provider, provider_key, created_at,
      UNIQUE(provider, provider_key))         -- provider='google' in MVP
refresh_tokens(id, user_id FK, token_hash, family_id, expires_at,
      revoked_at NULL, replaced_by NULL, created_at)   -- rotation + reuse detection

organizations(id, name, billing_owner_user_id FK, status, created_at)        -- B2B
org_seats(id, org_id FK, user_id FK NULL, seat_status, assigned_at NULL)      -- B2B
org_memberships(id, org_id FK, user_id FK, member_role, created_at)           -- B2B
```

### 6.2 Catalog
```
levels(id, name, slug, required_plan_tier, order_index, status)
tracks(id, level_id FK, name, slug, order_index)
categories(id, name, slug)
tags(id, name, slug)
modules(id, track_id FK, category_id FK, title, slug UNIQUE, description, summary,
        duration_seconds, provider_asset_id, thumbnail_url, order_index,
        status,                                  -- draft|published
        is_preview bool, required_plan_tier,
        published_at NULL, last_refreshed_at NULL, created_at, updated_at)
module_tags(module_id FK, tag_id FK, PK(module_id, tag_id))
resources(id, module_id FK, type, ref, title)   -- pdf|link
```

### 6.3 Plans, subscriptions, payments
```
plans(id, name, tier_level,                      -- 0..3
      price_monthly, price_annual, is_active, description,
      included_content_mapping JSONB)            -- tier→content rule, admin-editable
subscriptions(id, user_id FK, org_id FK NULL, plan_id FK,
      price_locked_idr, billing_cycle,           -- snapshot at signup → grandfathering (PRD §5.7)
      status,                                    -- active|past_due|grace|canceled|expired
      current_period_start, current_period_end,
      xendit_plan_ref, created_at, updated_at)
subscription_events(id, subscription_id FK, from_status, to_status, reason, created_at)
payment_transactions(id, user_id FK, subscription_id FK NULL, amount_idr,
      kind,                                      -- cycle|proration_upgrade
      method, status, xendit_ids JSONB, raw_payload JSONB, created_at)
webhook_events(id, source, external_id UNIQUE, signature_verified bool,
      processed_at NULL, payload JSONB, created_at)   -- idempotency ledger
```

### 6.4 Learning, certificates
```
watch_progress(id, user_id FK, module_id FK, resume_position_seconds,
      percent_complete, completed bool, completed_at NULL, last_watched_at,
      UNIQUE(user_id, module_id))                -- NEVER hard-deleted on downgrade/expiry
quizzes(id, module_id FK, pass_threshold, is_active)                 -- P1
quiz_questions(id, quiz_id FK, prompt, choices JSONB, correct_index)  -- P1
quiz_attempts(id, user_id FK, quiz_id FK, score, passed bool, created_at)  -- P1
capstones(id, level_id FK, title, brief)                              -- v1: encouraged, NON-gating
capstone_submissions(id, user_id FK, capstone_id FK, content, status, reviewed_at NULL)  -- review surface deferred
certificates(id, user_id FK, level_id FK, issued_at, verification_code UNIQUE, pdf_url,
      completed_module_ids JSONB)                -- snapshot of qualifying set at issuance
                                                 -- IMMUTABLE + retained indefinitely; level not re-evaluated once a cert exists
```

### 6.5 Engagement & system
```
notifications(id, user_id FK, type, payload JSONB, channel, read_at NULL, created_at)
notification_preferences(id, user_id FK, category, channel, enabled,
      UNIQUE(user_id, category, channel))
module_feedback(id, user_id FK, module_id FK, rating, comment NULL, created_at,
      UNIQUE(user_id, module_id))
feedback_submissions(id, user_id FK NULL, message, context, created_at)   -- product-level
onboarding_surveys(id, user_id FK, role, goals JSONB, preferred_tools JSONB, created_at)
video_notes(id, user_id FK, module_id FK, timestamp_seconds, type, text, created_at)  -- note|bookmark, P2
tour_states(id, user_id FK, tour_key, status, updated_at)             -- completed|skipped
faq_items(id, question, answer, order_index, is_published)
contact_submissions(id, name, email, message, created_at)
audit_logs(id, actor_user_id FK, action, target, metadata JSONB, created_at)
jobs(...)                                        -- background job ledger (or Hangfire schema)
```

### 6.6 Core invariants
- **Entitlement:** `canAccess(user, module) = module.is_preview OR (∃ active subscription with plan.tier_level ≥ module.required_plan_tier)`.
- **Progress retention:** downgrade/expiry changes entitlement only; `watch_progress` and `certificates` are never deleted.
- **Certificate immutability + graduate display:** once a `certificates` row exists for (user, level), that level is never re-evaluated; the dashboard renders it at **100% regardless of newly added modules**, which surface only as **bonus/"what's new"** (tracked separately), never as un-completion or denominator growth for that user.
- **Recommended-next-module (P0):** deterministic fallback = next incomplete module in curriculum order within the user's entitled content. The P1 interest survey only re-ranks/enhances this; it is never the sole source.
- **Grandfathering:** entitlement billing uses `subscriptions.price_locked_idr`, not the live `plans.price`; plan price edits affect new subscribers immediately and existing ones at next renewal.
- **Idempotency:** every external webhook recorded in `webhook_events.external_id`; re-delivery is a no-op.

---

## 7. AuthN / AuthZ & security (consequential — follows ADR-01/02)

### 7.1 Token model
- **Access token:** JWT, ~15 min TTL, signed (RS256 or HS256 w/ rotated key), carries `sub`, `role`, `email_verified`. Held **in memory** in the client (never localStorage).
- **Refresh token:** opaque, ~30 day TTL, **rotating**, stored hashed in `refresh_tokens` with a `family_id`. **Reuse detection:** if a revoked token is presented, revoke the whole family (theft response).
- **Cookie:** refresh token delivered as `httpOnly; Secure; SameSite=Lax; Path=/api/auth`. Browser JS cannot read it.

### 7.2 Flow (with Next BFF)
1. Login (password or Google) → .NET issues access token (body) + sets refresh cookie.
2. The Next **auth route handler** is the only thing that talks to the refresh cookie; it exchanges it for an access token and hands the SPA an in-memory access token.
3. SPA calls the .NET API directly with `Authorization: Bearer <access>`.
4. On 401 / near-expiry, SPA silently calls the Next refresh handler → new access token; refresh cookie rotates.
5. SSR pages that need user context call the refresh handler server-side using the incoming cookie.

**CSRF:** the only cookie-authenticated endpoint is refresh; protect it with SameSite=Lax + an Origin/Referer allowlist check. All business endpoints are Bearer-authenticated (cookies not auto-attached), so they are not CSRF-exposed.

> **Alternative (more secure, more work):** full BFF — Next route handlers proxy *all* API calls and the browser never holds any token. Choose this only if the XSS surface is judged high; it adds a proxy hop to every call. Default = §7.1/7.2.

### 7.3 Authorization
- Roles: `User`, `Admin`, `SuperAdmin` → ASP.NET Core policies. Admin/SuperAdmin gated in Next middleware (UX) **and** enforced server-side (truth).
- Resource checks (entitlement) are **always** server-side, never inferred from the client.

### 7.4 Account lifecycle flows
- **Register (email):** create user → send verification → **must verify before purchase** (PRD §5.1 AC10).
- **Google SSO:** auto-verified; link by verified email to avoid duplicates.
- **Change password:** require current password; rotate refresh family (logs out other sessions); confirmation email.
- **Forgot/reset:** time-limited single-use token.
- **Resend verification:** rate-limited.
- **Logout / logout-all:** revoke current / all refresh families.
- **Account deletion (UU PDP):** must cancel active subscription first → soft-delete (`deleted_at`, status=deleted, recoverable on login for a configurable grace, default 30 days) → scheduled job anonymizes PII after grace; `payment_transactions`/`audit_logs` retained (anonymized link). All steps audit-logged.

### 7.5 Hardening
HTTPS only; secrets server-side (key vault / env, never in repo or Next); rate limiting on auth + payment + playback endpoints; password hashing via Identity (PBKDF2/Argon2); signed webhooks; input validation; output encoding; dependency scanning in CI.

---

## 8. Entitlement engine

- Single domain service `IEntitlementService.CanAccess(userId, module)` used by: playback URL minting, resource download, module detail (to decide locked vs. playable), and progress save.
- **Evaluation points (all server-side):** playback endpoint, resource endpoint, any "start learning" action.
- **Caching:** per-request resolve of the user's active subscription tier; optional short-TTL cache keyed by `user_id` invalidated on any subscription state change (webhook). Never cache the *access grant* longer than the subscription state can change.
- **Preview bypass:** `module.is_preview` short-circuits to allow.

---

## 9. Payments & subscriptions (Xendit)

### 9.1 Provider abstraction
`IPaymentGateway` (create customer, create/activate recurring plan, charge MIT one-off for proration, update plan amount, parse+verify webhook). `XenditGateway` implements it. *Verification: confirm whether to build on Xendit's current Subscriptions vs legacy product (§16).* [Medium confidence the new product is correct target.]

### 9.2 Subscription state machine
```
            checkout success (webhook)
  (none) ───────────────────────────▶ active
  active ── cycle fail ▶ past_due ── retry fail ▶ grace ── grace end ▶ expired
  active ── user cancel ▶ canceled (entitled until period_end) ▶ expired→Free
  past_due/grace ── cycle success (webhook) ▶ active
```
All transitions logged in `subscription_events`. Entitlement re-derives from current status + tier.

### 9.3 Entitlement changes are webhook-only
`/api/webhooks/xendit`: verify signature → upsert `webhook_events` (idempotency) → if new, apply state transition + grant/extend entitlement → enqueue confirmation email/notification via outbox. Client checkout success page is informational only; it never grants access. **Reconciliation job:** a periodic task pulls current subscription/payment status from Xendit and repairs any state missed during origin downtime (idempotent via `webhook_events`) — good billing hygiene generally, and the safety net for any self-hosted/tunnelled origin (§13.1).

### 9.4 App-side proration on upgrade (PRD §5.2 AC6)
```
delta = (newTier.price_cycle − currentTier.price_cycle) × remainingDays ÷ cycleDays
1. Charge `delta` immediately as a merchant-initiated one-off (stored payment token).
2. On verified charge webhook → grant new tier entitlement immediately.
3. Update the recurring plan amount to newTier for subsequent cycles.
4. Record payment_transactions.kind = 'proration_upgrade'.
Downgrade: schedule plan change at next renewal (no immediate charge/refund).
```

### 9.5 Progress retention
Downgrade/expiry flips entitlement; a job/handler must **not** touch `watch_progress` or `certificates`. Re-access on re-subscribe restores the full history automatically.

### 9.6 Grandfathering rollover (renewal boundary)
Billing always charges `subscriptions.price_locked_idr`, not the live `plans.price`. When an admin changes a tier price, existing subscribers are unaffected until their next renewal. At the renewal boundary a scheduled job: (1) sends a **prior-notice email** a configurable window before renewal (default **14 days**); (2) on renewal, updates the Xendit recurring-plan amount (Update-Plan API) and writes the new `price_locked_idr`. New subscribers get the current price at signup.

---

## 10. Video pipeline (Bunny Stream)

- **Upload (admin):** request an upload target from `IVideoProvider` (Bunny library + signed upload) → admin uploads → Bunny encodes → `/api/webhooks/bunny` updates `modules.provider_asset_id` + status when ready. [Bunny supports token auth, signed URLs, DRM, webhooks — High confidence per vendor docs.]
- **Playback:** `POST /api/modules/{id}/playback` → entitlement check → `IVideoProvider.GetSignedPlaybackUrl(assetId, userId, shortTtl)` → return signed HLS URL (short expiry, per session). Never persisted, never public.
- **Player:** HLS adaptive + **manual quality selector** + ID captions (WebVTT). Optional email watermark on higher tiers (P1, Bunny overlay/DRM).
- **Data residency note:** Bunny primary storage region (Falkenstein, DE) cannot be disabled; course video is not personal data, but confirm UU PDP posture for any sensitive metadata (§16).

---

## 11. Cross-cutting product features

### 11.1 Notifications (P1)
**P0 transactional email** (ships with its triggering feature, not the center): email verification, password reset, change-password confirmation, payment receipt, dunning (past_due/grace/expiring).
**P1 notification center:** outbox → dispatcher → in-app (`notifications`) + email. In-app events: new/refreshed module, certificate issued, renewal/past_due/grace/expiring, payment receipt. Per-category/per-channel `notification_preferences` honored; unsubscribe links (UU PDP/anti-spam). *("Capstone reviewed" deferred with the capstone-review surface.)*

### 11.2 Feedback & ratings (P1)
`module_feedback` (1 per user+module, editable); aggregate exposed to admin analytics and optionally catalog "popularity" sort. Separate product-level `feedback_submissions` → support inbox.

### 11.3 Onboarding survey (P1)
One-time, skippable survey at first login → `onboarding_surveys`; seeds "recommended next module"; editable in profile.

### 11.4 Onboarding tour (P1) & what's-new (P1)
driver.js tour, localized, replayable, `tour_states` per user. "What's new": modules with `published_at`/`last_refreshed_at` after the user's last visit are badged; a feed surfaces additions (retention driver).

### 11.5 Notes & bookmarks (P2)
`video_notes` (timestamped), surfaced on module detail + dashboard.

---

## 12. Non-functional requirements (engineering view)
- **Performance:** catalog/dashboard < 2s on ID 4G; playback start < 3s. ISR/SSG for public pages; CDN for static + video.
- **Scale:** 10k users / 1k concurrent viewers without redesign; video delivery offloaded to Bunny CDN; stateless API behind LB; DB connection pooling.
- **Availability:** 99.5%; graceful degradation if Xendit/Bunny down (queue webhooks, show retry states).
- **Security/Privacy:** §7; UU PDP alignment; account deletion; cookie consent.
- **Accessibility:** WCAG 2.1 AA.
- **Observability:** logs/traces/alerts on payment, webhook, playback errors.
- **Backup/DR:** daily PostgreSQL backups, tested restore, ≥30-day retention.

---

## 13. Environments, config, CI/CD

### 13.1 Deployment topology (phased: self-host-first → managed cloud)
The app is **portable by design** (containers + provider abstractions + portable Postgres schema), so a cheap self-hosted start migrating to managed cloud later is supported and recommended.

**What is never "local":** Bunny (video), Xendit (payments), SES (email), R2 (storage) are SaaS called over the internet regardless of where the app runs — the bandwidth- and compliance-heavy parts are already offloaded.

**Hard requirement:** webhook endpoints (`/api/webhooks/xendit`, `/api/webhooks/bunny`) must be **publicly reachable over HTTPS at a stable URL** — entitlement changes only via verified webhooks, so an unreachable origin breaks paid access. A small VPS with a domain + TLS satisfies this.

**Reachability mechanism — Cloudflare Tunnel.** `cloudflared` gives a stable public HTTPS hostname via an outbound connection (no port-forwarding, static IP, or exposed origin IP). It is the right tool and is **endorsed for dev and private/pre-revenue beta on a local machine**, and is also fine **in front of the Phase-A VPS** (not mutually exclusive). But the tunnel solves *addressing only* — not availability, data durability, or security isolation. **A local PC as the production origin for paying customers is discouraged:** single point of failure (power/ISP/OS-reboot); the entitlement/subscription/payment DB sits on a consumer disk with no redundancy; and tunnelling makes a personal machine an internet-facing host for PII + secrets. (CDN/ToS is a non-issue here — video is served by Bunny, not through the tunnel.) If a local origin is used for the earliest paid cohort, these are **non-negotiable**: (1) automated **off-machine nightly DB backups** + tested restore; (2) `cloudflared` and the app as **auto-restarting Docker services**, isolated from personal use; (3) **uptime monitoring/alerting** on the tunnel URL; (4) the **webhook-reconciliation job** (§9.3); (5) a UPS for desktops. Move the origin to a VPS/managed host before meaningful paid traffic.

**Why a single small server suffices early:** video delivery is offloaded to Bunny's CDN, so the origin only mints signed URLs and writes small progress rows — it barely feels the 1k-concurrent-viewer figure.

| | Phase A — self-host (pre/early-revenue) | Phase B — managed cloud (scale) |
|---|---|---|
| Compute | 1 VPS (SG/Jakarta region), Docker Compose: Next + .NET API + Postgres | Cloud Run / Azure Container Apps (same containers) |
| DB | Postgres in container, **nightly backup to R2/offsite (mandatory)** | Managed PostgreSQL (Cloud SQL / Azure DB) |
| Cache/ISR | **Single instance → default file ISR cache, no Redis** | Multi-instance → Redis ISR cache handler |
| TLS/routing | Caddy/Traefik auto-HTTPS | Platform-managed |
| Availability | Single point of failure (99.5% fragile) | Redundant/autoscaled |
| Ops owned by you | backups, patching, TLS, monitoring, DR | mostly platform-managed |

**Migration is low-risk** because the ADRs already mandate containers + abstractions; Phase A→B is mostly infra/config, minimal code change. **Caveat:** confirm the VPS region satisfies UU PDP for where personal data physically resides (§16).

- **Environments:** local → staging → production. Separate Xendit (test/live), Bunny libraries, Google OAuth credentials, DB per env.
- **Secrets:** key vault / env injection; never in repo, never in Next client bundles.
- **CI:** build + lint + unit/integration tests + EF migration check + OpenAPI client generation + dependency scan. **CD:** containerized .NET API; Next on a Node/ISR-capable host; DB migrations gated on deploy.
- **Contracts:** .NET emits OpenAPI → frontend client generated from it (no hand-written API types).
- **Selected providers:** email **Amazon SES** (behind `IEmailSender`); object storage **Cloudflare R2** (S3-compatible, behind `IObjectStorage`, signed URLs for entitled downloads); certificate PDF **PDFsharp/MigraDoc** (.NET-native, MIT — free commercial with no revenue cap; QuestPDF rejected for its $1M revenue cliff, iText for AGPL); ISR cache handler Redis when multi-instance.

---

## 14. Testing strategy (critical paths first)
Automated coverage required on: auth (Google SSO, email-verify-before-purchase, change/reset password, logout-all, refresh rotation + reuse detection), **payment webhook idempotency**, **app-side prorated upgrade**, **entitlement / signed playback** (locked vs. preview vs. entitled), **progress retention across downgrade/expiry**, **certificate issuance on level completion**, account-deletion anonymization, notification preference honoring, feedback recording. Plus: ISR revalidation on publish; tour keyboard accessibility. E2E on the purchase→watch→complete→certificate happy path.

---

## 15. Milestone slicing (for execution, incl. Claude Code)
Goal: a **thin sellable slice** before the full P0 surface. Suggested order — each milestone is independently reviewable.

| M | Milestone | Contents |
|---|-----------|----------|
| **M0** | Foundation | Repos, `CLAUDE.md`, .NET solution skeleton (4 layers), Next App Router skeleton, PostgreSQL + EF Core baseline migration, CI, OpenAPI→client. |
| **M1** | Auth + identity | Register/login, Google SSO, email verification (P0 transactional email), refresh rotation + cookie/BFF, change/reset password, logout-all, roles, **self-service account deletion + scheduled anonymization (P0)**. |
| **M2** | Catalog + entitlement (read) | Levels/Tracks/Modules read APIs, public ISR catalog + module landing, `canAccess`, preview handling. |
| **M3** | Payments + subscriptions | Plans, Xendit checkout, webhook (idempotent, signed), state machine, entitlement-on-webhook, prorated upgrade, billing history. |
| **M4** | Player + progress + certificates | Signed Bunny playback, player + quality selector, progress save/resume/rollup, retention rule, **immutable** certificate + `/verify/{code}`, **+ minimal admin (publish/unpublish, price edit) and seeded Basic content**. **← first sellable slice (Basic).** |
| **M5** | Admin | Users, pricing, curriculum CRUD + publish/reorder + `is_preview`, audit log, core analytics. |
| **M6** | Static/legal + onboarding | All §5.8 pages, cookie consent, driver.js tour, interest survey, what's-new. |
| **M7** | Engagement (fast-follow) | Notifications, feedback/ratings, notes/bookmarks (P2), quizzes (P1, activates completion-gating). |

**Decision (confirmed):** M4 launches with **seeded Basic content + a minimal admin** (publish/unpublish, price edit); the **full curriculum-management UI is M5**. This keeps the first sellable slice free of a full admin build. Transactional emails ship within M1–M4 alongside their triggers; the notification center is M7.

---

## 16. Open items requiring verification (before/with build)
1. **Xendit Subscriptions generation** — build on current vs legacy product; confirm proration-via-MIT + recurring-amount-update flow against current API. [Needs verification]
2. **On-demand ISR on chosen host** — host = Cloud Run / Azure Container Apps (decided); confirm on-demand revalidation + Redis ISR cache handler behaves across instances. [Needs verification]
3. **Bunny UU PDP posture** — confirm DE primary-storage region is acceptable for all stored metadata. [Needs legal verification]
4. ~~**.NET 10 GA/LTS**~~ — **CONFIRMED GA Nov 11 2025, LTS to Nov 2028** (EF Core 10 / ASP.NET Core 10 / C# 14). Note: ASP.NET 10 returns 401/403 for API endpoints instead of cookie redirects (aligns with §7); Identity passkey support available (future 2FA).
5. **Pricing finalization** — placeholder tier prices in PRD must be set before payments go live.
6. ~~**Dark mode**~~ — **RESOLVED: deferred post-v1** (light-only); driver.js spotlight caveat out of scope for v1.

---

*End of TSD v0.4 (Draft). On approval, the execution package — `CLAUDE.md`, EF Core schema + migrations, and per-milestone specs (M0…M7) — is generated from this document.*
