"""Skin normalisation of the rest poses.

The four *Rest illustrations were painted with a slightly lighter, pinker skin
(median ~(237,168,121)) than the awake poses (~(230,160,107)).  The recolour
uses one global skin reference, so light skin targets amplified that gap into a
visible complexion flicker on sleep/wake.  Before anything else is built, the
skin-coloured pixels of every rest pose are scaled (in proportion to their skin
weight) so their skin median matches the awake poses.  ``check.py`` compares
Base + Curls against these normalised illustrations.
"""

from __future__ import annotations

from functools import lru_cache

import numpy as np
from scipy import ndimage as ndi

from common import POSES, load_pose
from regions import LUMA, PROTO, soft_weights


def _clean_skin(img):
    rgb, a = img[..., :3], img[..., 3]
    ds = np.linalg.norm(rgb - PROTO["skin"], axis=-1)
    m = (a > 200) & (ds < 20) & (rgb @ LUMA > 165)
    return ndi.binary_erosion(m, iterations=2)


def skin_median(img):
    return np.median(img[..., :3][_clean_skin(img)], axis=0)


@lru_cache(maxsize=None)
def factors():
    awake = [skin_median(load_pose(p)) for p in POSES if not p.endswith("Rest")]
    target = np.median(np.array(awake), axis=0)
    out = {}
    for p in POSES:
        out[p] = np.ones(3) if not p.endswith("Rest") else target / skin_median(load_pose(p))
    return out


def skin_weight(img):
    """Soft skin weight used for the normalisation (skin-coloured pixels only)."""
    rgb, a = img[..., :3], img[..., 3]
    skinish = np.linalg.norm(rgb - PROTO["skin"], axis=-1) < 48
    none = np.zeros(a.shape, bool)
    allow = {"skin": skinish, "hair": none, "main": none, "accent": none}
    return soft_weights(rgb, a, PROTO, allow)[..., 0]


def normalized_pose(name):
    img = load_pose(name)
    f = factors()[name]
    if np.allclose(f, 1.0):
        return img
    w = skin_weight(img)[..., None]
    out = img.copy()
    out[..., :3] = np.clip(img[..., :3] * (1 + w * (f - 1)), 0, 255)
    return np.rint(out).astype(np.float32)
