"""
Pysslingen, Torpets bärare: liten, snabb, med för stor luva och ett ägg på ryggen.

Ritad som klippdocka i sju delar som animeras i Godot. Alla delar ritas på samma
96×96-duk med fötterna i FEET, så att de hamnar rätt när de läggs på varandra.
PIVOTS säger kring vilken punkt varje del vrids. Profil åt höger; vänster fås genom spegling.
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from svglib import Canvas, ellipse_points, rotate  # noqa: E402

SIZE = (96, 96)
FEET = (48.0, 90.0)

# Vridpunkter i dukens koordinater
PIVOTS = {
    "ben_bak": (46.0, 72.0),
    "ben_fram": (50.0, 72.0),
    "kropp": (48.0, 72.0),
    "huvud": (50.0, 52.0),
    "arm_bak": (47.0, 56.0),
    "arm_fram": (52.0, 56.0),
    "agg": (40.0, 58.0),
}

# Ritordning, bakifrån och fram
# Små figurer får mindre penseldrag än byggnader
FX = 0.35

ORDER = ["arm_bak", "ben_bak", "agg", "kropp", "huvud", "ben_fram", "arm_fram"]


def _leg(c: Canvas, hip: tuple[float, float], back: bool) -> None:
    hx, hy = hip
    leg = [(hx - 3.0, hy - 1), (hx + 3.0, hy - 1), (hx + 2.5, hy + 13.5), (hx - 2.5, hy + 13.5)]
    shoe = [(hx - 3.8, hy + 13), (hx + 3, hy + 12.6), (hx + 7.8, hy + 15.2), (hx + 7.6, hy + 18.2), (hx - 4.2, hy + 18.2)]
    c.shape(leg, "#5b5146", ink_width=0.6, wet=0.4)
    c.shape(shoe, "jord_mork", ink_width=0.7, wet=0.4)
    if back:
        c.glaze(leg + shoe, "skugga", opacity=0.35, blur=0.8)


def _arm(c: Canvas, shoulder: tuple[float, float], back: bool) -> None:
    sx, sy = shoulder
    sleeve = [(sx - 3.2, sy - 1), (sx + 3.2, sy - 1), (sx + 2.8, sy + 10), (sx - 2.7, sy + 10.4)]
    c.shape(sleeve, "ull_gra" if not back else "#77705f", ink_width=0.6, wet=0.4)
    c.shape(ellipse_points(sx + 0.2, sy + 12.3, 2.9, 2.8), "hud", ink_width=0.55, wet=0.3)
    if back:
        c.glaze(sleeve, "skugga", opacity=0.3, blur=0.8)


def _body(c: Canvas) -> None:
    # Tunikan: rund och lite för stor
    tunic = [(38, 73), (37, 63), (40, 55.5), (45.5, 52), (53, 52), (58.5, 55.5), (61, 63), (60.5, 73), (55, 75),
             (43.5, 75)]
    c.shape(tunic, "ull_gra", ink_width=0.75)
    # Lapp på magen, i en annan ull
    patch = [(52, 61), (57, 60.6), (57.4, 65.6), (52.4, 66)]
    c.shape(patch, "ull_brun", ink_width=0.5, wet=0.3)
    for i in range(3):
        c.line((52.6 + i * 1.9, 60.8), (52.8 + i * 1.9, 59.8), width=0.4, opacity=0.8)
    c.hatch([(38, 73), (37, 63), (40.5, 55.5), (43, 75)], spacing=1.8, angle=1.0, width=0.4, opacity=0.35)
    # Bälte av snöre
    c.ink([(37.5, 67), (49, 68.5), (61, 67)], width=1.0, color="halmgult", opacity=1.0, taper=False)
    c.ink([(49, 68.5), (48, 72)], width=0.6, color="halmgult", opacity=1.0)
    # Remmen till korgen, över axeln
    c.ink([(40.5, 57), (50, 52.6), (55.5, 58.5), (57.5, 66)], width=1.4, color="jord_mork", opacity=1.0, taper=False)


def _head(c: Canvas) -> None:
    # Ansikte och skägg
    face = ellipse_points(51, 45, 7.4, 7.0, n=16)
    c.shape(face, "hud", ink_width=0.65, wet=0.4)
    beard = [(44.5, 46), (46.8, 51.5), (51, 54.2), (55.8, 52.2), (58.2, 47.5), (55, 49.2), (50, 49.6)]
    c.shape(beard, "bjorkvitt", ink_width=0.55, wet=0.3)
    c.shape(ellipse_points(58, 45.5, 3.2, 2.8), "hud_rod", ink_width=0.6, wet=0.3)  # näsan
    c.dot(54, 42.3, 0.75)  # ögat
    c.ink([(52.5, 40.3), (55.4, 39.8)], width=0.45, opacity=0.8)  # ögonbrynet
    c.glaze(ellipse_points(53.5, 47, 2.0, 1.4), "hud_rod", opacity=0.4, blur=0.8)  # kind
    # Luvan: för stor, lappad, med toppen som hänger bakåt
    cap = [(43.2, 45), (42.6, 38.5), (41.2, 32), (37.5, 26), (32, 21.5), (27.5, 20.5), (29.5, 23.2), (35, 26.5),
           (41.5, 29.2), (49.5, 30.8), (55.5, 32.2), (59.2, 35.6), (60.6, 40.8), (52.5, 39.4), (46, 41.2)]
    c.shape(cap, "mossa_rod", ink_width=0.75)
    c.shape([(46.5, 33.5), (51, 32.6), (51.6, 36.5), (47.2, 37.2)], "falurott_mork", ink_width=0.45, wet=0.3)
    for i in range(3):
        c.line((46.8 + i * 1.6, 33.0 - i * 0.2), (47.2 + i * 1.6, 37.6 - i * 0.2), width=0.3, opacity=0.6)
    c.ink([(42.8, 42), (49, 40.3), (55, 39.9), (60.6, 41.2)], width=0.9, opacity=0.9)  # luvans kant
    c.glaze([(42.6, 38.5), (41.2, 32), (37.5, 26), (32, 21.5), (35, 26.5), (41.5, 29.2)], "skugga", opacity=0.25,
            blur=1.0)
    c.shape(ellipse_points(27, 21, 2.3, 2.1), "bjorkvitt", ink_width=0.5, wet=0.3)  # tofsen


def _egg(c: Canvas) -> None:
    # En liten näverkorg på ryggen, med ägget som sticker upp
    egg = rotate(ellipse_points(34.5, 55.5, 8.0, 10.2, n=22), 34.5, 55.5, -0.12)
    c.shape(egg, "agg", ink_width=0.75, wet=0.5)
    c.glaze([(28, 58), (40, 57), (41, 62), (34, 63), (28, 61)], "agg_skugga", opacity=0.6, blur=1.2)
    c.glaze(ellipse_points(31.5, 51, 2.0, 2.9), "#ffffff", opacity=0.75, blur=0.8)
    basket = [(25.5, 58.5), (44, 58), (42.5, 72), (27.5, 72.5)]
    c.shape(basket, "halmgult", ink_width=0.7, wet=0.4)
    c.hatch(basket, spacing=2.0, angle=0.0, width=0.45, opacity=0.6, length_jitter=0.05)
    c.hatch(basket, spacing=2.6, angle=1.45, width=0.35, opacity=0.4, length_jitter=0.05)
    c.ink([(25.2, 58.6), (44.3, 58.1)], width=1.1, color="jord_mork", opacity=1.0, taper=False)  # kanten


PARTS = {
    "ben_bak": lambda c: _leg(c, PIVOTS["ben_bak"], back=True),
    "ben_fram": lambda c: _leg(c, PIVOTS["ben_fram"], back=False),
    "kropp": _body,
    "huvud": _head,
    "arm_bak": lambda c: _arm(c, PIVOTS["arm_bak"], back=True),
    "arm_fram": lambda c: _arm(c, PIVOTS["arm_fram"], back=False),
    "agg": _egg,
}


def draw_part(name: str, seed: int = 11) -> Canvas:
    c = Canvas(SIZE[0], SIZE[1], seed + ORDER.index(name) * 101, fx=FX)
    PARTS[name](c)
    return c


def draw_assembled(seed: int = 11) -> Canvas:
    """Alla delar i vila, plus skugga. För förhandsvisning och ikoner."""
    c = Canvas(SIZE[0], SIZE[1], seed, fx=FX)
    c.glaze(ellipse_points(FEET[0], FEET[1] - 1, 15, 3), "skugga", opacity=0.4, blur=1.5)
    for name in ORDER:
        part = draw_part(name, seed)
        c.defs.extend(part.defs)
        c.body.extend(part.body)
    return c


def build() -> dict[str, str]:
    out = {f"pyssling_{name}": draw_part(name).to_svg() for name in ORDER}
    out["pyssling"] = draw_assembled().to_svg()
    return out


def rig() -> dict:
    """Klippdockans uppbyggnad för Godot: delar, ritordning och vridpunkter, i 1x-pixlar."""
    return {
        "size": list(SIZE),
        "feet": list(FEET),
        "order": ORDER,
        "pivots": {k: list(v) for k, v in PIVOTS.items()},
        # Varje del hänger på en förälder; benen och kroppen på figuren, resten på kroppen.
        "parent": {"ben_bak": None, "ben_fram": None, "kropp": None,
                   "huvud": "kropp", "arm_bak": "kropp", "arm_fram": "kropp", "agg": "kropp"},
    }


if __name__ == "__main__":
    import resvg_py

    out = Path(sys.argv[1] if len(sys.argv) > 1 else "pyssling.png")
    out.write_bytes(bytes(resvg_py.svg_to_bytes(svg_string=draw_assembled().to_svg(), background="#f4ecd9", zoom=5)))
