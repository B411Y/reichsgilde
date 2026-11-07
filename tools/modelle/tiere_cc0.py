"""Schwein und Ziege der Restflächen aus freien CC0-Modellen (statt der Eigenbauten in fuellung_p3.py).

Quellen (Lizenz CC0 1.0, an der Seite geprüft, Herkunft in game/assets/modelle/props/HERKUNFT.md):
  * Schwein: „Pig“ von Quaternius (https://poly.pizza/m/TNvG3QUFlp, Public Domain CC0), glTF mit Skelett, 562 Dreiecke.
  * Ziege: „Goat“ von Copabrangorandel auf OpenGameArt (https://opengameart.org/node/142490, CC0), ein unbewegliches OBJ
    ohne Textur (in ZBrush modelliert, Netz mit rund 13.000 Vierecken, hier stark vereinfacht).
Die Dateien liegen nicht im Repository: Ordner in der Umgebungsvariablen RG_KITS (Standard ~/werkzeuge/assets/entpackt),
  Quaternius_Pig_CC0/pig_TNvG3QUFlp.glb  und  OGA_Goat_CC0/goatlowrez.obj.

Das Modul liefert Objekte (keine Netz-Sammler): schwein(name, wuehlen) und ziege(name, fressen). Maßstab Meter, Ursprung Mitte am
Boden, Blick nach -Y, Z oben (Blender), Stoffe aus bs.Stoffe nach Namen („Schwein“, „Ziege“ …). Die Haltung „Wühlen“ und
„Fressen“ ist ein Neigen von Kopf und Hals um eine Achse quer (kein Skelett im Spiel, nur Standbilder).
"""

import math
import os

import bmesh
import bpy

import bausteine as bs
from bausteine import Vector

KITS = os.environ.get("RG_KITS", os.path.expanduser("~/werkzeuge/assets/entpackt"))
SCHWEIN_DATEI = os.path.join(KITS, "Quaternius_Pig_CC0", "pig_TNvG3QUFlp.glb")
ZIEGE_DATEI = os.path.join(KITS, "OGA_Goat_CC0", "goatlowrez.obj")

bs.FARBEN.update({
    "Schwein": (0.78, 0.60, 0.55),                  # Hausschwein, rosa, im Sonnenlicht etwas gedeckt
    "Schwein hell": (0.88, 0.75, 0.69),
    "Schwein dunkel": (0.58, 0.42, 0.38),           # Flecken
    "Ziege": (0.50, 0.40, 0.29),
    "Ziege hell": (0.80, 0.74, 0.62),
    "Ziege fleck": (0.64, 0.55, 0.40),
    "Ziege dunkel": (0.23, 0.18, 0.14),
    "Horn": (0.50, 0.45, 0.36),
    "Huf": (0.11, 0.095, 0.085),
})
LAENGE_SCHWEIN = 1.2       # Meter von der Nase bis zum Hintern
FAKTOR_ZIEGE = 0.29        # Maßstab der Quelle (Ziegenhöhe 3,7 → 1,07 m, Länge ohne Schwanzrest rund 1,1 m)


def _weiche(x):
    """0 bis 1 weich (Smoothstep)."""
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def _neigen(bm, punkt, winkel_grad, gewicht):
    """Dreht jede Ecke um die X-Achse durch `punkt` um winkel·gewicht(Ecke). Positiv senkt eine Nase, die nach -Y zeigt."""
    for v in bm.verts:
        w = gewicht(v)
        if w <= 0.0:
            continue
        a = math.radians(winkel_grad) * w
        d = v.co - punkt
        v.co = punkt + Vector((d.x, d.y * math.cos(a) - d.z * math.sin(a), d.y * math.sin(a) + d.z * math.cos(a)))


def _vereinfachen(bm, ziel_dreiecke):
    """Ecken zusammenlegen und auf rund `ziel_dreiecke` Dreiecke vereinfachen (Decimate); gibt ein neues bmesh zurück."""
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    me = bpy.data.meshes.new("_tmp")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("_tmp", me)
    bpy.context.scene.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    for ob in bpy.context.scene.objects:
        ob.select_set(ob is o)
    me.calc_loop_triangles()
    if ziel_dreiecke and len(me.loop_triangles) > ziel_dreiecke:
        m = o.modifiers.new("D", "DECIMATE")
        m.ratio = ziel_dreiecke / len(me.loop_triangles)
        bpy.ops.object.modifier_apply(modifier=m.name)
    bm2 = bmesh.new()
    bm2.from_mesh(o.data)
    bpy.data.objects.remove(o)
    bpy.data.meshes.remove(me)
    return bm2


