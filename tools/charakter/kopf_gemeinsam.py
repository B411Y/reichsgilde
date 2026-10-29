"""Gemeinsame Bausteine der Charakter-Versuche (läuft in Blender, eigener Code, MIT).

- Hautmasken als Eckfarben (R = Rötung, G = Glanz/T-Zone, B = Vertiefung), damit der Hautshader in Godot ohne
  eigene Texturen Rötung, Rauheit und Höhlen kennt
- Zuschnitt zur Büste über bmesh (Formschlüssel bleiben erhalten)
- Übertragung von Formschlüsseln vom Körper auf angehängte Teile (Augen, Zähne, Hauer) über feste
  Dreieckszuordnungen (je Ecke des Teils drei Körperecken mit Gewichten)
- glTF-Export mit Formschlüsseln
"""
import math

import bmesh
import bpy
import numpy as np
from mathutils import Vector


def argumente():
    import sys
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def alter_zu_makro(jahre):
    """MakeHuman-Altersregler (0..1) aus Lebensjahren: 1 J. = 0, 11 J. = 0,1875, 25 J. = 0,5, 90 J. = 1."""
    j = float(jahre)
    if j <= 11:
        return max(0.0, (j - 1.0) / 10.0 * 0.1875)
    if j <= 25:
        return 0.1875 + (j - 11.0) / 14.0 * 0.3125
    return min(1.0, 0.5 + (j - 25.0) / 65.0 * 0.5)


def ecken_als_feld(obj, schluessel=None):
    """Eckpositionen (n, 3) des Netzes oder eines Formschlüssels."""
    n = len(obj.data.vertices)
    feld = np.empty(n * 3, dtype=np.float32)
    if schluessel is None:
        obj.data.vertices.foreach_get("co", feld)
    else:
        obj.data.shape_keys.key_blocks[schluessel].data.foreach_get("co", feld)
    return feld.reshape(n, 3)


def schluessel_setzen(obj, name, feld):
    if obj.data.shape_keys is None:
        obj.shape_key_add(name="Basis", from_mix=False)
    kb = obj.data.shape_keys.key_blocks.get(name) or obj.shape_key_add(name=name, from_mix=False)
    kb.data.foreach_set("co", np.ascontiguousarray(feld, dtype=np.float32).ravel())
    kb.value = 0.0
    return kb


def formen_uebertragen(koerper, teil, zuordnung, namen, mindest=1e-5):
    """Formschlüssel `namen` vom Körper auf ein Teil übertragen.

    zuordnung: (idx (m,3) int, gew (m,3) float) – für jede Ecke des Teils drei Körperecken und ihre Gewichte.
    Es wird nur der Versatz übertragen (Σ w_i · Δ_i); Maßstab und Form des Teils selbst bleiben (Augapfel bleibt rund).
    Gibt die Namen der wirklich angelegten Schlüssel zurück.
    """
    idx, gew = zuordnung
    basis = ecken_als_feld(koerper, "Basis")
    teil_basis = ecken_als_feld(teil)
    angelegt = []
    for name in namen:
        if name not in koerper.data.shape_keys.key_blocks:
            continue
        delta = ecken_als_feld(koerper, name) - basis
        d = (delta[idx] * gew[..., None]).sum(axis=1)
        if np.abs(d).max() < mindest:
            continue
        schluessel_setzen(teil, name, teil_basis + d)
        angelegt.append(name)
    return angelegt


def naechste_zuordnung(koerper_ecken, teil_ecken, k=3):
    """Zuordnung über die k nächsten Körperecken (Gewicht ~ 1/Abstand) – für Teile ohne mhclo (Hauer, ICT)."""
    from mathutils.kdtree import KDTree
    baum = KDTree(len(koerper_ecken))
    for i, p in enumerate(koerper_ecken):
        baum.insert(Vector(p), i)
    baum.balance()
    idx = np.zeros((len(teil_ecken), k), dtype=np.int64)
    gew = np.zeros((len(teil_ecken), k), dtype=np.float32)
    for j, p in enumerate(teil_ecken):
        treffer = baum.find_n(Vector(p), k)
        w = np.array([1.0 / max(t[2], 1e-4) for t in treffer])
        idx[j] = [t[1] for t in treffer]
        gew[j] = w / w.sum()
    return idx, gew


