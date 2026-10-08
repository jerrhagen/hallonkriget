"""
Rådgivarna i hörnet: Sixten, torpets katt, och Major, Storgårdens hund. Porträtt i en oval ram,
som ett fotografi på väggen. 128 × 128 i 1×.
"""
from __future__ import annotations

from svglib import Canvas, ellipse_points

SIZE = 128
CX, CY = 64, 66


def frame(c: Canvas) -> None:
    c.wash(ellipse_points(CX, CY - 2, 60, 62, n=40), "ram", wet=0.5, grain=0.6)
    c.ink(ellipse_points(CX, CY - 2, 60, 62, n=40), closed=True, width=1.6)
    c.wash(ellipse_points(CX, CY - 2, 52, 54, n=40), "papper", wet=0.3, grain=0.4)
    c.glaze(ellipse_points(CX, CY + 30, 44, 22), "ang", opacity=0.35, blur=5)
    c.ink(ellipse_points(CX, CY - 2, 52, 54, n=40), closed=True, width=0.9, opacity=0.7)


def sixten() -> str:
    c = Canvas(SIZE, SIZE, seed=58, fx=0.7)
    frame(c)
    # Kroppen och den randiga halsduken.
    body = [(30, 118), (36, 92), (50, 84), (78, 84), (92, 92), (98, 118)]
    c.wash(body, "katt_gra", wet=0.6)
    c.ink(body, width=1.3)
    scarf = [(40, 88), (64, 96), (88, 88), (86, 94), (64, 102), (42, 94)]
    c.wash(scarf, "mossgront", wet=0.4)
    c.ink(scarf, closed=True, width=1.0)
    c.wash([(70, 98), (76, 99), (74, 116), (67, 114)], "mossgront", wet=0.4)
    c.ink([(70, 98), (76, 99), (74, 116), (67, 114)], closed=True, width=0.9)
    # Huvudet med öron.
    ears = [[(34, 46), (36, 18), (54, 34)], [(94, 46), (92, 18), (74, 34)]]
    for e in ears:
        c.wash(e, "katt_gra", wet=0.5)
        c.ink(e, closed=True, width=1.2)
    c.wash([(39, 40), (38, 26), (49, 35)], "hud_rod", opacity=0.6, wet=0.3)
    c.wash([(89, 40), (90, 26), (79, 35)], "hud_rod", opacity=0.6, wet=0.3)
    head = ellipse_points(64, 56, 32, 28, n=30)
    c.wash(head, "katt_gra", wet=0.6)
    c.wash(ellipse_points(64, 68, 16, 11), "katt_vit", wet=0.4)
    for x0, y0, x1, y1 in [(56, 30, 58, 40), (64, 29, 64, 40), (72, 30, 70, 40),
                           (34, 54, 44, 56), (35, 62, 44, 62), (94, 54, 84, 56), (93, 62, 84, 62)]:
        c.line((x0, y0), (x1, y1), width=2.2, color="katt_rand", opacity=0.8)
    c.ink(head, closed=True, width=1.4)
    # Halvslutna ögon: han har sett det här förut.
    for ex in (52, 76):
        c.wash(ellipse_points(ex, 54, 6, 4.5), "halmgult", wet=0.3)
        c.ink(ellipse_points(ex, 54, 6, 4.5), closed=True, width=0.9)
        c.line((ex - 6.5, 52), (ex + 6.5, 52), width=1.6)
        c.line((ex, 52.5), (ex, 57.5), width=1.8)
    c.wash([(60, 63), (68, 63), (64, 67)], "hud_rod", wet=0.2)
    c.ink([(60, 63), (68, 63), (64, 67)], closed=True, width=0.8)
    c.ink([(64, 67), (64, 70), (59, 72)], width=0.8)
    c.ink([(64, 70), (69, 72)], width=0.8)
    for dy in (-2, 2):
        c.line((50, 66 + dy), (28, 64 + dy * 2), width=0.5, opacity=0.6)
        c.line((78, 66 + dy), (100, 64 + dy * 2), width=0.5, opacity=0.6)
    return c.to_svg()


def major() -> str:
    c = Canvas(SIZE, SIZE, seed=1958, fx=0.7)
    frame(c)
    body = [(28, 118), (34, 94), (50, 86), (78, 86), (94, 94), (100, 118)]
    c.wash(body, "hund_brun", wet=0.6)
    c.ink(body, width=1.3)
    collar = [(42, 88), (64, 94), (86, 88), (86, 94), (64, 100), (42, 94)]
    c.wash(collar, "halsband", wet=0.3)
    c.ink(collar, closed=True, width=1.0)
    c.wash(ellipse_points(64, 104, 5, 5), "halmgult", wet=0.3)
    c.ink(ellipse_points(64, 104, 5, 5), closed=True, width=0.8)
    head = [(40, 40), (46, 26), (64, 22), (82, 26), (88, 40), (86, 62), (80, 80), (64, 86), (48, 80),
            (42, 62)]
    c.wash(head, "hund_brun", wet=0.6)
    c.wash(ellipse_points(64, 70, 15, 13), "katt_vit", wet=0.4, opacity=0.8)
    c.ink(head, closed=True, width=1.4)
    # Hängande öron.
    for ear in ([(44, 30), (30, 40), (28, 66), (36, 70), (44, 50)], [(84, 30), (98, 40), (100, 66), (92, 70), (84, 50)]):
        c.wash(ear, "hund_mork", wet=0.5)
        c.ink(ear, closed=True, width=1.2)
    # Allvarliga ögon under buskiga bryn.
    for ex in (53, 75):
        c.wash(ellipse_points(ex, 50, 4.5, 4.5), "katt_vit", wet=0.2)
        c.ink(ellipse_points(ex, 50, 4.5, 4.5), closed=True, width=0.8)
        c.dot(ex, 51, 2.4)
        c.line((ex - 7, 43 + (2 if ex < 64 else -1)), (ex + 6, 43 + (-1 if ex < 64 else 2)), width=2.6,
               color="hund_mork")
    c.wash(ellipse_points(64, 64, 7, 5), "nos", wet=0.2)
    c.ink(ellipse_points(64, 64, 7, 5), closed=True, width=0.8)
    c.ink([(64, 69), (64, 74), (57, 77)], width=0.9)
    c.ink([(64, 74), (71, 77)], width=0.9)
    return c.to_svg()


def build() -> dict[str, str]:
    return {"sixten": sixten(), "major": major()}
