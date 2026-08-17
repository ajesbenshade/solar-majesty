"""
Solar Majesty — Phase 4 hero building kits as FBX.

Sheet-matched HAB / Colony Commons / LAB / Power / landing pad against
ConceptSheets (HAB-1 cylinder, command dome citadel, LAB-1 cylinder,
PWR-1 + solar field, pad + Starship stack). Guild Hall is CMD-1 civic
dress; Mining is OPS-1 annex. HAB / Commons / LAB / CMD-1 / OPS-1 carry
bevelled panel lines (carbon rings, spine seams, civic wrap bands).
Farm / Camp / Mine and wonders use distinct industrial silhouettes.
Cardinal square airlocks stay in Unity and mate flush at the footprint face.

Run:
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    --python Blender/scripts/sm_hero_building_kits.py
"""

from __future__ import annotations

import math
import shutil
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
BLENDER_DIR = SCRIPT_DIR.parent
PROJECT_ROOT = BLENDER_DIR.parent
BLEND_OUT = BLENDER_DIR / "SolarMajesty_HeroBuildings.blend"
EXPORT_DIR = BLENDER_DIR / "exports"
UNITY_BUILDINGS = PROJECT_ROOT / "Assets" / "Resources" / "Buildings"

# Grid: 4-cell = 6 m, 6-cell = 9 m (ColonyLayout.DefaultCellSize 1.5).
FOOT_4 = 6.0
FOOT_6 = 9.0


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
    if "Emission Color" in bsdf.inputs and base[1] > 0.7 and base[2] > 0.8 and base[0] < 0.5:
        bsdf.inputs["Emission Color"].default_value = (*base, 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = 1.8
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def create_palette() -> dict:
    return {
        "SM_White": make_principled("SM_White", (0.85, 0.86, 0.88), 0.12, 0.42),
        "SM_Black": make_principled("SM_Black", (0.03, 0.03, 0.035), 0.35, 0.48),
        "SM_Graphite": make_principled("SM_Graphite", (0.12, 0.13, 0.14), 0.40, 0.40),
        "SM_Orange": make_principled("SM_Orange", (0.95, 0.38, 0.05), 0.08, 0.35),
        "SM_Steel": make_principled("SM_Steel", (0.45, 0.47, 0.50), 0.70, 0.32),
        "SM_Cyan": make_principled("SM_Cyan", (0.22, 0.84, 0.98), 0.05, 0.22),
        "SM_Yellow": make_principled("SM_Yellow", (0.95, 0.82, 0.12), 0.08, 0.38),
        "SM_Concrete": make_principled("SM_Concrete", (0.40, 0.41, 0.43), 0.08, 0.62),
        "SM_Ice": make_principled("SM_Ice", (0.52, 0.76, 0.86), 0.04, 0.28),
        "SM_Dust": make_principled("SM_Dust", (0.52, 0.36, 0.22), 0.10, 0.58),
        "SM_Solar": make_principled("SM_Solar", (0.07, 0.14, 0.36), 0.35, 0.28),
        "SM_Glass": make_principled("SM_Glass", (0.55, 0.62, 0.70), 0.00, 0.08),
        "SM_Plant": make_principled("SM_Plant", (0.18, 0.55, 0.22), 0.04, 0.58),
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


def add_box_u(name, loc, scale):
    """Unity Y-up cube (pos/scale) → Blender Z-up."""
    ux, uy, uz = loc
    sx, sy, sz = scale
    return add_cube(name, 1.0, (ux, uz, uy), scale=(sx, sz, sy))


def add_cyl_u(name, loc, scale, vertices=10):
    ux, uy, uz = loc
    sx, sy, sz = scale
    radius = 0.25 * (sx + sz)
    depth = 2.0 * sy
    return add_cylinder(name, radius, depth, (ux, uz, uy), vertices=vertices)


def add_sph_u(name, loc, scale, segments=10, rings=6):
    ux, uy, uz = loc
    sx, sy, sz = scale
    radius = 0.5 * max(sx, sy, sz)
    obj = add_uv_sphere(name, radius, (ux, uz, uy), segments=segments, rings=rings)
    if radius > 1e-6 and (abs(sx - sy) > 0.01 or abs(sy - sz) > 0.01):
        obj.scale = (sx / (2.0 * radius), sz / (2.0 * radius), sy / (2.0 * radius))
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def add_cone(name, radius1, depth, location, vertices=16):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices, radius1=radius1, radius2=0.0, depth=depth, location=location,
    )
    obj = bpy.context.active_object
    obj.name = name
    return obj


def add_cyl_x(name, radius, length, location, vertices=28):
    """Horizontal cylinder along Blender X (Unity X after FBX)."""
    return add_cylinder(name, radius, length, location, rotation=(0, math.pi / 2, 0), vertices=vertices)


def orient_along_xy(obj, ang):
    """Lay a Z-up cylinder onto the XY plane along angle (0 = +Y)."""
    obj.rotation_euler = (math.pi / 2, 0.0, ang)
    apply_rot(obj)


def apply_rot(obj: bpy.types.Object):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)


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


def finish(parts, name, collection, park):
    obj = join_parts(parts, name)
    col = ensure_collection(collection)
    link_object(obj, col)
    set_origin_to_ground(obj)
    obj.location = park
    return obj


def dock_collar(parts, mats, pos, outward):
    ns = abs(outward[1]) >= abs(outward[0])
    rot = (math.pi / 2, 0, 0) if ns else (0, 0, math.pi / 2)
    sleeve = add_cylinder("TMP", 0.48, 0.55, pos, rotation=rot, vertices=16)
    assign_mat(sleeve, mats["SM_White"])
    parts.append(sleeve)
    ox, oy = outward[0] * 0.32, outward[1] * 0.32
    ring = add_cylinder("TMP", 0.54, 0.08, (pos[0] + ox, pos[1] + oy, pos[2]), rotation=rot, vertices=16)
    assign_mat(ring, mats["SM_Orange"])
    parts.append(ring)


def scaffold_tower(parts, mats, at, height, span):
    ax, ay = at
    for dx, dy in ((-span, -span), (span, -span), (-span, span), (span, span)):
        post = add_cube("TMP", 1.0, (ax + dx, ay + dy, height * 0.5), scale=(0.08, 0.08, height))
        assign_mat(post, mats["SM_Black"])
        parts.append(post)
    beam = add_cube("TMP", 1.0, (ax, ay, height * 0.92), scale=(span * 2.1, span * 2.1, 0.08))
    assign_mat(beam, mats["SM_Yellow"])
    parts.append(beam)


def scaffold_low(parts, mats, at, width):
    ax, ay = at
    for i in range(3):
        x = ax - width * 0.4 + i * width * 0.4
        post = add_cube("TMP", 1.0, (x, ay, 1.15), scale=(0.07, 0.07, 2.2))
        assign_mat(post, mats["SM_Black"])
        parts.append(post)
    beam = add_cube("TMP", 1.0, (ax, ay, 2.2), scale=(width, 0.07, 0.07))
    assign_mat(beam, mats["SM_Yellow"])
    parts.append(beam)


def pressurized_dome(parts, mats, radius, drum_h, citadel: bool):
    y_drum = drum_h * 0.5 + 0.28
    plinth = add_cylinder("TMP", radius * 1.18, 0.16, (0, 0, 0.16), vertices=24)
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    ring = add_cylinder("TMP", radius * 1.10, 0.05, (0, 0, 0.28), vertices=24)
    assign_mat(ring, mats["SM_Orange"])
    parts.append(ring)
    drum = add_cylinder("TMP", radius, drum_h, (0, 0, y_drum), vertices=8)
    assign_mat(drum, mats["SM_White"])
    parts.append(drum)
    band = add_cylinder("TMP", radius * 1.06, 0.07, (0, 0, y_drum - drum_h * 0.08), vertices=16)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)
    stripe = add_cylinder(
        "TMP", radius * 1.08, 0.14 if citadel else 0.09,
        (0, 0, y_drum + drum_h * 0.08), vertices=16,
    )
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    if citadel:
        stripe2 = add_cylinder("TMP", radius * 1.07, 0.08, (0, 0, y_drum + drum_h * 0.28), vertices=16)
        assign_mat(stripe2, mats["SM_Orange"])
        parts.append(stripe2)
    dome_y = y_drum + drum_h * 0.42
    z_scale = 0.95 if citadel else 0.82
    dome = add_uv_sphere("TMP", radius * 0.98, (0, 0, dome_y), scale=(1.0, 1.0, z_scale), segments=20, rings=10)
    assign_mat(dome, mats["SM_White"])
    parts.append(dome)
    cap = add_cylinder("TMP", radius * 0.58, 0.07, (0, 0, dome_y + radius * (0.42 if citadel else 0.32)), vertices=16)
    assign_mat(cap, mats["SM_White"])
    parts.append(cap)
    cupola = add_cylinder(
        "TMP", radius * 0.21, 0.28 if citadel else 0.16,
        (0, 0, dome_y + radius * (0.55 if citadel else 0.42)), vertices=12,
    )
    assign_mat(cupola, mats["SM_Black"])
    parts.append(cupola)
    rows = 2 if citadel else 1
    for row in range(rows):
        wy = y_drum + (-drum_h * 0.12 if row == 0 else drum_h * 0.22)
        for i in range(8):
            if not citadel and i % 2 == 0:
                continue
            ang = i * (math.pi / 4.0)
            r = radius * 1.02
            visor = add_cube(
                "TMP", 1.0,
                (math.sin(ang) * r, math.cos(ang) * r, wy),
                scale=(radius * 0.42, 0.08, 0.22 if citadel else 0.18),
            )
            visor.rotation_euler = (0.0, 0.0, ang)
            apply_rot(visor)
            assign_mat(visor, mats["SM_Cyan"])
            parts.append(visor)
    return y_drum, dome_y


