"""
Bake seamless Earth/Mars ground albedo + normal PNGs for Solar Majesty.

Run from repo root:
  python Blender/scripts/sm_bake_ground_textures.py
"""

from __future__ import annotations

import math
import struct
import zlib
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
OUT = SCRIPT_DIR.parent.parent / "Assets" / "Resources" / "Environment" / "Textures"
SIZE = 1024


def _hash2(ix: int, iy: int) -> float:
    n = ix * 374761393 + iy * 668265263
    n = (n ^ (n >> 13)) * 1274126177
    return ((n ^ (n >> 16)) & 0x7FFFFFFF) / 2147483647.0


def _smooth(t: float) -> float:
    return t * t * (3.0 - 2.0 * t)


def value_noise(x: float, y: float) -> float:
    x0 = math.floor(x)
    y0 = math.floor(y)
    fx = _smooth(x - x0)
    fy = _smooth(y - y0)
    v00 = _hash2(x0, y0)
    v10 = _hash2(x0 + 1, y0)
    v01 = _hash2(x0, y0 + 1)
    v11 = _hash2(x0 + 1, y0 + 1)
    a = v00 * (1 - fx) + v10 * fx
    b = v01 * (1 - fx) + v11 * fx
    return a * (1 - fy) + b * fy


def fbm(x: float, y: float, octaves: int = 5) -> float:
    amp = 0.5
    freq = 1.0
    total = 0.0
    for _ in range(octaves):
        total += amp * value_noise(x * freq, y * freq)
        freq *= 2.0
        amp *= 0.5
    return total


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def earth_pixel(u: float, v: float) -> tuple[float, float, float]:
    # Domain-warped fBm — soft meadow, no hard square patches that tile into a grid.
    wx = u + 0.18 * value_noise(u * 2.3 + 1.7, v * 2.3 + 4.1)
    wy = v + 0.18 * value_noise(u * 2.1 + 8.3, v * 2.1 + 2.9)
    n = fbm(wx * 4.2, wy * 4.2, 6)
    soil = fbm(wx * 2.4 + 11.0, wy * 2.4 + 5.0, 5)
    moisture = fbm(wx * 1.6 + 20.0, wy * 1.6 + 9.0, 4)
    grass_g = lerp(0.30, 0.50, n)
    grass_r = lerp(0.16, 0.30, soil * 0.7 + moisture * 0.3)
    grass_b = lerp(0.09, 0.17, n * 0.4 + moisture * 0.2)
    if soil > 0.68 and moisture < 0.42:
        return (
            lerp(0.36, 0.48, soil),
            lerp(0.28, 0.36, soil),
            lerp(0.15, 0.22, soil),
        )
    if moisture > 0.72:
        return (grass_r * 0.85, grass_g * 0.95, grass_b * 1.15)
    return (grass_r, grass_g, grass_b)


def mars_pixel(u: float, v: float) -> tuple[float, float, float]:
    """Scalar fallback regolith. Prefer :func:`mars_regolith` — this path's "pebbles" are
    single-pixel hash noise, which mips away to a flat tint at isometric range, and its noise
    lattice does not wrap so the tile shows a seam every 8.5 m."""
    wx = u + 0.12 * value_noise(u * 3.1 + 2.0, v * 3.1 + 6.0)
    wy = v + 0.12 * value_noise(u * 2.8 + 7.0, v * 2.8 + 1.5)
    n = fbm(wx * 3.6, wy * 3.6, 6)
    mott = fbm(wx * 8.4 + 3, wy * 8.4 + 7, 5)
    # Anisotropic wind ripples — readable at iso without a grid tile.
    ripple = value_noise(wx * 18.0 + wy * 2.4, wy * 3.2)
    ripple2 = value_noise(wx * 7.0 + wy * 11.0, wy * 6.5 + 4.0)
    grit = _hash2(int(u * 1024) & 1023, int(v * 1024) & 1023)

    r = lerp(0.50, 0.78, n * 0.72 + ripple * 0.18)
    g = lerp(0.18, 0.38, n * 0.55 + mott * 0.30 + ripple2 * 0.15)
    b = lerp(0.08, 0.17, mott * 0.7 + ripple * 0.3)

    # Iron-oxide dark veins (not craters).
    if mott > 0.70:
        r *= 0.86
        g *= 0.80
        b *= 0.76
    # Packed-dust flats — slightly lighter, matte (campus aprons share this language).
    if n > 0.62 and mott < 0.45:
        r = lerp(r, 0.80, 0.18)
        g = lerp(g, 0.40, 0.12)
        b = lerp(b, 0.16, 0.08)
    # Sparse pebbles.
    if grit > 0.975:
        r = lerp(r, 0.42, 0.55)
        g = lerp(g, 0.22, 0.55)
        b = lerp(b, 0.12, 0.55)
    elif grit > 0.955:
        r *= 0.92
        g *= 0.90
        b *= 0.88

    # Fine dust sheen — keep midtones, do not blow out.
    sheen = 0.97 + ripple * 0.04
    return (max(0.08, min(0.92, r * sheen)), max(0.06, min(0.50, g * sheen)), max(0.04, min(0.24, b)))


