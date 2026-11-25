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
ZB, ZK, ZS, ZO, ZSC = M.Z_SOCKEL, M.Z_KAEMPFER, M.Z_SCHEITEL_L, M.Z_OKT_KAEMPFER, M.Z_SCHEITEL
XS = M.X_SEITE
Y0, Y1 = M.Y_INNEN0, M.Y_INNEN1
CX, CY = M.OKT_M
AI = M.OKT_A_INNEN
PUTZ = "gewoelbe"


def innen_drin(x, y):
    """Liegt der Punkt im Innenraum (Langhaus bis zur Stirnwand oder Achteck)?"""
    if abs(x) < XS and Y0 < y < Y1: return True
    for _, _, n in M.vieleck(CX, CY, AI, 8, 0.0):
        if (x - CX) * n[0] + (y - CY) * n[1] > AI: return False
    return True


def wandbereiche(name):
    """Teile der Innenflaeche einer Wand (s ab dem Anfang der Aussenflaeche), an denen wirklich Wand steht: innen Raum, aussen Mauer."""
    (ox, oy), (ex, ey), l, (nx, ny), d = M.wand(name)
    qx, qy = ox - nx * d, oy - ny * d
    wand_da = lambda s: innen_drin(qx + ex * s - nx * 0.05, qy + ey * s - ny * 0.05) and not innen_drin(qx + ex * s + nx * 0.05, qy + ey * s + ny * 0.05)
    schritt = 0.05
    n_ = int((l + 3.0) / schritt)
    ss = [-1.5 + i * schritt for i in range(n_ + 1)]
    aus, start = [], None
    for i, s_ in enumerate(ss):
        da = wand_da(s_)
        if da and start is None:
            start = s_ if i == 0 else _bisekt(wand_da, ss[i - 1], s_)
        if not da and start is not None:
            aus.append((start, _bisekt(wand_da, s_, ss[i - 1]))); start = None
    if start is not None: aus.append((start, ss[-1]))
    return [(a, b) for a, b in aus if b - a > 0.05]


def _bisekt(f, ohne, mit):
    """Grenze zwischen einem Punkt ohne Wand (`ohne`) und einem mit Wand (`mit`) auf 1 mm."""
    for _ in range(20):
        m = (ohne + mit) / 2
        if f(m): mit = m
        else: ohne = m
    return mit


