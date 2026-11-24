"""PLACEHOLDER hamster habitat (P2): a modest cage drawn over the shared pet room.

Two layers the size of the habitat, drawn at the baby's pose-pixel scale (so the
outline is the baby's ~8 px): ``Back`` stands behind the hamster (a wire dome on
a pastel tray, a bedding mound, the exercise wheel and a play tunnel) and
``Front`` in front of it (the tray's front lip with a spill of bedding and the
seed bowl), so the hamster sits in its bedding.  Fixed colours, no region map:
the room theme around it stays as it is.  The floor line the hamster stands on
is ``FLOOR`` pixels above the bottom of both images (layout.json ``habitat``).
"""

from __future__ import annotations

import math

import numpy as np

import cat
from cat import L, OUTLINE, cel, outline
from raster import Canvas, catmull, ellipse, poly, polyline

W, H = 760, 480
FLOOR = 64                 # px from the image bottom to the floor line
FLOOR_Y = H - FLOOR
TRAY = (170, 212, 192)     # pastel mint plastic
TRAY_FRONT = (156, 200, 180)
BARS = (184, 178, 204)     # soft lilac-grey wire
BEDDING = (242, 226, 192)
SHAVING = (214, 188, 146)
WHEEL = (150, 190, 228)
TUNNEL = (246, 190, 150)
BOWL = (240, 170, 176)
SEED = (206, 160, 98)
SEED_LIGHT = (236, 206, 150)
DARK = (120, 96, 92)


def _tier():
    L.ow = cat.BABY_OW
    L.k = L.ow / cat.BASE_OW
    L.dw = cat.BASE_DW * L.k


def _shaded(cv, fill, color, light=(6, 8), factor=0.84):
    F = cel(cv, fill, light, factor)
    cv.paint(fill, np.asarray(color, float)[None, None, :] * cat.warm(F))


def _rounded(x0, y0, x1, y1, r):
    def p(ctx):
        ctx.new_sub_path()
        ctx.arc(x1 - r, y0 + r, r, -math.pi / 2, 0)
        ctx.arc(x1 - r, y1 - r, r, 0, math.pi / 2)
        ctx.arc(x0 + r, y1 - r, r, math.pi / 2, math.pi)
        ctx.arc(x0 + r, y0 + r, r, math.pi, 1.5 * math.pi)
        ctx.close_path()
    return p


def _shavings(cv, region, seed, n=60):
    rng = np.random.default_rng(seed)
    ys, xs = np.nonzero(region > 0.5)
    if len(xs) == 0:
        return
    pick = rng.choice(len(xs), size=min(n, len(xs)), replace=False)
    for i in pick:
        x, y = cv.X[ys[i], xs[i]], cv.Y[ys[i], xs[i]]
        a = rng.uniform(0, math.pi)
        r = rng.uniform(5, 9)

        def arc(ctx, x=x, y=y, a=a, r=r):
            ctx.arc(x, y, r, a, a + 2.2)
        cv.paint(cv.stroke_mask(arc, 2.6) * region, SHAVING)