def height_from_rgb(r: float, g: float, b: float) -> float:
    return 0.299 * r + 0.587 * g + 0.114 * b


# --------------------------------------------------------------------------------------
# Mars regolith (vectorised, periodic)
#
# The Mars tile repeats every 8.5 m over 1024 px, i.e. ~120 px per metre. Pebbles therefore
# have to be authored as real shapes tens of pixels across; per-pixel hash noise is
# sub-millimetre and disappears into the mip chain, which is why the ground reads as one flat
# tint in the campus stills no matter how high _DetailTexAmount goes.
#
# Every lattice here wraps modulo its own integer frequency, so the tile is genuinely seamless.
# Requires numpy; falls back to the scalar path above when it is missing.
# --------------------------------------------------------------------------------------

LUM = (0.299, 0.587, 0.114)

# Per-channel means of the tile this replaces. The shader multiplies the grade by this tile, so
# holding the means fixed keeps the tuned Mars exposure and avoids re-grading the whole body.
MARS_TARGET_RGB = (0.700, 0.362, 0.167)


def _np():
    try:
        import numpy
    except ImportError:
        return None
    return numpy


def _hash01(np, ix, iy, seed: int):
    """Deterministic [0,1) hash of an integer lattice cell."""
    n = (ix.astype("int64") * 374761393 + iy.astype("int64") * 668265263 + seed * 2654435761)
    n = n.astype("uint64")
    n = (n ^ (n >> np.uint64(13))) * np.uint64(1274126177)
    n = n ^ (n >> np.uint64(16))
    return (n & np.uint64(0x7FFFFFFF)).astype("float64") / 2147483647.0


def _vnoise(np, x, y, period_x: int, period_y: int = 0, seed: int = 0):
    """Value noise whose lattice wraps at `period_x` / `period_y` cells, so it tiles exactly.

    Anisotropic ripples need independent periods: sampling a stretched field through one square
    period leaves the shorter axis unwrapped and puts a seam back in the tile."""
    period_y = period_y or period_x
    x0 = np.floor(x)
    y0 = np.floor(y)
    fx = _smooth_np(np, x - x0)
    fy = _smooth_np(np, y - y0)
    ix = x0.astype("int64") % period_x
    iy = y0.astype("int64") % period_y
    ix1 = (ix + 1) % period_x
    iy1 = (iy + 1) % period_y
    v00 = _hash01(np, ix, iy, seed)
    v10 = _hash01(np, ix1, iy, seed)
    v01 = _hash01(np, ix, iy1, seed)
    v11 = _hash01(np, ix1, iy1, seed)
    a = v00 + (v10 - v00) * fx
    b = v01 + (v11 - v01) * fx
    return a + (b - a) * fy


def _smooth_np(np, t):
    return t * t * (3.0 - 2.0 * t)


def _fbm(np, u, v, base: int, octaves: int, seed: int = 0):
    total = np.zeros_like(u)
    amp = 0.5
    freq = base
    for o in range(octaves):
        total += amp * _vnoise(np, u * freq, v * freq, freq, freq, seed + o * 17)
        freq *= 2
        amp *= 0.5
    return total


# Light direction the stone shading assumes, matching the Mars key light in
# CelestialBodyCatalog.BuildMars (SunEuler 20/-62): low, from the upper left of the tile.
STONE_LIGHT = (-0.42, 0.55, 0.72)


