# Course-book UI revamp Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle every live INVERTA surface as a course book: one white sheet per page on the blue-grey background, chapters marked by type and a rule instead of cards, the syllabus as a table of contents. Visual change only.

**Architecture:**
- A small set of layout primitives in `frontend/components/ui/Page.tsx` (sheet, page title, running head, chapter, footnote, table of contents) plus one `.data-table` CSS class.
- Each surface task rebuilds its pages from those primitives.
- No behaviour, route, query or API call changes.

**Tech Stack:** Next.js 15 App Router, React 19, Tailwind v4 (`@theme` in `app/globals.css`), TanStack Query. No new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-30-course-book-revamp-design.md`.

**Visual references:** open them in a browser.
- `docs/design/course-book/thesis.html`: content and type for the program page, dashboard and session page. Ignore its outer frame.
- `docs/design/course-book/frames.html`: frame B ("Lembar di meja") is the frame to build.

**Branch:** `feat/course-book-revamp`.

## Global Constraints

- **Colours:** use only the existing tokens (`bg`, `surface`, `surface-2`, `border`, `ink`, `ink-muted`, `ink-subtle`, `primary*`, `accent*`, `success*`, `warning*`, `danger*`). No new hex values in components.
- **Font:** Plus Jakarta Sans only, already loaded in `app/layout.tsx`. No new font.
- **Page title:** `clamp(32px,5vw,46px)`, `font-extrabold`, `tracking-[-0.025em]`, `leading-[1.08]`.
- **Chapter heading:** `text-[26px] font-extrabold tracking-[-0.015em]`.
- **Running head:** `text-[13.5px] font-semibold text-ink-muted tabular-nums`.
- **Body:** 16px at a 60–65ch measure.
- **Chapter:** `border-t border-ink pt-4`, about 48px (`mt-12`) above it. No cards inside a sheet.
- **Objects that keep a box:** price/enrol box, certificate cards, exam result, modals. They use `rounded-lg border border-border bg-surface`, no shadow.
- **Sticky headers:** solid `bg-surface`, never `/90`, `/95` or `backdrop-blur`.
- **Active nav link:** `text-ink` with a 2px primary underline (`shadow-[inset_0_-2px_0_var(--color-primary)]`), not a filled pill.
- **Removed from our own UI strings and markup:**
  - emoji;
  - "→" / "←" arrows inside link text;
  - em dashes (U+2014);
  - small uppercase purple eyebrows: each becomes a `RunningHead` or is dropped.

  Database content is left as entered.
- **Kept verbatim:** every GR-14 statement (the score is an INVERTA prediction, not an official ETS/TOEFL score).
- **Kept intact:**
  - every `data-tour` attribute (driver.js tour);
  - every `id` used for focus (`exam-result-title`, etc.);
  - every `aria-*` attribute;
  - the exam motion classes (`exam-*`).
- **Dormant routes are not touched:** `/catalog*`, `/modules/*`, `/pricing`, `/app/learn/*`, `/for-business`, `/style-guide`, `/feedback`.
- **Every task passes:**
  - `cd frontend && npx tsc --noEmit -p . && npx next lint`, with 0 errors;
  - its own tells grep (Task 7 runs the full audit).
- **Commits:** end each with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Phone width (375px):**
   - the sheet's side padding drops to 20px;
   - every `<table>` sits inside an `overflow-x-auto` wrapper;
   - the page itself never scrolls sideways.

   Owned by Task 1 (primitives); each surface task checks its own tables.
2. **Keyboard focus is visible** on every new link and table-of-contents row that is a link (`focus-visible:outline-2 outline-primary outline-offset-2`). Task 1's `TocItem` owns it.
3. **Long text wraps:** long session titles, long learner names and long certificate codes wrap inside their column (`min-w-0`, `break-words`) and never push the meta column off screen. Task 1's `TocItem` owns it; Tasks 4 and 5 check it with the longest real title, "Reading: skimming, scanning & vocabulary".
4. **Every state still renders inside the sheet:** empty, loading and error, including
   - not enrolled;
   - no final result yet;
   - certificate pending (no band mapping);
   - an ended live session.

   Tasks 4 and 5 list the states each page has and keep each one.
5. **The exam sitting:**
   - the sticky section header still sticks at the top, inside or outside the sheet;
   - no ancestor's `overflow-hidden` breaks `position: sticky`;
   - the `exam-*` motion still fires.

   Owned by Task 5.

---

### Task 1: Primitives, header and card

**Files:**
- Create: `frontend/components/ui/Page.tsx`
- Modify:
  - `frontend/components/ui/index.ts` (export the primitives);
  - `frontend/components/ui/Card.tsx` (drop `shadow-sm`);
  - `frontend/app/globals.css` (add `.data-table`);
  - `frontend/components/app/AppHeader.tsx`;
  - `frontend/components/PublicNav.tsx`;
  - `frontend/components/exam/Sitting.tsx` (line 51, header only).

**Interfaces (Produces):** all are server-safe, so no `"use client"` is needed.
- `SheetPage({ children, wide?: boolean }): JSX.Element`
  - Outer: `bg-bg min-h-[calc(100vh-58px)] px-3 py-4 sm:px-7 sm:py-8`.
  - Inner: the sheet, `mx-auto w-full rounded-lg bg-surface px-5 py-8 sm:px-11 sm:py-10 shadow-[0_1px_0_var(--color-border),0_10px_30px_-18px_rgba(17,37,63,0.2)]`, sized `max-w-[760px]`, or `max-w-6xl` when `wide`.
  - Never `overflow-hidden` (sticky children must work).
- `RunningHead({ children, className? })`: renders a `<p>` with the running-head classes.
- `PageTitle({ children, id?, tabIndex?, className? })`: renders an `<h1>` with the page-title classes plus `mt-2 text-balance`, and `focus:outline-none` only when `tabIndex` is set.
- `Lead({ children })`: `<p className="mt-4 max-w-[58ch] text-[18px] leading-relaxed text-ink-muted">`.
- `Chapter({ title, intro?, id?, children, className? })`: a `<section aria-labelledby>` with `mt-12 border-t border-ink pt-4`, an `<h2>` with the chapter-heading classes, an optional intro `<p className="mt-2 max-w-[60ch] text-ink-muted">`, then the children.
- `Footnote({ children })`: `<p className="mt-4 max-w-[66ch] text-[13px] text-ink-muted">`.
- `Toc({ children, label })`: `<ol aria-label={label} className="mt-4 flex flex-col">`.
- `TocItem({ n, title, kind?, meta, state, href? })`:
  - Types: `n: number`, `title: string`, `kind?: string` (an amber pill: `rounded-full bg-accent-soft px-2 text-[11.5px] font-bold text-accent`), `meta: ReactNode`, `state: "done" | "current" | "open" | "locked"`, `href?: string`.
  - Row grid: `grid grid-cols-[2.2em_minmax(0,1fr)_auto] items-baseline gap-2.5 border-b border-surface-2 py-[11px]`.
  - The number cell shows `✓` in `text-success` when `done`, otherwise `n` in `text-ink-subtle`, or `text-primary` when `current`.
  - `current` adds `-mx-3 px-3 rounded-sm bg-primary-soft border-transparent`.
  - `locked` sets the title to `text-ink-muted font-semibold`.
  - When `href` is set and the state is not `locked`, the title is a `next/link` with a visible focus outline.
  - Long titles wrap (`min-w-0 break-words`).
- `.data-table` in `globals.css`:
  - `width:100%; border-collapse:collapse; font-variant-numeric:tabular-nums`;
  - `th`: left-aligned, 12.5px, ink-muted, weight 600, padding `0 12px 8px 0`, with a 1px `var(--ds-color-ink)` bottom border;
  - `td`: padding `11px 12px 11px 0`, with a 1px `var(--ds-color-surface-2)` bottom border;
  - `.num`: right-aligned;
  - `tfoot td`: weight 800, a 1px ink top border, no bottom border.

- [ ] **Step 1: Branch.** Run `git checkout -b feat/course-book-revamp` from up-to-date `main`.
- [ ] **Step 2: Write `Page.tsx` and the exports.** Use the signatures above. Keep the class strings exactly as given, and match the file style of `components/ui/Card.tsx` (`cn` from `@/lib/cn`).
- [ ] **Step 3: Add `.data-table` to `globals.css`.** Put it after the `--ds-*` block and before the exam motion section, under the comment `/* ---- Course book: data tables ---- */`.
- [ ] **Step 4: Update `Card.tsx`.** Remove `shadow-sm` and update the doc comment ("Object box: white, hairline border, lg radius.").
- [ ] **Step 5: Update the headers.**
  - `AppHeader` and `PublicNav`: change `bg-surface/90 backdrop-blur` to `bg-surface`.
  - `AppHeader` links: the active state becomes `text-ink shadow-[inset_0_-2px_0_var(--color-primary)] rounded-none`, and inactive stays `text-ink-muted hover:text-ink`. Apply the same to `PublicNav`'s active link if it has one.
  - `Sitting.tsx:51`: change `bg-surface/95 ... backdrop-blur` to `bg-surface`.
  - Keep every `data-tour` attribute.
- [ ] **Step 6: Verify.**
  - Run `cd frontend && npx tsc --noEmit -p . && npx next lint`. Expected: no errors.
  - Run `grep -rn "backdrop-blur" components app --include=*.tsx | grep -v -E "catalog|modules|pricing|learn/|for-business|style-guide"`. Expected: only modal scrims, if any; no headers.
- [ ] **Step 7: Commit** with the message `feat(ui): course-book page primitives, solid headers, unshadowed cards`.

### Task 2: Public program page

**Files:**
- Modify:
  - `frontend/app/(marketing)/program/[slug]/page.tsx`;
  - `frontend/components/program/{EnrollCta,HowItWorks,SyllabusParts,ItpFormat,ProgramFaq,ClosingCta}.tsx`;
  - `frontend/components/program/howItWorksSteps.ts` (copy only);
  - `frontend/components/FaqAccordion.tsx`.

**Interfaces:** consumes Task 1's `SheetPage`, `RunningHead`, `PageTitle`, `Lead`, `Chapter`, `Footnote`, `Toc`, `TocItem` and `.data-table`.

Target, from `thesis.html` "Program page", inside `SheetPage` (not wide). On ≥860px the sheet holds a two-column grid: `minmax(0,1fr) 300px`, gap 56px.

- [ ] **Step 1: Build the title page.**
  - `RunningHead`: "Program persiapan TOEFL ITP".
  - `PageTitle`: the program title from the API, unchanged.
  - `Lead`: the program summary.
  - Facts as a `<dl>` in a 4-column grid (2 columns under 560px): `dt` 12.5px ink-muted, `dd` 20px extrabold tabular. Use the same facts the page shows today (sessions, video total, live count, final test minutes). This replaces the stat row.
- [ ] **Step 2: Make `EnrollCta` the only box.**
  - It sits in the right column, `sticky top-[78px]`, styled as an object box (`rounded-lg border border-border p-[22px]`).
  - Contents: the price at 30px extrabold tabular with the "sekali bayar, bukan langganan" line, the primary button full width, the existing benefit list with a `text-success` ✓ per item, and the existing fine print.
  - All enrol behaviour stays unchanged.
- [ ] **Step 3: Make the syllabus a `Chapter`.**
  - Title "Daftar isi", intro "Sesi terbuka berurutan. Sesi berikutnya terbuka setelah Anda lulus tes di sesi sebelumnya."
  - Each session is a `TocItem`: `state="open"`, no `href`, `kind` = "Live" for live sessions and "Tes akhir" for the final, and `meta` = the duration/format text shown today.
  - Delete the numbered-circle and card markup. `SyllabusParts` either renders inside the row as the `meta` line or is folded into `meta`; pick whichever keeps its information.
- [ ] **Step 4: Make the ITP format a table.** Use `Chapter` "Format tes akhir" with `ItpFormat` as `<table className="data-table">`:
  - columns Bagian, Soal, Waktu, Skor skala;
  - a `tfoot` Total row;
  - the values from `ITP_FORMAT` in `lib`;
  - wrapped in `overflow-x-auto`.

  Then a `Footnote` with the existing GR-14 sentence.
- [ ] **Step 5: Make "Cara kerja" an ordered list.** Use `Chapter` "Cara kerja" with an `<ol className="list-decimal pl-6 marker:font-extrabold marker:text-primary">`. Each step is a bold line plus an ink-muted sentence. The circles go.
- [ ] **Step 6: Unbox the FAQ.** `ProgramFaq` uses `Chapter` "Pertanyaan umum".
  - `FaqAccordion` loses its boxes: each item is a `<details>` (or the existing toggle) separated by a `border-b border-surface-2`, the question bold at 16px, the answer ink-muted below.
  - Keep keyboard operation.
- [ ] **Step 7: Rework `ClosingCta`.** It becomes a plain closing paragraph with the primary button, not a coloured band.
- [ ] **Step 8: Clean the copy and verify.**
  - Copy pass on these files: no em dash, no arrows, no emoji, no uppercase eyebrow.
  - Run `grep -n "—\|→\|uppercase" <the files above>`. Expected: no hits in displayed strings.
  - Run `tsc` and `lint` as in Task 1.
- [ ] **Step 9: Commit** with the message `feat(ui): program page as a course-book title page and table of contents`.

### Task 3: Public shell, marketing pages, auth, checkout, verify

**Files:**
- Modify:
  - `frontend/components/PublicShell.tsx`;
  - `frontend/components/SiteFooter.tsx`;
  - `frontend/app/(marketing)/{how-it-works,about,help,contact,legal/[doc]}/page.tsx`;
  - `frontend/app/(marketing)/contact/ContactForm.tsx`;
  - `frontend/app/(auth)/layout.tsx`;
  - `frontend/components/auth/AuthCard.tsx`;
  - the five `frontend/app/(auth)/*` pages, for eyebrows/copy only;
  - `frontend/app/checkout/success/{page,SuccessClient}.tsx`;
  - `frontend/app/checkout/dev-pay/*`;
  - `frontend/app/verify/[code]/page.tsx`;
  - `frontend/app/maintenance/page.tsx`.

**Interfaces:** consumes Task 1's primitives.

- [ ] **Step 1: `PublicShell`.** It renders `PublicNav`, then `SheetPage` around the children, then `SiteFooter`.
  - The footer sits on `bg-bg`, unboxed, in small ink-muted text.
  - Each marketing page uses `RunningHead` / `PageTitle` / `Lead` / `Chapter` instead of its own heading markup.
- [ ] **Step 2: `how-it-works`.** The steps become the same ordered list as Task 2 Step 5.
- [ ] **Step 3: `about`, `help`, `contact` and `legal`.**
  - Each uses chapters.
  - `help` uses the unboxed `FaqAccordion` from Task 2.
  - `legal` keeps its review banner as one `Footnote`-style note at the top, in `bg-warning-soft rounded-sm px-4 py-3`.
- [ ] **Step 4: Auth.**
  - `(auth)/layout.tsx` uses `bg-bg` (replacing `#E9EEF5`) and centres a `max-w-[440px]` sheet with the sheet classes from Task 1.
  - `AuthCard` becomes an unboxed stack, since the sheet is the box. Its title uses `PageTitle` at the smaller size `text-[30px]`; the auth pages pass it via `className`.
  - `AuthError` stays as it is.
- [ ] **Step 5: Checkout.**
  - `success` becomes a sheet with `PageTitle`, a paragraph and the two links, without the floating card.
  - `dev-pay` changes the "Langganan" label to "Program" when `kind=ProgramPurchase`. It keeps "Langganan" for any other kind.
- [ ] **Step 6: `verify/[code]`.**
  - A sheet with a `RunningHead` "Verifikasi sertifikat", the learner name as `PageTitle`, the result as a `.data-table`, and the GR-14 text as a `Footnote`.
  - Keep its SSG/ISR exports unchanged.
- [ ] **Step 7: `maintenance`.** A sheet with `PageTitle` and the existing paragraph.
- [ ] **Step 8: Copy pass and verify.** Remove em dashes, arrows, emoji and eyebrows from these files, then run `tsc` and `lint`.
- [ ] **Step 9: Commit** with the message `feat(ui): public shell, auth, checkout and verify on the course-book sheet`.

### Task 4: Learner dashboard

**Files:**
- Modify:
  - `frontend/app/app/dashboard/page.tsx`;
  - `frontend/components/dashboard/{ProgressHeader,FinalResultCard,SessionTestHistory,CertificateCards}.tsx`.
- Delete: `frontend/components/dashboard/FinalResultChart.tsx`, if nothing imports it after Step 4.

**Interfaces:** consumes Task 1's primitives. Data comes from the hooks and queries the page uses today; don't add API calls.

Target, from `thesis.html` "Dasbor", inside `SheetPage`:

- [ ] **Step 1: Header.**
  - `RunningHead` with the program title.
  - `PageTitle` "Selamat datang kembali, {firstName}", with no emoji.
- [ ] **Step 2: Next-up block (`ProgressHeader`).**
  - A running head "Berikutnya, sesi {k} dari {n}".
  - The next session title at `text-[30px] font-extrabold`.
  - One ink-muted sentence for what unlocks it. Reuse what the page knows today: the next session's kind.
  - A segmented meter: n bars, `h-1.5 w-[26px] rounded-[3px]`, done bars `bg-primary`, the rest `bg-border`, with an `aria-label` of "{done} dari {n} sesi selesai".
  - The primary button "Lanjutkan sesi {k}" links to the session.
  - The progress ring goes.
  - When every session is done, show "Semua sesi selesai" and link to the certificate. Keep the existing all-done message's meaning.
  - Keep the `data-tour` attributes.
- [ ] **Step 3: Table of contents.** A `Chapter` "Daftar isi" with a `TocItem` per session:
  - state from completion and unlock;
  - `meta` = "Lulus, {score}" for passed tests, "Hadir" for attended live, "Berikutnya" for the current session and "Terkunci" for locked ones;
  - `href` to the session page when not locked.

  Use only fields the page already has. If the score isn't available, use "Selesai".
- [ ] **Step 4: Final result (`FinalResultCard`).** A `Chapter` "Hasil tes akhir":
  - the total at `text-[60px] font-extrabold text-primary tabular-nums` with "Skor prediksi¹" under it;
  - a `.data-table` with Bagian, Benar, Skor, in ITP order;
  - a `Footnote` "¹ Skor prediksi INVERTA, bukan skor TOEFL resmi dari ETS." (the existing GR-14 sentence; keep its words).

  The chart goes. Show "Belum ada hasil tes akhir." when no result exists.
- [ ] **Step 5: Test history (`SessionTestHistory`).** A `Chapter` "Riwayat tes sesi".
  - Keep the chart if it has data; drop its card wrapper.
  - The empty text stays.
- [ ] **Step 6: Certificates (`CertificateCards`).** These keep the object box, with no shadow:
  - a title, then the score and date as a running head, the code in `font-mono text-[13px]`;
  - links "Unduh PDF" and "Halaman verifikasi", underlined primary, without arrows.
- [ ] **Step 7: Verify.**
  - Run `tsc` and `lint`.
  - Check by reading the code that the longest title wraps (`TocItem` handles it) and that the not-enrolled empty state still renders inside the sheet.
- [ ] **Step 8: Commit** with the message `feat(ui): dashboard as next-up, table of contents and score report`.

### Task 5: Learner session, program, assessment, exam, certificates, account, notifications

**Files:**
- Modify:
  - `frontend/app/app/{program/[id],session/[id],assessment/[id],certificates,account,notifications}/page.tsx`;
  - `frontend/app/app/exam/[attemptId]/page.tsx`;
  - `frontend/components/learn/{SessionView,GatingTest,NotesPanel,QuizPanel,RatingWidget}.tsx`;
  - `frontend/components/exam/ExamResult.tsx`, framing only.

**Interfaces:** consumes Task 1's primitives.

- [ ] **Step 1: Session page (`SessionView`).** Following `thesis.html` "Halaman sesi":
  - `RunningHead` "Sesi {k} dari {n}", then the title as `PageTitle`, then the description as `Lead`, then the video.
  - The parts as a list: a status mark (done = filled success circle, partly done = half primary, not started = border ring), the bold part name, an ink-muted status line, and the action button on the right.
  - A pager at the bottom (`border-t border-border pt-4`, previous and next session, "Terkunci" when locked), using the session data the view already has. Skip the pager when that data isn't available.
  - `GatingTest`, `NotesPanel` and `QuizPanel` keep their behaviour and lose card shadows; use the object box only where they are a form the learner submits.
  - Keep every `aria-*` and focus behaviour.
- [ ] **Step 2: In-app program page (`app/program/[id]`).** `PageTitle`, then the same `Toc` as the dashboard. Reuse the Task 4 meta rules.
- [ ] **Step 3: Assessment intro (`assessment/[id]`).**
  - A sheet with `RunningHead` "Tes akhir", `PageTitle`, and the facts as a `.data-table` (soal, waktu, kesempatan).
  - The proctoring notice stays a `bg-warning-soft rounded-sm` note.
  - The GR-14 text becomes a `Footnote`.
  - The start button is unchanged.
- [ ] **Step 4: Exam.**
  - The exam page renders the sitting and the result inside `SheetPage`.
  - Check the sticky `Sitting` header still sticks: no `overflow-hidden` ancestor, and its `-mx-6 px-6` matches the sheet padding. Change it to `-mx-5 px-5 sm:-mx-11 sm:px-11`.
  - `ExamResult` keeps its result box, with no shadow.
  - Keep every `exam-*` class and the `exam-result-title` id.
- [ ] **Step 5: `certificates`, `account` and `notifications`.**
  - Each has `PageTitle` and chapters; certificates use the Task 4 certificate box.
  - The account forms sit unboxed in chapters ("Profil", "Kata sandi", "Tagihan", and so on); keep every form and handler.
  - Notifications are a list with `border-b border-surface-2` rows.
- [ ] **Step 6: Copy pass and verify.** Remove em dashes, arrows, emoji and eyebrows from these files, then run `tsc` and `lint`.
- [ ] **Step 7: Commit** with the message `feat(ui): learner pages on the course-book sheet`.

### Task 6: Admin

**Files:**
- Modify:
  - `frontend/app/admin/layout.tsx`;
  - the live admin pages `frontend/app/admin/{page,programs,programs/[id]/score-bands,questions,questions/import,enrollments,attempts,users,assessments/[id]}` (`page.tsx` in each), headings and wrappers only;
  - `frontend/components/admin/fields.tsx`, only if it holds shared wrapper classes.

**Interfaces:** consumes Task 1's `SheetPage` (with `wide`), `PageTitle`, `Chapter` and `.data-table`.

- [ ] **Step 1: Layout.** The content area renders the children inside `SheetPage wide`. The dark sidebar stays as it is.
- [ ] **Step 2: Each live admin page.**
  - The page heading becomes `PageTitle` with `className="text-[30px]"` (admin is denser).
  - Outer card wrappers around sections become `Chapter`s, or plain `div`s when a page has a single section.
  - Existing `<table>` elements get `className="data-table"` inside `overflow-x-auto`. Their own row styling can be removed where it conflicts.
  - Modals keep their box.
  - No logic changes.
- [ ] **Step 3: Copy pass and verify.** Remove em dashes and arrows from our UI strings; the `app/admin/attempts/page.tsx:81` "—" empty-cell marker becomes "Tidak ada". Then run `tsc` and `lint`.
- [ ] **Step 4: Commit** with the message `feat(ui): admin pages on the wide sheet`.

### Task 7: Tells audit, build and leftovers

**Files:** any live-surface file the audit flags.

- [ ] **Step 1: Run the audit.**
  - From `frontend`: `python3 "$SKILL_BASE/design-audit/scripts/audit.py" . --group tells`. The controller supplies `SKILL_BASE`.
  - Every finding on a live surface is fixed, or kept only when the spec's "Allowed patterns" names it.
  - Expected leftovers, not problems: `animate-spin` in `Spinner` and `animate-pulse` in `Skeleton` (real loading states), plus findings in dormant routes.
- [ ] **Step 2: Run the build.** Run `npm run build` in `frontend`. Expected: "Compiled successfully". The `fetch failed` lines from static pages while no API runs are known and fine.
- [ ] **Step 3: Grep for leftover cards.** Run `grep -rn "shadow-sm\|shadow-lg" app components --include=*.tsx`, excluding dormant routes and `Modal`. Each remaining hit is either an object box (allowed) or removed.
- [ ] **Step 4: Commit** with the message `chore(ui): course-book tells pass`.

## Outcome
(filled in after execution)
