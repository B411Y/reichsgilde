"""Neubau Rathaus, Raum 4: Kerker (Keller unter der Eingangshalle) - im Spiel ein Teil von "InteriorRoot".

    blender -b --python tools/modelle/neubau/rathaus_kerker.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/rathaus_kerker_innen.glb:
    Raum_Kerker         Kopfsteinpflaster, Kerkermauern mit Blendboegen, Balkendecke mit dem Loch der Kellertreppe, Kellertreppe aus Stein
                        (von der Kellertuer der Halle an der Rueckwand hinab), Trennmauern der zwei Zellen
    Ausstattung_Kerker  Stroh, Eimer, Kettenringe, Tisch und Schemel des Waechters mit Laterne, Waffenstaender, Faesser, Kisten, Fackeln
    Gegenstand_Zellengitter   anklickbar: das Stadtgericht (wie heute der Klick in den Kerker)
    Anker: Platz_Gefangener_1..3 (in den Zellen), Platz_Waechter, Schild_Kerker, Blick_Auge/_Ziel, Treppe_Kerker_Halle, Licht_*, Feuer_Fackel_*
"""
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rathaus_bib as B                                  # noqa: E402
import rathaus_masse as M                                # noqa: E402
from rathaus_bib import EZ, Teil, feld                   # noqa: E402

BI = B.BAENDER_RATHAUS_INNEN
X0, X1, Y0, Y1, ZB, ZD = M.RAEUME["kerker"][:6]
GITTER_X = X0 + 1.90
ZELLEN = [(Y0, -1.30), (-1.20, 1.20)]                    # y-Bereiche der beiden Zellen
LOCH = M.KELLERLOCH
FACKELN = [(X1, -1.00, (-1, 0, 0)), (1.00, Y0, (0, 1, 0))]


def baue_raum():
    r = Teil("Raum_Kerker", BI, saat=151)
    B.boden(r, "Innen", "pflaster", X0, X1, Y0, Y1, ZB, saat=152)
    for a, b, n, s in (((X0, Y0), (X1, Y0), (0, 1), 153), ((X1, Y0), (X1, Y1), (-1, 0), 154), ((X0, Y1), (X1, Y1), (0, -1), 155), ((X0, Y0), (X0, Y1), (1, 0), 156)):
        B.innenwand(r, "Innen", a, b, n, ZB - 0.02, ZD, "kerker", saat=s)
    # Blendboegen an der rechten Wand und in den Zellen (wie im Artwork), Ziegelring aus Sandstein
    for o, eu, nn, s0, s1 in (((X1, Y0), (0, 1), (-1, 0), 0.6, 2.2), ((X1, Y0), (0, 1), (-1, 0), 3.2, 4.8), ((X0, Y0), (0, 1), (1, 0), 0.3, 2.1), ((X0, Y0), (0, 1), (1, 0), 2.6, 4.6)):
        B.bogenring(r, "Innen", "quader", o, eu, nn, B.Loch(s0, s1, ZB, ZD - 0.25, bogen=True), breite=0.16, vor=0.05, kaempfer=False)
    # Decke mit dem Loch der Kellertreppe, Balken, Kanten des Lochs bis zum Hallenboden
    B.boden(r, "Innen", "kerker", X0, X1, Y0, Y1, ZD, oben=False, loecher=[LOCH], saat=157)
    x = X0 + 0.5
    while x < X1 - 0.2:
        if not (LOCH[0] - 0.15 < x < LOCH[1] + 0.15):
            r.kasten("Innen", (x - 0.13, Y0, ZD - 0.26), (x + 0.13, Y1, ZD), "balken", ohne=("+z",), laengs="y")
        else:
            r.kasten("Innen", (x - 0.13, Y0, ZD - 0.26), (x + 0.13, LOCH[2], ZD), "balken", ohne=("+z",), laengs="y")
        x += 1.10
    for p, e, n, lang in (((LOCH[0], LOCH[2]), (1, 0, 0), (0, 1, 0), LOCH[1] - LOCH[0]), ((LOCH[0], LOCH[2]), (0, 1, 0), (1, 0, 0), LOCH[3] - LOCH[2]),
                          ((LOCH[1], LOCH[2]), (0, 1, 0), (-1, 0, 0), LOCH[3] - LOCH[2])):
        r.rechteck("Innen", (p[0], p[1], ZD), e, (0, 0, 1), lang, M.Z_SOCKEL - ZD, "kerker", zu=(p[0] + e[0] * lang / 2 + n[0], p[1] + e[1] * lang / 2 + n[1], 0))
    # Kellertreppe aus Stein: von unten (Fuss bei K_X1) nach +x bis zur Kellertuer
    B.gerade_treppe(r, "Innen", "kerker", (M.K_X1, (LOCH[2] + LOCH[3]) / 2, ZB), (1, 0, 0), (0, 1, 0), M.TREPPE_BREITE, M.K_STUFEN, M.K_STEIG, M.K_AUFTRITT, ZB, block=True)
    # Trennmauern der Zellen
    for y0, y1 in ((ZELLEN[0][1], ZELLEN[1][0]), (ZELLEN[1][1], ZELLEN[1][1] + 0.12)):
        r.kasten("Innen", (X0, y0, ZB), (GITTER_X, y1, ZD), "kerker", ohne=("-x", "+z", "-z"))
    return r


