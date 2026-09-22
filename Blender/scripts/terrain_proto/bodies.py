"""Per-body natural terrain generators (prototype of TerrainDataBake v2)."""
from __future__ import annotations

import math
import numpy as np

from tproto import (fbm, eroded_fbm, ridged, gnoise, smoothstep, crater_population, stamp_craters,
                    campus_flatten, horizon_ao, hash01, CAMPUS, TAU)

W = 384.0


def grid(n=512):
    xs = np.linspace(0, W, n)
    X, Z = np.meshgrid(xs, xs)
    return X, Z, W / (n - 1)


def dist_campus(X, Z):
    return np.hypot(X - CAMPUS[0], Z - CAMPUS[1])


def settle_env(X, Z):
    """Relief envelope: gentle rolling ground where the colony grows, full relief beyond."""
    dc = np.hypot(X - CAMPUS[0], Z - CAMPUS[1])
    return 0.42 + 0.58 * smoothstep(22.0, 70.0, dc)


def seed_angle(seed, salt):
    return float(hash01(np.array([seed]), np.array([salt]), 991)[0]) * TAU


def asym_dune(t):
    """Transverse dune/TAR profile: long gentle stoss, short steep lee."""
    up = smoothstep(0.0, 0.72, t)
    down = 1.0 - smoothstep(0.72, 1.0, t)
    return np.where(t < 0.72, up, down)


def warp(X, Z, seed, scale, amount):
    wx = fbm(X / scale, Z / scale, seed + 501, 3) * amount
    wz = fbm(X / scale + 31.7, Z / scale - 12.3, seed + 502, 3) * amount
    return X + wx, Z + wz


# --- landmarks (campus-relative, match TerrainDataBake constants) -------------------------

MESA_LOCAL = (16.0, 18.0)
CANYON_LOCAL = (-8.0, 18.0)
SIG_CRATER_LOCAL = (-14.0, 12.0)
POND_LOCAL = (14.5, 6.2)


def landmark_mesa(X, Z, seed, height=5.8, radius=8.2):
    cx, cz = CAMPUS[0] + MESA_LOCAL[0], CAMPUS[1] + MESA_LOCAL[1]
    dx, dz = X - cx, Z - cz
    ang = np.arctan2(dz, dx)
    edge = radius * (1.0 + 0.16 * fbm(np.cos(ang) * 2.2 + 7, np.sin(ang) * 2.2 - 3, seed + 41, 3))
    t = np.hypot(dx, dz) / edge
    # two-tier butte: caprock plateau + talus apron, with layered steps on the cliff
    cap = smoothstep(1.02, 0.86, t)
    apron = smoothstep(1.55, 0.95, t)
    steps = np.floor(cap * 4.0) / 4.0
    cap = cap * 0.75 + steps * 0.25
    return height * (0.78 * cap + 0.22 * apron)


def landmark_channel(X, Z, seed, depth=3.3, width=6.0, length=30.0):
    """Sinuous dry channel through the canyon landmark."""
    cx, cz = CAMPUS[0] + CANYON_LOCAL[0], CAMPUS[1] + CANYON_LOCAL[1]
    u = (X - cx)  # along channel (east-west)
    wig = 3.2 * np.sin(u / 7.0) + 1.4 * np.sin(u / 3.1)
    v = (Z - cz) - wig
    along = smoothstep(length * 0.5 + 6, length * 0.5 - 4, np.abs(u))
    prof = np.clip(1.0 - (v / (width * 0.5)) ** 2, 0, None) ** 0.8
    bank = np.exp(-((np.abs(v) - width * 0.62) / 1.2) ** 2) * 0.25
    return (-depth * prof + bank * depth * 0.2) * along


def landmark_crater(X, Z, radius, depth, rim):
    cx, cz = CAMPUS[0] + SIG_CRATER_LOCAL[0], CAMPUS[1] + SIG_CRATER_LOCAL[1]
    rn = np.hypot(X - cx, Z - cz) / radius
    inside = rn < 1
    p = np.where(inside, -depth + (depth + rim) * rn ** 2, rim * np.maximum(rn, 1) ** -3.0)
    return p


# --- generators ------------------------------------------------------------------------------