def turret(parts, mats, at, scale=1.0):
    x, y, z = at
    s = scale
    plinth = add_cylinder("TMP", 0.31 * s, 0.05 * s, (x, y, z + 0.05 * s), vertices=12)
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    ring = add_cylinder("TMP", 0.35 * s, 0.03 * s, (x, y, z + 0.12 * s), vertices=12)
    assign_mat(ring, mats["SM_Orange"])
    parts.append(ring)
    head = add_cube("TMP", 1.0, (x, y + 0.05 * s, z + 0.50 * s), scale=(0.44 * s, 0.36 * s, 0.24 * s))
    assign_mat(head, mats["SM_White"])
    parts.append(head)
    for sx in (-0.11 * s, 0.11 * s):
        barrel = add_cylinder(
            "TMP", 0.035 * s, 0.56 * s, (x + sx, y + 0.40 * s, z + 0.50 * s),
            rotation=(math.pi / 2, 0, 0), vertices=10,
        )
        assign_mat(barrel, mats["SM_Steel"])
        parts.append(barrel)
        lens = add_uv_sphere("TMP", 0.055 * s, (x + sx, y + 0.20 * s, z + 0.52 * s), segments=8, rings=6)
        assign_mat(lens, mats["SM_Cyan"])
        parts.append(lens)
    stripe = add_cube("TMP", 1.0, (x, y, z + 0.64 * s), scale=(0.28 * s, 0.08 * s, 0.04 * s))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)


def bevel_hull(obj, width=0.045, segments=2):
    """Fillet hard edges so white hulls catch isometric light (CMD/OPS boxes)."""
    if obj is None or obj.type != "MESH" or width <= 0:
        return obj
    me = obj.data
    bm = bmesh.new()
    try:
        bm.from_mesh(me)
        kwargs = dict(
            geom=list(bm.edges) + list(bm.verts),
            offset=width,
            offset_type="OFFSET",
            segments=max(1, int(segments)),
            profile=0.5,
            clamp_overlap=True,
        )
        try:
            bmesh.ops.bevel(bm, affect="EDGES", **kwargs)
        except TypeError:
            bmesh.ops.bevel(bm, **kwargs)
        bm.to_mesh(me)
        me.update()
    except Exception as e:
        print(f"[SM] bevel skip {obj.name}: {e}")
    finally:
        bm.free()
    return obj


def cyl_x_panel_lines(parts, mats, length, radius, z_axis, skip_y_sign=0.0):
    """Carbon rings + spine on a HAB/LAB X-cylinder. Stay inside existing bounds."""
    for x in (-length * 0.18, length * 0.18):
        ring = add_cyl_x("TMP", radius * 1.018, 0.032, (x, 0.0, z_axis), vertices=32)
        assign_mat(ring, mats["SM_Graphite"])
        parts.append(ring)
    spine = add_cube(
        "TMP", 1.0, (0.0, 0.0, z_axis + radius * 1.012),
        scale=(length * 0.58, 0.035, 0.028),
    )
    assign_mat(spine, mats["SM_Black"])
    parts.append(spine)
    for y_sign in (-1.0, 1.0):
        if skip_y_sign != 0.0 and y_sign == skip_y_sign:
            continue
        seam = add_cube(
            "TMP", 1.0,
            (0.0, y_sign * radius * 0.70, z_axis + radius * 0.50),
            scale=(length * 0.46, 0.028, 0.028),
        )
        assign_mat(seam, mats["SM_Graphite"])
        parts.append(seam)


def commons_panel_lines(parts, mats, radius):
    """Equatorial rings + drum meridians on the command dome (not a smooth blob)."""
    for z, r, key in (
        (0.88, radius * 1.015, "SM_Graphite"),
        (1.55, radius * 1.012, "SM_Black"),
        (2.22, radius * 0.94, "SM_Graphite"),
        (2.62, radius * 0.80, "SM_Black"),
        (2.92, radius * 0.64, "SM_Graphite"),
    ):
        ring = add_cylinder("TMP", r, 0.038, (0.0, 0.0, z), vertices=32)
        assign_mat(ring, mats[key])
        parts.append(ring)
    for i in range(8):
        ang = i * math.pi / 4.0
        px = math.sin(ang) * radius * 1.012
        py = math.cos(ang) * radius * 1.012
        for z, h in ((0.82, 0.38), (1.62, 0.28)):
            seam = add_cube("TMP", 1.0, (px, py, z), scale=(0.032, 0.032, h))
            seam.rotation_euler = (0.0, 0.0, ang)
            apply_rot(seam)
            assign_mat(seam, mats["SM_Black"])
            parts.append(seam)


# ---------------------------------------------------------------------------
# Kits
# ---------------------------------------------------------------------------

def build_hab(mats: dict) -> bpy.types.Object:
    """HAB-1 living module: horizontal cylinder on skids (sheet Ø8×L12 → 4×4 / 6 m)."""
    name = "SM_Hero_HAB"
    remove_if_exists(name)
    parts = []
    length = FOOT_4 * 0.92
    radius = length / 3.0
    z_axis = radius + 0.22

    shell = add_cyl_x("TMP", radius, length * 0.72, (0, 0, z_axis), vertices=32)
    assign_mat(shell, mats["SM_White"])
    parts.append(shell)
    mid = add_cyl_x("TMP", radius * 1.03, 0.72, (0, 0, z_axis), vertices=32)
    assign_mat(mid, mats["SM_Black"])
    parts.append(mid)

    for sign in (-1.0, 1.0):
        x = sign * (length * 0.36)
        cap = add_cyl_x("TMP", radius * 0.99, 0.78, (x, 0, z_axis), vertices=32)
        assign_mat(cap, mats["SM_Black"])
        parts.append(cap)
        ring = add_cyl_x("TMP", radius * 1.05, 0.10, (x + sign * 0.38, 0, z_axis), vertices=32)
        assign_mat(ring, mats["SM_Orange"])
        parts.append(ring)
        dock = add_cyl_x("TMP", 0.62, 0.42, (sign * (length * 0.50), 0, z_axis), vertices=20)
        assign_mat(dock, mats["SM_Graphite"])
        parts.append(dock)
        dock_o = add_cyl_x("TMP", 0.70, 0.07, (sign * (length * 0.52), 0, z_axis), vertices=20)
        assign_mat(dock_o, mats["SM_Orange"])
        parts.append(dock_o)

    front = add_cyl_x("TMP", 0.78, 0.08, (-length * 0.50, 0, z_axis), vertices=20)
    assign_mat(front, mats["SM_Steel"])
    parts.append(front)
    sq = add_cube("TMP", 1.0, (-length * 0.54, 0, z_axis), scale=(0.10, 0.55, 0.55))
    assign_mat(sq, mats["SM_White"])
    bevel_hull(sq, 0.018, 1)
    parts.append(sq)
    rear_f = add_cube("TMP", 1.0, (length * 0.50, 0, z_axis), scale=(0.06, 0.88, 1.32))
    assign_mat(rear_f, mats["SM_Black"])
    parts.append(rear_f)
    rear = add_cube("TMP", 1.0, (length * 0.48, 0, z_axis), scale=(0.10, 0.72, 1.15))
    assign_mat(rear, mats["SM_Orange"])
    parts.append(rear)

    door_f = add_cube("TMP", 1.0, (0.12, -radius * 1.02, z_axis), scale=(1.05, 0.08, 1.35))
    assign_mat(door_f, mats["SM_Black"])
    parts.append(door_f)
    door = add_cube("TMP", 1.0, (0.12, -radius * 0.96, z_axis), scale=(0.85, 0.12, 1.15))
    assign_mat(door, mats["SM_Orange"])
    parts.append(door)

    box_a = add_cube("TMP", 1.0, (-0.85, 0.05, z_axis + radius * 0.82), scale=(1.05, 0.62, 0.38))
    assign_mat(box_a, mats["SM_Graphite"])
    parts.append(box_a)
    box_b = add_cube("TMP", 1.0, (0.55, -0.08, z_axis + radius * 0.78), scale=(0.72, 0.48, 0.28))
    assign_mat(box_b, mats["SM_White"])
    parts.append(box_b)
    box_cap = add_cube("TMP", 1.0, (0.55, -0.08, z_axis + radius * 0.96), scale=(0.55, 0.36, 0.10))
    assign_mat(box_cap, mats["SM_Black"])
    parts.append(box_cap)
    ant = add_cylinder("TMP", 0.03, 0.7, (-0.85, 0.2, z_axis + radius * 1.15), vertices=8)
    assign_mat(ant, mats["SM_Steel"])
    parts.append(ant)

    for x in (-1.35, 1.15):
        vis = add_cube("TMP", 1.0, (x, radius * 0.92, z_axis + 0.12), scale=(0.55, 0.06, 0.22))
        assign_mat(vis, mats["SM_Cyan"])
        parts.append(vis)

    cyl_x_panel_lines(parts, mats, length, radius, z_axis, skip_y_sign=-1.0)

    for x, y in ((-1.85, -1.15), (-1.85, 1.15), (1.85, -1.15), (1.85, 1.15)):
        leg = add_cube("TMP", 1.0, (x, y, 0.42), scale=(0.55, 0.38, 0.72))
        assign_mat(leg, mats["SM_Black"])
        parts.append(leg)
        pad = add_cube("TMP", 1.0, (x, y, 0.10), scale=(0.82, 0.58, 0.16))
        assign_mat(pad, mats["SM_Graphite"])
        parts.append(pad)
    return finish(parts, name, "30_Hero_HAB", (0.0, 0.0, 0.0))


