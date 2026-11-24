#!/usr/bin/env python3
"""Build the PLACEHOLDER cat art: base poses, region maps, breed marking overlays,
the collar accessory overlay, layout.json and Unity .meta files.

    python3 tools/pets/build.py            # all poses (about a minute on 4 cores)
    python3 tools/pets/build.py --jobs 1   # serial

Deterministic: fixed geometry and seeds, fixed PNG encoder settings; existing
.meta GUIDs are kept, so a rerun without changes produces no diff.
"""

from __future__ import annotations

import argparse
import io
import json
import sys
import uuid
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from cat import BREEDS, POSES, REF_ACCENT, REF_COAT, REF_IRIS, REF_MARK, RUNTIME_SCALE  # noqa: E402
from palette import breed_entries, collar_entries, eye_entries  # noqa: E402

sys.path.insert(0, str(HERE.parent / "appearance"))
from recolor import composite  # noqa: E402  (the baby contract composite)

ROOT = HERE.parent.parent
PETS_DIR = ROOT / "unity" / "Assets" / "Resources" / "BAIby" / "Pets"
OUT_DIR = PETS_DIR / "Cat"
MARGIN = 4

FILE_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def ensure_meta(path: Path, folder: bool) -> None:
    meta = path.with_name(path.name + ".meta")
    guid = None
    if meta.exists():
        for line in meta.read_text().splitlines():
            if line.startswith("guid:"):
                guid = line.split(":", 1)[1].strip()
    if not guid:
        guid = uuid.uuid4().hex
    text = (FOLDER_META if folder else FILE_META).format(guid=guid)
    if not meta.exists() or meta.read_text() != text:
        meta.write_text(text)


def png_bytes(arr: np.ndarray) -> bytes:
    buf = io.BytesIO()
    Image.fromarray(arr, "RGBA").save(buf, format="PNG", compress_level=9, optimize=False)
    return buf.getvalue()


def write_png(path: Path, arr: np.ndarray) -> None:
    data = png_bytes(arr)
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)


def bbox(alpha: np.ndarray):
    ys, xs = np.nonzero(alpha)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def render_job(job):
    """(pose, kind, name) -> (img, regions) at full canvas size."""
    from poses import render
    pose, kind, name = job
    if kind == "base":
        cv = render(pose)
        return job, cv.image()
    if kind == "marking":
        cv = render(pose, breed=name, capture="marking")
    elif kind == "darkpatch":
        cv = render(pose, breed=name, capture="darkpatch")
    else:
        cv = render(pose, capture="collar")
    return job, cv.overlay() + (cv.image()[0],)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--jobs", type=int, default=4)
    ap.add_argument("--poses", default=",".join(POSES))
    args = ap.parse_args()
    poses = args.poses.split(",")

    jobs = []
    for pose in poses:
        jobs.append((pose, "base", None))
        jobs += [(pose, "marking", b) for b in BREEDS]
        jobs.append((pose, "darkpatch", "Calico"))
        jobs.append((pose, "collar", "Collar"))
    if args.jobs > 1:
        with ProcessPoolExecutor(args.jobs) as ex:
            results = dict(ex.map(render_job, jobs))
    else:
        results = dict(map(render_job, jobs))

    layout_path = OUT_DIR / "layout.json"
    old = json.loads(layout_path.read_text()) if layout_path.exists() else {}
    pose_entries = {p["name"]: p for p in old.get("poses", [])}
    overlay_entries = {o["path"]: o for o in old.get("overlays", []) if o["pose"] not in poses}

    fidelity = []
    for pose in poses:
        img, reg = results[(pose, "base", None)]
        x0, y0, x1, y1 = bbox(img[..., 3])
        x0, y0 = max(0, x0 - MARGIN), max(0, y0 - MARGIN)
        x1, y1 = min(img.shape[1], x1 + MARGIN), min(img.shape[0], y1 + MARGIN)
        write_png(OUT_DIR / "Base" / f"{pose}.bytes", img[y0:y1, x0:x1])
        write_png(OUT_DIR / "Base" / f"{pose}Regions.bytes", reg[y0:y1, x0:x1])
        pose_entries[pose] = {"name": pose, "width": x1 - x0, "height": y1 - y0}

        overlays = [(f"Markings/{b}/{pose}", (pose, "marking", b), "secondary") for b in BREEDS]
        overlays.append((f"Markings/Calico/{pose}Dark", (pose, "darkpatch", "Calico"), "dark"))
        overlays.append((f"Accessories/Collar/{pose}", (pose, "collar", "Collar"), "secondary"))
        for path, key, gtarget in overlays:
            oimg, oreg, full = results[key]
            oimg, oreg, full = oimg[y0:y1, x0:x1], oreg[y0:y1, x0:x1], full[y0:y1, x0:x1]
            # fidelity: overlay composited over the base must reproduce the tagged render
            comp, _ = composite(img[y0:y1, x0:x1], reg[y0:y1, x0:x1], oimg, oreg, 0, 0)
            err = np.abs(comp.astype(int) - full.astype(int)).max(axis=-1)
            fidelity.append((path, int(err.max()), float(err.mean()), int((err > 8).sum())))
            if not oimg[..., 3].any():
                raise SystemExit(f"empty overlay {path}")
            ox0, oy0, ox1, oy1 = bbox(oimg[..., 3])
            write_png(OUT_DIR / f"{path}.bytes", oimg[oy0:oy1, ox0:ox1])
            write_png(OUT_DIR / f"{path}Regions.bytes", oreg[oy0:oy1, ox0:ox1])
            overlay_entries[path] = {"path": path, "pose": pose, "x": ox0, "y": oy0,
                                     "width": ox1 - ox0, "height": oy1 - oy0, "gTarget": gtarget}
        print(f"{pose}: {x1 - x0}x{y1 - y0}")

    order = {p: i for i, p in enumerate(POSES)}
    kinds = [f"Markings/{b}" for b in BREEDS] + ["Accessories/Collar"]
    layout = {
        "placeholder": True,
        "species": "Cat",
        "reference": {
            "coat": list(REF_COAT),
            "markings": list(REF_MARK),
            "eyes": list(REF_IRIS),
            "accent": list(REF_ACCENT),
        },
        # suggested on-screen scale of the pet relative to the baby pose pixels (the
        # cat canvases are about baby-sized); preview.py's nursery sheet uses it
        "runtimeScale": {"young": RUNTIME_SCALE["Young"], "grown": RUNTIME_SCALE["Grown"]},
        "poses": sorted(pose_entries.values(), key=lambda p: order[p["name"]]),
        "overlays": sorted(overlay_entries.values(),
                           key=lambda o: (order[o["pose"]], kinds.index(o["path"].rsplit("/", 1)[0]), o["path"])),
        "palette": {"coats": breed_entries(), "eyes": eye_entries(), "collarAccents": collar_entries()},
    }
    text = json.dumps(layout, indent=1) + "\n"
    if not layout_path.exists() or layout_path.read_text() != text:
        layout_path.write_text(text)

    # Unity metas for every folder and file we own
    ensure_meta(PETS_DIR, folder=True)
    for p in sorted(PETS_DIR.rglob("*")):
        if p.suffix == ".meta":
            continue
        ensure_meta(p, folder=p.is_dir())
    worst = max(fidelity, key=lambda f: f[3])
    print(f"overlay fidelity: worst {worst[0]} max {worst[1]}, mean {worst[2]:.3f}, {worst[3]} px > 8 "
          f"(anti-aliased outline rims over markings)")
    print("wrote", OUT_DIR.relative_to(ROOT))


if __name__ == "__main__":
    main()
