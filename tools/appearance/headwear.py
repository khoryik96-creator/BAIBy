"""Procedurally drawn headwear overlays: Bow, BearBeanie, BucketHat, DaisyClip, and the look-pack hats
PomBeanie (Cozy knits) and SunHat (Garden party).

Drawn in each pose's head-local frame. Hats are sized per pose so that they
cover every hair style above the brim (hair is composited below headwear);
hair below the brim peeks out naturally. Fabric uses the outfit-accent weight
(A) so headwear always matches the outfit; secondary fabric uses outfit-main
(B); fixed details (daisy petals, centre) use weight 0.
"""

from __future__ import annotations

import math

import numpy as np
from scipy import ndimage as ndi

from common import HeadFrame
from hairstyles import inside_image
from paint import ACCENT, FIXED, MAIN, Canvas, bezier, shade, spline

LINE_PX = 6.5
DETAIL_PX = 3.0
NB = 360


# ----------------------------------------------------------------- helpers

def hat_frame(ctx):
    """Head frame for hats, optionally nudged back on the head (per-pose ``hat_shift``)."""
    fr = ctx["frame"]
    du = ctx["head"].get("hat_shift", 0.0)
    if not du:
        return fr
    h = dict(fr.h)
    h["cx"] = fr.cx + du * fr.rx * fr.cos
    h["cy"] = fr.cy + du * fr.rx * fr.sin
    return HeadFrame(h)


def outline_fill(cv, sil, ref, col, region, line_px=LINE_PX):
    cv.paint(sil, col, region)
    cv.paint(cv.outline_band(sil, line_px), ref["outline"], FIXED)


class Sub:
    """Accessory frame inside head-local space: x along the tangent, y outward."""

    def __init__(self, origin, normal_deg, scale):
        self.o = origin
        a = math.radians(normal_deg)
        self.n = (math.cos(a), math.sin(a))
        self.t = (math.sin(a), -math.cos(a))
        self.s = scale

    def __call__(self, x, y):
        return (self.o[0] + self.s * (x * self.t[0] + y * self.n[0]),
                self.o[1] + self.s * (x * self.t[1] + y * self.n[1]))

    def pts(self, seq):
        return [self(x, y) for x, y in seq]


def skull_normal_deg(fr, theta):
    """Outward normal angle (degrees, head-local) of the skull at polar angle theta."""
    e = 1e-3
    u0, v0 = fr.skull_point(theta - e)
    u1, v1 = fr.skull_point(theta + e)
    tx, ty = u1 - u0, v1 - v0
    return math.degrees(math.atan2(-tx, ty))


def hair_profile(fr, hair_union, brim):
    """Per-angle max skull-radius of hair above the brim curve (head-local)."""
    h, w = hair_union.shape
    ys, xs = np.nonzero(hair_union)
    u, v = fr.to_local(xs + 0.5, ys + 0.5)
    above = v > brim(u)
    u, v = u[above], v[above]
    r = fr.skull_radius(u, v)
    vn = v / fr.top
    phi = np.degrees(np.arctan2(vn, u)) % 360
    prof = np.ones(NB) * 1.0
    np.maximum.at(prof, (phi * NB / 360).astype(int) % NB, r)
    return prof


def smooth_profile(prof, grow=0.035):
    p = ndi.maximum_filter1d(prof, 31, mode="wrap")
    p = ndi.gaussian_filter1d(p, 10, mode="wrap")
    p = np.maximum(p, ndi.gaussian_filter1d(prof, 4, mode="wrap"))
    p = ndi.gaussian_filter1d(p, 6, mode="wrap")
    return p + grow


