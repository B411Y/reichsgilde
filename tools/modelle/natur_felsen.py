"""Felsstücke für den Fels-Pinsel (Felspinsel F1): 8 Grundformen in je 3 Feinheitsstufen, Struktur prozedural gebacken.

    blender -b --python tools/modelle/natur_felsen.py -- [--schnell] [--ohne-bilder] [--bilder=<ordner>] [--nur=wand,block]

Schreibt game/assets/modelle/natur/natur_felsen.glb und die Strukturkarten game/assets/texturen/felsen/:
    felsen_normal.webp     Normale (Tangentenraum, OpenGL: G+ = UV-v nach oben), verlustfrei
    felsen_hoehlung.webp   R Umgebungsverdeckung (1 = frei), G Kante (1 = scharfe, vorstehende Kante),
                           B Rinne (1 = tiefe Kluft oder Fuge), verlustfrei
Beide 2048 × 1024 Pixel: ein Atlas mit 4 × 2 Feldern zu 512 × 512, je Form ein Feld (Reihenfolge FORMEN, Zeile für Zeile
von oben links). Das Gestein (Farbe, Schichtung, Moos, Flechten) kommt aus dem Material (F2), nicht aus dem Modell: dieselben
Formen gelten für Grauwacke, Trachyt, Sandstein und Kalkstein.

Ablauf je Form:
  1. Hochaufgelöst formen: die Form ist eine Vereinigung kantiger Brocken (konvexe Hüllen zufälliger Punkte auf einem
     Superellipsoid – große, ebene Bruchflächen), mit dem Voxel-Remesher zu einem Netz verschmolzen, dann entlang der
     Normalen verschoben: Klüfte (Voronoi-Ränder), Beulen und Rauheit (Rauschen). Unter z = 0 abgeschnitten.
  2. Spielnetz: das hohe Netz vereinfacht (Collapse) auf DREIECKE[form][0]; Stufe 1 und 2 vereinfachen Stufe 0 weiter und
     behalten deren UV, nutzen also dieselben Karten.
  3. Backen (Cycles, vom hohen aufs Spielnetz): Normale, Umgebungsverdeckung, Kante und Rinne (aus der „Pointiness“).

Knoten (Ursprung: Mitte der Grundfläche am unteren Ende, z = 0; Y oben im Spiel; Vorderseite +Z im Spiel, die Rückseite gehört
in den Hang): „<Form>_0“ (fein), „<Form>_1“, „<Form>_2“ (grob), z. B. „Wand_0“. Unterseite offen: Die Stücke stecken
20–40 % ihrer Höhe im Hang. Ein Material „Fels“ (Platzhalter, das Spiel setzt das Fels-Material).
Vertexfarbe COLOR_0 (linear): R Höhlung (Verdeckung × (1 − Rinne), 1 = frei), G Kante, B Höhe im Stück (0 unten … 1 oben),
A Tiefe (0 vorn … 1 hinten, zum Hang). Schichthöhe in Metern ist die Modellhöhe (VERTEX.y im Spiel).
UV0: Atlas der Strukturkarten. Kein UV2.
"""

import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oberflaeche"))
import bausteine as bs  # noqa: E402
import bodentexturen_blender as bt  # noqa: E402  (nur der WebP-Schreiber)

NAME = "natur_felsen"
TEXTUREN = os.path.join(bs.WURZEL, "game", "assets", "texturen", "felsen")
ATLAS = (2048, 1024)
FELD = 512
SPALTEN = 4

# Form: Maße (Breite x, Tiefe y, Höhe z in m) nur als Richtwert, Dreiecke je Stufe 0/1/2
FORMEN = ("wand", "kuppe", "rippe", "block", "platte", "ueberhang", "schuttbrocken", "felsnase")
KNOTEN = {"wand": "Wand", "kuppe": "Kuppe", "rippe": "Rippe", "block": "Block", "platte": "Platte",
          "ueberhang": "Ueberhang", "schuttbrocken": "Schuttbrocken", "felsnase": "Felsnase"}
DREIECKE = {"wand": (2400, 800, 240), "kuppe": (2000, 700, 200), "rippe": (1800, 600, 180), "block": (1000, 350, 100),
            "platte": (700, 250, 80), "ueberhang": (2400, 800, 240), "schuttbrocken": (240, 90, 30),
            "felsnase": (1600, 550, 160)}


