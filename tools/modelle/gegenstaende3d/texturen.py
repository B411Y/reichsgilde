"""Texturen der drei 3D-Gegenstaende (Kartentisch, Geldtruhe, Siegelstempel), selbst gemalt (numpy, feste Saat), keine Fremdbilder.
Alle Funktionen liefern uint8-Felder (Hoehe, Breite, 3) in sRGB; alle Bilder sind kachelbar (periodisch)."""
import os
import sys

import numpy as np

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HIER, "..", "buecher3d"))
from buch_texturen import png_schreiben, vnoise  # noqa: E402,F401  (gleiche Helfer wie bei den Buechern)


def _fertig(bild):
    return np.clip(bild * 255 + 0.5, 0, 255).astype(np.uint8)


def holz(saat, farbe, n=256, ringe=22.0, kontrast=0.30, aeste=2, fugen=0):
    """Gesaegtes Brett, Maserung laeuft entlang u (Bildbreite): Jahresringe als verzerrte Streifen, Poren, wenige Aeste."""
    rng = np.random.default_rng(saat)
    y = np.arange(n)[:, None] / n
    x = np.arange(n)[None, :] / n
    verz = 0.55 * vnoise(rng, n, n, 3, 2) + 0.25 * vnoise(rng, n, n, 8, 3)
    w = (y + 0.09 * verz) * ringe
    ring = 0.5 + 0.5 * np.cos(2 * np.pi * w)
    ring = ring ** 3
    poren = vnoise(rng, n, n, 96, 12)
    fleck = vnoise(rng, n, n, 4, 4)
    bild = 1.0 - kontrast * ring - 0.10 * (poren - 0.5) + 0.12 * (fleck - 0.5)
    for _ in range(aeste):
        cx, cy = rng.random(2)
        dx = (x - cx + 0.5) % 1.0 - 0.5
        dy = ((y - cy + 0.5) % 1.0 - 0.5) * 2.2
        d = np.sqrt(dx * dx + dy * dy)
        bild = bild - 0.45 * np.exp(-(d / 0.025) ** 2) - 0.12 * np.exp(-(d / 0.06) ** 2) * ring
    if fugen:
        # Brettfugen quer zur Maserung (dunkle Linie mit hellerer Kante darunter)
        v = (np.arange(n) * fugen / n) % 1.0
        fuge = np.where(v < 2.0 * fugen / n, 0.35, np.where(v < 3.0 * fugen / n, 1.12, 1.0))
        bild = bild * fuge[:, None]
    bild = np.array(farbe)[None, None, :] * bild[..., None]
    return _fertig(bild)


def eisen(saat=7301, n=128):
    """Geschmiedetes Eisen: dunkles Grau, Hammerschlag (Dellen), etwas Rost."""
    rng = np.random.default_rng(saat)
    hammer = vnoise(rng, n, n, 16, 16)
    fein = vnoise(rng, n, n, 64, 64)
    rost = np.clip((vnoise(rng, n, n, 6, 6) - 0.55) * 3.0, 0, 1) * (0.6 + 0.4 * fein)
    grau = np.array((0.20, 0.20, 0.21)) * (0.80 + 0.35 * hammer[..., None] + 0.12 * (fein[..., None] - 0.5))
    rostf = np.array((0.36, 0.19, 0.10))
    bild = grau * (1 - 0.55 * rost[..., None]) + rostf * 0.55 * rost[..., None]
    return _fertig(bild)


def messing(saat=7302, n=64):
    """Gegossenes Messing: warmes Gelb mit dunkler Patina in Flecken."""
    rng = np.random.default_rng(saat)
    f = vnoise(rng, n, n, 6, 6)
    g = vnoise(rng, n, n, 24, 24)
    pat = np.clip((f - 0.45) * 2.2, 0, 1)
    bild = np.array((0.74, 0.56, 0.24)) * (0.85 + 0.25 * g[..., None]) * (1 - 0.40 * pat[..., None]) + np.array((0.20, 0.22, 0.12)) * 0.30 * pat[..., None]
    return _fertig(bild)


def pergament(saat=7303, n=256):
    """Kalbspergament: warmes Creme, wolkige Flecken, Haarseite mit feinen Poren, Stockflecken."""
    rng = np.random.default_rng(saat)
    wolke = 0.55 * vnoise(rng, n, n, 4, 4) + 0.30 * vnoise(rng, n, n, 11, 11) + 0.15 * vnoise(rng, n, n, 30, 30)
    poren = vnoise(rng, n, n, 128, 128)
    bild = np.array((0.88, 0.80, 0.63)) * (0.86 + 0.20 * wolke[..., None]) * (1.0 - 0.06 * (poren[..., None] - 0.5))
    for _ in range(5):
        cx, cy = rng.random(2) * n
        r = rng.uniform(2.0, 6.0)
        yy, xx = np.mgrid[0:n, 0:n]
        dx = (xx - cx + n / 2) % n - n / 2
        dy = (yy - cy + n / 2) % n - n / 2
        d = np.sqrt(dx * dx + dy * dy)
        bild = bild * (1 - 0.18 * np.exp(-(d / r) ** 2))[..., None]
    return _fertig(bild)


def urkunde(saat=7306, b=256, h=192):
    """Pergament einer Urkunde mit Schriftzeilen in brauner Tinte (Zeilen als Wortbloecke), kachelbar ueber die ganze Urkunde gelegt (UV 0..1)."""
    bild = pergament(saat, n=256)[:h, :b].astype(float) / 255.0
    rng = np.random.default_rng(saat + 1)
    tinte = np.array((0.24, 0.15, 0.09))
    y = 18
    while y < h - 30:
        x = 16
        ende = b - 16 - (rng.integers(0, 60) if y > h - 60 else 0)
        while x < ende:
            w = int(rng.integers(8, 30))
            x1 = min(ende, x + w)
            bild[y:y + 2, x:x1] = bild[y:y + 2, x:x1] * 0.50 + tinte * 0.50
            x = x1 + int(rng.integers(4, 8))
        y += 10
    return _fertig(bild)


