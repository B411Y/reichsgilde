# Schnelle Blender-Ansicht von .tfx-Strähnen am HG-Kopf (ohne Godot): vorn, schräg, Seite.
# Aufruf: blender_hg.sh -b <mensch_a.blend> -P tfx_ansicht.py -- <bild_präfix> <a.tfx> [b.tfx …]
import bpy, sys, struct, math
import numpy as np
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
ziel, dateien = a[0], a[1:]
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
sc.render.resolution_x = sc.render.resolution_y = 512
for o in bpy.data.objects:
    if o.get("hg_haircard"):
        o.hide_render = True
for m in bpy.data.objects["HG_Body"].modifiers:
    if m.type == "PARTICLE_SYSTEM":
        m.show_render = False
mat = bpy.data.materials.new("str"); mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.03, 0.02, 1)
for d in dateien:
    raw = open(d, "rb").read()
    _, s, n = struct.unpack("<fII", raw[:12])
    p = np.frombuffer(raw[160:160 + s * n * 16], "<f4").reshape(s, n, 4)[..., :3]
    p = np.stack([p[..., 0], -p[..., 2], p[..., 1]], -1)   # Y-oben -> Z-oben
    cu = bpy.data.curves.new("h", "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = 0.0002
    for st in p[::2]:
        sp = cu.splines.new("POLY"); sp.points.add(n - 1)
        sp.points.foreach_set("co", np.c_[st, np.ones(n)].reshape(-1))
    o = bpy.data.objects.new("h", cu); o.data.materials.append(mat); sc.collection.objects.link(o)
rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
kopf = rig.matrix_world @ rig.data.bones["head"].head_local + Vector((0, 0, 0.06))
cam = bpy.data.objects.get("Camera") or bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
if cam.name not in sc.collection.objects:
    sc.collection.objects.link(cam)
sc.camera = cam; cam.data.lens = 70
for i, w in enumerate([0, 35, 90]):
    r = math.radians(w)
    cam.location = kopf + Vector((math.sin(r) * 0.9, -math.cos(r) * 0.9, 0.02))
    cam.rotation_euler = (kopf - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = f"{ziel}_{i}.png"
    bpy.ops.render.render(write_still=True)