def _stones(np, u, v, freq: int, seed: int, r_lo: float, r_hi: float, presence: float):
    """Sparse jittered-grid stone field with directional shading.

    Returns (dome, shade, shadow). `dome` is the hemisphere height inside each stone, `shade` its
    Lambert term under STONE_LIGHT, and `shadow` the cast shadow on the dust down-sun of it.
    `presence` is the fraction of cells that actually hold a stone: filling every cell reads as
    polka dots, not regolith.

    The radius is modulated by noise per pixel so outlines come out lumpy. Perfect circles read as
    ball bearings dropped on the sand however well they are lit."""
    fu = u * freq
    fv = v * freq
    iu = np.floor(fu).astype("int64")
    iv = np.floor(fv).astype("int64")
    lumpy = 0.80 + 0.44 * _vnoise(np, u * freq * 2, v * freq * 2, freq * 2, freq * 2, seed + 5)

    lx, ly, lz = STONE_LIGHT
    lnorm = (lx * lx + ly * ly) ** 0.5
    nearest = np.full(u.shape, 1e9)
    off_x = np.zeros_like(u)
    off_y = np.zeros_like(u)
    shadow = np.zeros_like(u)

    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            cu = (iu + dx) % freq
            cv = (iv + dy) % freq
            keep = _hash01(np, cu * 29 + 13, cv * 31 + 17, seed) < presence
            jx = _hash01(np, cu * 3 + 1, cv * 7 + 5, seed)
            jy = _hash01(np, cu * 11 + 3, cv * 5 + 9, seed)
            rad = (r_lo + (r_hi - r_lo) * _hash01(np, cu * 17 + 7, cv * 23 + 11, seed)) * lumpy
            dxp = (fu - ((iu + dx) + jx)) / rad
            dyp = (fv - ((iv + dy) + jy)) / rad
            dist = np.where(keep, np.sqrt(dxp * dxp + dyp * dyp), 1e9)

            closer = dist < nearest
            nearest = np.where(closer, dist, nearest)
            off_x = np.where(closer, dxp, off_x)
            off_y = np.where(closer, dyp, off_y)

            # Cast shadow: the same disc, offset down-sun and only outside the stone itself.
            sx = dxp + lx / lnorm * 0.55
            sy = dyp + ly / lnorm * 0.55
            sd = np.where(keep, np.sqrt(sx * sx + sy * sy), 1e9)
            shadow = np.maximum(shadow, np.clip((1.05 - sd) / 0.45, 0.0, 1.0))

    dome = np.sqrt(np.maximum(0.0, 1.0 - np.minimum(nearest, 1.0) ** 2))
    shade = np.clip(off_x * lx + off_y * ly + dome * lz, 0.0, 1.0)
    shadow = np.clip(shadow - np.clip(dome * 3.0, 0.0, 1.0), 0.0, 1.0)
    return dome, shade, shadow