def fit_dome(fr, hair_union, brim, shape, margin=0.035, min_k=1.08, edge_px=3):
    """Smallest scaled skull (kx, ky) covering all hair above ``brim`` that fits the image.

    Returns an angular profile usable with dome_mask/dome_point.
    """
    ys, xs = np.nonzero(hair_union)
    u, v = fr.to_local(xs + 0.5, ys + 0.5)
    # hair must be covered above the brim around the crown; a little hair may
    # puff out at the sides under a hat (that reads naturally)
    ang = np.degrees(np.arctan2(v / fr.top, u))
    sel = (v > brim(u)) & (ang > 32) & (ang < 148)
    u, v = u[sel][::3], v[sel][::3]
    H, W = shape

    def inside_image(kx, ky, edge_px=edge_px):
        for deg in range(0, 181, 3):
            uu, vv = fr.skull_point(math.radians(deg))
            if vv * ky < float(brim(uu * kx)) + 0.08:
                continue          # below the brim the dome is not drawn
            x, y = fr.to_pose(uu * kx, vv * ky)
            if not (edge_px <= x <= W - edge_px and edge_px <= y <= H - edge_px):
                return False
        return True

    best = None
    capped = []
    for kx in np.arange(min_k, 1.9, 0.01):
        r = fr.skull_radius(u / kx, v) if len(u) else np.zeros(1)
        # minimal ky so every pixel is inside: search
        lo, hi = min_k, 2.2
        for _ in range(24):
            mid = (lo + hi) / 2
            ok = (fr.skull_radius(u / kx, v / mid) < 1.0 - 0.004).all() if len(u) else True
            if ok:
                hi = mid
            else:
                lo = mid
        ky = hi
        cand = (kx + margin, ky + margin)
        covers = 0
        if not inside_image(*cand) and inside_image(*cand, edge_px=1):
            covers = 1        # a sliver of the crown may touch the image edge
        elif not inside_image(*cand):
            # cap ky by the image as a fallback only (it may leave hair uncovered)
            covers = 2
            ky2 = ky + margin
            while ky2 > min_k and not inside_image(cand[0], ky2):
                ky2 -= 0.01
            if not inside_image(cand[0], ky2):
                continue
            cand = (cand[0], ky2)
        area = cand[0] * cand[1] + 0.6 * abs(cand[0] - cand[1])
        if covers == 2:
            frac = float((fr.skull_radius(u / cand[0], v / cand[1]) < 1.0).mean()) if len(u) else 1.0
            capped.append((frac, area, cand))
            continue
        key = (covers, area)
        if best is None or key < best[0]:
            best = (key, cand)
    if best is None and capped:
        # cannot cover everything inside the image: stay compact, losing at most a
        # few percent of coverage against the most covering option
        top = max(c[0] for c in capped)
        ok = [c for c in capped if c[0] >= top - 0.04]
        best = (None, min(ok, key=lambda c: c[1])[2])
    kx, ky = best[1] if best else (1.15, 1.15)
    # angular profile (by geometric angle, as dome_mask looks it up)
    prof = np.zeros(NB)
    for i in range(NB):
        ph = math.radians((i + 0.5) * 360 / NB)
        c, sn = math.cos(ph), math.sin(ph) * (fr.top if math.sin(ph) >= 0 else fr.bot)
        s_ = 1.0 / fr.skull_radius(c / kx, sn / ky)
        prof[i] = s_ * fr.skull_radius(c, sn)
    tier = best[0][0] if best and best[0] is not None else 2
    return prof, kx, ky, tier


def fit_hat(fr, hair_union, brim, shape, **kw):
    """Fit the hat dome, also trying small slides/tilts of the hat on the head.

    Returns (frame, profile). A slide is only taken when it hides clearly more
    hair (or is the only way to stay inside the image), so most poses keep the
    centred hat.
    """
    ys, xs = np.nonzero(hair_union)
    hx, hy = xs + 0.5, ys + 0.5
    u0, v0 = fr.to_local(hx, hy)
    ang = np.degrees(np.arctan2(v0 / fr.top, u0))
    sel = (v0 > brim(u0)) & (ang > 32) & (ang < 148)
    hx, hy = hx[sel][::3], hy[sel][::3]
    results = []
    for du in (0.0, -0.06, 0.06, -0.12, 0.12):
        for dt in (0.0, -3.0, 3.0):      # keep the hat within +-5 deg of the head
            h = dict(fr.h)
            h["cx"] = fr.cx + du * fr.rx * fr.cos
            h["cy"] = fr.cy + du * fr.rx * fr.sin
            h["tilt"] = fr.h["tilt"] + dt
            f2 = HeadFrame(h)
            prof, kx, ky, tier = fit_dome(f2, hair_union, brim, shape, **kw)
            u, v = f2.to_local(hx, hy)
            frac = float((f2.skull_radius(u / kx, v / ky) < 1.0).mean()) if len(u) else 1.0
            cost = abs(du) * 0.4 + abs(dt) * 0.004   # prefer the centred hat
            results.append((tier, frac, kx * ky, cost, f2, prof))
    base = results[0]
    best = base
    for r in results[1:]:
        better_cover = r[1] > best[1] + 0.01 + r[3] * 0.1
        same_cover_smaller = abs(r[1] - best[1]) <= 0.01 and r[2] + r[3] < best[2] + best[3] - 0.02
        if r[0] < best[0] or (r[0] == best[0] and (better_cover or same_cover_smaller)):
            best = r
    return best[4], best[5]


def cover_crown(cv, fr, prof, hair_union, line, margin=0.03):
    """Let the dome bulge locally wherever hair still pokes out above ``line``
    around the crown (the fitted dome is a scaled skull and may miss a curl),
    then keep it inside the image."""
    need = smooth_profile(hair_profile(fr, hair_union, line), grow=margin)
    deg = (np.arange(NB) + 0.5) * 360 / NB
    w = np.clip(np.minimum(deg - 28, 152 - deg) / 14.0, 0, 1)
    grown = np.maximum(prof, prof * (1 - w) + np.maximum(prof, need) * w)
    return np.maximum(clamp_profile_to_image(cv, fr, grown, margin_px=2), prof)


def dome_mask(cv, fr, prof):
    vn = cv.V / fr.top
    phi = np.degrees(np.arctan2(vn, cv.U)) % 360
    lim = prof[(phi * NB / 360).astype(int) % NB]
    return fr.skull_radius(cv.U, cv.V) < lim


def dome_point(fr, prof, deg):
    """Head-local point on the dome edge at polar angle deg (0=right, 90=crown)."""
    ph = math.radians(deg)
    c, sn = math.cos(ph), math.sin(ph) * (fr.top if math.sin(ph) >= 0 else fr.bot)
    k = prof[int((deg % 360) * NB / 360) % NB]
    s_ = k / fr.skull_radius(c, sn)
    return c * s_, sn * s_


