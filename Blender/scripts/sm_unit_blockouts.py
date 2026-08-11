"""
Solar Majesty — hero unit blockouts (Scout / Engineer / Defense / Dust Stalker).

Readable Majesty-scale silhouettes with SpaceX white/black/orange palette.
Scout refined against ConceptSheets/SM_Unit_ScoutDrone_Turnaround.jpg (Imagine).

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
    LO-SCT-1 Scout — refined against ConceptSheets/SM_Unit_ScoutDrone_Turnaround.jpg.
    Tall tripod probe: white thermal shell, black frame, cyan eye, whip antenna, orange beacons.
    Game-readable height ~3.6 m (sheet lists 4.2 m).
    """
    name = "SM_Unit_ScoutDrone"
    remove_if_exists(name)
    parts = []

    # Lower hip / pelvis (black structural)
    hip = add_cylinder("TMP", 0.32, 0.28, (0, 0, 1.05), vertices=20)
    assign_mat(hip, mats["SM_Black"])
    parts.append(hip)

    # Main torso — white thermal shell (elongated)
    torso = add_cylinder("TMP", 0.30, 1.35, (0, 0, 1.85), vertices=24)
    assign_mat(torso, mats["SM_White"])
    parts.append(torso)

    # Mid black structural band
    band = add_cylinder("TMP", 0.34, 0.12, (0, 0, 1.55), vertices=24)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)

    # Upper chest collar
    collar = add_cylinder("TMP", 0.33, 0.16, (0, 0, 2.45), vertices=20)
    assign_mat(collar, mats["SM_Black"])
    parts.append(collar)

    # Sensor head — small box + cyan eye
    head = add_cube("TMP", 1.0, (0, 0.05, 2.78), scale=(0.28, 0.32, 0.26))
    assign_mat(head, mats["SM_White"])
    parts.append(head)

    eye = add_uv_sphere("TMP", 0.09, (0, 0.22, 2.78), segments=16, rings=8)
    assign_mat(eye, mats["SM_Cyan"])
    parts.append(eye)

    # Orange shoulder beacons
    for sx in (-0.28, 0.28):
        beacon = add_uv_sphere("TMP", 0.06, (sx, 0.0, 2.55), segments=12, rings=6)
        assign_mat(beacon, mats["SM_Orange"])
        parts.append(beacon)

    # Whip antenna (tall, steel + orange tip)
    ant = add_cylinder("TMP", 0.025, 1.05, (0.08, -0.05, 3.45), vertices=10)
    assign_mat(ant, mats["SM_Steel"])
    parts.append(ant)
    tip = add_uv_sphere("TMP", 0.045, (0.08, -0.05, 4.0), segments=10, rings=6)
    assign_mat(tip, mats["SM_Orange"])
    parts.append(tip)

    # Tripod legs (3) — black/steel articulations + flat feet
    for i in range(3):
        ang = (2.0 * math.pi * i) / 3.0 + math.pi / 6.0
        # hip attachment
        hx = math.cos(ang) * 0.28
        hy = math.sin(ang) * 0.28
        thigh = add_cube("TMP", 1.0, (hx * 1.4, hy * 1.4, 0.72), scale=(0.1, 0.1, 0.55))
        assign_mat(thigh, mats["SM_Black"])
        parts.append(thigh)
        # shin
        fx = math.cos(ang) * 0.55
        fy = math.sin(ang) * 0.55
        shin = add_cube("TMP", 1.0, (fx, fy, 0.28), scale=(0.08, 0.08, 0.42))
        assign_mat(shin, mats["SM_Steel"])
        parts.append(shin)
        # foot pad
        foot = add_cube("TMP", 1.0, (fx * 1.15, fy * 1.15, 0.05), scale=(0.22, 0.16, 0.05))
        assign_mat(foot, mats["SM_Graphite"])
        parts.append(foot)

    obj = join_parts(parts, name)
    col = ensure_collection("10_Units_Scout")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (-4.0, 0.0, 0.0)
    return obj


