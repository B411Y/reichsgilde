"""Werkstatt des Alchimisten – Tinkturei (Stufe 1), Kräuterladen (Stufe 2), Alchimistenstube (Stufe 3).
Fläche 16 × 14 m.

    blender -b --python tools/modelle/alchimist.py        oder
    python tools/modelle/alchimist.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/alchimist.glb und Bilder nach docs/modelle/.

Objekte im GLB (wie bei Schmied, Tischler und Steinmetz):
    Haus        das Laboratorium, verputzt, in jeder Stufe gleich
    Stufe1      Tinkturei: Fässer mit Kräuterauszügen, Trockengestelle mit Kräuterbündeln, Farbbottich,
                Kräuterbeete, Brennholz, Zaun
    Stufe2      Kräuterladen: Ladenanbau mit Auslage und Schild, Rosen- und Kräuterbeete, Trockengestell,
                Flaschen auf dem Ladenbrett, Gaube
    Stufe3      Alchimistenstube: achteckiger Laborturm mit Schieferhelm und Schlot, im Fenster der Feuerschein
                des Athanors („Glut“), Brunnen, Beete, Gaube, Dachreiter mit violetter Spitze
    Wappen, Fahne, Leerstand, Geruest, Anker_Tuer, Anker_Karren
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.
Das Material „Glut“ (Fenster des Athanors) leuchtet – das Spiel regelt die Stärke wie bei der Esse.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Laboratorium links (7,6 × 7,2 m): zwei Geschosse in hellem Kalkputz auf einem Bruchsteinsockel, das
    Obergeschoss kragt zur Gasse vor, steiles Schieferdach mit dem Giebel zur Gasse, Fensterläden in der
    Hausfarbe, Haustür mit Sandsteingewände und Stufen, Ladenfenster mit Klappläden, Laborschlot,
    Zunftzeichen: ein Glaskolben am Ausleger.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "alchimist"
FLAECHE = (16.0, 14.0)
STUFEN = ("Tinkturei", "Kräuterladen", "Alchimistenstube")

# ── Maße des Laboratoriums ───────────────────────────────────────────────────
X0, X1, Y0, Y1 = -7.2, 0.4, -5.0, 2.2
CX, CY = (X0 + X1) / 2, (Y0 + Y1) / 2
HW = (X1 - X0) / 2
VOR = 0.3                                   # Vorkragung des Obergeschosses zur Gasse
Z_SOCKEL, H_EG, H_OG = 0.4, 3.0, 2.6
Z_OG = Z_SOCKEL + H_EG
Z_TRAUFE = Z_OG + H_OG                      # 6,0
NEIGUNG = 54


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []

    # ── Sockel, Erdgeschoss, vorkragendes Obergeschoss ────────────────────────
    haus.quader((CX, CY, Z_SOCKEL / 2), (X1 - X0 + 0.12, Y1 - Y0 + 0.12, Z_SOCKEL), st["Bruchstein"])
    haus.quader((CX, CY, (Z_SOCKEL + Z_OG) / 2), (X1 - X0, Y1 - Y0, H_EG), st["Putz"], ohne=("+z",))
    yo = Y0 - VOR
    haus.quader((CX, (yo + Y1) / 2, (Z_OG + Z_TRAUFE) / 2), (X1 - X0, Y1 - yo, H_OG), st["Putz"])
    for sx, x in ((-1, X0), (1, X1)):
        bs.eckquader(haus, st, x, Y0, sx, -1, Z_SOCKEL, Z_OG - 0.1)
        bs.eckquader(haus, st, x, Y1, sx, 1, Z_SOCKEL, Z_OG - 0.1)
    # Schwelle und Knaggen unter dem Überhang
    haus.quader((CX, yo - 0.04, Z_OG + 0.08), (X1 - X0 + 0.1, 0.1, 0.2), st["Fachwerk"], ohne=("+y",))
    for x in (X0 + 0.25, CX - 1.3, CX + 1.3, X1 - 0.25):
        haus.stab((x, Y0 + 0.02, Z_OG - 0.55), (x, yo + 0.02, Z_OG - 0.02), 0.14, 0.14, st["Fachwerk"], quer=(1, 0, 0))

    # ── Fenster mit Läden, Haustür, Ladenfenster ──────────────────────────────
    F = "fenster"
    M_vorn = bs.wandmatrix((CX, Y0, Z_SOCKEL), "vorne")
    M_vorn_og = bs.wandmatrix((CX, yo, Z_OG), "vorne")
    M_rechts = bs.wandmatrix((X1, CY, Z_SOCKEL), "rechts")
    M_links = bs.wandmatrix((X0, CY, Z_SOCKEL), "links")
    M_rechts_og = bs.wandmatrix((X1, (yo + Y1) / 2, Z_OG), "rechts")
    M_links_og = bs.wandmatrix((X0, (yo + Y1) / 2, Z_OG), "links")
    M_hinten = bs.wandmatrix((CX, Y1, Z_SOCKEL), "hinten")
    M_hinten_og = bs.wandmatrix((CX, Y1, Z_OG), "hinten")
    eg = dict(v=0.9, b=0.8, h=1.2)
    og = dict(v=0.55, b=0.8, h=1.15)

    def fenster(M, liste, laeden=True):
        for o in liste:
            bs.gewaende(haus, M, o, st)
            if laeden:
                fensterlaeden(haus, st, M, o)
            oeffnungen.append((M, o))

    tuer = Oeffnung("tuer", -1.8, 0.0, 1.1, 2.2)
    bs.tuer(haus, M_vorn, tuer, st)
    tuerrahmen(haus, st, M_vorn, tuer)
    oeffnungen.append((M_vorn, tuer))
    x_tuer = CX + tuer.u
    haus.quader((x_tuer, Y0 - 0.45, 0.1), (1.7, 0.3, 0.2), st["Sandstein"])
    haus.quader((x_tuer, Y0 - 0.15, (Z_SOCKEL + 0.01) / 2), (1.7, 0.3, Z_SOCKEL + 0.01), st["Sandstein"])   # 1 cm über dem Sockel
    laden = Oeffnung("fenster", 0.6, 0.85, 1.8, 1.1)
    bs.gewaende(haus, M_vorn, laden, st)
    ladenklappen(haus, st, M_vorn, laden)
    oeffnungen.append((M_vorn, laden))
    fenster(M_vorn, [Oeffnung(F, 2.6, **eg)])
    fenster(M_vorn_og, [Oeffnung(F, u, **og) for u in (-2.6, -0.9, 0.9, 2.6)])
    for M, M_og in ((M_rechts, M_rechts_og), (M_links, M_links_og)):
        fenster(M, [Oeffnung(F, u, **eg) for u in (-2.0, 1.2)])
        fenster(M_og, [Oeffnung(F, u, **og) for u in (-2.4, 0.0, 2.4)])
    fenster(M_hinten, [Oeffnung(F, 1.8, **eg)])
    hinten = Oeffnung("tuer", -1.6, 0.0, 1.0, 2.1)
    bs.tuer(haus, M_hinten, hinten, st)
    tuerrahmen(haus, st, M_hinten, hinten)
    oeffnungen.append((M_hinten, hinten))
    fenster(M_hinten_og, [Oeffnung(F, u, **og) for u in (-1.8, 1.8)])
    zunftzeichen(haus, st, M_vorn, -0.7, 2.75)

    # ── Schieferdach mit dem Giebel zur Gasse, verputzte Giebel, Laborschlot ──
    drehung = Matrix.Translation((CX, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(haus, st, yo, Y1, -HW, HW, Z_TRAUFE, NEIGUNG, traufe=0.35, ortgang=0.35,
                         matrix=drehung, **bs.SCHIEFER)
    hoch = HW * dach["tan"]
    for y, seite, liste in ((yo, "vorne", [Oeffnung(F, u, 0.45, 0.7, 1.0) for u in (-1.4, 1.4)]
                             + [Oeffnung(F, 0.0, 2.45, 0.5, 0.7)]),
                            (Y1, "hinten", [Oeffnung(F, 0.0, 0.6, 0.7, 1.0)])):
        M = bs.wandmatrix((CX, y, Z_TRAUFE), seite)
        bs.platte(haus, M, [(-HW, 0), (HW, 0), (0, hoch)], 0, 0.3, st["Putz"])
        haus.quader((0, -0.04, 0.05), (2 * HW + 0.1, 0.1, 0.14), st["Fachwerk"], M, ohne=("+y",))
        for o in liste:
            bs.gewaende(haus, M, o, st)
            oeffnungen.append((M, o))
    xs, ys = CX + 1.5, Y1 - 1.4                                       # Schlot auf der rechten Dachfläche
    z0 = dach["oben_vorn"](-(xs - CX)) - 0.3
    oben = dach["z_first"] + 1.0
    haus.quader((xs, ys, (z0 + oben) / 2), (0.8, 0.8, oben - z0), st["Bruchstein dunkel"])
    haus.quader((xs, ys, oben + 0.06), (0.98, 0.98, 0.12), st["Sandstein"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            haus.quader((xs + sx * 0.3, ys + sy * 0.3, oben + 0.32), (0.12, 0.12, 0.4), st["Bruchstein dunkel"])
    haus.prisma([(-0.6, 0), (0.6, 0), (0, 0.35)], -0.6, 0.6, st["Schiefer"],
                matrix=Matrix.Translation((xs, ys, oben + 0.52)) @ Matrix.Rotation(math.radians(90), 4, "Z"))

    # ── Wappen im Giebel, Fahne am hinteren Giebel ────────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, bs.wandmatrix((CX, yo, Z_TRAUFE), "vorne"), 0.0, 1.75, st, groesse=0.7)
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (CX, Y1 - 0.4, dach["z_first"] - 0.3), hoehe=2.5, richtung=1)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    tinkturei(stufe1, st)
    stufe2 = bs.Netz("Stufe2")
    kraeuterladen(stufe2, st, M_vorn, laden)
    bs.gaube(stufe2, st, dach, [], x0=-0.8, x1=0.8, y_front=-HW + 1.2)
    stufe3 = bs.Netz("Stufe3")
    alchimistenstube(stufe3, st, M_vorn, laden)
    bs.gaube(stufe3, st, dach, [], x0=-0.8, x1=0.8, y_front=-HW + 1.2)
    bs.dachreiter(stufe3, st, Vector((CX, Y1 - 2.6, dach["z_first"])), spitze="Violett")

    # ── Leerstand, Gerüst, Anker ──────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.0, Y0 - 1.3), (X1 - 0.2, Y0 - 0.15), (X0 - 0.15, -1.0), (X1 + 0.15, 1.8)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=X1 + 0.15, y_innen=yo - 0.35, y_aussen=yo - 1.25,
                  ebenen=(2.4, 4.8), hoehe_innen=7.0, hoehe_aussen=7.6, felder=3)
    anker = [bs.anker("Anker_Tuer", (x_tuer, Y0 - 0.15, Z_SOCKEL + 0.01)),
             bs.anker("Anker_Karren", (CX + laden.u, Y0 - 1.7, 0.0))]

    objekte = [haus.objekt(), stufe1.objekt(), stufe2.objekt(), stufe3.objekt(), wappen.objekt(), fahne.objekt(),
               leer.objekt(), geruest.objekt()] + anker
    for o in objekte:
        if o.name in ("Stufe2", "Stufe3", "Leerstand", "Geruest"):
            bs.zustand(o)
        elif o.name == "Stufe1":
            o["zustand"] = True
    return objekte


# ─────────────────────────────────────────────────────────────────────────────
#  Teile des Laboratoriums
# ─────────────────────────────────────────────────────────────────────────────

def fensterlaeden(netz, st, M, o, winkel=35):
    """Zwei halb geöffnete Läden in der Hausfarbe neben einem Steinfenster (außen am Gewände angeschlagen)."""
    breite = o.b / 2 + 0.08
    for s in (-1, 1):
        dreh = -winkel if s > 0 else 180 + winkel
        t = (M @ Matrix.Translation((o.u + s * (o.b / 2 + 0.16), -0.075, o.v + o.h / 2))
             @ Matrix.Rotation(math.radians(dreh), 4, "Z"))
        netz.quader((breite / 2, 0, 0), (breite, 0.035, o.h + 0.04), st["Hausfarbe"], t)


def tuerrahmen(netz, st, M, o, g=0.16):
    """Gewände aus Sandstein um eine Tür: zwei Pfosten und ein Sturz."""
    for s in (-1, 1):
        netz.quader((o.u + s * (o.b / 2 + g / 2), -0.025, o.v + o.h / 2), (g, 0.07, o.h), st["Sandstein"], M,
                    ohne=("+y", "+z"))
    netz.quader((o.u, -0.03, o.v + o.h + g / 2), (o.b + 2 * g + 0.06, 0.08, g), st["Sandstein"], M, ohne=("+y",))


def ladenklappen(netz, st, M, o):
    """Ladenfenster: die untere Klappe liegt als Ladenbrett waagrecht vor dem Fenster (an Ketten),
    die obere steht schräg als Vordach."""
    netz.quader((o.u, -0.33, o.v - 0.05), (o.b + 0.1, 0.56, 0.05), st["Hausfarbe"], M)
    for s in (-1, 1):
        netz.stab(M @ Vector((o.u + s * o.b / 2, -0.58, o.v - 0.03)), M @ Vector((o.u + s * o.b / 2, -0.06, o.v + 0.55)),
                  0.02, 0.02, st["Eisen"])
    t = M @ Matrix.Translation((o.u, -0.08, o.v + o.h + 0.14)) @ Matrix.Rotation(math.radians(-28), 4, "X")
    netz.quader((0, -0.3, 0), (o.b + 0.1, 0.6, 0.04), st["Hausfarbe"], t)


def zunftzeichen(netz, st, M, u, v):
    """Zunftzeichen am Ausleger: ein grüner Glaskolben mit langem Hals in einem Eisenring."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -1.1, v)), 0.05, 0.05, eisen)
    netz.stab(M @ Vector((u, 0, v - 0.5)), M @ Vector((u, -0.8, v - 0.02)), 0.04, 0.04, eisen)
    mitte = M @ Vector((u, -0.75, v - 0.62))
    netz.stab(mitte + Vector((0, 0, 0.62)), mitte + Vector((0, 0, 0.42)), 0.02, 0.02, eisen)
    bs.achteck(netz, st, mitte + Vector((0, 0, -0.2)), 0.2, 0.0, 0.28, "Elixier")              # Bauch
    bs.achteck(netz, st, mitte + Vector((0, 0, -0.2)), 0.14, -0.06, 0.0, "Elixier")
    bs.achteck(netz, st, mitte + Vector((0, 0, -0.2)), 0.05, 0.28, 0.6, "Elixier")              # Hals
    bs.achteck(netz, st, mitte + Vector((0, 0, -0.2)), 0.23, 0.12, 0.16, "Eisen")               # Ring