def build_commons(mats: dict) -> bpy.types.Object:
    """Command-dome civic citadel — player-facing Colony Commons (6×6 / 9 m)."""
    name = "SM_Hero_Commons"
    remove_if_exists(name)
    parts = []
    radius = FOOT_6 * 0.38

    plinth = add_cylinder("TMP", radius * 1.22, 0.55, (0, 0, 0.28), vertices=32)
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    mech = add_cylinder("TMP", radius * 1.12, 0.22, (0, 0, 0.58), vertices=32)
    assign_mat(mech, mats["SM_Graphite"])
    parts.append(mech)
    for i in range(8):
        ang = i * math.pi / 4.0
        lamp = add_cube(
            "TMP", 1.0,
            (math.sin(ang) * radius * 1.14, math.cos(ang) * radius * 1.14, 0.52),
            scale=(0.14, 0.10, 0.08),
        )
        assign_mat(lamp, mats["SM_Orange"])
        parts.append(lamp)

    drum = add_cylinder("TMP", radius, 1.15, (0, 0, 1.15), vertices=32)
    assign_mat(drum, mats["SM_White"])
    bevel_hull(drum, 0.028, 1)
    parts.append(drum)
    band = add_cylinder("TMP", radius * 1.04, 0.14, (0, 0, 1.35), vertices=32)
    assign_mat(band, mats["SM_Black"])
    parts.append(band)
    stripe = add_cylinder("TMP", radius * 1.06, 0.10, (0, 0, 1.72), vertices=32)
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    dome = add_uv_sphere(
        "TMP", radius * 1.02, (0, 0, 1.85),
        scale=(1.0, 1.0, 0.72), segments=32, rings=14,
    )
    assign_mat(dome, mats["SM_White"])
    parts.append(dome)
    dband = add_cylinder("TMP", radius * 0.72, 0.10, (0, 0, 3.05), vertices=24)
    assign_mat(dband, mats["SM_Black"])
    parts.append(dband)

    cup_lo = add_cylinder("TMP", radius * 0.28, 0.42, (0, 0, 3.55), vertices=16)
    assign_mat(cup_lo, mats["SM_White"])
    parts.append(cup_lo)
    cup_band = add_cylinder("TMP", radius * 0.30, 0.08, (0, 0, 3.72), vertices=16)
    assign_mat(cup_band, mats["SM_Black"])
    parts.append(cup_band)
    cup_hi = add_cylinder("TMP", radius * 0.18, 0.32, (0, 0, 3.95), vertices=12)
    assign_mat(cup_hi, mats["SM_White"])
    parts.append(cup_hi)
    cup_cap = add_cylinder("TMP", radius * 0.20, 0.08, (0, 0, 4.14), vertices=12)
    assign_mat(cup_cap, mats["SM_Black"])
    parts.append(cup_cap)

    ant = add_cylinder("TMP", 0.045, 1.15, (0, 0, 4.75), vertices=8)
    assign_mat(ant, mats["SM_Steel"])
    parts.append(ant)
    dish = add_uv_sphere("TMP", 0.28, (0.42, 0, 5.15), scale=(1.0, 1.0, 0.28), segments=12, rings=6)
    assign_mat(dish, mats["SM_Graphite"])
    parts.append(dish)
    beacon = add_uv_sphere("TMP", 0.12, (0, 0, 5.45), segments=8, rings=6)
    assign_mat(beacon, mats["SM_Cyan"])
    parts.append(beacon)

    for i in range(8):
        if i % 2 == 0:
            continue
        ang = i * math.pi / 4.0
        vis = add_cube(
            "TMP", 1.0,
            (math.sin(ang) * radius * 1.02, math.cos(ang) * radius * 1.02, 1.35),
            scale=(radius * 0.38, 0.08, 0.22),
        )
        vis.rotation_euler = (0.0, 0.0, ang)
        apply_rot(vis)
        assign_mat(vis, mats["SM_Cyan"])
        parts.append(vis)

    # Radial stubs stay in Unity (DockSleeve + CampusDressing) so unused faces
    # can hide. Joined FBX stubs cannot be toggled per dock.

    commons_panel_lines(parts, mats, radius)
    return finish(parts, name, "31_Hero_Commons", (14.0, 0.0, 0.0))


def build_power(mats: dict) -> bpy.types.Object:
    """PWR-1 node + solar field (sheet) on the 4×4 / 6 m footprint."""
    name = "SM_Hero_Power"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4

    plinth = add_cube("TMP", 1.0, (0, 0, 0.08), scale=(w * 0.94, d * 0.94, 0.14))
    assign_mat(plinth, mats["SM_Graphite"])
    parts.append(plinth)
    stripe = add_cube("TMP", 1.0, (0, 0, 0.16), scale=(w * 0.14, d * 0.92, 0.03))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)

    ny = d * 0.22
    hull = add_cube("TMP", 1.0, (0, ny, 0.95), scale=(w * 0.42, d * 0.34, 1.65))
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)
    cap = add_cube("TMP", 1.0, (0, ny, 1.82), scale=(w * 0.46, d * 0.38, 0.14))
    assign_mat(cap, mats["SM_Black"])
    parts.append(cap)
    for sx in (-w * 0.18, w * 0.18):
        chamfer = add_cube("TMP", 1.0, (sx, ny, 0.72), scale=(w * 0.10, d * 0.28, 1.15))
        assign_mat(chamfer, mats["SM_Graphite"])
        parts.append(chamfer)
    ramp = add_cube("TMP", 1.0, (0, ny + d * 0.16, 0.22), scale=(w * 0.22, d * 0.12, 0.28))
    assign_mat(ramp, mats["SM_Concrete"])
    parts.append(ramp)
    door_f = add_cube("TMP", 1.0, (0, ny + d * 0.16, 0.78), scale=(0.72, 0.08, 1.05))
    assign_mat(door_f, mats["SM_Black"])
    parts.append(door_f)
    door = add_cube("TMP", 1.0, (0, ny + d * 0.15, 0.78), scale=(0.52, 0.06, 0.85))
    assign_mat(door, mats["SM_Orange"])
    parts.append(door)
    tower = add_cylinder("TMP", 0.32, 0.85, (0, ny, 2.35), vertices=16)
    assign_mat(tower, mats["SM_White"])
    parts.append(tower)
    tband = add_cylinder("TMP", 0.36, 0.08, (0, ny, 2.42), vertices=16)
    assign_mat(tband, mats["SM_Orange"])
    parts.append(tband)
    tcap = add_cylinder("TMP", 0.22, 0.16, (0, ny, 2.82), vertices=12)
    assign_mat(tcap, mats["SM_Black"])
    parts.append(tcap)
    for sx, sy in ((-0.42, -0.22), (0.42, -0.22), (-0.42, 0.22), (0.42, 0.22)):
        vent = add_cube("TMP", 1.0, (sx, ny + sy, 1.88), scale=(0.38, 0.32, 0.04))
        assign_mat(vent, mats["SM_Black"])
        parts.append(vent)
    beacon = add_uv_sphere("TMP", 0.12, (0, ny, 3.05), segments=8, rings=6)
    assign_mat(beacon, mats["SM_Cyan"])
    parts.append(beacon)

    cols, rows = 4, 3
    cell_w, cell_d = w * 0.18, d * 0.16
    pitch_x, pitch_z = w * 0.20, d * 0.17
    origin_x = -pitch_x * (cols - 1) * 0.5
    origin_z = -d * 0.18 - pitch_z * (rows - 1) * 0.5
    tilt = math.radians(-18)
    for r in range(rows):
        for c in range(cols):
            x = origin_x + c * pitch_x
            y = origin_z + r * pitch_z
            pylon = add_cylinder("TMP", 0.035, 0.55, (x, y, 0.38), vertices=8)
            assign_mat(pylon, mats["SM_Steel"])
            parts.append(pylon)
            frame = add_cube("TMP", 1.0, (x, y, 0.72), scale=(cell_w * 1.08, cell_d * 1.08, 0.05))
            frame.rotation_euler = (tilt, 0, 0)
            apply_rot(frame)
            assign_mat(frame, mats["SM_Graphite"])
            parts.append(frame)
            panel = add_cube("TMP", 1.0, (x, y, 0.76), scale=(cell_w, cell_d, 0.03))
            panel.rotation_euler = (tilt, 0, 0)
            apply_rot(panel)
            assign_mat(panel, mats["SM_Solar"])
            parts.append(panel)
            visor = add_cube("TMP", 1.0, (x, y + cell_d * 0.10, 0.88), scale=(cell_w * 0.90, 0.03, 0.02))
            visor.rotation_euler = (tilt, 0, 0)
            apply_rot(visor)
            assign_mat(visor, mats["SM_Cyan"])
            parts.append(visor)
        bus = add_cube("TMP", 1.0, (0, origin_z + r * pitch_z, 0.20), scale=(w * 0.72, 0.05, 0.03))
        assign_mat(bus, mats["SM_Cyan"])
        parts.append(bus)
    # Orange corner brackets on the array (sheet), not per-cell clutter.
    arr_y = origin_z + pitch_z
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        br = add_cube(
            "TMP", 1.0,
            (sx * pitch_x * 1.55, arr_y + sy * pitch_z * 1.15, 0.68),
            scale=(0.12, 0.12, 0.08),
        )
        assign_mat(br, mats["SM_Orange"])
        parts.append(br)
    return finish(parts, name, "32_Hero_Power", (28.0, 0.0, 0.0))


