"""Texturen der drei Buecher, selbst gemalt (numpy, feste Saat), keine Fremdbilder.
Alle Funktionen liefern uint8-Felder (Hoehe, Breite, 3) in sRGB. Kachelbare Bilder (Leder, Riemen, Schnitt, Pergamentkante) sind periodisch."""
import struct
import zlib

import numpy as np


def png_schreiben(pfad, rgb):
    h, b, _ = rgb.shape
    roh = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff)
    with open(pfad, "wb") as fh:
        fh.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", b, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(roh, 9)) + chunk(b"IEND", b""))


def vnoise(rng, h, b, gy, gx):
    """Periodisches Wertrauschen (kachelbar), glatt interpoliert."""
    g = rng.random((gy, gx))
    ys = np.arange(h) * gy / h
    xs = np.arange(b) * gx / b
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    fy = ys - y0
    fx = xs - x0
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)
    y1 = (y0 + 1) % gy
    x1 = (x0 + 1) % gx
    a = g[np.ix_(y0, x0)]
    bb = g[np.ix_(y0, x1)]
    c = g[np.ix_(y1, x0)]
    d = g[np.ix_(y1, x1)]
    fyy = fy[:, None]
    fxx = fx[None, :]
    return (a * (1 - fxx) + bb * fxx) * (1 - fyy) + (c * (1 - fxx) + d * fxx) * fyy


def _fertig(bild):
    return np.clip(bild * 255 + 0.5, 0, 255).astype(np.uint8)


def leder(saat, farbe, n=256, falten=1.0, narbung=0.22):
    """Kachelbares Leder: Narbung (feine Punkte), grosse Flecken, einzelne dunkle Falten."""
    rng = np.random.default_rng(saat)
    fleck = 0.5 * vnoise(rng, n, n, 5, 5) + 0.3 * vnoise(rng, n, n, 14, 14) + 0.2 * vnoise(rng, n, n, 40, 40)
    narb = vnoise(rng, n, n, 128, 128) * 0.6 + vnoise(rng, n, n, 64, 64) * 0.4
    f = vnoise(rng, n, n, 7, 7) - 0.5
    falte = np.exp(-(f / 0.018) ** 2) * falten
    farbe = np.array(farbe)
    bild = farbe * (0.80 + 0.40 * fleck[..., None]) * (1.0 + (narb[..., None] - 0.5) * narbung)
    bild = bild * (1.0 - 0.38 * falte[..., None])
    return _fertig(bild)


