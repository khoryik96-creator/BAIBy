"""Placeholder hamster art (P2): four small poses in the cat's chibi style.

Same look as ``cat.py`` (big glossy eyes with two highlights, the baby's large
peach cheek blush, flat two-tone cel shading with a faint grain and #4D2917
outlines sized for the runtime draw scale), reusing its painting helpers: a
hamster is a round bun with no neck, puffy cheek pouches, small round ears, a
light muzzle and belly, and tiny pink paws held under the chin.

Region contract (as for the cat, see README "Contract"): R main coat, G the
light belly and muzzle (the breed coat's ``secondary`` colour), B iris, A the
accessory accent.  Unlike the cat, the hamster base carries G weight (every
hamster has a light belly).  Breed overlays:

* ``Markings/{WinterWhite,Sapphire,Campbells}/{Pose}Dark``: the dorsal stripe,
  recoloured on its own with the coat's ``dark`` colour (gTarget "dark").
* ``Markings/Panda/{Pose}Dark``: the panda's patches (ears, eye patches and a
  saddle band), gTarget "dark".
* ``Markings/Roborovski/{Pose}``: white brow spots (G).
* ``Markings/TeddyBear/{Pose}``: long fur tufts (a feature overlay in main coat,
  R weight) that reach past the base silhouette.
* ``Accessories/Bow/{Pose}``: a tiny bow between the ears (A, tag "bow").

The Syrian golden (free starter) is the plain base.
"""

from __future__ import annotations

import math

import numpy as np

import cat
from cat import (OUTLINE, PINK_EAR, PINK_EAR_DEEP, NOSE, BLUSH, BLUSH_A, WHITE, CONTACT, WR, WG, WA, W0, Ctx, Frame,
                 L, _accent_part, cel, chain_points, closed_eye, eye, outline, outline_over, part, smoothstep, stroke_marks,
                 warm)
from raster import Canvas, catmull, ellipse, poly, polyline, tapered

# Reference colours (the recolour contract): a golden Syrian back, a cream belly, dark glossy eyes, and the
# baby outfit accent reference for the bow.
REF_COAT = (232, 170, 98)
REF_MARK = (248, 234, 212)
REF_IRIS = (120, 80, 66)
REF_ACCENT = cat.REF_ACCENT

PAW = (246, 186, 178)
PAW_DEEP = (232, 160, 156)

POSES = ["YoungAwake", "YoungRest", "GrownAwake", "GrownRest"]
# Suggested runtime scale beside the baby: a young hamster is a little smaller than a kitten's head.
RUNTIME_SCALE = {"Young": 0.48, "Grown": 0.56}
CANVAS = {"YoungAwake": (480, 470), "GrownAwake": (520, 500), "YoungRest": (560, 400), "GrownRest": (600, 420)}
# Breed overlays: (breed, kind) with kind "g" (Markings/{breed}/{pose}) or "dark" (Markings/{breed}/{pose}Dark).
MARKINGS = [("Roborovski", "g"), ("TeddyBear", "g"), ("WinterWhite", "dark"), ("Sapphire", "dark"),
            ("Campbells", "dark"), ("Panda", "dark")]
STRIPED = ("WinterWhite", "Sapphire", "Campbells")
WEAR = ["Bow"]
# The Teddy bear's long coat reaches past the base silhouette (a feature overlay).
FEATURE = ("TeddyBear",)


def set_tier(tier: str) -> None:
    L.ow = round(cat.BABY_OW / RUNTIME_SCALE[tier] * 1.04, 1)   # Young 17.3, Grown 14.9
    L.k = L.ow / cat.BASE_OW
    L.dw = cat.BASE_DW * L.k


def use_hamster_refs() -> None:
    """Point cat.py's painting helpers at the hamster's reference colours."""
    cat.REF_COAT = REF_COAT
    cat.REF_MARK = REF_MARK
    cat.REF_IRIS = REF_IRIS


def belly_color(F):
    return np.asarray(REF_MARK, float)[None, None, :] * warm(F)


def coat_color(F):
    return np.asarray(REF_COAT, float)[None, None, :] * warm(F)


