# Landing redesign + learner sidebar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:**
- Rebuild the program page as full-width bands, with two CSS animations.
- Move the learner menu into a left sidebar and widen the dashboard into two columns.
- Fix the stretched footer logo.

**Architecture:** Presentation-only changes in `frontend/`. No backend or API change.
- The program page becomes a server component, composed of band sections and two new client animation components.
- A new `LearnerShell` client component replaces `AppHeader` on the five learner pages that use it.
- Tests are Playwright e2e specs; the repo has no frontend unit-test runner. They run against the isolated `inverta-e2e` stack.

**Tech Stack:**
- Next.js 15 App Router
- Tailwind v4 tokens in `app/globals.css`
- TanStack Query
- Playwright + `@axe-core/playwright`

**Spec:** `docs/superpowers/specs/2026-11-03-landing-and-learner-shell-design.md`.
Read it before starting. §-references below point into it.

## Global Constraints
- **Copy:**
  - Bahasa Indonesia.
  - Light mode only.
  - Font: Plus Jakarta Sans.
  - Design tokens from `app/globals.css` only. No raw hex, except the AA hexes already used in the codebase (`#166534`, `#92400E`).
- **No new npm dependencies.** Animations are CSS keyframes.
  - **No infinite loops** (WCAG 2.2.2).
  - The only JavaScript is B's `IntersectionObserver` and its pause/replay button.
- **Type scale:**
  - hero `h1`: `clamp(40px, 5vw, 64px)`, weight 800, tracking `-0.025em`, `max-w-[16ch]`-ish;
  - band `h2`: 28–32px, weight 800;
  - body: 16–17px.
- **Copy, verbatim:**
  - band 4's title is `Yang membedakan`; never "Kenapa berhasil";
  - its column is `Format seperti TOEFL ITP`; never "Format tes asli".
- **WCAG 2.1 AA** (the axe suite must stay at zero serious or critical violations):
  - exactly one `h1` per page;
  - no console errors;
  - no horizontal scroll at 375px.
- **GR-14:** the footnote text on the program page stays verbatim. Any shown score is labelled *"Contoh. Prediksi INVERTA, bukan skor TOEFL resmi"*.
- **GR-2:** the enrol button only starts checkout via the existing `EnrollCta.onClick` logic; nothing else changes access.
- **GR-4:** no business logic in Next.js.
- **ITP numbers:** shown numbers come from `lib/itpFormat.ts` (`ITP_FORMAT`, `ITP_TOTAL_QUESTIONS`, `ITP_TOTAL_MINUTES`), never literals. The backend `LandingContentTests` reads that file.
- **Untouched:** the session page `/app/session/[id]`, the exam screens `/app/assessment/*` and `/app/exam/*`, the dormant `/app/learn/[id]`, and the admin area.
- **Running the e2e suite:**
  1. From `frontend/`: `npm run e2e:up` (fresh stack, about 3 minutes).
  2. `npm run e2e -- <file>`.
  3. `npm run e2e:down` when finished with the plan.
- **Each task ends with all three of:** `npm run lint`, `npx tsc --noEmit`, and the task's e2e file passing.

## Review Focus
1. **Long names and titles at 375px.** A learner named "Muhammad Rizky Pratama Wibowo", or a 70-character session title, must truncate or wrap in the sidebar and drawer, never push the page wide. Covered in Task 5 by the "long name does not overflow" test.
2. **Program content gaps.** A program with no `summary`, no `description`, no live sessions or no FAQ items must render with no empty heading and no stray "0". Covered in Task 4: the hero falls back to the draft sub-header, and bands skip empty parts.
3. **Finished program.** A learner with no next session must not see an empty "Berikutnya" box. Covered in Task 6 by the `nextSessionId === null` branch, asserted on a learner whose program is complete (journey step 7).
4. **Reduced motion.** With `prefers-reduced-motion: reduce`, both animations show a static final state with zero running animations. Covered in Task 3's test.
5. **Drawer focus.** Opening the mobile menu, pressing Escape or following a link must close it and return focus to the menu button, with no focus lost to the hidden page. Covered in Task 5's drawer test.

---

### Task 1: Footer logo + footer width

