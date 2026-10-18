"""Wachturm des Gardisten (Umland): steinerner Turm an der Landstraße. Fläche 12 × 12 m.

    blender -b --python tools/modelle/wachturm.py        oder
    python tools/modelle/wachturm.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/wachturm.glb und Bilder nach docs/modelle/.

Objekte im GLB (siehe landbau.py):
    Haus        quadratischer Turm aus Bruchstein mit Eckquadern, Schießscharten, Tür über einer Treppe,
                Wehrplatte mit Zinnen und eisernem Feuerkorb, Palisade um den Hof
    Arbeit      Glut im Feuerkorb (Material „Glut“, leuchtet), Speerständer, Fass, Brennholz, Laterne an der Tür
    Fahne       am Mast auf der Wehrplatte
    Leerstand   Bretter vor Tür und Fenster, umgestürzte Pfähle der Palisade, Unkraut
    Anker_Tuer  am Fuß der Treppe (Ein- und Ausgang), Anker_Karren vor dem Hof
Der Gardist hat keinen Karren (wie bisher im Spiel); der Anker ist trotzdem da, damit alle Umlandbetriebe gleich sind.

Aufbau (Blender: Z oben, Weg bei -Y; Fläche x ±6, y ±6): der Turm in der Mitte, etwas nach hinten gerückt,
die Palisade um den Hof, vorn offen zum Weg.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Oeffnung, Vector  # noqa: E402

NAME = "wachturm"
FLAECHE = (12.0, 12.0)
TX, TY, TB, TH = 0.0, 1.2, 4.4, 11.0           # Turmmitte, Breite, Höhe der Mauer bis zur Wehrplatte
TUER_V = 1.5                                   # die Tür liegt über einer Treppe


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus, arbeit, fahne, leer = bs.Netz("Haus"), bs.Netz("Arbeit"), bs.Netz("Fahne"), bs.Netz("Leerstand")

    lb.flaeche(haus, st, lb.rechteck(-6, -6, 6, 6), 0.008, "Gras")
    lb.flaeche(haus, st, [(-1.4, -6), (1.4, -6), (2.6, -0.8), (-2.6, -0.8)], 0.014, "Erde hell")
    oeffnungen = turm(haus, st)
    # Palisade: spitze Pfähle, vorn offen
    for i, (a, b) in enumerate((((-4.6, -3.2), (-5.4, 5.4)), ((-5.4, 5.4), (5.4, 5.4)), ((5.4, 5.4), (4.6, -3.2)))):
        palisade(haus, st, a, b, erster=i == 0)

    # ── Arbeit ────────────────────────────────────────────────────────────────
    z = TH + 0.12
    for k in range(5):                                                  # Glut im Korb
        w = 2 * math.pi * k / 5 + 0.3
        arbeit.quader((TX + 0.18 * math.cos(w), TY + 0.18 * math.sin(w), z + 1.1 + 0.05 * k), (0.28, 0.28, 0.18), st["Glut"])
    for k in range(4):                                                  # Speerständer
        x = 3.0 + 0.22 * k
        arbeit.stab((x, -1.6, 0.0), (x + 0.05, -1.4, 2.4), 0.05, 0.05, st["Holz"])
        lb.kegel(arbeit, (x + 0.05, -1.4), 0.06, 2.4, 2.75, st["Eisen"], n=4)
    arbeit.stab((2.8, -1.5, 1.1), (3.9, -1.5, 1.1), 0.08, 0.06, st["Holz"])
    bs.fass(arbeit, st, Vector((-2.9, -1.4, 0)), r=0.32, h=0.85)
    lb.polter(arbeit, st, (-3.6, 2.4), 1.6, reihen=3, r=0.16, quer=True)
    arbeit.quader((TX + 0.95, TY - TB / 2 - 0.2, TUER_V + 2.2), (0.18, 0.18, 0.26), st["Glut"])           # Laterne
    arbeit.quader((TX + 0.95, TY - TB / 2 - 0.2, TUER_V + 2.36), (0.24, 0.24, 0.06), st["Eisen"])
    bs.fahne(fahne, st, (TX - 1.5, TY + 1.5, TH + 0.12), hoehe=3.4, richtung=1)

    # ── Leerstand ─────────────────────────────────────────────────────────────
    lb.vernageln(leer, st, oeffnungen)
    for k, (x, y, w) in enumerate(((-5.0, 1.0, 20), (4.0, 3.6, -125), (-1.0, 4.4, -10))):
        r = math.radians(w)
        leer.stab((x, y, 0.12), (x + 2.4 * math.cos(r), y + 2.4 * math.sin(r), 0.12), 0.22, 0.22, st["Holz alt"], quer=(0, 0, 1))
    lb.unkraut(leer, st, [(-1.0, -3.0), (1.6, -2.0), (-3.0, 0.0), (3.4, -0.4), (-2.0, 4.0), (2.4, 4.4), (0.0, -5.0)], saat=12)

    anker = [bs.anker("Anker_Tuer", (TX, TY - TB / 2 - 2.2, 0.0)), bs.anker("Anker_Karren", (0.0, -5.3, 0.0))]
    return lb.knoten(haus, arbeit, fahne, leer, anker)


def turm(netz, st):
    """Turm mit Sockel, Fugen, Eckquadern, Scharten, Tür über der Treppe, Wehrplatte mit Zinnen und Feuerkorb."""
    h = TB / 2
    netz.quader((TX, TY, 0.2), (TB + 0.3, TB + 0.3, 0.4), st["Bruchstein dunkel"], ohne=("-z",))
    netz.quader((TX, TY, 0.4 + (TH - 0.4) / 2), (TB, TB, TH - 0.4), st["Bruchstein"], ohne=("-z", "+z"))
    oeffnungen = []
    for seite, (x, y) in (("vorne", (TX, TY - h)), ("hinten", (TX, TY + h)), ("links", (TX - h, TY)), ("rechts", (TX + h, TY))):
        M = bs.wandmatrix((x, y, 0.4), seite)
        bs.fugen(netz, M, TB, 0, TH - 0.4, st, abstand=0.5)
        u = 1.2 if seite == "vorne" else 0.0                           # vorn neben Tür und Fenster
        for v in (3.4, 6.6):                                            # Schießscharten
            bs.platte(netz, M, [(u - 0.07, v), (u + 0.07, v), (u + 0.07, v + 0.9), (u - 0.07, v + 0.9)], -0.008, -0.004,
                      st["Loch"])
        if seite == "vorne":
            tuer = Oeffnung("tuer", -0.6, TUER_V - 0.4, 1.0, 2.0)
            bs.gewaende(netz, M, tuer, st, kreuz=False, stein="Sandstein")
            bs.tuer(netz, M, tuer, st)
            oeffnungen.append((M, tuer))
            fenster = Oeffnung("fenster", -0.6, 6.0, 0.6, 0.9, laeden=False)
            bs.gewaende(netz, M, fenster, st, kreuz=False)
            oeffnungen.append((M, fenster))
    for sx in (-1, 1):
        for sy in (-1, 1):
            bs.eckquader(netz, st, TX + sx * h, TY + sy * h, sx, sy, 0.4, TH - 0.6, stoff="Sandstein")
    # Treppe vor der Tür (Stufen aus Stein, von links nach oben)
    netz.quader((TX - 0.6, TY - h - 0.3, 0.735), (1.1, 0.6, 1.47), st["Bruchstein dunkel"], ohne=("-z",))    # Podest
    for k in range(4):                                                  # Stufen, schmaler als das Podest
        tiefe = 0.6 + 0.3 * (3 - k)
        netz.quader((TX - 0.6, TY - h - 0.6 - 0.15 - 0.3 * (3 - k), 0.15 * (k + 1)), (1.0, 0.3, 0.3 * (k + 1)),
                    st["Bruchstein dunkel"], ohne=("-z",))
    # Wehrplatte: Gesims, Zinnen, Feuerkorb
    bs.gesims(netz, st, TX - h, TX + h, TY - h, TY + h, TH - 0.4, hoehe=0.2, vor=0.12, stoff="Sandstein")
    # Wehrplatte etwas breiter als der Turm, darauf die Brüstung und die Zinnen (alles in eigenen Ebenen)
    netz.quader((TX, TY, TH + 0.06), (TB + 0.1, TB + 0.1, 0.12), st["Bruchstein dunkel"], ohne=("-z",))
    z0 = TH + 0.12
    for sx in (-1, 1):
        netz.quader((TX + sx * (h - 0.245), TY, z0 + 0.24), (0.45, TB - 0.04, 0.48), st["Bruchstein"], ohne=("-z",))
        for y in (-1.8, -0.6, 0.6, 1.8):
            netz.quader((TX + sx * (h - 0.245), TY + y, z0 + 0.48 + 0.35), (0.45, 0.6, 0.7), st["Bruchstein"], ohne=("-z",))
    for sy in (-1, 1):
        netz.quader((TX, TY + sy * (h - 0.245), z0 + 0.25), (TB - 0.98, 0.45, 0.5), st["Bruchstein"], ohne=("-z",))
        for x in (-0.9, 0.3) if sy < 0 else (-0.6, 0.6):
            netz.quader((TX + x, TY + sy * (h - 0.245), z0 + 0.5 + 0.35), (0.6, 0.45, 0.7), st["Bruchstein"], ohne=("-z",))
    lb.saeule(netz, st, (TX, TY, TH + 0.12), 0.08, 0, 0.9, "Eisen", n=6)          # Feuerkorb auf dem Pfahl
    for k in range(6):
        w = 2 * math.pi * k / 6
        netz.stab((TX + 0.12 * math.cos(w), TY + 0.12 * math.sin(w), TH + 1.0),
                  (TX + 0.42 * math.cos(w), TY + 0.42 * math.sin(w), TH + 1.6), 0.05, 0.05, st["Eisen"])
    lb.saeule(netz, st, (TX, TY, TH + 0.12), 0.38, 0.92, 1.02, "Eisen", n=8)
    return oeffnungen


def palisade(netz, st, a, b, abstand=0.42, erster=True):
    """Palisade aus spitzen Pfählen von a nach b, dazu ein Querriegel innen. `erster`: auch den Pfahl bei a setzen
    (an einer Ecke steht er schon vom vorigen Abschnitt)."""
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    n = round((b - a).length / abstand)
    for k in range(0 if erster else 1, n + 1):
        p = a.lerp(b, k / n)
        hoehe = 2.0 + 0.25 * ((k * 7) % 3)
        lb.saeule(netz, st, (p.x, p.y, 0), 0.17, 0, hoehe, "Holz alt", n=6)
        lb.kegel(netz, (p.x, p.y), 0.17, hoehe, hoehe + 0.35, st["Holz alt"], n=6, dreh=math.pi / 6)
    innen = (b - a).normalized().cross(Vector((0, 0, 1))) * -0.22
    netz.stab(a + innen + Vector((0, 0, 1.4)), b + innen + Vector((0, 0, 1.4)), 0.12, 0.1, st["Holz"], quer=(0, 0, 1))


def main():
    lb.ausgeben(NAME, FLAECHE, bauen(), ziel_hoehe=5.5, ausschnitt=26.0)


if __name__ == "__main__":
    main()
