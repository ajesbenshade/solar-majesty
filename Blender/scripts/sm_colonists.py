#!/usr/bin/env python3
"""Solar Majesty colonists v1 (art batch 3): suited villager variants on the Kevin Iglesias rig.

Replaces the capsule + ring placeholder built by VillagerAgent.Spawn. Four role variants share one
skeleton whose bone names and hierarchy copy Kevin Iglesias' Human Basic Motions rig
(B-hips, B-spine, B-chest, B-neck, B-head, B-shoulder/upperArm/forearm/hand, B-thigh/shin/foot/toe),
so Unity maps it as a Humanoid and plays the pack's HumanM/F Idle01 and Walk01_Forward clips by
retargeting. No mesh or texture from the pack is used, only the joint layout and names.

  Engineer     yellow trim   hard-hat crest, head lamp, tool pouches, wrench on the pack
  Hydroponics  green trim    sprayer tank + hose, wide sun visor, seed pouch
  Medic        red trim      med pack with crosses (top + back), antenna
  Hauler       violet trim   cargo frame + crate, heavy shoulders

Style: Majesty 2 proportions read from the overseer camera (big helmet, big gloves and boots, chunky
torso, short legs), white hull suit with teal/blue accents (colony palette), role colour on the top
surfaces (helmet crest, shoulder pads, pack) so roles read at distance. Heroes are cream + orange,
so colonists stay white + teal to read as civilians.

Conventions
  * Blender: Z up, the colonist faces -Y, its left side is +X. With the FBX settings below Blender -Y
    lands on Unity +Z, so the model faces +Z (travel direction) in Unity. (Fauna v1 was modelled
    facing +Y and walked backwards; do not flip this.)
  * Rest pose is a T-pose (Unity Humanoid prefers it). Every mesh part is rigidly skinned to one
    bone (segmented suit), so there is no weight painting to drift.
  * Materials: slot 0 SM_Art_Colonist_Suit (shared 128px palette atlas T_Colonist_Suit.png),
    slot 1 SM_Art_Colonist_Role_<Variant> (solid role colour). Unity can recolour the trim alone
    with Renderer.SetPropertyBlock(block, 1).
  * Budget ~1.5k triangles per variant.

Usage (Blender 4.2.3 LTS):
  blender -b --python Blender/scripts/sm_colonists.py -- --export Blender/exports/Colonists \
      --blend Blender/SolarMajesty_Colonists.blend [--review /tmp/renders] [--only Medic]
"""

from __future__ import annotations

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, Euler

# ---------------------------------------------------------------------------- palette

