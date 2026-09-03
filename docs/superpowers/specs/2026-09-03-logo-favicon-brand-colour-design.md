# Logo, favicon and brand colour — design

**Date:** 2026-09-03
**Status:** approved, ready for planning
**Sub-project 1 of 5** — see §8 for the others.

---

## 1. Why

The app has no logo file and no favicon. `frontend/public/` contains only `.gitkeep`, and
`app/layout.tsx` declares no `icons`, so every INVERTA tab shows the browser's blank-page glyph.

In place of a logo, three headers each hand-roll their own text mark, and they have already drifted:
`app/admin/layout.tsx:50` renders **"A"** (from the archived *Academy* product), while
`components/app/AppHeader.tsx:35` renders **"I"**. A learner and an admin see different brands.

A real logo now exists. Adopting it forces a colour decision, because the logo is violet and the
app is blue — see §4.

## 2. Scope

**In:** the two logo files added as assets, an icon set generated from them, one `<Logo>` component
replacing all three placeholders, and the brand colour retokened to the logo's violet.

**Out:**

- **The wordmark's "ACADEMY" line.** The lockup reads *inverta ACADEMY*, but the app calls itself
  "INVERTA" in headers, certificates and emails, and *Academy* was the archived product's name.
  Renaming the product reaches the certificate PDF and email templates and is a business decision,
  not a UI one. The logo ships as supplied; the surrounding copy is untouched.
- **Anything on the other four sub-projects** (§8).

## 3. The assets

Two files, both 3600×1251 RGBA with transparent backgrounds:

| Source | Content | Ships as |
|---|---|---|
| `Inverta Logo Font-01.png` | Wordmark in `#7F00FF` | `frontend/public/logo.png` |
| `Inverta Logo Font-02.png` | Same wordmark in white | `frontend/public/logo-white.png` |

The white variant exists for dark backgrounds. The admin sidebar is `bg-ink`, so it needs it; the
colour file would be nearly invisible there.

`Inverta Logo Font.jpg` is not used — it is the same artwork flattened onto white at lower
resolution, and a JPEG cannot carry the transparency the headers need.

Both are downscaled to **720px wide** on the way in, preserving transparency. 3600px is print
artwork; the headers render ~30px tall and the widest use is the auth pages at ~200px, so 720
covers a 3× display without committing 3600px PNGs to the repository.

## 4. Brand colour

The logo is `#7F00FF`. The app's `--color-primary` is `#0050e6`. These clash, and there is no
version of "just add the logo" that avoids choosing.

**Decision: the app adopts the logo's violet.** The logo is the brand; the interface follows it.

Contrast is not a constraint here — measured, not assumed:

| Pair | Ratio | AA normal (4.5) | AA UI (3.0) |
|---|---|---|---|
| `#7F00FF` on white | **6.28** | pass | pass |
| white on `#7F00FF` | **6.28** | pass | pass |
| `#0050e6` on white (today) | 6.38 | pass | pass |

The violet is within 0.1 of the blue it replaces, so WCAG 2.1 AA is preserved rather than merely
argued for.

**New token values** in `frontend/app/globals.css`:

| Token | From | To |
|---|---|---|
| `--color-primary` | `#0050e6` | `#7F00FF` |
| `--color-primary-hover` | `#0040c0` | `#6D00D9` |
| `--color-primary-soft` | `#e5edff` | `#F1E5FF` |
| `--ds-color-primary` | `#0050e6` | `#7F00FF` |
| `--ds-color-primary-hover` | `#0040c0` | `#6D00D9` |

`--color-primary-ink` stays `#ffffff` — white on the violet is the 6.28 pair above.

**This is a one-file change on the frontend.** Verified: `grep` for `0050e6` across
`frontend/app` and `frontend/components` returns nothing outside `globals.css`. Every button,
badge, focus ring and progress bar already reads the token.

### The certificate PDF

`backend/src/Infrastructure/Learning/CertificatePdf.cs:34` hardcodes
`XColor.FromArgb(0x00, 0x50, 0xE6)`. It changes to `0x7F, 0x00, 0xFF`.

**Consequence, accepted deliberately:** the PDF is rendered on demand, not stored, so an
already-issued certificate re-renders in violet the next time it is downloaded. Golden rule 6 makes
a certificate immutable, and it stays so — the score, the section breakdown, the recipient and the
verification code are all unchanged. Only the ink colour differs. This was raised and approved
rather than discovered later.

