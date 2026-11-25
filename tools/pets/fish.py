"""Placeholder fish art (P4): procedural chibi fish in the cat's style, and the Neon tetra school.

Same look as ``cat.py`` (a big glossy eye with two highlights, the baby's peach cheek blush, flat two-tone cel shading
with a faint grain and #4D2917 outlines sized for the runtime draw scale), reusing its painting helpers.  A fish is a
round, chubby body seen from the side, swimming to the right: one big eye, a blush, a small happy mouth, a light belly
and a little pectoral fin.  At rest (lights off) it tilts its nose down a little, its fins relax and its eyes close.

Region contract (as for the cat, see README "Contract"): R the body (the breed coat's ``main``), G the fins and the
light belly (the coat's ``secondary``), B the iris.  Like the hamster's and the dog's, the fish base carries G.

The base has no tail, dorsal or anal fin: every breed's fins are its feature overlay ``Markings/{Breed}/{Pose}`` (G,
plus the Discus's tall round body in R), so a goldfish's twin tail never has to hide under a betta's veil.  Breed
overlays:

* Goldfish: a twin fan tail, a rounded dorsal and a little anal fin (G).
* Guppy: a big round fan tail and a tall dorsal, with spots (G).
* Betta: a long flowing veil tail and long flowing dorsal, anal and ventral fins (G).
* Angelfish: tall swept triangular dorsal and anal fins and long ventral streamers (G);
  ``Markings/Angelfish/{Pose}Dark``: its vertical bars (gTarget "dark").
* Koi: a soft tail and fins and two barbels at the mouth (G); ``Markings/Koi/{Pose}Dark``: its patches.
* Clownfish: rounded fins (G); ``Markings/Clownfish/{Pose}Dark``: its three bands (the coat's ``dark`` is white).
* Discus: a tall round disc body (R) with a fringe of fins (G) and wavy lines (G) over the base.
* NeonTetraSchool: on its own base, ``Base/NeonTetraSchool{Pose}``: a little school of four slim fish; its overlay
  is their tails and fins and the neon stripe (G); ``Markings/NeonTetraSchool/{Pose}Dark``: the red of their rear half.

There is no neck wear: the fish's ``TankCastle`` accessory is a habitat layer (``tank.py``).
"""

from __future__ import annotations

import math

import numpy as np

import cat
from cat import BLUSH, BLUSH_A, OUTLINE, WG, WR, W0, Ctx, Frame, L, cel, closed_eye, eye, outline, outline_over, warm
from raster import Canvas, catmull, ellipse, polyline

# Reference colours (the recolour contract): a goldfish orange body, a peach belly and fins, a deep glossy eye, and
# the baby outfit accent reference (unused by the fish art).
REF_COAT = (244, 150, 72)
REF_MARK = (252, 214, 168)
REF_IRIS = (70, 74, 104)
REF_ACCENT = cat.REF_ACCENT

MOUTH = (226, 120, 120)

POSES = ["YoungAwake", "YoungRest", "GrownAwake", "GrownRest"]
# The Neon tetra school's own base poses: "NeonTetraSchool" + pose.
VARIANT = "NeonTetraSchool"
VARIANT_POSES = [VARIANT + p for p in POSES]
ALL_POSES = POSES + VARIANT_POSES
# Suggested runtime scale beside the baby: a young fish is small; a grown one a little bigger, still smaller than a
# kitten.  The tank is drawn at the baby's pose-pixel scale (tank.py).
RUNTIME_SCALE = {"Young": 0.42, "Grown": 0.5}
CANVAS = {"YoungAwake": (620, 520), "GrownAwake": (700, 560), "YoungRest": (620, 520), "GrownRest": (700, 560)}
# Breed overlays: Markings/{breed}/{pose} for every breed; "Dark" ones too for these.
BREEDS = ["Goldfish", "Guppy", "Betta", "NeonTetraSchool", "Angelfish", "Koi", "Clownfish", "Discus"]
DARK = ["NeonTetraSchool", "Angelfish", "Koi", "Clownfish"]
WEAR: list[str] = []


def set_tier(tier: str) -> None:
    L.ow = round(cat.BABY_OW / RUNTIME_SCALE[tier] * 1.04, 1)   # Young 19.8, Grown 16.6
    L.k = L.ow / cat.BASE_OW
    L.dw = cat.BASE_DW * L.k


