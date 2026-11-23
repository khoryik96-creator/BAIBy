"""Tiny supersampled painter for drawing new chibi art in a head-local frame.

All shapes are given in head units (1.0 = skull half-width, +v = towards the
crown). Painting happens on a 4x supersampled grid over the whole pose and is
box-filtered down, giving clean anti-aliased edges. Every paint carries a
region vector (skin, hair, main, accent) so the region map is exact.
"""

from __future__ import annotations

import math

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

from common import SS, HeadFrame, HiResGrid

HAIR = np.array([0.0, 1.0, 0.0, 0.0])
MAIN = np.array([0.0, 0.0, 1.0, 0.0])
ACCENT = np.array([0.0, 0.0, 0.0, 1.0])
SKIN = np.array([1.0, 0.0, 0.0, 0.0])
FIXED = np.zeros(4)


class Canvas:
    def __init__(self, frame: HeadFrame, shape, ss: int = SS):
        h, w = shape
        self.frame = frame
        self.g = HiResGrid((0, 0, w, h), frame, ss)
        self.ss = ss
        self.H, self.W = h * ss, w * ss
        self.col = np.zeros((self.H, self.W, 3), np.float32)
        self.alpha = np.zeros((self.H, self.W), np.float32)
        self.wts = np.zeros((self.H, self.W, 4), np.float32)
        self.U, self.V = self.g.U, self.g.V
        self.px = self.g.px                     # one hi-res pixel in head units
        self.pose_px = 1.0 / frame.rx           # one pose pixel in head units

    # ------------------------------------------------------------ coordinates
    def to_hires(self, pts):
        """Local (u, v) points -> hi-res pixel coordinates for PIL drawing."""
        out = []
        for u, v in pts:
            x, y = self.frame.to_pose(u, v)
            out.append((x * self.ss, y * self.ss))
        return out

    # ------------------------------------------------------------ mask makers
    def empty(self):
        return np.zeros((self.H, self.W), bool)

    def polygon(self, pts):
        im = Image.new("L", (self.W, self.H), 0)
        ImageDraw.Draw(im).polygon(self.to_hires(pts), fill=255)
        return np.asarray(im) > 127

    def circle(self, cu, cv, r):
        return (self.U - cu) ** 2 + (self.V - cv) ** 2 < r * r

    def ellipse(self, cu, cv, ru, rv, ang=0.0):
        a = math.radians(ang)
        du, dv = self.U - cu, self.V - cv
        x = du * math.cos(a) + dv * math.sin(a)
        y = -du * math.sin(a) + dv * math.cos(a)
        return (x / ru) ** 2 + (y / rv) ** 2 < 1

    def stroke(self, pts, w0, w1=None, taper=None):
        """Round-capped polyline whose width (head units) goes w0 -> w1.

        ``taper`` (fn of t in 0..1 -> width) overrides linear interpolation.
        """
        if w1 is None:
            w1 = w0
        pts = densify(pts, 0.01)
        im = Image.new("L", (self.W, self.H), 0)
        d = ImageDraw.Draw(im)
        hp = self.to_hires(pts)
        n = len(hp)
        scale = self.frame.rx * self.ss
        for i, (x, y) in enumerate(hp):
            t = i / max(n - 1, 1)
            wd = taper(t) if taper else (w0 + (w1 - w0) * t)
            r = max(wd * scale / 2.0, 0.6)
            d.ellipse([x - r, y - r, x + r, y + r], fill=255)
        return np.asarray(im) > 127

    # ----------------------------------------------------------------- paint
    def paint(self, mask, color, region, opacity=1.0):
        m = mask.astype(np.float32) * float(opacity)
        if not m.any():
            return
        c = np.asarray(color, np.float32)
        self.col = self.col * (1 - m[..., None]) + c * m[..., None]
        self.wts = self.wts * (1 - m[..., None]) + np.asarray(region, np.float32) * m[..., None]
        self.alpha = self.alpha * (1 - m) + m

    def erase(self, mask):
        m = mask.astype(np.float32)
        self.alpha *= (1 - m)

    def touches_border(self, mask, px=2):
        m = int(px * self.ss)
        return bool(mask[:m].any() or mask[-m:].any() or mask[:, :m].any() or mask[:, -m:].any())

    def outline_band(self, sil, width_px):
        """Inner band of ``width_px`` pose pixels along the silhouette edge."""
        d = ndi.distance_transform_edt(sil)
        return sil & (d <= width_px * self.ss)

    def result(self):
        a = self.g.down(self.alpha)
        prem = self.g.down(self.col * self.alpha[..., None])
        pw = self.g.down(self.wts * self.alpha[..., None])
        safe = np.where(a > 1e-6, a, 1.0)[..., None]
        rgb = prem / safe
        wts = pw / safe
        rgba = np.concatenate([rgb, a[..., None] * 255.0], axis=-1)
        rgba[a <= 1e-6] = 0
        wts[a <= 1e-6] = 0
        return rgba, wts


def densify(pts, step):
    out = [pts[0]]
    for (u0, v0), (u1, v1) in zip(pts[:-1], pts[1:]):
        n = max(1, int(math.hypot(u1 - u0, v1 - v0) / step))
        for k in range(1, n + 1):
            t = k / n
            out.append((u0 + (u1 - u0) * t, v0 + (v1 - v0) * t))
    return out


def bezier(p0, p1, p2, p3=None, n=40):
    """Quadratic (3 pts) or cubic (4 pts) Bezier as a list of points."""
    out = []
    for i in range(n + 1):
        t = i / n
        if p3 is None:
            a = (1 - t) ** 2; b = 2 * (1 - t) * t; c = t * t
            out.append((a * p0[0] + b * p1[0] + c * p2[0], a * p0[1] + b * p1[1] + c * p2[1]))
        else:
            a = (1 - t) ** 3; b = 3 * (1 - t) ** 2 * t; c = 3 * (1 - t) * t * t; d = t ** 3
            out.append((a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0],
                        a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1]))
    return out


def spline(pts, n=24):
    """Catmull-Rom through the given points."""
    if len(pts) < 3:
        return list(pts)
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t
                                    + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(2)))
    out.append(pts[-1])
    return out


def shade(c, k):
    return np.clip(np.asarray(c, float) * k, 0, 255)
