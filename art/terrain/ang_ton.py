"""
Storskalig ton för ängen: mjuka fläckar i grönt och halmgult som multipliceras över
markrutorna, så att ängen inte ser ut som en tapet. Sträcks ut över hela kartan i Godot.

Bilden har kartans proportioner (26×16 rutor, 32 px per ruta), så att fläckarna inte blir
utdragna. Tre lager: stora fläckar på 5–6 rutor, mellanstora på 2 rutor och små på en ruta.
De små bryter upprepningen mellan markrutorna, som annars syns som rutor vid full utzoomning.
"""
from __future__ import annotations

WIDTH, HEIGHT = 832, 512


def build() -> dict[str, str]:
    svg = f"""<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" viewBox="0 0 {WIDTH} {HEIGHT}">
<defs>
  <filter id="ton" x="0" y="0" width="100%" height="100%" color-interpolation-filters="sRGB">
    <feTurbulence type="fractalNoise" baseFrequency="0.006" numOctaves="3" seed="19" result="a"/>
    <feTurbulence type="fractalNoise" baseFrequency="0.016" numOctaves="2" seed="41" result="b"/>
    <feTurbulence type="fractalNoise" baseFrequency="0.035" numOctaves="2" seed="7" result="c"/>
    <feColorMatrix in="a" type="matrix" values="0 0 0 0 0.78  0 0 0 0 0.85  0 0 0 0 0.64  0 0 0 2.6 -0.95" result="gron"/>
    <feColorMatrix in="b" type="matrix" values="0 0 0 0 1.0  0 0 0 0 0.93  0 0 0 0 0.72  0 0 0 2.4 -0.95" result="halm"/>
    <feColorMatrix in="c" type="matrix" values="0 0 0 0 0.88  0 0 0 0 0.92  0 0 0 0 0.80  0 0 0 2.2 -0.9" result="smatt"/>
    <feFlood flood-color="#ffffff" result="vit"/>
    <feMerge><feMergeNode in="vit"/><feMergeNode in="gron"/><feMergeNode in="halm"/><feMergeNode in="smatt"/></feMerge>
  </filter>
</defs>
<rect width="{WIDTH}" height="{HEIGHT}" filter="url(#ton)"/>
</svg>
"""
    return {"ang_ton": svg}
