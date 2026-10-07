"""
Solar Majesty -- Stalker den (enemy lair) as a real, sculpted-looking model.

Replaces the ~60-primitive den StalkerLair.BuildMarker assembles at runtime with one
scripted model in two states:

  Den_Active  -- segmented dark-chitin hive mound (~8.5 m), crown of horn ridges with
                 hot orange veins, three horned spires with glowing eyes, a lit cave
                 maw framed by a chitin arch, bone fangs and two big mandible tusks,
                 glowing egg pods, toxic slime, a ribcage + horned skull, a stone ring
                 with standing slabs behind, rubble, and a trampled dirt clearing.
  Den_Ruined  -- the same site burnt out: a jagged hollow stump, snapped spires, the
                 crown lying in the dirt, burst pods, broken fangs, a toppled slab,
                 scorched soil. No glow, so a cleared den reads "safe" at a glance.

Layout matches StalkerLair.BuildMarker (den-body local space, mouth toward -Z, pivot
at ground centre, footprint ~12.5 x 11 m clearing). The survey beacon StalkerLair adds
at lair-local (4.6, 0, -3.8) sits at body-local ~(5.94, 0, 0.57) once the body is turned
45 degrees; nothing solid is placed within ~1.3 m of it.

Each state's colour + ambient occlusion is baked (Cycles) into one atlas, so Unity needs
only URP Lit materials with a white _BaseColor (StalkerLair can tint per renderer):
  SM_StalkerDen_Active (body), SM_StalkerDen_Glow (emissive), SM_StalkerDen_Ruined.
Soil and stone are baked near-neutral so the per-planet tint multiplies cleanly.

Outputs:
  Assets/Resources/Dens/SM_StalkerDen.fbx         meshes Active_* / Ruined_*
  Assets/Resources/Dens/T_StalkerDen_Active.png   atlas (albedo; glow parts double as emission map)
  Assets/Resources/Dens/T_StalkerDen_Ruined.png
  Blender/exports/SM_StalkerDen.fbx, Blender/renders/stalker_den_*.png (turnaround)
  Blender/SolarMajesty_StalkerDen.blend

Unity hierarchy (assembled by the prefab step, see PR): StalkerDen > DenBody >
  Den_Active { Den_Hive, Den_Bones, Den_Glow, Den_Soil, Den_Stone }
  Den_Ruined { Den_Hive, Den_Bones, Den_Soil, Den_Stone }   (inactive)

Run:
  blender --background --python Blender/scripts/sm_stalker_den.py
  blender --background --python Blender/scripts/sm_stalker_den.py -- --no-render
"""

from __future__ import annotations

import math
import random
import shutil
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

SCRIPT_DIR = Path(__file__).resolve().parent
BLENDER_DIR = SCRIPT_DIR.parent
PROJECT_ROOT = BLENDER_DIR.parent
BLEND_OUT = BLENDER_DIR / "SolarMajesty_StalkerDen.blend"
EXPORT_DIR = BLENDER_DIR / "exports"
RENDER_DIR = BLENDER_DIR / "renders"
UNITY_DENS = PROJECT_ROOT / "Assets" / "Resources" / "Dens"
GRASS_TEX = PROJECT_ROOT / "Assets" / "Resources" / "Environment" / "Textures" / "SM_Ground_Earth_Albedo.png"

FBX_NAME = "SM_StalkerDen"
ATLAS_ACTIVE = 2048
ATLAS_RUINED = 1024

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
NO_RENDER = "--no-render" in ARGS

# Earth defaults StalkerLair / PlanetaryWorldGen pass for soil and stone (used only for previews).
EARTH_SOIL = (0.40, 0.31, 0.22)
EARTH_STONE = (0.52, 0.48, 0.43)

# Survey beacon in den-body space (Unity x, z) -- keep it clear.
BEACON_U = (5.94, 0.57)


# --------------------------------------------------------------------------- helpers

def U(x: float, y: float, z: float) -> Vector:
    """Unity den-body local (x right, y up, mouth at -Z) -> Blender (Z up).

    FBX export (forward -Z, up Y) + Unity's X mirror maps Blender (x, y, z) to Unity
    (-x, z, -y), so the mouth faces Blender +Y here."""
    return Vector((-x, -z, y))


def lin(c):
    """sRGB triple (as authored in StalkerLair) -> linear for Blender colour attributes."""
    def f(v):
        v = max(0.0, v)
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return (f(c[0]), f(c[1]), f(c[2]))


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def mul(a, k):
    return tuple(v * k for v in a)


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def nz(p: Vector, freq=1.0, seed=0.0) -> float:
    return noise.noise(Vector((p.x * freq + seed * 17.1, p.y * freq + seed * 3.7, p.z * freq - seed * 9.3)))


def interp(table, h):
    """Piecewise-linear lookup in [(h, value), ...]."""
    if h <= table[0][0]:
        return table[0][1]
    for (h0, v0), (h1, v1) in zip(table, table[1:]):
        if h <= h1:
            t = (h - h0) / max(1e-6, h1 - h0)
            return v0 + (v1 - v0) * t
    return table[-1][1]


# Palette (sRGB, StalkerLair's colours pushed a little for readability).
CHITIN_DEEP = (0.13, 0.05, 0.10)
CHITIN_MID = (0.36, 0.15, 0.28)
CHITIN_HI = (0.72, 0.38, 0.54)
CHITIN_TOP = (0.45, 0.20, 0.34)
MAW_DARK = (0.05, 0.02, 0.03)
BONE = (0.90, 0.85, 0.70)
BONE_DARK = (0.55, 0.47, 0.35)
VEIN_EDGE = (0.90, 0.22, 0.04)
VEIN_CORE = (1.00, 0.62, 0.12)
HOT_CORE = (1.00, 0.90, 0.55)
POD_LOW = (0.55, 0.10, 0.04)
POD_HI = (1.00, 0.55, 0.12)
SLIME = (0.30, 0.62, 0.10)
NEUTRAL = (0.93, 0.93, 0.93)

ASH_DEEP = (0.17, 0.16, 0.16)
ASH_MID = (0.50, 0.47, 0.45)
ASH_HI = (0.72, 0.69, 0.65)
BONE_BURNT = (0.62, 0.57, 0.48)


class Group:
    """One output mesh object, built in a single bmesh with a colour layer."""

    def __init__(self, name: str):
        self.name = name
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")
        self.mat_keys: list[str] = []

    def mat_index(self, key: str) -> int:
        if key not in self.mat_keys:
            self.mat_keys.append(key)
        return self.mat_keys.index(key)

    def paint(self, faces, fn, key: str, smooth: bool = True):
        self.bm.normal_update()
        mi = self.mat_index(key)
        for f in faces:
            f.material_index = mi
            f.smooth = smooth
            for loop in f.loops:
                c = fn(loop.vert.co, loop.vert.normal if smooth else f.normal, loop.vert)
                loop[self.col] = (*lin(c), 1.0)

    def tris(self) -> int:
        return sum(len(f.verts) - 2 for f in self.bm.faces)

    def to_object(self, col: bpy.types.Collection) -> bpy.types.Object:
        me = bpy.data.meshes.new(f"{FBX_NAME}_{self.name}")
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, me)
        col.objects.link(obj)
        for key in self.mat_keys:
            me.materials.append(bake_material(key))
        return obj


