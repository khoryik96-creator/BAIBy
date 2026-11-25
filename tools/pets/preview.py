#!/usr/bin/env python3
"""Contact sheets for the PLACEHOLDER pet art (cat, hamster, dog, fish and rabbit), built only from the shipped files.

    python3 tools/pets/preview.py [--out DIR] [--species Cat,Hamster,Dog,Fish,Rabbit]

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

Fish sheets (fish-*.png): breeds x poses, every coat, the fish in its tank (with the castle, at rest and with cloudy
water), and a 1:1 detail sheet.

Rabbit sheets (rabbit-*.png): breeds x poses, every coat, the rest poses, the neck wear, the rabbit in its pen
(with and without scattered hay), the young and grown rabbit and the Flemish giant beside the Sitter baby, and a 1:1
detail sheet.

Hamster sheets (hamster-*.png): breeds x poses, every coat, the rest poses, the
tiny bow, the young and grown hamster beside the Sitter baby and a kitten at their
runtime scales, and a 1:1 detail sheet.
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
from palette import (COATS, COLLAR_ACCENTS, DEFAULT_EYES, DOG_COATS, DOG_EYES, EYES, FISH_COATS, FISH_EYES,  # noqa: E402
                     HAMSTER_COATS, HAMSTER_EYES, RABBIT_COATS, RABBIT_EYES, allowed_eyes, coat_colors, dog_allowed_eyes,
                     dog_coat_colors, fish_allowed_eyes, fish_coat_colors, hamster_allowed_eyes, hamster_coat_colors,
                     rabbit_allowed_eyes, rabbit_coat_colors)

CAT = ROOT / "unity" / "Assets" / "Resources" / "BAIby" / "Pets" / "Cat"
HAMSTER = CAT.parent / "Hamster"
DOG = CAT.parent / "Dog"
FISH = CAT.parent / "Fish"
RABBIT = CAT.parent / "Rabbit"
RES = ROOT / "unity" / "Assets" / "Resources" / "BAIby"
DEFAULT_OUT = Path("/tmp/claude-0/-home-user-BAIBy/729cdbf9-e760-56a7-bde4-d84d73210b9a/scratchpad/cat-preview")
POSES = ["YoungAwake", "YoungRest", "GrownAwake", "GrownRest"]
BG = (238, 240, 228, 255)
INK = (77, 41, 23, 255)


def load(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA")).copy()


class Cat:
    def __init__(self, folder=CAT):
        self.folder = folder
        self.layout = json.loads((folder / "layout.json").read_text())
        r = self.layout["reference"]
        self.refs = [tuple(r["coat"]), tuple(r["markings"]), tuple(r["eyes"]), tuple(r["accent"])]
        self.overlays = {o["path"]: o for o in self.layout["overlays"]}

    def layers(self, pose, breed=None, collar=False, dark_targets=None, wear="Collar"):
        """Composite base -> markings -> (Calico) dark patches -> collar.  Overlays with
        gTarget "dark" are recoloured on their own with ``dark_targets`` first and then
        composited with zero region weights; without targets they stay raw."""
        img = load(self.folder / "Base" / f"{pose}.bytes")
        reg = load(self.folder / "Base" / f"{pose}Regions.bytes")
        paths = []
        if breed and f"Markings/{breed}/{pose}" in self.overlays:
            paths.append(f"Markings/{breed}/{pose}")
        if breed:
            paths += [p for p, o in self.overlays.items()
                      if o["pose"] == pose and p.startswith(f"Markings/{breed}/") and o.get("gTarget") == "dark"]
        if collar:
            paths.append(f"Accessories/{wear}/{pose}")
        for p in paths:
            o = self.overlays[p]
            oi = load(self.folder / f"{p}.bytes")
            orr = load(self.folder / f"{p}Regions.bytes")
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


class Hamster(Cat):
    def __init__(self):
        super().__init__(HAMSTER)

    def draw(self, pose, breed="SyrianGolden", coat=None, eyes=None, collar=None, wear="Bow"):
        coat = coat or next(iter(HAMSTER_COATS[breed]))
        main, sec, dark = hamster_coat_colors(breed, coat)
        eye = HAMSTER_EYES[eyes or "Dark"]
        accent = (baby.OUTFIT[collar][1] if collar and baby.OUTFIT[collar] else self.refs[3]) if collar else self.refs[3]
        img, reg = self.layers(pose, breed, collar=collar is not None, dark_targets=[main, dark, eye, accent], wear=wear)
        out = baby.recolor(img, reg, [main, sec, eye, accent], self.refs)
        return Image.fromarray(out, "RGBA")


class Dog(Cat):
    """The dog: a breed listed in ``breedBases`` (the Corgi) stands on its own base pose ("Corgi" + pose), whose
    markings keep the plain pose's path and whose neck wear follows its pose."""

    def __init__(self):
        super().__init__(DOG)
        self.variants = set(self.layout.get("breedBases", []))

    def layers(self, pose, breed=None, collar=False, dark_targets=None, wear="Collar"):
        shown = breed + pose if breed in self.variants else pose
        img = load(self.folder / "Base" / f"{shown}.bytes")
        reg = load(self.folder / "Base" / f"{shown}Regions.bytes")
        paths = []
        if breed:
            paths += [p for p in (f"Markings/{breed}/{pose}", f"Markings/{breed}/{pose}Dark") if p in self.overlays]
        if collar:
            paths.append(f"Accessories/{wear}/{shown}")
        for p in paths:
            o = self.overlays[p]
            oi = load(self.folder / f"{p}.bytes")
            orr = load(self.folder / f"{p}Regions.bytes")
            if o.get("gTarget") == "dark" and dark_targets is not None:
                oi = baby.recolor(oi, orr, dark_targets, self.refs)
                orr = np.zeros_like(orr)
            img, reg = baby.composite(img, reg, oi, orr, o["x"], o["y"])
        return img, reg

    def draw(self, pose, breed="MixedBreed", coat=None, eyes=None, collar=None, wear="Collar"):
        coat = coat or next(iter(DOG_COATS[breed]))
        main, sec, dark = dog_coat_colors(breed, coat)
        eye = DOG_EYES[eyes or ("Blue" if breed == "Husky" else "Brown")]
        accent = (baby.OUTFIT[collar][1] if collar and baby.OUTFIT[collar] else self.refs[3]) if collar else self.refs[3]
        img, reg = self.layers(pose, breed, collar=collar is not None, dark_targets=[main, dark, eye, accent], wear=wear)
        out = baby.recolor(img, reg, [main, sec, eye, accent], self.refs)
        return Image.fromarray(out, "RGBA")


