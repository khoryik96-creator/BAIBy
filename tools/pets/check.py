#!/usr/bin/env python3
"""Self-check of the PLACEHOLDER pet assets: the cat (P1), the hamster (P2), the dog (P3), the fish (P4) and the
rabbit (P5).

    python3 tools/pets/check.py

Verifies: every expected file (and its .meta, with a unique GUID and the right
importer) exists; layout.json shape and JsonUtility-friendly types; Base and
region map sizes match layout.json and stay in the baby pose size range; region
weights sum <= 255 and are 0 where the image is transparent; markings carry only
G weight and the collar only A weight; every overlay (including the Calico
dark-patch overlays) has the right gTarget, matches its declared size,
lies inside its pose bounds and over the base silhouette; every palette coat has
main/secondary/dark colours; the outline colour is the baby #4D2917.  Exits non-zero on any failure.

The hamster follows the same rules with its own art: its base also carries G
weight (the light belly and muzzle), its dark overlays are the dorsal stripe and
the Panda's patches, the Roborovski's brow spots are G, the Teddy bear's long
coat (a feature overlay in the main coat, R) may reach past the base
silhouette, and its bow is A.  Its poses are smaller than the baby's.  Each
species' art stays within about 2 MB.

The dog (P3): every shipped breed's overlay carries its ears (a feature overlay in
the main coat R that may reach past the silhouette) and any G markings; the
Beagle's saddle and the Dalmatian's spots are dark overlays; the Corgi stands on
its own short-legged base poses (``breedBases``: ``Base/Corgi{Pose}``), with its
neck wear; the Dachshund has no art (arriving soon).  Its budget is 3 MB: eight
base poses and eleven breeds' ears.

The fish (P4): every breed's overlay carries its fins (a feature overlay in G that
may reach past the silhouette; the Discus's tall disc adds R); the Angelfish's
bars, the Koi's patches, the Clownfish's bands and the Neon tetras' red rear are
dark overlays that stay inside the silhouette; the Neon tetra school stands on its
own base poses (``Base/NeonTetraSchool{Pose}``); there is no neck wear.  Its
habitat is the tank, with ``haze`` (the cloudy water) and ``castle`` (the tank
castle) layers beside the back and front, and a ``swim`` height above the floor.

The rabbit (P5): every breed's overlay carries its ears (a feature overlay in the
main coat R that may reach past the silhouette) and any G markings or fluff; the
Harlequin's half face and bands are a dark overlay that stays inside the silhouette;
the Flemish giant stands on its own larger base poses (``Base/FlemishGiant{Pose}``),
with its neck wear.  Its habitat is the pen, with a ``haze`` layer (the scattered
hay of a missed litter tidy) beside the back and front.  Its taller poses (the ears)
may reach 860 px.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
PETS = ROOT / "unity" / "Assets" / "Resources" / "BAIby" / "Pets"
CAT = PETS / "Cat"
POSES = ["YoungAwake", "YoungRest", "GrownAwake", "GrownRest"]
BREEDS = ["Tabby", "Tuxedo", "Siamese", "Calico"]
WEAR = ["Collar", "Bandana", "Bow"]
SIZE_RANGE = (380, 700)          # baby poses are 409..619 px on each side


class CatRules:
    name = "Cat"
    breeds = BREEDS
    wear = WEAR
    size_range = SIZE_RANGE
    min_side = 300
    base_allowed = (0, 2)

    @staticmethod
    def want_overlays():
        return ({f"Markings/{b}/{p}" for b in BREEDS for p in POSES} | {f"Markings/Calico/{p}Dark" for p in POSES}
                | {f"Accessories/{w}/{p}" for w in WEAR for p in POSES})

    @staticmethod
    def allowed(path):
        return (3,) if path.startswith("Accessories/") else (1,)

    @staticmethod
    def may_leave_silhouette(path):
        return False


class HamsterRules:
    name = "Hamster"
    breeds = ["Roborovski", "TeddyBear", "WinterWhite", "Sapphire", "Campbells", "Panda"]
    wear = ["Bow"]
    size_range = (300, 700)      # small poses: the hamster is drawn smaller than the baby
    min_side = 220
    base_allowed = (0, 1, 2)     # every hamster has a light belly (G)
    dark = ["WinterWhite", "Sapphire", "Campbells", "Panda"]

    @classmethod
    def want_overlays(cls):
        return ({f"Markings/{b}/{p}" for b in ("Roborovski", "TeddyBear") for p in POSES}
                | {f"Markings/{b}/{p}Dark" for b in cls.dark for p in POSES}
                | {f"Accessories/{w}/{p}" for w in cls.wear for p in POSES})

    @staticmethod
    def allowed(path):
        if path.startswith("Accessories/"):
            return (3,)
        return (0,) if path.startswith("Markings/TeddyBear/") else (1,)

    @staticmethod
    def may_leave_silhouette(path):
        return path.startswith("Markings/TeddyBear/")


class DogRules:
    name = "Dog"
    breeds = ["MixedBreed", "Golden", "Beagle", "Shiba", "Corgi", "Poodle", "Husky", "Samoyed", "Pomeranian", "Dalmatian",
              "BorderCollie"]
    wear = WEAR
    size_range = SIZE_RANGE
    min_side = 300
    base_allowed = (0, 1, 2)     # every dog has a light muzzle, bib and paw tips (G)
    dark = ["Beagle", "Dalmatian"]
    variants = ["Corgi"]
    budget = 3_200_000

    @classmethod
    def poses(cls):
        return POSES + [v + p for v in cls.variants for p in POSES]

    @classmethod
    def want_overlays(cls):
        plain = [b for b in cls.breeds if b not in cls.variants]
        return ({f"Markings/{b}/{p}" for b in cls.breeds for p in POSES}
                | {f"Markings/{b}/{p}Dark" for b in cls.dark for p in POSES}
                | {f"Accessories/{w}/{p}" for w in cls.wear for p in cls.poses()}) if plain else set()

    @staticmethod
    def allowed(path):
        if path.startswith("Accessories/"):
            return (3,)
        return (1,) if path.endswith("Dark") else (0, 1)

    @staticmethod
    def required(path):
        # the ears are in the main coat; only some breeds add G markings
        return (3,) if path.startswith("Accessories/") else (1,) if path.endswith("Dark") else (0,)

    @staticmethod
    def may_leave_silhouette(path):
        return path.startswith("Markings/") and not path.endswith("Dark")

    @classmethod
    def pose_of(cls, path):
        """The base pose an overlay composites over: a variant breed's markings sit on its own base."""
        parts = path.split("/")
        if parts[0] == "Markings" and parts[1] in cls.variants:
            return parts[1] + parts[2]
        return None


