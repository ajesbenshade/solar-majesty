"""Numpy prototype of the natural-planet terrain generator.

Mirrors (intended to match) Assets/Scripts/Runtime/World/TerrainNoise.cs + TerrainDataBake.cs so the
renders here predict the in-game look. All distances are metres.
"""
from __future__ import annotations

import math
import numpy as np

U32 = np.uint32
TAU = math.tau

# --- hashing / noise -------------------------------------------------------------------

def hash_u32(x, z, seed):
    x = np.asarray(x).astype(np.int64).astype(np.uint32)
    z = np.asarray(z).astype(np.int64).astype(np.uint32)
    s = np.uint32(seed & 0xFFFFFFFF)
    with np.errstate(over="ignore"):
        h = (x * U32(0x8DA6B343)) ^ (z * U32(0xD8163841)) ^ (s * U32(0xCB1AB31F))
        h ^= h >> U32(16)
        h *= U32(0x7FEB352D)
        h ^= h >> U32(15)
        h *= U32(0x846CA68B)
        h ^= h >> U32(16)
    return h


def hash01(x, z, seed):
    return (hash_u32(x, z, seed) & U32(0xFFFFFF)).astype(np.float64) / 16777216.0


_GX = np.array([math.cos(i / 16 * TAU + 0.19634954) for i in range(16)])
_GZ = np.array([math.sin(i / 16 * TAU + 0.19634954) for i in range(16)])


def gnoise(x, z, seed):
    """2D gradient noise with analytic derivatives. Returns (v, dvdx, dvdz); v ~ [-1, 1]."""
    ix = np.floor(x)
    iz = np.floor(z)
    fx = x - ix
    fz = z - iz
    ix = ix.astype(np.int64)
    iz = iz.astype(np.int64)
    u = fx * fx * fx * (fx * (fx * 6 - 15) + 10)
    v = fz * fz * fz * (fz * (fz * 6 - 15) + 10)
    du = 30 * fx * fx * (fx - 1) ** 2
    dv = 30 * fz * fz * (fz - 1) ** 2

    def g(ox, oz):
        h = hash_u32(ix + ox, iz + oz, seed) & U32(15)
        return _GX[h], _GZ[h]

    gax, gaz = g(0, 0)
    gbx, gbz = g(1, 0)
    gcx, gcz = g(0, 1)
    gdx, gdz = g(1, 1)
    va = gax * fx + gaz * fz
    vb = gbx * (fx - 1) + gbz * fz
    vc = gcx * fx + gcz * (fz - 1)
    vd = gdx * (fx - 1) + gdz * (fz - 1)
    k = va - vb - vc + vd
    val = va + u * (vb - va) + v * (vc - va) + u * v * k
    dx = gax + u * (gbx - gax) + v * (gcx - gax) + u * v * (gax - gbx - gcx + gdx) + du * (vb - va + v * k)
    dz = gaz + u * (gbz - gaz) + v * (gcz - gaz) + u * v * (gaz - gbz - gcz + gdz) + dv * (vc - va + u * k)
    s = 1.4142
    return val * s, dx * s, dz * s


# octave rotation (~36.87 deg) breaks grid alignment between octaves
_RC, _RS = 0.8, 0.6


def fbm(x, z, seed, octaves, lac=2.0, gain=0.5):
    f = np.zeros_like(x)
    a = 0.5
    px, pz = x, z
    for i in range(octaves):
        n, _, _ = gnoise(px, pz, seed + i * 131)
        f += a * n
        px, pz = (_RC * px - _RS * pz) * lac, (_RS * px + _RC * pz) * lac
        a *= gain
    return f


