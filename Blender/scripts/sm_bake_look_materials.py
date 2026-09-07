"""
Bake seamless PBR look tiles for Solar Majesty hull / steel / solar / canvas / dusty metal.

Run from repo root:
  python Blender/scripts/sm_bake_look_materials.py
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from sm_bake_ground_textures import (
    _hash2,
    fbm,
    lerp,
    make_normal,
    value_noise,
    write_png,
    write_unity_meta_albedo,
    write_unity_meta_normal,
)

OUT = SCRIPT_DIR.parent.parent / "Assets" / "Resources" / "Art" / "Materials"
LOOK_SIZE = 512


def white_hull(u: float, v: float) -> tuple[float, float, float]:
    panel = 1.0 / 8.0
    px = (u % panel) / panel
    py = (v % panel) / panel
    seam = 1.0 if (px < 0.035 or py < 0.035 or px > 0.965 or py > 0.965) else 0.0
    grit = _hash2(int(u * 512) & 511, int(v * 512) & 511)
    dirt = fbm(u * 6.0 + 2.0, v * 6.0 + 1.0, 4)
    shell = (0.96 + grit * 0.03 - dirt * 0.04, 0.96 + grit * 0.03 - dirt * 0.035, 0.95 + grit * 0.02)
    if seam:
        return (0.22, 0.23, 0.25)
    # Rivets at panel corners.
    if (0.08 < px < 0.14 or 0.86 < px < 0.92) and (0.08 < py < 0.14 or 0.86 < py < 0.92):
        return (0.14, 0.14, 0.15)
    return shell


def steel(u: float, v: float) -> tuple[float, float, float]:
    stroke = value_noise(u * 48.0, v * 3.2)
    n = fbm(u * 5.0, v * 9.0, 4)
    dust = fbm(u * 3.0 + 8.0, v * 3.0 + 2.0, 3)
    r = lerp(0.40, 0.62, stroke * 0.65 + n * 0.35)
    g = lerp(0.42, 0.64, stroke * 0.65 + n * 0.35)
    b = lerp(0.45, 0.66, stroke * 0.65 + n * 0.35)
    # Settled Mars dust in recesses — sheen, not a full orange wash.
    if dust > 0.62:
        r = lerp(r, 0.62, 0.18)
        g = lerp(g, 0.40, 0.12)
        b = lerp(b, 0.24, 0.10)
    return (r, g, b)


def dusty_metal(u: float, v: float) -> tuple[float, float, float]:
    stroke = value_noise(u * 36.0, v * 4.0)
    n = fbm(u * 4.2 + 3.0, v * 4.2, 5)
    r = lerp(0.28, 0.48, n)
    g = lerp(0.24, 0.38, n * 0.7 + stroke * 0.3)
    b = lerp(0.20, 0.32, stroke)
    grit = _hash2(int(u * 400) & 399, int(v * 400) & 399)
    if grit > 0.96:
        r = lerp(r, 0.70, 0.25)
        g = lerp(g, 0.42, 0.18)
    return (r, g, b)


def solar(u: float, v: float) -> tuple[float, float, float]:
    cell = 1.0 / 8.0
    px = (u % cell) / cell
    py = (v % cell) / cell
    bus = px < 0.04 or py < 0.04
    finger = abs(px - 0.5) < 0.02
    n = value_noise(u * 22.0, v * 22.0)
    # Blue-black PV with wet/dust sheen.
    r = lerp(0.06, 0.12, n)
    g = lerp(0.10, 0.18, n)
    b = lerp(0.20, 0.38, n)
    if bus:
        return (0.52, 0.54, 0.58)
    if finger:
        return (lerp(r, 0.45, 0.35), lerp(g, 0.48, 0.35), lerp(b, 0.55, 0.25))
    # Dust film on cells.
    dust = fbm(u * 7.0, v * 7.0, 3)
    r = lerp(r, 0.42, dust * 0.12)
    g = lerp(g, 0.28, dust * 0.08)
    return (r, g, b)


def canvas(u: float, v: float) -> tuple[float, float, float]:
    # Soft warp/weft, domain-warped so it reads as taut fabric not a screen door.
    wu = u + 0.035 * value_noise(u * 9.0, v * 2.5)
    wv = v + 0.035 * value_noise(u * 2.5, v * 9.0)
    warp = 0.5 + 0.5 * math.sin(wu * math.tau * 16.0)
    weft = 0.5 + 0.5 * math.sin(wv * math.tau * 14.0)
    weave = warp * 0.35 + weft * 0.35
    fold = fbm(u * 2.6 + 1.0, v * 2.6, 5)
    stain = fbm(u * 1.5 + 9.0, v * 1.5, 3)
    t = fold * 0.55 + weave * 0.25 + stain * 0.20
    r = lerp(0.66, 0.90, t)
    g = lerp(0.54, 0.78, t)
    b = lerp(0.34, 0.52, fold * 0.7 + stain * 0.3)
    return (r, g, b)


def make_albedo(fn, size: int) -> list[tuple[float, float, float]]:
    pixels = []
    for y in range(size):
        for x in range(size):
            pixels.append(fn(x / size, y / size))
    return pixels


def main():
    print(f"[SM] Baking {LOOK_SIZE}x{LOOK_SIZE} look tiles -> {OUT}")
    jobs = [
        ("SM_Mat_WhiteHull_Albedo.png", white_hull, False),
        ("SM_Mat_Steel_Albedo.png", steel, False),
        ("SM_Mat_DustyMetal_Albedo.png", dusty_metal, False),
        ("SM_Mat_Solar_Albedo.png", solar, False),
        ("SM_Mat_Canvas_Albedo.png", canvas, False),
    ]
    albedos = {}
    for name, fn, _ in jobs:
        pix = make_albedo(fn, LOOK_SIZE)
        albedos[name] = pix
        path = OUT / name
        write_png(path, pix, LOOK_SIZE)
        write_unity_meta_albedo(path)

    normals = [
        ("SM_Mat_WhiteHull_Normal.png", "SM_Mat_WhiteHull_Albedo.png", 3.2),
        ("SM_Mat_Steel_Normal.png", "SM_Mat_Steel_Albedo.png", 2.8),
        ("SM_Mat_Canvas_Normal.png", "SM_Mat_Canvas_Albedo.png", 2.2),
    ]
    for name, src, strength in normals:
        n = make_normal(albedos[src], LOOK_SIZE, strength=strength)
        path = OUT / name
        write_png(path, n, LOOK_SIZE)
        write_unity_meta_normal(path)
    print("[SM] Look tiles done.")


if __name__ == "__main__":
    main()
