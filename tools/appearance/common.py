"""Shared helpers for the baby appearance pipeline (paths, IO, geometry)."""

from __future__ import annotations

import io
import math
import uuid
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[2]
BABY_DIR = REPO / "unity" / "Assets" / "Resources" / "BAIby" / "Baby"
OUT_DIR = BABY_DIR / "Appearance"
BACKDROP = REPO / "unity" / "Assets" / "Resources" / "BAIby" / "Nursery" / "Backdrop.png"

POSES = ["Newborn", "NewbornRest", "Sitter", "SitterRest",
         "Crawler", "CrawlerRest", "Walker", "WalkerRest"]
STAGES = ["Newborn", "Sitter", "Crawler", "Walker"]
STYLES = ["Curls", "Coils", "Smooth", "Tuft"]
HEADWEAR = ["Bow", "BearBeanie", "BucketHat", "DaisyClip", "PomBeanie", "SunHat"]

# Supersampling factor used when drawing new art.
SS = 4


# --------------------------------------------------------------------------- IO

def load_pose(name: str) -> np.ndarray:
    """Original pose illustration as float32 RGBA in 0..255."""
    return np.asarray(Image.open(BABY_DIR / f"{name}.png").convert("RGBA"), dtype=np.float32)


def to_u8(a: np.ndarray) -> np.ndarray:
    return np.clip(np.rint(a), 0, 255).astype(np.uint8)


def png_bytes(arr: np.ndarray) -> bytes:
    """Encode RGBA uint8 as a deterministic, optimized PNG."""
    buf = io.BytesIO()
    Image.fromarray(to_u8(arr), "RGBA").save(buf, format="PNG", optimize=True, compress_level=9)
    return buf.getvalue()


def write_bytes_png(path: Path, arr: np.ndarray) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)
    data = png_bytes(arr)
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)
    return len(data)


def read_bytes_png(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.float32)


# ------------------------------------------------------------------ Unity metas

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
    """Write a .meta next to ``path`` keeping an existing GUID (stable reruns)."""
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


# -------------------------------------------------------------- head geometry

class HeadFrame:
    """Head-local frame. Units: 1.0 == skull half-width (rx). +v points to the crown."""

    def __init__(self, h: dict):
        self.h = h
        self.cx, self.cy = float(h["cx"]), float(h["cy"])
        self.rx = float(h["rx"])
        self.top = h["ry_top"] / self.rx       # crown height in head units
        self.bot = h["ry_bot"] / self.rx       # chin depth in head units
        self.n_top = float(h.get("n_top", 2.0))
        a = math.radians(h["tilt"])
        self.cos, self.sin = math.cos(a), math.sin(a)

    # pose pixel (x right, y down) -> local (u right, v up), head units
    def to_local(self, x, y):
        dx = (x - self.cx) / self.rx
        dy = (y - self.cy) / self.rx
        u = dx * self.cos + dy * self.sin
        v = dx * self.sin - dy * self.cos
        return u, v

    def to_pose(self, u, v):
        x = self.cx + self.rx * (u * self.cos + v * self.sin)
        y = self.cy + self.rx * (u * self.sin - v * self.cos)
        return x, y

    def skull_radius(self, u, v):
        """<1 inside the skull, 1 on its outline (generalised radius)."""
        u = np.asarray(u, dtype=np.float64)
        v = np.asarray(v, dtype=np.float64)
        n = self.n_top
        top = (np.abs(u) ** n + np.abs(v / self.top) ** n) ** (1.0 / n)
        bot = np.hypot(u, v / self.bot)
        return np.where(v >= 0, top, bot)

    def skull_point(self, theta, scale=1.0):
        """Local point on the skull outline at polar angle theta (0 = right, pi/2 = crown)."""
        c, s = math.cos(theta), math.sin(theta)
        if s >= 0:
            n = self.n_top
            cc = math.copysign(abs(c) ** (2 / n), c)
            ss = abs(s) ** (2 / n)
            return cc * scale, ss * self.top * scale
        return c * scale, s * self.bot * scale


