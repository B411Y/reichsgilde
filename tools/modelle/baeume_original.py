"""Die Originalbäume des Quaternius-Pakets „Stylized Nature MegaKit [Standard]“ (CC0 1.0) für die Stadtansicht.

    flock /tmp/claude-1000/blender.lock ~/werkzeuge/blender/blender.sh -b --python tools/modelle/baeume_original.py

Anders als `kit_import.py` (stark vereinfachte Bäume mit grauen Blattmasken) bleiben hier die Modelle des Pakets, wie sie sind:
volle Dreiecke, Original-Rinde mit Normalenkarte, Original-Blattkarten in Originalfarben. Die Jahreszeiten färbt der Shader
(`baum_wind.gdshader`), nicht das Modell. Zu jedem Baum entstehen drei Fassungen:
  <Name>          nah: das Originalmodell (Maßstab wie bisher, Fuß im Nullpunkt, Mittelpunkt auf x/z = 0)
  <Name>_Fern     fern: Rinde vereinfacht, vom Laub nur ein Teil der Blattkarten, dafür größer (Umriss und Dichte bleiben)
  <Name>_Winter   kahl: nur die Rinde des Originals (nur Laubbäume; Tannen und Büsche haben keine)
  <Name>_FernWinter  kahl und fern
Schreibt `game/assets/modelle/natur/natur_original_baeume.glb` (Texturen WebP, Rinde 1024, Blätter 512).
"""

import json
import math
import os
import random
import struct
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402

KITS = os.environ.get("RG_KITS", os.path.expanduser("~/werkzeuge/assets/entpackt"))
ORDNER = os.path.join(KITS, "Stylized_Nature_MegaKitStandard", "glTF")

# (Knoten, Datei, Höhe in m, Art) – Art: laub (hat Winterfassung), nadel, busch (Winterfassung aus der Rinde, falls vorhanden)
BAEUME = [
    ("Eiche1", "CommonTree_5", 8.0, "laub"),
    ("Eiche2", "CommonTree_3", 9.5, "laub"),
    ("Eiche3", "CommonTree_2", 8.5, "laub"),
    ("Eiche4", "CommonTree_1", 9.0, "laub"),
    ("Eiche5", "CommonTree_4", 6.0, "laub"),
    # Obstbäume: die knorrigen, breiten TwistedTree des Kits (Apfel und Kirsche unterscheidet das Spiel nur durch Tönung und Blüte)
    ("Obstbaum1", "TwistedTree_2", 5.0, "laub"),
    ("Obstbaum2", "TwistedTree_4", 5.6, "laub"),
    ("Tanne1", "Pine_5", 7.5, "nadel"),
    ("Tanne2", "Pine_2", 8.0, "nadel"),
    ("Tanne3", "Pine_4", 10.5, "nadel"),
    ("Tanne4", "Pine_1", 7.5, "nadel"),
    ("Tanne5", "Pine_3", 7.5, "nadel"),
    ("Busch1", "Bush_Common", 1.6, "busch"),
    ("Busch2", "Bush_Common_Flowers", 1.6, "busch"),
]
LAUBSTOFFE = ("Leaves_NormalTree", "Leaves_TwistedTree", "Leaves_Pine", "Flowers")
GROESSE = {"Bark_NormalTree": 1024, "Bark_TwistedTree": 1024, "Leaves_NormalTree": 512, "Leaves_TwistedTree": 512, "Leaves_Pine": 512, "Flowers": 512}
FERN_RINDE = 0.30        # Anteil der Rindendreiecke in der Fernfassung
FERN_LAUB = 0.40         # Anteil der Blattkarten in der Fernfassung
FERN_VERGROESSERN = 1.30  # verbleibende Karten wachsen um ihren Schwerpunkt


def aktiv(o):
    for x in bpy.context.selected_objects:
        x.select_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def basisname(m):
    return m.name.split(".")[0] if m else None


