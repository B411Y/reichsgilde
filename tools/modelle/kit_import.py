"""Übernimmt ausgewählte Teile der Quaternius-Pakete (CC0 1.0) als Baukästen ins Spiel.

    blender -b --python tools/modelle/kit_import.py -- [natur|props|dorf|alle] [--schnell] [--ohne-bilder]
    python tools/modelle/kit_import.py natur         (mit dem pip-Modul bpy)

Die Pakete liegen nicht im Repository (sie sind groß); das Skript liest sie aus dem Ordner in der Umgebungsvariablen
RG_KITS (Standard: ~/werkzeuge/assets/entpackt), jeweils entpackt wie von der Seite geladen:
    Stylized_Nature_MegaKitStandard/glTF/, Fantasy_Props_MegaKitStandard/Exports/glTF/,
    Medieval_Village_MegaKitStandard/Medieval Village MegaKit[Standard]/glTF/
Herkunft und Lizenz stehen in game/assets/modelle/props/HERKUNFT.md und game/assets/modelle/natur/HERKUNFT.md.

Was das Skript tut (statt Handarbeit):
  * Teile laden, mehrteilige zu einem Netz verbinden, auf unser Maß bringen (Meter, Fuß im Nullpunkt, Vorderseite +Z),
  * zu große Netze vereinfachen (Dreiecksziel je Teil; Bäume nie: Collapse zerreißt Blattkarten und dünne Äste),
  * Texturen auf eine sinnvolle Größe verkleinern (WebP) und die Materialien neu aufbauen: Farbe, Normale, Rauheit/Metall,
  * Laub und Gras mit unseren Jahreszeitenfarben koppeln: Die Blattkarte wird zu einer grauen Maske mit Alpha, die Farbe
    kommt aus „Laub“, „Laub hell“ oder „Laub dunkel“ (das Spiel färbt diese Namen je Jahreszeit um),
  * zu jedem Laubbaum eine kahle Fassung „<Name>_Winter“ (nur die Rinde) anlegen,
  * als Baukasten schreiben: natur/natur_kit_boden.glb, natur/natur_kit_steine.glb, natur/natur_kit_baeume.glb,
    props/props_gegenstaende.glb (Fässer, Kisten, Möbel, Werkzeug, Markt) und props/props_dorf.glb (Zierteile des Dorfpakets).
Jeder Knoten steht mit dem Fuß im Nullpunkt; der Katalog (game/daten/katalog.json) nennt Datei, Knoten und Variantenzahl.
"""

import json
import math
import os
import struct
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Vector  # noqa: E402

try:
    import numpy as np
except ImportError:                                   # Blender bringt numpy mit; ohne gibt es keine Laubmaske
    np = None

KITS = os.environ.get("RG_KITS", os.path.expanduser("~/werkzeuge/assets/entpackt"))
ORDNER = {
    "natur": os.path.join(KITS, "Stylized_Nature_MegaKitStandard", "glTF"),
    "props": os.path.join(KITS, "Fantasy_Props_MegaKitStandard", "Exports", "glTF"),
    "dorf": os.path.join(KITS, "Medieval_Village_MegaKitStandard", "Medieval Village MegaKit[Standard]", "glTF"),
}

# ─────────────────────────────────────────────────────────────────────────────
#  Materialregeln: Kit-Material → Auflösung der Texturen (Pixel), Laubfarbe, Metall
# ─────────────────────────────────────────────────────────────────────────────

