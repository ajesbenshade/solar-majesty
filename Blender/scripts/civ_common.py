"""Shared Solar Majesty civ-building mesh kit.

Copied from sm_core_citadels.py (Builder, finish_mesh, trim materials, FBX export).
Textures: the shared 2048 citadel trim sheet. Do not regenerate it here.
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent
TEX_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Citadels" / "Textures"
BLEND_DIR = None

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def _arg(name, default):
    return ARGS[ARGS.index(name) + 1] if name in ARGS else default


CASTLE_SCALE = float(_arg("--scale", 18.0 / 12.5))
BLEND_DIR = _arg("--blend-dir", None)
TEX = 2048

# ---------------------------------------------------------------- trim sheet layout
# (v0, v1) in UV space, v = 0 at the bottom of the image.
BANDS = {
    "EMIT": (0.000, 0.040),
    "TEAM": (0.040, 0.100),
    "CARB": (0.100, 0.220),
    "EDGE": (0.220, 0.260),
    "VENT": (0.260, 0.340),
    "RIB": (0.340, 812 / 2048),            # narrowed 2026-11-11 (lower edge unchanged) to make room for GLASS
    "GLASS": (812 / 2048, 860 / 2048),     # rows 812-859 (48 px): frosted glass, neutral albedo, in the emission mask
    "PANEL_DK": (0.420, 0.600),
    "PANEL": (0.600, 1.000),
}
SLOTS = ["Trim", "Carbon", "TeamColour", "CoreEmission", "Accent", "ChargerAmber"]
# surface kind -> (material slot, band)
KINDS = {
    "PANEL": (0, "PANEL"), "PANEL_DK": (0, "PANEL_DK"), "RIB": (0, "RIB"), "VENT": (0, "VENT"),
    "EDGE": (0, "EDGE"), "CARB": (1, "CARB"), "TEAM": (2, "TEAM"), "CORE": (3, "EMIT"),
    "GLASS": (0, "GLASS"),                 # windows: Trim slot; Trim's emission map lights only this strip
    "ACCENT": (4, "TEAM"),                 # player accent (banners/pennants): own slot, neutral TEAM band texels
    "AMBER": (5, "TEAM"),                  # HAB charger pods: ChargerAmber, untextured warm emission
}
KIND_LIST = list(KINDS)
TEXEL_PER_M = 200.0 / TEX      # UV units per metre at full density (200 px/m on the 2048 sheet)
PAD = 3.0 / TEX


def srgb2lin(c):
    return tuple(((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c)


# ================================================================ texture generation
def make_trim_sheet():
    N = TEX
    rng = np.random.default_rng(1337)
    v = (np.arange(N) + 0.5) / N                     # row -> v (row 0 = bottom)
    u = (np.arange(N) + 0.5) / N
    U, V = np.meshgrid(u, v)
    px = np.arange(N)[None, :].repeat(N, 0)          # column index
    alb = np.zeros((N, N, 3), np.float32)
    h = np.zeros((N, N), np.float32)
    rough = np.zeros((N, N), np.float32)
    metal = np.zeros((N, N), np.float32)
    ao = np.ones((N, N), np.float32)
    emit = np.zeros((N, N), np.float32)
    noise = rng.random((N // 8, N // 8)).astype(np.float32)
    noise = np.kron(noise, np.ones((8, 8), np.float32))
    fine = rng.random((N, N)).astype(np.float32)

    def rows(name):
        v0, v1 = BANDS[name]
        return slice(int(round(v0 * N)), int(round(v1 * N)))

    def band_t(name):                                 # 0..1 across the band (V)
        v0, v1 = BANDS[name]
        return (V - v0) / (v1 - v0)

    def put(name, col, r, m, hh=None):
        s = rows(name)
        alb[s] = col[s] if col.ndim == 3 else col
        rough[s] = r[s] if np.ndim(r) == 2 else r
        metal[s] = m[s] if np.ndim(m) == 2 else m
        if hh is not None:
            h[s] = hh[s]

    one = np.ones((N, N), np.float32)
    # PANEL: light ceramic panels, seams every 256 px in U and mid-band, per-panel tone shift, bolts
    t = band_t("PANEL")
    seam_u = (px % 256) < 4
    seam_v = np.abs(t - 0.5) < 0.006
    tone = 0.80 + 0.04 * rng.random(N // 256 * 2 + 2)[(px // 256) * 2 + (t > 0.5)]
    base = (tone + 0.025 * (noise - 0.5) + 0.015 * (fine - 0.5))
    base = np.where(seam_u | seam_v, 0.42, base)
    bolt = (((px % 256) - 16) ** 2 + ((t * 820 % 410) - 16) ** 2) < 25
    base = np.where(bolt, 0.55, base)
    col = np.stack([base * 1.0, base * 0.985, base * 0.96], -1)
    hh = np.where(seam_u | seam_v, -1.0, 0.0) + np.where(bolt, 0.6, 0.0)
    put("PANEL", col, 0.55 + 0.1 * noise, 0.0, hh)
    ao[rows("PANEL")] = np.where((seam_u | seam_v)[rows("PANEL")], 0.55, 1.0)
    # PANEL_DK: gunmetal panels, seams every 128 px, rivet rows
    t = band_t("PANEL_DK")
    seam_u = (px % 128) < 3
    seam_v = (t < 0.02) | (t > 0.98)
    riv = ((px % 32) - 16) ** 2 + (((t * 368) % 368 - 20) ** 2) < 9
    base = 0.36 + 0.03 * (noise - 0.5)
    base = np.where(seam_u | seam_v, 0.16, base)
    base = np.where(riv, 0.5, base)
    put("PANEL_DK", np.stack([base, base * 1.01, base * 1.05], -1), 0.45 + 0.1 * noise, 0.8,
        np.where(seam_u | seam_v, -1.0, 0.0) + np.where(riv, 0.5, 0.0))
    # RIB: bevelled rib profile across V with grooves every 64 px
    t = band_t("RIB")
    prof = np.sin(np.clip(t, 0, 1) * math.pi)
    groove = (px % 64) < 3
    base = 0.30 + 0.22 * prof
    base = np.where(groove, 0.18, base)
    put("RIB", np.stack([base, base * 1.02, base * 1.06], -1), 0.38, 0.85, prof - np.where(groove, 0.5, 0.0))
    # VENT: vertical slots every 32 px in a frame
    t = band_t("VENT")
    slot = ((px % 32) < 18) & (t > 0.15) & (t < 0.85)
    base = np.where(slot, 0.06, 0.30)
    put("VENT", np.stack([base, base, base * 1.05], -1), np.where(slot, 0.8, 0.5), 0.6, np.where(slot, -1.0, 0.0))
    ao[rows("VENT")] = np.where(slot[rows("VENT")], 0.35, 1.0)
    # EDGE: bright worn metal edge
    base = 0.72 + 0.12 * (fine - 0.5) + 0.05 * (noise - 0.5)
    put("EDGE", np.stack([base, base, base * 1.02], -1), 0.32, 0.9, 0.3 * one)
    # CARB: carbon weave around #1E2024
    weave = (((px // 8) + (np.arange(N)[:, None] // 8)) % 2).astype(np.float32)
    car = np.array([30, 32, 36], np.float32) / 255.0
    col = car[None, None, :] * (0.92 + 0.16 * weave[..., None])
    put("CARB", col, 0.68 - 0.08 * weave, 0.15, 0.15 * weave)
    # TEAM: neutral light grey (multiply with team tint), thin dark border lines
    t = band_t("TEAM")
    border = (t < 0.05) | (t > 0.95)
    base = np.where(border, 0.35, 0.86 + 0.02 * (noise - 0.5))
    put("TEAM", np.stack([base] * 3, -1), 0.5, 0.1, np.where(border, -0.5, 0.0))
    # GLASS: frosted glass, neutral (no hue) so each civ tints it; emissive mask 0.85 inside, dark 2 px frame
    t = band_t("GLASS")
    frame = (t < 0.05) | (t > 0.95)
    frost = 0.62 + 0.05 * (noise - 0.5) + 0.03 * (fine - 0.5)
    base = np.where(frame, 0.18, frost)
    put("GLASS", np.stack([base] * 3, -1), np.where(frame, 0.5, 0.38 + 0.08 * noise), 0.0, np.where(frame, -0.6, 0.0))
    emit[rows("GLASS")] = np.where(frame[rows("GLASS")], 0.0, 0.85)
    # EMIT: white strip, mask = 1 except the 2 px edges
    t = band_t("EMIT")
    edge = (t < 0.03) | (t > 0.97)
    put("EMIT", np.stack([np.where(edge, 0.2, 0.95)] * 3, -1), 0.3, 0.0)
    emit[rows("EMIT")] = np.where(edge[rows("EMIT")], 0.0, 1.0)

    # normal map from height (OpenGL, +Y = +v)
    k = 2.5
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    nrm = np.stack([-dx * k, -dy * k, np.ones_like(h)], -1)
    nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True)
    alb = np.clip(alb, 0, 1)
    maps = {
        "BaseColor": (np.dstack([alb, np.ones((N, N))]), "sRGB"),
        "Normal": (np.dstack([nrm * 0.5 + 0.5, np.ones((N, N))]), "Non-Color"),
        "ORM": (np.dstack([ao, rough, metal, np.ones((N, N))]), "Non-Color"),
        "MetallicSmoothness": (np.dstack([metal, metal, metal, 1.0 - rough]), "Non-Color"),
        "Emission": (np.dstack([emit, emit, emit, np.ones((N, N))]), "sRGB"),
    }
    TEX_DIR.mkdir(parents=True, exist_ok=True)
    for key, (arr, cs) in maps.items():
        name = f"SM_Citadel_Trim_{key}"
        img = bpy.data.images.new(name, N, N, alpha=True, float_buffer=False)
        img.colorspace_settings.name = cs
        img.pixels.foreach_set(arr.astype(np.float32).ravel())
        img.filepath_raw = str(TEX_DIR / f"{name}.png")
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
    print("[SM] trim sheet written")


# ================================================================ materials
def load_img(key, cs):
    p = TEX_DIR / f"SM_Citadel_Trim_{key}.png"
    img = bpy.data.images.load(str(p), check_existing=True)
    img.colorspace_settings.name = cs
    return img


def make_materials():
    """Export-pure materials: atlas textures wired straight into Principled so FBX carries them.
    (Team / civ tints are applied in Unity, or by the review render script.)"""
    mats = []
    for slot in SLOTS:
        m = bpy.data.materials.get(slot) or bpy.data.materials.new(slot)
        try:
            m.use_nodes = True
        except Exception:
            pass
        nt = m.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        p = nt.nodes.new("ShaderNodeBsdfPrincipled")
        p.name = "Principled BSDF"
        nt.links.new(p.outputs[0], out.inputs[0])
        base = nt.nodes.new("ShaderNodeTexImage"); base.name = "BaseTex"
        base.image = load_img("BaseColor", "sRGB")
        nt.links.new(base.outputs[0], p.inputs["Base Color"])
        orm = nt.nodes.new("ShaderNodeTexImage"); orm.name = "ORMTex"
        orm.image = load_img("ORM", "Non-Color")
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(orm.outputs[0], sep.inputs[0])
        nt.links.new(sep.outputs[1], p.inputs["Roughness"])
        nt.links.new(sep.outputs[2], p.inputs["Metallic"])
        nrm = nt.nodes.new("ShaderNodeTexImage"); nrm.name = "NormalTex"
        nrm.image = load_img("Normal", "Non-Color")
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(nrm.outputs[0], nm.inputs["Color"])
        nt.links.new(nm.outputs[0], p.inputs["Normal"])
        if slot == "ChargerAmber":
            # Solid amber, no trim sheet. Warm emission for HAB charging pods.
            for n in list(nt.nodes):
                if n.type != "OUTPUT_MATERIAL":
                    nt.nodes.remove(n)
            p = nt.nodes.new("ShaderNodeBsdfPrincipled")
            p.name = "Principled BSDF"
            nt.links.new(p.outputs[0], nt.nodes["Material Output"].inputs[0] if "Material Output" in nt.nodes else out.inputs[0])
            # relink to remaining output
            outs = [n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"]
            nt.links.new(p.outputs[0], outs[0].inputs[0])
            amber = (0.89, 0.45, 0.08, 1.0)
            p.inputs["Base Color"].default_value = amber
            p.inputs["Metallic"].default_value = 0.15
            p.inputs["Roughness"].default_value = 0.38
            p.inputs["Emission Color"].default_value = (1.0, 0.55, 0.10, 1.0)
            p.inputs["Emission Strength"].default_value = 3.5
            m.diffuse_color = amber
        elif slot in ("CoreEmission", "Trim"):
            em = nt.nodes.new("ShaderNodeTexImage"); em.name = "EmissionTex"
            em.image = load_img("Emission", "sRGB")
            nt.links.new(em.outputs[0], p.inputs["Emission Color"])
            p.inputs["Emission Strength"].default_value = 4.0 if slot == "CoreEmission" else 0.4
        mats.append(m)
    return mats


# ================================================================ geometry builder
def rz(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'Z')


class B:
    """Accumulates verts / faces / surface-kind / smooth flag (concept castle_common.Builder, plus kinds)."""

    def __init__(self):
        self.v, self.f, self.k, self.s = [], [], [], []

    def add(self, verts, faces, kind, smooth=False, M=None):
        o = len(self.v)
        for p in verts:
            p = Vector(p)
            if M is not None:
                p = M @ p
            self.v.append(tuple(p))
        for f in faces:
            self.f.append([o + i for i in f])
            self.k.append(kind)
            self.s.append(smooth)

    def box(self, c, size, kind, M=None, top=None, shift=(0, 0)):
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        tx, ty = (hx, hy) if top is None else (top[0] / 2, top[1] / 2)
        cx, cy, cz = c
        dx, dy = shift
        v = [(cx - hx, cy - hy, cz - hz), (cx + hx, cy - hy, cz - hz), (cx + hx, cy + hy, cz - hz), (cx - hx, cy + hy, cz - hz),
             (cx - tx + dx, cy - ty + dy, cz + hz), (cx + tx + dx, cy - ty + dy, cz + hz),
             (cx + tx + dx, cy + ty + dy, cz + hz), (cx - tx + dx, cy + ty + dy, cz + hz)]
        f = [(0, 3, 2, 1), (4, 5, 6, 7)] + [(i, (i + 1) % 4, (i + 1) % 4 + 4, i + 4) for i in range(4)]
        self.add(v, f, kind, False, M)

    def cbox(self, c, size, kind, ch=0.08, M=None, lod=0):
        if lod >= 1:                                   # chamfers are LOD0 only
            return self.box(c, size, kind, M)
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        pts = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    pts += [(c[0] + sx * (hx - ch), c[1] + sy * hy, c[2] + sz * (hz - ch)),
                            (c[0] + sx * hx, c[1] + sy * (hy - ch), c[2] + sz * (hz - ch)),
                            (c[0] + sx * (hx - ch), c[1] + sy * (hy - ch), c[2] + sz * hz)]
        self.hull(pts, kind, M)

    def hull(self, pts, kind, M=None):
        bm = bmesh.new()
        vs = [bm.verts.new(p) for p in pts]
        bmesh.ops.convex_hull(bm, input=vs)
        bmesh.ops.dissolve_limit(bm, angle_limit=0.01, verts=bm.verts[:], edges=bm.edges[:])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.verts.index_update()
        self.add([v.co[:] for v in bm.verts], [[v.index for v in f.verts] for f in bm.faces], kind, False, M)
        bm.free()

    def beam(self, a, b, w, d, kind, M=None):
        a, b = Vector(a), Vector(b)
        if M is not None:
            a, b = M @ a, M @ b
        z = (b - a)
        L = z.length
        z.normalize()
        ref = Vector((0, 0, 1)) if abs(z.z) < 0.95 else Vector((1, 0, 0))
        x = ref.cross(z).normalized()
        y = z.cross(x).normalized()
        if abs(z.z) < 0.95:
            x, y = z.cross(Vector((0, 0, 1))).normalized(), None
            y = x.cross(z).normalized()
            x = z.cross(y).normalized()
        R = Matrix((x, y, z)).transposed().to_4x4()
        R.translation = a
        self.box((0, 0, L / 2), (w, d, L), kind, R)

    def cyl(self, c, r0, r1, h, n, kind, M=None, smooth=True, a0=0.0, caps=True):
        cx, cy, cz = c
        ang = [math.radians(a0) + 2 * math.pi * i / n for i in range(n)]
        bot = [(cx + r0 * math.cos(a), cy + r0 * math.sin(a), cz) for a in ang]
        if r1 > 1e-5:
            top = [(cx + r1 * math.cos(a), cy + r1 * math.sin(a), cz + h) for a in ang]
            self.add(bot + top, [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)], kind, smooth, M)
            if caps:
                self.add(top, [list(range(n))], kind, False, M)
        else:
            self.add(bot + [(cx, cy, cz + h)], [(i, (i + 1) % n, n) for i in range(n)], kind, smooth, M)
        if caps:
            self.add(bot, [list(range(n))[::-1]], kind, False, M)

    def ring(self, ri, ro, z0, z1, kind, a0=0.0, a1=360.0, n=32, M=None, smooth=True, ro_top=None, ri_top=None,
             kind_top=None, kind_inner=None):
        full = abs((a1 - a0) - 360.0) < 1e-6
        k = n if full else n + 1
        ang = [math.radians(a0 + (a1 - a0) * i / n) for i in range(k)]
        rot = ro if ro_top is None else ro_top
        rit = ri if ri_top is None else ri_top

        def circ(r, z):
            return [(r * math.cos(a), r * math.sin(a), z) for a in ang]

        def strip(A, Bv, sm, kd):
            q = [(i, (i + 1) % k, (i + 1) % k + k, i + k) for i in range(k if full else k - 1)]
            self.add(A + Bv, q, kd, sm, M)

        strip(circ(ro, z0), circ(rot, z1), smooth, kind)
        strip(circ(rit, z1), circ(ri, z0), smooth if kind_inner is None else False, kind_inner or kind)
        strip(circ(rot, z1), circ(rit, z1), False, kind_top or kind)
        strip(circ(ri, z0), circ(ro, z0), False, kind)
        if not full:
            for a, flip in ((ang[0], False), (ang[-1], True)):
                ca, sa = math.cos(a), math.sin(a)
                q = [(ri * ca, ri * sa, z0), (ro * ca, ro * sa, z0), (rot * ca, rot * sa, z1), (rit * ca, rit * sa, z1)]
                self.add(q, [(0, 1, 2, 3) if not flip else (3, 2, 1, 0)], kind, False, M)

    def dome(self, c, r, hz, n, rings, kind, M=None):
        cx, cy, cz = c
        rows = []
        for j in range(rings):
            t = (math.pi / 2) * j / rings
            rr, zz = r * math.cos(t), hz * math.sin(t)
            rows.append([(cx + rr * math.cos(2 * math.pi * i / n), cy + rr * math.sin(2 * math.pi * i / n), cz + zz) for i in range(n)])
        v = [p for row in rows for p in row] + [(cx, cy, cz + hz)]
        f = []
        for j in range(rings - 1):
            for i in range(n):
                a, b2 = j * n + i, j * n + (i + 1) % n
                f.append((a, b2, b2 + n, a + n))
        o = (rings - 1) * n
        f += [(o + i, o + (i + 1) % n, len(v) - 1) for i in range(n)]
        self.add(v, f, kind, True, M)

    def sphere(self, c, r, n, rings, kind, M=None):
        M = M or Matrix.Identity(4)
        self.dome(c, r, r, n, rings, kind, M)
        T = Matrix.Translation(c)
        self.dome(c, r, r, n, rings, kind, M @ T @ Matrix.Rotation(math.pi, 4, 'X') @ T.inverted())

    def bipyramid(self, c, r, up, dn, n, kind, M=None, a0=0.0):
        cx, cy, cz = c
        eq = [(cx + r * math.cos(math.radians(a0) + 2 * math.pi * i / n), cy + r * math.sin(math.radians(a0) + 2 * math.pi * i / n), cz) for i in range(n)]
        v = eq + [(cx, cy, cz + up), (cx, cy, cz - dn)]
        f = [(i, (i + 1) % n, n) for i in range(n)] + [((i + 1) % n, i, n + 1) for i in range(n)]
        self.add(v, f, kind, False, M)

    def torus(self, c, R, r, n, m, kind, M=None):
        v, f = [], []
        for i in range(n):
            a = 2 * math.pi * i / n
            for j in range(m):
                b2 = 2 * math.pi * j / m
                v.append((c[0] + (R + r * math.cos(b2)) * math.cos(a), c[1] + (R + r * math.cos(b2)) * math.sin(a), c[2] + r * math.sin(b2)))
        for i in range(n):
            for j in range(m):
                a, b2 = i * m + j, i * m + (j + 1) % m
                a2, b3 = ((i + 1) % n) * m + j, ((i + 1) % n) * m + (j + 1) % m
                f.append((a, a2, b3, b2))
        self.add(v, f, kind, True, M)

    def scaled(self, s):
        self.v = [(x * s, y * s, z * s) for x, y, z in self.v]
        return self


# ================================================================ mesh finishing
def finish_mesh(b: B, name, mats, cull=True):
    """Weld, cull hidden faces, trim-sheet UVs, material slots -> mesh object (not linked)."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in b.v]
    bm.verts.index_update()
    kl = bm.faces.layers.int.new("kind")
    for f, kd, sm in zip(b.f, b.k, b.s):
        try:
            face = bm.faces.new([vs[i] for i in f])
        except ValueError:          # duplicate face
            continue
        face[kl] = KIND_LIST.index(kd)
        face.smooth = sm
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0006)
    bmesh.ops.dissolve_degenerate(bm, dist=0.0002, edges=bm.edges[:])
    bm.normal_update()
    if cull:
        bm.faces.ensure_lookup_table()
        tree = BVHTree.FromBMesh(bm)
        dead = []
        for face in bm.faces:
            n = face.normal
            c = face.calc_center_median()
            if n.length < 0.5:
                continue
            if n.z < -0.9 and c.z < 0.02:                      # sits on the ground
                dead.append(face)
                continue
            samples = [c] + [c.lerp(v.co, 0.8) for v in face.verts]
            hidden = True
            for p in samples:
                o = p + n * 0.002
                hit, hn, idx, dist = tree.ray_cast(o, n, 50.0)
                if hit is None:
                    hidden = False
                    break
                if hn.dot(n) > 0.0 or dist < 0.025:            # inside a solid, or pressed against one
                    continue
                hidden = False
                break
            if hidden:
                dead.append(face)
        bmesh.ops.delete(bm, geom=dead, context='FACES_ONLY')
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    # trim-sheet UVs: per face planar projection, uniform scale, fitted & centred inside its band
    uvl = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        kd = KIND_LIST[face[kl]]
        slot, band = KINDS[kd]
        face.material_index = slot
        v0, v1 = BANDS[band]
        bh = (v1 - v0) - 2 * PAD
        n = face.normal
        if abs(n.z) > 0.7:
            ua, va = Vector((1, 0, 0)), Vector((0, 1, 0))
        else:
            ua = Vector((0, 0, 1)).cross(n)
            ua = ua.normalized() if ua.length > 1e-6 else Vector((1, 0, 0))
            va = Vector((0, 0, 1))
        pu = [l.vert.co.dot(ua) for l in face.loops]
        pv = [l.vert.co.dot(va) for l in face.loops]
        span = max(pv) - min(pv)
        s = TEXEL_PER_M if span * TEXEL_PER_M <= bh else bh / max(span, 1e-6)
        off = v0 + PAD + (bh - span * s) * 0.5
        for l, a, c2 in zip(face.loops, pu, pv):
            l[uvl].uv = (a * s, off + (c2 - min(pv)) * s)
    # only the slots this mesh actually uses, in SLOTS order (castles: Trim, Carbon, TeamColour, Accent;
    # Core parts: Carbon, CoreEmission)
    used = sorted({f.material_index for f in bm.faces})
    remap = {g: i for i, g in enumerate(used)}
    for f in bm.faces:
        f.material_index = remap[f.material_index]
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    by_name = {m.name: m for m in mats}
    for g in used:
        me.materials.append(by_name[SLOTS[g]])
    me.update()
    ob = bpy.data.objects.new(name, me)
    return ob


