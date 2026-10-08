"""
Hönshuset, Torpets version: rött, lappat och lite snett. 2×2 rutor.

Bilden är 256 px bred (två rutor à 128) och 352 hög. De nedersta 256 pixlarna är
byggnadens fotavtryck; resten är tak och vindflöjel som sticker upp över rutorna ovanför.
Vyn är snett ovanifrån med gaveln mot betraktaren, som i Knights and Merchants.
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from svglib import Canvas, ellipse_points, rect_points, translate  # noqa: E402
from animals.hona import draw_hen  # noqa: E402

WIDTH, HEIGHT = 256, 352
FOOTPRINT = (2, 2)
# Där byggnadens fotavtryck börjar i bilden: rutan (0,0) har sitt övre vänstra hörn här.
ANCHOR = (0, HEIGHT - 256)

# Gavelväggen
L, R, BASE, EAVE = 60.0, 176.0, 298.0, 228.0
PEAK = (118.0, 170.0)
# Djupet bakåt i bilden (snett uppåt höger, så att högra långsidan syns)
DX, DY = 24.0, -66.0


def side(s: float, h: float) -> tuple[float, float]:
    """En punkt på högra långsidan: s = 0 framme, 1 baktill; h = höjd över marken."""
    return (R + DX * s, BASE - h + DY * s)


def front_point(u: float, h: float) -> tuple[float, float]:
    """En punkt på gaveln: u = 0 vänster, 1 höger; h = höjd över marken."""
    return (L + (R - L) * u, BASE - h)


def draw(seed: int = 7) -> Canvas:
    c = Canvas(WIDTH, HEIGHT, seed)
    wall_h = BASE - EAVE

    # --- mark och skugga ---------------------------------------------------
    c.wash(ellipse_points(126, 316, 106, 28, n=28), "jord", opacity=0.42, wet=1.2, grain=1.4, solid=False)
    c.glaze([(R - 4, BASE + 4), (R + DX + 22, BASE + DY + 14), (R + DX + 34, BASE + DY + 46), (R + 30, BASE + 10)],
            "skugga", opacity=0.22, blur=5)
    c.glaze(ellipse_points(118, 302, 72, 8), "skugga", opacity=0.35, blur=3)

    # --- högra långsidan ---------------------------------------------------
    side_wall = [side(0, 0), side(0, wall_h), side(1, wall_h), side(1, 0)]
    c.wash(side_wall, "falurott_mork", wet=0.9)
    for i in range(1, 9):
        s = i / 9
        c.line(side(s, 2), side(s, wall_h - 2), width=0.7, opacity=0.55)
    c.hatch(side_wall, spacing=3.2, angle=1.1, opacity=0.35)
    c.ink(side_wall, closed=True, width=1.4)
    # Regntunna vid långsidan
    bx, by = R + 22, BASE + 2
    c.shape([(bx - 9, by), (bx - 10, by - 22), (bx + 10, by - 22), (bx + 9, by)], "gratt_tra_mork", ink_width=1.2)
    c.shape(ellipse_points(bx, by - 22, 10, 3.2), "#3a4248", ink_width=1.0, wet=0.4)
    for hy in (5, 16):
        c.line((bx - 9.5, by - hy), (bx + 9.5, by - hy), width=1.4, color="blck_ljus")

    # --- gaveln -----------------------------------------------------------
    gable = [front_point(0, 0), front_point(0, wall_h), PEAK, front_point(1, wall_h), front_point(1, 0)]
    c.wash(gable, "falurott", wet=1.1)
    # Lockpanel: lodräta bräder
    x = L + 9
    while x < R - 6:
        top = BASE - wall_h if abs(x - PEAK[0]) > (R - L) / 2 else None
        # Höjden under taket vid x
        frac = abs(x - PEAK[0]) / ((R - L) / 2)
        y_top = PEAK[1] + (EAVE - PEAK[1]) * frac + 2
        c.line((x, BASE - 2), (x + c.rng.uniform(-0.6, 0.6), y_top), width=0.8, opacity=0.6)
        x += 9.5 + c.rng.uniform(-1, 1)
    # Skugga under takfoten
    c.glaze([(L, EAVE - 2), PEAK, (R, EAVE - 2), (R, EAVE + 14), (PEAK[0], PEAK[1] + 16), (L, EAVE + 14)],
            "falurott_mork", opacity=0.55, blur=3)
    c.ink(gable, closed=True, width=1.6)

    # Vita knutbrädor
    for u0 in (0.0, 0.955):
        kb = [front_point(u0, 1), front_point(u0, wall_h), front_point(u0 + 0.045, wall_h), front_point(u0 + 0.045, 1)]
        c.shape(kb, "knutvirke", ink_width=1.0, wet=0.4)

    # Fönster i gaveln
    wx, wy, ww, wh = 109, 196, 19, 17
    c.shape(rect_points(wx - 3, wy - 3, ww + 6, wh + 6), "knutvirke", ink_width=1.1, wet=0.4)
    c.shape(rect_points(wx, wy, ww, wh), "#3d4a52", ink_width=1.0, wet=0.5)
    c.glaze(rect_points(wx + 2, wy + 2, 6, 5), "#cfe0e6", opacity=0.6, blur=1.0)
    c.line((wx + ww / 2, wy), (wx + ww / 2, wy + wh), width=1.6, color="knutvirke")
    c.line((wx, wy + wh / 2), (wx + ww, wy + wh / 2), width=1.6, color="knutvirke")

    # Människodörr till vänster och hönslucka till höger, med hönstrappa
    door = [(72, BASE), (72, 252), (96, 252), (96, BASE)]
    c.shape(door, "falurott_mork", ink_width=1.3)
    for dx in (78, 84, 90):
        c.line((dx, BASE - 2), (dx, 255), width=0.7, opacity=0.5)
    c.shape(rect_points(73, 268, 22, 4), "knutvirke", ink_width=0.8, wet=0.3)   # tvärslå, ljus
    c.line((74, 255), (93, 293), width=1.0, opacity=0.6)                       # snedslå
    c.dot(92, 276, 1.2)                                                       # vred
    hatch = [(128, 286), (128, 268), (131, 263), (144, 263), (147, 268), (147, 286)]
    c.shape(hatch, "#2e2621", ink_width=1.2, wet=0.5)
    c.shape([(127, 287), (148, 287), (148, 290), (127, 290)], "knutvirke", ink_width=0.9, wet=0.3)
    ramp = [(130, 289), (145, 289), (156, 334), (139, 336)]
    c.shape(ramp, "gratt_tra", ink_width=1.2, wet=0.6)
    for t in (0.2, 0.4, 0.6, 0.8):
        a = (130 + (139 - 130) * t, 289 + (336 - 289) * t)
        b = (145 + (156 - 145) * t, 289 + (334 - 289) * t)
        c.line(a, b, width=1.6, color="gratt_tra_mork")

    # --- taket ------------------------------------------------------------
    ov = 8
    LE, PF, RE = (L - ov, EAVE + ov * 0.6), (PEAK[0], PEAK[1] - 6), (R + ov, EAVE + ov * 0.6)
    LB, PB, RB = (LE[0] + DX, LE[1] + DY), (PF[0] + DX, PF[1] + DY), (RE[0] + DX, RE[1] + DY)
    left_roof = [LE, PF, PB, LB]
    right_roof = [PF, RE, RB, PB]
    c.wash(left_roof, "gratt_tra", wet=1.0)
    c.wash(right_roof, "gratt_tra_mork", wet=1.0)
    # Takbräder längs fallet
    for i in range(1, 12):
        t = i / 12
        a = (PF[0] + DX * t, PF[1] + DY * t)
        c.line(a, (LE[0] + DX * t, LE[1] + DY * t), width=0.7, opacity=0.5)
        c.line(a, (RE[0] + DX * t, RE[1] + DY * t), width=0.7, opacity=0.5)
    c.glaze([PF, RE, RB, PB], "skugga", opacity=0.18, blur=4)
    # Lapp av takpapp, spikad
    patch = [(84, 176), (102, 160), (113, 176), (95, 192)]
    c.shape(patch, "takpapp", ink_width=1.1, wet=0.6)
    for px, py in [(86, 177), (101, 162), (111, 176), (95, 190), (93, 168), (104, 184)]:
        c.dot(px, py, 0.8)
    # Mossa längs takfoten
    for i in range(7):
        t = 0.1 + i * 0.12 + c.rng.uniform(-0.03, 0.03)
        mx, my = (LE[0] + DX * t, LE[1] + DY * t)
        c.wash(ellipse_points(mx + 4, my - 2, 5, 3), "mossgront", wet=0.9, grain=1.4)
    c.ink(left_roof, closed=True, width=1.6)
    c.ink(right_roof, closed=True, width=1.6)
    # Vindskivor: vita brädor längs gavelns takkant
    for a, b in ((LE, PF), (PF, RE)):
        c.ink([a, b], width=5.5, color="knutvirke", taper=False, opacity=1.0, jitter=0.15)
        c.ink([(a[0], a[1] + 3), (b[0], b[1] + 3)], width=0.9, opacity=0.8)
        c.ink([(a[0], a[1] - 3), (b[0], b[1] - 3)], width=0.9, opacity=0.8)
    # Nockbräda
    c.ink([PF, PB], width=3.2, color="gratt_tra_mork", taper=False, opacity=1.0)
    c.ink([PF, PB], width=1.0)

    # Vindflöjel: en tupp av plåt på en pinne
    vx, vy = PB[0], PB[1]
    c.line((vx, vy), (vx + 1, vy - 34), width=1.3)
    from animals.hona import BODY, COMB
    tupp = [(vx + px * 0.75, vy - 34 + py * 0.75 + 15) for px, py in BODY]
    kam = [(vx + px * 0.75, vy - 34 + py * 0.75 + 15) for px, py in COMB]
    c.shape(tupp, "blck_ljus", ink_width=0.8, wet=0.3)
    c.shape(kam, "blck_ljus", ink_width=0.6, wet=0.2)
    c.line((vx - 10, vy - 22), (vx + 12, vy - 22), width=1.1)
    c.ink([(vx + 12, vy - 22), (vx + 8, vy - 25), (vx + 8, vy - 19)], closed=True, width=0.9)

    # --- småsaker på gården -------------------------------------------------
    # Emaljskål med vete
    bowl = ellipse_points(186, 318, 11, 4.5)
    c.shape(ellipse_points(186, 321, 11, 6), "emalj_bla", ink_width=1.1, wet=0.5)
    c.shape(bowl, "halmgult", ink_width=0.9, wet=0.4)
    c.dot(193, 323, 1.5, color="blck", opacity=0.6)  # kantstött
    for _ in range(14):
        c.dot(150 + c.rng.uniform(0, 70), 322 + c.rng.uniform(0, 18), 0.8, color="halmgult_ljus", opacity=0.95)
    # Halmstrån vid luckan
    for _ in range(9):
        x0, y0 = 120 + c.rng.uniform(0, 40), 330 + c.rng.uniform(0, 12)
        c.line((x0, y0), (x0 + c.rng.uniform(-7, 7), y0 + c.rng.uniform(-3, 3)), width=0.9, color="halmgult")
    # En fjäder och ett ägg som någon tappat
    c.ink([(40, 334), (44, 330), (49, 329)], width=1.6, color="hona_vit", opacity=1.0)
    c.ink([(40, 334), (49, 329)], width=0.5)
    c.shape(ellipse_points(58, 338, 3.6, 4.6, rot=0.4), "agg", ink_width=0.8, wet=0.3)
    # Stenar
    c.shape(ellipse_points(30, 312, 7, 4.5), "granit", ink_width=0.9, wet=0.6)
    c.shape(ellipse_points(222, 302, 5, 3.5), "granit_mork", ink_width=0.9, wet=0.6)

    # Höns: en på väg uppför trappan, en som pickar på gården
    draw_hen(c, 147, 311, facing=-1)
    draw_hen(c, 52, 326, facing=1, color="hona_brun", pecking=True)
    draw_hen(c, 210, 338, scale=1.2, facing=-1)

    return c


def build() -> dict[str, str]:
    return {"honshus": draw().to_svg()}


if __name__ == "__main__":
    import resvg_py

    out = Path(sys.argv[1] if len(sys.argv) > 1 else "honshus.png")
    out.write_bytes(bytes(resvg_py.svg_to_bytes(svg_string=draw().to_svg(), background="#f4ecd9", zoom=2)))