class HCtx(Ctx):
    """The cat's context plus the hamster's capture flags."""


def _bun(cx, cy, rx, ry_top, ry_bot, n=360, flat=0.0, floor=None, pinch=0.0):
    """A soft bun outline: rounder on top, fuller below; optionally flattened on the floor."""
    th = np.linspace(-np.pi, np.pi, n, endpoint=False)
    c, s = np.cos(th), np.sin(th)
    ry = np.where(s < 0, ry_top, ry_bot)
    x = cx + rx * c * (1 + 0.05 * np.clip(s, 0, None)) * (1 - pinch * np.clip(-s, 0, None) ** 2)
    y = cy + ry * s
    if floor is not None:
        y = np.minimum(y, floor)
    return poly([tuple(q) for q in np.stack([x, y], 1)])


# ---------------------------------------------------------------- face
def face(c, fr, g, fill, closed=False):
    """Blush, eyes, a light muzzle (G), nose, mouth and whiskers inside the head ``fill``."""
    cv = c.cv
    k = L.k
    ex, ey, er = g["ex"], g["ey"], g["er"]
    # muzzle: a light rounded patch round the nose and mouth, and the chin
    muzzle = cv.fill_mask(fr.wrap(catmull([(-46, g["ny"] - 26), (0, g["ny"] - 34), (46, g["ny"] - 26), (62, g["ny"] + 12),
                                            (34, g["ny"] + 46), (0, g["ny"] + 52), (-34, g["ny"] + 46),
                                            (-62, g["ny"] + 12)], tension=0.5)))
    F = cel(cv, fill, (9, 11), reach=2.0)
    cv.paint(muzzle * fill, belly_color(F), WG)
    for sd in (-1, 1):
        bl = cv.fill_mask(fr.wrap(ellipse(sd * (ex + er * 0.42), ey + er * 1.10, er * 0.70, er * 0.44)))
        cv.paint(cv.blur(bl, 3.0) * fill * BLUSH_A, BLUSH, glaze=True)
    for sd in (-1, 1):
        if closed:
            closed_eye(c, fr, sd * ex, ey, er, sd)
        else:
            eye(c, fr, sd * ex, ey, er, 1.05, 0.0, sd)
    ny = g["ny"]
    nose = fr.wrap(catmull([(-9, ny - 5), (0, ny - 6.5), (9, ny - 5), (1.5, ny + 4), (-1.5, ny + 4)], tension=0.6))
    cv.paint(cv.fill_mask(nose), NOSE)
    cv.paint(cv.fill_mask(fr.wrap(ellipse(-3, ny - 3, 3, 1.8, -0.2))) * 0.85, WHITE)
    cv.paint(cv.stroke_mask(nose, 2.6 * k * fr.s), OUTLINE)

    def m(ctx):
        ctx.move_to(0, ny + 4)
        ctx.line_to(0, ny + 10)
        ctx.move_to(-14, ny + 8)
        ctx.curve_to(-12, ny + 18, -2, ny + 18, 0, ny + 10)
        ctx.curve_to(2, ny + 18, 12, ny + 18, 14, ny + 8)
    cv.paint(cv.stroke_mask(fr.wrap(m), L.dw * 0.75 * fr.s), OUTLINE)
    # two tiny front teeth peeking under the mouth (awake only)
    if not closed:
        teeth = cv.fill_mask(fr.wrap(catmull([(-5, ny + 14), (5, ny + 14), (5, ny + 21), (-5, ny + 21)], tension=0.2)))
        cv.paint(teeth, WHITE)
        cv.paint(cv.stroke_mask(fr.wrap(catmull([(-5, ny + 14), (5, ny + 14), (5, ny + 21), (-5, ny + 21)], tension=0.2)),
                                1.8 * k), OUTLINE)


