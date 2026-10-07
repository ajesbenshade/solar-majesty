#!/usr/bin/env python3
"""Enemy fauna v2 for Solar Majesty: seven rigged, animated creatures in the hostile style.

Style (approved 2026-10-07): Majesty 2 charm read from a high overseer camera. Chunky,
exaggerated silhouettes with big heads, eyes and claws. Hostile language matches the new
stalker den: organic dark chitin, bone, and hot orange glow (eyes, vents, sacs), so
"alive + orange glow = hostile" against the clean white/blue colony.

Each creature keeps its concept silhouette (ConceptSheets/SM_Unit_*_Turnaround.jpg):
  Dust Stalker  four-eyed quadruped, bone back plates, spined ridge, swept horns
  Ash Hopper    six stilt X-legs, segmented ash dome, glowing knee joints, rear cerci
  Soil Creeper  long graphite tile segments with olive plates, many short legs, tail cerci
  Rock Tick     wide crab carapace with a dorsal horn, two big orange pincers, eight legs
  Regolith Mite round dust-brown pillbug with graphite plates, glowing eye cluster
  Watt Leech    white ray with a glowing dorsal groove, side fins, front prongs
  Ice Wisp      seven-point crystal star around a hot core, floating over a glow ring

Mesh: one skinned mesh per creature, 1.5k-3k triangles, two materials:
  SM_Art_Fauna_<Name>       body; colour + AO baked (Cycles) to Assets/Art/Fauna/Textures/T_Fauna_<Name>.png
  SM_Art_Fauna_GlowAccent   shared emissive orange (eyes, vents, sacs, joints)
The material names also fall back sensibly through IndustrialArtDressing if the authored
materials are not kept (body -> the kind's hide slot, "Accent" -> the Orange slot).

Rig: same approach as v1 (Root + Body/segment chain + per-leg Hip/Knee/Foot, rigid or
blended weights). Joints are world-aligned (every bone points +Y with zero roll), so a
pose-bone rotation (x, y, z) is pitch / roll / yaw in creature space.
Clips: Idle, Walk, Strike, Down (Down plays once and holds). Every clip keys every bone on
every frame; looping clips end on their first pose. Walk stride speeds are measured from
the gait (stance travel / stance time) and written to UnitClipMeta.json by the roster.

Modelled with the front at +Y, ground at z = 0, metres. On output (mesh, joint heads and
keys) every creature is turned 180 degrees about Z: with the roster's FBX axis settings
Blender +Y lands on Unity -Z, and fauna roots face their travel direction (+Z), so without
the turn clip-driven fauna walk backwards (v1 did; only the static Stalker prefab was rescued
by FaunaDressing.AlignHead). Keys stay exact: Rz(pi) conjugation maps Euler XYZ (x, y, z)
to (-x, -y, z) and locations (x, y, z) to (-x, -y, z).

Used by sm_animated_roster.py (export path):
  blender --background --python Blender/scripts/sm_animated_roster.py -- --export --only Stalker,Hopper,Creeper,Tick,Mite,Leech,Wisp
Review renders (Blender lineup + frame strips) straight from this file:
  blender --background --python Blender/scripts/sm_fauna_v2.py -- --review [--out DIR] [--fast]
Tested on Blender 5.2.2 LTS (needs 4.4+ for action slots).
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

ROOT = Path(__file__).resolve().parents[2]
ART_DIR = ROOT / "Assets" / "Art" / "Fauna"
TEX_DIR = ART_DIR / "Textures"
ATLAS = 1024
FPS = 24
CLIPS = ("Idle", "Walk", "Strike", "Down")
GLOW_MAT = "SM_Art_Fauna_GlowAccent"
BAKE_TEXTURES = True          # roster lineup renders flip this off and reuse the PNGs
NO_BAKE = False               # quick previews: no UVs/bake, body shows vertex colour
META: dict[str, dict] = {}    # unit -> {"walkSpeed", "height", "tris"}
FACE_UNITY_FORWARD = True     # turn 180 deg on output so the head ends up on Unity +Z

UNIT = {
    "Stalker": ("SM_Unit_DustStalker", "DustStalker"),
    "Hopper": ("SM_Unit_AshHopper", "AshHopper"),
    "Creeper": ("SM_Unit_SoilCreeper", "SoilCreeper"),
    "Tick": ("SM_Unit_RockTick", "RockTick"),
    "Mite": ("SM_Unit_RegolithMite", "RegolithMite"),
    "Leech": ("SM_Unit_WattLeech", "WattLeech"),
    "Wisp": ("SM_Unit_IceWisp", "IceWisp"),
}
# Mesh datablock names from v1 FBXs, kept so Unity's name-based mesh fileIDs stay put.
OLD_GEOM = {
    "Stalker": "body.001", "Hopper": "body.001", "Creeper": "pl0.001", "Tick": "shell.001",
    "Mite": "belly.001", "Leech": "rib0.001", "Wisp": "core.001",
}


# --------------------------------------------------------------------------- colour

def lin(c):
    def f(v):
        v = max(0.0, v)
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return (f(c[0]), f(c[1]), f(c[2]))


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def nz(p, freq=1.0, seed=0.0):
    return noise.noise(Vector((p[0] * freq + seed * 13.1, p[1] * freq - seed * 5.3, p[2] * freq + seed * 7.7)))


def shade(deep, mid, hi, sheen=0.55, seed=0.0, freq=6.0, amp=0.10):
    """Chitin-style colour: dark underside, mid flanks, glossy highlight on top, light mottling."""
    def fn(co, n):
        up = n.z
        c = mix(deep, mid, smooth01(0.5 + 0.75 * up))
        if up > 0.35:
            c = mix(c, hi, (up - 0.35) / 0.65 * sheen)
        m = 1.0 + amp * nz(co, freq, seed)
        return tuple(max(0.0, v * m) for v in c)
    return fn


def flat(c):
    return lambda co, n: c


# --------------------------------------------------------------------------- mesh builder

class Build:
    """One skinned mesh in a single bmesh: colour layer, deform weights, 2 material slots."""

    def __init__(self, key: str):
        self.key = key
        self.unit, self.name = UNIT[key]
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")
        self.dl = self.bm.verts.layers.deform.verify()
        self.groups: list[str] = []

    def gi(self, bone: str) -> int:
        if bone not in self.groups:
            self.groups.append(bone)
        return self.groups.index(bone)

    def part(self, faces, color, bone=None, weights=None, glow=False, smooth=True):
        self.bm.normal_update()
        verts = set()
        for f in faces:
            f.material_index = 1 if glow else 0
            f.smooth = smooth
            for lp in f.loops:
                n = lp.vert.normal if smooth else f.normal
                lp[self.col] = (*[min(1.0, max(0.0, c)) for c in color(lp.vert.co, n)], 1.0)  # byte layer: display values
            verts.update(f.verts)
        for v in verts:
            w = weights(v.co) if weights else {bone: 1.0}
            dv = v[self.dl]
            dv.clear()
            tot = sum(max(0.0, x) for x in w.values()) or 1.0
            for b, x in w.items():
                if x > 1e-4:
                    dv[self.gi(b)] = x / tot
        return faces

    def tris(self) -> int:
        return sum(len(f.verts) - 2 for f in self.bm.faces)

    # ---- primitives (all write into self.bm and return faces) ----

    def loft(self, pts, rx, rz=None, sides=10, up=(0, 0, 1), disp=None, phase=0.0, cap0=True, cap1=True):
        """Tube through pts with elliptical rings (rx sideways, rz 'up'). A radius of 0 makes a point."""
        bm = self.bm
        pts = [Vector(p) for p in pts]
        rz = rz if rz is not None else rx
        n = len(pts)
        upv0 = Vector(up).normalized()
        rows = []
        for i in range(n):
            a = pts[max(0, i - 1)]
            b = pts[min(n - 1, i + 1)]
            t = (b - a).normalized()
            side = t.cross(upv0)
            if side.length < 1e-4:
                side = t.cross(Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((1, 0, 0)))
            side.normalize()
            upv = side.cross(t).normalized()
            r1, r2 = rx[i], rz[i]
            if r1 <= 1e-5 and r2 <= 1e-5:
                rows.append([bm.verts.new(pts[i])])
                continue
            row = []
            for s in range(sides):
                th = phase + 2 * math.pi * s / sides
                p = pts[i] + side * (math.cos(th) * r1) + upv * (math.sin(th) * r2)
                if disp:
                    p = disp(i, th, p, pts[i], side, upv)
                row.append(bm.verts.new(p))
            rows.append(row)
        faces = []
        for a, b in zip(rows, rows[1:]):
            if len(a) == 1 and len(b) == 1:
                continue
            if len(b) == 1:
                for s in range(sides):
                    faces.append(bm.faces.new((a[s], a[(s + 1) % sides], b[0])))
            elif len(a) == 1:
                for s in range(sides):
                    faces.append(bm.faces.new((a[0], b[(s + 1) % sides], b[s])))
            else:
                for s in range(sides):
                    faces.append(bm.faces.new((a[s], a[(s + 1) % sides], b[(s + 1) % sides], b[s])))
        if cap0 and len(rows[0]) > 2:
            faces.append(bm.faces.new(list(reversed(rows[0]))))
        if cap1 and len(rows[-1]) > 2:
            faces.append(bm.faces.new(rows[-1]))
        for f in faces:          # rings run side -> up around +t, which winds inward; flip outward
            f.normal_flip()
        return faces

    def spike(self, base, tip, r, sides=5, bend=(0, 0, 0), mid=0.55, up=(0, 0, 1)):
        base, tip = Vector(base), Vector(tip)
        m = base.lerp(tip, 0.5) + Vector(bend)
        pts = [base, base.lerp(m, 0.9), m.lerp(tip, 0.6), tip]
        return self.loft(pts, [r, r * 0.85, r * mid * 0.8, 0.0], sides=sides, up=up)

    def curve_tube(self, pts, radii, sides=6, up=(0, 0, 1), flatten=1.0, cap1=True):
        return self.loft(pts, radii, [r * flatten for r in radii], sides=sides, up=up, cap1=cap1)

    def ellipsoid(self, center, size, u=8, v=5, rot=None, disp=None):
        ret = bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=0.5)
        verts = ret["verts"]
        m = Matrix.Translation(Vector(center)) @ (rot.to_4x4() if rot else Matrix.Identity(4)) @ Matrix.Diagonal((*size, 1.0))
        for vt in verts:
            local = vt.co.copy()
            vt.co = m @ local
            if disp:
                vt.co = disp(vt.co, local)
        out = set()
        for vt in verts:
            out.update(vt.link_faces)
        return list(out)

    def arc_plate(self, center, radius_x, radius_z, y, width, thick, a0=0.35, a1=2.79, segs=6, lift=0.0):
        """Curved armour plate following an elliptical body section at height 'center'."""
        cx, cz = center
        pts = []
        for i in range(segs + 1):
            a = a0 + (a1 - a0) * i / segs
            pts.append(Vector((cx + math.cos(a) * radius_x, y, cz + math.sin(a) * radius_z + lift)))
        # loft 'side' is radial here (tangent x Y), so rx = thickness and rz = half width along Y
        rz = [width * 0.5 * (0.75 + 0.25 * math.sin(math.pi * i / segs)) for i in range(len(pts))]
        rx = [thick] * len(pts)
        return self.loft(pts, rx, rz, sides=4, up=(0, 1, 0), phase=math.pi / 4)

    # ---- finish ----

    def to_object(self, body_mat, glow_mat):
        me = bpy.data.meshes.new(OLD_GEOM[self.key])
        if FACE_UNITY_FORWARD:
            for v in self.bm.verts:
                v.co.x, v.co.y = -v.co.x, -v.co.y
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        if "Col" in me.color_attributes:
            me.color_attributes.active_color_name = "Col"
            me.color_attributes.render_color_index = me.color_attributes.find("Col")
        obj = bpy.data.objects.new(self.unit, me)
        bpy.context.scene.collection.objects.link(obj)
        for g in self.groups:
            obj.vertex_groups.new(name=g)
        me.materials.append(body_mat)
        me.materials.append(glow_mat)
        return obj


# --------------------------------------------------------------------------- rig + clips

class Rig:
    def __init__(self, unit: str, scale: float = 1.0):
        self.unit = unit
        self.len = 0.06 * scale
        arm = bpy.data.armatures.new(unit + "_RigData")
        self.obj = bpy.data.objects.new(unit + "_Rig", arm)
        bpy.context.scene.collection.objects.link(self.obj)
        self.heads: dict[str, Vector] = {}
        self.parents: dict[str, str | None] = {}
        self.add("Root", (0, 0, 0), None)

    def add(self, name, head, parent):
        self.heads[name] = Vector(head)
        self.parents[name] = parent
        return name

    def finish(self):
        bpy.context.view_layer.objects.active = self.obj
        bpy.ops.object.mode_set(mode="EDIT")
        eb = self.obj.data.edit_bones
        for name, head in self.heads.items():
            if FACE_UNITY_FORWARD:
                head = Vector((-head.x, -head.y, head.z))
            b = eb.new(name)
            b.head = head
            b.tail = head + Vector((0, self.len, 0))
            b.roll = 0.0
        for name, parent in self.parents.items():
            if parent:
                eb[name].parent = eb[parent]
                eb[name].use_connect = False
        bpy.ops.object.mode_set(mode="OBJECT")
        return self.obj


def begin_action(arm, name):
    if arm.animation_data is None:
        arm.animation_data_create()
    act = bpy.data.actions.new(name)
    arm.animation_data.action = act
    if hasattr(act, "slots"):
        slot = act.slots.new(id_type="OBJECT", name=arm.name)
        arm.animation_data.action_slot = slot
    return act


class Pose(dict):
    """bone -> [rot xyz, loc xyz]; additive helpers."""

    def r(self, bone, x=0.0, y=0.0, z=0.0):
        e = self.setdefault(bone, [[0.0, 0.0, 0.0], [0.0, 0.0, 0.0]])
        e[0][0] += x; e[0][1] += y; e[0][2] += z
        return self

    def l(self, bone, x=0.0, y=0.0, z=0.0):
        e = self.setdefault(bone, [[0.0, 0.0, 0.0], [0.0, 0.0, 0.0]])
        e[1][0] += x; e[1][1] += y; e[1][2] += z
        return self


def make_clip(arm, name, frames, fn):
    """Key every bone (rotation + location) on every frame 1..frames+1. fn(t in [0,1]) -> Pose."""
    act = begin_action(arm, name)
    pbs = list(arm.pose.bones)
    for pb in pbs:
        pb.rotation_mode = "XYZ"
    for f in range(frames + 1):
        t = f / frames
        pose = fn(t)
        for pb in pbs:
            rot, loc = pose.get(pb.name, ((0.0, 0.0, 0.0), (0.0, 0.0, 0.0)))
            if FACE_UNITY_FORWARD:
                rot = (-rot[0], -rot[1], rot[2])
                loc = (-loc[0], -loc[1], loc[2])
            pb.rotation_euler = rot
            pb.location = loc
            pb.keyframe_insert("rotation_euler", frame=f + 1)
            pb.keyframe_insert("location", frame=f + 1)
    act.use_frame_range = True
    act.frame_start = 1
    act.frame_end = frames + 1
    act.use_fake_user = False
    return act


def ease_in_out(t):
    return smooth01(t)


def ease_out(t):
    t = max(0.0, min(1.0, t))
    return 1 - (1 - t) ** 3


def ease_in(t):
    t = max(0.0, min(1.0, t))
    return t ** 3


def strike_curve(t, wind=0.32, hit=0.46, hold=0.62):
    """-1 at full wind-up, +1 at the hit, back to 0: anticipation -> snap -> hold -> recover."""
    if t < wind:
        return -ease_in_out(t / wind)
    if t < hit:
        return -1 + 2 * ease_out((t - wind) / (hit - wind))
    if t < hold:
        return 1.0
    return 1 - ease_in_out((t - hold) / (1 - hold))


def bounce(t, settle=0.72):
    """0 -> 1 with a small overshoot and settle, for Down collapses."""
    if t < settle:
        return ease_in(t / settle) * 1.04
    k = (t - settle) / (1 - settle)
    return 1.0 + 0.04 * math.cos(k * math.pi) * (1 - k)


def gait_phase(phi, duty=0.5):
    """phi in [0,1). Returns (swing in [-1,1] with +1 = fully forward, lift in [0,1])."""
    phi %= 1.0
    if phi < duty:
        return 1 - 2 * (phi / duty), 0.0
    s = (phi - duty) / (1 - duty)
    return -1 + 2 * smooth01(s), math.sin(math.pi * s)


def side_of(x):
    return -1.0 if x < 0 else 1.0


# --------------------------------------------------------------------------- materials / bake

def bake_material(name, chitin=True):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    vc = nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Col"
    ao = nodes.new("ShaderNodeAmbientOcclusion")
    ao.samples = 16
    ao.inputs["Distance"].default_value = 0.35
    aomix = nodes.new("ShaderNodeMapRange")
    aomix.inputs["To Min"].default_value = 0.38
    links.new(ao.outputs["AO"], aomix.inputs["Value"])
    tex = nodes.new("ShaderNodeTexNoise")
    tex.inputs["Scale"].default_value = 9.0
    tex.inputs["Detail"].default_value = 5.0
    mottle = nodes.new("ShaderNodeMapRange")
    mottle.inputs["From Min"].default_value = 0.3
    mottle.inputs["From Max"].default_value = 0.7
    mottle.inputs["To Min"].default_value = 0.86
    mottle.inputs["To Max"].default_value = 1.12
    links.new(tex.outputs["Fac"], mottle.inputs["Value"])
    m1 = nodes.new("ShaderNodeMix"); m1.data_type = "RGBA"; m1.blend_type = "MULTIPLY"
    m1.inputs["Factor"].default_value = 1.0
    links.new(vc.outputs["Color"], m1.inputs[6])
    links.new(aomix.outputs["Result"], m1.inputs[7])
    m2 = nodes.new("ShaderNodeMix"); m2.data_type = "RGBA"; m2.blend_type = "MULTIPLY"
    m2.inputs["Factor"].default_value = 1.0
    links.new(m1.outputs[2], m2.inputs[6])
    links.new(mottle.outputs["Result"], m2.inputs[7])
    last = m2.outputs[2]
    if chitin:
        vor = nodes.new("ShaderNodeTexVoronoi")
        vor.feature = "DISTANCE_TO_EDGE"
        vor.inputs["Scale"].default_value = 7.0
        crack = nodes.new("ShaderNodeMapRange")
        crack.inputs["From Max"].default_value = 0.05
        crack.inputs["To Min"].default_value = 0.72
        links.new(vor.outputs["Distance"], crack.inputs["Value"])
        m3 = nodes.new("ShaderNodeMix"); m3.data_type = "RGBA"; m3.blend_type = "MULTIPLY"
        m3.inputs["Factor"].default_value = 1.0
        links.new(last, m3.inputs[6])
        links.new(crack.outputs["Result"], m3.inputs[7])
        last = m3.outputs[2]
    links.new(last, emit.inputs["Color"])
    links.new(emit.outputs["Emission"], out.inputs["Surface"])
    img = nodes.new("ShaderNodeTexImage")
    img.name = "BAKE_TARGET"
    nodes.active = img
    return mat


def glow_material():
    mat = bpy.data.materials.get(GLOW_MAT)
    if mat:
        return mat
    mat = bpy.data.materials.new(GLOW_MAT)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.9, 0.22, 0.03, 1.0)
    bsdf.inputs["Emission Color"].default_value = (1.0, 0.14, 0.01, 1.0)
    bsdf.inputs["Emission Strength"].default_value = 1.6
    bsdf.inputs["Roughness"].default_value = 0.35
    # Same texture node so the bake pass (which needs an active image in every slot) succeeds.
    img = mat.node_tree.nodes.new("ShaderNodeTexImage")
    img.name = "BAKE_TARGET"
    mat.node_tree.nodes.active = img
    return mat


def body_material(name, img, roughness=0.55):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    if img is not None:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = img
    else:
        tex = nodes.new("ShaderNodeVertexColor")
        tex.layer_name = "Col"
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = 0.0
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def unwrap(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(62), island_margin=0.012, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake_or_load(obj, name, chitin=True):
    """Bake vertex colour x AO x mottling into T_Fauna_<name>.png (or reuse it)."""
    png = TEX_DIR / f"T_Fauna_{name}.png"
    if NO_BAKE:
        return None
    unwrap(obj)
    if not BAKE_TEXTURES and png.is_file():
        img = bpy.data.images.load(str(png), check_existing=True)
        return img
    TEX_DIR.mkdir(parents=True, exist_ok=True)
    bmat = bake_material(f"BAKE_{name}", chitin)
    gmat = obj.data.materials[1]
    obj.data.materials[0] = bmat
    img = bpy.data.images.new(f"T_Fauna_{name}", ATLAS, ATLAS, alpha=False)
    img.colorspace_settings.name = "sRGB"
    for m in (bmat, gmat):
        n = m.node_tree.nodes.get("BAKE_TARGET")
        n.image = img
        m.node_tree.nodes.active = n
    # Glow faces bake their vertex colour too (harmless; the glow material ignores the map).
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.render.bake.margin = 8
    scene.render.bake.use_clear = True
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    gnode_link = None
    # Temporarily give the glow slot the bake shader so glow faces bake their own colour.
    obj.data.materials[1] = bmat
    bpy.ops.object.bake(type="EMIT", margin=8, use_clear=True)
    obj.data.materials[1] = gmat
    img.filepath_raw = str(png)
    img.file_format = "PNG"
    img.save()
    print(f"[SM] baked {png.name}")
    bpy.data.materials.remove(bmat)
    return img


# --------------------------------------------------------------------------- shared parts

GLOW = flat((1.0, 0.55, 0.15))
BONE = (0.86, 0.80, 0.66)
BONE_SH = (0.52, 0.45, 0.35)


def bone_shade(seed=0.0):
    return shade(BONE_SH, mix(BONE_SH, BONE, 0.75), BONE, sheen=0.6, seed=seed, freq=9.0, amp=0.07)


def eye(b: Build, center, r, bone, u=8, v=5, squash=0.85):
    return b.part(b.ellipsoid(center, (r * 2, r * 2 * squash, r * 2), u=u, v=v), GLOW, bone, glow=True)


def leg(b: Build, rig: Rig, prefix, hip, knee, foot, r_hip, r_knee, r_foot, col, parent="Body",
        sides=7, knee_ball=None, knee_glow=False, foot_claws=0, claw_col=None, toe=None, lower_sides=None):
    """Hip -> Knee -> Foot leg with rigid parts. Returns the bone names."""
    hip, knee, foot = Vector(hip), Vector(knee), Vector(foot)
    rig.add(prefix + "_Hip", hip, parent)
    rig.add(prefix + "_Knee", knee, prefix + "_Hip")
    rig.add(prefix + "_Foot", foot, prefix + "_Knee")
    up_ref = (0, 1, 0)
    mid = hip.lerp(knee, 0.5) + Vector((0, 0, (knee - hip).length * 0.08))
    b.part(b.loft([hip, mid, knee], [r_hip, (r_hip + r_knee) * 0.55, r_knee], sides=sides, up=up_ref), col, prefix + "_Hip")
    if knee_ball:
        b.part(b.ellipsoid(knee, (knee_ball * 2,) * 3, u=6, v=4), GLOW if knee_glow else col, prefix + "_Knee", glow=knee_glow)
    ls = lower_sides or sides
    lower_end = foot if toe is None else foot
    mid2 = knee.lerp(lower_end, 0.45)
    if foot_claws:
        b.part(b.loft([knee, mid2, lower_end], [r_knee * 0.9, r_foot * 1.1, r_foot], sides=ls, up=up_ref), col, prefix + "_Knee")
        fwd = Vector((0, 1, 0))
        out = Vector((side_of(foot.x), 0, 0))
        b.part(b.ellipsoid(foot + Vector((0, 0.02, 0.01)), (r_foot * 2.6, r_foot * 3.0, r_foot * 1.6), u=6, v=4),
               col, prefix + "_Foot")
        for k in range(foot_claws):
            a = (k - (foot_claws - 1) / 2) * 0.55
            d = (fwd * math.cos(a) + out * math.sin(a) * 0.8).normalized()
            base = foot + d * r_foot * 1.2 + Vector((0, 0, 0.01))
            tip = base + d * r_foot * 2.6 + Vector((0, 0, -0.035))
            b.part(b.spike(base, tip, r_foot * 0.45, sides=4, bend=(0, 0, r_foot * 0.6)), claw_col or flat(BONE),
                   prefix + "_Foot")
    else:
        # tapered point foot (insect)
        b.part(b.loft([knee, mid2, lower_end], [r_knee * 0.9, r_foot, 0.0], sides=ls, up=up_ref), col, prefix + "_Knee")
    return prefix + "_Hip", prefix + "_Knee", prefix + "_Foot"


# --------------------------------------------------------------------------- finishing

def finish(b: Build, rig: Rig, clips, specs, walk_speed, chitin=True, roughness=0.55):
    """specs: [(frames, fn)] matching CLIPS. Bakes, binds, keys clips, records meta."""
    tris = b.tris()
    glow = glow_material()
    body = bpy.data.materials.get("SM_Art_Fauna_" + b.name) or bpy.data.materials.new("SM_Art_Fauna_" + b.name)
    obj = b.to_object(body, glow)
    img = bake_or_load(obj, b.name, chitin)
    obj.data.materials[0] = body_material("SM_Art_Fauna_" + b.name, img, roughness)
    obj.data.materials[1] = glow
    zs = [v.co.z for v in obj.data.vertices]
    height = max(zs) - min(0.0, min(zs))
    arm = rig.finish()
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    mod.use_vertex_groups = True
    names = list(clips)
    if len(names) < len(CLIPS):
        stem = names[0].rsplit("_", 1)[0] + "_" if "_" in names[0] else ""
        names += [stem + c for c in CLIPS[len(names):]]
    first = None
    for name, (frames, fn) in zip(names, specs):
        act = make_clip(arm, name, frames, fn)
        first = first or act
    arm.animation_data.action = first
    if hasattr(first, "slots") and first.slots:
        arm.animation_data.action_slot = first.slots[0]
    META[b.unit] = {"walkSpeed": round(walk_speed, 3), "height": round(height, 3), "tris": tris}
    print(f"[SM] {b.unit}: {tris} tris, height {height:.2f} m, walk {walk_speed:.2f} m/s, bones {len(arm.data.bones)}")
    return arm, obj


def interp_rings(rings, y):
    """rings [(y, zc, rx, rz)] -> zc, rx, rz at y."""
    if y <= rings[0][0]:
        return rings[0][1:]
    for a, c in zip(rings, rings[1:]):
        if y <= c[0]:
            t = (y - a[0]) / max(1e-6, c[0] - a[0])
            return tuple(a[k] + (c[k] - a[k]) * t for k in (1, 2, 3))
    return rings[-1][1:]


def body_loft(b, rings, sides, disp=None):
    pts = [(0, r[0], r[1]) for r in rings]
    return b.loft(pts, [r[2] for r in rings], [r[3] for r in rings], sides=sides, disp=disp, phase=0.0)


def keel_and_belly(keel=0.04, belly=0.04):
    def disp(i, th, p, c, side, upv):
        s = math.sin(th)
        if s > 0.62:
            p = p + upv * keel * (s - 0.62) / 0.38
        if s < -0.45:
            p = p + upv * belly * (-s - 0.45) / 0.55
        return p
    return disp


def chain_weights(nodes):
    """nodes [(bone, y)] ascending y -> linear blend between neighbours along y."""
    def w(co):
        y = co.y
        if y <= nodes[0][1]:
            return {nodes[0][0]: 1.0}
        for (b0, y0), (b1, y1) in zip(nodes, nodes[1:]):
            if y <= y1:
                t = smooth01((y - y0) / max(1e-6, y1 - y0))
                return {b0: 1 - t, b1: t}
        return {nodes[-1][0]: 1.0}
    return w


# --------------------------------------------------------------------------- Dust Stalker

def build_stalker(clips):
    b = Build("Stalker")
    rig = Rig(b.unit)
    CH = shade((0.05, 0.02, 0.035), (0.21, 0.08, 0.14), (0.54, 0.27, 0.37), sheen=0.75, seed=1.0)
    CH_D = shade((0.03, 0.015, 0.025), (0.13, 0.05, 0.09), (0.34, 0.18, 0.25), sheen=0.6, seed=2.0)
    BN = bone_shade(1.0)
    rig.add("Body", (0, 0.02, 0.66), "Root")
    rig.add("Head", (0, 0.56, 0.75), "Body")
    rig.add("Jaw", (0, 0.74, 0.64), "Head")
    rig.add("Tail", (0, -0.58, 0.64), "Body")
    rig.add("Tail2", (0, -0.98, 0.63), "Tail")

    rings = [(-0.70, 0.62, 0.10, 0.10), (-0.58, 0.63, 0.21, 0.19), (-0.40, 0.66, 0.28, 0.25),
             (-0.18, 0.69, 0.31, 0.28), (0.04, 0.73, 0.33, 0.30), (0.24, 0.76, 0.33, 0.30),
             (0.42, 0.75, 0.28, 0.27), (0.55, 0.73, 0.20, 0.20), (0.63, 0.72, 0.12, 0.13)]

    def torso_w(co):
        y = co.y
        if y < -0.48:
            t = smooth01((-0.48 - y) / 0.22) * 0.7
            return {"Body": 1 - t, "Tail": t}
        if y > 0.48:
            t = smooth01((y - 0.48) / 0.15) * 0.6
            return {"Body": 1 - t, "Head": t}
        return {"Body": 1.0}
    b.part(body_loft(b, rings, 12, keel_and_belly(0.05, 0.05)), CH, weights=torso_w)

    # Head: big wedge skull with a brow ridge, lower jaw, glowing throat.
    head_r = [(0.48, 0.75, 0.17, 0.16), (0.60, 0.79, 0.23, 0.20), (0.73, 0.80, 0.25, 0.18),
              (0.87, 0.76, 0.21, 0.14), (0.99, 0.72, 0.15, 0.10), (1.08, 0.69, 0.08, 0.07), (1.13, 0.68, 0.0, 0.0)]

    def brow(i, th, p, c, side, upv):
        s = math.sin(th)
        if i in (2, 3) and s > 0.2:
            p = p + upv * 0.035 * s + side * 0.02 * math.cos(th)
        if s < -0.3:
            p = p + upv * 0.03 * (-s - 0.3)
        return p
    b.part(body_loft(b, head_r, 10, brow), CH, "Head")
    jaw_r = [(0.70, 0.64, 0.15, 0.06), (0.84, 0.62, 0.15, 0.055), (0.98, 0.62, 0.11, 0.045), (1.08, 0.63, 0.05, 0.03)]
    b.part(body_loft(b, jaw_r, 8), CH_D, "Jaw")
    b.part(b.ellipsoid((0, 0.88, 0.665), (0.17, 0.30, 0.035), u=6, v=4), GLOW, "Head", glow=True)
    for sx in (-1, 1):
        b.part(b.spike((sx * 0.085, 1.00, 0.67), (sx * 0.09, 1.04, 0.55), 0.03, sides=5, bend=(0, 0.01, 0)), BN, "Head")
        b.part(b.spike((sx * 0.075, 1.03, 0.63), (sx * 0.085, 1.08, 0.73), 0.022, sides=4), BN, "Jaw")
        eye(b, (sx * 0.12, 0.93, 0.80), 0.052, "Head", u=8, v=5)
        eye(b, (sx * 0.17, 0.80, 0.85), 0.037, "Head", u=6, v=4)
        b.part(b.spike((sx * 0.13, 0.62, 0.90), (sx * 0.25, 0.34, 1.05), 0.045, sides=5, bend=(sx * 0.03, 0, 0.09)), BN, "Head")

    # Dorsal spines and split bone plates with glowing vents between them.
    for y, h in ((0.40, 0.13), (0.24, 0.19), (0.08, 0.22), (-0.08, 0.21), (-0.24, 0.18), (-0.40, 0.14), (-0.54, 0.10)):
        zc, rx, rz = interp_rings(rings, y)
        zb = zc + rz + 0.03
        col = (lambda zb, h: (lambda co, n: mix((0.05, 0.04, 0.05), BONE, smooth01((co.z - zb) / h) ** 1.4)))(zb, h)
        b.part(b.spike((0, y, zb - 0.03), (0, y - 0.09, zb + h), 0.045, sides=5, bend=(0, 0.02, 0)), col, "Body")
    for y in (0.30, 0.06, -0.20):
        zc, rx, rz = interp_rings(rings, y)
        for a0, a1 in ((0.22, 1.30), (1.84, 2.92)):
            b.part(b.arc_plate((0, zc), rx + 0.03, rz + 0.03, y, 0.17, 0.028, a0, a1, segs=4), BN, "Body")
    for y in (0.18, -0.07):
        zc, rx, rz = interp_rings(rings, y)
        for a0, a1 in ((0.30, 1.25), (1.89, 2.84)):
            b.part(b.arc_plate((0, zc), rx + 0.012, rz + 0.012, y, 0.035, 0.014, a0, a1, segs=4), GLOW, "Body", glow=True)

    # Legs: thick forelimbs with bone bracers, paws with bone claws.
    specs = [("L0", -1, 0.40, 0.52, 0.50, 0.14), ("L1", 1, 0.40, 0.52, 0.50, 0.14),
             ("L2", -1, -0.38, -0.54, -0.40, 0.125), ("L3", 1, -0.38, -0.54, -0.40, 0.125)]
    for name, sx, yh, yk, yf, rh in specs:
        front = yh > 0
        hip = (sx * 0.27, yh, 0.62)
        knee = (sx * (0.40 if front else 0.43), yk, 0.34 if front else 0.40)
        foot = (sx * (0.38 if front else 0.41), yf, 0.06)
        leg(b, rig, name, hip, knee, foot, rh, 0.078, 0.066, CH, sides=7, knee_ball=0.085,
            foot_claws=3, claw_col=BN)
        if front:
            k, f = Vector(knee), Vector(foot)
            b.part(b.loft([k.lerp(f, 0.18), k.lerp(f, 0.45), k.lerp(f, 0.72)], [0.088, 0.094, 0.08], sides=8,
                          up=(0, 1, 0)), BN, name + "_Knee")
        else:
            h_, k = Vector(hip), Vector(knee)
            b.part(b.loft([h_.lerp(k, 0.35), h_.lerp(k, 0.6), h_.lerp(k, 0.85)], [0.11, 0.105, 0.085], sides=8,
                          up=(0, 1, 0)), BN, name + "_Hip")

    # Tail with bone spines.
    tail_pts = [(0, -0.55, 0.64), (0, -0.75, 0.66), (0, -0.95, 0.66), (0, -1.15, 0.64), (0, -1.32, 0.62), (0, -1.44, 0.62)]
    b.part(b.loft(tail_pts, [0.17, 0.14, 0.11, 0.08, 0.05, 0.0], [0.15, 0.12, 0.10, 0.07, 0.045, 0.0], sides=8),
           CH, weights=chain_weights([("Tail2", -1.05), ("Tail", -0.85)]))
    for y, h in ((-0.82, 0.12), (-1.02, 0.10), (-1.20, 0.08)):
        zt = {-0.82: 0.78, -1.02: 0.76, -1.20: 0.71}[y]
        b.part(b.spike((0, y, zt - 0.03), (0, y - 0.08, zt + h), 0.035, sides=4), BN, "Tail2" if y < -1.0 else "Tail")

    LEGS = [("L0", 0.0, True), ("L3", 0.0, False), ("L1", 0.5, True), ("L2", 0.5, False)]
    A = 0.42
    W = 16

    def idle(t):
        p = Pose()
        s = math.sin(2 * math.pi * t)
        p.l("Body", z=0.012 * s).r("Body", x=0.02 * s)
        p.r("Head", x=0.06 * math.sin(4 * math.pi * t), z=0.20 * s)
        p.r("Jaw", x=-0.10 * max(0.0, math.sin(4 * math.pi * t)))
        p.r("Tail", z=0.25 * math.sin(2 * math.pi * t + 1.0), x=0.04 * s)
        p.r("Tail2", z=0.32 * math.sin(2 * math.pi * t + 0.2))
        for n, _, front in LEGS:
            p.r(n + "_Hip", x=-0.02 * s)
        return p

    def walk(t):
        p = Pose()
        p.l("Body", z=-0.035 * math.cos(4 * math.pi * t) - 0.01)
        p.r("Body", x=0.035 * math.sin(4 * math.pi * t), y=0.06 * math.sin(2 * math.pi * t))
        p.r("Head", x=-0.06 * math.sin(4 * math.pi * t), z=0.08 * math.sin(2 * math.pi * t), y=-0.05 * math.sin(2 * math.pi * t))
        p.r("Jaw", x=-0.06 * (0.5 + 0.5 * math.sin(4 * math.pi * t)))
        p.r("Tail", z=0.28 * math.sin(2 * math.pi * t), x=0.06 * math.sin(4 * math.pi * t))
        p.r("Tail2", z=0.36 * math.sin(2 * math.pi * t - 0.9))
        for n, off, front in LEGS:
            sw, lift = gait_phase(t + off)
            hip = A * sw + 0.22 * lift
            knee = (-1.05 if front else 0.85) * lift
            p.r(n + "_Hip", x=hip).r(n + "_Knee", x=knee).r(n + "_Foot", x=-(hip + knee) * 0.8)
        return p

    def strike(t):
        s = strike_curve(t)
        w, h = max(0.0, -s), max(0.0, s)
        p = Pose()
        p.l("Body", y=-0.10 * w + 0.32 * h, z=-0.07 * w - 0.03 * h)
        p.r("Body", x=0.14 * w - 0.16 * h)
        p.r("Head", x=0.28 * w - 0.30 * h)
        p.r("Jaw", x=-0.75 * max(h, 0.35 * w))
        p.r("L0_Hip", x=-0.30 * w + 0.95 * h).r("L1_Hip", x=-0.30 * w + 0.95 * h)
        p.r("L0_Knee", x=-0.6 * w + 0.15 * h).r("L1_Knee", x=-0.6 * w + 0.15 * h)
        p.r("L0_Foot", x=0.5 * w - 0.6 * h).r("L1_Foot", x=0.5 * w - 0.6 * h)
        p.r("L2_Hip", x=0.25 * w - 0.40 * h).r("L3_Hip", x=0.25 * w - 0.40 * h)
        p.r("L2_Knee", x=0.3 * w).r("L3_Knee", x=0.3 * w)
        p.r("Tail", x=0.30 * w - 0.22 * h, z=0.35 * h)
        p.r("Tail2", x=0.20 * w, z=0.45 * h)
        return p

    def down(t):
        d = bounce(t)
        st = math.sin(min(t / 0.3, 1.0) * math.pi) * 0.18
        p = Pose()
        p.l("Body", x=0.14 * d, z=-0.33 * d)
        p.r("Body", y=1.22 * d - st, x=0.10 * d)
        p.r("Head", x=-0.22 * d, y=0.30 * d, z=0.15 * st)
        p.r("Jaw", x=-0.55 * d)
        p.r("Tail", z=-0.45 * d, y=0.2 * d)
        p.r("Tail2", z=-0.55 * d)
        for n, _, front in LEGS:
            top = n in ("L0", "L2")
            p.r(n + "_Hip", x=(0.35 if front else -0.30) * d, y=(0.45 if top else -0.25) * d)
            p.r(n + "_Knee", x=(-0.9 if front else 0.8) * d)
            p.r(n + "_Foot", x=0.3 * d)
        return p

    walk_speed = 2 * 0.56 * math.sin(A) / (0.5 * W / FPS)
    return finish(b, rig, clips, [(32, idle), (W, walk), (18, strike), (28, down)], walk_speed, roughness=0.5)


# --------------------------------------------------------------------------- Ash Hopper

def build_hopper(clips):
    b = Build("Hopper")
    rig = Rig(b.unit, 2.0)
    ASH = shade((0.12, 0.12, 0.13), (0.38, 0.37, 0.36), (0.68, 0.66, 0.63), sheen=0.65, seed=3.0)
    GRA = shade((0.06, 0.06, 0.07), (0.20, 0.20, 0.22), (0.46, 0.46, 0.48), sheen=0.6, seed=4.0)
    BLK = shade((0.04, 0.04, 0.04), (0.12, 0.12, 0.13), (0.34, 0.34, 0.36), sheen=0.5, seed=5.0)
    BN = bone_shade(3.0)
    Z = 1.72
    rig.add("Body", (0, 0, Z), "Root")
    rig.add("Head", (0, 0.50, Z + 0.02), "Body")
    rig.add("Tail", (0, -0.40, Z), "Body")

    ys = [-0.46 + 0.92 * i / 10 for i in range(11)]
    rings = []
    for i, y in enumerate(ys):
        r = 0.37 * max(0.18, math.sqrt(max(0.0, 1 - (y / 0.49) ** 2))) * (0.94 if i % 2 else 1.0)
        rings.append((y, Z + 0.02 * math.cos(y * 3), r, r * 0.86))

    def seg_col(co, n):
        c = ASH(co, n)
        g = 0.5 + 0.5 * math.cos(2 * math.pi * (co.y + 0.46) / 0.184)
        return tuple(v * (0.72 + 0.28 * g) for v in c)
    b.part(body_loft(b, rings, 14, keel_and_belly(0.03, 0.08)), seg_col, "Body")

    # Abdomen with glowing sacs and long cerci.
    ab = [(0, -0.34, Z - 0.02), (0, -0.54, Z - 0.06), (0, -0.74, Z - 0.12), (0, -0.90, Z - 0.17), (0, -0.98, Z - 0.19)]
    b.part(b.loft(ab, [0.24, 0.27, 0.21, 0.11, 0.0], [0.21, 0.23, 0.18, 0.09, 0.0], sides=12), GRA, "Tail")
    for sx in (-1, 1):
        b.part(b.ellipsoid((sx * 0.21, -0.58, Z - 0.05), (0.09, 0.20, 0.12), u=6, v=4), GLOW, "Tail", glow=True)
        b.part(b.spike((sx * 0.05, -0.90, Z - 0.15), (sx * 0.26, -1.48, Z + 0.10), 0.026, sides=4, bend=(0, 0, 0.12)), BLK, "Tail")
        b.part(b.spike((sx * 0.09, -0.84, Z - 0.10), (sx * 0.12, -1.42, Z + 0.32), 0.024, sides=4, bend=(0, 0, 0.10)), BLK, "Tail")

    head_r = [(0.38, Z + 0.02, 0.17, 0.16), (0.50, Z + 0.05, 0.22, 0.20), (0.64, Z + 0.04, 0.21, 0.18),
              (0.77, Z, 0.15, 0.12), (0.86, Z - 0.04, 0.0, 0.0)]
    b.part(body_loft(b, head_r, 10), ASH, "Head")
    for sx in (-1, 1):
        eye(b, (sx * 0.13, 0.68, Z + 0.10), 0.068, "Head", u=8, v=6)
        b.part(b.spike((sx * 0.07, 0.80, Z - 0.06), (sx * 0.03, 0.96, Z - 0.22), 0.028, sides=4, bend=(sx * 0.04, 0.02, 0)), BN, "Head")
        b.part(b.spike((sx * 0.08, 0.74, Z + 0.15), (sx * 0.34, 1.05, Z + 0.56), 0.016, sides=4, bend=(0, -0.05, 0.10)), BLK, "Head")
    b.part(b.arc_plate((0, Z + 0.02), 0.19, 0.165, 0.735, 0.035, 0.013, 0.55, 2.59, segs=6), GLOW, "Head", glow=True)

    legs = []
    for side, sx in (("L", -1), ("R", 1)):
        for i, (yh, yk, yf) in enumerate(((0.22, 0.46, 0.92), (0.0, 0.02, 0.06), (-0.22, -0.44, -0.90))):
            name = f"{side}{i}"
            leg(b, rig, name, (sx * 0.22, yh, Z - 0.12), (sx * 0.76, yk, Z + 0.20), (sx * 0.96, yf, 0.0),
                0.075, 0.062, 0.046, BLK, sides=7, knee_ball=0.088, knee_glow=True)
            legs.append((name, sx, i))
    tripod = {("L", 0), ("R", 1), ("L", 2)}
    A = 0.34
    W = 16

    def idle(t):
        p = Pose()
        s = math.sin(2 * math.pi * t)
        p.l("Body", z=0.025 * s).r("Body", x=0.03 * s, y=0.03 * math.sin(2 * math.pi * t + 1))
        p.r("Head", z=0.22 * math.sin(2 * math.pi * t + 0.5), x=0.08 * math.sin(4 * math.pi * t))
        p.r("Tail", x=-0.06 * s, z=0.10 * math.sin(2 * math.pi * t + 2))
        for name, sx, i in legs:
            p.r(name + "_Hip", y=-sx * 0.03 * s)
        return p

    def walk(t):
        p = Pose()
        p.l("Body", z=0.075 * (0.5 - 0.5 * math.cos(4 * math.pi * t)))
        p.r("Body", y=0.06 * math.sin(2 * math.pi * t), x=-0.04 * math.cos(4 * math.pi * t))
        p.r("Head", x=0.10 * math.cos(4 * math.pi * t), z=0.10 * math.sin(2 * math.pi * t))
        p.r("Tail", z=-0.16 * math.sin(2 * math.pi * t), x=0.06 * math.cos(4 * math.pi * t))
        for name, sx, i in legs:
            off = 0.0 if (name[0], i) in tripod else 0.5
            sw, lift = gait_phase(t + off)
            p.r(name + "_Hip", z=sx * A * sw, y=-sx * 0.42 * lift)
            p.r(name + "_Knee", y=sx * 0.18 * lift)
        return p

    def strike(t):
        s = strike_curve(t, 0.36, 0.48, 0.62)
        w, h = max(0.0, -s), max(0.0, s)
        p = Pose()
        p.l("Body", y=-0.10 * w + 0.28 * h, z=0.12 * w - 0.14 * h)
        p.r("Body", x=0.38 * w - 0.22 * h)
        p.r("Head", x=0.10 * w - 0.40 * h)
        p.r("Tail", x=-0.25 * w + 0.15 * h)
        for name, sx, i in legs:
            if i == 0:
                p.r(name + "_Hip", y=-sx * (1.0 * w - 0.15 * h), z=sx * (0.25 * w + 0.45 * h))
                p.r(name + "_Knee", y=sx * (0.35 * w - 0.25 * h))
            elif i == 2:
                p.r(name + "_Hip", y=sx * 0.12 * w, z=-sx * 0.15 * h)
        return p

    def down(t):
        d = bounce(t, 0.68)
        p = Pose()
        p.l("Body", z=-1.38 * d, y=0.05 * d)
        p.r("Body", y=0.30 * d, x=-0.12 * d)
        p.r("Head", x=-0.35 * d, z=0.25 * d)
        p.r("Tail", x=0.25 * d, z=-0.20 * d)
        curl = smooth01((t - 0.62) / 0.38)
        # Feet stay planted while the body drops: upper leg rises by u, then solve the outward
        # roll a of the lower leg so the foot sits on the ground (rest vectors in the x/z plane).
        u = 0.35 * min(1.0, d)
        knee_z = (Z - 0.12 - 1.38 * d) + (0.54 * math.sin(u) + 0.32 * math.cos(u))
        lo, hi = 0.0, 1.5
        for _ in range(30):
            a = (lo + hi) / 2
            if knee_z + 0.2 * math.sin(a) - 1.92 * math.cos(a) < 0.03:
                lo = a
            else:
                hi = a
        a = (lo + hi) / 2 + 0.35 * curl
        for name, sx, i in legs:
            p.r(name + "_Hip", y=-sx * u, z=sx * (0.15 if i == 0 else -0.10 if i == 2 else 0.0) * d)
            p.r(name + "_Knee", y=-sx * (a - u))
        return p

    walk_speed = 2 * 0.80 * math.sin(A) * 0.85 / (0.5 * W / FPS)
    return finish(b, rig, clips, [(32, idle), (W, walk), (16, strike), (28, down)], walk_speed, roughness=0.55)


# --------------------------------------------------------------------------- Soil Creeper

def build_creeper(clips):
    b = Build("Creeper")
    rig = Rig(b.unit)
    GR = shade((0.06, 0.06, 0.07), (0.22, 0.22, 0.24), (0.52, 0.52, 0.55), sheen=0.7, seed=6.0)
    OL = shade((0.10, 0.12, 0.05), (0.36, 0.40, 0.20), (0.62, 0.66, 0.40), sheen=0.6, seed=7.0)
    BLK = shade((0.04, 0.04, 0.04), (0.12, 0.12, 0.13), (0.34, 0.34, 0.36), sheen=0.5, seed=8.0)
    BN = bone_shade(6.0)
    R = [0.25, 0.30, 0.33, 0.345, 0.335, 0.31]
    parent = "Root"
    seg_y = []
    for i in range(6):
        y = -0.80 + i * 0.32
        seg_y.append(y)
        bone = f"Seg{i}"
        rig.add(bone, (0, y, 0.28), parent)
        parent = bone
        r = R[i]
        rings = [(y + dy, 0.27 + 0.02 * f, r * f, r * f * 0.82) for dy, f in
                 ((-0.21, 0.72), (-0.15, 0.95), (0.0, 1.0), (0.14, 0.95), (0.20, 0.74))]
        b.part(body_loft(b, rings, 10, keel_and_belly(0.025, 0.06)), OL if i in (2, 3) else GR, bone)
        for side, sx in (("a", -1), ("b", 1)):
            leg(b, rig, f"S{i}{side}", (sx * 0.20, y, 0.15), (sx * (r + 0.10), y + 0.04, 0.17),
                (sx * (r + 0.19), y + 0.09, 0.0), 0.05, 0.04, 0.026, BLK, parent=bone, sides=5)
            if 1 <= i <= 4:
                b.part(b.ellipsoid((sx * r * 0.93, y + 0.02, 0.16), (0.05, 0.11, 0.055), u=6, v=3), GLOW, bone, glow=True)
    rig.add("Head", (0, 1.00, 0.27), "Seg5")
    head_r = [(0.86, 0.26, 0.22, 0.18), (0.97, 0.28, 0.24, 0.19), (1.09, 0.26, 0.20, 0.15), (1.19, 0.22, 0.12, 0.09), (1.25, 0.20, 0.0, 0.0)]
    b.part(body_loft(b, head_r, 10, keel_and_belly(0.02, 0.04)), GR, "Head")
    for sx in (-1, 1):
        eye(b, (sx * 0.135, 1.11, 0.33), 0.05, "Head", u=6, v=4)
        eye(b, (sx * 0.17, 1.00, 0.42), 0.045, "Head", u=6, v=4)
        b.part(b.spike((sx * 0.08, 1.19, 0.16), (sx * 0.03, 1.32, 0.07), 0.026, sides=4, bend=(sx * 0.03, 0.01, 0)), BN, "Head")
        b.part(b.spike((sx * 0.06, 1.17, 0.31), (sx * 0.30, 1.44, 0.44), 0.015, sides=4, bend=(sx * 0.02, 0, 0.06)), BLK, "Head")
        b.part(b.curve_tube([(sx * 0.06, -0.98, 0.25), (sx * 0.10, -1.18, 0.31), (sx * 0.16, -1.38, 0.39), (sx * 0.19, -1.54, 0.48)],
                            [0.042, 0.036, 0.026, 0.0], sides=6), GLOW, "Seg0", glow=True)

    W = 16
    A = 0.55

    def idle(t):
        p = Pose()
        for i in range(6):
            p.r(f"Seg{i}", x=0.025 * math.sin(2 * math.pi * t - i * 0.8), z=0.03 * math.sin(2 * math.pi * t + i * 0.6))
        p.r("Head", z=0.22 * math.sin(2 * math.pi * t), x=0.06 * math.sin(4 * math.pi * t))
        return p

    def walk(t):
        p = Pose()
        for i in range(6):
            p.r(f"Seg{i}", z=0.075 * math.sin(2 * math.pi * t + i * 0.9), x=0.02 * math.sin(4 * math.pi * t + i))
            p.l(f"Seg{i}", z=0.012 * math.sin(4 * math.pi * t + i * 0.7))
            for side, sx in (("a", -1), ("b", 1)):
                sw, lift = gait_phase(t + i * 0.16 + (0.0 if side == "a" else 0.5))
                n = f"S{i}{side}"
                p.r(n + "_Hip", z=sx * A * sw, y=-sx * 0.45 * lift)
                p.r(n + "_Knee", y=sx * 0.2 * lift)
        p.r("Head", z=-0.10 * math.sin(2 * math.pi * t + 5.4), x=0.05 * math.sin(4 * math.pi * t))
        return p

    def strike(t):
        s = strike_curve(t)
        w, h = max(0.0, -s), max(0.0, s)
        p = Pose()
        p.r("Seg3", x=0.16 * w - 0.06 * h)
        p.r("Seg4", x=0.28 * w - 0.14 * h)
        p.r("Seg5", x=0.30 * w - 0.20 * h)
        p.l("Seg5", y=-0.04 * w + 0.12 * h)
        p.r("Head", x=0.25 * w - 0.40 * h)
        p.r("Seg0", x=-0.10 * w + 0.05 * h)
        for side, sx in (("a", -1), ("b", 1)):
            for i in (4, 5):
                p.r(f"S{i}{side}_Hip", z=sx * 0.5 * w, y=-sx * 0.6 * w)
        return p

    def down(t):
        # Pillbug curl: ventral bend per joint, tail segment pitched up so the ball rests on the
        # ground. Chain FK (in the y/z plane) keeps every segment centre >= its half height.
        d = bounce(t, 0.7)
        th0, bend = 2.1 * d, 0.86 * d
        py, pz = -0.80, 0.28
        cy, cz = [], []
        for k in range(6):
            ang = th0 - bend * k
            ny, nz_ = py + 0.32 * math.cos(ang), pz + 0.32 * math.sin(ang)
            cy.append((py + ny) / 2); cz.append((pz + nz_) / 2)
            py, pz = ny, nz_
        lift = max(0.0, max(R[k] * 0.82 + 0.02 - cz[k] for k in range(6)))
        shift = -(sum(cy) / 6 - (-0.80 + 0.16 + 0.32 * 2.5)) * 0.85
        p = Pose()
        p.r("Seg0", x=th0, y=0.20 * d)
        p.l("Seg0", z=lift, y=shift)
        for i in range(1, 6):
            p.r(f"Seg{i}", x=-bend)
        p.r("Head", x=-0.70 * d)
        for i in range(6):
            for side, sx in (("a", -1), ("b", 1)):
                n = f"S{i}{side}"
                p.r(n + "_Hip", y=sx * 0.75 * d, z=sx * 0.1 * math.sin(i + t * 9) * d * (1 - t))
                p.r(n + "_Knee", y=sx * 0.6 * d)
        return p

    walk_speed = 2 * 0.30 * math.sin(A) / (0.5 * W / FPS)
    return finish(b, rig, clips, [(32, idle), (W, walk), (16, strike), (30, down)], walk_speed, roughness=0.5)


# --------------------------------------------------------------------------- Rock Tick

def build_tick(clips):
    b = Build("Tick")
    rig = Rig(b.unit)
    GR = shade((0.08, 0.07, 0.06), (0.28, 0.25, 0.22), (0.58, 0.53, 0.47), sheen=0.7, seed=9.0)
    BR = shade((0.12, 0.07, 0.04), (0.38, 0.25, 0.15), (0.60, 0.45, 0.30), sheen=0.5, seed=10.0)
    BLK = shade((0.04, 0.04, 0.04), (0.12, 0.115, 0.115), (0.34, 0.32, 0.32), sheen=0.5, seed=11.0)
    ORG = shade((0.30, 0.07, 0.015), (0.82, 0.30, 0.05), (1.0, 0.64, 0.32), sheen=0.6, seed=12.0, amp=0.06)
    rig.add("Body", (0, 0, 0.40), "Root")
    rings = [(-0.44, 0.36, 0.22, 0.10), (-0.36, 0.40, 0.46, 0.20), (-0.20, 0.43, 0.62, 0.25), (0.0, 0.44, 0.67, 0.26),
             (0.18, 0.43, 0.63, 0.24), (0.32, 0.40, 0.52, 0.20), (0.42, 0.37, 0.32, 0.13), (0.46, 0.35, 0.0, 0.0)]

    def rim(i, th, p, c, side, upv):
        s, co = math.sin(th), math.cos(th)
        if abs(co) > 0.8 and s > -0.4:
            k = (abs(co) - 0.8) / 0.2
            p = p + side * (0.05 * k * (1 if co > 0 else -1)) - upv * 0.035 * k
        if s > 0.75:
            p = p + upv * 0.03 * (s - 0.75) / 0.25
        if s < -0.3:
            p = p + upv * 0.10 * (-s - 0.3)
        return p
    b.part(body_loft(b, rings, 16, rim), GR, "Body")
    b.part(b.ellipsoid((0, 0.0, 0.27), (0.92, 0.72, 0.24), u=10, v=5), BR, "Body")
    b.part(b.ellipsoid((0, 0.40, 0.29), (0.36, 0.14, 0.17), u=8, v=4), BR, "Body")
    b.part(b.spike((0, -0.04, 0.64), (0, -0.22, 1.02), 0.08, sides=6, bend=(0, 0.04, 0.0)), GR, "Body")
    for sx in (-1, 1):
        b.part(b.spike((sx * 0.66, 0.10, 0.40), (sx * 0.88, 0.16, 0.45), 0.05, sides=5), GR, "Body")
        eye(b, (sx * 0.15, 0.43, 0.47), 0.058, "Body", u=8, v=5)
        b.part(b.ellipsoid((sx * 0.23, -0.12, 0.665), (0.10, 0.19, 0.03), u=6, v=4), GLOW, "Body", glow=True)
        for y, xf, hgt in ((0.22, 0.42, 0.07), (0.02, 0.62, 0.09), (-0.22, 0.48, 0.08), (-0.30, 0.20, 0.06)):
            zc, rx, rz = interp_rings(rings, y)
            x = rx * xf
            z = zc + rz * math.sqrt(max(0.0, 1 - xf * xf)) - 0.01
            nrm = Vector((x / (rx * rx), 0, (z - zc) / (rz * rz))).normalized()
            base = Vector((sx * x, y, z))
            nrm.x *= sx
            b.part(b.spike(base, base + nrm * hgt + Vector((0, -0.02, 0)), 0.055, sides=5, mid=0.8), GR, "Body")
    zc, rx, rz = interp_rings(rings, 0.20)
    b.part(b.arc_plate((0, zc), rx + 0.004, rz + 0.004, 0.20, 0.03, 0.012, 0.35, 2.79, segs=8), GLOW, "Body", glow=True)

    # Pincers: graphite arm, big orange claw, movable finger.
    for side, sx in (("L", -1), ("R", 1)):
        pin, claw, fin = "Pin" + side, "Claw" + side, "Finger" + side
        rig.add(pin, (sx * 0.28, 0.32, 0.33), "Body")
        rig.add(claw, (sx * 0.52, 0.62, 0.30), pin)
        rig.add(fin, (sx * 0.46, 0.74, 0.33), claw)
        b.part(b.loft([(sx * 0.26, 0.30, 0.32), (sx * 0.40, 0.47, 0.35), (sx * 0.52, 0.62, 0.30)], [0.08, 0.078, 0.07],
                      sides=7, up=(0, 0, 1)), GR, pin)
        hand_col = (lambda co, n: mix(GR(co, n), ORG(co, n), smooth01((co.y - 0.60) / 0.16)))
        b.part(b.loft([(sx * 0.52, 0.60, 0.30), (sx * 0.56, 0.74, 0.30), (sx * 0.58, 0.88, 0.28), (sx * 0.57, 1.02, 0.25),
                       (sx * 0.55, 1.11, 0.22)], [0.085, 0.135, 0.125, 0.07, 0.0], [0.07, 0.10, 0.095, 0.055, 0.0], sides=9),
               hand_col, claw)
        b.part(b.spike((sx * 0.47, 0.76, 0.33), (sx * 0.44, 1.07, 0.30), 0.05, sides=6, bend=(-sx * 0.05, 0, 0.03)), ORG, fin)

    legs = []
    for i, (yh, dy) in enumerate(((0.22, 0.10), (0.06, 0.03), (-0.10, -0.04), (-0.26, -0.10))):
        for s, sx in ((0, -1), (1, 1)):
            n = f"T{i}{s}"
            leg(b, rig, n, (sx * 0.40, yh, 0.34), (sx * 0.74, yh + dy, 0.50), (sx * 0.95, yh + 2.2 * dy, 0.0),
                0.066, 0.056, 0.036, BLK, sides=6, knee_ball=0.062)
            legs.append((n, sx, i, s))
    W = 12
    A = 0.32

    def idle(t):
        p = Pose()
        s = math.sin(2 * math.pi * t)
        p.l("Body", z=0.01 * s).r("Body", x=0.02 * s, z=0.04 * math.sin(2 * math.pi * t + 1))
        for side, sx in (("L", -1), ("R", 1)):
            p.r("Pin" + side, x=0.08 * math.sin(2 * math.pi * t + (0 if sx < 0 else 2)))
            clack = max(0.0, math.sin(4 * math.pi * t + (0 if sx < 0 else 1.5))) ** 4
            p.r("Finger" + side, z=sx * 0.35 * clack)
        return p

    def walk(t):
        p = Pose()
        p.l("Body", z=0.02 * math.cos(8 * math.pi * t))
        p.r("Body", y=0.06 * math.sin(2 * math.pi * t), z=0.05 * math.sin(2 * math.pi * t + 0.5))
        for side, sx in (("L", -1), ("R", 1)):
            p.r("Pin" + side, x=0.10 + 0.08 * math.sin(2 * math.pi * t + (0 if sx < 0 else math.pi)))
            p.r("Finger" + side, z=sx * 0.15 * (0.5 + 0.5 * math.sin(4 * math.pi * t)))
        for n, sx, i, s in legs:
            sw, lift = gait_phase(t + i * 0.25 + s * 0.5)
            p.r(n + "_Hip", z=sx * A * sw, y=-sx * 0.40 * lift)
            p.r(n + "_Knee", y=sx * 0.18 * lift)
        return p

    def strike(t):
        s = strike_curve(t, 0.34, 0.46, 0.62)
        w, h = max(0.0, -s), max(0.0, s)
        p = Pose()
        p.l("Body", y=-0.05 * w + 0.16 * h, z=0.03 * w)
        p.r("Body", x=0.10 * w - 0.10 * h)
        for side, sx in (("L", -1), ("R", 1)):
            p.r("Pin" + side, x=0.65 * w - 0.25 * h, z=-sx * 0.35 * w + sx * 0.30 * h)
            p.r("Claw" + side, x=0.25 * w - 0.15 * h)
            p.r("Finger" + side, z=sx * 0.70 * w - sx * 0.10 * h)
        for n, sx, i, s_ in legs:
            if i >= 2:
                p.r(n + "_Hip", z=-sx * 0.12 * h)
        return p

    def down(t):
        d = bounce(t, 0.66)
        p = Pose()
        p.l("Body", z=-0.15 * d)
        p.r("Body", y=0.28 * d, x=0.14 * d)
        for side, sx in (("L", -1), ("R", 1)):
            p.r("Pin" + side, x=-0.45 * d, z=-sx * 0.25 * d)
            p.r("Claw" + side, x=-0.2 * d)
            p.r("Finger" + side, z=sx * 0.35 * d)
        for n, sx, i, s in legs:
            curl = smooth01((t - 0.45) / 0.55)
            p.r(n + "_Hip", y=-sx * 0.45 * d + sx * 0.10 * curl, z=sx * 0.1 * (i - 1.5) * d)
            p.r(n + "_Knee", y=sx * (0.25 * d + 0.85 * curl))
        return p

    walk_speed = 2 * 0.56 * math.sin(A) * 0.9 / (0.5 * W / FPS)
    return finish(b, rig, clips, [(32, idle), (W, walk), (18, strike), (28, down)], walk_speed, roughness=0.5)


# --------------------------------------------------------------------------- Regolith Mite

def build_mite(clips):
    b = Build("Mite")
    rig = Rig(b.unit)
    DU = shade((0.20, 0.15, 0.10), (0.60, 0.48, 0.34), (0.84, 0.74, 0.58), sheen=0.5, seed=13.0)
    GR = shade((0.06, 0.06, 0.07), (0.24, 0.24, 0.26), (0.55, 0.55, 0.58), sheen=0.75, seed=14.0)
    BLK = shade((0.04, 0.04, 0.04), (0.12, 0.115, 0.115), (0.34, 0.32, 0.32), sheen=0.5, seed=15.0)
    BN = bone_shade(13.0)
    Z = 0.28
    rig.add("Body", (0, 0, Z), "Root")
    rig.add("Head", (0, 0.44, Z - 0.03), "Body")
    ys = [-0.52 + 0.98 * i / 10 for i in range(11)]
    rings = [(y, Z, 0.37 * max(0.2, (1 - ((y + 0.03) / 0.50) ** 2)) ** 0.5, 0.35 * max(0.2, (1 - ((y + 0.03) / 0.50) ** 2)) ** 0.5)
             for y in ys]
    b.part(body_loft(b, rings, 18, keel_and_belly(0.0, 0.12)), DU, "Body")
    for y in (0.30, 0.14, -0.02, -0.18, -0.34):
        zc, rx, rz = interp_rings(rings, y)
        b.part(b.arc_plate((0, zc), rx + 0.022, rz + 0.022, y, 0.175, 0.03, 0.28, 2.86, segs=6), GR, "Body")
    b.part(b.loft([(0, -0.47, Z + 0.02), (0, -0.56, Z + 0.0), (0, -0.63, Z - 0.03)], [0.20, 0.18, 0.12], [0.10, 0.08, 0.04],
                  sides=10), GR, "Body")
    for sx in (-1, 1):
        for y in (-0.10, 0.12):
            zc, rx, rz = interp_rings(rings, y)
            b.part(b.ellipsoid((sx * rx * 0.96, y, Z - 0.07), (0.05, 0.09, 0.065), u=6, v=3), GLOW, "Body", glow=True)
    head_r = [(0.38, Z - 0.03, 0.17, 0.15), (0.49, Z - 0.02, 0.19, 0.16), (0.59, Z - 0.04, 0.15, 0.12), (0.66, Z - 0.06, 0.0, 0.0)]
    b.part(body_loft(b, head_r, 10), DU, "Head")
    eye(b, (0, 0.62, Z), 0.052, "Head", u=8, v=5)
    for sx in (-1, 1):
        eye(b, (sx * 0.095, 0.59, Z - 0.02), 0.034, "Head", u=6, v=4)
        b.part(b.spike((sx * 0.06, 0.63, Z - 0.12), (sx * 0.02, 0.76, Z - 0.18), 0.025, sides=4, bend=(sx * 0.03, 0.01, 0)), BN, "Head")
    legs = []
    for i, (yh, dy) in enumerate(((0.20, 0.07), (0.0, 0.0), (-0.20, -0.07))):
        for s, sx in ((0, -1), (1, 1)):
            n = f"M{i}{s}"
            leg(b, rig, n, (sx * 0.24, yh, 0.16), (sx * 0.41, yh + dy, 0.23), (sx * 0.50, yh + 1.7 * dy, 0.0),
                0.05, 0.042, 0.03, BLK, sides=6, knee_ball=0.046)
            legs.append((n, sx, i, s))
    tripod = {(0, 0), (1, 1), (2, 0)}
    W = 10
    A = 0.45

    def idle(t):
        p = Pose()
        s = math.sin(2 * math.pi * t)
        p.l("Body", z=0.008 * s).r("Body", x=0.025 * s, y=0.02 * math.sin(2 * math.pi * t + 2))
        p.r("Head", z=0.25 * math.sin(2 * math.pi * t), x=0.08 * math.sin(4 * math.pi * t))
        for n, sx, i, s_ in legs:
            p.r(n + "_Hip", z=sx * 0.06 * math.sin(4 * math.pi * t + i))
        return p

    def walk(t):
        p = Pose()
        p.l("Body", z=0.022 * (0.5 - 0.5 * math.cos(4 * math.pi * t)))
        p.r("Body", y=0.08 * math.sin(2 * math.pi * t), z=0.06 * math.sin(2 * math.pi * t + 0.4))
        p.r("Head", x=0.08 * math.cos(4 * math.pi * t), z=-0.10 * math.sin(2 * math.pi * t))
        for n, sx, i, s in legs:
            sw, lift = gait_phase(t + (0.0 if (i, s) in tripod else 0.5))
            p.r(n + "_Hip", z=sx * A * sw, y=-sx * 0.50 * lift)
            p.r(n + "_Knee", y=sx * 0.2 * lift)
        return p

    def strike(t):
        s = strike_curve(t, 0.34, 0.46, 0.60)
        w, h = max(0.0, -s), max(0.0, s)
        hop = math.sin(math.pi * max(0.0, min(1.0, (t - 0.32) / 0.30)))
        p = Pose()
        p.l("Body", y=-0.07 * w + 0.24 * h, z=-0.05 * w + 0.12 * hop)
        p.r("Body", x=0.18 * w - 0.14 * h)
        p.r("Head", x=0.28 * w - 0.45 * h)
        for n, sx, i, s_ in legs:
            p.r(n + "_Hip", y=sx * 0.25 * w - sx * 0.35 * hop, z=sx * (0.3 if i == 0 else -0.25 if i == 2 else 0) * hop)
        return p

    def down(t):
        e = ease_in_out(min(1.0, t / 0.62))
        hop = math.sin(math.pi * min(1.0, t / 0.55))
        curl = smooth01((t - 0.5) / 0.5)
        p = Pose()
        p.l("Body", z=0.24 * hop + 0.10 * e, x=-0.08 * e)
        p.r("Body", y=-math.pi * e + 0.10 * math.sin(math.pi * curl) * (1 - curl), x=0.05 * e)
        p.r("Head", x=0.35 * e, z=0.15 * curl)
        for n, sx, i, s in legs:
            tw = 0.18 * math.sin(t * 40 + i * 2) * (1 - curl) * e
            p.r(n + "_Hip", y=-sx * 0.25 * e + tw, z=sx * 0.12 * (1 - i) * e)
            p.r(n + "_Knee", y=sx * 1.0 * curl)
        return p

    walk_speed = 2 * 0.30 * math.sin(A) / (0.5 * W / FPS)
    return finish(b, rig, clips, [(32, idle), (W, walk), (16, strike), (30, down)], walk_speed, roughness=0.6)


# --------------------------------------------------------------------------- Watt Leech

def build_leech(clips):
    b = Build("Leech")
    rig = Rig(b.unit)
    WH = shade((0.56, 0.58, 0.61), (0.88, 0.89, 0.91), (0.98, 0.98, 0.99), sheen=0.7, seed=16.0, amp=0.04)
    DK = shade((0.03, 0.03, 0.04), (0.08, 0.08, 0.10), (0.20, 0.20, 0.24), sheen=0.5, seed=17.0)
    nodes = [("Rib0", -0.70), ("Rib1", -0.35), ("Rib2", 0.0), ("Rib3", 0.35), ("Rib4", 0.62), ("Head", 0.80)]
    parent = "Root"
    for n, y in nodes:
        rig.add(n, (0, y, 0.19), parent)
        parent = n
    W8 = chain_weights(nodes)
    ys = [-1.0 + 1.98 * i / 16 for i in range(17)]

    def prof(y):
        u = (y + 1.0) / 1.98
        return max(0.0, math.sin(math.pi * min(1.0, u * 1.0)) ** 0.65 * (0.75 + 0.25 * u))
    rings = []
    for y in ys:
        k = prof(y)
        if y >= 0.97:
            k = 0.0
        rings.append((y, 0.19 + 0.015 * math.sin(math.pi * (y + 1) / 1.98), 0.34 * k, 0.18 * k))

    def groove(i, th, p, c, side, upv):
        s = math.sin(th)
        if s > 0.93:
            p = p - upv * 0.05 * (s - 0.93) / 0.07
        elif s > 0.72:
            p = p + upv * 0.03 * (1 - abs(s - 0.83) / 0.11)
        if s < -0.4:
            p = p + upv * 0.06 * (-s - 0.4) / 0.6
        return p
    b.part(body_loft(b, rings, 24, groove), WH, weights=W8)
    gy = [-0.78 + 1.52 * i / 11 for i in range(12)]
    gpts, grx, grz = [], [], []
    for y in gy:
        zc, rx, rz = interp_rings(rings, y)
        gpts.append((0, y, zc + rz - 0.035))
        k = 0.6 + 0.4 * math.sin(math.pi * (y + 0.78) / 1.52)
        grx.append(0.05 * k)
        grz.append(0.024 * k)
    b.part(b.loft(gpts, grx, grz, sides=6), GLOW, weights=W8, glow=True)
    for side, sx in (("L", -1), ("R", 1)):
        rig.add("Fin" + side, (sx * 0.24, 0.30, 0.16), "Rib3")
        rig.add("FinB" + side, (sx * 0.20, -0.40, 0.15), "Rib1")
        b.part(b.curve_tube([(sx * 0.22, 0.32, 0.16), (sx * 0.42, 0.20, 0.13), (sx * 0.58, 0.04, 0.10), (sx * 0.72, -0.12, 0.08)],
                            [0.09, 0.085, 0.06, 0.0], sides=6, flatten=0.2), WH, "Fin" + side)
        b.part(b.curve_tube([(sx * 0.18, -0.38, 0.15), (sx * 0.34, -0.50, 0.12), (sx * 0.46, -0.62, 0.10), (sx * 0.55, -0.74, 0.08)],
                            [0.07, 0.065, 0.045, 0.0], sides=6, flatten=0.2), WH, "FinB" + side)
        b.part(b.curve_tube([(sx * 0.02, -0.93, 0.19), (sx * 0.08, -1.04, 0.195), (sx * 0.15, -1.18, 0.20)],
                            [0.06, 0.05, 0.0], sides=6, flatten=0.3), WH, "Rib0")
        b.part(b.spike((sx * 0.10, 0.86, 0.17), (sx * 0.15, 1.20, 0.11), 0.042, sides=6, bend=(sx * 0.03, 0, 0.01)), WH, "Head")
        eye(b, (sx * 0.13, 0.84, 0.24), 0.046, "Head", u=6, v=4)
        for y in (-0.55, -0.30, -0.05, 0.20, 0.45):
            zc, rx, rz = interp_rings(rings, y)
            x0 = rx * 0.80
            bone = min(nodes, key=lambda nd: abs(nd[1] - y))[0]
            b.part(b.loft([(sx * (x0 - 0.02), y, 0.10), (sx * (x0 + 0.05), y, 0.10)], [0.05, 0.05], sides=8, up=(0, 1, 0)),
                   DK, bone)
        for y in (-0.42, 0.08):
            zc, rx, rz = interp_rings(rings, y)
            bone = min(nodes, key=lambda nd: abs(nd[1] - y))[0]
            b.part(b.ellipsoid((sx * 0.13, y, zc + rz - 0.005), (0.045, 0.07, 0.04), u=6, v=3), GLOW, bone, glow=True)
    W = 20
    ribs = ["Rib0", "Rib1", "Rib2", "Rib3", "Rib4"]

    def idle(t):
        p = Pose()
        for i, r in enumerate(ribs):
            p.r(r, x=0.02 * math.sin(2 * math.pi * t - i * 0.9), z=0.03 * math.sin(2 * math.pi * t - i * 0.7))
        p.r("Head", z=0.18 * math.sin(2 * math.pi * t + 1), x=0.06 * math.sin(4 * math.pi * t))
        for side, sx in (("L", -1), ("R", 1)):
            p.r("Fin" + side, y=sx * 0.12 * math.sin(2 * math.pi * t))
            p.r("FinB" + side, y=sx * 0.10 * math.sin(2 * math.pi * t + 1.2))
        return p

    def walk(t):
        p = Pose()
        for i, r in enumerate(ribs):
            p.r(r, z=0.15 * math.sin(2 * math.pi * t - i * 0.95), x=0.045 * math.sin(4 * math.pi * t - i * 1.1))
        p.r("Head", z=-0.12 * math.sin(2 * math.pi * t - 5 * 0.95 + 0.8))
        p.l("Rib0", z=0.01 * math.sin(4 * math.pi * t))
        for side, sx in (("L", -1), ("R", 1)):
            p.r("Fin" + side, y=sx * 0.38 * math.sin(2 * math.pi * t), z=-sx * 0.15 * math.cos(2 * math.pi * t))
            p.r("FinB" + side, y=sx * 0.30 * math.sin(2 * math.pi * t - 1.4))
        return p

    def strike(t):
        s = strike_curve(t)
        w, h = max(0.0, -s), max(0.0, s)
        p = Pose()
        p.l("Rib0", y=-0.05 * w + 0.10 * h)
        p.l("Rib2", y=0.10 * h)
        p.r("Rib2", x=0.10 * w)
        p.r("Rib3", x=0.30 * w - 0.12 * h)
        p.r("Rib4", x=0.30 * w - 0.20 * h)
        p.r("Head", x=0.25 * w - 0.45 * h)
        p.r("Rib1", z=0.10 * h)
        for side, sx in (("L", -1), ("R", 1)):
            p.r("Fin" + side, y=sx * 0.55 * w - sx * 0.2 * h)
        return p

    def down(t):
        d = bounce(t, 0.7)
        p = Pose()
        p.r("Rib0", y=2.5 * d)
        p.l("Rib0", z=0.03 * d)
        for i, r in enumerate(ribs[1:], 1):
            p.r(r, z=0.22 * d * (1 if i % 2 else -1), x=0.08 * d)
        p.r("Head", x=0.3 * d, z=0.3 * d)
        for side, sx in (("L", -1), ("R", 1)):
            p.r("Fin" + side, y=-sx * 0.35 * d)
            p.r("FinB" + side, y=-sx * 0.30 * d)
        return p

    walk_speed = 0.85 / (W / FPS)
    return finish(b, rig, clips, [(32, idle), (W, walk), (18, strike), (28, down)], walk_speed, chitin=False, roughness=0.35)


# --------------------------------------------------------------------------- Ice Wisp

def build_wisp(clips):
    b = Build("Wisp")
    rig = Rig(b.unit)
    Z = 1.15
    rig.add("Body", (0, 0, Z), "Root")

    def ice(base_d, L):
        def fn(co, n):
            d = (Vector(co) - Vector((0, 0, Z))).length / L
            c = mix((0.20, 0.36, 0.58), (0.90, 0.96, 1.0), smooth01(d * 1.1))
            k = 0.82 + 0.18 * max(0.0, n.z) + 0.06 * nz(co, 7.0, 3.0)
            return tuple(v * k for v in c)
        return fn
    DK = shade((0.02, 0.03, 0.05), (0.06, 0.08, 0.12), (0.22, 0.28, 0.36), sheen=0.6, seed=18.0)
    dirs = [(Vector((0, 0, 1)), 0.88)]
    for k in range(3):
        a = math.radians(90 + 120 * k)
        e = math.radians(18)
        dirs.append((Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))), 0.78))
    for k in range(3):
        a = math.radians(150 + 120 * k)
        e = math.radians(-36)
        dirs.append((Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))), 0.70))
    c0 = Vector((0, 0, Z))
    for k, (d, L) in enumerate(dirs):
        bone = f"Shard{k}"
        rig.add(bone, c0, "Body")
        up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((0, 1, 0))
        pts = [c0 + d * (L * f) for f in (0.10, 0.30, 0.55, 0.80, 1.0)]
        b.part(b.loft(pts, [0.06, 0.13, 0.115, 0.07, 0.0], sides=8, up=up, phase=math.pi / 8), ice(0, L), bone, smooth=False)
        side = d.cross(up).normalized()
        for j, (f, sgn, ln) in enumerate(((0.35, 1, 0.24), (0.55, -1, 0.18))):
            base = c0 + d * (L * f) + side * sgn * 0.07
            tip = base + (d * 0.6 + side * sgn * 0.8).normalized() * ln
            b.part(b.spike(base, tip, 0.04, sides=5, up=up), ice(0, L), bone, smooth=False)
        if k > 0:
            nrm = side.cross(d).normalized()
            if nrm.z < 0:
                nrm = -nrm
            pod = c0 + d * (L * 0.45) + nrm * 0.10
            b.part(b.ellipsoid(pod, (0.09, 0.09, 0.07), u=6, v=4,
                               rot=d.to_track_quat("Y", "Z").to_matrix()), DK, bone)
            b.part(b.ellipsoid(pod + nrm * 0.035 + d * 0.02, (0.045, 0.06, 0.04), u=6, v=3), GLOW, bone, glow=True)
    b.part(b.ellipsoid(c0, (0.32, 0.32, 0.36), u=10, v=7), GLOW, "Body", glow=True, smooth=False)
    ring = [(math.cos(2 * math.pi * i / 12) * 0.25, math.sin(2 * math.pi * i / 12) * 0.25, Z) for i in range(12)]
    ring_pts = [Vector(p) for p in ring] + [Vector(ring[0])]
    b.part(b.loft(ring_pts, [0.035] * 13, [0.06] * 13, sides=5, up=(0, 0, 1), cap0=False, cap1=False), DK, "Body", smooth=False)
    for k in range(3):
        a = math.radians(30 + 120 * k)
        base = c0 + Vector((math.cos(a) * 0.08, math.sin(a) * 0.08, -0.14))
        b.part(b.spike(base, base + Vector((0, 0, -0.26)), 0.035, sides=5), ice(0, 0.6), "Body", smooth=False)
    # Frost glow ring on the ground (stays at ground height after SnapToGround).
    bm = b.bm
    segs = 48
    inner, outer = [], []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        inner.append(bm.verts.new((math.cos(a) * 0.60, math.sin(a) * 0.60, 0.015)))
        outer.append(bm.verts.new((math.cos(a) * 0.67, math.sin(a) * 0.67, 0.015)))
    rf = [bm.faces.new((inner[i], outer[i], outer[(i + 1) % segs], inner[(i + 1) % segs])) for i in range(segs)]
    b.part(rf, GLOW, "Root", glow=True)

    def shard_dir(k):
        return dirs[k][0]

    def tilt(k, ang):
        d = shard_dir(k)
        ax = d.cross(Vector((0, 0, 1)))
        if ax.length < 1e-4:
            ax = Vector((1, 0, 0))
        e = Matrix.Rotation(ang, 3, ax.normalized()).to_euler("XYZ")
        return e.x, e.y, e.z

    W = 24

    def idle(t):
        p = Pose()
        p.l("Body", z=0.06 * math.sin(2 * math.pi * t)).r("Body", z=0.30 * math.sin(2 * math.pi * t), x=0.05 * math.sin(4 * math.pi * t))
        for k in range(7):
            d = shard_dir(k) * (0.035 * math.sin(4 * math.pi * t + k * 0.9))
            p.l(f"Shard{k}", x=d.x, y=d.y, z=d.z)
        return p

    def walk(t):
        p = Pose()
        p.l("Body", z=0.07 * math.sin(4 * math.pi * t), y=0.03 * math.sin(2 * math.pi * t))
        p.r("Body", x=-0.22 + 0.04 * math.sin(4 * math.pi * t), z=0.55 * math.sin(2 * math.pi * t), y=0.08 * math.sin(2 * math.pi * t))
        for k in range(7):
            d = shard_dir(k) * (0.05 * math.sin(4 * math.pi * t + k * 0.9))
            p.l(f"Shard{k}", x=d.x, y=d.y, z=d.z)
        return p

    def strike(t):
        s = strike_curve(t, 0.36, 0.46, 0.60)
        w, h = max(0.0, -s), max(0.0, s)
        p = Pose()
        p.l("Body", y=-0.06 * w + 0.38 * h, z=0.08 * w - 0.10 * h)
        p.r("Body", x=0.10 * w - 0.32 * h, z=-0.45 * w + 0.9 * h)
        for k in range(7):
            d = shard_dir(k) * (-0.11 * w + 0.24 * h)
            p.l(f"Shard{k}", x=d.x, y=d.y, z=d.z)
        return p

    def down(t):
        d = bounce(t, 0.7)
        crack = math.sin(min(1.0, t / 0.22) * math.pi * 3) * 0.08 * (1 - min(1.0, t / 0.22))
        p = Pose()
        p.l("Body", z=-0.82 * d)
        p.r("Body", x=0.30 * d + crack, y=0.18 * d)
        for k in range(7):
            ang = -1.1 if k == 0 else (-0.45 if k <= 3 else 0.62)
            rx, ry, rz = tilt(k, ang * d)
            p.r(f"Shard{k}", x=rx, y=ry, z=rz)
            dd = shard_dir(k) * (0.06 * d)
            p.l(f"Shard{k}", x=dd.x, y=dd.y, z=dd.z)
        return p

    return finish(b, rig, clips, [(32, idle), (W, walk), (16, strike), (28, down)], 1.40, chitin=False, roughness=0.25)


# --------------------------------------------------------------------------- entry points

BUILDERS = {
    "Stalker": build_stalker, "Hopper": build_hopper, "Creeper": build_creeper, "Tick": build_tick,
    "Mite": build_mite, "Leech": build_leech, "Wisp": build_wisp,
}


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def build_all(keys=None, prefixed=False):
    out = {}
    for key in keys or BUILDERS:
        clips = [f"{key}_{c}" for c in CLIPS] if prefixed else list(CLIPS)
        out[key] = BUILDERS[key](clips)
    return out


# --------------------------------------------------------------------------- review renders

LINEUP_ORDER = ("Stalker", "Hopper", "Creeper", "Tick", "Mite", "Leech", "Wisp")


def _review_world(fast):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 24 if fast else 96
    sc.cycles.use_denoising = True
    try:
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    world = bpy.data.worlds.new("EarthSky")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.50, 0.66, 0.90, 1.0)
    bg.inputs["Strength"].default_value = 0.9
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.6
    sun.data.color = (1.0, 0.95, 0.86)
    sun.data.angle = math.radians(4)
    sun.rotation_euler = (math.radians(48), 0.0, math.radians(30))
    sc.collection.objects.link(sun)
    # Grass ground: noise-mottled green, like the game's Earth temp scene.
    me = bpy.data.meshes.new("Grass")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=60)
    bm.to_mesh(me)
    bm.free()
    g = bpy.data.objects.new("Grass", me)
    sc.collection.objects.link(g)
    mat = bpy.data.materials.new("GrassMat")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.95
    noise_n = nt.nodes.new("ShaderNodeTexNoise")
    noise_n.inputs["Scale"].default_value = 3.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.10, 0.20, 0.045, 1)
    ramp.color_ramp.elements[1].color = (0.20, 0.33, 0.08, 1)
    nt.links.new(noise_n.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    me.materials.append(mat)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def _aim(cam, target, pitch_deg, yaw_deg, dist=40.0):
    p, y = math.radians(pitch_deg), math.radians(yaw_deg)
    d = Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p)))
    cam.location = Vector(target) + d * dist
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()


def _use_clip(arm, clip, t):
    for a in bpy.data.actions:
        if a.name == clip or (a.name.split(".")[0] == clip and arm.name in [s.name_display for s in a.slots]):
            arm.animation_data.action = a
            arm.animation_data.action_slot = a.slots[0]
            lo, hi = a.frame_range
            return int(round(lo + (hi - lo) * t))
    return 1


def review(out: Path, fast=False, strips=("Stalker", "Hopper")):
    """Blender lineup render on grass + Walk/Strike/Down frame strips (PNG)."""
    global BAKE_TEXTURES
    BAKE_TEXTURES = False
    bpy.ops.wm.read_factory_settings(use_empty=True)
    out.mkdir(parents=True, exist_ok=True)
    res = build_all(LINEUP_ORDER, prefixed=True)
    cam = _review_world(fast)
    sc = bpy.context.scene
    # Lineup: game-like overseer view (ortho, pitch 30, creatures facing the camera's left).
    spacing = 2.9
    for i, key in enumerate(LINEUP_ORDER):
        arm = res[key][0]
        arm.location = ((i - 3) * spacing, 0.0, 0.0)
        arm.rotation_euler = (0.0, 0.0, math.radians(205 + (180 if FACE_UNITY_FORWARD else 0)))
        sc.frame_set(_use_clip(arm, f"{key}_Idle", 0.0))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 21.5
    sc.render.resolution_x, sc.render.resolution_y = (1600, 700) if not fast else (1000, 440)
    _aim(cam, (0, 0, 0.7), 30, 0)
    sc.render.filepath = str(out / "blender_lineup.png")
    bpy.ops.render.render(write_still=True)
    # Action lineup: everyone mid-Strike.
    for key in LINEUP_ORDER:
        _use_clip(res[key][0], f"{key}_Strike", 0.5)
    frames = {}
    for key in LINEUP_ORDER:
        arm = res[key][0]
        frames[key] = _use_clip(arm, f"{key}_Strike", 0.46)
    # Each armature has its own action; one scene frame drives them all, so pick a shared frame.
    sc.frame_set(9)
    sc.render.filepath = str(out / "blender_lineup_strike.png")
    bpy.ops.render.render(write_still=True)
    # Frame strips: solo creature, 6 samples per clip.
    sc.render.resolution_x, sc.render.resolution_y = (480, 400) if not fast else (320, 270)
    for key in strips:
        for k2 in LINEUP_ORDER:
            for o in (res[k2][0], res[k2][1]):
                o.hide_render = k2 != key
        arm = res[key][0]
        arm.location = (0, 0, 0)
        arm.rotation_euler = (0.0, 0.0, math.pi if FACE_UNITY_FORWARD else 0.0)
        h = META[UNIT[key][0]]["height"]
        cam.data.ortho_scale = max(2.6, h * 1.7)
        for clip in ("Walk", "Strike", "Down"):
            for j, t in enumerate((0.0, 0.17, 0.33, 0.5, 0.67, 0.83) if clip == "Walk" else
                                  (0.0, 0.22, 0.34, 0.46, 0.6, 0.85) if clip == "Strike" else
                                  (0.0, 0.2, 0.4, 0.6, 0.8, 1.0)):
                sc.frame_set(_use_clip(arm, f"{key}_{clip}", t))
                _aim(cam, (0, 0, h * 0.4), 24, 64)
                sc.render.filepath = str(out / f"strip_{key}_{clip}_{j}.png")
                bpy.ops.render.render(write_still=True)
    print(f"[SM] review renders in {out}")


def _args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return argv


if __name__ == "__main__":
    argv = _args()
    if "--build" in argv:
        reset_scene()
        keys = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None
        build_all(keys)
        if "--save" in argv:
            bpy.ops.wm.save_as_mainfile(filepath=argv[argv.index("--save") + 1])
        print(json.dumps(META, indent=1))
    elif "--review" in argv:
        out = Path(argv[argv.index("--out") + 1]) if "--out" in argv else ROOT / ".dream-loop" / "stills" / "fauna_v2"
        strips = argv[argv.index("--strips") + 1].split(",") if "--strips" in argv else ("Stalker", "Hopper")
        review(out, fast="--fast" in argv, strips=strips)