def mars_regolith(size: int):
    """Return (albedo, height) float arrays for the Mars ground tile, or None without numpy."""
    np = _np()
    if np is None:
        return None

    t = (np.arange(size, dtype="float64") + 0.5) / size
    u, v = np.meshgrid(t, t)

    # Domain-warped base grade: metre-scale drifts of dust over darker crust.
    wu = u + 0.10 * _vnoise(np, u * 3, v * 3, 3, 3, 21)
    wv = v + 0.10 * _vnoise(np, u * 3, v * 3, 3, 3, 47)
    base = _fbm(np, wu, wv, 4, 5, 101)
    mottle = _fbm(np, wu, wv, 8, 4, 233)
    # Decimetre mottle. Without it the dust between the stones is a smooth gradient and reads as
    # plastic no matter how strong the per-pixel grain is.
    fine = _fbm(np, u, v, 16, 5, 557)

    # Wind ripples: high frequency across the drift, low along it, so they read as lines rather
    # than blobs. The shear is a whole x-period per tile so the diagonal still wraps. Kept low
    # contrast — pushed further it banded the whole tile diagonally.
    ripple = _vnoise(np, u * 26 + v * 26, v * 5, 26, 5, 311)
    ripple = 0.5 + 0.5 * np.sin((ripple * 2.0 - 1.0) * 1.5)
    ripple_fine = _vnoise(np, u * 64 + v * 64, v * 9, 64, 9, 419)

    # Every channel tracks `base`. Leaving blue independent of it made the low-lying dust drift
    # purple, because b/r climbed wherever the red dropped.
    r = 0.43 + 0.26 * base + 0.05 * ripple + 0.19 * fine
    g = 0.155 + 0.14 * base + 0.06 * mottle + 0.02 * ripple + 0.09 * fine
    b = 0.048 + 0.065 * base + 0.05 * mottle + 0.01 * ripple + 0.038 * fine

    # Iron-oxide veins — darker crust showing through, not craters (craters are mesh props).
    vein = np.clip((mottle - 0.62) * 3.0, 0.0, 1.0)
    r *= 1.0 - 0.16 * vein
    g *= 1.0 - 0.22 * vein
    b *= 1.0 - 0.24 * vein

    # Packed-dust flats: the same language the campus aprons use.
    flat = np.clip((base - 0.58) * 3.2, 0.0, 1.0) * (1.0 - vein)
    r += 0.10 * flat
    g += 0.05 * flat

    height = 0.35 * base + 0.16 * mottle + 0.06 * ripple + 0.02 * ripple_fine

    # Two sparse stone fields: cobbles ~10-20 cm across and chips ~3-7 cm, both wide enough to
    # survive mipping at overseer range. Lit from the same side as the Mars key light so they read
    # as loose stone with a bright crown and a cast shadow, not as flat discs. Stones stay in the
    # dust's own red-brown family; pushing the lit term higher bleached them to grey pebbles.
    # Tones are the dust's own hue scaled down in value. An independently chosen "stone grey"
    # carries more blue relative to red than the dust does and the stones came out reading as
    # blue-grey holes punched in the sand.
    for freq, seed, r_lo, r_hi, presence, value, weight, lift in (
        (34, 601, 0.16, 0.38, 0.055, 0.94, 1.00, 0.20),
        (96, 733, 0.13, 0.30, 0.13, 0.98, 0.60, 0.11),
    ):
        dome, shade, shadow = _stones(np, u, v, freq, seed, r_lo, r_hi, presence)
        # Half-buried: a wide blend at the rim puts drifted dust against each stone instead of
        # cutting it out of the sand with a hard edge.
        mask = np.clip(dome * 1.9, 0.0, 1.0) * weight
        # The crown has to go brighter than the surrounding dust and the far side darker. Held
        # entirely below 1.0 the stones read as dents punched into the sand instead of sitting on it.
        lit = value * (0.62 + 0.92 * shade)
        stone = 1.0 - mask + lit * mask
        r *= stone
        g *= stone
        b *= stone
        dark = 1.0 - 0.26 * shadow
        r *= dark
        g *= dark
        b *= dark
        height += lift * dome

    # Per-pixel sand grain, applied over the stones so their surfaces carry it too. This is the
    # texture that already read well on the previous tile and it is what keeps the dust from
    # looking like smooth plastic between the stones.
    xi = (u * size).astype("int64") % size
    yi = (v * size).astype("int64") % size
    grain = _hash01(np, xi, yi, 877)
    sand = 0.84 + 0.32 * grain
    r *= sand
    g *= sand
    b *= sand

    # Dark mineral specks. A soft darkening reads as noise; a hard lerp toward the rock tone is
    # what gives the dust its bite on the previous tile.
    speck = (grain > 0.960).astype("float64") * 0.55
    r = r * (1.0 - speck) + 0.30 * speck
    g = g * (1.0 - speck) + 0.14 * speck
    b = b * (1.0 - speck) + 0.07 * speck

    albedo = np.clip(np.stack([r, g, b], axis=-1), 0.02, 1.0)

    # Hold the per-channel means of the tile this replaces: the shader multiplies the body grade
    # by this map, so drifting the mean would silently re-expose all of Mars.
    for c in range(3):
        albedo[:, :, c] *= MARS_TARGET_RGB[c] / max(float(albedo[:, :, c].mean()), 1e-4)
    albedo = np.clip(albedo, 0.0, 1.0)

    height -= height.min()
    height /= max(float(height.max()), 1e-6)
    return albedo, height


def normal_from_height(height, strength: float = 6.0):
    """Tangent-space normal from a real height field, wrapping at the tile edge.

    The albedo-derived path treats a dark iron-oxide vein as a dent. Deriving from height keeps
    the relief on the stones where it belongs."""
    np = _np()
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * strength
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * strength
    nx = -dx
    ny = -dy
    nz = np.ones_like(height)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.clip(np.stack([nx * inv, ny * inv, nz * inv], axis=-1) * 0.5 + 0.5, 0.0, 1.0)


def write_png_array(path: Path, rgb) -> None:
    """PNG writer for an (h, w, 3) float array in 0..1."""
    np = _np()
    size = rgb.shape[0]
    px = (np.clip(rgb, 0.0, 1.0) * 255.0 + 0.5).astype("uint8")
    rows = np.concatenate(
        [np.zeros((size, 1), dtype="uint8"), px.reshape(size, size * 3)], axis=1)

    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(
            ">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0)
    png = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
           + chunk(b"IDAT", zlib.compress(rows.tobytes(), 9)) + chunk(b"IEND", b""))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)
    print(f"[SM] Wrote {path} ({len(png)} bytes)")