def argumente():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bilder = next((x.split("=", 1)[1] for x in a if x.startswith("--bilder=")), bs.VORSCHAU)
    nur = next((x.split("=", 1)[1].split(",") for x in a if x.startswith("--nur=")), None)
    return "--schnell" in a, "--ohne-bilder" in a, bilder, nur


# ── 1. Formen aus Brocken ────────────────────────────────────────────────────────────────────────────────────────────
def brocken(z, mitte, masse, kante=3.0, punkte=26, zacken=0.12, drehung=(0, 0, 0)):
    """Konvexer, kantiger Brocken: Hülle von Punkten auf einem Superellipsoid (Exponent `kante`: 2 rund, 4 kastig)."""
    pts = []
    gold = math.pi * (3 - math.sqrt(5))
    for i in range(punkte):
        zz = 1 - 2 * (i + 0.5) / punkte
        r = math.sqrt(1 - zz * zz)
        w = gold * i + z.uniform(-0.35, 0.35)
        d = Vector((math.cos(w) * r, math.sin(w) * r, zz))
        # Superellipsoid: Richtung so strecken, dass |x|^e + |y|^e + |z|^e = 1
        s = (abs(d.x) ** kante + abs(d.y) ** kante + abs(d.z) ** kante) ** (-1 / kante)
        d = d * s * (1 + z.uniform(-zacken, zacken))
        pts.append(Vector((d.x * masse[0] / 2, d.y * masse[1] / 2, d.z * masse[2] / 2)))
    rot = Matrix.Rotation(math.radians(drehung[2]), 4, "Z") @ Matrix.Rotation(math.radians(drehung[1]), 4, "Y") \
        @ Matrix.Rotation(math.radians(drehung[0]), 4, "X")
    bm = bmesh.new()
    for p in pts:
        bm.verts.new((rot @ p) + Vector(mitte))
    bmesh.ops.convex_hull(bm, input=bm.verts)
    return bm


