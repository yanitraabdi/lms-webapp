# Logo, Favicon and Brand Colour Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace three drifted text placeholders with the real INVERTA logo, ship a favicon generated from the brain glyph, and move the app's brand colour from blue to the logo's violet.

**Architecture:** The two supplied PNGs are committed to `docs/brand/` as the source of truth. One Python script derives every asset from them — downscaled logos into `frontend/public/`, and composited icons into `frontend/app/` where Next.js App Router serves them by file convention. A single `<Logo>` component replaces all three hand-rolled marks. The colour change is one file, because the frontend is fully tokenised.

**Tech Stack:** Next.js App Router (file-convention icons, `next/image`), Tailwind CSS custom properties, Pillow for asset generation, PDFsharp for the certificate.

**Source spec:** `docs/superpowers/specs/2026-09-03-logo-favicon-brand-colour-design.md` — read it before Task 1.

## Global Constraints

- **Brand violet is `#7F00FF`.** Hover `#6D00D9`. Soft `#F1E5FF`. Ink stays `#ffffff`.
- **WCAG 2.1 AA must hold.** `#7F00FF` on white measures 6.28:1 — already verified, do not re-derive a different shade without re-measuring.
- **No hardcoded brand hex in components.** Everything reads the CSS custom property. The frontend currently has zero hardcoded `0050e6` outside `globals.css`; keep it that way.
- **Icon files are served by Next.js file convention.** Do NOT add a `metadata.icons` block to `app/layout.tsx` — `app/icon.png` and `app/apple-icon.png` generate their own `<link>` tags, and declaring both duplicates them.
- **Certificates are immutable (GR-6).** The PDF's ink colour may change; its score, section breakdown, recipient and verification code may not.
- **The wordmark's "ACADEMY" line is out of scope.** Ship the logo as supplied; do not rename the product in headers, certificates or emails.
- `Nullable` enabled and `TreatWarningsAsErrors` on for the backend. A warning fails the build.
- Frontend verification is `npx tsc --noEmit && npm run build`. There is no frontend test runner.

---

## File Structure

**Create:**

| File | Responsibility |
|---|---|
| `docs/brand/inverta-logo.png` | Colour source, 3600×1251 RGBA. Brand source of truth. |
| `docs/brand/inverta-logo-white.png` | White source, for dark backgrounds. |
| `scripts/generate-brand-assets.py` | Derives every logo and icon from the two sources. |
| `frontend/public/logo.png` | Derived, 720px wide colour wordmark. |
| `frontend/public/logo-white.png` | Derived, 720px wide white wordmark. |
| `frontend/app/icon.png` | Derived, 512×512 favicon. |
| `frontend/app/apple-icon.png` | Derived, 180×180 iOS icon. |
| `frontend/scripts/check-icons.mjs` | Build guard — fails the build if an icon is missing or wrong-sized. |
| `frontend/components/Logo.tsx` | The one logo component. |

**Modify:**

| File | Change |
|---|---|
| `frontend/app/globals.css:33-36, 82-85` | Retoken primary to violet. |
| `backend/src/Infrastructure/Learning/CertificatePdf.cs:34` | Brand colour in the PDF. |
| `frontend/package.json` | Add the `prebuild` script. |
| `frontend/components/PublicNav.tsx:35-38` | Replace the "A" mark. |
| `frontend/components/app/AppHeader.tsx:35` | Replace the "I" mark. |
| `frontend/app/admin/layout.tsx:50` | Replace the "A" mark. |

---

## Task 1: Brand colour

The frontend is fully tokenised, so this is two files and it recolours the whole app. Doing it first means the icons generated in Task 2 land in an app that already matches them.

**Files:**
- Modify: `frontend/app/globals.css:33-36` and `:82-85`
- Modify: `backend/src/Infrastructure/Learning/CertificatePdf.cs:34`

**Interfaces:**
- Produces: CSS custom properties `--color-primary`, `--color-primary-hover`, `--color-primary-soft`, `--ds-color-primary`, `--ds-color-primary-hover`, `--ds-color-primary-soft`, all violet. Later tasks read these; nothing hardcodes hex.

- [ ] **Step 1: Verify the contrast claim before changing anything**

The spec asserts `#7F00FF` passes AA. Confirm it rather than trusting the document:

