"""Holzschlag des Tischlers (Umland): Holz aus dem Wald. Fläche 22 × 18 m.

    blender -b --python tools/modelle/holzschlag.py        oder
    python tools/modelle/holzschlag.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/umland/holzschlag.glb und Bilder nach docs/modelle/.

Objekte im GLB (siehe landbau.py):
    Haus        Blockhütte mit Schindeldach, Tannen am Rand, Baumstümpfe, Reisighaufen
    Arbeit      Holzpolter, Sägebock mit Stamm und Säge, Hackklotz mit Axt, Scheitholzstapel, gefällte Tanne
    Fahne       an der Hütte
    Leerstand   Bretter vor Tür und Fenstern, Unkraut zwischen den Stümpfen
    Anker_Tuer  vor der Hütte, Anker_Karren am Polter

Aufbau (Blender: Z oben, Weg bei -Y; Fläche x ±11, y ±9): hinten Wald, in der Mitte der Schlag mit Stümpfen,
links vorn die Blockhütte, rechts vorn das Polter am Weg, damit der Karren dort laden kann.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "holzschlag"
FLAECHE = (22.0, 18.0)


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus, arbeit, fahne, leer = bs.Netz("Haus"), bs.Netz("Arbeit"), bs.Netz("Fahne"), bs.Netz("Leerstand")

    # ── Boden, Wald, Stümpfe ──────────────────────────────────────────────────
    lb.flaeche(haus, st, [(-11, -9), (11, -9), (11, 2.5), (4, 4.0), (-4, 3.4), (-11, 2.0)], 0.012, "Erde hell")
    for i, (x, y, hoehe) in enumerate(((-8.8, 6.9, 9.0), (-5.9, 6.2, 7.6), (-2.8, 7.0, 9.6), (0.6, 6.6, 8.2),
                                       (3.9, 7.0, 9.2), (6.9, 6.4, 7.8), (8.9, 7.0, 8.8), (-8.4, 3.8, 6.4),
                                       (8.9, 3.6, 6.8), (-1.4, 4.8, 6.2))):
        lb.tanne(haus, st, (x, y), hoehe=hoehe, r=1.25 + hoehe * 0.06, saat=i)
    for x, y, r in ((-1.0, 1.4, 0.38), (2.4, 2.6, 0.45), (5.2, 1.2, 0.36), (-3.6, 2.4, 0.42), (0.8, -1.6, 0.34),
                    (3.4, -0.2, 0.4), (7.6, 2.6, 0.38)):
        lb.stumpf(haus, st, (x, y), r=r, h=0.45 + r * 0.4)
    for k, (x, y) in enumerate(((7.8, -0.4), (8.4, -0.9))):     # Reisighaufen
        lb.haufen(haus, st, (x + 0.3 * k, y), 1.3 - 0.3 * k, 0.7, "Tanne", "Holz alt", saat=30 + k)

    # ── Blockhütte links vorn ─────────────────────────────────────────────────
    h = lb.huette(haus, st, -10.0, -4.4, -6.8, -2.2, 2.3, wand="block", deckung=bs.SCHINDEL, neigung=38,
                  tuer_u=1.3, fenster_vorn=(-1.2,), fenster_seite=(("rechts", -0.5),))
    xs, ys = -8.6, -3.3                                          # Schornstein aus Bruchstein
    z0 = h["dach"]["oben_hinten"](ys) - 0.3
    haus.quader((xs, ys, z0 + 0.7), (0.5, 0.5, 1.4), st["Bruchstein dunkel"], ohne=("-z",))
    bs.fahne(fahne, st, (-10.4, -7.1, 0), hoehe=4.4, richtung=1)

    # ── Arbeit ────────────────────────────────────────────────────────────────
    lb.polter(arbeit, st, (5.0, -5.6), 5.2, reihen=4, r=0.3)
    lb.stamm(arbeit, st, (-1.2, 3.2, 0.32), (5.6, 4.6, 0.32), 0.3)          # gefällte Tanne mit Krone
    lb.haufen(arbeit, st, (6.6, 4.9), 1.2, 0.8, "Tanne", "Tanne hell", saat=40)            # abgeschlagene Äste
    saegebock(arbeit, st, Vector((0.2, -4.0, 0)))
    lb.stumpf(arbeit, st, (-2.6, -5.4), r=0.36, h=0.6)                       # Hackklotz mit Axt
    lb.werkzeug(arbeit, st, (-2.5, -5.4, 0.6), laenge=0.75, winkel=25, art="axt")
    for lage in range(4):                                                     # Scheitholz an der Hütte
        arbeit.quader((-3.95, -4.4, 0.13 + 0.25 * lage), (0.42, 3.2 - 0.25 * lage, 0.24), st["Holz alt"], ohne=("-z", "-x"))
    arbeit.vieleck_nach([(-4.161, -4.4 + d, z) for d, z in ((-1.55, 0.02), (1.55, 0.02), (1.25, 0.99), (-1.25, 0.99))],
                        (1, 0, 0), st["Scheitholz"])

    # ── Leerstand ─────────────────────────────────────────────────────────────
    lb.vernageln(leer, st, h["oeffnungen"])
    lb.unkraut(leer, st, [(-0.6, 1.0), (2.0, 2.2), (5.6, 0.8), (1.2, -2.0), (3.0, -4.6), (6.0, -6.0), (-2.6, -5.0),
                          (-4.0, -3.4), (-1.0, -7.0), (8.0, -3.0), (3.8, 0.3)], saat=5)

    anker = [bs.anker("Anker_Tuer", tuple(h["tuer"])), bs.anker("Anker_Karren", (5.0, -8.0, 0.0))]
    return lb.knoten(haus, arbeit, fahne, leer, anker)


def saegebock(netz, st, fuss):
    """Sägebock aus zwei Holzkreuzen, darauf ein Stamm mit Bügelsäge."""
    x, y = fuss.x, fuss.y
    for dx in (-0.8, 0.8):
        for s in (-1, 1):                     # die beiden Beine eines Kreuzes leicht versetzt (sonst flimmern sie)
            netz.stab((x + dx + s * 0.05, y - s * 0.45, 0), (x + dx + s * 0.05, y + s * 0.45, 1.0), 0.09, 0.09, st["Holz"],
                      quer=(1, 0, 0))
    lb.stamm(netz, st, (x - 1.6, y, 0.92), (x + 1.7, y, 0.92), 0.22)
    netz.stab((x + 0.25, y - 0.32, 1.45), (x + 0.25, y - 0.32, 0.75), 0.05, 0.04, st["Holz"], quer=(1, 0, 0))
    netz.stab((x + 0.25, y - 0.32, 1.42), (x + 0.25, y + 0.32, 1.42), 0.05, 0.04, st["Holz"], quer=(1, 0, 0))
    netz.stab((x + 0.25, y + 0.32, 1.45), (x + 0.25, y + 0.32, 0.75), 0.05, 0.04, st["Holz"], quer=(1, 0, 0))
    netz.stab((x + 0.25, y - 0.32, 0.8), (x + 0.25, y + 0.32, 0.8), 0.07, 0.01, st["Eisen"], quer=(1, 0, 0))


def main():
    lb.ausgeben(NAME, FLAECHE, bauen(), ziel_hoehe=2.5, ausschnitt=29.0)


if __name__ == "__main__":
    main()
