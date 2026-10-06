# Landing page (programme page) — design

**Date:** 2026-10-06 · **Status:** approved in brainstorming, awaiting spec review
**Sub-project 4 of 6** in the learner-dashboard spec §8 ("landing page density"). `/` redirects to
`/program/toefl-preparation`, so this page *is* the homepage.

## 1. Why and the decision

The page today has:
- a hero;
- a sticky price card with 5 "included" points;
- "Tentang program ini";
- a syllabus that lists session titles only;
- the GR-14 prediction note.

A visitor can't see what the test is like, how learning works, or the common questions before buying.

**Decision (PO, 2026-10-06):** fill the page with **real content** using the **existing design system**. This is not a visual redesign.

## 2. Page order

| # | Section | Status |
|---|---|---|
| 1 | Hero + price card (sticky on desktop) | unchanged |
| 2 | **Cara kerja**: 4 steps | new |
| 3 | Tentang program ini | unchanged |
| 4 | **Silabus**, now showing each session's parts | changed |
| 5 | **Format tes ITP** | new |
| 6 | **FAQ** | new |
| 7 | **Closing CTA**: price + enrol button | new |
| 8 | Prediction note (GR-14) | unchanged |

### 2.1 Cara kerja
These are the same four steps `/how-it-works` shows today:
- Daftar & verifikasi email
- Bayar sekali
- Belajar berurutan
- Tes akhir & prediksi skor

The step list moves to one module, `frontend/components/program/howItWorksSteps.ts`, which both pages import, so the copy exists once. On the landing page the steps render as a compact 4-column row (a single column under 640px), numbered, because the order is a real sequence.

### 2.2 Silabus with parts
- **Video sessions** show their parts under the title as small labels, in order, e.g. `Video materi · Tes · Video pembahasan`. They use the parts' kinds: `LessonVideo` → "Video materi", `Test` → "Tes", `Discussion` → "Video pembahasan". A session with several tests shows each one.
- **Live sessions** keep their schedule line.
- **The final assessment** shows `140 soal · 115 menit`.

### 2.3 Format tes ITP
Three cards and a summary line.

| Section | Questions | Minutes |
|---|---|---|
| Listening | 50 | 35 |
| Structure & Written Expression | 40 | 25 |
| Reading | 50 | 55 |

The summary line reads `Total 140 soal · 115 menit · skor prediksi 310–677`.

The numbers come from `frontend/lib/itpFormat.ts`, a constant mirroring `backend/src/Domain/ToeflScoring.cs`. A backend test reads that file and fails if the two disagree (§5).

### 2.4 FAQ
- **Content:** the first 6 published FAQ items from the existing `GET /api/faq`, fetched on the server with the page's revalidation and ordered by their `OrderIndex`.
- **Display:** they use the same accordion as `/help`. Move `FaqAccordion` to `components/` and import it in both places.
- **Link to the rest:** a link `Lihat semua pertanyaan →` to `/help` is shown when there are more than 6.
- **When there is nothing to show:** if the fetch fails or returns nothing, the whole section is omitted.

### 2.5 Closing CTA
- **Price:** one row with the programme price (`{formatIdr(price)} · sekali bayar`).
- **Enrol button:** the same `EnrollCta` component as the price card, so the enrolment logic stays in one place.
- **Heading:** `Siap mulai persiapan TOEFL Anda?`

## 3. Backend

- **New field on public sessions.** `PublicSessionDto` gains `IReadOnlyList<PublicSessionPartDto> Parts`, where `PublicSessionPartDto(string Kind, string Title)`. Parts are ordered by `OrderIndex`. Video sessions get their parts; other types get an empty list. The DTO carries **no** asset id, assessment id or test content (GR-3, GR-11).
- **The query** is built in `ProgramService.GetPublicAsync`, in the same query or one extra query per page, never one per session.
- **Revalidation.** `SessionPartAdminService.SaveAsync` revalidates `/` and `/program/{slug}` after a successful save, the same way `ProgramAdminService` already does after session edits, so the public syllabus is never stale for up to 5 minutes.

## 4. SEO and accessibility

- **Structured data.** Add JSON-LD in the page:
  - `Course`: name, description, provider `INVERTA`, and `offers` with the price in IDR;
  - `FAQPage`: the FAQ items shown.

  Neither claims an official TOEFL score (GR-14). Strings are escaped safely inside the `<script type="application/ld+json">` with `JSON.stringify`.
- **Headings and page:**
  - one `h1` (the programme name), and one `h2` per section;
  - the steps form an ordered list;
  - visible focus on every interactive element;
  - layout verified at 375px.
- **The page stays SSG/ISR** with `revalidate = 300`.

## 5. Tests

**Backend:**
- the public programme DTO lists the parts in order for a video session, with the right kinds and titles, and an empty list for a live session;
- the raw JSON of the public endpoint contains no `providerAssetId` and no `assessmentId`;
- saving parts triggers revalidation of the programme paths. Use the existing fake revalidator in tests if there is one; otherwise assert through whatever seam `ProgramAdminService`'s revalidation test uses;
- `ItpFormatConstantsTests` reads `frontend/lib/itpFormat.ts` from the repo and asserts that its question counts, minutes and score range equal the `ToeflScoring` constants.

**Frontend:**
- lint and build;
- a manual check at 375px and at desktop width;
- the FAQ section omitted when the FAQ fetch fails.

## 6. Out of scope

- A visual redesign.
- Testimonials and instructor profile (sub-project 6).
- Preview videos.
- Changing `/how-it-works`, beyond sharing the steps module.
