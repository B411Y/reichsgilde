"""Kachelbare Felsstruktur für das Fels-Material (Felspinsel F2) – prozedural, 512 × 512 Pixel, eine Kachel deckt 2,5 m × 2,5 m.

    <Python mit numpy> tools/oberflaeche/felstexturen.py     (z. B. das Python von Blender; nutzt die Bausteine aus texturen.py)

Schreibt game/assets/texturen/fels/fels_struktur.png. Kanäle je Pixel (alle linear, keine Farbe – die Farbe kommt aus
game/daten/gestein.json im Shader fels.gdshaderinc):
    R, G  Neigung der Fläche in U und V (Normale, 128 = flach)
    B     Höhe (0 tief … 1 hoch): Bruchflächen, Risse und Körnung; der Shader nimmt sie für Höhlungen (Moos) und Kanten (Flechten)
    A     Flecken (0 … 1): grobes und feines Rauschen gemischt, aus dem der Shader Moospolster und Flechtenflecken schneidet
Platzhalter bis F1: Dort wird die Struktur aus hochaufgelösten Felsformen in Blender gebacken und ersetzt diese Datei (gleiche Kanäle).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import texturen as t  # noqa: E402  (Bausteine: rauschen, fbm, weich, zellen, png)

N = t.N


def struktur():
    # Bruchflächen: Voronoi-Zellen, jede mit eigener Neigung (kantiger Bruch wie Grauwacke und Kalk)
    nummer, f1, f2, wx, wy = t.zellen(6, 6, 401, 0.95)
    rng = np.random.default_rng(402)
    nx, ny = (rng.random(36) * 2 - 1)[nummer], (rng.random(36) * 2 - 1)[nummer]
    facette = 0.10 * (nx * wx + ny * wy) + 6.0 * t.zufall_je(nummer, 403, 36)
    # kleinere Splitter in den großen Flächen
    nummer2, g1, g2, vx, vy = t.zellen(17, 17, 404, 0.9)
    rng2 = np.random.default_rng(405)
    mx, my = (rng2.random(289) * 2 - 1)[nummer2], (rng2.random(289) * 2 - 1)[nummer2]
    splitter = 0.06 * (mx * vx + my * vy)
    # Risse an den Zellgrenzen, nur stückweise offen
    riss = np.exp(-(((f2 - f1) / 2) / 1.4) ** 2) * np.clip((t.rauschen(40, 406) - 0.35) * 4, 0, 1)
    riss_fein = np.exp(-(((g2 - g1) / 2) / 0.9) ** 2) * np.clip((t.rauschen(18, 407) - 0.5) * 4, 0, 1)
    koernung = t.fbm(408, [24, 8, 3, 1.2])
    hoehe = t.weich(facette, 2.0) + 0.5 * t.weich(splitter, 1.0) + 7.0 * koernung - 9.0 * riss - 3.0 * riss_fein
    # Neigung aus der Höhe (Pixel): wie texturen.schicht, nur etwas flacher, damit die Kanten nicht zu hart wirken
    gu = (np.roll(hoehe, -1, axis=1) - np.roll(hoehe, 1, axis=1)) / 2 * 0.8
    gv = (np.roll(hoehe, -1, axis=0) - np.roll(hoehe, 1, axis=0)) / 2 * 0.8
    laenge = np.sqrt(1 + gu ** 2 + gv ** 2)
    hoehe01 = (hoehe - np.percentile(hoehe, 1)) / (np.percentile(hoehe, 99) - np.percentile(hoehe, 1))
    flecken = 0.65 * t.rauschen(70, 409) + 0.35 * t.fbm(410, [14, 5, 2])
    flecken = (flecken - flecken.min()) / (flecken.max() - flecken.min())
    return np.dstack([t.kodieren(-gu / laenge), t.kodieren(-gv / laenge),
                      np.clip(np.round(hoehe01 * 255), 0, 255).astype(np.uint8),
                      np.clip(np.round(flecken * 255), 0, 255).astype(np.uint8)])


def main():
    ziel = os.path.join(t.ZIEL, "fels")
    os.makedirs(ziel, exist_ok=True)
    pfad = os.path.join(ziel, "fels_struktur.png")
    t.png(pfad, struktur())
    print("geschrieben:", os.path.normpath(pfad))


if __name__ == "__main__":
    main()