def raum():
    r = D.Teil("Raum_Dom", BI, saat=51)
    cs0, cs1 = M.CHORSTUFEN_Y
    xb, xf = M.X_BOGEN, M.X_FLAECHE
    t = M.T_WAND
    # ── Boeden: Schachbrett im Langhaus, drei Stufen zum Achteck, dort Sandsteinplatten, zwei Stufen zum Podest des Hochaltars ──
    D.schachboden(r, -XS, XS, Y0, cs0, ZB)
    for sx in (-1, 1):
        D.schachboden(r, min(sx * xb, sx * XS), max(sx * xb, sx * XS), cs0, Y1, ZB)
    B.gerade_treppe(r, "Innen", "sandstein", (0, cs0), (0, 1, 0), (1, 0, 0), 2 * xb, 3, 0.15, 0.30, ZB, block=True)
    v0, v1, mm = BI["sandstein"]
    mitte = V((CX, CY, M.Z_CHOR))
    ecken = [V((*a, M.Z_CHOR)) for a, _, _ in M.vieleck(CX, CY, AI + 0.05, 8, 0.0)]
    for i in range(8):
        r.vieleck("Innen", [mitte, ecken[i], ecken[(i + 1) % 8]], [(0, v1), (1, v1), (1, v0)], zu=mitte + EZ * 5)
    ay0, ay1 = M.ALTARSTUFEN_Y
    B.gerade_treppe(r, "Innen", "sandstein", (0, ay0), (0, 1, 0), (1, 0, 0), 9.0, 2, 0.15, 0.30, M.Z_CHOR, block=True)
    pm = V((0, M.HOCHALTAR[1], M.Z_ALTAR))
    rand = [V((-4.5, ay1, M.Z_ALTAR)), V((4.5, ay1, M.Z_ALTAR)), V((2.35, CY + AI - 0.1, M.Z_ALTAR)), V((-2.35, CY + AI - 0.1, M.Z_ALTAR))]
    for i in range(4):
        r.vieleck("Innen", [pm, rand[i], rand[(i + 1) % 4]], [(0, v1), (1, v1), (1, v0)], zu=pm + EZ * 5)
    # ── Waende: heller Putz, Laibungen und Masswerk aus Werkstein; Stuecke nach der Lage von Raum und Mauer ──
    for name in ("west", "seite_r", "seite_l") + tuple(f"okt_{k}" for k in range(8)):
        (ox, oy), e, l, (nx, ny), d = M.wand(name)
        ls = [D.loch_von(o) for o in M.OEFFNUNGEN if o[0] == name]
        zo = ZS if name in ("west", "seite_r", "seite_l") else ZO
        for sa, sb in wandbereiche(name):
            B.mauer(r, "Innen", (ox - nx * d, oy - ny * d), e, (-nx, -ny), l, ZB, zo, PUTZ, ls, laibung=("Innen", "werkstein"), dicke=d / 2, zbasis=ZB, s_bereich=(sa, sb))
    # Stirnwand zwischen Langhaus und Achteck: der Bogen (Kaempfer unter dem Fuss des Zeltgewoelbes)
    bogen = lambda a: D.Spitzloch(a - xb, a + xb, ZB, ZK, 1.2, sohlbank=False)
    D.flaeche(r, (XS, Y1), (-XS, Y1), (0, -1), ZB, ZS, band=PUTZ, loecher_=[bogen(XS)], dicke=t)
    D.flaeche(r, (-xf, Y1), (xf, Y1), (0, 1), ZB, ZO, band=PUTZ, loecher_=[bogen(xf)])
    # Masswerk von innen (vor der Scheibe), wie aussen
    for o in M.OEFFNUNGEN:
        if o[5] != "spitz": continue
        name = o[0]; d = M.wand(name)[4]; nn = M.wand(name)[3]
        D.masswerk(r, lambda s_, z, name=name, d=d: V(M.punkt(name, s_, z, -d / 2 - 0.07)), V((-nn[0], -nn[1], 0)), D.loch_von(o), stoff="Innen")
    # ── Gewoelbe: Kreuzgewoelbe ueber dem Langhaus (Gurtboegen zwischen den Jochen), Zeltgewoelbe ueber dem Achteck ──
    joch = (Y1 - Y0) / M.JOCHE
    for i in range(M.JOCHE):
        ya, yb = Y0 + i * joch, Y0 + (i + 1) * joch
        D.kreuzgewoelbe(r, -XS, XS, ya, yb, ZK, ZS, gurt=("y1",) if i < M.JOCHE - 1 else ())
    c = V((CX, CY, 0))
    ecke = [V((*a, 0)) for a, _, _ in M.vieleck(CX, CY, AI, 8, 0.0)]
    teile = 5
    ring = lambda j, k: c + (ecke[k % 8] - c) * math.cos(math.pi / 2 * j / teile) + EZ * (ZO + (ZSC - ZO) * math.sin(math.pi / 2 * j / teile))
    for j in range(teile):
        for k in range(8):
            pts = [ring(j, k), ring(j, k + 1), ring(j + 1, k + 1), ring(j + 1, k)]
            D._kappe(r, pts, c + EZ * (ZO - 5))
        for k in range(8):
            D.rippe(r, ring(j, k), ring(j + 1, k))
    for k in range(8):
        D.rippe(r, ring(0, k), ring(0, k + 1))
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


LEUCHTER = [(-3.2, M.CHORSTUFEN_Y[0] - 0.5, ZB), (3.2, M.CHORSTUFEN_Y[0] - 0.5, ZB), (-2.9, 5.6, M.Z_CHOR), (2.9, 5.6, M.Z_CHOR),
            (-3.2, Y0 + 0.6, ZB), (3.2, Y0 + 0.6, ZB)]
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
        w = sx * (AI - 0.06)
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
        B.bretttuer(m, "Innen", "holz", "Innen", "kleinteile", (M.PORTAL_X + seite * b_ / 4, M.Y_WEST + 0.32, z0), (1, 0, 0), (0, 1, 0), b_ / 2 - 0.01, z1 - z0, bretter=4, eisen_ub=D.feld("eisen", True))
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
    rp = XS
    for sx in (-1, 1):
        for y in M.FAHNEN_Y:
            x0, x1 = sx * (rp - 0.16), sx * (rp - 0.95)
            D.farbstab(k, "Innen", (sx * (rp + 0.04), y, 6.25), (sx * (rp - 1.05), y, 6.25), 0.035, "eisen")
            for seite in (-1, 1):
                k.rechteck("Innen", (x0, y + seite * 0.004, 4.0), (x1 - x0, 0, 0), (0, 0, 1), 0.79, 2.2, "stoff", laengs="v", zu=((x0 + x1) / 2, y + seite * 6, 5))
                D.farbflaeche(k, "Innen", [V((x0, y + seite * 0.008, 4.0)), V((x1, y + seite * 0.008, 4.0)), V((x1, y + seite * 0.008, 4.12)), V((x0, y + seite * 0.008, 4.12))], "gold", zu=(0, y + seite * 6, 4.0))
    return k