def baue_gitter():
    """Gitter der beiden Zellen mit je einer Tuer (Bandeisen, Schloss)."""
    g = Teil("Gegenstand_Zellengitter", BI, saat=161)
    e = feld("eisen")
    for y0, y1 in ZELLEN:
        for z in (ZB + 0.08, ZB + 1.05, ZD - 0.25):
            g.stab("Innen", (GITTER_X, y0, z), (GITTER_X, y1, z), 0.05, "kleinteile", breite=0.07, u_bereich=e)
        y = y0 + 0.08
        while y < y1 - 0.04:
            g.stab("Innen", (GITTER_X, y, ZB), (GITTER_X, y, ZD - 0.2), 0.032, "kleinteile", u_bereich=e)
            y += 0.16
        ym = (y0 + y1) / 2
        g.rechteck("Innen", (GITTER_X - 0.03, ym - 0.08, ZB + 1.00), (0, 1, 0), (0, 0, 1), 0.16, 0.14, "kleinteile", u_bereich=feld("messing"), zu=(GITTER_X + 1, ym, 1))
    return g


def baue_ausstattung():
    a = Teil("Ausstattung_Kerker", BI, saat=171)
    for y0, y1 in ZELLEN:
        a.rechteck("Innen", (X0 + 0.15, y0 + 0.25, ZB + 0.02), (1, 0, 0), (0, 1, 0), 1.25, y1 - y0 - 0.5, "stroh", zu=(X0 + 0.8, 0, ZB + 2))
        a.dreh("Innen", (GITTER_X - 0.35, y1 - 0.30, ZB), [(0.13, 0.0), (0.15, 0.30)], "balken", seiten=10, gedreht=True)          # Eimer
        a.dreh("Innen", (X0 + 0.04, (y0 + y1) / 2, ZB + 1.6), [(0.08, 0.0), (0.08, 0.02)], "kleinteile", seiten=8, u_bereich=feld("eisen"))   # Kettenring
        a.stab("Innen", (X0 + 0.06, (y0 + y1) / 2, ZB + 1.55), (X0 + 0.06, (y0 + y1) / 2 + 0.1, ZB + 0.9), 0.025, "kleinteile", u_bereich=feld("eisen"))
    # Tisch und Schemel des Waechters, Laterne, Schluesselbund
    B.tisch(a, "Innen", "dielen", 1.00, 2.10, -2.70, -2.05, hoehe=0.76, z0=ZB, knieraum=0.66)
    a.dreh("Innen", (1.55, -1.70, ZB), [(0.16, 0.0), (0.14, 0.42), (0.18, 0.44)], "dielen", seiten=10)
    a.dreh("Innen", (1.28, -2.42, ZB + 0.76), [(0.07, 0.0), (0.07, 0.20), (0.03, 0.26)], "kleinteile", seiten=6, u_bereich=feld("messing"))   # Laterne
    # Waffenstaender und Faesser an der rechten Wand, Kisten vorn
    a.kasten("Innen", (X1 - 0.30, 1.0, ZB), (X1 - 0.05, 2.1, ZB + 0.06), "balken")
    a.kasten("Innen", (X1 - 0.30, 1.0, ZB + 1.10), (X1 - 0.05, 2.1, ZB + 1.16), "balken")
    for y in (1.25, 1.85):
        a.stab("Innen", (X1 - 0.18, y, ZB + 0.06), (X1 - 0.12, y, ZB + 2.10), 0.035, "balken")
    for (fx, fy, r) in ((X1 - 0.5, -3.2, 0.30), (X1 - 1.15, -3.3, 0.28)):
        a.dreh("Innen", (fx, fy, ZB), [(r * 0.86, 0.0), (r, 0.40), (r * 0.86, 0.80)], "balken", seiten=12, gedreht=True)
    a.kasten("Innen", (-0.2, Y0 + 0.10, ZB), (0.40, Y0 + 0.65, ZB + 0.50), "dielen")
    for fx, fy, n in FACKELN:
        p = Vector((fx, fy, ZB + 2.0)); n = Vector(n)
        a.stab("Innen", p - n * 0.02, p + n * 0.16 + EZ * 0.06, 0.03, "kleinteile", u_bereich=feld("eisen"))
        a.stab("Innen", p + n * 0.16 - EZ * 0.15, p + n * 0.16 + EZ * 0.20, 0.045, "balken", kappen=True)
    return a


