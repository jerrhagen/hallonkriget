"""
Kartans terräng i fas 2: en ruta på 128×128 per terräng och variant, i samma ordning som
Terrain i sim/Map/Terrain.cs. Glänta och äng är ängens rutor (glänta med få blommor, äng med
många). De andra är gräset med terrängen ovanpå, ritad innanför rutans kant så att rutorna går
att lägga kant i kant. Vatten, landsväg, myr och åker täcker hela rutan.

Övergångarna mellan terränger är raka tills vidare; de kommer med den riktiga grafiken.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from svglib import Canvas, ellipse_points, rect_points  # noqa: E402
from terrain import ang  # noqa: E402

TILE = 128
VARIANTS = 2
# Samma ordning som enum Terrain.
TERRAINS = ["glanta", "stenig", "skog", "ang", "myr", "vatten", "skrot", "hallonsnar", "plommon", "landsvag", "aker"]


def _grass(seed: int) -> Canvas:
    c = Canvas(TILE, TILE, seed=1000, fx=0.6)
    c.raw(f'<rect width="{TILE}" height="{TILE}" fill="#a9b46d"/>')
    with c.tiled(TILE, TILE):
        ang._base(c)
    c.rng.seed(seed)
    return c


def _full(color: str, base: str, seed: int) -> Canvas:
    """En terräng som täcker hela rutan, periodisk så att den går ihop åt alla håll."""
    c = Canvas(TILE, TILE, seed=seed, fx=0.6)
    c.raw(f'<rect width="{TILE}" height="{TILE}" fill="{base}"/>')
    with c.tiled(TILE, TILE):
        for _ in range(7):
            x, y = c.rng.uniform(0, TILE), c.rng.uniform(0, TILE)
            c.glaze(ellipse_points(x, y, c.rng.uniform(16, 34), c.rng.uniform(10, 22)), color,
                    opacity=c.rng.uniform(0.15, 0.3), blur=7)
    return c


def _spruce(c: Canvas, x: float, y: float, h: float) -> None:
    """En gran snett ovanifrån: en mörk kon med en skugga åt höger."""
    w = h * 0.42
    c.glaze(ellipse_points(x + w * 0.5, y + 2, w * 0.9, w * 0.35), "skugga", opacity=0.3, blur=2)
    tiers = 3
    for i in range(tiers):
        top = y - h + i * h * 0.28
        bottom = y - h * 0.15 * (tiers - 1 - i)
        half = w * (0.55 + 0.25 * i)
        pts = [(x, top), (x + half, bottom), (x + half * 0.2, bottom - 2), (x - half * 0.2, bottom - 2), (x - half, bottom)]
        c.wash(pts, "gran" if i % 2 == 0 else "gran_mork", wet=0.5, grain=0.6, offset=0.4)
        c.ink(pts, closed=True, width=0.8, opacity=0.75)
    c.line((x, y), (x, y + 3), width=1.4, color="jord_mork")


def _stone(c: Canvas, x: float, y: float, r: float) -> None:
    pts = ellipse_points(x, y, r, r * 0.68, n=9)
    c.glaze(ellipse_points(x + 2, y + r * 0.5, r, r * 0.35), "skugga", opacity=0.3, blur=2)
    c.shape(pts, c.rng.choice(["granit", "granit_mork", "granit"]), ink_width=0.9, wet=0.5)
    c.ink([(x - r * 0.4, y - r * 0.2), (x + r * 0.1, y - r * 0.4)], width=0.5, opacity=0.4)


def _bush(c: Canvas, x: float, y: float, r: float, berry: str, berries: int) -> None:
    c.glaze(ellipse_points(x + 2, y + r * 0.6, r * 1.1, r * 0.4), "skugga", opacity=0.3, blur=2)
    for k in range(4):
        bx, by = x + c.rng.uniform(-r * 0.5, r * 0.5), y + c.rng.uniform(-r * 0.4, r * 0.2)
        blob = ellipse_points(bx, by, r * 0.6, r * 0.5, n=10)
        c.wash(blob, c.rng.choice(["mossgront", "mossgront_mork", "ang_mork"]), wet=0.6, grain=0.8, offset=0.4)
    c.ink(ellipse_points(x, y - r * 0.1, r * 1.05, r * 0.8, n=14), closed=True, width=0.7, opacity=0.6)
    for _ in range(berries):
        c.dot(x + c.rng.uniform(-r * 0.8, r * 0.8), y + c.rng.uniform(-r * 0.6, r * 0.4), 1.5, color=berry, opacity=0.95)


def _tree(c: Canvas, x: float, y: float, r: float) -> None:
    """Ett plommonträd: rund krona på en stam, med gröna kårt."""
    c.glaze(ellipse_points(x + r * 0.6, y + 3, r * 1.1, r * 0.4), "skugga", opacity=0.3, blur=3)
    c.ink([(x, y + 2), (x - 1, y - r * 0.8)], width=2.4, color="jord_mork", opacity=0.9)
    crown = ellipse_points(x, y - r * 1.4, r, r * 0.85, n=14)
    c.wash(crown, "mossgront", wet=0.8, grain=0.8)
    c.glaze(ellipse_points(x - r * 0.3, y - r * 1.6, r * 0.5, r * 0.35), "ang_ljus", opacity=0.4, blur=2)
    c.ink(crown, closed=True, width=0.9, opacity=0.7)
    for _ in range(7):
        c.dot(x + c.rng.uniform(-r * 0.7, r * 0.7), y - r * 1.4 + c.rng.uniform(-r * 0.5, r * 0.6), 1.7, color="kart")


def _inside(c: Canvas, margin: float = 16) -> tuple[float, float]:
    return c.rng.uniform(margin, TILE - margin), c.rng.uniform(margin + 8, TILE - margin)


def draw(terrain: str, v: int) -> Canvas:
    seed = 5000 + TERRAINS.index(terrain) * 10 + v
    if terrain == "glanta":
        return ang.draw(v)
    if terrain == "ang":
        return ang.draw(2 + v)
    if terrain == "skog":
        c = _grass(seed)
        c.glaze(rect_points(4, 4, TILE - 8, TILE - 8), "gran_mork", opacity=0.25, blur=8)
        spots = [(30, 46), (82, 36), (56, 80), (100, 92), (24, 104)] if v == 0 else [(40, 40), (92, 52), (30, 92), (76, 104)]
        for x, y in sorted(spots, key=lambda p: p[1]):
            _spruce(c, x + c.rng.uniform(-4, 4), y + c.rng.uniform(-3, 3), c.rng.uniform(34, 44))
        return c
    if terrain == "stenig":
        c = _grass(seed)
        c.glaze(rect_points(8, 8, TILE - 16, TILE - 16), "granit", opacity=0.25, blur=8)
        for _ in range(7 + v * 2):
            _stone(c, *_inside(c), c.rng.uniform(5, 12))
        return c
    if terrain == "myr":
        c = _full("myr_mork", "#857d4e", seed)
        for _ in range(5):
            x, y = _inside(c, 14)
            c.wash(ellipse_points(x, y, c.rng.uniform(8, 14), c.rng.uniform(4, 7)), "vatten_mork", opacity=0.6, wet=0.8)
        for _ in range(14):
            x, y = _inside(c, 8)
            ang._tuft(c, x, y, c.rng.uniform(6, 10), "myr_mork")
        return c
    if terrain == "vatten":
        c = _full("vatten_mork", "#8eacbe", seed)
        for _ in range(6):
            x, y = _inside(c, 14)
            ln = c.rng.uniform(10, 20)
            c.ink([(x, y), (x + ln * 0.5, y - 1.5), (x + ln, y)], width=0.8, color="vatten_ljus", opacity=0.8, taper=True)
        return c
    if terrain == "skrot":
        c = _grass(seed)
        c.glaze(ellipse_points(64, 70, 50, 40), "jord_mork", opacity=0.35, blur=6)
        for _ in range(6):
            x, y = _inside(c, 26)
            w, h = c.rng.uniform(10, 22), c.rng.uniform(6, 12)
            c.shape(rect_points(x - w / 2, y - h / 2, w, h), c.rng.choice(["gratt_tra_mork", "takpapp", "granit_mork", "jord_mork"]),
                    ink_width=0.9, wet=0.4)
        # Ett hjul utan däck och en hink
        c.ink(ellipse_points(48, 58, 9, 9, n=16), closed=True, width=1.4, opacity=0.8)
        c.ink(ellipse_points(48, 58, 2, 2, n=8), closed=True, width=1.0, opacity=0.8)
        c.shape([(76, 84), (90, 84), (88, 98), (78, 98)], "emalj_bla", ink_width=0.9, wet=0.4)
        return c
    if terrain == "hallonsnar":
        c = _grass(seed)
        spots = [(34, 44), (86, 40), (60, 82), (28, 98), (98, 96)]
        for x, y in sorted(spots[: 4 + v], key=lambda p: p[1]):
            _bush(c, x, y, c.rng.uniform(13, 17), "hallon", 6)
        return c
    if terrain == "plommon":
        c = _grass(seed)
        spots = [(44, 70), (94, 108)] if v == 0 else [(70, 82)]
        for x, y in spots:
            _tree(c, x, y, c.rng.uniform(20, 24))
        return c
    if terrain == "landsvag":
        c = _full("jord", "#c3ad86", seed)
        with c.tiled(TILE, TILE):
            for _ in range(30):
                x, y = c.rng.uniform(0, TILE), c.rng.uniform(0, TILE)
                c.dot(x, y, c.rng.uniform(0.8, 1.8), color=c.rng.choice(["granit_mork", "jord_mork"]), opacity=0.5)
        return c
    if terrain == "aker":
        c = _full("plojd", "#a98a5f", seed)
        with c.tiled(TILE, TILE):
            for k in range(8):
                y = k * 16 + 8
                c.ink([(-4, y), (TILE / 2, y + c.rng.uniform(-1, 1)), (TILE + 4, y)], width=1.0, color="jord_mork",
                      opacity=0.55, taper=False)
                for _ in range(5):
                    x = c.rng.uniform(0, TILE)
                    c.ink([(x, y - 1), (x + c.rng.uniform(-1, 1), y - 7)], width=0.8, color="halmgult", opacity=0.8)
        return c
    raise ValueError(terrain)


def names() -> list[str]:
    return [f"terr_{t}_{v}" for t in TERRAINS for v in range(VARIANTS)]


def build() -> dict[str, str]:
    return {f"terr_{t}_{v}": draw(t, v).to_svg() for t in TERRAINS for v in range(VARIANTS)}