# Laub: Blattkarte wird grau (Helligkeit bleibt), die Farbe kommt aus dem Material mit unserem Namen. `sommer` ist sRGB wie in
# bausteine.FARBEN; das Spiel liest sie als Sommerfarbe. Die Maske ist im Mittel 0,8 hell, darum die Farbe durch 0,8.
LAUB_HELLIGKEIT = 0.8
STOFFREGELN = {
    # Natur
    "Leaves_NormalTree": dict(name="Laub", farbe=(0.24, 0.35, 0.16), groesse=512, laub=True),
    "Leaves_TwistedTree": dict(name="Laub dunkel", farbe=(0.18, 0.28, 0.13), groesse=512, laub=True),
    "Leaves": dict(name="Laub hell", farbe=(0.34, 0.44, 0.20), groesse=512, laub=True),
    "Grass": dict(name="Laub hell", farbe=(0.34, 0.44, 0.20), groesse=256, laub=True),
    "Leaves_Pine": dict(name="Kiefernnadeln", farbe=(0.20, 0.30, 0.17), groesse=512, alpha=True, tint=True),
    "Bark_NormalTree": dict(name="Baumrinde", farbe=(0.34, 0.26, 0.19), groesse=1024, normal=512, tint=True),
    "Bark_DeadTree": dict(name="Totholzrinde", farbe=(0.45, 0.40, 0.33), groesse=1024, normal=512, tint=True),
    "Bark_TwistedTree": dict(name="Baumrinde", farbe=(0.34, 0.26, 0.19), groesse=1024, normal=512, tint=True),
    "Flowers": dict(name="Bluetenkarte", groesse=512, alpha=True),
    "Mushrooms": dict(name="Pilze Kit", groesse=512),
    "PathRocks": dict(name="Pfadsteine", groesse=512),
    "Rocks": dict(name="Felsen Kit", groesse=512),
    # Fantasy Props
    "MI_Trim_Furniture": dict(name="Moebel", groesse=1024, normal=512, orm=512),
    "MI_Trim_Metal": dict(name="Beschlag", groesse=1024, normal=512, orm=512),
    "MI_Trim_Metal_Vertex": dict(name="Beschlag", groesse=1024, normal=512, orm=512),
    "MI_Trim_Props": dict(name="Gegenstand", groesse=1024, normal=512, orm=512),
    "MI_Trim_Props_Vertex": dict(name="Gegenstand", groesse=1024, normal=512, orm=512),
    "MI_Trim_Cloth": dict(name="Tuch Kit", groesse=1024, normal=512, orm=512),
    "MI_Banner": dict(name="Tuch Kit", groesse=1024, normal=512, orm=512),
    "MI_Page_Empty": dict(name="Pergament Kit", groesse=256),
    # Medieval Village (nur Zierteile)
    "MI_WoodTrim": dict(name="Zierholz", groesse=1024, normal=512, orm=512),
    "MI_WoodTrim_Wear": dict(name="Zierholz", groesse=1024, normal=512, orm=512),
    "MI_Brick": dict(name="Zierziegel", groesse=512, normal=512, orm=512),
    "MI_RockTrim": dict(name="Zierstein", groesse=1024, normal=512, orm=512),
    "MI_UnevenBrick": dict(name="Zierbruchstein", groesse=512, normal=512, orm=512),
    "MI_MetalOrnaments": dict(name="Zierbeschlag", groesse=512, orm=512, metall=0.6),
}

_stoffe = {}          # Kit-Materialname → Blender-Material
_nachbessern = {}     # Name im GLB → Änderungen am Material-JSON (Alpha, Farbe, doppelseitig)


def gltf_stoffe(pfad):
    """Die Materialien einer .gltf-Datei: Name → (Farbbild, Normalenbild, ORM-Bild, alphaMode, Farbfaktor)."""
    with open(pfad, encoding="utf-8") as f:
        j = json.load(f)
    bilder, texturen = j.get("images", []), j.get("textures", [])

    def uri(t):
        return bilder[texturen[t["index"]]["source"]]["uri"] if t else None

    aus = {}
    for m in j.get("materials", []):
        p = m.get("pbrMetallicRoughness", {})
        aus[m["name"]] = dict(farbe=uri(p.get("baseColorTexture")), normal=uri(m.get("normalTexture")),
                              orm=uri(p.get("metallicRoughnessTexture")), alpha=m.get("alphaMode"),
                              faktor=p.get("baseColorFactor"), rauheit=p.get("roughnessFactor"))
    return aus


def bild_holen(ordner, datei, groesse, farbraum="sRGB"):
    """Lädt ein Bild und verkleinert es auf höchstens `groesse` Pixel Kantenlänge."""
    b = bpy.data.images.load(os.path.join(ordner, datei), check_existing=False)
    b.alpha_mode = "STRAIGHT"
    b.colorspace_settings.name = farbraum
    w, h = b.size
    f = min(1.0, groesse / max(w, h))
    if f < 1.0:
        b.scale(max(1, int(w * f)), max(1, int(h * f)))
    return b


def laubmaske(bild):
    """Macht aus der farbigen Blattkarte eine graue Maske (mittlere Helligkeit LAUB_HELLIGKEIT) mit dem Alpha der Karte."""
    if np is None:
        return
    d = np.empty(len(bild.pixels), dtype=np.float32)
    bild.pixels.foreach_get(d)
    d = d.reshape(-1, 4)
    voll = d[:, 3] > 0.5
    hell = d[:, 0] * 0.2126 + d[:, 1] * 0.7152 + d[:, 2] * 0.0722
    mittel = float(hell[voll].mean()) if voll.any() else 0.5
    grau = np.clip(hell / max(mittel, 1e-4) * LAUB_HELLIGKEIT, 0.0, 1.0)
    d[:, 0] = d[:, 1] = d[:, 2] = grau
    bild.pixels.foreach_set(d.ravel())
    bild.update()


