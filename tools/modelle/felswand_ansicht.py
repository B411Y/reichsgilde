"""Ansicht des Felswand-Satzes (Felsen R7): jedes gewählte Stück von vier Seiten (die Spielkamera dreht in 90°-Schritten),
schräg von oben wie im Spiel, mit den gebackenen Atlanten und einer Gesteinsfarbe – zum Prüfen geschlossener Rückseiten und
der Farbe einer Gesteinsart, ohne Godot.

    blender -b --python tools/modelle/felswand_ansicht.py -- [--glb=<datei>] [--texturen=<ordner>] [--struktur=<png>]
            [--nur=wand_gross_1,pfeilerbuendel] [--gestein=trachyt] [--farben=<hell>,<dunkel>[,<einsprenglinge>]]
            [--name=<vorsilbe>] [--bilder=<ordner>] [--seite]

Vorgaben: Modell und Atlanten aus dem Spiel (game/assets/...), Gestein aus game/daten/gestein.json, Bilder nach docs/modelle.
--farben überschreibt die Farben der Gesteinsart (sRGB-Hex ohne #), z. B. zum Vergleich alt gegen neu; die dritte Farbe malt
Feldspat-Einsprenglinge (Trachyt). --seite: statt der vier Seiten ein Blick von vorn und einer genau von der Seite (Fallen
der Schichtung, Keil oder Wand). Je Stück ein Bild <vorsilbe>_<form>.png mit den Ansichten nebeneinander.
Nur Vorschau: Der Stoff ähnelt fels.gdshaderinc (Farbe hell/dunkel nach Struktur und Rauschen, Verdeckung und Rinnen aus dem
Atlas, Moos oben), ist aber nicht derselbe.
"""

import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402


def argumente():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    wert = lambda n, d: next((x.split("=", 1)[1] for x in a if x.startswith(f"--{n}=")), d)  # noqa: E731
    nur = wert("nur", "wand_gross_1,pfeilerbuendel,wand_mittel_1")
    return dict(glb=wert("glb", os.path.join(bs.WURZEL, "game", "assets", "modelle", "natur", "natur_felswand.glb")),
                texturen=wert("texturen", os.path.join(bs.WURZEL, "game", "assets", "texturen", "felswand")),
                struktur=wert("struktur", os.path.join(bs.WURZEL, "game", "assets", "texturen", "fels", "fels_struktur.png")),
                nur=nur.split(","), gestein=wert("gestein", "grauwacke"), farben=wert("farben", None),
                name=wert("name", "r7"), bilder=wert("bilder", bs.VORSCHAU), seite="--seite" in a)


def linear(hexfarbe):
    h = hexfarbe.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def gesteinsfarben(kennung, farben):
    with open(os.path.join(bs.WURZEL, "game", "daten", "gestein.json"), encoding="utf-8") as f:
        daten = json.load(f)
    g = next((x for x in daten["gesteine"] if x["kennung"] == kennung), daten["gesteine"][0])
    hell, dunkel = g["farbe_hell"], g["farbe_dunkel"]
    korn = g.get("einsprenglinge_farbe") if g.get("einsprenglinge", 0) > 0 else None
    if farben:
        teile = farben.split(",")
        hell, dunkel = teile[0], teile[1]
        korn = teile[2] if len(teile) > 2 else None
    return linear(hell), linear(dunkel), (linear(korn) if korn else None)


