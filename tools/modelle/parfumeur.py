"""Werkstatt des Parfumeurs – Siederei (Stufe 1), Duftwerkstatt (Stufe 2), Parfumerie (Stufe 3). Fläche 16 × 14 m.

    blender -b --python tools/modelle/parfumeur.py        oder
    python tools/modelle/parfumeur.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/parfumeur.glb und Bilder nach docs/modelle/.

Objekte im GLB (wie bei den anderen Werkstätten):
    Haus        das Fachwerkhaus mit Zwerchhaus und dem Destillierkessel, in jeder Stufe gleich
    Stufe1      Siederei: Beete mit Lavendel und Jasmin, Trockengestell mit Lavendelbündeln, Brennholz, Zaun
    Stufe2      Duftwerkstatt: dazu eine Werkstatt als Anbau, Spindelpresse, Rosenbogen am Hoftor, Gaube
    Stufe3      Parfumerie: Werkstatt, achteckiger Duftpavillon, Springbrunnen, Rosenbeete, Gaube,
                Dachreiter mit lavendelfarbener Spitze
    Wappen, Fahne, Leerstand, Geruest, Anker_Tuer, Anker_Karren
Genau einer der Knoten Stufe1–Stufe3 ist sichtbar; Leerstand und Geruest sind standardmäßig aus.
Das Material „Glut“ (Feuer unter dem Kessel) leuchtet – das Spiel regelt die Stärke wie bei der Esse.

Aufbau (Blender: Z oben, Gasse bei -Y; Fläche x ±8, y ±7, davon 2 m Vorplatz vorn):
    Fachwerkhaus links (7,8 × 6,0 m) auf einem Bruchsteinsockel, Ziegeldach mit der Traufe zur Gasse und einem
    Zwerchhaus in der Mitte, lila Fensterläden und Blumenkästen mit Lavendel und Jasmin (wie bisher im Spiel).
    An der Ostseite der Destillierkessel aus Kupfer mit Helm, Kühlrohr und Kühlfass
    (wie bisher im Spiel).
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "parfumeur"
FLAECHE = (16.0, 14.0)
STUFEN = ("Siederei", "Duftwerkstatt", "Parfumerie")

# Farben nur für diesen Beruf (wie bisher im Spiel)
bs.FARBEN.update({
    "Kupfer blank": (0.78, 0.45, 0.25),     # Destillierkessel
    "Lavendel": (0.55, 0.43, 0.78),
    "Jasmin": (0.96, 0.95, 0.88),
    "Laden lila": (0.42, 0.30, 0.58),       # Fensterläden der Siederei
    "Lavendel Spitze": (0.62, 0.45, 0.78),  # Spitze des Dachreiters beim Parfumeur
})
bs.METALL.update({"Kupfer blank": 0.6, "Lavendel Spitze": 0.5})
bs.RAUHEIT["Kupfer blank"] = 0.35

# ── Maße des Hauses ──────────────────────────────────────────────────────────
X0, X1, Y0, Y1 = -7.4, 0.4, -5.0, 1.0
CX, CY = (X0 + X1) / 2, (Y0 + Y1) / 2
Z_SOCKEL, H_EG, H_OG = 0.4, 3.0, 2.6
Z_OG = Z_SOCKEL + H_EG
Z_TRAUFE = Z_OG + H_OG                      # 6,0
NEIGUNG = 50
ZW = 1.3                                    # halbe Breite des Zwerchhauses
H_ZW = 1.6                                  # Wandhöhe des Zwerchhauses über der Traufe


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []

    # ── Sockel, Putzkörper, Geschossbalken ────────────────────────────────────
    haus.quader((CX, CY, Z_SOCKEL / 2), (X1 - X0 + 0.12, Y1 - Y0 + 0.12, Z_SOCKEL), st["Bruchstein"])
    haus.quader((CX, CY, (Z_SOCKEL + Z_TRAUFE) / 2), (X1 - X0, Y1 - Y0, Z_TRAUFE - Z_SOCKEL), st["Putz"], ohne=("+z",))
    haus.quader((CX, CY, Z_OG - 0.05), (X1 - X0 + 0.08, Y1 - Y0 + 0.08, 0.16), st["Fachwerk"], ohne=("+z",))

    F = "fenster"
    eg = dict(v=0.9, b=0.9, h=1.1, laeden=False)
    og = dict(v=0.6, b=0.8, h=1.1, laeden=False)
    tuer = Oeffnung("tuer", -1.0, 0.0, 1.1, 2.2)
    waende = {
        "vorne": ((CX, Y0), X1 - X0, [tuer] + [Oeffnung(F, u, **eg) for u in (-3.0, 1.0, 2.7)],
                  [Oeffnung(F, u, **og) for u in (-3.0, -1.7, 1.7, 3.0)]),
        "rechts": ((X1, CY), Y1 - Y0, [Oeffnung(F, 1.6, **eg)], [Oeffnung(F, u, **og) for u in (-1.6, 1.6)]),
        "links": ((X0, CY), Y1 - Y0, [Oeffnung(F, u, **eg) for u in (-1.6, 1.6)], [Oeffnung(F, u, **og) for u in (-1.6, 1.6)]),
        "hinten": ((CX, Y1), X1 - X0, [Oeffnung("tuer", 1.6, 0.0, 1.0, 2.1), Oeffnung(F, -1.6, **eg)],
                   [Oeffnung(F, u, **og) for u in (-2.2, 0.0, 2.2)]),
    }
    M_vorn = None
    for seite, ((x, y), L, unten, oben) in waende.items():
        for z, hoehe, liste, kreuze in ((Z_SOCKEL, H_EG, unten, False), (Z_OG, H_OG, oben, True)):
            M = bs.wandmatrix((x, y, z), seite)
            if seite == "vorne" and z == Z_SOCKEL:
                M_vorn = M
            bs.fachwerk(haus, M, L, hoehe, liste, st, kreuze=kreuze, saat=len(seite) + int(z))
            for o in liste:
                if o.art == "fenster":
                    bs.fenster(haus, M, o, st)
                    laeden(haus, st, M, o)
                    if seite in ("vorne", "rechts") and (z == Z_OG or seite == "vorne"):
                        blumenkasten(haus, st, M, o, saat=int(o.u * 10) + int(z))
                oeffnungen.append((M, o))
    bs.tuer(haus, M_vorn, tuer, st)
    bs.tuer(haus, bs.wandmatrix((CX, Y1, Z_SOCKEL), "hinten"), waende["hinten"][2][0], st)
    haus.quader((CX + tuer.u, Y0 - 0.2, (Z_SOCKEL + 0.01) / 2), (1.5, 0.4, Z_SOCKEL + 0.01), st["Sandstein"])
    haus.quader((CX + tuer.u, Y0 - 0.5, 0.1), (1.5, 0.2, 0.2), st["Sandstein"])
    zunftzeichen(haus, st, M_vorn, tuer.u + 0.95, 2.75)

    # ── Ziegeldach mit der Traufe zur Gasse, Zwerchhaus, Giebel, Schornstein ──
    dach = bs.satteldach(haus, st, X0, X1, Y0, Y1, Z_TRAUFE, NEIGUNG, traufe=0.35, ortgang=0.3, **bs.ZIEGEL)
    hoch = (Y1 - Y0) / 2 * dach["tan"]
    for x, seite in ((X0, "links"), (X1, "rechts")):
        bs.fachwerkgiebel(haus, st, bs.wandmatrix((x, CY, Z_TRAUFE), seite), Y1 - Y0, hoch, oeffnungen)
    zwerchhaus(haus, st, oeffnungen)
    xs, ys = CX - 2.2, CY + 1.2
    z0 = dach["oben_hinten"](ys) - 0.3
    haus.quader((xs, ys, (z0 + dach["z_first"] + 0.7) / 2), (0.6, 0.6, dach["z_first"] + 0.7 - z0), st["Bruchstein dunkel"])
    haus.quader((xs, ys, dach["z_first"] + 0.76), (0.78, 0.78, 0.12), st["Bruchstein"])

    # ── Destillierkessel an der Ostseite (wie bisher im Spiel) ────────────────
    destille(haus, st, (2.2, -2.4, 0))

    # ── Wappen, Fahne ─────────────────────────────────────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, bs.wandmatrix((CX, Y0, Z_OG), "vorne"), 0.0, 1.2, st, groesse=0.7)
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (X0 + 1.8, dach["y_first"], dach["z_first"] - 0.25), hoehe=2.4)

    # ── Stufen ────────────────────────────────────────────────────────────────
    stufe1 = bs.Netz("Stufe1")
    siederei(stufe1, st)
    stufe2 = bs.Netz("Stufe2")
    duftwerkstatt(stufe2, st)
    bs.gaube(stufe2, st, dach, [], x0=X0 + 1.0, x1=X0 + 2.4, y_front=Y0 + 1.1)
    stufe3 = bs.Netz("Stufe3")
    parfumerie(stufe3, st)
    bs.gaube(stufe3, st, dach, [], x0=X0 + 1.0, x1=X0 + 2.4, y_front=Y0 + 1.1)
    bs.dachreiter(stufe3, st, Vector((CX - 2.4, dach["y_first"], dach["z_first"])), spitze="Lavendel Spitze")

    # ── Leerstand, Gerüst, Anker ──────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (Mo, o) in enumerate(oeffnungen):
        bs.bretter(leer, Mo, o, st, i)
    for x, y in ((X0 + 0.3, Y0 - 0.15), (-2.2, Y0 - 1.3), (X1 - 0.2, Y0 - 0.15), (X0 - 0.15, -1.0), (X1 + 0.15, 0.6)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=X0 - 0.15, x1=X1 + 0.15, y_innen=Y0 - 0.65, y_aussen=Y0 - 1.5,
                  ebenen=(2.4, 4.8), hoehe_innen=5.4, hoehe_aussen=6.4, felder=3)
    anker = [bs.anker("Anker_Tuer", (CX + tuer.u, Y0 - 0.2, Z_SOCKEL + 0.01)),
             bs.anker("Anker_Karren", (2.2, Y0 - 1.6, 0.0))]

    objekte = [haus.objekt(), stufe1.objekt(), stufe2.objekt(), stufe3.objekt(), wappen.objekt(), fahne.objekt(),
               leer.objekt(), geruest.objekt()] + anker
    for o in objekte:
        if o.name in ("Stufe2", "Stufe3", "Leerstand", "Geruest"):
            bs.zustand(o)
        elif o.name == "Stufe1":
            o["zustand"] = True
    return objekte


# ─────────────────────────────────────────────────────────────────────────────
#  Teile des Hauses
# ─────────────────────────────────────────────────────────────────────────────

def laeden(netz, st, M, o, winkel=55):
    """Zwei halb geöffnete Fensterläden in Lila (wie bisher im Spiel)."""
    breite = o.b / 2
    for s in (-1, 1):
        dreh = -winkel if s > 0 else 180 + winkel
        t = (M @ Matrix.Translation((o.u + s * o.b / 2, -bs.VOR_PFOSTEN - 0.012, o.v + o.h / 2))
             @ Matrix.Rotation(math.radians(dreh), 4, "Z"))
        netz.quader((breite / 2, 0, 0), (breite, 0.035, o.h), st["Laden lila"], t)


def blumenkasten(netz, st, M, o, saat=0):
    """Blumenkasten unter dem Fenster mit Lavendel und Jasmin (wie bisher im Spiel)."""
    zufall = random.Random(saat)
    netz.quader((o.u, -0.22, o.v - 0.14), (o.b + 0.2, 0.3, 0.24), st["Holz"], M, ohne=("+y",))
    n = 4
    for k in range(n):
        u = o.u - o.b / 2 + (o.b) * (k + 0.5) / n
        stoff = "Lavendel" if k % 2 == 0 else "Jasmin"
        h = zufall.uniform(0.22, 0.32)
        netz.quader(M @ Vector((u, -0.22, o.v - 0.02 + h / 2)), (0.2, 0.2, h), st["Laub"])
        netz.quader(M @ Vector((u, -0.22, o.v - 0.02 + h + 0.05)), (0.17, 0.17, 0.1), st[stoff])


def zunftzeichen(netz, st, M, u, v):
    """Zunftzeichen am Ausleger: ein Flakon aus Kupfer mit lila Stöpsel."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -1.0, v)), 0.05, 0.05, eisen)
    netz.stab(M @ Vector((u, 0, v - 0.5)), M @ Vector((u, -0.7, v - 0.02)), 0.04, 0.04, eisen)
    m = M @ Vector((u, -0.7, v - 0.75))
    netz.stab(m + Vector((0, 0, 0.75)), m + Vector((0, 0, 0.6)), 0.02, 0.02, eisen)
    bs.achteck(netz, st, m, 0.22, 0.0, 0.32, "Kupfer blank")
    bs.achteck(netz, st, m, 0.08, 0.32, 0.48, "Kupfer blank")
    bs.achteck(netz, st, m, 0.11, 0.48, 0.6, "Laden lila")