def use_fish_refs() -> None:
    cat.REF_COAT = REF_COAT
    cat.REF_MARK = REF_MARK
    cat.REF_IRIS = REF_IRIS


def body_color(F):
    return np.asarray(REF_COAT, float)[None, None, :] * warm(F)


def fin_color(F):
    return np.asarray(REF_MARK, float)[None, None, :] * warm(F)


class FCtx(Ctx):
    """The cat's context."""


def _paint_part(c, fill, color, weights, tag=None, light=(5, 6), line=None, factor=cat.SHADOW, reach=2.2, under=None):
    """A filled part with one cel shadow step, the faint grain and its outline (at interior weight over ``under``)."""
    cv = c.cv
    if line is None:
        line = outline(cv, fill) if under is None else outline_over(cv, fill, under)
    F = cel(cv, fill, light, factor, reach) * cat.grain(cv)
    cv.paint(fill, color(F), weights, tag=tag)
    cv.paint(line, OUTLINE, tag=tag)
    return F


def _rays(c, fill, root, tips, tag, width=None):
    """Fin rays: soft darker strokes from the root fanning out to the tips, inside the fin."""
    cv = c.cv
    width = L.dw * 0.55 if width is None else width
    m = np.zeros_like(cv.X)
    for tip in tips:
        m = np.maximum(m, cv.stroke_mask(polyline([root, tip]), width))
    cv.paint(m * fill * 0.55, np.asarray(REF_MARK, float) * 0.82, WG, tag=tag)


# ---------------------------------------------------------------- geometry
def geometry(grown: bool, resting: bool):
    """Body size, the eye and the fins' sizes for a pose, in the body's local frame (+x forward, +y down)."""
    if grown:
        g = dict(rx=176, ry=132, er=46, fin=1.0)
    else:
        g = dict(rx=142, ry=126, er=50, fin=0.86)
    g["ex"], g["ey"] = g["rx"] * 0.42, -g["ry"] * 0.14
    g["rot"] = 0.10 if resting else -0.04
    g["droop"] = 1.0 if resting else 0.0
    return g


def body_path(g, squash=1.0):
    """A chubby egg: a round head, a full belly, narrowing to a short rounded tail stem."""
    rx, ry = g["rx"], g["ry"] * squash
    pts = [(rx * 1.0, ry * 0.02), (rx * 0.88, -ry * 0.58), (rx * 0.42, -ry * 0.98), (-rx * 0.22, -ry * 0.92),
           (-rx * 0.66, -ry * 0.56), (-rx * 0.94, -ry * 0.28), (-rx * 1.02, 0), (-rx * 0.94, ry * 0.28),
           (-rx * 0.66, ry * 0.56), (-rx * 0.18, ry * 0.94), (rx * 0.44, ry * 0.98), (rx * 0.88, ry * 0.6)]
    return catmull(pts, tension=0.5)


def _local_path(fr, pts, tension=0.5):
    return fr.wrap(catmull([tuple(p) for p in pts], tension=tension))