def form_brocken(form, z):
    """Liste der Brocken (bmesh) einer Form. Vorderseite −Y (im Spiel +Z), Hang hinten (+Y). Alles reicht unter z = 0."""
    b = []
    if form == "wand":                       # 8 × 2,5 × 6: Pfeiler und Quader nebeneinander, Front leicht versetzt
        x = -4.0
        while x < 4.0:
            w = z.uniform(1.3, 2.2)
            h = z.uniform(4.6, 6.2)
            b.append(brocken(z, (x + w / 2, z.uniform(-0.15, 0.25), h / 2 - 0.4), (w * 1.1, 2.4, h + 0.8), 4.0, 30,
                             0.06, (z.uniform(-3, 3), z.uniform(-4, 4), z.uniform(-5, 5))))
            if z.random() < 0.6:             # Quader obendrauf, etwas zurückgesetzt
                b.append(brocken(z, (x + w / 2, 0.5, h + 0.1), (w * 1.0, 1.9, 1.0), 4.5, 22, 0.05,
                                 (0, z.uniform(-6, 6), z.uniform(-6, 6))))
            x += w * 0.9
        b.append(brocken(z, (0, 1.0, 2.2), (8.4, 1.6, 5.2), 4.0, 30, 0.04))          # Rücken, damit nichts durchscheint
    elif form == "kuppe":                    # 6 × 6 × 3,2: runder, gebankter Buckel
        b.append(brocken(z, (0, 0.2, 0.9), (6.0, 5.6, 4.6), 2.3, 46, 0.07))
        b.append(brocken(z, (-1.4, -0.9, 0.6), (3.0, 3.0, 3.2), 2.5, 26, 0.1, (0, 0, 20)))
        b.append(brocken(z, (1.6, 0.6, 1.3), (2.6, 2.8, 3.4), 2.6, 26, 0.1, (0, 0, -15)))
    elif form == "rippe":                    # 2,4 × 9 × 3,5: Rippe hangabwärts (nach vorn −Y) flacher werdend
        for k in range(5):
            y = 3.6 - k * 1.85
            h = 3.6 - k * 0.6
            b.append(brocken(z, (z.uniform(-0.15, 0.15), y, h / 2 - 0.3), (z.uniform(1.8, 2.5), 2.4, h + 0.6), 3.2, 24,
                             0.1, (z.uniform(-6, 6), z.uniform(-10, 10), z.uniform(-12, 12))))
    elif form == "block":                    # 2,4 × 2,2 × 2: kastiger Quader
        b.append(brocken(z, (0, 0, 0.8), (2.4, 2.2, 2.4), 4.5, 28, 0.06, (2, -3, 8)))
    elif form == "platte":                   # 3 × 2,4 × 0,7: flache Platte, leicht schief
        b.append(brocken(z, (0, 0, 0.2), (3.0, 2.4, 1.0), 4.0, 30, 0.05, (3, -2, 0)))
    elif form == "ueberhang":                # 6 × 4 × 4,5: unten zurückgesetzt, oben kragt eine Bank 1,2 m vor
        b.append(brocken(z, (0, 0.8, 1.2), (5.6, 2.6, 3.2), 3.6, 28, 0.06))
        b.append(brocken(z, (0.2, -0.3, 3.4), (6.2, 3.8, 1.6), 4.0, 30, 0.05, (-6, 2, 3)))
        b.append(brocken(z, (-1.6, 0.9, 4.1), (2.6, 2.2, 1.2), 3.5, 20, 0.08, (0, 0, 12)))
    elif form == "schuttbrocken":            # 0,6 × 0,5 × 0,4: kantiger Brocken für Halden
        b.append(brocken(z, (0, 0, 0.14), (0.62, 0.5, 0.52), 2.8, 18, 0.14, (5, 8, 20)))
    elif form == "felsnase":                 # 3 × 4,5 × 3,8: keilförmige Nase, die aus dem Hang nach vorn und oben ragt
        b.append(brocken(z, (0, 1.0, 1.4), (3.0, 2.8, 3.6), 3.4, 26, 0.07))
        b.append(brocken(z, (0, -0.6, 2.2), (2.2, 3.4, 1.6), 3.2, 24, 0.08, (-22, 0, 5)))
        b.append(brocken(z, (0.15, -1.6, 2.6), (1.3, 1.6, 1.0), 3.0, 18, 0.1, (-30, 5, -8)))
    return b