def muenzen(saat=7304, n=128, gold_anteil=0.55):
    """Ausgeschuetteter Muenzhaufen von oben: dicht liegende runde Muenzen (Gold und Silber), dunkle Fugen dazwischen."""
    rng = np.random.default_rng(saat)
    bild = np.zeros((n, n, 3)) + np.array((0.16, 0.12, 0.06))
    yy, xx = np.mgrid[0:n, 0:n]
    for _ in range(170):
        cx, cy = rng.random(2) * n
        r = rng.uniform(5.0, 7.5)
        dx = (xx - cx + n / 2) % n - n / 2
        dy = (yy - cy + n / 2) % n - n / 2
        sch = rng.uniform(0.55, 1.0)       # schraeg liegende Muenzen wirken oval
        d = np.sqrt(dx * dx + (dy / sch) ** 2)
        innen = d < r
        if rng.random() < gold_anteil:
            f = np.array((0.86, 0.66, 0.24))
        else:
            f = np.array((0.74, 0.74, 0.70))
        licht = 0.80 + 0.35 * np.clip(-(dx + dy) / (2 * r), -1, 1)
        rand = (d > r - 1.3) & innen
        bild[innen] = (f * licht[..., None])[innen]
        bild[rand] = bild[rand] * 0.62
    return _fertig(bild)


def leder(saat=7305, n=64, farbe=(0.42, 0.28, 0.16)):
    """Weiches Beutelleder (Geldsack): Flecken und Narbung."""
    rng = np.random.default_rng(saat)
    fl = 0.6 * vnoise(rng, n, n, 5, 5) + 0.4 * vnoise(rng, n, n, 16, 16)
    nb = vnoise(rng, n, n, 48, 48)
    bild = np.array(farbe) * (0.80 + 0.35 * fl[..., None]) * (1 + 0.15 * (nb[..., None] - 0.5))
    return _fertig(bild)


def karte_vorschau(n=512):
    """Nur fuer die Vorschaubilder (nicht im Spiel): eine einfache Platzhalter-Karte in Tinte und Farbe auf durchsichtigem Grund (RGBA)."""
    rng = np.random.default_rng(7310)
    bild = np.zeros((int(n * 0.70), n, 4))
    h, b = bild.shape[:2]
    yy, xx = np.mgrid[0:h, 0:b]
    land = vnoise(rng, h, b, 5, 4) + 0.35 * vnoise(rng, h, b, 13, 10)
    fluss = np.abs((xx / b - 0.5) - 0.12 * np.sin(yy / h * 7.0)) < 0.012
    bild[..., :3] = (0.30, 0.42, 0.22)
    bild[..., 3] = np.where(land > 0.78, 0.55, 0.0)
    wald = land > 0.95
    bild[wald, :3] = (0.18, 0.30, 0.14)
    bild[wald, 3] = 0.8
    bild[fluss, :3] = (0.20, 0.32, 0.55)
    bild[fluss, 3] = 0.9
    tinte = (0.15, 0.10, 0.06)
    for _ in range(9):
        cx, cy = rng.random() * (b - 40) + 20, rng.random() * (h - 40) + 20
        m = (np.abs(xx - cx) < 7) & (np.abs(yy - cy) < 7)
        bild[m, :3] = (0.62, 0.14, 0.10)
        bild[m, 3] = 0.95
    rahmen = (xx < 6) | (xx > b - 7) | (yy < 6) | (yy > h - 7)
    bild[rahmen, :3] = tinte
    bild[rahmen, 3] = 0.95
    return np.clip(bild * 255 + 0.5, 0, 255).astype(np.uint8)


def wappen_vorschau(n=64):
    """Nur fuer die Vorschaubilder: Wappen-Platzhalter fuer den Siegelabdruck (Ring, Schild, Kreuz), RGBA."""
    bild = np.zeros((n, n, 4))
    yy, xx = np.mgrid[0:n, 0:n]
    d = np.sqrt((xx - n / 2 + 0.5) ** 2 + (yy - n / 2 + 0.5) ** 2)
    dunkel = (0.0, 0.0, 0.0, 0.45)
    ring = (d > n * 0.40) & (d < n * 0.46)
    schild = (np.abs(xx - n / 2 + 0.5) < n * 0.20) & (yy > n * 0.25) & (yy < n * 0.62 + (n * 0.2 - np.abs(xx - n / 2 + 0.5)) * 0.8)
    kreuz = schild & ((np.abs(xx - n / 2 + 0.5) < n * 0.04) | (np.abs(yy - n * 0.45) < n * 0.04))
    bild[ring] = dunkel
    bild[schild] = (0.0, 0.0, 0.0, 0.30)
    bild[kreuz] = (1.0, 0.9, 0.8, 0.25)
    return np.clip(bild * 255 + 0.5, 0, 255).astype(np.uint8)


def png_rgba(pfad, rgba):
    """RGBA-PNG (nur Vorschau)."""
    import struct
    import zlib
    h, b, _ = rgba.shape
    roh = b"".join(b"\x00" + rgba[y].tobytes() for y in range(h))

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff)
    with open(pfad, "wb") as fh:
        fh.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", b, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(roh, 9)) + chunk(b"IEND", b""))
