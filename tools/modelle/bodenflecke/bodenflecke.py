#!/usr/bin/env python3
"""Bodenflecke je Gebaeudeart (Decals unter den Haeusern, Stadtansicht.Bodenfleck.cs).

Aufruf (Python 3 mit Pillow und numpy, kein Blender):
    python3 tools/modelle/bodenflecke/bodenflecke.py <ordner mit den Quellbildern> game/assets/texturen/bodenflecke

Die Quellbilder sind fremde CC0-Texturen von Poly Haven (1K-JPG, nur die Farbe, <asset>_diff_1k.jpg von
api.polyhaven.com/files/<asset>; nie im Repository). Namen, Urheber und Pruefsummen: game/assets/texturen/bodenflecke/HERKUNFT.md.

Je Art zwei Bilder, bodenfleck_<art>_klein.png und bodenfleck_<art>_gross.png, je 512 x 512, sRGB mit Alpha. Das Decal zieht
das Bild ueber die Grundflaeche des Hauses plus Rand; die Mitte liegt unter dem Haus, zu sehen ist der Ring darum. Damit der
Ring in Metern etwa gleich breit bleibt, hat das grosse Bild (Flaechen ab etwa 13 m Kante) alles halb so gross: Rand,
Baender, Flecken und die Zeichnung der Aufnahme. Grobe Formen mit klarem Hell-Dunkel, damit sie im Pixel-Look lesbar
bleiben. Alles ist fest gesaet: gleiche Quellen geben gleiche Bilder.
"""
import os
import sys

import numpy as np
from PIL import Image

KANTE = 512
K = 1  # Groessenklasse des Bildes, das gerade entsteht: 1 = klein, 2 = gross (alles halb so gross)
QUELLEN = ""


def quelle(name):
    """Die Farbe einer Quelle als Feld 512 x 512 x 3 (0..1); im grossen Bild K x K-mal gekachelt (die Aufnahmen sind nahtlos)."""
    teil = KANTE // K
    bild = Image.open(os.path.join(QUELLEN, name + "_diff_1k.jpg")).convert("RGB").resize((teil, teil), Image.LANCZOS)
    return np.tile(np.asarray(bild, dtype=np.float32) / 255.0, (K, K, 1))


def rauschen(saat, zellen, oktaven=3):
    """Weiches Rauschen 0..1 (512 x 512): Zufallsgitter mit `zellen` Feldern je Kante (im grossen Bild doppelt so viele),
    bikubisch vergroessert, je Oktave doppelt so fein."""
    zufall = np.random.default_rng(saat * 10 + K)
    zellen *= K
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
    """Abstand zum Bildrand in Einheiten des kleinen Bildes: 0 am Rand, 1 in der Mitte des kleinen Bildes (im grossen 2),
    mit runden Ecken. Ein Band bei 0.3 liegt damit in beiden Klassen etwa gleich viele Meter vom Rand."""
    achse = (np.arange(KANTE, dtype=np.float32) + 0.5) / KANTE
    x, y = np.meshgrid(np.abs(achse - 0.5) * 2, np.abs(achse - 0.5) * 2)
    ecke = 0.25 / K  # Rundung der Ecken, Anteil der halben Kante
    dx, dy = np.maximum(x - (1 - ecke), 0), np.maximum(y - (1 - ecke), 0)
    aussen = np.sqrt(dx * dx + dy * dy)
    innen = np.minimum(np.maximum(x, y) - (1 - ecke), 0)
    return np.clip(-(aussen + innen - ecke), 0, 1) * K


def faerben(bild, ziel, kontrast=1.0, eigenfarbe=0.0):
    """Faerbt eine Aufnahme auf eine Zielfarbe um: Die Helligkeitszeichnung bleibt, der Farbton kommt vom Ziel;
    `eigenfarbe` 0..1 laesst so viel von der Farbe der Aufnahme stehen."""
    hell = bild.mean(axis=2)
    hell = (hell - hell.mean()) * kontrast
    neu = np.clip(np.asarray(ziel, dtype=np.float32)[None, None, :] * (1 + hell[..., None] * 2.2), 0, 1)
    return neu * (1 - eigenfarbe) + bild * eigenfarbe


def mischen(unten, oben, maske):
    return unten * (1 - maske[..., None]) + oben * maske[..., None]


def alpha_rand(saat, breite=0.09, unruhe=0.06):
    """Die Deckung: voll in der Mitte, zum Rand weich und unregelmaessig aus (Masse wie randabstand)."""
    abstand = randabstand() + (rauschen(saat, 5) - 0.5) * unruhe * 2
    return stufe(abstand, 0.02, 0.02 + breite)


