"""
Renderar all grafik: kör ritskripten, skriver SVG till art/out/svg/ och PNG i 1× och 2×
till game/art/, där Godot läser dem.

    pip install resvg-py pillow
    python3 art/render.py              # allt
    python3 art/render.py honshus      # bara moduler vars namn innehåller "honshus"

1× är 128 px per ruta, alltså dubbla skärmstorleken vid normal zoom; 2× är för inzoomning.
Samma skript ger exakt samma bilder varje gång, eftersom all slump styrs av frön.
"""
from __future__ import annotations

import importlib
import io
import json
import sys
from pathlib import Path

import resvg_py
from PIL import Image

ART = Path(__file__).resolve().parent
ROOT = ART.parent
OUT_SVG = ART / "out" / "svg"
GAME_ART = ROOT / "game" / "art"

sys.path.insert(0, str(ART))

# (kategori, modul). Varje modul har build() -> {namn: svg}; klippdockor har även rig().
ASSETS = [
    ("buildings", "buildings.honshus"),
    ("people", "people.pyssling"),
    ("terrain", "terrain.ang"),
    ("terrain", "terrain.ang_ton"),
    ("terrain", "terrain.stig_prov"),
    ("terrain", "terrain.terrang"),
    ("terrain", "terrain.stigar"),
    ("ui", "ui.papper"),
]

# Rutor läggs dessutom ihop till en atlas per terräng, för Godots TileSet.
ATLASES = {
    "ang": ("terrain", [f"ang_{i}" for i in range(4)]),
    # Kartans terräng: en kolumn per terräng och variant, i ordningen i Terrain.cs.
    "terrang": ("terrain", [f"terr_{t}_{v}" for t in
                            ["glanta", "stenig", "skog", "ang", "myr", "vatten", "skrot", "hallonsnar", "plommon",
                             "landsvag", "aker"] for v in range(2)]),
    # Stigarna: en kolumn per kombination av grannar (1 norr, 2 öster, 4 söder, 8 väster).
    "stigar": ("terrain", [f"stig_{m:02d}" for m in range(16)]),
}

# Rutor som bara behövs i sin atlas, inte som egna bilder.
ATLAS_ONLY = set(ATLASES["terrang"][1]) | set(ATLASES["stigar"][1])

# Bilder som bara behövs i en storlek
ONLY_1X = {"papper", "ang_ton"}


def render(svg: str, zoom: int) -> Image.Image:
    return Image.open(io.BytesIO(bytes(resvg_py.svg_to_bytes(svg_string=svg, zoom=zoom)))).convert("RGBA")


def main() -> None:
    only = sys.argv[1:]
    rendered: dict[str, dict[int, Image.Image]] = {}
    for category, module_name in ASSETS:
        if only and not any(o in module_name for o in only):
            continue
        module = importlib.import_module(module_name)
        svgs = module.build()
        (OUT_SVG / category).mkdir(parents=True, exist_ok=True)
        (GAME_ART / category).mkdir(parents=True, exist_ok=True)
        for name, svg in svgs.items():
            (OUT_SVG / category / f"{name}.svg").write_text(svg, encoding="utf-8")
            zooms = (1,) if name in ONLY_1X else (1, 2)
            rendered[name] = {}
            for z in zooms:
                img = render(svg, z)
                rendered[name][z] = img
                suffix = "" if z == 1 else f"@{z}x"
                if name not in ATLAS_ONLY:
                    img.save(GAME_ART / category / f"{name}{suffix}.png", optimize=True)
            print(f"  {category}/{name}")
        if hasattr(module, "meta"):
            meta_name = module_name.split(".")[-1]
            (GAME_ART / category / f"{meta_name}.json").write_text(
                json.dumps(module.meta(), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        if hasattr(module, "rig"):
            rig_name = module_name.split(".")[-1]
            (GAME_ART / category / f"{rig_name}_rig.json").write_text(
                json.dumps(module.rig(), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    for atlas, (category, names) in ATLASES.items():
        if not all(n in rendered for n in names):
            continue
        for z in (1, 2):
            tiles = [rendered[n][z] for n in names]
            w, h = tiles[0].size
            sheet = Image.new("RGBA", (w * len(tiles), h))
            for i, t in enumerate(tiles):
                sheet.paste(t, (i * w, 0))
            suffix = "" if z == 1 else f"@{z}x"
            sheet.save(GAME_ART / category / f"{atlas}_atlas{suffix}.png", optimize=True)
        print(f"  {category}/{atlas}_atlas")


if __name__ == "__main__":
    main()
