"""Hand-tuned head geometry for every pose illustration.

Coordinates are in pose pixel space (x right, y DOWN, PIL convention).
Each head is a "skull" made of two half-ellipses sharing the horizontal
semi-axis ``rx``: ``ry_top`` above the equator and ``ry_bot`` below it, in a
head-local frame rotated by ``tilt`` degrees (positive = top of head leans to
the viewer's right, i.e. clockwise on screen).

``ears`` are extra skin ellipses (cx, cy, rx, ry, angle) that are re-drawn in
the hair-free Base where hair used to cover them.
``erase`` are ellipses (cx, cy, rx, ry, angle) forced into the hair zone
(stray curl tips tucked behind an ear).
``body`` are fabric ellipses re-drawn where hair hid the outfit outline.
``hat_shift`` nudges beanie/bucket hat back on the head (head units) where the
head nearly touches the image edge.
``protect`` are ellipses (cx, cy, rx, ry, angle) protected from hair removal (eyebrows,
eyes near the hairline).
"""

HEADS = {
    "Newborn":     dict(cx=239.7, cy=239.9, rx=144.1, ry_top=184, ry_bot=96.2, tilt=-21.8, n_top=2.3,
                        ears=[(113, 311, 29, 33, 30)]),
    "NewbornRest": dict(cx=243.9, cy=243.1, rx=141.9, ry_top=180, ry_bot=92.0, tilt=-23.1, n_top=2.4,
                        ears=[(110, 314, 29, 33, 30)]),
    "Sitter":      dict(cx=216.1, cy=262.1, rx=143.0, ry_top=192, ry_bot=89.3, tilt=-5.9, n_top=2.4,
                        ears=[(80, 297, 25, 33, 20), (363, 253, 22, 32, -8)]),
    "SitterRest":  dict(cx=221.2, cy=271.5, rx=143.2, ry_top=191, ry_bot=90.0, tilt=-9.9, n_top=2.4,
                        ears=[(86, 312, 25, 33, 20), (366, 264, 20, 32, 10)]),
    "Crawler":     dict(cx=369.9, cy=261.1, rx=135.0, ry_top=190, ry_bot=94.6, tilt=-16.2, n_top=2.4,
                        ears=[(221, 302, 27, 33, 20)], body=[(120, 402, 130, 95, -10)], hat_shift=-0.12),
    "CrawlerRest": dict(cx=343.2, cy=213.5, rx=139.1, ry_top=185, ry_bot=96.1, tilt=30.8, n_top=2.3,
                        ears=[(443, 314, 31, 29, -10)], erase=[(486, 312, 16, 9, 30), (486, 320, 12, 9, 20), (477, 314, 7, 14, 0)]),
    "Walker":      dict(cx=240.9, cy=238.9, rx=137.0, ry_top=188, ry_bot=97.7, tilt=-17.9, n_top=2.5,
                        ears=[(100, 292, 27, 33, 25)], erase=[(62, 287, 15, 8, -20), (58, 293, 12, 8, -20)]),
    "WalkerRest":  dict(cx=399.4, cy=211.6, rx=135.9, ry_top=185, ry_bot=96.6, tilt=34.3, n_top=2.4,
                        ears=[(490, 320, 29, 29, -10)], erase=[(525, 322, 16, 9, 30), (518, 328, 12, 9, 20)]),
}
