"""
Solar Majesty — environment prop blockouts (trees, rocks, dune, vista crater).

Stand-ins when Copilot 3D GLBs are not ready yet. Exports FBX to
Assets/Resources/Environment/.

Run:
  blender --background --python Blender/scripts/sm_environment_blockouts.py
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
EXPORT_DIR = BLENDER_DIR / "exports"
UNITY_ENV = PROJECT_ROOT / "Assets" / "Resources" / "Environment"


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"


def make_mat(name: str, color: tuple, roughness: float = 0.55):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def assign(obj, mat):
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)


def join_named(name: str, parts: list) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    return obj


def origin_ground(obj):
    bpy.context.view_layer.update()
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
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
    obj.location = (0.0, 0.0, 0.0)


def export_fbx(obj: bpy.types.Object):
    EXPORT_DIR.mkdir(parents=True, exist_ok=True)
    UNITY_ENV.mkdir(parents=True, exist_ok=True)
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
    shutil.copy2(fbx, UNITY_ENV / f"{obj.name}.fbx")
    print(f"[SM] Exported {obj.name}.fbx")


def build_tree_a(mats):
    parts = []
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.22, depth=0.35, location=(0, 0, 0.18))
    base = bpy.context.active_object
    assign(base, mats["trunk"])
    parts.append(base)
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.14, depth=1.15, location=(0, 0, 0.85))
    trunk = bpy.context.active_object
    assign(trunk, mats["trunk"])
    parts.append(trunk)
    # Irregular canopy cluster — not a single lollipop sphere
    lobes = [
        ((0.0, 0.0, 1.95), (1.05, 0.95, 0.9), "leaf"),
        ((0.55, -0.25, 1.75), (0.7, 0.65, 0.55), "leaf"),
        ((-0.45, 0.35, 1.7), (0.65, 0.6, 0.5), "leaf_dark"),
        ((0.15, 0.4, 2.15), (0.55, 0.5, 0.45), "leaf"),
        ((-0.2, -0.35, 2.0), (0.5, 0.48, 0.42), "leaf_dark"),
    ]
    for loc, sc, mat_key in lobes:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=loc)
        lobe = bpy.context.active_object
        lobe.scale = sc
        bpy.ops.object.transform_apply(scale=True)
        assign(lobe, mats[mat_key])
        parts.append(lobe)
    obj = join_named("SM_Tree_Broadleaf_A", parts)
    origin_ground(obj)
    return obj


def build_tree_b(mats):
    parts = []
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.18, depth=0.3, location=(0.05, 0, 0.15))
    base = bpy.context.active_object
    assign(base, mats["trunk"])
    parts.append(base)
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.12, depth=1.45, location=(0.05, 0, 0.95))
    trunk = bpy.context.active_object
    assign(trunk, mats["trunk"])
    parts.append(trunk)
    # Short side branch
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8, radius=0.06, depth=0.55, location=(0.35, 0.05, 1.35), rotation=(0, 1.1, 0.3)
    )
    branch = bpy.context.active_object
    assign(branch, mats["trunk"])
    parts.append(branch)
    lobes = [
        ((-0.2, 0.15, 2.15), (0.85, 0.75, 0.7), "leaf_dark"),
        ((0.45, -0.2, 1.9), (0.75, 0.7, 0.6), "leaf"),
        ((0.7, 0.1, 1.55), (0.55, 0.5, 0.45), "leaf"),
        ((0.05, -0.35, 2.35), (0.5, 0.48, 0.42), "leaf_dark"),
        ((-0.35, -0.2, 1.85), (0.6, 0.55, 0.5), "leaf"),
    ]
    for loc, sc, mat_key in lobes:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=loc)
        lobe = bpy.context.active_object
        lobe.scale = sc
        bpy.ops.object.transform_apply(scale=True)
        assign(lobe, mats[mat_key])
        parts.append(lobe)
    obj = join_named("SM_Tree_Broadleaf_B", parts)
    origin_ground(obj)
    return obj


def build_rock_a(mats):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.55, location=(0, 0, 0.28))
    rock = bpy.context.active_object
    rock.scale = (1.1, 0.85, 0.65)
    bpy.ops.object.transform_apply(scale=True)
    assign(rock, mats["rock"])
    rock.name = "SM_Rock_Boulder_A"
    origin_ground(rock)
    return rock


def build_rock_b(mats):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.45, location=(0, 0, 0.2))
    a = bpy.context.active_object
    a.scale = (1.35, 1.0, 0.5)
    bpy.ops.object.transform_apply(scale=True)
    assign(a, mats["rock_dark"])
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.28, location=(0.35, 0.15, 0.15))
    b = bpy.context.active_object
    assign(b, mats["rock"])
    obj = join_named("SM_Rock_Boulder_B", [a, b])
    origin_ground(obj)
    return obj


def build_crater_vista(mats):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=5.0, depth=0.35, location=(0, 0, 0.12))
    rim = bpy.context.active_object
    assign(rim, mats["rim"])
    bpy.ops.mesh.primitive_cylinder_add(vertices=28, radius=3.4, depth=0.12, location=(0.1, -0.1, 0.04))
    floor = bpy.context.active_object
    assign(floor, mats["floor"])
    obj = join_named("SM_Crater_Vista", [rim, floor])
    origin_ground(obj)
    return obj


def build_dune(mats):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=1.0, location=(0, 0, 0.35))
    dune = bpy.context.active_object
    dune.scale = (3.0, 1.1, 0.55)
    bpy.ops.object.transform_apply(scale=True)
    assign(dune, mats["dune"])
    dune.name = "SM_Dune_Low"
    origin_ground(dune)
    return dune


def main():
    print("[SM] === Environment blockouts ===")
    reset_scene()
    mats = {
        "trunk": make_mat("SM_Trunk", (0.28, 0.18, 0.10), 0.7),
        "leaf": make_mat("SM_Leaf", (0.22, 0.42, 0.16), 0.65),
        "leaf_dark": make_mat("SM_LeafDark", (0.14, 0.32, 0.12), 0.7),
        "rock": make_mat("SM_Rock", (0.55, 0.32, 0.18), 0.75),
        "rock_dark": make_mat("SM_RockDark", (0.35, 0.22, 0.14), 0.8),
        "rim": make_mat("SM_CraterRim", (0.58, 0.30, 0.14), 0.7),
        "floor": make_mat("SM_CraterFloor", (0.34, 0.14, 0.07), 0.75),
        "dune": make_mat("SM_Dune", (0.72, 0.42, 0.22), 0.85),
    }
    objs = [
        build_tree_a(mats),
        build_tree_b(mats),
        build_rock_a(mats),
        build_rock_b(mats),
        build_crater_vista(mats),
        build_dune(mats),
    ]
    blend = BLENDER_DIR / "SolarMajesty_Environment.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    for o in objs:
        export_fbx(o)
    print("[SM] === Done ===")


if __name__ == "__main__":
    main()
