"""
Generate achievement icon for Deadzone modifier unlock.

Outputs:
  - Assets/Achievements/DEADZONE_UNSEALED.png
  - Assets/Achievements/DEADZONE_UNSEALED_locked.png

Run from project root:
  python Scripts/Tools/gen_deadzone_unlock_icon.py
"""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps

SIZE = 512
OUT_DIR = Path("Assets/Achievements")


def new_layer() -> Image.Image:
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def glow(base: Image.Image, draw_fn, blur: float = 20.0) -> None:
    layer = new_layer()
    draw_fn(ImageDraw.Draw(layer))
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    base.alpha_composite(layer)


def radial_bg(
    base: Image.Image,
    center_rgb: tuple[int, int, int],
    edge_rgb: tuple[int, int, int],
    steps: int = 90,
) -> None:
    d = ImageDraw.Draw(base)
    cx = SIZE // 2
    cy = SIZE // 2
    max_r = int(math.sqrt(cx * cx + cy * cy)) + 8
    step = max(1, max_r // steps)
    for r in range(max_r, 0, -step):
        t = r / max_r
        color = tuple(int(edge_rgb[i] * t + center_rgb[i] * (1 - t)) for i in range(3))
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color + (255,))


def add_grid(base: Image.Image, spacing: int, color: tuple[int, int, int, int]) -> None:
    layer = new_layer()
    d = ImageDraw.Draw(layer)
    for i in range(0, SIZE + spacing, spacing):
        d.line([(i, 0), (i, SIZE)], fill=color, width=1)
        d.line([(0, i), (SIZE, i)], fill=color, width=1)
    base.alpha_composite(layer)


def add_scanlines(base: Image.Image, alpha: int = 18) -> None:
    layer = new_layer()
    d = ImageDraw.Draw(layer)
    for y in range(0, SIZE, 4):
        d.line([(0, y), (SIZE, y)], fill=(0, 0, 0, alpha), width=1)
    base.alpha_composite(layer)


