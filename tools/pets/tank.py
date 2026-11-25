"""PLACEHOLDER fish tank (P4): a rounded glass tank drawn over the shared pet room.

Layers the size of the habitat, drawn at the baby's pose-pixel scale (so the outline is the baby's ~8 px), with fixed
colours and no region map, like the hamster's cage (``habitat.py``):

* ``Back``: behind the fish: the tank's lid with its soft light, the water (a gentle aqua gradient with a lighter
  surface band), a few soft plants, a pebble bed and a bubbler stone with a little column of bubbles.
* ``Front``: in front of the fish: the tank's base rail with the pebbles' front edge, a short plant at each corner and
  two glass highlights.
* ``Haze``: the cloudy water, a milky tint over the water area only.  The room shows it at an alpha that follows the
  water (low Water, cloudy water from a missed wipe, flakes left over), between the fish and the front layer.
* ``Castle``: the tank castle (the fish's ``TankCastle`` accessory), a little pastel castle on the pebbles behind the
  fish, shown only when the fish has it.

``FLOOR`` is the floor line (the pebble bed's top edge) above the bottom of every layer; ``SWIM`` is how far above it
the fish's pose stands when it swims (``layout.json`` ``habitat``).
"""

from __future__ import annotations

import math

import numpy as np

import cat
from cat import L, OUTLINE, cel, outline
from raster import Canvas, catmull, ellipse, polyline

W, H = 820, 600
FLOOR = 96                 # px from the image bottom to the floor line (the pebbles' top)
FLOOR_Y = H - FLOOR
SWIM = 150                 # px above the floor line where a swimming fish's pose stands
WATER_TOP = 96             # the water surface
TANK = (16, 40, W - 16, H - 30)

FRAME = (196, 190, 216)    # soft lilac-grey rails
FRAME_DEEP = (176, 168, 200)
LIGHT = (255, 238, 196)
WATER_TOP_C = (204, 236, 238)
WATER_BOTTOM_C = (160, 210, 224)
SURFACE = (226, 246, 244)
PLANT = (132, 192, 150)
PLANT_DEEP = (104, 166, 128)
PLANT_SOFT = (168, 214, 170)
PEBBLES = [(240, 220, 196), (222, 206, 232), (246, 204, 190), (212, 228, 214), (236, 232, 222)]
SAND = (236, 222, 200)
STONE = (198, 196, 210)
BUBBLE = (246, 252, 252)
HAZE = (214, 226, 204)
CASTLE = (240, 206, 214)
CASTLE_DEEP = (220, 180, 196)
DOOR = (120, 96, 110)
FLAG = (246, 196, 120)


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


def _water(cv):
    """The water area inside the glass, from the surface to the bottom of the tank."""
    x0, _, x1, y1 = TANK
    return cv.fill_mask(_rounded(x0 + 10, WATER_TOP, x1 - 10, y1 - 4, 22))


def _plant(cv, base_x, base_y, height, lean, seed, color=PLANT, blades=5):
    """A soft clump of rounded leaves rising from the pebbles."""
    rng = np.random.default_rng(seed)
    m = np.zeros_like(cv.X)
    for i in range(blades):
        a = (i - (blades - 1) / 2) * 0.22 + lean + rng.uniform(-0.06, 0.06)
        h = height * rng.uniform(0.72, 1.0)
        bx = base_x + (i - (blades - 1) / 2) * 10
        tip = (bx + math.sin(a) * h, base_y - math.cos(a) * h)
        mid = (bx + math.sin(a) * h * 0.5 + rng.uniform(-14, 14), base_y - math.cos(a) * h * 0.55)
        w = rng.uniform(22, 30)
        leaf = catmull([(bx - w * 0.5, base_y + 6), (mid[0] - w * 0.55, mid[1]), (tip[0], tip[1]),
                        (mid[0] + w * 0.55, mid[1]), (bx + w * 0.5, base_y + 6)], tension=0.5)
        m = np.maximum(m, cv.fill_mask(leaf))
    _shaded(cv, m, color, light=(4, 6), factor=0.84)
    # a pale vein down each leaf
    rng = np.random.default_rng(seed)
    for i in range(blades):
        a = (i - (blades - 1) / 2) * 0.22 + lean + rng.uniform(-0.06, 0.06)
        h = height * rng.uniform(0.72, 1.0)
        bx = base_x + (i - (blades - 1) / 2) * 10
        rng.uniform(-14, 14)
        rng.uniform(22, 30)
        vein = cv.stroke_mask(polyline([(bx, base_y), (bx + math.sin(a) * h * 0.8, base_y - math.cos(a) * h * 0.8)]), 2.4)
        cv.paint(vein * m * 0.45, PLANT_SOFT)
    cv.paint(outline(cv, m, ow=L.dw * 1.2, lo=1, hi=1), OUTLINE)
    return m


