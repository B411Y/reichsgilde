"""Fachwerkhaus – erste Stufe des Wohnhauses der Spielhäuser (Stand Gemeiner). Belegte Fläche 8 × 10 m.

    blender -b --python tools/modelle/fachwerkhaus.py        oder
    python tools/modelle/fachwerkhaus.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/fachwerkhaus.glb und Bilder nach docs/modelle/.

Objekte im GLB wie beim Bürgerhaus: Haus, Wappen, Fahne, Leerstand und Geruest (die beiden
letzten standardmäßig aus).

Ein ärmliches, eingeschossiges Haus (Blender: Z oben, Gasse bei -Y):
    niedriger Feldsteinsockel, 6 × 5,6 m, grobes Fachwerk mit weiten Feldern und krummen Ständern,
    fleckiger Lehmputz mit abgeplatzten Stellen, kleine Luken ohne Glas, schiefe Läden,
    dickes Strohdach mit Grassoden auf dem First und Moos, Bretter im Giebel mit Eulenloch,
    gemauerter Schornstein. Daneben ein Holzstapel, ein Hackklotz und hinten ein Bretterschuppen.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Oeffnung, Vector  # noqa: E402

NAME = "fachwerkhaus"
FLAECHE = (8.0, 10.0)

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
HX, VORN, HINTEN = 3.0, -3.0, 2.6        # Haus: x ±3, y von -3 bis 2,6
Z_SOCKEL = 0.3
H_WAND = 2.5
Z_TRAUFE = Z_SOCKEL + H_WAND             # 2,8
NEIGUNG = 48
UEBER = 0.045


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    oeffnungen = []
    zufall = random.Random(3)

    def wand(M, L, liste, saat):
        bs.fachwerk(haus, M, L, H_WAND, liste, st, feld=1.75, schief=0.08, saat=saat)
        for o in liste:
            if o.art == "tuer":
                bs.tuer(haus, M, o, st)
            else:   # Luken ohne Glas und Kreuz, Läden mal weit, mal kaum offen
                bs.fenster(haus, M, o, st, kreuz=None, winkel=zufall.uniform(15, 55), winkel_rechts=zufall.uniform(15, 70))
            oeffnungen.append((M, o))

    y_mitte = (VORN + HINTEN) / 2
    tief = HINTEN - VORN

    # ── Sockel aus Feldsteinen, Lehmkörper ────────────────────────────────────
    haus.quader((0, y_mitte, Z_SOCKEL / 2), (2 * HX + 0.12, tief + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    for i, (u, b) in enumerate(((-2.4, 0.5), (-0.7, 0.35), (1.9, 0.45), (0.3, 0.3))):
        haus.quader((u, VORN - 0.075, 0.16), (b, 0.04, 0.2), st["Bruchstein"], ohne=("+y",))
        haus.quader((HX + 0.075, u * 0.8 + y_mitte, 0.15), (0.04, b, 0.18), st["Bruchstein"], ohne=("-x",))
        haus.quader((-HX - 0.075, -u * 0.8 + y_mitte, 0.16), (0.04, b, 0.19), st["Bruchstein"], ohne=("+x",))
    haus.quader((0, y_mitte, Z_SOCKEL + H_WAND / 2), (2 * HX, tief, H_WAND), st["Lehmputz"], ohne=("+z",))

    # ── Wände: grobes Fachwerk, kleine Luken ──────────────────────────────────
    F, T = "fenster", "tuer"
    luke = dict(v=0.75, b=0.7, h=0.72)
    M_vorn = bs.wandmatrix((0, VORN, Z_SOCKEL), "vorne")
    M_rechts = bs.wandmatrix((HX, y_mitte, Z_SOCKEL), "rechts")
    M_hinten = bs.wandmatrix((0, HINTEN, Z_SOCKEL), "hinten")
    M_links = bs.wandmatrix((-HX, y_mitte, Z_SOCKEL), "links")
    wand(M_vorn, 2 * HX + 2 * UEBER, [Oeffnung(F, -1.7, **luke), Oeffnung(T, 0.9, 0.15, 0.95, 1.8), Oeffnung(F, 2.2, **luke)], 1)
    wand(M_rechts, tief, [Oeffnung(F, 0.2, **luke)], 2)
    wand(M_hinten, 2 * HX + 2 * UEBER, [Oeffnung(F, 1.8, **luke)], 3)
    wand(M_links, tief, [Oeffnung(F, 0.6, **luke)], 4)
    # Abgeplatzter Putz: das Lehmflechtwerk schaut durch
    for M, u, v, r in ((M_vorn, -0.35, 0.6, 0.32), (M_vorn, 2.6, 1.7, 0.22), (M_rechts, -1.6, 1.5, 0.3),
                       (M_rechts, 1.6, 0.5, 0.25), (M_links, -1.5, 1.1, 0.28), (M_hinten, -0.9, 1.6, 0.3)):
        fleck(haus, M, u, v, r, st["Lehm"])
    # Trittstein vor der Tür
    haus.quader((0.9, VORN - 0.3, 0.1), (1.1, 0.5, 0.2), st["Bruchstein"])

    # ── Strohdach (Krüppelwalm), Bretter im Giebel, Moos, Schornstein ─────────
    dach = strohdach(haus, st)
    for s, seite in ((1, "rechts"), (-1, "links")):
        bretter_giebel(haus, st, bs.wandmatrix((s * HX, y_mitte, Z_TRAUFE), seite), tief, dach["giebel_hoehe"], dach["giebel_oben"])
    for seite, x, s, r in (("vorn", -1.5, 1.2, 0.55), ("vorn", 1.4, 2.6, 0.4), ("hinten", 0.6, 1.0, 0.6), ("hinten", -1.6, 2.4, 0.35)):
        bs.dachloch(haus, st, dach, seite, x, s, r, stoff="Moos")
    xs, ys = -1.0, 0.8
    z0 = dach["oben_hinten"](ys + 0.25) - 0.3
    haus.quader((xs, ys, (z0 + dach["z_first"] + 0.6) / 2), (0.55, 0.55, dach["z_first"] + 0.6 - z0), st["Bruchstein dunkel"])
    haus.quader((xs + 0.03, ys - 0.02, dach["z_first"] + 0.7), (0.62, 0.6, 0.2), st["Bruchstein"])

    # ── Holzstapel an der linken Wand, Hackklotz, Schuppen hinten ─────────────
    holzstapel(haus, st, x_wand=-HX - 0.12, y0=-1.7, y1=1.1)
    bs.fass(haus, st, Vector((-2.5, VORN - 0.6, 0)), r=0.24, h=0.48, reifen=(), stoff="Holz alt")
    schuppen(haus, st, x0=-0.4, x1=2.6, y0=HINTEN, y1=4.3)

    # ── Wappen neben der Tür, Fahne auf dem First ─────────────────────────────
    wappen = bs.Netz("Wappen")
    bs.wappen(wappen, M_vorn, -0.35, 1.3, st, groesse=0.72, rahmen="Holz alt")
    fahne = bs.Netz("Fahne")
    bs.fahne(fahne, st, (2.2, dach["y_first"], dach["z_first"] - 0.2), hoehe=2.4)

    # ── Leerstand: Bretter, Löcher im Stroh, viel Unkraut ─────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for seite, x, s, r in (("vorn", 0.4, 2.2, 0.45), ("vorn", -2.3, 3.3, 0.3), ("hinten", 1.8, 2.4, 0.45)):
        bs.dachloch(leer, st, dach, seite, x, s, r)
    for x, y, g in ((-2.9, VORN - 0.15, 0.6), (-1.0, VORN - 0.14, 0.45), (2.7, VORN - 0.15, 0.55), (HX + 0.15, -1.6, 0.6),
                    (HX + 0.14, 1.9, 0.5), (-HX - 0.15, 2.1, 0.55), (1.2, VORN - 1.2, 0.4), (-0.6, HINTEN + 0.2, 0.5)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=g)

    # ── Gerüst vor der Gassenseite (nur ein Geschoss) ─────────────────────────
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=-HX - 0.15, x1=HX + 0.15, y_innen=VORN - 0.25, y_aussen=VORN - 1.15,
                  ebenen=(1.85,), hoehe_innen=2.4, hoehe_aussen=3.3)

    return [haus.objekt(), wappen.objekt(), fahne.objekt(), bs.zustand(leer.objekt()), bs.zustand(geruest.objekt())]


def fleck(netz, M, u, v, r, m):
    """Ausgefranster Fleck auf einer Wand (hinter dem Fachwerk)."""
    zacken = [1.0, 0.6, 0.9, 0.7, 1.1, 0.55, 0.85]
    punkte = [M @ Vector((u + r * z * math.cos(2 * math.pi * k / 7), -0.004, v + r * 0.75 * z * math.sin(2 * math.pi * k / 7)))
              for k, z in enumerate(zacken)]
    netz.vieleck_nach(punkte, M.to_3x3() @ Vector((0, -1, 0)), m)


NEIGUNG_WALM = 58      # der kleine Walm über dem Giebel ist steiler als die Hauptflächen
STROH_DICKE = 0.45     # so dick wirkt das Stroh an Traufe und Ortgang
WALM_AB = 1.9          # über der Wandoberkante beginnt der Walm


def strohdach(netz, st, traufe=0.5, ortgang=0.35):
    """Krüppelwalmdach aus Stroh: zwei Hauptflächen, über den Giebeln je ein kleiner Walm,
    dicke senkrechte Kanten an Traufe und Ortgang, Grassoden auf dem First und dunkle Regenstreifen.
    Gibt Maße zurück, die bs.dachloch versteht (für Moos und Löcher)."""
    th = math.radians(NEIGUNG)
    tan, cos, sin = math.tan(th), math.cos(th), math.sin(th)
    halb = (HINTEN - VORN) / 2
    y_first = (VORN + HINTEN) / 2
    ueber = STROH_DICKE / cos                       # Oberfläche liegt so hoch über der Wandkante

    def oben_vorn(y):
        return Z_TRAUFE + ueber + (y - VORN) * tan

    def oben_hinten(y):
        return Z_TRAUFE + ueber + (HINTEN - y) * tan

    xo = HX + ortgang
    z_te = oben_vorn(VORN - traufe)                 # Oberkante an der Traufe
    z_first = oben_vorn(y_first)
    z_walm = Z_TRAUFE + WALM_AB
    y_wv = VORN + (z_walm - Z_TRAUFE - ueber) / tan     # wo die Hauptfläche die Walmhöhe erreicht
    y_wh = HINTEN - (z_walm - Z_TRAUFE - ueber) / tan
    x_first = xo - (z_first - z_walm) / math.tan(math.radians(NEIGUNG_WALM))
    stroh, kante = st["Stroh"], st["Strohkante"]
    A, B = Vector((-xo, VORN - traufe, z_te)), Vector((xo, VORN - traufe, z_te))
    C, F = Vector((xo, y_wv, z_walm)), Vector((-xo, y_wv, z_walm))
    D, E = Vector((x_first, y_first, z_first)), Vector((-x_first, y_first, z_first))
    A2, B2 = Vector((-xo, HINTEN + traufe, z_te)), Vector((xo, HINTEN + traufe, z_te))
    C2, F2 = Vector((xo, y_wh, z_walm)), Vector((-xo, y_wh, z_walm))
    netz.vieleck_nach([A, B, C, D, E, F], (0, -sin, cos), stroh)
    netz.vieleck_nach([A2, B2, C2, D, E, F2], (0, sin, cos), stroh)
    netz.vieleck_nach([C, C2, D], (1, 0, 0.6), stroh)
    netz.vieleck_nach([F, F2, E], (-1, 0, 0.6), stroh)
    unten = Vector((0, 0, -STROH_DICKE))
    for p, q, n in ((A, B, (0, -1, 0)), (A2, B2, (0, 1, 0)), (B, C, (1, 0, 0)), (C, C2, (1, 0, 0)), (C2, B2, (1, 0, 0)),
                    (A, F, (-1, 0, 0)), (F, F2, (-1, 0, 0)), (F2, A2, (-1, 0, 0))):
        netz.vieleck_nach([p, q, q + unten, p + unten], n, kante)
    # First: Grassoden, rundum etwas überstehend
    netz.stab((-x_first - 0.15, y_first, z_first + 0.05), (x_first + 0.15, y_first, z_first + 0.05), 0.5, 0.5,
              st["Firstsoden"], matrix=None, quer=(0, 1, 1))
    # Regenstreifen: schmale dunkle Bahnen die Fläche hinunter
    zufall = random.Random(11)
    for seite, rich, nor, start_y in ((-1, Vector((0, cos, sin)), Vector((0, -sin, cos)), VORN - traufe),
                                      (1, Vector((0, -cos, sin)), Vector((0, sin, cos)), HINTEN + traufe)):
        for _ in range(14):
            x = zufall.uniform(-xo + 0.3, xo - 0.3)
            laenge = zufall.uniform(0.8, 2.6)
            fuss = Vector((x, start_y, z_te)) + nor * 0.012
            if abs(x) > x_first:                       # im Walmbereich nicht über die Fläche hinaus
                laenge = min(laenge, (z_walm - z_te) / sin - 0.2)
            b = zufall.uniform(0.04, 0.09)
            q = [fuss + Vector((-b, 0, 0)), fuss + Vector((b, 0, 0)), fuss + Vector((b * 0.4, 0, 0)) + rich * laenge,
                 fuss + Vector((-b * 0.4, 0, 0)) + rich * laenge]
            netz.vieleck_nach(q, nor, kante)
    return {"matrix": bs.Matrix.Identity(4), "dicke": -0.06, "tan": tan, "y_first": y_first, "z_first": z_first,
            "oben_vorn": oben_vorn, "oben_hinten": oben_hinten,
            "traufe_vorn": Vector((VORN - traufe, z_te)), "traufe_hinten": Vector((HINTEN + traufe, z_te)),
            "richtung_vorn": Vector((cos, sin)), "normale_vorn": Vector((-sin, cos)),
            "richtung_hinten": Vector((-cos, sin)), "normale_hinten": Vector((sin, cos)),
            "giebel_hoehe": WALM_AB - STROH_DICKE, "giebel_oben": (halb - (WALM_AB - STROH_DICKE) / tan)}


def bretter_giebel(netz, st, M, L, hoch, oben_halb):
    """Giebel unter dem Walm aus verwitterten Brettern: Trapez, Deckleisten, Eulenloch oben."""
    halb = L / 2
    umriss = [(-halb, 0), (halb, 0), (oben_halb, hoch), (-oben_halb, hoch)]
    bs.platte(netz, M, umriss, 0, 0.25, st["Holz alt"])
    bs.balken(netz, M, -halb, bs.B / 2, halb, bs.B / 2, st["Fachwerk"], vor=bs.VOR_SCHWELLE, enden=True)
    u = -halb + 0.45
    while u < halb - 0.3:
        rand = min(hoch, hoch * (halb - abs(u)) / (halb - oben_halb)) - 0.06
        if rand > 0.3 and abs(u) > 0.4:
            netz.vieleck([M @ Vector(p) for p in ((u - 0.03, -0.02, bs.B), (u + 0.03, -0.02, bs.B),
                                                  (u + 0.03, -0.02, rand), (u - 0.03, -0.02, rand))], st["Fachwerk"])
        u += 0.42
    loch = [(-0.3, hoch - 0.55), (0.3, hoch - 0.55), (0, hoch - 0.12)]
    netz.vieleck([M @ Vector((a, -0.03, b)) for a, b in loch], st["Loch"])


def holzstapel(netz, st, x_wand, y0, y1, hoehe=1.15, tiefe=0.55):
    """Aufgeschichtetes Brennholz an einer Wand, mit schrägem Brett als Regenschutz."""
    x = x_wand - tiefe / 2
    netz.quader((x, (y0 + y1) / 2, hoehe / 2), (tiefe, y1 - y0, hoehe), st["Scheitholz"])
    for k in range(1, 4):     # Lagen als dunkle Linien auf der Stirnseite
        z = hoehe * k / 4
        netz.vieleck([(x_wand - tiefe - 0.004, y0, z - 0.02), (x_wand - tiefe - 0.004, y0, z + 0.02),
                      (x_wand - tiefe - 0.004, y1, z + 0.02), (x_wand - tiefe - 0.004, y1, z - 0.02)], st["Holz alt"])
    netz.stab((x_wand + 0.02, y0 - 0.1, hoehe + 0.35), (x_wand - tiefe - 0.2, y0 - 0.1, hoehe + 0.05), 0.06, 0.06,
              st["Holz alt"])
    netz.quader((x - 0.07, (y0 + y1) / 2, hoehe + 0.2), (tiefe + 0.3, y1 - y0 + 0.3, 0.05), st["Holz alt"],
                matrix=bs.Matrix.Translation((x - 0.07, 0, hoehe + 0.2)) @ bs.Matrix.Rotation(math.radians(-25), 4, "Y")
                @ bs.Matrix.Translation((-(x - 0.07), 0, -(hoehe + 0.2))))


def schuppen(netz, st, x0, x1, y0, y1, h_hoch=2.05, h_tief=1.6):
    """Bretterschuppen an der Rückwand mit Pultdach aus Brettern und offener Tür an der Seite."""
    alt = st["Holz alt"]
    profil = [(y0, 0), (y1, 0), (y1, h_tief), (y0, h_hoch)]
    netz.prisma(profil, x0, x1, alt)
    u = x0 + 0.3        # senkrechte Deckleisten auf der Rückseite (+y)
    while u < x1 - 0.2:
        netz.vieleck([(u + 0.03, y1 + 0.02, 0), (u - 0.03, y1 + 0.02, 0), (u - 0.03, y1 + 0.02, h_tief - 0.05),
                      (u + 0.03, y1 + 0.02, h_tief - 0.05)], st["Fachwerk"])
        u += 0.4
    netz.vieleck([(x1 + 0.005, y0 + 0.5, 0), (x1 + 0.005, y0 + 1.3, 0), (x1 + 0.005, y0 + 1.3, 1.55),
                  (x1 + 0.005, y0 + 0.5, 1.6)], st["Loch"])
    neig = math.atan2(h_hoch - h_tief, y1 - y0)
    laenge = (y1 - y0 + 0.25) / math.cos(neig)
    mitte = Vector(((x0 + x1) / 2, (y0 + y1 + 0.25) / 2, (h_hoch + h_tief) / 2 + 0.06 - 0.25 * math.tan(neig) / 2))
    netz.quader((0, 0, 0), (x1 - x0 + 0.3, laenge, 0.07), alt,
                matrix=bs.Matrix.Translation(mitte) @ bs.Matrix.Rotation(-neig, 4, "X"))


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=3.6, ausschnitt=20.0, schnell="--schnell" in sys.argv)
    normal = lambda o: o.name not in ("Leerstand", "Geruest")                     # noqa: E731
    print(bs.rendern(NAME, normal))
    print(bs.rendern(NAME + "-leerstand", lambda o: o.name not in ("Geruest", "Wappen", "Fahne")))
    print(bs.rendern(NAME + "-geruest", lambda o: o.name != "Leerstand"))
    if "--schnell" not in sys.argv:
        print(bs.rundum(NAME, normal))


if __name__ == "__main__":
    main()