def build_farm(mats: dict) -> bpy.types.Object:
    """AG-1 vaulted greenhouse + ice plant (4×4 / 6 m)."""
    name = "SM_Hero_Farm"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4
    plinth = add_box_u("TMP", (0.0, 0.10, 0.0), (w * 0.94, 0.16, d * 0.90))
    assign_mat(plinth, mats["SM_Graphite"])
    parts.append(plinth)
    sill = add_box_u("TMP", (-w * 0.08, 0.28, 0.0), (w * 0.70, 0.22, d * 0.52))
    assign_mat(sill, mats["SM_Black"])
    parts.append(sill)
    hall = add_box_u("TMP", (-w * 0.08, 0.82, 0.0), (w * 0.66, 1.28, d * 0.46))
    assign_mat(hall, mats["SM_White"])
    parts.append(hall)
    for i in range(5):
        x = -w * 0.34 + i * w * 0.13
        arch = add_box_u("TMP", (x, 1.42, 0.0), (0.08, 1.05, d * 0.52))
        assign_mat(arch, mats["SM_Black"])
        parts.append(arch)
    vault_r = min(w, d) * 0.20
    vault = add_cyl_x("TMP", vault_r, w * 0.64, (-w * 0.08, 0.0, 1.48), vertices=24)
    assign_mat(vault, mats["SM_Glass"])
    parts.append(vault)
    for x in (-w * 0.36, w * 0.18):
        ring = add_cyl_x("TMP", vault_r * 1.04, 0.08, (x, 0.0, 1.48), vertices=20)
        assign_mat(ring, mats["SM_Orange"])
        parts.append(ring)
    for i in range(3):
        x = -w * 0.28 + i * w * 0.16
        tray = add_box_u("TMP", (x, 0.42, 0.0), (w * 0.14, 0.10, d * 0.32))
        assign_mat(tray, mats["SM_Plant"])
        parts.append(tray)
        glow = add_box_u("TMP", (x, 0.52, 0.0), (w * 0.11, 0.06, d * 0.24))
        assign_mat(glow, mats["SM_Ice"])
        parts.append(glow)
    hatch = add_box_u("TMP", (-w * 0.08, 0.72, d * 0.24), (0.62, 0.85, 0.08))
    assign_mat(hatch, mats["SM_Orange"])
    parts.append(hatch)
    tank_h = (2.35, 1.85)
    tank_z = (-d * 0.22, d * 0.22)
    for i in range(2):
        h = tank_h[i]
        tank = add_cyl_u("TMP", (w * 0.34, h * 0.5 + 0.18, tank_z[i]), (0.78, h * 0.5, 0.78), vertices=16)
        assign_mat(tank, mats["SM_Steel"])
        parts.append(tank)
        band = add_cyl_u("TMP", (w * 0.34, h * 0.5 + 0.18 + h * 0.10, tank_z[i]), (0.86, 0.06, 0.86), vertices=16)
        assign_mat(band, mats["SM_Ice"])
        parts.append(band)
        tcap = add_cyl_u("TMP", (w * 0.34, h + 0.22, tank_z[i]), (0.58, 0.08, 0.58), vertices=12)
        assign_mat(tcap, mats["SM_Black"])
        parts.append(tcap)
    manifold = add_cylinder(
        "TMP", 0.06, 1.70, (w * 0.34, 0.0, 2.55),
        rotation=(math.pi / 2, 0, 0), vertices=10,
    )
    assign_mat(manifold, mats["SM_Black"])
    parts.append(manifold)
    riser = add_cyl_u("TMP", (w * 0.34, 2.05, -d * 0.22), (0.12, 1.55, 0.12), vertices=10)
    assign_mat(riser, mats["SM_Black"])
    parts.append(riser)
    scaffold_tower(parts, mats, (w * 0.34, 0.0), 3.6, 0.85)
    cond = add_sph_u("TMP", (w * 0.22, 3.55, d * 0.18), (0.62, 0.32, 0.62))
    assign_mat(cond, mats["SM_Ice"])
    parts.append(cond)
    return finish(parts, name, "33_Hero_Farm", (42.0, 0.0, 0.0))


def build_camp(mats: dict) -> bpy.types.Object:
    """Low horizontal drum plant — cylinder language, not a HAB."""
    name = "SM_Hero_Camp"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4
    plinth = add_box_u("TMP", (0.0, 0.08, 0.0), (w * 0.94, 0.14, d * 0.88))
    assign_mat(plinth, mats["SM_Graphite"])
    parts.append(plinth)
    chassis = add_cyl_x("TMP", d * 0.29, w * 0.76, (-w * 0.06, 0.0, 0.72), vertices=20)
    assign_mat(chassis, mats["SM_Black"])
    parts.append(chassis)
    hull = add_cyl_x("TMP", d * 0.24, w * 0.56, (-w * 0.06, 0.0, 0.72), vertices=20)
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)
    band = add_cyl_x("TMP", d * 0.31, 0.10, (-w * 0.06, 0.0, 0.72), vertices=20)
    assign_mat(band, mats["SM_Orange"])
    parts.append(band)
    hopper = add_cyl_u("TMP", (w * 0.32, 1.05, 0.0), (w * 0.32, 1.15, w * 0.32), vertices=16)
    assign_mat(hopper, mats["SM_Dust"])
    parts.append(hopper)
    hband = add_cyl_u("TMP", (w * 0.32, 1.35, 0.0), (w * 0.36, 0.06, w * 0.36), vertices=16)
    assign_mat(hband, mats["SM_Orange"])
    parts.append(hband)
    scoop = add_box_u("TMP", (w * 0.48, 0.42, 0.0), (0.38, 0.42, d * 0.36))
    assign_mat(scoop, mats["SM_Orange"])
    parts.append(scoop)
    for i in range(3):
        z = -d * 0.20 + i * d * 0.20
        pipe = add_cyl_x("TMP", 0.06, w * 0.72, (0.02, z, 1.28), vertices=10)
        assign_mat(pipe, mats["SM_Yellow"] if i == 1 else mats["SM_Orange"])
        parts.append(pipe)
    tank_l = add_cyl_u("TMP", (-w * 0.28, 0.72, d * 0.32), (0.82, 0.62, 0.82), vertices=14)
    assign_mat(tank_l, mats["SM_Dust"])
    parts.append(tank_l)
    tank_r = add_cyl_u("TMP", (w * 0.08, 0.62, d * 0.32), (1.02, 0.48, 1.02), vertices=14)
    assign_mat(tank_r, mats["SM_Dust"])
    parts.append(tank_r)
    scaffold_low(parts, mats, (-w * 0.28, -d * 0.28), w * 0.7)
    belt = add_box_u("TMP", (w * 0.08, 0.28, -d * 0.28), (w * 0.7, 0.16, 0.35))
    assign_mat(belt, mats["SM_Yellow"])
    parts.append(belt)
    return finish(parts, name, "34_Hero_Camp", (56.0, 0.0, 0.0))


def build_mine(mats: dict) -> bpy.types.Object:
    """Twin silos + A-frame headframe."""
    name = "SM_Hero_Mine"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4
    deck = add_box_u("TMP", (0.0, 0.18, 0.0), (w * 0.95, 0.32, d * 0.90))
    assign_mat(deck, mats["SM_Graphite"])
    parts.append(deck)
    for sx in (-w * 0.24, w * 0.24):
        silo = add_cyl_u("TMP", (sx, 1.55, 0.08), (w * 0.36, 1.45, w * 0.36), vertices=16)
        assign_mat(silo, mats["SM_Dust"])
        parts.append(silo)
        band = add_cyl_u("TMP", (sx, 2.15, 0.08), (w * 0.40, 0.07, w * 0.40), vertices=16)
        assign_mat(band, mats["SM_Orange"])
        parts.append(band)
        tcap = add_cyl_u("TMP", (sx, 3.05, 0.08), (w * 0.28, 0.08, w * 0.28), vertices=12)
        assign_mat(tcap, mats["SM_Black"])
        parts.append(tcap)
    leg_l = add_box_u("TMP", (-w * 0.18, 2.35, -d * 0.18), (0.12, 3.4, 0.12))
    leg_l.rotation_euler = (0.0, math.radians(16), 0.0)
    apply_rot(leg_l)
    assign_mat(leg_l, mats["SM_Black"])
    parts.append(leg_l)
    leg_r = add_box_u("TMP", (w * 0.18, 2.35, -d * 0.18), (0.12, 3.4, 0.12))
    leg_r.rotation_euler = (0.0, math.radians(-16), 0.0)
    apply_rot(leg_r)
    assign_mat(leg_r, mats["SM_Black"])
    parts.append(leg_r)
    house = add_box_u("TMP", (0.0, 3.55, -d * 0.12), (w * 0.28, 0.42, 0.38))
    assign_mat(house, mats["SM_White"])
    parts.append(house)
    head = add_box_u("TMP", (0.0, 4.05, -d * 0.12), (w * 0.62, 0.16, 0.42))
    assign_mat(head, mats["SM_Yellow"])
    parts.append(head)
    winch = add_cyl_x("TMP", 0.21, 0.44, (0.0, -d * 0.12, 3.55), vertices=12)
    assign_mat(winch, mats["SM_Steel"])
    parts.append(winch)
    hopper = add_box_u("TMP", (0.0, 0.78, d * 0.30), (w * 0.38, 1.05, d * 0.28))
    assign_mat(hopper, mats["SM_Orange"])
    parts.append(hopper)
    pipe = add_cyl_x("TMP", 0.07, w * 0.52, (0.0, 0.08, 2.55), vertices=10)
    assign_mat(pipe, mats["SM_Black"])
    parts.append(pipe)
    scaffold_low(parts, mats, (0.0, -d * 0.34), w * 0.55)
    return finish(parts, name, "35_Hero_Mine", (70.0, 0.0, 0.0))


def build_defense(mats: dict) -> bpy.types.Object:
    name = "SM_Hero_Defense"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4
    plinth = add_cube("TMP", 1.0, (0, 0, 0.12), scale=(w * 0.92, d * 0.92, 0.22))
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    hull = add_cube("TMP", 1.0, (0, 0, 0.95), scale=(w * 0.72, d * 0.62, 1.55))
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)
    band = add_cube("TMP", 1.0, (0, 0, 0.55), scale=(w * 0.76, d * 0.66, 0.14))
    assign_mat(band, mats["SM_Black"])
    parts.append(band)
    stripe = add_cube("TMP", 1.0, (0, d * 0.32, 1.35), scale=(w * 0.55, 0.08, 0.12))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    visor = add_cube("TMP", 1.0, (0, d * 0.32, 1.12), scale=(w * 0.42, 0.07, 0.16))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    hatch = add_cube("TMP", 1.0, (0, d * 0.32, 0.7), scale=(0.7, 0.08, 0.85))
    assign_mat(hatch, mats["SM_Orange"])
    parts.append(hatch)
    span = min(w, d) * 0.38
    for i in range(4):
        ang = (i * 90 + 45) * math.pi / 180.0
        px, py = math.sin(ang) * span, math.cos(ang) * span
        bollard = add_cylinder("TMP", 0.08, 1.0, (px, py, 0.55), vertices=10)
        assign_mat(bollard, mats["SM_Black"])
        parts.append(bollard)
        eye = add_uv_sphere("TMP", 0.07, (px, py, 1.12), segments=8, rings=6)
        assign_mat(eye, mats["SM_Cyan"])
        parts.append(eye)
    turret(parts, mats, (0.0, 0.08, 1.85), scale=1.35)
    return finish(parts, name, "36_Hero_Defense", (84.0, 0.0, 0.0))


