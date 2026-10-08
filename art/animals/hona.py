"""Hönan, som småfigur i byggnader och senare som klippdocka."""
from __future__ import annotations

from svglib import Canvas, ellipse_points, rotate

# Hönan i profil åt höger, fötterna vid (0, 0). Kropp, stjärt, hals och huvud i en kontur.
BODY = [(-9, -6), (-11, -11), (-14, -19), (-11.5, -20.5), (-7, -15), (-1, -14.5), (4, -15.5),
        (6, -19), (8.5, -21.5), (11.5, -20.5), (12.5, -17), (11, -13), (10, -8), (6.5, -3.5), (0, -2),
        (-5.5, -2.8)]
COMB = [(7.2, -21), (7.4, -24.2), (8.8, -22.8), (9.8, -25.2), (11, -22.6), (12.3, -23.4), (12.2, -20.5)]
WATTLE = [(11.6, -16.2), (12.6, -12.8), (10.6, -14)]
BEAK = [(12.3, -19.2), (15.6, -17.8), (12.4, -16.6)]
WING = [(-6, -10), (-1, -11.5), (4, -9.5), (3, -6.5), (-3, -6)]
EYE = (10.2, -18.6)


def draw_hen(c: Canvas, x: float, y: float, scale: float = 1.3, facing: int = 1,
             color: str = "hona_vit", pecking: bool = False) -> None:
    """En höna som står på marken vid (x, y). facing 1 = höger, -1 = vänster."""
    s, f = scale, facing
    angle = 0.62 if pecking else 0.0

    def place(pts):
        pts = rotate(pts, 0, -3, angle)
        return [(x + px * s * f, y + py * s) for px, py in pts]

    c.glaze(ellipse_points(x, y + 0.5 * s, 10 * s, 2.4 * s), "skugga", opacity=0.4, blur=1.2)
    for lx in (-1.5, 2.5):
        a, b = place([(lx, -3)])[0], (x + (lx + 0.5) * s * f, y)
        c.line(a, b, width=0.9 * s, color="nabb")
        c.line(b, (b[0] + 3 * s * f, b[1] + 0.3), width=0.7 * s, color="nabb")

    c.wash(place(COMB), "kam", wet=0.35, grain=0.6)
    c.wash(place(WATTLE), "kam", wet=0.3, grain=0.6)
    c.wash(place(BODY), color, wet=0.6, grain=0.7)
    c.glaze(place([(-9, -6), (-1, -5), (6, -4.5), (0, -2), (-6, -3)]), "skugga", opacity=0.25, blur=1.5)
    c.ink(place(BODY), closed=True, width=0.85 * s)
    c.ink(place(WING), width=0.6 * s, opacity=0.75)
    c.ink(place(COMB), closed=True, width=0.5 * s, opacity=0.6)
    c.wash(place(BEAK), "nabb", wet=0.25, grain=0.5)
    c.ink(place(BEAK), closed=True, width=0.6 * s)
    ex, ey = place([EYE])[0]
    c.dot(ex, ey, 0.8 * s)
