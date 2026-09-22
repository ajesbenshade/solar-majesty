#!/usr/bin/env python3
"""Rigged, textured Idle / Walk / Strike roster for Solar Majesty.

Existing classes only. Defense is the tracked guardian. Hopper has six legs.
Wisp has seven points. Creeper is graphite. Leech is a white ray.
Mite is a pillbug. Stalker is a four-eyed quadruped.

Blender 5.2. Run:
  blender --background --python Blender/scripts/sm_animated_roster.py -- --export --render
"""

from __future__ import annotations

import math
import shutil
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
TEX_DIR = ROOT / ".dream-loop" / "textures"
STILL_DIR = ROOT / ".dream-loop" / "stills"
EXPORT_DIR = ROOT / "Blender" / "exports" / "units"
UNITY_DIR = ROOT / "Assets" / "Resources" / "Units"

UNIT_NAMES = {
    "Stalker": "SM_Unit_DustStalker",
    "Hopper": "SM_Unit_AshHopper",
    "Creeper": "SM_Unit_SoilCreeper",
    "Tick": "SM_Unit_RockTick",
    "Mite": "SM_Unit_RegolithMite",
    "Leech": "SM_Unit_WattLeech",
    "Wisp": "SM_Unit_IceWisp",
    "Scout": "SM_Unit_ScoutDrone",
    "Engineer": "SM_Unit_EngineerBot",
    "Defense": "SM_Unit_DefenseMech",
    "Medic": "SM_Unit_Medic",
    "Harvester": "SM_Unit_HarvesterBot",
    "Surveyor": "SM_Unit_SurveyorBot",
    "Terraformer": "SM_Unit_TerraformerBot",
    "Courier": "SM_Unit_CourierBot",
    "Geologist": "SM_Unit_GeologistBot",
    "Sentinel": "SM_Unit_SentinelMech",
}

# x, y on the Mars still. Fronts point +Y; the still yaws them toward the camera.
LAYOUT = [
    ("Stalker", -10.4, -0.6, "Strike"),
    ("Hopper", -7.0, -0.2, "Walk"),
    ("Creeper", -3.6, -0.8, "Walk"),
    ("Tick", -0.4, -0.3, "Strike"),
    ("Mite", 2.4, -0.55, "Walk"),
    ("Leech", 5.2, -0.25, "Walk"),
    ("Wisp", 8.4, 0.15, "Idle"),
    ("Scout", -11.2, 6.4, "Walk"),
    ("Engineer", -8.4, 6.0, "Strike"),
    ("Defense", -5.2, 6.6, "Idle"),
    ("Medic", -2.4, 6.05, "Idle"),
    ("Harvester", 0.5, 6.45, "Walk"),
    ("Surveyor", 3.2, 5.9, "Idle"),
    ("Terraformer", 6.2, 6.7, "Walk"),
    ("Courier", 9.2, 6.15, "Walk"),
    ("Geologist", 12.0, 6.35, "Strike"),
    ("Sentinel", 14.6, 6.05, "Idle"),
]


def args_after_dd() -> list[str]:
    if "--" not in sys.argv:
        return ["--export", "--render"]
    return sys.argv[sys.argv.index("--") + 1 :]


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.actions,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.curves,
    ):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def load_image(name: str, filename: str):
    if name in bpy.data.images:
        return bpy.data.images[name]
    path = TEX_DIR / filename
    if not path.is_file():
        print(f"[SM] missing texture {path}")
        return None
    img = bpy.data.images.load(str(path))
    img.name = name
    img.colorspace_settings.name = "sRGB"
    return img


def make_mat(name, color, metallic, roughness, image=None, emission=0.0, transmission=0.0, uv_scale=2.4, coord_name="UV"):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    if emission > 0 and "Emission Strength" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    if transmission > 0 and "Transmission Weight" in bsdf.inputs:
        bsdf.inputs["Transmission Weight"].default_value = transmission
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = min(roughness, 0.12)
        mat.surface_render_method = "BLENDED"
    if image is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        mapping = nt.nodes.new("ShaderNodeMapping")
        coord = nt.nodes.new("ShaderNodeTexCoord")
        mapping.inputs["Scale"].default_value = (uv_scale, uv_scale, uv_scale)
        nt.links.new(coord.outputs[coord_name], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.9
        bump.inputs["Distance"].default_value = 0.04
        nt.links.new(tex.outputs["Color"], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def palette() -> dict:
    ceramic = load_image("ceramic", "ceramic.png")
    carbon = load_image("carbon", "carbon.png")
    chitin = load_image("chitin", "chitin.png")
    return {
        "white": make_mat("SM_White", (0.82, 0.78, 0.70), 0.08, 0.78, ceramic, uv_scale=3.2),
        "black": make_mat("SM_Black", (0.05, 0.05, 0.055), 0.35, 0.55, carbon, uv_scale=4.0),
        "graphite": make_mat("SM_Graphite", (0.16, 0.15, 0.14), 0.18, 0.62, chitin, uv_scale=3.4),
        "orange": make_mat("SM_Orange", (0.81, 0.21, 0.08), 0.12, 0.55),
        "cyan": make_mat("SM_Cyan", (0.20, 0.55, 0.62), 0.05, 0.45, emission=0.15),
        "red": make_mat("SM_Red", (0.55, 0.08, 0.05), 0.08, 0.5, emission=0.12),
        "steel": make_mat("SM_Steel", (0.55, 0.57, 0.60), 0.88, 0.32),
        "dust": make_mat("SM_Dust", (0.50, 0.38, 0.26), 0.05, 0.78),
        "olive": make_mat("SM_Olive", (0.34, 0.36, 0.16), 0.08, 0.7),
        "ice": make_mat("SM_Ice", (0.86, 0.84, 0.80), 0.02, 0.42, emission=0.02, transmission=0.15),
        "ash": make_mat("SM_Ash", (0.55, 0.53, 0.50), 0.12, 0.58, chitin, uv_scale=2.2),
        "white_ray": make_mat("SM_WhiteRay", (0.93, 0.94, 0.95), 0.08, 0.35, ceramic, uv_scale=2.0),
    }


class Rig:
    def __init__(self, name: str):
        arm = bpy.data.armatures.new(name + "_RigData")
        self.obj = bpy.data.objects.new(name + "_Rig", arm)
        bpy.context.scene.collection.objects.link(self.obj)
        bpy.context.view_layer.objects.active = self.obj
        bpy.ops.object.mode_set(mode="EDIT")
        self.bone("Root", (0, 0, 0), (0, 0, 0.18), None)

    def bone(self, name, head, tail, parent):
        b = self.obj.data.edit_bones.new(name)
        b.head = Vector(head)
        b.tail = Vector(tail)
        if parent:
            b.parent = self.obj.data.edit_bones[parent]
        if (Vector(tail) - Vector(head)).length < 0.02:
            b.tail = Vector(head) + Vector((0, 0.02, 0.04))
        return name

    def finish(self):
        bpy.ops.object.mode_set(mode="OBJECT")
        return self.obj


def bm_to_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.update()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def tag(obj, mat, bone):
    obj.data.materials.append(mat)
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    return obj


def _bevel(bm, amount):
    if amount <= 0:
        return
    try:
        bmesh.ops.bevel(
            bm,
            geom=list(bm.edges),
            offset=amount,
            segments=2,
            profile=0.5,
            affect="EDGES",
            clamp_overlap=True,
        )
    except TypeError:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=amount, segments=1, affect="EDGES")


def cube(name, size, loc, mat, bone, bevel=0.0, rot_z=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    sx, sy, sz = size
    for v in bm.verts:
        v.co.x *= sx
        v.co.y *= sy
        v.co.z *= sz
    _bevel(bm, bevel if bevel else min(sx, sy, sz) * 0.08)
    if abs(rot_z) > 1e-4:
        bmesh.ops.rotate(
            bm,
            verts=list(bm.verts),
            cent=(0, 0, 0),
            matrix=Euler((0, 0, rot_z)).to_matrix().to_4x4(),
        )
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(loc))
    return tag(bm_to_obj(name, bm), mat, bone)


def uv_sphere(name, radius, loc, mat, bone, scale=(1, 1, 1), u=18, v=10):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=radius, calc_uvs=True)
    for vert in bm.verts:
        vert.co.x *= scale[0]
        vert.co.y *= scale[1]
        vert.co.z *= scale[2]
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(loc))
    obj = tag(bm_to_obj(name, bm), mat, bone)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def cone(name, r1, r2, depth, loc, direction, mat, bone, verts=12):
    bm = bmesh.new()
    delta = Vector(direction)
    if delta.length < 1e-4:
        delta = Vector((0, 0, 1))
    quat = delta.normalized().to_track_quat("Z", "Y")
    mat4 = Matrix.Translation(Vector(loc)) @ quat.to_matrix().to_4x4()
    bmesh.ops.create_cone(
        bm,
        cap_ends=True,
        cap_tris=False,
        segments=verts,
        radius1=r1,
        radius2=r2,
        depth=depth,
        matrix=mat4,
        calc_uvs=True,
    )
    obj = tag(bm_to_obj(name, bm), mat, bone)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def seg(name, a, b, r1, r2, mat, bone, verts=10):
    a, b = Vector(a), Vector(b)
    delta = b - a
    length = max(0.02, delta.length)
    mid = (a + b) / 2
    return cone(name, r1, r2, length, mid, delta, mat, bone, verts)


