"""Placeholder rabbit art (P5): four poses in the cat's chibi style, and the Flemish giant's larger base.

Same look as ``cat.py`` (big glossy eyes with two highlights, the baby's large peach cheek blush, flat two-tone cel
shading with a faint grain and #4D2917 outlines sized for the runtime draw scale), reusing its painting helpers.  A
bunny is a big round head with puffy whisker pads round a little pink nose and a Y-shaped mouth, sitting up on a soft
pear-shaped body with two big hind feet flat on the floor, two small front paws and a cotton tail; at rest it lies
down in a loaf with its eyes closed and its ears laid back along its back.

Region contract (as for the cat, see README "Contract"): R main coat, G the light whisker pads, chin, belly, the soles
of the feet and the cotton tail (the breed coat's ``secondary`` colour), B iris, A the accessory accent.  Like the
dog's, the rabbit base carries G.

The base has no ears: every breed's ears are its feature overlay ``Markings/{Breed}/{Pose}`` (main coat R, plus any G
markings), so upright ears never have to hide a lop's.  Breed overlays:

* Dutch, Rex, Harlequin: upright ears.  Dutch adds its white blaze and the white front of its body (G).
* NetherlandDwarf: short little upright ears.
* HollandLop: lop ears hanging down beside the cheeks (in front of the head).
* Lionhead: short ears and a fluffy mane round the face (R).
* Angora: upright ears with tufted tips and long fluffy tufts on the cheeks, the crown and the sides (R).
* Harlequin: ``Markings/Harlequin/{Pose}Dark``: one half of the face and bands across the body (gTarget "dark").
* FlemishGiant: upright ears, a little longer, on its own base, ``Base/FlemishGiant{Pose}``: the rabbit drawn larger
  (a bigger body and head on a bigger canvas).

``Accessories/{Collar,Bandana,Bow}/{Pose}`` are the neck wear (A), for the Flemish giant's poses too.
"""

from __future__ import annotations

import math

import numpy as np

import cat
from cat import (BLUSH, BLUSH_A, CONTACT, OUTLINE, PINK_EAR, PINK_EAR_DEEP, WHITE, WG, WR, W0, Ctx, Frame, L, cel,
                 chain_points, closed_eye, collar_band, eye, outline, outline_over, part, smoothstep, toes, warm)
from raster import Canvas, catmull, ellipse, poly, polyline, tapered

# Reference colours (the recolour contract): a warm agouti coat, a cream muzzle and belly, deep brown eyes, and the
# baby outfit accent reference for the neck wear.
REF_COAT = (206, 166, 128)
REF_MARK = (250, 240, 226)
REF_IRIS = (112, 78, 66)
REF_ACCENT = cat.REF_ACCENT

NOSE = (238, 150, 156)
NOSE_DEEP = (214, 118, 130)

POSES = ["YoungAwake", "YoungRest", "GrownAwake", "GrownRest"]
# The Flemish giant's own base poses: "FlemishGiant" + pose.
VARIANT = "FlemishGiant"
VARIANT_POSES = [VARIANT + p for p in POSES]
ALL_POSES = POSES + VARIANT_POSES
# Suggested runtime scale beside the baby: a young rabbit is about a kitten's size, a grown one a little smaller than a
# grown dog (0.56 / 0.68); the Flemish giant's base is drawn larger, so it shows bigger at the same scale.
RUNTIME_SCALE = {"Young": 0.5, "Grown": 0.6}
# How much larger the Flemish giant is drawn: its body, and a little its head.
GIANT_BODY = 1.24
GIANT_HEAD = 1.10
# Breed overlays: Markings/{breed}/{pose} for every breed; "Dark" ones too for these.
BREEDS = ["Dutch", "NetherlandDwarf", "HollandLop", "Lionhead", "Angora", "Rex", "FlemishGiant", "Harlequin"]
DARK = ["Harlequin"]
WEAR = ["Collar", "Bandana", "Bow"]
EARS = {"Dutch": "up", "Rex": "up", "Harlequin": "up", "FlemishGiant": "long", "NetherlandDwarf": "short",
        "Lionhead": "short", "HollandLop": "lop", "Angora": "tufted"}
# Ear length per kind, as a share of a plain upright ear.
EAR_LENGTH = {"up": 1.0, "long": 1.1, "short": 0.62, "tufted": 1.0}


def set_tier(tier: str) -> None:
    L.ow = round(cat.BABY_OW / RUNTIME_SCALE[tier] * 1.04, 1)   # Young 16.6, Grown 13.9
    L.k = L.ow / cat.BASE_OW
    L.dw = cat.BASE_DW * L.k


def use_rabbit_refs() -> None:
    cat.REF_COAT = REF_COAT
    cat.REF_MARK = REF_MARK
    cat.REF_IRIS = REF_IRIS


def light_color(F):
    return np.asarray(REF_MARK, float)[None, None, :] * warm(F)


def coat_color(F):
    return np.asarray(REF_COAT, float)[None, None, :] * warm(F)


class RCtx(Ctx):
    """The cat's context; ``giant`` draws the Flemish giant's larger base."""
    giant = False