# --------------------------------------------------------------------------- primitives

def faces_of(verts):
    out = set()
    for v in verts:
        out.update(v.link_faces)
    return list(out)


def lathe(g: Group, rings, segs: int, disp=None, phase: float = 0.0):
    """rings: [(center Vector, radius_x, radius_y, extra)] bottom->top. radius 0 => tip.
    disp(ring_i, theta, pos, extra) -> pos. Bottom left open (sits in the ground)."""
    bm = g.bm
    rows = []
    for i, (c, rx, ry, extra) in enumerate(rings):
        if rx <= 1e-5:
            rows.append([bm.verts.new(c)])
            continue
        row = []
        for s in range(segs):
            th = phase + 2 * math.pi * s / segs
            p = Vector((c.x + math.cos(th) * rx, c.y + math.sin(th) * ry, c.z))
            if disp:
                p = disp(i, th, p, extra)
            row.append(bm.verts.new(p))
        rows.append(row)
    faces = []
    for a, b in zip(rows, rows[1:]):
        if len(b) == 1:
            for s in range(segs):
                faces.append(bm.faces.new((a[s], a[(s + 1) % segs], b[0])))
        elif len(a) == 1:
            for s in range(segs):
                faces.append(bm.faces.new((a[0], b[(s + 1) % segs], b[s])))
        else:
            for s in range(segs):
                faces.append(bm.faces.new((a[s], a[(s + 1) % segs], b[(s + 1) % segs], b[s])))
    return faces, rows


def tube(g: Group, pts, radii, sides: int = 6, cap_end: bool = False, flatten: float = 1.0):
    """Tapered tube along pts. A radius of 0 at the end makes a point; cap_end closes a blunt end."""
    bm = g.bm
    pts = [Vector(p) for p in pts]
    n = len(pts)
    tangents = []
    for i in range(n):
        a = pts[max(0, i - 1)]
        b = pts[min(n - 1, i + 1)]
        tangents.append((b - a).normalized())
    ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
    normal = tangents[0].cross(ref).normalized()
    rows = []
    for i in range(n):
        t = tangents[i]
        normal = (normal - t * normal.dot(t)).normalized()
        binorm = t.cross(normal).normalized()
        r = radii[i]
        if r <= 1e-5:
            rows.append([bm.verts.new(pts[i])])
            continue
        row = []
        for s in range(sides):
            th = 2 * math.pi * s / sides
            off = normal * (math.cos(th) * r) + binorm * (math.sin(th) * r * flatten)
            row.append(bm.verts.new(pts[i] + off))
        rows.append(row)
    faces = []
    for a, b in zip(rows, rows[1:]):
        if len(b) == 1:
            for s in range(sides):
                faces.append(bm.faces.new((a[s], a[(s + 1) % sides], b[0])))
        else:
            for s in range(sides):
                faces.append(bm.faces.new((a[s], a[(s + 1) % sides], b[(s + 1) % sides], b[s])))
    if cap_end and len(rows[-1]) > 2:
        faces.append(bm.faces.new(rows[-1]))
    # Start cap (blunt base) if it is above ground.
    if len(rows[0]) > 2 and pts[0].z > 0.05:
        faces.append(bm.faces.new(list(reversed(rows[0]))))
    return faces


def ellipsoid(g: Group, center: Vector, size: Vector, u=8, v=6, rot: Matrix | None = None, disp=None):
    ret = bmesh.ops.create_uvsphere(g.bm, u_segments=u, v_segments=v, radius=0.5)
    verts = ret["verts"]
    m = Matrix.Translation(center) @ (rot.to_4x4() if rot else Matrix.Identity(4)) @ Matrix.Diagonal((*size, 1.0))
    for vt in verts:
        local = vt.co.copy()
        vt.co = m @ local
        if disp:
            vt.co = disp(vt.co, local)
    return faces_of(verts)


def rock(g: Group, center: Vector, size: Vector, rot: Matrix, seed: float, bevel: float = 0.18, jitter: float = 0.12):
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    if bevel > 0:
        bmesh.ops.bevel(tmp, geom=list(tmp.verts) + list(tmp.edges), offset=bevel, segments=1,
                        affect="EDGES", profile=0.5)
    m = Matrix.Translation(center) @ rot.to_4x4() @ Matrix.Diagonal((*size, 1.0))
    vmap = {}
    for vt in tmp.verts:
        local = vt.co.copy()
        j = Vector((nz(local, 2.3, seed), nz(local, 2.3, seed + 1), nz(local, 2.3, seed + 2))) * jitter
        vmap[vt] = g.bm.verts.new(m @ (local + j))
    faces = [g.bm.faces.new([vmap[v] for v in f.verts]) for f in tmp.faces]
    tmp.free()
    return faces


def rot_euler(x=0.0, y=0.0, z=0.0) -> Matrix:
    return (Matrix.Rotation(math.radians(z), 3, "Z") @ Matrix.Rotation(math.radians(y), 3, "Y")
            @ Matrix.Rotation(math.radians(x), 3, "X"))


def bury_check(p: Vector):
    """True if Blender-space point p is too close to the survey beacon."""
    bx, bz = BEACON_U
    b = U(bx, 0, bz)
    return (Vector((p.x, p.y)) - Vector((b.x, b.y))).length < 1.35


# --------------------------------------------------------------------------- hive shape

# (h, radius) chitin plates: a lip at the bottom of each tier, a tucked groove above it.
HIVE_PROFILE = [
    (-0.25, 2.95), (0.0, 2.85), (0.35, 2.68), (0.8, 2.48), (1.25, 2.30), (1.6, 2.12),
    (1.8, 1.66), (2.15, 1.86), (2.75, 1.76), (3.15, 1.56),
    (3.35, 1.16), (3.75, 1.30), (4.35, 1.18), (4.7, 1.02),
    (4.88, 0.76), (5.25, 0.86), (5.75, 0.70), (5.95, 0.50),
    (6.3, 0.50), (6.9, 0.36), (7.5, 0.24), (8.05, 0.12), (8.55, 0.0),
]
# Outer envelope (for ridges riding over the plates).
HIVE_ENVELOPE = [(-0.25, 2.95), (1.6, 2.2), (3.15, 1.62), (4.7, 1.08), (5.95, 0.62), (7.5, 0.3), (8.55, 0.05)]
LIPS = {1.6, 3.15, 4.7, 5.95}
GROOVES = {1.8, 3.35, 4.88}