**Files:**
- Modify: `frontend/components/SiteFooter.tsx` (logo wrapper; container `max-w-[760px] … min-[820px]:px-0`)
- Create: `frontend/e2e/landing.spec.ts`

**Interfaces:**
- Produces: `e2e/landing.spec.ts`, which later tasks append to.
  - Use `test`/`expect` from `./support/learner` (the cookie-consent fixture).
  - `const SLUG = "toefl-preparation"`.

- [ ] **Step 1: Write the failing test** in `e2e/landing.spec.ts`:

```ts
test("footer logo keeps its aspect ratio", async ({ page }) => {
  await page.goto(`/program/${SLUG}`);
  const img = page.locator("footer img[alt='INVERTA']");
  const { w, h, nw, nh } = await img.evaluate((el: HTMLImageElement) =>
    ({ w: el.clientWidth, h: el.clientHeight, nw: el.naturalWidth, nh: el.naturalHeight }));
  expect(Math.abs(w / h - nw / nh)).toBeLessThan(0.05);
});
```

- [ ] **Step 2:** Run `npm run e2e -- e2e/landing.spec.ts`. It should FAIL, because the rendered ratio is wider than 720/214.

- [ ] **Step 3: Fix the logo and widen the footer.**
  - Add `self-start` on the footer `<Logo />` via its `className`.
  - Change the footer container to `max-w-6xl px-6`, the same gutter as `PublicNav`'s `px-6`.
  - Grep every `<Logo` use (`PublicNav`, `AppHeader`, `app/admin/layout.tsx`). Any other one inside a `flex-col` parent gets `self-start` too.

- [ ] **Step 4:** Run the test. It should PASS. Also run `npm run e2e -- e2e/smoke.spec.ts -g public`; it should PASS with no overflow.

- [ ] **Step 5: Commit:** `fix(ui): stop the footer stretching the logo and widen the footer`.

---

### Task 2: One enrol button label, compact size, enrol in the public nav

**Files:**
- Modify: `frontend/components/program/EnrollCta.tsx`
- Modify: `frontend/components/PublicNav.tsx`
- Modify: `frontend/e2e/journey.spec.ts:38-39`
- Test: `frontend/e2e/landing.spec.ts`

**Interfaces:**
- Produces: `EnrollCta({ programId, priceIdr?, className?, size? })`.
  - `size?: "md" | "sm"` (default `"md"`).
  - `"sm"` renders a non-full-width compact button, matching `PublicNav`'s `ctaBase` height `h-9`.
  - The label is always `"Daftar sekarang"`.
- Produces: `PublicNav({ className?, programId? })`.
  - When `programId` is set and the visitor is **not** authenticated, the primary nav button is `<EnrollCta programId={programId} size="sm" />` instead of the `Daftar gratis` link.
  - Signed-in visitors keep `Dasbor` and the account menu, plus the same compact `EnrollCta`.

- [ ] **Step 1: Write the failing test** (append to `landing.spec.ts`):

```ts
test("anonymous visitor sees one enrol label in nav and hero, nav enrol goes to login", async ({ page }) => {
  await page.goto(`/program/${SLUG}`);
  await expect(page.getByRole("button", { name: "Masuk & daftar" })).toHaveCount(0);
  await expect(page.getByRole("banner").getByRole("button", { name: "Daftar sekarang" })).toBeVisible();
  await page.getByRole("banner").getByRole("button", { name: "Daftar sekarang" }).click();
  await page.waitForURL(/\/login\?next=%2Fprogram%2Ftoefl-preparation/);
});
test("other public pages keep Daftar gratis", async ({ page }) => {
  await page.goto("/about");
  await expect(page.getByRole("banner").getByRole("link", { name: "Daftar gratis" })).toBeVisible();
});
```

- [ ] **Step 2:** Run `npm run e2e -- e2e/landing.spec.ts`. The first test should FAIL.

- [ ] **Step 3: Implement** the two prop changes above.
  - Pass `programId={program.id}` to `<PublicNav>` in `app/(marketing)/program/[slug]/page.tsx`. That page's full rewrite comes in Task 4; only add the prop now.
  - Change `journey.spec.ts` lines 38–39 to scope the locator to the page body: `page.getByRole("main").getByRole("button", { name: "Daftar sekarang" }).first()`.

