#!/usr/bin/env python3
"""Self-check of the generated baby appearance assets against the shared contract.

    python3 tools/appearance/check.py

Verifies: every contract file (and its .meta) exists; layout.json shape and
JsonUtility-friendly types; Base/region sizes match the pose PNGs; region
weights sum <= 255 and are 0 where the image is transparent; overlays lie
inside the pose bounds, match their declared size and never touch the pose
border (no hard cut-offs); Base + Curls reproduces the original illustration
(skin-normalised for the rest poses, see skinnorm.py); overlay pixels that
repeat the Base's outfit keep their outfit weights (no unrecoloured seams); hats
cover every hair style above their brim.  Exits non-zero on any failure.
"""

from __future__ import annotations

import json
import re
import sys

import numpy as np
from PIL import Image

from common import BABY_DIR, HEADWEAR, OUT_DIR, POSES, STYLES, HeadFrame
from heads import HEADS
from headwear import COVER_LINES, hat_frame
from recolor import composite
from skinnorm import normalized_pose

GUID = re.compile(r"^guid: [0-9a-f]{32}$", re.M)
failures = []
warnings = []


def fail(msg):
    failures.append(msg)
    print("FAIL", msg)


def load(path):
    return np.asarray(Image.open(path).convert("RGBA"))


def check_meta(path, folder):
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        fail(f"missing meta {meta.relative_to(OUT_DIR.parent)}")
        return None
    text = meta.read_text()
    m = GUID.search(text)
    if not m:
        fail(f"bad guid in {meta.name}")
        return None
    want = "folderAsset: yes" if folder else "TextScriptImporter:"
    if want not in text:
        fail(f"{meta.name} lacks {want!r}")
    return m.group(0)