class FishRules:
    name = "Fish"
    breeds = ["Goldfish", "Guppy", "Betta", "NeonTetraSchool", "Angelfish", "Koi", "Clownfish", "Discus"]
    wear = []
    size_range = (300, 700)      # a fish is drawn smaller than the baby
    min_side = 220
    base_allowed = (0, 1, 2)     # every fish has a light belly and a little side fin (G)
    dark = ["NeonTetraSchool", "Angelfish", "Koi", "Clownfish"]
    variants = ["NeonTetraSchool"]
    budget = 2_500_000           # about 2.4 MB: eight bases, eight breeds' fins and the tank

    @classmethod
    def poses(cls):
        return POSES + [v + p for v in cls.variants for p in POSES]

    @classmethod
    def want_overlays(cls):
        return ({f"Markings/{b}/{p}" for b in cls.breeds for p in POSES}
                | {f"Markings/{b}/{p}Dark" for b in cls.dark for p in POSES})

    @staticmethod
    def allowed(path):
        return (1,) if path.endswith("Dark") else (0, 1)

    @staticmethod
    def required(path):
        # every breed's fins are in the secondary colour (G); the Discus's disc adds the main colour
        return (1,)

    @staticmethod
    def may_leave_silhouette(path):
        return path.startswith("Markings/") and not path.endswith("Dark")

    @classmethod
    def pose_of(cls, path):
        """The base pose an overlay composites over: the Neon tetras' markings sit on their own base."""
        parts = path.split("/")
        if parts[0] == "Markings" and parts[1] in cls.variants:
            pose = parts[2][:-len("Dark")] if parts[2].endswith("Dark") else parts[2]
            return parts[1] + pose
        return None