```bash
python3 -c "
def lum(h):
    r,g,b=(int(h[i:i+2],16)/255 for i in (1,3,5))
    f=lambda c: c/12.92 if c<=0.03928 else ((c+0.055)/1.055)**2.4
    r,g,b=f(r),f(g),f(b); return .2126*r+.7152*g+.0722*b
def ratio(a,b):
    la,lb=lum(a),lum(b); hi,lo=max(la,lb),min(la,lb); return (hi+.05)/(lo+.05)
print('violet on white:', round(ratio('#7F00FF','#FFFFFF'),2), '(need >= 4.5)')
print('white on violet:', round(ratio('#FFFFFF','#7F00FF'),2), '(need >= 4.5)')
"
```

Expected: both print `6.28`. If either is below 4.5, STOP and report — do not pick a different shade without telling the plan's owner, because the spec's colour was chosen against these numbers.

- [ ] **Step 2: Retoken the light-theme block**

In `frontend/app/globals.css`, replace lines 33-36:

```css
  --color-primary: #7F00FF;
  --color-primary-hover: #6D00D9;
  --color-primary-ink: #ffffff;
  --color-primary-soft: #F1E5FF;
```

- [ ] **Step 3: Retoken the design-system block**

In the same file, replace lines 82-85:

```css
  --ds-color-primary: #7F00FF;
  --ds-color-primary-hover: #6D00D9;
  --ds-color-primary-ink: #ffffff;
  --ds-color-primary-soft: #F1E5FF;
```

- [ ] **Step 4: Confirm nothing hardcodes the old blue**

```bash
cd "frontend" && grep -rn "0050e6\|0040c0\|e5edff" app components lib
```

Expected: **no output**. If anything appears outside `globals.css`, it is a hardcoded brand colour that would now contradict the token — fix it to use the token rather than swapping its hex.

- [ ] **Step 5: Change the certificate PDF**

In `backend/src/Infrastructure/Learning/CertificatePdf.cs`, line 34 currently reads:

```csharp
        var primary = XColor.FromArgb(0x00, 0x50, 0xE6);
```

Replace with:

```csharp
        // Brand violet #7F00FF. The PDF renders on demand rather than being stored, so an
        // already-issued certificate re-renders in the new colour — deliberate and approved.
        // Immutability (GR-6) is untouched: score, section breakdown, recipient and verification
        // code are unchanged; only the ink differs.
        var primary = XColor.FromArgb(0x7F, 0x00, 0xFF);
```

- [ ] **Step 6: Verify both sides build and the suite passes**

```bash
cd "backend" && dotnet build Academy.slnx && dotnet test Academy.slnx
```

Expected: `0 Warning(s)`, `0 Error(s)`, and 394 tests passing. The certificate PDF test (`Certificate_pdf_renders_with_embedded_fonts`) exercises the changed line, so a malformed colour fails here.

```bash
cd "frontend" && npx tsc --noEmit && npm run build
```

Expected: no type errors, build succeeds.

- [ ] **Step 7: Commit**

```bash
git add frontend/app/globals.css backend/src/Infrastructure/Learning/CertificatePdf.cs
git commit -m "feat: adopt the logo's violet as the brand colour"
```

---

## Task 2: Brand assets and the icon set

Everything is derived from two committed sources by one script, so regenerating is a command rather than a memory of what was cropped.

**Files:**
- Create: `docs/brand/inverta-logo.png`, `docs/brand/inverta-logo-white.png`
- Create: `scripts/generate-brand-assets.py`
- Create (generated): `frontend/public/logo.png`, `frontend/public/logo-white.png`, `frontend/app/icon.png`, `frontend/app/apple-icon.png`
- Create: `frontend/scripts/check-icons.mjs`
- Modify: `frontend/package.json`

**Interfaces:**
- Produces: `/logo.png` and `/logo-white.png` as public URLs for Task 3's `<Logo>`; `app/icon.png` and `app/apple-icon.png` served by Next's file convention.

- [ ] **Step 1: Commit the sources**

```bash
mkdir -p docs/brand
cp ~/Downloads/"Inverta Logo Font-01.png" docs/brand/inverta-logo.png
cp ~/Downloads/"Inverta Logo Font-02.png" docs/brand/inverta-logo-white.png
```

Verify they are what the plan expects — 3600×1251 RGBA, the first violet and the second white:

```bash
python3 -c "
from PIL import Image
from collections import Counter
for f in ['docs/brand/inverta-logo.png','docs/brand/inverta-logo-white.png']:
    im=Image.open(f).convert('RGBA'); px=list(im.getdata())
    c=Counter(p[:3] for p in px if p[3]>200 and not all(v>240 for v in p[:3]))
    print(f, im.size, 'dominant:', c.most_common(1) or 'white/none')
"
```