- [ ] **Step 4:** Run `landing.spec.ts`; it should PASS. Run `journey.spec.ts`; it should PASS.

- [ ] **Step 5: Commit:** `feat(landing): one enrol label everywhere and an enrol button in the program nav`.

---

### Task 3: Animations A (the path) and B (the app peek)

**Files:**
- Create: `frontend/components/program/JourneyAnimation.tsx`
- Create: `frontend/components/program/AppPeekAnimation.tsx`
- Modify: `frontend/app/globals.css` (keyframes + a `prefers-reduced-motion` block next to the existing one at line ~183)
- Test: `frontend/e2e/landing.spec.ts`

**Interfaces:**
- Produces: `JourneyAnimation(): JSX.Element`. Server-safe, no props.
- Produces: `AppPeekAnimation({ lessonTitle }: { lessonTitle?: string }): JSX.Element`. A `"use client"` component.
  - An `IntersectionObserver` adds a `data-playing` attribute the first time the figure is about 40% visible.
  - The scenes run once (about 9s total), then rest on scene 3.
  - One button outside the `aria-hidden` part: it reads `Jeda` while playing (sets `animation-play-state: paused`) and `Putar ulang` otherwise (restarts by toggling `data-playing` off and on).
- Both render a root `<figure data-anim="journey" | "peek">`:
  - the visual part has `aria-hidden="true"`;
  - a sibling `<figcaption className="sr-only">` holds the text equivalent.

**Content (spec §2.3, §2.4):**
- **A's five steps**, as label / sub-label:
  1. `Video` / `materi`
  2. `Tes sesi` / `lulus = lanjut`
  3. `Kelas live` / `bersama pengajar`
  4. `Tes akhir` / `format ITP`
  5. `Sertifikat` / `prediksi skor`

  **Plays once** on load, total ≤ 4.5s (`animation-iteration-count: 1`, `animation-fill-mode: forwards`), then rests fully lit. The track fills and each dot lights in turn. Under 560px the track goes vertical. sr-only text: `Alur program: video, tes sesi, kelas live, tes akhir, sertifikat.`
- **B's three scenes**, about 3s each:
  1. A video with a filling bar and the caption `lessonTitle ?? "Video materi"`.
  2. A question with the chosen option highlighted, then a pill `Lulus · Sesi berikutnya terbuka`.
  3. A certificate with an illustrative score `520` and `Listening 51 · Structure 53 · Reading 52`, plus the visible label `Contoh. Prediksi INVERTA, bukan skor TOEFL resmi`.

  sr-only text: `Contoh tampilan aplikasi: video materi, tes sesi yang lulus, lalu sertifikat prediksi skor.`
- **Visual reference:** `.superpowers/brainstorm/97060-1793706232/content/hero-options.html`, options A and B. Port the shapes to tokens and Tailwind; don't copy the inline hexes.

- [ ] **Step 1: Write the failing test** (append to `landing.spec.ts`):

```ts
const running = (el: Element) => el.getAnimations({ subtree: true }).filter((a) => a.playState === "running").length;

test("animations have text equivalents and stop under reduced motion", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto(`/program/${SLUG}`);
  for (const name of ["journey", "peek"]) {
    const fig = page.locator(`figure[data-anim="${name}"]`);
    await expect(fig.locator("figcaption.sr-only")).not.toBeEmpty();
    expect(await fig.evaluate(running)).toBe(0);
  }
  await expect(page.locator('figure[data-anim="peek"]')).toContainText("bukan skor TOEFL resmi");
});

test("the path plays once and stops; the peek plays on view, pauses and replays", async ({ page }) => {
  await page.goto(`/program/${SLUG}`);
  const journey = page.locator('figure[data-anim="journey"]');
  await expect.poll(() => journey.evaluate(running), { timeout: 6_000 }).toBe(0); // WCAG 2.2.2: ≤ 5s, no loop
  const peek = page.locator('figure[data-anim="peek"]');
  await peek.scrollIntoViewIfNeeded();
  await expect(peek.getByRole("button", { name: "Jeda" })).toBeVisible();
  await peek.getByRole("button", { name: "Jeda" }).click();
  expect(await peek.evaluate(running)).toBe(0);
  await expect.poll(() => peek.getByRole("button", { name: "Putar ulang" }).count(), { timeout: 12_000 }).toBe(1);
});
```