def zwerchhaus(netz, st, oeffnungen):
    """Zwerchhaus in der Mitte der Gassenseite: die Wand läuft über die Traufe, Fachwerk mit Fenster,
    eigenes Satteldach mit Fachwerkgiebel, das in das Hauptdach einschneidet."""
    tief = 1.6
    netz.quader((CX, Y0 + tief / 2, Z_TRAUFE + H_ZW / 2), (2 * ZW, tief, H_ZW), st["Putz"], ohne=("+z", "+y", "-z"))
    M = bs.wandmatrix((CX, Y0, Z_TRAUFE), "vorne")
    o = Oeffnung("fenster", 0.0, 0.25, 0.8, 1.0, laeden=False)
    bs.fachwerk(netz, M, 2 * ZW, H_ZW, [o], st, kreuze=True, saat=9)
    bs.fenster(netz, M, o, st)
    laeden(netz, st, M, o)
    oeffnungen.append((M, o))
    drehung = Matrix.Translation((CX, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(netz, st, Y0, Y0 + 3.0, -ZW, ZW, Z_TRAUFE + H_ZW, 50, traufe=0.25, ortgang=0.25,
                         matrix=drehung, **bs.ZIEGEL)
    # kleiner Giebel: Putz, Schwelle, Firstsäule und zwei Streben (der große Fachwerkgiebel passt hier nicht)
    Mg = bs.wandmatrix((CX, Y0, Z_TRAUFE + H_ZW), "vorne")
    hoch = ZW * dach["tan"]
    bs.platte(netz, Mg, [(-ZW, 0), (ZW, 0), (0, hoch)], 0, 0.3, st["Putz"])
    holz = st["Fachwerk"]
    bs.balken(netz, Mg, -ZW + 0.05, bs.B / 2, ZW - 0.05, bs.B / 2, holz, vor=bs.VOR_SCHWELLE, enden=True)
    bs.balken(netz, Mg, 0, bs.B, 0, hoch - 0.3, holz)
    for s in (-1, 1):
        bs.balken(netz, Mg, s * 0.12, bs.B + 0.1, s * ZW * 0.6, hoch * 0.4 - 0.1, holz, vor=bs.VOR_STREBE)


def destille(netz, st, fuss):
    """Destillierkessel aus blankem Kupfer auf einer gemauerten Feuerstelle (Glut im Schürloch), Helm, Kühlrohr
    zum Kühlfass, Vorlage auf einem Bock; Brennholz an der Hauswand."""
    x, y = fuss[0], fuss[1]
    netz.quader((x, y, 0.45), (1.6, 1.6, 0.9), st["Bruchstein"])
    netz.vieleck_nach([(x - 0.3, y - 0.803, 0.15), (x + 0.3, y - 0.803, 0.15), (x + 0.3, y - 0.803, 0.55), (x - 0.3, y - 0.803, 0.55)],
                      (0, -1, 0), st["Glut"])
    m = Vector((x, y, 0.9))
    for r, z0, z1 in ((0.62, 0.0, 0.35), (0.72, 0.35, 0.8), (0.55, 0.8, 1.05), (0.32, 1.05, 1.2)):
        bs.achteck(netz, st, m, r, z0, z1, "Kupfer blank", n=10)
    helm = m + Vector((0, 0, 1.2))
    bs.achteck(netz, st, helm, 0.22, 0.0, 0.35, "Kupfer blank", n=8)
    spitze = helm + Vector((0, 0, 0.75))
    ecken = [helm + Vector((0.3 * math.cos(math.radians(45 * k + 22.5)), 0.3 * math.sin(math.radians(45 * k + 22.5)), 0.35))
             for k in range(8)]
    for k in range(8):
        a, b = ecken[k], ecken[(k + 1) % 8]
        netz.vieleck_nach([a, b, spitze], (a + b) / 2 - (helm + Vector((0, 0, 0.35))), st["Kupfer blank"])
    fass = Vector((x + 1.5, y + 1.2, 0))
    netz.stab(spitze - Vector((0, 0, 0.15)), fass + Vector((-0.1, -0.1, 1.15)), 0.08, 0.08, st["Kupfer blank"])
    bs.fass(netz, st, fass, r=0.45, h=1.1)
    bs.rund_oben(netz, st, fass, 0.4, 1.04, "Wasser")
    netz.stab(fass + Vector((0.3, 0.3, 0.3)), fass + Vector((0.75, 0.6, 0.55)), 0.05, 0.05, st["Kupfer blank"])
    netz.quader(fass + Vector((0.85, 0.7, 0.25)), (0.4, 0.4, 0.5), st["Holz alt"])
    bs.achteck(netz, st, fass + Vector((0.85, 0.7, 0.5)), 0.14, 0.0, 0.2, "Lavendel", n=6)
    bs.achteck(netz, st, fass + Vector((0.85, 0.7, 0.7)), 0.05, 0.0, 0.12, "Lavendel", n=6)
    # Brennholz an der Hauswand
    for k in range(3):
        netz.quader((X1 + 0.5, y + 1.2, 0.09 + 0.17 * k), (0.5, 1.4 - 0.1 * k, 0.16), st["Holz"])


def strauch(netz, st, fuss, bluete, saat=0, r=0.24):
    """Kleiner Strauch: grüner Busch, oben Blüten (Lavendel, Jasmin, Rose)."""
    zufall = random.Random(saat)
    h = zufall.uniform(0.3, 0.42)
    bs.achteck(netz, st, Vector(fuss), r, 0.0, h, "Laub", n=5)
    bs.achteck(netz, st, Vector(fuss), r * 0.8, h, h + 0.14, bluete, n=5)


def beet(netz, st, x0, x1, y0, y1, bluete, saat=0, abstand=0.62):
    """Beet mit Bretterrand und Erde, darin Reihen von Sträuchern."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    for y in (y0, y1):
        netz.quader((cx, y, 0.1), (x1 - x0, 0.06, 0.2), st["Holz alt"])
    for x in (x0, x1):
        netz.quader((x, cy, 0.1), (0.06, y1 - y0 - 0.06, 0.2), st["Holz alt"])
    netz.quader((cx, cy, 0.07), (x1 - x0 - 0.06, y1 - y0 - 0.06, 0.14), st["Lehm"], ohne=("-z",))
    zufall = random.Random(saat)
    x = x0 + abstand / 2
    while x < x1 - 0.2:
        y = y0 + abstand / 2
        while y < y1 - 0.2:
            strauch(netz, st, (x + zufall.uniform(-0.05, 0.05), y, 0.14), bluete if callable(bluete) is False else bluete(),
                    saat=zufall.randint(0, 999))
            y += abstand
        x += abstand


def trockengestell(netz, st, mitte, breite=2.4, dreh=0.0, n=6):
    """Gestell mit Lavendel- und Jasminbündeln zum Trocknen."""
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(math.radians(dreh), 4, "Z")
    for s in (-1, 1):
        netz.stab(t @ Vector((s * breite / 2, 0, 0)), t @ Vector((s * breite / 2, 0, 2.0)), 0.1, 0.1, st["Holz alt"])
    netz.stab(t @ Vector((-breite / 2 - 0.1, 0, 1.95)), t @ Vector((breite / 2 + 0.1, 0, 1.95)), 0.08, 0.08, st["Holz"])
    for k in range(n):
        p = t @ Vector((-breite / 2 + breite * (k + 0.5) / n, 0, 1.9))
        netz.stab(p, p - Vector((0, 0, 0.12)), 0.02, 0.02, st["Seil"])
        netz.quader(p - Vector((0, 0, 0.38)), (0.16, 0.16, 0.5), st["Lavendel" if k % 2 == 0 else "Jasmin"])


def spindelpresse(netz, st, fuss):
    """Spindelpresse für die Blüten: zwei Ständer mit Querhaupt, Spindel mit Hebel, Pressbett mit Korb und Rinne."""
    x, y = fuss[0], fuss[1]
    for dx in (-0.7, 0.7):
        netz.quader((x + dx, y, 1.2), (0.22, 0.3, 2.4), st["Fachwerk"])
    netz.quader((x, y, 2.3), (1.8, 0.34, 0.3), st["Fachwerk"])
    netz.quader((x, y, 0.4), (1.6, 1.0, 0.2), st["Holz"])
    bs.fass(netz, st, Vector((x, y, 0.5)), r=0.38, h=0.55, reifen=(0.1, 0.42), stoff="Holz alt")
    bs.achteck(netz, st, Vector((x, y, 1.05)), 0.32, 0.0, 0.12, "Holz", n=8)
    bs.achteck(netz, st, Vector((x, y, 1.17)), 0.09, 0.0, 1.0, "Holz", n=8)
    netz.stab((x - 0.9, y + 0.1, 1.55), (x + 0.9, y - 0.1, 1.55), 0.07, 0.07, st["Holz"])
    netz.stab((x + 0.8, y, 0.3), (x + 1.2, y, 0.18), 0.12, 0.08, st["Holz alt"])
    bs.fass(netz, st, Vector((x + 1.35, y, 0)), r=0.2, h=0.35, reifen=(), stoff="Holz")


def rosenbogen(netz, st, mitte, breite=1.6, h=2.4):
    """Rosenbogen am Hoftor: zwei Pfosten, ein Bogen aus Latten, darauf Laub und Rosen."""
    x, y = mitte
    for s in (-1, 1):
        netz.stab((x + s * breite / 2, y, 0), (x + s * breite / 2, y, h), 0.1, 0.1, st["Holz alt"])
    teile = 6
    pts = [Vector((x + breite / 2 * math.cos(math.pi * k / teile), y, h + 0.5 * math.sin(math.pi * k / teile))) for k in range(teile + 1)]
    for a, b in zip(pts, pts[1:]):
        netz.stab(a, b, 0.12, 0.3, st["Laub"], quer=(0, 1, 0), mehr=0.05)
    for k, p in enumerate(pts[1:-1]):
        netz.quader(p + Vector((0, -0.16, 0.06)), (0.14, 0.06, 0.14), st["Rose"])
    for s in (-1, 1):
        for z in (0.7, 1.4, 2.0):
            netz.quader((x + s * breite / 2, y - 0.08, z), (0.18, 0.06, 0.4), st["Laub"])
            netz.quader((x + s * breite / 2, y - 0.12, z + 0.1), (0.1, 0.03, 0.1), st["Rose"])


def werkstatt(netz, st):
    """Duftwerkstatt als Anbau hinten rechts: Fachwerk auf Sockel, Ziegeldach mit Giebel nach rechts, Tür und Fenster."""
    x0, x1, y0, y1 = 1.2, 5.6, 2.0, 5.6
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    h = 3.0
    netz.quader((cx, cy, 0.15), (x1 - x0 + 0.1, y1 - y0 + 0.1, 0.3), st["Bruchstein"])
    netz.quader((cx, cy, 0.3 + h / 2), (x1 - x0, y1 - y0, h), st["Putz"], ohne=("+z",))
    tuer = Oeffnung("tuer", -1.3, 0.0, 1.1, 2.1)
    fenster = [Oeffnung("fenster", u, 0.9, 0.8, 1.1, laeden=False) for u in (0.2, 1.55)]
    M = bs.wandmatrix((cx, y0, 0.3), "vorne")
    bs.fachwerk(netz, M, x1 - x0, h, [tuer] + fenster, st, saat=21)
    bs.tuer(netz, M, tuer, st)
    for o in fenster:
        bs.fenster(netz, M, o, st)
        laeden(netz, st, M, o)
        blumenkasten(netz, st, M, o, saat=int(o.u * 7))
    M_r = bs.wandmatrix((x1, cy, 0.3), "rechts")
    fr = [Oeffnung("fenster", 0.0, 0.9, 0.9, 1.1, laeden=False)]
    bs.fachwerk(netz, M_r, y1 - y0, h, fr, st, saat=22)
    bs.fenster(netz, M_r, fr[0], st)
    laeden(netz, st, M_r, fr[0])
    for seite, (x, y), L, saat in (("hinten", (cx, y1), x1 - x0, 23), ("links", (x0, cy), y1 - y0, 24)):
        bs.fachwerk(netz, bs.wandmatrix((x, y, 0.3), seite), L, h, [], st, saat=saat)
    dach = bs.satteldach(netz, st, x0, x1, y0, y1, 0.3 + h, 45, traufe=0.3, ortgang=0.25, **bs.ZIEGEL)
    for x, seite in ((x1, "rechts"), (x0, "links")):
        bs.fachwerkgiebel(netz, st, bs.wandmatrix((x, cy, 0.3 + h), seite), y1 - y0, (y1 - y0) / 2 * dach["tan"], [])
    netz.quader((cx + tuer.u, y0 - 0.2, 0.155), (1.4, 0.4, 0.31), st["Sandstein"])


def pavillon(netz, st, mitte, r=1.5, h=2.5):
    """Achteckiger Duftpavillon: Steinsockel, Pfosten mit Brüstung aus Latten, Zeltdach in Lila mit goldenem Knauf,
    darin ein Tisch mit Flakons."""
    m = Vector(mitte)
    bs.achteck(netz, st, m, r + 0.15, 0.0, 0.25, "Sandstein")

    def punkt(radius, k, z):
        w = math.radians(22.5 + 45 * k)
        return m + Vector((radius * math.cos(w), radius * math.sin(w), z))

    for k in range(8):
        netz.stab(punkt(r, k, 0.25), punkt(r, k, h), 0.12, 0.12, st["Holz"])
        if k != 6:                                                     # Eingang zur Gasse
            netz.stab(punkt(r, k, 0.95), punkt(r, k + 1, 0.95), 0.08, 0.08, st["Holz"])
            for f in (0.25, 0.5, 0.75):
                a = punkt(r, k, 0.25).lerp(punkt(r, k + 1, 0.25), f)
                netz.stab(a, a + Vector((0, 0, 0.7)), 0.04, 0.04, st["Holz alt"])
        netz.stab(punkt(r, k, h), punkt(r, k + 1, h), 0.12, 0.12, st["Holz"])
    gipfel = m + Vector((0, 0, h + 1.6))
    for k in range(8):
        a, b = punkt(r + 0.3, k, h - 0.1), punkt(r + 0.3, k + 1, h - 0.1)
        netz.vieleck_nach([a, b, gipfel], (a + b) / 2 - (m + Vector((0, 0, h))), st["Laden lila"])
    bs.achteck(netz, st, gipfel - Vector((0, 0, 0.1)), 0.1, 0.0, 0.3, "Gold", n=8)
    bs.achteck(netz, st, m + Vector((0, 0, 0.25)), 0.08, 0.0, 0.7, "Holz", n=6)
    bs.achteck(netz, st, m + Vector((0, 0, 0.95)), 0.5, 0.0, 0.06, "Holz", n=8)
    for k, stoff in enumerate(("Lavendel", "Rose", "Jasmin", "Kupfer blank", "Lavendel")):
        w = math.radians(72 * k)
        p = m + Vector((0.28 * math.cos(w), 0.28 * math.sin(w), 1.01))
        bs.achteck(netz, st, p, 0.06, 0.0, 0.14, stoff, n=6)
        bs.achteck(netz, st, p, 0.025, 0.14, 0.22, stoff, n=6)


def springbrunnen(netz, st, mitte):
    """Kleiner Springbrunnen: achteckiges Becken mit Wasser, Säule mit Schale."""
    m = Vector(mitte)
    bs.achteck(netz, st, m, 0.95, 0.0, 0.5, "Sandstein")
    bs.rund_oben(netz, st, m, 0.82, 0.503, "Wasser")
    bs.achteck(netz, st, m, 0.12, 0.5, 1.4, "Sandstein")
    bs.achteck(netz, st, m, 0.38, 1.4, 1.55, "Sandstein")
    bs.rund_oben(netz, st, m, 0.32, 1.553, "Wasser")


# ─────────────────────────────────────────────────────────────────────────────
#  Stufen
# ─────────────────────────────────────────────────────────────────────────────

def zaun(netz, st, tor=False):
    if tor:
        bs.zaun(netz, st, [(6.4, -4.6), (7.8, -4.6), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])
    else:
        bs.zaun(netz, st, [(7.8, -4.6), (7.8, 6.8), (-7.8, 6.8), (-7.8, Y1 + 0.3)])


def siederei(netz, st):
    """Stufe 1: Beete mit Lavendel und Jasmin, Trockengestell, Zaun."""
    beet(netz, st, 4.6, 7.4, -3.8, -1.0, "Lavendel", saat=1)
    beet(netz, st, 4.6, 7.4, -0.2, 2.6, "Jasmin", saat=2)
    beet(netz, st, 1.4, 4.0, 3.2, 6.2, "Lavendel", saat=3)
    beet(netz, st, 4.6, 7.4, 3.4, 6.2, "Jasmin", saat=4)
    trockengestell(netz, st, (3.6, 1.6, 0), dreh=90)
    zaun(netz, st)


def duftwerkstatt(netz, st):
    """Stufe 2: Werkstatt als Anbau hinten, Spindelpresse, Rosenbogen am Hoftor, Beete, Trockengestell, Zaun."""
    werkstatt(netz, st)
    spindelpresse(netz, st, (5.6, 0.6, 0))
    beet(netz, st, 4.6, 7.4, -3.8, -1.6, "Lavendel", saat=5)
    beet(netz, st, 6.2, 7.4, 2.2, 6.2, "Jasmin", saat=6, abstand=0.58)
    trockengestell(netz, st, (3.6, 0.6, 0), breite=1.8, dreh=90, n=4)
    rosenbogen(netz, st, (5.6, -4.6))
    zaun(netz, st, tor=True)
    bs.zaun(netz, st, [(X1 + 0.15, -4.6), (4.8, -4.6)])


def parfumerie(netz, st):
    """Stufe 3: Werkstatt, Duftpavillon, Springbrunnen, Rosenbeete, Rosenbogen, Zaun."""
    werkstatt(netz, st)
    pavillon(netz, st, (6.0, -2.4, 0))
    springbrunnen(netz, st, (5.2, 0.8, 0))
    beet(netz, st, 6.2, 7.4, 2.2, 6.2, "Rose", saat=7, abstand=0.58)
    beet(netz, st, 3.0, 4.1, 0.3, 1.7, "Lavendel", saat=8, abstand=0.55)
    rosenbogen(netz, st, (5.6, -4.6))
    zaun(netz, st, tor=True)
    bs.zaun(netz, st, [(X1 + 0.15, -4.6), (4.8, -4.6)])


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
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=4.4, ausschnitt=27.0, schnell=schnell)
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
