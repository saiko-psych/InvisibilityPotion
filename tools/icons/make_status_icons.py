#!/usr/bin/env python3
"""Procedural status-effect icons for InvisibilityPotion.

Draws each icon at 256x256 and downscales to 128x128 (Lanczos). Output:
  InvisibilityPotion/Icons/se_veil_t1.png ... se_veil_cooldown.png (embedded in the DLL)
  tools/icons/preview-status-icons.png (contact sheet on dark and light backgrounds, at 128 and 64 px)

Style follows vanilla status icons: pale high-contrast silhouette, coloured accents,
soft outer glow, transparent background.

Requires Python 3 with Pillow and numpy. Run from anywhere:
  python3 tools/icons/make_status_icons.py
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 256          # drawing size
OUT = 128        # shipped size
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ICON_DIR = os.path.join(ROOT, "InvisibilityPotion", "Icons")
PREVIEW = os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview-status-icons.png")


# ---------------------------------------------------------------- helpers

def blank():
    return Image.new("L", (S, S), 0)


def arr(img):
    return np.asarray(img, dtype=np.float32) / 255.0


def to_l(a):
    return Image.fromarray(np.clip(a * 255.0, 0, 255).astype(np.uint8), "L")


def blur(a, r):
    return arr(to_l(a).filter(ImageFilter.GaussianBlur(r)))


def bezier(p0, p1, p2, p3, n=24):
    pts = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        x = u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0]
        y = u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]
        pts.append((x, y))
    return pts


def compose(layers):
    """layers: list of (alpha array 0..1, rgb tuple 0..255). Painter's order, 'over' operator."""
    rgb = np.zeros((S, S, 3), np.float32)
    a = np.zeros((S, S), np.float32)
    for la, col in layers:
        la = np.clip(la, 0, 1)
        c = np.array(col, np.float32) / 255.0
        out_a = la + a * (1 - la)
        safe = np.where(out_a > 1e-6, out_a, 1)
        rgb = (c[None, None, :] * la[..., None] + rgb * a[..., None] * (1 - la[..., None])) / safe[..., None]
        a = out_a
    img = np.dstack([rgb, a[..., None]])
    return Image.fromarray(np.clip(img * 255, 0, 255).astype(np.uint8), "RGBA")


# ---------------------------------------------------------------- shapes

def hood_mask():
    """Hooded head and shoulders, filled. Face opening returned separately."""
    img = blank()
    d = ImageDraw.Draw(img)
    pts = []
    # left side: pointed hood tip -> cheek -> neck pinch -> shoulder -> down
    pts += bezier((128, 14), (102, 24), (78, 58), (78, 104))
    pts += bezier((78, 104), (78, 130), (86, 146), (92, 154))
    pts += bezier((92, 154), (50, 160), (22, 188), (16, 240))
    pts += [(240, 240)]
    pts += bezier((240, 240), (234, 188), (206, 160), (164, 154))
    pts += bezier((164, 154), (170, 146), (178, 130), (178, 104))
    pts += bezier((178, 104), (178, 58), (154, 24), (128, 14))
    d.polygon(pts, fill=255)
    return arr(img)


def face_mask():
    img = blank()
    d = ImageDraw.Draw(img)
    pts = bezier((128, 62), (110, 64), (102, 88), (104, 108))
    pts += bezier((104, 108), (106, 128), (116, 140), (128, 144))
    pts += bezier((128, 144), (140, 140), (150, 128), (152, 108))
    pts += bezier((152, 108), (154, 88), (146, 64), (128, 62))
    d.polygon(pts, fill=255)
    return blur(arr(img), 1.2)


def hood_fold():
    """A thin highlight line along the hood rim to keep the silhouette readable."""
    img = blank()
    d = ImageDraw.Draw(img)
    pts = bezier((128, 46), (100, 52), (90, 82), (92, 110))
    pts += bezier((92, 110), (94, 138), (110, 154), (128, 158))
    pts += bezier((128, 158), (146, 154), (162, 138), (164, 110))
    pts += bezier((164, 110), (166, 82), (156, 52), (128, 46))
    d.line(pts, fill=255, width=5, joint="curve")
    return blur(arr(img), 1.0)


def vertical_fade(y0, y1):
    """1 above y0, 0 below y1, smooth in between."""
    y = np.arange(S, dtype=np.float32)[:, None]
    t = np.clip((y - y0) / (y1 - y0), 0, 1)
    t = t * t * (3 - 2 * t)
    return np.repeat(1 - t, S, axis=1)


def spiral(d, cx, cy, r0, r1, turns, width, direction=1, squash=0.55):
    n = 80
    prev = None
    for i in range(n + 1):
        t = i / n
        ang = direction * t * turns * 2 * math.pi
        r = r0 + (r1 - r0) * t
        x = cx + r * math.cos(ang)
        y = cy + r * math.sin(ang) * squash
        if prev is not None:
            w = max(1, int(round(width * (0.35 + 0.65 * t))))
            d.line([prev, (x, y)], fill=255, width=w)
            d.ellipse([x - w / 2, y - w / 2, x + w / 2, y + w / 2], fill=255)
        prev = (x, y)


