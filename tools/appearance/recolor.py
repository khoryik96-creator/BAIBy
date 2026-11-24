"""Contract recolour + composite math, mirroring
unity/Assets/BAIby/Character/Appearance/AppearanceRecolor.cs operation for operation.

Images are uint8 RGBA arrays (H, W, 4), rows top-down. Region maps are RGBA8 with
R = skin, G = hair, B = outfit main, A = outfit accent weights.
Rounding: round-half-to-even on doubles (numpy.rint == C# Math.Round default), clamped 0..255.
"""

from __future__ import annotations

import numpy as np

# Palette targets (sRGB). ``None`` = identity option -> use the measured reference.
SKIN = {
    "Warm": None, "Fair": (248, 218, 196), "Light": (238, 192, 154),
    "Tan": (196, 128, 82), "Brown": (166, 104, 68), "Deep": (120, 72, 48),
}
HAIR = {
    "Chestnut": None, "Black": (46, 36, 34), "Espresso": (82, 54, 40),
    "Auburn": (150, 70, 40), "Honey": (200, 146, 80), "Platinum": (224, 206, 168),
}
OUTFIT = {
    "Sage": None,
    "Oat": ((226, 206, 178), (188, 150, 116)),
    "Sky": ((222, 234, 242), (138, 170, 196)),
    "Blush": ((246, 226, 220), (212, 150, 150)),
    "Butter": ((248, 236, 196), (226, 190, 96)),
    "Lilac": ((232, 222, 242), (170, 150, 200)),
    "Terracotta": ((240, 222, 200), (196, 116, 84)),
    "Midnight": ((70, 82, 110), (226, 190, 120)),
}


def _round_byte(v):
    v = np.where(np.isnan(v), 0.0, v)
    return np.where(v >= 255, 255.0, np.where(v > 0, np.rint(v), 0.0))


def targets(reference: dict, skin="Warm", hair="Chestnut", outfit="Sage"):
    """Return (targets, references) as four RGB tuples in region-channel order."""
    ref = [tuple(reference["skin"]), tuple(reference["hair"]),
           tuple(reference["outfitMain"]), tuple(reference["outfitAccent"])]
    s = SKIN[skin] or ref[0]
    h = HAIR[hair] or ref[1]
    o = OUTFIT[outfit] or (ref[2], ref[3])
    return [s, h, o[0], o[1]], ref


def composite(dst_img, dst_map, src_img, src_map, x, y):
    """Straight-alpha 'over' of an overlay and its region map at (x, y), y from the top.

    Region weights blend with the same coefficients as colour, then any rounding
    excess over 255 is taken from the largest weight (first in R,G,B,A on ties).
    """
    img = dst_img.copy()
    mp = dst_map.copy()
    H, W = img.shape[:2]
    h, w = src_img.shape[:2]
    # clip to canvas
    sx0, sy0 = max(0, -x), max(0, -y)
    sx1, sy1 = min(w, W - x), min(h, H - y)
    if sx1 <= sx0 or sy1 <= sy0:
        return img, mp
    s = src_img[sy0:sy1, sx0:sx1].astype(np.float64)
    sm = src_map[sy0:sy1, sx0:sx1].astype(np.float64)
    d = img[y + sy0:y + sy1, x + sx0:x + sx1].astype(np.float64)
    dm = mp[y + sy0:y + sy1, x + sx0:x + sx1].astype(np.float64)
    sa_i = s[..., 3]
    a = sa_i / 255.0
    b = d[..., 3] / 255.0
    alpha = a + b * (1 - a)
    safe = np.where(alpha > 0, alpha, 1.0)
    os_ = (a / safe)[..., None]
    cs_ = (b * (1 - a) / safe)[..., None]
    rgb = _round_byte(s[..., :3] * os_ + d[..., :3] * cs_)
    out_a = _round_byte(alpha * 255)
    wts = _round_byte(sm * os_ + dm * cs_)
    total = wts.sum(axis=-1)
    over = total > 255
    if over.any():
        for idx in zip(*np.nonzero(over)):
            v = wts[idx]
            t = int(v.sum())
            while t > 255:
                k = int(np.argmax(v))   # first max on ties
                v[k] -= 1
                t -= 1
            wts[idx] = v
    full = sa_i == 255
    part = (sa_i > 0) & ~full
    new_d = d.copy()
    new_m = dm.copy()
    new_d[full] = s[full]
    new_m[full] = sm[full]
    new_d[part, :3] = rgb[part]
    new_d[part, 3] = out_a[part]
    new_m[part] = wts[part]
    img[y + sy0:y + sy1, x + sx0:x + sx1] = new_d.astype(np.uint8)
    mp[y + sy0:y + sy1, x + sx0:x + sx1] = new_m.astype(np.uint8)
    return img, mp


def recolor(img, rmap, tgts, refs):
    """out_c = round(s_c (1 - Σw) + Σ_r w_r clamp(round(s_c t_rc / ref_rc), 0, 255))."""
    tables = np.zeros((4, 3, 256))
    src = np.arange(256, dtype=np.float64)
    for r in range(4):
        for c in range(3):
            ref = float(refs[r][c])
            if ref > 0:
                tables[r, c] = _round_byte(src * float(tgts[r][c]) / ref)
            else:
                tables[r, c] = src
    m = rmap.astype(np.int64)
    total = m.sum(axis=-1)
    w = m.astype(np.float64) / 255.0
    norm = total > 255
    if norm.any():
        w[norm] = m[norm] / total[norm][:, None]
    keep = 1 - (w[..., 0] + w[..., 1] + w[..., 2] + w[..., 3])
    keep = np.where(norm, 0.0, keep)
    out = img.copy()
    s = img[..., :3].astype(np.int64)
    for c in range(3):
        sc = s[..., c]
        rec = (w[..., 0] * tables[0, c][sc] + w[..., 1] * tables[1, c][sc]
               + w[..., 2] * tables[2, c][sc] + w[..., 3] * tables[3, c][sc])
        val = _round_byte(sc * keep + rec)
        out[..., c] = np.where(total == 0, img[..., c], val).astype(np.uint8)
    return out
