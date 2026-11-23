"""Procedurally drawn hair styles (Coils, Smooth, Tuft) in the art's chibi style.

Every style is drawn in the head-local frame (see common.HeadFrame), so it
follows each pose's head position and tilt.  Hair paint uses colours derived
from the measured hair reference with hair weight 1, so the runtime recolour
maps every hair colour option onto it; outlines use the art's dark brown with
weight 0.
"""

from __future__ import annotations

import math

import numpy as np

from common import safe_scale
from paint import HAIR, FIXED, Canvas, bezier, shade, spline

LINE_PX = 7.0        # outer outline weight in pose pixels (matches the art)
DETAIL_PX = 3.2      # interior strand line weight


def skull_pts(fr, scale=1.0, t0=0.0, t1=180.0, n=90):
    return [fr.skull_point(math.radians(t0 + (t1 - t0) * i / n), scale) for i in range(n + 1)]


def head_scale(fr):
    """Pose pixels per head unit, used to keep line weights in pose pixels."""
    return fr.rx


def inside_image(cv: Canvas, sil, margin_px=5.0, round_px=6.0):
    """Keep a silhouette clear of the pose border, ending in a rounded lobe.

    Where hair would run into the image edge it is trimmed ``margin_px`` inside
    it and the cut is rounded off (morphological opening), so the outline closes
    the hair instead of a hard vertical cut.
    """
    m = int(margin_px * cv.ss)
    if not cv.touches_border(sil, margin_px):
        return sil
    out = sil.copy()
    out[:m] = False; out[-m:] = False; out[:, :m] = False; out[:, -m:] = False
    r = int(round_px * cv.ss)
    yy, xx = np.ogrid[-r:r + 1, -r:r + 1]
    disk = xx * xx + yy * yy <= r * r
    from scipy import ndimage as ndi
    opened = ndi.binary_opening(out, structure=disk)
    return opened


def outlined(cv: Canvas, sil, ref, base_col, line_px=LINE_PX):
    """Fill a silhouette with hair colour and stroke its edge with the outline colour."""
    cv.paint(sil, base_col, HAIR)
    band = cv.outline_band(sil, line_px)
    cv.paint(band, ref["outline"], FIXED)


# --------------------------------------------------------------------- Smooth