def speichern(ziel, art, farbe, alpha):
    name = "bodenfleck_%s_%s.png" % (art, "klein" if K == 1 else "gross")
    daten = np.dstack([farbe, alpha[..., None]])
    Image.fromarray((np.clip(daten, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(ziel, name), optimize=True)
    print("geschrieben:", name)


def wohnhaus(ziel):
    """Wohnhaus: heller Traufkies dicht am Haus, aussen dunkle festgetretene Erde."""
    erde = faerben(quelle("brown_mud_dry"), (0.33, 0.27, 0.20), 0.9)
    kies = faerben(quelle("gravel_floor_02"), (0.55, 0.52, 0.45), 0.9)
    abstand = randabstand() + (rauschen(11, 7) - 0.5) * 0.10
    band = stufe(abstand, 0.30, 0.36)                                  # Kies ab etwa einem Drittel der halben Kante nach innen
    streu = stufe(rauschen(12, 14, 2), 0.66, 0.74) * stufe(abstand, 0.10, 0.18) * 0.8   # verschleppte Kieselnester in der Erde
    farbe = mischen(erde, kies, np.maximum(band, streu))
    farbe *= (1 - 0.30 * stufe(rauschen(13, 4), 0.55, 0.80) * (1 - band))[..., None]    # dunkle, feuchte Stellen in der Erde
    speichern(ziel, "wohnhaus", farbe, alpha_rand(14))


def schmiede(ziel):
    """Schmiede: schwarzer Kohlestaub in grossen Flecken, dazwischen helle Asche, wenig Erde."""
    brand = quelle("burned_ground_01")
    erde = faerben(quelle("brown_mud_dry"), (0.27, 0.23, 0.19), 0.8)
    kohle = faerben(brand, (0.075, 0.07, 0.068), 1.2)
    asche = faerben(brand, (0.52, 0.505, 0.48), 0.7)
    farbe = mischen(erde, kohle, stufe(rauschen(21, 4), 0.38, 0.50))   # gut die Haelfte der Flaeche ist Kohlestaub
    farbe = mischen(farbe, asche, stufe(rauschen(22, 6), 0.66, 0.72))  # Aschehaufen mit fester Kante
    farbe = mischen(farbe, kohle, stufe(rauschen(23, 20, 2), 0.70, 0.76) * 0.85)  # Kohlebrocken
    speichern(ziel, "schmiede", farbe, alpha_rand(24))


def hof(ziel):
    """Hof und Muehle: dunkler nasser Matsch, darauf grosse Flecken helles Stroh, dazu Pfuetzen."""
    matsch = faerben(quelle("brown_mud_03"), (0.25, 0.19, 0.13), 1.2)
    stroh = faerben(quelle("thatch_roof_angled"), (0.62, 0.52, 0.30), 1.3)
    halme = rauschen(32, 40, 1)                                        # ausgefranster Rand der Strohflecken
    farbe = matsch * (1 - 0.45 * stufe(rauschen(33, 5), 0.62, 0.72))[..., None]   # Pfuetzen: fast schwarz
    farbe = mischen(farbe, stroh, stufe(rauschen(31, 4) + (halme - 0.5) * 0.16, 0.60, 0.67))
    speichern(ziel, "hof", farbe, alpha_rand(34, breite=0.14, unruhe=0.10))


def schreiner(ziel):
    """Schreiner: helle Hobelspaene in breiten Wehen auf Erde, dazu dunkles Saegemehl."""
    erde = faerben(quelle("brown_mud_dry"), (0.31, 0.26, 0.20), 0.8)
    spaene = faerben(quelle("wood_chips"), (0.74, 0.60, 0.38), 1.6, eigenfarbe=0.15)
    mehl = faerben(quelle("wood_chips"), (0.48, 0.36, 0.22), 0.8)
    farbe = mischen(erde, mehl, stufe(rauschen(41, 5), 0.45, 0.60))
    farbe = mischen(farbe, spaene, stufe(rauschen(42, 4) + (rauschen(43, 30, 1) - 0.5) * 0.12, 0.52, 0.58))
    speichern(ziel, "schreiner", farbe, alpha_rand(44))


def steinmetz(ziel):
    """Steinmetz: heller Steinstaub flaechig, darin grober Splitt und dunkle Erdinseln."""
    erde = faerben(quelle("brown_mud_dry"), (0.30, 0.26, 0.21), 0.8)
    staub = faerben(quelle("brown_mud_dry"), (0.70, 0.68, 0.62), 0.5)
    splitt = faerben(quelle("gravel_floor_02"), (0.60, 0.59, 0.56), 1.5)
    farbe = mischen(erde, staub, stufe(rauschen(51, 4), 0.36, 0.46))
    farbe = mischen(farbe, splitt, stufe(rauschen(52, 6), 0.58, 0.64))
    speichern(ziel, "steinmetz", farbe, alpha_rand(54))


def wirt(ziel):
    """Wirt: nasse dunkle Erde mit Lachen, darauf zertretenes Stroh in Streifen."""
    nass = faerben(quelle("brown_mud_03"), (0.20, 0.16, 0.12), 1.0)
    stroh = faerben(quelle("thatch_roof_angled"), (0.55, 0.47, 0.28), 1.1)
    farbe = nass * (1 - 0.5 * stufe(rauschen(61, 6), 0.60, 0.68))[..., None]
    farbe = mischen(farbe, stroh, stufe(rauschen(62, 5) + (rauschen(63, 36, 1) - 0.5) * 0.2, 0.56, 0.64) * 0.9)
    speichern(ziel, "wirt", farbe, alpha_rand(64))


def dunkel(ziel):
    """Alchimist und Parfumeur: fleckig-dunkler Grund - ausgegossene Suede, Russ, verfaerbte Erde."""
    erde = faerben(quelle("brown_mud_dry"), (0.26, 0.22, 0.18), 0.8)
    russ = faerben(quelle("burned_ground_01"), (0.10, 0.095, 0.09), 1.0)
    gruen = faerben(quelle("brown_mud_03"), (0.21, 0.22, 0.16), 0.9)   # gruenliche Lache
    rot = faerben(quelle("brown_mud_03"), (0.29, 0.19, 0.15), 0.9)     # rostrote Lache
    hell = faerben(quelle("brown_mud_dry"), (0.58, 0.55, 0.47), 0.6)   # Kalk- und Salzraender
    farbe = mischen(erde, russ, stufe(rauschen(71, 4), 0.50, 0.60))
    farbe = mischen(farbe, gruen, stufe(rauschen(72, 6), 0.68, 0.73))
    farbe = mischen(farbe, rot, stufe(rauschen(73, 6), 0.71, 0.76))
    farbe = mischen(farbe, hell, stufe(rauschen(74, 8), 0.74, 0.78))
    speichern(ziel, "dunkel", farbe, alpha_rand(75))


def steinplatten(ziel):
    """Dom, Rathaus, Kirchen, Stapelhaus: alter gepflasterter Vorplatz, nah an den Farben der Aufnahme, am Rand Erde."""
    pflaster = faerben(quelle("cobblestone_large_01"), (0.50, 0.48, 0.44), 1.2, eigenfarbe=0.45)  # etwas grauer als die Aufnahme
    erde = faerben(quelle("brown_mud_dry"), (0.36, 0.30, 0.23), 0.8)
    abstand = randabstand() + (rauschen(81, 7) - 0.5) * 0.10
    farbe = mischen(erde, pflaster, stufe(abstand, 0.10, 0.16))        # ganz aussen laeuft das Pflaster in Erde aus
    speichern(ziel, "steinplatten", farbe, alpha_rand(84, breite=0.06, unruhe=0.04))


def marktstroh(ziel):
    """Marktstaende: zertretenes, schmutziges Stroh, duenn und loechrig (das Pflaster des Marktes blickt durch)."""
    stroh = faerben(quelle("thatch_roof_angled"), (0.58, 0.49, 0.29), 1.2)
    schmutz = faerben(quelle("brown_mud_03"), (0.27, 0.21, 0.15), 1.0)
    farbe = mischen(stroh, schmutz, stufe(rauschen(91, 6), 0.52, 0.62))
    luecken = stufe(rauschen(92, 7) + (rauschen(93, 36, 1) - 0.5) * 0.2, 0.34, 0.46)
    speichern(ziel, "marktstroh", farbe, alpha_rand(94, breite=0.20, unruhe=0.14) * luecken)


def saum(ziel):
    """Erdsaum am Fuss eines Sockels: dunkle, feuchte Erde mit einzelnen Steinen, weit und weich auslaufend."""
    erde = faerben(quelle("brown_mud_03"), (0.23, 0.19, 0.14), 1.1)
    steine = faerben(quelle("gravel_floor_02"), (0.50, 0.48, 0.43), 1.2)
    farbe = mischen(erde, steine, stufe(rauschen(101, 12, 2), 0.76, 0.80) * 0.8)
    speichern(ziel, "saum", farbe, alpha_rand(104, breite=0.16, unruhe=0.10))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    QUELLEN = sys.argv[1]
    os.makedirs(sys.argv[2], exist_ok=True)
    for K in (1, 2):
        for bauen in (wohnhaus, schmiede, hof, schreiner, steinmetz, wirt, dunkel, steinplatten, marktstroh, saum):
            bauen(sys.argv[2])