def whiskers(c, fr, g, half_w):
    cv = c.cv
    for sd in (-1, 1):
        for (y0, dy, ln) in ((g["ny"] + 2, -10, 1.0), (g["ny"] + 12, 2, 0.9)):
            x0 = half_w + L.ow * 0.4 + 2
            Lw = 40 * ln
            pts = chain_points([((sd * x0, y0), (sd * (x0 + Lw * 0.35), y0 + dy * 0.15),
                                 (sd * (x0 + Lw * 0.75), y0 + dy * 0.7), (sd * (x0 + Lw), y0 + dy))], n=20)
            pts = np.array([fr.pt(*q) for q in pts])
            cv.paint(cv.fill_mask(tapered(pts, 3.0 * L.k * fr.s, taper=(0.9, 0.2))), OUTLINE)


def ear(c, fr, x, y, r, tilt, marks):
    cv = c.cv
    efill = cv.fill_mask(fr.wrap(ellipse(x, y, r, r * 0.92, tilt)))

    def inner(fill, F):
        inn = cv.fill_mask(fr.wrap(ellipse(x + (2 if x > 0 else -2), y + 3, r * 0.60, r * 0.54, tilt)))
        cv.paint(inn * fill, PINK_EAR)
        cv.paint(cv.blur(inn, 3) * fill * 0.45, PINK_EAR_DEEP, glaze=True)
    return part(c, fill=efill, light=(4, 5), marks=marks, details=inner)


# ---------------------------------------------------------------- awake (sitting up)
def sitting(c, grown: bool):
    cv = c.cv
    br = c.breed if c.marks_on else None
    W = cv.W
    cx = W / 2
    if grown:
        floor = 470
        body = dict(cy=330, rx=184, top=150, bot=146)
        g = dict(hx=cx, hy=208, rx=162, ry=128, cheek=0.16, ex=66, ey=-4, er=37, ny=40, ear=(0.60, -0.80, 40))
    else:
        floor = 440
        body = dict(cy=318, rx=150, top=132, bot=126)
        g = dict(hx=cx, hy=200, rx=168, ry=136, cheek=0.14, ex=70, ey=-2, er=42, ny=42, ear=(0.58, -0.82, 42))
    fr = Frame(g["hx"], g["hy"], 1.0, 0.0)

    # ---- body: a round bun sitting on the floor, the belly light (G)
    bpath = _bun(cx, body["cy"], body["rx"], body["top"], body["bot"], floor=floor - 4)
    bfill_raw = cv.fill_mask(bpath)
    hmask = cv.fill_mask(fr.wrap(_head_path(g)))
    contact = cv.blur(cv.shift(hmask, 2, 14), 5) * (1 - hmask)
    bm = []
    if br == "Panda":
        # a dark saddle band round the middle of the body
        band = cv.fill_mask(catmull([(cx - body["rx"] - 10, body["cy"] - 40), (cx, body["cy"] - 58),
                                     (cx + body["rx"] + 10, body["cy"] - 40), (cx + body["rx"] + 10, body["cy"] + 36),
                                     (cx, body["cy"] + 20), (cx - body["rx"] - 10, body["cy"] + 36)], tension=0.45))
        bm.append((band, "dark"))

    def belly(fill, F):
        b = cv.fill_mask(ellipse(cx, body["cy"] + body["bot"] * 0.34, body["rx"] * 0.62, body["bot"] * 0.74))
        cv.paint(b * fill, belly_color(F), WG)

    bfill, _ = part(c, fill=bfill_raw, light=(10, 12), shade=[(contact, CONTACT)], marks=bm, details=belly)
    body_cover = np.maximum(bfill, outline(cv, bfill_raw))

    # ---- feet: two little pink feet peeking out at the floor
    for sd in (-1, 1):
        fx = cx + sd * body["rx"] * 0.42
        fp = ellipse(fx, floor - 14, 30, 15, sd * 0.08)
        ff = cv.fill_mask(fp)
        F = cel(cv, ff, (3, 4), 0.86)
        cv.paint(ff, np.asarray(PAW, float)[None, None, :] * F[..., None], W0)
        for dx in (-9, 0, 9):
            cv.paint(cv.stroke_mask(polyline([(fx + dx, floor - 24), (fx + dx * 1.1, floor - 16)]), L.dw * 0.55) * ff, OUTLINE)
        cv.paint(outline(cv, ff, ow=L.ow * 0.8), OUTLINE)

    # ---- ears behind the head, then the head (no neck: it sits on the bun)
    ex_, ey_, er_ = g["ear"]
    for sd in (-1, 1):
        em = []
        if br == "Panda":
            em.append((np.ones_like(cv.X), "dark"))
        ear(c, fr, sd * g["rx"] * ex_, g["ry"] * ey_, er_, sd * 0.25, em)
    hm = []
    if br in STRIPED:
        hm.append((_crown_stripe(cv, fr, g), "dark"))
    elif br == "Panda":
        for sd in (-1, 1):
            hm.append((cv.fill_mask(fr.wrap(ellipse(sd * g["ex"], g["ey"] + 4, g["er"] * 1.32, g["er"] * 1.22, sd * 0.35))), "dark"))
    elif br == "Roborovski":
        for sd in (-1, 1):
            hm.append((cv.fill_mask(fr.wrap(ellipse(sd * (g["ex"] - 4), g["ey"] - g["er"] * 1.55, g["er"] * 0.46, g["er"] * 0.30,
                                                    sd * -0.25))), "g"))
    hfill = cv.fill_mask(fr.wrap(_head_path(g)))
    hline = outline_over(cv, hfill, bfill)
    part(c, fill=hfill, line=hline, light=(9, 11), reach=2.2, marks=hm,
         details=lambda fill, F: face(c, fr, g, fill))
    whiskers(c, fr, g, _half_width(g, g["ny"]))

    # ---- tiny paws held together under the chin
    for sd in (-1, 1):
        px, py = cx + sd * 26, g["hy"] + g["ry"] + 8
        arm = cv.fill_mask(catmull([(px + sd * 34, py - 30), (px + sd * 6, py - 10), (px - sd * 4, py + 8), (px + sd * 20, py + 18),
                                    (px + sd * 44, py - 4)], tension=0.5))
        arm = arm * (1 - hfill)
        part(c, fill=arm, light=(4, 5), line=outline(cv, arm, ow=L.dw * 1.2, lo=1, hi=1))
        pw = cv.fill_mask(ellipse(px, py + 4, 17, 13, sd * 0.3))
        F = cel(cv, pw, (2, 3), 0.86)
        cv.paint(pw, np.asarray(PAW, float)[None, None, :] * F[..., None], W0)
        cv.paint(outline(cv, pw, ow=L.dw * 1.2, lo=1, hi=1), OUTLINE)

    _bow(c, fr, g)
    if br == "TeddyBear":
        _teddy_awake(c, fr, g, cx, body, floor, np.maximum(bfill, hfill))