def zuschneiden(obj, behalten):
    """Ecken löschen, für die behalten(index, position) False ist. Formschlüssel, UV und Gruppen bleiben."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    weg = [v for v in bm.verts if not behalten(v.index, v.co)]
    bmesh.ops.delete(bm, geom=weg, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return len(weg)


def gauss(abstand_qu, radius):
    return np.exp(-abstand_qu / (2.0 * radius * radius))


def masken_setzen(obj, punkte, kopfmass, ohren=None, lippen=None, hoehlen=True):
    """Eckfarbe „Hautmaske“ (R Rötung, G Glanz, B Vertiefung) aus Merkpunkten des Gesichts.

    punkte: dict mit Weltpunkten (nase, mund, auge_l, auge_r, stirn, kinn); kopfmass: Augenabstand in Metern
    ohren/lippen: optionale Gewichte je Ecke (0..1), z. B. aus Ecken-Gruppen
    """
    p = ecken_als_feld(obj)
    def qa(punkt):
        return ((p - np.array(punkt)) ** 2).sum(axis=1)
    s = kopfmass
    al, ar = np.array(punkte["auge_l"]), np.array(punkte["auge_r"])
    nase, mund = np.array(punkte["nase"]), np.array(punkte["mund"])
    # Wangen: unter den Augen, etwas nach außen und vorn
    unten = (mund - (al + ar) / 2.0)
    wange_l = al + unten * 0.45 + (al - ar) * 0.18
    wange_r = ar + unten * 0.45 + (ar - al) * 0.18
    rot = (0.85 * gauss(qa(wange_l), s * 0.42) + 0.85 * gauss(qa(wange_r), s * 0.42)
           + 0.9 * gauss(qa(nase), s * 0.28) + 0.25 * gauss(qa(mund), s * 0.35))
    if ohren is not None:
        rot = rot + 0.75 * ohren
    if lippen is not None:
        rot = rot + 0.25 * lippen
    # T-Zone glänzt: Stirn, Nasenrücken, Nasenspitze, Kinn; Lippen feucht
    glanz = (0.8 * gauss(qa(punkte["stirn"]), s * 0.55) + 0.9 * gauss(qa(nase), s * 0.3)
             + 0.5 * gauss(qa((nase + (al + ar) / 2.0) / 2.0), s * 0.22) + 0.45 * gauss(qa(punkte["kinn"]), s * 0.3))
    if lippen is not None:
        glanz = glanz + 0.8 * lippen
    hoehle = np.zeros(len(p))
    attr = obj.data.color_attributes.get("Hautmaske")
    if attr is None:
        attr = obj.data.color_attributes.new("Hautmaske", "BYTE_COLOR", "POINT")
    obj.data.color_attributes.active_color = attr
    if hoehlen:
        # Vertiefungen über Blenders „Dirty Vertex Colors“ (schreibt in die aktive Eckfarbe)
        bpy.context.view_layer.objects.active = obj
        for o in bpy.context.selected_objects:
            o.select_set(False)
        obj.select_set(True)
        bpy.ops.object.mode_set(mode="VERTEX_PAINT")
        bpy.ops.paint.vertex_color_dirt(blur_strength=1.0, blur_iterations=2, clean_angle=math.pi, dirt_angle=0.0,
                                        dirt_only=True, normalize=True)
        bpy.ops.object.mode_set(mode="OBJECT")
        werte = np.empty(len(p) * 4, dtype=np.float32)
        attr.data.foreach_get("color", werte)
        dreck = werte.reshape(-1, 4)[:, 0]
        # dirt_only: 1 = flach/außen, kleiner = Furche. Linear (Farbe wird als sRGB gespeichert)
        hoehle = np.clip((0.5 - dreck) * 2.5, 0.0, 1.0)
    farbe = np.stack([np.clip(rot, 0, 1), np.clip(glanz, 0, 1), hoehle, np.ones(len(p))], axis=1).astype(np.float32)
    attr.data.foreach_set("color", farbe.ravel())
    return attr


def gltf_export(objekte, pfad, morph_normalen=True, eckfarben=True, tangenten=True):
    """Ausgewählte Objekte als .glb mit Formschlüsseln (ohne Animation, ohne Modifikatoren)."""
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objekte:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objekte[0]
    optionen = dict(filepath=pfad, export_format="GLB", use_selection=True, export_apply=False,
                    export_morph=True, export_morph_normal=morph_normalen, export_morph_tangent=False,
                    export_tangents=tangenten, export_animations=False, export_skins=False,
                    export_image_format="AUTO", export_yup=True)
    if eckfarben:
        optionen["export_vertex_color"] = "ACTIVE"
        optionen["export_active_vertex_color_when_no_material"] = True
    else:
        optionen["export_vertex_color"] = "NONE"
    try:
        bpy.ops.export_scene.gltf(**optionen)
    except TypeError:
        # ältere Exporter kennen die Eckfarb-Optionen anders
        optionen.pop("export_vertex_color", None)
        optionen.pop("export_active_vertex_color_when_no_material", None)
        bpy.ops.export_scene.gltf(**optionen)