def make_albedo(fn, size: int) -> list[tuple[float, float, float]]:
    pixels = []
    for y in range(size):
        for x in range(size):
            u = x / size
            v = y / size
            pixels.append(fn(u, v))
    return pixels


def make_normal(albedo: list[tuple[float, float, float]], size: int, strength: float = 4.0) -> list[tuple[float, float, float]]:
    def h(x: int, y: int) -> float:
        return height_from_rgb(*albedo[(y % size) * size + (x % size)])

    out = []
    for y in range(size):
        for x in range(size):
            dx = (h(x + 1, y) - h(x - 1, y)) * strength
            dy = (h(x, y + 1) - h(x, y - 1)) * strength
            nx, ny, nz = -dx, -dy, 1.0
            inv = 1.0 / math.sqrt(nx * nx + ny * ny + nz * nz)
            nx, ny, nz = nx * inv, ny * inv, nz * inv
            out.append(((nx + 1) * 0.5, (ny + 1) * 0.5, (nz + 1) * 0.5))
    return out


def write_png(path: Path, pixels: list[tuple[float, float, float]], size: int):
    """Minimal RGB PNG writer (no Pillow required)."""
    raw = bytearray()
    for y in range(size):
        raw.append(0)  # filter None
        for x in range(size):
            r, g, b = pixels[y * size + x]
            raw.append(max(0, min(255, int(r * 255))))
            raw.append(max(0, min(255, int(g * 255))))
            raw.append(max(0, min(255, int(b * 255))))

    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)
    print(f"[SM] Wrote {path} ({len(png)} bytes)")


def write_unity_meta_albedo(path: Path):
    import hashlib
    guid = hashlib.md5(path.name.encode()).hexdigest()
    meta = path.with_suffix(path.suffix + ".meta")
    meta.write_text(
        f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings: []
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
        encoding="utf-8",
    )


def write_unity_meta_normal(path: Path):
    import hashlib
    guid = hashlib.md5(path.name.encode()).hexdigest()
    meta = path.with_suffix(path.suffix + ".meta")
    meta.write_text(
        f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 0
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 1
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings: []
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
        encoding="utf-8",
    )


def bake_mars(force: bool) -> bool:
    """Vectorised periodic regolith. Returns False when numpy is unavailable."""
    baked = mars_regolith(SIZE)
    if baked is None:
        print("[SM] numpy missing — Mars falls back to the scalar tile.")
        return False

    albedo, height = baked
    jobs = [
        (OUT / "SM_Ground_Mars_Albedo.png", albedo, write_unity_meta_albedo),
        (OUT / "SM_Ground_Mars_Normal.png", normal_from_height(height, 6.0),
         write_unity_meta_normal),
    ]
    for path, arr, meta in jobs:
        if path.exists() and not force:
            print(f"[SM] Skip existing {path.name} (pass --force to overwrite authored tiles)")
            continue
        write_png_array(path, arr)
        meta(path)
    return True


def main():
    import sys
    # --force is scoped per body on purpose. The committed Earth tile is authored, not a product
    # of earth_pixel, so a global --force silently replaced a good meadow with flat scalar noise.
    force = "--force" in sys.argv
    only = [a[2:] for a in sys.argv[1:] if a in ("--mars", "--earth")]
    do_mars = not only or "mars" in only
    do_earth = not only or "earth" in only
    print(f"[SM] Baking {SIZE}x{SIZE} ground textures -> {OUT}")

    mars_vectorised = bake_mars(force) if do_mars else True

    files = []
    if do_earth:
        earth = make_albedo(earth_pixel, SIZE)
        files.append((OUT / "SM_Ground_Earth_Albedo.png", earth, False))
        files.append((OUT / "SM_Ground_Earth_Normal.png",
                      make_normal(earth, SIZE, strength=3.5), True))
    if do_mars and not mars_vectorised:
        mars = make_albedo(mars_pixel, SIZE)
        files.append((OUT / "SM_Ground_Mars_Albedo.png", mars, False))
        files.append((OUT / "SM_Ground_Mars_Normal.png",
                      make_normal(mars, SIZE, strength=4.5), True))

    for path, pix, is_normal in files:
        if path.exists() and not force:
            print(f"[SM] Skip existing {path.name} (pass --force to overwrite authored tiles)")
            continue
        write_png(path, pix, SIZE)
        if is_normal:
            write_unity_meta_normal(path)
        else:
            write_unity_meta_albedo(path)
    print("[SM] Done.")


if __name__ == "__main__":
    main()
