"""Vorschau der neuen Einrichtung (personal_einrichtung.py): jedes Stück vor einer Wandecke, einmal aus der Spielkamera des Innenraums (Sichtfeld 50°,
Auge etwa 2,3 m hoch, 3–5 m entfernt) und einmal nah; Materialien mit den Texturen der Material-Bibliothek, wie das Spiel sie zeigt.

    blender -b --factory-startup --python tools/modelle/personal_vorschau.py -- [--stueck=Schleifstein,Athanor] [--aus=Ordner] [--schnell]

Schreibt <Name>.png (links Spielkamera, rechts nah) und <Name>_wand.png nach `--aus` (Vorgabe docs/modelle/personal_einrichtung/) und zählt die Dreiecke
(personal_vorschau.csv). Nur Vorschau, es wird nichts nach game/assets geschrieben.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import personal_einrichtung as pe  # noqa: E402

SCHNELL = "--schnell" in sys.argv


def arg(name, vorgabe=None):
    for a in sys.argv:
        if a.startswith(f"--{name}="):
            return a.split("=", 1)[1]
    return vorgabe


AUS = arg("aus", os.path.join(bs.WURZEL, "docs", "modelle", "personal_einrichtung"))
os.makedirs(AUS, exist_ok=True)
bs.VORSCHAU = AUS


def blick_auf(kam, auge, ziel):
    auge, ziel = Vector(auge), Vector(ziel)
    vor = (ziel - auge).normalized()
    rechts = vor.cross(Vector((0, 0, 1))).normalized()
    oben = rechts.cross(vor)
    m = Matrix((rechts, oben, -vor)).transposed().to_4x4()
    m.translation = auge
    kam.matrix_world = m


def buehne(breite, tiefe=3.0, hoch=3.0, wandhoehe=3.2, ohne_decke=True):
    """Boden, Rückwand und zwei Seitenwände (Putz, unten Bruchstein), Licht wie ein Fenster von links vorn und eine Lampe."""
    import bpy
    szene = bpy.context.scene
    st = bs.Stoffe()
    wand = bs.Netz("Raum_Werk", alle=True)
    hw = breite / 2 + 1.0
    boden = bs.Netz("Raum_Boden")
    boden.quader((0, tiefe / 2 - 0.2, -0.05), (2 * hw, tiefe + 1.6, 0.1), st["Bruchstein dunkel"])
    # Backsteinboden im Läuferverband (Reihen um halbe Steine versetzt)
    for j in range(int((tiefe + 1.6) / 0.15)):
        for i in range(int(2 * hw / 0.3) + 1):
            x = -hw + 0.15 + i * 0.3 + (0.15 if j % 2 else 0.0)
            if abs(x) > hw - 0.14:
                continue
            boden.quader((x, -0.2 + 0.075 + j * 0.15, 0.002), (0.285, 0.135, 0.004), st["Dachziegel"] if (i + j) % 3 else st["Firstziegel"], ohne=("-z",))
    wand.vieleck_nach([(-hw, 0, 0), (hw, 0, 0), (hw, 0, wandhoehe), (-hw, 0, wandhoehe)], (0, 1, 0), st["Putz"])
    for s in (-1, 1):
        wand.vieleck_nach([(s * hw, 0, 0), (s * hw, tiefe + 1.4, 0), (s * hw, tiefe + 1.4, wandhoehe), (s * hw, 0, wandhoehe)], (-s, 0, 0), st["Putz"])
    wand.quader((0, 0.02, 0.17), (2 * hw, 0.04, 0.34), st["Bruchstein"], ohne=("-y",))
    for s in (-1, 1):
        wand.quader((s * (hw - 0.02), tiefe / 2, 0.17), (0.04, tiefe + 1.4, 0.34), st["Bruchstein"], ohne=("-x", "+x"))
    wand.quader((0, 0.05, wandhoehe - 0.1), (2 * hw, 0.1, 0.2), st["Fachwerk"], ohne=("-y",))
    wand.objekt()
    boden.objekt()
    return szene, st, hw


def welt_und_licht(szene, cx, d):
    import bpy
    szene.render.engine = "CYCLES"
    bs.cycles_geraet(szene)
    szene.cycles.samples = 24 if SCHNELL else 72
    szene.cycles.use_denoising = True
    szene.view_settings.view_transform = "Standard"
    szene.view_settings.exposure = -0.7
    welt = bpy.data.worlds.new("Himmel")
    welt.use_nodes = True
    welt.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.44, 0.5, 1)
    welt.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.3
    szene.world = welt
    for name, ort, energie, farbe, groesse in (("Fenster", (cx - 2.2, 2.4, 2.6), 120, (0.9, 0.94, 1.0), 2.0),
                                              ("Fuell", (cx + 1.4, 3.2, 3.0), 50, (1.0, 0.9, 0.75), 3.0),
                                              ("Lampe", (cx, 1.4, 2.7), 6, (1.0, 0.8, 0.55), 0.4)):
        l = bpy.data.lights.new(name, "AREA" if name != "Lampe" else "POINT")
        l.energy, l.color = energie, farbe
        if name != "Lampe":
            l.size = groesse
        o = bpy.data.objects.new(name, l)
        o.location = ort
        szene.collection.objects.link(o)
        v = Vector((cx, 0.8, 0.9)) - Vector(ort)
        o.rotation_euler = v.to_track_quat("-Z", "Y").to_euler()


def ein_stueck(name):
    import bpy
    bs.neue_szene()
    funktion, d = pe.ALLE[name]
    breite = {"Stube": 3.4, "Gewoelbekeller": 3.6, "Lehrlingsstube": 3.6, "Vorratskammer": 2.6, "Bibliothek": 3.0, "Kueche": 2.6}.get(name, 2.4)
    szene, st, hw = buehne(breite)
    netz = bs.Netz("Einrichtung_" + name, alle=True)
    M = Matrix.Identity(4)
    zusatz = {}
    funktion(netz, st, M)
    netz.objekt()
    if name == "Lehrlingsstube":
        pass
    dreiecke = netz.dreiecke()
    cx = 0.0
    if name in ("Lehrlingsstube",):
        cx = 0.35
    if name in ("Stube",):
        cx = 0.0
    welt_und_licht(szene, cx, d)
    # texturieren wie im Spiel (Material-Bibliothek)
    try:
        import fuellung_vorschau as fv
        fv.texturieren()
    except Exception as fehler:  # noqa: BLE001
        print("keine Texturen:", fehler)
    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    kam.data.sensor_fit = "VERTICAL"
    kam.data.angle_y = math.radians(50)
    kam.data.clip_start = 0.05
    szene.collection.objects.link(kam)
    szene.camera = kam
    szene.render.resolution_x, szene.render.resolution_y = 960, 540
    szene.render.resolution_percentage = 100
    bilder = []
    hoehe_mitte = 0.9 if name not in ("Hauswappen",) else 1.0
    ziele = {
        "spiel": ((cx + 0.30 * d, 0.95 * d + 1.7, 2.35 + 0.06 * d), (cx, 0.55, hoehe_mitte)),
        "nah": ((cx + 0.35 * d, 0.55 * d + 0.6, 1.55), (cx, 0.5, hoehe_mitte + 0.1)),
    }
    for art, (auge, ziel) in ziele.items():
        blick_auf(kam, auge, ziel)
        bilder.append(bs.rendern_einzeln(f"_{name}_{art}", f"{name} – {'Spielkamera' if art == 'spiel' else 'nah'}"))
    pfad = bs.zusammensetzen(name, bilder, spalten=1)
    return name, dreiecke, pfad


def main():
    namen = (arg("stueck") or ",".join(pe.ALLE)).split(",")
    zeilen = []
    for n in namen:
        name, dreiecke, pfad = ein_stueck(n)
        print(f"{name}: {dreiecke} Dreiecke → {pfad}")
        zeilen.append(f"{name},{dreiecke}")
    with open(os.path.join(AUS, "personal_vorschau.csv"), "a", encoding="utf-8") as f:
        f.write("\n".join(zeilen) + "\n")


main()