def hive_center(h: float) -> Vector:
    # Leans back (Unity +Z = Blender -Y) like the original stacked spheres, more near the spike.
    back = 0.70 + 0.165 * h + 0.03 * max(0.0, h - 5.5) ** 2
    return U(0.0, h, back)


def front_y(h: float) -> float:
    """Blender Y of the hive's front surface (mouth side) at height h."""
    return hive_center(h).y + interp(HIVE_PROFILE, h)


MAW_Z = 1.0          # Blender z of the maw centre
MAW_HALF_W = 1.30
MAW_HALF_H = 1.12
MAW_DEPTH = 1.35


def build_hive(g: Group, ruined: bool, seed: float):
    segs = 20
    prof = HIVE_PROFILE
    if ruined:
        prof = [p for p in HIVE_PROFILE if p[0] <= 3.2]
    rings = []
    for h, r in prof:
        tag = 1.0 if h in LIPS else (-1.0 if h in GROOVES else 0.0)
        rings.append((hive_center(h), r, r, (h, tag)))

    def disp(i, th, p, extra):
        h, tag = extra
        c = hive_center(h)
        rr = (p - Vector((c.x, c.y, p.z))).length
        if rr < 1e-4:
            return p
        d = (p - Vector((c.x, c.y, p.z))) / rr
        lobes = 1.0 + 0.07 * math.sin(5 * th + h * 1.3) + 0.05 * nz(p, 0.9, seed)
        if ruined:
            lobes *= 1.0 + 0.10 * nz(p, 1.7, seed + 4)
        p = Vector((c.x, c.y, p.z)) + d * rr * lobes
        # Maw recess on the front (+Y) side.
        if d.y > 0.15 and h < 2.4:
            dx = p.x / MAW_HALF_W
            dz = (p.z - MAW_Z) / MAW_HALF_H
            q = dx * dx + dz * dz
            if q < 1.0:
                k = (1.0 - q) ** 0.6
                depth = MAW_DEPTH * (0.55 if ruined else 1.0)
                p.y -= depth * k
                p.z += 0.10 * k
        return p

    if ruined:
        # Jagged burnt rim at the top of the stump, then a hollow throat inside.
        top_h = prof[-1][0]
        rings[-1] = (hive_center(top_h), prof[-1][1], prof[-1][1], (top_h, 0.0))
    faces, rows = lathe(g, rings, segs, disp, phase=math.pi / 2)
    if ruined:
        top = rows[-1]
        for s, vt in enumerate(top):
            jag = (0.75 if s % 5 in (0, 2) else (-0.2 if s % 5 == 4 else 0.2)) * (0.6 + 0.8 * abs(nz(vt.co, 3.1, seed + s))) - 0.1
            vt.co.z += jag
        c = hive_center(prof[-1][0])
        inner = []
        for s, vt in enumerate(top):
            d = vt.co - Vector((c.x, c.y, vt.co.z))
            inner.append(g.bm.verts.new(Vector((c.x, c.y, prof[-1][0] - 0.45)) + d * 0.62))
        pit = g.bm.verts.new(Vector((c.x, c.y, 1.2)))
        n = len(top)
        for s in range(n):
            faces.append(g.bm.faces.new((top[s], top[(s + 1) % n], inner[(s + 1) % n], inner[s])))
            faces.append(g.bm.faces.new((inner[s], inner[(s + 1) % n], pit)))

    zmax = prof[-1][0]

    def color(co, nrm, vt):
        h = co.z
        c = hive_center(max(-0.25, min(8.5, h)))
        rr = (Vector((co.x - c.x, co.y - c.y))).length
        env = interp(HIVE_ENVELOPE, h)
        lip = smooth01((rr / max(0.05, env) - 0.78) / 0.2)  # outer plate edges catch light
        up = smooth01(h / 8.0)
        deep, mid, hi, top = (ASH_DEEP, ASH_MID, ASH_HI, ASH_MID) if ruined else (CHITIN_DEEP, CHITIN_MID, CHITIN_HI, CHITIN_TOP)
        base = mix(deep, mid, 0.35 + 0.65 * smooth01((h + 0.2) / 1.6))
        base = mix(base, top, up * 0.6)
        base = mix(base, hi, lip * 0.9)
        # Maw interior.
        if co.y > c.y and h < 2.6:
            dx = co.x / MAW_HALF_W
            dz = (co.z - MAW_Z) / MAW_HALF_H
            q = dx * dx + dz * dz
            if q < 1.15:
                base = mix(base, MAW_DARK, smooth01((1.15 - q) / 0.35))
                if not ruined and q < 0.55:
                    base = mix(base, (0.55, 0.12, 0.03), smooth01((0.55 - q) / 0.4) * 0.6)
        if ruined:
            if h > zmax - 0.6 or rr < env * 0.66:
                base = mix(base, ASH_DEEP, 0.6)   # charred rim and hollow throat
            base = mix(base, (0.28, 0.24, 0.22), 0.25 * max(0.0, nz(co, 1.3, seed + 2)))
        return base

    g.paint(faces, color, "ash" if ruined else "chitin")