class Fish(Dog):
    """The fish: the Neon tetra school stands on its own base ("NeonTetraSchool" + pose); no neck wear."""

    def __init__(self):
        Cat.__init__(self, FISH)
        self.variants = set(self.layout.get("breedBases", []))

    def draw(self, pose, breed="Goldfish", coat=None, eyes=None, collar=None, wear="Collar"):
        coat = coat or next(iter(FISH_COATS[breed]))
        main, sec, dark = fish_coat_colors(breed, coat)
        eye = FISH_EYES[eyes or "Dark"]
        img, reg = self.layers(pose, breed, dark_targets=[main, dark, eye, self.refs[3]])
        out = baby.recolor(img, reg, [main, sec, eye, self.refs[3]], self.refs)
        return Image.fromarray(out, "RGBA")

    def tank(self, layer):
        return Image.open(self.folder / f"Habitat/{layer}.bytes").convert("RGBA")

    def in_tank(self, pose, breed, coat=None, castle=False, haze=0.0):
        """The fish in its tank at its runtime scale, as the room draws it: back, castle, fish, haze, front."""
        hab = self.layout["habitat"]
        k = self.layout["runtimeScale"]["grown" if pose.startswith("Grown") else "young"]
        fish = self.draw(pose, breed, coat)
        fish = fish.resize((round(fish.width * k), round(fish.height * k)), Image.LANCZOS)
        sheet = self.tank("Back").copy()
        if castle:
            sheet.alpha_composite(self.tank("Castle"))
        lift = hab["swim"] if pose.endswith("Awake") else hab["swim"] // 3
        y = hab["height"] - hab["floor"] - lift - fish.height
        sheet.alpha_composite(fish, ((hab["width"] - fish.width) // 2, y))
        if haze > 0:
            h = np.asarray(self.tank("Haze")).copy()
            h[..., 3] = (h[..., 3] * haze).astype(np.uint8)
            sheet.alpha_composite(Image.fromarray(h, "RGBA"))
        sheet.alpha_composite(self.tank("Front"))
        return sheet


class Rabbit(Dog):
    """The rabbit: the Flemish giant stands on its own larger base ("FlemishGiant" + pose); the neck wear follows the
    shown pose."""

    def __init__(self):
        Cat.__init__(self, RABBIT)
        self.variants = set(self.layout.get("breedBases", []))

    def draw(self, pose, breed="Dutch", coat=None, eyes=None, collar=None, wear="Collar"):
        coat = coat or next(iter(RABBIT_COATS[breed]))
        main, sec, dark = rabbit_coat_colors(breed, coat)
        eye = RABBIT_EYES[eyes or "Dark"]
        accent = (baby.OUTFIT[collar][1] if collar and baby.OUTFIT[collar] else self.refs[3]) if collar else self.refs[3]
        img, reg = self.layers(pose, breed, collar=collar is not None, dark_targets=[main, dark, eye, accent], wear=wear)
        out = baby.recolor(img, reg, [main, sec, eye, accent], self.refs)
        return Image.fromarray(out, "RGBA")

    def pen(self, layer):
        return Image.open(self.folder / f"Habitat/{layer}.bytes").convert("RGBA")

    def in_pen(self, pose, breed, coat=None, hay=0.0):
        """The rabbit in its pen at its runtime scale, as the room draws it: back, rabbit, scattered hay, front."""
        hab = self.layout["habitat"]
        k = self.layout["runtimeScale"]["grown" if pose.startswith("Grown") else "young"]
        im = self.draw(pose, breed, coat)
        im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
        sheet = self.pen("Back").copy()
        y = hab["height"] - hab["floor"] - im.height + 8
        sheet.alpha_composite(im, ((hab["width"] - im.width) // 2, y))
        if hay > 0:
            h = np.asarray(self.pen("Hay")).copy()
            h[..., 3] = (h[..., 3] * hay).astype(np.uint8)
            sheet.alpha_composite(Image.fromarray(h, "RGBA"))
        sheet.alpha_composite(self.pen("Front"))
        return sheet


def rabbit_sheets(out):
    rab = Rabbit()
    breeds = list(RABBIT_COATS)
    cells = []
    for b in breeds:
        for p in POSES:
            cells.append((rab.draw(p, b), f"{b} / {p}"))
    grid(cells, 4, 360, 400, 0.45, "Placeholder rabbit: breed x pose (default coat)").save(out / "rabbit-breeds.png")

    cells = []
    for b in breeds:
        eyes = rabbit_allowed_eyes(b)
        for k, coat in enumerate(RABBIT_COATS[b]):
            e = eyes[k % len(eyes)]
            cells.append((rab.draw("YoungAwake" if k % 2 == 0 else "GrownAwake", b, coat, e), f"{b} {coat} / {e}"))
    grid(cells, 6, 260, 330, 0.36, "Rabbit coats per breed (recoloured with the baby formula)").save(out / "rabbit-coats.png")

    cells = []
    for b in breeds:
        for p in ("YoungRest", "GrownRest"):
            cells.append((rab.draw(p, b, list(RABBIT_COATS[b])[-1]), f"{b} / {p}"))
    grid(cells, 4, 380, 240, 0.45, "Rabbit rest poses (a loaf, ears laid back)").save(out / "rabbit-rest.png")

    cells = []
    colours = ["Sky", "Butter", "Lilac", "Terracotta"]
    for i, p in enumerate(POSES):
        for j, wear in enumerate(("Collar", "Bandana", "Bow")):
            b = ("Dutch", "HollandLop", "FlemishGiant")[j]
            cells.append((rab.draw(p, b, collar=colours[(i + j) % 4], wear=wear), f"{wear} / {b} {p}"))
    grid(cells, 3, 360, 400, 0.45, "Neck wear (region A) in the outfit accent palette").save(out / "rabbit-wear.png")

    cells = [(rab.in_pen("YoungAwake", "Dutch"), "Dutch, young, in its pen"),
             (rab.in_pen("GrownAwake", "HollandLop"), "Holland lop, grown"),
             (rab.in_pen("GrownRest", "Lionhead"), "Lionhead, resting"),
             (rab.in_pen("GrownAwake", "FlemishGiant"), "Flemish giant"),
             (rab.in_pen("YoungAwake", "Angora", hay=0.5), "Angora, a little scattered hay"),
             (rab.in_pen("GrownAwake", "Harlequin", hay=1.0), "Harlequin, scattered hay (full)")]
    grid(cells, 3, 450, 300, 0.5, "The pen (habitat) and the scattered hay").save(out / "rabbit-pen.png")

    rs = rab.layout["runtimeScale"]
    sitter = Image.open(RES / "Baby" / "Sitter.png").convert("RGBA")

    def scaled(im, k):
        return im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)

    young = scaled(rab.draw("YoungAwake", "Dutch"), rs["young"])
    grown = scaled(rab.draw("GrownAwake", "HollandLop"), rs["grown"])
    giant = scaled(rab.draw("GrownAwake", "FlemishGiant"), rs["grown"])
    h = max(sitter.height, young.height, grown.height, giant.height)
    sheet = Image.new("RGBA", (sitter.width + young.width + grown.width + giant.width + 80, h + 20), BG)
    x = 0
    for im in (sitter, young, grown, giant):
        sheet.alpha_composite(im, (x, h - im.height))
        x += im.width + 20
    sheet.resize((sheet.width // 2, sheet.height // 2), Image.LANCZOS).save(out / "rabbit-nursery.png")

    cells = [(rab.draw("YoungAwake", "Dutch"), "Dutch Black"), (rab.draw("GrownAwake", "Lionhead"), "Lionhead Cream"),
             (rab.draw("YoungAwake", "Angora"), "Angora White"), (rab.draw("GrownRest", "HollandLop"), "Holland lop asleep")]
    grid(cells, 2, 700, 860, 1.0, "Rabbit 1:1 detail").save(out / "rabbit-detail.png")


def fish_sheets(out):
    fish = Fish()
    breeds = list(FISH_COATS)
    cells = []
    for b in breeds:
        for p in POSES:
            cells.append((fish.draw(p, b), f"{b} / {p}"))
    grid(cells, 4, 340, 300, 0.5, "Placeholder fish: breed x pose (default coat)").save(out / "fish-breeds.png")

    cells = []
    for b in breeds:
        eyes = fish_allowed_eyes(b)
        for k, coat in enumerate(FISH_COATS[b]):
            e = eyes[k % len(eyes)]
            cells.append((fish.draw("YoungAwake" if k % 2 == 0 else "GrownAwake", b, coat, e), f"{b} {coat} / {e}"))
    grid(cells, 6, 260, 220, 0.4, "Fish coats per breed (recoloured with the baby formula)").save(out / "fish-coats.png")

    cells = [(fish.in_tank("YoungAwake", "Goldfish"), "Goldfish, young, swimming"),
             (fish.in_tank("GrownAwake", "Betta", castle=True), "Betta, grown, with the tank castle"),
             (fish.in_tank("GrownRest", "Koi"), "Koi, lights off"),
             (fish.in_tank("YoungAwake", "NeonTetraSchool"), "Neon tetra school"),
             (fish.in_tank("GrownAwake", "Clownfish", haze=0.5), "Clownfish, cloudy water (half)"),
             (fish.in_tank("GrownAwake", "Discus", haze=1.0), "Discus, cloudy water (full)")]
    grid(cells, 3, 430, 340, 0.5, "The tank (habitat), the castle and the cloudy water").save(out / "fish-tank.png")

    cells = [(fish.draw("GrownAwake", "Goldfish"), "Goldfish Orange"), (fish.draw("GrownAwake", "Angelfish"), "Angelfish Silver"),
             (fish.draw("YoungAwake", "NeonTetraSchool"), "Neon tetra school"), (fish.draw("GrownRest", "Guppy"), "Guppy asleep")]
    grid(cells, 2, 640, 520, 1.0, "Fish 1:1 detail").save(out / "fish-detail.png")


def dog_sheets(out):
    dog = Dog()
    cat = Cat()
    breeds = list(DOG_COATS)
    cells = []
    for b in breeds:
        for p in POSES:
            cells.append((dog.draw(p, b), f"{b} / {p}"))
    grid(cells, 4, 340, 300, 0.5, "Placeholder dog: breed x pose (default coat; the Dachshund is arriving soon)").save(
        out / "dog-breeds.png")

    cells = []
    k = 0
    for b in breeds:
        eyes = dog_allowed_eyes(b)
        for coat in DOG_COATS[b]:
            e = eyes[k % len(eyes)]
            cells.append((dog.draw("YoungAwake" if k % 2 == 0 else "GrownAwake", b, coat, e), f"{b} {coat} / {e}"))
            k += 1
    grid(cells, 6, 260, 290, 0.48, "Dog coats per breed (recoloured with the baby formula)").save(out / "dog-coats.png")

    cells = []
    for b in breeds:
        for p in ("YoungRest", "GrownRest"):
            cells.append((dog.draw(p, b, list(DOG_COATS[b])[-1]), f"{b} / {p}"))
    grid(cells, 4, 340, 220, 0.5, "Dog rest poses (lying down, chin on paws)").save(out / "dog-rest.png")

    cells = []
    colours = ["Sky", "Butter", "Lilac", "Terracotta", "Blush", "Oat", "Midnight", "Sage"]
    for i, p in enumerate(POSES):
        for j, wear in enumerate(("Collar", "Bandana", "Bow")):
            colour = colours[(i * 3 + j) % len(colours)]
            b = ("MixedBreed", "Corgi", "Husky", "Golden")[i]
            cells.append((dog.draw(p, b, collar=colour, wear=wear), f"{wear} {colour} / {b} {p}"))
    grid(cells, 3, 340, 300, 0.5, "Neck wear (region A) in the outfit accent palette").save(out / "dog-wear.png")

    cells = []
    for p in POSES:
        cells.append((dog.draw(p, "MixedBreed"), f"Mixed breed / {p}"))
        cells.append((dog.draw(p, "Corgi"), f"Corgi (short legs) / {p}"))
    grid(cells, 4, 340, 300, 0.5, "The premium Corgi: its own short-legged base and big ears").save(out / "dog-corgi.png")

    # beside the Sitter baby and a kitten, each at its layout runtimeScale
    rs, crs = dog.layout["runtimeScale"], cat.layout["runtimeScale"]
    sitter = Image.open(RES / "Baby" / "Sitter.png").convert("RGBA")

    def scaled(im, k):
        return im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)

    kitten = scaled(cat.draw("YoungAwake", "Tabby"), crs["young"])
    puppy = scaled(dog.draw("YoungAwake", "MixedBreed"), rs["young"])
    grown = scaled(dog.draw("GrownAwake", "Golden"), rs["grown"])
    corgi = scaled(dog.draw("GrownAwake", "Corgi"), rs["grown"])
    panels = []
    for row in ((kitten, puppy), (grown, corgi)):
        bd = Image.open(RES / "Nursery" / "Backdrop.png").convert("RGBA")
        bd.alpha_composite(sitter, (16, 1470 - sitter.height))
        x = 16 + sitter.width + 10
        for im in row:
            bd.alpha_composite(im, (x, 1500 - im.height))
            x += im.width + 8
        panels.append(bd.crop((0, 900, bd.width, 1672)))
    w, h = panels[0].size
    sheet = Image.new("RGBA", (w * 2 + 20, h), BG)
    for i, pnl in enumerate(panels):
        sheet.alpha_composite(pnl, (i * (w + 20), 0))
    sheet.resize((sheet.width * 2 // 3, sheet.height * 2 // 3), Image.LANCZOS).save(out / "dog-nursery.png")

    cells = [(dog.draw("YoungAwake", "Corgi"), "Corgi Red"), (dog.draw("GrownAwake", "Husky"), "Husky Gray"),
             (dog.draw("YoungAwake", "Dalmatian"), "Dalmatian"), (dog.draw("GrownRest", "Beagle"), "Beagle asleep")]
    grid(cells, 2, 680, 640, 1.0, "Dog 1:1 detail").save(out / "dog-detail.png")


def hamster_sheets(out):
    ham = Hamster()
    cat = Cat()
    breeds = list(HAMSTER_COATS)
    cells = []
    for b in breeds:
        for p in POSES:
            cells.append((ham.draw(p, b), f"{b} / {p}"))
    grid(cells, 4, 320, 270, 0.6, "Placeholder hamster: breed x pose (default coat)").save(out / "hamster-breeds.png")

    cells = []
    k = 0
    for b in breeds:
        eyes = hamster_allowed_eyes(b)
        for coat in HAMSTER_COATS[b]:
            for p in ("YoungAwake", "GrownAwake"):
                e = eyes[k % len(eyes)]
                cells.append((ham.draw(p, b, coat, e), f"{b} {coat} / {e}"))
            k += 1
    grid(cells, 6, 250, 250, 0.5, "Hamster coats per breed (recoloured with the baby formula)").save(out / "hamster-coats.png")

    cells = []
    for b in breeds:
        for coat in list(HAMSTER_COATS[b])[:2]:
            for p in ("YoungRest", "GrownRest"):
                cells.append((ham.draw(p, b, coat), f"{b} {coat} / {p}"))
    grid(cells, 4, 320, 190, 0.5, "Hamster rest poses (curled asleep), two coats per breed").save(out / "hamster-rest.png")

    cells = []
    colours = ["Sky", "Butter", "Lilac", "Terracotta", "Blush", "Oat", "Midnight", "Sage"]
    for i, p in enumerate(POSES):
        for j in range(2):
            colour = colours[(i * 2 + j) % len(colours)]
            cells.append((ham.draw(p, breeds[(i * 2 + j) % len(breeds)], collar=colour), f"Bow {colour} / {p}"))
    grid(cells, 4, 320, 270, 0.6, "A tiny bow (region A) in the outfit accent palette").save(out / "hamster-bow.png")

    # beside the Sitter baby and a kitten, each at its layout runtimeScale
    rs, crs = ham.layout["runtimeScale"], cat.layout["runtimeScale"]
    sitter = Image.open(RES / "Baby" / "Sitter.png").convert("RGBA")

    def scaled(im, k):
        return im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)

    kitten = scaled(cat.draw("YoungAwake", "Tabby"), crs["young"])
    young = scaled(ham.draw("YoungAwake", "SyrianGolden"), rs["young"])
    grown = scaled(ham.draw("GrownAwake", "Panda"), rs["grown"])
    asleep = scaled(ham.draw("YoungRest", "WinterWhite", "Gray"), rs["young"])
    panels = []
    for row in ((kitten, young), (grown, asleep)):
        bd = Image.open(RES / "Nursery" / "Backdrop.png").convert("RGBA")
        bd.alpha_composite(sitter, (16, 1470 - sitter.height))
        x = 16 + sitter.width + 10
        for im in row:
            bd.alpha_composite(im, (x, 1500 - im.height))
            x += im.width + 8
        panels.append(bd.crop((0, 900, bd.width, 1672)))
    w, h = panels[0].size
    sheet = Image.new("RGBA", (w * 2 + 20, h), BG)
    for i, pnl in enumerate(panels):
        sheet.alpha_composite(pnl, (i * (w + 20), 0))
    sheet.resize((sheet.width * 2 // 3, sheet.height * 2 // 3), Image.LANCZOS).save(out / "hamster-nursery.png")

    cells = [(ham.draw("YoungAwake", "TeddyBear"), "Teddy bear Golden"), (ham.draw("GrownAwake", "Campbells"), "Campbell's Gray"),
             (ham.draw("YoungRest", "Panda"), "Panda asleep")]
    grid(cells, 3, 600, 480, 1.0, "Hamster 1:1 detail").save(out / "hamster-detail.png")


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
    ap.add_argument("--species", default="Cat,Hamster,Dog,Fish,Rabbit")
    args = ap.parse_args()
    out = args.out
    out.mkdir(parents=True, exist_ok=True)
    species = args.species.split(",")
    if "Hamster" in species:
        hamster_sheets(out)
    if "Dog" in species:
        dog_sheets(out)
    if "Fish" in species:
        fish_sheets(out)
    if "Rabbit" in species:
        rabbit_sheets(out)
    if "Cat" not in species:
        print("wrote", out)
        return
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