def gen_mars(seed, n=512, crater_count=56):
    X, Z, step = grid(n)
    Xw, Zw = warp(X, Z, seed, 90.0, 22.0)
    plains = 14.0 * eroded_fbm(Xw / 150, Zw / 150, seed, 6, erosion=1.3)
    medium = 1.8 * fbm(Xw / 28, Zw / 28, seed + 11, 4)
    small = 0.28 * fbm(X / 5.5, Z / 5.5, seed + 13, 3)
    H = plains + medium + small

    # dune / TAR fields collect in the lows
    low = smoothstep(1.2, -2.0, plains)
    field = smoothstep(-0.05, 0.3, fbm(X / 120, Z / 120, seed + 5, 3))
    dmask = np.clip(field * (0.35 + 0.65 * low), 0, 1)
    th = seed_angle(seed, 3)
    wx, wz = math.cos(th), math.sin(th)
    s = X * wx + Z * wz + 10.0 * fbm(X / 45, Z / 45, seed + 7, 3)
    lam = 7.5
    t = np.mod(s / lam, 1.0)
    crest = 0.55 + 0.45 * fbm(X / 18, Z / 18, seed + 8, 2)
    dunes = asym_dune(t) * crest * dmask
    H += 0.62 * dunes

    # far buttes / mesas with layered cliffs
    dc = np.hypot(X - CAMPUS[0], Z - CAMPUS[1])
    blob = fbm(X / 70, Z / 70, seed + 9, 4)
    far = smoothstep(60, 95, dc)
    m1 = smoothstep(0.14, 0.2, blob)
    m2 = smoothstep(0.24, 0.31, blob)
    mesa = (0.6 * m1 + 0.4 * m2) * far
    H += 6.5 * mesa

    fresh = np.zeros_like(H)
    craters = crater_population(seed, W, crater_count * 5, 1.2, 28.0, slope=2.0, age_bias=0.85, salt=201,
                                keep=lambda x, z, r: math.hypot(x - CAMPUS[0], z - CAMPUS[1]) > 22 + r)
    stamp_craters(H, fresh, X, Z, craters, step, depth_ratio=0.14, rim_ratio=0.035)

    H *= settle_env(X, Z)
    H += landmark_mesa(X, Z, seed)
    H += landmark_channel(X, Z, seed)
    H += landmark_crater(X, Z, 7.4, 3.6, 0.8)
    H *= campus_flatten(X, Z)

    masks = dict(dunes=dunes * dmask, dune_field=dmask, mesa=mesa, fresh=fresh)
    return X, Z, step, H, masks


def gen_luna(seed, n=512, crater_count=96):
    X, Z, step = grid(n)
    Xw, Zw = warp(X, Z, seed, 110.0, 18.0)
    plains = 12.0 * eroded_fbm(Xw / 170, Zw / 170, seed, 6, erosion=1.0)
    medium = 1.4 * fbm(Xw / 30, Zw / 30, seed + 11, 4)
    small = 0.22 * fbm(X / 5, Z / 5, seed + 13, 3)
    mare = smoothstep(0.02, 0.22, fbm(X / 230, Z / 230, seed + 3, 3))
    H = (plains + medium) * (1.0 - 0.7 * mare) - 1.5 * mare + small

    fresh = np.zeros_like(H)

    def keep(x, z, r):
        if math.hypot(x - CAMPUS[0], z - CAMPUS[1]) < 20 + r:
            return False
        return True

    craters = crater_population(seed, W, crater_count * 14, 0.9, 42.0, slope=2.05, age_bias=0.5, keep=keep, salt=202)
    stamp_craters(H, fresh, X, Z, craters, step, depth_ratio=0.2, rim_ratio=0.045, rays=True)
    H *= settle_env(X, Z)
    H += landmark_crater(X, Z, 8.0, 3.4, 0.75)
    H *= campus_flatten(X, Z)
    return X, Z, step, H, dict(mare=mare, fresh=fresh)