def _head_path(g):
    """A wide head with puffy cheek pouches: rounder on top, the cheeks bulging out below the eyes."""
    th = np.linspace(-np.pi, np.pi, 720, endpoint=False)
    c, s = np.cos(th), np.sin(th)
    e = np.where(s < 0, 1.0 / 0.95, 1.0 / 1.05)
    x = g["rx"] * np.sign(c) * np.abs(c) ** e
    y = g["ry"] * np.sign(s) * np.abs(s) ** e
    phi = np.arctan2(np.clip(s, 0, None), np.abs(c))
    lower = smoothstep(-0.1, 0.5, s)
    x = x * (1 + g["cheek"] * np.exp(-((phi - 0.35) / 0.42) ** 2) * lower)
    return poly([tuple(q) for q in np.stack([x, y], 1)])


def _half_width(g, y):
    th = np.linspace(0, np.pi / 2, 200)
    c, s = np.cos(th), np.sin(th)
    x = g["rx"] * c ** (1 / 1.05)
    yy = g["ry"] * s ** (1 / 1.05)
    phi = th
    x = x * (1 + g["cheek"] * np.exp(-((phi - 0.35) / 0.42) ** 2) * smoothstep(-0.1, 0.5, s))
    return float(np.interp(y, yy, x))


def _crown_stripe(cv, fr, g):
    """The dorsal stripe where it shows from the front: a tapered stroke from the forehead over the crown."""
    pts = np.array([fr.pt(0, g["ey"] - g["er"] * 1.15), fr.pt(1, -g["ry"] * 0.6), fr.pt(0, -g["ry"] * 1.05)])
    pts = chain_points([(pts[0], pts[1], pts[1], pts[2])], n=30)
    return cv.fill_mask(tapered(pts, 22 * L.k / 2.0, taper=(0.15, 0.7)))


