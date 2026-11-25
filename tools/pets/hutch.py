"""PLACEHOLDER rabbit pen (P5): a cosy indoor pen drawn over the shared pet room.

Layers the size of the habitat, drawn at the baby's pose-pixel scale (so the outline is the baby's ~8 px), with fixed
colours and no region map, like the hamster's cage (``habitat.py``) and the fish's tank (``tank.py``):

* ``Back``: behind the rabbit: a low picket pen, a soft mat on the floor, a little wooden hutch with a peaked roof and an
  arched doorway on the left, a hay rack full of hay on the pen's right side and a litter tray with a little hay in it
  below it.
* ``Front``: in front of the rabbit: the mat's front edge, a short pen post at each corner and the greens bowl.
* ``Hay``: the scattered hay of a missed litter tidy (the rabbit's mess), loose strands over the floor round the
  rabbit.  The room shows it at an alpha that follows the mess, between the rabbit and the front layer (the habitat's
  ``haze`` slot, as the fish's cloudy water).

``FLOOR`` is the floor line (the mat's top edge) above the bottom of every layer.
"""

from __future__ import annotations

import math

import numpy as np

import cat
from cat import L, OUTLINE, cel, outline
from raster import Canvas, catmull, ellipse, poly, polyline

W, H = 860, 540
FLOOR = 66                 # px from the image bottom to the floor line
FLOOR_Y = H - FLOOR

WOOD = (236, 204, 160)
WOOD_DEEP = (214, 176, 130)
PICKET = (246, 226, 196)
ROOF = (232, 160, 150)
DOORWAY = (120, 88, 78)
MAT = (196, 222, 200)      # pastel sage mat
MAT_FRONT = (178, 208, 186)
HAY = (240, 214, 132)
HAY_DEEP = (214, 182, 98)
HAY_LIGHT = (250, 234, 172)
TRAY = (190, 206, 236)     # soft periwinkle plastic
TRAY_FRONT = (172, 190, 226)
BOWL = (246, 190, 170)
LEAF = (146, 200, 128)
LEAF_DEEP = (112, 172, 104)
CARROT = (246, 160, 82)


def _tier():
    L.ow = cat.BABY_OW
    L.k = L.ow / cat.BASE_OW
    L.dw = cat.BASE_DW * L.k


def _shaded(cv, fill, color, light=(6, 8), factor=0.86):
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


def _strands(cv, x0, x1, y0, y1, n, seed, length=(18, 34), width=3.6, region=None):
    """Loose hay strands: short slightly curved strokes in two straw tones, each with a fine dark edge."""
    rng = np.random.default_rng(seed)
    m = np.zeros_like(cv.X)
    light = np.zeros_like(cv.X)
    for i in range(n):
        x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
        a = rng.uniform(-0.9, 0.9) + (math.pi if rng.uniform() < 0.5 else 0)
        ln = rng.uniform(*length)
        bend = rng.uniform(-6, 6)
        dx, dy = math.cos(a) * ln, math.sin(a) * ln * 0.55
        pts = [(x, y), (x + dx * 0.5 - dy * 0.1 + bend * 0.3, y + dy * 0.5 + bend), (x + dx, y + dy)]
        s = cv.stroke_mask(catmull(pts, closed=False), width)
        if region is not None:
            s = s * region
        if i % 3 == 0:
            light = np.maximum(light, s)
        else:
            m = np.maximum(m, s)
    edge = np.clip(outline(cv, np.maximum(m, light), ow=1.6, lo=1, hi=1), 0, 1)
    cv.paint(edge * 0.55, HAY_DEEP)
    cv.paint(m, HAY)
    cv.paint(light, HAY_LIGHT)


