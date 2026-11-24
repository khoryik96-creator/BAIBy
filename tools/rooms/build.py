#!/usr/bin/env python3
"""Build the premium nursery room themes from the shipped Sage backdrop.

    python3 tools/rooms/build.py            # write the theme backdrops + .meta files
    python3 tools/rooms/build.py --preview  # also write a contact sheet (default: the session scratchpad; --out DIR)

No new illustration: each theme recolours the wall and the rug of
`Resources/BAIby/Nursery/Backdrop.png` (Sage). The artist's own Peach version
(`BackdropPeach.png`) differs from Sage exactly where the room's colour lives
(wall and rug), so the per-pixel difference of the two is the recolour mask; the
quilt, furniture, plant, books and light stay as painted. Inside the mask every
channel is scaled by target / reference (the baby appearance recolour rule), which
keeps the painted texture and the window light. A theme may add a cheap painted
touch on the wall: soft clouds (Cloud nursery) or a sprinkle of stars (Starlight).

The outputs are JPEGs (quality 90, no chroma subsampling) the size of the
original: the backdrop is an opaque painting, so JPEG keeps it under ~0.4 MB
instead of ~1.9 MB as a PNG, and Unity imports it uncompressed like the PNGs.
The build is deterministic (fixed seeds, fixed encoder settings) and keeps
existing .meta GUIDs.
"""

from __future__ import annotations

import argparse
import io
import math
import uuid
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

REPO = Path(__file__).resolve().parents[2]
NURSERY = REPO / "unity" / "Assets" / "Resources" / "BAIby" / "Nursery"
SAGE = NURSERY / "Backdrop.png"
PEACH = NURSERY / "BackdropPeach.png"
DEFAULT_OUT = Path("/tmp/claude-0/-home-user-BAIBy/729cdbf9-e760-56a7-bde4-d84d73210b9a/scratchpad/looks-rooms")

# Theme name -> (file stem, wall target, rug target, light contrast, touch). Targets are sRGB; the light
# contrast scales the window light and texture around the wall's own brightness (softer at night).
THEMES = {
    "CloudNursery": ("BackdropCloud", (198, 220, 238), (230, 236, 244), 1.0, "clouds"),
    "Starlight": ("BackdropStarlight", (104, 108, 160), (164, 150, 200), 0.6, "stars"),
    "GoldenHour": ("BackdropGoldenHour", (240, 206, 140), (150, 170, 124), 1.0, None),
}
LUMA = np.array([0.299, 0.587, 0.114])

# The wall ends at the baseboard and the rug starts below the floorboards (rows of the 1672 px image).
WALL_END = 945
RUG_START = 1000
# Boxes (x0, y0, x1, y1) around the plant and the books on the shelf, which keep their own colours.
PROTECT = [(0, 120, 175, 500), (650, 30, 941, 262)]
# The window light falls on the wall in this box (x0, y0, x1, y1).
LIGHT = (100, 110, 480, 660)


def load(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float64)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def masks(sage, peach):
    """Soft wall and rug weights from where the artist recoloured Sage into Peach, kept to the sage
    paint itself: the plant's leaves and the books differ between the two paintings too, but their
    chromaticity is far from the wall's, so they keep their colours."""
    diff = np.abs(sage - peach).max(axis=-1)
    chroma = sage / np.maximum(sage.sum(axis=-1, keepdims=True), 1e-6)
    painted = smoothstep(18, 36, diff)
    paint = np.median(chroma[painted > 0.95], axis=0)
    away = np.abs(chroma - paint).max(axis=-1)
    # around the plant and the books the gate is strict; elsewhere it is loose, so the warm window light
    # and the shadow under the shelf (both a little off the paint's chromaticity) follow the wall
    strict = np.zeros(away.shape, bool)
    for x0, y0, x1, y1 in PROTECT:
        strict[y0:y1, x0:x1] = True
    gate = np.where(strict, 1 - smoothstep(0.016, 0.03, away), 1 - smoothstep(0.045, 0.08, away))
    # the window light on the wall is the same warm light in both paintings, so the difference misses it
    lit = Image.new("L", (away.shape[1], away.shape[0]), 0)
    ImageDraw.Draw(lit).rectangle(LIGHT, fill=255)
    lit = np.asarray(lit.filter(ImageFilter.GaussianBlur(30)), dtype=np.float64) / 255 * ~strict
    weight = np.maximum(painted, lit) * gate
    weight = np.asarray(Image.fromarray((weight * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)),
                        dtype=np.float64) / 255
    rows = np.arange(sage.shape[0])[:, None]
    wall = weight * (rows < WALL_END)
    rug = weight * (rows >= RUG_START)
    return wall, rug


def reference(img, weight):
    sel = weight > 0.95
    return np.median(img[sel], axis=0)


def recolor(img, weight, ref, target, contrast=1.0):
    """The target colour at the reference brightness, following each pixel's brightness (its painted
    texture and the window light) around it; contrast < 1 softens that light."""
    y_ref = float(ref @ LUMA)
    k = (y_ref + (img @ LUMA - y_ref) * contrast) / y_ref
    scaled = np.clip(np.asarray(target, float)[None, None, :] * k[..., None], 0, 255)
    return img * (1 - weight[..., None]) + scaled * weight[..., None]


def blend(img, layer, alpha):
    return img * (1 - alpha[..., None]) + np.asarray(layer, float) * alpha[..., None]