def fits(cv, mask, margin_px=2):
    """True if mask stays inside the pose image with a margin."""
    m = margin_px * cv.ss
    return not (mask[:m].any() or mask[-m:].any() or mask[:, :m].any() or mask[:, -m:].any())


def clamp_profile_to_image(cv, fr, prof, margin_px=3):
    """Shrink the dome where it would leave the image (it only covers hair, which is inside)."""
    H, W = cv.H / cv.ss, cv.W / cv.ss
    out = prof.copy()
    for i in range(NB):
        deg = (i + 0.5) * 360 / NB
        for _ in range(60):
            u, v = fr.skull_point(math.radians(deg), out[i])
            x, y = fr.to_pose(u, v)
            if margin_px <= x <= W - margin_px and margin_px <= y <= H - margin_px:
                break
            out[i] -= 0.01
    return np.minimum(prof, ndi.minimum_filter1d(out, 9, mode="wrap"))


# -------------------------------------------------------------- Bear beanie

def beanie_brim(u):
    return 0.74 - 0.30 * np.asarray(u) ** 2


BUCKET_VB = 0.88


def bucket_brim_top(u):
    return BUCKET_VB + 0.10 - 0.10 * np.asarray(u) ** 2


# hair above these curves (around the crown) must be hidden by the hat
COVER_LINES = {"BearBeanie": lambda u: beanie_brim(u) + 0.12,
               "BucketHat": lambda u: bucket_brim_top(u) - 0.02,
               "PomBeanie": lambda u: beanie_brim(u) + 0.12,
               "SunHat": lambda u: bucket_brim_top(u) - 0.02}

def bear_beanie(ctx):
    fr = hat_frame(ctx); ref = ctx["ref"]
    A = ref["accent"]; M = ref["main"]

    brim = beanie_brim
    fr, prof = fit_hat(fr, ctx["hair_union"], COVER_LINES["BearBeanie"], ctx["shape"])
    cv = Canvas(fr, ctx["shape"])
    prof = cover_crown(cv, fr, prof, ctx["hair_union"], COVER_LINES["BearBeanie"])
    dome = dome_mask(cv, fr, prof) & (cv.V > brim(cv.U) + 0.10)
    # folded rib band, a touch wider than the dome, with rounded ends
    big = prof + 0.035
    ub = 0.0
    for uu in np.linspace(0, 2.2, 221):
        vv = float(brim(uu)) + 0.125
        phi = math.degrees(math.atan2(vv / fr.top, uu)) % 360
        if fr.skull_radius(uu, vv) < big[int(phi * NB / 360) % NB]:
            ub = uu
    ub -= 0.09

    def make_band(ub):
        m = (cv.V > brim(cv.U)) & (cv.V < brim(cv.U) + 0.25) & (np.abs(cv.U) < ub)
        for s in (-1, 1):
            m |= cv.ellipse(s * ub, float(brim(ub)) + 0.125, 0.10, 0.125, -s * math.degrees(math.atan(0.6 * ub)))
        return m

    band = make_band(ub)
    while cv.touches_border(band, 3) and ub > 0.7:
        ub -= 0.03
        band = make_band(ub)
    # bear ears on the dome, slid down until they fit inside the image
    ears = []
    for side in (-1, 1):
        found = None
        for rad in (0.205, 0.18, 0.16):
            for deg in list(np.arange(44, 6, -2)) + list(np.arange(46, 72, 2)):
                d = 90 + side * deg
                u, v = dome_point(fr, prof, d)
                n = math.radians(90 + side * deg * 1.1)
                ou, ov = u + 0.01 * math.cos(n), v + 0.01 * math.sin(n)
                m = cv.circle(ou, ov, rad)
                if not cv.touches_border(m, 3):
                    found = (ou, ov, m, rad)
                    break
            if found:
                break
        if found:
            ears.append(found + (False,))
            continue
        # no room outside the image: tuck the ear onto the dome, drawn in front
        for inset in np.arange(0.06, 0.40, 0.02):
            for deg in (34, 26, 18):
                d = 90 + side * deg
                u, v = dome_point(fr, prof, d)
                n = math.radians(d)
                ou, ov = u - inset * math.cos(n), v - inset * math.sin(n)
                m = cv.circle(ou, ov, 0.17)
                if not cv.touches_border(m, 3):
                    found = (ou, ov, m, 0.17, True)
                    break
            if found:
                break
        if found:
            ears.append(found)
    def draw_ears(front):
        sil = cv.empty()
        for _, _, m, _, f in ears:
            if f == front:
                sil |= m
        if not sil.any():
            return
        outline_fill(cv, sil, ref, A, ACCENT)
        for ou, ov, _, rad, f in ears:
            if f != front:
                continue
            inner = cv.circle(ou, ov, rad * 0.54)
            if not front:
                inner &= ~dome
            cv.paint(inner, M, MAIN)
            cv.paint(cv.outline_band(inner, DETAIL_PX * 0.8), shade(A, 0.75), ACCENT)

    # ears normally sit behind the dome: draw them first, then the dome over them
    draw_ears(False)
    outline_fill(cv, dome, ref, A, ACCENT)
    d_in = dome & ~cv.outline_band(dome, LINE_PX)
    # cel shade on the far side + soft knit stripes on the dome
    sh = d_in & (fr.skull_radius(cv.U + 0.18, cv.V + 0.05) > prof.mean() - 0.02)
    cv.paint(sh, shade(A, 0.86), ACCENT)
    # matte knit: soft vertical ribs (a lit ridge beside each groove) that
    # gather towards the crown, no glossy highlight
    w = DETAIL_PX / fr.rx
    for k in np.linspace(-0.88, 0.88, 11):
        top = dome_point(fr, prof, 90 - k * 62)
        pts = bezier((k * 1.05, brim(k * 1.05) + 0.26), (k * 1.10, 0.95), (top[0] * 0.90, top[1] * 0.90), n=30)
        ridge = cv.stroke([(u - 0.035, v) for u, v in pts], 0, 0, taper=lambda t: w * 1.6 * (1.0 - 0.6 * t)) & d_in
        cv.paint(ridge, shade(A, 1.05), ACCENT, opacity=0.8)
        m = cv.stroke(pts, 0, 0, taper=lambda t: w * (1.0 - 0.55 * t)) & d_in
        cv.paint(m, shade(A, 0.80), ACCENT, opacity=0.9)
    # soft crown shading (matte, no specular)
    lit = d_in & (fr.skull_radius(cv.U + 0.25, cv.V - 0.05) < prof.mean() - 0.30)
    cv.paint(lit, shade(A, 1.04), ACCENT, opacity=0.6)
    # rib band
    outline_fill(cv, band, ref, shade(A, 0.95), ACCENT)
    b_in = band & ~cv.outline_band(band, LINE_PX)
    for k in np.arange(-1.6, 1.61, 0.115):
        x0 = k
        pts = [(x0, brim(x0) + 0.03), (x0 * 1.03, brim(x0) + 0.22)]
        m = cv.stroke(pts, w * 0.9, w * 0.9) & b_in
        cv.paint(m, shade(A, 0.78), ACCENT)
    under = b_in & (cv.V < brim(cv.U) + 0.07)
    cv.paint(under, shade(A, 0.82), ACCENT, opacity=0.6)
    draw_ears(True)
    return cv.result()