def build_maw_frame(g_chitin: Group, g_bone: Group, ruined: bool, seed: float):
    # Arch lip round the mouth, riding on the hive's front surface.
    pts, radii = [], []
    n = 13
    for i in range(n):
        t = math.pi * i / (n - 1)
        x = -math.cos(t) * (MAW_HALF_W + 0.22)
        z = -0.05 + (MAW_HALF_H + 1.05) * math.sin(t)
        if ruined and 0.42 * math.pi < t < 0.70 * math.pi:
            continue  # the crown of the arch has fallen in
        y = front_y(max(0.0, z)) + 0.02
        pts.append(Vector((x, y, z)))
        radii.append(0.36 - 0.08 * math.sin(t) + 0.05 * math.sin(i * 2.1))
    if ruined:
        # Two broken stubs.
        k = len(pts) // 2
        for part in (pts[:k], pts[k:]):
            pass
        left, right = pts[:k], pts[k:]
        lr, rr_ = radii[:k], radii[k:]
        faces = tube(g_chitin, left, lr, sides=6, cap_end=True)
        faces += tube(g_chitin, right, rr_, sides=6, cap_end=True)
    else:
        faces = tube(g_chitin, pts, radii, sides=7)

    def arch_col(co, nrm, vt):
        if ruined:
            return mix(ASH_MID, ASH_HI, 0.3 + 0.4 * max(0.0, nrm.z))
        return mix(CHITIN_MID, CHITIN_HI, 0.35 + 0.5 * max(0.0, nrm.z + nrm.y * 0.4))

    g_chitin.paint(faces, arch_col, "ash" if ruined else "chitin")

    # Fangs hanging round the arch + two big mandible tusks at the ground.
    bone_faces = []
    fang_t = [0.16, 0.30, 0.44, 0.56, 0.70, 0.84]
    for j, tt in enumerate(fang_t):
        t = math.pi * tt
        x = -math.cos(t) * (MAW_HALF_W + 0.15)
        z = -0.05 + (MAW_HALF_H + 0.95) * math.sin(t)
        y = front_y(max(0.0, z)) + 0.12
        base = Vector((x, y, z))
        inward = Vector((-x, 0.0, MAW_Z - z)).normalized()
        L = 0.95 + 0.25 * math.sin(j * 1.7)
        if ruined:
            if j in (2, 3):
                continue
            L *= 0.45
        p1 = base + Vector((0, 0.35, 0)) * L + inward * 0.25 * L
        p2 = base + Vector((0, 0.55, 0)) * L + inward * 0.75 * L
        p3 = base + Vector((0, 0.50, 0)) * L + inward * 1.05 * L
        radii = [0.17, 0.13, 0.08, 0.0] if not ruined else [0.17, 0.13, 0.09, 0.07]
        bone_faces += tube(g_bone, [base, p1, p2, p3], radii, sides=5, cap_end=ruined)
    for sgn in (-1, 1):
        L = 1.0 if not ruined else 0.55
        b = Vector((sgn * 1.85, front_y(0.3) - 0.25, 0.15))
        pts = [b,
               b + Vector((sgn * 0.25, 0.75, 0.55)) * L,
               b + Vector((sgn * 0.05, 1.55, 1.05)) * L,
               b + Vector((-sgn * 0.55, 2.15, 1.25)) * L,
               b + Vector((-sgn * 1.05, 2.45, 1.05)) * L]
        radii = [0.34, 0.30, 0.22, 0.12, 0.0]
        if ruined:
            pts = pts[:3]
            radii = [0.34, 0.30, 0.24]
        bone_faces += tube(g_bone, pts, radii, sides=6, cap_end=ruined)

    def fang_col(co, nrm, vt):
        b, d = (BONE_BURNT, ASH_MID) if ruined else (BONE, BONE_DARK)
        return mix(d, b, 0.45 + 0.55 * max(0.0, nrm.z * 0.7 + 0.5))

    g_bone.paint(bone_faces, fang_col, "bone")


def build_ridges(g_chitin: Group, g_glow: Group | None, ruined: bool, seed: float):
    faces = []
    angles = [30, 90, 150, 210, 270, 330]
    for k, a_deg in enumerate(angles):
        if ruined and k % 2 == 1:
            continue
        a = math.radians(a_deg)
        du = (math.cos(a), math.sin(a))
        d = Vector((-du[0], -du[1], 0.0))  # Blender direction
        h0 = 2.45 if a_deg == 270 else 0.15
        h1 = 5.0 if not ruined else 2.6
        pts, radii = [], []
        steps = 6
        for i in range(steps + 1):
            h = h0 + (h1 - h0) * i / steps
            c = hive_center(h)
            pts.append(Vector((c.x, c.y, h)) + d * (interp(HIVE_ENVELOPE, h) + 0.08))
            radii.append(0.25 - 0.08 * i / steps)
        if ruined:
            radii[-1] = 0.2
            faces += tube(g_chitin, pts, radii, sides=5, cap_end=True)
            continue
        tip = pts[-1] + d * 0.85 + Vector((0, 0, 0.75))
        pts += [pts[-1] + d * 0.45 + Vector((0, 0, 0.55)), tip]
        radii += [0.15, 0.0]
        faces += tube(g_chitin, pts, radii, sides=5)

    def col(co, nrm, vt):
        if ruined:
            return mix(ASH_DEEP, ASH_HI, 0.35 + 0.35 * max(0.0, nrm.z))
        return mix(CHITIN_MID, CHITIN_HI, 0.3 + 0.6 * smooth01(co.z / 6.0) + 0.2 * max(0.0, nrm.z))

    g_chitin.paint(faces, col, "ash" if ruined else "chitin")

    if g_glow is None:
        return
    vfaces = []
    for k in range(6):
        a = math.radians(60 * k)
        pts, radii = [], []
        steps = 10
        for i in range(steps + 1):
            h = 0.35 + 4.7 * i / steps
            wob = math.radians(9.0 * math.sin(i * 1.3 + k))
            aa = a + wob
            d = Vector((-math.cos(aa), -math.sin(aa), 0.0))
            if d.y > 0.55 and h < 2.5:
                continue  # never across the maw
            c = hive_center(h)
            pts.append(Vector((c.x, c.y, h)) + d * (interp(HIVE_PROFILE, h) + 0.02))
            radii.append(0.13 * (1.0 - 0.45 * i / steps))
        if len(pts) < 3:
            continue
        radii[-1] = 0.0
        vfaces += tube(g_glow, pts, radii, sides=3)

    def vcol(co, nrm, vt):
        return mix(VEIN_EDGE, VEIN_CORE, 0.5 + 0.5 * nz(co, 2.0, seed))

    g_glow.paint(vfaces, vcol, "glow")


SPIRES = [(-2.6, 1.6, 1.05), (2.5, 1.9, 0.92), (-0.8, 3.6, 1.18)]


def build_spires(g_chitin: Group, g_glow: Group | None, ruined: bool, seed: float):
    faces = []
    gfaces = []
    for si, (ux, uz, k) in enumerate(SPIRES):
        base = U(ux, 0, uz)
        out = Vector((base.x, base.y, 0)).normalized()
        prof = [(-0.1, 0.85), (0.5, 0.80), (0.95, 0.62), (1.1, 0.45), (1.55, 0.55), (2.0, 0.45),
                (2.15, 0.32), (2.6, 0.36), (3.1, 0.25), (3.8, 0.13), (4.5, 0.0)]
        if ruined:
            prof = prof[:5] if si != 2 else prof[:4]
        rings = []
        for h, r in prof:
            hh = h * k
            lean = out * (0.06 * hh + 0.035 * hh * hh)
            rings.append((base + lean + Vector((0, 0, hh)), r * k, r * k, (hh,)))

        def disp(i, th, p, extra, si=si):
            return p + (p - rings[i][0]).normalized() * 0.06 * nz(p, 1.8, seed + si) if (p - rings[i][0]).length > 1e-4 else p

        f, rows = lathe(g_chitin, rings, 9, disp)
        if ruined:
            top = rows[-1]
            c = rings[-1][0]
            for s, vt in enumerate(top):
                vt.co.z += (0.35 if s % 2 else -0.1) + 0.2 * nz(vt.co, 2.3, seed + si)
            cap = g_chitin.bm.verts.new(c + Vector((0, 0, -0.25)))
            for s in range(len(top)):
                f.append(g_chitin.bm.faces.new((top[s], top[(s + 1) % len(top)], cap)))
        faces += f
        if g_glow is not None:
            hh = 2.35 * k
            lean = out * (0.06 * hh + 0.035 * hh * hh)
            c = base + lean + Vector((0, 0, hh))
            r = interp([(p[0] * k, p[1] * k) for p in prof], hh)
            eye = c + Vector((0, 1, 0)) * (r * 0.92)
            gfaces += ellipsoid(g_glow, eye, Vector((0.42, 0.22, 0.55)) * k, u=6, v=4)

    def col(co, nrm, vt):
        if ruined:
            return mix(ASH_DEEP, ASH_HI, 0.3 + 0.4 * max(0.0, nrm.z))
        return mix(CHITIN_DEEP, CHITIN_HI, 0.25 + 0.55 * smooth01(co.z / 4.5) + 0.2 * max(0.0, nrm.z))

    g_chitin.paint(faces, col, "ash" if ruined else "chitin")
    if g_glow is not None:
        g_glow.paint(gfaces, lambda co, n, v: mix(VEIN_CORE, HOT_CORE, 0.5), "glow")