def gen_earth(seed, n=512, lakes=8, rivers=5):
    X, Z, step = grid(n)
    Xw, Zw = warp(X, Z, seed, 120.0, 26.0)
    hills = 20.0 * eroded_fbm(Xw / 210, Zw / 210, seed, 6, erosion=0.9)
    medium = 2.2 * fbm(Xw / 40, Zw / 40, seed + 11, 4)
    small = 0.15 * fbm(X / 6, Z / 6, seed + 13, 3)
    dc = np.hypot(X - CAMPUS[0], Z - CAMPUS[1])
    rocks = 7.0 * np.clip(ridged(X / 55, Z / 55, seed + 17, 4) - 0.62, 0, None) * smoothstep(70, 110, dc)
    H = hills + medium + small + rocks
    # campus plateau is gentler (people settle on flats)
    H *= settle_env(X, Z)

    wet = np.zeros_like(H)
    water = []
    # lakes: pick low sites from candidate grid
    st = 0
    cand = []
    for i in range(160):
        x = float(hash01(np.array([i]), np.array([1]), seed)[0]) * (W - 40) + 20
        z = float(hash01(np.array([i]), np.array([2]), seed)[0]) * (W - 40) + 20
        if math.hypot(x - CAMPUS[0], z - CAMPUS[1]) < 32:
            continue
        hi = H[int(z / step), int(x / step)]
        cand.append((hi, x, z, i))
    cand.sort()
    for hi, x, z, i in cand:
        if len(water) >= lakes:
            break
        if any(math.hypot(x - w[0], z - w[1]) < 45 for w in water):
            continue
        r = 7 + float(hash01(np.array([i]), np.array([3]), seed)[0]) * 9
        ang = np.arctan2(Z - z, X - x)
        lob = 1 + 0.25 * np.sin(ang * 2 + i) + 0.15 * np.sin(ang * 3 + 2 * i)
        rn = np.hypot(X - x, Z - z) / (r * lob)
        basin = hi - 1.5 * np.clip(1 - rn ** 2, 0, None) ** 0.7
        blend = smoothstep(1.6, 1.0, rn)
        H = np.minimum(H, H * (1 - blend) + basin * blend)
        level = hi - 0.3
        wet = np.maximum(wet, smoothstep(1.45, 0.95, rn))
        water.append((x, z, r, level))
    # rivers: edge -> nearest lake (or opposite edge), meandering, carved as a continuous channel
    for k in range(rivers):
        a0 = float(hash01(np.array([k]), np.array([7]), seed)[0]) * TAU
        sx0 = W / 2 + math.cos(a0) * W * 0.75
        sz0 = W / 2 + math.sin(a0) * W * 0.75
        sx0, sz0 = min(max(sx0, 2), W - 2), min(max(sz0, 2), W - 2)
        lakes_only = [w for w in water if w[2] > 5]
        if lakes_only and k % 2 == 0:
            tgt = min(lakes_only, key=lambda w: math.hypot(w[0] - sx0, w[1] - sz0))
            ex, ez = tgt[0], tgt[1]
        else:
            a1 = a0 + math.pi * 0.55 + float(hash01(np.array([k]), np.array([8]), seed)[0]) * 0.9
            ex = min(max(W / 2 + math.cos(a1) * W * 0.75, 2), W - 2)
            ez = min(max(W / 2 + math.sin(a1) * W * 0.75, 2), W - 2)
        segs = 70
        pts = []
        L = math.hypot(ex - sx0, ez - sz0)
        nx_, nz_ = -(ez - sz0) / L, (ex - sx0) / L
        for s_ in range(segs + 1):
            t = s_ / segs
            px = sx0 + (ex - sx0) * t
            pz = sz0 + (ez - sz0) * t
            wander = (22 * float(fbm(np.array([t * 2.6 + k * 1.7]), np.array([k * 7.1]), seed + 61, 3)[0])
                      + 6 * math.sin(t * 17 + k)) * math.sin(math.pi * t) ** 0.5
            px += nx_ * wander
            pz += nz_ * wander
            # skirt the settlement
            dcx, dcz = px - CAMPUS[0], pz - CAMPUS[1]
            dd = math.hypot(dcx, dcz)
            if dd < 38:
                px, pz = CAMPUS[0] + dcx / max(dd, 1e-3) * 38, CAMPUS[1] + dcz / max(dd, 1e-3) * 38
            pts.append((px, pz))
        hs = [float(H[int(np.clip(p[1] / step, 0, n - 1)), int(np.clip(p[0] / step, 0, n - 1))]) for p in pts]
        # bed descends monotonically downstream (smoothed running minimum)
        bed = []
        cur = 1e9
        for h in hs:
            cur = min(cur, h)
            bed.append(cur)
        for idx in range(len(pts) - 1):
            (ax, az), (bx, bz) = pts[idx], pts[idx + 1]
            width = 2.2 + 1.0 * math.sin(idx * 0.37 + k) + 1.4 * idx / segs
            bedh = min(bed[idx], bed[idx + 1]) - 0.9
            r = width * 3.5
            i0 = int(max(0, (min(ax, bx) - r) / step)); i1 = int(min(n, (max(ax, bx) + r) / step + 2))
            j0 = int(max(0, (min(az, bz) - r) / step)); j1 = int(min(n, (max(az, bz) + r) / step + 2))
            if i0 >= i1 or j0 >= j1:
                continue
            xs = X[j0:j1, i0:i1]; zs = Z[j0:j1, i0:i1]
            vx, vz = bx - ax, bz - az
            ll = max(vx * vx + vz * vz, 1e-6)
            tt = np.clip(((xs - ax) * vx + (zs - az) * vz) / ll, 0, 1)
            d = np.hypot(xs - (ax + vx * tt), zs - (az + vz * tt))
            prof = bedh + (d / width) ** 2 * 0.9
            sub = H[j0:j1, i0:i1]
            blend = smoothstep(width * 3.5, width * 1.0, d)
            sub[:] = np.minimum(sub, sub * (1 - blend) + np.minimum(sub, prof) * blend)
            wet[j0:j1, i0:i1] = np.maximum(wet[j0:j1, i0:i1], smoothstep(width * 2.0, width * 0.7, d))
            water.append(((ax + bx) / 2, (az + bz) / 2, width * 0.55, bedh + 0.55))
    # vista pond near campus
    px, pz = CAMPUS[0] + POND_LOCAL[0], CAMPUS[1] + POND_LOCAL[1]
    rn = np.hypot(X - px, Z - pz) / 3.8
    H += -0.8 * np.clip(1 - rn ** 2, 0, None)
    wet = np.maximum(wet, smoothstep(1.5, 1.0, rn))
    H *= campus_flatten(X, Z)
    soil = smoothstep(0.2, 0.55, fbm(X / 26, Z / 26, seed + 21, 4))
    return X, Z, step, H, dict(wet=wet, soil=soil, water=water)