def tris(ob):
    ob.data.calc_loop_triangles()
    return len(ob.data.loop_triangles)


def link(ob, parent=None):
    bpy.context.scene.collection.objects.link(ob)
    if parent is not None:
        ob.parent = parent
    return ob


def empty(name, loc, parent, size=0.5, kind='PLAIN_AXES'):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = kind
    e.empty_display_size = size
    e.location = loc
    return link(e, parent)


# ================================================================ shared Core module
# Authored in concept units (pre CASTLE_SCALE); socket = collar base centre.
CORE_CRYSTAL_Z = 1.35


def core_parts(mats, S):
    """Returns [(name, object, local location, rotation)] of the Core module; S = CASTLE_SCALE."""
    out = []
    # carbon collar / bezel: a dish so the crystal always has dark carbon behind it from overseer angles
    b = B()
    b.cyl((0, 0, 0), 1.45, 1.35, 0.30, 16, "CARB", smooth=False, caps=False)
    b.ring(0.80, 1.35, 0.30, 0.45, "CARB", n=16, smooth=False, ro_top=1.25, kind_inner="CORE")   # cyan inner lip
    b.add([(0.8 * math.cos(2 * math.pi * i / 16), 0.8 * math.sin(2 * math.pi * i / 16), 0.3) for i in range(16)],
          [list(range(16))], "CARB")                                                      # dish floor
    for i in range(6):                                                    # bezel teeth
        b.box((0, -1.32, 0.52), (0.36, 0.22, 0.16), "CARB", rz(60 * i + 30), top=(0.24, 0.14))
    out.append(("Core_Collar", finish_mesh(b.scaled(S), "Core_Collar", mats, cull=True), (0, 0, 0), (0, 0, 0)))
    # crystal (spins about its local up axis); origin at the crystal centre
    b = B()
    b.bipyramid((0, 0, 0), 0.80, 1.0, 0.85, 8, "CORE")
    b.bipyramid((0, 0, 0), 0.92, 0.18, 0.18, 8, "CARB", a0=22.5)          # carbon girdle band
    out.append(("Core_Crystal", finish_mesh(b.scaled(S), "Core_Crystal", mats, cull=False),
                (0, 0, CORE_CRYSTAL_Z * S), (0, 0, 0)))
    # two gimbal halos, flat in their local XY plane, tilted by object rotation, spin about local up
    for nm, R, rot in (("Core_HaloA", 1.18, (math.radians(24), 0, 0)), ("Core_HaloB", 1.36, (0, math.radians(-20), 0))):
        b = B()
        b.ring(R, R + 0.1, -0.06, 0.06, "CARB", n=24, smooth=False, kind_inner="CORE")
        out.append((nm, finish_mesh(b.scaled(S), nm, mats, cull=False), (0, 0, CORE_CRYSTAL_Z * S), rot))
    return out