def build_maw_glow(g_glow: Group):
    c = Vector((0.0, front_y(0.8) - MAW_DEPTH * 0.72, 0.92))
    faces = ellipsoid(g_glow, c, Vector((1.55, 0.55, 1.25)), u=10, v=6)

    def col(co, nrm, vt):
        q = ((co.x - c.x) / 0.75) ** 2 + ((co.z - c.z) / 0.62) ** 2
        return mix(HOT_CORE, VEIN_EDGE, smooth01(q))

    g_glow.paint(faces, col, "glow")
    # Glowing drool / light spill on the floor of the mouth.
    tongue = ellipsoid(g_glow, Vector((0.0, front_y(0.2) - 0.25, 0.02)), Vector((1.3, 1.6, 0.12)), u=8, v=4)
    g_glow.paint(tongue, lambda co, n, v: mix(VEIN_EDGE, VEIN_CORE, 0.35), "glow")


POD_SPOTS_U = [  # (angle deg, dist, size) round the front, kept off the mouth's lane
    (203, 3.05, 0.95), (218, 2.55, 0.80), (226, 3.55, 0.70), (238, 2.75, 0.62),
    (300, 2.70, 0.70), (312, 3.15, 0.92), (326, 2.55, 0.66), (338, 3.45, 0.78), (214, 3.9, 0.55),
]


def build_pods(g: Group, ruined: bool, seed: float):
    faces = []
    for i, (a_deg, dist, size) in enumerate(POD_SPOTS_U):
        a = math.radians(a_deg)
        uc = (math.cos(a) * dist, math.sin(a) * dist + 0.4)
        s = size * 1.15
        c = U(uc[0], s * 0.55, uc[1])
        tilt = rot_euler(8 * math.sin(i * 2.3), 8 * math.cos(i * 1.7), 0)

        def disp(p, local, i=i, ruined=ruined, c=c, s=s):
            if ruined and local.z > -0.05:
                # Burst: fold the top half down into a ragged bowl.
                lz = local.z
                fold = -0.05 - (lz + 0.05) * 0.9 + 0.12 * nz(local, 6.0, seed + i)
                return p + Vector((0, 0, (fold - lz) * s * 1.35))
            return p + (p - c).normalized() * 0.04 * nz(p, 4.0, seed + i)

        faces.append((ellipsoid(g, c, Vector((s, s, s * 1.3)), u=8, v=5, rot=tilt, disp=disp), c, s))

    for f, c, s in faces:
        if ruined:
            g.paint(f, lambda co, n, v, c=c, s=s: mix(ASH_DEEP, (0.35, 0.22, 0.18), 0.3 + 0.4 * max(0.0, n.z) * 0.0 + 0.3 * smooth01((co.z - c.z + s * 0.6) / s)), "ash")
        else:
            def pc(co, n, v, c=c, s=s):
                t = smooth01((co.z - (c.z - s * 0.65)) / (s * 1.3))
                vein = max(0.0, nz(co, 5.0, seed)) * 0.6
                return mix(mix(POD_LOW, POD_HI, t), POD_LOW, vein)
            g.paint(f, pc, "glow")


def build_slime(g: Group, seed: float, ruined: bool):
    c = U(1.2, 0.03, -3.1)
    bm = g.bm
    n = 12
    center = bm.verts.new(c + Vector((0, 0, 0.03)))
    ring = []
    for i in range(n):
        th = 2 * math.pi * i / n + math.radians(25)
        r = 1.0 + 0.25 * nz(Vector((math.cos(th), math.sin(th), 0)), 1.5, seed)
        ring.append(bm.verts.new(c + Vector((math.cos(th) * 1.35 * r, math.sin(th) * 0.9 * r, 0.0))))
    faces = [bm.faces.new((center, ring[i], ring[(i + 1) % n])) for i in range(n)]
    if ruined:
        g.paint(faces, lambda co, nn, v: (0.10, 0.12, 0.06), "ash")
    else:
        g.paint(faces, lambda co, nn, v: mix(SLIME, (0.55, 0.85, 0.25), 0.4 if (co - c).length < 0.5 else 0.0), "glow")


def build_bones(g: Group, ruined: bool, seed: float):
    faces = []
    rib_at = U(-3.2, 0, -2.4)
    # Spine runs along Unity z (Blender -y).
    spine = [rib_at + Vector((0.0, 1.2 - i * 0.48, 0.92 + 0.12 * math.sin(i * 0.9))) for i in range(6)]
    spine[0].z = 0.1
    spine[-1].z = 0.55
    faces += tube(g, spine, [0.14, 0.17, 0.18, 0.17, 0.15, 0.10], sides=5, cap_end=True)
    for i in range(5):
        y = 0.95 - i * 0.42
        for sgn in (-1, 1):
            if ruined and (i + (sgn > 0)) % 3 == 0:
                continue
            p0 = rib_at + Vector((sgn * 0.10, y, 0.95))
            pts = [p0,
                   rib_at + Vector((sgn * 0.62, y + 0.05, 1.05)),
                   rib_at + Vector((sgn * 0.95, y + 0.10, 0.62)),
                   rib_at + Vector((sgn * 0.85, y + 0.12, -0.05))]
            faces += tube(g, pts, [0.09, 0.085, 0.075, 0.07], sides=4)
    # Horned skull in front of the ribcage.
    sk = rib_at + Vector((-0.2, 1.65, 0.34))
    skull_rot = rot_euler(0, 0, -20)
    faces += ellipsoid(g, sk, Vector((0.78, 1.05, 0.62)), u=8, v=6, rot=skull_rot)
    faces += ellipsoid(g, sk + skull_rot @ Vector((0, 0.42, -0.14)), Vector((0.52, 0.55, 0.32)), u=6, v=4, rot=skull_rot)
    for sgn in (-1, 1):
        b = sk + skull_rot @ Vector((sgn * 0.28, -0.25, 0.18))
        pts = [b, b + skull_rot @ Vector((sgn * 0.35, -0.25, 0.30)), b + skull_rot @ Vector((sgn * 0.45, -0.05, 0.75)),
               b + skull_rot @ Vector((sgn * 0.30, 0.15, 1.0))]
        faces += tube(g, pts, [0.13, 0.10, 0.06, 0.0], sides=5)
    # Gnawed bones scattered at the mouth.
    loose = [(-1.7, -3.6, 25), (0.3, -4.4, -40), (2.7, -3.4, 70), (-2.4, -4.4, 110)]
    for j, (ux, uz, yaw) in enumerate(loose):
        c = U(ux, 0.08, uz)
        d = rot_euler(0, 0, yaw) @ Vector((0.42, 0, 0))
        faces += tube(g, [c - d, c + d], [0.08, 0.08], sides=4, cap_end=True)
        for e in (c - d, c + d):
            faces += ellipsoid(g, e, Vector((0.2, 0.2, 0.16)), u=5, v=3)

    def col(co, nrm, vt):
        b, d = (BONE_BURNT, ASH_MID) if ruined else (BONE, BONE_DARK)
        c = mix(d, b, 0.5 + 0.5 * max(0.0, nrm.z * 0.8 + 0.3))
        # Eye sockets.
        for sgn in (-1, 1):
            eye = sk + skull_rot @ Vector((sgn * 0.24, 0.38, 0.12))
            if (co - eye).length < 0.2:
                c = mix(c, (0.08, 0.05, 0.04), 0.9)
        return c

    g.paint(faces, col, "bone")


