"""
Solar Majesty — hero unit blockouts.

Readable Majesty-scale silhouettes with SpaceX white/black/orange palette.
Scout / Engineer / Defense / Stalker refined against ConceptSheets turnarounds.
Phase 3 adds Medic, Harvester, Surveyor, Terraformer, Courier, Geologist,
Sentinel, Mite, Leech, Ice Wisp, Rock Tick, Soil Creeper, Ash Hopper.

Run:
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    --python Blender/scripts/sm_unit_blockouts.py
"""

from __future__ import annotations

import math
import shutil
from pathlib import Path

import bpy
from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
BLENDER_DIR = SCRIPT_DIR.parent
PROJECT_ROOT = BLENDER_DIR.parent
BLEND_OUT = BLENDER_DIR / "SolarMajesty_Units.blend"
EXPORT_DIR = BLENDER_DIR / "exports"
UNITY_UNITS = PROJECT_ROOT / "Assets" / "Resources" / "Units"


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.12, 0.13, 0.15, 1.0)
        bg.inputs[1].default_value = 0.6


def ensure_collection(name: str) -> bpy.types.Collection:
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def link_object(obj: bpy.types.Object, col: bpy.types.Collection):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)


def make_principled(name: str, base: tuple, metallic: float = 0.15, roughness: float = 0.45):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nodes = nt.nodes
    links = nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (*base, 1.0)
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = metallic
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def create_palette() -> dict:
    return {
        "SM_White": make_principled("SM_White", (0.85, 0.86, 0.88), 0.12, 0.42),
        "SM_Black": make_principled("SM_Black", (0.03, 0.03, 0.035), 0.35, 0.48),
        "SM_Graphite": make_principled("SM_Graphite", (0.12, 0.13, 0.14), 0.40, 0.40),
        "SM_Orange": make_principled("SM_Orange", (0.95, 0.38, 0.05), 0.08, 0.35),
        "SM_Steel": make_principled("SM_Steel", (0.45, 0.47, 0.50), 0.70, 0.32),
        "SM_Cyan": make_principled("SM_Cyan", (0.25, 0.75, 0.95), 0.05, 0.28),
        "SM_Scout": make_principled("SM_Scout", (0.35, 0.78, 0.92), 0.10, 0.38),
        "SM_Engineer": make_principled("SM_Engineer", (0.92, 0.48, 0.14), 0.10, 0.40),
        "SM_Defense": make_principled("SM_Defense", (0.78, 0.18, 0.18), 0.12, 0.42),
        "SM_Stalker": make_principled("SM_Stalker", (0.28, 0.05, 0.07), 0.05, 0.55),
        "SM_Mite": make_principled("SM_Mite", (0.42, 0.32, 0.22), 0.08, 0.62),
        "SM_Leech": make_principled("SM_Leech", (0.18, 0.55, 0.62), 0.12, 0.38),
        "SM_Geologist": make_principled("SM_Geologist", (0.62, 0.48, 0.28), 0.18, 0.48),
        "SM_Sentinel": make_principled("SM_Sentinel", (0.52, 0.16, 0.14), 0.14, 0.42),
        "SM_Wisp": make_principled("SM_Wisp", (0.62, 0.88, 0.96), 0.04, 0.22),
        "SM_Tick": make_principled("SM_Tick", (0.28, 0.24, 0.22), 0.22, 0.55),
        "SM_Creeper": make_principled("SM_Creeper", (0.32, 0.42, 0.18), 0.08, 0.62),
        "SM_Hopper": make_principled("SM_Hopper", (0.48, 0.46, 0.42), 0.18, 0.48),
    }


def assign_mat(obj: bpy.types.Object, mat: bpy.types.Material):
    if obj.data and hasattr(obj.data, "materials"):
        if obj.data.materials:
            obj.data.materials[0] = mat
        else:
            obj.data.materials.append(mat)


def add_cylinder(name, radius, depth, location, rotation=(0, 0, 0), vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth,
        location=location, rotation=rotation,
    )
    obj = bpy.context.active_object
    obj.name = name
    return obj


def add_uv_sphere(name, radius, location, scale=None, segments=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=rings, radius=radius, location=location,
    )
    obj = bpy.context.active_object
    obj.name = name
    if scale:
        obj.scale = scale
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def add_cube(name, size, location, scale=None):
    bpy.ops.mesh.primitive_cube_add(size=size, location=location)
    obj = bpy.context.active_object
    obj.name = name
    if scale:
        obj.scale = scale
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def add_cone(name, radius1, depth, location, vertices=24):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices, radius1=radius1, radius2=0.0, depth=depth, location=location,
    )
    obj = bpy.context.active_object
    obj.name = name
    return obj


def set_origin_to_ground(obj: bpy.types.Object):
    bpy.context.view_layer.update()
    mat = obj.matrix_world
    corners = [mat @ Vector(c) for c in obj.bound_box]
    min_z = min(c.z for c in corners)
    cx = sum(c.x for c in corners) / 8.0
    cy = sum(c.y for c in corners) / 8.0
    cursor = bpy.context.scene.cursor
    prev = cursor.location.copy()
    cursor.location = Vector((cx, cy, min_z))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    cursor.location = prev
    obj.location = (obj.location.x, obj.location.y, 0.0)


def join_parts(parts: list, name: str) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    return obj


def remove_if_exists(name: str):
    obj = bpy.data.objects.get(name)
    if obj:
        bpy.data.objects.remove(obj, do_unlink=True)


# ---------------------------------------------------------------------------
# Units
# ---------------------------------------------------------------------------

