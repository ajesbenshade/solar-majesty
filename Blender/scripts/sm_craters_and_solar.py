"""
Solar Majesty — crater terrain meshes + a proper solar-array farm.

Rebuilds SM_PWR1_SolarArray (replaces the flat-plane blockout).
Adds SM_Crater_Small / Medium / Large.

Run:
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    /Users/aaronesbenshade/solar-conquest/Blender/SolarMajesty_Modules.blend \
    --python /Users/aaronesbenshade/solar-conquest/Blender/scripts/sm_craters_and_solar.py
"""

from __future__ import annotations

import math
import shutil
from pathlib import Path

import bpy
from mathutils import Vector

BLENDER_DIR = Path(__file__).resolve().parent.parent
BLEND_OUT = BLENDER_DIR / "SolarMajesty_Modules.blend"
EXPORT_DIR = BLENDER_DIR / "exports"
REPO = BLENDER_DIR.parent
UNITY_ENV = REPO / "Assets" / "Resources" / "Environment"
UNITY_BLD = REPO / "Assets" / "Resources" / "Buildings"


def ensure_collection(name: str, parent_name: str = "SM_ROOT") -> bpy.types.Collection:
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        parent = bpy.data.collections.get(parent_name)
        if parent:
            parent.children.link(col)
        else:
            bpy.context.scene.collection.children.link(col)
    return col


def link_object(obj: bpy.types.Object, col: bpy.types.Collection):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)


def get_mat(name: str) -> bpy.types.Material:
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    colors = {
        "SM_White": (0.85, 0.86, 0.88, 1),
        "SM_Black": (0.03, 0.03, 0.035, 1),
        "SM_Graphite": (0.12, 0.13, 0.14, 1),
        "SM_Orange": (0.95, 0.38, 0.05, 1),
        "SM_Steel": (0.45, 0.47, 0.5, 1),
        "SM_Regolith": (0.38, 0.34, 0.30, 1),
        "SM_Crater": (0.28, 0.26, 0.23, 1),
        "SM_Solar": (0.035, 0.05, 0.10, 1),
    }
    if bsdf and name in colors:
        bsdf.inputs["Base Color"].default_value = colors[name]
        if name == "SM_Solar":
            bsdf.inputs["Metallic"].default_value = 0.55
            bsdf.inputs["Roughness"].default_value = 0.18
        if name == "SM_Regolith":
            bsdf.inputs["Roughness"].default_value = 0.92
        if name == "SM_Crater":
            bsdf.inputs["Roughness"].default_value = 0.95
    return mat


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


def add_cube(name, size, location, scale=None):
    bpy.ops.mesh.primitive_cube_add(size=size, location=location)
    obj = bpy.context.active_object
    obj.name = name
    if scale:
        obj.scale = scale
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def add_torus(name, major, minor, location, major_seg=28, minor_seg=10):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major,
        minor_radius=minor,
        major_segments=major_seg,
        minor_segments=minor_seg,
        location=location,
    )
    obj = bpy.context.active_object
    obj.name = name
    return obj


def join_parts(parts: list, name: str) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
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


def remove_if_exists(name: str):
    obj = bpy.data.objects.get(name)
    if obj:
        bpy.data.objects.remove(obj, do_unlink=True)


def finalize(obj: bpy.types.Object, col: bpy.types.Collection, location: tuple):
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = location
    export_col = bpy.data.collections.get("09_ExportReady")
    if export_col and obj.name not in [o.name for o in export_col.objects]:
        export_col.objects.link(obj)
    return obj


def export_one(obj: bpy.types.Object):
    EXPORT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    fbx = EXPORT_DIR / f"{obj.name}.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(fbx),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
    )
    print(f"[SM] Exported {fbx.name}")
    return fbx


def copy_to_unity(fbx: Path, dest_dir: Path):
    dest_dir.mkdir(parents=True, exist_ok=True)
    dest = dest_dir / fbx.name
    shutil.copy2(fbx, dest)
    print(f"[SM] Copied → {dest.relative_to(REPO)}")


def build_crater(name: str, radius: float, park: tuple) -> bpy.types.Object:
    remove_if_exists(name)
    col = ensure_collection("10_Craters")
    parts = []

    pit = add_cylinder("TMP", radius * 0.42, 0.18, (0, 0, -0.02), vertices=24)
    assign_mat(pit, get_mat("SM_Crater"))
    parts.append(pit)

    floor = add_cylinder("TMP", radius * 0.78, 0.10, (0, 0, 0.04), vertices=28)
    assign_mat(floor, get_mat("SM_Regolith"))
    parts.append(floor)

    rim = add_torus("TMP", radius * 0.92, radius * 0.16, (0, 0, 0.14), 28, 10)
    assign_mat(rim, get_mat("SM_Regolith"))
    parts.append(rim)

    ejecta = add_torus("TMP", radius * 1.18, radius * 0.07, (0, 0, 0.05), 24, 8)
    assign_mat(ejecta, get_mat("SM_Graphite"))
    parts.append(ejecta)

    for i in range(6):
        ang = i * (math.pi * 2.0 / 6.0) + 0.2
        d = radius * 0.95
        boulder = add_cube(
            "TMP", 1.0,
            (math.cos(ang) * d, math.sin(ang) * d, 0.18),
            scale=(radius * 0.18, radius * 0.14, radius * 0.10),
        )
        assign_mat(boulder, get_mat("SM_Graphite"))
        parts.append(boulder)

    obj = join_parts(parts, name)
    return finalize(obj, col, park)