def attach_core(socket, mats, S):
    total = 0
    for nm, ob, loc, rot in core_parts(mats[:4], S):
        link(ob, socket)
        ob.location = loc
        ob.rotation_euler = rot
        total += tris(ob)
    return total


# ================================================================ castles (concept units, front = -Y)
def build_ziggurat(L):
    """Concept A. Returns builder, anchors (concept units), core socket z."""
    b = B()
    # plinth pulled in to 12.4 m (concept) so the 18 m plot holds; front notch for the gate ramp
    b.box((0, 0.75, 0.25), (12.4, 10.9, 0.5), "CARB", top=(12.0, 10.7), shift=(0, -0.1))
    for sgn in (-1, 1):
        b.box((sgn * 3.75, -5.45, 0.25), (4.9, 1.5, 0.5), "CARB", top=(4.5, 1.3), shift=(-sgn * 0.2, 0.1))
    TIERS = [(5.0, 4.4, 0.5, 3.7), (3.7, 3.2, 3.95, 6.55), (2.5, 2.1, 6.8, 9.0)]
    LEDGE = [(4.65, "CARB"), (3.42, "TEAM"), (2.32, "CARB")]      # tier-2 ledge carries the civ colour from above
    for (hb, ht, z0, z1), (hl, lm) in zip(TIERS, LEDGE):
        b.box((0, 0, (z0 + z1) / 2), (2 * hb, 2 * hb, z1 - z0), "PANEL", top=(2 * ht, 2 * ht))
        b.cbox((0, 0, z1 + 0.125), (2 * hl, 2 * hl, 0.25), lm, 0.06, lod=L)
    for k in range(4):
        M = rz(90 * k)
        for ti, (hb, ht, z0, z1) in enumerate(TIERS):
            front_gate = (k == 0 and ti == 0)
            sl = (hb - ht) / (z1 - z0)

            def face(x, z, off=0.0, hb=hb, sl=sl, z0=z0):
                return (x, -(hb - sl * (z - z0)) - off, z)
            if not front_gate and L <= 1:                          # bronze "trim-fall" down each face
                b.beam(face(0, z0 + 0.15, 0.02), face(0, z1 - 0.1, 0.02), 1.0, 0.10, "CARB", M)
                b.beam(face(0, z0 + 0.3, 0.07), face(0, z1 - 0.2, 0.07), 0.34, 0.08, "TEAM", M)
            if L <= 1:
                for fx in ((0.38, 0.62, 0.86) if L == 0 else (0.5, 0.86)):
                    for sgn in (-1, 1):
                        x = sgn * fx * ht
                        b.beam(face(x, z0, 0.0), face(x, z1 - 0.05, 0.0), 0.28 - 0.04 * ti, 0.36, "RIB", M)
                        if L == 0:
                            b.beam(face(x, z0 + 0.5, 0.2), face(x, z1 - 0.5, 0.2), 0.07, 0.05, "EDGE", M)
            if L == 0:
                for sgn in (-1, 1):
                    x = sgn * 0.5 * ht
                    zc = (z0 + z1) / 2
                    b.beam(face(x - 0.3, zc, 0.02), face(x + 0.3, zc, 0.02), 0.06, 0.5, "GLASS", M)
            if L <= 1:
                nf = (7, 5, 3)[ti]
                for i in range(nf):
                    x = (i - (nf - 1) / 2) * (2 * ht - 0.9) / max(nf - 1, 1)
                    if abs(x) < 0.3:
                        continue
                    b.box((x, -ht - 0.02, z1 + 0.48), (0.42, 0.3, 0.46), "PANEL" if ti else "CARB", M, top=(0.3, 0.16))
            b.box((0, -ht - 0.03, z1 - 0.16), (2 * ht - 0.5, 0.06, 0.12), "TEAM", M)
            if ti <= 1:                                            # player-accent banners (tiers 1-2), all LODs
                bx, bw, bl = ((3.25, 0.7, 2.2), (2.36, 0.48, 1.9))[ti]
                for sgn in (-1, 1):
                    x = sgn * bx
                    b.beam(face(x, z1 - 0.35, 0.1), face(x, z1 - 0.35 - bl, 0.1), bw, 0.05, "ACCENT", M)
                    if L <= 1:
                        b.beam(face(x - bw / 2 - 0.08, z1 - 0.3, 0.14), face(x + bw / 2 + 0.08, z1 - 0.3, 0.14), 0.08, 0.08, "CARB", M)
        if L == 0:
            for sgn in (-1, 1):
                b.cyl((sgn * 2.6, -4.05, 3.95), 0.28, 0.28, 0.9, 10, "PANEL_DK", M)
                b.cyl((sgn * 2.6, -4.05, 4.85), 0.28, 0.0, 0.25, 10, "CARB", M)
                b.cbox((sgn * 1.7, -4.0, 4.2), (0.7, 0.5, 0.5), "VENT", 0.05, M)
                b.cbox((sgn * 1.2, -2.85, 7.0), (0.6, 0.45, 0.4), "PANEL_DK", 0.05, M)
                for i in range(3):
                    b.box((sgn * 5.35, -3.0 + i * 3.0, 0.62), (0.5, 1.6, 0.24), "RIB", M, top=(0.3, 1.4))
        Mc = rz(90 * k + 45)
        r = 5.45 * math.sqrt(2)
        Mo = Mc @ Matrix.Translation((0, -r, 0)) @ rz(45)
        b.cbox((0, 0, 0.7), (1.5, 1.5, 0.4), "PANEL_DK", 0.08, Mo, lod=L)            # pylon foot (pulled in)
        b.box((0, 0, 3.2), (1.3, 1.3, 4.6), "CARB", Mo, top=(0.8, 0.8))
        b.cbox((0, 0, 5.62), (1.05, 1.05, 0.24), "EDGE", 0.05, Mo, lod=L)
        b.bipyramid((0, 0, 6.2), 0.3, 0.55, 0.4, 4, "TEAM", Mo, 45)
        if L <= 1:
            for q in range(4):
                b.box((0, -0.57, 3.2), (0.1, 0.06, 3.2), "TEAM", Mo @ rz(90 * q), top=(0.1, 0.06), shift=(0, 0.17))
        b.beam((0, -r + 0.5, 4.6), (0, -4.6, 6.75), 0.26, 0.34, "RIB", Mc)
        if L <= 1:
            b.beam((0, -r + 0.5, 2.2), (0, -6.3, 3.75), 0.22, 0.28, "RIB", Mc)
        P0, P1, P2, P3 = (0, -2.75, 9.2), (0, -3.5, 10.7), (0, -2.7, 12.3), (0, -1.45, 13.3)
        b.beam(P0, P1, 0.62, 0.5, "PANEL", Mc); b.beam(P1, P2, 0.52, 0.42, "PANEL", Mc); b.beam(P2, P3, 0.4, 0.3, "PANEL", Mc)
        b.cbox((0, -3.5, 10.7), (0.8, 0.7, 0.5), "CARB", 0.08, Mc, lod=L)
        if L <= 1:
            b.cbox((0, -2.7, 12.3), (0.62, 0.55, 0.4), "CARB", 0.06, Mc, lod=L)
            b.beam((0, -3.18, 10.75), (0, -2.42, 12.25), 0.12, 0.06, "TEAM", Mc)
            b.beam((0, -2.5, 9.25), (0, -3.2, 10.65), 0.14, 0.06, "TEAM", Mc)
    # gatehouse + ramp (ramp shortened vs the concept so the castle stays on the 18 m plot)
    GY = 0.35                                          # gatehouse pulled back 0.35 (concept m) to stay on the plot
    for sgn in (-1, 1):
        b.box((sgn * 1.55, -5.35 + GY, 1.95), (0.9, 1.9, 2.9), "PANEL", top=(0.7, 1.7))
        b.box((sgn * 1.55, -6.32 + GY, 1.8), (0.12, 0.06, 2.2), "TEAM")
        if L <= 1:
            b.box((sgn * 2.6, -5.95, 0.3), (0.5, 0.4, 0.6), "RIB", top=(0.3, 0.3))
    b.cbox((0, -5.35 + GY, 3.65), (4.3, 2.1, 0.6), "CARB", 0.1, lod=L)
    b.box((0, -6.42 + GY, 3.65), (3.2, 0.06, 0.16), "TEAM")
    b.box((0, -5.2 + GY, 1.7), (2.2, 0.3, 2.6), "VENT")
    if L <= 1:
        b.box((0, -5.37 + GY, 1.7), (0.1, 0.06, 2.4), "TEAM")
    # gate ramp in the plinth notch: z 0.5 at the door (y -4.7) down to the plot edge (y -6.2)
    b.add([(-1.3, -4.7, 0), (1.3, -4.7, 0), (1.3, -4.7, 0.5), (-1.3, -4.7, 0.5), (-1.3, -6.2, 0), (1.3, -6.2, 0)],
          [(4, 5, 2, 3), (0, 3, 4), (1, 5, 2), (0, 4, 5, 1), (0, 1, 2, 3)], "CARB")
    # core cradle (the shared Core collar seats on top at z = 9.65)
    b.cyl((0, 0, 9.2), 1.75, 1.5, 0.45, 24 if L == 0 else (16 if L == 1 else 8), "CARB")
    anchors = {
        "CommonsKeep": (0, 0, (0.5 + 3.7) / 2),        # tier 1, the keep mass
        "CommonsDome": (0, 0, (3.95 + 9.0) / 2),       # upper tiers 2+3, the command block
        "CitadelCrownY": (0, 0, 13.3),                 # crown claw tips
    }
    for k in range(4):                                 # four corner obelisk pylons, tip height
        a = math.radians(90 * k + 45)
        r = 5.45 * math.sqrt(2)
        anchors[f"CommonsTower_{k}"] = (r * math.sin(a), -r * math.cos(a), 6.2)
    return b, anchors, 9.65, 1.0


