"""Klosterhof des Predigers (Umland): Lumpen und Wachs. Fläche 22 × 18 m.

    blender -b --python tools/modelle/klosterhof.py        oder
    python tools/modelle/klosterhof.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/umland/klosterhof.glb und Bilder nach docs/modelle/.

Objekte im GLB (siehe landbau.py):
    Haus        Zellenhaus aus Bruchstein mit Schieferdach und Dachreiter (Glocke), Gemüsebeete, Bienenstand
                mit Pultdach, Wegkreuz, Trockenmauer, Linde
    Arbeit      Kohl in den Beeten, sechs Bienenkörbe, Leine mit Lumpen zum Trocknen, Lumpenbündel, Wachsblöcke
    Fahne       am Zellenhaus
    Leerstand   Bretter vor Tür und Fenstern, Unkraut in den Beeten
    Anker_Tuer  vor dem Zellenhaus, Anker_Karren am Tor

Aufbau (Blender: Z oben, Weg bei -Y; Fläche x ±11, y ±9): hinten das Zellenhaus mit der Traufe zum Hof,
davor die Beete im Kreuz um das Wegkreuz, rechts der Bienenstand, links die Lumpenleine. Eine Trockenmauer
fasst den Hof ein, vorn das offene Tor zum Weg.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "klosterhof"
FLAECHE = (22.0, 18.0)
BEETE = [(-5.4, -6.4, -1.0, -3.6), (1.0, -6.4, 5.4, -3.6), (-5.4, -2.4, -1.0, 0.4), (1.0, -2.4, 5.4, 0.4)]


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus, arbeit, fahne, leer = bs.Netz("Haus"), bs.Netz("Arbeit"), bs.Netz("Fahne"), bs.Netz("Leerstand")

    lb.flaeche(haus, st, lb.rechteck(-11, -9, 11, 9), 0.008, "Gras")
    lb.flaeche(haus, st, lb.rechteck(-0.7, -9.0, 0.7, 2.0), 0.014, "Erde hell")          # Kreuzweg
    lb.flaeche(haus, st, lb.rechteck(-6.0, -3.3, 6.0, -2.7), 0.016, "Erde hell")
    lb.flaeche(haus, st, lb.rechteck(-8.0, 1.6, 8.0, 2.4), 0.012, "Erde hell")
    for x0, y0, x1, y1 in BEETE:
        lb.beet(haus, st, x0, y0, x1, y1, z=0.16)
        for y in (y0 - 0.05, y1 + 0.05):
            haus.quader(((x0 + x1) / 2, y, 0.1), (x1 - x0 + 0.2, 0.1, 0.2), st["Bruchstein"], ohne=("-z",))
    # Wegkreuz in der Mitte
    haus.quader((0, -3.0, 0.15), (0.7, 0.7, 0.3), st["Bruchstein"], ohne=("-z",))
    haus.quader((0, -3.0, 1.55), (0.18, 0.18, 2.5), st["Holz"])
    haus.quader((0, -3.0, 2.25), (1.1, 0.16, 0.16), st["Holz"])
    # Trockenmauer, vorn offen (Tor)
    bs.trockenmauer(haus, st, [(-1.6, -8.6), (-10.6, -8.6)], hoehe=0.9)
    bs.trockenmauer(haus, st, [(-10.6, -8.1), (-10.6, 8.6)], hoehe=0.86)
    bs.trockenmauer(haus, st, [(1.6, -8.6), (10.6, -8.6)], hoehe=0.9)
    bs.trockenmauer(haus, st, [(10.6, -8.1), (10.6, 8.6)], hoehe=0.86)
    for x in (-1.6, 1.6):                                                    # Torpfeiler
        haus.quader((x, -8.6, 0.75), (0.6, 0.6, 1.5), st["Bruchstein"])
        haus.quader((x, -8.6, 1.56), (0.7, 0.7, 0.12), st["Sandstein"])
    bs.linde(haus, st, Vector((-8.4, -5.6, 0)), hoehe=6.4, r=2.2, saat=5)

    # ── Zellenhaus hinten ─────────────────────────────────────────────────────
    h = lb.huette(haus, st, -6.4, 4.4, 3.2, 7.8, 3.0, wand="stein", deckung=bs.SCHIEFER, neigung=45,
                  tuer_u=-0.6, fenster_vorn=(-3.6, 1.8, 3.8), fenster_seite=(("links", 0.0), ("rechts", 0.0)),
                  giebel_fenster=True)
    bs.dachreiter(haus, st, (-2.4, h["dach"]["y_first"], h["dach"]["z_first"]), spitze="Schiefer", koerper="Putz",
                  ecken="Holz")
    bs.fahne(fahne, st, (4.8, 2.9, 0), hoehe=4.4, richtung=-1)

    # ── Bienenstand rechts ────────────────────────────────────────────────────
    bx0, bx1, by = 6.6, 10.0, -2.0
    for x, hoehe in ((bx0, 2.0), (bx1, 1.62)):                       # Pfosten, das Dach fällt nach rechts
        for y in (by - 0.5, by + 0.7):
            haus.quader((x, y, hoehe / 2), (0.14, 0.14, hoehe), st["Holz"], ohne=("-z",))
    bs.pultdach(haus, st, bx0 - 0.3, bx1 + 0.3, by - 0.8, by + 1.0, 2.1, 1.66, stoff="Holz alt")
    haus.quader(((bx0 + bx1) / 2, by, 0.55), (bx1 - bx0 + 0.3, 0.9, 0.12), st["Holz alt"])

    # ── Arbeit ────────────────────────────────────────────────────────────────
    for i, (x0, y0, x1, y1) in enumerate(BEETE):
        for j in range(3):
            y = y0 + (y1 - y0) * (j + 0.5) / 3
            lb.pflanzreihe(arbeit, st, (x0 + 0.4, y), (x1 - 0.4, y), 0.75, 0.34, 0.45, "Kohl", "Kraut", z=0.16,
                           saat=i * 5 + j, n=6)
    for k in range(4):
        lb.bienenkorb(arbeit, st, (bx0 + 0.55 + k * 0.78, by, 0.61), r=0.3, h=0.62)
    # Leine mit Lumpen links
    for x in (-9.6, -6.2):
        arbeit.quader((x, 0.6, 0.9), (0.1, 0.1, 1.8), st["Holz"], ohne=("-z",))
    arbeit.stab((-9.6, 0.6, 1.72), (-6.2, 0.6, 1.72), 0.03, 0.03, st["Seil"])
    for k, (x, b, h_) in enumerate(((-9.0, 0.6, 0.8), (-8.2, 0.5, 0.65), (-7.5, 0.7, 0.9), (-6.8, 0.45, 0.6))):
        arbeit.quader((x, 0.6 + 0.01 * k, 1.7 - h_ / 2), (b, 0.03, h_), st["Lumpen" if k % 2 else "Segeltuch"])
    for k, (x, y) in enumerate(((-7.6, 2.6), (-6.9, 2.8))):
        lb.haufen(arbeit, st, (x, y), 0.45, 0.5, "Lumpen", "Segeltuch", saat=100 + k, n=6)
    for k in range(3):                                                  # Wachsblöcke auf einem Brett
        arbeit.quader((7.4 + 0.45 * k, -4.4, 0.32), (0.34, 0.24, 0.2), st["Heu"])
    arbeit.quader((7.85, -4.4, 0.11), (1.6, 0.5, 0.1), st["Holz alt"])
    for x in (7.2, 8.5):
        arbeit.quader((x, -4.4, 0.03), (0.12, 0.4, 0.06), st["Holz"])

    # ── Leerstand ─────────────────────────────────────────────────────────────
    lb.vernageln(leer, st, h["oeffnungen"])
    for i, (x0, y0, x1, y1) in enumerate(BEETE):
        for k in range(3):
            lb.busch(leer, st, (x0 + 0.8 + k * (x1 - x0 - 1.6) / 2, (y0 + y1) / 2 + 0.5 * (k % 2 - 0.5)), 0.32, 0.55,
                     "Gras", "Moos", n=5, saat=30 + i * 3 + k)
    lb.unkraut(leer, st, [(7.0, -1.6), (9.0, -2.2), (-8.0, 1.0), (0.4, -7.0), (-3.0, 2.0), (3.0, 2.2)], saat=11)

    anker = [bs.anker("Anker_Tuer", tuple(h["tuer"])), bs.anker("Anker_Karren", (0.0, -8.2, 0.0))]
    return lb.knoten(haus, arbeit, fahne, leer, anker)


def main():
    lb.ausgeben(NAME, FLAECHE, bauen(), ziel_hoehe=2.5, ausschnitt=29.0)


if __name__ == "__main__":
    main()
