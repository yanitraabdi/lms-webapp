# Full-width pages everywhere — design

**Date:** 2026-11-05.
**Status:** the PO asked for "all of them" and then said "just use your judgement and start". Every layout decision below is the builder's call unless it is marked as the PO's.

## 1. Why
The 2026-11-03 redesign (`2026-11-03-landing-and-learner-shell-design.md`) removed the card look from three places: the program page, the learner sidebar and the dashboard. Every other live page still uses the old look, a narrow white "sheet" card (`SheetPage` / `SHEET_CLASSES`) floating on the grey background. The PO dislikes the card look and the wasted space, and asked for **all** remaining pages to be changed (PO's words: "All of them").

**Success:**
- No live page renders a floating white card (`SHEET_CLASSES` shadow and rounded box) any more.
- Each area uses the width the way the redesigned pages do.
- Everything else is unchanged: behaviour, copy, the golden rules, accessibility (axe at serious/critical, one `h1`, no overflow at 375px), and every e2e test.

## 2. The rule for every page
- **No floating card.** Content sits directly on a full-width surface: `bg-surface` for reading pages, `bg-bg` where panels sit on it.
- **Wide container, readable text.** The page container is `mx-auto max-w-6xl` with the same gutters as the landing bands and `LearnerShell` (`px-6`, or `px-4 sm:px-8` in the app). Running text inside stays at a comfortable measure (about 65–70ch), so prose never runs 1100px wide.
- **Panels like the dashboard's.** Grouped content uses a top rule and no shadow. The only exception is the dark "Berikutnya" style, kept for a single emphasised panel.
- **No new design tokens, no new dependencies, no new copy** beyond what a new layout strictly needs.

## 3. Pages, area by area

### 3.1 Shared layout pieces (the main lever)
Most pages go through a handful of shared wrappers, so most of the change happens there:
- **`SheetPage`** (`components/ui/Page.tsx`):
  - Used by the 4 learner pages, the session page (`SessionView`), the assessment intro and the exam page, and by `PublicShell`.
  - Becomes a no-card container: a full-width background with the content in `max-w-6xl` (the `wide` prop then has no effect).
  - **New prop `focus`:** a narrower, centred column (about `max-w-3xl`) with no card. Used only for the test screens (§3.4).
  - The name stays, to avoid churn across about 10 call sites. *(My call.)*
- **`SHEET_CLASSES`:** removed once nothing uses it.
  - Today it's used by: auth layout, verify, checkout success, maintenance, admin layout.
  - Each of those gets its own no-card treatment below.
- **`PublicShell`:** keeps the nav and footer; its content area becomes the no-card wide surface.

### 3.2 Learner pages (sidebar shell)
**Pages:** `/app/certificates`, `/app/notifications`, `/app/account`, `/app/program/[id]`.

**Layout:** no card, in the same container as the dashboard: `mx-auto max-w-6xl px-4 py-6 sm:px-8`.

**Where the width lets each page do more** *(my calls)*:
- **Sertifikat:** certificates in a responsive grid (1 column on phones, 2 at `md`, 3 at `xl`), each in the dashboard's top-rule panel style.
  - The GR-14 text stays on every certificate.
- **Profil & tagihan:**
  - **Layout:** two columns at `lg`: profile and password on the left, enrolment and receipts on the right.
  - **Danger zone:** account deletion stays at the bottom, full width.
- **Notifikasi:** the list on the left; notification preferences in a right column at `lg`.
- **Program detail:** it already has a master-detail layout (`wide`). It only loses the card.

### 3.3 Public info pages (`PublicShell`)
**Pages:** `/about`, `/how-it-works`, `/help`, `/contact`, `/legal/[doc]`.

- **Container:** no card. A full-width white surface, with content in `max-w-6xl px-6`, title area first.
- **About, How it works:** use the landing `Band` rhythm where content splits naturally into sections. The `h1` is in the first band. Prose stays at about 65ch.
- **Legal pages (5):** two columns at `lg`. *(My call: legal texts are long, and a contents list makes them usable.)*
  - **Left, sticky:** a "Daftar isi" (contents) list of the section headings, which already have `sec-{i}` ids.
  - **Right:** the text, at about 70ch.
  - **Phones:** the contents list is hidden.
- **Help:** the FAQ goes full width. The search, if present, stays at the top.
- **Contact:** the form and the contact details side by side at `md`.

### 3.4 Test screens: focus mode, no card *(my call)*
**Pages:** the session page (`/app/session/[id]`, through `SessionView`), the assessment intro (`/app/assessment/[id]`) and the exam sitting and result (`/app/exam/[attemptId]`).

These screens stay **narrow on purpose**, so a learner concentrating on questions isn't spread across a 1400px line.
- They lose the floating card: `SheetPage focus`, a centred `max-w-3xl` column directly on a white full-height surface.
- **Behaviour is untouched:** the timer, the exam's sticky header, proctoring, the audio player and the Sitting UI are not edited. Only the outer wrapper changes.
- The sticky exam header's negative margins (`-mx-5 sm:-mx-11`) are tied to the old card padding. Adjust them to the new padding so the header still spans the column.

### 3.5 Sign-in pages *(my call)*
**Pages:** `(auth)` layout, i.e. login, register, forgot/reset password and verify-email.

- **Desktop (`lg` and up):** a split screen, no card.
  - **Left half:** a brand panel in `bg-ink`: the white logo, the landing headline ("Belajar TOEFL langkah demi langkah, sampai tahu prediksi skor Anda.") and the five-step path as a static list (not the animation). This reminds the learner what they're signing up for.
  - **Right half:** the form on `bg-surface`, about 400px wide, vertically centred.
- **Phones and tablets:** the form alone on `bg-surface`, full width with gutters. The logo stays at the top, as today.
- A login card is a common, deliberate pattern, but the PO asked for all pages. The split screen keeps focus on the form while removing the floating card.

### 3.6 One-off pages *(my calls)*
- **`/verify/{code}`:** no card. Its own slim header stays.
  - **Valid:** the certificate shows as a wide "document" band: the name large, the scores in a row, the GR-14 disclaimer and the verification facts beside them at `md`.
  - **Not found:** the not-found state is a plain message in the same container.
  - All GR-14 copy is unchanged.
- **Checkout success, maintenance:** a centred message with no card, on the plain background. These are short status messages, so a centred column (about 520px) without a box is right.
- **Admin layout:** the dark sidebar is unchanged. The content area loses its card: `bg-surface` with full height, and pages in `max-w-6xl` with padding. Admin pages' own tables and forms are unchanged.

## 4. Constraints
- **Golden rules hold.** This is presentation only:
  - GR-4: no logic in Next.js.
  - GR-14: copy unchanged wherever a score appears.
  - GR-11–13: exam behaviour untouched.
- **e2e suite.** All tests pass unchanged, except where a test's locator targets the removed card itself.
  - Add a smoke assertion that no page renders the old card shadow, so the look can't come back.
  - Add a `/legal/terms` desktop check that the contents list links to the sections.
- **Testing route.** Use `npm run e2e:dev` (`next dev`, no Docker builds). Build the production image once, at the end.
- **Dormant archived routes** (`/catalog*`, `/modules/*`, `/pricing`, `/for-business`, `/style-guide`, `/feedback`, `/app/learn/*`) are out of scope. They may change only as a side effect of `PublicShell`/`SheetPage` and must not break; the `(catalog)` layout is not edited.

## 5. Left to the builder
- Exact spacing, breakpoints and type sizes within the existing scale.
- How each About / How-it-works section maps to bands.
- The sticky offset of the legal contents list.
