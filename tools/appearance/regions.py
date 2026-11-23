"""Region classification (skin / hair / outfit main / outfit accent) with soft weights.

Every colour class is modelled as the segment between the dark-brown outline
colour ``O`` and the class reference colour ``C``.  A pixel ``p`` close to that
segment is ``t * C + (1 - t) * O`` (anti-aliased line edge, or C shaded).  The
weight that makes the contract recolour (``s * target / reference``) reproduce
``t * C' + (1 - t) * O`` is ``t * L(C) / L(p)``; for plain shading (p = k*C)
it evaluates to 1, so shaded fabric/skin recolours fully, while line pixels
fade to 0.
"""

from __future__ import annotations

import numpy as np
from scipy import ndimage as ndi

LUMA = np.array([0.299, 0.587, 0.114])

# Initial prototypes (refined by ``measure_references``).
PROTO = {
    "outline": np.array([78.0, 42.0, 24.0]),
    "skin": np.array([231.0, 160.0, 107.0]),
    "hair": np.array([128.0, 80.0, 57.0]),
    "main": np.array([246.0, 222.0, 198.0]),
    "accent": np.array([174.0, 168.0, 120.0]),
}

CLASSES = ["skin", "hair", "main", "accent"]


def _segment(rgb, o, c):
    d = c - o
    t = ((rgb - o) @ d) / float(d @ d)
    proj = o + np.clip(t, 0, 1.15)[..., None] * d
    dist = np.linalg.norm(rgb - proj, axis=-1)
    lum_c = float(c @ LUMA)
    lum_p = np.maximum(rgb @ LUMA, 1.0)
    w = np.clip(np.clip(t, 0, None) * lum_c / lum_p, 0, 1)
    return dist, w, t


def soft_weights(rgb, alpha, protos, allow, tol=34.0, return_best=False):
    """Return float weights (H, W, 4) in 0..1 for skin, hair, main, accent.

    ``allow`` maps class -> bool mask of pixels where that class may be chosen.
    """
    h, w = alpha.shape
    o = protos["outline"]
    best = np.full((h, w), np.inf)
    out = np.zeros((h, w, 4))
    for i, name in enumerate(CLASSES):
        dist, wt, t = _segment(rgb, o, protos[name])
        ok = allow[name] & (dist < tol) & (dist < best)
        best = np.where(ok, dist, best)
        out[ok] = 0
        out[ok, i] = wt[ok]
    # fully transparent pixels carry no weight (colour is meaningless there)
    out[alpha < 1] = 0
    # pixels close to the outline colour are line art
    near_line = np.linalg.norm(rgb - o, axis=-1) < 18
    out[near_line] = 0
    best[near_line] = np.inf
    if return_best:
        return out, best
    return out


def hair_skin_mix(rgb, alpha, protos, wts, best, allow, tol=22.0):
    """Split pixels that mix hair and skin (strand edges, hair shadow on skin).

    ``p = a H + (1 - a) S`` gets hair weight ``a L(H) / L(p)`` and skin weight
    ``(1 - a) L(S) / L(p)``, so a light hair colour lifts only the hair share
    (no blown-out strands or bright fringes where curls sit on skin).
    """
    H, S = protos["hair"], protos["skin"]
    d = S - H
    a = 1.0 - ((rgb - H) @ d) / float(d @ d)          # 1 = hair, 0 = skin
    ac = np.clip(a, 0, 1)
    proj = H + (1 - ac)[..., None] * d
    dist = np.linalg.norm(rgb - proj, axis=-1)
    ok = allow & (alpha >= 1) & (a > 0.02) & (a < 0.98) & (dist < tol) & (dist < best)
    lp = np.maximum(rgb @ LUMA, 1.0)
    wh = np.clip(ac * float(H @ LUMA) / lp, 0, 1)
    ws = np.clip((1 - ac) * float(S @ LUMA) / lp, 0, 1)
    out = wts.copy()
    out[ok] = 0
    out[ok, 1] = wh[ok]
    out[ok, 0] = ws[ok]
    tot = out.sum(axis=-1)
    over = tot > 1
    out[over] /= tot[over][:, None]
    return out


