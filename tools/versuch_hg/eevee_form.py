# Schnelle Eevee-Ansicht einer Form (HG-Livekey .npz) am HG-Körper, ohne Godot: vorn, schräg, Seite, Ohr nah.
# Aufruf: blender_hg.sh -b <mensch_a.blend> -P eevee_form.py -- <form.npz> <bild_präfix> [wert=1]
import bpy, sys, math
import numpy as np
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
form, ziel = a[0], a[1]
wert = float(a[2]) if len(a) > 2 else 1.0
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
sc.render.resolution_x = sc.render.resolution_y = 512
body = bpy.data.objects["HG_Body"]
for o in bpy.data.objects:
    if o.get("hg_haircard"):
        o.hide_render = True
for m in body.modifiers:
    if m.type == "PARTICLE_SYSTEM":
        m.show_render = False
n = len(body.data.vertices)
basis = np.empty(n * 3); body.data.shape_keys.key_blocks["Basis"].data.foreach_get("co", basis)
d = np.load(form); delta = np.zeros(n * 3); delta[d["indices"]] = d["relative_coordinates"]
k = body.shape_key_add(name="probe", from_mix=False); k.data.foreach_set("co", basis + delta); k.value = wert
rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
kopf = rig.matrix_world @ rig.data.bones["head"].head_local + Vector((0, 0, 0.08))
cam = bpy.data.objects.get("Camera") or bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
if cam.name not in sc.collection.objects:
    sc.collection.objects.link(cam)
sc.camera = cam
for i, (w, ab, ziel_off, lens) in enumerate([(0, 0.9, (0, 0, 0), 85), (35, 0.9, (0, 0, 0), 85), (90, 0.9, (0, 0, 0), 85),
                                              (75, 0.32, (0.075, 0.01, -0.01), 85), (-20, 0.5, (0.07, 0.0, -0.0), 85)]):
    r = math.radians(w)
    zielp = kopf + Vector(ziel_off)
    cam.location = zielp + Vector((math.sin(r) * ab, -math.cos(r) * ab, 0.02))
    cam.rotation_euler = (zielp - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    sc.render.filepath = f"{ziel}_{i}.png"
    bpy.ops.render.render(write_still=True)