def stoff_holen(kit, name, daten):
    """Das Spielmaterial zu einem Kit-Material (einmal gebaut, danach geteilt)."""
    if name in _stoffe:
        return _stoffe[name]
    regel = STOFFREGELN.get(name)
    if regel is None:
        raise KeyError(f"Keine Materialregel für {name}")
    ordner = ORDNER[kit]
    neu = regel["name"]
    if neu in bpy.data.materials:                      # zwei Kit-Materialien, ein Spielmaterial (z. B. Zierholz)
        _stoffe[name] = bpy.data.materials[neu]
        return _stoffe[name]
    m = bpy.data.materials.new(neu)
    m.use_nodes = True
    k = m.node_tree.nodes
    p = k["Principled BSDF"]
    links = m.node_tree.links
    m.use_backface_culling = False
    farbe = daten.get("farbe")
    if farbe:
        b = bild_holen(ordner, farbe, regel["groesse"])
        b.name = neu + "_farbe"
        tint = regel.get("laub") or regel.get("tint")
        if tint:
            laubmaske(b)
        t = k.new("ShaderNodeTexImage")
        t.image = b
        if tint:                                   # graue Maske mal Farbe (das Spiel liest die Farbe als Grundfarbe des Materials)
            mix = k.new("ShaderNodeMix")
            mix.data_type, mix.blend_type = "RGBA", "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            mix.inputs[7].default_value = [srgb_linear(min(1.0, c / LAUB_HELLIGKEIT)) for c in regel["farbe"]] + [1.0]
            links.new(t.outputs["Color"], mix.inputs[6])
            links.new(mix.outputs[2], p.inputs["Base Color"])
        else:
            links.new(t.outputs["Color"], p.inputs["Base Color"])
        if regel.get("laub") or regel.get("alpha"):
            links.new(t.outputs["Alpha"], p.inputs["Alpha"])
    else:
        f = daten.get("faktor") or [0.5, 0.5, 0.5, 1]
        p.inputs["Base Color"].default_value = f
    if daten.get("normal") and regel.get("normal"):
        n = bild_holen(ordner, daten["normal"], regel["normal"], "Non-Color")
        n.name = neu + "_normal"
        t = k.new("ShaderNodeTexImage")
        t.image = n
        nm = k.new("ShaderNodeNormalMap")
        links.new(t.outputs["Color"], nm.inputs["Color"])
        links.new(nm.outputs["Normal"], p.inputs["Normal"])
    if daten.get("orm") and regel.get("orm"):
        o = bild_holen(ordner, daten["orm"], regel["orm"], "Non-Color")
        o.name = neu + "_orm"
        t = k.new("ShaderNodeTexImage")
        t.image = o
        s = k.new("ShaderNodeSeparateColor")
        links.new(t.outputs["Color"], s.inputs["Color"])
        links.new(s.outputs["Green"], p.inputs["Roughness"])
        if "ORM" in daten["orm"] and "metall" not in regel:
            links.new(s.outputs["Blue"], p.inputs["Metallic"])
        else:
            p.inputs["Metallic"].default_value = regel.get("metall", 0.0)
    else:
        p.inputs["Roughness"].default_value = 0.85
    if regel.get("laub") or regel.get("tint"):
        faktor = [min(1.0, c / LAUB_HELLIGKEIT) for c in regel["farbe"]]
        _nachbessern[neu] = dict(alphaMode="MASK" if (regel.get("laub") or regel.get("alpha")) else None, alphaCutoff=0.45, doubleSided=True, farbe_srgb=faktor)
    elif regel.get("alpha"):
        _nachbessern[neu] = dict(alphaMode="MASK", alphaCutoff=0.45, doubleSided=True)
    _stoffe[name] = m
    return m


# ─────────────────────────────────────────────────────────────────────────────
#  Teile laden und zurechtstutzen
# ─────────────────────────────────────────────────────────────────────────────

def objekt_aktiv(o):
    for x in bpy.context.selected_objects:
        x.select_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def dreiecke(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def vereinfachen(o, ziel):
    """Reduziert das Netz auf etwa `ziel` Dreiecke (Collapse mit erhaltenem UV-Rand)."""
    n = dreiecke(o)
    if ziel is None or n <= ziel:
        return
    objekt_aktiv(o)
    mod = o.modifiers.new("Weniger", "DECIMATE")
    mod.ratio = max(0.02, ziel / n)
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)


def teil_nach_stoff(o, behalten):
    """Kopie des Objekts, die nur Flächen der Materialien in `behalten` (Spielnamen) behält."""
    import bmesh
    k = o.copy()
    k.data = o.data.copy()
    bpy.context.scene.collection.objects.link(k)
    bm = bmesh.new()
    bm.from_mesh(k.data)
    weg = [f for f in bm.faces if not (k.data.materials[f.material_index] and k.data.materials[f.material_index].name in behalten)]
    bmesh.ops.delete(bm, geom=weg, context="FACES")
    bm.to_mesh(k.data)
    bm.free()
    return k


def verbinden(objekte, name):
    objekt_aktiv(objekte[0])
    for o in objekte:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objekte[0]
    if len(objekte) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    return o