def fass(netz, st, boden, inhalt="Kraut", r=0.36, h=0.95):
    """Offenes Fass mit einem Auszug darin."""
    bs.fass(netz, st, Vector(boden), r=r, h=h)
    bs.rund_oben(netz, st, Vector(boden), r - 0.04, h - 0.06, inhalt)


def buendel(netz, st, oben, laenge=0.5, stoff="Heu"):
    """Kräuterbündel an einer Schnur, kopfüber zum Trocknen."""
    oben = Vector(oben)
    netz.stab(oben, oben - Vector((0, 0, 0.12)), 0.02, 0.02, st["Seil"])
    netz.prisma([(-0.11, 0), (0.11, 0), (0.05, -laenge), (-0.05, -laenge)], -0.08, 0.08, st[stoff],
                matrix=Matrix.Translation(oben - Vector((0, 0, 0.12))) @ Matrix.Rotation(math.radians(90), 4, "Z"))


def trockengestell(netz, st, mitte, breite=2.4, dreh=0.0, n=5, saat=0):
    """Gestell aus zwei Pfosten und einer Stange, daran Kräuterbündel."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for s in (-1, 1):
        netz.stab(t @ Vector((s * breite / 2, 0, 0)), t @ Vector((s * breite / 2, 0, 2.0)), 0.12, 0.12, st["Holz alt"])
        netz.stab(t @ Vector((s * breite / 2, -0.35, 0)), t @ Vector((s * breite / 2, 0, 1.0)), 0.08, 0.08, st["Holz alt"])
    netz.stab(t @ Vector((-breite / 2 - 0.1, 0, 1.95)), t @ Vector((breite / 2 + 0.1, 0, 1.95)), 0.09, 0.09, st["Holz"])
    zufall = random.Random(saat)
    for k in range(n):
        x = -breite / 2 + breite * (k + 0.5) / n
        buendel(netz, st, t @ Vector((x, 0, 1.9)), laenge=zufall.uniform(0.4, 0.6), stoff="Kraut" if k % 2 else "Heu")


def beet(netz, st, mitte, laenge, breite, pflanze="Kraut", bluete=None, saat=0):
    """Hochbeet mit Bretterrand und Erde, darauf Reihen von Pflanzen (auf Wunsch mit Blüten)."""
    x, y = mitte[0], mitte[1]
    for s in (-1, 1):
        netz.quader((x, y + s * breite / 2, 0.15), (laenge, 0.06, 0.3), st["Holz alt"])
        netz.quader((x + s * laenge / 2, y, 0.15), (0.06, breite - 0.06, 0.3), st["Holz alt"])
    netz.quader((x, y, 0.13), (laenge - 0.06, breite - 0.06, 0.26), st["Lehm"], ohne=("-z",))
    zufall = random.Random(saat)
    reihen = max(1, int(breite / 0.45))
    je = max(1, int(laenge / 0.45))
    for i in range(reihen):
        for j in range(je):
            fuss = Vector((x - laenge / 2 + laenge * (j + 0.5) / je + zufall.uniform(-0.06, 0.06),
                           y - breite / 2 + breite * (i + 0.5) / reihen, 0.26))
            bs.buschel(netz, st, fuss, groesse=zufall.uniform(0.3, 0.42), stoff=pflanze)
            if bluete:
                netz.quader(fuss + Vector((0, 0, 0.45)), (0.12, 0.12, 0.1), st[bluete])


def flaschen(netz, st, M, u, v, n=6, abstand=0.24):
    """Eine Reihe bunter Flaschen und Tiegel, etwa auf einem Ladenbrett."""
    farben = ("Elixier", "Rose", "Farbe", "Heu", "Kraut", "Elixier")
    for k in range(n):
        p = M @ Vector((u + (k - (n - 1) / 2) * abstand, -0.32 + 0.08 * (k % 2), v))
        if k % 3 == 2:
            bs.achteck(netz, st, p, 0.08, 0.0, 0.1, "Sandstein", n=6)                        # Tiegel mit Salbe
        else:
            bs.achteck(netz, st, p, 0.06, 0.0, 0.18, farben[k % len(farben)], n=6)
            bs.achteck(netz, st, p, 0.025, 0.18, 0.26, farben[k % len(farben)], n=6)


def brennholz(netz, st, mitte, laenge=1.8, lagen=4, dreh=0.0):
    """Gestapelte Scheite für das Feuer unter dem Kessel."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for k in range(lagen):
        for j in range(4 - k % 2):
            netz.quader((-laenge / 2 + laenge * (j + 0.5 + 0.5 * (k % 2)) / 4, 0, 0.09 + 0.17 * k), (0.4, 0.5, 0.16),
                        st["Holz"], t)
    netz.quader((0, 0, 0.09 + 0.17 * lagen + 0.03), (laenge + 0.2, 0.7, 0.04), st["Holz alt"], t)


