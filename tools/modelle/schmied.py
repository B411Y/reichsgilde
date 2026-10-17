"""Werkstatt des Schmieds – Gießerei (Stufe 1), Schmiede (Stufe 2), Goldschmiede (Stufe 3). Fläche 16 × 14 m.

    blender -b --python tools/modelle/schmied.py        oder
    python tools/modelle/schmied.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/schmied.glb und Bilder nach docs/modelle/.

Objekte im GLB:
    Haus        die Schmiedehalle, in jeder Stufe gleich
    Stufe1      Gießerei: offener Schuppen mit Esse, Amboss, Kohle, Formkästen, Bretterzaun
    Stufe2      Schmiede: Anbau mit zweiter Esse, Gaube, Notstall für den Hufbeschlag, Zaun
    Stufe3      Goldschmiede: Anbau mit Obergeschoss und Laden, Hinterhaus, Gaube,
                Dachreiter mit goldener Spitze, Hofmauer mit Tor, goldenes Schild
    Wappen, Fahne
    Leerstand   Bretter vor Tor und Fenstern der Halle (verkaufte Werkstatt)
    Geruest     Gerüst vor der Halle (Ausbau)
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.
Das Material „Glut“ (Esse, Feuerschein im Fenster) leuchtet – das Spiel regelt die Stärke.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Halle links (9 × 6,5 m): Erdgeschoss aus Bruchstein mit Rundbogentor, Fachwerk-Obergeschoss,
    Ziegeldach mit großer Esse. Rechts daneben wächst die Werkstatt mit der Stufe.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "schmied"
FLAECHE = (16.0, 14.0)
STUFEN = ("Gießerei", "Schmiede", "Goldschmiede")

# ── Maße der Halle ───────────────────────────────────────────────────────────
X0, X1, Y0, Y1 = -7.2, 1.8, -5.0, 1.5      # Außenkanten der Halle
CX, CY = (X0 + X1) / 2, (Y0 + Y1) / 2
Z_SOCKEL, H_EG, H_OG = 0.3, 3.3, 2.4
Z_OG = Z_SOCKEL + H_EG
Z_TRAUFE = Z_OG + H_OG                      # 6,0
UEBER = 0.045
# Anbau rechts (Stufe 2 und 3)
AX0, AX1, AY0, AY1 = 2.0, 7.4, -4.6, 1.5


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []          # nur die der Halle: im Leerstand vernagelt

    # ── Halle: Sockel, Bruchstein-Erdgeschoss, Fachwerk-Obergeschoss ─────────
    haus.quader((CX, CY, Z_SOCKEL / 2), (X1 - X0 + 0.12, Y1 - Y0 + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    haus.quader((CX, CY, Z_SOCKEL + H_EG / 2), (X1 - X0, Y1 - Y0, H_EG), st["Bruchstein"], ohne=("+z",))
    haus.quader((CX, CY, Z_OG + H_OG / 2), (X1 - X0, Y1 - Y0, H_OG), st["Putz"])
    for sx, x in ((-1, X0), (1, X1)):
        for sy, y in ((-1, Y0), (1, Y1)):
            bs.eckquader(haus, st, x, y, sx, sy, Z_SOCKEL, Z_OG, stoff="Bruchstein dunkel")
    M = {s: bs.wandmatrix(p, s) for s, p in (("vorne", (CX, Y0, Z_SOCKEL)), ("rechts", (X1, CY, Z_SOCKEL)),
                                             ("hinten", (CX, Y1, Z_SOCKEL)), ("links", (X0, CY, Z_SOCKEL)))}
    L = {"vorne": X1 - X0, "hinten": X1 - X0, "rechts": Y1 - Y0, "links": Y1 - Y0}
    for s in M:
        bs.fugen(haus, M[s], L[s] - 0.02, 0, H_EG, st, abstand=0.45)

    def steinfenster(s, o, **art):
        bs.gewaende(haus, M[s], o, st, stein="Bruchstein dunkel", **art)
        oeffnungen.append((M[s], o))

    tor = Oeffnung("tuer", -1.4, 0.0, 2.4, 2.95)
    bs.portal(haus, M["vorne"], tor, st)
    oeffnungen.append((M["vorne"], tor))
    steinfenster("vorne", Oeffnung("fenster", 2.0, 1.0, 1.0, 1.1), glas="Glut", gitter=True)   # Feuerschein der Esse
    steinfenster("vorne", Oeffnung("fenster", -3.6, 1.1, 0.85, 1.0), gitter=True)
    steinfenster("links", Oeffnung("fenster", -1.2, 1.1, 0.85, 1.0), gitter=True)
    steinfenster("links", Oeffnung("fenster", 1.4, 1.1, 0.85, 1.0), gitter=True)
    steinfenster("rechts", Oeffnung("fenster", 1.6, 1.1, 0.85, 1.0), glas="Glut", gitter=True)
    steinfenster("hinten", Oeffnung("fenster", 2.6, 1.1, 0.85, 1.0), gitter=True)
    hintertuer = Oeffnung("tuer", -1.2, 0.0, 1.0, 2.1)
    bs.tuer(haus, M["hinten"], hintertuer, st)
    haus.quader((-1.2, -0.03, 2.2), (1.4, 0.1, 0.2), st["Bruchstein dunkel"], M["hinten"], ohne=("+y",))
    oeffnungen.append((M["hinten"], hintertuer))
    # Schwelle zwischen Stein und Fachwerk
    haus.quader((CX, CY, Z_OG - 0.06), (X1 - X0 + 0.1, Y1 - Y0 + 0.1, 0.14), st["Fachwerk"], ohne=("+z",))

    # Obergeschoss in Fachwerk
    fo = dict(v=0.55, b=0.85, h=1.1)
    Mo = {s: bs.wandmatrix(p, s) for s, p in (("vorne", (CX, Y0, Z_OG)), ("rechts", (X1, CY, Z_OG)),
                                              ("hinten", (CX, Y1, Z_OG)), ("links", (X0, CY, Z_OG)))}
    for s, us in (("vorne", (-3.0, -0.2, 2.8)), ("hinten", (-2.6, 0.4, 2.9)), ("links", (-1.5, 1.5)), ("rechts", (0.0,))):
        lang = L[s] + (2 * UEBER if s in ("vorne", "hinten") else 0)
        liste = [Oeffnung("fenster", u, **fo) for u in us]
        bs.fachwerk(haus, Mo[s], lang, H_OG, liste, st, kreuze=s == "vorne")
        for o in liste:
            bs.fenster(haus, Mo[s], o, st)
            oeffnungen.append((Mo[s], o))

    # ── Dach, Giebel, große Esse ──────────────────────────────────────────────
    dach = bs.satteldach(haus, st, X0, X1, Y0, Y1, Z_TRAUFE, 50, traufe=0.4, ortgang=0.35, **bs.ZIEGEL)
    hoch = (Y1 - Y0) / 2 * dach["tan"]
    for x, seite in ((X1, "rechts"), (X0, "links")):
        bs.fachwerkgiebel(haus, st, bs.wandmatrix((x, CY, Z_TRAUFE), seite), Y1 - Y0, hoch, [])
    esse(haus, st, Vector((0.4, dach["y_first"] + 0.2, 0)), dach["z_first"] + 1.5, z_fuss=dach["z_first"] - 1.6, breite=1.15)

    # Zunftzeichen am Tor: Amboss am Ausleger
    ausleger(haus, st, M["vorne"], 0.45, 2.75)

    # ── Wappen und Fahne ──────────────────────────────────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, Mo["vorne"], 1.3, 1.1, st, groesse=0.9)
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (X0 + 0.9, dach["y_first"], dach["z_first"] - 0.3), hoehe=2.6, richtung=1)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    giesserei(stufe1, st)
    stufe2 = bs.Netz("Stufe2")
    anbau(stufe2, st, obergeschoss=False)
    bs.gaube(stufe2, st, dach, [], x0=-5.6, x1=-4.0, y_front=Y0 + 1.1)
    notstall(stufe2, st, Vector((5.0, 4.6, 0)))
    kohlehaufen(stufe2, st, Vector((-5.0, 4.3, 0)), 2.2, 1.6, 0.8)
    bs.fass(stufe2, st, Vector((2.6, 2.4, 0)), r=0.3, h=0.8)
    bs.zaun(stufe2, st, [(7.8, AY1 + 0.2), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])
    amboss(stufe2, st, Vector((5.9, -5.7, 0)))
    stufe3 = bs.Netz("Stufe3")
    anbau(stufe3, st, obergeschoss=True)
    bs.gaube(stufe3, st, dach, [], x0=-5.6, x1=-4.0, y_front=Y0 + 1.1)
    dachreiter(stufe3, st, Vector((-2.4, dach["y_first"], dach["z_first"])))
    hinterhaus(stufe3, st)
    hofmauer(stufe3, st)

    # ── Leerstand und Gerüst (an der Halle) ───────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (Mx, o) in enumerate(oeffnungen):
        bs.bretter(leer, Mx, o, st, i)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.0, Y0 - 1.0), (X1 - 0.3, Y0 - 0.16), (X0 - 0.15, -2.0), (0.5, Y0 - 0.15)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=X1 + 0.15, y_innen=Y0 - 0.3, y_aussen=Y0 - 1.2,
                  ebenen=(2.4, 4.6), hoehe_innen=5.6, hoehe_aussen=7.0, felder=4)

    objekte = [haus.objekt(), stufe1.objekt(), stufe2.objekt(), stufe3.objekt(), wappen.objekt(), fahne.objekt(),
               leer.objekt(), geruest.objekt()]
    for o in objekte:
        if o.name in ("Stufe2", "Stufe3", "Leerstand", "Geruest"):
            bs.zustand(o)
        elif o.name == "Stufe1":
            o["zustand"] = True        # Stufe: genau eine sichtbar, Stufe1 ist die Vorgabe
    return objekte


# ─────────────────────────────────────────────────────────────────────────────
#  Teile des Schmieds
# ─────────────────────────────────────────────────────────────────────────────

def esse(netz, st, mitte, z_kopf, z_fuss, breite=1.0, tiefe=0.9):
    """Gemauerter Schornstein der Esse: oben ein Rand, innen die Glut (leuchtet bei der Arbeit)."""
    x, y = mitte.x, mitte.y
    netz.quader((x, y, (z_fuss + z_kopf) / 2), (breite, tiefe, z_kopf - z_fuss), st["Bruchstein dunkel"], ohne=("+z",))
    r = 0.14
    netz.vieleck([(x - breite / 2, y - tiefe / 2, z_kopf - 0.02), (x + breite / 2, y - tiefe / 2, z_kopf - 0.02),
                  (x + breite / 2, y + tiefe / 2, z_kopf - 0.02), (x - breite / 2, y + tiefe / 2, z_kopf - 0.02)], st["Glut"])
    for dx, dy, bx, by in ((0, -(tiefe - r) / 2, breite + 0.1, r), (0, (tiefe - r) / 2, breite + 0.1, r),
                           (-(breite - r) / 2, 0, r, tiefe - 2 * r), ((breite - r) / 2, 0, r, tiefe - 2 * r)):
        netz.quader((x + dx, y + dy, z_kopf + 0.12), (bx, by, 0.28), st["Bruchstein"])


def ausleger(netz, st, M, u, v):
    """Zunftzeichen: schmiedeeiserner Arm mit einem Amboss als Schild."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -1.1, v)), 0.05, 0.05, eisen)
    netz.stab(M @ Vector((u, 0, v - 0.5)), M @ Vector((u, -0.8, v - 0.02)), 0.04, 0.04, eisen)
    for d in (-0.85, -0.25):
        netz.stab(M @ Vector((u, d, v)), M @ Vector((u, d, v - 0.25)), 0.02, 0.02, eisen)
    umriss = [(-0.55, 0.0), (0.0, 0.0), (0.0, -0.1), (-0.12, -0.18), (-0.12, -0.32), (0.05, -0.38), (0.05, -0.45),
              (-0.55, -0.45), (-0.55, -0.38), (-0.38, -0.32), (-0.38, -0.18), (-0.62, -0.1)]
    dreh = M @ Matrix.Translation((u, 0, v - 0.27)) @ Matrix.Rotation(math.radians(-90), 4, "Z")
    netz.prisma([(-a - 0.28, b) for a, b in umriss], -0.03, 0.03, eisen, matrix=dreh)


