"""
Solar Majesty — hero unit blockouts.

Readable Majesty-scale silhouettes with SpaceX white/black/orange palette.
Scout / Engineer / Defense / Stalker matched to ConceptSheets turnarounds
(hover Scout keeps rotors for the mockup + smoke test; Defense stays tracked).
Remaining classes + leftover fauna are sheet-matched to ConceptSheets
SM_Unit_*_Turnaround.jpg (Imagine LO-*-1). Keep in-game RTS sizes — do not
shrink Soil Creeper / Ash Hopper to the sheet scale bars. No purple. Sensors cyan.
Do not clone Engineer biped, Scout hover, or Defense Guardian (red viewport /
huge shoulder pods). Sentinel uses continuous treads (sheet), not stub pads.

Run:
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    --python Blender/scripts/sm_unit_blockouts.py
"""

from __future__ import annotations

import math
import shutil
import sys
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
        "SM_Leech": make_principled("SM_Leech", (0.88, 0.90, 0.92), 0.10, 0.32),
        "SM_Ice": make_principled("SM_Ice", (0.78, 0.92, 0.98), 0.02, 0.14),
        "SM_DustBrown": make_principled("SM_DustBrown", (0.62, 0.48, 0.32), 0.06, 0.64),
        "SM_Geologist": make_principled("SM_Geologist", (0.62, 0.48, 0.28), 0.18, 0.48),
        "SM_Sentinel": make_principled("SM_Sentinel", (0.52, 0.16, 0.14), 0.14, 0.42),
        "SM_Wisp": make_principled("SM_Wisp", (0.62, 0.88, 0.96), 0.04, 0.22),
        "SM_Tick": make_principled("SM_Tick", (0.28, 0.24, 0.22), 0.22, 0.55),
        "SM_Creeper": make_principled("SM_Creeper", (0.32, 0.42, 0.18), 0.08, 0.62),
        "SM_Hopper": make_principled("SM_Hopper", (0.52, 0.50, 0.46), 0.10, 0.52),
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


def apply_rot(obj: bpy.types.Object):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)


def add_shaft(name, radius, start, end, vertices=8):
    """Cylinder spanning start→end (Blender +Z aligned, then rotated)."""
    s = Vector(start)
    e = Vector(end)
    delta = e - s
    length = max(delta.length, 0.01)
    mid = (s + e) * 0.5
    obj = add_cylinder(name, radius, length, mid, vertices=vertices)
    obj.rotation_euler = Vector((0.0, 0.0, 1.0)).rotation_difference(delta).to_euler()
    apply_rot(obj)
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
    Scout drone — Imagine LO-SCT-1 fuselage + Phase 4 hover.
    Boxy cyan-lens head, orange neck ring, whip antenna, white thermal shell,
    four rotor rings. Not a Surveyor tripod. NavMesh still walks. Height ~2.75 m.
    """
    name = "SM_Unit_ScoutDrone"
    remove_if_exists(name)
    parts = []

    # Ground-contact hover cushion so SnapToGround does not bury the rotors.
    pad = add_cylinder("TMP", 0.24, 0.05, (0, 0, 0.04), vertices=16)
    assign_mat(pad, mats["SM_Graphite"])
    parts.append(pad)
    stem = add_cylinder("TMP", 0.055, 0.78, (0, 0, 0.46), vertices=10)
    assign_mat(stem, mats["SM_Black"])
    parts.append(stem)

    # Cruciform rotor arms (hover read vs Imagine tripod / Surveyor mast).
    for i in range(4):
        ang = (math.pi * 0.25) + i * (math.pi * 0.5)
        ax = math.cos(ang) * 0.48
        ay = math.sin(ang) * 0.48
        arm = add_cube("TMP", 1.0, (ax * 0.5, ay * 0.5, 0.98), scale=(0.44, 0.055, 0.055))
        arm.rotation_euler = (0.0, 0.0, ang)
        apply_rot(arm)
        assign_mat(arm, mats["SM_White"])
        parts.append(arm)
        ring = add_cylinder("TMP", 0.20, 0.04, (ax, ay, 0.98), vertices=16)
        assign_mat(ring, mats["SM_Black"])
        parts.append(ring)
        disc = add_cylinder("TMP", 0.15, 0.02, (ax, ay, 1.01), vertices=12)
        assign_mat(disc, mats["SM_Steel"])
        parts.append(disc)

    # Tall thermal fuselage (sheet: white shell, black structural bands).
    torso = add_cylinder("TMP", 0.18, 1.05, (0, 0, 1.55), vertices=20)
    assign_mat(torso, mats["SM_White"])
    parts.append(torso)
    for z in (1.18, 1.52, 1.88):
        band = add_cylinder("TMP", 0.205, 0.07, (0, 0, z), vertices=20)
        assign_mat(band, mats["SM_Black"])
        parts.append(band)
    chevron = add_cube("TMP", 1.0, (0.19, 0.0, 1.62), scale=(0.04, 0.12, 0.16))
    assign_mat(chevron, mats["SM_Black"])
    parts.append(chevron)

    # Orange neck collar + boxy sensor head + cyan circular lens.
    collar = add_cylinder("TMP", 0.16, 0.10, (0, 0.02, 2.12), vertices=16)
    assign_mat(collar, mats["SM_Orange"])
    parts.append(collar)
    head = add_cube("TMP", 1.0, (0, 0.06, 2.32), scale=(0.28, 0.32, 0.26))
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    lens = add_cylinder(
        "TMP", 0.09, 0.06, (0, 0.24, 2.32),
        rotation=(math.pi / 2, 0, 0), vertices=16,
    )
    assign_mat(lens, mats["SM_Cyan"])
    parts.append(lens)
    beacon = add_uv_sphere("TMP", 0.05, (0.16, -0.04, 1.95), segments=10, rings=6)
    assign_mat(beacon, mats["SM_Orange"])
    parts.append(beacon)

    ant = add_cylinder("TMP", 0.018, 0.85, (0.04, -0.08, 2.82), vertices=8)
    assign_mat(ant, mats["SM_Steel"])
    parts.append(ant)
    tip = add_uv_sphere("TMP", 0.032, (0.04, -0.08, 3.26), segments=8, rings=6)
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
    Engineer Bot — Imagine v2 habitat-builder biped.
    Hunched white dome, cyan visor, chest docks, crate backpack, orange arm
    stripes, chunky three-finger hands, treaded boots. Height ~1.95 m.
    """
    name = "SM_Unit_EngineerBot"
    remove_if_exists(name)
    parts = []

    hips = add_cylinder("TMP", 0.32, 0.24, (0, 0.02, 0.55), vertices=18)
    assign_mat(hips, mats["SM_Black"])
    parts.append(hips)
    torso = add_uv_sphere("TMP", 0.42, (0, 0.04, 1.12), scale=(1.12, 0.92, 1.18), segments=20, rings=12)
    assign_mat(torso, mats["SM_White"])
    parts.append(torso)
    band = add_cylinder("TMP", 0.44, 0.09, (0, 0.04, 0.88), vertices=20)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)

    for dx in (-0.12, 0.12):
        dock = add_cylinder(
            "TMP", 0.09, 0.05, (dx, 0.40, 1.12),
            rotation=(math.pi / 2, 0, 0), vertices=14,
        )
        assign_mat(dock, mats["SM_Graphite"])
        parts.append(dock)
        ring = add_cylinder(
            "TMP", 0.12, 0.03, (dx, 0.44, 1.12),
            rotation=(math.pi / 2, 0, 0), vertices=14,
        )
        assign_mat(ring, mats["SM_Orange"])
        parts.append(ring)

    head = add_uv_sphere("TMP", 0.22, (0, 0.10, 1.62), scale=(1.05, 1.15, 0.92), segments=16, rings=10)
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    visor = add_cube("TMP", 1.0, (0, 0.28, 1.62), scale=(0.34, 0.05, 0.10))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)

    pack = add_cube("TMP", 1.0, (0, -0.38, 1.12), scale=(0.48, 0.28, 0.58))
    assign_mat(pack, mats["SM_Graphite"])
    parts.append(pack)
    pack_lid = add_cube("TMP", 1.0, (0, -0.38, 1.42), scale=(0.44, 0.24, 0.06))
    assign_mat(pack_lid, mats["SM_White"])
    parts.append(pack_lid)
    pack_stripe = add_cube("TMP", 1.0, (0, -0.53, 1.12), scale=(0.08, 0.03, 0.36))
    assign_mat(pack_stripe, mats["SM_Orange"])
    parts.append(pack_stripe)
    for lz in (0.92, 1.22):
        latch = add_cube("TMP", 1.0, (0.18, -0.54, lz), scale=(0.08, 0.04, 0.06))
        assign_mat(latch, mats["SM_Steel"])
        parts.append(latch)

    toolbox = add_cube("TMP", 1.0, (0.48, 0.04, 0.78), scale=(0.24, 0.18, 0.20))
    assign_mat(toolbox, mats["SM_Black"])
    parts.append(toolbox)
    tool_stripe = add_cube("TMP", 1.0, (0.48, 0.20, 0.78), scale=(0.20, 0.03, 0.05))
    assign_mat(tool_stripe, mats["SM_Orange"])
    parts.append(tool_stripe)

    for sx in (-0.52, 0.52):
        shoulder = add_uv_sphere("TMP", 0.14, (sx, 0.04, 1.32), segments=12, rings=8)
        assign_mat(shoulder, mats["SM_White"])
        parts.append(shoulder)
        upper = add_cube("TMP", 1.0, (sx * 1.12, 0.12, 1.02), scale=(0.14, 0.14, 0.32))
        assign_mat(upper, mats["SM_Graphite"])
        parts.append(upper)
        stripe = add_cube("TMP", 1.0, (sx * 1.24, 0.12, 1.08), scale=(0.04, 0.12, 0.20))
        assign_mat(stripe, mats["SM_Orange"])
        parts.append(stripe)
        lower = add_cube("TMP", 1.0, (sx * 1.14, 0.22, 0.72), scale=(0.12, 0.12, 0.24))
        assign_mat(lower, mats["SM_Steel"])
        parts.append(lower)
        hand = add_cube("TMP", 1.0, (sx * 1.16, 0.32, 0.54), scale=(0.12, 0.14, 0.12))
        assign_mat(hand, mats["SM_Black"])
        parts.append(hand)
        for fy in (-0.05, 0.0, 0.05):
            finger = add_cube("TMP", 1.0, (sx * 1.16, 0.42, 0.52 + fy * 0.4), scale=(0.03, 0.08, 0.03))
            assign_mat(finger, mats["SM_Black"])
            parts.append(finger)

    for sx in (-0.18, 0.18):
        thigh = add_cube("TMP", 1.0, (sx, 0.04, 0.36), scale=(0.16, 0.18, 0.30))
        assign_mat(thigh, mats["SM_White"])
        parts.append(thigh)
        shin = add_cube("TMP", 1.0, (sx, 0.06, 0.16), scale=(0.14, 0.16, 0.16))
        assign_mat(shin, mats["SM_Graphite"])
        parts.append(shin)
        boot = add_cube("TMP", 1.0, (sx, 0.14, 0.05), scale=(0.20, 0.30, 0.08))
        assign_mat(boot, mats["SM_Black"])
        parts.append(boot)
        tread = add_cube("TMP", 1.0, (sx, 0.16, 0.015), scale=(0.18, 0.26, 0.03))
        assign_mat(tread, mats["SM_Graphite"])
        parts.append(tread)

    obj = join_parts(parts, name)
    col = ensure_collection("11_Units_Engineer")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (0.0, 0.0, 0.0)
    return obj