STONES_U = []  # filled by layout()
SLABS_U = []
RUBBLE_U = []


def layout(seed: int):
    rng = random.Random(seed)
    R = lambda lo, hi: lo + rng.random() * (hi - lo)
    STONES_U.clear(); SLABS_U.clear(); RUBBLE_U.clear()
    for i in range(9):
        a = math.radians(32 + (172 - 32) * i / 8)
        dist = R(3.5, 4.5)
        s = R(1.3, 2.3)
        STONES_U.append((math.cos(a) * dist, math.sin(a) * dist, s, R(0.9, 1.35), R(0.8, 1.2), R(-14, 14), R(0, 90), R(-14, 14)))
    for i in range(4):
        a = math.radians(40 + i * 36)
        tall = R(2.8, 4.2)
        SLABS_U.append((math.cos(a) * 5.0, math.sin(a) * 5.0, tall, R(0.8, 1.15), R(0.42, 0.6), R(-8, 8), -math.degrees(a) + 90, R(-9, 9)))
    tries = 0
    while len(RUBBLE_U) < 11 and tries < 200:
        tries += 1
        a = math.radians(R(0, 360))
        dist = R(4.7, 6.0)
        x, z = math.cos(a) * dist, math.sin(a) * dist
        if bury_check(U(x, 0, z)) or (abs(x) < 1.6 and z < 0):
            continue
        RUBBLE_U.append((x, z, R(0.35, 0.85), R(-20, 20), R(0, 90), R(-20, 20)))


def build_stones(g: Group, ruined: bool, seed: float):
    faces = []
    for i, (x, z, s, sx, sz, rx, ry, rz) in enumerate(STONES_U):
        c = U(x, s * 0.32, z)
        faces += rock(g, c, Vector((s * sx, s * sz, s)), rot_euler(rx, rz, -ry), seed + i, bevel=0.16)
    for i, (x, z, tall, w, t, rx, ry, rz) in enumerate(SLABS_U):
        if ruined and i == 1:
            # Toppled slab lying in the dirt.
            c = U(x * 0.82, w * 0.3, z * 0.82)
            faces += rock(g, c, Vector((tall, t, w)), rot_euler(0, 0, -ry + 90) @ rot_euler(0, 90, 0), seed + 30, bevel=0.10)
            continue
        c = U(x, tall * 0.44, z)
        m = rot_euler(rx, rz, -ry)
        faces += rock(g, c, Vector((w, t, tall)), m, seed + 20 + i, bevel=0.10, jitter=0.10)
    for i, (x, z, s, rx, ry, rz) in enumerate(RUBBLE_U):
        c = U(x, s * 0.25, z)
        faces += rock(g, c, Vector((s, s * 1.1, s * 0.7)), rot_euler(rx, rz, ry), seed + 50 + i, bevel=0.0, jitter=0.2)
    if ruined:
        # Chunks of the burst hive among the rubble.
        for i, (x, z) in enumerate([(-1.9, -2.6), (2.2, -2.0), (1.5, 3.9)]):
            pass

    def col(co, nrm, vt):
        up = max(0.0, nrm.z)
        v = min(1.0, 0.80 + 0.20 * up + 0.05 * nz(co, 3.0, seed))
        if ruined:
            v *= 0.92
        return (v, v * 0.99, v * 0.97)

    g.paint(faces, col, "stone", smooth=False)


def build_soil(g: Group, ruined: bool, seed: float):
    bm = g.bm
    segs = 36
    fracs = [0.0, 0.28, 0.55, 0.80, 0.93, 1.0]
    rows = []
    rot = math.radians(12)
    for fi, f in enumerate(fracs):
        if f == 0.0:
            rows.append([bm.verts.new(Vector((0, 0, 0.13)))])
            continue
        row = []
        for s in range(segs):
            th = 2 * math.pi * s / segs
            ux, uz = math.cos(th) * 6.15, math.sin(th) * 5.45
            edge = 1.0 + 0.07 * nz(Vector((math.cos(th) * 2, math.sin(th) * 2, 0)), 1.0, seed) if f > 0.9 else 1.0
            x = (ux * math.cos(rot) - uz * math.sin(rot)) * f * edge
            z = (ux * math.sin(rot) + uz * math.cos(rot)) * f * edge
            hgt = 0.13 * (1 - f * f) + (0.025 if f < 0.95 else -0.06)
            row.append(bm.verts.new(U(x, hgt, z)))
        rows.append(row)
    faces = []
    for a, b in zip(rows, rows[1:]):
        if len(a) == 1:
            for s in range(segs):
                faces.append(bm.faces.new((a[0], b[s], b[(s + 1) % segs])))
        else:
            for s in range(segs):
                faces.append(bm.faces.new((a[s], b[s], b[(s + 1) % segs], a[(s + 1) % segs])))
    mouth = U(0, 0, -2.4)

    def col(co, nrm, vt):
        v = min(1.0, 0.97 + 0.04 * nz(co, 1.4, seed))
        # Trampled lane in front of the mouth.
        dm = Vector((co.x - mouth.x, (co.y - mouth.y) * 0.75)).length
        v *= 0.84 + 0.16 * smooth01(dm / 3.2)
        if ruined:
            burn = smooth01(1.0 - Vector((co.x, co.y)).length / 4.8) * 0.55 + 0.15 * max(0.0, nz(co, 0.8, seed + 3))
            v *= 1.0 - burn
        return (v, v, v)

    g.paint(faces, col, "soil")


# --------------------------------------------------------------------------- bake + materials

BAKE_TARGET: dict[str, bpy.types.Image] = {}
CURRENT_IMAGE: list = [None]


