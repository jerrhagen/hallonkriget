"""
Stigarna: 16 rutor, en för varje kombination av grannar som också är stig (1 norr, 2 öster,
4 söder, 8 väster). Bakgrunden är genomskinlig, så stigen läggs ovanpå terrängen. Bandet går
från mitten ut till de kanter där grannen är stig, och är lika brett vid kanten i alla rutor,
så att stigarna går ihop.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from svglib import Canvas, ellipse_points  # noqa: E402

TILE = 128
HALF = TILE / 2
WIDTH = 50  # bandets bredd vid kanten

DIRS = [(1, (0, -1)), (2, (1, 0)), (4, (0, 1)), (8, (-1, 0))]


def _arm(dx: int, dy: int, w: float) -> list[tuple[float, float]]:
    """En rektangel från mitten till kanten, lite bredare vid mitten."""
    nx, ny = -dy, dx
    end = (HALF + dx * (HALF + 6), HALF + dy * (HALF + 6))
    start = (HALF - dx * w * 0.5, HALF - dy * w * 0.5)
    hw = w / 2
    return [(start[0] + nx * hw, start[1] + ny * hw), (end[0] + nx * hw, end[1] + ny * hw),
            (end[0] - nx * hw, end[1] - ny * hw), (start[0] - nx * hw, start[1] - ny * hw)]


def draw(mask: int) -> Canvas:
    c = Canvas(TILE, TILE, seed=700 + mask, fx=0.6)
    arms = [(dx, dy) for bit, (dx, dy) in DIRS if mask & bit]
    # Raka bitar har ingen mittfläck, så att en lång stig inte ser ut som ett pärlband.
    straight = mask in (5, 10)
    center = ellipse_points(HALF, HALF, WIDTH * 0.52, WIDTH * 0.5, n=16)
    # Kanterna klipps vid rutan, så att stigen går ihop med grannrutans utan glipa.
    c.defs.append(f'<clipPath id="ruta"><rect width="{TILE}" height="{TILE}"/></clipPath>')
    c.raw('<g clip-path="url(#ruta)">')
    for dx, dy in arms:
        c.glaze(_arm(dx, dy, WIDTH + 8), "jord_mork", opacity=0.22, blur=4)
    if not straight:
        c.glaze(center, "jord_mork", opacity=0.22, blur=4)
    for dx, dy in arms:
        c.wash(_arm(dx, dy, WIDTH), "jord", opacity=0.8, wet=0.8, grain=1.4, solid=False, wobble_amp=0.8)
    if not straight:
        c.wash(center, "jord", opacity=0.8, wet=0.8, grain=1.4, solid=False)
    for dx, dy in arms:
        c.wash(_arm(dx, dy, WIDTH * 0.4), "halmgult_ljus", opacity=0.4, wet=1.0, grain=1.6, solid=False, wobble_amp=0.6)
    if not straight:
        c.wash(ellipse_points(HALF, HALF, WIDTH * 0.24, WIDTH * 0.22), "halmgult_ljus", opacity=0.3, solid=False)
    # Småsten och spår
    for _ in range(4 + 2 * len(arms)):
        a = c.rng.uniform(0, 2 * math.pi)
        r = c.rng.uniform(0, WIDTH * 0.4)
        x, y = HALF + math.cos(a) * r, HALF + math.sin(a) * r
        if arms and c.rng.random() < 0.6:
            dx, dy = c.rng.choice(arms)
            t = c.rng.uniform(0.2, 1.0) * HALF
            x, y = HALF + dx * t + dy * c.rng.uniform(-14, 14), HALF + dy * t + dx * c.rng.uniform(-14, 14)
        c.dot(x, y, c.rng.uniform(1.0, 2.2), color=c.rng.choice(["granit", "granit_mork", "jord_mork"]), opacity=0.7)
    c.raw("</g>")
    return c


def build() -> dict[str, str]:
    return {f"stig_{m:02d}": draw(m).to_svg() for m in range(16)}