def build_defense(mats: dict) -> bpy.types.Object:
    """
    Defense Mech — Imagine Guardian Class tracked hull.
    Continuous carbon treads, sloping white ceramic, large dark-red viewport,
    massive shoulder pods with red ports, small roof turret. Not a biped.
    Height ~2.05 m, width ~2.4 m.
    """
    name = "SM_Unit_DefenseMech"
    remove_if_exists(name)
    parts = []

    belly = add_cube("TMP", 1.0, (0, 0.02, 0.52), scale=(1.05, 1.28, 0.42))
    assign_mat(belly, mats["SM_Black"])
    parts.append(belly)

    for sx in (-0.78, 0.78):
        track = add_cube("TMP", 1.0, (sx, 0.02, 0.22), scale=(0.42, 1.58, 0.32))
        assign_mat(track, mats["SM_Black"])
        parts.append(track)
        skirt = add_cube("TMP", 1.0, (sx, 0.02, 0.42), scale=(0.36, 1.48, 0.10))
        assign_mat(skirt, mats["SM_Graphite"])
        parts.append(skirt)
        for wy in (-0.52, -0.18, 0.18, 0.52):
            wh = add_cylinder(
                "TMP", 0.13, 0.10,
                (sx + (0.16 if sx > 0 else -0.16), wy, 0.20),
                rotation=(0, math.pi / 2, 0),
                vertices=12,
            )
            assign_mat(wh, mats["SM_Steel"])
            parts.append(wh)

    hull = add_cube("TMP", 1.0, (0, -0.04, 1.12), scale=(1.12, 1.18, 0.78))
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)
    slope = add_cube("TMP", 1.0, (0, 0.52, 1.02), scale=(0.92, 0.28, 0.62))
    assign_mat(slope, mats["SM_White"])
    parts.append(slope)
    face = add_cube("TMP", 1.0, (0, 0.68, 1.08), scale=(0.72, 0.10, 0.48))
    assign_mat(face, mats["SM_Graphite"])
    parts.append(face)
    visor = add_cube("TMP", 1.0, (0, 0.74, 1.12), scale=(0.48, 0.06, 0.28))
    assign_mat(visor, mats["SM_Defense"])
    parts.append(visor)
    emblem = add_cube("TMP", 1.0, (0, 0.62, 1.48), scale=(0.16, 0.06, 0.14))
    assign_mat(emblem, mats["SM_Orange"])
    parts.append(emblem)

    for sx in (-1.08, 1.08):
        shoulder = add_cube("TMP", 1.0, (sx, 0.02, 1.22), scale=(0.58, 0.88, 0.72))
        assign_mat(shoulder, mats["SM_White"])
        parts.append(shoulder)
        port = add_cube("TMP", 1.0, (sx, 0.46, 1.22), scale=(0.28, 0.06, 0.32))
        assign_mat(port, mats["SM_Defense"])
        parts.append(port)
        haz = add_cube("TMP", 1.0, (sx, 0.22, 1.58), scale=(0.38, 0.08, 0.06))
        assign_mat(haz, mats["SM_Orange"])
        parts.append(haz)
        haz2 = add_cube("TMP", 1.0, (sx, 0.22, 1.48), scale=(0.38, 0.08, 0.06))
        assign_mat(haz2, mats["SM_Orange"])
        parts.append(haz2)

    turret = add_cube("TMP", 1.0, (0, -0.06, 1.72), scale=(0.36, 0.32, 0.18))
    assign_mat(turret, mats["SM_Graphite"])
    parts.append(turret)
    optic = add_uv_sphere("TMP", 0.055, (0, 0.12, 1.78), segments=10, rings=6)
    assign_mat(optic, mats["SM_Cyan"])
    parts.append(optic)

    rear = add_cube("TMP", 1.0, (0, -0.68, 1.05), scale=(0.88, 0.14, 0.58))
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
    Dust Stalker — Imagine creature sheet, RTS predator.
    Low quadruped, four orange eyes, serrated dorsal fins, wrapping bone plates,
    white forearm armor, three-toed talons, thick tail. Not a beetle/tick.
    Length ~2.8 m, shoulder ~1.15 m.
    """
    name = "SM_Unit_DustStalker"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere("TMP", 0.58, (0, 0.06, 0.50), scale=(1.05, 2.35, 0.70), segments=24, rings=12)
    assign_mat(body, mats["SM_Stalker"])
    parts.append(body)
    hump = add_uv_sphere("TMP", 0.34, (0, 0.18, 0.78), scale=(1.20, 1.40, 0.72), segments=16, rings=8)
    assign_mat(hump, mats["SM_Stalker"])
    parts.append(hump)

    # Wrapping bone plates (midsection + shoulders).
    for y, sx, sz in ((0.42, 0.72, 0.20), (0.05, 0.64, 0.18), (-0.38, 0.54, 0.15)):
        plate = add_cube("TMP", 1.0, (0, y, 0.78), scale=(sx, 0.30, sz))
        assign_mat(plate, mats["SM_White"])
        parts.append(plate)
    for sx in (-0.42, 0.42):
        wrap = add_cube("TMP", 1.0, (sx, 0.22, 0.62), scale=(0.16, 0.55, 0.28))
        assign_mat(wrap, mats["SM_White"])
        parts.append(wrap)
        glow = add_uv_sphere("TMP", 0.05, (sx * 1.15, 0.08, 0.55), segments=8, rings=6)
        assign_mat(glow, mats["SM_Orange"])
        parts.append(glow)

    head = add_uv_sphere("TMP", 0.30, (0, 1.32, 0.58), scale=(0.82, 1.28, 0.76), segments=20, rings=10)
    assign_mat(head, mats["SM_Stalker"])
    parts.append(head)
    snout = add_cube("TMP", 1.0, (0, 1.62, 0.48), scale=(0.26, 0.28, 0.14))
    assign_mat(snout, mats["SM_Black"])
    parts.append(snout)
    # Two pairs of orange eyes.
    for x, y, z in (
        (-0.14, 1.48, 0.70), (0.14, 1.48, 0.70),
        (-0.22, 1.40, 0.62), (0.22, 1.40, 0.62),
    ):
        eye = add_uv_sphere("TMP", 0.055, (x, y, z), segments=10, rings=6)
        assign_mat(eye, mats["SM_Orange"])
        parts.append(eye)

    for i, y in enumerate((0.95, 0.58, 0.22, -0.14, -0.50, -0.86, -1.18)):
        h = 0.58 - i * 0.05
        ridge = add_cone("TMP", 0.07, h, (0, y, 0.90 + h * 0.34))
        assign_mat(ridge, mats["SM_Black"])
        parts.append(ridge)

    tail = add_uv_sphere("TMP", 0.26, (0, -1.42, 0.40), scale=(0.62, 2.05, 0.52), segments=16, rings=8)
    assign_mat(tail, mats["SM_Stalker"])
    parts.append(tail)
    tip = add_cone("TMP", 0.10, 0.48, (0, -2.05, 0.36))
    assign_mat(tip, mats["SM_Black"])
    parts.append(tip)

    for sx, sy in ((-0.55, 0.58), (0.55, 0.58), (-0.50, -0.50), (0.50, -0.50)):
        upper = add_cube("TMP", 1.0, (sx, sy, 0.40), scale=(0.16, 0.16, 0.40))
        assign_mat(upper, mats["SM_Stalker"])
        parts.append(upper)
        bracer = add_cube("TMP", 1.0, (sx * 1.10, sy, 0.18), scale=(0.20, 0.20, 0.14))
        assign_mat(bracer, mats["SM_White"])
        parts.append(bracer)
        for cx, cy in ((-0.07, 0.08), (0.0, 0.12), (0.07, 0.08)):
            claw = add_cone("TMP", 0.032, 0.16, (sx + cx, sy + cy, 0.05))
            assign_mat(claw, mats["SM_Black"])
            parts.append(claw)

    obj = join_parts(parts, name)
    col = ensure_collection("13_Units_Stalker")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (8.0, 0.0, 0.0)
    return obj


def build_medic(mats: dict) -> bpy.types.Object:
    """
    Medic LO-MED-1 — Imagine hover capsule sheet.
    White ceramic top / black carbon belly, orange hazard stripes, cyan cross
    and visor, IV pole + bag, four cyan hover discs (no rotors).
    ~1.7 × 0.85 × 1.35 m. Not a biped, not Scout.
    """
    name = "SM_Unit_Medic"
    remove_if_exists(name)
    parts = []

    pad = add_cylinder("TMP", 0.28, 0.04, (0, 0.02, 0.03), vertices=16)
    assign_mat(pad, mats["SM_Graphite"])
    parts.append(pad)

    hull = add_uv_sphere(
        "TMP", 0.42, (0, 0.04, 0.72),
        scale=(1.02, 2.02, 0.82), segments=22, rings=12,
    )
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)
    belly = add_uv_sphere(
        "TMP", 0.38, (0, 0.04, 0.50),
        scale=(0.98, 1.92, 0.42), segments=20, rings=10,
    )
    assign_mat(belly, mats["SM_Black"])
    parts.append(belly)

    visor = add_cube("TMP", 1.0, (0, 0.82, 0.74), scale=(0.48, 0.04, 0.08))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    for sx, yaw in ((-0.30, math.radians(38)), (0.30, math.radians(-38))):
        stripe = add_cube("TMP", 1.0, (sx, 0.72, 0.52), scale=(0.10, 0.22, 0.07))
        stripe.rotation_euler = (math.radians(18), 0.0, yaw)
        apply_rot(stripe)
        assign_mat(stripe, mats["SM_Orange"])
        parts.append(stripe)

    cross_h = add_cube("TMP", 1.0, (0, 0.06, 1.08), scale=(0.46, 0.08, 0.05))
    assign_mat(cross_h, mats["SM_Cyan"])
    parts.append(cross_h)
    cross_v = add_cube("TMP", 1.0, (0, 0.06, 1.08), scale=(0.08, 0.46, 0.05))
    assign_mat(cross_v, mats["SM_Cyan"])
    parts.append(cross_v)

    for sx in (-0.44, 0.44):
        housing = add_cylinder(
            "TMP", 0.12, 0.06, (sx, -0.12, 0.70),
            rotation=(0, math.pi / 2, 0), vertices=14,
        )
        assign_mat(housing, mats["SM_Graphite"])
        parts.append(housing)
        mark = add_cube("TMP", 1.0, (sx * 1.08, -0.12, 0.70), scale=(0.03, 0.08, 0.03))
        assign_mat(mark, mats["SM_Orange"])
        parts.append(mark)
        mark2 = add_cube("TMP", 1.0, (sx * 1.08, -0.12, 0.70), scale=(0.03, 0.03, 0.08))
        assign_mat(mark2, mats["SM_Orange"])
        parts.append(mark2)

    pole = add_cylinder("TMP", 0.018, 0.58, (-0.22, -0.72, 1.18), vertices=8)
    assign_mat(pole, mats["SM_Steel"])
    parts.append(pole)
    bag = add_uv_sphere("TMP", 0.07, (-0.22, -0.72, 1.50), scale=(0.85, 0.70, 1.15), segments=10, rings=6)
    assign_mat(bag, mats["SM_Cyan"])
    parts.append(bag)
    tube = add_cylinder("TMP", 0.012, 0.28, (-0.18, -0.62, 1.22), vertices=6)
    tube.rotation_euler = (math.radians(35), 0.0, 0.0)
    apply_rot(tube)
    assign_mat(tube, mats["SM_Steel"])
    parts.append(tube)

    for sx, sy in ((-0.38, 0.52), (0.38, 0.52), (-0.38, -0.52), (0.38, -0.52)):
        disc = add_cylinder("TMP", 0.14, 0.04, (sx, sy, 0.20), vertices=14)
        assign_mat(disc, mats["SM_Black"])
        parts.append(disc)
        glow = add_cylinder("TMP", 0.11, 0.02, (sx, sy, 0.14), vertices=12)
        assign_mat(glow, mats["SM_Cyan"])
        parts.append(glow)

    obj = join_parts(parts, name)
    col = ensure_collection("14_Units_Medic")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (12.0, 0.0, 0.0)
    return obj


def build_harvester(mats: dict) -> bpy.types.Object:
    """
    Harvester LO-HAR-1 — Imagine tracked scoop hopper sheet.
    White cab, cyan visor, orange front blade, rear hopper with orange lip,
    small side excavator arm. Continuous treads. Not a Terraformer dozer.
    ~1.55 m tall.
    """
    name = "SM_Unit_HarvesterBot"
    remove_if_exists(name)
    parts = []

    chassis = add_cube("TMP", 1.0, (0, 0.02, 0.42), scale=(1.08, 1.32, 0.36))
    assign_mat(chassis, mats["SM_Black"])
    parts.append(chassis)
    cab = add_cube("TMP", 1.0, (0, 0.22, 0.98), scale=(0.82, 0.72, 0.62))
    assign_mat(cab, mats["SM_White"])
    parts.append(cab)
    visor = add_cube("TMP", 1.0, (0, 0.60, 1.08), scale=(0.58, 0.05, 0.16))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    ant = add_cylinder("TMP", 0.016, 0.22, (0.22, 0.18, 1.38), vertices=6)
    assign_mat(ant, mats["SM_Steel"])
    parts.append(ant)
    node = add_uv_sphere("TMP", 0.04, (-0.18, 0.12, 1.32), segments=8, rings=6)
    assign_mat(node, mats["SM_Cyan"])
    parts.append(node)

    hopper = add_cube("TMP", 1.0, (0, -0.62, 1.05), scale=(0.88, 0.52, 0.62))
    assign_mat(hopper, mats["SM_Graphite"])
    parts.append(hopper)
    hop_lip = add_cube("TMP", 1.0, (0, -0.62, 1.38), scale=(0.82, 0.46, 0.08))
    assign_mat(hop_lip, mats["SM_Orange"])
    parts.append(hop_lip)
    ore = add_cube("TMP", 1.0, (0, -0.62, 1.18), scale=(0.64, 0.34, 0.18))
    assign_mat(ore, mats["SM_Steel"])
    parts.append(ore)

    # Orange front scoop/blade — not a Terraformer V-plow.
    blade = add_cube("TMP", 1.0, (0, 0.98, 0.48), scale=(1.18, 0.12, 0.58))
    blade.rotation_euler = (math.radians(16), 0.0, 0.0)
    apply_rot(blade)
    assign_mat(blade, mats["SM_Orange"])
    parts.append(blade)
    for sx in (-0.28, 0.28):
        brace = add_cube("TMP", 1.0, (sx, 0.62, 0.62), scale=(0.08, 0.38, 0.08))
        assign_mat(brace, mats["SM_Graphite"])
        parts.append(brace)

    # Small side excavator (vehicle left / -X).
    boom = add_cube("TMP", 1.0, (-0.68, 0.08, 1.02), scale=(0.10, 0.42, 0.10))
    assign_mat(boom, mats["SM_Black"])
    parts.append(boom)
    fore = add_cube("TMP", 1.0, (-0.68, 0.42, 0.72), scale=(0.08, 0.10, 0.42))
    assign_mat(fore, mats["SM_Black"])
    parts.append(fore)
    bucket = add_cube("TMP", 1.0, (-0.68, 0.58, 0.42), scale=(0.16, 0.22, 0.12))
    assign_mat(bucket, mats["SM_Steel"])
    parts.append(bucket)
    lip = add_cube("TMP", 1.0, (-0.68, 0.70, 0.36), scale=(0.14, 0.04, 0.10))
    assign_mat(lip, mats["SM_Orange"])
    parts.append(lip)

    for sx in (-0.62, 0.62):
        track = add_cube("TMP", 1.0, (sx, 0.02, 0.18), scale=(0.28, 1.42, 0.28))
        assign_mat(track, mats["SM_Black"])
        parts.append(track)
        for sy in (-0.52, -0.26, 0.0, 0.26, 0.52):
            hub = add_cylinder(
                "TMP", 0.09, 0.10, (sx, sy, 0.18),
                rotation=(0, math.pi / 2, 0), vertices=10,
            )
            assign_mat(hub, mats["SM_Steel"])
            parts.append(hub)

    obj = join_parts(parts, name)
    col = ensure_collection("15_Units_Harvester")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (16.0, 0.0, 0.0)
    return obj


def build_surveyor(mats: dict) -> bpy.types.Object:
    """
    Surveyor LO-SRV-1 — Imagine tripod mast sheet.
    White cylinder on three black carbon legs with pad feet + white thigh armor,
    cyan eye, white dish on mast, orange beacon. ~2.55 m tall. Not Scout.
    """
    name = "SM_Unit_SurveyorBot"
    remove_if_exists(name)
    parts = []

    body = add_cylinder("TMP", 0.32, 0.62, (0, 0, 0.95), vertices=18)
    assign_mat(body, mats["SM_White"])
    parts.append(body)
    band = add_cylinder("TMP", 0.36, 0.10, (0, 0, 0.92), vertices=18)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)
    lens = add_cube("TMP", 1.0, (0, 0.36, 0.92), scale=(0.16, 0.05, 0.14))
    assign_mat(lens, mats["SM_Cyan"])
    parts.append(lens)
    tag = add_cube("TMP", 1.0, (0.33, 0.0, 0.78), scale=(0.03, 0.10, 0.06))
    assign_mat(tag, mats["SM_Orange"])
    parts.append(tag)

    mast = add_cylinder("TMP", 0.040, 1.22, (0, 0, 1.86), vertices=10)
    assign_mat(mast, mats["SM_Steel"])
    parts.append(mast)
    beacon_stem = add_cylinder("TMP", 0.016, 0.22, (0.16, 0.0, 1.38), vertices=6)
    assign_mat(beacon_stem, mats["SM_Black"])
    parts.append(beacon_stem)
    beacon = add_uv_sphere("TMP", 0.05, (0.16, 0.0, 1.52), segments=10, rings=6)
    assign_mat(beacon, mats["SM_Orange"])
    parts.append(beacon)

    dish = add_uv_sphere("TMP", 0.42, (0, 0, 2.48), scale=(1.0, 1.0, 0.18), segments=20, rings=8)
    assign_mat(dish, mats["SM_White"])
    parts.append(dish)
    cluster = add_cylinder("TMP", 0.08, 0.10, (0, 0, 2.56), vertices=12)
    assign_mat(cluster, mats["SM_Graphite"])
    parts.append(cluster)
    glow = add_cylinder("TMP", 0.07, 0.04, (0, 0, 2.62), vertices=12)
    assign_mat(glow, mats["SM_Cyan"])
    parts.append(glow)

    for ang_deg in (20.0, 140.0, 260.0):
        rad = math.radians(ang_deg)
        fx = math.cos(rad) * 0.86
        fy = math.sin(rad) * 0.86
        thigh = add_cube("TMP", 1.0, (fx * 0.38, fy * 0.38, 0.64), scale=(0.70, 0.09, 0.09))
        thigh.rotation_euler = (0.0, math.radians(-40), rad)
        apply_rot(thigh)
        assign_mat(thigh, mats["SM_Black"])
        parts.append(thigh)
        plate = add_cube("TMP", 1.0, (fx * 0.42, fy * 0.42, 0.70), scale=(0.36, 0.14, 0.06))
        plate.rotation_euler = (0.0, math.radians(-40), rad)
        apply_rot(plate)
        assign_mat(plate, mats["SM_White"])
        parts.append(plate)
        shin = add_cube("TMP", 1.0, (fx * 0.80, fy * 0.80, 0.24), scale=(0.48, 0.08, 0.08))
        shin.rotation_euler = (0.0, math.radians(-55), rad)
        apply_rot(shin)
        assign_mat(shin, mats["SM_Black"])
        parts.append(shin)
        foot = add_cube("TMP", 1.0, (fx, fy, 0.04), scale=(0.22, 0.22, 0.06))
        assign_mat(foot, mats["SM_Black"])
        parts.append(foot)

    obj = join_parts(parts, name)
    col = ensure_collection("16_Units_Surveyor")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (20.0, 0.0, 0.0)
    return obj


def build_terraformer(mats: dict) -> bpy.types.Object:
    """
    Terraformer LO-TRF-1 — Imagine tracked dozer sheet.
    White ceramic cab, orange front blade, orange rear rake/tiller, cyan visor,
    orange beacons, continuous treads. Not a front-scoop hopper.
    RTS size ~2.5 m class (do not shrink to a toy).
    """
    name = "SM_Unit_TerraformerBot"
    remove_if_exists(name)
    parts = []

    chassis = add_cube("TMP", 1.0, (0, 0.02, 0.72), scale=(1.18, 1.68, 0.48))
    assign_mat(chassis, mats["SM_White"])
    parts.append(chassis)
    belly = add_cube("TMP", 1.0, (0, 0.02, 0.40), scale=(1.28, 1.78, 0.16))
    assign_mat(belly, mats["SM_Black"])
    parts.append(belly)
    cab = add_cube("TMP", 1.0, (0, 0.38, 1.28), scale=(0.78, 0.58, 0.52))
    assign_mat(cab, mats["SM_White"])
    parts.append(cab)
    visor = add_cube("TMP", 1.0, (0, 0.68, 1.38), scale=(0.62, 0.05, 0.16))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    lamp = add_uv_sphere("TMP", 0.055, (0.22, 0.68, 1.18), segments=8, rings=6)
    assign_mat(lamp, mats["SM_Cyan"])
    parts.append(lamp)
    beacon = add_uv_sphere("TMP", 0.06, (0, 0.28, 1.62), segments=10, rings=6)
    assign_mat(beacon, mats["SM_Orange"])
    parts.append(beacon)
    ant = add_cylinder("TMP", 0.016, 0.42, (0.22, 0.22, 1.72), vertices=6)
    assign_mat(ant, mats["SM_Steel"])
    parts.append(ant)

    tanks = add_cube("TMP", 1.0, (0, -0.48, 1.22), scale=(0.92, 0.72, 0.42))
    assign_mat(tanks, mats["SM_White"])
    parts.append(tanks)
    for sx in (-0.28, 0.28):
        cap = add_uv_sphere("TMP", 0.055, (sx, -0.48, 1.48), segments=8, rings=6)
        assign_mat(cap, mats["SM_Orange"])
        parts.append(cap)

    # Orange dozer blade (full orange, not a steel V-plow with a lip).
    blade = add_cube("TMP", 1.0, (0, 1.22, 0.58), scale=(1.55, 0.12, 0.78))
    blade.rotation_euler = (math.radians(8), 0.0, 0.0)
    apply_rot(blade)
    assign_mat(blade, mats["SM_Orange"])
    parts.append(blade)
    for sx in (-0.38, 0.38):
        arm = add_cube("TMP", 1.0, (sx, 0.82, 0.62), scale=(0.10, 0.48, 0.10))
        assign_mat(arm, mats["SM_Black"])
        parts.append(arm)

    # Orange rear rake/tiller — wider than hull, many tines.
    rake = add_cube("TMP", 1.0, (0, -1.12, 0.42), scale=(2.05, 0.10, 0.10))
    assign_mat(rake, mats["SM_Orange"])
    parts.append(rake)
    hang = add_cube("TMP", 1.0, (0, -0.92, 0.72), scale=(0.12, 0.42, 0.10))
    assign_mat(hang, mats["SM_Black"])
    parts.append(hang)
    for i in range(11):
        tx = -0.95 + i * 0.19
        tine = add_cone("TMP", 0.028, 0.28, (tx, -1.18, 0.22), vertices=8)
        tine.rotation_euler = (math.radians(165), 0.0, 0.0)
        apply_rot(tine)
        assign_mat(tine, mats["SM_Orange"])
        parts.append(tine)

    for sx in (-0.68, 0.68):
        track = add_cube("TMP", 1.0, (sx, 0.02, 0.20), scale=(0.32, 1.52, 0.32))
        assign_mat(track, mats["SM_Black"])
        parts.append(track)
        for sy in (-0.55, -0.18, 0.18, 0.55):
            hub = add_cylinder(
                "TMP", 0.12, 0.11, (sx, sy, 0.20),
                rotation=(0, math.pi / 2, 0), vertices=10,
            )
            assign_mat(hub, mats["SM_Steel"])
            parts.append(hub)

    obj = join_parts(parts, name)
    col = ensure_collection("17_Units_Terraformer")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (24.0, 0.0, 0.0)
    return obj


def build_courier(mats: dict) -> bpy.types.Object:
    """
    Courier LO-COU-1 — Imagine six-wheel hauler sheet.
    White cab + white crate with orange corners, cyan visor bar, orange roof
    beacon, whip antenna, black grille. No drill, no vials. ~2.0 × 1.45 m.
    """
    name = "SM_Unit_CourierBot"
    remove_if_exists(name)
    parts = []

    chassis = add_cube("TMP", 1.0, (0, 0.02, 0.48), scale=(0.78, 1.58, 0.28))
    assign_mat(chassis, mats["SM_White"])
    parts.append(chassis)
    belly = add_cube("TMP", 1.0, (0, 0.02, 0.26), scale=(0.70, 1.48, 0.16))
    assign_mat(belly, mats["SM_Black"])
    parts.append(belly)
    bumper = add_cube("TMP", 1.0, (0, 0.82, 0.22), scale=(0.72, 0.10, 0.12))
    assign_mat(bumper, mats["SM_Black"])
    parts.append(bumper)

    crate = add_cube("TMP", 1.0, (0, -0.28, 1.00), scale=(0.80, 0.92, 0.78))
    assign_mat(crate, mats["SM_White"])
    parts.append(crate)
    for sx, sy, sz in (
        (-0.38, -0.70, 0.64), (0.38, -0.70, 0.64),
        (-0.38, 0.14, 0.64), (0.38, 0.14, 0.64),
        (-0.38, -0.70, 1.36), (0.38, -0.70, 1.36),
        (-0.38, 0.14, 1.36), (0.38, 0.14, 1.36),
    ):
        corner = add_cube("TMP", 1.0, (sx, sy, sz), scale=(0.10, 0.10, 0.10))
        assign_mat(corner, mats["SM_Orange"])
        parts.append(corner)

    cab = add_cube("TMP", 1.0, (0, 0.68, 0.82), scale=(0.64, 0.42, 0.44))
    assign_mat(cab, mats["SM_White"])
    parts.append(cab)
    grille = add_cube("TMP", 1.0, (0, 0.90, 0.62), scale=(0.22, 0.04, 0.18))
    assign_mat(grille, mats["SM_Black"])
    parts.append(grille)
    visor = add_cube("TMP", 1.0, (0, 0.90, 0.92), scale=(0.50, 0.04, 0.10))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    for sx in (-0.22, 0.22):
        lamp = add_uv_sphere("TMP", 0.04, (sx, 0.90, 0.52), segments=8, rings=6)
        assign_mat(lamp, mats["SM_Cyan"])
        parts.append(lamp)
    ant = add_cylinder("TMP", 0.016, 0.78, (0.22, 0.58, 1.38), vertices=8)
    assign_mat(ant, mats["SM_Steel"])
    parts.append(ant)
    tip = add_uv_sphere("TMP", 0.032, (0.22, 0.58, 1.78), segments=8, rings=6)
    assign_mat(tip, mats["SM_Orange"])
    parts.append(tip)
    beacon = add_uv_sphere("TMP", 0.055, (0, 0.58, 1.12), segments=10, rings=6)
    assign_mat(beacon, mats["SM_Orange"])
    parts.append(beacon)

    for sx in (-0.46, 0.46):
        for sy in (-0.58, 0.02, 0.62):
            wheel = add_cylinder(
                "TMP", 0.18, 0.11, (sx, sy, 0.18),
                rotation=(0, math.pi / 2, 0), vertices=12,
            )
            assign_mat(wheel, mats["SM_Black"])
            parts.append(wheel)
            hub = add_cylinder(
                "TMP", 0.07, 0.13, (sx, sy, 0.18),
                rotation=(0, math.pi / 2, 0), vertices=10,
            )
            assign_mat(hub, mats["SM_Steel"])
            parts.append(hub)
            fender = add_cube("TMP", 1.0, (sx, sy, 0.38), scale=(0.16, 0.22, 0.06))
            assign_mat(fender, mats["SM_Black"])
            parts.append(fender)

    obj = join_parts(parts, name)
    col = ensure_collection("18_Units_Courier")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (28.0, 0.0, 0.0)
    return obj


def build_mite(mats: dict) -> bpy.types.Object:
    """
    Regolith mite — Imagine farm-scavenger pillbug sheet.
    Dust-brown carapace, four overlapping graphite top plates, six pointed
    graphite legs, central cyan eye + two orange nubs, downward mandibles,
    side access panel. Longer than wide. ~0.95 m RTS. Not a Tick crab.
    """
    name = "SM_Unit_RegolithMite"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere(
        "TMP", 0.19, (0, 0.02, 0.23),
        scale=(1.22, 2.00, 1.02), segments=20, rings=12,
    )
    assign_mat(body, mats["SM_DustBrown"])
    parts.append(body)
    for y, z, sx, sy, sz in (
        (0.22, 0.40, 0.38, 0.16, 0.07),
        (0.06, 0.42, 0.42, 0.18, 0.08),
        (-0.10, 0.40, 0.38, 0.16, 0.07),
        (-0.24, 0.36, 0.30, 0.14, 0.06),
    ):
        plate = add_cube("TMP", 1.0, (0, y, z), scale=(sx, sy, sz))
        assign_mat(plate, mats["SM_Graphite"])
        parts.append(plate)
    panel = add_cube("TMP", 1.0, (0.22, 0.04, 0.21), scale=(0.02, 0.12, 0.08))
    assign_mat(panel, mats["SM_Steel"])
    parts.append(panel)
    for gy in (-0.03, 0.04, 0.11):
        grate = add_cube("TMP", 1.0, (0.235, gy, 0.21), scale=(0.01, 0.010, 0.06))
        assign_mat(grate, mats["SM_Graphite"])
        parts.append(grate)
    for y, z in ((0.14, 0.28), (0.0, 0.26), (-0.14, 0.24)):
        for sx in (-0.18, 0.18):
            bolt = add_uv_sphere("TMP", 0.014, (sx, y, z), segments=6, rings=4)
            assign_mat(bolt, mats["SM_Steel"])
            parts.append(bolt)

    eye = add_uv_sphere("TMP", 0.052, (0, 0.38, 0.27), segments=12, rings=8)
    assign_mat(eye, mats["SM_Cyan"])
    parts.append(eye)
    for x in (-0.10, 0.10):
        nub = add_uv_sphere("TMP", 0.030, (x, 0.34, 0.31), segments=8, rings=6)
        assign_mat(nub, mats["SM_Orange"])
        parts.append(nub)
    for x, yaw in ((-0.05, 0.22), (0.05, -0.22)):
        mandible = add_cone("TMP", 0.038, 0.12, (x, 0.42, 0.13), vertices=8)
        mandible.rotation_euler = (math.pi * 0.72, 0.0, yaw)
        apply_rot(mandible)
        assign_mat(mandible, mats["SM_Graphite"])
        parts.append(mandible)

    for y, z, r, d in ((-0.38, 0.18, 0.048, 0.09), (-0.45, 0.11, 0.032, 0.08)):
        tail = add_cone("TMP", r, d, (0, y, z), vertices=8)
        tail.rotation_euler = (math.pi * 0.72, 0.0, math.pi)
        apply_rot(tail)
        assign_mat(tail, mats["SM_Graphite"])
        parts.append(tail)

    for sx, sy in (
        (-1.0, 0.18), (1.0, 0.18),
        (-1.0, 0.00), (1.0, 0.00),
        (-1.0, -0.18), (1.0, -0.18),
    ):
        hip = (sx * 0.14, sy, 0.16)
        knee = (sx * 0.24, sy, 0.10)
        tip = (sx * 0.32, sy, 0.02)
        thigh = add_shaft("TMP", 0.020, hip, knee, vertices=8)
        assign_mat(thigh, mats["SM_Graphite"])
        parts.append(thigh)
        shin = add_shaft("TMP", 0.014, knee, tip, vertices=8)
        assign_mat(shin, mats["SM_Graphite"])
        parts.append(shin)
        foot = add_cone("TMP", 0.018, 0.07, (sx * 0.32, sy, 0.03), vertices=6)
        assign_mat(foot, mats["SM_Graphite"])
        parts.append(foot)

    obj = join_parts(parts, name)
    col = ensure_collection("19_Fauna_Mite")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (32.0, 0.0, 0.0)
    return obj


def build_leech(mats: dict) -> bpy.types.Object:
    """
    Watt leech — Imagine white ray/beetle sheet (not a segmented millipede).
    White carapace, cyan dorsal groove, two orange front nubs, white mandibles,
    four rearward fin-flippers, six black circular discs per side. Low profile.
    Keep existing ~1.5 m RTS length.
    """
    name = "SM_Unit_WattLeech"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere(
        "TMP", 0.32, (0, 0.0, 0.16),
        scale=(1.42, 2.12, 0.46), segments=24, rings=12,
    )
    assign_mat(body, mats["SM_White"])
    parts.append(body)
    groove = add_cube("TMP", 1.0, (0, 0.02, 0.30), scale=(0.11, 1.18, 0.055))
    assign_mat(groove, mats["SM_Cyan"])
    parts.append(groove)
    core = add_cube("TMP", 1.0, (0, 0.02, 0.27), scale=(0.05, 1.08, 0.03))
    assign_mat(core, mats["SM_Cyan"])
    parts.append(core)
    for sx in (-0.15, 0.15):
        lobe = add_uv_sphere(
            "TMP", 0.14, (sx, 0.52, 0.15),
            scale=(1.12, 1.20, 0.62), segments=14, rings=8,
        )
        assign_mat(lobe, mats["SM_White"])
        parts.append(lobe)
    tail = add_uv_sphere(
        "TMP", 0.13, (0, -0.52, 0.13),
        scale=(1.15, 1.28, 0.55), segments=14, rings=8,
    )
    assign_mat(tail, mats["SM_White"])
    parts.append(tail)

    for x, yaw in ((-0.08, 0.18), (0.08, -0.18)):
        jaw = add_cone("TMP", 0.038, 0.18, (x, 0.76, 0.10), vertices=8)
        jaw.rotation_euler = (math.pi * 0.62, 0.0, yaw)
        apply_rot(jaw)
        assign_mat(jaw, mats["SM_White"])
        parts.append(jaw)
    for x in (-0.13, 0.13):
        nub = add_uv_sphere("TMP", 0.036, (x, 0.58, 0.22), segments=10, rings=6)
        assign_mat(nub, mats["SM_Orange"])
        parts.append(nub)

    for sx, sy, yaw in (
        (-0.50, 0.26, math.radians(16)),
        (0.50, 0.26, math.radians(-16)),
        (-0.44, -0.32, math.radians(-10)),
        (0.44, -0.32, math.radians(10)),
    ):
        fin = add_cone("TMP", 0.11, 0.40, (sx, sy, 0.10), vertices=8)
        fin.scale = (0.28, 1.0, 0.14)
        bpy.ops.object.select_all(action="DESELECT")
        fin.select_set(True)
        bpy.context.view_layer.objects.active = fin
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        fin.rotation_euler = (math.pi * 0.5, 0.0, math.pi + yaw)
        apply_rot(fin)
        assign_mat(fin, mats["SM_White"])
        parts.append(fin)

    for sx in (-0.40, 0.40):
        for y in (0.22, 0.12, 0.02, -0.08, -0.18, -0.28):
            disc = add_cylinder(
                "TMP", 0.055, 0.028, (sx, y, 0.10),
                rotation=(0, math.pi / 2, 0), vertices=12,
            )
            assign_mat(disc, mats["SM_Black"])
            parts.append(disc)

    obj = join_parts(parts, name)
    col = ensure_collection("20_Fauna_Leech")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (36.0, 0.0, 0.0)
    return obj


def build_geologist(mats: dict) -> bpy.types.Object:
    """
    Geologist LO-GEO-1 — Imagine six-wheel drill rover sheet.
    White ceramic, two vertical orange nose stripes, vertical drill with orange
    housing, sample vial rack (cyan+orange caps), sensor mast with cyan lens.
    Not a Courier. ~2.1 × 1.35 m.
    """
    name = "SM_Unit_GeologistBot"
    remove_if_exists(name)
    parts = []

    chassis = add_cube("TMP", 1.0, (0, 0.04, 0.44), scale=(0.72, 1.38, 0.28))
    assign_mat(chassis, mats["SM_White"])
    parts.append(chassis)
    belly = add_cube("TMP", 1.0, (0, 0.04, 0.24), scale=(0.62, 1.22, 0.14))
    assign_mat(belly, mats["SM_Black"])
    parts.append(belly)
    nose = add_uv_sphere("TMP", 0.22, (0, 0.68, 0.48), scale=(1.35, 0.85, 0.72), segments=14, rings=8)
    assign_mat(nose, mats["SM_White"])
    parts.append(nose)
    for sx in (-0.12, 0.12):
        stripe = add_cube("TMP", 1.0, (sx, 0.78, 0.52), scale=(0.05, 0.04, 0.22))
        assign_mat(stripe, mats["SM_Orange"])
        parts.append(stripe)
        lamp = add_cube("TMP", 1.0, (sx * 1.4, 0.78, 0.40), scale=(0.06, 0.03, 0.04))
        assign_mat(lamp, mats["SM_Cyan"])
        parts.append(lamp)

    rack = add_cube("TMP", 1.0, (0, -0.52, 0.62), scale=(0.52, 0.40, 0.28))
    assign_mat(rack, mats["SM_Graphite"])
    parts.append(rack)
    vials = (
        (-0.14, -0.42, mats["SM_Cyan"]), (0.0, -0.42, mats["SM_Orange"]),
        (0.14, -0.42, mats["SM_Cyan"]), (-0.14, -0.62, mats["SM_Orange"]),
        (0.0, -0.62, mats["SM_Cyan"]), (0.14, -0.62, mats["SM_Orange"]),
    )
    for sx, sy, cap in vials:
        vial = add_cylinder("TMP", 0.032, 0.16, (sx, sy, 0.86), vertices=8)
        assign_mat(vial, mats["SM_Steel"])
        parts.append(vial)
        lid = add_cylinder("TMP", 0.034, 0.04, (sx, sy, 0.96), vertices=8)
        assign_mat(lid, cap)
        parts.append(lid)

    mast = add_cylinder("TMP", 0.04, 0.58, (0, 0.18, 0.92), vertices=10)
    assign_mat(mast, mats["SM_White"])
    parts.append(mast)
    head = add_cube("TMP", 1.0, (0, 0.18, 1.26), scale=(0.22, 0.16, 0.14))
    assign_mat(head, mats["SM_Graphite"])
    parts.append(head)
    lens = add_cylinder(
        "TMP", 0.055, 0.05, (0, 0.28, 1.26),
        rotation=(math.pi / 2, 0, 0), vertices=12,
    )
    assign_mat(lens, mats["SM_Cyan"])
    parts.append(lens)
    bar = add_cube("TMP", 1.0, (0, 0.28, 1.18), scale=(0.16, 0.03, 0.04))
    assign_mat(bar, mats["SM_Cyan"])
    parts.append(bar)

    # Vertical core-drill, orange housing, bit toward ground.
    arm = add_cube("TMP", 1.0, (0, 0.72, 0.92), scale=(0.10, 0.12, 0.36))
    assign_mat(arm, mats["SM_Graphite"])
    parts.append(arm)
    housing = add_cylinder("TMP", 0.11, 0.16, (0, 0.72, 0.68), vertices=12)
    assign_mat(housing, mats["SM_Orange"])
    parts.append(housing)
    bit = add_cylinder("TMP", 0.042, 0.42, (0, 0.72, 0.38), vertices=10)
    assign_mat(bit, mats["SM_Steel"])
    parts.append(bit)
    tip = add_cone("TMP", 0.05, 0.14, (0, 0.72, 0.12), vertices=8)
    assign_mat(tip, mats["SM_Steel"])
    parts.append(tip)

    for sx in (-0.44, 0.44):
        for sy in (-0.58, 0.04, 0.66):
            wheel = add_cylinder(
                "TMP", 0.16, 0.10, (sx, sy, 0.16),
                rotation=(0, math.pi / 2, 0), vertices=14,
            )
            assign_mat(wheel, mats["SM_Black"])
            parts.append(wheel)
            hub = add_cylinder(
                "TMP", 0.06, 0.12, (sx, sy, 0.16),
                rotation=(0, math.pi / 2, 0), vertices=10,
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
    """
    Sentinel LO-SEN-1 — Imagine tracked turret sheet.
    Continuous tank treads (not stub pads). White ceramic hull, orange V chevron
    on top, cyan visor, twin-barrel turret with cyan tips, black carbon tracks.
    No red viewport, no huge shoulder pods, not Defense Guardian. ~1.55 m tall.
    """
    name = "SM_Unit_SentinelMech"
    remove_if_exists(name)
    parts = []

    hull = add_cube("TMP", 1.0, (0, 0.02, 0.72), scale=(1.12, 1.28, 0.48))
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)
    skirt = add_cube("TMP", 1.0, (0, 0.02, 0.38), scale=(1.22, 1.38, 0.18))
    assign_mat(skirt, mats["SM_Black"])
    parts.append(skirt)
    visor = add_cube("TMP", 1.0, (0, 0.66, 0.78), scale=(0.72, 0.05, 0.08))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    for side, yaw in ((-1.0, math.radians(32)), (1.0, math.radians(-32))):
        arm = add_cube("TMP", 1.0, (side * 0.16, 0.18, 1.00), scale=(0.42, 0.08, 0.05))
        arm.rotation_euler = (0.0, 0.0, yaw)
        apply_rot(arm)
        assign_mat(arm, mats["SM_Orange"])
        parts.append(arm)

    neck = add_cylinder("TMP", 0.16, 0.18, (0, 0.04, 1.08), vertices=12)
    assign_mat(neck, mats["SM_Black"])
    parts.append(neck)
    turret = add_cube("TMP", 1.0, (0, 0.08, 1.28), scale=(0.48, 0.38, 0.24))
    assign_mat(turret, mats["SM_White"])
    parts.append(turret)
    for sx in (-0.12, 0.12):
        barrel = add_cylinder(
            "TMP", 0.055, 0.72, (sx, 0.52, 1.28),
            rotation=(math.pi / 2, 0, 0), vertices=12,
        )
        assign_mat(barrel, mats["SM_Graphite"])
        parts.append(barrel)
        tip = add_uv_sphere("TMP", 0.048, (sx, 0.90, 1.28), segments=10, rings=6)
        assign_mat(tip, mats["SM_Cyan"])
        parts.append(tip)

    for sx in (-0.62, 0.62):
        track = add_cube("TMP", 1.0, (sx, 0.02, 0.18), scale=(0.30, 1.42, 0.28))
        assign_mat(track, mats["SM_Black"])
        parts.append(track)
        for sy in (-0.48, -0.16, 0.16, 0.48):
            hub = add_cylinder(
                "TMP", 0.10, 0.10, (sx, sy, 0.18),
                rotation=(0, math.pi / 2, 0), vertices=10,
            )
            assign_mat(hub, mats["SM_Steel"])
            parts.append(hub)

    obj = join_parts(parts, name)
    col = ensure_collection("22_Units_Sentinel")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (44.0, 0.0, 0.0)
    return obj


def build_wisp(mats: dict) -> bpy.types.Object:
    """
    Ice wisp — Imagine seven-point ice-star sheet.
    Translucent white-cyan spikes, hexagonal hub with cyan core, black ridges
    with orange nubs, hover glow ring. Not a Scout. No propellers.
    ~1.15 m hover, ~1.6 m span (RTS-readable).
    """
    name = "SM_Unit_IceWisp"
    remove_if_exists(name)
    parts = []

    pad = add_cylinder("TMP", 0.28, 0.03, (0, 0, 0.03), vertices=16)
    assign_mat(pad, mats["SM_Cyan"])
    parts.append(pad)
    hub = add_cylinder("TMP", 0.22, 0.16, (0, 0, 1.00), vertices=6)
    assign_mat(hub, mats["SM_Black"])
    parts.append(hub)
    core = add_uv_sphere("TMP", 0.12, (0, 0, 1.00), segments=14, rings=8)
    assign_mat(core, mats["SM_Cyan"])
    parts.append(core)
    for i in range(7):
        ang = i * (2.0 * math.pi / 7.0)
        ax = math.cos(ang) * 0.52
        ay = math.sin(ang) * 0.52
        shard = add_cone("TMP", 0.07, 0.62, (ax, ay, 1.00), vertices=8)
        shard.rotation_euler = (math.pi * 0.5, 0.0, ang)
        apply_rot(shard)
        assign_mat(shard, mats["SM_Ice"])
        parts.append(shard)
        ridge = add_cube("TMP", 1.0, (ax * 0.72, ay * 0.72, 1.00), scale=(0.04, 0.36, 0.04))
        ridge.rotation_euler = (0.0, 0.0, ang)
        apply_rot(ridge)
        assign_mat(ridge, mats["SM_Black"])
        parts.append(ridge)
        for t in (0.42, 0.62, 0.80):
            nx = math.cos(ang) * t * 0.72
            ny = math.sin(ang) * t * 0.72
            nub = add_uv_sphere("TMP", 0.028, (nx, ny, 1.04), segments=6, rings=4)
            assign_mat(nub, mats["SM_Orange"])
            parts.append(nub)

    obj = join_parts(parts, name)
    col = ensure_collection("23_Fauna_Wisp")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (48.0, 0.0, 0.0)
    return obj


def build_tick(mats: dict) -> bpy.types.Object:
    """
    Rock tick — Imagine wide crab sheet.
    Graphite shield carapace, dorsal fin spike, six black legs, orange pincer
    tips, cyan eyes. Wider than long. ~1.15 m wide, ~0.55 m tall.
    """
    name = "SM_Unit_RockTick"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere("TMP", 0.24, (0, 0, 0.22), scale=(2.40, 1.05, 0.78), segments=16, rings=8)
    assign_mat(body, mats["SM_Tick"])
    parts.append(body)
    shield = add_cube("TMP", 1.0, (0, -0.02, 0.38), scale=(0.72, 0.42, 0.10))
    assign_mat(shield, mats["SM_Graphite"])
    parts.append(shield)
    for y, sx in ((0.10, 0.68), (-0.08, 0.62), (-0.24, 0.52)):
        tile = add_cube("TMP", 1.0, (0, y, 0.36), scale=(sx, 0.14, 0.06))
        assign_mat(tile, mats["SM_Graphite"])
        parts.append(tile)
    spike = add_cone("TMP", 0.08, 0.28, (0, -0.04, 0.56), vertices=8)
    assign_mat(spike, mats["SM_Graphite"])
    parts.append(spike)
    for x in (-0.18, 0.18):
        arm = add_cube("TMP", 1.0, (x, 0.38, 0.20), scale=(0.08, 0.22, 0.08))
        assign_mat(arm, mats["SM_Black"])
        parts.append(arm)
        claw = add_cone("TMP", 0.04, 0.14, (x, 0.58, 0.16), vertices=8)
        assign_mat(claw, mats["SM_Orange"])
        parts.append(claw)
    for x in (-0.10, 0.10):
        eye = add_uv_sphere("TMP", 0.04, (x, 0.22, 0.32), segments=8, rings=6)
        assign_mat(eye, mats["SM_Cyan"])
        parts.append(eye)
    for sx, sy in (
        (-0.62, 0.22), (0.62, 0.22),
        (-0.70, -0.04), (0.70, -0.04),
        (-0.52, -0.32), (0.52, -0.32),
    ):
        thigh = add_cube("TMP", 1.0, (sx * 0.55, sy, 0.20), scale=(0.34, 0.06, 0.06))
        assign_mat(thigh, mats["SM_Black"])
        parts.append(thigh)
        foot = add_cone("TMP", 0.035, 0.10, (sx, sy, 0.05), vertices=6)
        assign_mat(foot, mats["SM_Black"])
        parts.append(foot)

    obj = join_parts(parts, name)
    col = ensure_collection("24_Fauna_Tick")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (52.0, 0.0, 0.0)
    return obj


def build_creeper(mats: dict) -> bpy.types.Object:
    """
    Soil creeper — Imagine armored isopod/millipede sheet.
    Overlapping graphite plates, one olive-brown segment, many black legs,
    orange head nubs, cyan eyes, orange tail cerci.
    Keep ~2 m long for RTS even if the sheet scale bar is tiny.
    """
    name = "SM_Unit_SoilCreeper"
    remove_if_exists(name)
    parts = []

    ys = (0.88, 0.62, 0.36, 0.10, -0.16, -0.42, -0.68, -0.92)
    for i, y in enumerate(ys):
        r = 0.20 - i * 0.008
        seg = add_uv_sphere("TMP", r, (0, y, 0.24), scale=(1.05, 0.85, 0.78), segments=12, rings=8)
        assign_mat(seg, mats["SM_Creeper"] if i == 1 else mats["SM_Graphite"])
        parts.append(seg)
        plate = add_cube("TMP", 1.0, (0, y, 0.40), scale=(0.28, 0.22, 0.07))
        assign_mat(plate, mats["SM_Creeper"] if i == 1 else mats["SM_Graphite"])
        parts.append(plate)
        for sx in (-0.22, 0.22):
            for yo in (-0.05, 0.05):
                leg = add_cube("TMP", 1.0, (sx, y + yo, 0.08), scale=(0.08, 0.04, 0.14))
                assign_mat(leg, mats["SM_Black"])
                parts.append(leg)

    head = add_uv_sphere("TMP", 0.16, (0, 1.12, 0.26), scale=(1.05, 1.10, 0.78), segments=12, rings=8)
    assign_mat(head, mats["SM_Graphite"])
    parts.append(head)
    for x in (-0.08, 0.08):
        nub = add_uv_sphere("TMP", 0.04, (x, 1.18, 0.38), segments=8, rings=6)
        assign_mat(nub, mats["SM_Orange"])
        parts.append(nub)
        eye = add_uv_sphere("TMP", 0.035, (x, 1.24, 0.28), segments=8, rings=6)
        assign_mat(eye, mats["SM_Cyan"])
        parts.append(eye)
    for x in (-0.06, 0.06):
        jaw = add_cone("TMP", 0.025, 0.08, (x, 1.28, 0.16), vertices=6)
        assign_mat(jaw, mats["SM_Black"])
        parts.append(jaw)
    for x in (-0.06, 0.06):
        cerci = add_cone("TMP", 0.03, 0.22, (x, -1.12, 0.16), vertices=6)
        cerci.rotation_euler = (math.pi * 0.5, 0.0, math.pi)
        apply_rot(cerci)
        assign_mat(cerci, mats["SM_Orange"])
        parts.append(cerci)

    obj = join_parts(parts, name)
    col = ensure_collection("25_Fauna_Creeper")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (56.0, 0.0, 0.0)
    return obj


def build_hopper(mats: dict) -> bpy.types.Object:
    """
    Ash hopper — Imagine insectoid shrimp/flea sheet.
    Ash-grey segmented carapace, graphite abdomen, six spindly black X-legs
    with orange knees and sharp point feet, cyan eyes, orange brow stripe,
    thin tail needles. NOT a robot / no propellers.
    Keep ~1.6–1.7 m RTS height (ignore the sheet's ~0.45 m scale bar).
    """
    name = "SM_Unit_AshHopper"
    remove_if_exists(name)
    parts = []

    # Arched shrimp segments — body held high for RTS read.
    for y, z, r, sc, mat_key in (
        (0.30, 1.30, 0.15, (1.05, 0.92, 0.88), "SM_Hopper"),
        (0.12, 1.48, 0.20, (1.18, 1.08, 1.02), "SM_Hopper"),
        (-0.06, 1.36, 0.17, (1.10, 1.00, 0.90), "SM_Hopper"),
        (-0.24, 1.16, 0.15, (0.98, 1.08, 0.82), "SM_Graphite"),
        (-0.40, 1.00, 0.12, (0.88, 1.12, 0.72), "SM_Graphite"),
    ):
        seg = add_uv_sphere("TMP", r, (0, y, z), scale=sc, segments=14, rings=8)
        assign_mat(seg, mats[mat_key])
        parts.append(seg)
    for y, z, sx in (
        (0.18, 1.56, 0.28),
        (0.02, 1.50, 0.26),
        (-0.14, 1.38, 0.24),
        (-0.30, 1.20, 0.20),
    ):
        plate = add_cube("TMP", 1.0, (0, y, z), scale=(sx, 0.10, 0.055))
        assign_mat(plate, mats["SM_Graphite"])
        parts.append(plate)

    for x in (-0.08, 0.08):
        eye = add_uv_sphere("TMP", 0.048, (x, 0.42, 1.28), segments=10, rings=6)
        assign_mat(eye, mats["SM_Cyan"])
        parts.append(eye)
    stripe = add_cube("TMP", 1.0, (0, 0.40, 1.40), scale=(0.22, 0.04, 0.045))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    for x in (-0.05, -0.018, 0.018, 0.05):
        jaw = add_cone("TMP", 0.016, 0.10, (x, 0.44, 1.18), vertices=6)
        jaw.rotation_euler = (math.pi * 0.82, 0.0, 0.0)
        apply_rot(jaw)
        assign_mat(jaw, mats["SM_Black"])
        parts.append(jaw)
    for x in (-0.05, 0.0, 0.05):
        spike = add_cone("TMP", 0.014, 0.28, (x, -0.58, 0.90), vertices=6)
        spike.rotation_euler = (math.pi * 0.62, 0.0, 0.0)
        apply_rot(spike)
        assign_mat(spike, mats["SM_Graphite"])
        parts.append(spike)

    # Six spindly X-legs: hip → orange knee → shin → point foot.
    for sx, sy, hip_y, hip_z in (
        (-1.0, 0.58, 0.16, 1.22),
        (1.0, 0.58, 0.16, 1.22),
        (-1.0, 0.04, 0.02, 1.18),
        (1.0, 0.04, 0.02, 1.18),
        (-1.0, -0.52, -0.14, 1.10),
        (1.0, -0.52, -0.14, 1.10),
    ):
        hip = (sx * 0.12, hip_y, hip_z)
        knee = (sx * 0.55, sy * 0.72, 0.68)
        foot = (sx * 0.78, sy, 0.04)
        femur = add_shaft("TMP", 0.028, hip, knee, vertices=8)
        assign_mat(femur, mats["SM_Black"])
        parts.append(femur)
        joint = add_uv_sphere("TMP", 0.055, knee, segments=8, rings=6)
        assign_mat(joint, mats["SM_Orange"])
        parts.append(joint)
        tibia = add_shaft("TMP", 0.020, knee, foot, vertices=8)
        assign_mat(tibia, mats["SM_Black"])
        parts.append(tibia)
        tip = add_cone("TMP", 0.022, 0.12, (sx * 0.78, sy, 0.05), vertices=6)
        assign_mat(tip, mats["SM_Black"])
        parts.append(tip)

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


def parse_only() -> set[str] | None:
    if "--" not in sys.argv:
        return None
    args = sys.argv[sys.argv.index("--") + 1 :]
    return set(args) if args else None


def main():
    print("[SM] === Unit hero blockouts ===")
    reset_scene()
    mats = create_palette()
    only = parse_only()
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
        if only is not None and u.name not in only:
            continue
        print(" ", dims_report(u))
        export_one(u)
        copy_to_unity(u.name)
    print("[SM] === Done ===")


if __name__ == "__main__":
    main()