def weihrauch():
    w = D.Teil("Weihrauch", BI, saat=73)
    x, y, z = M.WEIHRAUCH
    oben = M.WEIHRAUCH_OBEN                                # knapp unter dem Zeltgewoelbe
    D.farbstab(w, "Innen", (x, y, z + 0.3), (x, y, oben), 0.02, "eisen")
    w.dreh("Innen", (x, y, 0), [(0.0, z + 0.10), (0.07, z + 0.14), (0.09, z + 0.21), (0.06, z + 0.27), (0.02, z + 0.32)], "kleinteile", seiten=8, u_bereich=D.feld("gold", True), deckel=False)
    return w, (x, y, oben)


def evangelisch():
    e = D.Teil("Einrichtung_Evangelisch", BI, saat=75)
    x, y = M.ABENDMAHLSTISCH
    B.tisch(e, "Innen", "holz", x - 1.10, x + 1.10, y - 0.45, y + 0.45, hoehe=0.92, z0=M.Z_CHOR)
    e.kasten("Innen", (x - 1.16, y - 0.50, M.Z_CHOR + 0.92), (x + 1.16, y + 0.50, M.Z_CHOR + 0.95), "leinen", laengs="x", ohne=("-z",))
    D.farbkasten(e, "Innen", (x + 0.10, y - 0.17, M.Z_CHOR + 0.95), (x + 0.60, y + 0.17, M.Z_CHOR + 1.03), "tinte", ohne=("-z",))            # Bibel
    D.farbkasten(e, "Innen", (x + 0.12, y - 0.15, M.Z_CHOR + 1.03), (x + 0.58, y + 0.15, M.Z_CHOR + 1.045), "pergament", ohne=("-z",))
    for dx in (-0.8, -0.5):
        D.farbkasten(e, "Innen", (x + dx - 0.04, y - 0.04, M.Z_CHOR + 0.95), (x + dx + 0.04, y + 0.04, M.Z_CHOR + 1.10), "messing", ohne=("-z",))
        kerze(e, x + dx, y, M.Z_CHOR + 1.10)
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
        B.anker("Tuer_Dom_Stadt", (M.PORTAL_X, Y0 + 0.02, ZB + 1.35), ziel="stadt", breite=M.PORTAL[0], hoehe=M.PORTAL[2] - M.PORTAL[1], achse="y"),
        B.anker("Licht_Raum_1", (0.0, Y0 + 3.0, 7.6), art="Raum"),
        B.anker("Licht_Raum_2", (0.0, Y0 + 8.2, 7.6), art="Raum"),
        B.anker("Licht_Raum_3", (0.0, CY + 1.0, 9.0), art="Raum"),
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
    for i, (p, energie) in enumerate((((0, Y0 + 3.0, 8), 7000), ((0, Y0 + 8.2, 8), 7000), ((0, CY + 1.0, 10.5), 9000), ((-3.6, Y0 + 6.0, 5), 2500), ((3.6, Y0 + 6.0, 5), 2500),
                                      ((-4.0, CY + 3.0, 6), 2500), ((4.0, CY + 3.0, 6), 2500))):
        l = bpy.data.objects.new(f"Kontrolllicht{i}", bpy.data.lights.new(f"Kontrolllicht{i}", "POINT"))
        l.data.energy = energie * 0.2; l.data.color = (1.0, 0.86, 0.68); l.data.shadow_soft_size = 0.6; l.location = p
        bpy.context.scene.collection.objects.link(l)
    brenn = 18.0 / math.tan(math.radians(66) / 2)
    ev = ("Einrichtung_Evangelisch", "Kreuz_Evangelisch"); ka = ("Einrichtung_Katholisch", "Weihrauch"); si = ("Einrichtung_Simultan",)
    B.kontrollbild(os.path.join(o, "domev_innen_blick_evangelisch.jpg"), M.BLICK_AUGE, M.BLICK_ZIEL, brennweite=brenn, verbergen=ka + si, proben=32)
    B.kontrollbild(os.path.join(o, "domev_innen_blick_katholisch.jpg"), M.BLICK_AUGE, M.BLICK_ZIEL, brennweite=brenn, verbergen=ev + si, proben=32)
    B.kontrollbild(os.path.join(o, "domev_innen_orgel_kanzel.jpg"), (-3.4, Y0 + 3.0, ZB + 1.7), (3.5, -6.0, 3.6), brennweite=brenn, verbergen=ka + si, proben=32)
    B.kontrollbild(os.path.join(o, "domev_innen_altar.jpg"), (0.0, Y1 + 0.2, M.Z_CHOR + 1.7), (0.0, 9.0, 4.0), brennweite=brenn, verbergen=ka + si, proben=32)
    B.kontrollbild(os.path.join(o, "domev_innen_rueckblick.jpg"), (0.0, Y1 + 3.5, M.Z_CHOR + 1.7), (0.0, Y0, 5.0), brennweite=brenn, verbergen=ka + si, proben=32)


if __name__ == "__main__":
    main()