def hoch_netz(form, saat, schnell):
    z = random.Random(saat)
    bm = bmesh.new()
    for teil in form_brocken(form, z):
        me = bpy.data.meshes.new("_t")
        teil.to_mesh(me)
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        teil.free()
    me = bpy.data.meshes.new(f"_Hoch_{form}")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(f"_Hoch_{form}", me)
    bpy.context.scene.collection.objects.link(obj)
    gross = max(obj.dimensions)
    mod = obj.modifiers.new("Remesh", "REMESH")
    mod.mode = "VOXEL"
    mod.voxel_size = gross / (140 if schnell else 300)
    mod.adaptivity = 0.0
    anwenden(obj)
    verschieben(obj, gross, saat)
    abschneiden(obj)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def anwenden(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    neu = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    alt = obj.data
    obj.modifiers.clear()
    name = alt.name
    obj.data = neu
    bpy.data.meshes.remove(alt)
    neu.name = name


def verschieben(obj, gross, saat):
    """Klüfte, Beulen und Rauheit entlang der Normalen (in Metern relativ zur Größe der Form)."""
    me = obj.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(n, 3)
    no = np.empty(n * 3)
    me.vertices.foreach_get("normal", no)
    no = no.reshape(n, 3)
    versatz = Vector((saat * 0.731 % 50, saat * 0.377 % 50, saat * 0.913 % 50))
    d = np.empty(n)
    for i in range(n):
        p = Vector(co[i]) + versatz
        # verzogene Lage, damit die Klüfte nicht wie ein Pflaster aussehen
        q = p + noise.noise_vector(p / (gross * 0.22), noise_basis="PERLIN_ORIGINAL") * (gross * 0.06)
        abst, _ = noise.voronoi(q / (gross * 0.34), distance_metric="DISTANCE", exponent=2.5)
        kluft = max(0.0, 1.0 - (abst[1] - abst[0]) / 0.05)                    # 1 auf der Grenze zweier Zellen
        abst, _ = noise.voronoi(q / (gross * 0.12) + Vector((7.1, 3.3, 1.9)), distance_metric="DISTANCE", exponent=2.5)
        riss = max(0.0, 1.0 - (abst[1] - abst[0]) / 0.035)
        beule = noise.fractal(p / (gross * 0.3), 0.5, 2.0, 3, noise_basis="PERLIN_ORIGINAL")
        rau = noise.fractal(p / (gross * 0.035), 0.6, 2.2, 3, noise_basis="BLENDER")
        d[i] = gross * (-0.009 * kluft * kluft - 0.0025 * riss * riss + 0.010 * beule + 0.0035 * rau)
    co += no * d[:, None]
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def abschneiden(obj):
    """Alles unter z = 0 weg, die Unterseite bleibt offen."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, 0), plane_no=(0, 0, 1), clear_inner=True)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


# ── 2. Spielnetze ────────────────────────────────────────────────────────────────────────────────────────────────────
def vereinfachen(quelle, name, ziel):
    me = quelle.data.copy()
    me.name = name
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    dreiecke = sum(len(p.vertices) - 2 for p in me.polygons)
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = min(1.0, ziel / max(1, dreiecke))
    mod.use_collapse_triangulate = True
    anwenden(obj)
    obj.modifiers.new("Tri", "TRIANGULATE")
    anwenden(obj)
    for v in obj.data.vertices:              # Collapse legt Punkte am offenen Rand auch knapp unter z = 0
        if v.co.z < 0:
            v.co.z = 0.0
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def aufklappen(obj, feld):
    """UV: Smart Project, in das Atlasfeld `feld` gepackt."""
    for o in bpy.context.scene.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.008)
    bpy.ops.object.mode_set(mode="OBJECT")
    sp, ze = feld % SPALTEN, feld // SPALTEN
    zeilen = ATLAS[1] // FELD
    su, sv = FELD / ATLAS[0], FELD / ATLAS[1]
    rand = 3 / FELD                                       # 3 Pixel Abstand zum Nachbarfeld
    uv = obj.data.uv_layers.active.data
    for l in uv:
        u, v = l.uv
        u = rand + u * (1 - 2 * rand)
        v = rand + v * (1 - 2 * rand)
        l.uv = (sp * su + u * su, (zeilen - 1 - ze) * sv + v * sv)   # Zeile 0 oben im Bild


# ── 3. Backen ────────────────────────────────────────────────────────────────────────────────────────────────────────
def bild(name, farbe):
    img = bpy.data.images.new(name, ATLAS[0], ATLAS[1], alpha=False, float_buffer=True)
    img.colorspace_settings.name = "Non-Color"
    img.generated_color = farbe
    return img


def back_stoff(img):
    m = bpy.data.materials.new("_Backziel")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    knoten = m.node_tree.nodes.new("ShaderNodeTexImage")
    knoten.image = img
    m.node_tree.nodes.active = knoten
    return m


def hoch_stoff():
    """Emission = (Kante, Rinne, 0) aus der Pointiness des hohen Netzes."""
    m = bpy.data.materials.new("_Hoch")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    aus = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    kante = nt.nodes.new("ShaderNodeMapRange")
    kante.inputs["From Min"].default_value, kante.inputs["From Max"].default_value = 0.51, 0.58
    rinne = nt.nodes.new("ShaderNodeMapRange")
    rinne.inputs["From Min"].default_value, rinne.inputs["From Max"].default_value = 0.49, 0.43
    nt.links.new(geo.outputs["Pointiness"], kante.inputs["Value"])
    nt.links.new(geo.outputs["Pointiness"], rinne.inputs["Value"])
    comb = nt.nodes.new("ShaderNodeCombineColor")
    nt.links.new(kante.outputs["Result"], comb.inputs[0])
    nt.links.new(rinne.outputs["Result"], comb.inputs[1])
    nt.links.new(comb.outputs[0], em.inputs["Color"])
    nt.links.new(em.outputs[0], aus.inputs["Surface"])
    return m


def backen(paare, gross, schnell):
    """paare: [(hoch, spiel)] – backt alle Formen in drei Atlanten; gibt (normal, ao, kante_rinne) als numpy zurück."""
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    print("Cycles backt auf:", bs.cycles_geraet(szene))
    szene.cycles.samples = 16 if schnell else 64
    bake = szene.render.bake
    bake.use_selected_to_active = True
    bake.use_clear = False
    bake.margin = 3
    bake.margin_type = "EXTEND"
    bake.target = "IMAGE_TEXTURES"
    bilder = {"NORMAL": bild("_normal", (0.5, 0.5, 1.0, 1.0)), "AO": bild("_ao", (1, 1, 1, 1)),
              "EMIT": bild("_kante", (0, 0, 0, 1))}
    stoffe = {k: back_stoff(v) for k, v in bilder.items()}
    hs = hoch_stoff()
    for hoch, spiel in paare:
        hoch.data.materials.clear()
        hoch.data.materials.append(hs)
        g = gross[hoch.name]
        bake.cage_extrusion = g * 0.02
        bake.max_ray_distance = g * 0.05
        for o in szene.objects:                  # nur das Paar rendert (alle Stücke stehen im Ursprung)
            o.hide_render = o not in (hoch, spiel)
        for art in ("NORMAL", "AO", "EMIT"):
            spiel.data.materials.clear()
            spiel.data.materials.append(stoffe[art])
            for o in szene.objects:
                o.select_set(o in (hoch, spiel))
            bpy.context.view_layer.objects.active = spiel
            if art == "NORMAL":
                bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")
            else:
                bpy.ops.object.bake(type=art)
        print(f"  gebacken: {spiel.name}")
    aus = {}
    for k, img in bilder.items():
        px = np.empty(ATLAS[0] * ATLAS[1] * 4, np.float32)
        img.pixels.foreach_get(px)
        aus[k] = px.reshape(ATLAS[1], ATLAS[0], 4)[::-1]                    # Zeile 0 = oben
    return aus


def texturen_schreiben(gebacken):
    os.makedirs(TEXTUREN, exist_ok=True)
    normal = np.clip(np.round(gebacken["NORMAL"][:, :, :3] * 255), 0, 255).astype(np.uint8)
    ao = gebacken["AO"][:, :, 0]
    kr = gebacken["EMIT"]
    hoehlung = np.dstack([ao, kr[:, :, 0], kr[:, :, 1]])
    hoehlung = np.clip(np.round(hoehlung * 255), 0, 255).astype(np.uint8)
    for datei, daten in (("felsen_normal.webp", normal), ("felsen_hoehlung.webp", hoehlung)):
        pfad = os.path.join(TEXTUREN, datei)
        groesst, _ = bt.webp(pfad, daten, 100)
        print(f"geschrieben: {os.path.relpath(pfad, bs.WURZEL)} (Abweichung nach Zurücklesen höchstens {groesst.max()})")


def vertexfarben(obj, gebacken):
    """COLOR_0 je Ecke: R Höhlung, G Kante (aus den gebackenen Karten an der UV), B Höhe, A Tiefe."""
    me = obj.data
    attr = me.color_attributes.new("Farbe", "FLOAT_COLOR", "CORNER")
    uv = me.uv_layers.active.data
    zmax = max(v.co.z for v in me.vertices) or 1.0
    ys = [v.co.y for v in me.vertices]
    y0, y1 = min(ys), max(ys)
    ao, kr = gebacken["AO"], gebacken["EMIT"]
    for li, loop in enumerate(me.loops):
        u, v = uv[li].uv
        px = min(ATLAS[0] - 1, max(0, int(u * ATLAS[0])))
        py = min(ATLAS[1] - 1, max(0, int((1 - v) * ATLAS[1])))
        p = me.vertices[loop.vertex_index].co
        hoehlung = float(ao[py, px, 0]) * (1 - 0.6 * float(kr[py, px, 1]))
        attr.data[li].color = (hoehlung, float(kr[py, px, 0]), max(0.0, p.z / zmax),
                               (p.y - y0) / max(1e-6, y1 - y0))
    me.color_attributes.active_color = attr


def platzhalter_stoff():
    m = bpy.data.materials.new("Fels")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.32, 0.31, 0.29, 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    return m


# ── Vorschau ─────────────────────────────────────────────────────────────────────────────────────────────────────────
def vorschau_stoff():
    """Grauer Fels mit den gebackenen Karten (aus den geschriebenen WebP-Dateien) – so ungefähr sieht F2 sie."""
    m = bpy.data.materials.new("_Vorschau")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.9
    nimg = nt.nodes.new("ShaderNodeTexImage")
    nimg.image = bpy.data.images.load(os.path.join(TEXTUREN, "felsen_normal.webp"))
    nimg.image.colorspace_settings.name = "Non-Color"
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(nimg.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    himg = nt.nodes.new("ShaderNodeTexImage")
    himg.image = bpy.data.images.load(os.path.join(TEXTUREN, "felsen_hoehlung.webp"))
    himg.image.colorspace_settings.name = "Non-Color"
    trenn = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(himg.outputs["Color"], trenn.inputs[0])
    # Farbe: Grundgrau × Verdeckung, Kanten heller, Rinnen dunkler; Moos auf Flächen nach oben (wie F2 es vorhat)
    grau = nt.nodes.new("ShaderNodeRGB")
    grau.outputs[0].default_value = (0.30, 0.29, 0.27, 1)
    hell = nt.nodes.new("ShaderNodeMix")
    hell.data_type = "RGBA"
    nt.links.new(trenn.outputs[1], hell.inputs["Factor"])
    nt.links.new(grau.outputs[0], hell.inputs[6])
    hell.inputs[7].default_value = (0.55, 0.53, 0.49, 1)
    dunkel = nt.nodes.new("ShaderNodeMix")
    dunkel.data_type = "RGBA"
    nt.links.new(trenn.outputs[2], dunkel.inputs["Factor"])
    nt.links.new(hell.outputs[2], dunkel.inputs[6])
    dunkel.inputs[7].default_value = (0.06, 0.06, 0.055, 1)
    ao = nt.nodes.new("ShaderNodeMix")
    ao.data_type = "RGBA"
    ao.blend_type = "MULTIPLY"
    ao.inputs["Factor"].default_value = 1.0
    nt.links.new(dunkel.outputs[2], ao.inputs[6])
    nt.links.new(trenn.outputs[0], ao.inputs[7])
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Normal"], sep.inputs[0])
    moos_f = nt.nodes.new("ShaderNodeMapRange")
    moos_f.inputs["From Min"].default_value, moos_f.inputs["From Max"].default_value = 0.72, 0.9
    nt.links.new(sep.outputs[2], moos_f.inputs["Value"])
    moos = nt.nodes.new("ShaderNodeMix")
    moos.data_type = "RGBA"
    nt.links.new(moos_f.outputs["Result"], moos.inputs["Factor"])
    nt.links.new(ao.outputs[2], moos.inputs[6])
    moos.inputs[7].default_value = (0.09, 0.13, 0.04, 1)
    nt.links.new(moos.outputs[2], bsdf.inputs["Base Color"])
    return m


def vorschau(spielnetze, ordner, schnell):
    """Alle 24 Stücke in einer Reihe je Stufe (Spielkamera 40°), dazu Nahbild Stufe 0, im Boden um 25 % eingesunken."""
    szene = bpy.context.scene
    st = vorschau_stoff()
    boden_st = bs._hilfsstoff("_Boden", (0.33, 0.33, 0.25))
    me = bpy.data.meshes.new("_Boden")
    me.from_pydata([(-60, -60, 0), (60, -60, 0), (60, 60, 0), (-60, 60, 0)], [], [(0, 1, 2, 3)])
    me.materials.append(boden_st)
    boden = bpy.data.objects.new("_Boden", me)
    szene.collection.objects.link(boden)
    kopien = []
    x = -22.0
    for form in [f for f in FORMEN if (f, 0) in spielnetze]:
        breite = max(spielnetze[(form, 0)].dimensions.x, 1.0)
        for stufe in range(3):
            o = spielnetze[(form, stufe)]
            k = bpy.data.objects.new(f"_V_{o.name}", o.data.copy())
            k.data.materials.clear()
            k.data.materials.append(st)
            h = o.dimensions.z
            k.location = (x + breite / 2, -10.0 + stufe * 9.0, -0.25 * h)
            szene.collection.objects.link(k)
            kopien.append(k)
        x += breite + 1.5
    sonne = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
    sonne.data.energy = 3.2
    sonne.data.color = (1.0, 0.94, 0.84)
    sonne.data.angle = math.radians(2.5)
    sonne.rotation_euler = (math.radians(45), 0, math.radians(-35))
    szene.collection.objects.link(sonne)
    welt = bpy.data.worlds.new("Himmel")
    try:
        welt.use_nodes = True
    except AttributeError:
        pass
    welt.node_tree.nodes["Background"].inputs["Color"].default_value = (0.40, 0.45, 0.53, 1)
    welt.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.85
    szene.world = welt
    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    kam.data.type = "ORTHO"
    szene.collection.objects.link(kam)
    szene.camera = kam
    szene.cycles.samples = 16 if schnell else 64
    szene.cycles.use_denoising = True
    szene.view_settings.view_transform = "Standard"
    os.makedirs(ordner, exist_ok=True)
    mitte_x = (x - 1.5 - 22.0) / 2
    bilder = []
    for name, ziel, ausschnitt, groesse in (("alle", (mitte_x, -1.0, 1.0), x + 26, (2000, 1000)),
                                           ("nah_links", (-22 + 12, -10, 2.0), 26, (1600, 900)),
                                           ("nah_rechts", (x - 14, -10, 2.0), 26, (1600, 900))):
        n = math.radians(bs.KAMERA_NEIGUNG)
        richtung = Vector((0.0, math.cos(n), -math.sin(n)))                # von vorn (−Y) auf die Front
        kam.location = Vector(ziel) - richtung * 200
        kam.rotation_euler = richtung.to_track_quat("-Z", "Y").to_euler()
        kam.data.ortho_scale = ausschnitt
        kam.data.clip_end = 500
        szene.render.resolution_x, szene.render.resolution_y = groesse
        szene.render.resolution_percentage = 50 if schnell else 100
        for o in szene.objects:
            if o.type == "MESH":
                o.hide_render = not (o.name.startswith("_V_") or o.name == "_Boden")
        pfad = os.path.join(ordner, f"{NAME}_{name}.png")
        szene.render.filepath = pfad
        bpy.ops.render.render(write_still=True)
        bilder.append(pfad)
    return bilder


def main():
    schnell, ohne_bilder, ordner, nur = argumente()
    bs.neue_szene()
    formen = [f for f in FORMEN if not nur or f in nur]
    if nur or schnell:
        print("WARNUNG: --nur/--schnell überschreibt Modell und Atlas mit einer Probe – vor dem Commit ohne beides neu bauen")
    paare, gross, spielnetze = [], {}, {}
    for i, form in enumerate(FORMEN):
        if form not in formen:
            continue
        hoch = hoch_netz(form, 4100 + 17 * i, schnell)
        gross[hoch.name] = max(hoch.dimensions)
        tri_hoch = sum(len(p.vertices) - 2 for p in hoch.data.polygons)
        stufe0 = vereinfachen(hoch, f"{KNOTEN[form]}_0", DREIECKE[form][0])
        aufklappen(stufe0, i)
        spielnetze[(form, 0)] = stufe0
        for s in (1, 2):
            spielnetze[(form, s)] = vereinfachen(stufe0, f"{KNOTEN[form]}_{s}", DREIECKE[form][s])
        paare.append((hoch, stufe0))
        d = hoch.dimensions
        print(f"{form}: {d.x:.2f} × {d.y:.2f} × {d.z:.2f} m, hoch {tri_hoch} Dreiecke, Stufen "
              + " / ".join(str(len(spielnetze[(form, s)].data.polygons)) for s in range(3)))
    gebacken = backen(paare, gross, schnell)
    texturen_schreiben(gebacken)
    fels = platzhalter_stoff()
    teile = []
    for (form, s), o in sorted(spielnetze.items(), key=lambda e: (FORMEN.index(e[0][0]), e[0][1])):
        vertexfarben(o, gebacken)
        o.data.materials.clear()
        o.data.materials.append(fels)
        teile.append(o)
    pfad = bs.exportieren(NAME, teile)
    print("geschrieben:", os.path.relpath(pfad, bs.WURZEL))
    if not ohne_bilder:
        for h, _ in paare:
            h.hide_render = True
        for p in vorschau(spielnetze, ordner, schnell):
            print("Bild:", p)


if __name__ == "__main__":
    main()