# --------------------------------------------------------------- Bucket hat

def bucket_hat(ctx):
    fr = hat_frame(ctx); ref = ctx["ref"]
    A = ref["accent"]; M = ref["main"]
    vb = BUCKET_VB
    brim_top = bucket_brim_top
    fr, prof = fit_hat(fr, ctx["hair_union"], COVER_LINES["BucketHat"], ctx["shape"], margin=0.04)
    cv = Canvas(fr, ctx["shape"])
    prof = cover_crown(cv, fr, prof, ctx["hair_union"], COVER_LINES["BucketHat"])
    crown = dome_mask(cv, fr, prof) & (cv.V > brim_top(cv.U) - 0.04)
    # brim width from the crown width at brim height
    half = 0.0
    for uu in np.linspace(0, 2.0, 201):
        k = fr.skull_radius(uu, vb)
        vn = vb / fr.top
        phi = math.degrees(math.atan2(vn, uu)) % 360
        if k < prof[int(phi * NB / 360) % NB]:
            half = uu
    def bottom(u, Wb):
        return vb - 0.20 - 0.26 * (np.asarray(u) / Wb) ** 2

    def make_brim(Wb):
        # a soft brim that slopes down and out from the crown base on both sides:
        # its top edge hugs the crown (so the band runs right into it), the
        # bottom edge is wider and droops, and the ends are rounded
        Wt = half + 0.015
        top = [(u, float(brim_top(u)) - 0.005) for u in np.linspace(-Wt, Wt, 80)]
        right = (bezier((Wt, float(brim_top(Wt))), (Wt + 0.10, float(brim_top(Wt)) - 0.02),
                        (Wb + 0.015, float(bottom(Wb, Wb)) + 0.14), (Wb, float(bottom(Wb, Wb)) + 0.06), n=16)
                 + bezier((Wb, float(bottom(Wb, Wb)) + 0.06), (Wb - 0.005, float(bottom(Wb, Wb)) - 0.005),
                          (Wb - 0.04, float(bottom(Wb - 0.04, Wb))), (Wb - 0.09, float(bottom(Wb - 0.09, Wb))), n=10))
        bot = [(u, float(bottom(u, Wb))) for u in np.linspace(Wb - 0.09, -(Wb - 0.09), 100)]
        left = [(-u, v) for u, v in right[::-1]]
        return cv.polygon(top + right + bot + left)

    Wb = half + 0.22
    brim = make_brim(Wb)
    while cv.touches_border(brim, 4) and Wb > half + 0.07:
        Wb -= 0.02
        brim = make_brim(Wb)
    # a crown sized for very wide curls can still push the brim end off the
    # image: end it in a rounded lobe just inside the border instead of a cut
    brim = inside_image(cv, brim, margin_px=4.0, round_px=9.0)
    # crown first, then brim in front
    outline_fill(cv, crown, ref, A, ACCENT)
    c_in = crown & ~cv.outline_band(crown, LINE_PX)
    sh = c_in & (fr.skull_radius(cv.U + 0.18, cv.V + 0.04) > prof.mean() - 0.01)
    cv.paint(sh, shade(A, 0.86), ACCENT)
    # ribbon band (outfit main) around the crown base, down into the brim
    ribbon = c_in & (cv.V < brim_top(cv.U) + 0.17)
    cv.paint(ribbon, M, MAIN)
    rl = cv.stroke([(u, brim_top(u) + 0.17) for u in np.linspace(-1.6, 1.6, 60)], DETAIL_PX / fr.rx, DETAIL_PX / fr.rx) & c_in
    cv.paint(rl, ref["outline"], FIXED)
    # top seam of the crown
    seam = cv.stroke([dome_point(fr, prof - 0.16, d) for d in np.linspace(25, 155, 50)], 0, 0,
                     taper=lambda t: DETAIL_PX / fr.rx * (0.3 + 0.8 * math.sin(math.pi * t))) & c_in
    cv.paint(seam, shade(A, 0.74), ACCENT)
    outline_fill(cv, brim, ref, A, ACCENT)
    b_in = brim & ~cv.outline_band(brim, LINE_PX)
    # soft shade on the lower, downturned part of the brim
    under = b_in & (cv.V < vb - 0.11 - 0.26 * np.clip(cv.U / Wb, -1, 1) ** 2)
    cv.paint(under, shade(A, 0.86), ACCENT)
    # stitch lines following the brim's slope
    w = DETAIL_PX / fr.rx
    for f in (0.35, 0.62):
        pts = [(u, float(brim_top(u)) * (1 - f) + float(bottom(u, Wb)) * f - 0.01)
               for u in np.linspace(-(half + 0.08 * f), half + 0.08 * f, 120)]
        dash = cv.empty()
        for i in range(0, len(pts) - 4, 7):
            dash |= cv.stroke(pts[i:i + 4], w * 0.75, w * 0.75)
        cv.paint(dash & b_in, shade(A, 0.72), ACCENT)
    return cv.result()


