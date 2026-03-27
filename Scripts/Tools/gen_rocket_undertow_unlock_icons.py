"""
Generate achievement icons for Rocket and Undertow map unlocks.

Outputs:
  - Assets/Achievements/ROCKET_UNSEALED.png
  - Assets/Achievements/ROCKET_UNSEALED_locked.png
  - Assets/Achievements/UNDERTOW_UNSEALED.png
  - Assets/Achievements/UNDERTOW_UNSEALED_locked.png

Run from project root:
  python Scripts/Tools/gen_rocket_undertow_unlock_icons.py
"""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps

SIZE = 512
OUT_DIR = Path("Assets/Achievements")


def new_layer() -> Image.Image:
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def glow(base: Image.Image, draw_fn, blur: float = 20) -> None:
    layer = new_layer()
    draw_fn(ImageDraw.Draw(layer))
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    base.alpha_composite(layer)


def radial_bg(base: Image.Image, center_rgb: tuple[int, int, int], edge_rgb: tuple[int, int, int], steps: int = 88) -> None:
    d = ImageDraw.Draw(base)
    cx = SIZE // 2
    cy = SIZE // 2
    max_r = int(math.sqrt(cx * cx + cy * cy)) + 8
    for r in range(max_r, 0, -max(1, max_r // steps)):
        t = r / max_r
        c = tuple(int(edge_rgb[i] * t + center_rgb[i] * (1 - t)) for i in range(3))
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=c + (255,))


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


def make_rocket_icon() -> Image.Image:
    base = Image.new("RGBA", (SIZE, SIZE), (8, 8, 16, 255))
    radial_bg(base, center_rgb=(46, 18, 14), edge_rgb=(9, 6, 12))
    add_grid(base, spacing=32, color=(90, 34, 24, 45))
    add_scanlines(base, alpha=22)

    accent = (255, 122, 34)
    hot = (255, 225, 165)

    # Atmospheric blast aura.
    def _aura(d: ImageDraw.ImageDraw) -> None:
        for r, a in [(210, 18), (170, 24), (130, 30)]:
            d.ellipse([256 - r, 236 - r, 256 + r, 236 + r], fill=accent + (a,))

    glow(base, _aura, blur=52)

    # Explosion ring near the impact point.
    def _ring(d: ImageDraw.ImageDraw) -> None:
        d.ellipse([286, 86, 466, 266], outline=hot + (220,), width=12)
        d.ellipse([304, 104, 448, 248], outline=accent + (210,), width=7)

    glow(base, _ring, blur=14)

    d = ImageDraw.Draw(base)
    d.ellipse([292, 92, 460, 260], outline=hot + (255,), width=5)

    # Rocket body (tilted up-right).
    body = [(136, 378), (322, 192), (354, 224), (168, 410)]
    nose = [(322, 192), (392, 162), (354, 224)]
    fin_l = [(152, 400), (112, 446), (190, 432)]
    fin_r = [(182, 370), (228, 332), (238, 404)]

    def _rocket_glow(dd: ImageDraw.ImageDraw) -> None:
        dd.polygon(body, fill=accent + (210,))
        dd.polygon(nose, fill=hot + (230,))
        dd.polygon(fin_l, fill=accent + (200,))
        dd.polygon(fin_r, fill=accent + (200,))

    glow(base, _rocket_glow, blur=12)
    d = ImageDraw.Draw(base)
    d.polygon(body, fill=(65, 20, 9, 255), outline=accent + (255,))
    d.polygon(nose, fill=accent + (255,))
    d.polygon(fin_l, fill=(85, 28, 14, 255), outline=accent + (255,))
    d.polygon(fin_r, fill=(85, 28, 14, 255), outline=accent + (255,))

    # Rocket stripe and viewport.
    d.line([(190, 356), (332, 214)], fill=hot + (255,), width=7)
    d.ellipse([248, 270, 284, 306], fill=(255, 240, 210, 255), outline=hot + (255,), width=3)

    # Exhaust trail.
    exhaust_poly = [(116, 404), (36, 474), (158, 440)]
    def _exhaust(dd: ImageDraw.ImageDraw) -> None:
        dd.polygon(exhaust_poly, fill=(255, 172, 84, 220))
        dd.polygon([(98, 416), (24, 500), (154, 456)], fill=(255, 228, 164, 210))

    glow(base, _exhaust, blur=16)
    d = ImageDraw.Draw(base)
    d.polygon(exhaust_poly, fill=(255, 156, 64, 220))
    d.polygon([(96, 420), (38, 486), (146, 450)], fill=(255, 229, 170, 230))

    # Burst shards around impact ring.
    shard_angles = [20, 48, 76, 104, 132, 160, 206, 234, 262, 290, 318, 346]
    cx, cy = 376, 176
    for angle in shard_angles:
        a = math.radians(angle)
        x0 = cx + int(math.cos(a) * 74)
        y0 = cy + int(math.sin(a) * 74)
        x1 = cx + int(math.cos(a) * 108)
        y1 = cy + int(math.sin(a) * 108)
        d.line([(x0, y0), (x1, y1)], fill=hot + (235,), width=4)

    return base.convert("RGB")


def make_undertow_icon() -> Image.Image:
    base = Image.new("RGBA", (SIZE, SIZE), (5, 12, 20, 255))
    radial_bg(base, center_rgb=(8, 45, 70), edge_rgb=(4, 8, 16))
    add_grid(base, spacing=32, color=(24, 88, 112, 42))
    add_scanlines(base, alpha=20)

    accent = (36, 206, 245)
    bright = (180, 255, 255)
    cx, cy = 256, 256

    # Vortex field.
    def _field(d: ImageDraw.ImageDraw) -> None:
        for r, a in [(206, 14), (164, 22), (124, 32), (90, 38)]:
            d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=accent + (a,), width=14)

    glow(base, _field, blur=38)

    # Spiral bands.
    spiral = new_layer()
    sd = ImageDraw.Draw(spiral)
    for t in range(10, 1060, 2):
        theta = t * 0.031
        radius = 210 - (t * 0.17)
        if radius < 18:
            break
        x = cx + int(math.cos(theta) * radius)
        y = cy + int(math.sin(theta) * radius)
        if t % 12 == 0:
            sd.ellipse([x - 3, y - 3, x + 3, y + 3], fill=accent + (190,))
    spiral = spiral.filter(ImageFilter.GaussianBlur(2))
    base.alpha_composite(spiral)

    # Three inward arrow lanes.
    d = ImageDraw.Draw(base)
    for start_deg in (18, 138, 258):
        points: list[tuple[int, int]] = []
        for step in range(0, 126):
            th = math.radians(start_deg + step * 2.3)
            r = 190 - step * 1.25
            if r < 34:
                break
            points.append((cx + int(math.cos(th) * r), cy + int(math.sin(th) * r)))
        if len(points) < 3:
            continue

        def _lane(dd: ImageDraw.ImageDraw, pts=points) -> None:
            dd.line(pts, fill=accent + (225,), width=12, joint="curve")

        glow(base, _lane, blur=11)
        d = ImageDraw.Draw(base)
        d.line(points, fill=bright + (255,), width=5, joint="curve")

        p1 = points[-1]
        p2 = points[-2]
        dx = p1[0] - p2[0]
        dy = p1[1] - p2[1]
        ln = max(1.0, math.hypot(dx, dy))
        ux, uy = dx / ln, dy / ln
        px, py = -uy, ux
        head = [
            (int(p1[0] + ux * 14), int(p1[1] + uy * 14)),
            (int(p1[0] - ux * 7 + px * 9), int(p1[1] - uy * 7 + py * 9)),
            (int(p1[0] - ux * 7 - px * 9), int(p1[1] - uy * 7 - py * 9)),
        ]
        d.polygon(head, fill=bright + (255,))

    # Core sink point.
    def _core(dd: ImageDraw.ImageDraw) -> None:
        dd.ellipse([cx - 36, cy - 36, cx + 36, cy + 36], fill=accent + (210,))
        dd.ellipse([cx - 14, cy - 14, cx + 14, cy + 14], fill=bright + (250,))

    glow(base, _core, blur=16)
    d = ImageDraw.Draw(base)
    d.ellipse([cx - 26, cy - 26, cx + 26, cy + 26], fill=(7, 35, 58, 255), outline=accent + (255,), width=4)
    d.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=bright + (255,))

    return base.convert("RGB")


