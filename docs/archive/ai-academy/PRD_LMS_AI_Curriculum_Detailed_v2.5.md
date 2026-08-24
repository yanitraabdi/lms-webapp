# Product Requirements Document (PRD)
## AI Productivity Academy — LMS with AI Learning Curriculum (Detailed)

| | |
|---|---|
| **Document version** | 2.5 (Draft — detailed) |
| **Date** | June 2026 |
| **Status** | For review |
| **Product** | Subscription video LMS hosting a structured AI-skills curriculum |
| **Primary market** | Indonesia — corporate employees told to adopt AI; also business, tech, developers |
| **UI / content language** | Bahasa Indonesia |
| **Tech stack** | .NET 10 (backend) · **Next.js (App Router) + TypeScript** (frontend, per TSD ADR-01) · PostgreSQL · email + Google SSO (multi-provider auth, see §5.1) |
| **Video** | **Bunny Stream** — signed/expiring HLS playback, behind an `IVideoProvider` abstraction (see §7) |
| **Payments** | Xendit (monthly + annual, recurring) |

> This v2 expands v1 by integrating the **curriculum** (55 modules across Basic/Intermediate/Advanced) with the platform, and detailing how content structure, access tiers, progress, and certificates work together. Where v1 covered the platform, this version makes the learning product concrete.

> **v2.2 decisions (open-questions resolved):** email verification **required before purchase**; upgrade billing is **prorated, computed app-side** (Xendit has no native proration — see §5.2); **lifetime pricing deferred**; free preview is **an admin-toggleable per-module flag** (not a fixed list); **SSR/prerender confirmed** for marketing/SEO pages; **quizzes are P1** — when shipped they gate module completion and level certificates (P0 completion = watch threshold only); **PPN/tax invoicing deferred** to a later phase; **Microsoft/LinkedIn SSO dropped for now** (Google-only, abstraction retained); account deletion = **soft-delete + anonymization**.

> **v2.3 change:** quizzes moved back to **P1** (out of the initial sellable release). P0 module completion = watch threshold only; quiz-gating activates when the quiz feature ships.

> **v2.4 changes (ambiguity-log resolutions):** capstones are **non-gating in v1** — certificates are driven by module completion, capstone review deferred (§2.5); **certificates are immutable once issued** (§2.5, §5.6); `watch_progress` is **retained indefinitely** (§5.2 AC8); **account deletion = P0/M1** (§5.1); **transactional emails are P0**, the notification center/preferences is P1 (§5.9); price changes **grandfather until renewal** with a price snapshot on the subscription (§5.7, §9); public verify uses **`/verify/{code}`** (§2.5, §5.8); **quiz-gating is non-retroactive** (§5.5); **watermarking = P1 capability, Phase 4 delivery** (§7 R4); refunds = **policy + manual handling in v1** (§5.8); providers selected — **Amazon SES** (email), **Cloudflare R2** (object storage), **PDFsharp/MigraDoc** (cert PDF, MIT) (§8); captions are **content-ops** (§7 R3); **dark mode deferred** post-v1.

> **v2.5 changes (post-review nits + deployment):** frontend committed to **Next.js** (§8, was stale); **recommended-next-module P0 fallback** defined (§5.6 AC1); **graduate display rule** specified — certified levels always show 100% with new modules as bonus (§5.6); **grandfathering rollover mechanics** defined (§5.7 AC2); `Certificate` data model synced with TSD (§9); AC numbering fixed (§5.5, §5.6). **Note: the TSD is the source of truth for the data model & deployment.** Certificate PDF lib = **PDFsharp/MigraDoc (MIT)**, replacing QuestPDF.

---

## 1. Overview

### 1.1 Purpose
A subscription LMS that delivers a structured, Indonesian-language curriculum teaching professionals to use AI productively — from "what is AI" to building MCP servers and RAG systems — behind a paywall, with progress tracking and verifiable certificates. The primary buyer is the corporate employee pushed by their boss to use AI; the curriculum is built so each short video produces one usable skill.

### 1.2 Problem statement
Indonesian professionals are told to "use AI" but have no structured, local-language path. Global courses are English and untailored to local corporate context. There's no single place to progress from absolute basics to advanced building, with proof of completion for employers.

### 1.3 Vision
The default Indonesian platform where professionals and teams learn AI step by step — sold B2C (individuals) and B2B (companies buying seats), with certificates that matter to employers.

### 1.4 Goals & success metrics
| Goal | Metric | Target (first 6 months) |
|------|--------|--------------------------|
| Paying users | Paid conversions | 300 |
| Engagement | Avg. completion rate per enrolled level | ≥ 40% |
| Retention | Monthly subscription churn | < 8% |
| B2B traction | Companies with ≥ 5 seats | 5 |
| Quality signal | Certificates issued | ≥ 150 |
| Funnel | Free-preview → paid conversion | ≥ 5% |

