"""
Solar Majesty — Blender setup + Modular Tube Connector + HAB-1 blockout.

Run (from anywhere):
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    --python /path/to/sm_setup_and_blockout.py

Or open Blender → Scripting → Open this file → Run Script.

Scale: 1 Blender unit = 1 meter (Unity-friendly).
Pivots: ground contact (feet) for modules; tube connector at centerline base.
"""

from __future__ import annotations

import math
import os
from pathlib import Path

import bpy
from mathutils import Vector, Euler

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

SCRIPT_DIR = Path(__file__).resolve().parent
BLENDER_DIR = SCRIPT_DIR.parent
PROJECT_ROOT = BLENDER_DIR.parent
CONCEPT_DIRS = [
    PROJECT_ROOT / "ConceptSheets",
    PROJECT_ROOT / "ReferenceArt",
    PROJECT_ROOT,
    BLENDER_DIR / "references",
]

REF_FILES = [
    "SM_HAB-1_HabitatModule_Turnaround.jpg",
    "SM_LAB-1_LaboratoryModule_Turnaround.jpg",
    "SM_CMD-1_OPS-1_CommandOps_Turnaround.jpg",
    "SM_CommandDome_CentralHub_Turnaround.jpg",
    "SM_PWR-1_PowerNode_SolarArrays.jpg",
    "SM_Starship_LandingPad_UprightStack.jpg",
    "SM_MarsColony_BaseOverview_Isometric.jpg",  # optional / may be missing
]

BLEND_OUT = BLENDER_DIR / "SolarMajesty_Modules.blend"
EXPORT_DIR = BLENDER_DIR / "exports"


def find_image(name: str) -> Path | None:
    for d in CONCEPT_DIRS:
        p = d / name
        if p.is_file():
            return p
    return None


# ---------------------------------------------------------------------------
# Scene reset & units
# ---------------------------------------------------------------------------

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    # Comfortable modeling grid for 8–12 m modules
    scene.tool_settings.use_snap = True
    # World
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.12, 0.13, 0.15, 1.0)
        bg.inputs[1].default_value = 0.6


def ensure_collection(name: str, parent: bpy.types.Collection | None = None) -> bpy.types.Collection:
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        if parent is None:
            bpy.context.scene.collection.children.link(col)
        else:
            parent.children.link(col)
    return col


def link_object(obj: bpy.types.Object, col: bpy.types.Collection):
    # Unlink from scene root if present
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)


def set_origin_to_ground(obj: bpy.types.Object):
    """Place object origin at bottom-center (ground contact) in world space."""
    bpy.context.view_layer.update()
    # Use bounding box in world space
    mat = obj.matrix_world
    corners = [mat @ Vector(c) for c in obj.bound_box]
    min_z = min(c.z for c in corners)
    cx = sum(c.x for c in corners) / 8.0
    cy = sum(c.y for c in corners) / 8.0
    # Cursor method
    cursor = bpy.context.scene.cursor
    prev = cursor.location.copy()
    cursor.location = Vector((cx, cy, min_z))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    cursor.location = prev
    # Move so origin sits at world origin ground
    obj.location = (0.0, 0.0, 0.0)


# ---------------------------------------------------------------------------
# Materials (Principled BSDF — white / black / orange / steel)
# ---------------------------------------------------------------------------

def make_principled(name: str, base: tuple, metallic: float = 0.15, roughness: float = 0.45) -> bpy.types.Material:
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
    bsdf.location = (0, 0)
    out.location = (300, 0)
    bsdf.inputs["Base Color"].default_value = (*base, 1.0)
    # Blender 4+/5 input names
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = metallic
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def create_palette() -> dict:
    return {
        "SM_White": make_principled("SM_White", (0.85, 0.86, 0.88), metallic=0.12, roughness=0.42),
        "SM_Black": make_principled("SM_Black", (0.03, 0.03, 0.035), metallic=0.35, roughness=0.48),
        "SM_Graphite": make_principled("SM_Graphite", (0.12, 0.13, 0.14), metallic=0.4, roughness=0.4),
        "SM_Orange": make_principled("SM_Orange", (0.95, 0.38, 0.05), metallic=0.08, roughness=0.35),
        "SM_Steel": make_principled("SM_Steel", (0.45, 0.47, 0.5), metallic=0.7, roughness=0.32),
        "SM_Glass": make_principled("SM_Glass", (0.55, 0.62, 0.7), metallic=0.0, roughness=0.08),
    }


