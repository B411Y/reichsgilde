"""Werkstatt des Wirts – Schänke (Stufe 1), Wirtshaus (Stufe 2), Gasthaus (Stufe 3). Fläche 16 × 14 m.

    blender -b --python tools/modelle/wirt.py        oder
    python tools/modelle/wirt.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/wirt.glb und Bilder nach docs/modelle/.

Objekte im GLB (wie bei den anderen Werkstätten):
    Haus        der Gasthof in L-Form, in jeder Stufe gleich
    Stufe1      Schänke: Tisch mit Bänken vor der Tür, Fasslager an der Hauswand, Linde mit Tischen im Hof,
                Brennholz und Fässer im Hinterhof, Zaun
    Stufe2      Wirtshaus: dazu Biergarten unter einer Laube, Kegelbahn am Zaun, Gaube
    Stufe3      Gasthaus: Stall für die Pferde der Reisenden mit Tränke und Pferd, Biergarten unter Laube und
                Linde, Gaube, Dachreiter mit weinroter Spitze
    Wappen, Fahne, Leerstand, Geruest, Anker_Tuer, Anker_Karren
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Gasthof in L-Form aus Fachwerk auf einem Bruchsteinsockel, Ziegeldach wie bisher im Spiel:
    der Gassenflügel (8,6 × 5,2 m) mit der Traufe zur Gasse, Toreinfahrt mit Rampe, Schänkentür mit Stufe und
    Wirtshausschild am Ausleger; rechts dahinter der Seitenflügel (3,4 × 6,2 m) nach hinten, sein Dach läuft in
    das Dach des Gassenflügels. Im Winkel des L liegt hinten links der Hinterhof, rechts der Hof für
    Biergarten, Kegelbahn und Stall.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "wirt"
FLAECHE = (16.0, 14.0)
STUFEN = ("Schänke", "Wirtshaus", "Gasthaus")

# ── Maße des Gasthofs ────────────────────────────────────────────────────────
X0, XA, Y0, YA = -7.4, 1.2, -5.0, 0.2       # Gassenflügel
XB0, XB1, YB = -2.6, 0.8, 6.4               # Seitenflügel (von YA bis YB)
CXA, CYA = (X0 + XA) / 2, (Y0 + YA) / 2
CXB, CYB = (XB0 + XB1) / 2, (YA + YB) / 2
HB = (XB1 - XB0) / 2
Z_SOCKEL, H_EG, H_OG = 0.3, 3.0, 2.6
Z_OG = Z_SOCKEL + H_EG
Z_TRAUFE = Z_OG + H_OG                      # 5,9
NEIGUNG = 50


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []

    # ── Sockel und Putzkörper der beiden Flügel ───────────────────────────────
    haus.quader((CXA, CYA, Z_SOCKEL / 2), (XA - X0 + 0.12, YA - Y0 + 0.12, Z_SOCKEL), st["Bruchstein"])
    haus.quader((CXB, (YA + 0.06 + YB + 0.06) / 2, Z_SOCKEL / 2), (XB1 - XB0 + 0.12, YB - YA, Z_SOCKEL), st["Bruchstein"])
    haus.quader((CXA, CYA, (Z_SOCKEL + Z_TRAUFE) / 2), (XA - X0, YA - Y0, Z_TRAUFE - Z_SOCKEL), st["Putz"])
    haus.quader((CXB, CYB, (Z_SOCKEL + Z_TRAUFE) / 2), (XB1 - XB0, YB - YA, Z_TRAUFE - Z_SOCKEL), st["Putz"], ohne=("-y",))
    for (x0, x1, y0, y1) in ((X0, XA, Y0, YA), (XB0, XB1, YA, YB)):
        haus.quader(((x0 + x1) / 2, (y0 + y1) / 2, Z_OG - 0.05), (x1 - x0 + 0.08, y1 - y0 + 0.08 - 0.04 * (y0 == YA), 0.16),
                    st["Fachwerk"], ohne=("+z",) + (("-y",) if y0 == YA else ()))

    # ── Wände: Fachwerk, Fenster, Türen ───────────────────────────────────────
    F = "fenster"
    eg = dict(v=0.9, b=1.0, h=1.2)
    og = dict(v=0.6, b=0.8, h=1.1)
    waende = {   # Name: (Mitte, Außenseite, Länge, Erdgeschoss, Obergeschoss)
        "A vorn": ((CXA, Y0), "vorne", XA - X0, [Oeffnung("tuer", -2.6, 0.15, 2.6, 2.6), Oeffnung(F, -0.3, **eg),
                                                Oeffnung("tuer", 1.3, 0.0, 1.2, 2.25), Oeffnung(F, 3.0, **eg)],
                   [Oeffnung(F, u, **og) for u in (-3.2, -1.6, 0.0, 1.6, 3.2)]),
        "A links": ((X0, CYA), "links", YA - Y0, [Oeffnung(F, u, **eg) for u in (-1.2, 1.2)],
                    [Oeffnung(F, u, **og) for u in (-1.2, 1.2)]),
        "A rechts": ((XA, CYA), "rechts", YA - Y0, [Oeffnung(F, u, **eg) for u in (-1.2, 1.2)],
                     [Oeffnung(F, u, **og) for u in (-1.2, 1.2)]),
        "A hinten": (((X0 + XB0) / 2, YA), "hinten", XB0 - X0, [Oeffnung("tuer", 1.0, 0.0, 1.0, 2.1), Oeffnung(F, -1.2, **eg)],
                     [Oeffnung(F, u, **og) for u in (-1.2, 1.2)]),
        "B links": ((XB0, CYB), "links", YB - YA, [Oeffnung(F, u, **eg) for u in (-1.6, 1.2)],
                    [Oeffnung(F, u, **og) for u in (-1.6, 1.2)]),
        "B rechts": ((XB1, CYB), "rechts", YB - YA, [Oeffnung("tuer", -1.6, 0.0, 1.1, 2.2), Oeffnung(F, 0.4, **eg),
                                                    Oeffnung(F, 2.2, **eg)],
                     [Oeffnung(F, u, **og) for u in (-1.9, 0.0, 1.9)]),
        "B hinten": ((CXB, YB), "hinten", XB1 - XB0, [Oeffnung(F, 0.0, **eg)], [Oeffnung(F, 0.0, **og)]),
    }
    M_wand = {}
    for name, ((x, y), seite, L, unten, oben) in waende.items():
        for z, hoehe, liste, kreuze in ((Z_SOCKEL, H_EG, unten, False), (Z_OG, H_OG, oben, True)):
            M = bs.wandmatrix((x, y, z), seite)
            M_wand[name, z] = M
            bs.fachwerk(haus, M, L, hoehe, liste, st, kreuze=kreuze, saat=len(name) + int(z))
            for o in liste:
                if o.art == "fenster":
                    bs.fenster(haus, M, o, st)
                oeffnungen.append((M, o))
    M_vorn = M_wand["A vorn", Z_SOCKEL]
    tor, _, tuer, _ = waende["A vorn"][3]
    bs.tor_fluegel(haus, st, M_vorn, tor)
    bs.tuer(haus, M_vorn, tuer, st)
    for name, nummer in (("A hinten", 0), ("B rechts", 0)):
        bs.tuer(haus, M_wand[name, Z_SOCKEL], waende[name][3][nummer], st)
    x_tor, x_tuer = CXA + tor.u, CXA + tuer.u
    rampe = [(Y0 - 1.1, 0.0), (Y0, 0.0), (Y0, Z_SOCKEL + 0.01), (Y0 - 0.25, Z_SOCKEL + 0.01)]   # 1 cm über dem Sockel
    haus.prisma(rampe, x_tor - 1.45, x_tor + 1.45, st["Bruchstein"])
    haus.quader((x_tuer, Y0 - 0.25, (Z_SOCKEL + 0.01) / 2), (1.6, 0.5, Z_SOCKEL + 0.01), st["Sandstein"])
    wirtshausschild(haus, st, M_vorn, tuer.u, 2.8)

    # ── Dächer: Gassenflügel mit Traufe zur Gasse, Seitenflügel läuft hinein ──
    dach = bs.satteldach(haus, st, X0, XA, Y0, YA, Z_TRAUFE, NEIGUNG, traufe=0.4, ortgang=0.3, **bs.ZIEGEL)
    hoch_a = (YA - Y0) / 2 * dach["tan"]
    for x, seite in ((X0, "links"), (XA, "rechts")):
        bs.fachwerkgiebel(haus, st, bs.wandmatrix((x, CYA, Z_TRAUFE), seite), YA - Y0, hoch_a, oeffnungen)
    y_kehle = YA - HB - 0.1                  # dort trifft der First des Seitenflügels die hintere Dachfläche
    drehung = Matrix.Translation((CXB, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach_b = bs.satteldach(haus, st, y_kehle, YB, -HB, HB, Z_TRAUFE, NEIGUNG, traufe=0.35, ortgang=0.3,
                           matrix=drehung, **bs.ZIEGEL)
    bs.fachwerkgiebel(haus, st, bs.wandmatrix((CXB, YB, Z_TRAUFE), "hinten"), XB1 - XB0, HB * dach_b["tan"], oeffnungen)
    xs, ys = CXA - 2.0, CYA + 1.1                                     # Küchenschornstein, hintere Dachfläche
    z0 = dach["oben_hinten"](ys) - 0.3
    haus.quader((xs, ys, (z0 + dach["z_first"] + 0.7) / 2), (0.6, 0.6, dach["z_first"] + 0.7 - z0), st["Bruchstein dunkel"])
    haus.quader((xs, ys, dach["z_first"] + 0.76), (0.78, 0.78, 0.12), st["Bruchstein"])

    # ── Wappen, Fahne ─────────────────────────────────────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, M_wand["A vorn", Z_OG], 0.8, 1.15, st, groesse=0.65)
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (XA - 0.3, dach["y_first"], dach["z_first"] - 0.25), hoehe=2.4, richtung=1)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    schaenke(stufe1, st)
    stufe2 = bs.Netz("Stufe2")
    wirtshaus(stufe2, st)
    bs.gaube(stufe2, st, dach, [], x0=CXA - 0.4, x1=CXA + 1.2, y_front=Y0 + 1.1)
    stufe3 = bs.Netz("Stufe3")
    gasthaus(stufe3, st)
    bs.gaube(stufe3, st, dach, [], x0=CXA - 0.4, x1=CXA + 1.2, y_front=Y0 + 1.1)
    bs.dachreiter(stufe3, st, Vector((CXA - 2.9, dach["y_first"], dach["z_first"])), spitze="Weinrot")

    # ── Leerstand, Gerüst, Anker ──────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.6, Y0 - 1.3), (XA - 0.2, Y0 - 0.15), (X0 - 0.15, -1.0), (XB1 + 0.15, 3.0)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=XA + 0.15, y_innen=Y0 - 0.3, y_aussen=Y0 - 1.2,
                  ebenen=(2.3, 4.6), hoehe_innen=5.4, hoehe_aussen=6.4, felder=3)
    anker = [bs.anker("Anker_Tuer", (x_tuer, Y0 - 0.2, Z_SOCKEL + 0.01)),
             bs.anker("Anker_Karren", (x_tor, Y0 - 1.6, 0.0))]

    objekte = [haus.objekt(), stufe1.objekt(), stufe2.objekt(), stufe3.objekt(), wappen.objekt(), fahne.objekt(),
               leer.objekt(), geruest.objekt()] + anker
    for o in objekte:
        if o.name in ("Stufe2", "Stufe3", "Leerstand", "Geruest"):
            bs.zustand(o)
        elif o.name == "Stufe1":
            o["zustand"] = True
    return objekte


# ─────────────────────────────────────────────────────────────────────────────
#  Teile des Gasthofs
# ─────────────────────────────────────────────────────────────────────────────

def wirtshausschild(netz, st, M, u, v):
    """Wirtshausschild in Ocker (wie bisher im Spiel) mit einem Bierkrug, an einem langen Ausleger;
    darunter ein grüner Kranz als Schankzeichen."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -1.4, v)), 0.06, 0.06, eisen)
    netz.stab(M @ Vector((u, 0, v - 0.6)), M @ Vector((u, -1.0, v - 0.02)), 0.05, 0.05, eisen)
    for d in (-1.25, -0.55):
        netz.stab(M @ Vector((u, d, v)), M @ Vector((u, d, v - 0.2)), 0.02, 0.02, eisen)
    dreh = M @ Matrix.Translation((u, -0.9, v - 0.62)) @ Matrix.Rotation(math.radians(-90), 4, "Z")
    netz.prisma([(-0.5, -0.42), (0.5, -0.42), (0.5, 0.42), (-0.5, 0.42)], -0.04, 0.04, st["Ocker"], matrix=dreh)
    quer = dreh.to_3x3() @ Vector((1, 0, 0))
    netz.prisma([(-0.17, -0.28), (0.17, -0.28), (0.15, 0.2), (-0.15, 0.2)], 0.04, 0.07, st["Holz"], matrix=dreh)   # Krug
    netz.prisma([(-0.18, 0.2), (0.18, 0.2), (0.18, 0.28), (-0.18, 0.28)], 0.04, 0.075, st["Putz"], matrix=dreh)    # Schaum
    for a, b in (((0.17, 0.12), (0.3, 0.12)), ((0.3, 0.12), (0.3, -0.16)), ((0.3, -0.16), (0.17, -0.16))):         # Henkel
        netz.stab(dreh @ Vector((0.055, *a)), dreh @ Vector((0.055, *b)), 0.045, 0.03, st["Holz"], quer=quer)
    # Kranz unter dem Ausleger, nah an der Wand
    mitte = M @ Vector((u, -0.3, v - 0.55))
    for k in range(8):
        w0, w1 = math.radians(45 * k), math.radians(45 * (k + 1))
        a = mitte + Vector((0.2 * math.cos(w0), 0, 0.2 * math.sin(w0)))
        b = mitte + Vector((0.2 * math.cos(w1), 0, 0.2 * math.sin(w1)))
        netz.stab(a, b, 0.1, 0.1, st["Laub"], quer=(0, 1, 0), mehr=0.04)
    netz.stab(mitte + Vector((0, 0, 0.2)), M @ Vector((u, -0.3, v - 0.02)), 0.02, 0.02, st["Seil"])


