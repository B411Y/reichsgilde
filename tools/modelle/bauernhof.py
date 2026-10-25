"""Bauernhof des Wirts (Umland): Gerste und Roggen. Fläche 26 × 20 m.

    blender -b --python tools/modelle/bauernhof.py        oder
    python tools/modelle/bauernhof.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/umland/bauernhof.glb und Bilder nach docs/modelle/.

Objekte im GLB (siehe landbau.py):
    Haus        Scheune in Fachwerk mit Ziegeldach und Tor, Bockwindmühle (Bock, Mühlenhaus, Flügelgitter, Steert),
                Acker in Streifen, Zaun, Misthaufen
    Arbeit      Gerste und Roggen auf dem Acker (im Wechsel, wie bisher im Spiel), Garben auf dem abgeernteten
                Streifen, Segeltuch auf den Flügeln, Zugpferd, Säcke
    Fahne       auf der Scheune
    Leerstand   Bretter vor Tor und Fenstern, Unkraut auf dem Acker
    Anker_Tuer  vor dem Scheunentor, Anker_Karren auf dem Hof

Aufbau (Blender: Z oben, Weg bei -Y; Fläche x ±13, y ±10): links vorn die Scheune mit Hof, rechts und hinten
die Ackerstreifen, hinten rechts die Mühle.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

NAME = "bauernhof"
FLAECHE = (26.0, 20.0)
STREIFEN = [(-1.4 + 2.3 * k) for k in range(6)]      # x-Mitte der Ackerstreifen
ACKER_Y = (-8.6, 2.4)
MUEHLE = Vector((-7.4, 4.8, 0))


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    haus, arbeit, fahne, leer = bs.Netz("Haus"), bs.Netz("Arbeit"), bs.Netz("Fahne"), bs.Netz("Leerstand")

    lb.flaeche(haus, st, lb.rechteck(-13, -10, 13, 10), 0.008, "Gras")
    lb.flaeche(haus, st, [(-13, -10), (-2.8, -10), (-2.8, -0.6), (-13, -0.6)], 0.014, "Erde hell")       # Hof
    for k, x in enumerate(STREIFEN):
        lb.flaeche(haus, st, lb.rechteck(x - 1.05, ACKER_Y[0], x + 1.05, ACKER_Y[1]), 0.02, "Erde")
        for j in range(4):                                                    # Furchen
            fx = x - 0.75 + j * 0.5
            haus.quader((fx, sum(ACKER_Y) / 2, 0.07), (0.16, ACKER_Y[1] - ACKER_Y[0] - 0.2, 0.1), st["Erde"], ohne=("-z",))
    bs.zaun(haus, st, [(-2.4, -9.6), (12.6, -9.6), (12.6, 3.2), (-2.4, 3.2)], hoehe=0.9, abstand=2.2)
    lb.haufen(haus, st, (-3.8, -2.2), 1.2, 0.7, "Erde", "Heu", saat=91)                # Misthaufen

    # ── Scheune ───────────────────────────────────────────────────────────────
    sx0, sx1, sy0, sy1 = -12.0, -4.6, -6.4, -1.6
    h = lb.huette(haus, st, sx0, sx1, sy0, sy1, 3.2, wand="fachwerk", deckung=bs.ZIEGEL, neigung=46,
                  tuer_u=None, fenster_vorn=(), fenster_seite=(("rechts", 0.0),), giebel_fenster=True)
    M = bs.wandmatrix(((sx0 + sx1) / 2, sy0, 0.25), "vorne")
    tor = Oeffnung("tuer", 0.6, 0.0, 2.6, 2.6)
    M_tor = M @ Matrix.Translation((0, -0.06, 0))
    haus.quader(M @ Vector((tor.u, -0.035, tor.h / 2 + 0.06)), (tor.b + 0.3, 0.07, tor.h + 0.12), st["Fachwerk"])
    bs.tor_fluegel(haus, st, M_tor, tor)
    h["oeffnungen"].append((M_tor, tor))
    bs.fahne(fahne, st, (sx0 + 0.6, (sy0 + sy1) / 2, h["dach"]["z_first"] - 0.2), hoehe=2.4, richtung=1)

    # ── Bockwindmühle ─────────────────────────────────────────────────────────
    muehle(haus, arbeit, leer, st, MUEHLE)

    # ── Arbeit: Korn, Garben, Pferd, Säcke ────────────────────────────────────
    for k, x in enumerate(STREIFEN):
        if k == len(STREIFEN) - 1:                                            # abgeerntet: Stoppeln und Garben
            lb.flaeche(arbeit, st, lb.rechteck(x - 1.0, ACKER_Y[0] + 0.1, x + 1.0, ACKER_Y[1] - 0.1), 0.15, "Stoppel")
            for j in range(4):
                lb.kegel(arbeit, (x + 0.4 * (j % 2) - 0.2, ACKER_Y[0] + 1.4 + j * 2.6), 0.42, 0.15, 1.25,
                         st["Korn"], st["Heu"], n=6)
            continue
        korn = "Korn" if k % 2 == 0 else "Roggen"
        dreh = Matrix.Rotation(math.radians(90), 4, "Z")                      # Prisma-x → entlang y (die Furchen)
        for j in range(4):                                                    # Halme in Reihen über den Furchen
            fx = x - 0.75 + j * 0.5
            hoch = 0.62 + 0.05 * ((j + k) % 3)
            arbeit.prisma([(-0.26, 0.1), (0.26, 0.1), (0.16, hoch), (-0.16, hoch)],
                          ACKER_Y[0] + 0.15 + 0.05 * j, ACKER_Y[1] - 0.15 - 0.04 * j, st[korn],
                          matrix=Matrix.Translation((fx, 0, 0)) @ dreh)
    bs.pferd(arbeit, st, Vector((-3.6, -5.6, 0)), 104)
    for k in range(3):
        lb.saeule(arbeit, st, (-4.1 + 0.55 * k, -0.9, 0), 0.24, 0, 0.62, "Lumpen", n=7)

    # ── Leerstand ─────────────────────────────────────────────────────────────
    lb.vernageln(leer, st, h["oeffnungen"])
    pkt = [(x + d, ACKER_Y[0] + 1.0 + 2.1 * j) for k, x in enumerate(STREIFEN) for j, d in ((0, -0.4), (2, 0.3), (4, -0.1))]
    lb.unkraut(leer, st, pkt + [(-6.0, -8.0), (-10.0, -8.5), (-3.4, -4.0)], groesse=0.7, saat=9)

    anker = [bs.anker("Anker_Tuer", tuple(M @ Vector((tor.u, -0.5, -0.25)))),
             bs.anker("Anker_Karren", (-7.6, -8.6, 0.0))]
    return lb.knoten(haus, arbeit, fahne, leer, anker)


def muehle(haus, arbeit, leer, st, fuss):
    """Bockwindmühle: Kreuzbock mit Hausbaum, Mühlenhaus auf dem Bock, vier Flügel nach vorn (-Y), Steert nach hinten."""
    holz = st["Holz"]
    x, y = fuss.x, fuss.y
    for d in ((1, 0), (0, 1)):                                                # Kreuzschwellen auf Steinsockeln
        z = 0.45 + 0.03 * d[1]                       # die zweite Schwelle liegt etwas höher (sonst flimmert die Kreuzung)
        haus.stab((x - 2.0 * d[0], y - 2.0 * d[1], z), (x + 2.0 * d[0], y + 2.0 * d[1], z), 0.3, 0.3, holz, quer=(0, 0, 1))
        for s in (-1, 1):
            haus.quader((x + s * 1.85 * d[0], y + s * 1.85 * d[1], 0.15), (0.6, 0.6, 0.3), st["Bruchstein"], ohne=("-z",))
    for dx, dy in ((1.7, 0), (-1.7, 0), (0, 1.66), (0, -1.66)):                # Streben zum Hausbaum
        haus.stab((x + dx, y + dy, 0.6), (x + dx * 0.15, y + dy * 0.15, 2.6), 0.18, 0.18, holz)
    lb.saeule(haus, st, (x, y, 0), 0.26, 0.6, 3.4, "Holz", n=8)
    # Mühlenhaus
    hx, hy, z0, hb, ht, hh = x, y, 3.4, 3.0, 3.6, 3.4
    haus.quader((hx, hy, z0 + hh / 2), (hb, ht, hh), st["Holz alt"])
    M = bs.wandmatrix((hx, hy - ht / 2, z0), "vorne")
    luke = Oeffnung("fenster", 0.9, 1.4, 0.5, 0.6, laeden=False)
    bs.fenster(haus, M, luke, st, kreuz=None)
    dach = bs.satteldach(haus, st, hx - hb / 2, hx + hb / 2, hy - ht / 2, hy + ht / 2, z0 + hh, 40, traufe=0.25,
                         ortgang=0.2, enden="Holz", matrix=None, **bs.SCHINDEL)
    hoch = ht / 2 * dach["tan"]
    for s in (-1, 1):
        M = bs.wandmatrix((hx + s * hb / 2, hy, z0 + hh), "rechts" if s > 0 else "links")
        bs.platte(haus, M, [(-ht / 2, 0), (ht / 2, 0), (0, hoch)], 0, 0.3, st["Holz alt"])
    # Treppe und Steert nach hinten
    haus.stab((hx - 0.4, hy + ht / 2 + 2.3, 0.3), (hx - 0.4, hy + ht / 2, z0 + 0.1), 0.6, 0.12, holz, quer=(1, 0, 0))
    haus.stab((hx + 0.4, hy + ht / 2 + 0.2, z0 + 0.3), (hx + 0.9, hy + ht / 2 + 2.6, 0.5), 0.16, 0.16, holz)
    # Welle und Flügel nach vorn: Ruten mit Gitter (Haus), Segeltuch darauf (Arbeit)
    nabe = Vector((hx, hy - ht / 2 - 0.45, z0 + hh * 0.72))
    haus.stab(nabe + Vector((0, 0.5, 0)), nabe - Vector((0, 0.15, 0)), 0.32, 0.32, holz, quer=(1, 0, 0))
    # Jeder Flügel liegt in einer eigenen Ebene (vor der Nabe gestaffelt), sonst flimmern die Leisten an der Nabe.
    for k in range(2):                                                 # zwei durchgehende Ruten über Kreuz
        w = math.radians(25 + 90 * k)
        r = Vector((math.cos(w), 0, math.sin(w)))
        vor = Vector((0, -0.14 * k, 0))
        haus.stab(nabe + vor - r * 5.6, nabe + vor + r * 5.6, 0.16, 0.12, holz, quer=(0, 1, 0))
    for k in range(4):
        w = math.radians(25 + 90 * k)
        r = Vector((math.cos(w), 0, math.sin(w)))
        q = Vector((-math.sin(w), 0, math.cos(w)))                     # quer zur Rute, in der Flügelebene
        vor = Vector((0, -0.26 - 0.015 * k, 0))
        for f in (0.0, 1.0):                                           # Gitter: zwei Längsleisten, Sprossen
            a, b = nabe + r * 1.2 + q * (0.15 + 0.7 * f), nabe + r * 5.4 + q * (0.15 + 0.7 * f)
            haus.stab(a + vor + Vector((0, -0.006 * f, 0)), b + vor + Vector((0, -0.006 * f, 0)), 0.05, 0.04, holz, quer=(0, 1, 0))
        for j in range(6):
            p = nabe + r * (1.3 + j * 0.8) + vor + Vector((0, -0.045 - 0.003 * j, 0))
            haus.stab(p + q * 0.1, p + q * 0.9, 0.04, 0.03, holz, quer=(0, 1, 0))
        ecken = [nabe + r * 1.25 + q * 0.12, nabe + r * 5.35 + q * 0.12, nabe + r * 5.35 + q * 0.88, nabe + r * 1.25 + q * 0.88]
        arbeit.vieleck_nach([e + vor + Vector((0, -0.09, 0)) for e in ecken], (0, -1, 0), st["Segeltuch"])
        arbeit.vieleck_nach([e + vor + Vector((0, -0.075, 0)) for e in ecken], (0, 1, 0), st["Segeltuch"])


def main():
    lb.ausgeben(NAME, FLAECHE, bauen(), ziel_hoehe=3.0, ausschnitt=33.0)


if __name__ == "__main__":
    main()
