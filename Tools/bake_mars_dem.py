#!/usr/bin/env python3
"""Crop HiRISE Victoria DTM into Assets/Resources/World/MarsDem/MarsHeight.png.

Zooms the crater + rim into the ortho-10 still (~80 m around campus) so bowls and
mesa lips actually read. NODATA is inpainted; we do not fill off-DTM pixels with
a clipped plateau.

Credit: NASA/JPL-Caltech/UArizona. Public domain. Source IMG is not vendored.
"""
from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Resources/World/MarsDem/MarsHeight.png"
SRC = Path("/tmp/mars-dem/victoria.IMG")

LINES, SAMPLES, HEADER = 1694, 1279, 5116
MPP = 1.0118550737574
# Deepest bowl pixel; outer terrace south-east of the rim (flat enough for campus).
CRATER = (837.0, 757.0)
TERRACE = (883.0, 929.0)
# World metres the PNG covers around campus (still is ~40 m). Full 384 m map
# clamps outside this window.
COVER = 80.0
OUT_N = 512
HEIGHT_RANGE = 32.0
# Target: bowl ~-7 m, far rim ~+10 m, after subtracting campus grade.
BOWL_TARGET = -7.2
RIM_TARGET = 10.0


def load_dtm(path: Path) -> np.ndarray:
    raw = path.read_bytes()
    arr = np.frombuffer(raw[HEADER:], dtype="<f4").reshape(LINES, SAMPLES)
    elev = arr.astype(np.float64)
    elev[arr.view(np.uint32) == 0xFF7FFFFB] = np.nan
    return elev


def inpaint(elev: np.ndarray) -> np.ndarray:
    filled = elev.copy()
    missing = ~np.isfinite(filled)
    if not missing.any():
        return filled
    med = float(np.nanmedian(filled))
    filled[missing] = med
    # Pull border NODATA toward nearest valid with a few box-filter passes.
    valid0 = np.isfinite(elev)
    for _ in range(12):
        padded = np.pad(filled, 1, mode="edge")
        acc = (
            padded[:-2, :-2] + padded[:-2, 1:-1] + padded[:-2, 2:]
            + padded[1:-1, :-2] + padded[1:-1, 1:-1] + padded[1:-1, 2:]
            + padded[2:, :-2] + padded[2:, 1:-1] + padded[2:, 2:]
        ) / 9.0
        filled = np.where(valid0, elev, acc)
    return filled


def sample_dem(elev: np.ndarray, de: float, dn: float, tr: float, tc: float) -> float:
    c = tc + de / MPP
    r = tr - dn / MPP
    if c < 1 or r < 1 or c > SAMPLES - 2 or r > LINES - 2:
        c = min(SAMPLES - 2, max(1, c))
        r = min(LINES - 2, max(1, r))
    r0, c0 = int(math.floor(r)), int(math.floor(c))
    fr, fc = r - r0, c - c0
    acc = wt = 0.0
    for dr, dc, w in (
        (0, 0, (1 - fr) * (1 - fc)),
        (0, 1, (1 - fr) * fc),
        (1, 0, fr * (1 - fc)),
        (1, 1, fr * fc),
    ):
        v = elev[r0 + dr, c0 + dc]
        if np.isfinite(v):
            acc += v * w
            wt += w
    return acc / wt if wt > 1e-6 else float(elev[r0, c0])


def main() -> None:
    if not SRC.exists():
        raise SystemExit(f"Missing {SRC}. Download DTEEC_001414_1780_001612_1780_U01.IMG")
    elev = inpaint(load_dtm(SRC))
    tr, tc = TERRACE
    cr, cc = CRATER
    de = (cc - tc) * MPP
    dn = (tr - cr) * MPP
    # Pin the bowl to SignatureCraterLocal (-14, 12).
    target_e, target_n = -14.0, 12.0
    horiz = math.hypot(de, dn) / math.hypot(target_e, target_n)
    yaw = math.atan2(target_n, target_e) - math.atan2(dn, de)
    cy, sy = math.cos(-yaw), math.sin(-yaw)

    grid = np.empty((OUT_N, OUT_N), dtype=np.float64)
    half = COVER * 0.5
    for j in range(OUT_N):
        lz = j / (OUT_N - 1) * COVER - half
        for i in range(OUT_N):
            lx = i / (OUT_N - 1) * COVER - half
            east = (lx * cy - lz * sy) * horiz
            north = (lx * sy + lz * cy) * horiz
            grid[j, i] = sample_dem(elev, east, north, tr, tc)

    campus = grid[OUT_N // 2, OUT_N // 2]
    rel0 = grid - campus
    p10 = float(np.percentile(rel0, 10))
    p90 = float(np.percentile(rel0, 90))
    bowl_s = BOWL_TARGET / p10 if p10 < -0.5 else 1.0
    rim_s = RIM_TARGET / p90 if p90 > 0.5 else 1.0
    vert = float(np.clip(min(bowl_s, rim_s), 0.15, 3.5))
    rel = np.clip(rel0 * vert, -15.5, 15.5)
    enc = np.clip((rel + HEIGHT_RANGE * 0.5) / HEIGHT_RANGE, 0.0, 1.0)
    u16 = np.flipud((enc * 65535.0 + 0.5).astype(np.uint16))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(u16).save(OUT)

    def at(lx: float, lz: float) -> float:
        i = int(round((lx + half) / COVER * (OUT_N - 1)))
        j = int(round((lz + half) / COVER * (OUT_N - 1)))
        i = min(OUT_N - 1, max(0, i))
        j = min(OUT_N - 1, max(0, j))
        return float(rel[j, i])

    print(
        f"wrote {OUT} vert={vert:.3f} cover={COVER} "
        f"min={rel.min():.2f} max={rel.max():.2f} "
        f"campus={at(0,0):.2f} crater={at(-14,12):.2f} "
        f"mesa={at(16,18):.2f} canyon={at(-8,18):.2f} far={at(12,14):.2f}"
    )


if __name__ == "__main__":
    main()
