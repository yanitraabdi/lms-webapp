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
