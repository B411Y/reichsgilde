"""Textur für Feuer und Flammen – prozedural erzeugt, mit festem Startwert, keine fremden Bilder.

    python tools/feuer/texturen.py        (braucht nur numpy; z. B. mit dem Python von Blender)

Schreibt nach game/assets/texturen/feuer/:
    rauschen.png          128 × 128, kachelbar: R, G, B drei verschiedene Rauschmuster, A = 255. Für das Wabern der Flamme, den Rauch und die Glut.

Die Flammen-Flipbooks (flamme_ruhig.png, flamme_wild.png) kommen aus einer Blender-Feuersimulation: tools/feuer/flammen_sim.py.
Entfallen sind das gerechnete Flammen-Flipbook (flamme_flipbook.png, Phase 1b) und mit der Partikel-Technik B das Rauch-Flipbook
(rauch_flipbook.png) und der Funke (funke.png): Seit Phase 2 (Nutzer 30.10.2026) zeichnen alle Feuer Flamme, Funken und Rauch mit Shadern.
"""
import os
import struct
import zlib

import numpy as np

ZIEL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "game", "assets", "texturen", "feuer")
SAAT = 20261029


# ═════════════════════════════════════════════════════════════════════════════
#  Rauschen: Wertrauschen auf einem Gitter, in Y immer ringförmig (für Schleifen), in X wahlweise
# ═════════════════════════════════════════════════════════════════════════════

def glatt(t):
    return t * t * (3 - 2 * t)


def rausch(X, Y, zx, zy, saat, ring_x=True):
    """Wertrauschen 0..1. X und Y in Gitterperioden (0..1 = ein ganzer Umlauf), `zx` × `zy` Zellen; Y läuft ringförmig herum."""
    g = np.random.default_rng(saat).random((zy, zx + 1))
    x = X * zx
    y = Y * zy
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = glatt(x - x0)
    fy = glatt(y - y0)
    if ring_x:
        xa, xb = x0 % zx, (x0 + 1) % zx
    else:
        xa, xb = np.clip(x0, 0, zx), np.clip(x0 + 1, 0, zx)
    ya, yb = y0 % zy, (y0 + 1) % zy
    oben = g[ya, xa] * (1 - fx) + g[ya, xb] * fx
    unten = g[yb, xa] * (1 - fx) + g[yb, xb] * fx
    return oben * (1 - fy) + unten * fy


def fbm(X, Y, zx, zy, oktaven, saat, ring_x=True):
    """Mehrere Lagen Rauschen übereinander (jede doppelt so fein, halb so stark), 0..1."""
    summe = np.zeros_like(X, dtype=float)
    stark = 1.0
    gesamt = 0.0
    for j in range(oktaven):
        summe += stark * rausch(X, Y, zx * 2 ** j, zy * 2 ** j, saat + 101 * j, ring_x)
        gesamt += stark
        stark *= 0.5
    return summe / gesamt


# ═════════════════════════════════════════════════════════════════════════════
#  Rauschen
# ═════════════════════════════════════════════════════════════════════════════

def rauschen(g=128, saat=SAAT + 90):
    v, u = np.mgrid[0:g, 0:g]
    X = (u + 0.5) / g
    Y = (v + 0.5) / g
    r = fbm(X, Y, 4, 4, 4, saat)
    gr = fbm(X, Y, 3, 3, 4, saat + 11)
    b = np.abs(2 * fbm(X, Y, 5, 5, 3, saat + 23) - 1)
    # Auf 0..1 spreizen, damit der Shader den vollen Bereich nutzt
    bild = [(k - k.min()) / (k.max() - k.min()) for k in (r, gr, b)]
    return np.dstack(bild + [np.ones_like(r)])


# ═════════════════════════════════════════════════════════════════════════════
#  PNG schreiben (ohne Pillow, wie tools/oberflaeche/texturen.py)
# ═════════════════════════════════════════════════════════════════════════════

def png(pfad, bild):
    """RGBA (0..1) als PNG: je Zeile der kleinere von zwei Filtern (Sub, Up), dann zlib."""
    roh = np.clip(np.round(bild * 255), 0, 255).astype(np.int16)
    hoehe, breite, _ = roh.shape
    sub = (roh - np.pad(roh, ((0, 0), (1, 0), (0, 0)))[:, :-1]) % 256
    up = (roh - np.pad(roh, ((1, 0), (0, 0), (0, 0)))[:-1]) % 256
    kosten = lambda f: np.minimum(f, 256 - f).reshape(hoehe, -1).sum(axis=1)
    nimm_up = kosten(up) < kosten(sub)
    zeilen = np.where(nimm_up[:, None, None], up, sub).astype(np.uint8).reshape(hoehe, -1)
    filt = np.where(nimm_up, 2, 1).astype(np.uint8)[:, None]
    daten = np.hstack([filt, zeilen]).tobytes()

    def chunk(t, c):
        return struct.pack(">I", len(c)) + t + c + struct.pack(">I", zlib.crc32(t + c) & 0xffffffff)

    with open(pfad, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", breite, hoehe, 8, 6, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(daten, 9)) + chunk(b"IEND", b""))


def main():
    os.makedirs(ZIEL, exist_ok=True)
    for name, bild in (("rauschen.png", rauschen()),):
        pfad = os.path.join(ZIEL, name)
        png(pfad, bild)
        print("geschrieben:", os.path.normpath(pfad), f"({os.path.getsize(pfad) // 1024} KB)")


if __name__ == "__main__":
    main()
