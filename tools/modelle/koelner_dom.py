"""Kölner Dom um 1400 – die Baustelle (Sonderbau, nur für Köln). Belegte Fläche 14 × 34 m.

    blender -b --python tools/modelle/koelner_dom.py -- [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]
    python tools/modelle/koelner_dom.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/gebaeude/koelner_dom.glb und Bilder nach docs/modelle/. Der Dom der übrigen Karten (dom.glb) bleibt.

Mischung aus Geschichte und Fantasy (Entscheidung des Nutzers, 27.10.2026, docs/bereiche/leveldesign.md „Köln-Wahrzeichen“):
    Chor        fertig, sehr hoch, Werk elbischer Steinmetze: heller, leicht silbriger Stein („Elbenstein“), kupfergrünes
                Dach, schlanke Fialen, Strebewerk mit Bögen, Maßwerk mit Ranken, grünlich-gläserne Fenster, Dachreiter
    Westwand    provisorische Bretterwand mit Holzrahmen vor dem Chor (belegt: seit 1304)
    Langhaus    niedriger Stumpf (Kapitellhöhe) aus grauem Haustein unter flachen Notdächern aus Schindeln, Querhausarme
    Nordturm    halb hoch (Fantasy), oben offen mit zerbrochener Mauerkrone und Gerüst
    Südturm     Stumpf mit hölzernem Tretrad-Baukran (Kranhaus, zwei Treträder, Ausleger über das Langhaus, Seil, Steinlast)
Menschenwerk ist grauer, gröberer Stein, elbisches Werk heller und feiner.

Objekte im GLB:
    Chor, Westwand, Langhaus, Nordturm, Suedturm, Kranhaus, Tretrad    (Teile, alle sichtbar)
    Anker_Tuer          vor dem Notportal zwischen den Türmen (Westseite, −Y)
    Anker_EwigesLicht   im Bogen des Notportals (wie beim Dom der übrigen Karten)
Keine Zustände und keine Hausfarbe: Der Dom gehört der Stadt (`pruefen.py … 14 34 --sonderbau`).

Aufbau (Blender: Z oben, Westseite mit den Türmen bei −Y, Chor bei +Y, Süden = +X; Ursprung in der Mitte der Fläche):
    y −17 … −11,6   zwei Turmstümpfe (x ±1,4 … ±6,8) mit Notportal dazwischen
    y −11,6 … 3     Langhaus- und Querhausstumpf (Wände 6,5 m) unter einem Notdach, Querhausarme bis x ±7
    y 3 … 16        Chor: Hochchor 6 m breit, Traufe 28 m, Dach bis 32 m, Dachreiter bis etwa 41 m; Umgang mit Pultdach;
                    zwölf Strebepfeiler mit Fialen und fliegenden Bögen
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import dom  # noqa: E402  (Spitzbogen, Gewände)
from bausteine import Matrix, Vector  # noqa: E402

NAME = "koelner_dom"
FLAECHE = (14.0, 34.0)

# Farben nur dieses Modells: heller Elbenstein, grünliches Glas, kupfergrünes Dach des Chors
bs.FARBEN.update({"Elbenstein": (0.78, 0.80, 0.80), "Elbenglas": (0.10, 0.25, 0.21), "Elbendach": (0.42, 0.58, 0.52)})
bs.RAUHEIT.update({"Elbenglas": 0.2})
bs.BIBLIOTHEK_JE_FLAECHE = True              # Material-Bibliothek je Fläche (Material „Farben“)

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
R_H = 3.0                    # Hochchor: Außenfläche der Wand bei x ±3
R_U = 6.0                    # Umgang: Außenfläche der Wand bei x ±6
Y_W = 3.0                    # Westwand des Chors
Y_A = 10.2                   # Beginn der Apsis (Mittelpunkt des Halbkreises)
Z_H = 28.0                   # Traufe des Hochchors
Z_U = 11.5                   # Traufe (Wandoberkante) des Umgangs
Z_UM = 14.5                  # Anschluss des Umgangdachs an den Hochchor
NEIGUNG = 55                 # Dach des Chors
APSIS = (0, 36, 72, 108, 144, 180)     # Ecken der Apsis (Grad von +X)
STATIONEN = (3.5, 5.9, 8.3, Y_A)       # Strebepfeiler der geraden Seiten
Z_L = 6.5                    # Wandoberkante des Langhausstumpfs
Y_L0 = -11.6                 # Westende des Langhauses (Ostkante der Türme)
Y_T0 = -16.9                 # Westkante der Türme (mit Sockel genau bei −17)
H_SUED, H_NORD = 13.5, 21.0
SUED = (1.4, 6.8)            # Türme: x-Bereich
NORD = (-6.8, -1.4)
KRAN = Vector((4.1, -14.3, 0.0))       # Mitte des Kranhauses auf dem Südturm


def wm(punkt, phi):
    """Wandsystem an `punkt` (Fuß der Außenfläche) für die Außennormale in Richtung `phi` (Grad von +X, gegen den Uhrzeigersinn)."""
    return Matrix.Translation(Vector(punkt)) @ Matrix.Rotation(math.radians(phi + 90), 4, "Z")


def apsis_punkt(r, w, z=0.0):
    return Vector((r * math.cos(math.radians(w)), Y_A + r * math.sin(math.radians(w)), z))


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    chor = bs.Netz("Chor")
    chor_bauen(chor, st)
    west = bs.Netz("Westwand")
    westwand_bauen(west, st)
    lang = bs.Netz("Langhaus")
    langhaus_bauen(lang, st)
    nord = bs.Netz("Nordturm")
    nordturm_bauen(nord, st)
    sued = bs.Netz("Suedturm")
    suedturm_bauen(sued, st)
    haus = bs.Netz("Kranhaus")
    rad = bs.Netz("Tretrad", alle=True)
    kran_bauen(haus, rad, st)
    return [chor.objekt(), west.objekt(), lang.objekt(), nord.objekt(), sued.objekt(), haus.objekt(), rad.objekt(),
            bs.anker("Anker_Tuer", (0.0, Y_T0 + 0.15, 0.3)),
            bs.anker("Anker_EwigesLicht", (0.0, Y_T0 + 0.7 - 0.12, 4.3), 180.0)]


# ─────────────────────────────────────────────────────────────────────────────
#  Kleinteile des Elbenwerks
# ─────────────────────────────────────────────────────────────────────────────

def fiale(netz, st, x, y, z0, r, h, stoff="Elbenstein"):
    """Schlanke Fiale: Sockelblock und vierseitige Spitze (ohne Boden)."""
    s = h * 0.2
    netz.quader((x, y, z0 + s / 2), (2 * r, 2 * r, s), st[stoff], ohne=("-z",))
    spitze = Vector((x, y, z0 + h))
    ecken = [Vector((x + sx * r * 0.82, y + sy * r * 0.82, z0 + s)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for k in range(4):
        a, b = ecken[k], ecken[(k + 1) % 4]
        netz.vieleck_nach([a, b, spitze], (a + b) / 2 - Vector((x, y, z0 + s - 0.6)), st[stoff])


def elbenfenster(netz, st, M, u, v, b, h, glas, ranken=False):
    """Hohes Spitzbogenfenster: grünliches oder dunkles Glas, Gewände und Mittelpfosten aus Elbenstein, auf Wunsch Ranken im Bogenfeld
    (zwei Blattbögen und zwei geschwungene Ranken aus dünnen Stäben)."""
    bs.platte(netz, M, dom.spitzbogen(u, v, b, h), -0.012, -0.002, st[glas])
    dom.spitzring(netz, M, u, v, b, h, 0.13, -0.10, -0.004, st["Elbenstein"])
    k = v + h - b * math.sin(math.radians(60))
    netz.quader((u, -0.06, (v + k) / 2 + 0.25), (0.07, 0.1, k - v + 0.5), st["Elbenstein"], M, ohne=("+y",))
    netz.quader((u, -0.07, v - 0.06), (b + 0.4, 0.14, 0.12), st["Elbenstein"], M, ohne=("+y",))
    if not ranken:
        return
    oben = v + h - 0.35
    unten = k + 0.15

    def linie(punkte):
        for p, q in zip(punkte, punkte[1:]):
            netz.stab((p[0], -0.05, p[1]), (q[0], -0.05, q[1]), 0.07, 0.08, st["Elbenstein"], matrix=M, quer=(0, 1, 0),
                      ohne=("+y", "+x", "-x"), mehr=0.06)

    for s in (-1, 1):                                     # Blattbögen
        linie([(u + s * 0.3 * b * math.sin(math.pi * i / 4), unten + (oben - unten) * i / 4) for i in range(5)])
        linie([(u + s * 0.46 * b * (1 - math.cos(math.pi * i / 3)) / 2, k - 0.05 + 0.9 * i / 3) for i in range(4)])   # Ranke


# ─────────────────────────────────────────────────────────────────────────────
#  Chor
# ─────────────────────────────────────────────────────────────────────────────

def chor_bauen(netz, st):
    bays = [(a + b) / 2 for a, b in zip(STATIONEN, STATIONEN[1:])]
    laenge = Y_A - Y_W
    # Hochchor: gerade Wände, Apsis, Gesims, Fenster
    for s in (-1, 1):
        netz.quader((s * (R_H - 0.4), (Y_W + Y_A) / 2, Z_H / 2), (0.8, laenge, Z_H), st["Elbenstein"], ohne=("-z",))
        netz.quader((s * (R_U - 0.3), (Y_W + Y_A) / 2, Z_U / 2), (0.6, laenge, Z_U), st["Elbenstein"], ohne=("-z",))
        netz.quader((s * (R_U + 0.04), (Y_W + Y_A) / 2, 0.45), (0.2, laenge, 0.9), st["Elbenstein"], ohne=("-z",))   # Sockel
        netz.quader((s * (R_H + 0.08), (Y_W + Y_A) / 2, Z_H - 0.3), (0.3, laenge + 0.1, 0.5), st["Elbenstein"], ohne=("-z",))
        for y in bays:
            Mw = wm((s * R_H, y, 0), 0 if s > 0 else 180)
            elbenfenster(netz, st, Mw, 0.0, 15.3, 1.5, 11.3, "Fenster", ranken=True)
            Mu = wm((s * R_U, y, 0), 0 if s > 0 else 180)
            elbenfenster(netz, st, Mu, 0.0, 3.0, 1.5, 7.2, "Elbenglas")
    for i in range(5):                                    # Apsis des Hochchors und des Umgangs
        a, b = APSIS[i], APSIS[i + 1]
        for r, h, d in ((R_H, Z_H, 0.8), (R_U, Z_U, 0.6)):
            p, q = apsis_punkt(r, a), apsis_punkt(r, b)
            sehne = (q - p).length
            M = wm((p + q) / 2, (a + b) / 2)
            netz.quader((0, d / 2, h / 2), (sehne, d, h), st["Elbenstein"], M, ohne=("-z",))
            if r == R_H:
                netz.quader((0, -0.08, Z_H - 0.28), (sehne + 0.2, 0.3, 0.5), st["Elbenstein"], M, ohne=("+y", "-z"))
                elbenfenster(netz, st, M, 0.0, 15.3, 1.2, 11.3, "Fenster", ranken=True)
            else:
                netz.quader((0, -0.04, 0.45), (sehne + 0.2, 0.2, 0.9), st["Elbenstein"], M, ohne=("+y", "-z"))
                elbenfenster(netz, st, M, 0.0, 3.0, 1.5, 7.2, "Elbenglas")
    # Dach des Hochchors (kupfergrün) mit Kegel über der Apsis
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")      # lokal x → Welt y
    dach = bs.satteldach(netz, st, Y_W - 0.2, Y_A - 0.15, -R_H, R_H, Z_H, NEIGUNG, traufe=0.2, ortgang=0.15, dicke=0.18,
                         reihe=0.5, stufe=0.03, deckung="Elbendach", kante="Elbendach", first="Elbenstein", first_dicke=0.2,
                         enden="Elbendach", matrix=drehung)
    zf = dach["z_first"]
    rb = R_H + 0.2
    zb = dach["oben_vorn"](-R_H - 0.2)
    spitze = Vector((0, Y_A, zf))
    for i in range(5):
        a, b = apsis_punkt(rb, APSIS[i], zb), apsis_punkt(rb, APSIS[i + 1], zb)
        netz.vieleck_nach([a, b, spitze], (a + b) / 2 - Vector((0, Y_A, zb - 1.0)), st["Elbendach"])
    # Pultdach des Umgangs: Rechtecke an den geraden Seiten, Trapeze um die Apsis
    zl = Z_U + 0.1 - 0.3 * (Z_UM - Z_U) / (R_U - R_H)
    ro = R_U + 0.3
    for s in (-1, 1):
        netz.vieleck_nach([(s * ro, 2.8, zl), (s * ro, Y_A, zl), (s * R_H, Y_A, Z_UM), (s * R_H, 2.8, Z_UM)], (s * 1, 0, 1), st["Elbendach"])
    for i in range(5):
        a, b = APSIS[i], APSIS[i + 1]
        na, nb = apsis_punkt(ro, a, zl), apsis_punkt(ro, b, zl)
        ia, ib = apsis_punkt(R_H, a, Z_UM), apsis_punkt(R_H, b, Z_UM)
        m = (na + nb) / 2
        netz.vieleck_nach([na, nb, ib, ia], Vector((m.x, m.y - Y_A, 0)).normalized() + Vector((0, 0, 1)), st["Elbendach"])
    # Strebewerk: Pfeiler an den geraden Seiten und rund um die Apsis
    for s in (-1, 1):
        for y in STATIONEN:
            strebe(netz, st, Vector((s * R_U, y, 0)), Vector((s, 0, 0)))
    for w in APSIS[1:-1]:
        strebe(netz, st, apsis_punkt(R_U, w), Vector((math.cos(math.radians(w)), math.sin(math.radians(w)), 0)))
    # Dachreiter: schlanke Laterne und Helm über dem Dach
    y_r = 5.0
    z0 = zf - 0.4
    bs.achteck(netz, st, (0, y_r, z0), 0.95, 0.0, 3.6, "Elbenstein")
    bs.achteck(netz, st, (0, y_r, z0), 1.05, 0.0, 0.3, "Elbenstein")
    bs.achteck(netz, st, (0, y_r, z0), 1.05, 3.4, 3.7, "Elbenstein")
    for k in range(8):                                    # schmale Öffnungen der Laterne (Seiten des Achtecks bei 0°, 45° …)
        w = 45 * k
        M = wm((0.878 * math.cos(math.radians(w)), y_r + 0.878 * math.sin(math.radians(w)), z0), w)
        bs.platte(netz, M, dom.spitzbogen(0.0, 0.8, 0.45, 2.3), -0.012, -0.002, st["Elbenglas"])
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        fiale(netz, st, sx * 0.9, y_r + sy * 0.9, z0 + 3.5, 0.2, 3.0)
    bs.turmhelm(netz, st, Vector((0, y_r, 0)), 0.9, z0 + 3.7, 9.0, stoff="Elbendach", n=8)
    # Fialen auf dem Gesims des Hochchors an den Ecken der Apsis
    for w in APSIS[1:-1]:
        p = apsis_punkt(R_H + 0.15, w)
        fiale(netz, st, p.x, p.y, Z_H - 0.6, 0.3, 3.8)


def strebe(netz, st, basis, richt):
    """Strebepfeiler an der Umgangwand `basis` (Fuß der Außenfläche) nach außen `richt`: zwei Absätze, Fiale, fliegender Bogen
    zum Pfeilerstumpf an der Hochchorwand und dessen Fiale."""
    w = math.atan2(richt.y, richt.x)
    T = Matrix.Translation(basis) @ Matrix.Rotation(w, 4, "Z")          # x nach außen, y quer
    T3 = T.to_3x3()
    quer = T3 @ Vector((0, 1, 0))
    netz.quader((0.4, 0, 6.1), (1.0, 0.8, 12.2), st["Elbenstein"], T, ohne=("-z",))
    netz.quader((0.4, 0, 12.15), (1.05, 0.9, 0.12), st["Elbenstein"], T)
    netz.quader((0.5, 0, 14.9), (0.8, 0.62, 5.5), st["Elbenstein"], T, ohne=("-z",))
    netz.quader((0.5, 0, 17.7), (0.95, 0.76, 0.12), st["Elbenstein"], T)
    f = T @ Vector((0.5, 0, 17.76))
    fiale(netz, st, f.x, f.y, f.z, 0.32, 4.6)
    # Stumpf an der Hochchorwand (liegt um R_U − R_H weiter innen) mit Fiale
    d = R_U - R_H
    netz.quader((-d + 0.55, 0, 22.6), (1.1, 0.8, 9.0), st["Elbenstein"], T, ohne=("-z",))
    netz.quader((-d + 0.55, 0, 27.15), (1.25, 0.94, 0.12), st["Elbenstein"], T)
    f = T @ Vector((-d + 0.55, 0, 27.2))
    fiale(netz, st, f.x, f.y, f.z, 0.34, 4.2)
    # fliegender Bogen vom Pfeiler (x ≈ 0,1) zum Stumpf (x ≈ −d + 1,1), steigt zuerst steil und läuft flach in den Stumpf
    a, b = Vector((0.12, 0, 15.2)), Vector((-d + 1.05, 0, 21.4))
    n = 5
    punkte = []
    for i in range(n + 1):
        s = i / n
        punkte.append(Vector((a.x + (b.x - a.x) * s, 0, a.z + (b.z - a.z) * math.sin(math.pi * s / 2))))
    for p, q in zip(punkte, punkte[1:]):
        netz.stab(T @ p, T @ q, 0.5, 0.42, st["Elbenstein"], quer=quer, ohne=("+x", "-x"), mehr=0.12)
    netz.stab(T @ (punkte[0] + Vector((0, 0, -0.3))), T @ (punkte[-1] + Vector((0.0, 0, -1.3))), 0.18, 0.2, st["Elbenstein"],
              quer=quer, ohne=("+x", "-x"))                      # Stütze unter dem Bogen


# ─────────────────────────────────────────────────────────────────────────────
#  Provisorische Westwand des Chors
# ─────────────────────────────────────────────────────────────────────────────

def westwand_bauen(netz, st):
    M = bs.wandmatrix((0, Y_W - 0.5, 0), "vorne")
    zg = Z_H + R_H * math.tan(math.radians(NEIGUNG))
    umriss = [(-R_U, 5.5), (-R_U, Z_U), (-R_H, Z_UM), (-R_H, Z_H), (0, zg), (R_H, Z_H), (R_H, Z_UM), (R_U, Z_U), (R_U, 5.5)]
    bs.platte(netz, M, umriss, 0, 0.5, st["Bretter"])
    holz = st["Geruestholz"]
    for u, v0, v1 in ((-5.6, 5.5, Z_U - 0.6), (-3.0, 5.5, Z_H), (0.0, 5.5, zg - 0.4), (3.0, 5.5, Z_H), (5.6, 5.5, Z_U - 0.6)):
        bs.balken(netz, M, u, v0, u, v1, holz, vor=0.12, breite=0.24)
    for v, h in ((7.5, R_U), (14.2, R_H), (21.0, R_H), (Z_H - 0.1, R_H)):
        bs.balken(netz, M, -h, v, h, v, holz, vor=0.12, breite=0.22)
    for u0, v0, u1, v1 in ((-5.6, 7.5, -3.0, 14.2), (5.6, 7.5, 3.0, 14.2), (-3.0, 14.2, 0.0, 21.0), (3.0, 14.2, 0.0, 21.0),
                           (-3.0, 21.0, 0.0, Z_H), (3.0, 21.0, 0.0, Z_H)):
        bs.balken(netz, M, u0, v0, u1, v1, holz, vor=0.1, breite=0.18)
    # Strebe zur Wand: zwei schräge Stützbalken von unten
    for s in (-1, 1):
        netz.stab((s * 2.2, Y_W - 1.6, 5.5), (s * 2.2, Y_W - 0.6, 13.0), 0.24, 0.24, st["Geruestholz"], quer=(1, 0, 0))


# ─────────────────────────────────────────────────────────────────────────────
#  Langhaus- und Querhausstumpf
# ─────────────────────────────────────────────────────────────────────────────

def langhaus_bauen(netz, st):
    ym = (Y_L0 + 2.5) / 2
    laenge = 2.5 - Y_L0
    for s in (-1, 1):
        netz.quader((s * 5.6, ym, Z_L / 2), (0.8, laenge, Z_L), st["Haustein"], ohne=("-z",))
        netz.quader((s * 5.95, ym, 0.45), (0.3, laenge + 0.1, 0.9), st["Bruchstein dunkel"], ohne=("-z",))
        netz.quader((s * 5.98, ym, Z_L - 0.15), (0.3, laenge + 0.1, 0.3), st["Bruchstein"], ohne=("-z",))
        M = wm((s * 6.0, 0, 0), 0 if s > 0 else 180)
        for y in (-9.6, -6.8, -4.0):                      # Pfeilerstümpfe außen, rau abgebrochen
            netz.quader((s * 6.4, y, 3.7), (0.9, 0.8, 7.4), st["Haustein"], ohne=("-z",))
            netz.quader((s * 6.4, y, 7.5), (1.0, 0.9, 0.2), st["Bruchstein"], ohne=("-z",))
        for y in (-8.2, -5.4):                            # Fensteransätze: dunkle Öffnung, Sohlbank, Holzbrett statt Bogen
            Mw = wm((s * 6.0, y, 0), 0 if s > 0 else 180)
            bs.platte(netz, Mw, [(-0.6, 1.6), (0.6, 1.6), (0.6, 5.0), (-0.6, 5.0)], -0.03, -0.004, st["Loch"])
            netz.quader((0, -0.06, 1.5), (1.5, 0.12, 0.14), st["Bruchstein"], Mw, ohne=("+y",))
            netz.quader((0, -0.05, 5.0), (1.5, 0.1, 0.5), st["Holz alt"], Mw, ohne=("+y",))
        # Querhausarm: niedriger Anbau mit Pultdach
        netz.quader((s * 6.5, -0.4, 2.8), (0.6, 5.0, 5.6), st["Haustein"], ohne=("-z",))
        netz.quader((s * 6.8, -0.4, 5.5), (0.2, 5.0, 0.3), st["Bruchstein"], ohne=("-z",))
        t = Matrix.Translation((s * 6.4, -0.4, 5.85)) @ Matrix.Rotation(math.radians(s * 21.8), 4, "Y")       # fällt nach außen
        netz.quader((0, 0, 0), (1.1, 5.4, 0.1), st["Holz alt"], t)
    # Rückwand des Langhauses zwischen den Türmen
    netz.quader((0, Y_L0 + 0.2, 3.25), (12.0, 0.5, 6.5), st["Haustein"], ohne=("-z",))
    # Notdach: flaches Satteldach aus Schindeln über Langhaus und Querhaus, im Westen ein Giebel aus Brettern
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")
    dach = bs.satteldach(netz, st, Y_L0 + 0.4, 2.5, -6.0, 6.0, Z_L, 24, traufe=0.45, ortgang=0.3, enden="Holz alt", matrix=drehung,
                         **bs.SCHINDEL)
    hoch = 6.0 * dach["tan"]
    M = bs.wandmatrix((0, Y_L0 - 0.5, Z_L), "vorne")
    bs.platte(netz, M, [(-6.0, 0), (6.0, 0), (0, hoch)], 0, 0.75, st["Bretter"])
    for u in (-4.0, -2.0, 2.0, 4.0):                       # Sparren des Dachstuhls, die an der Westseite herausschauen
        netz.stab((u, Y_L0 - 0.55, Z_L + 0.1), (u * 0.5, Y_L0 - 0.55, Z_L + hoch * 0.8), 0.18, 0.12, st["Geruestholz"], quer=(0, 1, 0))
    # Steinblöcke und Bauholz vor dem Notportal
    rnd = random.Random(11)
    for k in range(7):
        if k % 2:
            netz.quader((-4.0 + 1.3 * k, Y_T0 + 1.0 + rnd.random() * 0.6, 0.4), (0.9, 0.7, 0.8), st["Haustein"], ohne=("-z",))
        else:
            netz.quader((-3.7 + 1.3 * k, Y_T0 + 0.9 + rnd.random() * 0.5, 0.15), (1.8, 0.2, 0.3), st["Geruestholz"], ohne=("-z",))
    # Notportal zwischen den Türmen: Mauer, Spitzbogentür, Gewände, Stufen, Holzabdeckung
    yp = Y_T0 + 0.7
    netz.quader((0, yp + 0.5, 4.0), (3.0, 1.0, 8.0), st["Haustein"], ohne=("-z",))
    Mp = bs.wandmatrix((0, yp, 0), "vorne")
    bs.platte(netz, Mp, dom.spitzbogen(0.0, 0.0, 2.0, 4.6), -0.03, -0.002, st["Holz alt"])
    dom.spitzring(netz, Mp, 0.0, 0.0, 2.0, 4.6, 0.25, -0.16, -0.004, st["Bruchstein"])
    for v in (0.9, 2.1, 3.2):
        netz.quader((0, -0.06, v), (1.8, 0.03, 0.09), st["Eisen"], Mp, ohne=("+y",))
    netz.quader((0, -0.25, 0.075), (2.8, 0.5, 0.15), st["Bruchstein"], Mp, ohne=("+y", "-z"))
    netz.quader((0, yp + 0.6, 8.1), (3.2, 1.4, 0.25), st["Holz alt"], ohne=("-z",))


# ─────────────────────────────────────────────────────────────────────────────
#  Türme
# ─────────────────────────────────────────────────────────────────────────────

def schlitz(netz, st, M, u, v, b=0.55, h=2.2):
    bs.platte(netz, M, dom.spitzbogen(u, v, b, h), -0.012, -0.002, st["Loch"])
    dom.spitzring(netz, M, u, v, b, h, 0.14, -0.08, -0.004, st["Bruchstein"])


def turm_aussen(netz, st, x0, x1, hoehe, hohl, krone):
    """Turmstumpf aus Haustein mit Sockel, Gesimsbändern, Schlitzfenstern und zerbrochener Mauerkrone (Steine bis `krone` Meter hoch)."""
    y0, y1 = Y_T0, Y_L0
    xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
    if hohl:
        t = 1.2
        netz.quader((xm, y0 + t / 2, hoehe / 2), (x1 - x0, t, hoehe), st["Haustein"], ohne=("-z",))
        netz.quader((xm, y1 - t / 2, hoehe / 2), (x1 - x0, t, hoehe), st["Haustein"], ohne=("-z",))
        netz.quader((x0 + t / 2, ym, hoehe / 2), (t, y1 - y0 - 2 * t, hoehe), st["Haustein"], ohne=("-z",))
        netz.quader((x1 - t / 2, ym, hoehe / 2), (t, y1 - y0 - 2 * t, hoehe), st["Haustein"], ohne=("-z",))
    else:
        netz.quader((xm, ym, hoehe / 2), (x1 - x0, y1 - y0, hoehe), st["Haustein"], ohne=("-z",))

    def ring(z, h, vor, stoff):
        """Band um den Turm, `vor` Meter vor der Wand."""
        d = 2 * vor
        netz.quader((xm, y0, z), (x1 - x0 + d, d, h), st[stoff], ohne=("-z",))
        netz.quader((xm, y1, z), (x1 - x0 + d, d, h), st[stoff], ohne=("-z",))
        netz.quader((x0, ym, z), (d, y1 - y0 - d, h), st[stoff], ohne=("-z",))
        netz.quader((x1, ym, z), (d, y1 - y0 - d, h), st[stoff], ohne=("-z",))

    ring(0.55, 1.1, 0.1, "Bruchstein dunkel")
    for f in (0.33, 0.66):
        ring(hoehe * f, 0.3, 0.08, "Bruchstein")
    # Schlitzfenster: Gassenseite (West), Außenseite und Innenseite zum Portal
    sued = x0 > 0
    seiten = [wm((xm, y0, 0), -90), wm((x1 if sued else x0, ym, 0), 0 if sued else 180), wm((x0 if sued else x1, ym, 0), 180 if sued else 0)]
    zs = (0.2, 0.5, 0.78) if hoehe > 15 else (0.28, 0.62)
    for M in seiten:
        for f in zs:
            schlitz(netz, st, M, 0.0, hoehe * f - 1.1)
    # Mauerkrone: Steine in unterschiedlicher Höhe, die Mauer ist nie fertig geworden
    rnd = random.Random(5 if hohl else 9)
    n = 6
    rand = []
    for k in range(n):
        f = (k + 0.5) / n
        rand += [(x0 + 0.6 + f * (x1 - x0 - 1.2), y0 + 0.6), (x0 + 0.6 + f * (x1 - x0 - 1.2), y1 - 0.6)]
        g = y0 + 0.6 + f * (y1 - y0 - 1.2)
        rand += [(x0 + 0.6, g), (x1 - 0.6, g)]
    for (px, py) in rand:
        h = 0.3 + rnd.random() * (krone - 0.3)
        netz.quader((px, py, hoehe + h / 2 - 0.05), (0.85, 0.85, h), st["Haustein"] if rnd.random() < 0.6 else st["Bruchstein"], ohne=("-z",))


def geruest(netz, st, x0, x1, y0, y1, z_fuss, ebenen):
    """Gerüst im Innern eines offenen Turms: Eckstangen von `z_fuss` bis über die oberste Ebene, Riegel und Bohlen auf den Ebenen, Leiter."""
    holz = st["Geruestholz"]
    kopf = ebenen[-1] + 1.6
    for x in (x0, x1):
        for y in (y0, y1):
            netz.stab((x, y, z_fuss), (x, y, kopf), 0.14, 0.14, holz)
    for z in ebenen:
        for y in (y0, y1):
            netz.stab((x0 - 0.1, y, z), (x1 + 0.1, y, z), 0.1, 0.1, holz)
        for x in (x0, x1):
            netz.stab((x, y0 - 0.1, z), (x, y1 + 0.1, z), 0.1, 0.1, holz, quer=(1, 0, 0))
    for k in range(4):
        netz.quader(((x0 + x1) / 2, y0 + (y1 - y0) * (k + 0.5) / 4, ebenen[0] + 0.08), (x1 - x0 + 0.5, (y1 - y0) / 4 - 0.04, 0.06), holz)
    for dx in (-0.2, 0.2):
        netz.stab(((x0 + x1) / 2 + dx, y0 - 0.3, z_fuss), ((x0 + x1) / 2 + dx, y0 - 0.05, ebenen[0] + 0.9), 0.07, 0.05, holz, quer=(1, 0, 0))


def nordturm_bauen(netz, st):
    turm_aussen(netz, st, NORD[0], NORD[1], H_NORD, hohl=True, krone=1.6)
    # Boden aus Bohlen im Innern, damit man beim Hineinsehen nicht ins Leere schaut
    netz.quader(((NORD[0] + NORD[1]) / 2, (Y_T0 + Y_L0) / 2, H_NORD - 3.0), (NORD[1] - NORD[0] - 2.4, Y_L0 - Y_T0 - 2.4, 0.2), st["Holz alt"])
    geruest(netz, st, -5.4, -4.0, -15.5, -13.0, H_NORD - 2.9, (H_NORD + 1.0, H_NORD + 2.2))
    for k in range(3):                                     # gestapelte Steine für den Weiterbau
        netz.quader((-3.3 + 0.2 * (k % 2), -13.5 - 0.7 * k, H_NORD - 2.9 + 0.25), (0.8, 0.6, 0.5), st["Haustein"])


def suedturm_bauen(netz, st):
    turm_aussen(netz, st, SUED[0], SUED[1], H_SUED, hohl=False, krone=0.8)
    # Bohlenboden für den Kran, die Mauerkrone steht außen herum
    netz.quader(((SUED[0] + SUED[1]) / 2, (Y_T0 + Y_L0) / 2, H_SUED + 0.02), (SUED[1] - SUED[0] - 1.8, Y_L0 - Y_T0 - 1.8, 0.14), st["Bretter"])


# ─────────────────────────────────────────────────────────────────────────────
#  Tretradkran auf dem Südturm
# ─────────────────────────────────────────────────────────────────────────────

def kran_bauen(haus, rad, st):
    holz = st["Holz"]
    kx, ky = KRAN.x, KRAN.y
    z0 = H_SUED + 0.15
    hx, hy = 1.35, 1.2
    fuss = z0 + 1.55                                       # Boden des Kranhauses auf Pfosten
    for sx in (-1, 1):
        for sy in (-1, 1):
            haus.stab((kx + sx * hx, ky + sy * hy, z0), (kx + sx * hx, ky + sy * hy, fuss), 0.28, 0.28, st["Geruestholz"])
    haus.quader((kx, ky, fuss + 0.1), (2 * hx + 0.3, 2 * hy + 0.3, 0.2), holz, ohne=("-z",))
    h = 2.4
    for (cx, cy, br, tf) in ((kx, ky - hy, 2 * hx, 0.12), (kx, ky + hy, 2 * hx, 0.12), (kx - hx, ky, 0.12, 2 * hy), (kx + hx, ky, 0.12, 2 * hy)):
        haus.quader((cx, cy, fuss + 0.2 + h / 2), (br, tf, h), st["Bretter"], ohne=("-z", "+z"))
    for sx in (-1, 1):
        for sy in (-1, 1):
            haus.stab((kx + sx * hx, ky + sy * hy, fuss + 0.2), (kx + sx * hx, ky + sy * hy, fuss + 0.2 + h), 0.2, 0.2, st["Fachwerk"])
    drehung = Matrix.Rotation(math.radians(90), 4, "Z")              # First entlang Y (zum Ausleger)
    ob = fuss + 0.2 + h
    d = bs.satteldach(haus, st, ky - hy, ky + hy, -(kx + hx), -(kx - hx), ob, 38, traufe=0.3, ortgang=0.25, enden="Holz alt", matrix=drehung,
                      **bs.SCHINDEL)
    hoch = hx * d["tan"]
    for y, seite in ((ky - hy - 0.2, "vorne"), (ky + hy + 0.2, "hinten")):
        bs.platte(haus, bs.wandmatrix((kx, y, ob), seite), [(-hx, 0), (hx, 0), (0, hoch)], 0, 0.12, st["Bretter"])
    # Ausleger über das Langhaus: steigt vom Haus bis über die Turmkante, Strebe, Rolle, Seil, Haken, Steinlast
    a = Vector((kx, ky - hy + 0.4, ob + 0.4))
    spitze = Vector((kx, ky + hy + 3.4, ob + 1.9))
    haus.stab(a, spitze, 0.3, 0.26, st["Holz"], quer=(1, 0, 0))
    haus.stab(Vector((kx, ky + hy + 0.1, ob - 0.9)), a.lerp(spitze, 0.7), 0.2, 0.18, st["Holz"], quer=(1, 0, 0))
    for sx in (-1, 1):
        haus.stab(spitze + Vector((sx * 0.17, -0.55, -0.2)), spitze + Vector((sx * 0.17, 0.15, 0.1)), 0.24, 0.05, st["Holz alt"], quer=(1, 0, 0))
    haus.stab((kx - 0.14, spitze.y + 0.05, spitze.z - 0.05), (kx + 0.14, spitze.y + 0.05, spitze.z - 0.05), 0.3, 0.3, st["Eisen"], quer=(0, 1, 0))
    haken = Vector((kx, spitze.y + 0.2, H_SUED - 0.2))                # hängt über dem Langhausdach, nicht auf dem Turm
    haus.stab(spitze + Vector((0, 0.05, 0.1)), Vector((kx, ky + hy - 0.3, ob + 0.3)), 0.04, 0.04, st["Seil"])
    haus.stab(spitze + Vector((0, 0.22, -0.1)), haken + Vector((0, 0, 0.2)), 0.04, 0.04, st["Seil"])
    haus.quader(haken + Vector((0, 0, 0.1)), (0.1, 0.08, 0.2), st["Eisen"])
    for sx in (-1, 1):
        haus.stab(haken + Vector((sx * 0.03, 0, 0.0)), haken + Vector((sx * 0.45, 0, -0.75)), 0.03, 0.03, st["Seil"])
    haus.quader(haken + Vector((0, 0, -1.0)), (1.0, 0.75, 0.7), st["Haustein"])
    # Treträder links und rechts außen am Kranhaus, gemeinsame Achse
    achse_z = fuss + 1.3
    n, r, b = 12, 2.0, 0.4
    for mx in (kx - hx - 0.7, kx + hx + 0.7):
        for x in (mx - b, mx + b):
            for k in range(n):
                w0, w1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
                wm_ = (w0 + w1) / 2
                dd = 0.05 + 0.003 * (k % 2)
                for rr, normale in ((r, 1), (r - 0.14, -1)):
                    p = [(x - dd, ky + rr * math.cos(w0), achse_z + rr * math.sin(w0)), (x + dd, ky + rr * math.cos(w0), achse_z + rr * math.sin(w0)),
                         (x + dd, ky + rr * math.cos(w1), achse_z + rr * math.sin(w1)), (x - dd, ky + rr * math.cos(w1), achse_z + rr * math.sin(w1))]
                    rad.vieleck_nach(p, (0, normale * math.cos(wm_), normale * math.sin(wm_)), st["Holz"])
                for sx in (-1, 1):
                    p = [(x + sx * dd, ky + r * math.cos(w0), achse_z + r * math.sin(w0)), (x + sx * dd, ky + r * math.cos(w1), achse_z + r * math.sin(w1)),
                         (x + sx * dd, ky + (r - 0.14) * math.cos(w1), achse_z + (r - 0.14) * math.sin(w1)),
                         (x + sx * dd, ky + (r - 0.14) * math.cos(w0), achse_z + (r - 0.14) * math.sin(w0))]
                    rad.vieleck_nach(p, (sx, 0, 0), st["Holz"])
            for k in range(4):                                       # Speichen
                w = 2 * math.pi * (k + 0.5) / 4
                rad.stab((x, ky, achse_z), (x, ky + (r - 0.1) * math.cos(w), achse_z + (r - 0.1) * math.sin(w)), 0.1, 0.1, st["Holz"], quer=(1, 0, 0))
        for k in range(n):                                           # Trittbretter
            w = 2 * math.pi * (k + 0.5) / n
            t = Matrix.Translation((mx, ky + (r - 0.165) * math.cos(w), achse_z + (r - 0.165) * math.sin(w))) @ Matrix.Rotation(w, 4, "X")
            rad.quader((0, 0, 0), (2 * b - 0.1, 0.05, 0.3), st["Bretter"], matrix=t)
    rad.stab((kx - hx - 1.3, ky, achse_z), (kx + hx + 1.3, ky, achse_z), 0.26, 0.26, holz, quer=(0, 1, 0))      # Welle durch das Haus


# ─────────────────────────────────────────────────────────────────────────────
#  Ausgabe
# ─────────────────────────────────────────────────────────────────────────────

def main():
    import bpy
    objekte = bauen()
    bs.bericht(NAME, objekte)
    vorher, nachher = bs.farben_zusammenlegen(objekte)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    alles = lambda o: True                                                         # noqa: E731
    bs.vorschau_buehne(FLAECHE, ziel_hoehe=19.0, ausschnitt=74.0, schnell=schnell)
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, alles, 3))
        return
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, alles))
        return
    print(bs.rendern(NAME, alles, 3))                                              # Nordwest (vorne links)
    print(bs.rendern(NAME + "-suedost", alles, 1))                                 # Südost (hinten rechts)
    if "--nah" in sys.argv:
        szene = bpy.context.scene
        for o in szene.objects:
            if o.type == "MESH":
                o.hide_render = False
        for name, drehung, ziel, breite in (("-nah-chor", 1, (0, 9, 16), 34.0), ("-nah-kran", 0, (4.1, -13, 15), 26.0),
                                            ("-nah-nordturm", 3, (-4, -14, 16), 26.0)):
            szene["_ziel"] = 0.0
            bs.blick(drehung)
            kam = szene.camera
            kam.location += Vector(ziel)
            kam.data.ortho_scale = breite
            print(bs.rendern_einzeln(NAME + name))
        szene["_ziel"] = 16.0
        return
    if not schnell:
        print(bs.rundum(NAME, alles))
    print(bs.anker_bild(NAME, alles))
    print(bs.nachtbild(NAME, alles, 3))


if __name__ == "__main__":
    main()
