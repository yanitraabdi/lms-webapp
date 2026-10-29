# Course-book UI revamp — design

Validated with the user on 2026-10-30 (genjutsu cast, rendered-page gates).

## Problem
The UI reads as AI-generated:
- "Card soup": every block is the same white card on a blue-grey background (`rounded-lg` ×79).
- A SaaS template hero (title, bold stat row, price card with checkmarks).
- Reflex layouts: numbered circles, three equal cards, boxed accordions.
- Small tells: 👋, "→" links, purple uppercase eyebrows, blurred sticky headers, em dashes in our copy.
- Flat type: titles barely larger than body text.

## Visual thesis (validated)
INVERTA reads like a well-set course book: one reading column, the syllabus as its table of contents,
chapters marked by type and white space instead of boxes. A box appears only around something you act
on or keep: the price, a certificate, a result.

**Allowed patterns:**
- a dark hairline rule above each chapter heading;
- a running head ("Sesi 3 dari 8");
- session numbers, because the program is strictly linear;
- tabular numerals for scores, times and prices.

**Rejected by the user:**
- dot leaders in the table of contents ("too many unnecessary components");
- the exam-room direction (too many borders);
- the study-desk direction (too much colour).

## Frame (validated: variant B, "Lembar di meja")
- The blue-grey app background (`--color-bg`) stays.
- Each page is **one white sheet** centred on it:
  - reading pages: max 760px wide;
  - admin pages: a wide sheet.
- That sheet is the only large box on screen. Inside it, content is unboxed.

References (open in a browser):
- `docs/design/course-book/thesis.html`: the three surfaces in the course-book direction. Its outer frame is A, so read it for content and type only.
- `docs/design/course-book/frames.html`: frame B is the one to build.

## Rules
1. **Colour:** every colour token stays, and no new colours. Primary stays the action colour. Accent (amber) only for kind labels such as "Live" and "Tes akhir".
2. **Type:** Plus Jakarta Sans only.
   - Page title: `clamp(32px, 5vw, 46px)`, weight 800, tracking −0.025em, line-height 1.08.
   - Chapter heading: 26px, weight 800.
   - Running head: 13.5px, weight 600, ink-muted.
   - Body: 16px at a measure of 60–65ch.
   - Small: 13–13.5px.
3. **Chapters:** a chapter is a section with a 1px `--color-ink` rule on top, 16px padding above its heading, and about 48px of space before it. No cards inside a sheet, except the "objects" in rule 4.
4. **Objects that keep a box:** a 1px border with `rounded-lg` and no shadow, inside the sheet. These are:
   - the price/enrol box;
   - certificate cards;
   - the exam result;
   - modals.
5. **Table of contents:** one row per session, separated by a `surface-2` line, laid out as
   `number | title (+ kind label) | meta`.
   - Done: a ✓ in success colour.
   - Current: a primary-soft row highlight.
   - Locked: the title in ink-muted.
   - No dot leaders.
6. **Data:** a real `<table>` with a 1px ink rule under the header and `tabular-nums`. Never a row of equal cards.
7. **Sticky headers:** solid `bg-surface`, never translucent or blurred.
8. **Navigation:** the active link is ink text with a 2px primary underline, not a filled pill.
9. **Removed from our own copy and markup:**
   - emoji;
   - "→" arrows in links;
   - em dashes (U+2014) in UI strings we wrote;
   - small uppercase purple eyebrows (they become a running head or are dropped).

   Database content (for example session titles) is left as entered.
10. **Kept:**
    - the exam motion from `feat/exam-motion`;
    - every GR-14 statement ("prediksi INVERTA, bukan skor TOEFL resmi / ETS");
    - every existing behaviour, route and API call.

    This is a visual change only.
11. **Scope:**
    - live INVERTA surfaces: public marketing, program, auth, checkout, verify, learner app and admin;
    - dormant archived routes are not touched: `/catalog*`, `/modules/*`, `/pricing`, `/app/learn/*`, `/for-business`, `/style-guide` (CLAUDE.md: do not extend dormant code).

## Out of scope
- Dark mode (the app is light-only).
- New features or new copy beyond rephrasing to drop the removed patterns.
- Motion beyond what already ships.