---

## 2. The AI Learning Curriculum (product content)

The curriculum is the product. The platform exists to deliver it. Content is organized **Level → Track → Module (video)**. Videos are 7–10 minutes; complex topics are split into mini-series so each video stays in range and teaches one usable skill.

### 2.1 Structure overview
| Level | Primary audience | Tracks | Modules | Outcome |
|-------|------------------|--------|---------|---------|
| **Basic** | Corporate employees & normies | 4 | 12 | Confident daily AI use (prompting + 5 tools) |
| **Intermediate** | Business people & tech enthusiasts | 7 | 25 | Master tool ecosystems + team workflows |
| **Advanced** | Developers & power users | 6 | 18 | Build MCP, RAG, agents, deploy to production |
| **Total** | All segments | 17 | **55** | ≈ 7–9 hours of content |

### 2.2 Access model (tier → content)
Tiers are **cumulative** (higher tier unlocks all lower content). Content-to-tier mapping is admin-configured, not hardcoded.

| Tier | Price (placeholder, admin-editable) | Unlocks | Certificate |
|------|--------------------------------------|---------|-------------|
| **Free** | Rp 0 | Basic modules flagged `is_preview` by admin (default 3) | No |
| **Beginner** | e.g. Rp 99k/mo · Rp 990k/yr | All Basic (12) | Basic |
| **Intermediate** | e.g. Rp 199k/mo · Rp 1,990k/yr | Basic + Intermediate (37) | Basic + Intermediate |
| **Advanced** | e.g. Rp 299k/mo · Rp 2,990k/yr | All (55) | All |

### 2.3 Module metadata (per video)
Each module carries: title, Level, Track, **category** (cross-cutting grouping, e.g. "Productivity Tools", "Building with Claude"), **tags** (e.g. `prompting`, `claude`, `mcp`, `rag`), description, summary, duration, required tier, curriculum order, video asset reference, thumbnail, resources (cheat-sheet PDF, prompt library, links), and an **end-of-module quiz (5 questions, P1)** that — once shipped — gates module completion (§5.5).

### 2.4 Curriculum map (tracks & module counts)
**Basic (12):** Foundation (3) · Tool Landscape (4) · Prompting Fundamentals (3) · Quick Wins (2).
**Intermediate (25):** Tool Deep Dive — Claude ecosystem (4) · OpenAI ecosystem (4) · Google ecosystem (4) · Other tools (3) · Multimodal & Creative (3) · Workflow & Automation (4) · Data & Knowledge incl. RAG intro (3).
**Advanced (18):** Claude Code (3) · API & Foundation (3) · Build Your Own MCP mini-series (4) · RAG Production mini-series (3) · Clawdbot/OpenClaw mini-series (3) · Agent & Production (2).

> The full 55-module list and per-module detail live in the curriculum document and are loaded into the platform as content records. The platform must not assume a fixed module list — admins add/edit/reorder modules over time (the curriculum evolves, especially Tool Deep Dive content).

### 2.5 Capstone & certificates per level
- **Capstones are encouraged but do NOT gate certificates in v1.** Certificates are issued on module completion (§5.6); a capstone submission/review surface and the "capstone reviewed" notification are deferred to a later phase. Revisit gating if the credential's employer value should depend on reviewed capstones.
- **Basic capstone:** "Productivity Day Plan" — document a workday using AI (≥5 tasks). PDF reflection.
- **Intermediate capstone:** "Team Workflow Blueprint" — design an AI workflow for their team (SOP + ≥1 custom Project/GPT/Gem) + 5-min demo video.
- **Advanced capstone:** "AI System in Production" — build an MCP server / RAG system / Clawdbot setup / multi-agent system; public repo + docs + demo.
- Completing **100% of a level's published modules** issues a **certificate (PDF)** with name, level, date, and a **public verification URL** (`/verify/{code}`, using the unguessable `verification_code`) — a key selling point for LinkedIn and corporate L&D. **Certificates are immutable once issued**: level completion is snapshotted at issuance, so later-added modules surface as new content (§5.6 AC6) but never revoke a certificate or push a graduate below 100%.

---

## 3. Scope

