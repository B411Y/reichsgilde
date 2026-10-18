"""Steine fürs Umland – ein Baukasten, je Größe zwei oder drei Varianten.

    blender -b --python tools/modelle/natur_steine.py        oder
    python tools/modelle/natur_steine.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/natur_steine.glb und docs/modelle/natur_steine.png.

Knoten (Ursprung je in der Mitte am Boden):
    Kiesel1–3           eine Handvoll kleiner Steine (bis 0,25 m)
    Felsbrocken1–3      einzelner Brocken (0,6–1,2 m)
    Findling1–2         großer Block mit Moos obenauf (2–3 m)
    Lesesteinhaufen1–2  Haufen aufgelesener Feldsteine am Ackerrand, mit Gras dazwischen
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "natur_steine"


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    teile = []
    for art, bauer, anzahl in (("Kiesel", kiesel, 3), ("Felsbrocken", brocken, 3), ("Findling", findling, 2),
                               ("Lesesteinhaufen", lesesteine, 2)):
        for i in range(anzahl):
            netz = bs.Netz(f"{art}{i + 1}")
            bauer(netz, st, i, sum(map(ord, art)) * 10 + i)
            teile.append(netz.objekt())
    return teile


def kiesel(netz, st, i, saat):
    z = nt.zufall(saat)
    for k in range(4 + 2 * i):
        w = 2 * math.pi * k / (4 + 2 * i) + z.uniform(-0.4, 0.4)
        r = z.uniform(0.05, 0.35 + 0.1 * i)
        g = z.uniform(0.07, 0.14)
        lb.felsbrocken(netz, st, (math.cos(w) * r, math.sin(w) * r), g, g * 0.75, saat=saat * 10 + k, n=5,
                       stoff="Fels" if k % 2 else "Bruchstein", stoff2="Fels dunkel")


def brocken(netz, st, i, saat):
    r = (0.55, 0.8, 1.1)[i]
    lb.felsbrocken(netz, st, (0, 0), r, r * 0.9, saat=saat, n=7)
    if i:
        lb.felsbrocken(netz, st, (r * 0.95, r * 0.3), r * 0.35, r * 0.3, saat=saat + 1, n=5)


def findling(netz, st, i, saat):
    r = (1.4, 1.1)[i]
    lb.felsbrocken(netz, st, (0, 0), r, r * 1.5, saat=saat, n=8, stoff="Fels", stoff2="Fels dunkel",
                   profil=((1.0, 0.0), (0.95, 0.45), (0.6, 1.0)))
    # Moos auf der Kuppe: eine flache Kappe knapp über der Oberseite
    nt.krone(netz, st, (0.05, 0.05, r * 1.5 * 0.97), r * 0.45, r * 0.1, saat=saat + 3, stoffe=("Moos", "Gras"), n=6,
             flach_unten=0.2)
    for k in range(3):
        w = 2 * math.pi * k / 3 + 0.5
        bs.buschel(netz, st, Vector((math.cos(w) * r * 1.05, math.sin(w) * r * 1.05, 0)), groesse=0.5)


def lesesteine(netz, st, i, saat):
    z = nt.zufall(saat)
    laenge = 2.4 if i == 0 else 1.6
    for k in range(9 if i == 0 else 6):
        x = z.uniform(-laenge / 2, laenge / 2)
        y = z.uniform(-0.45, 0.45) * (1 - abs(x) / laenge)
        hoehe = 0.3 * (1 - abs(x) / laenge) * z.uniform(0.8, 1.2)
        g = z.uniform(0.2, 0.32)
        lb.felsbrocken(netz, st, (x, y, max(0.0, hoehe - g * 0.6)), g, g * 0.8, saat=saat * 10 + k, n=5,
                       stoff="Bruchstein" if k % 2 else "Fels", stoff2="Fels dunkel")
    for k in range(4):
        bs.buschel(netz, st, Vector((-laenge / 2 + k * laenge / 3, 0.5 * (-1) ** k, 0)), groesse=0.45)


def main():
    nt.ausgeben(NAME, bauen(), spalten=5, abstand=4.0, zeilenabstand=5.0, ziel_hoehe=0.6, ausschnitt=19.0)


if __name__ == "__main__":
    main()
