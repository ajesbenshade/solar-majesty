"""
Solar Majesty: colonist "colony stride", an original long-stride lope for the colonist suits.

Blender 4.2.3, scripted and reproducible. It reuses the colonist skeleton from sm_colonists.py
(Kevin Iglesias joint names, re-proportioned, facing -Y in Blender = +Z in Unity) and keys one
looping in-place cycle analytically. Feet are placed on the ground and the legs solved with an
exact two-bone IK, so the planted foot slides back at exactly STRIDE_SPEED.

Why it is a lope and not a walk: the suits have 0.405 m legs (hip joint to ankle). At the
villagers' 2.4 m/s that is Froude ~1.2, past the walk-to-run limit (~0.5). A walk would need
~6 steps/s (the old Walk01 at 2.5x), and that is the scurry. This cycle trades steps for a short
low-gravity flight phase.
- each step covers 1.26 m: 0.42 m of foot contact plus a 0.84 m float
- one cycle (two steps) is 1.2 s at 1x
- at 2.4 m/s it plays at ~1.14x, about 1.9 steps/s

Usage:
  blender -b --python Blender/scripts/sm_colonist_stride.py -- \
      --export Assets/Art/Colonists/Anim --blend Blender/SolarMajesty_ColonistStride.blend \
      [--review /tmp/stride_review]

Exports SM_Colonist@Stride.fbx (armature + "Stride" take, no mesh). Unity imports it as Humanoid
with the same explicit human map as the suits.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sm_colonists as base  # noqa: E402

FPS = 30
CYCLE = 36                    # frames per cycle (two steps) -> 1.2 s at 1x
STEP = CYCLE // 2
STANCE = 6                    # frames each foot is planted
STRIDE_SPEED = 2.1            # m/s ground speed the cycle covers at 1x
CONTACT = STRIDE_SPEED * STANCE / FPS          # 0.42 m of planted travel
STEP_LEN = STRIDE_SPEED * STEP / FPS           # 1.26 m per step
ANKLE_Z = 0.075               # ankle height with the boot flat (rest pose)
TOE_OFS = Vector((0.0, -0.087, -0.045))        # toe joint relative to ankle, rest
FWD = Vector((0.0, -1.0, 0.0))                 # colonists face -Y in Blender

HEAD = {b[0]: Vector(b[2]) for b in base.BONES}
TAIL = {b[0]: Vector(b[3]) for b in base.BONES}
L_THIGH = (HEAD["B-shin.L"] - HEAD["B-thigh.L"]).length
L_SHIN = (HEAD["B-foot.L"] - HEAD["B-shin.L"]).length


def _num(v):
    return float(v) if isinstance(v, (int, float)) else Vector(v)


def catmull(points, s, periodic=False):
    """Catmull-Rom through (s, value) points; values may be floats or Vectors."""
    n = len(points)
    if periodic:
        s = s % 1.0
    for i in range(n - 1):
        s0, s1 = points[i][0], points[i + 1][0]
        if s0 <= s <= s1:
            break
    else:
        i = n - 2
        s0, s1 = points[i][0], points[i + 1][0]
    t = 0.0 if s1 == s0 else (s - s0) / (s1 - s0)

    def p(k):
        if periodic:
            k %= n - 1
        else:
            k = max(0, min(n - 1, k))
        return points[k][1]
    p0, p1, p2, p3 = (_num(p(k)) for k in (i - 1, i, i + 1, i + 2))
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)


# Hip (B-hips head) height over one step: touchdown, compress mid-stance, push-off, float apex.
HIPS_Z = [(0 / 18, 0.432), (3 / 18, 0.418), (6 / 18, 0.445), (12 / 18, 0.490), (18 / 18, 0.432)]

# Swing path of the ankle, relative to the hips, from lift-off (s=0) to touchdown (s=1):
# heel kick behind, knee drive, reach, then pull back onto the ground.
SWING = [
    (0.00, Vector((0.0, 0.183, 0.108))),
    (0.18, Vector((0.0, 0.290, 0.215))),
    (0.45, Vector((0.0, 0.040, 0.235))),
    (0.72, Vector((0.0, -0.245, 0.170))),
    (0.90, Vector((0.0, -0.228, 0.098))),
    (1.00, Vector((0.0, -CONTACT / 2, ANKLE_Z))),
]
# Foot pitch (deg, + = heel up / toe down) over the swing.
SWING_PITCH = [(0.00, 25.0), (0.18, 48.0), (0.45, 22.0), (0.72, -12.0), (0.90, -8.0), (1.00, 0.0)]


def foot_target(f, td, x):
    """Ankle position, foot pitch (rad), toe-flat flag for a leg whose touchdown frame is td."""
    u = (f - td) % CYCLE
    if u <= STANCE:
        g = -CONTACT / 2 + STRIDE_SPEED * u / FPS          # ground contact moves back (+Y)
        a = 0.0 if u <= 4 else math.radians(25.0) * (u - 4) / 2.0
        if a == 0.0:
            return Vector((x, g, ANKLE_Z)), 0.0, True
        toe = Vector((x, g, ANKLE_Z)) + TOE_OFS            # toe stays planted, heel peels up
        ankle = toe + Matrix.Rotation(a, 3, "X") @ (-TOE_OFS)
        return ankle, a, True
    s = (u - STANCE) / (CYCLE - STANCE)
    v = catmull(SWING, s)
    return Vector((x, v.y, v.z)), math.radians(catmull(SWING_PITCH, s)), False


def two_bone(hip, ankle):
    """Knee position for an exact two-bone solve, knee bending toward FWD."""
    d_vec = ankle - hip
    d = min(d_vec.length, (L_THIGH + L_SHIN) * 0.9995)
    dirv = d_vec.normalized()
    a = (L_THIGH ** 2 - L_SHIN ** 2 + d * d) / (2 * d)
    h = math.sqrt(max(0.0, L_THIGH ** 2 - a * a))
    perp = (FWD - dirv * FWD.dot(dirv)).normalized()
    return hip + dirv * a + perp * h, hip + dirv * d


def rest_dir(name):
    return (TAIL[name] - HEAD[name]).normalized()


def set_bone(rig, name, rot, head=None):
    """Pose a bone by its armature-space rotation from rest (and optionally its head)."""
    pb = rig.pose.bones[name]
    bpy.context.view_layer.update()
    h = head if head is not None else pb.head.copy()
    rest = rig.data.bones[name].matrix_local.to_3x3()
    pb.matrix = Matrix.Translation(h) @ (rot.to_matrix() @ rest).to_4x4()
    bpy.context.view_layer.update()
    return rot


def pose_frame(rig, f):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)
    w = math.tau * f / CYCLE
    ph = (f % STEP) / STEP
    hz = catmull(HIPS_Z, ph, periodic=True)
    # Pelvis: yaw with the forward leg, slight sway over the planted foot.
    yaw = math.radians(-7.0) * math.cos(w)
    sway = 0.012 * math.cos(w)
    r_hips = Quaternion((0, 0, 1), yaw)
    set_bone(rig, "B-hips", r_hips, Vector((sway, 0.0, hz)))
    lean = math.radians(11.0)
    r_spine = Quaternion((1, 0, 0), lean * 0.7) @ Quaternion((0, 0, 1), yaw * 0.3)
    set_bone(rig, "B-spine", r_spine)
    r_chest = Quaternion((1, 0, 0), lean) @ Quaternion((0, 0, 1), -yaw * 0.9)
    set_bone(rig, "B-chest", r_chest)
    set_bone(rig, "B-neck", Quaternion((1, 0, 0), lean * 0.5))
    set_bone(rig, "B-head", Quaternion((1, 0, 0), math.radians(1.5) - 0.02 * math.cos(2 * w)))

    # Arms: hang ~22 deg off the body, counter-swing the legs, jog-bent elbows.
    for s, x, sgn in (("L", 1.0, 1.0), ("R", -1.0, -1.0)):
        set_bone(rig, f"B-shoulder.{s}", r_chest)
        sw = math.radians(8.0) - sgn * math.radians(34.0) * math.cos(w - 0.35)
        ab = math.radians(24.0)
        up_dir = Vector((x * math.sin(ab), -math.cos(ab) * math.sin(sw), -math.cos(ab) * math.cos(sw))).normalized()
        r_up = rest_dir(f"B-upperArm.{s}").rotation_difference(up_dir)
        set_bone(rig, f"B-upperArm.{s}", r_up)
        bend = math.radians(68.0 + 16.0 * max(0.0, math.sin(sw)) / math.sin(math.radians(42)))
        perp = (FWD - up_dir * FWD.dot(up_dir)).normalized()
        axis = up_dir.cross(perp).normalized()
        r_fore = Quaternion(axis, bend) @ r_up
        set_bone(rig, f"B-forearm.{s}", r_fore)
        set_bone(rig, f"B-hand.{s}", r_fore)

    # Legs: L touches down at frame 0, R at frame STEP.
    for s, x, td in (("L", 0.10, 0), ("R", -0.10, STEP)):
        ankle, pitch, planted = foot_target(f, td, x)
        hip = rig.pose.bones[f"B-thigh.{s}"].head.copy()
        knee, ankle_hit = two_bone(hip, ankle)
        r_th = rest_dir(f"B-thigh.{s}").rotation_difference((knee - hip).normalized())
        set_bone(rig, f"B-thigh.{s}", r_th)
        shin_dir_rest = r_th @ rest_dir(f"B-shin.{s}")
        r_sh = shin_dir_rest.rotation_difference((ankle_hit - knee).normalized()) @ r_th
        set_bone(rig, f"B-shin.{s}", r_sh, knee)
        r_ft = Quaternion((1, 0, 0), pitch)
        set_bone(rig, f"B-foot.{s}", r_ft, ankle_hit)
        set_bone(rig, f"B-toe.{s}", Quaternion() if planted else r_ft)


def key_cycle(rig):
    scn = bpy.context.scene
    scn.render.fps = FPS
    scn.frame_start, scn.frame_end = 0, CYCLE
    rig.animation_data_create()
    act = bpy.data.actions.new("Stride")
    rig.animation_data.action = act
    stats = []
    for f in range(CYCLE + 1):
        scn.frame_set(f)
        pose_frame(rig, f % CYCLE)
        for pb in rig.pose.bones:
            pb.keyframe_insert("location", frame=f, group=pb.name)
            pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
        bpy.context.view_layer.update()
        la = rig.matrix_world @ rig.pose.bones["B-foot.L"].head
        ra = rig.matrix_world @ rig.pose.bones["B-foot.R"].head
        stats.append((f, la.copy(), ra.copy()))
    for fc in act.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    return act, stats


def report(stats):
    # Planted-foot speed and ground contact straight from the solved pose.
    vs, zs = [], []
    for (f0, l0, r0), (f1, l1, r1) in zip(stats, stats[1:]):
        for td, a0, a1 in ((0, l0, l1), (STEP, r0, r1)):
            u = (f0 - td) % CYCLE
            if u < 4:                         # flat-foot part of the stance
                vs.append((a1.y - a0.y) * FPS)
                zs.append(a0.z)
    print(f"[SM] stride: cycle {CYCLE} f ({CYCLE / FPS:.2f} s), step {STEP_LEN:.2f} m, contact {CONTACT:.2f} m, "
          f"planted speed {min(vs):.3f}..{max(vs):.3f} m/s, planted ankle z {min(zs):.3f}..{max(zs):.3f}, "
          f"at 2.4 m/s -> {2.4 / STRIDE_SPEED:.3f}x, {2.4 / STEP_LEN:.2f} steps/s")


def export_anim(rig, path):
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL", apply_unit_scale=True,
        object_types={"ARMATURE"}, add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        armature_nodetype="NULL", bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, axis_forward="-Z", axis_up="Y", path_mode="STRIP",
    )


def review(rig, out_dir):
    """Small Cycles side-view strip of the Engineer suit on the stride (review only)."""
    atlas = base.build_atlas(os.path.join(out_dir, "T_Colonist_Suit.png"))
    suit, roles = base.make_materials(atlas)
    k = base.Kit("SM_Colonist_Engineer")
    base.build_body(k, "Engineer")
    base.build_role(k, "Engineer")
    obj = k.to_object([suit, roles["Engineer"]])
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    scn = bpy.context.scene
    scn.render.engine = "CYCLES"
    scn.cycles.samples = 12
    scn.cycles.use_denoising = True
    scn.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("Review")
    scn.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.66, 0.80, 1)
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    scn.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(40), 0, math.radians(60))
    scn.render.resolution_x, scn.render.resolution_y = 360, 360
    cam_d = bpy.data.cameras.new("Cam")
    cam_d.type = "ORTHO"
    cam_d.ortho_scale = 1.7
    cam = bpy.data.objects.new("Cam", cam_d)
    scn.collection.objects.link(cam)
    scn.camera = cam
    cam.location = (6.0, 0.0, 0.62)
    cam.rotation_euler = (math.radians(90), 0, math.radians(90))
    bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, 0))
    for f in range(0, CYCLE, 3):
        scn.frame_set(f)
        scn.render.filepath = os.path.join(out_dir, f"stride_{f:02d}.png")
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(flag, default=None):
        return argv[argv.index(flag) + 1] if flag in argv else default

    export_dir, blend, review_dir = opt("--export"), opt("--blend"), opt("--review")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    rig = base.build_rig("SM_Colonist_Stride")
    act, stats = key_cycle(rig)
    report(stats)
    if export_dir:
        os.makedirs(export_dir, exist_ok=True)
        export_anim(rig, os.path.join(export_dir, "SM_Colonist@Stride.fbx"))
    if review_dir:
        os.makedirs(review_dir, exist_ok=True)
        review(rig, review_dir)
    if blend:
        for img in bpy.data.images:
            if img.filepath:
                img.filepath = ""
                img.pack() if img.has_data else None
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend), compress=True)


if __name__ == "__main__":
    main()
