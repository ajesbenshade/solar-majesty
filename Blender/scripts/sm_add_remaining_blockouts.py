"""
Solar Majesty — add remaining modular building blockouts to the EXISTING blend.

Does NOT modify:
  SM_ModularTubeConnector, SM_HAB1_HabitatModule, SM_LAB1_LaboratoryModule

Run:
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    /Users/aaronesbenshade/solar-conquest/Blender/SolarMajesty_Modules.blend \
    --python .../sm_add_remaining_blockouts.py
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector

BLENDER_DIR = Path(__file__).resolve().parent.parent
BLEND_OUT = BLENDER_DIR / "SolarMajesty_Modules.blend"
EXPORT_DIR = BLENDER_DIR / "exports"

PROTECTED = {
    "SM_ModularTubeConnector",
    "SM_HAB1_HabitatModule",
    "SM_LAB1_LaboratoryModule",
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

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
    # Fallback minimal Principled if palette missing
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


def add_uv_sphere(name, radius, location, segments=24):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=max(12, segments // 2),
        radius=radius, location=location,
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
    """Remove only non-protected objects when re-running this script."""
    if name in PROTECTED:
        return
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


# ---------------------------------------------------------------------------
# Builders
# ---------------------------------------------------------------------------

def build_cmd1() -> bpy.types.Object:
    """CMD-1: boxy modular command ~9 m footprint, low height."""
    remove_if_exists("SM_CMD1_CommandBuilding")
    parts = []
    # Main white shell ~9 x 9 x 3.5
    body = add_cube("TMP", 1.0, (0, 0, 2.0), scale=(9.0, 9.0, 3.5))
    assign_mat(body, get_mat("SM_White"))
    parts.append(body)
    # Black base plinth
    base = add_cube("TMP", 1.0, (0, 0, 0.35), scale=(10.0, 10.0, 0.7))
    assign_mat(base, get_mat("SM_Black"))
    parts.append(base)
    # Upper inset tier
    top = add_cube("TMP", 1.0, (0, 0, 4.0), scale=(6.5, 6.5, 1.2))
    assign_mat(top, get_mat("SM_White"))
    parts.append(top)
    # Black structural belt
    belt = add_cube("TMP", 1.0, (0, 0, 3.4), scale=(9.2, 9.2, 0.4))
    assign_mat(belt, get_mat("SM_Black"))
    parts.append(belt)
    # Front stairs
    stairs = add_cube("TMP", 1.0, (0, -5.2, 0.5), scale=(3.0, 1.8, 1.0))
    assign_mat(stairs, get_mat("SM_Graphite"))
    parts.append(stairs)
    # Door
    door = add_cube("TMP", 1.0, (0, -4.6, 1.6), scale=(1.6, 0.2, 2.2))
    assign_mat(door, get_mat("SM_Black"))
    parts.append(door)
    # Orange accents
    for x in (-3.5, 3.5):
        strip = add_cube("TMP", 1.0, (x, -4.55, 2.5), scale=(0.8, 0.15, 0.25))
        assign_mat(strip, get_mat("SM_Orange"))
        parts.append(strip)
    # Top cupola
    cup = add_cylinder("TMP", 1.2, 0.8, (0, 0, 4.9))
    assign_mat(cup, get_mat("SM_Black"))
    parts.append(cup)
    # Antenna stubs
    for x, y in ((2.5, 2.5), (-2.5, 2.5)):
        ant = add_cylinder("TMP", 0.08, 2.0, (x, y, 5.5))
        assign_mat(ant, get_mat("SM_Steel"))
        parts.append(ant)
    # Side docking stub (for modular connector)
    dock = add_cylinder("TMP", 1.2, 1.5, (5.5, 0, 2.2), rotation=(0, math.radians(90), 0))
    assign_mat(dock, get_mat("SM_White"))
    parts.append(dock)
    ring = add_cylinder("TMP", 1.35, 0.2, (6.3, 0, 2.2), rotation=(0, math.radians(90), 0))
    assign_mat(ring, get_mat("SM_Orange"))
    parts.append(ring)

    obj = join_parts(parts, "SM_CMD1_CommandBuilding")
    return finalize(obj, ensure_collection("05_CMD_OPS"), (20.0, 0.0, 0.0))


def build_ops1() -> bpy.types.Object:
    """OPS-1: similar boxy unit, slightly smaller ~7.5 m footprint."""
    remove_if_exists("SM_OPS1_OperationsUnit")
    parts = []
    body = add_cube("TMP", 1.0, (0, 0, 1.7), scale=(7.5, 7.5, 3.0))
    assign_mat(body, get_mat("SM_White"))
    parts.append(body)
    base = add_cube("TMP", 1.0, (0, 0, 0.3), scale=(8.5, 8.5, 0.6))
    assign_mat(base, get_mat("SM_Black"))
    parts.append(base)
    top = add_cube("TMP", 1.0, (0, 0, 3.5), scale=(5.5, 5.5, 1.0))
    assign_mat(top, get_mat("SM_White"))
    parts.append(top)
    belt = add_cube("TMP", 1.0, (0, 0, 2.9), scale=(7.7, 7.7, 0.35))
    assign_mat(belt, get_mat("SM_Black"))
    parts.append(belt)
    stairs = add_cube("TMP", 1.0, (0, -4.4, 0.4), scale=(2.5, 1.5, 0.8))
    assign_mat(stairs, get_mat("SM_Graphite"))
    parts.append(stairs)
    door = add_cube("TMP", 1.0, (0, -3.85, 1.4), scale=(1.4, 0.2, 1.8))
    assign_mat(door, get_mat("SM_Black"))
    parts.append(door)
    for x in (-2.8, 2.8):
        strip = add_cube("TMP", 1.0, (x, -3.8, 2.2), scale=(0.7, 0.12, 0.2))
        assign_mat(strip, get_mat("SM_Orange"))
        parts.append(strip)
    # Roof equipment
    equip = add_cube("TMP", 1.0, (0, 1.0, 4.2), scale=(3.0, 2.0, 0.5))
    assign_mat(equip, get_mat("SM_Graphite"))
    parts.append(equip)
    ant = add_cylinder("TMP", 0.07, 1.8, (2.0, 2.0, 4.8))
    assign_mat(ant, get_mat("SM_Steel"))
    parts.append(ant)
    # Dock stub
    dock = add_cylinder("TMP", 1.15, 1.4, (-4.6, 0, 1.9), rotation=(0, math.radians(90), 0))
    assign_mat(dock, get_mat("SM_White"))
    parts.append(dock)
    ring = add_cylinder("TMP", 1.3, 0.18, (-5.4, 0, 1.9), rotation=(0, math.radians(90), 0))
    assign_mat(ring, get_mat("SM_Orange"))
    parts.append(ring)

    obj = join_parts(parts, "SM_OPS1_OperationsUnit")
    return finalize(obj, ensure_collection("05_CMD_OPS"), (20.0, 14.0, 0.0))


def build_command_dome() -> bpy.types.Object:
    """Central dome hub ~14 m diameter + radial tube stubs."""
    remove_if_exists("SM_CommandDome_CentralHub")
    parts = []
    dome_r = 7.0  # 14 m diameter
    # Flattened dome: UV sphere scaled on Z
    dome = add_uv_sphere("TMP", dome_r, (0, 0, 1.5), segments=32)
    dome.scale = (1.0, 1.0, 0.55)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign_mat(dome, get_mat("SM_White"))
    parts.append(dome)
    # Black ring / collar mid-dome
    ring = add_cylinder("TMP", dome_r * 1.02, 0.6, (0, 0, 3.5), vertices=48)
    assign_mat(ring, get_mat("SM_Black"))
    parts.append(ring)
    # Orange accent ring
    o_ring = add_cylinder("TMP", dome_r * 1.04, 0.15, (0, 0, 4.0), vertices=48)
    assign_mat(o_ring, get_mat("SM_Orange"))
    parts.append(o_ring)
    # Apex cupola
    cup = add_cylinder("TMP", 2.2, 1.2, (0, 0, 6.5))
    assign_mat(cup, get_mat("SM_Black"))
    parts.append(cup)
    cup_top = add_cylinder("TMP", 1.4, 0.5, (0, 0, 7.3))
    assign_mat(cup_top, get_mat("SM_White"))
    parts.append(cup_top)
    # Base plinth
    plinth = add_cylinder("TMP", dome_r * 1.15, 1.0, (0, 0, 0.5), vertices=48)
    assign_mat(plinth, get_mat("SM_Black"))
    parts.append(plinth)
    # Radial tube stubs (8)
    n_stubs = 8
    stub_len = 4.0
    stub_r = 1.15
    for i in range(n_stubs):
        ang = (2.0 * math.pi * i) / n_stubs
        # Outer end of stub
        dist = dome_r * 0.75 + stub_len * 0.5
        x = math.cos(ang) * dist
        y = math.sin(ang) * dist
        z = 2.2
        rot_z = ang
        stub = add_cylinder(
            "TMP", stub_r, stub_len,
            (x, y, z),
            rotation=(math.radians(90), 0, rot_z),
            vertices=24,
        )
        assign_mat(stub, get_mat("SM_White"))
        parts.append(stub)
        # Orange tip ring at outer end
        tip_dist = dome_r * 0.75 + stub_len - 0.15
        tx = math.cos(ang) * tip_dist
        ty = math.sin(ang) * tip_dist
        tip = add_cylinder(
            "TMP", stub_r * 1.12, 0.2,
            (tx, ty, z),
            rotation=(math.radians(90), 0, rot_z),
            vertices=24,
        )
        assign_mat(tip, get_mat("SM_Orange"))
        parts.append(tip)
        # Small foot under outer end
        fx = math.cos(ang) * (tip_dist - 0.3)
        fy = math.sin(ang) * (tip_dist - 0.3)
        foot = add_cube("TMP", 0.4, (fx, fy, 0.25), scale=(1.0, 1.0, 0.6))
        assign_mat(foot, get_mat("SM_Graphite"))
        parts.append(foot)

    obj = join_parts(parts, "SM_CommandDome_CentralHub")
    return finalize(obj, ensure_collection("06_CommandDome"), (45.0, 0.0, 0.0))


def build_pwr1() -> tuple[bpy.types.Object, bpy.types.Object]:
    """PWR-1 compact power building + separate solar array plane."""
    remove_if_exists("SM_PWR1_PowerNode")
    remove_if_exists("SM_PWR1_SolarArray")
    col = ensure_collection("07_PWR1")
    parts = []
    # Main volume ~7 m
    body = add_cube("TMP", 1.0, (0, 0, 2.0), scale=(7.0, 7.0, 3.8))
    assign_mat(body, get_mat("SM_White"))
    parts.append(body)
    base = add_cube("TMP", 1.0, (0, 0, 0.3), scale=(8.0, 8.0, 0.6))
    assign_mat(base, get_mat("SM_Black"))
    parts.append(base)
    # Upper tower
    tower = add_cube("TMP", 1.0, (0, 0, 4.5), scale=(3.5, 3.5, 1.8))
    assign_mat(tower, get_mat("SM_White"))
    parts.append(tower)
    cap = add_cylinder("TMP", 1.3, 0.8, (0, 0, 5.8))
    assign_mat(cap, get_mat("SM_Black"))
    parts.append(cap)
    cap_o = add_cylinder("TMP", 1.4, 0.12, (0, 0, 6.25))
    assign_mat(cap_o, get_mat("SM_Orange"))
    parts.append(cap_o)
    # Orange vertical stripe
    stripe = add_cube("TMP", 1.0, (0, -3.55, 2.5), scale=(0.25, 0.15, 2.5))
    assign_mat(stripe, get_mat("SM_Orange"))
    parts.append(stripe)
    # Door
    door = add_cube("TMP", 1.0, (0, -3.6, 1.5), scale=(1.8, 0.2, 2.0))
    assign_mat(door, get_mat("SM_Black"))
    parts.append(door)
    # Side vents (black)
    for x in (-2.5, 2.5):
        vent = add_cube("TMP", 1.0, (x, 0, 3.5), scale=(1.2, 1.5, 0.8))
        assign_mat(vent, get_mat("SM_Graphite"))
        parts.append(vent)
    # Ramp
    ramp = add_cube("TMP", 1.0, (0, -4.6, 0.35), scale=(2.5, 1.6, 0.7))
    assign_mat(ramp, get_mat("SM_Graphite"))
    parts.append(ramp)

    node = join_parts(parts, "SM_PWR1_PowerNode")
    finalize(node, col, (20.0, -18.0, 0.0))

    # Solar array — flat plane ~10 x 12 m, slightly elevated
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0, 0, 0.15))
    array = bpy.context.active_object
    array.name = "SM_PWR1_SolarArray"
    array.scale = (10.0, 12.0, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # Dark panel material — reuse graphite/black
    assign_mat(array, get_mat("SM_Graphite"))
    # Frame rim via thin cubes joined... keep separate simple plane
    # Orange edge markers
    edges = []
    for sx, sy, sc in (
        (0, 6.1, (10.2, 0.2, 0.08)),
        (0, -6.1, (10.2, 0.2, 0.08)),
        (5.1, 0, (0.2, 12.2, 0.08)),
        (-5.1, 0, (0.2, 12.2, 0.08)),
    ):
        e = add_cube("TMP", 1.0, (sx, sy, 0.2), scale=sc)
        assign_mat(e, get_mat("SM_Orange"))
        edges.append(e)
    # Join array with edges
    bpy.ops.object.select_all(action="DESELECT")
    array.select_set(True)
    for e in edges:
        e.select_set(True)
    bpy.context.view_layer.objects.active = array
    bpy.ops.object.join()
    array = bpy.context.active_object
    array.name = "SM_PWR1_SolarArray"
    finalize(array, col, (28.0, -18.0, 0.0))

    return node, array


def build_landing_pad() -> tuple[bpy.types.Object, bpy.types.Object]:
    """40 m diameter pad + simple Starship placeholder."""
    remove_if_exists("SM_LandingPad")
    remove_if_exists("SM_Starship_Placeholder")
    col = ensure_collection("08_LandingPad")
    parts = []
    # Main disc Ø40 m
    pad = add_cylinder("TMP", 20.0, 0.6, (0, 0, 0.3), vertices=64)
    assign_mat(pad, get_mat("SM_Graphite"))
    parts.append(pad)
    # Concentric rings (orange) — thin cylinders slightly above
    for r, d in ((18.0, 0.8), (12.0, 0.6), (6.0, 0.5)):
        ring = add_cylinder("TMP", r, 0.12, (0, 0, 0.65), vertices=64)
        # Make ring as thin annulus by... just solid rings as markers is fine for blockout
        assign_mat(ring, get_mat("SM_Orange"))
        # Scale down thickness visually: smaller depth already
        parts.append(ring)
    # Center pad
    center = add_cylinder("TMP", 3.0, 0.2, (0, 0, 0.7), vertices=32)
    assign_mat(center, get_mat("SM_Black"))
    parts.append(center)
    # Perimeter lip
    lip = add_cylinder("TMP", 20.5, 1.2, (0, 0, 0.4), vertices=64)
    assign_mat(lip, get_mat("SM_Black"))
    # Don't join lip as solid cylinder fills pad — use as outer only by making it a thin wall
    # For blockout: skip solid lip, use 8 rim boxes
    bpy.data.objects.remove(lip, do_unlink=True)
    for i in range(12):
        ang = (2.0 * math.pi * i) / 12
        x = math.cos(ang) * 19.5
        y = math.sin(ang) * 19.5
        block = add_cube("TMP", 1.0, (x, y, 0.8), scale=(2.0, 1.2, 1.0))
        assign_mat(block, get_mat("SM_Black"))
        parts.append(block)

    pad_obj = join_parts(parts, "SM_LandingPad")
    finalize(pad_obj, col, (-50.0, 0.0, 0.0))

    # Starship placeholder — tall thin stack ~50 m (readable, not full 122 m game-scale)
    # Sheet says 122 m; for RTS readability use ~45 m placeholder
    sparts = []
    body = add_cylinder("TMP", 2.2, 40.0, (0, 0, 22.0), vertices=24)
    assign_mat(body, get_mat("SM_White"))
    sparts.append(body)
    # Black bands
    for z in (12.0, 22.0, 32.0):
        band = add_cylinder("TMP", 2.3, 2.0, (0, 0, z), vertices=24)
        assign_mat(band, get_mat("SM_Black"))
        sparts.append(band)
    # Nose
    nose = add_uv_sphere("TMP", 2.2, (0, 0, 43.0), segments=16)
    nose.scale = (1.0, 1.0, 1.4)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign_mat(nose, get_mat("SM_White"))
    sparts.append(nose)
    # Fins (simple boxes)
    for y in (-2.8, 2.8):
        fin = add_cube("TMP", 1.0, (0, y, 8.0), scale=(0.3, 1.5, 4.0))
        assign_mat(fin, get_mat("SM_Black"))
        sparts.append(fin)
    # Orange stripe
    stripe = add_cube("TMP", 1.0, (2.15, 0, 25.0), scale=(0.15, 0.8, 6.0))
    assign_mat(stripe, get_mat("SM_Orange"))
    sparts.append(stripe)

    ship = join_parts(sparts, "SM_Starship_Placeholder")
    finalize(ship, col, (-50.0, 0.0, 0.0))

    return pad_obj, ship


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------

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
        print(f"[SM] GLB skip {obj.name}: {e}")


def dims_report(obj: bpy.types.Object) -> str:
    bpy.context.view_layer.update()
    mat = obj.matrix_world
    corners = [mat @ Vector(c) for c in obj.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return (
        f"{obj.name}: "
        f"W={max(xs)-min(xs):.1f}m  D={max(ys)-min(ys):.1f}m  H={max(zs)-min(zs):.1f}m  "
        f"loc=({obj.location.x:.0f}, {obj.location.y:.0f}, {obj.location.z:.0f})"
    )


def main():
    print("[SM] === Add remaining blockouts (preserve HAB/LAB/Connector) ===")

    # Rename legacy WIP collections if present (non-destructive)
    for old, new in (
        ("05_CMD_OPS_WIP", "05_CMD_OPS"),
        ("06_CommandDome_WIP", "06_CommandDome"),
        ("07_PWR1_WIP", "07_PWR1"),
        ("08_LandingPad_WIP", "08_LandingPad"),
    ):
        c = bpy.data.collections.get(old)
        if c and bpy.data.collections.get(new) is None:
            c.name = new

    created = []
    created.append(build_cmd1())
    created.append(build_ops1())
    created.append(build_command_dome())
    node, array = build_pwr1()
    created.extend([node, array])
    pad, ship = build_landing_pad()
    created.extend([pad, ship])

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))
    print(f"[SM] Saved {BLEND_OUT}")

    print("\n[SM] === FINAL OBJECT LIST ===")
    for obj in created:
        print(" ", dims_report(obj))
        export_one(obj)

    # Also list protected existing
    print("\n[SM] === PROTECTED (unchanged) ===")
    for name in PROTECTED:
        o = bpy.data.objects.get(name)
        if o:
            print(" ", dims_report(o))
        else:
            print(f"  {name}: (not in file)")

    print("[SM] === Done ===")


if __name__ == "__main__":
    main()