def build_scout(mats: dict) -> bpy.types.Object:
    """
    Scout drone — Imagine tall probe + Phase 4 hover read.
    White thermal shell, cyan eye, whip antenna, four rotor rings, thin repulsor pad.
    NavMesh still walks; the pad keeps origin on the ground. Height ~2.6 m.
    """
    name = "SM_Unit_ScoutDrone"
    remove_if_exists(name)
    parts = []

    # Ground-contact hover cushion so SnapToGround does not bury the rotors.
    pad = add_cylinder("TMP", 0.22, 0.04, (0, 0, 0.04), vertices=16)
    assign_mat(pad, mats["SM_Graphite"])
    parts.append(pad)
    stem = add_cylinder("TMP", 0.05, 0.7, (0, 0, 0.42), vertices=10)
    assign_mat(stem, mats["SM_Black"])
    parts.append(stem)

    # Cruciform rotor arms + discs (hover silhouette)
    for i in range(4):
        ang = (math.pi * 0.25) + i * (math.pi * 0.5)
        ax = math.cos(ang) * 0.42
        ay = math.sin(ang) * 0.42
        arm = add_cube("TMP", 1.0, (ax * 0.5, ay * 0.5, 0.92), scale=(0.38, 0.06, 0.06))
        arm.rotation_euler = (0.0, 0.0, ang)
        assign_mat(arm, mats["SM_White"])
        parts.append(arm)
        ring = add_cylinder("TMP", 0.18, 0.04, (ax, ay, 0.92), vertices=16)
        assign_mat(ring, mats["SM_Black"])
        parts.append(ring)
        disc = add_cylinder("TMP", 0.14, 0.02, (ax, ay, 0.95), vertices=12)
        assign_mat(disc, mats["SM_Steel"])
        parts.append(disc)

    # Tall sensor probe (Imagine sheet)
    torso = add_cylinder("TMP", 0.16, 0.85, (0, 0, 1.45), vertices=20)
    assign_mat(torso, mats["SM_White"])
    parts.append(torso)
    band = add_cylinder("TMP", 0.19, 0.08, (0, 0, 1.25), vertices=20)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)
    head = add_uv_sphere("TMP", 0.16, (0, 0.04, 1.95), segments=16, rings=10)
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    eye = add_uv_sphere("TMP", 0.07, (0, 0.16, 1.95), segments=12, rings=8)
    assign_mat(eye, mats["SM_Cyan"])
    parts.append(eye)
    for sx in (-0.14, 0.14):
        beacon = add_uv_sphere("TMP", 0.045, (sx, 0.0, 1.72), segments=10, rings=6)
        assign_mat(beacon, mats["SM_Orange"])
        parts.append(beacon)
    ant = add_cylinder("TMP", 0.02, 0.7, (0.06, -0.04, 2.42), vertices=8)
    assign_mat(ant, mats["SM_Steel"])
    parts.append(ant)
    tip = add_uv_sphere("TMP", 0.035, (0.06, -0.04, 2.78), segments=8, rings=6)
    assign_mat(tip, mats["SM_Orange"])
    parts.append(tip)

    obj = join_parts(parts, name)
    col = ensure_collection("10_Units_Scout")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (-4.0, 0.0, 0.0)
    return obj


def build_engineer(mats: dict) -> bpy.types.Object:
    """
    Engineer Bot — small white biped (Phase 4 + Imagine v2).
    Rounded white shell, cyan visor, orange dock/toolbox, backpack. Height ~1.85 m.
    """
    name = "SM_Unit_EngineerBot"
    remove_if_exists(name)
    parts = []

    hips = add_cylinder("TMP", 0.28, 0.22, (0, 0, 0.52), vertices=18)
    assign_mat(hips, mats["SM_Black"])
    parts.append(hips)
    torso = add_uv_sphere("TMP", 0.38, (0, 0, 1.05), scale=(1.05, 0.85, 1.15), segments=20, rings=12)
    assign_mat(torso, mats["SM_White"])
    parts.append(torso)
    band = add_cylinder("TMP", 0.40, 0.08, (0, 0, 0.82), vertices=20)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)

    dock = add_cylinder("TMP", 0.11, 0.06, (0, 0.36, 1.08), rotation=(math.pi / 2, 0, 0), vertices=16)
    assign_mat(dock, mats["SM_Graphite"])
    parts.append(dock)
    dock_ring = add_cylinder("TMP", 0.14, 0.03, (0, 0.39, 1.08), rotation=(math.pi / 2, 0, 0), vertices=16)
    assign_mat(dock_ring, mats["SM_Orange"])
    parts.append(dock_ring)

    head = add_uv_sphere("TMP", 0.18, (0, 0.04, 1.52), segments=16, rings=10)
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    visor = add_cube("TMP", 1.0, (0, 0.18, 1.52), scale=(0.26, 0.04, 0.08))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)

    pack = add_cube("TMP", 1.0, (0, -0.32, 1.05), scale=(0.38, 0.22, 0.48))
    assign_mat(pack, mats["SM_Graphite"])
    parts.append(pack)
    pack_stripe = add_cube("TMP", 1.0, (0, -0.44, 1.08), scale=(0.06, 0.03, 0.28))
    assign_mat(pack_stripe, mats["SM_Orange"])
    parts.append(pack_stripe)
    toolbox = add_cube("TMP", 1.0, (0.42, 0.02, 0.72), scale=(0.22, 0.16, 0.18))
    assign_mat(toolbox, mats["SM_Black"])
    parts.append(toolbox)
    tool_stripe = add_cube("TMP", 1.0, (0.42, 0.16, 0.72), scale=(0.18, 0.03, 0.04))
    assign_mat(tool_stripe, mats["SM_Orange"])
    parts.append(tool_stripe)

    for sx in (-0.48, 0.48):
        shoulder = add_uv_sphere("TMP", 0.12, (sx, 0.02, 1.22), segments=12, rings=8)
        assign_mat(shoulder, mats["SM_White"])
        parts.append(shoulder)
        upper = add_cube("TMP", 1.0, (sx * 1.08, 0.08, 0.95), scale=(0.12, 0.12, 0.28))
        assign_mat(upper, mats["SM_Graphite"])
        parts.append(upper)
        stripe = add_cube("TMP", 1.0, (sx * 1.18, 0.08, 0.98), scale=(0.04, 0.1, 0.18))
        assign_mat(stripe, mats["SM_Orange"])
        parts.append(stripe)
        lower = add_cube("TMP", 1.0, (sx * 1.1, 0.16, 0.68), scale=(0.1, 0.1, 0.22))
        assign_mat(lower, mats["SM_Steel"])
        parts.append(lower)
        hand = add_cube("TMP", 1.0, (sx * 1.12, 0.22, 0.52), scale=(0.1, 0.12, 0.1))
        assign_mat(hand, mats["SM_Black"])
        parts.append(hand)

    for sx in (-0.16, 0.16):
        thigh = add_cube("TMP", 1.0, (sx, 0.02, 0.34), scale=(0.14, 0.16, 0.28))
        assign_mat(thigh, mats["SM_White"])
        parts.append(thigh)
        shin = add_cube("TMP", 1.0, (sx, 0.04, 0.16), scale=(0.12, 0.14, 0.16))
        assign_mat(shin, mats["SM_Graphite"])
        parts.append(shin)
        boot = add_cube("TMP", 1.0, (sx, 0.1, 0.05), scale=(0.18, 0.26, 0.08))
        assign_mat(boot, mats["SM_Black"])
        parts.append(boot)

    obj = join_parts(parts, name)
    col = ensure_collection("11_Units_Engineer")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (0.0, 0.0, 0.0)
    return obj


