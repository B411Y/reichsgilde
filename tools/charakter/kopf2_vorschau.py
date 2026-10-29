"""Charakter Phase 2: schnelle Vorschaubilder aus Blender (Workbench, ohne Godot), um Netz, Naht, Kragen, Wimpern und
Formen beim Bauen zu prüfen. Eigener Code, MIT.

vorschau(objekte, ordner, formen=[{"jawOpen": 1.0}, …], ansichten=[(winkel, abstand_m, ziel_z_versatz), …])
"""
import math
import os

import bpy


def vorschau(objekte, ordner, name, formen=({},), ansichten=((0, 0.75, 0.0),), groesse=(420, 520), ziel=(0, 0, 0)):
    os.makedirs(ordner, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.show_cavity = False
    sc.render.resolution_x, sc.render.resolution_y = groesse
    sc.render.film_transparent = False
    sc.world = sc.world or bpy.data.worlds.new("Welt")
    kamera = bpy.data.objects.get("Vorschaukamera")
    if kamera is None:
        cam = bpy.data.cameras.new("Vorschaukamera")
        cam.lens = 85
        cam.clip_start = 0.01
        kamera = bpy.data.objects.new("Vorschaukamera", cam)
        sc.collection.objects.link(kamera)
    sc.camera = kamera
    for o in sc.objects:
        if o.type == "MESH":
            o.hide_render = o not in objekte
    dateien = []
    for fi, werte in enumerate(formen):
        for o in objekte:
            if o.data.shape_keys:
                for kb in o.data.shape_keys.key_blocks[1:]:
                    kb.value = float(werte.get(kb.name, 0.0))
        for ai, (winkel, abstand, dz) in enumerate(ansichten):
            w = math.radians(winkel)
            kamera.location = (ziel[0] + abstand * math.sin(w), ziel[1] - abstand * math.cos(w), ziel[2] + dz)
            kamera.rotation_euler = (math.pi / 2, 0, w)
            pfad = os.path.join(ordner, f"{name}_{fi:02d}_{ai}.png")
            sc.render.filepath = pfad
            bpy.ops.render.render(write_still=True)
            dateien.append(pfad)
    for o in objekte:
        if o.data.shape_keys:
            for kb in o.data.shape_keys.key_blocks[1:]:
                kb.value = 0.0
    return dateien
