"""Solar Majesty - 3D 'SOLAR MAJESTY' title lockup for the in-engine intro.

Run (Blender 4.2.x, headless):
    blender -b --factory-startup -P Blender/scripts/sm_title_logo.py -- [--no-render] [--fast]

Builds the lockup from scratch, saves Blender/SolarMajesty_TitleLogo.blend, writes
    Assets/Art/Intro/SM_Title_SolarMajesty.fbx   (meshes + empties, no materials embedded)
    Assets/Art/Intro/T_Title_Glint.png           (emission sweep band for the gold material)
and (unless --no-render) review renders into $SM_TITLE_RENDER_DIR (default Blender/renders/title):
    title_intro_framing_1280x800.png / _1920x1080.png  (exact intro framing: 11 m, vertical FOV 35)
    title_transparent_3840x2160.png                     (hero, transparent film, for store/splash)
    title_angle.png                                     (three-quarter, shows depth/bevels)
The Unity materials/prefab are built in Unity from this FBX (see the PR); the Blender materials
here only drive the review renders.

Look: Majesty-style regal lettering moved to a space colony. Cinzel Black caps (SIL OFL 1.1,
Blender/fonts/Cinzel-Black.ttf), white hull-panel faces, polished gold bevels/sides, a dark
navy keel outline behind every glyph for contrast, teal emissive inlays. The O of SOLAR is a
crown-ringed planet (white hull sphere, gold ring with a teal inner stripe, small gold crown).

Facing / pivot contract (Unity, after the default FBX import):
    * Text FACES -Z. It reads correctly (left->right along +X) for a camera that looks along +Z.
    * Root pivot = centre of the whole lockup's bounds. Every letter/part has its own pivot
      at its own bounds centre so the Timeline can pop/scale/rise letters individually.
    * Size: 7.00 m wide x 2.34 m tall (crown tip to J tail) x 0.90 m deep (ring), sized for the
      intro slot (11 m from the settled camera, vertical FOV 35) so it mounts at scale 1.
    In Blender that means the lockup faces +Y and reads toward -X (Blender +Y -> Unity -Z with
    the roster's axis settings: forward -Z, up Y, bake space transform).

Hierarchy (.blend and the Unity prefab; the FBX itself is flat - see export_fbx):
    SM_Title_SolarMajesty
      Word_SOLAR   : Letter_SOLAR_0_S, Emblem_Planet {Planet_Sphere, Planet_Ring, Planet_RingInlay,
                     Planet_Crown}, Letter_SOLAR_2_L, Letter_SOLAR_3_A, Letter_SOLAR_4_R
      Word_MAJESTY : Letter_MAJESTY_0_M ... Letter_MAJESTY_6_Y
      Trim         : Trim_Left, Trim_Right
Material slots (names Unity remaps): SM_Title_Hull, SM_Title_Gold, SM_Title_Keel, SM_Title_Accent.
UV0 on every part is ONE planar projection over the whole lockup (u = 0 at the S, 1 at the Y,
v = height / width), so a single band texture offset in u sweeps across the title (shine).
"""

import math
import os
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
DO_RENDER = "--no-render" not in ARGS
FAST = "--fast" in ARGS

REPO = Path(os.environ.get("SM_REPO", Path(__file__).resolve().parents[2]))
FONT_PATH = REPO / "Blender" / "fonts" / "Cinzel-Black.ttf"
OUT_DIR = REPO / "Assets" / "Art" / "Intro"
FBX_PATH = OUT_DIR / "SM_Title_SolarMajesty.fbx"
GLINT_PATH = OUT_DIR / "T_Title_Glint.png"
BLEND_PATH = REPO / "Blender" / "SolarMajesty_TitleLogo.blend"
HULL_TEX = REPO / "Assets" / "Resources" / "Art" / "Materials" / "SM_Mat_WhiteHull_Albedo.png"
HULL_NRM = REPO / "Assets" / "Resources" / "Art" / "Materials" / "SM_Mat_WhiteHull_Normal.png"
RENDER_DIR = Path(os.environ.get("SM_TITLE_RENDER_DIR", REPO / "Blender" / "renders" / "title"))

