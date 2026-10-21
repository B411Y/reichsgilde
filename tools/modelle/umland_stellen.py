"""Arbeitsstellen der Einwohner im Umland: Weide, Feld und Wald – drei Teile in einem Modell.

    blender -b --python tools/modelle/umland_stellen.py        oder
    python tools/modelle/umland_stellen.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/umland/umland_stellen.glb und Bilder nach docs/modelle/.

Objekte im GLB, jedes mit eigenem Ursprung im Nullpunkt (Mitte der Stelle, wie `Umlandstellen` im Spiel):
    Weide   Koppel 14 × 10 m mit Zaun und Gatter, Tränke, zwei Büsche, Weidestein
    Feld    Acker 14 × 9 m in Furchen, Korn und Kraut im Wechsel (wie bisher im Spiel), Vogelscheuche
    Wald    Tannen und eine Linde, Reisigbündel und ein Stapel Brennholz am Weg
„Anker_Tuer“ vorn in der Mitte: dort kommen die Einwohner an (bei allen drei Teilen dieselbe Stelle).
Die Stellen gehören keinem Haus: keine Hausfarbe, keine Zustände (wie Sonderbauten, `pruefen.py --sonderbau`).
Das Spiel setzt jedes Teil an seine Stelle; der Weg dorthin kommt von vorn (-Y in Blender, +Z im Spiel).
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

NAME = "umland_stellen"
FLAECHE = (16.0, 14.0)           # größte Ausdehnung der Teile (der Wald)


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    weide, feld, wald = bs.Netz("Weide"), bs.Netz("Feld"), bs.Netz("Wald")

    # ── Weide ─────────────────────────────────────────────────────────────────
    lb.flaeche(weide, st, lb.rechteck(-7, -5, 7, 5), 0.01, "Gras")
    bs.zaun(weide, st, [(-1.0, -5.0), (-7.0, -5.0), (-7.0, 5.0), (7.0, 5.0), (7.0, -5.0), (1.0, -5.0)], hoehe=1.0,
            abstand=2.0)
    weide.stab((1.0, -5.0, 0.3), (-0.4, -6.0, 0.3), 0.08, 0.06, st["Holz alt"], quer=(0, 0, 1))
    weide.stab((1.0, -5.0, 0.85), (-0.4, -6.0, 0.85), 0.08, 0.06, st["Holz alt"], quer=(0, 0, 1))
    weide.stab((-0.4, -6.0, 0.0), (-0.4, -6.0, 1.0), 0.08, 0.08, st["Holz alt"])
    weide.quader((4.6, 2.6, 0.25), (2.2, 0.7, 0.5), st["Holz"], ohne=("-z",))       # Tränke
    weide.vieleck_nach([(3.6, 2.35, 0.45), (5.6, 2.35, 0.45), (5.6, 2.85, 0.45), (3.6, 2.85, 0.45)], (0, 0, 1), st["Wasser"])
    lb.busch(weide, st, (-5.2, 3.4), 1.0, 1.6, saat=1)
    lb.busch(weide, st, (5.6, -3.4), 0.8, 1.2, saat=2)
    lb.felsbrocken(weide, st, (-2.0, 1.4), 0.6, 0.5, saat=3)
    for k, (x, y) in enumerate(((-3.0, -2.0), (1.0, 3.0), (3.0, -1.0), (-5.0, -3.6), (2.4, 0.6))):
        bs.buschel(weide, st, Vector((x, y, 0.01)), groesse=0.5, stoff="Gras" if k % 2 else "Moos")

    # ── Feld ──────────────────────────────────────────────────────────────────
    lb.flaeche(feld, st, lb.rechteck(-7, -4.5, 7, 4.5), 0.012, "Erde")
    dreh = Matrix.Rotation(1.5708, 4, "Z")
    for i in range(6):
        x = -6.0 + i * 2.4
        hoch = 0.5 if i % 2 == 0 else 0.35
        feld.prisma([(-0.42, 0.01), (0.42, 0.01), (0.26, hoch), (-0.26, hoch)], -4.1 + 0.05 * i, 4.1 - 0.04 * i,
                    st["Korn" if i % 2 == 0 else "Kraut"], matrix=Matrix.Translation((x, 0, 0)) @ dreh)
    # Vogelscheuche
    feld.stab((5.6, 1.0, 0.0), (5.6, 1.0, 2.0), 0.08, 0.08, st["Holz"])
    feld.stab((4.9, 1.0, 1.55), (6.3, 1.0, 1.55), 0.07, 0.07, st["Holz"])
    feld.quader((5.6, 1.0, 1.3), (0.5, 0.3, 0.7), st["Lumpen"])
    feld.quader((5.6, 1.0, 1.95), (0.3, 0.28, 0.3), st["Heu"])
    lb.kegel(feld, (5.6, 1.0), 0.32, 2.08, 2.4, st["Stroh"], n=6)

    # ── Wald ──────────────────────────────────────────────────────────────────
    lb.flaeche(wald, st, [(-7.5, -6.5), (7.5, -6.5), (7.5, 6.5), (-7.5, 6.5)], 0.01, "Moos")
    for i, (x, y, h) in enumerate(((-5.0, -3.0, 8.0), (-1.0, -4.4, 10.0), (4.0, -4.0, 7.0), (5.4, 2.0, 9.0),
                                   (-4.0, 4.0, 9.0), (1.0, 5.0, 7.0), (-5.8, 1.0, 6.0))):
        lb.tanne(wald, st, (x, y), hoehe=h, r=1.2 + 0.07 * h, saat=40 + i)
    bs.linde(wald, st, Vector((1.6, 0.6, 0)), hoehe=6.6, r=2.0, saat=8)
    for k in range(3):                                                    # Reisigbündel
        lb.stamm(wald, st, (1.2 + 0.05 * k, -1.6 + 0.3 * k, 0.17), (3.4 - 0.1 * k, -1.5 + 0.3 * k, 0.17), 0.17, n=6)
    lb.polter(wald, st, (-1.6, -1.2), 1.4, reihen=3, r=0.16, quer=True)
    for k, (x, y) in enumerate(((-2.4, 2.0), (3.0, 3.0), (-3.0, -5.0), (5.0, -1.0))):
        lb.stumpf(wald, st, (x, y), r=0.3, h=0.4)

    # Ankunft der Einwohner: vorn in der Mitte, am Gatter der Weide (für alle drei Teile dieselbe Stelle)
    return [weide.objekt(), feld.objekt(), wald.objekt(), bs.anker("Anker_Tuer", (0.0, -5.4, 0.0))]


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte, einzeln=True)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=2.0, ausschnitt=24.0, schnell=schnell)
    teile = [o for o in objekte if o.type == "MESH"]
    bilder = [bs.rendern(f"{NAME}-{o.name.lower()}", lambda x, o=o: x == o) for o in teile]
    print(bs.nebeneinander(NAME, bilder, [o.name for o in teile]))
    for p in bilder:
        os.remove(p)


if __name__ == "__main__":
    main()
