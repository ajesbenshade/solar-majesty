# Solar Majesty - SM_Citadel_Arcology (Pathfinder Accord, civ 4): production blockout of concept D1 "Stilt Arcology".
# Octagonal stepped citadel deck on four walker legs, open undercroft; the shared Core module (SM_Core_Module.fbx)
# hangs at Core_Socket and is NOT part of this mesh.
#
# Run (headless, deterministic, rerunnable):
#   blender -b -P Blender/scripts/sm_citadel_arcology.py                 # build + export FBX
#   blender -b -P Blender/scripts/sm_citadel_arcology.py -- --render     # + review renders (SM_ARCOLOGY_RENDER_DIR)
#
# Conventions (match sm_hero_building_kits.py): Z-up build, front = -Y, pivot = ground centre, FBX axis_forward=-Z,
# axis_up=Y, FBX_SCALE_ALL, apply_unit_scale, mesh_smooth_type=FACE, no leaf bones. Metres.
# Scale: built at concept proportions (12.5 m footprint) and multiplied by ONE parameter CASTLE_SCALE (default
# 18 / 12.5 = 1.44) baked into the vertices, so the FBX imports at 1:1 with a ~18 m footprint.
# Material slots: Trim (hull / pewter / window bands of the shared trim sheet), Carbon, TeamColour (neutral; the code
# tints it with the CIV colour, emerald for Pathfinder), Accent (neutral; the code tints it with the PLAYER's chosen accent;
# on a narrow inner ring on the tier-1 roof, thin leg collars and a stripe across each hanging banner, ~2-5% of visible surface). No CoreEmission slot here:
# the castle carries no cyan, the Core module brings its own.
# UVs: on the shared 2048 trim sheet made by sm_core_citadels.py (same BANDS, same 200 px/m planar projection fitted and
# centred in the band). Role -> band: hull -> PANEL_DK, pale pewter structure -> PANEL, window strips -> GLASS (Trim slot;
# Trim's emission map lights only that strip),
# carbon -> CARB, team -> TEAM. The civ hull/pewter colours are a material tint in engine; the review renders use them.
# Core_Socket follows the shared Core module's convention (socket = collar base centre, crystal 1.35 concept-m above it),
# placed so the crystal sits where concept D1 slung its Core (2.35 m concept). The Core mesh is NOT in this FBX.
import bpy, bmesh, math, os, sys, json
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

CASTLE_SCALE = float(os.environ.get("SM_CASTLE_SCALE", 18.0 / 12.5))
NAME = "SM_Citadel_Arcology"
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
EXPORT_DIR = os.path.join(REPO, "Assets", "Art", "Citadels")
RENDER_DIR = os.environ.get("SM_ARCOLOGY_RENDER_DIR", "/workspace/art/citadel-arcology")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
LOD_TARGETS = (12000, 5000, 1500)          # tri budgets (LOD0 hard cap, LOD1/LOD2 targets)
TEX_DIR = os.path.join(EXPORT_DIR, "Textures")

# ---- shared trim sheet layout (copied from sm_core_citadels.py BANDS; v = 0 at the image bottom)
SHEET_BANDS = {"EMIT": (0.000, 0.040), "TEAM": (0.040, 0.100), "CARB": (0.100, 0.220), "EDGE": (0.220, 0.260),
               "VENT": (0.260, 0.340), "RIB": (0.340, 812 / 2048), "GLASS": (812 / 2048, 860 / 2048),
               "PANEL_DK": (0.420, 0.600), "PANEL": (0.600, 1.000)}   # GLASS added 2026-11-11 (rows 812-859)
TEXEL_PER_M = 200.0 / 2048
PAD = 3.0 / 2048
HULL, PEW, GLS, TEAM, CARB, ACC = range(6)
BANDS = {HULL: SHEET_BANDS["PANEL_DK"], PEW: SHEET_BANDS["PANEL"], GLS: SHEET_BANDS["GLASS"],
         CARB: SHEET_BANDS["CARB"], TEAM: SHEET_BANDS["TEAM"], ACC: SHEET_BANDS["TEAM"]}   # Accent reuses the neutral tint band
SLOT_OF = {HULL: 0, PEW: 0, GLS: 0, CARB: 1, TEAM: 2, ACC: 3}
CORE_CRYSTAL_Z = 1.35          # shared module: crystal centre above Core_Socket (concept units)
SLOTS = ("Trim", "Carbon", "TeamColour", "Accent")

# review palette (sRGB)
C_HULL = (0x14 / 255, 0x22 / 255, 0x26 / 255)      # teal-black #142226
C_TEAM = (0x0D / 255, 0x99 / 255, 0x54 / 255)      # emerald #0D9954
C_PEW = (0.58, 0.61, 0.61)                          # pale pewter
C_GLS = (0.40, 0.45, 0.45)                          # frost glass (window strip)
C_CARB = (0x1E / 255, 0x20 / 255, 0x24 / 255)      # carbon #1E2024
C_CYAN = (0.22, 0.84, 0.98)                         # Core stand-in ONLY
CORE_EMIT = 3.0                                     # review Core emission strength
GLASS_EMIT = CORE_EMIT / 12.0                       # windows: dim emerald glow, 12x below the Core (matches other castles)
C_ACC = (0.85, 0.12, 0.62)                          # review-only sample player accent (magenta); neutral in the FBX


def rz(deg):
    return Matrix.Rotation(math.radians(deg), 4, 'Z')


