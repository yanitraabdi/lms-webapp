# Landing Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fill the programme page, which is the homepage, with real content: how it works, the parts of each session in the syllabus, the ITP format, the FAQ, a closing CTA, and Course/FAQPage structured data. It uses the existing design system.

**Architecture:**
- **Backend:** the public programme DTO gains each session's parts as kind and title only, and saving parts revalidates the public page.
- **Frontend:** the page gains small server components under `components/program/`. The how-it-works steps and the FAQ accordion become shared modules.
- **ITP format numbers:** they live in a frontend constant that a backend test pins to `ToeflScoring`.

**Tech Stack:** .NET 10 minimal APIs, EF Core, xUnit with Testcontainers, Next.js App Router (SSG/ISR), Tailwind tokens, openapi-typescript client.

**Spec:** `docs/superpowers/specs/2026-10-06-landing-page-design.md`

## Global Constraints

- **Public data.** `PublicSessionDto` gains a trailing `IReadOnlyList<PublicSessionPartDto> Parts`, with `record PublicSessionPartDto(string Kind, string Title)`.
  - Parts are ordered by `OrderIndex`.
  - Video sessions get their parts; every other type gets an empty list.
  - The DTO carries no asset id, no assessment id and no test content (GR-3, GR-11).
  - Parts are loaded in one query per page, never one per session.
- **Revalidation.** After a successful `SessionPartAdminService.SaveAsync`, the service calls `IContentRevalidator.RevalidateAsync(["/", "/program/{slug}"])`. That call is best-effort, as in `ProgramAdminService`.
- **Page order:**
  1. Hero + price card (unchanged)
  2. Cara kerja
  3. Tentang program ini (unchanged)
  4. Silabus with parts
  5. Format tes ITP
  6. FAQ
  7. Closing CTA
  8. Prediction note (unchanged)
- **Part labels:** `LessonVideo` → `Video materi`, `Test` → `Tes`, `Discussion` → `Video pembahasan`. They are joined with ` · `. The final assessment row reads `140 soal · 115 menit`.
- **ITP format:**
  - Listening: 50 soal, 35 menit
  - Structure & Written Expression: 40 soal, 25 menit
  - Reading: 50 soal, 55 menit
  - Summary line: `Total 140 soal · 115 menit · skor prediksi 310–677`
  - The source is `frontend/lib/itpFormat.ts`, pinned by a backend test to `ToeflScoring`.
- **FAQ:**
  - the first 6 published items from `GET /api/faq`, by `OrderIndex`, fetched on the server with the page's revalidation (300);
  - a link `Lihat semua pertanyaan →` to `/help` only when there are more than 6;
  - the section is omitted when the fetch fails or returns nothing;
  - the same accordion component as `/help`, moved to `components/`, without the search box on the landing page.
- **Closing CTA:** heading `Siap mulai persiapan TOEFL Anda?`, the price `{formatIdr(price)} · sekali bayar`, and the same `EnrollCta` component.
- **How-it-works steps:** one module, `frontend/components/program/howItWorksSteps.ts`, used by both the landing page and `/how-it-works`. The copy is unchanged.
- **JSON-LD:**
  - `Course` with the name, description, `provider` = `{ "@type": "Organization", "name": "INVERTA" }`, and `offers` = `{ "@type": "Offer", "price": <number>, "priceCurrency": "IDR" }`;
  - `FAQPage` with the FAQ items shown;
  - emitted in `<script type="application/ld+json">` via `JSON.stringify`, with `<` escaped as `<`;
  - no claim of an official TOEFL score anywhere (GR-14).
- **Markup and layout:**
  - one `h1` (the programme name) and one `h2` per section;
  - the steps form an ordered list (`<ol>`);
  - visible focus on every interactive element;
  - works at 375px;
  - the page stays `revalidate = 300`.
- **Section rhythm.** The page is denser now, so new and existing sections alternate background to stay distinguishable. Sections 2 (Cara kerja), 5 (Format tes ITP) and 7 (Closing CTA) use `bg-surface` with `border-y border-border`; the others stay on the page background. FAQ answers render with `whitespace-pre-line` so line breaks stored in the database show.
- **Commits** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Backend checks:** `dotnet build backend/Academy.slnx` with 0 warnings and `dotnet test backend/Academy.slnx` green.
- **Frontend checks:** `cd frontend && npm run lint && npm run build` green.
- **Never** read, print or edit `.env`.

