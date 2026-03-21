"""
gen_first_steps_icon.py - generates TUTORIAL_COMPLETE.png achievement icon
Motif: ascending 3-step staircase in lime-green, medal-on-stem style.
Run from project root: python Scripts/Tools/gen_first_steps_icon.py
"""

import math
import random
from PIL import Image, ImageDraw, ImageFilter

SIZE = 512
OUT  = "Assets/Achievements/TUTORIAL_COMPLETE.png"

COL   = (80, 255, 140)   # lime-green main
DARK  = (6,  22,  10)    # near-black bg edge
MID   = (12, 42,  20)    # radial center


def new_layer():
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def glow(img, draw_fn, blur=24):
    layer = new_layer()
    draw_fn(ImageDraw.Draw(layer))
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    img.alpha_composite(layer)


def radial_bg(img, center_color, edge_color, steps=80):
    d = ImageDraw.Draw(img)
    cx, cy = SIZE // 2, SIZE // 2
    max_r = int(math.sqrt(cx**2 + cy**2)) + 10
    for r in range(max_r, 0, -max_r // steps):
        t = r / max_r
        c = tuple(int(edge_color[i] * t + center_color[i] * (1 - t)) for i in range(3))
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=c + (255,))


def grid_lines(img, spacing=32, color=(20, 55, 28, 45)):
    layer = new_layer()
    d = ImageDraw.Draw(layer)
    for i in range(0, SIZE + spacing, spacing):
        d.line([(i, 0), (i, SIZE)], fill=color, width=1)
        d.line([(0, i), (SIZE, i)], fill=color, width=1)
    img.alpha_composite(layer)