def lin(c):
    return tuple(((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c)


def cs(a):
    return math.cos(math.radians(a)), math.sin(math.radians(a))


class Builder:
    def __init__(self):
        self.v, self.f, self.m, self.s = [], [], [], []

    def add(self, verts, faces, role, smooth=False, M=None):
        o = len(self.v)
        for p in verts:
            p = Vector(p)
            if M is not None:
                p = M @ p
            self.v.append(tuple(p))
        for f in faces:
            self.f.append([o + i for i in f])
            self.m.append(role)
            self.s.append(smooth)

    def box(self, c, size, role, M=None, top=None, shift=(0, 0)):
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        tx, ty = (hx, hy) if top is None else (top[0] / 2, top[1] / 2)
        cx, cy, cz = c
        dx, dy = shift
        v = [(cx - hx, cy - hy, cz - hz), (cx + hx, cy - hy, cz - hz), (cx + hx, cy + hy, cz - hz), (cx - hx, cy + hy, cz - hz),
             (cx - tx + dx, cy - ty + dy, cz + hz), (cx + tx + dx, cy - ty + dy, cz + hz),
             (cx + tx + dx, cy + ty + dy, cz + hz), (cx - tx + dx, cy + ty + dy, cz + hz)]
        f = [(0, 3, 2, 1), (4, 5, 6, 7)] + [(i, (i + 1) % 4, (i + 1) % 4 + 4, i + 4) for i in range(4)]
        self.add(v, f, role, False, M)

    def hull(self, pts, role, M=None):
        bm = bmesh.new()
        vs = [bm.verts.new(p) for p in pts]
        bmesh.ops.convex_hull(bm, input=vs)
        bmesh.ops.dissolve_limit(bm, angle_limit=0.01, verts=bm.verts[:], edges=bm.edges[:])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.verts.index_update()
        self.add([v.co[:] for v in bm.verts], [[v.index for v in f.verts] for f in bm.faces], role, False, M)
        bm.free()

    def cbox(self, c, size, role, ch=0.08, M=None, plain=False):
        if plain:
            return self.box(c, size, role, M)
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        pts = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    pts += [(c[0] + sx * (hx - ch), c[1] + sy * hy, c[2] + sz * (hz - ch)),
                            (c[0] + sx * hx, c[1] + sy * (hy - ch), c[2] + sz * (hz - ch)),
                            (c[0] + sx * (hx - ch), c[1] + sy * (hy - ch), c[2] + sz * hz)]
        self.hull(pts, role, M)

    def beam(self, a, b, w, d, role, M=None):
        a, b = Vector(a), Vector(b)
        if M is not None:
            a, b = M @ a, M @ b
        z = (b - a)
        L = z.length
        z.normalize()
        x = Vector((0, 0, 1)).cross(z)
        if x.length < 1e-4:
            x = Vector((1, 0, 0))
        x.normalize()
        y = z.cross(x)
        R = Matrix((x, y, z)).transposed().to_4x4()
        R.translation = (a + b) / 2
        self.box((0, 0, 0), (w, d, L), role, R)

    def cyl(self, c, r0, r1, h, n, role, M=None, smooth=True, a0=0.0, caps=True, bottom=True):
        cx, cy, cz = c
        ang = [math.radians(a0) + 2 * math.pi * i / n for i in range(n)]
        bot = [(cx + r0 * math.cos(a), cy + r0 * math.sin(a), cz) for a in ang]
        if r1 > 1e-5:
            top = [(cx + r1 * math.cos(a), cy + r1 * math.sin(a), cz + h) for a in ang]
            self.add(bot + top, [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)], role, smooth, M)
            if caps:
                self.add(top, [list(range(n))], role, False, M)
        else:
            self.add(bot + [(cx, cy, cz + h)], [(i, (i + 1) % n, n) for i in range(n)], role, smooth, M)
        if caps and bottom:
            self.add(bot, [list(range(n))[::-1]], role, False, M)

    def ring(self, ri, ro, z0, z1, role, a0=0.0, n=32, M=None, smooth=True):
        ang = [math.radians(a0 + 360.0 * i / n) for i in range(n)]

        def circ(r, z):
            return [(r * math.cos(a), r * math.sin(a), z) for a in ang]

        def strip(A, B, sm):
            self.add(A + B, [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)], role, sm, M)
        strip(circ(ro, z0), circ(ro, z1), smooth)
        strip(circ(ri, z1), circ(ri, z0), smooth)
        strip(circ(ro, z1), circ(ri, z1), False)
        strip(circ(ri, z0), circ(ro, z0), False)

    def dome(self, c, r, hz, n, rings, role, M=None):
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
                a, b = j * n + i, j * n + (i + 1) % n
                f.append((a, b, b + n, a + n))
        o = (rings - 1) * n
        f += [(o + i, o + (i + 1) % n, len(v) - 1) for i in range(n)]
        self.add(v, f, role, True, M)

    def torus(self, c, R, r, n, m, role, M=None):
        v, f = [], []
        for i in range(n):
            a = 2 * math.pi * i / n
            for j in range(m):
                b = 2 * math.pi * j / m
                v.append((c[0] + (R + r * math.cos(b)) * math.cos(a), c[1] + (R + r * math.cos(b)) * math.sin(a), c[2] + r * math.sin(b)))
        for i in range(n):
            for j in range(m):
                a, b = i * m + j, i * m + (j + 1) % m
                a2, b2 = ((i + 1) % n) * m + j, ((i + 1) % n) * m + (j + 1) % m
                f.append((a, a2, b2, b))
        self.add(v, f, role, True, M)


# ------------------------------------------------------------------ geometry (concept units, 12.5 m footprint)
A8 = 22.5
CORE_SOCKET = (0.0, 0.0, 2.35 - 1.35)   # collar base; crystal centre lands at concept 2.35 (see CORE_CRYSTAL_Z)
KNEE = (6.3, 0.0, 7.1)


