# Functional Specification Document (FSD)
## INVERTA — TOEFL Preparation Program (v0.1 Draft)

| | |
|---|---|
| **Product** | INVERTA — online TOEFL-prep program (Bahasa Indonesia UI) |
| **Model** | **One-time program purchase/enrollment** (not subscription) |
| **Relationship to prior docs** | **Pivot.** The AI Productivity Academy PRD v2.5 / TSD v0.4 are **paused as product specs** but remain the **platform foundation**: stack, auth, video, payments plumbing, deployment, conventions (CLAUDE.md) are inherited. This FSD supersedes them for *product behavior*. Archived at [`docs/archive/ai-academy/`](archive/ai-academy/README.md). |
| **Early scale** | 20–50 enrollments/month (first 3 months) — sizing decisions assume this |
| **Status** | Draft — open questions in §10 (blocking items Q1/Q2/Q4 resolved 2026-08-14, see §12) |

> **Source note:** business input (13 Aug) contains a contradiction — "1 live + 9 video" vs
> "6 video × 15 menit." This FSD therefore treats **program structure as admin-configurable** (§3).
> Confirm the real session count (§10 Q1).

---

## 1. Product summary

A paid, **linear** TOEFL-preparation program. A student enrolls (one-time payment), then progresses
through an ordered sequence of sessions: recorded video lessons (each followed by a **required short
test** that gates the next session) plus at least one **live session** (Zoom/in-person). The program
ends with a **final online assessment** (multi-section: listening, reading, vocabulary,
written/structure) under **soft proctoring** (tab-switch detection, warn once, second violation =
auto-submit). The student instantly receives a **score** and a **TOEFL-prediction certificate by
email**.

**Primary difference from the prior product:** progression is *earned* (linear + test-gated),
certification is *score-based* (not completion-based), and monetization is *per-program* (not tiered
subscription).

---

## 2. What is inherited vs dropped from PRD v2.5 / TSD v0.4

**Inherited unchanged (platform):**
- Stack: .NET 10 (4-layer) · Next.js App Router · PostgreSQL · Tailwind · TanStack Query · next-intl (id).
- Auth (TSD §7 in full): email/password + Google SSO, JWT + rotating refresh cookie + BFF, email
  verification **required before purchase**, change/reset password, logout-all, soft-delete +
  anonymization (UU PDP). Golden rules 1–5, 9–10 in CLAUDE.md.
- Video: Bunny Stream behind `IVideoProvider`, signed per-session playback after server-side access
  check. Manual quality selector + ID captions.
- Payments rails: Xendit behind `IPaymentGateway`, **webhook-only entitlement**, idempotency ledger,
  reconciliation job.
- Email: SES behind `IEmailSender`. Storage: R2. Cert PDF: PDFsharp/MigraDoc.
- Deployment: §13.1 topology (self-host + Cloudflare Tunnel viable at this scale — 50 users/month
  makes Phase A comfortably sufficient).
- Static/legal set (§5.8): Terms, Privacy, **Refund policy (now for a one-time purchase — needs
  rewriting, §10 Q8)**, Contact, FAQ, verify page.

**Dropped (do not build):**
- Subscription tiers, cumulative unlock, recurring billing, proration, grandfathering
  (`price_locked` semantics change — see §7), dunning states, plan management.
- Free-preview browsing model, catalog filters/search as primary navigation (a simple program
  landing replaces it).
- Completion-based certificates (replaced by score-based).
- The 55-module AI curriculum, interest survey, what's-new feed (little value in a short linear program).

**Changed:**
- Quizzes: were P1/optional → **gating tests are P0 core**.
- Certificates: completion-triggered → **assessment-score-triggered**, emailed, with TOEFL-prediction band.
- New P0 capabilities: **assessment engine**, **soft proctoring**, **live-session step**, **audio
  playback (listening section)**.

---

## 3. Program structure (admin-configurable)

```
Program (e.g. "INVERTA TOEFL Prep Batch 1")
 └── Session 1..N (ordered; admin defines count & type)
      ├── type = video   → Bunny video (~15 min) + required gating test
      ├── type = live    → scheduled Zoom/in-person meeting (details + attendance)
      └── type = final_assessment → multi-section proctored test
```

