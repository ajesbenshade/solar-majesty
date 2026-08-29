"""
Solar Majesty — import Copilot 3D GLB, scale to RTS dimensions, apply SM palette stubs, export FBX.

Manual cleanup in Blender GUI is expected before shipping. This script handles repetitive
import / scale / origin / export boilerplate.

Run:
  blender --background --python Blender/scripts/sm_import_copilot3d.py -- \\
    --name SM_Unit_CourierBot --target-height 1.45 --category units

Optional:
  --target-width 2.0 --target-depth 1.0 --skip-materials
"""

from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
BLENDER_DIR = SCRIPT_DIR.parent
PROJECT_ROOT = BLENDER_DIR.parent
IMPORT_DIR = BLENDER_DIR / "imports" / "copilot3d"
EXPORT_DIR = BLENDER_DIR / "exports"

UNITY_DEST = {
    "units": PROJECT_ROOT / "Assets" / "Resources" / "Units",
    "buildings": PROJECT_ROOT / "Assets" / "Resources" / "Buildings",
    "environment": PROJECT_ROOT / "Assets" / "Resources" / "Environment",
}


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
        "SM_Glass": make_principled("SM_Glass", (0.55, 0.62, 0.70), 0.00, 0.08),
        "SM_Cyan": make_principled("SM_Cyan", (0.25, 0.75, 0.95), 0.05, 0.28),
    }


def bounds_xyz(obj: bpy.types.Object) -> tuple[float, float, float]:
    bpy.context.view_layer.update()
    mat = obj.matrix_world
    corners = [mat @ Vector(c) for c in obj.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)


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


def import_glb(path: Path) -> bpy.types.Object:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    if not imported:
        raise RuntimeError(f"No mesh objects imported from {path}")
    if len(imported) == 1:
        return imported[0]
    bpy.ops.object.select_all(action="DESELECT")
    for o in imported:
        o.select_set(True)
    bpy.context.view_layer.objects.active = imported[0]
    bpy.ops.object.join()
    return bpy.context.active_object


def scale_to_targets(obj: bpy.types.Object, height: float | None, width: float | None, depth: float | None):
    """Scale Copilot meshes for RTS.

    Flat monocular GLBs (H << W) explode if we uniform-scale by height first.
    Prefer: uniform from max horizontal span -> optional W/D tweak -> Z stretch to height.
    """
    w, d, h = bounds_xyz(obj)

    if width or depth:
        target_span = max(width or 0.0, depth or 0.0)
        cur_span = max(w, d)
        if cur_span > 1e-6 and target_span > 0.0:
            u = target_span / cur_span
            obj.scale = (u, u, u)
            bpy.ops.object.transform_apply(scale=True)
            w, d, h = bounds_xyz(obj)
        stretch = Vector((1.0, 1.0, 1.0))
        if width and w > 1e-6:
            stretch.x = width / w
        if depth and d > 1e-6:
            stretch.y = depth / d
        if abs(stretch.x - 1.0) > 1e-4 or abs(stretch.y - 1.0) > 1e-4:
            obj.scale = stretch
            bpy.ops.object.transform_apply(scale=True)
            w, d, h = bounds_xyz(obj)
        if height and h > 1e-6:
            obj.scale = (1.0, 1.0, height / h)
            bpy.ops.object.transform_apply(scale=True)
    elif height and h > 1e-6:
        u = height / h
        obj.scale = (u, u, u)
        bpy.ops.object.transform_apply(scale=True)


def apply_sm_stub_material(obj: bpy.types.Object, mat: bpy.types.Material):
    if obj.data and hasattr(obj.data, "materials"):
        obj.data.materials.clear()
        obj.data.materials.append(mat)


def export_one(obj: bpy.types.Object):
    EXPORT_DIR.mkdir(parents=True, exist_ok=True)
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
    obj.location = loc
    return fbx


def copy_to_unity(name: str, category: str):
    dest_root = UNITY_DEST.get(category)
    if dest_root is None:
        raise ValueError(f"Unknown category {category!r}; use units|buildings|environment")
    dest_root.mkdir(parents=True, exist_ok=True)
    src = EXPORT_DIR / f"{name}.fbx"
    if not src.is_file():
        print(f"[SM] Missing export {src}")
        return
    dst = dest_root / f"{name}.fbx"
    shutil.copy2(src, dst)
    print(f"[SM] Copied -> {dst.relative_to(PROJECT_ROOT)}")