# Intro slot contract (IntroShot on cursor/first-launch-intro-f7e7): the title is mounted at scale 1,
# 11 m in front of the settled camera (vertical FOV 35 deg -> 6.94 m tall frame, 11.1 m wide at 16:10).
# TITLE_WIDTH is chosen so the lockup fills ~63% of a 16:10 frame / ~57% of 16:9 without rescaling.
TITLE_WIDTH = 7.0
INTRO_DISTANCE = 11.0
INTRO_VFOV = 35.0

# ---------------------------------------------------------------- layout (metres)
CAP_MAJ = 1.60          # cap height of MAJESTY
CAP_SOL = 0.86          # cap height of SOLAR
TRACK_MAJ = 1.06        # Blender space_character
TRACK_SOL = 1.55
LINE_GAP = 0.34         # gap between SOLAR baseline and MAJESTY cap line
DEPTH = 0.30            # letter body thickness (front cap to back cap)
BEVEL = 0.058           # gold bevel radius on MAJESTY (scaled for SOLAR)
BEVEL_RES = 2
INSET = 0.55              # fraction of the bevel the outline is pulled in (1.0 self-intersects Cinzel hairlines)
KEEL_BEVEL = 0.0
RING_TILT = 19.0          # deg the ring plane tips toward the camera (0 = edge-on)
HULL_TILING = 3.0          # Unity SM_Title_Hull _BaseMap_ST.xy
HULL_NORMAL_STRENGTH = 0.45  # Unity _BumpScale
CURVE_RES = 3           # glyph curve resolution (tri budget)
KEEL_GROW = 0.055       # keel outline growth beyond the bevelled glyph
KEEL_DEPTH = 0.16
KEEL_BACK = 0.12        # keel sits this far behind the glyph centre plane

# HudSkin palette (sRGB) -> linear for Blender
def srgb_to_lin(c):
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

GOLD = srgb_to_lin((0.96, 0.79, 0.46))   # HudSkin.Gold pushed toward GoldBright for PBR metal
GOLD_DEEP = srgb_to_lin((0.58, 0.43, 0.20))
HULL = srgb_to_lin((0.93, 0.93, 0.91))
KEEL = srgb_to_lin((0.075, 0.10, 0.16))
TEAL = srgb_to_lin((0.25, 0.86, 1.0))


# ---------------------------------------------------------------- helpers
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    return sc


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def mesh_from_eval(src, name):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(src.evaluated_get(dg))
    me.transform(src.matrix_world)
    me.name = name
    return me


def text_mesh(body, size, track, extrude, bevel, offset, name, bevel_res=None):
    """Extruded glyphs as a mesh in natural orientation: face -Y, reading +X, baseline z=0."""
    font = bpy.data.fonts.load(str(FONT_PATH), check_existing=True)
    cu = bpy.data.curves.new(name + "_crv", "FONT")
    cu.body = body
    cu.font = font
    cu.size = size
    cu.space_character = track
    cu.align_x = "LEFT"
    cu.resolution_u = CURVE_RES
    cu.extrude = extrude
    cu.bevel_depth = bevel
    cu.bevel_resolution = BEVEL_RES if bevel_res is None else bevel_res
    cu.bevel_mode = "ROUND"
    cu.offset = offset
    cu.fill_mode = "BOTH"
    ob = link(bpy.data.objects.new(name + "_tmp", cu))
    ob.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.view_layer.update()
    me = mesh_from_eval(ob, name)
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    return me


