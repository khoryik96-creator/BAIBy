"""Split each pose into a hair-free Base and the original Curls overlay."""

from __future__ import annotations

import numpy as np
from scipy import ndimage as ndi

from common import SS, HeadFrame, HiResGrid, push_pull_fill
from regions import PROTO, LUMA

OUTLINE_W = 8.0       # px, matches the illustration's head outline
EAR_PATCH_PAD = 7     # px beyond an ear ellipse that is redrawn next to the hair
EAR_PATCH_NEAR = 6    # px: ear rim this close to the hair zone is redrawn
LINE_SOFT = 0.55      # gaussian sigma (px) softening new outlines like the painted art


def disk(r):
    y, x = np.ogrid[-r:r + 1, -r:r + 1]
    return x * x + y * y <= r * r


def local_maps(shape, frame: HeadFrame):
    h, w = shape
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float64)
    u, v = frame.to_local(xs + 0.5, ys + 0.5)
    r = frame.skull_radius(u, v)
    return u, v, r


def hair_zone(img, frame: HeadFrame, ears=None, protect=None, extra=None):
    """Boolean mask of everything that belongs to the original hair layer."""
    rgb, a = img[..., :3], img[..., 3]
    u, v, r = local_maps(a.shape, frame)
    region = (r < 1.75) & (v > frame.h.get("v_min", -0.6) * frame.bot)
    dh = np.linalg.norm(rgb - PROTO["hair"], axis=-1)
    core = (dh < 36) & (a > 128) & region
    core = ndi.binary_opening(core, structure=np.ones((3, 3)))
    lab, n = ndi.label(core, structure=np.ones((3, 3)))
    if n:
        sizes = ndi.sum(core, lab, range(1, n + 1))
        core = np.isin(lab, 1 + np.nonzero(sizes > 120)[0])
    # 1) the hair's own outline strokes
    z = ndi.binary_dilation(core, structure=disk(9)) & region
    # 2) thin strands, tips and hair shadow on the skin: grow through pixels that
    #    are not clean skin (but never along the dark outline any further)
    ds = np.linalg.norm(rgb - PROTO["skin"], axis=-1)
    lum = rgb @ LUMA
    clean_skin = (ds < 20) & (lum > 168)
    dark_line = lum < 92
    grow = (~clean_skin) & (a > 128) & region & (((~dark_line) & (r < 1.05)) | (r < 0.93))
    if protect is not None:
        grow &= ~protect
    zz = z.copy()
    for _ in range(26):
        zz = zz | (ndi.binary_dilation(zz) & grow)
    z = zz
    # 3) curl tips and outer outline beyond the skull (but never into ears)
    ear_guard = np.zeros(a.shape, bool)
    for e in ears or []:
        ear_guard |= ellipse_mask(a.shape, e[0], e[1], e[2] + 2, e[3] + 2, e[4])
    fabric = ((np.linalg.norm(rgb - PROTO["main"], axis=-1) < 40)
              | (np.linalg.norm(rgb - PROTO["accent"], axis=-1) < 40)) & (a > 128)
    fabric = ndi.binary_dilation(fabric, structure=disk(3))
    near_core = ndi.distance_transform_edt(~core) < 16
    outside = (r > 0.97) & (a > 8) & region & ~ear_guard & ~fabric & near_core
    for _ in range(16):
        z = z | (ndi.binary_dilation(z) & outside)
    # fill enclosed gaps (skin or background peeking between curls)
    z = ndi.binary_fill_holes(z) & region
    z = ndi.binary_dilation(z, structure=disk(1))
    if extra is not None:
        z |= extra
    return z, core


def ellipse_mask(shape, cx, cy, rx, ry, ang_deg):
    yy, xx = np.mgrid[0:shape[0], 0:shape[1]].astype(np.float64)
    a = np.radians(ang_deg)
    dx, dy = xx + 0.5 - cx, yy + 0.5 - cy
    u = dx * np.cos(a) + dy * np.sin(a)
    v = -dx * np.sin(a) + dy * np.cos(a)
    return (u / rx) ** 2 + (v / ry) ** 2 < 1