- **R1:** A Program is an ordered list of Sessions; count and mix are admin-configurable (resolves
  the 9-vs-6 contradiction).
- **R2:** **Linear lock:** Session *k+1* is locked until Session *k* is completed. Completion means:
  video watched ≥ threshold (default 90%) **AND** its gating test passed; live session marked
  attended (§5); final assessment submitted.
- **R3:** Programs can run as **batches/cohorts** (a start date; live-session schedule attached).
  Multiple batches of the same program can exist. *(Resolved — see §12: optional batches.)*
- **R4:** Locked sessions are visible (title + status) but not playable — consistent with the prior
  "visible but locked" principle.

---

## 4. Enrollment & payment (one-time)

- **R1:** Public **program landing page** (SEO, SSG): syllabus, schedule, price, outcomes,
  testimonials → "Daftar" CTA.
- **R2:** Purchase requires a **verified account** (inherited rule). Flow: register/login → verify → pay.
- **R3:** Payment = **single Xendit invoice/charge** (VA, e-wallet, QRIS, cards). No recurring anything.
- **R4:** **Enrollment is granted only on the verified Xendit webhook** (golden rule 2 unchanged).
  The success page never grants access.
- **R5:** Enrollment record = (user, program/batch, amount paid, status:
  `pending_payment | active | completed | revoked`). Access check everywhere becomes
  `isEnrolled(user, program) && sessionUnlocked(user, session)`.
- **R6:** Receipt email on payment (P0 transactional set: verification, reset, receipt — dunning is gone).

---

## 5. Sessions

**Video sessions:** inherited player (signed Bunny HLS, resume, quality selector, captions);
progress tracked as before (`watch_progress` reused as-is). On reaching the watch threshold, the
**gating test unlocks** (same page).

**Live sessions (new, kept deliberately thin):**
- Store schedule (datetime, mode zoom/offline, join URL or location), show it in the student's
  program view, send a reminder email (e.g. H-1). **No Zoom API integration in v1** — the join URL is
  a link; attendance is **marked by admin** (bulk-mark UI). Integration with Zoom attendance APIs is
  a later enhancement. *(Deliberate scope cut — flag if the business expects automated attendance.)*
- Completion of a live session = admin marks attended (or admin can waive/mark all).

---

## 6. Assessment engine (P0 — the core new build)

### 6.1 Gating tests (after each video)
- Short (e.g. 5–10 questions), MCQ-first; per-test **pass threshold** (admin-set); instant auto-score.
- **Retake policy:** unlimited retries by default, admin-cappable per test. *(Default chosen for a
  paid program — failing students who paid is a support problem; confirmed §12.)*
- Passing unlocks the next session (R2 §3). Attempts recorded (score, timestamps, answers).

### 6.2 Final assessment (multi-section)
- **Sections:** Listening (audio + MCQ), Reading (passage + MCQ), Vocabulary (MCQ),
  Structure/Written expression (MCQ). *(Written = MCQ in v1 — free-text essays require human or AI
  grading and break "instant score"; out of scope v1. Confirm §10 Q5.)*
- **Timing:** per-section timers (admin-set); auto-submit at expiry; **one attempt by default**
  (§12 — an admin-granted retake issues a *new* certificate).
- **Listening audio:** stored in R2 (or Bunny audio), served via signed URL; per-question play-count
  limit (admin-set, e.g. play once) to mimic TOEFL conditions.
- **Instant scoring:** auto-scored on submit; raw per-section scores + total.
- **TOEFL prediction:** raw score → predicted TOEFL band via an **admin-maintained mapping table**.
  **Engineering builds the mapping mechanism; the business must supply the pedagogically valid
  mapping** (this is a content/credibility dependency, not code — §10 Q6). The certificate must say
  **"TOEFL Prediction"** clearly (it is not an official ETS score; avoid implying otherwise —
  trademark/claims caution, §10 Q7).

### 6.3 Question bank
- Admin CRUD: questions typed (mcq now; extensible), tagged by section/skill, with audio/passage
  attachments; tests assembled from the bank (fixed set v1; randomization later).

---

## 7. Soft proctoring (final assessment; optionally gating tests)

