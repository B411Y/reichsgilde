"""Neubau Dom evangelisch, Innenraum: dreischiffige Halle mit Kreuzgewoelben auf Rundpfeilern, geputzte Waende, Chor mit Chorschluss;
Baenke, Kanzel, Taufstein, Leuchter, Chorgestuehl, Orgelempore und je Stadtglaube eine Einrichtung (dieselben Knoten wie im Dom katholisch).

    blender -b --python tools/modelle/neubau/dom_evangelisch_innen.py -- [--bilder=<ordner>] [--ohne-ao]

Schreibt game/assets/modelle/gebaeude/neubau/dom_evangelisch_innen.glb mit den Knoten
    Raum_Dom                  Waende, Boeden, Gewoelbe, Pfeiler (Material Innen)
    Glas_Dom                  Scheiben (Innenglas) in derselben Ebene wie aussen
    Moebel_Dom                Baenke, Kanzel, Taufstein, Leuchter, Chorgestuehl, Orgelempore, Tuerblaetter
    Einrichtung_Katholisch    Hochaltar mit Retabel, Altarkerzen, Marienaltar, Fahnen, Laeufer
    Weihrauch                 Ampel an der Kette (Ursprung am Aufhaengepunkt), gehoert zu Katholisch
    Einrichtung_Evangelisch   Abendmahlstisch mit Bibel und Kerzen
    Kreuz_Evangelisch         Holzkreuz im Chorschluss
    Einrichtung_Simultan      Schranke im Mittelgang
    Anker: Blick_Auge, Blick_Ziel, Tuer_Dom_Stadt, Licht_Raum_*, Licht_Kerze_*/Feuer_Kerze_*, Feuer_Altarkerze_*, Glaeubig_<n>,
           Predigt_1, Anker_Bischof, Anker_Pfarrer
und die gebackene Verdeckung in game/assets/texturen/neubau/dom_innen_ao_atlas.png (Gruppe "dom_innen", Platz dom_evangelisch).
Alle Masse aus dom_evangelisch_masse.py: dieselben Waende und Oeffnungen wie die Huelle (dom_evangelisch_aussen.py). Blatt dom_innen_trim
(wie der Dom katholisch); die Waende tragen hellen Putz (Band "gewoelbe") statt des grauen Werksteins.
"""
import math
import os
import sys

import bpy
from mathutils import Vector as V

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rathaus_bib as B                                  # noqa: E402
import dom_bib as D                                      # noqa: E402
import dom_evangelisch_masse as M                        # noqa: E402
from rathaus_bib import EZ                               # noqa: E402

BI = D.BAENDER_DOM_INNEN
MODELL = "dom_evangelisch"
ZB, ZK, ZS = M.Z_SOCKEL, M.Z_KAEMPFER, M.Z_SCHEITEL
XM, XP, XS = M.X_MITTE, M.PFEILER_X, M.X_SEITE
Y0, Y1 = M.Y_INNEN0, M.Y_INNEN1
PUTZ = "gewoelbe"


def scheidbogen(r, x, ya, yb, teile=5):
    """Bogen zwischen zwei Pfeilern in der Achse x (Grenze von Mittel- und Seitenschiffgewoelbe)."""
    cy, ay, H = (ya + yb) / 2, (yb - ya) / 2, ZS - ZK
    hs = [H * math.sin(math.pi / 2 * k / teile) for k in range(teile + 1)]
    dy = [D.spitz_breite(ay, H, h) for h in hs]
    for sy in (-1, 1):
        for k in range(teile):
            D.rippe(r, (x, cy + sy * dy[k], ZK + hs[k]), (x, cy + sy * dy[k + 1], ZK + hs[k + 1]), breite=0.30, tiefe=0.22, band="sandstein")