def laden(kit, stamm, name, hoehe=None, breite=None, faktor=1.0, ziel=None, drehung=0.0, ziel_laub=None, ziel_rest=None, winter=False,
          unter_null=0.0):
    """Lädt `stamm`.gltf aus dem Paket `kit`, baut die Materialien um und gibt das fertige Objekt `name` zurück.

    hoehe/breite: Zielmaß in Metern (sonst `faktor`). ziel: Dreiecksziel für das ganze Netz; ziel_laub/ziel_rest:
    getrennt für Laubflächen (Materialien mit Alpha) und den Rest. winter: zusätzlich Objekt „<name>_Winter“ ohne Laub."""
    pfad = os.path.join(ORDNER[kit], stamm + ".gltf")
    stoffe = gltf_stoffe(pfad)
    vorher = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=pfad)
    neue = [o for o in set(bpy.data.objects) - vorher]
    netze = [o for o in neue if o.type == "MESH"]
    for o in set(neue) - set(netze):
        bpy.data.objects.remove(o)
    for o in netze:
        o.parent = None
    o = verbinden(netze, name)
    # Materialien ersetzen (die Reihenfolge der Plätze bleibt)
    namen = [m.name.split(".")[0] if m else None for m in o.data.materials]
    for i, kn in enumerate(namen):
        if kn in stoffe:
            o.data.materials[i] = stoff_holen(kit, kn, stoffe[kn])
    for m in [m for m in bpy.data.materials if m.users == 0]:
        bpy.data.materials.remove(m)
    # Maß: Zielhöhe oder -breite, sonst Faktor
    x = [v.co.x for v in o.data.vertices]
    y = [v.co.y for v in o.data.vertices]
    z = [v.co.z for v in o.data.vertices]
    if hoehe:
        faktor = hoehe / max(1e-6, max(z) - min(z))
    elif breite:
        faktor = breite / max(1e-6, max(max(x) - min(x), max(y) - min(y)))
    o.scale = (faktor, faktor, faktor)
    o.rotation_euler = (0, 0, math.radians(drehung))
    objekt_aktiv(o)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if unter_null is not None:                       # Fuß auf den Boden (Wurzeln dürfen bis `unter_null` darunter reichen)
        tief = min(v.co.z for v in o.data.vertices)
        if tief < -unter_null or (tief > 0.02 and unter_null == 0.0):
            for v in o.data.vertices:
                v.co.z += -tief - unter_null
    # Mittelpunkt auf x/y = 0
    bx = (max(v.co.x for v in o.data.vertices) + min(v.co.x for v in o.data.vertices)) / 2
    by = (max(v.co.y for v in o.data.vertices) + min(v.co.y for v in o.data.vertices)) / 2
    if abs(bx) > 1e-4 or abs(by) > 1e-4:
        for v in o.data.vertices:
            v.co.x -= bx
            v.co.y -= by
    laub = {m.name for m in o.data.materials if m and _nachbessern.get(m.name, {}).get("alphaMode")}
    if ziel_laub is not None or ziel_rest is not None:
        a = teil_nach_stoff(o, laub) if laub else None             # nur Laub
        b = teil_nach_stoff(o, {m.name for m in o.data.materials if m and m.name not in laub})   # nur Rinde u. a.
        bpy.data.objects.remove(o)
        if a is not None:
            vereinfachen(a, ziel_laub)
        vereinfachen(b, ziel_rest)
        o = verbinden([x for x in (a, b) if x is not None], name)
    else:
        vereinfachen(o, ziel)
    o.data.update()
    return o


def winterfassung(o, name):
    """Kopie ohne Laubflächen: die Rinde allein."""
    behalten = {m.name for m in o.data.materials if m and not _nachbessern.get(m.name, {}).get("alphaMode")}
    k = teil_nach_stoff(o, behalten)
    k.name = k.data.name = name
    return k


def sauber(objekte):
    """Entfernt Farbattribute, die nur weiß sind (sonst malt das Spiel sie als Vertexfarbe), setzt Glättung."""
    for o in objekte:
        for a in list(o.data.color_attributes):
            if np is not None:
                d = np.empty(len(a.data) * 4, dtype=np.float32)
                a.data.foreach_get("color", d)
                if float(d.min()) > 0.97:
                    o.data.color_attributes.remove(a)
        objekt_aktiv(o)
        if any(_nachbessern.get(m.name, {}).get("alphaMode") for m in o.data.materials if m):
            if o.data.has_custom_normals:
                bpy.ops.mesh.customdata_custom_splitnormals_clear()
            bpy.ops.object.shade_flat()               # Blattkarten: glatte Normalen färben sie nach dem Vereinfachen schwarz
        else:
            bpy.ops.object.shade_smooth()
        o["teil"] = o.name


# ─────────────────────────────────────────────────────────────────────────────
#  Ausgabe: GLB mit Bildern, danach Material-JSON nachbessern
# ─────────────────────────────────────────────────────────────────────────────

