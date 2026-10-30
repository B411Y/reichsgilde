"""Ausschnitt eines Bilds mit Koordinatenraster (und gesetzten Merkpunkten) – zum Setzen der Merkpunkte von Hand.

Charakter Phase 6 (S0/S1): Die Merkpunkte der Fotovorlagen (vorlagen_merkpunkte.json) werden von Hand gesetzt. Das
Raster zeigt die Koordinaten des Originalbilds: feine Linien alle 10, kräftige alle 50 Bildpunkte, Zahlen am Rand.

    $BPY -I tools/charakter/messen/raster.py <bild> <x0> <y0> <x1> <y1> <aus.png> [skala] [--punkte=<json>,<vorlage>,<ansicht>]
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from messen import bild as B  # noqa: E402

# 3 × 5 Ziffern
_Z = {
    "0": ["111", "101", "101", "101", "111"], "1": ["010", "110", "010", "010", "111"], "2": ["111", "001", "111", "100", "111"],
    "3": ["111", "001", "111", "001", "111"], "4": ["101", "101", "111", "001", "001"], "5": ["111", "100", "111", "001", "111"],
    "6": ["111", "100", "111", "101", "111"], "7": ["111", "001", "010", "010", "010"], "8": ["111", "101", "111", "101", "111"],
    "9": ["111", "101", "111", "001", "111"], "-": ["000", "000", "111", "000", "000"],
}


def zahl(a, x, y, text, farbe, g=2):
    for i, ch in enumerate(text):
        m = _Z.get(ch)
        if m is None:
            continue
        for r, zeile in enumerate(m):
            for c, bit in enumerate(zeile):
                if bit == "1":
                    yy, xx = y + r * g, x + (i * 4 + c) * g
                    a[max(0, yy):yy + g, max(0, xx):xx + g] = farbe


def raster(bild, x0, y0, x1, y1, skala=1.5, punkte=None):
    img = B.lesen(bild)[y0:y1, x0:x1]
    img = B.skalieren(img, skala) if skala != 1 else img.copy()
    h, w = img.shape[:2]
    rand = 22
    aus = np.full((h + rand, w + rand + 8, 3), 0.1, np.float32)
    aus[rand:, rand:rand + w] = img
    for v in range((x0 // 10 + 1) * 10, x1, 10):
        px = rand + int(round((v - x0) * skala))
        if px >= aus.shape[1]:
            continue
        stark = v % 50 == 0
        aus[rand:, px] = aus[rand:, px] * (0.35 if stark else 0.75) + (np.array([1, 0.9, 0]) if stark else np.array([0.6, 0.6, 0.6])) * (0.65 if stark else 0.25)
        if stark:
            zahl(aus, px - 6, 3, str(v), (1, 0.9, 0))
    for v in range((y0 // 10 + 1) * 10, y1, 10):
        py = rand + int(round((v - y0) * skala))
        if py >= aus.shape[0]:
            continue
        stark = v % 50 == 0
        aus[py, rand:] = aus[py, rand:] * (0.35 if stark else 0.75) + (np.array([0, 0.9, 1]) if stark else np.array([0.6, 0.6, 0.6])) * (0.65 if stark else 0.25)
        if stark:
            zahl(aus, 1, py - 5, str(v)[-3:], (0, 0.9, 1))
    for name, (x, y) in (punkte or {}).items():
        px, py = rand + int(round((x - x0) * skala)), rand + int(round((y - y0) * skala))
        for d in range(-5, 6):
            for q in ((py, px + d), (py + d, px)):
                if 0 <= q[0] < aus.shape[0] and 0 <= q[1] < aus.shape[1]:
                    aus[q] = (1, 0.1, 0.6)
    return aus


if __name__ == "__main__":
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    opt = dict(x[2:].split("=", 1) for x in sys.argv[1:] if x.startswith("--") and "=" in x)
    pkte = None
    if "punkte" in opt:
        datei, vorlage, ansicht = opt["punkte"].split(",")
        pkte = json.load(open(datei, encoding="utf-8"))["vorlagen"][vorlage][ansicht]["punkte"]
    bild, x0, y0, x1, y1, ziel = a[0], *map(int, a[1:5]), a[5]
    B.schreiben(ziel, raster(bild, x0, y0, x1, y1, float(a[6]) if len(a) > 6 else 1.5, pkte))
    print(ziel)