def join(parts, name):
    parts = [p for p in parts if p is not None]
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.join()
    obj = parts[0]
    obj.name = name
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    try:
        bpy.ops.uv.smart_project(angle_limit=1.05, island_margin=0.015)
    except Exception as exc:
        print("[SM] uv skip", exc)
    bpy.ops.object.mode_set(mode="OBJECT")
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(55))
    except Exception:
        pass
    return obj


def bind(mesh, arm):
    mesh.parent = arm
    mod = mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    mod.use_vertex_groups = True
    return mesh


def begin_action(arm, name):
    if arm.animation_data is None:
        arm.animation_data_create()
    act = bpy.data.actions.new(name)
    arm.animation_data.action = act
    slot = act.slots.new(id_type="OBJECT", name=arm.name)
    arm.animation_data.action_slot = slot
    return act


def pose(arm):
    bpy.context.view_layer.objects.active = arm
    if bpy.context.mode != "POSE":
        bpy.ops.object.mode_set(mode="POSE")


def krot(arm, bone, frame, x=0.0, y=0.0, z=0.0):
    pose(arm)
    pb = arm.pose.bones[bone]
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = (x, y, z)
    pb.keyframe_insert("rotation_euler", frame=frame)


def kloc(arm, bone, frame, x=0.0, y=0.0, z=0.0):
    pose(arm)
    pb = arm.pose.bones[bone]
    pb.location = (x, y, z)
    pb.keyframe_insert("location", frame=frame)


def gait(arm, clip, hips, frames=24, bob="Body"):
    """hips: (bone, phase) phase in turns. X swing is the step."""
    begin_action(arm, clip)
    for frame in range(1, frames + 1):
        t = (frame - 1) / frames
        for bone, phase in hips:
            s = math.sin((t + phase) * math.tau)
            krot(arm, bone, frame, x=s * 0.62)
            knee = bone.replace("_Hip", "_Knee")
            if knee in arm.pose.bones:
                lift = max(0.0, math.cos((t + phase) * math.tau))
                krot(arm, knee, frame, x=0.15 + lift * 0.85)
        if bob in arm.pose.bones:
            krot(arm, bob, frame, x=abs(math.sin(t * math.tau * 2.0)) * 0.05)


def spin(arm, bones, frames, axis="y", turns=2.0, bob=None):
    for frame in range(1, frames + 1):
        ang = (frame - 1) / frames * math.tau * turns
        for bone in bones:
            if bone not in arm.pose.bones:
                continue
            kw = {axis: ang}
            krot(arm, bone, frame, **kw)
        if bob and bob in arm.pose.bones:
            krot(arm, bob, frame, x=math.sin((frame / frames) * math.tau) * 0.04)


def idle_sway(arm, clip, extras=()):
    begin_action(arm, clip)
    body = "Body"
    if body not in arm.pose.bones:
        for candidate in ("Seg3", "Rib2", "Head", "Root"):
            if candidate in arm.pose.bones:
                body = candidate
                break
    keys = {1: (0, 0, 0), 16: (0.045, 0, 0.03), 32: (0, 0, 0)}
    for frame, ang in keys.items():
        if body in arm.pose.bones:
            krot(arm, body, frame, *ang)
        for bone, amp in extras:
            if bone not in arm.pose.bones:
                continue
            krot(arm, bone, frame, x=amp[0] * (1 if frame == 16 else 0), y=amp[1] * (1 if frame == 16 else 0), z=amp[2] * (1 if frame == 16 else 0))


def strike_pose(arm, clip, peak, frames=16):
    """peak: dict bone -> (x,y,z) at the middle frame. Optional 'loc' key is unused."""
    begin_action(arm, clip)
    for frame, blend in ((1, 0.0), (8, 1.0), (16, 0.0)):
        for bone, ang in peak.items():
            if bone not in arm.pose.bones:
                continue
            krot(arm, bone, frame, ang[0] * blend, ang[1] * blend, ang[2] * blend)


def leg_pair(rig, parts, prefix, hip, knee, foot, r0, r1, mat, parent="Body"):
    rig.bone(prefix + "_Hip", hip, knee, parent)
    rig.bone(prefix + "_Knee", knee, foot, prefix + "_Hip")
    toe = Vector(foot) + Vector((0, 0.06, 0.0))
    rig.bone(prefix + "_Foot", foot, toe, prefix + "_Knee")
    parts.append(seg(prefix + "u", hip, knee, r0, r1, mat, prefix + "_Hip"))
    parts.append(seg(prefix + "l", knee, foot, r1, r1 * 0.65, mat, prefix + "_Knee"))
    parts.append(uv_sphere(prefix + "f", r1 * 0.7, foot, mat, prefix + "_Foot", u=10, v=6))


def wheel(rig, parts, name, center, radius, mat, parent="Body"):
    c = Vector(center)
    axle = c + Vector((radius * 0.15, 0, 0))
    rig.bone(name, c, axle, parent)
    parts.append(cone(name + "m", radius, radius, radius * 0.28, c, Vector((1, 0, 0)), mat, name, verts=16))
    hub = c + Vector((0, 0, 0))
    parts.append(uv_sphere(name + "h", radius * 0.28, hub, mat, name, u=8, v=6))


def finish_character(parts, rig: Rig, mesh_name: str):
    arm = rig.finish()
    mesh = join(parts, mesh_name)
    bind(mesh, arm)
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm, mesh


# --- fauna -----------------------------------------------------------------

def build_stalker(mats, clips):
    rig = Rig(UNIT_NAMES["Stalker"])
    rig.bone("Body", (0, 0, 0.28), (0, 0.15, 0.55), "Root")
    rig.bone("Head", (0, 0.55, 0.42), (0, 0.85, 0.48), "Body")
    rig.bone("Tail", (0, -0.55, 0.38), (0, -1.05, 0.55), "Body")
    parts = []
    parts.append(uv_sphere("body", 0.28, (0, 0.05, 0.40), mats["graphite"], "Body", scale=(1.05, 2.3, 0.72), u=24, v=14))
    parts.append(uv_sphere("head", 0.16, (0, 0.62, 0.46), mats["graphite"], "Head", scale=(1.1, 1.35, 0.8), u=16, v=10))
    parts.append(seg("snout", (0, 0.7, 0.44), (0, 0.98, 0.40), 0.09, 0.035, mats["graphite"], "Head"))
    for i, x in enumerate((-0.07, 0.07, -0.12, 0.12)):
        y = 0.78 if abs(x) < 0.1 else 0.68
        z = 0.54 if abs(x) < 0.1 else 0.50
        parts.append(uv_sphere(f"eye{i}", 0.028, (x, y, z), mats["orange"], "Head", u=8, v=6))
    for i, y in enumerate((-0.15, 0.05, 0.25)):
        parts.append(cube(f"ridge{i}", (0.06, 0.16, 0.10), (0, y, 0.62), mats["black"], "Body", bevel=0.01))
    parts.append(seg("tail", (0, -0.35, 0.38), (0, -0.95, 0.62), 0.08, 0.02, mats["graphite"], "Tail"))
    hips = [(-0.22, 0.32, 0.36), (0.22, 0.32, 0.36), (-0.22, -0.28, 0.34), (0.22, -0.28, 0.34)]
    feet = [(-0.38, 0.48, 0.02), (0.38, 0.48, 0.02), (-0.40, -0.48, 0.02), (0.40, -0.48, 0.02)]
    for i, (h, f) in enumerate(zip(hips, feet)):
        knee = ((h[0] + f[0]) * 0.5, (h[1] + f[1]) * 0.5, 0.22)
        leg_pair(rig, parts, f"L{i}", h, knee, f, 0.055, 0.04, mats["black"])
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Stalker"])
    idle_sway(arm, clips[0], extras=(("Head", (0.08, 0, 0)), ("Tail", (-0.12, 0, 0.2))))
    gait(arm, clips[1], [(f"L{i}_Hip", 0.0 if i % 2 == 0 else 0.5) for i in range(4)])
    strike_pose(arm, clips[2], {
        "Body": (0.35, 0, 0),
        "Head": (0.45, 0, 0),
        "L0_Hip": (0.9, 0, 0),
        "L1_Hip": (0.9, 0, 0),
        "Tail": (-0.4, 0, 0),
    })
    return arm, mesh


