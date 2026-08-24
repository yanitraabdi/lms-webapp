# ARCHIVED — AI Productivity Academy (paused product specs)

> **Status: PAUSED as product specs, retained as platform reference.** Archived on 2026-08-14 when
> the product pivoted to **INVERTA — TOEFL Preparation Program**
> (see [`docs/FSD_INVERTA_TOEFL_v0.1.md`](../../FSD_INVERTA_TOEFL_v0.1.md)).
>
> **Archived, not deleted** — deliberately retained for a possible later revival of the AI-skills
> product on the same platform (FSD §10 Q10).

## What's in here

| File | What it was |
|---|---|
| `PRD_LMS_AI_Curriculum_Detailed_v2.5.md` | Product spec: 55-module AI curriculum, tiered cumulative subscriptions, completion certificates. |
| `TSD_LMS_AI_Curriculum_v0.4.md` | Technical spec: stack, schema, auth, payments, deployment topology. |
| `M0_Foundation_Spec.md` | The M0 foundation package spec (delivered). |

## What still applies

These documents are **superseded for product behaviour** but remain the **platform foundation**.
Still authoritative (and inherited unchanged by INVERTA):

- **TSD §3** stack & rendering matrix · **§7** auth in full · **§13** deployment topology ·
  **§14** testing approach.
- Provider abstractions: `IVideoProvider` (Bunny), `IPaymentGateway` (Xendit), `IEmailSender` (SES),
  `IObjectStorage` (R2), certificate PDF via PDFsharp/MigraDoc.
- Database conventions: snake_case, uuid v7 app-assigned PKs, timestamptz, enums-as-text,
  `decimal(18,2)` money, jsonb bags, full FKs (see `docs/DECISIONS.md`).

## What is superseded

Subscription tiers, cumulative unlock, recurring billing/proration/grandfathering/dunning, the
free-preview browsing model, catalog-as-primary-navigation, completion-based certificates, and the
55-module curriculum itself. See FSD §2 for the full inherited/dropped/changed breakdown.

**Code note:** the corresponding tables (`plans`, `subscriptions`, `subscription_events`, module
tier/preview semantics, notification preferences) and their services remain in the codebase but are
**dormant** — not dropped — for the same revival reason.