def bottich(netz, st, boden, r=0.75):
    """Farbbottich: breiter, niedriger Bottich mit rotem Sud und einem Rührholz."""
    bs.fass(netz, st, Vector(boden), r=r, h=0.6, reifen=(0.1, 0.45))
    bs.rund_oben(netz, st, Vector(boden), r - 0.05, 0.52, "Farbe")
    netz.stab(Vector(boden) + Vector((0.2, 0.1, 0.4)), Vector(boden) + Vector((0.6, 0.5, 1.4)), 0.06, 0.06, st["Holz"])


def brunnen(netz, st, mitte):
    """Ziehbrunnen: achteckiger Brunnenkranz, zwei Pfosten mit Welle, Seil und Eimer."""
    m = Vector(mitte)
    bs.achteck(netz, st, m, 0.75, 0.0, 0.8, "Bruchstein")
    bs.rund_oben(netz, st, m, 0.58, 0.803, "Loch")
    for s in (-1, 1):
        netz.stab(m + Vector((s * 0.82, 0, 0)), m + Vector((s * 0.82, 0, 2.0)), 0.12, 0.12, st["Holz alt"])
    netz.stab(m + Vector((-1.0, 0, 1.8)), m + Vector((1.0, 0, 1.8)), 0.1, 0.1, st["Holz"], quer=(0, 0, 1))
    netz.stab(m + Vector((0, 0, 1.75)), m + Vector((0, 0, 1.15)), 0.03, 0.03, st["Seil"])
    bs.fass(netz, st, m + Vector((0, 0, 0.85)), r=0.16, h=0.3, reifen=(0.05,))
    netz.prisma([(-0.9, 0), (0.9, 0), (0, 0.45)], -0.55, 0.55, st["Schiefer"],
                matrix=Matrix.Translation(m + Vector((0, 0, 2.0))))


