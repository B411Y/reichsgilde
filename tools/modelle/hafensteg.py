"""Hafensteg: hölzerne Landungsbrücke mit Karrenrampe und Tretradkran für Häfen mit Schiffsanleger (Köln Rheinhafen, Greybox-Hafen).

    blender -b --python tools/modelle/hafensteg.py -- [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/wasser/wasser_hafensteg.glb und das Vorschaubild docs/modelle/wasser_hafensteg.png.
Maße wie beim Hafen (wasser.py): z = 0 ist die Uferkante, die Kaibohlen liegen auf KAI = 0,25 m, das Wasser bei −1,0 m.
Blender: Land bei −Y, Wasser bei +Y (im Spiel Wasser bei −Z). Ursprung auf der Uferkante in der Mitte.

    Hafensteg   Das Deck des Kopfes liegt 0,45 m unter dem Kai (DECK = −0,2 m, auf Höhe des Dollbords der Schiffe, wie die Landebrücken
                am Rhein bei wechselndem Wasserstand). Vom Kai führt eine 3 m breite Karrenrampe aus Bohlen mit Trittleisten
                (9 % Gefälle, von y = −0,6 bis 5,0) hinunter, dann flach bis y = 8,0; dort der Kopf 18 m × 2,4 m quer dazu
                (x −9…9, y 8,0…10,4). Bohlen auf Streckbalken und Jochen, Pfahlpaare im Wasser (bis −4,5 m), Saumbalken,
                Reibepfähle an der Seeseite (keiner bei x = 0, dort liegt die Laufplanke), vier Poller, Festmacherringe.
    Kranplattform  6 m × 4,4 m auf der Landseite des Kopfes (x 3…9, y 3,6…8,0) mit dem Tretradkran (Kranhaus drehbar, Tretrad als Kind
                „Tretrad“, das das Spiel dreht): Der Ausleger ragt über den Kopf bis über das Schiff (Spitze bei y ≈ 17), ohne Seil und Last:
                Das Spiel hängt Seil und Haken ein, dreht das Kranhaus und hebt die Ladung des Zierschiffs (Stadtansicht.Kranladen.cs). Kisten und Fässer auf dem Kopf zwischen Rampe und Kran.
    Anker_Ufer (Mitte der Uferkante, Blick aufs Wasser), Anker_Liegeplatz (Seekante des Kopfes in der Mitte, y = 10,4, Blick aufs
    Wasser: dort liegt die Bordwand des Schiffes, 0,4 m Abstand), Anker_Planke (auf dem Kopf, 1,2 m hinter der Seekante).
Keine Hausfarbe, keine Zustände (gehört der Stadt); Prüfung mit `pruefen.py <glb> --wasser`.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import hafenbauten as hb  # noqa: E402
import landbau as lb  # noqa: E402
import wasser as ws  # noqa: E402
from bausteine import Vector  # noqa: E402

KAI, FUSS = ws.KAI, ws.FUSS
DECK = KAI - 0.45           # Deck des Kopfes und der Plattform (Dollbord des Schiffes liegt bei −0,15 m)
HALB = 1.5                  # halbe Breite des Zugangs (Rampe)
Y_LAND = -0.6               # Anfang der Rampe auf dem Kai
RAMPE_BIS = 5.0             # Ende der Rampe, ab hier flach
ZUGANG = 8.0                # Ende des Zugangs (Kopf beginnt)
KOPF_H = 1.2                # halbe Tiefe des Kopfes
KOPF_BIS = ZUGANG + 2 * KOPF_H    # Seekante des Kopfes = 10,4 m
KOPF_X = 9.0                # halbe Länge des Kopfes
PL_X0, PL_X1, PL_Y0 = 3.0, 9.0, 3.6   # Kranplattform (bis ZUGANG)
KRAN_ORT = Vector((6.0, 5.8, DECK + 0.14))   # Drehachse des Kranhauses
AUSLEGER = 9.3              # Spitze 9,3 m vor der Vorderwand (11,2 m von der Drehachse): reicht über das Schiff, das 5 m längs versetzt liegt (Zierschiff)
STAB = (-9.0, -7.5, -4.5, -1.5, 1.5, 4.5, 7.5, 9.0)   # Reibepfähle: keiner bei x = 0 (Laufplanke)


def hoehe(y):
    """Oberkante der Rampe bei y: von KAI auf DECK, ab RAMPE_BIS flach."""
    t = min(1.0, max(0.0, (y - Y_LAND) / (RAMPE_BIS - Y_LAND)))
    return KAI + (DECK - KAI) * t


def balken_y(netz, st, x0, x1, ya, yb, z0, z1, h, stoff, ohne=("-z",)):
    """Balken entlang Y von ya bis yb (Querschnitt x0–x1 × h), Oberkante bei ya z0, bei yb z1."""
    ws.klotz(netz, st[stoff], [(x0, ya, z0 - h), (x1, ya, z0 - h), (x1, yb, z1 - h), (x0, yb, z1 - h),
                               (x0, ya, z0), (x1, ya, z0), (x1, yb, z1), (x0, yb, z1)], ohne=ohne)


def rampe(netz, st):
    """Karrenrampe: Bohlen quer, Oberkante folgt hoehe(y); Trittleisten alle 0,6 m; flaches Stück bis ZUGANG."""
    import random
    zf = random.Random(11)
    stoffe = ("Holz alt", "Holz", "Holz alt", "Bretter", "Holz", "Holz alt")
    n = round((ZUGANG - Y_LAND) / 0.333)
    b = (ZUGANG - Y_LAND) / n
    for k in range(n):
        ya, yb = Y_LAND + k * b + 0.012, Y_LAND + (k + 1) * b - 0.012
        dz = zf.uniform(-0.014, 0.004)
        za, zb = hoehe(ya) + dz, hoehe(yb) + dz
        ws.klotz(netz, st[stoffe[(k * 5 + 11) % len(stoffe)]],
                 [(-HALB, ya, za - 0.08), (HALB, ya, za - 0.08), (HALB, yb, zb - 0.08), (-HALB, yb, zb - 0.08),
                  (-HALB, ya, za), (HALB, ya, za), (HALB, yb, zb), (-HALB, yb, zb)], ohne=("-z", "s1", "s3"))
    y = Y_LAND + 0.5
    while y < RAMPE_BIS:                                          # Trittleisten gegen das Rutschen
        ws.kasten(netz, st, -HALB + 0.05, HALB - 0.05, y - 0.04, y + 0.04, hoehe(y) - 0.01, hoehe(y) + 0.04, "Holz", ohne=("-z",))
        y += 0.6
    # Streckbalken darunter (Rampe geneigt, dann flach)
    for x in (-1.1, 0.0, 1.1):
        balken_y(netz, st, x - 0.1, x + 0.1, Y_LAND, RAMPE_BIS, hoehe(Y_LAND) - 0.05, hoehe(RAMPE_BIS) - 0.05, 0.3, "Holz")
        balken_y(netz, st, x - 0.1, x + 0.1, RAMPE_BIS, ZUGANG, DECK - 0.05, DECK - 0.05, 0.3, "Holz")
    # Saumbalken (Schrammbord) an den Seiten
    for s in (-1, 1):
        x0, x1 = (HALB, HALB + 0.2) if s > 0 else (-HALB - 0.2, -HALB)
        balken_y(netz, st, x0, x1, Y_LAND, RAMPE_BIS, hoehe(Y_LAND) + 0.1, hoehe(RAMPE_BIS) + 0.1, 0.4, "Holz")
        balken_y(netz, st, x0, x1, RAMPE_BIS, ZUGANG - 0.1, DECK + 0.1, DECK + 0.1, 0.4, "Holz")
    # Joche: Pfahlpaare mit Querholm, Köpfe unter dem Deck
    for y in (1.0, 3.2, 6.0):
        d = hoehe(y)
        ws.kasten(netz, st, -HALB - 0.1, HALB + 0.1, y - 0.14, y + 0.14, d - 0.62, d - 0.30, "Holz alt", ohne=("-z",))
        for x in (-HALB + 0.15, HALB - 0.15):
            ws.pfahl(netz, st, x, y, FUSS, d - 0.34, r=0.16, n=7)


def bohlen_quer(netz, st, y0, y1, x0, x1, z, saat, dicke=0.08, breite=0.333):
    """Bohlen quer zum Zugang (entlang X), von y0 bis y1 hintereinander, Oberkante z (flach)."""
    import random
    zf = random.Random(saat)
    stoffe = ("Holz alt", "Holz", "Holz alt", "Bretter", "Holz", "Holz alt")
    n = max(1, round((y1 - y0) / breite))
    b = (y1 - y0) / n
    for k in range(n):
        ya, yb = y0 + k * b + 0.012, y0 + (k + 1) * b - 0.012
        zz = z + zf.uniform(-0.014, 0.004)
        ws.kasten(netz, st, x0, x1, ya, yb, zz - dicke, zz, stoffe[(k * 5 + saat) % len(stoffe)], ohne=("-z",))


def ware(netz, st):
    """Fässer und Kisten auf dem Kopf zwischen Rampe und Kran sowie auf dem Kopf links der Rampe."""
    for i, (x, y) in enumerate(((2.35, 9.0), (2.95, 9.35), (2.4, 9.75))):
        bs.fass(netz, st, Vector((x, y, DECK)), r=0.3, h=0.85, reifen=(0.2 + 0.012 * i, 0.74))
    bs.fass(netz, st, Vector((2.65, 9.2, DECK + 0.85)), r=0.28, h=0.8)
    for x, y, d, g in ((-3.6, 9.0, 0.1, (0.85, 0.7, 0.6)), (-4.6, 9.3, -0.2, (0.8, 0.7, 0.55)), (-3.9, 9.9, 0.3, (0.7, 0.6, 0.55))):
        lb.kiste(netz, st, (x, y, DECK), g, dreh=d)
    lb.kiste(netz, st, (-3.9, 9.3, DECK + 0.6), (0.7, 0.6, 0.5), dreh=0.25)
    for k, (x, y) in enumerate(((-6.4, 9.0), (-6.0, 9.5), (-6.7, 9.6))):
        lb.saeule(netz, st, (x, y, DECK), 0.24, 0, 0.45, "Sack", n=7)


def hafensteg(st):
    netz = bs.Netz("Hafensteg")
    rampe(netz, st)
    # Deck des Kopfes (Bohlen entlang Y) und der Kranplattform
    ws.bohlen(netz, st, -KOPF_X, KOPF_X, ZUGANG, KOPF_BIS, lambda x: DECK, saat=17)
    ws.bohlen(netz, st, PL_X0, PL_X1, PL_Y0, ZUGANG, lambda x: DECK, saat=23)
    # Streckbalken unter der Plattform, Querbalken unter Kopf und Plattform
    for x in (PL_X0 + 0.4, 6.0, PL_X1 - 0.4):
        ws.kasten(netz, st, x - 0.1, x + 0.1, PL_Y0, ZUGANG, DECK - 0.34, DECK - 0.05, "Holz", ohne=("-z",))
    for y in (ZUGANG + 0.4, ZUGANG + 1.2, ZUGANG + 2.0):
        ws.kasten(netz, st, -KOPF_X, KOPF_X, y - 0.1, y + 0.1, DECK - 0.34, DECK - 0.05, "Holz", ohne=("-z",))
    for y in (PL_Y0 + 0.5, 5.8):
        ws.kasten(netz, st, PL_X0, PL_X1, y - 0.1, y + 0.1, DECK - 0.34, DECK - 0.05, "Holz", ohne=("-z",))
    # Saumbalken am Kopf und an der Plattform
    ws.kasten(netz, st, -KOPF_X, KOPF_X, KOPF_BIS - 0.1, KOPF_BIS + 0.1, DECK - 0.3, DECK + 0.14, "Holz", ohne=("-z",))
    ws.kasten(netz, st, -KOPF_X, PL_X0, ZUGANG - 0.1, ZUGANG + 0.1, DECK - 0.3, DECK + 0.1, "Holz", ohne=("-z",))
    ws.kasten(netz, st, PL_X0, PL_X1, PL_Y0 - 0.1, PL_Y0 + 0.1, DECK - 0.3, DECK + 0.14, "Holz", ohne=("-z",))
    ws.kasten(netz, st, PL_X0 - 0.1, PL_X0 + 0.1, PL_Y0, ZUGANG, DECK - 0.3, DECK + 0.14, "Holz", ohne=("-z",))
    for s in (-1, 1):
        ws.kasten(netz, st, s * KOPF_X - 0.1, s * KOPF_X + 0.1, ZUGANG, KOPF_BIS, DECK - 0.3, DECK + 0.14, "Holz", ohne=("-z",))
    ws.kasten(netz, st, PL_X1 - 0.1, PL_X1 + 0.1, PL_Y0, ZUGANG, DECK - 0.3, DECK + 0.14, "Holz", ohne=("-z",))
    # Schwelle am Land
    ws.kasten(netz, st, -HALB - 0.3, HALB + 0.3, -0.9, -0.5, -0.1, KAI - 0.08, "Holz alt", ohne=("-z",))
    # Pfähle des Kopfes: Reihen unter dem Deck, die Seeseite ragt als Reibepfahl über das Deck
    for x in STAB:
        ws.kasten(netz, st, x - 0.14, x + 0.14, ZUGANG, KOPF_BIS, DECK - 0.62, DECK - 0.30, "Holz alt", ohne=("-z",))
        ws.pfahl(netz, st, x, ZUGANG + 0.15, FUSS, DECK - 0.34, r=0.16, n=7)
        ws.pfahl(netz, st, x, KOPF_BIS + 0.12, FUSS, DECK + 0.75, r=0.18, n=8, kopf="Scheitholz")
    # Pfähle der Kranplattform
    for y in (PL_Y0 + 0.4, 5.8):
        ws.kasten(netz, st, PL_X0, PL_X1, y - 0.14, y + 0.14, DECK - 0.62, DECK - 0.30, "Holz alt", ohne=("-z",))
        for x in (PL_X0 + 0.4, 6.0, PL_X1 - 0.4):
            ws.pfahl(netz, st, x, y, FUSS, DECK - 0.34, r=0.17, n=7)
    # Poller auf dem Kopf nahe der Seekante
    for x in (-5.4, -1.6, 1.6, 5.4):
        ws.pfahl(netz, st, x, KOPF_BIS - 0.55, DECK, DECK + 0.5, r=0.17, n=8, kopf="Scheitholz")
        ws.kasten(netz, st, x - 0.2, x + 0.2, KOPF_BIS - 0.75, KOPF_BIS - 0.35, DECK + 0.3, DECK + 0.38, "Eisen")
    # Festmacherringe an der Seekante
    for x in (-3.5, 3.5):
        ws.walze(netz, st, KOPF_BIS + 0.12, DECK - 0.05, 0.08, x - 0.03, x + 0.03, "Eisen", n=8)
    # Pfosten am Landende der Rampe
    for s in (-1, 1):
        ws.pfahl(netz, st, s * (HALB + 0.1), 0.0, KAI, KAI + 1.1, r=0.1, n=6, kopf="Scheitholz")
    # Schwellen unter dem Kranhaus
    for x in (KRAN_ORT.x - 1.3, KRAN_ORT.x + 1.3):
        ws.kasten(netz, st, x - 0.2, x + 0.2, KRAN_ORT.y - 2.2, KRAN_ORT.y + 2.1, DECK, KRAN_ORT.z, "Holz alt", ohne=("-z",))
    ware(netz, st)
    # Tretradkran: Kranhaus (drehbar) mit Tretrad als Kind
    kh = hb.kranhaus(st, AUSLEGER, last=False)
    kh.location = KRAN_ORT
    rad = hb.kind(kh, hb.kran_tretrad(st), hb.RAD_ACHSE)
    tretrad_anker = ws.anschluss(kh, "Anker_Tretrad", hb.RAD_ACHSE + Vector((-(hb.KH_X + 0.75), 0, -hb.RAD_R + 0.17)), 90)
    return [netz.objekt(), kh, rad, tretrad_anker]


def bauen(st):
    teile = hafensteg(st)
    anker = [bs.anker("Anker_Ufer", (0, 0, KAI), 0), bs.anker("Anker_Liegeplatz", (0, KOPF_BIS, DECK), 0),
             bs.anker("Anker_Planke", (0, KOPF_BIS - 1.2, DECK), 0)]
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
    alle = objekte + [k for o in objekte for k in o.children_recursive if k not in objekte]
    netze = [o for o in alle if o.type == "MESH"]
    bs.bericht("wasser_hafensteg", netze + [o for o in alle if o.type != "MESH"], einzeln=True)
    bs.BIBLIOTHEK_JE_FLAECHE = True
    vorher, nachher = bs.farben_zusammenlegen(netze, behalten=hb.BEHALTEN)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren("wasser_hafensteg", objekte))
    if "--ohne-bilder" not in sys.argv:
        print(vorschau(objekte, schnell))


if __name__ == "__main__":
    main()