def islands(me):
    """Split a mesh into connected face islands -> list of new meshes (sorted by x)."""
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.faces.ensure_lookup_table()
    seen, groups = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, grp = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            grp.append(g.index)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        groups.append(set(grp))
    # a glyph's caps, walls and hole walls can come out as separate islands: fold every island
    # whose XZ bounds sit inside another island's bounds into that one
    def bb(grp):
        xs = [v.co.x for i in grp for v in bm.faces[i].verts]
        zs = [v.co.z for i in grp for v in bm.faces[i].verts]
        return min(xs), max(xs), min(zs), max(zs)
    boxes = [bb(g) for g in groups]
    order = sorted(range(len(groups)), key=lambda i: -(boxes[i][1] - boxes[i][0]) * (boxes[i][3] - boxes[i][2]))
    owner = {}
    for i in order:
        x0, x1, z0, z1 = boxes[i]
        for j in order:
            if j == i or j in owner or (boxes[j][1] - boxes[j][0]) * (boxes[j][3] - boxes[j][2]) < (x1 - x0) * (z1 - z0):
                continue
            X0, X1, Z0, Z1 = boxes[j]
            e = 1e-3
            if x0 >= X0 - e and x1 <= X1 + e and z0 >= Z0 - e and z1 <= Z1 + e:
                owner[i] = j
                break
    def root(i):
        while i in owner:
            i = owner[i]
        return i
    merged = {}
    for i, g in enumerate(groups):
        merged.setdefault(root(i), set()).update(g)
    groups = list(merged.values())
    out = []
    for i, grp in enumerate(groups):
        b2 = bm.copy()
        b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f.index not in grp], context="FACES")
        m = bpy.data.meshes.new(f"{me.name}_{i}")
        b2.to_mesh(m)
        b2.free()
        out.append(m)
    bm.free()
    out.sort(key=lambda m: sum(v.co.x for v in m.vertices) / max(1, len(m.vertices)))
    return out


def bounds(me_or_list):
    mes = me_or_list if isinstance(me_or_list, (list, tuple)) else [me_or_list]
    xs, ys, zs = [], [], []
    for m in mes:
        for v in m.vertices:
            xs.append(v.co.x); ys.append(v.co.y); zs.append(v.co.z)
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))


def merge(meshes, name):
    bm = bmesh.new()
    for m in meshes:
        bm.from_mesh(m)
    out = bpy.data.meshes.new(name)
    bm.to_mesh(out)
    bm.free()
    return out


def set_mats(me, mats):
    idx = [p.material_index for p in me.polygons]   # clear() would reset every index to 0
    me.materials.clear()
    for m in mats:
        me.materials.append(m)
    for p, i in zip(me.polygons, idx):
        p.material_index = i


def assign_glyph_slots(me, front_y, slot_cap, slot_side):
    """Front cap (flat, facing -Y at the front plane) -> slot_cap, everything else -> slot_side."""
    for p in me.polygons:
        if p.normal.y < -0.999 and abs(p.center.y - front_y) < 1e-3:
            p.material_index = slot_cap
        else:
            p.material_index = slot_side


def finish_shading(me, angle_deg=36.0):
    """Triangulate (beauty, so Unity and Blender agree), smooth, sharp by angle + material edges."""
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    lim = math.radians(angle_deg)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        lf = e.link_faces
        sharp = len(lf) != 2
        if not sharp:
            a, b = lf
            sharp = a.material_index != b.material_index or a.normal.angle(b.normal, 0.0) > lim
        e.smooth = not sharp
    bm.to_mesh(me)
    bm.free()


def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


# ---------------------------------------------------------------- materials (Blender preview)
def principled(name, base, metal=0.0, rough=0.5, emit=None, emit_strength=0.0, tex=None, nrm=None,
               use_albedo=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*base, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    if emit is not None:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1)
        bsdf.inputs["Emission Strength"].default_value = emit_strength
    if tex is not None and Path(tex).exists():
        uv = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (HULL_TILING, HULL_TILING, 1.0)  # Unity: _BaseMap_ST
        nt.links.new(uv.outputs["UV"], mp.inputs["Vector"])
        im = nt.nodes.new("ShaderNodeTexImage")
        im.image = bpy.data.images.load(str(tex), check_existing=True)
        nt.links.new(mp.outputs["Vector"], im.inputs["Vector"])
        mul = nt.nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1.0
        nt.links.new(im.outputs["Color"], mul.inputs[6])
        mul.inputs[7].default_value = (*base, 1)
        if use_albedo:
            nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])
        if nrm is not None and Path(nrm).exists():
            ni = nt.nodes.new("ShaderNodeTexImage")
            ni.image = bpy.data.images.load(str(nrm), check_existing=True)
            ni.image.colorspace_settings.name = "Non-Color"
            nt.links.new(mp.outputs["Vector"], ni.inputs["Vector"])
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nm.inputs["Strength"].default_value = HULL_NORMAL_STRENGTH
            nt.links.new(ni.outputs["Color"], nm.inputs["Color"])
            nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return m