def build_defense(mats: dict) -> bpy.types.Object:
    """
    Defense Mech — bulky tracked guardian (Imagine sheet + Phase 4 mass).
    White/carbon hull, orange hazard, cyan optics, shield plate. Height ~2.15 m.
    """
    name = "SM_Unit_DefenseMech"
    remove_if_exists(name)
    parts = []

    belly = add_cube("TMP", 1.0, (0, 0, 0.48), scale=(0.95, 1.15, 0.4))
    assign_mat(belly, mats["SM_Black"])
    parts.append(belly)

    for sx, sy in ((-0.62, 0.5), (0.62, 0.5), (-0.62, -0.5), (0.62, -0.5)):
        bogie = add_cube("TMP", 1.0, (sx, sy, 0.24), scale=(0.36, 0.48, 0.32))
        assign_mat(bogie, mats["SM_Graphite"])
        parts.append(bogie)
        track = add_cube("TMP", 1.0, (sx, sy, 0.08), scale=(0.4, 0.55, 0.1))
        assign_mat(track, mats["SM_Black"])
        parts.append(track)
        for wy in (-0.14, 0.14):
            wh = add_cylinder(
                "TMP", 0.11, 0.08,
                (sx + (0.14 if sx > 0 else -0.14), sy + wy, 0.16),
                rotation=(0, math.pi / 2, 0),
                vertices=12,
            )
            assign_mat(wh, mats["SM_Steel"])
            parts.append(wh)

    hull = add_cube("TMP", 1.0, (0, 0.04, 1.12), scale=(1.05, 1.12, 0.85))
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)
    face = add_cube("TMP", 1.0, (0, 0.62, 1.18), scale=(0.62, 0.08, 0.5))
    assign_mat(face, mats["SM_Graphite"])
    parts.append(face)
    visor = add_cube("TMP", 1.0, (0, 0.66, 1.22), scale=(0.42, 0.04, 0.12))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    crest = add_cube("TMP", 1.0, (0, 0.58, 1.52), scale=(0.18, 0.06, 0.1))
    assign_mat(crest, mats["SM_Orange"])
    parts.append(crest)

    shield = add_cube("TMP", 1.0, (-0.78, 0.22, 1.05), scale=(0.1, 0.7, 0.95))
    assign_mat(shield, mats["SM_Steel"])
    parts.append(shield)
    shield_stripe = add_cube("TMP", 1.0, (-0.84, 0.22, 1.35), scale=(0.04, 0.45, 0.08))
    assign_mat(shield_stripe, mats["SM_Orange"])
    parts.append(shield_stripe)

    for sx in (-0.92, 0.92):
        shoulder = add_cube("TMP", 1.0, (sx, 0.0, 1.28), scale=(0.42, 0.65, 0.55))
        assign_mat(shoulder, mats["SM_White"])
        parts.append(shoulder)
        haz = add_cube("TMP", 1.0, (sx, 0.36, 1.52), scale=(0.28, 0.06, 0.06))
        assign_mat(haz, mats["SM_Orange"])
        parts.append(haz)

    cannon = add_cylinder(
        "TMP", 0.09, 0.7, (0.55, 0.55, 1.22),
        rotation=(math.pi / 2, 0, 0), vertices=12,
    )
    assign_mat(cannon, mats["SM_Graphite"])
    parts.append(cannon)

    turret = add_cylinder("TMP", 0.24, 0.28, (0, -0.08, 1.68), vertices=16)
    assign_mat(turret, mats["SM_Graphite"])
    parts.append(turret)
    dome = add_uv_sphere("TMP", 0.16, (0, -0.08, 1.88), scale=(1.0, 1.0, 0.7), segments=14, rings=8)
    assign_mat(dome, mats["SM_White"])
    parts.append(dome)
    optic = add_uv_sphere("TMP", 0.06, (0, 0.08, 1.9), segments=10, rings=6)
    assign_mat(optic, mats["SM_Cyan"])
    parts.append(optic)

    rear = add_cube("TMP", 1.0, (0, -0.62, 1.0), scale=(0.78, 0.12, 0.55))
    assign_mat(rear, mats["SM_Black"])
    parts.append(rear)

    obj = join_parts(parts, name)
    col = ensure_collection("12_Units_Defense")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (4.0, 0.0, 0.0)
    return obj


def build_stalker(mats: dict) -> bpy.types.Object:
    """
    Dust Stalker — refined against ConceptSheets/SM_Unit_DustStalker_Turnaround.jpg.
    Low quadruped predator: dark hide, bone-white dorsal plating, orange eyes/seams,
    spine ridges, thick tapered tail. Game height ~0.95 m.
    """
    name = "SM_Unit_DustStalker"
    remove_if_exists(name)
    parts = []

    # Main body — elongated dark carapace
    body = add_uv_sphere("TMP", 0.55, (0, 0, 0.42), scale=(1.15, 1.7, 0.55), segments=24, rings=12)
    assign_mat(body, mats["SM_Stalker"])
    parts.append(body)

    # White shoulder / dorsal armor plates
    for y, scz in ((0.25, 0.22), (-0.15, 0.2), (-0.5, 0.16)):
        plate = add_cube("TMP", 1.0, (0, y, 0.62), scale=(0.55, 0.35, scz))
        assign_mat(plate, mats["SM_White"])
        parts.append(plate)

    # Head — tapered
    head = add_uv_sphere("TMP", 0.28, (0, 0.95, 0.48), scale=(0.9, 1.15, 0.75), segments=20, rings=10)
    assign_mat(head, mats["SM_Stalker"])
    parts.append(head)

    # Glowing orange eyes
    for x in (-0.12, 0.12):
        eye = add_uv_sphere("TMP", 0.06, (x, 1.15, 0.52), segments=12, rings=8)
        assign_mat(eye, mats["SM_Orange"])
        parts.append(eye)

    # Jaw glow seams
    for x in (-0.1, 0.1):
        seam = add_cube("TMP", 1.0, (x, 1.05, 0.35), scale=(0.04, 0.18, 0.04))
        assign_mat(seam, mats["SM_Orange"])
        parts.append(seam)

    # Dorsal spines along back → tail
    for i, y in enumerate((0.7, 0.35, 0.0, -0.35, -0.7, -1.0)):
        h = 0.32 - i * 0.03
        ridge = add_cone("TMP", 0.07, h, (0, y, 0.72 + h * 0.35))
        assign_mat(ridge, mats["SM_Black"])
        parts.append(ridge)

    # Thick tapered tail
    tail = add_uv_sphere("TMP", 0.22, (0, -1.15, 0.35), scale=(0.7, 1.6, 0.55), segments=16, rings=8)
    assign_mat(tail, mats["SM_Stalker"])
    parts.append(tail)
    tip = add_cone("TMP", 0.1, 0.35, (0, -1.65, 0.32))
    assign_mat(tip, mats["SM_Black"])
    parts.append(tip)

    # Four legs with bracer plates + claws
    for sx, sy in ((-0.42, 0.45), (0.42, 0.45), (-0.4, -0.4), (0.4, -0.4)):
        upper = add_cube("TMP", 1.0, (sx, sy, 0.32), scale=(0.14, 0.14, 0.28))
        assign_mat(upper, mats["SM_Stalker"])
        parts.append(upper)
        bracer = add_cube("TMP", 1.0, (sx * 1.05, sy, 0.18), scale=(0.16, 0.16, 0.12))
        assign_mat(bracer, mats["SM_White"])
        parts.append(bracer)
        # claws
        for cx in (-0.05, 0.05):
            claw = add_cone("TMP", 0.03, 0.12, (sx + cx, sy + 0.08, 0.06))
            assign_mat(claw, mats["SM_Black"])
            parts.append(claw)
        # orange joint glow
        glow = add_uv_sphere("TMP", 0.04, (sx, sy, 0.38), segments=8, rings=6)
        assign_mat(glow, mats["SM_Orange"])
        parts.append(glow)

    obj = join_parts(parts, name)
    col = ensure_collection("13_Units_Stalker")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (8.0, 0.0, 0.0)
    return obj


