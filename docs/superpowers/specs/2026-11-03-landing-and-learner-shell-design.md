# Landing page redesign, learner sidebar, footer logo — design

**Date:** 2026-11-03.
**Status:** agreed in brainstorming (mockups shown in the visual companion), awaiting a builder check.

## 1. Why

The product owner reviewed the deployed site and raised three things:

1. **Program page wastes space and looks like cards** (`/program/toefl-preparation`, which is also the homepage).
   - The page is a centred white sheet, `SheetPage`, max 760px wide, on a tinted background.
   - The price box is a separate bordered card in a 300px right column.
   - Most of a 1900px screen is empty, and the PO does not like the card look.
2. **The footer logo is stretched.**
   - `SiteFooter` puts `<Logo />` in a `flex flex-col` column.
   - The default `align-items: stretch` widens the image to the column width, overriding `w-auto`.
3. **The learner area's menu is a top bar.** The PO wants it in a left sidebar, with the page using the freed width.

**Success:**
- A visitor sees, without scrolling:
  - what INVERTA is, in one sentence;
  - how it works, as an animation;
  - an enrol button.
- The page reads as full-width bands, not cards.
- A learner's dashboard uses the width of the screen.

## 2. Program page (`app/(marketing)/program/[slug]/page.tsx`)

### 2.1 Layout
- The page drops `SheetPage` and the card look. It becomes **full-width bands**.
- Each band's content is centred in a wide container, about `max-w-6xl`, matching `AppHeader`.
- Bands are separated by a 1px `border` rule and alternate between `bg-surface` and `bg-bg`.
- No band content sits in a bordered or shadowed box.
- **Vary the rhythm, so the page isn't a stack of identical bands:**
  - the hero is tall and roomy;
  - bands 2 and 4 are compact column rows;
  - band 3 (session list + animation B) is the widest and gets the most space;
  - band 5 (price) is narrow and centred, with the price large, since it appears on the page only once.
- **Type scale** (Plus Jakarta Sans only):
  - hero `h1`: `clamp(40px, 5vw, 64px)`, weight 800, tracking about −0.025em, max width about 16 words per line;
  - band `h2`: 28–32px, weight 800;
  - body: 16–17px.
- *(Design review, 2026-11-03.)*
- `PublicNav` and `SiteFooter` stay.
- The footer's inner container widens from `max-w-[760px]` to the same max width as the bands.
  - Its gutters are redone to match: drop `min-[820px]:px-0`.
  - This affects every public page; that's intended.
- The `EnrollCta` sticky offset (`top-[81px]`) belonged to the old sticky price column and goes away.

### 2.2 Sections, in order (PO-approved)

| # | Band | Content |
|---|---|---|
| 1 | **Hero** | Described in §2.3. |
| 2 | **Apa itu INVERTA** (product) | Four columns saying what each part *contains*, from real program data rather than repeating animation A's order: **Video materi** (`{videoCount} video · {totalDurationLabel}`) · **Tes per sesi** (wajib lulus untuk membuka sesi berikutnya) · **Kelas live** (`{liveCount} kelas live terjadwal`; column omitted if 0) · **Tes akhir** (`{ITP_TOTAL_QUESTIONS} soal · {ITP_TOTAL_MINUTES} menit · skor prediksi`). |
| 3 | **Daftar isi program** (programme) | Left: the existing session list (`Toc`/`TocItem` with `SessionMeta`/`SyllabusParts`, unchanged in behaviour). Right: **animation B, the app peek** (§2.4). |
| 4 | **Yang membedakan** (solution) | Three columns: *Tidak bisa loncat* (tests gate progress) · *Format seperti TOEFL ITP* (Listening 50 · Structure 40 · Reading 50, berwaktu) · *Hasil yang bisa dibuktikan* (certificate with a public verification code). Not "Kenapa berhasil" (an efficacy claim, legal review open) and not "Format tes asli" (reads as official, GR-14). |
| 5 | **Biaya** (price) | The price (`formatIdr(program.priceIdr)`), "Sekali bayar, bukan langganan", the five benefits from `EnrollCta`'s list, and the enrol button. |
| 6 | **FAQ** | `ProgramFaq`, unchanged. |
| 7 | **Prediction note** | The existing GR-14 `Footnote` text, verbatim. Non-negotiable. |