# ---------------------------------------------------------------- breed fins (feature overlays, G)
def fins(c, fr, g, breed):
    """The breed's tail, dorsal and anal fins, behind the body (tag "marking")."""
    cv = c.cv
    if not (c.marks_on and breed):
        return
    rx, ry, k, d = g["rx"], g["ry"], g["fin"], g["droop"]
    tag = "marking"
    shapes = []   # (local points, root, tips)
    tail_x = -rx * 0.86
    if breed in ("Goldfish", "Koi", "Clownfish"):
        spread = {"Goldfish": 1.0, "Koi": 0.82, "Clownfish": 0.7}[breed]
        lobe = ry * 0.84 * spread * k * (1 - 0.15 * d)
        reach = rx * (0.80 if breed == "Goldfish" else 0.62) * k
        notch = 0.42 if breed == "Goldfish" else 0.2
        tail = [(tail_x + 8, -ry * 0.16), (tail_x - reach * 0.55, -lobe * 0.9), (tail_x - reach, -lobe * 1.05 + d * 12),
                (tail_x - reach * (1 - notch * 0.4), -lobe * 0.25), (tail_x - reach * (1 - notch), d * 10),
                (tail_x - reach * (1 - notch * 0.4), lobe * 0.25 + d * 14), (tail_x - reach, lobe * 1.05 + d * 22),
                (tail_x - reach * 0.55, lobe * 0.9 + d * 10), (tail_x + 8, ry * 0.16)]
        shapes.append((tail, (tail_x, 0), [(tail_x - reach, -lobe), (tail_x - reach * 0.9, -lobe * 0.35),
                                           (tail_x - reach * 0.9, lobe * 0.45), (tail_x - reach, lobe + d * 20)]))
        top = ry * (0.48 if breed != "Clownfish" else 0.36) * k * (1 - 0.35 * d)
        dorsal = [(-rx * 0.42, -ry * 0.78), (-rx * 0.30, -ry * 0.92 - top * 0.7), (-rx * 0.02, -ry * 0.94 - top),
                  (rx * 0.22, -ry * 0.98 - top * 0.35), (rx * 0.30, -ry * 0.82)]
        shapes.append((dorsal, (-rx * 0.05, -ry * 0.8), [(-rx * 0.30, -ry * 0.9 - top * 0.7), (-rx * 0.05, -ry * 0.92 - top),
                                                          (rx * 0.18, -ry * 0.95 - top * 0.35)]))
        anal = [(-rx * 0.62, ry * 0.42), (-rx * 0.66, ry * 0.62 + 30 * k + d * 8), (-rx * 0.40, ry * 0.70 + 24 * k),
                (-rx * 0.26, ry * 0.66)]
        shapes.append((anal, (-rx * 0.48, ry * 0.55), [(-rx * 0.62, ry * 0.62 + 26 * k)]))
    elif breed == "Guppy":
        fan = rx * 0.98 * k
        tail = [(tail_x + 8, -ry * 0.14), (tail_x - fan * 0.35, -ry * 0.62), (tail_x - fan * 0.85, -ry * 0.98 + d * 18),
                (tail_x - fan * 1.08, -ry * 0.35 + d * 12), (tail_x - fan * 1.1, ry * 0.2 + d * 22),
                (tail_x - fan * 0.88, ry * 0.86 + d * 26), (tail_x - fan * 0.35, ry * 0.62 + d * 8), (tail_x + 8, ry * 0.14)]
        shapes.append((tail, (tail_x, 0), [(tail_x - fan * 0.85, -ry * 0.9), (tail_x - fan * 1.06, -ry * 0.2),
                                           (tail_x - fan * 1.04, ry * 0.4), (tail_x - fan * 0.8, ry * 0.85)]))
        top = ry * 0.62 * k * (1 - 0.35 * d)
        dorsal = [(-rx * 0.55, -ry * 0.62), (-rx * 0.62, -ry * 0.82 - top * 0.8), (-rx * 0.30, -ry * 0.88 - top),
                  (-rx * 0.08, -ry * 0.92)]
        shapes.append((dorsal, (-rx * 0.3, -ry * 0.8), [(-rx * 0.6, -ry * 0.82 - top * 0.7), (-rx * 0.34, -ry * 0.86 - top)]))
    elif breed == "Betta":
        veil = rx * 1.1 * k
        tail = [(tail_x + 10, -ry * 0.2), (tail_x - veil * 0.4, -ry * 0.78), (tail_x - veil * 0.9, -ry * 0.82 + d * 20),
                (tail_x - veil * 1.06, -ry * 0.1 + d * 30), (tail_x - veil * 0.98, ry * 0.75 + d * 36),
                (tail_x - veil * 0.62, ry * 1.18 + d * 30), (tail_x - veil * 0.2, ry * 0.86 + d * 14), (tail_x + 10, ry * 0.2)]
        shapes.append((tail, (tail_x, 0), [(tail_x - veil * 0.9, -ry * 0.7), (tail_x - veil * 1.02, 0),
                                           (tail_x - veil * 0.9, ry * 0.7), (tail_x - veil * 0.6, ry * 1.1)]))
        top = ry * 0.72 * k * (1 - 0.3 * d)
        dorsal = [(-rx * 0.62, -ry * 0.55), (-rx * 0.92, -ry * 0.72 - top * 0.6), (-rx * 0.62, -ry * 0.86 - top),
                  (-rx * 0.18, -ry * 0.98 - top * 0.6), (rx * 0.05, -ry * 0.9)]
        shapes.append((dorsal, (-rx * 0.3, -ry * 0.8), [(-rx * 0.82, -ry * 0.72 - top * 0.6), (-rx * 0.5, -ry * 0.86 - top)]))
        anal = [(-rx * 0.70, ry * 0.42), (-rx * 0.98, ry * 0.72 + 70 * k + d * 20), (-rx * 0.6, ry * 0.92 + 80 * k + d * 14),
                (-rx * 0.18, ry * 0.92 + 20 * k), (rx * 0.05, ry * 0.7)]
        shapes.append((anal, (-rx * 0.35, ry * 0.7), [(-rx * 0.9, ry * 0.75 + 64 * k), (-rx * 0.55, ry * 0.9 + 72 * k)]))
        ventral = [(rx * 0.12, ry * 0.66), (rx * 0.0, ry * 1.3 + 30 * k), (rx * 0.12, ry * 1.32 + 30 * k), (rx * 0.26, ry * 0.7)]
        shapes.append((ventral, (rx * 0.12, ry * 0.8), [(rx * 0.06, ry * 1.28 + 30 * k)]))
    elif breed == "Angelfish":
        top = ry * 0.98 * k * (1 - 0.2 * d)
        dorsal = [(-rx * 0.48, -ry * 0.70), (-rx * 0.80, -ry * 0.70 - top * 0.95), (-rx * 0.66, -ry * 0.62 - top),
                  (-rx * 0.06, -ry * 0.82 - top * 0.25), (rx * 0.22, -ry * 0.86)]
        shapes.append((dorsal, (-rx * 0.2, -ry * 0.8), [(-rx * 0.74, -ry * 0.68 - top), (-rx * 0.4, -ry * 0.75 - top * 0.6)]))
        anal = [(-rx * 0.48, ry * 0.62), (-rx * 0.80, ry * 0.68 + top * 0.95), (-rx * 0.66, ry * 0.62 + top),
                (-rx * 0.06, ry * 0.8 + top * 0.25), (rx * 0.18, ry * 0.8)]
        shapes.append((anal, (-rx * 0.2, ry * 0.8), [(-rx * 0.74, ry * 0.66 + top), (-rx * 0.4, ry * 0.74 + top * 0.6)]))
        tail = [(tail_x + 8, -ry * 0.16), (tail_x - rx * 0.5 * k, -ry * 0.62), (tail_x - rx * 0.58 * k, 0 + d * 8),
                (tail_x - rx * 0.5 * k, ry * 0.62 + d * 12), (tail_x + 8, ry * 0.16)]
        shapes.append((tail, (tail_x, 0), [(tail_x - rx * 0.5 * k, -ry * 0.5), (tail_x - rx * 0.5 * k, ry * 0.5)]))
        for side in (0, 1):
            streamer = [(rx * 0.02 + side * 16, ry * 0.7), (-rx * 0.05 + side * 16, ry * 1.25 + top * 0.35),
                        (rx * 0.02 + side * 16, ry * 1.27 + top * 0.35), (rx * 0.12 + side * 16, ry * 0.74)]
            shapes.append((streamer, None, []))
    elif breed == "Discus":
        tail = [(tail_x + 8, -ry * 0.16), (tail_x - rx * 0.42 * k, -ry * 0.5), (tail_x - rx * 0.46 * k, 0 + d * 8),
                (tail_x - rx * 0.42 * k, ry * 0.5 + d * 10), (tail_x + 8, ry * 0.16)]
        shapes.append((tail, (tail_x, 0), [(tail_x - rx * 0.42 * k, -ry * 0.4), (tail_x - rx * 0.42 * k, ry * 0.4)]))
    for pts, root, tips in shapes:
        fill = cv.fill_mask(_local_path(fr, pts))
        _paint_part(c, fill, fin_color, WG, tag=tag, light=(4, 5))
        if root is not None and tips:
            _rays(c, fill, fr.pt(*root), [fr.pt(*t) for t in tips], tag)
        if breed == "Guppy" and root is not None and root[0] == tail_x:
            rng = np.random.default_rng(5)
            for _ in range(14):
                sx = tail_x - rx * rng.uniform(0.3, 0.95) * k
                sy = ry * rng.uniform(-0.6, 0.6)
                spot = cv.fill_mask(fr.wrap(ellipse(sx, sy, 9 * k, 7 * k))) * fill
                cv.paint(spot, np.asarray(REF_MARK, float) * 0.68, WG, tag=tag)


