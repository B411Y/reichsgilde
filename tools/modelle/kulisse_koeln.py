"""Kulissenberge um Köln: ein Ring aus drei Staffeln nach den echten Höhen (DGM NRW), vereinfacht.

    blender -b --python tools/modelle/kulisse_koeln.py -- [--schnell] [--ohne-bilder] [--bilder=<ordner>]

Liest tools/modelle/kulisse_koeln_profil.json (aus kulisse_hoehen.py) und schreibt
game/assets/modelle/kulisse/kulisse_koeln.glb und game/daten/berge.json (Namen, Lage, Höhe der Berge und ihr Ort im Spiel).
Vorschaubilder nach --bilder (Vorgabe docs/modelle/).

Lage: Der Ring liegt hinter der Außenkante der Kölner Karte (Spielfeld 700 × 450 m um −50 | 25 plus 300 m Randstreifen)
und reicht bis 300 m darüber hinaus. Jede Peilung (Grad ab Norden, die Karte ist genordet) zeigt die echten Berge in dieser
Richtung: die nahe Staffel das Band 6–16 km (Ville, vorderes Bergisches Land), die mittlere 16–27 km, die ferne 27–49 km
(Siebengebirge, Eifelrand). Waagrecht ist alles stark gestaucht (Kilometer werden zu Metern), die Höhe ist echt × HOEHE_MASS
über TAL_NN – so bleiben die Verhältnisse der Berge zueinander erhalten.

Knoten (Ursprung = Ursprung der Karte, Heumarkt; Y oben im Spiel): Staffel_Nah, Staffel_Mitte, Staffel_Fern – je ein Netz,
ein Material „Kulisse“. Keine Kollision, kein Schatten nötig (das Spiel schaltet ihn ab), nicht bespielbar.
Vertexfarbe COLOR_0 (linear): R = Wald (0–1), G = Fels (helle Bänder an steilen Stellen), B = Tiefe im Ring
(0 an der Außenkante, 1 bei 300 m dahinter; für den Dunst), A = Höhe / 250 m.
"""

import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402

NAME = "kulisse_koeln"
PROFIL = os.path.join(bs.WURZEL, "tools", "modelle", "kulisse_koeln_profil.json")
BERGE = os.path.join(bs.WURZEL, "game", "daten", "berge.json")

# Kölner Karte (game/karten/koeln.json): Spielfeld und Randstreifen → Außenkante als Rechteck um die Mitte
MITTE = (-50.0, 25.0)               # Spiel x | z
AUSSEN_HALB = (650.0, 525.0)        # (700 + 2·300) / 2, (450 + 2·300) / 2
RING_TIEFE = 300.0                  # so weit reicht der Ring hinter die Außenkante
TAL_NN = 37.0                       # m ü. NN, die im Spiel Höhe 0 haben (Rheinufer, wie beim Einlesen der Höhenkarte)
HOEHE_MASS = 0.5                    # echte Höhe über dem Tal × dies = Höhe im Spiel
HANG_MAX = 0.8                      # steilster Anstieg entlang des Rings (Höhe je Meter), sonst würden Gipfel zu Nadeln

# Staffel: Name im Profil, Knotenname, Kammtiefe von–bis (m hinter der Außenkante)
STAFFELN = (("nah", "Staffel_Nah", 75.0, 120.0),
            ("mitte", "Staffel_Mitte", 140.0, 200.0),
            ("fern", "Staffel_Fern", 210.0, 275.0))
HINTEN = 55.0                       # so weit fällt jede Staffel hinter dem Kamm ab (sieht die Kamera kaum)
SPALTEN_JE_GRAD = 1.5               # Spalten des Rasters (etwa 8 m am Ring)
ZEILE = 10.0                        # Zeilenabstand in der Tiefe (m)
ZEILEN_TIEFE = 330.0                # letzte Zeile: Rückseite der fernen Staffel


def argumente():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bilder = next((x.split("=", 1)[1] for x in a if x.startswith("--bilder=")), bs.VORSCHAU)
    return "--schnell" in a, "--ohne-bilder" in a, bilder


