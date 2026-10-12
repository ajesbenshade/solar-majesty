#!/usr/bin/env python3
"""Procedural Solar Majesty application icon set.

Renders the app icon (dark metallic tile, gold HUD bevel, rust planet with a gold orbit ring and
the Colony Commons dome on its horizon) at every standalone size Unity asks for, straight from
signed-distance shapes, so each size is drawn for its own pixel grid instead of being shrunk from
the 1024 master. Small sizes drop the fine detail and thicken the strokes so they still read.

Needs only Python 3 + numpy (PNG is written with zlib).

    python3 Tools/release/icons/make_app_icons.py            # writes Assets/Art/Icons/
    python3 Tools/release/icons/make_app_icons.py out_dir    # writes somewhere else
"""
import os
import struct
import sys
import zlib

import numpy as np

SIZES = [1024, 512, 256, 128, 64, 48, 32, 16]
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
DEFAULT_OUT = os.path.join(REPO, "Assets", "Art", "Icons")


def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


# HUD palette (matches the gold / orange Overseer chrome).
GOLD_HI = hexc("ffe29a")
GOLD = hexc("f2b23e")
ORANGE = hexc("e8701e")
GOLD_LO = hexc("8a4a12")
STEEL_TOP = hexc("343b45")
STEEL_BOT = hexc("0d1014")
RUST_DARK = hexc("3a130b")
RUST = hexc("b8482a")
RUST_HI = hexc("f29a5c")
CREAM = hexc("f4efe4")
CREAM_SHADE = hexc("a9a49a")


def png(path, rgba):
    a = (np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8)
    h, w, _ = a.shape
    raw = b"".join(b"\x00" + a[y].tobytes() for y in range(h))

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


class Canvas:
    """Premultiplied RGBA canvas in normalised coordinates: x right, y up, [-1, 1]."""

    def __init__(self, size, ss):
        n = size * ss
        self.n = n
        self.px = 2.0 / n  # one supersample in normalised units
        c = (np.arange(n) + 0.5) / n * 2.0 - 1.0
        self.x, self.y = np.meshgrid(c, -c)
        self.rgb = np.zeros((n, n, 3))
        self.a = np.zeros((n, n))

    def cov(self, d, soft=1.0):
        """Coverage from a signed distance (negative inside), anti-aliased over ~1 sample."""
        return np.clip(0.5 - d / (self.px * soft), 0.0, 1.0)

    def over(self, color, alpha):
        alpha = np.clip(alpha, 0, 1)
        if np.ndim(color) == 1:
            color = np.broadcast_to(color, self.rgb.shape)
        self.rgb = color * alpha[..., None] + self.rgb * (1 - alpha[..., None])
        self.a = alpha + self.a * (1 - alpha)

    def add(self, color, amount):
        if np.ndim(color) == 1:
            color = np.broadcast_to(color, self.rgb.shape)
        self.rgb = self.rgb + color * np.clip(amount, 0, None)[..., None] * self.a[..., None]

    def resolve(self, ss):
        n = self.n // ss
        rgb = self.rgb.reshape(n, ss, n, ss, 3).mean(axis=(1, 3))
        a = self.a.reshape(n, ss, n, ss).mean(axis=(1, 3))
        out = np.zeros((n, n, 4))
        safe = np.maximum(a, 1e-6)[..., None]
        out[..., :3] = np.where(a[..., None] > 0, rgb / safe, 0)
        out[..., 3] = a
        return out


def lerp(a, b, t):
    t = np.clip(t, 0, 1)[..., None]
    return a * (1 - t) + b * t


def ramp(stops, t):
    """Piecewise-linear colour ramp; stops = [(pos, rgb), ...]."""
    t = np.clip(t, 0, 1)
    out = np.zeros(t.shape + (3,))
    for (p0, c0), (p1, c1) in zip(stops[:-1], stops[1:]):
        m = (t >= p0) & (t <= p1)
        k = (t - p0) / max(p1 - p0, 1e-6)
        out[m] = lerp(c0, c1, k)[m]
    return out


def squircle_sd(x, y, r, n=5.0):
    ax, ay = np.abs(x), np.abs(y)
    return (ax ** n + ay ** n) ** (1.0 / n) - r