def build_ring(L):
    """Concept B (concept scale 0.9). Cage widened / raised so the shared Core and its halos clear it."""
    b = B()
    Z0 = 0.35
    n48 = (48, 32, 20)[L]
    b.cyl((0, 0, 0), 6.5, 6.3, Z0, n48, "CARB")
    if L <= 1:
        b.ring(2.6, 4.7, Z0, Z0 + 0.06, "PANEL_DK", n=n48)
    GATES = (-90, 30, 150)
    WINGS = (90, 210, 330)
    GAP = 15
    seg = (18, 10, 6)[L]
    for g in GATES:
        a0, a1 = g + GAP, g + 120 - GAP
        b.ring(5.0, 5.95, Z0, 2.7, "PANEL", a0, a1, seg, ri_top=5.1, ro_top=5.8)
        b.ring(4.95, 5.95, 2.7, 2.92, "EDGE", a0, a1, seg)
        if L <= 1:
            b.ring(5.93, 5.99, 1.75, 1.93, "TEAM", a0 + 2, a1 - 2, seg, smooth=False)
        if L == 0:
            b.ring(5.2, 5.7, 2.92, 3.02, "RIB", a0 + 1, a1 - 1, seg)
            for i in range(11):
                a = a0 + 4 + (a1 - a0 - 8) * i / 10
                if abs(((a - WINGS[0]) % 120) - 0) < 12 or abs(((a - WINGS[0]) % 120) - 120) < 12:
                    continue
                b.box((0, -5.72, 3.2), (0.5, 0.28, 0.56), "PANEL", rz(a + 90), top=(0.34, 0.16))
        rr = lambda z: 5.95 - 0.15 * (z - Z0) / (2.7 - Z0) + 0.06           # battered outer wall radius + offset
        for da in (30, 46, 74, 90):                                            # player-accent wall banners (wing +-25 deg), all LODs
            b.beam((0, -rr(2.55), 2.55), (0, -rr(0.9), 0.9), 0.8, 0.04, "ACCENT", rz(g + da + 90))
        Mg = rz(g + 90)
        for sgn in (-1, 1):
            Mp = rz(g + 90 + sgn * (GAP + 1.5)) @ Matrix.Translation((0, -5.5, 0))
            b.box((0, 0, Z0 + 2.2), (1.25, 1.5, 4.4), "PANEL_DK", Mp, top=(0.8, 1.0))
            b.cbox((0, 0, Z0 + 4.52), (1.0, 1.2, 0.24), "EDGE", 0.05, Mp, lod=L)
            b.box((0, 0, Z0 + 4.95), (0.3, 0.5, 0.6), "TEAM", Mp, top=(0.1, 0.2))
            if L <= 1:
                b.box((0, -0.72, Z0 + 2.2), (0.12, 0.06, 3.0), "TEAM", Mp, top=(0.12, 0.06), shift=(0, 0.17))
                b.cbox((0, 0, Z0 + 0.25), (1.6, 1.85, 0.5), "CARB", 0.08, Mp, lod=L)
        b.cbox((0, -5.5, Z0 + 3.55), (3.1, 0.7, 0.5), "PANEL", 0.08, Mg, lod=L)
        b.box((0, -5.87, Z0 + 3.55), (2.3, 0.06, 0.14), "TEAM", Mg)
        b.box((0, -4.05, Z0 + 0.08), (0.3, 3.9, 0.05), "TEAM", Mg)
        if L <= 1:
            for sgn in (-1, 1):
                b.box((sgn * 0.75, -4.05, Z0 + 0.09), (0.16, 3.9, 0.07), "RIB", Mg)
        b.box((0, -6.65, Z0 / 2), (2.6, 0.5, Z0), "CARB", Mg, top=(2.6, 0.08), shift=(0, 0.21))   # steepened ramp
    for wa in WINGS:
        M = rz(wa + 90)
        yo, yi, zo, zi = -5.2, -1.6, 3.0, 5.9
        hb, ht = 1.05, 0.55
        v = [(-hb, yo, Z0), (hb, yo, Z0), (hb, yi, Z0), (-hb, yi, Z0), (-ht, yo, zo), (ht, yo, zo), (ht, yi, zi), (-ht, yi, zi)]
        b.add(v, [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], "PANEL", False, M)
        b.beam((0, yo + 0.1, zo + 0.06), (0, yi, zi + 0.06), 0.8, 0.14, "EDGE", M)
        b.beam((0, yo + 0.3, zo + 0.16), (0, yi + 0.2, zi + 0.16), 0.16, 0.08, "TEAM", M)
        if L <= 1:
            nr = 6 if L == 0 else 3
            for i in range(nr):
                t = (i + 0.5) / nr
                y = yo + (yi - yo) * t
                zt = zo + (zi - zo) * t
                for sgn in (-1, 1):
                    b.beam((sgn * (hb + 0.02), y, Z0), (sgn * (ht + 0.02), y, zt), 0.2, 0.16, "RIB", M)
                    if L == 0 and i < 5:
                        yy = y + (yi - yo) / 12
                        b.beam((sgn * (hb - 0.18), yy, Z0 + 1.0), (sgn * (hb - 0.34), yy, Z0 + 1.7), 0.3, 0.05, "GLASS", M)
        nd = (20, 12, 8)[L]
        b.cyl((0, -5.45, Z0), 1.3, 1.15, 3.5, nd, "PANEL_DK", M)
        if L <= 1:
            b.ring(1.16, 1.24, 2.5, 2.7, "TEAM", n=nd, M=M @ Matrix.Translation((0, -5.45, 0)), smooth=False)
        b.cyl((0, -5.45, Z0 + 3.5), 1.3, 1.3, 0.22, nd, "CARB", M)
        b.dome((0, -5.45, Z0 + 3.72), 1.05, 0.7, nd, (4, 3, 2)[L], "PANEL", M)
        if L <= 1:
            b.cyl((0, -5.45, Z0 + 4.35), 0.05, 0.03, 1.5, 6, "RIB", M)
            b.box((0.4, -5.45, Z0 + 5.4), (0.72, 0.04, 0.42), "ACCENT", M, top=(0.72, 0.04))     # accent pennant
            b.bipyramid((0, -5.45, Z0 + 5.9), 0.12, 0.14, 0.14, 4, "TEAM", M)
        b.beam((0, -2.2, 5.4), (0, -1.0, 9.7), 0.22, 0.3, "RIB", M)
    nh = (30, 18, 10)[L]
    b.cyl((0, 0, Z0), 2.35, 2.0, 4.0, nh, "PANEL")
    b.ring(1.7, 2.25, 4.35, 4.65, "EDGE", n=nh)
    b.cyl((0, 0, 4.35), 1.6, 1.35, 2.75, nh, "PANEL_DK")
    if L <= 1:
        b.ring(1.36, 1.5, 5.4, 5.6, "TEAM", n=nh, smooth=False)
    b.cyl((0, 0, 7.1), 2.0, 2.0, 0.3, nh, "CARB")                       # cradle disc: socket on top (7.4)
    CR, CT = 1.95, 10.6                                                 # cage radius / cage top (widened, raised)
    for i in range(6):
        Ms = rz(60 * i + 30)
        b.beam((0, -CR, 7.4), (0, -CR, CT + 0.15), 0.2, 0.16, "EDGE", Ms)
        b.beam((0, -CR, CT + 0.1), (0, -1.1, CT + 0.3), 0.16, 0.14, "RIB", Ms)      # spokes to the spire cap
        if L <= 1:
            b.beam((0, -CR, 8.0), (0, -1.6, 7.45), 0.12, 0.1, "RIB", Ms)
    dz = CT - 9.9
    b.ring(CR - 0.15, CR + 0.15, CT, CT + 0.2, "CARB", n=nh, smooth=False)       # open cage crown ring
    b.cyl((0, 0, CT), 1.3, 1.15, 0.4, nh, "CARB")                               # small spire cap (Core reads from above)
    b.cyl((0, 0, 10.3 + dz), 1.05, 0.28, 4.6, (24, 14, 8)[L], "PANEL")
    if L <= 1:
        for z, r in ((11.0, 0.98), (12.1, 0.8), (13.2, 0.62), (14.3, 0.44)):
            b.ring(r - 0.05, r + 0.09, z + dz, z + dz + 0.14, "TEAM", n=(24, 14)[L], smooth=False)
    b.cyl((0, 0, 14.9 + dz), 0.3, 0.3, 0.15, 12 if L < 2 else 6, "CARB")
    b.cyl((0, 0, 15.05 + dz), 0.06, 0.03, 1.5, 6, "RIB")
    b.bipyramid((0, 0, 16.6 + dz), 0.12, 0.16, 0.16, 4, "TEAM")
    nhalo = (48, 32, 16)[L]
    b.ring(2.75, 3.0, 11.85 + dz, 12.0 + dz, "RIB", n=nhalo)
    if L <= 1:
        b.ring(3.0, 3.05, 11.87 + dz, 11.98 + dz, "TEAM", n=nhalo, smooth=False)
    for i in range(3):
        b.cbox((0, -2.87, 11.92 + dz), (0.7, 0.5, 0.36), "EDGE", 0.06, rz(GATES[i] + 90), lod=L)
    anchors = {
        "CommonsKeep": (0, 0, (Z0 + 4.35) / 2),               # hub drum
        "CommonsDome": (0, 0, (4.35 + 7.1) / 2),              # upper hub tier under the Core cage
        "CitadelCrownY": (0, 0, 14.9 + dz + 0.15),            # spire cap
    }
    for i, wa in enumerate(WINGS):                            # bastion drums
        a = math.radians(wa + 90)
        anchors[f"CommonsTower_{i}"] = (5.45 * math.sin(a), -5.45 * math.cos(a), Z0 + 3.72 + 0.7)
    for i, g in enumerate(GATES):                             # gate towers (lintel top centre)
        a = math.radians(g + 90)
        anchors[f"CommonsTower_{3 + i}"] = (5.5 * math.sin(a), -5.5 * math.cos(a), Z0 + 4.64)
    return b, anchors, 7.4, 0.9