NBINS = 720
JOIN_BINS = 16      # 8 degrees


def phi_of(frame: HeadFrame, u, v):
    vn = np.where(v >= 0, v / frame.top, v / frame.bot)
    return np.mod(np.arctan2(vn, u), 2 * np.pi)


def skull_correction(img, frame: HeadFrame, z, ear_mask):
    """Per-angle radius correction so the skull meets the visible face outline.

    Along every ray the centre of the dark outline band near radius 1 is
    measured where the face is visible; across the hair-covered arcs the
    correction is interpolated smoothly, so the redrawn outline joins the
    original one without steps.
    """
    rgb, a = img[..., :3], img[..., 3]
    dark = (rgb @ LUMA < 105) & (a > 128)
    zd = ndi.binary_dilation(z, iterations=3)
    hidden = zd | ear_mask
    H, W = a.shape
    hair_bins = np.zeros(NBINS, bool)
    phis = (np.arange(NBINS) + 0.5) * 2 * np.pi / NBINS
    ss = np.linspace(0.78, 1.22, 133)
    vals = np.full(NBINS, np.nan)
    for i, ph in enumerate(phis):
        cu, cv = np.cos(ph), np.sin(ph)
        # point on the unit skull in this direction
        r1 = frame.skull_radius(cu, cv * (frame.top if cv >= 0 else frame.bot))
        u = cu / r1 * ss
        vn = cv / r1 * ss
        v = vn * (frame.top if cv >= 0 else frame.bot)
        x, y = frame.to_pose(u, v)
        xi = np.clip(np.rint(x - 0.5).astype(int), 0, W - 1)
        yi = np.clip(np.rint(y - 0.5).astype(int), 0, H - 1)
        if zd[yi[66], xi[66]]:
            hair_bins[i] = True
        if zd[yi[66], xi[66]]:
            continue
        if ear_mask[yi[66], xi[66]]:
            # an ear sits over the outline here: the face outline is the ear's
            # inner line (the innermost dark run), never the ear's outer rim
            d = dark[yi, xi] & ~zd[yi, xi] & (ss <= 1.06)
        else:
            d = dark[yi, xi] & ~hidden[yi, xi]
        if d.sum() < 4:
            continue
        # contiguous dark run closest to s=1
        idx = np.nonzero(d)[0]
        runs = np.split(idx, np.nonzero(np.diff(idx) > 1)[0] + 1)
        best = min(runs, key=lambda rr: abs(ss[rr].mean() - 1.0))
        if len(best) < 4:
            continue
        # outer edge of the band minus the stroke's inner extent -> skull radius
        vals[i] = ss[best[-1]] - 1.0
    ok = ~np.isnan(vals)
    # the art's face outline dips inwards where it used to meet the hair (a
    # concave corner at the temple); ignore the rays right next to the hidden
    # arcs so the new outline bridges smoothly from cheek to crown
    near_hidden = ndi.binary_dilation(~ok, iterations=JOIN_BINS) & ok
    ok &= ~near_hidden
    raw_ok = ~np.isnan(vals)
    if raw_ok.sum() >= 8:
        xs = np.nonzero(raw_ok)[0]
        measured = np.interp(np.arange(NBINS), np.concatenate([xs - NBINS, xs, xs + NBINS]),
                             np.tile(vals[raw_ok], 3))
        measured = ndi.median_filter(measured, size=5, mode="wrap")
    else:
        measured = np.zeros(NBINS)
    if ok.sum() < 8:
        return np.zeros(NBINS), measured, hair_bins
    # reject outliers, then periodic interpolation across hidden arcs
    med = ndi.median_filter(np.where(ok, vals, 0), size=9, mode="wrap")
    ok &= np.abs(vals - med) < 0.03
    xs = np.nonzero(ok)[0]
    xp = np.concatenate([xs - NBINS, xs, xs + NBINS])
    fp = np.tile(vals[ok], 3)
    full = np.interp(np.arange(NBINS), xp, fp)
    full = ndi.uniform_filter1d(full, 9, mode="wrap")
    return np.clip(full, -0.15, 0.15), np.clip(measured, -0.15, 0.15), hair_bins


