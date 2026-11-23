#!/usr/bin/env python3
"""Developer helper: refine the hand-placed skull curves in heads.py.

The skull outline is only visible where the face is not covered by hair; it
should sit on the centre of the illustration's own dark outline there, and it
must stay inside the original silhouette everywhere else (Base + Curls has to
reproduce the original).  This script nudges cx, cy, rx, ry_bot, tilt and
n_top to satisfy that and prints suggested values.  It never edits heads.py.

    python3 tools/appearance/fit_heads.py [Pose ...]
"""

from __future__ import annotations

import math
import sys

import numpy as np
from scipy import ndimage as ndi
from scipy.optimize import minimize

from common import POSES, HeadFrame, load_pose
from heads import HEADS
from regions import LUMA
from separate import hair_zone, ellipse_mask


def fit(name):
    img = load_pose(name)
    rgb, a = img[..., :3], img[..., 3]
    h0 = dict(HEADS[name])
    fr0 = HeadFrame(h0)
    z, _ = hair_zone(img, fr0, h0.get("ears", []))
    ears = np.zeros(a.shape, bool)
    for e in h0.get("ears", []):
        ears |= ellipse_mask(a.shape, e[0], e[1], e[2] + 6, e[3] + 6, e[4])
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]].astype(float)
    u, v = fr0.to_local(xx + .5, yy + .5)
    r0 = fr0.skull_radius(u, v)
    dark = (rgb @ LUMA < 100) & (a > 128) & (r0 > 0.85) & (r0 < 1.15) & ~z & ~ears
    # outline centre-line score
    score = ndi.gaussian_filter(dark.astype(float), 2.0)
    zz = ndi.binary_dilation(z, iterations=2) | ears
    inside = ndi.binary_erosion(a > 200, iterations=3)
    th = np.radians(np.arange(0, 360, 1.5))
    keys = ["cx", "cy", "rx", "ry_bot", "tilt", "n_top"]
    q0 = np.array([h0[k] if k != "n_top" else h0.get("n_top", 2.0) for k in keys], float)

    def cost(q):
        h = dict(h0)
        h.update({k: q[i] for i, k in enumerate(keys)})
        fr = HeadFrame(h)
        pts = np.array([fr.to_pose(*fr.skull_point(t)) for t in th])
        xi = np.clip(np.rint(pts[:, 0]).astype(int), 0, a.shape[1] - 1)
        yi = np.clip(np.rint(pts[:, 1]).astype(int), 0, a.shape[0] - 1)
        vis = ~zz[yi, xi]
        c = 0.0
        if vis.sum() > 3:
            c -= score[yi, xi][vis].mean() * 4.0
        hidden = ~vis
        if hidden.any():
            c += (~inside[yi, xi][hidden]).mean() * 6.0
        c += 0.002 * np.sum(((q - q0) / np.array([6, 6, 6, 6, 4, 0.3])) ** 2)
        return c

    best = minimize(cost, q0, method="Nelder-Mead",
                    options=dict(xatol=0.3, fatol=1e-5, maxiter=4000))
    out = {k: round(float(best.x[i]), 1) for i, k in enumerate(keys)}
    return out, cost(q0), best.fun


if __name__ == "__main__":
    for p in sys.argv[1:] or POSES:
        out, c0, c1 = fit(p)
        print(p, out, f"cost {c0:.3f} -> {c1:.3f}")