# ---------------------------------------------------------------------- Bow

def surface_scale(ctx, fr, theta):
    """Skull scale of the rebuilt Base head edge along the ray at polar angle theta."""
    a = ctx["base"][..., 3]
    H, W = a.shape
    last = 1.0
    for k in np.arange(0.70, 1.40, 0.005):
        x, y = fr.to_pose(*fr.skull_point(theta, k))
        xi, yi = int(x), int(y)
        if 0 <= xi < W and 0 <= yi < H and a[yi, xi] > 128:
            last = k
    return last


def bow(ctx):
    fr = ctx["frame"]; ref = ctx["ref"]
    cv = Canvas(fr, ctx["shape"])
    A = ref["accent"]
    # on the side of the crown; lying poses (head tilted towards the viewer's
    # right) wear it further round so it follows the head's tilt
    th = math.radians(124 if fr.h["tilt"] < 15 else 146)
    # knot sits on the scalp (just inside the rebuilt head edge), so the bow is
    # seated for every hair style, bald crowns included
    o = fr.skull_point(th, surface_scale(ctx, fr, th) - 0.05)
    S = Sub(o, skull_normal_deg(fr, th) - (8 if fr.h["tilt"] < 15 else 0), 1.35)
    loops = cv.empty()
    for s in (-1, 1):
        pts = S.pts(bezier((0, 0), (s * 0.12, 0.20), (s * 0.34, 0.20), (s * 0.31, 0.0), n=24)
                    + bezier((s * 0.31, 0.0), (s * 0.34, -0.20), (s * 0.12, -0.20), (0, 0), n=24))
        loops |= cv.polygon(pts)
    tails = cv.empty()
    for s in (-1, 1):
        pts = S.pts([(0, -0.02), (s * 0.10, -0.10), (s * 0.17, -0.25), (s * 0.10, -0.23),
                     (s * 0.06, -0.27), (s * 0.02, -0.10)])
        tails |= cv.polygon(pts)
    knot = cv.polygon(S.pts([(0.07 * math.cos(a), 0.085 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 40)]))
    outline_fill(cv, tails, ref, shade(A, 0.92), ACCENT)
    outline_fill(cv, loops, ref, A, ACCENT)
    l_in = loops & ~cv.outline_band(loops, LINE_PX)
    w = DETAIL_PX / fr.rx
    for s in (-1, 1):
        # inner fold shading near the knot and pleat lines
        fold = cv.polygon(S.pts(bezier((0, 0), (s * 0.08, 0.10), (s * 0.15, 0.08), (s * 0.13, 0.0), n=12)
                                + bezier((s * 0.13, 0.0), (s * 0.15, -0.08), (s * 0.08, -0.10), (0, 0), n=12)))
        cv.paint(fold & l_in, shade(A, 0.80), ACCENT)
        for dy in (0.07, -0.07):
            m = cv.stroke(S.pts(bezier((s * 0.10, dy * 0.4), (s * 0.18, dy * 1.4), (s * 0.27, dy * 1.6), n=12)), 0, 0,
                          taper=lambda t: w * (0.3 + 0.9 * math.sin(math.pi * t))) & l_in
            cv.paint(m, shade(A, 0.72), ACCENT)
        hi = cv.polygon(S.pts([(s * (0.22 + 0.04 * math.cos(a)), 0.10 + 0.025 * math.sin(a))
                               for a in np.linspace(0, 2 * math.pi, 20)])) & l_in
        cv.paint(hi, shade(A, 1.18), ACCENT)
    outline_fill(cv, knot, ref, shade(A, 0.95), ACCENT, LINE_PX * 0.85)
    return cv.result()


# --------------------------------------------------------------- Daisy clip