def aussenkante(peilung):
    """Abstand von der Kartenmitte bis zur Außenkante in Richtung `peilung` (Grad ab Norden): Superellipse (p = 8), so
    groß, dass sie die Ecken des Rechtecks einschließt – der Ring knickt an den Ecken nicht."""
    p, s = 8.0, 2.0 ** (1 / 8)
    w = math.radians(peilung)
    a, b = AUSSEN_HALB[0] * s, AUSSEN_HALB[1] * s
    return 1.0 / ((abs(math.sin(w)) / a) ** p + (abs(math.cos(w)) / b) ** p) ** (1 / p)


def punkt(peilung, tiefe):
    """Blender-Punkt (x Osten, y Norden) in `tiefe` m hinter der Außenkante."""
    r = aussenkante(peilung) + tiefe
    w = math.radians(peilung)
    return MITTE[0] + math.sin(w) * r, -MITTE[1] + math.cos(w) * r


def glatt(werte, sigma, rund=True):
    """Gauß über eine Ringliste."""
    n = len(werte)
    k = int(3 * sigma) + 1
    gew = [math.exp(-0.5 * (i / sigma) ** 2) for i in range(-k, k + 1)]
    return [sum(g * werte[(i + j) % n] for g, j in zip(gew, range(-k, k + 1))) / sum(gew) for i in range(n)]


def hang_begrenzen(hoehe, abstand):
    """Kegel-Dehnung: kein Nachbar fällt steiler als HANG_MAX ab – Gipfel bleiben, ihre Flanken werden breiter."""
    n = len(hoehe)
    h = list(hoehe)
    for _ in range(2):
        for i in range(1, 2 * n):
            h[i % n] = max(h[i % n], h[(i - 1) % n] - HANG_MAX * abstand)
        for i in range(2 * n, 0, -1):
            h[(i - 1) % n] = max(h[(i - 1) % n], h[i % n] - HANG_MAX * abstand)
    return h


def staffel_daten(profil, name, anzahl, kamm_von, kamm_bis):
    """Kammhöhe (Spiel-m) und Kammtiefe je Spalte."""
    s = next(s for s in profil["staffeln"] if s["name"] == name)
    schritt = profil["schritt_grad"]
    n_quelle = len(s["kamm_nn"])
    hoehe, lage = [], []
    for i in range(anzahl):
        pe = 360.0 * i / anzahl
        f = pe / schritt
        j = int(f) % n_quelle
        t = f - int(f)
        nn = s["kamm_nn"][j] * (1 - t) + s["kamm_nn"][(j + 1) % n_quelle] * t
        hoehe.append(max(0.0, nn - TAL_NN) * HOEHE_MASS)
        lage.append(s["kamm_lage"][j] * (1 - t) + s["kamm_lage"][(j + 1) % n_quelle] * t)
    # Gipfel mit Namen: Das 100-m-Raster mittelt sie ab, ihre feine Höhe hebt den Kamm an ihrer Peilung
    for g in profil["gipfel"]:
        if s["von_km"] <= g["entfernung_km"] <= s["bis_km"] + 3.0:
            ziel = max(0.0, g["hoehe_nn"] - TAL_NN) * HOEHE_MASS
            for i in range(anzahl):
                d = (360.0 * i / anzahl - g["peilung"] + 180) % 360 - 180
                hoehe[i] = max(hoehe[i], ziel * math.exp(-0.5 * (d / 0.45) ** 2))
    bogen = 2 * math.pi * (aussenkante(0) + aussenkante(90)) / 2 / anzahl
    hoehe = hang_begrenzen(glatt(hoehe, 0.8), bogen)
    lage = glatt(lage, 4.0)
    tiefe = [kamm_von + (kamm_bis - kamm_von) * min(1.0, max(0.0, x)) for x in lage]
    return hoehe, tiefe


def beulen(x, y, staerke):
    """Grobe Beulen (Wald als Buckel) und Rinnen (Erosion) in Spiel-Metern."""
    p = Vector((x / 38.0, y / 38.0, 0.37))
    b = noise.fractal(p, 0.55, 2.1, 4, noise_basis="PERLIN_ORIGINAL")
    r = 1.0 - abs(noise.noise(Vector((x / 45.0, y / 45.0, 4.1)), noise_basis="PERLIN_ORIGINAL"))
    return staerke * (0.7 * b - 0.25 * r * r)


