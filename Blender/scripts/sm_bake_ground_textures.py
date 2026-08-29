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
    wx = u + 0.14 * value_noise(u * 3.1 + 2.0, v * 3.1 + 6.0)
    wy = v + 0.14 * value_noise(u * 2.8 + 7.0, v * 2.8 + 1.5)
    n = fbm(wx * 4.0, wy * 4.0, 6)
    mott = fbm(wx * 9.0 + 3, wy * 9.0 + 7, 5)
    r = lerp(0.52, 0.76, n)
    g = lerp(0.20, 0.36, n * 0.65 + mott * 0.35)
    b = lerp(0.09, 0.18, mott)
    if mott > 0.72:
        r *= 0.88
        g *= 0.82
        b *= 0.78
    return (r, g, b)


def height_from_rgb(r: float, g: float, b: float) -> float:
    return 0.299 * r + 0.587 * g + 0.114 * b


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


def main():
    print(f"[SM] Baking {SIZE}x{SIZE} ground textures -> {OUT}")
    earth = make_albedo(earth_pixel, SIZE)
    mars = make_albedo(mars_pixel, SIZE)
    earth_n = make_normal(earth, SIZE, strength=3.5)
    mars_n = make_normal(mars, SIZE, strength=4.5)

    files = [
        (OUT / "SM_Ground_Earth_Albedo.png", earth, False),
        (OUT / "SM_Ground_Earth_Normal.png", earth_n, True),
        (OUT / "SM_Ground_Mars_Albedo.png", mars, False),
        (OUT / "SM_Ground_Mars_Normal.png", mars_n, True),
    ]
    for path, pix, is_normal in files:
        write_png(path, pix, SIZE)
        if is_normal:
            write_unity_meta_normal(path)
        else:
            write_unity_meta_albedo(path)
    print("[SM] Done.")


if __name__ == "__main__":
    main()