class RabbitRules(DogRules):
    name = "Rabbit"
    breeds = ["Dutch", "NetherlandDwarf", "HollandLop", "Lionhead", "Angora", "Rex", "FlemishGiant", "Harlequin"]
    wear = WEAR
    size_range = (300, 880)      # upright ears make the sitting poses tall; the Flemish giant is drawn larger
    min_side = 300
    base_allowed = (0, 1, 2)     # every rabbit has light whisker pads, a belly and a cotton tail (G)
    dark = ["Harlequin"]
    variants = ["FlemishGiant"]
    budget = 3_600_000           # eight bases, eight breeds' ears and fluff, the neck wear and the pen

    @classmethod
    def poses(cls):
        return POSES + [v + p for v in cls.variants for p in POSES]

    @classmethod
    def want_overlays(cls):
        return ({f"Markings/{b}/{p}" for b in cls.breeds for p in POSES}
                | {f"Markings/{b}/{p}Dark" for b in cls.dark for p in POSES}
                | {f"Accessories/{w}/{p}" for w in cls.wear for p in cls.poses()})


OUTLINE = np.array([77, 41, 23])
GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)

failures = []


def fail(msg):
    failures.append(msg)
    print("FAIL", msg)


def load(path: Path):
    im = Image.open(path)
    if im.format != "PNG":
        fail(f"{path.name} is not PNG data")
    return np.asarray(im.convert("RGBA"))


def check_meta(path: Path, folder: bool, guids: list):
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        fail(f"missing meta for {path.relative_to(ROOT)}")
        return
    text = meta.read_text()
    m = GUID.search(text)
    if not m:
        fail(f"bad guid in {meta.relative_to(ROOT)}")
        return
    guids.append(m.group(1))
    want = "folderAsset: yes" if folder else "TextScriptImporter:"
    if want not in text:
        fail(f"{meta.relative_to(ROOT)} lacks {want!r}")


def check_regions(name, img, reg, allowed, required=None):
    if img.shape != reg.shape:
        fail(f"{name}: image {img.shape[1::-1]} vs regions {reg.shape[1::-1]}")
        return
    total = reg.astype(int).sum(axis=-1)
    if total.max() > 255:
        fail(f"{name}: region sum {total.max()} > 255")
    if (total[img[..., 3] == 0] > 0).any():
        fail(f"{name}: region weight on transparent pixels")
    for ch in range(4):
        if ch not in allowed and reg[..., ch].any():
            fail(f"{name}: unexpected weight in channel {'RGBA'[ch]}")
    for ch in (allowed if required is None else required):
        if not reg[..., ch].any():
            fail(f"{name}: channel {'RGBA'[ch]} is empty")