def kachel_gross(kachel, h, b):
    """Kachelbild auf (h, b) auslegen."""
    ky = -(-h // kachel.shape[0])
    kx = -(-b // kachel.shape[1])
    return np.tile(kachel, (ky, kx, 1))[:h, :b].astype(float) / 255.0


def _rechteck_linie(bild, x0, y0, x1, y1, dicke, farbe):
    """Rahmenlinie eines Rechtecks (Pixelgrenzen), Farbe 0..1."""
    farbe = np.array(farbe)
    bild[y0:y0 + dicke, x0:x1 + 1] = farbe
    bild[y1 - dicke + 1:y1 + 1, x0:x1 + 1] = farbe
    bild[y0:y1 + 1, x0:x0 + dicke] = farbe
    bild[y0:y1 + 1, x1 - dicke + 1:x1 + 1] = farbe


def einband_personal(lederkachel, b=512, h=768):
    """Derber Arbeitsbuch-Einband: dunkles Leder, abgegriffene Mitte, helle Nahtstiche am Rand, Kratzer, dunkle Rillen am Rand."""
    rng = np.random.default_rng(4101)
    bild = kachel_gross(lederkachel, h, b)
    yy, xx = np.mgrid[0:h, 0:b]
    rand = np.minimum.reduce([xx, b - 1 - xx, yy, h - 1 - yy]).astype(float)
    # abgegriffen: Mitte heller, Rand dunkler
    abgr = 0.5 + 0.5 * np.clip(1.0 - rand / 130.0, 0, 1)
    bild = bild * (0.80 + 0.30 * (1 - np.clip(1.0 - rand / 140.0, 0, 1))[..., None]) * (1.0 - 0.16 * (np.clip(1.0 - rand / 60.0, 0, 1))[..., None])
    mitte = vnoise(rng, h, b, 6, 4)
    bild = bild * (0.92 + 0.16 * mitte[..., None]) * (0.96 + 0.08 * abgr[..., None])
    # Naht: Stiche in 22 Pixel Abstand zum Rand
    faden = np.array([0.74, 0.62, 0.40])
    for y in range(22, h - 22, 18):
        bild[y:y + 10, 21:24] = faden
        bild[y:y + 10, b - 24:b - 21] = faden
    for x in range(22, b - 22, 18):
        bild[21:24, x:x + 10] = faden
        bild[h - 24:h - 21, x:x + 10] = faden
    # Rille innen neben der Naht
    dunkel = np.array([0.13, 0.08, 0.05])
    _rechteck_linie(bild, 33, 33, b - 34, h - 34, 2, dunkel)
    # Kratzer
    for _ in range(26):
        x = rng.integers(20, b - 20)
        y = rng.integers(20, h - 20)
        l = rng.integers(14, 60)
        w = rng.random() * 1.2 - 0.6
        for k in range(l):
            xi = int(x + k)
            yi = int(y + k * w)
            if 0 <= xi < b and 0 <= yi < h:
                bild[yi, xi] = bild[yi, xi] * 0.55 + np.array([0.62, 0.48, 0.32]) * 0.45
    return _fertig(bild)


def einband_familie(lederkachel, feld_u, feld_v, b=512, h=768):
    """Edler Einband: dunkelrotes Leder, doppelter Goldrahmen, Zierpunkte, Eckbogen; Aussparung (Goldrahmen) fuer das Wappenfeld.
    feld_u, feld_v = halbe Breite und Hoehe des Wappenfelds als Anteil des Deckels (Mitte des Deckels)."""
    rng = np.random.default_rng(4102)
    bild = kachel_gross(lederkachel, h, b)
    yy, xx = np.mgrid[0:h, 0:b]
    rand = np.minimum.reduce([xx, b - 1 - xx, yy, h - 1 - yy]).astype(float)
    bild = bild * (0.88 + 0.12 * vnoise(rng, h, b, 5, 4)[..., None]) * (1.0 - 0.20 * np.clip(1.0 - rand / 40.0, 0, 1)[..., None])
    gold = np.array([0.88, 0.70, 0.30])
    gold_d = np.array([0.62, 0.46, 0.16])
    _rechteck_linie(bild, 24, 24, b - 25, h - 25, 9, gold)
    _rechteck_linie(bild, 44, 44, b - 45, h - 45, 3, gold)
    # Zierpunkte zwischen den Rahmen
    for x in range(60, b - 60, 22):
        for y in (34, h - 35):
            bild[y - 2:y + 3, x - 2:x + 3] = gold_d
    for y in range(60, h - 60, 22):
        for x in (34, b - 35):
            bild[y - 2:y + 3, x - 2:x + 3] = gold_d
    # Eckbogen (Viertelkreise nach innen)
    for (cx, cy) in ((44, 44), (b - 45, 44), (44, h - 45), (b - 45, h - 45)):
        for r in (30, 38):
            ang = np.linspace(0, 2 * np.pi, 400)
            for a in ang:
                x = int(cx + r * np.cos(a))
                y = int(cy + r * np.sin(a))
                if 0 <= x < b and 0 <= y < h and (x - cx) * (1 if cx < b / 2 else -1) >= 0 and (y - cy) * (1 if cy < h / 2 else -1) >= 0:
                    bild[y:y + 3, x:x + 3] = gold
    # Rahmen ums Wappenfeld (das Feld selbst liegt als eigene Flaeche darueber)
    fx0 = int(b * (0.5 - feld_u)) - 14
    fx1 = int(b * (0.5 + feld_u)) + 14
    fy0 = int(h * (0.5 - feld_v)) - 14
    fy1 = int(h * (0.5 + feld_v)) + 14
    bild[fy0:fy1 + 1, fx0:fx1 + 1] = np.array([0.30, 0.06, 0.08])
    _rechteck_linie(bild, fx0, fy0, fx1, fy1, 6, gold)
    _rechteck_linie(bild, fx0 + 10, fy0 + 10, fx1 - 10, fy1 - 10, 2, gold_d)
    return _fertig(bild)


def einband_kontor(b=512, h=768):
    """Pergamenteinband des Kontorbuchs: cremefarben mit Flecken und Faser, Schildchen mit Titelzeilen (Platzhalter)."""
    rng = np.random.default_rng(4103)
    fleck = 0.55 * vnoise(rng, h, b, 5, 4) + 0.30 * vnoise(rng, h, b, 16, 12) + 0.15 * vnoise(rng, h, b, 60, 40)
    faser = vnoise(rng, h, b, 120, 6)
    hell = np.array([0.86, 0.78, 0.60])
    tief = np.array([0.68, 0.56, 0.36])
    t = np.clip((fleck - 0.35) * 1.8, 0, 1)[..., None]
    bild = hell * (1 - t) + tief * t
    bild = bild * (0.94 + 0.10 * faser[..., None])
    yy, xx = np.mgrid[0:h, 0:b]
    rand = np.minimum.reduce([xx, b - 1 - xx, yy, h - 1 - yy]).astype(float)
    schmutz = np.clip(1.0 - rand / 45.0, 0, 1) ** 2
    bild = bild * (1 - 0.35 * schmutz[..., None]) + np.array([0.34, 0.24, 0.12]) * 0.35 * schmutz[..., None]
    # Schildchen oben, Mitte
    x0, x1, y0, y1 = 110, 400, 470, 620   # y wird unten gerechnet: Bildzeile 0 = oben
    zeile0 = h - y1
    zeile1 = h - y0
    bild[zeile0:zeile1, x0:x1] = np.array([0.93, 0.90, 0.80])
    tinte = np.array([0.20, 0.12, 0.08])
    _rechteck_linie(bild, x0 + 6, zeile0 + 6, x1 - 7, zeile1 - 7, 3, tinte)
    _rechteck_linie(bild, x0 + 14, zeile0 + 14, x1 - 15, zeile1 - 15, 1, tinte)
    for k, (l0, l1) in enumerate(((70, 220), (110, 180), (150, 200))):
        bild[zeile0 + 34 + k * 30:zeile0 + 34 + k * 30 + 5, x0 + 30 + (l0 - 70):x0 + 30 + l1] = tinte
    return _fertig(bild)


def riemen(saat=4110, n=128):
    """Helles, gegerbtes Riemenleder (kachelbar)."""
    return leder(saat, (0.62, 0.45, 0.26), n=n, falten=0.5, narbung=0.15)


def pergamentkante(n=128):
    rng = np.random.default_rng(4111)
    f = 0.6 * vnoise(rng, n, n, 6, 6) + 0.4 * vnoise(rng, n, n, 24, 24)
    bild = np.array([0.80, 0.70, 0.50]) * (0.88 + 0.20 * f[..., None])
    return _fertig(bild)


# ------------------------------------------------------------------ Seiten (512 x 768, Hochformat, Bildzeile 0 = oben)
def _papier(rng, h, b, farbe, fleck_st=0.10):
    f = 0.55 * vnoise(rng, h, b, 4, 3) + 0.30 * vnoise(rng, h, b, 14, 10) + 0.15 * vnoise(rng, h, b, 60, 40)
    faser = vnoise(rng, h, b, 100, 5)
    bild = np.array(farbe) * (1.0 - fleck_st + fleck_st * 2 * f[..., None]) * (0.95 + 0.07 * faser[..., None])
    yy, xx = np.mgrid[0:h, 0:b]
    rand = np.minimum.reduce([xx, b - 1 - xx, yy, h - 1 - yy]).astype(float)
    rand_d = np.clip(1.0 - rand / 22.0, 0, 1) ** 2
    return bild * (1.0 - 0.18 * rand_d[..., None])


def _wortzeile(bild, rng, y, x0, x1, dicke, farbe, wort=(14, 52), luecke=9):
    x = x0
    farbe = np.array(farbe)
    while x < x1:
        l = int(rng.integers(wort[0], wort[1]))
        if x + l > x1:
            break
        yy = y + int(rng.integers(-1, 2))
        bild[yy:yy + dicke, x:x + l] = bild[yy:yy + dicke, x:x + l] * 0.2 + farbe * 0.8
        x += l + luecke + int(rng.integers(0, 5))


def seiten_personal(b=512, h=768):
    """Arbeitsbuch: grobes Papier, handschriftliche Zeilen (Wortstriche), Randlinie."""
    rng = np.random.default_rng(4121)
    bild = _papier(rng, h, b, (0.90, 0.82, 0.64), 0.12)
    tinte = (0.22, 0.14, 0.09)
    for i, y in enumerate(range(80, h - 70, 27)):
        _wortzeile(bild, rng, y, 54, b - 40 - int(rng.integers(0, 30)), 3, tinte)
    bild[60:h - 50, 42:44] = bild[60:h - 50, 42:44] * 0.5 + np.array([0.55, 0.20, 0.14]) * 0.5
    return _fertig(bild)


def seiten_familie(b=512, h=768):
    """Familienbuch: feines Papier, doppelte Rotlinie als Rahmen, Zeilen, grosser Anfangsbuchstabe (Platzhalter)."""
    rng = np.random.default_rng(4122)
    bild = _papier(rng, h, b, (0.93, 0.87, 0.70), 0.08)
    rot = np.array([0.58, 0.16, 0.12])
    _rechteck_linie(bild, 30, 30, b - 31, h - 31, 3, rot)
    _rechteck_linie(bild, 40, 40, b - 41, h - 41, 1, rot)
    tinte = (0.26, 0.17, 0.12)
    bild[70:120, 60:110] = rot * 0.85
    for i, y in enumerate(range(80, h - 80, 24)):
        x0 = 120 if i < 2 else 60
        _wortzeile(bild, rng, y, x0, b - 60, 2, tinte, wort=(12, 44), luecke=8)
    return _fertig(bild)


def seiten_kontor(b=512, h=768):
    """Rechnungsbuch: Kopfzeile, Spalten (Datum | Posten | Pfund | Schilling | Pfennig) mit roten Doppellinien, blaugraue Zeilenlinien, einige Eintraege."""
    rng = np.random.default_rng(4123)
    bild = _papier(rng, h, b, (0.91, 0.89, 0.79), 0.07)
    blau = np.array([0.52, 0.60, 0.70])
    rot = np.array([0.66, 0.20, 0.16])
    tinte = np.array([0.20, 0.16, 0.14])
    zeilenabst = 24
    y_kopf = 90
    for y in range(y_kopf, h - 40, zeilenabst):
        bild[y:y + 2, 28:b - 28] = bild[y:y + 2, 28:b - 28] * 0.35 + blau * 0.65
    # Kopf
    bild[40:46, 28:b - 28] = tinte * 0.9
    bild[y_kopf + 2:y_kopf + 6, 28:b - 28] = tinte * 0.9
    spalten = (28, 108, 330, 390, 450, b - 28)
    for x in spalten:
        bild[40:h - 36, x:x + 3] = bild[40:h - 36, x:x + 3] * 0.2 + rot * 0.8
    for x in (108, 330, 390, 450):
        bild[40:h - 36, x + 5:x + 6] = bild[40:h - 36, x + 5:x + 6] * 0.4 + rot * 0.6
    # Kopfbeschriftung (Platzhalter-Striche)
    for (x0, x1) in ((40, 90), (160, 260), (338, 380), (398, 440), (458, 496)):
        bild[62:67, x0:x1] = tinte
    # Eintraege
    for k in range(10):
        y = y_kopf + 6 + zeilenabst * (k + 1) * 1 - 4
        if k % 3 == 2:
            continue
        _wortzeile(bild, rng, y + 6, 36, 100, 3, tinte, wort=(10, 20), luecke=5)
        _wortzeile(bild, rng, y + 6, 118, 322, 3, tinte, wort=(14, 40), luecke=8)
        for (x0, x1) in ((338, 382), (398, 442), (458, 498)):
            if rng.random() < 0.8:
                bild[y + 5:y + 8, x1 - int(rng.integers(12, 36)):x1 - 4] = tinte
    return _fertig(bild)


# ------------------------------------------------------------------ Seitenschnitt (Kanten des Blocks, 64 x 64; v laeuft ueber die Dicke)
def schnitt_personal(n=64):
    rng = np.random.default_rng(4131)
    lagen = vnoise(rng, n, n, 24, 2)
    bild = np.array([0.66, 0.56, 0.38]) * (0.70 + 0.45 * lagen[..., None])
    bild[::9] *= 0.78
    return _fertig(bild)


def schnitt_familie(n=64):
    rng = np.random.default_rng(4132)
    lagen = vnoise(rng, n, n, 16, 2)
    bild = np.array([0.88, 0.70, 0.30]) * (0.82 + 0.28 * lagen[..., None])
    bild[::8] *= 0.88
    return _fertig(bild)


def schnitt_kontor(n=64):
    rng = np.random.default_rng(4133)
    lagen = vnoise(rng, n, n, 20, 2)
    bild = np.array([0.82, 0.74, 0.54]) * (0.82 + 0.28 * lagen[..., None])
    punkte = rng.random((n, n))
    bild[punkte > 0.93] = np.array([0.66, 0.16, 0.12])
    return _fertig(bild)