def srgb_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def nachbessern(pfad):
    """Schreibt Alpha-Modus, Schwellwert, Doppelseitigkeit und Laubfarbe in die Materialien der GLB (der Exporter kennt Masken nur
    über Knotengruppen; so ist das Ergebnis eindeutig)."""
    with open(pfad, "rb") as f:
        daten = f.read()
    laenge_json, _ = struct.unpack_from("<II", daten, 12)
    j = json.loads(daten[20:20 + laenge_json])
    rest = daten[20 + laenge_json:]
    for m in j.get("materials", []):
        n = m.get("name", "").split(".")[0]
        a = _nachbessern.get(n)
        m["doubleSided"] = True
        if a is None:
            m.pop("alphaMode", None)
            continue
        if a["alphaMode"]:
            m["alphaMode"] = a["alphaMode"]
            m["alphaCutoff"] = a["alphaCutoff"]
        else:
            m.pop("alphaMode", None)
        if "farbe_srgb" in a:
            m.setdefault("pbrMetallicRoughness", {})["baseColorFactor"] = [srgb_linear(c) for c in a["farbe_srgb"]] + [1.0]
    neu = json.dumps(j, separators=(",", ":")).encode("utf-8")
    neu += b" " * (-len(neu) % 4)
    gesamt = 12 + 8 + len(neu) + len(rest)
    with open(pfad, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, gesamt))
        f.write(struct.pack("<II", len(neu), 0x4E4F534A))
        f.write(neu)
        f.write(rest)


def schreiben(name, objekte):
    """Exportiert die Objekte als natur/<name>.glb bzw. props/<name>.glb (WebP-Texturen)."""
    pfad = bs.modellpfad(name)
    for o in bpy.context.scene.objects:
        o.select_set(o in objekte)
    bpy.ops.export_scene.gltf(
        filepath=pfad, export_format="GLB", use_selection=True, export_yup=True, export_apply=True, export_extras=True,
        export_normals=True, export_texcoords=True, export_animations=False, export_skins=False, export_morph=False,
        export_lights=False, export_cameras=False, export_image_format="WEBP", export_image_quality=82,
        export_vertex_color="ACTIVE", export_all_vertex_colors=False)
    nachbessern(pfad)
    print("geschrieben:", pfad, round(os.path.getsize(pfad) / 1024), "KB")
    return pfad


def melden(name, objekte):
    print(f"== {name}: {len(objekte)} Knoten")
    for o in objekte:
        bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
        print(f"  {o.name:22s} {dreiecke(o):5d} Dreiecke  "
              f"{max(v.x for v in bb) - min(v.x for v in bb):5.2f} x {max(v.z for v in bb) - min(v.z for v in bb):5.2f} x "
              f"{max(v.y for v in bb) - min(v.y for v in bb):5.2f} m")


# ─────────────────────────────────────────────────────────────────────────────
#  Mittelweg für Bäume: weniger Dreiecke ohne Collapse über das ganze Netz
# ─────────────────────────────────────────────────────────────────────────────

def _lage(v):
    """Schlüssel für die Lage einer Ecke (der glTF-Import trennt Ecken an UV-Nähten, die Lage bleibt gleich)."""
    return (round(v.co.x * 2000), round(v.co.y * 2000), round(v.co.z * 2000))


def teile(bm):
    """Teilt die Flächen in zusammenhängende Stücke (über gleiche Kanten nach Lage): Äste, Nadelbüschel, Blattkarten.
    Gibt Listen von Flächen zurück, die größten zuerst."""
    eltern = {f.index: f.index for f in bm.faces}

    def wurzel(i):
        while eltern[i] != i:
            eltern[i] = eltern[eltern[i]]
            i = eltern[i]
        return i

    kanten = {}
    for f in bm.faces:
        ls = [_lage(v) for v in f.verts]
        for a, b in zip(ls, ls[1:] + ls[:1]):
            e = (min(a, b), max(a, b))
            if e in kanten:
                eltern[wurzel(f.index)] = wurzel(kanten[e])
            else:
                kanten[e] = f.index
    gruppen = {}
    for f in bm.faces:
        gruppen.setdefault(wurzel(f.index), []).append(f)
    return sorted(gruppen.values(), key=len, reverse=True)


def _masse(flaechen):
    """Mittelpunkt und größte Ausdehnung eines Stücks."""
    pts = [v.co for f in flaechen for v in f.verts]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return (lo + hi) / 2, (hi - lo).length