def bake_material(key: str) -> bpy.types.Material:
    """Emission shader = vertex colour x (procedural mottling) x AO, for baking into the atlas."""
    name = f"BAKE_{key}"
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    vc = nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Col"
    glow = key == "glow"
    if glow:
        links.new(vc.outputs["Color"], emit.inputs["Color"])
    else:
        ao = nodes.new("ShaderNodeAmbientOcclusion")
        ao.samples = 16
        ao.inputs["Distance"].default_value = 1.6 if key in ("soil",) else 0.9
        aomix = nodes.new("ShaderNodeMapRange")
        aomix.inputs["To Min"].default_value = {"soil": 0.42, "stone": 0.5}.get(key, 0.30)
        links.new(ao.outputs["AO"], aomix.inputs["Value"])
        tex = nodes.new("ShaderNodeTexNoise")
        tex.inputs["Scale"].default_value = {"chitin": 3.5, "ash": 4.0, "bone": 6.0, "stone": 2.2, "soil": 1.6}.get(key, 3.0)
        tex.inputs["Detail"].default_value = 6.0
        mottle = nodes.new("ShaderNodeMapRange")
        amp = {"chitin": 0.22, "ash": 0.30, "bone": 0.12, "stone": 0.22, "soil": 0.20}.get(key, 0.15)
        mottle.inputs["To Min"].default_value = 1.0 - amp
        mottle.inputs["To Max"].default_value = 1.0 + amp
        mottle.inputs["From Min"].default_value = 0.3
        mottle.inputs["From Max"].default_value = 0.7
        links.new(tex.outputs["Fac"], mottle.inputs["Value"])
        m1 = nodes.new("ShaderNodeMix")
        m1.data_type = "RGBA"
        m1.blend_type = "MULTIPLY"
        m1.inputs["Factor"].default_value = 1.0
        links.new(vc.outputs["Color"], m1.inputs[6])
        links.new(aomix.outputs["Result"], m1.inputs[7])
        m2 = nodes.new("ShaderNodeMix")
        m2.data_type = "RGBA"
        m2.blend_type = "MULTIPLY"
        m2.inputs["Factor"].default_value = 1.0
        links.new(m1.outputs[2], m2.inputs[6])
        links.new(mottle.outputs["Result"], m2.inputs[7])
        if key in ("chitin", "ash", "stone"):
            # Cell cracks: chitin plates / rock fractures.
            vor = nodes.new("ShaderNodeTexVoronoi")
            vor.feature = "DISTANCE_TO_EDGE"
            vor.inputs["Scale"].default_value = 2.6 if key != "stone" else 1.8
            crack = nodes.new("ShaderNodeMapRange")
            crack.inputs["From Max"].default_value = 0.06
            crack.inputs["To Min"].default_value = {"stone": 0.80, "ash": 0.70}.get(key, 0.55)
            links.new(vor.outputs["Distance"], crack.inputs["Value"])
            m3 = nodes.new("ShaderNodeMix")
            m3.data_type = "RGBA"
            m3.blend_type = "MULTIPLY"
            m3.inputs["Factor"].default_value = 1.0
            links.new(m2.outputs[2], m3.inputs[6])
            links.new(crack.outputs["Result"], m3.inputs[7])
            links.new(m3.outputs[2], emit.inputs["Color"])
        else:
            links.new(m2.outputs[2], emit.inputs["Color"])
    links.new(emit.outputs["Emission"], out.inputs["Surface"])
    img_node = nodes.new("ShaderNodeTexImage")
    img_node.name = "BAKE_TARGET"
    nodes.active = img_node
    return mat


def set_bake_image(objs, img):
    for o in objs:
        for slot in o.material_slots:
            n = slot.material.node_tree.nodes.get("BAKE_TARGET")
            n.image = img
            slot.material.node_tree.nodes.active = n


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def unwrap(objs):
    select_only(objs)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.006, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake_state(objs, others, size, out_png: Path) -> bpy.types.Image:
    for o in others:
        o.hide_render = True
    for o in objs:
        o.hide_render = False
    img = bpy.data.images.new(out_png.stem, size, size, alpha=False)
    img.colorspace_settings.name = "sRGB"
    unwrap(objs)
    set_bake_image(objs, img)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.render.bake.margin = 12
    scene.render.bake.use_clear = True
    select_only(objs)
    bpy.ops.object.bake(type="EMIT", margin=12, use_clear=True)
    img.filepath_raw = str(out_png)
    img.file_format = "PNG"
    img.save()
    print(f"[SM] Baked {out_png.name}")
    for o in others:
        o.hide_render = False
    return img


