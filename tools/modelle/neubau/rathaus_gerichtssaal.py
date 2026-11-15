"""Neubau Rathaus, Raum 3: Gerichtssaal (Erdgeschoss links) - im Spiel ein Teil von "InteriorRoot".

    blender -b --python tools/modelle/neubau/rathaus_gerichtssaal.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/rathaus_gerichtssaal_innen.glb:
    Raum_Gericht        Steinplatten, Bruchstein aussen (drei Rundbogenfenster links, eins vorn), Fachwerkwand zur Halle mit der Tuer,
                        Balkendecke
    Ausstattung_Gericht Podest mit drei hohen Richterstuehlen, Schranke, zwei Zuhoererbaenke, Richtschwert und Stadtwappen, Truhe, Fackeln
    Gegenstand_Richtertisch   anklickbar: das Stadtgericht (wie heute der Klick in den Gerichtssaal)
    Glas_Gericht        Butzenglas
    Anker: Platz_Richter_1..3 (sitzen, Sitzpunkt), Platz_Klaeger, Platz_Beklagter (stehen vor der Schranke), Schild_Gericht,
           Blick_Auge/_Ziel, Tuer_Gericht_Halle, Licht_*, Feuer_Fackel_*
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
X0, X1, Y0, Y1, ZB, ZD = M.RAEUME["gerichtssaal"][:6]
TB, TH = M.TUER_LICHT
RAUM = Vector(((X0 + X1) / 2, 0.0, 2.0))
GY = M.TUEREN["Tuer_Halle_Gericht"][3][1]
POD = (X0 + 0.20, X1 - 0.55, 1.95, Y1, 0.35)          # Podest der Richter: x0, x1, y0, y1, Hoehe
RICHTER_X = (-5.75, -4.75, -3.75)
RICHTER_Y = 3.05
SCHRANKE_Y = 0.75
FACKELN = [(X0, -0.75, (1, 0, 0)), (X1, 1.30, (-1, 0, 0))]


def oeffnungen(wand):
    return [(a0, a1, z0, z1, True) for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN if w == wand and raum == "gerichtssaal"]


def baue_raum():
    r = Teil("Raum_Gericht", BI, saat=101)
    B.boden(r, "Innen", "platten", X0, X1, Y0, Y1, ZB, saat=102)
    B.innenwand(r, "Innen", (X0, Y0), (X1, Y0), (0, 1), ZB - 0.02, ZD, "bruchstein", oeffnungen("vorn_eg"), saat=103)
    B.innenwand(r, "Innen", (X0, Y0), (X0, Y1), (1, 0), ZB - 0.02, ZD, "bruchstein", oeffnungen("links_eg"), saat=104)
    B.innenwand(r, "Innen", (X0, Y1), (X1, Y1), (0, -1), ZB - 0.02, ZD, "bruchstein", saat=105)
    # Fachwerkwand zur Halle: Putz mit Staendern, Riegeln und Streben, die Tuer
    B.innenwand(r, "Innen", (X1, Y0), (X1, Y1), (-1, 0), ZB - 0.02, ZD, "putz", [(GY[0], GY[1], ZB, ZB + TH, False)], saat=106)
    n = Vector((-1, 0, 0))
    P = lambda y, z: Vector((X1, y, z))
    r.brett("Innen", P(Y0, ZB + 0.10), P(Y1, ZB + 0.10), 0.20, n, "balken", tiefe=0.04)
    r.brett("Innen", P(Y0, ZD - 0.10), P(Y1, ZD - 0.10), 0.20, n, "balken", tiefe=0.04)
    staender = [Y0 + 0.10, GY[0] - 0.09, GY[1] + 0.09, 1.10, 2.45, Y1 - 0.10]
    for y in staender:
        r.brett("Innen", P(y, ZB + 0.20), P(y, ZD - 0.20), 0.17, n, "balken", tiefe=0.04)
    r.brett("Innen", P(GY[0] - 0.09, ZB + TH + 0.08), P(GY[1] + 0.09, ZB + TH + 0.08), 0.16, n, "balken", tiefe=0.04)
    for a, b in ((Y0 + 0.10, GY[0] - 0.09), (GY[1] + 0.09, 1.10), (1.10, 2.45), (2.45, Y1 - 0.10)):
        r.brett("Innen", P(a, (ZB + ZD) / 2), P(b, (ZB + ZD) / 2), 0.15, n, "balken", tiefe=0.035)
        r.brett("Innen", P(a, ZB + 0.20), P(b, (ZB + ZD) / 2), 0.14, n, "balken", tiefe=0.03)
    # Gewaende der Fenster innen
    for wand, o, eu, nn in (("vorn_eg", (X0 - 0.6, Y0), (1, 0), (0, 1)), ("links_eg", (X0, Y0 - 0.6), (0, 1), (1, 0))):
        for a0, a1, z0, z1, _ in oeffnungen(wand):
            start = o[0] if eu[0] else o[1]
            B.bogenring(r, "Innen", "quader", o, eu, nn, B.Loch(a0 - start, a1 - start, z0, z1, bogen=True), breite=0.15, vor=0.03, kaempfer=False)
    # Decke mit Balken quer zur Laengsrichtung
    B.boden(r, "Innen", "dielen", X0, X1, Y0, Y1, ZD, oben=False, laengs="x", saat=107)
    for y in M.GERICHT_BALKEN:            # Auflager auf der linken Wand: ueber den Bogenfenstern (Pruefung AUFLAGER)
        r.kasten("Innen", (X0, y - 0.11, ZD - 0.24), (X1, y + 0.11, ZD), "balken", ohne=("+z",), laengs="x")
    return r


def baue_ausstattung():
    a = Teil("Ausstattung_Gericht", BI, saat=111)
    px0, px1, py0, py1, ph = POD
    a.kasten("Innen", (px0, py0, ZB), (px1, py1, ZB + ph), "dielen", ohne=("+y", "-z"), baender={"-y": "taefelung", "-x": "taefelung", "+x": "taefelung"})
    a.kasten("Innen", (px0 + 0.8, py0 - 0.30, ZB), (px1 - 0.8, py0, ZB + ph / 2), "quader", ohne=("+y", "-z"))      # Stufe
    for x in RICHTER_X:
        B.stuhl(a, "Innen", "taefelung", (x, RICHTER_Y, ZB + ph), 180.0, sitz=M.SITZ_HOEHE, breite=0.70, tiefe=0.52, vorn=0.19, lehne=1.70, polster=("Innen", "stoff"), armlehnen=True)
    # Schranke quer durch den Saal mit Durchlass an der Tuerseite
    B.gelaender(a, "Innen", "balken", (X0 + 0.05, SCHRANKE_Y, ZB), (X1 - 1.05, SCHRANKE_Y, ZB), hoehe=0.95, abstand=0.16, docke=0.06)
    # zwei Zuhoererbaenke
    for yb in (-1.35, -2.55):
        a.kasten("Innen", (X0 + 0.35, yb - 0.20, ZB + 0.40), (X1 - 1.15, yb + 0.20, ZB + 0.45), "dielen", laengs="x")
        a.kasten("Innen", (X0 + 0.35, yb - 0.27, ZB + 0.47), (X1 - 1.15, yb - 0.22, ZB + 0.95), "taefelung", laengs="x")
        for x in (X0 + 0.45, X1 - 1.30):
            a.kasten("Innen", (x, yb - 0.17, ZB), (x + 0.07, yb + 0.17, ZB + 0.398), "balken", ohne=("+z", "-z"))
    # Rueckwand: Stadtwappen und Richtschwert
    c = Vector(((px0 + px1) / 2, Y1 - 0.04, ZB + 2.75))
    umriss = [(-0.40, 0.48), (0.40, 0.48), (0.40, 0.0), (0.0, -0.50), (-0.40, 0.0)]
    pts = [c + Vector((u, 0, w)) for u, w in umriss]
    for i in range(len(pts)):
        a.vieleck("Innen", [c, pts[i], pts[(i + 1) % len(pts)]], [(0.5, BI["stoff"][1]), (0.6, BI["stoff"][0]), (0.7, BI["stoff"][0])], zu=c - Vector((0, 2, 0)))
    for dx in (-0.95, 0.95):
        s = c + Vector((dx, -0.01, -0.35))
        a.vieleck("Innen", [s + Vector((-0.04, 0, 0)), s + Vector((0.04, 0, 0)), s + Vector((0.025, 0, 1.25)), s + Vector((-0.025, 0, 1.25))], [((feld("eisen")[0] + feld("eisen")[1]) / 2, BI["kleinteile"][1])] * 4, zu=s - Vector((0, 2, 0)))
        a.vieleck("Innen", [s + Vector((-0.18, 0, -0.02)), s + Vector((0.18, 0, -0.02)), s + Vector((0.18, 0, 0.03)), s + Vector((-0.18, 0, 0.03))], [((feld("gold")[0] + feld("gold")[1]) / 2, BI["kleinteile"][1])] * 4, zu=s - Vector((0, 2, 0)))
    # Truhe der Akten vorn links, Fackeln
    a.kasten("Innen", (X0 + 0.15, Y0 + 0.30, ZB), (X0 + 0.70, Y0 + 1.25, ZB + 0.55), "taefelung")
    for fx, fy, n in FACKELN:
        p = Vector((fx, fy, ZB + 2.05)); n = Vector(n)
        a.stab("Innen", p - n * 0.02, p + n * 0.16 + EZ * 0.06, 0.03, "kleinteile", u_bereich=feld("eisen"))
        a.stab("Innen", p + n * 0.16 - EZ * 0.15, p + n * 0.16 + EZ * 0.20, 0.045, "balken", kappen=True)
    return a


def baue_richtertisch():
    g = Teil("Gegenstand_Richtertisch", BI, saat=113)
    px0, px1, py0, py1, ph = POD
    x0, x1 = RICHTER_X[0] - 0.50, RICHTER_X[-1] + 0.50
    B.tisch(g, "Innen", "taefelung", x0, x1, 2.25, 2.70, hoehe=M.TISCH_HOEHE, z0=ZB + ph, knieraum=M.KNIERAUM)
    g.kasten("Innen", (x0, 2.20, ZB + ph + 0.06), (x1, 2.24, ZB + ph + M.TISCH_HOEHE - 0.002), "taefelung", laengs="x")   # Blende zum Saal
    g.rechteck("Innen", ((x0 + x1) / 2 - 0.55, 2.19, ZB + ph + 0.10), (1, 0, 0), (0, 0, 1), 1.10, 0.62, "stoff", zu=(0, -5, 1))
    g.rechteck("Innen", ((x0 + x1) / 2 - 0.55, 2.30, ZB + ph + M.TISCH_HOEHE + 0.003), (1, 0, 0), (0, 1, 0), 1.10, 0.36, "stoff", zu=(-4.7, 2.4, 5))
    g.kasten("Innen", (-4.20, 2.38, ZB + ph + M.TISCH_HOEHE), (-3.90, 2.60, ZB + ph + M.TISCH_HOEHE + 0.06), "stoff")         # Gesetzbuch
    g.dreh("Innen", (-5.20, 2.48, ZB + ph + M.TISCH_HOEHE), [(0.05, 0.0), (0.05, 0.02), (0.012, 0.03), (0.012, 0.22)], "kleinteile", seiten=8, u_bereich=feld("messing"))   # Waage (Staender)
    g.stab("Innen", (-5.38, 2.48, ZB + ph + M.TISCH_HOEHE + 0.22), (-5.02, 2.48, ZB + ph + M.TISCH_HOEHE + 0.22), 0.012, "kleinteile", u_bereich=feld("messing"))
    return g


def baue_glas():
    g = Teil("Glas_Gericht", BI, saat=117)
    for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN:
        if raum == "gerichtssaal":
            B.scheibe(g, "Innenglas", w, a0, a1, z0, z1, -M.WAND_DICKE[w] / 2 - 0.004, zu=RAUM)
    return g


def anker_raum():
    zp = ZB + POD[4]
    k = [
        B.anker("Blick_Auge", (X1 - 0.45, Y0 + 0.45, ZB + 1.60)),
        B.anker("Blick_Ziel", ((X0 + X1) / 2 - 0.4, Y1 - 0.8, ZB + 1.55)),
        B.anker("Tuer_Gericht_Halle", (X1 - 0.02, (GY[0] + GY[1]) / 2, ZB + TH / 2), ziel="rathaus:halle", breite=TB, hoehe=TH, achse="x"),
        B.anker("Schild_Gericht", ((X0 + X1) / 2, 1.6, ZB + 3.3)),
        B.anker("Platz_Beklagter", (-5.30, 1.40, ZB), grad=0.0),
        B.anker("Platz_Klaeger", (-3.90, 1.40, ZB), grad=0.0),
        B.anker("Licht_Raum", ((X0 + X1) / 2, 0.0, ZD - 0.5), art="Raum"),
    ]
    for i, x in enumerate(RICHTER_X):
        k.append(B.anker(f"Platz_Richter_{i + 1}", (x, RICHTER_Y, zp), grad=180.0, sitzhoehe=M.SITZ_HOEHE))
    for i, (fx, fy, n) in enumerate(FACKELN):
        p = Vector((fx, fy, ZB + 2.05)) + Vector(n) * 0.16
        k.append(B.anker(f"Licht_Fackel_{i + 1}", tuple(p + EZ * 0.35 + Vector(n) * 0.10), art="Fackel"))
        k.append(B.anker(f"Feuer_Fackel_{i + 1}", tuple(p + EZ * 0.20)))
    return k


def main():
    opt = B.optionen()
    B.raum_bauen("gerichtssaal", [baue_raum, baue_ausstattung, baue_richtertisch, baue_glas], anker_raum, opt)
    if "bilder" in opt:
        B.raum_bilder(opt["bilder"], "gericht", "Raum_Gericht", ZD,
                      [("raumkamera", "Blick_Auge", "Blick_Ziel"), ("zur_tuer", (-5.6, 3.0, ZB + 2.2), (X1, -1.0, ZB + 1.3))],
                      ((-9.5, -8.0, ZB + 12.0), ((X0 + X1) / 2, 0.3, ZB)),
                      mass=((-5.30, 1.40, ZB, 1.75, 0.46), (-3.90, 1.40, ZB, 2.50, 0.66)),
                      lichter=[((X0 + 0.5, -0.75, ZB + 2.4), 150), ((X1 - 0.5, 1.3, ZB + 2.4), 150), (((X0 + X1) / 2, 0.0, ZD - 0.4), 700)],
                      dazu=("rathaus.glb", "rathaus_eingangshalle_innen.glb"))


if __name__ == "__main__":
    main()