def rinde_schlank(o, ziel, mindest=0.45):
    """Äste und Stamm mit weniger Dreiecken: erst fallen die kleinsten Zweige weg (sie verschwinden im Laub), dann werden die
    übrigen Röhren verschweißt und vorsichtig zusammengefaltet. Die Ecken sind danach verbunden, darum reißt nichts auf."""
    import bmesh
    n = dreiecke(o)
    if n <= ziel:
        return o
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table()
    stuecke = teile(bm)
    nach_laenge = sorted(stuecke, key=lambda g: _masse(g)[1], reverse=True)
    behalten, summe = [], 0
    for g in nach_laenge:                              # lange Äste zuerst, bis das Dreiecksbudget für `mindest` reicht
        if summe >= ziel / mindest and behalten:
            break
        behalten.append(g)
        summe += sum(len(f.verts) - 2 for f in g)
    gut = {f.index for g in behalten for f in g}
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in gut], context="FACES")
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bm.to_mesh(o.data)
    bm.free()
    if o.data.has_custom_normals:
        objekt_aktiv(o)
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    vereinfachen(o, ziel)
    return o


def laub_schlank(o, ziel, saat, dunkel=0.5, streuung=0.14, farbton=0.05):
    """Blattkarten und Nadelbüschel ausdünnen, ohne das Netz zu verformen: von innen nach außen fallen einzelne Stücke weg
    (die Silhouette bleibt), die übrigen werden um ihre Mitte größer. Dazu Eckenfarben: Mitte und Unterseite dunkler (AO),
    je Stück leicht andere Helligkeit und ein Hauch Farbton (das Spiel multipliziert sie mit der Jahreszeitenfarbe)."""
    import bmesh
    import random
    zufall = random.Random(saat)
    n = dreiecke(o)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table()
    stuecke = teile(bm)
    pts = [v.co for v in bm.verts]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    mitte, halb = (lo + hi) / 2, (hi - lo) / 2
    halb = Vector((max(halb.x, 1e-3), max(halb.y, 1e-3), max(halb.z, 1e-3)))

    def tiefe(p):
        """0 an der Oberfläche der Krone, 1 in der Mitte."""
        d = Vector(((p.x - mitte.x) / halb.x, (p.y - mitte.y) / halb.y, (p.z - mitte.z) / halb.z))
        return max(0.0, 1.0 - min(1.0, d.length))

    anteil = min(1.0, ziel / max(1, n))
    # Rangfolge: außen zuerst, mit Zufall gemischt, damit innen nicht alles fehlt
    rang = sorted(stuecke, key=lambda g: tiefe(_masse(g)[0]) + zufall.random() * 0.55)
    k = max(1, round(len(stuecke) * anteil))
    bleiben = rang[:k]
    gut = {f.index for g in bleiben for f in g}
    gross = (1.0 / anteil) ** 0.5 * 0.92 if anteil < 1.0 else 1.0          # Fläche bleibt etwa gleich, etwas weniger gegen Überdeckung
    ebene = bm.loops.layers.float_color.new("Col")
    vorher = {}
    for g in bleiben:
        m, _ = _masse(g)
        j = 1.0 - streuung + 2 * streuung * zufall.random()
        h = zufall.uniform(-1, 1)
        for f in g:
            for l in f.loops:
                v = l.vert
                p = v.co.copy()
                if v.index not in vorher:
                    vorher[v.index] = m + (p - m) * gross
                t = max(0.0, min(1.0, 0.6 * (1 - tiefe(p)) + 0.4 * ((p.z - lo.z) / max(1e-3, hi.z - lo.z))))
                w = (dunkel + (1.0 - dunkel) * t ** 0.8) * j
                l[ebene] = (w * (1 + farbton * h), w, w * (1 - farbton * h), 1.0)
    bm.verts.ensure_lookup_table()
    for i, p in vorher.items():
        bm.verts[i].co = p
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in gut], context="FACES")
    bm.to_mesh(o.data)
    bm.free()
    return o


def baum_mittel(o, name, rinde, laub, saat=1, **farbe):
    """Der Kit-Baum `o` mit etwa `rinde` + `laub` Dreiecken (Rinde und Laub getrennt behandelt)."""
    import bmesh
    alpha = {m.name for m in o.data.materials if m and _nachbessern.get(m.name, {}).get("alphaMode")}
    rest = {m.name for m in o.data.materials if m and m.name not in alpha}
    teile_ = []
    if alpha:
        a = teil_nach_stoff(o, alpha)
        teile_.append(laub_schlank(a, laub, saat, **farbe))
    b = teil_nach_stoff(o, rest)
    rinde_schlank(b, rinde)
    weiss = b.data.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    weiss.data.foreach_set("color", [1.0] * (4 * len(weiss.data)))
    teile_.append(b)
    bpy.data.objects.remove(o)
    return verbinden(teile_, name)


# ─────────────────────────────────────────────────────────────────────────────
#  Die Baukästen
# ─────────────────────────────────────────────────────────────────────────────