def daisy_clip(ctx):
    fr = ctx["frame"]; ref = ctx["ref"]
    cv = Canvas(fr, ctx["shape"])
    A = ref["accent"]
    th = math.radians(54)
    o = fr.skull_point(th, 1.0)
    S = Sub(o, skull_normal_deg(fr, th), 1.55)
    petal_col = np.array([252.0, 248.0, 238.0])
    centre_col = np.array([244.0, 192.0, 72.0])
    # little leaf (outfit accent) peeking out behind the flower
    leaf = cv.polygon(S.pts(bezier((0.02, -0.02), (0.14, 0.10), (0.30, 0.06), (0.32, -0.04), n=16)
                            + bezier((0.32, -0.04), (0.22, -0.10), (0.10, -0.10), (0.02, -0.02), n=16)))
    outline_fill(cv, leaf, ref, A, ACCENT, LINE_PX * 0.8)
    vein = cv.stroke(S.pts(bezier((0.06, -0.02), (0.16, 0.0), (0.27, -0.02), n=10)), 0, 0,
                     taper=lambda t: DETAIL_PX / fr.rx * (1 - t) * 0.9) & leaf
    cv.paint(vein, shade(A, 0.72), ACCENT)
    petals = cv.empty()
    n = 9
    rp = 0.112
    for k in range(n):
        a = 2 * math.pi * k / n + 0.2
        cx, cy = 0.105 * math.cos(a), 0.105 * math.sin(a)
        pts = S.pts([(cx + rp * 0.62 * math.cos(t) * math.cos(a) - 0.050 * math.sin(t) * math.sin(a),
                      cy + rp * 0.62 * math.cos(t) * math.sin(a) + 0.050 * math.sin(t) * math.cos(a))
                     for t in np.linspace(0, 2 * math.pi, 24)])
        petals |= cv.polygon(pts)
    outline_fill(cv, petals, ref, petal_col, FIXED, LINE_PX * 0.8)
    p_in = petals & ~cv.outline_band(petals, LINE_PX * 0.8)
    # petal separation lines + soft shade
    w = DETAIL_PX / fr.rx * 0.8
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n + 0.2
        m = cv.stroke(S.pts([(0.06 * math.cos(a), 0.06 * math.sin(a)), (0.15 * math.cos(a), 0.15 * math.sin(a))]),
                      w, w * 0.4) & p_in
        cv.paint(m, ref["outline"], FIXED, opacity=0.7)
    sh = p_in & cv.polygon(S.pts([(0.2 * math.cos(t), 0.2 * math.sin(t)) for t in np.linspace(math.pi * 1.1, math.pi * 1.9, 16)] + [(0, 0)]))
    cv.paint(sh, shade(petal_col, 0.90), FIXED)
    centre = cv.polygon(S.pts([(0.058 * math.cos(t), 0.058 * math.sin(t)) for t in np.linspace(0, 2 * math.pi, 30)]))
    outline_fill(cv, centre, ref, centre_col, FIXED, LINE_PX * 0.7)
    dot = cv.polygon(S.pts([(-0.015 + 0.018 * math.cos(t), 0.015 + 0.014 * math.sin(t)) for t in np.linspace(0, 2 * math.pi, 16)]))
    cv.paint(dot, np.array([252.0, 230.0, 150.0]), FIXED)
    return cv.result()


# ------------------------------------------------- Pom-pom beanie (Cozy knits)

def fuzzy_disc(cv, cu, cv_, r, lobes=11, amp=0.07, phase=0.3):
    """A pom-pom: a disc with a soft scalloped (yarn-tuft) edge."""
    pts = [(cu + r * (1 + amp * math.sin(lobes * t + phase)) * math.cos(t),
            cv_ + r * (1 + amp * math.sin(lobes * t + phase)) * math.sin(t))
           for t in np.linspace(0, 2 * math.pi, 120)]
    return cv.polygon(pts)


