"""Placeholder dog art (P3): four poses in the cat's chibi style, and the Corgi's short-legged variant.

Same look as ``cat.py`` (big glossy eyes with two highlights, the baby's large peach cheek blush, flat two-tone cel
shading with a faint grain and #4D2917 outlines sized for the runtime draw scale), reusing its painting helpers.  A
puppy is a big round head with a soft light muzzle, a glossy button nose and a happy open mouth on a pear-shaped
sitting body with two straight forelegs and a curled, wagging tail; at rest it lies down with its chin on its paws.

Region contract (as for the cat, see README "Contract"): R main coat, G the light muzzle, chest bib and paw tips (the
breed coat's ``secondary`` colour), B iris, A the accessory accent.  Like the hamster's, the dog base carries G.

The base has no ears: every breed's ears are its feature overlay ``Markings/{Breed}/{Pose}`` (main coat R, plus any
G markings), so a floppy, a prick or a button ear never has to hide another.  Breed overlays:

* MixedBreed, BorderCollie: button ears folding forward (BorderCollie adds a white blaze, collar and socks, G).
* Golden: long feathered ears and feathering on the chest, legs and tail (R).
* Beagle: long floppy ears (R); ``Markings/Beagle/{Pose}Dark``: the black saddle (gTarget "dark").
* Dalmatian: floppy ears (R); ``Markings/Dalmatian/{Pose}Dark``: the spots (gTarget "dark").
* Poodle: long curly ears, a topknot and curly cuffs (R).
* Shiba: prick ears and the urajiro (cream cheeks, brows and throat, G).
* Husky: prick ears and the face mask with brow spots (G).
* Samoyed, Pomeranian: prick ears and a fluffy ruff (R).
* Corgi: big prick ears and a white blaze (G) on its own base, ``Base/Corgi{Pose}``: the dog with short legs (the
  leg region scaled to 55%) and a longer, lower body.

``Accessories/{Collar,Bandana,Bow}/{Pose}`` are the neck wear (A), for the Corgi's poses too.  The Dachshund's long
body needs a new silhouette, so it has no art here (it is listed as arriving soon).
"""

from __future__ import annotations

import math

import numpy as np

import cat
from cat import (BLUSH, BLUSH_A, CONTACT, OUTLINE, PINK_EAR, PINK_EAR_DEEP, WHITE, WA, W0, WG, WR, Ctx, Frame, L, blob,
                 cel, chain_points, closed_eye, collar_band, eye, outline, outline_over, part, smoothstep, toes, warm)
from raster import Canvas, catmull, ellipse, poly, polyline, tapered

# Reference colours (the recolour contract): a warm tan coat, a cream muzzle and bib, brown eyes, and the baby outfit
# accent reference for the neck wear.
REF_COAT = (214, 160, 104)
REF_MARK = (250, 238, 218)
REF_IRIS = (128, 84, 56)
REF_ACCENT = cat.REF_ACCENT

NOSE = (78, 54, 50)
NOSE_HI = (196, 170, 164)
TONGUE = (238, 128, 132)
TONGUE_DEEP = (214, 98, 108)

POSES = ["YoungAwake", "YoungRest", "GrownAwake", "GrownRest"]
# The Corgi's own base poses (short legs): "Corgi" + pose.
VARIANT = "Corgi"
VARIANT_POSES = [VARIANT + p for p in POSES]
ALL_POSES = POSES + VARIANT_POSES
# Suggested runtime scale beside the baby: a puppy is a little bigger than a kitten, a grown dog bigger again.
RUNTIME_SCALE = {"Young": 0.58, "Grown": 0.76}
CANVAS = {"YoungAwake": (600, 680), "GrownAwake": (620, 760), "YoungRest": (760, 500), "GrownRest": (830, 520)}
# Breed overlays: Markings/{breed}/{pose} for every shipped breed; "Dark" ones too for these.
BREEDS = ["MixedBreed", "Golden", "Beagle", "Shiba", "Corgi", "Poodle", "Husky", "Samoyed", "Pomeranian", "Dalmatian",
          "BorderCollie"]
DARK = ["Beagle", "Dalmatian"]
WEAR = ["Collar", "Bandana", "Bow"]
EARS = {"MixedBreed": "button", "BorderCollie": "button", "Golden": "long", "Beagle": "long", "Dalmatian": "floppy",
        "Poodle": "curly", "Shiba": "prick", "Husky": "prick", "Samoyed": "prick", "Pomeranian": "small",
        "Corgi": "corgi"}


def set_tier(tier: str) -> None:
    L.ow = round(cat.BABY_OW / RUNTIME_SCALE[tier] * 1.04, 1)   # Young 14.3, Grown 10.9
    L.k = L.ow / cat.BASE_OW
    L.dw = cat.BASE_DW * L.k


def use_dog_refs() -> None:
    cat.REF_COAT = REF_COAT
    cat.REF_MARK = REF_MARK
    cat.REF_IRIS = REF_IRIS


def light_color(F):
    return np.asarray(REF_MARK, float)[None, None, :] * warm(F)


def coat_color(F):
    return np.asarray(REF_COAT, float)[None, None, :] * warm(F)


class DCtx(Ctx):
    """The cat's context; ``corgi`` draws the short-legged variant."""
    corgi = False


# ---------------------------------------------------------------- head
def head_path(g):
    """A wide round head, a little fuller at the cheeks."""
    th = np.linspace(-np.pi, np.pi, 720, endpoint=False)
    c, s = np.cos(th), np.sin(th)
    e = np.where(s < 0, 1.0 / 0.96, 1.0 / 1.06)
    x = g["rx"] * np.sign(c) * np.abs(c) ** e
    y = np.where(s < 0, g["ry"], g["ry"] * 0.96) * np.sign(s) * np.abs(s) ** e
    phi = np.arctan2(np.clip(s, 0, None), np.abs(c))
    x = x * (1 + 0.07 * np.exp(-((phi - 0.30) / 0.40) ** 2) * smoothstep(-0.1, 0.5, s))
    return poly([tuple(q) for q in np.stack([x, y], 1)])


