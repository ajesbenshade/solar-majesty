"""Solar Majesty — phase-2 civ buildings.

Inn, GuildHall, HAB, Power, Defense for Lumen Concord, Tether Exchange,
Bastion Mandate and Pathfinder Accord.

4x4 cells * 1.5 m = 6 m plot. Pivot at ground centre, front = Blender -Y
(Unity -Z). Shared 2048 citadel trim sheet. Material slots in SLOTS order:
Trim, Carbon, TeamColour, Accent, plus CoreEmission (small cyan Core motif)
and ChargerAmber (HAB pods only). LODs authored, not decimated.

Heights stay under 10 m (Dome castle is 12.59 m, the shortest castle).
Footprint stays inside 5.85 m.

Exports: SM_{Civ}_{Building}.fbx with {name}_LOD0/1/2 and empties Gate,
Core_Socket (Defense also GunPivot). No .meta, no C#.

Run: blender -b -P sm_civ_buildings.py -- --out DIR --render DIR
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent
sys.path.insert(0, str(SCRIPT_DIR))

from civ_common import (  # noqa: E402
    B, TEX_DIR, SLOTS, bounds, empty, export_fbx, finish_mesh, link,
    make_materials, reset, rz, tris,
)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def _arg(name, default=None):
    return ARGS[ARGS.index(name) + 1] if name in ARGS else default


OUT_DIR = Path(_arg("--out", str(PROJECT_ROOT / "Assets" / "Resources" / "Buildings" / "Civ")))
RENDER_DIR = Path(_arg("--render", str(PROJECT_ROOT / "Docs" / "Art" / "civ-buildings")))
BLEND_DIR = _arg("--blend-dir", None)
ONLY = set(ARGS[ARGS.index("--only") + 1].split(",")) if "--only" in ARGS else None

FOOT = 6.0
MAX_FOOT = 5.85
MAX_H = 10.0
LOD_TARGETS = (4000, 1500, 500)
PLOT_HALF = MAX_FOOT / 2.0

CIVS = ("Lumen", "Exchange", "Mandate", "Pathfinder")
BUILDINGS = ("Inn", "GuildHall", "HAB", "Power", "Defense")

# Review-only tints (FBX materials stay neutral). TeamColour is the civ colour.
REVIEW_TINT = {
    "Lumen":      {"team": (0.682, 0.467, 0.106), "trim": (0.957, 0.922, 0.847)},  # bronze / ivory
    "Exchange":   {"team": (0.45, 0.28, 0.72),    "trim": (0.431, 0.463, 0.518)},  # violet / graphite
    "Mandate":    {"team": (0.557, 0.231, 0.165), "trim": (0.663, 0.722, 0.627)},  # oxide red / sage
    "Pathfinder": {"team": (0.051, 0.600, 0.329), "trim": (0.45, 0.58, 0.60)},     # emerald / teal
}


def nseg(L, a=20, b=12, c=8):
    return (a, b, c)[L]


def Rx(deg):
    return Matrix.Rotation(math.radians(deg), 4, "X")


def Ry(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Y")


def T(*c):
    return Matrix.Translation(Vector(c) if len(c) != 1 else c[0])


def TR(loc, rot):
    return T(*loc) @ rot


# ---------------------------------------------------------------------------
# Shared ornaments (Core motif, door, accent, pods)
# ---------------------------------------------------------------------------

def core_shard(b, c, scale=1.0, n=6):
    """Small cyan bipyramid — the one Core motif on the building."""
    b.bipyramid(c, 0.16 * scale, 0.22 * scale, 0.18 * scale, n, "CORE")
    b.cyl((c[0], c[1], c[2] - 0.06 * scale), 0.18 * scale, 0.18 * scale, 0.10 * scale, 8, "CARB",
          smooth=False, caps=True)


def claw_over_door(b, y, z, L, scale=1.0):
    """Concord: two beams meeting above the door, cyan shard in the claw."""
    s = scale
    b.beam((-0.55 * s, y, z), (0.0, y - 0.08 * s, z + 0.70 * s), 0.12 * s, 0.10 * s, "CARB")
    b.beam((0.55 * s, y, z), (0.0, y - 0.08 * s, z + 0.70 * s), 0.12 * s, 0.10 * s, "CARB")
    if L <= 1:
        b.beam((-0.55 * s, y, z), (0.0, y - 0.08 * s, z + 0.70 * s), 0.04 * s, 0.04 * s, "TEAM")
        b.beam((0.55 * s, y, z), (0.0, y - 0.08 * s, z + 0.70 * s), 0.04 * s, 0.04 * s, "TEAM")
    core_shard(b, (0.0, y - 0.10 * s, z + 0.78 * s), 0.85 * s)
    return (0.0, y - 0.10 * s, z + 0.78 * s)


def caged_light(b, c, L, h=0.55):
    """Exchange: cyan in a carbon cage, typically on a mast."""
    cx, cy, cz = c
    core_shard(b, (cx, cy, cz + h * 0.35), 0.9)
    if L <= 1:
        n = nseg(L, 8, 6, 4)
        b.cyl((cx, cy, cz), 0.22, 0.22, 0.06, n, "CARB", smooth=False)
        b.cyl((cx, cy, cz + h), 0.22, 0.18, 0.06, n, "CARB", smooth=False)
        for i in range(4 if L == 0 else 3):
            a = math.radians(45 + 90 * i)
            x, y = 0.20 * math.cos(a), 0.20 * math.sin(a)
            b.beam((cx + x, cy + y, cz + 0.04), (cx + x, cy + y, cz + h), 0.035, 0.035, "CARB")
    return (cx, cy, cz + h * 0.35)


def crown_window(b, c, L, w=0.7, h=0.38):
    """Mandate: cyan window slot under a small crown."""
    cx, cy, cz = c
    b.box((cx, cy, cz), (w + 0.16, 0.16, h + 0.16), "CARB")
    if L <= 1:
        b.box((cx, cy - 0.02, cz), (w, 0.10, h), "GLASS")
    core_shard(b, (cx, cy - 0.12, cz), 0.7)
    b.box((cx, cy, cz + h * 0.5 + 0.18), (w * 0.6, 0.22, 0.10), "TEAM", top=(w * 0.35, 0.16))
    return (cx, cy - 0.12, cz)


def underfloor_light(b, c, L):
    """Pathfinder: cyan slung under the raised floor, in a short carbon sling."""
    cx, cy, cz = c
    core_shard(b, c, 1.0)
    if L <= 1:
        b.box((cx, cy, cz + 0.18), (0.70, 0.16, 0.06), "CARB")
        b.beam((cx - 0.35, cy, cz + 0.18), (cx - 0.35, cy, cz + 0.55), 0.06, 0.06, "CARB")
        b.beam((cx + 0.35, cy, cz + 0.18), (cx + 0.35, cy, cz + 0.55), 0.06, 0.06, "CARB")
    return c


def door_vent(b, c, w, h, depth=0.14, team=True):
    cx, cy, cz = c
    b.box((cx, cy, cz), (w + 0.16, depth, h + 0.16), "CARB")
    b.box((cx, cy - 0.02, cz), (w, depth * 0.7, h), "VENT")
    if team:
        b.box((cx, cy - depth * 0.4, cz + h * 0.5 + 0.06), (w + 0.04, 0.05, 0.07), "TEAM")


def accent_banners(b, c, L, span=0.9, drop=1.1, thick=0.05):
    """Player-accent banners, kept on every LOD."""
    cx, cy, cz = c
    for sgn in (-1, 1):
        x = cx + sgn * span
        b.beam((x, cy, cz), (x, cy, cz - drop), thick + 0.02, 0.04, "ACCENT")
        if L <= 1:
            b.box((x, cy, cz + 0.05), (thick + 0.08, 0.08, 0.08), "CARB")


def amber_pod(b, c, length, radius, axis="x", L=0):
    """HAB charger pod. axis 'x' or 'y'. Carbon collar + amber body."""
    cx, cy, cz = c
    n = nseg(L, 16, 12, 8)
    if axis == "x":
        # cyl is Z-up; rotate around Y so the axis runs along X
        M = T(cx, cy, cz) @ Ry(90)
        b.cyl((0, 0, -length * 0.5), radius, radius, length, n, "AMBER", M, smooth=True)
        b.cyl((0, 0, -0.08), radius * 1.12, radius * 1.12, 0.16, n, "CARB", M, smooth=False)
        if L == 0:
            b.cyl((0, 0, -length * 0.42), radius * 1.08, radius * 1.08, 0.08, n, "CARB", M, smooth=False)
            b.cyl((0, 0, length * 0.34), radius * 1.08, radius * 1.08, 0.08, n, "CARB", M, smooth=False)
    else:
        M = T(cx, cy, cz) @ Rx(90)
        b.cyl((0, 0, -length * 0.5), radius, radius, length, n, "AMBER", M, smooth=True)
        b.cyl((0, 0, -0.08), radius * 1.12, radius * 1.12, 0.16, n, "CARB", M, smooth=False)
        if L == 0:
            b.cyl((0, 0, -length * 0.42), radius * 1.08, radius * 1.08, 0.08, n, "CARB", M, smooth=False)
            b.cyl((0, 0, length * 0.34), radius * 1.08, radius * 1.08, 0.08, n, "CARB", M, smooth=False)


def corner_pylons(b, span_x, span_y, h, L, tip="TEAM", foot_z=0.0):
    for sx in (-1, 1):
        for sy in (-1, 1):
            M = T(sx * span_x, sy * span_y, foot_z)
            b.box((0, 0, h * 0.45), (0.42, 0.42, h * 0.9), "CARB", M, top=(0.28, 0.28))
            b.bipyramid((0, 0, h * 0.95), 0.16, 0.28, 0.10, 4, tip, M, 45)
            if L == 0:
                for q in range(4):
                    b.box((0, -0.20, h * 0.5), (0.05, 0.04, h * 0.6), "TEAM", M @ rz(90 * q))


def team_cornice(b, c, sx, sy, thick=0.14):
    b.box(c, (sx, sy, thick), "TEAM")


def rib_faces(b, c, h, w, L, M=None):
    if L > 0:
        return
    cx, cy, cz = c
    for sgn in (-1, 1):
        b.box((cx + sgn * w * 0.32, cy, cz), (0.10, 0.12, h * 0.85), "RIB", M)


# ---------------------------------------------------------------------------
# LUMEN CONCORD — stepped tiers, corner pylons, claw over the door
# ---------------------------------------------------------------------------

def lumen_inn(L):
    b = B()
    # plinth, porch notch on -Y
    b.box((0, 0.15, 0.14), (5.5, 5.2, 0.28), "CARB", top=(5.3, 5.0))
    # lower tier
    b.box((0, 0.20, 1.35), (4.5, 4.2, 2.14), "PANEL", top=(4.15, 3.9))
    team_cornice(b, (0, 0.20, 2.48), 4.7, 4.4, 0.14)
    # upper tier
    b.box((0, 0.30, 3.35), (3.05, 2.7, 1.55), "PANEL", top=(2.75, 2.45))
    team_cornice(b, (0, 0.30, 4.18), 3.25, 2.9, 0.12)
    if L <= 1:
        b.box((0, 0.30, 4.38), (2.4, 2.1, 0.22), "CARB", top=(2.1, 1.85))
    corner_pylons(b, 2.45, 2.25, 3.6, L)
    # porch + door on -Y
    for sgn in (-1, 1):
        b.box((sgn * 1.05, -2.35, 1.05), (0.22, 0.22, 1.9), "CARB")
    b.box((0, -2.40, 2.10), (2.4, 1.0, 0.16), "PANEL_DK", top=(2.1, 0.8), shift=(0, 0.05))
    door_vent(b, (0, -1.95, 1.25), 1.05, 1.7)
    core = claw_over_door(b, -2.15, 2.20, L)
    accent_banners(b, (0, -2.15, 2.15), L, span=1.35, drop=1.2)
    if L == 0:
        for sx in (-1.4, 1.4):
            b.box((sx, -2.05, 1.55), (0.7, 0.08, 0.55), "GLASS")
        rib_faces(b, (0, 2.25, 1.4), 1.8, 4.0, L)
        b.box((0, 2.28, 1.4), (0.08, 0.06, 1.8), "TEAM")
    anchors = {"Gate": (0, -2.55, 0.05), "Core_Socket": core}
    return b, anchors


def lumen_guild(L):
    b = B()
    b.box((0, 0.10, 0.14), (5.6, 5.4, 0.28), "CARB")
    # two stepped shells
    for sx in (-1, 1):
        x = sx * 1.50
        b.box((x, 0.15, 1.45), (2.35, 4.8, 2.3), "PANEL", top=(2.10, 4.45))
        team_cornice(b, (x, 0.15, 2.68), 2.50, 5.0, 0.14)
        b.box((x, 0.25, 3.55), (1.65, 3.2, 1.5), "PANEL", top=(1.45, 2.95))
        team_cornice(b, (x, 0.25, 4.36), 1.80, 3.35, 0.12)
        if L == 0:
            for fy in (-1.4, 0.4, 1.8):
                b.box((x + sx * 1.15, fy, 1.55), (0.10, 0.70, 0.55), "GLASS")
    # connector + class sign facing -Y
    b.box((0, 0.10, 1.55), (0.85, 3.4, 2.0), "PANEL_DK")
    b.box((0, -2.45, 2.95), (1.9, 0.10, 0.95), "TEAM")  # class sign
    if L <= 1:
        b.box((0, -2.40, 2.95), (1.6, 0.06, 0.12), "ACCENT")
    for sgn in (-1, 1):
        b.box((sgn * 0.70, -2.45, 1.15), (0.22, 0.22, 2.1), "CARB")
    b.box((0, -2.50, 2.25), (1.7, 0.9, 0.14), "PANEL_DK")
    door_vent(b, (0, -2.25, 1.20), 1.0, 1.65)
    core = claw_over_door(b, -2.45, 2.25, L, 0.95)
    accent_banners(b, (0, -2.50, 2.20), L, span=1.15, drop=1.15)
    corner_pylons(b, 2.60, 2.45, 3.4, L)
    anchors = {"Gate": (0, -2.80, 0.05), "Core_Socket": core}
    return b, anchors


def lumen_hab(L):
    b = B()
    b.box((0, 0.05, 0.14), (5.5, 5.5, 0.28), "CARB")
    b.box((0, 0.10, 0.42), (4.6, 4.6, 0.28), "PANEL_DK")
    # central living module, stepped
    b.box((0, 0.15, 1.45), (3.2, 3.6, 2.0), "PANEL", top=(2.95, 3.3))
    team_cornice(b, (0, 0.15, 2.52), 3.4, 3.8, 0.14)
    b.box((0, 0.20, 3.15), (2.15, 2.4, 1.1), "PANEL", top=(1.95, 2.15))
    team_cornice(b, (0, 0.20, 3.76), 2.30, 2.55, 0.12)
    # amber pods in side niches (chargers)
    for sx, zy in ((-1, 0.4), (-1, -0.9), (1, 0.4), (1, -0.9)):
        amber_pod(b, (sx * 2.15, zy, 1.15), 1.55, 0.38, "y", L)
        # niche hood
        b.box((sx * 1.85, zy, 1.70), (0.55, 1.35, 0.14), "CARB")
    door_vent(b, (0, -1.70, 1.20), 1.0, 1.55)
    core = claw_over_door(b, -1.85, 2.05, L, 0.85)
    accent_banners(b, (0, -1.85, 2.05), L, span=1.15, drop=1.0)
    corner_pylons(b, 2.50, 2.50, 2.6, L)
    if L == 0:
        b.box((0, 1.90, 1.55), (1.1, 0.08, 0.55), "GLASS")
    anchors = {"Gate": (0, -2.55, 0.05), "Core_Socket": core}
    return b, anchors


def lumen_power(L):
    b = B()
    b.box((0, 0, 0.12), (5.6, 5.6, 0.24), "CARB")
    # three terraces of panels stepping back from -Y, rising
    # panel centres along Y, tilted about X (top toward +Y / back)
    for i, (y, z, w) in enumerate(((-1.85, 0.55, 5.2), (-0.35, 1.05, 4.6), (1.05, 1.55, 4.0))):
        M = T(0, y, z) @ Rx(-32)
        b.box((0, 0, 0), (w, 1.35, 0.07), "PANEL_DK", M)
        b.box((0, 0, -0.05), (w + 0.08, 1.42, 0.05), "CARB", M)
        team_cornice_edge = T(0, y, z) @ Rx(-32)
        if L <= 1:
            b.box((0, -0.62, 0.0), (w, 0.06, 0.04), "TEAM", team_cornice_edge)
        # terrace riser
        if i == 0:
            b.box((0, y + 0.75, z * 0.45), (w * 0.98, 0.18, z * 0.85), "CARB")
        else:
            prev_z = (0.55, 1.05, 1.55)[i - 1]
            b.box((0, y - 0.55, (prev_z + z) * 0.35), (w * 0.95, 0.16, (z - 0.2)), "CARB")
    # stepped transformer hut at the back
    b.box((0, 2.15, 1.15), (2.2, 1.6, 1.9), "PANEL", top=(2.0, 1.4))
    team_cornice(b, (0, 2.15, 2.16), 2.35, 1.75, 0.12)
    b.box((0, 2.15, 2.55), (1.4, 1.05, 0.7), "PANEL_DK", top=(1.2, 0.9))
    door_vent(b, (0, 1.40, 1.05), 0.75, 1.2, team=True)
    core = claw_over_door(b, 1.35, 1.85, L, 0.7)
    # the claw faces -Y already (beams in world). Hut door faces -Y (toward the panels). Good.
    accent_banners(b, (0, 1.35, 1.85), L, span=0.85, drop=0.85)
    if L == 0:
        b.cyl((0, 2.15, 2.95), 0.18, 0.18, 0.55, 10, "CARB", smooth=False)
        b.cyl((0, 2.15, 3.48), 0.22, 0.05, 0.12, 10, "TEAM", smooth=False)
    anchors = {"Gate": (0, 1.15, 0.05), "Core_Socket": core}
    return b, anchors


def lumen_defense(L):
    b = B()
    b.box((0, 0.15, 0.16), (4.4, 4.4, 0.32), "CARB")
    b.box((0, 0.15, 0.85), (3.4, 3.4, 1.10), "PANEL", top=(3.1, 3.1))
    team_cornice(b, (0, 0.15, 1.48), 3.6, 3.6, 0.14)
    corner_pylons(b, 2.00, 2.00, 2.2, L)
    # main gun pylon
    n = nseg(L, 12, 8, 6)
    b.cyl((0, 0.35, 1.50), 0.55, 0.42, 3.4, n, "CARB", smooth=False)
    b.cyl((0, 0.35, 2.55), 0.60, 0.60, 0.16, n, "TEAM", smooth=False)
    b.cyl((0, 0.35, 3.85), 0.50, 0.50, 0.14, n, "TEAM", smooth=False)
    b.cyl((0, 0.35, 4.85), 0.48, 0.32, 0.30, n, "EDGE", smooth=False)
    # yoke + barrel pointing -Y
    b.box((0, 0.35, 5.25), (1.15, 0.55, 0.45), "PANEL_DK")
    b.box((0, -0.85, 5.25), (0.32, 2.15, 0.32), "CARB")          # barrel, stays on the 6 m plot
    if L <= 1:
        # muzzle: local cyl along Y via Rx, origin at the muzzle tip
        b.cyl((0, 0, 0), 0.20, 0.14, 0.28, n, "EDGE", T(0, -1.95, 5.25) @ Rx(-90), smooth=False)
        b.box((0, 0.05, 5.25), (0.12, 1.2, 0.12), "TEAM")
    # cyan at pylon cap, behind the yoke (small)
    core = (0, 0.55, 5.55)
    core_shard(b, core, 0.75)
    accent_banners(b, (0, -1.55, 1.55), L, span=1.4, drop=1.0)
    if L == 0:
        door_vent(b, (0, -1.55, 0.95), 0.7, 1.0)
    anchors = {
        "Gate": (0, -2.15, 0.05),
        "Core_Socket": core,
        "GunPivot": (0, 0.35, 5.25),
    }
    return b, anchors


# ---------------------------------------------------------------------------
# TETHER EXCHANGE — drum in a ring wall, thin mast, caged cyan
# ---------------------------------------------------------------------------

def _ring_wall(b, ri, ro, z0, z1, L, gap=70.0):
    """Ring with a gap around -Y (270°)."""
    n = nseg(L, 36, 24, 16)
    # solid from 270+gap/2 to 270-gap/2 + 360
    a0 = 270.0 + gap / 2.0
    a1 = 270.0 - gap / 2.0 + 360.0
    b.ring(ri, ro, z0, z1, "CARB", a0=a0, a1=a1, n=n, smooth=False, kind_top="TEAM")
    if L <= 1:
        b.ring(ri - 0.04, ri + 0.02, z0 + 0.15, z1 - 0.15, "RIB", a0=a0, a1=a1, n=n, smooth=False)


def _mast(b, c, h, L, cage=True):
    cx, cy, cz = c
    n = nseg(L, 10, 8, 6)
    b.cyl((cx, cy, cz), 0.12, 0.10, h, n, "PANEL_DK", smooth=False)
    b.cyl((cx, cy, cz + h * 0.45), 0.16, 0.16, 0.08, n, "TEAM", smooth=False)
    core = c
    if cage:
        core = caged_light(b, (cx, cy, cz + h), L)
    return core


def _drum(b, c, r, h, L, team_z=None):
    n = nseg(L, 24, 16, 10)
    cx, cy, cz = c
    b.cyl((cx, cy, cz), r, r * 0.96, h, n, "PANEL", smooth=False)
    tz = team_z if team_z is not None else cz + h * 0.55
    b.cyl((cx, cy, tz), r * 1.05, r * 1.05, 0.14, n, "TEAM", smooth=False)
    if L <= 1:
        b.cyl((cx, cy, cz + h * 0.22), r * 1.02, r * 1.02, 0.08, n, "ACCENT", smooth=False)
    b.cyl((cx, cy, cz + h), r * 0.96, r * 0.70, 0.22, n, "CARB", smooth=False)


def exchange_inn(L):
    b = B()
    b.cyl((0, 0, 0), 2.85, 2.75, 0.28, nseg(L, 32, 20, 12), "CARB", smooth=False)
    _ring_wall(b, 2.25, 2.80, 0.28, 1.75, L, gap=72)
    _drum(b, (0, 0.15, 0.28), 1.65, 3.15, L)
    # gate arch in the -Y gap
    for sgn in (-1, 1):
        b.box((sgn * 0.95, -2.45, 1.15), (0.36, 0.50, 2.05), "PANEL_DK", top=(0.30, 0.42))
    b.box((0, -2.45, 2.30), (2.15, 0.55, 0.32), "CARB")
    b.box((0, -2.50, 2.30), (1.8, 0.12, 0.10), "TEAM")
    door_vent(b, (0, -1.50, 1.20), 1.05, 1.7)
    core = _mast(b, (0, 0.15, 3.55), 2.15, L)
    accent_banners(b, (0, -2.50, 2.15), L, span=1.25, drop=1.15)
    if L == 0:
        for a in (30, 90, 150, 210, 330):
            rad = math.radians(a)
            x, y = 1.66 * math.cos(rad), 0.15 + 1.66 * math.sin(rad)
            b.box((x, y, 1.6), (0.08, 0.55, 0.55), "GLASS", rz(a + 90))
    anchors = {"Gate": (0, -2.80, 0.05), "Core_Socket": core}
    return b, anchors


def exchange_guild(L):
    b = B()
    b.cyl((0, 0, 0), 2.88, 2.78, 0.26, nseg(L, 32, 20, 12), "CARB", smooth=False)
    # two drums
    for sx in (-1, 1):
        _drum(b, (sx * 1.35, 0.10, 0.26), 1.25, 3.05, L)
        # gate arch per drum, toward -Y
        b.box((sx * 1.35, -1.35, 1.15), (0.95, 0.22, 1.7), "VENT")
        b.box((sx * 1.35, -1.45, 2.10), (1.15, 0.36, 0.28), "CARB")
    # connecting collar
    b.box((0, 0.10, 1.55), (0.55, 1.9, 2.1), "PANEL_DK")
    b.box((0, -1.55, 2.85), (2.0, 0.10, 0.85), "TEAM")  # class sign
    if L <= 1:
        b.box((0, -1.50, 2.85), (1.65, 0.06, 0.10), "ACCENT")
    # shared outer ring, lower, with a front gap
    _ring_wall(b, 2.40, 2.88, 0.26, 1.45, L, gap=80)
    core = _mast(b, (0, 0.10, 2.85), 2.35, L)
    accent_banners(b, (0, -1.60, 2.10), L, span=2.15, drop=1.1)
    anchors = {"Gate": (0, -2.85, 0.05), "Core_Socket": core}
    return b, anchors


def exchange_hab(L):
    b = B()
    n = nseg(L, 28, 18, 12)
    b.cyl((0, 0, 0), 2.80, 2.70, 0.26, n, "CARB", smooth=False)
    _drum(b, (0, 0.10, 0.26), 1.15, 2.35, L)
    # ring of amber pods around the drum
    pods = [(-2.05, 0.55), (-2.05, -0.85), (2.05, 0.55), (2.05, -0.85)]
    for x, y in pods:
        amber_pod(b, (x, y, 0.95), 1.70, 0.40, "y", L)
        # short ring segment as a cradle
        b.box((x * 0.85, y, 0.45), (0.7, 0.85, 0.22), "CARB")
    _ring_wall(b, 2.35, 2.82, 0.26, 1.15, L, gap=70)
    for sgn in (-1, 1):
        b.box((sgn * 0.85, -2.15, 1.00), (0.30, 0.40, 1.7), "PANEL_DK")
    b.box((0, -2.15, 1.95), (1.85, 0.45, 0.26), "CARB")
    door_vent(b, (0, -1.05, 1.05), 0.95, 1.5)
    core = _mast(b, (0, 0.10, 2.70), 1.85, L)
    accent_banners(b, (0, -2.20, 1.90), L, span=1.15, drop=1.0)
    anchors = {"Gate": (0, -2.80, 0.05), "Core_Socket": core}
    return b, anchors


def exchange_power(L):
    b = B()
    n = nseg(L, 28, 18, 12)
    b.cyl((0, 0, 0), 2.85, 2.75, 0.24, n, "CARB", smooth=False)
    # circular solar field: 8 (or fewer) tilted panels around a headframe
    count = (10, 8, 6)[L]
    for i in range(count):
        a = 360.0 * i / count + 18.0  # keep a clear walk at -Y
        if 240 < (a % 360) < 300:
            continue
        M = rz(a) @ T(0, -1.95, 0.85) @ Rx(-38)
        b.box((0, 0, 0), (1.15, 1.45, 0.07), "PANEL_DK", M)
        b.box((0, 0, -0.05), (1.22, 1.52, 0.05), "CARB", M)
        if L <= 1:
            b.box((0, -0.68, 0), (1.15, 0.05, 0.04), "TEAM", M)
    # round headframe
    _drum(b, (0, 0.15, 0.24), 0.95, 2.15, L)
    b.cyl((0, 0.15, 2.40), 0.55, 0.40, 0.7, nseg(L, 14, 10, 8), "PANEL_DK", smooth=False)
    door_vent(b, (0, -0.80, 1.00), 0.75, 1.25)
    core = _mast(b, (0, 0.15, 3.05), 1.65, L)
    accent_banners(b, (0, -0.95, 1.85), L, span=0.85, drop=0.9)
    anchors = {"Gate": (0, -2.85, 0.05), "Core_Socket": core}
    return b, anchors


def exchange_defense(L):
    b = B()
    n = nseg(L, 24, 16, 10)
    b.cyl((0, 0, 0), 2.15, 2.05, 0.28, n, "CARB", smooth=False)
    b.ring(1.55, 2.10, 0.28, 1.25, "CARB", n=n, smooth=False, kind_top="TEAM")
    # slim mast
    b.cyl((0, 0, 1.25), 0.28, 0.20, 4.4, nseg(L, 12, 8, 6), "PANEL_DK", smooth=False)
    b.cyl((0, 0, 2.55), 0.34, 0.34, 0.12, n, "TEAM", smooth=False)
    b.cyl((0, 0, 4.15), 0.28, 0.28, 0.12, n, "TEAM", smooth=False)
    # gun at the top, barrel -Y
    b.box((0, 0, 5.85), (0.95, 0.45, 0.40), "CARB")
    b.box((0, -1.05, 5.85), (0.26, 2.3, 0.26), "PANEL_DK")
    if L <= 1:
        b.box((0, -0.4, 5.85), (0.10, 1.1, 0.10), "TEAM")
    core = caged_light(b, (0, 0.35, 6.15), L, h=0.45)
    accent_banners(b, (0, -1.7, 1.30), L, span=1.15, drop=0.9)
    if L == 0:
        door_vent(b, (0, -1.55, 0.80), 0.65, 0.9)
    anchors = {"Gate": (0, -2.15, 0.05), "Core_Socket": core, "GunPivot": (0, 0, 5.85)}
    return b, anchors


# ---------------------------------------------------------------------------
# BASTION MANDATE — low dome, octagonal skirt, cyan window under the crown
# ---------------------------------------------------------------------------

def _oct_skirt(b, r, h, L, team=True):
    n = 8
    b.cyl((0, 0, 0), r, r * 0.96, h, n, "CARB", smooth=False, a0=22.5)
    if team:
        b.cyl((0, 0, h - 0.10), r * 0.98, r * 1.02, 0.14, n, "TEAM", smooth=False, a0=22.5)


def _low_dome(b, c, r, hz, L, kind="PANEL"):
    n = nseg(L, 16, 12, 8)
    rings = (6, 4, 3)[L]
    b.dome(c, r, hz, n, rings, kind)


def mandate_inn(L):
    b = B()
    _oct_skirt(b, 2.85, 0.70, L)
    _low_dome(b, (0, 0.10, 0.70), 2.25, 1.85, L)
    # eaves ring
    b.cyl((0, 0.10, 0.62), 2.40, 2.40, 0.18, 8, "CARB", smooth=False, a0=22.5)
    # blast door on -Y, wide and short
    b.box((0, -2.15, 1.15), (2.15, 0.55, 1.85), "CARB")
    b.box((0, -2.35, 1.15), (1.70, 0.22, 1.55), "VENT")
    b.box((0, -2.42, 2.05), (1.85, 0.12, 0.12), "TEAM")
    # cyan window under the crown, facing -Y (front of dome)
    core = crown_window(b, (0, -1.35, 2.15), L, w=0.75, h=0.40)
    accent_banners(b, (0, -2.40, 2.05), L, span=1.35, drop=1.15)
    if L == 0:
        for a in (22.5 + 45 * i for i in (1, 2, 5, 6)):
            rad = math.radians(a)
            # glass slits on the skirt faces not at the door (door is -Y = 270)
            if 240 < a < 300:
                continue
            x, y = 2.20 * math.cos(rad), 0.10 + 2.20 * math.sin(rad)
            b.box((x, y, 1.35), (0.08, 0.55, 0.45), "GLASS", rz(a + 90))
    anchors = {"Gate": (0, -2.80, 0.05), "Core_Socket": core}
    return b, anchors


def mandate_guild(L):
    b = B()
    _oct_skirt(b, 2.88, 0.65, L)
    # two low domes
    for sx in (-1, 1):
        _low_dome(b, (sx * 1.35, 0.15, 0.65), 1.45, 1.55, L)
        # blast door on each
        b.box((sx * 1.35, -1.45, 1.10), (1.35, 0.40, 1.55), "CARB")
        b.box((sx * 1.35, -1.60, 1.10), (1.05, 0.16, 1.25), "VENT")
    # shared connector + class sign
    b.box((0, 0.10, 1.25), (0.7, 2.2, 1.7), "PANEL_DK")
    b.box((0, -1.70, 2.45), (1.9, 0.10, 0.75), "TEAM")
    if L <= 1:
        b.box((0, -1.65, 2.45), (1.55, 0.06, 0.10), "ACCENT")
    core = crown_window(b, (0, -0.55, 2.15), L, w=0.70, h=0.36)
    accent_banners(b, (0, -1.75, 2.05), L, span=2.15, drop=1.05)
    anchors = {"Gate": (0, -2.80, 0.05), "Core_Socket": core}
    return b, anchors


def mandate_hab(L):
    b = B()
    _oct_skirt(b, 2.82, 0.55, L)
    # half-dome hood: a dome shifted toward +Y, so -Y is more open (pods visible)
    _low_dome(b, (0, 0.55, 0.55), 2.15, 1.70, L)
    b.cyl((0, 0.55, 0.50), 2.25, 2.25, 0.16, 8, "CARB", smooth=False, a0=22.5)
    # amber pods under the front of the hood, on the pad
    for sx, y in ((-1.15, -1.35), (1.15, -1.35), (0.0, -1.65)):
        amber_pod(b, (sx, y, 0.95), 1.45, 0.36, "y" if abs(sx) > 0.1 else "x", L)
    door_vent(b, (0, -0.55, 1.15), 0.95, 1.2)
    core = crown_window(b, (0, -0.55, 1.95), L, w=0.70, h=0.34)
    accent_banners(b, (0, -2.15, 1.55), L, span=1.45, drop=0.95)
    anchors = {"Gate": (0, -2.80, 0.05), "Core_Socket": core}
    return b, anchors


def mandate_power(L):
    b = B()
    # buried: low octagonal berm, hatch-topped bunker, panels behind berms
    _oct_skirt(b, 2.85, 0.85, L)
    # berm shoulders (raised CARB along +X, -X, +Y)
    for a in (45, 135, 45 + 180, 135 + 180):
        if 240 < (a % 360) < 300:
            continue
        M = rz(a)
        b.box((0, 2.15, 0.85), (2.2, 1.1, 1.1), "CARB", M, top=(1.8, 0.7), shift=(0, 0.15))
    # panels behind the side berms, low and almost flush, tilted
    for a in (0, 180):
        M = rz(a) @ T(0, 1.85, 1.15) @ Rx(-22)
        b.box((0, 0, 0), (2.4, 1.35, 0.07), "PANEL_DK", M)
        b.box((0, 0, -0.05), (2.5, 1.42, 0.05), "CARB", M)
        if L <= 1:
            b.box((0, -0.62, 0), (2.4, 0.05, 0.04), "TEAM", M)
    # hatch-topped bunker in the middle
    b.cyl((0, 0.15, 0.85), 1.15, 1.10, 0.85, 8, "PANEL", smooth=False, a0=22.5)
    b.cyl((0, 0.15, 1.65), 1.20, 0.85, 0.18, 8, "CARB", smooth=False, a0=22.5)
    b.box((0, 0.15, 1.90), (1.05, 0.85, 0.16), "PANEL_DK")
    b.box((0, 0.15, 2.02), (0.7, 0.55, 0.10), "TEAM")
    door_vent(b, (0, -1.00, 1.15), 0.85, 1.05)
    core = crown_window(b, (0, -0.55, 1.85), L, w=0.55, h=0.28)
    accent_banners(b, (0, -1.15, 1.65), L, span=0.95, drop=0.8)
    anchors = {"Gate": (0, -2.80, 0.05), "Core_Socket": core}
    return b, anchors


def mandate_defense(L):
    b = B()
    # squat octagonal casemate — the Mandate showpiece, wide and low
    _oct_skirt(b, 2.80, 0.55, L)
    b.cyl((0, 0.10, 0.55), 2.35, 2.25, 2.05, 8, "PANEL", smooth=False, a0=22.5)
    team_cornice_h = 2.55
    b.cyl((0, 0.10, team_cornice_h), 2.40, 2.40, 0.16, 8, "TEAM", smooth=False, a0=22.5)
    b.cyl((0, 0.10, 2.70), 2.15, 1.65, 0.35, 8, "CARB", smooth=False, a0=22.5)
    # small upper cap
    _low_dome(b, (0, 0.10, 3.00), 1.55, 0.85, L, "PANEL_DK")
    # embrasure + gun facing -Y
    b.box((0, -2.15, 1.45), (2.25, 0.85, 1.55), "CARB")
    b.box((0, -2.40, 1.45), (1.55, 0.35, 1.05), "VENT")
    b.box((0, -2.50, 1.45), (0.28, 0.85, 0.28), "PANEL_DK")  # barrel, kept inside the 6 m plot
    if L <= 1:
        b.box((0, -2.30, 1.45), (0.12, 0.55, 0.12), "TEAM")
    core = crown_window(b, (0, -1.15, 3.05), L, w=0.70, h=0.34)
    accent_banners(b, (0, -2.45, 2.30), L, span=1.45, drop=1.1)
    anchors = {"Gate": (0, -2.80, 0.05), "Core_Socket": core, "GunPivot": (0, -2.15, 1.45)}
    return b, anchors


# ---------------------------------------------------------------------------
# PATHFINDER ACCORD — body on legs, daylight underneath, cyan under the floor
# ---------------------------------------------------------------------------

def _legs(b, pts, body_z, L, splay=0.45):
    """pts are pad centres. Legs rise to (inward by splay) at body_z."""
    for x, y in pts:
        inward_x = x - math.copysign(splay, x) if abs(x) > 0.1 else x
        inward_y = y - math.copysign(splay, y) if abs(y) > 0.1 else y
        b.box((x, y, 0.08), (0.72, 0.72, 0.16), "CARB")
        b.beam((x, y, 0.16), (inward_x, inward_y, body_z), 0.24, 0.24, "PANEL_DK")
        # team collar at the body joint
        b.cyl((inward_x, inward_y, body_z - 0.12), 0.22, 0.22, 0.20, nseg(L, 8, 6, 4), "TEAM", smooth=False)
        if L == 0:
            b.box((x, y, 0.18), (0.50, 0.50, 0.08), "RIB")


def _ramp(b, y0, y1, z1, w=1.15):
    """Ramp from ground at y0 to deck at (y1, z1), facing -Y (y0 < y1)."""
    hw = w / 2
    # sloping top + sides; underside
    b.add(
        [(-hw, y0, 0.02), (hw, y0, 0.02), (hw, y1, z1), (-hw, y1, z1),
         (-hw, y0, 0.0), (hw, y0, 0.0), (hw, y1, z1 - 0.08), (-hw, y1, z1 - 0.08)],
        [(0, 1, 2, 3), (4, 7, 6, 5), (0, 3, 7, 4), (1, 5, 6, 2)],
        "CARB",
    )
    # low rails
    for sgn in (-1, 1):
        b.beam((sgn * hw, y0, 0.08), (sgn * hw, y1, z1 + 0.08), 0.06, 0.06, "TEAM")


def pathfinder_inn(L):
    b = B()
    body_z = 1.85
    _legs(b, [(-2.15, -1.85), (-2.15, 1.85), (2.15, -1.85), (2.15, 1.85)], body_z, L)
    # raised cabin
    b.box((0, 0.10, body_z + 0.85), (4.4, 3.7, 1.70), "PANEL", top=(4.15, 3.45))
    team_cornice(b, (0, 0.10, body_z + 1.76), 4.55, 3.85, 0.12)
    b.box((0, 0.10, body_z + 2.10), (3.4, 2.7, 0.50), "PANEL_DK", top=(3.1, 2.45))
    # underside plate (visible from overseer angle)
    b.box((0, 0.10, body_z - 0.06), (4.2, 3.5, 0.10), "CARB")
    # hatch on -Y + ramp
    door_vent(b, (0, -1.75, body_z + 0.75), 1.05, 1.35)
    _ramp(b, -2.85, -1.80, body_z, 1.20)
    core = underfloor_light(b, (0, 0.0, body_z - 0.35), L)
    accent_banners(b, (0, -1.85, body_z + 1.55), L, span=1.25, drop=1.0)
    if L == 0:
        for sx in (-1.4, 1.4):
            b.box((sx, -1.75, body_z + 0.95), (0.7, 0.08, 0.50), "GLASS")
        b.box((0, 1.90, body_z + 0.95), (1.4, 0.08, 0.50), "GLASS")
    anchors = {"Gate": (0, -2.85, 0.05), "Core_Socket": core}
    return b, anchors


def pathfinder_guild(L):
    b = B()
    body_z = 1.80
    _legs(b, [(-2.35, -1.95), (-2.35, 1.95), (2.35, -1.95), (2.35, 1.95)], body_z, L, splay=0.40)
    # two raised cabins
    for sx in (-1, 1):
        b.box((sx * 1.45, 0.10, body_z + 0.90), (2.3, 3.9, 1.80), "PANEL", top=(2.1, 3.65))
        team_cornice(b, (sx * 1.45, 0.10, body_z + 1.86), 2.45, 4.05, 0.12)
        door_vent(b, (sx * 1.45, -1.85, body_z + 0.75), 0.90, 1.25)
        if L == 0:
            b.box((sx * 2.55, 0.10, body_z + 0.95), (0.08, 1.1, 0.5), "GLASS")
    # catwalk + class sign
    b.box((0, 0.10, body_z + 0.12), (0.85, 3.2, 0.16), "CARB")
    b.box((0, 0.10, body_z + 1.15), (0.70, 2.4, 1.4), "PANEL_DK")
    b.box((0, -1.95, body_z + 1.85), (1.85, 0.10, 0.75), "TEAM")
    if L <= 1:
        b.box((0, -1.90, body_z + 1.85), (1.5, 0.06, 0.10), "ACCENT")
    _ramp(b, -2.85, -1.90, body_z, 1.25)
    core = underfloor_light(b, (0, 0.10, body_z - 0.30), L)
    accent_banners(b, (0, -2.00, body_z + 1.55), L, span=2.15, drop=1.0)
    anchors = {"Gate": (0, -2.85, 0.05), "Core_Socket": core}
    return b, anchors


def pathfinder_hab(L):
    b = B()
    body_z = 1.95
    _legs(b, [(-2.20, -1.90), (-2.20, 1.90), (2.20, -1.90), (2.20, 1.90)], body_z, L)
    # raised deck
    b.box((0, 0.10, body_z + 0.10), (4.5, 4.0, 0.22), "CARB")
    b.box((0, 0.10, body_z + 0.95), (3.6, 3.1, 1.50), "PANEL", top=(3.35, 2.85))
    team_cornice(b, (0, 0.10, body_z + 1.76), 3.75, 3.25, 0.12)
    # amber pods HUNG UNDER the deck — visible through the legs at 45°
    for x, y in ((-1.35, -0.85), (1.35, -0.85), (-1.35, 0.95), (1.35, 0.95)):
        amber_pod(b, (x, y, body_z - 0.45), 1.55, 0.36, "y", L)
        # short sling
        if L <= 1:
            b.beam((x, y - 0.55, body_z), (x, y - 0.55, body_z - 0.35), 0.05, 0.05, "CARB")
            b.beam((x, y + 0.55, body_z), (x, y + 0.55, body_z - 0.35), 0.05, 0.05, "CARB")
    door_vent(b, (0, -1.50, body_z + 0.75), 1.0, 1.25)
    _ramp(b, -2.85, -1.55, body_z, 1.20)
    core = underfloor_light(b, (0, 0.15, body_z - 0.35), L)
    accent_banners(b, (0, -1.60, body_z + 1.50), L, span=1.20, drop=0.95)
    anchors = {"Gate": (0, -2.85, 0.05), "Core_Socket": core}
    return b, anchors


def pathfinder_power(L):
    b = B()
    body_z = 1.55
    # two long skids along Y
    for sx in (-1, 1):
        b.box((sx * 1.55, 0, 0.12), (0.55, 5.4, 0.24), "CARB")
        b.beam((sx * 1.55, -2.2, 0.24), (sx * 1.15, -1.0, body_z), 0.20, 0.20, "PANEL_DK")
        b.beam((sx * 1.55, 2.2, 0.24), (sx * 1.15, 1.0, body_z), 0.20, 0.20, "PANEL_DK")
        b.cyl((sx * 1.15, 0, body_z - 0.10), 0.20, 0.20, 0.18, nseg(L, 8, 6, 4), "TEAM", smooth=False)
    # small raised cabin
    b.box((0, 0.20, body_z + 0.70), (2.15, 2.0, 1.35), "PANEL", top=(1.95, 1.8))
    team_cornice(b, (0, 0.20, body_z + 1.42), 2.30, 2.15, 0.10)
    b.box((0, 0.20, body_z - 0.05), (2.3, 2.2, 0.10), "CARB")
    # fold-out panel wings along ±X
    for sx in (-1, 1):
        M = T(sx * 1.85, 0.0, body_z + 0.55) @ Ry(sx * 26)
        b.box((0, 0, 0), (1.80, 4.2, 0.07), "PANEL_DK", M)
        b.box((0, 0, -0.05), (1.88, 4.3, 0.05), "CARB", M)
        if L <= 1:
            b.box((0, -1.95, 0), (1.80, 0.06, 0.04), "TEAM", M)
    door_vent(b, (0, -0.80, body_z + 0.60), 0.80, 1.05)
    _ramp(b, -2.85, -0.85, body_z, 1.05)
    core = underfloor_light(b, (0, 0.20, body_z - 0.30), L)
    accent_banners(b, (0, -0.90, body_z + 1.20), L, span=0.85, drop=0.8)
    anchors = {"Gate": (0, -2.85, 0.05), "Core_Socket": core}
    return b, anchors


def pathfinder_defense(L):
    b = B()
    body_z = 2.05
    # tripod: three legs, one toward -Y (front), two toward the back corners
    pts = [(0.0, -2.25), (-2.15, 1.55), (2.15, 1.55)]
    for x, y in pts:
        b.box((x, y, 0.10), (0.78, 0.78, 0.18), "CARB")
        # apex under the platform
        b.beam((x, y, 0.18), (x * 0.25, y * 0.25, body_z), 0.26, 0.26, "PANEL_DK")
        b.cyl((x * 0.25, y * 0.25, body_z - 0.12), 0.22, 0.22, 0.20, nseg(L, 8, 6, 4), "TEAM", smooth=False)
    # platform + gun
    n = nseg(L, 12, 8, 6)
    b.cyl((0, 0, body_z), 1.25, 1.15, 0.28, n, "CARB", smooth=False)
    b.cyl((0, 0, body_z + 0.28), 0.55, 0.40, 1.85, n, "PANEL_DK", smooth=False)
    b.cyl((0, 0, body_z + 1.05), 0.62, 0.62, 0.12, n, "TEAM", smooth=False)
    # gun yoke + barrel -Y
    b.box((0, 0, body_z + 2.25), (1.05, 0.50, 0.42), "CARB")
    b.box((0, -1.15, body_z + 2.25), (0.28, 2.45, 0.28), "PANEL_DK")
    if L <= 1:
        b.box((0, -0.50, body_z + 2.25), (0.10, 1.15, 0.10), "TEAM")
    core = underfloor_light(b, (0, 0.0, body_z - 0.35), L)
    accent_banners(b, (0, -1.15, body_z + 0.35), L, span=0.95, drop=0.85)
    anchors = {"Gate": (0, -2.55, 0.05), "Core_Socket": core, "GunPivot": (0, 0, body_z + 2.25)}
    return b, anchors


# ---------------------------------------------------------------------------
# Dispatch, assemble, export, review render
# ---------------------------------------------------------------------------

BUILDERS = {
    ("Lumen", "Inn"): lumen_inn,
    ("Lumen", "GuildHall"): lumen_guild,
    ("Lumen", "HAB"): lumen_hab,
    ("Lumen", "Power"): lumen_power,
    ("Lumen", "Defense"): lumen_defense,
    ("Exchange", "Inn"): exchange_inn,
    ("Exchange", "GuildHall"): exchange_guild,
    ("Exchange", "HAB"): exchange_hab,
    ("Exchange", "Power"): exchange_power,
    ("Exchange", "Defense"): exchange_defense,
    ("Mandate", "Inn"): mandate_inn,
    ("Mandate", "GuildHall"): mandate_guild,
    ("Mandate", "HAB"): mandate_hab,
    ("Mandate", "Power"): mandate_power,
    ("Mandate", "Defense"): mandate_defense,
    ("Pathfinder", "Inn"): pathfinder_inn,
    ("Pathfinder", "GuildHall"): pathfinder_guild,
    ("Pathfinder", "HAB"): pathfinder_hab,
    ("Pathfinder", "Power"): pathfinder_power,
    ("Pathfinder", "Defense"): pathfinder_defense,
}


def assemble(civ, building, mats):
    name = f"SM_{civ}_{building}"
    root = empty(name, (0, 0, 0), None, size=0.4)
    stats = {"name": name, "civ": civ, "building": building, "tris": {}, "slots": {}}
    lod0 = None
    anchors = {}
    for L in range(3):
        b, anc = BUILDERS[(civ, building)](L)
        if L == 0:
            anchors = anc
        ob = finish_mesh(b, f"{name}_LOD{L}", mats, cull=True)
        # hide LOD1/2 in the viewport so a full-scene render shows LOD0 only
        if L > 0:
            ob.hide_render = True
            ob.hide_viewport = True
        link(ob, root)
        stats["tris"][f"LOD{L}"] = tris(ob)
        used = [m.name for m in ob.data.materials]
        stats["slots"][f"LOD{L}"] = used
        if L == 0:
            lod0 = ob
    for k, loc in anchors.items():
        e = empty(k, loc, root, size=0.25)
        if k == "Gate":
            # default empty +Y is unused; rotate so local -Y matches the door
            e.rotation_euler = (0, 0, 0)
    lo, hi = bounds([lod0])
    stats["bounds"] = {
        "W": round(hi.x - lo.x, 3),
        "D": round(hi.y - lo.y, 3),
        "H": round(hi.z - lo.z, 3),
        "xmin": round(lo.x, 3), "xmax": round(hi.x, 3),
        "ymin": round(lo.y, 3), "ymax": round(hi.y, 3),
        "zmin": round(lo.z, 3), "zmax": round(hi.z, 3),
    }
    stats["ok_foot"] = stats["bounds"]["W"] <= 6.001 and stats["bounds"]["D"] <= 6.001
    stats["ok_height"] = stats["bounds"]["H"] < MAX_H
    stats["ok_lod0"] = stats["tris"]["LOD0"] <= LOD_TARGETS[0]
    return root, stats


def export_one(root, path):
    loc = root.location.copy()
    root.location = (0, 0, 0)
    bpy.context.view_layer.update()
    # unhide LODs for export
    for o in root.children:
        o.hide_viewport = False
        o.hide_render = False
    export_fbx(root, path)
    # re-hide LOD1/2 for review renders
    for o in root.children:
        if "_LOD1" in o.name or "_LOD2" in o.name:
            o.hide_render = True
            o.hide_viewport = True
    root.location = loc
    bpy.context.view_layer.update()


def apply_review_tints(mats_by_name, civ):
    """Multiply Trim and TeamColour albedos for the review render only."""
    tint = REVIEW_TINT[civ]
    for slot, color in (("Trim", tint["trim"]), ("TeamColour", tint["team"])):
        m = mats_by_name.get(slot)
        if m is None or not m.use_nodes:
            continue
        nt = m.node_tree
        # skip if already tinted
        if nt.nodes.get(f"Tint_{slot}"):
            continue
        princ = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if princ is None:
            continue
        # find existing Base Color link
        incoming = None
        for l in nt.links:
            if l.to_node == princ and l.to_socket.name == "Base Color":
                incoming = l
                break
        mix = nt.nodes.new("ShaderNodeMixRGB")
        mix.name = f"Tint_{slot}"
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1.0
        mix.inputs[2].default_value = (*color, 1.0)
        if incoming is not None:
            fr = incoming.from_socket
            nt.links.remove(incoming)
            nt.links.new(fr, mix.inputs[1])
        else:
            mix.inputs[1].default_value = (1, 1, 1, 1)
        nt.links.new(mix.outputs[0], princ.inputs["Base Color"])
        # CoreEmission: also boost cyan on the principled if we can
    core = mats_by_name.get("CoreEmission")
    if core and core.use_nodes:
        princ = next((n for n in core.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if princ is not None:
            # tint emission toward cyan (texture is a white mask)
            em = princ.inputs.get("Emission Color")
            # leave texture; add a mix if not present
            if core.node_tree.nodes.get("CyanTint") is None and em is not None:
                nt = core.node_tree
                incoming = None
                for l in list(nt.links):
                    if l.to_node == princ and l.to_socket.name == "Emission Color":
                        incoming = l
                        break
                mix = nt.nodes.new("ShaderNodeMixRGB")
                mix.name = "CyanTint"
                mix.blend_type = "MULTIPLY"
                mix.inputs[0].default_value = 1.0
                mix.inputs[2].default_value = (0.25, 0.95, 1.0, 1.0)
                if incoming is not None:
                    fr = incoming.from_socket
                    nt.links.remove(incoming)
                    nt.links.new(fr, mix.inputs[1])
                nt.links.new(mix.outputs[0], princ.inputs["Emission Color"])
                princ.inputs["Emission Strength"].default_value = 8.0


def setup_world(night=False):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1920
    sc.render.resolution_y = 1080
    sc.render.film_transparent = False
    sc.render.filepath = str(RENDER_DIR / "tmp")
    ee = sc.eevee
    for attr, val in (("use_ssr", True), ("taa_render_samples", 32), ("use_shadows", True)):
        if hasattr(ee, attr):
            setattr(ee, attr, val)
    world = bpy.data.worlds.new("World") if bpy.data.worlds.get("World") is None else bpy.data.worlds["World"]
    sc.world = world
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    bg = nt.nodes.new("ShaderNodeBackground")
    if night:
        bg.inputs[0].default_value = (0.018, 0.025, 0.045, 1)
        bg.inputs[1].default_value = 0.4
        sc.render.resolution_x = 1920
    else:
        bg.inputs[0].default_value = (0.42, 0.48, 0.58, 1)
        bg.inputs[1].default_value = 0.55
    nt.links.new(bg.outputs[0], out.inputs[0])
    # sun
    for ob in list(bpy.data.objects):
        if ob.type == "LIGHT":
            bpy.data.objects.remove(ob, do_unlink=True)
    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 6.5 if not night else 0.35
    sun.angle = math.radians(18 if not night else 8)
    sun.color = (1.0, 0.93, 0.82) if not night else (0.45, 0.55, 0.85)
    so = bpy.data.objects.new("Sun", sun)
    so.rotation_euler = (math.radians(48), 0, math.radians(35))
    bpy.context.scene.collection.objects.link(so)
    if night:
        # fill so forms still read
        fill = bpy.data.lights.new("Fill", "SUN")
        fill.energy = 0.25
        fill.color = (0.35, 0.45, 0.7)
        fo = bpy.data.objects.new("Fill", fill)
        fo.rotation_euler = (math.radians(70), 0, math.radians(-50))
        bpy.context.scene.collection.objects.link(fo)
    # ground
    if bpy.data.objects.get("Ground") is None:
        bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, -0.005))
        g = bpy.context.active_object
        g.name = "Ground"
        gm = bpy.data.materials.new("GroundMat")
        gm.use_nodes = True
        gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.12, 0.13, 0.14, 1)
        gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
        g.data.materials.append(gm)
    # faint 6 m plot tiles under each building — added by place_grid


def ensure_camera():
    cam = bpy.data.cameras.new("Cam")
    cam.type = "ORTHO"
    cam.ortho_scale = 52
    co = bpy.data.objects.new("Cam", cam)
    bpy.context.scene.collection.objects.link(co)
    bpy.context.scene.camera = co
    return co


def place_camera(co, kind, cols=5, rows=4, spacing=11.0):
    """kind: dusk (3/4 overseer), top, night (same as dusk)."""
    cx = (cols - 1) * spacing * 0.5
    cy = (rows - 1) * spacing * 0.5
    target = Vector((cx, -cy, 1.6))
    if kind == "top":
        co.location = (cx, -cy, 28)
        co.rotation_euler = (0, 0, 0)
        # look down: rotation X = 0 isn't down. Default cam looks -Z.
        # Set to look down: rotation (pi, 0, 0) or (0,0,0) with cam pointing -Z and we want -Z world = down so rot (0,0,0) at +Z looks down. Yes.
        co.rotation_euler = (0, 0, 0)
        co.data.ortho_scale = spacing * max(cols, rows) + 4
        # wait, default camera looks along local -Z. At (cx,-cy,28) with rot (0,0,0), -Z is world -Z, which is downward. Good.
    else:
        # overseer ~48° elevation, slightly off-axis, looking at the grid centre
        elev = math.radians(48)
        az = math.radians(28)
        dist = 42
        co.location = target + Vector((
            dist * math.cos(elev) * math.sin(az),
            -dist * math.cos(elev) * math.cos(az),
            dist * math.sin(elev),
        ))
        # aim at target: track
        direction = target - co.location
        co.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        co.data.ortho_scale = 54


def add_plot_squares(origins, size=6.0):
    """Faint plot outlines (line mesh) under each building."""
    for i, (x, y, _) in enumerate(origins):
        h = size / 2
        verts = [(-h + x, -h + y, 0.01), (h + x, -h + y, 0.01),
                 (h + x, h + y, 0.01), (-h + x, h + y, 0.01)]
        edges = [(0, 1), (1, 2), (2, 3), (3, 0)]
        me = bpy.data.meshes.new(f"Plot_{i}")
        me.from_pydata(verts, edges, [])
        ob = bpy.data.objects.new(f"Plot_{i}", me)
        bpy.context.scene.collection.objects.link(ob)


def render_to(path, co, kind, night=False):
    setup_world(night=night)
    place_camera(co, kind)
    bpy.context.scene.render.filepath = str(path)
    bpy.context.scene.render.image_settings.file_format = "PNG"
    bpy.context.scene.render.image_settings.color_mode = "RGB"
    bpy.ops.render.render(write_still=True)
    print(f"[SM] rendered {path}")


def write_budget(all_stats, path):
    lines = [
        "# Phase-2 civ buildings — poly budget",
        "",
        "LOD targets: LOD0 ≤ 4000, LOD1 ~1500, LOD2 ~500. "
        "Plot is 6 × 6 m (4×4 cells). Height cap 10 m (Dome castle is 12.59 m).",
        "",
        "| Asset | LOD0 | LOD1 | LOD2 | W×D×H (m) | Foot OK | Height OK | Slots (LOD0) |",
        "|-------|-----:|-----:|-----:|-----------|---------|-----------|--------------|",
    ]
    for s in all_stats:
        b = s["bounds"]
        lines.append(
            f"| `{s['name']}` | {s['tris']['LOD0']} | {s['tris']['LOD1']} | {s['tris']['LOD2']} | "
            f"{b['W']:.2f}×{b['D']:.2f}×{b['H']:.2f} | "
            f"{'yes' if s['ok_foot'] else 'NO'} | {'yes' if s['ok_height'] else 'NO'} | "
            f"{', '.join(s['slots']['LOD0'])} |"
        )
    path.write_text("\n".join(lines) + "\n")
    print(f"[SM] budget table {path}")


def main():
    if not (TEX_DIR / "SM_Citadel_Trim_BaseColor.png").is_file():
        raise SystemExit(f"missing trim sheet in {TEX_DIR}")
    reset()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    RENDER_DIR.mkdir(parents=True, exist_ok=True)
    mats = make_materials()
    mats_by_name = {m.name: m for m in mats}

    spacing = 11.0
    all_stats = []
    roots = []
    origins = []
    for ci, civ in enumerate(CIVS):
        for bi, bld in enumerate(BUILDINGS):
            tag = f"{civ}_{bld}"
            if ONLY and tag not in ONLY and civ not in ONLY and bld not in ONLY:
                continue
            print(f"[SM] building {civ} {bld}")
            root, stats = assemble(civ, bld, mats)
            x, y = bi * spacing, -ci * spacing
            root.location = (x, y, 0)
            origins.append((x, y, 0))
            # export from origin
            export_one(root, OUT_DIR / f"{root.name}.fbx")
            all_stats.append(stats)
            roots.append((root, civ))
            warn = []
            if not stats["ok_foot"]:
                warn.append(f"FOOTPRINT {stats['bounds']}")
            if not stats["ok_height"]:
                warn.append(f"HEIGHT {stats['bounds']['H']}")
            if not stats["ok_lod0"]:
                warn.append(f"LOD0 tris {stats['tris']['LOD0']}")
            if warn:
                print(f"[SM] WARN {stats['name']}: {'; '.join(warn)}")
            else:
                print(f"[SM] OK {stats['name']} "
                      f"{stats['tris']}  {stats['bounds']['W']:.2f}x{stats['bounds']['D']:.2f}x{stats['bounds']['H']:.2f}")

    # review tints: materials are shared across civs, so we CANNOT tint all
    # four civs in one scene with one material set. Render per-row (per civ)
    # then composite, OR duplicate materials per civ.
    # Duplicate materials per civ and reassign LOD0 meshes.
    for root, civ in roots:
        # make civ-local copies
        local = {}
        for slot in SLOTS:
            src = mats_by_name[slot]
            m = src.copy()
            m.name = f"{slot}_{civ}"
            local[slot] = m
        apply_review_tints(local, civ)
        for o in root.children:
            if o.type != "MESH":
                continue
            new_slots = []
            for m in o.data.materials:
                new_slots.append(local.get(m.name, m) if not m.name.startswith(tuple(s + "_" for s in SLOTS)) else m)
                # after first pass names are still Trim, Carbon, ...
            # replace
            for i, m in enumerate(list(o.data.materials)):
                key = m.name.split("_")[0] if m.name.split("_")[0] in SLOTS else m.name
                # ChargerAmber, TeamColour, CoreEmission have no underscore in the slot name... 
                # SLOTS names: Trim, Carbon, TeamColour, CoreEmission, Accent, ChargerAmber
                # copy is Trim_Lumen etc. Original is Trim.
                for sl in SLOTS:
                    if m.name == sl or m.name == f"{sl}_{civ}":
                        o.data.materials[i] = local[sl]
                        break

    add_plot_squares(origins)
    co = ensure_camera()
    setup_world(night=False)
    render_to(RENDER_DIR / "contact_dusk.png", co, "dusk", night=False)
    render_to(RENDER_DIR / "contact_top.png", co, "top", night=False)
    render_to(RENDER_DIR / "contact_night.png", co, "dusk", night=True)

    # Pathfinder hero 3/4 (just those five, tighter camera)
    # hide other civs
    for root, civ in roots:
        hide = civ != "Pathfinder"
        root.hide_render = hide
        root.hide_viewport = hide
    # re-aim at Pathfinder row (ci=3)
    # temporarily reuse dusk camera with a tighter scale by hacking place_camera
    # We'll set a custom camera here.
    setup_world(night=False)
    pf_y = -3 * spacing
    pf_cx = 2 * spacing
    target = Vector((pf_cx, pf_y, 1.8))
    elev, az, dist = math.radians(46), math.radians(32), 22
    co.location = target + Vector((
        dist * math.cos(elev) * math.sin(az),
        -dist * math.cos(elev) * math.cos(az),
        dist * math.sin(elev),
    ))
    co.rotation_euler = (target - co.location).to_track_quat("-Z", "Y").to_euler()
    co.data.ortho_scale = 28
    bpy.context.scene.render.filepath = str(RENDER_DIR / "pathfinder_hero.png")
    bpy.ops.render.render(write_still=True)
    print("[SM] rendered pathfinder_hero.png")

    # unhide for blend save
    for root, civ in roots:
        root.hide_render = False
        root.hide_viewport = False

    write_budget(all_stats, SCRIPT_DIR / "BUDGET.md")
    (OUT_DIR / "stats.json").write_text(json.dumps(all_stats, indent=2))
    if BLEND_DIR:
        Path(BLEND_DIR).mkdir(parents=True, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(Path(BLEND_DIR) / "civ_buildings.blend"))
    print("[SM] done", len(all_stats), "buildings")


if __name__ == "__main__" or "--" in sys.argv:
    # Blender -P sets __name__ to the script's __name__ which is often "__main__"
    main()
