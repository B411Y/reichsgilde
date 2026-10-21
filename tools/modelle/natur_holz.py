"""Totholz fürs Umland – ein Baukasten, je Art zwei Varianten.

    blender -b --python tools/modelle/natur_holz.py        oder
    python tools/modelle/natur_holz.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/natur/natur_holz.glb und docs/modelle/natur_holz.png.

Knoten (Ursprung je in der Mitte am Boden):
    Totholz1–2      umgestürzter Stamm mit Wurzelteller (1) oder abgebrochen mit Pilzen (2), 4–5 m
    Stumpf1–2       alter Baumstumpf mit Moos und Wurzeln
    Reisighaufen1–2 aufgeschichtete Äste
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "natur_holz"


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    teile = []
    for art, bauer in (("Totholz", totholz), ("Stumpf", stumpf), ("Reisighaufen", reisig)):
        for i in range(2):
            netz = bs.Netz(f"{art}{i + 1}")
            bauer(netz, st, i, sum(map(ord, art)) * 10 + i)
            teile.append(netz.objekt())
    return teile


def totholz(netz, st, i, saat):
    z = nt.zufall(saat)
    laenge = 4.6 if i == 0 else 3.6
    r = 0.3 if i == 0 else 0.26
    a, b = Vector((-laenge / 2, 0, r * 0.9)), Vector((laenge / 2, 0.2, r * 0.8))
    nt.ast(netz, st, a, b, r, r * 0.7, stoff="Totholz", n=7)
    nt.ast(netz, st, a, a + Vector((0.05, 0, 0)), r * 0.98, r * 0.98, stoff="Scheitholz", n=7)   # Schnittfläche
    if i == 0:                                                          # Wurzelteller, aufgestellt
        for k in range(7):
            w = 2 * math.pi * k / 7 + 0.3
            nt.ast(netz, st, a, a + Vector((-0.15, math.cos(w) * 0.9, 0.35 + math.sin(w) * 0.6)), 0.08, 0.02,
                   stoff="Totholz", n=4)
        lb.felsbrocken(netz, st, (a.x - 0.1, 0, 0), 0.5, 0.45, saat=saat, stoff="Erde", stoff2="Erde hell", n=6)
    else:                                                               # Baumpilze an der Seite
        for k in range(3):
            p = a.lerp(b, 0.3 + 0.18 * k) + Vector((0, -r * 0.95, 0.08 * k))
            lb.saeule(netz, st, p, 0.12, 0, 0.05, "Pilzhut braun", n=6)
    for k in range(3):                                                   # Äste und Moos
        p = a.lerp(b, 0.45 + 0.15 * k)
        w = (-1) ** k * z.uniform(0.6, 1.0)
        nt.ast(netz, st, p, p + Vector((0.4, w * 0.8, 0.5)), 0.06, 0.02, stoff="Totholz", n=4)
    nt.krone(netz, st, a.lerp(b, 0.3) + Vector((0, 0, r * 0.85)), 0.5, 0.08, saat=saat + 5, stoffe=("Moos", "Gras"),
             n=6, flach_unten=0.2)


def stumpf(netz, st, i, saat):
    r, h = (0.45, 0.6) if i == 0 else (0.35, 0.4)
    lb.saeule(netz, st, (0, 0, 0), r, 0, h, "Rinde", enden="Scheitholz", n=8)
    for k in range(5):                                                   # Wurzelanläufe
        w = 2 * math.pi * k / 5 + 0.2 * i
        nt.ast(netz, st, (math.cos(w) * r * 0.7, math.sin(w) * r * 0.7, h * 0.4),
               (math.cos(w) * r * 1.9, math.sin(w) * r * 1.9, 0.02), 0.11, 0.03, n=4)
    if i == 0:
        nt.krone(netz, st, (0.1, 0, h + 0.03), r * 0.6, 0.06, saat=saat, stoffe=("Moos", "Gras"), n=6, flach_unten=0.3)
    else:
        for k in range(2):
            lb.saeule(netz, st, (r * 0.95, 0.1 * k, h * (0.4 + 0.25 * k)), 0.1, 0, 0.04, "Pilzhut braun", n=6)


def reisig(netz, st, i, saat):
    z = nt.zufall(saat)
    for k in range(14 if i == 0 else 9):
        w = z.uniform(-0.5, 0.5) + (0 if k % 2 else math.pi / 2) * 0.3
        laenge = z.uniform(1.4, 2.2) * (1.0 if i == 0 else 0.75)
        hoehe = 0.08 + 0.09 * (k % 5)
        m = Vector((z.uniform(-0.2, 0.2), z.uniform(-0.3, 0.3), hoehe))
        d = Vector((math.cos(w), math.sin(w), z.uniform(-0.05, 0.05))) * laenge / 2
        nt.ast(netz, st, m - d, m + d, 0.04, 0.02, stoff="Holz alt" if k % 3 else "Totholz", n=4)


def main():
    nt.ausgeben(NAME, bauen(), spalten=3, abstand=5.5, zeilenabstand=5.5, ziel_hoehe=0.5, ausschnitt=17.0)


if __name__ == "__main__":
    main()
