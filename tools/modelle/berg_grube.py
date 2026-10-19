"""Felsberg hinter der Grube (Umland): ersetzt die zwei abgeflachten Kugeln, die das Spiel hinter grube.glb setzt.

    blender -b --python tools/modelle/berg_grube.py        oder
    python tools/modelle/berg_grube.py [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/berg_grube.glb und docs/modelle/berg_grube.png.

Inhalt: ein Knoten „Berg“ im Koordinatensystem von grube.glb (Ursprung in der Mitte der Grubenfläche, im Spiel +Z
zur Straße, der Berg liegt nach −Z). Grundfläche etwa x −24 … 19 und z −39 … −4, Hauptgipfel 11,5 m bei (2|−22),
Nebengipfel 8,5 m bei (−12|−16) und ein kleiner Sporn rechts. Der Hang ist ein Netz aus Facetten (Höhenraster 2,5 m
mit Rauschen), hell und dunkel nach der Lage zur Sonne wie die Felsen im Grubenmodell; flache Stellen tragen Gras.
Am Fuß liegt Geröll, an den Flanken stehen Tannen. Vorn läuft der Hang hinter dem Felshang der Grube aus
(Höhe 0 bis y = 3,5, Blender-Achsen), die Felsen des Grubenmodells stehen also davor.
Materialien: „Farben“ (alle festen Farben als Eckenfarbe, siehe bausteine.farben_zusammenlegen).
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import landbau as lb  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "berg_grube"
RASTER = 2.5
X0, X1, Y0, Y1 = -25.0, 20.0, 2.5, 40.0          # Blender: +Y ist im Spiel −Z (vom Weg weg)
# Gipfel (x, y, Halbmesser, Höhe) – Lage wie die Kugeln, die das Spiel bisher setzt
GIPFEL = ((2.0, 22.0, 17.0, 11.5), (-12.0, 16.0, 12.0, 8.5), (10.0, 15.0, 7.0, 5.0))
SONNE = Vector((-0.45, -0.55, 0.7)).normalized()


def hoehe_glatt(x, y):
    h = 0.0
    for gx, gy, r, hg in GIPFEL:
        t = math.hypot(x - gx, (y - gy) * 1.1) / r
        if t < 1:
            h = max(h, hg * (1 - t * t) ** 0.7)          # Kuppe, vorn steil wie die Felswand
    vorn = min(1.0, max(0.0, (y - 3.5) / 4.0))           # vorn auslaufen, hinter dem Felshang
    return h * vorn * vorn * (3 - 2 * vorn)


def raster():
    zufall = random.Random(17)
    nx, ny = int((X1 - X0) / RASTER) + 1, int((Y1 - Y0) / RASTER) + 1
    punkte = {}
    for i in range(nx):
        for j in range(ny):
            x, y = X0 + i * RASTER, Y0 + j * RASTER
            h = hoehe_glatt(x, y)
            if h > 0.05:                                  # Rauschen nur am Hang, der Rand bleibt am Boden
                x += zufall.uniform(-0.6, 0.6)
                y += zufall.uniform(-0.6, 0.6)
                h = max(0.0, h + zufall.uniform(-0.7, 0.7) * min(1.0, h / 2))
            punkte[i, j] = Vector((x, y, h if h > 0.05 else 0.0))
    return punkte, nx, ny


def tanne_auf(netz, st, x, y, z, hoehe, r, saat):
    """Wie landbau.tanne, aber auf der Höhe z des Hangs."""
    zufall = random.Random(saat)
    fuss = max(0.0, z - 0.3)                          # ein Stück in den Hang, nie unter den Boden
    lb.saeule(netz, st, (x, y, fuss), 0.18, 0.0, z + hoehe * 0.22 - fuss, "Rinde", n=6)
    for i, (f_r, f_z0, f_z1) in enumerate(((1.0, 0.16, 0.62), (0.78, 0.38, 0.84), (0.52, 0.6, 1.0))):
        lb.kegel(netz, (x, y), r * f_r * zufall.uniform(0.92, 1.08), z + hoehe * f_z0, z + hoehe * f_z1,
                 st["Tanne"], st["Tanne hell"], n=7, dreh=zufall.uniform(0, 1) + i * 0.4)


def bauen(st):
    berg = bs.Netz("Berg")
    punkte, nx, ny = raster()
    zufall = random.Random(5)
    for i in range(nx - 1):
        for j in range(ny - 1):
            a, b, c, d = punkte[i, j], punkte[i + 1, j], punkte[i + 1, j + 1], punkte[i, j + 1]
            dreiecke = ((a, b, c), (a, c, d)) if (i + j) % 2 else ((a, b, d), (b, c, d))
            for p, q, r in dreiecke:
                if max(p.z, q.z, r.z) < 0.05:
                    continue
                n = (q - p).cross(r - p).normalized()
                if n.z < 0:
                    n, q, r = -n, r, q
                mitte_z = (p.z + q.z + r.z) / 3
                if n.z > 0.86 and mitte_z < 9 and zufall.random() < 0.7:
                    stoff = "Gras"                        # flache Stellen bewachsen
                else:
                    stoff = "Fels" if n.dot(SONNE) > 0.62 else "Fels dunkel"
                berg.vieleck_nach([p, q, r], n, st[stoff])

    # Geröll am Fuß und Tannen an den Flanken (Lage aus dem glatten Hang, ein Stück eingesenkt)
    zufall = random.Random(23)
    geroell = 0
    while geroell < 14:
        x, y = zufall.uniform(-22, 17), zufall.uniform(5, 36)
        h = hoehe_glatt(x, y)
        if 0.3 < h < 3.0:
            lb.felsbrocken(berg, st, (x, y, h - 0.25), zufall.uniform(0.5, 1.2), zufall.uniform(0.4, 0.9), saat=100 + geroell)
            geroell += 1
    tannen = [(-20.0, 20.0), (-17.0, 26.0), (-6.5, 27.0), (12.0, 22.0), (15.0, 18.0), (-3.0, 32.0), (9.0, 30.0)]
    for k, (x, y) in enumerate(tannen):
        h = hoehe_glatt(x, y)
        tanne_auf(berg, st, x, y, h - 0.2, 5.5 + (k % 3) * 1.2, 1.5, saat=200 + k)
    return berg.objekt()


def main():
    import grube
    objekte_grube = grube.bauen()                       # nur fürs Vorschaubild: der Anschluss an den Felshang
    st = bs.Stoffe()
    berg = bauen(st)
    berg.data.calc_loop_triangles()
    print(f"{NAME}: {len(berg.data.loop_triangles)} Dreiecke")
    vorher, nachher = bs.farben_zusammenlegen([berg])
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren(NAME, [berg]))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    bs.vorschau_buehne((22.0, 18.0), ziel_hoehe=4.0, ausschnitt=74.0, schnell=schnell)
    sichtbar = {berg} | {o for o in objekte_grube if o.name != "Leerstand"}
    print(bs.rendern(NAME, lambda o: o in sichtbar))


if __name__ == "__main__":
    main()
