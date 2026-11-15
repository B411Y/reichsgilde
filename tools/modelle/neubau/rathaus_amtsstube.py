"""Neubau Rathaus, Raum 2: Amtsstube (Anbau hinter dem Turm, Pultdach) - im Spiel ein Teil von "InteriorRoot".

    blender -b --python tools/modelle/neubau/rathaus_amtsstube.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/rathaus_amtsstube_innen.glb:
    Raum_Amt            Dielen, Fachwerkwaende (rechts und hinten mit Fenster), Mauer zum Turm, Wand zur Halle mit der Tuer, schraege
                        Decke unter dem Pultdach mit Sparren
    Ausstattung_Amt     Regale mit Rollen und Buechern, Schemel, Faesser, Fackel
    Gegenstand_Kanzleipult   anklickbar: Rathaus-Uebersicht (Stand, Buergerrecht, Aufsteigen - wie heute die Tafel in der Amtsstube)
    Gegenstand_Stadtkasse    anklickbar: Ratskammer, Reiter Kasse
    Gegenstand_Staendetafel  anklickbar: Rathaus-Uebersicht; darauf Standmarke (das Spiel setzt sie auf den Stand des Spielers)
    Standmarke          Marke auf der Staendetafel (zwischen den Ankern Stand_Unten und Stand_Oben)
    Glas_Amt            Butzenglas
    Anker: Platz_Schreiber (Stadtschreiber, sitzt), Schild_Amt, Blick_Auge/_Ziel, Tuer_Amt_Halle, Licht_*, Feuer_Fackel_*
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
X0, X1, Y0, Y1, ZB, _ = M.RAEUME["amtsstube"][:6]
TB, TH = M.TUER_LICHT
AY = M.TUEREN["Tuer_Halle_Amt"][3][1]
RAUM = Vector(((X0 + X1) / 2, (Y0 + Y1) / 2, 2.0))
DECKE = lambda x: M.anbau_dach_z(x) - 0.15                # Unterseite der Sparrenlage
STAENDE = 6
TAFEL = (4.15, 5.95, ZB + 1.10, ZB + 2.90)               # Staendetafel an der Turmwand: x0, x1, z0, z1
FACKEL = (X1, 0.40, (-1, 0, 0))


def oeffnungen(wand):
    return [(a0, a1, z0, z1, False) for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN if w == wand and raum == "amtsstube"]


def fachwerk_innen(r, a, b, n, z0, z1, oeff):
    a3, b3 = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0)); e = (b3 - a3).normalized(); lang = (b3 - a3).length; nn = Vector((n[0], n[1], 0))
    start = a[0] if abs(e.x) > 0.5 else a[1]
    loch = sorted((q0 - start, q1 - start) for q0, q1, *_ in oeff)
    r.brett("Innen", a3 + EZ * (z0 + 0.10), b3 + EZ * (z0 + 0.10), 0.18, nn, "balken", tiefe=0.04)
    s = 0.10
    while s < lang:
        if not any(l0 - 0.10 < s < l1 + 0.10 for l0, l1 in loch):
            top = min(z1(a3 + e * s), 9)
            r.brett("Innen", a3 + e * s + EZ * (z0 + 0.20), a3 + e * s + EZ * (top - 0.02), 0.15, nn, "balken", tiefe=0.04)
        s += 1.05


def baue_raum():
    r = Teil("Raum_Amt", BI, saat=121)
    B.boden(r, "Innen", "dielen", X0, X1, Y0, Y1, ZB, saat=122)
    hoch = lambda x: DECKE(x)
    # Wand zur Halle (x = 3,60): Putz bis unter die Decke (hier am hoechsten), die Tuer
    B.innenwand(r, "Innen", (X0, Y0), (X0, Y1), (1, 0), ZB - 0.02, hoch(X0), "putz", [(AY[0], AY[1], ZB, ZB + TH, False)], saat=123)
    # Rechte Wand (Fachwerk, Fenster) bis zur Traufe
    B.innenwand(r, "Innen", (X1, Y0), (X1, Y1), (-1, 0), ZB - 0.02, hoch(X1), "putz", oeffnungen("anbau_rechts"), saat=124)
    fachwerk_innen(r, (X1, Y0), (X1, Y1), (-1, 0), ZB, lambda p: hoch(X1), oeffnungen("anbau_rechts"))
    # Vorn die Turmmauer, hinten Fachwerk: oben schraeg unter dem Dach
    grenze = lambda z: (0.0, (X1 - X0) if z <= hoch(X1) else max(0.0, M.ANBAU_X[1] - (z + 0.15 - M.Z_ANBAU_TRAUFE) / M.PULT_NEIGUNG - X0))
    B.mauer(r, "Innen", (X0, Y0), (1, 0), (0, 1), X1 - X0, ZB - 0.02, hoch(X0), "bruchstein", zbasis=ZB, grenze=grenze, saat=125)
    l_hinten = [B.Loch(a0 - X0, a1 - X0, z0, z1) for a0, a1, z0, z1, _ in oeffnungen("anbau_hinten")]
    B.mauer(r, "Innen", (X0, Y1), (1, 0), (0, -1), X1 - X0, ZB - 0.02, hoch(X0), "putz", l_hinten, zbasis=ZB, grenze=grenze, saat=126)
    fachwerk_innen(r, (X0, Y1), (X1, Y1), (0, -1), ZB, lambda p: hoch(p.x), oeffnungen("anbau_hinten"))
    # Schraege Decke: Bretter in Streifen entlang y, Sparren alle 0,9 m
    v0, v1, m = BI["dielen"]
    y = Y0
    k = 0
    while y < Y1 - 1e-4:
        y2 = min(y + (v1 - v0) * m, Y1)
        a, b = Vector((X0, y, hoch(X0))), Vector((X1, y, hoch(X1)))
        t = (y2 - y) / ((v1 - v0) * m)
        u0 = (k * 0.37) % 1
        r.vieleck("Innen", [a, b, b + Vector((0, y2 - y, 0)), a + Vector((0, y2 - y, 0))],
                  [(u0, v1), (u0 + (b - a).length / m, v1), (u0 + (b - a).length / m, v1 - t * (v1 - v0)), (u0, v1 - t * (v1 - v0))], zu=(RAUM.x, y, 0))
        y = y2; k += 1
    y = Y0 + 0.30
    while y < Y1 - 0.2:
        a, b = Vector((X0, y, hoch(X0) - 0.01)), Vector((X1, y, hoch(X1) - 0.01))
        r.stab("Innen", a - EZ * 0.09, b - EZ * 0.09, 0.16, "balken", breite=0.10, oben=(0, 0, 1))
        y += 0.90
    return r


def regal(a, x0, x1, y0, y1, hoehe, boeden=5):
    """Wandregal mit Faechern: Seiten, Boeden, darin Rollen und Buecher (Pergament, Stoff)."""
    tiefe_x = abs(x1 - x0) < abs(y1 - y0)
    for s in ((x0, y0, x0 + 0.04, y1) if not tiefe_x else (x0, y0, x1, y0 + 0.04), (x1 - 0.04, y0, x1, y1) if not tiefe_x else (x0, y1 - 0.04, x1, y1)):
        a.kasten("Innen", (s[0], s[1], ZB), (s[2], s[3], ZB + hoehe), "taefelung", ohne=("-z",))
    for i in range(boeden):
        z = ZB + 0.05 + i * (hoehe - 0.10) / (boeden - 1)
        a.kasten("Innen", (x0 + 0.04 * (not tiefe_x), y0 + 0.04 * tiefe_x, z), (x1 - 0.04 * (not tiefe_x), y1 - 0.04 * tiefe_x, z + 0.03), "taefelung")
        if i == boeden - 1: continue
        n = 6
        for j in range(n):
            f = (j + 0.5) / n
            if tiefe_x:
                yy = y0 + 0.06 + f * (y1 - y0 - 0.12); xx = (x0 + x1) / 2
                if j % 3 == 2:
                    a.kasten("Innen", (xx - 0.12, yy - 0.04, z + 0.03), (xx + 0.12, yy + 0.04, z + 0.28), "stoff")
                else:
                    a.stab("Innen", (xx - 0.14, yy, z + 0.08), (xx + 0.14, yy, z + 0.08), 0.09, "kleinteile", u_bereich=feld("pergament"), kappen=True)


def baue_ausstattung():
    a = Teil("Ausstattung_Amt", BI, saat=131)
    regal(a, X0 + 0.02, X0 + 0.40, 2.05, 3.95, 2.30)
    regal(a, X1 - 0.40, X1 - 0.02, 2.55, 3.95, 2.10)
    # Schemel des Schreibers, Faesser und Kisten in der Ecke
    sx, sy = SCHREIBER
    a.dreh("Innen", (sx, sy, ZB), [(0.17, 0.0), (0.15, M.SITZ_HOEHE - 0.04), (0.19, M.SITZ_HOEHE)], "taefelung", seiten=10)
    for (fx, fy, r) in ((X0 + 0.40, Y0 + 0.40, 0.26),):
        a.dreh("Innen", (fx, fy, ZB), [(r * 0.86, 0.0), (r, 0.36), (r * 0.86, 0.72)], "balken", seiten=12, gedreht=True)
    a.kasten("Innen", (X0 + 0.80, Y0 + 0.10, ZB), (X0 + 1.30, Y0 + 0.55, ZB + 0.45), "dielen")
    fx, fy, n = FACKEL
    p = Vector((fx, fy, ZB + 2.05)); n = Vector(n)
    a.stab("Innen", p - n * 0.02, p + n * 0.16 + EZ * 0.06, 0.03, "kleinteile", u_bereich=feld("eisen"))
    a.stab("Innen", p + n * 0.16 - EZ * 0.15, p + n * 0.16 + EZ * 0.20, 0.045, "balken", kappen=True)
    return a


SCHREIBER = (5.45, 2.45)        # Sitzpunkt des Stadtschreibers, Blick nach vorn (-y) zum Pult


def baue_kanzleipult():
    g = Teil("Gegenstand_Kanzleipult", BI, saat=133)
    sx, sy = SCHREIBER
    B.tisch(g, "Innen", "taefelung", sx - 0.65, sx + 0.65, sy - 1.10, sy - 0.45, hoehe=M.TISCH_HOEHE, knieraum=M.KNIERAUM)
    z = ZB + M.TISCH_HOEHE
    k = Vector((sx - 0.30, sy - 0.98, z)); q = Vector((sx - 0.30, sy - 0.58, z + 0.12))
    g.vieleck("Innen", [k, k + Vector((0.6, 0, 0)), q + Vector((0.6, 0, 0)), q], [(0, BI["taefelung"][1]), (0.2, BI["taefelung"][1]), (0.2, BI["taefelung"][0]), (0, BI["taefelung"][0])], zu=(sx, sy, z + 3))
    g.rechteck("Innen", Vector((sx - 0.24, sy - 0.94, z + 0.012)), (1, 0, 0), (q - k).normalized(), 0.48, 0.34, "kleinteile", u_bereich=feld("pergament"), zu=(sx, sy, z + 3))
    g.kasten("Innen", (sx + 0.35, sy - 1.05, z), (sx + 0.60, sy - 0.75, z + 0.12), "stoff")
    g.dreh("Innen", (sx - 0.52, sy - 0.70, z), [(0.035, 0.0), (0.04, 0.05), (0.025, 0.07)], "kleinteile", seiten=8, u_bereich=feld("tinte"))
    g.dreh("Innen", (sx + 0.48, sy - 0.62, z), [(0.04, 0.0), (0.03, 0.02), (0.012, 0.03), (0.012, 0.16)], "kleinteile", seiten=8, u_bereich=feld("messing"))   # Leuchter
    g.dreh("Innen", (sx + 0.48, sy - 0.62, z + 0.16), [(0.02, 0.0), (0.02, 0.10)], "kleinteile", seiten=6, u_bereich=feld("wachs"))
    return g


def baue_stadtkasse():
    """Eisenbeschlagene Truhe der Stadtkasse an der rechten Wand (wie im Artwork), mit Schloss."""
    g = Teil("Gegenstand_Stadtkasse", BI, saat=135)
    x0, x1, y0, y1 = X1 - 0.70, X1 - 0.10, -0.25, 0.85
    g.kasten("Innen", (x0, y0, ZB), (x1, y1, ZB + 0.55), "taefelung", laengs="y")
    for y in (y0 + 0.12, (y0 + y1) / 2, y1 - 0.12):
        g.rechteck("Innen", (x0 - 0.006, y - 0.03, ZB + 0.02), (0, 1, 0), (0, 0, 1), 0.06, 0.52, "kleinteile", u_bereich=feld("eisen"), zu=(x0 - 1, y, 0.5))
        g.rechteck("Innen", (x0, y - 0.03, ZB + 0.556), (0, 1, 0), (1, 0, 0), 0.06, x1 - x0, "kleinteile", u_bereich=feld("eisen"), zu=(x0, y, 5))
    g.rechteck("Innen", (x0 - 0.008, (y0 + y1) / 2 - 0.07, ZB + 0.32), (0, 1, 0), (0, 0, 1), 0.14, 0.16, "kleinteile", u_bereich=feld("messing"), zu=(x0 - 1, 0.3, 0.7))
    return g


def baue_staendetafel():
    g = Teil("Gegenstand_Staendetafel", BI, saat=137)
    x0, x1, z0, z1 = TAFEL
    y = Y0 + 0.03
    g.rechteck("Innen", (x0, y, z0), (1, 0, 0), (0, 0, 1), x1 - x0, z1 - z0, "kleinteile", u_bereich=feld("pergament"), zu=((x0 + x1) / 2, 5, 2))
    for (p, q) in (((x0 - 0.05, z0 - 0.05), (x1 + 0.05, z0)), ((x0 - 0.05, z1), (x1 + 0.05, z1 + 0.05)), ((x0 - 0.05, z0), (x0, z1)), ((x1, z0), (x1 + 0.05, z1))):
        g.kasten("Innen", (p[0], Y0, p[1]), (q[0], Y0 + 0.06, q[1]), "taefelung", ohne=("-y",))
    for i in range(STAENDE):
        z = z0 + 0.12 + i * (z1 - z0 - 0.24) / (STAENDE - 1)
        g.rechteck("Innen", (x0 + 0.35, y + 0.002, z - 0.012), (1, 0, 0), (0, 0, 1), x1 - x0 - 0.45, 0.024, "kleinteile", u_bereich=feld("tinte"), zu=((x0 + x1) / 2, 5, z))
    return g


def baue_standmarke():
    m = Teil("Standmarke", BI, saat=139)
    x0 = TAFEL[0] + 0.08
    m.kasten("Innen", (x0, Y0 + 0.04, -0.09), (x0 + 0.18, Y0 + 0.10, 0.09), "stoff")      # um z = 0; das Spiel setzt die Hoehe
    return m


def baue_glas():
    g = Teil("Glas_Amt", BI, saat=141)
    for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN:
        if raum == "amtsstube":
            B.scheibe(g, "Innenglas", w, a0, a1, z0, z1, -M.WAND_DICKE[w] / 2 - 0.004, zu=RAUM)
    return g


def anker_raum():
    x0, x1, z0, z1 = TAFEL
    fx, fy, n = FACKEL
    p = Vector((fx, fy, ZB + 2.05)) + Vector(n) * 0.16
    return [
        B.anker("Blick_Auge", ((X0 + X1) / 2 + 0.35, Y1 - 0.15, ZB + 1.60), fov=80.0),   # an der Rueckwand zwischen den Regalen, weiter Blick: der kleine Raum ist ganz im Bild
        B.anker("Blick_Ziel", ((X0 + X1) / 2 - 0.2, Y0, ZB + 1.30)),
        B.anker("Tuer_Amt_Halle", (X0 + 0.02, (AY[0] + AY[1]) / 2, ZB + TH / 2), ziel="rathaus:halle", breite=TB, hoehe=TH, achse="x"),
        B.anker("Schild_Amt", (SCHREIBER[0], SCHREIBER[1] - 0.8, ZB + 2.4)),
        B.anker("Platz_Schreiber", (SCHREIBER[0], SCHREIBER[1], ZB), grad=180.0, sitzhoehe=M.SITZ_HOEHE),
        B.anker("Stand_Unten", (x0 + 0.17, Y0, z0 + 0.12)),
        B.anker("Stand_Oben", (x0 + 0.17, Y0, z1 - 0.12)),
        B.anker("Licht_Raum", ((X0 + X1) / 2, (Y0 + Y1) / 2, ZB + 2.6), art="Raum"),
        B.anker("Licht_Fackel_1", tuple(p + EZ * 0.35 + Vector(n) * 0.10), art="Fackel"),
        B.anker("Feuer_Fackel_1", tuple(p + EZ * 0.20)),
    ]


def main():
    opt = B.optionen()
    B.raum_bauen("amtsstube", [baue_raum, baue_ausstattung, baue_kanzleipult, baue_stadtkasse, baue_staendetafel, baue_standmarke, baue_glas], anker_raum, opt)
    if "bilder" in opt:
        import bpy
        bpy.data.objects["Standmarke"].location.z = TAFEL[2] + 0.12 + 2 * (TAFEL[3] - TAFEL[2] - 0.24) / (STAENDE - 1)
        B.raum_bilder(opt["bilder"], "amt", "Raum_Amt", 3.5,
                      [("raumkamera", "Blick_Auge", "Blick_Ziel"), ("zum_fenster", (X0 + 0.5, Y0 + 0.5, ZB + 1.6), (X1, 2.5, ZB + 1.6))],
                      ((11.5, 9.0, 11.0), ((X0 + X1) / 2, (Y0 + Y1) / 2, ZB)),
                      mass=((SCHREIBER[0] - 1.3, 0.9, ZB, 1.75, 0.46), (X0 + 0.6, 1.0, ZB, 2.50, 0.66)),
                      lichter=[((X1 - 0.5, 0.4, ZB + 2.4), 150), (((X0 + X1) / 2, 1.8, ZB + 2.7), 500)],
                      dazu=("rathaus.glb", "rathaus_eingangshalle_innen.glb"))


if __name__ == "__main__":
    main()