def smooth(ctx):
    fr = ctx["frame"]; ref = ctx["ref"]
    cv = Canvas(fr, ctx["shape"])
    H = ref["hair"]
    T = fr.top
    # sleek cap hugging the skull, slightly fuller at the crown
    r = fr.skull_radius(cv.U, cv.V)
    cap = (r < 1.075) & (cv.V > -0.05)
    # hairline: low at the temples, high at the side part (viewer's right)
    hl = spline([(-1.30, 0.30), (-1.06, 0.33), (-0.95, 0.50), (-0.70, 0.78), (-0.30, 0.92),
                 (0.10, 0.98), (0.45, 1.06), (0.58, 1.12), (0.72, 1.00), (0.88, 0.78),
                 (0.99, 0.52), (1.06, 0.36), (1.30, 0.32)], 16)
    cap &= ~cv.polygon(hl + [(1.6, -1.0), (-1.6, -1.0)])
    # the side-swept lock: from the part, across the forehead, to a soft point
    top_edge = bezier((0.60, 1.12), (0.10, 1.02), (-0.45, 0.90), (-0.90, 0.56), n=40)
    low_edge = bezier((-0.90, 0.56), (-0.60, 0.64), (-0.15, 0.70), (0.42, 0.98), n=40)
    lock = cv.polygon(top_edge + low_edge[1:] + [(0.60, 1.12)])
    sil = cap | lock
    # no thin slivers of cap along the temples (they read as a stray double
    # line); strands end on the head, never splaying past it
    sil = largest_parts(rounded(cv, sil, 7.0))
    sil = inside_image(cv, sil)
    outlined(cv, sil, ref, H)
    band = cv.outline_band(sil, LINE_PX)
    inner = sil & ~band
    # cel shade: the far (right/back) side of the cap and the lock's underside
    sh = inner & (fr.skull_radius(cv.U + 0.17, cv.V + 0.06) > 1.0)
    cv.paint(sh, shade(H, 0.80), HAIR)
    under = inner & lock & ~cv.polygon(bezier((0.62, 1.20), (0.10, 1.10), (-0.45, 0.98), (-0.95, 0.62), n=40)
                                         + bezier((-0.95, 0.62), (-0.60, 0.70), (-0.15, 0.78), (0.45, 1.06), n=40)[1:])
    cv.paint(under, shade(H, 0.84), HAIR)
    # glossy sheen arc across the crown
    sheen = cv.stroke(bezier((-0.70, T * 0.76), (-0.20, T * 1.00), (0.30, T * 0.98), n=40), 0, 0,
                      taper=lambda t: 0.085 * math.sin(math.pi * t) ** 0.8 + 0.008) & inner
    cv.paint(sheen, shade(H, 1.17), HAIR)
    # strand lines following the sweep + the lock's separation line
    dark = shade(H, 0.55)
    w = DETAIL_PX / fr.rx
    for pts in [
        top_edge[2:-4],
        bezier((0.50, 1.18), (0.05, 1.30), (-0.55, 1.16), (-0.93, 0.80)),
        bezier((0.58, 1.26), (0.20, 1.40), (-0.30, 1.38), (-0.70, 1.16)),
        bezier((0.66, 1.14), (0.84, 1.10), (0.97, 0.86), (1.00, 0.62)),
        bezier((0.20, 0.86), (-0.10, 0.84), (-0.40, 0.76), (-0.62, 0.66)),
    ]:
        m = cv.stroke(pts, 0, 0, taper=lambda t: w * (0.25 + 1.1 * math.sin(math.pi * t))) & inner
        cv.paint(m, dark, HAIR)
    return cv.result()


# ---------------------------------------------------------------------- Coils

def largest_parts(sil, frac=0.15):
    """Drop small disconnected bits (floating squiggles) of a silhouette."""
    from scipy import ndimage as ndi
    lab, n = ndi.label(sil)
    if n <= 1:
        return sil
    sizes = ndi.sum(sil, lab, range(1, n + 1))
    return np.isin(lab, 1 + np.nonzero(sizes >= frac * sizes.max())[0])


def rounded(cv: Canvas, sil, px=5.0):
    """Round off sharp corners (e.g. where the hairline cuts a puff)."""
    from scipy import ndimage as ndi
    r = int(px * cv.ss)
    yy, xx = np.ogrid[-r:r + 1, -r:r + 1]
    return ndi.binary_opening(sil, structure=(xx * xx + yy * yy <= r * r))


