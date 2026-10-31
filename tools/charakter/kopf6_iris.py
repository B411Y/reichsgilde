"""Sechs Irismuster für den Charakter-Editor (Phase 6, S7; eigener Code, MIT; numpy und OpenImageIO, Blenders Python).

Die sechs Farbbilder von RoboPoets (CC0) sind dieselbe Struktur in sechs Farben (Korrelation der Helligkeit 0,98–1,0) –
als „sechs Muster“ taugen sie nicht. Grundlage ist deshalb die Fasermaske „iris_color_mask“ (R Fasern, G innerer Kranz)
und „iris_depth“ (B Tiefe) wie in Phase 3; daraus entstehen sechs Varianten mit festen Startwerten:
  0 Grundmuster   1 Krypten (dunkle Lücken im mittleren Ring)   2 Furchen (Kontraktionsringe außen)
  3 Strahlen (betonte Radialfasern)   4 Flecken (kleine Pigmentflecken)   5 breiter Kranz
Ausgabe: iris6_<0..5>.png (512², RGB, linear wie iris_muster.png). Die Darstellung legt sie als Texture2DArray an; die
Wahl folgt der Augenfarbe und dem Personen-Samen, die Drehung ist je Person verschieden.

Aufruf: $BPY -I tools/charakter/kopf6_iris.py <ausgabeordner>
"""
import os
import sys

import numpy as np
import OpenImageIO as oiio

ROBO = os.path.expanduser("~/werkzeuge/charakter/robopoets/src/textures")
G = 512


def lesen(name):
    b = oiio.ImageBuf(os.path.join(ROBO, name))
    return b.get_pixels(oiio.FLOAT)


def schreiben(pfad, feld):
    buf = oiio.ImageBuf(oiio.ImageSpec(feld.shape[1], feld.shape[0], 3, oiio.FLOAT))
    buf.set_pixels(oiio.ROI(0, feld.shape[1], 0, feld.shape[0], 0, 1, 0, 3), np.ascontiguousarray(feld.astype(np.float32)))
    buf.write(pfad, "uint8")


def verkleinern(a):
    h = a.shape[0] // G
    return a.reshape(G, h, G, h, -1).mean(axis=(1, 3))


def main():
    aus = sys.argv[1]
    os.makedirs(aus, exist_ok=True)
    maske = lesen("iris_color_mask.webp")
    tiefe = lesen("iris_depth.webp")
    grund = np.zeros(maske.shape[:2] + (3,), np.float32)
    grund[..., 0], grund[..., 1] = maske[..., 0], maske[..., 1]
    grund[..., 2] = tiefe[..., 0]
    grund = verkleinern(grund)
    y, x = np.mgrid[0:G, 0:G]
    u, v = (x + 0.5) / G * 2 - 1, (y + 0.5) / G * 2 - 1
    r = np.sqrt(u * u + v * v)
    th = np.arctan2(v, u)
    for k in range(6):
        rng = np.random.default_rng(6600 + k)
        a = grund.copy()
        if k == 1:      # Krypten: radial gestreckte dunkle Lücken bei r 0,5–0,85
            for _ in range(18):
                rc, tc = rng.uniform(0.5, 0.85), rng.uniform(-np.pi, np.pi)
                dr, dt = rng.uniform(0.06, 0.12), rng.uniform(0.10, 0.20)
                d = ((r - rc) / dr) ** 2 + (np.angle(np.exp(1j * (th - tc))) / dt) ** 2
                w = np.exp(-2.0 * d)
                a[..., 0] *= 1 - 0.85 * w
                a[..., 2] = np.clip(a[..., 2] + 0.25 * w, 0, 1)
        elif k == 2:    # Furchen: drei unterbrochene Ringe außen
            for rc in (0.74, 0.83, 0.91):
                luecke = 0.5 + 0.5 * np.cos(rng.integers(2, 5) * th + rng.uniform(0, 6.28))
                w = np.exp(-((r - rc) / 0.012) ** 2) * np.clip(luecke * 1.6 - 0.2, 0, 1)
                a[..., 0] *= 1 - 0.55 * w
        elif k == 3:    # Strahlen: Radialfasern betont
            phase = rng.uniform(0, 6.28, 3)
            s = (np.cos(57 * th + phase[0] + 3 * r) + 0.6 * np.cos(91 * th + phase[1]) + 0.4 * np.cos(143 * th + phase[2])) / 2.0
            a[..., 0] = np.clip(a[..., 0] * (1 + 0.45 * s * np.clip((r - 0.3) / 0.2, 0, 1)), 0, 1)
        elif k == 4:    # Flecken: kleine Pigmentflecken
            for _ in range(rng.integers(4, 8)):
                rc, tc = rng.uniform(0.45, 0.88), rng.uniform(-np.pi, np.pi)
                px, py = rc * np.cos(tc), rc * np.sin(tc)
                w = np.exp(-((u - px) ** 2 + (v - py) ** 2) / (2 * rng.uniform(0.025, 0.045) ** 2))
                a[..., 0] *= 1 - 0.92 * w
        elif k == 5:    # breiter Kranz: G radial nach außen gestreckt und kräftiger
            s = np.clip(r / 1.35, 0, 1)
            ix = np.clip(((s * np.cos(th) + 1) / 2 * G).astype(int), 0, G - 1)
            iy = np.clip(((s * np.sin(th) + 1) / 2 * G).astype(int), 0, G - 1)
            a[..., 1] = np.clip(grund[iy, ix, 1] * 1.3, 0, 1)
        schreiben(os.path.join(aus, f"iris6_{k}.png"), a)
    print("FERTIG Iris", aus)


if __name__ == "__main__":
    main()
