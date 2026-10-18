"""Büsche und Sträucher fürs Umland – ein Baukasten, je Art zwei Varianten.

    blender -b --python tools/modelle/natur_buesche.py        oder
    python tools/modelle/natur_buesche.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/natur_buesche.glb und docs/modelle/natur_buesche.png.

Knoten (Ursprung je in der Mitte am Boden):
    Hasel1–2        mehrere Ballen aus dünnen Stämmchen (2,5–3 m)
    Holunder1–2     lockerer Strauch mit weißen Blütendolden (2,5 m)
    Brombeere1–2    niedrige, lange Hecke mit dunklen Beeren (3–4 m lang, 1 m hoch)
    Ginster1–2      struppiger Busch mit gelben Blüten (1,3 m)
    Wacholder1–2    schmale, dunkle Säule (2–3 m)
Laub: „Laub“, „Laub hell“, „Laub dunkel“ (je Jahreszeit umgefärbt), Wacholder in „Tanne“.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "natur_buesche"


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    teile = []
    for art, bauer, groessen in (("Hasel", hasel, (2.8, 2.4)), ("Holunder", holunder, (2.6, 2.2)),
                                 ("Brombeere", brombeere, (3.8, 2.8)), ("Ginster", ginster, (1.3, 1.1)),
                                 ("Wacholder", wacholder, (2.8, 2.0))):
        for i, g in enumerate(groessen):
            netz = bs.Netz(f"{art}{i + 1}")
            bauer(netz, st, g, sum(map(ord, art)) * 10 + i)      # fester Startwert je Art
            teile.append(netz.objekt())
    return teile


def hasel(netz, st, h, saat):
    z = nt.zufall(saat)
    for k in range(4):
        w = 2 * math.pi * k / 4 + z.uniform(-0.3, 0.3)
        a = Vector((math.cos(w) * 0.15, math.sin(w) * 0.15, 0))
        b = Vector((math.cos(w) * h * 0.22, math.sin(w) * h * 0.22, h * 0.4))
        nt.ast(netz, st, a, b, 0.05, 0.03, n=4)
    for k in range(3):
        w = 2 * math.pi * k / 3 + z.uniform(-0.4, 0.4)
        m = (math.cos(w) * h * 0.18, math.sin(w) * h * 0.18, h * z.uniform(0.45, 0.58))
        nt.krone(netz, st, m, h * 0.24, h * 0.22, saat=saat * 5 + k, stoffe=("Laub", "Laub hell"), n=6)


def holunder(netz, st, h, saat):
    z = nt.zufall(saat)
    nt.ast(netz, st, (0, 0, 0), (0.1, 0, h * 0.25), 0.08, 0.06, n=5)
    mitte = Vector((0.1, 0, h * 0.52))
    nt.krone(netz, st, mitte, h * 0.36, h * 0.3, saat=saat, stoffe=("Laub dunkel", "Laub"), n=7)
    for p in nt.punkte_auf_krone(mitte, h * 0.36, h * 0.3, 9, saat):
        nt.tupfen(netz, st, p, 0.16, "Blüte weiß", n=5)


def brombeere(netz, st, laenge, saat):
    """Lange, niedrige Hecke aus drei flachen Ballen, mit dunklen Beeren."""
    for k in range(3):
        x = (k - 1) * laenge * 0.3
        m = (x, 0.1 * (k % 2), 0.55)
        nt.krone(netz, st, m, laenge * 0.22, 0.5, saat=saat * 3 + k, ry=0.75, stoffe=("Laub dunkel", "Laub"), n=6)
        for p in nt.punkte_auf_krone(m, laenge * 0.22, 0.5, 5, saat * 3 + k, ry=0.75):
            nt.tupfen(netz, st, p, 0.06, "Beere")


def ginster(netz, st, h, saat):
    """Struppig: viele schmale Kegel aus einem Punkt, dazwischen gelbe Blütentupfer."""
    z = nt.zufall(saat)
    for k in range(9):
        w = 2 * math.pi * k / 9 + z.uniform(-0.2, 0.2)
        r = z.uniform(0.15, 0.45) * h
        lb.kegel(netz, (math.cos(w) * r * 0.6, math.sin(w) * r * 0.6), 0.22 * h, 0.0, h * z.uniform(0.7, 1.0),
                 st["Laub dunkel"], st["Laub"], n=4, dreh=w)
    for k in range(10):
        w = 2 * math.pi * k / 10 + 0.3
        r = 0.32 * h
        nt.tupfen(netz, st, (math.cos(w) * r, math.sin(w) * r, h * z.uniform(0.45, 0.75)), 0.09, "Ginster", n=4)


def wacholder(netz, st, h, saat):
    """Schmale Säule aus zwei gestaffelten Kegeln."""
    z = nt.zufall(saat)
    nt.ast(netz, st, (0, 0, 0), (0, 0, 0.3), 0.08, 0.07, n=4)
    lb.kegel(netz, (0, 0), h * 0.2, 0.15, h * 0.7, st["Tanne"], st["Tanne hell"], n=6, dreh=z.uniform(0, 1))
    lb.kegel(netz, (0.03, 0), h * 0.15, h * 0.4, h, st["Tanne hell"], st["Tanne"], n=6, dreh=z.uniform(0, 1))


def main():
    nt.ausgeben(NAME, bauen(), spalten=5, abstand=5.0, zeilenabstand=6.0, ziel_hoehe=1.2, ausschnitt=26.0)


if __name__ == "__main__":
    main()