def tisch(netz, st, mitte, dreh=0.0, laenge=2.2, kruege=3, saat=0):
    """Wirtshaustisch auf zwei Böcken mit zwei Bänken, darauf ein paar Krüge."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    holz, alt = st["Holz"], st["Holz alt"]
    netz.quader((0, 0, 0.76), (laenge, 0.8, 0.07), holz, t)
    for dx in (-laenge * 0.38, laenge * 0.38):
        netz.quader((dx, 0, 0.36), (0.1, 0.6, 0.72), alt, t)
        netz.quader((dx, 0, 0.05), (0.12, 0.75, 0.1), alt, t)
        for dy in (-0.68, 0.68):
            netz.quader((dx, dy, 0.21), (0.09, 0.09, 0.42), alt, t)
    for dy in (-0.68, 0.68):
        netz.quader((0, dy, 0.45), (laenge, 0.3, 0.06), holz, t)
    zufall = random.Random(saat)
    for k in range(kruege):
        p = t @ Vector((zufall.uniform(-laenge * 0.4, laenge * 0.4), zufall.uniform(-0.25, 0.25), 0.795))
        bs.achteck(netz, st, p, 0.07, 0.0, 0.18, "Sandstein" if k % 2 else "Holz", n=6)


def fass_liegend(netz, st, mitte, dreh=0.0, r=0.36, laenge=0.95):
    """Liegendes Fass (Achteck entlang x) mit zwei Eisenreifen, Stirnseiten in hellem Holz."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    profil = [(r * math.cos(math.radians(22.5 + 45 * k)), r * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    netz.prisma(profil, -laenge / 2, laenge / 2, st["Holz"], matrix=t, m_enden=st["Schnittholz"])
    ring = [(p[0] * 1.04, p[1] * 1.04) for p in profil]
    for x in (-laenge * 0.3, laenge * 0.3):
        netz.prisma(ring, x - 0.03, x + 0.03, st["Eisen"], matrix=t)