def disc(c, fr, g):
    """The Discus's tall round body (R) with its fringe of fins (G), over the base body (tag "marking")."""
    cv = c.cv
    rx, ry, d = g["rx"], g["ry"], g["droop"]
    fringe = cv.fill_mask(fr.wrap(ellipse(-rx * 0.06, 0, rx * 0.98, ry * 1.42 - d * 6)))
    _paint_part(c, fringe, fin_color, WG, tag="marking", light=(4, 5))
    body = cv.fill_mask(fr.wrap(ellipse(rx * 0.02, 0, rx * 0.92, ry * 1.22 - d * 6)))
    F = _paint_part(c, body, body_color, WR, tag="marking", light=(7, 9), under=fringe)
    # wavy lines across the disc (G)
    lines = np.zeros_like(cv.X)
    for k in range(5):
        y0 = -ry * 0.95 + k * ry * 0.48
        pts = [fr.pt(-rx * 0.85 + t * rx * 1.6, y0 + math.sin(t * 6.0 + k) * 10) for t in np.linspace(0, 1, 24)]
        lines = np.maximum(lines, cv.stroke_mask(polyline(pts), 9))
    cv.paint(lines * body * 0.85, fin_color(F), WG, tag="marking")
    return body


# ---------------------------------------------------------------- one fish
def fish(c, fr, g, breed, resting, dark=()):
    """Fins behind, the body (R) with its light belly (G), any breed markings, the pectoral fin and the face."""
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    fins(c, fr, g, breed)
    bfill = cv.fill_mask(fr.wrap(body_path(g)))
    marks = [(m, "dark") for m in dark]

    def belly(fill, F):
        b = cv.fill_mask(fr.wrap(ellipse(rx * 0.12, ry * 0.62, rx * 0.86, ry * 0.52)))
        cv.paint(cv.blur(b, 2.0) * fill, fin_color(F), WG)

    cat.part(c, fill=bfill, light=(8, 10), reach=2.2, marks=marks, details=belly)
    # a few soft scale arcs on the back
    for (sx, sy) in ((-rx * 0.30, -ry * 0.42), (-rx * 0.52, -ry * 0.14), (-rx * 0.10, -ry * 0.20)):
        arc = chain_points_local(fr, [(sx - 6, sy - 15), (sx + 8, sy), (sx - 6, sy + 15)])
        cv.paint(cv.stroke_mask(polyline(arc), L.dw * 0.5) * bfill * 0.30, OUTLINE)
    top = bfill
    if breed == "Discus" and c.marks_on:
        top = np.maximum(bfill, disc(c, fr, g))
    pectoral(c, fr, g, resting, top)
    face(c, fr, g, resting, top)
    return top


