"""Werkstatt des Geldleihers – Wechselstube (Stufe 1), Geldleihe (Stufe 2), Bankhaus (Stufe 3). Fläche 16 × 14 m.

    blender -b --python tools/modelle/geldleiher.py        oder
    python tools/modelle/geldleiher.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/geldleiher.glb und Bilder nach docs/modelle/.

Objekte im GLB (wie bei den anderen Werkstätten):
    Haus        das Kaufmannshaus mit Laubengang, in jeder Stufe gleich
    Stufe1      Wechselstube: Hof hinter einer verputzten Mauer mit Tor, Pfandgut (Kisten, Truhen, Fass, ein
                verpfändeter Karren), Linde mit Bank
    Stufe2      Geldleihe: dazu ein Kontor als Anbau mit Giebel zur Gasse, Gaube
    Stufe3      Bankhaus: Kontor, Tresorturm mit eiserner Tür und Kupferhelm, Ziergarten mit Prunkbrunnen,
                Gaube, Dachreiter mit königsblauer Spitze
    Wappen, Fahne, Leerstand, Geruest, Anker_Tuer, Anker_Karren
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Kaufmannshaus links (7,6 × 7,0 m), verputzt wie bisher im Spiel: zur Gasse ein Laubengang mit drei Rundbögen
    aus Sandstein, darunter der Wechseltisch mit grünem Tuch, Münzstapeln, Waage und Truhe (wie bisher im Spiel vor
    dem Tor), dahinter Tür und vergitterte Fenster (wie bisher im Spiel). Darüber das Obergeschoss mit Steinfenstern,
    Ziegeldach mit einem Treppengiebel zur Gasse, Schild mit goldener Münze am Ausleger (wie bisher im Spiel).
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "geldleiher"
FLAECHE = (16.0, 14.0)
STUFEN = ("Wechselstube", "Geldleihe", "Bankhaus")

# Farben nur für diesen Beruf (wie bisher im Spiel)
bs.FARBEN.update({
    "Tuch": (0.16, 0.38, 0.22),             # grünes Tuch des Wechseltischs
    "Koenigsblau": (0.15, 0.28, 0.55),      # Spitze des Dachreiters beim Geldleiher
})
bs.METALL["Koenigsblau"] = 0.5

# ── Maße des Kaufmannshauses ─────────────────────────────────────────────────
X0, X1, Y0, Y1 = -7.2, 0.4, -5.0, 2.0
CX, CY = (X0 + X1) / 2, (Y0 + Y1) / 2
HW = (X1 - X0) / 2
LAUBE = 1.8                                 # Tiefe des Laubengangs
Z_BODEN = 0.15
H_EG, H_OG = 3.4, 3.0
Z_TRAUFE = H_EG + H_OG                      # 6,4
NEIGUNG = 55


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []
    ye = Y0 + LAUBE                         # Erdgeschosswand hinter dem Laubengang

    # ── Laubengang, Erdgeschoss, Obergeschoss ─────────────────────────────────
    haus.quader((CX, (Y0 + ye) / 2 + 0.01, Z_BODEN / 2), (X1 - X0 - 0.04, LAUBE - 0.02, Z_BODEN), st["Sandstein"])   # Boden der Laube
    haus.quader((CX, (ye + Y1 + 0.06) / 2, Z_BODEN / 2), (X1 - X0 + 0.12, Y1 + 0.06 - ye, Z_BODEN), st["Sandstein"])
    haus.quader((CX, (ye + Y1) / 2, (Z_BODEN + H_EG) / 2), (X1 - X0, Y1 - ye, H_EG - Z_BODEN), st["Putz"], ohne=("+z",))
    haus.quader((X0 + 0.2, (Y0 + 0.5 + ye) / 2, (Z_BODEN + H_EG) / 2), (0.4, ye - Y0 - 0.5, H_EG - Z_BODEN), st["Putz"],
                ohne=("+z",))
    haus.quader((CX, CY, H_EG + H_OG / 2), (X1 - X0, Y1 - Y0, H_OG), st["Putz"], ohne=("+z",))
    for z, h, vor in ((H_EG, 0.18, 0.08), (Z_TRAUFE, 0.22, 0.1)):
        bs.gesims(haus, st, X0, X1, Y0, Y1, z, hoehe=h, vor=vor)
    for sx, x in ((-1, X0), (1, X1)):
        for sy, y in ((-1, Y0), (1, Y1)):
            bs.eckquader(haus, st, x, y, sx, sy, H_EG + 0.18, Z_TRAUFE)
        bs.eckquader(haus, st, x, Y1, sx, 1, Z_BODEN, H_EG)
    M_laube = bs.wandmatrix((CX, Y0, 0.0), "vorne")
    laubengang(haus, st, M_laube, X1 - X0, H_EG, [(u, 1.8, 2.8) for u in (-2.4, 0.0, 2.4)])
    laubengang(haus, st, bs.wandmatrix((X1, Y0 + 0.5 + (LAUBE - 0.5) / 2, 0.0), "rechts"), LAUBE - 0.5, H_EG, [(0.0, 0.8, 2.4)])

    F = "fenster"
    M = {"eg vorn": bs.wandmatrix((CX, ye, Z_BODEN), "vorne"),
         "eg rechts": bs.wandmatrix((X1, (ye + Y1) / 2, Z_BODEN), "rechts"),
         "eg links": bs.wandmatrix((X0, (ye + Y1) / 2, Z_BODEN), "links"),
         "eg hinten": bs.wandmatrix((CX, Y1, Z_BODEN), "hinten"),
         "og vorn": bs.wandmatrix((CX, Y0, H_EG), "vorne"), "og rechts": bs.wandmatrix((X1, CY, H_EG), "rechts"),
         "og links": bs.wandmatrix((X0, CY, H_EG), "links"), "og hinten": bs.wandmatrix((CX, Y1, H_EG), "hinten")}
    gitter = dict(v=0.9, b=0.8, h=1.2)
    og = dict(v=0.65, b=0.9, h=1.4)
    tuer = Oeffnung("tuer", -1.4, 0.0, 1.3, 2.6)
    bs.portal(haus, M["eg vorn"], tuer, st)
    oeffnungen.append((M["eg vorn"], tuer))
    for name, liste, kw in (("eg vorn", (0.8, 2.6), dict(gitter=True)), ("eg rechts", (0.0,), dict(gitter=True)),
                            ("eg links", (-1.2, 1.2), dict(gitter=True)), ("eg hinten", (-1.6,), dict(gitter=True)),
                            ("og vorn", (-2.4, 0.0, 2.4), dict(verdachung=True)), ("og rechts", (-2.0, 0.6, 2.4), {}),
                            ("og links", (-2.0, 2.0), {}), ("og hinten", (-2.0, 2.0), {})):
        for u in liste:
            o = Oeffnung(F, u, **(gitter if name.startswith("eg") else og))
            bs.gewaende(haus, M[name], o, st, **kw)
            oeffnungen.append((M[name], o))
    hinten = Oeffnung("tuer", 1.6, 0.0, 1.0, 2.2)
    bs.tuer(haus, M["eg hinten"], hinten, st)
    haus.quader((1.6, -0.03, 2.3), (1.4, 0.1, 0.2), st["Sandstein"], M["eg hinten"], ohne=("+y",))
    oeffnungen.append((M["eg hinten"], hinten))
    wechseltisch(haus, st, (CX + 1.7, Y0 + 0.95, Z_BODEN))
    muenzschild(haus, st, M["og vorn"], -3.3, 0.25)

    # ── Ziegeldach mit dem Giebel zur Gasse, Treppengiebel vorn und hinten, Schornstein ──
    drehung = Matrix.Translation((CX, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(haus, st, Y0, Y1, -HW, HW, Z_TRAUFE + 0.22, NEIGUNG, traufe=0.3, ortgang=-0.25, enden="Putz",
                         first_dicke=0.2, matrix=drehung, **bs.ZIEGEL)
    oben = 0.0
    for y, seite, vorne in ((Y0, "vorne", True), (Y1, "hinten", False)):
        Mg = bs.wandmatrix((CX, y, Z_TRAUFE), seite)
        oben = treppengiebel(haus, st, Mg, dach, oeffnungen, vorne)
    xs, ys = CX - 1.6, Y1 - 2.0
    z0 = dach["oben_hinten"](-(xs - CX)) - 0.3
    haus.quader((xs, ys, (z0 + dach["z_first"] + 0.8) / 2), (0.65, 0.65, dach["z_first"] + 0.8 - z0), st["Putz"])
    haus.quader((xs, ys, dach["z_first"] + 0.86), (0.85, 0.85, 0.12), st["Sandstein"])

    # ── Wappen im Giebel, Fahne auf dem vorderen Giebel ───────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, bs.wandmatrix((CX, Y0, Z_TRAUFE), "vorne"), 0.0, 1.45, st, groesse=0.6, rahmen="Sandstein")
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (CX, Y0 + 0.2, Z_TRAUFE + oben), hoehe=2.2)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    wechselstube(stufe1, st)
    stufe2 = bs.Netz("Stufe2")
    geldleihe(stufe2, st)
    bs.gaube(stufe2, st, dach, [], x0=-0.2, x1=1.2, y_front=-HW + 1.2, art="stein", koerper="Putz")
    stufe3 = bs.Netz("Stufe3")
    bankhaus(stufe3, st)
    bs.gaube(stufe3, st, dach, [], x0=-0.2, x1=1.2, y_front=-HW + 1.2, art="stein", koerper="Putz")
    bs.dachreiter(stufe3, st, Vector((CX, -1.6, dach["z_first"])), spitze="Koenigsblau", ecken="Sandstein")

    # ── Leerstand, Gerüst, Anker ──────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (Mo, o) in enumerate(oeffnungen):
        bs.bretter(leer, Mo, o, st, i)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.2, Y0 - 1.3), (X1 - 0.2, Y0 - 0.15), (X0 - 0.15, -1.0), (X1 + 0.15, 1.4)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=X1 + 0.15, y_innen=Y0 - 0.3, y_aussen=Y0 - 1.2,
                  ebenen=(2.6, 5.2), hoehe_innen=8.0, hoehe_aussen=8.6, felder=3)
    anker = [bs.anker("Anker_Tuer", (CX + tuer.u, ye - 0.3, Z_BODEN)),
             bs.anker("Anker_Karren", (CX, Y0 - 1.6, 0.0))]

    objekte = [haus.objekt(), stufe1.objekt(), stufe2.objekt(), stufe3.objekt(), wappen.objekt(), fahne.objekt(),
               leer.objekt(), geruest.objekt()] + anker
    for o in objekte:
        if o.name in ("Stufe2", "Stufe3", "Leerstand", "Geruest"):
            bs.zustand(o)
        elif o.name == "Stufe1":
            o["zustand"] = True
    return objekte


# ─────────────────────────────────────────────────────────────────────────────
#  Teile des Kaufmannshauses
# ─────────────────────────────────────────────────────────────────────────────

def laubengang(netz, st, M, L, H, boegen, dicke=0.5, teile=6):
    """Bogenwand aus Sandstein: eine Scheibe über die ganze Wand, unten Rundbögen ausgespart (u, Breite, Scheitelhöhe)."""
    halb = L / 2
    umriss = [(-halb, 0.0)]
    for u, b, h in sorted(boegen):
        r = b / 2
        k = h - r
        umriss += [(u - r, 0.0), (u - r, k)]
        umriss += [(u + r * math.cos(math.pi - math.pi * i / teile), k + r * math.sin(math.pi - math.pi * i / teile))
                   for i in range(1, teile)]
        umriss += [(u + r, k), (u + r, 0.0)]
    umriss += [(halb, 0.0), (halb, H), (-halb, H)]
    bs.platte(netz, M, umriss, 0.0, dicke, st["Sandstein"])
    for u, b, h in boegen:                                         # Schlussstein und Kämpfer
        netz.quader((u, -0.04, h + 0.12), (0.26, 0.08, 0.3), st["Sandstein"], M, ohne=("+y",))
        for s in (-1, 1):
            netz.quader((u + s * (b / 2 + 0.06), -0.03, h - b / 2), (0.32, 0.06, 0.14), st["Sandstein"], M, ohne=("+y",))


def treppengiebel(netz, st, M, dach, oeffnungen, vorne, stufen=5, oben_halb=0.45, dicke=0.4):
    """Treppengiebel aus Putz über der Traufe: Stufen ragen über das Dach, darauf Sandsteinplatten; vorn zwei Fenster
    und ein kleines oben (dazwischen sitzt das Wappen), hinten zwei. Gibt die Höhe über der Traufe zurück."""
    th = math.radians(NEIGUNG)
    tan = math.tan(th)
    z_dach0 = 0.22 + dach["dicke"] / math.cos(th)

    def dach_bei(a):
        return z_dach0 + (HW - a) * tan

    grenzen = [HW - k * (HW - oben_halb) / stufen for k in range(stufen + 1)]
    hoehen = [dach_bei(grenzen[k + 1]) + 0.35 for k in range(stufen)]
    spitze = dach_bei(0) + 0.75
    rechts = [(HW, 0)]
    for k in range(stufen):
        rechts += [(grenzen[k], hoehen[k]), (grenzen[k + 1], hoehen[k])]
    rechts += [(oben_halb, spitze)]
    bs.platte(netz, M, rechts + [(-u, v) for u, v in reversed(rechts)], 0, dicke, st["Putz"])
    for k in range(stufen):
        b = grenzen[k] - grenzen[k + 1]
        for s in (-1, 1):
            netz.quader((s * (grenzen[k] + grenzen[k + 1]) / 2, dicke / 2, hoehen[k] + 0.05), (b + 0.12, dicke + 0.12, 0.1),
                        st["Sandstein"], M)
    netz.quader((0, dicke / 2, spitze + 0.06), (2 * oben_halb + 0.14, dicke + 0.14, 0.12), st["Sandstein"], M)
    liste = [Oeffnung("fenster", -1.35, 0.6, 0.7, 1.05), Oeffnung("fenster", 1.35, 0.6, 0.7, 1.05)]
    if vorne:
        liste.append(Oeffnung("fenster", 0.0, 2.6, 0.55, 0.85))
    for o in liste:
        bs.gewaende(netz, M, o, st)
        oeffnungen.append((M, o))
    return spitze + 0.12


def wechseltisch(netz, st, fuss):
    """Wechseltisch mit grünem Tuch, Münzstapeln und Waage, dahinter eine Geldtruhe und ein Schemel
    (wie bisher im Spiel vor dem Tor)."""
    x, y, z = fuss
    for dx in (-1.0, 1.0):
        netz.quader((x + dx, y, z + 0.43), (0.12, 0.8, 0.86), st["Holz"])
    netz.quader((x, y, z + 0.91), (2.4, 1.0, 0.1), st["Tuch"])
    for dx, h in ((-0.8, 0.28), (-0.55, 0.16), (-0.3, 0.22), (-0.05, 0.1)):
        bs.achteck(netz, st, Vector((x + dx, y - 0.1, z + 0.96)), 0.1, 0.0, h, "Gold", n=8)
    w = Vector((x + 0.65, y, z + 0.96))
    netz.quader(w + Vector((0, 0, 0.32)), (0.06, 0.06, 0.64), st["Eisen"])
    netz.quader(w + Vector((0, 0, 0.64)), (0.8, 0.04, 0.04), st["Eisen"])
    for dx in (-0.37, 0.37):
        netz.stab(w + Vector((dx, 0, 0.62)), w + Vector((dx, 0, 0.42)), 0.015, 0.015, st["Eisen"])
        bs.achteck(netz, st, w + Vector((dx, 0, 0.38)), 0.13, 0.0, 0.04, "Gold", n=8)
    t = Vector((x - 0.2, y + 0.75, z))
    netz.quader(t + Vector((0, 0, 0.3)), (1.0, 0.5, 0.6), st["Holz"])
    netz.quader(t + Vector((0, 0, 0.62)), (1.04, 0.54, 0.06), st["Eisen"])
    for dx in (-0.35, 0.35):
        netz.quader(t + Vector((dx, -0.26, 0.3)), (0.06, 0.03, 0.6), st["Eisen"])
    bs.fass(netz, st, Vector((x + 0.9, y + 0.65, z)), r=0.2, h=0.45, reifen=(), stoff="Holz")


def muenzschild(netz, st, M, u, v):
    """Schild mit goldener Münze (wie bisher im Spiel) an einem Eisenarm über dem Laubengang."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v + 0.3)), M @ Vector((u, -1.0, v + 0.3)), 0.05, 0.05, eisen)
    netz.stab(M @ Vector((u, 0, v + 0.85)), M @ Vector((u, -0.7, v + 0.32)), 0.04, 0.04, eisen)
    netz.stab(M @ Vector((u, -0.75, v + 0.3)), M @ Vector((u, -0.75, v + 0.05)), 0.02, 0.02, eisen)
    n = 16
    mitte = M @ Vector((u, -0.75, v - 0.45))
    t = Matrix.Translation(mitte) @ M.to_3x3().to_4x4() @ Matrix.Rotation(math.radians(90), 4, "Z")   # Prisma-x → zur Gasse
    netz.prisma([(0.48 * math.cos(2 * math.pi * k / n), 0.48 * math.sin(2 * math.pi * k / n)) for k in range(n)],
                -0.04, 0.04, st["Gold"], matrix=t)
    netz.prisma([(-0.12, -0.18), (0.12, -0.18), (0.12, 0.18), (-0.12, 0.18)], -0.06, -0.04, st["Ocker"], matrix=t)  # Prägung


