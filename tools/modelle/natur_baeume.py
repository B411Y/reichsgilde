"""Bäume fürs Umland – ein Baukasten, je Art zwei oder drei Varianten.

    blender -b --python tools/modelle/natur_baeume.py        oder
    python tools/modelle/natur_baeume.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/natur_baeume.glb und docs/modelle/natur_baeume.png.

Knoten (Ursprung je am Stammfuß, Höhe in Klammern):
    Tanne1–3        Nadelbaum mit gestuften Kegeln (11, 8, 6 m) – wie bisher im Spiel, aber ausgearbeitet
    Eiche1–3        knorriger Stamm, breite Krone aus mehreren Ballen (9–11 m)
    Buche1–2        glatter grauer Stamm, hohe ovale Krone (12 m)
    Birke1–3        weißer Stamm mit schwarzen Flecken, lichte schmale Krone (7–9 m)
    Linde1–2        runde Krone wie die Dorflinde (7 m)
    Apfelbaum1–2    kurzer Stamm, runde Krone mit roten Äpfeln (4–5 m)
    Kopfweide1–2    dicker kurzer Stamm mit Kopf, Ruten nach oben (am Bach, 4 m)
    Baum_tot1–2     abgestorbener Baum ohne Laub, vergraut (6–8 m)
Laub: „Laub“, „Laub hell“, „Laub dunkel“ – das Spiel färbt sie je Jahreszeit (siehe natur.py).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "natur_baeume"


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    teile = []

    def teil(name, bauer, *args):
        netz = bs.Netz(name)
        bauer(netz, st, *args)
        teile.append(netz.objekt())

    for i, (h, r) in enumerate(((11.0, 2.4), (8.0, 1.9), (6.0, 1.5))):
        teil(f"Tanne{i + 1}", lambda n, s, h=h, r=r, i=i: lb.tanne(n, s, (0, 0), hoehe=h, r=r, saat=100 + i))
    for i, h in enumerate((10.5, 9.0, 11.0)):
        teil(f"Eiche{i + 1}", eiche, h, 200 + i)
    for i, h in enumerate((12.0, 10.0)):
        teil(f"Buche{i + 1}", buche, h, 300 + i)
    for i, h in enumerate((9.0, 7.5, 8.2)):
        teil(f"Birke{i + 1}", birke, h, 400 + i)
    for i, h in enumerate((7.0, 6.2)):
        teil(f"Linde{i + 1}", lambda n, s, h=h, i=i: linde(n, s, h, 500 + i))
    for i, h in enumerate((4.6, 4.0)):
        teil(f"Apfelbaum{i + 1}", apfelbaum, h, 600 + i)
    for i, h in enumerate((4.0, 3.4)):
        teil(f"Kopfweide{i + 1}", kopfweide, h, 700 + i)
    for i, h in enumerate((8.0, 6.0)):
        teil(f"Baum_tot{i + 1}", baum_tot, h, 800 + i)
    return teile


def eiche(netz, st, h, saat):
    """Knorriger Stamm, der sich in drei Äste teilt; darauf mehrere breite Ballen."""
    z = nt.zufall(saat)
    gabel = Vector((z.uniform(-0.2, 0.2), z.uniform(-0.2, 0.2), h * 0.36))
    nt.ast(netz, st, (0, 0, 0), gabel, 0.42, 0.34, n=7)
    nt.ast(netz, st, (0, 0, 0), (0, 0, 0.5), 0.55, 0.42, n=7)                      # Wurzelanlauf
    ballen = []
    for k in range(3):
        w = 2 * math.pi * k / 3 + z.uniform(-0.3, 0.3)
        ende = gabel + Vector((math.cos(w) * h * 0.2, math.sin(w) * h * 0.2, h * z.uniform(0.18, 0.28)))
        nt.ast(netz, st, gabel, ende, 0.26, 0.12, n=5)
        ballen.append(ende)
    for k, m in enumerate(ballen):
        nt.krone(netz, st, m + Vector((0, 0, h * 0.06)), h * 0.24, h * 0.17, saat=saat * 10 + k,
                 stoffe=("Laub dunkel", "Laub", "Laub hell"))
    nt.krone(netz, st, gabel + Vector((0, 0, h * 0.5)), h * 0.26, h * 0.18, saat=saat * 10 + 9,
             stoffe=("Laub", "Laub hell"))


def buche(netz, st, h, saat):
    """Gerader, glatter grauer Stamm, hohe ovale Krone, unten ein zweiter Ballen."""
    nt.ast(netz, st, (0, 0, 0), (0, 0, h * 0.55), 0.3, 0.2, stoff="Rinde hell", n=7)
    nt.ast(netz, st, (0, 0, h * 0.4), (h * 0.12, 0.1, h * 0.6), 0.14, 0.07, stoff="Rinde hell", n=5)
    nt.krone(netz, st, (0, 0, h * 0.68), h * 0.25, h * 0.3, saat=saat, stoffe=("Laub", "Laub hell", "Laub"))
    nt.krone(netz, st, (h * 0.12, 0.2, h * 0.5), h * 0.17, h * 0.13, saat=saat + 1, stoffe=("Laub dunkel", "Laub"))


def birke(netz, st, h, saat):
    """Weißer, leicht schiefer Stamm mit schwarzen Flecken, schmale lichte Krone aus zwei Teilen."""
    z = nt.zufall(saat)
    oben = Vector((z.uniform(-0.3, 0.3), z.uniform(-0.3, 0.3), h * 0.8))
    nt.ast(netz, st, (0, 0, 0), oben, 0.17, 0.08, stoff="Birkenrinde", n=6)
    for k in range(5):                                                   # Flecken: kleine dunkle Ringe
        t = 0.12 + 0.13 * k
        p = Vector((0, 0, 0)).lerp(oben, t)
        r = 0.17 + (0.08 - 0.17) * t + 0.012
        nt.ast(netz, st, p, p + (oben.normalized() * 0.12), r, r * 0.97, stoff="Rinde schwarz", n=6)
    nt.krone(netz, st, oben + Vector((0, 0, -h * 0.12)), h * 0.17, h * 0.24, saat=saat, stoffe=("Laub hell", "Laub"))
    nt.krone(netz, st, oben + Vector((h * 0.08, 0, -h * 0.34)), h * 0.12, h * 0.12, saat=saat + 1,
             stoffe=("Laub hell", "Laub"))


def linde(netz, st, h, saat):
    nt.ast(netz, st, (0, 0, 0), (0, 0, h * 0.45), 0.32, 0.24, n=7)
    nt.krone(netz, st, (0, 0, h * 0.66), h * 0.34, h * 0.32, saat=saat, stoffe=("Laub", "Laub hell"), n=8)


def apfelbaum(netz, st, h, saat):
    """Kurzer, krummer Stamm, breite runde Krone, rote Äpfel außen auf der Krone."""
    z = nt.zufall(saat)
    gabel = Vector((z.uniform(-0.2, 0.2), 0.1, h * 0.35))
    nt.ast(netz, st, (0, 0, 0), gabel, 0.18, 0.14, n=6)
    mitte = gabel + Vector((0, 0, h * 0.32))
    nt.krone(netz, st, mitte, h * 0.36, h * 0.26, saat=saat, stoffe=("Laub", "Laub dunkel"), n=8)
    for p in nt.punkte_auf_krone(mitte, h * 0.36, h * 0.26, 12, saat):
        nt.tupfen(netz, st, p, 0.11, "Apfel")


def kopfweide(netz, st, h, saat):
    """Dicker, kurzer Stamm mit knotigem Kopf, aus dem dünne Ruten mit silbrigem Laub aufsteigen."""
    z = nt.zufall(saat)
    kopf = Vector((0, 0, h * 0.5))
    nt.ast(netz, st, (0, 0, 0), kopf, 0.4, 0.46, n=7)
    nt.krone(netz, st, kopf + Vector((0, 0, 0.12)), 0.55, 0.25, saat=saat, stoffe=("Rinde", "Moos"), n=6)
    for k in range(7):
        w = 2 * math.pi * k / 7 + z.uniform(-0.2, 0.2)
        fuss = kopf + Vector((math.cos(w) * 0.35, math.sin(w) * 0.35, 0.2))
        spitze = fuss + Vector((math.cos(w) * h * 0.18, math.sin(w) * h * 0.18, h * z.uniform(0.38, 0.5)))
        nt.ast(netz, st, fuss, spitze, 0.05, 0.02, n=4)
        lb.kegel(netz, ((fuss.x + spitze.x) / 2, (fuss.y + spitze.y) / 2), 0.38, fuss.z + 0.5, spitze.z + 0.25,
                 st["Laub hell"], st["Laub"], n=5, dreh=w)


def baum_tot(netz, st, h, saat):
    """Abgestorbener Baum: vergrauter Stamm, abgebrochene Spitze, kahle Äste."""
    z = nt.zufall(saat)
    oben = Vector((z.uniform(-0.3, 0.3), z.uniform(-0.3, 0.3), h * 0.85))
    nt.ast(netz, st, (0, 0, 0), oben, 0.3, 0.12, stoff="Totholz", n=6)
    for k in range(5):
        t = 0.35 + 0.12 * k
        p = Vector((0, 0, 0)).lerp(oben, t)
        w = 2 * math.pi * k / 5 * 2.1 + z.uniform(-0.3, 0.3)
        laenge = h * z.uniform(0.18, 0.3) * (1.1 - t * 0.5)
        ende = p + Vector((math.cos(w) * laenge, math.sin(w) * laenge, laenge * z.uniform(0.3, 0.8)))
        nt.ast(netz, st, p, ende, 0.08, 0.03, stoff="Totholz", n=4)
        zweig = ende + Vector((math.cos(w + 0.8) * laenge * 0.35, math.sin(w + 0.8) * laenge * 0.35, laenge * 0.3))
        nt.ast(netz, st, ende, zweig, 0.035, 0.015, stoff="Totholz", n=3)


def main():
    nt.ausgeben(NAME, bauen(), spalten=7, abstand=9.0, zeilenabstand=17.0, ziel_hoehe=4.0, ausschnitt=70.0)


if __name__ == "__main__":
    main()
