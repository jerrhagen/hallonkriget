"""
En upptrampad gårdsstig för stilprovet: från vänster, förbi hönshusets trappa och vidare åt höger.

Riktiga stigar blir 16 rutvarianter i fas 2. Det här är en enda dekal, för att pröva hur
en stig ser ut i stilen. Stigens mittlinje skrivs till stig_prov.json så att pysslingarna
i Godot går på den.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from svglib import Canvas, Noise1D, densify, ellipse_points, smooth_path, wobble  # noqa: E402

# Dekalens övre vänstra hörn i världen (1×-pixlar) och storlek
ORIGIN = (560, 900)
SIZE = (2800, 720)

# Mittlinjen i världskoordinater. Punkt 3 är foten av hönstrappan.
CENTER_WORLD = [(620, 1520), (900, 1400), (1200, 1180), (1552, 1034), (1900, 1120), (2300, 1220), (2750, 1150),
                (3300, 1260)]
RAMP_INDEX = 3


def _local(p):
    return (p[0] - ORIGIN[0], p[1] - ORIGIN[1])


def centerline() -> list[tuple[float, float]]:
    """Mittlinjen, utjämnad och tätt samplad, i dekalens koordinater."""
    pts = [_local(p) for p in CENTER_WORLD]
    # Catmull-Rom-sampling
    out = []
    for i in range(len(pts) - 1):
        p0 = pts[max(i - 1, 0)]
        p1, p2 = pts[i], pts[i + 1]
        p3 = pts[min(i + 2, len(pts) - 1)]
        for k in range(24):
            t = k / 24
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(2)))
    out.append(pts[-1])
    return out


def _band(c: Canvas, line, width: float, var: float) -> tuple[list, list]:
    noise = Noise1D(c.rng)
    left, right = [], []
    for i, p in enumerate(line):
        a = line[max(i - 1, 0)]
        b = line[min(i + 1, len(line) - 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(tx, ty) or 1
        nx, ny = -ty / ln, tx / ln
        w = width * (1 + var * noise(i * 0.15))
        left.append((p[0] + nx * w / 2, p[1] + ny * w / 2))
        right.append((p[0] - nx * w / 2, p[1] - ny * w / 2))
    return left, right


def draw() -> Canvas:
    c = Canvas(SIZE[0], SIZE[1], seed=58, fx=1.0)
    line = centerline()

    left, right = _band(c, line, 78, 0.35)
    c.glaze(left + right[::-1], "jord_mork", opacity=0.25, blur=6)
    c.wash(left + right[::-1], "jord", opacity=0.75, wet=1.6, grain=1.6, solid=False)
    # Den upptrampade mitten är ljusare
    l2, r2 = _band(c, line, 34, 0.5)
    c.wash(l2 + r2[::-1], "halmgult_ljus", opacity=0.45, wet=1.4, grain=1.8, solid=False)
    # Brutna bläcklinjer längs kanterna, inte hela vägen
    for edge in (left, right):
        i = 0
        while i < len(edge) - 6:
            n = c.rng.randint(4, 12)
            if c.rng.random() < 0.6:
                c.ink(edge[i:i + n], width=1.1, opacity=0.6, amp=1.0)
            i += n + c.rng.randint(2, 8)
    # Gräs som växer in över kanten, stenar och spår
    for edge in (left, right):
        for i in range(0, len(edge), 3):
            if c.rng.random() < 0.55:
                x, y = edge[i]
                for k in range(c.rng.randint(2, 4)):
                    a = -math.pi / 2 + c.rng.uniform(-0.6, 0.6)
                    ln = c.rng.uniform(5, 10)
                    c.ink([(x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln)], width=0.9, color="mossgront_mork",
                          opacity=0.6, amp=0.3)
    for _ in range(40):
        i = c.rng.randrange(len(line))
        x, y = line[i]
        x += c.rng.uniform(-30, 30)
        y += c.rng.uniform(-22, 22)
        r = c.rng.uniform(2, 5)
        c.shape(ellipse_points(x, y, r, r * 0.7), c.rng.choice(["granit", "granit_mork"]), ink_width=0.7, wet=0.4)
    for _ in range(30):
        i = c.rng.randrange(len(line))
        x, y = line[i]
        x += c.rng.uniform(-20, 20)
        y += c.rng.uniform(-14, 14)
        c.ink([(x, y), (x + c.rng.uniform(4, 9), y + c.rng.uniform(-1, 1))], width=0.8, color="jord_mork", opacity=0.5)
    return c


def build() -> dict[str, str]:
    return {"stig_prov": draw().to_svg()}


def meta() -> dict:
    """Stigens mittlinje i världskoordinater, tätt samplad, och var hönstrappan är."""
    pts = [[round(x + ORIGIN[0], 1), round(y + ORIGIN[1], 1)] for x, y in centerline()]
    return {"origin": list(ORIGIN), "size": list(SIZE), "points": pts, "ramp_index": RAMP_INDEX * 24}