def back() -> Canvas:
    _tier()
    cv = Canvas(W, H)
    fence_top = FLOOR_Y - 190
    # the pen: pickets with rounded tops on two rails
    pickets = np.zeros_like(cv.X)
    for x in np.arange(26, W - 20, 52):
        pickets = np.maximum(pickets, cv.fill_mask(_rounded(x, fence_top, x + 34, FLOOR_Y - 4, 16)))
    rails = np.zeros_like(cv.X)
    for y in (fence_top + 40, FLOOR_Y - 60):
        rails = np.maximum(rails, cv.fill_mask(_rounded(12, y, W - 12, y + 18, 9)))
    _shaded(cv, pickets, PICKET, light=(3, 6))
    cv.paint(outline(cv, pickets, ow=L.dw * 1.1, lo=1, hi=1), OUTLINE)
    _shaded(cv, rails, WOOD, light=(2, 5))
    cv.paint(outline(cv, rails, ow=L.dw * 1.1, lo=1, hi=1), OUTLINE)

    # the soft mat on the floor
    mat = cv.fill_mask(_rounded(6, FLOOR_Y - 26, W - 6, H - 8, 30))
    _shaded(cv, mat, MAT, light=(2, 8), factor=0.9)
    dots = np.zeros_like(cv.X)
    rng = np.random.default_rng(4)
    for _ in range(26):
        dots = np.maximum(dots, cv.fill_mask(ellipse(rng.uniform(40, W - 40), rng.uniform(FLOOR_Y - 14, H - 24), 5, 3)))
    cv.paint(dots * mat * 0.5, (226, 240, 226))
    cv.paint(outline(cv, mat), OUTLINE)

    # the hutch on the left: a wooden box with a peaked roof and an arched doorway
    hx0, hx1, hy0 = 30, 270, FLOOR_Y - 230
    box = cv.fill_mask(_rounded(hx0, hy0, hx1, FLOOR_Y + 8, 14))
    _shaded(cv, box, WOOD, light=(3, 8))
    for y in np.arange(hy0 + 40, FLOOR_Y, 42):
        cv.paint(cv.stroke_mask(polyline([(hx0 + 10, y), (hx1 - 10, y)]), 3.0) * box * 0.5, WOOD_DEEP)
    door = cv.fill_mask(catmull([(hx0 + 70, FLOOR_Y + 6), (hx0 + 70, hy0 + 110), (hx0 + 120, hy0 + 70),
                                 (hx1 - 70, hy0 + 110), (hx1 - 70, FLOOR_Y + 6)], tension=0.5))
    cv.paint(door, DOORWAY)
    cv.paint(cv.fill_mask(ellipse(hx0 + 120, FLOOR_Y - 10, 46, 14)) * door, (96, 70, 64))
    cv.paint(outline(cv, box), OUTLINE)
    cv.paint(outline(cv, door, ow=L.dw, lo=1, hi=1), OUTLINE)
    roof = cv.fill_mask(catmull([(hx0 - 26, hy0 + 20), ((hx0 + hx1) / 2, hy0 - 96), (hx1 + 26, hy0 + 20),
                                 (hx1 + 14, hy0 + 40), ((hx0 + hx1) / 2, hy0 - 66), (hx0 - 14, hy0 + 40)], tension=0.25))
    _shaded(cv, roof, ROOF, light=(3, 8))
    cv.paint(outline(cv, roof), OUTLINE)
    # a little heart on the roof's gable
    hx, hy = (hx0 + hx1) / 2, hy0 - 22
    heart = cv.fill_mask(catmull([(hx, hy + 12), (hx - 16, hy - 2), (hx - 9, hy - 13), (hx, hy - 6), (hx + 9, hy - 13),
                                  (hx + 16, hy - 2)], tension=0.4))
    cv.paint(heart, (246, 236, 228))
    cv.paint(outline(cv, heart, ow=L.dw * 0.8, lo=1, hi=1), OUTLINE)

    # the hay rack on the right: a wooden V of slats, overflowing with hay
    rx0, rx1, ry0, ry1 = W - 280, W - 60, fence_top - 30, fence_top + 110
    hay = cv.fill_mask(catmull([(rx0 + 6, ry0 + 30), (rx0 + 50, ry0 - 18), (rx0 + 110, ry0 - 6), (rx1 - 60, ry0 - 22),
                                (rx1 - 6, ry0 + 28), (rx1 - 40, ry1 - 30), (rx0 + 40, ry1 - 30)], tension=0.5))
    _shaded(cv, hay, HAY, light=(3, 7), factor=0.88)
    _strands(cv, rx0 + 10, rx1 - 10, ry0 - 16, ry1 - 40, 40, seed=11, length=(16, 30), width=3.0, region=hay)
    cv.paint(outline(cv, hay, ow=L.dw * 1.1, lo=1, hi=1), OUTLINE)
    rack = cv.fill_mask(poly([(rx0, ry0 + 20), (rx1, ry0 + 20), (rx1 - 50, ry1), (rx0 + 50, ry1)]))
    slots = np.zeros_like(cv.X)
    for k in range(5):
        x = rx0 + 46 + k * (rx1 - rx0 - 92) / 4
        slots = np.maximum(slots, cv.fill_mask(_rounded(x - 9, ry0 + 36, x + 9, ry1 - 16, 8)))
    cv.paint(np.clip(rack - slots, 0, 1), np.asarray(WOOD, float))
    _shaded(cv, slots * rack, HAY, light=(2, 4))
    cv.paint(outline(cv, rack), OUTLINE)
    cv.paint(outline(cv, slots * rack, ow=L.dw * 0.8, lo=1, hi=1), OUTLINE)

    # the litter tray below the rack, with a little hay in it
    tx0, tx1, ty0 = W - 300, W - 36, FLOOR_Y - 56
    tray = cv.fill_mask(_rounded(tx0, ty0, tx1, FLOOR_Y + 14, 22))
    _shaded(cv, tray, TRAY, light=(2, 8))
    inside = cv.fill_mask(_rounded(tx0 + 16, ty0 + 10, tx1 - 16, ty0 + 30, 10))
    cv.paint(inside, (234, 226, 206))
    _strands(cv, tx0 + 24, tx1 - 24, ty0 + 10, ty0 + 26, 22, seed=13, length=(14, 26), width=2.8, region=inside)
    cv.paint(outline(cv, tray), OUTLINE)
    cv.paint(outline(cv, inside, ow=L.dw * 0.8, lo=1, hi=1), OUTLINE)

    return cv


