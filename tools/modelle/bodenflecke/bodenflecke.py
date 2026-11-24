#!/usr/bin/env python3
"""Bodenflecke je Gebaeudeart (Decals unter den Haeusern, Stadtansicht.Bodenfleck.cs), Fassung vom 24.11.2026 (zweite Runde).

Aufruf (Python 3 mit Pillow und numpy, kein Blender):
    python3 tools/modelle/bodenflecke/bodenflecke.py <ordner mit den Quellbildern> game/assets/texturen/bodenflecke

Die Quellbilder sind fremde CC0-Texturen von Poly Haven (1K-JPG, nur die Farbe, <asset>_diff_1k.jpg von
api.polyhaven.com/files/<asset>; nie im Repository). Namen, Urheber und Pruefsummen: game/assets/texturen/bodenflecke/HERKUNFT.md.

Je Art zwei Bilder, bodenfleck_<art>_klein.png und bodenfleck_<art>_gross.png, je 512 x 512, sRGB mit Alpha. Das Decal zieht
das Bild ueber die Grundflaeche des Hauses plus Rand; die Mitte liegt unter dem Haus, zu sehen ist der Ring darum.

Aufbau (anders als die erste Runde, die ein Foto auf die Flaeche streckte und daraus harte Hell-Dunkel-Flecken schnitt):
  * Das Foto liefert nur die feine Zeichnung (Hochpass, ohne die grossen Hell-Dunkel-Flaechen) und wird in fester Metergroesse
    gekachelt (etwa 3 m je Kachel in beiden Klassen; das grosse Bild deckt doppelt so viel Flaeche, hat also doppelt so viele Kacheln).
  * Die Helligkeit wird auf 3 Stufen vereinfacht (dunkel, mittel, hell, weich ineinander) und nur wenig von der Grundfarbe abgesetzt.
  * Die Grundfarbe ist je Art ein gedaempfter Ton nahe am Stadtlehm (LEHM), nie weiss oder reinschwarz.
  * Wenige kleine Koerner in einer Akzentfarbe (Schlacke, Kies, Spaene, Stroh) statt grosser Flecken.
  * Die Deckung ist eine weiche, unregelmaessige Randmaske.
Alles ist fest gesaet: gleiche Quellen geben gleiche Bilder.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

KANTE = 512
K = 1  # Groessenklasse des Bildes, das gerade entsteht: 1 = klein, 2 = gross (doppelt so viel Flaeche je Bild)
QUELLEN = ""
KACHELN = 4  # Kacheln je Kante im kleinen Bild (etwa 3 m); im grossen K-mal so viele
LEHM = np.array([0.47, 0.37, 0.28], dtype=np.float32)  # der Stadtlehm, an dem sich alle Toene ausrichten


def feinzeichnung(name, kacheln):
    """Die feine Zeichnung einer Aufnahme (Hochpass der Helligkeit, Mittel 0, Streuung 1), `kacheln`-mal gekachelt als Feld 512 x 512."""
    bild = Image.open(os.path.join(QUELLEN, name + "_diff_1k.jpg")).convert("L").resize((256, 256), Image.LANCZOS)
    dreifach = Image.fromarray(np.tile(np.asarray(bild), (3, 3)))
    weich = np.asarray(dreifach.filter(ImageFilter.GaussianBlur(10)), dtype=np.float32)[256:512, 256:512]
    fein = np.asarray(bild, dtype=np.float32) - weich
    fein /= max(1e-6, fein.std())
    teil = KANTE // kacheln
    klein = np.asarray(Image.fromarray(fein).resize((teil, teil), Image.BILINEAR), dtype=np.float32)
    return np.tile(klein, (kacheln, kacheln))[:KANTE, :KANTE]


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
    mit runden Ecken."""
    achse = (np.arange(KANTE, dtype=np.float32) + 0.5) / KANTE
    x, y = np.meshgrid(np.abs(achse - 0.5) * 2, np.abs(achse - 0.5) * 2)
    ecke = 0.25 / K  # Rundung der Ecken, Anteil der halben Kante
    dx, dy = np.maximum(x - (1 - ecke), 0), np.maximum(y - (1 - ecke), 0)
    aussen = np.sqrt(dx * dx + dy * dy)
    innen = np.minimum(np.maximum(x, y) - (1 - ecke), 0)
    return np.clip(-(aussen + innen - ecke), 0, 1) * K


def alpha_rand(saat, breite=0.12, unruhe=0.07):
    """Die Deckung: voll in der Mitte, zum Rand weich und unregelmaessig aus (Masse wie randabstand)."""
    abstand = randabstand() + (rauschen(saat, 5) - 0.5) * unruhe * 2
    return stufe(abstand, 0.02, 0.02 + breite)


def drei_stufen(feld, weiche=0.35):
    """Vereinfacht ein Feld (Mittel 0, Streuung 1) auf drei Helligkeitsstufen -1, 0, +1, an den Uebergaengen weich."""
    hoch = stufe(feld, 0.55 - weiche / 2, 0.55 + weiche / 2)
    tief = stufe(-feld, 0.55 - weiche / 2, 0.55 + weiche / 2)
    return hoch - tief


def grund(saat, quelle_name, farbe, spanne=0.10, grob=0.5):
    """Ein Grund aus drei Helligkeitsstufen um `farbe`: feine Zeichnung der Aufnahme plus etwas grobes Rauschen, `spanne` = Abstand der Stufen."""
    farbe = np.asarray(farbe, dtype=np.float32)
    fein = feinzeichnung(quelle_name, KACHELN * K)
    wolke = (rauschen(saat, 4, 2) - 0.5) * 3.0
    stufen = drei_stufen(fein * (1 - grob) + wolke * grob)
    return farbe[None, None, :] * (1 + stufen[..., None] * spanne)


