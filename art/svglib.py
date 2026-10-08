"""
Bilderboksknepen i kod: skeva linjer, bläckkonturer med varierande tjocklek,
akvarellfyllning med flammig färg och mörkare kanter, kryssskuggning.

Allt slumpas från ett frö, så samma skript ger exakt samma bild varje gång.
Koordinater i pixlar vid 1x (128 px per ruta).
"""
from __future__ import annotations

import json
import math
import random
from pathlib import Path

PALETTE: dict[str, str] = {
    k: v for k, v in json.loads((Path(__file__).parent / "palette.json").read_text(encoding="utf-8")).items()
    if not k.startswith("_")
}


def col(name_or_hex: str) -> str:
    """Färg ur paletten, eller en hexfärg rakt av."""
    return PALETTE.get(name_or_hex, name_or_hex)


Point = tuple[float, float]


# ---------------------------------------------------------------------------
# Geometri


def _lerp(a: Point, b: Point, t: float) -> Point:
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def densify(points: list[Point], closed: bool, step: float) -> list[Point]:
    """Delar upp långa sträckor så att det finns punkter att vicka på."""
    out: list[Point] = []
    n = len(points)
    segs = n if closed else n - 1
    for i in range(segs):
        a, b = points[i], points[(i + 1) % n]
        length = math.dist(a, b)
        k = max(1, int(length / step))
        for j in range(k):
            out.append(_lerp(a, b, j / k))
    if not closed:
        out.append(points[-1])
    return out


class Noise1D:
    """Mjukt brus längs en linje: slumpade värden med cosinusinterpolation."""

    def __init__(self, rng: random.Random, period: int = 64):
        self.values = [rng.uniform(-1, 1) for _ in range(period)]
        self.period = period

    def __call__(self, x: float) -> float:
        i = math.floor(x)
        t = x - i
        a = self.values[i % self.period]
        b = self.values[(i + 1) % self.period]
        t2 = (1 - math.cos(t * math.pi)) / 2
        return a * (1 - t2) + b * t2


def wobble(points: list[Point], rng: random.Random, closed: bool = False,
           amp: float = 1.2, step: float = 7.0, freq: float = 0.12) -> list[Point]:
    """Den skeva linjen: varje rak sträcka får små, mjuka avvikelser vinkelrätt mot sig själv."""
    pts = densify(points, closed, step)
    noise = Noise1D(rng)
    off = rng.uniform(0, 50)
    n = len(pts)
    out = []
    dist = 0.0
    for i, p in enumerate(pts):
        if i > 0:
            dist += math.dist(pts[i - 1], p)
        prev = pts[i - 1] if (i > 0 or closed) else p
        nxt = pts[(i + 1) % n] if (i < n - 1 or closed) else p
        tx, ty = nxt[0] - prev[0], nxt[1] - prev[1]
        ln = math.hypot(tx, ty) or 1.0
        nx, ny = -ty / ln, tx / ln
        d = noise(off + dist * freq) * amp
        out.append((p[0] + nx * d, p[1] + ny * d))
    return out


def smooth_path(points: list[Point], closed: bool) -> str:
    """Catmull-Rom genom punkterna, skrivet som kubiska Bézierkurvor."""
    if len(points) < 2:
        return ""
    pts = points
    n = len(pts)
    d = [f"M{pts[0][0]:.2f},{pts[0][1]:.2f}"]
    segs = n if closed else n - 1
    for i in range(segs):
        p0 = pts[(i - 1) % n] if (closed or i > 0) else pts[i]
        p1 = pts[i]
        p2 = pts[(i + 1) % n]
        p3 = pts[(i + 2) % n] if (closed or i + 2 < n) else p2
        c1 = (p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6)
        c2 = (p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6)
        d.append(f"C{c1[0]:.2f},{c1[1]:.2f} {c2[0]:.2f},{c2[1]:.2f} {p2[0]:.2f},{p2[1]:.2f}")
    if closed:
        d.append("Z")
    return " ".join(d)