def build_hopper(mats, clips):
    rig = Rig(UNIT_NAMES["Hopper"])
    rig.bone("Body", (0, 0, 0.9), (0, 0.1, 1.35), "Root")
    rig.bone("Head", (0, 0.22, 1.25), (0, 0.48, 1.32), "Body")
    parts = []
    parts.append(uv_sphere("body", 0.22, (0, 0, 1.18), mats["ash"], "Body", scale=(0.85, 1.5, 1.15), u=18, v=12))
    parts.append(uv_sphere("head", 0.12, (0, 0.28, 1.28), mats["ash"], "Head", scale=(1, 1.2, 0.9)))
    parts.append(cube("stripe", (0.16, 0.04, 0.05), (0, 0.40, 1.30), mats["orange"], "Head", bevel=0.005))
    parts.append(uv_sphere("eyeL", 0.03, (-0.07, 0.38, 1.34), mats["cyan"], "Head", u=8, v=6))
    parts.append(uv_sphere("eyeR", 0.03, (0.07, 0.38, 1.34), mats["cyan"], "Head", u=8, v=6))
    # Six stilts. Front and rear cross in side view (X-legs); middle is a strut.
    specs = [
        ("L0", (-0.12, 0.22, 1.05), (-0.34, 0.02, 0.55), (-0.28, 0.28, 0.02)),
        ("L1", (-0.14, 0.0, 1.02), (-0.42, 0.0, 0.52), (-0.30, 0.0, 0.02)),
        ("L2", (-0.12, -0.22, 1.05), (-0.34, -0.02, 0.55), (-0.28, -0.30, 0.02)),
        ("R0", (0.12, 0.22, 1.05), (0.34, 0.02, 0.55), (0.28, 0.28, 0.02)),
        ("R1", (0.14, 0.0, 1.02), (0.42, 0.0, 0.52), (0.30, 0.0, 0.02)),
        ("R2", (0.12, -0.22, 1.05), (0.34, -0.02, 0.55), (0.28, -0.30, 0.02)),
    ]
    for name, hip, knee, foot in specs:
        leg_pair(rig, parts, name, hip, knee, foot, 0.035, 0.028, mats["black"])
        parts.append(uv_sphere(name + "k", 0.04, knee, mats["orange"], name + "_Knee", u=8, v=6))
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Hopper"])
    idle_sway(arm, clips[0], extras=(("Head", (0.06, 0, 0)),))
    gait(arm, clips[1], [(n + "_Hip", i * (1 / 6)) for i, (n, *_) in enumerate(specs)])
    strike_pose(arm, clips[2], {
        "Body": (-0.15, 0, 0),
        "Head": (0.4, 0, 0),
        "L0_Knee": (1.1, 0, 0),
        "R0_Knee": (1.1, 0, 0),
        "L1_Hip": (-0.4, 0, 0),
        "R1_Hip": (-0.4, 0, 0),
    })
    return arm, mesh


def build_creeper(mats, clips):
    rig = Rig(UNIT_NAMES["Creeper"])
    parts = []
    parent = "Root"
    seg_bones = []
    for i in range(6):
        y = -0.85 + i * 0.32
        bone = f"Seg{i}"
        rig.bone(bone, (0, y, 0.16), (0, y + 0.12, 0.28), parent)
        seg_bones.append(bone)
        parent = bone
        mat = mats["olive"] if i == 3 else mats["graphite"]
        parts.append(uv_sphere(f"pl{i}", 0.16, (0, y, 0.22), mat, bone, scale=(1.15, 1.05, 0.55), u=16, v=8))
        parts.append(cube(f"tile{i}", (0.22, 0.18, 0.04), (0, y, 0.32), mats["graphite"], bone, bevel=0.006))
        for side, x in (("a", -0.16), ("b", 0.16)):
            hip = (x * 0.55, y, 0.14)
            foot = (x * 1.35, y + 0.04, 0.02)
            knee = ((hip[0] + foot[0]) * 0.5, y, 0.08)
            leg_pair(rig, parts, f"S{i}{side}", hip, knee, foot, 0.03, 0.022, mats["black"], parent=bone)
    rig.bone("Head", (0, 1.05, 0.2), (0, 1.28, 0.24), "Seg5")
    parts.append(uv_sphere("head", 0.11, (0, 1.12, 0.22), mats["graphite"], "Head", scale=(1, 1.3, 0.7)))
    parts.append(uv_sphere("eye", 0.025, (0.06, 1.22, 0.28), mats["cyan"], "Head", u=8, v=5))
    parts.append(uv_sphere("nub", 0.02, (-0.05, 1.2, 0.30), mats["orange"], "Head", u=8, v=5))
    parts.append(seg("tendril", (0, -1.0, 0.18), (0, -1.25, 0.28), 0.03, 0.012, mats["orange"], "Seg0"))
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Creeper"])
    idle_sway(arm, clips[0])
    begin_action(arm, clips[1])
    hips = []
    for i in range(6):
        for side in ("a", "b"):
            hips.append((f"S{i}{side}_Hip", (i * 0.15) + (0 if side == "a" else 0.5)))
    # gait() opens its own action; key the wave directly.
    arm.animation_data.action = bpy.data.actions[clips[1]]
    for frame in range(1, 25):
        t = (frame - 1) / 24
        for i, bone in enumerate(seg_bones):
            krot(arm, bone, frame, z=math.sin((t * math.tau) + i * 0.7) * 0.12)
        for bone, phase in hips:
            s = math.sin((t + phase) * math.tau)
            krot(arm, bone, frame, x=s * 0.5)
    strike_pose(arm, clips[2], {"Head": (0.5, 0, 0), "Seg5": (0.25, 0, 0), "Seg0": (-0.2, 0, 0)})
    return arm, mesh


def build_tick(mats, clips):
    rig = Rig(UNIT_NAMES["Tick"])
    rig.bone("Body", (0, 0, 0.22), (0, 0.05, 0.42), "Root")
    rig.bone("PinL", (-0.18, 0.28, 0.22), (-0.42, 0.55, 0.16), "Body")
    rig.bone("PinR", (0.18, 0.28, 0.22), (0.42, 0.55, 0.16), "Body")
    parts = []
    parts.append(uv_sphere("shell", 0.34, (0, 0, 0.28), mats["graphite"], "Body", scale=(1.7, 1.05, 0.55), u=22, v=12))
    parts.append(cone("spike", 0.04, 0.01, 0.22, (0, -0.02, 0.48), Vector((0, 0, 1)), mats["graphite"], "Body"))
    parts.append(uv_sphere("eL", 0.03, (-0.12, 0.28, 0.36), mats["cyan"], "Body", u=8, v=6))
    parts.append(uv_sphere("eR", 0.03, (0.12, 0.28, 0.36), mats["cyan"], "Body", u=8, v=6))
    parts.append(seg("pL", (-0.12, 0.32, 0.2), (-0.38, 0.58, 0.12), 0.045, 0.02, mats["graphite"], "PinL"))
    parts.append(uv_sphere("pLt", 0.035, (-0.40, 0.60, 0.12), mats["orange"], "PinL", u=8, v=5))
    parts.append(seg("pR", (0.12, 0.32, 0.2), (0.38, 0.58, 0.12), 0.045, 0.02, mats["graphite"], "PinR"))
    parts.append(uv_sphere("pRt", 0.035, (0.40, 0.60, 0.12), mats["orange"], "PinR", u=8, v=5))
    for i, y in enumerate((-0.18, 0.0, 0.18)):
        for s, x in enumerate((-1, 1)):
            hip = (0.28 * x, y, 0.18)
            knee = (0.48 * x, y + 0.05, 0.1)
            foot = (0.58 * x, y + 0.08, 0.02)
            leg_pair(rig, parts, f"T{i}{s}", hip, knee, foot, 0.035, 0.026, mats["black"])
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Tick"])
    idle_sway(arm, clips[0])
    gait(arm, clips[1], [(f"T{i}{s}_Hip", (i * 0.2) + s * 0.5) for i in range(3) for s in range(2)])
    strike_pose(arm, clips[2], {"PinL": (0.2, 0, -0.7), "PinR": (0.2, 0, 0.7), "Body": (0.15, 0, 0)})
    return arm, mesh


