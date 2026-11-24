#!/usr/bin/env python3
"""Contact sheets of the generated appearance assets, using the exact runtime math.

    python3 tools/appearance/preview.py [--out DIR]

Reads only the shipped assets (layout.json + .bytes files), composites
Base -> Hair -> Headwear and recolours once, exactly like the C# runtime.
"""

from __future__ import annotations

import argparse
import json
import random
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from common import BACKDROP, OUT_DIR, POSES, STAGES, STYLES, HEADWEAR
from recolor import HAIR, OUTFIT, SKIN, composite, recolor, targets

DEFAULT_OUT = Path("/tmp/claude-0/-home-user-BAIBy/729cdbf9-e760-56a7-bde4-d84d73210b9a/scratchpad/appearance-preview")
SKIN_UI = ["Fair", "Light", "Warm", "Tan", "Brown", "Deep"]


class Assets:
    def __init__(self, root: Path = OUT_DIR):
        self.root = root
        self.layout = json.loads((root / "layout.json").read_text())
        self.ref = self.layout["reference"]
        self.over = {o["path"]: o for o in self.layout["overlays"]}
        self.cache = {}

    def load(self, rel):
        if rel not in self.cache:
            img = np.asarray(Image.open(self.root / f"{rel}.bytes").convert("RGBA")).copy()
            reg = np.asarray(Image.open(self.root / f"{rel}Regions.bytes").convert("RGBA")).copy()
            self.cache[rel] = (img, reg)
        return self.cache[rel]

    def render(self, pose, skin="Warm", hair="Chestnut", style="Curls", outfit="Sage", hat="None"):
        img, reg = self.load(f"Base/{pose}")
        img, reg = img.copy(), reg.copy()
        layers = [f"Hair/{style}/{pose}"]
        if hat != "None":
            layers.append(f"Headwear/{hat}/{pose}")
        for rel in layers:
            o = self.over[rel]
            oi, orr = self.load(rel)
            img, reg = composite(img, reg, oi, orr, o["x"], o["y"])
        tg, rf = targets(self.ref, skin, hair, outfit)
        return recolor(img, reg, tg, rf)


def font(size):
    for f in ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
              "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf"]:
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            pass
    return ImageFont.load_default()


def on_bg(rgba, bg=(236, 232, 226)):
    im = Image.fromarray(rgba, "RGBA")
    out = Image.new("RGBA", im.size, bg + (255,))
    out.alpha_composite(im)
    return out.convert("RGB")