def fasslager(netz, st, mitte, dreh=0.0, n=3):
    """Fasslager: Balkengestell mit einer Reihe liegender Fässer, oben eines quer, daneben zwei stehende."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    laenge = n * 0.8
    for dx in (-0.35, 0.35):
        netz.quader(t @ Vector((dx, 0, 0.08)), (0.12, laenge + 0.3, 0.16), st["Holz alt"], matrix=t.to_3x3().to_4x4())
    for k in range(n):
        y = -laenge / 2 + 0.4 + 0.8 * k
        fass_liegend(netz, st, t @ Vector((0, y, 0.52)), dreh=dreh)
    fass_liegend(netz, st, t @ Vector((0, 0.0, 1.2)), dreh=dreh, r=0.3, laenge=0.8)
    for k, dy in enumerate((laenge / 2 + 0.55, laenge / 2 + 1.25)):
        bs.fass(netz, st, t @ Vector((0.05 * k, dy, 0)), r=0.33, h=0.95)


def laube(netz, st, x0, x1, y0, y1, h=2.4, saat=0):
    """Laube: Pfosten, Pfetten, Latten und darüber Weinlaub in Flecken."""
    holz = st["Holz alt"]
    xs = [x0, (x0 + x1) / 2, x1]
    for x in xs:
        for y in (y0, y1):
            netz.stab((x, y, 0), (x, y, h), 0.12, 0.12, holz)
    for y in (y0, y1):
        netz.stab((x0 - 0.2, y, h - 0.06), (x1 + 0.2, y, h - 0.06), 0.12, 0.12, holz)
    x = x0 + 0.15
    while x < x1:
        netz.stab((x, y0 - 0.25, h + 0.05), (x, y1 + 0.25, h + 0.05), 0.06, 0.06, holz, quer=(1, 0, 0))
        x += 0.45
    zufall = random.Random(saat)
    for k in range(9):
        cx, cy = zufall.uniform(x0 + 0.4, x1 - 0.4), zufall.uniform(y0 + 0.2, y1 - 0.2)
        rr = zufall.uniform(0.45, 0.8)
        netz.vieleck_nach([(cx + rr * math.cos(math.radians(60 * j + 15 * k)), cy + rr * math.sin(math.radians(60 * j + 15 * k)),
                            h + 0.1 + 0.01 * k) for j in range(6)], (0, 0, 1), st["Laub" if k % 2 else "Laub hell"])
    for x in (x0, x1):                                       # Ranken an den Eckpfosten
        for y in (y0, y1):
            netz.stab((x + 0.07, y, 0.2), (x + 0.07, y, h), 0.08, 0.05, st["Laub"], quer=(1, 0, 0))


def kegelbahn(netz, st, x, y0, y1):
    """Kegelbahn: Bohlenbahn mit Randbrettern, neun Kegel unter einem Pultdach am Ende, Kugel und Bank am Anfang."""
    netz.quader((x, (y0 + y1) / 2, 0.06), (0.9, y1 - y0, 0.12), st["Schnittholz"])
    for s in (-1, 1):
        netz.quader((x + s * 0.5, (y0 + y1) / 2, 0.11), (0.1, y1 - y0, 0.22), st["Holz alt"])
    yk = y1 - 0.9
    for i in range(3):
        for j in range(3):
            p = Vector((x + (i - j) * 0.17, yk + (i + j - 2) * 0.17, 0.12))
            bs.achteck(netz, st, p, 0.05, 0.0, 0.3, "Putz", n=6)
            bs.achteck(netz, st, p, 0.035, 0.3, 0.38, "Putz", n=6)
    for s in (-1, 1):
        for y in (y1 - 1.8, y1):
            netz.stab((x + s * 0.65, y, 0), (x + s * 0.65, y, 2.1), 0.1, 0.1, st["Holz alt"])
    bs.pultdach(netz, st, x - 0.75, x + 0.7, y1 - 1.9, y1 + 0.1, 2.25, 2.0)
    bs.achteck(netz, st, Vector((x + 0.15, y0 + 0.5, 0.12)), 0.12, 0.0, 0.22, "Holz")
    netz.quader((x - 0.95, y0 + 1.4, 0.45), (0.3, 1.6, 0.06), st["Holz"])
    for y in (y0 + 0.75, y0 + 2.05):
        netz.quader((x - 0.95, y, 0.21), (0.25, 0.08, 0.42), st["Holz alt"])


def stall(netz, st, x0, x1, y0, y1, h=3.0):
    """Stall für die Pferde der Gäste: Fachwerk auf niedrigem Sockel, Ziegeldach (First entlang x),
    vorn zwei Stalltüren in der Hausfarbe (oben offen) und eine Heuluke im Giebel."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    netz.quader((cx, cy, 0.1), (x1 - x0 + 0.1, y1 - y0 + 0.1, 0.2), st["Bruchstein"])
    netz.quader((cx, cy, (0.2 + h) / 2), (x1 - x0, y1 - y0, h - 0.2), st["Putz"], ohne=("+z",))
    tueren = [Oeffnung("tuer", u, 0.0, 1.2, 2.2) for u in (-1.4, 1.2)]
    fenster = [Oeffnung("fenster", u, 1.3, 0.7, 0.6, laeden=False) for u in (-2.4, 0.0, 2.2)]
    M = bs.wandmatrix((cx, y0, 0.2), "vorne")
    bs.fachwerk(netz, M, x1 - x0, h - 0.2, tueren + fenster, st, saat=3)
    for o in fenster:
        bs.fenster(netz, M, o, st, kreuz=None)
    for o in tueren:                                         # Stalltür: unten zu, oben offen
        netz.quader((o.u, -0.01, o.v + o.h * 0.3), (o.b, 0.04, o.h * 0.6), st["Hausfarbe"], M, ohne=("+y",))
        netz.vieleck([M @ Vector(p) for p in ((o.u - o.b / 2, -0.005, o.v + o.h * 0.6), (o.u + o.b / 2, -0.005, o.v + o.h * 0.6),
                                              (o.u + o.b / 2, -0.005, o.v + o.h), (o.u - o.b / 2, -0.005, o.v + o.h))], st["Loch"])
        t = M @ Matrix.Translation((o.u + o.b / 2, -0.03, o.v + o.h * 0.8)) @ Matrix.Rotation(math.radians(-100), 4, "Z")
        netz.quader((-o.b / 2, 0, 0), (o.b, 0.04, o.h * 0.4), st["Hausfarbe"], t)
    M_r = bs.wandmatrix((x1, cy, 0.2), "rechts")
    bs.fachwerk(netz, M_r, y1 - y0, h - 0.2, [], st, saat=4)
    dach = bs.satteldach(netz, st, x0, x1, y0, y1, h, 45, traufe=0.35, ortgang=0.3, **bs.ZIEGEL)
    hoch = (y1 - y0) / 2 * dach["tan"]
    for x, seite in ((x1, "rechts"), (x0, "links")):
        Mg = bs.wandmatrix((x, cy, h), seite)
        bs.platte(netz, Mg, [(-(y1 - y0) / 2, 0), ((y1 - y0) / 2, 0), (0, hoch)], 0, 0.3, st["Putz"])
        luke = Oeffnung("tuer", 0.0, 0.25, 0.8, 0.8)
        netz.vieleck([Mg @ Vector(p) for p in ((-0.4, -0.005, 0.25), (0.4, -0.005, 0.25), (0.4, -0.005, 1.05),
                                                (-0.4, -0.005, 1.05))], st["Loch"])
        for u in (-0.5, 0.5):
            bs.balken(netz, Mg, u, 0.1, u, min(1.2, hoch - 0.3), st["Fachwerk"])
        bs.balken(netz, Mg, -0.55, 1.15, 0.55, 1.15, st["Fachwerk"], vor=bs.VOR_RIEGEL)
        netz.quader((0, -0.12, 0.3), (0.7, 0.2, 0.25), st["Heu"], Mg)
        del luke