def build_pad(mats: dict) -> bpy.types.Object:
    """Circular pad + Starship-like stack (sheet language, 6×6 / 9 m RTS scale)."""
    name = "SM_Hero_LandingPad"
    remove_if_exists(name)
    parts = []
    span = FOOT_6
    dia = span * 0.92
    disc = add_cylinder("TMP", dia * 0.5, 0.16, (0, 0, 0.08), vertices=32)
    assign_mat(disc, mats["SM_Graphite"])
    parts.append(disc)
    lip = add_cylinder("TMP", dia * 0.52, 0.10, (0, 0, 0.06), vertices=32)
    assign_mat(lip, mats["SM_Concrete"])
    parts.append(lip)
    yellow = add_cylinder("TMP", dia * 0.505, 0.04, (0, 0, 0.15), vertices=32)
    assign_mat(yellow, mats["SM_Yellow"])
    parts.append(yellow)
    for r, depth in ((0.42, 0.04), (0.28, 0.035), (0.16, 0.03)):
        ring = add_cylinder("TMP", dia * r, depth, (0, 0, 0.17), vertices=28)
        assign_mat(ring, mats["SM_Orange"])
        parts.append(ring)
    inner = add_cylinder("TMP", dia * 0.11, 0.03, (0, 0, 0.18), vertices=16)
    assign_mat(inner, mats["SM_Black"])
    parts.append(inner)
    # Orange H around the stack base (visible beside the ship).
    for sx in (-0.42, 0.42):
        bar = add_cube("TMP", 1.0, (sx, 0, 0.20), scale=(0.10, 0.95, 0.03))
        assign_mat(bar, mats["SM_Orange"])
        parts.append(bar)
    cross = add_cube("TMP", 1.0, (0, 0, 0.20), scale=(0.84, 0.12, 0.03))
    assign_mat(cross, mats["SM_Orange"])
    parts.append(cross)
    for i in range(4):
        ang = i * 90 * math.pi / 180.0
        tick = add_cube(
            "TMP", 1.0,
            (math.sin(ang) * dia * 0.44, math.cos(ang) * dia * 0.44, 0.19),
            scale=(0.12, 0.42, 0.03),
        )
        tick.rotation_euler = (0, 0, ang)
        apply_rot(tick)
        assign_mat(tick, mats["SM_Orange"])
        parts.append(tick)
        light = add_uv_sphere(
            "TMP", 0.09,
            (math.sin(ang) * dia * 0.46, math.cos(ang) * dia * 0.46, 0.28),
            segments=8, rings=6,
        )
        assign_mat(light, mats["SM_Cyan"])
        parts.append(light)
        vent = add_cube(
            "TMP", 1.0,
            (math.sin(ang + 0.4) * dia * 0.48, math.cos(ang + 0.4) * dia * 0.48, 0.22),
            scale=(0.28, 0.16, 0.10),
        )
        vent.rotation_euler = (0, 0, ang)
        apply_rot(vent)
        assign_mat(vent, mats["SM_Black"])
        parts.append(vent)
        # Square modular interface blocks at the stack (Lego language).
        block = add_cube(
            "TMP", 1.0,
            (math.sin(ang) * 0.95, math.cos(ang) * 0.95, 0.38),
            scale=(0.42, 0.32, 0.55),
        )
        assign_mat(block, mats["SM_Graphite"])
        parts.append(block)

    # Starship-like stack — visual only, ~7.6 m (not the sheet's 122 m).
    skirt = add_cylinder("TMP", 0.68, 0.55, (0, 0, 0.55), vertices=20)
    assign_mat(skirt, mats["SM_Black"])
    parts.append(skirt)
    body = add_cylinder("TMP", 0.52, 5.2, (0, 0, 3.15), vertices=20)
    assign_mat(body, mats["SM_White"])
    parts.append(body)
    heat = add_cube("TMP", 1.0, (0, -0.42, 2.85), scale=(0.85, 0.18, 4.4))
    assign_mat(heat, mats["SM_Black"])
    parts.append(heat)
    for z in (1.85, 3.55, 5.05):
        band = add_cylinder("TMP", 0.56, 0.10, (0, 0, z), vertices=16)
        assign_mat(band, mats["SM_Orange"] if z > 3.0 else mats["SM_Black"])
        parts.append(band)
    nose = add_cone("TMP", 0.52, 1.55, (0, 0, 6.55), vertices=16)
    assign_mat(nose, mats["SM_White"])
    parts.append(nose)
    for sy in (-0.58, 0.58):
        fin = add_cube("TMP", 1.0, (0.08, sy, 1.05), scale=(0.12, 0.55, 1.25))
        assign_mat(fin, mats["SM_Black"])
        parts.append(fin)
        flap = add_cube("TMP", 1.0, (0.12, sy * 0.72, 5.55), scale=(0.08, 0.38, 0.72))
        assign_mat(flap, mats["SM_Black"])
        parts.append(flap)
    return finish(parts, name, "37_Hero_Pad", (0.0, 16.0, 0.0))


def build_guild(mats: dict) -> bpy.types.Object:
    """CMD-1 civic hall (sheet) + guild banner. Not a Commons dome."""
    name = "SM_Hero_GuildHall"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4
    plinth = add_box_u("TMP", (0.0, 0.14, 0.0), (w * 0.96, 0.26, d * 0.92))
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    mech = add_box_u("TMP", (0.0, 0.42, 0.0), (w * 0.88, 0.32, d * 0.78))
    assign_mat(mech, mats["SM_Graphite"])
    parts.append(mech)
    hull_lo = add_box_u("TMP", (0.0, 1.05, -d * 0.04), (w * 0.78, 1.15, d * 0.62))
    assign_mat(hull_lo, mats["SM_White"])
    bevel_hull(hull_lo, 0.05, 2)
    parts.append(hull_lo)
    hull_hi = add_box_u("TMP", (0.0, 1.95, -d * 0.06), (w * 0.62, 0.85, d * 0.50))
    assign_mat(hull_hi, mats["SM_White"])
    bevel_hull(hull_hi, 0.04, 2)
    parts.append(hull_hi)
    for y in (0.68, 1.42):
        band = add_box_u("TMP", (0.0, y, -d * 0.04), (w * 0.80, 0.04, d * 0.64))
        assign_mat(band, mats["SM_Black"])
        parts.append(band)
    for sx in (-w * 0.20, w * 0.20):
        groove = add_box_u("TMP", (sx, 1.08, -d * 0.04), (0.04, 1.05, d * 0.63))
        assign_mat(groove, mats["SM_Graphite"])
        parts.append(groove)
    for sx in (-w * 0.12, 0.0, w * 0.12):
        roof = add_box_u("TMP", (sx, 2.39, -d * 0.06), (0.035, 0.035, d * 0.48))
        assign_mat(roof, mats["SM_Black"])
        parts.append(roof)
    for sx, sz in (
        (-w * 0.38, -d * 0.33), (w * 0.38, -d * 0.33),
        (-w * 0.38, d * 0.22), (w * 0.38, d * 0.22),
    ):
        post = add_box_u("TMP", (sx, 1.05, sz), (0.08, 1.12, 0.08))
        assign_mat(post, mats["SM_Black"])
        parts.append(post)
    cap = add_box_u("TMP", (0.0, 2.42, -d * 0.06), (w * 0.68, 0.12, d * 0.56))
    assign_mat(cap, mats["SM_Black"])
    parts.append(cap)
    for sx in (-w * 0.16, w * 0.16):
        col = add_box_u("TMP", (sx, 1.15, d * 0.28), (0.14, 1.85, 0.12))
        assign_mat(col, mats["SM_Orange"])
        parts.append(col)
    frame = add_box_u("TMP", (0.0, 0.95, d * 0.30), (0.72, 1.15, 0.10))
    assign_mat(frame, mats["SM_Black"])
    parts.append(frame)
    hatch = add_box_u("TMP", (0.0, 0.95, d * 0.32), (0.52, 0.95, 0.08))
    assign_mat(hatch, mats["SM_Orange"])
    parts.append(hatch)
    steps = add_box_u("TMP", (0.0, 0.22, d * 0.42), (w * 0.36, 0.16, d * 0.18))
    assign_mat(steps, mats["SM_Concrete"])
    parts.append(steps)
    step2 = add_box_u("TMP", (0.0, 0.36, d * 0.36), (w * 0.30, 0.12, d * 0.12))
    assign_mat(step2, mats["SM_Graphite"])
    parts.append(step2)
    visor = add_box_u("TMP", (0.0, 1.58, d * 0.28), (w * 0.28, 0.16, 0.07))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    sensor = add_sph_u("TMP", (0.0, 2.72, -d * 0.06), (0.55, 0.28, 0.55))
    assign_mat(sensor, mats["SM_White"])
    parts.append(sensor)
    sband = add_cyl_u("TMP", (0.0, 2.62, -d * 0.06), (0.62, 0.04, 0.62), vertices=16)
    assign_mat(sband, mats["SM_Black"])
    parts.append(sband)
    ant_l = add_cyl_u("TMP", (-0.45, 3.15, -d * 0.12), (0.05, 0.55, 0.05), vertices=8)
    assign_mat(ant_l, mats["SM_Steel"])
    parts.append(ant_l)
    ant_r = add_cyl_u("TMP", (0.38, 3.05, 0.08), (0.04, 0.42, 0.04), vertices=8)
    assign_mat(ant_r, mats["SM_Steel"])
    parts.append(ant_r)
    for sign in (1.0, -1.0):
        port = add_box_u("TMP", (sign * (w * 0.5 - 0.15), 0.85, 0.0), (0.30, 0.62, 0.62))
        assign_mat(port, mats["SM_White"])
        parts.append(port)
        ring = add_box_u("TMP", (sign * (w * 0.5 - 0.04), 0.85, 0.0), (0.08, 0.70, 0.70))
        assign_mat(ring, mats["SM_Orange"])
        parts.append(ring)
    mast = add_cyl_u("TMP", (w * 0.22, 3.35, -d * 0.18), (0.08, 0.85, 0.08), vertices=8)
    assign_mat(mast, mats["SM_Steel"])
    parts.append(mast)
    banner = add_box_u("TMP", (w * 0.22 + 0.28, 3.55, -d * 0.18), (0.52, 0.38, 0.05))
    assign_mat(banner, mats["SM_Orange"])
    parts.append(banner)
    beacon = add_sph_u("TMP", (w * 0.22, 4.25, -d * 0.18), (0.18, 0.18, 0.18))
    assign_mat(beacon, mats["SM_Cyan"])
    parts.append(beacon)
    return finish(parts, name, "38_Hero_Guild", (14.0, 16.0, 0.0))