def safe_scale(frame: "HeadFrame", shape, margin_px=4.0):
    """Largest uniform skull scale whose upper outline stays inside the image.

    Hair is drawn inside this so every hat (which may grow up to it) can cover it.
    """
    h, w = shape
    k = 1.0
    while k < 2.2:
        ok = True
        for deg in range(25, 156, 4):
            u, v = frame.skull_point(math.radians(deg), k + 0.005)
            x, y = frame.to_pose(u, v)
            if not (margin_px <= x <= w - margin_px and margin_px <= y <= h - margin_px):
                ok = False
                break
        if not ok:
            break
        k += 0.005
    return k


class HiResGrid:
    """Supersampled pixel-centre grid over a pose-space box (x0, y0, x1, y1)."""

    def __init__(self, box, frame: HeadFrame, ss: int = SS):
        self.box = tuple(int(b) for b in box)
        x0, y0, x1, y1 = self.box
        self.ss = ss
        self.w, self.h = (x1 - x0), (y1 - y0)
        xs = x0 + (np.arange(self.w * ss) + 0.5) / ss
        ys = y0 + (np.arange(self.h * ss) + 0.5) / ss
        self.X, self.Y = np.meshgrid(xs, ys)
        self.frame = frame
        self.U, self.V = frame.to_local(self.X, self.Y)
        # one hi-res pixel in head units
        self.px = 1.0 / (ss * frame.rx)

    def down(self, a: np.ndarray) -> np.ndarray:
        s = self.ss
        if a.ndim == 2:
            return a.reshape(self.h, s, self.w, s).mean(axis=(1, 3))
        return a.reshape(self.h, s, self.w, s, a.shape[-1]).mean(axis=(1, 3))


def push_pull_fill(rgb: np.ndarray, known: np.ndarray, levels: int = 9) -> np.ndarray:
    """Smoothly fill unknown pixels from known ones (pyramid push-pull)."""
    h, w = known.shape
    wgt = known.astype(np.float64)
    col = rgb.astype(np.float64) * wgt[..., None]
    pyr = [(col, wgt)]
    for _ in range(levels):
        c, k = pyr[-1]
        hh, ww = k.shape
        if hh < 2 or ww < 2:
            break
        ph, pw = hh + (hh % 2), ww + (ww % 2)
        c2 = np.zeros((ph, pw, 3)); c2[:hh, :ww] = c
        k2 = np.zeros((ph, pw)); k2[:hh, :ww] = k
        c2 = c2.reshape(ph // 2, 2, pw // 2, 2, 3).sum(axis=(1, 3))
        k2 = k2.reshape(ph // 2, 2, pw // 2, 2).sum(axis=(1, 3))
        pyr.append((c2, k2))
    c, k = pyr[-1]
    est = c / np.maximum(k, 1e-9)[..., None]
    for c, k in reversed(pyr[:-1]):
        hh, ww = k.shape
        up = np.repeat(np.repeat(est, 2, axis=0), 2, axis=1)[:hh, :ww]
        # light blur of the upsampled estimate to avoid blockiness
        from scipy import ndimage as ndi
        up = ndi.uniform_filter(up, size=(3, 3, 1), mode="nearest")
        kk = np.clip(k, 0, 1)[..., None]
        here = c / np.maximum(k, 1e-9)[..., None]
        est = kk * here + (1 - kk) * up
    return est


def over(dst: np.ndarray, src: np.ndarray, x: int = 0, y: int = 0) -> np.ndarray:
    """Straight-alpha 'over' of src (RGBA float 0..255) onto dst at (x, y), y from top."""
    out = dst.copy()
    h, w = src.shape[:2]
    d = out[y:y + h, x:x + w]
    sa = src[..., 3:4] / 255.0
    da = d[..., 3:4] / 255.0
    oa = sa + da * (1 - sa)
    rgb = (src[..., :3] * sa + d[..., :3] * da * (1 - sa)) / np.maximum(oa, 1e-9)
    d[..., :3] = np.where(oa > 0, rgb, 0)
    d[..., 3:4] = oa * 255.0
    out[y:y + h, x:x + w] = d
    return out
