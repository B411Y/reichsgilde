"""Rheinschiff „Niederländer“ (zweiter Schiffstyp neben dem Oberländer, oberlaender.py).

    blender -b --python tools/modelle/niederlaender.py -- [--schnell] [--ohne-bilder] [--seite]

Schreibt game/assets/modelle/wasser/wasser_niederlaender.glb und das Vorschaubild docs/modelle/wasser_niederlaender.png.

Niederländer: Frachtschiff der Rheinschiffer unterhalb von Köln um 1400, 17 m × 5,2 m (Maße plausibel, nicht belegt), bauchig und gedrungen:
volle Breite fast bis zum Bug, runde Kimm, ein Kiel unter dem Boden, kein hochgezogener Bug, niedrigerer Dollbord (liegt tiefer im Wasser),
hinten ein Spiegel mit kleinem Achterdeck und einer kleinen Hütte, vorn ein Vordeck mit dem kurzen Mast und einem breiten Rahsegel (Treidelmast, steht immer; der
Laderaum liegt dahinter), Heckruder mit Pinne.

Die Bausteine kommen aus oberlaender.py (Klinkerplanken, Rumpfbau, Segel, Laufplanke, Wimpel, Ladungsstücke): Dieses Skript setzt dort Maße und
Querschnitt und ruft dieselben Funktionen auf. Knoten und Anker heißen wie beim Oberländer (Schiff, Mast, Takelage, Segel_Gesetzt, Segel_Gerefft,
Fahne, Laufplanke, Ladung_1–18, Anker_Bug/Heck/Kai/Planke/Schiffer, Verdraenger), das Spiel behandelt beide gleich; zusätzlich ein Knoten Kiel.
Die Ladung hat dieselben Bündel und Stückarten je Nummer wie beim Oberländer (der Stapel am Steg gilt für beide Schiffe), nur andere x.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import oberlaender as ob  # noqa: E402
import wasser as ws  # noqa: E402

LAENGE, BREITE = 17.0, 5.2
HALB = LAENGE / 2
BORD = 0.80             # etwas niedriger als beim Oberländer
TIEF = 0.85             # tiefer Tiefgang, dazu der Kiel
KIEL = 0.2


def profil(x):
    """Querschnitt an der Stelle x: (halbe Breite am Dollbord, halbe Breite am Boden, Höhe Boden, Höhe Dollbord)."""
    hinten = max(0.0, (-4.5 - x) / 4.0)                    # 0 … 1 zum Spiegel
    vorn = max(0.0, (x - 3.6) / 4.9)                       # 0 … 1 zum Bug
    w = BREITE / 2
    if hinten > 0:
        w = w - (w - 1.6) * hinten ** 2
    if vorn > 0:
        w = 0.14 + (BREITE / 2 - 0.14) * (1 - vorn ** 2.6)  # voller Bug, erst kurz vor dem Steven schmal
    b = w * (0.74 - 0.16 * vorn ** 1.2)                    # runde Kimm: der Boden ist schmaler als das Dollbord
    zb = -TIEF + 0.9 * (max(0.0, (x - 3.6) / 4.9)) ** 2.2 + 0.5 * hinten ** 2
    g = BORD + 0.35 * vorn ** 2 + 0.3 * hinten ** 1.5      # kaum Sprung
    return w, b, zb, g


def einrichten():
    """Maße und Einbauten des Oberländers durch die des Niederländers ersetzen (die Funktionen in oberlaender.py lesen sie zur Laufzeit)."""
    ob.LAENGE, ob.BREITE, ob.HALB = LAENGE, BREITE, HALB
    ob.BORD, ob.TIEF, ob.DECK = BORD, TIEF, 0.9
    ob.MAST, ob.MAST_HOCH, ob.RAH = 7.0, 9.2, 7.6
    ob.HUETTE = (-7.1, -4.9)
    ob.SCHOTTEN = (-4.7, 5.6)
    ob.SPANTEN = [-3.8 + 1.5 * i for i in range(7)]
    ob.DUCHTEN = (-4.3, 0.0, 5.3)
    ob.FUGEN = range(-4, 6)
    ob.SCHIFFER_X = -HALB + 0.6
    ob.profil = profil


# Sechs Bündel wie beim Oberländer (Arten und Nummern gleich), nur andere x; in der Mitte bleibt die Lücke bei der Laufplanke
BUENDEL_X = [3.9, 2.4, 0.9, -0.9, -2.4, -3.9]


def ladung_bauen(st):
    objekte = []
    for nr, p in enumerate(ob.ladungsplan(BUENDEL_X, ob.BODEN), 1):
        netz = bs.Netz(f"Ladung_{nr}")
        ob.stueck(netz, st, p["art"], p["x"], p["y"], p["z"], nr)
        objekte.append(netz.objekt())
    return objekte


def kiel(st):
    """Kiel unter dem Boden vom Vorder- bis zum Hintersteven, vorn und hinten angespitzt hochgezogen."""
    netz = bs.Netz("Kiel")
    z = -TIEF - KIEL / 2 + 0.05
    netz.stab((-HALB + 1.2, 0, z), (HALB - 1.6, 0, z), 0.2, KIEL, st["Holz alt"], quer=(0, 1, 0))
    netz.stab((HALB - 1.6, 0, z), (HALB - 0.2, 0, profil(HALB - 0.2)[2] + 0.1), 0.18, 0.16, st["Holz alt"], quer=(0, 1, 0))
    netz.stab((-HALB + 1.2, 0, z), (-HALB + 0.1, 0, profil(-HALB + 0.1)[2] + 0.1), 0.18, 0.16, st["Holz alt"], quer=(0, 1, 0))
    return netz.objekt()


def bauen(st):
    einrichten()
    rumpf_obj = ob.rumpf(st)
    teile = [rumpf_obj, ob.verdraenger(st, profil, HALB), kiel(st)] + [n.objekt() for n in ob._MASTTEILE] + [ob.segel_gesetzt(st), ob.segel_gerefft(st), ob.wimpel(st)]
    teile += ladung_bauen(st)
    planke = ob.laufplanke(st)
    w, _, _, g = profil(ob.ANKER_X)
    planke.location = (ob.ANKER_X, -w, g)
    teile.append(planke)
    teile += [bs.anker("Anker_Bug", (HALB + 0.1, 0, 0), 270), bs.anker("Anker_Heck", (-HALB - 0.1, 0, 0), 90),
              bs.anker("Anker_Kai", (ob.ANKER_X, -BREITE / 2 - 0.3, 0), 0),
              bs.anker("Anker_Planke", (ob.ANKER_X, -w - ob.PLANKE, BORD - 0.05), 0),
              bs.anker("Anker_Schiffer", (ob.SCHIFFER_X, 0.35, ob.DECK), 270)]
    return teile


def vorschau(objekte, schnell):
    ws.buehne(22.0, 2.5, schnell)
    ws.gelaende([(-60, -3.0), (60, -3.0)], (-60, 60, 0.0), achse="y")
    zeigen = {o for o in objekte if o.name not in ("Segel_Gerefft", "Verdraenger")} | {k for o in objekte for k in o.children_recursive}
    ws.stempel("Niederländer: Segel gesetzt, Ladung 1–18, Laufplanke, Wimpel")
    a = bs.rendern("_wasser_niederlaender_a", lambda o: o in zeigen)
    zeigen = {o for o in objekte if o.name not in ("Segel_Gesetzt", "Fahne", "Verdraenger") and not o.name.startswith("Ladung_")}
    ws.stempel("im Hafen: Segel gerefft, leer")
    b = bs.rendern("_wasser_niederlaender_b", lambda o: o in zeigen, 2)
    if "--seite" in sys.argv:
        ws.stempel("Niederländer, von der Seite")
        zeigen = {o for o in objekte if o.name not in ("Segel_Gerefft", "Verdraenger")} | {k for o in objekte for k in o.children_recursive}
        bs.rendern("_niederlaender_seite", lambda o: o in zeigen, 3)
    return ws.nebeneinander("wasser_niederlaender", [a, b])


def main():
    schnell = "--schnell" in sys.argv
    name = "wasser_niederlaender"
    bs.neue_szene()
    st = bs.Stoffe()
    objekte = bauen(st)
    netze = [o for o in objekte if o.type == "MESH"]
    alle = objekte + [k for o in objekte for k in o.children_recursive if k not in objekte]
    bs.bericht(name, [o for o in alle if o.type == "MESH"] + [o for o in alle if o.type != "MESH"], einzeln=True)
    bs.BIBLIOTHEK_JE_FLAECHE = True
    import hafenbauten as hb  # noqa: E402
    vorher, nachher = bs.farben_zusammenlegen([o for o in alle if o.type == "MESH"], behalten=hb.BEHALTEN)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren(name, objekte))
    if "--ohne-bilder" not in sys.argv:
        print(vorschau(netze + [o for o in objekte if o.type != "MESH"], schnell))


if __name__ == "__main__":
    main()
