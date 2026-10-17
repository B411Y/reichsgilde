"""Fachwerkhaus – erste Stufe des Wohnhauses der Spielhäuser. Belegte Fläche 8 × 10 m.

    blender -b --python tools/modelle/fachwerkhaus.py        oder
    python tools/modelle/fachwerkhaus.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/fachwerkhaus.glb und Bilder nach docs/modelle/.

Objekte im GLB:
    Haus        das Haus selbst
    Wappen      Schild in der Hausfarbe an der Gassenseite     (im Leerstand ausblenden)
    Fahne       Fahne in der Hausfarbe auf dem First           (im Leerstand ausblenden)
    Leerstand   Bretter vor Türen und Fenstern, Löcher im Dach, Unkraut   – standardmäßig aus
    Geruest     Baugerüst vor der Gassenseite mit Leiter                  – standardmäßig aus

Das Haus (Blender: Z oben, Gasse bei -Y):
    Bruchsteinsockel, Erdgeschoss 6,4 × 7 m, Obergeschoss kragt vorn und seitlich 35 cm vor
    (Balkenköpfe sichtbar), Satteldach 50° mit Traufe zur Gasse, Schleppgaube, Schornstein,
    Fachwerk mit Eckstreben und Andreaskreuzen unter den Fenstern im Obergeschoss.
    Vorne 2 m Vorplatz (Gasse), hinten 1 m Hof.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "fachwerkhaus"
FLAECHE = (8.0, 10.0)

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
EG_X, EG_VORN, EG_HINTEN = 3.2, -3.0, 4.0         # Erdgeschoss: x ±3,2, y von -3 bis 4
KRAG = 0.35                                        # Vorkragung des Obergeschosses
OG_X, OG_VORN, OG_HINTEN = EG_X + KRAG, EG_VORN - KRAG, EG_HINTEN
Z_SOCKEL = 0.45
H_EG = 2.6                                         # Fachwerk des Erdgeschosses
Z_BALKENLAGE = Z_SOCKEL + H_EG                     # 3,05: hier liegen die Balkenköpfe
Z_OG = Z_BALKENLAGE + 0.2                          # 3,25
H_OG = 2.6
Z_TRAUFE = Z_OG + H_OG                             # 5,85
NEIGUNG = 50
UEBER = 0.045                                      # Fachwerk der Giebel- und Rückwand läuft um die Ecke


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []      # (Wandmatrix, Oeffnung) – für die Bretter im Leerstand

    def wand(M, L, H, liste, **art):
        bs.fachwerk(haus, M, L, H, liste, st, **art)
        for o in liste:
            (bs.tuer if o.art == "tuer" else bs.fenster)(haus, M, o, st)
            oeffnungen.append((M, o))

    y_eg = (EG_VORN + EG_HINTEN) / 2
    y_og = (OG_VORN + OG_HINTEN) / 2

    # ── Sockel mit einzelnen vorstehenden Steinen ─────────────────────────────
    haus.quader((0, y_eg, Z_SOCKEL / 2), (2 * EG_X + 0.1, EG_HINTEN - EG_VORN + 0.1, Z_SOCKEL), st["Bruchstein"])
    steine = [(-2.6, 0.12, 0.5), (-0.9, 0.3, 0.4), (2.4, 0.1, 0.55), (-3.0, 0.28, 0.35), (0.2, 0.08, 0.45)]
    for i, (u, v, b) in enumerate(steine):
        haus.quader((u, EG_VORN - 0.065, v + 0.07), (b, 0.04, 0.16), st["Bruchstein dunkel"], ohne=("+y",))
        haus.quader((EG_X + 0.065, -u * 1.1, v + 0.06), (0.04, b, 0.15), st["Bruchstein dunkel"], ohne=("-x",))
        haus.quader((-EG_X - 0.065, u * 1.1 + 0.5, v + 0.08), (0.04, b, 0.14), st["Bruchstein dunkel"], ohne=("+x",))
        haus.quader((-u, EG_HINTEN + 0.065, v + 0.07), (b, 0.04, 0.15), st["Bruchstein dunkel"], ohne=("-y",))

    # ── Putzkörper der beiden Geschosse ───────────────────────────────────────
    haus.quader((0, y_eg, (Z_SOCKEL + Z_OG) / 2), (2 * EG_X, EG_HINTEN - EG_VORN, Z_OG - Z_SOCKEL), st["Putz"], ohne=("+z",))
    haus.quader((0, y_og, Z_OG + H_OG / 2), (2 * OG_X, OG_HINTEN - OG_VORN, H_OG), st["Putz"])

    # ── Balkenlage: Stockschwelle, Balkenköpfe unter der Vorkragung, Gratstichbalken ──
    haus.quader((0, y_eg, Z_BALKENLAGE + 0.1), (2 * EG_X + 0.06, EG_HINTEN - EG_VORN + 0.06, 0.2), st["Fachwerk"], ohne=("+z",))
    z = Z_BALKENLAGE + 0.1
    for i in range(8):
        x = -EG_X + 0.45 + i * (2 * EG_X - 0.9) / 7
        haus.quader((x, (EG_VORN + OG_VORN - 0.04) / 2, z), (0.17, EG_VORN - OG_VORN + 0.04, 0.2), st["Fachwerk"], ohne=("+z", "+y"))
    for i in range(8):
        y = EG_VORN + 0.45 + i * (EG_HINTEN - EG_VORN - 0.8) / 7
        for s in (-1, 1):
            haus.quader((s * (EG_X + OG_X + 0.04) / 2, y, z), (OG_X - EG_X + 0.04, 0.17, 0.2), st["Fachwerk"],
                        ohne=("+z", "-x" if s > 0 else "+x"))
    for s in (-1, 1):
        haus.stab((s * EG_X, EG_VORN, z), (s * (OG_X + 0.05), OG_VORN - 0.05, z), 0.2, 0.2, st["Fachwerk"], quer=(0, 0, 1))

    # ── Erdgeschoss ───────────────────────────────────────────────────────────
    F, T = "fenster", "tuer"
    L_quer, L_tief = 2 * EG_X + 2 * UEBER, EG_HINTEN - EG_VORN
    wand(bs.wandmatrix((0, EG_VORN, Z_SOCKEL), "vorne"), L_quer, H_EG,
         [Oeffnung(F, -1.55, 0.9, 0.95, 1.15), Oeffnung(T, 1.2, 0.15, 1.0, 1.9)])
    wand(bs.wandmatrix((EG_X, y_eg, Z_SOCKEL), "rechts"), L_tief, H_EG,
         [Oeffnung(F, -1.8, 0.9, 0.95, 1.15), Oeffnung(F, 1.4, 0.9, 0.95, 1.15)])
    wand(bs.wandmatrix((0, EG_HINTEN, Z_SOCKEL), "hinten"), L_quer, H_EG,
         [Oeffnung(F, -1.4, 0.9, 0.95, 1.15), Oeffnung(T, 1.6, 0.15, 0.9, 1.9)])
    wand(bs.wandmatrix((-EG_X, y_eg, Z_SOCKEL), "links"), L_tief, H_EG,
         [Oeffnung(F, 1.6, 0.9, 0.95, 1.15), Oeffnung(F, -1.7, 0.9, 0.95, 1.15)])
    # Stufen vor der Haustür und vor der Hintertür
    for x, y, r in ((1.2, EG_VORN, -1), (-1.6, EG_HINTEN, 1)):
        haus.quader((x, y + r * 0.3, 0.13), (1.4, 0.6, 0.26), st["Bruchstein"])
        haus.quader((x, y + r * 0.12, 0.36), (1.25, 0.3, 0.2), st["Bruchstein"])

    # ── Obergeschoss: Andreaskreuze in der Brüstung ───────────────────────────
    L_quer, L_tief = 2 * OG_X + 2 * UEBER, OG_HINTEN - OG_VORN
    fo = dict(v=0.8, b=0.9, h=1.15)
    wand(bs.wandmatrix((0, OG_VORN, Z_OG), "vorne"), L_quer, H_OG,
         [Oeffnung(F, u, **fo) for u in (-2.45, -0.85, 2.15)], kreuze=True)
    wand(bs.wandmatrix((OG_X, y_og, Z_OG), "rechts"), L_tief, H_OG,
         [Oeffnung(F, u, **fo) for u in (-1.9, 1.7)], kreuze=True)
    wand(bs.wandmatrix((0, OG_HINTEN, Z_OG), "hinten"), L_quer, H_OG,
         [Oeffnung(F, u, **fo) for u in (-1.6, 1.6)], kreuze=True)
    wand(bs.wandmatrix((-OG_X, y_og, Z_OG), "links"), L_tief, H_OG,
         [Oeffnung(F, u, **fo) for u in (-1.7, 1.9)], kreuze=True)

    # ── Dach ──────────────────────────────────────────────────────────────────
    dach = bs.satteldach(haus, st, -OG_X, OG_X, OG_VORN, OG_HINTEN, Z_TRAUFE, NEIGUNG)
    hoch = (OG_HINTEN - OG_VORN) / 2 * dach["tan"]          # Höhe des Giebeldreiecks
    for s, seite in ((1, "rechts"), (-1, "links")):
        giebel(haus, st, bs.wandmatrix((s * OG_X, y_og, Z_TRAUFE), seite), OG_HINTEN - OG_VORN, hoch, oeffnungen)
    gaube(haus, st, dach, oeffnungen, x0=-2.5, x1=-0.8, y_front=OG_VORN + 1.3)
    # Schornstein auf der Hofseite
    xs, ys = -1.5, 1.7
    z_dach = dach["oben_hinten"](ys + 0.3)
    haus.quader((xs, ys, (z_dach - 0.3 + dach["z_first"] + 0.75) / 2), (0.62, 0.62, dach["z_first"] + 0.75 - z_dach + 0.3),
                st["Bruchstein dunkel"])
    haus.quader((xs, ys, dach["z_first"] + 0.8), (0.82, 0.82, 0.1), st["Bruchstein"])

    # ── Bank vor dem Fenster und Regentonne an der Seite ──────────────────────
    holz = st["Holz"]
    haus.quader((-1.55, EG_VORN - 0.33, 0.46), (1.5, 0.36, 0.07), holz)
    for x in (-2.15, -0.95):
        haus.quader((x, EG_VORN - 0.33, 0.21), (0.08, 0.3, 0.42), holz)
    fass(haus, st, Vector((EG_X + 0.42, 2.9, 0)))

    # ── Wappen über der Tür (Obergeschoss) und Fahne auf dem First ───────────
    wappen = bs.Netz("Wappen")
    M = bs.wandmatrix((0, OG_VORN, Z_OG), "vorne")
    schild = [(-0.34, 0.42), (0.34, 0.42), (0.34, -0.02), (0.0, -0.44), (-0.34, -0.02)]
    mitte_u, mitte_v = 0.65, 0.95
    bs.platte(wappen, M, [(mitte_u + u * 1.16, mitte_v + v * 1.12) for u, v in schild], -0.085, -0.05, st["Fachwerk"])
    bs.platte(wappen, M, [(mitte_u + u, mitte_v + v) for u, v in schild], -0.11, -0.08, st["Hausfarbe"])
    bs.balken(wappen, M, mitte_u - 0.24, mitte_v + 0.52, mitte_u + 0.24, mitte_v + 0.52, st["Eisen"], vor=0.12, breite=0.05)

    fahne = bs.Netz("Fahne")
    xf, yf, zf = 2.9, dach["y_first"], dach["z_first"]
    fahne.stab((xf, yf, zf - 0.3), (xf, yf, zf + 2.4), 0.08, 0.08, holz)
    fahne.quader((xf, yf, zf + 2.45), (0.14, 0.14, 0.14), st["Eisen"])
    tuch = [(0, 0), (1.25, 0.05), (0.98, 0.38), (1.25, 0.72), (0, 0.72)]   # Schwalbenschwanz
    fahne.prisma(tuch, -0.015, 0.015, st["Hausfarbe"],
                 matrix=Matrix.Translation((xf - 0.04, yf, zf + 1.55)) @ Matrix.Rotation(math.radians(90), 4, "Z"))

    # ── Leerstand ─────────────────────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for seite, x, s, r in (("vorn", 0.8, 2.6, 0.42), ("vorn", 2.4, 4.4, 0.22), ("hinten", -2.0, 3.2, 0.38)):
        dachloch(leer, st, dach, seite, x, s, r)
    for x, y in ((-3.1, EG_VORN - 0.15), (2.6, EG_VORN - 0.12), (EG_X + 0.12, -1.0), (-EG_X - 0.12, 2.6), (0.4, EG_HINTEN + 0.15)):
        buschel(leer, st, Vector((x, y, 0)), groesse=0.5)

    # ── Gerüst vor der Gassenseite ────────────────────────────────────────────
    geruest = bs.Netz("Geruest")
    baugeruest(geruest, st, x0=-OG_X - 0.1, x1=OG_X + 0.1, y_innen=OG_VORN - 0.25, y_aussen=OG_VORN - 1.15)

    objekte = [haus.objekt(), wappen.objekt(), fahne.objekt(), bs.zustand(leer.objekt()), bs.zustand(geruest.objekt())]
    return objekte


def giebel(netz, st, M, L, hoch, oeffnungen):
    """Giebeldreieck mit Fachwerk: Schwelle, Firstsäule, Kehlbalken, Ständer, Streben, zwei Fenster."""
    holz = st["Fachwerk"]
    halb = L / 2
    bs.platte(netz, M, [(-halb, 0), (halb, 0), (0, hoch)], 0, 0.3, st["Putz"])

    def rand(u):                     # Oberkante des Dreiecks bei u
        return hoch * (1 - abs(u) / halb)

    bs.balken(netz, M, -halb + 0.05, bs.B / 2, halb - 0.05, bs.B / 2, holz, vor=bs.VOR_SCHWELLE)
    bs.balken(netz, M, 0, bs.B, 0, hoch - 0.35, holz)
    vk = 2.2
    breite_k = halb * (1 - (vk + bs.B) / hoch) - 0.05
    bs.balken(netz, M, -breite_k, vk + bs.B / 2, breite_k, vk + bs.B / 2, holz, vor=bs.VOR_SCHWELLE)
    fb, fh, fv, fu = 0.7, 0.95, 0.55, 1.05
    for s in (-1, 1):
        for u in (fu - fb / 2 - bs.B / 2, fu + fb / 2 + bs.B / 2):
            bs.balken(netz, M, s * u, bs.B, s * u, vk, holz)
        aussen = 2.4
        bs.balken(netz, M, s * aussen, bs.B, s * aussen, rand(aussen + bs.B / 2) - 0.08, holz)
        for v in (fv - bs.B / 2, fv + fh + bs.B / 2):
            bs.balken(netz, M, s * 0.07, v, s * (fu + fb / 2 + bs.B), v, holz, vor=bs.VOR_RIEGEL)
        # Strebe im Außenfeld, Kopfstreben über dem Kehlbalken
        bs.balken(netz, M, s * (fu + fb / 2 + bs.B), bs.B, s * (aussen - bs.B / 2), rand(aussen) - 0.25, holz, vor=bs.VOR_STREBE)
        bs.balken(netz, M, s * 0.95, vk + bs.B, s * 0.08, hoch - 1.0, holz, vor=bs.VOR_STREBE)
        o = Oeffnung("fenster", s * fu, fv, fb, fh)
        bs.fenster(netz, M, o, st)
        oeffnungen.append((M, o))


def gaube(netz, st, dach, oeffnungen, x0, x1, y_front, neigung=17):
    """Schleppgaube auf der Gassenseite: Putzkörper, Fachwerkfront mit Fenster, flaches Ziegeldach."""
    tan_g = math.tan(math.radians(neigung))
    z_fuss = dach["oben_vorn"](y_front)
    hoehe = 1.45
    z_kopf = z_fuss + hoehe
    tiefe = hoehe / (dach["tan"] - tan_g)              # bis die Gaube das Hauptdach trifft
    y_hinten = y_front + tiefe
    profil = [(y_front, z_fuss - 0.3), (y_front, z_kopf), (y_hinten, z_kopf + tiefe * tan_g), (y_hinten, z_kopf + tiefe * tan_g - 0.35)]
    netz.prisma(profil, x0, x1, st["Putz"])
    M = bs.wandmatrix(((x0 + x1) / 2, y_front, z_fuss), "vorne")
    o = Oeffnung("fenster", 0, 0.4, 0.8, 0.72)
    bs.fachwerk(netz, M, x1 - x0, hoehe, [o], st, eckstreben=False)
    bs.fenster(netz, M, o, st)
    oeffnungen.append((M, o))
    # Dach der Gaube mit Ziegelreihen
    richtung = Vector((math.cos(math.radians(neigung)), math.sin(math.radians(neigung))))
    normale = Vector((-richtung.y, richtung.x))
    ueber = 0.3
    unten = Vector((y_front, z_kopf)) - richtung * ueber
    laenge = (tiefe + ueber + 0.25) / richtung.x
    ende = unten + richtung * laenge
    oben = bs.zaehne(unten, richtung, normale, laenge, 0.12, reihe=0.4, stufe=0.04)
    profil = [tuple(unten), tuple(ende)] + [tuple(p) for p in reversed(oben)]
    netz.prisma(profil, x0 - 0.18, x1 + 0.18, st["Dachziegel"], m_enden=st["Fachwerk"], kante=bs.reihenkante(st, 0.04))


def fass(netz, st, boden, r=0.33, h=0.95):
    """Regentonne: Achteck aus Holz mit zwei Eisenreifen."""
    def ring(radius, z0, z1, m):
        profil = [(radius * math.cos(math.radians(22.5 + 45 * k)), radius * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
        dreh = Matrix.Translation(boden) @ Matrix.Rotation(math.radians(-90), 4, "Y")   # Prisma-x → Welt-z
        netz.prisma(profil, z0, z1, m, matrix=dreh)
    ring(r, 0, h, st["Holz"])
    for z in (0.2, 0.75):
        ring(r + 0.015, z, z + 0.06, st["Eisen"])


def dachloch(netz, st, dach, seite, x, s, r):
    """Leerstand: ausgebrochene Ziegel – ein ausgefranstes dunkles Loch, darüber zwei Dachlatten."""
    start, rich, nor = dach["traufe_" + seite], dach["richtung_" + seite], dach["normale_" + seite]
    hoehe = dach["dicke"] + 0.07

    def punkt(dx, ds, mehr=0.0):
        p = start + rich * (s + ds) + nor * (hoehe + mehr)
        return Vector((x + dx, p.x, p.y))

    zacken = [1.0, 0.65, 0.95, 0.7, 1.05, 0.6, 0.9, 0.75]
    rand = [punkt(r * z * math.cos(k * math.pi / 4), r * 0.8 * z * math.sin(k * math.pi / 4)) for k, z in enumerate(zacken)]
    normale = Vector((0, nor.x, nor.y))
    netz.vieleck_nach(rand, normale, st["Loch"])
    for ds in (-0.18 * r / 0.4, 0.2 * r / 0.4):
        netz.stab(punkt(-r * 0.95, ds, 0.03), punkt(r * 0.95, ds, 0.03), 0.06, 0.03, st["Holz"], quer=normale)


def buschel(netz, st, fuss, groesse=0.35):
    """Unkraut: zwei gekreuzte, spitze Dreiecke."""
    for w in (20, 110):
        d = Vector((math.cos(math.radians(w)), math.sin(math.radians(w)), 0)) * groesse / 2
        spitze = fuss + Vector((0, 0, groesse * 1.2))
        n = d.cross(Vector((0, 0, 1)))
        netz.vieleck_nach([fuss - d, fuss + d, spitze], n, st["Gras"])
        netz.vieleck_nach([fuss - d, fuss + d, spitze], -n, st["Gras"])


def baugeruest(netz, st, x0, x1, y_innen, y_aussen, ebenen=(2.15, 4.25)):
    """Stangengerüst: zwei Reihen Stangen, Längs- und Querriegel, Bohlen, Geländer, Kreuzstreben, Leiter."""
    holz = st["Geruestholz"]
    n = 4
    xs = [x0 + (x1 - x0) * i / (n - 1) for i in range(n)]
    for x in xs:
        netz.stab((x, y_aussen, 0), (x, y_aussen, 6.3), 0.13, 0.13, holz)
        netz.stab((x, y_innen, 0), (x, y_innen, 5.35), 0.13, 0.13, holz)
    for z in ebenen:
        for y in (y_innen, y_aussen):
            netz.stab((x0 - 0.15, y, z), (x1 + 0.15, y, z), 0.1, 0.1, holz)
        for x in xs:
            netz.stab((x, y_innen + 0.1, z + 0.08), (x, y_aussen - 0.1, z + 0.08), 0.09, 0.09, holz, quer=(1, 0, 0))
        tief = y_innen - y_aussen
        for k in range(3):
            y = y_aussen + tief * (k + 0.5) / 3
            netz.quader(((x0 + x1) / 2, y, z + 0.15), (x1 - x0 + 0.1, tief / 3 - 0.03, 0.05), holz)
        netz.stab((x0 - 0.1, y_aussen - 0.02, z + 1.0), (x1 + 0.1, y_aussen - 0.02, z + 1.0), 0.08, 0.08, holz)
    netz.stab((x0, y_aussen - 0.08, 0.2), (xs[2], y_aussen - 0.08, ebenen[1]), 0.1, 0.06, holz)
    netz.stab((xs[1], y_aussen - 0.12, 0.2), (x1, y_aussen - 0.12, ebenen[1]), 0.1, 0.06, holz)
    # Leiter an der rechten Seite bis zur ersten Ebene
    fuss, kopf = (x1 - 0.45, y_aussen - 0.38, 0), (x1 - 0.45, y_aussen - 0.12, ebenen[0] + 0.9)
    for dx in (-0.22, 0.22):
        netz.stab((fuss[0] + dx, fuss[1], fuss[2]), (kopf[0] + dx, kopf[1], kopf[2]), 0.07, 0.05, holz)
    for k in range(1, 9):
        f = k / 9
        p = Vector(fuss).lerp(Vector(kopf), f)
        netz.stab((p.x - 0.22, p.y, p.z), (p.x + 0.22, p.y, p.z), 0.04, 0.04, holz)
    # Ein paar Balken am Boden
    for k in range(3):
        netz.quader((x0 + 1.2, y_aussen - 0.36 + 0.1 * k, 0.06 + 0.11 * (k == 1)), (2.2, 0.12, 0.12), holz)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=5.0, ausschnitt=20.0, schnell="--schnell" in sys.argv)
    normal = lambda o: o.name not in ("Leerstand", "Geruest")                     # noqa: E731
    print(bs.rendern(NAME, normal))
    print(bs.rendern(NAME + "-leerstand", lambda o: o.name not in ("Geruest", "Wappen", "Fahne")))
    print(bs.rendern(NAME + "-geruest", lambda o: o.name != "Leerstand"))
    if "--schnell" not in sys.argv:
        print(bs.rundum(NAME, normal))


if __name__ == "__main__":
    main()