def raum():
    r = D.Teil("Raum_Dom", BI, saat=51)
    cs0, cs1 = M.CHORSTUFEN_Y
    # ── Boeden ──
    D.schachboden(r, -XS, XS, Y0, cs0, ZB)
    for sx in (-1, 1):
        D.schachboden(r, min(sx * XM, sx * XS), max(sx * XM, sx * XS), cs0, Y1, ZB)
    D.schachboden(r, -XM, XM, Y1, M.ALTARSTUFEN_Y[1], M.Z_CHOR)
    B.gerade_treppe(r, "Innen", "sandstein", (0, cs0), (0, 1, 0), (1, 0, 0), 2 * XM, 3, 0.15, 0.30, ZB, block=True)
    B.gerade_treppe(r, "Innen", "sandstein", (0, M.ALTARSTUFEN_Y[0]), (0, 1, 0), (1, 0, 0), 6.4, 2, 0.15, 0.30, M.Z_CHOR, block=True)
    v0, v1, mm = BI["sandstein"]
    mitte = V((0, M.Y_APSIS + 1.9, M.Z_ALTAR))
    rand = [V((-3.3, M.ALTARSTUFEN_Y[1], M.Z_ALTAR)), V((3.3, M.ALTARSTUFEN_Y[1], M.Z_ALTAR))] + [V((*M.apsis_ecke(k, M.R_APSIS_INNEN + 0.2), M.Z_ALTAR)) for k in range(1, 5)]
    for i in range(len(rand)):
        a, b = rand[i], rand[(i + 1) % len(rand)]
        r.vieleck("Innen", [mitte, a, b], [(0, v1), (1, v1), (1, v0)], zu=mitte + EZ * 5)
    # ── Waende der Halle: heller Putz, Laibungen und Masswerk aus Werkstein ──
    t = M.T_WAND
    for name in ("seite_l", "seite_r", "west"):
        D.innenwand(r, M, name, ZB, ZS, band=PUTZ, s_bereich=(t, M.wand(name)[2] - t))
    # Ostwand mit dem Chorbogen (Laibung = volle Wandstaerke)
    D.flaeche(r, (XS, Y1), (-XS, Y1), (0, -1), ZB, ZS, band=PUTZ, loecher_=[D.Spitzloch(XS - XM, XS + XM, ZB, ZS, (ZS - ZK) / XM, sohlbank=False)], dicke=t)
    # ── Chor ──
    for seite in "lr":
        D.innenwand(r, M, f"chor_{seite}", ZB, ZS, band=PUTZ)
    b_, z0, z1 = M.FENSTER_CHOR
    c = V((0, M.Y_APSIS, 0))
    teile = 5
    H = ZS - ZK
    hs = [H * math.sin(math.pi / 2 * k / teile) for k in range(teile + 1)]
    ts = [D.spitz_breite(M.R_APSIS_INNEN, H, h) / M.R_APSIS_INNEN for h in hs]
    for k in range(5):
        a, b = M.apsis_ecke(k + 1, M.R_APSIS_INNEN), M.apsis_ecke(k, M.R_APSIS_INNEN)
        l = math.hypot(b[0] - a[0], b[1] - a[1])
        n = ((a[0] + b[0]) / -2, M.Y_APSIS - (a[1] + b[1]) / 2)
        D.flaeche(r, a, b, n, ZB, ZS, band=PUTZ, loecher_=[D.Spitzloch(l / 2 - b_ / 2, l / 2 + b_ / 2, z0, z1, M.STEIL)], dicke=M.wand("apsis_0")[4] / 2)
        A = [c + (V((*a, 0)) - c) * t_ + EZ * (ZK + h) for t_, h in zip(ts, hs)]
        Bn = [c + (V((*b, 0)) - c) * t_ + EZ * (ZK + h) for t_, h in zip(ts, hs)]
        for j in range(teile):
            D._kappe(r, [A[j], Bn[j], Bn[j + 1], A[j + 1]], c + EZ * (ZK - 5))
            D.rippe(r, A[j], A[j + 1])
            if k == 4: D.rippe(r, Bn[j], Bn[j + 1])
    # Masswerk von innen (vor der Scheibe), wie aussen
    for o in M.OEFFNUNGEN:
        if o[5] != "spitz": continue
        name = o[0]; d = M.wand(name)[4]; nn = M.wand(name)[3]
        D.masswerk(r, lambda s_, z, name=name, d=d: V(M.punkt(name, s_, z, -d / 2 - 0.07)), V((-nn[0], -nn[1], 0)), D.loch_von(o), stoff="Innen")
    # ── Rundpfeiler und Gewoelbe: drei gleich hohe Schiffe, Scheidboegen zwischen den Pfeilern ──
    for sx in (-1, 1):
        for y in M.PFEILER_Y:
            D.saeule(r, (sx * XP, y), M.PFEILER_R, ZB, ZK)
    for i in range(M.JOCHE):
        ya, yb = Y0 + i * M.JOCH, Y0 + (i + 1) * M.JOCH
        gurt = ("y1",) if i < M.JOCHE - 1 else ()
        D.kreuzgewoelbe(r, -XP, XP, ya, yb, ZK, ZS, gurt=gurt)
        for sx in (-1, 1):
            D.kreuzgewoelbe(r, min(sx * XP, sx * XS), max(sx * XP, sx * XS), ya, yb, ZK, ZS, gurt=gurt)
            scheidbogen(r, sx * XP, ya, yb)
    D.kreuzgewoelbe(r, -XM, XM, M.Y_OST, M.Y_APSIS, ZK, ZS, gurt=("y1",))
    return r


def glas():
    g = D.Teil("Glas_Dom", BI, saat=87)
    for o in M.OEFFNUNGEN:
        if o[5] == "portal": continue
        name = o[0]
        d = M.wand(name)[4]
        loch = D.loch_von(o)
        P = lambda s, z, name=name, d=d: V(M.punkt(name, s, z, -d / 2))
        D.scheibe(g, "Innenglas", P, loch, V(M.punkt(name, (loch.s0 + loch.s1) / 2, (loch.z0 + loch.z1) / 2, -5)))
    return g


