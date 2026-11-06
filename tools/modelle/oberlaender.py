"""Rheinschiff „Oberländer“ (Neuanfang, ersetzt den Lastkahn aus hafenbauten.py).

    blender -b --python tools/modelle/oberlaender.py -- [--schnell] [--ohne-bilder] [--seite]

Schreibt game/assets/modelle/wasser/wasser_schiff.glb (gleicher Name wie der alte Kahn, das Spiel lädt ihn unverändert) und
das Vorschaubild docs/modelle/wasser_schiff.png.

Oberländer: Frachtschiff der Rheinschiffer oberhalb von Köln um 1400, 22 m × 4,4 m (Maße plausibel, nicht belegt),
fast kiellos mit flachem Boden, Rumpf aus überlappenden Planken (Klinker), schmaler, hochgezogener Bug, hinten ein
Spiegel mit kleinem Achterdeck und einer Hütte (Satteldach), ein Mast mit einem Rahsegel, Heckruder mit Pinne.
X ist die Längsachse, der Bug liegt bei +X, die Seite zum Kai bei −Y (im Spiel +Z). Ursprung auf der Wasserlinie in der Mitte.

    Schiff         Rumpf (Klinkerplanken, Dollbord in Steghöhe), Boden, Spanten, Achterdeck, Hütte, Mastfuß, Ruder, Pinne.
    Mast           Mast mit Rah (ohne Fuß); Drehpunkt (2,9; 0; Dollbord + 0,25) – im Hafen am Kran legt das Spiel ihn nach vorn.   Takelage  Wanten, Stagen, Webeleinen.
    Segel_Gesetzt  das Rahsegel, voll (Wind von achtern) – zur Fahrt;   Segel_Gerefft  dasselbe an der Rah aufgetucht – im Hafen.
    Laufplanke     Ursprung am Dollbord mittschiffs (Kaiseite), führt 2,6 m nach −Y und fällt 0,16 m (auf das Deck des Stegs).
    Ladung_1–18    Einzelstücke (Fass, Kiste, Sack, Tuchballen) im Raum; das Spiel blendet so viele ein, wie geladen ist.
                   Ausgeblendet wird von hinten nach vorn in der Nummer: erst die oben gestapelten (18–15), dann die unteren
                   (14 … 1), die Mittschiffs (bei der Laufplanke) zuerst, die an den Enden zuletzt.
    Fahne          Wimpel in der Hausfarbe am Masttopp.
    Anker_Bug (Blick +X), Anker_Heck (Blick −X), Anker_Kai (Wasserlinie, 0,3 m vor der Bordwand mittschiffs zum Kai),
    Anker_Planke (Ende der Laufplanke), Anker_Schiffer (auf dem Achterdeck an der Pinne, Blick +X).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import hafenbauten as hb  # noqa: E402  (Wolle, BEHALTEN, beide_seiten, Vorschau-Bühne)
import landbau as lb  # noqa: E402
import wasser as ws  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

LAENGE, BREITE = 22.0, 4.4
HALB = LAENGE / 2
BORD = 0.85             # Dollbord mittschiffs über der Wasserlinie (Steghöhe)
TIEF = 0.5              # Tiefgang, flacher Boden
BODEN = 0.22            # Wegerung im Raum über der Wasserlinie: kein Wasser im Rumpf
DECK = 0.95             # Achterdeck
GAENGE = 8              # Plankengänge je Seite
DICKE = 0.045           # Überlappung der Klinker
MAST = 2.9              # Mast (x)
MAST_HOCH = 11.0        # über dem Dollbord
RAH = 9.0               # Höhe der Rah über dem Dollbord
PLANKE = 2.6
HUETTE = (-9.4, -6.4)   # x-Bereich der Hütte
# Lage der Einbauten im Rumpf; der Niederländer (niederlaender.py) setzt hier seine eigenen Werte
SCHOTTEN = (-6.0, 7.8)                                  # Stirnwände des Raums
SPANTEN = [-5.4 + 1.5 * i for i in range(9)]
DUCHTEN = (-5.9, 0.3, 7.6)
FUGEN = range(-6, 8)                                    # Quernähte der Wegerung
SCHIFFER_X = None                                       # x des Schiffers (None: 1,1 m vor dem Heck)


def glatt(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def profil(x):
    """Querschnitt an der Stelle x: (halbe Breite am Dollbord, halbe Breite am Boden, Höhe Boden, Höhe Dollbord)."""
    hinten = max(0.0, (-6.0 - x) / 5.0)                    # 0 … 1 zum Spiegel
    vorn = max(0.0, (x - 3.0) / 8.0)                       # 0 … 1 zum Bug
    w = BREITE / 2
    if hinten > 0:
        w = w - (w - 1.35) * hinten ** 2
    if vorn > 0:
        w = 0.12 + (BREITE / 2 - 0.12) * (1 - vorn ** 1.8)
    b = w * (0.80 - 0.42 * vorn ** 1.2)
    zb = -TIEF + 2.2 * (max(0.0, (x - 2.0) / 9.0)) ** 2.2 + 0.4 * hinten ** 2
    g = BORD + 2.0 * (max(0.0, (x - 1.0) / 10.0)) ** 2.4 + 0.45 * hinten ** 1.5
    return w, b, zb, g


def seite(x, u):
    """Punkt der Seitenlinie (y > 0) beim Anteil u (0 = Kimm am Boden, 1 = Dollbord)."""
    w, b, zb, g = profil(x)
    return b + (w - b) * u, zb + (g - zb) * u


def flur(x):
    """Höhe des Bodens im Raum und halbe Breite dort (innen)."""
    w, b, zb, g = profil(x)
    fz = max(BODEN, zb + 0.06)
    u = min(1.0, (fz - zb) / max(1e-6, g - zb))
    return fz, max(0.05, b + (w - b) * u - 0.07)


def innen_breite(x, z):
    w, b, zb, g = profil(x)
    u = min(1.0, max(0.0, (z - zb) / max(1e-6, g - zb)))
    return max(0.05, b + (w - b) * u - 0.07)


def stationen():
    xs, x = [], -HALB
    while x < HALB - 1e-6:
        xs.append(round(x, 3))
        x += 0.4 if abs(x) > 3 else 0.6
    xs.append(HALB)
    for extra in HUETTE:
        if extra not in xs:
            xs.append(extra)
    return sorted(xs)


_MASTTEILE = []


def rumpf(st):
    netz = bs.Netz("Schiff")
    xs = stationen()
    mitte = Vector((0, 0, BORD))
    stoffe = ["Holz alt", "Holz"]
    for xa, xb in zip(xs, xs[1:]):
        for s in (-1, 1):
            for k in range(GAENGE):
                u0, u1 = k / GAENGE, (k + 1) / GAENGE
                um = u0 + (u1 - u0) * 0.78                    # unter dem nächsten Gang liegt der Schatten
                pa0, pb0 = seite(xa, u0), seite(xb, u0)
                pam, pbm = seite(xa, um), seite(xb, um)
                pa1, pb1 = seite(xa, u1), seite(xb, u1)
                # Der Gang ragt unten um DICKE nach außen (Klinker), oben läuft er in den Boden des nächsten Gangs
                versatz = lambda f: DICKE * (1 - f)  # noqa: E731
                unten_a = Vector((xa, s * (pa0[0] + versatz(0)), pa0[1]))
                unten_b = Vector((xb, s * (pb0[0] + versatz(0)), pb0[1]))
                mitte_a = Vector((xa, s * (pam[0] + versatz(0.78)), pam[1]))
                mitte_b = Vector((xb, s * (pbm[0] + versatz(0.78)), pbm[1]))
                oben_a = Vector((xa, s * pa1[0], pa1[1]))
                oben_b = Vector((xb, s * pb1[0], pb1[1]))
                richtung = (unten_a + unten_b + oben_a + oben_b) / 4 - mitte
                netz.vieleck_nach([unten_a, unten_b, mitte_b, mitte_a], richtung, st[stoffe[(k + (xs.index(xa) // 6)) % 2]])
                netz.vieleck_nach([mitte_a, mitte_b, oben_b, oben_a], richtung, st["Teerholz"] if k < GAENGE - 1 else st[stoffe[0]])
            # Innenseite vom Boden des Raums bis zum Dollbord
            (fza, fa), (fzb, fb) = flur(xa), flur(xb)
            wa, wb = profil(xa)[0], profil(xb)[0]
            ga, gb = profil(xa)[3], profil(xb)[3]
            if ga - fza > 0.05 and gb - fzb > 0.05:
                for k in range(5):
                    t0, t1 = k / 5, (k + 1) / 5
                    pa = lambda t: (xa, s * (fa + (wa - 0.09 - fa) * t), fza + (ga - 0.02 - fza) * t)  # noqa: E731
                    pb = lambda t: (xb, s * (fb + (wb - 0.09 - fb) * t), fzb + (gb - 0.02 - fzb) * t)  # noqa: E731
                    netz.vieleck_nach([pa(t0), pb(t0), pb(t1), pa(t1)], (0, -s, 0.3), st[stoffe[(k + 1) % 2]])
            # Dollbord: breite Leiste oben
            netz.vieleck_nach([(xa, s * (wa + 0.03), ga + 0.025), (xb, s * (wb + 0.03), gb + 0.025),
                               (xb, s * (wb - 0.10), gb + 0.025), (xa, s * (wa - 0.10), ga + 0.025)], (0, 0, 1), st["Holz alt"])
        # Boden (Wegerung) im Raum; vorn und hinten steigt er mit dem Rumpf
        (fza, fa), (fzb, fb) = flur(xa), flur(xb)
        if fa > 0.06 and fb > 0.06:
            n = 9
            for k in range(n):
                t0, t1 = -1 + 2 * k / n + 0.012, -1 + 2 * (k + 1) / n - 0.012
                netz.vieleck_nach([(xa, fa * t0, fza), (xb, fb * t0, fzb), (xb, fb * t1, fzb), (xa, fa * t1, fza)], (0, 0, 1),
                                  st["Bretter"] if (k + xs.index(xa) // 5) % 2 else st["Holz"])
    # Bodenfugen und Quernähte der Wegerung
    for x in FUGEN:
        fz, f = flur(x + 0.0)
        ws.linie(netz, st, (x, -f, fz), (x, f, fz), (0, 0, 1), "Fuge", breite=0.03, abstand=0.004)
    # Achterdeck über dem hinteren Ende (hinter der Hütte), Spiegel, Vordersteven
    xs_deck = [x for x in xs if x <= HUETTE[0]]
    for xa, xb in zip(xs_deck, xs_deck[1:]):
        ya, yb = innen_breite(xa, DECK), innen_breite(xb, DECK)
        netz.vieleck_nach([(xa, -ya, DECK), (xb, -yb, DECK), (xb, yb, DECK), (xa, ya, DECK)], (0, 0, 1), st["Bretter"])
    for y in (-0.9, -0.3, 0.3, 0.9):
        ws.linie(netz, st, (-HALB + 0.1, y, DECK), (HUETTE[0], y, DECK), (0, 0, 1), "Fuge", breite=0.03, abstand=0.004)
    w, b, zb, g = profil(-HALB)
    for k in range(GAENGE):                                       # Spiegel: waagerechte Bretter, leicht schräg nach hinten
        z0, z1 = zb + (g - zb) * k / GAENGE, zb + (g - zb) * (k + 1) / GAENGE
        y0, y1 = b + (w - b) * k / GAENGE, b + (w - b) * (k + 1) / GAENGE
        netz.vieleck_nach([(-HALB - 0.02, -y0, z0), (-HALB - 0.02, y0, z0), (-HALB - 0.02, y1, z1), (-HALB - 0.02, -y1, z1)], (-1, 0, 0),
                          st[stoffe[k % 2]])
    # Schotten (Stirnwand des Raums) vorn und hinten
    for x in SCHOTTEN:
        fz, f = flur(x)
        w_, _, _, g_ = profil(x)
        netz.vieleck_nach([(x, -f, fz), (x, f, fz), (x, w_ - 0.09, g_ - 0.02), (x, -(w_ - 0.09), g_ - 0.02)], (-x, 0, 0), st["Holz alt"])
    # Vordersteven: kräftiger Balken von der Bodenkante über den Bug, vorn hochgezogen; Hintersteven (Heck) niedriger
    wb_, bb_, zb_, gb_ = profil(HALB)
    netz.stab((HALB - 0.1, 0, zb_), (HALB + 0.12, 0, gb_ + 0.25), 0.26, 0.2, st["Holz alt"], quer=(0, 1, 0))
    netz.stab((HALB + 0.12, 0, gb_ + 0.25), (HALB + 0.2, 0, gb_ + 0.62), 0.22, 0.18, st["Holz alt"], quer=(0, 1, 0))
    netz.stab((-HALB + 0.05, -1.3, g - 0.05), (-HALB + 0.05, -1.3, g + 0.35), 0.16, 0.16, st["Holz alt"])
    netz.stab((-HALB + 0.05, 1.3, g - 0.05), (-HALB + 0.05, 1.3, g + 0.35), 0.16, 0.16, st["Holz alt"])
    # Spanten und Duchten
    for x in SPANTEN:
        fz, f = flur(x)
        w_, _, _, g_ = profil(x)
        for s in (-1, 1):
            netz.stab((x, s * (f + 0.01), fz), (x, s * (w_ - 0.12), g_ - 0.05), 0.12, 0.08, st["Holz alt"], quer=(1, 0, 0))
    for x in DUCHTEN:
        w_, _, _, g_ = profil(x)
        netz.stab((x, -(w_ - 0.1), g_ - 0.14), (x, w_ - 0.1, g_ - 0.14), 0.3, 0.1, st["Holz"], quer=(0, 0, 1))
    # Mast, Rah, Wanten, Stagen: Mast (mit Rah) und Takelage sind eigene Knoten, damit das Spiel im Hafen den Mast legen kann (Kran, Stadtansicht.Kranladen.cs);
    # im Rumpf bleibt der Mastfuß mit einem Stumpf des Mastes
    netz.stab((MAST, 0, BODEN), (MAST, 0, BORD + 0.25), 0.28, 0.28, st["Holz"])
    netz.quader((MAST + 0.1, 0, BORD + 0.25), (0.8, 0.6, 0.5), st["Holz alt"], ohne=("-z",))   # Mastfuß-Block
    mast = bs.Netz("Mast")
    mast.stab((MAST, 0, BORD + 0.25), (MAST, 0, BORD + MAST_HOCH), 0.28, 0.28, st["Holz"])
    mast.quader((MAST, 0, BORD + MAST_HOCH - 0.6), (0.38, 0.38, 0.4), st["Holz alt"])
    mast.stab((MAST + 0.2, -4.0, BORD + RAH), (MAST + 0.2, 4.0, BORD + RAH), 0.15, 0.15, st["Holz"], quer=(1, 0, 0))
    tak = bs.Netz("Takelage")
    tak.stab((MAST, 0, BORD + MAST_HOCH - 0.4), (HALB - 0.2, 0, gb_ + 0.55), 0.04, 0.04, st["Seil"])       # Vorstag
    tak.stab((MAST, 0, BORD + MAST_HOCH - 0.4), (-HALB + 0.8, 0, g + 0.2), 0.04, 0.04, st["Seil"])          # Achterstag
    w_, _, _, g_ = profil(MAST)
    for s in (-1, 1):
        for dx in (-0.55, 0.45):
            tak.stab((MAST + dx, s * (w_ - 0.12), g_ + 0.04), (MAST + dx * 0.2, s * 0.14, BORD + MAST_HOCH - 0.5 - abs(dx)), 0.04, 0.04,
                     st["Seil"], quer=(1, 0, 0))
        tak.stab((MAST + 0.2, s * 3.9, BORD + RAH), (MAST - 3.2, s * (w_ - 0.15), g_ + 0.05), 0.035, 0.035, st["Seil"], quer=(1, 0, 0))
        for k in range(1, 9):                                         # Webeleinen zwischen den Wanten
            t = k / 9
            za = g_ + 0.04 + (BORD + MAST_HOCH - 0.5 - 0.55 - g_ - 0.04) * t
            ya = s * (w_ - 0.12) * (1 - t) + s * 0.14 * t
            tak.stab((MAST - 0.55 * (1 - t) - 0.1 * t, ya, za), (MAST + 0.45 * (1 - t) + 0.09 * t, ya, za), 0.03, 0.03, st["Seil"])
    _MASTTEILE[:] = [mast, tak]
    # Hütte achtern: kleines Haus aus Brettern mit Satteldach (Schindeln), Tür nach vorn, zwei Luken
    x0, x1 = HUETTE
    zh = BORD + 0.95
    breit = 1.25
    ws.kasten(netz, st, x0, x1, -breit, breit, BODEN + 0.05, zh, "Holz alt", ohne=("-z", "+z"))
    bs.satteldach(netz, st, x0, x1, -breit, breit, zh, 26, traufe=0.2, ortgang=0.18, enden="Holz alt", **bs.SCHINDEL)
    hoch = breit * math.tan(math.radians(26))
    for x, ausen in ((x0, "links"), (x1, "rechts")):
        bs.platte(netz, bs.wandmatrix((x, 0, zh), ausen), [(-breit, 0), (breit, 0), (0, hoch)], 0, 0.1, st["Holz alt"])
    ws.kasten(netz, st, x1, x1 + 0.03, -0.38, 0.38, BODEN + 0.05, zh - 0.25, "Loch", ohne=("-z",))
    for s in (-1, 1):
        ws.kasten(netz, st, (x0 + x1) / 2 - 0.3, (x0 + x1) / 2 + 0.3, s * breit - 0.015, s * breit + 0.015, BORD + 0.35, BORD + 0.75,
                  "Loch", ohne=("-z",))
    for z in (BORD + 0.15, BORD + 0.55):                         # Latten an der Hütte
        for s in (-1, 1):
            ws.linie(netz, st, (x0, s * breit, z), (x1, s * breit, z), (0, s, 0), "Teerholz", breite=0.04)
    # Heckruder mit Pinne: Blatt hinter dem Spiegel, Schaft hoch über das Achterdeck, Pinne nach vorn
    xr = -HALB - 0.3
    netz.stab((xr, 0, 1.55), (xr, 0, -0.75), 0.12, 0.12, st["Holz"])
    netz.quader((xr - 0.45, 0, 0.05), (1.0, 0.1, 1.6), st["Teerholz"])
    for z in (-0.4, 0.5):
        netz.quader((xr + 0.02, 0, z), (0.5, 0.16, 0.08), st["Eisen"])
    netz.stab((xr, 0.05, 1.62), (HUETTE[0], 0.2, 1.75), 0.08, 0.08, st["Holz"])
    # Anker am Bug, Poller
    netz.stab((HALB - 1.5, 0.75, profil(HALB - 1.5)[3] - 0.2), (HALB - 1.5, 0.75, profil(HALB - 1.5)[3] + 0.15), 0.1, 0.5, st["Eisen"], quer=(1, 0, 0))
    for x in (HALB - 2.4, -HALB + 2.4):
        w_, _, _, g_ = profil(x)
        for s in (-1, 1):
            lb.saeule(netz, st, (x, s * 0.55, 0), 0.1, g_ - 0.03, g_ + 0.35, "Holz", n=6)
    return netz.objekt()


def segel_gesetzt(st):
    netz = bs.Netz("Segel_Gesetzt", alle=True)
    oben, unten, halb = BORD + RAH - 0.1, BORD + 2.5, 3.6
    nu, nv = 4, 3

    def p(i, j):
        u, v = -1 + 2 * i / nu, j / nv
        bauch = 0.9 * (1 - u * u) * math.sin(math.pi * (0.15 + 0.85 * (1 - v)))
        breite = halb * (1 - 0.05 * (1 - v))
        return Vector((MAST + 0.3 + bauch, u * breite, unten + (oben - unten) * v))
    for i in range(nu):
        for j in range(nv):
            hb.beide_seiten(netz, [p(i, j), p(i + 1, j), p(i + 1, j + 1), p(i, j + 1)], (1, 0, 0), st["Segeltuch"])
    for s in (-1, 1):
        netz.stab(p(0 if s < 0 else nu, 0), (MAST - 2.4, s * (BREITE / 2 - 0.15), BORD + 0.1), 0.03, 0.03, st["Seil"], quer=(1, 0, 0))
    for i in range(1, nu):
        for j in range(nv):
            ws.linie(netz, st, p(i, j), p(i, j + 1), (1, 0, 0), "Seil", breite=0.035, abstand=0.012)
    return netz.objekt()


def segel_gerefft(st):
    netz = bs.Netz("Segel_Gerefft")
    z = BORD + RAH - 0.22
    netz.prisma([(0.2 * math.cos(2 * math.pi * k / 6), z + 0.16 * math.sin(2 * math.pi * k / 6)) for k in range(6)],
                -3.6, 3.6, st["Segeltuch"], matrix=Matrix.Translation((MAST + 0.25, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    for y in (-2.6, -0.9, 0.9, 2.6):
        netz.quader((MAST + 0.25, y, z), (0.44, 0.06, 0.36), st["Seil"])
    return netz.objekt()


def laufplanke(st):
    """Ursprung am Dollbord mittschiffs auf der Kaiseite; die Planke liegt nach −Y und fällt 0,16 m."""
    netz = bs.Netz("Laufplanke")
    a, b = Vector((0, 0.15, 0.04)), Vector((0, -PLANKE, -0.12))
    netz.stab(a, b, 0.55, 0.06, st["Bretter"], quer=(0, 0, 1))
    k = 1
    while k * 0.4 < PLANKE:
        p = a.lerp(b, k * 0.4 / (PLANKE + 0.15)) + Vector((0, 0, 0.04))
        netz.quader(p, (0.5, 0.05, 0.03), st["Holz"], ohne=("-z",))
        k += 1
    return netz.objekt()


def wimpel(st):
    netz = bs.Netz("Fahne")
    z0 = BORD + MAST_HOCH
    netz.stab((MAST, 0, z0 - 0.1), (MAST, 0, z0 + 1.3), 0.06, 0.06, st["Holz"])
    netz.quader((MAST, 0, z0 + 1.35), (0.12, 0.12, 0.12), st["Eisen"])
    tuch = [(0, 0), (2.6, 0.02), (2.1, 0.26), (2.6, 0.5), (0, 0.52)]
    netz.prisma(tuch, -0.015, 0.015, st["Hausfarbe"],
                matrix=Matrix.Translation((MAST - 0.04, 0, z0 + 0.7)) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    return netz.objekt()


# ─────────────────────────────────────────────────────────────────────────────
#  Ladung: einzelne Stücke
# ─────────────────────────────────────────────────────────────────────────────

def stueck(netz, st, art, x, y, z, nummer):
    """Ein Ladungsstück mit dem Fuß bei (x, y, z); gibt die Höhe zurück."""
    if art == "fass":
        bs.fass(netz, st, Vector((x, y, z)), r=0.40, h=0.95, reifen=(0.18 + 0.01 * (nummer % 3), 0.72))
        return 0.95
    if art == "kiste":
        lb.kiste(netz, st, (x, y, z), (1.15, 0.85, 0.72), dreh=0.04 * ((nummer % 5) - 2))
        return 0.72
    if art == "sack":                                              # drei Säcke, einer obenauf
        for dx, dy, dz in ((-0.3, -0.2, 0), (0.3, -0.2, 0), (0.0, 0.25, 0)):
            lb.saeule(netz, st, (x + dx, y + dy, z), 0.27, 0, 0.5, "Sack", n=6)
        lb.saeule(netz, st, (x, y - 0.05, z), 0.26, 0.5, 0.95, "Sack", n=6)
        return 0.95
    # Tuchballen: verschnürt
    lb.kiste(netz, st, (x, y, z), (1.15, 0.8, 0.62), stoff="Wolle", dreh=0.03 * ((nummer % 4) - 1.5))
    for dx in (-0.3, 0.3):
        netz.quader((x + dx, y, z + 0.32), (0.06, 0.83, 0.02), st["Seil"])
    return 0.62


# Unten: sieben Reihen zu je zwei Stücken (y = ∓0,85); oben: vier Stücke auf Kisten/Ballen
UNTEN = [(-5.3, "fass", "fass"), (-3.6, "kiste", "sack"), (-1.9, "fass", "ballen"), (-0.2, "kiste", "kiste"),
         (1.5, "sack", "fass"), (4.3, "ballen", "kiste"), (6.0, "fass", "sack")]
OBEN = [(-3.6, -0.85, "ballen"), (-0.2, -0.85, "ballen"), (-0.2, 0.85, "ballen"), (4.3, 0.85, "ballen")]
ANKER_X = 0.3


def ladungsplan(unten_reihen=None, oben_reihen=None, boden=None):
    """Die nummerierten Plätze der Ladung (Liste von Wörterbüchern, Nummer = Index + 1): erst die unteren, dann die oberen Stücke;
    wer weit von der Laufplanke liegt, bekommt die kleinere Nummer (wird zuletzt entladen)."""
    unten_reihen = UNTEN if unten_reihen is None else unten_reihen
    oben_reihen = OBEN if oben_reihen is None else oben_reihen
    boden = BODEN if boden is None else boden
    plaetze = []
    for x, a, b in unten_reihen:
        plaetze.append({"x": x, "y": -0.85, "art": a, "z": boden, "oben": False})
        plaetze.append({"x": x, "y": 0.85, "art": b, "z": boden, "oben": False})
    plaetze.sort(key=lambda p: -abs(p["x"] - ANKER_X) - 0.001 * abs(p["y"]))
    unten = {(p["x"], p["y"]): p for p in plaetze}
    hoehen = {"fass": 0.95, "kiste": 0.72, "sack": 0.95, "ballen": 0.62}
    oben = [{"x": x, "y": y, "art": art, "z": boden + hoehen[unten[(x, y)]["art"]], "oben": True} for x, y, art in oben_reihen]
    oben.sort(key=lambda p: -abs(p["x"] - ANKER_X))
    return plaetze + oben


def ladung(st):
    objekte = []
    for nr, p in enumerate(ladungsplan(), 1):
        netz = bs.Netz(f"Ladung_{nr}")
        stueck(netz, st, p["art"], p["x"], p["y"], p["z"], nr)
        objekte.append(netz.objekt())
    return objekte


def bauen(st):
    rumpf_obj = rumpf(st)
    teile = [rumpf_obj] + [n.objekt() for n in _MASTTEILE] + [segel_gesetzt(st), segel_gerefft(st), wimpel(st)] + ladung(st)
    planke = laufplanke(st)
    w, _, _, g = profil(ANKER_X)
    planke.location = (ANKER_X, -w, g)
    teile.append(planke)
    anker = [bs.anker("Anker_Bug", (HALB + 0.1, 0, 0), 270), bs.anker("Anker_Heck", (-HALB - 0.1, 0, 0), 90),
             bs.anker("Anker_Kai", (ANKER_X, -BREITE / 2 - 0.3, 0), 0),
             bs.anker("Anker_Planke", (ANKER_X, -BREITE / 2 - PLANKE, BORD - 0.05), 0),
             bs.anker("Anker_Schiffer", (-HALB + 1.1 if SCHIFFER_X is None else SCHIFFER_X, 0.35, DECK), 270)]
    return teile + anker


def vorschau(objekte, schnell):
    ws.buehne(26.0, 2.5, schnell)
    ws.gelaende([(-60, -3.0), (60, -3.0)], (-60, 60, 0.0), achse="y")
    zeigen = {o for o in objekte if o.name != "Segel_Gerefft"} | {k for o in objekte for k in o.children_recursive}
    ws.stempel("Oberländer: Segel gesetzt, Ladung 1–18, Laufplanke, Wimpel")
    a = bs.rendern("_wasser_schiff_a", lambda o: o in zeigen)
    zeigen = {o for o in objekte if o.name not in ("Segel_Gesetzt", "Fahne") and not o.name.startswith("Ladung_")}
    ws.stempel("im Hafen: Segel gerefft, leer")
    b = bs.rendern("_wasser_schiff_b", lambda o: o in zeigen, 2)
    if "--seite" in sys.argv:                                     # große Einzelansicht für Bilder (nicht im Repository)
        ws.stempel("Oberländer, von der Seite")
        zeigen = {o for o in objekte if o.name != "Segel_Gerefft"} | {k for o in objekte for k in o.children_recursive}
        bs.rendern("_oberlaender_seite", lambda o: o in zeigen, 3)
    return ws.nebeneinander("wasser_schiff", [a, b])


def main():
    schnell = "--schnell" in sys.argv
    name = "wasser_schiff"
    bs.neue_szene()
    st = bs.Stoffe()
    objekte = bauen(st)
    netze = [o for o in objekte if o.type == "MESH"]
    alle = objekte + [k for o in objekte for k in o.children_recursive if k not in objekte]
    bs.bericht(name, [o for o in alle if o.type == "MESH"] + [o for o in alle if o.type != "MESH"], einzeln=True)
    bs.BIBLIOTHEK_JE_FLAECHE = True
    vorher, nachher = bs.farben_zusammenlegen([o for o in alle if o.type == "MESH"], behalten=hb.BEHALTEN)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren(name, objekte))
    if "--ohne-bilder" not in sys.argv:
        print(vorschau(netze + [o for o in objekte if o.type != "MESH"], schnell))


if __name__ == "__main__":
    main()