def amboss(netz, st, fuss):
    """Amboss auf einem Holzklotz."""
    bs.fass(netz, st, fuss, r=0.28, h=0.55, reifen=(), stoff="Holz alt")
    z = fuss.z + 0.55
    netz.quader((fuss.x, fuss.y, z + 0.12), (0.22, 0.32, 0.24), st["Eisen"])
    netz.quader((fuss.x, fuss.y, z + 0.32), (0.28, 0.62, 0.16), st["Eisen"])
    netz.vieleck_nach([(fuss.x - 0.13, fuss.y + 0.31, z + 0.25), (fuss.x + 0.13, fuss.y + 0.31, z + 0.25),
                       (fuss.x, fuss.y + 0.62, z + 0.36)], (0, 0.3, 1), st["Eisen"])
    netz.vieleck_nach([(fuss.x - 0.13, fuss.y + 0.31, z + 0.25), (fuss.x, fuss.y + 0.62, z + 0.36),
                       (fuss.x - 0.14, fuss.y + 0.31, z + 0.4)], (-1, 0, 0.2), st["Eisen"])
    netz.vieleck_nach([(fuss.x + 0.13, fuss.y + 0.31, z + 0.25), (fuss.x, fuss.y + 0.62, z + 0.36),
                       (fuss.x + 0.14, fuss.y + 0.31, z + 0.4)], (1, 0, 0.2), st["Eisen"])
    netz.vieleck_nach([(fuss.x - 0.14, fuss.y + 0.31, z + 0.4), (fuss.x + 0.14, fuss.y + 0.31, z + 0.4),
                       (fuss.x, fuss.y + 0.62, z + 0.36)], (0, 0.1, 1), st["Eisen"])