**Changes to existing pieces:**
- **"Tentang program ini"** (the admin-editable `program.description`) moves into band 2, as a paragraph under the heading, above the four columns. *(My call: it's the admin's own product copy, so it belongs with "what INVERTA is", and dropping it would orphan an editable field.)*
- **`HowItWorks`** (the 4-step row) is removed from this page; animation A carries that job.
  - The component has no other user, so it is deleted.
  - `/how-it-works` uses only `howItWorksSteps.ts`, which stays.
- **`ItpFormat`** is removed from this page; its numbers appear in bands 2 and 4.
  - It keeps its source of truth: the numbers must come from `frontend/lib/itpFormat.ts`, not literals, so the backend drift test still guards them.
  - `ItpFormat` has no other user, so it is deleted; `lib/itpFormat.ts` stays.
- **`ClosingCta`** is replaced by band 5.
- **The facts `<dl>`** (session, video and live counts, total material) moves into band 3's heading area as one quiet line, e.g. `8 sesi · 6 video · 1 kelas live · 1 jam 20 menit materi`. *(My call.)*

### 2.3 Hero (band 1)
Two columns on desktop, stacked on mobile.

**Left column:**
- **Eyebrow:** `Persiapan TOEFL ITP · Bahasa Indonesia`.
- **Headline (`h1`):** the product's one-sentence summary. Draft: *"Belajar TOEFL langkah demi langkah, sampai tahu prediksi skor Anda."*
  - The page's only `h1`.
  - Not `program.name`; the name moves to the `<title>` and JSON-LD only, which they already use.
- **Sub-header:** draft *"Video materi, tes singkat di setiap sesi, kelas live bersama pengajar, lalu simulasi tes akhir berformat ITP dengan sertifikat prediksi skor."*
  - If `program.summary` is set, use it here instead of the draft, so admins can still edit it.
- **Buttons:** primary **"Daftar sekarang"** (the existing `EnrollCta` button behaviour), then a secondary link "Lihat isi program" that scrolls to band 3, the session list.
- **Small note under the buttons:** `Sekali bayar · akses seluruh program`.

**Right column: animation A, "the path".**
- Five steps on a horizontal track: **Video** (materi) → **Tes sesi** (lulus = lanjut) → **Kelas live** (bersama pengajar) → **Tes akhir** (format ITP) → **Sertifikat** (prediksi skor).
- A line fills left to right, and each step's dot lights in turn.
- It **plays once** on load, about 4.5s, then rests with every step lit. It does not loop (§2.6).
- **Phones (under about 560px):** the track goes vertical, top to bottom, so the labels stay readable.

The approved mockup is `.superpowers/brainstorm/97060-1793706232/content/hero-options.html`, option A.

### 2.4 Animation B, the app peek (band 3)
A stylised app window, not a screenshot. **It starts when it scrolls into view and plays its three scenes once**, about 3s each, then rests on scene 3. A small **"Putar ulang"** (replay) button under it restarts it.

The scenes:
1. A lesson video "plays", with a progress bar filling. Caption: a real session title from `sessions`, or a generic one.
2. A test question with the chosen answer highlighted, then a "Lulus · Sesi berikutnya terbuka" pill.
3. A certificate showing a predicted score. **The score is illustrative** and must be labelled on the mock itself with *"Contoh. Prediksi INVERTA, bukan skor TOEFL resmi"* (GR-14).

### 2.5 "Always show the enrol button"
- The hero carries the enrol button.
- **Label:** the enrol button reads **"Daftar sekarang"** for every visitor, signed in or not.
  - Today `EnrollCta` shows "Masuk & daftar" to anonymous visitors.
  - Clicking as an anonymous visitor still goes to login and back, as today.
  - *(My call: one label everywhere, so the hero, nav and band 5 buttons match.)*
