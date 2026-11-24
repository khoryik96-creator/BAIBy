"""Tiny layered rasteriser: anti-aliased cairo coverage masks composited in numpy.

Everything is drawn at ``SS`` x supersampling (coordinates are given in final 1x
pixels; cairo scales them) and box-downsampled at the end, so edges get cairo's
anti-aliasing *and* a 2x2 average.

Each paint carries four region weights (R, G, B, A in 0..255, the recolour
contract) that are composited with exactly the same coverage as the colour, so
the region map comes out with matching anti-aliased edges: an outline painted
over coat fades the coat weight out in proportion to its coverage.

Overlay capture: a paint can be tagged (``tag="marking"``, ``tag="collar"``).
A canvas built with ``capture=<tag>`` draws layers with that tag into the main
image *and* into a separate overlay buffer; untagged layers drawn afterwards
occlude the overlay buffer.  Layers with any other tag are skipped.  The base
image is the canvas with ``capture=None`` (all tagged layers skipped), so an
overlay composited over the base reproduces the tagged render (exactly where
nothing occludes the overlay, approximately on the anti-aliased rims of the
outlines drawn over it).
"""

from __future__ import annotations

import math

import cairo
import numpy as np
from scipy import ndimage

SS = 2  # supersampling factor


class Canvas:
    def __init__(self, width: int, height: int, capture: str | None = None, ss: int = SS):
        self.W, self.H, self.ss = width, height, ss
        self.w, self.h = width * ss, height * ss
        self.capture = capture
        shape = (self.h, self.w)
        # premultiplied colour, alpha, premultiplied region weights
        self.rgb = np.zeros(shape + (3,))
        self.a = np.zeros(shape)
        self.wt = np.zeros(shape + (4,))
        self.ov_rgb = np.zeros(shape + (3,))
        self.ov_a = np.zeros(shape)
        self.ov_wt = np.zeros(shape + (4,))
        yy, xx = np.mgrid[0:self.h, 0:self.w]
        self.X = (xx + 0.5) / ss  # pixel-centre coordinates in 1x units
        self.Y = (yy + 0.5) / ss

    # ------------------------------------------------------------ coverage masks
    def mask(self, draw) -> np.ndarray:
        """Coverage (0..1 float) of whatever ``draw(ctx)`` fills/strokes."""
        surf = cairo.ImageSurface(cairo.FORMAT_A8, self.w, self.h)
        ctx = cairo.Context(surf)
        ctx.set_antialias(cairo.ANTIALIAS_BEST)
        ctx.scale(self.ss, self.ss)
        ctx.set_source_rgba(0, 0, 0, 1)
        ctx.set_line_cap(cairo.LINE_CAP_ROUND)
        ctx.set_line_join(cairo.LINE_JOIN_ROUND)
        draw(ctx)
        surf.flush()
        stride = surf.get_stride()
        buf = np.ndarray((self.h, stride), dtype=np.uint8, buffer=surf.get_data())
        return buf[:, : self.w].astype(np.float64) / 255.0

    def fill_mask(self, path) -> np.ndarray:
        def d(ctx):
            path(ctx)
            ctx.fill()
        return self.mask(d)

    def stroke_mask(self, path, width: float) -> np.ndarray:
        def d(ctx):
            path(ctx)
            ctx.set_line_width(width)
            ctx.stroke()
        return self.mask(d)

    # ------------------------------------------------------------ mask helpers
    def shift(self, m: np.ndarray, dx: float, dy: float) -> np.ndarray:
        """Translate a mask by (dx, dy) 1x pixels (bilinear, zero fill)."""
        return ndimage.shift(m, (dy * self.ss, dx * self.ss), order=1, mode="constant", cval=0.0)

    def blur(self, m: np.ndarray, sigma: float) -> np.ndarray:
        return ndimage.gaussian_filter(m, sigma * self.ss, mode="constant")

    # ------------------------------------------------------------ painting
    def paint(self, m, color, weights=(0, 0, 0, 0), tag: str | None = None, glaze: bool = False):
        """Paint ``color`` (RGB 0..255, scalar triple or HxWx3 array) with coverage ``m``.

        ``glaze=True`` marks a translucent untagged layer (blush, soft tints): instead of
        occluding the captured overlay it is painted *into* it, which keeps
        overlay-over-base exact for partial coverage.
        """
        if tag is not None and tag != self.capture:
            return
        m = np.clip(m, 0.0, 1.0)
        col = np.asarray(color, dtype=np.float64)
        if col.ndim == 1:
            col = col[None, None, :]
        wts = np.asarray(weights, dtype=np.float64)
        if wts.ndim == 1:
            wts = wts[None, None, :]
        mm = m[..., None]
        inv = 1.0 - m
        self.rgb = col * mm + self.rgb * inv[..., None]
        self.wt = wts * mm + self.wt * inv[..., None]
        self.a = m + self.a * inv
        if tag is None and glaze:
            self.ov_rgb = col * (mm * self.ov_a[..., None]) + self.ov_rgb * inv[..., None]
            self.ov_wt = wts * (mm * self.ov_a[..., None]) + self.ov_wt * inv[..., None]
        elif tag is None:
            # untagged content occludes the captured overlay
            self.ov_rgb *= inv[..., None]
            self.ov_wt *= inv[..., None]
            self.ov_a *= inv
        else:
            self.ov_rgb = col * mm + self.ov_rgb * inv[..., None]
            self.ov_wt = wts * mm + self.ov_wt * inv[..., None]
            self.ov_a = m + self.ov_a * inv

    # ------------------------------------------------------------ output
    def _down(self, rgb, a, wt):
        s = self.ss
        H, W = self.H, self.W

        def box(x):
            return x.reshape(H, s, W, s, *x.shape[2:]).mean(axis=(1, 3))

        rgb, a, wt = box(rgb), box(a), box(wt)
        a8 = np.rint(np.clip(a * 255.0, 0, 255))
        safe = np.where(a > 1e-9, a, 1.0)
        col = np.rint(np.clip(rgb / safe[..., None], 0, 255))
        w = np.rint(np.clip(wt / safe[..., None], 0, 255))
        col[a8 == 0] = 0
        w[a8 == 0] = 0
        # rounding can push the sum to 256..258: trim from the largest weight
        total = w.sum(axis=-1)
        for y, x in zip(*np.nonzero(total > 255)):
            v = w[y, x]
            while v.sum() > 255:
                v[int(np.argmax(v))] -= 1
        img = np.dstack([col, a8]).astype(np.uint8)
        return img, w.astype(np.uint8)

    def image(self):
        """(RGBA uint8, region map uint8) at 1x."""
        return self._down(self.rgb, self.a, self.wt)

    def overlay(self):
        """(RGBA uint8, region map uint8) of the captured overlay at 1x (full canvas)."""
        return self._down(self.ov_rgb, self.ov_a, self.ov_wt)