### 3.1 In scope
Authentication (email/password + **Google SSO**, multi-provider-ready; change password; resend verification; logout-all; self-service account deletion); subscription plans + Xendit (monthly/annual, recurring); **progress retention across downgrade/expiry**; video catalog with Level/Track/category/tag filtering & search; video detail + secure player with progress tracking & resume + **manual quality selector**; resources/downloads; per-module quizzes; **per-module feedback & ratings**; **in-video notes & bookmarks**; user dashboard with progress + certificates + **"what's new" indicator** + **signup interest survey**; **unified notifications (in-app + email)**; **guided onboarding product tour (driver.js)**; admin (users, pricing, content/curriculum); expanded static/legal pages (About, Contact, FAQ, Help Center, Feedback, How it Works, For Business, Testimonials, Blog, Terms, Privacy, Refund, Cookie, Accessibility).

### 3.2 Out of scope (later)
B2B seat management & company L&D dashboard (Phase 2; **data model is B2B-ready now** — see §9); **2FA/MFA** (P2); **session/active-device manager** (P2); **Microsoft Entra / LinkedIn SSO** (dropped for now — Google-only); community/forum; live Q&A; advanced quiz grading/proctoring; in-browser coding labs; in-video transcript search; mobile native app; in-LMS AI assistant; **PPN/tax-compliant corporate invoicing** (deferred — see §12 #10).

### 3.3 Key assumptions (confirm)
- **A1** Tier stacking is cumulative (recommended).
- **A2** Billing monthly + annual (annual discounted); lifetime optional later.
- **A3** Free tier = Basic modules an admin flags as preview (`Module.is_preview`, required_tier 0); default 3, but admin-configurable — not a hardcoded list.
- **A4** IDR only; **A5** one account = one user (B2B seats Phase 2).
- **A6** Videos uploaded/managed by admins; hosted on an external streaming provider with signed playback.
- **A7** Video provider = **Bunny Stream** (decided), accessed only through an `IVideoProvider` abstraction so it can be swapped via a single adapter.
- **A8** Data model is **B2B-ready from day one** (Organization/Seat entities defined now; seat-management UX ships Phase 2).
- **A9** On downgrade/expiry, **watch progress and issued certificates are retained** (read-only); content access is re-gated by entitlement but completion history is never destroyed.

---

## 4. User roles & personas
| Role | Needs |
|------|-------|
| **Guest** | Browse catalog previews & pricing, sign up |
| **Free user** | Watch preview modules, see locked content + upgrade prompt |
| **Subscriber** | Watch entitled content, track progress, earn certificates, manage billing |
| **Admin** | Manage users, pricing, content/curriculum |
| **Super Admin** | All admin + manage admins + financial settings |

**Persona — "Pak Budi" (primary):** ops manager, non-technical, told to use AI. Wants a clear Indonesian path and a certificate to show completion.
**Persona — "Sari, L&D Manager" (B2B, Phase 2):** wants to buy seats for her team and see who completed what.

---

## 5. Functional requirements
Priority: **P0** = MVP, **P1** = fast-follow, **P2** = later.

### 5.1 Authentication (P0)
- AC1: Email/password registration (name, email, password ≥8 chars); duplicate-email rejected; passwords hashed (ASP.NET Core Identity).
- AC2: **Google SSO** on signup & login, equal prominence; account linking by verified email (no duplicate user); Google accounts auto-`email_verified`.
- AC3: JWT access + refresh rotation; email verification for email/password signups; forgot/reset password (time-limited link).
- AC4: Roles: User, Admin, Super Admin; rate limiting + lockout on auth endpoints.
- AC5: **Change password** while authenticated (requires current password; invalidates other sessions; confirmation email). (P0)
- AC6: **Resend email-verification** link (rate-limited). (P0)
- AC7: **Logout** (revoke current refresh token) and **logout-all-devices** (revoke all refresh tokens for the user). (P0)
- AC8: **Self-service account deletion** (UU PDP right to erasure): **soft-delete + anonymization** — soft-delete with a configurable grace window (default 30 days, account recoverable on login), after which PII is anonymized and the record retained only for legal/financial integrity; any active subscription must be canceled first; fully audit-logged. **(P0, ships M1.)**
- AC9: External login kept **multi-provider-capable** at the data layer (`UserExternalLogin.provider`), but **MVP ships Google SSO only**. Microsoft Entra / LinkedIn are explicitly out of scope for now (§3.2).
- AC10: **Email verification is required before purchase.** Account must exist and be verified before checkout — email/password users verify via link first; Google SSO users are auto-verified, satisfying this immediately. Content access additionally requires entitlement.
- AC11: Optional **2FA/MFA** (email OTP or TOTP). (P2)

### 5.2 Subscription plans & payments (P0)
- AC1: Pricing page renders tiers from DB (admin-editable).
- AC2: Subscribe → **Xendit** checkout (VA, e-wallet, QRIS, cards as available); recurring auto-renewal.
- AC3: Subscription states: active / past_due / grace / canceled / expired; transitions logged.
- AC4: **Entitlements change only on verified server-side Xendit webhook** (idempotent, signature-verified) — never trust the client.
- AC5: On success → activate/extend, invoice record, confirmation email. On failure → past_due → grace → expired with retries.
- AC6: User can cancel (active until period end → downgrades to Free), **upgrade (immediate, prorated)**, downgrade (next renewal). **Proration is computed app-side** (Xendit has no native proration): on upgrade, charge `delta = (newTierPrice − currentTierPrice) × remainingDays ÷ cycleDays` as an immediate merchant-initiated charge on the stored payment token, then set the recurring amount to the new tier for subsequent cycles. New entitlement is granted only on the verified webhook (AC4). *Confirm which Xendit Subscriptions generation (new vs legacy) to build against — needs verification.*
- AC7: Billing history with invoices/receipts.
- AC8: **Progress retention on downgrade/expiry (P0)** — `WatchProgress` rows and issued `Certificate`s are preserved (read-only) when a user downgrades or a subscription expires. Content access is re-gated by entitlement, but completion history and certificates are never deleted. Both certificates and in-progress `WatchProgress` are **retained indefinitely** (rows are tiny; retention drives re-subscribe value); no cleanup job is planned.

### 5.3 Video catalog (P0)
- AC1: Hierarchy **Level → Track → Module**; module also has one category + multiple tags.
- AC2: Filter by Level, category, tag (multi-select), and free-text search (title + description); sort by curriculum order (default), newest, popularity (P1).
- AC3: Locked modules visible (title, thumbnail, duration, description) with clear "Upgrade to [tier]" CTA.
- AC4: Each card shows completion status (not started / in progress / completed) and level badge + tags.
- AC5: Pagination/lazy load for large catalogs.

### 5.4 Video detail & player (P0)
- AC1: Detail page: player, title, description, summary, duration, tags, resources, next-module navigation.
- AC2: **Access verified server-side before issuing a signed, expiring playback URL** (never a permanent public URL).
- AC3: Resume from last position; progress saved periodically (~every 10–15s, on pause/exit).
- AC4: Module marked **completed** when watched ≥ configurable threshold (default 90%). **(P1)** Once quizzes ship, completion additionally requires a passing quiz attempt for modules that have a quiz (§5.5). Completion drives Track/Level rollups and certificate eligibility (§5.6 AC4).
- AC5: Player: play/pause, seek, speed, volume, fullscreen, Indonesian captions.
- AC6: Resources downloadable only by entitled users.
- AC7: Progress updates roll up to Track/Level progress and dashboard in near-real-time.
- AC8: **Manual video quality selector** (P1) — explicit resolution choice (e.g. 240p–1080p) with a current-bitrate / data-usage hint, in addition to automatic ABR. Important for constrained Indonesian networks and data caps.
- AC9: **In-video notes & bookmarks** (P2) — timestamped notes and bookmarks per module, surfaced on module detail and the dashboard.

### 5.5 Quizzes (P1)
- AC1: 5-question quiz per module; pass threshold configurable (e.g. 4/5). Unlimited retries unless admin caps them.
- AC2: Quiz attempts recorded; once this feature ships, **a passing attempt is required to mark a module complete** (§5.4 AC4) for modules that have a quiz — and therefore to progress level completion → certificate.
- AC3: Admin authors/edits quiz questions and the pass threshold per module (§5.7 AC3).
- AC4: Modules without an authored quiz use the watch-threshold only (legacy/preview content isn't blocked).
- AC5: **Quiz-gating is non-retroactive.** Enabling a quiz applies only to *new* completions; users who already completed a module via the watch threshold remain completed and keep any issued certificate.

### 5.6 User dashboard & certificates (P0)
- AC1: Dashboard home: active plan, overall progress %, "continue watching", **recommended next module**. **P0 source:** the next incomplete module in curriculum order within the user's entitled content (deterministic fallback). Survey-based personalization (AC8) layers on top when shipped — it never replaces the P0 fallback.
- AC2: Progress tracker per Level and Track (e.g. "Basic: 8/12, 67%"). **Certified levels are special-cased** — see AC6.
- AC3: Summary: videos watched, total watch time, streaks (P1).
- AC4: **Certificate** generated on completion of 100% of a level's *published modules at that time* (PDF: name, level, date, unique `verification_code`) with public **verification URL** (`/verify/{code}`). Capstones do not gate issuance in v1.
- AC5: Profile management (name, email change → re-verify, password, avatar P1); subscription management.
- AC6: **Certificates are immutable, and graduates always display as complete.** Once a certificate exists for a (user, level), that level is never re-evaluated: the dashboard shows it at **100% regardless of newly added modules**, which surface only as **"bonus / new content available"** (tracked separately, e.g. "2 new modules since you finished"). New modules never grow that user's denominator, invalidate the certificate, or reduce shown completion. (Implementation rule for the §9 rollup.)
- AC7: **"What's new" indicator (P1)** — modules published or refreshed since the user's last visit are badged ("Baru"); a changelog/new-content feed surfaces additions (retention driver; ties to the §11 refresh cadence). This also drives the "bonus content" surfacing in AC6.
- AC8: **Signup interest survey (P1)** — one-time, skippable survey (role, goals, preferred tools) captured at onboarding to *enhance* the recommended next module (AC1); editable later in profile.

### 5.7 Admin & curriculum management (P0)
- AC1: **User management** — list/search/filter; view detail (subscription, progress, certificates, payments); suspend/reactivate; manually grant/revoke a plan (B2B/comps) with reason logged; manage admin roles (Super Admin).
- AC2: **Pricing/plans** — create/edit tiers (name, description, monthly/annual price, included content mapping, active); **price changes grandfather existing subscribers until their next renewal** (the subscription snapshots its price at signup — §9), then the new price applies. **Rollover mechanics:** at the renewal boundary a scheduled job updates the Xendit recurring-plan amount (Update-Plan API) and the subscription's `price_locked`; a **prior-notice email** is sent a configurable window before renewal (default **14 days**) via the notification system. New subscribers get the new price immediately; promo codes (P1).
- AC3: **Content/curriculum** — create/edit/delete Levels, Tracks, Categories, Tags; upload/attach video + thumbnail + metadata + resources; set required tier and curriculum order; **reorder modules within a track**; draft vs published (unpublished invisible to users); bulk tag/move; manage capstones & quizzes.
- AC4: **Analytics** — active subscribers by tier, MRR, signups, conversions, churn, most-watched modules, completion rates, certificates issued (core P0; charts/CSV P1).
- AC5: Full audit log of pricing, manual grants, role changes.

### 5.8 Static pages (P0 unless noted)
**Core/legal (P0):** About; Contact (form → support inbox + stored, spam-protected); FAQ (admin-editable CMS-lite); **Terms of Service**; **Privacy Policy (UU PDP)**; **Refund Policy** (subscription + Indonesian consumer expectations; **v1 = policy page + manual/out-of-band refunds**, no automated Xendit refund flow — an admin manual-adjustment action is a later enhancement); **Cookie Policy + consent banner**; **Accessibility Statement** (WCAG target, §6); public **Certificate Verification** (`/verify/{code}`); system pages (404, error, maintenance); **Sitemap**.
**Acquisition/trust (P1):** **How it Works**; **For Business** (B2B lead capture, live before B2B features ship); **Testimonials/Success Stories**; **Blog/Articles** (SEO).
**Support (P1):** **Help Center / Knowledge Base** (deeper than FAQ); **Feedback** page (product-level — see §5.10 AC3).
All SEO-friendly (these drive acquisition).

### 5.9 Transactional email (P0) & notification center (P1)
**Transactional email — P0 (must exist at launch):** email verification (required before purchase), password reset, change-password confirmation, payment confirmation/receipt, and dunning (past_due / grace / expiring). These ship with the features that trigger them, independent of the notification center.

**Notification center — P1:**
- AC1: Unified notification model with an **in-app notification center** plus an **email** channel.
- AC2: In-app events: new/refreshed module, certificate issued, subscription renewing / past_due / grace / expiring (dunning), payment receipt. *(The "capstone reviewed" event is deferred with the capstone-review surface — §2.5.)*
- AC3: Per-category, per-channel **preferences** with unsubscribe links (UU PDP / anti-spam compliance).

### 5.10 Feedback & ratings (P1)
- AC1: **Per-module rating** (e.g. 1–5 or thumbs) + optional free-text feedback, by entitled users (one per user+module, editable).
- AC2: Aggregate rating exposed to **admin analytics** as a quality signal; optionally surfaced in catalog and usable for "popularity" sort (§5.3 AC2 P1).
- AC3: **Product-level feedback form** (separate from per-module) routed to the support inbox + stored.

### 5.11 Onboarding & product tour (P1)
- AC1: First-run **guided tour using driver.js (MIT-licensed)** covering catalog, player, progress, and certificates; dismissible and replayable from Help.
- AC2: Tour content **localized (Bahasa Indonesia)** and meets keyboard-navigation / WCAG targets (§6).
- AC3: Per-user tour state stored (completed / skipped) so it isn't re-shown.

---

## 6. Non-functional requirements
| Category | Requirement |
|----------|-------------|
| **Performance** | Catalog/dashboard < 2s on typical ID broadband/4G; video starts < 3s. |
| **Scalability** | 10k users, 1k concurrent viewers without redesign; video delivery offloaded to CDN. |
| **Availability** | 99.5%; graceful degradation if Xendit/video provider down. |
| **Security** | HTTPS; secrets server-side; signed video URLs; webhook signature verification; rate limiting on auth & payment. |
| **Privacy** | UU PDP alignment; clear privacy/terms; account deletion on request. |
| **Localization** | Bahasa Indonesia UI; i18n-ready for English later. |
| **Accessibility** | WCAG 2.1 AA targets; captions; keyboard nav. |
| **Observability** | Logging, error tracking, alerts on payment/webhook/video errors. |
| **Backup/DR** | Daily PostgreSQL backups, tested restore, ≥30-day retention. |

---

## 7. Video hosting & content protection (P0 — critical)
A paid video product must prevent casual piracy.
- R1: **Never** serve from a public URL/plain bucket. **Provider = Bunny Stream (decided)**, accessed only via an `IVideoProvider` abstraction (`CreateUploadTarget`, `GetSignedPlaybackUrl(assetId, userId, ttl)`, `HandleProviderWebhook`) so it can be swapped via a single adapter. Rationale: per-GB pricing favors short-form VOD (7–10 min modules); transcoding, token auth, signed URLs, and DRM (MediaCage) are included; Singapore PoP gives good Indonesian latency. **Caveat:** Bunny's primary storage region (Falkenstein, DE) cannot be disabled — likely fine for course video (not personal data), but confirm UU PDP handling for any sensitive metadata. *Pricing/latency verified June 2026; re-check before contract.*
- R2: Playback URL generated **per session, server-side, after entitlement check**, short expiry.
- R3: Adaptive bitrate for varying Indonesian networks **plus a manual quality selector (§5.4 AC8)**; Indonesian captions (WebVTT — **produced by content-ops**: manual upload or optional Bunny transcription; engineering only uploads/serves WebVTT).
- R4: Optional email watermark on higher tiers — **P1 capability, delivered Phase 4** (higher tiers don't exist until Advanced/Phase 4); Bunny supports overlay/DRM.
- R5: Provider decided (Bunny) and isolated behind `IVideoProvider` — backend, upload flow, player, and signed-URL endpoint design against the abstraction, not the vendor.

---

## 8. Architecture
- **Frontend:** **Next.js (App Router) + TypeScript — committed (TSD ADR-01).** Single codebase: public pages (marketing/catalog/module/verify) are SSG/ISR for SEO; the authenticated app (player, dashboard, admin) is client-rendered. .NET 10 remains the only business API. **driver.js** for the onboarding tour. (See TSD §4 for the per-route rendering matrix and §13 for deployment.)
- **Backend:** .NET 10 (ASP.NET Core Web API), Identity (multi-provider external login: Google in MVP, Microsoft/LinkedIn P2), EF Core; `IVideoProvider` abstraction (Bunny adapter); `NotificationService` (in-app + email).
- **Database:** PostgreSQL.
- **Video:** Bunny Stream (signed HLS playback) behind `IVideoProvider`.
- **Payments:** Xendit (webhooks). **Email:** Amazon SES (behind an abstraction). **Object storage:** Cloudflare R2 (S3-compatible, no egress) for PDFs/certs/thumbnails. **Certificate PDF:** PDFsharp/MigraDoc (.NET-native, **MIT — free commercial, no revenue cap**; QuestPDF rejected for its $1M-revenue license cliff; iText rejected as AGPL). **Hosting:** containers on GCP Cloud Run or Azure Container Apps + CDN; Next runs as a Node server (on-demand ISR). **Dark mode:** deferred post-v1 (light-only).

```
[React SPA] --JWT--> [.NET 10 API] --> [PostgreSQL]
     | (signed URL)        |--> [Object storage: PDFs/certs/thumbnails]
     v                     |--> [Email provider] (notifications)
[Bunny Stream (signed HLS)]|--> [Xendit] <--webhooks--> [/api/webhooks/xendit]
                           |--> [Bunny webhooks] --> [/api/webhooks/bunny]
```

**Access rule:** `canAccess(user, module) = user has active subscription whose tier ≥ module.required_tier` (Free preview = tier 0). Evaluated server-side on every protected resource (playback URL, resource download).

---

## 9. Data model (key entities)
- **User** — id, email, password_hash (nullable for SSO), name, role, email_verified, status (incl. `deleted`), **deleted_at, anonymized_at** (UU PDP erasure).
- **UserExternalLogin** — **provider** (Google now; Microsoft/LinkedIn later), provider_key (account linking). *Multi-provider by design.*
- **Organization** *(B2B-ready, surfaced Phase 2)* — id, name, billing_owner_user_id, status.
- **OrgSeat / OrgMembership** *(B2B-ready)* — org_id, user_id (nullable until assigned), seat_status, member_role.
- **Plan** — name, tier_level (0–3), price_monthly, price_annual, included content mapping, is_active.
- **Subscription** — user_id, **org_id (nullable, for per-seat later)**, plan_id, **price_locked + billing_cycle (snapshot at signup → grandfathering, §5.7 AC2)**, status, current_period_start/end, xendit_ref.
- **PaymentTransaction** — user_id, amount (IDR), method, status, xendit ids, raw_payload.
- **Level / Track / Category / Tag** — curriculum structure; Level.required_plan_tier.
- **Module (Video)** — track_id, category_id, title, description, summary, duration, provider_asset_id (Bunny), thumbnail, order, status (draft/published), **is_preview (admin flag → free tier)**, **published_at / last_refreshed_at** (drives "what's new"), required_plan_tier.
- **ModuleTag** (m:n), **Resource** (module_id, type pdf/link, ref).
- **WatchProgress** — user_id, module_id, resume_position, percent_complete, completed, last_watched_at (unique per user+module). **Never hard-deleted on downgrade/expiry (A9); access re-gated by entitlement only.**
- **Quiz / QuizQuestion / QuizAttempt** (P1) — quiz per module; pass_threshold; attempts store score + passed; once shipped, a passing attempt gates module completion.
- **Capstone / CapstoneSubmission** — per level.
- **Certificate** — user_id, level_id, issued_at, verification_code (unique), pdf_url, **completed_module_ids (snapshot of qualifying set at issuance)**. **Immutable once issued and retained indefinitely** regardless of subscription state; the level is never re-evaluated afterward (§5.6 AC6). *(The TSD §6 schema is the source of truth for the data model.)*
- **Notification** — user_id, type, payload, channel (in_app/email), read_at, created_at. *(P1)*
- **NotificationPreference** — user_id, category, channel, enabled. *(P1)*
- **ModuleFeedback** — user_id, module_id, rating, comment, created_at (unique per user+module). *(P1)*
- **OnboardingSurvey (UserInterest)** — user_id, role, goals, preferred_tools, created_at. *(P1)*
- **VideoNote / Bookmark** — user_id, module_id, timestamp_seconds, type (note/bookmark), text. *(P2)*
- **TourState** — user_id, tour_key, status (completed/skipped), updated_at. *(P1)*
- **FaqItem**, **ContactSubmission**, **FeedbackSubmission** (product-level), **AuditLog**.

**Entitlement:** `canAccess(user, module) = active Subscription with plan.tier_level >= module.required_plan_tier` (preview modules require_tier = 0).
**Progress rollups:** Level/Track progress = completed published modules ÷ total in scope (cached, invalidated on update).

---

## 10. Key API surface (indicative)
| Method | Endpoint | Purpose | Auth |
|--------|----------|---------|------|
| POST | `/api/auth/register` `/login` `/refresh` | Auth | Public |
| GET | `/api/auth/google` `/google/callback` | Google SSO (multi-provider-ready) | Public |
| POST | `/api/auth/change-password` ; `/resend-verification` ; `/logout` ; `/logout-all` | Auth management | User |
| DELETE | `/api/account` | Self-service deletion (UU PDP) | User |
| GET | `/api/plans` | Plans & prices | Public |
| POST | `/api/subscriptions/checkout` | Start Xendit checkout | User |
| POST | `/api/webhooks/xendit` | Payment events (idempotent, signed) | Xendit |
| POST | `/api/webhooks/bunny` | Video asset/encode events (signed) | Bunny |
| GET | `/api/subscriptions/me` ; POST `/cancel` `/change` | Manage subscription | User |
| GET | `/api/catalog` | Catalog w/ filters (level/category/tag/search) | User/Guest |
| GET | `/api/catalog/new` | New/refreshed content feed ("what's new") | User |
| GET | `/api/modules/{id}` | Module detail | User (entitlement-aware) |
| POST | `/api/modules/{id}/playback` | Signed playback URL (entitlement-checked) | User |
| POST | `/api/modules/{id}/progress` | Save progress | User |
| GET/POST | `/api/modules/{id}/feedback` | Per-module rating/feedback | User |
| GET/POST/DELETE | `/api/modules/{id}/notes` | In-video notes/bookmarks | User |
| GET/PUT | `/api/notifications` ; `/notifications/preferences` ; POST `/{id}/read` | Notifications | User |
| GET/POST | `/api/onboarding/survey` ; GET/PUT `/api/onboarding/tour` | Interest survey & tour state | User |
| POST | `/api/feedback` | Product-level feedback | User/Public |
| GET | `/api/dashboard` | Dashboard data | User |
| GET | `/api/certificates` ; `/api/verify/{code}` | List / public verify | User / Public |
| GET/POST/PUT/DELETE | `/api/admin/...` | Users, plans, curriculum, feedback, analytics | Admin |
| POST | `/api/contact` ; GET `/api/faq` | Static pages | Public |

---

## 11. Roadmap & phasing
**Phase 1 — Validation:** produce 3 Basic preview modules as a free lead magnet; collect emails + survey; validate pricing/length/topics.
**Phase 2 — Launch Basic:** full 12 Basic modules + resources + capstone (**quizzes P1, fast-follow**); auth (Google SSO, **email verification required before purchase**, change/reset password, resend verification, logout-all, account deletion); Xendit recurring + **app-side prorated upgrades**; **progress-retention policy**; catalog/player/progress (signed Bunny playback) + manual quality selector; SSR/prerendered marketing + catalog; dashboard + certificates + **signup interest survey** + **"what's new" indicator**; **unified notifications**; **per-module feedback & ratings**; **guided onboarding tour (driver.js)**; admin; expanded static/legal pages. **Sell the Basic course.**
**Phase 3 — Intermediate:** 25 modules; bundle pricing; community (Discord/Circle); begin B2B outreach.
**Phase 4 — Advanced + B2B:** 18 modules + GitHub companion repos + live office hours; **B2B seat management + company L&D dashboard**; promo codes; richer analytics; watermarking.
**Maintenance:** budget to refresh ~30% of Intermediate "Tool Deep Dive" modules every 6 months (vendor features change fast); position Intermediate+ as perpetual subscription.

---

## 12. Open questions / decisions needed
1. ~~**Video provider**~~ — **RESOLVED: Bunny Stream**, behind `IVideoProvider` abstraction (§7).
2. ~~**Tier stacking**~~ — **RESOLVED: cumulative** (A1).
3. ~~**B2B seats**~~ — **RESOLVED: data model B2B-ready now** (§9); seat-management UX in Phase 2/4.
4. ~~**Email verification**~~ — **RESOLVED: required before purchase** (§5.1 AC10); Google SSO satisfies it automatically.
5. ~~**Proration**~~ — **RESOLVED: prorated on upgrade, computed app-side** (§5.2 AC6). *Remaining verification: which Xendit Subscriptions generation (new vs legacy) to integrate.*
6. ~~**Lifetime pricing**~~ — **RESOLVED: later** (deferred).
7. ~~**Free tier scope**~~ — **RESOLVED: admin-toggleable `Module.is_preview` flag** (default 3), not a fixed list (A3, §2.2).
8. ~~**SSR/SEO**~~ — **RESOLVED: yes**, SSR/prerender for marketing/catalog (§8). *Sub-decision (TSD): Next.js vs SPA + separate prerendered surface.*
9. ~~**Quizzes**~~ — **RESOLVED: P1** (out of initial release). When shipped, a passing quiz gates module completion for modules that have one; P0 completion = watch threshold only (§5.5).
10. **Tax/invoicing (PPN)** — **DEFERRED** (later phase, B2B). Confirm PPN handling & compliant invoices before B2B sales.
11. ~~**Microsoft Entra / LinkedIn SSO**~~ — **RESOLVED: not now** (Google-only; abstraction retained, §5.1 AC9).
12. ~~**Account-deletion mechanics**~~ — **RESOLVED: soft-delete + anonymization**, default 30-day grace window, configurable (§5.1 AC8).

---

## 13. Appendix — Definition of Done
Acceptance criteria pass; works on latest Chrome/Safari/Firefox + mobile web; localized in Bahasa Indonesia; loading/empty/error states; events logged; automated tests on critical paths (auth incl. Google SSO + email-verify-before-purchase + change/reset password + logout-all, payment webhook idempotency, **app-side prorated upgrade**, entitlement/signed-playback, progress→certificate (**quiz-gated completion when quizzes ship, P1**), **progress retained across downgrade/expiry**, **account deletion anonymizes PII**, **notification delivery honors preferences**, **per-module feedback recorded**); guided tour keyboard-accessible & localized; reviewed and merged.

---

*End of PRD v2.5 (Draft). The curriculum content (55 modules) is maintained in the curriculum document and loaded as platform content records.*