def final_material(name: str, img: bpy.types.Image, glow: bool = False, tint=None) -> bpy.types.Material:
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = img
    col = tex.outputs["Color"]
    if tint is not None:
        m = nodes.new("ShaderNodeMix")
        m.data_type = "RGBA"
        m.blend_type = "MULTIPLY"
        m.inputs["Factor"].default_value = 1.0
        links.new(col, m.inputs[6])
        m.inputs[7].default_value = (*lin(tint), 1.0)
        col = m.outputs[2]
    links.new(col, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.72 if not glow else 0.5
    bsdf.inputs["Metallic"].default_value = 0.0
    if glow:
        links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 4.0
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def assign(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.material_index = 0


# --------------------------------------------------------------------------- build

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.55, 0.66, 0.82, 1.0)
    bg.inputs[1].default_value = 0.9


def build_state(ruined: bool, col: bpy.types.Collection, prefix: str):
    seed = 7.0 if not ruined else 11.0
    hive = Group(f"{prefix}_Hive")
    bones = Group(f"{prefix}_Bones")
    glow = None if ruined else Group(f"{prefix}_Glow")
    soil = Group(f"{prefix}_Soil")
    stone = Group(f"{prefix}_Stone")

    build_hive(hive, ruined, seed)
    build_maw_frame(hive, bones, ruined, seed)
    build_ridges(hive, glow, ruined, seed)
    build_spires(hive, glow, ruined, seed)
    if not ruined:
        build_maw_glow(glow)
        build_pods(glow, False, seed)
        build_slime(glow, seed, False)
    else:
        build_pods(hive, True, seed)
        build_slime(hive, seed, True)
        build_fallen(hive, seed)
    build_bones(bones, ruined, seed)
    build_stones(stone, ruined, 3.0)
    build_soil(soil, ruined, 5.0)

    groups = [g for g in (hive, bones, glow, soil, stone) if g is not None]
    counts = {g.name: g.tris() for g in groups}
    objs = [g.to_object(col) for g in groups]
    return objs, counts


def build_fallen(g: Group, seed: float):
    """Ruined only: the hive's crown lying in the dirt, and a snapped spire tip."""
    faces = []
    base = U(-3.4, 0.62, 0.9)
    axis = U(-1.0, 0.0, 0.25).normalized()
    pts, radii = [], []
    seg = [(0.0, 0.95), (0.35, 0.86), (0.75, 0.62), (0.95, 0.48), (1.3, 0.5), (1.9, 0.36), (2.5, 0.22), (3.1, 0.1), (3.5, 0.0)]
    for d, r in seg:
        p = base + axis * d
        p.z = max(r * 0.75, 0.2) if r > 0 else 0.25
        pts.append(p)
        radii.append(r)
    faces += tube(g, pts, radii, sides=9)
    tip_base = U(3.4, 0.25, -0.6)
    ax2 = U(0.8, 0.0, -0.6).normalized()
    faces += tube(g, [tip_base, tip_base + ax2 * 0.9, tip_base + ax2 * 1.8 + Vector((0, 0, 0.1))], [0.38, 0.26, 0.0], sides=7)

    def col(co, nrm, vt):
        return mix(ASH_DEEP, ASH_HI, 0.25 + 0.45 * max(0.0, nrm.z) + 0.15 * nz(co, 2.0, seed))

    g.paint(faces, col, "ash")


def export_fbx(objs, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    select_only(objs)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        apply_unit_scale=True,
        bake_space_transform=True,
        object_types={"MESH"},
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="STRIP",
        embed_textures=False,
        use_custom_props=False,
        bake_anim=False,
    )
    print(f"[SM] Exported {path}")


# --------------------------------------------------------------------------- preview renders

def setup_preview(img_active, img_ruined):
    scene = bpy.context.scene
    col = bpy.data.collections.get("Preview") or bpy.data.collections.new("Preview")
    if col.name not in scene.collection.children:
        scene.collection.children.link(col)
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, -0.02))
    ground = bpy.context.active_object
    ground.name = "PreviewGround"
    for c in list(ground.users_collection):
        c.objects.unlink(ground)
    col.objects.link(ground)
    gm = bpy.data.materials.new("PreviewGrass")
    gm.use_nodes = True
    nodes, links = gm.node_tree.nodes, gm.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.9
    if GRASS_TEX.is_file():
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(str(GRASS_TEX))
        mapping = nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (6, 6, 6)
        coord = nodes.new("ShaderNodeTexCoord")
        links.new(coord.outputs["UV"], mapping.inputs["Vector"])
        links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (0.12, 0.25, 0.06, 1)
    ground.data.materials.append(gm)

    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.2
    sun_data.angle = math.radians(3)
    sun_data.color = (1.0, 0.95, 0.88)
    sun = bpy.data.objects.new("Sun", sun_data)
    col.objects.link(sun)
    sun.rotation_euler = (math.radians(48), 0, math.radians(-38))

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cam_data)
    col.objects.link(cam)
    scene.camera = cam
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in type(scene.view_settings).bl_rna.properties["view_transform"].enum_items] else "Filmic"
    scene.view_settings.look = "None"
    return cam


def aim(cam, yaw_deg: float, pitch_deg: float, ortho: float, focus=Vector((0, 0, 2.4))):
    """yaw 0 = camera in front of the mouth (Blender +Y side) looking toward -Y."""
    yaw = math.radians(yaw_deg)
    pitch = math.radians(pitch_deg)
    d = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    cam.location = focus + d * 60
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = ortho
    cam.data.clip_end = 200


def render_previews(active_objs, ruined_objs, cam):
    RENDER_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    shots = []
    for state, show, hide in (("active", active_objs, ruined_objs), ("ruined", ruined_objs, active_objs)):
        for o in hide:
            o.hide_render = True
        for o in show:
            o.hide_render = False
        for yaw in (0, 90, 180, 270):
            aim(cam, yaw, 30, 17)
            p = RENDER_DIR / f"stalker_den_{state}_yaw{yaw:03d}.png"
            scene.render.filepath = str(p)
            bpy.ops.render.render(write_still=True)
            shots.append(p)
            print(f"[SM] Rendered {p.name}")
    for o in active_objs + ruined_objs:
        o.hide_render = False
    return shots


# --------------------------------------------------------------------------- main

def main():
    reset_scene()
    layout(20251007)
    col_a = bpy.data.collections.new("Den_Active")
    col_r = bpy.data.collections.new("Den_Ruined")
    bpy.context.scene.collection.children.link(col_a)
    bpy.context.scene.collection.children.link(col_r)

    active, counts_a = build_state(False, col_a, "Active")
    ruined, counts_r = build_state(True, col_r, "Ruined")

    UNITY_DENS.mkdir(parents=True, exist_ok=True)
    img_a = bake_state(active, ruined, ATLAS_ACTIVE, UNITY_DENS / "T_StalkerDen_Active.png")
    img_r = bake_state(ruined, active, ATLAS_RUINED, UNITY_DENS / "T_StalkerDen_Ruined.png")

    m_body = final_material("SM_StalkerDen_Active", img_a)
    m_glow = final_material("SM_StalkerDen_Glow", img_a, glow=True)
    m_ruin = final_material("SM_StalkerDen_Ruined", img_r)
    for o in active:
        assign(o, m_glow if o.name.endswith("_Glow") else m_body)
    for o in ruined:
        assign(o, m_ruin)
    # Strip colour attributes from export meshes (Unity's URP Lit ignores them anyway).
    for o in active + ruined:
        for a in list(o.data.color_attributes):
            o.data.color_attributes.remove(a)
        o.data.uv_layers[0].name = "UVMap"

    export_fbx(active + ruined, EXPORT_DIR / f"{FBX_NAME}.fbx")
    shutil.copy2(EXPORT_DIR / f"{FBX_NAME}.fbx", UNITY_DENS / f"{FBX_NAME}.fbx")

    print("[SM] Triangles:")
    for k, v in {**counts_a, **counts_r}.items():
        print(f"[SM]   {k}: {v}")
    print(f"[SM]   Den_Active total: {sum(counts_a.values())}")
    print(f"[SM]   Den_Ruined total: {sum(counts_r.values())}")

    if not NO_RENDER:
        # Preview with Earth's soil/stone tint, as StalkerLair applies it in game.
        tinted = {}
        for o in active + ruined:
            if o.name.endswith("_Soil") or o.name.endswith("_Stone"):
                tint = EARTH_SOIL if o.name.endswith("_Soil") else EARTH_STONE
                img = img_a if o in active else img_r
                key = (o.name.split("_")[-1], o in active)
                if key not in tinted:
                    tinted[key] = final_material(f"PREVIEW_{key[0]}_{'A' if key[1] else 'R'}", img, tint=tint)
                o.data.materials[0] = tinted[key]
        cam = setup_preview(img_a, img_r)
        render_previews(active, ruined, cam)
        for o in active + ruined:
            if o.name.endswith("_Soil") or o.name.endswith("_Stone"):
                o.data.materials[0] = m_ruin if o in ruined else m_body

    img_a.pack()
    img_r.pack()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT), compress=True)
    print(f"[SM] Saved {BLEND_OUT}")


if __name__ == "__main__":
    main()
