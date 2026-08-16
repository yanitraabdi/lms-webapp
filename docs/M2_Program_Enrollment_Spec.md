# M2 — Program & Enrollment (INVERTA)

Implements FSD §3 (program structure), §4 (enrollment & payment), §5 (session shells) and the
TSD-delta §1–§5. **Goal: a student can discover the program, pay once, and see the ordered syllabus
with correct lock states.** Video playback, gating tests and the final assessment land in M3/M4.

---

## 1. Scope

**In:**
- Program / batch / session model + admin CRUD (minimal — enough to author a program).
- Public program landing page (SSG/ISR) with syllabus, schedule, price.
- One-time Xendit invoice checkout; **webhook → enrollment**; receipt email.
- Student program view: ordered sessions with lock states, driven by `ISessionAccessService`.
- The `InvertaProgramsAndAssessments` migration (schema delta, additive).
- Placeholder program seed.

**Out (later milestones):** playback + progress (M3), gating tests/question bank (M3), final
assessment + proctoring + certificate (M4), live reminders + attendance UI + admin dashboards (M5),
rewritten legal/static (M6).

**Stub-but-visible in M2:** a session's "Buka" action is present but disabled with a "segera hadir"
state for video/assessment types until M3/M4 land — the lock logic itself is real.

---

## 2. Data (migration `InvertaProgramsAndAssessments`)

Per TSD-delta §2/§3/§9 — additive only, no DROPs:

- **New:** `programs`, `program_batches`, `program_sessions`, `enrollments`,
  `session_completions`, `live_attendance`, `assessments`, `questions`,
  `assessment_questions`, `attempts`, `proctor_events`, `score_band_mappings`.
  *(The assessment tables are created now, in one migration, even though the engine ships in M3/M4 —
  one schema change is cleaner than three.)*
- **Altered:** `watch_progress` (`module_id` nullable + `session_id` + CHECK),
  `certificates` (`level_id` nullable + `program_id`/`attempt_id`/`section_scores`/`predicted_band`),
  `payment_transactions.kind` gains `ProgramPurchase`.

**Acceptance:** `dotnet ef migrations has-pending-model-changes` reports none; the migration applies
to both a fresh database *and* the existing dev volume; no archived-product table is dropped.

---

## 3. Backend

### 3.1 Domain
- `SessionAccess.CanAccess(enrolled, session, previousCompleted)` — pure, unit-tested.
- `LinearLock` helper: first session always unlocked; otherwise requires previous completion.
- `EnrollmentStatus` state machine: `PendingPayment → Active → Completed`, `→ Revoked` from any.
  Only `Active`/`Completed` grant access.

### 3.2 Application
| Contract | Notes |
|---|---|
| `IProgramService` | `GetPublicAsync(slug)` (landing/SSG), `GetForStudentAsync(userId, programId)` → sessions + per-session `locked/available/completed` + progress summary. |
| `IEnrollmentService` | `CheckoutAsync(userId, programId, batchId?)` → invoice URL (**never grants**); `IsEnrolledAsync`; `ListMineAsync`. |
| `ISessionAccessService` | **The gate.** `CanAccessAsync(userId, sessionId)`; throws `403` problem-details otherwise. Every M3/M4 resource endpoint will call this. |
| `ISessionCompletionService` | Created in M2 with the `live`/`final` paths stubbed; M3 wires the video+test path. Idempotent, non-retroactive. |
| `IProgramAdminService` | CRUD programs/batches/sessions, reorder, publish. |

DTOs carry **no** answer keys and no provider asset ids to students.

### 3.3 Infrastructure
- `EnrollmentService` — creates a `PendingPayment` enrollment + `payment_transactions`
  (`kind=ProgramPurchase`), then a **single** Xendit invoice via `IPaymentGateway` (dev-sim locally).
- `PaymentWebhookProcessor` — **extended, not replaced**: on a verified, idempotent `paid` event for
  a `ProgramPurchase`, flip the enrollment to `Active`, stamp `enrolled_at`/`amount_paid_idr`, and
  send the receipt email. Subscription handling stays for the archived product but is unreachable.
- `ProgramSeeder` — idempotent placeholder program (FSD §12): published program, a few
  video sessions, one live session, one final-assessment session, plus a **clearly-marked
  placeholder** `score_band_mappings` set.
- `IEmailSender.SendEnrollmentReceiptAsync`.

### 3.4 Endpoints
```
GET  /api/programs/{slug}                 public (landing)
POST /api/programs/{id}/enroll            auth + EmailVerified → invoice URL
GET  /api/me/enrollments                  auth
GET  /api/me/programs/{id}                auth + enrolled → sessions with lock states
POST /api/webhooks/xendit                 unchanged plumbing; now creates enrollments
CRUD /api/admin/programs|batches|sessions  Admin policy
```
Rate-limit `enroll` under the existing `payment` policy.

---

## 4. Frontend

| Route | Rendering | Content |
|---|---|---|
| `/` | SSG | Program-led landing (replaces the tiered-pricing home). Hero, outcomes, syllabus preview, price, "Daftar" CTA. |
| `/program/{slug}` | SSG/ISR | Full syllabus, schedule/batch, price, FAQ → CTA. Revalidated on admin publish (existing `/api/revalidate`). |
| `/checkout/success` | CSR | Informational only — states that access appears once payment is confirmed. **Never grants.** |
| `/app/program/{id}` | CSR | Ordered session list with lock states (locked = visible, not actionable), progress header, next-step CTA. |
| `/admin/programs` | CSR | Program/session CRUD, reorder, publish. |

Navigation drops catalog/pricing links (routes stay in-repo, dormant). `lib/programs.ts` is the new
typed client over the regenerated OpenAPI schema.

---

## 5. Acceptance criteria

1. An anonymous visitor sees the program landing (SSG) with syllabus and price; no session is playable.
2. An unverified user cannot check out (inherited email-verify-before-purchase rule).
3. `POST /enroll` returns an invoice URL and creates a `PendingPayment` enrollment that grants **nothing**.
4. Visiting `/checkout/success` without a webhook grants nothing (`/api/me/programs/{id}` → 403).
5. After the verified webhook, the enrollment is `Active` and the program view returns sessions.
6. Session 1 is `available`; sessions 2..N are `locked` and their resource endpoints return 403.
7. Replaying the same webhook is idempotent — exactly one enrollment, one receipt.
8. Revoking an enrollment blocks access but deletes no `watch_progress`/`attempts`/`certificates`.
9. Admin can create/reorder/publish sessions; publishing revalidates the landing page.

## 6. Tests (integration unless noted)
- Domain unit: linear-lock truth table; enrollment state machine.
- Checkout → unpaid → blocked; webhook → active → unlocked (the M2 backbone).
- Webhook idempotency for `ProgramPurchase` (replay → one enrollment, one receipt email).
- Lock states: k+1 forbidden until k completes (uses a stubbed completion in M2).
- Email-verify-before-purchase still enforced.
- Retention on revoke.

---

*Depends on: TSD-delta §1–§5, §9. Followed by M3 (video sessions + gating tests).*