def pom_beanie(ctx):
    """A knit beanie with a fold-up rib band, a knit stripe in the outfit's main colour
    and a yarn pom-pom on top (main colour); the body is the bear beanie's."""
    fr = hat_frame(ctx); ref = ctx["ref"]
    A = ref["accent"]; M = ref["main"]

    brim = beanie_brim
    fr, prof = fit_hat(fr, ctx["hair_union"], COVER_LINES["PomBeanie"], ctx["shape"])
    cv = Canvas(fr, ctx["shape"])
    prof = cover_crown(cv, fr, prof, ctx["hair_union"], COVER_LINES["PomBeanie"])
    dome = dome_mask(cv, fr, prof) & (cv.V > brim(cv.U) + 0.10)
    big = prof + 0.035
    ub = 0.0
    for uu in np.linspace(0, 2.2, 221):
        vv = float(brim(uu)) + 0.125
        phi = math.degrees(math.atan2(vv / fr.top, uu)) % 360
        if fr.skull_radius(uu, vv) < big[int(phi * NB / 360) % NB]:
            ub = uu
    ub -= 0.09

    def make_band(ub):
        m = (cv.V > brim(cv.U)) & (cv.V < brim(cv.U) + 0.25) & (np.abs(cv.U) < ub)
        for s in (-1, 1):
            m |= cv.ellipse(s * ub, float(brim(ub)) + 0.125, 0.10, 0.125, -s * math.degrees(math.atan(0.6 * ub)))
        return m

    band = make_band(ub)
    while cv.touches_border(band, 3) and ub > 0.7:
        ub -= 0.03
        band = make_band(ub)

    # the pom-pom sits on the crown; where the image is too tight above the head it
    # slides down the dome and, if need be, shrinks and tucks onto the dome
    # it is drawn in front of the dome, so it may sink into the crown where the image is tight
    pom = None
    for rad in (0.25, 0.22, 0.19):
        for lift in (0.55, 0.35, 0.15, -0.05, -0.25, -0.45, -0.65, -0.85, -1.05):
            for deg in (90, 84, 96, 78, 102):
                u, v = dome_point(fr, prof, deg)
                n = math.radians(deg)
                ou, ov = u + rad * lift * math.cos(n), v + rad * lift * math.sin(n)
                m = fuzzy_disc(cv, ou, ov, rad)
                if not cv.touches_border(m, 3):
                    pom = (ou, ov, rad, m)
                    break
            if pom:
                break
        if pom:
            break

    outline_fill(cv, dome, ref, A, ACCENT)
    d_in = dome & ~cv.outline_band(dome, LINE_PX)
    sh = d_in & (fr.skull_radius(cv.U + 0.18, cv.V + 0.05) > prof.mean() - 0.02)
    cv.paint(sh, shade(A, 0.86), ACCENT)
    w = DETAIL_PX / fr.rx
    # a knit stripe in the outfit's main colour round the dome above the band, with
    # a row of little knit "v" stitches in the accent colour
    s0 = lambda u: brim(u) + 0.36
    s1 = lambda u: brim(u) + 0.52
    stripe = d_in & (cv.V > s0(cv.U)) & (cv.V < s1(cv.U))
    cv.paint(stripe, M, MAIN)
    for edge in (s0, s1):
        line = cv.stroke([(u, edge(u)) for u in np.linspace(-1.7, 1.7, 80)], w * 0.9, w * 0.9) & d_in
        cv.paint(line, shade(A, 0.80), ACCENT)
    for k in np.arange(-1.6, 1.61, 0.16):
        mid = (s0(k) + s1(k)) / 2
        vv = cv.stroke([(k - 0.045, mid + 0.035), (k, mid - 0.03), (k + 0.045, mid + 0.035)], w * 0.8, w * 0.8)
        cv.paint(vv & stripe, shade(A, 0.95), ACCENT)
    # soft vertical knit ribs above and below the stripe
    for k in np.linspace(-0.88, 0.88, 11):
        top = dome_point(fr, prof, 90 - k * 62)
        pts = bezier((k * 1.05, brim(k * 1.05) + 0.26), (k * 1.10, 0.95), (top[0] * 0.90, top[1] * 0.90), n=30)
        rib = cv.stroke(pts, 0, 0, taper=lambda t: w * (1.0 - 0.55 * t)) & d_in & ~stripe
        cv.paint(rib, shade(A, 0.82), ACCENT, opacity=0.85)
    lit = d_in & ~stripe & (fr.skull_radius(cv.U + 0.25, cv.V - 0.05) < prof.mean() - 0.30)
    cv.paint(lit, shade(A, 1.04), ACCENT, opacity=0.6)
    # rib band (fold-up cuff)
    outline_fill(cv, band, ref, shade(A, 0.95), ACCENT)
    b_in = band & ~cv.outline_band(band, LINE_PX)
    for k in np.arange(-1.6, 1.61, 0.115):
        pts = [(k, brim(k) + 0.03), (k * 1.03, brim(k) + 0.22)]
        cv.paint(cv.stroke(pts, w * 0.9, w * 0.9) & b_in, shade(A, 0.78), ACCENT)
    cv.paint(b_in & (cv.V < brim(cv.U) + 0.07), shade(A, 0.82), ACCENT, opacity=0.6)
    # the pom-pom: main colour, a cel shadow on the far side and a few yarn tufts
    if pom is not None:
        ou, ov, rad, m = pom
        outline_fill(cv, m, ref, M, MAIN)
        p_in = m & ~cv.outline_band(m, LINE_PX)
        cv.paint(p_in & ((cv.U - ou + 0.10) ** 2 + (cv.V - ov - 0.08) ** 2 > (rad * 0.92) ** 2), shade(M, 0.84), MAIN)
        for k in range(7):
            a = 2 * math.pi * k / 7 + 0.5
            r0, r1 = rad * 0.25, rad * 0.62
            tuft = cv.stroke([(ou + r0 * math.cos(a), ov + r0 * math.sin(a)),
                              (ou + r1 * math.cos(a + 0.25), ov + r1 * math.sin(a + 0.25))], 0, 0,
                             taper=lambda t: w * 0.9 * math.sin(math.pi * max(t, 0.05)))
            cv.paint(tuft & p_in, shade(M, 0.78), MAIN)
    return cv.result()


# ------------------------------------------------------ Sun hat (Garden party)

STRAW = np.array([238.0, 210.0, 148.0])
ROSE = np.array([240.0, 150.0, 160.0])
LEAF = np.array([150.0, 176.0, 112.0])


