"""Texturen für Feuer und Flammen (Phase 1, Testszene) – alles prozedural erzeugt, mit festem Startwert, keine fremden Bilder.

    python tools/feuer/texturen.py        (braucht nur numpy; z. B. mit dem Python von Blender)

Schreibt nach game/assets/texturen/feuer/:
    flamme_flipbook.png   8 × 4 Bilder zu je 128 × 128 Pixeln (1024 × 512): eine Flammenzunge, die flackert und nach oben ausfranst.
                          Zeile für Zeile von links oben; das letzte Bild geht nahtlos ins erste über (Schleife). Farbe von
                          weißgelb (heiß, unten Mitte) über Orange bis dunkelrot (Rand), Alpha = Dichte. Für die Partikel-Flamme (B).
    rauch_flipbook.png    4 × 4 Bilder zu je 128 × 128 Pixeln (512 × 512): eine weiche, wirbelnde Rauchwolke (grau, Alpha = Dichte). Für B.
    funke.png             32 × 32: ein glühender Punkt (Funke, Glutstück). Für B.
    rauschen.png          128 × 128, kachelbar: R, G, B drei verschiedene Rauschmuster, A = 255. Für die Shader-Flamme (A) und den Rauch.

Die Bilder der Flamme laufen im Takt: Das Rauschen steigt von Bild zu Bild um ganze Gitterperioden nach oben, darum schließt sich
die Schleife ohne Sprung. Die Farbe wird als „gerade“ Farbe mit Alpha abgelegt (nicht vormultipliziert), auch am Rand.
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
#  Flamme
# ═════════════════════════════════════════════════════════════════════════════

# Farbverlauf nach „Temperatur“ T (0 kalt/Rand … 1 heiß/Kern): Stützstellen und Farben (linear 0..1)
T_STELLEN = [0.00, 0.12, 0.30, 0.50, 0.72, 0.90, 1.00]
T_FARBEN = np.array([
    [0.30, 0.02, 0.00],   # dunkelrot am äußersten Rand
    [0.70, 0.07, 0.01],   # rot
    [0.98, 0.28, 0.03],   # rotorange
    [1.00, 0.55, 0.08],   # orange
    [1.00, 0.80, 0.22],   # gelb
    [1.00, 0.95, 0.60],   # hellgelb
    [1.00, 1.00, 0.88],   # weißgelb im Kern
])


def farbverlauf(T):
    return np.dstack([np.interp(T, T_STELLEN, T_FARBEN[:, k]) for k in range(3)])


def flammenbild(t, g=128, saat=SAAT):
    """Ein Bild der Flamme zur Zeit t (0..1, Schleife). Rückgabe g × g × 4 (RGBA, 0..1). Unten ist der Fuß, oben die Spitze."""
    v, u = np.mgrid[0:g, 0:g]
    x = (u + 0.5) / g * 2 - 1                  # −1 … 1
    h = 1 - (v + 0.5) / g                      # 0 unten … 1 oben (die Zunge füllt die Höhe nicht ganz)
    h = np.clip((h - 0.04) / 0.90, -0.2, 1.3)  # etwas Rand unten und oben
    X = (x + 1) / 2
    hh = np.clip(h, 0, 1)

    # Große, langsame Bewegung (die ganze Zunge wiegt sich) und feines, schnelles Lecken (die Spitzen): in X fein, in Y gestreckt,
    # damit die Ränder senkrechte Zungen bilden und keine waagerechten Streifen
    grob = fbm(X, hh * 0.5 - t * 1.0, 2, 4, 3, saat + 1)
    fein = fbm(X, hh * 1.0 - t * 2.0, 8, 5, 3, saat + 2)
    zack = fbm(X, hh * 1.5 - t * 3.0, 14, 8, 2, saat + 3)

    wiegen = (grob - 0.5) * 0.9 * hh ** 1.4 + 0.10 * hh ** 1.6 * np.sin(2 * np.pi * (t + 0.15))
    xc = x - wiegen

    # Umriss: eine Tropfenform, unten gerundet, nach oben spitz
    breite = 0.66 * (1 - hh) ** 0.85 * (1 - np.exp(-hh * 12.0)) + 0.004
    innen = np.clip(1 - (np.abs(xc) / breite) ** 2, 0, 1)

    # Ausfransen: je weiter oben, desto stärker frisst das Rauschen den Rand an; oben lösen sich einzelne Zungen
    frass = (0.10 + 0.95 * hh ** 1.0) * (1.0 - (0.6 * fein + 0.4 * zack)) * 1.25
    d = np.clip(innen * (1.25 + 0.35 * hh) - frass, 0, 1)

    # Heiß ist es unten und in der Mitte: nur der dichte Kern ist weißgelb, die Ränder sind orange bis rot
    T = np.clip(d ** 1.7 * (1.25 - 0.75 * hh), 0, 1)
    rgb = farbverlauf(T)
    alpha = stufe(0.02, 0.35, d) * (0.92 - 0.12 * hh)
    # unten sanft einblenden (kein harter Rand auf dem Boden), ganz oben auslaufen
    alpha *= stufe(0.0, 0.07, h) * (1 - stufe(0.80, 1.05, h))
    return np.dstack([rgb, alpha])


def flammen_blatt(spalten=8, zeilen=4, g=128, saat=SAAT):
    n = spalten * zeilen
    blatt = np.zeros((zeilen * g, spalten * g, 4))
    for k in range(n):
        z, s = divmod(k, spalten)
        blatt[z * g:(z + 1) * g, s * g:(s + 1) * g] = flammenbild(k / n, g, saat)
    return blatt


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
    for name, bild in (("flamme_flipbook.png", flammen_blatt()), ("rauch_flipbook.png", rauch_blatt()),
                       ("funke.png", funke()), ("rauschen.png", rauschen())):
        pfad = os.path.join(ZIEL, name)
        png(pfad, bild)
        print("geschrieben:", os.path.normpath(pfad), f"({os.path.getsize(pfad) // 1024} KB)")


if __name__ == "__main__":
    main()