## File map

| File | Task | Responsibility |
|---|---|---|
| `backend/src/Application/Programs/ProgramContracts.cs` | 1 | `PublicSessionPartDto`, `PublicSessionDto.Parts` |
| `backend/src/Infrastructure/Programs/ProgramService.cs` | 1 | Load parts for the public page |
| `backend/src/Infrastructure/Programs/SessionPartAdminService.cs` | 1 | Revalidate after save |
| `frontend/lib/itpFormat.ts` (new) | 1 | ITP format constant |
| `backend/tests/Integration.Tests/LandingContentTests.cs` (new) | 1 | Tests |
| `frontend/components/program/howItWorksSteps.ts`, `HowItWorks.tsx`, `ItpFormat.tsx`, `SyllabusParts.tsx` (new); `app/(marketing)/how-it-works/page.tsx`; `app/(marketing)/program/[slug]/page.tsx`; `api-client/schema.ts` | 2 | Steps, syllabus parts, ITP format |
| `frontend/components/FaqAccordion.tsx` (moved), `components/program/ProgramFaq.tsx`, `ClosingCta.tsx`, `ProgramJsonLd.tsx` (new); `app/(marketing)/help/*`; the programme page | 3 | FAQ, closing CTA, structured data |

---

### Task 1: Public parts, revalidation, ITP constant

**Files:**
- Modify: `backend/src/Application/Programs/ProgramContracts.cs`
- Modify: `backend/src/Infrastructure/Programs/ProgramService.cs` (`GetPublicAsync`)
- Modify: `backend/src/Infrastructure/Programs/SessionPartAdminService.cs` (constructor + `SaveAsync`)
- Create: `frontend/lib/itpFormat.ts`
- Test: `backend/tests/Integration.Tests/LandingContentTests.cs`

**Interfaces:**
- Produces:
  - `record PublicSessionPartDto(string Kind, string Title)`;
  - `PublicSessionDto(..., string? LiveMode, IReadOnlyList<PublicSessionPartDto> Parts)`;
  - the `ITP_FORMAT` export in `frontend/lib/itpFormat.ts`.

