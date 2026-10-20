"""Kachelbare Texturen für den Holzschnitt-Look – alles prozedural erzeugt, nahtlos, 256 × 256 Pixel.

    python tools/holzschnitt/texturen.py        (braucht numpy und Pillow)

Schreibt nach game/assets/holzschnitt/ (Linien schwarz = 0 auf weiß = 1):
    schraffur.png    R einfache Schraffur /, G Gegenschraffur \\, B dichte Waagrechte (tiefer Schatten)
    papier.png       R Papierkorn, G Farbflecken (wo die Farbe dünner sitzt), B Bodentupfen (Erde, Gras)
    struktur_a.png   R Holzmaserung, G Bretter, B Mauerquader, A Fels
    struktur_b.png   R Dachziegel, G Stroh, B Schiefer, A Laub
Welche Materialart welchen Kanal nimmt, steht in materialarten.json daneben.
"""
import os
import numpy as np
from PIL import Image

N = 256
ZIEL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "game", "assets", "holzschnitt")
y, x = np.mgrid[0:N, 0:N] / N          # 0..1 – Muster mit ganzen Perioden sind kachelbar


def rauschen(grob, saat):
    """Kachelbares Rauschen (Zufallsspektrum, tiefpassgefiltert), 0..1."""
    r = np.random.default_rng(saat)
    f = np.fft.fftfreq(N)
    fx, fy = np.meshgrid(f, f)
    z = np.fft.ifft2(np.fft.fft2(r.standard_normal((N, N))) * np.exp(-(fx ** 2 + fy ** 2) * grob ** 2)).real
    return (z - z.min()) / (z.max() - z.min())


def linien(phase, perioden, breite, wackel=0.0, saat=0):
    """Linien (0) auf Weiß (1); `breite` als Anteil der Periode, Dicke und Lage leicht unruhig wie geschnitten."""
    t = (phase * perioden + (rauschen(40, saat) - 0.5) * wackel) % 1.0
    abstand = np.minimum(t, 1 - t)
    halb = breite / 2 * (0.7 + 0.6 * rauschen(25, saat + 1))
    return np.clip((abstand - halb) / 0.05, 0, 1)


def fugen(u, v, reihen, spalten, breite_h, breite_v, versatz=0.5, saat=0):
    """Verband aus Reihen (Fugen 0): jede zweite Reihe um `versatz` verschoben, Fugen leicht unruhig."""
    w = (rauschen(30, saat) - 0.5) * 0.06
    zeile = np.floor(v * reihen)
    uu = (u * spalten + versatz * (zeile % 2) + w * spalten) % 1.0
    vv = (v * reihen + w * reihen * 0.5) % 1.0
    return np.where((np.minimum(vv, 1 - vv) < breite_h / 2) | (np.minimum(uu, 1 - uu) < breite_v / 2), 0.0, 1.0)


def speichern(name, *kanaele):
    os.makedirs(ZIEL, exist_ok=True)
    feld = np.dstack(kanaele) if len(kanaele) > 1 else kanaele[0]
    Image.fromarray((np.clip(feld, 0, 1) * 255).astype(np.uint8)).save(os.path.join(ZIEL, name), optimize=True)


def main():
    rng = np.random.default_rng(7)
    # Schraffur
    speichern("schraffur.png", linien(x + y, 16, 0.32, 0.35, 1), linien(x - y, 16, 0.32, 0.35, 2), linien(y, 48, 0.42, 0.25, 3))
    # Papier und Boden
    korn = 0.75 * rauschen(3, 4) + 0.25 * rng.random((N, N))
    flecken = rauschen(60, 5)
    tupfen = np.ones((N, N))
    for _ in range(420):                       # kurze Striche wie Grasbüschel und Erdkrumen
        cx, cy, l = rng.integers(0, N), rng.integers(0, N), rng.integers(3, 8)
        for k in range(l):
            tupfen[(cy - k) % N, (cx + k // 3) % N] = 0.0
    speichern("papier.png", korn, flecken, tupfen)
    # Struktur A: Holz, Bretter, Mauer, Fels
    holz = linien(x + 0.03 * np.sin(2 * np.pi * (y * 2 + rauschen(50, 8))), 10, 0.12, 0.8, 9)
    bretter = np.minimum(linien(x, 6, 0.10, 0.15, 11), 0.35 + 0.65 * linien(x * 6 + 0.05 * np.sin(2 * np.pi * y * 3), 4, 0.12, 0.6, 12))
    mauer = fugen(x, y, 4, 3, 0.12, 0.06, saat=10)
    # Fels: wenige lange Risse, schräg und gebrochen, statt Höhenlinien
    riss1 = linien(x * 0.8 + y * 0.6 + 0.25 * rauschen(70, 13), 3, 0.035, 0.0, 14)
    riss2 = linien(x * 0.3 - y * 0.9 + 0.3 * rauschen(60, 15), 2, 0.03, 0.0, 16)
    luecken = rauschen(40, 22) > 0.45                                # Risse nur stückweise, wie mit dem Messer gesetzt
    fels = np.where(luecken, np.minimum(riss1, riss2), 1.0)
    speichern("struktur_a.png", holz, bretter, mauer, fels)
    # Struktur B: Ziegel (Biberschwanz: Reihen mit runden Unterkanten), Stroh, Schiefer, Laub
    zeile = np.floor(y * 8)
    uu = (x * 8 + 0.5 * (zeile % 2)) % 1.0
    vv = (y * 8) % 1.0
    kante = 0.62 + 0.30 * (1 - (2 * uu - 1) ** 2)              # runde Unterkante jedes Ziegels (Mitte am tiefsten)
    ziegel = np.where(np.abs(vv - kante) < 0.045, 0.0, 1.0) * np.where((np.minimum(uu, 1 - uu) < 0.025) & (vv < kante), 0.0, 1.0)
    halme = linien(x + 0.015 * np.sin(2 * np.pi * (y * 2 + rauschen(30, 17))), 28, 0.10, 0.5, 18)
    stroh = np.minimum(0.45 + 0.55 * halme, linien(y, 4, 0.05, 0.3, 20))      # feine Halme, kräftige Bindelagen
    schiefer = fugen(x, y, 10, 5, 0.10, 0.07, versatz=0.4, saat=21)
    laub = np.ones((N, N))
    for _ in range(480):                       # kleine Bögen wie geschnittene Blätter
        cx, cy, r = rng.uniform(0, N), rng.uniform(0, N), rng.uniform(4, 8)
        for w in np.linspace(0.15, 0.85, 12) * np.pi:
            laub[int(cy - r * np.sin(w)) % N, int(cx + r * np.cos(w)) % N] = 0.0
    speichern("struktur_b.png", ziegel, stroh, schiefer, laub)
    print("geschrieben nach", os.path.normpath(ZIEL))


if __name__ == "__main__":
    main()
