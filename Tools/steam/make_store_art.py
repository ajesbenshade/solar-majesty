"""
Solar Majesty: application icon, wordmark and Steam store/library art, reproducibly.

    python Tools/steam/make_store_art.py icon            # Assets/Art/Icon + Docs/StoreArt icons
    python Tools/steam/make_store_art.py store [--shots DIR]
    python Tools/steam/make_store_art.py all [--shots DIR]
    python Tools/steam/make_store_art.py logo            # the wordmark alone, for a quick look

`store` composes the capsules from in-game key-art renders. Capture new ones with

    powershell -ExecutionPolicy Bypass -File Tools\\qa\\store_screenshots.ps1 -GameDir Builds\\SteamRC\\Windows -Keyart

(which writes <world>_keyart_<pose>[_golden].png, 5760 px wide, into Builds\\StoreShots). The
renders a slot uses are copied into Docs/StoreArt/source and composition always reads those
copies, so without new renders `store` rebuilds the committed art exactly. Docs/StoreArt/README.md
lists every output and where it goes in Steamworks.

Needs Pillow and numpy. The wordmark font is Cinzel Black (SIL OFL 1.1, Blender/fonts), the same
face as the 3D title in the intro.
"""
from __future__ import annotations

import argparse
import math
import os
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FONT = os.path.join(ROOT, "Blender", "fonts", "Cinzel-Black.ttf")
ICON_DIR = os.path.join(ROOT, "Assets", "Art", "Icon")
OUT = os.path.join(ROOT, "Docs", "StoreArt")
SRC = os.path.join(OUT, "source")

# Palette, taken from the intro title (ivory hull, copper trim, cyan accent, dusk sky).
IVORY_HI = (252, 247, 238)
IVORY_LO = (214, 200, 182)
COPPER_HI = (250, 190, 120)
COPPER_MID = (214, 128, 64)
COPPER_LO = (120, 52, 22)
CYAN = (120, 225, 245)
INK = (24, 14, 22)


# ---------------------------------------------------------------------------------------------
# Small raster helpers (float RGBA layers in numpy, composited "over")
# ---------------------------------------------------------------------------------------------

def vgradient(w, h, stops):
    """Vertical gradient image from [(t, rgb), ...]."""
    ys = np.linspace(0.0, 1.0, h)[:, None]
    out = np.zeros((h, 1, 3), np.float32)
    ts = [s[0] for s in stops]
    cs = np.array([s[1] for s in stops], np.float32)
    for c in range(3):
        out[:, 0, c] = np.interp(ys[:, 0], ts, cs[:, c])
    return Image.fromarray(np.repeat(out, w, axis=1).clip(0, 255).astype(np.uint8), "RGB")


def radial(w, h, cx, cy, r, inner, outer, power=1.0):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / max(r, 1e-3)
    t = np.clip(d, 0, 1) ** power
    a = np.array(inner, np.float32)
    b = np.array(outer, np.float32)
    img = a[None, None, :] * (1 - t[..., None]) + b[None, None, :] * t[..., None]
    mode = "RGBA" if len(inner) == 4 else "RGB"
    return Image.fromarray(img.clip(0, 255).astype(np.uint8), mode)


def solid(size, rgb, mask):
    layer = Image.new("RGBA", size, rgb + (0,))
    layer.putalpha(mask)
    return layer


def fill_with(img_rgb, mask):
    layer = img_rgb.convert("RGBA")
    layer.putalpha(mask)
    return layer


def shift(mask, dx, dy):
    return ImageChops.offset(mask, int(dx), int(dy))


def mask_mul(a, b):
    return ImageChops.multiply(a, b)


def dilate(mask, px):
    """Grow a mask by about px pixels (Gaussian spread + low threshold: fast, round corners)."""
    px = float(px)
    if px <= 0.5:
        return mask
    sigma = px / 2.3
    blurred = mask.filter(ImageFilter.GaussianBlur(sigma))
    return blurred.point(lambda v: 0 if v < 1 else min(255, int((v - 1) * 255 / 5)))


# ---------------------------------------------------------------------------------------------
# The ringed planet (icon emblem and the "O" of the wordmark)
# ---------------------------------------------------------------------------------------------