def traenke(netz, st, mitte, dreh=0.0):
    """Pferdetränke: Holztrog mit Wasser."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, 0.3), (1.8, 0.6, 0.6), st["Holz alt"], t)
    netz.quader((0, 0, 0.605), (1.66, 0.46, 0.01), st["Wasser"], t, ohne=("-z",))


def hinterhof(netz, st, saat=0):
    """Hinterhof im Winkel des L: Brennholz für die Küche, ein paar Fässer, ein Hackklotz."""
    zufall = random.Random(saat)
    for k in range(3):
        bs.fass(netz, st, Vector((-6.9 + 0.75 * k, 1.0, 0)), r=0.32, h=0.9)
    for lage in range(4):
        for j in range(5 - lage % 2):
            netz.quader((-6.9, 2.6 + 0.48 * (j + 0.5 * (lage % 2)), 0.09 + 0.17 * lage), (0.5, 0.4, 0.16), st["Holz"])
    netz.quader((-6.9, 3.8, 0.09 + 0.17 * 4 + 0.03), (0.75, 2.8, 0.04), st["Holz alt"])
    bs.fass(netz, st, Vector((-5.0, 3.0, 0)), r=0.3, h=0.55, reifen=(), stoff="Holz alt")
    for k in range(4):
        netz.quader((-4.6 + zufall.uniform(-0.4, 0.4), 3.4 + zufall.uniform(-0.4, 0.4), 0.06), (0.45, 0.14, 0.12), st["Holz"])


# ─────────────────────────────────────────────────────────────────────────────
#  Stufen
# ─────────────────────────────────────────────────────────────────────────────

def vorplatz(netz, st):
    """Was in jeder Stufe vor dem Haus und an der Hauswand steht: Tisch mit Bänken neben der Tür, Fasslager."""
    tisch(netz, st, (0.5, -6.0, 0), laenge=2.2, saat=1)
    fasslager(netz, st, (2.1, -3.1, 0))
    hinterhof(netz, st)


def schaenke(netz, st):
    """Stufe 1: Tisch vor der Tür, Fasslager, Linde mit zwei Tischen im Hof, Zaun."""
    vorplatz(netz, st)
    bs.linde(netz, st, (5.0, 2.6, 0), saat=2)
    tisch(netz, st, (4.4, 0.4, 0), laenge=2.0, saat=3)
    tisch(netz, st, (5.6, 4.6, 0), dreh=90, laenge=2.0, saat=4)
    bs.zaun(netz, st, [(7.8, -4.8), (7.8, 6.8), (-7.8, 6.8), (-7.8, YA + 0.3)])


def wirtshaus(netz, st):
    """Stufe 2: dazu ein Biergarten unter einer Laube, die Linde mit Tischen und eine Kegelbahn am Zaun."""
    vorplatz(netz, st)
    laube(netz, st, 2.6, 5.8, -4.0, -0.8, saat=5)
    tisch(netz, st, (4.2, -3.3, 0), laenge=2.4, saat=6)
    tisch(netz, st, (4.2, -1.5, 0), laenge=2.4, saat=7)
    bs.linde(netz, st, (4.0, 3.8, 0), saat=8)
    tisch(netz, st, (3.4, 1.4, 0), laenge=2.0, saat=9)
    tisch(netz, st, (4.4, 5.8, 0), laenge=2.0, saat=10)
    kegelbahn(netz, st, 6.8, -4.4, 5.6)
    bs.zaun(netz, st, [(7.8, -4.8), (7.8, 6.8), (-7.8, 6.8), (-7.8, YA + 0.3)])


def gasthaus(netz, st):
    """Stufe 3: Stall für die Pferde der Reisenden mit Tränke und Pferd, Biergarten unter Laube und Linde."""
    vorplatz(netz, st)
    stall(netz, st, 2.2, 7.6, 3.4, 6.3)
    traenke(netz, st, (3.6, 2.5, 0))
    bs.pferd(netz, st, (4.6, 1.4, 0), 145)
    laube(netz, st, 2.8, 6.0, -4.0, -0.8, saat=11)
    tisch(netz, st, (4.4, -3.3, 0), laenge=2.4, saat=12)
    tisch(netz, st, (4.4, -1.5, 0), laenge=2.4, saat=13)
    bs.linde(netz, st, (6.6, 1.0, 0), hoehe=5.6, r=1.15, saat=14)
    bs.zaun(netz, st, [(7.8, -4.8), (7.8, 3.3)])
    bs.zaun(netz, st, [(2.0, 6.8), (-7.8, 6.8), (-7.8, YA + 0.3)])


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    zustaende = ("Stufe1", "Stufe2", "Stufe3", "Leerstand", "Geruest")

    def nur(*an):
        return lambda o: o.name not in zustaende or o.name in an

    schnell = "--schnell" in sys.argv
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=4.2, ausschnitt=27.0, schnell=schnell)
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, nur("Stufe1")))
        return
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, nur("Stufe3")))
        return
    bilder = [bs.rendern(f"{NAME}-stufe{k}", nur(f"Stufe{k}")) for k in (1, 2, 3)]
    print(bilder)
    print(bs.rendern(NAME + "-leerstand", lambda o: nur("Stufe1", "Leerstand")(o) and o.name not in ("Wappen", "Fahne")))
    print(bs.rendern(NAME + "-geruest", nur("Stufe2", "Geruest")))
    if not schnell:
        print(bs.rundum(NAME + "-stufe3", nur("Stufe3")))
    print(bs.nebeneinander(NAME, bilder, [f"Stufe {k + 1}: {n}" for k, n in enumerate(STUFEN)]))
    print(bs.anker_bild(NAME, nur("Stufe1")))
    print(bs.nachtbild(NAME, nur("Stufe3")))


if __name__ == "__main__":
    main()
