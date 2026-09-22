"""Render prototype terrain in Blender (bpy). Usage: python3 render_terrain.py Mars,Luna out_dir [seed]"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bodies  # noqa: E402
import shade  # noqa: E402

SUN = {  # Unity SunEuler (pitch, yaw) from CelestialBodyCatalog
    "Earth": (38, -32), "Luna": (48, -35), "Mars": (38, -20), "Belt": (22, 48), "Europa": (28, 12)}
SKY = {"Earth": (0.62, 0.74, 0.88), "Luna": (0.02, 0.02, 0.025), "Mars": (0.85, 0.55, 0.36),
       "Belt": (0.02, 0.02, 0.03), "Europa": (0.08, 0.10, 0.14)}
AMB = {"Earth": 0.9, "Luna": 0.08, "Mars": 0.7, "Belt": 0.06, "Europa": 0.25}


def build(body, seed, outdir):
    X, Z, step, H, m = bodies.GENS[body](seed)
    alb, extra = shade.albedo(body, X, Z, step, H, m, seed)
    hi = shade.upsample_with_detail(body, alb, X, Z)
    tex = os.path.join(outdir, f"{body}_albedo.png")
    Image.fromarray((hi * 255).astype(np.uint8)).save(tex)
    return X, Z, H, m, tex


def make_scene(body, X, Z, H, m, tex):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    n = H.shape[0]
    verts = np.stack([X.ravel(), Z.ravel(), H.ravel()], -1)
    idx = np.arange(n * n).reshape(n, n)
    a, b, c, d = idx[:-1, :-1].ravel(), idx[:-1, 1:].ravel(), idx[1:, 1:].ravel(), idx[1:, :-1].ravel()
    faces = np.stack([a, b, c, d], -1)
    me = bpy.data.meshes.new("T")
    me.vertices.add(len(verts))
    me.vertices.foreach_set("co", verts.astype(np.float32).ravel())
    me.loops.add(faces.size)
    me.loops.foreach_set("vertex_index", faces.astype(np.int32).ravel())
    me.polygons.add(len(faces))
    me.polygons.foreach_set("loop_start", (np.arange(len(faces)) * 4).astype(np.int32))
    me.polygons.foreach_set("loop_total", np.full(len(faces), 4, np.int32))
    me.update()
    me.validate()
    uv = me.uv_layers.new(name="UV")
    li = faces.ravel()
    uvs = np.stack([X.ravel()[li] / 384.0, Z.ravel()[li] / 384.0], -1)
    uv.data.foreach_set("uv", uvs.astype(np.float32).ravel())
    me.polygons.foreach_set("use_smooth", np.ones(len(faces), bool))
    ob = bpy.data.objects.new("T", me)
    bpy.context.scene.collection.objects.link(ob)
    mat = bpy.data.materials.new("G")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.92 if body != "Europa" else 0.45
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = bpy.data.images.load(tex)
    nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.35
    bump.inputs["Distance"].default_value = 0.02
    nt.links.new(img.outputs["Color"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    ob.data.materials.append(mat)

    sc = bpy.context.scene
    w = bpy.data.worlds.new("W")
    sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (*SKY[body], 1)
    bg.inputs[1].default_value = AMB[body]
    pitch, yaw = SUN[body]
    sd = bpy.data.lights.new("Sun", "SUN")
    sd.energy = 4.0 if body not in ("Europa",) else 3.0
    sd.angle = math.radians(0.6 if body in ("Luna", "Belt", "Europa") else 2.5)
    s = bpy.data.objects.new("Sun", sd)
    sc.collection.objects.link(s)
    # Unity: pitch below horizon, yaw about up. Blender sun points along -Z of its rotation.
    s.rotation_euler = (math.radians(90 - pitch), 0, math.radians(-yaw + 180))
    sc.render.engine = "CYCLES"
    sc.cycles.samples = int(os.environ.get("SAMPLES", "16"))
    sc.cycles.use_denoising = True
    sc.view_settings.view_transform = "AgX"
    if body == "Mars":
        sc.world.mist_settings.start = 40
    return sc


def camera(sc, target, ortho, yaw=45, pitch=30):
    cd = bpy.data.cameras.new("C")
    cd.type = "ORTHO"
    cd.ortho_scale = ortho
    cd.clip_end = 2000
    cam = bpy.data.objects.new("C", cd)
    sc.collection.objects.link(cam)
    fx, fy = math.sin(math.radians(yaw)), math.cos(math.radians(yaw))
    fz = -math.tan(math.radians(pitch))
    f = Vector((fx, fy, fz)).normalized()
    cam.location = Vector(target) - f * 400
    cam.rotation_euler = f.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    return cam


def main():
    names = sys.argv[1].split(",")
    outdir = sys.argv[2]
    seed = int(sys.argv[3]) if len(sys.argv) > 3 else 7
    views = os.environ.get("VIEWS", "close,wide").split(",")
    os.makedirs(outdir, exist_ok=True)
    for body in names:
        X, Z, H, m, tex = build(body, seed, outdir)
        sc = make_scene(body, X, Z, H, m, tex)
        sc.render.resolution_x = int(os.environ.get("RES", "960"))
        sc.render.resolution_y = sc.render.resolution_x * 9 // 16
        cx, cz = bodies.CAMPUS
        for v in views:
            if v == "close":
                camera(sc, (cx + 2, cz + 4, 0), 38)
            elif v == "mid":
                camera(sc, (cx + 20, cz + 30, 0), 110)
            else:
                camera(sc, (192, 192, 0), 330, pitch=38)
            sc.render.filepath = os.path.join(outdir, f"{body}_{v}.png")
            bpy.ops.render.render(write_still=True)
            print("[R]", body, v)


main()