def chain_points_local(fr, pts, n=30):
    """A smooth curve through local points, in canvas coordinates."""
    from raster import chain_points
    p = np.asarray(pts, float)
    curve = chain_points([(p[0], p[1], p[1], p[2])], n=n)
    return [fr.pt(*q) for q in curve]


def pectoral(c, fr, g, resting, under):
    """A little paddle fin on the side, behind the gill (G); relaxed against the body at rest."""
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    droop = 10 if resting else 0
    px, py = -rx * 0.02, ry * 0.30
    s = 0.8 + 0.2 * g["fin"]
    pts = [(px + 6 * s, py - 8 * s), (px - 30 * s, py - 2 * s + droop * 0.5), (px - 46 * s, py + 14 * s + droop),
           (px - 32 * s, py + 30 * s + droop), (px - 4 * s, py + 14 * s)]
    fill = cv.fill_mask(_local_path(fr, pts, tension=0.45))
    _paint_part(c, fill, fin_color, WG, light=(3, 4), line=outline_over(cv, fill, under, thin=L.dw * 0.9))
    _rays(c, fill, fr.pt(px + 2, py + 2), [fr.pt(px - 44 * s, py + 12 * s + droop + t * s) for t in (-8, 4, 16)], None)


def face(c, fr, g, resting, fill):
    """One big glossy eye (closed at rest), the blush and a small happy mouth."""
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    ex, ey, er = g["ex"], g["ey"], g["er"]
    bl = cv.fill_mask(fr.wrap(ellipse(ex - er * 0.2, ey + er * 1.32, er * 0.7, er * 0.4)))
    cv.paint(cv.blur(bl, 3.0) * fill * BLUSH_A, BLUSH, glaze=True)
    if resting:
        closed_eye(c, fr, ex, ey, er, 1)
    else:
        eye(c, fr, ex, ey, er, 1.06, 0.0, 1)
    mx, my = rx * 0.86, ry * 0.18
    if resting:
        pts = chain_points_local(fr, [(mx - 16, my - 2), (mx - 6, my + 6), (mx + 2, my)], n=20)
        cv.paint(cv.stroke_mask(polyline(pts), L.dw * 0.8), OUTLINE)
    else:
        mouth = cv.fill_mask(fr.wrap(ellipse(mx - 4, my + 2, 11, 8)))
        cv.paint(mouth, MOUTH)
        cv.paint(cv.stroke_mask(fr.wrap(ellipse(mx - 4, my + 2, 11, 8)), L.dw * 0.8), OUTLINE)