def kerze(t, x, y, z, hoehe=0.25, dick=0.05):
    D.farbkasten(t, "Innen", (x - dick / 2, y - dick / 2, z), (x + dick / 2, y + dick / 2, z + hoehe), "wachs", ohne=("-z",))


LEUCHTER = [(-3.0, M.CHORSTUFEN_Y[0] - 0.5, ZB), (3.0, M.CHORSTUFEN_Y[0] - 0.5, ZB), (-2.7, M.Y_APSIS + 0.3, M.Z_CHOR), (2.7, M.Y_APSIS + 0.3, M.Z_CHOR),
            (-3.0, Y0 + 0.6, ZB), (3.0, Y0 + 0.6, ZB)]
ALTARKERZEN = [(M.HOCHALTAR[0] - 1.05 + i * 0.42, M.HOCHALTAR[1] + 0.28, M.Z_ALTAR + 1.04) for i in range(6)]


def orgel(m):
    """Orgelempore im rechten Seitenschiff vor der Ostwand: Buehne auf vier Pfosten, Bruestung, Orgel mit Pfeifenprospekt."""
    (x0, x1), (y0, y1), hoch = M.ORGEL
    zf = ZB + hoch
    k = lambda mn, mx, **kw: m.kasten("Innen", mn, mx, "holz", **kw)
    k((x0, y0, zf - 0.14), (x1, y1, zf), laengs="x")
    for x in (x0 + 0.04, x1 - 0.20):
        for y in (y0 + 0.04, y1 - 0.20):
            k((x, y, ZB), (x + 0.16, y + 0.16, zf - 0.14), ohne=("-z", "+z"))
    # Bruestung zum Schiff (vorn und links), Handlauf darauf
    k((x0, y0, zf), (x1, y0 + 0.05, zf + 1.00), laengs="x", ohne=("-z", "+z"))
    k((x0, y0 + 0.05, zf), (x0 + 0.05, y1, zf + 1.00), laengs="y", ohne=("-z", "+z", "-y"))
    k((x0 - 0.02, y0 - 0.02, zf + 1.00), (x1, y0 + 0.07, zf + 1.06), laengs="x")
    k((x0 - 0.02, y0 + 0.07, zf + 1.00), (x0 + 0.07, y1, zf + 1.06), laengs="y", ohne=("-y",))
    # Orgel: Unterbau mit Spieltisch, Gehaeuse, Pfeifen im Prospekt (die mittleren am laengsten), Kranz
    xc = (x0 + x1) / 2
    k((xc - 0.80, y1 - 0.58, zf), (xc + 0.80, y1, zf + 0.90), laengs="x", ohne=("-z",))
    k((xc - 0.80, y1 - 0.44, zf + 0.90), (xc + 0.80, y1, zf + 2.90), ohne=("-z", "+z"))
    k((xc - 0.87, y1 - 0.50, zf + 2.90), (xc + 0.87, y1, zf + 3.02), laengs="x")
    for i in range(9):
        x = xc - 0.68 + i * 0.17
        lang = 1.75 - 0.20 * abs(i - 4)
        D.farbkasten(m, "Innen", (x - 0.055, y1 - 0.53, zf + 0.90), (x + 0.055, y1 - 0.45, zf + 0.98 + lang), "messing", ohne=("-z", "+y"))


