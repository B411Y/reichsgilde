#!/usr/bin/env python3
"""Erzeugt die eigenen Wasser-Bilder für den Wasser-Shader (nahtlos kachelbar, mit festem Startwert).

Aufruf (Blender bringt numpy mit, sonst genügt jedes Python mit numpy):
    ~/werkzeuge/blender/blender.sh -b --python tools/wasser/wasser_texturen.py -- [Zielordner]
Ohne Zielordner: game/assets/texturen/realistisches_wasser/. Kein Blender-Modul nötig, nur numpy, zlib, struct.

Dateien und Belegung (wie der Shader sie liest; alle Werte als rohe Bytes, ohne sRGB-Umrechnung):
  wasser_normal_a.png   512x512   RGB  Normalkarte, weiche breite Dünung (Shader: 75 %)
  wasser_normal_b.png   1024x1024 RGB  Normalkarte, feines Gekräusel auf Dünung (Shader: 25 %)
  wasser_stroemung.png  512x512   RGB  R,G = Verschiebung um 0,5 (verwirbelt die Normalkarten), B = 0
  wasser_schaum.png     256x256   RGB  Grau (Shader liest R): Schlieren und Flocken, hell = Schaum
  wasser_kaustik.png    1024x1024 RGB  Grau (Shader liest R): 4x4-Atlas aus 16 Bildern zu je 256 px, nahtlos in Zeit und Raum

Verfahren: Rauschen im Frequenzraum (FFT) mit gewähltem Spektrum; dadurch ist jedes Feld von Natur aus kachelbar.
Kaustik: periodisches Höhenfeld mit ganzzahligen Schwingungen je Durchlauf, die Strahlen werden gebrochen und gezählt
(Lichtstrahlen-Streuung statt Zellmuster), daher schließt die Animation von Bild 16 wieder an Bild 1 an.
"""
import os
import struct
import sys
import zlib

import numpy as np

# ---------------------------------------------------------------- Hilfen

def png_schreiben(pfad, rgb):
    """rgb: Feld (h, w, 3) mit Werten 0..1 -> 8-Bit-PNG (Farbtyp 2)."""
    b = (np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)
    h, w, _ = b.shape
    roh = b"".join(b"\x00" + b[y].tobytes() for y in range(h))

    def chunk(art, daten):
        c = struct.pack(">I", len(daten)) + art + daten
        return c + struct.pack(">I", zlib.crc32(art + daten) & 0xFFFFFFFF)

    with open(pfad, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(roh, 9)) + chunk(b"IEND", b""))


def frequenzen(n):
    k = np.fft.fftfreq(n) * n  # ganze Schwingungen je Bild
    return np.meshgrid(k, k)


def rauschen(n, rng, kmin, kmax, steigung, streck=1.0, winkel=0.0):
    """Gaußsches Rauschen mit Leistungsspektrum ~ k^-steigung zwischen kmin und kmax (weiche Ränder), Mittel 0, Streuung 1.
    streck > 1: Muster in Richtung `winkel` (Grad) in die Länge gezogen."""
    kx, ky = frequenzen(n)
    w = np.radians(winkel)
    ka = kx * np.cos(w) + ky * np.sin(w)    # Richtung der Streckung
    kq = -kx * np.sin(w) + ky * np.cos(w)
    k = np.sqrt((ka / streck) ** 2 + kq ** 2) + 1e-9
    amp = k ** (-steigung / 2)
    amp *= 1 / (1 + (kmin / k) ** 4) / (1 + (k / kmax) ** 4)  # weicher Bandpass
    amp[0, 0] = 0
    weiss = np.fft.fft2(rng.standard_normal((n, n)))
    r = np.fft.ifft2(weiss * amp).real
    return (r - r.mean()) / r.std()


def verzerren(feld, dx, dy):
    """Feld an den um (dx, dy) Pixel verschobenen Stellen lesen (bilinear, umlaufend)."""
    n = feld.shape[0]
    y, x = np.mgrid[0:n, 0:n].astype(np.float64)
    x = (x + dx) % n
    y = (y + dy) % n
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - x0, y - y0
    x1, y1 = (x0 + 1) % n, (y0 + 1) % n
    return (feld[y0, x0] * (1 - fx) * (1 - fy) + feld[y0, x1] * fx * (1 - fy)
            + feld[y1, x0] * (1 - fx) * fy + feld[y1, x1] * fx * fy)