def build(L):
    """L = LOD level 0/1/2. Silhouette parts are kept at every level; detail drops out and segment counts fall."""
    b = Builder()
    N = lambda n, lo=6: n if L == 0 else max(lo, int(round(n * (0.85 if L == 1 else 0.4))))
    d0, d1 = L == 0, L <= 1          # detail flags
    # ---- four walker legs on the diagonals
    for k in range(4):
        M = rz(45 + 90 * k)
        foot, knee, hip = (7.0, 0, 0.5), KNEE, (3.3, 0, 6.6)
        b.cyl((7.0, 0, 0), 1.05, 0.8, 0.3, N(10, 8), PEW, M, bottom=False)          # foot pad
        b.cyl((7.0, 0, 0.3), 0.55, 0.45, 0.35, N(10), HULL, M, caps=d1)
        if d1:
            for s in (-1, 1):
                b.box((7.0, s * 0.75, 0.16), (0.9, 0.5, 0.32), PEW, M, top=(0.5, 0.3))
            b.box((7.65, 0, 0.16), (0.6, 0.6, 0.32), PEW, M, top=(0.3, 0.4))
        b.beam(foot, knee, 0.62, 0.5, HULL, M)                                       # shin
        b.beam((6.93, 0, 1.2), (6.8, 0, 2.4), 0.7, 0.58, TEAM, M)                     # team band
        b.beam((6.72, 0, 3.1), (6.48, 0, 5.4), 0.74, 0.62, PEW, M)                    # shin armour
        b.beam((6.777, 0, 2.6), (6.74, 0, 2.95), 0.7, 0.58, ACC, M)                  # player Accent collar on the shin
        b.beam((4.9, 0, 6.867), (4.6, 0, 6.817), 0.88, 0.7, ACC, M)                  # player Accent collar on the thigh
        if d1:
            for s in (-1, 1):
                b.beam((6.96, s * 0.42, 0.9), (6.38, s * 0.42, 6.4), 0.1, 0.1, PEW, M)  # hydraulic lines
        Mk = M @ Matrix.Translation(knee) @ Matrix.Rotation(math.pi / 2, 4, 'X')
        b.cyl((0, 0, -0.55), 0.62, 0.62, 1.1, N(14, 8), PEW, Mk)                      # knee drum
        b.cyl((0, 0, -0.62), 0.3, 0.3, 1.24, N(10), TEAM, Mk)                          # knee hub (team)
        b.beam(knee, hip, 0.8, 0.62, HULL, M)                                        # thigh
        if d1:
            b.beam((5.6, 0, 6.2), (3.9, 0, 5.3), 0.22, 0.22, PEW, M)                  # thigh piston
            b.beam((3.9, 0, 5.3), (2.6, 0, 5.9), 0.3, 0.3, PEW, M)
            b.cyl((6.3, 0, 7.4), 0.2, 0.12, 0.9, 6, PEW, M)                          # knee beacon post (unlit)
        b.cbox((3.3, 0, 6.6), (1.3, 1.5, 1.2), PEW, 0.12, M, plain=not d1)           # hip block
        if d1:
            b.torus((7.0, 0, 0.75), 0.5, 0.12, N(12, 8), 6 if d0 else 4, PEW, M)     # ankle collar
        if d0:
            for s in (-1, 1):
                b.torus((0, 0, s * 0.58), 0.5, 0.1, 12, 6, HULL, Mk)                 # knee bearing rings
            for dz in (22, -22):
                b.cyl((3.6, 0, 5.3), 0.34, 0.26, 0.4, 8, HULL, rz(45 + 90 * k + dz))   # underbelly vents

    # ---- raised deck (octagon, flats to the axes) and stepped tiers
    b.cyl((0, 0, 5.6), 2.6, 4.9, 1.1, 8, PEW, a0=A8, smooth=False)               # underbelly
    b.cyl((0, 0, 6.7), 5.2, 5.2, 0.5, 8, HULL, a0=A8, smooth=False)              # main deck slab
    b.ring(5.12, 5.3, 6.85, 7.05, TEAM, a0=A8, n=8, smooth=False)                # team strake
    b.cyl((0, 0, 7.2), 4.1, 3.6, 1.7, 8, HULL, a0=A8, smooth=False, bottom=False)  # tier 1 (CommonsKeep)
    b.ring(3.5, 3.95, 8.9, 9.1, TEAM, a0=A8, n=8, smooth=False)                # tier-1 crown band = civ colour (dominant outer ring)
    b.ring(3.05, 3.275, 8.9, 9.02, ACC, a0=A8, n=8, smooth=False)              # narrow inner ring on the tier-1 roof = player Accent
    b.cyl((0, 0, 9.1), 2.7, 2.3, 1.3, 8, HULL, a0=A8, smooth=False, bottom=False)  # tier 2
    b.ring(2.2, 2.5, 10.4, 10.55, TEAM, a0=A8, n=8, smooth=False)
    b.dome((0, 0, 10.55), 1.9, 0.8, N(16, 8), 4 if L < 2 else 3, HULL)           # cap (CommonsDome)
    for k in range(8):
        M = rz(45 * k)
        b.box((3.86, 0, 8.05), (0.12, 2.0, 0.7), GLS, M)                           # tier-1 window bands
        if d1:
            b.box((2.5, 0, 9.75), (0.12, 1.2, 0.5), GLS, M)                        # tier-2 window bands
            b.box((3.95, 1.55, 8.05), (0.3, 0.16, 1.7), PEW, M)                    # tier-1 ribs
        if d1:
            b.beam((4.7, 0, 7.2), (3.75, 0, 8.6), 0.16, 0.16, PEW, rz(45 * k + 22.5))  # corner buttress fins
            b.box((4.95, 0, 6.2), (0.5, 1.4, 0.5), PEW, M, top=(0.3, 1.4))          # belly lugs
    if d1:   # deck rail (LOD1: posts at the octagon corners only)
        pts = [Vector((5.05 * cs(A8 + 45 * k)[0], 5.05 * cs(A8 + 45 * k)[1], 7.2)) for k in range(8)]
        dense = [p.lerp(pts[(k + 1) % 8], t / 4) for k, p in enumerate(pts) for t in range(4 if d0 else 1)]
        for i, a in enumerate(dense):
            c = dense[(i + 1) % len(dense)]
            b.beam(a + Vector((0, 0, 0.55)), c + Vector((0, 0, 0.55)), 0.06, 0.06, PEW)
            b.box((a.x, a.y, a.z + 0.275), (0.06, 0.06, 0.55), PEW)

    # ---- mast (reach / comms) + flag
    b.cyl((0, 0, 11.2), 0.22, 0.12, 2.2, N(8), PEW)
    b.cyl((0, 0, 13.4), 0.05, 0.03, 1.1, 6 if L < 2 else 4, PEW)
    b.torus((0, 0, 12.2), 0.75, 0.06, N(20, 8), 6 if L == 0 else 4, PEW)
    if d1:
        b.torus((0, 0, 12.9), 0.48, 0.05, N(16, 8), 4, PEW)
        for k in range(4):
            b.beam((0, 0, 12.2), (0.75 * cs(90 * k)[0], 0.75 * cs(90 * k)[1], 12.2), 0.05, 0.05, PEW)
    if d0:
        for k in range(4):
            b.beam((0.1 * cs(45 + 90 * k)[0], 0.1 * cs(45 + 90 * k)[1], 13.2),
                   (2.15 * cs(45 + 90 * k)[0], 2.15 * cs(45 + 90 * k)[1], 10.5), 0.035, 0.035, PEW)   # stays
    R = Matrix.Translation((0, 0, 14.4)) @ rz(20)
    b.box((0.55, 0, -0.325), (1.0, 0.04, 0.55), TEAM, R)                           # flag
    if d1:
        b.box((0.5, 0, -0.03), (1.1, 0.07, 0.06), PEW, R)
    # dish + sensor pods
    if d1:
        Md = Matrix.Translation((-2.9, 1.2, 9.1)) @ rz(150)
        b.cyl((0, 0, 0), 0.1, 0.1, 0.7, 6, PEW, Md)
        b.dome((0, 0, 0), 0.75, 0.3, N(14, 8), 3, PEW, Md @ Matrix.Translation((0.25, 0, 1.05))
               @ Matrix.Rotation(math.radians(-60), 4, 'Y') @ Matrix.Rotation(math.pi, 4, 'X'))
    if d1:
        for (x, y) in ((2.9, 1.3), (2.6, -2.0), (-2.4, -2.3)):
            b.cbox((x, y, 9.3), (0.7, 0.6, 0.45), PEW, 0.06, plain=not d0)
            b.cyl((x, y, 9.5), 0.07, 0.05, 0.9, 6, PEW)

    # ---- cantilevered landing pads (+X / -X)
    for s in (-1, 1):
        b.cyl((s * 5.25, 0, 6.85), 1.0, 1.0, 0.2, N(12, 8), PEW, smooth=False)
        if d1:
            b.ring(0.6, 0.74, 7.05, 7.07, TEAM, n=N(16, 8), M=Matrix.Translation((s * 5.25, 0, 0)))
        for t in (-1, 1):
            b.beam((s * 5.9, t * 0.6, 6.85), (s * 4.5, t * 0.9, 5.9), 0.14, 0.14, PEW)
        if d0:   # parked courier drone
            b.cbox((s * 5.25, 0, 7.3), (0.9, 0.5, 0.3), HULL, 0.08)
            for t in (-1, 1):
                b.box((s * 5.25, t * 0.5, 7.36), (0.3, 0.5, 0.06), PEW)
                b.cyl((s * 5.25, t * 0.78, 7.36), 0.3, 0.3, 0.05, 10, PEW)

    # ---- hanging team banners (sides + rear; front carries the lift)
    for k in (1, 2, 3):
        Rb = rz(90 * k) @ Matrix.Translation((0, -5.24, 6.65))
        w, h = 1.5, 2.6
        b.box((0, 0, 0.04), (w + 0.24, 0.1, 0.1), PEW, Rb)
        b.box((0, 0, -h * 0.4), (w, 0.05, h * 0.8), TEAM, Rb)
        if d1:
            for sx in (-1, 1):
                b.hull([(sx * w / 2, y, -h * 0.8) for y in (-0.025, 0.025)] + [(0, y, -h * 0.8) for y in (-0.025, 0.025)]
                       + [(sx * w / 2, y, -h) for y in (-0.025, 0.025)], TEAM, Rb)
            b.box((0, 0, -h * 0.25), (w * 0.5, 0.07, 0.12), PEW, Rb)
        b.box((0, 0, -h * 0.62), (w + 0.02, 0.08, 0.24), ACC, Rb)                 # player Accent stripe across the banner

    # ---- Core sling (castle side only; the Core + collar come from SM_Core_Module at Core_Socket)
    b.cyl((0, 0, 3.5), 0.18, 0.18, 1.3, N(8), CARB)                               # tether down to the crystal tip (module top 3.35)
    b.cyl((0, 0, 4.75), 0.55, 1.2, 0.9, N(12, 8), CARB)                            # yoke under the belly
    for k in range(4):
        c, s_ = cs(90 * k)
        b.beam((2.05 * c, 2.05 * s_, 5.7), (1.68 * c, 1.68 * s_, 1.2), 0.2, 0.2, CARB)   # sling arms down to the collar dish rim
        b.box((1.6 * c, 1.6 * s_, 1.25), (0.3, 0.3, 0.3), CARB, rz(0))                       # grip shoe at the rim
        if d1:
            b.beam((1.68 * c, 1.68 * s_, 1.15), (2.3 * c, 2.3 * s_, 0.25), 0.07, 0.07, PEW)  # mooring cables
    b.cyl((0, 0, 0), 2.7, 2.5, 0.22, 8, CARB, a0=A8, smooth=False, bottom=False)  # ground dock pad
    if d1:
        b.ring(1.5, 1.72, 0.22, 0.3, PEW, n=N(24, 12))
    b.cyl((0, 0, 0.22), 0.5, 0.3, 0.5, N(10), CARB, bottom=False)                  # dock plinth (uplink lives in the Core module)
    if d0:
        for k in range(8):
            b.box((2.2, 0, 0.3), (0.5, 0.3, 0.25), PEW, rz(45 * k + 22.5))

    # ---- front lift
    for s in (-1, 1):
        b.beam((s * 0.7, -5.0, 6.9), (s * 0.7, -5.0, 0.0), 0.14, 0.14, PEW)
    if d1:
        for z in (1.3, 2.6, 3.9, 5.2):
            b.box((0, -5.0, z), (1.5, 0.08, 0.08), PEW)
    b.cbox((0, -5.0, 1.15), (1.2, 0.9, 1.9), HULL, 0.1, plain=not d1)            # lift car
    b.box((0, -5.47, 1.2), (0.7, 0.04, 1.3), GLS)
    b.box((0, -5.0, 2.16), (1.3, 1.0, 0.12), TEAM)
    # ground station: one chamfered pad (its sloped front lip replaces the concept's separate ramp, which poked 0.36 m
    # past the 18 m plot edge); front edge at y = -6.24 concept = -8.99 m
    b.box((0, -5.1, 0.06), (2.4, 2.28, 0.12), PEW, top=(2.2, 1.9), shift=(0, 0.1))
    b.box((0, -4.9, 6.95), (2.0, 1.0, 0.5), PEW)                                   # lift head
    # ---- rear service ladder + undercroft cargo
    a, c, u = Vector((0, 4.9, 0.0)), Vector((0, 4.9, 6.7)), Vector((0.5, 0, 0))
    b.beam(a, c, 0.08, 0.08, PEW)
    b.beam(a + u, c + u, 0.08, 0.08, PEW)
    if d1:
        for i in range(8 if d0 else 4):
            n_ = 8 if d0 else 4
            p, q = a.lerp(c, i / n_), a.lerp(c, (i + 1) / n_)
            b.beam(p, q + u, 0.056, 0.056, PEW) if i % 2 == 0 else b.beam(p + u, q, 0.056, 0.056, PEW)
    crates = ((-3.6, 2.6, 0.9), (-3.9, 1.4, 0.7), (3.7, 2.4, 0.8), (3.2, 3.4, 1.0), (-2.9, 3.6, 0.8))
    for (x, y, w) in (crates if d1 else ()):
        b.cbox((x, y, w / 2), (w * 1.3, w, w), HULL, 0.05, plain=not d0)
        b.box((x, y, w * 0.5), (w * 1.32, w * 0.3, w * 1.02), TEAM)
    return b