def moebel():
    m = D.Teil("Moebel_Dom", BI, saat=61)
    # Baenke: Sitzflaeche auf SITZHOEHE, Lehne im Ruecken (Westen), Wangen
    bb, tiefe, _ = M.BANK
    zs = ZB + M.SITZHOEHE
    for reihe in range(M.BANK_REIHEN):
        y = M.BANK_Y0 + reihe * M.BANK[2]
        for sx in (-1, 1):
            x0, x1 = sx * M.BANK_X - bb / 2, sx * M.BANK_X + bb / 2
            m.kasten("Innen", (x0, y - 0.20, zs - 0.04), (x1, y + 0.25, zs), "holz", laengs="x")
            m.kasten("Innen", (x0, y - 0.27, ZB + 0.12), (x1, y - 0.22, ZB + 0.98), "holz", laengs="x", ohne=("-z",))
            for x in (x0 - 0.04, x1):
                m.kasten("Innen", (x, y - 0.30, ZB), (x + 0.04, y + 0.27, ZB + 1.04), "holz", ohne=("-z",))
    # Kanzel: Fuss, Korb, Schalldeckel, Treppe
    kx, ky, kh = M.KANZEL
    c = (kx, ky, 0)
    m.dreh("Innen", c, [(0.34, ZB), (0.20, ZB + 0.18), (0.20, ZB + kh - 0.35), (0.72, ZB + kh - 0.05)], "holz", seiten=8, deckel=True, gedreht=True, glatt=False)
    m.dreh("Innen", c, [(0.72, ZB + kh - 0.05), (0.78, ZB + kh + 1.00), (0.82, ZB + kh + 1.05)], "holz", seiten=8, deckel=False, gedreht=True, glatt=False)
    m.dreh("Innen", c, [(0.80, ZB + kh + 1.04), (0.70, ZB + kh - 0.02)], "holz", seiten=8, deckel=False, gedreht=True, glatt=False)       # Innenseite der Bruestung
    m.dreh("Innen", c, [(0.0, ZB + kh + 2.55), (0.95, ZB + kh + 2.62), (0.95, ZB + kh + 2.76), (0.45, ZB + kh + 3.05), (0.0, ZB + kh + 3.6)], "holz", seiten=8, deckel=False, gedreht=True, glatt=False)
    m.stab("Innen", (kx - 0.74, ky, ZB + kh + 1.0), (kx - 0.74, ky, ZB + kh + 2.6), 0.10, "holz", breite=0.5)                             # Rueckwand traegt den Schalldeckel
    B.gerade_treppe(m, "Innen", "holz", (kx - 0.2, ky + 0.70 + 7 * 0.24), (0, -1, 0), (1, 0, 0), 0.70, 7, kh / 7, 0.24, ZB, wange=("Innen", "holz"))
    # Taufstein
    tx, ty = M.TAUFSTEIN
    m.dreh("Innen", (tx, ty, 0), [(0.46, ZB), (0.40, ZB + 0.14), (0.22, ZB + 0.24), (0.22, ZB + 0.62), (0.58, ZB + 0.92), (0.60, ZB + 1.05), (0.50, ZB + 1.05), (0.46, ZB + 0.98), (0.0, ZB + 0.98)],
           "werkstein", seiten=8, deckel=False, glatt=False)
    # Standleuchter
    for x, y, z in LEUCHTER:
        D.farbstab(m, "Innen", (x, y, z), (x, y, z + 1.45), 0.05, "eisen")
        D.farbkasten(m, "Innen", (x - 0.14, y - 0.14, z), (x + 0.14, y + 0.14, z + 0.04), "eisen", ohne=("-z",))
        D.farbkasten(m, "Innen", (x - 0.09, y - 0.09, z + 1.45), (x + 0.09, y + 0.09, z + 1.48), "eisen")
        kerze(m, x, y, z + 1.48, hoehe=0.30, dick=0.06)
    # Chorgestuehl: frei vor beiden Chorwaenden (6 cm Abstand), Rueckwand mit Gesims, Sitzbank mit Wangen je Platz, Pultwand davor
    y0, y1 = M.CHORGESTUEHL
    zc = M.Z_CHOR
    for sx in (-1, 1):
        w = sx * (XM - 0.06)
        def k(xa, xb, ya, yb, za, zb, **kw):
            m.kasten("Innen", (min(w - sx * xa, w - sx * xb), ya, za), (max(w - sx * xa, w - sx * xb), yb, zb), "holz", **kw)
        k(0.0, 0.06, y0, y1, zc, zc + 1.90, laengs="y", ohne=("-z",))                 # Rueckwand
        k(-0.02, 0.12, y0 - 0.03, y1 + 0.03, zc + 1.90, zc + 1.98, laengs="y")        # Gesims
        k(0.06, 0.52, y0, y1, zc + M.SITZHOEHE - 0.04, zc + M.SITZHOEHE, laengs="y")  # Sitz
        k(0.46, 0.50, y0, y1, zc, zc + M.SITZHOEHE - 0.04, laengs="y", ohne=("-z", "+z"))
        plaetze = 4
        for i in range(plaetze + 1):                                                  # Wangen zwischen den Plaetzen
            y = y0 + (y1 - y0 - 0.05) * i / plaetze
            k(0.06, 0.56, y, y + 0.05, zc, zc + (1.25 if i in (0, plaetze) else 0.72), ohne=("-z",))
        k(0.98, 1.03, y0, y1, zc, zc + 0.95, laengs="y", ohne=("-z",))                # Pultwand
        k(0.86, 1.08, y0, y1, zc + 0.95, zc + 0.99, laengs="y")
        for y in (y0, y1 - 0.05):
            k(0.86, 1.08, y, y + 0.05, zc, zc + 0.95, ohne=("-z", "+z"))
    orgel(m)
    # Portal von innen: zwei Tuerblaetter in der Wandmitte
    b_, z0, z1 = M.PORTAL
    for seite in (-1, 1):
        B.bretttuer(m, "Innen", "holz", "Innen", "kleinteile", (seite * b_ / 4, M.Y_WEST + 0.32, z0), (1, 0, 0), (0, 1, 0), b_ / 2 - 0.01, z1 - z0, bretter=4, eisen_ub=D.feld("eisen", True))
    return m