def planet_emblem(size, crown=True, detail=True, ring_frac=0.17, inlay=True, tilt=-16.0):
    """
    RGBA ringed planet (the ivory hull planet of the intro title) with an optional crown, on
    transparent, `size` px square. Drawn supersampled, then reduced.
    ring_frac: ring thickness as a fraction of the planet radius (thicker for tiny icons).
    """
    S = min(max(size * 3, 384), 3072)
    cx, cy = S * 0.5, S * (0.58 if crown else 0.5)
    r = S * (0.25 if crown else 0.268)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    full = (S, S)

    rx, ry = r * 1.80, r * 0.48
    rw = r * ring_frac

    ring = Image.new("L", full, 0)
    d = ImageDraw.Draw(ring)
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
    d.ellipse([cx - rx + rw, cy - ry + rw * 0.5, cx + rx - rw, cy + ry - rw * 0.5], fill=0)
    ring = ring.rotate(tilt, resample=Image.BICUBIC, center=(cx, cy))
    front = Image.new("L", full, 0)
    ImageDraw.Draw(front).rectangle([0, cy, S, S], fill=255)
    front = front.rotate(tilt, resample=Image.BICUBIC, center=(cx, cy))
    ring_back = mask_mul(ring, ImageChops.invert(front))
    ring_front = mask_mul(ring, front)

    ring_rgb = radial(S, S, cx - r * 0.8, cy - r * 0.5, r * 2.3, (255, 200, 130), (150, 64, 26), 1.0)
    pm = Image.new("L", full, 0)
    ImageDraw.Draw(pm).ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)

    # a thin ink keyline around every part keeps the silhouette crisp on any background
    key = max(1.5, r * 0.035)

    # back half of the ring (behind the planet, shaded)
    img = Image.alpha_composite(img, solid(full, INK, dilate(ring_back, key)))
    img = Image.alpha_composite(img, fill_with(ring_rgb, ring_back))
    img = Image.alpha_composite(img, solid(full, (30, 12, 8), ring_back.point(lambda v: int(v * 0.4))))

    # planet: ivory sphere, light from the upper left, warm bounce on the lower right
    img = Image.alpha_composite(img, solid(full, INK, dilate(pm, key)))
    sphere = radial(S, S, cx - r * 0.42, cy - r * 0.45, r * 1.6, (255, 252, 244), (128, 104, 96), 1.25)
    img = Image.alpha_composite(img, fill_with(sphere, pm))
    if detail:
        seams = Image.new("L", full, 0)
        sd = ImageDraw.Draw(seams)
        lw = max(1, int(r * 0.022))
        for k in (0.42, 0.82):
            sd.ellipse([cx - r * k, cy - r, cx + r * k, cy + r], outline=255, width=lw)
        sd.line([cx, cy - r, cx, cy + r], fill=255, width=lw)
        for k in (-0.5, 0.05):
            hh = r * math.sqrt(max(0.0, 1 - k * k))
            sd.line([cx - hh, cy + r * k, cx + hh, cy + r * k], fill=255, width=lw)
        seams = seams.rotate(tilt * 0.6, resample=Image.BICUBIC, center=(cx, cy))
        seams = mask_mul(seams, pm)
        img = Image.alpha_composite(img, solid(full, (110, 90, 80), seams.point(lambda v: int(v * 0.45))))
    bounce = radial(S, S, cx + r * 0.75, cy + r * 0.75, r * 1.1, (230, 120, 70, 110), (230, 120, 70, 0), 1.0)
    bounce.putalpha(mask_mul(bounce.getchannel("A"), pm))
    img = Image.alpha_composite(img, bounce)
    spec = radial(S, S, cx - r * 0.45, cy - r * 0.5, r * 0.42, (255, 255, 255, 170), (255, 255, 255, 0), 1.4)
    spec.putalpha(mask_mul(spec.getchannel("A"), pm))
    img = Image.alpha_composite(img, spec)

    # front half of the ring, with the cyan inlay
    img = Image.alpha_composite(img, solid(full, INK, dilate(ring_front, key)))
    img = Image.alpha_composite(img, fill_with(ring_rgb, ring_front))
    if inlay:
        inner = Image.new("L", full, 0)
        iw = max(1, int(rw * 0.26))
        ImageDraw.Draw(inner).ellipse(
            [cx - rx + rw * 0.95, cy - ry + rw * 0.5, cx + rx - rw * 0.95, cy + ry - rw * 0.5], outline=255, width=iw)
        inner = inner.rotate(tilt, resample=Image.BICUBIC, center=(cx, cy))
        img = Image.alpha_composite(img, solid(full, CYAN, mask_mul(inner, front)))

    if crown:
        n = 5
        cw, ch = r * 0.92, r * 0.55
        base_y = cy - r * 0.80
        xs = [cx - cw / 2 + cw * i / (n - 1) for i in range(n)]
        tips = [1.0, 0.70, 1.22, 0.70, 1.0]
        poly = [(cx - cw / 2 * 0.86, base_y + ch * 0.28), (cx - cw / 2, base_y - ch * 0.1)]
        for i in range(n):
            poly.append((xs[i], base_y - ch * tips[i]))
            if i < n - 1:
                poly.append(((xs[i] + xs[i + 1]) / 2, base_y - ch * 0.30))
        poly += [(cx + cw / 2, base_y - ch * 0.1), (cx + cw / 2 * 0.86, base_y + ch * 0.28)]
        cm = Image.new("L", full, 0)
        ImageDraw.Draw(cm).polygon(poly, fill=255)
        top = int(base_y - ch * 1.3)
        grad = vgradient(S, int(ch * 1.7) + 2, [(0, (255, 236, 186)), (0.45, COPPER_HI), (0.75, COPPER_MID), (1, COPPER_LO)])
        canvas = Image.new("RGB", full, COPPER_LO)
        canvas.paste(grad, (0, top))
        jewels = Image.new("L", full, 0)
        jd = ImageDraw.Draw(jewels)
        jr = r * 0.085
        for i in (0, 2, 4):
            jx, jy = xs[i], base_y - ch * tips[i]
            jd.ellipse([jx - jr, jy - jr, jx + jr, jy + jr], fill=255)
        crown_all = ImageChops.lighter(cm, jewels)
        img = Image.alpha_composite(img, solid(full, INK, dilate(crown_all, key * 1.2)))
        img = Image.alpha_composite(img, fill_with(canvas, cm))
        # crown band with a cyan gem, echoing the ring inlay
        bm = Image.new("L", full, 0)
        ImageDraw.Draw(bm).rectangle([0, base_y - ch * 0.02, S, base_y + ch * 0.3], fill=255)
        bm = mask_mul(bm, cm)
        img = Image.alpha_composite(img, solid(full, (120, 50, 20), bm.point(lambda v: int(v * 0.55))))
        gm = Image.new("L", full, 0)
        g = ch * 0.13
        gy = base_y + ch * 0.13
        ImageDraw.Draw(gm).polygon([(cx, gy - g), (cx + g * 1.2, gy), (cx, gy + g), (cx - g * 1.2, gy)], fill=255)
        img = Image.alpha_composite(img, solid(full, CYAN, gm))
        img = Image.alpha_composite(img, solid(full, (255, 240, 205), jewels))

    return img.resize((size, size), Image.LANCZOS)


