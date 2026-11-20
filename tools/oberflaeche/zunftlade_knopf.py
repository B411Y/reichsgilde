#!/usr/bin/env python3
"""Baut die Knopfflächen der Fenster (UI-Umbau „Zunftlade“, Teil 2) aus dem Holz der Fächer, das schon im Repository liegt:

  fenster/knopf.webp     Holz der Fächer (auf 300 px verkleinert, kachelt) mit schlichter Kante: außen 1 px dunkel, innen 2 px
                 (oben hell, links mittel, rechts und unten dunkel) – die Farben der Knopfkante des Entwurfs.
  fenster/knopf_an.webp  dasselbe, golden eingefärbt, Kante hell: der gewählte Knopf (Reiter, Schalter).

Nur Verkleinern, Einfärben und eine Linie am Rand; keine Verläufe, keine Schatten (Nutzer, 18.11.2026).
Im Thema (game/scenes/fenster_thema.tres) sind es Stilkästen mit 3 px Rand, die Mitte wird gekachelt.

Aufruf (Python 3 mit numpy und Pillow):
  python3 -I tools/oberflaeche/zunftlade_knopf.py game/assets/oberflaeche/zunftlade
"""
import sys
import numpy as np
from PIL import Image

ZIEL = sys.argv[1]
N = 300


def kante(a, aussen, oben, rechts, unten, links):
    a[0, :], a[-1, :], a[:, 0], a[:, -1] = aussen, aussen, aussen, aussen
    a[1:3, 1:-1] = oben
    a[-3:-1, 1:-1] = unten
    a[1:-1, 1:3] = links
    a[1:-1, -3:-1] = rechts
    return a


def farbe(hexwert):
    return np.array([int(hexwert[i:i + 2], 16) for i in (1, 3, 5)], dtype=np.float32) / 255.0


holz = np.asarray(Image.open(ZIEL + '/fachholz.webp').convert('RGB').resize((N, N), Image.LANCZOS), dtype=np.float32) / 255.0

normal = kante(holz.copy(), farbe('#0c0805'), farbe('#8a6a3c'), farbe('#2a1a0c'), farbe('#120b05'), farbe('#5a3f22'))
# golden: das Holz zur Hälfte mit dem Goldbraun des Entwurfs gemischt (eine Farbe, kein Verlauf)
gold = holz * 0.48 + farbe('#bc8e46') * 0.52
an = kante(gold, farbe('#0c0805'), farbe('#fff3c0'), farbe('#ffe08a'), farbe('#c9a455'), farbe('#fff3c0'))

for bild, name in ((normal, 'knopf.webp'), (an, 'knopf_an.webp')):
    Image.fromarray((np.clip(bild, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB').save(ZIEL + '/fenster/' + name, lossless=True, method=6)
print('fertig')
