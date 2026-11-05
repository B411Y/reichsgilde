"""Hafensteg: hölzerne Landungsbrücke (T-Steg) für Häfen mit Schiffsanleger (Köln Rheinhafen, Greybox-Hafen).

    blender -b --python tools/modelle/hafensteg.py -- [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/wasser/wasser_hafensteg.glb und das Vorschaubild docs/modelle/wasser_hafensteg.png.
Maße wie beim Hafen (wasser.py): z = 0 ist die Uferkante, die Bohlen liegen auf KAI = 0,25 m, das Wasser bei −1,0 m.
Blender: Land bei −Y, Wasser bei +Y (im Spiel Wasser bei −Z). Ursprung auf der Uferkante in der Mitte.

    Hafensteg   Zugang 2,4 m breit von y = −0,6 (auf dem Kai) bis y = 8,0, dann der Kopf 16 m × 2,4 m quer dazu (x −8…8, y 8,0…10,4).
                Bohlen auf Streckbalken und Jochen, Pfahlpaare im Wasser (bis −4,5 m), Saumbalken, Reibepfähle an der Seeseite,
                vier Poller am Kopf, Festmacherringe. Insgesamt ragt er 10,4 m ins Wasser.
    Anker_Ufer (Mitte der Uferkante, Blick aufs Wasser), Anker_Liegeplatz (Seekante des Kopfes in der Mitte, y = 10,4, Blick aufs
    Wasser: dort liegt die Bordwand des Schiffes, 0,4 m Abstand), Anker_Planke (auf dem Kopf, 1,2 m hinter der Seekante).
Keine Hausfarbe, keine Zustände (gehört der Stadt); Prüfung mit `pruefen.py <glb> --wasser`.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import wasser as ws  # noqa: E402
from bausteine import Vector  # noqa: E402

KAI, FUSS = ws.KAI, ws.FUSS
HALB = 1.2                  # halbe Breite des Zugangs und des Kopfes
ZUGANG = 8.0                # Ende des Zugangs (Kopf beginnt)
KOPF_BIS = ZUGANG + 2 * HALB    # Seekante des Kopfes = 10,4 m
KOPF_X = 8.0                # halbe Länge des Kopfes


def bohlen_quer(netz, st, y0, y1, x0, x1, saat, dicke=0.08, breite=0.333):
    """Bohlen quer zum Zugang (entlang X), von y0 bis y1 hintereinander, Oberkante KAI."""
    import random
    zf = random.Random(saat)
    stoffe = ("Holz alt", "Holz", "Holz alt", "Bretter", "Holz", "Holz alt")
    n = max(1, round((y1 - y0) / breite))
    b = (y1 - y0) / n
    for k in range(n):
        ya, yb = y0 + k * b + 0.012, y0 + (k + 1) * b - 0.012
        z = KAI + zf.uniform(-0.014, 0.004)
        ws.kasten(netz, st, x0, x1, ya, yb, z - dicke, z, stoffe[(k * 5 + saat) % len(stoffe)], ohne=("-z",))


def hafensteg(st):
    netz = bs.Netz("Hafensteg")
    # Decks: Zugang (Bohlen quer) und Kopf (Bohlen entlang Y, quer zum Kopf)
    bohlen_quer(netz, st, -0.6, ZUGANG, -HALB, HALB, saat=11)
    ws.bohlen(netz, st, -KOPF_X, KOPF_X, ZUGANG, KOPF_BIS, lambda x: KAI, saat=17)
    # Streckbalken unter dem Zugang (entlang Y), Querbalken unter dem Kopf
    for x in (-0.8, 0.0, 0.8):
        ws.kasten(netz, st, x - 0.1, x + 0.1, -0.6, ZUGANG, KAI - 0.34, KAI - 0.05, "Holz", ohne=("-z",))
    for y in (ZUGANG + 0.4, ZUGANG + 1.2, ZUGANG + 2.0):
        ws.kasten(netz, st, -KOPF_X, KOPF_X, y - 0.1, y + 0.1, KAI - 0.34, KAI - 0.05, "Holz", ohne=("-z",))
    # Saumbalken (Schrammbord) rundum
    for s in (-1, 1):
        ws.kasten(netz, st, s * HALB - 0.1 if s > 0 else -HALB - 0.1 + 0.0, s * HALB + 0.1 if s > 0 else -HALB + 0.1, -0.6, ZUGANG - 0.1, KAI - 0.3, KAI + 0.1, "Holz", ohne=("-z",))
    ws.kasten(netz, st, -KOPF_X, KOPF_X, KOPF_BIS - 0.1, KOPF_BIS + 0.1, KAI - 0.3, KAI + 0.12, "Holz", ohne=("-z",))
    ws.kasten(netz, st, -KOPF_X, KOPF_X, ZUGANG - 0.1 + 0.0, ZUGANG + 0.1, KAI - 0.3, KAI + 0.09, "Holz", ohne=("-z",))
    for s in (-1, 1):
        ws.kasten(netz, st, s * KOPF_X - 0.1, s * KOPF_X + 0.1, ZUGANG, KOPF_BIS, KAI - 0.3, KAI + 0.1, "Holz", ohne=("-z",))
    # Schwelle am Land
    ws.kasten(netz, st, -HALB - 0.2, HALB + 0.2, -0.9, -0.5, -0.1, KAI - 0.08, "Holz alt", ohne=("-z",))
    # Joche des Zugangs: Pfahlpaare alle 2,5 m mit Querholm
    for y in (1.0, 3.5, 6.0):
        ws.kasten(netz, st, -HALB - 0.1, HALB + 0.1, y - 0.14, y + 0.14, KAI - 0.62, KAI - 0.30, "Holz alt", ohne=("-z",))
        for x in (-HALB + 0.15, HALB - 0.15):
            ws.pfahl(netz, st, x, y, FUSS, KAI - 0.34, r=0.16, n=7)
    # Pfähle des Kopfes: alle 2,67 m in zwei Reihen, die Seeseite ragt als Reibepfahl über das Deck
    xs = [-KOPF_X + 2.0 * KOPF_X / 6 * k for k in range(7)]
    for x in xs:
        ws.kasten(netz, st, x - 0.14, x + 0.14, ZUGANG, KOPF_BIS, KAI - 0.62, KAI - 0.30, "Holz alt", ohne=("-z",))
        ws.pfahl(netz, st, x, ZUGANG + 0.15, FUSS, KAI - 0.34, r=0.16, n=7)
        ws.pfahl(netz, st, x, KOPF_BIS + 0.12, FUSS, KAI + 0.55, r=0.18, n=8, kopf="Scheitholz")
    # Poller auf dem Kopf nahe der Seekante
    for x in (-5.4, -1.6, 1.6, 5.4):
        ws.pfahl(netz, st, x, KOPF_BIS - 0.55, KAI, KAI + 0.5, r=0.17, n=8, kopf="Scheitholz")
        ws.kasten(netz, st, x - 0.2, x + 0.2, KOPF_BIS - 0.75, KOPF_BIS - 0.35, KAI + 0.3, KAI + 0.38, "Eisen")
    # Festmacherringe an der Seekante
    for x in (-3.5, 3.5):
        ws.walze(netz, st, KOPF_BIS + 0.12, KAI - 0.05, 0.08, x - 0.03, x + 0.03, "Eisen", n=8)
    # Pfosten mit Laterne am Landende
    for s in (-1, 1):
        ws.pfahl(netz, st, s * (HALB - 0.1), 0.2, KAI, KAI + 1.1, r=0.1, n=6, kopf="Scheitholz")
    return [netz.objekt()]


def bauen(st):
    teile = hafensteg(st)
    anker = [bs.anker("Anker_Ufer", (0, 0, KAI), 0), bs.anker("Anker_Liegeplatz", (0, KOPF_BIS, KAI), 0),
             bs.anker("Anker_Planke", (0, KOPF_BIS - 1.2, KAI), 0)]
    return teile + anker


def vorschau(objekte, schnell):
    ws.buehne(30.0, 0.0, schnell)
    # Kai links (Land bei −Y), Wasser ab y = 0 (Spundwand), Vorschau-Hilfsgelände mit dem Wasser bei −1 m
    ws.gelaende([(-60, 0), (0, 0), (0.2, -1.6), (60, -1.6)], (0, 60), achse="y")
    import bpy
    for o in objekte:
        if o.parent is None and o.type == "MESH":
            o.location.y -= 3.0
    for n in ("_Gelaende", "_Wasser"):
        bpy.data.objects[n].location.y -= 3.0
    alles = set(objekte) | {k for o in objekte for k in o.children_recursive}
    bilder = [bs.rendern("wasser_hafensteg", lambda o: o in alles)]
    if not schnell:
        szene = bpy.context.scene
        p = szene.render.resolution_percentage
        szene.render.resolution_percentage = p // 2
        teile = []
        for k, text in ((0, "Blick von vorne rechts"), (1, "von hinten rechts"), (3, "von vorne links"), (2, "von hinten links")):
            ws.stempel(text)
            teile.append(bs.rendern(f"_wasser_hafensteg-blick{k}", lambda o: o in alles, k))
        szene.render.resolution_percentage = p
        ws.stempel(None)
        bilder.append(bs.zusammensetzen("wasser_hafensteg-rundum", teile, spalten=2))
    return bilder


def main():
    schnell = "--schnell" in sys.argv
    bs.neue_szene()
    st = bs.Stoffe()
    objekte = bauen(st)
    netze = [o for o in objekte if o.type == "MESH"]
    bs.bericht("wasser_hafensteg", netze + [o for o in objekte if o.type != "MESH"], einzeln=True)
    bs.BIBLIOTHEK_JE_FLAECHE = True
    vorher, nachher = bs.farben_zusammenlegen(netze, behalten=ws.BEHALTEN)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren("wasser_hafensteg", objekte))
    if "--ohne-bilder" not in sys.argv:
        print(vorschau(objekte, schnell))


if __name__ == "__main__":
    main()