def make_icon():
    img = Image.new("RGBA", (SIZE, SIZE), DARK + (255,))
    radial_bg(img, MID, DARK)
    grid_lines(img)

    cx, cy = SIZE // 2, SIZE // 2

    # ── corner vignette (dark green atmospheric glow) ──
    for bx, by in [(0, 0), (SIZE, 0), (0, SIZE), (SIZE, SIZE)]:
        def _corner(d, bx=bx, by=by):
            d.ellipse([bx - 180, by - 180, bx + 180, by + 180], fill=(0, 80, 30, 50))
        glow(img, _corner, blur=60)

    # ── scanlines ──
    scan = new_layer()
    sd = ImageDraw.Draw(scan)
    for y in range(0, SIZE, 4):
        sd.line([(0, y), (SIZE, y)], fill=(0, 0, 0, 22), width=1)
    img.alpha_composite(scan)

    # ── medal pin geometry ──
    node_y  = 100          # top ball centre
    stem_top = node_y + 22
    stem_bot = 218         # where stem meets staircase top

    # ── large outer aura behind staircase ──
    def _aura(d):
        for r, a in [(200, 8), (160, 16), (120, 26)]:
            d.ellipse([cx - r, cy + 20 - r, cx + r, cy + 20 + r], fill=COL + (a,))
    glow(img, _aura, blur=55)

    # ── inner glow halo ──
    def _halo(d):
        for r, a in [(140, 20), (100, 38), (70, 50)]:
            d.ellipse([cx - r, cy + 20 - r, cx + r, cy + 20 + r], fill=COL + (a,))
    glow(img, _halo, blur=32)

    # ── staircase motif ──
    # Three steps ascending left → right, centred around cx, bottom at y=390
    step_w   = 80    # width of each tread (horizontal surface)
    step_h   = 52    # height of each riser
    gap      = 4     # gap between steps for definition
    bottom_y = 390

    # Step coords (left x, top y) - step 1 lowest, step 3 highest
    steps_geom = [
        (cx - step_w * 3 // 2 - gap,  bottom_y - step_h),          # step 1 (left, lowest)
        (cx - step_w // 2,             bottom_y - step_h * 2 - gap),# step 2 (mid)
        (cx + step_w // 2 + gap,       bottom_y - step_h * 3 - gap * 2),  # step 3 (right, highest)
    ]

    # Glow behind each step (brighter on higher steps)
    for i, (sx, sy) in enumerate(steps_geom):
        alpha = 35 + i * 22
        def _step_glow(d, sx=sx, sy=sy, a=alpha):
            d.rectangle([sx - 10, sy - 10, sx + step_w + 10, bottom_y + 10], fill=COL + (a,))
        glow(img, _step_glow, blur=22)

    d = ImageDraw.Draw(img)

    # Dark fill + colored border for each step
    for i, (sx, sy) in enumerate(steps_geom):
        # Dark tinted interior
        dc = tuple(c // 7 for c in COL)
        d.rectangle([sx, sy, sx + step_w, bottom_y], fill=dc + (255,))
        d.rectangle([sx, sy, sx + step_w, bottom_y], outline=COL, width=3)
        # Highlight top-left corner of each tread (inner shine)
        d.rectangle([sx + 4, sy + 4, sx + step_w - 4, sy + 8], fill=(180, 255, 210, 160))

    # Small upward arrow on the top-right step
    ax = steps_geom[2][0] + step_w // 2
    ay = steps_geom[2][1] - 28
    arrow_pts = [(ax, ay - 18), (ax - 14, ay + 2), (ax - 6, ay + 2),
                 (ax - 6, ay + 18), (ax + 6, ay + 18), (ax + 6, ay + 2), (ax + 14, ay + 2)]
    def _arrow_glow(d):
        d.polygon(arrow_pts, fill=COL + (140,))
    glow(img, _arrow_glow, blur=14)
    d = ImageDraw.Draw(img)
    d.polygon(arrow_pts, fill=COL)

    # ── stem connecting node to staircase ──
    # Stem runs from node_y+22 down to top of step 3
    step3_top = steps_geom[2][1]
    def _stem_glow(d):
        d.rectangle([cx - 10, stem_top - 4, cx + 10, step3_top + 4], fill=COL + (120,))
    glow(img, _stem_glow, blur=12)
    d = ImageDraw.Draw(img)
    d.rectangle([cx - 7, stem_top, cx + 7, step3_top], fill=COL)
    # Highlight stripe on stem
    d.rectangle([cx - 2, stem_top + 6, cx + 2, step3_top - 6], fill=(210, 255, 225, 200))

    # ── node glow ──
    def _node_glow(d):
        d.ellipse([cx - 38, node_y - 38, cx + 38, node_y + 38], fill=COL + (200,))
    glow(img, _node_glow, blur=22)

    def _node_core(d):
        d.ellipse([cx - 16, node_y - 16, cx + 16, node_y + 16], fill=(220, 255, 235, 240))
    glow(img, _node_core, blur=8)

    d = ImageDraw.Draw(img)
    d.ellipse([cx - 22, node_y - 22, cx + 22, node_y + 22], fill=COL)
    d.ellipse([cx - 11, node_y - 11, cx + 11, node_y + 11], fill=(210, 255, 230))

    # ── scattered spark dots ──
    spark = new_layer()
    spd = ImageDraw.Draw(spark)
    rng = random.Random(42)
    for _ in range(50):
        angle = math.radians(rng.uniform(0, 360))
        r = rng.randint(70, 200)
        sx2 = int(cx + r * math.cos(angle))
        sy2 = int(cy + 20 + r * math.sin(angle) * 0.8)
        sz = rng.randint(1, 3)
        alpha = rng.randint(100, 200)
        col = (60, 220, 100, alpha) if rng.random() > 0.4 else (160, 255, 190, alpha)
        spd.ellipse([sx2 - sz, sy2 - sz, sx2 + sz, sy2 + sz], fill=col)
    spark = spark.filter(ImageFilter.GaussianBlur(1))
    img.alpha_composite(spark)

    return img.convert("RGB")


if __name__ == "__main__":
    icon = make_icon()
    icon.save(OUT)
    print(f"Saved {OUT}")