def laden(datei, hoehe, name):
    pfad = os.path.join(ORDNER, datei + ".gltf")
    vorher = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=pfad)
    neue = list(set(bpy.data.objects) - vorher)
    netze = [o for o in neue if o.type == "MESH"]
    for o in set(neue) - set(netze):
        bpy.data.objects.remove(o)
    for o in netze:
        o.parent = None
    aktiv(netze[0])
    for o in netze:
        o.select_set(True)
    if len(netze) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    # gleiche Materialien teilen (Rinde und Blätter sind in allen Bäumen dieselben)
    for i, m in enumerate(o.data.materials):
        n = basisname(m)
        if m is not None and m.name != n and n in bpy.data.materials:
            o.data.materials[i] = bpy.data.materials[n]
    for m in [m for m in bpy.data.materials if m.users == 0]:
        bpy.data.materials.remove(m)
    z = [v.co.z for v in o.data.vertices]
    f = hoehe / max(1e-6, max(z) - min(z))
    o.scale = (f, f, f)
    aktiv(o)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    tief = min(v.co.z for v in o.data.vertices)
    bx = (max(v.co.x for v in o.data.vertices) + min(v.co.x for v in o.data.vertices)) / 2
    by = (max(v.co.y for v in o.data.vertices) + min(v.co.y for v in o.data.vertices)) / 2
    for v in o.data.vertices:
        v.co.z -= tief
        v.co.x -= bx
        v.co.y -= by
    return o


def ist_laub(m):
    return m is not None and basisname(m) in LAUBSTOFFE


def teil(o, behalten_laub, name):
    """Kopie mit nur den Laubflächen (behalten_laub=True) bzw. nur den übrigen (False)."""
    k = o.copy()
    k.data = o.data.copy()
    k.name = k.data.name = name
    bpy.context.scene.collection.objects.link(k)
    bm = bmesh.new()
    bm.from_mesh(k.data)
    weg = [f for f in bm.faces if ist_laub(k.data.materials[f.material_index]) != behalten_laub]
    bmesh.ops.delete(bm, geom=weg, context="FACES")
    bm.to_mesh(k.data)
    bm.free()
    return k


def verbinden(objekte, name):
    objekte = [o for o in objekte if o is not None and len(o.data.polygons) > 0]
    aktiv(objekte[0])
    for o in objekte:
        o.select_set(True)
    if len(objekte) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    return o


def rinde_duenn(o, anteil):
    n = len(o.data.polygons)
    aktiv(o)
    mod = o.modifiers.new("Weniger", "DECIMATE")
    mod.ratio = max(0.05, anteil)
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return n


def laub_duenn(o, anteil, saat):
    """Behält einen Teil der Blattkarten (zusammenhängende Flächen) und vergrößert sie um ihren Schwerpunkt."""
    rnd = random.Random(saat)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table()
    gesehen, inseln = set(), []
    for f in bm.faces:
        if f.index in gesehen:
            continue
        stapel, insel = [f], []
        gesehen.add(f.index)
        while stapel:
            g = stapel.pop()
            insel.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in gesehen:
                        gesehen.add(h.index)
                        stapel.append(h)
        inseln.append(insel)
    weg = []
    for insel in inseln:
        if rnd.random() < anteil:
            vs = {v for g in insel for v in g.verts}
            c = sum((v.co for v in vs), type(next(iter(vs)).co)((0, 0, 0))) / len(vs)
            for v in vs:
                v.co = c + (v.co - c) * FERN_VERGROESSERN
        else:
            weg.extend(insel)
    bmesh.ops.delete(bm, geom=weg, context="FACES")
    bm.to_mesh(o.data)
    bm.free()


def bilder_verkleinern():
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        for k in m.node_tree.nodes:
            if k.type == "TEX_IMAGE" and k.image:
                g = GROESSE.get(basisname(m), 512)
                w, h = k.image.size
                f = min(1.0, g / max(w, h))
                if f < 1.0 and w > 0:
                    k.image.scale(max(1, int(w * f)), max(1, int(h * f)))


