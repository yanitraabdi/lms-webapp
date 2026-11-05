# Full-width pages everywhere: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task by task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** remove the floating white "sheet" card from every remaining live page and give each area a layout that uses the width.

**Architecture:** this is presentation only, in `frontend/`.
- The main lever is the shared wrappers: `SheetPage`, `SHEET_CLASSES`, `PublicShell`, the `(auth)` layout and the admin layout. Pages then get small per-page layout changes.
- The tests are Playwright e2e plus axe, run with `npm run e2e:dev`. It serves `next dev` on :3100 against the e2e API and Postgres containers, with no Docker builds.

**Tech stack:** Next.js 15 App Router, Tailwind v4 tokens in `app/globals.css`, and Playwright with `@axe-core/playwright`.

**Spec:** `docs/superpowers/specs/2026-11-05-full-width-pages-design.md`. Read it first. The § references below point into it.

## Global constraints
- **Presentation only.**
  - No change to API calls, data, auth, scoring, timers, proctoring or exam behaviour (GR-4, GR-11–13).
  - Copy unchanged, except a new "Daftar isi" label on legal pages.
  - GR-14 text stays verbatim wherever a score appears.
- **Styling.**
  - Existing design tokens only, no raw hex except the AA hexes already used (`#166534`, `#92400E`).
  - Plus Jakarta Sans.
  - No new npm dependencies.
- **Accessibility.**
  - Axe stays at zero serious or critical findings.
  - Exactly one `h1` per page.
  - No console errors.
  - No horizontal scroll at 375px.
- **Measure.** Running text stays at about 65–70ch inside the wide containers.
- **Testing.**
  - Use `cd frontend && npm run e2e:dev [-- <file>]`. Never use Docker builds, `npm run e2e:up` or `next build` before Task 6.
  - The Docker VM is shared with other projects.
  - If `inverta-e2e-api-1` exits with code 137 (out of memory), `docker start inverta-e2e-api-1` is allowed.
- **Out of scope.** Dormant routes: `/catalog*`, `/modules/*`, `/pricing`, `/for-business`, `/style-guide`, `/feedback`, `/app/learn/*`. They must not break; don't redesign them.
- **Each task ends with** `npm run lint`, `npx tsc --noEmit`, and that task's e2e files passing.

## Review focus
1. **The exam sticky header.** It spans its column via negative margins tied to the old card padding (`-mx-5 sm:-mx-11` in `components/exam/Sitting.tsx`). After the wrapper change it must still span the column without overflow. Task 1 checks this at 375px and 1280px.
2. **Long legal pages on phones.** The contents list must not appear on phones or push the text. Task 3's test covers it.
3. **The auth split on tablets (768–1023px).** Below `lg` the form shows alone, without a half-empty brand panel. Task 4's smoke check at 375px covers phones; check 800px in Task 6.
4. **The account page's danger zone.** Account deletion must stay full width, at the bottom, and keep its confirmation flow. Task 2 does not touch its logic.
5. **The card coming back.** A smoke assertion (Task 1) fails if any page renders the old card shadow.

---

### Task 1: the no-card `SheetPage`, `focus` mode, `PublicShell`, and a guard test

**Files:**
- Modify: `frontend/components/ui/Page.tsx` (`SheetPage`)
- Modify: `frontend/components/PublicShell.tsx`
- Modify: the test screens, to use `focus`:
  - `frontend/components/learn/SessionView.tsx` (lines ~139, 147)
  - `frontend/app/app/assessment/[id]/page.tsx`
  - `frontend/app/app/exam/[attemptId]/page.tsx`
- Modify: `frontend/components/exam/Sitting.tsx` (sticky header negative margins only)
- Test: `frontend/e2e/smoke.spec.ts`

**Interfaces:**
- Produces `SheetPage({ children, wide?, focus? })`.
  - It renders no card: no `SHEET_CLASSES`.
  - **Default:** a full-width `bg-surface` area, `min-h-[calc(100vh-58px)]`, with content in `mx-auto w-full max-w-6xl px-4 py-8 sm:px-8 sm:py-10`.
  - **`focus`:** the same surface, with content in `max-w-3xl`.
  - **`wide`:** kept for compatibility; it is the same as the default.
- `SHEET_CLASSES` is still exported in this task. Task 5 removes it.

- [ ] **Step 1: write the failing guard test.** In `smoke.spec.ts`, add to `checkPage` (run for every public, learner and admin page, on both projects):

```ts
// The old floating "sheet" card (SHEET_CLASSES) must not come back on any live page.
const cards = await page.evaluate(() =>
  [...document.querySelectorAll("body *")].filter((el) => getComputedStyle(el).boxShadow.includes("-18px")).length);
expect(cards).toBe(0);
```

- [ ] **Step 2:** Run `npm run e2e:dev -- e2e/smoke.spec.ts`. It should FAIL on pages using `SheetPage` or `SHEET_CLASSES`.