def check_species(rules, guids):
    CAT = PETS / rules.name
    lp = CAT / "layout.json"
    if not lp.exists():
        fail(f"{rules.name}/layout.json missing")
        return
    layout = json.loads(lp.read_text())
    if layout.get("placeholder") is not True:
        fail("layout.json must say placeholder: true")
    if layout.get("species") != rules.name:
        fail(f"layout.json names species {layout.get('species')!r}, expected {rules.name!r}")
    ref = layout.get("reference", {})
    for k in ("coat", "markings", "eyes", "accent"):
        v = ref.get(k)
        if not (isinstance(v, list) and len(v) == 3 and all(isinstance(x, int) and 0 < x <= 255 for x in v)):
            fail(f"reference.{k} must be three ints 1..255, got {v!r}")
    rs = layout.get("runtimeScale", {})
    for k in ("young", "grown"):
        v = rs.get(k)
        if not (isinstance(v, (int, float)) and 0.3 <= v <= 1.0):
            fail(f"runtimeScale.{k} must be a number in 0.3..1.0, got {v!r}")
    if rs and rs.get("young", 0) >= rs.get("grown", 0):
        fail("runtimeScale.young must be smaller than runtimeScale.grown")
    poses = {p["name"]: p for p in layout.get("poses", [])}
    want_poses = rules.poses() if hasattr(rules, "poses") else POSES
    if sorted(poses) != sorted(want_poses):
        fail(f"layout poses {sorted(poses)} != {sorted(want_poses)}")
    if sorted(layout.get("breedBases", [])) != sorted(getattr(rules, "variants", [])):
        fail(f"{rules.name} breedBases {layout.get('breedBases')!r} != {getattr(rules, 'variants', [])!r}")
    overlays = {o["path"]: o for o in layout.get("overlays", [])}
    want_overlays = rules.want_overlays()
    if set(overlays) != want_overlays:
        fail(f"overlay set mismatch: missing {sorted(want_overlays - set(overlays))}, "
             f"extra {sorted(set(overlays) - want_overlays)}")
    for o in overlays.values():
        for k in ("x", "y", "width", "height"):
            if not isinstance(o.get(k), int):
                fail(f"{o.get('path')}: {k} must be an int")
        want_target = "dark" if o["path"].endswith("Dark") else "secondary"
        if o.get("gTarget") != want_target:
            fail(f"{o['path']}: gTarget must be {want_target!r}, got {o.get('gTarget')!r}")
    for entry in layout.get("palette", {}).get("coats", []):
        for k in ("main", "secondary", "dark"):
            v = entry.get(k)
            if not (isinstance(v, list) and len(v) == 3 and all(isinstance(x, int) and 0 <= x <= 255 for x in v)):
                fail(f"palette {entry.get('breed')}/{entry.get('coat')}: {k} must be three ints 0..255")

    base_alpha = {}
    for name, p in poses.items():
        ip, rp = CAT / "Base" / f"{name}.bytes", CAT / "Base" / f"{name}Regions.bytes"
        if not ip.exists() or not rp.exists():
            fail(f"missing Base/{name}(.bytes/Regions.bytes)")
            continue
        img, reg = load(ip), load(rp)
        h, w = img.shape[:2]
        if (w, h) != (p["width"], p["height"]):
            fail(f"Base/{name} is {w}x{h}, layout says {p['width']}x{p['height']}")
        if not (rules.size_range[0] <= max(w, h) <= rules.size_range[1] and min(w, h) >= rules.min_side):
            fail(f"{rules.name} Base/{name} size {w}x{h} outside the pose size range")
        check_regions(f"{rules.name} Base/{name}", img, reg, allowed=rules.base_allowed,
                      required=(0, 2) if name.endswith("Awake") else (0,))  # closed eyes have no iris
        # style: the darkest opaque colour is the baby outline
        opaque = img[..., 3] == 255
        dist = np.abs(img[..., :3].astype(int) - OUTLINE).sum(axis=-1)
        if (opaque & (dist <= 6)).mean() < 0.03:
            fail(f"{rules.name} Base/{name}: too few #4D2917 outline pixels")
        base_alpha[name] = img[..., 3]
        check_meta(ip, False, guids)
        check_meta(rp, False, guids)
        print(f"ok {rules.name} Base/{name} {w}x{h}")

    for path, o in sorted(overlays.items()):
        ip, rp = CAT / f"{path}.bytes", CAT / f"{path}Regions.bytes"
        if not ip.exists() or not rp.exists():
            fail(f"missing {path}(.bytes/Regions.bytes)")
            continue
        img, reg = load(ip), load(rp)
        h, w = img.shape[:2]
        if (w, h) != (o["width"], o["height"]):
            fail(f"{path} is {w}x{h}, layout says {o['width']}x{o['height']}")
        pose = poses.get(o["pose"])
        if pose is None:
            fail(f"{path}: unknown pose {o['pose']}")
            continue
        expected_pose = rules.pose_of(path) if hasattr(rules, "pose_of") else None
        if expected_pose is not None and o["pose"] != expected_pose:
            fail(f"{path}: composites over {o['pose']}, expected the breed's own base {expected_pose}")
        if o["x"] < 0 or o["y"] < 0 or o["x"] + w > pose["width"] or o["y"] + h > pose["height"]:
            fail(f"{path}: overlay {o['x']},{o['y']} {w}x{h} outside pose {pose['width']}x{pose['height']}")
            continue
        allowed = rules.allowed(path)
        check_regions(f"{rules.name} {path}", img, reg, allowed,
                      required=rules.required(path) if hasattr(rules, "required") else None)
        if o["pose"] in base_alpha and not rules.may_leave_silhouette(path):
            under = base_alpha[o["pose"]][o["y"]:o["y"] + h, o["x"]:o["x"] + w]
            outside = (img[..., 3] > 8) & (under == 0)
            if outside.any():
                fail(f"{path}: {int(outside.sum())} overlay pixels outside the base silhouette")
        check_meta(ip, False, guids)
        check_meta(rp, False, guids)
        print(f"ok {rules.name} {path} at {o['x']},{o['y']} {w}x{h}")

    # the hamster's and the fish's habitat: a back and a front layer of the declared size, opaque somewhere, floor
    # inside; the fish's tank adds its haze and castle layers and the height it swims at
    habitat = layout.get("habitat")
    if rules.name in ("Hamster", "Fish", "Rabbit") and not habitat:
        fail(f"the {rules.name.lower()} layout needs its habitat")
    if habitat:
        hw, hh, hf = habitat.get("width"), habitat.get("height"), habitat.get("floor")
        if not all(isinstance(v, int) for v in (hw, hh, hf)) or not 0 < hf < hh:
            fail(f"{rules.name} habitat needs int width, height and a floor inside it")
        layers = ["back", "front"]
        if rules.name == "Fish":
            swim = habitat.get("swim")
            if not isinstance(swim, int) or not 0 < swim < hh - hf:
                fail(f"the tank needs an int swim height inside it, got {swim!r}")
            layers += ["haze", "castle"]
        elif rules.name == "Rabbit":
            # the scattered hay of a missed litter tidy, faded in by the room
            layers += ["haze"]
        for key in layers:
            hp = CAT / f"{habitat.get(key)}.bytes"
            if not hp.exists():
                fail(f"missing habitat {key} {hp.relative_to(ROOT)}")
                continue
            img = load(hp)
            if img.shape[1] != hw or img.shape[0] != hh:
                fail(f"habitat {key} is {img.shape[1]}x{img.shape[0]}, layout says {hw}x{hh}")
            # the haze is translucent by design (the rabbit's scattered hay is loose strands); the other layers are
            # opaque somewhere
            if not ((img[..., 3] > 0).any() if key == "haze" else (img[..., 3] == 255).any()):
                fail(f"habitat {key} is empty")
            check_meta(hp, False, guids)
            print(f"ok {rules.name} habitat {key} {img.shape[1]}x{img.shape[0]}")
        if (CAT / "Habitat").is_dir():
            check_meta(CAT / "Habitat", True, guids)

    check_meta(lp, False, guids)
    folders = [CAT, CAT / "Base", CAT / "Markings"] + ([CAT / "Accessories"] if rules.wear else [])
    for folder in folders + [CAT / "Accessories" / w for w in rules.wear] + [CAT / "Markings" / b for b in rules.breeds]:
        if not folder.is_dir():
            fail(f"missing folder {folder.relative_to(ROOT)}")
        else:
            check_meta(folder, True, guids)