def main():
    guids = []
    lp = OUT_DIR / "layout.json"
    if not lp.exists():
        fail("layout.json missing")
        return
    layout = json.loads(lp.read_text())
    guids.append(check_meta(lp, False))
    ref = layout.get("reference", {})
    for k in ("skin", "hair", "outfitMain", "outfitAccent"):
        v = ref.get(k)
        if not (isinstance(v, list) and len(v) == 3 and all(isinstance(c, int) and 1 <= c <= 255 for c in v)):
            fail(f"reference.{k} must be 3 ints in 1..255, got {v}")
    poses = {p["name"]: p for p in layout.get("poses", [])}
    if sorted(poses) != sorted(POSES):
        fail(f"poses mismatch: {sorted(poses)}")
    overlays = {o["path"]: o for o in layout.get("overlays", [])}
    want = {f"Hair/{s}/{p}" for s in STYLES for p in POSES} | {f"Headwear/{k}/{p}" for k in HEADWEAR for p in POSES}
    if set(overlays) != want:
        fail(f"overlay set mismatch: missing {sorted(want - set(overlays))[:5]} extra {sorted(set(overlays) - want)[:5]}")
    for o in layout.get("overlays", []):
        for key in ("path", "pose", "x", "y", "width", "height"):
            if key not in o:
                fail(f"overlay {o} lacks {key}")
        for key in ("x", "y", "width", "height"):
            if not isinstance(o.get(key), int):
                fail(f"overlay {o['path']}.{key} not int")

    folders = {OUT_DIR}
    total = lp.stat().st_size
    maes = {}
    for p in POSES:
        orig = np.clip(np.rint(normalized_pose(p)), 0, 255).astype(np.uint8)
        if orig.shape != load(BABY_DIR / f"{p}.png").shape:
            fail(f"{p}: normalised pose shape mismatch")
        H, W = orig.shape[:2]
        if p in poses and (poses[p]["width"], poses[p]["height"]) != (W, H):
            fail(f"layout size for {p} != png size {W}x{H}")
        assets = {}
        for rel in [f"Base/{p}"] + [f"Hair/{s}/{p}" for s in STYLES] + [f"Headwear/{k}/{p}" for k in HEADWEAR]:
            ip = OUT_DIR / f"{rel}.bytes"
            rp = OUT_DIR / f"{rel}Regions.bytes"
            if not ip.exists() or not rp.exists():
                fail(f"missing {rel}(.Regions).bytes")
                continue
            total += ip.stat().st_size + rp.stat().st_size
            folders.add(ip.parent)
            folders.add(ip.parent.parent)
            guids.append(check_meta(ip, False))
            guids.append(check_meta(rp, False))
            img, reg = load(ip), load(rp)
            assets[rel] = (img, reg)
            if img.shape != reg.shape:
                fail(f"{rel}: image {img.shape} vs regions {reg.shape}")
                continue
            s = reg.astype(int).sum(axis=-1)
            if (s > 255).any():
                fail(f"{rel}: {int((s > 255).sum())} px with weight sum > 255")
            if ((img[..., 3] == 0) & (s > 0)).any():
                fail(f"{rel}: weights on fully transparent pixels")
            if rel.startswith("Base/"):
                if img.shape[:2] != (H, W):
                    fail(f"{rel}: size {img.shape[1]}x{img.shape[0]} != pose {W}x{H}")
            else:
                o = overlays.get(rel)
                if o is None:
                    continue
                if (o["width"], o["height"]) != (img.shape[1], img.shape[0]):
                    fail(f"{rel}: declared {o['width']}x{o['height']} != file {img.shape[1]}x{img.shape[0]}")
                if o["pose"] != p:
                    fail(f"{rel}: pose field {o['pose']}")
                if o["x"] < 0 or o["y"] < 0 or o["x"] + o["width"] > W or o["y"] + o["height"] > H:
                    fail(f"{rel}: overlay {o['x']},{o['y']} {o['width']}x{o['height']} outside pose {W}x{H}")
                if img[..., 3].max() == 0:
                    fail(f"{rel}: empty overlay")
                # a hard cut-off where an overlay runs into the pose image edge
                ox, oy = o["x"], o["y"]
                opaque = img[..., 3] > 128
                edge = 0
                if ox == 0:
                    edge += int(opaque[:, 0].sum())
                if oy == 0:
                    edge += int(opaque[0, :].sum())
                if ox + img.shape[1] == W:
                    edge += int(opaque[:, -1].sum())
                if oy + img.shape[0] == H:
                    edge += int(opaque[-1, :].sum())
                if edge:
                    fail(f"{rel}: {edge} opaque px on the pose border (cut off)")
        if f"Base/{p}" not in assets or f"Hair/Curls/{p}" not in assets:
            continue
        # Base + Curls reproduces the original
        b, br = assets[f"Base/{p}"]
        c, cr = assets[f"Hair/Curls/{p}"]
        o = overlays[f"Hair/Curls/{p}"]
        img, _ = composite(b, br, c, cr, o["x"], o["y"])
        oa = orig.astype(np.float64)
        ia = img.astype(np.float64)
        # compare premultiplied colour + alpha (colour of invisible pixels is irrelevant)
        pm_o = np.concatenate([oa[..., :3] * oa[..., 3:] / 255, oa[..., 3:]], -1)
        pm_i = np.concatenate([ia[..., :3] * ia[..., 3:] / 255, ia[..., 3:]], -1)
        err = np.abs(pm_o - pm_i)
        mae = float(err.mean())
        mx = float(err.max())
        bad = int((err.max(axis=-1) > 6).sum())
        maes[p] = (mae, mx, bad)
        print(f"{p:12s} Base+Curls vs original: mean abs err {mae:.4f}, max {mx:.1f}, px>6: {bad}")
        # straight "over" never lowers alpha, so where the rebuilt skull covers a
        # spot that was background in the art (gaps between curl tips and the
        # face, the temple notch the new outline bridges) Base + Curls cannot be
        # exact: a few hundred anti-aliased pixels; everything else must match
        if mae > 0.25 or bad > 600:
            fail(f"{p}: Base+Curls differs from the original (mae {mae:.3f}, {bad} px > 6)")
        # overlay pixels that repeat the Base's outfit must keep outfit weights,
        # or every non-default outfit shows an unrecoloured strip there
        bi = b.astype(int)
        for rel, (oi_, or_) in assets.items():
            if rel.startswith("Base/"):
                continue
            o2 = overlays[rel]
            ys_, xs_ = o2["y"], o2["x"]
            hh, ww = oi_.shape[:2]
            bsub = bi[ys_:ys_ + hh, xs_:xs_ + ww]
            bm = br[ys_:ys_ + hh, xs_:xs_ + ww].astype(int)
            same = np.abs(oi_[..., :3].astype(int) - bsub[..., :3]).max(axis=-1) <= 16
            seam = ((oi_[..., 3] > 128) & same & (bm[..., 2] + bm[..., 3] > 100)
                    & (or_[..., 2].astype(int) + or_[..., 3] < 30))
            n = int(seam.sum())
            if n > 12:
                fail(f"{rel}: {n} px repeat the Base outfit without outfit weights (seam)")
        # hats hide every hair style around the crown above their brim
        frame = hat_frame(dict(frame=HeadFrame(HEADS[p]), head=HEADS[p]))
        ys, xs = np.mgrid[0:H, 0:W]
        u, v = frame.to_local(xs + 0.5, ys + 0.5)
        ang = np.degrees(np.arctan2(v / frame.top, u))
        crown = (ang > 55) & (ang < 125)   # the top of the head; side puffs may peek out
        for k, line in COVER_LINES.items():
            hw, _ = assets.get(f"Headwear/{k}/{p}", (None, None))
            if hw is None:
                continue
            ho = overlays[f"Headwear/{k}/{p}"]
            hat = np.zeros((H, W), bool)
            hat[ho["y"]:ho["y"] + ho["height"], ho["x"]:ho["x"] + ho["width"]] = hw[..., 3] > 64
            for s in STYLES:
                hi, _ = assets[f"Hair/{s}/{p}"]
                so = overlays[f"Hair/{s}/{p}"]
                hm = np.zeros((H, W), bool)
                hm[so["y"]:so["y"] + so["height"], so["x"]:so["x"] + so["width"]] = hi[..., 3] > 128
                poke = hm & ~hat & crown & (v > line(u))
                n = int(poke.sum())
                if n > 60:
                    fail(f"{p} {k} over {s}: {n} hair px visible above the brim around the crown")
                elif n > 20:
                    warnings.append(f"{p} {k} over {s}: {n} hair px visible above the brim around the crown")
    for f in sorted(folders):
        if f.exists():
            guids.append(check_meta(f, True))
    g = [x for x in guids if x]
    if len(g) != len(set(g)):
        fail("duplicate GUIDs")
    for wmsg in warnings:
        print("WARN", wmsg)
    print(f"files checked: {len(g)} metas; total asset bytes (bytes+json): {total}")
    if failures:
        print(f"{len(failures)} failure(s)")
        sys.exit(1)
    print("OK")


if __name__ == "__main__":
    main()