def ring_netze(profil):
    """Ein durchgehendes Höhenfeld über den ganzen Ring (Spalten je Peilung, Zeilen je Tiefe): je Staffel ein Buckel mit
    dem Kamm an seiner Tiefe, das Feld ist das (weiche) Maximum der Buckel. Geteilt wird es an den Tälern zwischen den
    Kämmen – so schließen die Staffeln lückenlos aneinander und lassen sich trotzdem einzeln zeigen (Niedrig: nur Nah)."""
    anzahl = int(round(360 * SPALTEN_JE_GRAD))
    daten = [staffel_daten(profil, name, anzahl, von, bis) for name, _, von, bis in STAFFELN]
    zeilen = int(ZEILEN_TIEFE / ZEILE) + 1
    tiefen = [k * ZEILE for k in range(zeilen)]

    def buckel(h, kt, d):
        vorn = min(kt, max(45.0, 1.35 * h))
        u = (d - kt) / (vorn if d < kt else HINTEN)
        return h * (0.5 + 0.5 * math.cos(math.pi * u)) if abs(u) < 1 else 0.0

    hoehen, grenzen = [], []
    for i in range(anzahl):
        spalte = []
        for d in tiefen:
            werte = sorted((buckel(max(h[i], 3.0), t[i], d) for h, t in daten), reverse=True)
            a, b = werte[0], werte[1]                                              # weiches Maximum (Täler ohne Knick)
            k = 10.0
            g = max(0.0, k - abs(a - b)) / k
            spalte.append(a + g * g * k * 0.25)
        hoehen.append(spalte)
        # Täler: tiefster Punkt zwischen zwei Kämmen
        g = []
        for s in range(len(daten) - 1):
            k0, k1 = int(daten[s][1][i] / ZEILE), int(math.ceil(daten[s + 1][1][i] / ZEILE))
            g.append(min(range(k0, k1 + 1), key=lambda k: spalte[k]))
        grenzen.append(g)
    objekte = []
    for s, (name, knoten, _, _) in enumerate(STAFFELN):
        verts, farben, flaechen, index = [], [], [], {}
        for i in range(anzahl + 1):
            ii = i % anzahl
            pe = 360.0 * ii / anzahl
            von = 0 if s == 0 else grenzen[ii][s - 1]
            bis = zeilen - 1 if s == len(STAFFELN) - 1 else grenzen[ii][s]
            for k in range(min(von, grenzen_min(grenzen, s, ii, True)), max(bis, grenzen_max(grenzen, s, ii, zeilen)) + 1):
                d = tiefen[k]
                x, y = punkt(pe, d)
                h = hoehen[ii][k]
                z = h + beulen(x, y, 1.6 + 0.045 * h) * min(1.0, h / 25.0)
                if k == 0 or k == zeilen - 1:
                    z = -4.0                                                       # Fuß und Rückseite unter den Boden
                index[(i, k)] = len(verts)
                verts.append((x, y, max(z, -4.0)))
                farben.append(min(1.0, d / RING_TIEFE))
        for i in range(anzahl):
            ii, jj = i % anzahl, (i + 1) % anzahl
            von = 0 if s == 0 else max(grenzen[ii][s - 1], grenzen[jj][s - 1])
            bis = zeilen - 1 if s == len(STAFFELN) - 1 else max(grenzen[ii][s], grenzen[jj][s])
            for k in range(von, bis):
                q = [(i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1)]
                if all(e in index for e in q):
                    flaechen.append(tuple(index[e] for e in q))
        # Spalte anzahl ist Spalte 0: dieselben Punkte doppelt – zusammenlegen
        me = bpy.data.meshes.new(knoten)
        me.from_pydata(verts, [], flaechen)
        me.update()
        obj = bpy.data.objects.new(knoten, me)
        bpy.context.scene.collection.objects.link(obj)
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.01)
        lose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=lose, context="VERTS")
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        vertexfarben(obj)
        h, t = daten[s]
        objekte.append((name, obj, h, t, anzahl))
    return objekte