def build_mite(mats, clips):
    rig = Rig(UNIT_NAMES["Mite"])
    rig.bone("Body", (0, 0, 0.16), (0, 0.08, 0.32), "Root")
    rig.bone("Head", (0, 0.38, 0.16), (0, 0.55, 0.18), "Body")
    parts = []
    parts.append(uv_sphere("belly", 0.22, (0, 0, 0.18), mats["dust"], "Body", scale=(0.85, 1.7, 0.7), u=18, v=10))
    for i, y in enumerate((-0.22, -0.05, 0.12)):
        parts.append(cube(f"plate{i}", (0.28, 0.16, 0.05), (0, y, 0.32), mats["graphite"], "Body", bevel=0.008))
    parts.append(uv_sphere("head", 0.09, (0, 0.42, 0.18), mats["graphite"], "Head", scale=(1, 1.2, 0.8)))
    parts.append(cube("jaw", (0.06, 0.08, 0.03), (0, 0.52, 0.14), mats["black"], "Head", bevel=0.004))
    parts.append(uv_sphere("eye", 0.022, (0.05, 0.48, 0.24), mats["cyan"], "Head", u=8, v=5))
    parts.append(uv_sphere("nubL", 0.018, (-0.06, 0.46, 0.26), mats["orange"], "Head", u=6, v=4))
    parts.append(uv_sphere("nubR", 0.018, (0.06, 0.40, 0.26), mats["orange"], "Head", u=6, v=4))
    for i, y in enumerate((-0.18, 0.0, 0.16)):
        for s, x in enumerate((-1, 1)):
            hip = (0.14 * x, y, 0.12)
            knee = (0.22 * x, y, 0.07)
            foot = (0.26 * x, y + 0.03, 0.02)
            leg_pair(rig, parts, f"M{i}{s}", hip, knee, foot, 0.028, 0.02, mats["black"])
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Mite"])
    idle_sway(arm, clips[0])
    gait(arm, clips[1], [(f"M{i}{s}_Hip", i * 0.25 + s * 0.5) for i in range(3) for s in range(2)])
    strike_pose(arm, clips[2], {"Head": (0.6, 0, 0), "Body": (0.15, 0, 0)})
    return arm, mesh


def build_leech(mats, clips):
    rig = Rig(UNIT_NAMES["Leech"])
    parts = []
    parent = "Root"
    bones = []
    for i in range(5):
        y = -0.7 + i * 0.32
        bone = f"Rib{i}"
        rig.bone(bone, (0, y, 0.1), (0, y + 0.16, 0.16), parent)
        bones.append(bone)
        parent = bone
        parts.append(uv_sphere(f"rib{i}", 0.16, (0, y, 0.14), mats["white_ray"], bone, scale=(1.4, 1.05, 0.28), u=16, v=8))
    rig.bone("Head", (0, 0.85, 0.12), (0, 1.15, 0.14), "Rib4")
    parts.append(cone("funnel", 0.1, 0.03, 0.28, (0, 1.0, 0.12), Vector((0, 1, 0)), mats["white_ray"], "Head"))
    parts.append(cube("groove", (0.06, 1.3, 0.025), (0, 0.05, 0.22), mats["cyan"], "Rib2", bevel=0.004))
    for i, y in enumerate((-0.55, -0.2, 0.15, 0.45)):
        parts.append(uv_sphere(f"dL{i}", 0.055, (-0.2, y, 0.08), mats["black"], bones[min(i, 4)], scale=(1, 1, 0.45), u=10, v=6))
        parts.append(uv_sphere(f"dR{i}", 0.055, (0.2, y, 0.08), mats["black"], bones[min(i, 4)], scale=(1, 1, 0.45), u=10, v=6))
    parts.append(uv_sphere("nub", 0.02, (0.08, 1.05, 0.16), mats["orange"], "Head", u=6, v=4))
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Leech"])
    idle_sway(arm, clips[0])
    begin_action(arm, clips[1])
    for frame in range(1, 25):
        t = (frame - 1) / 24
        for i, bone in enumerate(bones):
            krot(arm, bone, frame, z=math.sin(t * math.tau + i * 0.8) * 0.18, x=math.sin(t * math.tau * 2 + i) * 0.05)
        kloc(arm, "Body", frame, z=math.sin(t * math.tau) * 0.04) if "Body" in arm.pose.bones else None
    strike_pose(arm, clips[2], {"Head": (0.7, 0, 0), "Rib4": (0.3, 0, 0)})
    return arm, mesh


def build_wisp(mats, clips):
    rig = Rig(UNIT_NAMES["Wisp"])
    rig.bone("Body", (0, 0, 0.7), (0, 0, 0.95), "Root")
    parts = []
    parts.append(uv_sphere("core", 0.08, (0, 0, 0.85), mats["white"], "Body", u=12, v=8))
    for i in range(7):
        ang = i * (math.tau / 7)
        direction = Vector((math.cos(ang), math.sin(ang) * 0.35, math.sin(ang) * 0.25 + 0.15))
        tip = Vector((0, 0, 0.85)) + direction.normalized() * 0.85
        bone = f"Shard{i}"
        rig.bone(bone, (0, 0, 0.85), tip, "Body")
        parts.append(seg(bone + "m", (0, 0, 0.85), tip, 0.06, 0.012, mats["ice"], bone, verts=8))
    parts.append(cone("up", 0.05, 0.01, 0.35, (0, 0, 1.15), Vector((0, 0, 1)), mats["ice"], "Body"))
    parts.append(uv_sphere("nubL", 0.018, (-0.06, 0.1, 0.9), mats["orange"], "Body", u=6, v=4))
    parts.append(uv_sphere("nubR", 0.018, (0.06, 0.1, 0.9), mats["orange"], "Body", u=6, v=4))
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Wisp"])
    idle_sway(arm, clips[0])
    begin_action(arm, clips[1])
    spin(arm, [f"Shard{i}" for i in range(7)], 24, axis="y", turns=0.35, bob="Body")
    peak = {f"Shard{i}": (0.4, 0, 0) for i in range(7)}
    peak["Body"] = (0, 0.3, 0)
    strike_pose(arm, clips[2], peak)
    return arm, mesh


# --- heroes ----------------------------------------------------------------

def build_scout(mats, clips):
    rig = Rig(UNIT_NAMES["Scout"])
    rig.bone("Body", (0, 0, 1.1), (0, 0, 1.8), "Root")
    rig.bone("Head", (0, 0.02, 2.05), (0, 0.2, 2.25), "Body")
    rig.bone("Antenna", (0.04, -0.06, 2.35), (0.04, -0.06, 3.15), "Head")
    parts = []
    parts.append(cone("stem", 0.05, 0.05, 0.7, (0, 0, 0.45), Vector((0, 0, 1)), mats["black"], "Body", verts=10))
    parts.append(cone("fuselage", 0.18, 0.18, 1.05, (0, 0, 1.5), Vector((0, 0, 1)), mats["white"], "Body", verts=20))
    for z in (1.18, 1.52, 1.88):
        parts.append(cone(f"band{z}", 0.2, 0.2, 0.06, (0, 0, z), Vector((0, 0, 1)), mats["black"], "Body", verts=20))
    parts.append(cube("head", (0.28, 0.32, 0.24), (0, 0.06, 2.28), mats["white"], "Head", bevel=0.02))
    parts.append(cone("lens", 0.08, 0.08, 0.05, (0, 0.24, 2.28), Vector((0, 1, 0)), mats["cyan"], "Head"))
    parts.append(cone("collar", 0.15, 0.15, 0.08, (0, 0.02, 2.08), Vector((0, 0, 1)), mats["orange"], "Body", verts=16))
    parts.append(uv_sphere("beacon", 0.045, (0.16, -0.02, 1.95), mats["orange"], "Body", u=8, v=6))
    parts.append(cone("ant", 0.016, 0.01, 0.8, (0.04, -0.06, 2.75), Vector((0, 0, 1)), mats["steel"], "Antenna", verts=8))
    parts.append(uv_sphere("tip", 0.03, (0.04, -0.06, 3.18), mats["orange"], "Antenna", u=8, v=5))
    for i in range(4):
        ang = math.pi * 0.25 + i * math.pi * 0.5
        x, y = math.cos(ang) * 0.52, math.sin(ang) * 0.52
        bone = f"Rotor{i}"
        rig.bone(bone, (x, y, 0.98), (x, y, 1.12), "Body")
        parts.append(cube(f"arm{i}", (0.48, 0.045, 0.035), (x * 0.5, y * 0.5, 0.98), mats["white"], "Body", bevel=0.006, rot_z=ang))
        # arm cubes aren't rotated; a thin radial box is enough at this distance if we also drop a ring.
        parts.append(cone(f"ring{i}", 0.18, 0.18, 0.035, (x, y, 0.98), Vector((0, 0, 1)), mats["black"], bone, verts=16))
        parts.append(cone(f"disc{i}", 0.12, 0.12, 0.015, (x, y, 1.02), Vector((0, 0, 1)), mats["steel"], bone, verts=12))
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Scout"])
    idle_sway(arm, clips[0], extras=(("Antenna", (0.0, 0, 0.15)), ("Head", (0.04, 0, 0))))
    begin_action(arm, clips[1])
    spin(arm, [f"Rotor{i}" for i in range(4)], 24, axis="y", turns=2.0, bob="Body")
    strike_pose(arm, clips[2], {"Body": (0.35, 0, 0), "Head": (0.25, 0, 0), "Antenna": (0.4, 0, 0)})
    return arm, mesh


