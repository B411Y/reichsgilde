"""Steinhaus – dritte Stufe des Wohnhauses der Spielhäuser (Stand Patrizier). Belegte Fläche 8 × 10 m.

    blender -b --python tools/modelle/steinhaus.py        oder
    python tools/modelle/steinhaus.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/steinhaus.glb und Bilder nach docs/modelle/.

Objekte im GLB wie bei den anderen Wohnhäusern: Haus, Wappen, Fahne, Leerstand und Geruest
(die beiden letzten standardmäßig aus).

Ein Patrizierhaus aus Stein (Blender: Z oben, Gasse bei -Y):
    7 × 7,4 m, drei Geschosse aus Haustein mit Lagerfugen, Eckquader und Gesimse aus Sandstein,
    Treppengiebel zur Gasse und zum Hof, steiles Schieferdach mit Gauben an beiden Traufseiten,
    Rundbogenportal mit Freitreppe, Steinfenster mit Kreuzstock in der Hausfarbe,
    großes Wappen über dem Portal, Laterne am Portal.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "steinhaus"
FLAECHE = (8.0, 10.0)

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
HX, VORN, HINTEN = 3.5, -3.0, 4.4         # x ±3,5, y von -3 bis 4,4
Z_SOCKEL = 0.5
GESCHOSSE = (3.1, 2.8, 2.6)               # Erdgeschoss, 1. und 2. Obergeschoss
Z_TRAUFE = Z_SOCKEL + sum(GESCHOSSE)      # 9,0
H_WAND = sum(GESCHOSSE)
NEIGUNG = 55
GIEBEL_DICKE = 0.45


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []
    y_mitte = (VORN + HINTEN) / 2
    tief = HINTEN - VORN
    v1, v2 = GESCHOSSE[0], GESCHOSSE[0] + GESCHOSSE[1]       # Geschossgrenzen im Wandsystem

    # ── Sockel, Baukörper, Gesimse, Eckquader ─────────────────────────────────
    haus.quader((0, y_mitte, Z_SOCKEL / 2), (2 * HX + 0.12, tief + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    haus.quader((0, y_mitte, Z_SOCKEL + H_WAND / 2), (2 * HX, tief, H_WAND), st["Haustein"], ohne=("+z",))
    for z, h, vor in ((Z_SOCKEL - 0.06, 0.12, 0.09), (Z_SOCKEL + v1, 0.16, 0.07), (Z_SOCKEL + v2, 0.16, 0.07),
                      (Z_TRAUFE, 0.26, 0.13)):
        bs.gesims(haus, st, -HX, HX, VORN, HINTEN, z, hoehe=h, vor=vor)
    for sx in (-1, 1):
        for sy, y in ((-1, VORN), (1, HINTEN)):
            bs.eckquader(haus, st, sx * HX, y, sx, sy, Z_SOCKEL, Z_TRAUFE)

    # ── Wände: Lagerfugen, Steinfenster, Portal ───────────────────────────────
    M_vorn = bs.wandmatrix((0, VORN, Z_SOCKEL), "vorne")
    M_rechts = bs.wandmatrix((HX, y_mitte, Z_SOCKEL), "rechts")
    M_hinten = bs.wandmatrix((0, HINTEN, Z_SOCKEL), "hinten")
    M_links = bs.wandmatrix((-HX, y_mitte, Z_SOCKEL), "links")
    F = "fenster"
    eg = dict(v=0.95, b=0.95, h=1.4)
    og1 = dict(v=v1 + 0.7, b=0.95, h=1.45)
    og2 = dict(v=v2 + 0.6, b=0.9, h=1.3)

    def fenster(M, liste, verdachung=False):
        for o in liste:
            bs.gewaende(haus, M, o, st, verdachung=verdachung)
            oeffnungen.append((M, o))

    for M, L in ((M_vorn, 2 * HX), (M_rechts, tief), (M_hinten, 2 * HX), (M_links, tief)):
        bs.fugen(haus, M, L - 0.02, 0.0, H_WAND, st)
    fenster(M_vorn, [Oeffnung(F, u, **eg) for u in (-2.2, 2.2)])
    fenster(M_vorn, [Oeffnung(F, u, **og1) for u in (-2.2, 2.2)], verdachung=True)
    fenster(M_vorn, [Oeffnung(F, u, **og2) for u in (-2.2, 0.0, 2.2)])
    for M in (M_rechts, M_links):
        fenster(M, [Oeffnung(F, u, **eg) for u in (-1.9, 1.9)])
        fenster(M, [Oeffnung(F, u, **og1) for u in (-1.9, 1.9)], verdachung=True)
        fenster(M, [Oeffnung(F, u, **og2) for u in (-1.9, 1.9)])
    fenster(M_hinten, [Oeffnung(F, 1.8, **eg)])
    fenster(M_hinten, [Oeffnung(F, u, **og1) for u in (-1.8, 1.8)], verdachung=True)
    fenster(M_hinten, [Oeffnung(F, u, **og2) for u in (-1.8, 1.8)])
    # Rundbogenportal mit Freitreppe, Hintertür mit Sandsteinsturz
    tor = Oeffnung("tuer", 0.0, 0.0, 1.5, 2.75)
    bs.portal(haus, M_vorn, tor, st)
    oeffnungen.append((M_vorn, tor))
    for k, (tiefe, hoehe) in enumerate(((1.0, 0.17), (0.7, 0.34), (0.4, 0.5))):
        haus.quader((0, VORN - tiefe / 2, hoehe / 2), (2.3 - 0.25 * k, tiefe, hoehe), st["Sandstein"])
    hinten = Oeffnung("tuer", -1.6, 0.0, 1.0, 2.1)
    bs.tuer(haus, M_hinten, hinten, st)
    haus.quader((-1.6, -0.03, 2.2), (1.4, 0.1, 0.22), st["Sandstein"], M_hinten, ohne=("+y",))
    oeffnungen.append((M_hinten, hinten))
    haus.quader((1.6, HINTEN + 0.25, 0.25), (1.3, 0.5, 0.5), st["Sandstein"])
    laterne(haus, st, M_vorn, 1.35, 2.55)

    # ── Schieferdach (First quer zur Gasse), Treppengiebel, Gauben, Schornstein ──
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")      # lokal x → Welt y, lokal y → Welt -x
    dach = bs.satteldach(haus, st, VORN, HINTEN, -HX, HX, Z_TRAUFE + 0.26, NEIGUNG, traufe=0.3, ortgang=-0.25,
                         enden="Haustein", first_dicke=0.2, matrix=drehung, **bs.SCHIEFER)
    oben = treppengiebel(haus, st, bs.wandmatrix((0, VORN, Z_TRAUFE), "vorne"), dach, oeffnungen)
    treppengiebel(haus, st, bs.wandmatrix((0, HINTEN, Z_TRAUFE), "hinten"), dach, oeffnungen)
    bs.gaube(haus, st, dach, oeffnungen, x0=0.0, x1=1.5, y_front=-HX + 1.15, hoehe=1.35, art="stein")
    dach_hinten = dict(dach, matrix=dach["matrix"] @ Matrix.Rotation(math.radians(180), 4, "Z"))
    bs.gaube(haus, st, dach_hinten, oeffnungen, x0=-2.0, x1=-0.5, y_front=-HX + 1.15, hoehe=1.35, art="stein")
    xs, ys = -1.5, 2.6                              # Schornstein auf der linken Dachfläche
    z_dach = dach["oben_hinten"](-xs) - 0.3          # lokal y = -Welt x
    hoehe = dach["z_first"] + 0.9 - z_dach
    haus.quader((xs, ys, z_dach + hoehe / 2), (0.7, 0.7, hoehe), st["Haustein"])
    haus.quader((xs, ys, z_dach + hoehe + 0.06), (0.9, 0.9, 0.12), st["Sandstein"])

    # ── Wappen über dem Portal, Fahne auf dem vorderen Giebel ─────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, M_vorn, 0.0, v1 + 1.45, st, groesse=1.45, rahmen="Sandstein")
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (0.0, VORN + GIEBEL_DICKE / 2, Z_TRAUFE + oben), hoehe=2.4)

    # ── Leerstand ─────────────────────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for seite, x, s, r in (("vorn", 0.0, 2.4, 0.4), ("vorn", 3.0, 4.6, 0.25), ("hinten", 1.5, 3.3, 0.4)):
        bs.dachloch(leer, st, dach, seite, x, s, r)
    for x, y in ((-3.3, VORN - 0.15), (1.4, VORN - 1.1), (HX + 0.15, -1.2), (HX + 0.14, 2.8), (-HX - 0.15, 0.4),
                 (0.3, HINTEN + 0.15)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)

    # ── Gerüst vor der Gassenseite über alle drei Geschosse ──────────────────
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=-HX - 0.15, x1=HX + 0.15, y_innen=VORN - 0.3, y_aussen=VORN - 1.2,
                  ebenen=(2.5, 5.0, 7.5), hoehe_innen=9.4, hoehe_aussen=10.0)

    return [haus.objekt(), wappen.objekt(), fahne.objekt(), bs.zustand(leer.objekt()), bs.zustand(geruest.objekt())]


def treppengiebel(netz, st, M, dach, oeffnungen, stufen=5, oben_halb=0.45):
    """Treppengiebel über der Traufe: Mauerscheibe mit Stufen, die über das Dach hinausragen,
    Sandsteinplatten auf jeder Stufe, Lagerfugen und drei kleine Fenster. Gibt die Höhe des
    obersten Blocks über der Traufe zurück (für die Fahne)."""
    th = math.radians(NEIGUNG)
    tan = math.tan(th)
    z_dach0 = 0.26 + dach["dicke"] / math.cos(th)       # Dachoberfläche über der Traufe an der Wandkante

    def dach_bei(a):                                     # Höhe der Dachoberfläche bei |u| = a
        return z_dach0 + (HX - a) * tan

    grenzen = [HX - k * (HX - oben_halb) / stufen for k in range(stufen + 1)]   # von außen nach innen
    hoehen = [dach_bei(grenzen[k + 1]) + 0.35 for k in range(stufen)]
    spitze = dach_bei(0) + 0.75
    rechts = [(HX, 0)]
    for k in range(stufen):
        rechts += [(grenzen[k], hoehen[k]), (grenzen[k + 1], hoehen[k])]
    rechts += [(oben_halb, spitze)]
    umriss = rechts + [(-u, v) for u, v in reversed(rechts)]
    bs.platte(netz, M, umriss, 0, GIEBEL_DICKE, st["Haustein"])
    # Sandsteinplatten auf den Stufen und dem obersten Block
    for k in range(stufen):
        b = grenzen[k] - grenzen[k + 1]
        for s in (-1, 1):
            netz.quader((s * (grenzen[k] + grenzen[k + 1]) / 2, GIEBEL_DICKE / 2, hoehen[k] + 0.05),
                        (b + 0.12, GIEBEL_DICKE + 0.12, 0.1), st["Sandstein"], M)
    netz.quader((0, GIEBEL_DICKE / 2, spitze + 0.06), (2 * oben_halb + 0.14, GIEBEL_DICKE + 0.14, 0.12), st["Sandstein"], M)
    # Lagerfugen: je Höhe so breit, wie die Stufen dort reichen
    v = 0.42
    while v < spitze - 0.1:
        breite = max([g for g, h in zip(grenzen, hoehen + [spitze]) if h > v + 0.05] + [oben_halb])
        if v > hoehen[-1]:
            breite = oben_halb
        netz.vieleck([M @ Vector(p) for p in ((-breite + 0.01, -0.003, v - 0.014), (breite - 0.01, -0.003, v - 0.014),
                                               (breite - 0.01, -0.003, v + 0.014), (-breite + 0.01, -0.003, v + 0.014))],
                     st["Fuge"])
        v += 0.42
    for o in (Oeffnung("fenster", -1.25, 0.75, 0.7, 1.05), Oeffnung("fenster", 1.25, 0.75, 0.7, 1.05),
              Oeffnung("fenster", 0.0, 2.55, 0.55, 0.85)):
        bs.gewaende(netz, M, o, st)
        oeffnungen.append((M, o))
    return spitze + 0.12


def laterne(netz, st, M, u, v):
    """Schmiedeeiserne Laterne an einem Wandarm neben dem Portal."""
    eisen = st["Eisen"]
    netz.stab(M @ Vector((u, 0, v + 0.35)), M @ Vector((u, -0.55, v + 0.35)), 0.04, 0.04, eisen)
    netz.stab(M @ Vector((u, 0, v)), M @ Vector((u, -0.45, v + 0.33)), 0.03, 0.03, eisen)
    mitte = M @ Vector((u, -0.5, v + 0.05))
    netz.quader(mitte, (0.22, 0.22, 0.3), st["Glas"])
    netz.quader(mitte + Vector((0, 0, 0.2)), (0.3, 0.3, 0.08), eisen)
    netz.quader(mitte - Vector((0, 0, 0.18)), (0.26, 0.26, 0.05), eisen)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=7.6, ausschnitt=26.0, schnell="--schnell" in sys.argv)
    normal = lambda o: o.name not in ("Leerstand", "Geruest")                     # noqa: E731
    print(bs.rendern(NAME, normal))
    print(bs.rendern(NAME + "-leerstand", lambda o: o.name not in ("Geruest", "Wappen", "Fahne")))
    print(bs.rendern(NAME + "-geruest", lambda o: o.name != "Leerstand"))
    if "--schnell" not in sys.argv:
        print(bs.rundum(NAME, normal))


if __name__ == "__main__":
    main()