Expected: both `(3600, 1251)`; the first reports `(127, 0, 255)`; the second reports `white/none` because it is white on transparent.

If the sizes differ, STOP — the crop coordinates in Step 2 are measured against a 3600×1251 image and will cut the wrong region.

- [ ] **Step 2: Write the generator**

Create `scripts/generate-brand-assets.py`:

```python
#!/usr/bin/env python3
"""
Derives every INVERTA logo and icon asset from the two committed brand sources.

Run after changing anything in docs/brand/:
    python3 scripts/generate-brand-assets.py

The favicon is NOT the wordmark. A 3600x1251 lockup is an unreadable smear at 16px, so the icon
is the brain glyph that sits inside the "a" — the only element of the lockup that is square and
legible at that size. It is a KNOCKOUT: transparent, punched out of the solid violet bowl, so
rendering it means compositing white through the transparency rather than copying pixels.
"""
from PIL import Image, ImageDraw

VIOLET = (0x7F, 0x00, 0xFF, 255)

SRC_COLOUR = "docs/brand/inverta-logo.png"
SRC_WHITE = "docs/brand/inverta-logo-white.png"

# Measured from docs/brand/inverta-logo.png. The brain knockout inside the "a".
GLYPH_BOX = (2830, 426, 3118, 662)          # 288 x 236
LOGO_WIDTH = 720                             # headers render ~30px tall; 720 covers 3x displays
GLYPH_FRACTION = 0.76                        # 12% padding each side
RADIUS_FRACTION = 0.22                       # matches the rounded-lg the old placeholders used


def write_logo(src: str, dest: str) -> None:
    """
    Trim, then downscale a 3600px print asset to something sane to ship.

    The trim is not cosmetic. The supplied artwork carries transparent padding — the wordmark
    fills only 66% of the canvas height — so a CSS height applied to the untrimmed file sizes the
    PADDING, and the wordmark renders about a third smaller than intended.
    """
    im = Image.open(src).convert("RGBA")
    box = im.getbbox()
    if box is None:
        raise SystemExit(f"{src} is fully transparent — wrong file?")
    im = im.crop(box)
    h = round(im.height * LOGO_WIDTH / im.width)
    im.resize((LOGO_WIDTH, h), Image.LANCZOS).save(dest)
    print(f"  {dest}  {LOGO_WIDTH}x{h}")


def write_icon(dest: str, size: int) -> None:
    """Violet rounded square with the brain knocked through it in white."""
    src = Image.open(SRC_COLOUR).convert("RGBA")
    glyph = src.crop(GLYPH_BOX)

    # The brain is the TRANSPARENT region inside the bowl, so the mask is inverted alpha.
    mask = glyph.split()[3].point(lambda a: 255 if a < 20 else 0)

    target_w = round(size * GLYPH_FRACTION)
    target_h = round(mask.height * target_w / mask.width)
    mask = mask.resize((target_w, target_h), Image.LANCZOS)

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    corner = Image.new("L", (size, size), 0)
    ImageDraw.Draw(corner).rounded_rectangle(
        (0, 0, size - 1, size - 1), radius=round(size * RADIUS_FRACTION), fill=255)
    canvas.paste(Image.new("RGBA", (size, size), VIOLET), (0, 0), corner)

    white = Image.new("RGBA", (target_w, target_h), (255, 255, 255, 255))
    canvas.paste(white, ((size - target_w) // 2, (size - target_h) // 2), mask)
    canvas.save(dest)
    print(f"  {dest}  {size}x{size}")


def main() -> None:
    print("logos:")
    write_logo(SRC_COLOUR, "frontend/public/logo.png")
    write_logo(SRC_WHITE, "frontend/public/logo-white.png")
    print("icons:")
    write_icon("frontend/app/icon.png", 512)
    write_icon("frontend/app/apple-icon.png", 180)


if __name__ == "__main__":
    main()
```

- [ ] **Step 3: Generate the assets**

```bash
python3 scripts/generate-brand-assets.py
```

Expected output — note the logos are ~720x214, not 720x250, because the transparent padding is
trimmed off first:

```
logos:
  frontend/public/logo.png  720x214
  frontend/public/logo-white.png  720x214
icons:
  frontend/app/icon.png  512x512
  frontend/app/apple-icon.png  180x180
```

The exact height depends on the trim, so take whatever the script prints. `getbbox()` is
alpha-based, so it finds the white wordmark in the second file just as well as the violet one.

- [ ] **Step 4: Look at the icon before trusting it**

Compositing bugs produce a plausible-looking file that is a violet square with nothing on it, or a brain that touches the edges. Open `frontend/app/icon.png` and confirm:

