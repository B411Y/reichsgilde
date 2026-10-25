"""Steinbruch des Steinmetzen (Umland): Bruchstein und Granit. Fläche 22 × 18 m.

    blender -b --python tools/modelle/steinbruch.py        oder
    python tools/modelle/steinbruch.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/umland/steinbruch.glb und Bilder nach docs/modelle/.

Objekte im GLB (siehe landbau.py):
    Haus        abgetreppte Bruchwand mit hellen Schnittflächen, Felsen, Schuppen aus Bruchstein, Splitt
    Arbeit      Galgenkran mit Seil und Steinzange, gestapelte Quader, Keile und Schlägel, Schubkarre
    Fahne       am Schuppen
    Leerstand   Bretter vor Tür und Fenstern, der Kran fehlt, Unkraut auf den Stufen
    Anker_Tuer  vor dem Schuppen, Anker_Karren vor den Quadern

Aufbau (Blender: Z oben, Weg bei -Y; Fläche x ±11, y ±9): hinten rechts die Bruchwand in drei Stufen, davor der Kran,
links vorn der Schuppen, rechts vorn die fertigen Quader am Weg.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "steinbruch"
bs.FARBEN.update({
    "Schnittstein": (0.63, 0.61, 0.56),    # frisch gebrochene Wand
    "Splitt": (0.56, 0.54, 0.50),
})
FLAECHE = (22.0, 18.0)
# Bruchwand: drei Stufen, jede weiter hinten und höher (Vorderkante y, Höhe)
STUFEN = ((-0.4, 1.6), (2.0, 3.4), (4.2, 5.4))
BX0, BX1 = -4.0, 8.4


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus, arbeit, fahne, leer = bs.Netz("Haus"), bs.Netz("Arbeit"), bs.Netz("Fahne"), bs.Netz("Leerstand")

    # ── Boden mit Splitt, Bruchwand ───────────────────────────────────────────
    lb.flaeche(haus, st, [(-11, -9), (11, -9), (11, -0.8), (-11, -0.8)], 0.012, "Erde hell")
    lb.flaeche(haus, st, [(-1.5, -5.5), (7.5, -6.5), (9.6, -1.4), (0.5, -1.0)], 0.02, "Splitt")
    for i, (y, h) in enumerate(STUFEN):
        x0, x1 = BX0 + 0.8 * i, BX1 - 0.3 * i
        # Stufe: helle Schnittflächen vorn und oben, dunkler Fels an den Seiten
        hinten = 8.6 - 0.25 * i                               # Rückseiten gestaffelt (sonst flimmern sie)
        haus.quader(((x0 + x1) / 2, (y + hinten) / 2, h / 2), (x1 - x0, hinten - y, h), st["Fels dunkel"], ohne=("-z", "-y"))
        haus.vieleck([(x0, y, 0), (x1, y, 0), (x1, y, h), (x0, y, h)], st["Schnittstein"])
        if i == len(STUFEN) - 1:                             # oben wächst Gras
            lb.flaeche(haus, st, [(x0 + 0.3, y + 0.6), (x1 - 0.2, y + 0.4), (x1 - 0.2, hinten - 0.2), (x0 + 0.3, hinten - 0.2)],
                       h + 0.01, "Gras")
        for k in range(3):                                    # lose Brocken an der Kante
            lb.felsbrocken(haus, st, (x0 + 1.2 + k * (x1 - x0 - 2.4) / 2, y + 0.55, h - 0.05), 0.5 + 0.1 * k, 0.45,
                           saat=70 + 3 * i + k)
    for i, (x, y, r, h) in enumerate(((-6.6, 5.2, 2.8, 4.8), (-3.6, 7.0, 2.0, 6.4), (9.0, 4.6, 1.5, 6.0),
                                      (-8.8, 1.8, 1.6, 2.2), (9.3, 0.4, 1.1, 2.4), (2.4, 7.6, 1.3, 6.4))):
        lb.felsbrocken(haus, st, (x, y), r, h, saat=50 + i)
    for i, (x, y, r) in enumerate(((1.0, -2.2, 0.5), (6.8, -1.9, 0.4), (-2.4, -1.8, 0.45))):
        lb.felsbrocken(haus, st, (x, y), r, r, saat=60 + i, stoff="Bruchstein", stoff2="Fels")

    # ── Schuppen aus Bruchstein links vorn ────────────────────────────────────
    h = lb.huette(haus, st, -10.2, -4.8, -7.0, -2.8, 2.4, wand="stein", deckung=bs.SCHIEFER, neigung=38,
                  tuer_u=1.2, fenster_vorn=(-1.2,), giebel_fenster=False)
    bs.fahne(fahne, st, (-10.6, -7.4, 0), hoehe=4.4, richtung=1)

    # ── Arbeit: Kran, Quader, Werkzeug ────────────────────────────────────────
    kran(arbeit, st, Vector((2.6, -0.2, 0)))
    for i, (x, y, z, sx, sy, sz) in enumerate(((5.0, -6.4, 0, 1.4, 0.9, 0.8), (6.5, -6.4, 0, 1.3, 0.9, 0.8),
                                                (5.7, -6.4, 0.8, 1.5, 0.85, 0.7), (8.0, -5.6, 0, 1.0, 1.0, 1.0),
                                                (3.4, -6.8, 0, 0.9, 0.7, 0.6), (8.1, -7.4, 0, 1.2, 0.8, 0.6))):
        lb.kiste(arbeit, st, (x, y, z), (sx, sy, sz), stoff="Bruchstein" if i % 3 else "Fels", dreh=0.05 * (i % 2))
    lb.schubkarre(arbeit, st, (-0.6, -4.2, 0), dreh=0.5, ladung="Bruchstein")
    for k, x in enumerate((-4.3, -4.0)):
        lb.werkzeug(arbeit, st, (x, -3.2 - 0.15 * k, 0), laenge=1.0, winkel=-12 + 10 * k, art="hacke")
    for x, y in ((4.1, -1.4), (4.6, -1.2)):                   # Keile in einem Spalt vor der unteren Stufe
        arbeit.quader((x, y, 0.15), (0.12, 0.3, 0.3), st["Eisen"])

    # ── Leerstand ─────────────────────────────────────────────────────────────
    lb.vernageln(leer, st, h["oeffnungen"])
    lb.unkraut(leer, st, [(-1.0, 0.6), (3.0, 2.6), (6.0, 4.6), (0.0, -3.0), (5.0, -4.0), (-3.0, -6.0), (8.0, -2.0),
                          (-4.4, -3.5), (2.0, -7.0), (7.0, 0.4)], saat=6)

    anker = [bs.anker("Anker_Tuer", tuple(h["tuer"])), bs.anker("Anker_Karren", (6.0, -8.2, 0.0))]
    return lb.knoten(haus, arbeit, fahne, leer, anker)


def kran(netz, st, fuss):
    """Dreibein: drei Stangen, oben gebunden, mit Rolle, Seil, Steinzange und gehobenem Quader; Haspel daneben."""
    x, y = fuss.x, fuss.y
    holz = st["Holz"]
    kopf = Vector((x, y + 0.4, 5.4))
    for dx, dy in ((-1.7, -1.0), (1.7, -1.0), (0.0, 2.2)):
        netz.stab((x + dx, y + dy, 0), kopf + Vector((dx * 0.04, dy * 0.04, 0.2)), 0.16, 0.16, holz)
    netz.quader(kopf + Vector((0, 0, -0.15)), (0.3, 0.3, 0.3), st["Seil"])                # Bund und Rolle
    netz.stab(kopf + Vector((0, 0, -0.3)), kopf + Vector((0, 0, -2.75)), 0.03, 0.03, st["Seil"])
    for s in (-1, 1):
        netz.stab(kopf + Vector((s * 0.06, 0, -2.75)), kopf + Vector((s * 0.3, 0, -3.15)), 0.05, 0.05, st["Eisen"])
    lb.kiste(netz, st, kopf + Vector((0, 0, -3.85)), (1.0, 0.7, 0.6), stoff="Bruchstein")
    # Haspel: zwei Böcke, Welle mit Kurbel; das Seil läuft schräg hinauf
    hx, hy = x - 2.6, y - 1.6
    for dx in (-0.5, 0.5):
        netz.quader((hx + dx, hy, 0.4), (0.12, 0.5, 0.8), holz, ohne=("-z",))
    lb.stamm(netz, st, (hx - 0.7, hy, 0.75), (hx + 0.7, hy, 0.75), 0.16)
    netz.stab((hx + 0.62, hy, 0.75), (hx + 0.62, hy - 0.45, 1.05), 0.05, 0.05, st["Eisen"])
    netz.stab((hx + 0.1, hy + 0.1, 0.9), kopf + Vector((-0.05, -0.05, -0.1)), 0.03, 0.03, st["Seil"])


def main():
    lb.ausgeben(NAME, FLAECHE, bauen(), ziel_hoehe=2.8, ausschnitt=29.0)


if __name__ == "__main__":
    main()
