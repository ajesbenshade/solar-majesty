#!/usr/bin/env python3
"""Solar Majesty hero roster v2 — armored humanoid "knights" (2026-09-22 concept art).

All ten specialist classes share one humanoid skeleton. Each class gets a build preset
(slim / medium / heavy), a helmet style, an accent scheme, and a signature prop kit that
matches ConceptSheets/Heroes_v2/SM_Unit_*_Hero_v2.jpg.

Clips (every bone keyed on every frame, so no clip inherits another clip's pose):
  Idle    48f loop   breathing, weight shift, look-around, prop secondary motion
  Walk    ~13f loop  planted-foot jog (analytic 2-bone IK), speed-matched in Unity
  Strike  24f once   class attack: anticipation -> hit -> recover
  Work    32f loop   class labour (drill, scoop, rake, heal, scan ...)
  Down    30f once   collapse to a kneel-slump; last frame is held by UnitClipPlayer

Rest-pose convention: every bone points straight up (+Z) with zero roll, so for any bone
  rx > 0  swings a hanging limb forward (+Y) / tips a torso back
  ry > 0  yaws left (counter-clockwise from above)
  rz > 0  swings a hanging limb toward +X / rolls a torso so its right side rises
Front is +Y, the character's right hand is +X.

Imported by sm_animated_roster.py. Needs Blender 4.4+ (action slots). Tested on 5.0.
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

TAU = math.tau
FPS = 24


# ---------------------------------------------------------------------------
# small maths helpers
# ---------------------------------------------------------------------------

def clamp(x, lo, hi):
    return lo if x < lo else hi if x > hi else x


def smooth(t):
    t = clamp(t, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def smoother(t):
    t = clamp(t, 0.0, 1.0)
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


def ease_out_back(t, s=1.7):
    t = clamp(t, 0.0, 1.0) - 1.0
    return t * t * ((s + 1.0) * t + s) + 1.0


def keys(t, pts):
    """Piecewise smootherstep through [(t0, v0), (t1, v1), ...]. Holds the ends."""
    if t <= pts[0][0]:
        return pts[0][1]
    for (t0, v0), (t1, v1) in zip(pts, pts[1:]):
        if t <= t1:
            u = smoother((t - t0) / max(t1 - t0, 1e-6))
            return v0 + (v1 - v0) * u
    return pts[-1][1]


def wave(t, cycles=1.0, phase=0.0):
    return math.sin((t * cycles + phase) * TAU)


# ---------------------------------------------------------------------------
# geometry toolkit (rigid-skinned hard-surface parts)
# ---------------------------------------------------------------------------

def _finish(name, bm, mat, bone, smooth_shade=False):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(me.vertices))), 1.0, "REPLACE")
    if smooth_shade:
        for p in me.polygons:
            p.use_smooth = True
    return obj


def _place(bm, loc, rot):
    if rot and any(abs(r) > 1e-5 for r in rot):
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Euler(rot, "XYZ").to_matrix())
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(loc))


def _bevel(bm, amount, segs):
    if amount <= 0:
        return
    try:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=amount, segments=segs, profile=0.5,
                        affect="EDGES", clamp_overlap=True)
    except TypeError:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=amount, segments=segs, affect="EDGES")


class Kit:
    """Collects parts for one character. Every part is rigidly bound to one bone."""

    def __init__(self, mats):
        self.m = mats
        self.parts = []
        self.n = 0
        self.xf = None      # optional Matrix applied to new parts (author-in-pose helper)

    def _obj(self, name, bm, mat, bone, smooth_shade=False):
        if self.xf is not None:
            bmesh.ops.transform(bm, verts=bm.verts, matrix=self.xf)
        return _finish(name, bm, mat, bone, smooth_shade)

    def _nm(self, tag):
        self.n += 1
        return f"p{self.n:03d}_{tag}"

    def box(self, size, loc, mat, bone, bevel=None, segs=1, rot=(0, 0, 0), taper=(1.0, 1.0),
            top_taper=(1.0, 1.0), shift_top=(0.0, 0.0)):
        """Bevelled box. taper scales the bottom face, top_taper the top face."""
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        sx, sy, sz = size
        for v in bm.verts:
            v.co.x *= sx
            v.co.y *= sy
            v.co.z *= sz
            if v.co.z < 0:
                v.co.x *= taper[0]
                v.co.y *= taper[1]
            else:
                v.co.x *= top_taper[0]
                v.co.y *= top_taper[1]
                v.co.x += shift_top[0]
                v.co.y += shift_top[1]
        b = bevel if bevel is not None else min(sx, sy, sz) * 0.16
        if max(sx, sy, sz) < 0.13:
            segs = 1
        _bevel(bm, b, segs)
        _place(bm, loc, rot)
        self.parts.append(self._obj(self._nm("box"), bm, self.m[mat], bone))
        return self.parts[-1]

    def cyl(self, r, depth, loc, mat, bone, axis="Z", verts=12, r2=None, bevel=0.0, rot=None, smooth_side=True):
        bm = bmesh.new()
        if r < 0.03:
            verts = min(verts, 8)
        elif r < 0.07:
            verts = min(verts, 12)
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=verts, radius1=r,
                              radius2=r if r2 is None else r2, depth=depth)
        if bevel > 0:
            caps = [e for e in bm.edges if len(e.link_faces) == 2 and
                    any(len(f.verts) > 4 for f in e.link_faces)]
            try:
                bmesh.ops.bevel(bm, geom=caps, offset=bevel, segments=2, profile=0.5,
                                affect="EDGES", clamp_overlap=True)
            except TypeError:
                pass
        base = {"Z": (0, 0, 0), "X": (0, math.pi / 2, 0), "Y": (-math.pi / 2, 0, 0)}[axis]
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Euler(base, "XYZ").to_matrix())
        _place(bm, loc, rot)
        obj = self._obj(self._nm("cyl"), bm, self.m[mat], bone)
        if smooth_side:
            for p in obj.data.polygons:
                p.use_smooth = len(p.vertices) == 4
        self.parts.append(obj)
        return obj

    def seg(self, a, b, r, mat, bone, r2=None, verts=12):
        a, b = Vector(a), Vector(b)
        d = b - a
        q = d.normalized().to_track_quat("Z", "Y")
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=verts, radius1=r,
                              radius2=r if r2 is None else r2, depth=max(d.length, 0.01))
        bmesh.ops.transform(bm, verts=bm.verts, matrix=Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4())
        obj = self._obj(self._nm("seg"), bm, self.m[mat], bone)
        for p in obj.data.polygons:
            p.use_smooth = len(p.vertices) == 4
        self.parts.append(obj)
        return obj

    def sphere(self, r, loc, mat, bone, scale=(1, 1, 1), u=16, v=10, rot=None):
        u, v = min(u, 18), min(v, 11)
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
        for vert in bm.verts:
            vert.co.x *= scale[0]
            vert.co.y *= scale[1]
            vert.co.z *= scale[2]
        _place(bm, loc, rot)
        self.parts.append(self._obj(self._nm("sph"), bm, self.m[mat], bone, smooth_shade=True))
        return self.parts[-1]

    def torus(self, R, r, loc, mat, bone, axis="Z", major=20, minor=8):
        bm = bmesh.new()
        for i in range(major):
            a = i / major * TAU
            for j in range(minor):
                b = j / minor * TAU
                x = (R + r * math.cos(b)) * math.cos(a)
                y = (R + r * math.cos(b)) * math.sin(a)
                z = r * math.sin(b)
                bm.verts.new((x, y, z))
        bm.verts.ensure_lookup_table()
        for i in range(major):
            for j in range(minor):
                a = i * minor + j
                b = ((i + 1) % major) * minor + j
                c = ((i + 1) % major) * minor + (j + 1) % minor
                d = i * minor + (j + 1) % minor
                bm.faces.new((bm.verts[a], bm.verts[b], bm.verts[c], bm.verts[d]))
        base = {"Z": (0, 0, 0), "X": (0, math.pi / 2, 0), "Y": (math.pi / 2, 0, 0)}[axis]
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Euler(base, "XYZ").to_matrix())
        _place(bm, loc, None)
        self.parts.append(self._obj(self._nm("tor"), bm, self.m[mat], bone, smooth_shade=True))
        return self.parts[-1]

    def slab(self, outline, thick, loc, mat, bone, bevel=0.01, rot=(0, 0, 0)):
        """Extrude a 2D outline drawn in the XZ plane (front = +Y) by `thick` along Y."""
        bm = bmesh.new()
        front = [bm.verts.new((x, thick / 2, z)) for x, z in outline]
        back = [bm.verts.new((x, -thick / 2, z)) for x, z in outline]
        n = len(outline)
        bm.faces.new(front)
        bm.faces.new(list(reversed(back)))
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((front[j], front[i], back[i], back[j]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        _bevel(bm, bevel, 1)
        _place(bm, loc, rot)
        self.parts.append(self._obj(self._nm("slab"), bm, self.m[mat], bone))
        return self.parts[-1]

    def rock(self, r, loc, mat, bone, seed=0):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r)
        for i, v in enumerate(bm.verts):
            k = 0.72 + 0.5 * abs(math.sin(seed * 12.9898 + i * 78.233) * 0.7)
            v.co *= k
        _place(bm, loc, (seed * 0.7, seed * 1.3, seed * 0.4))
        self.parts.append(self._obj(self._nm("rock"), bm, self.m[mat], bone))
        return self.parts[-1]


# ---------------------------------------------------------------------------
# skeleton
# ---------------------------------------------------------------------------

@dataclass
class Build:
    name: str
    foot_h: float
    shin: float
    thigh: float
    hip_w: float
    chest_w: float
    chest_d: float
    chest_h: float
    shoulder_w: float
    upper: float
    forearm: float
    head_r: float
    neck: float
    bulk: float
    pad: float          # shoulder pauldron scale
    boot: float         # boot length
    stride_duty: float  # stance fraction of the jog cycle

    @property
    def ankle_z(self):
        return self.foot_h

    @property
    def knee_z(self):
        return self.foot_h + self.shin

    @property
    def hip_z(self):
        return self.foot_h + self.shin + self.thigh

    @property
    def leg(self):
        return self.shin + self.thigh

    @property
    def spine_z(self):
        return self.hip_z + 0.13

    @property
    def chest_base_z(self):
        return self.spine_z + 0.10

    @property
    def chest_z(self):
        return self.chest_base_z + self.chest_h / 2

    @property
    def chest_top(self):
        return self.chest_base_z + self.chest_h

    @property
    def shoulder_z(self):
        return self.chest_top - 0.07 * self.bulk

    @property
    def neck_z(self):
        return self.chest_top - 0.02

    @property
    def head_z(self):
        return self.neck_z + self.neck

    @property
    def elbow_z(self):
        return self.shoulder_z - self.upper

    @property
    def wrist_z(self):
        return self.elbow_z - self.forearm

    def arm_x(self, s, joint):
        splay = {"shoulder": 0.0, "elbow": 0.02, "wrist": 0.035}[joint] * self.bulk
        return s * (self.shoulder_w + splay)


BUILDS = {
    "slim": Build("slim", foot_h=0.10, shin=0.45, thigh=0.45, hip_w=0.115, chest_w=0.44, chest_d=0.30,
                  chest_h=0.40, shoulder_w=0.27, upper=0.29, forearm=0.28, head_r=0.155, neck=0.06,
                  bulk=0.9, pad=0.85, boot=0.28, stride_duty=0.36),
    "medium": Build("medium", foot_h=0.11, shin=0.41, thigh=0.41, hip_w=0.145, chest_w=0.56, chest_d=0.38,
                    chest_h=0.44, shoulder_w=0.34, upper=0.28, forearm=0.29, head_r=0.16, neck=0.05,
                    bulk=1.12, pad=1.0, boot=0.32, stride_duty=0.40),
    "heavy": Build("heavy", foot_h=0.13, shin=0.41, thigh=0.39, hip_w=0.185, chest_w=0.70, chest_d=0.46,
                   chest_h=0.48, shoulder_w=0.43, upper=0.29, forearm=0.31, head_r=0.155, neck=0.05,
                   bulk=1.38, pad=0.92, boot=0.38, stride_duty=0.42),
}

SIDES = (("L", -1), ("R", 1))

_C = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))   # bone-local basis -> world (columns X, Y, Z)


def pose_matrix(rig, chain):
    """World-space deform matrix of the last bone in `chain` [(bone, (rx, ry, rz)), ...] (root first)."""
    M = Matrix.Identity(4)
    for bone, (rx, ry, rz) in chain:
        pvt = rig.heads[bone]
        R = (_C @ Euler((rx, ry, rz), "XYZ").to_matrix() @ _C.inverted()).to_4x4()
        M = M @ Matrix.Translation(pvt) @ R @ Matrix.Translation(-pvt)
    return M


class authored_in_pose:
    """Parts created inside this block are modelled where they should sit in the hold pose."""

    def __init__(self, k, rig, chain):
        self.k, self.m = k, pose_matrix(rig, chain).inverted()

    def __enter__(self):
        self.k.xf = self.m

    def __exit__(self, *exc):
        self.k.xf = None


class HeroRig:
    def __init__(self, name, build: Build):
        self.b = build
        arm = bpy.data.armatures.new(name + "_RigData")
        self.obj = bpy.data.objects.new(name + "_Rig", arm)
        bpy.context.scene.collection.objects.link(self.obj)
        bpy.context.view_layer.objects.active = self.obj
        bpy.ops.object.mode_set(mode="EDIT")
        self.heads = {}
        b = build
        self.joint("Root", (0, 0, 0), None, 0.2)
        self.joint("Hips", (0, 0, b.hip_z), "Root")
        self.joint("Spine", (0, 0, b.spine_z), "Hips")
        self.joint("Chest", (0, 0, b.chest_base_z), "Spine")
        self.joint("Neck", (0, 0, b.neck_z), "Chest", 0.05)
        self.joint("Head", (0, 0, b.head_z), "Neck")
        self.joint("Pack", (0, -b.chest_d * 0.5, b.chest_z), "Chest")
        for S, s in SIDES:
            self.joint(f"Shoulder{S}", (s * b.shoulder_w * 0.5, 0, b.shoulder_z), "Chest", 0.06)
            self.joint(f"UpperArm{S}", (b.arm_x(s, "shoulder"), 0, b.shoulder_z), f"Shoulder{S}")
            self.joint(f"Forearm{S}", (b.arm_x(s, "elbow"), 0, b.elbow_z), f"UpperArm{S}")
            self.joint(f"Hand{S}", (b.arm_x(s, "wrist"), 0, b.wrist_z), f"Forearm{S}", 0.06)
            self.joint(f"Thigh{S}", (s * b.hip_w, 0, b.hip_z), "Hips")
            self.joint(f"Shin{S}", (s * b.hip_w, 0, b.knee_z), f"Thigh{S}")
            self.joint(f"Foot{S}", (s * b.hip_w, 0, b.ankle_z), f"Shin{S}", 0.06)

    def joint(self, name, head, parent, length=0.10):
        eb = self.obj.data.edit_bones.new(name)
        eb.head = Vector(head)
        eb.tail = Vector(head) + Vector((0, 0, length))
        eb.roll = 0.0
        if parent:
            eb.parent = self.obj.data.edit_bones[parent]
        self.heads[name] = Vector(head)
        return name

    def finish(self):
        bpy.ops.object.mode_set(mode="OBJECT")
        for pb in self.obj.pose.bones:
            pb.rotation_mode = "ZYX" if pb.name.startswith("Foot") else "XYZ"
        return self.obj


# ---------------------------------------------------------------------------
# body + class kits
# ---------------------------------------------------------------------------

@dataclass
class Style:
    build: str
    helmet: str = "knight"       # knight | dome | round3 | angular | slit
    visor: str = "cyan"          # material key for the visor glow
    limb: str = "white"          # armour colour on shins / forearms (Harvester runs dark)
    chest_band: bool = False     # Engineer orange wrap band
    crest: bool = False          # Geologist orange helmet stripe
    ear_pods: bool = True
    heavy_hands: bool = False
    pad_mark: str = ""           # material key for a triangle decal on the pauldrons
    extras: dict = field(default_factory=dict)


def build_body(k: Kit, rig: HeroRig, st: Style):
    b = rig.b
    B = b.bulk
    W = "white"
    limb = st.limb

    # --- pelvis ---------------------------------------------------------------
    k.box((b.hip_w * 2 + 0.10 * B, 0.24 * B, 0.18), (0, 0, b.hip_z + 0.02), "black", "Hips", bevel=0.03)
    k.box((b.hip_w * 2 + 0.16 * B, 0.29 * B, 0.06), (0, 0, b.hip_z + 0.11), "graphite", "Hips", bevel=0.02)
    k.box((0.10 * B, 0.03, 0.05), (0, 0.15 * B, b.hip_z + 0.11), "orange", "Hips", bevel=0.008)
    k.box((0.24 * B, 0.06, 0.16), (0, 0.14 * B, b.hip_z - 0.03), W, "Hips", bevel=0.02, taper=(0.7, 1.0))
    for S, s in SIDES:
        k.box((0.05, 0.19 * B, 0.17), (s * (b.hip_w + 0.085 * B), 0.0, b.hip_z - 0.07), W, "Thigh" + S,
              bevel=0.018, rot=(0, s * 0.12, 0), taper=(1.0, 0.8))

    # --- abdomen: stacked black servo rings -----------------------------------
    for i in range(3):
        z = b.hip_z + 0.16 + i * 0.055
        w = (0.30 + 0.03 * i) * B
        k.box((w, w * 0.78, 0.045), (0, 0, z), "black", "Spine", bevel=0.015)
    for S, s in SIDES:
        k.seg((s * 0.12 * B, -0.02, b.hip_z + 0.12), (s * 0.15 * B, -0.03, b.chest_base_z + 0.04), 0.018,
              "steel", "Spine", verts=8)

    # --- chest cuirass ----------------------------------------------------------
    cz = b.chest_z
    k.box((b.chest_w, b.chest_d, b.chest_h), (0, 0, cz), W, "Chest", bevel=0.05 * B, segs=3,
          taper=(0.72, 0.82))
    for S, s in SIDES:
        k.box((b.chest_w * 0.42, 0.05, b.chest_h * 0.52), (s * b.chest_w * 0.22, b.chest_d * 0.5 + 0.005,
              cz + b.chest_h * 0.14), W, "Chest", bevel=0.018, rot=(0.12, 0, -s * 0.2), taper=(0.8, 1.0))
    k.box((0.075 * B, 0.05, b.chest_h * 0.62), (0, b.chest_d * 0.48, cz - b.chest_h * 0.06), "black", "Chest",
          bevel=0.012)
    k.box((b.chest_w * 0.8, 0.07, b.chest_h * 0.78), (0, -b.chest_d * 0.5, cz + 0.02), "graphite", "Chest",
          bevel=0.02)
    k.box((b.chest_w * 0.62, b.chest_d * 0.7, 0.05), (0, 0, b.chest_top + 0.005), "black", "Chest", bevel=0.015)
    k.cyl(0.085 * B, 0.07, (0, 0, b.neck_z + 0.01), "black", "Neck", verts=14, bevel=0.01)
    k.box((b.chest_w * 0.5, b.chest_d * 0.66, 0.07), (0, -0.01, b.chest_top + 0.03), W, "Chest", bevel=0.025,
          top_taper=(0.8, 0.8))
    k.box((b.chest_w * 0.34, 0.05, 0.1), (0, b.chest_d * 0.36, b.hip_z + 0.2), W, "Spine", bevel=0.015,
          taper=(0.8, 1.0))
    if st.chest_band:
        k.box((b.chest_w * 1.02, b.chest_d * 1.04, 0.075), (0, 0.004, cz - b.chest_h * 0.06), "orange", "Chest",
              bevel=0.02, taper=(0.96, 0.98))

    # --- head -----------------------------------------------------------------
    build_helmet(k, rig, st)

    # --- arms ------------------------------------------------------------------
    for S, s in SIDES:
        sx, ex, wx = b.arm_x(s, "shoulder"), b.arm_x(s, "elbow"), b.arm_x(s, "wrist")
        sz, ez, wz = b.shoulder_z, b.elbow_z, b.wrist_z
        ua, fa, hand = f"UpperArm{S}", f"Forearm{S}", f"Hand{S}"
        k.sphere(0.085 * B, (sx, 0, sz), "black", ua, u=14, v=10)
        pw, pd, ph = 0.22 * B * b.pad, 0.28 * B * b.pad, 0.17 * B * b.pad
        k.box((pw, pd, ph), (sx + s * 0.035 * B, 0, sz + 0.035 * B), W, ua, bevel=0.055 * B * b.pad, segs=3,
              rot=(0, s * 0.3, 0), taper=(1.1, 1.06), top_taper=(0.8, 0.86))
        k.box((pw * 0.72, pd * 0.8, ph * 0.5), (sx + s * 0.07 * B, 0, sz - 0.045 * B), W, ua,
              bevel=0.025 * B, segs=2, rot=(0, s * 0.3, 0))
        k.box((pw * 0.9, pd * 0.94, 0.03), (sx + s * 0.05 * B, 0, sz - 0.005), "black", ua, bevel=0.01,
              rot=(0, s * 0.3, 0))
        if st.pad_mark:
            k.slab([(-0.04, 0.03), (0.04, 0.03), (0.0, -0.035)], 0.02,
                   (sx + s * 0.05 * B, pd * 0.5 + 0.005, sz + 0.04 * B), st.pad_mark, ua, bevel=0.003)
        k.seg((sx, 0, sz), (ex, 0, ez), 0.05 * B, "black", ua, verts=10)
        k.box((0.12 * B, 0.13 * B, b.upper * 0.58), (sx + s * 0.012, 0, (sz + ez) / 2 - 0.02), W, ua,
              bevel=0.025, taper=(0.86, 0.9))
        k.cyl(0.058 * B, 0.13 * B, (ex, 0, ez), "black", fa, axis="X", verts=14, bevel=0.01)
        k.cyl(0.03 * B, 0.14 * B, (ex, 0, ez), "graphite", fa, axis="X", verts=10)
        fz = (ez + wz) / 2 - 0.01
        k.box((0.14 * B, 0.16 * B, b.forearm * 0.74), (ex + s * 0.01, 0.005, fz), limb, fa, bevel=0.03, segs=2,
              taper=(0.82, 0.86))
        k.box((0.12 * B, 0.14 * B, 0.035), (wx, 0, wz + 0.035), "black", fa, bevel=0.01)
        hs = 1.4 if st.heavy_hands else 1.2
        k.box((0.075 * B * hs, 0.095 * B * hs, 0.10 * hs), (wx, 0.005, wz - 0.05 * hs), "black", hand, bevel=0.015)
        k.box((0.07 * B * hs, 0.085 * B * hs, 0.05 * hs), (wx, 0.02, wz - 0.115 * hs), "graphite", hand,
              bevel=0.012, rot=(0.35, 0, 0))

    # --- legs ------------------------------------------------------------------
    for S, s in SIDES:
        x = s * b.hip_w
        th, sh, ft = f"Thigh{S}", f"Shin{S}", f"Foot{S}"
        k.sphere(0.075 * B, (x, 0, b.hip_z), "black", th, u=12, v=8)
        k.seg((x, 0, b.hip_z), (x, 0, b.knee_z), 0.065 * B, "black", th, verts=12)
        k.box((0.19 * B, 0.22 * B, b.thigh * 0.66), (x + s * 0.01, 0.012, b.knee_z + b.thigh * 0.52), W, th,
              bevel=0.03, segs=2, taper=(0.84, 0.88))
        k.cyl(0.07 * B, 0.15 * B, (x, 0, b.knee_z), "black", sh, axis="X", verts=14, bevel=0.012)
        k.box((0.13 * B, 0.08, 0.13 * B), (x, 0.085 * B, b.knee_z + 0.005), W, sh, bevel=0.025,
              rot=(-0.1, 0, 0), top_taper=(0.85, 0.9))
        k.box((0.18 * B, 0.22 * B, b.shin * 0.68), (x, 0.02, b.ankle_z + b.shin * 0.46), limb, sh, bevel=0.03,
              segs=2, taper=(0.82, 0.86), top_taper=(1.0, 1.0))
        k.box((0.08 * B, 0.07, b.shin * 0.55), (x, -0.08 * B, b.ankle_z + b.shin * 0.48), "black", sh, bevel=0.015)
        k.cyl(0.05 * B, 0.13 * B, (x, 0, b.ankle_z), "black", ft, axis="X", verts=12)
        bl = b.boot
        k.box((0.18 * B, bl * 0.78, 0.12), (x, bl * 0.18, b.ankle_z - 0.03), W, ft, bevel=0.03, segs=2,
              top_taper=(0.85, 0.7), shift_top=(0, -0.03))
        k.box((0.2 * B, bl, 0.045), (x, bl * 0.2, 0.0225), "black", ft, bevel=0.012)
        k.box((0.15 * B, 0.03, 0.03), (x, bl * 0.68, 0.055), "orange", ft, bevel=0.006)


def build_helmet(k: Kit, rig: HeroRig, st: Style):
    b = rig.b
    r = b.head_r
    hz = b.head_z + r * 0.82
    H = "Head"
    vis = st.visor
    if st.helmet == "dome":         # Medic: big round helmet, wraparound visor
        r *= 1.12
        hz = b.head_z + r * 0.92
        k.sphere(r, (0, 0, hz), "white", H, scale=(0.98, 1.02, 1.0), u=24, v=14)
        k.sphere(r * 0.86, (0, r * 0.28, hz - r * 0.08), "graphite", H, scale=(0.95, 0.72, 0.62), u=20, v=12)
        k.box((r * 1.2, 0.02, 0.018), (0, r * 0.97, hz - r * 0.02), vis, H, bevel=0.006)
        k.box((0.03, 0.02, r * 0.5), (0, r * 0.95, hz - r * 0.1), vis, H, bevel=0.006)
        k.box((r * 0.9, r * 0.8, 0.04), (0, 0, hz + r * 0.93), "white", H, bevel=0.015)
    elif st.helmet == "round3":     # Surveyor: dome head, black eye band with three lenses
        k.sphere(r * 1.05, (0, 0, hz), "white", H, scale=(1.0, 1.05, 0.98), u=24, v=14)
        k.box((r * 1.7, r * 0.6, r * 0.42), (0, r * 0.62, hz - r * 0.12), "black", H, bevel=0.03, segs=3)
        for i, x in enumerate((-0.36, 0.0, 0.36)):
            k.cyl(r * 0.13, 0.03, (x * r, r * 0.93, hz - r * 0.12), vis, H, axis="Y", verts=12)
    elif st.helmet == "angular":    # Defense / Sentinel: faceted great-helm, slit visor
        k.box((r * 1.9, r * 2.0, r * 1.75), (0, 0.01, hz), "white", H, bevel=r * 0.35, segs=1,
              top_taper=(0.78, 0.82))
        k.box((r * 0.28, r * 2.1, r * 0.25), (0, 0, hz + r * 0.86), "white", H, bevel=0.012)
        k.box((r * 1.5, 0.05, r * 0.3), (0, r * 1.0, hz - r * 0.05), "graphite", H, bevel=0.012,
              top_taper=(0.85, 1.0))
        k.box((r * 1.2, 0.04, r * 0.11), (0, r * 1.03, hz - r * 0.05), vis, H, bevel=0.01)
        k.box((r * 1.2, r * 0.9, r * 0.35), (0, r * 0.45, hz - r * 0.72), "black", H, bevel=0.02)
        k.box((r * 0.35, 0.03, r * 0.22), (0, r * 1.02, hz + r * 0.4), "orange", H, bevel=0.006)
    else:                            # knight: rounded helmet, visor band, chin guard
        k.sphere(r, (0, 0, hz), "white", H, scale=(0.95, 1.08, 1.0), u=22, v=14)
        k.box((r * 1.6, r * 1.0, r * 0.5), (0, r * 0.55, hz - r * 0.08), "graphite", H, bevel=0.04, segs=3)
        k.box((r * 1.35, 0.03, r * 0.13), (0, r * 1.06, hz - r * 0.06), vis, H, bevel=0.01)
        k.box((r * 1.1, r * 0.8, r * 0.38), (0, r * 0.4, hz - r * 0.66), "black", H, bevel=0.03)
        k.box((r * 0.9, r * 0.35, 0.03), (0, r * 0.6, hz + r * 0.35), "white", H, bevel=0.01, rot=(-0.4, 0, 0))
    if st.crest:
        k.box((r * 0.26, r * 1.9, r * 0.1), (0, 0.0, hz + r * 0.92), "orange", H, bevel=0.01)
    if st.ear_pods and st.helmet in ("knight", "dome"):
        rr = r * (1.12 if st.helmet == "dome" else 1.0)
        for S, s in SIDES:
            k.cyl(rr * 0.34, 0.07, (s * rr * 0.95, 0, hz - rr * 0.05), "black", H, axis="X", verts=16, bevel=0.01)
            k.cyl(rr * 0.22, 0.075, (s * rr * 0.97, 0, hz - rr * 0.05), "white", H, axis="X", verts=14)


# --- class kits --------------------------------------------------------------

def kit_courier(k, rig):
    b = rig.b
    P = rig.heads["Pack"]
    rig_prop(rig, "Cargo", (0, P.y - 0.06, P.z - 0.05), "Pack")
    rig_prop(rig, "Beacon", (0, P.y - 0.32, P.z + 0.42), "Cargo")
    rig_prop(rig, "Antenna", (b.head_r * 0.9, -0.04, b.head_z + b.head_r * 1.3), "Head")
    cy = P.y - 0.32
    k.box((0.58, 0.52, 0.62), (0, cy, P.z + 0.02), "white", "Cargo", bevel=0.03, segs=2)
    k.box((0.61, 0.55, 0.05), (0, cy, P.z + 0.33), "graphite", "Cargo", bevel=0.012)
    k.box((0.61, 0.55, 0.05), (0, cy, P.z - 0.29), "graphite", "Cargo", bevel=0.012)
    for x in (-0.28, 0.28):
        for y in (cy - 0.25, cy + 0.25):
            k.box((0.07, 0.07, 0.66), (x, y, P.z + 0.02), "orange", "Cargo", bevel=0.01)
    k.box((0.4, 0.02, 0.28), (0.0, cy - 0.27, P.z + 0.02), "steel", "Cargo", bevel=0.006)
    k.box((0.12, 0.04, 0.12), (0.0, cy - 0.29, P.z - 0.08), "orange", "Cargo", bevel=0.006)
    k.box((0.36, 0.08, 0.48), (0, P.y - 0.03, P.z), "black", "Cargo", bevel=0.02)
    k.cyl(0.07, 0.04, (0, cy, P.z + 0.37), "black", "Beacon", verts=16)
    k.cyl(0.055, 0.08, (0, cy, P.z + 0.43), "orange", "Beacon", verts=16, bevel=0.01)
    k.cyl(0.004 * 3, 0.5, (b.head_r * 0.9, -0.04, b.head_z + b.head_r * 1.55), "black", "Antenna", verts=6)
    k.sphere(0.02, (b.head_r * 0.9, -0.04, b.head_z + b.head_r * 1.55 + 0.26), "orange", "Antenna", u=8, v=6)


def shield_parts(k, center, w, h, face_yaw, trim="orange", rim="graphite", chevrons=True, mark=False):
    """Tall armoured shield standing upright at `center`, facing +Y rotated by face_yaw."""
    rot = (0, 0, face_yaw)
    outline = [(-w, h * 0.62), (-w * 0.58, h), (w * 0.58, h), (w, h * 0.62), (w, -h * 0.55), (w * 0.45, -h),
               (-w * 0.45, -h), (-w, -h * 0.55)]
    k.slab(outline, 0.06, center, rim, "Shield", bevel=0.012, rot=rot)
    k.slab([(x * 0.86, z * 0.9) for x, z in outline], 0.085, center, "white", "Shield", bevel=0.01, rot=rot)
    fwd = Vector((-math.sin(face_yaw), math.cos(face_yaw), 0.0))
    side = Vector((math.cos(face_yaw), math.sin(face_yaw), 0.0))
    c = Vector(center)
    edge = c + fwd * 0.045
    k.box((0.03, 0.02, h * 1.3), tuple(edge + side * (w * 0.72)), trim, "Shield", bevel=0.004, rot=rot)
    if chevrons:
        for i in range(3):
            for sgn in (-1, 1):
                k.box((w * 0.34, 0.02, 0.03), tuple(edge + side * (sgn * w * 0.15) + Vector((0, 0, -h * 0.55 + i * 0.07))),
                      trim, "Shield", bevel=0.004, rot=(0, sgn * 0.6, face_yaw))
    if mark:
        k.slab([(-0.08, 0.06), (0.08, 0.06), (0.0, -0.08)], 0.1, tuple(c + Vector((0, 0, -0.08))), trim, "Shield",
               bevel=0.004, rot=rot)


def kit_defense(k, rig):
    b = rig.b
    B = b.bulk
    # Shield on the right hand (+X), gatling on the left forearm (-X), as the sheet shows.
    grip = held_point(rig, "shield", "R", 1, rig.heads["HandR"])
    rig_prop(rig, "Shield", rig.heads["HandR"], "HandR")
    with authored_in_pose(k, rig, HOLD_CHAINS["shield"]("R", 1)):
        c = grip + Vector((0.12, 0.12, -0.02))
        shield_parts(k, tuple(c), 0.30, 0.64, face_yaw=-0.35)
        k.box((0.1, 0.12, 0.1), tuple(grip + Vector((0.05, 0.03, 0.0))), "black", "Shield", bevel=0.015)
    # Gatling along the left forearm, authored level and pointing forward in the carry pose.
    chain = HOLD_CHAINS["cannon"]("L", -1)
    M = pose_matrix(rig, chain)
    mid = M @ ((rig.heads["ForearmL"] + rig.heads["HandL"]) / 2)
    rig_prop(rig, "Cannon", rig.heads["ForearmL"], "ForearmL")
    cx, cy, cz = mid.x - 0.02, mid.y + 0.02, mid.z + 0.13
    muzzle = Vector((cx, cy + 0.42, cz))
    rig_prop(rig, "Barrels", tuple(M.inverted() @ muzzle), "Cannon",
             direction=tuple(M.inverted().to_3x3() @ Vector((0, 1, 0))))
    with authored_in_pose(k, rig, chain):
        k.box((0.2, 0.56, 0.2), (cx, cy + 0.04, cz), "white", "Cannon", bevel=0.03)
        k.box((0.22, 0.2, 0.22), (cx, cy - 0.22, cz), "black", "Cannon", bevel=0.02)
        k.box((0.12, 0.36, 0.035), (cx, cy + 0.06, cz + 0.11), "orange", "Cannon", bevel=0.006)
        k.box((0.1, 0.3, 0.1), (cx, cy + 0.0, cz - 0.13), "graphite", "Cannon", bevel=0.01)
        k.box((0.08, 0.14, 0.14), (cx - 0.12, cy - 0.06, cz + 0.02), "graphite", "Cannon", bevel=0.01)
        k.cyl(0.09, 0.1, (cx, cy + 0.34, cz), "black", "Barrels", axis="Y", verts=16, bevel=0.01)
        for i in range(6):
            a = i / 6 * TAU
            k.cyl(0.022, 0.28, (cx + math.cos(a) * 0.052, cy + 0.5, cz + math.sin(a) * 0.052), "graphite",
                  "Barrels", axis="Y", verts=8)
        k.cyl(0.085, 0.035, (cx, cy + 0.6, cz), "steel", "Barrels", axis="Y", verts=16)
        k.cyl(0.085, 0.03, (cx, cy + 0.44, cz), "orange", "Barrels", axis="Y", verts=16)
    # Chest chevrons + back power pack
    for i in range(2):
        k.box((0.14, 0.02, 0.025), (0.14, b.chest_d * 0.52, b.chest_z + 0.12 - i * 0.05), "orange", "Chest",
              bevel=0.004, rot=(0, 0.5, 0))
    P = rig.heads["Pack"]
    k.box((b.chest_w * 0.7, 0.22, b.chest_h * 0.7), (0, P.y - 0.1, P.z), "graphite", "Pack", bevel=0.03)
    k.box((b.chest_w * 0.5, 0.05, 0.08), (0, P.y - 0.22, P.z + 0.1), "orange", "Pack", bevel=0.008)


def kit_engineer(k, rig):
    b = rig.b
    B = b.bulk
    P = rig.heads["Pack"]
    rig_prop(rig, "Reel", (-0.2, P.y - 0.14, P.z + 0.02), "Pack")
    rig_prop(rig, "Drill", (b.arm_x(1, "wrist"), 0.0, b.wrist_z), "ForearmR")
    rig_prop(rig, "Bit", (b.arm_x(1, "wrist"), 0.0, b.wrist_z - 0.2), "Drill")
    rig_prop(rig, "Panel", (b.arm_x(-1, "wrist") - 0.05, 0.1, b.wrist_z - 0.1), "HandL")
    # Backpack utility frame + orange cable reel
    k.box((b.chest_w * 0.78, 0.26, b.chest_h * 0.9), (0.05, P.y - 0.12, P.z + 0.04), "graphite", "Pack", bevel=0.03)
    k.box((0.2, 0.06, 0.12), (0.18, P.y - 0.26, P.z + 0.22), "black", "Pack", bevel=0.01)
    k.cyl(0.03, 0.02, (0.24, P.y - 0.29, P.z + 0.22), "cyan", "Pack", axis="Y", verts=10)
    k.torus(0.13, 0.05, (-0.22, P.y - 0.16, P.z + 0.02), "orange", "Reel", axis="X", major=22, minor=8)
    k.cyl(0.08, 0.16, (-0.22, P.y - 0.16, P.z + 0.02), "black", "Reel", axis="X", verts=14)
    # Hip tool boxes
    for S, s in SIDES:
        x = s * (b.hip_w + 0.16 * B)
        k.box((0.1, 0.22, 0.2), (x, 0.03, b.hip_z - 0.02), "graphite", "Hips", bevel=0.02)
        k.box((0.105, 0.14, 0.08), (x, 0.05, b.hip_z + 0.03), "orange", "Hips", bevel=0.012)
    # Right-arm drill/welder
    dx, dz = b.arm_x(1, "wrist"), b.wrist_z
    k.box((0.2 * B, 0.22 * B, 0.26), (dx + 0.02, 0.0, dz + 0.04), "white", "Drill", bevel=0.035)
    k.box((0.05, 0.16, 0.08), (dx + 0.14, 0.0, dz + 0.08), "orange", "Drill", bevel=0.008)
    k.box((0.16 * B, 0.2 * B, 0.05), (dx + 0.02, 0.0, dz - 0.1), "black", "Drill", bevel=0.01)
    for i, (r, h) in enumerate(((0.09, 0.08), (0.075, 0.07), (0.06, 0.07), (0.045, 0.06))):
        k.cyl(r, h, (dx, 0.0, dz - 0.16 - i * 0.075), "steel" if i % 2 == 0 else "graphite", "Bit", verts=14,
              bevel=0.006)
    k.cyl(0.035, 0.16, (dx, 0.0, dz - 0.5), "steel", "Bit", verts=10, r2=0.01)
    # Left-hand HAB wall panel
    px, py, pz = b.arm_x(-1, "wrist") - 0.12, 0.2, b.wrist_z - 0.18
    k.box((0.1, 0.72, 0.52), (px, py, pz), "white", "Panel", bevel=0.03, segs=2)
    k.box((0.11, 0.66, 0.06), (px, py, pz - 0.14), "orange", "Panel", bevel=0.008)
    k.box((0.115, 0.08, 0.46), (px, py + 0.32, pz), "graphite", "Panel", bevel=0.01)
    k.box((0.115, 0.08, 0.46), (px, py - 0.32, pz), "graphite", "Panel", bevel=0.01)
    k.cyl(0.02, 0.02, (px - 0.06, py + 0.25, pz + 0.18), "cyan", "Panel", axis="X", verts=8)


def kit_geologist(k, rig):
    b = rig.b
    B = b.bulk
    rig_prop(rig, "Mast", (0, -0.02, b.head_z + b.head_r * 1.8), "Head")
    rig_prop(rig, "CoreDrill", (b.arm_x(-1, "wrist"), 0.04, b.wrist_z - 0.06), "HandL")
    # Chest sample rack: five vials in a black frame
    fy = b.chest_d * 0.5 + 0.06
    fz = b.chest_z - 0.04
    k.box((b.chest_w * 0.8, 0.07, 0.07), (0, fy, fz + 0.14), "black", "Chest", bevel=0.012)
    k.box((b.chest_w * 0.8, 0.09, 0.03), (0, fy, fz - 0.1), "black", "Chest", bevel=0.008)
    fills = ("graphite", "orange", "graphite", "graphite", "orange")
    for i in range(5):
        x = (i - 2) * b.chest_w * 0.15
        k.cyl(0.032, 0.2, (x, fy, fz + 0.01), "glass", "Chest", verts=10)
        k.cyl(0.028, 0.13, (x, fy, fz - 0.02), fills[i], "Chest", verts=10)
        k.cyl(0.034, 0.03, (x, fy, fz + 0.12), "orange", "Chest", verts=10)
    # Orange shoulder strap
    k.box((0.06, b.chest_d * 1.02, 0.03), (-0.14, 0.0, b.chest_top - 0.02), "orange", "Chest", bevel=0.006,
          rot=(0, 0.35, 0))
    # Head mast + crystal beacon
    hz = b.head_z + b.head_r * 1.8
    for i in range(3):
        k.cyl(0.03 - i * 0.004, 0.04, (0, -0.02, hz + i * 0.05), "black", "Mast", verts=10)
    k.cyl(0.045, 0.03, (0, -0.02, hz + 0.16), "black", "Mast", verts=12)
    k.sphere(0.055, (0, -0.02, hz + 0.23), "cyan", "Mast", u=8, v=6, scale=(1, 1, 1.15))
    # Core drill (held vertical by a top handle, bit down)
    x, y, z = b.arm_x(-1, "wrist"), 0.06, b.wrist_z - 0.1
    k.cyl(0.03, 0.12, (x, y, z), "graphite", "CoreDrill", verts=10)
    k.cyl(0.095, 0.46, (x, y, z - 0.3), "white", "CoreDrill", verts=16, bevel=0.01)
    for dz in (-0.1, -0.44):
        k.cyl(0.1, 0.045, (x, y, z + dz), "orange", "CoreDrill", verts=16)
    k.box((0.03, 0.05, 0.12), (x, y + 0.09, z - 0.28), "cyan", "CoreDrill", bevel=0.006)
    k.cyl(0.07, 0.16, (x, y, z - 0.6), "steel", "CoreDrill", verts=12, r2=0.05)
    for i in range(6):
        a = i / 6 * TAU
        k.box((0.022, 0.022, 0.06), (x + math.cos(a) * 0.055, y + math.sin(a) * 0.055, z - 0.7), "steel",
              "CoreDrill", bevel=0.004)


def kit_harvester(k, rig):
    b = rig.b
    B = b.bulk
    P = rig.heads["Pack"]
    rig_prop(rig, "Hopper", (0, P.y - 0.05, P.z), "Pack")
    rig_prop(rig, "Scoop", (b.arm_x(1, "wrist"), 0.06, b.wrist_z - 0.06), "HandR")
    hy, hz = P.y - 0.36, P.z + 0.2
    w, d, h = 0.62, 0.56, 0.5
    k.box((w, d, 0.05), (0, hy, hz - h / 2), "black", "Hopper", bevel=0.01)
    for sgn in (-1, 1):
        k.box((0.05, d, h), (sgn * w / 2, hy, hz), "black", "Hopper", bevel=0.01)
        k.box((w, 0.05, h), (0, hy + sgn * d / 2, hz), "black", "Hopper", bevel=0.01)
    k.box((w * 0.7, 0.02, h * 0.55), (0, hy - d / 2 - 0.02, hz - 0.03), "white", "Hopper", bevel=0.008)
    k.box((w + 0.04, 0.05, 0.04), (0, hy - d / 2, hz + h / 2), "orange", "Hopper", bevel=0.006)
    k.box((w + 0.04, 0.05, 0.04), (0, hy + d / 2, hz + h / 2), "orange", "Hopper", bevel=0.006)
    for i, (x, y, r) in enumerate(((-0.14, -0.1, 0.13), (0.12, 0.06, 0.12), (0.0, 0.1, 0.1), (0.18, -0.12, 0.1),
                                   (-0.16, 0.12, 0.09), (0.02, -0.12, 0.11))):
        k.rock(r, (x, hy + y, hz + h / 2 - 0.02), "orange" if i % 3 == 0 else "graphite", "Hopper", seed=i + 1)
    k.box((0.36, 0.12, 0.44), (0, P.y - 0.04, P.z), "graphite", "Hopper", bevel=0.02)
    # Scoop: open bucket, orange interior, white side plates
    x, y, z = b.arm_x(1, "wrist") + 0.04, 0.2, b.wrist_z - 0.12
    k.seg((b.arm_x(1, "wrist"), 0.02, b.wrist_z - 0.06), (x, y - 0.1, z), 0.03, "black", "Scoop")
    k.box((0.34, 0.36, 0.03), (x, y + 0.05, z - 0.12), "orange", "Scoop", bevel=0.008, rot=(-0.25, 0, 0))
    k.box((0.34, 0.03, 0.26), (x, y - 0.12, z), "orange", "Scoop", bevel=0.008)
    for sgn in (-1, 1):
        k.slab([(-0.18, 0.13), (0.2, -0.02), (0.2, -0.14), (-0.18, -0.14)], 0.03, (x + sgn * 0.17, y + 0.02, z),
               "white", "Scoop", bevel=0.006, rot=(0, 0, math.pi / 2))
    k.box((0.36, 0.05, 0.04), (x, y + 0.23, z - 0.16), "steel", "Scoop", bevel=0.006)


def kit_medic(k, rig):
    b = rig.b
    P = rig.heads["Pack"]
    rig_prop(rig, "Kit", (b.hip_w + 0.18, 0.04, b.hip_z - 0.06), "Hips")
    # Cyan cross on the chest
    cy = b.chest_d * 0.5 + 0.03
    k.box((0.07, 0.03, 0.2), (0, cy, b.chest_z + 0.05), "cyan", "Chest", bevel=0.006)
    k.box((0.2, 0.03, 0.07), (0, cy, b.chest_z + 0.05), "cyan", "Chest", bevel=0.006)
    k.box((b.chest_w * 0.7, 0.02, 0.02), (0, cy - 0.005, b.chest_z - 0.1), "orange", "Chest", bevel=0.004)
    # Folded stretcher on the back, carried diagonally like the sheet
    rot = (0, 0.42, 0)
    for sgn in (-1, 1):
        k.box((0.04, 0.05, 0.78), (sgn * 0.1 + 0.04, P.y - 0.1, P.z + 0.04), "black", "Pack", bevel=0.008,
              rot=rot)
    k.box((0.2, 0.04, 0.6), (0.04, P.y - 0.13, P.z + 0.04), "graphite", "Pack", bevel=0.008, rot=rot)
    k.box((0.2, 0.05, 0.26), (0.02, P.y - 0.16, P.z - 0.02), "white", "Pack", bevel=0.01, rot=rot)
    k.box((0.07, 0.02, 0.07), (0.02, P.y - 0.19, P.z - 0.02), "cyan", "Pack", bevel=0.004, rot=rot)
    # Kit sphere on right hip with IV line
    kx, ky, kz = b.hip_w + 0.2, 0.04, b.hip_z - 0.08
    k.sphere(0.13, (kx, ky, kz), "white", "Kit", u=18, v=12)
    k.torus(0.128, 0.012, (kx, ky, kz - 0.02), "orange", "Kit", axis="Z", major=20, minor=6)
    k.cyl(0.05, 0.02, (kx, ky + 0.125, kz + 0.01), "cyan", "Kit", axis="Y", verts=12)
    k.box((0.05, 0.08, 0.1), (kx - 0.1, ky, kz + 0.06), "black", "Kit", bevel=0.01)
    k.seg((kx, ky + 0.08, kz - 0.1), (kx - 0.02, ky + 0.12, kz - 0.32), 0.006, "cyan", "Kit", verts=6)
    k.cyl(0.018, 0.05, (kx - 0.02, ky + 0.12, kz - 0.35), "white", "Kit", verts=8)


def kit_scout(k, rig):
    b = rig.b
    rig_prop(rig, "Beacon", (b.arm_x(-1, "shoulder") - 0.02, 0, b.shoulder_z + 0.12), "UpperArmL")
    rig_prop(rig, "Antenna", (b.head_r * 0.7, -0.05, b.head_z + b.head_r * 1.5), "Head")
    bx, bz = b.arm_x(-1, "shoulder") - 0.02, b.shoulder_z + 0.12
    k.cyl(0.035, 0.05, (bx, 0, bz), "black", "Beacon", verts=12)
    k.cyl(0.05, 0.09, (bx, 0, bz + 0.07), "orange", "Beacon", verts=16, bevel=0.01)
    ax = b.head_r * 0.7
    k.cyl(0.012, 0.55, (ax, -0.05, b.head_z + b.head_r * 1.5 + 0.27), "black", "Antenna", verts=6, r2=0.006)
    # Chest camera (range finder)
    cy = b.chest_d * 0.5 + 0.05
    k.box((0.16, 0.1, 0.09), (-0.06, cy, b.chest_top - 0.08), "black", "Chest", bevel=0.015)
    k.cyl(0.035, 0.05, (-0.12, cy + 0.06, b.chest_top - 0.08), "graphite", "Chest", axis="Y", verts=14)
    k.cyl(0.022, 0.02, (-0.12, cy + 0.09, b.chest_top - 0.08), "cyan", "Chest", axis="Y", verts=12)


def kit_sentinel(k, rig):
    b = rig.b
    B = b.bulk
    P = rig.heads["Pack"]
    rig_prop(rig, "Turret", (0, P.y + 0.08, b.chest_top + 0.12), "Chest")
    rig_prop(rig, "GunL", (-0.2, P.y + 0.12, b.chest_top + 0.28), "Turret")
    rig_prop(rig, "GunR", (0.2, P.y + 0.12, b.chest_top + 0.28), "Turret")
    rig_prop(rig, "Shield", rig.heads["ForearmL"], "ForearmL")
    # Orange chevron line across the chest
    for sgn in (-1, 1):
        k.box((b.chest_w * 0.52, 0.02, 0.025), (sgn * b.chest_w * 0.24, b.chest_d * 0.52 + 0.01, b.chest_z + 0.02),
              "orange", "Chest", bevel=0.004, rot=(0, -sgn * 0.22, 0))
    # Back turret yoke + twin cannons
    ty, tz = P.y + 0.08, b.chest_top + 0.12
    k.box((0.32, 0.3, 0.16), (0, ty, tz), "graphite", "Turret", bevel=0.03)
    k.cyl(0.1, 0.1, (0, ty, tz + 0.1), "black", "Turret", verts=16)
    k.box((0.52, 0.18, 0.1), (0, ty + 0.04, tz + 0.16), "white", "Turret", bevel=0.02)
    for S, sgn in (("L", -1), ("R", 1)):
        gx, gy, gz = sgn * 0.2, ty + 0.12, tz + 0.28
        g = f"Gun{S}"
        k.box((0.14, 0.34, 0.14), (gx, gy, gz), "white", g, bevel=0.03)
        k.cyl(0.06, 0.26, (gx, gy + 0.28, gz), "graphite", g, axis="Y", verts=14)
        k.cyl(0.066, 0.04, (gx, gy + 0.33, gz), "orange", g, axis="Y", verts=14)
        k.cyl(0.07, 0.05, (gx, gy + 0.43, gz), "black", g, axis="Y", verts=14)
    # Tall octagonal shield strapped to the left forearm, upright in the carry pose
    grip = held_point(rig, "shield", "L", -1, rig.heads["HandL"])
    with authored_in_pose(k, rig, HOLD_CHAINS["shield"]("L", -1)):
        c = grip + Vector((-0.13, 0.1, 0.06))
        shield_parts(k, tuple(c), 0.26, 0.62, face_yaw=0.4, rim="black", chevrons=False, mark=True)


def kit_surveyor(k, rig):
    b = rig.b
    P = rig.heads["Pack"]
    rig_prop(rig, "Dish", (0.12, P.y - 0.14, P.z + 0.72), "Pack")
    rig_prop(rig, "Staff", (b.arm_x(1, "wrist"), 0.04, b.wrist_z - 0.06), "HandR")
    # Backpack + lattice mast
    k.box((0.34, 0.2, 0.48), (0.02, P.y - 0.1, P.z - 0.02), "black", "Pack", bevel=0.02)
    k.box((0.08, 0.22, 0.4), (0.2, P.y - 0.1, P.z - 0.04), "orange", "Pack", bevel=0.01)
    for dx in (-0.04, 0.04):
        for dy in (-0.04, 0.04):
            k.cyl(0.008, 0.62, (0.12 + dx, P.y - 0.14 + dy, P.z + 0.42), "steel", "Pack", verts=6)
    for i in range(5):
        k.box((0.1, 0.1, 0.012), (0.12, P.y - 0.14, P.z + 0.16 + i * 0.12), "steel", "Pack", bevel=0.002)
    dz = P.z + 0.72
    k.cyl(0.02, 0.1, (0.12, P.y - 0.14, dz), "graphite", "Dish", verts=8)
    k.cyl(0.19, 0.04, (0.12, P.y - 0.1, dz + 0.1), "white", "Dish", axis="Y", verts=24, r2=0.05,
          rot=(0.6, 0, 0))
    k.torus(0.19, 0.012, (0.12, P.y - 0.1, dz + 0.1), "orange", "Dish", axis="Y", major=24, minor=6)
    k.seg((0.12, P.y - 0.1, dz + 0.1), (0.12, P.y + 0.02, dz + 0.2), 0.008, "black", "Dish", verts=6)
    # Survey staff
    x, y, z = b.arm_x(1, "wrist"), 0.04, b.wrist_z - 0.06
    k.cyl(0.02, 1.62, (x, y + 0.04, z - 0.12), "graphite", "Staff", verts=10, rot=(0.0, 0.0, 0.0))
    k.cyl(0.03, 0.04, (x, y + 0.04, z + 0.5), "orange", "Staff", verts=12)
    k.cyl(0.024, 0.12, (x, y + 0.04, z + 0.58), "black", "Staff", verts=10)
    k.cyl(0.026, 0.08, (x, y + 0.04, z - 0.9), "steel", "Staff", verts=10, r2=0.012)


def kit_terraformer(k, rig):
    b = rig.b
    B = b.bulk
    P = rig.heads["Pack"]
    rig_prop(rig, "Blade", (b.arm_x(1, "wrist"), 0.06, b.wrist_z - 0.06), "HandR")
    rig_prop(rig, "Rake", (b.arm_x(-1, "wrist"), 0.06, b.wrist_z - 0.06), "HandL")
    # Seed tanks
    for sgn in (-1, 1):
        x, y, z = sgn * 0.24, P.y - 0.12, P.z + 0.2
        k.cyl(0.14, 0.5, (x, y, z), "black", "Pack", verts=18, bevel=0.02)
        k.cyl(0.145, 0.03, (x, y, z + 0.1), "graphite", "Pack", verts=18)
        k.cyl(0.05, 0.05, (x, y, z + 0.28), "orange", "Pack", verts=12)
    k.box((0.3, 0.16, 0.36), (0, P.y - 0.06, P.z), "graphite", "Pack", bevel=0.02)
    # Chest core light + tabard
    k.cyl(0.045, 0.03, (0, b.chest_d * 0.5 + 0.02, b.chest_z - 0.02), "orange", "Chest", axis="Y", verts=14)
    k.box((0.34, 0.03, 0.34), (0, 0.17 * B, b.hip_z - 0.12), "black", "Hips", bevel=0.01, taper=(0.85, 1.0))
    k.slab([(0, 0.07), (0.05, 0.0), (0, -0.06), (-0.05, 0.0)], 0.02, (0, 0.17 * B + 0.02, b.hip_z - 0.08),
           "orange", "Hips", bevel=0.003)
    # Spade-blade in the right hand, point down-forward
    x, y, z = b.arm_x(1, "wrist") + 0.02, 0.1, b.wrist_z - 0.06
    k.cyl(0.028, 0.3, (x, y, z), "black", "Blade", verts=10)
    k.box((0.2, 0.06, 0.05), (x, y, z - 0.16), "graphite", "Blade", bevel=0.01)
    blade = [(-0.1, 0.0), (0.1, 0.0), (0.12, -0.3), (0.0, -0.62), (-0.12, -0.3)]
    k.slab(blade, 0.04, (x, y, z - 0.18), "white", "Blade", bevel=0.008, rot=(0, 0, math.pi / 2))
    k.slab([(-0.03, -0.05), (0.03, -0.05), (0.0, -0.5)], 0.05, (x, y, z - 0.18), "steel", "Blade", bevel=0.004,
           rot=(0, 0, math.pi / 2))
    k.box((0.03, 0.05, 0.05), (x, y + 0.03, z - 0.24), "orange", "Blade", bevel=0.004)
    # Rake in the left hand
    x, y, z = b.arm_x(-1, "wrist"), 0.1, b.wrist_z - 0.06
    k.cyl(0.022, 0.5, (x, y + 0.06, z - 0.16), "graphite", "Rake", verts=10, rot=(0.25, 0, 0))
    rz = z - 0.44
    k.box((0.14, 0.4, 0.16), (x, y + 0.14, rz), "black", "Rake", bevel=0.01)
    k.box((0.12, 0.36, 0.12), (x, y + 0.14, rz), "orange", "Rake", bevel=0.006)
    for i in range(7):
        k.cyl(0.008, 0.1, (x, y + 0.14 - 0.16 + i * 0.053, rz - 0.12), "steel", "Rake", verts=6, r2=0.003)


def rig_prop(rig: HeroRig, name, head, parent, direction=None):
    """Add a prop bone after the base skeleton (armature must still be in edit mode).

    Prop bones point up like every other bone unless `direction` is given (spinners such as
    gatling barrels point along their spin axis so ry spins them in place)."""
    rig.joint(name, head, parent, 0.08)
    if direction is not None:
        eb = rig.obj.data.edit_bones[name]
        eb.tail = Vector(head) + Vector(direction).normalized() * 0.08


HOLD_CHAINS = {
    "shield": lambda S, s: [(f"Shoulder{S}", (0, 0, 0)), (f"UpperArm{S}", (0.35, 0, s * 0.12)),
                            (f"Forearm{S}", (1.0, 0, 0))],
    "cannon": lambda S, s: [(f"Shoulder{S}", (0, 0, 0)), (f"UpperArm{S}", (0.25, 0, s * 0.08)),
                            (f"Forearm{S}", (1.35, 0, 0))],
}


def held_point(rig, mode, S, s, bone_head):
    return pose_matrix(rig, HOLD_CHAINS[mode](S, s)) @ Vector(bone_head)


CLASSES = {
    "Courier": (Style("slim", helmet="knight", visor="cyan"), kit_courier),
    "Defense": (Style("heavy", helmet="angular", visor="defense", heavy_hands=True), kit_defense),
    "Engineer": (Style("medium", helmet="knight", chest_band=True, heavy_hands=True, pad_mark="orange"), kit_engineer),
    "Geologist": (Style("medium", helmet="knight", crest=True, pad_mark="orange"), kit_geologist),
    "Harvester": (Style("medium", helmet="knight", limb="graphite", heavy_hands=True), kit_harvester),
    "Medic": (Style("slim", helmet="dome"), kit_medic),
    "Scout": (Style("slim", helmet="knight"), kit_scout),
    "Sentinel": (Style("heavy", helmet="angular", heavy_hands=True, pad_mark="orange"), kit_sentinel),
    "Surveyor": (Style("slim", helmet="round3", ear_pods=False), kit_surveyor),
    "Terraformer": (Style("heavy", helmet="knight", heavy_hands=True), kit_terraformer),
}

# Gameplay move speeds (Resources/DemoContent/Specialists/*.asset) the jog is authored for.
GAME_SPEED = {
    "Courier": 4.3, "Defense": 3.0, "Engineer": 3.1, "Geologist": 3.3, "Harvester": 3.4,
    "Medic": 3.6, "Scout": 4.4, "Sentinel": 2.9, "Surveyor": 4.1, "Terraformer": 3.0,
}


# ---------------------------------------------------------------------------
# animation
# ---------------------------------------------------------------------------

class Pose(dict):
    """bone -> [rx, ry, rz, x, y, z] where x/y/z are world-axis offsets (converted on key)."""

    def r(self, bone, x=0.0, y=0.0, z=0.0):
        v = self.setdefault(bone, [0.0] * 6)
        v[0] += x
        v[1] += y
        v[2] += z
        return self

    def t(self, bone, x=0.0, y=0.0, z=0.0):
        v = self.setdefault(bone, [0.0] * 6)
        v[3] += x
        v[4] += y
        v[5] += z
        return self


def bake(arm, name, frames, fn, cyclic=True):
    if arm.animation_data is None:
        arm.animation_data_create()
    act = bpy.data.actions.new(name)
    arm.animation_data.action = act
    slot = act.slots.new(id_type="OBJECT", name=arm.name)
    arm.animation_data.action_slot = slot
    bpy.context.view_layer.objects.active = arm
    if bpy.context.mode != "POSE":
        bpy.ops.object.mode_set(mode="POSE")
    last = frames + 1 if cyclic else frames
    for f in range(1, last + 1):
        t = (f - 1) / frames if cyclic else (f - 1) / max(frames - 1, 1)
        pose = fn(t)
        for pb in arm.pose.bones:
            v = pose.get(pb.name)
            if v is None:
                pb.rotation_euler = (0.0, 0.0, 0.0)
                pb.location = (0.0, 0.0, 0.0)
            else:
                pb.rotation_euler = (v[0], v[1], v[2])
                # bone local axes at rest: X = world X, Y = world Z, Z = world -Y
                pb.location = (v[3], v[5], -v[4])
            pb.keyframe_insert("rotation_euler", frame=f)
            pb.keyframe_insert("location", frame=f)
    bpy.ops.object.mode_set(mode="OBJECT")
    return act


def leg_ik(b: Build, s, hip, foot, toe=0.0):
    """hip/foot are world offsets from rest. Returns (thigh rx, rz), shin rx, foot (rx, rz)."""
    a, c = b.thigh, b.shin
    hx, hy, hz = s * b.hip_w + hip[0], hip[1], b.hip_z + hip[2]
    fx, fy, fz = s * b.hip_w + foot[0], foot[1], b.ankle_z + foot[2]
    vx, vy, vz = fx - hx, fy - hy, fz - hz
    phi = math.atan2(vx, -vz)
    down = math.hypot(vx, vz)
    L = clamp(math.hypot(vy, down), (a + c) * 0.35, (a + c) * 0.9995)
    theta = math.atan2(vy, down)
    knee = math.pi - math.acos(clamp((a * a + c * c - L * L) / (2 * a * c), -1, 1))
    alpha = math.acos(clamp((a * a + L * L - c * c) / (2 * a * L), -1, 1))
    th = theta + alpha
    sh = -knee
    return (th, phi), sh, (-(th + sh) + toe, -phi)


def apply_legs(pose: Pose, b: Build, hip, feet, toes=(0.0, 0.0)):
    for (S, s), foot, toe in zip(SIDES, feet, toes):
        (tx, tz), sx, (fx, fz) = leg_ik(b, s, hip, foot, toe)
        pose.r(f"Thigh{S}", x=tx, z=tz)
        pose.r(f"Shin{S}", x=sx)
        pose.r(f"Foot{S}", x=fx, z=fz)


def arms_rest(pose: Pose, b: Build, holds: dict):
    """Relaxed arm carry. holds: side -> 'free' | 'tool' | 'shield' | 'panel' | 'staff'."""
    for S, s in SIDES:
        mode = holds.get(S, "free")
        spread = 0.06 * b.bulk
        if mode == "shield":
            pose.r(f"UpperArm{S}", x=0.35, z=s * 0.12)
            pose.r(f"Forearm{S}", x=1.0)
        elif mode == "cannon":
            pose.r(f"UpperArm{S}", x=0.25, z=s * 0.08)
            pose.r(f"Forearm{S}", x=1.35)
            pose.r(f"Hand{S}", x=-0.3)
        elif mode == "panel":
            pose.r(f"UpperArm{S}", x=0.05, z=s * 0.18)
            pose.r(f"Forearm{S}", x=0.25)
        elif mode == "staff":
            pose.r(f"UpperArm{S}", x=0.2, z=s * 0.12)
            pose.r(f"Forearm{S}", x=0.7)
            pose.r(f"Hand{S}", x=-0.7)
        elif mode == "tool":
            pose.r(f"UpperArm{S}", x=0.1, z=s * spread * 1.6)
            pose.r(f"Forearm{S}", x=0.35)
        else:
            pose.r(f"UpperArm{S}", x=0.06, z=s * spread)
            pose.r(f"Forearm{S}", x=0.22)


HOLDS = {
    "Courier": {},
    "Defense": {"R": "shield", "L": "cannon"},
    "Engineer": {"R": "tool", "L": "panel"},
    "Geologist": {"L": "tool"},
    "Harvester": {"R": "tool"},
    "Medic": {},
    "Scout": {},
    "Sentinel": {"L": "shield"},
    "Surveyor": {"R": "staff"},
    "Terraformer": {"R": "tool", "L": "tool"},
}


def idle_clip(cls, b: Build, props):
    heavy = b.name == "heavy"
    amp = 1.25 if heavy else 1.0

    def fn(t):
        p = Pose()
        breath = wave(t, 2)
        shift = wave(t, 1) * 0.022 * amp
        hip = (shift, 0.0, -0.035 * amp + breath * 0.006)
        apply_legs(p, b, hip, [(0.0, 0.0, 0.0), (0.0, 0.0, 0.0)])
        p.t("Hips", x=hip[0], z=hip[2])
        p.r("Hips", z=-shift * 1.2)
        p.r("Spine", x=0.02 + breath * 0.015, z=shift * 1.6)
        p.r("Chest", x=breath * 0.02)
        look = keys(t, [(0, 0), (0.25, 0), (0.35, 0.35), (0.6, 0.35), (0.7, -0.15), (0.9, -0.15), (1.0, 0)])
        p.r("Head", y=look * 0.8, x=-0.03 + breath * 0.01)
        p.r("Neck", y=look * 0.3)
        arms_rest(p, b, HOLDS[cls])
        for S, s in SIDES:
            p.r(f"UpperArm{S}", x=breath * 0.02, z=s * breath * 0.012)
            p.r(f"Shoulder{S}", z=-s * breath * 0.01)
        p.r("Pack", x=-breath * 0.01)
        if "Antenna" in props:
            p.r("Antenna", x=wave(t, 2, 0.2) * 0.12, z=wave(t, 1, 0.1) * 0.08)
        if "Dish" in props:
            p.r("Dish", y=wave(t, 1) * 0.6)
        if "Barrels" in props:
            p.r("Barrels", y=t * TAU * 0.0)
        if "Turret" in props:
            p.r("Turret", y=look * 0.9)
        if "Reel" in props:
            p.r("Reel", x=wave(t, 1) * 0.3)
        if "Beacon" in props and cls == "Courier":
            p.r("Beacon", y=t * TAU * 2)
        return p

    return fn


def walk_params(cls, b: Build):
    v = GAME_SPEED[cls]
    d = b.stride_duty
    D = 0.86 * b.leg
    T = D / (v * d)
    frames = max(10, int(round(T * FPS)))
    T = frames / FPS
    v_ref = D / (d * T)
    return frames, D, d, v_ref


def walk_clip(cls, b: Build, props):
    frames, D, duty, v_ref = walk_params(cls, b)
    heavy = b.name == "heavy"
    lift = (0.16 if heavy else 0.19) * b.leg
    crouch = 0.075 * b.leg
    holds = HOLDS[cls]

    def foot_path(p):
        p %= 1.0
        if p < duty:
            u = p / duty
            return D * 0.5 - D * u, 0.0, 0.0
        u = (p - duty) / (1 - duty)
        y = -D * 0.5 + D * smoother(u)
        z = lift * math.sin(math.pi * u) ** 1.3
        toe = -0.5 * math.sin(math.pi * min(u * 1.6, 1.0)) if u < 0.62 else 0.25 * math.sin(math.pi * (u - 0.62) / 0.38)
        return y, z, toe

    def fn(t):
        p = Pose()
        yl, zl, tl = foot_path(t)
        yr, zr, tr = foot_path(t + 0.5)
        bob = math.cos(t * TAU * 2) * 0.028 * b.leg
        sway = math.sin(t * TAU) * 0.03 * (1.4 if heavy else 1.0)
        hip = (sway, 0.02, -crouch - bob)
        stance_l = (t % 1.0) < duty
        stance_r = ((t + 0.5) % 1.0) < duty
        apply_legs(p, b, hip, [(-sway * 0.4, yl, zl), (-sway * 0.4, yr, zr)],
                   toes=(tl if not stance_l else 0.0, tr if not stance_r else 0.0))
        p.t("Hips", x=hip[0], y=hip[1], z=hip[2])
        twist = math.sin(t * TAU) * 0.16
        p.r("Hips", y=twist, z=-sway * 1.4)
        p.r("Spine", x=-0.14 - (0.04 if heavy else 0.0), y=-twist * 1.4, z=sway * 1.8)
        p.r("Chest", x=-0.02 + bob * 0.8, y=-twist * 0.6)
        p.r("Head", x=0.12 + bob * 1.2, y=twist * 0.6)
        arms_rest(p, b, holds)
        for S, s in SIDES:
            mode = holds.get(S, "free")
            phase = 0.0 if S == "L" else 0.5
            swing = math.sin((t + phase) * TAU)   # opposite the same-side leg
            if mode in ("free", "tool"):
                if mode == "free":
                    p.r(f"UpperArm{S}", x=swing * 0.55)
                    p.r(f"Forearm{S}", x=0.45 + max(0.0, swing) * 0.55)
                else:
                    p.r(f"UpperArm{S}", x=swing * 0.22)
                    p.r(f"Forearm{S}", x=0.15 + max(0.0, swing) * 0.15)
            elif mode == "staff":
                p.r(f"UpperArm{S}", x=swing * 0.25)
            else:
                p.r(f"UpperArm{S}", x=swing * 0.12)
            p.r(f"Shoulder{S}", x=-swing * 0.05)
        p.r("Pack", x=bob * 1.5)
        for prop in ("Antenna",):
            if prop in props:
                p.r(prop, x=-0.25 + math.sin(t * TAU * 2 + 1.0) * 0.18)
        if "Cargo" in props:
            p.r("Cargo", x=math.sin(t * TAU * 2 + 0.6) * 0.04)
        if "Hopper" in props:
            p.r("Hopper", x=math.sin(t * TAU * 2 + 0.6) * 0.035)
        if "Kit" in props:
            p.r("Kit", x=math.sin(t * TAU * 2 + 0.8) * 0.14)
        if "Dish" in props:
            p.r("Dish", x=math.sin(t * TAU * 2 + 0.5) * 0.08)
        if "Beacon" in props and cls == "Courier":
            p.r("Beacon", y=t * TAU * 2)
        if "Turret" in props:
            p.r("Turret", x=math.sin(t * TAU * 2 + 0.7) * 0.03)
        return p

    return frames, v_ref, fn


def strike_clip(cls, b: Build, props):
    """0-0.3 anticipation, 0.3-0.45 hit, 0.45-1 recover. Returns fn."""
    holds = HOLDS[cls]

    def base(t, crouch=0.06, lunge=0.0):
        p = Pose()
        c = keys(t, [(0, 0.03), (0.3, crouch), (0.42, crouch * 0.6), (1.0, 0.03)]) * b.leg
        ly = keys(t, [(0, 0), (0.3, -0.03), (0.42, lunge), (0.7, lunge), (1.0, 0)])
        hip = (0.0, ly, -c)
        # Right foot steps forward on the hit for lunge moves
        step = keys(t, [(0, 0), (0.3, 0), (0.42, lunge * 1.8), (0.75, lunge * 1.8), (1, 0)])
        liftf = keys(t, [(0, 0), (0.3, 0), (0.36, 0.08 if lunge else 0), (0.42, 0), (1, 0)])
        apply_legs(p, b, hip, [(0, -lunge * 0.6 * smooth(t / 0.42) if t < 0.42 else -lunge * 0.6 * (1 - smooth((t - 0.7) / 0.3)), 0),
                               (0, step, liftf)])
        p.t("Hips", y=hip[1], z=hip[2])
        arms_rest(p, b, holds)
        return p

    if cls in ("Defense",):
        def fn(t):
            p = base(t, crouch=0.09)
            brace = keys(t, [(0, 0), (0.25, 1), (0.85, 1), (1, 0)])
            p.r("Spine", x=-0.1 * brace, y=0.25 * brace)
            p.r("UpperArmR", x=0.3 * brace, z=-0.05 * brace)
            p.r("ForearmR", x=0.25 * brace)
            p.r("UpperArmL", x=0.4 * brace, z=-0.1 * brace)
            p.r("ForearmL", x=-0.4 * brace)
            kick = sum(max(0.0, 1 - abs(t - c) / 0.05) for c in (0.35, 0.47, 0.59, 0.71))
            p.r("UpperArmL", x=-0.08 * kick)
            p.r("Spine", x=0.03 * kick)
            p.r("Barrels", y=smooth((t - 0.28) / 0.6) * TAU * 3)
            p.r("Head", x=0.05 * brace)
            return p
    elif cls == "Sentinel":
        def fn(t):
            p = base(t, crouch=0.08)
            brace = keys(t, [(0, 0), (0.25, 1), (0.85, 1), (1, 0)])
            p.r("Spine", x=0.05 * brace)
            p.r("UpperArmL", x=0.4 * brace, z=0.2 * brace)
            p.r("ForearmL", x=0.3 * brace)
            p.r("Turret", x=0.12 * brace)
            for g, c in (("GunL", 0.36), ("GunR", 0.5), ("GunL", 0.64), ("GunR", 0.78)):
                k = max(0.0, 1 - abs(t - c) / 0.06)
                p.t(g, y=-0.07 * k)
            return p
    elif cls == "Engineer":
        def fn(t):
            p = base(t, lunge=0.1)
            wind = keys(t, [(0, 0), (0.3, -0.7), (0.42, 1.25), (0.7, 1.1), (1, 0)])
            p.r("UpperArmR", x=wind, z=-0.1 * max(wind, 0))
            p.r("ForearmR", x=keys(t, [(0, 0), (0.3, 1.1), (0.42, 0.1), (0.7, 0.2), (1, 0)]))
            p.r("Spine", y=keys(t, [(0, 0), (0.3, -0.35), (0.42, 0.3), (0.7, 0.2), (1, 0)]),
                x=keys(t, [(0, 0), (0.3, 0.05), (0.42, -0.18), (1, 0)]))
            p.r("Bit", y=smooth((t - 0.35) / 0.4) * TAU * 4)
            return p
    elif cls == "Geologist":
        def fn(t):
            p = base(t, crouch=0.11)
            lift = keys(t, [(0, 0), (0.3, 1.0), (0.42, -0.2), (0.7, -0.15), (1, 0)])
            p.r("UpperArmL", x=0.9 * max(lift, 0) - 0.1 * min(lift, 0) * -1, z=-0.1)
            p.r("ForearmL", x=0.6 * max(lift, 0))
            p.r("UpperArmR", x=0.6 * max(lift, 0), z=-0.25 * max(lift, 0))
            p.r("ForearmR", x=0.9 * max(lift, 0))
            p.r("Spine", x=keys(t, [(0, 0), (0.3, 0.1), (0.42, -0.3), (0.7, -0.25), (1, 0)]))
            p.r("CoreDrill", y=smooth((t - 0.35) / 0.5) * TAU * 3)
            return p
    elif cls == "Harvester":
        def fn(t):
            p = base(t, crouch=0.1, lunge=0.08)
            sw = keys(t, [(0, 0), (0.3, -0.9), (0.45, 1.3), (0.7, 1.1), (1, 0)])
            p.r("UpperArmR", x=sw, z=0.15)
            p.r("ForearmR", x=keys(t, [(0, 0), (0.3, 0.2), (0.45, 0.9), (1, 0)]))
            p.r("HandR", x=keys(t, [(0, 0), (0.3, 0.4), (0.45, -0.6), (1, 0)]))
            p.r("Spine", x=keys(t, [(0, 0), (0.3, -0.25), (0.45, 0.1), (1, 0)]) * -1,
                y=keys(t, [(0, 0), (0.3, -0.3), (0.45, 0.25), (1, 0)]))
            return p
    elif cls == "Terraformer":
        def fn(t):
            p = base(t, crouch=0.1, lunge=0.1)
            up = keys(t, [(0, 0), (0.3, 1.0), (0.42, -0.25), (0.7, -0.2), (1, 0)])
            p.r("UpperArmR", x=2.4 * max(up, 0) + 0.9 * min(up, 0) * -1, z=0.1)
            p.r("ForearmR", x=0.9 * max(up, 0) + 0.3)
            p.r("HandR", x=-0.5 * max(up, 0) + 0.6 * max(-up, 0) * 4)
            p.r("Spine", x=keys(t, [(0, 0), (0.3, 0.2), (0.42, -0.35), (0.7, -0.3), (1, 0)]),
                y=keys(t, [(0, 0), (0.3, 0.25), (0.42, -0.1), (1, 0)]))
            p.r("UpperArmL", x=keys(t, [(0, 0), (0.3, -0.3), (0.42, 0.2), (1, 0)]))
            return p
    elif cls == "Surveyor":
        def fn(t):
            p = base(t, lunge=0.12)
            th = keys(t, [(0, 0), (0.3, -0.4), (0.42, 1.2), (0.7, 1.05), (1, 0)])
            p.r("UpperArmR", x=th)
            p.r("ForearmR", x=keys(t, [(0, 0), (0.3, 0.9), (0.42, -0.4), (1, 0)]))
            p.r("HandR", x=keys(t, [(0, 0), (0.42, 0.9), (0.7, 0.9), (1, 0)]))
            p.r("Spine", y=keys(t, [(0, 0), (0.3, -0.3), (0.42, 0.25), (1, 0)]))
            return p
    elif cls == "Medic":
        def fn(t):
            p = base(t, lunge=0.08)
            push = keys(t, [(0, 0), (0.3, -0.2), (0.42, 1.5), (0.7, 1.4), (1, 0)])
            p.r("UpperArmR", x=push, z=-0.1 * max(push, 0))
            p.r("ForearmR", x=keys(t, [(0, 0), (0.3, 1.4), (0.42, 0.05), (0.7, 0.1), (1, 0)]))
            p.r("HandR", x=-0.9 * max(push, 0) / 1.5)
            p.r("UpperArmL", x=-0.3 * max(push, 0) / 1.5)
            p.r("Spine", y=keys(t, [(0, 0), (0.3, -0.25), (0.42, 0.2), (1, 0)]))
            return p
    else:   # Scout, Courier: jab / shoulder shove
        def fn(t):
            p = base(t, lunge=0.14)
            jab = keys(t, [(0, 0), (0.3, -0.3), (0.4, 1.45), (0.62, 1.3), (1, 0)])
            p.r("UpperArmR", x=jab)
            p.r("ForearmR", x=keys(t, [(0, 0), (0.3, 1.5), (0.4, 0.1), (0.62, 0.15), (1, 0)]))
            p.r("UpperArmL", x=keys(t, [(0, 0), (0.3, 0.6), (0.4, -0.4), (1, 0)]), z=-0.1)
            p.r("ForearmL", x=keys(t, [(0, 0), (0.3, 1.4), (0.4, 1.0), (1, 0)]))
            p.r("Spine", y=keys(t, [(0, 0), (0.3, -0.4), (0.4, 0.35), (0.62, 0.3), (1, 0)]),
                x=keys(t, [(0, 0), (0.4, -0.12), (1, 0)]))
            return p
    return fn


def work_clip(cls, b: Build, props):
    holds = HOLDS[cls]

    def base(t, crouch=0.08, lean=-0.2):
        p = Pose()
        c = crouch * b.leg + wave(t, 2) * 0.01
        apply_legs(p, b, (0, 0.02, -c), [(0, 0.12, 0), (0, -0.1, 0)])
        p.t("Hips", y=0.02, z=-c)
        p.r("Spine", x=lean)
        arms_rest(p, b, holds)
        return p

    if cls == "Engineer":
        def fn(t):
            p = base(t, lean=-0.15)
            p.r("UpperArmR", x=1.05 + wave(t, 4) * 0.05, z=-0.25)
            p.r("ForearmR", x=0.35)
            p.r("UpperArmL", x=0.5, z=-0.1)
            p.r("ForearmL", x=0.4)
            p.r("Bit", y=t * TAU * 8)
            p.r("Head", x=0.2)
            return p
    elif cls in ("Geologist",):
        def fn(t):
            p = base(t, crouch=0.14, lean=-0.3)
            pump = abs(wave(t, 2))
            # Both hands on the core drill, bit kept vertical into the regolith.
            p.r("UpperArmL", x=0.35 + pump * 0.08, z=0.05)
            p.r("ForearmL", x=0.4)
            p.r("HandL", x=-0.75 - pump * 0.08 - 0.35)
            p.r("UpperArmR", x=0.55 + pump * 0.08, z=-0.35)
            p.r("ForearmR", x=0.75)
            p.r("CoreDrill", y=t * TAU * 6)
            p.r("Head", x=0.35)
            return p
    elif cls == "Harvester":
        def fn(t):
            p = base(t, crouch=0.1, lean=-0.25)
            sw = keys(t, [(0, -0.2), (0.35, 1.2), (0.55, 1.4), (0.8, 0.2), (1.0, -0.2)])
            p.r("UpperArmR", x=sw, z=0.15)
            p.r("ForearmR", x=0.6)
            p.r("HandR", x=keys(t, [(0, 0.5), (0.35, -0.2), (0.55, -1.0), (0.8, 0.3), (1, 0.5)]))
            p.r("Spine", y=keys(t, [(0, -0.2), (0.45, 0.3), (1, -0.2)]))
            return p
    elif cls == "Terraformer":
        def fn(t):
            p = base(t, crouch=0.1, lean=-0.3)
            pull = keys(t, [(0, 0.2), (0.5, 1.0), (1, 0.2)])
            p.r("UpperArmL", x=pull, z=0.05)
            p.r("ForearmL", x=0.3 + (1 - pull) * 0.4)
            p.r("UpperArmR", x=0.3, z=0.05)
            p.r("Spine", y=(pull - 0.6) * 0.4)
            return p
    elif cls == "Medic":
        def fn(t):
            p = base(t, crouch=0.2, lean=-0.35)
            p.r("UpperArmR", x=1.0 + wave(t, 2) * 0.1, z=-0.2)
            p.r("ForearmR", x=0.3)
            p.r("UpperArmL", x=0.9 + wave(t, 2, 0.5) * 0.1, z=0.2)
            p.r("ForearmL", x=0.4)
            p.r("Head", x=0.35, y=wave(t, 1) * 0.2)
            return p
    elif cls == "Surveyor":
        def fn(t):
            p = base(t, crouch=0.04, lean=-0.05)
            p.r("UpperArmR", x=0.45, z=0.1)
            p.r("ForearmR", x=0.9)
            p.r("HandR", x=-0.9)
            p.r("Head", y=wave(t, 1) * 0.6, x=-0.1)
            p.r("Dish", y=t * TAU)
            return p
    elif cls in ("Defense", "Sentinel"):
        def fn(t):
            p = base(t, crouch=0.09, lean=-0.05)
            scan = wave(t, 1) * 0.45
            p.r("Spine", y=scan * 0.5)
            p.r("Head", y=scan)
            if "Turret" in props:
                p.r("Turret", y=scan * 1.5)
            return p
    elif cls == "Courier":
        def fn(t):
            p = base(t, crouch=0.08 + 0.1 * abs(wave(t, 1)), lean=-0.25)
            p.r("UpperArmR", x=0.9, z=-0.2)
            p.r("ForearmR", x=0.8 + wave(t, 2) * 0.2)
            p.r("UpperArmL", x=0.9, z=0.2)
            p.r("ForearmL", x=0.8 + wave(t, 2, 0.5) * 0.2)
            if "Beacon" in props:
                p.r("Beacon", y=t * TAU * 3)
            return p
    else:   # Scout: shade visor and sweep the horizon
        def fn(t):
            p = base(t, crouch=0.03, lean=0.02)
            p.r("UpperArmR", x=1.9, z=-0.4)
            p.r("ForearmR", x=1.5)
            p.r("Head", y=wave(t, 1) * 0.7, x=-0.08)
            p.r("Spine", y=wave(t, 1) * 0.25)
            if "Antenna" in props:
                p.r("Antenna", z=wave(t, 2) * 0.15)
            return p
    return fn


def down_clip(cls, b: Build, props):
    holds = HOLDS[cls]

    def fn(t):
        p = Pose()
        k1 = smoother(t / 0.45)                  # buckle to the knees
        k2 = ease_out_back(clamp((t - 0.35) / 0.65, 0, 1), 1.2)  # slump forward
        drop = k1 * (b.leg * 0.62) + k2 * 0.05
        # Knees fold: drive IK with a hip drop and feet trailing back
        apply_legs(p, b, (0, -0.05 * k1, -drop), [(0, -0.05 * k1, 0), (0, -0.25 * k1, 0.02 * k1)])
        p.t("Hips", y=-0.05 * k1, z=-drop)
        p.r("Hips", x=-0.2 * k2, z=0.08 * k2)
        p.r("Spine", x=-0.35 * k2, z=0.1 * k2)
        p.r("Chest", x=-0.2 * k2)
        p.r("Head", x=0.5 * k2, y=0.25 * k2)
        arms_rest(p, b, holds)
        for S, s in SIDES:
            p.r(f"UpperArm{S}", x=0.2 * k2, z=s * 0.12 * k2)
            p.r(f"Forearm{S}", x=0.3 * k2)
        return p

    return fn


# ---------------------------------------------------------------------------
# entry point used by sm_animated_roster.py
# ---------------------------------------------------------------------------

def join_parts(parts, name):
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = parts[0]
    obj.name = name
    obj.data.name = name
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    try:
        bpy.ops.uv.smart_project(angle_limit=1.05, island_margin=0.015)
    except Exception as exc:  # pragma: no cover
        print("[SM] uv skip", exc)
    bpy.ops.object.mode_set(mode="OBJECT")
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(40))
    except Exception:
        pass
    return obj


def build_hero(cls, mats, mesh_name, clip_names=("Idle", "Walk", "Strike", "Work", "Down")):
    """Build rig + mesh + all clips. Returns (armature, mesh, meta)."""
    style, kit_fn = CLASSES[cls]
    b = BUILDS[style.build]
    rig = HeroRig(mesh_name, b)
    k = Kit(mats)
    base_bones = set(rig.heads)
    kit_fn(k, rig)                       # adds prop bones (edit mode) + prop geometry
    props = set(rig.heads) - base_bones
    build_body(k, rig, style)
    arm = rig.finish()
    mesh = join_parts(k.parts, mesh_name)
    mesh.parent = arm
    mod = mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = arm

    idle_n, walk_n, strike_n, work_n, down_n = clip_names
    bake(arm, idle_n, 48, idle_clip(cls, b, props))
    frames, v_ref, walk_fn = walk_clip(cls, b, props)
    bake(arm, walk_n, frames, walk_fn)
    bake(arm, strike_n, 24, strike_clip(cls, b, props), cyclic=False)
    bake(arm, work_n, 32, work_clip(cls, b, props))
    bake(arm, down_n, 30, down_clip(cls, b, props), cyclic=False)
    arm.animation_data.action = bpy.data.actions[idle_n]
    arm.animation_data.action_slot = bpy.data.actions[idle_n].slots[0]
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    meta = {
        "walkSpeed": round(v_ref, 3),
        "walkFrames": frames,
        "build": b.name,
        "height": round(b.head_z + b.head_r * 2.0, 3),
        "tris": tris,
        "bones": len(arm.data.bones),
    }
    return arm, mesh, meta


HERO_KEYS = tuple(CLASSES.keys())