def gefaelle(h):
    """Steigung (Kachel-sicher, Mittendifferenz)."""
    return (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / 2, (np.roll(h, -1, 0) - np.roll(h, 1, 0)) / 2


def normalkarte(h, streuung_xy):
    """Höhenfeld -> Normalkarte (Tangentenraum, grün = nach oben); Steilheit so, dass die Streuung von x und y stimmt."""
    gx, gy = gefaelle(h)
    s = streuung_xy / ((gx.std() + gy.std()) / 2)
    nx, ny = -gx * s, -gy * s
    nz = np.sqrt(np.clip(1 - nx ** 2 - ny ** 2, 0.05, 1))
    n = np.stack([nx, ny, nz], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5

# ---------------------------------------------------------------- die fünf Bilder

def normal_a():
    """Breite, weiche Dünung: wenige lange Wellen, leicht verwirbelt, Spitzen ein wenig schärfer als Täler."""
    rng = np.random.default_rng(1480)
    n = 512
    h = rauschen(n, rng, 2, 14, 3.2, streck=1.6, winkel=25)
    h += 0.35 * rauschen(n, rng, 10, 36, 2.8)
    h = verzerren(h, 14 * rauschen(n, rng, 1, 6, 2), 14 * rauschen(n, rng, 1, 6, 2))
    h = h + 0.18 * h ** 2
    return normalkarte(h, 0.082)


def normal_b():
    """Feineres Gekräusel auf mittleren Wellen: unregelmäßig, nicht kreisrund, ohne Wiederholung innerhalb der Kachel."""
    rng = np.random.default_rng(1493)
    n = 1024
    h = rauschen(n, rng, 3, 18, 3.0, streck=1.25, winkel=-20)
    h += 0.4 * rauschen(n, rng, 12, 40, 2.8, streck=1.15, winkel=40)
    h += 0.1 * (1 - np.abs(rauschen(n, rng, 8, 30, 2.6)))  # vereinzelte Kämme
    h = verzerren(h, 9 * rauschen(n, rng, 2, 8, 2), 9 * rauschen(n, rng, 2, 8, 2))
    return normalkarte(h, 0.26)


def stroemung():
    """R,G: weiche, schräg gestreckte Schlieren um 0,43 (Mittelwert wie bisher), B = 0."""
    rng = np.random.default_rng(1500)
    n = 512
    r = rauschen(n, rng, 6, 46, 2.4, streck=2.0, winkel=35)
    g = rauschen(n, rng, 6, 46, 2.4, streck=2.0, winkel=-30)
    out = np.zeros((n, n, 3))
    out[..., 0] = 0.43 + 0.124 * r
    out[..., 1] = 0.44 + 0.124 * g
    return out


def schaum():
    """Zerrissene Schlieren und Flocken, keine runden Blasen. Der Shader hebt hohe Werte stark an (x^10), die hellen Bereiche
    bestimmen deshalb die Form des Schaums; dunkel = klares Wasser."""
    rng = np.random.default_rng(1519)
    n = 256
    fleck = rauschen(n, rng, 2, 8, 2.6, streck=1.8, winkel=20)                 # grobe Felder
    schlier = rauschen(n, rng, 4, 16, 2.8, streck=2.6, winkel=20)               # Schlieren
    flocke = rauschen(n, rng, 10, 32, 3.0, streck=1.4, winkel=-15)              # Flocken
    flocke = verzerren(flocke, 3 * rauschen(n, rng, 2, 6, 2), 3 * rauschen(n, rng, 2, 6, 2))
    riss = 1 - np.abs(rauschen(n, rng, 5, 20, 2.8, streck=2.0, winkel=20))      # zerrissene Fäden
    z = 0.8 * fleck + 0.7 * schlier + 0.15 * flocke + 0.25 * riss
    z = (z - z.mean()) / z.std()
    v = np.clip(0.48 + 0.22 * z, 0, 1)
    return np.repeat(v[..., None], 3, -1)


def kaustik():
    """16 Bilder zu 256 px als 4x4-Atlas (Reihe für Reihe). Nahtlos im Raum (umlaufendes Höhenfeld) und in der Zeit
    (ganzzahlige Schwingungen je Durchlauf)."""
    rng = np.random.default_rng(1531)
    n, quell, frames = 256, 512, 16
    kx, ky = frequenzen(quell)
    k = np.sqrt(kx ** 2 + ky ** 2) + 1e-9
    # Anfangsfeld: Wellen mit Längen von 1/2 bis 1/7,5 der Kachel (grob genug für klare Linien)
    amp = k ** (-1.1) / (1 + (2.0 / k) ** 4) / (1 + (k / 7.5) ** 4)
    amp[0, 0] = 0
    h0 = np.fft.fft2(rng.standard_normal((quell, quell)) + 1j * rng.standard_normal((quell, quell))) * amp
    # Tempo je Welle: ganze Schwingungen je Durchlauf (große Wellen langsam), umlaufend in der Zeit
    omega = (1 + np.round(0.22 * k)).astype(int)
    gx, gy = np.meshgrid(np.arange(quell), np.arange(quell))
    tiefe = 5.2   # Abstand Wasseroberfläche -> Grund in Bildpunkten, bestimmt, wie stark die Strahlen sich bündeln
    atlas = np.zeros((n * 4, n * 4))
    for f in range(frames):
        phase = np.exp(2j * np.pi * omega * f / frames)
        h = np.fft.ifft2(h0 * phase).real
        h *= 2.1 / h.std() * 0.55
        dx, dy = gefaelle(h)
        zx = (gx + tiefe * dx * quell / 64) % quell * (n / quell)
        zy = (gy + tiefe * dy * quell / 64) % quell * (n / quell)
        x0, y0 = np.floor(zx).astype(int), np.floor(zy).astype(int)
        fx, fy = zx - x0, zy - y0
        x1, y1 = (x0 + 1) % n, (y0 + 1) % n
        bild = np.zeros(n * n)
        for xx, yy, w in ((x0, y0, (1 - fx) * (1 - fy)), (x1, y0, fx * (1 - fy)), (x0, y1, (1 - fx) * fy), (x1, y1, fx * fy)):
            bild += np.bincount((yy * n + xx).ravel(), weights=w.ravel(), minlength=n * n)
        bild = bild.reshape(n, n)
        # leicht weichzeichnen (umlaufend) gegen Körnung
        for ax in (0, 1):
            bild = (np.roll(bild, 1, ax) + 2 * bild + np.roll(bild, -1, ax)) / 4
        atlas[(f // 4) * n:(f // 4 + 1) * n, (f % 4) * n:(f % 4 + 1) * n] = bild
    # Auf die Werteverteilung des alten Atlas bringen: Mittel ~0,09, Spitzen bis ~1, Grundlicht ~0,02
    a = atlas / atlas.mean()
    a = np.clip(a - 0.95, 0, None)
    a = a / np.percentile(a, 99.9)
    a = np.clip(a, 0, 1) ** 1.15
    a = 0.02 + 0.96 * a
    return np.repeat(a[..., None], 3, -1)


def main():
    ziel = "game/assets/texturen/realistisches_wasser"
    if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1:
        ziel = sys.argv[sys.argv.index("--") + 1]
    os.makedirs(ziel, exist_ok=True)
    for name, fn in (("wasser_normal_a", normal_a), ("wasser_normal_b", normal_b), ("wasser_stroemung", stroemung),
                     ("wasser_schaum", schaum), ("wasser_kaustik", kaustik)):
        bild = fn()
        png_schreiben(os.path.join(ziel, name + ".png"), bild)
        print(name, bild.shape[:2], "Mittel %.3f Streuung %.3f" % (bild[..., 0].mean(), bild[..., 0].std()))


if __name__ == "__main__":
    main()
