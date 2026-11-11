"""Standbilder des Tuerschlosses (Ruhe, gedrueckt) aus tuerschloss.blend.
blender -b <ausordner>/tuerschloss.blend --python tuerschloss_bilder.py -- <bilderordner>"""
import bpy, sys, os, math
from mathutils import Vector
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HIER, "..", "handschuh"))
import v6_rig_lib as L
aus = sys.argv[sys.argv.index("--") + 1]; os.makedirs(aus, exist_ok=True)
cam = L.buehne(384)
kl = bpy.data.objects["Klinke"]
ANS = {"vorn": ((0, -0.001, 1), 0.30), "schraeg": ((-0.55, -0.45, 0.7), 0.30), "seite": ((-1, 0.0, 0.12), 0.30), "oben": ((0.15, 1, 0.35), 0.30)}
for nm, wi in (("ruhe", 0), ("gedrueckt", -28)):
    kl.animation_data.action = None; kl.rotation_euler = (0, 0, math.radians(wi))
    for an, (d, o) in ANS.items():
        v = Vector(d).normalized(); cam.data.ortho_scale = o; cam.location = Vector((0, 0.0, 0.02)) + v
        cam.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler()
        bpy.context.scene.render.filepath = os.path.join(aus, f"schloss_{nm}_{an}.png"); bpy.ops.render.render(write_still=True)
# grosse Vorderansicht fuer den Vergleich mit der 2D-Vorlage (Klinke in Ruhe)
kl.rotation_euler = (0, 0, 0); sc = bpy.context.scene
sc.render.resolution_x = sc.render.resolution_y = 640
v = Vector((0, -0.001, 1)).normalized(); cam.data.ortho_scale = 0.215; cam.location = Vector((0.036, 0.0, 0.02)) + v
cam.rotation_euler = (-v).to_track_quat("-Z", "Y").to_euler()
sc.render.filepath = os.path.join(aus, "schloss_vorn_gross.png"); bpy.ops.render.render(write_still=True)
