"""Rathaus – Markthalle mit Fachwerk-Ratssaal und Uhrturm (Sonderbau). Belegte Fläche 16 × 10 m.

    blender -b --python tools/modelle/rathaus.py        oder
    python tools/modelle/rathaus.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/rathaus.glb und Bilder nach docs/modelle/.

Objekte im GLB:
    Haus        das ganze Rathaus (die Räume zeigt das Spiel wie bisher in der Raumansicht, die Hülle ist geschlossen)
    Anker_Tuer  in der Markthalle vor dem Portal
Keine Zustände und keine Hausfarbe: Das Rathaus gehört der Stadt (`pruefen.py … --sonderbau`). Läden, Türen und
das Stadtwappen tragen die Stadtfarben Rot und Weiß.

Aufbau (Blender: Z oben, Gasse und Markt bei -Y; Ursprung in der Mitte wie beim Platzhalter, 16 × 10 m):
    Erdgeschoss aus Haustein, vorn und an der linken Seite eine offene Markthalle mit Rundbögen auf Pfeilern; darin
    die Stadtwaage, eine Bank und das Portal zum Rathaus. Darüber der Ratssaal in Fachwerk mit roten Läden und dem
    Stadtwappen, Schieferdach mit der Traufe zum Markt, Fachwerkgiebel, zwei Gauben. Vorn rechts ein Uhrturm aus
    Haustein mit Zifferblättern, Glockenstube und spitzem Helm.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "rathaus"
FLAECHE = (16.0, 10.0)

bs.FARBEN.update({"Pflaster": (0.52, 0.49, 0.44)})       # Boden der Markthalle (nur dieses Modell)

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
HX, VORN, HINTEN = 7.5, -4.5, 4.5        # mit Dachüberstand genau 16 × 10 m
Y_HALLE = -1.5                     # Rückwand der Markthalle
Z_SOCKEL = 0.3
H_EG = 4.2
Z_OG = Z_SOCKEL + H_EG             # 4,5
H_OG = 3.6
Z_TRAUFE = Z_OG + H_OG             # 8,1
NEIGUNG = 50
TX0, TX1, TY0, TY1 = 4.1, HX + 0.25, VORN - 0.25, -1.0     # Uhrturm vorn rechts, steht etwas vor
T_HOCH = 17.0
PFEILER = 0.6
BOGEN_B = (TX0 + HX - 5 * PFEILER) / 4     # vier Bögen zwischen fünf Pfeilern bis zum Turm


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    st["Hausfarbe"] = st["Stadtrot"]           # Läden und Türen der Bausteine in Stadtrot statt in einer Hausfarbe
    haus = bs.Netz("Haus")
    erdgeschoss(haus, st)
    obergeschoss(haus, st)
    uhrturm(haus, st)
    return [haus.objekt(), bs.anker("Anker_Tuer", (-1.8, Y_HALLE - 0.6, 0.15))]


def bogenwand(netz, st, M, L, H, boegen, dicke=0.6, teile=8):
    """Scheibe aus Haustein über die ganze Länge, unten Rundbögen ausgespart (u, Breite, Kämpferhöhe)."""
    halb = L / 2
    umriss = [(-halb, 0.0)]
    for u, b, k in sorted(boegen):
        r = b / 2
        umriss += [(u - r, 0.0), (u - r, k)]
        umriss += [(u + r * math.cos(math.pi - math.pi * i / teile), k + r * math.sin(math.pi - math.pi * i / teile))
                   for i in range(1, teile)]
        umriss += [(u + r, k), (u + r, 0.0)]
    umriss += [(halb, 0.0), (halb, H), (-halb, H)]
    bs.platte(netz, M, umriss, 0.0, dicke, st["Haustein"])
    for u, b, k in boegen:                                       # Bogenring, Schlussstein, Kämpfer
        r = b / 2
        innen = [(u + r * math.cos(math.pi * i / teile), k + r * math.sin(math.pi * i / teile)) for i in range(teile + 1)]
        aussen = [(u + (r + 0.18) * math.cos(math.pi * i / teile), k + (r + 0.18) * math.sin(math.pi * i / teile))
                  for i in range(teile + 1)]
        bs.platte(netz, M, aussen + list(reversed(innen)), -0.06, -0.002, st["Sandstein"])
        netz.quader((u, -0.05, k + r + 0.12), (0.26, 0.1, 0.32), st["Sandstein"], M, ohne=("+y",))
        for s in (-1, 1):
            netz.quader((u + s * (r + 0.1), -0.04, k - 0.08), (0.4, 0.08, 0.16), st["Sandstein"], M, ohne=("+y",))


def erdgeschoss(netz, st):
    stein = st["Haustein"]
    # Steinerner Kern hinter der Halle, Sockel, Boden der Halle
    ym = (Y_HALLE + HINTEN) / 2
    netz.quader((0, ym, Z_SOCKEL / 2), (2 * HX + 0.1, HINTEN - Y_HALLE + 0.1, Z_SOCKEL), st["Bruchstein dunkel"], ohne=("-y",))
    netz.quader((0, ym, (Z_SOCKEL + Z_OG) / 2), (2 * HX, HINTEN - Y_HALLE, H_EG), stein, ohne=("+z",))
    # Boden und Balkendecke der Halle hinter den Bogenwänden (0,6 m dick)
    x0, y0 = -HX + 0.6, VORN + 0.6
    netz.quader(((x0 + TX0) / 2, (y0 + Y_HALLE) / 2, 0.075), (TX0 - x0, Y_HALLE - y0, 0.15), st["Pflaster"], ohne=("-z",))
    netz.quader(((x0 + TX0) / 2, (y0 + Y_HALLE) / 2, Z_OG - 0.15), (TX0 - x0, Y_HALLE - y0, 0.3), st["Fachwerk"],
                ohne=("+z",))
    # Bogenwände: vorn vier Bögen bis zum Turm, links einer
    L = TX0 + HX
    boegen = [(-L / 2 + PFEILER + BOGEN_B / 2 + i * (BOGEN_B + PFEILER), BOGEN_B, 2.6) for i in range(4)]
    bogenwand(netz, st, bs.wandmatrix(((-HX + TX0) / 2, VORN, 0), "vorne"), L, Z_OG, boegen)
    bogenwand(netz, st, bs.wandmatrix((-HX, (VORN + 0.6 + Y_HALLE) / 2, 0), "links"), Y_HALLE - VORN - 0.6, Z_OG,
              [(0.0, 1.4, 2.4)])                                    # stößt an die vordere Bogenwand
    # Rückwand der Halle: Portal, Fenster, Lagerfugen
    M = bs.wandmatrix((0, Y_HALLE, Z_SOCKEL), "vorne")
    bs.fugen(netz, M, 2 * HX - 0.02, 0.0, H_EG, st)
    tor = Oeffnung("tuer", -1.8, 0.0, 1.6, 3.0)
    bs.portal(netz, M, tor, st)
    for u in (-5.6, 1.4):
        bs.gewaende(netz, M, Oeffnung("fenster", u, 1.0, 1.0, 1.5), st, kreuz=False, gitter=True)
    # Stadtwaage an einem Balken, Gewichte, Bank
    waage = Vector((1.4, (VORN + Y_HALLE) / 2, 0))
    netz.stab(waage + Vector((0, 0, Z_OG - 0.3)), waage + Vector((0, 0, 2.4)), 0.04, 0.04, st["Eisen"])
    netz.quader(waage + Vector((0, 0, 2.4)), (1.4, 0.08, 0.08), st["Eisen"])
    for s in (-1, 1):
        p = waage + Vector((s * 0.65, 0, 0))
        netz.stab(p + Vector((0, 0, 2.4)), p + Vector((0, 0, 1.6)), 0.02, 0.02, st["Eisen"])
        bs.achteck(netz, st, p + Vector((0, 0, 1.52)), 0.28, 0.0, 0.08, "Kupfer", n=8)
    for k in range(3):
        bs.achteck(netz, st, waage + Vector((-0.3 + 0.3 * k, 0.8, 0.15)), 0.1 + 0.03 * k, 0.0, 0.12 + 0.05 * k, "Eisen", n=6)
    holz = st["Holz"]
    netz.quader((-5.6, Y_HALLE - 0.35, 0.6), (2.0, 0.4, 0.08), holz)
    for x in (-6.4, -4.8):
        netz.quader((x, Y_HALLE - 0.35, 0.37), (0.1, 0.34, 0.45), holz)
    # Seiten und Rückseite: Fenster mit Gittern, Hintertür
    for M, L, fenster in ((bs.wandmatrix((HX, (Y_HALLE + HINTEN) / 2, Z_SOCKEL), "rechts"), HINTEN - Y_HALLE, (-1.6, 1.6)),
                          (bs.wandmatrix((-HX, (Y_HALLE + HINTEN) / 2, Z_SOCKEL), "links"), HINTEN - Y_HALLE, (-1.6, 1.6)),
                          (bs.wandmatrix((0, HINTEN, Z_SOCKEL), "hinten"), 2 * HX, (-5.5, -2.2, 2.2, 5.5))):
        bs.fugen(netz, M, L - 0.02, 0.0, H_EG, st)
        for u in fenster:
            bs.gewaende(netz, M, Oeffnung("fenster", u, 1.2, 0.9, 1.3), st, kreuz=False, gitter=True)
    hinten = Oeffnung("tuer", 0.0, 0.0, 1.2, 2.4)
    bs.tuer(netz, bs.wandmatrix((0, HINTEN, Z_SOCKEL), "hinten"), hinten, st)


def obergeschoss(netz, st):
    """Ratssaal in Fachwerk mit roten Läden, Stadtwappen, Schieferdach, Fachwerkgiebel, Gauben, Schornstein."""
    netz.quader((0, 0, Z_OG + H_OG / 2), (2 * HX, HINTEN - VORN, H_OG), st["Putz"])
    netz.quader((0, 0, Z_OG + 0.08), (2 * HX + 0.08, HINTEN - VORN + 0.08, 0.16), st["Fachwerk"], ohne=("+z",))
    F = "fenster"
    fo = dict(v=0.85, b=1.0, h=1.5, laeden=False)
    vorn_mitte = (-HX + TX0) / 2
    waende = (
        (bs.wandmatrix((vorn_mitte, VORN, Z_OG), "vorne"), TX0 + HX, [Oeffnung(F, u, **fo) for u in (-4.6, -2.4, 2.4, 4.6)]),
        (bs.wandmatrix((HX, (TY1 + HINTEN) / 2, Z_OG), "rechts"), HINTEN - TY1, [Oeffnung(F, u, **fo) for u in (-1.6, 1.6)]),
        (bs.wandmatrix((-HX, 0, Z_OG), "links"), HINTEN - VORN, [Oeffnung(F, u, **fo) for u in (-2.6, 0.0, 2.6)]),
        (bs.wandmatrix((0, HINTEN, Z_OG), "hinten"), 2 * HX, [Oeffnung(F, u, **fo) for u in (-5.6, -2.8, 0.0, 2.8, 5.6)]),
    )
    for M, L, liste in waende:
        bs.fachwerk(netz, M, L, H_OG, liste, st, kreuze=True, saat=int(L * 10))
        for o in liste:
            bs.fenster(netz, M, o, st)
            laeden(netz, st, M, o)
    stadtwappen(netz, st, bs.wandmatrix((vorn_mitte, VORN, Z_OG), "vorne"), 0.0, 1.75, g=1.45)
    dach = bs.satteldach(netz, st, -HX, HX, VORN, HINTEN, Z_TRAUFE, NEIGUNG, traufe=0.3, ortgang=0.35, **bs.SCHIEFER)
    hoch = (HINTEN - VORN) / 2 * dach["tan"]
    for x, seite in ((HX, "rechts"), (-HX, "links")):
        bs.fachwerkgiebel(netz, st, bs.wandmatrix((x, 0, Z_TRAUFE), seite), HINTEN - VORN, hoch, [])
    for x0 in (-6.0, -2.4):
        bs.gaube(netz, st, dach, [], x0=x0, x1=x0 + 1.4, y_front=VORN + 1.2)
    xs, ys = -4.0, 2.2
    z0 = dach["oben_hinten"](ys) - 0.3
    netz.quader((xs, ys, (z0 + dach["z_first"] + 0.8) / 2), (0.65, 0.65, dach["z_first"] + 0.8 - z0), st["Bruchstein dunkel"])
    netz.quader((xs, ys, dach["z_first"] + 0.86), (0.85, 0.85, 0.12), st["Bruchstein"])


def laeden(netz, st, M, o, winkel=50):
    """Zwei halb geöffnete Fensterläden in Stadtrot mit weißem Querstreifen."""
    breite = o.b / 2
    for s in (-1, 1):
        dreh = -winkel if s > 0 else 180 + winkel
        t = (M @ Matrix.Translation((o.u + s * o.b / 2, -bs.VOR_PFOSTEN - 0.012, o.v + o.h / 2))
             @ Matrix.Rotation(math.radians(dreh), 4, "Z"))
        netz.quader((breite / 2, 0, 0), (breite, 0.035, o.h), st["Stadtrot"], t)
        netz.quader((breite / 2, -0.02, 0), (breite, 0.01, 0.16), st["Stadtweiss"], t)


def stadtwappen(netz, st, M, u, v, g=1.0):
    """Stadtwappen: roter Schild mit weißem Kreuz in einem Sandsteinrahmen."""
    bs.platte(netz, M, [(u + x * 1.16 * g, v + y * 1.12 * g) for x, y in bs.SCHILD], -0.085, -0.05, st["Sandstein"])
    bs.platte(netz, M, [(u + x * g, v + y * g) for x, y in bs.SCHILD], -0.11, -0.08, st["Stadtrot"])
    netz.quader((u, -0.12, v), (0.12 * g, 0.02, 0.7 * g), st["Stadtweiss"], M)
    netz.quader((u, -0.12, v + 0.08 * g), (0.6 * g, 0.02, 0.12 * g), st["Stadtweiss"], M)


def zifferblatt(netz, st, M, u, v, r=0.75, n=16):
    """Turmuhr: weißes Zifferblatt mit goldenem Ring, zwei Zeigern und Stundenstrichen."""
    kreis = [(u + r * math.cos(2 * math.pi * k / n), v + r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    bs.platte(netz, M, [(u + (r + 0.12) * math.cos(2 * math.pi * k / n), v + (r + 0.12) * math.sin(2 * math.pi * k / n))
                        for k in range(n)], -0.05, -0.002, st["Gold"])
    bs.platte(netz, M, kreis, -0.07, -0.05, st["Stadtweiss"])
    for k in range(12):
        w = 2 * math.pi * k / 12
        netz.quader((u + 0.82 * r * math.cos(w), -0.075, v + 0.82 * r * math.sin(w)),
                    (0.06, 0.01, 0.06), st["Eisen"], M)
    for laenge, w in ((0.55 * r, 20), (0.8 * r, 110)):                 # Stunden- und Minutenzeiger
        t = M @ Matrix.Translation((u, -0.085, v)) @ Matrix.Rotation(math.radians(-w), 4, "Y")
        netz.quader((0, 0, laenge / 2), (0.06, 0.012, laenge), st["Eisen"], t)


def uhrturm(netz, st):
    """Uhrturm aus Haustein vorn rechts: Tür, Fenster, Zifferblätter nach vorn und zur Seite, Glockenstube, Helm."""
    cx, cy = (TX0 + TX1) / 2, (TY0 + TY1) / 2
    b, t = TX1 - TX0, TY1 - TY0
    netz.quader((cx, cy, Z_SOCKEL / 2 + 0.01), (b + 0.1, t + 0.1, Z_SOCKEL + 0.02), st["Bruchstein dunkel"])
    netz.quader((cx, cy, (Z_SOCKEL + T_HOCH) / 2), (b, t, T_HOCH - Z_SOCKEL), st["Haustein"])
    for z in (Z_OG, Z_TRAUFE + 0.2, 13.4, T_HOCH - 0.2):
        bs.gesims(netz, st, TX0, TX1, TY0, TY1, z, hoehe=0.18, vor=0.08)
    for sx, x in ((-1, TX0), (1, TX1)):
        for sy, y in ((-1, TY0), (1, TY1)):
            bs.eckquader(netz, st, x, y, sx, sy, Z_SOCKEL, T_HOCH - 0.2)
    seiten = {"vorne": bs.wandmatrix((cx, TY0, Z_SOCKEL), "vorne"), "rechts": bs.wandmatrix((TX1, cy, Z_SOCKEL), "rechts"),
              "links": bs.wandmatrix((TX0, cy, Z_SOCKEL), "links"), "hinten": bs.wandmatrix((cx, TY1, Z_SOCKEL), "hinten")}
    for seite, M in seiten.items():
        bs.fugen(netz, M, (b if seite in ("vorne", "hinten") else t) - 0.04, 0.0, T_HOCH - Z_SOCKEL - 0.3, st)
        for u in (-0.6, 0.6):                                           # Glockenstube
            o = Oeffnung("fenster", u, 13.75 - Z_SOCKEL, 0.6, 2.1)
            bs.gewaende(netz, M, o, st, kreuz=False, glas="Loch")
        if seite in ("vorne", "rechts"):
            zifferblatt(netz, st, M, 0.0, 11.6 - Z_SOCKEL)
        if seite in ("vorne", "rechts", "links"):
            bs.gewaende(netz, M, Oeffnung("fenster", 0.0, 6.0 - Z_SOCKEL, 0.6, 1.1), st, kreuz=False)
    tuer = Oeffnung("tuer", 0.0, 0.0, 1.1, 2.3)
    bs.portal(netz, seiten["vorne"], tuer, st)
    bs.turmhelm(netz, st, Vector((cx, cy, 0)), (b / 2 + 0.25) * math.sqrt(2), T_HOCH, 6.0, stoff="Schiefer", n=4)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    alles = lambda o: True                                                         # noqa: E731
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=8.0, ausschnitt=30.0, schnell=schnell)
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, alles))
        return
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, alles))
        return
    print(bs.rendern(NAME, alles))
    if not schnell:
        print(bs.rundum(NAME, alles))
    print(bs.anker_bild(NAME, alles))
    print(bs.nachtbild(NAME, alles))


if __name__ == "__main__":
    main()