def eroded_fbm(x, z, seed, octaves, lac=2.0, gain=0.5, erosion=1.0):
    """IQ-style derivative-damped fBm: slopes suppress fine octaves -> eroded, natural relief."""
    f = np.zeros_like(x)
    ddx = np.zeros_like(x)
    ddz = np.zeros_like(x)
    a = 0.5
    px, pz = x, z
    # track rotation so derivatives accumulate in a consistent frame
    c, s = 1.0, 0.0
    scale = 1.0
    for i in range(octaves):
        n, nx, nz = gnoise(px, pz, seed + i * 131)
        # derivative back into base frame (inverse rotation), weighted by octave frequency
        bx = (c * nx + s * nz) * scale
        bz = (-s * nx + c * nz) * scale
        ddx += bx * a
        ddz += bz * a
        f += a * n / (1.0 + erosion * (ddx * ddx + ddz * ddz))
        px, pz = (_RC * px - _RS * pz) * lac, (_RS * px + _RC * pz) * lac
        c, s = c * _RC - s * _RS, s * _RC + c * _RS
        scale *= lac
        a *= gain
    return f


def ridged(x, z, seed, octaves, lac=2.0, gain=0.5):
    f = np.zeros_like(x)
    w = np.ones_like(x)
    a = 0.5
    px, pz = x, z
    for i in range(octaves):
        n, _, _ = gnoise(px, pz, seed + i * 131)
        r = 1.0 - np.abs(n)
        r = r * r * w
        w = np.clip(r * 2.0, 0.0, 1.0)
        f += a * r
        px, pz = (_RC * px - _RS * pz) * lac, (_RS * px + _RC * pz) * lac
        a *= gain
    return f


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# --- craters ----------------------------------------------------------------------------

class Crater:
    __slots__ = ("x", "z", "r", "age", "fresh", "seed")

    def __init__(self, x, z, r, age, seed):
        self.x, self.z, self.r, self.age, self.seed = x, z, r, age, seed
        self.fresh = 1.0 - age


def rng_stream(seed, salt):
    """Deterministic LCG-free stream built on the same hash (portable to C#)."""
    i = 0
    while True:
        yield float(hash01(np.array([i]), np.array([salt]), seed)[0])
        i += 1


def crater_population(seed, width, count, rmin, rmax, slope=2.0, age_bias=0.5, salt=77, keep=None):
    """Power-law sizes: N(>R) ~ R^-slope. age in [0,1], 1 = heavily degraded."""
    out = []
    st = rng_stream(seed, salt)
    tries = 0
    while len(out) < count and tries < count * 6:
        tries += 1
        u = next(st)
        r = rmin * (1.0 - u * (1.0 - (rmin / rmax) ** slope)) ** (-1.0 / slope)
        x = next(st) * width
        z = next(st) * width
        age = next(st) ** (1.0 / max(0.05, 1 - age_bias + 0.5))
        if keep is not None and not keep(x, z, r):
            continue
        out.append(Crater(x, z, r, age, int(next(st) * 1e6)))
    # older first so younger craters overprint
    out.sort(key=lambda c: -c.age)
    return out


def crater_profile(rn, depth, rim, flat=0.0):
    """rn = d / R. Bowl inside, raised rim at 1, ejecta falling as rn^-3 outside."""
    inside = rn < 1.0
    p = np.where(inside, -depth + (depth + rim) * np.power(np.clip(rn, 0, 1), 2.0), 0.0)
    if flat > 0:
        p = np.where(inside, np.maximum(p, -depth * (1 - flat)), p)
    out = rim * np.power(np.maximum(rn, 1.0), -3.0) - rim * 0.03
    p = np.where(inside, p, np.maximum(out, 0.0))
    # soften rim crest
    crest = np.exp(-((rn - 1.0) / 0.12) ** 2) * rim * 0.15
    return p + crest