def assign_mat(obj: bpy.types.Object, mat: bpy.types.Material):
    if obj.data and hasattr(obj.data, "materials"):
        if obj.data.materials:
            obj.data.materials[0] = mat
        else:
            obj.data.materials.append(mat)


# ---------------------------------------------------------------------------
# Reference images
# ---------------------------------------------------------------------------

def add_reference_image(path: Path, name: str, location: Vector, size: float, rot_euler: Euler) -> bpy.types.Object | None:
    if not path.is_file():
        print(f"[SM] Missing reference: {path.name}")
        return None
    img = bpy.data.images.load(str(path), check_existing=True)
    # Empty image reference
    bpy.ops.object.empty_add(type="IMAGE", location=location)
    empty = bpy.context.active_object
    empty.name = f"REF_{name}"
    empty.data = img
    empty.empty_display_size = size
    empty.rotation_euler = rot_euler
    # Prefer front-facing opacity for modeling
    empty.use_empty_image_alpha = True
    empty.color[3] = 0.85
    empty.show_empty_image_orthographic = True
    empty.show_empty_image_perspective = True
    empty.empty_image_side = "FRONT"
    return empty


def setup_references(ref_col: bpy.types.Collection):
    """
    Place orthographic-ish reference empties around the origin for HAB-1 modeling.
    Front = -Y, Side = +X, Iso off to the side.
    """
    layout = {
        "SM_HAB-1_HabitatModule_Turnaround.jpg": {
            "name": "HAB1_Sheet",
            "loc": Vector((0, -18, 6)),
            "size": 14.0,
            "rot": Euler((math.radians(90), 0, 0)),
        },
        "SM_LAB-1_LaboratoryModule_Turnaround.jpg": {
            "name": "LAB1_Sheet",
            "loc": Vector((22, -18, 5)),
            "size": 12.0,
            "rot": Euler((math.radians(90), 0, 0)),
        },
        "SM_CMD-1_OPS-1_CommandOps_Turnaround.jpg": {
            "name": "CMD_OPS_Sheet",
            "loc": Vector((-24, -18, 5)),
            "size": 14.0,
            "rot": Euler((math.radians(90), 0, 0)),
        },
        "SM_CommandDome_CentralHub_Turnaround.jpg": {
            "name": "Dome_Sheet",
            "loc": Vector((0, -18, 22)),
            "size": 12.0,
            "rot": Euler((math.radians(90), 0, 0)),
        },
        "SM_PWR-1_PowerNode_SolarArrays.jpg": {
            "name": "PWR1_Sheet",
            "loc": Vector((22, 8, 5)),
            "size": 12.0,
            "rot": Euler((math.radians(90), 0, math.radians(90))),
        },
        "SM_Starship_LandingPad_UprightStack.jpg": {
            "name": "LandingPad_Sheet",
            "loc": Vector((-24, 8, 6)),
            "size": 14.0,
            "rot": Euler((math.radians(90), 0, math.radians(-90))),
        },
        "SM_MarsColony_BaseOverview_Isometric.jpg": {
            "name": "BaseOverview_Sheet",
            "loc": Vector((0, 20, 8)),
            "size": 16.0,
            "rot": Euler((math.radians(75), 0, 0)),
        },
    }

    found = []
    missing = []
    for fname, cfg in layout.items():
        p = find_image(fname)
        if p is None:
            missing.append(fname)
            continue
        empty = add_reference_image(p, cfg["name"], cfg["loc"], cfg["size"], cfg["rot"])
        if empty:
            link_object(empty, ref_col)
            found.append(fname)

    print(f"[SM] References loaded: {len(found)}")
    if missing:
        print(f"[SM] References missing: {missing}")
    return found, missing


