"""Texturen für Feuer und Flammen (Phase 1, Testszene) – alles prozedural erzeugt, mit festem Startwert, keine fremden Bilder.

    python tools/feuer/texturen.py        (braucht nur numpy; z. B. mit dem Python von Blender)

Schreibt nach game/assets/texturen/feuer/:
    rauch_flipbook.png    4 × 4 Bilder zu je 128 × 128 Pixeln (512 × 512): eine weiche, wirbelnde Rauchwolke (grau, Alpha = Dichte). Für B.
    funke.png             32 × 32: ein glühender Punkt (Funke, Glutstück). Für B.
    rauschen.png          128 × 128, kachelbar: R, G, B drei verschiedene Rauschmuster, A = 255. Für die Shader-Flamme (A) und den Rauch.

Die Bilder des Rauchs laufen im Takt: Das Rauschen steigt von Bild zu Bild um ganze Gitterperioden nach oben, darum schließt sich
die Schleife ohne Sprung. Die Farbe wird als „gerade“ Farbe mit Alpha abgelegt (nicht vormultipliziert), auch am Rand.

Die Flammen-Flipbooks (flamme_ruhig.png, flamme_wild.png) kommen seit Phase 1b aus einer Blender-Feuersimulation: tools/feuer/flammen_sim.py.
Das frühere gerechnete Flammen-Flipbook (flamme_flipbook.png) ist entfallen (Nutzer 29.10.2026: wirkte nicht echt).
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


def stufe(a, b, x):
    return glatt(np.clip((x - a) / (b - a), 0, 1))


# ═════════════════════════════════════════════════════════════════════════════
#  Rauch, Funke, Rauschen
# ═════════════════════════════════════════════════════════════════════════════

def rauchbild(t, g=128, saat=SAAT + 50):
    """Eine weiche, wirbelnde Wolke (grau, Alpha = Dichte), Schleife in t."""
    v, u = np.mgrid[0:g, 0:g]
    x = (u + 0.5) / g * 2 - 1
    y = (v + 0.5) / g * 2 - 1
    r = np.sqrt(x * x + y * y)
    X = (x + 1) / 2
    Y = (y + 1) / 2
    n1 = fbm(X, Y - t * 1.0, 3, 3, 4, saat)
    n2 = fbm(X + 0.37, Y * 1.0 - t * 2.0 + 0.5, 4, 4, 3, saat + 7)
    wolke = 1 - r / (1.0 + 0.10 * (n1 - 0.5))
    dichte = np.clip(wolke * 1.5 - 0.55 * (1 - n1) - 0.25 * (1 - n2) + 0.3, 0, 1)
    dichte = stufe(0.0, 0.9, dichte) * np.clip(1 - stufe(0.78, 1.0, r), 0, 1)
    grau = 0.46 + 0.34 * n2 - 0.10 * (1 - dichte)
    rgb = np.dstack([grau * 1.00, grau * 0.99, grau * 0.97])
    return np.dstack([rgb, dichte * 0.85])


def rauch_blatt(spalten=4, zeilen=4, g=128):
    n = spalten * zeilen
    blatt = np.zeros((zeilen * g, spalten * g, 4))
    for k in range(n):
        z, s = divmod(k, spalten)
        blatt[z * g:(z + 1) * g, s * g:(s + 1) * g] = rauchbild(k / n, g)
    return blatt


def funke(g=32):
    v, u = np.mgrid[0:g, 0:g]
    r = np.sqrt(((u + 0.5) / g * 2 - 1) ** 2 + ((v + 0.5) / g * 2 - 1) ** 2)
    kern = np.exp(-(r * 2.6) ** 2)
    rand = np.exp(-(r * 1.5) ** 2)
    rgb = np.dstack([np.ones_like(r), 0.45 + 0.5 * kern, 0.10 + 0.6 * kern ** 2])
    return np.dstack([rgb, np.clip(0.75 * rand + 0.6 * kern, 0, 1)])


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
    for name, bild in (("rauch_flipbook.png", rauch_blatt()),
                       ("funke.png", funke()), ("rauschen.png", rauschen())):
        pfad = os.path.join(ZIEL, name)
        png(pfad, bild)
        print("geschrieben:", os.path.normpath(pfad), f"({os.path.getsize(pfad) // 1024} KB)")


if __name__ == "__main__":
    main()