def build_lab(mats: dict) -> bpy.types.Object:
    """LAB-1 isolated cylinder (sheet Ø4.5×L8.7 → 4×4 / 6 m). Smaller sibling of HAB."""
    name = "SM_Hero_LAB"
    remove_if_exists(name)
    parts = []
    length = FOOT_4 * 0.90
    radius = length / 3.86
    z_axis = radius + 0.20

    shell = add_cyl_x("TMP", radius, length * 0.78, (0, 0, z_axis), vertices=32)
    assign_mat(shell, mats["SM_White"])
    parts.append(shell)
    belly = add_cube("TMP", 1.0, (0, 0, z_axis - radius * 0.42), scale=(length * 0.72, radius * 1.35, radius * 0.55))
    assign_mat(belly, mats["SM_Graphite"])
    parts.append(belly)
    mid = add_cyl_x("TMP", radius * 1.04, 0.55, (0, 0, z_axis), vertices=32)
    assign_mat(mid, mats["SM_Black"])
    parts.append(mid)

    for sign in (-1.0, 1.0):
        x = sign * (length * 0.34)
        cap = add_cyl_x("TMP", radius * 1.01, 0.42, (x, 0, z_axis), vertices=28)
        assign_mat(cap, mats["SM_Black"])
        parts.append(cap)
        ring = add_cyl_x("TMP", radius * 1.08, 0.08, (x + sign * 0.28, 0, z_axis), vertices=28)
        assign_mat(ring, mats["SM_Orange"])
        parts.append(ring)
        stripe = add_cube("TMP", 1.0, (x, -radius * 0.15, z_axis + radius * 0.35), scale=(0.10, 0.12, 0.85))
        assign_mat(stripe, mats["SM_Orange"])
        parts.append(stripe)
        dock = add_cyl_x("TMP", 0.52, 0.38, (sign * (length * 0.48), 0, z_axis), vertices=18)
        assign_mat(dock, mats["SM_White"])
        parts.append(dock)
        flange = add_cyl_x("TMP", 0.60, 0.07, (sign * (length * 0.52), 0, z_axis), vertices=18)
        assign_mat(flange, mats["SM_Black"])
        parts.append(flange)

    front = add_cyl_x("TMP", 0.58, 0.08, (-length * 0.50, 0, z_axis), vertices=18)
    assign_mat(front, mats["SM_Steel"])
    parts.append(front)
    sq = add_cube("TMP", 1.0, (-length * 0.54, 0, z_axis), scale=(0.08, 0.42, 0.42))
    assign_mat(sq, mats["SM_White"])
    bevel_hull(sq, 0.014, 1)
    parts.append(sq)

    hatch_f = add_cube("TMP", 1.0, (0.15, radius * 0.98, z_axis), scale=(0.72, 0.08, 0.72))
    assign_mat(hatch_f, mats["SM_Black"])
    parts.append(hatch_f)
    hatch = add_cube("TMP", 1.0, (0.15, radius * 0.94, z_axis), scale=(0.55, 0.08, 0.55))
    assign_mat(hatch, mats["SM_Orange"])
    parts.append(hatch)

    bay = add_cube("TMP", 1.0, (0.85, -radius * 0.55, z_axis + 0.15), scale=(1.15, 0.55, 0.72))
    assign_mat(bay, mats["SM_Steel"])
    parts.append(bay)
    for i in range(3):
        sample = add_cylinder("TMP", 0.07, 0.32, (0.55 + i * 0.22, -radius * 0.55, z_axis + 0.55), vertices=8)
        assign_mat(sample, mats["SM_Ice"])
        parts.append(sample)

    grille = add_cube("TMP", 1.0, (-0.35, 0.05, z_axis + radius * 0.92), scale=(0.85, 0.42, 0.08))
    assign_mat(grille, mats["SM_Black"])
    parts.append(grille)
    pipe = add_cyl_x("TMP", 0.05, length * 0.55, (0.05, 0, 0.32), vertices=10)
    assign_mat(pipe, mats["SM_Steel"])
    parts.append(pipe)

    mast = add_cylinder("TMP", 0.035, 0.95, (1.05, 0.12, z_axis + radius + 0.55), vertices=8)
    assign_mat(mast, mats["SM_Steel"])
    parts.append(mast)
    dish = add_uv_sphere("TMP", 0.32, (1.05, 0.12, z_axis + radius + 1.05), scale=(1.0, 1.0, 0.22), segments=14, rings=8)
    assign_mat(dish, mats["SM_White"])
    parts.append(dish)
    ring = add_cylinder("TMP", 0.34, 0.04, (1.05, 0.12, z_axis + radius + 1.05), vertices=14)
    assign_mat(ring, mats["SM_Orange"])
    parts.append(ring)
    lens = add_uv_sphere("TMP", 0.07, (1.05, 0.28, z_axis + radius + 1.08), segments=8, rings=6)
    assign_mat(lens, mats["SM_Cyan"])
    parts.append(lens)

    for y in (-1.05, 1.05):
        skid = add_cube("TMP", 1.0, (0, y, 0.10), scale=(length * 0.62, 0.32, 0.18))
        assign_mat(skid, mats["SM_Black"])
        parts.append(skid)
        foot = add_cube("TMP", 1.0, (0, y, 0.28), scale=(length * 0.22, 0.22, 0.28))
        assign_mat(foot, mats["SM_Graphite"])
        parts.append(foot)
    cyl_x_panel_lines(parts, mats, length, radius, z_axis, skip_y_sign=1.0)
    return finish(parts, name, "39_Hero_LAB", (28.0, 16.0, 0.0))


def build_loom(mats: dict) -> bpy.types.Object:
    """Weather lattice + cooling towers. Not a white cabin."""
    name = "SM_Hero_ClimateLoom"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_6
    plinth = add_box_u("TMP", (0.0, 0.14, 0.0), (w * 0.94, 0.26, d * 0.94))
    assign_mat(plinth, mats["SM_Graphite"])
    parts.append(plinth)
    bunker = add_box_u("TMP", (-w * 0.32, 0.72, -d * 0.30), (w * 0.28, 1.15, d * 0.28))
    assign_mat(bunker, mats["SM_Black"])
    parts.append(bunker)
    bcap = add_box_u("TMP", (-w * 0.32, 1.35, -d * 0.30), (w * 0.32, 0.10, d * 0.32))
    assign_mat(bcap, mats["SM_White"])
    parts.append(bcap)
    hatch = add_box_u("TMP", (-w * 0.32, 0.72, -d * 0.16), (0.55, 0.72, 0.08))
    assign_mat(hatch, mats["SM_Orange"])
    parts.append(hatch)
    for i in range(4):
        x = -w * 0.32 + i * w * 0.22
        post = add_box_u("TMP", (x, 2.35, d * 0.22), (0.12, 4.2, 0.12))
        assign_mat(post, mats["SM_Black"])
        parts.append(post)
        brace = add_box_u("TMP", (x, 2.55, d * 0.08), (0.08, 0.08, d * 0.28))
        assign_mat(brace, mats["SM_Steel"])
        parts.append(brace)
    boom = add_box_u("TMP", (0.02, 4.45, d * 0.22), (w * 0.82, 0.14, 0.22))
    assign_mat(boom, mats["SM_Yellow"])
    parts.append(boom)
    for i in range(5):
        x = -w * 0.34 + i * w * 0.17
        nozzle = add_cyl_u("TMP", (x, 3.95, d * 0.22), (0.14, 0.28, 0.14), vertices=8)
        assign_mat(nozzle, mats["SM_Ice"])
        parts.append(nozzle)
    tower_l = add_cyl_u("TMP", (w * 0.28, 1.85, -d * 0.22), (1.15, 1.75, 1.15), vertices=16)
    assign_mat(tower_l, mats["SM_Ice"])
    parts.append(tower_l)
    tower_r = add_cyl_u("TMP", (w * 0.28, 1.35, d * 0.08), (0.92, 1.25, 0.92), vertices=14)
    assign_mat(tower_r, mats["SM_Steel"])
    parts.append(tower_r)
    band = add_cyl_u("TMP", (w * 0.28, 2.35, -d * 0.22), (1.28, 0.08, 1.28), vertices=14)
    assign_mat(band, mats["SM_Orange"])
    parts.append(band)
    flare = add_cyl_u("TMP", (w * 0.28, 3.55, -d * 0.22), (0.55, 0.22, 0.55), vertices=12)
    assign_mat(flare, mats["SM_Black"])
    parts.append(flare)
    scaffold_tower(parts, mats, (w * 0.18, d * 0.32), 4.8, 0.95)
    cond = add_sph_u("TMP", (0.05, 4.85, d * 0.22), (0.55, 0.28, 0.55))
    assign_mat(cond, mats["SM_Ice"])
    parts.append(cond)
    beacon = add_sph_u("TMP", (-w * 0.32, 1.62, -d * 0.30), (0.18, 0.18, 0.18))
    assign_mat(beacon, mats["SM_Cyan"])
    parts.append(beacon)
    return finish(parts, name, "40_Hero_Loom", (42.0, 16.0, 0.0))