def build_dome(L):
    """Concept C (concept scale 0.93)."""
    b = B()
    Z0 = 0.3
    T = math.tan(math.radians(22.5))
    b.cyl((0, 0, 0), 6.75, 6.55, Z0, 8, "CARB", smooth=False, a0=22.5)

    def slab(M, xl, xr, kd="PANEL"):
        yo_b, yo_t, yi, zt = -6.1, -5.25, -4.6, 2.7
        v = [(xl, yo_b, Z0), (xr, yo_b, Z0), (xr, yi, Z0), (xl, yi, Z0),
             (xl * 0.86, yo_t, zt), (xr * 0.86, yo_t, zt), (xr * 0.86, yi, zt), (xl * 0.86, yi, zt)]
        b.add(v, [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], kd, False, M)

    def gp(x, t, off=0.0):
        return (x * (1 - 0.14 * t), -6.1 + 0.85 * t - off * 0.94, Z0 + 2.4 * t + off * 0.33)

    R, HZ, ZD = 4.3, 3.3, 1.5
    for k in range(8):
        M = rz(45 * k)
        W = 6.1 * T - 0.42
        if k == 0:
            for sgn in (-1, 1):
                slab(M, *sorted((sgn * 1.75, sgn * W)))
        else:
            slab(M, -W, W)
        if L <= 1:
            for sgn in (-1, 1):
                xs, pw = ([sgn * 2.0], 0.5) if k == 0 else ([sgn * 0.55, sgn * 1.6], 0.86)
                if L == 1:
                    xs = xs[-1:]
                for x in xs:
                    kd = "EDGE" if (k % 2 == 0) else "PANEL_DK"
                    b.beam(gp(x, 0.12, 0.05), gp(x, 0.72, 0.05), pw, 0.1, kd, M)
        if k != 0:
            b.beam(gp(-W * 0.8, 0.86, 0.02), gp(W * 0.8, 0.86, 0.02), 0.07, 0.3, "TEAM", M)
            b.cbox((0, -4.95, 2.82), (W * 1.5, 0.5, 0.24), "TEAM", 0.06, M, lod=L)       # team parapet (outer ring)
            if L <= 1:
                for i in range(4):
                    b.box(((i - 1.5) * 0.95, -5.05, 3.12), (0.5, 0.3, 0.38), "PANEL", M, top=(0.36, 0.2))
        Mc = rz(45 * k + 22.5)
        b.cyl((0, -6.12, Z0), 0.62, 0.5, 3.1, 8 if L < 2 else 6, "PANEL_DK", Mc, smooth=False, a0=22.5)
        b.cyl((0, -6.12, Z0 + 3.1), 0.62, 0.62, 0.18, 8 if L < 2 else 6, "CARB", Mc, smooth=False, a0=22.5)
        b.cyl((0, -6.12, Z0 + 3.28), 0.3, 0.2, 0.3, 8 if L < 2 else 4, "TEAM", Mc, smooth=False, a0=22.5)
        b.box((0, -6.68, Z0 + 1.75), (0.42, 0.06, 1.5), "ACCENT", Mc, top=(0.38, 0.06), shift=(0, 0.05))   # accent banner
        if L <= 1:
            b.box((0, -6.4, Z0 + 0.2), (1.3, 0.8, 0.4), "CARB", Mc, top=(1.0, 0.6))      # post foot (pulled in)
        nrib = (8, 5, 3)[L]
        pts = []
        for j in range(nrib):
            t = math.radians(4 + 68 * j / (nrib - 1))
            pts.append((0, -(R + 0.05) * math.cos(t), ZD + (HZ + 0.05) * math.sin(t)))
        if L <= 1:
            for p, q in zip(pts[:-1], pts[1:]):
                b.beam(p, q, 0.34, 0.16, "CARB", Mc)
            b.ring(R + 0.12, R + 0.2, ZD + 0.12, ZD + 0.38, "GLASS", 45 * k - 90 + 22.5 + 5, 45 * k - 90 + 67.5 - 5,
                   4 if L == 0 else 2, smooth=False)
        if L == 0:
            for j, tdeg in enumerate((22, 40, 57)):
                t = math.radians(tdeg)
                wdt = 2 * (R * math.cos(t)) * T * 0.62
                p0 = (0, -(R + 0.02) * math.cos(t - 0.12), ZD + (HZ + 0.02) * math.sin(t - 0.12))
                p1 = (0, -(R + 0.02) * math.cos(t + 0.12), ZD + (HZ + 0.02) * math.sin(t + 0.12))
                b.beam(p0, p1, wdt, 0.1, "EDGE" if (j + k) % 2 == 0 else "PANEL_DK", M)
    n = (48, 24, 16)[L]
    b.ring(4.2, 4.75, Z0, 1.5, "CARB", n=(32, 16, 8)[L])
    b.ring(4.3, 4.42, 1.5, 2.0, "ACCENT", n=n)                          # narrow inner accent trim at the dome foot
    b.dome((0, 0, 1.5), 4.3, 3.3, n, (10, 6, 4)[L], "PANEL")
    b.cyl((0, 0, 4.55), 1.75, 1.55, 0.5, (32, 16, 8)[L], "CARB")         # oculus cradle, socket on top (5.05)
    for i in range(8):
        Ma = rz(45 * i + 22.5)
        if L == 2 and i % 2:
            continue
        h = 4.6 if i % 2 == 0 else 3.0
        base = (0, -1.95, 4.4)
        tip = (0, -1.95 - h * 0.2, 4.4 + h)
        b.cbox((0, -1.95, 4.5), (0.5, 0.5, 0.5), "PANEL_DK", 0.06, Ma, lod=L)
        b.beam(base, tip, 0.11, 0.11, "EDGE", Ma)
        if L <= 1:
            for t in ((0.45, 0.65, 0.82) if L == 0 else (0.6,)):
                y = base[1] + (tip[1] - base[1]) * t
                z = base[2] + (tip[2] - base[2]) * t
                b.box((0, y, z), (0.9 - 0.6 * t, 0.05, 0.05), "RIB", Ma)
            b.beam((0, -1.95 - 0.35, 4.4 + 1.75), (0, -1.2, 5.0), 0.07, 0.07, "RIB", Ma)
        b.bipyramid((0, tip[1], tip[2] + 0.1), 0.1, 0.3, 0.12, 4, "TEAM", Ma)
        if L <= 1:                                                        # accent pennant below the mast tip
            pz = tip[2] - 0.9
            py = base[1] + (tip[1] - base[1]) * ((pz - base[2]) / (tip[2] - base[2]))
            b.box((0.33, py, pz), (0.6, 0.03, 0.4), "ACCENT", Ma)
    if L <= 1:
        b.torus((0, 0, 6.2), 2.33, 0.06, (48, 24)[L], (6, 4)[L], "RIB")
        b.ring(2.3, 2.36, 6.3, 6.36, "TEAM", n=(48, 24)[L], smooth=False)
    else:
        b.ring(2.27, 2.39, 6.14, 6.3, "RIB", n=16, smooth=False)
    b.box((0, -5.15, Z0 + 1.35), (3.5, 2.6, 2.7), "CARB", top=(3.0, 2.6))
    b.cbox((0, -5.15, Z0 + 2.85), (3.8, 2.9, 0.3), "PANEL", 0.08, lod=L)
    b.box((0, -6.47, Z0 + 1.15), (2.3, 0.1, 2.3), "VENT")
    b.box((0, -6.54, Z0 + 1.15), (0.1, 0.06, 2.2), "TEAM")
    for sgn in (-1, 1):
        if L <= 1:
            b.beam((sgn * 0.25, -6.54, Z0 + 1.9), (sgn * 0.95, -6.54, Z0 + 1.3), 0.06, 0.12, "TEAM")
            b.beam((sgn * 0.25, -6.54, Z0 + 1.2), (sgn * 0.95, -6.54, Z0 + 0.6), 0.06, 0.12, "TEAM")
        b.box((sgn * 1.55, -6.5, Z0 + 1.3), (0.5, 0.3, 2.6), "PANEL", top=(0.4, 0.2))
    b.box((0, -6.51, Z0 + 2.62), (2.6, 0.08, 0.14), "TEAM")
    b.box((0, -6.35, Z0 / 2), (3.0, 0.7, Z0), "CARB", top=(3.0, 0.45), shift=(0, 0.125))   # steep ramp, inside the plot
    for sgn in (-1, 1):
        Md = rz(180 + sgn * 45)
        nd = (20, 12, 8)[L]
        b.cyl((0, -4.45, 1.5), 0.12, 0.09, 1.9, 8 if L < 2 else 4, "RIB", Md)
        Mt = Md @ Matrix.Translation((0, -4.45, 3.5)) @ Matrix.Rotation(math.radians(55), 4, 'X')
        b.cyl((0, 0, 0), 0.15, 0.95, 0.38, nd, "PANEL", Mt)
        if L <= 1:
            b.cyl((0, 0, 0.38), 0.03, 0.03, 0.7, 6, "RIB", Mt)
            b.bipyramid((0, 0, 1.1), 0.08, 0.1, 0.1, 4, "TEAM", Mt)
            for a in (90, 270):
                Mv = rz(a + sgn * 22.5)
                b.cyl((0, -4.48, 1.5), 0.3, 0.26, 1.3, (12, 8)[L], "VENT", Mv)
                b.cyl((0, -4.48, 2.8), 0.34, 0.34, 0.12, (12, 8)[L], "CARB", Mv)
    anchors = {
        "CommonsKeep": (0, 0, ZD + HZ * 0.4),          # armoured dome mass
        "CommonsDome": (0, 0, ZD + HZ),                # dome apex (under the oculus cradle)
        "CitadelCrownY": (0, 0, 6.36),                 # antenna crown ring
    }
    for k in range(8):                                 # eight bastion posts, cap height
        a = math.radians(45 * k + 22.5)
        anchors[f"CommonsTower_{k}"] = (6.12 * math.sin(a), -6.12 * math.cos(a), Z0 + 3.58)
    return b, anchors, 5.05, 0.93


CASTLES = [
    ("SM_Citadel_Ziggurat", build_ziggurat),
    ("SM_Citadel_Ring", build_ring),
    ("SM_Citadel_Dome", build_dome),
]


# ================================================================ export
def export_fbx(root, path):
    bpy.ops.object.select_all(action="DESELECT")
    stack = [root]
    while stack:
        o = stack.pop()
        o.select_set(True)
        stack.extend(o.children)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        apply_unit_scale=True,
        object_types={"MESH", "EMPTY"},
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="RELATIVE",
        use_custom_props=False,
        bake_anim=False,
    )
    print(f"[SM] exported {path.name}")


def bounds(objs):
    pts = []
    for o in objs:
        pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    sc.unit_settings.length_unit = "METERS"


def save_blend(name):
    if BLEND_DIR:
        Path(BLEND_DIR).mkdir(parents=True, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(Path(BLEND_DIR) / f"{name}.blend"))