def half_width(g, y):
    th = np.linspace(-np.pi / 2, np.pi / 2, 400)
    c, s = np.cos(th), np.sin(th)
    e = np.where(s < 0, 1.0 / 0.96, 1.0 / 1.06)
    x = g["rx"] * c ** e
    yy = np.where(s < 0, g["ry"], g["ry"] * 0.96) * np.sign(s) * np.abs(s) ** e
    phi = np.arctan2(np.clip(s, 0, None), np.abs(c))
    x = x * (1 + 0.07 * np.exp(-((phi - 0.30) / 0.40) ** 2) * smoothstep(-0.1, 0.5, s))
    return float(np.interp(y, yy, x))


def muzzle_path(g):
    my, mw, mh = g["my"], g["mw"], g["mh"]
    return catmull([(-mw, my - mh * 0.15), (-mw * 0.55, my - mh * 0.85), (0, my - mh), (mw * 0.55, my - mh * 0.85),
                    (mw, my - mh * 0.15), (mw * 0.82, my + mh * 0.62), (0, my + mh * 0.92), (-mw * 0.82, my + mh * 0.62)],
                   tension=0.55)


def face(c, fr, g, fill, closed=False):
    """Blush, eyes, the light muzzle (G), the nose and a happy mouth inside the head ``fill``."""
    cv = c.cv
    k = L.k
    ex, ey, er = g["ex"], g["ey"], g["er"]
    muzzle = cv.fill_mask(fr.wrap(muzzle_path(g)))
    F = cel(cv, muzzle, (5, 6), reach=2.0)
    cv.paint(muzzle * fill, light_color(F), WG)
    for sd in (-1, 1):
        bl = cv.fill_mask(fr.wrap(ellipse(sd * (ex + er * 0.55), ey + er * 1.15, er * 0.66, er * 0.42)))
        cv.paint(cv.blur(bl, 3.0) * fill * BLUSH_A, BLUSH, glaze=True)
    for sd in (-1, 1):
        if closed:
            closed_eye(c, fr, sd * ex, ey, er, sd)
        else:
            eye(c, fr, sd * ex, ey, er, 1.04, 0.0, sd)
    ny = g["my"] - g["mh"] * 0.52
    nw = g["nw"]
    nose = fr.wrap(catmull([(-nw, ny - nw * 0.45), (0, ny - nw * 0.62), (nw, ny - nw * 0.45), (nw * 0.55, ny + nw * 0.30),
                            (0, ny + nw * 0.48), (-nw * 0.55, ny + nw * 0.30)], tension=0.55))
    nm = cv.fill_mask(nose)
    cv.paint(nm, NOSE)
    cv.paint(cv.fill_mask(fr.wrap(ellipse(-nw * 0.30, ny - nw * 0.30, nw * 0.30, nw * 0.16, -0.2))) * nm, NOSE_HI)
    cv.paint(cv.stroke_mask(nose, 2.6 * k * fr.s), OUTLINE)
    mt = ny + nw * 0.48
    w = g["mw"] * 0.46

    if closed:
        def m(ctx):
            ctx.move_to(0, mt)
            ctx.line_to(0, mt + 9)
            ctx.move_to(-w, mt + 4)
            ctx.curve_to(-w * 0.85, mt + 18, -6, mt + 18, 0, mt + 9)
            ctx.curve_to(6, mt + 18, w * 0.85, mt + 18, w, mt + 4)
        cv.paint(cv.stroke_mask(fr.wrap(m), L.dw * 0.75 * fr.s), OUTLINE)
        return

    # an open, happy mouth with a little pink tongue
    mouth = catmull([(-w, mt + 6), (-w * 0.45, mt + 12), (0, mt + 9), (w * 0.45, mt + 12), (w, mt + 6),
                     (w * 0.55, mt + 30), (0, mt + 38), (-w * 0.55, mt + 30)], tension=0.5)
    mm = cv.fill_mask(fr.wrap(mouth))
    cv.paint(mm, (122, 58, 52))
    tongue = cv.fill_mask(fr.wrap(ellipse(0, mt + 33, w * 0.48, 13))) * mm
    cv.paint(tongue, TONGUE)
    cv.paint(cv.stroke_mask(fr.wrap(polyline([(0, mt + 26), (0, mt + 37)])), L.dw * 0.4) * tongue, TONGUE_DEEP)
    cv.paint(cv.stroke_mask(fr.wrap(mouth), L.dw * 0.8 * fr.s), OUTLINE)
    cv.paint(cv.stroke_mask(fr.wrap(polyline([(0, mt), (0, mt + 9)])), L.dw * 0.75 * fr.s), OUTLINE)


# ---------------------------------------------------------------- ears (always an overlay, tag "marking")
def _pink(cv, fill, path):
    inner = cv.fill_mask(path) * fill
    cv.paint(inner, PINK_EAR, tag="marking")
    cv.paint(cv.blur(inner, 3) * fill * 0.4, PINK_EAR_DEEP, glaze=False, tag="marking")


def _ear_part(c, path, light=(4, 5), line=None, region=WR, color=None):
    """An ear drawn as an overlay part: coat fill (R) with a cel step, outlined, all tag "marking"."""
    cv = c.cv
    fill = cv.fill_mask(path)
    F = cel(cv, fill, light, reach=2.0) * cat.grain(cv)
    col = coat_color(F) if color is None else color(F)
    cv.paint(fill, col, region, tag="marking")
    if line is None:
        line = outline(cv, fill)
    return fill, line


