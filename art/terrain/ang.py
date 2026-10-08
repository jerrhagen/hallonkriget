"""
Äng: gräs med blommor, i fyra varianter. Varje ruta är 128×128 och går att lägga kant i kant
åt alla håll. Varianterna skiljer sig i hur mycket det blommar.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from svglib import Canvas, ellipse_points, rect_points, rotate  # noqa: E402

TILE = 128
VARIANTS = 4


def _tuft(c: Canvas, x: float, y: float, size: float, color: str) -> None:
    """En grästuva: några strån som spretar uppåt från samma punkt."""
    n = c.rng.randint(3, 6)
    for i in range(n):
        a = -math.pi / 2 + (i - (n - 1) / 2) * 0.32 + c.rng.uniform(-0.15, 0.15)
        ln = size * c.rng.uniform(0.6, 1.0)
        bend = c.rng.uniform(-2, 2)
        tip = (x + math.cos(a) * ln + bend, y + math.sin(a) * ln)
        mid = (x + math.cos(a) * ln * 0.5 + bend * 0.3, y + math.sin(a) * ln * 0.5)
        c.ink([(x, y), mid, tip], width=0.9, color=color, opacity=0.45, amp=0.3)


def _prastkrage(c: Canvas, x: float, y: float) -> None:
    c.line((x, y + 2), (x + c.rng.uniform(-1, 1), y + 9), width=0.6, color="mossgront_mork")
    for i in range(9):
        a = i / 9 * 2 * math.pi + c.rng.uniform(-0.1, 0.1)
        petal = rotate(ellipse_points(x + 2.6, y, 2.0, 0.85, n=8), x, y, a)
        c.wash(petal, "prastkrage", wet=0.2, grain=0.3, offset=0.2)
    c.wash(ellipse_points(x, y, 1.4, 1.4, n=8), "smorblomma", wet=0.2, grain=0.3, offset=0.2)
    c.ink(ellipse_points(x, y, 4.4, 4.2, n=12), closed=True, width=0.35, opacity=0.35)


def _smorblomma(c: Canvas, x: float, y: float) -> None:
    c.line((x, y + 1), (x, y + 7), width=0.5, color="mossgront_mork")
    c.wash(ellipse_points(x, y, 2.2, 1.9, n=8), "smorblomma", wet=0.25, grain=0.3, offset=0.2)
    c.dot(x, y, 0.5, color="blck", opacity=0.4)


def _blaklocka(c: Canvas, x: float, y: float) -> None:
    c.ink([(x, y + 8), (x + 1, y + 2), (x + 3, y - 1)], width=0.5, color="mossgront_mork", opacity=0.8)
    bell = [(x + 1.5, y - 1), (x + 5, y - 2.5), (x + 6, y + 1.5), (x + 4.5, y + 3.5), (x + 2.5, y + 2.5)]
    c.wash(bell, "blaklocka", wet=0.25, grain=0.4, offset=0.2)
    c.ink(bell, closed=True, width=0.4, opacity=0.6)


def _klover(c: Canvas, x: float, y: float) -> None:
    for a in (0, 2.1, 4.2):
        leaf = ellipse_points(x + math.cos(a) * 2.2, y + 3 + math.sin(a) * 1.4, 1.8, 1.4, n=8)
        c.wash(leaf, "mossgront", wet=0.2, grain=0.4, offset=0.2)
    c.wash(ellipse_points(x, y, 2.4, 2.6, n=10), "klover", wet=0.25, grain=0.6, offset=0.2)
    c.hatch(ellipse_points(x, y, 2.4, 2.6, n=10), spacing=0.9, angle=0.8, width=0.3, opacity=0.4)


FLOWERS = [(_prastkrage, 3), (_smorblomma, 4), (_blaklocka, 2), (_klover, 2)]


def _base(c: Canvas) -> None:
    """Grunden, gemensam för alla varianter och periodisk, så att alla varianter passar ihop."""
    for _ in range(8):
        x, y = c.rng.uniform(0, TILE), c.rng.uniform(0, TILE)
        color = c.rng.choice(["ang_ljus", "ang_mork", "ang_ljus", "halmgult_ljus"])
        c.glaze(ellipse_points(x, y, c.rng.uniform(14, 34), c.rng.uniform(10, 24)), color,
                opacity=c.rng.uniform(0.22, 0.4), blur=8)
    for _ in range(4):
        x, y = c.rng.uniform(0, TILE), c.rng.uniform(0, TILE)
        c.wash(ellipse_points(x, y, c.rng.uniform(12, 24), c.rng.uniform(8, 14)),
               c.rng.choice(["ang", "ang_ljus"]), opacity=0.3, wet=1.5, grain=1.5, solid=False)
    for _ in range(20):
        _tuft(c, c.rng.uniform(0, TILE), c.rng.uniform(0, TILE), c.rng.uniform(5, 9),
              c.rng.choice(["ang_mork", "mossgront_mork", "mossgront_mork"]))


def draw(variant: int) -> Canvas:
    c = Canvas(TILE, TILE, seed=1000, fx=0.6)
    c.raw(f'<rect width="{TILE}" height="{TILE}" fill="#a9b46d"/>')
    with c.tiled(TILE, TILE):
        _base(c)
    # Det som skiljer varianterna ligger helt inne i rutan och korsar aldrig kanten.
    c.rng.seed(2000 + variant)
    margin = 10

    def inside() -> tuple[float, float]:
        return c.rng.uniform(margin, TILE - margin), c.rng.uniform(margin, TILE - margin)

    for _ in range(2 + variant):
        x, y = inside()
        c.glaze(ellipse_points(x, y, c.rng.uniform(5, 9), c.rng.uniform(4, 7)),
                c.rng.choice(["ang_ljus", "halmgult_ljus"]), opacity=0.35, blur=3)
    for _ in range(8):
        x, y = inside()
        _tuft(c, x, y, c.rng.uniform(6, 9), c.rng.choice(["ang_mork", "mossgront_mork"]))
    flowers = [0, 4, 8, 13][variant % VARIANTS]
    weighted = [f for f, w in FLOWERS for _ in range(w)]
    for _ in range(flowers):
        c.rng.choice(weighted)(c, *inside())
    return c


def build() -> dict[str, str]:
    return {f"ang_{v}": draw(v).to_svg() for v in range(VARIANTS)}


if __name__ == "__main__":
    import resvg_py
    from PIL import Image
    import io

    # Förhandsvisning: 4×3 rutor med slumpade varianter, för att se skarvarna.
    tiles = [Image.open(io.BytesIO(bytes(resvg_py.svg_to_bytes(svg_string=draw(v).to_svg())))) for v in range(VARIANTS)]
    sheet = Image.new("RGBA", (TILE * 4, TILE * 3))
    import random
    r = random.Random(3)
    for ty in range(3):
        for tx in range(4):
            sheet.paste(tiles[r.randrange(VARIANTS)], (tx * TILE, ty * TILE))
    sheet.save(sys.argv[1] if len(sys.argv) > 1 else "ang.png")