- The brain glyph is white, centred, and does not touch any edge.
- The corners are rounded, not square.
- The background is the brand violet, not black or transparent.

If the square is blank, the mask inverted the wrong way — the brain is the region where alpha is **below** 20, not above.

- [ ] **Step 5: Write the build guard**

A missing favicon fails silently: nothing errors, the tab just shows a blank glyph, and nobody notices for weeks. Create `frontend/scripts/check-icons.mjs`:

```javascript
// Fails the build when an app icon is missing or the wrong size.
// Reads the PNG IHDR header directly — width and height are big-endian uint32 at bytes 16-24 —
// so this needs no image library on a frontend that deliberately has few dependencies.
import { readFileSync } from "node:fs";

const EXPECTED = [
  ["app/icon.png", 512],
  ["app/apple-icon.png", 180],
];

let failed = false;
for (const [path, size] of EXPECTED) {
  let buf;
  try {
    buf = readFileSync(new URL(`../${path}`, import.meta.url));
  } catch {
    console.error(`✗ ${path} is missing. Run: python3 scripts/generate-brand-assets.py`);
    failed = true;
    continue;
  }
  if (buf.length === 0) {
    console.error(`✗ ${path} is empty.`);
    failed = true;
    continue;
  }
  const width = buf.readUInt32BE(16);
  const height = buf.readUInt32BE(20);
  if (width !== size || height !== size) {
    console.error(`✗ ${path} is ${width}x${height}, expected ${size}x${size}.`);
    failed = true;
  }
}

if (failed) process.exit(1);
console.log("✓ app icons present and correctly sized");
```

- [ ] **Step 6: Prove the guard actually fails**

A guard that cannot fail is decoration. Break it deliberately, watch it catch the break, then restore:

```bash
cd "frontend" && mv app/icon.png /tmp/icon.png.bak && node scripts/check-icons.mjs; echo "exit=$?"
```

Expected: `✗ app/icon.png is missing.` and `exit=1`.

```bash
cd "frontend" && mv /tmp/icon.png.bak app/icon.png && node scripts/check-icons.mjs; echo "exit=$?"
```

Expected: `✓ app icons present and correctly sized` and `exit=0`.

- [ ] **Step 7: Wire it into the build**

In `frontend/package.json`, add `prebuild` to `scripts` immediately before `build`:

```json
    "prebuild": "node scripts/check-icons.mjs",
```

npm runs `prebuild` automatically before `npm run build`, so a broken icon now breaks CI rather than shipping.

- [ ] **Step 8: Verify the build runs the guard**

```bash
cd "frontend" && npm run build 2>&1 | head -5
```

Expected: `✓ app icons present and correctly sized` appears before Next's build output.

- [ ] **Step 9: Commit**

```bash
git add docs/brand scripts/generate-brand-assets.py frontend/public/logo.png \
  frontend/public/logo-white.png frontend/app/icon.png frontend/app/apple-icon.png \
  frontend/scripts/check-icons.mjs frontend/package.json
git commit -m "feat: brand assets and a favicon generated from the brain glyph"
```

---

## Task 3: The Logo component

Three headers currently hand-roll their own mark and have already drifted — two render "A" (from the archived *Academy* product) and one renders "I". One component ends that.

**Files:**
- Create: `frontend/components/Logo.tsx`
- Modify: `frontend/components/PublicNav.tsx:35-38`
- Modify: `frontend/components/app/AppHeader.tsx:35`
- Modify: `frontend/app/admin/layout.tsx:50`

**Interfaces:**
- Consumes: `frontend/public/logo.png` and `frontend/public/logo-white.png` from Task 2, imported statically (`@/*` maps to `./*` in `tsconfig.json`, so `@/public/logo.png` resolves).
- Produces: `<Logo variant="colour" | "white" className?: string />`

- [ ] **Step 1: Write the component**

Create `frontend/components/Logo.tsx`:

```tsx
import Image from "next/image";
import logoColour from "@/public/logo.png";
import logoWhite from "@/public/logo-white.png";
import { cn } from "@/lib/cn";

/**
 * The INVERTA wordmark. One component for every header, because three hand-rolled marks had
 * already drifted — two rendered "A" (from the archived Academy product) and one rendered "I",
 * so a learner and an admin saw different brands.
 *
 * `variant="white"` is for dark backgrounds: the admin sidebar is `bg-ink`, where the colour
 * wordmark is nearly invisible.
 */
export function Logo({
  variant = "colour",
  className,
}: {
  variant?: "colour" | "white";
  className?: string;
}) {
  return (
    <Image
      // Statically imported, so width and height come from the file itself. Hardcoding them
      // would silently drift the moment the artwork is regenerated at a different aspect.
      src={variant === "white" ? logoWhite : logoColour}
      // The wordmark IS the site name here, so that is what a screen reader should announce.
      // "INVERTA logo" would add a word that tells a listener nothing.
      alt="INVERTA"
      // Above the fold on every page — a late-loading logo reads as a broken site.
      priority
      className={cn("h-7 w-auto", className)}
    />
  );
}
```