def _objekt(name, bm, klassen):
    """bmesh ins Objekt: Stoff je Fläche aus `klassen[Fläche]`, Ecken zusammengelegt, glatt schattiert."""
    st = bs.Stoffe()
    namen = []
    for fl in bm.faces:
        n = klassen[fl.index]
        if n not in namen:
            namen.append(n)
        fl.material_index = namen.index(n)
        fl.smooth = True
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)      # sonst wirkt die glatte Schattierung nicht (die Quelle teilt Ecken nicht)
    for ebene in list(bm.verts.layers.deform):                    # Skelettgewichte brauchen wir nicht mehr
        bm.verts.layers.deform.remove(ebene)
    bm.verts.index_update()
    gesehen, doppelt = set(), []
    for fl in bm.faces:                                          # Flächen mit denselben Ecken (innen liegende Doppel) wegnehmen
        schluessel = frozenset(v.index for v in fl.verts)
        if schluessel in gesehen:
            doppelt.append(fl)
        gesehen.add(schluessel)
    bmesh.ops.delete(bm, geom=doppelt, context="FACES_ONLY")
    bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.validate(verbose=True)
    for n in namen:
        me.materials.append(st[n])
    for p in me.polygons:
        p.use_smooth = True
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def _laden_schwein():
    """Liest das Schwein im Ruhezustand des Skeletts als bmesh in Metern (Quelle: 100-fach skaliertes Skelett) und räumt die Importe weg."""
    vorher = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=SCHWEIN_DATEI)
    neue = [o for o in bpy.data.objects if o not in vorher]
    quelle = next(o for o in neue if o.type == "MESH" and len(o.data.polygons) > 100)
    for o in neue:
        if o.type == "ARMATURE":
            o.data.pose_position = "REST"
            if o.animation_data:
                o.animation_data.action = None
    bpy.context.view_layer.update()
    e = quelle.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = e.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.transform(e.matrix_world)
    gruppen = {g.index: g.name for g in quelle.vertex_groups}
    kopf, hals = {}, {}
    for i, v in enumerate(quelle.data.vertices):
        for g in v.groups:
            if gruppen[g.group] == "Head":
                kopf[i] = g.weight
            elif gruppen[g.group] == "Neck":
                hals[i] = g.weight
    mats = [m.name for m in quelle.data.materials]
    huf_index = 1 if len(mats) > 1 else 0         # die kleine Gruppe (40 Dreiecke) sind die Hufe; Quelle: Material 0 Körper, 1 Hufe
    e.to_mesh_clear()
    for o in neue:
        bpy.data.objects.remove(o)
    return bm, kopf, hals, huf_index


def schwein(name, wuehlen=False):
    """Hausschwein (Quaternius, CC0), rosa mit hellem Bauch, Hufe dunkel; Länge 1,2 m, Ursprung Mitte am Boden, Nase nach -Y."""
    bm, kopf, hals, huf_index = _laden_schwein()
    ys = [v.co.y for v in bm.verts]
    y0, y1 = min(ys), max(ys)
    z0 = min(v.co.z for v in bm.verts)
    f = LAENGE_SCHWEIN / (y1 - y0)
    ymitte = (y0 + y1) / 2
    hoehe = (max(v.co.z for v in bm.verts) - z0) * f
    klassen = {}
    for fl in bm.faces:
        c = fl.calc_center_median()
        if fl.material_index == huf_index:
            klassen[fl.index] = "Huf"
        elif fl.normal.z < -0.3 or (c.z - z0) * f < 0.2 * hoehe:
            klassen[fl.index] = "Schwein hell"
        else:
            klassen[fl.index] = "Schwein"
    for v in bm.verts:
        v.co = Vector((v.co.x * f, (v.co.y - ymitte) * f, (v.co.z - z0) * f))
    if wuehlen:
        drehpunkt = Vector((0.0, (-3.2 - ymitte) * f, (3.3 - z0) * f))
        _neigen(bm, drehpunkt, 40.0, lambda v: min(1.0, kopf.get(v.index, 0.0) + 0.6 * hals.get(v.index, 0.0)))
    return _objekt(name, bm, klassen)


def _laden_ziege():
    vorher = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=ZIEGE_DATEI)
    quelle = next(o for o in bpy.data.objects if o not in vorher)
    bm = bmesh.new()
    bm.from_mesh(quelle.data)
    bm.transform(quelle.matrix_world)
    bpy.data.objects.remove(quelle)
    return bm


def ziege(name, fressen=False):
    """Ziege (Copabrangorandel, CC0): Schwanz gekürzt, Fuß in z = 0, Länge rund 1,1 m, Kopf nach -Y; fressend mit gesenktem Kopf."""
    bm = _laden_ziege()
    # Schwanz: der lange Stab hinter dem Rumpf (y > 1,35) wird auf ein Viertel gestaucht
    for v in bm.verts:
        if v.co.y > 1.35:
            v.co.y = 1.35 + (v.co.y - 1.35) * 0.25
    bm = _vereinfachen(bm, 2000)
    # Stoffe je Fläche vor dem Neigen (Höhe zr und Lage yr in Einheiten der Quelle; Kopf bei yr −2, Schwanz bei +1,5)
    klassen = {}
    for fl in bm.faces:
        c = fl.calc_center_median()
        zr, yr = c.z, c.y
        if zr > 3.3 and yr > -1.25:
            n = "Horn"
        elif zr < 0.28:
            n = "Huf"
        elif zr < 1.15:
            n = "Ziege dunkel"            # Läufe unten dunkel wie bei der Hausziege
        elif (fl.normal.z < -0.5 and zr < 2.0) or (yr < -1.95 and zr < 2.9):
            n = "Ziege hell"              # Bauch und Maul
        else:
            n = "Ziege"
        klassen[fl.index] = n
    if fressen:
        drehpunkt = Vector((0.0, -0.55, 2.35))
        _neigen(bm, drehpunkt, 62.0, lambda v: _weiche((-v.co.y - 0.85) / 0.5) * _weiche((v.co.z - 2.15) / 0.5))
    z0 = min(v.co.z for v in bm.verts)
    ys = [v.co.y for v in bm.verts]
    ymitte = (min(ys) + max(ys)) / 2
    f = FAKTOR_ZIEGE
    for v in bm.verts:
        v.co = Vector((v.co.x * f, (v.co.y - ymitte) * f, (v.co.z - z0) * f))
    return _objekt(name, bm, klassen)