# ---------------------------------------------------------------------------------------------
# Application icon
# ---------------------------------------------------------------------------------------------

def badge_background(S, rounded=True, stars=True, pad_frac=0.035):
    """Dusk-sky badge (the intro title's sky): rounded square, or full square."""
    pad = S * pad_frac if rounded else 0
    rad = S * 0.19
    if rounded:
        badge = Image.new("L", (S, S), 0)
        ImageDraw.Draw(badge).rounded_rectangle([pad, pad, S - pad - 1, S - pad - 1], radius=rad, fill=255)
    else:
        badge = Image.new("L", (S, S), 255)
    bg = vgradient(S, S, [(0, (16, 14, 46)), (0.5, (62, 30, 84)), (0.82, (150, 64, 70)), (1.0, (214, 104, 58))]).convert("RGBA")
    glow = radial(S, S, S * 0.5, S * 0.6, S * 0.55, (255, 160, 90, 110), (255, 160, 90, 0), 1.2)
    bg = Image.alpha_composite(bg, glow)
    if stars:
        rng = np.random.default_rng(7)
        sd = ImageDraw.Draw(bg)
        for _ in range(70):
            x, y = rng.uniform(0.1, 0.9) * S, rng.uniform(0.08, 0.45) * S
            rr = rng.uniform(0.7, 2.0) * S / 1024
            sd.ellipse([x - rr, y - rr, x + rr, y + rr], fill=(255, 245, 230, int(rng.uniform(80, 220))))
    bg.putalpha(badge)
    if rounded:
        edge = ImageChops.subtract(badge, ImageChops.invert(dilate(ImageChops.invert(badge), S * 0.01)))
        bg = Image.alpha_composite(bg, solid((S, S), (255, 210, 170), edge.point(lambda v: int(v * 0.35))))
    return bg


def icon_image(size):
    """One icon size. Small sizes drop the crown, seams, stars and inlay and thicken the ring."""
    S = min(max(size * 4, 256), 2048)
    small = size <= 32
    tiny = size <= 20
    img = badge_background(S, rounded=True, stars=size >= 64, pad_frac=0.0 if small else 0.035)
    crown = size >= 48
    em_size = int(S * (0.98 if crown else 1.02))
    ring_frac = 0.36 if tiny else 0.28 if small else 0.2 if size <= 64 else 0.17
    em = planet_emblem(em_size, crown=crown, detail=size >= 64, ring_frac=ring_frac, inlay=size >= 24)
    ox = (S - em_size) // 2
    oy = (S - em_size) // 2
    sh = em.getchannel("A").filter(ImageFilter.GaussianBlur(S * 0.018))
    shadow = solid(em.size, (10, 4, 16), sh.point(lambda v: int(v * 0.5)))
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    layer.paste(shadow, (ox + int(S * 0.01), oy + int(S * 0.02)), shadow)
    layer.alpha_composite(em, (ox, oy)) if ox >= 0 and oy >= 0 else layer.paste(em, (ox, oy), em)
    # keep the emblem inside the badge
    layer.putalpha(mask_mul(layer.getchannel("A"), img.getchannel("A")))
    img = Image.alpha_composite(img, layer)
    out = img.resize((size, size), Image.LANCZOS)
    if small:
        out = out.filter(ImageFilter.UnsharpMask(radius=0.6, percent=50, threshold=0))
    return out


ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]
UNITY_SIZES = [16, 24, 32, 48, 64, 128, 256, 512, 1024]