def ears_behind(c, fr, g):
    """Prick ears stand behind the head: drawn first, the head covers their base."""
    if not c.marks_on:
        return
    kind = EARS.get(c.breed)
    if kind not in ("prick", "small", "corgi"):
        return
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    size = {"prick": 1.0, "small": 0.72, "corgi": 1.32}[kind]
    for sd in (-1, 1):
        base_in, base_out = (sd * rx * 0.20, -ry * 0.62), (sd * rx * 0.92, -ry * 0.42)
        tip = (sd * rx * (0.70 + 0.06 * size), -ry * (0.82 + 0.62 * size))
        pts = [base_in, (sd * rx * 0.30, -ry * (0.98 + 0.25 * size)), tip, (sd * rx * (0.90 + 0.04 * size), -ry * (0.80 + 0.2 * size)),
               base_out]
        path = fr.wrap(catmull(pts, tension=0.42))
        fill, line = _ear_part(c, path)
        inner = [(sd * rx * 0.34, -ry * 0.70), (sd * rx * 0.40, -ry * (0.98 + 0.2 * size)), (sd * rx * (0.68 + 0.05 * size), -ry * (0.80 + 0.5 * size)),
                 (sd * rx * 0.80, -ry * (0.78 + 0.12 * size)), (sd * rx * 0.74, -ry * 0.58)]
        _pink(cv, fill, fr.wrap(catmull(inner, tension=0.45)))
        cv.paint(line, OUTLINE, tag="marking")


def ears_front(c, fr, g, under):
    """Button, floppy, long and curly ears hang over the head's top corners, in front of it."""
    if not c.marks_on:
        return
    kind = EARS.get(c.breed)
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    for sd in (-1, 1):
        if kind == "button":
            pts = [(sd * rx * 0.36, -ry * 0.86), (sd * rx * 0.60, -ry * 1.10), (sd * rx * 0.98, -ry * 0.84),
                   (sd * rx * 1.04, -ry * 0.50), (sd * rx * 0.88, -ry * 0.44), (sd * rx * 0.70, -ry * 0.66)]
        elif kind == "floppy":
            pts = [(sd * rx * 0.40, -ry * 0.88), (sd * rx * 0.74, -ry * 1.02), (sd * rx * 1.04, -ry * 0.72),
                   (sd * rx * 1.14, -ry * 0.10), (sd * rx * 1.02, ry * 0.18), (sd * rx * 0.86, -ry * 0.02),
                   (sd * rx * 0.70, -ry * 0.50)]
        elif kind in ("long", "curly"):
            pts = [(sd * rx * 0.38, -ry * 0.90), (sd * rx * 0.74, -ry * 1.04), (sd * rx * 1.04, -ry * 0.70),
                   (sd * rx * 1.18, -ry * 0.02), (sd * rx * 1.12, ry * 0.46), (sd * rx * 0.94, ry * 0.52),
                   (sd * rx * 0.84, ry * 0.10), (sd * rx * 0.70, -ry * 0.48)]
        else:
            return
        path = fr.wrap(catmull(pts, tension=0.5))
        if kind == "curly":
            fill = _scallop(cv, path, 15)
            F = cel(cv, fill, (4, 5), reach=2.0) * cat.grain(cv)
            cv.paint(fill, coat_color(F), WR, tag="marking")
            _curls(cv, fill, 11)
            line = outline_over(cv, fill, under, thin=L.dw * 0.9)
        else:
            fill, line = _ear_part(c, path)
            line = outline_over(cv, fill, under, thin=L.dw * 0.9)
            # a soft fold line along the ear
            fold = chain_points([(fr.pt(sd * rx * 0.62, -ry * 0.74), fr.pt(sd * rx * 0.82, -ry * 0.60),
                                  fr.pt(sd * rx * 0.92, -ry * 0.30), fr.pt(sd * rx * 0.94, -ry * 0.05))], n=24)
            if kind != "button":
                cv.paint(cv.fill_mask(tapered(fold, L.dw * 0.6, taper=(0.2, 0.1))) * fill * 0.7, OUTLINE, tag="marking")
        cv.paint(line, OUTLINE, tag="marking")


def _feather_edge(c, fr, sd, rx, ry, under):
    """A Golden's feathered ear tips: three soft tufts at the bottom of the ear."""
    tufts = []
    for k, (x, y) in enumerate(((1.12, 0.40), (1.02, 0.50), (0.92, 0.46))):
        base = fr.pt(sd * rx * x, ry * (y - 0.08))
        tufts.append((base, math.pi / 2 + sd * (0.35 - 0.2 * k), 30, 26, sd * 8))
    _tufts(c, tufts, under)


# ---------------------------------------------------------------- fur helpers (feature overlays)
def _scallop(cv, path, r):
    """A path's fill with a scalloped (curly) edge: the shape grown by a ring of circles along its edge."""
    base = cv.fill_mask(path)
    from scipy import ndimage
    inside = base >= 0.5
    edge = inside & ~ndimage.binary_erosion(inside, iterations=2)
    ys, xs = np.nonzero(edge)
    if len(xs) == 0:
        return base
    pts = np.stack([cv.X[ys, xs], cv.Y[ys, xs]], 1)
    # pick evenly spaced edge points
    order = np.argsort(np.arctan2(pts[:, 1] - pts[:, 1].mean(), pts[:, 0] - pts[:, 0].mean()))
    pts = pts[order]
    keep = [pts[0]]
    for q in pts[1:]:
        if np.hypot(*(q - keep[-1])) >= r * 1.25:
            keep.append(q)
    m = base
    for (x, y) in keep:
        m = np.maximum(m, cv.fill_mask(ellipse(x, y, r, r)))
    return m