def wisp(d, x0, x1, y, amp, width):
    pts = []
    for i in range(41):
        t = i / 40
        x = x0 + (x1 - x0) * t
        pts.append((x, y + amp * math.sin(t * math.pi * 1.6)))
    for i in range(len(pts) - 1):
        taper = math.sin(math.pi * i / (len(pts) - 1))
        w = max(1, int(round(width * (0.25 + 0.75 * taper))))
        d.line([pts[i], pts[i + 1]], fill=255, width=w)


def mist_mask(density):
    """Swirling mist band at the bottom. density 1..3."""
    img = blank()
    d = ImageDraw.Draw(img)
    # base curls, always present
    spiral(d, 92, 196, 4, 30, 1.15, 9, direction=1)
    spiral(d, 168, 192, 4, 32, 1.15, 9, direction=-1)
    wisp(d, 36, 220, 222, 6, 10)
    if density >= 2:
        spiral(d, 130, 168, 3, 22, 1.1, 7, direction=1)
        wisp(d, 44, 150, 176, -5, 7)
        wisp(d, 120, 214, 206, 5, 8)
    if density >= 3:
        spiral(d, 60, 150, 3, 20, 1.1, 6, direction=-1)
        spiral(d, 198, 146, 3, 20, 1.1, 6, direction=1)
        wisp(d, 70, 186, 132, 4, 6)
        wisp(d, 30, 120, 236, 3, 7)
    a = arr(img)
    return np.clip(blur(a, 1.4) * 1.15, 0, 1)


# ---------------------------------------------------------------- icons

def veil(tier):
    spec = {
        # figure tint, accent (mist/glow), figure opacity, mist density, fade start/end
        1: ((222, 236, 220), (150, 196, 150), 0.80, 1, 172, 232),
        2: ((214, 230, 248), (92, 150, 214), 0.50, 2, 160, 222),
        3: ((236, 222, 255), (164, 104, 236), 0.20, 3, 140, 206),
    }[tier]
    fig_col, accent, opacity, density, f0, f1 = spec

    hood = hood_mask()
    face = face_mask()
    fade = vertical_fade(f0, f1)
    body = np.clip(hood - face, 0, 1) * fade
    rim = hood_fold() * fade * np.clip(hood, 0, 1)
    mist = mist_mask(density)

    figure_a = body * opacity
    # the rim keeps the silhouette legible at low opacity
    rim_a = rim * min(1.0, opacity + 0.45)
    shape = np.clip(np.maximum(body, mist), 0, 1)
    glow = blur(shape, 9) * 0.85
    halo = blur(shape, 3) * 0.6
    face_shadow = face * fade * min(1.0, opacity + 0.25)

    mist_col = tuple(int(0.45 * a + 0.55 * f) for a, f in zip(accent, fig_col))
    layers = [
        (glow, accent),
        (halo, accent),
        (face_shadow, (14, 12, 22)),
        (figure_a, fig_col),
        (rim_a, fig_col),
        (mist * 0.95, mist_col),
    ]
    if tier == 3:
        eyes = blank()
        d = ImageDraw.Draw(eyes)
        for cx in (114, 142):
            d.ellipse([cx - 7, 100, cx + 7, 108], fill=255)
        e = arr(eyes)
        layers += [(blur(e, 6) * 1.0, (200, 140, 255)), (blur(e, 1.2), (250, 240, 255))]
    return compose(layers)


def crack_path():
    pts = [(124, 12), (134, 34), (120, 52), (138, 74), (126, 92), (142, 112),
           (122, 134), (136, 156), (124, 178), (140, 200), (128, 222), (134, 244)]
    return pts


def broken():
    fig_col = (238, 230, 222)
    accent = (236, 96, 40)
    hood = hood_mask()
    face = face_mask()
    fade = vertical_fade(196, 244)
    body = np.clip(hood - face, 0, 1) * fade

    # split the figure along the crack: left half shifted left, right half right
    pts = crack_path()
    left = blank()
    ImageDraw.Draw(left).polygon([(0, 0)] + [(pts[0][0], 0)] + pts + [(pts[-1][0], S), (0, S)], fill=255)
    lm = arr(left)
    shift = 7
    body_l = np.roll(body * lm, -shift, axis=1)
    body_r = np.roll(body * (1 - lm), shift, axis=1)
    face_l = np.roll(face * fade * lm, -shift, axis=1)
    face_r = np.roll(face * fade * (1 - lm), shift, axis=1)
    figure = np.clip(body_l + body_r, 0, 1)

    crack = blank()
    d = ImageDraw.Draw(crack)
    d.line(pts, fill=255, width=6, joint="curve")
    # a couple of side branches
    d.line([pts[3], (pts[3][0] - 26, pts[3][1] + 12)], fill=255, width=4)
    d.line([pts[6], (pts[6][0] + 24, pts[6][1] + 10)], fill=255, width=4)
    c = arr(crack)

    sparks = blank()
    d = ImageDraw.Draw(sparks)
    rnd = random.Random(3)
    for _ in range(14):
        p = pts[rnd.randrange(1, len(pts) - 1)]
        sx = p[0] + rnd.uniform(-34, 34)
        sy = p[1] + rnd.uniform(-18, 18)
        r = rnd.uniform(2.5, 5.0)
        d.ellipse([sx - r, sy - r, sx + r, sy + r], fill=255)
    sp = arr(sparks)

    shape = np.clip(figure + c, 0, 1)
    layers = [
        (blur(shape, 9) * 0.6, (180, 70, 40)),
        (blur(c + sp, 7) * 1.0, accent),
        (np.clip(face_l + face_r, 0, 1), (18, 12, 12)),
        (figure, fig_col),
        (blur(c, 2.5), (255, 120, 50)),
        (c, (255, 214, 150)),
        (blur(sp, 1.0), (255, 190, 100)),
    ]
    return compose(layers)


