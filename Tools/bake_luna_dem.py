#!/usr/bin/env python3
"""Crop LROC Linné crater DTM into Assets/Resources/World/LunaDem/LunaHeight.png.

Same still-window recipe as Mars: ~80 m around campus, bowl pinned to
SignatureCraterLocal, NODATA inpainted, 0.5 = campus grade.

Credit: NASA/GSFC/Arizona State University LROC NAC_DTM_LINNECRATER. Public domain.
Source TIF is not vendored.
"""
from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Resources/World/LunaDem/LunaHeight.png"
SRC = Path("/tmp/luna-dem/NAC_DTM_LINNECRATER.TIF")

MPP = 2.0
CRATER = (5929.0, 1542.0)
TERRACE = (6313.0, 2356.0)
COVER = 80.0
OUT_N = 512
HEIGHT_RANGE = 32.0
BOWL_TARGET = -8.0
RIM_TARGET = 9.0


def load_dtm(path: Path) -> np.ndarray:
    im = Image.open(path)
    elev = np.array(im, dtype=np.float64)
    elev[elev < -1e30] = np.nan
    return elev


def inpaint(elev: np.ndarray) -> np.ndarray:
    filled = elev.copy()
    missing = ~np.isfinite(filled)
    if not missing.any():
        return filled
    med = float(np.nanmedian(filled))
    filled[missing] = med
    valid0 = np.isfinite(elev)
    for _ in range(8):
        padded = np.pad(filled, 1, mode="edge")
        acc = (
            padded[:-2, :-2] + padded[:-2, 1:-1] + padded[:-2, 2:]
            + padded[1:-1, :-2] + padded[1:-1, 1:-1] + padded[1:-1, 2:]
            + padded[2:, :-2] + padded[2:, 1:-1] + padded[2:, 2:]
        ) / 9.0
        filled = np.where(valid0, elev, acc)
    return filled


def sample_dem(elev: np.ndarray, de: float, dn: float, tr: float, tc: float) -> float:
    lines, samples = elev.shape
    c = tc + de / MPP
    r = tr - dn / MPP
    c = min(samples - 2, max(1, c))
    r = min(lines - 2, max(1, r))
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
        raise SystemExit(f"Missing {SRC}. Download NAC_DTM_LINNECRATER.TIF from LROC PDS.")
    elev = inpaint(load_dtm(SRC))
    tr, tc = TERRACE
    cr, cc = CRATER
    de = (cc - tc) * MPP
    dn = (tr - cr) * MPP
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
    vert = float(np.clip(min(abs(bowl_s), abs(rim_s)), 0.012, 3.5))
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
        f"wrote {OUT} vert={vert:.4f} cover={COVER} "
        f"min={rel.min():.2f} max={rel.max():.2f} "
        f"campus={at(0,0):.2f} crater={at(-14,12):.2f} "
        f"mesa={at(16,18):.2f} canyon={at(-8,18):.2f} far={at(12,14):.2f}"
    )


if __name__ == "__main__":
    main()