def back() -> Canvas:
    _tier()
    cv = Canvas(W, H)
    cx = W / 2
    tray_top = FLOOR_Y - 70
    # wire dome: bars inside an arch, the rim bars a little heavier
    arch = ellipse(cx, tray_top + 8, W / 2 - 24, tray_top - 12)
    inside = cv.fill_mask(arch) * (cv.Y <= tray_top + 8)
    bars = np.zeros_like(cv.X)
    for x in np.arange(56, W - 40, 44):
        bars = np.maximum(bars, cv.stroke_mask(polyline([(x, 0), (x, tray_top + 4)]), 4.5))
    bars = bars * inside
    for y in (tray_top - 140, tray_top - 280):
        bars = np.maximum(bars, cv.stroke_mask(polyline([(0, y), (W, y)]), 4.5) * inside)
    rim = np.clip(cv.stroke_mask(arch, 9.0) * (cv.Y <= tray_top + 8), 0, 1)
    cv.paint(bars * 0.85, BARS)
    cv.paint(rim, BARS)
    cv.paint(np.clip(outline(cv, rim, ow=3.2, lo=1, hi=1) * 0.55, 0, 1), OUTLINE)
    # a little door latch on the dome
    latch = cv.fill_mask(_rounded(cx - 16, tray_top - 200, cx + 16, tray_top - 186, 6))
    cv.paint(latch, BARS)
    cv.paint(outline(cv, latch, ow=3.0, lo=1, hi=1), OUTLINE)

    # the tray's back wall
    tray = cv.fill_mask(_rounded(14, tray_top, W - 14, FLOOR_Y + 30, 26))
    _shaded(cv, tray, TRAY, light=(2, 10))
    cv.paint(outline(cv, tray), OUTLINE)

    # the wheel on its stand, to the right
    wx, wy, wr = W - 170, tray_top - 40, 118
    stand = np.zeros_like(cv.X)
    for dx in (-46, 46):
        stand = np.maximum(stand, cv.stroke_mask(polyline([(wx, wy), (wx + dx, FLOOR_Y - 8)]), 14))
    _shaded(cv, stand, (200, 210, 226))
    cv.paint(outline(cv, stand, ow=L.dw * 1.1, lo=1, hi=1), OUTLINE)
    disc = cv.fill_mask(ellipse(wx, wy, wr, wr))
    cv.paint(disc * 0.30, (232, 242, 250))
    ring = np.clip(cv.fill_mask(ellipse(wx, wy, wr, wr)) - cv.fill_mask(ellipse(wx, wy, wr - 20, wr - 20)), 0, 1)
    spokes = np.zeros_like(cv.X)
    for k in range(6):
        a = k * math.pi / 3 + 0.3
        spokes = np.maximum(spokes, cv.stroke_mask(polyline([(wx, wy), (wx + math.cos(a) * (wr - 12), wy + math.sin(a) * (wr - 12))]), 6))
    for k in range(18):
        a = k * math.pi / 9
        r0, r1 = wr - 20, wr - 4
        cv.paint(cv.stroke_mask(polyline([(wx + math.cos(a) * r0, wy + math.sin(a) * r0),
                                          (wx + math.cos(a) * r1, wy + math.sin(a) * r1)]), 3.0) * 0.5, (110, 150, 196))
    _shaded(cv, np.maximum(ring, spokes), WHEEL)
    hub = cv.fill_mask(ellipse(wx, wy, 16, 16))
    _shaded(cv, hub, (226, 236, 246))
    cv.paint(outline(cv, np.maximum(ring, hub)), OUTLINE)
    cv.paint(outline(cv, spokes, ow=L.dw * 0.8, lo=1, hi=1) * (1 - ring), OUTLINE)

    # bedding mound behind the hamster, with a scatter of shavings
    mound = cv.fill_mask(catmull([(30, FLOOR_Y + 6), (90, tray_top + 26), (250, tray_top + 4), (420, tray_top + 14),
                                  (600, tray_top + 2), (W - 34, tray_top + 30), (W - 30, FLOOR_Y + 6)], tension=0.5))
    _shaded(cv, mound, BEDDING, light=(4, 10), factor=0.88)
    _shavings(cv, mound, seed=3, n=70)
    cv.paint(outline(cv, mound, ow=L.dw * 1.1, lo=1, hi=1), OUTLINE)

    # a play tunnel lying in the bedding to the left
    tx0, tx1, ty = 46, 220, FLOOR_Y - 46
    tube = cv.fill_mask(_rounded(tx0, ty - 40, tx1, ty + 40, 40))
    _shaded(cv, tube, TUNNEL, light=(2, 8))
    for x in (tx0 + 60, tx0 + 110):
        cv.paint(cv.stroke_mask(polyline([(x, ty - 38), (x + 6, ty), (x, ty + 38)]), 3.2) * tube * 0.6, (214, 150, 112))
    mouth = cv.fill_mask(ellipse(tx1 - 16, ty, 22, 36))
    cv.paint(mouth, (110, 80, 72))
    cv.paint(cv.fill_mask(ellipse(tx1 - 12, ty + 6, 14, 26)) * mouth, (80, 58, 54))
    cv.paint(outline(cv, tube), OUTLINE)
    cv.paint(outline(cv, mouth, ow=L.dw, lo=1, hi=1), OUTLINE)
    return cv


def front() -> Canvas:
    _tier()
    cv = Canvas(W, H)
    lip_top = FLOOR_Y + 2
    # the tray's front lip, with a spill of bedding along its top edge
    spill = cv.fill_mask(catmull([(26, lip_top + 10), (80, lip_top - 12), (180, lip_top - 6), (300, lip_top - 16),
                                  (440, lip_top - 8), (560, lip_top - 16), (690, lip_top - 6), (W - 24, lip_top + 10)],
                                 tension=0.5, closed=True))
    _shaded(cv, spill, BEDDING, light=(4, 10), factor=0.9)
    _shavings(cv, spill, seed=5, n=26)
    cv.paint(outline(cv, spill, ow=L.dw * 1.1, lo=1, hi=1), OUTLINE)
    lip = cv.fill_mask(_rounded(6, lip_top, W - 6, H - 6, 24))
    _shaded(cv, lip, TRAY_FRONT, light=(2, 8))
    shine = cv.fill_mask(_rounded(34, lip_top + 12, W - 34, lip_top + 20, 4))
    cv.paint(shine * 0.55, (214, 238, 226))
    cv.paint(outline(cv, lip), OUTLINE)

    # the seed bowl at the front, to the left
    bx, by = 172, lip_top + 4
    seeds = cv.fill_mask(ellipse(bx, by - 10, 52, 18))
    cv.paint(seeds, SEED)
    rng = np.random.default_rng(9)
    for _ in range(16):
        x, y = bx + rng.uniform(-40, 40), by - 12 + rng.uniform(-8, 6)
        cv.paint(cv.fill_mask(ellipse(x, y, 6, 3.6, rng.uniform(0, math.pi))) * seeds, SEED_LIGHT)
    bowl = cv.fill_mask(poly([(bx - 64, by - 10), (bx + 64, by - 10), (bx + 46, by + 34), (bx - 46, by + 34)]))
    bowl = np.maximum(bowl, cv.fill_mask(ellipse(bx, by - 10, 64, 12)))
    _shaded(cv, bowl, BOWL, light=(3, 6))
    cv.paint(outline(cv, np.maximum(bowl, seeds)), OUTLINE)
    return cv
