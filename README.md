# AI Productivity Academy — LMS

Subscription video LMS teaching AI skills to Indonesian professionals (Bahasa Indonesia).
**.NET 10 API · Next.js (App Router) · PostgreSQL · Bunny Stream · Xendit.**

The **source of truth** is in [`/docs`](docs): the PRD (`PRD_LMS_AI_Curriculum_Detailed_v2.5.md`),
the TSD (`TSD_LMS_AI_Curriculum_v0.4.md`), and the per-milestone specs. Project rules for
contributors and AI agents live in [`CLAUDE.md`](CLAUDE.md). Build decisions for this repo are
in [`docs/DECISIONS.md`](docs/DECISIONS.md). The exported design system + screen prototypes are
in [`docs/design-handoff`](docs/design-handoff) (reference for per-milestone frontend work).

> Status: **M0 — Foundation.** Skeleton + data layer only; no product features yet (see the
> milestone plan in TSD §15).

## Layout

```
backend/          .NET 10 solution (Api · Application · Domain · Infrastructure + tests)
frontend/         Next.js App Router app (Tailwind, TanStack Query, next-intl, design tokens)
docs/             PRD, TSD, milestone specs, design handoff
docker-compose.yml  local full stack (postgres + api + frontend)
```

## Quick start

### Full stack (Docker)
```bash
cp .env.example .env          # adjust if needed; no real secrets required for local
docker compose up --build     # postgres + api (migrates on startup) + frontend
# API     → http://localhost:8080  (health: /health, OpenAPI: /openapi/v1.json)
# Web     → http://localhost:3000
```

### Backend (local)
```bash
dotnet build backend/Academy.slnx
dotnet test  backend/Academy.slnx
dotnet run --project backend/src/Api          # needs a local Postgres (see .env)

# migrations
dotnet ef migrations add <Name> --project backend/src/Infrastructure --startup-project backend/src/Api
dotnet ef database update      --project backend/src/Infrastructure --startup-project backend/src/Api
dotnet ef migrations script --idempotent      # review SQL
```

### Frontend (local)
```bash
cd frontend
npm install
npm run dev                    # http://localhost:3000
npm run generate:api           # regenerate typed client from the API OpenAPI doc
```

## Conventions
- PostgreSQL `snake_case`, `uuid` v7 PKs (app-assigned), `timestamptz`, enums stored as text,
  money `decimal(18,2)` (IDR), JSON bags as `jsonb`.
- Backend clean architecture (Domain → Application → Infrastructure/Api). Nullable + warnings-as-errors.
- Frontend: light-mode only, WCAG 2.1 AA, Bahasa Indonesia first, generated API client only,
  no business logic / secrets in Next.
- See [`CLAUDE.md`](CLAUDE.md) for the golden-rule invariants (entitlement server-side only,
  webhook-only entitlement changes, signed playback, certificate immutability, etc.).
