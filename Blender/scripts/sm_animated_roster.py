#!/usr/bin/env python3
"""Rigged, animated unit roster for Solar Majesty.

Heroes (the ten specialist classes) are armoured humanoid knights built by
sm_hero_humanoids.py from the 2026-09-22 concept art (ConceptSheets/Heroes_v2/). They
ship Idle / Walk / Strike / Work / Down clips and a stride speed for Unity.

Enemy fauna (v2, 2026-10-07) are built by sm_fauna_v2.py in the hostile den style (dark
chitin, bone, orange glow): Hopper has six legs, Wisp has seven points, Creeper is
graphite, Leech is a white ray, Mite is a pillbug, Stalker is a four-eyed quadruped. They
ship Idle / Walk / Strike / Down clips that key every bone on every frame, a measured stride
speed, and a baked colour atlas per creature (Assets/Art/Fauna/Textures).

Blender 4.4+ (tested 5.0 / 5.2). Run:
  blender --background --python Blender/scripts/sm_animated_roster.py -- --export --render
Flags: --export   write FBX to Blender/exports/units and Assets/Resources/Units + UnitClipMeta.json
       --render   Cycles lineup still + frames      --still  still only      --preview  EEVEE
       --only Engineer,Scout   limit to some keys
"""

from __future__ import annotations

import json
import math
import shutil
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import sm_hero_humanoids as heroes  # noqa: E402
import sm_fauna_v2 as fauna_v2  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
TEX_DIR = ROOT / ".dream-loop" / "textures"
STILL_DIR = ROOT / ".dream-loop" / "stills"
EXPORT_DIR = ROOT / "Blender" / "exports" / "units"
UNITY_DIR = ROOT / "Assets" / "Resources" / "Units"
META_PATH = UNITY_DIR / "UnitClipMeta.json"
HERO_META: dict = {}

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


_MISSING_TEX: set = set()


def load_image(name: str, filename: str):
    if name in bpy.data.images:
        return bpy.data.images[name]
    path = TEX_DIR / filename
    if not path.is_file():
        # Dream-loop textures are optional render dressing (gitignored). Unity never sees them:
        # IndustrialArtDressing remaps SM_* material names to the in-game hull library.
        if name not in _MISSING_TEX:
            _MISSING_TEX.add(name)
            print(f"[SM] optional texture not found, using flat colour: {path.name}")
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
        "orange": make_mat("SM_Orange", (0.85, 0.30, 0.05), 0.08, 0.45, emission=0.25),
        "cyan": make_mat("SM_Cyan", (0.20, 0.72, 0.95), 0.05, 0.35, emission=2.5),
        "red": make_mat("SM_Red", (0.55, 0.08, 0.05), 0.08, 0.5, emission=0.12),
        "steel": make_mat("SM_Steel", (0.55, 0.57, 0.60), 0.88, 0.32),
        "dust": make_mat("SM_Dust", (0.50, 0.38, 0.26), 0.05, 0.78),
        "olive": make_mat("SM_Olive", (0.34, 0.36, 0.16), 0.08, 0.7),
        "ice": make_mat("SM_Ice", (0.86, 0.84, 0.80), 0.02, 0.42, emission=0.02, transmission=0.15),
        "ash": make_mat("SM_Ash", (0.55, 0.53, 0.50), 0.12, 0.58, chitin, uv_scale=2.2),
        "white_ray": make_mat("SM_WhiteRay", (0.93, 0.94, 0.95), 0.08, 0.35, ceramic, uv_scale=2.0),
        # hero-only slots; names are the tokens IndustrialArtDressing maps in Unity
        "glass": make_mat("SM_Glass", (0.55, 0.68, 0.76), 0.0, 0.08, transmission=0.4),
        "defense": make_mat("SM_VisorRed", (0.95, 0.12, 0.06), 0.0, 0.35, emission=3.0),
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

# --- fauna -----------------------------------------------------------------
# v2 creatures live in sm_fauna_v2.py (their own mesh, rig, clips and baked atlas). The
# palette materials are not used: each creature brings SM_Art_Fauna_<Name> + the shared
# SM_Art_Fauna_GlowAccent.

FAUNA_CLIPS = fauna_v2.CLIPS


def _fauna_v2(key):
    def build(mats, clips):
        return fauna_v2.BUILDERS[key](clips)

    build.__name__ = f"build_{key.lower()}"
    return build


# --- heroes ----------------------------------------------------------------
# Humanoid knights live in sm_hero_humanoids.py. These wrappers keep the BUILDERS contract
# (mats, clip names) -> (armature, mesh) and record the stride speed for Unity.

HERO_CLIPS = ("Idle", "Walk", "Strike", "Work", "Down")