- [ ] **Step 3: implement.**
  - Change `SheetPage` as specified, and keep `PublicShell` using it.
  - Pass `focus` in `SessionView`, the assessment page and the exam page.
  - Update the `Sitting.tsx` header's negative margins to the new padding (`-mx-4 sm:-mx-8`), so the sticky bar still spans the column edge to edge.

- [ ] **Step 4:** Run `npm run e2e:dev`, the whole suite.
  - The pages behind `SHEET_CLASSES`: auth, admin, verify, checkout, maintenance. Those still failing the guard are expected until Tasks 4–5. Temporarily scope the guard with a `CARD_PENDING` allow-list of those paths (smoke rows `/login`, `/register`, `/forgot-password`, `/verify/UNKNOWN-CODE`, every `/admin*`). Tasks 4 and 5 delete entries from it.
  - Everything else should PASS, including `journey.spec.ts`, which sits the whole exam.

- [ ] **Step 5:** commit with message `feat(ui): sheet pages without the card; focus column for test screens`.

---

### Task 2: learner page layouts (spec §3.2)

**Files:**
- Modify: `frontend/app/app/certificates/page.tsx`
- Modify: `frontend/app/app/account/page.tsx`
- Modify: `frontend/app/app/notifications/page.tsx`
- Check only: `frontend/app/app/program/[id]/page.tsx` (it already uses `SheetPage wide`; Task 1 made that card-free)
- Test: `frontend/e2e/shell.spec.ts`

**Interfaces:** consumes the no-card `SheetPage` from Task 1.

**Layouts (spec §3.2):**
- **Certificates:**
  - grid `grid-cols-1 md:grid-cols-2 xl:grid-cols-3`;
  - each certificate uses the top-rule panel style, with no shadow;
  - the GR-14 line stays on each card.
- **Account:**
  - `lg:grid lg:grid-cols-2 lg:gap-10`: profile and password on the left; enrolment, receipts and the rest on the right;
  - the deletion "danger zone" stays full width and last;
  - no logic changes.
- **Notifications:** at `lg`, `grid-cols-[minmax(0,1fr)_320px]`, with the list left and the preferences right.

