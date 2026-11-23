"""The four cat poses.  Each pose function draws on a generous canvas; build.py
crops every pose to its silhouette (+ margin) afterwards.

Sitting (awake): small teardrop haunches tucked behind and under a pear-shaped
chest, hind feet pointing forward beside the front paws, two separate forelegs,
and a fluffy chest ruff that overlaps the tops of the legs so the head flows
into the body.  Rest: a loaf with a closed thigh, a hind paw at the front of the
thigh, forearms tucked under the chest with the paws peeking out under the chin,
and a low cranium (the closed eyes sit mid-head).
"""

from __future__ import annotations

import numpy as np

from cat import (CONTACT, GROWN_HEAD, OUTLINE, YOUNG_HEAD, Ctx, Frame, L, arclength_coords, blob, chain_points,
                 collar_band, head, head_outline, leg_bars, leg_sock, outline, outline_over, part, rest_head, set_tier,
                 smoothstep, stroke_marks, tail_marks, taper_tube, toes, tube)
from raster import Canvas, catmull, ellipse, poly, polyline, tapered

CANVAS = {"YoungAwake": (560, 600), "GrownAwake": (580, 670), "YoungRest": (660, 470), "GrownRest": (720, 490)}


def _head_mask(cv, fr, g):
    return cv.fill_mask(fr.wrap(poly([tuple(q) for q in head_outline(g)])))


def _mirror(pts, cx, base_y, sd):
    return [(cx + sd * x, base_y + y) for x, y in pts]