def coils(ctx):
    fr = ctx["frame"]; ref = ctx["ref"]
    cv = Canvas(fr, ctx["shape"])
    H = ref["hair"]
    rng = np.random.RandomState(11)
    hl = spline([(-1.02, 0.10), (-0.93, 0.44), (-0.64, 0.72), (-0.25, 0.84), (0.12, 0.86),
                 (0.48, 0.80), (0.80, 0.62), (0.97, 0.36), (1.02, 0.10)], 12)
    face = cv.polygon(hl + [(1.02, -1.2), (-1.02, -1.2)])

    def cloud(k, pr):
        # round cloud: clumps of puffs on an enlarged skull outline, ending in
        # round lobes above the ears (never hanging down the cheeks)
        sil = cv.empty()
        for i, deg in enumerate(np.arange(16, 166, 12.5)):
            u, v = fr.skull_point(math.radians(deg), k)
            sil |= cv.circle(u, v, pr * (1.0 + 0.10 * math.sin(i * 2.3)))
        for side in (-1, 1):                     # side lobes that end over the ears
            sil |= cv.circle(side * (k - 0.10), 0.36, pr * 0.78)
        sil |= (fr.skull_radius(cv.U, cv.V) < k) & (cv.V > 0.30)
        sil &= ~face
        sil &= cv.V > 0.14
        # forehead hairline made of little coils
        for i in range(6, len(hl) - 6, 8):
            u, v = hl[i]
            if v > 0.45:
                sil |= cv.circle(u, v + 0.015, 0.085)
        return largest_parts(rounded(cv, sil))

    # as big as the pose image allows (never cut by the image edge)
    k, pr = 1.15, 0.25
    sil = cloud(k, pr)
    ks = safe_scale(fr, ctx["shape"]) - 0.07
    r = fr.skull_radius(cv.U, cv.V)
    while (cv.touches_border(sil, 3) or (sil & (r > ks)).any()) and k > 1.0:
        k -= 0.015
        pr *= 0.975
        sil = cloud(k, pr)
    sil = inside_image(cv, sil)
    outlined(cv, sil, ref, H)
    band = cv.outline_band(sil, LINE_PX)
    inner = sil & ~band
    # cel shading: lower rim of the cloud and the part framing the face
    lower = inner & (fr.skull_radius(cv.U * 0.98, cv.V - 0.20) > 1.10)
    near_face = inner & (np.hypot(cv.U, (cv.V - 0.45) / 0.55) < 1.32)
    cv.paint(lower | near_face, shade(H, 0.86), HAIR)
    # soft light on the upper-left of the cloud
    lit = inner & (fr.skull_radius(cv.U + 0.30, cv.V - 0.22) < 0.78) & ~near_face
    cv.paint(lit, shade(H, 1.07), HAIR)
    # clumps: rounded bunches with a soft shadow crescent under each one
    keep = sil & ~cv.outline_band(sil, LINE_PX + 2.5)
    clump_sh = cv.empty()
    clump_hi = cv.empty()
    cs = 0.30
    for j, gv in enumerate(np.arange(0.1, fr.top + 0.6, cs * 0.8)):
        for gu in np.arange(-1.6, 1.6, cs):
            u = gu + (cs / 2 if j % 2 else 0) + rng.uniform(-0.06, 0.06)
            v = gv + rng.uniform(-0.05, 0.05)
            R = cs * rng.uniform(0.50, 0.68)
            c = cv.circle(u, v, R)
            clump_sh |= c & ~cv.circle(u - 0.03, v + 0.06, R)
            clump_hi |= cv.ellipse(u - R * 0.25, v + R * 0.45, R * 0.42, R * 0.20, 15)
    cv.paint(clump_sh & keep, shade(H, 0.80), HAIR, opacity=0.85)
    cv.paint(clump_hi & keep & ~clump_sh, shade(H, 1.14), HAIR, opacity=0.75)
    # coil texture: tapered "c" loops of varied size, loosely packed
    dark = shade(H, 0.66)
    w = DETAIL_PX / fr.rx
    sp = 0.15
    marks = cv.empty()
    for j, gv in enumerate(np.arange(-0.2, fr.top + 0.6, sp * 0.87)):
        for gu in np.arange(-1.6, 1.6, sp):
            u = gu + (sp / 2 if j % 2 else 0) + rng.uniform(-0.045, 0.045)
            v = gv + rng.uniform(-0.04, 0.04)
            if rng.uniform() < 0.22:
                continue
            x, y = fr.to_pose(u, v)
            xi, yi = int(x * cv.ss), int(y * cv.ss)
            if not (0 <= xi < cv.W and 0 <= yi < cv.H) or not inner[yi, xi]:
                continue
            rr = rng.uniform(0.034, 0.068)
            a0 = rng.uniform(140, 280)
            arc = [(u + rr * math.cos(math.radians(a)), v + rr * math.sin(math.radians(a)))
                   for a in np.linspace(a0, a0 + rng.uniform(190, 250), 18)]
            marks |= cv.stroke(arc, 0, 0, taper=lambda t: w * (0.25 + 0.95 * math.sin(math.pi * t)))
    cv.paint(marks & keep, dark, HAIR)
    return cv.result()