# ---------------------------------------------------------------- path helpers

def ellipse(cx, cy, rx, ry, rot=0.0):
    def p(ctx):
        ctx.save()
        ctx.translate(cx, cy)
        ctx.rotate(rot)
        ctx.scale(rx, ry)
        ctx.new_sub_path()
        ctx.arc(0, 0, 1, 0, 2 * math.pi)
        ctx.restore()
    return p


def poly(points, closed=True):
    def p(ctx):
        ctx.move_to(*points[0])
        for q in points[1:]:
            ctx.line_to(*q)
        if closed:
            ctx.close_path()
    return p


def catmull(points, closed=True, tension=0.5):
    """Smooth path through ``points`` (Catmull-Rom converted to cubic Beziers)."""
    pts = [tuple(map(float, q)) for q in points]
    n = len(pts)

    def p(ctx):
        ctx.move_to(*pts[0])
        segs = n if closed else n - 1
        for i in range(segs):
            p0 = pts[(i - 1) % n] if (closed or i > 0) else pts[0]
            p1 = pts[i]
            p2 = pts[(i + 1) % n]
            p3 = pts[(i + 2) % n] if (closed or i + 2 < n) else pts[-1]
            k = tension / 3.0 * 2.0
            c1 = (p1[0] + (p2[0] - p0[0]) * k / 2, p1[1] + (p2[1] - p0[1]) * k / 2)
            c2 = (p2[0] - (p3[0] - p1[0]) * k / 2, p2[1] - (p3[1] - p1[1]) * k / 2)
            ctx.curve_to(*c1, *c2, *p2)
        if closed:
            ctx.close_path()
    return p


def bezier_points(p0, p1, p2, p3, n=64):
    t = np.linspace(0, 1, n)[:, None]
    a, b, c, d = (np.asarray(q, float) for q in (p0, p1, p2, p3))
    pts = ((1 - t) ** 3) * a + 3 * ((1 - t) ** 2) * t * b + 3 * (1 - t) * t * t * c + t ** 3 * d
    return pts


def chain_points(beziers, n=64):
    """Concatenate cubic Bezier segments [(p0,p1,p2,p3), ...] into one polyline."""
    out = []
    for i, seg in enumerate(beziers):
        pts = bezier_points(*seg, n=n)
        out.append(pts if i == 0 else pts[1:])
    return np.concatenate(out)


def polyline(points):
    pts = np.asarray(points, float)

    def p(ctx):
        ctx.move_to(*pts[0])
        for q in pts[1:]:
            ctx.line_to(*q)
    return p


def tapered(points, width, taper=(0.15, 0.15), profile=None):
    """Closed brush-stroke polygon along a polyline: full ``width`` in the middle,
    tapering to ``taper`` x width at the ends (round-ish by a sine profile)."""
    pts = np.asarray(points, float)
    if len(pts) == 2:
        pts = np.linspace(pts[0], pts[1], 16)
    d = np.gradient(pts, axis=0)
    d /= np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-9)
    nrm = np.stack([-d[:, 1], d[:, 0]], axis=1)
    t = np.linspace(0, 1, len(pts))
    if profile is None:
        prof = np.sin(np.pi * t) ** 0.6
        prof = np.maximum(prof, np.where(t < 0.5, taper[0], taper[1]))
    else:
        prof = profile(t)
    hw = (width / 2.0) * prof[:, None]
    left = pts + nrm * hw
    right = pts - nrm * hw
    ring = np.concatenate([left, right[::-1]])
    return poly([tuple(q) for q in ring])


def rotate_pts(points, angle, origin=(0.0, 0.0)):
    pts = np.asarray(points, float) - origin
    c, s = math.cos(angle), math.sin(angle)
    return pts @ np.array([[c, s], [-s, c]]) + origin
