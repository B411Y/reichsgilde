"""Kräutergarten des Alchimisten (Umland): Kräuter und Kröten. Fläche 20 × 16 m.

    blender -b --python tools/modelle/kraeutergarten.py        oder
    python tools/modelle/kraeutergarten.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/kraeutergarten.glb und Bilder nach docs/modelle/.

Objekte im GLB (siehe landbau.py):
    Haus        Gärtnerhütte in Fachwerk mit Strohdach, vier Beete mit Holzeinfassung, Krötenteich mit Schilf,
                Flechtzaun, Weidenbaum
    Arbeit      Kräuter in den Beeten, Trockengestell mit Bündeln, Körbe, Gießkanne, Kröten am Teich
    Fahne       an der Hütte
    Leerstand   Bretter vor Tür und Fenstern, Unkraut in den leeren Beeten
    Anker_Tuer  vor der Hütte, Anker_Karren am Gartentor

Aufbau (Blender: Z oben, Weg bei -Y; Fläche x ±10, y ±8): links die Hütte, in der Mitte die Beete,
rechts hinten der Teich. Vorn ein Flechtzaun mit Tor zum Weg.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "kraeutergarten"
FLAECHE = (20.0, 16.0)
BEETE = [(-3.0 + 2.6 * k, -4.2, -1.4 + 2.6 * k, 2.6) for k in range(4)]     # x0, y0, x1, y1
TEICH = (6.6, 3.8, 2.4)                                                   # Mitte und Halbmesser


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus, arbeit, fahne, leer = bs.Netz("Haus"), bs.Netz("Arbeit"), bs.Netz("Fahne"), bs.Netz("Leerstand")

    lb.flaeche(haus, st, [(-10, -8), (10, -8), (10, 8), (-10, 8)], 0.008, "Gras")
    lb.flaeche(haus, st, lb.rechteck(-4.0, -7.6, -3.2, 3.4), 0.014, "Erde hell")             # Wege
    lb.flaeche(haus, st, lb.rechteck(-3.2, -5.2, 9.0, -4.6), 0.014, "Erde hell")
    # Beete mit Holzeinfassung
    for x0, y0, x1, y1 in BEETE:
        lb.beet(haus, st, x0, y0, x1, y1, z=0.18)
        for y in (y0 - 0.06, y1 + 0.06):
            haus.quader(((x0 + x1) / 2, y, 0.12), (x1 - x0 + 0.24, 0.1, 0.24), st["Holz alt"], ohne=("-z",))
        for x in (x0 - 0.06, x1 + 0.06):
            haus.quader((x, (y0 + y1) / 2, 0.11), (0.1, y1 - y0, 0.22), st["Holz alt"], ohne=("-z",))
    # Krötenteich mit Ufersteinen und Schilf
    mx, my, r = TEICH
    ring = [(mx + r * math.cos(2 * math.pi * k / 10) * (1 + 0.12 * math.sin(3 * k)),
             my + r * 0.8 * math.sin(2 * math.pi * k / 10)) for k in range(10)]
    lb.flaeche(haus, st, ring, 0.03, "Wasser")
    for k in range(0, 10, 2):
        x, y = ring[k]
        lb.felsbrocken(haus, st, (x, y), 0.35, 0.3, saat=80 + k, stoff="Fels", stoff2="Moos")
    for k, (x, y) in enumerate(((mx + 2.0, my + 1.0), (mx - 2.2, my + 1.2), (mx + 1.2, my + 1.9), (mx - 0.6, my + 2.0))):
        lb.busch(haus, st, (x, y), 0.35, 1.3, stoff="Schilf", stoff2="Gras", n=5, saat=k)
    bs.linde(haus, st, Vector((-7.6, 5.0, 0)), hoehe=6.2, r=2.1, saat=3)
    # Flechtzaun vorn, Tor in der Mitte (Lücke)
    bs.zaun(haus, st, [(-9.6, -7.4), (-4.4, -7.4)], hoehe=0.9, abstand=1.3, stoff="Holz alt")
    bs.zaun(haus, st, [(-2.6, -7.4), (9.6, -7.4), (9.6, 7.4)], hoehe=0.9, abstand=1.3, stoff="Holz alt")

    # ── Hütte links ───────────────────────────────────────────────────────────
    h = lb.huette(haus, st, -9.4, -5.2, -5.6, -1.4, 2.4, wand="fachwerk", deckung=bs.STROH, neigung=48,
                  tuer_u=0.9, fenster_vorn=(-1.1,), fenster_seite=(("rechts", 0.8),))
    bs.fahne(fahne, st, (-9.8, -5.9, 0), hoehe=4.6, richtung=1)

    # ── Arbeit ────────────────────────────────────────────────────────────────
    pflanzen = (("Kraut", "Laub hell"), ("Heu", "Kraut"), ("Rose", "Kraut"), ("Laub", "Kraut"))
    for (x0, y0, x1, y1), (a, b) in zip(BEETE, pflanzen):
        for k in range(3):
            x = x0 + (x1 - x0) * (k + 0.5) / 3
            lb.pflanzreihe(arbeit, st, (x, y0 + 0.4), (x, y1 - 0.4), 0.7, 0.28, 0.55, a, b, z=0.18, saat=k + int(x * 10))
    trockengestell(arbeit, st, Vector((-3.6, 5.0, 0)))
    for x, y in ((-2.4, -5.0), (-1.8, -5.4)):
        lb.saeule(arbeit, st, (x, y, 0), 0.28, 0, 0.35, "Holz alt", n=8)          # Körbe
        lb.saeule(arbeit, st, (x, y, 0.35), 0.24, 0, 0.12, "Kraut", n=8)
    lb.saeule(arbeit, st, (3.0, -5.0, 0), 0.18, 0, 0.42, "Kupfer", n=8)            # Gießkanne
    arbeit.stab((3.15, -5.0, 0.32), (3.55, -5.0, 0.55), 0.05, 0.05, st["Kupfer"])
    for k in range(3):                                                            # Kröten am Teich
        a = 2.0 + 0.9 * k
        x, y = mx + r * 1.05 * math.cos(a), my + r * 0.85 * math.sin(a)
        arbeit.quader((x, y, 0.08), (0.24, 0.3, 0.14), st["Moos"], ohne=("-z",))

    # ── Leerstand ─────────────────────────────────────────────────────────────
    lb.vernageln(leer, st, h["oeffnungen"])
    for x0, y0, x1, y1 in BEETE:
        for k in range(4):
            lb.busch(leer, st, (x0 + 0.4 + (x1 - x0 - 0.8) * (k % 2), y0 + 0.8 + 1.8 * k), 0.3, 0.55, "Gras", "Moos",
                     n=5, saat=k + int(x0 * 7))
    lb.unkraut(leer, st, [(-3.6, -2.0), (-5.0, -6.6), (0.0, -6.0), (4.4, -6.2), (8.0, 0.0)], saat=7)

    anker = [bs.anker("Anker_Tuer", tuple(h["tuer"])), bs.anker("Anker_Karren", (-3.5, -7.9, 0.0))]
    return lb.knoten(haus, arbeit, fahne, leer, anker)


def trockengestell(netz, st, fuss):
    """Zwei Böcke, eine Stange, daran hängen Kräuterbündel."""
    x, y = fuss.x, fuss.y
    for dx in (-1.4, 1.4):
        for s in (-1, 1):
            netz.stab((x + dx + 0.04 * s, y - s * 0.4, 0), (x + dx + 0.04 * s, y + s * 0.1, 1.9), 0.08, 0.08, st["Holz"],
                      quer=(1, 0, 0))
    netz.stab((x - 1.6, y - 0.15, 1.82), (x + 1.6, y - 0.15, 1.82), 0.07, 0.07, st["Holz"])
    for k in range(7):
        bx = x - 1.1 + k * 0.37
        lb.kegel(netz, (bx, y - 0.15), 0.13, 1.78, 1.1, st["Heu" if k % 2 else "Kraut"], n=5)


def main():
    lb.ausgeben(NAME, FLAECHE, bauen(), ziel_hoehe=2.0, ausschnitt=26.0)


if __name__ == "__main__":
    main()