# ------------------------------------------------------------------ mesh processing
def fib_dirs(n):
    out, g = [], math.pi * (3 - math.sqrt(5))
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(max(0.0, 1 - z * z))
        out.append(Vector((r * math.cos(g * i), r * math.sin(g * i), z)))
    return out


def cull_hidden(bm, ndirs=96):
    """Delete faces no ray can see: ground-contact undersides and faces buried inside other parts.
    A face is visible if a ray from (centre or a near-corner point) in front of it escapes the mesh in a direction that is not
    steeply into the ground (dir.z > -0.12)."""
    bm.faces.ensure_lookup_table()
    tree = BVHTree.FromBMesh(bm)
    dirs = [d for d in fib_dirs(ndirs) if d.z > -0.12]
    kill = []
    for f in bm.faces:
        n = f.normal
        c = f.calc_center_median()
        if c.z < 0.02 and n.z < -0.5:
            kill.append(f)
            continue
        if f.calc_area() < 1e-8:
            kill.append(f)
            continue
        samples = [c] + [c.lerp(v.co, 0.85) for v in f.verts]
        # area-weighted interior samples (deterministic) so a face partly under a small part is not judged by one point
        cos = [v.co for v in f.verts]
        tris_ = [(cos[0], cos[i], cos[i + 1]) for i in range(1, len(cos) - 1)]
        ns = min(64, max(0, int(f.calc_area() / 0.15)))
        for j in range(ns):
            ta, tb, tc = tris_[j % len(tris_)]
            u_, v_ = (j * 0.618034) % 1.0, (j * 0.754878 + 0.31) % 1.0
            if u_ + v_ > 1:
                u_, v_ = 1 - u_, 1 - v_
            samples.append(ta + (tb - ta) * u_ + (tc - ta) * v_)
        vis = False
        fdirs = [d for d in dirs if d.dot(n) >= 0.05]
        for p in samples:
            for d in fdirs:
                hit = tree.ray_cast(p + n * 2e-3 + d * 2e-3, d, 200.0)
                if hit[0] is None:
                    vis = True
                    break
            if vis:
                break
        if not vis:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    return len(kill)