def cooldown():
    fig_col = (176, 178, 182)
    accent = (120, 124, 130)
    hood = hood_mask()
    face = face_mask()
    fade = vertical_fade(176, 236)
    body = np.clip(hood - face, 0, 1) * fade

    # clock arc around the figure: three quarters done, gap at the top right
    ring = blank()
    d = ImageDraw.Draw(ring)
    d.arc([14, 14, 242, 242], start=-90 + 110, end=-90 + 360, fill=255, width=12)
    r = arr(ring)
    ring_dim = blank()
    ImageDraw.Draw(ring_dim).arc([14, 14, 242, 242], start=-90, end=-90 + 110, fill=255, width=6)
    rd = arr(ring_dim)

    # hourglass badge bottom right
    hg = blank()
    d = ImageDraw.Draw(hg)
    cx, cy = 194, 190
    d.rectangle([cx - 30, cy - 46, cx + 30, cy - 38], fill=255)
    d.rectangle([cx - 30, cy + 38, cx + 30, cy + 46], fill=255)
    d.polygon([(cx - 24, cy - 38), (cx + 24, cy - 38), (cx + 4, cy - 2), (cx + 4, cy + 2),
               (cx + 24, cy + 38), (cx - 24, cy + 38), (cx - 4, cy + 2), (cx - 4, cy - 2)], fill=255)
    h = arr(hg)
    sand = blank()
    d = ImageDraw.Draw(sand)
    d.polygon([(cx - 12, cy - 18), (cx + 12, cy - 18), (cx, cy - 2)], fill=255)
    d.polygon([(cx - 20, cy + 36), (cx + 20, cy + 36), (cx, cy + 20)], fill=255)
    sd = arr(sand)
    hg_back = blur(h, 4)
    # carve the badge out of the figure so it stays readable
    body = body * (1 - np.clip(blur(h, 5) * 1.6, 0, 1))
    face_sh = face * fade

    shape = np.clip(body + r, 0, 1)
    layers = [
        (blur(shape, 8) * 0.55, accent),
        (face_sh, (20, 20, 24)),
        (body, fig_col),
        (rd * 0.55, (110, 112, 118)),
        (r, (204, 206, 210)),
        (hg_back * 0.9, (20, 20, 24)),
        (h, (226, 228, 232)),
        (sd, (150, 150, 140)),
    ]
    return compose(layers)


# ---------------------------------------------------------------- output

def finish(img):
    return img.resize((OUT, OUT), Image.LANCZOS)


def contact_sheet(icons):
    names = list(icons)
    pad = 16
    cell = OUT + pad
    w = pad + len(names) * cell
    rows = []
    for bg in ((34, 32, 30), (196, 190, 178)):
        row = Image.new("RGBA", (w, cell + 64 + pad), bg + (255,))
        for i, n in enumerate(names):
            x = pad + i * cell
            row.alpha_composite(icons[n], (x, pad))
            small = icons[n].resize((64, 64), Image.LANCZOS)
            row.alpha_composite(small, (x + (OUT - 64) // 2, pad + OUT + 8))
        rows.append(row)
    sheet = Image.new("RGBA", (w, sum(r.height for r in rows)))
    y = 0
    for r in rows:
        sheet.alpha_composite(r, (0, y))
        y += r.height
    return sheet


def main():
    os.makedirs(ICON_DIR, exist_ok=True)
    icons = {
        "se_veil_t1": finish(veil(1)),
        "se_veil_t2": finish(veil(2)),
        "se_veil_t3": finish(veil(3)),
        "se_veil_broken": finish(broken()),
        "se_veil_cooldown": finish(cooldown()),
    }
    for name, img in icons.items():
        path = os.path.join(ICON_DIR, name + ".png")
        img.save(path, optimize=True)
        print(f"{path}  {os.path.getsize(path)} bytes")
    contact_sheet(icons).save(PREVIEW, optimize=True)
    print(PREVIEW)


if __name__ == "__main__":
    main()