- [ ] **Step 2:** Run it. It should FAIL, because there are no figures yet.

- [ ] **Step 3: Implement both components and their CSS.**
  - Temporarily mount both at the end of the existing program page, so the test can see them. Task 4 places them properly.
  - Fixed heights so there is no layout shift.
  - Under reduced motion:
    - A shows all five dots lit and the track full.
    - B shows scene 3 (the certificate) and renders no button.
  - The pause-then-replay test expects that after `Jeda`, the button becomes `Putar ulang` once paused or finished.
    - Simplest rule: the label is `Jeda` only while playing *and not paused*.

- [ ] **Step 4:** Run `landing.spec.ts`; it should PASS. Run `smoke.spec.ts -g public`; it should PASS, with axe clean and no overflow at 375px.

- [ ] **Step 5: Commit:** `feat(landing): path and app-peek animations, static under reduced motion`.

---

### Task 4: Program page as full-width bands

**Files:**
- Rewrite: `frontend/app/(marketing)/program/[slug]/page.tsx` (keep `generateMetadata`, `generateStaticParams`, `revalidate`, `SessionMeta`, `ProgramJsonLd`)
- Create: `frontend/components/program/Band.tsx`
- Delete: `frontend/components/program/HowItWorks.tsx`, `ItpFormat.tsx`, `ClosingCta.tsx`. They have no other users; `/how-it-works` imports only `howItWorksSteps.ts`, which stays.
- Test: `frontend/e2e/landing.spec.ts`

**Interfaces:**
- Consumes: `EnrollCta` with the `size` prop (Task 2), `PublicNav` with `programId` (Task 2), `JourneyAnimation` and `AppPeekAnimation` (Task 3).
- Produces: `Band({ id?, title?, eyebrow?, tint?, children })`.
  - Renders `<section aria-labelledby>`, full width, with a top border and `bg-surface`, or `bg-bg` when `tint`.
  - Inner container: `mx-auto max-w-6xl px-6`.
  - `title` renders as `h2`.