def ellipse_points(cx: float, cy: float, rx: float, ry: float, n: int = 24, rot: float = 0.0) -> list[Point]:
    cr, sr = math.cos(rot), math.sin(rot)
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        x, y = rx * math.cos(a), ry * math.sin(a)
        pts.append((cx + x * cr - y * sr, cy + x * sr + y * cr))
    return pts


def rect_points(x: float, y: float, w: float, h: float) -> list[Point]:
    return [(x, y), (x + w, y), (x + w, y + h), (x, y + h)]


def translate(points: list[Point], dx: float, dy: float) -> list[Point]:
    return [(x + dx, y + dy) for x, y in points]


def rotate(points: list[Point], cx: float, cy: float, angle: float) -> list[Point]:
    c, s = math.cos(angle), math.sin(angle)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in points]


# ---------------------------------------------------------------------------
# Ritytan


class Canvas:
    """En SVG-bild som byggs upp lager för lager: akvarell under, bläck över."""

    def __init__(self, width: int, height: int, seed: int, fx: float = 1.0):
        """fx skalar penseldragens storlek: 1 för byggnader, mindre för små figurer."""
        self.fx = fx
        self.width = width
        self.height = height
        self.rng = random.Random(seed)
        self.defs: list[str] = []
        self.body: list[str] = []
        self._ids = 0

    def _id(self, prefix: str) -> str:
        self._ids += 1
        return f"{prefix}{self._ids}"

    # --- akvarell -----------------------------------------------------------

    def _wash_filter(self, wet: float, grain: float) -> str:
        fid = self._id("wc")
        s = self.rng.randint(1, 9999)
        self.defs.append(f"""
<filter id="{fid}" x="-25%" y="-25%" width="150%" height="150%" color-interpolation-filters="sRGB">
  <feTurbulence type="fractalNoise" baseFrequency="{0.045 / self.fx:.3f}" numOctaves="2" seed="{s}" result="warp"/>
  <feDisplacementMap in="SourceGraphic" in2="warp" scale="{3.8 * wet * self.fx:.2f}" xChannelSelector="R" yChannelSelector="G" result="shape"/>
  <feTurbulence type="fractalNoise" baseFrequency="{0.018 / max(self.fx, 0.5):.3f}" numOctaves="2" seed="{s + 3}" result="blot"/>
  <feColorMatrix in="blot" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 2.6 -0.75" result="blotMask"/>
  <feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="1" seed="{s + 7}" result="grain"/>
  <feColorMatrix in="grain" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 {-1.6 * grain:.2f} {0.6 + 0.9 * grain:.2f}" result="grainMask"/>
  <feComposite in="shape" in2="blotMask" operator="in" result="flammig"/>
  <feComposite in="flammig" in2="grainMask" operator="in" result="flammigKorn"/>
  <feComponentTransfer in="shape" result="base"><feFuncA type="linear" slope="0.62"/></feComponentTransfer>
  <feComponentTransfer in="flammigKorn" result="top"><feFuncA type="linear" slope="0.45"/></feComponentTransfer>
  <feMorphology in="shape" operator="erode" radius="{max(0.5, 1.6 * wet * self.fx):.2f}" result="inner"/>
  <feComposite in="shape" in2="inner" operator="out" result="edge"/>
  <feGaussianBlur in="edge" stdDeviation="{0.7 * self.fx:.2f}" result="edgeSoft"/>
  <feComponentTransfer in="edgeSoft" result="rim"><feFuncA type="linear" slope="0.55"/></feComponentTransfer>
  <feMerge><feMergeNode in="base"/><feMergeNode in="top"/><feMergeNode in="rim"/></feMerge>
</filter>""")
        return fid

    def wash(self, points: list[Point], color: str, opacity: float = 1.0, wet: float = 1.0,
             grain: float = 1.0, offset: float = 1.3, wobble_amp: float = 1.5, solid: bool = True) -> None:
        """
        Akvarellfyllning. Lite förskjuten mot konturen, som när färgen går utanför linjerna.
        solid lägger först en ogenomskinlig pappersfärgad botten innanför konturen, så att det
        som ligger bakom inte lyser igenom.
        """
        if solid:
            self.body.append(f'<path d="{smooth_path(densify(points, True, 6), True)}" fill="{col("papper")}"/>')
        dx = self.rng.uniform(-offset, offset) * self.fx
        dy = self.rng.uniform(-offset, offset) * self.fx
        pts = wobble(translate(points, dx, dy), self.rng, closed=True, amp=wobble_amp * self.fx, step=8 * self.fx)
        fid = self._wash_filter(wet, grain)
        self.body.append(
            f'<path d="{smooth_path(pts, True)}" fill="{col(color)}" filter="url(#{fid})" opacity="{opacity:.2f}"/>'
        )

    def glaze(self, points: list[Point], color: str, opacity: float = 0.35, blur: float = 3.0) -> None:
        """Ett tunt, mjukt lager ovanpå, för skuggor och toningar."""
        fid = self._id("gl")
        self.defs.append(
            f'<filter id="{fid}" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="{blur}"/></filter>'
        )
        pts = wobble(points, self.rng, closed=True, amp=2.5 * self.fx, step=10 * self.fx)
        self.body.append(
            f'<path d="{smooth_path(pts, True)}" fill="{col(color)}" opacity="{opacity:.2f}" filter="url(#{fid})"/>'
        )

    # --- bläck --------------------------------------------------------------

    def ink(self, points: list[Point], closed: bool = False, width: float = 1.6, color: str = "blck",
            amp: float = 0.9, taper: bool = True, opacity: float = 0.92, jitter: float = 0.45) -> None:
        """Bläckkontur med varierande tjocklek, ritad som en fylld form kring linjen."""
        pts = wobble(points, self.rng, closed=closed, amp=amp * self.fx, step=5 * self.fx)
        n = len(pts)
        if n < 2:
            return
        noise = Noise1D(self.rng)
        off = self.rng.uniform(0, 40)
        left, right = [], []
        total = sum(math.dist(pts[i], pts[i + 1]) for i in range(n - 1)) or 1.0
        dist = 0.0
        for i, p in enumerate(pts):
            if i > 0:
                dist += math.dist(pts[i - 1], p)
            prev = pts[i - 1] if (i > 0 or closed) else p
            nxt = pts[(i + 1) % n] if (i < n - 1 or closed) else p
            tx, ty = nxt[0] - prev[0], nxt[1] - prev[1]
            ln = math.hypot(tx, ty) or 1.0
            nx, ny = -ty / ln, tx / ln
            w = width * (1 + jitter * noise(off + dist * 0.08))
            if taper and not closed:
                t = dist / total
                w *= min(1.0, 0.35 + 2.2 * t, 0.35 + 2.2 * (1 - t))
            w = max(w, 0.25)
            left.append((p[0] + nx * w / 2, p[1] + ny * w / 2))
            right.append((p[0] - nx * w / 2, p[1] - ny * w / 2))
        if closed:
            d = smooth_path(left, True) + " " + smooth_path(right[::-1], True)
            self.body.append(f'<path d="{d}" fill="{col(color)}" fill-rule="evenodd" opacity="{opacity:.2f}"/>')
        else:
            outline = left + right[::-1]
            self.body.append(f'<path d="{smooth_path(outline, True)}" fill="{col(color)}" opacity="{opacity:.2f}"/>')

    def line(self, a: Point, b: Point, width: float = 1.2, color: str = "blck", amp: float = 0.6,
             opacity: float = 0.9) -> None:
        self.ink([a, b], width=width, color=color, amp=amp, opacity=opacity)

    def dot(self, x: float, y: float, r: float, color: str = "blck", opacity: float = 0.9) -> None:
        pts = wobble(ellipse_points(x, y, r, r * self.rng.uniform(0.8, 1.0), n=8), self.rng, closed=True,
                     amp=r * 0.15, step=3)
        self.body.append(f'<path d="{smooth_path(pts, True)}" fill="{col(color)}" opacity="{opacity:.2f}"/>')

    def hatch(self, region: list[Point], spacing: float = 4.0, angle: float = -0.6, width: float = 0.7,
              color: str = "blck", opacity: float = 0.45, length_jitter: float = 0.25) -> None:
        """Kryssskuggning: tunna parallella streck inom en yta, lite ojämnt dragna."""
        cid = self._id("cl")
        poly = " ".join(f"{x:.2f},{y:.2f}" for x, y in region)
        self.defs.append(f'<clipPath id="{cid}"><polygon points="{poly}"/></clipPath>')
        xs = [p[0] for p in region]
        ys = [p[1] for p in region]
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        r = math.hypot(max(xs) - min(xs), max(ys) - min(ys)) / 2 + 4
        ca, sa = math.cos(angle), math.sin(angle)
        self.body.append(f'<g clip-path="url(#{cid})">')
        k = -r
        while k <= r:
            s = r * (1 - self.rng.uniform(0, length_jitter))
            e = r * (1 - self.rng.uniform(0, length_jitter))
            a = (cx + ca * -s - sa * k, cy + sa * -s + ca * k)
            b = (cx + ca * e - sa * k, cy + sa * e + ca * k)
            self.ink([a, b], width=width, color=color, amp=0.5, opacity=opacity, jitter=0.6)
            k += spacing * self.rng.uniform(0.8, 1.2)
        self.body.append("</g>")

    def shape(self, points: list[Point], fill: str, ink_width: float = 1.6, opacity: float = 1.0,
              wet: float = 1.0, ink: bool = True) -> None:
        """Det vanligaste: en akvarellyta med bläckkontur."""
        self.wash(points, fill, opacity=opacity, wet=wet)
        if ink:
            self.ink(points, closed=True, width=ink_width)

    def tiled(self, period_x: float, period_y: float) -> "_Tiled":
        """
        Allt som ritas inom blocket upprepas med perioden, så att bilden går att lägga
        kant i kant utan skarvar. Upprepningen görs med <use>, så brus och filter blir
        exakt likadana i varje kopia.
        """
        return _Tiled(self, period_x, period_y)

    def raw(self, svg: str) -> None:
        self.body.append(svg)

    def group(self, transform: str) -> "_Group":
        return _Group(self, transform)

    def to_svg(self) -> str:
        return (
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.width}" height="{self.height}" '
            f'viewBox="0 0 {self.width} {self.height}">\n<defs>{"".join(self.defs)}\n</defs>\n'
            + "\n".join(self.body)
            + "\n</svg>\n"
        )


class _Group:
    def __init__(self, canvas: Canvas, transform: str):
        self.canvas = canvas
        self.transform = transform

    def __enter__(self):
        self.canvas.body.append(f'<g transform="{self.transform}">')
        return self.canvas

    def __exit__(self, *exc):
        self.canvas.body.append("</g>")


class _Tiled:
    def __init__(self, canvas: Canvas, px: float, py: float):
        self.canvas = canvas
        self.px = px
        self.py = py

    def __enter__(self):
        self.start = len(self.canvas.body)
        return self.canvas

    def __exit__(self, *exc):
        c = self.canvas
        items = c.body[self.start:]
        del c.body[self.start:]
        gid = c._id("tile")
        c.defs.append(f'<g id="{gid}">' + "".join(items) + "</g>")
        for dy in (-self.py, 0, self.py):
            for dx in (-self.px, 0, self.px):
                c.body.append(f'<use href="#{gid}" transform="translate({dx:.0f},{dy:.0f})"/>')