def join_sector(hair_bins, pad=JOIN_BINS + 8, fade=14):
    """Per-bin weight of the arc where the head outline is redrawn.

    1 over the hair-covered arcs and ``pad`` bins beyond them (the temple
    corners), fading to 0 over ``fade`` bins where the new outline already lies
    on the art's own face outline, so the splice is seamless.
    """
    if not hair_bins.any():
        return np.zeros(NBINS)
    idx = np.nonzero(hair_bins)[0]
    b = np.arange(NBINS)
    d = np.abs(b[:, None] - idx[None, :])
    d = np.minimum(d, NBINS - d).min(axis=1)
    return np.clip(1 - (d - pad) / float(fade), 0, 1)


def skull_coverage(frame: HeadFrame, shape, scale=1.0, ss=4, corr=None):
    """Anti-aliased coverage of the skull (radius<scale) on the pose grid."""
    h, w = shape
    g = HiResGrid((0, 0, w, h), frame, ss)
    lim = scale
    if corr is not None:
        ph = phi_of(frame, g.U, g.V)
        lim = scale + corr[np.minimum((ph / (2 * np.pi) * NBINS).astype(int), NBINS - 1)]
    m = frame.skull_radius(g.U, g.V) < lim
    return g.down(m.astype(np.float32))


def ellipse_coverage(shape, cx, cy, rx, ry, ang_deg, ss=4):
    h, w = shape
    xs = (np.arange(w * ss) + 0.5) / ss
    ys = (np.arange(h * ss) + 0.5) / ss
    X, Y = np.meshgrid(xs, ys)
    a = np.radians(ang_deg)
    dx, dy = X - cx, Y - cy
    u = dx * np.cos(a) + dy * np.sin(a)
    v = -dx * np.sin(a) + dy * np.cos(a)
    m = (u / rx) ** 2 + (v / ry) ** 2 < 1
    return m.reshape(h, ss, w, ss).mean(axis=(1, 3))


def _ellipse_hi(g: HiResGrid, cx, cy, rx, ry, ang_deg):
    a = np.radians(ang_deg)
    dx, dy = g.X - cx, g.Y - cy
    u = dx * np.cos(a) + dy * np.sin(a)
    v = -dx * np.sin(a) + dy * np.cos(a)
    return (u / rx) ** 2 + (v / ry) ** 2 < 1


def _band_hi(g: HiResGrid, inside, width, keep=None):
    """Anti-aliased inner band (``width`` pose px) along the edge of a hi-res mask."""
    if not inside.any():
        return np.zeros((g.h, g.w))
    band = inside & (ndi.distance_transform_edt(inside) <= width * g.ss)
    if keep is not None:
        band &= keep
    return g.down(band.astype(np.float32)).astype(np.float64)


def stroke_from_coverage(cov, width):
    """Inner band of given width along the edge of an (AA) coverage mask."""
    inside = cov > 0.5
    d_in = ndi.distance_transform_edt(inside)
    d_out = ndi.distance_transform_edt(~inside)
    sd = np.where(inside, d_in - 0.5, -(d_out - 0.5))   # signed, + inside
    band = np.clip(width - sd + 0.5, 0, 1) * np.clip(sd + 0.5, 0, 1)
    return band