def front() -> Canvas:
    _tier()
    cv = Canvas(W, H)
    # the mat's front edge
    lip = cv.fill_mask(_rounded(6, FLOOR_Y + 22, W - 6, H - 6, 22))
    _shaded(cv, lip, MAT_FRONT, light=(2, 6))
    cv.paint(outline(cv, lip), OUTLINE)
    # a short pen post at each corner
    for x in (14, W - 52):
        post = cv.fill_mask(_rounded(x, FLOOR_Y - 70, x + 38, H - 10, 16))
        _shaded(cv, post, PICKET, light=(3, 6))
        cv.paint(outline(cv, post), OUTLINE)
    # the greens bowl at the front left, with leaves and a carrot top
    bx, by = 170, FLOOR_Y + 30
    leaves = np.zeros_like(cv.X)
    for (dx, dy, r, a) in ((-30, -20, 26, -0.5), (0, -30, 28, 0.1), (30, -18, 24, 0.6), (-8, -14, 22, 1.2)):
        leaves = np.maximum(leaves, cv.fill_mask(ellipse(bx + dx, by + dy, r, r * 0.62, a)))
    _shaded(cv, leaves, LEAF, light=(3, 5))
    for (dx, dy, a) in ((-30, -20, -0.5), (0, -30, 0.1), (30, -18, 0.6)):
        cv.paint(cv.stroke_mask(polyline([(bx + dx - 18 * math.cos(a), by + dy - 18 * math.sin(a)),
                                          (bx + dx + 18 * math.cos(a), by + dy + 18 * math.sin(a))]), 2.6) * leaves, LEAF_DEEP)
    carrot = cv.fill_mask(poly([(bx + 44, by - 34), (bx + 62, by - 22), (bx + 20, by - 6)]))
    cv.paint(carrot, CARROT)
    cv.paint(outline(cv, np.maximum(leaves, carrot), ow=L.dw, lo=1, hi=1), OUTLINE)
    bowl = cv.fill_mask(poly([(bx - 66, by - 14), (bx + 66, by - 14), (bx + 48, by + 26), (bx - 48, by + 26)]))
    bowl = np.maximum(bowl, cv.fill_mask(ellipse(bx, by - 14, 66, 12)))
    _shaded(cv, bowl, BOWL, light=(3, 6))
    cv.paint(outline(cv, bowl), OUTLINE)
    return cv


def hay() -> Canvas:
    """Scattered hay over the floor (the mess), drawn fully; the room fades it in with the mess."""
    _tier()
    cv = Canvas(W, H)
    _strands(cv, 60, W - 60, FLOOR_Y - 18, H - 26, 120, seed=21, length=(20, 40), width=4.0)
    # a few small clumps
    for (x, y) in ((180, FLOOR_Y + 4), (430, FLOOR_Y + 26), (660, FLOOR_Y + 2)):
        clump = cv.fill_mask(ellipse(x, y, 30, 10))
        _shaded(cv, clump, HAY, light=(2, 4), factor=0.9)
        _strands(cv, x - 30, x + 30, y - 8, y + 8, 10, seed=int(x), length=(16, 26), width=3.2)
    return cv