def make_materials():
    # The white hull albedo is already near-white, so the base tint stays neutral.
    # Clean white hull: the shared hull NORMAL map only (panel seams), no albedo grid.
    hull = principled("SM_Title_Hull", HULL, 0.0, 0.38, tex=HULL_TEX, nrm=HULL_NRM)
    gold = principled("SM_Title_Gold", GOLD, 1.0, 0.24)
    keel = principled("SM_Title_Keel", KEEL, 0.55, 0.38)
    acc = principled("SM_Title_Accent", (0.05, 0.25, 0.32), 0.0, 0.3, emit=TEAL, emit_strength=3.0)
    return hull, gold, keel, acc


# ---------------------------------------------------------------- geometry
def build_word(text, cap, track, bevel_scale):
    """Returns ([(char, letter_mesh)], baseline-relative), meshes have slots [hull, gold, keel]."""
    probe = text_mesh("M", 1.0, 1.0, 0.0, 0.0, 0.0, "probe")
    lo, hi = bounds(probe)
    bpy.data.meshes.remove(probe)
    size = cap / (hi.z - lo.z)
    bev = BEVEL * bevel_scale
    ext = DEPTH * 0.5 - bev
    body = text_mesh(text, size, track, ext, bev, -bev * INSET, f"{text}_body")
    keel = text_mesh(text, size, track, KEEL_DEPTH * 0.5 - KEEL_BEVEL, KEEL_BEVEL, KEEL_GROW * bevel_scale,
                     f"{text}_keel", bevel_res=0)
    keel.transform(Matrix.Translation((0, KEEL_BACK, 0)))
    bparts, kparts = islands(body), islands(keel)
    bpy.data.meshes.remove(body)
    bpy.data.meshes.remove(keel)
    if len(bparts) != len(text) or len(kparts) != len(text):
        raise RuntimeError(f"{text}: {len(bparts)} glyph islands / {len(kparts)} keel islands")
    letters = []
    for ch, b, k in zip(text, bparts, kparts):
        front = bounds(b)[0].y
        assign_glyph_slots(b, front, 0, 1)
        for p in k.polygons:
            p.material_index = 2
        m = merge([b, k], f"{text}_{ch}")
        bpy.data.meshes.remove(b)
        bpy.data.meshes.remove(k)
        letters.append([ch, m])
    return letters


def translate(me, d):
    me.transform(Matrix.Translation(d))


def build_planet(center, radius):
    """Crown-ringed planet replacing the O. Natural orientation (faces -Y). Returns {name: mesh}."""
    parts = {}
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=20, radius=radius)
    sph = bpy.data.meshes.new("Planet_Sphere")
    bm.to_mesh(sph); bm.free()
    for p in sph.polygons:
        p.material_index = 0
    parts["Planet_Sphere"] = sph

    # gold ring: flattened torus, tilted toward camera and rolled a little
    ring_tf = (Matrix.Rotation(math.radians(-13), 4, "Y")
               @ Matrix.Rotation(math.radians(RING_TILT), 4, "X"))

    def torus(name, R, r, segs, rsegs, flat, slot):
        bm = bmesh.new()
        verts = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ring = []
            for j in range(rsegs):
                b = 2 * math.pi * j / rsegs
                rr = R + r * math.cos(b)
                ring.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), r * flat * math.sin(b))))
            verts.append(ring)
        for i in range(segs):
            for j in range(rsegs):
                a, b = verts[i][j], verts[(i + 1) % segs][j]
                c, d = verts[(i + 1) % segs][(j + 1) % rsegs], verts[i][(j + 1) % rsegs]
                bm.faces.new((a, b, c, d))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me); bm.free()
        for p in me.polygons:
            p.material_index = slot
        me.transform(ring_tf)
        return me

    parts["Planet_Ring"] = torus("Planet_Ring", radius * 1.48, radius * 0.17, 72, 10, 0.34, 1)
    parts["Planet_RingInlay"] = torus("Planet_RingInlay", radius * 1.25, radius * 0.045, 72, 6, 1.0, 3)

    # crown: band + 5 points with ball tips, sitting on the planet's north pole, facing -Y
    bm = bmesh.new()
    cr, top = radius * 0.56, radius * 0.83
    band_h = radius * 0.24
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=24,
                                radius1=cr, radius2=cr * 1.08, depth=band_h)
    bmesh.ops.translate(bm, verts=res["verts"], vec=(0, 0, top + band_h * 0.5))
    for k in range(5):
        a = math.radians(-90 + (k - 2) * 36)          # spread over the camera-facing half
        x, y = cr * 1.0 * math.cos(a), cr * 1.0 * math.sin(a)
        h = radius * (0.78 if k == 2 else 0.58 if k in (1, 3) else 0.44)
        res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=8,
                                    radius1=radius * 0.15, radius2=radius * 0.015, depth=h)
        bmesh.ops.translate(bm, verts=res["verts"], vec=(x, y, top + band_h + h * 0.5 - radius * 0.02))
        res = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=radius * 0.09)
        bmesh.ops.translate(bm, verts=res["verts"], vec=(x, y, top + band_h + h))
    for k in range(5):                                # back points (fewer, smaller) for depth
        a = math.radians(90 + (k - 2) * 36)
        x, y = cr * math.cos(a), cr * math.sin(a)
        h = radius * 0.34
        res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=6,
                                    radius1=radius * 0.11, radius2=radius * 0.015, depth=h)
        bmesh.ops.translate(bm, verts=res["verts"], vec=(x, y, top + band_h + h * 0.5 - radius * 0.02))
    crown = bpy.data.meshes.new("Planet_Crown")
    bm.to_mesh(crown); bm.free()
    for p in crown.polygons:
        p.material_index = 1
    parts["Planet_Crown"] = crown
    for me in parts.values():
        translate(me, Vector(center))
    return parts


