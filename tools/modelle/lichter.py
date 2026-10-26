"""Lichter der Stadt bei Nacht: Handlaterne, Fackel, Wandlaterne, Feuerkorb und Ewiges Licht (Baukasten).

    blender -b --python tools/modelle/lichter.py        oder
    python tools/modelle/lichter.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/gebaeude/lichter.glb und Bilder nach docs/modelle/ (lichter.png bei Tag, lichter-nacht.png).
Die Datei liegt in gebaeude/, weil es Ausstattung der Stadt ist wie die Marktstände (markt.py → marktstaende.glb); ein eigener
Ordner samt Eintrag in bausteine.unterordner wäre für fünf kleine Teile zu viel.

Fünf getrennte Teile (je ein Objekt, mit extras „teil“ und „sichtbar“ = aus, wie die Marktstände; das Spiel stellt sie, wo es sie
braucht). Maßstab wie figur_mensch.glb (Meter, Blender: Z oben, im Spiel Y oben). Ursprung und Richtung:
    Handlaterne   Ursprung am Bügel, wo die Hand greift; hängt 30 cm darunter. Vier Scheiben („Laternenlicht“), Eisenrahmen.
    Fackel        Ursprung in der Mitte der Hand-Umwicklung; Holzstab (−30 cm bis +32 cm), Pechkopf, Flamme bis +78 cm.
    Wandlaterne   Ursprung in der Mitte der Wandplatte (die Wand liegt bei y ≤ 0); der Arm ragt nach +Y (im Spiel −Z, also in der
                  Blickrichtung eines Ankers), die Laterne hängt am Ende, 55 cm unter der Platte.
    Feuerkorb     Ursprung am Fuß der Eisenstange auf dem Boden; Stange 1,85 m, Korb darauf, Flamme bis 2,45 m.
    EwigesLicht   Ursprung am Aufhängepunkt oben (Z nach unten), Kette 45 cm, rote Ampel darunter (Gesamtlänge 73 cm).
Zwei Materialien sind eigene Namen ohne Emission: „Flamme“ (offenes Feuer: Fackel, Feuerkorb) und „Laternenlicht“ (Scheiben der
Laternen und des Ewigen Lichts). Das Spiel schaltet Emission und Flackern selbst; die Nachtbilder tun es nur zur Vorschau.
Das Ewige Licht ist im Modell nur ein Laternenlicht: Das Spiel färbt es rot (eigener Knoten, eigene Materialüberschreibung).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "lichter"
bs.BIBLIOTHEK_JE_FLAECHE = True          # Modell-Texturen: Material-Bibliothek je Fläche (Material „Farben“)
bs.FARBEN.update({
    "Pech": (0.07, 0.055, 0.045),        # Pechwicklung am Kopf der Fackel
    "Flamme": (1.0, 0.55, 0.14),         # offenes Feuer; ohne Emission, das Spiel lässt es leuchten und flackern
    "Laternenlicht": (0.90, 0.72, 0.40),  # Horn- bzw. Glasscheibe der Laternen und der Ampel; ohne Emission wie „Flamme“
})
# Diese beiden bleiben beim Zusammenlegen eigene Materialien, damit das Spiel sie nach Namen findet
BEHALTEN = bs.BENANNTE_STOFFE | {"Flamme", "Laternenlicht"}
# Nachtbilder (nur Vorschau): Farbe und Stärke der Emission
bs.NACHTLICHT.update({"Flamme": ((1.0, 0.5, 0.12), 30.0), "Laternenlicht": ((1.0, 0.68, 0.28), 14.0)})

TEILE = ["Handlaterne", "Fackel", "Wandlaterne", "Feuerkorb", "EwigesLicht"]


def laterne(netz, st, mx, my, z0, b, h, dach):
    """Laterne: Bodenplatte, vier Pfosten, vier Scheiben, Deckplatte und Pyramidendach, aufgebaut ab `z0` (Unterkante).
    `b` Breite des Körpers, `h` Höhe zwischen den Platten, `dach` Höhe der Pyramide. Gibt die Höhe der Spitze zurück."""
    e, licht = st["Eisen"], st["Laternenlicht"]
    d = 0.18 * b                                  # Dicke der Platten
    p = b * 0.07                                  # Pfostenstärke
    za, zb = z0 + d, z0 + d + h
    netz.quader((mx, my, z0 + d / 2), (b * 1.04, b * 1.04, d), e)
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader((mx + sx * (b / 2 - p / 2), my + sy * (b / 2 - p / 2), (za + zb) / 2), (p, p, h), e)
    for n in ((1, 0), (-1, 0), (0, 1), (0, -1)):  # Scheiben zwischen den Pfosten, von außen sichtbar
        q = Vector((-n[1], n[0], 0)) * (b / 2 - p)
        m = Vector((mx + n[0] * (b / 2 - p * 0.4), my + n[1] * (b / 2 - p * 0.4), 0))
        netz.vieleck_nach([m - q + Vector((0, 0, za)), m + q + Vector((0, 0, za)), m + q + Vector((0, 0, zb)),
                           m - q + Vector((0, 0, zb))], (n[0], n[1], 0), licht)
    netz.quader((mx, my, zb + d * 0.4), (b * 1.14, b * 1.14, d * 0.8), e)
    zd = zb + d * 0.8
    spitze = Vector((mx, my, zd + dach))
    ecken = [Vector((mx + sx * b * 0.57, my + sy * b * 0.57, zd)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for k in range(4):
        a, c = ecken[k], ecken[(k + 1) % 4]
        netz.vieleck_nach([a, c, spitze], (a + c) / 2 - Vector((mx, my, zd)) + Vector((0, 0, 0.3)), e)
    return zd + dach


def handlaterne(st):
    netz = bs.Netz("Handlaterne", alle=True)
    spitze = laterne(netz, st, 0, 0, -0.30, 0.11, 0.17, 0.055)
    e = st["Eisen"]
    punkte = [(-0.06, -0.10), (-0.052, -0.045), (-0.022, -0.008), (0.022, -0.008), (0.052, -0.045), (0.06, -0.10)]
    for (x0, z0), (x1, z1) in zip(punkte, punkte[1:]):        # Bügel: Bogen über dem Dach, oben greift die Hand (z = 0)
        netz.stab((x0, 0, z0), (x1, 0, z1), 0.012, 0.012, e, mehr=0.008)
    return netz.objekt()


def fackel(st):
    netz = bs.Netz("Fackel", alle=True)
    nt.ast(netz, st, (0, 0, -0.30), (0, 0, 0.32), 0.024, 0.021, stoff="Holz", n=6)
    nt.ast(netz, st, (0, 0, -0.07), (0, 0, 0.07), 0.029, 0.029, stoff="Leder", n=6)          # Wicklung um die Hand
    nt.ast(netz, st, (0, 0, 0.27), (0, 0, 0.47), 0.026, 0.052, stoff="Pech", n=7)             # Pechkopf
    nt.ast(netz, st, (0, 0, 0.47), (0, 0, 0.50), 0.052, 0.040, stoff="Pech", n=7)
    nt.ast(netz, st, (0, 0, 0.50), (0, 0, 0.58), 0.046, 0.054, stoff="Flamme", n=6)
    lb.kegel(netz, (0, 0), 0.054, 0.58, 0.80, st["Flamme"], n=6)
    lb.kegel(netz, (0.035, 0.012), 0.03, 0.56, 0.69, st["Flamme"], n=5)
    return netz.objekt()


def wandlaterne(st):
    netz = bs.Netz("Wandlaterne")
    e = st["Eisen"]
    netz.quader((0, 0.018, 0), (0.12, 0.036, 0.26), e, ohne=("-y",))                          # Platte an der Wand
    for z in (-0.09, 0.09):
        netz.quader((0, 0.04, z), (0.03, 0.012, 0.03), e)                                      # Nagelköpfe
    netz.stab((0, 0.03, 0.04), (0, 0.58, 0.04), 0.045, 0.026, e, quer=(1, 0, 0))               # Arm
    netz.stab((0, 0.03, -0.22), (0, 0.40, 0.025), 0.026, 0.022, e, quer=(1, 0, 0))             # Strebe
    netz.stab((0, 0.58, 0.04), (0, 0.58, -0.02), 0.022, 0.022, e, mehr=0.0)                    # Haken
    spitze = laterne(netz, st, 0, 0.58, -0.62, 0.20, 0.28, 0.10)
    netz.stab((0, 0.58, -0.02), (0, 0.58, spitze), 0.014, 0.014, e)                            # Aufhängung
    return netz.objekt()


def feuerkorb(st):
    netz = bs.Netz("Feuerkorb")
    e = st["Eisen"]
    for k in range(3):                                                                         # drei Füße
        w = 2 * math.pi * k / 3 + 0.5
        netz.stab((0, 0, 0.55), (0.30 * math.cos(w), 0.30 * math.sin(w), 0.0), 0.05, 0.05, e, mehr=0.03)
    nt.ast(netz, st, (0, 0, 0), (0, 0, 1.85), 0.05, 0.042, stoff="Eisen", n=6)                 # Stange
    n = 6
    boden, rand = 1.84, 2.18
    for k in range(n):                                                                         # Korbstäbe, oben weiter
        w0, w1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        unten = Vector((0.15 * math.cos(w0), 0.15 * math.sin(w0), boden))
        oben = Vector((0.32 * math.cos(w0), 0.32 * math.sin(w0), rand))
        oben2 = Vector((0.32 * math.cos(w1), 0.32 * math.sin(w1), rand))
        netz.stab(unten, oben, 0.035, 0.035, e)
        netz.stab(oben + Vector((0, 0, 0.012)), oben2 + Vector((0, 0, 0.012)), 0.045, 0.045, e)   # Rand
    nt.ast(netz, st, (0, 0, boden - 0.02), (0, 0, boden + 0.03), 0.17, 0.17, stoff="Eisen", n=6)   # Boden des Korbs
    nt.ast(netz, st, (0, 0, boden + 0.03), (0, 0, boden + 0.22), 0.16, 0.26, stoff="Kohle", n=6)   # Kohlehaufen
    lb.kegel(netz, (0, 0), 0.22, boden + 0.18, 2.47, st["Flamme"], n=6)
    lb.kegel(netz, (0.10, 0.04), 0.13, boden + 0.18, 2.28, st["Flamme"], n=5)
    lb.kegel(netz, (-0.09, -0.06), 0.12, boden + 0.18, 2.22, st["Flamme"], n=5)
    return netz.objekt()


def ewiges_licht(st):
    netz = bs.Netz("EwigesLicht", alle=True)
    for i in range(5):                                                                         # Kettenglieder, abwechselnd quer
        netz.stab((0, 0, -0.09 * i + 0.01), (0, 0, -0.09 * (i + 1) + 0.025), 0.016, 0.016, st["Eisen"],
                  quer=(1, 0, 0) if i % 2 else (0, 1, 0))
    gold = st["Gold"]
    lb.kegel(netz, (0, 0), 0.065, -0.52, -0.44, gold, n=6)                                     # Dach der Ampel
    nt.ast(netz, st, (0, 0, -0.52), (0, 0, -0.68), 0.062, 0.046, stoff="Laternenlicht", n=6)   # Glas
    nt.ast(netz, st, (0, 0, -0.68), (0, 0, -0.73), 0.046, 0.02, stoff="Gold", n=6)             # Fuß
    nt.ast(netz, st, (0, 0, -0.525), (0, 0, -0.505), 0.068, 0.068, stoff="Gold", n=6)          # Rand oben
    return netz.objekt()


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    objekte = [handlaterne(st), fackel(st), wandlaterne(st), feuerkorb(st), ewiges_licht(st)]
    for o in objekte:
        o["teil"] = True
        o["sichtbar"] = False
    return objekte


def vorschau(objekte, schnell):
    """Alle fünf nebeneinander (Wandlaterne an einer Mauer), bei Tag und bei Nacht."""
    import bpy
    nachbau = {o.name: o for o in objekte}
    orte = {"Feuerkorb": -2.6, "Wandlaterne": -0.9, "Handlaterne": 0.9, "Fackel": 1.7, "EwigesLicht": 2.8}
    sichtbar = []
    for name, d in orte.items():
        o = nachbau[name]
        o.location = (d * 0.7071, d * 0.7071, 0.0)
        sichtbar.append(o)
    # Teile, die über dem Boden schweben, stellt die Vorschau auf Stützen (nur Bild)
    wand = bs.Netz("_Wand")
    wand.quader((0, -0.3, 1.1), (1.6, 0.6, 2.4), bs._hilfsstoff("_Mauer", (0.50, 0.46, 0.40)))
    w = wand.objekt()
    w.rotation_euler = (0, 0, math.radians(-135))
    w.location = nachbau["Wandlaterne"].location + Vector((0, 0, 0.0))
    nachbau["Wandlaterne"].rotation_euler = (0, 0, math.radians(-135))
    nachbau["Wandlaterne"].location.z = 1.9
    stuetzen = bs.Netz("_Stuetzen")
    sm = bs._hilfsstoff("_Halter", (0.30, 0.22, 0.14))
    stuetzen.quader((orte["Handlaterne"] * 0.7071, orte["Handlaterne"] * 0.7071, 0.45), (0.3, 0.3, 0.9), sm)
    stuetzen.quader((orte["Fackel"] * 0.7071, orte["Fackel"] * 0.7071, 0.55), (0.3, 0.3, 1.1), sm)
    stuetzen.objekt()
    nachbau["Handlaterne"].location.z = 0.9 + 0.30
    nachbau["Fackel"].location.z = 1.1 + 0.30
    nachbau["EwigesLicht"].location.z = 2.0
    bs.vorschau_buehne(None, ziel_hoehe=1.3, ausschnitt=7.5, schnell=schnell)
    szene = bpy.context.scene
    szene.render.resolution_x, szene.render.resolution_y = 1600, 900
    print(bs.rendern(NAME, lambda o: o.name in nachbau))
    # Ewiges Licht rot (das Spiel überschreibt das Material des Knotens)
    rot = bpy.data.materials.new("_Rotlicht")
    rot.use_nodes = True
    p = rot.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*bs._linear((0.8, 0.1, 0.08)), 1.0)
    p.inputs["Emission Color"].default_value = (*bs._linear((1.0, 0.1, 0.06)), 1.0)
    p.inputs["Emission Strength"].default_value = 10.0
    obj = nachbau["EwigesLicht"]
    for i, m in enumerate(obj.data.materials):
        if m and m.name.split(".")[0] == "Laternenlicht":
            obj.data.materials[i] = rot
    print(bs.nachtbild(NAME, lambda o: o.name in nachbau))


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte, einzeln=True)
    vorher, nachher = bs.farben_zusammenlegen(objekte, behalten=BEHALTEN)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    vorschau(objekte, "--schnell" in sys.argv)


if __name__ == "__main__":
    main()