def _bow(c, fr, g):
    """A tiny bow between the ears (region A, tag "bow")."""
    if not c.collar_on:
        return
    cv = c.cv
    bx, by = fr.pt(g["rx"] * 0.30, -g["ry"] * 0.86)
    s = 17.0
    loops = np.zeros_like(cv.X)
    for side in (-1, 1):
        loops = np.maximum(loops, cv.fill_mask(ellipse(bx + side * s * 0.95, by, s * 0.95, s * 0.62, rot=side * -0.25)))
    shown = _accent_part(c, loops, "bow", None)
    for side in (-1, 1):
        inner = cv.fill_mask(ellipse(bx + side * s * 0.55, by + 1, s * 0.36, s * 0.26, rot=side * -0.25)) * shown
        cv.paint(inner, np.asarray(REF_ACCENT, float) * 0.72, WA, tag="bow")
    _accent_part(c, cv.fill_mask(ellipse(bx, by, s * 0.36, s * 0.42)), "bow", None, shade_k=0.95)


# ---------------------------------------------------------------- the Teddy bear's long coat
def _flame(base, direction, length, bend):
    """Points of one fur tuft: from ``base`` along ``direction`` (radians), curling by ``bend``."""
    dx, dy = math.cos(direction), math.sin(direction)
    nx, ny = -dy, dx
    tip = (base[0] + dx * length + nx * bend, base[1] + dy * length + ny * bend)
    mid = (base[0] + dx * length * 0.5 + nx * bend * 0.2, base[1] + dy * length * 0.5 + ny * bend * 0.2)
    return chain_points([(base, mid, mid, tip)], n=24)


def _fur(c, tufts, body):
    """Long fur tufts in the main coat (R): wide at the root (inside the silhouette, so they cover its outline) and
    pointed at the tip.  Outlined at full weight outside the body and fading to a fine line over it, so the coat
    reads as one shaggy silhouette; a short flick in each tuft; tag "marking"."""
    cv = c.cv
    m = np.zeros_like(cv.X)
    flicks = np.zeros_like(cv.X)
    for (base, direction, length, width, bend) in tufts:
        pts = _flame(base, direction, length, bend)
        m = np.maximum(m, cv.fill_mask(tapered(pts, width, taper=(0.95, 0.06),
                                                profile=lambda t: np.clip(1.12 - t ** 1.8, 0.16, 1.0))))
        m = np.maximum(m, cv.fill_mask(ellipse(*pts[-1], width * 0.09, width * 0.09)))
        inner = _flame(base, direction, length * 0.50, bend * 0.6)[10:]
        flicks = np.maximum(flicks, cv.fill_mask(tapered(inner, L.dw * 0.62, taper=(0.2, 0.1))))
    F = cel(cv, m, (4, 5), 0.82, reach=2.2) * cat.grain(cv)
    cv.paint(m, coat_color(F), WR, tag="marking")
    cv.paint(flicks * m * 0.55, OUTLINE, tag="marking")
    line = outline_over(cv, m, body, ow=L.ow * 0.85, thin=L.dw * 0.45, soft=1.5)
    cv.paint(line, OUTLINE, tag="marking")


