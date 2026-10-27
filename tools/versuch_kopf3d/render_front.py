# Frontansicht (orthografisch, flach, Textur) einer TRELLIS-GLB: Eingabe für die Bild-KI ("Augen schließen") und
# exakt passende Rückprojektion. blender -b --python render_front.py -- --glb=<datei> --aus=<png> [--mitte=x,z --groesse=1.0]
import sys, bpy, math
from mathutils import Vector
a = dict(x[2:].split("=", 1) for x in sys.argv[sys.argv.index("--") + 1:] if x.startswith("--"))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a["glb"])
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "FLAT"; sc.display.shading.color_type = "TEXTURE"
sc.view_settings.view_transform = "Standard"
sc.display.render_aa = "8"
sc.render.resolution_x = sc.render.resolution_y = 1024
sc.world = bpy.data.worlds.new("w"); sc.world.color = (1, 1, 1)
mx, mz = [float(q) for q in a.get("mitte", "0,0").split(",")]
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = float(a.get("groesse", "1.0"))
cam.location = (mx, -3, mz); cam.rotation_euler = (math.radians(90), 0, 0)
sc.render.filepath = a["aus"]
bpy.ops.render.render(write_still=True)
print("RENDER", a["aus"], "mitte", mx, mz, "ortho", cam.data.ortho_scale)