def _curls(cv, fill, r):
    """Little curl marks inside a curly coat, at detail weight."""
    ys, xs = np.nonzero(fill > 0.95)
    if len(xs) == 0:
        return
    rng = np.random.default_rng(int(xs.sum() % 9973))
    X, Y = cv.X[ys, xs], cv.Y[ys, xs]
    picks = rng.choice(len(X), size=min(len(X), max(3, int(fill.sum() / cv.ss ** 2 / (r * r * 9)))), replace=False)
    for i in picks:
        x, y = X[i], Y[i]
        a0 = rng.uniform(0, 2 * np.pi)
        a = np.linspace(a0, a0 + 4.2, 18)
        pts = np.stack([x + np.cos(a) * r * 0.55, y + np.sin(a) * r * 0.55], 1)
        cv.paint(cv.fill_mask(tapered(pts, L.dw * 0.5, taper=(0.3, 0.2))) * fill * 0.65, OUTLINE, tag="marking")


def _flame(base, direction, length, bend):
    dx, dy = math.cos(direction), math.sin(direction)
    nx, ny = -dy, dx
    tip = (base[0] + dx * length + nx * bend, base[1] + dy * length + ny * bend)
    mid = (base[0] + dx * length * 0.5 + nx * bend * 0.2, base[1] + dy * length * 0.5 + ny * bend * 0.2)
    return chain_points([(base, mid, mid, tip)], n=24)


def _tufts(c, tufts, under, region=WR):
    """Soft fur tufts (feathering, a ruff): wide at the root, rounded points, outlined thin over the body."""
    cv = c.cv
    m = np.zeros_like(cv.X)
    for (base, direction, length, width, bend) in tufts:
        pts = _flame(base, direction, length, bend)
        m = np.maximum(m, cv.fill_mask(tapered(pts, width, taper=(0.95, 0.10),
                                                profile=lambda t: np.clip(1.10 - t ** 1.7, 0.2, 1.0))))
        m = np.maximum(m, cv.fill_mask(ellipse(*pts[-1], width * 0.11, width * 0.11)))
    F = cel(cv, m, (4, 5), 0.82, reach=2.2) * cat.grain(cv)
    cv.paint(m, coat_color(F) if region == WR else light_color(F), region, tag="marking")
    line = outline_over(cv, m, under, ow=L.ow * 0.85, thin=L.dw * 0.45, soft=1.5)
    cv.paint(line, OUTLINE, tag="marking")
    return m


def _fluff(c, path, under, r):
    """A fluffy cloud of fur (a Samoyed's or Pomeranian's ruff, a Golden's chest): a scalloped shape in the main
    coat (R) with a few fur flicks, outlined thin where it lies over the body."""
    cv = c.cv
    m = _scallop(cv, path, r)
    F = cel(cv, m, (5, 6), 0.84, reach=2.2) * cat.grain(cv)
    cv.paint(m, coat_color(F), WR, tag="marking")
    ys, xs = np.nonzero(m > 0.95)
    if len(xs):
        cy0, cx0 = cv.Y[ys, xs].mean(), cv.X[ys, xs].mean()
        hw = (cv.X[ys, xs].max() - cv.X[ys, xs].min()) / 2
        for t in np.linspace(-0.75, 0.75, 6):
            x, y = cx0 + hw * 0.62 * t, cy0 + 10 + 16 * (1 - abs(t))
            flick = _flame((x, y), math.pi / 2 + t * 0.6, r * 1.1, 3)
            cv.paint(cv.fill_mask(tapered(flick, L.dw * 0.6, taper=(0.2, 0.1))) * m * 0.7, OUTLINE, tag="marking")
    cv.paint(outline_over(cv, m, under, ow=L.ow * 0.9, thin=L.dw * 0.5, soft=1.5), OUTLINE, tag="marking")
    return m


def _ruff(c, cx, cy, rx, ry, under, n=11, start=0.10, end=0.90, bump=0.26):
    """A fluffy ruff (Samoyed, Pomeranian): a scalloped collar of fur round the neck and chest, main coat (R)."""
    cv = c.cv
    a = np.linspace(math.pi * start, math.pi * end, n)
    m = np.zeros_like(cv.X)
    for t in a:
        x, y = cx + rx * math.cos(t), cy + ry * math.sin(t) * 0.55
        m = np.maximum(m, cv.fill_mask(ellipse(x, y, rx * bump, rx * bump * 0.9)))
    m = np.maximum(m, cv.fill_mask(ellipse(cx, cy + ry * 0.18, rx * 0.86, ry * 0.42)))
    F = cel(cv, m, (5, 6), 0.84, reach=2.2) * cat.grain(cv)
    cv.paint(m, coat_color(F), WR, tag="marking")
    for t in a[1:-1:2]:
        x, y = cx + rx * 0.78 * math.cos(t), cy + ry * 0.55 * math.sin(t) * 0.8
        flick = _flame((x, y - 8), math.pi / 2 + (t - math.pi / 2) * 0.6, 18, 4)
        cv.paint(cv.fill_mask(tapered(flick, L.dw * 0.6, taper=(0.2, 0.1))) * m * 0.7, OUTLINE, tag="marking")
    line = outline_over(cv, m, under, ow=L.ow * 0.9, thin=L.dw * 0.5, soft=1.5)
    cv.paint(line, OUTLINE, tag="marking")
    return m


