"""Vorschaubilder für fuellung_p2.py: alle Teile nebeneinander (oben Sommer, unten Winter), Licht von oben links,
Namen als Textobjekte (kein Pillow nötig). Dazu ein Bild im Pixel-Look (niedrig aufgelöst, hart vergrößert).

Schreibt docs/modelle/fuellung_<name>.png; mit der Umgebungsvariable FUELLUNG_KOPIE (Ordner) zusätzlich eine Kopie
und fuellung_<name>_pixel.png dorthin.
"""

import math
import os
import shutil

import bpy

import bausteine as bs
from bausteine import Vector

SCHRIFT = os.path.join(bs.WURZEL, "game", "assets", "oberflaeche", "schriften", "Alegreya-Variabel.ttf")


def _breite(o):
    return (o.dimensions.x + o.dimensions.y) * 0.62 + 0.8


def _reihe(objekte, tiefe):
    """Ordnet eine Reihe quer zur Blickrichtung an (Mitte bei 0) und gibt ihre Gesamtbreite zurück."""
    breiten = [_breite(o) for o in objekte]
    gesamt = sum(breiten)
    x = -gesamt / 2
    for o, b in zip(objekte, breiten):
        m = x + b / 2
        x += b
        o.location = Vector((m * 0.7071 - tiefe * 0.7071, m * 0.7071 + tiefe * 0.7071, 0))
    return gesamt


def _beschriften(objekte, kam):
    schrift = bpy.data.fonts.load(SCHRIFT) if os.path.exists(SCHRIFT) else None
    mat = bpy.data.materials.new("_Beschriftung")
    try:
        mat.use_nodes = True
    except AttributeError:
        pass
    p = mat.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (0, 0, 0, 1)
    p.inputs["Emission Color"].default_value = (0.95, 0.92, 0.82, 1)
    p.inputs["Emission Strength"].default_value = 1.6
    bpy.context.view_layer.update()
    hoch = kam.matrix_world.to_3x3() @ Vector((0, 1, 0))
    for o in objekte:
        kurve = bpy.data.curves.new("_t_" + o.name, "FONT")
        kurve.body = o.name.replace("_Winter", " (Winter)")
        kurve.size = 0.26
        kurve.align_x = "CENTER"
        if schrift:
            kurve.font = schrift
        t = bpy.data.objects.new("_t_" + o.name, kurve)
        t.data.materials.append(mat)
        t.rotation_euler = kam.rotation_euler
        t.location = o.location - hoch * 0.5 + (kam.matrix_world.to_3x3() @ Vector((0, 0, 1))) * 15
        t.visible_shadow = False
        bpy.context.scene.collection.objects.link(t)


def _sonne_links():
    az = math.radians(-45)
    licht = Vector((math.cos(az - math.radians(55)), math.sin(az - math.radians(55)), 1.15)).normalized()
    bpy.data.objects["Sonne"].rotation_euler = (-licht).to_track_quat("-Z", "Y").to_euler()


def _rendern(pfad, sommer_und_winter):
    for o in bpy.context.scene.objects:
        if o.type == "MESH":
            o.hide_render = not (o.name.startswith("_") or o in sommer_und_winter)
    bpy.context.scene.render.filepath = pfad
    bpy.ops.render.render(write_still=True)


def vorschau(name, sommer, winter):
    import sys
    schnell = "--schnell" in sys.argv
    szene = bpy.context.scene
    alle = sommer + winter
    hmax = max(o.dimensions.z for o in alle)
    gmax = max(max(o.dimensions.x, o.dimensions.y) for o in alle)
    tiefe = 0.45 * gmax + 0.9 + 0.4 * hmax
    b1 = _reihe(sommer, tiefe)
    b2 = _reihe(winter, -tiefe) if winter else 0.0
    breit = max(max(b1, b2) * 1.05 + 1.2, (2 * tiefe * 0.643 + 0.77 * hmax + 0.7 * gmax + 1.0) * 16 / 9)
    bs.vorschau_buehne(None, ziel_hoehe=0.25 * hmax + 0.3, ausschnitt=breit, schnell=schnell)
    boden = bpy.data.materials.get("_Boden")
    if boden:
        boden.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*bs._linear((0.40, 0.42, 0.31)), 1)
    szene.cycles.samples = 24 if schnell else 64
    szene.render.resolution_x, szene.render.resolution_y = 1600, 900
    szene.render.resolution_percentage = 50 if schnell else 100
    bs.blick(0)
    _sonne_links()
    _beschriften(alle, szene.camera)
    os.makedirs(bs.VORSCHAU, exist_ok=True)
    pfad = os.path.join(bs.VORSCHAU, name + ".png")
    _rendern(pfad, alle)
    print("Bild:", pfad)
    kopie = os.environ.get("FUELLUNG_KOPIE")
    if not kopie:
        return
    os.makedirs(kopie, exist_ok=True)
    shutil.copy(pfad, os.path.join(kopie, name + ".png"))
    # Pixel-Look: niedrig aufgelöst rendern, dann ohne Glättung vierfach vergrößern
    szene.render.resolution_percentage = 100
    szene.render.resolution_x, szene.render.resolution_y = 400, 225
    szene.cycles.samples = 32
    klein = os.path.join(kopie, "_px_klein.png")
    for o in bpy.context.scene.objects:
        if o.type == "FONT":
            o.hide_render = True
    _rendern(klein, alle)
    import numpy as np
    import OpenImageIO as oiio
    src = oiio.ImageBuf(klein)
    px = src.get_pixels(oiio.FLOAT)
    gross_px = np.repeat(np.repeat(px, 4, axis=0), 4, axis=1)
    h, w, c = gross_px.shape
    gross = oiio.ImageBuf(oiio.ImageSpec(w, h, c, oiio.FLOAT))
    gross.set_pixels(oiio.ROI(0, w, 0, h, 0, 1, 0, c), gross_px)
    gross.write(os.path.join(kopie, name + "_pixel.png"))
    os.remove(klein)