# ---------------------------------------------------------------- geometry
def awake_geometry(grown: bool, giant: bool):
    """The sitting rabbit, laid out from the floor up; returns the canvas size too."""
    b = GIANT_BODY if giant else 1.0
    h = GIANT_HEAD if giant else 1.0
    if grown:
        g = dict(rx=146 * h, ry=120 * h, ex=58 * h, ey=2 * h, er=38 * h, ny=40 * h)
        body = dict(h=262 * b, top=100 * b, mid=156 * b, wide=164 * b, foot=(70 * b, 26 * b))
        ear = 206
    else:
        g = dict(rx=160 * h, ry=132 * h, ex=64 * h, ey=6 * h, er=44 * h, ny=42 * h)
        body = dict(h=206 * b, top=94 * b, mid=144 * b, wide=150 * b, foot=(62 * b, 24 * b))
        ear = 184
    ear *= h * (1.1 if giant else 1.0)
    W = int(round(2 * max(g["rx"], body["wide"] + body["foot"][0] * 0.7) + 140))
    head_top = body["h"] + g["ry"] * 0.86 + g["ry"]
    H = int(round(head_top + ear * 1.02 + 70))
    floor = H - 30
    chin = floor - body["h"]
    hy = chin - g["ry"] * 0.86
    return W, H, dict(cx=W / 2, floor=floor, chin=chin, hy=hy, g=g, body=body, ear=ear)


def rest_geometry(grown: bool, giant: bool):
    """The resting loaf: the body lying along the floor, the head at its front (left)."""
    b = GIANT_BODY if giant else 1.0
    h = GIANT_HEAD if giant else 1.0
    if grown:
        g = dict(rx=136 * h, ry=112 * h, ex=54 * h, ey=6 * h, er=34 * h, ny=38 * h)
        body = dict(rx=250 * b, ry=132 * b)
        ear = 196
    else:
        g = dict(rx=148 * h, ry=122 * h, ex=60 * h, ey=8 * h, er=40 * h, ny=40 * h)
        body = dict(rx=224 * b, ry=122 * b)
        ear = 176
    ear *= h * (1.1 if giant else 1.0)
    W = int(round(2 * body["rx"] + g["rx"] * 0.9 + 170))
    H = int(round(max(2 * body["ry"], 2 * g["ry"] + 30) + 170))
    floor = H - 30
    bx = W / 2 + g["rx"] * 0.40
    by = floor - body["ry"] * 0.98
    hx = bx - body["rx"] * 0.70
    hy = floor - g["ry"] * 0.98 - 18
    return W, H, dict(floor=floor, bx=bx, by=by, hx=hx, hy=hy, g=g, body=body, ear=ear)


def canvas_size(pose: str):
    giant = pose.startswith(VARIANT)
    plain = pose[len(VARIANT):] if giant else pose
    grown = plain.startswith("Grown")
    W, H, _ = (awake_geometry if plain.endswith("Awake") else rest_geometry)(grown, giant)
    return W, H


# ---------------------------------------------------------------- head and face
def head_path(g):
    """A round head, a little wider at the cheeks."""
    th = np.linspace(-np.pi, np.pi, 720, endpoint=False)
    c, s = np.cos(th), np.sin(th)
    e = np.where(s < 0, 1.0 / 0.94, 1.0 / 1.08)
    x = g["rx"] * np.sign(c) * np.abs(c) ** e
    y = np.where(s < 0, g["ry"], g["ry"] * 0.94) * np.sign(s) * np.abs(s) ** e
    phi = np.arctan2(np.clip(s, 0, None), np.abs(c))
    x = x * (1 + 0.08 * np.exp(-((phi - 0.32) / 0.42) ** 2) * smoothstep(-0.1, 0.5, s))
    return poly([tuple(q) for q in np.stack([x, y], 1)])


def half_width(g, y):
    th = np.linspace(-np.pi / 2, np.pi / 2, 400)
    c, s = np.cos(th), np.sin(th)
    e = np.where(s < 0, 1.0 / 0.94, 1.0 / 1.08)
    x = g["rx"] * c ** e
    yy = np.where(s < 0, g["ry"], g["ry"] * 0.94) * np.sign(s) * np.abs(s) ** e
    phi = np.arctan2(np.clip(s, 0, None), np.abs(c))
    x = x * (1 + 0.08 * np.exp(-((phi - 0.32) / 0.42) ** 2) * smoothstep(-0.1, 0.5, s))
    return float(np.interp(y, yy, x))


