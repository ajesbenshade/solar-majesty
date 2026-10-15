#!/usr/bin/env python3
"""Solar Majesty worker-bot villagers (canon: the player is the central AI, villagers are worker bots).

Replaces the four human colonist suits of art batch 3 (sm_colonists.py, PR #75) with four worker-bot
variants that plug into the same hookup (ColonistArt -> Resources/Colonists, Humanoid Animator, the
original colony-stride walk of PR #79, TerrainFollow grounding). Only meshes/prefabs swap:

  * Same skeleton as the suits (sm_colonists.BONES: Kevin Iglesias-style joint names, re-proportioned),
    so the Stride clip and the new Idle retarget unchanged. Every part is rigidly skinned to one bone.
  * Blender Z up, the bot faces -Y (Unity +Z after the FBX axes below), pivot at the soles (z = 0).
  * Two materials: slot 0 SM_Art_WorkerBot_Chassis (shared 128 px palette atlas T_WorkerBot_Chassis.png,
    emission map T_WorkerBot_Chassis_Emit.png lights only the cyan sensor cell), slot 1
    SM_Art_WorkerBot_Role_<Variant> (solid role colour). Role faces use U < 0.5 for paint and U > 0.5 for
    the beacon; T_WorkerBot_RoleGlow.png (a 2 px mask) makes only the beacon glow strongly.

Family: the white-ceramic / black-carbon / cyan-sensor language of the concept sheets
(SM_Unit_EngineerBot, SM_Unit_CourierBot), upgraded from the in-game capsule + band placeholder:
the capsule's banded barrel becomes the boxy chassis with a painted role band. No face or human
features: one round sensor eye in a visor slot, a beacon instead of hair/crest.

  Engineer     yellow   welder arm (R), twin gas bottles on the back, hazard band
  Hydroponics  green    sprayer wand (R), nutrient tank + planter hopper on the back
  Medic        red      the Repair bot (role ID stays Medic): repair-kit arm (R, wrench claw), docked repair drone on the back
  Hauler       violet   cargo clamps on both arms, back rack with a strapped crate, wide shoulders

Budget ~1.5-2k triangles per variant.

Usage (Blender 4.2 LTS):
  blender -b --python Blender/scripts/sm_worker_bots.py -- --export <dir> [--blend <file.blend>] \
      [--review <dir>] [--only Engineer,Hauler]
Exports SM_WorkerBot_<Variant>.fbx (Engineer, Hydroponics, Medic = Repair bot, Hauler; rig + mesh, T-pose, no animation) and SM_WorkerBot@Idle.fbx
(armature-only, original 4 s idle loop: sensor scan, servo settle, tool check).
"""

from __future__ import annotations

import math
import os
import struct
import sys
import zlib

import bmesh
import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sm_colonists as base          # noqa: E402  skeleton, Kit primitives, FBX export settings
import sm_colonist_stride as stride  # noqa: E402  pose helpers (set_bone, two_bone, export_anim)

srgb = base.srgb

# ---------------------------------------------------------------------------- palette
ATLAS, CELL = 128, 32
SWATCH = {
    0: (0.93, 0.93, 0.91),   # white ceramic chassis
    1: (0.72, 0.74, 0.76),   # ceramic shade / panel seams
    2: (0.11, 0.12, 0.13),   # black carbon (joints, belly, pistons)
    3: (0.25, 0.27, 0.30),   # dark gunmetal
    4: (0.55, 0.58, 0.62),   # steel
    5: None,                 # cyan sensor (gradient, emissive)
    6: (0.04, 0.05, 0.06),   # visor glass black
    7: (0.95, 0.47, 0.12),   # orange hazard (family accent from the concept sheets)
    8: (0.62, 0.45, 0.28),   # crate tan
    9: (0.98, 0.98, 0.97),   # decal white
}
CERAMIC, SHADE, CARBON, GUN, STEEL, SENSOR, GLASS, HAZARD, TAN, DECAL = range(10)
PAINT, BEACON = -1, -2      # role slot: paint (dim) / beacon (glow)
SENSOR_LO, SENSOR_HI = (0.05, 0.55, 0.75), (0.55, 0.97, 1.0)