def sun_hat(ctx):
    """A straw sun hat: a rounded crown on a wide, flat brim seen from just below eye
    level (the brim's back half shows behind the crown), a ribbon band in the outfit
    accent and a small rose on the side. Straw, rose and leaf keep their own colours."""
    fr = hat_frame(ctx); ref = ctx["ref"]
    A = ref["accent"]
    vb = BUCKET_VB
    fr, prof = fit_hat(fr, ctx["hair_union"], COVER_LINES["SunHat"], ctx["shape"], margin=0.04)
    cv = Canvas(fr, ctx["shape"])
    prof = cover_crown(cv, fr, prof, ctx["hair_union"], COVER_LINES["SunHat"])
    half = 0.0
    for uu in np.linspace(0, 2.0, 201):
        k = fr.skull_radius(uu, vb)
        phi = math.degrees(math.atan2(vb / fr.top, uu)) % 360
        if k < prof[int(phi * NB / 360) % NB]:
            half = uu
    vc = vb + 0.04          # brim centre line (where the crown sits)
    rb = 0.25               # brim depth (half height of the brim ellipse)

    def droop(u, Wb):
        return 0.10 * np.clip(np.asarray(u) / Wb, -1, 1) ** 2

    def make_brim(Wb):
        pts = [(Wb * math.cos(t), vc + rb * math.sin(t) - float(droop(Wb * math.cos(t), Wb)))
               for t in np.linspace(0, 2 * math.pi, 160)]
        return cv.polygon(pts)

    Wb = half + 0.46
    brim = make_brim(Wb)
    while cv.touches_border(brim, 4) and Wb > half + 0.12:
        Wb -= 0.02
        brim = make_brim(Wb)
    brim = inside_image(cv, brim, margin_px=4.0, round_px=9.0)
    crown = dome_mask(cv, fr, prof) & (cv.V > vc + 0.01 - droop(cv.U, Wb))
    w = DETAIL_PX / fr.rx
    # brim first (its back half shows behind the crown), with concentric straw rows
    outline_fill(cv, brim, ref, STRAW, FIXED)
    b_in = brim & ~cv.outline_band(brim, LINE_PX)
    for f in (0.45, 0.72):
        pts = [(Wb * f * math.cos(t), vc + rb * f * math.sin(t) - float(droop(Wb * f * math.cos(t), Wb)))
               for t in np.linspace(math.pi * 1.04, math.pi * 1.96, 90)]
        cv.paint(cv.stroke(pts, w * 0.8, w * 0.8) & b_in, shade(STRAW, 0.80), FIXED)
    under = b_in & (cv.V < vc - rb * 0.55 - droop(cv.U, Wb))
    cv.paint(under, shade(STRAW, 0.90), FIXED)
    # crown over the brim's back half; woven straw texture and a cel shadow
    outline_fill(cv, crown, ref, STRAW, FIXED)
    c_in = crown & ~cv.outline_band(crown, LINE_PX)
    cv.paint(c_in & (fr.skull_radius(cv.U + 0.18, cv.V + 0.04) > prof.mean() - 0.01), shade(STRAW, 0.88), FIXED)
    for k in np.arange(-1.5, 1.51, 0.14):
        top = dome_point(fr, prof - 0.08, 90 - k * 55)
        pts = bezier((k, vc + 0.05), (k * 1.05, vc + 0.30), (top[0], top[1]), n=24)
        cv.paint(cv.stroke(pts, 0, 0, taper=lambda t: w * 0.75 * (1 - 0.5 * t)) & c_in, shade(STRAW, 0.82), FIXED)
    # ribbon band in the outfit accent, with a little rose where it meets the brim
    ribbon = c_in & (cv.V < vc + 0.20 - droop(cv.U, Wb))
    cv.paint(ribbon, A, ACCENT)
    cv.paint(ribbon & (cv.V < vc + 0.08 - droop(cv.U, Wb)), shade(A, 0.85), ACCENT)
    edge = [(u, vc + 0.20 - float(droop(u, Wb))) for u in np.linspace(-1.7, 1.7, 80)]
    cv.paint(cv.stroke(edge, w, w) & c_in, ref["outline"], FIXED)
    side = 1 if fr.h["tilt"] < 15 else -1
    ru = side * min(half * 0.72, Wb - 0.25)
    rv = vc + 0.11 - float(droop(ru, Wb))
    leaf = cv.ellipse(ru + side * 0.18, rv - 0.04, 0.14, 0.065, side * -20)
    outline_fill(cv, leaf, ref, LEAF, FIXED, LINE_PX * 0.7)
    rose = fuzzy_disc(cv, ru, rv, 0.15, lobes=5, amp=0.10, phase=0.0)
    outline_fill(cv, rose, ref, ROSE, FIXED, LINE_PX * 0.75)
    r_in = rose & ~cv.outline_band(rose, LINE_PX * 0.75)
    swirl = [(ru + 0.085 * (1 - t * 0.7) * math.cos(5.5 * t + 1.0), rv + 0.085 * (1 - t * 0.7) * math.sin(5.5 * t + 1.0))
             for t in np.linspace(0, 1, 40)]
    cv.paint(cv.stroke(swirl, w * 0.7, w * 0.5) & r_in, shade(ROSE, 0.70), FIXED)
    return cv.result()


FUNCS = {"Bow": bow, "BearBeanie": bear_beanie, "BucketHat": bucket_hat, "DaisyClip": daisy_clip,
         "PomBeanie": pom_beanie, "SunHat": sun_hat}


def draw(kind, ctx):
    return FUNCS[kind](ctx)