def katholisch():
    k = D.Teil("Einrichtung_Katholisch", BI, saat=71)
    ax, ay = M.HOCHALTAR
    za = M.Z_ALTAR
    # Mensa aus Stein mit weissem Tuch und rotem Antependium
    D.block(k, "Innen", "sandstein", (ax - 1.30, ay - 0.50), (1, 0), (0, 1), 2.60, 1.00, za, za + 1.00, ohne=("unten", "oben"))
    k.kasten("Innen", (ax - 1.36, ay - 0.55, za + 1.00), (ax + 1.36, ay + 0.55, za + 1.04), "leinen", laengs="x", ohne=("-z",))
    k.rechteck("Innen", (ax - 1.10, ay - 0.515, za + 0.12), (1, 0, 0), (0, 0, 1), 2.20, 0.80, "stoff", zu=(ax, ay - 5, za))
    for x, y, z in ALTARKERZEN:
        D.farbkasten(k, "Innen", (x - 0.04, y - 0.04, z), (x + 0.04, y + 0.04, z + 0.22), "messing", ohne=("-z",))
        kerze(k, x, y, z + 0.22, hoehe=0.35, dick=0.045)
    D.farbkasten(k, "Innen", (ax - 0.22, ay + 0.10, za + 1.04), (ax + 0.22, ay + 0.38, za + 1.62), "gold", ohne=("-z",))      # Tabernakel
    # Retabel: Schrein auf der Predella, Mittelbild mit Kreuz, Seitenfelder, zwei bemalte Fluegel, Gesprenge aus Fialen
    ry = M.RETABEL_Y
    vorn = lambda x0, x1, z0, z1, name, y=ry - 0.012: D.farbflaeche(k, "Innen", [V((x0, y, z0)), V((x1, y, z0)), V((x1, y, z1)), V((x0, y, z1))], name, zu=((x0 + x1) / 2, y - 6, z0))
    def tafel(x0, x1, z0, z1, y, dreh=0.0):
        a, b = V((x0, y, 0)), V((x1, y - dreh, 0))
        k.rechteck("Innen", a + EZ * z0, b - a, EZ, (b - a).length, z1 - z0, "gold", laengs="v", zu=((x0 + x1) / 2, y - 6, z0))
        e_ = (b - a).normalized(); hinten = V((-e_.y, e_.x, 0)) * (1 if e_.x > 0 else -1) * 0.06     # Rueckseite 6 cm hinter der Bildseite, senkrecht zur Tafel
        k.rechteck("Innen", a + EZ * z0 + hinten, b - a, EZ, (b - a).length, z1 - z0, "holz", laengs="v", zu=(a + b) / 2 + hinten * 100 + EZ * z0)
        if dreh:                                                                                     # Fluegel stehen frei: Stirn aussen und Oberkante schliessen
            k.rechteck("Innen", b + EZ * z0, hinten, EZ, hinten.length, z1 - z0, "holz", laengs="v", zu=b + e_ * 6 + EZ * z0)
            k.rechteck("Innen", a + EZ * z1, b - a, hinten, (b - a).length, hinten.length, "holz", zu=(a + b) / 2 + EZ * (z1 + 6))
    D.block(k, "Innen", "sandstein", (ax - 1.5, ry - 0.2), (1, 0), (0, 1), 3.0, 0.32, za, za + 1.25, ohne=("unten",))       # Sockel hinter der Mensa (hinten frei vor der Chorwand)
    k.rechteck("Innen", (ax - 1.45, ry - 0.215, za + 1.25), (1, 0, 0), (0, 0, 1), 2.90, 0.42, "gold", zu=(ax, ry - 6, za))     # Predella
    for i in range(5):
        x = ax - 1.30 + i * 0.54
        vorn(x, x + 0.44, za + 1.30, za + 1.62, ("rot", "blau", "gruen", "blau", "rot")[i], y=ry - 0.225)
    zs0, zs1 = za + 1.67, za + 4.00                                                                                           # Schrein
    tafel(ax - M.RETABEL_HALB, ax + M.RETABEL_HALB, zs0, zs1, ry)
    vorn(ax - 0.62, ax + 0.62, zs0 + 0.18, zs1 - 0.18, "blau")                                                                # Mittelbild
    for sx in (-1, 1):
        vorn(ax + 0.74 if sx > 0 else ax - 1.28, ax + 1.28 if sx > 0 else ax - 0.74, zs0 + 0.18, zs0 + 1.10, "rot")
        vorn(ax + 0.74 if sx > 0 else ax - 1.28, ax + 1.28 if sx > 0 else ax - 0.74, zs0 + 1.22, zs1 - 0.18, "gruen")
    # Kreuz: Stamm und zwei einzelne Arme (ein durchgehender Querbalken laege deckungsgleich im Stamm)
    D.farbkasten(k, "Innen", (ax - 0.06, ry - 0.06, zs0 + 0.36), (ax + 0.06, ry - 0.014, zs1 - 0.36), "gold", ohne=("+y",))
    for sx in (-1, 1):
        D.farbkasten(k, "Innen", (min(ax + sx * 0.06, ax + sx * 0.42), ry - 0.06, zs0 + 1.38), (max(ax + sx * 0.06, ax + sx * 0.42), ry - 0.014, zs0 + 1.50), "gold", ohne=("+y", "-x" if sx > 0 else "+x"))
    # Fluegel: um 45 Grad nach vorn aufgeklappt, je zwei Bildfelder (frei vor der Chorwand: dom_evangelisch_masse.retabel_abstand)
    for sx in (-1, 1):
        xa, xb, dreh = ax + sx * M.RETABEL_HALB, ax + sx * (M.RETABEL_HALB + M.RETABEL_FLUEGEL * math.cos(math.radians(45))), M.RETABEL_FLUEGEL * math.sin(math.radians(45))
        tafel(xa, xb, zs0 + 0.10, zs1 - 0.10, ry, dreh=dreh)
        e = V((xb - xa, -dreh, 0)).normalized(); nrm = V((-e.y * sx, e.x * sx, 0)) * (-1 if (V((-e.y * sx, e.x * sx, 0)).y > 0) else 1)
        for za_, zb_, name in ((zs0 + 0.26, zs0 + 1.08, "blau"), (zs0 + 1.22, zs1 - 0.26, "rot")):
            p0 = V((xa, ry, 0)) + e * 0.14 + nrm * 0.012; p1 = V((xa, ry, 0)) + e * (V((xb - xa, -dreh, 0)).length - 0.14) + nrm * 0.012
            D.farbflaeche(k, "Innen", [p0 + EZ * za_, p1 + EZ * za_, p1 + EZ * zb_, p0 + EZ * zb_], name, zu=(p0 + p1) / 2 + nrm * 6 + EZ * za_)
    # Gesprenge: fuenf Fialen ueber dem Schrein, die mittlere hoch; Kehlleiste als Abschluss
    D.farbkasten(k, "Innen", (ax - 1.46, ry - 0.05, zs1), (ax + 1.46, ry + 0.09, zs1 + 0.10), "gold")
    for i, hoch in enumerate((0.7, 1.0, 1.7, 1.0, 0.7)):
        x = ax - 1.32 + i * 0.66
        D.farbkasten(k, "Innen", (x - 0.06, ry - 0.04, zs1 + 0.10), (x + 0.06, ry + 0.08, zs1 + 0.10 + hoch * 0.6), "gold", ohne=("-z", "+z"))
        sp = V((x, ry + 0.02, zs1 + 0.10 + hoch))
        fuss = [V((x - 0.09, ry - 0.07, zs1 + 0.10 + hoch * 0.6)), V((x + 0.09, ry - 0.07, zs1 + 0.10 + hoch * 0.6)), V((x + 0.09, ry + 0.11, zs1 + 0.10 + hoch * 0.6)), V((x - 0.09, ry + 0.11, zs1 + 0.10 + hoch * 0.6))]
        for q in range(4):
            D.farbflaeche(k, "Innen", [fuss[q], fuss[(q + 1) % 4], sp], "gold", weg_von=V((x, ry + 0.02, zs1)))
    # Laeufer ueber Stufen und Podest
    pfad = [(M.ALTARSTUFEN_Y[0] - 2.2, M.Z_CHOR), (M.ALTARSTUFEN_Y[0], M.Z_CHOR), (M.ALTARSTUFEN_Y[0], M.Z_CHOR + 0.15), (M.ALTARSTUFEN_Y[0] + 0.3, M.Z_CHOR + 0.15),
            (M.ALTARSTUFEN_Y[0] + 0.3, za), (ay - 0.6, za)]
    for (y0, z0), (y1, z1) in zip(pfad[:-1], pfad[1:]):
        lang = math.hypot(y1 - y0, z1 - z0)
        nrm = V((0, -(z1 - z0), (y1 - y0))).normalized()
        o = V((ax - 0.8, y0, z0)) + nrm * 0.012
        k.rechteck("Innen", o, V((0, y1 - y0, z1 - z0)), V((1, 0, 0)), lang, 1.6, "stoff", zu=o + nrm * 5 + V((0.8, 0, 0)))
    # Marienaltar im linken Seitenschiff an der Aussenwand: Sockel, Figur im blauen Mantel
    mx, my = M.MARIENALTAR
    D.block(k, "Innen", "sandstein", (mx - 0.9, my - 0.40), (1, 0), (0, 1), 0.8, 0.80, ZB, ZB + 1.0, ohne=("unten",))
    k.kasten("Innen", (mx - 0.94, my - 0.44, ZB + 1.0), (mx - 0.06, my + 0.44, ZB + 1.03), "leinen", ohne=("-z",))
    D.block(k, "Innen", "werkstein", (mx - 0.75, my - 0.25), (1, 0), (0, 1), 0.5, 0.5, ZB + 1.03, ZB + 1.5, ohne=("unten",))
    fc = (mx - 0.5, my, 0)
    k.dreh("Innen", fc, [(0.26, ZB + 1.5), (0.20, ZB + 2.1), (0.17, ZB + 2.55), (0.09, ZB + 2.72)], "kleinteile", seiten=8, u_bereich=D.feld("blau", True), deckel=False)
    k.dreh("Innen", fc, [(0.0, ZB + 2.70), (0.10, ZB + 2.76), (0.11, ZB + 2.88), (0.07, ZB + 2.98), (0.0, ZB + 3.0)], "kleinteile", seiten=8, u_bereich=D.feld("haut", True), deckel=False)
    D.farbkasten(k, "Innen", (mx - 0.62, my - 0.12, ZB + 2.98), (mx - 0.38, my + 0.12, ZB + 3.03), "gold")
    for dy in (-0.3, 0.3):
        kerze(k, mx - 0.25, my + dy, ZB + 1.03, hoehe=0.3)
    # Fahnen an den beiden vorderen Pfeilerpaaren (zum Mittelschiff)
    rp = XP - M.PFEILER_R
    for sx in (-1, 1):
        for y in M.PFEILER_Y[:2]:
            x0, x1 = sx * (rp - 0.16), sx * (rp - 0.95)
            D.farbstab(k, "Innen", (sx * (rp + 0.04), y, 6.25), (sx * (rp - 1.05), y, 6.25), 0.035, "eisen")
            for seite in (-1, 1):
                k.rechteck("Innen", (x0, y + seite * 0.004, 4.0), (x1 - x0, 0, 0), (0, 0, 1), 0.79, 2.2, "stoff", laengs="v", zu=((x0 + x1) / 2, y + seite * 6, 5))
                D.farbflaeche(k, "Innen", [V((x0, y + seite * 0.008, 4.0)), V((x1, y + seite * 0.008, 4.0)), V((x1, y + seite * 0.008, 4.12)), V((x0, y + seite * 0.008, 4.12))], "gold", zu=(0, y + seite * 6, 4.0))
    return k