def build_engineer(mats, clips):
    rig = Rig(UNIT_NAMES["Engineer"])
    rig.bone("Body", (0, 0, 0.7), (0, 0.05, 1.35), "Root")
    rig.bone("Head", (0, 0.08, 1.45), (0, 0.2, 1.7), "Body")
    rig.bone("ArmL", (-0.42, 0.05, 1.2), (-0.72, 0.15, 0.85), "Body")
    rig.bone("ArmR", (0.42, 0.05, 1.2), (0.72, 0.2, 0.85), "Body")
    parts = []
    parts.append(uv_sphere("torso", 0.38, (0, 0.02, 1.05), mats["white"], "Body", scale=(1.15, 0.95, 1.05), u=20, v=12))
    parts.append(cone("band", 0.4, 0.4, 0.08, (0, 0.02, 0.82), Vector((0, 0, 1)), mats["black"], "Body", verts=18))
    parts.append(cube("pack", (0.42, 0.26, 0.48), (0, -0.32, 1.12), mats["steel"], "Body", bevel=0.02))
    parts.append(cube("stripe", (0.08, 0.04, 0.32), (0, -0.46, 1.12), mats["orange"], "Body", bevel=0.004))
    parts.append(cube("box", (0.26, 0.2, 0.18), (0.48, 0.05, 0.78), mats["black"], "Body", bevel=0.012))
    parts.append(cube("visor", (0.32, 0.04, 0.06), (0, 0.28, 1.5), mats["black"], "Head", bevel=0.004))
    parts.append(cube("dock", (0.22, 0.05, 0.08), (0, 0.36, 1.15), mats["orange"], "Body", bevel=0.006))
    parts.append(uv_sphere("dome", 0.22, (0, 0.06, 1.55), mats["white"], "Head", scale=(1.1, 0.9, 0.75)))
    for side, x, bone in (("L", -1, "ArmL"), ("R", 1, "ArmR")):
        parts.append(seg(f"arm{side}", (0.32 * x, 0.05, 1.2), (0.62 * x, 0.12, 0.85), 0.07, 0.05, mats["steel"], bone))
        parts.append(cube(f"hand{side}", (0.12, 0.1, 0.08), (0.68 * x, 0.16, 0.78), mats["white"], bone, bevel=0.008))
        parts.append(cube(f"ast{side}", (0.03, 0.08, 0.16), (0.5 * x, 0.08, 1.15), mats["orange"], bone, bevel=0.003))
    for side, x, bone in (("L", -0.16, "LegL"), ("R", 0.16, "LegR")):
        hip = (x, 0.02, 0.62)
        knee = (x, 0.04, 0.32)
        foot = (x, 0.1, 0.04)
        leg_pair(rig, parts, bone, hip, knee, foot, 0.09, 0.07, mats["black"])
        parts.append(cube(f"boot{side}", (0.16, 0.28, 0.08), (x, 0.12, 0.05), mats["black"], bone + "_Foot", bevel=0.008))
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Engineer"])
    idle_sway(arm, clips[0], extras=(("Head", (0.05, 0, 0)),))
    gait(arm, clips[1], [("LegL_Hip", 0.0), ("LegR_Hip", 0.5)])
    # arms counter-swing on the walk action
    for frame in range(1, 25):
        t = (frame - 1) / 24
        krot(arm, "ArmL", frame, x=math.sin(t * math.tau) * 0.4)
        krot(arm, "ArmR", frame, x=math.sin(t * math.tau + math.pi) * 0.4)
    strike_pose(arm, clips[2], {"ArmR": (-1.3, 0, 0.2), "Body": (0.12, 0, 0), "Head": (0.1, 0, 0)})
    return arm, mesh


def tread_side(rig, parts, name, x, length, radius, z, mat_tread, mat_wheel):
    """Elongated housing plus three spinning road wheels. Continuous skirt, not legs."""
    rig.bone(name, (x, 0, z), (x + 0.08, 0, z), "Body")
    # Individual tread shoes, with a gap under the hull. Not one black plinth.
    for i in range(8):
        y = -length * 0.46 + i * (length * 0.92 / 7.0)
        parts.append(cube(
            f"{name}shoe{i}",
            (radius * 1.35, length * 0.07, radius * 0.85),
            (x, y, z * 0.72),
            mat_tread,
            "Body",
            bevel=0.008,
        ))
    for i, y in enumerate((-length * 0.32, 0.0, length * 0.32)):
        wheel(rig, parts, f"{name}W{i}", (x, y, z), radius * 0.55, mat_wheel)


def build_defense(mats, clips):
    rig = Rig(UNIT_NAMES["Defense"])
    rig.bone("Body", (0, 0, 0.45), (0, 0.1, 0.9), "Root")
    rig.bone("Turret", (0, 0.05, 0.95), (0, 0.15, 1.25), "Body")
    rig.bone("Shield", (-0.7, 0.05, 0.7), (-0.95, 0.1, 0.95), "Body")
    parts = []
    parts.append(cube("hull", (1.15, 1.55, 0.55), (0, 0, 0.72), mats["white"], "Body", bevel=0.06))
    parts.append(cube("band", (1.2, 1.35, 0.12), (0, 0, 0.55), mats["black"], "Body", bevel=0.02))
    parts.append(cube("viewport", (0.55, 0.06, 0.16), (0, 0.8, 0.82), mats["red"], "Body", bevel=0.008))
    parts.append(cube("shield", (0.12, 0.7, 0.7), (-0.78, 0.1, 0.85), mats["steel"], "Shield", bevel=0.02))
    parts.append(cube("shoulder", (0.38, 0.42, 0.32), (0.62, -0.15, 1.05), mats["white"], "Body", bevel=0.03))
    parts.append(cube("shoulderO", (0.1, 0.36, 0.08), (0.84, -0.15, 1.05), mats["orange"], "Body", bevel=0.008))
    parts.append(cube("turret", (0.36, 0.5, 0.22), (0, 0.15, 1.12), mats["white"], "Turret", bevel=0.02))
    parts.append(cone("barrel", 0.05, 0.04, 0.45, (0.1, 0.45, 1.12), Vector((0, 1, 0)), mats["black"], "Turret", verts=10))
    tread_side(rig, parts, "TrL", -0.62, 1.35, 0.22, 0.28, mats["black"], mats["steel"])
    tread_side(rig, parts, "TrR", 0.62, 1.35, 0.22, 0.28, mats["black"], mats["steel"])
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Defense"])
    idle_sway(arm, clips[0], extras=(("Turret", (0, 0, 0.12)),))
    begin_action(arm, clips[1])
    wheels = [f"Tr{s}W{i}" for s in ("L", "R") for i in range(3)]
    spin(arm, wheels, 24, axis="y", turns=1.5, bob=None)
    for frame in (1, 12, 24):
        krot(arm, "Body", frame, x=0.02 if frame == 12 else 0)
    strike_pose(arm, clips[2], {"Turret": (0, 0, 0.55), "Shield": (0, 0.2, 0), "Body": (0.05, 0, 0)})
    return arm, mesh