def natur_boden():
    bs.neue_szene()
    o = []
    for i, s in enumerate(("Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Short", "Grass_Wispy_Tall")):
        o.append(laden("natur", s, f"KitGras{i + 1}", hoehe=(0.55, 0.9, 0.5, 0.8)[i]))
    for i, s in enumerate(("Clover_1", "Clover_2")):
        o.append(laden("natur", s, f"Klee{i + 1}", hoehe=0.3))
    o.append(laden("natur", "Fern_1", "KitFarn1", breite=1.3))
    for i, s in enumerate(("Plant_1", "Plant_7", "Plant_1_Big")):
        o.append(laden("natur", s, f"Kraut{i + 1}", hoehe=(0.7, 0.2, 1.4)[i]))
    for i, s in enumerate(("Flower_3_Group", "Flower_4_Group", "Flower_3_Single", "Flower_4_Single")):
        o.append(laden("natur", s, f"KitBlumen{i + 1}", hoehe=0.7))
    for i, s in enumerate(("Mushroom_Common", "Mushroom_Laetiporus")):
        o.append(laden("natur", s, f"KitPilze{i + 1}", faktor=0.6 if i == 0 else 0.35))
    sauber(o)
    return o


def natur_steine():
    bs.neue_szene()
    o = []
    for i in range(1, 6):
        o.append(laden("natur", f"Pebble_Round_{i}", f"KitKiesel{i}", faktor=1.0))
    for i in range(1, 7):
        o.append(laden("natur", f"Pebble_Square_{i}", f"KitKiesel{i + 5}", faktor=1.0))
    for i, s in enumerate(("Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3")):
        o.append(laden("natur", s, f"KitFels{i + 1}", faktor=0.6, ziel=300))
    for i, s in enumerate(("RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Round_Small_3")):
        o.append(laden("natur", s, f"Trittstein{i + 1}", faktor=0.9, ziel=500))
    for i, s in enumerate(("RockPath_Square_Small_1", "RockPath_Square_Small_2", "RockPath_Square_Small_3")):
        o.append(laden("natur", s, f"Trittstein{i + 4}", faktor=0.9, ziel=400))
    sauber(o)
    return o


# Dreiecksziele je Baum (Rinde, Laub bzw. Nadeln): Mittelweg zwischen dem vollen Kit-Baum (1.600 bis 6.600) und unseren Bäumen (37 bis 217)
KIEFERN = (("Pine_5", 7.5, 200, 760), ("Pine_2", 8.0, 300, 600), ("Pine_4", 10.5, 320, 760), ("Pine_1", 7.5, 300, 600), ("Pine_3", 7.5, 300, 440))
TOTBAEUME = (("DeadTree_1", 7.0, 1500), ("DeadTree_2", 8.0, 1500), ("DeadTree_3", 9.0, 1500))
LAUBBAEUME = (("CommonTree_5", 8.0, 450, 520), ("CommonTree_3", 9.5, 450, 560), ("CommonTree_2", 8.5, 500, 540))


def natur_baeume():
    bs.neue_szene()
    o = []
    for i, (s, h, rinde, nadeln) in enumerate(KIEFERN):
        b = laden("natur", s, f"Kiefer{i + 1}", hoehe=h)
        o.append(baum_mittel(b, b.name, rinde, nadeln, saat=10 + i))
    for i, (s, h, rinde) in enumerate(TOTBAEUME):
        b = laden("natur", s, f"Totbaum{i + 1}", hoehe=h)
        o.append(baum_mittel(b, b.name, rinde, 0, saat=20 + i))
    laub = []
    for i, (s, h, rinde, blaetter) in enumerate(LAUBBAEUME):
        b = laden("natur", s, f"Laubbaum{i + 1}", hoehe=h)
        laub.append(baum_mittel(b, b.name, rinde, blaetter, saat=30 + i))
    o.extend(laub)
    for b in laub:
        o.append(winterfassung(b, b.name + "_Winter"))
    sauber(o)
    return o


