"""Bodenpflanzen und Kleinkram fürs Umland – ein Baukasten, je Art zwei oder drei Varianten.

    blender -b --python tools/modelle/natur_boden.py        oder
    python tools/modelle/natur_boden.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/natur_boden.glb und docs/modelle/natur_boden.png.

Knoten (Ursprung je in der Mitte am Boden, alle unter 1,6 m):
    Farn1–2             Wedel im Kreis
    Gras1–3             Büschel hohen Grases (einzeln oder als Horst)
    Schilf1–2           hohe Halme mit braunen Kolben (für Ufer und Teiche)
    Brennnessel1–2      dichter Trupp schmaler Stängel
    Wildblumen1–2       Gras mit Mohn und Kornblumen (1) oder Margeriten (2)
    Pilze1–2            Fliegenpilze (1), Steinpilze (2)
    Maulwurfshuegel1–2  kleine Erdhaufen
Gras, Farn und Nessel bleiben grün; nur „Laub …“ färbt das Spiel je Jahreszeit.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "natur_boden"


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    teile = []
    for art, bauer, anzahl in (("Farn", farn, 2), ("Gras", gras, 3), ("Schilf", schilf, 2),
                               ("Brennnessel", nessel, 2), ("Wildblumen", blumen, 2), ("Pilze", pilze, 2),
                               ("Maulwurfshuegel", huegel, 2)):
        for i in range(anzahl):
            netz = bs.Netz(f"{art}{i + 1}")
            bauer(netz, st, i, sum(map(ord, art)) * 10 + i)
            teile.append(netz.objekt())
    return teile


def farn(netz, st, i, saat):
    z = nt.zufall(saat)
    n = 7 if i == 0 else 5
    for k in range(n):
        w = 2 * math.pi * k / n + z.uniform(-0.2, 0.2)
        laenge = (0.8 if i == 0 else 0.6) * z.uniform(0.85, 1.1)
        nt.blatt(netz, st, (0, 0, 0.02 + 0.004 * k), (math.cos(w), math.sin(w)), laenge, laenge * 0.32, "Farn",
                 hoch=0.55)


def gras(netz, st, i, saat):
    z = nt.zufall(saat)
    horste = 1 if i == 0 else (3 if i == 1 else 5)
    for k in range(horste):
        w = 2 * math.pi * k / horste
        r = 0.0 if horste == 1 else 0.3 + 0.1 * (k % 2)
        mitte = (math.cos(w) * r, math.sin(w) * r)
        for j in range(5):
            u = 2 * math.pi * j / 5 + z.uniform(0, 1)
            fuss = Vector((mitte[0] + math.cos(u) * 0.05, mitte[1] + math.sin(u) * 0.05, 0))
            spitze = fuss + Vector((math.cos(u) * 0.2, math.sin(u) * 0.2, z.uniform(0.45, 0.7)))
            halm(netz, st, fuss, spitze, 0.07, "Gras" if j % 2 else "Laub hell")


def halm(netz, st, fuss, spitze, breite, stoff):
    """Schmales, spitzes Blatt vom Fuß zur Spitze, beidseitig (Rückseite leicht versetzt)."""
    richtung = spitze - fuss
    q = richtung.cross(Vector((0, 0, 1)))
    q = q.normalized() * breite / 2 if q.length > 1e-6 else Vector((breite / 2, 0, 0))
    n = q.cross(richtung).normalized()
    netz.vieleck_nach([fuss - q, fuss + q, spitze], n, st[stoff])
    netz.vieleck_nach([fuss - q - n * 0.006, fuss + q - n * 0.006, spitze - n * 0.006], -n, st[stoff])


def schilf(netz, st, i, saat):
    z = nt.zufall(saat)
    for k in range(9 if i == 0 else 6):
        w = 2 * math.pi * k / 9 + z.uniform(-0.3, 0.3)
        r = z.uniform(0.05, 0.4)
        fuss = Vector((math.cos(w) * r, math.sin(w) * r, 0))
        hoch = z.uniform(1.1, 1.6)
        spitze = fuss + Vector((math.cos(w) * 0.12, math.sin(w) * 0.12, hoch))
        halm(netz, st, fuss, spitze, 0.06, "Schilf")
        if k % 3 == 0:                                                   # Rohrkolben
            nt.ast(netz, st, fuss + Vector((0, 0, 0.05)), fuss + Vector((0.02, 0, hoch * 0.85)), 0.012, 0.01,
                   stoff="Schilf", n=3)
            nt.ast(netz, st, fuss + Vector((0.02, 0, hoch * 0.85)), fuss + Vector((0.025, 0, hoch * 0.85 + 0.22)),
                   0.045, 0.04, stoff="Kolben", n=5)


def nessel(netz, st, i, saat):
    z = nt.zufall(saat)
    for k in range(8 if i == 0 else 5):
        w = 2 * math.pi * k / 8 + z.uniform(-0.3, 0.3)
        r = z.uniform(0.05, 0.35)
        lb.kegel(netz, (math.cos(w) * r, math.sin(w) * r), 0.14, 0.0, z.uniform(0.6, 0.95), st["Nessel"],
                 st["Laub dunkel"], n=4, dreh=w)


def blumen(netz, st, i, saat):
    z = nt.zufall(saat)
    gras(netz, st, 1, saat + 1)
    farben = ("Mohn", "Kornblume", "Mohn") if i == 0 else ("Blüte weiß", "Blüte weiß", "Ginster")
    for k in range(7):
        w = 2 * math.pi * k / 7 + z.uniform(-0.3, 0.3)
        r = z.uniform(0.1, 0.5)
        fuss = Vector((math.cos(w) * r, math.sin(w) * r, 0))
        kopf = fuss + Vector((0, 0, z.uniform(0.45, 0.65)))
        nt.ast(netz, st, fuss, kopf, 0.012, 0.01, stoff="Gras", n=3)
        nt.tupfen(netz, st, kopf, 0.07, farben[k % 3], n=5)


def pilze(netz, st, i, saat):
    z = nt.zufall(saat)
    for k in range(3 if i == 0 else 4):
        w = 2 * math.pi * k / 3 + z.uniform(-0.3, 0.3)
        r = 0.0 if k == 0 else z.uniform(0.15, 0.3)
        x, y = math.cos(w) * r, math.sin(w) * r
        hoehe = (0.22 if i == 0 else 0.14) * z.uniform(0.7, 1.1) * (1.3 if k == 0 else 1.0)
        dicke = 0.035 if i == 0 else 0.055
        lb.saeule(netz, st, (x, y, 0), dicke, 0, hoehe, "Pilzstiel", n=6)
        hut = 0.11 if i == 0 else 0.09
        lb.kegel(netz, (x, y), hut, hoehe - 0.01, hoehe + hut * 0.75, st["Pilzhut" if i == 0 else "Pilzhut braun"],
                 n=7)
        if i == 0:                                                         # weiße Tupfen auf dem Fliegenpilz
            for j in range(3):
                u = 2 * math.pi * j / 3 + 0.4
                nt.tupfen(netz, st, (x + math.cos(u) * hut * 0.45, y + math.sin(u) * hut * 0.45, hoehe + hut * 0.42),
                          0.018, "Pilzstiel", n=3)


def huegel(netz, st, i, saat):
    lb.haufen(netz, st, (0, 0), 0.35 if i == 0 else 0.28, 0.22 if i == 0 else 0.17, "Erde", "Erde hell", saat=saat, n=7)


def main():
    nt.ausgeben(NAME, bauen(), spalten=5, abstand=2.2, zeilenabstand=2.8, ziel_hoehe=0.4, ausschnitt=12.5)


if __name__ == "__main__":
    main()