def grenzen_min(grenzen, s, i, _):
    """Untere Zeile der Staffel s in Spalte i und ihrer Nachbarn (die Vierecke reichen über zwei Spalten)."""
    if s == 0:
        return 0
    n = len(grenzen)
    return min(grenzen[(i + j) % n][s - 1] for j in (-1, 0, 1))


def grenzen_max(grenzen, s, i, zeilen):
    if s == len(grenzen[0]):
        return zeilen - 1
    n = len(grenzen)
    return max(grenzen[(i + j) % n][s] for j in (-1, 0, 1))


def vertexfarben(obj):
    """COLOR_0: Wald, Fels, Tiefe, Höhe – aus der Neigung der fertigen Normalen."""
    me = obj.data
    attr = me.color_attributes.new("Farbe", "FLOAT_COLOR", "POINT")
    for v in me.vertices:
        steil = 1.0 - v.normal.z                                                   # 0 eben, 0,36 bei 50°, 0,5 bei 60°
        band = 0.5 + 0.5 * noise.noise(Vector((v.co.x / 25.0, v.co.y / 25.0, v.co.z / 6.0)))
        fels = min(1.0, max(0.0, (steil - 0.38) / 0.16)) * (0.3 + 0.7 * band)
        aussen = Vector((v.co.x - MITTE[0], v.co.y + MITTE[1], 0)).normalized()
        if v.normal.x * aussen.x + v.normal.y * aussen.y > 0.25:                  # Rückseite (vom Spielfeld weg): kein Fels
            fels = 0.0
        lichtung = noise.noise(Vector((v.co.x / 90.0, v.co.y / 90.0, 7.7)))
        fuss = min(1.0, max(0.0, v.co.z / 12.0))
        wald = min(1.0, max(0.0, 0.95 - fels * 1.1 - max(0.0, lichtung - 0.25) * 1.8)) * (0.35 + 0.65 * fuss)
        pe = math.degrees(math.atan2(v.co.x - MITTE[0], v.co.y + MITTE[1])) % 360
        r = math.hypot(v.co.x - MITTE[0], v.co.y + MITTE[1]) - aussenkante(pe)
        attr.data[v.index].color = (wald, fels, min(1.0, max(0.0, r / RING_TIEFE)),
                                    min(1.0, max(0.0, v.co.z / 250.0)))
    me.color_attributes.active_color = attr


def material():
    """Für die Vorschau: Wald dunkelgrün, Fels hell (Trachyt), Wiesen am Fuß. Im Spiel ersetzt der Dunst-Shader es."""
    m = bpy.data.materials.new("Kulisse")
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.95
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "Farbe"
    trenn = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(attr.outputs["Color"], trenn.inputs[0])
    wiese = nt.nodes.new("ShaderNodeRGB")
    wiese.outputs[0].default_value = (0.20, 0.25, 0.10, 1)
    wald = nt.nodes.new("ShaderNodeRGB")
    wald.outputs[0].default_value = (0.045, 0.075, 0.035, 1)
    fels = nt.nodes.new("ShaderNodeRGB")
    fels.outputs[0].default_value = (0.42, 0.40, 0.35, 1)
    m1 = nt.nodes.new("ShaderNodeMix")
    m1.data_type = "RGBA"
    nt.links.new(trenn.outputs[0], m1.inputs["Factor"])
    nt.links.new(wiese.outputs[0], m1.inputs[6])
    nt.links.new(wald.outputs[0], m1.inputs[7])
    m2 = nt.nodes.new("ShaderNodeMix")
    m2.data_type = "RGBA"
    nt.links.new(trenn.outputs[1], m2.inputs["Factor"])
    nt.links.new(m1.outputs[2], m2.inputs[6])
    nt.links.new(fels.outputs[0], m2.inputs[7])
    nt.links.new(m2.outputs[2], bsdf.inputs["Base Color"])
    return m


def spielort(peilung, tiefe):
    x, y = punkt(peilung, tiefe)
    return x, -y                                                                   # Spiel: z = Süden