def weihrauch():
    w = D.Teil("Weihrauch", BI, saat=73)
    x, y, z = M.WEIHRAUCH
    oben = ZK + 3.3                                        # knapp unter dem Gewoelbe des Chorjochs
    D.farbstab(w, "Innen", (x, y, z + 0.3), (x, y, oben), 0.02, "eisen")
    w.dreh("Innen", (x, y, 0), [(0.0, z + 0.10), (0.07, z + 0.14), (0.09, z + 0.21), (0.06, z + 0.27), (0.02, z + 0.32)], "kleinteile", seiten=8, u_bereich=D.feld("gold", True), deckel=False)
    return w, (x, y, oben)


def evangelisch():
    e = D.Teil("Einrichtung_Evangelisch", BI, saat=75)
    x, y = M.ABENDMAHLSTISCH
    B.tisch(e, "Innen", "holz", x - 1.10, x + 1.10, y - 0.45, y + 0.45, hoehe=0.92, z0=ZB)
    e.kasten("Innen", (x - 1.16, y - 0.50, ZB + 0.92), (x + 1.16, y + 0.50, ZB + 0.95), "leinen", laengs="x", ohne=("-z",))
    D.farbkasten(e, "Innen", (x + 0.10, y - 0.17, ZB + 0.95), (x + 0.60, y + 0.17, ZB + 1.03), "tinte", ohne=("-z",))            # Bibel
    D.farbkasten(e, "Innen", (x + 0.12, y - 0.15, ZB + 1.03), (x + 0.58, y + 0.15, ZB + 1.045), "pergament", ohne=("-z",))
    for dx in (-0.8, -0.5):
        D.farbkasten(e, "Innen", (x + dx - 0.04, y - 0.04, ZB + 0.95), (x + dx + 0.04, y + 0.04, ZB + 1.10), "messing", ohne=("-z",))
        kerze(e, x + dx, y, ZB + 1.10)
    return e