def stoff(texturen, struktur, hell, dunkel, korn):
    m = bpy.data.materials.new("_Ansicht")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    nt = m.node_tree
    L = nt.links.new
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.9
    nimg = nt.nodes.new("ShaderNodeTexImage")
    nimg.image = bpy.data.images.load(os.path.join(texturen, "felswand_normal.webp"))
    nimg.image.colorspace_settings.name = "Non-Color"
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    L(nimg.outputs["Color"], nmap.inputs["Color"])
    L(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    himg = nt.nodes.new("ShaderNodeTexImage")
    himg.image = bpy.data.images.load(os.path.join(texturen, "felswand_hoehlung.webp"))
    himg.image.colorspace_settings.name = "Non-Color"
    trenn = nt.nodes.new("ShaderNodeSeparateColor")
    L(himg.outputs["Color"], trenn.inputs[0])
    # Struktur (Höhe im Kanal B) dreiseitig über die Objektlage, 2,5 m je Kachel
    koord = nt.nodes.new("ShaderNodeTexCoord")
    mass = nt.nodes.new("ShaderNodeVectorMath")
    mass.operation = "SCALE"
    mass.inputs["Scale"].default_value = 1 / 2.5
    L(koord.outputs["Object"], mass.inputs[0])
    simg = nt.nodes.new("ShaderNodeTexImage")
    simg.image = bpy.data.images.load(struktur)
    simg.image.colorspace_settings.name = "Non-Color"
    simg.projection = "BOX"
    simg.projection_blend = 0.3
    L(mass.outputs[0], simg.inputs["Vector"])
    strenn = nt.nodes.new("ShaderNodeSeparateColor")
    L(simg.outputs["Color"], strenn.inputs[0])
    rausch = nt.nodes.new("ShaderNodeTexNoise")
    rausch.inputs["Scale"].default_value = 0.35
    rausch.inputs["Detail"].default_value = 3
    L(koord.outputs["Object"], rausch.inputs["Vector"])
    t = nt.nodes.new("ShaderNodeMath")
    t.operation = "MULTIPLY_ADD"
    L(strenn.outputs[2], t.inputs[0])
    t.inputs[1].default_value = 0.55
    L(rausch.outputs["Fac"], t.inputs[2])
    rampe = nt.nodes.new("ShaderNodeMapRange")
    rampe.inputs["From Min"].default_value, rampe.inputs["From Max"].default_value = 0.55, 1.25
    L(t.outputs[0], rampe.inputs["Value"])
    farbe = nt.nodes.new("ShaderNodeMix")
    farbe.data_type = "RGBA"
    L(rampe.outputs["Result"], farbe.inputs["Factor"])
    farbe.inputs[6].default_value = dunkel
    farbe.inputs[7].default_value = hell
    letzte = farbe.outputs[2]
    if korn:                         # Feldspat-Einsprenglinge: kleine, helle Tafeln
        vor = nt.nodes.new("ShaderNodeTexVoronoi")
        vor.feature = "F1"
        vor.inputs["Scale"].default_value = 9.0
        L(koord.outputs["Object"], vor.inputs["Vector"])
        schwelle = nt.nodes.new("ShaderNodeMapRange")
        schwelle.inputs["From Min"].default_value, schwelle.inputs["From Max"].default_value = 0.16, 0.12
        L(vor.outputs["Distance"], schwelle.inputs["Value"])
        km = nt.nodes.new("ShaderNodeMix")
        km.data_type = "RGBA"
        L(schwelle.outputs["Result"], km.inputs["Factor"])
        L(letzte, km.inputs[6])
        km.inputs[7].default_value = korn
        letzte = km.outputs[2]
    rinne = nt.nodes.new("ShaderNodeMix")
    rinne.data_type = "RGBA"
    L(trenn.outputs[2], rinne.inputs["Factor"])
    L(letzte, rinne.inputs[6])
    rinne.inputs[7].default_value = (0.02, 0.02, 0.018, 1)
    ao = nt.nodes.new("ShaderNodeMix")
    ao.data_type = "RGBA"
    ao.blend_type = "MULTIPLY"
    ao.inputs["Factor"].default_value = 1.0
    L(rinne.outputs[2], ao.inputs[6])
    L(trenn.outputs[0], ao.inputs[7])
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    L(geo.outputs["Normal"], sep.inputs[0])
    moos_f = nt.nodes.new("ShaderNodeMapRange")
    moos_f.inputs["From Min"].default_value, moos_f.inputs["From Max"].default_value = 0.8, 0.97
    L(sep.outputs[2], moos_f.inputs["Value"])
    moos = nt.nodes.new("ShaderNodeMix")
    moos.data_type = "RGBA"
    L(moos_f.outputs["Result"], moos.inputs["Factor"])
    L(ao.outputs[2], moos.inputs[6])
    moos.inputs[7].default_value = (0.045, 0.07, 0.02, 1)
    L(moos.outputs[2], bsdf.inputs["Base Color"])
    return m


def kamera_setzen(kam, ziel, abstand, neigung, drehung):
    n, d = math.radians(neigung), math.radians(drehung)
    richtung = Vector((math.sin(d) * math.cos(n), math.cos(d) * math.cos(n), -math.sin(n)))   # 0 = von vorn (−Y)
    kam.location = Vector(ziel) - richtung * abstand
    kam.rotation_euler = richtung.to_track_quat("-Z", "Y").to_euler()


def zusammensetzen(pfade, aus):
    teile = []
    for p in pfade:
        img = bpy.data.images.load(p)
        px = np.empty(img.size[0] * img.size[1] * 4, np.float32)
        img.pixels.foreach_get(px)
        teile.append(px.reshape(img.size[1], img.size[0], 4))
    ganz = np.concatenate(teile, axis=1)
    ganz[:, :, 3] = 1
    img = bpy.data.images.new("_ganz", ganz.shape[1], ganz.shape[0], alpha=False)
    img.pixels.foreach_set(ganz.ravel())
    img.filepath_raw = aus
    img.file_format = "PNG"
    img.save()
    for p in pfade:
        os.remove(p)
    print("Bild:", aus, flush=True)


def main():
    arg = argumente()
    szene = bs.neue_szene()
    bpy.ops.import_scene.gltf(filepath=arg["glb"])
    szene.render.engine = "CYCLES"
    print("Cycles:", bs.cycles_geraet(szene))
    szene.cycles.samples = 32
    szene.cycles.use_denoising = True
    try:
        szene.view_settings.view_transform = "AgX"
    except TypeError:
        szene.view_settings.view_transform = "Standard"
    sonne = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
    sonne.data.energy = 3.2
    sonne.data.color = (1.0, 0.95, 0.86)
    sonne.rotation_euler = (math.radians(48), 0, math.radians(-35))
    szene.collection.objects.link(sonne)
    welt = bpy.data.worlds.new("Himmel")
    try:
        welt.use_nodes = True
    except AttributeError:
        pass
    welt.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.47, 0.55, 1)
    welt.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    szene.world = welt
    hell, dunkel, korn = gesteinsfarben(arg["gestein"], arg["farben"])
    st = stoff(arg["texturen"], arg["struktur"], hell, dunkel, korn)
    gras = bs._hilfsstoff("_Gras", (0.30, 0.33, 0.20))
    bpy.ops.mesh.primitive_plane_add(size=400)
    boden = bpy.context.active_object
    boden.data.materials.append(gras)
    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    szene.collection.objects.link(kam)
    szene.camera = kam
    kam.data.lens = 40
    szene.render.resolution_x, szene.render.resolution_y = 900, 700
    os.makedirs(arg["bilder"], exist_ok=True)
    knoten = {o.name: o for o in szene.objects if o.type == "MESH" and o is not boden}
    for form in arg["nur"]:
        teil = "".join(w.capitalize() for w in form.split("_"))
        teil = {"Pfeiler1": "Pfeiler1"}.get(teil, teil)
        obj = knoten.get(f"{teil}_0")
        if obj is None:
            print("fehlt:", form, sorted(knoten)[:5])
            continue
        for o in knoten.values():
            o.hide_render = o is not obj
        obj.location = (0, 0, 0)
        obj.data.materials.clear()
        obj.data.materials.append(st)
        for p in obj.data.polygons:
            p.use_smooth = True
        d = obj.dimensions
        groesse = max(d.x, d.y, d.z)
        ziel = (0, 0, d.z * 0.4)
        # glTF: Schauseite +Z im Spiel → in Blender −Y, Rückseite +Y
        ansichten = [(0, 40), (90, 40), (180, 40), (270, 40)] if not arg["seite"] else [(0, 20), (90, 5), (270, 5)]
        pfade = []
        for i, (dreh, neig) in enumerate(ansichten):
            kamera_setzen(kam, ziel, groesse * 1.9, neig, dreh)
            sonne.rotation_euler = (math.radians(48), 0, math.radians(-35 + dreh))     # Licht dreht mit, wie im Spiel tagsüber
            szene.render.filepath = os.path.join(arg["bilder"], f"_{arg['name']}_{form}_{i}.png")
            bpy.ops.render.render(write_still=True)
            pfade.append(szene.render.filepath)
        zusammensetzen(pfade, os.path.join(arg["bilder"], f"{arg['name']}_{form}.png"))
        tri = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        print(f"{form}: {d.x:.2f} × {d.y:.2f} × {d.z:.2f} m, Stufe 0 {tri} Dreiecke", flush=True)


if __name__ == "__main__":
    main()