def laborturm(netz, st, mitte, r=1.8, h=7.6):
    """Achteckiger Laborturm in Kalkputz auf Bruchsteinsockel, Sandsteinbänder, Steinfenster, Tür zur Gasse,
    Schieferhelm mit violettem Knauf und ein Schlot für den Athanor. Das Fenster vorn rechts im
    ersten Stock zeigt den Feuerschein („Glut“)."""
    m = Vector(mitte)
    bs.achteck(netz, st, m, r + 0.08, 0.0, 0.45, "Bruchstein")
    bs.achteck(netz, st, m, r, 0.45, h - 0.22, "Putz")         # oben schließt das Sandsteinband ab
    bs.achteck(netz, st, m, r + 0.06, 3.1, 3.26, "Sandstein")
    bs.achteck(netz, st, m, r + 0.1, h - 0.22, h, "Sandstein")
    a = r * math.cos(math.radians(22.5))
    oeffnungen = []

    def wand(phi):
        w = math.radians(phi)
        return Matrix.Translation(m + Vector((a * math.cos(w), a * math.sin(w), 0))) @ Matrix.Rotation(w + math.pi / 2, 4, "Z")

    tuer = Oeffnung("tuer", 0.0, 0.45, 0.95, 2.1)
    M = wand(-90)
    bs.tuer(netz, M, tuer, st)
    tuerrahmen(netz, st, M, tuer, g=0.13)
    netz.quader(M @ Vector((0, -0.3, 0.22)), (1.3, 0.6, 0.45), st["Sandstein"],
                matrix=Matrix.Rotation(math.radians(0), 4, "Z"))
    for phi, liste in ((-90, [(3.7, None), (5.8, None)]),
                       (-45, [(1.3, None), (3.7, "Glut"), (5.8, None)]),
                       (0, [(1.3, None), (3.7, None), (5.8, None)]),
                       (-135, [(3.7, None), (5.8, None)]),
                       (45, [(3.7, None)]), (135, [(3.7, None)])):
        M = wand(phi)
        for v, glas in liste:
            o = Oeffnung("fenster", 0.0, v, 0.55, 1.0)
            bs.gewaende(netz, M, o, st, kreuz=glas is None, glas=glas, stein="Sandstein")
            oeffnungen.append((M, o))
    # Schieferhelm
    gipfel = m + Vector((0, 0, h + 3.2))
    ecken = [m + Vector(((r + 0.35) * math.cos(math.radians(22.5 + 45 * k)), (r + 0.35) * math.sin(math.radians(22.5 + 45 * k)),
                         h - 0.3)) for k in range(8)]
    for k in range(8):
        e0, e1 = ecken[k], ecken[(k + 1) % 8]
        netz.vieleck_nach([e0, e1, gipfel], (e0 + e1) / 2 - (m + Vector((0, 0, h - 1.0))), st["Schiefer"])
        netz.vieleck_nach([e0, e1, e1 + Vector((0, 0, -0.12)), e0 + Vector((0, 0, -0.12))], (e0 + e1) / 2 - (m + Vector((0, 0, h))),
                          st["Schieferkante"])
    netz.stab(gipfel - Vector((0, 0, 0.2)), gipfel + Vector((0, 0, 0.5)), 0.06, 0.06, st["Eisen"])
    netz.quader(gipfel + Vector((0, 0, 0.32)), (0.22, 0.22, 0.22), st["Violett"])
    # Schlot des Athanors an der Rückseite
    w = math.radians(60)
    s = m + Vector(((a + 0.3) * math.cos(w), (a + 0.3) * math.sin(w), 0))
    netz.quader(s + Vector((0, 0, (h + 2.4) / 2)), (0.7, 0.7, h + 2.4), st["Bruchstein dunkel"])
    netz.quader(s + Vector((0, 0, h + 2.46)), (0.86, 0.86, 0.12), st["Sandstein"])
    return oeffnungen