def kreuz():
    k = D.Teil("Kreuz_Evangelisch", BI, saat=77)
    x, y = M.KREUZ
    za = M.Z_ALTAR
    k.stab("Innen", (x, y, za), (x, y, za + 4.4), 0.14, "holz", breite=0.20, oben=(0, 1, 0), kappen=True)
    for seite in (-1, 1):                                  # Arme einzeln (ein durchgehender Balken laege im Stamm: schwarzer Fleck)
        k.stab("Innen", (x + seite * 0.10, y, za + 3.4), (x + seite * 0.85, y, za + 3.4), 0.14, "holz", breite=0.20, oben=(0, 1, 0), kappen=True)
    return k


def simultan():
    s = D.Teil("Einrichtung_Simultan", BI, saat=79)
    y0, y1 = M.SCHRANKE_Y
    n = 6
    for i in range(n):
        y = y0 + (y1 - y0) * i / (n - 1)
        s.kasten("Innen", (-0.05, y - 0.05, ZB), (0.05, y + 0.05, ZB + 0.95), "holz", ohne=("-z",))
    s.stab("Innen", (0, y0, ZB + 0.85), (0, y1, ZB + 0.85), 0.05, "stoff")
    return s


def ursprung(obj, punkt):
    """Setzt den Ursprung eines Objekts auf `punkt` (die Ecken bleiben in der Welt, wo sie sind)."""
    p = V(punkt)
    for v in obj.data.vertices: v.co -= p
    obj.location = p