def anker_raum():
    k = [
        B.anker("Blick_Auge", (X1 - 0.45, Y0 + 0.45, ZB + 1.60)),
        B.anker("Blick_Ziel", (X0 + 0.8, 0.8, ZB + 1.0)),
        B.anker("Treppe_Kerker_Halle", ((M.K_X0 + M.K_X1) / 2, (LOCH[2] + LOCH[3]) / 2, ZB + 1.6), ziel="rathaus:halle", groesse="2.4,1.2,2.6"),
        B.anker("Schild_Kerker", (GITTER_X + 0.3, -1.0, ZB + 2.3)),
        B.anker("Platz_Gefangener_1", (X0 + 0.95, -2.80, ZB), grad=90.0),
        B.anker("Platz_Gefangener_2", (X0 + 1.25, -1.95, ZB), grad=90.0),
        B.anker("Platz_Gefangener_3", (X0 + 1.10, 0.10, ZB), grad=90.0),
        B.anker("Platz_Waechter", (0.70, -0.40, ZB), grad=-90.0),
        B.anker("Licht_Raum", (0.3, 0.0, ZD - 0.4), art="Raum"),
    ]
    for i, (fx, fy, n) in enumerate(FACKELN):
        p = Vector((fx, fy, ZB + 2.0)) + Vector(n) * 0.16
        k.append(B.anker(f"Licht_Fackel_{i + 1}", tuple(p + EZ * 0.35 + Vector(n) * 0.10), art="Fackel"))
        k.append(B.anker(f"Feuer_Fackel_{i + 1}", tuple(p + EZ * 0.20)))
    return k


def main():
    opt = B.optionen()
    B.raum_bauen("kerker", [baue_raum, baue_ausstattung, baue_gitter], anker_raum, opt)
    if "bilder" in opt:
        B.raum_bilder(opt["bilder"], "kerker", "Raum_Kerker", ZD,
                      [("raumkamera", "Blick_Auge", "Blick_Ziel"), ("zur_treppe", (-0.2, -2.8, ZB + 1.6), (1.2, 3.6, ZB + 1.4))],
                      ((5.5, -8.5, ZB + 11.0), (0.25, 0.0, ZB)),
                      mass=((0.70, -0.40, ZB, 1.75, 0.46), (-1.30, -1.95 + 2.0, ZB, 2.50, 0.66)),
                      lichter=[((X1 - 0.5, -1.0, ZB + 2.3), 160), ((1.0, Y0 + 0.5, ZB + 2.3), 160), ((0.3, 0.0, ZD - 0.4), 300)],
                      dazu=("rathaus.glb", "rathaus_eingangshalle_innen.glb"))


if __name__ == "__main__":
    main()
