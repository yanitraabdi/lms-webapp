# Build decisions (this repository)

Decisions made while scaffolding, beyond what the PRD/TSD already lock. The PRD/TSD remain the
source of truth; this records repo-level choices and their rationale.

## Confirmed by the product owner
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