def blush_soften(rgb, wts, protos, strength=0.32):
    """Cheek blush keeps a share of its own colour so it stays rosy on every skin tone.

    The blush is the skin colour with green/blue pulled down; its skin weight is
    lowered in proportion, so on deep skin it reads as a warm rosy glow instead
    of a dark smudge (and a touch stronger on fair skin).
    """
    S = protos["skin"]
    gr_ref = S[1] / S[0]
    r = np.maximum(rgb[..., 0], 1.0)
    b = np.clip((gr_ref - rgb[..., 1] / r) / 0.06, 0, 1)
    b = np.where((wts[..., 0] > 0.5) & (rgb @ LUMA > 120), b, 0.0)
    out = wts.copy()
    out[..., 0] = wts[..., 0] * (1 - strength * b)
    return out


def line_multiply(rgb, alpha, wts, protos, face, radius=13, face_radius=40):
    """Dark line art next to skin or outfit-main recolours by full multiply.

    Outline pixels (and their anti-aliased ramps, the irises, brows, mouth and
    closed-eye lines) take weight 1 of the nearest confident skin/main class, so
    the line becomes ``O * target / reference``: near-black on deep skin and on
    dark outfits, unchanged on the identity look, and never a noisy partial mix.
    Pixels whose nearest confident class is hair or accent keep their weights.
    """
    lum = rgb @ LUMA
    o = protos["outline"]
    conf = []
    for i, name in enumerate(CLASSES):
        c = protos[name]
        m = (wts[..., i] > 0.85) & (lum > 0.72 * float(c @ LUMA)) & (alpha > 128)
        conf.append(m)
    dists = np.stack([ndi.distance_transform_edt(~m) if m.any() else np.full(m.shape, np.inf)
                      for m in conf], -1)
    out = wts.copy()
    for i, name in ((0, "skin"), (2, "main")):
        c = protos[name]
        dist, _, t = _segment(rgb, o, c)
        lineish = (alpha >= 1) & (lum < float(c @ LUMA) - 6) & ((dist < 34) | (np.linalg.norm(rgb - o, axis=-1) < 46))
        lim = np.where(face, face_radius, radius) if name == "skin" else radius
        others = np.min(np.delete(dists, i, axis=-1), axis=-1)
        # clearly nearer this class than any other (a shared line between two
        # classes is split in the middle, without a noisy dither along it)
        own = (dists[..., i] + 2.5 < others) & (dists[..., i] <= lim)
        own = ndi.binary_closing(ndi.binary_opening(own, iterations=1), iterations=1)
        sel = lineish & own
        out[sel] = 0
        out[sel, i] = 1
    # accent-coloured bits deep inside the main fabric (fold-line tips) belong
    # to the main fabric
    stray = (out[..., 3] > 0) & (dists[..., 2] + 2.5 < dists[..., 3])
    out[stray, 2] = np.minimum(1.0, out[stray, 2] + out[stray, 3])
    out[stray, 3] = 0
    # line pixels next to the accent keep no accent weight below the midpoint
    # between line and fabric brightness: a light or warm accent target would
    # otherwise lift them into a rusty, speckled band
    acc = protos["accent"]
    mid = 0.5 * (float(o @ LUMA) + float(acc @ LUMA))
    dark_acc = (out[..., 3] > 0) & (lum < mid)
    out[dark_acc, 3] = 0
    return out


def weights_to_u8(wts):
    """Quantise weights so that the per-pixel sum never exceeds 255."""
    q = np.rint(np.clip(wts, 0, 1) * 255).astype(np.int32)
    s = q.sum(axis=-1)
    over = s > 255
    if over.any():
        f = 255.0 / s[over]
        q[over] = np.floor(q[over] * f[:, None]).astype(np.int32)
    return q.astype(np.float32)


def measure_references(poses, hair_zones, face_masks):
    """Median reference colours of confident pixels across all poses."""
    acc = {k: [] for k in CLASSES}
    for name, img in poses.items():
        rgb, a = img[..., :3], img[..., 3]
        solid = a > 200
        hz = hair_zones[name]
        face = face_masks[name]
        for k in CLASSES:
            d = np.linalg.norm(rgb - PROTO[k], axis=-1)
            m = solid & (d < 26)
            if k == "hair":
                m &= ndi.binary_erosion(hz, iterations=2)
            elif k == "skin":
                m &= ~hz
            else:
                m &= ~face
            m = ndi.binary_erosion(m, iterations=2)
            acc[k].append(rgb[m])
    ref = {}
    for k in CLASSES:
        px = np.concatenate(acc[k], axis=0)
        ref[k] = np.median(px, axis=0)
    ref["outline"] = PROTO["outline"]
    return ref