def hoehe_bei(obj, x, y):
    """Höhe des Netzes über (x, y) per Strahl von oben."""
    dg = bpy.context.evaluated_depsgraph_get()
    treffer, ort, *_ = bpy.context.scene.ray_cast(dg, Vector((x, y, 2000.0)), Vector((0, 0, -1)))
    return ort.z if treffer else 0.0


def berge_schreiben(profil, staffeln):
    """game/daten/berge.json: Gipfel und Gebiete mit echter Lage und Höhe und ihrem Ort im Spiel (für Beschriftungen)."""
    eintraege = []
    for art, liste in (("gipfel", profil["gipfel"]), ("gebiet", profil["gebiete"])):
        for g in liste:
            if art == "gipfel":
                st = next((s for s in profil["staffeln"] if s["von_km"] <= g["entfernung_km"] <= s["bis_km"] + 3.0),
                          profil["staffeln"][-1])["name"]
            else:
                st = g["staffel"]
            obj, hoehe, tiefe, anzahl = staffeln[st]
            i = int(round(g["peilung"] / 360.0 * anzahl)) % anzahl
            x, y = punkt(g["peilung"], tiefe[i])
            # Gipfel: genau an seiner Peilung auf dem Kamm; Gebiet: höchster Punkt in einem kleinen Fächer
            beste = (hoehe_bei(obj, x, y), x, y)
            for dp in ((-0.6, -0.3, 0.0, 0.3, 0.6) if art == "gebiet" else (0.0,)):
                for dt in (-12.0, 0.0, 12.0):
                    xx, yy = punkt(g["peilung"] + dp, tiefe[i] + dt)
                    z = hoehe_bei(obj, xx, yy)
                    if z > beste[0]:
                        beste = (z, xx, yy)
            e = {"name": g["name"], "art": art}
            if art == "gipfel":
                e["gebiet"] = g["gebiet"]
            e.update({"breite": g["breite"], "laenge": g["laenge"], "hoehe_nn": g["hoehe_nn"],
                      "peilung": g["peilung"], "entfernung_km": g["entfernung_km"], "staffel": st,
                      "spiel": [round(beste[1], 1), round(beste[0], 1), round(-beste[2], 1)]})
            eintraege.append(e)
    daten = {
        "beschreibung": "Berge der Kulisse um Köln (Modell assets/modelle/kulisse/kulisse_koeln.glb). Breite/Länge "
                        "WGS84, hoehe_nn aus dem DGM NRW (Datenlizenz Deutschland – Zero 2.0), peilung in Grad ab Norden, "
                        "entfernung_km von der Kartenmitte; spiel = Ort im Spiel (x, y, z in Metern, z = Süden) auf dem "
                        "Kamm des Modells, für eine Beschriftung.",
        "karte": "koeln",
        "modell": "kulisse/kulisse_koeln",
        "berge": eintraege,
    }
    with open(BERGE, "w", encoding="utf-8") as f:
        json.dump(daten, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("geschrieben:", os.path.relpath(BERGE, bs.WURZEL))
    return eintraege


def vorschau(ordner, schnell, berge):
    szene = bpy.context.scene
    boden = bpy.data.meshes.new("_Boden")
    a, b = AUSSEN_HALB
    mx, my = MITTE[0], -MITTE[1]
    boden.from_pydata([(mx - a - 900, my - b - 900, -0.5), (mx + a + 900, my - b - 900, -0.5),
                       (mx + a + 900, my + b + 900, -0.5), (mx - a - 900, my + b + 900, -0.5)], [], [(0, 1, 2, 3)])
    bo = bpy.data.objects.new("_Boden", boden)
    szene.collection.objects.link(bo)
    bo.data.materials.append(bs._hilfsstoff("_Wiese", (0.36, 0.38, 0.24)))
    feld = bpy.data.meshes.new("_Spielfeld")
    feld.from_pydata([(mx - 350, my - 225, 0.0), (mx + 350, my - 225, 0.0), (mx + 350, my + 225, 0.0),
                      (mx - 350, my + 225, 0.0)], [], [(0, 1, 2, 3)])
    fo = bpy.data.objects.new("_Spielfeld", feld)
    szene.collection.objects.link(fo)
    fo.data.materials.append(bs._hilfsstoff("_Feld", (0.55, 0.50, 0.38)))
    for g in berge:                                                                # rote Stäbe: Ort der Beschriftung
        if g["art"] != "gipfel":
            continue
        x, z, y = g["spiel"][0], g["spiel"][1], -g["spiel"][2]
        bpy.ops.mesh.primitive_cylinder_add(radius=2.5, depth=30, location=(x, y, z + 15))
        bpy.context.object.name = "_Marke_" + g["name"]
        bpy.context.object.data.materials.append(bs._hilfsstoff("_Marke", (0.85, 0.1, 0.1)))

    sonne = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
    sonne.data.energy = 3.0
    sonne.data.angle = math.radians(3)
    sonne.rotation_euler = (math.radians(50), 0, math.radians(200))
    szene.collection.objects.link(sonne)
    welt = bpy.data.worlds.new("Himmel")
    try:
        welt.use_nodes = True
    except AttributeError:
        pass
    welt.node_tree.nodes["Background"].inputs["Color"].default_value = (0.45, 0.52, 0.62, 1)
    szene.world = welt
    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    kam.data.type = "ORTHO"
    kam.data.clip_end = 6000
    szene.collection.objects.link(kam)
    szene.camera = kam
    szene.render.engine = "CYCLES"
    bs.cycles_geraet(szene)
    szene.cycles.samples = 16 if schnell else 64
    szene.cycles.use_denoising = True
    szene.view_settings.view_transform = "Standard"
    os.makedirs(ordner, exist_ok=True)
    bilder = []
    # Draufsicht und Spielblicke: Kamera 40° geneigt, vom Spielfeldrand zur Kante geschaut
    for name, ziel, peilung, neig, ausschnitt, groesse in (
            ("aufsicht", (mx, my, 0), 0.0, 89.9, 2400, (1400, 1200)),
            ("blick_suedost", None, 145.0, 40.0, 1100, (1600, 900)),
            ("blick_ost", None, 70.0, 40.0, 1100, (1600, 900)),
            ("blick_suedwest", None, 215.0, 40.0, 1100, (1600, 900)),
            ("blick_west", None, 265.0, 40.0, 1100, (1600, 900))):
        if ziel is None:                       # Blickpunkt am Spielfeldrand in dieser Richtung
            w = math.radians(peilung)
            ziel = (mx + math.sin(w) * (350 + 300), my + math.cos(w) * (225 + 300), 0)
        w = math.radians(peilung)
        n = math.radians(neig)
        richtung = Vector((math.sin(w) * math.cos(n), math.cos(w) * math.cos(n), -math.sin(n)))
        kam.location = Vector(ziel) - richtung * 3000
        kam.rotation_euler = richtung.to_track_quat("-Z", "Y").to_euler()
        kam.data.ortho_scale = ausschnitt
        szene.render.resolution_x, szene.render.resolution_y = groesse
        szene.render.resolution_percentage = 50 if schnell else 100
        pfad = os.path.join(ordner, f"{NAME}_{name}.png")
        szene.render.filepath = pfad
        bpy.ops.render.render(write_still=True)
        bilder.append(pfad)
    return bilder


def main():
    schnell, ohne_bilder, ordner = argumente()
    with open(PROFIL, encoding="utf-8") as f:
        profil = json.load(f)
    bs.neue_szene()
    m = material()
    staffeln = {}
    teile = []
    for name, obj, hoehe, tiefe, anzahl in ring_netze(profil):
        obj.data.materials.append(m)
        staffeln[name] = (obj, hoehe, tiefe, anzahl)
        teile.append(obj)
        dreiecke = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        print(f"{obj.name}: {dreiecke} Dreiecke, Kamm bis {max(hoehe):.0f} m")
    print("zusammen:", sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in teile), "Dreiecke")
    berge = berge_schreiben(profil, staffeln)
    pfad = bs.exportieren(NAME, teile)
    print("geschrieben:", os.path.relpath(pfad, bs.WURZEL))
    if not ohne_bilder:
        for p in vorschau(ordner, schnell, berge):
            print("Bild:", p)


if __name__ == "__main__":
    main()
