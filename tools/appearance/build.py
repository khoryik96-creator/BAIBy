#!/usr/bin/env python3
"""Build the baby appearance assets (Base, region maps, hair styles, headwear, layout).

    python3 tools/appearance/build.py

Deterministic: the same inputs always give byte-identical outputs, and existing
.meta GUIDs are kept so reruns never churn Unity references.
"""

from __future__ import annotations

import json
import sys

import numpy as np
from scipy import ndimage as ndi

from common import (HEADWEAR, OUT_DIR, POSES, STYLES, HeadFrame, ensure_meta, read_bytes_png,
                    load_pose, write_bytes_png)
from heads import HEADS
from regions import (CLASSES, LUMA, PROTO, blush_soften, hair_skin_mix, line_multiply,
                     soft_weights, weights_to_u8)
from separate import build_base, skull_coverage
from skinnorm import normalized_pose
import hairstyles
import headwear


def face_mask(frame, shape):
    """Skull area (eyes, eye whites, highlights live here): no outfit classes."""
    return skull_coverage(frame, shape, scale=1.0) > 0.01


def measure(poses, zones, faces):
    acc = {k: [] for k in CLASSES}
    dark = []
    for name, img in poses.items():
        rgb, a = img[..., :3], img[..., 3]
        solid = ndi.binary_erosion(a > 200, iterations=2)
        z, core = zones[name]
        face = faces[name]
        for k in CLASSES:
            d = np.linalg.norm(rgb - PROTO[k], axis=-1)
            m = solid & (d < 28)
            if k == "hair":
                m &= ndi.binary_erosion(core, iterations=2)
            elif k == "skin":
                m &= ~z & face
            else:
                m &= ~face
            m = ndi.binary_erosion(m, iterations=1)
            acc[k].append(rgb[m])
        lum = rgb @ LUMA
        dark.append(rgb[solid & (lum < 62)])
    ref = {k: np.median(np.concatenate(v), axis=0) for k, v in acc.items()}
    ref["outline"] = np.median(np.concatenate(dark), axis=0)
    return ref


def classify_base(img, face, protos):
    rgb, a = img[..., :3], img[..., 3]
    full = np.ones(a.shape, bool)
    allow = {"skin": full, "hair": ~full, "main": ~face, "accent": ~face}
    w = soft_weights(rgb, a, protos, allow)
    w = line_multiply(rgb, a, w, protos, face)
    return blush_soften(rgb, w, protos)


def classify_original(img, face, zone, core, protos):
    """The whole original illustration (hair included), for the Curls overlay map.

    Inside the hair zone hair-coloured pixels are hair and skin-coloured ones
    skin, with hair/skin mixes split between both; outside it the classes are
    the same as for Base (so outfit or face pixels that end up in the overlay
    keep their outfit/skin weights).
    """
    rgb, a = img[..., :3], img[..., 3]
    ds = np.linalg.norm(rgb - protos["skin"], axis=-1)
    skinish = ds < 48
    hz = ndi.binary_dilation(zone, iterations=2)
    # hair weight only on the hair mass itself (its core plus the anti-aliased
    # rim); thin strands and the hair's shadow on the forehead stay skin
    # shading, so light hair colours never blow them out into bright streaks
    bulk = core        # the connected hair mass (with its locks); loose bits are not hair
    mass = ndi.binary_dilation(bulk, iterations=4) & hz
    # fabric-coloured pixels (the outfit edge under the curls) are never hair
    fabric = ((np.linalg.norm(rgb - protos["main"], axis=-1) < 45)
              | (np.linalg.norm(rgb - protos["accent"], axis=-1) < 45)) & ~face
    fabric = ndi.binary_dilation(fabric, iterations=1) & ~face & ~core
    allow = {"skin": (~mass | skinish) & ~fabric, "hair": mass & ~skinish & ~fabric,
             "main": ~face & (~mass | fabric), "accent": ~face & (~mass | fabric)}
    w, best = soft_weights(rgb, a, protos, allow, return_best=True)
    w = hair_skin_mix(rgb, a, protos, w, best, mass)
    # stray hair-weighted islands (strand bits cut off from the hair mass) turn
    # into skin shading too, so no light dots or patches appear on the forehead
    hw = w[..., 1] > 0.25
    lab, n = ndi.label(hw, structure=np.ones((3, 3)))
    if n:
        big = ndi.binary_erosion(bulk, iterations=2)
        keep_ids = np.unique(lab[big & hw])
        stray = hw & ~np.isin(lab, keep_ids[keep_ids > 0])
        stray = ndi.binary_dilation(stray, iterations=1) & (w[..., 1] > 0) & ~big
        w[stray, 0] = np.minimum(1.0, w[stray, 0] + w[stray, 1])
        w[stray, 1] = 0
    w = line_multiply(rgb, a, w, protos, face)
    return blush_soften(rgb, w, protos)