def build_medic(mats, clips):
    rig = Rig(UNIT_NAMES["Medic"])
    rig.bone("Body", (0, 0, 0.35), (0, 0.1, 0.7), "Root")
    rig.bone("IV", (0.28, -0.45, 0.55), (0.28, -0.45, 1.15), "Body")
    parts = []
    parts.append(cube("hull", (0.7, 1.15, 0.32), (0, 0, 0.48), mats["white"], "Body", bevel=0.04))
    parts.append(cube("belly", (0.62, 1.0, 0.12), (0, 0, 0.28), mats["black"], "Body", bevel=0.02))
    parts.append(cube("nose", (0.5, 0.08, 0.08), (0, 0.6, 0.55), mats["orange"], "Body", bevel=0.008))
    parts.append(cube("crossV", (0.08, 0.08, 0.22), (0, 0.05, 0.7), mats["cyan"], "Body", bevel=0.004))
    parts.append(cube("crossH", (0.22, 0.08, 0.08), (0, 0.05, 0.7), mats["cyan"], "Body", bevel=0.004))
    parts.append(cube("visor", (0.4, 0.04, 0.08), (0, 0.58, 0.58), mats["cyan"], "Body", bevel=0.004))
    parts.append(cone("pole", 0.015, 0.015, 0.6, (0.28, -0.45, 0.85), Vector((0, 0, 1)), mats["steel"], "IV", verts=8))
    parts.append(uv_sphere("bag", 0.06, (0.28, -0.45, 1.12), mats["cyan"], "IV", u=8, v=6))
    parts.append(uv_sphere("kit", 0.1, (-0.32, -0.2, 0.62), mats["white"], "Body", u=10, v=8))
    parts.append(cube("latch", (0.04, 0.06, 0.04), (-0.32, -0.1, 0.62), mats["orange"], "Body", bevel=0.003))
    for i, (x, y) in enumerate(((-0.32, 0.35), (0.32, 0.35), (-0.32, -0.35), (0.32, -0.35))):
        bone = f"Disc{i}"
        rig.bone(bone, (x, y, 0.16), (x, y, 0.28), "Body")
        parts.append(cone(bone + "m", 0.12, 0.12, 0.04, (x, y, 0.16), Vector((0, 0, 1)), mats["black"], bone, verts=14))
        parts.append(cone(bone + "g", 0.05, 0.05, 0.02, (x, y, 0.19), Vector((0, 0, 1)), mats["cyan"], bone, verts=10))
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Medic"])
    idle_sway(arm, clips[0], extras=(("IV", (0.08, 0, 0)),))
    begin_action(arm, clips[1])
    spin(arm, [f"Disc{i}" for i in range(4)], 24, axis="y", turns=1.2, bob="Body")
    strike_pose(arm, clips[2], {"IV": (0.5, 0, 0), "Body": (0.08, 0, 0)})
    return arm, mesh


def build_harvester(mats, clips):
    rig = Rig(UNIT_NAMES["Harvester"])
    rig.bone("Body", (0, 0, 0.5), (0, 0.1, 0.95), "Root")
    rig.bone("Scoop", (0, 0.7, 0.35), (0, 1.15, 0.22), "Body")
    parts = []
    parts.append(cube("hull", (0.9, 1.2, 0.5), (0, -0.05, 0.7), mats["white"], "Body", bevel=0.05))
    parts.append(cube("band", (0.95, 1.05, 0.1), (0, -0.05, 0.48), mats["black"], "Body", bevel=0.02))
    parts.append(cube("cab", (0.45, 0.35, 0.28), (0, 0.15, 1.05), mats["white"], "Body", bevel=0.02))
    parts.append(cube("visor", (0.32, 0.04, 0.08), (0, 0.34, 1.1), mats["cyan"], "Body", bevel=0.004))
    parts.append(cube("hopper", (0.7, 0.45, 0.4), (0, -0.55, 0.85), mats["steel"], "Body", bevel=0.02))
    parts.append(cube("lip", (0.74, 0.06, 0.06), (0, -0.32, 1.02), mats["orange"], "Body", bevel=0.006))
    parts.append(cube("blade", (0.85, 0.08, 0.22), (0, 0.95, 0.28), mats["orange"], "Scoop", bevel=0.01))
    parts.append(seg("arm", (0, 0.55, 0.45), (0, 0.9, 0.32), 0.06, 0.05, mats["graphite"], "Scoop"))
    tread_side(rig, parts, "TrL", -0.52, 1.15, 0.2, 0.26, mats["black"], mats["steel"])
    tread_side(rig, parts, "TrR", 0.52, 1.15, 0.2, 0.26, mats["black"], mats["steel"])
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Harvester"])
    idle_sway(arm, clips[0])
    begin_action(arm, clips[1])
    spin(arm, [f"Tr{s}W{i}" for s in ("L", "R") for i in range(3)], 24, axis="y", turns=1.4)
    strike_pose(arm, clips[2], {"Scoop": (0.7, 0, 0), "Body": (0.08, 0, 0)})
    return arm, mesh


def build_surveyor(mats, clips):
    rig = Rig(UNIT_NAMES["Surveyor"])
    rig.bone("Body", (0, 0, 0.7), (0, 0, 1.15), "Root")
    rig.bone("Mast", (0, 0, 1.2), (0, 0, 2.15), "Body")
    rig.bone("Dish", (0, 0, 2.2), (0, 0.15, 2.45), "Mast")
    parts = []
    parts.append(cone("body", 0.16, 0.18, 0.7, (0, 0, 0.95), Vector((0, 0, 1)), mats["white"], "Body", verts=16))
    parts.append(cone("band", 0.2, 0.2, 0.06, (0, 0, 0.85), Vector((0, 0, 1)), mats["black"], "Body", verts=16))
    parts.append(cone("lens", 0.06, 0.06, 0.04, (0, 0.18, 1.0), Vector((0, 1, 0)), mats["cyan"], "Body"))
    parts.append(cone("mast", 0.035, 0.03, 0.9, (0, 0, 1.7), Vector((0, 0, 1)), mats["steel"], "Mast", verts=8))
    parts.append(uv_sphere("beacon", 0.04, (0.1, 0, 1.85), mats["orange"], "Mast", u=8, v=5))
    parts.append(cone("dish", 0.28, 0.05, 0.08, (0, 0.02, 2.28), Vector((0, 0.15, 1)), mats["white"], "Dish", verts=20))
    parts.append(cone("ring", 0.3, 0.3, 0.02, (0, 0.03, 2.26), Vector((0, 0.15, 1)), mats["orange"], "Dish", verts=20))
    parts.append(uv_sphere("cluster", 0.05, (0, 0.08, 2.36), mats["cyan"], "Dish", u=8, v=6))
    for i in range(3):
        ang = i * math.tau / 3 + 0.4
        hip = (math.cos(ang) * 0.12, math.sin(ang) * 0.12, 0.62)
        foot = (math.cos(ang) * 0.55, math.sin(ang) * 0.55, 0.02)
        knee = (math.cos(ang) * 0.4, math.sin(ang) * 0.4, 0.28)
        leg_pair(rig, parts, f"P{i}", hip, knee, foot, 0.045, 0.035, mats["steel"])
        parts.append(cube(f"pad{i}", (0.16, 0.16, 0.03), foot, mats["black"], f"P{i}_Foot", bevel=0.006))
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Surveyor"])
    idle_sway(arm, clips[0], extras=(("Dish", (0.08, 0, 0.1)),))
    gait(arm, clips[1], [(f"P{i}_Hip", i / 3) for i in range(3)], bob="Body")
    strike_pose(arm, clips[2], {"Dish": (0.45, 0, 0), "Mast": (0.1, 0, 0)})
    return arm, mesh