- [ ] **Step 1: write the failing test.** In `shell.spec.ts`, desktop 1280×800, with the file's learner signed in, visit `/app/account`. Assert:
  - the `h2` "Profil" (or the account page's first section heading; read the page for the exact text) and the enrolment section heading are side by side: their bounding boxes have different `x` and overlapping `y` ranges;
  - `scrollWidth <= 1280`.
- [ ] **Step 2:** Run `npm run e2e:dev -- e2e/shell.spec.ts`. It should FAIL, because the sections are stacked.
- [ ] **Step 3:** implement the three layouts.
- [ ] **Step 4:** Run `npm run e2e:dev -- e2e/shell.spec.ts e2e/smoke.spec.ts e2e/journey.spec.ts`. All should PASS.
- [ ] **Step 5:** commit with message `feat(app): learner pages use the full width`.

---

### Task 3: public info pages (spec §3.3)

**Files:**
- Modify the pages:
  - `frontend/app/(marketing)/about/page.tsx`
  - `frontend/app/(marketing)/how-it-works/page.tsx`
  - `frontend/app/(marketing)/help/page.tsx`
  - `frontend/app/(marketing)/contact/page.tsx`
  - `frontend/app/(marketing)/legal/[doc]/page.tsx`
- Modify `frontend/components/PublicShell.tsx` if a page needs bands outside the container. Add an optional `bare` prop that renders children without `SheetPage`, so a page can compose `Band`s like the program page.
- Reuse: `frontend/components/program/Band.tsx`
- Test: `frontend/e2e/landing.spec.ts`

**Layouts (spec §3.3):**
- **About and How it works:** `PublicShell bare` with `Band`s. The `h1` is in the first band (`pad="py-14 md:py-20"`), and each existing `Chapter` becomes a `Band`. Prose stays at `max-w-[65ch]`.
- **Legal:** at `lg`, `grid-cols-[220px_minmax(0,1fr)] gap-12`.
  - **Left:** `<nav aria-label="Daftar isi">`, `sticky top-24`, with links `#sec-{i}` for each section heading.
  - **Right:** the existing content at `max-w-[70ch]`.
  - The nav is `hidden lg:block`.
- **Help:** the FAQ is full width in the container.
- **Contact:** at `md`, two columns, with the form left and the details right.

- [ ] **Step 1: write the failing test.** Append to `landing.spec.ts`:

```ts
test("legal pages have a contents list on desktop and none on phones", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto("/legal/terms");
  const toc = page.getByRole("navigation", { name: "Daftar isi" });
  await expect(toc).toBeVisible();
  const first = toc.getByRole("link").first();
  const href = await first.getAttribute("href");
  await first.click();
  await expect(page.locator(href!)).toBeInViewport();
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(toc).toBeHidden();
});
```

- [ ] **Step 2:** Run `npm run e2e:dev -- e2e/landing.spec.ts`. It should FAIL, because there is no "Daftar isi" nav.
- [ ] **Step 3:** implement the five pages.
- [ ] **Step 4:** Run `npm run e2e:dev -- e2e/landing.spec.ts e2e/smoke.spec.ts`. All should PASS.
- [ ] **Step 5:** commit with message `feat(marketing): public info pages use the full width; legal contents list`.

---

### Task 4: sign-in pages, split screen (spec §3.5)

**Files:**
- Modify: `frontend/app/(auth)/layout.tsx`
- Create: `frontend/components/auth/AuthBrandPanel.tsx`
- Test: `frontend/e2e/smoke.spec.ts` (drop the auth rows from `CARD_PENDING`)

**Interfaces:**
- Produces `AuthBrandPanel()`, a server-safe component that renders:
  - `Logo variant="white"`;
  - the landing headline, as a `p`, not a heading, because the form page owns the `h1`;
  - the five path steps as a static `ol`: `Video`, `Tes sesi`, `Kelas live`, `Tes akhir`, `Sertifikat`, with the sub-labels from `components/program/JourneyAnimation.tsx`. Reuse the step data: export it from there if it isn't already.

**Layout:**
- `lg:grid lg:grid-cols-2 min-h-screen`.
- **Left:** `hidden lg:flex` `bg-ink` panel, holding the brand panel.
- **Right:** `bg-surface`, with the form centred, `w-full max-w-[400px]`.
- **Below `lg`:** the form only, on `bg-surface`.
- Keep `export const dynamic = "force-dynamic"`.

- [ ] **Step 1:** Remove `/login`, `/register` and `/forgot-password` from `CARD_PENDING`, then run `npm run e2e:dev -- e2e/smoke.spec.ts -g public`. It should FAIL the card guard on those pages.
- [ ] **Step 2:** implement the layout and the panel.
- [ ] **Step 3:** Run `npm run e2e:dev -- e2e/smoke.spec.ts e2e/harness.spec.ts e2e/shell.spec.ts`. All should PASS. These sign in through the form.
- [ ] **Step 4:** commit with message `feat(auth): split-screen sign-in without the card`.

---

### Task 5: verify, checkout success, maintenance, admin, and removing `SHEET_CLASSES` (spec §3.6)

**Files:**
- Modify:
  - `frontend/app/verify/[code]/page.tsx`
  - `frontend/app/checkout/success/SuccessClient.tsx`
  - `frontend/app/maintenance/page.tsx`
  - `frontend/app/admin/layout.tsx`
- Modify: `frontend/components/ui/Page.tsx` and `frontend/components/ui/index.ts` (delete `SHEET_CLASSES` once `grep -rn SHEET_CLASSES app components` finds nothing)
- Test: `frontend/e2e/smoke.spec.ts` (empty `CARD_PENDING`, then delete it) and `frontend/e2e/journey.spec.ts` (it checks the verify page)

**Layouts (spec §3.6):**
- **Verify:**
  - the slim header stays;
  - the main area is a full-width `bg-surface`, with content in `max-w-6xl px-6`;
  - **valid:** at `md`, two columns: the name, scores and band on the left; the verification facts and the GR-14 disclaimer on the right;
  - **not found:** a plain message in the same container;
  - all copy is unchanged.
- **Checkout success and maintenance:** a centred column, `max-w-[520px]`, directly on `bg-bg`, with no box.
- **Admin:**
  - the content area is `bg-surface` at full height;
  - pages sit in `mx-auto w-full max-w-6xl px-6 py-8`;
  - the dark sidebar is untouched.

- [ ] **Step 1:** Empty `CARD_PENDING`, then run `npm run e2e:dev -- e2e/smoke.spec.ts`. It should FAIL on verify and admin.
- [ ] **Step 2:** implement the four layouts, then delete `SHEET_CLASSES` and the `CARD_PENDING` allow-list.
- [ ] **Step 3:** Run `npm run e2e:dev`, the whole suite. All should PASS.
- [ ] **Step 4:** commit with message `feat(ui): verify, status and admin pages without the card; drop SHEET_CLASSES`.

---

### Task 6: final verification
- [ ] From `frontend/`, run `npm run lint && npx tsc --noEmit`.
- [ ] Do one production check:
  - `npm run e2e:up && npm run e2e`, all passing;
  - then `npm run e2e:down`, so the test stack doesn't keep running beside the deployed one.
- [ ] Do a browser check at 1440, 800 and 375 of:
  - `/legal/terms`, `/about`, `/login`, `/verify/<a valid code>`;
  - signed in as a learner: `/app/certificates`, `/app/account`, `/app/notifications`, one session page and the exam intro;
  - signed in as the e2e admin: `/admin`.

  Fix anything visibly off, each fix as its own commit.