def gen_europa(seed, n=512):
    X, Z, step = grid(n)
    H = 3.2 * fbm(X / 140, Z / 140, seed, 5) + 0.12 * fbm(X / 5, Z / 5, seed + 13, 3)
    lin = np.zeros_like(H)
    for k in range(11):
        a = float(hash01(np.array([k]), np.array([31]), seed)[0]) * math.pi
        ox = float(hash01(np.array([k]), np.array([32]), seed)[0]) * W
        oz = float(hash01(np.array([k]), np.array([33]), seed)[0]) * W
        dx, dz = math.cos(a), math.sin(a)
        # signed distance to a gently curved line with wiggle
        along = (X - ox) * dx + (Z - oz) * dz
        side = -(X - ox) * dz + (Z - oz) * dx
        side = side + along * along * (float(hash01(np.array([k]), np.array([34]), seed)[0]) - 0.5) * 0.002
        side = side + 2.5 * fbm(along / 40, np.full_like(along, k * 3.7), seed + 71, 3)
        w = 2.2 + 3.0 * float(hash01(np.array([k]), np.array([35]), seed)[0])
        hgt = 0.6 + 0.9 * float(hash01(np.array([k]), np.array([36]), seed)[0])
        ad = np.abs(side)
        ridge = np.exp(-((ad - w) / (0.42 * w)) ** 2) - 0.45 * np.exp(-(ad / (0.28 * w)) ** 2)
        H = H + hgt * ridge
        lin = np.maximum(lin, smoothstep(w * 2.4, w * 0.6, ad) * (0.5 + 0.5 * k / 10))
    cracks = ridged(X / 22, Z / 22, seed + 17, 3)
    H -= 0.18 * smoothstep(0.78, 0.95, cracks)
    lin = np.maximum(lin, 0.45 * smoothstep(0.8, 0.95, cracks))
    # chaos terrain: jumbled raft blocks in one or two far regions
    chaos = np.zeros_like(H)
    for k in range(2):
        cx = float(hash01(np.array([k]), np.array([41]), seed)[0]) * (W - 120) + 60
        cz = float(hash01(np.array([k]), np.array([42]), seed)[0]) * (W - 120) + 60
        if math.hypot(cx - CAMPUS[0], cz - CAMPUS[1]) < 90:
            cx = W - cx
        r = 38 + 20 * float(hash01(np.array([k]), np.array([43]), seed)[0])
        m = smoothstep(1.0, 0.7, np.hypot(X - cx, Z - cz) / r * (1 + 0.2 * fbm(X / 20, Z / 20, seed + 44, 2)))
        cell = 10.0
        gx0 = np.floor(X / cell)
        gz0 = np.floor(Z / cell)
        best = np.full_like(X, 1e9)
        second = np.full_like(X, 1e9)
        bid_x = np.zeros_like(X)
        bid_z = np.zeros_like(X)
        for oz in (-1, 0, 1):
            for ox in (-1, 0, 1):
                cxg = gx0 + ox
                czg = gz0 + oz
                px = (cxg + hash01(cxg, czg, seed + 47)) * cell
                pz = (czg + hash01(cxg, czg, seed + 48)) * cell
                d = np.hypot(X - px, Z - pz)
                closer = d < best
                second = np.where(closer, best, np.minimum(second, d))
                bid_x = np.where(closer, cxg, bid_x)
                bid_z = np.where(closer, czg, bid_z)
                best = np.where(closer, d, best)
        h1 = hash01(bid_x, bid_z, seed + 45)
        tiltx = (hash01(bid_x, bid_z, seed + 46) - 0.5) * 0.25
        tiltz = (hash01(bid_x, bid_z, seed + 49) - 0.5) * 0.25
        blocks = (h1 - 0.3) * 2.2 + tiltx * (X - bid_x * cell) + tiltz * (Z - bid_z * cell)
        gap = smoothstep(0.6, 1.8, second - best)
        H = H * (1 - m) + (H * 0.3 + blocks * gap - 0.9 * (1 - gap)) * m
        chaos = np.maximum(chaos, m)
    fresh = np.zeros_like(H)
    craters = crater_population(seed, W, 30, 1.5, 16.0, slope=2.2, age_bias=0.1, salt=204,
                                keep=lambda x, z, r: math.hypot(x - CAMPUS[0], z - CAMPUS[1]) > 24 + r)
    stamp_craters(H, fresh, X, Z, craters, step, depth_ratio=0.16, rim_ratio=0.04)
    H *= campus_flatten(X, Z)
    return X, Z, step, H, dict(lineae=lin, chaos=chaos, fresh=fresh)


def gen_belt(seed, n=512):
    X, Z, step = grid(n)
    Xw, Zw = warp(X, Z, seed, 80.0, 20.0)
    H = 11.0 * (ridged(Xw / 110, Zw / 110, seed, 5) - 0.5) + 12.0 * eroded_fbm(Xw / 60, Zw / 60, seed + 3, 5) \
        + 1.0 * fbm(X / 11, Z / 11, seed + 11, 4)
    fresh = np.zeros_like(H)
    craters = crater_population(seed, W, 900, 1.0, 30.0, slope=2.0, age_bias=0.55, salt=205,
                                keep=lambda x, z, r: math.hypot(x - CAMPUS[0], z - CAMPUS[1]) > 20 + r)
    stamp_craters(H, fresh, X, Z, craters, step, depth_ratio=0.2, rim_ratio=0.05)
    H *= settle_env(X, Z)
    H *= campus_flatten(X, Z)
    return X, Z, step, H, dict(fresh=fresh)


GENS = dict(Mars=gen_mars, Luna=gen_luna, Earth=gen_earth, Europa=gen_europa, Belt=gen_belt)