VARIANTS = {
    "Engineer":    (0.98, 0.77, 0.10),
    "Hydroponics": (0.36, 0.80, 0.26),
    "Medic":       (0.92, 0.18, 0.16),   # Repair bot art; saved role ID / prefab name stay "Medic"
    "Hauler":      (0.58, 0.36, 0.92),
}


def _png(path, w, h, pixel):
    rows = []
    for y in range(h):
        row = bytearray([0])
        for x in range(w):
            row += bytes(int(round(max(0.0, min(1.0, c)) * 255)) for c in pixel(x, y))
        rows.append(bytes(row))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b""))


def _cell(x, y):
    return (y // CELL) * 4 + x // CELL


def build_textures(tex_dir):
    def albedo(x, y):
        i = _cell(x, y)
        if i == SENSOR:
            t = 1.0 - (y % CELL) / (CELL - 1)
            return tuple(SENSOR_LO[k] + (SENSOR_HI[k] - SENSOR_LO[k]) * t for k in range(3))
        return SWATCH.get(i) or (1.0, 0.0, 1.0)

    def emit(x, y):
        return albedo(x, y) if _cell(x, y) == SENSOR else (0.0, 0.0, 0.0)

    paths = {k: os.path.join(tex_dir, n) for k, n in (
        ("albedo", "T_WorkerBot_Chassis.png"), ("emit", "T_WorkerBot_Chassis_Emit.png"),
        ("glow", "T_WorkerBot_RoleGlow.png"))}
    _png(paths["albedo"], ATLAS, ATLAS, albedo)
    _png(paths["emit"], ATLAS, ATLAS, emit)
    _png(paths["glow"], 2, 1, lambda x, y: (0.12,) * 3 if x == 0 else (1.0,) * 3)
    imgs = {}
    for k, p in paths.items():
        img = bpy.data.images.load(p, check_existing=False)
        img.name = os.path.splitext(os.path.basename(p))[0]
        if k != "albedo":
            img.colorspace_settings.name = "Non-Color" if k == "glow" else "sRGB"
        imgs[k] = img
    return imgs


class BotKit(base.Kit):
    """Kit with the worker-bot atlas and paint/beacon UVs on the role slot."""

    def _merge(self, tmp, mat, bone, smooth, swatch):
        gid = self._gid(bone)
        vmap = {}
        zs = [v.co.z for v in tmp.verts]
        zmin, zmax = (min(zs), max(zs)) if zs else (0, 1)
        for v in tmp.verts:
            nv = self.bm.verts.new(v.co)
            nv[self.dl][gid] = 1.0
            vmap[v] = nv
        for f in tmp.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = 1 if swatch < 0 else 0
            nf.smooth = smooth
            for loop in nf.loops:
                if swatch < 0:
                    loop[self.uv].uv = (0.25 if swatch == PAINT else 0.75, 0.5)
                    continue
                col, row = swatch % 4, swatch // 4
                u0, v0, s = col * CELL / ATLAS, 1.0 - (row + 1) * CELL / ATLAS, CELL / ATLAS
                if swatch == SENSOR:
                    t = (loop.vert.co.z - zmin) / max(1e-6, zmax - zmin)
                    loop[self.uv].uv = (u0 + s * 0.5, v0 + s * (0.06 + 0.88 * t))
                else:
                    loop[self.uv].uv = (u0 + s * 0.5, v0 + s * 0.5)
        tmp.free()


# ---------------------------------------------------------------------------- the bot
# Bone layout (sm_colonists.BONES): hips 0.50, spine 0.58-0.70, chest 0.70-0.86, neck 0.86-0.92,
# head 0.92-1.14, arms along +-X at z ~0.82 (upper 0.19-0.35, fore 0.35-0.49, hand 0.49-0.58),
# legs at x +-0.10 (thigh 0.48-0.27, shin 0.27-0.075, ankle 0.075, toe joint y -0.075 z 0.03).

def build_body(k: BotKit, variant: str):
    hauler = variant == "Hauler"
    wide = 1.10 if hauler else 1.0

    # Head: a boxy sensor block, black visor slot with one big round cyan eye + a slit, ear pods,
    # role paint band over the top, and the role beacon on a short mast (biggest read from above).
    hz = 1.03
    k.box((0.31, 0.27, 0.21), (0, 0.0, hz), CERAMIC, "B-head", bevel=0.035)
    k.box((0.25, 0.03, 0.11), (0, -0.131, hz + 0.005), GLASS, "B-head", bevel=0.012)
    k.cyl(0.042, 0.02, (0.0, -0.148, hz + 0.005), SENSOR, "B-head", rot=(90, 0, 0), verts=10)
    k.cyl(0.05, 0.008, (0.0, -0.142, hz + 0.005), GUN, "B-head", rot=(90, 0, 0), verts=10)
    k.box((0.17, 0.275, 0.012), (0, 0.0, hz + 0.106), PAINT, "B-head")
    for x in (1, -1):
        k.cyl(0.05, 0.035, (0.168 * x, 0.0, hz), CARBON, "B-head", rot=(0, 90, 0), verts=8)
    k.cyl(0.022, 0.05, (0, 0.04, hz + 0.135), CARBON, "B-head", verts=6)
    k.sphere(0.072, (0, 0.04, hz + 0.165), BEACON, "B-head", u=10, v=6, cut_below=-0.15)
    k.cyl(0.08, 0.016, (0, 0.04, hz + 0.155), GUN, "B-head", verts=10)
    k.cyl(0.065, 0.05, (0, 0, 0.89), CARBON, "B-neck", verts=8)

    # Chassis: the placeholder's banded barrel, now a beveled ceramic box with a painted role band
    # all the way round (front, sides, top) and an orange hazard chevron plate on the chest.
    k.box((0.44 * wide, 0.30, 0.30), (0, 0.0, 0.765), CERAMIC, "B-chest", bevel=0.04, taper=(1.06, 1.0))
    k.box((0.455 * wide, 0.312, 0.07), (0, 0.0, 0.80), PAINT, "B-chest", bevel=0.01)
    k.box((0.20, 0.02, 0.07), (0, -0.155, 0.70), GUN, "B-chest", bevel=0.008)
    for i in range(3):
        k.box((0.035, 0.012, 0.045), (-0.06 + 0.06 * i, -0.166, 0.70), HAZARD, "B-chest", rot=(0, 30, 0))
    k.box((0.30 * wide, 0.20, 0.035), (0, 0.0, 0.925), PAINT, "B-chest", bevel=0.01)   # top deck
    # Waist: black carbon bellows, pelvis block with hip pods.
    k.cyl(0.13, 0.13, (0, 0, 0.635), CARBON, "B-spine", verts=10, r2=0.15)
    k.cyl(0.145, 0.025, (0, 0, 0.64), GUN, "B-spine", verts=10)
    k.box((0.30 * wide, 0.22, 0.11), (0, 0.0, 0.525), GUN, "B-hips", bevel=0.025)
    for x in (1, -1):
        k.cyl(0.06, 0.05, (0.15 * x, 0.0, 0.49), CARBON, "B-hips", rot=(0, 90, 0), verts=8)

    # Arms: role-painted shoulder pods (top surface reads from the overseer camera), carbon upper arm,
    # ceramic forearm shell. The left hand is a 2-finger gripper; the right is the role tool.
    pad = 1.25 if hauler else 1.0
    for s, x in (("L", 1), ("R", -1)):
        k.box((0.15 * pad, 0.17 * pad, 0.12 * pad), (0.215 * x, 0.0, 0.86), PAINT, f"B-upperArm.{s}", bevel=0.03)
        k.seg((0.25 * x, 0, 0.83), (0.35 * x, 0, 0.82), 0.04, CARBON, f"B-upperArm.{s}", verts=6)
        k.sphere(0.05, (0.352 * x, 0, 0.82), GUN, f"B-forearm.{s}", u=6, v=4)
        k.box((0.13, 0.11, 0.11), (0.43 * x, 0, 0.815), CERAMIC, f"B-forearm.{s}", bevel=0.025)
        k.box((0.02, 0.112, 0.112), (0.47 * x, 0, 0.815), HAZARD, f"B-forearm.{s}")
    k.box((0.06, 0.08, 0.08), (0.52, 0, 0.81), GUN, "B-hand.L", bevel=0.015)
    for y in (0.03, -0.03):
        k.box((0.075, 0.022, 0.05), (0.575, y, 0.80), CARBON, "B-hand.L")

    # Legs: carbon piston thighs, ceramic knee guards and shin shells, big dark feet split at the toe.
    for s, x in (("L", 1), ("R", -1)):
        k.seg((0.095 * x, 0, 0.47), (0.10 * x, 0, 0.29), 0.062, CARBON, f"B-thigh.{s}", verts=7)
        k.box((0.11, 0.075, 0.09), (0.10 * x, -0.04, 0.275), CERAMIC, f"B-shin.{s}", bevel=0.02)
        k.box((0.12, 0.13, 0.16), (0.10 * x, 0.005, 0.175), CERAMIC, f"B-shin.{s}", bevel=0.025, taper=(1.0, 0.9))
        k.box((0.122, 0.132, 0.025), (0.10 * x, 0.005, 0.215), PAINT, f"B-shin.{s}")
        k.box((0.15, 0.18, 0.095), (0.10 * x, -0.005, 0.0475), GUN, f"B-foot.{s}", bevel=0.02)
        k.box((0.14, 0.09, 0.07), (0.10 * x, -0.115, 0.035), CARBON, f"B-toe.{s}", bevel=0.02)


def build_role(k: BotKit, variant: str):
    hx = -0.535   # right hand / tool mount (bone B-hand.R points to -X)
    if variant == "Engineer":
        # Welder arm: yellow housing, steel torch barrel, cyan arc tip; twin gas bottles on the back.
        k.box((0.10, 0.11, 0.11), (hx, 0, 0.81), PAINT, "B-hand.R", bevel=0.02)
        k.cyl(0.028, 0.14, (hx - 0.11, 0, 0.81), STEEL, "B-hand.R", rot=(0, 90, 0), verts=8, r2=0.02)
        k.cyl(0.018, 0.02, (hx - 0.185, 0, 0.81), SENSOR, "B-hand.R", rot=(0, 90, 0), verts=6)
        for x in (0.065, -0.065):
            k.cyl(0.055, 0.24, (x, 0.2, 0.78), STEEL, "B-chest", verts=8)
            k.sphere(0.056, (x, 0.2, 0.90), PAINT, "B-chest", u=8, v=4, cut_below=0.0)
        k.seg((-0.065, 0.2, 0.93), (-0.30, 0.03, 0.86), 0.012, CARBON, "B-chest", verts=5)
    elif variant == "Hydroponics":
        # Sprayer wand with a green spray head; nutrient tank + planter hopper on the back.
        k.box((0.08, 0.09, 0.09), (hx, 0, 0.81), GUN, "B-hand.R", bevel=0.015)
        k.seg((hx - 0.03, -0.01, 0.81), (hx - 0.24, -0.06, 0.80), 0.016, STEEL, "B-hand.R", verts=6)
        k.cyl(0.05, 0.07, (hx - 0.27, -0.07, 0.80), PAINT, "B-hand.R", rot=(0, 90, 0), verts=8, r2=0.025)
        k.cyl(0.09, 0.26, (0.0, 0.21, 0.80), CERAMIC, "B-chest", verts=10)
        k.cyl(0.093, 0.06, (0.0, 0.21, 0.94), PAINT, "B-chest", verts=10)
        k.cyl(0.093, 0.04, (0.0, 0.21, 0.69), PAINT, "B-chest", verts=10)
        k.box((0.26, 0.10, 0.08), (0, 0.20, 0.63), PAINT, "B-chest", bevel=0.015)       # planter hopper
        k.seg((-0.06, 0.21, 0.72), (-0.30, 0.05, 0.80), 0.014, CARBON, "B-chest", verts=5)
    elif variant == "Medic":    # the Repair bot
        # Repair-kit arm: red case with a white wrench decal and a two-prong claw; docked drone on the back.
        k.box((0.12, 0.12, 0.12), (hx - 0.01, 0, 0.81), PAINT, "B-hand.R", bevel=0.02)
        k.box((0.08, 0.012, 0.02), (hx - 0.01, -0.066, 0.81), DECAL, "B-hand.R", rot=(0, 35, 0))
        for y in (0.035, -0.035):
            k.box((0.09, 0.02, 0.03), (hx - 0.11, y, 0.81), STEEL, "B-hand.R")
        k.box((0.06, 0.14, 0.06), (0, 0.19, 0.80), GUN, "B-chest", bevel=0.012)        # dock arm
        k.cyl(0.14, 0.05, (0, 0.20, 0.97), CERAMIC, "B-chest", verts=12)                 # drone body
        k.cyl(0.145, 0.025, (0, 0.20, 0.97), PAINT, "B-chest", verts=12)                 # red ring
        k.cyl(0.03, 0.015, (0, 0.20, 1.0), SENSOR, "B-chest", verts=8)
        for a in (45, 135, 225, 315):
            ax, ay = math.cos(math.radians(a)) * 0.15, math.sin(math.radians(a)) * 0.15
            k.cyl(0.04, 0.008, (ax, 0.20 + ay, 0.99), CARBON, "B-chest", verts=8)        # rotor discs
    elif variant == "Hauler":
        # Cargo clamps on both forearms (violet C-jaws) and a back rack with a strapped crate.
        for s, x in (("L", 1), ("R", -1)):
            b = f"B-hand.{s}"
            k.box((0.06, 0.16, 0.06), (0.53 * x, 0, 0.84), PAINT, b, bevel=0.012)
            for y in (0.07, -0.07):
                k.box((0.09, 0.03, 0.10), (0.575 * x, y, 0.80), PAINT, b, bevel=0.01)
        for x in (1, -1):
            k.seg((0.15 * x, 0.17, 0.56), (0.15 * x, 0.19, 1.12), 0.02, STEEL, "B-chest", verts=6)
        k.box((0.34, 0.06, 0.05), (0, 0.17, 0.60), PAINT, "B-chest")
        k.box((0.36, 0.26, 0.30), (0, 0.31, 0.93), TAN, "B-chest", bevel=0.02)
        k.box((0.38, 0.27, 0.05), (0, 0.31, 0.99), PAINT, "B-chest")
        k.box((0.06, 0.28, 0.31), (0, 0.31, 0.93), PAINT, "B-chest")
        k.box((0.34, 0.06, 0.04), (0, 0.17, 1.10), STEEL, "B-chest")


# ---------------------------------------------------------------------------- materials

def make_materials(imgs):
    ch = bpy.data.materials.new("SM_Art_WorkerBot_Chassis")
    ch.use_nodes = True
    nt = ch.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = imgs["albedo"]; t.interpolation = "Closest"
    e = nt.nodes.new("ShaderNodeTexImage"); e.image = imgs["emit"]; e.interpolation = "Closest"
    nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(e.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 2.0
    bsdf.inputs["Roughness"].default_value = 0.42
    roles = {}
    for v, c in VARIANTS.items():
        m = bpy.data.materials.new("SM_Art_WorkerBot_Role_" + v)
        m.use_nodes = True
        nt = m.node_tree
        b = nt.nodes.get("Principled BSDF")
        lin = srgb(*c)
        b.inputs["Base Color"].default_value = (*lin, 1.0)
        b.inputs["Roughness"].default_value = 0.38
        g = nt.nodes.new("ShaderNodeTexImage"); g.image = imgs["glow"]; g.interpolation = "Closest"
        mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1.0
        mul.inputs["A"].default_value = (*lin, 1.0)
        nt.links.new(g.outputs["Color"], mul.inputs["B"])
        nt.links.new(mul.outputs["Result"], b.inputs["Emission Color"])
        b.inputs["Emission Strength"].default_value = 2.5
        roles[v] = m
    return ch, roles


# ---------------------------------------------------------------------------- idle (original)
IDLE_FPS, IDLE_LEN = 30, 120     # 4.0 s loop


def _smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def idle_pose(rig, f):
    """Worker-bot idle: planted stance, servo settle, sensor sweep left/right, tool-arm check."""
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
    t = f / IDLE_LEN
    w = math.tau * t
    hz = 0.488 + 0.004 * math.cos(2 * w)                       # servo settle, 2 per loop
    stride.set_bone(rig, "B-hips", Quaternion((0, 0, 1), math.radians(2.0) * math.sin(w)), Vector((0, 0, hz)))
    stride.set_bone(rig, "B-spine", Quaternion((1, 0, 0), math.radians(2.0)))
    r_chest = Quaternion((1, 0, 0), math.radians(3.0 + 1.0 * math.cos(2 * w)))
    stride.set_bone(rig, "B-chest", r_chest)
    stride.set_bone(rig, "B-neck", r_chest)
    # Sensor sweep: hold centre, snap-ease left, hold, sweep right, hold, back (mechanical, not breathing).
    keys = [(0.00, 0.0), (0.12, 0.0), (0.22, 28.0), (0.40, 28.0), (0.55, -24.0), (0.72, -24.0), (0.84, 0.0), (1.0, 0.0)]
    for (a0, y0), (a1, y1) in zip(keys, keys[1:]):
        if a0 <= t <= a1:
            yaw = y0 + (y1 - y0) * _smooth((t - a0) / (a1 - a0))
            break
    tilt = math.radians(-4.0) * math.sin(2 * w)
    stride.set_bone(rig, "B-head", Quaternion((0, 0, 1), math.radians(yaw)) @ Quaternion((1, 0, 0), tilt) @ r_chest)
    # Arms hang 14 deg off the body, elbows bent; the tool arm lifts to check its tool mid-loop.
    lift = _smooth((t - 0.45) / 0.08) * (1 - _smooth((t - 0.70) / 0.08))
    for s, x in (("L", 1.0), ("R", -1.0)):
        stride.set_bone(rig, f"B-shoulder.{s}", r_chest)
        ab = math.radians(14.0 + 1.5 * math.cos(2 * w))
        fw = math.radians(6.0 + (30.0 * lift if s == "R" else 0.0))
        up_dir = Vector((x * math.sin(ab), -math.cos(ab) * math.sin(fw), -math.cos(ab) * math.cos(fw))).normalized()
        r_up = stride.rest_dir(f"B-upperArm.{s}").rotation_difference(up_dir)
        stride.set_bone(rig, f"B-upperArm.{s}", r_up)
        bend = math.radians(28.0 + (55.0 * lift if s == "R" else 0.0))
        perp = (stride.FWD - up_dir * stride.FWD.dot(up_dir)).normalized()
        axis = up_dir.cross(perp).normalized()
        r_fore = Quaternion(axis, bend) @ r_up
        stride.set_bone(rig, f"B-forearm.{s}", r_fore)
        roll = Quaternion(stride.rest_dir(f"B-hand.{s}"), math.radians(35.0) * lift) if s == "R" else Quaternion()
        stride.set_bone(rig, f"B-hand.{s}", r_fore @ roll)
    # Legs: feet flat at rest position, knees solved for the lowered hips.
    for s, x in (("L", 0.10), ("R", -0.10)):
        ankle = Vector((x, 0.012, stride.ANKLE_Z))
        hip = rig.pose.bones[f"B-thigh.{s}"].head.copy()
        knee, ankle_hit = stride.two_bone(hip, ankle)
        r_th = stride.rest_dir(f"B-thigh.{s}").rotation_difference((knee - hip).normalized())
        stride.set_bone(rig, f"B-thigh.{s}", r_th)
        r_sh = (r_th @ stride.rest_dir(f"B-shin.{s}")).rotation_difference((ankle_hit - knee).normalized()) @ r_th
        stride.set_bone(rig, f"B-shin.{s}", r_sh, knee)
        stride.set_bone(rig, f"B-foot.{s}", Quaternion(), ankle_hit)
        stride.set_bone(rig, f"B-toe.{s}", Quaternion())


def key_idle(rig):
    scn = bpy.context.scene
    scn.render.fps = IDLE_FPS
    rig.animation_data_create()
    act = bpy.data.actions.new("Idle")
    rig.animation_data.action = act
    for f in range(0, IDLE_LEN + 1, 2):
        scn.frame_set(f)
        idle_pose(rig, f % IDLE_LEN)
        for pb in rig.pose.bones:
            pb.keyframe_insert("location", frame=f, group=pb.name)
            pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
    scn.frame_start, scn.frame_end = 0, IDLE_LEN
    return act


# ---------------------------------------------------------------------------- build / review

def build_variant(variant, chassis, role_mat, x_offset=0.0):
    name = "SM_WorkerBot_" + variant
    rig = base.build_rig(name)
    k = BotKit(name)
    build_body(k, variant)
    build_role(k, variant)
    obj = k.to_object([chassis, role_mat])
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    rig.location.x = x_offset
    return rig, obj


def review(rigs, out_dir, idle_act):
    scn = bpy.context.scene
    scn.render.engine = "CYCLES"
    scn.cycles.samples = 20
    scn.cycles.use_denoising = True
    scn.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("Review")
    scn.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.66, 0.80, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0))
    gm = bpy.data.materials.new("Ground"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*srgb(0.52, 0.42, 0.33), 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.95
    bpy.context.active_object.data.materials.append(gm)
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.2
    sun.data.angle = math.radians(8)
    scn.collection.objects.link(sun)
    sun.rotation_euler = Euler((math.radians(48), 0, math.radians(-38)))
    cam_d = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_d)
    scn.collection.objects.link(cam)
    scn.camera = cam
    for r in rigs:   # pose all bots on the idle (frame 20: arms down, sensor centred)
        r.animation_data_create()
        r.animation_data.action = idle_act
    scn.frame_set(20)

    def shoot(path, target, yaw_deg, pitch_deg, ortho=None, persp_dist=None, res=(1600, 800)):
        scn.render.resolution_x, scn.render.resolution_y = res
        yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
        d = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
        if ortho:
            cam_d.type = "ORTHO"; cam_d.ortho_scale = ortho
            cam.location = Vector(target) + d * 30
        else:
            cam_d.type = "PERSP"; cam_d.lens = 50
            cam.location = Vector(target) + d * persp_dist
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        scn.render.filepath = path
        bpy.ops.render.render(write_still=True)

    n = len(rigs)
    shoot(os.path.join(out_dir, "workerbots_lineup.png"), (0, 0, 0.62), 28, 14, ortho=1.15 * n + 0.5)
    shoot(os.path.join(out_dir, "workerbots_lineup_back.png"), (0, 0, 0.62), 180 + 28, 14, ortho=1.15 * n + 0.5)
    # Overseer angle: high, perspective from ~18 m like the default RTS zoom, small on screen.
    shoot(os.path.join(out_dir, "workerbots_overseer.png"), (0, 0, 0.5), 35, 52, persp_dist=18.0, res=(1280, 720))
    shoot(os.path.join(out_dir, "workerbots_overseer_close.png"), (0, 0, 0.5), 35, 52, persp_dist=8.0, res=(1280, 720))


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(flag, default=None):
        return argv[argv.index(flag) + 1] if flag in argv else default

    export_dir, blend, review_dir, only = opt("--export"), opt("--blend"), opt("--review"), opt("--only")
    names = [v for v in VARIANTS if not only or v in only.split(",")]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    tex_dir = export_dir or os.path.dirname(os.path.abspath(blend or review_dir or "."))
    os.makedirs(tex_dir, exist_ok=True)
    imgs = build_textures(tex_dir)
    chassis, roles = make_materials(imgs)

    idle_rig = base.build_rig("SM_WorkerBot_Idle")
    idle_act = key_idle(idle_rig)
    if export_dir:
        stride.export_anim(idle_rig, os.path.join(export_dir, "SM_WorkerBot@Idle.fbx"))
    bpy.data.objects.remove(idle_rig)

    rigs = []
    for i, v in enumerate(names):
        rig, obj = build_variant(v, chassis, roles[v], (i - (len(names) - 1) * 0.5) * 1.15)
        ws = [obj.matrix_world @ vv.co for vv in obj.data.vertices]
        print(f"[SM] SM_WorkerBot_{v}: {tri_count(obj)} tris, {len(obj.data.vertices)} verts, "
              f"z {min(w.z for w in ws):.3f}..{max(w.z for w in ws):.3f} m, bones {len(rig.data.bones)}, "
              f"mats {[m.name for m in obj.data.materials]}")
        if export_dir:
            base.export_fbx(rig, obj, os.path.join(export_dir, f"SM_WorkerBot_{v}.fbx"))
        rigs.append(rig)
    if blend:
        for img in bpy.data.images:
            img.pack()
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend), compress=True)
    if review_dir:
        os.makedirs(review_dir, exist_ok=True)
        review(rigs, review_dir, idle_act)


if __name__ == "__main__":
    main()