def build_medic(mats: dict) -> bpy.types.Object:
    """Field medic — white shell, cyan cross, kit satchel, orange beacon. ~2.3 m."""
    name = "SM_Unit_Medic"
    remove_if_exists(name)
    parts = []

    hips = add_cylinder("TMP", 0.28, 0.22, (0, 0, 0.62), vertices=18)
    assign_mat(hips, mats["SM_Black"])
    parts.append(hips)
    torso = add_cylinder("TMP", 0.34, 1.05, (0, 0, 1.25), vertices=22)
    assign_mat(torso, mats["SM_White"])
    parts.append(torso)
    band = add_cylinder("TMP", 0.38, 0.1, (0, 0, 1.05), vertices=22)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)
    head = add_uv_sphere("TMP", 0.22, (0, 0.04, 1.92), segments=16, rings=10)
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    visor = add_cube("TMP", 1.0, (0, 0.2, 1.92), scale=(0.28, 0.04, 0.08))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    cross_h = add_cube("TMP", 1.0, (0, 0.36, 1.42), scale=(0.22, 0.04, 0.06))
    assign_mat(cross_h, mats["SM_Cyan"])
    parts.append(cross_h)
    cross_v = add_cube("TMP", 1.0, (0, 0.36, 1.42), scale=(0.06, 0.04, 0.22))
    assign_mat(cross_v, mats["SM_Cyan"])
    parts.append(cross_v)
    kit = add_uv_sphere("TMP", 0.16, (0.38, 0.02, 1.05), scale=(1.1, 0.8, 0.85), segments=12, rings=8)
    assign_mat(kit, mats["SM_White"])
    parts.append(kit)
    kit_stripe = add_cube("TMP", 1.0, (0.5, 0.02, 1.05), scale=(0.04, 0.12, 0.16))
    assign_mat(kit_stripe, mats["SM_Orange"])
    parts.append(kit_stripe)
    beacon = add_cube("TMP", 1.0, (-0.18, 0.0, 2.22), scale=(0.07, 0.07, 0.08))
    assign_mat(beacon, mats["SM_Orange"])
    parts.append(beacon)
    for sx in (-0.22, 0.22):
        thigh = add_cube("TMP", 1.0, (sx, 0.02, 0.38), scale=(0.14, 0.16, 0.32))
        assign_mat(thigh, mats["SM_White"])
        parts.append(thigh)
        boot = add_cube("TMP", 1.0, (sx, 0.08, 0.07), scale=(0.18, 0.26, 0.08))
        assign_mat(boot, mats["SM_Black"])
        parts.append(boot)
    for sx in (-0.48, 0.48):
        arm = add_cube("TMP", 1.0, (sx, 0.04, 1.28), scale=(0.12, 0.12, 0.42))
        assign_mat(arm, mats["SM_Graphite"])
        parts.append(arm)

    obj = join_parts(parts, name)
    col = ensure_collection("14_Units_Medic")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (12.0, 0.0, 0.0)
    return obj


def build_harvester(mats: dict) -> bpy.types.Object:
    """Ore harvester — squat hopper, scoop, cyan visor. ~1.8 m."""
    name = "SM_Unit_HarvesterBot"
    remove_if_exists(name)
    parts = []

    belly = add_cylinder("TMP", 0.55, 0.7, (0, 0, 0.72), vertices=20)
    assign_mat(belly, mats["SM_White"])
    parts.append(belly)
    band = add_cylinder("TMP", 0.6, 0.1, (0, 0, 0.55), vertices=20)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)
    hopper = add_cube("TMP", 1.0, (0, -0.42, 1.15), scale=(0.7, 0.42, 0.55))
    assign_mat(hopper, mats["SM_Steel"])
    parts.append(hopper)
    hop_lip = add_cube("TMP", 1.0, (0, -0.42, 1.42), scale=(0.62, 0.36, 0.06))
    assign_mat(hop_lip, mats["SM_Orange"])
    parts.append(hop_lip)
    scoop = add_cube("TMP", 1.0, (0, 0.58, 0.42), scale=(0.85, 0.22, 0.14))
    assign_mat(scoop, mats["SM_Orange"])
    parts.append(scoop)
    scoop_arm = add_cube("TMP", 1.0, (0, 0.38, 0.62), scale=(0.18, 0.32, 0.1))
    assign_mat(scoop_arm, mats["SM_Graphite"])
    parts.append(scoop_arm)
    visor = add_cube("TMP", 1.0, (0, 0.5, 1.05), scale=(0.48, 0.06, 0.1))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    head = add_cube("TMP", 1.0, (0, 0.28, 1.08), scale=(0.5, 0.28, 0.28))
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    for sx in (-0.38, 0.38):
        track = add_cube("TMP", 1.0, (sx, 0.0, 0.16), scale=(0.22, 0.7, 0.18))
        assign_mat(track, mats["SM_Black"])
        parts.append(track)

    obj = join_parts(parts, name)
    col = ensure_collection("15_Units_Harvester")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (16.0, 0.0, 0.0)
    return obj