def stamp_craters(H, fresh_mask, X, Z, craters, step, depth_ratio=0.2, rim_ratio=0.045, rays=False,
                  campus=None):
    n = H.shape[0]
    for c in craters:
        ext = c.r * (5.5 if (rays and c.fresh > 0.8 and c.r > 3.0) else 2.6 if c.fresh > 0.6 else 1.8)
        i0 = max(0, int((c.x - ext) / step))
        i1 = min(n, int((c.x + ext) / step) + 2)
        j0 = max(0, int((c.z - ext) / step))
        j1 = min(n, int((c.z + ext) / step) + 2)
        if i0 >= i1 or j0 >= j1:
            continue
        xs = X[j0:j1, i0:i1]
        zs = Z[j0:j1, i0:i1]
        dx = xs - c.x
        dz = zs - c.z
        d = np.sqrt(dx * dx + dz * dz)
        ang = np.arctan2(dz, dx)
        # irregular rim (few lobes)
        wob = 1.0 + 0.06 * np.sin(ang * 3 + c.seed % 7) + 0.04 * np.sin(ang * 5 + c.seed % 11)
        rn = d / (c.r * wob)
        deg = c.age
        depth = depth_ratio * 2 * c.r * (1.0 - 0.72 * deg)
        rim = rim_ratio * 2 * c.r * (1.0 - 0.8 * deg)
        flat = 0.35 if c.r > 18 else 0.0
        prof = crater_profile(rn, depth, rim, flat)
        if c.r > 24:  # central peak on the largest
            prof += np.exp(-(rn / 0.16) ** 2) * depth * 0.45 * (1 - deg)
        # younger craters overprint the floor of whatever they land on
        hc = H[min(n - 1, int(c.z / step)), min(n - 1, int(c.x / step))]
        k = smoothstep(1.15, 0.85, rn) * (0.55 + 0.45 * c.fresh)
        sub = H[j0:j1, i0:i1]
        sub[:] = sub * (1 - k) + (hc * 0.6 + sub * 0.4) * k + prof
        if fresh_mask is not None and c.fresh > 0.45:
            f = (c.fresh - 0.45) / 0.55
            ej = np.clip(np.power(np.maximum(rn, 0.9), -2.2), 0, 1) * smoothstep(0.7, 1.0, rn)
            inner = smoothstep(1.0, 0.6, rn) * 0.35
            m = (ej + inner) * f
            if rays and c.fresh > 0.8 and c.r > 3.0:
                nray = 7 + c.seed % 6
                g, _, _ = gnoise(ang * 6.0, rn * 1.3, c.seed)
                ray = np.power(np.abs(np.sin(ang * nray * 1.5 + c.seed)), 40) * (0.5 + 0.5 * g) * \
                    smoothstep(5.5, 1.2, rn) * smoothstep(0.9, 1.3, rn)
                m = m + np.maximum(ray, 0) * 0.55 * f
            fm = fresh_mask[j0:j1, i0:i1]
            fm[:] = np.maximum(fm, np.clip(m, 0, 1))


# --- campus flatten (matches TerrainDataBake.CampusFlatten) ------------------------------

CAMPUS = (192.0, 192.0)
CAMPUS_B = (222.0, 218.0)
PAD = (192.0 + 16.0, 192.0)  # LaunchSite.PadWorld


def falloff(X, Z, o, radius, blend):
    d = np.hypot(X - o[0], Z - o[1])
    t = np.clip((d - radius) / blend, 0, 1)
    return t * t * (3 - 2 * t)


def campus_flatten(X, Z):
    k = falloff(X, Z, CAMPUS, 12.0, 5.0)
    k = np.minimum(k, falloff(X, Z, PAD, 8.0, 5.0))
    k = np.minimum(k, falloff(X, Z, CAMPUS_B, 10.0, 5.0))
    return k


# --- AO ----------------------------------------------------------------------------------

def horizon_ao(H, step, dirs=8, steps=10, max_dist=24.0):
    n = H.shape[0]
    ao = np.zeros_like(H)
    for k in range(dirs):
        a = k / dirs * TAU
        cx, cz = math.cos(a), math.sin(a)
        best = np.full_like(H, 0.0)
        dist = step
        for s in range(steps):
            dist = step * (1.6 ** s)
            if dist > max_dist:
                break
            ox = int(round(cx * dist / step))
            oz = int(round(cz * dist / step))
            sh = np.roll(np.roll(H, -oz, axis=0), -ox, axis=1)
            slope = (sh - H) / dist
            best = np.maximum(best, slope)
        ao += 1.0 - np.sin(np.arctan(best))
    return ao / dirs
