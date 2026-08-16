# M0 — Foundation (Milestone Spec)

**Goal:** stand up the skeleton both stacks build on — solution structure, data layer, local + CI plumbing — with **zero product features**. When M0 is done, M1 (Auth) can start without touching infrastructure.

**Source of truth:** TSD v0.4 (esp. §3, §5.1, §6, §13) and PRD v2.5. CLAUDE.md holds the conventions. This spec says *what to scaffold*, not how to design features.

---

## In scope
Repos & structure · .NET 10 solution (4 layers + tests) · Next.js App Router skeleton · PostgreSQL + EF Core wired with the **full schema + InitialCreate migration** · health checks · OpenAPI emission + generated frontend client · CI · local `docker compose` · config/secrets scaffolding.

## Explicitly out of scope (do NOT build in M0)
Any auth logic, any business endpoint beyond `/health`, any UI screen beyond an app shell, any provider integration (Bunny/Xendit/SES/R2) beyond empty interface stubs, any entitlement/payment/playback code. Those belong to M1+.

---

## Tasks (ordered)

### 1. Monorepo
- Create the structure in CLAUDE.md (`/backend`, `/frontend`, `/docs`, `/.github/workflows`, root `docker-compose.yml`, `README.md`, `CLAUDE.md`).
- Put PRD v2.5, TSD v0.4, and the milestone specs in `/docs`.
- `.gitignore`, `.editorconfig`, license.

### 2. .NET 10 solution (4 layers + tests)
- Projects: `Api`, `Application`, `Domain`, `Infrastructure` under `/backend/src`; test projects under `/backend/tests` (`Domain.Tests`, `Application.Tests`, `Api.Tests`, `Integration.Tests`).
- Enforce layer references: Domain → (none); Application → Domain; Infrastructure → Application/Domain; Api → Application/Infrastructure.
- Solution-wide: `Nullable=enable`, `TreatWarningsAsErrors=true`, central package management (`Directory.Packages.props`).
- `Api` boots with a minimal `Program.cs`: problem-details, OpenAPI, health checks, rate-limiter registered (no policies wired to features yet), CORS for the frontend origin.

### 3. Data layer (the schema in this package)
- Add the provided files: `Domain/Common/Entity.cs`, `Domain/Enums.cs`, `Domain/Entities/*.cs`, `Infrastructure/Persistence/AppDbContext.cs`, `Infrastructure/Persistence/ModelConfiguration.cs`. (Split grouped entity files into one-class-per-file if preferred.)
- Packages (confirm latest stable compatible with .NET 10 — see Risks): `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, `EFCore.NamingConventions`.
- DI: register `AppDbContext` with `UseNpgsql(...).UseSnakeCaseNamingConvention()`.
- **Generate the migration with the tool (do not hand-write it):**
  ```
  dotnet ef migrations add InitialCreate \
    --project backend/src/Infrastructure --startup-project backend/src/Api
  dotnet ef database update \
    --project backend/src/Infrastructure --startup-project backend/src/Api
  ```
- Sanity-check the SQL: `dotnet ef migrations script --idempotent`. Verify it creates every table with the expected unique indexes (email, slug, verification_code, webhook external_id, watch_progress (user,module), module_feedback (user,module), external login (provider,key)), jsonb columns, enum-as-text, and decimal(18,2) money. Confirm the `User` soft-delete query filter is applied.

### 4. Frontend skeleton (Next.js App Router)
- `npx create-next-app` (TypeScript, App Router, Tailwind, ESLint).
- Route groups reflecting TSD §4.2: `(marketing)` and `(catalog)` as SSG/ISR placeholders; `app/` (authenticated) and `admin/` as client-rendered placeholders; `verify/[code]` as an SSR placeholder. Each is a stub — no real content.
- `next-intl` configured with `id` as default locale; a `/messages/id.json` with a few keys to prove wiring. Light-mode-only theme tokens.
- Add driver.js as a dependency (no tour yet).
- `/api-client` folder with the generation script wired (task 5).

### 5. OpenAPI → typed client
- `Api` emits an OpenAPI document at build/dev.
- Add an `npm run generate:api` script (e.g. `openapi-typescript` or `orval`) that reads the OpenAPI doc and writes the typed client into `/frontend/api-client`. Prove it round-trips against the `/health` contract.

### 6. Local dev — docker compose
- `docker-compose.yml` runs: `postgres`, `api`, `frontend`. API waits for Postgres; migration applied on startup (or a one-shot migrate service).
- `.env.example` with all required config keys (DB connection, JWT signing key placeholder, provider keys as empty placeholders) — **no real secrets committed**.
- Document `docker compose up` in the README (ties to TSD §13.1 Phase A).

### 7. CI
- `.github/workflows/ci.yml`: restore → build (warnings-as-errors) → `dotnet test` → `dotnet ef migrations script` validation (or apply to an ephemeral Postgres service) → frontend `npm ci && npm run build` → `npm run generate:api` drift check (fail if generated client is stale).

---

## Acceptance criteria (M0 Definition of Done)
1. `dotnet build backend` succeeds with **zero warnings**.
2. `dotnet test backend` runs green (placeholder tests are fine; at least one per test project).
3. `InitialCreate` applies cleanly to a **fresh PostgreSQL**; every entity in this package becomes a table with the unique indexes, jsonb columns, enum-as-text, and money precision listed in task 3.
4. `GET /health` returns 200 with the API booted (problem-details + OpenAPI + rate-limiter middleware present).
5. OpenAPI doc generates and `npm run generate:api` produces a typed client with no drift.
6. `npm run build` (frontend) succeeds; the five route-group placeholders render; `id` locale loads.
7. `docker compose up` brings up postgres + api + frontend; the app reaches the API through the configured origin.
8. CI is green on a clean checkout.

---

## Risks / verification notes
- **Package versions:** confirm Npgsql EF provider, EFCore.NamingConventions, and the OpenAPI client generator have stable releases targeting **.NET 10** before pinning. If a dependency lags, note it and pin the newest compatible. *(Needs verification at build time.)*
- **uuid v7 + ValueGeneratedNever:** the context sets PKs app-side via `Guid.CreateVersion7()`. Verify the InitialCreate doesn't add a DB default for `id`.
- **Soft-delete query filter** on `User` affects navigations from related entities — confirm intended behavior in M1 when auth queries arrive (EF will warn about required-relationship filters; address then).
- **`DateTimeOffset` ↔ `timestamptz`:** confirm Npgsql maps as expected for your version; standardize on UTC.
- This milestone intentionally creates tables with no consuming code yet (full-schema baseline — a deliberate decision; the alternative is per-milestone migrations). Empty tables are harmless; flag if you'd rather scope InitialCreate to identity-only.

---

## Package contents (this handoff)
`CLAUDE.md` · `M0_Foundation_Spec.md` · `schema/Domain/Enums.cs` · `schema/Domain/Entities/{Identity,Catalog,Billing,Learning,Engagement}.cs` · `schema/Infrastructure/Persistence/{AppDbContext,ModelConfiguration}.cs`. Place the schema files into the solution per task 3; generate the migration there.
