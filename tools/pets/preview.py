#!/usr/bin/env python3
"""Contact sheets for the PLACEHOLDER cat art, built only from the shipped files.

    python3 tools/pets/preview.py [--out DIR]

Recolouring and compositing use tools/appearance/recolor.py (the baby contract
math, mirroring AppearanceRecolor.cs): composite base -> markings -> Calico dark
patches (pre-recoloured with the coat's dark target) -> collar, then one recolour
pass with targets [coat main, markings, eyes, accent].

Sheets:
  breeds.png   every breed x every pose, breed default coat and eyes
  coats.png    every coat of every breed (Young and Grown awake), cycling the breed's allowed eye colours
  rest.png     every breed's rest poses with alternate coats
  collar.png   collar accessory with the baby outfit accent palette (palette COLLAR_ACCENTS overrides)
  wear.png     collar, bandana and bow on every pose, in free and pack colours ("Cat bandanas & bows")
  raw.png      unrecoloured base + overlays and their region maps (debug)
  nursery.png  each pose on the nursery backdrop beside the Sitter baby, cat at layout runtimeScale
  detail.png   three cats at 1:1 for close inspection
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
sys.path.insert(0, str(ROOT / "tools" / "appearance"))
sys.path.insert(0, str(HERE))

import recolor as baby  # noqa: E402  (tools/appearance/recolor.py)
from palette import COATS, COLLAR_ACCENTS, DEFAULT_EYES, EYES, allowed_eyes, coat_colors  # noqa: E402

CAT = ROOT / "unity" / "Assets" / "Resources" / "BAIby" / "Pets" / "Cat"
RES = ROOT / "unity" / "Assets" / "Resources" / "BAIby"
DEFAULT_OUT = Path("/tmp/claude-0/-home-user-BAIBy/729cdbf9-e760-56a7-bde4-d84d73210b9a/scratchpad/cat-preview")
POSES = ["YoungAwake", "YoungRest", "GrownAwake", "GrownRest"]
BG = (238, 240, 228, 255)
INK = (77, 41, 23, 255)


def load(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA")).copy()


class Cat:
    def __init__(self):
        self.layout = json.loads((CAT / "layout.json").read_text())
        r = self.layout["reference"]
        self.refs = [tuple(r["coat"]), tuple(r["markings"]), tuple(r["eyes"]), tuple(r["accent"])]
        self.overlays = {o["path"]: o for o in self.layout["overlays"]}

    def layers(self, pose, breed=None, collar=False, dark_targets=None, wear="Collar"):
        """Composite base -> markings -> (Calico) dark patches -> collar.  Overlays with
        gTarget "dark" are recoloured on their own with ``dark_targets`` first and then
        composited with zero region weights; without targets they stay raw."""
        img = load(CAT / "Base" / f"{pose}.bytes")
        reg = load(CAT / "Base" / f"{pose}Regions.bytes")
        paths = []
        if breed and breed != "DomesticShorthair":
            paths.append(f"Markings/{breed}/{pose}")
            paths += [p for p, o in self.overlays.items()
                      if o["pose"] == pose and p.startswith(f"Markings/{breed}/") and o.get("gTarget") == "dark"]
        if collar:
            paths.append(f"Accessories/{wear}/{pose}")
        for p in paths:
            o = self.overlays[p]
            oi = load(CAT / f"{p}.bytes")
            orr = load(CAT / f"{p}Regions.bytes")
            if o.get("gTarget") == "dark" and dark_targets is not None:
                oi = baby.recolor(oi, orr, dark_targets, self.refs)
                orr = np.zeros_like(orr)
            img, reg = baby.composite(img, reg, oi, orr, o["x"], o["y"])
        return img, reg

    def draw(self, pose, breed="DomesticShorthair", coat=None, eyes=None, collar=None, wear="Collar"):
        coat = coat or next(iter(COATS[breed]))
        main, sec, dark = coat_colors(breed, coat)
        eye = EYES[eyes or DEFAULT_EYES[breed]]
        if collar in COLLAR_ACCENTS:
            accent = COLLAR_ACCENTS[collar]
        else:
            accent = baby.OUTFIT[collar][1] if collar and baby.OUTFIT[collar] else self.refs[3]
        img, reg = self.layers(pose, breed, collar=collar is not None, dark_targets=[main, dark, eye, accent], wear=wear)
        out = baby.recolor(img, reg, [main, sec, eye, accent], self.refs)
        return Image.fromarray(out, "RGBA")


def font(size):
    for name in ("DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def grid(cells, cols, cell_w, cell_h, scale, title=None, pad=12):
    """cells: list of (PIL image or None, label)."""
    rows = (len(cells) + cols - 1) // cols
    top = 44 if title else 0
    sheet = Image.new("RGBA", (cols * cell_w + pad * 2, rows * cell_h + pad * 2 + top), BG)
    d = ImageDraw.Draw(sheet)
    if title:
        d.text((pad, 10), title, fill=INK, font=font(22))
    f = font(14)
    for i, (im, label) in enumerate(cells):
        cx = pad + (i % cols) * cell_w
        cy = top + pad + (i // cols) * cell_h
        if im is not None:
            if scale != 1:
                im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
            sheet.alpha_composite(im, (cx + (cell_w - im.width) // 2, cy + (cell_h - 24 - im.height)))
        d.text((cx + 6, cy + cell_h - 20), label, fill=INK, font=f)
    return sheet


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = ap.parse_args()
    out = args.out
    out.mkdir(parents=True, exist_ok=True)
    cat = Cat()
    breeds = list(COATS)
    s = 0.5

    cells = []
    for b in breeds:
        for p in POSES:
            cells.append((cat.draw(p, b), f"{b} / {p}"))
    grid(cells, 4, 350, 340, s, "Placeholder cat: breed x pose (default coat + eyes)").save(out / "breeds.png")

    cells = []
    k = 0
    for b in breeds:
        eye_cycle = allowed_eyes(b)          # Siamese: blue only
        for coat in COATS[b]:
            for p in ("YoungAwake", "GrownAwake"):
                e = eye_cycle[k % len(eye_cycle)]
                cells.append((cat.draw(p, b, coat, e), f"{b} {coat} / {e}"))
            k += 1
    grid(cells, 6, 260, 330, 0.5, "Coats per breed (recoloured with the baby formula)").save(out / "coats.png")

    cells = []
    for b in breeds:
        for coat in list(COATS[b])[:2] if len(COATS[b]) > 1 else list(COATS[b]):
            for p in ("YoungRest", "GrownRest"):
                cells.append((cat.draw(p, b, coat), f"{b} {coat} / {p}"))
    grid(cells, 4, 350, 270, 0.5, "Rest poses, two coats per breed").save(out / "rest.png")

    cells = []
    for i, name in enumerate(baby.OUTFIT):
        b = breeds[i % len(breeds)]
        p = POSES[i % 4]
        cells.append((cat.draw(p, b, collar=name), f"Collar {name} / {b}"))
    grid(cells, 4, 350, 340, 0.5, "Collar accessory (region A) with the outfit accent palette").save(out / "collar.png")

    cells = []
    colours = ["Sky", "Butter", "Lilac", "Terracotta"]
    for i, p in enumerate(POSES):
        for j, wear in enumerate(("Collar", "Bandana", "Bow")):
            colour = colours[(i + j) % len(colours)]
            cells.append((cat.draw(p, breeds[(i + j) % len(breeds)], collar=colour, wear=wear), f"{wear} {colour} / {p}"))
    grid(cells, 3, 350, 340, 0.5, "Neck wear: collar (free), bandana and bow (Cat bandanas & bows)").save(out / "wear.png")

    cells = []
    for p in POSES:
        img, reg = cat.layers(p)
        cells.append((Image.fromarray(img), f"Base {p}"))
        rg = reg.copy()
        rg[..., 3] = 255
        rgb = np.zeros_like(rg)
        rgb[..., 0], rgb[..., 1], rgb[..., 2] = reg[..., 0], reg[..., 1], np.maximum(reg[..., 2], reg[..., 3])
        rgb[..., 3] = 255
        cells.append((Image.fromarray(rgb), f"Regions {p}"))
        for b in ("Tabby", "Calico"):
            img, reg = cat.layers(p, b, collar=True)
            cells.append((Image.fromarray(img), f"{b}+collar raw"))
    grid(cells, 4, 350, 340, 0.5, "Raw (unrecoloured) layers and region maps (R coat, G markings, B eyes/A accent)").save(out / "raw.png")

    # nursery comparison: the baby at 1:1, the cat at layout.json "runtimeScale"
    # (Young ~0.55, Grown ~0.7 of the baby-sized canvases), one cat per backdrop so
    # nothing overlaps: Sitter + Young awake, Sitter + Grown awake, SitterRest +
    # Young rest, SitterRest + Grown rest
    rs = cat.layout["runtimeScale"]

    def backdrop():
        return Image.open(RES / "Nursery" / "Backdrop.png").convert("RGBA")

    def scaled(im, k):
        return im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)

    sitter = Image.open(RES / "Baby" / "Sitter.png").convert("RGBA")
    sitter_rest = Image.open(RES / "Baby" / "SitterRest.png").convert("RGBA")
    specs = [(sitter, "YoungAwake", ("Tabby", "Ginger", "Green", "Sky")),
             (sitter, "GrownAwake", ("Tuxedo", "Black", "Amber", "Terracotta")),
             (sitter_rest, "YoungRest", ("Calico", "Tricolor", None, None)),
             (sitter_rest, "GrownRest", ("Siamese", "Seal", None, "Blush"))]
    panels = []
    for baby_im, pose, (b, coat, eyes, collar) in specs:
        bd = backdrop()
        bd.alpha_composite(baby_im, (16, 1470 - baby_im.height))
        k = rs["grown"] if pose.startswith("Grown") else rs["young"]
        im = scaled(cat.draw(pose, b, coat, eyes, collar=collar), k)
        # right-aligned; nudge forward (down) until no visible pixel overlaps the baby
        x = bd.width - im.width - 12
        ba = np.zeros((bd.height, bd.width), bool)
        ba[1470 - baby_im.height:1470, 16:16 + baby_im.width] = np.asarray(baby_im)[..., 3] > 8
        ca = np.asarray(im)[..., 3] > 8
        y = 1490
        while y < bd.height and (ba[y - im.height:y, x:x + im.width] & ca).any():
            y += 10
        bd.alpha_composite(im, (x, y - im.height))
        panels.append(bd.crop((0, 900, bd.width, 1672)))
    w, h = panels[0].size
    sheet = Image.new("RGBA", (w * 2 + 20, h * 2 + 20), BG)
    for i, pnl in enumerate(panels):
        sheet.alpha_composite(pnl, ((i % 2) * (w + 20), (i // 2) * (h + 20)))
    sheet = sheet.resize((sheet.width * 2 // 3, sheet.height * 2 // 3), Image.LANCZOS)
    sheet.save(out / "nursery.png")

    # close-up at 1:1 for detail checks
    cells = [(cat.draw("YoungAwake", "Siamese"), "Siamese Seal"), (cat.draw("YoungAwake", "Calico"), "Calico Tricolor"),
             (cat.draw("GrownAwake", "Tabby"), "Tabby Brown")]
    grid(cells, 3, 470, 640, 1.0, "1:1 detail").save(out / "detail.png")
    print("wrote", out)


if __name__ == "__main__":
    main()