# ---------------------------------------------------------------- breed markings on the body
def dark_marks(c, fr, g, breed):
    """Dark overlays on the body (tag "darkpatch"): the Angelfish's bars, the Koi's patches, the Clownfish's bands."""
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    out = []
    if breed == "Angelfish":
        for x in (rx * 0.18, -rx * 0.22, -rx * 0.6):
            out.append(cv.fill_mask(fr.wrap(catmull([(x - 14, -ry * 1.6), (x + 14, -ry * 1.6), (x + 20, ry * 1.6),
                                                       (x - 8, ry * 1.6)], tension=0.2))))
    elif breed == "Koi":
        for (x, y, r, seed) in ((rx * 0.32, -ry * 0.62, 62, 3), (-rx * 0.30, -ry * 0.45, 70, 5), (-rx * 0.7, ry * 0.05, 44, 9)):
            out.append(cv.fill_mask(fr.wrap(cat.blob(x, y, r, seed, amp=0.22, squash=0.8))))
    elif breed == "Clownfish":
        for (x, w) in ((rx * 0.18, 38), (-rx * 0.34, 46), (-rx * 0.82, 26)):
            out.append(cv.fill_mask(fr.wrap(catmull([(x - w * 0.5, -ry * 1.2), (x + w * 0.6, -ry * 1.2),
                                                       (x + w * 0.5 + 10, 0), (x + w * 0.6, ry * 1.2),
                                                       (x - w * 0.5, ry * 1.2), (x - w * 0.5 + 10, 0)], tension=0.5))))
    return out


def koi_barbels(c, fr, g):
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    for side in (0, 1):
        pts = chain_points_local(fr, [(rx * 0.82 - side * 6, ry * 0.26), (rx * 0.78 - side * 10, ry * 0.46),
                                      (rx * 0.64 - side * 12, ry * 0.58)])
        m = cv.stroke_mask(polyline(pts), L.dw * 1.0)
        cv.paint(m, fin_color(np.ones_like(m) * 0.92), WG, tag="marking")
        cv.paint(np.clip(cv.stroke_mask(polyline(pts), L.dw * 1.8) - m, 0, 1), OUTLINE, tag="marking")


# ---------------------------------------------------------------- poses
def single(c, grown, resting):
    cv = c.cv
    g = geometry(grown, resting)
    fr = Frame(cv.W * 0.58, cv.H * 0.50, 1.0, g["rot"])
    br = c.breed if c.marks_on else None
    dark = dark_marks(c, fr, g, br) if br in DARK and c.cv.capture == "darkpatch" else []
    fish(c, fr, g, br, resting, dark)
    if br == "Koi":
        koi_barbels(c, fr, g)


# The school: four slim little fish, back to front: (dx, dy, scale) from the canvas centre.
SCHOOL = [(-150, -96, 0.50), (120, -120, 0.46), (-70, 58, 0.56), (150, 40, 0.52)]
SCHOOL_REST = [(-140, -40, 0.50), (110, -60, 0.46), (-70, 70, 0.56), (140, 74, 0.52)]


