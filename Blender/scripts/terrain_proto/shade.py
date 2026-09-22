"""Albedo + splat for the prototype (approximates the SM_PlanetGround v2 shader)."""
from __future__ import annotations

import numpy as np
from PIL import Image

from tproto import fbm, smoothstep, horizon_ao

import os
TEX = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets", "Resources",
                   "Environment", "Textures") + os.sep

PAL = {
    # dust, dark sand / mare / feature, rock, bright ejecta, extra (grass / ice / wet)
    "Mars": dict(dust=(0.58, 0.29, 0.14), dark=(0.31, 0.16, 0.10), rock=(0.44, 0.25, 0.14),
                 bright=(0.68, 0.40, 0.21), strata=(0.56, 0.34, 0.19)),
    "Luna": dict(dust=(0.50, 0.49, 0.47), dark=(0.27, 0.27, 0.27), rock=(0.40, 0.39, 0.38),
                 bright=(0.74, 0.73, 0.71), strata=(0.45, 0.44, 0.43)),
    "Earth": dict(dust=(0.30, 0.45, 0.17), dark=(0.19, 0.33, 0.11), rock=(0.42, 0.40, 0.36),
                  bright=(0.46, 0.52, 0.24), strata=(0.44, 0.40, 0.34), soil=(0.38, 0.30, 0.20),
                  wet=(0.34, 0.30, 0.22)),
    "Europa": dict(dust=(0.84, 0.86, 0.88), dark=(0.58, 0.40, 0.30), rock=(0.70, 0.74, 0.78),
                   bright=(0.92, 0.95, 0.98), strata=(0.72, 0.70, 0.70)),
    "Belt": dict(dust=(0.19, 0.18, 0.17), dark=(0.12, 0.115, 0.11), rock=(0.26, 0.25, 0.24),
                 bright=(0.36, 0.34, 0.32), strata=(0.24, 0.23, 0.22)),
}


def lerp(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return a + (b - a) * t


def col(c, shape):
    return np.broadcast_to(np.array(c, dtype=np.float64), shape + (3,)).copy()


def slope_of(H, step):
    gz, gx = np.gradient(H, step)
    ny = 1.0 / np.sqrt(1 + gx * gx + gz * gz)
    return 1.0 - ny


def albedo(body, X, Z, step, H, m, seed=7):
    sh = H.shape
    P = PAL[body]
    slope = slope_of(H, step)
    ao = horizon_ao(H, step)
    lap = (np.roll(H, 1, 0) + np.roll(H, -1, 0) + np.roll(H, 1, 1) + np.roll(H, -1, 1) - 4 * H) / (step * step)
    cav = np.clip(lap * 0.6, -1, 1)  # + in hollows, - on crests

    base = col(P["dust"], sh)
    macro = fbm(X / 70, Z / 70, seed + 301, 4)
    mid = fbm(X / 14, Z / 14, seed + 302, 3)
    rock = smoothstep(0.10, 0.34, slope)

    if body == "Mars":
        dark = np.clip(m["dunes"] * 1.2 + 0.35 * m["dune_field"] + 0.4 * smoothstep(0.02, 0.2, cav), 0, 1)
        dark *= smoothstep(-0.25, 0.25, macro) * 0.6 + 0.4
        dark = np.maximum(dark, 0.45 * smoothstep(0.02, 0.28, fbm(X / 150, Z / 150, seed + 311, 3)))
        base = lerp(base, col(P["dark"], sh), dark * 0.75)
        strata = 0.5 + 0.5 * np.sin(H * 4.2 + 1.3 * fbm(X / 9, Z / 9, seed + 303, 2))
        rc = lerp(col(P["rock"], sh), col(P["strata"], sh), strata)
        base = lerp(base, rc, rock)
        base = lerp(base, col(P["bright"], sh), m["fresh"] * 0.6)
    elif body == "Luna":
        base = lerp(base, col(P["dark"], sh), m["mare"] * 0.85)
        base = lerp(base, col(P["rock"], sh), rock * 0.6)
        base = lerp(base, col(P["bright"], sh), np.clip(m["fresh"], 0, 1) * 0.85)
    elif body == "Earth":
        soil = np.clip(m["soil"] * 0.6 + rock * 0.5, 0, 1)
        grassvar = lerp(col(P["dust"], sh), col(P["dark"], sh), smoothstep(-0.2, 0.25, macro))
        grassvar = lerp(grassvar, col(P["bright"], sh), smoothstep(0.1, 0.35, mid) * 0.5)
        base = lerp(grassvar, col(P["soil"], sh), soil * 0.55)
        base = lerp(base, col(P["rock"], sh), smoothstep(0.2, 0.45, slope))
        base = lerp(base, col(P["wet"], sh), m["wet"] * 0.8)
    elif body == "Europa":
        base = lerp(base, col(P["rock"], sh), rock * 0.4)
        base = lerp(base, col(P["dark"], sh), np.clip(m["lineae"] * 0.85 + m["chaos"] * 0.55, 0, 1))
        base = lerp(base, col(P["bright"], sh), m["fresh"] * 0.8)
    else:
        base = lerp(base, col(P["rock"], sh), rock * 0.7)
        base = lerp(base, col(P["dark"], sh), smoothstep(0.02, 0.25, cav) * 0.6)
        base = lerp(base, col(P["bright"], sh), m["fresh"] * 0.7)

    var = 1.0 + 0.10 * macro + 0.07 * mid
    base = base * var[..., None]
    base = base * (0.55 + 0.45 * ao)[..., None]
    return np.clip(base, 0, 1), dict(slope=slope, ao=ao)


def detail_tile(body):
    name = "SM_Ground_Earth_Albedo.png" if body == "Earth" else "SM_Ground_Mars_Albedo.png"
    im = np.asarray(Image.open(TEX + name).convert("RGB")).astype(np.float64) / 255.0
    lum = im.mean(-1)
    return lum / lum.mean(), im


def upsample_with_detail(body, alb, X, Z, out_n=2048, tile_m=8.5):
    """Bilinear upsample + anti-tiled grit (two scales, rotated) like the shader."""
    img = Image.fromarray((alb * 255).astype(np.uint8)).resize((out_n, out_n), Image.BICUBIC)
    a = np.asarray(img).astype(np.float64) / 255.0
    lum, rgb = detail_tile(body)
    t = lum.shape[0]
    xs = np.linspace(0, 384, out_n)
    XX, ZZ = np.meshgrid(xs, xs)

    def samp(scale, rot):
        c, s = np.cos(rot), np.sin(rot)
        u = (c * XX - s * ZZ) / scale
        v = (s * XX + c * ZZ) / scale
        iu = (np.mod(u, 1) * t).astype(int) % t
        iv = (np.mod(v, 1) * t).astype(int) % t
        return lum[iv, iu]

    d1 = samp(tile_m, 0.0)
    d2 = samp(tile_m * 2.7, 0.9)
    w = smoothstep(-0.2, 0.2, fbm(XX / 20, ZZ / 20, 909, 2))
    d = d1 * (1 - w) + d2 * w
    strength = 0.45 if body != "Europa" else 0.15
    a = a * (1 + (d - 1) * strength)[..., None]
    return np.clip(a, 0, 1)