def render(size):
    small = size <= 32
    tiny = size <= 16
    ss = 4 if size <= 256 else 2
    cv = Canvas(size, ss)
    x, y, px = cv.x, cv.y, cv.px
    # Stroke widths never fall below roughly one output pixel.
    out_px = 2.0 / size

    # --- Tile ---------------------------------------------------------------------------------
    margin = 0.90 if size >= 128 else (0.95 if size >= 48 else 0.99)
    tile = squircle_sd(x, y, margin)
    base = lerp(STEEL_TOP, STEEL_BOT, (0.85 - y) / 1.7)
    # Brushed sheen: a soft diagonal highlight band across the metal.
    sheen = np.exp(-((x * 0.55 + y * 0.85 - 0.35) / 0.32) ** 2) * 0.10
    base = base + sheen[..., None] * hexc("cfd8e6")
    if not small:
        rng = np.random.default_rng(7)
        streak = rng.normal(0, 1, (cv.n, 1)) * 0.012
        base = base + streak  # fine horizontal brushing
    vign = np.clip(1.0 - 0.35 * (x ** 2 + y ** 2), 0.55, 1)
    cv.over(base * vign[..., None], cv.cov(tile))

    # Gold bevel just inside the tile edge.
    bevel_w = 0.035 if not small else max(0.045, 0.8 * out_px)
    band = np.maximum(tile, -(tile + bevel_w))  # <0 inside the band
    gold_edge = ramp([(0, GOLD_LO), (0.45, ORANGE), (0.75, GOLD), (1, GOLD_HI)], (y + 1) / 2)
    cv.over(gold_edge, cv.cov(band))
    if not small:
        inner = np.abs(tile + bevel_w + 0.006) - 0.004
        cv.over(hexc("05070a"), cv.cov(inner) * 0.85)

    # HUD corner ticks on the bigger sizes.
    if size >= 128:
        t = 0.010
        for sx in (-1, 1):
            for sy in (-1, 1):
                if sx > 0 and sy > 0:
                    continue  # the star glint owns that corner
                cx, cy = sx * 0.66, sy * 0.66
                h = np.maximum(np.abs(x - (cx - sx * 0.06)) - 0.06, np.abs(y - cy) - t)
                v = np.maximum(np.abs(x - cx) - t, np.abs(y - (cy - sy * 0.06)) - 0.06)
                cv.over(ORANGE, cv.cov(np.minimum(h, v)) * 0.55)

    # --- Planet geometry ------------------------------------------------------------------------
    pcx, pcy = 0.0, -0.06
    pr = 0.43 if not small else (0.47 if not tiny else 0.54)
    dx, dy = x - pcx, y - pcy
    pd = np.sqrt(dx ** 2 + dy ** 2) - pr

    # Warm glow behind the planet.
    glow = np.exp(-np.maximum(pd, 0) / 0.16) * (pd > 0) * 0.55
    cv.add(ORANGE * 0.6, glow * (tile < 0))

    # Orbit ring: tilted ellipse.
    tilt = np.deg2rad(-16)
    rx = dx * np.cos(tilt) - dy * np.sin(tilt)
    ry = dx * np.sin(tilt) + dy * np.cos(tilt)
    ra, rb = (0.80, 0.22) if not small else ((0.86, 0.25) if not tiny else (0.92, 0.27))
    ring_w = max(0.030 if not small else 0.065, 0.9 * out_px)
    # Approximate distance to the ellipse outline.
    k = np.sqrt((rx / ra) ** 2 + (ry / rb) ** 2)
    grad = np.sqrt((rx / ra ** 2) ** 2 + (ry / rb ** 2) ** 2) / np.maximum(k, 1e-6)
    ell = (k - 1.0) / np.maximum(grad, 1e-6)
    ring_sd = np.abs(ell) - ring_w
    ring_col = ramp([(0, ORANGE), (0.5, GOLD), (1, GOLD_HI)], (rx / ra + 1) / 2)
    back = ry > 0
    # Far side of the ring dims smoothly instead of switching at the crossing points.
    far = np.clip(0.5 + ry / 0.12, 0, 1)
    ring_col = ring_col * (1 - 0.38 * far)[..., None]

    cv.over(ring_col, cv.cov(ring_sd) * back)

    # --- Planet body ------------------------------------------------------------------------------
    inside = np.clip(1 - (dx ** 2 + dy ** 2) / pr ** 2, 0, 1)
    nz = np.sqrt(inside)
    nx, ny = dx / pr, dy / pr
    lx, ly, lz = -0.55, 0.55, 0.63
    lam = np.clip(nx * lx + ny * ly + nz * lz, 0, 1)
    shade = lam ** 0.9
    # Latitude bands and a few craters break up the surface on the bigger sizes.
    if not small:
        lat = ny + 0.08 * np.sin(nx * 7.0)
        bands = 0.05 * np.sin(lat * 13.0) + 0.03 * np.sin(lat * 29.0 + 1.3)
        shade = shade * (1 + bands)
        for (cx, cy, cr) in [(-0.18, 0.05, 0.07), (0.12, -0.17, 0.05), (0.2, 0.15, 0.035),
                             (-0.05, -0.28, 0.04)]:
            cd = np.sqrt((x - (pcx + cx)) ** 2 + (y - (pcy + cy)) ** 2)
            crater = np.exp(-((cd - cr) / (cr * 0.35)) ** 2) * 0.10 - (cd < cr) * 0.08
            shade = shade + crater * nz
    body = ramp([(0, RUST_DARK), (0.45, RUST), (0.85, RUST_HI), (1, hexc("ffd2a0"))], shade)
    # Gold rim light from behind, opposite the key light.
    rim = np.clip(1 - nz, 0, 1) ** 3 * np.clip(nx * 0.7 - ny * 0.7 + 0.25, 0, 1)
    body = body + rim[..., None] * GOLD * 0.9
    cv.over(body, cv.cov(pd))

    # --- Colony Commons dome on the planet's horizon --------------------------------------------
    if not tiny:
        ang = np.deg2rad(106)
        ux, uy = np.cos(ang), np.sin(ang)  # outward normal at the dome site
        tx, ty = uy, -ux  # tangent
        sink = 0.03
        ox, oy = pcx + ux * (pr - sink), pcy + uy * (pr - sink)
        lx_ = (x - ox) * tx + (y - oy) * ty  # along the horizon
        ly_ = (x - ox) * ux + (y - oy) * uy  # height above it
        s = 0.85 if not small else 1.25
        plinth_w, plinth_h = 0.135 * s, 0.032 * s
        drum_w, drum_h = 0.098 * s, 0.036 * s
        dome_r = 0.098 * s
        plinth = np.maximum(np.abs(lx_) - plinth_w, np.abs(ly_ - plinth_h / 2) - plinth_h / 2)
        drum_y0 = plinth_h
        drum = np.maximum(np.abs(lx_) - drum_w, np.abs(ly_ - (drum_y0 + drum_h / 2)) - drum_h / 2)
        dome_cy = drum_y0 + drum_h
        dome = np.maximum(np.sqrt(lx_ ** 2 + (ly_ - dome_cy) ** 2) - dome_r, -(ly_ - dome_cy))
        cup = np.maximum(np.abs(lx_) - 0.014 * s, np.abs(ly_ - (dome_cy + dome_r + 0.008 * s)) - 0.016 * s)
        shape = np.minimum(np.minimum(plinth, drum), np.minimum(dome, cup))
        # Dark outline so the white dome separates from the orange glow at every size.
        outline = max(0.012, 0.9 * out_px)
        cv.over(hexc("0b0c0f"), cv.cov(shape - outline))
        # Light from upper-left in tile space -> shade by the dome's local x.
        lit = np.clip(0.75 - 0.9 * (lx_ / (dome_r + 1e-6)) * 0.5 + 0.15 * (ly_ - dome_cy) / dome_r, 0, 1)
        dome_col = lerp(CREAM_SHADE, CREAM, lit)
        cv.over(dome_col, cv.cov(shape))
        band_sd = np.maximum(np.abs(lx_) - drum_w, np.abs(ly_ - (dome_cy - 0.012 * s)) - 0.012 * s)
        cv.over(ORANGE, cv.cov(np.maximum(band_sd, shape)))
        if not small:
            # Window slits on the drum.
            for wx in (-0.05, -0.017, 0.017, 0.05):
                win = np.maximum(np.abs(lx_ - wx * s) - 0.008 * s,
                                 np.abs(ly_ - (drum_y0 + drum_h * 0.42)) - 0.012 * s)
                cv.over(hexc("1b2430"), cv.cov(win))
            # Beacon on the cupola.
            bx, by = ox + ux * (dome_cy + dome_r + 0.026 * s), oy + uy * (dome_cy + dome_r + 0.026 * s)
            bd = np.sqrt((x - bx) ** 2 + (y - by) ** 2)
            cv.add(GOLD_HI, np.exp(-(bd / 0.022) ** 2) * 0.9)

    # --- Front half of the ring, with its shadow on the planet --------------------------------
    front = ~back
    shadow = cv.cov(np.abs(ell - ring_w * 2.2) - ring_w * 1.2, soft=6.0) * front * (pd < 0)
    cv.over(hexc("1a0904"), shadow * 0.35)
    cv.over(hexc("140a04"), cv.cov(ring_sd - max(0.008, 0.6 * out_px)) * front * 0.8)
    cv.over(ring_col, cv.cov(ring_sd) * front)
    if not small:
        hl = np.abs(ell + ring_w * 0.35) - ring_w * 0.25
        cv.over(GOLD_HI, cv.cov(hl) * front * 0.55)

    # --- Star glint -------------------------------------------------------------------------------
    if size >= 48:
        gx, gy = 0.58, 0.58
        gdx, gdy = np.abs(x - gx), np.abs(y - gy)
        arm = 0.12 if size >= 128 else 0.15
        star = np.minimum(np.maximum(gdx - arm, gdy - 0.010 - 0.02 * (1 - gdx / arm)),
                          np.maximum(gdy - arm, gdx - 0.010 - 0.02 * (1 - gdy / arm)))
        cv.over(GOLD_HI, cv.cov(star) * (tile < -0.05))
        cv.add(GOLD_HI, np.exp(-((gdx ** 2 + gdy ** 2) / 0.004)) * 0.6 * (tile < -0.05))

    # Clip everything to the tile.
    clip = cv.cov(tile)
    cv.rgb = cv.rgb * (clip[..., None] / np.maximum(cv.a, 1e-6)[..., None]).clip(0, 1) * (cv.a > 0)[..., None]
    cv.a = np.minimum(cv.a, clip)
    return cv.resolve(ss)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    os.makedirs(out, exist_ok=True)
    sizes = [int(v) for v in os.environ.get("SM_ICON_SIZES", "").split(",") if v] or SIZES
    for s in sizes:
        path = os.path.join(out, "SM_AppIcon_%d.png" % s)
        png(path, render(s))
        print("wrote", path)


if __name__ == "__main__":
    main()
