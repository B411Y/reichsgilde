"""Grube des Schmieds (Umland): Eisen und Silber aus dem Berg. Fläche 22 × 18 m.

    blender -b --python tools/modelle/grube.py        oder
    python tools/modelle/grube.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/grube.glb und Bilder nach docs/modelle/.

Objekte im GLB (siehe landbau.py):
    Haus        Felshang mit Stollenmundloch und Türstock, Gleis aus Bohlen, Abraumhalde, Schuppen
    Arbeit      Förderwagen mit Erz, Erz- und Silberhaufen, Werkzeug, Schubkarre, Grubenlampe
    Fahne       am Schuppen
    Leerstand   Bretter vor Stollen, Tür und Fenstern, umgestürzter Förderwagen, Unkraut
    Anker_Tuer  vor dem Stollen (dort gehen die Knappen ein und aus), Anker_Karren auf dem Hof vorn

Aufbau (Blender: Z oben, Weg bei -Y; Fläche x ±11, y ±9): hinten der Felshang mit dem Stollen in der Mitte,
davon führt das Gleis nach vorn auf den Hof. Links vorn der Schuppen, rechts die Halde. Der große Berg dahinter
bleibt im Spiel (er ist viel größer als die Fläche).
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Oeffnung, Vector  # noqa: E402

NAME = "grube"
FLAECHE = (22.0, 18.0)
SX = 1.0                      # Mitte des Stollens
Y_HANG = 2.6                  # Vorderkante der Felswand am Stollen


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus, arbeit, fahne, leer = bs.Netz("Haus"), bs.Netz("Arbeit"), bs.Netz("Fahne"), bs.Netz("Leerstand")

    # ── Hof und Felshang ──────────────────────────────────────────────────────
    lb.flaeche(haus, st, [(-11, -9), (11, -9), (11, 1.0), (6, 3.2), (-5, 3.2), (-11, 1.5)], 0.012, "Erde hell")
    # Wand um den Stollen: oben zurückgeneigt, die Seiten verdecken die Felsen
    haus.prisma([(Y_HANG, 0), (Y_HANG + 3.2, 0), (Y_HANG + 3.2, 4.4), (Y_HANG + 0.9, 3.9)], SX - 2.2, SX + 2.2,
                st["Fels dunkel"])
    for i, (x, y, r, h) in enumerate(((-3.6, 4.6, 3.0, 5.6), (5.8, 4.8, 3.0, 6.2), (SX, 5.6, 3.0, 7.4),
                                      (-8.0, 5.8, 2.4, 4.2), (8.8, 6.4, 1.8, 3.6), (-1.0, 6.8, 1.8, 6.0),
                                      (3.6, 6.8, 1.7, 6.8))):
        lb.felsbrocken(haus, st, (x, y), r, h, saat=i + 3)
    for i, (x, y, r, h) in enumerate(((-6.0, 2.4, 1.0, 0.8), (8.6, 3.6, 0.8, 0.6), (-1.6, 2.6, 0.6, 0.5))):
        lb.felsbrocken(haus, st, (x, y), r, h, saat=20 + i)
    # Stollenmundloch: dunkle Öffnung, Türstock aus Rundholz
    haus.quader((SX, Y_HANG + 0.23, 1.25), (1.9, 0.5, 2.5), st["Loch"], ohne=("-z",))
    for s in (-1, 1):
        lb.saeule(haus, st, (SX + s * 1.08, Y_HANG - 0.12, 0), 0.14, 0.0, 2.55, "Holz", enden="Scheitholz", n=7)
    lb.stamm(haus, st, (SX - 1.45, Y_HANG - 0.14, 2.68), (SX + 1.45, Y_HANG - 0.14, 2.68), 0.17)
    # Gleis aus Bohlen vom Stollen auf den Hof
    for k in range(9):
        y = Y_HANG - 0.5 - k * 0.75
        haus.quader((SX, y, 0.05), (1.5, 0.22, 0.07), st["Holz alt"], ohne=("-z",))
    for s in (-1, 1):
        haus.stab((SX + s * 0.48, Y_HANG + 0.1, 0.12), (SX + s * 0.48, Y_HANG - 6.7, 0.12), 0.1, 0.08, st["Holz"],
                  quer=(0, 0, 1))
    # Abraumhalde rechts
    lb.haufen(haus, st, (7.2, 0.4), 3.1, 1.5, "Bruchstein dunkel", "Kohle", saat=7)
    lb.haufen(haus, st, (8.6, -2.2), 1.6, 0.8, "Bruchstein dunkel", "Fels dunkel", saat=8)

    # ── Schuppen links vorn ───────────────────────────────────────────────────
    h = lb.huette(haus, st, -10.2, -4.6, -6.6, -2.2, 2.4, wand="bretter", deckung=bs.SCHIEFER, neigung=40,
                  tuer_u=1.2, fenster_vorn=(-1.3,), fenster_seite=(("rechts", -0.6),))
    bs.fass(haus, st, Vector((-4.1, -6.1, 0)), r=0.3, h=0.8)
    bs.fahne(fahne, st, (-10.5, -6.9, 0), hoehe=4.6, richtung=1)

    # ── Arbeit ────────────────────────────────────────────────────────────────
    hunt(arbeit, st, Vector((SX, -1.4, 0.16)), ladung="Erz")
    lb.haufen(arbeit, st, (4.4, -4.6), 1.5, 0.75, "Erz", "Fels dunkel", saat=11)
    lb.haufen(arbeit, st, (2.6, -6.6), 0.9, 0.5, "Silbererz", "Fels", saat=12)
    lb.schubkarre(arbeit, st, (5.6, -2.2, 0), dreh=2.4, ladung="Erz")
    for i, x in enumerate((-4.0, -3.7)):
        lb.werkzeug(arbeit, st, (x, -2.0, 0), laenge=1.15, winkel=-14 + 9 * i, art="hacke" if i == 0 else "schaufel")
    # Grubenlampe am Türstock
    arbeit.quader((SX - 1.08, Y_HANG - 0.36, 2.1), (0.2, 0.2, 0.28), st["Glut"])
    arbeit.quader((SX - 1.08, Y_HANG - 0.36, 2.28), (0.26, 0.26, 0.06), st["Eisen"])
    bs.fass(arbeit, st, Vector((-3.7, -3.1, 0)), r=0.3, h=0.8)

    # ── Leerstand ─────────────────────────────────────────────────────────────
    M = bs.wandmatrix((SX, Y_HANG - 0.05, 0), "vorne")
    bs.bretter(leer, M, Oeffnung("tuer", 0.0, 0.0, 1.9, 2.4), st, 3)
    lb.vernageln(leer, st, h["oeffnungen"])
    hunt(leer, st, Vector((SX + 1.6, -2.6, 0.0)), kipp=True)
    lb.unkraut(leer, st, [(SX + 0.2, -1.0), (SX - 0.4, -3.4), (SX + 0.5, -5.0), (-4.3, -2.6), (-7.0, -7.0), (3.0, -6.0),
                          (6.0, -3.5), (-2.0, 1.5), (4.0, 2.0)], saat=4)

    anker = [bs.anker("Anker_Tuer", (SX, Y_HANG - 0.5, 0.0)), bs.anker("Anker_Karren", (SX + 2.5, -7.4, 0.0))]
    return lb.knoten(haus, arbeit, fahne, leer, anker)


def hunt(netz, st, boden, ladung=None, kipp=False):
    """Förderwagen (Hunt): Holzkasten mit Eisenbändern auf vier Rädern; `kipp`: umgestürzt auf der Seite."""
    import math
    from bausteine import Matrix
    t = Matrix.Translation(boden)
    if kipp:
        t = t @ Matrix.Translation((0.3, 0, 0.47)) @ Matrix.Rotation(math.radians(90), 4, "Y") @ Matrix.Translation((0, 0, -0.6))
    netz.quader((0, 0, 0.6), (0.9, 1.3, 0.6), st["Holz"], t)
    for z in (0.42, 0.78):
        netz.quader((0, 0, z), (0.94, 1.34, 0.06), st["Eisen"], t)
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader((sx * 0.47, sy * 0.4, 0.2), (0.06, 0.34, 0.34), st["Eisen"], t)
    if ladung:
        lb.haufen(netz, st, t @ Vector((0, 0, 0.88)), 0.45, 0.28, ladung, "Fels dunkel", saat=2, n=6)


def main():
    lb.ausgeben(NAME, FLAECHE, bauen(), ziel_hoehe=2.5, ausschnitt=29.0)


if __name__ == "__main__":
    main()