- [ ] **Step 1: The frontend constant** (no logic, so it's written first so the pinning test has something to read)

`frontend/lib/itpFormat.ts`:

```ts
/**
 * The TOEFL ITP format shown on the programme page. Mirrors backend/src/Domain/ToeflScoring.cs —
 * the backend test ItpFormatConstantsTests reads THIS file and fails if the numbers drift.
 * Keep the shape (one `questions:` and `minutes:` per section, `totalMin:`, `totalMax:`) parseable.
 */
export const ITP_FORMAT = {
  listening: { label: "Listening", questions: 50, minutes: 35 },
  structure: { label: "Structure & Written Expression", questions: 40, minutes: 25 },
  reading: { label: "Reading", questions: 50, minutes: 55 },
  totalMin: 310,
  totalMax: 677,
} as const;

export const ITP_TOTAL_QUESTIONS =
  ITP_FORMAT.listening.questions + ITP_FORMAT.structure.questions + ITP_FORMAT.reading.questions;
export const ITP_TOTAL_MINUTES =
  ITP_FORMAT.listening.minutes + ITP_FORMAT.structure.minutes + ITP_FORMAT.reading.minutes;
```

- [ ] **Step 2: Write the failing tests**

Create `LandingContentTests.cs` using `AuthApiFactory`. Copy the admin, program and parts helpers from `StudentResultsTests.cs` / `SessionPartAdminTests.cs`. The program must be **published** for the public endpoint to return it, so reuse whatever helper those tests use to publish, which includes satisfying readiness.

1. `Public_programme_lists_parts_in_order_for_video_sessions`:
   - Session 1 (Video) has parts `[Lesson "Pengantar", Test "Kuis 1", Discussion "Pembahasan 1"]`, and there is a Live session.
   - GET the public programme endpoint (find its route in `backend/src/Api/Endpoints/ProgramEndpoints.cs`, e.g. `/api/programs/{slug}`).
   - Session 1's `parts` are exactly `[{kind:"LessonVideo",title:"Pengantar"},{kind:"Test",title:"Kuis 1"},{kind:"Discussion",title:"Pembahasan 1"}]`, and the Live session's `parts` is `[]`.
2. `Public_programme_never_exposes_asset_or_assessment_ids`: the raw JSON doesn't contain `providerAssetId` or `assessmentId` (case-insensitive), and doesn't contain the Bunny asset string used for the parts.
3. `Saving_parts_revalidates_the_public_pages` (service-level):
   - Build `SessionPartAdminService` directly with the factory's `AppDbContext` (scoped), `VideoOptions` from the container, and a fake `IContentRevalidator` that records its paths.
   - Save a valid parts list for a video session.
   - The fake received `"/"` and `"/program/{slug}"`.
4. `ItpFormatConstantsTests` (a separate class in the same file, no fixture):
   - Locate the repo root by walking up from `AppContext.BaseDirectory` until a directory containing `frontend/lib/itpFormat.ts` is found.
   - Read the file. For each of `listening`, `structure` and `reading`, regex out `questions:\s*(\d+)` and `minutes:\s*(\d+)` within that section's object, plus `totalMin:\s*(\d+)` and `totalMax:\s*(\d+)`.
   - Assert they equal `ToeflScoring.ListeningQuestions`/`ListeningMinutes`, `StructureQuestions`/`StructureMinutes`, `ReadingQuestions`/`ReadingMinutes`, `TotalMin` and `TotalMax`.

Run: `dotnet test backend/tests/Integration.Tests --filter "LandingContentTests|ItpFormatConstantsTests"`. Expected: compile failure, or failures on 1–3.

- [ ] **Step 3: Implement**

`ProgramContracts.cs`:

```csharp
/// <summary>One step inside a public video session: its kind and title only — never the asset or
/// the test behind it (GR-3, GR-11).</summary>
public record PublicSessionPartDto(string Kind, string Title);

public record PublicSessionDto(
    Guid Id, int OrderIndex, string Type, string Title, string? Description,
    int? DurationSeconds, DateTimeOffset? ScheduledAt, string? LiveMode,
    IReadOnlyList<PublicSessionPartDto> Parts);
```

`ProgramService.GetPublicAsync`: after loading `sessions` (project to an anonymous type first, then map), load every part of the program's video sessions in **one** query:

```csharp
        var sessionIds = rows.Select(s => s.Id).ToList();
        var parts = (await db.SessionParts
                .Where(pt => sessionIds.Contains(pt.SessionId))
                .OrderBy(pt => pt.OrderIndex)
                .Select(pt => new { pt.SessionId, pt.Kind, pt.Title })
                .ToListAsync(ct))
            .GroupBy(pt => pt.SessionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PublicSessionPartDto>)
                g.Select(pt => new PublicSessionPartDto(pt.Kind.ToString(), pt.Title)).ToList());
```

Build each `PublicSessionDto` with `Type == Video ? parts.GetValueOrDefault(id, []) : []`. Keep the existing summary comment, extended to say parts are kind and title only. Fix every other `new PublicSessionDto(` (`grep -rn "new PublicSessionDto" backend`).

`SessionPartAdminService`: add `IContentRevalidator revalidator` to the primary constructor. After the successful `SaveChangesAsync` in `SaveAsync`:

```csharp
        // The public syllabus shows each session's parts (landing spec §3) — refresh it now rather
        // than up to 5 minutes later. Best-effort, as in ProgramAdminService.
        var slug = await db.ProgramSessions.Where(s => s.Id == sessionId)
            .Select(s => s.Program.Slug).FirstOrDefaultAsync(ct);
        if (slug is not null) await revalidator.RevalidateAsync(["/", $"/program/{slug}"], ct);
```

Match how `ProgramAdminService.RevalidateAsync` handles failures. If it doesn't catch, the real `NextContentRevalidator` is already best-effort, so don't add a catch.

- [ ] **Step 4: Run and commit**

Run: the tests above, then the 0-warning build and the full suite. Expected: all pass.

```bash
git add backend frontend/lib/itpFormat.ts
git commit -m "feat: public syllabus carries each session's parts; parts save refreshes the landing page

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: How it works, syllabus parts, ITP format

**Files:**
- Modify: `frontend/api-client/schema.ts` (regenerated)
- Create: `frontend/components/program/howItWorksSteps.ts`, `HowItWorks.tsx`, `SyllabusParts.tsx`, `ItpFormat.tsx`
- Modify: `frontend/app/(marketing)/how-it-works/page.tsx` (import the shared steps)
- Modify: `frontend/app/(marketing)/program/[slug]/page.tsx`

**Interfaces:**
- Consumes: Task 1's `PublicSession.parts` and `ITP_FORMAT`.
- Produces: `HOW_IT_WORKS_STEPS`, and the components `<HowItWorks />`, `<SyllabusParts session />` and `<ItpFormat />`.

- [ ] **Step 1: Regenerate the client**

```bash
dotnet build backend/Academy.slnx
cd backend/src/Api
ASPNETCORE_ENVIRONMENT=Development RunMigrations=false SeedSampleData=false \
  dotnet run --no-build --urls http://localhost:8091 > /tmp/api-openapi.log 2>&1 &
until curl -sf http://localhost:8091/openapi/v1.json -o /tmp/openapi.json; do sleep 2; done
pkill -f "urls http://localhost:8091"
cd ../../../frontend
npx openapi-typescript /tmp/openapi.json -o api-client/schema.ts
grep -c "PublicSessionPartDto" api-client/schema.ts
```

Expected: the count is at least 1.

- [ ] **Step 2: Shared steps**

`components/program/howItWorksSteps.ts` exports `HOW_IT_WORKS_STEPS`, an array identical to the current `steps` in `app/(marketing)/how-it-works/page.tsx` (same `n`, `title` and `body` text). Replace that page's local `steps` with the import. Its rendering is unchanged.

- [ ] **Step 3: Components**

`HowItWorks.tsx` is a server component:
- a `<section>` with `<h2>` `Cara kerja`;
- an `<ol className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">` of the four steps;
- each step is an `<li>` with the number in the same `bg-primary-soft text-primary` circle style as `/how-it-works`, the title, and the body (`text-[13.5px] text-ink-muted`).

`SyllabusParts.tsx` (server):

```tsx
const PART_LABEL: Record<string, string> = { LessonVideo: "Video materi", Test: "Tes", Discussion: "Video pembahasan" };

/** The parts of a session as a single muted line; renders nothing when there are none. */
export function SyllabusParts({ session }: { session: PublicSession }) {
  if (session.type === "FinalAssessment")
    return <p className="mt-1 text-[12.5px] text-ink-subtle">{ITP_TOTAL_QUESTIONS} soal · {ITP_TOTAL_MINUTES} menit</p>;
  const parts = session.parts ?? [];
  if (parts.length === 0) return null;
  return <p className="mt-1 text-[12.5px] text-ink-subtle">{parts.map((p) => PART_LABEL[p.kind] ?? p.kind).join(" · ")}</p>;
}
```

`ItpFormat.tsx` (server):
- a `<section>` with `<h2>` `Format tes ITP`;
- a short intro: `Tes akhir program ini mengikuti format TOEFL ITP.`;
- three cards in `grid gap-4 sm:grid-cols-3` from `ITP_FORMAT`, each showing the label, `{questions} soal` and `{minutes} menit`;
- the summary line `Total {ITP_TOTAL_QUESTIONS} soal · {ITP_TOTAL_MINUTES} menit · skor prediksi {totalMin}–{totalMax}`.

- [ ] **Step 4: Page**

In `app/(marketing)/program/[slug]/page.tsx`:
- render `<HowItWorks />` as a bordered section between the hero and "Tentang program ini";
- inside `SyllabusRow`, render `<SyllabusParts session={session} />` under the title, keeping the existing live schedule line;
- render `<ItpFormat />` after the syllabus.

Use the same `px-6 py-12` and `mx-auto max-w-3xl` (or `max-w-5xl` for grids) section rhythm as the existing sections.

- [ ] **Step 5: Lint, build, commit**

Run: `cd frontend && npm run lint && npm run build`. Expected: both pass, and `/program/[slug]` is still ISR (revalidate 300) in the build output.

```bash
git add frontend
git commit -m "feat: landing page shows how it works, each session's parts and the ITP format

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: FAQ, closing CTA, structured data

**Files:**
- Move: `frontend/app/(marketing)/help/FaqAccordion.tsx` → `frontend/components/FaqAccordion.tsx` (update `/help`'s import)
- Create: `frontend/components/program/ProgramFaq.tsx`, `ClosingCta.tsx`, `ProgramJsonLd.tsx`
- Modify: `frontend/app/(marketing)/program/[slug]/page.tsx`

**Interfaces:**
- Consumes: `getFaq({ revalidate })` and `FaqItem` from `lib/content.ts`; `EnrollCta`; `formatIdr`.
- Produces: `FaqAccordion({ items, searchable = true })`.

- [ ] **Step 1: Accordion**

Move the file. Add a `searchable?: boolean` prop (default `true`). When it is false, don't render the search input and use `items` directly. `/help` keeps calling it without the prop, so its behaviour is unchanged.

- [ ] **Step 2: Components**

`ProgramFaq.tsx` (server) takes `{ items: FaqItem[] }`:
- returns `null` when `items` is empty;
- shows the first 6 items, sorted by `orderIndex` with `num()`;
- renders a `<section>` with `<h2>` `Pertanyaan yang sering diajukan` and `<FaqAccordion items={top6} searchable={false} />`;
- when `items.length > 6`, adds a `<Link href="/help">` `Lihat semua pertanyaan →`.

`ClosingCta.tsx` (server) takes `{ programId, priceIdr }`. It renders a `<section>` with a centred card holding:
- the `<h2>` `Siap mulai persiapan TOEFL Anda?`;
- the line `{formatIdr(priceIdr)} · sekali bayar`;
- `<EnrollCta programId={programId} className="mt-4" />`.

`ProgramJsonLd.tsx` (server) takes `{ program, faq }`:

```tsx
export function ProgramJsonLd({ program, faq }: { program: PublicProgram; faq: FaqItem[] }) {
  const data: object[] = [{
    "@context": "https://schema.org", "@type": "Course",
    name: program.name, description: program.summary ?? program.description,
    provider: { "@type": "Organization", name: "INVERTA" },
    offers: { "@type": "Offer", price: num(program.priceIdr), priceCurrency: "IDR" },
  }];
  if (faq.length > 0) data.push({
    "@context": "https://schema.org", "@type": "FAQPage",
    mainEntity: faq.map((f) => ({ "@type": "Question", name: f.question,
      acceptedAnswer: { "@type": "Answer", text: f.answer } })),
  });
  // `<` escaped so a "</script>" inside content cannot close the tag early.
  const json = JSON.stringify(data).replace(/</g, "\\u003c");
  return <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: json }} />;
}
```

- [ ] **Step 3: Page**

In the programme page:
- fetch the FAQ: `const faq = await getFaq({ revalidate: 300 }).catch(() => []);`;
- compute `faqShown`, the first 6 by `orderIndex`;
- render `<ProgramFaq items={faq} />` after `<ItpFormat />`;
- render `<ClosingCta programId={program.id} priceIdr={program.priceIdr} />` before the prediction note;
- render `<ProgramJsonLd program={program} faq={faqShown} />` once, inside `<main>`.

Check that the page still has exactly one `<h1>`.

- [ ] **Step 4: Lint, build, commit**

Run: `cd frontend && npm run lint && npm run build`. Expected: both pass, and `/program/[slug]` and `/help` both build.

```bash
git add frontend
git commit -m "feat: landing page FAQ, closing call to action and Course/FAQ structured data

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: Browser check (controller)**

