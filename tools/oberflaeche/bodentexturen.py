"""Elf kachelbare Bodentexturen für die Kartenwerkstatt (Schritt 5e) – prozedural, gemalt-realistisch, 512 × 512 Pixel je Schicht.

    python tools/oberflaeche/bodentexturen.py        (braucht numpy, z. B. das Python von Blender; nutzt die Bausteine aus texturen.py)

Schreibt game/assets/texturen/boden.png: elf Schichten untereinander (512 × 5632), die Godot als Texture2DArray lädt
(boden.png.import: slices/vertical=11). Reihenfolge wie `Bodentextur` im Kern:
    0 Wiese  1 Erde  2 Fels  3 Kies  4 Waldboden  5 Sand  6 Schlamm  7 Moos  8 Lehm  9 Pflaster  10 Mist
Kanäle je Pixel: RGB Farbe (sRGB, so wie sie im Spiel erscheint), A Höhe (0 tief … 1 hoch; der Boden-Shader lässt damit
Erhabenes über Tiefes wachsen, wo zwei Texturen ineinander übergehen). Eine Schicht deckt 6 m × 6 m des Bodens.
Die Farben folgen `bodentexturen` in game/daten/katalog.json (Grundton), etwas dunkler, weil das Licht sie aufhellt.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import texturen as t  # noqa: E402  (Bausteine: rauschen, fbm, zellen, png)

N = t.N
U, V = t.U, t.V


def hexfarbe(h, dunkler=0.74):
    return np.array([int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)]) * dunkler


def mische(a, b, w):
    return a[None, None, :] * (1 - w[..., None]) + b[None, None, :] * w[..., None]


def norm(x):
    return (x - x.min()) / (x.max() - x.min() + 1e-9)


def faerben(farbe, hell, ton=None, stark=0.5):
    """Grundfarbe mal Helligkeit (0..1, Mitte 0,5) und optional zwischen zwei Tönen."""
    f = np.ones((N, N, 3)) * farbe
    f = f * (1 + (hell[..., None] - 0.5) * 2 * stark)
    return f


def koernung(saat, anteil=1.0):
    return norm(t.rauschen(1.5, saat) + 0.5 * t.rauschen(3, saat + 1)) * anteil


def wiese():
    # Gras: das Spiel malt die Wiese als Farbe (0,33 / 0,40 / 0,24); die Schicht gibt nur Halme und Flecken dazu
    g = np.array([0.33, 0.40, 0.24])
    flecken = t.fbm(11, [60, 24, 8])
    halme = t.rauschen(1.6, 12, 3.0)
    fein = koernung(13)
    hell = 0.5 + 0.30 * (flecken - 0.5) + 0.30 * (halme - 0.5) + 0.18 * (fein - 0.5)
    f = faerben(g, hell, stark=0.55)
    f[..., 0] *= 1 + 0.10 * (flecken - 0.5)      # gelbliche und bläuliche Stellen
    f[..., 2] *= 1 - 0.15 * (flecken - 0.5)
    return f, 0.55 + 0.4 * (halme - 0.5)


def erde(farbe):
    schollen = t.fbm(21, [50, 20, 8, 3])
    nummer, f1, f2, wx, wy = t.zellen(22, 22, 22, 0.9)
    krume = np.clip(f1 / 12, 0, 1)
    korn = koernung(23)
    hell = 0.5 + 0.35 * (schollen - 0.5) + 0.22 * (korn - 0.5) - 0.18 * (1 - krume) + 0.10 * (t.zufall_je(nummer, 24, 22 * 22) * 0.5)
    f = faerben(farbe, hell, stark=0.7)
    steine = (t.rauschen(1.2, 25) > 0.88)[..., None] * 0.12
    return f + steine, 0.35 + 0.5 * krume * 0.6 + 0.3 * (schollen - 0.5)


def fels(farbe):
    nummer, f1, f2, wx, wy = t.zellen(7, 7, 31, 0.9)
    riss = np.clip((f2 - f1) / 9, 0, 1)                 # 0 in der Fuge, 1 auf der Platte
    platte = t.zufall_je(nummer, 32, 49)
    grob = t.fbm(33, [70, 26, 10, 4])
    fein = t.rauschen(1.4, 34)
    hell = 0.5 + 0.30 * (grob - 0.5) + 0.12 * platte + 0.22 * (fein - 0.5) - 0.38 * (1 - riss) ** 1.5
    f = faerben(farbe, hell, stark=0.8)
    warm = (t.fbm(35, [40, 14]) - 0.5)[..., None] * np.array([0.10, 0.05, -0.05])      # rostige und kühle Streifen
    flechte = (t.rauschen(5, 36) > 0.80)[..., None] * np.array([-0.03, 0.05, -0.04])
    return f + warm + flechte, 0.2 + 0.7 * riss ** 0.7 + 0.15 * (grob - 0.5)


def kies(farbe):
    nummer, f1, f2, wx, wy = t.zellen(54, 54, 41, 0.85)
    rund = np.clip(1 - f1 / 11, 0, 1)
    ton = t.zufall_je(nummer, 42, 54 * 54)
    warm = t.zufall_je(nummer, 43, 54 * 54)
    hell = 0.5 + 0.35 * ton * 0.8 + 0.28 * (rund - 0.4) + 0.08 * (koernung(44) - 0.5)
    f = faerben(farbe, hell, stark=0.9)
    f[..., 0] *= 1 + 0.07 * warm
    f[..., 2] *= 1 - 0.09 * warm
    fuge = np.clip((f2 - f1) / 3.0, 0, 1)
    f = f * (0.55 + 0.45 * fuge[..., None])
    return f, 0.15 + 0.85 * rund * fuge


def waldboden(farbe):
    bett = t.fbm(51, [60, 22, 8])
    nadeln = t.rauschen(1.2, 52, 5.0) + 0.7 * t.rauschen(1.2, 53, 0.2)         # Streu in zwei Richtungen
    nadeln = norm(nadeln)
    laub = (t.rauschen(2.2, 54) > 0.82)
    hell = 0.5 + 0.30 * (bett - 0.5) + 0.28 * (nadeln - 0.5)
    f = faerben(farbe, hell, stark=0.9)
    moos = np.clip((t.fbm(55, [30, 10]) - 0.58) * 5, 0, 1)
    f = mische_arr(f, np.array([0.22, 0.30, 0.12]), moos * 0.35)
    streu = np.clip((t.fbm(56, [26, 9]) - 0.45) * 3, 0, 1)
    f = mische_arr(f, np.array([0.30, 0.22, 0.12]), streu * (0.35 + 0.4 * nadeln))
    f = f + laub[..., None] * np.array([0.12, 0.06, -0.02])                       # helle Blattreste
    return f, 0.4 + 0.3 * (nadeln - 0.5) + 0.2 * laub


def mische_arr(bild, farbe, w):
    return bild * (1 - w[..., None]) + np.asarray(farbe)[None, None, :] * w[..., None]


def sand(farbe):
    wellen = np.sin((V * 9 + 1.6 * (t.rauschen(60, 61) - 0.5) + 0.4 * (t.rauschen(18, 62) - 0.5)) * 2 * np.pi)
    korn = koernung(63)
    hell = 0.5 + 0.10 * wellen + 0.20 * (korn - 0.5) + 0.12 * (t.fbm(64, [60, 20]) - 0.5)
    f = faerben(farbe, hell, stark=0.55)
    return f, 0.5 + 0.25 * wellen


def schlamm(farbe):
    nummer, f1, f2, wx, wy = t.zellen(9, 9, 71, 0.9)
    riss = np.clip((f2 - f1) / 5, 0, 1)
    nass = t.fbm(72, [55, 22, 9])
    hell = 0.5 + 0.30 * (nass - 0.5) + 0.12 * (koernung(73) - 0.5) - 0.32 * (1 - riss) ** 2 * (nass > 0.45)
    f = faerben(farbe, hell, stark=0.8)
    pfuetze = np.clip((nass - 0.62) * 6, 0, 1)
    f = mische_arr(f, farbe * 0.65, pfuetze * 0.5)
    return f, 0.55 + 0.25 * (nass - 0.5) - 0.3 * (1 - riss) ** 2 * (nass > 0.45)


def moos(farbe):
    polster = t.fbm(81, [30, 12, 5, 2])
    fasern = norm(t.rauschen(1.3, 82) + 0.6 * t.rauschen(2.5, 83, 2.0))
    hell = 0.5 + 0.34 * (polster - 0.5) + 0.30 * (fasern - 0.5)
    f = faerben(farbe, hell, stark=0.95)
    f[..., 0] *= 1 + 0.12 * (t.rauschen(40, 84) - 0.5)
    glanz = (t.rauschen(1.8, 85) > 0.90)[..., None] * np.array([0.05, 0.08, 0.0])
    return f + glanz, 0.4 + 0.5 * (polster - 0.5) + 0.2 * (fasern - 0.5)


def lehm(farbe):
    schollen = t.fbm(91, [60, 24, 10])
    nummer, f1, f2, wx, wy = t.zellen(14, 14, 92, 0.95)
    riss = np.clip((f2 - f1) / 3.5, 0, 1)
    feinriss = (t.rauschen(2.5, 93) > 0.84)
    hell = 0.5 + 0.28 * (schollen - 0.5) + 0.14 * (koernung(94) - 0.5) - 0.20 * (1 - riss) ** 2 * (schollen > 0.5) - 0.10 * feinriss
    f = faerben(farbe, hell, stark=0.8)
    kiesel = (t.rauschen(1.2, 95) > 0.90)[..., None] * 0.10
    return f + kiesel, 0.5 + 0.2 * (schollen - 0.5) - 0.2 * (1 - riss) ** 2


def pflaster(farbe):
    nummer, f1, f2, wx, wy = t.zellen(11, 11, 101, 0.45)
    abstand = np.clip((f2 - f1) / 2, 0, 20)
    stein = np.clip(abstand / 5.5, 0, 1)
    kuppe = np.clip(f1 / 24, 0, 1)
    ton = t.zufall_je(nummer, 102, 121)
    warm = t.zufall_je(nummer, 103, 121)
    hell = 0.62 + 0.14 * ton + 0.10 * (koernung(104) - 0.5) - 0.12 * kuppe * stein
    f = faerben(farbe, hell, stark=1.0)
    f[..., 0] *= 1 + 0.08 * warm
    f[..., 2] *= 1 - 0.10 * warm
    fuge = np.array([0.27, 0.23, 0.17])
    f = mische_arr(f, fuge, (1 - stein) ** 1.5 * 0.85)
    return f, 0.1 + 0.9 * stein * (1 - 0.35 * kuppe)


def mist(farbe):
    haufen = t.fbm(111, [50, 18, 7])
    stroh_a = norm(t.rauschen(1.0, 112, 7.0))
    stroh_b = norm(t.rauschen(1.0, 113, 0.15))
    halm = (stroh_a > 0.72) | (stroh_b > 0.72)
    hell = 0.5 + 0.30 * (haufen - 0.5) + 0.18 * (koernung(114) - 0.5)
    f = faerben(farbe, hell, stark=0.9)
    f = mische_arr(f, np.array([0.55, 0.44, 0.20]), halm * (0.55 + 0.35 * haufen))
    nass = np.clip((haufen - 0.62) * 5, 0, 1)
    f = f * (1 - 0.3 * nass[..., None])
    return f, 0.4 + 0.3 * (haufen - 0.5) + 0.3 * halm


def alle():
    katalog = {"erde": "#7a5c3e", "fels": "#8a8580", "kies": "#a59c8a", "waldboden": "#4f5a32", "sand": "#c9b27e",
               "schlamm": "#5a4e36", "moos": "#5f7a3a", "lehm": "#a07a50", "pflaster": "#76736c", "mist": "#5b4a2e"}
    f = {k: hexfarbe(v) for k, v in katalog.items()}
    return [wiese(), erde(f["erde"]), fels(f["fels"]), kies(f["kies"]), waldboden(f["waldboden"]), sand(f["sand"]),
            schlamm(f["schlamm"]), moos(f["moos"]), lehm(f["lehm"]), pflaster(f["pflaster"]), mist(f["mist"])]


def main():
    schichten = []
    for farbe, hoehe in alle():
        rgb = np.clip(farbe, 0, 1)
        a = np.clip(hoehe, 0, 1)
        schichten.append(np.dstack([np.round(rgb * 255), np.round(a * 255)]).astype(np.uint8))
    bild = np.vstack(schichten)
    pfad = os.path.join(t.ZIEL, "boden.png")
    t.png(pfad, bild)
    print("geschrieben:", os.path.normpath(pfad), f"({os.path.getsize(pfad) // 1024} KB)")
    for name, s in zip("wiese erde fels kies waldboden sand schlamm moos lehm pflaster mist".split(), schichten):
        print(f"{name:10s} mittel", s[..., :3].reshape(-1, 3).mean(axis=0).round(0))


if __name__ == "__main__":
    main()