def grid(cells, row_labels, col_labels, cell=260, title=""):
    """cells[r][c] = PIL image; arranged in a labelled grid."""
    lw, th = 150, 40
    rows, cols = len(cells), len(cells[0])
    W = lw + cols * cell
    H = th + 30 + rows * cell
    sheet = Image.new("RGB", (W, H), (250, 248, 244))
    d = ImageDraw.Draw(sheet)
    f, fs = font(22), font(18)
    d.text((10, 8), title, fill=(60, 50, 44), font=f)
    for c, lab in enumerate(col_labels):
        d.text((lw + c * cell + 8, th + 4), lab, fill=(90, 80, 70), font=fs)
    for r in range(rows):
        d.text((8, th + 30 + r * cell + cell // 2 - 10), row_labels[r], fill=(90, 80, 70), font=fs)
        for c in range(cols):
            im = cells[r][c].copy()
            im.thumbnail((cell - 8, cell - 8), Image.LANCZOS)
            x = lw + c * cell + (cell - im.width) // 2
            y = th + 30 + r * cell + (cell - im.height) // 2
            sheet.paste(im, (x, y))
    return sheet


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = ap.parse_args()
    out = args.out
    out.mkdir(parents=True, exist_ok=True)
    A = Assets()

    # (a) skin tones x stages
    cells = [[on_bg(A.render(p, skin=s)) for p in STAGES] for s in SKIN_UI]
    grid(cells, SKIN_UI, STAGES, title="Skin tones x stages").save(out / "a_skin.png")
    # (a2) skin close-up faces (readability check)
    cells = [[on_bg(A.render(p, skin=s, style=st))
              for p, st in [("Sitter", "Curls"), ("Walker", "Tuft"), ("CrawlerRest", "Coils")]] for s in SKIN_UI]
    grid(cells, SKIN_UI, ["Sitter Curls", "Walker Tuft", "CrawlerRest Coils"], cell=330,
         title="Skin tones: faces").save(out / "a2_skin_faces.png")
    # (b) hair colours x styles (Sitter) and x stages (Curls)
    hair = list(HAIR)
    cells = [[on_bg(A.render("Sitter", hair=h, style=st)) for st in STYLES] for h in hair]
    grid(cells, hair, STYLES, title="Hair colours x styles (Sitter)").save(out / "b_hair_colors.png")
    cells = [[on_bg(A.render(p, hair=h)) for p in STAGES] for h in hair]
    grid(cells, hair, STAGES, title="Hair colours x stages (Curls)").save(out / "b2_hair_stages.png")
    # (c) styles x all poses
    cells = [[on_bg(A.render(p, style=st)) for p in POSES] for st in STYLES]
    grid(cells, STYLES, POSES, cell=240, title="Hair styles x poses").save(out / "c_styles.png")
    # (d) outfits x stages
    outfits = list(OUTFIT)
    cells = [[on_bg(A.render(p, outfit=o)) for p in STAGES] for o in outfits]
    grid(cells, outfits, STAGES, title="Outfits x stages").save(out / "d_outfits.png")
    # (e) headwear x poses
    cells = [[on_bg(A.render(p, hat=k)) for p in POSES] for k in HEADWEAR]
    grid(cells, HEADWEAR, POSES, cell=240, title="Headwear x poses (Curls)").save(out / "e_headwear.png")
    cells = [[on_bg(A.render("Sitter", hat=k, style=st)) for st in STYLES] for k in HEADWEAR]
    grid(cells, HEADWEAR, STYLES, title="Headwear x styles (Sitter)").save(out / "e2_headwear_styles.png")
    cells = [[on_bg(A.render(p, hat=k, outfit=o)) for p in ["Sitter", "WalkerRest"] for o in ["Midnight", "Blush"]]
             for k in HEADWEAR]
    grid(cells, HEADWEAR, ["Sitter Midnight", "Sitter Blush", "WRest Midnight", "WRest Blush"],
         title="Headwear follows outfit").save(out / "e3_headwear_outfits.png")
    # (f) random combos on the nursery backdrop
    rng = random.Random(2026)
    bd = Image.open(BACKDROP).convert("RGB")
    combos = []
    for i in range(6):
        combo = dict(skin=rng.choice(list(SKIN)), hair=rng.choice(hair), style=rng.choice(STYLES),
                     outfit=rng.choice(outfits), hat=rng.choice(["None"] + HEADWEAR))
        pose = POSES[i % len(POSES) if i < 4 else (i * 3) % 8]
        combos.append((pose, combo))
    tiles = []
    for pose, combo in combos:
        b = bd.copy()
        b.thumbnail((720, 720))
        im = Image.fromarray(A.render(pose, **combo), "RGBA")
        scale = min(0.48 * b.height / im.height, 0.80 * b.width / im.width)
        im = im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)
        bb = b.convert("RGBA")
        bb.alpha_composite(im, ((bb.width - im.width) // 2, bb.height - im.height - int(0.08 * bb.height)))
        d = ImageDraw.Draw(bb)
        d.rectangle([0, 0, bb.width, 30], fill=(255, 255, 255, 200))
        d.text((8, 7), f"{pose}: " + ", ".join(f"{v}" for v in combo.values()), fill=(50, 40, 30), font=font(12))
        tiles.append(bb.convert("RGB"))
    W = tiles[0].width
    H = tiles[0].height
    sheet = Image.new("RGB", (W * 3, H * 2))
    for i, t in enumerate(tiles):
        sheet.paste(t.resize((W, H)), ((i % 3) * W, (i // 3) * H))
    sheet.save(out / "f_combos.png")
    print("wrote", sorted(p.name for p in out.glob("*.png")))


if __name__ == "__main__":
    main()