- `PublicNav`'s primary button changes from "Daftar gratis" (→ `/register`) to **"Daftar sekarang"** on the program page.
  - `PublicNav` takes an optional `programId` prop. When it is given, the nav shows the enrol button.
  - A compact, non-full-width size of the `EnrollCta` button is fine.
  - **e2e:** `journey.spec.ts` already uses `.first()`. Scope its locator to the hero, e.g. via `main`, so it never depends on the nav/hero order.
  - It reuses `EnrollCta`'s logic: an anonymous visitor goes to login and returns, and an unverified one goes to `/verify-email`.
  - *(My call: the PO asked for the enrol button to always be visible; "Daftar gratis" today creates an account, which is a different action.)*
  - On other public pages the nav keeps "Daftar gratis".
- `PublicNav` is already sticky (`sticky top-0`), so the nav button stays visible while scrolling.
- Band 5 repeats the button.
- The phone-only duplicate `EnrollCta` above "Tentang program ini" goes away; the hero button covers it.

### 2.6 Animation rules (both A and B)
- **No infinite loops.** WCAG 2.2.2 (Pause, Stop, Hide, Level A) requires a way to pause motion that auto-starts and lasts over 5s.
  - A finishes in 5s or less.
  - B auto-plays once, about 9s, so it needs its own stop: its "Putar ulang" button doubles as **"Jeda"** (pause) while it is playing.
  - Two loops running beside reading content also make the page feel busy; each playing once lands harder.
- **CSS keyframes for the motion.** The only JavaScript is in B: an `IntersectionObserver` that starts it, and the pause/replay button. No animation library is added.
- **Reduced motion** (`prefers-reduced-motion: reduce`): both show a static final state.
  - A: all five steps lit.
  - B: scene 3 (the certificate), with no replay button.
- The animations are decorative:
  - wrap them `aria-hidden`;
  - give each a visually hidden text equivalent, e.g. A: "Alur program: video, tes sesi, kelas live, tes akhir, sertifikat".
- No layout shift; fixed heights.

## 3. Learner sidebar

### 3.1 Which pages
These five pages currently render `AppHeader`, a top bar; they switch to the sidebar shell:
- `/app/dashboard`
- `/app/certificates`
- `/app/account`
- `/app/notifications`
- `/app/program/[id]`

**How it's built:**
- There is no shared `app/app/layout.tsx`; each of the five pages renders `AppHeader` itself.
- The sidebar becomes one `LearnerShell` component, wrapping each page's content.
- It replaces the `<AppHeader />` call in those five pages; no route folders move.
- `AppHeader` is deleted if nothing else uses it.
- *(My call: smallest change, no route-group move.)*

**Unchanged:**
- `/app/learn/[id]` (dormant archived product, own inline header).
- The session page, `/app/session/[id]`, keeps its current header.
- The final-exam screens keep their current header, with no sidebar (no distractions during a sitting).

**Notification bell:**
- `AppHeader`'s `NotificationBell` (unread dropdown, 60s poll) does not move into the sidebar.
- **Desktop:** the "Notifikasi" menu item shows an unread-count badge from the same data.
- **Mobile:** the top bar keeps the bell next to the menu button.
- *(My call.)*

**Bantuan:**
- "Bantuan" links to the public `/help` page, as it does today.
- That page has the public nav, not the sidebar.
- *(My call: unchanged behaviour.)*

### 3.2 Sidebar
- **Width:** about 220px, white `bg-surface`, right border, full height, sticky.
- **Top:** the colour `Logo`, linking to `/app/dashboard`.
- **Menu items:** Dasbor · Sertifikat · Notifikasi · Profil & tagihan · Bantuan.
  - Active item: `bg-primary-soft text-primary`.
  - **Admin link:** for admin roles, the existing admin link is added at the end of the menu.