def anker():
    k = [
        B.anker("Blick_Auge", M.BLICK_AUGE),
        B.anker("Blick_Ziel", M.BLICK_ZIEL),
        B.anker("Tuer_Dom_Stadt", (0.0, Y0 + 0.02, ZB + 1.35), ziel="stadt", breite=M.PORTAL[0], hoehe=M.PORTAL[2] - M.PORTAL[1], achse="y"),
        B.anker("Licht_Raum_1", (0.0, Y0 + 4.2, 7.6), art="Raum"),
        B.anker("Licht_Raum_2", (0.0, Y0 + 12.8, 7.6), art="Raum"),
        B.anker("Licht_Raum_3", (0.0, M.Y_APSIS + 0.5, 6.4), art="Raum"),
        B.anker("Predigt_1", (M.KANZEL[0], M.KANZEL[1], ZB + M.KANZEL[2]), grad=math.degrees(math.atan2(-M.KANZEL[0], -6.6))),
        B.anker("Anker_Bischof", M.PLATZ_BISCHOF, grad=180),
        B.anker("Anker_Pfarrer", M.PLATZ_PFARRER, grad=180),
    ]
    for i, (x, y, z) in enumerate(LEUCHTER):
        k.append(B.anker(f"Licht_Kerze_{i + 1}", (x, y, z + 1.85), art="Kerze"))
        k.append(B.anker(f"Feuer_Kerze_{i + 1}", (x, y, z + 1.78)))
    for i, (x, y, z) in enumerate(ALTARKERZEN):
        k.append(B.anker(f"Feuer_Altarkerze_{i + 1}", (x, y, z + 0.57)))
    for nr, x, y, links in M.sitzplaetze():
        k.append(B.anker(f"Glaeubig_{nr}", (x, y, ZB), grad=0, sitzhoehe=M.SITZHOEHE, links=1 if links else 0))
    return k


def main():
    opt = B.optionen()
    D.neu()
    stoffe = D.stoffe_innen()
    w, haken = weihrauch()
    teile = [raum(), glas(), moebel(), katholisch(), w, evangelisch(), kreuz(), simultan()]
    print("DREIECKE innen:", {t.name: t.dreiecke() for t in teile}, "Summe", sum(t.dreiecke() for t in teile))
    objekte = [t.objekt(stoffe, mit_farbe=False) for t in teile]
    rest = [o for o in objekte[1:] if not o.name.startswith("Glas_")]
    B.uv2_und_ao([([objekte[0]], []), (rest, [objekte[0]])], "dom_innen", MODELL, mit_ao="ohne-ao" not in opt, proben=32)
    ursprung(bpy.data.objects["Weihrauch"], haken)
    B.exportiere(objekte + anker(), os.path.join(B.ZIEL_MODELL, MODELL + "_innen.glb"))
    if "bilder" in opt:
        bilder(opt["bilder"])


def bilder(o):
    os.makedirs(o, exist_ok=True)
    for i, (p, energie) in enumerate((((0, Y0 + 4.2, 8), 9000), ((0, Y0 + 12.8, 8), 9000), ((0, M.Y_APSIS + 0.5, 6.4), 5000), ((-5.4, Y0 + 8.5, 5), 3000), ((5.4, Y0 + 8.5, 5), 3000),
                                      ((-5.4, Y1 - 2, 5), 2000), ((5.4, Y1 - 2.2, 6.5), 2000))):
        l = bpy.data.objects.new(f"Kontrolllicht{i}", bpy.data.lights.new(f"Kontrolllicht{i}", "POINT"))
        l.data.energy = energie * 0.2; l.data.color = (1.0, 0.86, 0.68); l.data.shadow_soft_size = 0.6; l.location = p
        bpy.context.scene.collection.objects.link(l)
    brenn = 18.0 / math.tan(math.radians(66) / 2)
    ev = ("Einrichtung_Evangelisch", "Kreuz_Evangelisch"); ka = ("Einrichtung_Katholisch", "Weihrauch"); si = ("Einrichtung_Simultan",)
    B.kontrollbild(os.path.join(o, "domev_innen_blick_evangelisch.jpg"), M.BLICK_AUGE, M.BLICK_ZIEL, brennweite=brenn, verbergen=ka + si, proben=32)
    B.kontrollbild(os.path.join(o, "domev_innen_blick_katholisch.jpg"), M.BLICK_AUGE, M.BLICK_ZIEL, brennweite=brenn, verbergen=ev + si, proben=32)
    B.kontrollbild(os.path.join(o, "domev_innen_orgel_kanzel.jpg"), (-5.6, Y0 + 9.0, ZB + 1.7), (3.5, Y1 - 1.0, 3.6), brennweite=brenn, verbergen=ka + si, proben=32)
    B.kontrollbild(os.path.join(o, "domev_innen_rueckblick.jpg"), (0.0, Y1 - 2.6, ZB + 1.7), (0.0, Y0, 5.0), brennweite=brenn, verbergen=ka + si, proben=32)


if __name__ == "__main__":
    main()