def build_surveyor(mats: dict) -> bpy.types.Object:
    """Surveyor — tall mast, dish, cyan lens. ~2.8 m."""
    name = "SM_Unit_SurveyorBot"
    remove_if_exists(name)
    parts = []

    hips = add_cylinder("TMP", 0.22, 0.2, (0, 0, 0.55), vertices=16)
    assign_mat(hips, mats["SM_Black"])
    parts.append(hips)
    torso = add_cylinder("TMP", 0.24, 1.15, (0, 0, 1.25), vertices=20)
    assign_mat(torso, mats["SM_White"])
    parts.append(torso)
    band = add_cylinder("TMP", 0.28, 0.08, (0, 0, 1.45), vertices=20)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)
    lens = add_cube("TMP", 1.0, (0, 0.26, 1.55), scale=(0.28, 0.08, 0.1))
    assign_mat(lens, mats["SM_Cyan"])
    parts.append(lens)
    mast = add_cylinder("TMP", 0.035, 0.7, (0, 0, 2.15), vertices=10)
    assign_mat(mast, mats["SM_Steel"])
    parts.append(mast)
    dish = add_uv_sphere("TMP", 0.32, (0, 0, 2.52), scale=(1.0, 1.0, 0.22), segments=20, rings=8)
    assign_mat(dish, mats["SM_White"])
    parts.append(dish)
    dish_ring = add_cylinder("TMP", 0.34, 0.03, (0, 0, 2.52), vertices=20)
    assign_mat(dish_ring, mats["SM_Orange"])
    parts.append(dish_ring)
    for sx in (-0.18, 0.18):
        thigh = add_cube("TMP", 1.0, (sx, 0.02, 0.32), scale=(0.1, 0.12, 0.28))
        assign_mat(thigh, mats["SM_Graphite"])
        parts.append(thigh)
        foot = add_cube("TMP", 1.0, (sx, 0.08, 0.05), scale=(0.14, 0.22, 0.06))
        assign_mat(foot, mats["SM_Black"])
        parts.append(foot)
    beacon = add_uv_sphere("TMP", 0.045, (0.12, 0.0, 1.95), segments=10, rings=6)
    assign_mat(beacon, mats["SM_Orange"])
    parts.append(beacon)

    obj = join_parts(parts, name)
    col = ensure_collection("16_Units_Surveyor")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (20.0, 0.0, 0.0)
    return obj


def build_terraformer(mats: dict) -> bpy.types.Object:
    """Terraformer — tank backpack, spray boom, orange nozzles. ~2.1 m."""
    name = "SM_Unit_TerraformerBot"
    remove_if_exists(name)
    parts = []

    hips = add_cylinder("TMP", 0.32, 0.24, (0, 0, 0.58), vertices=18)
    assign_mat(hips, mats["SM_Black"])
    parts.append(hips)
    torso = add_cylinder("TMP", 0.38, 0.95, (0, 0, 1.18), vertices=22)
    assign_mat(torso, mats["SM_White"])
    parts.append(torso)
    band = add_cylinder("TMP", 0.42, 0.1, (0, 0, 0.92), vertices=22)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)
    head = add_cube("TMP", 1.0, (0, 0.08, 1.78), scale=(0.32, 0.3, 0.24))
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    visor = add_cube("TMP", 1.0, (0, 0.24, 1.78), scale=(0.26, 0.04, 0.08))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    for sx in (-0.22, 0.22):
        tank = add_cylinder("TMP", 0.14, 0.7, (sx, -0.38, 1.22), vertices=14)
        assign_mat(tank, mats["SM_Steel"])
        parts.append(tank)
        cap = add_uv_sphere("TMP", 0.08, (sx, -0.38, 1.6), segments=10, rings=6)
        assign_mat(cap, mats["SM_Orange"])
        parts.append(cap)
    boom = add_cube("TMP", 1.0, (0, 0.55, 1.15), scale=(0.9, 0.08, 0.08))
    assign_mat(boom, mats["SM_Graphite"])
    parts.append(boom)
    for sx in (-0.4, 0.4):
        nozzle = add_cone("TMP", 0.06, 0.16, (sx, 0.62, 1.02), vertices=10)
        assign_mat(nozzle, mats["SM_Orange"])
        parts.append(nozzle)
    for sx in (-0.22, 0.22):
        thigh = add_cube("TMP", 1.0, (sx, 0.02, 0.34), scale=(0.14, 0.16, 0.28))
        assign_mat(thigh, mats["SM_White"])
        parts.append(thigh)
        boot = add_cube("TMP", 1.0, (sx, 0.1, 0.06), scale=(0.18, 0.26, 0.08))
        assign_mat(boot, mats["SM_Black"])
        parts.append(boot)

    obj = join_parts(parts, name)
    col = ensure_collection("17_Units_Terraformer")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (24.0, 0.0, 0.0)
    return obj


def build_courier(mats: dict) -> bpy.types.Object:
    """Courier — cargo crate back, compact chassis, orange stripe. ~1.9 m."""
    name = "SM_Unit_CourierBot"
    remove_if_exists(name)
    parts = []

    chassis = add_cube("TMP", 1.0, (0, 0, 0.55), scale=(0.7, 0.85, 0.45))
    assign_mat(chassis, mats["SM_White"])
    parts.append(chassis)
    belly = add_cube("TMP", 1.0, (0, 0, 0.28), scale=(0.62, 0.78, 0.18))
    assign_mat(belly, mats["SM_Black"])
    parts.append(belly)
    crate = add_cube("TMP", 1.0, (0, -0.22, 1.05), scale=(0.55, 0.48, 0.5))
    assign_mat(crate, mats["SM_Steel"])
    parts.append(crate)
    stripe = add_cube("TMP", 1.0, (0, -0.48, 1.05), scale=(0.42, 0.04, 0.08))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    head = add_cube("TMP", 1.0, (0, 0.32, 0.95), scale=(0.4, 0.28, 0.28))
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    visor = add_cube("TMP", 1.0, (0, 0.48, 0.95), scale=(0.32, 0.04, 0.1))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    for sx, sy in ((-0.32, 0.28), (0.32, 0.28), (-0.32, -0.28), (0.32, -0.28)):
        wheel = add_cylinder(
            "TMP", 0.12, 0.08, (sx, sy, 0.14),
            rotation=(0, math.pi / 2, 0), vertices=12,
        )
        assign_mat(wheel, mats["SM_Graphite"])
        parts.append(wheel)
    beacon = add_uv_sphere("TMP", 0.05, (0.22, 0.18, 1.22), segments=10, rings=6)
    assign_mat(beacon, mats["SM_Orange"])
    parts.append(beacon)

    obj = join_parts(parts, name)
    col = ensure_collection("18_Units_Courier")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (28.0, 0.0, 0.0)
    return obj