def face(c, fr, g, fill, closed=False):
    """Blush, eyes, the light whisker pads and chin (G), the little pink nose and a Y mouth, and whiskers."""
    cv = c.cv
    k = L.k
    ex, ey, er, ny = g["ex"], g["ey"], g["er"], g["ny"]
    pad_r = g["rx"] * 0.19
    pads = np.zeros_like(cv.X)
    for sd in (-1, 1):
        pads = np.maximum(pads, cv.fill_mask(fr.wrap(ellipse(sd * pad_r * 0.86, ny + pad_r * 0.62, pad_r, pad_r * 0.80))))
    pads = np.maximum(pads, cv.fill_mask(fr.wrap(ellipse(0, ny + pad_r * 1.30, pad_r * 0.62, pad_r * 0.50))))
    F = cel(cv, pads, (4, 5), reach=2.0)
    cv.paint(pads * fill, light_color(F), WG)
    for sd in (-1, 1):
        bl = cv.fill_mask(fr.wrap(ellipse(sd * (ex + er * 0.50), ey + er * 1.22, er * 0.70, er * 0.42)))
        cv.paint(cv.blur(bl, 3.0) * fill * BLUSH_A, BLUSH, glaze=True)
    for sd in (-1, 1):
        if closed:
            closed_eye(c, fr, sd * ex, ey, er, sd)
        else:
            eye(c, fr, sd * ex, ey, er, 1.06, 0.0, sd)
    # the nose: a small soft triangle, point down
    nw = g["rx"] * 0.085
    nose = fr.wrap(catmull([(-nw, ny - nw * 0.40), (0, ny - nw * 0.55), (nw, ny - nw * 0.40), (nw * 0.30, ny + nw * 0.45),
                            (-nw * 0.30, ny + nw * 0.45)], tension=0.5))
    nm = cv.fill_mask(nose)
    cv.paint(nm, NOSE)
    cv.paint(cv.fill_mask(fr.wrap(ellipse(-nw * 0.25, ny - nw * 0.18, nw * 0.30, nw * 0.16, -0.2))) * nm, WHITE)
    cv.paint(cv.stroke_mask(nose, 2.4 * k * fr.s), OUTLINE)
    # the Y mouth: a short line down from the nose that parts into two soft curves
    my = ny + nw * 0.45
    w = pad_r * 0.62

    def m(ctx):
        ctx.move_to(0, my)
        ctx.line_to(0, my + pad_r * 0.36)
        ctx.move_to(-w, my + pad_r * 0.42)
        ctx.curve_to(-w * 0.55, my + pad_r * 0.70, -w * 0.12, my + pad_r * 0.62, 0, my + pad_r * 0.36)
        ctx.curve_to(w * 0.12, my + pad_r * 0.62, w * 0.55, my + pad_r * 0.70, w, my + pad_r * 0.42)
    cv.paint(cv.stroke_mask(fr.wrap(m), L.dw * 0.72 * fr.s), OUTLINE)
    if not closed:
        # two little front teeth
        tw, ty = pad_r * 0.20, my + pad_r * 0.52
        teeth = fr.wrap(catmull([(-tw, ty), (tw, ty), (tw, ty + tw * 1.3), (-tw, ty + tw * 1.3)], tension=0.2))
        cv.paint(cv.fill_mask(teeth), WHITE)
        cv.paint(cv.stroke_mask(teeth, 1.8 * k), OUTLINE)
        cv.paint(cv.stroke_mask(fr.wrap(polyline([(0, ty + 1), (0, ty + tw * 1.25)])), 1.4 * k), OUTLINE)


def whiskers(c, fr, g):
    cv = c.cv
    pad_r = g["rx"] * 0.19
    for sd in (-1, 1):
        for (dy, rise, ln) in ((pad_r * 0.40, -12, 1.0), (pad_r * 0.80, 0, 0.92), (pad_r * 1.10, 10, 0.8)):
            x0 = pad_r * 1.55
            y0 = g["ny"] + dy
            Lw = g["rx"] * 0.42 * ln
            pts = chain_points([((sd * x0, y0), (sd * (x0 + Lw * 0.35), y0 + rise * 0.15),
                                 (sd * (x0 + Lw * 0.75), y0 + rise * 0.7), (sd * (x0 + Lw), y0 + rise))], n=20)
            pts = np.array([fr.pt(*q) for q in pts])
            cv.paint(cv.fill_mask(tapered(pts, 2.6 * L.k * fr.s, taper=(0.9, 0.2))), OUTLINE)


def head_marks(c, fr, g):
    """Breed marks on the head: the Dutch blaze (G) and the Harlequin's split face (dark)."""
    br = c.breed if c.marks_on else None
    cv = c.cv
    rx, ry, ey, er, ny = g["rx"], g["ry"], g["ey"], g["er"], g["ny"]
    if br == "Dutch":
        # a white blaze from the muzzle up between the eyes, widening into the cheeks and chin
        blaze = chain_points([((0, ny + 40), (0, ey), (0, ey - er), (0, -ry * 0.78))], n=40)
        blaze = np.array([fr.pt(*q) for q in blaze])
        m = cv.fill_mask(tapered(blaze, er * 1.1, taper=(1.0, 0.25), profile=lambda t: np.clip(1.0 - 0.7 * t ** 1.2, 0.3, 1.0)))
        m = np.maximum(m, cv.fill_mask(fr.wrap(ellipse(0, ny + ry * 0.36, rx * 0.62, ry * 0.42))))
        return [(m, "g")]
    if br == "Harlequin":
        # one half of the face (the rabbit's right, the viewer's left) in the dark colour
        m = cv.fill_mask(fr.wrap(poly([(-rx * 1.4, -ry * 1.4), (0, -ry * 1.4), (0, ry * 1.4), (-rx * 1.4, ry * 1.4)])))
        return [(m, "dark")]
    return []