# ---------------------------------------------------------------------------
# Modeling helpers
# ---------------------------------------------------------------------------

def add_cylinder(name, radius, depth, location, rotation=(0, 0, 0), vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
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


def add_torus(name, major, minor, location, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major,
        minor_radius=minor,
        major_segments=48,
        minor_segments=12,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.active_object
    obj.name = name
    return obj


def join_selected(name: str) -> bpy.types.Object:
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    return obj


# ---------------------------------------------------------------------------
# A) Modular Tube Connector (standard docking interface)
# From CMD/OPS sheet: white cylindrical corridor, black docking rings both ends,
# ~3–4 m long at building scale; HAB dock diameter ~2.4 m interior visual.
# Use consistent docking OD ≈ 2.8 m outer ring to mate with HAB-1 endcaps.
# ---------------------------------------------------------------------------

def build_modular_tube_connector(mats: dict, col: bpy.types.Collection) -> bpy.types.Object:
    """
    Faithful blockout of the STANDARD modular connector shown under CMD/OPS sheets.
    Length ~4.0 m, outer body radius ~1.25 m, docking flanges ~1.4 m radius.
    """
    parts = []

    # Main pressure tube (white)
    body = add_cylinder("TMP_tube_body", radius=1.15, depth=3.2, location=(0, 0, 1.15), rotation=(0, math.radians(90), 0))
    assign_mat(body, mats["SM_White"])
    parts.append(body)

    # Mid structural band (black)
    mid = add_cylinder("TMP_tube_mid", radius=1.22, depth=0.55, location=(0, 0, 1.15), rotation=(0, math.radians(90), 0))
    assign_mat(mid, mats["SM_Black"])
    parts.append(mid)

    # Docking rings (black + orange accent ring) — both ends along X
    for sign, tag in ((-1, "A"), (1, "B")):
        x = sign * 1.85
        ring = add_cylinder(f"TMP_dock_{tag}", radius=1.35, depth=0.35, location=(x, 0, 1.15), rotation=(0, math.radians(90), 0))
        assign_mat(ring, mats["SM_Black"])
        parts.append(ring)
        accent = add_cylinder(f"TMP_dock_orange_{tag}", radius=1.42, depth=0.08, location=(x + sign * 0.18, 0, 1.15), rotation=(0, math.radians(90), 0))
        assign_mat(accent, mats["SM_Orange"])
        parts.append(accent)
        # Inner collar (steel)
        collar = add_cylinder(f"TMP_collar_{tag}", radius=0.95, depth=0.2, location=(x + sign * 0.25, 0, 1.15), rotation=(0, math.radians(90), 0))
        assign_mat(collar, mats["SM_Steel"])
        parts.append(collar)

    # Small orange service boxes on mid band
    for y in (-0.9, 0.9):
        box = add_cube("TMP_svc", 0.35, location=(0, y, 1.35), scale=(1.2, 0.6, 0.5))
        assign_mat(box, mats["SM_Orange"])
        parts.append(box)

    # Support skids (black) — ground contact
    for y in (-0.7, 0.7):
        skid = add_cube("TMP_skid", 0.4, location=(0, y, 0.15), scale=(3.5, 0.6, 0.35))
        assign_mat(skid, mats["SM_Black"])
        parts.append(skid)

    # Join
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    obj = join_selected("SM_ModularTubeConnector")
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = (0.0, 0.0, 0.0)
    return obj


# ---------------------------------------------------------------------------
# B) HAB-1 Cylindrical Habitat — blockout from sheet
# DIAMETER 8.0 m → radius 4.0 m
# LENGTH 12.0 m
# White shell, black end/mid bands, orange hatch + rings, landing feet
# Axis along X (docking left/right), ground under skids
# ---------------------------------------------------------------------------

def build_hab1_blockout(mats: dict, col: bpy.types.Collection) -> bpy.types.Object:
    R = 4.0          # radius
    L = 12.0         # length
    z_center = R * 0.55 + 0.35  # lifted onto feet (~ half radius + foot height) — readable RTS scale

    # For Majesty readability we keep full 8m diameter but rest on skids:
    # cylinder axis horizontal along X, center height = radius so bottom touches skids.
    z_axis = R + 0.25  # 4.25 m — bottom of cylinder just above ground pads

    parts = []

    # Primary white pressure shell
    shell = add_cylinder("TMP_hab_shell", radius=R, depth=L * 0.78, location=(0, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=64)
    assign_mat(shell, mats["SM_White"])
    parts.append(shell)

    # Black structural mid band
    mid = add_cylinder("TMP_hab_midband", radius=R * 1.02, depth=1.4, location=(0, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=64)
    assign_mat(mid, mats["SM_Black"])
    parts.append(mid)

    # Black end caps / docking barrels
    for sign, tag in ((-1, "FWD"), (1, "AFT")):
        x = sign * (L * 0.5 - 0.9)
        cap = add_cylinder(f"TMP_hab_cap_{tag}", radius=R * 0.98, depth=1.6, location=(x, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=64)
        assign_mat(cap, mats["SM_Black"])
        parts.append(cap)
        # Orange docking ring accent
        o = add_cylinder(f"TMP_hab_oring_{tag}", radius=R * 1.04, depth=0.18, location=(x + sign * 0.55, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=64)
        assign_mat(o, mats["SM_Orange"])
        parts.append(o)
        # Docking tunnel collar (smaller — modular interface)
        dock = add_cylinder(f"TMP_hab_dock_{tag}", radius=1.45, depth=0.7, location=(x + sign * 1.1, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=48)
        assign_mat(dock, mats["SM_Graphite"])
        parts.append(dock)
        dock_o = add_cylinder(f"TMP_hab_docko_{tag}", radius=1.55, depth=0.1, location=(x + sign * 1.4, 0, z_axis), rotation=(0, math.radians(90), 0), vertices=48)
        assign_mat(dock_o, mats["SM_Orange"])
        parts.append(dock_o)

    # Front circular hatch plate (steel disc)
    hatch_f = add_cylinder("TMP_hab_front_hatch", radius=1.8, depth=0.15, location=(-L * 0.5 + 0.15, 0, z_axis), rotation=(0, math.radians(90), 0))
    assign_mat(hatch_f, mats["SM_Steel"])
    parts.append(hatch_f)

    # Side orange access hatch (sheet: large orange door mid-body)
    door = add_cube("TMP_hab_door", 1.0, location=(0.2, -R * 0.92, z_axis), scale=(1.4, 0.25, 2.2))
    assign_mat(door, mats["SM_Orange"])
    parts.append(door)
    door_frame = add_cube("TMP_hab_door_frame", 1.0, location=(0.2, -R * 0.98, z_axis), scale=(1.7, 0.15, 2.6))
    assign_mat(door_frame, mats["SM_Black"])
    parts.append(door_frame)

    # Top utility box (white/black)
    util = add_cube("TMP_hab_util", 1.0, location=(-1.5, 0, z_axis + R * 0.85), scale=(2.2, 1.4, 0.7))
    assign_mat(util, mats["SM_White"])
    parts.append(util)
    util_top = add_cube("TMP_hab_util_top", 1.0, location=(-1.5, 0, z_axis + R * 0.95), scale=(1.6, 1.0, 0.35))
    assign_mat(util_top, mats["SM_Black"])
    parts.append(util_top)

    # Small orange marker strips on shell
    for x in (-3.5, 3.5):
        strip = add_cube("TMP_hab_strip", 0.4, location=(x, -R * 0.15, z_axis + R * 0.55), scale=(0.8, 0.15, 0.12))
        assign_mat(strip, mats["SM_Orange"])
        parts.append(strip)

    # Landing feet / skids (4) — black, ground contact (sheet: dual skid pairs)
    foot_z = 0.35
    for x, y in ((-3.5, -1.8), (-3.5, 1.8), (3.5, -1.8), (3.5, 1.8)):
        leg = add_cube("TMP_hab_leg", 0.5, location=(x, y, foot_z + 0.4), scale=(1.0, 0.7, 1.6))
        assign_mat(leg, mats["SM_Black"])
        parts.append(leg)
        pad = add_cube("TMP_hab_pad", 0.5, location=(x, y, 0.12), scale=(1.6, 1.2, 0.25))
        assign_mat(pad, mats["SM_Graphite"])
        parts.append(pad)

    # Join into single mesh for blockout export
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    obj = join_selected("SM_HAB1_HabitatModule")
    link_object(obj, col)
    set_origin_to_ground(obj)
    # Park HAB-1 along +Y for side-by-side with connector at origin
    obj.location = (0.0, 10.0, 0.0)
    return obj


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------

def export_assets(objects: list[bpy.types.Object]):
    EXPORT_DIR.mkdir(parents=True, exist_ok=True)
    for obj in objects:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        # FBX for Unity
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
        print(f"[SM] Exported {fbx}")
        # GLB optional
        glb = EXPORT_DIR / f"{obj.name}.glb"
        try:
            bpy.ops.export_scene.gltf(
                filepath=str(glb),
                use_selection=True,
                export_format="GLB",
                export_apply=True,
            )
            print(f"[SM] Exported {glb}")
        except Exception as e:
            print(f"[SM] GLB export skipped: {e}")


def add_camera_and_light():
    bpy.ops.object.camera_add(location=(18, -22, 14), rotation=(math.radians(60), 0, math.radians(35)))
    cam = bpy.context.active_object
    cam.name = "SM_WorkCamera"
    bpy.context.scene.camera = cam
    bpy.ops.object.light_add(type="SUN", location=(5, -8, 20))
    sun = bpy.context.active_object
    sun.name = "SM_Sun"
    sun.data.energy = 3.0
    bpy.ops.object.light_add(type="AREA", location=(-6, -4, 10))
    fill = bpy.context.active_object
    fill.name = "SM_Fill"
    fill.data.energy = 200.0
    fill.scale = (8, 8, 1)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    print("[SM] === Solar Majesty Blender setup ===")
    reset_scene()
    mats = create_palette()

    # Collections
    root = ensure_collection("SM_ROOT")
    col_ref = ensure_collection("01_References", root)
    col_kit = ensure_collection("02_ModularKit", root)
    col_hab = ensure_collection("03_HAB1", root)
    col_lab = ensure_collection("04_LAB1_WIP", root)
    col_cmd = ensure_collection("05_CMD_OPS_WIP", root)
    col_dome = ensure_collection("06_CommandDome_WIP", root)
    col_pwr = ensure_collection("07_PWR1_WIP", root)
    col_pad = ensure_collection("08_LandingPad_WIP", root)
    col_export = ensure_collection("09_ExportReady", root)
    # Hide empty WIP collections from clutter (still exist for workflow)
    for c in (col_lab, col_cmd, col_dome, col_pwr, col_pad):
        c.hide_viewport = False

    found, missing = setup_references(col_ref)
    add_camera_and_light()

    connector = build_modular_tube_connector(mats, col_kit)
    hab1 = build_hab1_blockout(mats, col_hab)

    # Duplicate to ExportReady (instances linked)
    for obj in (connector, hab1):
        link_object(obj, col_export)

    BLENDER_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))
    print(f"[SM] Saved {BLEND_OUT}")

    export_assets([connector, hab1])

    print("[SM] === Done ===")
    print(f"[SM] Open: {BLEND_OUT}")
    print(f"[SM] Exports: {EXPORT_DIR}")
    if missing:
        print(f"[SM] Optional missing refs (add to ConceptSheets/): {missing}")


if __name__ == "__main__":
    main()
