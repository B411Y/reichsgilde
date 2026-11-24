#!/usr/bin/env python3
"""Bodenflecke je Gebaeudeart (Decal unter den Haeusern, Stadtansicht.Bodenfleck.cs).

Aufruf (Python 3 mit Pillow und numpy, kein Blender):
    python3 tools/modelle/bodenflecke/bodenflecke.py <ordner mit den Quellbildern> game/assets/texturen/bodenflecke

Die Quellbilder sind fremde CC0-Texturen von Poly Haven (1K-JPG, nur die Farbe, <asset>_diff_1k.jpg von
api.polyhaven.com/files/<asset>; nie im Repository). Namen, Urheber und Pruefsummen: game/assets/texturen/bodenflecke/HERKUNFT.md.

Jedes Bild ist 512 x 512, sRGB mit Alpha. Das Decal zieht es ueber die Grundflaeche des Hauses plus Rand; die Mitte liegt
unter dem Haus, zu sehen ist der Ring darum (etwa das aeussere Drittel; der weiche Rand ist darum schmal). Grobe Formen mit klarem Hell-Dunkel (Flecken ab etwa einem Meter), damit sie
im Pixel-Look lesbar bleiben; die Zeichnung der Aufnahme liegt nur fein darueber. Alles ist fest gesaet: gleiche Quellen
geben gleiche Bilder.
"""
import os
import sys

import numpy as np
from PIL import Image

KANTE = 512


def quelle(ordner, name):
    """Die Farbe einer Quelle als Feld 512 x 512 x 3 (0..1), aus der Mitte geschnitten und verkleinert."""
    bild = Image.open(os.path.join(ordner, name + "_diff_1k.jpg")).convert("RGB").resize((KANTE, KANTE), Image.LANCZOS)
    return np.asarray(bild, dtype=np.float32) / 255.0