def trim_uvs(me, roles):
    """Same projection as sm_core_citadels.py: horizontal faces map plan XY, others map (horizontal tangent, Z);
    uniform 200 px/m, shrunk only if the face is taller than its band, centred in the band with a 3 px pad."""
    uv = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        n = poly.normal
        if abs(n.z) > 0.7:
            ua, va = Vector((1, 0, 0)), Vector((0, 1, 0))
        else:
            ua = Vector((0, 0, 1)).cross(n)
            ua = ua.normalized() if ua.length > 1e-6 else Vector((1, 0, 0))
            va = Vector((0, 0, 1))
        co = [me.vertices[me.loops[li].vertex_index].co for li in poly.loop_indices]
        pu = [c.dot(ua) for c in co]
        pv = [c.dot(va) for c in co]
        v0, v1 = BANDS[roles[poly.index]]
        bh = (v1 - v0) - 2 * PAD
        span = max(pv) - min(pv)
        sc = TEXEL_PER_M if span * TEXEL_PER_M <= bh else bh / max(span, 1e-6)
        off = v0 + PAD + (bh - span * sc) * 0.5
        for li, a, c2 in zip(poly.loop_indices, pu, pv):
            uv.data[li].uv = (a * sc, off + (c2 - min(pv)) * sc)


def make_lod(L):
    b = build(L)
    S = CASTLE_SCALE
    bm = bmesh.new()
    # clamp tiny ground dips (tilted ladder webs) so the pivot plane is the true bottom
    vs = [bm.verts.new((x * S, y * S, max(0.0, z * S) if z > -0.05 else z * S)) for (x, y, z) in b.v]
    lay = bm.faces.layers.int.new("role")  # noqa
    for f, r, sm in zip(b.f, b.m, b.s):
        try:
            face = bm.faces.new([vs[i] for i in f])
        except ValueError:
            continue
        face[lay] = r
        face.smooth = sm
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0005 * S)
    bm.normal_update()   # primitives are wound outward; no recalc (it flips caps on an overlapping-part soup)
    culled = cull_hidden(bm)
    bm.faces.ensure_lookup_table()
    roles = [f[lay] for f in bm.faces]
    name = f"{NAME}_LOD{L}"
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for i, p in enumerate(me.polygons):
        p.material_index = SLOT_OF[roles[i]]
    trim_uvs(me, roles)
    # sharp edges from angle so welded caps don't smear normals (FBX exports the resulting split normals)
    bm = bmesh.new()
    bm.from_mesh(me)
    for e in bm.edges:
        if len(e.link_faces) != 2 or e.calc_face_angle(0) > math.radians(40):
            e.smooth = False
    bm.to_mesh(me)
    bm.free()
    for s_ in SLOTS:
        me.materials.append(bpy.data.materials.get(s_) or bpy.data.materials.new(s_))
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if tris(ob) > LOD_TARGETS[L] * (1.0 if L == 0 else 1.1):
        mod = ob.modifiers.new("dec", 'DECIMATE')
        mod.ratio = LOD_TARGETS[L] / tris(ob)
        bpy.context.view_layer.objects.active = ob
        ob.select_set(True)
        bpy.ops.object.modifier_apply(modifier="dec")
        ob.select_set(False)
    ob["sm_culled_faces"] = culled
    return ob


def tris(ob):
    ob.data.calc_loop_triangles()
    return len(ob.data.loop_triangles)


ANCHORS = {   # concept-space positions (scaled by CASTLE_SCALE on placement)
    "Core_Socket": CORE_SOCKET,                         # shared Core module root (collar base centre, +Z up)
    "CommonsKeep": (0.0, 0.0, 7.2 + 0.85),              # tier-1 drum centre
    "CommonsDome": (0.0, 0.0, 10.55 + 0.4),             # cap dome centre
    "CitadelCrownY": (0.0, 0.0, 11.35),                 # crown = top of the cap dome (mast base)
}
for k in range(4):   # towers = the four legs, at the knee drums; 0 = back-right (+X+Y), then counter-clockwise
    p = rz(45 + 90 * k) @ Vector(KNEE)
    ANCHORS[f"CommonsTower_{k}"] = tuple(p)


def anchors(root):
    out = []
    for name, p in ANCHORS.items():
        e = bpy.data.objects.new(name, None)
        e.empty_display_type = 'ARROWS' if name == "Core_Socket" else 'PLAIN_AXES'
        e.empty_display_size = 0.8
        e.location = Vector(p) * CASTLE_SCALE
        bpy.context.scene.collection.objects.link(e)
        e.parent = root
        out.append(e)
    return out


def export_materials():
    """Export materials: the shared trim-sheet textures wired into Principled (as sm_core_citadels.py does) so the FBX
    carries the texture references; falls back to flat neutral colours if the sheet is not generated yet."""
    for slot, col in zip(SLOTS, ((0.6, 0.6, 0.6), C_CARB, (0.5, 0.5, 0.5), (0.5, 0.5, 0.5))):
        m = bpy.data.materials.get(slot) or bpy.data.materials.new(slot)
        m.use_nodes = True
        nt = m.node_tree
        p = nt.nodes.get("Principled BSDF")
        p.inputs["Base Color"].default_value = (*lin(col), 1)
        for kind, cspace, sock in (("BaseColor", "sRGB", "Base Color"), ("Normal", "Non-Color", None)):
            f = os.path.join(TEX_DIR, f"SM_Citadel_Trim_{kind}.png")
            if not os.path.exists(f):
                continue
            img = bpy.data.images.load(f, check_existing=True)
            img.colorspace_settings.name = cspace
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; t.name = kind + "Tex"
            if sock:
                nt.links.new(t.outputs[0], p.inputs[sock])
            else:
                nm = nt.nodes.new("ShaderNodeNormalMap")
                nt.links.new(t.outputs[0], nm.inputs["Color"])
                nt.links.new(nm.outputs[0], p.inputs["Normal"])