# ---------------------------------------------------------------- head marks (G or dark)
def head_marks(c, fr, g):
    br = c.breed if c.marks_on else None
    cv = c.cv
    rx, ry, ex, ey, er = g["rx"], g["ry"], g["ex"], g["ey"], g["er"]
    marks = []
    if br == "Husky":
        # the face mask: everything below a cap that peaks between the eyes, with brow spots
        cap = cv.fill_mask(fr.wrap(catmull([(-rx * 1.2, -ry * 1.3), (rx * 1.2, -ry * 1.3), (rx * 1.15, ey - er * 0.6),
                                            (ex + er * 1.1, ey - er * 1.35), (ex * 0.4, ey - er * 1.25), (0, ey + er * 0.2),
                                            (-ex * 0.4, ey - er * 1.25), (-ex - er * 1.1, ey - er * 1.35),
                                            (-rx * 1.15, ey - er * 0.6)], tension=0.4)))
        mask = 1 - cap
        for sd in (-1, 1):
            mask = np.maximum(mask, cv.fill_mask(fr.wrap(ellipse(sd * ex * 0.92, ey - er * 1.62, er * 0.40, er * 0.26, sd * 0.2))))
        marks.append((mask, "g"))
    elif br == "Shiba":
        # urajiro: cream cheeks and brow spots
        m = np.zeros_like(cv.X)
        for sd in (-1, 1):
            m = np.maximum(m, cv.fill_mask(fr.wrap(ellipse(sd * rx * 0.50, ry * 0.42, rx * 0.40, ry * 0.36, sd * 0.3))))
            m = np.maximum(m, cv.fill_mask(fr.wrap(ellipse(sd * ex * 0.95, ey - er * 1.55, er * 0.38, er * 0.24, sd * 0.2))))
        marks.append((m, "g"))
    elif br in ("Corgi", "BorderCollie"):
        # a white blaze from the muzzle up between the eyes
        top = -ry * (0.95 if br == "BorderCollie" else 0.55)
        blaze = chain_points([((0, g["my"] - g["mh"] * 0.6), (0, ey), (0, ey - er), (0, top))], n=40)
        blaze = np.array([fr.pt(*q) for q in blaze])
        w = er * (1.0 if br == "Corgi" else 0.8)
        marks.append((cv.fill_mask(tapered(blaze, w, taper=(1.0, 0.25),
                                           profile=lambda t: np.clip(1.0 - 0.75 * t ** 1.3, 0.25, 1.0))), "g"))
    elif br == "Dalmatian":
        # kept between the ears, which hang over the head's corners (the spots overlay composites after them)
        spots = [(-rx * 0.20, -ry * 0.62, 12), (rx * 0.22, -ry * 0.82, 9), (rx * 0.05, -ry * 0.40, 6)]
        marks.append((_spots(cv, fr, spots, 51), "dark"))
    elif br == "Beagle":
        # a tan face with a white muzzle and blaze; the saddle is on the body
        blaze = chain_points([((0, g["my"] - g["mh"] * 0.6), (0, ey), (0, ey - er * 0.8), (0, -ry * 0.70))], n=40)
        blaze = np.array([fr.pt(*q) for q in blaze])
        marks.append((cv.fill_mask(tapered(blaze, er * 0.7, taper=(1.0, 0.3))), "g"))
    return marks


def _spots(cv, fr, spots, seed):
    m = np.zeros_like(cv.X)
    for i, (x, y, r) in enumerate(spots):
        px, py = fr.pt(x, y) if fr is not None else (x, y)
        m = np.maximum(m, cv.fill_mask(blob(px, py, r * L.k * 0.9, seed=seed + i, amp=0.22)))
    return m


def body_spots(cv, pts, seed):
    return _spots(cv, None, pts, seed)