# ─────────────────────────────────────────────────────────────────────────────
#  Stufen
# ─────────────────────────────────────────────────────────────────────────────

def tinkturei(netz, st):
    """Stufe 1: Fässer mit Kräuterauszügen an der Hauswand, Trockengestelle mit Kräuterbündeln vor dem Haus
    und im Hof, Farbbottich, Brennholz, Kräuterbeete hinten, Bretterzaun."""
    for k, (y, inhalt) in enumerate(((-3.6, "Kraut"), (-2.7, "Farbe"), (-1.8, "Elixier"), (-0.9, "Kraut"))):
        fass(netz, st, (X1 + 0.55, y, 0), inhalt)
    trockengestell(netz, st, (3.6, -5.7, 0), saat=1)
    trockengestell(netz, st, (4.4, -1.6, 0), breite=2.8, dreh=90, n=6, saat=2)
    bottich(netz, st, (6.4, -3.4, 0))
    brennholz(netz, st, (6.6, 0.6, 0), dreh=90)
    beet(netz, st, (3.0, 3.6, 0), 3.2, 1.2, saat=3)
    beet(netz, st, (3.0, 5.6, 0), 3.2, 1.2, saat=4)
    beet(netz, st, (6.2, 4.6, 0), 1.6, 3.0, saat=5)
    bs.zaun(netz, st, [(7.8, -4.8), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])