def school(c, grown, resting):
    cv = c.cv
    capture = c.cv.capture
    for i, (dx, dy, s) in enumerate(SCHOOL_REST if resting else SCHOOL):
        s *= 1.12 if grown else 1.0
        g = dict(rx=210 * s, ry=118 * s, er=max(18.0, 58 * s), fin=1.0, droop=1.0 if resting else 0.0)
        g["ex"], g["ey"] = g["rx"] * 0.46, -g["ry"] * 0.12
        rot = (0.08 if resting else -0.05) + 0.03 * (i % 2)
        fr = Frame(cv.W * 0.52 + dx, cv.H * 0.5 + dy, 1.0, rot)
        tetra(c, fr, g, resting, capture)


def tetra(c, fr, g, resting, capture):
    """One neon tetra: a slim body (R), its tail and fins and the neon stripe (G, "marking"), the red rear ("darkpatch")."""
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    d = g["droop"]
    tail_x = -rx * 0.86
    if c.marks_on:
        tail = [(tail_x + 6, -ry * 0.14), (tail_x - rx * 0.42, -ry * 0.66), (tail_x - rx * 0.30, 0 + d * 4),
                (tail_x - rx * 0.42, ry * 0.66 + d * 6), (tail_x + 6, ry * 0.14)]
        dorsal = [(-rx * 0.2, -ry * 0.8), (-rx * 0.12, -ry * 1.28), (rx * 0.05, -ry * 0.86)]
        for pts in (tail, dorsal):
            fill = cv.fill_mask(_local_path(fr, pts, tension=0.35))
            _paint_part(c, fill, fin_color, WG, tag="marking", light=(3, 4))
    bfill = cv.fill_mask(fr.wrap(body_path(g, squash=1.0)))
    marks = []
    if c.marks_on and capture == "darkpatch":
        rear = cv.fill_mask(fr.wrap(catmull([(-rx * 1.1, ry * 0.05), (-rx * 0.05, ry * 0.1), (rx * 0.12, ry * 1.2),
                                             (-rx * 1.1, ry * 1.2)], tension=0.3)))
        marks.append((rear, "dark"))
    if c.marks_on and capture == "marking":
        stripe = cv.fill_mask(fr.wrap(catmull([(-rx * 0.86, -ry * 0.10), (-rx * 0.2, -ry * 0.24), (rx * 0.42, -ry * 0.22),
                                               (rx * 0.42, -ry * 0.04), (-rx * 0.2, -ry * 0.02), (-rx * 0.86, ry * 0.06)],
                                              tension=0.4)))
        marks.append((stripe, "g"))
    cat.part(c, fill=bfill, light=(5, 6), reach=2.0, marks=marks)
    face(c, fr, g, resting, bfill)


POSE_FUNCS = {
    "YoungAwake": lambda c: single(c, grown=False, resting=False),
    "GrownAwake": lambda c: single(c, grown=True, resting=False),
    "YoungRest": lambda c: single(c, grown=False, resting=True),
    "GrownRest": lambda c: single(c, grown=True, resting=True),
}
for _pose in POSES:
    POSE_FUNCS[VARIANT + _pose] = (lambda grown, resting: lambda c: school(c, grown, resting))(
        _pose.startswith("Grown"), _pose.endswith("Rest"))


def render(pose: str, breed: str | None = None, capture: str | None = None) -> Canvas:
    saved = (cat.REF_COAT, cat.REF_MARK, cat.REF_IRIS)
    use_fish_refs()
    try:
        plain = pose[len(VARIANT):] if pose.startswith(VARIANT) else pose
        W, H = CANVAS[plain]
        set_tier("Grown" if plain.startswith("Grown") else "Young")
        cv = Canvas(W, H, capture=capture)
        if pose.startswith(VARIANT) and breed is None and capture is not None:
            breed = VARIANT
        c = FCtx(cv=cv, breed=breed, marks_on=(capture in ("marking", "darkpatch") and breed is not None),
                 collar_on=False)
        POSE_FUNCS[pose](c)
        return cv
    finally:
        # the cat's painting helpers read these module globals: give the cat its own colours back
        cat.REF_COAT, cat.REF_MARK, cat.REF_IRIS = saved
