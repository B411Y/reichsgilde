"""Stadthof – fünfte Stufe des Wohnhauses der Spielhäuser (Stand Freiherr, Reichsgraf). Belegte Fläche 16 × 14 m.

    blender -b --python tools/modelle/stadthof.py        oder
    python tools/modelle/stadthof.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/stadthof.glb und Bilder nach docs/modelle/.

Objekte im GLB wie bei den anderen Wohnhäusern: Haus, Wappen, Fahne, Leerstand und Geruest
(die beiden letzten standardmäßig aus), dazu der Anker „Anker_Tuer“.

Ein Adelshof aus hellem Haustein wie das Stadtpalais (Blender: Z oben, Gasse bei -Y), wie bisher im Spiel mit Haupthaus
hinten, zwei Flügeln vorn und Hofmauer mit Tor:
    Haupthaus 14,8 × 4,8 m mit drei Geschossen, Schieferdach mit der Traufe zum Hof, Treppengiebeln und Gauben; vor
    der Mitte ein achteckiger Treppenturm mit Portal und spitzem Helm. Zwei Flügel mit zwei Geschossen und
    Treppengiebel zur Gasse. Vorn zwischen den Flügeln die Hofmauer mit einem Torhaus (Rundbogendurchfahrt, Tor in
    der Hausfarbe, Wappen darüber, Fahne auf dem Dach). Im Ehrenhof Pflaster, ein Brunnen und zwei Buchsbeete.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "stadthof"
FLAECHE = (16.0, 14.0)

# Farben nur für dieses Modell (wie beim Stadtpalais; jedes Modell läuft in einem eigenen Prozess)
bs.FARBEN.update({
    "Palaststein": (0.70, 0.66, 0.58),
    "Fuge": (0.55, 0.52, 0.46),
    "Pflaster": (0.52, 0.49, 0.44),
    "Buchs": (0.20, 0.31, 0.15),
})

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
FRONT = -5.0                               # Vorderkante des Torhauses an der Gasse (2 m Vorplatz davor)
# Haupthaus hinten
HX, HV, HH = 7.4, 1.8, 6.4                 # x ±7,4, y von 1,8 (Hofseite) bis 6,4
Z_SOCKEL = 0.5
GESCHOSSE = (3.3, 3.0, 2.6)
Z_TRAUFE = Z_SOCKEL + sum(GESCHOSSE)       # 9,4
Z_GESIMS = 0.26
NEIGUNG = 52
# Flügel
FI, FA = 4.4, 7.4                          # Flügel von |x| = 4,4 (Hofseite) bis 7,4
FV = -4.6                                  # Giebel der Flügel an der Gasse
FL_GESCHOSSE = (3.2, 2.8)
FL_TRAUFE = Z_SOCKEL + sum(FL_GESCHOSSE)   # 6,5
FL_NEIGUNG = 50
# Hofmauer und Torhaus
MAUER_Y, MAUER_D, MAUER_H = FV, 0.5, 3.2
TX, T_HINTEN, T_HOCH = 1.9, -3.4, 6.0      # Torhaus x ±1,9, y von FRONT bis -3,4, Traufe bei 6,0
# Treppenturm
TURM_R, TURM_Y = 1.45, HV - 0.2
TURM_HOCH = Z_TRAUFE + 2.8


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus = bs.Netz("Haus")
    wappen = bs.Netz("Wappen")
    fahne = bs.Netz("Fahne")
    oeffnungen = []

    dach = haupthaus(haus, st, oeffnungen)
    for sx in (-1, 1):
        fluegel(haus, st, oeffnungen, sx)
    torhaus(haus, wappen, fahne, st, oeffnungen)
    ehrenhof(haus, st)
    treppenturm(haus, st, oeffnungen)

    # ── Leerstand ─────────────────────────────────────────────────────────────
    leer = bs.Netz("Leerstand")
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(leer, M, o, st, i)
    for seite, x, s, r in (("vorn", -4.6, 2.2, 0.45), ("vorn", 3.4, 3.0, 0.35), ("hinten", 0.8, 2.6, 0.45)):
        bs.dachloch(leer, st, dach, seite, x, s, r)
    for x, y in ((-3.2, -3.0), (1.6, -0.4), (-1.6, 0.0), (3.6, -3.0), (-FA - 0.15, -1.0), (FA + 0.15, 3.6),
                 (0.6, HH + 0.15), (-5.2, FRONT + 0.2)):
        bs.buschel(leer, st, Vector((x, y, 0)), groesse=0.55)

    # ── Gerüst vor der Gassenseite ────────────────────────────────────────────
    geruest = bs.Netz("Geruest")
    bs.baugeruest(geruest, st, x0=-FA - 0.2, x1=FA + 0.2, y_innen=FRONT - 0.3, y_aussen=FRONT - 1.2,
                  ebenen=(2.4, 4.8), hoehe_innen=6.6, hoehe_aussen=7.2, felder=5)

    # Ankerpunkt: vor dem Tor in der Durchfahrt, Blick in den Hof
    tuer = bs.anker("Anker_Tuer", (0.0, FRONT - 0.2, 0.0))
    return [haus.objekt(), wappen.objekt(), fahne.objekt(), bs.zustand(leer.objekt()), bs.zustand(geruest.objekt()), tuer]


def fenster_setzen(netz, st, oeffnungen, M, liste, verdachung=False):
    for o in liste:
        bs.gewaende(netz, M, o, st, verdachung=verdachung)
        oeffnungen.append((M, o))


def haupthaus(netz, st, oeffnungen):
    """Haupthaus hinten: drei Geschosse, Gesimse, Lagerfugen, Steinfenster, Schieferdach mit Traufe zum Hof,
    Treppengiebel an beiden Seiten, Gauben vorn und hinten, zwei Schornsteine."""
    stein = st["Palaststein"]
    tief, ym = HH - HV, (HV + HH) / 2
    v1, v2 = GESCHOSSE[0], GESCHOSSE[0] + GESCHOSSE[1]
    netz.quader((0, ym, Z_SOCKEL / 2), (2 * HX + 0.12, tief + 0.12, Z_SOCKEL), st["Bruchstein dunkel"])
    netz.quader((0, ym, Z_SOCKEL + sum(GESCHOSSE) / 2), (2 * HX, tief, sum(GESCHOSSE)), stein, ohne=("+z",))
    for z, h, vor in ((Z_SOCKEL - 0.06, 0.12, 0.09), (Z_SOCKEL + v1, 0.16, 0.07), (Z_SOCKEL + v2, 0.16, 0.07),
                      (Z_TRAUFE, Z_GESIMS, 0.13)):
        bs.gesims(netz, st, -HX, HX, HV, HH, z, hoehe=h, vor=vor)
    for sx in (-1, 1):
        for sy, y in ((-1, HV), (1, HH)):
            bs.eckquader(netz, st, sx * HX, y, sx, sy, Z_SOCKEL, Z_TRAUFE)
    M_hof = bs.wandmatrix((0, HV, Z_SOCKEL), "vorne")
    M_hinten = bs.wandmatrix((0, HH, Z_SOCKEL), "hinten")
    M_r = bs.wandmatrix((HX, ym, Z_SOCKEL), "rechts")
    M_l = bs.wandmatrix((-HX, ym, Z_SOCKEL), "links")
    for M, L in ((M_hof, 2 * HX), (M_hinten, 2 * HX), (M_r, tief), (M_l, tief)):
        bs.fugen(netz, M, L - 0.02, 0.0, sum(GESCHOSSE), st)
    F = "fenster"
    eg = dict(v=1.0, b=1.0, h=1.5)
    og1 = dict(v=v1 + 0.7, b=1.0, h=1.7)
    og2 = dict(v=v2 + 0.55, b=0.9, h=1.25)
    # Hofseite: nur zwischen den Flügeln sichtbar, die Mitte verdeckt der Treppenturm
    hof = (-3.55, -2.0, 2.0, 3.55)
    fenster_setzen(netz, st, oeffnungen, M_hof, [Oeffnung(F, u, **eg) for u in hof])
    fenster_setzen(netz, st, oeffnungen, M_hof, [Oeffnung(F, u, **og1) for u in hof], verdachung=True)
    fenster_setzen(netz, st, oeffnungen, M_hof, [Oeffnung(F, u, **og2) for u in hof])
    rueck = (-6.0, -3.6, -1.2, 1.2, 3.6, 6.0)
    fenster_setzen(netz, st, oeffnungen, M_hinten, [Oeffnung(F, u, **eg) for u in rueck if u != 1.2])
    fenster_setzen(netz, st, oeffnungen, M_hinten, [Oeffnung(F, u, **og1) for u in rueck], verdachung=True)
    fenster_setzen(netz, st, oeffnungen, M_hinten, [Oeffnung(F, u, **og2) for u in rueck])
    hinten = Oeffnung("tuer", 1.2, 0.0, 1.1, 2.3)
    bs.tuer(netz, M_hinten, hinten, st)
    netz.quader((1.2, -0.03, 2.4), (1.5, 0.1, 0.22), st["Sandstein"], M_hinten, ohne=("+y",))
    oeffnungen.append((M_hinten, hinten))
    netz.quader((1.2, HH + 0.18, 0.24), (1.4, 0.36, 0.48), st["Sandstein"], ohne=("-y",))
    for M in (M_r, M_l):
        fenster_setzen(netz, st, oeffnungen, M, [Oeffnung(F, 1.0, **og2)])
    # Dach, Treppengiebel, Gauben, Schornsteine
    dach = bs.satteldach(netz, st, -HX, HX, HV, HH, Z_TRAUFE + Z_GESIMS, NEIGUNG, traufe=0.35, ortgang=-0.25,
                         enden="Palaststein", first_dicke=0.2, **bs.SCHIEFER)
    z_dach0 = Z_GESIMS + dach["dicke"] / math.cos(math.radians(NEIGUNG))
    for x, seite in ((HX, "rechts"), (-HX, "links")):
        bs.treppengiebel(netz, st, bs.wandmatrix((x, ym, Z_TRAUFE), seite), tief / 2, z_dach0, dach["tan"], oeffnungen,
                         [Oeffnung("fenster", 0.0, 0.8, 0.6, 0.95)], stufen=4, oben_halb=0.4, stoff="Palaststein")
    for x0 in (-5.6, 3.9):
        bs.gaube(netz, st, dach, oeffnungen, x0=x0, x1=x0 + 1.5, y_front=HV + 1.0, hoehe=1.3, art="stein",
                 koerper="Palaststein")
    dach_hinten = dict(dach, matrix=dach["matrix"] @ Matrix.Rotation(math.radians(180), 4, "Z"))
    for x0 in (-4.4, -0.75, 2.9):
        bs.gaube(netz, st, dach_hinten, oeffnungen, x0=x0, x1=x0 + 1.5, y_front=-HH + 1.0, hoehe=1.3, art="stein",
                 koerper="Palaststein")
    for xs in (-3.0, 4.6):
        ys = HV + 3.4
        z_dach = dach["oben_hinten"](ys) - 0.3
        hoehe = dach["z_first"] + 0.9 - z_dach
        netz.quader((xs, ys, z_dach + hoehe / 2), (0.7, 0.7, hoehe), stein)
        netz.quader((xs, ys, z_dach + hoehe + 0.06), (0.9, 0.9, 0.12), st["Sandstein"])
    return dach


def fluegel(netz, st, oeffnungen, sx):
    """Seitenflügel von der Gasse bis zum Haupthaus: zwei Geschosse, Treppengiebel zur Gasse,
    Satteldach mit dem First von vorn nach hinten, Fenster zur Gasse, zum Hof und nach außen."""
    stein = st["Palaststein"]
    x0, x1 = sorted((sx * FI, sx * FA))
    xm, breite, lang = (x0 + x1) / 2, x1 - x0, HV - FV
    ym = (FV + HV) / 2
    v1 = FL_GESCHOSSE[0]
    h = sum(FL_GESCHOSSE)
    netz.quader((xm, ym - 0.06, Z_SOCKEL / 2), (breite + 0.12, lang, Z_SOCKEL), st["Bruchstein dunkel"], ohne=("+y",))
    netz.quader((xm, ym, Z_SOCKEL + h / 2), (breite, lang, h), stein, ohne=("+z", "+y"))
    for z, hh, vor in ((Z_SOCKEL - 0.06, 0.12, 0.09), (Z_SOCKEL + v1, 0.18, 0.07), (FL_TRAUFE, 0.22, 0.12)):
        bs.gesims(netz, st, x0, x1, FV, HV - 0.2, z, hoehe=hh, vor=vor)
    for x, ex in ((x0, -1), (x1, 1)):
        bs.eckquader(netz, st, x, FV, ex, -1, Z_SOCKEL, FL_TRAUFE)
    innen, aussen = ("rechts", "links") if sx < 0 else ("links", "rechts")
    M_vorn = bs.wandmatrix((xm, FV, Z_SOCKEL), "vorne")
    M_innen = bs.wandmatrix((sx * FI, ym, Z_SOCKEL), innen)
    M_aussen = bs.wandmatrix((sx * FA, ym, Z_SOCKEL), aussen)
    for M, L in ((M_vorn, breite), (M_innen, lang), (M_aussen, lang)):
        bs.fugen(netz, M, L - 0.02, 0.0, h, st)
    F = "fenster"
    eg = dict(v=1.0, b=0.9, h=1.4)
    og = dict(v=v1 + 0.6, b=0.9, h=1.5)
    fenster_setzen(netz, st, oeffnungen, M_vorn, [Oeffnung(F, 0.0, **eg)])
    fenster_setzen(netz, st, oeffnungen, M_vorn, [Oeffnung(F, 0.0, **og)], verdachung=True)
    laengs = (-1.9, 0.3, 2.5)
    fenster_setzen(netz, st, oeffnungen, M_innen, [Oeffnung(F, u, **eg) for u in laengs])
    fenster_setzen(netz, st, oeffnungen, M_innen, [Oeffnung(F, u, **og) for u in laengs], verdachung=True)
    fenster_setzen(netz, st, oeffnungen, M_aussen, [Oeffnung(F, u, **eg) for u in (-1.9, 2.5)])
    fenster_setzen(netz, st, oeffnungen, M_aussen, [Oeffnung(F, u, **og) for u in laengs])
    # Dach: First entlang y, läuft hinten in das Haupthaus
    drehung = Matrix.Translation((xm, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")   # lokal x → Welt y
    dach = bs.satteldach(netz, st, FV, HV + 0.6, -breite / 2, breite / 2, FL_TRAUFE + 0.22, FL_NEIGUNG, traufe=0.3,
                         ortgang=-0.25, enden="Palaststein", first_dicke=0.18, matrix=drehung, **bs.SCHIEFER)
    z_dach0 = 0.22 + dach["dicke"] / math.cos(math.radians(FL_NEIGUNG))
    bs.treppengiebel(netz, st, bs.wandmatrix((xm, FV, FL_TRAUFE), "vorne"), breite / 2, z_dach0, dach["tan"], oeffnungen,
                     [Oeffnung("fenster", 0.0, 0.7, 0.55, 0.85)], stufen=3, oben_halb=0.35, stoff="Palaststein")


def torhaus(netz, wappen, fahne, st, oeffnungen):
    """Torhaus mit Rundbogendurchfahrt zwischen den Flügeln, links und rechts die Hofmauer mit Sandsteinabdeckung.
    Tor in der Hausfarbe, darüber das Wappen, Satteldach mit der Traufe zur Gasse und der Fahne auf dem First."""
    stein = st["Palaststein"]
    tief = T_HINTEN - FRONT
    ym = (FRONT + T_HINTEN) / 2
    for sx in (-1, 1):                        # Hofmauer zwischen Torhaus und Flügel
        a, b = sorted((sx * TX, sx * FI))
        netz.quader(((a + b) / 2, MAUER_Y + MAUER_D / 2, MAUER_H / 2), (b - a, MAUER_D, MAUER_H), stein, ohne=("-x", "+x"))
        netz.quader(((a + b) / 2, MAUER_Y + MAUER_D / 2, MAUER_H + 0.08), (b - a, MAUER_D + 0.14, 0.16), st["Sandstein"],
                    ohne=("-x", "+x"))
        bs.fugen(netz, bs.wandmatrix(((a + b) / 2, MAUER_Y, 0), "vorne"), b - a - 0.02, 0.0, MAUER_H, st)
    netz.quader((0, ym, T_HOCH / 2), (2 * TX, tief, T_HOCH), stein, ohne=("+z",))      # ohne Sockel: die Durchfahrt reicht bis zum Boden
    bs.gesims(netz, st, -TX, TX, FRONT, T_HINTEN, T_HOCH, hoehe=0.22, vor=0.12)
    bs.gesims(netz, st, -TX, TX, FRONT, T_HINTEN, Z_SOCKEL + 3.6, hoehe=0.16, vor=0.07)
    for sx in (-1, 1):
        for sy, y in ((-1, FRONT), (1, T_HINTEN)):
            bs.eckquader(netz, st, sx * TX, y, sx, sy, 0.0, T_HOCH)
    M_vorn = bs.wandmatrix((0, FRONT, 0), "vorne")
    M_hof = bs.wandmatrix((0, T_HINTEN, 0), "hinten")
    for M in (M_vorn, M_hof):
        bs.fugen(netz, M, 2 * TX - 0.02, 0.0, T_HOCH, st)
    tor = Oeffnung("tuer", 0.0, 0.0, 2.4, 3.5)
    bs.portal(netz, M_vorn, tor, st)
    oeffnungen.append((M_vorn, tor))
    bs.portal(netz, M_hof, Oeffnung("tuer", 0.0, 0.0, 2.4, 3.5), st)
    o = Oeffnung("fenster", 0.0, Z_SOCKEL + 4.15, 0.8, 1.05)
    fenster_setzen(netz, st, oeffnungen, M_hof, [o])
    bs.wappen(wappen, M_vorn, 0.0, Z_SOCKEL + 4.6, st, groesse=0.95, rahmen="Sandstein")
    dach = bs.satteldach(netz, st, -TX, TX, FRONT, T_HINTEN, T_HOCH + 0.22, 50, traufe=0.3, ortgang=0.25,
                         enden="Palaststein", first_dicke=0.18, **bs.SCHIEFER)
    for x, seite in ((TX, "rechts"), (-TX, "links")):
        bs.steingiebel(netz, st, bs.wandmatrix((x, ym, T_HOCH), seite), tief, tief / 2 * dach["tan"], oeffnungen,
                       stoff="Palaststein", mit_fenster=False)
    bs.fahne(fahne, st, (0.0, dach["y_first"], dach["z_first"] - 0.1), hoehe=2.4)
    # Prellsteine an der Durchfahrt
    for sx in (-1, 1):
        bs.achteck(netz, st, (sx * 1.45, FRONT - 0.2, 0), 0.2, 0.0, 0.55, "Sandstein")


def ehrenhof(netz, st):
    """Ehrenhof zwischen Flügeln, Torhaus und Haupthaus: Pflaster, achteckiger Brunnen mit Säule, zwei Buchsbeete."""
    netz.quader((0, (T_HINTEN + HV) / 2 - 0.3, 0.015), (2 * FI - 0.02, HV - T_HINTEN + 1.0, 0.03), st["Pflaster"],
                ohne=("-z",))
    m = Vector((0, -1.9, 0))
    bs.achteck(netz, st, m, 1.0, 0.0, 0.6, "Sandstein")
    bs.rund_oben(netz, st, m, 0.86, 0.603, "Wasser")
    bs.achteck(netz, st, m, 0.15, 0.6, 1.7, "Sandstein")
    bs.achteck(netz, st, m + Vector((0, 0, 1.7)), 0.45, 0.0, 0.14, "Sandstein")
    bs.rund_oben(netz, st, m, 0.38, 1.843, "Wasser")
    bs.achteck(netz, st, m + Vector((0, 0, 1.84)), 0.12, 0.0, 0.35, "Gold")
    for sx in (-1, 1):
        x = sx * 2.9
        yb = -1.6
        netz.quader((x, yb, 0.04), (1.5, 2.9, 0.06), st["Lehm"], ohne=("-z",))
        for y in (yb - 1.32, yb + 1.32):
            netz.quader((x, y, 0.22), (1.54, 0.3, 0.4), st["Buchs"], ohne=("-z",))
        for xx in (x - 0.62, x + 0.62):                   # etwas niedriger als die Querhecken: keine gemeinsame Fläche
            netz.quader((xx, yb, 0.2), (0.3, 2.34, 0.36), st["Buchs"], ohne=("-z",))
        bs.achteck(netz, st, (x, yb, 0.07), 0.3, 0.0, 0.7, "Buchs", n=6)


def treppenturm(netz, st, oeffnungen):
    """Achteckiger Treppenturm vor der Mitte des Haupthauses: Portal in der Hausfarbe, Fenster versetzt wie die
    Wendeltreppe, Gesimse, spitzer Schieferhelm mit Knauf."""
    mitte = Vector((0, TURM_Y, 0))
    bs.achteck(netz, st, mitte, TURM_R + 0.1, 0.0, Z_SOCKEL + 0.04, "Bruchstein dunkel")
    bs.achteck(netz, st, mitte, TURM_R, Z_SOCKEL, TURM_HOCH - 0.1, "Palaststein")
    for z, h, mehr in ((Z_SOCKEL + GESCHOSSE[0], 0.2, 0.08), (Z_SOCKEL + GESCHOSSE[0] + GESCHOSSE[1], 0.2, 0.08),
                       (TURM_HOCH - 0.22, 0.22, 0.14)):
        bs.achteck(netz, st, mitte, TURM_R + mehr, z, z + h, "Sandstein")
    apo = TURM_R * math.cos(math.radians(22.5))
    M = bs.wandmatrix((0, TURM_Y - apo, 0), "vorne")
    tuer = Oeffnung("tuer", 0.0, Z_SOCKEL, 1.05, 2.5)
    bs.portal(netz, M, tuer, st)
    oeffnungen.append((M, tuer))
    for k, (tiefe, hoehe) in enumerate(((0.75, 0.16), (0.5, 0.32), (0.25, 0.48))):
        netz.quader((0, TURM_Y - apo - tiefe / 2, hoehe / 2), (1.9 - 0.2 * k, tiefe, hoehe), st["Sandstein"], ohne=("+y",))
    # Fenster auf den schrägen Seiten, versetzt nach oben wie die Treppe
    for k, (winkel, v) in enumerate(((-135, 3.0), (-45, 4.6), (-135, 6.4), (-45, 8.0), (-90, 10.2))):
        w = math.radians(winkel)
        rich = Vector((math.cos(w), math.sin(w), 0))
        fuss = mitte + rich * apo
        Mf = Matrix.Translation(fuss) @ Matrix.Rotation(w + math.radians(90), 4, "Z") @ bs.wandmatrix((0, 0, 0), "vorne")
        o = Oeffnung("fenster", 0.0, v, 0.45, 0.95)
        bs.gewaende(netz, Mf, o, st, kreuz=False)
        oeffnungen.append((Mf, o))
    bs.turmhelm(netz, st, mitte, TURM_R + 0.22, TURM_HOCH, 4.2)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    normal = lambda o: o.name not in ("Leerstand", "Geruest")                     # noqa: E731
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=6.0, ausschnitt=34.0, schnell=schnell)
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, normal))
        return
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, normal))
        return
    print(bs.rendern(NAME, normal))
    print(bs.rendern(NAME + "-leerstand", lambda o: o.name not in ("Geruest", "Wappen", "Fahne")))
    print(bs.rendern(NAME + "-geruest", lambda o: o.name != "Leerstand"))
    if not schnell:
        print(bs.rundum(NAME, normal))
    print(bs.anker_bild(NAME, normal))
    print(bs.nachtbild(NAME, normal))


if __name__ == "__main__":
    main()
