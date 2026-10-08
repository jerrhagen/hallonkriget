"""
Storskalig ton för ängen: stora, mjuka fläckar i grönt och halmgult som multipliceras över
markrutorna, så att ängen inte ser ut som en tapet. Sträcks ut över hela kartan i Godot.
"""
from __future__ import annotations

SIZE = 512


def build() -> dict[str, str]:
    svg = f"""<svg xmlns="http://www.w3.org/2000/svg" width="{SIZE}" height="{SIZE}" viewBox="0 0 {SIZE} {SIZE}">
<defs>
  <filter id="ton" x="0" y="0" width="100%" height="100%" color-interpolation-filters="sRGB">
    <feTurbulence type="fractalNoise" baseFrequency="0.006" numOctaves="3" seed="19" result="a"/>
    <feTurbulence type="fractalNoise" baseFrequency="0.014" numOctaves="2" seed="41" result="b"/>
    <feColorMatrix in="a" type="matrix" values="0 0 0 0 0.80  0 0 0 0 0.86  0 0 0 0 0.66  0 0 0 2.4 -0.95" result="gron"/>
    <feColorMatrix in="b" type="matrix" values="0 0 0 0 1.0  0 0 0 0 0.93  0 0 0 0 0.72  0 0 0 2.2 -0.95" result="halm"/>
    <feFlood flood-color="#ffffff" result="vit"/>
    <feMerge><feMergeNode in="vit"/><feMergeNode in="gron"/><feMergeNode in="halm"/></feMerge>
  </filter>
</defs>
<rect width="{SIZE}" height="{SIZE}" filter="url(#ton)"/>
</svg>
"""
    return {"ang_ton": svg}