def _teddy_awake(c, fr, g, cx, body, floor, under):
    tufts = []
    # cheek ruffs falling outward from the pouches
    for sd in (-1, 1):
        for k, y in enumerate((g["ny"] - 18, g["ny"] + 8, g["ny"] + 32)):
            hw = _half_width(g, y)
            base = fr.pt(sd * (hw - 14), y)
            d = math.pi / 2 - sd * (1.05 - 0.28 * k)
            tufts.append((base, d, 58 - 5 * k, 38, sd * 14))
    # a shaggy coat down the sides of the bun
    for sd in (-1, 1):
        for k, t in enumerate((0.0, 0.40, 0.80)):
            a = t * 0.95 - 0.10
            x = cx + sd * body["rx"] * 0.96 * math.cos(a)
            y = body["cy"] + body["bot"] * 0.96 * math.sin(a) - 6
            d = math.pi / 2 - sd * (1.0 - 0.45 * t)
            tufts.append(((x - sd * 14, y), d, 54, 42, sd * 16))
    # a little crest on the crown
    for dx, d, ln in ((-16, -math.pi / 2 - 0.50, 38), (4, -math.pi / 2 + 0.05, 46), (22, -math.pi / 2 + 0.55, 36)):
        tufts.append((fr.pt(dx, -g["ry"] * 0.86), d, ln, 30, 10))
    _fur(c, tufts, under)


def _teddy_rest(c, fr, g, body, under):
    cx, cy, rx, ry, floor = body
    tufts = []
    # a long coat flowing back along the arched back and over the rump
    for k in range(6):
        t = (k + 0.5) / 6
        a = math.pi * (1.28 + 0.66 * t)
        x, y = cx + rx * 0.90 * math.cos(a), cy + ry * 0.88 * math.sin(a)
        # along the back towards the rump, lifted a little off the body, curling down at the tip
        d = math.atan2(ry * math.cos(a), -rx * math.sin(a)) - 0.42
        tufts.append(((x, y), d, 56, 44, 16))
    # the rump and a skirt brushing the floor
    for k, (a, ln) in enumerate(((-0.25, 52), (0.10, 54), (0.42, 48), (0.75, 42))):
        x, y = cx + rx * 0.94 * math.cos(a), cy + ry * 0.94 * math.sin(a)
        tufts.append(((x - 12, min(y, floor - 22)), a + 0.30, ln, 42, 14))
    for sd in (-1, 1):
        y = g["ny"] + 8
        base = fr.pt(sd * (_half_width(g, y) - 14), y)
        tufts.append((base, math.pi / 2 - sd * 0.9, 46, 34, sd * 12))
    _fur(c, tufts, under)