def props_gegenstaende():
    bs.neue_szene()
    o = []

    def p(stamm, name, **kw):
        kw.setdefault("ziel", 1500)
        o.append(laden("props", stamm, name, **kw))

    for i, s in enumerate(("Barrel", "Barrel_Apples")):
        p(s, f"Fass{i + 1}")
    p("Barrel_Holder", "Fassgestell1")
    for i, s in enumerate(("Crate_Wooden", "FarmCrate_Empty", "FarmCrate_Apple", "FarmCrate_Carrot")):
        p(s, f"Kiste{i + 1}")
    p("Chest_Wood", "Truhe1")
    p("Bucket_Wooden_1", "Eimer1")
    for i, s in enumerate(("Bag", "Pouch_Large")):
        p(s, f"Sack{i + 1}", faktor=1.0 if i == 0 else 2.0)
    p("Cauldron", "Kessel1")
    p("Pot_1", "Topf1")
    p("Bench", "Bank1")
    p("Table_Large", "Tisch1")
    p("Stool", "Hocker1")
    p("Chair_1", "Stuhl1")
    for i, s in enumerate(("Bed_Twin1", "Bed_Twin2")):
        p(s, f"Bett{i + 1}")
    p("Cabinet", "Schrank1")
    for i, s in enumerate(("Bookcase_2", "Shelf_Arch")):
        p(s, f"Regal{i + 1}")
    p("Workbench", "Werkbank1")
    for i, s in enumerate(("Anvil", "Anvil_Log")):
        p(s, f"KitAmboss{i + 1}")
    p("Whetstone", "Schleifstein1")
    p("WeaponStand", "Waffenstaender1")
    p("Dummy", "Strohpuppe1")
    for i, s in enumerate(("Stall_Empty", "Stall_Cart_Empty")):
        p(s, f"Marktstand{i + 1}")
    for i, s in enumerate(("Banner_1", "Banner_2")):
        p(s, f"Banner{i + 1}")
    for i, s in enumerate(("Rope_1", "Rope_2", "Rope_3")):
        p(s, f"Seil{i + 1}")
    for i, s in enumerate(("Vase_2", "Vase_4")):
        p(s, f"Vase{i + 1}")
    for i, s in enumerate(("BookGroup_Medium_1", "BookGroup_Medium_2", "BookGroup_Small_1")):
        p(s, f"Buecher{i + 1}")
    p("CandleStick_Stand", "Kerzenstaender1")
    p("Cage_Small", "Kaefig1", ziel=1000)
    sauber(o)
    return o


def props_dorf():
    bs.neue_szene()
    o = []

    def p(stamm, name, **kw):
        kw.setdefault("ziel", 1500)
        o.append(laden("dorf", stamm, name, **kw))

    for i, s in enumerate(("Stairs_Exterior_Straight", "Stairs_Exterior_Platform", "Stairs_Exterior_SidePlatform")):
        p(s, f"Treppe{i + 1}")
    for i, s in enumerate(("Prop_WoodenFence_Single", "Prop_WoodenFence_Extension1", "Prop_WoodenFence_Extension2")):
        p(s, f"Zaun{i + 1}")
    for i, s in enumerate(("Prop_MetalFence_Simple", "Prop_MetalFence_Ornament")):
        p(s, f"Eisenzaun{i + 1}", ziel=700)
    for i, s in enumerate(("Door_4_Flat", "Door_8_Flat")):
        p(s, f"Tuer{i + 1}")
    for i, s in enumerate(("WindowShutters_Wide_Flat_Open", "WindowShutters_Thin_Flat_Open", "WindowShutters_Wide_Flat_Closed")):
        p(s, f"Fensterladen{i + 1}")
    for i, s in enumerate(("Prop_Chimney", "Prop_Chimney2")):
        p(s, f"Schornstein{i + 1}")
    p("Prop_Wagon", "Handwagen1")
    for i, s in enumerate(("Balcony_Cross_Straight", "Balcony_Simple_Straight")):
        p(s, f"Balkon{i + 1}", ziel=600)
    sauber(o)
    return o


BAUKAESTEN = {
    "natur": [("natur_kit_boden", natur_boden, dict(spalten=7, abstand=2.2, zeilenabstand=2.6, ziel_hoehe=0.5, ausschnitt=20)),
              ("natur_kit_steine", natur_steine, dict(spalten=7, abstand=2.2, zeilenabstand=2.6, ziel_hoehe=0.4, ausschnitt=20)),
              ("natur_kit_baeume", natur_baeume, dict(spalten=7, abstand=9, zeilenabstand=10, ziel_hoehe=5, ausschnitt=70))],
    "props": [("props_gegenstaende", props_gegenstaende, dict(spalten=8, abstand=3.4, zeilenabstand=3.4, ziel_hoehe=1, ausschnitt=27))],
    "dorf": [("props_dorf", props_dorf, dict(spalten=6, abstand=4.2, zeilenabstand=4.2, ziel_hoehe=1.5, ausschnitt=26))],
}


def main():
    args = [a for a in sys.argv[sys.argv.index("--") + 1:] if not a.startswith("--")] if "--" in sys.argv else \
        [a for a in sys.argv[1:] if not a.startswith("--")]
    wahl = args[0] if args else "alle"
    gruppen = ["natur", "props", "dorf"] if wahl == "alle" else [g for g in BAUKAESTEN if g == wahl or any(n == wahl for n, _, _ in BAUKAESTEN[g])]
    for g in gruppen:
        for name, bauer, bild in BAUKAESTEN[g]:
            if wahl not in ("alle", g, name):
                continue
            _stoffe.clear()
            _nachbessern.clear()
            objekte = bauer()
            melden(name, objekte)
            schreiben(name, objekte)
            if "--ohne-bilder" not in sys.argv:
                sichtbar = [x for x in objekte if not x.name.endswith("_Winter")] if g == "natur" and name.endswith("baeume") else objekte
                nt.bild(name, sichtbar, **bild)


if __name__ == "__main__":
    main()