- **Mechanism:** client detects tab/window focus loss via the Page Visibility API + window blur.
  Strike 1 → prominent warning overlay ("Jangan tinggalkan halaman tes"). Strike 2 → **auto-submit**
  the assessment as-is, flagged `proctor_flagged = true`.
- **Server-side record:** every violation event is timestamped and stored with the attempt (client
  emits events; server logs them) so admin can review disputes.
- **Grace rules (design for false positives):** ignore blur events < 2s (OS notifications); the
  strike counter and rule are shown *before* the test starts; fullscreen requested (not enforced) at start.
- **Honest limitation (must be understood by the business):** this is **deterrence, not exam
  security** — client-side only, circumventable with a second device, and cannot *prevent* leaving,
  only detect it. Proportionate for a prediction test; do not market it as tamper-proof.
- Admin can **reinstate/reset** a flagged attempt (support path for false positives).

---

## 8. Scoring, certificate & delivery

- On final-assessment submit: instant score screen (per-section + total + predicted TOEFL band).
- **Certificate (PDF, MigraDoc):** name, program, date, per-section scores, predicted band, unique
  `verification_code`; **emailed automatically (SES)** and downloadable from the app.
- **Public verify page `/verify/{code}`** retained — it remains a trust/shareability asset even for a
  prediction cert.
- Certificates remain **immutable + retained indefinitely** (inherited invariants). Score-based
  issuance replaces completion-based; a retake (if allowed) issues a **new** certificate rather than
  mutating the old one.

---

## 9. Data model — delta from TSD §6

**Reused as-is:** users/auth tables, `watch_progress`, `certificates` (+ new columns),
`webhook_events`, `payment_transactions` (kind gains `program_purchase`), audit/contact/FAQ, org
tables (dormant).

**New / replaced:**
```
programs(id, name, slug, description, price_idr, status, created_at, ...)
program_batches(id, program_id FK, name, start_date, status)          -- if cohorts confirmed
program_sessions(id, program_id FK, order_index, type,                -- video|live|final_assessment
        title, module-ish video fields (provider_asset_id, duration) OR live fields
        (scheduled_at, mode, join_url/location), gating_test_id FK NULL)
enrollments(id, user_id FK, program_id FK, batch_id FK NULL,
        status,                       -- pending_payment|active|completed|revoked
        amount_paid_idr, xendit_ref, enrolled_at)
session_completions(id, user_id FK, session_id FK, completed_at, method)  -- watch+test | attended | submitted
live_attendance(id, session_id FK, user_id FK, attended bool, marked_by FK)
assessments(id, session_id FK NULL, kind,                              -- gating|final
        config JSONB: sections[], per-section time limits, pass_threshold, retake_cap, proctoring on/off)
questions(id, section, type, prompt, choices JSONB, correct JSONB,
        audio_ref NULL, passage_ref NULL, tags)
assessment_questions(assessment_id FK, question_id FK, order_index)
attempts(id, user_id FK, assessment_id FK, started_at, submitted_at,
        auto_submitted bool, proctor_flagged bool,
        answers JSONB, section_scores JSONB, total_score, passed bool)
proctor_events(id, attempt_id FK, kind, occurred_at, client_meta JSONB)
score_band_mappings(id, program_id FK, min_raw, max_raw, predicted_band)  -- admin-maintained
certificates: + program_id FK NULL, attempt_id FK NULL,
              + section_scores JSONB, predicted_band                    -- score-based issuance
```

**Dropped from active use (tables may remain, unused):** plans, subscriptions, subscription_events,
module tier/preview semantics, notification-preference machinery (v1 emails are transactional only).

**FK policy:** per the M0 decision — full FKs, no-navigation config, `Restrict` on mandatory user
refs, `SetNull` on optional.

---

## 10. Open questions (blocking marked ●)

1. ● **Session count/mix** — 9 videos or 6? Structure is configurable, but seed content needs the
   real syllabus. → **Resolved §12** (configurable + placeholder seed; real syllabus still needed).
2. ● **Cohorts/batches** — do enrollments belong to a dated batch (shared live-session schedule), or
   is enrollment continuous/self-paced with ad-hoc live sessions? → **Resolved §12** (optional batches).
3. **Live-session attendance** — is admin-marked attendance acceptable v1 (recommended), or is
   automated Zoom attendance expected? → *open, default = admin-marked.*