def srgb(r, g, b):
    def f(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (f(r), f(g), f(b))

ATLAS = 128          # px
CELL = 32            # px per swatch (4 x 4 grid)
# Swatch index -> sRGB colour. 5 is the visor gradient column (dark at the chin, sky highlight up top).
SWATCH = {
    0: (0.94, 0.95, 0.96),   # white hull
    1: (0.78, 0.81, 0.85),   # hull shade / seams
    2: (0.07, 0.66, 0.72),   # teal accent
    3: (0.13, 0.33, 0.60),   # deep colony blue
    4: (0.19, 0.21, 0.24),   # dark rubber (gloves, boots, joints)
    5: None,                 # visor glass gradient
    6: (0.57, 0.60, 0.64),   # steel
    7: (0.08, 0.09, 0.10),   # near black
    8: (0.98, 0.98, 0.97),   # emblem white
    9: (0.62, 0.45, 0.28),   # crate canvas / tan
}
WHITE, SHADE, TEAL, BLUE, DARK, VISOR, STEEL, BLACK, EMBLEM, TAN = range(10)
ROLE = -1  # marker: role material slot

VISOR_LO = (0.03, 0.06, 0.12)
VISOR_HI = (0.30, 0.52, 0.72)

VARIANTS = {
    "Engineer":    (0.98, 0.77, 0.10),
    "Hydroponics": (0.36, 0.80, 0.26),
    "Medic":       (0.90, 0.19, 0.19),
    "Hauler":      (0.56, 0.37, 0.88),
}


def swatch_rect(i):
    col, row = i % 4, i // 4
    u0, v0 = col * CELL / ATLAS, 1.0 - (row + 1) * CELL / ATLAS
    return u0, v0, CELL / ATLAS


def build_atlas(path):
    """Write the palette atlas as an 8-bit sRGB PNG (pure Python, no PIL) and load it."""
    import struct
    import zlib
    rows = []
    for y in range(ATLAS):          # PNG rows run top -> bottom
        row = bytearray([0])
        for x in range(ATLAS):
            col, r = x // CELL, y // CELL
            i = r * 4 + col
            c = SWATCH.get(i)
            if i == VISOR:
                t = 1.0 - (y % CELL) / (CELL - 1)    # bottom of the cell = chin (dark)
                c = tuple(VISOR_LO[k] + (VISOR_HI[k] - VISOR_LO[k]) * (t ** 1.6) for k in range(3))
            if c is None:
                c = (1.0, 0.0, 1.0)
            row += bytes(int(round(max(0.0, min(1.0, ch)) * 255)) for ch in c)
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", ATLAS, ATLAS, 8, 2, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)
    img = bpy.data.images.load(path, check_existing=False)
    img.name = "T_Colonist_Suit"
    return img


# ---------------------------------------------------------------------------- skeleton

# Kevin Iglesias Human Basic Motions joint names and parenting, re-proportioned to a chunky
# 1.33 m colonist (Majesty 2 peasant scale next to the 1.98 m hero mechs).
BONES = [
    # name,              parent,            head,                  tail
    ("B-hips",           None,              (0.0, 0.0, 0.50),      (0.0, 0.0, 0.58)),
    ("B-spine",          "B-hips",          (0.0, 0.0, 0.58),      (0.0, 0.0, 0.70)),
    ("B-chest",          "B-spine",         (0.0, 0.0, 0.70),      (0.0, 0.0, 0.86)),
    ("B-neck",           "B-chest",         (0.0, 0.0, 0.86),      (0.0, 0.0, 0.92)),
    ("B-head",           "B-neck",          (0.0, 0.0, 0.92),      (0.0, 0.0, 1.14)),
]
for s, x in (("L", 1.0), ("R", -1.0)):
    BONES += [
        (f"B-shoulder.{s}",  "B-chest",           (0.04 * x, 0.0, 0.83), (0.19 * x, 0.0, 0.83)),
        (f"B-upperArm.{s}",  f"B-shoulder.{s}",   (0.19 * x, 0.0, 0.83), (0.35 * x, 0.0, 0.82)),
        (f"B-forearm.{s}",   f"B-upperArm.{s}",   (0.35 * x, 0.0, 0.82), (0.49 * x, 0.0, 0.81)),
        (f"B-hand.{s}",      f"B-forearm.{s}",    (0.49 * x, 0.0, 0.81), (0.58 * x, 0.0, 0.805)),
        (f"B-thigh.{s}",     "B-hips",            (0.09 * x, 0.0, 0.48), (0.10 * x, 0.0, 0.27)),
        (f"B-shin.{s}",      f"B-thigh.{s}",      (0.10 * x, 0.0, 0.27), (0.10 * x, 0.012, 0.075)),
        (f"B-foot.{s}",      f"B-shin.{s}",       (0.10 * x, 0.012, 0.075), (0.10 * x, -0.075, 0.03)),
        (f"B-toe.{s}",       f"B-foot.{s}",       (0.10 * x, -0.075, 0.03), (0.10 * x, -0.15, 0.03)),
    ]

# Unity HumanBodyBones names for the Avatar (mirrored in the Unity setup + tests).
HUMAN_MAP = {
    "B-hips": "Hips", "B-spine": "Spine", "B-chest": "Chest", "B-neck": "Neck", "B-head": "Head",
    "B-shoulder.L": "LeftShoulder", "B-upperArm.L": "LeftUpperArm", "B-forearm.L": "LeftLowerArm", "B-hand.L": "LeftHand",
    "B-shoulder.R": "RightShoulder", "B-upperArm.R": "RightUpperArm", "B-forearm.R": "RightLowerArm", "B-hand.R": "RightHand",
    "B-thigh.L": "LeftUpperLeg", "B-shin.L": "LeftLowerLeg", "B-foot.L": "LeftFoot", "B-toe.L": "LeftToes",
    "B-thigh.R": "RightUpperLeg", "B-shin.R": "RightLowerLeg", "B-foot.R": "RightFoot", "B-toe.R": "RightToes",
}


def build_rig(name):
    arm = bpy.data.armatures.new(name + "_RigData")
    arm.display_type = "STICK"
    rig = bpy.data.objects.new(name + "_Rig", arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    for bname, parent, head, tail in BONES:
        b = arm.edit_bones.new(bname)
        b.head = head
        b.tail = tail
        b.roll = 0.0
        if parent:
            b.parent = eb[parent]
            b.use_connect = False
        eb[bname] = b
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def bone_head(name):
    for b in BONES:
        if b[0] == name:
            return Vector(b[2])
    raise KeyError(name)


# ---------------------------------------------------------------------------- mesh parts

class Kit:
    """Collects rigid parts (one bone each) into one skinned mesh."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.dl = self.bm.verts.layers.deform.new()
        self.groups = []

    def _gid(self, bone):
        if bone not in self.groups:
            self.groups.append(bone)
        return self.groups.index(bone)

    def _merge(self, tmp, mat, bone, smooth, swatch):
        """Copy tmp bmesh into the kit with material slot, UVs and a rigid bone weight."""
        gid = self._gid(bone)
        vmap = {}
        zs = [v.co.z for v in tmp.verts]
        zmin, zmax = (min(zs), max(zs)) if zs else (0, 1)
        for v in tmp.verts:
            nv = self.bm.verts.new(v.co)
            nv[self.dl][gid] = 1.0
            vmap[v] = nv
        for f in tmp.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = 1 if swatch == ROLE else 0
            nf.smooth = smooth
            for loop in nf.loops:
                if swatch == ROLE:
                    loop[self.uv].uv = (0.5, 0.5)
                    continue
                u0, v0, s = swatch_rect(swatch)
                if swatch == VISOR:
                    t = (loop.vert.co.z - zmin) / max(1e-6, zmax - zmin)
                    loop[self.uv].uv = (u0 + s * 0.5, v0 + s * (0.06 + 0.88 * t))
                else:
                    loop[self.uv].uv = (u0 + s * 0.5, v0 + s * 0.5)
        tmp.free()

    @staticmethod
    def _xf(tmp, loc, rot=(0, 0, 0), scale=(1, 1, 1)):
        m = Matrix.Translation(Vector(loc)) @ Euler(tuple(math.radians(a) for a in rot)).to_matrix().to_4x4() \
            @ Matrix.Diagonal(Vector((*scale, 1.0)))
        bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)

    def box(self, size, loc, swatch, bone, rot=(0, 0, 0), bevel=0.0, segs=1, smooth=False, taper=None):
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        if taper:  # scale the top face (z>0) in x/y
            for v in tmp.verts:
                if v.co.z > 0:
                    v.co.x *= taper[0]
                    v.co.y *= taper[1]
        self._xf(tmp, (0, 0, 0), (0, 0, 0), size)
        if bevel > 0:
            bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=bevel, segments=segs, affect="EDGES",
                            profile=0.5, clamp_overlap=True)
        self._xf(tmp, loc, rot)
        self._merge(tmp, None, bone, smooth, swatch)

    def cyl(self, r, depth, loc, swatch, bone, rot=(0, 0, 0), verts=8, r2=None, caps=True, smooth=True):
        tmp = bmesh.new()
        bmesh.ops.create_cone(tmp, cap_ends=caps, cap_tris=False, segments=verts,
                              radius1=r, radius2=r if r2 is None else r2, depth=depth)
        self._xf(tmp, loc, rot)
        self._merge(tmp, None, bone, smooth, swatch)

    def seg(self, a, b, r, swatch, bone, verts=8, r2=None, smooth=True):
        a, b = Vector(a), Vector(b)
        d = b - a
        rot = d.to_track_quat("Z", "Y").to_euler()
        tmp = bmesh.new()
        bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=verts,
                              radius1=r, radius2=r if r2 is None else r2, depth=d.length)
        m = Matrix.Translation((a + b) * 0.5) @ rot.to_matrix().to_4x4()
        bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)
        self._merge(tmp, None, bone, smooth, swatch)

    def sphere(self, r, loc, swatch, bone, scale=(1, 1, 1), u=10, v=6, rot=(0, 0, 0), smooth=True, cut_below=None):
        tmp = bmesh.new()
        bmesh.ops.create_uvsphere(tmp, u_segments=u, v_segments=v, radius=r)
        if cut_below is not None:  # drop the lower part (dome); z in local unit space
            kill = [vv for vv in tmp.verts if vv.co.z < cut_below * r - 1e-5]
            bmesh.ops.delete(tmp, geom=kill, context="VERTS")
            edges = [e for e in tmp.edges if e.is_boundary]
            if edges:
                bmesh.ops.edgeloop_fill(tmp, edges=edges)
        self._xf(tmp, (0, 0, 0), (0, 0, 0), scale)
        self._xf(tmp, loc, rot)
        self._merge(tmp, None, bone, smooth, swatch)

    def to_object(self, mats):
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in mats:
            me.materials.append(m)
        obj = bpy.data.objects.new(self.name, me)
        bpy.context.collection.objects.link(obj)
        for g in self.groups:
            obj.vertex_groups.new(name=g)
        return obj


# ---------------------------------------------------------------------------- the colonist

def build_body(k: Kit, variant: str):
    hauler = variant == "Hauler"
    wide = 1.08 if hauler else 1.0

    # Helmet (big head), visor (faces -Y = front), role crest across the top, neck ring.
    hz = 1.035
    k.sphere(0.205, (0, 0.0, hz), WHITE, "B-head", scale=(1.0, 1.0, 0.96), u=12, v=7)
    k.sphere(0.168, (0, -0.118, hz + 0.004), VISOR, "B-head", scale=(1.0, 0.70, 0.68), u=10, v=6)
    crest_w = 0.46 if variant == "Engineer" else 0.32
    k.sphere(0.224, (0, 0.03, hz + 0.0), ROLE, "B-head", scale=(crest_w, 0.98, 0.985), u=8, v=6, cut_below=0.2)
    k.cyl(0.115, 0.05, (0, 0, 0.885), STEEL, "B-neck", verts=8)
    # Ear pucks (comms) in teal: read as a helmet, not a ball.
    for x in (1, -1):
        k.cyl(0.06, 0.04, (0.198 * x, 0.0, hz), TEAL, "B-head", rot=(0, 90, 0), verts=6)

    # Torso: chunky white chest, colony-blue flexible abdomen, teal belt with a steel buckle.
    k.box((0.36 * wide, 0.25, 0.20), (0, 0.0, 0.775), WHITE, "B-chest", bevel=0.045, segs=1, taper=(1.08, 1.0))
    k.box((0.15, 0.03, 0.07), (0, -0.128, 0.79), TEAL, "B-chest", bevel=0.01)
    k.box((0.07, 0.02, 0.035), (0.055, -0.137, 0.79), ROLE, "B-chest")  # role tag on the chest panel
    k.cyl(0.13 * wide, 0.11, (0, 0, 0.635), BLUE, "B-spine", verts=10, r2=0.15 * wide)
    k.box((0.31 * wide, 0.22, 0.10), (0, 0.0, 0.53), TEAL, "B-hips", bevel=0.025)
    k.box((0.08, 0.03, 0.06), (0, -0.112, 0.53), STEEL, "B-hips")

    # Arms: role shoulder pads (top surface reads from the overseer camera), white sleeves,
    # dark elbows, teal cuffs, big dark gloves.
    pad = 1.25 if hauler else 1.0
    for s, x in (("L", 1), ("R", -1)):
        k.sphere(0.092 * pad, (0.205 * x, 0.0, 0.85), ROLE, f"B-upperArm.{s}", scale=(1.15, 1.05, 0.85), u=8, v=6, cut_below=-0.2)
        k.seg((0.22 * x, 0, 0.828), (0.35 * x, 0, 0.82), 0.062, WHITE, f"B-upperArm.{s}", verts=7)
        k.sphere(0.055, (0.352 * x, 0, 0.82), DARK, f"B-forearm.{s}", u=6, v=4)
        k.seg((0.36 * x, 0, 0.82), (0.475 * x, 0, 0.812), 0.064, WHITE, f"B-forearm.{s}", r2=0.07, verts=7)
        k.seg((0.455 * x, 0, 0.812), (0.495 * x, 0, 0.81), 0.077, TEAL, f"B-forearm.{s}", verts=7)
        k.box((0.105, 0.095, 0.10), (0.545 * x, -0.005, 0.807), DARK, f"B-hand.{s}", bevel=0.03)
        k.box((0.045, 0.04, 0.035), (0.515 * x, -0.06, 0.80), DARK, f"B-hand.{s}")  # thumb

    # Legs: white thighs/shins, teal knee pads, big dark boots split at the toe joint.
    for s, x in (("L", 1), ("R", -1)):
        k.seg((0.093 * x, 0, 0.49), (0.10 * x, 0, 0.275), 0.083, WHITE, f"B-thigh.{s}")
        k.sphere(0.064, (0.10 * x, -0.03, 0.27), TEAL, f"B-shin.{s}", scale=(1.0, 0.8, 1.0), u=8, v=5)
        k.seg((0.10 * x, 0.005, 0.27), (0.10 * x, 0.01, 0.11), 0.074, WHITE, f"B-shin.{s}", r2=0.068)
        k.box((0.135, 0.17, 0.095), (0.10 * x, -0.005, 0.0475), DARK, f"B-foot.{s}", bevel=0.025)
        k.cyl(0.075, 0.05, (0.10 * x, 0.012, 0.11), SHADE, f"B-shin.{s}", verts=7)
        k.box((0.13, 0.085, 0.075), (0.10 * x, -0.11, 0.0375), DARK, f"B-toe.{s}", bevel=0.025)

    # Base life-support pack (variants dress it).
    if variant != "Hauler":
        k.box((0.27, 0.13, 0.25), (0, 0.175, 0.77), WHITE, "B-chest", bevel=0.03)
        k.box((0.29, 0.06, 0.05), (0, 0.18, 0.66), TEAL, "B-chest", bevel=0.01)


def build_role(k: Kit, variant: str):
    if variant == "Engineer":
        # Head lamp on the right side, tool pouches, wrench strapped to the pack, yellow pack lid.
        k.cyl(0.035, 0.07, (-0.16, -0.09, 1.11), STEEL, "B-head", rot=(90, 0, 0), verts=8)
        k.cyl(0.028, 0.012, (-0.16, -0.128, 1.11), ROLE, "B-head", rot=(90, 0, 0), verts=8)
        k.box((0.28, 0.14, 0.04), (0, 0.175, 0.905), ROLE, "B-chest", bevel=0.012)
        for x in (1, -1):
            k.box((0.075, 0.06, 0.085), (0.15 * x, -0.07, 0.51), ROLE, "B-hips", bevel=0.012)
        k.box((0.035, 0.02, 0.30), (0.07, 0.25, 0.80), STEEL, "B-chest", rot=(0, 25, 0))
        k.box((0.09, 0.025, 0.06), (0.135, 0.25, 0.935), STEEL, "B-chest", rot=(0, 25, 0))
    elif variant == "Hydroponics":
        # Sprayer tank (green cap), hose to the right hip, wide sun brim, seed pouch.
        k.cyl(0.085, 0.30, (0, 0.27, 0.80), SHADE, "B-chest", verts=10)
        k.cyl(0.088, 0.06, (0, 0.27, 0.975), ROLE, "B-chest", verts=10)
        k.cyl(0.088, 0.04, (0, 0.27, 0.66), ROLE, "B-chest", verts=10)
        k.seg((-0.07, 0.27, 0.70), (-0.17, 0.10, 0.60), 0.018, DARK, "B-chest", verts=6)
        k.seg((-0.17, 0.10, 0.60), (-0.17, -0.02, 0.53), 0.018, DARK, "B-hips", verts=6)
        k.cyl(0.255, 0.018, (0, 0.0, 1.0), ROLE, "B-head", verts=14, r2=0.245)
        k.box((0.09, 0.06, 0.08), (0.15, -0.07, 0.51), ROLE, "B-hips", bevel=0.015)
        k.box((0.05, 0.012, 0.05), (-0.06, -0.137, 0.79), ROLE, "B-chest", rot=(0, 45, 0))  # leaf tag
    elif variant == "Medic":
        # Med pack with crosses on top and back, antenna, white-cross shoulder flashes.
        k.box((0.18, 0.05, 0.016), (0, 0.175, 0.90), ROLE, "B-chest")
        k.box((0.05, 0.18, 0.016), (0, 0.175, 0.90), ROLE, "B-chest")
        k.box((0.16, 0.012, 0.05), (0, 0.243, 0.78), ROLE, "B-chest")
        k.box((0.05, 0.012, 0.16), (0, 0.243, 0.78), ROLE, "B-chest")
        k.seg((0.12, 0.08, 1.16), (0.15, 0.10, 1.38), 0.008, STEEL, "B-head", verts=5)
        k.sphere(0.022, (0.15, 0.10, 1.39), ROLE, "B-head", u=6, v=4)
        for x in (1, -1):
            k.box((0.08, 0.025, 0.012), (0.215 * x, 0.0, 0.925), EMBLEM, f"B-upperArm.{'L' if x > 0 else 'R'}")
            k.box((0.025, 0.08, 0.012), (0.215 * x, 0.0, 0.925), EMBLEM, f"B-upperArm.{'L' if x > 0 else 'R'}")
    elif variant == "Hauler":
        # Steel A-frame with a violet-strapped crate riding high: biggest silhouette of the four.
        for x in (1, -1):
            k.seg((0.13 * x, 0.15, 0.56), (0.13 * x, 0.17, 1.12), 0.018, STEEL, "B-chest", verts=6)
        k.box((0.30, 0.06, 0.05), (0, 0.16, 0.60), STEEL, "B-chest")
        k.box((0.34, 0.26, 0.30), (0, 0.29, 0.92), TAN, "B-chest", bevel=0.02)
        k.box((0.36, 0.27, 0.05), (0, 0.29, 0.98), ROLE, "B-chest")
        k.box((0.06, 0.28, 0.31), (0, 0.29, 0.92), ROLE, "B-chest")
        k.box((0.30, 0.20, 0.02), (0, 0.29, 1.075), SHADE, "B-chest")
        k.box((0.26, 0.10, 0.04), (0, -0.07, 0.86), DARK, "B-chest", bevel=0.01)  # chest strap


# ---------------------------------------------------------------------------- materials

def make_materials(atlas):
    suit = bpy.data.materials.get("SM_Art_Colonist_Suit") or bpy.data.materials.new("SM_Art_Colonist_Suit")
    suit.use_nodes = True
    nt = suit.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = atlas
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.45
    roles = {}
    for v, c in VARIANTS.items():
        m = bpy.data.materials.new("SM_Art_Colonist_Role_" + v)
        m.use_nodes = True
        b = m.node_tree.nodes.get("Principled BSDF")
        lin = srgb(*c)
        b.inputs["Base Color"].default_value = (*lin, 1.0)
        b.inputs["Roughness"].default_value = 0.4
        b.inputs["Emission Color"].default_value = (*lin, 1.0)
        b.inputs["Emission Strength"].default_value = 0.18
        roles[v] = m
    return suit, roles


# ---------------------------------------------------------------------------- build / export

def build_variant(variant, suit, role_mat, x_offset=0.0):
    name = "SM_Colonist_" + variant
    rig = build_rig(name)
    k = Kit(name)
    build_body(k, variant)
    build_role(k, variant)
    obj = k.to_object([suit, role_mat])
    obj.parent = rig
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    rig.location.x = x_offset
    return rig, obj


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def export_fbx(rig, obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    old = rig.location.copy()
    rig.location = (0, 0, 0)
    rig.select_set(True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        apply_unit_scale=True,
        object_types={"ARMATURE", "MESH"},
        mesh_smooth_type="FACE",
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="STRIP",
    )
    rig.location = old


def pose_relaxed(rig, t=0.0, walk=False):
    """Review-only pose (arms down, optional stride). Never exported."""
    pb = rig.pose.bones
    for b in pb:
        b.rotation_mode = "XYZ"
        b.rotation_euler = (0, 0, 0)
    sw = math.sin(t * math.tau) if walk else 0.0
    for s, x in (("L", 1), ("R", -1)):
        # Bones point along +-X for arms (rest T-pose). Rotate about the bone's local axis that
        # maps to world Y to drop arms ~70 deg.
        pb[f"B-upperArm.{s}"].rotation_euler = (0, 0, math.radians(-68) * x) if False else (0, 0, 0)
    # Simple world-space approach: rotate pose bones via matrices.
    bpy.context.view_layer.update()
    for s, x in (("L", 1), ("R", -1)):
        b = pb[f"B-upperArm.{s}"]
        ang = math.radians(72 * x)
        swing = math.radians(18 * sw * (1 if s == "L" else -1))
        rot = Matrix.Rotation(swing, 4, "X") @ Matrix.Rotation(ang, 4, "Y")
        head = b.head.copy()
        mw = Matrix.Translation(head) @ rot @ Matrix.Translation(-head) @ b.matrix
        b.matrix = mw
        bpy.context.view_layer.update()
        f = pb[f"B-forearm.{s}"]
        head = f.head.copy()
        f.matrix = Matrix.Translation(head) @ Matrix.Rotation(math.radians(-14), 4, "X") @ Matrix.Translation(-head) @ f.matrix
        bpy.context.view_layer.update()
    if walk:
        for s, sign in (("L", 1), ("R", -1)):
            th = pb[f"B-thigh.{s}"]
            head = th.head.copy()
            th.matrix = Matrix.Translation(head) @ Matrix.Rotation(math.radians(-24 * sw * sign), 4, "X") @ Matrix.Translation(-head) @ th.matrix
            bpy.context.view_layer.update()
            sh = pb[f"B-shin.{s}"]
            head = sh.head.copy()
            bend = max(0.0, -sw * sign) * 30
            sh.matrix = Matrix.Translation(head) @ Matrix.Rotation(math.radians(bend), 4, "X") @ Matrix.Translation(-head) @ sh.matrix
            bpy.context.view_layer.update()


def review(rigs, out_dir):
    scn = bpy.context.scene
    scn.render.engine = "CYCLES"
    scn.cycles.samples = 48
    scn.cycles.use_denoising = True
    scn.render.film_transparent = False
    scn.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("Review")
    scn.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.66, 0.80, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    # ground
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    g = bpy.context.active_object
    gm = bpy.data.materials.new("Ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*srgb(0.40, 0.52, 0.28), 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.95
    g.data.materials.append(gm)
    sun_d = bpy.data.lights.new("Sun", "SUN")
    sun_d.energy = 3.2
    sun_d.angle = math.radians(8)
    sun = bpy.data.objects.new("Sun", sun_d)
    scn.collection.objects.link(sun)
    sun.rotation_euler = Euler((math.radians(48), 0, math.radians(-38)))
    cam_d = bpy.data.cameras.new("Cam")
    cam_d.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cam_d)
    scn.collection.objects.link(cam)
    scn.camera = cam
    scn.render.resolution_x, scn.render.resolution_y = 1600, 900

    def shoot(path, target, ortho, yaw_deg, pitch_deg=35):
        cam_d.ortho_scale = ortho
        yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
        # Camera looks toward the target from the front-ish side (-Y is the colonists' front).
        dirv = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
        cam.location = Vector(target) + dirv * 20
        cam.rotation_euler = (-dirv).to_track_quat("-Z", "Y").to_euler()
        scn.render.filepath = path
        bpy.ops.render.render(write_still=True)

    for r in rigs:
        pose_relaxed(r)
    n = len(rigs)
    shoot(os.path.join(out_dir, "blender_lineup_front.png"), (0, 0, 0.62), 1.2 * n + 0.6, 30, 22)
    shoot(os.path.join(out_dir, "blender_lineup_overseer.png"), (0, 0, 0.5), 1.2 * n + 0.6, 35, 45)
    shoot(os.path.join(out_dir, "blender_lineup_back.png"), (0, 0, 0.62), 1.2 * n + 0.6, 180 + 30, 22)
    for i, r in enumerate(rigs):
        pose_relaxed(r, t=0.25 * (i % 2 + 1), walk=True)
    shoot(os.path.join(out_dir, "blender_lineup_stride.png"), (0, 0, 0.62), 1.2 * n + 0.6, 60, 20)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(flag, default=None):
        return argv[argv.index(flag) + 1] if flag in argv else default

    export_dir = opt("--export")
    blend = opt("--blend")
    review_dir = opt("--review")
    only = opt("--only")
    names = [v for v in VARIANTS if not only or v in only.split(",")]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    tex_dir = export_dir or os.path.dirname(os.path.abspath(blend or "."))
    os.makedirs(tex_dir, exist_ok=True)
    atlas = build_atlas(os.path.join(tex_dir, "T_Colonist_Suit.png"))
    suit, roles = make_materials(atlas)

    rigs = []
    spacing = 1.2
    for i, v in enumerate(names):
        rig, obj = build_variant(v, suit, roles[v], (i - (len(names) - 1) * 0.5) * spacing)
        ws = [obj.matrix_world @ vv.co for vv in obj.data.vertices]
        h = max(w.z for w in ws)
        print(f"[SM] SM_Colonist_{v}: {tri_count(obj)} tris, {len(obj.data.vertices)} verts, height {h:.2f} m, "
              f"bones {len(rig.data.bones)}, mats {[m.name for m in obj.data.materials]}")
        if export_dir:
            export_fbx(rig, obj, os.path.join(export_dir, f"SM_Colonist_{v}.fbx"))
        rigs.append(rig)

    if blend:
        atlas.filepath = "//" + os.path.relpath(os.path.join(tex_dir, "T_Colonist_Suit.png"), os.path.dirname(os.path.abspath(blend)))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend), compress=True)
    if review_dir:
        os.makedirs(review_dir, exist_ok=True)
        review(rigs, review_dir)


if __name__ == "__main__":
    main()