def _hero_builder(key):
    def build(mats, clips):
        names = tuple(clips) + tuple(
            (clips[0].rsplit("_", 1)[0] + "_" + extra) if "_" in clips[0] else extra
            for extra in HERO_CLIPS[len(clips):]
        )
        arm, mesh, meta = heroes.build_hero(key, mats, UNIT_NAMES[key], names)
        HERO_META[UNIT_NAMES[key]] = meta
        return arm, mesh

    build.__name__ = f"build_{key.lower()}"
    return build


# --- clip hygiene ------------------------------------------------------------

def fill_rest_keys(arm, clip_names):
    """Key the rest pose on every bone a fauna clip leaves untouched.

    Without this the FBX bake samples whatever pose the previous clip left on those bones,
    so e.g. Idle exported with Walk's last leg pose."""
    from bpy_extras import anim_utils

    if arm.animation_data is None:
        return
    bpy.context.view_layer.objects.active = arm
    for act in [bpy.data.actions.get(n) for n in clip_names]:
        if act is None or not act.slots:
            continue
        slot = act.slots[0]
        bag = anim_utils.action_get_channelbag_for_slot(act, slot)
        if bag is None:
            continue
        keyed = {fc.data_path.split('"')[1] for fc in bag.fcurves if fc.data_path.startswith("pose.bones")}
        start, end = act.frame_range
        arm.animation_data.action = act
        arm.animation_data.action_slot = slot
        if bpy.context.mode != "POSE":
            bpy.ops.object.mode_set(mode="POSE")
        for pb in arm.pose.bones:
            if pb.name in keyed:
                continue
            pb.rotation_mode = "XYZ"
            pb.rotation_euler = (0.0, 0.0, 0.0)
            pb.location = (0.0, 0.0, 0.0)
            for f in (start, end):
                pb.keyframe_insert("rotation_euler", frame=f)
                pb.keyframe_insert("location", frame=f)
    bpy.ops.object.mode_set(mode="OBJECT")


FAUNA_BUILDERS = {key: _fauna_v2(key) for key in ("Stalker", "Hopper", "Creeper", "Tick", "Mite", "Leech", "Wisp")}


def _fauna_builder(fn):
    def build(mats, clips):
        arm, mesh = fn(mats, clips)
        fill_rest_keys(arm, clips)
        return arm, mesh

    build.__name__ = fn.__name__
    return build


BUILDERS = {key: _fauna_builder(fn) for key, fn in FAUNA_BUILDERS.items()}
for _key in ("Scout", "Engineer", "Defense", "Medic", "Harvester", "Surveyor", "Terraformer", "Courier",
             "Geologist", "Sentinel"):
    BUILDERS[_key] = _hero_builder(_key)


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


def only_keys():
    flags = args_after_dd()
    if "--only" in flags:
        i = flags.index("--only")
        if i + 1 < len(flags):
            return [k.strip() for k in flags[i + 1].split(",") if k.strip()]
    return list(BUILDERS.keys())


def write_meta():
    """Stride speeds + clip lists for UnitClipPlayer (merged with any existing entries)."""
    data = {}
    if META_PATH.is_file():
        try:
            for row in json.loads(META_PATH.read_text()).get("units", []):
                data[row["unit"]] = row
        except (ValueError, KeyError):
            data = {}
    for unit, meta in HERO_META.items():
        data[unit] = {
            "unit": unit,
            "walkSpeed": meta["walkSpeed"],
            "height": meta["height"],
            "clips": list(HERO_CLIPS),
            "downHoldsLastFrame": True,
        }
    for unit, meta in fauna_v2.META.items():
        data[unit] = {
            "unit": unit,
            "walkSpeed": meta["walkSpeed"],
            "height": meta["height"],
            "clips": list(FAUNA_CLIPS),
            "downHoldsLastFrame": True,
        }
    for key, unit in UNIT_NAMES.items():
        if key in FAUNA_BUILDERS and unit not in data:
            data[unit] = {"unit": unit, "walkSpeed": 0.0, "height": 0.0, "clips": list(FAUNA_CLIPS),
                          "downHoldsLastFrame": True}
    META_PATH.parent.mkdir(parents=True, exist_ok=True)
    META_PATH.write_text(json.dumps({"units": sorted(data.values(), key=lambda r: r["unit"])}, indent=2) + "\n")
    print(f"[SM] meta {META_PATH}")


def export_all():
    for key in only_keys():
        builder = BUILDERS[key]
        print(f"[SM] === {key} ===")
        clear_scene()
        mats = palette()
        clips = FAUNA_CLIPS if key in FAUNA_BUILDERS else ("Idle", "Walk", "Strike")
        arm, mesh = builder(mats, clips)
        export_fbx(arm, mesh, UNIT_NAMES[key])
    write_meta()


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
    fauna_v2.BAKE_TEXTURES = False  # reuse the committed atlases instead of re-baking
    for key, x, y, which in LAYOUT:
        print(f"[SM] place {key}")
        names = FAUNA_CLIPS if key in FAUNA_BUILDERS else ("Idle", "Walk", "Strike")
        arm, mesh = BUILDERS[key](mats, tuple(f"{key}_{n}" for n in names))
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