4. ● **Retake policy** — gating tests and, critically, the final assessment. → **Resolved §12**
   (gating unlimited/admin-cappable; final 1 attempt, retake issues a new cert).
5. **Written section** — confirm MCQ-only v1 (instant score preserved). Free-text/essay grading is a
   later phase. → *open, default = MCQ-only.*
6. ● **Score→TOEFL-band mapping** — business/pedagogy must supply the mapping table; engineering only
   builds the mechanism. **Without it, the headline feature (prediction) has no valid content.**
   → *OPEN — BLOCKING FOR LAUNCH, not for build. Mechanism ships with a clearly-marked placeholder map.*
7. **"TOEFL" naming/claims** — TOEFL is an ETS trademark; "TOEFL Prediction Test" phrasing and
   disclaimers should get a legal sanity-check. → *OPEN — needs the business's legal review before
   any public launch. Engineering ships explicit "prediction, not an official ETS score" disclaimers.*
8. **Refund policy** — one-time purchase changes the policy (cooling-off? pro-rata before session X?).
   Rewrite the policy page. → *open (M6).*
9. **Price** — final program price (and whether batches can differ). → *open; seed uses a placeholder.*
10. **AI Academy assets** — confirm the 55-module curriculum + subscription specs are archived (not
    deleted). → **Done** — `docs/archive/ai-academy/` + dormant (not dropped) tables/code.

---

## 11. Milestone re-slice (replaces TSD §15 M2–M7)

| M | Milestone | Contents |
|---|-----------|----------|
| **M0** | Foundation | **Unchanged — already delivered.** Schema delta (§9) added as a second migration. |
| **M1** | Auth | **Unchanged — inherited in full (delivered).** |
| **M2** | Program & enrollment | Program/session/batch model, landing page (SSG), Xendit one-time invoice, webhook→enrollment, receipt email, student program view with linear lock states. |
| **M3** | Video sessions + gating tests | Player (inherited) + watch threshold, question bank (admin CRUD), gating tests + instant score + unlock, attempts. |
| **M4** | Final assessment + proctoring + certificate | Multi-section timed engine, listening audio, soft proctoring (+ event log, admin reinstate), scoring + band mapping, cert PDF + email + `/verify/{code}`. **← sellable program complete.** |
| **M5** | Live sessions + admin polish | Live schedule + reminder email + attendance marking; admin dashboards (enrollments, attempts, flags); refund/manual-adjust action. |
| **M6** | Static/legal + onboarding tour | Rewritten for one-time purchase; driver.js tour; FAQ/contact. |

---

## 12. Decisions confirmed (2026-08-14)

Resolving the blocking items above, confirmed by the product owner before implementation:

| # | Decision | Rationale / consequence |
|---|---|---|
| **Approach** | **Specs first, then build.** | Archive AI Academy specs, write TSD-delta + revised CLAUDE.md golden rules + M2 spec, *then* build M0 schema delta → M2. Keeps a source-of-truth trail (matches this FSD's closing note). |
| **Q2 Cohorts** | **Optional batches.** `program_batches` exists; `enrollments.batch_id` is **nullable**. | A program can run as a dated cohort (shared live schedule) *or* accept continuous self-paced enrollment. Supports both without a later migration. |
| **Q4 Retakes** | **Final assessment: 1 attempt** by default; an admin-granted retake creates a **new** immutable certificate (never mutates the old). **Gating tests: unlimited**, admin-cappable per test. | Preserves prediction credibility while keeping paid students unblocked on gating tests. Honors the certificate-immutability invariant. |
| **Q1 Syllabus** | **Configurable + placeholder seed.** | Structure stays admin-configurable; a placeholder program (video+test sessions, 1 live, 1 final assessment) is seeded so the build is unblocked. **The real syllabus is still owed by the business.** |

**Still owed by the business (not engineering):** Q6 score→band mapping (blocking for launch),
Q7 TOEFL naming/legal review (blocking for public launch), Q3 attendance automation, Q5 written
section, Q8 refund policy copy, Q9 final price, Q1 real syllabus content.

---

*End of FSD v0.1. Blocking build items resolved in §12; this FSD now drives
`TSD_Delta_INVERTA_v0.1.md`, the revised `CLAUDE.md` golden rules, and the M2 spec.*