def export_fbx(root, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in [root] + list(root.children_recursive):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True,
        apply_scale_options="FBX_SCALE_ALL", apply_unit_scale=True,
        object_types={"MESH", "EMPTY"},            # EMPTY so the named anchors ship
        mesh_smooth_type="FACE", add_leaf_bones=False,
        axis_forward="-Z", axis_up="Y",
        path_mode="RELATIVE", use_custom_props=False, bake_anim=False,
    )
    bpy.ops.object.select_all(action='DESELECT')


def plan_extent(o):
    """Exact plan bounds in metres (world): must sit inside the 18 x 18 m Commons square centred on the pivot."""
    ws = [o.matrix_world @ v.co for v in o.data.vertices]
    return {"min_x": round(min(w.x for w in ws), 5), "max_x": round(max(w.x for w in ws), 5),
            "min_y": round(min(w.y for w in ws), 5), "max_y": round(max(w.y for w in ws), 5),
            "max_abs": round(max(max(abs(w.x), abs(w.y)) for w in ws), 5)}


def slot_area_pct(o):
    """Share of surface area per material slot (after hidden-face culling, so ~visible surface)."""
    tot, per = 0.0, {}
    for p in o.data.polygons:
        nm = o.data.materials[p.material_index].name.split(".")[0]
        per[nm] = per.get(nm, 0.0) + p.area
        tot += p.area
    return {k: round(100 * v / tot, 2) for k, v in per.items()}


def verify_fbx(path):
    """Re-import the FBX into a scratch scene and report names, tris, dims and anchor positions (Blender Z-up metres)."""
    src = [o for o in bpy.data.objects if o.name in ANCHORS or o.name.startswith(NAME)]
    mats_src = [m for m in bpy.data.materials if m.name in SLOTS]
    for m in mats_src:
        m.name = m.name + "__src"
    for o in src:
        o.name = o.name + "__src"
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    rep = {"objects": {}, "anchors": {}}
    for o in new:
        if o.type == 'MESH':
            bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
            dims = [max(p[i] for p in bb) - min(p[i] for p in bb) for i in range(3)]
            rep["objects"][o.name] = {"tris": tris(o), "dims": [round(x, 3) for x in dims],
                                      "min_z": round(min(p.z for p in bb), 3),
                                      "centre_xy": [round((max(p[i] for p in bb) + min(p[i] for p in bb)) / 2, 3) for i in (0, 1)],
                                      "materials": [m.name for m in o.data.materials],
                                      "uv": [u.name for u in o.data.uv_layers],
                                      "slot_area_pct": slot_area_pct(o),
                                      "plan_extent_m": plan_extent(o)}
        else:
            rep["anchors"][o.name] = [round(x, 3) for x in o.matrix_world.translation]
            rep.setdefault("parents", {})[o.name] = o.parent.name if o.parent else None
    want = list(ANCHORS)
    rep["anchor_check"] = {n: {"found": n in rep["anchors"],
                               "expected": [round(x * CASTLE_SCALE, 3) for x in ANCHORS[n]],
                               "got": rep["anchors"].get(n),
                               "err_m": (round((Vector(rep["anchors"][n]) - Vector(ANCHORS[n]) * CASTLE_SCALE).length, 4)
                                         if n in rep["anchors"] else None)} for n in want}
    rep["root_children"] = sorted(c.name for c in new if c.parent and c.parent.name == NAME)
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    for o in src:
        o.name = o.name[:-5]
    for m in [m for m in bpy.data.materials if m.name in SLOTS]:
        bpy.data.materials.remove(m)
    for m in mats_src:
        m.name = m.name[:-5]
    return rep