def draw_head(c, fr, g, under, closed=False):
    """The head (outlined thin where it lies over the body), its marks, face and whiskers."""
    cv = c.cv
    hfill_raw = cv.fill_mask(fr.wrap(head_path(g)))
    hline = outline_over(cv, hfill_raw, under)
    hfill, _ = part(c, fill=hfill_raw, line=hline, light=(9, 11), reach=2.2, marks=head_marks(c, fr, g),
                    details=lambda fill, F: face(c, fr, g, fill, closed=closed))
    whiskers(c, fr, g)
    return hfill


# ---------------------------------------------------------------- ears (always an overlay, tag "marking")
def _ear_fill(c, path, line=None):
    """An ear drawn as an overlay part: coat fill (R) with a cel step, all tag "marking"."""
    cv = c.cv
    fill = cv.fill_mask(path)
    F = cel(cv, fill, (4, 5), reach=2.0) * cat.grain(cv)
    cv.paint(fill, coat_color(F), WR, tag="marking")
    return fill


def _pink(cv, fill, path):
    inner = cv.fill_mask(path) * fill
    cv.paint(inner, PINK_EAR, tag="marking")
    cv.paint(cv.blur(inner, 3) * fill * 0.4, PINK_EAR_DEEP, tag="marking")


def _long_ear(base, angle, length, width, bend=0.0):
    """Points round one long upright ear: from a base point along ``angle`` (radians, -pi/2 is straight up),
    widest a little above the base and rounded at the tip; ``bend`` curls the tip sideways."""
    dx, dy = math.cos(angle), math.sin(angle)
    nx, ny = -dy, dx
    bx, by = base
    pts = []
    for t, w in ((0.0, 0.50), (0.18, 0.62), (0.45, 0.60), (0.72, 0.50), (0.90, 0.34), (1.0, 0.0)):
        cx, cy = bx + dx * length * t + nx * bend * t * t, by + dy * length * t + ny * bend * t * t
        pts.append((cx, cy, w))
    left = [(x + nx * width * w, y + ny * width * w) for x, y, w in pts]
    right = [(x - nx * width * w, y - ny * width * w) for x, y, w in pts[:-1]]
    return left + right[::-1]


def upright_ears(c, fr, g, length, tilt=0.16, rest=False):
    """Two upright ears standing behind the head (the head covers their base); at rest they lie back along the body."""
    if not c.marks_on:
        return
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    kind = EARS.get(c.breed)
    width = rx * (0.34 if kind == "short" else 0.30)
    for sd in ((1, -1) if rest else (-1, 1)):
        if rest:
            # both ears lie back over the body to the right, the far one a little higher
            base = (rx * (0.10 + 0.24 * (sd > 0)), -ry * (0.84 if sd > 0 else 0.76))
            angle = -0.42 - (0.14 if sd > 0 else -0.02)
            bend = 22
        else:
            base = (sd * rx * 0.30, -ry * 0.76)
            angle = -math.pi / 2 + sd * tilt
            bend = sd * 10
        pts = _long_ear(base, angle, length, width, bend)
        path = fr.wrap(catmull(pts, tension=0.5))
        fill = _ear_fill(c, path)
        if not (rest and sd > 0):
            inner = _long_ear((base[0] + math.cos(angle) * length * 0.10, base[1] + math.sin(angle) * length * 0.10),
                              angle, length * 0.78, width * 0.52, bend * 0.8)
            _pink(cv, fill, fr.wrap(catmull(inner, tension=0.5)))
        if kind == "tufted":
            _ear_tufts(c, fr, base, angle, length, width, bend, fill)
        cv.paint(outline(cv, fill), OUTLINE, tag="marking")


def _ear_tufts(c, fr, base, angle, length, width, bend, under):
    """An Angora's tufted ear tips: a few soft tufts at the top of the ear."""
    dx, dy = math.cos(angle), math.sin(angle)
    nx, ny = -dy, dx
    tx = base[0] + dx * length * 0.92 + nx * bend * 0.85
    ty = base[1] + dy * length * 0.92 + ny * bend * 0.85
    tufts = []
    for k, off in enumerate((-0.5, 0.5)):
        bpt = fr.pt(tx + nx * width * off * 0.4, ty + ny * width * off * 0.4)
        tufts.append((bpt, angle + off * 0.45 + fr.rot, length * 0.20, width * 0.62, 5 * off))
    _tufts(c, tufts, under)