- **Bottom:** the account button, showing initials and name, with the menu the account button in `AppHeader` has today (incl. "Keluar").
  - Its accessible name stays **"Menu akun {initials}"**, which the e2e and axe checks rely on.
- **Onboarding tour:** the `data-tour="nav-program"` and `data-tour="nav-certificates"` anchors move to the sidebar items.
  - The tour (`OnboardingFlow`) must still find them on desktop.
  - On mobile, the two nav steps anchor to the menu button instead, so phone users still get a tour.
    - `OnboardingFlow` drops missing anchors, and marks the tour done if none are left.
    - *(My call.)*
- **Phones (under `md`):** the sidebar is replaced by a slim top bar with the logo and a menu button. The button opens the same menu as a drawer.
  - The drawer has a focus trap, Escape closes it, and it closes on route change.
  - No horizontal page scroll at 375px; the e2e smoke suite checks this.
- **Admin's dark sidebar** (`app/admin/layout.tsx`) is **not changed**.

### 3.3 Dashboard layout (`/app/dashboard`)
The content area is wide, up to about `max-w-6xl`, with no white sheet card. It has two columns on desktop and one on mobile.

**Main column:**
- the page title "Selamat datang kembali, {name}";
- the progress line and bar (`ProgressHeader`);
- the session list (`Toc`/`TocItem`, unchanged behaviour: states, links, locked rows).

**Right column (about 280px), top to bottom:**
1. **"Berikutnya" panel:** the next session's title and a "Lanjutkan belajar" button to `nextHref(next)`. Emphasised with dark `bg-ink`.
   - Hidden when there is no next session, i.e. the program is complete.
2. **Session-test history** (`SessionTestHistory`).
3. **Final result and certificates** (`FinalResultCard`, `CertificateCards`).

**Mobile:** the right column stacks below the main column, except "Berikutnya", which goes first.

**Other states keep their current behaviour, placed in the main column:**
- pending payment notice;
- empty state;
- error states.

**The other four sidebar pages:** same shell; their content keeps its current design, placed in the content area at a sensible width. *(Builder's call; they don't need two columns.)*

## 4. Footer logo
- **Fix:** stop the flex column stretching the image: `self-start` on the logo, or `items-start` on the column.
- **Scope:** check every other `<Logo />` placement (`PublicNav`, `AppHeader`, admin sidebar, the new learner sidebar) for the same stretch.

## 5. Constraints
- Light mode only.
- Bahasa Indonesia copy.
- WCAG 2.1 AA:
  - the existing axe suite (`frontend/e2e`) must stay at zero serious or critical violations;
  - one `h1` per page;
  - no console errors;
  - no horizontal scroll at 375px.
- Design tokens only (`globals.css`), Plus Jakarta Sans.
- No new dependencies.
- GR-14 wording stays wherever a score appears, including the illustrative score in animation B.
- Golden rules:
  - **GR-4:** no business logic in Next.js; this is presentation only.
  - **GR-2:** the enrol button still only starts checkout; it never grants access.
- **E2E suite:**
  - `smoke.spec.ts` and `journey.spec.ts` must pass after the change. Update their selectors only where the UI they target genuinely moved, e.g. nav links now in the sidebar.
  - `journey.spec.ts` clicks "Daftar sekarang" on the program page. With the button in both the nav and the hero, the test's locator must stay unambiguous.

## 6. Non-goals
- No change to `/how-it-works`, `/about` or the other marketing pages beyond the shared nav/footer changes in §2.5 and §4.
- No change to the admin area.
- No new copy-management backend; the hero copy is in code, except `program.summary`, which overrides the sub-header.
- No dark mode.
- No visual-regression tooling.

## 7. Left to the builder
- Exact spacing, type sizes and breakpoints within the tokens.
- The animation timing curves.
- Icons for the sidebar items, from `components/ui/icons.tsx` where a fitting one exists; otherwise no icon. No icon library is added.
- How the other four sidebar pages fill the wider area.