# ------------------------------------------------------------------ review renders
def review_materials():
    """Review-only shading: the shared trim sheet's detail (luminance + normal) re-coloured per band with the civ palette
    (hull teal-black on PANEL_DK, pale pewter on PANEL, frost glass on VENT); TeamColour tinted emerald."""
    import numpy as np
    base_f = os.path.join(TEX_DIR, "SM_Citadel_Trim_BaseColor.png")
    have = os.path.exists(base_f)
    band_mean = {}
    if have:
        img = bpy.data.images.load(base_f, check_existing=True)
        w_, h_ = img.size
        px = np.array(img.pixels[:], np.float32).reshape(h_, w_, 4)
        lum = px[..., 0] * 0.2126 + px[..., 1] * 0.7152 + px[..., 2] * 0.0722
        for k, (v0, v1) in SHEET_BANDS.items():
            band_mean[k] = float(lum[int(v0 * h_):max(int(v1 * h_), int(v0 * h_) + 1)].mean()) or 1.0

    def shader(name, bands, metal, rough):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        p = nt.nodes.new("ShaderNodeBsdfPrincipled")
        nt.links.new(p.outputs[0], out.inputs[0])
        p.inputs["Metallic"].default_value = metal
        p.inputs["Roughness"].default_value = rough
        uvn = nt.nodes.new("ShaderNodeTexCoord")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(uvn.outputs["UV"], sep.inputs[0])
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = 'CONSTANT'
        els = ramp.color_ramp.elements
        (v0, c0, k0) = bands[0]
        els[0].position, els[0].color = 0.0, (*lin(c0), 1)
        gain = nt.nodes.new("ShaderNodeValToRGB")          # per-band 1/mean-luminance (normalises the sheet detail)
        gain.color_ramp.interpolation = 'CONSTANT'
        g = gain.color_ramp.elements
        inv = lambda k: min(4.0, 1.0 / band_mean.get(k, 1.0)) if have else 1.0
        g[0].position, g[0].color = 0.0, (inv(k0),) * 3 + (1,)
        els.remove(els[1]); g.remove(g[1])
        for (v, c, k) in bands[1:]:
            e = els.new(v); e.color = (*lin(c), 1)
            e = g.new(v); e.color = (inv(k),) * 3 + (1,)
        nt.links.new(sep.outputs["Y"], ramp.inputs[0])
        nt.links.new(sep.outputs["Y"], gain.inputs[0])
        col = ramp.outputs["Color"]
        if have:
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(base_f, check_existing=True)
            bw = nt.nodes.new("ShaderNodeRGBToBW")
            nt.links.new(t.outputs[0], bw.inputs[0])
            mul = nt.nodes.new("ShaderNodeMath"); mul.operation = 'MULTIPLY'
            nt.links.new(bw.outputs[0], mul.inputs[0]); nt.links.new(gain.outputs["Color"], mul.inputs[1])
            cl_ = nt.nodes.new("ShaderNodeClamp"); cl_.inputs["Max"].default_value = 1.6
            nt.links.new(mul.outputs[0], cl_.inputs[0])
            mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
            mix.inputs["Factor"].default_value = 1.0
            nt.links.new(ramp.outputs["Color"], mix.inputs["A"])
            nt.links.new(cl_.outputs[0], mix.inputs["B"])
            col = mix.outputs["Result"]
            nf = os.path.join(TEX_DIR, "SM_Citadel_Trim_Normal.png")
            if os.path.exists(nf):
                ni = bpy.data.images.load(nf, check_existing=True); ni.colorspace_settings.name = "Non-Color"
                tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = ni
                nm = nt.nodes.new("ShaderNodeNormalMap")
                nt.links.new(tn.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], p.inputs["Normal"])
        nt.links.new(col, p.inputs["Base Color"])
        return m
    sb = SHEET_BANDS
    trim = shader("Trim", [(0.0, C_PEW, "EDGE"), (sb["GLASS"][0], C_GLS, "GLASS"), (sb["GLASS"][1], C_HULL, "PANEL_DK"),
                           (sb["PANEL"][0], C_PEW, "PANEL")], 0.35, 0.5)
    # window glow: the sheet's emission mask (0.85 inside the GLASS strip, 0 elsewhere on Trim's bands) x emerald x GLASS_EMIT
    ef = os.path.join(TEX_DIR, "SM_Citadel_Trim_Emission.png")
    if os.path.exists(ef):
        nt = trim.node_tree
        p = [n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'][0]
        ei = bpy.data.images.load(ef, check_existing=True); ei.colorspace_settings.name = "Non-Color"
        te = nt.nodes.new("ShaderNodeTexImage"); te.image = ei
        bw = nt.nodes.new("ShaderNodeRGBToBW")
        nt.links.new(te.outputs[0], bw.inputs[0])
        p.inputs["Emission Color"].default_value = (*lin(C_TEAM), 1)
        mul = nt.nodes.new("ShaderNodeMath"); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = GLASS_EMIT
        nt.links.new(bw.outputs[0], mul.inputs[0])
        nt.links.new(mul.outputs[0], p.inputs["Emission Strength"])
    shader("Carbon", [(0.0, C_CARB, "CARB")], 0.2, 0.65)
    shader("TeamColour", [(0.0, C_TEAM, "TEAM")], 0.1, 0.5)
    shader("Accent", [(0.0, C_ACC, "TEAM")], 0.1, 0.5)

    def flat(name, col, metal, rough, emit=None, es=0.0):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        p = m.node_tree.nodes.get("Principled BSDF")
        p.inputs["Base Color"].default_value = (*lin(col), 1)
        p.inputs["Metallic"].default_value = metal
        p.inputs["Roughness"].default_value = rough
        if emit:
            p.inputs["Emission Color"].default_value = (*lin(emit), 1)
            p.inputs["Emission Strength"].default_value = es
    flat("SM_CoreStandIn_Cyan", C_CYAN, 0.0, 0.3, C_CYAN, CORE_EMIT)
    flat("SM_CoreStandIn_Carbon", C_CARB, 0.2, 0.65)
    flat("Ground", (0.50, 0.40, 0.31), 0.0, 0.95)
    flat("ScaleBot", (0.93, 0.93, 0.93), 0.0, 0.5)


def core_module_review(root):
    """Review-only: drop the shared SM_Core_Module.fbx (from sm_core_citadels.py) on Core_Socket; recolour its slots.
    Falls back to a simple stand-in if it is not there. Never exported with the castle."""
    f = os.path.join(EXPORT_DIR, "SM_Core_Module.fbx")
    sock = bpy.data.objects["Core_Socket"]
    if os.path.exists(f):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=f)
        new = [o for o in bpy.data.objects if o not in before]
        top = [o for o in new if o.parent is None]
        for o in top:
            o.matrix_world = Matrix.Translation(sock.matrix_world.translation)
        for o in new:
            if o.type == 'MESH':
                for i, m in enumerate(o.data.materials):
                    nm = m.name.split(".")[0] if m else ""
                    o.data.materials[i] = bpy.data.materials["SM_CoreStandIn_Cyan" if nm == "CoreEmission" else "SM_CoreStandIn_Carbon"]
        return new, "SM_Core_Module.fbx"
    return [core_standin()], "stand-in"