def build_base(img, frame: HeadFrame, h: dict, refs: dict):
    """Return (base RGBA float, hair zone mask, change mask)."""
    rgb, a = img[..., :3].astype(np.float64), img[..., 3].astype(np.float64)
    shape = a.shape
    protect = np.zeros(shape, bool)
    for k in h.get("protect", []):
        protect |= ellipse_mask(shape, *k)
    z, core = hair_zone(img, frame, h.get("ears", []), protect)
    z &= ~protect
    # erase patches (curl tips tucked behind an ear, ragged junctions): fully
    # re-synthesised, with a feathered edge so the patch never leaves a seam
    erase_soft = np.zeros(shape)
    for e in h.get("erase", []):
        erase_soft = np.maximum(erase_soft, ellipse_coverage(shape, *e))
    # the rim of every ear next to the hair zone is redrawn from its ellipse
    # too: the art's ear outline there was cut out of the hair layer (hard,
    # stepped edges, curl tips tucked behind the ear)
    near_hair = ndi.binary_dilation(z, structure=disk(EAR_PATCH_NEAR))
    for e in h.get("ears", []):
        rim = ellipse_mask(shape, e[0], e[1], e[2] + EAR_PATCH_PAD, e[3] + EAR_PATCH_PAD, e[4])
        rim &= ~ellipse_mask(shape, e[0], e[1], max(e[2] - 10, 1), max(e[3] - 10, 1), e[4])
        erase_soft = np.maximum(erase_soft, (rim & near_hair).astype(np.float64))
    if erase_soft.any():
        erase_soft = np.clip(ndi.gaussian_filter(erase_soft, 1.5) * 1.3, 0, 1) * ~protect
    z_hard = z.copy()
    z = z | (erase_soft > 0.5)

    ears = h.get("ears", [])
    ear_cov = np.zeros(shape)
    ear_mask = np.zeros(shape, bool)
    for e in ears:
        ear_cov = np.maximum(ear_cov, ellipse_coverage(shape, *e))
        ear_mask |= ellipse_mask(shape, e[0], e[1], e[2] + 8, e[3] + 8, e[4])
    bridge, measured, hair_bins = skull_correction(img, frame, z, ear_mask)
    # the redrawn outline uses the bridged radius over the hair-covered arcs and
    # glides onto the art's own (measured) outline before the splice, so the
    # join is tangential and lands exactly on the original line
    sw = join_sector(hair_bins, pad=JOIN_BINS + 2, fade=18)
    corr = sw * bridge + (1 - sw) * measured
    redraw = join_sector(hair_bins, pad=JOIN_BINS + 2 + 18 + 6, fade=4)
    # every new shape is rasterised on a supersampled grid, so the rebuilt
    # outline is anti-aliased on both edges (no stair-steps where it meets
    # the background or the skin)
    hi = HiResGrid((0, 0, shape[1], shape[0]), frame, SS)
    phh = phi_of(frame, hi.U, hi.V)
    lim = 1.0 + corr[np.minimum((phh / (2 * np.pi) * NBINS).astype(int), NBINS - 1)]
    head_hi = frame.skull_radius(hi.U, hi.V) < lim
    del phh, lim
    ear_hi = np.zeros(head_hi.shape, bool)
    for e in ears:
        ear_hi |= _ellipse_hi(hi, *e)
    body_hi = np.zeros(head_hi.shape, bool)
    for e in h.get("body", []):
        body_hi |= _ellipse_hi(hi, *e)
    skull = hi.down(head_hi.astype(np.float32)).astype(np.float64)
    head_cov = skull.copy()
    ear_cov = np.maximum(ear_cov, hi.down(ear_hi.astype(np.float32)))
    # body patches: fabric hidden under the hair (e.g. the crawler's back)
    body_cov = hi.down(body_hi.astype(np.float32)).astype(np.float64)

    # Redraw the whole upper head outline, not only the hair-covered arcs: the
    # new outline follows the measured face outline (per-angle correction), so
    # splicing it in along the full crown removes stair-steps, leftover curl
    # tips and ragged joins where the hair zone used to end.  The ring fades in
    # over a short arc above ``ring_v`` (head units) so the join is tangential.
    u, v, r = local_maps(shape, frame)
    ph = phi_of(frame, u, v)
    bins = np.minimum((ph / (2 * np.pi) * NBINS).astype(int), NBINS - 1)
    rr = r - corr[bins]
    ring_w = redraw[bins]
    inner = 1.0 - (OUTLINE_W + 2.5) / frame.rx
    guard = ear_mask | (body_cov > 0) | protect
    ring = (rr > inner) & (rr < 1.55) & ~guard & (ring_w > 0)
    # and wherever the crown is visible, nothing may stick out past the outline
    # (leftover curl tips and specks)
    outside = (rr > 1.0 + 3.0 / frame.rx) & (rr < 1.55) & (v > 0) & ~guard & (ring_w > 0)
    zr = z | ring | outside
    zsoft = np.maximum(np.maximum(z_hard.astype(np.float64), ring * ring_w), outside)
    zsoft = np.maximum(zsoft, erase_soft)

    # stubs of the art's own hair-edge line left inside the face next to the
    # new outline (the temple notch): dark bits touching the outline band from
    # the inside are painted over with skin
    lum0 = rgb @ LUMA
    band_in = stroke_from_coverage(head_cov, OUTLINE_W) > 0.3
    dark_in = ((lum0 < 120) & (a > 128) & (head_cov > 0.5) & ~band_in & (ring_w > 0)
               & (rr > 1.0 - (OUTLINE_W + 22.0) / frame.rx) & ~guard & ~z)
    lab, n = ndi.label(dark_in, structure=np.ones((3, 3)))
    if n:
        touch = np.unique(lab[ndi.binary_dilation(band_in, iterations=2) & dark_in])
        touch = touch[touch > 0]
        sizes = ndi.sum(dark_in, lab, touch) if len(touch) else []
        stubs = np.isin(lab, [t for t, sz in zip(touch, sizes) if sz < 500])
        stubs = ndi.binary_dilation(stubs, iterations=2) & (head_cov > 0.5) & ~guard
        zr = zr | stubs
        zsoft = np.maximum(zsoft, stubs.astype(np.float64))

    # skin fill: smooth continuation of the surrounding clean skin
    ds = np.linalg.norm(rgb - refs["skin"], axis=-1)
    lum = rgb @ LUMA
    known = (~zr) & (a > 200) & (ds < 22) & (lum > 165)
    known = ndi.binary_erosion(known, iterations=2)
    # this pose's own flat skin tone (rest poses are slightly lighter)
    ref = np.median(rgb[known & (skull > 0.99)], axis=0)
    lum_ref = float(ref @ LUMA)
    known &= np.abs(lum - lum_ref) < 7
    fill = push_pull_fill(rgb, known)
    # keep the fill inside the cel-flat skin band (no hair shadow creeping in)
    fill = np.clip(fill, ref - 5, ref + 5)

    outline_col = refs["outline"]
    # outline strokes: the head silhouette and the ear edges (ears sit behind)
    head_line = _band_hi(hi, head_hi, OUTLINE_W)
    ear_line = _band_hi(hi, ear_hi, OUTLINE_W * 0.85, ~head_hi)
    body_line = _band_hi(hi, body_hi, OUTLINE_W * 0.7, ~head_hi & ~ear_hi) if body_hi.any() else np.zeros(shape)
    del head_hi, ear_hi, body_hi, hi
    line = np.clip(head_line + ear_line, 0, 1)
    if body_cov.any():
        main = (np.linalg.norm(rgb - PROTO["main"], axis=-1) < 30) & ~z & (a > 200)
        fab = push_pull_fill(rgb, main)
        bc = (body_cov * (1 - head_cov) * (1 - ear_cov))[..., None]
        fill = fill * (1 - bc) + fab * bc

    new_rgb = fill * (1 - line[..., None]) + outline_col * line[..., None]
    if body_cov.any():
        # the outfit's own (softer) outline colour, sampled along the visible body edge
        ring = (stroke_from_coverage(body_cov, OUTLINE_W) > 0.5) & ~z & (a > 200) & (lum < 150)
        bcol = np.median(rgb[ring], axis=0) if ring.sum() > 20 else outline_col
        new_rgb = new_rgb * (1 - body_line[..., None]) + bcol * body_line[..., None]
    # keep the art's own clean skin (and its subtle shading) where it shows
    clean = (ds < 20) & (np.abs(lum - lum_ref) < 7) & (a > 200)
    keep_orig = clean & (head_cov > 0.999) & (ndi.maximum_filter(line, size=5) < 0.01)
    # feather: the art's own skin fades in over a few pixels (no seams where the
    # smooth fill meets the original shading)
    kw = np.clip(ndi.distance_transform_edt(keep_orig) / 5.0, 0, 1)[..., None]
    new_rgb = new_rgb * (1 - kw) + rgb * kw
    solid_a = float(np.median(a[a > 200]))
    sil = np.maximum(np.maximum(head_cov, ear_cov), body_cov)
    new_a = sil * solid_a

    base = img.astype(np.float64).copy()
    zf = zsoft
    base_rgb = rgb * (1 - zf[..., None]) + new_rgb * zf[..., None]
    base_a = a * (1 - zf) + new_a * zf
    # drop specks (curl tips) that are no longer attached to the body
    solid = base_a > 24
    lab, n = ndi.label(solid, structure=np.ones((3, 3)))
    if n > 1:
        sizes = ndi.sum(solid, lab, range(1, n + 1))
        keep = np.isin(lab, 1 + np.nonzero(sizes >= 0.02 * sizes.max())[0])
        keep = ndi.binary_dilation(keep, iterations=2)
        near_hair = ndi.binary_dilation(zr, iterations=24)
        base_a = np.where(keep | ~near_hair, base_a, 0)
    # never more opaque than the original inside the hair zone, so the original
    # curls layer composited over Base reproduces the illustration
    # (except where the redrawn skull bridges the temple notch the hair used to
    # hide: there the new outline must exist even though the art had background,
    # which costs Base+Curls a few hundred anti-aliased pixels, see check.py)
    # (the rebuilt ear rims too: clipping them to the art's alpha left hard
    # steps wherever a gap between curl tips used to show the background)
    bridge = (sil > 0) & ~protect
    base_a = np.where(zr & ~bridge, np.minimum(base_a, a), base_a)
    # no pinholes inside the head: enclosed (semi-)transparent pixels of the
    # rebuilt silhouette are filled with the rebuilt skin / outline
    solid = base_a > 200
    holes = ndi.binary_fill_holes(solid) & ~solid & (sil > 0.5)
    base_rgb[holes] = new_rgb[holes]
    base_a[holes] = new_a[holes]
    # isolated dark specks (strand tips the hair zone missed) on the bald
    # forehead: small non-skin bits right next to the hair zone become skin
    interior = (head_cov > 0.999) & (ndi.maximum_filter(line, size=7) < 0.01)
    lumb = base_rgb @ LUMA
    off = interior & (np.abs(lumb - lum_ref) > 10) & (base_a > 200)
    lab, n = ndi.label(off, structure=np.ones((3, 3)))
    if n:
        sizes = ndi.sum(off, lab, range(1, n + 1))
        near = ndi.binary_dilation(zr, iterations=14)
        ids = [i + 1 for i in range(n) if sizes[i] <= 40]
        speck = np.isin(lab, ids) & near
        speck = ndi.binary_dilation(speck, iterations=1) & interior
        base_rgb[speck] = fill[speck]
    base[..., :3] = base_rgb
    base[..., 3] = base_a
    base[..., :3][base[..., 3] < 0.5] = 0
    return base, z, core
