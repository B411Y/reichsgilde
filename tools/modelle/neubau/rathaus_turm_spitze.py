"""Neubau Rathaus, Raum 7: Turm-Spitze (Glockenstube, offen bis in den Helm) - im Spiel ein Teil von "InteriorRoot".

    blender -b --python tools/modelle/neubau/rathaus_turm_spitze.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/rathaus_turm_spitze_innen.glb:
    Raum_Spitze         Bohlenboden mit der Luke (oben und unten), Bruchsteinmauern mit den vier Schallarkaden (die Schalllaeden gehoeren
                        zur Huelle), Innenseite des Helms aus Brettern
    Glockenstuhl_Spitze Glockenstuhl aus Eiche, Glocke aus Bronze mit Joch und Rad, Seil bis zur Luke, Sparren und Kehlbalken des Helms,
                        Lukendeckel, Laterne
    Anker: Blick_Auge/_Ziel, Leiter_Spitze_Turm, Licht_*
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
X0, X1, Y0, Y1, ZB, ZD = M.RAEUME["turm_spitze"][:6]
LX0, LX1, LY0, LY1 = M.LUKE_LAGE
SPITZE = Vector((M.W_MITTE[0], M.W_MITTE[1], M.Z_HELM_SPITZE - 0.30))
GLOCKE = Vector((5.10, M.W_MITTE[1] - 0.25, ZB + 1.05))          # links der Luke


def schall(wand):
    return [(a0, a1, z0, z1, True) for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN if w == wand and raum == "turm_spitze"]


def baue_raum():
    r = Teil("Raum_Spitze", BI, saat=201)
    loch = [(LX0, LX1, LY0, LY1)]
    B.boden(r, "Innen", "dielen", X0, X1, Y0, Y1, ZB, loecher=loch, saat=202)
    B.boden(r, "Innen", "dielen", X0, X1, Y0, Y1, ZB - M.BODEN_DICKE, oben=False, loecher=loch, saat=203)
    for p, e, n, lang in (((LX0, LY0), (1, 0, 0), (0, 1, 0), LX1 - LX0), ((LX0, LY1), (1, 0, 0), (0, -1, 0), LX1 - LX0), ((LX0, LY0), (0, 1, 0), (1, 0, 0), LY1 - LY0), ((LX1, LY0), (0, 1, 0), (-1, 0, 0), LY1 - LY0)):
        r.rechteck("Innen", (p[0], p[1], ZB - M.BODEN_DICKE), e, (0, 0, 1), lang, M.BODEN_DICKE, "balken", zu=(p[0] + e[0] * lang / 2 + n[0], p[1] + e[1] * lang / 2 + n[1], ZB))
    B.innenwand(r, "Innen", (X0, Y0), (X1, Y0), (0, 1), ZB - 0.02, ZD, "bruchstein", schall("turm_vorn"), saat=204)
    B.innenwand(r, "Innen", (X1, Y0), (X1, Y1), (-1, 0), ZB - 0.02, ZD, "bruchstein", schall("turm_rechts"), saat=205)
    B.innenwand(r, "Innen", (X0, Y1), (X1, Y1), (0, -1), ZB - 0.02, ZD, "bruchstein", schall("turm_hinten"), saat=206)
    B.innenwand(r, "Innen", (X0, Y0), (X0, Y1), (1, 0), ZB - 0.02, ZD, "bruchstein", schall("turm_links"), saat=207)
    # Mauerkrone und Innenseite des Helms (Bretter in Streifen bis zur Spitze)
    ecken = [Vector((X0, Y0, ZD)), Vector((X1, Y0, ZD)), Vector((X1, Y1, ZD)), Vector((X0, Y1, ZD))]
    d0, d1, dm = BI["dielen"]
    for i in range(4):
        a, b = ecken[i], ecken[(i + 1) % 4]
        streifen = 9
        for k in range(streifen):
            t0, t1 = k / streifen, (k + 1) / streifen
            p0, p1 = a + (SPITZE - a) * t0, b + (SPITZE - b) * t0
            q0, q1 = a + (SPITZE - a) * t1, b + (SPITZE - b) * t1
            pts = [p0, p1, q1, q0] if (q1 - q0).length > 1e-4 else [p0, p1, q1]
            uvs = [(0, d1), ((p1 - p0).length / dm, d1), ((q1 - q0).length / dm, d0), (0, d0)][:len(pts)]
            r.vieleck("Innen", pts, uvs, zu=Vector((M.W_MITTE[0], M.W_MITTE[1], ZD + 1.0)))
    return r


def baue_glockenstuhl():
    g = Teil("Glockenstuhl_Spitze", BI, saat=211)
    # Glockenstuhl: zwei Boecke (je zwei schraege Pfosten), Schwelle und Holm quer ueber die Glocke
    for x in (GLOCKE.x - 0.75, GLOCKE.x + 0.75):
        for dy in (-0.55, 0.55):
            g.stab("Innen", (x, GLOCKE.y + dy, ZB), (x, GLOCKE.y + dy * 0.25, ZB + 2.05), 0.14, "balken")
        g.stab("Innen", (x, GLOCKE.y - 0.70, ZB + 0.07), (x, GLOCKE.y + 0.70, ZB + 0.07), 0.14, "balken")
    g.stab("Innen", (GLOCKE.x - 0.85, GLOCKE.y, ZB + 2.10), (GLOCKE.x + 0.85, GLOCKE.y, ZB + 2.10), 0.18, "balken", breite=0.20)
    # Glocke aus Bronze mit Krone und Joch
    g.dreh("Innen", (GLOCKE.x, GLOCKE.y, GLOCKE.z), [(0.58, 0.0), (0.56, 0.08), (0.44, 0.30), (0.36, 0.62), (0.32, 0.80), (0.18, 0.86), (0.0, 0.88)], "kleinteile", seiten=16, u_bereich=feld("bronze"))
    g.stab("Innen", (GLOCKE.x, GLOCKE.y, GLOCKE.z + 0.86), (GLOCKE.x, GLOCKE.y, ZB + 2.00), 0.08, "kleinteile", u_bereich=feld("eisen"))
    g.kasten("Innen", (GLOCKE.x - 0.45, GLOCKE.y - 0.10, ZB + 1.88), (GLOCKE.x + 0.45, GLOCKE.y + 0.10, ZB + 2.00), "balken")
    for k in range(16):                                                       # Laeuterad
        w0, w1 = 2 * math.pi * k / 16, 2 * math.pi * (k + 1) / 16
        c = Vector((GLOCKE.x + 0.62, GLOCKE.y, ZB + 2.0))
        g.stab("Innen", c + Vector((0, math.cos(w0), math.sin(w0))) * 0.55, c + Vector((0, math.cos(w1), math.sin(w1))) * 0.55, 0.04, "balken")
    g.stab("Innen", (GLOCKE.x + 0.62, GLOCKE.y + 0.55, ZB + 2.0), ((LX0 + LX1) / 2, LY1 - 0.15, ZB), 0.025, "stroh")        # Seil zur Luke
    # Sparren und Kehlbalken des Helms
    ecken = [Vector((X0 + 0.1, Y0 + 0.1, ZD)), Vector((X1 - 0.1, Y0 + 0.1, ZD)), Vector((X1 - 0.1, Y1 - 0.1, ZD)), Vector((X0 + 0.1, Y1 - 0.1, ZD))]
    for e in ecken:
        g.stab("Innen", e, SPITZE - (SPITZE - e).normalized() * 0.2, 0.16, "balken")
    zk = ZD + 2.2
    f = (zk - ZD) / (SPITZE.z - ZD)
    kehl = [e + (SPITZE - e) * f for e in ecken]
    for i in range(4):
        g.stab("Innen", kehl[i], kehl[(i + 1) % 4], 0.12, "balken")
    g.stab("Innen", kehl[0], kehl[2], 0.12, "balken")
    g.stab("Innen", (M.W_MITTE[0], M.W_MITTE[1], zk), SPITZE, 0.16, "balken")
    # Lukendeckel aufgeklappt, Laterne an der Wand
    g.kasten("Innen", (LX0, LY1, ZB), (LX1, LY1 + 0.06, ZB + LY1 - LY0), "dielen", laengs="x")
    g.dreh("Innen", (X0 + 0.25, Y1 - 0.35, ZB + 1.6), [(0.08, 0.0), (0.08, 0.22), (0.03, 0.30)], "kleinteile", seiten=6, u_bereich=feld("messing"))
    return g


def anker_raum():
    return [
        B.anker("Blick_Auge", (X1 - 0.25, Y1 - 0.22, ZB + 1.60)),
        B.anker("Blick_Ziel", (X0 + 0.3, Y0 + 0.4, ZB + 1.45)),
        B.anker("Leiter_Spitze_Turm", ((LX0 + LX1) / 2, (LY0 + LY1) / 2, ZB + 0.40), ziel="rathaus:turm", groesse="1.2,1.2,0.8"),
        B.anker("Licht_Raum", (X0 + 0.45, Y1 - 0.45, ZB + 1.9), art="Kerze"),
    ]


def main():
    opt = B.optionen()
    B.raum_bauen("turm_spitze", [baue_raum, baue_glockenstuhl], anker_raum, opt)
    if "bilder" in opt:
        B.raum_bilder(opt["bilder"], "spitze", "Raum_Spitze", 99.0,
                      [("raumkamera", "Blick_Auge", "Blick_Ziel"), ("helm", (X1 - 0.4, Y1 - 0.4, ZB + 1.6), (M.W_MITTE[0], M.W_MITTE[1], M.Z_HELM_SPITZE - 1.0))],
                      ((11.0, -9.0, 20.0), (5.7, -2.5, 12.5)),
                      mass=((X0 + 0.7, Y0 + 0.7, ZB, 2.50, 0.66),),
                      lichter=[((X0 + 0.45, Y1 - 0.45, ZB + 1.9), 120), ((5.7, -2.5, ZB + 3.0), 300)],
                      dazu=("rathaus.glb",))


if __name__ == "__main__":
    main()