def core_standin():
    """Render-only stand-in for SM_Core_Module at Core_Socket (not exported)."""
    b = Builder()
    r = 1.05
    c = Vector(CORE_SOCKET) + Vector((0, 0, CORE_CRYSTAL_Z))
    b.dome(tuple(c), r, r, 24, 7, 0)
    b.dome((0, 0, 0), r, r, 24, 7, 0, Matrix.Translation(c) @ Matrix.Rotation(math.pi, 4, 'X'))
    b.torus(tuple(c), r * 1.22, r * 0.26, 32, 8, 1)
    b.ring(r * 1.5, r * 1.72, c.z - r * 0.12, c.z + r * 0.12, 1, n=32)
    S = CASTLE_SCALE
    me = bpy.data.meshes.new("CoreStandIn")
    me.from_pydata([tuple(Vector(v) * S) for v in b.v], [], b.f)
    me.polygons.foreach_set("material_index", b.m)
    me.polygons.foreach_set("use_smooth", b.s)
    me.materials.append(bpy.data.materials["SM_CoreStandIn_Cyan"])
    me.materials.append(bpy.data.materials["SM_CoreStandIn_Carbon"])
    ob = bpy.data.objects.new("CoreStandIn", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def look(cam, pos, tgt):
    cam.location = pos
    cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()


def render_all(lods, rep):
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE_NEXT'
    s.render.resolution_x, s.render.resolution_y = 1920, 1080
    s.render.image_settings.file_format = 'PNG'
    for k, v in (("taa_render_samples", 32), ("use_raytracing", True), ("use_shadows", True)):
        try:
            setattr(s.eevee, k, v)
        except Exception:
            pass
    try:
        s.view_settings.view_transform = 'AgX'
        s.view_settings.look = 'AgX - Medium High Contrast'
    except Exception:
        pass
    review_materials()
    core_objs, core_src = core_module_review(None)
    rep["review_core"] = core_src
    gme = bpy.data.meshes.new("Ground")
    gme.from_pydata([(-400, -400, 0), (400, -400, 0), (400, 400, 0), (-400, 400, 0)], [], [(0, 1, 2, 3)])
    gme.materials.append(bpy.data.materials["Ground"])
    ground = bpy.data.objects.new("Ground", gme)
    s.collection.objects.link(ground)
    bb = Builder()
    bb.cyl((0, 0, 0.3), 0.3, 0.3, 1.2, 16, 0, caps=False)
    bb.dome((0, 0, 1.5), 0.3, 0.3, 16, 4, 0)
    bb.dome((0, 0, 0.3), 0.3, 0.3, 16, 4, 0, Matrix.Translation((0, 0, 0.6)) @ Matrix.Rotation(math.pi, 4, 'X'))
    bme = bpy.data.meshes.new("ScaleBot")
    bme.from_pydata(bb.v, [], bb.f)
    bme.materials.append(bpy.data.materials["ScaleBot"])
    bot = bpy.data.objects.new("ScaleBot_1p8m", bme)
    bot.location = (2.6 * CASTLE_SCALE, -8.6 * CASTLE_SCALE, 0)
    s.collection.objects.link(bot)

    w = bpy.data.worlds.new("World")
    try:
        w.use_nodes = True
    except Exception:
        pass
    bg = w.node_tree.nodes.get("Background")
    s.world = w
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    s.collection.objects.link(sun)
    sun.data.angle = math.radians(3)
    cl = bpy.data.objects.new("CoreGlow", bpy.data.lights.new("CoreGlow", 'POINT'))
    s.collection.objects.link(cl)
    cl.location = (Vector(CORE_SOCKET) + Vector((0, 0, CORE_CRYSTAL_Z))) * CASTLE_SCALE
    cl.data.color = lin(C_CYAN)
    cl.data.shadow_soft_size = 1.0
    cl.data.use_shadow = False
    camd = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", camd)
    s.collection.objects.link(cam)
    s.camera = cam

    def rig(kind):
        if kind == 'dusk':
            d, col, st, wc, ws, cp, eg = (-0.62, 0.66, -0.42), (1.0, 0.62, 0.42), 6.0, (0.34, 0.40, 0.70), 1.3, 900, 0.0
        elif kind == 'studio':
            d, col, st, wc, ws, cp, eg = (-0.45, 0.55, -0.75), (1, 0.98, 0.95), 3.2, (0.6, 0.63, 0.68), 0.8, 300, 0.0
        else:
            d, col, st, wc, ws, cp, eg = (0.4, 0.5, -0.8), (0.5, 0.62, 1.0), 0.18, (0.012, 0.018, 0.04), 1.0, 5000, 0.4
        sun.rotation_euler = Vector(d).to_track_quat('-Z', 'Y').to_euler()
        sun.data.color, sun.data.energy = col, st
        bg.inputs[0].default_value = (*wc, 1)
        bg.inputs[1].default_value = ws
        cl.data.energy = cp

    S = CASTLE_SCALE
    H = 14.5 * S
    tgt = Vector((0, 0, H * 0.42))

    def shot(path, kind, el, az, dist, ortho=None, target=None):
        rig(kind)
        t = target or tgt
        el_r, az_r = math.radians(el), math.radians(az)
        dv = Vector((math.sin(az_r) * math.cos(el_r), -math.cos(az_r) * math.cos(el_r), math.sin(el_r)))
        if ortho:
            camd.type, camd.ortho_scale = 'ORTHO', ortho
        else:
            camd.type, camd.lens = 'PERSP', 50
        camd.clip_end = 2000
        if el >= 89.9:
            cam.location, cam.rotation_euler = (t.x, t.y, 200), (0, 0, 0)
        else:
            look(cam, t + dv * dist, t)
        s.render.filepath = path
        bpy.ops.render.render(write_still=True)

    def only(ob):
        for o in lods:
            o.hide_render = o is not ob
        for o in lods:
            o.location = (0, 0, 0)

    os.makedirs(RENDER_DIR, exist_ok=True)
    only(lods[0])
    shot(os.path.join(RENDER_DIR, "hero_dusk.png"), 'dusk', 30, -35, 62)
    shot(os.path.join(RENDER_DIR, "top_down.png"), 'studio', 90, 0, 0, ortho=34.0, target=Vector((0, -1.5, 0)))
    shot(os.path.join(RENDER_DIR, "night.png"), 'night', 28, 38, 60)
    shot(os.path.join(RENDER_DIR, "front_studio.png"), 'studio', 4, 0, 90, ortho=44.0, target=Vector((0, 0, H * 0.5)))
    # LOD strip: three LODs side by side, same light, studio
    gap = 22.0
    for i, o in enumerate(lods):
        o.hide_render = False
        o.location = ((i - 1) * gap, 0, 0)
    for o in core_objs:
        o.hide_render = True
    bot.hide_render = True
    s.render.resolution_x, s.render.resolution_y = 1920, 720
    shot(os.path.join(RENDER_DIR, "lod_strip.png"), 'studio', 22, -20, 150, ortho=72.0, target=Vector((0, 0, H * 0.45)))
    s.render.resolution_x, s.render.resolution_y = 1920, 1080
    for o in lods:
        o.location = (0, 0, 0)
    for o in core_objs:
        o.hide_render = False
    bot.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(RENDER_DIR, "SM_Citadel_Arcology_review.blend"))


# ------------------------------------------------------------------ main
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    export_materials()
    lods = [make_lod(L) for L in range(3)]
    root = bpy.data.objects.new(NAME, None)
    root.empty_display_size = 2.0
    bpy.context.scene.collection.objects.link(root)
    for o in lods:
        o.parent = root
    anchors(root)
    path = os.path.join(EXPORT_DIR, NAME + ".fbx")
    export_fbx(root, path)
    rep = verify_fbx(path)
    rep["castle_scale"] = CASTLE_SCALE
    rep["built"] = {o.name: {"tris": tris(o), "dims": [round(x, 3) for x in o.dimensions], "culled_faces": o["sm_culled_faces"]}
                    for o in lods}
    rep["fbx"] = path
    rep["fbx_bytes"] = os.path.getsize(path)
    os.makedirs(RENDER_DIR, exist_ok=True)
    with open(os.path.join(RENDER_DIR, "build_report.json"), "w") as fh:
        json.dump(rep, fh, indent=1)
    print("SM_ARCOLOGY", json.dumps(rep["built"]), json.dumps(rep["anchor_check"]))
    if "--render" in ARGS:
        render_all(lods, rep)
        with open(os.path.join(RENDER_DIR, "build_report.json"), "w") as fh:
            json.dump(rep, fh, indent=1)


main()