def write_ico(path, images):
    """Multi-image .ico with PNG-compressed entries (Vista+), one hand-made image per size."""
    import io
    import struct
    entries = []
    for im in images:
        buf = io.BytesIO()
        im.save(buf, "PNG")
        entries.append((im.size[0], buf.getvalue()))
    header = struct.pack("<HHH", 0, 1, len(entries))
    offset = 6 + 16 * len(entries)
    dir_bytes = b""
    data = b""
    for size, png in entries:
        wb = 0 if size >= 256 else size
        dir_bytes += struct.pack("<BBBBHHII", wb, wb, 0, 0, 1, 32, len(png), offset + len(data))
        data += png
    with open(path, "wb") as f:
        f.write(header + dir_bytes + data)


def make_icons():
    os.makedirs(ICON_DIR, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    master = icon_image(1024)
    master.save(os.path.join(ICON_DIR, "SolarMajesty_Icon_1024.png"))
    for s in UNITY_SIZES[:-1]:
        icon_image(s).save(os.path.join(ICON_DIR, f"SolarMajesty_Icon_{s}.png"))
    ico_imgs = [icon_image(s) for s in ICO_SIZES]
    write_ico(os.path.join(ICON_DIR, "SolarMajesty.ico"), ico_imgs)
    # Steam: client icon (.ico, shown in the library list and desktop shortcut) and community icon.
    write_ico(os.path.join(OUT, "steam_client_icon.ico"), [icon_image(s) for s in (16, 24, 32, 48, 64, 128, 256)])
    # Community icon: square, full bleed (Steam rounds and frames it itself).
    full = icon_full_bleed(184)
    full.save(os.path.join(OUT, "community_icon_184.jpg"), quality=95)
    sheet = Image.new("RGBA", (16 + 24 + 32 + 48 + 64 + 128 + 256 + 8 * 9, 280), (235, 235, 235, 255))
    x = 8
    for s in ICO_SIZES:
        im = icon_image(s)
        sheet.paste(im, (x, 270 - s), im)
        x += s + 8
    dark = Image.new("RGBA", sheet.size, (32, 32, 36, 255))
    x = 8
    for s in ICO_SIZES:
        im = icon_image(s)
        dark.paste(im, (x, 270 - s), im)
        x += s + 8
    both = Image.new("RGBA", (sheet.width, sheet.height * 2))
    both.paste(sheet, (0, 0))
    both.paste(dark, (0, sheet.height))
    os.makedirs(SRC, exist_ok=True)
    both.save(os.path.join(SRC, "icon_size_sheet.png"))
    print("icons ->", ICON_DIR, "and", OUT)


def icon_full_bleed(size):
    """Square, no rounded corners; the emblem keeps a margin so Steam's frame never clips it."""
    S = size * 4
    bg = badge_background(S, rounded=False, stars=True)
    e = int(S * 0.9)
    em = planet_emblem(e, crown=True, detail=True)
    bg.alpha_composite(em, ((S - e) // 2, (S - e) // 2))
    return bg.convert("RGB").resize((size, size), Image.LANCZOS)


# ---------------------------------------------------------------------------------------------
# Wordmark
# ---------------------------------------------------------------------------------------------

_WORDMARK_CACHE = {}


def wordmark(width):
    """
    The "Solar Majesty" logo as RGBA, `width` px wide (tight bounds, including its shadow halo):
    SOLAR, with the crowned ringed planet as its O, over MAJESTY, as in the 3D intro title.
    """
    if "master" not in _WORDMARK_CACHE:
        _WORDMARK_CACHE["master"] = _wordmark_master()
    m = _WORDMARK_CACHE["master"]
    return m.resize((int(width), max(1, round(m.height * width / m.width))), Image.LANCZOS)


def _wordmark_master():
    H = 520  # cap height of MAJESTY at the working scale
    big = ImageFont.truetype(FONT, int(H / 0.935))
    small_h = H * 0.56
    small = ImageFont.truetype(FONT, int(small_h / 0.935))
    track_big = int(H * 0.03)
    track_small = int(small_h * 0.34)

    def line_mask(text, font, track, planet_at=None, planet_px=0):
        widths = []
        for ch in text:
            if planet_at is not None and ch == "O":
                widths.append(planet_px)
            else:
                b = font.getbbox(ch)
                widths.append(b[2] - b[0])
        total = sum(widths) + track * (len(text) - 1)
        asc = font.getbbox("H")
        cap = asc[3] - asc[1]
        desc = max(0, font.getbbox(text)[3] - asc[3])
        m = Image.new("L", (total + 40, cap + 40 + desc), 0)
        d = ImageDraw.Draw(m)
        x = 20
        slots = []
        for ch, w in zip(text, widths):
            if planet_at is not None and ch == "O":
                slots.append((x, w))
            else:
                b = font.getbbox(ch)
                d.text((x - b[0], 20 - asc[1]), ch, font=font, fill=255)
            x += w + track
        return m, slots, cap

    planet_px = int(small_h * 1.3)
    m_big, _, _ = line_mask("MAJESTY", big, track_big)
    m_small, slots, cap_small = line_mask("SOLAR", small, track_small, planet_at=True, planet_px=planet_px)
    gap = int(H * 0.16)
    top_room = int(small_h * 0.75)  # the crown rises above the cap line
    Wt = max(m_big.width, m_small.width)
    Ht = top_room + m_small.height + gap + m_big.height
    mask = Image.new("L", (Wt, Ht), 0)
    sx = (Wt - m_small.width) // 2
    mask.paste(m_small, (sx, top_room))
    mask.paste(m_big, ((Wt - m_big.width) // 2, top_room + m_small.height + gap))
    px, pw = slots[0]
    planet_box = (sx + px + pw // 2, top_room + 20 + cap_small // 2, pw)
    # side spears, as on the intro title (copper blades with a cyan inlay)
    spear_y = top_room + 20 + cap_small // 2
    spear_len = int((Wt - m_small.width) / 2 * 0.92)

    pad = int(H * 0.22)
    Wc, Hc = Wt + pad * 2, Ht + pad * 2
    full = Image.new("L", (Wc, Hc), 0)
    full.paste(mask, (pad, pad))
    mask = full
    pcx, pcy, pw = planet_box[0] + pad, planet_box[1] + pad, planet_box[2]
    spear_y += pad

    stroke = int(H * 0.045)
    ink = int(H * 0.022)
    depth = int(H * 0.06)

    face_m = mask
    rim_m = dilate(face_m, stroke)
    ink_m = dilate(rim_m, ink)
    # extrusion: the rim stepped down, as a darker copper body
    ext = Image.new("L", mask.size, 0)
    for i in range(1, depth + 1, 2):
        ext = ImageChops.lighter(ext, shift(rim_m, i * 0.35, i))
    ext_ink = dilate(ext, ink)

    img = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    # soft dark halo: keeps the logo readable over any render
    halo = dilate(ink_m, int(H * 0.04)).filter(ImageFilter.GaussianBlur(H * 0.09))
    img = Image.alpha_composite(img, solid(mask.size, (10, 6, 20), halo.point(lambda v: int(v * 0.85))))
    shadow = shift(ImageChops.lighter(ink_m, ext_ink), H * 0.03, H * 0.07).filter(ImageFilter.GaussianBlur(H * 0.035))
    img = Image.alpha_composite(img, solid(mask.size, (8, 4, 12), shadow.point(lambda v: int(v * 0.7))))
    img = Image.alpha_composite(img, solid(mask.size, INK, ImageChops.lighter(ink_m, ext_ink)))
    img = Image.alpha_composite(img, solid(mask.size, (92, 38, 16), ext))

    # copper rim: a metallic band gradient repeated per line
    h = mask.size[1]
    rim_rgb = vgradient(mask.size[0], h, [
        (0.0, COPPER_HI), (0.18, (255, 222, 170)), (0.32, COPPER_MID), (0.5, COPPER_HI),
        (0.62, (255, 210, 150)), (0.8, COPPER_MID), (1.0, COPPER_LO)])
    img = Image.alpha_composite(img, fill_with(rim_rgb, rim_m))
    # ivory face with a soft top light
    face_rgb = vgradient(mask.size[0], h, [(0, IVORY_HI), (0.45, (240, 230, 214)), (0.55, IVORY_LO), (1, (226, 214, 198))])
    img = Image.alpha_composite(img, fill_with(face_rgb, face_m))
    # inner bevel: a dark line just inside the face's lower-right edges, a light one upper-left
    inner = ImageChops.subtract(face_m, shift(face_m, -int(H * 0.012), -int(H * 0.016)))
    img = Image.alpha_composite(img, solid(mask.size, (150, 120, 100), inner.point(lambda v: int(v * 0.8))))
    hi = ImageChops.subtract(face_m, shift(face_m, int(H * 0.01), int(H * 0.012)))
    img = Image.alpha_composite(img, solid(mask.size, (255, 255, 255), hi.point(lambda v: int(v * 0.8))))

    # spears either side of SOLAR
    if spear_len > H * 0.4:
        sp = Image.new("RGBA", mask.size, (0, 0, 0, 0))
        spm = Image.new("L", mask.size, 0)
        cyn = Image.new("L", mask.size, 0)
        gem = Image.new("L", mask.size, 0)
        sd, cd, gd = ImageDraw.Draw(spm), ImageDraw.Draw(cyn), ImageDraw.Draw(gem)
        th = H * 0.05
        left_start = pad + (Wt - m_small.width) // 2 - H * 0.1
        for side in (-1, 1):
            if side < 0:
                x1 = left_start
                x0 = x1 - spear_len
            else:
                x0 = Wc - left_start
                x1 = x0 + spear_len
            tip, butt = (x0, x1) if side < 0 else (x1, x0)
            sd.polygon([(tip, spear_y), (butt, spear_y - th), (butt, spear_y + th)], fill=255)
            g = H * 0.09
            gd.polygon([(butt, spear_y - g), (butt + g * 0.9 * (1 if side < 0 else -1), spear_y),
                        (butt, spear_y + g), (butt - g * 0.9 * (1 if side < 0 else -1), spear_y)], fill=255)
            a = butt + (tip - butt) * 0.12
            b = butt + (tip - butt) * 0.62
            cd.rectangle([min(a, b), spear_y - th * 0.35, max(a, b), spear_y + th * 0.35], fill=255)
        allm = ImageChops.lighter(spm, gem)
        sp = Image.alpha_composite(sp, solid(mask.size, INK, dilate(allm, ink)))
        sp = Image.alpha_composite(sp, fill_with(rim_rgb, spm))
        sp = Image.alpha_composite(sp, solid(mask.size, (110, 70, 40), gem))
        gem_hi = mask_mul(gem, shift(gem, int(H * 0.03), int(H * 0.03)))
        sp = Image.alpha_composite(sp, solid(mask.size, COPPER_HI, gem_hi))
        sp = Image.alpha_composite(sp, solid(mask.size, CYAN, cyn))
        img = Image.alpha_composite(sp, img)

    # the planet "O"
    psize = int(pw * 2.05)
    em = planet_emblem(psize, crown=True, detail=True)
    pl = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    ex, ey = int(pcx - psize / 2), int(pcy - psize * 0.56)
    emsh = em.getchannel("A")
    ink_layer = solid(em.size, INK, dilate(emsh, max(2, ink // 2)))
    pl.paste(ink_layer, (ex, ey), ink_layer)
    pl.paste(em, (ex, ey), em)
    img = Image.alpha_composite(img, pl)

    bbox = img.getchannel("A").point(lambda v: 255 if v > 6 else 0).getbbox()
    return img.crop(bbox)


# ---------------------------------------------------------------------------------------------
# Store / library art
# ---------------------------------------------------------------------------------------------

def refresh_sources(shots, names):
    """
    Copy the renders the art uses from the capture folder into Docs/StoreArt/source (JPEG, long
    side 4096 px: more than any slot needs, the hero included). Composition always reads these
    copies, so the committed art can be rebuilt exactly without a capture run.
    """
    os.makedirs(SRC, exist_ok=True)
    for name in sorted(set(names)):
        png = os.path.join(shots, name + ".png") if shots else None
        dst = os.path.join(SRC, name + ".jpg")
        if png and os.path.exists(png):
            im = Image.open(png).convert("RGB")
            k = 4096 / max(im.size)
            if k < 1:
                im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
            im.save(dst, quality=90, subsampling=0, optimize=True)
            print(f"source: {name} <- {png}")
        elif not os.path.exists(dst):
            raise SystemExit(f"missing render {name}: capture with store_screenshots.ps1 -Keyart (see the docstring)")


def load_shot(name):
    return Image.open(os.path.join(SRC, name + ".jpg")).convert("RGB")


def cover(img, w, h, fx=0.5, fy=0.5, zoom=1.0):
    """Scale to cover w x h (times zoom), then crop centred on (fx, fy), fractions of the source."""
    s = max(w / img.width, h / img.height) * zoom
    sw, sh = max(w, round(img.width * s)), max(h, round(img.height * s))
    # Reduce in two steps for very large sources: faster, and LANCZOS stays clean.
    src = img
    if src.width > sw * 2:
        src = src.reduce(max(1, src.width // (sw * 2)))
    im = src.resize((sw, sh), Image.LANCZOS)
    cx, cy = im.width * fx, im.height * fy
    x0 = int(min(max(cx - w / 2, 0), im.width - w))
    y0 = int(min(max(cy - h / 2, 0), im.height - h))
    return im.crop((x0, y0, x0 + w, y0 + h))


def grade(img, sat=1.0, contrast=1.0, gamma=1.0, lift=0.0):
    a = np.asarray(img).astype(np.float32) / 255.0
    lum = (a * np.array([0.2126, 0.7152, 0.0722], np.float32)).sum(-1, keepdims=True)
    a = lum + (a - lum) * sat
    a = (a - 0.5) * contrast + 0.5
    a = np.clip(a, 0, 1) ** (1.0 / gamma)
    a = a * (1 - lift) + lift
    return Image.fromarray((a.clip(0, 1) * 255 + 0.5).astype(np.uint8), "RGB")


DUSK_INK = (14, 10, 34)  # the intro title's night sky, used for every scrim


def scrim(size, left=0.0, top=0.0, bottom=0.0, extent=0.55, vignette=0.0, color=DUSK_INK, cx=0.5, cy=0.5):
    """Soft darkening so the logo always reads: edge gradients plus an optional vignette."""
    w, h = size
    ys = np.linspace(0, 1, h, dtype=np.float32)[:, None]
    xs = np.linspace(0, 1, w, dtype=np.float32)[None, :]
    a = np.zeros((h, w), np.float32)

    def ramp(t):
        t = np.clip(t, 0, 1)
        return t * t * (3 - 2 * t)

    if left:
        a = np.maximum(a, left * ramp(1 - xs / extent))
    if top:
        a = np.maximum(a, top * ramp(1 - ys / extent))
    if bottom:
        a = np.maximum(a, bottom * ramp((ys - (1 - extent)) / extent))
    if vignette:
        d = np.sqrt(((xs - cx) / 0.72) ** 2 + ((ys - cy) / 0.72) ** 2 * (h / w) ** 0)
        a = np.maximum(a, vignette * ramp((d - 0.45) / 0.55))
    layer = Image.new("RGBA", size, color + (0,))
    layer.putalpha(Image.fromarray((a.clip(0, 1) * 255).astype(np.uint8), "L"))
    return layer


def place(base, logo, cx, cy):
    base = base.convert("RGBA")
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    layer.paste(logo, (int(round(cx - logo.width / 2)), int(round(cy - logo.height / 2))), logo)
    return Image.alpha_composite(base, layer)


def save(img, name, size):
    assert img.size == size, (name, img.size, size)
    path = os.path.join(OUT, name)
    if name.endswith(".jpg"):
        img.convert("RGB").save(path, quality=92, optimize=True, progressive=True, subsampling=0)
    else:
        img.save(path, optimize=True)
    print(f"{name}: {img.size[0]}x{img.size[1]}")


# Each Steam slot: which render, how it is cropped, where the logo goes, how the scrim falls.
#   src: render name (<world>_keyart_<pose>[_golden]); fx, fy: crop centre; zoom: >1 crops tighter
#   logo: (width as a fraction of the slot width, centre x, centre y as fractions)
#   scrim: arguments for scrim(); grade: arguments for grade()
SLOTS = {
    "header_capsule_920x430.jpg": dict(
        size=(920, 430), src="mars_keyart_right21_golden", fx=0.52, fy=0.5, zoom=1.06,
        logo=(0.41, 0.235, 0.47), scrim=dict(left=0.85, extent=0.6, vignette=0.35)),
    "small_capsule_462x174.jpg": dict(
        size=(462, 174), src="mars_keyart_right21_golden", fx=0.66, fy=0.5, zoom=1.35,
        logo=(0.78, 0.5, 0.5), scrim=dict(left=0.55, extent=1.0, vignette=0.55)),
    "main_capsule_1232x706.jpg": dict(
        size=(1232, 706), src="mars_keyart_right_golden", fx=0.52, fy=0.5, zoom=1.06,
        logo=(0.38, 0.22, 0.45), scrim=dict(left=0.85, extent=0.58, vignette=0.35)),
    "vertical_capsule_748x896.jpg": dict(
        size=(748, 896), src="mars_keyart_portrait_golden", fx=0.5, fy=0.45, zoom=1.0,
        logo=(0.84, 0.5, 0.15), scrim=dict(top=0.85, extent=0.42, vignette=0.35)),
    "library_capsule_600x900.jpg": dict(
        size=(600, 900), src="mars_keyart_portrait_golden", fx=0.5, fy=0.5, zoom=1.0,
        logo=(0.86, 0.5, 0.15), scrim=dict(top=0.85, extent=0.42, vignette=0.35)),
}
HERO = dict(size=(3840, 1240), src="mars_keyart_ultrawide_golden", fx=0.5, fy=0.5, zoom=1.0)
BACKGROUND = dict(size=(1438, 810), src="europa_keyart_band_golden", fx=0.5, fy=0.5, zoom=1.0)
GRADE = dict(sat=1.05, contrast=1.04)


def logo_for(width_px):
    return wordmark(int(width_px))


def compose_slot(name, cfg):
    img = load_shot(cfg["src"])
    w, h = cfg["size"]
    base = grade(cover(img, w, h, cfg["fx"], cfg["fy"], cfg["zoom"]), **GRADE)
    base = Image.alpha_composite(base.convert("RGBA"), scrim((w, h), **cfg["scrim"]))
    lw, lx, ly = cfg["logo"]
    logo = logo_for(w * lw)
    # never let the logo touch the slot's edges
    max_h = h * (0.86 if h < w else 0.4)
    if logo.height > max_h:
        logo = logo.resize((int(logo.width * max_h / logo.height), int(max_h)), Image.LANCZOS)
    out = place(base, logo, w * lx, h * ly).convert("RGB")
    save(out, name, (w, h))
    return out


def make_store(shots):
    os.makedirs(OUT, exist_ok=True)
    refresh_sources(shots, [c["src"] for c in SLOTS.values()] + [HERO["src"], BACKGROUND["src"]])
    made = {}
    for name, cfg in SLOTS.items():
        made[name] = compose_slot(name, cfg)

    # Library hero: no text or logo (Steam lays the library logo over it).
    img = load_shot(HERO["src"])
    hero = grade(cover(img, *HERO["size"], HERO["fx"], HERO["fy"], HERO["zoom"]), **GRADE)
    save(hero, "library_hero_3840x1240.jpg", HERO["size"])

    # Library logo: the wordmark on transparency, as large as the 1280x720 canvas allows with a
    # small margin (Steam scales and positions it over the hero).
    logo = logo_for(1240)
    if logo.height > 700:
        logo = logo.resize((int(logo.width * 700 / logo.height), 700), Image.LANCZOS)
    canvas = Image.new("RGBA", (1280, 720), (0, 0, 0, 0))
    canvas.paste(logo, ((1280 - logo.width) // 2, (720 - logo.height) // 2), logo)
    save(canvas, "library_logo_1280x720.png", (1280, 720))

    # Page background: Steam darkens and fades it behind the store page. Calm, no logo or text.
    img = load_shot(BACKGROUND["src"])
    bw, bh = BACKGROUND["size"]
    bg = grade(cover(img, bw, bh, BACKGROUND["fx"], BACKGROUND["fy"], BACKGROUND["zoom"]), sat=0.75, contrast=0.9)
    bg = Image.alpha_composite(bg.convert("RGBA"), scrim((bw, bh), top=0.45, bottom=0.75, extent=0.6, vignette=0.7)).convert("RGB")
    save(bg, "page_background_1438x810.jpg", (bw, bh))

    # Review sheets (not uploaded): the hero with the logo where the Steam library puts it, and
    # every capsule at its real size.
    prev = hero.resize((1920, 620), Image.LANCZOS).convert("RGBA")
    lg = logo.resize((int(logo.width * 0.36), int(logo.height * 0.36)), Image.LANCZOS)
    prev.alpha_composite(lg, (80, 620 - lg.height - 40))
    prev.convert("RGB").save(os.path.join(SRC, "preview_library_hero_with_logo.jpg"), quality=88)

    sheet = Image.new("RGB", (1232 + 920 + 60, 706 + 900 + 60), (24, 26, 32))
    sheet.paste(made["main_capsule_1232x706.jpg"], (20, 20))
    sheet.paste(made["header_capsule_920x430.jpg"], (1232 + 40, 20))
    sheet.paste(made["small_capsule_462x174.jpg"], (1232 + 40, 430 + 40))
    sheet.paste(made["vertical_capsule_748x896.jpg"], (20, 706 + 40))
    sheet.paste(made["library_capsule_600x900.jpg"], (748 + 40, 706 + 40))
    small = made["small_capsule_462x174.jpg"]
    for i, k in enumerate((0.5, 0.4)):  # search results and wishlist rows show it smaller still
        s = small.resize((int(small.width * k), int(small.height * k)), Image.LANCZOS)
        sheet.paste(s, (1232 + 40 + i * 250, 430 + 40 + 174 + 20))
    sheet.save(os.path.join(SRC, "preview_capsules.jpg"), quality=88)


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("what", choices=["icon", "store", "logo", "all"])
    ap.add_argument("--shots", default=os.path.join(ROOT, "Builds", "StoreShots"),
                    help="folder with <world>_keyart_<pose>.png renders; renders it lacks come from "
                         "the copies in Docs/StoreArt/source")
    a = ap.parse_args(argv)
    if a.what in ("icon", "all"):
        make_icons()
    if a.what == "logo":  # quick look at the wordmark alone
        os.makedirs(SRC, exist_ok=True)
        wordmark(1600).save(os.path.join(SRC, "wordmark_1600.png"))
    if a.what in ("store", "all"):
        make_store(a.shots)


if __name__ == "__main__":
    main(sys.argv[1:])