def build_mite(mats: dict) -> bpy.types.Object:
    """Regolith mite — low beetle, rock plates, orange eyes. ~0.55 m."""
    name = "SM_Unit_RegolithMite"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere("TMP", 0.28, (0, 0, 0.22), scale=(1.15, 1.45, 0.7), segments=18, rings=10)
    assign_mat(body, mats["SM_Mite"])
    parts.append(body)
    plate = add_cube("TMP", 1.0, (0, -0.04, 0.36), scale=(0.42, 0.38, 0.08))
    assign_mat(plate, mats["SM_Graphite"])
    parts.append(plate)
    mandible = add_cube("TMP", 1.0, (0, 0.32, 0.18), scale=(0.22, 0.16, 0.1))
    assign_mat(mandible, mats["SM_Black"])
    parts.append(mandible)
    for x in (-0.1, 0.1):
        eye = add_uv_sphere("TMP", 0.04, (x, 0.28, 0.28), segments=10, rings=6)
        assign_mat(eye, mats["SM_Orange"])
        parts.append(eye)
    for sx, sy in ((-0.22, 0.12), (0.22, 0.12), (-0.2, -0.16), (0.2, -0.16)):
        leg = add_cube("TMP", 1.0, (sx, sy, 0.1), scale=(0.06, 0.06, 0.16))
        assign_mat(leg, mats["SM_Black"])
        parts.append(leg)

    obj = join_parts(parts, name)
    col = ensure_collection("19_Fauna_Mite")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (32.0, 0.0, 0.0)
    return obj


def build_leech(mats: dict) -> bpy.types.Object:
    """Watt leech — long body, cyan core, ice ridge. ~1.2 m long."""
    name = "SM_Unit_WattLeech"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere("TMP", 0.22, (0, 0, 0.18), scale=(0.7, 2.2, 0.55), segments=18, rings=10)
    assign_mat(body, mats["SM_Leech"])
    parts.append(body)
    core = add_uv_sphere("TMP", 0.12, (0, 0.12, 0.28), segments=12, rings=8)
    assign_mat(core, mats["SM_Cyan"])
    parts.append(core)
    ridge = add_cube("TMP", 1.0, (0, -0.08, 0.32), scale=(0.1, 0.7, 0.06))
    assign_mat(ridge, mats["SM_White"])
    parts.append(ridge)
    spark = add_cube("TMP", 1.0, (0, 0.38, 0.32), scale=(0.08, 0.1, 0.14))
    assign_mat(spark, mats["SM_Orange"])
    parts.append(spark)
    head = add_uv_sphere("TMP", 0.12, (0, 0.48, 0.2), scale=(0.9, 1.1, 0.7), segments=12, rings=8)
    assign_mat(head, mats["SM_Leech"])
    parts.append(head)

    obj = join_parts(parts, name)
    col = ensure_collection("20_Fauna_Leech")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (36.0, 0.0, 0.0)
    return obj


def build_geologist(mats: dict) -> bpy.types.Object:
    """
    Geologist — six-wheel rover (Imagine + Phase 4). Core-drill arm, sample crate,
    cyan sensor mast. Length ~2.1 m, height ~1.35 m. Not a biped.
    """
    name = "SM_Unit_GeologistBot"
    remove_if_exists(name)
    parts = []

    chassis = add_cube("TMP", 1.0, (0, 0, 0.42), scale=(0.7, 1.15, 0.28))
    assign_mat(chassis, mats["SM_White"])
    parts.append(chassis)
    belly = add_cube("TMP", 1.0, (0, 0, 0.22), scale=(0.62, 1.05, 0.16))
    assign_mat(belly, mats["SM_Black"])
    parts.append(belly)
    stripe = add_cube("TMP", 1.0, (0, 0.58, 0.5), scale=(0.42, 0.04, 0.12))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)

    crate = add_cube("TMP", 1.0, (0, -0.55, 0.62), scale=(0.48, 0.38, 0.28))
    assign_mat(crate, mats["SM_Graphite"])
    parts.append(crate)
    crate_cap = add_cube("TMP", 1.0, (0, -0.55, 0.78), scale=(0.36, 0.28, 0.06))
    assign_mat(crate_cap, mats["SM_White"])
    parts.append(crate_cap)

    mast = add_cylinder("TMP", 0.04, 0.55, (0.12, 0.22, 0.85), vertices=10)
    assign_mat(mast, mats["SM_Steel"])
    parts.append(mast)
    cluster = add_uv_sphere("TMP", 0.1, (0.12, 0.22, 1.16), segments=12, rings=8)
    assign_mat(cluster, mats["SM_White"])
    parts.append(cluster)
    eye = add_uv_sphere("TMP", 0.05, (0.12, 0.32, 1.16), segments=10, rings=6)
    assign_mat(eye, mats["SM_Cyan"])
    parts.append(eye)

    arm = add_cube("TMP", 1.0, (-0.12, 0.72, 0.55), scale=(0.1, 0.55, 0.1))
    assign_mat(arm, mats["SM_Graphite"])
    parts.append(arm)
    collar = add_cylinder(
        "TMP", 0.08, 0.08, (-0.12, 1.05, 0.55),
        rotation=(math.pi / 2, 0, 0), vertices=12,
    )
    assign_mat(collar, mats["SM_Orange"])
    parts.append(collar)
    bit = add_cone("TMP", 0.06, 0.2, (-0.12, 1.22, 0.42), vertices=10)
    assign_mat(bit, mats["SM_Steel"])
    parts.append(bit)

    # Six wheels: 3 per side, touching ground.
    for sx in (-0.42, 0.42):
        for i, sy in enumerate((-0.55, 0.0, 0.55)):
            wheel = add_cylinder(
                "TMP", 0.16, 0.1,
                (sx, sy, 0.16),
                rotation=(0, math.pi / 2, 0),
                vertices=14,
            )
            assign_mat(wheel, mats["SM_Black"])
            parts.append(wheel)
            hub = add_cylinder(
                "TMP", 0.06, 0.12,
                (sx, sy, 0.16),
                rotation=(0, math.pi / 2, 0),
                vertices=10,
            )
            assign_mat(hub, mats["SM_Steel"])
            parts.append(hub)

    obj = join_parts(parts, name)
    col = ensure_collection("21_Units_Geologist")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (40.0, 0.0, 0.0)
    return obj