# ---------------------------------------------------------------- awake (sitting)
def sitting(c, grown: bool):
    cv = c.cv
    br = c.breed if c.marks_on else None
    W = cv.W
    leg = 0.55 if c.corgi else 1.0
    cx = W / 2 - 16
    if grown:
        g = dict(rx=152, ry=124, ex=60, ey=-4, er=39, my=58, mw=70, mh=52, nw=24)
        hy = 284
        top_w, mid_w, wide_w, bot_w = 80, 112, 134, 116
        drop = 262
        leg_dx, leg_w, paw = 50, 48, (34, 21)
    else:
        g = dict(rx=166, ry=136, ex=66, ey=0, er=44, my=58, mw=68, mh=50, nw=24)
        hy = 276
        top_w, mid_w, wide_w, bot_w = 74, 104, 124, 108
        drop = 214
        leg_dx, leg_w, paw = 46, 46, (32, 20)
    if c.corgi:
        top_w, mid_w, wide_w, bot_w = top_w * 1.12, mid_w * 1.18, wide_w * 1.18, bot_w * 1.16
    chin = hy + g["ry"] * 0.96
    floor = chin + drop * (0.55 + 0.45 * leg)
    fr = Frame(cx, hy, 1.0, 0.0)
    hfill_raw = cv.fill_mask(fr.wrap(head_path(g)))
    contact = cv.blur(cv.shift(hfill_raw, 2, 14), 5) * (1 - hfill_raw)
    shade = [(contact, CONTACT)]
    ymid = chin + (floor - chin) * 0.45

    # ---- tail: curled up on the right side, wagging (drawn first: the haunch covers its base)
    tpts = chain_points([((cx + wide_w * 0.7, floor - 30), (cx + wide_w + 70, floor - 20),
                          (cx + wide_w + 112, floor - 90), (cx + wide_w + 86, floor - 150))], n=40)
    tw = 40 if grown else 36
    tm = []
    if br == "BorderCollie":
        tip = cv.fill_mask(ellipse(*tpts[-1], tw * 0.75, tw * 0.75))
        tm.append((tip, "g"))
    tail_fill, _ = cat.taper_tube(c, tpts, tw, tw * 0.62, light=(4, 6), marks=tm)

    # ---- haunches, behind the chest
    hfills = []
    for sd in (-1, 1):
        hx = cx + sd * (wide_w - 18)
        hry = (96 if grown else 84) * (0.70 + 0.30 * leg)
        hp = ellipse(hx, floor - hry * 0.98, 66, hry, sd * 0.10)
        f, _ = part(c, hp, light=(6, 8), shade=shade, marks=_haunch_marks(c, cv, hx, floor, hry, sd))
        hfills.append(f)
        # a hind foot peeking out in front of the haunch
        fx = cx + sd * (wide_w + 4)
        fm = [(np.ones_like(cv.X), "g")] if br == "BorderCollie" else []
        part(c, ellipse(fx, floor - 15, 32, 16, sd * 0.06), light=(3, 4), marks=fm,
             details=lambda fill, F, x=fx: toes(cv, fill, x, floor - 2, 8, 9))
    haunches = np.maximum(*hfills)

    # ---- torso: a pear widening into the haunches, with the light bib (G)
    torso = catmull([(cx - top_w, chin - 46), (cx, chin - 60), (cx + top_w, chin - 46), (cx + mid_w, ymid),
                     (cx + wide_w, floor - 44), (cx + bot_w, floor - 2), (cx, floor + 2), (cx - bot_w, floor - 2),
                     (cx - wide_w, floor - 44), (cx - mid_w, ymid)])
    tfill_raw = cv.fill_mask(torso)
    tline = outline(cv, tfill_raw) * (1 - np.clip(cv.blur(haunches, 3.0) * 1.6, 0, 1))

    def bib(fill, F):
        b = cv.fill_mask(catmull([(cx - top_w * 0.62, chin - 30), (cx + top_w * 0.62, chin - 30), (cx + top_w * 0.5, chin + 40),
                                  (cx, ymid + (floor - ymid) * 0.55), (cx - top_w * 0.5, chin + 40)], tension=0.5))
        cv.paint(b * fill, light_color(F), WG)

    tmarks = _torso_marks(c, cv, cx, chin, floor, top_w, mid_w, wide_w, ymid)
    torso_fill, _ = part(c, fill=tfill_raw, line=tline, light=(7, 9), shade=shade, marks=tmarks, details=bib)
    body = np.maximum(np.maximum(torso_fill, haunches), tail_fill)

    # ---- forelegs, over the chest; paw tips are light (G)
    py = floor - paw[1] + 1
    legs = np.zeros_like(cv.X)
    for sd in (1, -1):
        x = cx + sd * leg_dx
        shaft = cv.stroke_mask(polyline([(x - sd * 4, chin + 20 + (1 - leg) * 40), (x, py - 6)]), leg_w)
        pw = cv.fill_mask(ellipse(x, py, paw[0], paw[1]))
        lf = np.maximum(shaft, pw)
        line = outline_over(cv, lf, torso_fill, thin=L.dw * 0.95)
        # the leg's top edge disappears into the chest fur
        line = line * smoothstep(chin + 30 + (1 - leg) * 40, chin + 80 + (1 - leg) * 40, cv.Y)
        lm = [(cv.fill_mask(ellipse(x, py + 2, paw[0] * 1.02, paw[1] * 1.15)) * 0.0, "g")]
        if br == "BorderCollie":
            lm = [(smoothstep(py - 46, py - 40, cv.Y), "g")]
        elif br == "Dalmatian":
            lm = [(body_spots(cv, [(x - 8, py - 70 * leg - 10, 9), (x + 10, py - 30, 7)], 70 + sd), "dark")]

        def toe(fill, F, x=x):
            tip = cv.fill_mask(ellipse(x, py + 4, paw[0] * 0.9, paw[1] * 0.75))
            cv.paint(tip * fill, light_color(F), WG)
            toes(cv, fill, x, py + paw[1] - 2, 9, 10)
        f, _ = part(c, fill=lf, line=line, light=(4, 6), marks=lm, details=toe)
        legs = np.maximum(legs, f)
        if br == "Poodle" and c.marks_on:
            cuff = _scallop(cv, ellipse(x, py - paw[1] * 1.6, leg_w * 0.62, 18), 11)
            F = cel(cv, cuff, (3, 4), reach=2.0) * cat.grain(cv)
            cv.paint(cuff, coat_color(F), WR, tag="marking")
            _curls(cv, cuff, 9)
            cv.paint(outline_over(cv, cuff, np.maximum(body, legs), thin=L.dw * 0.6), OUTLINE, tag="marking")
    body = np.maximum(body, legs)

    # ---- ruff and chest feathering (feature overlays), then the neck wear, the head and the ears
    under = body
    if br in ("Samoyed", "Pomeranian"):
        big = 1.12 if br == "Pomeranian" else 1.0
        under = np.maximum(under, _fluff(c, ellipse(cx, chin + 4, g["rx"] * 0.98 * big, 96 * big), body, 24))
    elif br == "Golden":
        under = np.maximum(under, _fluff(c, ellipse(cx, chin + 18, top_w * 1.05, 58), body, 16))

    elif br == "Poodle":
        chest = _scallop(cv, ellipse(cx, chin + 34, top_w * 0.95, 46), 14)
        F = cel(cv, chest, (4, 5), reach=2.0) * cat.grain(cv)
        cv.paint(chest, coat_color(F), WR, tag="marking")
        _curls(cv, chest, 10)
        cv.paint(outline_over(cv, chest, body, thin=L.dw * 0.6), OUTLINE, tag="marking")
    neck = chain_points([((cx - top_w * 1.05, chin - 24), (cx - top_w * 0.4, chin + 20), (cx + top_w * 0.4, chin + 20),
                          (cx + top_w * 1.05, chin - 24))], n=40)
    collar_band(c, neck, width=20 * L.k / 1.6, bell_at=(cx, chin + 24), bell_r=14, occl=_outside(cv, body))

    ears_behind(c, fr, g)
    hm = head_marks(c, fr, g)
    hline = outline_over(cv, hfill_raw, under)
    hfill, _ = part(c, fill=hfill_raw, line=hline, light=(9, 11), reach=2.2, marks=hm,
                    details=lambda fill, F: face(c, fr, g, fill))
    ears_front(c, fr, g, np.maximum(hfill, under))
    if br == "Poodle":
        _topknot(c, fr, g, hfill)


def _outside(cv, body):
    """Everything outside the body's drawn silhouette (fill and outline), so neck wear never leaves it."""
    return 1 - np.clip(body + outline(cv, body), 0, 1)


