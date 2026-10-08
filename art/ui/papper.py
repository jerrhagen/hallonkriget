"""
Papperstexturen som pappersshadern multiplicerar över hela skärmen.
Nästan vit, med fibrer och ojämn ton. Går att lägga kant i kant (stitchTiles).
"""
from __future__ import annotations

SIZE = 512


def build() -> dict[str, str]:
    svg = f"""<svg xmlns="http://www.w3.org/2000/svg" width="{SIZE}" height="{SIZE}" viewBox="0 0 {SIZE} {SIZE}">
<defs>
  <filter id="fiber" x="0" y="0" width="100%" height="100%" color-interpolation-filters="sRGB">
    <feTurbulence type="fractalNoise" baseFrequency="0.5 0.35" numOctaves="4" seed="3" stitchTiles="stitch" result="fine"/>
    <feColorMatrix in="fine" type="matrix" values="0 0 0 0 0.88  0 0 0 0 0.84  0 0 0 0 0.76  0 0 0 -2.2 1.2" result="fineTone"/>
    <feTurbulence type="fractalNoise" baseFrequency="0.0078125" numOctaves="3" seed="8" stitchTiles="stitch" result="blot"/>
    <feColorMatrix in="blot" type="matrix" values="0 0 0 0 0.86  0 0 0 0 0.80  0 0 0 0 0.70  0 0 0 -1.4 0.7" result="blotTone"/>
    <feFlood flood-color="#fbf7ee" result="paper"/>
    <feMerge><feMergeNode in="paper"/><feMergeNode in="blotTone"/><feMergeNode in="fineTone"/></feMerge>
  </filter>
</defs>
<rect width="{SIZE}" height="{SIZE}" filter="url(#fiber)"/>
</svg>
"""
    return {"papper": svg}
