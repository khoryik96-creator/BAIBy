"""Placeholder cat art: four poses drawn as anti-aliased vector shapes.

Style targets the baby poses: chibi proportions, big glossy eyes with two
highlights, a large soft peach cheek blush, flat two-tone cel shading (one
hard-edged shadow shape per part, no airbrushed rims or specular sheen) with a
faint grain, and #4D2917 cartoon outlines sized so they match the baby's ~8 px
outline at the runtime draw scale (``set_tier``).  Every coat pixel is painted as
``reference * shade`` so the baby recolour formula (out = s * target / reference)
keeps the shading for any coat.

Painter's order per part: fill (coat, region R) -> breed markings (region G,
tag "marking", or "darkpatch" for Calico's dark patches) -> fixed details ->
outline.  ``render()`` returns a Canvas; see raster.py for how tagged layers
become cropped overlays.
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field, replace

import numpy as np
from scipy.spatial import cKDTree

from raster import (Canvas, catmull, chain_points, ellipse, poly, polyline,
                    rotate_pts, tapered)

# ---------------------------------------------------------------- palette / contract
OUTLINE = (77, 41, 23)        # #4D2917, the baby outline


class L:
    """Line weights for the tier being drawn (``set_tier``).  The cat is shown at
    ``RUNTIME_SCALE`` of its canvas, so the canvas line weight is the baby's ~8 px
    outline divided by that scale: about 8 px on screen at runtime, like the baby."""
    k = 1.0
    ow = 7.5                  # outer outline width (canvas px)
    dw = 4.5                  # interior detail line width


BASE_OW, BASE_DW = 7.5, 4.5   # the weights the head/face geometry was designed at
BABY_OW = 8.0                 # measured outer outline of the baby poses (Sitter/Crawler/Walker)

REF_COAT = (240, 172, 110)    # R: main coat (ginger, the Domestic shorthair default)
REF_MARK = (150, 104, 74)     # G: secondary coat / markings
REF_IRIS = (170, 172, 84)     # B: iris (hazel)
REF_ACCENT = (178, 170, 122)  # A: accessory accent == baby outfitAccent reference

# Calico's dark patches are a separate overlay (tag "darkpatch", G weight) drawn
# with the plain markings reference; the runtime recolours that overlay with the
# coat's own ``dark`` target (layout palette), so the dilute coat gets a light
# blue-grey and the tricolour a charcoal.  See README "Contract".

WR, WG, WB, WA, W0 = (255, 0, 0, 0), (0, 255, 0, 0), (0, 0, 255, 0), (0, 0, 0, 255), (0, 0, 0, 0)

PINK_EAR = (244, 184, 178)
PINK_EAR_DEEP = (232, 160, 158)
NOSE = (236, 146, 146)
BLUSH = (240, 136, 112)       # the baby's peach blush hue, glazed at BLUSH_A (core)
BLUSH_A = 0.78
PUPIL = (58, 33, 24)
WHITE = (255, 252, 246)
HIGHLIGHT = (255, 246, 232)   # warm cream eye highlights, like the baby's
FLOOF = (255, 246, 236)
BELL = (236, 196, 98)
BELL_SHADE = (206, 160, 70)

SHADOW = 0.78                 # one hard-edged cel shadow step per part
CONTACT = 0.80
# Warm, hue-shifted shading like the baby art: a shade factor F becomes F ** WARM per
# channel (F = 0.78 -> x(0.83, 0.78, 0.76)), so shadows stay warm on light coats
# without turning dark coats reddish.
WARM = np.array([0.75, 1.0, 1.08])
GRAIN = 0.010                 # faint painted grain on coat fills (shade-only, recolour-safe)

BREEDS = ["Tabby", "Tuxedo", "Siamese", "Calico"]
# Suggested runtime draw scale relative to the baby art (layout.json "runtimeScale"):
# the Young cat should read as a kitten beside the seated baby, the Grown cat a bit bigger.
RUNTIME_SCALE = {"Young": 0.55, "Grown": 0.70}
POSES = ["YoungAwake", "YoungRest", "GrownAwake", "GrownRest"]
# Neck wear overlays (PetAccessory names): the free collar, then the pet pack's bandana and bow.
WEAR = ["Collar", "Bandana", "Bow"]


def set_tier(tier: str) -> None:
    """Size every line for the scale the tier is drawn at (``RUNTIME_SCALE``)."""
    L.ow = round(BABY_OW / RUNTIME_SCALE[tier] * 1.04, 1)   # Young 15.1, Grown 11.9
    L.k = L.ow / BASE_OW
    L.dw = BASE_DW * L.k


def warm(F):
    return np.maximum(F, 1e-6)[..., None] ** WARM[None, None, :]


def coat(F):
    return np.asarray(REF_COAT, float)[None, None, :] * warm(F)


def mark_color(F):
    return np.asarray(REF_MARK, float)[None, None, :] * warm(F)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def band(x, lo, hi, soft=0.6):
    """1 inside [lo, hi] with ``soft`` px anti-aliased edges."""
    return smoothstep(lo - soft, lo + soft, x) * (1 - smoothstep(hi - soft, hi + soft, x))


def blob(cx, cy, r, seed, amp=0.16, n=180, squash=1.0, rot=0.0):
    """Organic closed blob path (deterministic harmonics)."""
    rng = np.random.default_rng(seed)
    th = np.linspace(0, 2 * np.pi, n, endpoint=False)
    rr = np.ones_like(th)
    for k in (2, 3, 4, 5):
        rr += amp / k ** 0.7 * rng.uniform(-1, 1) * np.cos(k * th + rng.uniform(0, 2 * np.pi))
    pts = np.stack([np.cos(th) * rr * r, np.sin(th) * rr * r * squash], axis=1)
    pts = rotate_pts(pts, rot) + (cx, cy)
    return catmull([tuple(q) for q in pts])


# ---------------------------------------------------------------- frames
@dataclass
class Frame:
    """Uniform-scale rotated local frame (used for the head)."""
    cx: float
    cy: float
    s: float = 1.0
    rot: float = 0.0

    def wrap(self, path):
        def p(ctx):
            ctx.save()
            ctx.translate(self.cx, self.cy)
            ctx.rotate(self.rot)
            ctx.scale(self.s, self.s)
            path(ctx)
            ctx.restore()
        return p

    def local(self, X, Y):
        dx, dy = X - self.cx, Y - self.cy
        c, s = math.cos(-self.rot), math.sin(-self.rot)
        return (dx * c - dy * s) / self.s, (dx * s + dy * c) / self.s

    def pt(self, x, y):
        c, s = math.cos(self.rot), math.sin(self.rot)
        return (self.cx + self.s * (x * c - y * s), self.cy + self.s * (x * s + y * c))


@dataclass
class Ctx:
    cv: Canvas
    breed: str | None
    marks_on: bool
    collar_on: bool
    covers: dict = field(default_factory=dict)
    # the neck wear being captured: "Collar", "Bandana" or "Bow" (all region A, tagged with their own capture)
    wear: str = "Collar"


# ---------------------------------------------------------------- generic part
def cel(cv, fill, light=(7.0, 9.0), factor=SHADOW, reach=2.4):
    """One hard-edged cel shadow shape on the side away from the top-left light: the
    part minus a copy of itself shifted toward the light, with a crisp anti-aliased
    edge (no airbrushed rim, no light rim)."""
    lit = cv.shift(fill, -light[0] * reach, -light[1] * reach)
    sh = np.clip(fill - lit, 0, 1)
    sh = smoothstep(0.30, 0.70, cv.blur(sh, 0.7))
    return 1.0 - (1.0 - factor) * sh


def outline(cv, fill, ow=None, lo=0.78, hi=1.22, wmul=None):
    """Pressure-varied outline round a filled shape: thinner on the lit (top-left) side
    and at thin tips, thicker on the shadow side and under-curves.  Built from a signed
    distance field of the supersampled fill, so the edges stay anti-aliased."""
    from scipy import ndimage
    ow = L.ow if ow is None else ow
    inside = fill >= 0.5
    if not inside.any():
        return np.zeros_like(fill)
    ss = cv.ss
    d_out = ndimage.distance_transform_edt(~inside) / ss
    d_in = ndimage.distance_transform_edt(inside) / ss
    sd = np.where(inside, -(d_in - 0.5 / ss), d_out - 0.5 / ss)
    ys, xs = np.nonzero(inside)
    cx, cy = cv.X[ys, xs].mean(), cv.Y[ys, xs].mean()
    ext = max(np.ptp(cv.X[ys, xs]), np.ptp(cv.Y[ys, xs]), 1.0)
    u = np.array([0.62, 0.78])
    w = np.clip(0.5 + ((cv.X - cx) * u[0] + (cv.Y - cy) * u[1]) / (0.75 * ext), 0, 1)
    # thin tips: where the shape is narrow (small inscribed radius nearby) the line thins
    # (the window must reach ~ow*1.3 inside the shape, or every edge reads as a tip)
    rad = ndimage.maximum_filter(d_in, size=int(ow * 2.8 * ss) | 1)
    tip = np.clip(rad / (ow * 1.3), 0.6, 1.0)
    hw = ow / 2 * (lo + (hi - lo) * w) * tip
    if wmul is not None:                      # taper the line by width (not by opacity)
        hw = hw * wmul
    return np.clip((hw - np.abs(sd)) * ss + 0.5, 0, 1) * (hw > 0.05)


def outline_over(cv, fill, under, ow=None, thin=None, soft=2.5, wmul=None):
    """Outline of ``fill`` at full weight on the silhouette, but at interior weight
    where it lies over ``under`` (another coat part behind it), so overlapping parts
    of the same body read as folds, not as separate stickers."""
    full = outline(cv, fill, ow, wmul=wmul)
    th = outline(cv, fill, ow=thin if thin is not None else L.dw * 1.1, lo=1.0, hi=1.0, wmul=wmul)
    w = cv.blur(np.clip(under, 0, 1), soft)
    return full * (1 - w) + th * w


_GRAIN = {}


def grain(cv):
    key = (cv.w, cv.h)
    if key not in _GRAIN:
        rng = np.random.default_rng(7)
        n = rng.standard_normal((cv.h, cv.w))
        n = cv.blur(n, 0.9) * 3.0 + cv.blur(rng.standard_normal((cv.h, cv.w)), 2.5) * 4.0
        _GRAIN[key] = 1.0 + GRAIN * np.clip(n, -2.5, 2.5)
    return _GRAIN[key]


def part(c: Ctx, path=None, fill=None, line=None, *, light=(7.0, 9.0), shade=(), marks=(),
         details=None, occl=None, ow=None, region=WR, reach=2.4):
    """Paint one coat part: fill, markings, details, outline.  Returns (fill, F).

    ``marks`` is a list of (mask, kind): kind "g" paints the breed marking overlay
    (tag "marking"), kind "dark" the Calico dark-patch overlay (tag "darkpatch");
    both carry G weight.  Dark patches always paint over light ones."""
    cv = c.cv
    if fill is None:
        fill = cv.fill_mask(path)
    raw = fill
    if line is None:
        line = outline(cv, fill, ow)
    if occl is not None:
        fill = fill * (1 - occl)
        line = line * (1 - occl)
    F = cel(cv, raw, light, reach=reach) if light else np.ones_like(raw)
    for m, f in shade:
        F = F * (1.0 - (1.0 - f) * np.clip(m, 0, 1))
    F = F * grain(cv)
    cv.paint(fill, coat(F), region)
    if c.marks_on:
        rim = None
        for m, kind in sorted(marks, key=lambda mk: mk[1] == "dark"):
            Fm, tag = F, "marking"
            if kind == "dark":
                tag = "darkpatch"
                # a slightly lighter rim where a dark patch meets the outline, so the
                # shape does not merge into the dark-brown line
                if rim is None:
                    rim = np.clip(cv.blur((line > 0.3).astype(float), 1.4) * 2.2, 0, 1) * (line < 0.5)
                Fm = F * (1.0 + 0.30 * rim)
            cv.paint(np.clip(m, 0, 1) * fill, mark_color(Fm), WG, tag=tag)
    if details:
        details(fill, F)
    cv.paint(line, OUTLINE)
    c.covers_last = np.maximum(fill, line)
    return fill, F


def tube(c: Ctx, pts, width, **kw):
    """A constant-width tube (tail) along a polyline, outlined like a filled shape."""
    cv = c.cv
    path = polyline(pts)
    fill = cv.stroke_mask(path, width)
    line = outline(cv, fill)
    return part(c, fill=fill, line=line, **kw)


def taper_tube(c: Ctx, pts, w0, w1, **kw):
    """A tail tapering from ``w0`` at its base to ``w1`` at a rounded tip."""
    cv = c.cv
    pts = np.asarray(pts, float)
    prof = lambda t: (w0 + (w1 - w0) * t ** 0.9) / w0  # noqa: E731
    fill = np.maximum.reduce([cv.fill_mask(tapered(pts, w0, profile=prof)),
                              cv.fill_mask(ellipse(*pts[-1], w1 / 2, w1 / 2)),
                              cv.fill_mask(ellipse(*pts[0], w0 / 2, w0 / 2))])
    return part(c, fill=fill, line=outline(cv, fill), **kw)


def arclength_coords(cv, pts, region):
    """Per-pixel (arc length s along the polyline, signed lateral distance) for pixels in region."""
    pts = np.asarray(pts, float)
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    dense = np.linspace(0, s[-1], int(s[-1] * 2) + 2)
    px = np.interp(dense, s, pts[:, 0])
    py = np.interp(dense, s, pts[:, 1])
    tree = cKDTree(np.stack([px, py], 1))
    S = np.zeros_like(region)
    Lat = np.zeros_like(region)
    idx = np.nonzero(region > 0)
    q = np.stack([cv.X[idx], cv.Y[idx]], 1)
    d, j = tree.query(q)
    S[idx] = dense[j]
    tx = np.gradient(px)[j]
    ty = np.gradient(py)[j]
    side = np.sign((q[:, 0] - px[j]) * ty - (q[:, 1] - py[j]) * tx)
    Lat[idx] = d * side
    return S, Lat, s[-1]


def stroke_marks(cv, strokes, taper=(0.2, 0.2)):
    """Union of tapered brush strokes [(points, width), ...]; 3 points = one quadratic-ish curve."""
    m = np.zeros_like(cv.X)
    for st in strokes:
        pts, w = st[0], st[1]
        tp = st[2] if len(st) > 2 and st[2] is not None else taper
        prof = st[3] if len(st) > 3 else None
        pts = np.asarray(pts, float)
        if len(pts) == 3:
            pts = chain_points([(pts[0], pts[1], pts[1], pts[2])], n=32)
        elif len(pts) == 4:
            pts = chain_points([(pts[0], pts[1], pts[2], pts[3])], n=40)
        m = np.maximum(m, cv.fill_mask(tapered(pts, w, taper=tp, profile=prof)))
    return m


def leg_sock(cv, x0, x1, y_top, y_bot, curve=8.0, soft=9.0, lo=0.5, full=0.97):
    """Siamese point sock on a roughly vertical limb between x0..x1: a two-step cel
    gradient (a ``lo`` weight band, then full weight) whose top edge curves round
    the limb instead of being one horizontal cut."""
    xc, hw = (x0 + x1) / 2, max((x1 - x0) / 2, 1.0)
    u = np.clip((cv.X - xc) / hw, -1.2, 1.2)
    edge = y_top + curve * (1 - u * u)               # a smile: the band wraps the cylinder
    step1 = smoothstep(edge - 0.8, edge + 0.8, cv.Y)
    step2 = smoothstep(edge + soft - 0.8, edge + soft + 0.8, cv.Y)
    m = lo * step1 + (full - lo) * step2
    return m * (cv.Y < y_bot + 40)


def leg_bars(cv, xs, ys, ws=None, wrap=0.42, frac=0.78, seed=0):
    """Tabby leg bars: strokes that wrap round the limb (a smile whose ends climb the
    sides), with uneven spacing, width and length (``ys`` top to bottom, ``ws`` widths)."""
    strokes = []
    x0, x1 = xs
    xc, hw = (x0 + x1) / 2, (x1 - x0) / 2
    ws = ws or [10.0] * len(ys)
    for k, (y, w) in enumerate(zip(ys, ws)):
        f = frac * (1.0 - 0.12 * ((k + seed) % 2))
        off = (0.08 if (k + seed) % 2 else -0.08) * hw
        a = (xc - hw * f + off, y - wrap * hw * 0.55)
        b = (xc + off * 0.3, y + wrap * hw * 0.35)
        e = (xc + hw * f + off, y - wrap * hw * 0.65)
        strokes.append(([a, b, e], w))
    return stroke_marks(cv, strokes, taper=(0.12, 0.12))


# ---------------------------------------------------------------- head
@dataclass
class HeadGeo:
    rx: float
    ry: float
    ry_bot: float
    cheek: float
    taper: float
    ear_h: float
    ear_w: float
    ear_pos: float      # x of ear base centre / rx
    ear_tilt: float     # radians, outward
    ex: float           # eye centre x
    ey: float           # eye centre y
    er: float           # eye radius
    eye_ry: float       # vertical stretch
    eye_tilt: float     # outer corner up (radians)
    ny: float           # nose y
    tuft: float         # cheek tuft size
    squircle: float = 2.0   # jaw/side outline exponent: 2 = ellipse, higher = boxier
    dome: float = 1.90      # cranium exponent (< 2: a rounder, domed top)


YOUNG_HEAD = HeadGeo(rx=168, ry=136, ry_bot=128, cheek=0.04, taper=0.0, ear_h=104, ear_w=122,
                     ear_pos=0.55, ear_tilt=0.38, ex=76, ey=30, er=47, eye_ry=1.06, eye_tilt=0.0,
                     ny=78, tuft=0)
# Grown: a smaller head on a bigger body, slightly almond eyes (still big and set low,
# so the face stays chibi), a short jaw and taller ears.
GROWN_HEAD = HeadGeo(rx=148, ry=118, ry_bot=112, cheek=0.04, taper=0.03, ear_h=120, ear_w=112,
                     ear_pos=0.56, ear_tilt=0.30, ex=62, ey=22, er=41, eye_ry=0.92, eye_tilt=0.10,
                     ny=76, tuft=0)


def rest_head(g: HeadGeo, young: bool = False) -> HeadGeo:
    """Rest poses: a lower cranium so the closed eyes sit mid-head.  Young: a rounder
    head with bigger closed-eye arcs set lower."""
    if young:
        return replace(g, rx=g.rx * 0.95, ry=g.ry * 0.94, ear_h=g.ear_h * 0.92, ey=g.ey + 10, er=g.er * 1.10,
                       ny=g.ny + 4)
    return replace(g, rx=g.rx * 0.96, ry=g.ry * 0.90, ear_h=g.ear_h * 0.94, dome=1.96)


def head_outline(g: HeadGeo, n=1000):
    th = np.linspace(-np.pi, np.pi, n, endpoint=False)
    c, s = np.cos(th), np.sin(th)
    e = np.where(s < 0, 2.0 / g.dome, 2.0 / g.squircle)
    x = g.rx * np.sign(c) * np.abs(c) ** e
    y = np.where(s < 0, g.ry, g.ry_bot) * np.sign(s) * np.abs(s) ** e
    below = np.clip(s, 0, None)
    phi = np.arctan2(np.clip(s, 0, None), np.abs(c))           # 0 = side, pi/2 = chin
    lower = smoothstep(0.0, 0.55, s)                            # continuous across the sides
    # soft cheek fluff bulging out below the eyes (no jaw corner)
    x = x * (1 + g.cheek * np.exp(-((phi - 0.38) / 0.30) ** 2) * lower)
    x = x * (1 - g.taper * below ** 2)
    # one small rounded cheek tuft per side, pushed down-and-out
    bump = np.exp(-((phi - 0.50) / 0.20) ** 2) * lower
    ang = 0.50
    x = x + np.sign(c) * np.cos(ang) * g.tuft * bump
    y = y + np.sin(ang) * g.tuft * bump
    return np.stack([x, y], 1)


def outline_x(g: HeadGeo, y: float) -> float:
    """|x| of the head outline at local height y on the lower half."""
    pts = head_outline(g)
    lo = pts[(pts[:, 1] > 0) & (pts[:, 0] > 0)]
    lo = lo[np.argsort(lo[:, 1])]
    return float(np.interp(y, lo[:, 1], lo[:, 0]))


def ear_points(g: HeadGeo, side: int):
    b0 = np.array([side * g.rx * g.ear_pos, -g.ry * 0.70])
    tip = b0 + rotate_pts([[0.0, -g.ear_h]], side * g.ear_tilt)[0]
    along = (tip - b0) / np.linalg.norm(tip - b0)
    perp = np.array([-along[1], along[0]])
    a = b0 - perp * g.ear_w / 2
    cpt = b0 + perp * g.ear_w / 2
    return b0, tip, a, cpt, along, perp


def ear_path(g: HeadGeo, side: int, scale=1.0, anchor_t=0.0):
    b0, tip, a, cpt, along, perp = ear_points(g, side)
    anchor = b0 + (tip - b0) * anchor_t
    P = lambda q: anchor + (np.asarray(q) - anchor) * scale  # noqa: E731
    near_a = tip + (a - tip) * 0.20
    near_c = tip + (cpt - tip) * 0.20
    bulge_a = (a + tip) / 2 - perp * 6
    bulge_c = (cpt + tip) / 2 + perp * 6
    sink = b0 + along * -40
    pts = [P(a), P(bulge_a), P(near_a), P(tip + along * -2), P(near_c), P(bulge_c), P(cpt), P(sink)]
    return catmull([tuple(q) for q in pts], tension=0.55)


def siamese_mask(cv, fr: Frame, g: HeadGeo):
    """Seal-point face mask: a crisp shape centred on the nose and muzzle that tapers up
    to a soft point between the eyes and wraps only round the lower eye rims, narrower
    than the outer eye edges (cream shows at the temples).  Painted at full coverage with
    a hard edge (the part's own cel shadow shades it); no soft halo."""
    ex, ey, er, ny = g.ex, g.ey, g.er, g.ny
    top = ey - er * 0.62
    pts = [(0, top), (ex * 0.30, ey - er * 0.30), (ex * 0.62, ey + er * 0.10), (ex + er * 0.42, ey + er * 0.42),
           (ex + er * 0.26, ey + er * 0.98), (ex * 0.66, ny + 22), (ex * 0.30, ny + 36), (0, ny + 40)]
    full = [*pts, *[(-x, y) for x, y in reversed(pts[1:-1])]]
    return cv.fill_mask(fr.wrap(catmull(full, tension=0.5)))


def head(c: Ctx, fr: Frame, g: HeadGeo, eyes="open", mouth="w", under=None, lashes=False):
    """Draw the head.  ``under``: coverage of the body behind the lower head; where the
    jaw outline lies over it, it is drawn at interior weight so the head flows into the
    body instead of sitting on it like a helmet."""
    cv = c.cv
    k = L.k
    breed = c.breed if c.marks_on else None
    hpath = fr.wrap(poly([tuple(q) for q in head_outline(g)]))
    hfill = cv.fill_mask(hpath)
    hline = outline(cv, hfill)
    lx, ly = fr.local(cv.X, cv.Y)
    if under is not None:
        thin = outline(cv, hfill, ow=L.dw * 1.15, lo=1.0, hi=1.0)
        wgt = cv.blur(np.clip(under, 0, 1), 2.5) * smoothstep(g.ry_bot * 0.15, g.ry_bot * 0.45, ly)
        hline = hline * (1 - wgt) + thin * wgt
    hcover = np.maximum(hfill, hline)
    c.covers["head"] = hcover

    # ---- ears (behind the head)
    for side in (-1, 1):
        epath = fr.wrap(ear_path(g, side))
        efill = cv.fill_mask(epath)
        marks = []
        if breed == "Siamese":
            marks.append((np.full_like(efill, 0.97), "g"))
        elif breed == "Calico":
            marks.append((np.ones_like(efill), "g" if side < 0 else "dark"))

        def ear_details(fill, F, side=side):
            inner = cv.fill_mask(fr.wrap(ear_path(g, side, scale=0.62, anchor_t=0.10)))
            inner = inner * (1 - hcover)
            cv.paint(inner, PINK_EAR)
            deep = cv.fill_mask(fr.wrap(ear_path(g, side, scale=0.34, anchor_t=0.0)))
            cv.paint(cv.blur(deep, 3) * inner * 0.6, PINK_EAR_DEEP, glaze=True)
            # ear floof: two soft strands from the inner base
            b0, tip, a, cpt, along, perp = ear_points(g, side)
            for kk, off in enumerate((-0.10, 0.10)):
                base = b0 + perp * (off * g.ear_w * side) + along * 4
                end = base + along * (g.ear_h * (0.36 - 0.06 * kk)) + perp * (side * (6 if kk else -4))
                mid = (base + end) / 2 + perp * 5 * side * (1 if kk else -1)
                pts = chain_points([(base, mid, mid, end)], n=24)
                pts = np.array([fr.pt(*q) for q in pts])
                cv.paint(cv.fill_mask(tapered(pts, 5.5 * fr.s, taper=(0.9, 0.1))) * fill * 0.6, FLOOF, glaze=True)

        part(c, fill=efill * (1 - hfill), line=outline(cv, efill) * (1 - hfill), light=(5, 7),
             marks=[(m * (1 - hfill), kk) for m, kk in marks], details=ear_details)

    # ---- head marks
    marks = []
    siam = None
    if breed == "Tabby":
        # forehead M: ONE connected brush stroke with two clear peaks above the brows
        # (outer feet above the inner brows, a valley in the middle), corners rounded
        ry = g.ry
        ex, ey, er = g.ex, g.ey, g.er
        brow = ey - er * g.eye_ry - 8
        knots = np.array([(-ex * 0.62, brow - 2), (-ex * 0.36, -ry * 0.66), (0.0, -ry * 0.40),
                          (ex * 0.36, -ry * 0.66), (ex * 0.62, brow - 2)])
        dense = np.concatenate([np.linspace(knots[i], knots[i + 1], 40, endpoint=False) for i in range(4)]
                               + [knots[-1:]])
        kern = np.ones(9) / 9
        sm = np.stack([np.convolve(np.pad(dense[:, j], 4, mode="edge"), kern, mode="valid") for j in (0, 1)], 1)
        sm = np.array([fr.pt(*q) for q in sm])
        wM = 11.5 * L.k / 1.7 * fr.s
        prof = lambda t: np.clip(np.minimum(t, 1 - t) * 7.0, 0.35, 1.0)  # noqa: E731
        pts_strokes = [(sm, wM, None, prof)]
        # one short stripe up the crown from the valley
        crown = [(0.0, -ry * 0.52), (-2.0, -ry * 0.72), (1.0, -ry * 0.92)]
        pts_strokes.append((np.array([fr.pt(*q) for q in crown]), wM * 0.8, (0.2, 0.2)))
        for sd in (-1, 1):
            # one cheek swoosh from the outer eye corner sweeping back and UP toward the ear
            sw = [(sd * (g.ex + g.er + 4), g.ey + 8), (sd * g.rx * 0.86, g.ey - 4), (sd * g.rx * 1.0, g.ey - 22)]
            pts_strokes.append((np.array([fr.pt(*q) for q in sw]), 11.5 * L.k / 1.7 * fr.s, (0.35, 0.12)))
        marks.append((stroke_marks(cv, pts_strokes), "g"))
    elif breed == "Tuxedo":
        # white muzzle with a rounded inverted-V blaze between the eyes, merging into
        # the muzzle with curved edges and a soft, slightly off-centre rounded top
        top = g.ey - g.er * 0.78
        eyew = max(g.ex - g.er * 0.62, 12)
        muzzle = cv.fill_mask(fr.wrap(catmull([
            (-64, g.ny + 6), (-46, g.ny - 12), (-eyew * 0.78, g.ey + g.er * 0.55), (-eyew * 0.62, g.ey),
            (-eyew * 0.40, top + 12), (2, top), (eyew * 0.46, top + 13), (eyew * 0.66, g.ey),
            (eyew * 0.80, g.ey + g.er * 0.55), (46, g.ny - 12), (64, g.ny + 6), (58, g.ny + 34),
            (30, g.ry_bot * 0.97), (-30, g.ry_bot * 0.97), (-58, g.ny + 34)], tension=0.5)))
        marks.append((muzzle, "g"))
    elif breed == "Siamese":
        siam = siamese_mask(cv, fr, g)
        marks.append((siam, "g"))
    elif breed == "Calico":
        marks.append((cv.fill_mask(fr.wrap(blob(-g.rx * 0.62, -g.ry * 0.62, g.rx * 0.52, seed=11, squash=0.85))), "g"))
        marks.append((cv.fill_mask(fr.wrap(blob(g.rx * 0.70, -g.ry * 0.78, g.rx * 0.34, seed=12, squash=0.8))), "dark"))

    def face(fill, F):
        # blush: the baby's large, saturated peach circle, well inside the cheek; kept
        # off the Siamese mask (clipped to the cream cheek) so it never turns muddy.
        # Opaque enough at the core that it stays peach on dark coats.
        for sd in (-1, 1):
            bl = cv.fill_mask(fr.wrap(ellipse(sd * (g.ex + g.er * 0.16), g.ey + g.er * g.eye_ry + 9,
                                              g.er * 0.64, g.er * 0.40)))
            a = cv.blur(bl, 3.0) * fill * BLUSH_A
            if siam is not None:
                a = a * (1 - cv.blur(np.clip(siam, 0, 1), 1.0))
            cv.paint(a, BLUSH, glaze=True)
        # eyes
        for sd in (-1, 1):
            ex, ey = sd * g.ex, g.ey
            if eyes == "open":
                eye(c, fr, ex, ey, g.er, g.eye_ry, -sd * g.eye_tilt, sd, lashes)
            else:
                closed_eye(c, fr, ex, ey, g.er, sd)
        # nose
        nose = fr.wrap(catmull([(-12, g.ny - 7), (0, g.ny - 8.5), (12, g.ny - 7), (2, g.ny + 6), (-2, g.ny + 6)],
                               tension=0.6))
        cv.paint(cv.fill_mask(nose), NOSE)
        cv.paint(cv.fill_mask(fr.wrap(ellipse(-4, g.ny - 4, 4, 2.4, -0.2))) * 0.85, WHITE)
        cv.paint(cv.stroke_mask(nose, 3.2 * k * fr.s), OUTLINE)

        def m(ctx):
            ctx.move_to(0, g.ny + 6)
            ctx.line_to(0, g.ny + 12)
            ctx.move_to(-20, g.ny + 10)
            ctx.curve_to(-17, g.ny + 23, -2, g.ny + 23, 0, g.ny + 12)
            ctx.curve_to(2, g.ny + 23, 17, g.ny + 23, 20, g.ny + 10)
        if mouth == "w":
            cv.paint(cv.stroke_mask(fr.wrap(m), L.dw * 0.85 * fr.s), OUTLINE)
        for sd in (-1, 1):
            for (dx, dy) in ((24, 8), (33, 13), (26, 19)):
                cv.paint(cv.fill_mask(fr.wrap(ellipse(sd * dx, g.ny + dy, 2.2, 2.2))) * 0.75, OUTLINE, glaze=True)

    part(c, fill=hfill, line=hline, light=(9, 11), reach=2.2, marks=marks, details=face)

    # whiskers: start just OUTSIDE the cheek outline (never across the face) and fan out
    for sd in (-1, 1):
        for (y0, dy, ln) in ((g.ny - 2, -9, 1.0), (g.ny + 10, 3, 0.92)):
            x0 = outline_x(g, y0) + L.ow * 0.5 + 3
            Lw = 52 * ln
            pts = chain_points([((sd * x0, y0), (sd * (x0 + Lw * 0.35), y0 + dy * 0.15),
                                 (sd * (x0 + Lw * 0.75), y0 + dy * 0.7), (sd * (x0 + Lw), y0 + dy))], n=24)
            pts = np.array([fr.pt(*q) for q in pts])
            cv.paint(cv.fill_mask(tapered(pts, 3.4 * k * fr.s, taper=(0.9, 0.2))), OUTLINE)
    return hcover


def eye(c: Ctx, fr: Frame, ex, ey, r, ry, tilt, side, lashes=False):
    cv = c.cv
    k = L.k
    lx, ly = fr.local(cv.X, cv.Y)
    rim = cv.fill_mask(fr.wrap(ellipse(ex, ey, r, r * ry, tilt)))
    cv.paint(rim, OUTLINE)
    ri = r - 3.6 * k
    iris = cv.fill_mask(fr.wrap(ellipse(ex, ey + 0.5, ri, ri * ry, tilt)))
    t = (ly - (ey - ri * ry)) / (2 * ri * ry)          # 0 top .. 1 bottom of the iris
    F = 0.58 + 0.62 * smoothstep(0.0, 0.95, t)
    glow = cv.blur(cv.fill_mask(fr.wrap(ellipse(ex - side * 2, ey + ri * 0.55, ri * 0.75, ri * 0.38))), 3)
    F = F * (1 + 0.12 * glow)
    F = np.minimum(F, 1.22)
    col = np.asarray(REF_IRIS, float)[None, None, :] * F[..., None]
    cv.paint(iris, col, WB)
    # pupil
    pup = cv.fill_mask(fr.wrap(ellipse(ex, ey + 1, ri * 0.52, ri * 0.66 * ry, tilt)))
    cv.paint(pup * iris, PUPIL)
    # upper lid shade inside the iris top
    lid = cv.fill_mask(fr.wrap(ellipse(ex, ey - ri * 0.25, ri * 1.05, ri * 0.85 * ry, tilt)))
    cv.paint(iris * (1 - lid) * 0.25, PUPIL)
    # highlights
    hx = ex + side * -ri * 0.10 + ri * 0.24
    cv.paint(cv.fill_mask(fr.wrap(ellipse(hx, ey - ri * 0.36 * ry, ri * 0.31, ri * 0.29))), HIGHLIGHT)
    cv.paint(cv.fill_mask(fr.wrap(ellipse(ex - ri * 0.34, ey + ri * 0.36 * ry, ri * 0.14, ri * 0.13))), HIGHLIGHT)

    # top lid line (thicker than the rim, like the baby's upper lash line)
    def lidline(ctx):
        ctx.save()
        ctx.translate(ex, ey)
        ctx.rotate(tilt)
        ctx.scale(r + 0.5, (r + 0.5) * ry)
        if side > 0:
            ctx.arc(0, 0, 1, math.radians(198), math.radians(338))
        else:
            ctx.arc(0, 0, 1, math.radians(202), math.radians(342))
        ctx.restore()
    cv.paint(cv.stroke_mask(fr.wrap(lidline), 4.0 * k * fr.s), OUTLINE)
    if lashes:
        fx0 = ex + side * r * 0.88
        flick = np.array([fr.pt(fx0, ey - r * 0.42 * ry), fr.pt(fx0 + side * 8, ey - r * 0.60 * ry),
                          fr.pt(fx0 + side * 13, ey - r * 0.66 * ry)])
        flick = chain_points([(flick[0], flick[1], flick[1], flick[2])], n=16)
        cv.paint(cv.fill_mask(tapered(flick, 4.5 * k * fr.s, taper=(1.0, 0.2))), OUTLINE)


def closed_eye(c: Ctx, fr: Frame, ex, ey, r, side):
    cv = c.cv
    k = L.k
    w = r * 0.74
    pts = chain_points([((ex - w, ey + 2), (ex - w * 0.45, ey + r * 0.40), (ex + w * 0.45, ey + r * 0.40),
                         (ex + w, ey + 2))], n=40)
    pts = np.array([fr.pt(*q) for q in pts])
    # a faint light lid line just under the lash line keeps the closed eye readable
    # on dark coats and the Siamese mask (nearly invisible on light coats)
    under = chain_points([((ex - w * 0.85, ey + 5 + 2 * k), (ex - w * 0.4, ey + r * 0.40 + 4 + 2 * k),
                           (ex + w * 0.4, ey + r * 0.40 + 4 + 2 * k), (ex + w * 0.85, ey + 5 + 2 * k))], n=40)
    under = np.array([fr.pt(*q) for q in under])
    cv.paint(cv.fill_mask(tapered(under, 3.2 * fr.s, taper=(0.2, 0.2))) * 0.40, WHITE, glaze=True)
    cv.paint(cv.fill_mask(tapered(pts, 5.4 * k * fr.s, taper=(0.45, 0.45))), OUTLINE)


# ---------------------------------------------------------------- shared limb helpers
def toes(cv, fill, x, y, spread=11, length=11):
    for dx in (-spread, spread):
        p = polyline([(x + dx, y - length), (x + dx * 1.05, y)])
        cv.paint(cv.stroke_mask(p, L.dw * 0.72) * fill, OUTLINE)


def tail_marks(c: Ctx, pts, width):
    """Breed marks along a tail polyline (arc-length based)."""
    cv = c.cv
    breed = c.breed
    if not c.marks_on or breed is None:
        return []
    region = cv.stroke_mask(polyline(pts), width + 2)
    S, Lat, total = arclength_coords(cv, pts, region)
    if breed == "Tabby":
        m = np.zeros_like(S)
        chev = S - 0.35 * np.abs(Lat)            # chevrons pointing to the tip
        for i, tk in enumerate((0.38, 0.56, 0.73)):
            hw = 7.5 - i * 0.5
            m = np.maximum(m, band(chev, tk * total - hw, tk * total + hw, soft=0.9))
        m = np.maximum(m, smoothstep(0.89 * total - 1, 0.89 * total + 1, chev))
        return [(m * region, "g")]
    if breed == "Siamese":
        # the whole visible tail is pointed: a lighter cel step near the base, then
        # full colour, with a chevron (not perpendicular) edge between them
        s0 = 0.26 * total
        w = 0.62 + 0.35 * smoothstep(s0 - 1.0, s0 + 1.0, S - 0.45 * np.abs(Lat))
        return [(w * region, "g")]
    if breed == "Calico":
        # two organic patches with slanted, wobbly ends (no straight perpendicular cuts)
        wob = 4.0 * np.sin(Lat * 0.22 + 1.3) + 3.0 * np.sin(Lat * 0.51)
        u = S + 0.55 * Lat + wob
        dark = band(u, 0.30 * total, 0.52 * total, soft=1.0)
        u2 = S - 0.45 * Lat - wob
        ging = smoothstep(0.76 * total - 1, 0.76 * total + 1, u2)
        return [(dark * region, "dark"), (ging * region, "g")]
    return []


def collar_band(c: Ctx, pts, width=17.0, bell_at=None, bell_r=12.0, occl=None):
    """Accent-coloured neck wear (region A) round ``pts`` with a front piece at ``bell_at``:
    the collar's bell (fixed colour), the bandana's hanging triangle or the bow.
    ``pts=None`` draws only the front piece (for one in front of the head)."""
    if not c.collar_on:
        return
    if c.wear == "Bandana":
        return bandana(c, pts, width, bell_at, bell_r, occl)
    if c.wear == "Bow":
        return neck_bow(c, pts, width, bell_at, bell_r, occl)
    cv = c.cv
    lw = L.dw * 1.05
    if pts is not None:
        path = polyline(pts)
        fill = cv.stroke_mask(path, width)
        line = np.clip(cv.stroke_mask(path, width + lw) - cv.stroke_mask(path, max(width - lw, 1.0)), 0, 1)
        if occl is not None:
            fill = fill * (1 - occl)
            line = line * (1 - occl)
        F = cel(cv, fill, (2, 3), 0.84)
        col = np.asarray(REF_ACCENT, float)[None, None, :] * warm(F)
        cv.paint(fill, col, WA, tag="collar")
        cv.paint(line, OUTLINE, tag="collar")
    if bell_at is not None:
        bx, by = bell_at
        b = cv.fill_mask(ellipse(bx, by, bell_r, bell_r))
        if occl is not None:
            b = b * (1 - occl)
        Fb = cel(cv, b, (2, 3), 0.86)
        cv.paint(b, np.asarray(BELL, float)[None, None, :] * Fb[..., None], W0, tag="collar")
        cv.paint(cv.fill_mask(ellipse(bx - bell_r * 0.35, by - bell_r * 0.35, bell_r * 0.25, bell_r * 0.2)) * b,
                 WHITE, W0, tag="collar")
        cv.paint(cv.stroke_mask(polyline([(bx - bell_r * 0.55, by + bell_r * 0.25), (bx + bell_r * 0.55, by + bell_r * 0.25)]), 3.0) * b,
                 OUTLINE, W0, tag="collar")
        cv.paint(cv.fill_mask(ellipse(bx, by + bell_r * 0.52, 2.6, 2.6)) * b, OUTLINE, W0, tag="collar")
        bl = np.clip(cv.stroke_mask(ellipse(bx, by, bell_r, bell_r), lw), 0, 1)
        if occl is not None:
            bl = bl * (1 - occl)
        cv.paint(bl, OUTLINE, W0, tag="collar")


def _wear_band(c: Ctx, pts, width, occl, tag):
    """The neck band under a bandana or a bow: a narrower accent band with its outline."""
    cv = c.cv
    lw = L.dw * 1.05
    path = polyline(pts)
    fill = cv.stroke_mask(path, width)
    line = np.clip(cv.stroke_mask(path, width + lw) - cv.stroke_mask(path, max(width - lw, 1.0)), 0, 1)
    if occl is not None:
        fill = fill * (1 - occl)
        line = line * (1 - occl)
    F = cel(cv, fill, (2, 3), 0.84)
    cv.paint(fill, np.asarray(REF_ACCENT, float)[None, None, :] * warm(F), WA, tag=tag)
    cv.paint(line, OUTLINE, tag=tag)


def _accent_part(c: Ctx, fill, tag, occl, factor=0.84, light=(2, 3), shade_k=1.0):
    """Fill an accent part with one cel step and outline it (both clipped by ``occl``)."""
    cv = c.cv
    lw = L.dw * 1.05
    line = outline(cv, fill, ow=lw * 1.2, lo=1.0, hi=1.0)
    if occl is not None:
        fill = fill * (1 - occl)
        line = line * (1 - occl)
    F = cel(cv, fill, light, factor)
    cv.paint(fill, np.asarray(REF_ACCENT, float)[None, None, :] * warm(F * shade_k), WA, tag=tag)
    cv.paint(line, OUTLINE, tag=tag)
    return fill


def bandana(c: Ctx, pts, width, front, size, occl):
    """A neckerchief: a slim band round the neck and a soft triangle hanging over the chest, with two
    fold lines; recolourable like the collar (region A, tag "bandana")."""
    cv = c.cv
    tag = "bandana"
    if pts is not None:
        _wear_band(c, pts, width * 0.62, occl, tag)
    if front is None:
        return
    bx, by = front
    s = size * 3.9
    tri = [(bx - s, by - s * 0.42), (bx - s * 0.55, by - s * 0.30), (bx, by - s * 0.26), (bx + s * 0.55, by - s * 0.30),
           (bx + s, by - s * 0.42), (bx + s * 0.62, by + s * 0.25), (bx + s * 0.12, by + s * 0.86),
           (bx - s * 0.12, by + s * 0.86), (bx - s * 0.62, by + s * 0.25)]
    fill = cv.fill_mask(catmull(tri, closed=True, tension=0.35))
    shown = _accent_part(c, fill, tag, occl)
    for a, b, k in (((bx - s * 0.52, by - s * 0.18), (bx - s * 0.10, by + s * 0.56), 0.7),
                    ((bx + s * 0.50, by - s * 0.16), (bx + s * 0.18, by + s * 0.42), 0.6)):
        fold = cv.stroke_mask(polyline([a, ((a[0] + b[0]) / 2 + s * 0.04, (a[1] + b[1]) / 2), b]), L.dw * k) * shown
        cv.paint(fold, np.asarray(REF_ACCENT, float) * 0.74, WA, tag=tag)


def neck_bow(c: Ctx, pts, width, front, size, occl):
    """A ribbon round the neck tied in a bow at the front: two loops, a knot and two short tails
    (region A, tag "bow")."""
    cv = c.cv
    tag = "bow"
    if pts is not None:
        _wear_band(c, pts, width * 0.55, occl, tag)
    if front is None:
        return
    bx, by = front
    s = size * 2.0
    tails = np.zeros_like(cv.X)
    for side in (-1, 1):
        tail = [(bx + side * s * 0.15, by), (bx + side * s * 0.95, by + s * 1.55), (bx + side * s * 0.55, by + s * 1.45),
                (bx + side * s * 0.30, by + s * 1.75), (bx + side * s * 0.05, by + s * 0.35)]
        tails = np.maximum(tails, cv.fill_mask(poly(tail)))
    _accent_part(c, tails, tag, occl, shade_k=0.9)
    loops = np.zeros_like(cv.X)
    for side in (-1, 1):
        loop = ellipse(bx + side * s * 0.95, by - s * 0.05, s * 0.95, s * 0.62, rot=side * -0.25)
        loops = np.maximum(loops, cv.fill_mask(loop))
    shown = _accent_part(c, loops, tag, occl)
    for side in (-1, 1):
        inner = cv.fill_mask(ellipse(bx + side * s * 0.55, by - s * 0.02, s * 0.36, s * 0.26, rot=side * -0.25)) * shown
        cv.paint(inner, np.asarray(REF_ACCENT, float) * 0.72, WA, tag=tag)
    knot = cv.fill_mask(ellipse(bx, by, s * 0.36, s * 0.42))
    _accent_part(c, knot, tag, occl, shade_k=0.95)