def kohlehaufen(netz, st, mitte, breite, tiefe, hoehe):
    """Haufen Holzkohle: flache Pyramide mit abgeflachter Spitze."""
    unten = [mitte + Vector((sx * breite / 2, sy * tiefe / 2, 0)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    oben = [mitte + Vector((sx * breite / 6, sy * tiefe / 6, hoehe)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        j = (i + 1) % 4
        aussen = (unten[i] + unten[j]) / 2 - mitte
        netz.vieleck_nach([unten[i], unten[j], oben[j], oben[i]], aussen + Vector((0, 0, 1)), st["Kohle"])
    netz.vieleck_nach(oben, (0, 0, 1), st["Kohle"])


def giesserei(netz, st):
    """Stufe 1: offener Schuppen an der Halle mit Esse, Rauchfang, Blasebalg, Amboss und Löschtrog;
    im Hof Kohle und Formkästen, ein Bretterzaun um das Grundstück."""
    holz, alt = st["Fachwerk"], st["Holz alt"]
    x_w, x_a, y_a, y_b = X1, 5.6, -4.4, 0.8
    z_hoch, z_tief = 3.4, 2.5
    for y in (y_a + 0.1, (y_a + y_b) / 2, y_b - 0.1):
        netz.stab((x_a - 0.1, y, 0), (x_a - 0.1, y, z_tief), 0.16, 0.16, holz)
    netz.stab((x_a - 0.1, y_a, z_tief - 0.08), (x_a - 0.1, y_b, z_tief - 0.08), 0.16, 0.16, holz)
    neig = math.atan2(z_hoch - z_tief, x_a - x_w)
    laenge = (x_a - x_w + 0.4) / math.cos(neig)
    mitte = Vector(((x_w + x_a + 0.4) / 2, (y_a + y_b) / 2, (z_hoch + z_tief) / 2 + 0.05 - 0.2 * math.tan(neig)))
    dach = Matrix.Translation(mitte) @ Matrix.Rotation(neig, 4, "Y")
    netz.quader((0, 0, 0), (laenge, y_b - y_a + 0.4, 0.08), alt, matrix=dach)
    y = -(y_b - y_a + 0.4) / 2 + 0.32
    while y < (y_b - y_a + 0.4) / 2 - 0.1:       # Fugen zwischen den Dachbrettern
        netz.vieleck([dach @ Vector(p) for p in ((-laenge / 2, y - 0.02, 0.045), (laenge / 2, y - 0.02, 0.045),
                                                  (laenge / 2, y + 0.02, 0.045), (-laenge / 2, y + 0.02, 0.045))], st["Fachwerk"])
        y += 0.32
    # Esse mit Rauchfang und Schlot durch das Schuppendach
    ex, ey = 2.9, -1.3
    netz.quader((ex, ey, 0.45), (1.3, 1.1, 0.9), st["Bruchstein dunkel"], ohne=("+z",))
    netz.vieleck([(ex - 0.65, ey - 0.55, 0.9), (ex + 0.65, ey - 0.55, 0.9), (ex + 0.65, ey + 0.55, 0.9), (ex - 0.65, ey + 0.55, 0.9)],
                 st["Glut"])
    unten = [Vector((ex + sx * 0.65, ey + sy * 0.55, 1.6)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    oben = [Vector((ex + sx * 0.25, ey + sy * 0.25, 2.1)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        j = (i + 1) % 4
        netz.vieleck_nach([unten[i], unten[j], oben[j], oben[i]], (unten[i] + unten[j]) / 2 - Vector((ex, ey, 1.0)), st["Bruchstein dunkel"])
    esse(netz, st, Vector((ex, ey, 0)), 4.4, 2.1, breite=0.5, tiefe=0.5)
    netz.prisma([(ey - 0.45, 0.5), (ey + 0.45, 0.5), (ey - 0.45, 0.85)], ex + 0.7, ex + 1.1, st["Leder"])   # Blasebalg
    amboss(netz, st, Vector((4.3, -2.7, 0)))
    bs.fass(netz, st, Vector((4.6, -0.4, 0)), r=0.32, h=0.5, reifen=(0.3,))
    netz.vieleck([(4.6 + 0.29 * math.cos(math.radians(22.5 + 45 * k)), -0.4 + 0.29 * math.sin(math.radians(22.5 + 45 * k)), 0.44)
                  for k in range(8)], st["Wasser"])
    # Hof: Kohle, Formkästen für den Guss, Eisenstangen an der Wand
    kohlehaufen(netz, st, Vector((6.2, 3.2, 0)), 2.0, 1.5, 0.75)
    for k, (x, y) in enumerate(((6.5, -2.4), (6.5, -1.4), (6.5, -0.4))):
        netz.quader((x, y, 0.2 + 0.02 * k), (0.9, 0.75, 0.4), st["Formsand"])
        netz.quader((x, y, 0.42 + 0.02 * k), (0.95, 0.8, 0.05), alt)
    for k in range(5):
        netz.stab((X1 + 0.15 + 0.06 * k, 1.1, 0), (X1 + 0.05 + 0.06 * k, 1.25, 2.2), 0.04, 0.04, st["Eisen"])
    bs.zaun(netz, st, [(7.8, -4.8), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])


def anbau(netz, st, obergeschoss):
    """Anbau rechts der Halle, Giebel zur Gasse. Stufe 2: Schmiede mit Steinerdgeschoss, zweiter Esse und
    halb offenem Tor. Stufe 3: zusätzlich ein Fachwerk-Obergeschoss und ein Laden mit Klappläden."""
    cx, cy = (AX0 + AX1) / 2, (AY0 + AY1) / 2
    breite, tief = AX1 - AX0, AY1 - AY0
    h_eg, h_og = 3.2, 2.3
    z_eg = Z_SOCKEL + h_eg
    netz.quader((cx, cy, Z_SOCKEL / 2), (breite + 0.12, tief + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    netz.quader((cx, cy, Z_SOCKEL + h_eg / 2), (breite, tief, h_eg), st["Bruchstein"], ohne=("+z",))
    bs.eckquader(netz, st, AX1, AY0, 1, -1, Z_SOCKEL, z_eg, stoff="Bruchstein dunkel")
    bs.eckquader(netz, st, AX1, AY1, 1, 1, Z_SOCKEL, z_eg, stoff="Bruchstein dunkel")
    Mv = bs.wandmatrix((cx, AY0, Z_SOCKEL), "vorne")
    Mr = bs.wandmatrix((AX1, cy, Z_SOCKEL), "rechts")
    Mh = bs.wandmatrix((cx, AY1, Z_SOCKEL), "hinten")
    for Mx, L in ((Mv, breite), (Mr, tief), (Mh, breite)):
        bs.fugen(netz, Mx, L - 0.02, 0, h_eg, st, abstand=0.45)
    if obergeschoss:
        # Laden: breites Fenster mit Klappläden in der Hausfarbe, unten Verkaufstisch, oben Vordach
        laden = Oeffnung("fenster", -1.0, 0.75, 2.0, 1.25)
        bs.gewaende(netz, Mv, laden, st, stein="Sandstein")
        netz.quader((laden.u, -0.35, laden.v - 0.05), (laden.b + 0.1, 0.62, 0.07), st["Hausfarbe"], Mv)
        for s in (-1, 1):
            netz.stab(Mv @ Vector((laden.u + s * 0.9, -0.05, laden.v - 0.45)), Mv @ Vector((laden.u + s * 0.9, -0.6, laden.v - 0.08)),
                      0.05, 0.05, st["Fachwerk"])
        klappe = Mv @ Matrix.Translation((laden.u, -0.04, laden.v + laden.h + 0.18)) @ Matrix.Rotation(math.radians(-55), 4, "X")
        netz.quader((0, -0.33, 0), (laden.b + 0.1, 0.66, 0.06), st["Hausfarbe"], klappe)
        netz.quader((laden.u - 0.4, -0.37, laden.v + 0.03), (0.5, 0.3, 0.05), st["Gold"], Mv)
        tuer = Oeffnung("tuer", 1.6, 0.0, 1.1, 2.3)
        bs.portal(netz, Mv, tuer, st)
        schild(netz, st, Mv, 2.55, 2.9)
    else:
        tor = Oeffnung("tuer", -0.6, 0.0, 2.2, 2.6)
        bs.portal(netz, Mv, tor, st)
        bs.gewaende(netz, Mv, Oeffnung("fenster", 1.75, 1.1, 0.8, 0.9), st, stein="Bruchstein dunkel", glas="Glut", gitter=True)
    for u in (-1.6, 1.6):
        bs.gewaende(netz, Mr, Oeffnung("fenster", u, 1.1, 0.85, 1.0), st, stein="Bruchstein dunkel", gitter=True)
    bs.gewaende(netz, Mh, Oeffnung("fenster", 0.0, 1.1, 0.85, 1.0), st, stein="Bruchstein dunkel", gitter=True)
    z_traufe = z_eg
    if obergeschoss:
        netz.quader((cx, cy, z_eg + h_og / 2), (breite, tief, h_og), st["Putz"])
        netz.quader((cx, cy, z_eg - 0.06), (breite + 0.1, tief + 0.1, 0.14), st["Fachwerk"], ohne=("+z",))
        Mvo = bs.wandmatrix((cx, AY0, z_eg), "vorne")
        Mro = bs.wandmatrix((AX1, cy, z_eg), "rechts")
        Mho = bs.wandmatrix((cx, AY1, z_eg), "hinten")
        for Mx, L, us in ((Mvo, breite + 2 * UEBER, (-1.4, 1.4)), (Mro, tief, (-1.7, 1.7)), (Mho, breite + 2 * UEBER, (0.0,))):
            liste = [Oeffnung("fenster", u, 0.55, 0.85, 1.05) for u in us]
            bs.fachwerk(netz, Mx, L, h_og, liste, st, kreuze=Mx is Mvo)
            for o in liste:
                bs.fenster(netz, Mx, o, st)
        z_traufe = z_eg + h_og
    # Dach mit dem First quer zur Gasse (lokal x = Welt y)
    drehung = Matrix.Translation((cx, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(netz, st, AY0, AY1, -breite / 2, breite / 2, z_traufe, 50, traufe=0.3, ortgang=0.3,
                         matrix=drehung, **bs.ZIEGEL)
    hoch = breite / 2 * dach["tan"]
    for y, seite in ((AY0, "vorne"), (AY1, "hinten")):
        bs.fachwerkgiebel(netz, st, bs.wandmatrix((cx, y, z_traufe), seite), breite, hoch, [])
    # zweite Esse an der Rückseite des Anbaus
    esse(netz, st, Vector((AX1 - 1.2, AY1 - 1.0, 0)), z_traufe + hoch + 1.1, z_traufe + 0.5, breite=0.8, tiefe=0.8)


def schild(netz, st, M, u, v):
    """Goldschmiede: Ausleger mit goldenem Ring als Ladenschild."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -1.0, v)), 0.05, 0.05, eisen)
    netz.stab(M @ Vector((u, 0, v - 0.45)), M @ Vector((u, -0.75, v - 0.02)), 0.04, 0.04, eisen)
    teile = 10
    r_a, r_i = 0.34, 0.22
    ring = [(r_a * math.cos(2 * math.pi * k / teile), r_a * math.sin(2 * math.pi * k / teile)) for k in range(teile)]
    dreh = M @ Matrix.Translation((u, -0.7, v - 0.5)) @ Matrix.Rotation(math.radians(-90), 4, "Z")
    for k in range(teile):         # Ring aus zehn Stücken
        a0, a1 = 2 * math.pi * k / teile, 2 * math.pi * (k + 1) / teile
        stueck = [(r_a * math.cos(a0), r_a * math.sin(a0)), (r_a * math.cos(a1), r_a * math.sin(a1)),
                  (r_i * math.cos(a1), r_i * math.sin(a1)), (r_i * math.cos(a0), r_i * math.sin(a0))]
        netz.prisma(stueck, -0.04, 0.04, st["Gold"], matrix=dreh)
    del ring
    netz.stab(M @ Vector((u, -0.7, v)), M @ Vector((u, -0.7, v - 0.17)), 0.02, 0.02, eisen)


def notstall(netz, st, mitte):
    """Notstall zum Beschlagen der Pferde: vier Pfosten, Längs- und Querholme, kleines Brettdach."""
    holz = st["Fachwerk"]
    bx, by = 1.0, 1.4
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.stab(mitte + Vector((sx * bx / 2, sy * by / 2, 0)), mitte + Vector((sx * bx / 2, sy * by / 2, 2.1)), 0.16, 0.16, holz)
        for z in (0.9, 1.4):
            netz.stab(mitte + Vector((sx * bx / 2, -by / 2 - 0.2, z)), mitte + Vector((sx * bx / 2, by / 2 + 0.2, z)), 0.1, 0.1, holz)
    for sy in (-1, 1):
        netz.stab(mitte + Vector((-bx / 2 - 0.1, sy * by / 2, 2.05)), mitte + Vector((bx / 2 + 0.1, sy * by / 2, 2.05)), 0.12, 0.12, holz)
    netz.quader((0, 0, 2.25), (bx + 0.6, by + 0.6, 0.07), st["Holz alt"],
                matrix=Matrix.Translation(mitte) @ Matrix.Rotation(math.radians(8), 4, "X"))


def dachreiter(netz, st, first):
    """Dachreiter auf dem First mit goldener vierseitiger Spitze (wie bisher im Spiel ab Stufe 3)."""
    x, y, z = first
    netz.quader((x, y, z + 0.6), (1.3, 1.3, 1.6), st["Putz"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            netz.quader((x + sx * 0.6, y + sy * 0.6, z + 0.6), (0.14, 0.14, 1.6), st["Fachwerk"])
    for s in (-1, 1):
        netz.vieleck_nach([(x - 0.3, y + s * 0.66, z + 0.7), (x + 0.3, y + s * 0.66, z + 0.7),
                           (x + 0.3, y + s * 0.66, z + 1.25), (x - 0.3, y + s * 0.66, z + 1.25)], (0, s, 0), st["Loch"])
    spitze = Vector((x, y, z + 3.4))
    ecken = [Vector((x + sx * 0.85, y + sy * 0.85, z + 1.4)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        j = (i + 1) % 4
        netz.vieleck_nach([ecken[i], ecken[j], spitze], (ecken[i] + ecken[j]) / 2 - Vector((x, y, z + 1.0)), st["Gold"])
    netz.quader((x, y, z + 3.6), (0.1, 0.1, 0.45), st["Gold"])


def hinterhaus(netz, st):
    """Goldschmiede: steinernes Hinterhaus im Hof (Lager und Schatzkammer) mit vergitterten Fenstern."""
    x0, x1, y0, y1 = -7.2, 0.6, 2.3, 6.3
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    h = 4.6
    netz.quader((cx, cy, Z_SOCKEL / 2), (x1 - x0 + 0.12, y1 - y0 + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    netz.quader((cx, cy, Z_SOCKEL + h / 2), (x1 - x0, y1 - y0, h), st["Bruchstein"], ohne=("+z",))
    for sx, x in ((-1, x0), (1, x1)):
        for sy, y in ((-1, y0), (1, y1)):
            bs.eckquader(netz, st, x, y, sx, sy, Z_SOCKEL, Z_SOCKEL + h, stoff="Sandstein")
    bs.gesims(netz, st, x0, x1, y0, y1, Z_SOCKEL + h - 0.05, hoehe=0.18, vor=0.08)
    for seite, punkt, L, us in (("vorne", (cx, y0, Z_SOCKEL), x1 - x0, (-2.4, 2.4)), ("hinten", (cx, y1, Z_SOCKEL), x1 - x0, (-2.4, 0.0, 2.4)),
                                ("links", (x0, cy, Z_SOCKEL), y1 - y0, (0.0,)), ("rechts", (x1, cy, Z_SOCKEL), y1 - y0, (0.0,))):
        Mx = bs.wandmatrix(punkt, seite)
        bs.fugen(netz, Mx, L - 0.02, 0, h, st, abstand=0.45)
        for u in us:
            bs.gewaende(netz, Mx, Oeffnung("fenster", u, 2.6, 0.7, 0.9), st, gitter=True)
    Mv = bs.wandmatrix((cx, y0, Z_SOCKEL), "vorne")
    bs.tuer(netz, Mv, Oeffnung("tuer", 0.0, 0.0, 1.1, 2.1), st)
    netz.quader((0.0, -0.03, 2.2), (1.5, 0.1, 0.22), st["Sandstein"], Mv, ohne=("+y",))
    bs.satteldach(netz, st, x0, x1, y0, y1, Z_SOCKEL + h + 0.13, 45, traufe=0.3, ortgang=0.3, **bs.ZIEGEL)


def hofmauer(netz, st):
    """Goldschmiede: Hofmauer aus Bruchstein mit Torbogen statt des Bretterzauns."""
    stein = st["Bruchstein"]
    # Abschnitte so, dass sich keine Flächen decken: die Rückmauer läuft durch, die Seiten enden davor,
    # rechts lässt die Mauer Platz für das Tor
    for (x0, y0), (x1, y1) in (((7.7, AY1 + 0.2), (7.7, 3.4)), ((7.7, 5.8), (7.7, 6.5)), ((7.9, 6.7), (-7.9, 6.7)),
                               ((-7.7, 6.5), (-7.7, Y1 + 0.3))):
        laenge = math.hypot(x1 - x0, y1 - y0)
        mitte = Vector(((x0 + x1) / 2, (y0 + y1) / 2, 0.9))
        quer = abs(x1 - x0) > abs(y1 - y0)
        netz.quader(mitte, (laenge if quer else 0.4, 0.4 if quer else laenge, 1.8), stein)
        netz.quader(mitte + Vector((0, 0, 0.97 if quer else 0.95)), (laenge + 0.1 if quer else 0.5, 0.5 if quer else laenge, 0.1),
                    st["Sandstein"])                    # Rückmauer 2 cm höher: die Abdeckungen decken sich nicht
    # Torbogen in der rechten Mauer
    netz.quader((7.65, 4.6, 1.35), (0.45, 2.4, 2.7), stein)
    M = bs.wandmatrix((7.875, 4.6, 0), "rechts")
    bs.portal(netz, M, Oeffnung("tuer", 0.0, 0.0, 1.6, 2.4), st)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=3.6, ausschnitt=27.0, schnell=schnell)
    zustaende = ("Stufe1", "Stufe2", "Stufe3", "Leerstand", "Geruest")

    def nur(*an):
        return lambda o: o.name not in zustaende or o.name in an

    bilder = [bs.rendern(f"{NAME}-stufe{k}", nur(f"Stufe{k}")) for k in (1, 2, 3)]
    print(bilder)
    glut = bs.stoff("Glut").node_tree.nodes["Principled BSDF"].inputs["Emission Strength"]
    glut.default_value = 0.0                   # verlassen: die Esse ist kalt (im Spiel regelt das der Code)
    print(bs.rendern(NAME + "-leerstand", lambda o: o.name in ("Haus", "Stufe1", "Leerstand") or o.name.startswith("_")))
    glut.default_value = bs.LEUCHTEN["Glut"][1]
    print(bs.rendern(NAME + "-geruest", nur("Stufe2", "Geruest")))
    if not schnell:
        print(bs.rundum(NAME + "-stufe3", nur("Stufe3")))
    print(bs.nebeneinander(NAME, bilder, [f"Stufe {k + 1}: {n}" for k, n in enumerate(STUFEN)]))


if __name__ == "__main__":
    main()
