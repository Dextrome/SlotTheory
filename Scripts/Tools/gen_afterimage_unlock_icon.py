"""
Generate achievement icon for Afterimage unlock.

Outputs:
  - Assets/Achievements/AFTERIMAGE_UNSEALED.png
  - Assets/Achievements/AFTERIMAGE_UNSEALED_locked.png

Run from project root:
  python Scripts/Tools/gen_afterimage_unlock_icon.py
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


def radial_bg(base: Image.Image, center_rgb: tuple[int, int, int], edge_rgb: tuple[int, int, int], steps: int = 96) -> None:
    draw = ImageDraw.Draw(base)
    cx = SIZE // 2
    cy = SIZE // 2
    max_r = int(math.sqrt(cx * cx + cy * cy)) + 10
    step = max(1, max_r // steps)
    for r in range(max_r, 0, -step):
        t = r / max_r
        color = tuple(int(edge_rgb[i] * t + center_rgb[i] * (1 - t)) for i in range(3))
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color + (255,))


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


def draw_afterimage_arcs(draw: ImageDraw.ImageDraw, cx: int, cy: int, color: tuple[int, int, int, int], width: int = 8) -> None:
    # Two offset, broken rings to read as "imprint + delayed replay".
    draw.arc([cx - 160, cy - 160, cx + 160, cy + 160], start=210, end=345, fill=color, width=width)
    draw.arc([cx - 160, cy - 160, cx + 160, cy + 160], start=18, end=144, fill=color, width=width)

    draw.arc([cx - 108, cy - 108, cx + 108, cy + 108], start=196, end=322, fill=color, width=max(3, width - 2))
    draw.arc([cx - 108, cy - 108, cx + 108, cy + 108], start=34, end=156, fill=color, width=max(3, width - 2))


def make_afterimage_icon() -> Image.Image:
    base = Image.new("RGBA", (SIZE, SIZE), (6, 10, 18, 255))
    radial_bg(base, center_rgb=(22, 52, 70), edge_rgb=(5, 8, 16))
    add_grid(base, spacing=32, color=(48, 118, 142, 44))
    add_scanlines(base, alpha=22)

    cyan = (74, 236, 255)
    pale = (194, 255, 255)
    mist = (122, 246, 255)

    cx, cy = SIZE // 2, SIZE // 2

    def _ambient(d: ImageDraw.ImageDraw) -> None:
        for r, a in [(204, 14), (164, 22), (126, 30)]:
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=cyan + (a,))

    glow(base, _ambient, blur=48)

    def _rings(d: ImageDraw.ImageDraw) -> None:
        draw_afterimage_arcs(d, cx, cy, cyan + (220,), width=10)

    glow(base, _rings, blur=15)

    d = ImageDraw.Draw(base)
    draw_afterimage_arcs(d, cx, cy, pale + (245,), width=5)

    # Ghost scar line crossing center.
    path = [(120, 318), (178, 272), (228, 238), (286, 216), (334, 194), (396, 148)]

    def _scar(dd: ImageDraw.ImageDraw) -> None:
        dd.line(path, fill=mist + (215,), width=16, joint="curve")

    glow(base, _scar, blur=10)
    d.line(path, fill=pale + (255,), width=5, joint="curve")

    # Delayed trigger marker (small hollow core + ping ring).
    ping_x, ping_y = 336, 192

    def _ping(dd: ImageDraw.ImageDraw) -> None:
        dd.ellipse([ping_x - 34, ping_y - 34, ping_x + 34, ping_y + 34], outline=cyan + (220,), width=8)
        dd.ellipse([ping_x - 14, ping_y - 14, ping_x + 14, ping_y + 14], fill=cyan + (215,))

    glow(base, _ping, blur=12)
    d.ellipse([ping_x - 26, ping_y - 26, ping_x + 26, ping_y + 26], outline=pale + (255,), width=4)
    d.ellipse([ping_x - 8, ping_y - 8, ping_x + 8, ping_y + 8], fill=pale + (255,))

    # Echo arrowhead at the end of scar path to imply one replay burst.
    tip = path[-1]
    prev = path[-2]
    dx = tip[0] - prev[0]
    dy = tip[1] - prev[1]
    ln = max(1.0, math.hypot(dx, dy))
    ux, uy = dx / ln, dy / ln
    px, py = -uy, ux
    head = [
        (int(tip[0] + ux * 20), int(tip[1] + uy * 20)),
        (int(tip[0] - ux * 8 + px * 11), int(tip[1] - uy * 8 + py * 11)),
        (int(tip[0] - ux * 8 - px * 11), int(tip[1] - uy * 8 - py * 11)),
    ]

    def _head(dd: ImageDraw.ImageDraw) -> None:
        dd.polygon(head, fill=mist + (225,))

    glow(base, _head, blur=9)
    d.polygon(head, fill=pale + (255,))

    # Sparse shimmer particles.
    for deg in (12, 42, 78, 122, 168, 214, 252, 288, 324):
        a = math.radians(deg)
        rr = 190 if deg % 2 == 0 else 146
        sx = cx + int(math.cos(a) * rr)
        sy = cy + int(math.sin(a) * rr)
        d.ellipse([sx - 3, sy - 3, sx + 3, sy + 3], fill=mist + (188,))

    return base.convert("RGB")


def make_locked(unlocked_rgb: Image.Image) -> Image.Image:
    gray = ImageOps.grayscale(unlocked_rgb).convert("RGB")
    dim = ImageEnhance.Brightness(gray).enhance(0.34)
    low_sat = ImageEnhance.Color(dim).enhance(0.06)

    layer = low_sat.convert("RGBA")
    vignette = new_layer()
    d = ImageDraw.Draw(vignette)
    d.ellipse([64, 64, SIZE - 64, SIZE - 64], fill=(220, 220, 220, 28))
    vignette = vignette.filter(ImageFilter.GaussianBlur(20))
    layer.alpha_composite(vignette)
    return layer.convert("RGB")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    unlocked = make_afterimage_icon()
    unlocked.save(OUT_DIR / "AFTERIMAGE_UNSEALED.png")

    locked = make_locked(unlocked)
    locked.save(OUT_DIR / "AFTERIMAGE_UNSEALED_locked.png")

    print("Saved unlock icon:")
    print(" - AFTERIMAGE_UNSEALED(.png/_locked.png)")


if __name__ == "__main__":
    main()