def lop_ears(c, fr, g, under):
    """A Holland lop's ears hang down beside the cheeks from the crown, in front of the head."""
    if not c.marks_on or EARS.get(c.breed) != "lop":
        return
    cv = c.cv
    rx, ry = g["rx"], g["ry"]
    for sd in (-1, 1):
        pts = [(sd * rx * 0.22, -ry * 0.86), (sd * rx * 0.60, -ry * 1.02), (sd * rx * 0.98, -ry * 0.70),
               (sd * rx * 1.16, -ry * 0.02), (sd * rx * 1.14, ry * 0.56), (sd * rx * 0.98, ry * 0.74),
               (sd * rx * 0.84, ry * 0.36), (sd * rx * 0.74, -ry * 0.30), (sd * rx * 0.52, -ry * 0.62)]
        path = fr.wrap(catmull(pts, tension=0.5))
        fill = _ear_fill(c, path)
        inner = [(sd * rx * 0.94, -ry * 0.20), (sd * rx * 1.06, ry * 0.20), (sd * rx * 1.02, ry * 0.56),
                 (sd * rx * 0.92, ry * 0.52), (sd * rx * 0.86, ry * 0.10)]
        _pink(cv, fill, fr.wrap(catmull(inner, tension=0.5)))
        # a soft fold where the ear turns over the crown
        fold = chain_points([(fr.pt(sd * rx * 0.40, -ry * 0.80), fr.pt(sd * rx * 0.66, -ry * 0.80),
                              fr.pt(sd * rx * 0.84, -ry * 0.62), fr.pt(sd * rx * 0.92, -ry * 0.36))], n=24)
        cv.paint(cv.fill_mask(tapered(fold, L.dw * 0.6, taper=(0.2, 0.1))) * fill * 0.7, OUTLINE, tag="marking")
        cv.paint(outline_over(cv, fill, under, thin=L.dw * 0.9), OUTLINE, tag="marking")


# ---------------------------------------------------------------- fur (feature overlays: Lionhead and Angora)
def _flame(base, direction, length, bend):
    dx, dy = math.cos(direction), math.sin(direction)
    nx, ny = -dy, dx
    tip = (base[0] + dx * length + nx * bend, base[1] + dy * length + ny * bend)
    mid = (base[0] + dx * length * 0.5 + nx * bend * 0.2, base[1] + dy * length * 0.5 + ny * bend * 0.2)
    return chain_points([(base, mid, mid, tip)], n=24)


def _tufts(c, tufts, under):
    """Soft fur tufts in the main coat (R): wide at the root, rounded points, outlined thin over the body; a short
    flick in each; tag "marking"."""
    cv = c.cv
    m = np.zeros_like(cv.X)
    flicks = np.zeros_like(cv.X)
    for (base, direction, length, width, bend) in tufts:
        pts = _flame(base, direction, length, bend)
        m = np.maximum(m, cv.fill_mask(tapered(pts, width, taper=(0.95, 0.10),
                                                profile=lambda t: np.clip(1.10 - t ** 1.7, 0.2, 1.0))))
        m = np.maximum(m, cv.fill_mask(ellipse(*pts[-1], width * 0.11, width * 0.11)))
        inner = _flame(base, direction, length * 0.5, bend * 0.6)[10:]
        flicks = np.maximum(flicks, cv.fill_mask(tapered(inner, L.dw * 0.55, taper=(0.2, 0.1))))
    F = cel(cv, m, (4, 5), 0.84, reach=2.2) * cat.grain(cv)
    cv.paint(m, coat_color(F), WR, tag="marking")
    cv.paint(flicks * m * 0.5, OUTLINE, tag="marking")
    cv.paint(outline_over(cv, m, under, ow=L.ow * 0.85, thin=L.dw * 0.45, soft=1.5), OUTLINE, tag="marking")
    return m


def mane(c, fr, g, under):
    """A Lionhead's mane: a ring of soft tufts round the face and under the chin, behind the head (R)."""
    rx, ry = g["rx"], g["ry"]
    tufts = []
    for k in range(11):
        a = math.pi * (-0.05 + 1.10 * k / 10)          # from the right cheek round under the chin to the left
        x, y = rx * 0.86 * math.cos(a), ry * 0.80 * math.sin(a) + ry * 0.10
        base = fr.pt(x, y)
        tufts.append((base, a + fr.rot, rx * 0.34, rx * 0.36, 8 * math.cos(a)))
    for sd in (-1, 1):
        for a in (-0.35, -0.75):
            x, y = sd * rx * 0.92 * math.cos(a), ry * 0.92 * math.sin(a)
            base = fr.pt(x, y)
            tufts.append((base, (0 if sd > 0 else math.pi) - sd * a * 0.6 + fr.rot - sd * 0.15, rx * 0.30, rx * 0.32, sd * 6))
    return _tufts(c, tufts, under)


def crown_tuft(c, fr, g, under):
    """A little tuft of mane on the Lionhead's crown, or the Angora's forehead fluff, in front of the head."""
    rx, ry = g["rx"], g["ry"]
    tufts = [(fr.pt(dx, -ry * 0.80), -math.pi / 2 + d + fr.rot, ln, rx * 0.22, 6)
             for dx, d, ln in ((-rx * 0.14, -0.55, rx * 0.22), (0, 0.0, rx * 0.28), (rx * 0.14, 0.55, rx * 0.22))]
    _tufts(c, tufts, under)