def main():
    guids = []
    check_meta(PETS, True, guids)
    for rules in (CatRules, HamsterRules, DogRules, FishRules, RabbitRules):
        check_species(rules, guids)
    if (PETS / "Dog" / "Markings" / "Dachshund").exists():
        fail("the Dachshund has no art yet: it is listed as arriving soon")
    # every file under Pets/ has a meta, no stray metas
    for p in PETS.rglob("*"):
        if p.suffix == ".meta":
            if not p.with_suffix("").exists():
                fail(f"stray meta {p.relative_to(ROOT)}")
        elif not p.with_name(p.name + ".meta").exists():
            fail(f"missing meta for {p.relative_to(ROOT)}")
    # each species' shipped art stays within the Web download budget (about 2 MB)
    if (PETS / "Fish" / "Accessories").exists():
        fail("the fish wears nothing: its tank castle is a habitat layer")
    for rules in (CatRules, HamsterRules, DogRules, FishRules, RabbitRules):
        size = sum(f.stat().st_size for f in (PETS / rules.name).rglob("*") if f.is_file() and f.suffix != ".meta")
        budget = getattr(rules, "budget", 2_000_000)
        if size > budget:
            fail(f"{rules.name} art is {size / 1e6:.2f} MB, over the {budget / 1e6:.0f} MB budget")
        print(f"ok {rules.name} art {size / 1e6:.2f} MB")
    return finish(guids)


def finish(guids):
    dup = {g for g in guids if guids.count(g) > 1}
    if dup:
        fail(f"duplicate GUIDs {sorted(dup)}")
    if failures:
        print(f"\n{len(failures)} failure(s)")
        sys.exit(1)
    print("\nall checks passed")


if __name__ == "__main__":
    main()