def build_terraformer(mats, clips):
    rig = Rig(UNIT_NAMES["Terraformer"])
    rig.bone("Body", (0, 0, 0.55), (0, 0.1, 1.05), "Root")
    rig.bone("Blade", (0, 0.85, 0.4), (0, 1.25, 0.35), "Body")
    rig.bone("Rake", (0, -0.7, 0.45), (0, -1.15, 0.35), "Body")
    parts = []
    parts.append(cube("hull", (1.05, 1.5, 0.55), (0, 0, 0.75), mats["white"], "Body", bevel=0.05))
    parts.append(cube("belly", (1.0, 1.35, 0.16), (0, 0, 0.42), mats["black"], "Body", bevel=0.02))
    parts.append(cube("cab", (0.5, 0.4, 0.32), (0, 0.15, 1.15), mats["white"], "Body", bevel=0.02))
    parts.append(cube("visor", (0.36, 0.04, 0.08), (0, 0.36, 1.22), mats["cyan"], "Body", bevel=0.004))
    parts.append(uv_sphere("lamp", 0.04, (0.22, 0.2, 1.28), mats["cyan"], "Body", u=8, v=5))
    parts.append(uv_sphere("beacon", 0.045, (-0.18, -0.1, 1.25), mats["orange"], "Body", u=8, v=5))
    parts.append(cube("blade", (1.5, 0.08, 0.45), (0, 1.15, 0.4), mats["steel"], "Blade", bevel=0.015))
    parts.append(cube("lip", (1.52, 0.05, 0.08), (0, 1.2, 0.58), mats["orange"], "Blade", bevel=0.006))
    parts.append(cone("tankL", 0.16, 0.16, 0.4, (-0.28, -0.45, 1.05), Vector((0, 0, 1)), mats["steel"], "Body", verts=12))
    parts.append(cone("tankR", 0.16, 0.16, 0.4, (0.28, -0.45, 1.05), Vector((0, 0, 1)), mats["steel"], "Body", verts=12))
    parts.append(uv_sphere("capL", 0.08, (-0.28, -0.45, 1.28), mats["orange"], "Body", u=8, v=5))
    parts.append(uv_sphere("capR", 0.08, (0.28, -0.45, 1.28), mats["orange"], "Body", u=8, v=5))
    parts.append(cube("boom", (1.7, 0.06, 0.06), (0, -0.95, 0.55), mats["graphite"], "Rake", bevel=0.008))
    for i, x in enumerate((-0.6, -0.2, 0.2, 0.6)):
        parts.append(cube(f"noz{i}", (0.05, 0.05, 0.1), (x, -0.95, 0.46), mats["orange"], "Rake", bevel=0.004))
    parts.append(cube("rake", (0.08, 0.35, 0.28), (0.7, -0.15, 0.4), mats["steel"], "Rake", bevel=0.01))
    tread_side(rig, parts, "TrL", -0.58, 1.35, 0.22, 0.28, mats["black"], mats["steel"])
    tread_side(rig, parts, "TrR", 0.58, 1.35, 0.22, 0.28, mats["black"], mats["steel"])
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Terraformer"])
    idle_sway(arm, clips[0])
    begin_action(arm, clips[1])
    spin(arm, [f"Tr{s}W{i}" for s in ("L", "R") for i in range(3)], 24, axis="y", turns=1.2)
    strike_pose(arm, clips[2], {"Blade": (0.45, 0, 0), "Rake": (-0.25, 0, 0)})
    return arm, mesh


def build_courier(mats, clips):
    rig = Rig(UNIT_NAMES["Courier"])
    rig.bone("Body", (0, 0, 0.4), (0, 0.1, 0.85), "Root")
    rig.bone("Crate", (0, -0.25, 0.7), (0, -0.25, 1.05), "Body")
    parts = []
    parts.append(cube("chassis", (0.7, 1.35, 0.35), (0, 0.05, 0.48), mats["white"], "Body", bevel=0.04))
    parts.append(cube("belly", (0.64, 1.2, 0.12), (0, 0.05, 0.28), mats["black"], "Body", bevel=0.015))
    parts.append(cube("cab", (0.55, 0.4, 0.32), (0, 0.45, 0.78), mats["white"], "Body", bevel=0.02))
    parts.append(cube("visor", (0.36, 0.04, 0.1), (0, 0.66, 0.82), mats["cyan"], "Body", bevel=0.004))
    parts.append(uv_sphere("lamp", 0.035, (0.18, 0.66, 0.7), mats["cyan"], "Body", u=8, v=5))
    parts.append(cube("crate", (0.55, 0.5, 0.4), (0, -0.35, 0.85), mats["steel"], "Crate", bevel=0.02))
    parts.append(cube("cap", (0.55, 0.5, 0.06), (0, -0.35, 1.08), mats["white"], "Crate", bevel=0.008))
    for x, y in ((-0.26, -0.15), (0.26, -0.15), (-0.26, -0.55), (0.26, -0.55)):
        parts.append(cube("corner", (0.08, 0.08, 0.08), (x, y, 1.05), mats["orange"], "Crate", bevel=0.004))
    parts.append(cone("ant", 0.012, 0.008, 0.55, (-0.15, 0.2, 1.15), Vector((0, 0, 1)), mats["steel"], "Body", verts=6))
    parts.append(uv_sphere("atip", 0.025, (-0.15, 0.2, 1.45), mats["orange"], "Body", u=6, v=4))
    parts.append(uv_sphere("beacon", 0.04, (0.15, 0.35, 1.05), mats["orange"], "Body", u=8, v=5))
    wheels = []
    for i, y in enumerate((0.45, 0.0, -0.45)):
        for s, x in enumerate((-0.38, 0.38)):
            name = f"W{i}{s}"
            wheels.append(name)
            wheel(rig, parts, name, (x, y, 0.16), 0.16, mats["black"])
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Courier"])
    idle_sway(arm, clips[0])
    begin_action(arm, clips[1])
    spin(arm, wheels, 24, axis="y", turns=1.6)
    strike_pose(arm, clips[2], {"Crate": (0.0, 0, 0.15), "Body": (0.1, 0, 0)})
    return arm, mesh


def build_geologist(mats, clips):
    rig = Rig(UNIT_NAMES["Geologist"])
    rig.bone("Body", (0, 0, 0.4), (0, 0.08, 0.8), "Root")
    rig.bone("Drill", (0.35, 0.15, 0.7), (0.35, 0.15, 0.15), "Body")
    parts = []
    parts.append(cube("chassis", (0.65, 1.3, 0.32), (0, 0, 0.48), mats["white"], "Body", bevel=0.035))
    parts.append(cube("belly", (0.58, 1.15, 0.1), (0, 0, 0.28), mats["black"], "Body", bevel=0.012))
    parts.append(cube("nose", (0.4, 0.06, 0.08), (0, 0.66, 0.55), mats["orange"], "Body", bevel=0.006))
    parts.append(cube("cab", (0.4, 0.32, 0.26), (0, 0.25, 0.75), mats["white"], "Body", bevel=0.015))
    parts.append(cube("visor", (0.28, 0.04, 0.08), (0, 0.42, 0.8), mats["cyan"], "Body", bevel=0.004))
    parts.append(cube("crate", (0.4, 0.32, 0.28), (0, -0.4, 0.7), mats["graphite"], "Body", bevel=0.012))
    parts.append(uv_sphere("vialC", 0.04, (-0.08, -0.35, 0.9), mats["cyan"], "Body", u=8, v=5))
    parts.append(uv_sphere("vialO", 0.04, (0.08, -0.42, 0.9), mats["orange"], "Body", u=8, v=5))
    parts.append(cone("mast", 0.02, 0.02, 0.4, (-0.15, 0.05, 1.0), Vector((0, 0, 1)), mats["steel"], "Body", verts=8))
    parts.append(uv_sphere("cluster", 0.045, (-0.15, 0.05, 1.22), mats["cyan"], "Body", u=8, v=5))
    parts.append(seg("arm", (0.25, 0.1, 0.7), (0.4, 0.15, 0.55), 0.04, 0.035, mats["graphite"], "Drill"))
    parts.append(cone("bit", 0.045, 0.02, 0.4, (0.4, 0.15, 0.32), Vector((0, 0, -1)), mats["steel"], "Drill", verts=8))
    parts.append(cone("collar", 0.07, 0.07, 0.05, (0.4, 0.15, 0.5), Vector((0, 0, 1)), mats["orange"], "Drill", verts=10))
    wheels = []
    for i, y in enumerate((0.42, 0.0, -0.42)):
        for s, x in enumerate((-0.36, 0.36)):
            name = f"W{i}{s}"
            wheels.append(name)
            wheel(rig, parts, name, (x, y, 0.15), 0.15, mats["black"])
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Geologist"])
    idle_sway(arm, clips[0])
    begin_action(arm, clips[1])
    spin(arm, wheels, 24, axis="y", turns=1.5)
    strike_pose(arm, clips[2], {"Drill": (0.0, 0.8, 0), "Body": (0.06, 0, 0)})
    return arm, mesh


