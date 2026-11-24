#!/usr/bin/env python3
"""Self-check of the PLACEHOLDER cat assets.

    python3 tools/pets/check.py

Verifies: every expected file (and its .meta, with a unique GUID and the right
importer) exists; layout.json shape and JsonUtility-friendly types; Base and
region map sizes match layout.json and stay in the baby pose size range; region
weights sum <= 255 and are 0 where the image is transparent; markings carry only
G weight and the collar only A weight; every overlay (including the Calico
dark-patch overlays) has the right gTarget, matches its declared size,
lies inside its pose bounds and over the base silhouette; every palette coat has
main/secondary/dark colours; the outline colour is the baby #4D2917.  Exits non-zero on any failure.
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


def main():
    guids = []
    lp = CAT / "layout.json"
    if not lp.exists():
        fail("layout.json missing")
        return finish(guids)
    layout = json.loads(lp.read_text())
    if layout.get("placeholder") is not True:
        fail("layout.json must say placeholder: true")
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
    if sorted(poses) != sorted(POSES):
        fail(f"layout poses {sorted(poses)} != {sorted(POSES)}")
    overlays = {o["path"]: o for o in layout.get("overlays", [])}
    want_overlays = ({f"Markings/{b}/{p}" for b in BREEDS for p in POSES} | {f"Markings/Calico/{p}Dark" for p in POSES}
                     | {f"Accessories/{w}/{p}" for w in WEAR for p in POSES})
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
        if not (SIZE_RANGE[0] <= max(w, h) <= SIZE_RANGE[1] and min(w, h) >= 300):
            fail(f"Base/{name} size {w}x{h} outside the baby pose range")
        check_regions(f"Base/{name}", img, reg, allowed=(0, 2),
                      required=(0, 2) if name.endswith("Awake") else (0,))  # closed eyes have no iris
        # style: the darkest opaque colour is the baby outline
        opaque = img[..., 3] == 255
        dist = np.abs(img[..., :3].astype(int) - OUTLINE).sum(axis=-1)
        if (opaque & (dist <= 6)).mean() < 0.03:
            fail(f"Base/{name}: too few #4D2917 outline pixels")
        base_alpha[name] = img[..., 3]
        check_meta(ip, False, guids)
        check_meta(rp, False, guids)
        print(f"ok Base/{name} {w}x{h}")

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
        if o["x"] < 0 or o["y"] < 0 or o["x"] + w > pose["width"] or o["y"] + h > pose["height"]:
            fail(f"{path}: overlay {o['x']},{o['y']} {w}x{h} outside pose {pose['width']}x{pose['height']}")
            continue
        allowed = (3,) if path.startswith("Accessories/") else (1,)
        check_regions(path, img, reg, allowed)
        if o["pose"] in base_alpha:
            under = base_alpha[o["pose"]][o["y"]:o["y"] + h, o["x"]:o["x"] + w]
            outside = (img[..., 3] > 8) & (under == 0)
            if outside.any():
                fail(f"{path}: {int(outside.sum())} overlay pixels outside the base silhouette")
        check_meta(ip, False, guids)
        check_meta(rp, False, guids)
        print(f"ok {path} at {o['x']},{o['y']} {w}x{h}")

    check_meta(lp, False, guids)
    for folder in [PETS, CAT, CAT / "Base", CAT / "Markings", CAT / "Accessories"] + [CAT / "Accessories" / w for w in WEAR] + \
            [CAT / "Markings" / b for b in BREEDS]:
        if not folder.is_dir():
            fail(f"missing folder {folder.relative_to(ROOT)}")
        else:
            check_meta(folder, True, guids)
    # every file under Pets/ has a meta, no stray metas
    for p in PETS.rglob("*"):
        if p.suffix == ".meta":
            if not p.with_suffix("").exists():
                fail(f"stray meta {p.relative_to(ROOT)}")
        elif not p.with_name(p.name + ".meta").exists():
            fail(f"missing meta for {p.relative_to(ROOT)}")
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