def crop_overlay(rgba, wts):
    a = rgba[..., 3]
    ys, xs = np.nonzero(a > 0.5)
    if len(xs) == 0:
        raise RuntimeError("empty overlay")
    x0, x1 = int(xs.min()), int(xs.max()) + 1
    y0, y1 = int(ys.min()), int(ys.max()) + 1
    return rgba[y0:y1, x0:x1], wts[y0:y1, x0:x1], x0, y0


def finalize(rgba, wts):
    """Quantise an RGBA float image + weights to uint8-valued arrays."""
    out = np.clip(np.rint(rgba), 0, 255)
    out[..., :3][out[..., 3] == 0] = 0
    q = weights_to_u8(wts)
    q[out[..., 3] == 0] = 0
    return out.astype(np.float32), q


def build_only_hats(kinds, only=None):
    """Draw only the named headwear against the *shipped* Base and hair overlays and
    merge them into layout.json; every other file stays untouched.

    A full rebuild is deterministic on one machine, but numpy/scipy/Pillow versions
    shift a few anti-aliased pixels of the Base and hair; adding a hat must not
    churn them. The hats are fitted to exactly the hair the game composites under them.
    """
    lp = OUT_DIR / "layout.json"
    layout = json.loads(lp.read_text())
    overlays = {o["path"]: o for o in layout["overlays"]}
    poses = {p: normalized_pose(p) for p in POSES}
    dark = []
    for img in poses.values():
        rgb, a = img[..., :3], img[..., 3]
        solid = ndi.binary_erosion(a > 200, iterations=2)
        dark.append(rgb[solid & ((rgb @ LUMA) < 62)])
    r = layout["reference"]
    qref = {"skin": np.array(r["skin"], float), "hair": np.array(r["hair"], float),
            "main": np.array(r["outfitMain"], float), "accent": np.array(r["outfitAccent"], float),
            "outline": np.median(np.concatenate(dark), axis=0)}
    written = []
    for p in POSES:
        if only and p not in only:
            continue
        img = poses[p]
        h, w = img.shape[:2]
        base = read_bytes_png(OUT_DIR / f"Base/{p}.bytes")
        union = np.zeros((h, w), bool)
        for s in STYLES:
            o = overlays[f"Hair/{s}/{p}"]
            hair = read_bytes_png(OUT_DIR / f"Hair/{s}/{p}.bytes")
            union[o["y"]:o["y"] + o["height"], o["x"]:o["x"] + o["width"]] |= hair[..., 3] > 8
        ctx = dict(pose=p, frame=HeadFrame(HEADS[p]), head=HEADS[p], shape=(h, w), ref=qref,
                   base=base, original=img, hair_zone=None, hair_union=union)
        for kind in kinds:
            rgba, wts = headwear.draw(kind, ctx)
            o_rgba, o_w = finalize(rgba, wts)
            o_rgba, o_w, x0, y0 = crop_overlay(o_rgba, o_w)
            rel = f"Headwear/{kind}/{p}"
            write_bytes_png(OUT_DIR / f"{rel}.bytes", o_rgba)
            write_bytes_png(OUT_DIR / f"{rel}Regions.bytes", o_w)
            overlays[rel] = dict(path=rel, pose=p, x=x0, y=y0, width=int(o_rgba.shape[1]), height=int(o_rgba.shape[0]))
            written.append(rel)
        print(f"{p}: {', '.join(kinds)}", flush=True)
    layout["overlays"] = sorted(overlays.values(), key=lambda o: (
        POSES.index(o["pose"]), (STYLES + HEADWEAR).index(o["path"].split("/")[1])))
    text = json.dumps(layout, indent=1) + "\n"
    if lp.read_text() != text:
        lp.write_text(text)
    for kind in kinds:
        ensure_meta(OUT_DIR / "Headwear" / kind, folder=True)
    for rel in written:
        ensure_meta(OUT_DIR / f"{rel}.bytes", folder=False)
        ensure_meta(OUT_DIR / f"{rel}Regions.bytes", folder=False)