Rebuild the stack with `docker compose up -d --build api frontend`. At `http://localhost:6300/program/toefl-preparation`, at 375px and at desktop width, check:
1. the section order;
2. the syllabus labels;
3. the ITP numbers;
4. that the FAQ opens and closes from the keyboard;
5. that the closing CTA works;
6. that the JSON-LD parses, by checking `document.querySelectorAll('script[type="application/ld+json"]')` in the console.

---

## Self-review notes

- **Spec §5's revalidation test** is service-level with a fake `IContentRevalidator`. The integration factory has no revalidator seam, so this is the smallest test that proves the call.
- **The FAQ list is filtered once.** `ProgramFaq` receives every item and slices to 6 itself, while the JSON-LD receives the same first 6 (`faqShown`) the page computes. Both use `orderIndex`, so the structured data matches what's visible.

## Deferred minors (after final review)

Fixed in the final wave (a2a7f5c): ISR (`generateStaticParams`), single FAQ-limit source, stray `;`, accordion `aria-controls`, answers always in HTML, Offer `category`, test scope disposal.

Still open (non-blocking):
- Tests: final session's empty `parts` not asserted; `totalMin`/`totalMax` regex in `ItpFormatConstantsTests` unscoped; no negative test that revalidation is skipped on a failed parts save; seeding helpers copied rather than shared.
- UI polish: HowItWorks `border-b` vs `border-y`; ITP cards and ClosingCta card `bg-surface` on a `bg-surface` section (low contrast); stray blank lines in `/how-it-works`; `/how-it-works` jumps h1→h3 (pre-existing).
