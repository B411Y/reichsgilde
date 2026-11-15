"""Neubau Rathaus, Raum 5: Ratshalle (das ganze Obergeschoss) - im Spiel ein Teil von "InteriorRoot"; hier tagt der Rat.

    blender -b --python tools/modelle/neubau/rathaus_ratshalle.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/rathaus_ratshalle_innen.glb:
    Raum_Rat          Dielenboden mit Treppenloch, Taefelung und Putz mit Fachwerkstaendern, Balkendecke mit Kopfbaendern, Gelaender
    Kamin_Rat         Kamin aus Sandstein mit Haube und Stadtwappen (Feuer_Kamin_1)
    Tafel_Rat         U-Tafel (Teile hoechstens 2,2 m, damit sitzpruefung.py jedes als Moebel erkennt), Podest, Thron, sechs Stuehle,
                      Pult des Schreibers, Zuhoererbank, Leuchter mit acht Kerzen, Truhe, Behaenge
    Gegenstand_Ratsbuch, Gegenstand_Aemtertafel   anklickbar: Ratskammer (Antraege bzw. Aemter mit dem Ratsbaum)
    Antrag            Rolle auf der Tafel (die Sitzung zeigt sie, solange ein Antrag laeuft)
    Glas_Rat          Butzenglas (Innenglas)
    Anker der Sitzung (Stadtansicht.Ratssitzung.cs): Anker_Buergermeister, Anker_Sitz_1..6 (extras platz, sitzhoehe), Anker_Redner,
    Anker_Schreiber, Anker_Bank_1..4, Blick_Sitzung_Auge/_Ziel, Blick_Rat_R2; dazu Blick_Auge/_Ziel, Treppe_Rat_Halle, Licht_*, Feuer_*.
Die Anker der Sitzung liegen auf dem Sitzpunkt (Huefte ueber dem Anker, Regel des Figuren-Baukastens); -Z (im Spiel) blickt zur Tafel.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rathaus_bib as B                                  # noqa: E402
import rathaus_masse as M                                # noqa: E402
from rathaus_bib import EZ, Teil, feld                   # noqa: E402

BI = B.BAENDER_RATHAUS_INNEN
X0, X1, Y0, Y1, ZB, ZD = M.RAEUME["ratshalle"][:6]
Z_TAEFEL = ZB + 1.30
RAUM = Vector(((X0 + X1) / 2, 0.0, ZB + 2.0))
LOCH = (M.TREPPENLOCH[0], M.TREPPENLOCH[1], M.TREPPENLOCH[2], M.TREPPENLOCH[3])
KAMIN_Y = 0.0
LEUCHTER = Vector(((M.U_X[0] + M.U_X[1]) / 2, M.U_MITTE_Y, ZD - 1.45))


def fenster(wand):
    return [(a0, a1, z0, z1, False) for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN if w == wand and raum == "ratshalle"]


def wandflaeche(r, a, b, n, wand, saat):
    """Innenflaeche: unten Taefelung bis 1,30 m, darueber Putz; dieselben Fensteroeffnungen wie aussen."""
    B.innenwand(r, "Innen", a, b, n, ZB, Z_TAEFEL, "taefelung", fenster(wand), saat=saat)
    B.innenwand(r, "Innen", a, b, n, Z_TAEFEL, ZD, "putz", fenster(wand), saat=saat + 1)
    # Abschlussleiste der Taefelung und Fachwerkstaender auf dem Putz
    a3, b3 = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    e = (b3 - a3).normalized(); laenge = (b3 - a3).length; nn = Vector((n[0], n[1], 0))
    r.brett("Innen", a3 + EZ * Z_TAEFEL, b3 + EZ * Z_TAEFEL, 0.07, nn, "balken", tiefe=0.03)
    start = a[0] if abs(e.x) > 0.5 else a[1]
    richtung = e.x if abs(e.x) > 0.5 else e.y
    loecher = sorted(((q0 - start) * richtung, (q1 - start) * richtung) for q0, q1, *_ in fenster(wand))
    s = 0.12
    while s < laenge - 0.1:
        if not any(min(l) - 0.12 < s < max(l) + 0.12 for l in loecher):
            r.brett("Innen", a3 + e * s + EZ * Z_TAEFEL, a3 + e * s + EZ * ZD, 0.15, nn, "balken", tiefe=0.04)
        s += 1.20


def baue_raum():
    r = Teil("Raum_Rat", BI, saat=51)
    B.boden(r, "Innen", "dielen", X0, X1, Y0, Y1, ZB, loecher=[LOCH], saat=52)
    # Kanten des Treppenlochs (Deckenstaerke)
    lx0, lx1, ly0, ly1 = LOCH
    for p, e, n in (((lx0, ly0), (1, 0, 0), (0, 1, 0)), ((lx0, ly1), (1, 0, 0), (0, -1, 0)), ((lx0, ly0), (0, 1, 0), (1, 0, 0))):
        lang = (lx1 - lx0) if e[0] else (ly1 - ly0)
        r.rechteck("Innen", (p[0], p[1], M.Z_EG_DECKE), e, (0, 0, 1), lang, ZB - M.Z_EG_DECKE, "balken", zu=(p[0] + e[0] * lang / 2 + n[0], p[1] + e[1] * lang / 2 + n[1], 4.45))
    wandflaeche(r, (X0, Y0), (X1, Y0), (0, 1), "vorn_og", 53)
    wandflaeche(r, (X0, Y1), (X1, Y1), (0, -1), "hinten_og", 55)
    wandflaeche(r, (X0, Y0), (X0, Y1), (1, 0), "links_og", 57)
    wandflaeche(r, (X1, Y0), (X1, Y1), (-1, 0), "rechts_og", 59)
    # Decke: Bretter, Balken quer mit Kopfbaendern an beiden Waenden
    B.boden(r, "Innen", "dielen", X0, X1, Y0, Y1, ZD, oben=False, laengs="y", saat=61)
    for x in M.RATSHALLE_BALKEN:          # zwischen den Fenstern: Konsolen auf Mauerwerk (Pruefung AUFLAGER in rathaus_masse.py)
        r.kasten("Innen", (x - 0.13, Y0, ZD - M.BALKEN_H), (x + 0.13, Y1, ZD), "balken", ohne=("+z",), laengs="y")
        for y, s in ((Y0, 1), (Y1, -1)):
            r.stab("Innen", (x, y + s * 0.05, ZD - M.BALKEN_H - 0.80), (x, y + s * 0.75, ZD - M.BALKEN_H - 0.02), 0.13, "balken", breite=0.12)
            r.kasten("Innen", (x - M.KONSOLE[0], y if s > 0 else y - 0.12, ZD - M.BALKEN_H - M.KONSOLE[1]), (x + M.KONSOLE[0], y + 0.12 if s > 0 else y, ZD - M.BALKEN_H), "balken", laengs="z")
    # Gelaender um das Treppenloch (nicht an der Austrittsseite)
    B.gelaender(r, "Innen", "balken", (lx0 + 0.05, ly0 + 0.04, ZB), (lx1, ly0 + 0.04, ZB))
    B.gelaender(r, "Innen", "balken", (lx0 + 0.04, ly0 + 0.04, ZB), (lx0 + 0.04, ly1 - 0.04, ZB))
    B.gelaender(r, "Innen", "balken", (lx0 + 0.05, ly1 - 0.04, ZB), (lx1 - 0.6, ly1 - 0.04, ZB))
    return r


def baue_kamin():
    k = Teil("Kamin_Rat", BI, saat=63)
    xw = X0
    tief, breit = 0.70, 1.20
    for y0, y1 in ((KAMIN_Y - breit, KAMIN_Y - breit + 0.35), (KAMIN_Y + breit - 0.35, KAMIN_Y + breit)):          # Wangen
        k.kasten("Innen", (xw, y0, ZB), (xw + tief, y1, ZB + 1.40), "quader", ohne=("-x", "-z"))
    k.kasten("Innen", (xw, KAMIN_Y - breit - 0.10, ZB + 1.40), (xw + tief + 0.08, KAMIN_Y + breit + 0.10, ZB + 1.70), "quader", ohne=("-x",))   # Sturz
    k.kasten("Innen", (xw, KAMIN_Y - breit + 0.35, ZB), (xw + 0.06, KAMIN_Y + breit - 0.35, ZB + 1.40), "kerker", ohne=("-x",))           # Rueckwand, verrusst
    k.rechteck("Innen", (xw + 0.06, KAMIN_Y - breit + 0.35, ZB + 0.005), (1, 0, 0), (0, 1, 0), tief - 0.06, 2 * breit - 0.70, "platten", zu=(xw + 0.4, KAMIN_Y, ZB + 1))
    # Haube: Trapez bis unter die Decke
    unten_x, oben_x = xw + tief, xw + 0.38
    yb_u, yb_o = breit + 0.05, 0.70
    zu, zo = ZB + 1.70, ZD
    pts = [Vector((unten_x, KAMIN_Y - yb_u, zu)), Vector((unten_x, KAMIN_Y + yb_u, zu)), Vector((oben_x, KAMIN_Y + yb_o, zo)), Vector((oben_x, KAMIN_Y - yb_o, zo))]
    v0, v1, m = BI["quader"]
    k.vieleck("Innen", pts, [(0, v1), (2 * yb_u / m, v1), (2 * yb_o / m + 0.1, v0), (0.1, v0)], zu=(xw + 3, KAMIN_Y, 6.5))
    for s in (-1, 1):
        k.vieleck("Innen", [Vector((xw, KAMIN_Y + s * yb_u, zu)), pts[1 if s > 0 else 0], pts[2 if s > 0 else 3], Vector((xw, KAMIN_Y + s * yb_o, zo))],
                  [(0, v1), (0.2, v1), (0.2, v0), (0, v0)], zu=(xw + 0.3, KAMIN_Y + s * 3, 6.5))
    # Stadtwappen auf der Haube: Schild in Rot mit goldenem Rand
    c = Vector((xw + tief - 0.12, KAMIN_Y, ZB + 2.55))
    umriss = [(-0.38, 0.45), (0.38, 0.45), (0.38, 0.0), (0.0, -0.48), (-0.38, 0.0)]
    for skala, band, ub, d in ((1.12, "kleinteile", feld("gold"), 0.02), (1.0, "stoff", None, 0.035)):
        p = [c + Vector((d, u * skala, w * skala)) for u, w in umriss]
        for i in range(len(p)):
            k.vieleck("Innen", [c + Vector((d, 0, 0)), p[i], p[(i + 1) % len(p)]],
                      [((ub[0] + ub[1]) / 2 if ub else 0.5, BI[band][1])] * 3 if ub else [(0.5, BI[band][1]), (0.6, BI[band][0]), (0.7, BI[band][0])], zu=c + Vector((2, 0, 0)))
    return k


def tisch_stuecke(t, x0, x1, y0, y1, hoehe=M.TISCH_HOEHE, z0=ZB, laeufer=True):
    """Tisch aus Stuecken von hoechstens 2,2 m (eine Platte je Stueck) mit rotem Laeufer."""
    n = max(1, math.ceil(max(x1 - x0, y1 - y0) / 1.45))     # Schenkel in drei Stuecke: die Fugen (und Beine) liegen zwischen den Stuehlen
    for i in range(n):
        if x1 - x0 >= y1 - y0:
            a, b = x0 + (x1 - x0) * i / n, x0 + (x1 - x0) * (i + 1) / n
            B.tisch(t, "Innen", "taefelung", a, b, y0, y1, hoehe=hoehe, z0=z0, knieraum=M.KNIERAUM)
            if laeufer: t.rechteck("Innen", (a, (y0 + y1) / 2 - 0.20, z0 + hoehe + 0.003), (1, 0, 0), (0, 1, 0), b - a, 0.40, "stoff", zu=(a, y0, z0 + 5))
        else:
            a, b = y0 + (y1 - y0) * i / n, y0 + (y1 - y0) * (i + 1) / n
            B.tisch(t, "Innen", "taefelung", x0, x1, a, b, hoehe=hoehe, z0=z0, knieraum=M.KNIERAUM)
            if laeufer: t.rechteck("Innen", ((x0 + x1) / 2 - 0.20, a, z0 + hoehe + 0.003), (0, 1, 0), (1, 0, 0), b - a, 0.40, "stoff", zu=(x0, a, z0 + 5))


def baue_tafel():
    t = Teil("Tafel_Rat", BI, saat=71)
    td = M.TISCH_TIEFE / 2
    for y in M.U_SCHENKEL_Y:
        tisch_stuecke(t, M.U_X[0], M.U_X[1], y - td, y + td)
    tisch_stuecke(t, M.U_X[1], M.U_X[1] + M.TISCH_TIEFE, M.U_SCHENKEL_Y[0] - td, M.U_SCHENKEL_Y[1] + td)
    # Podest mit Stufe, Thron und Tisch des Buergermeisters
    px0, px1, py0, py1, ph = M.PODEST
    t.kasten("Innen", (px0, py0, ZB), (px1, py1, ZB + ph), "dielen", ohne=("+x", "-z"), baender={"-x": "taefelung", "-y": "taefelung", "+y": "taefelung"})
    t.kasten("Innen", (px0 - 0.28, py0 + 0.6, ZB), (px0, py1 - 0.6, ZB + ph / 2), "dielen", ohne=("+x", "-z"), baender={"-x": "taefelung"})
    bm = M.ratsplaetze()[0]
    B.stuhl(t, "Innen", "taefelung", bm[1], bm[2], sitz=M.SITZ_HOEHE, breite=0.76, tiefe=0.52, vorn=0.19, lehne=1.75, polster=("Innen", "stoff"), armlehnen=True)
    B.tisch(t, "Innen", "taefelung", px0 + 0.04, px0 + 0.44, M.U_MITTE_Y - 0.55, M.U_MITTE_Y + 0.55, hoehe=M.TISCH_HOEHE, z0=ZB + ph, knieraum=M.KNIERAUM)
    # Sechs Stuehle der Ratsherren
    for name, p, blick, platz in M.ratsplaetze()[1:]:
        B.stuhl(t, "Innen", "taefelung", p, blick, sitz=M.SITZ_HOEHE, breite=M.STUHL_BREITE, polster=("Innen", "stoff"))
    # Schreiber: Schemel und schraeges Pult
    (sx, sy, sz), sb = M.SCHREIBER
    t.dreh("Innen", (sx, sy, ZB), [(0.17, 0.0), (0.15, M.SITZ_HOEHE - 0.04), (0.19, M.SITZ_HOEHE)], "taefelung", seiten=10)
    B.tisch(t, "Innen", "taefelung", sx + 0.32, sx + 0.72, sy - 0.36, sy + 0.36, hoehe=0.82, knieraum=M.KNIERAUM + 0.04)   # Pult mit Knieraum
    k = Vector((sx + 0.32, sy - 0.36, ZB + 0.83)); q = Vector((sx + 0.72, sy - 0.36, ZB + 0.99))
    t.vieleck("Innen", [k, k + Vector((0, 0.72, 0)), q + Vector((0, 0.72, 0)), q], [(0, BI["taefelung"][1]), (0.2, BI["taefelung"][1]), (0.2, BI["taefelung"][0]), (0, BI["taefelung"][0])], zu=(sx, sy, 3 + ZB))
    t.rechteck("Innen", k + Vector((0.08, 0.14, 0.03)), (q - k).normalized(), (0, 1, 0), 0.30, 0.42, "kleinteile", u_bereich=feld("pergament"), zu=(sx, sy, 3 + ZB))
    # Zuhoererbank an der Rueckwand
    bx0 = M.BANK[0][0][0] - 0.38; bx1 = M.BANK[-1][0][0] + 0.38; by = M.BANK[0][0][1]
    t.kasten("Innen", (bx0, by - 0.19, ZB + M.SITZ_HOEHE - 0.05), (bx1, by + 0.25, ZB + M.SITZ_HOEHE), "dielen", laengs="x")
    t.kasten("Innen", (bx0, by + 0.27, ZB + M.SITZ_HOEHE + 0.01), (bx1, by + 0.33, ZB + 1.0), "taefelung", laengs="x")   # Lehne mit Fuge: eigenes Teil
    for x in (bx0 + 0.08, bx1 - 0.14):                       # nur an den Enden: kein Bein zwischen den Plaetzen
        t.kasten("Innen", (x, by - 0.16, ZB), (x + 0.06, by + 0.24, ZB + M.SITZ_HOEHE - 0.05), "balken", ohne=("+z", "-z"))
    # Leuchter: Radkranz mit acht Kerzen an einer Kette
    c = LEUCHTER
    t.stab("Innen", c + EZ * 0.05, Vector((c.x, c.y, ZD - M.BALKEN_H)), 0.025, "kleinteile", u_bereich=feld("eisen"))
    for i in range(16):
        w0, w1 = 2 * math.pi * i / 16, 2 * math.pi * (i + 1) / 16
        t.stab("Innen", c + Vector((math.cos(w0), math.sin(w0), 0)) * 0.62, c + Vector((math.cos(w1), math.sin(w1), 0)) * 0.62, 0.04, "kleinteile", u_bereich=feld("eisen"))
    for i in range(4):
        w = math.pi * i / 4
        t.stab("Innen", c + Vector((math.cos(w), math.sin(w), 0)) * 0.62, c - Vector((math.cos(w), math.sin(w), 0)) * 0.62, 0.025, "kleinteile", u_bereich=feld("eisen"))
    for p in KERZEN:
        t.dreh("Innen", p, [(0.022, 0.0), (0.022, 0.16)], "kleinteile", seiten=6, u_bereich=feld("wachs"))
    # Truhe neben dem Kamin, zwei Wandbehaenge an der Rueckwand
    t.kasten("Innen", (X0 + 0.15, Y0 + 0.35, ZB), (X0 + 0.70, Y0 + 1.35, ZB + 0.55), "taefelung", baender={"+z": "taefelung"})   # Truhe vorn links, weit von der Bank
    for xm in (-3.6, -0.0):
        o = Vector((xm - 0.55, Y1 - 0.03, Z_TAEFEL + 0.45))
        t.rechteck("Innen", o, (1, 0, 0), (0, 0, 1), 1.10, 1.70, "stoff", laengs="v", zu=o + Vector((0.5, -2, 0.8)))
        t.rechteck("Innen", o + Vector((0.08, -0.004, 0.10)), (1, 0, 0), (0, 0, 1), 0.94, 0.08, "kleinteile", u_bereich=feld("gold"), zu=o + Vector((0.5, -2, 0.8)))
        t.rechteck("Innen", o + Vector((0.08, -0.004, 1.52)), (1, 0, 0), (0, 0, 1), 0.94, 0.08, "kleinteile", u_bereich=feld("gold"), zu=o + Vector((0.5, -2, 0.8)))
    return t


KERZEN = [Vector((LEUCHTER.x + 0.62 * math.cos(2 * math.pi * i / 8), LEUCHTER.y + 0.62 * math.sin(2 * math.pi * i / 8), LEUCHTER.z + 0.02)) for i in range(8)]


def baue_ratsbuch():
    g = Teil("Gegenstand_Ratsbuch", BI, saat=81)
    x, y, z = M.U_X[1] + M.TISCH_TIEFE / 2, M.U_MITTE_Y, ZB + M.TISCH_HOEHE
    g.kasten("Innen", (x - 0.24, y - 0.33, z), (x + 0.24, y + 0.33, z + 0.03), "stoff")
    for s in (-1, 1):
        g.rechteck("Innen", Vector((x - 0.21, y + (0.02 if s > 0 else -0.30), z + 0.055)), (1, 0, 0), (0, 1, 0), 0.42, 0.28, "kleinteile", u_bereich=feld("pergament"), zu=(x, y, z + 3))
    g.dreh("Innen", (x + 0.32, y + 0.40, z), [(0.035, 0.0), (0.04, 0.05), (0.025, 0.07)], "kleinteile", seiten=8, u_bereich=feld("tinte"))
    g.stab("Innen", (x + 0.32, y + 0.40, z + 0.06), (x + 0.40, y + 0.48, z + 0.30), 0.012, "kleinteile", u_bereich=feld("pergament"))
    return g


def baue_aemtertafel():
    """Wandbehang mit dem Baum der Aemter hinter dem Thron: roter Grund, goldener Stamm und sieben Schilde (Buergermeister oben)."""
    g = Teil("Gegenstand_Aemtertafel", BI, saat=83)
    x = X1 - 0.03
    ya, yb = M.U_MITTE_Y - 1.0, M.U_MITTE_Y + 1.0
    za, zb = ZB + 2.05, ZB + 3.35
    g.rechteck("Innen", (x, ya, za), (0, 1, 0), (0, 0, 1), yb - ya, zb - za, "stoff", laengs="v", zu=(x - 2, 0, 6))
    gold = feld("gold")
    ym = (ya + yb) / 2
    g.rechteck("Innen", (x - 0.005, ym - 0.04, za + 0.10), (0, 1, 0), (0, 0, 1), 0.08, zb - za - 0.35, "kleinteile", u_bereich=gold, zu=(x - 2, ym, 6))
    for (dy, dz) in ((0, 1.05), (-0.45, 0.72), (0.45, 0.72), (-0.70, 0.36), (-0.22, 0.36), (0.22, 0.36), (0.70, 0.36)):
        g.rechteck("Innen", (x - 0.008, ym + dy - 0.10, za + dz), (0, 1, 0), (0, 0, 1), 0.20, 0.17, "kleinteile", u_bereich=gold, zu=(x - 2, ym, 6))
    for (y0, z0, y1, z1) in ((ym, za + 0.98, ym - 0.45, za + 0.85), (ym, za + 0.98, ym + 0.45, za + 0.85), (ym - 0.45, za + 0.70, ym - 0.70, za + 0.50), (ym + 0.45, za + 0.70, ym + 0.70, za + 0.50)):
        g.vieleck("Innen", [Vector((x - 0.006, y0 - 0.02, z0)), Vector((x - 0.006, y0 + 0.02, z0)), Vector((x - 0.006, y1 + 0.02, z1)), Vector((x - 0.006, y1 - 0.02, z1))], [((gold[0] + gold[1]) / 2, BI["kleinteile"][1])] * 4, zu=(x - 2, ym, 6))
    g.stab("Innen", (x - 0.06, ya - 0.08, zb + 0.04), (x - 0.06, yb + 0.08, zb + 0.04), 0.03, "kleinteile", u_bereich=feld("eisen"))
    return g


def baue_antrag():
    a = Teil("Antrag", BI, saat=85)
    x, y = M.U_X[1] + 0.25, M.U_SCHENKEL_Y[1] + 0.05
    a.stab("Innen", (x - 0.22, y, ZB + M.TISCH_HOEHE + 0.035), (x + 0.22, y, ZB + M.TISCH_HOEHE + 0.035), 0.06, "kleinteile", u_bereich=feld("pergament"), kappen=True)
    a.rechteck("Innen", (x - 0.03, y - 0.04, ZB + M.TISCH_HOEHE + 0.003), (1, 0, 0), (0, 1, 0), 0.06, 0.08, "stoff", zu=(x, y, ZB + 5))
    return a


def baue_glas():
    g = Teil("Glas_Rat", BI, saat=87)
    for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN:
        if raum == "ratshalle":
            B.scheibe(g, "Innenglas", w, a0, a1, z0, z1, -M.WAND_DICKE[w] / 2 - 0.004, zu=RAUM)
    return g


def blickanker(name, auge, ziel):
    """Anker, dessen -Z im Spiel (Blender +Y) samt Neigung zum Ziel zeigt: fuer Kameras aus dem Knoten (Blick_Rat_R2)."""
    o = B.anker(name, auge)
    o.rotation_euler = (Vector(ziel) - Vector(auge)).to_track_quat("Y", "Z").to_euler()
    return o


def anker_raum():
    k = [
        B.anker("Blick_Auge", (2.55, 1.65, ZB + 1.60)),
        B.anker("Blick_Ziel", (-5.20, -2.30, ZB + 1.15)),
        B.anker("Blick_Sitzung_Auge", (M.REDNER[0][0] - 1.55, M.U_MITTE_Y, ZB + 2.65)),
        B.anker("Blick_Sitzung_Ziel", (M.ratsplaetze()[0][1][0], M.U_MITTE_Y, ZB + 0.85)),
        blickanker("Blick_Rat_R2", (M.PODEST[0] + 0.55, M.U_MITTE_Y + 0.15, ZB + M.PODEST[4] + 1.85), (M.REDNER[0][0], M.U_MITTE_Y, ZB + 1.30)),
        B.anker("Treppe_Rat_Halle", (M.H_LAUF2_X1 - 0.45, (M.H_PODEST[2] + M.H_PODEST[3]) / 2, ZB + 0.60), ziel="rathaus:halle", groesse="1.0,1.2,1.6"),
        B.anker("Licht_Raum", (M.U_X[0] - 1.0, 0.8, ZD - 0.6), art="Raum"),
        B.anker("Licht_Kerze_1", tuple(LEUCHTER - EZ * 0.25), art="Kerze"),
        B.anker("Licht_Kamin_1", (X0 + 0.9, KAMIN_Y, ZB + 0.9), art="Kamin"),
        B.anker("Feuer_Kamin_1", (X0 + 0.38, KAMIN_Y, ZB + 0.06)),
        B.anker("Anker_Redner", M.REDNER[0], grad=M.REDNER[1]),
        B.anker("Anker_Schreiber", M.SCHREIBER[0], grad=M.SCHREIBER[1], sitzhoehe=M.SITZ_HOEHE),
    ]
    for i, p in enumerate(KERZEN):
        k.append(B.anker(f"Feuer_Kerze_{i + 1}", tuple(p + EZ * 0.16)))
    for name, p, blick, platz in M.ratsplaetze():
        extras = {"sitzhoehe": M.SITZ_HOEHE}
        if name != "Anker_Buergermeister": extras["platz"] = platz - 1
        k.append(B.anker(name, p, grad=blick, **extras))
    for i, (p, blick) in enumerate(M.BANK):
        k.append(B.anker(f"Anker_Bank_{i + 1}", p, grad=blick, sitzhoehe=M.SITZ_HOEHE))
    return k


def main():
    opt = B.optionen()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    stoffe = B.stoffe_innen()
    teile = [baue_raum(), baue_kamin(), baue_tafel(), baue_ratsbuch(), baue_aemtertafel(), baue_antrag(), baue_glas()]
    print("DREIECKE Ratshalle:", {t.name: t.dreiecke() for t in teile}, "Summe", sum(t.dreiecke() for t in teile))
    objekte = [t.objekt(stoffe, mit_farbe=False) for t in teile]
    o_raum, o_kamin, o_tafel, o_buch, o_tafel2, o_antrag, o_glas = objekte
    B.uv2_und_ao([([o_raum], []), ([o_kamin, o_tafel, o_buch, o_tafel2, o_antrag], [o_raum])], "rathaus_innen", "ratshalle", mit_ao="ohne-ao" not in opt)
    anker = anker_raum()
    B.exportiere(objekte + anker, os.path.join(B.ZIEL_MODELL, "rathaus_ratshalle_innen.glb"))
    if "bilder" in opt:
        bilder(opt["bilder"])


def bilder(o):
    """Kontrollbilder (Blender, kein Spielbild): Raumkamera, Sitzungskamera, Blick vom Podest, Uebersicht ohne Decke; mit der Huelle
    und der Eingangshalle darunter (Treppe im Treppenloch)."""
    os.makedirs(o, exist_ok=True)
    for datei in ("rathaus.glb", "rathaus_eingangshalle_innen.glb"):
        pfad = os.path.join(B.ZIEL_MODELL, datei)
        if os.path.exists(pfad): bpy.ops.import_scene.gltf(filepath=pfad)
    for ob in bpy.data.objects:
        if ob.name.startswith(("Scheiben", "Glas_Halle")): ob.hide_render = True
    mass = Teil("Massfiguren", BI)
    for (x, y, h, b) in ((-4.30, -1.15, 1.75, 0.46), (-0.5, 1.4, 2.50, 0.66)):
        B.massfigur(mass, "Mass", (x, y, ZB), h, b)
    mass.objekt({"Mass": B.stoff("Mass", farbe=(0.85, 0.35, 0.1))}, mit_farbe=False)
    for i, p in enumerate(KERZEN[::2] + [Vector((X0 + 0.9, KAMIN_Y, ZB + 0.8))]):
        l = bpy.data.objects.new(f"Kerzenlicht{i}", bpy.data.lights.new(f"Kerzenlicht{i}", "POINT"))
        l.data.energy = 90 if i < 4 else 400; l.data.color = (1.0, 0.72, 0.42); l.location = p + EZ * 0.25
        bpy.context.scene.collection.objects.link(l)
    lampe = bpy.data.objects.new("Kontrolllampe", bpy.data.lights.new("Kontrolllampe", "POINT"))
    lampe.data.energy = 1100; lampe.location = (-2.0, -1.0, ZD - 0.5)
    bpy.context.scene.collection.objects.link(lampe)
    brenn = 18.0 / math.tan(math.radians(66) / 2)
    L = lambda n: tuple(bpy.data.objects[n].location)
    B.kontrollbild(os.path.join(o, "rat_raumkamera.jpg"), L("Blick_Auge"), L("Blick_Ziel"), brennweite=brenn, verbergen=("Massfiguren",), proben=48)
    B.kontrollbild(os.path.join(o, "rat_sitzungskamera.jpg"), L("Blick_Sitzung_Auge"), L("Blick_Sitzung_Ziel"), brennweite=brenn, verbergen=("Massfiguren",), proben=48)
    B.kontrollbild(os.path.join(o, "rat_raumkamera_mass.jpg"), L("Blick_Auge"), L("Blick_Ziel"), brennweite=brenn, proben=32)
    # Wandansichten: die Konsolen der Deckenbalken sitzen zwischen den Fenstern auf der Wand (Pruefung AUFLAGER)
    B.kontrollbild(os.path.join(o, "rat_wand_vorn.jpg"), (-2.0, 3.6, ZB + 2.6), (-2.0, Y0, ZB + 2.9), brennweite=20, verbergen=("Massfiguren",), proben=40)
    B.kontrollbild(os.path.join(o, "rat_wand_hinten.jpg"), (-2.0, -3.9, ZB + 2.6), (-2.0, Y1, ZB + 2.9), brennweite=20, verbergen=("Massfiguren",), proben=40)
    weg = [ob.name for ob in bpy.data.objects if ob.name in ("Dach", "Haus", "Scheiben")]
    raum = bpy.data.objects["Raum_Rat"]
    import bmesh
    bm = bmesh.new(); bm.from_mesh(raum.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().z > ZD - 0.6], context="FACES"); bm.to_mesh(raum.data); bm.free()
    B.kontrollbild(os.path.join(o, "rat_uebersicht.jpg"), (6.5, -11.0, ZB + 10.5), (-2.0, 0.0, ZB), brennweite=22, verbergen=tuple(weg), proben=48)


if __name__ == "__main__":
    main()