def kraeuterladen(netz, st, M_laden, laden):
    """Stufe 2: Ladenanbau mit dem Giebel zur Gasse (Ladentür, Auslage unter einem Vordach, Hängeschild),
    Flaschen auf dem Ladenbrett des Hauses, Rosen- und Kräuterbeete, Trockengestell, Fässer, Zaun."""
    ax0, ax1, ay0, ay1 = X1, 4.6, -4.4, -0.4
    cx, breite = (ax0 + ax1) / 2, ax1 - ax0
    h = 3.1
    netz.quader((cx + 0.06, (ay0 + ay1) / 2, Z_SOCKEL / 2), (breite, ay1 - ay0 + 0.12, Z_SOCKEL), st["Bruchstein"],
                ohne=("-x",))                          # beginnt am Sockel des Hauses, ohne ihn zu überdecken
    netz.quader((cx, (ay0 + ay1) / 2, (Z_SOCKEL + h) / 2), (breite, ay1 - ay0, h - Z_SOCKEL), st["Putz"], ohne=("+z", "-x"))
    M = bs.wandmatrix((cx, ay0, Z_SOCKEL), "vorne")
    M_r = bs.wandmatrix((ax1, (ay0 + ay1) / 2, Z_SOCKEL), "rechts")
    tuer = Oeffnung("tuer", -0.9, 0.0, 1.0, 2.1)
    bs.tuer(netz, M, tuer, st)
    tuerrahmen(netz, st, M, tuer)
    netz.quader((cx + tuer.u, ay0 - 0.2, (Z_SOCKEL + 0.01) / 2), (1.4, 0.4, Z_SOCKEL + 0.01), st["Sandstein"])
    auslage = Oeffnung("fenster", 0.95, 0.7, 1.4, 1.2)
    bs.gewaende(netz, M, auslage, st)
    ladenklappen(netz, st, M, auslage)
    flaschen(netz, st, M, auslage.u, auslage.v - 0.025, n=5)
    for u in (-0.9, 1.0):
        o = Oeffnung("fenster", u, 0.9, 0.75, 1.1)
        bs.gewaende(netz, M_r, o, st)
        fensterlaeden(netz, st, M_r, o)
    drehung = Matrix.Translation((cx, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(netz, st, ay0, ay1, -breite / 2, breite / 2, h, 45, traufe=0.3, ortgang=0.25, matrix=drehung,
                         **bs.SCHIEFER)
    hoch = breite / 2 * dach["tan"]
    Mg = bs.wandmatrix((cx, ay0, h), "vorne")
    bs.platte(netz, Mg, [(-breite / 2, 0), (breite / 2, 0), (0, hoch)], 0, 0.3, st["Putz"])
    bs.gewaende(netz, Mg, Oeffnung("fenster", 0.0, 0.35, 0.55, 0.75), st)
    # Hängeschild mit Mörser
    netz.stab(M @ Vector((-2.0, 0, 2.6)), M @ Vector((-2.0, -1.0, 2.6)), 0.05, 0.05, st["Eisen"])
    netz.stab(M @ Vector((-2.0, 0, 2.1)), M @ Vector((-2.0, -0.75, 2.58)), 0.04, 0.04, st["Eisen"])
    schild = M @ Matrix.Translation((-2.0, -0.6, 2.2)) @ Matrix.Rotation(math.radians(-90), 4, "Z")
    netz.prisma([(-0.32, -0.3), (0.32, -0.3), (0.32, 0.3), (-0.32, 0.3)], -0.03, 0.03, st["Hausfarbe"], matrix=schild)
    netz.prisma([(-0.16, -0.18), (0.16, -0.18), (0.2, 0.06), (-0.2, 0.06)], 0.03, 0.06, st["Gold"], matrix=schild)
    netz.stab(schild @ Vector((0.045, -0.04, 0.0)), schild @ Vector((0.045, 0.16, 0.24)), 0.04, 0.03, st["Gold"])
    # Flaschen auf dem Ladenbrett des Hauses
    flaschen(netz, st, M_laden, laden.u, laden.v - 0.025, n=7)
    # Hof
    for y, inhalt in ((0.4, "Kraut"), (1.3, "Rose")):
        fass(netz, st, (X1 + 0.55, y, 0), inhalt)
    trockengestell(netz, st, (6.5, -2.4, 0), breite=2.6, dreh=90, n=6, saat=7)
    beet(netz, st, (3.0, 2.0, 0), 3.2, 1.2, pflanze="Kraut", bluete="Rose", saat=8)
    beet(netz, st, (3.0, 4.0, 0), 3.2, 1.2, saat=9)
    beet(netz, st, (3.0, 6.0, 0), 3.2, 1.0, pflanze="Kraut", bluete="Rose", saat=10)
    beet(netz, st, (6.3, 4.2, 0), 1.6, 3.6, saat=11)
    brennholz(netz, st, (6.6, 0.8, 0), dreh=90)
    bs.zaun(netz, st, [(7.8, -4.8), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])


def alchimistenstube(netz, st, M_laden, laden):
    """Stufe 3: achteckiger Laborturm an der Gasse (Athanor im ersten Stock, Schlot), Brunnen, Beete,
    Fässer, Flaschen auf dem Ladenbrett, Zaun."""
    laborturm(netz, st, (4.4, -2.4, 0))
    flaschen(netz, st, M_laden, laden.u, laden.v - 0.025, n=7)
    brunnen(netz, st, (2.6, 1.6, 0))
    for y, inhalt in ((0.4, "Elixier"), (-0.5, "Farbe")):
        fass(netz, st, (X1 + 0.55, y, 0), inhalt)
    trockengestell(netz, st, (7.0, 1.6, 0), breite=2.2, dreh=90, n=4, saat=12)
    beet(netz, st, (3.0, 4.2, 0), 3.2, 1.2, pflanze="Kraut", bluete="Rose", saat=13)
    beet(netz, st, (3.0, 6.0, 0), 3.2, 1.0, saat=14)
    beet(netz, st, (6.3, 4.8, 0), 1.6, 3.2, pflanze="Kraut", bluete="Heu", saat=15)
    brennholz(netz, st, (7.0, -5.4, 0))
    bs.zaun(netz, st, [(7.8, 0.4), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])


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
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=4.6, ausschnitt=27.0, schnell=schnell)
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