def axis_sock(cv, p0, p1, t_edge, hw, curve=0.10, soft=0.16, lo=0.5, full=0.97):
    """Siamese/Tuxedo sock along a limb axis from the paw tip ``p0`` to ``p1``: full
    weight up to ``t_edge`` (0..1 along the axis), a ``lo`` cel step for ``soft``
    beyond it; the edge curves round the limb (lateral distance bends it)."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d = p1 - p0
    ln = np.linalg.norm(d)
    u = d / ln
    t = ((cv.X - p0[0]) * u[0] + (cv.Y - p0[1]) * u[1]) / ln
    lat = ((cv.X - p0[0]) * -u[1] + (cv.Y - p0[1]) * u[0]) / max(hw, 1.0)
    tt = t + curve * np.clip(lat, -1.3, 1.3) ** 2
    e = 0.8 / ln
    step_full = 1 - smoothstep(t_edge - e, t_edge + e, tt)
    step_lo = 1 - smoothstep(t_edge + soft - e, t_edge + soft + e, tt)
    return full * step_full + lo * np.clip(step_lo - step_full, 0, 1)


# ---------------------------------------------------------------- sitting (awake)
def chest_tufts(cv, fill, cx, y, scale=1.0):
    """Two small fur flicks inside the ruff, at detail weight."""
    for dx, dy, ln in ((-16, 0, 0.9), (14, 4, 0.8)):
        x0, y0 = cx + dx * scale, y + dy * scale
        Ln = 15 * ln * scale
        pts = chain_points([((x0 - 6 * scale, y0 - Ln * 0.55), (x0 - 3 * scale, y0 - Ln * 0.1),
                             (x0 + 1 * scale, y0 + Ln * 0.2), (x0 + 4 * scale, y0 + Ln * 0.45))], n=20)
        cv.paint(cv.fill_mask(tapered_(pts, L.dw * 0.8)) * fill, OUTLINE)


def tapered_(pts, w):
    return tapered(pts, w, taper=(0.15, 0.25))


def _haunch(pts, cx, floor, sd, scale, dx):
    return [(cx + sd * (x * scale + dx), floor + y * scale) for x, y in pts]


def sitting(c: Ctx, grown: bool):
    """Young: a front-on kitten, two separate forelegs, tail curled round the feet.
    Grown: a slight 3/4 turn (near haunch bigger, far haunch tucked in, chest
    bulging forward on the far side, head tilted), the forelegs drawn as ONE pair of
    contours with a short split between the paws, and a tapered tail that comes round
    from behind the haunch at floor level and rises into a gentle curl."""
    cv = c.cv
    br = c.breed if c.marks_on else None
    W = cv.W
    g = GROWN_HEAD if grown else YOUNG_HEAD
    if grown:
        cx, floor = W / 2 - 44, 612
        hx, hy, hrot = cx - 8, 226, -0.06
        torso = dict(top_w=70, mid_w=110, wide_w=120, bot_w=100)
        front, neck_dx = 1.07, -10           # chest bulge on the front (far, sd=-1) side; neck leans forward
        haunch = [(88, -164), (128, -146), (158, -102), (168, -52), (158, -14), (128, 0), (88, 0), (74, -60), (72, -122)]
        hscale = {1: (1.08, 8), -1: (0.84, 22)}       # near haunch bigger, far one tucked in
        hfoot = {1: (140, -16, 33, 18), -1: (104, -14, 28, 16)}
        leg = dict(xc=cx - 10, half=29, w=60, paw=(34, 23))
        sock_top = 118
    else:
        cx, floor = W / 2 - 10, 532
        hx, hy, hrot = cx, 222, 0.0
        torso = dict(top_w=64, mid_w=86, wide_w=104, bot_w=92)
        front, neck_dx = 1.0, 0
        haunch = [(78, -130), (112, -116), (140, -84), (150, -44), (140, -12), (114, 0), (78, 0), (64, -40), (62, -92)]
        hscale = {1: (1.0, 0), -1: (1.0, 0)}
        hfoot = {1: (120, -15, 30, 17), -1: (120, -15, 30, 17)}
        leg = dict(top_dx=27, dx=42, w=58, paw=(34, 23))
        sock_top = 96
    chin = hy + g.ry_bot
    fr = Frame(hx, hy, 1.0, hrot)
    hmask = _head_mask(cv, fr, g)
    contact = cv.blur(cv.shift(hmask, 2, 12), 4) * (1 - hmask)
    shade_contact = [(contact, CONTACT)]
    tt, tm_, tw_, tb = torso["top_w"], torso["mid_w"], torso["wide_w"], torso["bot_w"]
    ymid = chin + (floor - chin) * 0.42

    # Tuxedo bib: one shape shared by the torso and the forelegs drawn over it
    bib = None
    if br == "Tuxedo":
        bx_ = cx + (neck_dx * 0.6 if grown else 0)
        bib = cv.fill_mask(catmull([(bx_ - tt * 0.95, chin - 60), (bx_ + tt * 0.95, chin - 60), (bx_ + tt * 0.85, chin + 20),
                                    (bx_ + 30, ymid + 30), (bx_ + 2, floor - 40), (bx_ - 28, ymid + 30),
                                    (bx_ - tt * 0.85, chin + 20)], tension=0.5))

    # ---- Grown: a tapered tail from behind the near haunch at floor level, rising
    # into a gentle curl (drawn first: the haunch covers its base)
    if grown:
        tail_pts = chain_points([((cx + 110, floor - 36), (cx + 180, floor - 10), (cx + 246, floor - 14),
                                  (cx + 262, floor - 80)),
                                 ((cx + 262, floor - 80), (cx + 276, floor - 140), (cx + 262, floor - 196),
                                  (cx + 226, floor - 196))])
        taper_tube(c, tail_pts, 50, 32, light=(5, 7), marks=tail_marks(c, tail_pts, 50))

    # ---- haunches: teardrops tucked behind and under the chest
    haunch_fill = {}
    for sd in (-1, 1):
        hs, hdx = hscale[sd]
        hp = catmull(_haunch(haunch, cx, floor, sd, hs, hdx), tension=0.5)
        hm = []
        if br == "Tabby":
            # concentric arcs that follow the haunch curve
            hcx, hcy = cx + sd * ((haunch[3][0] - 52) * hs + hdx), floor - 30 * hs
            arcs = []
            for k, (rx_, ry_) in enumerate(((34, 58), (58, 94))):
                a = np.linspace(-1.50, -0.05, 30)
                ptsa = np.stack([hcx + sd * np.cos(a) * rx_ * hs, hcy + np.sin(a) * ry_ * hs], 1)
                arcs.append((ptsa, (11.0, 8.5)[k]))
            hm.append((stroke_marks(cv, arcs, taper=(0.15, 0.45)), "g"))
        elif br == "Calico":
            if sd > 0:
                hm.append((cv.fill_mask(blob(cx + sd * (128 * hs + hdx), floor - 70, 46, seed=23)), "g"))
            else:
                hm.append((cv.fill_mask(blob(cx + sd * (132 * hs + hdx), floor - 84, 36, seed=24)), "dark"))
        f, _ = part(c, hp, light=(6, 8), shade=shade_contact, marks=hm)
        haunch_fill[sd] = f
    haunches = np.maximum(haunch_fill[-1], haunch_fill[1])

    # ---- torso: one soft pear shape widening into the haunches.  Its sides are not
    # outlined where they lie over the haunches (no chest-edge lines inside the body)
    nx = cx + neck_dx
    torso_path = catmull([(nx - tt * front, chin - 40), (nx, chin - 52), (nx + tt, chin - 40),
                          (cx + tm_ * (2 - front), ymid), (cx + tw_, floor - 46),
                          (cx + tb, floor - 2), (cx, floor + 2), (cx - tb, floor - 2), (cx - tw_ * front, floor - 46),
                          (cx - tm_ * front, ymid)])
    tfill_raw = cv.fill_mask(torso_path)
    tline = outline(cv, tfill_raw) * (1 - np.clip(cv.blur(haunches, 3.0) * 1.6, 0, 1))
    tm, shared = [], []        # shared: marks that continue across the forelegs drawn over the torso
    if br == "Tabby":
        strokes = []
        for sd in (-1, 1):
            for k, f in enumerate((0.30, 0.58)):
                y = chin + (floor - chin) * f + (6 if sd > 0 else 0)
                xe = tm_ + (tw_ - tm_) * f
                strokes.append(([(cx + sd * (xe + 8), y - 12), (cx + sd * (xe * 0.86), y + 2),
                                 (cx + sd * (xe * 0.70), y + 18)], (12.0, 9.0)[k]))
        tm.append((stroke_marks(cv, strokes), "g"))
    elif br == "Tuxedo":
        tm.append((bib, "g"))
    elif br == "Calico":
        shared.append((cv.fill_mask(blob(cx - tw_ * 0.85, ymid + 20, 40, seed=21)), "dark"))
        shared.append((cv.fill_mask(blob(cx + tm_ * 0.9, ymid - 26, 40, seed=22)), "g"))
    torso_fill, _ = part(c, fill=tfill_raw, line=tline, light=(7, 9),
                         shade=shade_contact, marks=tm + shared,
                         details=lambda fill, F: chest_tufts(cv, fill, nx, chin + 34, 1.0))

    # ---- hind feet pointing forward beside the front paws
    for sd in (-1, 1):
        fdx, fdy, frx, fry = hfoot[sd]
        fx, fy = cx + sd * fdx, floor + fdy
        fm = []
        if br in ("Tuxedo", "Siamese"):
            fm.append((np.ones_like(cv.X), "g"))
        part(c, ellipse(fx, fy, frx, fry, sd * 0.06), light=(4, 6), marks=fm,
             details=lambda fill, F, x=fx, y=fy + fry - 2: toes(cv, fill, x, y, 8, 9))

    # ---- forelegs, drawn over the chest; their contours are interior-weight lines
    # that start partway down the chest fur and become the outline at the paws
    prx, pry = leg["paw"]
    py = floor - pry + 1
    if grown:
        # one merged pair of legs: a single contour pair and a short split between the paws
        xs = {sd: leg["xc"] + sd * leg["half"] for sd in (-1, 1)}
        tops = {sd: leg["xc"] + sd * (leg["half"] - 8) for sd in (-1, 1)}
    else:
        xs = {sd: cx + sd * leg["dx"] for sd in (-1, 1)}
        tops = {sd: cx + sd * leg["top_dx"] for sd in (-1, 1)}
    leg_fill = {}
    for sd in (1, -1):
        shaft = cv.stroke_mask(polyline([(tops[sd], chin - 20), (xs[sd], py - 6)]), leg["w"])
        paw = cv.fill_mask(ellipse(xs[sd], py, prx, pry))
        leg_fill[sd] = np.maximum(shaft, paw)

    def leg_marks(sd, fill_sd):
        x_paw = xs[sd]
        lm = []
        if br == "Tabby":
            # two wrapped bars of uneven width and spacing; none right above the paw
            fs = ((0.40, 0.62) if sd > 0 else (0.46, 0.68)) if grown else ((0.40, 0.64) if sd > 0 else (0.44, 0.66))
            ys = [floor - (floor - chin) * f for f in fs]
            lm.append((leg_bars(cv, (x_paw - leg["w"] / 2, x_paw + leg["w"] / 2), ys, ws=[10.5, 8.0],
                                seed=(1 if sd > 0 else 0)) * fill_sd, "g"))
        elif br == "Tuxedo":
            sock = leg_sock(cv, x_paw - prx, x_paw + prx, floor - (64 if grown else 52), floor, curve=7, soft=0.1,
                            lo=1.0, full=1.0)
            lm.append((np.maximum(sock * fill_sd, bib), "g"))
        elif br == "Siamese":
            # one crisp point per leg following the leg contour (no lighter cuff band)
            top = floor - sock_top - (10 if sd > 0 else 0)
            lm.append((leg_sock(cv, x_paw - leg["w"] / 2, x_paw + leg["w"] / 2, top, floor, curve=12, soft=0.1,
                                lo=1.0, full=1.0) * fill_sd, "g"))
        elif br == "Calico" and sd < 0:
            lm.append((cv.fill_mask(blob(x_paw - 8, floor - 90, 30, seed=25)), "g"))
        return lm

    legs = {}
    if grown:
        fill = np.maximum(leg_fill[-1], leg_fill[1])
        line = outline_over(cv, fill, tfill_raw * (cv.Y < py - 4), wmul=smoothstep(ymid + 10, ymid + 80, cv.Y))
        lm = leg_marks(-1, leg_fill[-1]) + leg_marks(1, leg_fill[1])
        xc = leg["xc"]

        def leg_details(fill, F):
            for sd in (-1, 1):
                toes(cv, fill, xs[sd], floor + 1, 10, 11)
            # the split between the two front paws, fading out up the legs
            sp = np.stack([np.full(24, xc + 0.5), np.linspace(floor - 2, floor - 74, 24)], 1)
            cv.paint(cv.fill_mask(tapered(sp, L.dw * 1.05, profile=lambda t: np.clip(1.15 - t, 0.15, 1.0)))
                     * fill, OUTLINE)
        f, _ = part(c, fill=fill, line=line, light=None, shade=shade_contact, marks=lm + shared,
                    details=leg_details)
        legs = {-1: f, 1: f}
    else:
        for sd in (1, -1):
            fill = leg_fill[sd]
            line = outline_over(cv, fill, tfill_raw * (cv.Y < py - 4), wmul=smoothstep(ymid - 30, ymid + 40, cv.Y))
            f, _ = part(c, fill=fill, line=line, light=None, shade=shade_contact, marks=leg_marks(sd, fill) + shared,
                        details=lambda fill, F, x=xs[sd], y=floor + 1: toes(cv, fill, x, y, 10, 11))
            legs[sd] = f

    # ---- Young: tail out from behind the right haunch, curling round in front of the feet
    if not grown:
        tail_pts = chain_points([((cx + 124, floor - 64), (cx + 200, floor - 80), (cx + 232, floor - 40), (cx + 210, floor - 14)),
                                 ((cx + 210, floor - 14), (cx + 190, floor + 6), (cx + 120, floor + 2), (cx + 66, floor - 6))])
        tw = 42
        treg = cv.stroke_mask(polyline(tail_pts), tw + L.ow + 2)
        S, _, _ = arclength_coords(cv, tail_pts, treg)
        hcov = np.maximum(haunch_fill[1], outline(cv, haunch_fill[1]))
        tube(c, tail_pts, tw, light=(5, 7), marks=tail_marks(c, tail_pts, tw), occl=hcov * (S < 110))

    body = np.maximum.reduce([torso_fill, legs[-1], legs[1]])
    # ---- collar (accessory overlay): a band that sags well below the jaw so a full
    # band shows on the chest, bell at the front
    bw = 74
    ccx = nx
    collar_band(c, chain_points([((ccx - bw - 8, chin - 34), (ccx - 46, chin + 58), (ccx + 46, chin + 58),
                                  (ccx + bw + 8, chin - 34))]),
                width=22, bell_at=(ccx + 3, chin + 44), bell_r=13,
                occl=1 - np.maximum.reduce([body, hmask]))

    head(c, fr, g, eyes="open", under=body)


# ---------------------------------------------------------------- loaf / curled (rest)
def resting(c: Ctx, grown: bool):
    """Grown: a long loaf with a closed thigh, forearms tucked under the chin (paws
    ending inside the head's outline) and the tail wrapping the front half.
    Young: a rounder, taller curled ball, the head tucked low into the body, only
    the paw tips peeking out under the chin, and the tail wrapped all the way round
    the front."""
    cv = c.cv
    br = c.breed if c.marks_on else None
    g = rest_head(GROWN_HEAD if grown else YOUNG_HEAD, young=not grown)
    if grown:
        floor = 452
        body_c, body_r = (376, floor - 112), (204, 112)
        hc, hrot = (192, floor - 170), -0.08
        paws = [(168, floor - 21), (262, floor - 18)]
        paw_r, arm_w, arm_back = (36, 22), 44, 44
        rise, sq = 12, 2.6
        thigh = (0.42, 66, 90, 66)
    else:
        floor = 430
        body_c, body_r = (352, floor - 140), (178, 140)
        hc, hrot = (214, floor - 166), -0.10
        paws = [(186, floor - 16), (268, floor - 14)]
        paw_r, arm_w, arm_back = (30, 19), 38, 26
        rise, sq = 10, 2.2
        thigh = (0.36, 72, 80, 70)
    fr = Frame(hc[0], hc[1], 1.0, hrot)
    hmask = _head_mask(cv, fr, g)
    contact = cv.blur(cv.shift(hmask, 6, 8), 5) * (1 - hmask)
    shade_contact = [(contact, CONTACT)]

    bx, by = body_c
    brx, bry = body_r
    # loaf: superellipse-ish with a flat bottom and a gentle rise toward the haunch,
    # plus a soft scruff that fills the angle between the back of the head and the back
    th = np.linspace(-np.pi, np.pi, 400, endpoint=False)
    ct, st = np.cos(th), np.sin(th)
    e = 2.0 / sq
    px = bx + brx * np.sign(ct) * np.abs(ct) ** e
    py = by + bry * np.sign(st) * np.abs(st) ** e
    py = np.minimum(py, floor)
    py = py - rise * np.exp(-((px - (bx + brx * 0.45)) / (brx * 0.4)) ** 2) * (st < 0)
    loaf = cv.fill_mask(poly([tuple(q) for q in np.stack([px, py], 1)]))
    nape = fr.pt(g.rx * 0.80, -g.ry * 0.40)
    scruff = cv.fill_mask(ellipse(nape[0] + 34, nape[1] + 26, 74, 40, 0.12))
    bfill_raw = np.maximum(loaf, scruff)
    bm = []
    if br == "Tabby":
        strokes = []
        for k, xx in enumerate(np.linspace(bx - brx * 0.22, bx + brx * 0.56, 5)):
            top = by - bry * 0.98 - rise * np.exp(-((xx - (bx + brx * 0.45)) / (brx * 0.4)) ** 2)
            ln = (0.56, 0.42, 0.52, 0.40, 0.48)[k]
            strokes.append(([(xx - 6, top - 6), (xx + 10, top + bry * 0.26), (xx + 4, top + bry * ln)], 16 - k * 0.6))
        bm.append((stroke_marks(cv, strokes, taper=(0.35, 0.12)), "g"))
    elif br == "Tuxedo":
        bm.append((cv.fill_mask(ellipse(hc[0] + 60, floor - 24, 130, 62)), "g"))
    elif br == "Calico":
        bm.append((cv.fill_mask(blob(bx + brx * 0.02, by - bry * 0.78, brx * 0.40, seed=31, squash=0.7)), "g"))
        bm.append((cv.fill_mask(blob(bx + brx * 0.70, by - bry * 0.40, brx * 0.28, seed=32, squash=0.9)), "dark"))
    bfill, _ = part(c, fill=bfill_raw, light=(10, 12), shade=shade_contact, marks=bm)
    body_cover = np.maximum(bfill, outline(cv, bfill_raw))

    # thigh: a closed rounded shape at the rear of the loaf (interior line weight)
    tx, ty = bx + brx * thigh[0], floor - thigh[1]
    trx, try_ = thigh[2], thigh[3]
    tpath = ellipse(tx, ty, trx, try_, -0.10)
    tfill = cv.fill_mask(tpath) * bfill
    tline = outline(cv, cv.fill_mask(tpath), ow=L.dw * 1.15, lo=0.8, hi=1.25) * bfill
    hm = []
    if br == "Tabby":
        arcs = []
        for k, rr in enumerate((0.48, 0.80)):
            a = np.linspace(-2.7, -0.7, 34)
            arcs.append((np.stack([tx + np.cos(a) * trx * rr, ty + np.sin(a) * try_ * rr], 1), 12.5 - k))
        hm.append((stroke_marks(cv, arcs, taper=(0.15, 0.15)), "g"))
    elif br == "Calico":
        hm.append((cv.fill_mask(blob(bx + brx * 0.70, by - bry * 0.40, brx * 0.28, seed=32, squash=0.9)), "dark"))
    part(c, fill=tfill, line=tline, light=(6, 8), marks=hm)

    # hind paw at the front-bottom of the thigh, pointing forward, just above the tail
    fx, fy = tx - trx * 0.92, floor - (44 if not grown else 46)
    fpath = ellipse(fx, fy, 38, 17, -0.04)
    fm = []
    if br in ("Tuxedo", "Siamese"):
        fm.append((np.full_like(cv.X, 1.0 if br == "Tuxedo" else 0.97), "g"))

    def hind_toes(fill, F):
        for dy in (-6, 4):
            p_ = chain_points([((fx - 38 + 9, fy + dy - 3), (fx - 38 + 15, fy + dy - 1), (fx - 38 + 15, fy + dy + 1),
                                (fx - 38 + 20, fy + dy + 2))], n=10)
            cv.paint(cv.fill_mask(tapered_(p_, L.dw * 0.72)) * fill, OUTLINE)
    part(c, fpath, light=(4, 5), marks=fm, details=hind_toes)

    # tail along the front
    if grown:
        tail_pts = chain_points([((bx + brx * 0.90, by + 20), (bx + brx * 1.06, by + 76), (bx + brx * 0.92, floor - 2), (bx + brx * 0.50, floor - 8)),
                                 ((bx + brx * 0.50, floor - 8), (bx + brx * 0.05, floor - 12), (hc[0] + 150, floor - 2), (hc[0] + 96, floor - 16)),
                                 ((hc[0] + 96, floor - 16), (hc[0] + 84, floor - 20), (hc[0] + 74, floor - 26), (hc[0] + 66, floor - 34))])
        tw = 46
    else:
        # all the way round the front, under the paws, the tip curling up past them
        tail_pts = chain_points([((bx + brx * 0.86, by + 10), (bx + brx * 1.10, by + 80), (bx + brx * 0.98, floor - 2), (bx + brx * 0.50, floor - 4)),
                                 ((bx + brx * 0.50, floor - 4), (bx + brx * 0.05, floor - 6), (hc[0] + 40, floor - 2), (hc[0] - 50, floor - 10)),
                                 ((hc[0] - 50, floor - 10), (hc[0] - 82, floor - 14), (hc[0] - 98, floor - 30), (hc[0] - 92, floor - 50))])
        tw = 40
    treg = cv.stroke_mask(polyline(tail_pts), tw + L.ow + 2)
    S, _, _ = arclength_coords(cv, tail_pts, treg)

    def draw_tail():
        if grown:
            tube(c, tail_pts, tw, light=(5, 7), marks=tail_marks(c, tail_pts, tw), occl=body_cover * (S < 70))
        else:
            taper_tube(c, tail_pts, tw, tw * 0.80, light=(5, 7), marks=tail_marks(c, tail_pts, tw),
                       occl=body_cover * (S < 70))

    if not grown:
        draw_tail()

    # forearms tucked under the chest: short, the paws end under the chin inside the
    # head's outline (Young: only the paw tips peek out), drawn before the head
    arm_fill = np.zeros_like(cv.X)
    prx, pry = paw_r
    for k, (px_, py_) in enumerate(paws):
        back = (px_ + arm_back, floor - 46)
        shaft = cv.stroke_mask(polyline([back, (px_ + 8, py_ - 2)]), arm_w)
        paw = cv.fill_mask(ellipse(px_, py_, prx, pry, 0.03 * (1 if k else -1)))
        fill = np.maximum(shaft, paw)
        pm = []
        if br == "Tuxedo":
            pm.append((np.ones_like(cv.X), "g"))
        elif br == "Siamese":
            pm.append((axis_sock(cv, (px_ - prx, py_), back, 0.55 + 0.05 * k, arm_w / 2, curve=0.06, soft=0.0,
                                 lo=1.0, full=1.0), "g"))
        elif br == "Tabby" and grown:
            bars = stroke_marks(cv, [([(px_ + 22, py_ - 20), (px_ + 30, py_ - 4), (px_ + 26, py_ + 12)], 10),
                                     ([(px_ + 46, py_ - 26), (px_ + 52, py_ - 12), (px_ + 49, py_ + 2)], 7.5)])
            pm.append((bars, "g"))
        f, _ = part(c, fill=fill, light=(4, 6), marks=pm,
                    details=lambda fill, F, x=px_, y=py_: toes(cv, fill, x - prx * 0.55, y + pry - 2, 6, 8))
        arm_fill = np.maximum(arm_fill, f)

    if grown:
        draw_tail()

    # collar: a short arc sagging just below the jaw (the head covers the rest), bell in front
    chin = fr.pt(4, g.ry_bot)
    cw = g.rx * 0.46
    collar_band(c, chain_points([((chin[0] - cw, chin[1] - 34), (chin[0] - cw * 0.5, chin[1] + 34),
                                  (chin[0] + cw * 0.5, chin[1] + 34), (chin[0] + cw, chin[1] - 34))]),
                width=20, occl=1 - np.maximum(body_cover, arm_fill))

    head(c, fr, g, eyes="closed", under=np.maximum(bfill, arm_fill))
    collar_band(c, None, bell_at=(chin[0] + 2, chin[1] + 15), bell_r=12, occl=1 - np.maximum(body_cover, arm_fill))


POSE_FUNCS = {
    "YoungAwake": lambda c: sitting(c, grown=False),
    "GrownAwake": lambda c: sitting(c, grown=True),
    "YoungRest": lambda c: resting(c, grown=False),
    "GrownRest": lambda c: resting(c, grown=True),
}


def render(pose: str, breed: str | None = None, capture: str | None = None) -> Canvas:
    W, H = CANVAS[pose]
    set_tier("Grown" if pose.startswith("Grown") else "Young")
    cv = Canvas(W, H, capture=capture)
    c = Ctx(cv=cv, breed=breed, marks_on=(capture in ("marking", "darkpatch") and breed is not None),
            collar_on=(capture == "collar"))
    POSE_FUNCS[pose](c)
    return cv
