"""
Solar Majesty — hero unit blockouts (Scout / Engineer / Defense / Dust Stalker).

Readable Majesty-scale silhouettes (~2–3 m tall) with SpaceX white/black/orange palette.
No concept sheets required — proportions match runtime UnitPlaceholderFactory.

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
    """Tall thin probe ~2.9 m — explore silhouette."""
    name = "SM_Unit_ScoutDrone"
    remove_if_exists(name)
    parts = []

    body = add_cylinder("TMP", 0.28, 2.0, (0, 0, 1.15), vertices=24)
    assign_mat(body, mats["SM_Scout"])
    parts.append(body)

    band = add_cylinder("TMP", 0.32, 0.14, (0, 0, 1.35), vertices=24)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)

    sensor = add_uv_sphere("TMP", 0.22, (0, 0, 2.35))
    assign_mat(sensor, mats["SM_White"])
    parts.append(sensor)

    visor = add_cube("TMP", 1.0, (0, 0.18, 2.35), scale=(0.28, 0.08, 0.12))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)

    ant = add_cylinder("TMP", 0.03, 0.7, (0.16, 0, 2.85), vertices=12)
    assign_mat(ant, mats["SM_Steel"])
    parts.append(ant)

    tip = add_uv_sphere("TMP", 0.05, (0.16, 0, 3.2))
    assign_mat(tip, mats["SM_Orange"])
    parts.append(tip)

    beacon = add_cube("TMP", 1.0, (-0.2, 0, 2.7), scale=(0.08, 0.08, 0.08))
    assign_mat(beacon, mats["SM_Orange"])
    parts.append(beacon)

    foot = add_cylinder("TMP", 0.22, 0.12, (0, 0, 0.06), vertices=16)
    assign_mat(foot, mats["SM_Black"])
    parts.append(foot)

    obj = join_parts(parts, name)
    col = ensure_collection("10_Units_Scout")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (-4.0, 0.0, 0.0)
    return obj


def build_engineer(mats: dict) -> bpy.types.Object:
    """Squat builder ~2.1 m — toolbox + stripe."""
    name = "SM_Unit_EngineerBot"
    remove_if_exists(name)
    parts = []

    body = add_cylinder("TMP", 0.55, 1.5, (0, 0, 0.95), vertices=24)
    assign_mat(body, mats["SM_Engineer"])
    parts.append(body)

    band = add_cylinder("TMP", 0.60, 0.14, (0, 0, 0.75), vertices=24)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)

    toolbox = add_cube("TMP", 1.0, (0.72, 0, 0.85), scale=(0.42, 0.35, 0.32))
    assign_mat(toolbox, mats["SM_Black"])
    parts.append(toolbox)

    stripe = add_cube("TMP", 1.0, (0.72, 0.2, 0.95), scale=(0.44, 0.06, 0.08))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)

    visor = add_cube("TMP", 1.0, (0, 0.48, 1.35), scale=(0.45, 0.08, 0.14))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)

    arm = add_cube("TMP", 1.0, (-0.65, 0.15, 1.0), scale=(0.32, 0.14, 0.14))
    assign_mat(arm, mats["SM_Steel"])
    parts.append(arm)

    claw = add_cube("TMP", 1.0, (-0.95, 0.15, 1.0), scale=(0.12, 0.18, 0.1))
    assign_mat(claw, mats["SM_Graphite"])
    parts.append(claw)

    foot = add_cylinder("TMP", 0.42, 0.12, (0, 0, 0.06), vertices=16)
    assign_mat(foot, mats["SM_Black"])
    parts.append(foot)

    obj = join_parts(parts, name)
    col = ensure_collection("11_Units_Engineer")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (0.0, 0.0, 0.0)
    return obj


def build_defense(mats: dict) -> bpy.types.Object:
    """Wide combat chassis ~2.4 m — shield + shoulder."""
    name = "SM_Unit_DefenseMech"
    remove_if_exists(name)
    parts = []

    body = add_cylinder("TMP", 0.62, 1.7, (0, 0, 1.05), vertices=24)
    assign_mat(body, mats["SM_Defense"])
    parts.append(body)

    band = add_cylinder("TMP", 0.68, 0.14, (0, 0, 0.85), vertices=24)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)

    shoulder = add_cube("TMP", 1.0, (0.7, 0, 1.55), scale=(0.55, 0.5, 0.38))
    assign_mat(shoulder, mats["SM_White"])
    parts.append(shoulder)

    accent = add_cube("TMP", 1.0, (0.7, 0.28, 1.7), scale=(0.42, 0.08, 0.08))
    assign_mat(accent, mats["SM_Orange"])
    parts.append(accent)

    shield = add_cube("TMP", 1.0, (-0.78, 0.1, 1.15), scale=(0.12, 0.85, 1.05))
    assign_mat(shield, mats["SM_Steel"])
    parts.append(shield)

    plating = add_cube("TMP", 1.0, (0, 0.5, 1.25), scale=(0.55, 0.1, 0.3))
    assign_mat(plating, mats["SM_Black"])
    parts.append(plating)

    head = add_uv_sphere("TMP", 0.28, (0, 0, 2.05), scale=(1.0, 0.9, 0.85))
    assign_mat(head, mats["SM_White"])
    parts.append(head)

    foot = add_cylinder("TMP", 0.55, 0.14, (0, 0, 0.07), vertices=16)
    assign_mat(foot, mats["SM_Black"])
    parts.append(foot)

    obj = join_parts(parts, name)
    col = ensure_collection("12_Units_Defense")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (4.0, 0.0, 0.0)
    return obj


def build_stalker(mats: dict) -> bpy.types.Object:
    """Low predator ~0.9 m tall — orange eyes."""
    name = "SM_Unit_DustStalker"
    remove_if_exists(name)
    parts = []

    body = add_uv_sphere("TMP", 0.55, (0, 0, 0.4), scale=(1.55, 1.0, 0.55), segments=24, rings=12)
    assign_mat(body, mats["SM_Stalker"])
    parts.append(body)

    head = add_uv_sphere("TMP", 0.28, (0, 0.55, 0.48), scale=(1.0, 1.0, 0.85))
    assign_mat(head, mats["SM_Stalker"])
    parts.append(head)

    spine = add_cube("TMP", 1.0, (0, -0.25, 0.58), scale=(0.12, 0.7, 0.1))
    assign_mat(spine, mats["SM_Black"])
    parts.append(spine)

    for y in (0.05, -0.25):
        ridge = add_cone("TMP", 0.08, 0.28, (0, y, 0.72))
        assign_mat(ridge, mats["SM_Black"])
        parts.append(ridge)

    for x in (-0.14, 0.14):
        eye = add_uv_sphere("TMP", 0.055, (x, 0.78, 0.55), segments=12, rings=8)
        assign_mat(eye, mats["SM_Orange"])
        parts.append(eye)

    for x, y in ((-0.45, 0.3), (0.45, 0.3), (-0.4, -0.35), (0.4, -0.35)):
        leg = add_cube("TMP", 1.0, (x, y, 0.18), scale=(0.08, 0.08, 0.32))
        assign_mat(leg, mats["SM_Black"])
        parts.append(leg)

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
