"""
Solar Majesty — add SM_LAB1_LaboratoryModule blockout to the existing blend.

Target: Ø4.5 m × L8.7 m cylindrical lab (smaller sibling of HAB-1).
Does NOT modify HAB / connector / other modules.

Run:
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    Blender/SolarMajesty_Modules.blend \
    --python Blender/scripts/sm_add_lab1_blockout.py
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector

BLENDER_DIR = Path(__file__).resolve().parent.parent
BLEND_OUT = BLENDER_DIR / "SolarMajesty_Modules.blend"
EXPORT_DIR = BLENDER_DIR / "exports"
OBJ_NAME = "SM_LAB1_LaboratoryModule"


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
    }
    if bsdf and name in colors:
        bsdf.inputs["Base Color"].default_value = colors[name]
    return mat


def assign_mat(obj: bpy.types.Object, mat: bpy.types.Material):
    if obj.data and hasattr(obj.data, "materials"):
        if obj.data.materials:
            obj.data.materials[0] = mat
        else:
            obj.data.materials.append(mat)


def add_cylinder(name, radius, depth, location, rotation=(0, 0, 0), vertices=48):
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


def remove_lab():
    obj = bpy.data.objects.get(OBJ_NAME)
    if obj:
        bpy.data.objects.remove(obj, do_unlink=True)


def build_lab1() -> bpy.types.Object:
    """LAB-1: Ø4.5 × L8.7 cylindrical lab with dock stubs + orange hatch."""
    remove_lab()
    R = 2.25  # Ø4.5
    L = 8.7
    z_axis = R + 0.2

    parts = []
    shell = add_cylinder("TMP", R, L * 0.82, (0, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=48)
    assign_mat(shell, get_mat("SM_White"))
    parts.append(shell)

    mid = add_cylinder("TMP", R * 1.03, 1.0, (0, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=48)
    assign_mat(mid, get_mat("SM_Black"))
    parts.append(mid)

    for sign in (-1, 1):
        x = sign * (L * 0.38)
        cap = add_cylinder("TMP", R * 1.01, 0.55, (x, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=48)
        assign_mat(cap, get_mat("SM_Black"))
        parts.append(cap)
        ring = add_cylinder("TMP", R * 1.08, 0.12, (x + sign * 0.35, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=48)
        assign_mat(ring, get_mat("SM_Orange"))
        parts.append(ring)

    # Side instrument bay
    bay = add_cube("TMP", 1.0, (0, R * 0.85, z_axis + 0.2), scale=(2.8, 0.7, 1.4))
    assign_mat(bay, get_mat("SM_Graphite"))
    parts.append(bay)
    hatch = add_cube("TMP", 1.0, (0.2, R * 1.05, z_axis + 0.15), scale=(1.1, 0.12, 0.9))
    assign_mat(hatch, get_mat("SM_Orange"))
    parts.append(hatch)

    # Roof sensor mast
    mast = add_cylinder("TMP", 0.08, 2.2, (1.2, 0, z_axis + R + 0.6))
    assign_mat(mast, get_mat("SM_Steel"))
    parts.append(mast)
    dish = add_cylinder("TMP", 0.45, 0.12, (1.2, 0, z_axis + R + 1.6), rotation=(math.radians(70), 0, 0))
    assign_mat(dish, get_mat("SM_White"))
    parts.append(dish)

    # Skids
    for y in (-1.1, 1.1):
        skid = add_cube("TMP", 1.0, (0, y, 0.12), scale=(L * 0.7, 0.45, 0.25))
        assign_mat(skid, get_mat("SM_Black"))
        parts.append(skid)

    # Dock stubs (mate to modular connector OD ~2.8)
    for sign in (-1, 1):
        x = sign * (L * 0.48)
        dock = add_cylinder("TMP", 1.2, 1.2, (x, 0, z_axis), rotation=(0, math.radians(90), 0))
        assign_mat(dock, get_mat("SM_White"))
        parts.append(dock)
        flange = add_cylinder("TMP", 1.35, 0.18, (x + sign * 0.55, 0, z_axis), rotation=(0, math.radians(90), 0))
        assign_mat(flange, get_mat("SM_Black"))
        parts.append(flange)

    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = OBJ_NAME

    col = ensure_collection("04_LAB1")
    # Rename WIP slot if present
    wip = bpy.data.collections.get("04_LAB1_WIP")
    if wip and wip != col:
        for o in list(wip.objects):
            wip.objects.unlink(o)
            col.objects.link(o)
        wip.name = "04_LAB1_WIP_empty"

    link_object(obj, col)
    set_origin_to_ground(obj)
    # Park near HAB strip (HAB sits around Y=10 in setup script)
    obj.location = (0.0, 22.0, 0.0)

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


def dims_report(obj: bpy.types.Object) -> str:
    bpy.context.view_layer.update()
    mat = obj.matrix_world
    corners = [mat @ Vector(c) for c in obj.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return (
        f"{obj.name}: "
        f"W={max(xs)-min(xs):.1f}m  D={max(ys)-min(ys):.1f}m  H={max(zs)-min(zs):.1f}m"
    )


def main():
    print("[SM] === Add LAB-1 Laboratory Module ===")
    obj = build_lab1()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))
    print(f"[SM] Saved {BLEND_OUT}")
    print(" ", dims_report(obj))
    export_one(obj)
    print("[SM] === Done ===")


if __name__ == "__main__":
    main()