## 5. The icon set

Cropped from the **brain knockout inside the "a"** — the only element of the lockup that is square
and legible at 16px. Measured from `Inverta Logo Font-01.png`:

- Knockout bounding box: **x 2830–3118, y 426–662** (288 × 236)
- Centre: **(2974, 544)**

The brain is transparent, punched out of the solid purple bowl. Rendering it means compositing:
a `#7F00FF` rounded square, with the knockout region filled white.

| File | Size | Purpose |
|---|---|---|
| `frontend/app/icon.png` | 512 × 512 | Browser tab, bookmarks, PWA |
| `frontend/app/apple-icon.png` | 180 × 180 | iOS home screen |

**No `metadata.icons` entry is needed.** Next.js App Router serves `app/icon.*` and
`app/apple-icon.*` by file convention and generates the `<link>` tags itself. The absence of an
`icons` block in `app/layout.tsx` is therefore not a defect to fix — adding one would duplicate
what the convention already does.

The glyph occupies **76% of the square's width**, leaving 12% padding on each side, and is
centred on its own bounding box (2830–3118 × 426–662) rather than on the crop, so it reads as
optically centred. Corner radius is **22% of the square** — matching the `rounded-lg` the
placeholder marks use today, so the shape in the tab matches the shape in the header.

## 6. The `<Logo>` component

`frontend/components/Logo.tsx` — one component, three call sites.

```
<Logo variant="colour" | "white" />
```

- `variant="white"` → `logo-white.png`, for `app/admin/layout.tsx` (dark `bg-ink` sidebar)
- `variant="colour"` → `logo.png`, everywhere else

Replaces:

| File | Currently |
|---|---|
| `frontend/app/admin/layout.tsx:50` | a span rendering **"A"** |
| `frontend/components/app/AppHeader.tsx:35` | a span rendering **"I"** |
| `frontend/components/PublicNav.tsx` | its own inline mark |

Rendered with `next/image` so the 720px asset is served at the size actually displayed, with
`priority` on the header instances — the logo is above the fold on every page and a late-loading
logo reads as a broken site.

`alt="INVERTA"`. Not "INVERTA logo": a screen reader announcing "INVERTA logo image" is noise, and
the wordmark's function here is the site name.

## 7. Testing

The colour change is covered by the contrast measurements in §4 — the numbers are the test, and
they are already taken.

What genuinely needs an automated guard is the icon set, because **a missing favicon fails
silently**. Nothing errors; the tab just shows a blank page glyph, and nobody files it for weeks.

**The frontend has no test runner** — `package.json` has `dev`, `build`, `start`, `lint` and
`generate:api`, and no jest, vitest or testing-library. Installing one to assert a favicon's
dimensions would cost more than the feature.

So the guard is a build step, not a test: `frontend/scripts/check-icons.mjs`, wired as `prebuild`,
which fails the build when `app/icon.png` or `app/apple-icon.png` is missing, empty, non-square, or
not 512 / 180. It reads the PNG IHDR header directly — 20 lines of node, no dependency. A broken
icon then breaks CI instead of sitting unnoticed in production.

Everything else here is visual and is verified by loading the deployed pages: the three headers
show the logo, the admin sidebar's version is legible on dark, and the tab shows the brain.

## 8. The other four sub-projects

This design is the first of five, split because they share almost nothing and have very different
risk. Recorded here so the ordering is a decision rather than an accident:

1. **Logo, favicon and brand colour** — this document.
2. **Learner programme page as 30/70 master-detail.** Sessions listed left, the whole session
   inline right, so a learner stops bouncing back to the list between sessions.
   `SessionView({token, sessionId})` is already self-contained, which makes this a contained
   refactor.
3. **Landing page density.** `/` currently redirects to `/program/toefl-preparation`, so the
   "homepage" is the programme page. Rebuilt from real content only — hero with price above the
   fold, the 8-session syllabus, what is included, the ITP format, the 12 seeded FAQs.
4. **Final exam on a per-attempt UUID route**, 404 rather than 403 for an attempt that is not
   yours, and a lifetime that ends when the sitting ends. Separate because it touches a paid exam.
5. **Admin-editable instructor, outcome stats and testimonials.** Three tables, three admin
   screens. Last, because the blocks render as nothing until content exists, so #3 ships without it.

#3 and #5 are coupled by data, not layout: #3 reads those three blocks from one source, and #5
swaps that source from a stub to the database without touching the page.