def dims_report(obj: bpy.types.Object) -> str:
    w, d, h = bounds_xyz(obj)
    return f"{obj.name}: W={w:.2f}m  D={d:.2f}m  H={h:.2f}m"


def parse_args() -> argparse.Namespace:
    if "--" not in sys.argv:
        print("Usage: blender --background --python sm_import_copilot3d.py -- --name SM_Unit_CourierBot ...")
        sys.exit(1)
    raw = sys.argv[sys.argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(description="Import Copilot 3D GLB for Solar Majesty")
    parser.add_argument("--name", required=True, help="Object and FBX name (e.g. SM_Unit_CourierBot)")
    parser.add_argument("--target-height", type=float, default=None, help="Target height in meters")
    parser.add_argument("--target-width", type=float, default=None, help="Target width (X) in meters")
    parser.add_argument("--target-depth", type=float, default=None, help="Target depth (Y) in meters")
    parser.add_argument(
        "--category",
        choices=sorted(UNITY_DEST.keys()),
        default="units",
        help="Unity Resources subfolder",
    )
    parser.add_argument(
        "--glb",
        type=Path,
        default=None,
        help="Override GLB path (default: Blender/imports/copilot3d/{name}.glb)",
    )
    parser.add_argument(
        "--skip-materials",
        action="store_true",
        help="Keep imported materials instead of SM_White stub",
    )
    parser.add_argument("--no-export", action="store_true", help="Import and scale only; skip FBX copy")
    return parser.parse_args(raw)


def main():
    args = parse_args()
    glb_path = args.glb or (IMPORT_DIR / f"{args.name}.glb")
    if not glb_path.is_file():
        print(f"[SM] GLB not found: {glb_path}")
        sys.exit(1)

    print(f"[SM] === Copilot 3D import: {args.name} ===")
    reset_scene()

    obj = import_glb(glb_path)
    obj.name = args.name
    print(f"[SM] Imported {glb_path.name} -> {dims_report(obj)}")

    scale_to_targets(obj, args.target_height, args.target_width, args.target_depth)
    set_origin_to_ground(obj)
    print(f"[SM] Scaled -> {dims_report(obj)}")

    if not args.skip_materials:
        palette = create_palette()
        stub = palette["SM_White"]
        lower = args.name.lower()
        if "icewisp" in lower or "wisp" in lower:
            stub = palette.get("SM_Glass") or stub
            # Prefer cyan-tinted ice if present
            ice = make_principled("SM_Ice", (0.78, 0.92, 0.98), 0.02, 0.14)
            stub = ice
        elif "mite" in lower:
            stub = make_principled("SM_DustBrown", (0.62, 0.48, 0.32), 0.06, 0.64)
        elif "tick" in lower:
            stub = palette["SM_Graphite"]
        elif "tree" in lower:
            stub = make_principled("SM_Leaf", (0.22, 0.42, 0.16), 0.05, 0.65)
        elif "rock" in lower or "boulder" in lower:
            stub = make_principled("SM_Rock", (0.55, 0.32, 0.18), 0.15, 0.75)
        elif "dune" in lower:
            stub = make_principled("SM_Dune", (0.72, 0.42, 0.22), 0.05, 0.85)
        elif "crater" in lower:
            stub = make_principled("SM_CraterRim", (0.58, 0.30, 0.14), 0.12, 0.7)
        apply_sm_stub_material(obj, stub)
        print(f"[SM] Applied {stub.name} material stub (remap in GUI before shipping)")

    blend_out = BLENDER_DIR / f"SolarMajesty_Copilot3D_{args.name}.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_out))
    print(f"[SM] Saved {blend_out.relative_to(PROJECT_ROOT)}")

    if not args.no_export:
        export_one(obj)
        copy_to_unity(args.name, args.category)

    print("[SM] === Done ===")


if __name__ == "__main__":
    main()