def _pebbles(cv, y0, y1, seed, n):
    rng = np.random.default_rng(seed)
    bed = cv.fill_mask(_rounded(TANK[0] + 12, y0, TANK[2] - 12, y1, 18))
    _shaded(cv, bed, SAND, light=(2, 6), factor=0.9)
    for _ in range(n):
        x = rng.uniform(TANK[0] + 30, TANK[2] - 30)
        y = rng.uniform(y0 + 10, y1 - 8)
        r = rng.uniform(9, 17)
        p = cv.fill_mask(ellipse(x, y, r, r * 0.72, rng.uniform(-0.4, 0.4))) * bed
        col = PEBBLES[int(rng.integers(len(PEBBLES)))]
        _shaded(cv, p, col, light=(2, 3), factor=0.86)
        cv.paint(outline(cv, p, ow=2.6, lo=1, hi=1) * 0.55, OUTLINE)
    cv.paint(outline(cv, bed, ow=L.dw * 1.1, lo=1, hi=1), OUTLINE)


def _bubbles(cv, x, y, count, seed):
    rng = np.random.default_rng(seed)
    for i in range(count):
        r = 7 + 3 * (i % 3)
        bx = x + math.sin(i * 1.7) * 10 + rng.uniform(-3, 3)
        by = y - i * 46
        ring = cv.stroke_mask(ellipse(bx, by, r, r), 2.6)
        cv.paint(cv.fill_mask(ellipse(bx, by, r, r)) * 0.35, BUBBLE)
        cv.paint(ring * 0.85, (150, 196, 210))
        cv.paint(cv.fill_mask(ellipse(bx - r * 0.35, by - r * 0.35, r * 0.28, r * 0.24)), BUBBLE)


def back() -> Canvas:
    _tier()
    cv = Canvas(W, H)
    x0, y0, x1, y1 = TANK
    # the glass box: a soft rim round the water
    glass = cv.fill_mask(_rounded(x0, y0 + 24, x1, y1, 30))
    cv.paint(glass * 0.55, (226, 240, 244))
    cv.paint(outline(cv, glass, ow=L.ow, lo=0.9, hi=1.1), OUTLINE)

    # the water: a gentle vertical gradient and a lighter band under the surface
    water = _water(cv)
    t = np.clip((cv.Y - WATER_TOP) / (y1 - WATER_TOP), 0, 1)[..., None]
    col = np.asarray(WATER_TOP_C, float) * (1 - t) + np.asarray(WATER_BOTTOM_C, float) * t
    cv.paint(water, col)
    surface = cv.fill_mask(_rounded(x0 + 10, WATER_TOP, x1 - 10, WATER_TOP + 18, 9)) * water
    cv.paint(surface * 0.8, SURFACE)
    # soft light rays from the lid
    for k, rx in enumerate((210, 380, 560)):
        ray = cv.fill_mask(catmull([(rx - 26, WATER_TOP), (rx + 26, WATER_TOP), (rx + 70, FLOOR_Y), (rx + 6, FLOOR_Y)],
                                   tension=0.0)) * water
        cv.paint(cv.blur(ray, 6) * 0.16, (255, 255, 246))

    # plants behind the fish
    _plant(cv, 96, FLOOR_Y + 10, 300, -0.12, 3, PLANT_DEEP, 6)
    _plant(cv, 690, FLOOR_Y + 10, 340, 0.10, 5, PLANT, 7)
    _plant(cv, 560, FLOOR_Y + 12, 170, 0.18, 9, PLANT_SOFT, 4)

    # the bubbler stone and its bubbles, to the right
    _bubbles(cv, 640, FLOOR_Y - 36, 7, 11)
    stone = cv.fill_mask(ellipse(640, FLOOR_Y + 4, 30, 16))
    _shaded(cv, stone, STONE, light=(2, 3))
    cv.paint(outline(cv, stone, ow=L.dw * 1.1, lo=1, hi=1), OUTLINE)

    # the pebble bed, its top edge the floor line
    _pebbles(cv, FLOOR_Y - 6, y1 - 6, 17, 70)

    # the lid with its warm light
    lid = cv.fill_mask(_rounded(x0 - 6, y0 - 22, x1 + 6, y0 + 30, 18))
    _shaded(cv, lid, FRAME, light=(2, 6))
    lamp = cv.fill_mask(_rounded(x0 + 120, y0 + 18, x1 - 120, y0 + 30, 6))
    cv.paint(lamp, LIGHT)
    cv.paint(outline(cv, lid), OUTLINE)
    return cv


