#!/usr/bin/env python3
"""Hilfe-Mauszeiger (Dreieck mit Fragezeichen, Messing) aus der Vorlage des Nutzers.

Aufruf (numpy und Pillow, kein Blender nötig):

    python tools/oberflaeche/zeiger_hilfe.py VORLAGE.jpg AUSGABEORDNER

Schritte: dunklen Grund freistellen (Messing ist hell und rotstichig, der Grund fast schwarz), mit Flächenmittel
(Pillow BOX nach LANCZOS-Vorstufe) auf die Höhe 48/64/80 verkleinern, Gravur geht durch das Flächenmittel beim
Verkleinern weich verloren, Alpha hart (0 oder 255), 1 Pixel dunkle Kontur innen am Rand. Kein hellerer Außenrand, kein Schein.
Ausgabe: hilfe_<px>.png (Bild so groß wie die Zeigerfläche, quadratisch aufgefüllt ist unnötig) und hotspots.txt
(Spitze des Dreiecks = deckendes Pixel mit kleinstem x+y).

Herkunft der Vorlage: vom Nutzer mit Gemini erstellt (09.11.2026). Die Vorlage liegt nicht im Repository.
"""
import os
import sys

import numpy as np
from PIL import Image

GROESSEN = (48, 64, 80)
KONTUR = np.array([38, 26, 12], dtype=np.float32)


def freistellen(bild):
    a = np.asarray(bild.convert("RGB"), dtype=np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    hell = np.maximum(r, g)
    warm = r - b
    # weiche Maske: 0 im Grund, 1 im Messing
    m = np.clip((hell - 28) / 14.0, 0, 1) * np.clip((warm - 6) / 8.0, 0, 1)
    return m


def zuschneiden(bild, m):
    ys, xs = np.where(m > 0.5)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    return bild.crop((x0, y0, x1, y1)), m[y0:y1, x0:x1]


def erzeugen(bild, m, hoehe):
    w, h = bild.size
    breite = max(1, round(w * hoehe / h))
    # Farbe nur aus Messing-Pixeln mitteln (Grund nicht einmischen): vormultipliziert
    rgb = np.asarray(bild.convert("RGB"), dtype=np.float32) * m[..., None]
    def klein(kanal):
        im = Image.fromarray(kanal.astype(np.float32), mode="F")
        return np.asarray(im.resize((breite, hoehe), Image.BOX), dtype=np.float32)
    alpha = klein(m)
    farbe = np.stack([klein(rgb[..., i]) for i in range(3)], axis=-1) / np.maximum(alpha[..., None], 1e-3)
    harte = alpha > 0.5
    # Kontur: deckende Randpixel (mit leerem 4er-Nachbarn) dunkel
    pad = np.pad(harte, 1, constant_values=False)
    rand = harte & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    # leichter Kontrast und Schärfe der Fläche
    mitte = farbe.mean()
    farbe = np.clip((farbe - mitte) * 1.25 + mitte * 1.05, 0, 255)
    farbe[rand] = KONTUR
    out = np.zeros((hoehe, breite, 4), dtype=np.uint8)
    out[..., :3] = np.where(harte[..., None], farbe, 0).astype(np.uint8)
    out[..., 3] = np.where(harte, 255, 0)
    return Image.fromarray(out, "RGBA")


def main():
    vorlage, ziel = sys.argv[1], sys.argv[2]
    os.makedirs(ziel, exist_ok=True)
    bild = Image.open(vorlage)
    m = freistellen(bild)
    bild, m = zuschneiden(bild, m)
    punkte = []
    for g in GROESSEN:
        ausgabe = erzeugen(bild, m, g)
        ausgabe.save(os.path.join(ziel, f"hilfe_{g}.png"))
        al = np.asarray(ausgabe)[..., 3] > 0
        ys, xs = np.where(al)
        i = np.argmin(xs + ys)
        punkte.append((int(xs[i]), int(ys[i])))
    with open(os.path.join(ziel, "hotspots.txt"), "w") as f:
        for g, p in zip(GROESSEN, punkte):
            f.write(f"{g} {p[0]} {p[1]}\n")
    print(punkte)


if __name__ == "__main__":
    main()