# ----------------------------------------------------------------------- Tuft

def tuft(ctx):
    fr = ctx["frame"]; ref = ctx["ref"]
    cv = Canvas(fr, ctx["shape"])
    H = ref["hair"]
    T = fr.top
    w_line = LINE_PX / fr.rx
    # the signature swirl: rises from the crown and curls over to the right
    def make(sc, dv):
        t = T + dv
        sw = (bezier((-0.18, t - 0.10), (-0.20, t + 0.18 * sc), (0.10, t + 0.42 * sc), (0.34, t + 0.30 * sc), n=40)
              + bezier((0.34, t + 0.30 * sc), (0.52, t + 0.20 * sc), (0.40, t + 0.02 * sc), (0.26, t + 0.10 * sc), n=24)
              + bezier((0.26, t + 0.10 * sc), (0.18, t + 0.16 * sc), (0.22, t + 0.24 * sc), (0.29, t + 0.22 * sc), n=10))
        return sw

    def width(t):
        return 0.20 * (1 - t) ** 0.9 + 0.012

    # shrink, then tuck lower on the crown, until hats can always cover it
    ks = safe_scale(fr, ctx["shape"]) - 0.08
    r = fr.skull_radius(cv.U, cv.V)
    sc, dv = 1.0, 0.0
    while True:
        swirl = make(sc, dv)
        rim = cv.stroke(swirl, 0, 0, taper=lambda t: width(t) + 2 * w_line)
        if not (cv.touches_border(rim, 3) or (rim & (r > ks)).any()):
            break
        if dv > -0.16:
            dv -= 0.02
        elif sc > 0.62:
            sc -= 0.05
        elif dv > -0.34:
            dv -= 0.03
        else:
            break
    # a single tapered curl: the outline is an inner band of the lock itself, so
    # the tip narrows to a point instead of ending in a round blob
    def lock(t):
        return width(t) + 1.2 * w_line * (1 - t) ** 0.5

    n = len(swirl)
    cut = int(n * 0.72)
    t_cut = cut / (n - 1)
    main = cv.stroke(swirl[:cut + 1], 0, 0, taper=lambda t: lock(t * t_cut))
    outlined(cv, main, ref, H)
    body = main & ~cv.outline_band(main, LINE_PX)
    # the curled-over tip is drawn on top with its own outline, so the curl reads
    tip = cv.stroke(swirl[cut - 4:], 0, 0, taper=lambda t: lock(t_cut + t * (1 - t_cut)))
    outlined(cv, tip, ref, H)
    tip_in = tip & ~cv.outline_band(tip, LINE_PX)
    body = (body & ~tip) | tip_in
    # cel shade on the underside of the swirl and a highlight on top
    sh = cv.stroke(swirl[8:60], 0, 0, taper=lambda t: 0.06 * (1 - t) + 0.01)
    sh = cv.ellipse(0.0, 0.0, 9, 9) & sh
    # offset the shade toward the curl's inner side
    shade_mask = body & ~cv.stroke([(u - 0.035, v + 0.035) for u, v in swirl], 0, 0, taper=width)
    cv.paint(shade_mask, shade(H, 0.78), HAIR)
    hi = cv.stroke(bezier((-0.12, T + dv + 0.06), (-0.10, T + dv + 0.20 * sc), (0.06, T + dv + 0.30 * sc), n=16), 0, 0,
                   taper=lambda t: 0.045 * math.sin(math.pi * t) + 0.004) & body
    cv.paint(hi, shade(H, 1.17), HAIR)
    # a light fuzz line along the crown so the bare head reads as "baby hair"
    return cv.result()


STYLE_FUNCS = {"Smooth": smooth, "Coils": coils, "Tuft": tuft}


def draw(style, ctx):
    return STYLE_FUNCS[style](ctx)