def build_trim(x_outer, x_inner, z, height, side):
    """Gold blade bar with a diamond at the inner end and a teal inlay; natural orientation."""
    bm = bmesh.new()
    L = abs(x_inner - x_outer)
    sgn = 1 if x_inner > x_outer else -1
    d = 0.10
    # tapered blade: thin at the outer end, full height near the diamond
    prof = [(0.0, 0.18), (0.55, 0.62), (1.0, 1.0)]
    rings = []
    for t, s in prof:
        x = x_outer + sgn * L * 0.86 * t
        hh = height * 0.5 * s
        rings.append([bm.verts.new((x, -d, z - hh)), bm.verts.new((x, -d, z + hh)),
                      bm.verts.new((x, d, z + hh)), bm.verts.new((x, d, z - hh))])
    for a, b in zip(rings, rings[1:]):
        for i in range(4):
            bm.faces.new((a[i], b[i], b[(i + 1) % 4], a[(i + 1) % 4]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    # diamond
    dc = Vector((x_outer + sgn * L * 0.93, 0.0, z))
    dr = height * 1.25
    pts = [dc + Vector((sgn * dr, 0, 0)), dc + Vector((0, 0, dr * 0.8)), dc + Vector((-sgn * dr, 0, 0)),
           dc + Vector((0, 0, -dr * 0.8))]
    fv = [bm.verts.new(p + Vector((0, -d * 1.3, 0))) for p in [dc]][0]
    bv = bm.verts.new(dc + Vector((0, d * 1.1, 0)))
    rv = [bm.verts.new(p) for p in pts]
    for i in range(4):
        bm.faces.new((fv, rv[i], rv[(i + 1) % 4]))
        bm.faces.new((bv, rv[(i + 1) % 4], rv[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.material_index = 1
    # teal inlay on the blade front
    n0 = len(bm.faces)
    x0, x1 = x_outer + sgn * L * 0.30, x_outer + sgn * L * 0.80
    ih = height * 0.16
    v = [bm.verts.new((x0, -d - 0.012, z - ih)), bm.verts.new((x1, -d - 0.012, z - ih)),
         bm.verts.new((x1, -d - 0.012, z + ih)), bm.verts.new((x0, -d - 0.012, z + ih))]
    f = bm.faces.new(v if sgn < 0 else list(reversed(v)))
    f.normal_update()
    if f.normal.y > 0:
        f.normal_flip()
    f.material_index = 3
    me = bpy.data.meshes.new(f"Trim_{side}")
    bm.to_mesh(me); bm.free()
    return me


def planar_uv(me, xmin, zmin, width):
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = ((co.x - xmin) / width, (co.z - zmin) / width)


# ---------------------------------------------------------------- glint texture
def write_glint(path, w=512, h=128):
    """Black, with one soft slanted white band at u=0.5 (clamp). Sweep by offsetting u."""
    img = bpy.data.images.new("T_Title_Glint", w, h, alpha=False)
    px = [0.0] * (w * h * 4)
    for y in range(h):
        v = y / (h - 1)
        for x in range(w):
            u = x / (w - 1)
            d = (u + 0.22 * (v - 0.5)) - 0.5
            core = math.exp(-(d / 0.018) ** 2)
            halo = 0.35 * math.exp(-(d / 0.07) ** 2)
            c = min(1.0, core + halo)
            i = (y * w + x) * 4
            px[i:i + 4] = (c, c, c, 1.0)
    img.pixels = px
    img.filepath_raw = str(path)
    img.file_format = "PNG"
    img.save()
    return img


# ---------------------------------------------------------------- main build
def build():
    hull, gold, keel, acc = make_materials()
    slots = [hull, gold, keel, acc]

    sol = build_word("SOLAR", CAP_SOL, TRACK_SOL, CAP_SOL / CAP_MAJ * 1.15)
    maj = build_word("MAJESTY", CAP_MAJ, TRACK_MAJ, 1.0)

    # MAJESTY: baseline z=0, centred on x=0
    lo, hi = bounds([m for _, m in maj])
    for _, m in maj:
        translate(m, Vector((-(lo.x + hi.x) / 2, 0, 0)))
    maj_lo, maj_hi = bounds([m for _, m in maj])

    # SOLAR: replace O with the planet, open the spacing around it for the ring
    o_lo, o_hi = bounds(sol[1][1])
    o_c = (o_lo + o_hi) / 2
    r = (o_hi.z - o_lo.z) * 0.5 * 1.02
    bpy.data.meshes.remove(sol[1][1])
    gap = r * 0.42
    translate(sol[0][1], Vector((-gap, 0, 0)))
    for i in (2, 3, 4):
        translate(sol[i][1], Vector((gap, 0, 0)))
    planet = build_planet((o_c.x, 0.0, o_c.z), r)
    sol_meshes = [sol[0][1]] + list(planet.values()) + [m for _, m in sol[2:]]
    lo, hi = bounds([sol[0][1], sol[4][1]])
    sol_base_z = maj_hi.z + LINE_GAP
    for m in sol_meshes:
        translate(m, Vector((-(lo.x + hi.x) / 2, 0, sol_base_z)))
    s_lo, s_hi = bounds([sol[0][1], sol[4][1]])

    # trims: from MAJESTY's outer edges to just outside SOLAR, at SOLAR mid-height
    z_mid = sol_base_z + CAP_SOL * 0.5
    pad = 0.30
    trim_l = build_trim(maj_lo.x + 0.05, s_lo.x - pad, z_mid, CAP_SOL * 0.22, "Left")
    trim_r = build_trim(maj_hi.x - 0.05, s_hi.x + pad, z_mid, CAP_SOL * 0.22, "Right")

    parts = []  # (group, name, mesh)
    parts.append(("Word_SOLAR", "Letter_SOLAR_0_S", sol[0][1]))
    for k, me in planet.items():
        parts.append(("Emblem_Planet", k, me))
    for i in (2, 3, 4):
        parts.append(("Word_SOLAR", f"Letter_SOLAR_{i}_{sol[i][0]}", sol[i][1]))
    for i, (ch, me) in enumerate(maj):
        parts.append(("Word_MAJESTY", f"Letter_MAJESTY_{i}_{ch}", me))
    parts.append(("Trim", "Trim_Left", trim_l))
    parts.append(("Trim", "Trim_Right", trim_r))

    # centre the whole lockup on its bounds, then UVs (natural orientation, u along reading dir)
    lo, hi = bounds([p[2] for p in parts])
    c = (lo + hi) / 2
    for _, _, me in parts:
        translate(me, -c)
    lo, hi = bounds([p[2] for p in parts])
    width = hi.x - lo.x
    for _, _, me in parts:
        planar_uv(me, lo.x, lo.z, width)
        set_mats(me, slots)
        finish_shading(me)
    # uniform scale to the intro slot width (layout constants above are authored at ~11.3 m wide)
    k = TITLE_WIDTH / width
    for _, _, me in parts:
        me.transform(Matrix.Scale(k, 4))

    # turn 180 deg about Z: Blender +Y-facing == Unity -Z-facing after FBX import
    flip = Matrix.Rotation(math.pi, 4, "Z")
    for _, _, me in parts:
        me.transform(flip)

    root = link(bpy.data.objects.new("SM_Title_SolarMajesty", None))
    root.empty_display_type = "PLAIN_AXES"
    groups = {}
    for gname, parent_name in (("Word_SOLAR", None), ("Emblem_Planet", "Word_SOLAR"),
                               ("Word_MAJESTY", None), ("Trim", None)):
        mes = [p[2] for p in parts if p[0] == gname]
        if gname == "Word_SOLAR":
            mes += [p[2] for p in parts if p[0] == "Emblem_Planet"]
        glo, ghi = bounds(mes)
        gc = (glo + ghi) / 2 if gname != "Trim" else Vector((0, 0, ((glo + ghi) / 2).z))
        e = link(bpy.data.objects.new(gname, None))
        e.empty_display_type = "PLAIN_AXES"
        e.empty_display_size = 0.3
        e.parent = groups[parent_name] if parent_name else root
        e.matrix_world = Matrix.Translation(gc)
        groups[gname] = e
    objs = []
    for gname, name, me in parts:
        plo, phi = bounds(me)
        pc = (plo + phi) / 2
        if name.startswith("Planet_"):
            pc = groups["Emblem_Planet"].matrix_world.translation.copy()   # spin about planet centre
        translate(me, -pc)
        me.name = name
        ob = link(bpy.data.objects.new(name, me))
        ob.parent = groups[gname]
        ob.matrix_world = Matrix.Translation(pc)
        objs.append(ob)
    bpy.context.view_layer.update()
    return root, groups, objs, slots


def report(objs):
    total = 0
    lines = []
    for ob in objs:
        t = tri_count(ob.data)
        total += t
        lines.append(f"  {ob.name:24s} tris={t:6d}")
    lo, hi = bounds([o.data.copy() for o in []] or [_world_mesh(o) for o in objs])
    print("[SM-TITLE] parts:\n" + "\n".join(lines))
    print(f"[SM-TITLE] total tris={total}  size(m) x={hi.x - lo.x:.2f} y(depth)={hi.y - lo.y:.2f} z={hi.z - lo.z:.2f}")
    print(f"[SM-TITLE] bounds lo={tuple(round(v, 3) for v in lo)} hi={tuple(round(v, 3) for v in hi)}")
    return total, (hi - lo)


def _world_mesh(ob):
    m = ob.data.copy()
    m.transform(ob.matrix_world)
    return m


def export_fbx(root):
    """Export the parts FLAT (meshes at the FBX root, each at its own pivot).

    Blender's bake_space_transform mis-converts meshes parented under empties (they import rotated
    90 deg about X with y/z swapped), so the Word_/Emblem_/Trim groups live in the .blend for
    authoring only and the Unity prefab rebuilds them (same names, pivots at group bounds centres).
    """
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    meshes = [o for o in root.children_recursive if o.type == "MESH"]
    saved = {o: (o.parent, o.matrix_world.copy()) for o in meshes}
    for o in meshes:
        o.parent = None
        o.matrix_world = saved[o][1]
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        apply_unit_scale=True,
        bake_space_transform=True,
        object_types={"MESH"},
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="STRIP",
        embed_textures=False,
        bake_anim=False,
    )
    for o in meshes:
        o.parent = saved[o][0]
        o.matrix_world = saved[o][1]
    bpy.context.view_layer.update()
    print(f"[SM-TITLE] exported {FBX_PATH} ({len(meshes)} meshes, flat)")


# ---------------------------------------------------------------- review renders
def dusk_world(sc):
    w = bpy.data.worlds.new("SM_DuskSky")
    sc.world = w
    w.use_nodes = True
    nt = w.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    bg = nt.nodes.new("ShaderNodeBackground")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    # Generated on the world = view direction; z in [-1,1] -> map to [0,1]
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = -0.35
    mr.inputs["From Max"].default_value = 0.9
    nt.links.new(sep.outputs["Z"], mr.inputs["Value"])
    nt.links.new(mr.outputs["Result"], ramp.inputs["Fac"])
    cr = ramp.color_ramp
    cr.elements[0].position = 0.0
    cr.elements[0].color = (0.05, 0.03, 0.04, 1)
    cr.elements[1].position = 1.0
    cr.elements[1].color = (0.004, 0.006, 0.025, 1)
    for pos, col in ((0.26, (1.6, 0.55, 0.16, 1)), (0.36, (0.75, 0.22, 0.20, 1)),
                     (0.50, (0.16, 0.07, 0.20, 1)), (0.68, (0.025, 0.03, 0.10, 1))):
        el = cr.elements.new(pos)
        el.color = col
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.5
    nt.links.new(bg.outputs[0], out.inputs[0])


def lights(sc):
    def sun(name, rot, energy, color, angle=2.0):
        l = bpy.data.lights.new(name, "SUN")
        l.energy = energy
        l.color = color
        l.angle = math.radians(angle)
        o = link(bpy.data.objects.new(name, l))
        o.rotation_euler = [math.radians(a) for a in rot]
        return o
    # key: warm low dusk sun from camera-left/above (camera sits at +Y looking -Y)
    sun("Key_DuskSun", (58, 0, 150), 6.0, (1.0, 0.80, 0.58))
    # cool sky fill from the other side
    sun("Fill_Sky", (66, 0, 215), 1.8, (0.60, 0.74, 1.0))
    # rim from behind/above to catch the gold bevels
    sun("Rim_Back", (-50, 0, 180), 3.0, (1.0, 0.62, 0.35))


def camera(sc, loc, look, vfov_deg, name="Cam"):
    cd = bpy.data.cameras.new(name)
    cd.sensor_fit = "VERTICAL"
    cd.angle_y = math.radians(vfov_deg)
    cd.clip_start = 0.1
    cd.clip_end = 500
    cam = link(bpy.data.objects.new(name, cd))
    cam.location = loc
    d = Vector(look) - Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    return cam


def render(sc, path, w, h, samples, transparent):
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 6
    sc.render.resolution_x = w
    sc.render.resolution_y = h
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA" if transparent else "RGB"
    sc.render.image_settings.color_depth = "8"
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Punchy"
    sc.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    print(f"[SM-TITLE] render {path}")


def do_renders(sc, size):
    RENDER_DIR.mkdir(parents=True, exist_ok=True)
    dusk_world(sc)
    lights(sc)
    s = 0.25 if FAST else 1.0
    spp = 16 if FAST else 64
    # Exact intro framing: camera on +Y looking along -Y (= Unity +Z), 11 m, vertical FOV 35.
    camera(sc, (0, INTRO_DISTANCE, 0), (0, 0, 0), INTRO_VFOV)
    render(sc, RENDER_DIR / "title_intro_framing_1280x800.png", 1280, 800, max(16, spp // 2), False)
    render(sc, RENDER_DIR / "title_intro_framing_1920x1080.png", int(1920 * s), int(1080 * s), spp, False)
    # Hero: tighter, 4K, transparent film (store page / splash); composited onto a dusk sky afterwards.
    camera(sc, (0, 7.6, 0), (0, 0, 0), INTRO_VFOV)
    render(sc, RENDER_DIR / "title_transparent_3840x2160.png", int(3840 * s), int(2160 * s), spp, True)
    # Three-quarter look to show depth / bevels (an approach angle for the Timeline).
    camera(sc, (-5.2, 8.6, -1.6), (0, 0, 0), INTRO_VFOV)
    render(sc, RENDER_DIR / "title_angle.png", int(2400 * s), int(1350 * s), spp, False)


def main():
    sc = reset_scene()
    if not FONT_PATH.exists():
        raise SystemExit(f"font missing: {FONT_PATH}")
    root, groups, objs, slots = build()
    total, size = report(objs)
    write_glint(GLINT_PATH)
    export_fbx(root)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    print(f"[SM-TITLE] saved {BLEND_PATH}")
    if DO_RENDER:
        do_renders(sc, size)
    print("[SM-TITLE] done")


main()