def make_deadzone_icon() -> Image.Image:
    base = Image.new("RGBA", (SIZE, SIZE), (3, 16, 12, 255))
    radial_bg(base, center_rgb=(10, 50, 36), edge_rgb=(3, 11, 9))
    add_grid(base, spacing=32, color=(36, 160, 108, 40))
    add_scanlines(base, alpha=20)

    jade = (38, 220, 158)     # primary jade-teal
    pale = (175, 255, 225)    # bright highlight
    mist = (88, 240, 182)     # mid-tone for particles

    cx, cy = SIZE // 2, SIZE // 2

    # ── Atmospheric zone aura ─────────────────────────────────────────────────
    def _aura(d: ImageDraw.ImageDraw) -> None:
        for r, a in [(192, 14), (152, 22), (114, 28)]:
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=jade + (a,))

    glow(base, _aura, blur=52)

    # ── Diamond zone boundary (rotated square) ────────────────────────────────
    # Vertices at cardinal directions -- classic AoE zone marker shape.
    half = 155
    zone_pts = [
        (cx,          cy - half),   # top
        (cx + half,   cy),           # right
        (cx,          cy + half),   # bottom
        (cx - half,   cy),           # left
        (cx,          cy - half),   # close
    ]

    def _zone_glow(d: ImageDraw.ImageDraw) -> None:
        d.line(zone_pts, fill=jade + (145,), width=20, joint="curve")

    glow(base, _zone_glow, blur=18)
    d = ImageDraw.Draw(base)
    d.line(zone_pts, fill=pale + (245,), width=5, joint="curve")

    # Corner tick marks at each vertex -- "lock-on" brackets
    bracket_len = 22
    for vx, vy in zone_pts[:-1]:
        dx = cx - vx
        dy = cy - vy
        ln = math.hypot(dx, dy)
        ux, uy = dx / ln, dy / ln
        px, py = -uy, ux

        inner = (int(vx + ux * bracket_len), int(vy + uy * bracket_len))
        left  = (int(vx + px * bracket_len), int(vy + py * bracket_len))
        right = (int(vx - px * bracket_len), int(vy - py * bracket_len))

        def _bracket_glow(d: ImageDraw.ImageDraw, i=inner, l=left, r=right, v=(vx, vy)) -> None:
            d.line([l, v, r], fill=jade + (160,), width=10)
            d.line([v, i],    fill=jade + (130,), width=8)

        glow(base, _bracket_glow, blur=9)
        d.line([left, (vx, vy), right], fill=pale + (255,), width=3)
        d.line([(vx, vy), inner],       fill=pale + (200,), width=2)

    # ── 4 inward-converging arrows from diamond vertices ──────────────────────
    # Each arrow shaft runs from just inside the vertex toward an inner zone ring,
    # finishing with an arrowhead. Represents the zone "closing in" on the trigger.
    inner_stop_r = 72   # arrowheads stop at this radius from center
    shaft_inset  = 30   # start shaft this far inside the vertex

    for vx, vy in zone_pts[:-1]:
        dx = cx - vx
        dy = cy - vy
        ln = math.hypot(dx, dy)
        ux, uy = dx / ln, dy / ln
        px, py = -uy, ux

        shaft_start = (int(vx + ux * shaft_inset),        int(vy + uy * shaft_inset))
        shaft_end   = (int(cx - ux * (inner_stop_r + 18)), int(cy - uy * (inner_stop_r + 18)))
        tip         = (int(cx - ux * (inner_stop_r - 20)), int(cy - uy * (inner_stop_r - 20)))
        head_root   = (int(cx - ux * (inner_stop_r + 14)), int(cy - uy * (inner_stop_r + 14)))
        hl          = (int(head_root[0] + px * 13), int(head_root[1] + py * 13))
        hr          = (int(head_root[0] - px * 13), int(head_root[1] - py * 13))

        def _arrow_glow(
            d: ImageDraw.ImageDraw,
            ss=shaft_start, se=shaft_end,
            t=tip, l=hl, r=hr,
        ) -> None:
            d.line([ss, se], fill=jade + (170,), width=12)
            d.polygon([t, l, r], fill=jade + (170,))

        glow(base, _arrow_glow, blur=11)
        d.line([shaft_start, shaft_end], fill=pale + (245,), width=3)
        d.polygon([tip, hl, hr], fill=pale + (255,))

    # ── Inner trigger ring (arm window) ──────────────────────────────────────
    arm_r = inner_stop_r - 4

    def _inner_ring(d: ImageDraw.ImageDraw) -> None:
        d.ellipse([cx - arm_r, cy - arm_r, cx + arm_r, cy + arm_r],
                  outline=jade + (210,), width=12)

    glow(base, _inner_ring, blur=14)
    d.ellipse([cx - arm_r, cy - arm_r, cx + arm_r, cy + arm_r],
              outline=pale + (255,), width=3)

    # ── Central activation core ───────────────────────────────────────────────
    def _core(d: ImageDraw.ImageDraw) -> None:
        d.ellipse([cx - 28, cy - 28, cx + 28, cy + 28], fill=jade + (235,))

    glow(base, _core, blur=16)
    d.ellipse([cx - 13, cy - 13, cx + 13, cy + 13], fill=pale + (255,))
    d.ellipse([cx - 5,  cy - 5,  cx + 5,  cy + 5],  fill=(255, 255, 255, 255))

    # ── Sparse shimmer particles ──────────────────────────────────────────────
    for deg in (18, 63, 108, 153, 198, 243, 288, 333):
        a   = math.radians(deg)
        rr  = 205 if deg % 90 != 18 else 172
        sx  = cx + int(math.cos(a) * rr)
        sy  = cy + int(math.sin(a) * rr)
        pr  = 3 if deg % 2 == 0 else 2
        d.ellipse([sx - pr, sy - pr, sx + pr, sy + pr], fill=mist + (185,))

    return base.convert("RGB")


def make_locked(unlocked_rgb: Image.Image) -> Image.Image:
    gray    = ImageOps.grayscale(unlocked_rgb).convert("RGB")
    dim     = ImageEnhance.Brightness(gray).enhance(0.34)
    low_sat = ImageEnhance.Color(dim).enhance(0.06)

    layer    = low_sat.convert("RGBA")
    vignette = new_layer()
    d        = ImageDraw.Draw(vignette)
    d.ellipse([64, 64, SIZE - 64, SIZE - 64], fill=(220, 220, 220, 28))
    vignette = vignette.filter(ImageFilter.GaussianBlur(20))
    layer.alpha_composite(vignette)
    return layer.convert("RGB")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    unlocked = make_deadzone_icon()
    unlocked.save(OUT_DIR / "DEADZONE_UNSEALED.png")
    print(f"Saved {OUT_DIR / 'DEADZONE_UNSEALED.png'}")

    locked = make_locked(unlocked)
    locked.save(OUT_DIR / "DEADZONE_UNSEALED_locked.png")
    print(f"Saved {OUT_DIR / 'DEADZONE_UNSEALED_locked.png'}")


if __name__ == "__main__":
    main()