def angora_cheeks(c, fr, g, under):
    rx = g["rx"]
    tufts = []
    for sd in (-1, 1):
        for k, y in enumerate((g["ny"] - 24, g["ny"] + 6, g["ny"] + 34)):
            hw = half_width(g, y)
            base = fr.pt(sd * (hw - 14), y)
            tufts.append((base, math.pi / 2 - sd * (1.15 - 0.30 * k) + fr.rot, rx * (0.36 - 0.03 * k), rx * 0.24, sd * 12))
    _tufts(c, tufts, under)


def angora_body(c, cx, cy, rx, ry, under, floor, start=0.0, end=0.95):
    """Long fluffy tufts down the sides of the body (an Angora's coat)."""
    tufts = []
    for sd in (-1, 1):
        for t in np.linspace(start, end, 4):
            a = t * 1.25 - 0.45
            x = cx + sd * rx * 0.98 * math.cos(a)
            y = min(cy + ry * 0.95 * math.sin(a), floor - 30)
            d = math.pi / 2 - sd * (1.05 - 0.55 * t)
            tufts.append(((x - sd * 12, y), d, rx * 0.36, rx * 0.30, sd * 14))
    _tufts(c, tufts, under)


# ---------------------------------------------------------------- awake (sitting up)
def sitting(c, grown: bool):
    cv = c.cv
    br = c.breed if c.marks_on else None
    _, _, geo = awake_geometry(grown, c.giant)
    g, body = geo["g"], geo["body"]
    cx, floor, chin, hy = geo["cx"], geo["floor"], geo["chin"], geo["hy"]
    top_w, mid_w, wide_w = body["top"], body["mid"], body["wide"]
    fr = Frame(cx, hy, 1.0, 0.0)
    hfill_raw = cv.fill_mask(fr.wrap(head_path(g)))
    contact = cv.blur(cv.shift(hfill_raw, 2, 14), 5) * (1 - hfill_raw)
    shade = [(contact, CONTACT)]
    ymid = chin + (floor - chin) * 0.48

    # ---- the cotton tail peeking out on the right (drawn first: the body covers half of it)
    tx, ty, tr = cx + wide_w * 0.98, floor - body["h"] * 0.20, body["h"] * 0.16
    tail = cv.fill_mask(ellipse(tx, ty, tr, tr * 0.92))
    part(c, fill=tail, light=(3, 4), details=lambda fill, F: cv.paint(fill, light_color(F), WG))

    # ---- the body: a soft pear widening to the haunches, the light belly (G)
    bpath = catmull([(cx - top_w, chin - 40), (cx, chin - 56), (cx + top_w, chin - 40), (cx + mid_w, ymid),
                     (cx + wide_w, floor - 40), (cx + wide_w * 0.82, floor - 2), (cx, floor + 2), (cx - wide_w * 0.82, floor - 2),
                     (cx - wide_w, floor - 40), (cx - mid_w, ymid)], tension=0.52)
    bfill_raw = cv.fill_mask(bpath)
    bm = []
    if br == "Dutch":
        # the white front: the chest and front paws, a clean edge round the middle of the body
        bm.append((cv.fill_mask(catmull([(cx - top_w * 1.3, chin - 60), (cx + top_w * 1.3, chin - 60), (cx + mid_w * 1.1, ymid - 6),
                                         (cx, ymid + 18), (cx - mid_w * 1.1, ymid - 6)], tension=0.45)), "g"))
    elif br == "Harlequin":
        m = np.zeros_like(cv.X)
        for x0, x1 in ((cx - wide_w * 1.2, cx - wide_w * 0.45), (cx + wide_w * 0.20, cx + wide_w * 0.62)):
            m = np.maximum(m, cv.fill_mask(poly([(x0, chin - 80), (x1, chin - 80), (x1 + 20, floor + 10), (x0 + 20, floor + 10)])))
        bm.append((m, "dark"))

    def belly(fill, F):
        b = cv.fill_mask(ellipse(cx, ymid + (floor - ymid) * 0.30, mid_w * 0.62, (floor - chin) * 0.40))
        cv.paint(b * fill, light_color(F), WG)

    bfill, _ = part(c, fill=bfill_raw, light=(8, 10), shade=shade, marks=bm, details=belly)

    # ---- the haunches: a soft rounded hip on each side, the big hind feet flat on the floor in front of them
    under = bfill
    for sd in (-1, 1):
        hx = cx + sd * wide_w * 0.80
        hip = cv.fill_mask(ellipse(hx, floor - body["h"] * 0.24, wide_w * 0.36, body["h"] * 0.26, sd * 0.25))
        hip = np.clip(hip - bfill_raw * 0.0, 0, 1)
        hm = [(np.ones_like(cv.X), "dark")] if br == "Harlequin" and sd < 0 else []
        f, _ = part(c, fill=hip, line=outline_over(cv, hip, bfill, thin=L.dw * 0.9) * smoothstep(chin + 60, chin + 120, cv.Y),
                    light=(5, 6), marks=hm)
        under = np.maximum(under, f)
    fw, fh = body["foot"]
    for sd in (-1, 1):
        fx = cx + sd * (wide_w * 0.62)
        foot = cv.fill_mask(ellipse(fx, floor - fh * 0.62, fw, fh, sd * 0.10))
        fm = [(np.ones_like(cv.X), "g")] if br == "Dutch" else []

        def sole(fill, F, fx=fx):
            cv.paint(cv.fill_mask(ellipse(fx + sd * fw * 0.10, floor - fh * 0.28, fw * 0.86, fh * 0.50)) * fill, light_color(F), WG)
            toes(cv, fill, fx - sd * fw * 0.55, floor - fh * 0.15, 7, 8)
        f, _ = part(c, fill=foot, line=outline_over(cv, foot, under, thin=L.dw), light=(3, 4), marks=fm, details=sole)
        under = np.maximum(under, f)

    # ---- two small front paws in front of the belly
    pw, ph = fw * 0.46, fh * 0.92
    for sd in (-1, 1):
        px = cx + sd * pw * 1.15
        paw = cv.fill_mask(ellipse(px, floor - ph * 0.92, pw, ph, sd * 0.10))
        leg = cv.fill_mask(catmull([(px - pw * 0.8, floor - ph * 0.9), (px - pw * 0.65, ymid + 10), (px + pw * 0.65, ymid + 10),
                                    (px + pw * 0.8, floor - ph * 0.9)], tension=0.5))
        lf = np.maximum(paw, leg * smoothstep(ymid + 30, ymid + 70, cv.Y))
        lm = [(np.ones_like(cv.X), "g")] if br == "Dutch" else []
        line = outline_over(cv, lf, under, thin=L.dw * 0.95) * smoothstep(ymid + 50, ymid + 90, cv.Y)

        def tip(fill, F, px=px):
            toes(cv, fill, px, floor - 4, 6, 8)
        f, _ = part(c, fill=lf, line=line, light=(3, 4), marks=lm, details=tip)
        under = np.maximum(under, f)

    if br == "Angora":
        angora_body(c, cx, ymid, wide_w, (floor - chin) * 0.55, under, floor)

    # ---- the neck wear, then the head between its ears
    # just below the chin, where the head leaves the chest showing
    neck = chain_points([((cx - top_w * 1.05, chin + 4), (cx - top_w * 0.4, chin + 36), (cx + top_w * 0.4, chin + 36),
                          (cx + top_w * 1.05, chin + 4))], n=40)
    collar_band(c, neck, width=18 * L.k / 1.6, bell_at=(cx, chin + 44), bell_r=13, occl=_outside(cv, under))

    if br == "Lionhead":
        under = np.maximum(under, mane(c, fr, g, under))
    if EARS.get(br) in EAR_LENGTH:
        upright_ears(c, fr, g, geo["ear"] * EAR_LENGTH[EARS[br]])
    hfill = draw_head(c, fr, g, under)
    if br == "Lionhead":
        crown_tuft(c, fr, g, hfill)
    elif br == "Angora":
        angora_cheeks(c, fr, g, np.maximum(hfill, under))
        crown_tuft(c, fr, g, hfill)
    lop_ears(c, fr, g, np.maximum(hfill, under))