- [ ] **Step 2: Replace the public nav mark**

In `frontend/components/PublicNav.tsx`, lines 35-38 currently read:

```tsx
          <span className="flex h-7 w-7 items-center justify-center rounded-[7px] bg-primary text-sm font-extrabold text-primary-ink">
            A
          </span>
          <span className="text-sm font-bold text-ink">INVERTA</span>
```

Replace with:

```tsx
          <Logo />
```

Add the import beside the existing ones:

```tsx
import { Logo } from "@/components/Logo";
```

- [ ] **Step 3: Replace the app header mark**

In `frontend/components/app/AppHeader.tsx`, line 35 currently reads:

```tsx
            <span className="flex h-[30px] w-[30px] items-center justify-center rounded-lg bg-primary text-[15px] font-extrabold text-primary-ink">I</span>
            <span className="hidden text-sm font-extrabold sm:inline">INVERTA</span>
```

Replace with:

```tsx
            <Logo />
```

Add the import:

```tsx
import { Logo } from "@/components/Logo";
```

- [ ] **Step 4: Replace the admin sidebar mark**

In `frontend/app/admin/layout.tsx`, line 50 and the two lines under it currently read:

```tsx
          <span className="flex h-[30px] w-[30px] items-center justify-center rounded-lg bg-primary text-[15px] font-extrabold text-primary-ink">A</span>
          <div className="leading-tight">
            <div className="text-[13px] font-extrabold text-white">Academy</div>
            <div className="text-[10px] text-white/55">Admin</div>
          </div>
```

Replace with:

```tsx
          <Logo variant="white" className="h-6" />
          <div className="text-[10px] text-white/55">Admin</div>
```

The white variant is required here — the sidebar is `bg-ink`. This also removes the stray "Academy" label, which named the archived product.

Add the import:

```tsx
import { Logo } from "@/components/Logo";
```

- [ ] **Step 5: Verify the build**

```bash
cd "frontend" && npx tsc --noEmit && npm run build
```

Expected: no type errors, build succeeds. Lint treats unused imports as errors, so a forgotten `cn` or a leftover import fails here.

- [ ] **Step 6: Confirm no placeholder mark survives**

```bash
cd "frontend" && grep -rn "font-extrabold text-primary-ink\">[AI]<" app components
```

Expected: **no output**. Any hit is a fourth placeholder the plan did not know about — replace it with `<Logo />` too.

- [ ] **Step 7: Commit**

```bash
git add frontend/components/Logo.tsx frontend/components/PublicNav.tsx \
  frontend/components/app/AppHeader.tsx frontend/app/admin/layout.tsx
git commit -m "feat: one Logo component for all three headers"
```

---

## Done

Verify the whole thing before finishing the branch:

```bash
cd "backend" && dotnet build Academy.slnx && dotnet test Academy.slnx
```

```bash
cd "frontend" && npx tsc --noEmit && npm run build
```

Then deploy and look at it, because the rest is visual:

```bash
docker compose -f docker-compose.tunnel.yml up -d --build
```

Check on https://inverta.recosta.id:

- The browser tab shows the brain glyph, not a blank page icon.
- `/program/toefl-preparation` — the public header shows the colour wordmark.
- `/app/dashboard` — the app header shows the colour wordmark.
- `/admin` — the sidebar shows the **white** wordmark, legible against the dark background.
- Buttons, badges and focus rings are violet, not blue.

Then use `superpowers:finishing-a-development-branch`.

**What this deliberately does not do**, so nobody looks for it:

- It does not rename the product. The wordmark reads "inverta ACADEMY", but headers, certificates and emails still say "INVERTA" — renaming is a business decision that reaches the certificate PDF and email templates.
- It does not add `metadata.icons` to `app/layout.tsx`. Next.js App Router already generates those tags from the file convention; declaring them again duplicates them.
- It does not add a frontend test runner. The icon guard is a `prebuild` script because installing Jest to assert a favicon's dimensions would cost more than the feature.
