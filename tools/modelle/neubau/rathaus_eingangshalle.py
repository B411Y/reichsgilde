"""Neubau Rathaus, Raum 1: Eingangshalle (Erdgeschoss Mitte) - im Spiel ein Teil von "InteriorRoot".

    blender -b --python tools/modelle/neubau/rathaus_eingangshalle.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/rathaus_eingangshalle_innen.glb:
    Raum_Halle        Boden (Steinplatten), Mauern (Bruchstein), Decke mit Balken, Unterzug und Stuetze, Tuergewaende, Laibungen
    Treppe_Halle      Haupttreppe mit Podest, Wangen, Gelaender, Bretterverschlag mit der Kellertuer
    Tueren_Halle      Tuerblaetter: Gerichtssaal, Turm, Amtsstube, Keller (das Portal gehoert zur Huelle)
    Ausstattung_Halle Banner, Waffenstaender, Fackeln, Bank, Faesser und Kisten, Schreibpult
    Gegenstand_Anschlagbrett   anklickbar: oeffnet die Rathaus-Uebersicht (wie heute der Klick aufs Rathaus)
    Glas_Halle        Butzenglas der beiden Arkadenfenster (Innenglas)
    Anker: Blick_Auge, Blick_Ziel, Tuer_<von>_<nach> (Klick: Raumwechsel), Treppe_Halle_Rat, Licht_*, Feuer_*
Alle Masse aus rathaus_masse.py: die Mauern sind die Innenflaechen derselben Waende wie aussen (innen = aussen minus Wand).
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rathaus_bib as B                                  # noqa: E402
import rathaus_masse as M                                # noqa: E402
from rathaus_bib import EZ, Loch, Teil, feld, mauer      # noqa: E402

BI = B.BAENDER_RATHAUS_INNEN
X0, X1, Y0, Y1, ZB, ZD = M.RAEUME["eingangshalle"][:6]
TB, TH = M.TUER_LICHT
RAUM = Vector(((X0 + X1) / 2, 0.0, 2.0))
UZ_Y = -0.40                       # Unterzug quer durch die Halle, auf der Mittelstuetze
STUETZE_X = (X0 + X1) / 2


def arkade_loecher():
    """Die Arkade der Vorderwand in Weltkoordinaten (x) fuer die Innenflaeche."""
    return [(a0, a1, z0, z1, True) for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN if w == "vorn_eg" and raum == "eingangshalle"]


def tuer(name):
    return M.TUEREN[name][3]


def baue_raum():
    r = Teil("Raum_Halle", BI, saat=1)
    # Boden mit dem Loch der Kellertreppe
    B.boden(r, "Innen", "platten", X0, X1, Y0, Y1, ZB, loecher=[M.KELLERLOCH], saat=2)
    # Vorderwand (Innenflaeche der Aussenwand vorn_eg)
    B.innenwand(r, "Innen", (X0, Y0), (X1, Y0), (0, 1), ZB - 0.02, ZD, "bruchstein", arkade_loecher(), saat=3)
    # Trennwand zum Gerichtssaal (links), Tuer
    (gy0, gy1) = tuer("Tuer_Halle_Gericht")[1]
    B.innenwand(r, "Innen", (X0, Y0), (X0, Y1), (1, 0), ZB - 0.02, ZD, "bruchstein", [(gy0, gy1, ZB, ZB + TH, False)], saat=4)
    # rechte Wand zu Turm und Amtsstube
    ty = tuer("Tuer_Halle_Turm")[1]; ay = tuer("Tuer_Halle_Amt")[1]
    B.innenwand(r, "Innen", (X1, Y0), (X1, Y1), (-1, 0), ZB - 0.02, ZD, "bruchstein", [(ty[0], ty[1], ZB, ZB + TH, False), (ay[0], ay[1], ZB, ZB + TH, False)], saat=5)
    # Rueckwand
    B.innenwand(r, "Innen", (X0, Y1), (X1, Y1), (0, -1), ZB - 0.02, ZD, "bruchstein", saat=6)
    # Durchgaenge (Laibungen) und Gewaende aus Sandstein
    B.durchgang(r, "Innen", "quader", (X0, (gy0 + gy1) / 2, ZB), (0, 1, 0), (1, 0, 0), TB, TH, M.TRENN_X[1] - M.TRENN_X[0])
    B.durchgang(r, "Innen", "quader", (X1, (ty[0] + ty[1]) / 2, ZB), (0, 1, 0), (-1, 0, 0), TB, TH, M.TURM_X[0] + M.T_STEIN - X1)
    B.durchgang(r, "Innen", "quader", (X1, (ay[0] + ay[1]) / 2, ZB), (0, 1, 0), (-1, 0, 0), TB, TH, M.RECHTS_X[1] - X1)
    for mitte, eu, n in (((X0, (gy0 + gy1) / 2, ZB), (0, 1, 0), (1, 0, 0)), ((X1, (ty[0] + ty[1]) / 2, ZB), (0, 1, 0), (-1, 0, 0)), ((X1, (ay[0] + ay[1]) / 2, ZB), (0, 1, 0), (-1, 0, 0))):
        B.tuerrahmen(r, "Innen", "quader", mitte, eu, n, TB, TH)
    # Gewaende der Arkade innen (Sandstein um die Boegen) und Sohlbaenke
    for a0, a1, z0, z1, _ in arkade_loecher():
        l = Loch(a0 - X0, a1 - X0, z0, z1, bogen=True)
        B.bogenring(r, "Innen", "quader", (X0, Y0), (1, 0), (0, 1), l, breite=0.18, vor=0.03, kaempfer=False)
    # Decke: Bretter, Balken (quer), Unterzug auf der Mittelstuetze, Wechsel am Treppenloch
    B.boden(r, "Innen", "dielen", X0, X1, Y0, Y1, ZD, oben=False, loecher=[M.TREPPENLOCH], laengs="y", saat=7)
    bh = 0.24
    for x in M.HALLE_BALKEN:              # Auflager auf der Vorderwand: ueber den Arkadenboegen (Pruefung AUFLAGER)
        ye = M.TREPPENLOCH[2] if x < M.TREPPENLOCH[1] else Y1
        r.kasten("Innen", (x - 0.10, Y0, ZD - bh), (x + 0.10, ye, ZD), "balken", ohne=("+z",), laengs="y")
    r.kasten("Innen", (X0, M.TREPPENLOCH[2] - 0.22, ZD - bh), (M.TREPPENLOCH[1], M.TREPPENLOCH[2], ZD), "balken", ohne=("+z",), laengs="x")
    r.kasten("Innen", (X0, UZ_Y - 0.16, ZD - bh - 0.32), (X1, UZ_Y + 0.16, ZD - bh), "balken", ohne=("+z",), laengs="x")
    r.kasten("Innen", (STUETZE_X - 0.15, UZ_Y - 0.15, ZB), (STUETZE_X + 0.15, UZ_Y + 0.15, ZD - bh - 0.32), "balken", ohne=("+z", "-z"), laengs="z")
    zk = ZD - bh - 0.32
    for dx in (-1, 1):                                          # Kopfbaender
        r.stab("Innen", (STUETZE_X + dx * 0.12, UZ_Y, zk - 0.75), (STUETZE_X + dx * 0.85, UZ_Y, zk - 0.02), 0.14, "balken", breite=0.12)
    r.kasten("Innen", (STUETZE_X - 0.24, UZ_Y - 0.24, ZB), (STUETZE_X + 0.24, UZ_Y + 0.24, ZB + 0.22), "quader")      # Steinsockel
    return r


def baue_treppe():
    """Haupttreppe aus Eiche: Lauf 1 an der Trennwand, Podest in der Ecke, Lauf 2 an der Rueckwand bis ins Obergeschoss."""
    t = Teil("Treppe_Halle", BI, saat=11)
    b = M.TREPPE_BREITE
    z_podest = ZB + M.H_LAUF1 * M.H_STEIG
    # Lauf 1
    B.gerade_treppe(t, "Innen", "dielen", (X0 + b / 2, M.H_LAUF1_Y0, ZB), (0, 1, 0), (1, 0, 0), b, M.H_LAUF1, M.H_STEIG, M.H_AUFTRITT, ZB, wange=("Innen", "balken"))
    # Podest
    px0, px1, py0, py1 = M.H_PODEST
    t.kasten("Innen", (px0, py0, z_podest - 0.20), (px1, py1, z_podest), "dielen", ohne=("-x", "+y"), baender={"-z": "balken", "-y": "balken", "+x": "balken"})
    t.kasten("Innen", (px1 - 0.14, py0, ZB), (px1, py0 + 0.14, z_podest - 0.20), "balken", ohne=("+z", "-z"))
    # Lauf 2
    n2 = M.H_STUFEN - M.H_LAUF1
    B.gerade_treppe(t, "Innen", "dielen", (px1, (py0 + py1) / 2, z_podest), (1, 0, 0), (0, 1, 0), b, n2, M.H_STEIG, M.H_AUFTRITT, z_podest, wange=("Innen", "balken"))
    # Untersicht beider Laeufe (Bretter)
    for a, e, lang, z0 in (((X0, M.H_LAUF1_Y0, ZB), (0, 1, 0), (M.H_LAUF1 - 1) * M.H_AUFTRITT, ZB), ((px1, py0, z_podest), (1, 0, 0), (n2 - 1) * M.H_AUFTRITT, z_podest)):
        a = Vector(a); e = Vector(e)
        q = Vector((1, 0, 0)) if e.y else Vector((0, 1, 0))
        steig = (M.H_LAUF1 if e.y else n2) * M.H_STEIG
        p0 = a - EZ * 0.04; p1 = a + e * lang + EZ * (steig - 0.25)
        t.vieleck("Innen", [p0, p0 + q * b, p1 + q * b, p1], [(0, BI["dielen"][1]), (0, BI["dielen"][0]), (lang / 4, BI["dielen"][0]), (lang / 4, BI["dielen"][1])], zu=(p0 + p1) / 2 + q * b / 2 - EZ + e * 0.3)
    # Gelaender: Lauf 1 an der offenen Seite (x = -1,30), Lauf 2 vorn (y = 2,50)
    B.gelaender(t, "Innen", "balken", (px1 - 0.03, M.H_LAUF1_Y0, ZB + M.H_STEIG), (px1 - 0.03, py0 - 0.03, z_podest))
    B.gelaender(t, "Innen", "balken", (px1 + 0.05, py0 + 0.04, z_podest + M.H_STEIG), (M.H_LAUF2_X1, py0 + 0.04, M.Z_OG))
    # Verschlag unter der Treppe (Bretter), darin die Kellertuer
    kx0, kx1 = tuer("Tuer_Halle_Kerker")[0]
    yv = tuer("Tuer_Halle_Kerker")[1][0]
    unterseite = lambda x: min(ZD, z_podest + (x - px1) / M.H_AUFTRITT * M.H_STEIG - 0.27) if x > px1 else z_podest - 0.22
    xs = [px0] + [px1 + i * 0.30 for i in range(int((X1 - px1) / 0.30) + 1)] + [X1]
    xs = sorted(set(round(v, 4) for v in xs) | {kx0, kx1})
    for xa, xb in zip(xs[:-1], xs[1:]):
        if xb - xa < 1e-3: continue
        za, zb = unterseite(xa), unterseite(xb)
        unten = ZB + TH if xa >= kx0 - 1e-4 and xb <= kx1 + 1e-4 else ZB
        if za <= unten + 0.01 and zb <= unten + 0.01: continue
        pts = [Vector((xa, yv, unten)), Vector((xb, yv, unten)), Vector((xb, yv, zb)), Vector((xa, yv, za))]
        t.vieleck("Innen", pts, [(0.1 * xa, BI["dielen"][1]), (0.1 * xa, BI["dielen"][0]), ((zb - unten) / 4, BI["dielen"][0]), ((za - unten) / 4, BI["dielen"][1])], zu=((xa + xb) / 2, yv - 1, 1.5))
    # Seitenwand unter Lauf 1 (x = -1,30), Bretter bis unter die Stufen
    ys = [M.H_LAUF1_Y0 + i * 0.28 for i in range(M.H_LAUF1)] + [py0]
    for ya, yb in zip(ys[:-1], ys[1:]):
        za = ZB + (ya - M.H_LAUF1_Y0) / M.H_AUFTRITT * M.H_STEIG - 0.02; zb = ZB + (yb - M.H_LAUF1_Y0) / M.H_AUFTRITT * M.H_STEIG - 0.02
        pts = [Vector((px1, ya, ZB)), Vector((px1, yb, ZB)), Vector((px1, yb, zb)), Vector((px1, ya, za))]
        t.vieleck("Innen", pts, [(ya / 4, BI["dielen"][1]), (ya / 4, BI["dielen"][0]), (zb / 4 + 0.1, BI["dielen"][0]), (za / 4 + 0.1, BI["dielen"][1])], zu=(px1 + 1, (ya + yb) / 2, 1))
    t.vieleck("Innen", [Vector((px0, py0, ZB)), Vector((px1, py0, ZB)), Vector((px1, py0, z_podest - 0.2)), Vector((px0, py0, z_podest - 0.2))],
              [(0, BI["dielen"][1]), (0, BI["dielen"][0]), (0.5, BI["dielen"][0]), (0.5, BI["dielen"][1])], zu=(-1.9, py0 - 1, 1))
    return t


def baue_tueren():
    t = Teil("Tueren_Halle", BI, saat=13)
    gy = tuer("Tuer_Halle_Gericht")[1]; ty = tuer("Tuer_Halle_Turm")[1]; ay = tuer("Tuer_Halle_Amt")[1]
    kx = tuer("Tuer_Halle_Kerker")[0]
    for x, y, n in (((M.TRENN_X[0] + M.TRENN_X[1]) / 2, (gy[0] + gy[1]) / 2, (1, 0, 0)), (X1 + 0.30, (ty[0] + ty[1]) / 2, (-1, 0, 0)), (X1 + 0.30, (ay[0] + ay[1]) / 2, (-1, 0, 0))):
        B.bretttuer(t, "Innen", "taefelung", "Innen", "kleinteile", (x, y, ZB), (0, 1, 0), n, TB - 0.02, TH - 0.01, bretter=5, eisen_ub=feld("eisen"))
    B.bretttuer(t, "Innen", "dielen", "Innen", "kleinteile", ((kx[0] + kx[1]) / 2, tuer("Tuer_Halle_Kerker")[1][0] + 0.04, ZB), (1, 0, 0), (0, -1, 0), TB - 0.02, TH - 0.01, bretter=5, eisen_ub=feld("eisen"))
    return t


def baue_anschlagbrett():
    """Anschlagbrett an der rechten Wand zwischen Turm- und Amtstuer: Rahmen, Kork aus Brettern, Zettel aus Pergament."""
    g = Teil("Gegenstand_Anschlagbrett", BI, saat=17)
    x = X1 - 0.04
    ya, yb = -1.35, 0.15
    za, zb = ZB + 0.95, ZB + 2.05
    g.kasten("Innen", (x - 0.04, ya, za), (x, yb, zb), "dielen", ohne=("+x",))
    for (p0, p1) in (((x - 0.08, ya - 0.06, za - 0.06), (x, ya, zb + 0.06)), ((x - 0.08, yb, za - 0.06), (x, yb + 0.06, zb + 0.06)),
                     ((x - 0.08, ya, zb), (x, yb, zb + 0.06)), ((x - 0.08, ya, za - 0.06), (x, yb, za))):
        g.kasten("Innen", p0, p1, "balken", ohne=("+x",))
    zettel = [(-1.20, 1.55, 0.28, 0.36), (-0.85, 1.70, 0.24, 0.30), (-0.52, 1.45, 0.30, 0.40), (-0.18, 1.62, 0.26, 0.32), (-1.05, 1.15, 0.30, 0.26), (-0.40, 1.08, 0.34, 0.30), (-0.05, 1.20, 0.22, 0.28)]
    for i, (y, z, b, h) in enumerate(zettel):
        g.rechteck("Innen", (x - 0.045 - 0.002 * i, y, ZB + z - 0.3), (0, 1, 0), (0, 0, 1), b, h, "kleinteile", u_bereich=feld("pergament"), zu=(x - 1, y, 1.5))
    # Siegel und Naegel
    for y, z in ((-1.06, 1.88), (-0.37, 1.83), (-0.9, 1.33)):
        g.rechteck("Innen", (x - 0.06, y, ZB + z - 0.3), (0, 1, 0), (0, 0, 1), 0.04, 0.04, "stoff", zu=(x - 1, y, 1.5))
    return g


def baue_ausstattung():
    a = Teil("Ausstattung_Halle", BI, saat=19)
    # Banner an Stangen: an der Vorderwand ueber den Pfeilern und an der Trennwand
    def banner(p, eu, n, hoch=1.30, breit=0.55):
        p = Vector(p); eu = Vector(eu); n = Vector(n)
        a.stab("Innen", p - eu * 0.36 + n * 0.10, p + eu * 0.36 + n * 0.10, 0.03, "kleinteile", u_bereich=feld("eisen"))
        o = p - eu * breit / 2 + n * 0.11 - EZ * hoch
        a.rechteck("Innen", o, eu, EZ, breit, hoch, "stoff", laengs="v", zu=o + eu * breit / 2 + EZ + n * 2)
        a.rechteck("Innen", o + n * 0.004, eu, EZ, breit, 0.12, "kleinteile", u_bereich=feld("gold"), zu=o + eu * breit / 2 + n * 2)
        a.rechteck("Innen", o + EZ * (hoch - 0.30) + n * 0.004, eu, EZ, breit, 0.08, "kleinteile", u_bereich=feld("gold"), zu=o + eu * breit / 2 + EZ * hoch + n * 2)
        a.rechteck("Innen", o - n * 0.002, eu, EZ, breit, hoch, "stoff", laengs="v", zu=o + eu * breit / 2 + EZ - n * 2)
    for xm in ((M.ARKADE[0][1] + M.ARKADE[1][0]) / 2, (M.ARKADE[1][1] + M.ARKADE[2][0]) / 2):
        banner((xm, Y0, ZD - 0.35), (1, 0, 0), (0, 1, 0))
    banner((X0, -2.70, ZD - 0.35), (0, 1, 0), (1, 0, 0))
    banner((X1, -0.60, ZD - 0.20), (0, 1, 0), (-1, 0, 0), hoch=0.95)
    # Waffenstaender neben der Turmtuer: zwei Hellebarden und ein Rundschild
    wx, wy = X1 - 0.35, -3.35
    a.kasten("Innen", (wx - 0.45, wy - 0.12, ZB), (wx + 0.45, wy + 0.12, ZB + 0.06), "balken")
    a.kasten("Innen", (wx - 0.45, wy + 0.06, ZB + 1.10), (wx + 0.45, wy + 0.12, ZB + 1.16), "balken")
    for dx in (-0.25, 0.25):
        a.stab("Innen", (wx + dx, wy, ZB + 0.06), (wx + dx, wy + 0.06, ZB + 2.20), 0.035, "balken")
        k = Vector((wx + dx, wy + 0.06, ZB + 2.20))
        a.vieleck("Innen", [k + Vector((0, 0, -0.30)), k + Vector((0.16, 0, -0.18)), k + Vector((0.16, 0, 0.02)), k + Vector((0, 0, 0.28))], [(0.01, 0.99)] * 4, zu=k - Vector((0, 1, 0)))
        a.vieleck("Innen", [k + Vector((0, 0, -0.30)), k + Vector((0.16, 0, -0.18)), k + Vector((0.16, 0, 0.02)), k + Vector((0, 0, 0.28))], [(0.01, 0.99)] * 4, zu=k + Vector((0, 1, 0)))
    schild = Vector((wx, wy + 0.18, ZB + 0.80))
    for i in range(14):
        w0, w1 = 2 * math.pi * i / 14, 2 * math.pi * (i + 1) / 14
        p0 = schild + Vector((math.cos(w0), 0, math.sin(w0))) * 0.34; p1 = schild + Vector((math.cos(w1), 0, math.sin(w1))) * 0.34
        a.vieleck("Innen", [schild - Vector((0, 0.03, 0)), p0, p1], [(0.12, 0.99), (0.13, 0.985), (0.14, 0.99)], zu=schild - Vector((0, 1, 0)))
    # Fackeln (Halter aus Eisen, Stiel aus Holz)
    for (fx, fy, n) in FACKELN:
        p = Vector((fx, fy, ZB + 2.05)); n = Vector(n)
        a.stab("Innen", p - n * 0.02, p + n * 0.16 + EZ * 0.06, 0.03, "kleinteile", u_bereich=feld("eisen"))
        a.stab("Innen", p + n * 0.16 - EZ * 0.15, p + n * 0.16 + EZ * 0.20, 0.045, "balken", kappen=True)
    # Bank an der Trennwand vorn
    bx = X0 + 0.25
    a.kasten("Innen", (bx - 0.20, -3.35, ZB + 0.42), (bx + 0.20, -1.95, ZB + 0.47), "dielen", laengs="y")
    a.kasten("Innen", (X0 + 0.02, -3.35, ZB + 0.47), (X0 + 0.07, -1.95, ZB + 0.95), "dielen", laengs="y")
    for y in (-3.25, -2.10):
        a.kasten("Innen", (bx - 0.18, y - 0.04, ZB), (bx + 0.18, y + 0.04, ZB + 0.42), "balken", ohne=("+z", "-z"))
    # Faesser und Kisten links neben dem Portal
    for (fx, fy, r) in ((-0.95, -3.25, 0.30), (-1.62, -3.35, 0.28)):
        a.dreh("Innen", (fx, fy, ZB), [(r * 0.86, 0.0), (r, 0.40), (r * 0.86, 0.80)], "balken", seiten=12, gedreht=True)
        for zr in (0.12, 0.68):
            a.dreh("Innen", (fx, fy, ZB + zr - 0.025), [(r * 0.89 + 0.008, 0.0), (r * 0.89 + 0.008, 0.05)], "kleinteile", seiten=12, deckel=False, u_bereich=feld("eisen"))
    a.kasten("Innen", (-2.35, -3.55, ZB), (-1.85, -3.05, ZB + 0.50), "dielen")
    a.kasten("Innen", (-2.30, -3.50, ZB + 0.50), (-1.95, -3.12, ZB + 0.82), "dielen")
    # Schreibpult mit Buechern und Schemel vorn rechts unter dem Arkadenfenster
    B.tisch(a, "Innen", "taefelung", 1.95, 2.85, -3.55, -3.00, hoehe=0.78)
    a.kasten("Innen", (2.05, -3.45, ZB + 0.78), (2.40, -3.20, ZB + 0.86), "stoff")
    a.kasten("Innen", (2.10, -3.40, ZB + 0.86), (2.36, -3.22, ZB + 0.92), "kleinteile")
    a.rechteck("Innen", (2.45, -3.40, ZB + 0.785), (1, 0, 0), (0, 1, 0), 0.30, 0.22, "kleinteile", u_bereich=feld("pergament"), zu=(2.6, -3.3, 5))
    a.dreh("Innen", (2.75, -3.18, ZB + 0.78), [(0.03, 0.0), (0.035, 0.06), (0.02, 0.08)], "kleinteile", seiten=8, u_bereich=feld("tinte"))
    a.dreh("Innen", (2.40, -2.62, ZB), [(0.16, 0.0), (0.14, 0.46), (0.17, 0.48)], "balken", seiten=10)
    return a


FACKELN = [(X0, -0.20, (1, 0, 0)), (X1, -1.40 - 0.25, (-1, 0, 0)), (X1 - 0.45, Y1, (0, -1, 0))]   # Trennwand, rechte Wand, Rueckwand


def baue_glas():
    g = Teil("Glas_Halle", BI, saat=23)
    for w, (a0, a1), (z0, z1), art, raum in M.OEFFNUNGEN:
        if raum == "eingangshalle" and art == "bogenfenster":
            B.scheibe(g, "Innenglas", w, a0, a1, z0, z1, -M.WAND_DICKE[w] / 2 - 0.004, zu=RAUM)
    return g


def anker_raum():
    gy = tuer("Tuer_Halle_Gericht")[1]; ty = tuer("Tuer_Halle_Turm")[1]; ay = tuer("Tuer_Halle_Amt")[1]
    kx = tuer("Tuer_Halle_Kerker")[0]; ky = tuer("Tuer_Halle_Kerker")[1][0]
    a0, a1 = M.ARKADE[1]
    zm = ZB + TH / 2
    k = [
        B.anker("Blick_Auge", (-1.75, -3.15, ZB + 1.60)),            # neben dem Portal, Augenhoehe 1,60
        B.anker("Blick_Ziel", (1.60, 2.20, ZB + 1.45)),              # Blick in die Halle: Treppe, Kellertuer, Amtstuer, Anschlagbrett
        B.anker("Tuer_Halle_Stadt", ((a0 + a1) / 2, Y0 - 0.02, ZB + 1.55), ziel="stadt", breite=a1 - a0, hoehe=3.0, achse="y"),
        B.anker("Tuer_Halle_Gericht", (X0 + 0.02, (gy[0] + gy[1]) / 2, zm), ziel="rathaus:gericht", breite=TB, hoehe=TH, achse="x"),
        B.anker("Tuer_Halle_Turm", (X1 - 0.02, (ty[0] + ty[1]) / 2, zm), ziel="rathaus:turm", breite=TB, hoehe=TH, achse="x"),
        B.anker("Tuer_Halle_Amt", (X1 - 0.02, (ay[0] + ay[1]) / 2, zm), ziel="rathaus:amt", breite=TB, hoehe=TH, achse="x"),
        B.anker("Tuer_Halle_Kerker", ((kx[0] + kx[1]) / 2, ky - 0.04, zm), ziel="rathaus:kerker", breite=TB, hoehe=TH, achse="y"),
        B.anker("Treppe_Halle_Rat", (X0 + M.TREPPE_BREITE / 2, (M.H_LAUF1_Y0 + M.H_PODEST[2]) / 2, ZB + 1.1), ziel="rathaus:rat", groesse="1.2,2.4,2.4"),
        B.anker("Licht_Raum", (STUETZE_X, 0.6, ZD - 0.6), art="Raum"),
    ]
    for i, (fx, fy, n) in enumerate(FACKELN):
        p = Vector((fx, fy, ZB + 2.05)) + Vector(n) * 0.16
        k.append(B.anker(f"Licht_Fackel_{i + 1}", tuple(p + EZ * 0.35 + Vector(n) * 0.10), art="Fackel"))
        k.append(B.anker(f"Feuer_Fackel_{i + 1}", tuple(p + EZ * 0.20)))
    return k


def main():
    opt = B.optionen()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    stoffe = B.stoffe_innen()
    raum, treppe, tueren, ausstattung, brett, glas = baue_raum(), baue_treppe(), baue_tueren(), baue_ausstattung(), baue_anschlagbrett(), baue_glas()
    teile = [raum, treppe, tueren, ausstattung, brett, glas]
    print("DREIECKE Eingangshalle:", {t.name: t.dreiecke() for t in teile}, "Summe", sum(t.dreiecke() for t in teile))
    objekte = [t.objekt(stoffe, mit_farbe=False) for t in teile]
    o_raum, o_treppe, o_tueren, o_aus, o_brett, o_glas = objekte
    B.uv2_und_ao([([o_raum], []), ([o_treppe, o_tueren, o_aus, o_brett], [o_raum])], "rathaus_innen", "eingangshalle", mit_ao="ohne-ao" not in opt)
    anker = anker_raum()
    B.exportiere(objekte + anker, os.path.join(B.ZIEL_MODELL, "rathaus_eingangshalle_innen.glb"))
    if "bilder" in opt:
        bilder(opt["bilder"], objekte)


def bilder(o, objekte):
    """Kontrollbilder: Blick der Raumkamera (Augenhoehe 1,60, Brennweite wie Oeffnungswinkel 66 Grad) und Uebersicht; dazu die
    Huelle aussen (rathaus.glb), damit Fenster, Portal und Tueren an ihrer Stelle sitzen, und Massfiguren (Mensch 1,75, Oger 2,50)."""
    os.makedirs(o, exist_ok=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(B.ZIEL_MODELL, "rathaus.glb"))
    for ob in list(bpy.context.selected_objects):
        if ob.name.startswith("Scheiben"): ob.hide_render = True
    mass = Teil("Massfiguren", BI)
    for (x, y, h, b) in ((0.6, -1.6, 1.75, 0.46), (-0.4, 0.2, 2.50, 0.66), (2.55, 1.0, 2.50, 0.66)):
        B.massfigur(mass, "Mass", (x, y, ZB), h, b)
    mass.objekt({"Mass": B.stoff("Mass", farbe=(0.85, 0.35, 0.1))}, mit_farbe=False)
    lampe = bpy.data.objects.new("Kontrolllampe", bpy.data.lights.new("Kontrolllampe", "POINT"))
    lampe.data.energy = 900; lampe.location = (0.3, 0.0, 3.6)
    bpy.context.scene.collection.objects.link(lampe)
    for i, (fx, fy, n) in enumerate(FACKELN):
        l = bpy.data.objects.new(f"Fackellicht{i}", bpy.data.lights.new(f"Fackellicht{i}", "POINT"))
        l.data.energy = 120; l.data.color = (1.0, 0.7, 0.4); l.location = Vector((fx, fy, ZB + 2.4)) + Vector(n) * 0.3
        bpy.context.scene.collection.objects.link(l)
    auge = bpy.data.objects["Blick_Auge"].location; ziel = bpy.data.objects["Blick_Ziel"].location
    brenn = 18.0 / math.tan(math.radians(66) / 2) / 1.0    # 36-mm-Sensor: waagerechter Oeffnungswinkel 66 Grad
    B.kontrollbild(os.path.join(o, "halle_raumkamera.jpg"), tuple(auge), tuple(ziel), brennweite=brenn, verbergen=("Massfiguren",), proben=48, sonne=True)
    B.kontrollbild(os.path.join(o, "halle_raumkamera_mass.jpg"), tuple(auge), tuple(ziel), brennweite=brenn, proben=32)
    B.kontrollbild(os.path.join(o, "halle_zum_portal.jpg"), (-1.0, 2.0, ZB + 1.6), (0.9, -3.7, ZB + 1.6), brennweite=brenn, verbergen=("Massfiguren",), proben=48)
    B.kontrollbild(os.path.join(o, "halle_treppe_kerkertuer.jpg"), (1.6, -2.2, ZB + 1.6), (0.6, 3.6, ZB + 1.9), brennweite=brenn, verbergen=("Massfiguren",), proben=48)
    # Uebersicht von oben schraeg: Decke, Obergeschoss und Dach ausgeblendet
    oben = [ob.name for ob in bpy.data.objects if ob.name in ("Dach", "Scheiben")]
    raum = bpy.data.objects["Raum_Halle"]
    import bmesh
    bm = bmesh.new(); bm.from_mesh(raum.data)
    weg = [f for f in bm.faces if f.calc_center_median().z > ZD - 0.5]
    bmesh.ops.delete(bm, geom=weg, context="FACES"); bm.to_mesh(raum.data); bm.free()
    haus = bpy.data.objects.get("Haus")
    if haus is not None: haus.hide_render = True
    B.kontrollbild(os.path.join(o, "halle_uebersicht.jpg"), (5.5, -8.0, 9.5), (0.2, 0.3, 0.8), brennweite=24, verbergen=tuple(oben), proben=48)


if __name__ == "__main__":
    main()