def _outside(cv, body):
    """Everything outside the body's drawn silhouette (fill and outline), so neck wear never leaves it."""
    return 1 - np.clip(body + outline(cv, body), 0, 1)


# ---------------------------------------------------------------- rest (a loaf, eyes closed, ears laid back)
def resting(c, grown: bool):
    cv = c.cv
    br = c.breed if c.marks_on else None
    _, _, geo = rest_geometry(grown, c.giant)
    g, body = geo["g"], geo["body"]
    floor, bx, by, hx, hy = geo["floor"], geo["bx"], geo["by"], geo["hx"], geo["hy"]
    brx, bry = body["rx"], body["ry"]
    fr = Frame(hx, hy, 1.0, -0.05)
    hfill_raw = cv.fill_mask(fr.wrap(head_path(g)))
    contact = cv.blur(cv.shift(hfill_raw, 8, 8), 6) * (1 - hfill_raw)
    shade = [(contact, CONTACT)]

    # ---- the cotton tail at the back
    tr = bry * 0.30
    tail = cv.fill_mask(ellipse(bx + brx * 0.96, floor - bry * 0.70, tr, tr * 0.92))
    part(c, fill=tail, light=(3, 4), details=lambda fill, F: cv.paint(fill, light_color(F), WG))

    # ---- the loaf: a rounded mound, higher at the back
    path = catmull([(bx - brx, floor - 6), (bx - brx * 0.96, by - bry * 0.40), (bx - brx * 0.55, by - bry * 0.92),
                    (bx + brx * 0.20, by - bry * 1.02), (bx + brx * 0.78, by - bry * 0.70), (bx + brx * 1.0, by - bry * 0.05),
                    (bx + brx * 0.92, floor - 4), (bx, floor + 2)], tension=0.5)
    bfill_raw = cv.fill_mask(path)
    bm = []
    if br == "Dutch":
        bm.append((cv.fill_mask(poly([(bx - brx * 1.3, by - bry * 1.6), (bx - brx * 0.22, by - bry * 1.6),
                                      (bx - brx * 0.40, floor + 10), (bx - brx * 1.3, floor + 10)])), "g"))
    elif br == "Harlequin":
        m = np.zeros_like(cv.X)
        for x0 in (bx - brx * 0.30, bx + brx * 0.40):
            m = np.maximum(m, cv.fill_mask(poly([(x0, by - bry * 1.6), (x0 + brx * 0.32, by - bry * 1.6),
                                                 (x0 + brx * 0.22, floor + 10), (x0 - brx * 0.10, floor + 10)])))
        bm.append((m, "dark"))

    def belly(fill, F):
        cv.paint(cv.fill_mask(ellipse(bx - brx * 0.25, floor - 4, brx * 0.62, bry * 0.22)) * fill, light_color(F), WG)

    bfill, _ = part(c, fill=bfill_raw, light=(10, 12), shade=shade, marks=bm, details=belly)
    # the haunch curve and a hind foot along the floor at the back
    hip = cv.fill_mask(ellipse(bx + brx * 0.46, floor - bry * 0.50, brx * 0.42, bry * 0.50, -0.15))
    hm = [(np.ones_like(cv.X), "dark")] if br == "Harlequin" else []
    hipf, _ = part(c, fill=hip, line=outline_over(cv, hip, bfill, thin=L.dw * 0.9), light=(6, 8), marks=hm)
    foot = cv.fill_mask(ellipse(bx + brx * 0.30, floor - 16, brx * 0.30, 17, 0.02))
    footf, _ = part(c, fill=foot, line=outline_over(cv, foot, np.maximum(bfill, hipf), thin=L.dw), light=(3, 4),
                    details=lambda fill, F: cv.paint(cv.fill_mask(ellipse(bx + brx * 0.20, floor - 10, brx * 0.22, 9)) * fill,
                                                     light_color(F), WG))
    under = np.maximum(np.maximum(bfill, hipf), footf)

    # ---- front paws tucked under the chin
    for sd in (-1, 1):
        px = hx + sd * g["rx"] * 0.30 + 10
        paw = cv.fill_mask(ellipse(px, floor - 15, g["rx"] * 0.20, 15))
        lm = [(np.ones_like(cv.X), "g")] if br == "Dutch" else []
        f, _ = part(c, fill=paw, line=outline_over(cv, paw, under, thin=L.dw), light=(2, 3), marks=lm,
                    details=lambda fill, F, px=px: toes(cv, fill, px, floor - 4, 6, 7))
        under = np.maximum(under, f)
    if br == "Angora":
        angora_body(c, bx, by - bry * 0.1, brx * 0.92, bry, under, floor, start=0.35, end=1.0)

    # the neck shows behind the head's right side, where it meets the body
    neck = chain_points([((hx + g["rx"] * 0.55, hy + g["ry"] * 0.88), (hx + g["rx"] * 0.98, hy + g["ry"] * 0.66),
                          (hx + g["rx"] * 1.10, hy + g["ry"] * 0.10), (hx + g["rx"] * 1.02, hy - g["ry"] * 0.36))], n=40)
    collar_band(c, neck, width=17 * L.k / 1.6, bell_at=(hx + g["rx"] * 1.02, hy + g["ry"] * 0.50), bell_r=11,
                occl=_outside(cv, under))

    if br == "Lionhead":
        under = np.maximum(under, mane(c, fr, g, under))
    if EARS.get(br) in EAR_LENGTH:
        upright_ears(c, fr, g, geo["ear"] * EAR_LENGTH[EARS[br]], rest=True)
    hfill = draw_head(c, fr, g, under, closed=True)
    if br == "Lionhead":
        crown_tuft(c, fr, g, hfill)
    elif br == "Angora":
        angora_cheeks(c, fr, g, np.maximum(hfill, under))
    lop_ears(c, fr, g, np.maximum(hfill, under))


