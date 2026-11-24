#!/usr/bin/env python3
"""Build the PLACEHOLDER pet art: base poses, region maps, breed marking overlays,
accessory overlays, layout.json and Unity .meta files, for the cat (P1) and the
hamster (P2).

    python3 tools/pets/build.py                    # every species (a couple of minutes on 4 cores)
    python3 tools/pets/build.py --species Hamster  # one species
    python3 tools/pets/build.py --jobs 1           # serial

Cat: markings for the Tabby, Tuxedo, Siamese and Calico (and Calico's dark
patches), and the neck wear (Collar, and the Bandana and Bow of the "Cat
bandanas & bows" pack).  Hamster: the dorsal stripe and Panda patches (dark
overlays), the Roborovski's brow spots, the Teddy bear's long coat, a tiny
bow, and its habitat (``Habitat/Back`` and ``Habitat/Front``, habitat.py).

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

from cat import BREEDS, POSES, REF_ACCENT, REF_COAT, REF_IRIS, REF_MARK, RUNTIME_SCALE, WEAR  # noqa: E402
from palette import breed_entries, collar_entries, eye_entries, hamster_entries, hamster_eye_entries  # noqa: E402

sys.path.insert(0, str(HERE.parent / "appearance"))
from recolor import composite  # noqa: E402  (the baby contract composite)

ROOT = HERE.parent.parent
PETS_DIR = ROOT / "unity" / "Assets" / "Resources" / "BAIby" / "Pets"
OUT_DIR = PETS_DIR / "Cat"
MARGIN = 4
SPECIES = ["Cat", "Hamster"]
HAB_W, HAB_H, HAB_FLOOR = 760, 480, 64   # habitat.W, habitat.H, habitat.FLOOR

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


# ---------------------------------------------------------------- species specs
class CatSpec:
    name = "Cat"
    poses = POSES
    # overlays whose pixels may lie outside the base silhouette (none for the cat)
    features = ()

    @staticmethod
    def jobs(pose):
        jobs = [(pose, "marking", b) for b in BREEDS]
        jobs.append((pose, "darkpatch", "Calico"))
        jobs += [(pose, "wear", w) for w in WEAR]
        return jobs

    @staticmethod
    def overlays(pose):
        overlays = [(f"Markings/{b}/{pose}", (pose, "marking", b), "secondary") for b in BREEDS]
        overlays.append((f"Markings/Calico/{pose}Dark", (pose, "darkpatch", "Calico"), "dark"))
        overlays += [(f"Accessories/{w}/{pose}", (pose, "wear", w), "secondary") for w in WEAR]
        return overlays

    @staticmethod
    def kinds():
        return [f"Markings/{b}" for b in BREEDS] + [f"Accessories/{w}" for w in WEAR]

    @staticmethod
    def layout_head():
        return {
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
        }

    @staticmethod
    def palette():
        return {"coats": breed_entries(), "eyes": eye_entries(), "collarAccents": collar_entries()}


class HamsterSpec:
    name = "Hamster"

    def __init__(self):
        import hamster
        self.h = hamster
        self.poses = hamster.POSES
        self.features = tuple(f"Markings/{b}/" for b in hamster.FEATURE)

    def jobs(self, pose):
        jobs = [(pose, "marking" if kind == "g" else "darkpatch", b) for b, kind in self.h.MARKINGS]
        jobs += [(pose, "wear", w) for w in self.h.WEAR]
        return jobs

    def overlays(self, pose):
        out = []
        for b, kind in self.h.MARKINGS:
            if kind == "g":
                out.append((f"Markings/{b}/{pose}", (pose, "marking", b), "secondary"))
            else:
                out.append((f"Markings/{b}/{pose}Dark", (pose, "darkpatch", b), "dark"))
        out += [(f"Accessories/{w}/{pose}", (pose, "wear", w), "secondary") for w in self.h.WEAR]
        return out

    def kinds(self):
        return [f"Markings/{b}" for b, _ in self.h.MARKINGS] + [f"Accessories/{w}" for w in self.h.WEAR]

    def layout_head(self):
        h = self.h
        return {
            "placeholder": True,
            "species": "Hamster",
            "reference": {
                "coat": list(h.REF_COAT),
                "markings": list(h.REF_MARK),
                "eyes": list(h.REF_IRIS),
                "accent": list(h.REF_ACCENT),
            },
            "runtimeScale": {"young": h.RUNTIME_SCALE["Young"], "grown": h.RUNTIME_SCALE["Grown"]},
            # the cage drawn round the hamster in the shared pet room (habitat.py), at the baby's pose-pixel scale
            "habitat": {"back": "Habitat/Back", "front": "Habitat/Front", "width": HAB_W, "height": HAB_H,
                        "floor": HAB_FLOOR},
        }

    @staticmethod
    def extras(out_dir):
        import habitat
        write_png(out_dir / "Habitat" / "Back.bytes", habitat.back().image()[0])
        write_png(out_dir / "Habitat" / "Front.bytes", habitat.front().image()[0])

    @staticmethod
    def palette():
        return {"coats": hamster_entries(), "eyes": hamster_eye_entries(), "collarAccents": []}


def spec_for(name):
    return CatSpec() if name == "Cat" else HamsterSpec()


def render_job(job):
    """(species, pose, kind, name) -> (img, regions) at full canvas size."""
    species, pose, kind, name = job
    if species == "Cat":
        from poses import render
    else:
        from hamster import render
    if kind == "base":
        cv = render(pose)
        return job, cv.image()
    if kind == "marking":
        cv = render(pose, breed=name, capture="marking")
    elif kind == "darkpatch":
        cv = render(pose, breed=name, capture="darkpatch")
    else:
        cv = render(pose, capture=name.lower())
    return job, cv.overlay() + (cv.image()[0],)


def build(spec, results, poses):
    out_dir = PETS_DIR / spec.name
    layout_path = out_dir / "layout.json"
    old = json.loads(layout_path.read_text()) if layout_path.exists() else {}
    pose_entries = {p["name"]: p for p in old.get("poses", [])}
    overlay_entries = {o["path"]: o for o in old.get("overlays", []) if o["pose"] not in poses}
    S = spec.name

    fidelity = []
    for pose in poses:
        img, reg = results[(S, pose, "base", None)]
        x0, y0, x1, y1 = bbox(img[..., 3])
        # a feature overlay (the Teddy bear's long coat) widens the pose canvas past the base silhouette
        for path, (p, kind, name), _ in spec.overlays(pose):
            if spec.features and path.startswith(spec.features):
                ox0, oy0, ox1, oy1 = bbox(results[(S, p, kind, name)][0][..., 3])
                x0, y0, x1, y1 = min(x0, ox0), min(y0, oy0), max(x1, ox1), max(y1, oy1)
        x0, y0 = max(0, x0 - MARGIN), max(0, y0 - MARGIN)
        x1, y1 = min(img.shape[1], x1 + MARGIN), min(img.shape[0], y1 + MARGIN)
        write_png(out_dir / "Base" / f"{pose}.bytes", img[y0:y1, x0:x1])
        write_png(out_dir / "Base" / f"{pose}Regions.bytes", reg[y0:y1, x0:x1])
        pose_entries[pose] = {"name": pose, "width": x1 - x0, "height": y1 - y0}

        for path, key, gtarget in spec.overlays(pose):
            oimg, oreg, full = results[(S,) + key]
            oimg, oreg, full = oimg[y0:y1, x0:x1], oreg[y0:y1, x0:x1], full[y0:y1, x0:x1]
            # fidelity: overlay composited over the base must reproduce the tagged render
            comp, _ = composite(img[y0:y1, x0:x1], reg[y0:y1, x0:x1], oimg, oreg, 0, 0)
            err = np.abs(comp.astype(int) - full.astype(int)).max(axis=-1)
            fidelity.append((path, int(err.max()), float(err.mean()), int((err > 8).sum())))
            if not oimg[..., 3].any():
                raise SystemExit(f"empty overlay {path}")
            ox0, oy0, ox1, oy1 = bbox(oimg[..., 3])
            write_png(out_dir / f"{path}.bytes", oimg[oy0:oy1, ox0:ox1])
            write_png(out_dir / f"{path}Regions.bytes", oreg[oy0:oy1, ox0:ox1])
            overlay_entries[path] = {"path": path, "pose": pose, "x": ox0, "y": oy0,
                                     "width": ox1 - ox0, "height": oy1 - oy0, "gTarget": gtarget}
        print(f"{S} {pose}: {x1 - x0}x{y1 - y0}")

    order = {p: i for i, p in enumerate(spec.poses)}
    kinds = spec.kinds()
    layout = spec.layout_head()
    layout["poses"] = sorted(pose_entries.values(), key=lambda p: order[p["name"]])
    layout["overlays"] = sorted(overlay_entries.values(),
                                key=lambda o: (order[o["pose"]], kinds.index(o["path"].rsplit("/", 1)[0]), o["path"]))
    layout["palette"] = spec.palette()
    if hasattr(spec, "extras"):
        spec.extras(out_dir)
    text = json.dumps(layout, indent=1) + "\n"
    if not layout_path.exists() or layout_path.read_text() != text:
        layout_path.write_text(text)

    worst = max(fidelity, key=lambda f: f[3])
    print(f"{S} overlay fidelity: worst {worst[0]} max {worst[1]}, mean {worst[2]:.3f}, {worst[3]} px > 8 "
          f"(anti-aliased outline rims over markings)")
    print("wrote", out_dir.relative_to(ROOT))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--jobs", type=int, default=4)
    ap.add_argument("--species", default=",".join(SPECIES))
    ap.add_argument("--poses", default=None, help="comma-separated poses (default: all)")
    args = ap.parse_args()
    specs = [spec_for(name) for name in args.species.split(",")]

    jobs = []
    plan = []
    for spec in specs:
        poses = args.poses.split(",") if args.poses else list(spec.poses)
        plan.append((spec, poses))
        for pose in poses:
            jobs.append((spec.name, pose, "base", None))
            jobs += [(spec.name,) + j for j in spec.jobs(pose)]
    if args.jobs > 1:
        with ProcessPoolExecutor(args.jobs) as ex:
            results = dict(ex.map(render_job, jobs))
    else:
        results = dict(map(render_job, jobs))

    for spec, poses in plan:
        build(spec, results, poses)

    # Unity metas for every folder and file we own
    ensure_meta(PETS_DIR, folder=True)
    for p in sorted(PETS_DIR.rglob("*")):
        if p.suffix == ".meta":
            continue
        ensure_meta(p, folder=p.is_dir())


if __name__ == "__main__":
    main()