def rauschen(saat, zellen, oktaven=3):
    """Weiches Rauschen 0..1 (512 x 512): Zufallsgitter mit `zellen` Feldern je Kante, bikubisch vergroessert, je Oktave doppelt so fein."""
    zufall = np.random.default_rng(saat)
    summe = np.zeros((KANTE, KANTE), dtype=np.float32)
    gewicht, gesamt = 1.0, 0.0
    for _ in range(oktaven):
        gitter = (zufall.random((zellen + 3, zellen + 3)) * 255).astype(np.uint8)
        gross = Image.fromarray(gitter).resize((KANTE * (zellen + 3) // zellen, KANTE * (zellen + 3) // zellen), Image.BICUBIC)
        summe += np.asarray(gross, dtype=np.float32)[:KANTE, :KANTE] / 255.0 * gewicht
        gesamt += gewicht
        gewicht *= 0.5
        zellen *= 2
    feld = summe / gesamt
    return (feld - feld.min()) / max(1e-6, feld.max() - feld.min())


def stufe(feld, unten, oben):
    """Weiche Schwelle (smoothstep): 0 unter `unten`, 1 ueber `oben`."""
    t = np.clip((feld - unten) / max(1e-6, oben - unten), 0, 1)
    return t * t * (3 - 2 * t)


def randabstand():
    """Abstand zum Bildrand, 0 am Rand bis 1 in der Mitte, mit runden Ecken."""
    achse = (np.arange(KANTE, dtype=np.float32) + 0.5) / KANTE
    x, y = np.meshgrid(np.abs(achse - 0.5) * 2, np.abs(achse - 0.5) * 2)
    ecke = 0.25  # Rundung der Ecken, Anteil der halben Kante
    dx, dy = np.maximum(x - (1 - ecke), 0), np.maximum(y - (1 - ecke), 0)
    aussen = np.sqrt(dx * dx + dy * dy)
    innen = np.minimum(np.maximum(x, y) - (1 - ecke), 0)
    return np.clip(-(aussen + innen - ecke), 0, 1)  # 0 am Rand, 1 in der Mitte


def faerben(bild, ziel, kontrast=1.0):
    """Faerbt eine Aufnahme auf eine Zielfarbe um: Die Helligkeitszeichnung bleibt, der Farbton kommt vom Ziel."""
    hell = bild.mean(axis=2)
    hell = (hell - hell.mean()) * kontrast
    return np.clip(np.asarray(ziel, dtype=np.float32)[None, None, :] * (1 + hell[..., None] * 2.2), 0, 1)


def mischen(unten, oben, maske):
    return unten * (1 - maske[..., None]) + oben * maske[..., None]


def alpha_rand(saat, breite=0.09, unruhe=0.06):
    """Die Deckung: voll in der Mitte, zum Rand weich und unregelmaessig aus (Rand als Anteil der halben Kante)."""
    abstand = randabstand() + (rauschen(saat, 5) - 0.5) * unruhe * 2
    return stufe(abstand, 0.02, 0.02 + breite)


def speichern(ziel, name, farbe, alpha):
    daten = np.dstack([farbe, alpha[..., None]])
    Image.fromarray((np.clip(daten, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(ziel, name), optimize=True)
    print("geschrieben:", name)


def wohnhaus(q, ziel):
    """Wohnhaus: heller Traufkies dicht am Haus, aussen dunkle festgetretene Erde."""
    erde = faerben(quelle(q, "brown_mud_dry"), (0.33, 0.27, 0.20), 0.9)
    kies = faerben(quelle(q, "gravel_floor_02"), (0.55, 0.52, 0.45), 0.9)
    abstand = randabstand() + (rauschen(11, 7) - 0.5) * 0.10
    band = stufe(abstand, 0.30, 0.36)                                  # Kies ab etwa einem Drittel der halben Kante nach innen
    streu = stufe(rauschen(12, 14, 2), 0.66, 0.74) * stufe(abstand, 0.10, 0.18) * 0.8   # verschleppte Kieselnester in der Erde
    farbe = mischen(erde, kies, np.maximum(band, streu))
    farbe *= (1 - 0.30 * stufe(rauschen(13, 4), 0.55, 0.80) * (1 - band))[..., None]    # dunkle, feuchte Stellen in der Erde
    speichern(ziel, "bodenfleck_wohnhaus.png", farbe, alpha_rand(14))


def schmiede(q, ziel):
    """Schmiede: schwarzer Kohlestaub in grossen Flecken, dazwischen helle Asche, wenig Erde."""
    brand = quelle(q, "burned_ground_01")
    erde = faerben(quelle(q, "brown_mud_dry"), (0.27, 0.23, 0.19), 0.8)
    kohle = faerben(brand, (0.075, 0.07, 0.068), 1.2)
    asche = faerben(brand, (0.52, 0.505, 0.48), 0.7)
    r = rauschen(21, 4)
    farbe = mischen(erde, kohle, stufe(r, 0.38, 0.50))                 # gut die Haelfte der Flaeche ist Kohlestaub
    farbe = mischen(farbe, asche, stufe(rauschen(22, 6), 0.66, 0.72))  # Aschehaufen mit fester Kante
    farbe = mischen(farbe, kohle, stufe(rauschen(23, 20, 2), 0.70, 0.76) * 0.85)  # Kohlebrocken
    speichern(ziel, "bodenfleck_schmiede.png", farbe, alpha_rand(24))


def hof(q, ziel):
    """Hof: dunkler nasser Matsch, darauf grosse Flecken helles Stroh, dazu Pfuetzen."""
    matsch = faerben(quelle(q, "brown_mud_03"), (0.25, 0.19, 0.13), 1.2)
    stroh = faerben(quelle(q, "thatch_roof_angled"), (0.62, 0.52, 0.30), 1.3)
    r = rauschen(31, 4)
    halme = rauschen(32, 40, 1)                                        # ausgefranster Rand der Strohflecken
    farbe = matsch * (1 - 0.45 * stufe(rauschen(33, 5), 0.62, 0.72))[..., None]   # Pfuetzen: fast schwarz
    farbe = mischen(farbe, stroh, stufe(r + (halme - 0.5) * 0.16, 0.60, 0.67))
    speichern(ziel, "bodenfleck_hof.png", farbe, alpha_rand(34, breite=0.14, unruhe=0.10))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    os.makedirs(sys.argv[2], exist_ok=True)
    for bauen in (wohnhaus, schmiede, hof):
        bauen(sys.argv[1], sys.argv[2])