def clouds(img, wall, seed=7):
    """Soft painted clouds: round puffs with a light top and a faint blue underside."""
    h, w = wall.shape
    rng = np.random.default_rng(seed)
    alpha = Image.new("L", (w, h), 0)
    shade = Image.new("L", (w, h), 0)
    da, ds = ImageDraw.Draw(alpha), ImageDraw.Draw(shade)
    for cx, cy, size in ((170, 300, 1.0), (560, 150, 0.8), (720, 520, 1.1), (300, 690, 0.75), (820, 820, 0.6)):
        puffs = [(0, 0, 70), (-75, 18, 52), (72, 16, 56), (-30, -38, 50), (36, -30, 46), (110, 30, 36), (-118, 34, 34)]
        for dx, dy, r in puffs:
            r = r * size * rng.uniform(0.9, 1.1)
            x, y = cx + dx * size, cy + dy * size
            da.ellipse([x - r, y - r, x + r, y + r], fill=255)
        ds.rectangle([cx - 160 * size, cy + 22 * size, cx + 160 * size, cy + 90 * size], fill=255)
    a = np.asarray(alpha.filter(ImageFilter.GaussianBlur(10)), dtype=np.float64) / 255
    under = np.asarray(shade.filter(ImageFilter.GaussianBlur(18)), dtype=np.float64) / 255 * a
    out = blend(img, (250, 252, 255), a * 0.62 * wall)
    return blend(out, (186, 208, 230), under * 0.35 * wall)


def star_points(cx, cy, r, inner=0.45, points=5, rot=-90):
    pts = []
    for k in range(points * 2):
        rr = r if k % 2 == 0 else r * inner
        a = math.radians(rot + k * 180 / points)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    return pts


def stars(img, wall, seed=11):
    """A sprinkle of soft golden stars and tiny dots, each with a faint glow."""
    h, w = wall.shape
    rng = np.random.default_rng(seed)
    ss = 3
    shape = Image.new("L", (w * ss, h * ss), 0)
    d = ImageDraw.Draw(shape)
    placed = []
    while len(placed) < 26:
        x, y = rng.uniform(30, w - 30), rng.uniform(30, WALL_END - 80)
        if any(math.hypot(x - px, y - py) < 95 for px, py in placed):
            continue
        placed.append((x, y))
        if len(placed) % 3 == 0:
            r = rng.uniform(4, 6)
            d.ellipse([(x - r) * ss, (y - r) * ss, (x + r) * ss, (y + r) * ss], fill=255)
        else:
            r = rng.uniform(11, 19)
            d.polygon([(px * ss, py * ss) for px, py in star_points(x, y, r, rot=-90 + rng.uniform(-14, 14))], fill=255)
    shape = shape.resize((w, h), Image.LANCZOS)
    a = np.asarray(shape, dtype=np.float64) / 255
    glow = np.asarray(shape.filter(ImageFilter.GaussianBlur(9)), dtype=np.float64) / 255
    out = blend(img, (255, 236, 170), np.clip(glow * 0.55, 0, 1) * wall)
    return blend(out, (255, 228, 150), a * 0.92 * wall)


def build(name, sage, wall, rug, wall_ref, rug_ref):
    stem, wall_target, rug_target, contrast, touch = THEMES[name]
    out = recolor(sage, wall, wall_ref, wall_target, contrast)
    out = recolor(out, rug, rug_ref, rug_target, contrast)
    if touch == "clouds":
        out = clouds(out, wall)
    elif touch == "stars":
        out = stars(out, wall)
    return stem, np.clip(np.rint(out), 0, 255).astype(np.uint8)


def jpeg_bytes(arr):
    buf = io.BytesIO()
    Image.fromarray(arr, "RGB").save(buf, format="JPEG", quality=90, subsampling=0, optimize=True)
    return buf.getvalue()


def ensure_meta(path: Path):
    """The texture importer settings of Backdrop.png, with this file's own (kept) GUID."""
    meta = path.with_name(path.name + ".meta")
    guid = None
    if meta.exists():
        for line in meta.read_text().splitlines():
            if line.startswith("guid:"):
                guid = line.split(":", 1)[1].strip()
    template = (NURSERY / "Backdrop.png.meta").read_text()
    text = "\n".join(f"guid: {guid or uuid.uuid4().hex}" if line.startswith("guid:") else line
                     for line in template.splitlines()) + "\n"
    if not meta.exists() or meta.read_text() != text:
        meta.write_text(text)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = ap.parse_args()
    sage, peach = load(SAGE), load(PEACH)
    wall, rug = masks(sage, peach)
    wall_ref, rug_ref = reference(sage, wall), reference(sage, rug)
    print("wall reference", np.round(wall_ref).tolist(), "rug reference", np.round(rug_ref).tolist())
    built = []
    for name in THEMES:
        stem, arr = build(name, sage, wall, rug, wall_ref, rug_ref)
        path = NURSERY / f"{stem}.jpg"
        data = jpeg_bytes(arr)
        if not path.exists() or path.read_bytes() != data:
            path.write_bytes(data)
        ensure_meta(path)
        built.append((name, arr))
        print(f"{name}: {path.relative_to(REPO)} {len(data)} bytes")
    if args.preview:
        args.out.mkdir(parents=True, exist_ok=True)
        tiles = [Image.open(SAGE).convert("RGB"), Image.open(PEACH).convert("RGB")] + \
                [Image.fromarray(arr) for _, arr in built]
        w, h = tiles[0].width // 3, tiles[0].height // 3
        sheet = Image.new("RGB", (w * len(tiles), h))
        for i, tile in enumerate(tiles):
            sheet.paste(tile.resize((w, h), Image.LANCZOS), (i * w, 0))
        sheet.save(args.out / "rooms.png")
        print("wrote", args.out / "rooms.png")


if __name__ == "__main__":
    main()
