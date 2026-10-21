"""Duftgarten des Parfumeurs (Umland): Lavendel und Jasmin. Fläche 20 × 16 m.

    blender -b --python tools/modelle/duftgarten.py        oder
    python tools/modelle/duftgarten.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/umland/duftgarten.glb und Bilder nach docs/modelle/.

Objekte im GLB (siehe landbau.py):
    Haus        verputzte Gärtnerhütte mit Ziegeldach, Beetreihen, Laubengang aus Latten, Trockenmauer
    Arbeit      Lavendel in Reihen (wie bisher im Spiel im Wechsel mit Jasmin), Jasmin an der Laube,
                Trockengestell mit Lavendelbündeln, Körbe mit Blüten
    Fahne       an der Hütte
    Leerstand   Bretter vor Tür und Fenstern, Unkraut in den leeren Reihen
    Anker_Tuer  vor der Hütte, Anker_Karren am Eingang

Aufbau (Blender: Z oben, Weg bei -Y; Fläche x ±10, y ±8): links die Hütte, rechts die Reihen, hinten die Laube.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "duftgarten"
FLAECHE = (20.0, 16.0)
REIHEN = [(-2.6 + 1.8 * k) for k in range(7)]          # x der Beetreihen
Y0, Y1 = -5.6, 2.6


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus, arbeit, fahne, leer = bs.Netz("Haus"), bs.Netz("Arbeit"), bs.Netz("Fahne"), bs.Netz("Leerstand")

    lb.flaeche(haus, st, lb.rechteck(-10, -8, 10, 8), 0.008, "Gras")
    lb.flaeche(haus, st, lb.rechteck(-4.2, -8.0, -3.4, 4.6), 0.014, "Erde hell")
    lb.flaeche(haus, st, lb.rechteck(-3.4, 3.8, 9.4, 4.6), 0.014, "Erde hell")
    for x in REIHEN:
        lb.beet(haus, st, x - 0.55, Y0, x + 0.55, Y1, z=0.16)
    # Laubengang hinten: Pfosten und Latten, davor Jasmin (Arbeit)
    for x in (-3.0, -0.4, 2.2, 4.8, 7.4, 9.4):
        for y in (5.0, 7.2):
            haus.quader((x, y, 1.15), (0.14, 0.14, 2.3), st["Holz"], ohne=("-z",))
    for y in (5.0, 7.2):
        haus.stab((-3.2, y, 2.36), (9.6, y, 2.36), 0.12, 0.12, st["Holz"], quer=(0, 0, 1))
    for k in range(14):
        x = -2.8 + k * 0.92
        haus.stab((x, 4.9, 2.47), (x, 7.3, 2.47), 0.06, 0.08, st["Holz alt"], quer=(0, 0, 1))
    bs.trockenmauer(haus, st, [(-9.7, 7.7), (9.7, 7.7)], hoehe=0.8)
    bs.trockenmauer(haus, st, [(9.7, 7.2), (9.7, -7.7)], hoehe=0.76)

    # ── Hütte links ───────────────────────────────────────────────────────────
    h = lb.huette(haus, st, -9.4, -5.4, -6.0, -2.0, 2.5, wand="putz", deckung=bs.ZIEGEL, neigung=45,
                  tuer_u=0.8, fenster_vorn=(-1.0,), fenster_seite=(("rechts", 0.6),))
    bs.fahne(fahne, st, (-9.8, -6.3, 0), hoehe=4.6, richtung=1)

    # ── Arbeit ────────────────────────────────────────────────────────────────
    for i, x in enumerate(REIHEN):
        if i % 2 == 0:
            lb.pflanzreihe(arbeit, st, (x, Y0 + 0.4), (x, Y1 - 0.4), 0.62, 0.32, 0.62, "Lavendel", "Kraut", z=0.16, saat=i)
        else:
            jasmin_reihe(arbeit, st, x, saat=i)
    for k in range(6):                                               # Jasmin an der Laube
        x = -2.4 + k * 2.2
        lb.busch(arbeit, st, (x, 4.7), 0.55, 2.1, "Laub", "Laub hell", n=6, saat=10 + k)
        for d in (-0.2, 0.25):
            arbeit.quader((x + d, 4.4, 1.2 + d), (0.18, 0.12, 0.18), st["Jasmin"])
    gestell(arbeit, st, Vector((-6.0, 2.6, 0)))
    for x, y, b in ((-2.6, -6.6, "Lavendel"), (-1.9, -7.0, "Jasmin")):
        lb.saeule(arbeit, st, (x, y, 0), 0.3, 0, 0.36, "Holz alt", n=8)
        lb.saeule(arbeit, st, (x, y, 0.36), 0.26, 0, 0.12, b, n=8)

    # ── Leerstand ─────────────────────────────────────────────────────────────
    lb.vernageln(leer, st, h["oeffnungen"])
    for i, x in enumerate(REIHEN):
        lb.busch(leer, st, (x, -3.6 + 2.3 * (i % 3)), 0.3, 0.5, "Gras", "Moos", n=5, saat=20 + i)
    lb.unkraut(leer, st, [(-3.8, -1.0), (-5.0, -7.0), (1.0, 4.2), (6.0, 6.0), (8.5, -6.5), (-7.0, 3.0)], saat=8)

    anker = [bs.anker("Anker_Tuer", tuple(h["tuer"])), bs.anker("Anker_Karren", (-3.0, -7.9, 0.0))]
    return lb.knoten(haus, arbeit, fahne, leer, anker)


def jasmin_reihe(netz, st, x, saat=0):
    """Jasminbüsche in einer Reihe, weiße Blüten obenauf."""
    for k in range(5):
        y = Y0 + 0.7 + k * (Y1 - Y0 - 1.4) / 4
        lb.busch(netz, st, (x, y), 0.45, 0.9, "Laub", "Laub hell", n=6, saat=saat * 7 + k)
        netz.quader((x + 0.05 * (k % 2), y, 0.95), (0.28, 0.28, 0.12), st["Jasmin"])


def gestell(netz, st, fuss):
    """Trockengestell: Stange auf zwei Böcken mit hängenden Lavendelbündeln."""
    x, y = fuss.x, fuss.y
    for dx in (-1.3, 1.3):
        for s in (-1, 1):
            netz.stab((x + dx + 0.04 * s, y - s * 0.4, 0), (x + dx + 0.04 * s, y + s * 0.1, 1.8), 0.08, 0.08, st["Holz"],
                      quer=(1, 0, 0))
    netz.stab((x - 1.5, y - 0.15, 1.72), (x + 1.5, y - 0.15, 1.72), 0.07, 0.07, st["Holz"])
    for k in range(6):
        lb.kegel(netz, (x - 1.0 + k * 0.4, y - 0.15), 0.14, 1.68, 1.05, st["Lavendel" if k % 3 else "Jasmin"], n=5)


def main():
    lb.ausgeben(NAME, FLAECHE, bauen(), ziel_hoehe=2.0, ausschnitt=26.0)


if __name__ == "__main__":
    main()