def front() -> Canvas:
    _tier()
    cv = Canvas(W, H)
    x0, y0, x1, y1 = TANK
    # the front edge of the pebbles and the base rail
    lip = cv.fill_mask(_rounded(x0 + 12, FLOOR_Y + 26, x1 - 12, y1 - 6, 16))
    _shaded(cv, lip, SAND, light=(2, 6), factor=0.9)
    rng = np.random.default_rng(23)
    for _ in range(26):
        x = rng.uniform(x0 + 30, x1 - 30)
        y = rng.uniform(FLOOR_Y + 32, y1 - 12)
        r = rng.uniform(9, 15)
        p = cv.fill_mask(ellipse(x, y, r, r * 0.72, rng.uniform(-0.4, 0.4))) * lip
        _shaded(cv, p, PEBBLES[int(rng.integers(len(PEBBLES)))], light=(2, 3), factor=0.86)
        cv.paint(outline(cv, p, ow=2.6, lo=1, hi=1) * 0.55, OUTLINE)
    cv.paint(outline(cv, lip, ow=L.dw * 1.1, lo=1, hi=1), OUTLINE)
    rail = cv.fill_mask(_rounded(x0 - 6, y1 - 14, x1 + 6, H - 4, 14))
    _shaded(cv, rail, FRAME_DEEP, light=(2, 4))
    cv.paint(outline(cv, rail), OUTLINE)

    # a short plant in each front corner
    _plant(cv, 52, FLOOR_Y + 40, 120, 0.15, 31, PLANT, 4)
    _plant(cv, W - 60, FLOOR_Y + 40, 100, -0.2, 37, PLANT_DEEP, 3)

    # two glass highlights
    for (gx, gy, gl, gw) in ((W - 140, y0 + 70, 130, 12), (W - 108, y0 + 92, 70, 7)):
        streak = cv.stroke_mask(polyline([(gx, gy), (gx + gl * 0.45, gy + gl)]), gw)
        cv.paint(streak * 0.55, (255, 255, 255))
    return cv


def haze() -> Canvas:
    """The cloudy water: a milky tint over the water area (the room sets its strength)."""
    _tier()
    cv = Canvas(W, H)
    water = _water(cv)
    # a little more at the bottom, where the leftover flakes settle
    t = np.clip((cv.Y - WATER_TOP) / (TANK[3] - WATER_TOP), 0, 1)
    cv.paint(water * (0.55 + 0.25 * t), HAZE)
    return cv


def castle() -> Canvas:
    """The tank castle: a little pastel castle on the pebbles, to the left, behind the fish."""
    _tier()
    cv = Canvas(W, H)
    bx, by = 210, FLOOR_Y + 18
    walls = np.zeros_like(cv.X)
    # the keep and two towers with crenellations
    for (cx, w, h) in ((bx, 120, 130), (bx - 74, 56, 170), (bx + 74, 56, 150)):
        body = cv.fill_mask(_rounded(cx - w / 2, by - h, cx + w / 2, by, 8))
        for k in range(int(w // 24)):
            mx = cx - w / 2 + 6 + k * 24
            body = np.maximum(body, cv.fill_mask(_rounded(mx, by - h - 18, mx + 14, by - h + 4, 3)))
        walls = np.maximum(walls, body)
    _shaded(cv, walls, CASTLE, light=(4, 6), factor=0.84)
    # stone lines
    for y in range(int(by - 150), int(by), 30):
        cv.paint(cv.stroke_mask(polyline([(bx - 100, y), (bx + 100, y)]), 2.4) * walls * 0.35, CASTLE_DEEP)
    # an arched door and two windows
    door = cv.fill_mask(catmull([(bx - 24, by), (bx - 24, by - 46), (bx, by - 66), (bx + 24, by - 46), (bx + 24, by)],
                                tension=0.4))
    cv.paint(door, DOOR)
    for (wx, wy) in ((bx - 74, by - 120), (bx + 74, by - 104)):
        win = cv.fill_mask(_rounded(wx - 9, wy - 14, wx + 9, wy + 14, 8))
        cv.paint(win, DOOR)
    cv.paint(outline(cv, walls), OUTLINE)
    cv.paint(outline(cv, door, ow=L.dw, lo=1, hi=1), OUTLINE)
    # a little flag on the tall tower
    pole = cv.stroke_mask(polyline([(bx - 74, by - 188), (bx - 74, by - 236)]), 4)
    cv.paint(pole, OUTLINE)
    flag = cv.fill_mask(catmull([(bx - 72, by - 236), (bx - 30, by - 226), (bx - 72, by - 214)], tension=0.2))
    _shaded(cv, flag, FLAG, light=(2, 2))
    cv.paint(outline(cv, flag, ow=L.dw, lo=1, hi=1), OUTLINE)
    return cv