def make_locked(unlocked_rgb: Image.Image) -> Image.Image:
    gray = ImageOps.grayscale(unlocked_rgb).convert("RGB")
    dim = ImageEnhance.Brightness(gray).enhance(0.33)
    low_sat = ImageEnhance.Color(dim).enhance(0.08)

    # Subtle center lift for readability in the achievements list.
    layer = low_sat.convert("RGBA")
    vignette = new_layer()
    d = ImageDraw.Draw(vignette)
    d.ellipse([64, 64, SIZE - 64, SIZE - 64], fill=(220, 220, 220, 28))
    vignette = vignette.filter(ImageFilter.GaussianBlur(20))
    layer.alpha_composite(vignette)
    return layer.convert("RGB")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    rocket = make_rocket_icon()
    rocket.save(OUT_DIR / "ROCKET_UNSEALED.png")
    make_locked(rocket).save(OUT_DIR / "ROCKET_UNSEALED_locked.png")

    undertow = make_undertow_icon()
    undertow.save(OUT_DIR / "UNDERTOW_UNSEALED.png")
    make_locked(undertow).save(OUT_DIR / "UNDERTOW_UNSEALED_locked.png")

    print("Saved unlock icons:")
    print(" - ROCKET_UNSEALED(.png/_locked.png)")
    print(" - UNDERTOW_UNSEALED(.png/_locked.png)")


if __name__ == "__main__":
    main()