def nachbessern(pfad):
    """Blattkarten als Alpha-Maske (Schwelle 0,45) und doppelseitig, wie bei den übrigen Kit-Bäumen."""
    with open(pfad, "rb") as f:
        daten = f.read()
    laenge = struct.unpack_from("<I", daten, 12)[0]
    j = json.loads(daten[20:20 + laenge])
    rest = daten[20 + laenge:]
    for m in j.get("materials", []):
        m["doubleSided"] = True
        if m["name"].split(".")[0] in LAUBSTOFFE:
            m["alphaMode"] = "MASK"
            m["alphaCutoff"] = 0.45
        else:
            m.pop("alphaMode", None)
    neu = json.dumps(j, separators=(",", ":")).encode("utf-8")
    neu += b" " * (-len(neu) % 4)
    with open(pfad, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(neu) + len(rest)))
        f.write(struct.pack("<II", len(neu), 0x4E4F534A))
        f.write(neu)
        f.write(rest)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ergebnis = []
    for i, (name, datei, hoehe, art) in enumerate(BAEUME):
        o = laden(datei, hoehe, name)
        n_voll = len(o.data.polygons)
        rinde = teil(o, False, name + "_r")
        laub = teil(o, True, name + "_l")
        # Fernfassung
        fern_rinde = rinde.copy()
        fern_rinde.data = rinde.data.copy()
        bpy.context.scene.collection.objects.link(fern_rinde)
        rinde_duenn(fern_rinde, FERN_RINDE)
        fern_laub = laub.copy()
        fern_laub.data = laub.data.copy()
        bpy.context.scene.collection.objects.link(fern_laub)
        laub_duenn(fern_laub, FERN_LAUB, 100 + i)
        fern = verbinden([fern_rinde, fern_laub], name + "_Fern")
        winter, fern_winter = None, None
        if art == "laub":
            w = rinde.copy()
            w.data = rinde.data.copy()
            bpy.context.scene.collection.objects.link(w)
            w.name = w.data.name = name + "_Winter"
            winter = w
            fw = rinde.copy()
            fw.data = rinde.data.copy()
            bpy.context.scene.collection.objects.link(fw)
            rinde_duenn(fw, FERN_RINDE)
            fw.name = fw.data.name = name + "_FernWinter"
            fern_winter = fw
        bpy.data.objects.remove(o)
        nah = verbinden([rinde, laub], name)
        ergebnis += [nah, fern] + [x for x in (winter, fern_winter) if x]
        print(f"{name}: voll {n_voll} Dreiecke; nah {len(nah.data.polygons)}, fern {len(fern.data.polygons)}"
              + (f", winter {len(winter.data.polygons)}" if winter else ""))
    # übriggebliebene Hilfsobjekte entfernen
    for o in list(bpy.data.objects):
        if o not in ergebnis:
            bpy.data.objects.remove(o)
    bilder_verkleinern()
    # nebeneinander legen (nur zur Ansicht im Editor); der Fuß bleibt im Nullpunkt jedes Knotens, darum nur ein Raster der Namen
    for o in ergebnis:
        aktiv(o)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    pfad = bs.modellpfad("natur_original_baeume")
    for o in bpy.context.scene.objects:
        o.select_set(o in ergebnis)
    bpy.ops.export_scene.gltf(
        filepath=pfad, export_format="GLB", use_selection=True, export_yup=True, export_apply=True, export_extras=True,
        export_normals=True, export_texcoords=True, export_animations=False, export_skins=False, export_morph=False,
        export_lights=False, export_cameras=False, export_image_format="WEBP", export_image_quality=85)
    nachbessern(pfad)
    print("geschrieben:", pfad, round(os.path.getsize(pfad) / 1024), "KB")


main()