def main(argv):
    if "--only-hats" in argv:
        only = set(argv[argv.index("--poses") + 1].split(",")) if "--poses" in argv else None
        build_only_hats(argv[argv.index("--only-hats") + 1].split(","), only)
        return
    only = set(argv[argv.index("--poses") + 1].split(",")) if "--poses" in argv else set(POSES)
    skip_hats = "--no-hats" in argv
    poses = {p: normalized_pose(p) for p in POSES}
    frames = {p: HeadFrame(HEADS[p]) for p in POSES}
    bases, zones, faces = {}, {}, {}
    protos0 = dict(PROTO)
    for p in POSES:
        base, z, core = build_base(poses[p], frames[p], HEADS[p], protos0)
        bases[p] = base
        zones[p] = (z, core)
        faces[p] = face_mask(frames[p], z.shape)
    ref = measure(poses, zones, faces)
    protos = dict(ref)
    # rebuild with measured outline colour
    for p in POSES:
        base, z, core = build_base(poses[p], frames[p], HEADS[p], protos)
        bases[p] = base
        zones[p] = (z, core)

    layout = {
        "reference": {
            "skin": [int(round(v)) for v in ref["skin"]],
            "hair": [int(round(v)) for v in ref["hair"]],
            "outfitMain": [int(round(v)) for v in ref["main"]],
            "outfitAccent": [int(round(v)) for v in ref["accent"]],
        },
        "poses": [],
        "overlays": [],
    }
    # quantised refs are what the runtime uses: draw new art against them
    qref = {
        "skin": np.array(layout["reference"]["skin"], float),
        "hair": np.array(layout["reference"]["hair"], float),
        "main": np.array(layout["reference"]["outfitMain"], float),
        "accent": np.array(layout["reference"]["outfitAccent"], float),
        "outline": ref["outline"],
    }
    total = 0
    written = []

    def emit(rel, rgba, wts):
        nonlocal total
        total += write_bytes_png(OUT_DIR / f"{rel}.bytes", rgba)
        total += write_bytes_png(OUT_DIR / f"{rel}Regions.bytes", wts)
        written.append(rel)

    old = {}
    lp = OUT_DIR / "layout.json"
    if lp.exists():
        old = {o["path"]: o for o in json.loads(lp.read_text())["overlays"]}

    def keep(rel, p):
        o = old.get(rel)
        if o is None:
            raise RuntimeError(f"--poses/--no-hats need an existing {rel}")
        layout["overlays"].append(o)
        written.append(rel)

    for p in POSES:
        img = poses[p]
        h, w = img.shape[:2]
        layout["poses"].append({"name": p, "width": int(w), "height": int(h)})
        if p not in only:
            for rel in ([f"Hair/{s}/{p}" for s in STYLES] + [f"Headwear/{k}/{p}" for k in HEADWEAR]):
                keep(rel, p)
            written.append(f"Base/{p}")
            continue
        base = bases[p]
        z, core = zones[p]
        bw = classify_base(base, faces[p], protos)
        bw[..., 1] = 0
        b_rgba, b_w = finalize(base, bw)
        emit(f"Base/{p}", b_rgba, b_w)

        # Curls = the original pixels wherever the Base changed
        oi = np.rint(img)
        diff = (oi[..., 3] != b_rgba[..., 3]) | ((oi[..., 3] > 0) & (np.abs(oi[..., :3] - b_rgba[..., :3]).max(axis=-1) > 0))
        changed = (ndi.binary_dilation(z, iterations=1) | diff) & (img[..., 3] > 0)
        curls = np.zeros_like(img)
        curls[changed] = img[changed]
        cw = classify_original(img, faces[p], z, core, protos)
        cw[~changed] = 0
        # where the overlay repeats the Base's outfit (fabric and its outline
        # around the hair), it carries the Base's own outfit weights
        same = (changed & (np.abs(oi[..., :3] - b_rgba[..., :3]).max(axis=-1) <= 16)
                & (b_rgba[..., 3] > 128) & (b_w[..., 2] + b_w[..., 3] > 0))
        cw[same] = b_w[same] / 255.0
        c_rgba, c_w = finalize(curls, cw)
        c_rgba, c_w, x0, y0 = crop_overlay(c_rgba, c_w)
        emit(f"Hair/Curls/{p}", c_rgba, c_w)
        layout["overlays"].append(dict(path=f"Hair/Curls/{p}", pose=p, x=x0, y=y0,
                                       width=int(c_rgba.shape[1]), height=int(c_rgba.shape[0])))

        ctx = dict(pose=p, frame=frames[p], head=HEADS[p], shape=(h, w), ref=qref,
                   base=b_rgba, original=img, hair_zone=z)
        union = curls[..., 3] > 8
        for style in STYLES[1:]:
            rgba, wts = hairstyles.draw(style, ctx)
            union |= rgba[..., 3] > 8
            o_rgba, o_w = finalize(rgba, wts)
            o_rgba, o_w, x0, y0 = crop_overlay(o_rgba, o_w)
            emit(f"Hair/{style}/{p}", o_rgba, o_w)
            layout["overlays"].append(dict(path=f"Hair/{style}/{p}", pose=p, x=x0, y=y0,
                                           width=int(o_rgba.shape[1]), height=int(o_rgba.shape[0])))
        ctx["hair_union"] = union
        for kind in HEADWEAR:
            if skip_hats:
                keep(f"Headwear/{kind}/{p}", p)
                continue
            rgba, wts = headwear.draw(kind, ctx)
            o_rgba, o_w = finalize(rgba, wts)
            o_rgba, o_w, x0, y0 = crop_overlay(o_rgba, o_w)
            emit(f"Headwear/{kind}/{p}", o_rgba, o_w)
            layout["overlays"].append(dict(path=f"Headwear/{kind}/{p}", pose=p, x=x0, y=y0,
                                           width=int(o_rgba.shape[1]), height=int(o_rgba.shape[0])))
        print(f"{p}: done", flush=True)

    layout["overlays"].sort(key=lambda o: (POSES.index(o["pose"]), (STYLES + HEADWEAR).index(o["path"].split("/")[1])))
    text = json.dumps(layout, indent=1) + "\n"
    if not lp.exists() or lp.read_text() != text:
        lp.write_text(text)
    total += len(text.encode())

    # Unity metas for every folder and file we own
    ensure_meta(OUT_DIR, folder=True)
    folders = set()
    for rel in written:
        parts = rel.split("/")[:-1]
        for i in range(1, len(parts) + 1):
            folders.add(OUT_DIR.joinpath(*parts[:i]))
    for f in sorted(folders):
        ensure_meta(f, folder=True)
    for rel in written:
        ensure_meta(OUT_DIR / f"{rel}.bytes", folder=False)
        ensure_meta(OUT_DIR / f"{rel}Regions.bytes", folder=False)
    ensure_meta(lp, folder=False)
    print("reference", layout["reference"], "outline", np.round(ref["outline"]).tolist())
    print(f"total asset bytes: {total}")


if __name__ == "__main__":
    main(sys.argv[1:])