def putzmauer(netz, st, punkte, hoehe=2.0, dicke=0.4):
    """Verputzte Hofmauer mit Sandsteinabdeckung; Abschnitte stoßen an den Ecken stumpf aneinander."""
    pts = [Vector((x, y, 0)) for x, y in punkte]
    for i in range(len(pts) - 1):
        d = (pts[i + 1] - pts[i]).normalized()
        a = pts[i] + (d * dicke / 2 if i > 0 else Vector())
        b = pts[i + 1] + (d * dicke / 2 if i < len(pts) - 2 else Vector())
        netz.stab(a + Vector((0, 0, hoehe / 2)), b + Vector((0, 0, hoehe / 2)), dicke, hoehe, st["Putz"], quer=(0, 0, 1))
        a_kappe = a + (d * 0.06 if i > 0 else Vector())          # Abdeckungen überdecken sich an den Ecken nicht
        netz.stab(a_kappe + Vector((0, 0, hoehe + 0.06)), b + Vector((0, 0, hoehe + 0.06)), dicke + 0.1, 0.12, st["Sandstein"],
                  quer=(0, 0, 1), ohne=("-z",))


def hoftor(netz, st, x0, x1, y, h=2.2):
    """Zwei Pfeiler aus Sandstein mit Kugeln, dazwischen zwei offene Torflügel in der Hausfarbe."""
    for x in (x0, x1):
        netz.quader((x, y, h / 2), (0.5, 0.5, h), st["Sandstein"])
        bs.achteck(netz, st, Vector((x, y, h)), 0.2, 0.0, 0.3, "Sandstein", n=8)
    breite = (x1 - x0 - 0.5) / 2
    for x, s in ((x0 + 0.25, 1), (x1 - 0.25, -1)):
        t = Matrix.Translation((x, y, 1.0)) @ Matrix.Rotation(math.radians(90 if s > 0 else 90), 4, "Z")
        netz.quader((breite / 2, 0, 0), (breite, 0.06, 1.8), st["Hausfarbe"], t)