# ---------------------------------------------------------------- rest (curled up asleep)
def resting(c, grown: bool):
    cv = c.cv
    br = c.breed if c.marks_on else None
    W = cv.W
    if grown:
        floor = 390
        cx, cy, rx, ry = W / 2 + 40, 262, 230, 128
        g = dict(hx=W / 2 - 88, hy=262, rx=140, ry=112, cheek=0.12, ex=56, ey=0, er=34, ny=36, ear=(0.58, -0.78, 34))
    else:
        floor = 370
        cx, cy, rx, ry = W / 2 + 36, 252, 206, 118
        g = dict(hx=W / 2 - 80, hy=250, rx=146, ry=118, cheek=0.12, ex=60, ey=2, er=38, ny=38, ear=(0.58, -0.80, 36))
    fr = Frame(g["hx"], g["hy"], 1.0, -0.12)

    bpath = _bun(cx, cy, rx, ry, ry, floor=floor - 4, pinch=0.0)
    bfill_raw = cv.fill_mask(bpath)
    hmask = cv.fill_mask(fr.wrap(_head_path(g)))
    contact = cv.blur(cv.shift(hmask, 8, 8), 6) * (1 - hmask)
    bm = []
    if br in STRIPED:
        # the dorsal stripe along the arched back
        a = np.linspace(math.pi * 1.10, math.pi * 1.92, 60)
        pts = np.stack([cx + rx * 0.80 * np.cos(a), cy + ry * 0.84 * np.sin(a)], 1)
        bm.append((cv.fill_mask(tapered(pts, 30 * L.k / 2.0, taper=(0.5, 0.25))), "dark"))
    elif br == "Panda":
        band = cv.fill_mask(catmull([(cx - rx * 0.02, cy - ry - 10), (cx + rx * 0.30, cy - ry - 14), (cx + rx * 0.52, cy - ry * 0.2),
                                     (cx + rx * 0.50, floor + 4), (cx + rx * 0.10, floor + 4), (cx + rx * 0.02, cy)],
                                    tension=0.5))
        bm.append((band, "dark"))

    def belly(fill, F):
        b = cv.fill_mask(ellipse(cx - rx * 0.1, floor - 10, rx * 0.78, ry * 0.40))
        cv.paint(b * fill, belly_color(F), WG)

    bfill, _ = part(c, fill=bfill_raw, light=(10, 12), shade=[(contact, CONTACT)], marks=bm, details=belly)
    # a hind foot peeking out at the back
    fx, fy = cx + rx * 0.62, floor - 16
    ff = cv.fill_mask(ellipse(fx, fy, 28, 13, -0.1))
    F = cel(cv, ff, (3, 4), 0.86)
    cv.paint(ff, np.asarray(PAW, float)[None, None, :] * F[..., None], W0)
    cv.paint(outline(cv, ff, ow=L.ow * 0.8), OUTLINE)

    ex_, ey_, er_ = g["ear"]
    for sd in (-1, 1):
        em = [(np.ones_like(cv.X), "dark")] if br == "Panda" else []
        ear(c, fr, sd * g["rx"] * ex_, g["ry"] * ey_, er_ * (0.9 if sd > 0 else 1.0), sd * 0.25, em)
    hm = []
    if br in STRIPED:
        hm.append((_crown_stripe(cv, fr, g), "dark"))
    elif br == "Panda":
        for sd in (-1, 1):
            hm.append((cv.fill_mask(fr.wrap(ellipse(sd * g["ex"], g["ey"] + 6, g["er"] * 1.30, g["er"] * 1.05, sd * 0.35))), "dark"))
    elif br == "Roborovski":
        for sd in (-1, 1):
            hm.append((cv.fill_mask(fr.wrap(ellipse(sd * (g["ex"] - 4), g["ey"] - g["er"] * 1.15, g["er"] * 0.46, g["er"] * 0.30,
                                                    sd * -0.25))), "g"))
    hfill = cv.fill_mask(fr.wrap(_head_path(g)))
    hline = outline_over(cv, hfill, bfill)
    part(c, fill=hfill, line=hline, light=(9, 11), reach=2.2, marks=hm,
         details=lambda fill, F: face(c, fr, g, fill, closed=True))
    whiskers(c, fr, g, _half_width(g, g["ny"]))

    # paws tucked together under the chin
    for sd in (-1, 1):
        px, py = fr.pt(sd * 22, g["ry"] + 2)
        pw = cv.fill_mask(ellipse(px, py, 16, 11, sd * 0.2)) * (1 - hfill * 0.0)
        F = cel(cv, pw, (2, 3), 0.86)
        cv.paint(pw, np.asarray(PAW, float)[None, None, :] * F[..., None], W0)
        cv.paint(outline(cv, pw, ow=L.dw * 1.2, lo=1, hi=1), OUTLINE)

    _bow(c, fr, g)
    if br == "TeddyBear":
        _teddy_rest(c, fr, g, (cx, cy, rx, ry, floor), np.maximum(bfill, hfill))


POSE_FUNCS = {
    "YoungAwake": lambda c: sitting(c, grown=False),
    "GrownAwake": lambda c: sitting(c, grown=True),
    "YoungRest": lambda c: resting(c, grown=False),
    "GrownRest": lambda c: resting(c, grown=True),
}


def render(pose: str, breed: str | None = None, capture: str | None = None) -> Canvas:
    saved = (cat.REF_COAT, cat.REF_MARK, cat.REF_IRIS)
    use_hamster_refs()
    try:
        W, H = CANVAS[pose]
        set_tier("Grown" if pose.startswith("Grown") else "Young")
        cv = Canvas(W, H, capture=capture)
        wear = {"bow": "Bow"}.get(capture)
        c = HCtx(cv=cv, breed=breed, marks_on=(capture in ("marking", "darkpatch") and breed is not None),
                 collar_on=wear is not None, wear=wear or "Bow")
        POSE_FUNCS[pose](c)
        return cv
    finally:
        # the cat's painting helpers read these module globals: give the cat its own colours back
        cat.REF_COAT, cat.REF_MARK, cat.REF_IRIS = saved