def _haunch_marks(c, cv, hx, floor, hry, sd):
    br = c.breed if c.marks_on else None
    if br == "Beagle":
        # the saddle reaches down over the top of the haunch
        return [(cv.fill_mask(ellipse(hx + sd * 26, floor - hry * 1.75, 64, hry * 0.62, sd * 0.35)), "dark")]
    if br == "Dalmatian":
        return [(body_spots(cv, [(hx - 20, floor - hry * 1.3, 10), (hx + 18, floor - hry * 0.7, 12), (hx - 6, floor - hry * 0.3, 8)],
                            80 + sd), "dark")]
    if br == "Golden":
        return []
    return []


def _torso_marks(c, cv, cx, chin, floor, top_w, mid_w, wide_w, ymid):
    br = c.breed if c.marks_on else None
    if br == "Beagle":
        # the saddle shows on the sides of the body behind the chest
        m = np.zeros_like(cv.X)
        for sd in (-1, 1):
            m = np.maximum(m, cv.fill_mask(ellipse(cx + sd * (mid_w + 6), ymid + 10, 44, 80, sd * 0.2)))
        return [(m, "dark")]
    if br == "Dalmatian":
        return [(body_spots(cv, [(cx - 52, chin + 40, 10), (cx + 60, chin + 70, 11), (cx - 70, ymid + 40, 9),
                                 (cx + 30, ymid + 60, 8), (cx + 80, floor - 70, 10)], 60), "dark")]
    if br == "BorderCollie":
        # a white collar round the neck down into the bib
        return [(cv.fill_mask(catmull([(cx - top_w * 1.1, chin - 50), (cx + top_w * 1.1, chin - 50), (cx + top_w * 1.0, chin + 34),
                                       (cx + top_w * 0.6, ymid + 30), (cx, floor - 30), (cx - top_w * 0.6, ymid + 30),
                                       (cx - top_w * 1.0, chin + 34)], tension=0.5)), "g")]
    if br in ("Husky", "Shiba", "Corgi"):
        # a light throat and chest
        return [(cv.fill_mask(catmull([(cx - top_w * 0.9, chin - 50), (cx + top_w * 0.9, chin - 50), (cx + top_w * 0.8, chin + 40),
                                       (cx + top_w * 0.45, ymid + 40), (cx, floor - 40), (cx - top_w * 0.45, ymid + 40),
                                       (cx - top_w * 0.8, chin + 40)], tension=0.5)), "g")]
    return []


def _topknot(c, fr, g, hfill):
    """A Poodle's curly topknot on the crown (R)."""
    cv = c.cv
    path = fr.wrap(ellipse(0, -g["ry"] * 0.92, g["rx"] * 0.52, g["ry"] * 0.36))
    m = _scallop(cv, path, 15)
    F = cel(cv, m, (4, 5), reach=2.0) * cat.grain(cv)
    cv.paint(m, coat_color(F), WR, tag="marking")
    _curls(cv, m, 11)
    cv.paint(outline_over(cv, m, hfill, thin=L.dw * 0.7), OUTLINE, tag="marking")