def build_sentinel(mats: dict) -> bpy.types.Object:
    """Sentinel — squat turret chassis, chevrons, shield lip. ~1.7 m. Distinct from Defense."""
    name = "SM_Unit_SentinelMech"
    remove_if_exists(name)
    parts = []

    hull = add_cube("TMP", 1.0, (0, 0, 0.55), scale=(0.95, 0.85, 0.55))
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)
    skirt = add_cube("TMP", 1.0, (0, 0, 0.22), scale=(1.05, 0.95, 0.18))
    assign_mat(skirt, mats["SM_Black"])
    parts.append(skirt)
    chevron = add_cube("TMP", 1.0, (0, 0.44, 0.72), scale=(0.55, 0.04, 0.12))
    assign_mat(chevron, mats["SM_Orange"])
    parts.append(chevron)
    turret = add_cylinder("TMP", 0.28, 0.35, (0, 0, 1.05), vertices=16)
    assign_mat(turret, mats["SM_Sentinel"])
    parts.append(turret)
    barrel = add_cylinder(
        "TMP", 0.07, 0.55, (0, 0.38, 1.12),
        rotation=(math.pi / 2, 0, 0), vertices=12,
    )
    assign_mat(barrel, mats["SM_Graphite"])
    parts.append(barrel)
    shield = add_cube("TMP", 1.0, (-0.58, 0.12, 0.62), scale=(0.08, 0.7, 0.7))
    assign_mat(shield, mats["SM_Steel"])
    parts.append(shield)
    visor = add_cube("TMP", 1.0, (0, 0.22, 1.18), scale=(0.22, 0.04, 0.08))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    for sx, sy in ((-0.38, 0.32), (0.38, 0.32), (-0.38, -0.32), (0.38, -0.32)):
        track = add_cube("TMP", 1.0, (sx, sy, 0.10), scale=(0.22, 0.18, 0.12))
        assign_mat(track, mats["SM_Graphite"])
        parts.append(track)

    obj = join_parts(parts, name)
    col = ensure_collection("22_Units_Sentinel")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (44.0, 0.0, 0.0)
    return obj


def build_wisp(mats: dict) -> bpy.types.Object:
    """Ice wisp — shard cluster, cyan core. ~0.85 m hover silhouette."""
    name = "SM_Unit_IceWisp"
    remove_if_exists(name)
    parts = []

    core = add_uv_sphere("TMP", 0.16, (0, 0, 0.55), segments=14, rings=8)
    assign_mat(core, mats["SM_Cyan"])
    parts.append(core)
    halo = add_uv_sphere("TMP", 0.28, (0, 0, 0.55), scale=(1.0, 1.0, 0.7), segments=12, rings=8)
    assign_mat(halo, mats["SM_Wisp"])
    parts.append(halo)
    for i, (x, y, z, sx, sy, sz) in enumerate((
        (0.22, 0.08, 0.72, 0.08, 0.06, 0.28),
        (-0.18, -0.12, 0.42, 0.07, 0.05, 0.24),
        (0.05, 0.22, 0.38, 0.06, 0.18, 0.08),
        (-0.12, 0.10, 0.78, 0.05, 0.05, 0.20),
    )):
        shard = add_cube("TMP", 1.0, (x, y, z), scale=(sx, sy, sz))
        assign_mat(shard, mats["SM_White"] if i % 2 == 0 else mats["SM_Steel"])
        parts.append(shard)
    spark = add_uv_sphere("TMP", 0.05, (0.08, 0.18, 0.82), segments=8, rings=6)
    assign_mat(spark, mats["SM_Orange"])
    parts.append(spark)

    obj = join_parts(parts, name)
    col = ensure_collection("23_Fauna_Wisp")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (48.0, 0.0, 0.0)
    return obj


def build_tick(mats: dict) -> bpy.types.Object:
    """Rock tick — spiky crab, iron plates, orange pincers. ~0.45 m."""
    name = "SM_Unit_RockTick"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere("TMP", 0.18, (0, 0, 0.16), scale=(1.2, 1.35, 0.7), segments=14, rings=8)
    assign_mat(body, mats["SM_Tick"])
    parts.append(body)
    plate = add_cube("TMP", 1.0, (0, -0.02, 0.26), scale=(0.32, 0.28, 0.06))
    assign_mat(plate, mats["SM_Steel"])
    parts.append(plate)
    spike = add_cone("TMP", 0.06, 0.16, (0, -0.04, 0.36), vertices=8)
    assign_mat(spike, mats["SM_Graphite"])
    parts.append(spike)
    for x in (-0.12, 0.12):
        pincer = add_cube("TMP", 1.0, (x, 0.22, 0.14), scale=(0.05, 0.14, 0.05))
        assign_mat(pincer, mats["SM_Orange"])
        parts.append(pincer)
    for x in (-0.08, 0.08):
        eye = add_uv_sphere("TMP", 0.03, (x, 0.16, 0.22), segments=8, rings=6)
        assign_mat(eye, mats["SM_Orange"])
        parts.append(eye)
    for sx, sy in ((-0.18, 0.10), (0.18, 0.10), (-0.20, -0.08), (0.20, -0.08), (-0.12, -0.18), (0.12, -0.18)):
        leg = add_cube("TMP", 1.0, (sx, sy, 0.08), scale=(0.04, 0.04, 0.12))
        assign_mat(leg, mats["SM_Black"])
        parts.append(leg)

    obj = join_parts(parts, name)
    col = ensure_collection("24_Fauna_Tick")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (52.0, 0.0, 0.0)
    return obj