def build_engineer(mats: dict) -> bpy.types.Object:
    """
    Engineer Bot — refined against ConceptSheets/SM_Unit_EngineerBot_Turnaround.jpg (v2).
    Stocky habitat builder: weathered white shell, cyan visor, orange arm/leg stripes,
    chest dock port, rear cargo backpack. Game height ~2.2 m. No weapons.
    """
    name = "SM_Unit_EngineerBot"
    remove_if_exists(name)
    parts = []

    # Pelvis
    hips = add_cylinder("TMP", 0.40, 0.32, (0, 0, 0.58), vertices=20)
    assign_mat(hips, mats["SM_Black"])
    parts.append(hips)

    # Torso — weathered white (use white + slight graphite overlay via plates)
    torso = add_cylinder("TMP", 0.50, 0.95, (0, 0, 1.22), vertices=24)
    assign_mat(torso, mats["SM_White"])
    parts.append(torso)

    # Chest armor plate + circular dock port
    chest = add_cube("TMP", 1.0, (0, 0.38, 1.30), scale=(0.58, 0.12, 0.55))
    assign_mat(chest, mats["SM_White"])
    parts.append(chest)
    dock = add_cylinder("TMP", 0.14, 0.08, (0, 0.48, 1.28), rotation=(math.pi / 2, 0, 0), vertices=20)
    assign_mat(dock, mats["SM_Graphite"])
    parts.append(dock)
    dock_ring = add_cylinder("TMP", 0.17, 0.04, (0, 0.50, 1.28), rotation=(math.pi / 2, 0, 0), vertices=20)
    assign_mat(dock_ring, mats["SM_Orange"])
    parts.append(dock_ring)

    # Black mid band
    band = add_cylinder("TMP", 0.54, 0.12, (0, 0, 0.92), vertices=24)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)

    # Head + cyan visor
    head = add_cube("TMP", 1.0, (0, 0.08, 1.92), scale=(0.40, 0.36, 0.28))
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    visor = add_cube("TMP", 1.0, (0, 0.30, 1.92), scale=(0.36, 0.05, 0.09))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)

    # Rear cargo / power backpack
    pack = add_cube("TMP", 1.0, (0, -0.42, 1.25), scale=(0.55, 0.32, 0.7))
    assign_mat(pack, mats["SM_Graphite"])
    parts.append(pack)
    pack_shell = add_cube("TMP", 1.0, (0, -0.52, 1.35), scale=(0.48, 0.12, 0.5))
    assign_mat(pack_shell, mats["SM_White"])
    parts.append(pack_shell)
    pack_stripe = add_cube("TMP", 1.0, (0, -0.58, 1.35), scale=(0.08, 0.04, 0.4))
    assign_mat(pack_stripe, mats["SM_Orange"])
    parts.append(pack_stripe)

    # Arms — thick shoulders, orange upper-arm stripes, 3-finger hands
    for sx in (-0.68, 0.68):
        shoulder = add_cube("TMP", 1.0, (sx, 0.05, 1.55), scale=(0.28, 0.28, 0.28))
        assign_mat(shoulder, mats["SM_White"])
        parts.append(shoulder)
        upper = add_cube("TMP", 1.0, (sx * 1.05, 0.08, 1.2), scale=(0.2, 0.2, 0.4))
        assign_mat(upper, mats["SM_Graphite"])
        parts.append(upper)
        stripe = add_cube("TMP", 1.0, (sx * 1.18, 0.08, 1.25), scale=(0.05, 0.16, 0.28))
        assign_mat(stripe, mats["SM_Orange"])
        parts.append(stripe)
        lower = add_cube("TMP", 1.0, (sx * 1.1, 0.18, 0.85), scale=(0.16, 0.16, 0.32))
        assign_mat(lower, mats["SM_Steel"])
        parts.append(lower)
        # hand block + fingers
        hand = add_cube("TMP", 1.0, (sx * 1.12, 0.28, 0.62), scale=(0.14, 0.16, 0.12))
        assign_mat(hand, mats["SM_Black"])
        parts.append(hand)
        for fy in (-0.06, 0.0, 0.06):
            finger = add_cube("TMP", 1.0, (sx * 1.12, 0.38 + fy * 0.1, 0.52), scale=(0.04, 0.1, 0.04))
            assign_mat(finger, mats["SM_Graphite"])
            parts.append(finger)

    # Legs — orange lower-leg stripes, industrial boots
    for sx in (-0.28, 0.28):
        thigh = add_cube("TMP", 1.0, (sx, 0.02, 0.38), scale=(0.22, 0.24, 0.38))
        assign_mat(thigh, mats["SM_White"])
        parts.append(thigh)
        shin = add_cube("TMP", 1.0, (sx, 0.05, 0.18), scale=(0.2, 0.22, 0.22))
        assign_mat(shin, mats["SM_Graphite"])
        parts.append(shin)
        leg_stripe = add_cube("TMP", 1.0, (sx, 0.18, 0.2), scale=(0.16, 0.05, 0.14))
        assign_mat(leg_stripe, mats["SM_Orange"])
        parts.append(leg_stripe)
        boot = add_cube("TMP", 1.0, (sx, 0.12, 0.06), scale=(0.28, 0.38, 0.1))
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
    Defense Mech Guardian — refined against ConceptSheets/SM_Unit_DefenseMech_Turnaround.jpg.
    Tracked combat chassis: 4 tread pods, white ceramic hull, red class ID face,
    modular shoulder blocks, orange hazard accents. Game height ~2.0 m.
    """
    name = "SM_Unit_DefenseMech"
    remove_if_exists(name)
    parts = []

    # Central undercarriage / belly (black carbon-titanium)
    belly = add_cube("TMP", 1.0, (0, 0, 0.45), scale=(0.85, 1.05, 0.35))
    assign_mat(belly, mats["SM_Black"])
    parts.append(belly)

    # Four independent tread pods (front L/R, rear L/R)
    for sx, sy in ((-0.55, 0.45), (0.55, 0.45), (-0.55, -0.45), (0.55, -0.45)):
        bogie = add_cube("TMP", 1.0, (sx, sy, 0.22), scale=(0.32, 0.42, 0.28))
        assign_mat(bogie, mats["SM_Graphite"])
        parts.append(bogie)
        # track block (low)
        track = add_cube("TMP", 1.0, (sx, sy, 0.08), scale=(0.36, 0.48, 0.1))
        assign_mat(track, mats["SM_Black"])
        parts.append(track)
        # wheels suggestion
        for wy in (-0.12, 0.12):
            wh = add_cylinder(
                "TMP", 0.1, 0.08,
                (sx + (0.12 if sx > 0 else -0.12), sy + wy, 0.14),
                rotation=(0, math.pi / 2, 0),
                vertices=12,
            )
            assign_mat(wh, mats["SM_Steel"])
            parts.append(wh)

    # Main hull — white ceramic, sloping forward
    hull = add_cube("TMP", 1.0, (0, 0.05, 0.95), scale=(0.95, 1.0, 0.7))
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)

    # Red class-ID front face / hex panel stand-in
    face = add_cube("TMP", 1.0, (0, 0.55, 1.0), scale=(0.55, 0.08, 0.45))
    assign_mat(face, mats["SM_Defense"])
    parts.append(face)
    # Hex-ish sensor (cylinder facade)
    sensor = add_cylinder("TMP", 0.18, 0.06, (0, 0.62, 1.05), rotation=(math.pi / 2, 0, 0), vertices=6)
    assign_mat(sensor, mats["SM_Defense"])
    parts.append(sensor)

    # Small crest above face
    crest = add_cube("TMP", 1.0, (0, 0.52, 1.35), scale=(0.14, 0.06, 0.12))
    assign_mat(crest, mats["SM_Orange"])
    parts.append(crest)

    # Modular shoulder pods L/R
    for sx in (-0.85, 0.85):
        shoulder = add_cube("TMP", 1.0, (sx, 0.0, 1.15), scale=(0.45, 0.7, 0.55))
        assign_mat(shoulder, mats["SM_White"])
        parts.append(shoulder)
        # red side panel (class ID)
        panel = add_cube("TMP", 1.0, (sx * 1.15, 0.15, 1.15), scale=(0.08, 0.4, 0.35))
        assign_mat(panel, mats["SM_Defense"])
        parts.append(panel)
        # orange hazard strip
        haz = add_cube("TMP", 1.0, (sx, 0.38, 1.4), scale=(0.32, 0.06, 0.06))
        assign_mat(haz, mats["SM_Orange"])
        parts.append(haz)

    # Top sensor / comms turret
    turret = add_cylinder("TMP", 0.22, 0.28, (0, -0.05, 1.5), vertices=16)
    assign_mat(turret, mats["SM_Graphite"])
    parts.append(turret)
    dome = add_uv_sphere("TMP", 0.16, (0, -0.05, 1.68), scale=(1.0, 1.0, 0.7), segments=16, rings=8)
    assign_mat(dome, mats["SM_White"])
    parts.append(dome)

    # Rear black banding
    rear = add_cube("TMP", 1.0, (0, -0.55, 0.9), scale=(0.7, 0.12, 0.5))
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
