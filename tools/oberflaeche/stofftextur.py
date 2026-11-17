#!/usr/bin/env python3
"""Macht aus ambientCG „Fabric 045“ (CC0, grob gewebter Wollstoff) die drei Stoff-Texturen des Wandbehangs.

Aufruf: python3 tools/oberflaeche/stofftextur.py <Ordner mit entpackter Fabric045_1K-JPG.zip> game/assets/texturen/stoff
Braucht Pillow (kein numpy). Quelle und Prüfsumme: game/assets/texturen/stoff/HERKUNFT.md.

- Albedo: Graustufen; Mittelwert auf HELL gelegt und die Abweichung davon mit KONTRAST verstärkt. So nimmt das Malnehmen mit dem
  gedämpften Hausfarbton (Behangfarbe.cs) der Farbe kaum Helligkeit, die Fäden bleiben aber sichtbar.
- Normal (OpenGL-Format, wie Godot es erwartet): nur verkleinert.
- Rauheit: verkleinert und auf den Bereich ab RAU_AB gehoben (Wolle glänzt nicht; die Quelle liegt im Mittel bei 0,53).
Alles auf GROESSE x GROESSE mit Lanczos verkleinert.
"""
import sys
from PIL import Image, ImageStat

quelle, ziel = sys.argv[1], sys.argv[2]
NAME = "Fabric045"
GROESSE = 512
HELL = 0.80
KONTRAST = 2.2
RAU_AB = 0.45


def laden(karte):
    return Image.open(f"{quelle}/{NAME}_1K-JPG_{karte}.jpg").resize((GROESSE, GROESSE), Image.LANCZOS)


albedo = laden("Color").convert("L")
mittel = ImageStat.Stat(albedo).mean[0]
albedo = albedo.point(lambda v: max(0, min(255, round(255 * HELL + (v - mittel) * KONTRAST))))
albedo.save(f"{ziel}/stoff_albedo_grau.png", optimize=True)
laden("NormalGL").convert("RGB").save(f"{ziel}/stoff_normal.png", optimize=True)
laden("Roughness").convert("L").point(lambda v: round(255 * RAU_AB + v * (1 - RAU_AB) * 1.35) if v < 189 else 255).save(f"{ziel}/stoff_rauheit.png", optimize=True)