POSE_FUNCS = {
    "YoungAwake": lambda c: sitting(c, grown=False),
    "GrownAwake": lambda c: sitting(c, grown=True),
    "YoungRest": lambda c: resting(c, grown=False),
    "GrownRest": lambda c: resting(c, grown=True),
}


def render(pose: str, breed: str | None = None, capture: str | None = None) -> Canvas:
    """One pose (or a Flemish giant pose, "FlemishGiant" + pose) of the base, a breed overlay capture or a wear
    capture."""
    saved = (cat.REF_COAT, cat.REF_MARK, cat.REF_IRIS)
    use_rabbit_refs()
    try:
        giant = pose.startswith(VARIANT)
        plain = pose[len(VARIANT):] if giant else pose
        W, H = canvas_size(pose)
        set_tier("Grown" if plain.startswith("Grown") else "Young")
        cv = Canvas(W, H, capture=capture)
        wear = {"collar": "Collar", "bandana": "Bandana", "bow": "Bow"}.get(capture)
        c = RCtx(cv=cv, breed=breed, marks_on=(capture in ("marking", "darkpatch") and breed is not None),
                 collar_on=wear is not None, wear=wear or "Collar")
        c.giant = giant
        POSE_FUNCS[plain](c)
        return cv
    finally:
        cat.REF_COAT, cat.REF_MARK, cat.REF_IRIS = saved