def koerner(saat, anteil, zellen=70, oktaven=1):
    """Eine Maske kleiner Koerner: etwa `anteil` der Flaeche, mit hartem Rand (Pixel-Look)."""
    feld = rauschen(saat, zellen, oktaven)
    grenze = np.quantile(feld, 1 - anteil)
    return (feld >= grenze).astype(np.float32)


def auftragen(farbe, maske, ton, deckung=0.75):
    """Legt eine Koernermaske in der Farbe `ton` auf den Grund."""
    m = (maske * deckung)[..., None]
    return farbe * (1 - m) + np.asarray(ton, dtype=np.float32)[None, None, :] * m


def speichern(ziel, art, farbe, alpha):
    name = "bodenfleck_%s_%s.png" % (art, "klein" if K == 1 else "gross")
    daten = np.dstack([farbe, alpha[..., None]])
    Image.fromarray((np.clip(daten, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(ziel, name), optimize=True)
    print("geschrieben:", name)


def ring(abstand_von, abstand_bis):
    """Ein Band im Abstand zum Bildrand (wie randabstand, kleines Bild = 1)."""
    a = randabstand()
    return stufe(a, abstand_von, abstand_von + 0.08) * (1 - stufe(a, abstand_bis, abstand_bis + 0.08))


def wohnhaus(ziel):
    f = grund(11, "brown_mud_dry", LEHM * 0.95, 0.09)
    f = auftragen(f, koerner(12, 0.035) * ring(0.0, 0.5), (0.58, 0.52, 0.44), 0.6)  # wenig heller Kies nahe der Traufe
    speichern(ziel, "wohnhaus", f, alpha_rand(14))


def schmiede(ziel):
    # gleichmaessig dunkelgrauer Kohlestaub, wenige Schlackenkoerner (dunkel, leicht rostbraun oder blaugrau), kein Weiss
    f = grund(21, "burned_ground_01", (0.20, 0.19, 0.185), 0.12)
    f = auftragen(f, koerner(22, 0.012, 80), (0.31, 0.27, 0.24), 0.8)  # Schlacke, rostig
    f = auftragen(f, koerner(23, 0.010, 90), (0.13, 0.13, 0.14), 0.8)  # Schlacke, schwarz
    speichern(ziel, "schmiede", f, alpha_rand(24))


def hof(ziel):
    f = grund(31, "brown_mud_03", LEHM * 0.78, 0.10)
    f = auftragen(f, koerner(32, 0.03, 80), (0.58, 0.48, 0.28), 0.7)  # Strohhalme
    speichern(ziel, "hof", f, alpha_rand(34, 0.16, 0.10))


def schreiner(ziel):
    f = grund(41, "brown_mud_dry", LEHM * 0.98, 0.09)
    f = auftragen(f, koerner(42, 0.05, 80), (0.62, 0.50, 0.33), 0.7)  # Hobelspaene und Saegemehl
    speichern(ziel, "schreiner", f, alpha_rand(44))


def steinmetz(ziel):
    f = grund(51, "gravel_floor_02", (0.46, 0.43, 0.38), 0.10)
    f = auftragen(f, koerner(52, 0.04, 80), (0.57, 0.54, 0.48), 0.6)  # Splitt
    speichern(ziel, "steinmetz", f, alpha_rand(54))


def wirt(ziel):
    f = grund(61, "brown_mud_03", LEHM * 0.72, 0.11)
    f = auftragen(f, koerner(62, 0.025, 80), (0.54, 0.44, 0.26), 0.7)  # zertretenes Stroh
    speichern(ziel, "wirt", f, alpha_rand(64))


def dunkel(ziel):
    f = grund(71, "burned_ground_01", (0.27, 0.235, 0.205), 0.11)
    f = auftragen(f, koerner(72, 0.015, 80), (0.36, 0.31, 0.26), 0.6)
    speichern(ziel, "dunkel", f, alpha_rand(75))


def steinplatten(ziel):
    f = grund(81, "cobblestone_large_01", (0.43, 0.41, 0.38), 0.10, 0.2)
    speichern(ziel, "steinplatten", f, alpha_rand(84, 0.07, 0.04))


def marktstroh(ziel):
    f = grund(91, "brown_mud_03", LEHM * 0.85, 0.09)
    f = auftragen(f, koerner(92, 0.06, 80), (0.60, 0.50, 0.30), 0.7)
    luecken = 1 - 0.7 * stufe(rauschen(93, 6), 0.55, 0.7)  # das Stroh liegt in Placken, dazwischen guckt der Boden durch
    speichern(ziel, "marktstroh", f, alpha_rand(94, 0.20, 0.14) * luecken)


def saum(ziel):
    f = grund(101, "brown_mud_03", LEHM * 0.66, 0.10)
    f = auftragen(f, koerner(102, 0.03, 70), (0.42, 0.40, 0.37), 0.6)  # kleine Steine
    speichern(ziel, "saum", f, alpha_rand(104, 0.18, 0.10))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)
    QUELLEN = sys.argv[1]
    os.makedirs(sys.argv[2], exist_ok=True)
    for K in (1, 2):
        for art in (wohnhaus, schmiede, hof, schreiner, steinmetz, wirt, dunkel, steinplatten, marktstroh, saum):
            art(sys.argv[2])