def build_solar_array() -> bpy.types.Object:
    remove_if_exists("SM_PWR1_SolarArray")
    col = ensure_collection("07_PWR1")
    parts = []

    # Deck / frame
    deck = add_cube("TMP", 1.0, (0, 0, 1.15), scale=(10.4, 12.4, 0.18))
    assign_mat(deck, get_mat("SM_Black"))
    parts.append(deck)

    # Cell grid — dark photovoltaic wafers
    cols, rows = 5, 6
    cell_w, cell_d = 1.72, 1.72
    gap_x, gap_y = 0.18, 0.18
    origin_x = -((cols - 1) * (cell_w + gap_x)) * 0.5
    origin_y = -((rows - 1) * (cell_d + gap_y)) * 0.5
    for ix in range(cols):
        for iy in range(rows):
            x = origin_x + ix * (cell_w + gap_x)
            y = origin_y + iy * (cell_d + gap_y)
            cell = add_cube("TMP", 1.0, (x, y, 1.28), scale=(cell_w, cell_d, 0.08))
            assign_mat(cell, get_mat("SM_Solar"))
            parts.append(cell)

    # Cross beams
    for y in (-4.1, 0.0, 4.1):
        beam = add_cube("TMP", 1.0, (0, y, 1.22), scale=(10.2, 0.12, 0.10))
        assign_mat(beam, get_mat("SM_Steel"))
        parts.append(beam)
    for x in (-3.4, 0.0, 3.4):
        beam = add_cube("TMP", 1.0, (x, 0, 1.22), scale=(0.12, 12.2, 0.10))
        assign_mat(beam, get_mat("SM_Steel"))
        parts.append(beam)

    # Orange rim ticks
    for sx, sy, sc in (
        (0, 6.28, (10.5, 0.16, 0.12)),
        (0, -6.28, (10.5, 0.16, 0.12)),
        (5.28, 0, (0.16, 12.5, 0.12)),
        (-5.28, 0, (0.16, 12.5, 0.12)),
    ):
        edge = add_cube("TMP", 1.0, (sx, sy, 1.32), scale=sc)
        assign_mat(edge, get_mat("SM_Orange"))
        parts.append(edge)

    # Support pylons
    for x, y in ((-4.4, -5.2), (4.4, -5.2), (-4.4, 5.2), (4.4, 5.2)):
        leg = add_cube("TMP", 1.0, (x, y, 0.55), scale=(0.28, 0.28, 1.1))
        assign_mat(leg, get_mat("SM_Graphite"))
        parts.append(leg)
        foot = add_cube("TMP", 1.0, (x, y, 0.08), scale=(0.7, 0.7, 0.16))
        assign_mat(foot, get_mat("SM_Black"))
        parts.append(foot)

    # Inverter box
    box = add_cube("TMP", 1.0, (4.6, 0, 0.55), scale=(0.9, 1.4, 0.9))
    assign_mat(box, get_mat("SM_White"))
    parts.append(box)
    stripe = add_cube("TMP", 1.0, (5.08, 0, 0.55), scale=(0.08, 1.2, 0.55))
    assign_mat(stripe, get_mat("SM_Orange"))
    parts.append(stripe)

    obj = join_parts(parts, "SM_PWR1_SolarArray")
    return finalize(obj, col, (28.0, -18.0, 0.0))


def main():
    print("[SM] === Craters + solar array ===")
    created = [
        build_crater("SM_Crater_Small", 2.5, (-20.0, -30.0, 0.0)),
        build_crater("SM_Crater_Medium", 4.5, (0.0, -30.0, 0.0)),
        build_crater("SM_Crater_Large", 7.0, (22.0, -30.0, 0.0)),
        build_solar_array(),
    ]

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))
    print(f"[SM] Saved {BLEND_OUT}")

    for obj in created:
        fbx = export_one(obj)
        dest = UNITY_ENV if obj.name.startswith("SM_Crater") else UNITY_BLD
        copy_to_unity(fbx, dest)

    print("[SM] === Done ===")


if __name__ == "__main__":
    main()
