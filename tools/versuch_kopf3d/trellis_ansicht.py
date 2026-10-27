# TRELLIS-GLB ansehen: Maße ausgeben und Bilder von vorn/seitlich/schräg (Workbench, Textur flach).
# blender -b --python trellis_ansicht.py -- --glb=<datei> --aus=<ordnerpräfix>
import sys, math, bpy
from mathutils import Vector
a = dict(x[2:].split("=", 1) for x in sys.argv[sys.argv.index("--") + 1:] if x.startswith("--"))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a["glb"])
ms = [o for o in bpy.data.objects if o.type == "MESH"]
for o in ms:
    print("MESH", o.name, len(o.data.vertices), "Punkte", len(o.data.polygons), "Flaechen", [m.name for m in o.data.materials], tuple(round(x, 3) for x in o.dimensions), tuple(round(x, 3) for x in o.location))
mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for o in ms:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
print("BOX", mn, mx)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "FLAT"; sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x = sc.render.resolution_y = 800
sc.render.film_transparent = False
mid = (mn + mx) / 2; gr = (mx - mn).length
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = gr * 0.9
for name, az in (("vorn", 0), ("seite", 90), ("schraeg", 35)):
    r = gr * 2
    cam.location = mid + Vector((math.sin(math.radians(az)) * r, -math.cos(math.radians(az)) * r, 0))
    cam.rotation_euler = (math.radians(90), 0, math.radians(az))
    sc.render.filepath = f'{a["aus"]}_{name}.png'
    bpy.ops.render.render(write_still=True)