def kiste(netz, st, fuss, masse=(0.8, 0.6, 0.55), dreh=0.0):
    t = Matrix.Translation(Vector(fuss)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    netz.quader((0, 0, masse[2] / 2), masse, st["Holz"], t)
    for dx in (-masse[0] * 0.3, masse[0] * 0.3):
        netz.quader((dx, 0, masse[2] / 2), (0.06, masse[1] + 0.02, masse[2] + 0.02), st["Eisen"], t)


def pfandgut(netz, st, mitte):
    """Verpfändetes: Kisten, eine beschlagene Truhe, Fass, ein Karren mit Plane."""
    x, y = mitte
    kiste(netz, st, (x, y, 0))
    kiste(netz, st, (x + 0.1, y + 0.7, 0), (0.7, 0.55, 0.5), 10)
    kiste(netz, st, (x + 0.05, y + 0.3, 0.55), (0.6, 0.5, 0.45), -8)
    bs.fass(netz, st, Vector((x + 1.0, y - 0.2, 0)), r=0.32, h=0.9)
    t = Vector((x + 1.1, y + 0.8, 0))
    netz.quader(t + Vector((0, 0, 0.3)), (0.9, 0.5, 0.6), st["Hausfarbe"])
    netz.quader(t + Vector((0, 0, 0.64)), (0.94, 0.54, 0.08), st["Eisen"])
    # Karren mit Plane
    k = Vector((x + 0.4, y + 2.6, 0))
    netz.quader(k + Vector((0, 0, 0.75)), (2.0, 1.1, 0.1), st["Holz alt"])
    netz.quader(k + Vector((0, 0, 1.05)), (1.8, 1.0, 0.5), st["Lumpen"])
    profil = [(0.42 * math.cos(math.radians(30 * j)), 0.42 * math.sin(math.radians(30 * j))) for j in range(12)]
    for dy in (-0.62, 0.62):
        netz.prisma(profil, -0.04, 0.04, st["Holz"], matrix=Matrix.Translation(k + Vector((0, dy, 0.42))) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    netz.stab(k + Vector((1.0, 0, 0.7)), k + Vector((2.3, 0, 0.1)), 0.08, 0.08, st["Holz alt"])


def kontor(netz, st):
    """Kontor als Anbau rechts: zweigeschossig verputzt mit Eckquadern, Giebel zur Gasse, Tür und Steinfenster."""
    x0, x1, y0, y1 = X1, 3.8, -2.6, 1.6
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    breite = x1 - x0
    h1, h2 = 3.0, 2.4
    netz.quader((cx + 0.03, cy, 0.1), (breite + 0.06, y1 - y0 + 0.12, 0.2), st["Sandstein"], ohne=("-x",))
    netz.quader((cx, cy, 0.2 + (h1 + h2) / 2), (breite, y1 - y0, h1 + h2), st["Putz"], ohne=("+z", "-x"))
    bs.gesims(netz, st, x0 + 0.08, x1, y0, y1, 0.2 + h1, hoehe=0.14, vor=0.07)
    for y, sy in ((y0, -1), (y1, 1)):
        bs.eckquader(netz, st, x1, y, 1, sy, 0.2, 0.2 + h1 + h2)
    M_v = bs.wandmatrix((cx, y0, 0.2), "vorne")
    M_r = bs.wandmatrix((x1, cy, 0.2), "rechts")
    tuer = Oeffnung("tuer", -0.6, 0.0, 1.0, 2.2)
    bs.tuer(netz, M_v, tuer, st)
    netz.quader((tuer.u, -0.03, tuer.h + 0.12), (1.4, 0.1, 0.24), st["Sandstein"], M_v, ohne=("+y",))
    netz.quader((cx + tuer.u, y0 - 0.25, 0.105), (1.3, 0.5, 0.21), st["Sandstein"])
    bs.gewaende(netz, M_v, Oeffnung("fenster", 0.9, 0.9, 0.7, 1.2), st, gitter=True)
    for u in (-0.7, 0.8):
        bs.gewaende(netz, M_v, Oeffnung("fenster", u, h1 + 0.5, 0.75, 1.25), st)
    for u in (-1.2, 1.0):
        bs.gewaende(netz, M_r, Oeffnung("fenster", u, 0.9, 0.7, 1.2), st, gitter=True)
        bs.gewaende(netz, M_r, Oeffnung("fenster", u, h1 + 0.5, 0.75, 1.25), st)
    drehung = Matrix.Translation((cx, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(netz, st, y0, y1, -breite / 2, breite / 2, 0.2 + h1 + h2, 50, traufe=0.3, ortgang=0.25,
                         enden="Putz", matrix=drehung, **bs.ZIEGEL)
    hoch = breite / 2 * dach["tan"]
    Mg = bs.wandmatrix((cx, y0, 0.2 + h1 + h2), "vorne")
    bs.platte(netz, Mg, [(-breite / 2, 0), (breite / 2, 0), (0, hoch)], 0, 0.3, st["Putz"])
    bs.gewaende(netz, Mg, Oeffnung("fenster", 0.0, 0.3, 0.55, 0.8), st)


def tresorturm(netz, st, x0=4.8, y0=3.0, b=2.6, h=7.4):
    """Tresorturm: Haustein mit Eckquadern und Gesims, kleine vergitterte Fenster, eiserne Tür mit Nieten,
    Zeltdach aus grünem Kupfer mit goldenem Knauf."""
    x1, y1 = x0 + b, y0 + b
    cx, cy = x0 + b / 2, y0 + b / 2
    netz.quader((cx, cy, 0.2), (b + 0.12, b + 0.12, 0.4), st["Bruchstein dunkel"])
    netz.quader((cx, cy, 0.4 + (h - 0.4) / 2), (b, b, h - 0.4), st["Haustein"], ohne=("+z",))
    bs.gesims(netz, st, x0, x1, y0, y1, h - 0.22, hoehe=0.22, vor=0.1)
    for sx, x in ((-1, x0), (1, x1)):
        for sy, y in ((-1, y0), (1, y1)):
            bs.eckquader(netz, st, x, y, sx, sy, 0.4, h - 0.22)
    M_v = bs.wandmatrix((cx, y0, 0.0), "vorne")
    M_r = bs.wandmatrix((x1, cy, 0.0), "rechts")
    M_l = bs.wandmatrix((x0, cy, 0.0), "links")
    for Mw in (M_v, M_r):
        bs.fugen(netz, Mw, b - 0.02, 0.4, h - 0.22, st)
    netz.quader((0.0, -0.02, 0.4 + 1.05), (1.0, 0.06, 2.1), st["Eisen"], M_v, ohne=("+y",))
    for zu in (0.5, 1.0, 1.5, 2.0):
        for uu in (-0.4, 0.0, 0.4):
            netz.quader((uu, -0.06, 0.4 + zu), (0.07, 0.03, 0.07), st["Bruchstein dunkel"], M_v, ohne=("+y",))
    netz.quader((0.0, -0.03, 0.4 + 2.25), (1.4, 0.08, 0.22), st["Sandstein"], M_v, ohne=("+y",))
    for Mw, liste in ((M_v, (4.0, 5.8)), (M_r, (2.6, 4.6)), (M_l, (5.8,))):
        for v in liste:
            bs.gewaende(netz, Mw, Oeffnung("fenster", 0.0, v, 0.45, 0.7), st, gitter=True)
    gipfel = Vector((cx, cy, h + 3.0))
    ecken = [Vector((cx + sx * (b / 2 + 0.3), cy + sy * (b / 2 + 0.3), h - 0.1)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        p, q = ecken[i], ecken[(i + 1) % 4]
        netz.vieleck_nach([p, q, gipfel], (p + q) / 2 - Vector((cx, cy, h)), st["Kupfer"])
        netz.vieleck_nach([p, q, q + Vector((0, 0, -0.12)), p + Vector((0, 0, -0.12))], (p + q) / 2 - Vector((cx, cy, h)),
                          st["Kupfer"])
    bs.achteck(netz, st, gipfel + Vector((0, 0, -0.1)), 0.14, 0.0, 0.35, "Gold", n=8)


def prunkbrunnen(netz, st, mitte):
    """Prunkbrunnen: achteckiges Becken aus Sandstein mit Wasser, Säule mit Schale und goldener Kugel."""
    m = Vector(mitte)
    bs.achteck(netz, st, m, 1.15, 0.0, 0.15, "Bruchstein dunkel")
    bs.achteck(netz, st, m, 1.0, 0.15, 0.7, "Sandstein")
    bs.rund_oben(netz, st, m, 0.86, 0.703, "Wasser")
    bs.achteck(netz, st, m, 0.16, 0.7, 2.0, "Sandstein")
    bs.achteck(netz, st, m, 0.5, 2.0, 2.18, "Sandstein")
    bs.rund_oben(netz, st, m, 0.42, 2.183, "Wasser")
    bs.achteck(netz, st, m + Vector((0, 0, 2.18)), 0.12, 0.0, 0.3, "Gold", n=8)


def buchsbeet(netz, st, x0, x1, y0, y1, saat=0):
    """Beet mit niedriger Buchshecke als Rand und Blumen in der Mitte."""
    for y in (y0, y1):
        netz.quader(((x0 + x1) / 2, y, 0.2), (x1 - x0, 0.3, 0.4), st["Laub"])
    for x in (x0, x1):
        netz.quader((x, (y0 + y1) / 2, 0.2), (0.3, y1 - y0 - 0.3, 0.4), st["Laub"])
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, 0.04), (x1 - x0 - 0.3, y1 - y0 - 0.3, 0.08), st["Lehm"], ohne=("-z",))
    zufall = random.Random(saat)
    for k in range(6):
        p = Vector((zufall.uniform(x0 + 0.4, x1 - 0.4), zufall.uniform(y0 + 0.4, y1 - 0.4), 0.08))
        bs.buschel(netz, st, p, groesse=0.35)
        netz.quader(p + Vector((0, 0, 0.42)), (0.12, 0.12, 0.1), st["Gold"] if k % 2 else st["Hausfarbe"])


# ─────────────────────────────────────────────────────────────────────────────
#  Stufen
# ─────────────────────────────────────────────────────────────────────────────

def mauer_und_tor(netz, st):
    putzmauer(netz, st, [(X1 + 0.1, -4.6), (4.9, -4.6)])
    hoftor(netz, st, 5.15, 6.65, -4.6)
    putzmauer(netz, st, [(6.9, -4.6), (7.7, -4.6), (7.7, 6.7), (-7.7, 6.7), (-7.7, Y1 + 0.3)])


def wechselstube(netz, st):
    """Stufe 1: Hof hinter der Mauer mit Pfandgut, Linde mit Bank, Tor zur Gasse."""
    pfandgut(netz, st, (2.0, -3.4))
    bs.linde(netz, st, (5.6, 3.6, 0), hoehe=5.8, r=1.7, saat=41)
    netz.quader((4.3, 3.6, 0.45), (0.3, 1.6, 0.06), st["Holz"])
    for y in (3.0, 4.2):
        netz.quader((4.3, y, 0.21), (0.25, 0.08, 0.42), st["Holz alt"])
    mauer_und_tor(netz, st)


def geldleihe(netz, st):
    """Stufe 2: Kontor als Anbau, Pfandgut im hinteren Hof, Linde, Mauer mit Tor."""
    kontor(netz, st)
    pfandgut(netz, st, (2.4, 3.0))
    bs.linde(netz, st, (6.0, 1.4, 0), hoehe=5.8, r=1.6, saat=42)
    mauer_und_tor(netz, st)


def bankhaus(netz, st):
    """Stufe 3: Kontor, Tresorturm im hinteren Hof, Ziergarten mit Prunkbrunnen und Buchsbeeten."""
    kontor(netz, st)
    tresorturm(netz, st)
    prunkbrunnen(netz, st, (5.8, -1.0, 0))
    buchsbeet(netz, st, 4.4, 7.2, -3.9, -2.6, saat=43)
    buchsbeet(netz, st, 4.4, 7.2, 0.6, 1.9, saat=44)
    mauer_und_tor(netz, st)


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
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=5.0, ausschnitt=28.0, schnell=schnell)
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