# ---------------------------------------------------------------- rest (lying down, chin on paws)
def resting(c, grown: bool):
    cv = c.cv
    br = c.breed if c.marks_on else None
    W = cv.W
    leg = 0.55 if c.corgi else 1.0
    cx = W / 2
    if grown:
        g = dict(rx=148, ry=116, ex=58, ey=4, er=37, my=58, mw=68, mh=48, nw=23)
        floor, bx, by, brx, bry = 470, cx + 70, 330, 250, 128
    else:
        g = dict(rx=160, ry=128, ex=64, ey=8, er=42, my=60, mw=66, mh=48, nw=23)
        floor, bx, by, brx, bry = 450, cx + 60, 318, 224, 120
    brx *= 1.0 + 0.10 * (1 - leg) / 0.45
    bry *= 1.0 - 0.10 * (1 - leg) / 0.45
    hx, hy = cx - 70, floor - 54 - g["ry"] * 0.96
    fr = Frame(hx, hy, 1.0, -0.06)
    hfill_raw = cv.fill_mask(fr.wrap(head_path(g)))
    contact = cv.blur(cv.shift(hfill_raw, 8, 8), 6) * (1 - hfill_raw)
    shade = [(contact, CONTACT)]

    # ---- tail curled along the floor at the back
    tpts = chain_points([((bx + brx * 0.70, floor - 40), (bx + brx + 40, floor - 30), (bx + brx + 70, floor - 70),
                          (bx + brx + 40, floor - 110))], n=40)
    tw = 38 if grown else 34
    tm = [(cv.fill_mask(ellipse(*tpts[-1], tw * 0.75, tw * 0.75)), "g")] if br == "BorderCollie" else []
    tail_fill, _ = cat.taper_tube(c, tpts, tw, tw * 0.6, light=(4, 6), marks=tm)

    # ---- the body: a long mound behind the head, haunch bump at the back
    body = catmull([(bx - brx, floor - 8), (bx - brx * 0.95, by - bry * 0.55), (bx - brx * 0.45, by - bry),
                    (bx + brx * 0.35, by - bry * 0.95), (bx + brx * 0.92, by - bry * 0.35), (bx + brx, floor - 30),
                    (bx + brx * 0.80, floor), (bx - brx * 0.6, floor + 2)], tension=0.5)
    bfill_raw = cv.fill_mask(body)
    bm = []
    if br == "Beagle":
        bm.append((cv.fill_mask(catmull([(bx - brx * 0.55, by - bry * 1.1), (bx + brx * 0.45, by - bry * 1.1),
                                         (bx + brx * 0.70, by - bry * 0.2), (bx + brx * 0.2, by + bry * 0.05),
                                         (bx - brx * 0.40, by - bry * 0.25)], tension=0.5)), "dark"))
    elif br == "Dalmatian":
        bm.append((body_spots(cv, [(bx - brx * 0.4, by - bry * 0.6, 11), (bx + brx * 0.1, by - bry * 0.75, 12),
                                   (bx + brx * 0.55, by - bry * 0.35, 10), (bx + brx * 0.2, by - bry * 0.1, 9),
                                   (bx + brx * 0.75, floor - 50, 9)], 90), "dark"))

    def belly(fill, F):
        b = cv.fill_mask(ellipse(bx - brx * 0.2, floor - 6, brx * 0.7, bry * 0.26))
        cv.paint(b * fill, light_color(F), WG)

    bfill, _ = part(c, fill=bfill_raw, light=(10, 12), shade=shade, marks=bm, details=belly)
    # the hind leg folded against the body
    hx_, hy_ = bx + brx * 0.48, floor - bry * 0.42 * (0.7 + 0.3 * leg)
    hl = cv.fill_mask(ellipse(hx_, hy_, 74, 58 * (0.75 + 0.25 * leg), -0.2))
    part(c, fill=hl, line=outline_over(cv, hl, bfill, thin=L.dw * 1.0), light=(6, 8))
    hp = cv.fill_mask(ellipse(hx_ - 40, floor - 14, 34, 15, 0.05))
    part(c, fill=hp, light=(3, 4), details=lambda fill, F: cv.paint(fill * cv.fill_mask(ellipse(hx_ - 50, floor - 10, 24, 11)),
                                                                   light_color(F), WG))
    body_cover = np.maximum(np.maximum(bfill, hl), tail_fill)

    # ---- forepaws stretched out in front, the chin resting on them
    paws = np.zeros_like(cv.X)
    for sd in (-1, 1):
        px = hx + sd * 50
        fore = cv.fill_mask(catmull([(px - 30, floor - 50), (px + 30, floor - 50), (px + 36, floor - 4), (px - 36, floor - 4)],
                                    tension=0.5))
        fore = np.maximum(fore, cv.fill_mask(ellipse(px, floor - 18, 40, 20)))
        lm = [(smoothstep(floor - 46, floor - 40, cv.Y), "g")] if br == "BorderCollie" else []

        def tip(fill, F, px=px):
            cv.paint(cv.fill_mask(ellipse(px, floor - 10, 34, 14)) * fill, light_color(F), WG)
            toes(cv, fill, px, floor - 4, 10, 10)
        f, _ = part(c, fill=fore, line=outline_over(cv, fore, body_cover, thin=L.dw), light=(3, 4), marks=lm, details=tip)
        paws = np.maximum(paws, f)
    under = np.maximum(body_cover, paws)
    if br in ("Samoyed", "Pomeranian"):
        big = 1.12 if br == "Pomeranian" else 1.0
        under = np.maximum(under, _fluff(c, ellipse(hx + 20, hy + g["ry"] * 0.72, g["rx"] * 1.0 * big, 78 * big), under, 22))
    elif br == "Golden":
        under = np.maximum(under, _fluff(c, ellipse(hx + 10, hy + g["ry"] * 0.86, g["rx"] * 0.72, 44), under, 15))
    elif br == "Poodle":
        for (x, y, r) in ((bx - brx * 0.1, by - bry * 0.7, 60), (bx + brx * 0.6, floor - 60, 46)):
            m = _scallop(cv, ellipse(x, y, r, r * 0.75), 13)
            F = cel(cv, m, (4, 5), reach=2.0) * cat.grain(cv)
            cv.paint(m, coat_color(F), WR, tag="marking")
            _curls(cv, m, 10)
            cv.paint(outline_over(cv, m, under, thin=L.dw * 0.6), OUTLINE, tag="marking")
    # the neck shows behind the head's right side, where it meets the body
    neck = chain_points([((hx + g["rx"] * 0.60, hy + g["ry"] * 0.86), (hx + g["rx"] * 1.02, hy + g["ry"] * 0.62),
                          (hx + g["rx"] * 1.12, hy + g["ry"] * 0.05), (hx + g["rx"] * 1.00, hy - g["ry"] * 0.40))], n=40)
    collar_band(c, neck, width=18 * L.k / 1.6, bell_at=(hx + g["rx"] * 1.04, hy + g["ry"] * 0.52), bell_r=12,
                occl=_outside(cv, body_cover))

    ears_behind(c, fr, g)
    hm = head_marks(c, fr, g)
    hline = outline_over(cv, hfill_raw, under)
    hfill, _ = part(c, fill=hfill_raw, line=hline, light=(9, 11), reach=2.2, marks=hm,
                    details=lambda fill, F: face(c, fr, g, fill, closed=True))
    ears_front(c, fr, g, np.maximum(hfill, under))
    if br == "Poodle":
        _topknot(c, fr, g, hfill)


POSE_FUNCS = {
    "YoungAwake": lambda c: sitting(c, grown=False),
    "GrownAwake": lambda c: sitting(c, grown=True),
    "YoungRest": lambda c: resting(c, grown=False),
    "GrownRest": lambda c: resting(c, grown=True),
}


def render(pose: str, breed: str | None = None, capture: str | None = None) -> Canvas:
    """One pose (or a Corgi pose, "Corgi" + pose) of the base, a breed overlay capture or a wear capture."""
    saved = (cat.REF_COAT, cat.REF_MARK, cat.REF_IRIS)
    use_dog_refs()
    try:
        corgi = pose.startswith(VARIANT)
        plain = pose[len(VARIANT):] if corgi else pose
        W, H = CANVAS[plain]
        set_tier("Grown" if plain.startswith("Grown") else "Young")
        cv = Canvas(W, H, capture=capture)
        wear = {"collar": "Collar", "bandana": "Bandana", "bow": "Bow"}.get(capture)
        c = DCtx(cv=cv, breed=breed, marks_on=(capture in ("marking", "darkpatch") and breed is not None),
                 collar_on=wear is not None, wear=wear or "Collar")
        c.corgi = corgi
        POSE_FUNCS[plain](c)
        return cv
    finally:
        cat.REF_COAT, cat.REF_MARK, cat.REF_IRIS = saved
