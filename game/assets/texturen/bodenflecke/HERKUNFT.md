# Herkunft der Bodenflecke (Decals unter den Gebäuden)

`bodenfleck_{wohnhaus,schmiede,hof}.png` (je 512 × 512, sRGB mit Alpha) baut `tools/modelle/bodenflecke/bodenflecke.py` (Python 3, Pillow, numpy; keine Bild-KI) aus fünf fremden CC0-Texturen von Poly Haven (1K-JPG, nur die Farbe `<Asset-ID>_diff_1k.jpg`; abgerufen am 24.11.2026 über `api.polyhaven.com/files/<Asset-ID>`; Quelldateien nicht im Repository). Verwendet in `game/scripts/Stadtansicht.Bodenfleck.cs`.

**Lizenz der Quellen: CC0 1.0 Universal (Public Domain)**, https://polyhaven.com/license (Zitat in `game/assets/texturen/modelle/HERKUNFT.md`). Die drei Bilder selbst: Alle Rechte vorbehalten (`LICENSE-INHALTE.md`).

| Asset-ID | Name | Urheber | Seite | SHA-256 der JPG-Datei | Verwendet für |
|---|---|---|---|---|---|
| `brown_mud_dry` | Brown Mud Dry | Rob Tuytel | https://polyhaven.com/a/brown_mud_dry | `a56281b644de614787340f3db45e28d2f72ec32fc19a442f99002bae0ab11398` | Wohnhaus und Schmiede: Erde |
| `gravel_floor_02` | Gravel Floor 02 | Jenelle van Heerden, Dimitrios Savva | https://polyhaven.com/a/gravel_floor_02 | `ac9874264762a9e8d337e684259d51dac41a012933f0d8013033fee7076383f3` | Wohnhaus: Traufkies |
| `burned_ground_01` | Burned Ground 01 | Rob Tuytel | https://polyhaven.com/a/burned_ground_01 | `be83310cad64d23539e628f14525af651a03646f5a1cc6029bd19a9446ea72f9` | Schmiede: Kohlestaub, Asche |
| `brown_mud_03` | Brown Mud 03 | Rob Tuytel | https://polyhaven.com/a/brown_mud_03 | `6da4fbd6fc5d21044dcbdbd96e098fb464ed544b42c982e72ce8ccd1d53686c5` | Hof: Matsch |
| `thatch_roof_angled` | Thatch Roof Angled | Rob Tuytel, Dimitrios Savva | https://polyhaven.com/a/thatch_roof_angled | `ad84b9bf0a826c6e573f4bac09a0b96f33aec673a7e31cdd360667dc355f3599` | Hof: Stroh |

Nachbearbeitung (alles im Skript): auf 512 × 512 verkleinert; je Stoff auf eine Zielfarbe umgefärbt (die Zeichnung der Aufnahme bleibt); mit selbst gerechnetem, fest gesätem Rauschen zu groben Flecken gemischt (Kiesband am Haus, Kohle und Asche, Stroh auf Matsch, Pfützen); weicher, unregelmäßiger Alpha-Rand.