def build_creeper(mats: dict) -> bpy.types.Object:
    """Soil creeper — low millipede, soil plates, orange nubs. ~1.4 m long."""
    name = "SM_Unit_SoilCreeper"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere("TMP", 0.16, (0, 0, 0.14), scale=(0.7, 2.4, 0.55), segments=16, rings=10)
    assign_mat(body, mats["SM_Creeper"])
    parts.append(body)
    mid = add_uv_sphere("TMP", 0.14, (0, -0.18, 0.16), scale=(0.75, 1.4, 0.5), segments=12, rings=8)
    assign_mat(mid, mats["SM_Creeper"])
    parts.append(mid)
    plate = add_cube("TMP", 1.0, (0, -0.04, 0.26), scale=(0.22, 0.85, 0.06))
    assign_mat(plate, mats["SM_Graphite"])
    parts.append(plate)
    tendril = add_cube("TMP", 1.0, (0, -0.55, 0.12), scale=(0.06, 0.28, 0.06))
    assign_mat(tendril, mats["SM_Orange"])
    parts.append(tendril)
    head = add_uv_sphere("TMP", 0.11, (0, 0.42, 0.16), scale=(0.9, 1.1, 0.7), segments=12, rings=8)
    assign_mat(head, mats["SM_Creeper"])
    parts.append(head)
    for x in (-0.07, 0.07):
        nub = add_uv_sphere("TMP", 0.035, (x, 0.48, 0.22), segments=8, rings=6)
        assign_mat(nub, mats["SM_Orange"])
        parts.append(nub)
    sensor = add_uv_sphere("TMP", 0.03, (0, 0.52, 0.20), segments=8, rings=6)
    assign_mat(sensor, mats["SM_Cyan"])
    parts.append(sensor)
    for sy in (0.28, 0.08, -0.12, -0.32):
        for sx in (-0.14, 0.14):
            leg = add_cube("TMP", 1.0, (sx, sy, 0.06), scale=(0.05, 0.05, 0.10))
            assign_mat(leg, mats["SM_Black"])
            parts.append(leg)

    obj = join_parts(parts, name)
    col = ensure_collection("25_Fauna_Creeper")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (56.0, 0.0, 0.0)
    return obj


def build_hopper(mats: dict) -> bpy.types.Object:
    """Ash hopper — compact body, long legs, cyan eyes. ~1.1 m tall."""
    name = "SM_Unit_AshHopper"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere("TMP", 0.18, (0, 0, 0.72), scale=(1.05, 1.2, 0.85), segments=14, rings=8)
    assign_mat(body, mats["SM_Hopper"])
    parts.append(body)
    abdomen = add_uv_sphere("TMP", 0.12, (0, -0.16, 0.58), scale=(1.0, 1.15, 0.8), segments=12, rings=8)
    assign_mat(abdomen, mats["SM_Graphite"])
    parts.append(abdomen)
    for x in (-0.08, 0.08):
        eye = add_uv_sphere("TMP", 0.04, (x, 0.16, 0.82), segments=8, rings=6)
        assign_mat(eye, mats["SM_Cyan"])
        parts.append(eye)
    stripe = add_cube("TMP", 1.0, (0, 0.18, 0.70), scale=(0.16, 0.04, 0.08))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    for sx, sy in ((-0.22, 0.16), (0.22, 0.16), (-0.20, -0.16), (0.20, -0.16)):
        thigh = add_cube("TMP", 1.0, (sx * 0.55, sy * 0.45, 0.48), scale=(0.06, 0.06, 0.38))
        assign_mat(thigh, mats["SM_Black"])
        parts.append(thigh)
        knee = add_uv_sphere("TMP", 0.04, (sx, sy, 0.28), segments=8, rings=6)
        assign_mat(knee, mats["SM_Orange"])
        parts.append(knee)
        shin = add_cube("TMP", 1.0, (sx, sy, 0.16), scale=(0.05, 0.05, 0.22))
        assign_mat(shin, mats["SM_Steel"])
        parts.append(shin)
        foot = add_cube("TMP", 1.0, (sx * 1.15, sy * 1.15, 0.05), scale=(0.08, 0.10, 0.05))
        assign_mat(foot, mats["SM_Black"])
        parts.append(foot)

    obj = join_parts(parts, name)
    col = ensure_collection("26_Fauna_Hopper")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (60.0, 0.0, 0.0)
    return obj


def export_one(obj: bpy.types.Object):
    EXPORT_DIR.mkdir(parents=True, exist_ok=True)
    # Park at origin for clean export pivot
    loc = obj.location.copy()
    obj.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    fbx = EXPORT_DIR / f"{obj.name}.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(fbx),
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        apply_unit_scale=True,
        object_types={"MESH"},
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
    )
    print(f"[SM] Exported {fbx.name}")
    try:
        glb = EXPORT_DIR / f"{obj.name}.glb"
        bpy.ops.export_scene.gltf(
            filepath=str(glb),
            use_selection=True,
            export_format="GLB",
            export_apply=True,
        )
        print(f"[SM] Exported {glb.name}")
    except Exception as e:
        print(f"[SM] GLB skip: {e}")

    obj.location = loc


def copy_to_unity(name: str):
    UNITY_UNITS.mkdir(parents=True, exist_ok=True)
    src = EXPORT_DIR / f"{name}.fbx"
    if not src.is_file():
        print(f"[SM] Missing export {src}")
        return
    dst = UNITY_UNITS / f"{name}.fbx"
    shutil.copy2(src, dst)
    print(f"[SM] Copied → {dst.relative_to(PROJECT_ROOT)}")


def dims_report(obj: bpy.types.Object) -> str:
    bpy.context.view_layer.update()
    mat = obj.matrix_world
    corners = [mat @ Vector(c) for c in obj.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return (
        f"{obj.name}: "
        f"W={max(xs)-min(xs):.2f}m  D={max(ys)-min(ys):.2f}m  H={max(zs)-min(zs):.2f}m"
    )


def main():
    print("[SM] === Unit hero blockouts ===")
    reset_scene()
    mats = create_palette()
    units = [
        build_scout(mats),
        build_engineer(mats),
        build_defense(mats),
        build_stalker(mats),
        build_medic(mats),
        build_harvester(mats),
        build_surveyor(mats),
        build_terraformer(mats),
        build_courier(mats),
        build_geologist(mats),
        build_sentinel(mats),
        build_mite(mats),
        build_leech(mats),
        build_wisp(mats),
        build_tick(mats),
        build_creeper(mats),
        build_hopper(mats),
    ]
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))
    print(f"[SM] Saved {BLEND_OUT}")
    for u in units:
        print(" ", dims_report(u))
        export_one(u)
        copy_to_unity(u.name)
    print("[SM] === Done ===")


if __name__ == "__main__":
    main()