**Band contents and order:** spec §2.2, verbatim.
- **Hero** (§2.3):
  - `h1` = `Belajar TOEFL langkah demi langkah, sampai tahu prediksi skor Anda.`
  - Sub-header = `program.summary ?? "Video materi, tes singkat di setiap sesi, kelas live bersama pengajar, lalu simulasi tes akhir berformat ITP dengan sertifikat prediksi skor."`
  - Buttons: `EnrollCta`, then a `Lihat isi program` link to `#daftar-isi` (band 3's id).
  - Note: `Sekali bayar · akses seluruh program`.
- **Band 2** (`id="apa-itu"`, title `Apa itu INVERTA`):
  - `program.description` as a paragraph, if non-empty;
  - then four columns saying what each part *contains*, from real data (spec §2.2 row 2). They don't repeat animation A's order:
    1. `Video materi`: `{videoCount} video · {totalDurationLabel(program.totalDurationSeconds)}`.
    2. `Tes per sesi`: `Wajib lulus untuk membuka sesi berikutnya`.
    3. `Kelas live`: `{liveCount} kelas live terjadwal`. Omit this column when `liveCount === 0`.
    4. `Tes akhir`: `{ITP_TOTAL_QUESTIONS} soal · {ITP_TOTAL_MINUTES} menit · skor prediksi`.
- **Band 3** (title `Daftar isi program`):
  - the facts line, e.g. `8 sesi · 6 video · 1 kelas live · 1 jam 20 menit materi`; omit zero-count parts;
  - the session `Toc` (unchanged props);
  - `AppPeekAnimation`, given the first Video session's title.
- **Band 4** (`Yang membedakan`): three columns: `Tidak bisa loncat` · `Format seperti TOEFL ITP` · `Hasil yang bisa dibuktikan`. ITP numbers come from `ITP_FORMAT`.
- **Rhythm and type** (spec §2.1):
  - the hero is tall, e.g. `py-16 md:py-24`;
  - bands 2 and 4 are compact;
  - band 3 is the widest;
  - band 5 is narrow and centred, e.g. `max-w-xl text-center`, with the price at hero-scale weight;
  - the hero `h1` and band `h2`s use the type scale in Global Constraints.
- **Band 5** (`Biaya`): `formatIdr(program.priceIdr)`, `Sekali bayar, bukan langganan`, the five benefits, and `EnrollCta`.
  - Move the `BENEFITS` array out of `EnrollCta` into an export of `components/program/EnrollCta.tsx`.
  - Drop `EnrollCta`'s price-box branch and the `top-[81px]` sticky offset.
- **Then:** `ProgramFaq` and the GR-14 `Footnote`, verbatim.
- **No cards:** no bordered or shadowed box around band content.

- [ ] **Step 1: Write the failing test** (append to `landing.spec.ts`):

```ts
test("bands in order, hero enrol above the fold on desktop and phone", async ({ page }) => {
  for (const size of [{ width: 1280, height: 720 }, { width: 375, height: 812 }]) {
    await page.setViewportSize(size);
    await page.goto(`/program/${SLUG}`);
    await expect(page.locator("h1")).toHaveText("Belajar TOEFL langkah demi langkah, sampai tahu prediksi skor Anda.");
    await expect(page.getByRole("main").getByRole("button", { name: "Daftar sekarang" }).first()).toBeInViewport();
  }
  const h2 = await page.locator("main h2").allInnerTexts();
  expect(h2.slice(0, 5)).toEqual(["Apa itu INVERTA", "Daftar isi program", "Yang membedakan", "Biaya", "Pertanyaan umum"]);
  await expect(page.getByRole("main")).not.toContainText(/Kenapa berhasil|tes asli/);
  await page.getByRole("link", { name: "Lihat isi program" }).click();
  await expect(page.getByRole("list", { name: "Daftar sesi" })).toBeInViewport();
  await expect(page.getByRole("main")).toContainText("Rp");
  await expect(page.getByRole("main")).toContainText("bukan skor TOEFL resmi");
  await expect(page.getByRole("list", { name: "Daftar sesi" }).getByRole("listitem")).toHaveCount(8);
});
```

- [ ] **Step 2:** Run it. It should FAIL on the `h1` text.

- [ ] **Step 3: Implement the page, `Band` and the deletions.**
  - Remove the temporary Task 3 mounts.
  - The seeded program has a summary. If the hero sub-header shows the admin summary rather than the draft, that is correct per spec §2.3.

- [ ] **Step 4:** Run `landing.spec.ts`; it should PASS. Run `smoke.spec.ts -g public`; it should PASS. Run `journey.spec.ts`; it should PASS.

- [ ] **Step 5: Commit:** `feat(landing): program page as full-width bands, no cards`.

---

### Task 5: LearnerShell, the sidebar and mobile drawer

**Files:**
- Create: `frontend/components/app/LearnerShell.tsx`
- Modify: the five pages that render `<AppHeader />`:
  - `app/app/dashboard/page.tsx`
  - `app/app/certificates/page.tsx`
  - `app/app/account/page.tsx`
  - `app/app/notifications/page.tsx`
  - `app/app/program/[id]/page.tsx`
- Modify: `frontend/components/onboarding/OnboardingFlow.tsx` (step selectors)
- Modify: `frontend/components/app/NotificationBell.tsx` (export the unread query as a hook)
- Delete: `frontend/components/app/AppHeader.tsx`, once nothing imports it.
- Create: `frontend/e2e/shell.spec.ts`

**Interfaces:**
- Produces: `LearnerShell({ children }: { children: ReactNode }): JSX.Element`.
  - Renders the sidebar, or the mobile bar + drawer, plus `<main className="min-w-0 …">{children}</main>`.
  - Pages put their content inside, and drop their own `min-h-screen bg-bg` wrapper and `<AppHeader />`.
  - Pages keep `SheetPage` for now. Dashboard changes in Task 6.
- Produces: `useUnreadCount(): number` from `NotificationBell.tsx`. Same query key, `["notifications-bell"]`, so the cache is shared.

**Sidebar (spec §3.2):**
- `<nav aria-label="Menu utama">` with links Dasbor `/app/dashboard`, Sertifikat `/app/certificates`, Notifikasi `/app/notifications`, Profil & tagihan `/app/account`, Bantuan `/help`.
  - Active link: `aria-current="page"`, using `AppHeader`'s rule (`path === href || path.startsWith(href)`). Dasbor is also active on `/app/program/*`.
  - Admin link (`WrenchIcon`, "Admin") for `isAdminRole(user.role)`.
  - `data-tour="nav-program"` on Dasbor, `data-tour="nav-certificates"` on Sertifikat.
  - Unread badge on Notifikasi when `useUnreadCount() > 0`, showing `9+` above 9, with an accessible label like `Notifikasi, 3 belum dibaca`.
- **Bottom:** the existing `<AccountMenu initials>` plus the user's name, truncated. The button's accessible name stays `Menu akun {initials}`.
- **Desktop** (`md` and up): `md:grid md:grid-cols-[220px_minmax(0,1fr)]`; the sidebar is `sticky top-0 h-screen`.
- **Below `md`:**
  - a top bar: the logo, `<NotificationBell />`, and a button `aria-label="Buka menu"` with `aria-expanded` and `aria-controls`;
  - the drawer is a native `<dialog aria-label="Menu">` opened with `showModal()`, containing the same nav;
    - the browser then traps focus, closes on Escape and returns focus to the button;
    - `components/ui/Modal.tsx` has no focus trap, so don't reuse it;
  - close it on a backdrop click (`e.target === dialogEl`) and on a route change (`usePathname` effect).
- **Onboarding tour** (`OnboardingFlow`): each nav step's `element` becomes a selector that matches the *visible* anchor. On mobile, the two nav steps target the `Buka menu` button: give it `data-tour="nav-menu"` and pick `'[data-tour="nav-program"]'` only if `offsetParent !== null`, else `nav-menu`.

- [ ] **Step 1: Write the failing tests** in `e2e/shell.spec.ts`. Use `newLearner`/`signIn` from `support/learner`; one learner for the file, in `beforeAll`.

```ts
test("desktop: sidebar nav with active item, no top menu", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await signIn(page, l.email, l.password);
  const nav = page.getByRole("navigation", { name: "Menu utama" });
  await expect(nav.getByRole("link", { name: "Dasbor" })).toHaveAttribute("aria-current", "page");
  await nav.getByRole("link", { name: "Sertifikat" }).click();
  await expect(nav.getByRole("link", { name: "Sertifikat" })).toHaveAttribute("aria-current", "page");
  await expect(page.getByRole("button", { name: /^Menu akun / })).toBeVisible();
});
test("phone: drawer opens, Escape closes and returns focus", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(page, l.email, l.password);
  const open = page.getByRole("button", { name: "Buka menu" });
  await open.click();
  await expect(page.getByRole("dialog", { name: "Menu" })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog", { name: "Menu" })).toHaveCount(0);
  await expect(open).toBeFocused();
  await open.click();
  await page.getByRole("dialog", { name: "Menu" }).getByRole("link", { name: "Sertifikat" }).click();
  await page.waitForURL("**/app/certificates");
  await expect(page.getByRole("dialog", { name: "Menu" })).toHaveCount(0);
});
test("long name does not overflow at 375px", async ({ page }) => {
  const long = await newLearner("Muhammad Rizky Pratama Wibowo Santoso");
  await page.setViewportSize({ width: 375, height: 812 });
  await signIn(page, long.email, long.password);
  await page.getByRole("button", { name: "Buka menu" }).click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(375);
});
```

  - Add an optional `name` parameter to `newLearner(name = "Siswa E2E")` in `e2e/support/learner.ts`.

- [ ] **Step 2:** Run `npm run e2e -- e2e/shell.spec.ts`. It should FAIL, since there is no "Menu utama" nav.

- [ ] **Step 3: Implement** `LearnerShell` and `useUnreadCount`, change the 5 pages, update the tour, and delete `AppHeader.tsx`. Confirm `grep -rn AppHeader app components` returns nothing.

- [ ] **Step 4:** Run `shell.spec.ts`; it should PASS. Run `smoke.spec.ts -g learner`; it should PASS (axe, one `h1`, no overflow on all 4 learner pages). Run `harness.spec.ts`; it should PASS.

- [ ] **Step 5: Commit:** `feat(app): learner sidebar shell with a mobile drawer`.

---

### Task 6: Dashboard in two columns with "Berikutnya"

**Files:**
- Modify: `frontend/app/app/dashboard/page.tsx` (`ProgramDashboard` and the outer layout)
- Create: `frontend/components/dashboard/NextUpPanel.tsx`
- Test: `frontend/e2e/journey.spec.ts` (add assertions to existing steps)

**Interfaces:**
- Consumes: `LearnerShell` (Task 5); `nextHref(session, programId)` from `components/dashboard/ProgressHeader.tsx`.
- Produces: `NextUpPanel({ title, href }: { title: string; href: string })`.
  - A dark `bg-ink` panel with the eyebrow `Berikutnya`, the session title, and a link styled as the primary button: `Lanjutkan belajar`.

**Layout (spec §3.3):**
- The dashboard drops `SheetPage`. Content sits in `mx-auto max-w-6xl px-4 py-6 sm:px-8`.
- The title, then the pending-payment, empty and error states stay in the main flow, as today.
- `ProgramDashboard` renders `lg:grid lg:grid-cols-[minmax(0,1fr)_280px] lg:gap-8`.
  - **Main column:** `ProgressHeader` and the `Toc`.
  - **Aside**, in this order:
    1. `NextUpPanel`, only when `next` exists;
    2. `SessionTestHistory`;
    3. `FinalResultCard`;
    4. `CertificateCards`.
- **Below `lg`:** `NextUpPanel` renders first, above `ProgressHeader`. Render it once and move it with `order-*` classes; don't duplicate the DOM.
- No white sheet card. Panels use the band styling: a top rule, no shadow.

- [ ] **Step 1: Write the failing assertions** in `journey.spec.ts`:
  - **End of step 1**, after enrolment:

    ```ts
    await gotoReady(page, "/app/dashboard", page.getByText("Berikutnya"));
    await expect(page.getByRole("link", { name: "Lanjutkan belajar" })).toHaveAttribute("href", new RegExp(ids[0]));
    ```

  - **In the certificate step**, after the final assessment, when the program is complete:

    ```ts
    await gotoReady(page, "/app/dashboard", page.getByRole("list", { name: "Daftar sesi" }));
    await expect(page.getByText("Berikutnya")).toHaveCount(0);
    ```

- [ ] **Step 2:** Run `journey.spec.ts`. It should FAIL at "Berikutnya".

- [ ] **Step 3: Implement** `NextUpPanel` and the layout.

- [ ] **Step 4:** Run `journey.spec.ts`; it should PASS. Run `smoke.spec.ts`; it should PASS (all projects, including mobile 375). Run `shell.spec.ts`; it should PASS.

- [ ] **Step 5: Commit:** `feat(dashboard): two-column dashboard with a next-up panel`.

---

### Task 7: Full verification

- [ ] **Step 1:** From `frontend/`, run `npm run lint && npx tsc --noEmit && npm run build`. All should pass.
- [ ] **Step 2:** Run `npm run e2e` (the whole suite, both projects). Every test should PASS with no flaky retries.
- [ ] **Step 3:** Run `dotnet test backend/Academy.slnx --filter LandingContentTests`. It should PASS, confirming `itpFormat.ts` is untouched in shape.
- [ ] **Step 4: Browser check** on `http://localhost:3100`, at 1440px and at 375px:
  - `/program/toefl-preparation`;
  - signed in as a learner: `/app/dashboard`, `/app/certificates`, `/app/account`;
  - with reduced motion emulated, on the program page.

  Compare against the mockups in `.superpowers/brainstorm/97060-1793706232/content/`.
  - Fix anything visibly off: wasted space, stretched images, overlap.
  - Each fix is its own commit.
- [ ] **Step 5:** Run `npm run e2e:down`.