def build_spire(mats: dict) -> bpy.types.Object:
    """Tapered shield monument + rings. Not a Commons citadel."""
    name = "SM_Hero_AegisSpire"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_6
    plinth = add_box_u("TMP", (0.0, 0.16, 0.0), (w * 0.88, 0.28, d * 0.88))
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    span = min(w, d) * 0.38
    for i in range(4):
        ang = (i * 90 + 45) * math.pi / 180.0
        px, pz = math.sin(ang) * span, math.cos(ang) * span
        butt = add_box_u("TMP", (px, 1.05, pz), (0.28, 1.85, 0.28))
        butt.rotation_euler = (0.0, 0.0, ang)
        apply_rot(butt)
        assign_mat(butt, mats["SM_Graphite"])
        parts.append(butt)
        eye = add_cyl_u("TMP", (px, 2.05, pz), (0.18, 0.12, 0.18), vertices=10)
        assign_mat(eye, mats["SM_Cyan"])
        parts.append(eye)
    base = add_box_u("TMP", (0.0, 1.25, 0.0), (w * 0.36, 2.15, d * 0.36))
    assign_mat(base, mats["SM_White"])
    parts.append(base)
    mid = add_box_u("TMP", (0.0, 3.55, 0.0), (w * 0.22, 2.45, d * 0.22))
    assign_mat(mid, mats["SM_White"])
    parts.append(mid)
    needle = add_cyl_u("TMP", (0.0, 5.85, 0.0), (0.16, 1.35, 0.16), vertices=10)
    assign_mat(needle, mats["SM_Steel"])
    parts.append(needle)
    for y, sx, sz in ((1.85, w * 0.18, d * 0.19), (3.15, w * 0.12, d * 0.12), (4.45, w * 0.08, d * 0.12)):
        chev = add_box_u("TMP", (0.0, y, sz), (sx, 0.10 if y > 2.0 else 0.12, 0.08))
        assign_mat(chev, mats["SM_Orange"])
        parts.append(chev)
    for y, dia in ((2.25, w * 0.78), (3.85, w * 0.52), (5.25, w * 0.32)):
        ring = add_cyl_u("TMP", (0.0, y, 0.0), (dia, 0.05, dia), vertices=24)
        assign_mat(ring, mats["SM_Cyan"])
        parts.append(ring)
    beacon = add_sph_u("TMP", (0.0, 7.25, 0.0), (0.28, 0.28, 0.28))
    assign_mat(beacon, mats["SM_Cyan"])
    parts.append(beacon)
    return finish(parts, name, "41_Hero_Spire", (56.0, 16.0, 0.0))


def build_archive(mats: dict) -> bpy.types.Object:
    """Buried data silos + blast door. Low vault, not a loom or spire."""
    name = "SM_Hero_DeepArchive"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_6
    plinth = add_box_u("TMP", (0.0, 0.10, 0.0), (w * 0.94, 0.18, d * 0.94))
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    lid = add_box_u("TMP", (-w * 0.06, 1.55, -d * 0.08), (w * 0.62, 0.14, d * 0.58))
    assign_mat(lid, mats["SM_Graphite"])
    parts.append(lid)
    cap = add_box_u("TMP", (-w * 0.06, 1.68, -d * 0.08), (w * 0.52, 0.08, d * 0.48))
    assign_mat(cap, mats["SM_Black"])
    parts.append(cap)
    silo_z = (-d * 0.22, 0.02, d * 0.26)
    silo_y = (0.62, 0.72, 0.55)
    for i in range(3):
        silo = add_cyl_x("TMP", 0.36, w * 0.64, (-w * 0.08, silo_z[i], silo_y[i]), vertices=16)
        assign_mat(silo, mats["SM_White"] if i == 1 else mats["SM_Steel"])
        parts.append(silo)
        band = add_cyl_x("TMP", 0.40, 0.10, (-w * 0.08, silo_z[i], silo_y[i]), vertices=16)
        assign_mat(band, mats["SM_Orange"])
        parts.append(band)
    frame = add_box_u("TMP", (-w * 0.08, 0.85, d * 0.42), (w * 0.42, 1.35, 0.12))
    assign_mat(frame, mats["SM_Black"])
    parts.append(frame)
    hatch = add_box_u("TMP", (-w * 0.08, 0.78, d * 0.46), (w * 0.28, 1.05, 0.08))
    assign_mat(hatch, mats["SM_Orange"])
    parts.append(hatch)
    stripe = add_box_u("TMP", (-w * 0.08, 0.42, d * 0.44), (w * 0.48, 0.10, 0.08))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    stack = add_box_u("TMP", (w * 0.34, 0.95, -d * 0.22), (w * 0.18, 1.65, d * 0.28))
    assign_mat(stack, mats["SM_Steel"])
    parts.append(stack)
    dish0 = add_sph_u("TMP", (w * 0.34, 2.05, -d * 0.22), (0.92, 0.16, 0.92))
    assign_mat(dish0, mats["SM_White"])
    parts.append(dish0)
    dish1 = add_sph_u("TMP", (w * 0.22, 1.85, d * 0.18), (0.62, 0.12, 0.62))
    assign_mat(dish1, mats["SM_Graphite"])
    parts.append(dish1)
    mast = add_cyl_u("TMP", (-w * 0.06, 2.25, -d * 0.08), (0.08, 0.55, 0.08), vertices=8)
    assign_mat(mast, mats["SM_Steel"])
    parts.append(mast)
    beacon = add_sph_u("TMP", (-w * 0.06, 2.85, -d * 0.08), (0.18, 0.18, 0.18))
    assign_mat(beacon, mats["SM_Cyan"])
    parts.append(beacon)
    return finish(parts, name, "42_Hero_Archive", (70.0, 16.0, 0.0))


def build_ops(mats: dict) -> bpy.types.Object:
    """OPS-1 operations annex (sheet). Low elongated prism."""
    name = "SM_Hero_OPS"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4
    plinth = add_box_u("TMP", (0.0, 0.10, 0.0), (w * 0.94, 0.18, d * 0.88))
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    hull = add_box_u("TMP", (0.0, 0.72, 0.0), (w * 0.82, 1.12, d * 0.62))
    assign_mat(hull, mats["SM_White"])
    bevel_hull(hull, 0.05, 2)
    parts.append(hull)
    cap = add_box_u("TMP", (0.0, 1.32, 0.0), (w * 0.88, 0.12, d * 0.68))
    assign_mat(cap, mats["SM_Black"])
    parts.append(cap)
    for y in (0.42, 0.98):
        band = add_box_u("TMP", (0.0, y, 0.0), (w * 0.84, 0.04, d * 0.64))
        assign_mat(band, mats["SM_Black"])
        parts.append(band)
    for sz in (-d * 0.12, 0.0, d * 0.12):
        roof = add_box_u("TMP", (0.0, 1.39, sz), (w * 0.70, 0.03, 0.03))
        assign_mat(roof, mats["SM_Black"])
        parts.append(roof)
    for sx in (-w * 0.18, w * 0.18):
        plate = add_box_u("TMP", (sx, 0.78, -d * 0.32), (0.70, 0.52, 0.035))
        assign_mat(plate, mats["SM_Graphite"])
        parts.append(plate)
    for sx, sz in ((-w * 0.38, -d * 0.28), (w * 0.38, -d * 0.28), (-w * 0.38, d * 0.28), (w * 0.38, d * 0.28)):
        corner = add_cyl_u("TMP", (sx, 0.72, sz), (0.42, 1.12, 0.42), vertices=12)
        assign_mat(corner, mats["SM_White"])
        parts.append(corner)
    vframe = add_box_u("TMP", (0.0, 1.05, d * 0.32), (w * 0.62, 0.28, 0.08))
    assign_mat(vframe, mats["SM_Black"])
    parts.append(vframe)
    visor = add_box_u("TMP", (0.0, 1.05, d * 0.34), (w * 0.55, 0.18, 0.06))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    steps = add_box_u("TMP", (0.0, 0.18, d * 0.42), (w * 0.22, 0.12, d * 0.14))
    assign_mat(steps, mats["SM_Concrete"])
    parts.append(steps)
    hatch = add_box_u("TMP", (0.0, 0.52, d * 0.32), (0.42, 0.52, 0.08))
    assign_mat(hatch, mats["SM_Orange"])
    parts.append(hatch)
    vent = add_cyl_u("TMP", (0.35, 1.48, -0.12), (0.42, 0.08, 0.42), vertices=12)
    assign_mat(vent, mats["SM_Graphite"])
    parts.append(vent)
    ant_l = add_cyl_u("TMP", (-0.55, 1.85, 0.15), (0.05, 0.42, 0.05), vertices=8)
    assign_mat(ant_l, mats["SM_Steel"])
    parts.append(ant_l)
    ant_r = add_cyl_u("TMP", (0.48, 1.72, -0.22), (0.04, 0.32, 0.04), vertices=8)
    assign_mat(ant_r, mats["SM_Steel"])
    parts.append(ant_r)
    stripe = add_box_u("TMP", (0.0, 0.48, d * 0.32), (w * 0.72, 0.08, 0.06))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    beacon = add_sph_u("TMP", (-0.35, 1.62, 0.18), (0.16, 0.16, 0.16))
    assign_mat(beacon, mats["SM_Cyan"])
    parts.append(beacon)
    return finish(parts, name, "46_Hero_OPS", (28.0, 32.0, 0.0))