def build_sentinel(mats, clips):
    rig = Rig(UNIT_NAMES["Sentinel"])
    rig.bone("Body", (0, 0, 0.35), (0, 0.05, 0.7), "Root")
    rig.bone("Turret", (0, 0, 0.85), (0, 0.1, 1.15), "Body")
    parts = []
    parts.append(cube("hull", (0.85, 0.95, 0.4), (0, 0, 0.55), mats["white"], "Body", bevel=0.04))
    parts.append(cube("skirt", (0.95, 1.05, 0.16), (0, 0, 0.32), mats["black"], "Body", bevel=0.02))
    parts.append(cube("chev", (0.55, 0.05, 0.12), (0, 0.5, 0.62), mats["orange"], "Body", bevel=0.006))
    parts.append(cube("visor", (0.4, 0.04, 0.08), (0, 0.48, 0.78), mats["cyan"], "Body", bevel=0.004))
    parts.append(cube("shield", (0.08, 0.4, 0.45), (-0.5, 0.05, 0.7), mats["steel"], "Body", bevel=0.012))
    parts.append(cube("turret", (0.32, 0.4, 0.2), (0, 0.08, 1.0), mats["white"], "Turret", bevel=0.015))
    parts.append(cone("b0", 0.035, 0.03, 0.35, (-0.08, 0.35, 1.02), Vector((0, 1, 0)), mats["graphite"], "Turret", verts=8))
    parts.append(cone("b1", 0.035, 0.03, 0.35, (0.08, 0.35, 1.02), Vector((0, 1, 0)), mats["graphite"], "Turret", verts=8))
    parts.append(uv_sphere("l0", 0.03, (-0.08, 0.52, 1.02), mats["cyan"], "Turret", u=6, v=4))
    parts.append(uv_sphere("l1", 0.03, (0.08, 0.52, 1.02), mats["cyan"], "Turret", u=6, v=4))
    tread_side(rig, parts, "TrL", -0.42, 0.85, 0.16, 0.2, mats["black"], mats["steel"])
    tread_side(rig, parts, "TrR", 0.42, 0.85, 0.16, 0.2, mats["black"], mats["steel"])
    arm, mesh = finish_character(parts, rig, UNIT_NAMES["Sentinel"])
    idle_sway(arm, clips[0], extras=(("Turret", (0, 0, 0.2)),))
    begin_action(arm, clips[1])
    spin(arm, [f"Tr{s}W{i}" for s in ("L", "R") for i in range(3)], 24, axis="y", turns=1.3)
    strike_pose(arm, clips[2], {"Turret": (0.15, 0, 0.6)})
    return arm, mesh


BUILDERS = {
    "Stalker": build_stalker,
    "Hopper": build_hopper,
    "Creeper": build_creeper,
    "Tick": build_tick,
    "Mite": build_mite,
    "Leech": build_leech,
    "Wisp": build_wisp,
    "Scout": build_scout,
    "Engineer": build_engineer,
    "Defense": build_defense,
    "Medic": build_medic,
    "Harvester": build_harvester,
    "Surveyor": build_surveyor,
    "Terraformer": build_terraformer,
    "Courier": build_courier,
    "Geologist": build_geologist,
    "Sentinel": build_sentinel,
}


def export_fbx(arm, mesh, filename: str) -> Path:
    EXPORT_DIR.mkdir(parents=True, exist_ok=True)
    UNITY_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.view_layer.objects.active = arm
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    path = EXPORT_DIR / f"{filename}.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        apply_unit_scale=True,
        object_types={"ARMATURE", "MESH"},
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
    )
    dest = UNITY_DIR / f"{filename}.fbx"
    shutil.copy2(path, dest)
    print(f"[SM] exported {dest}")
    return dest


def export_all():
    for key, builder in BUILDERS.items():
        print(f"[SM] === {key} ===")
        clear_scene()
        mats = palette()
        arm, mesh = builder(mats, ("Idle", "Walk", "Strike"))
        export_fbx(arm, mesh, UNIT_NAMES[key])


def build_world():
    mars = load_image("mars", "mars.png")
    mat = make_mat("SM_Mars", (0.42, 0.16, 0.07), 0.0, 0.95, mars, uv_scale=14.0, coord_name="Generated")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=80)
    ground = bm_to_obj("MarsGround", bm)
    ground.data.materials.append(mat)
    for poly in ground.data.polygons:
        poly.use_smooth = False
    # A few fractured rocks, not a clutter field.
    rock_mat = make_mat("SM_Rock", (0.45, 0.22, 0.14), 0.05, 0.85, mars, uv_scale=2.0)
    for i, loc, sc in (
        (0, (-12, 6, 0.15), (0.8, 0.5, 0.35)),
        (1, (13, -1, 0.12), (0.5, 0.7, 0.25)),
        (2, (-3, 9, 0.1), (0.4, 0.3, 0.2)),
    ):
        rock = uv_sphere(f"rock{i}", 0.6, loc, rock_mat, "Root", scale=sc, u=8, v=5)
        # rocks are not skinned; drop the empty vertex group object into the scene only.
        rock.parent = None

    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 2.8
    sun_data.color = (1.0, 0.62, 0.38)
    sun_data.angle = math.radians(11.0)
    sun = bpy.data.objects.new("Sun", sun_data)
    bpy.context.scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(48), 0.0, math.radians(-35))

    fill_data = bpy.data.lights.new("Fill", "AREA")
    fill_data.energy = 250
    fill_data.color = (1.0, 0.62, 0.42)
    fill_data.size = 8
    fill = bpy.data.objects.new("Fill", fill_data)
    bpy.context.scene.collection.objects.link(fill)
    fill.location = (6, -8, 6)
    fill.rotation_euler = (math.radians(60), 0, math.radians(20))

    world = bpy.data.worlds.new("MarsSky")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.45, 0.18, 0.08, 1.0)
    bg.inputs["Strength"].default_value = 0.45

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 576
    scene.render.film_transparent = False
    if hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 64
    scene.eevee.use_shadows = True
    scene.eevee.use_raytracing = True
    scene.eevee.use_fast_gi = False
    try:
        scene.view_settings.view_transform = "AgX"
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "PERSP"
    cam_data.lens = 34
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = (2.4, -19.5, 13.2)
    target = bpy.data.objects.new("CamTarget", None)
    bpy.context.scene.collection.objects.link(target)
    target.location = (2.4, 3.4, 0.15)
    con = cam.constraints.new("TRACK_TO")
    con.target = target
    con.track_axis = "TRACK_NEGATIVE_Z"
    con.up_axis = "UP_Y"
    scene.camera = cam
    scene.frame_start = 1
    scene.frame_end = 24
    scene.render.fps = 24


def use_cycles(scene):
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "METAL"
        prefs.get_devices()
        for device in prefs.devices:
            device.use = True
        scene.cycles.device = "GPU"
        print("[SM] cycles GPU", [d.name for d in prefs.devices if d.use])
    except Exception as exc:
        print("[SM] cycles CPU", exc)


def render_lineup(animate=True):
    clear_scene()
    mats = palette()
    build_world()
    for key, x, y, which in LAYOUT:
        print(f"[SM] place {key}")
        arm, mesh = BUILDERS[key](mats, (f"{key}_Idle", f"{key}_Walk", f"{key}_Strike"))
        arm.location = (x, y, 0.0)
        arm.rotation_euler = (0.0, 0.0, math.pi)
        act_name = f"{key}_{which}"
        act = bpy.data.actions[act_name]
        arm.animation_data.action = act
        arm.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(8)
    if "--preview" not in args_after_dd():
        use_cycles(bpy.context.scene)
    STILL_DIR.mkdir(parents=True, exist_ok=True)
    still = STILL_DIR / "roster_still.png"
    bpy.context.scene.render.filepath = str(still)
    bpy.context.scene.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    print(f"[SM] still {still}")
    if not animate:
        return

    scene = bpy.context.scene
    frames = STILL_DIR / "frames"
    frames.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(frames / "f")
    scene.render.image_settings.file_format = "PNG"
    scene.frame_start = 1
    scene.frame_end = 24
    bpy.ops.render.render(animation=True)
    clip = STILL_DIR / "roster_cycle.mp4"
    print(f"[SM] frames {frames}")
    return clip


def main():
    flags = args_after_dd()
    print("[SM] animated roster", flags)
    if "--export" in flags:
        export_all()
    if "--render" in flags or "--still" in flags:
        render_lineup(animate="--render" in flags)
    print("[SM] done")


if __name__ == "__main__":
    main()