def _workshop_parts(parts, mats, w, d, h):
    """Hangar bay matching HeroBuildingKits.BuildWorkshop (Unity Y-up)."""
    plinth = add_box_u("TMP", (0.0, 0.10, 0.0), (w * 0.94, 0.18, d * 0.94))
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    apron = add_box_u("TMP", (0.0, 0.16, d * 0.32), (w * 0.72, 0.08, d * 0.28))
    assign_mat(apron, mats["SM_Concrete"])
    parts.append(apron)
    hull = add_box_u("TMP", (0.0, h * 0.5 + 0.12, -d * 0.08), (w * 0.78, h, d * 0.68))
    assign_mat(hull, mats["SM_White"])
    parts.append(hull)
    cap = add_box_u("TMP", (0.0, h + 0.18, -d * 0.08), (w * 0.84, 0.14, d * 0.74))
    assign_mat(cap, mats["SM_Black"])
    parts.append(cap)
    stripe = add_box_u("TMP", (0.0, h * 0.62, d * 0.26), (w * 0.55, 0.10, 0.08))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    visor = add_box_u("TMP", (0.0, h * 0.78, d * 0.26), (w * 0.38, 0.16, 0.07))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    door_l = add_box_u("TMP", (-w * 0.16, 0.95, d * 0.26), (w * 0.22, 1.55, 0.10))
    assign_mat(door_l, mats["SM_Orange"])
    parts.append(door_l)
    door_r = add_box_u("TMP", (w * 0.16, 0.95, d * 0.26), (w * 0.22, 1.55, 0.10))
    assign_mat(door_r, mats["SM_Orange"])
    parts.append(door_r)
    stack_l = add_cyl_u("TMP", (-w * 0.22, h + 0.55, -d * 0.18), (0.28, 0.42, 0.28), vertices=12)
    assign_mat(stack_l, mats["SM_Graphite"])
    parts.append(stack_l)
    stack_r = add_cyl_u("TMP", (w * 0.22, h + 0.55, -d * 0.18), (0.28, 0.42, 0.28), vertices=12)
    assign_mat(stack_r, mats["SM_Graphite"])
    parts.append(stack_r)
    crane_post = add_box_u("TMP", (-w * 0.38, 1.35, d * 0.18), (0.10, 2.4, 0.10))
    assign_mat(crane_post, mats["SM_Black"])
    parts.append(crane_post)
    crane_beam = add_box_u("TMP", (-w * 0.12, 2.52, d * 0.18), (w * 0.52, 0.08, 0.10))
    assign_mat(crane_beam, mats["SM_Yellow"])
    parts.append(crane_beam)
    beacon = add_sph_u("TMP", (0.0, h + 0.72, -d * 0.08), (0.22, 0.22, 0.22))
    assign_mat(beacon, mats["SM_Cyan"])
    parts.append(beacon)
    hatch = add_box_u("TMP", (0.0, 0.72, d * 0.27), (0.62, 0.85, 0.08))
    assign_mat(hatch, mats["SM_Orange"])
    parts.append(hatch)
    track_l = add_box_u("TMP", (-w * 0.30, 1.05, d * 0.27), (0.06, 1.85, 0.06))
    assign_mat(track_l, mats["SM_Black"])
    parts.append(track_l)
    track_r = add_box_u("TMP", (w * 0.30, 1.05, d * 0.27), (0.06, 1.85, 0.06))
    assign_mat(track_r, mats["SM_Black"])
    parts.append(track_r)
    bay_l = add_sph_u("TMP", (-w * 0.22, h * 0.92, d * 0.22), (0.16, 0.16, 0.16))
    assign_mat(bay_l, mats["SM_Orange"])
    parts.append(bay_l)
    bay_r = add_sph_u("TMP", (w * 0.22, h * 0.92, d * 0.22), (0.16, 0.16, 0.16))
    assign_mat(bay_r, mats["SM_Orange"])
    parts.append(bay_r)
    for i in range(3):
        chev = add_box_u(
            "TMP",
            (0.0, 0.20, d * 0.38 - i * 0.22),
            (0.55 - i * 0.08, 0.03, 0.10),
        )
        assign_mat(chev, mats["SM_Yellow"])
        parts.append(chev)


def build_workshop(mats: dict) -> bpy.types.Object:
    name = "SM_Hero_Workshop"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4
    _workshop_parts(parts, mats, w, d, 2.05)
    return finish(parts, name, "43_Hero_Workshop", (84.0, 16.0, 0.0))


def build_workshop_tall(mats: dict) -> bpy.types.Object:
    name = "SM_Hero_WorkshopTall"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4
    _workshop_parts(parts, mats, w, d, 2.55)
    # Roof turret — Defense / Sentinel hangar reads taller than the bay kit.
    turret(parts, mats, (0.0, -d * 0.08, 2.85), scale=1.05)
    return finish(parts, name, "44_Hero_WorkshopTall", (0.0, 32.0, 0.0))


def build_inn(mats: dict) -> bpy.types.Object:
    name = "SM_Hero_Inn"
    remove_if_exists(name)
    parts = []
    w = d = FOOT_4
    plinth = add_box_u("TMP", (0.0, 0.10, 0.0), (w * 0.92, 0.18, d * 0.92))
    assign_mat(plinth, mats["SM_Black"])
    parts.append(plinth)
    porch = add_box_u("TMP", (0.0, 0.22, d * 0.36), (w * 0.48, 0.12, d * 0.22))
    assign_mat(porch, mats["SM_Concrete"])
    parts.append(porch)
    hall = add_box_u("TMP", (0.0, 1.15, -d * 0.04), (w * 0.58, 2.1, d * 0.62))
    assign_mat(hall, mats["SM_White"])
    parts.append(hall)
    cap = add_box_u("TMP", (0.0, 2.28, -d * 0.04), (w * 0.64, 0.14, d * 0.68))
    assign_mat(cap, mats["SM_Black"])
    parts.append(cap)
    stripe = add_box_u("TMP", (0.0, 1.55, d * 0.27), (w * 0.42, 0.10, 0.08))
    assign_mat(stripe, mats["SM_Orange"])
    parts.append(stripe)
    visor = add_box_u("TMP", (0.0, 1.22, d * 0.27), (w * 0.32, 0.18, 0.07))
    assign_mat(visor, mats["SM_Cyan"])
    parts.append(visor)
    hatch = add_box_u("TMP", (0.0, 0.85, d * 0.27), (0.62, 1.05, 0.08))
    assign_mat(hatch, mats["SM_Orange"])
    parts.append(hatch)
    wing_l = add_box_u("TMP", (-w * 0.32, 0.82, -d * 0.06), (w * 0.22, 1.4, d * 0.42))
    assign_mat(wing_l, mats["SM_Graphite"])
    parts.append(wing_l)
    wing_r = add_box_u("TMP", (w * 0.32, 0.82, -d * 0.06), (w * 0.22, 1.4, d * 0.42))
    assign_mat(wing_r, mats["SM_Graphite"])
    parts.append(wing_r)
    post_r = add_cyl_u("TMP", (w * 0.18, 1.05, d * 0.42), (0.08, 0.85, 0.08), vertices=8)
    assign_mat(post_r, mats["SM_Steel"])
    parts.append(post_r)
    lantern_r = add_sph_u("TMP", (w * 0.18, 1.85, d * 0.42), (0.22, 0.22, 0.22))
    assign_mat(lantern_r, mats["SM_Orange"])
    parts.append(lantern_r)
    post_l = add_cyl_u("TMP", (-w * 0.18, 1.05, d * 0.42), (0.08, 0.85, 0.08), vertices=8)
    assign_mat(post_l, mats["SM_Steel"])
    parts.append(post_l)
    lantern_l = add_sph_u("TMP", (-w * 0.18, 1.85, d * 0.42), (0.22, 0.22, 0.22))
    assign_mat(lantern_l, mats["SM_Orange"])
    parts.append(lantern_l)
    canopy = add_box_u("TMP", (0.0, 1.55, d * 0.38), (w * 0.42, 0.06, d * 0.18))
    assign_mat(canopy, mats["SM_Black"])
    parts.append(canopy)
    bench = add_box_u("TMP", (0.0, 0.42, d * 0.40), (w * 0.28, 0.12, 0.18))
    assign_mat(bench, mats["SM_Graphite"])
    parts.append(bench)
    beacon = add_sph_u("TMP", (0.0, 2.72, -d * 0.04), (0.20, 0.20, 0.20))
    assign_mat(beacon, mats["SM_Cyan"])
    parts.append(beacon)
    return finish(parts, name, "45_Hero_Inn", (14.0, 32.0, 0.0))


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
    UNITY_BUILDINGS.mkdir(parents=True, exist_ok=True)
    src = EXPORT_DIR / f"{name}.fbx"
    if not src.is_file():
        print(f"[SM] Missing export {src}")
        return
    dst = UNITY_BUILDINGS / f"{name}.fbx"
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
    print("[SM] === Hero building kits ===")
    reset_scene()
    mats = create_palette()
    only = parse_only()
    buildings = [
        build_hab(mats),
        build_commons(mats),
        build_power(mats),
        build_farm(mats),
        build_camp(mats),
        build_mine(mats),
        build_defense(mats),
        build_pad(mats),
        build_guild(mats),
        build_lab(mats),
        build_loom(mats),
        build_spire(mats),
        build_archive(mats),
        build_ops(mats),
        build_workshop(mats),
        build_workshop_tall(mats),
        build_inn(mats),
    ]
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))
    print(f"[SM] Saved {BLEND_OUT}")
    for b in buildings:
        if only is not None and b.name not in only:
            continue
        print(" ", dims_report(b))
        export_one(b)
        copy_to_unity(b.name)
    print("[SM] === Done ===")


if __name__ == "__main__":
    main()
