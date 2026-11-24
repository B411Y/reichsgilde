# Herkunft der Bodenflecke (Decals unter den Gebäuden)

`bodenfleck_<art>_{klein,gross}.png` (zehn Arten, je zwei Größenklassen, je 512 × 512, sRGB mit Alpha) baut `tools/modelle/bodenflecke/bodenflecke.py` (Python 3, Pillow, numpy; keine Bild-KI) aus sieben fremden CC0-Texturen von Poly Haven (1K-JPG, nur die Farbe `<Asset-ID>_diff_1k.jpg`; abgerufen am 24.11.2026 über `api.polyhaven.com/files/<Asset-ID>`; Quelldateien nicht im Repository). Verwendet in `game/scripts/Stadtansicht.Bodenfleck.cs`.

**Lizenz der Quellen: CC0 1.0 Universal (Public Domain)**, https://polyhaven.com/license (Zitat in `game/assets/texturen/modelle/HERKUNFT.md`). Die Bilder selbst: Alle Rechte vorbehalten (`LICENSE-INHALTE.md`).

| Asset-ID | Name | Urheber | Seite | SHA-256 der JPG-Datei | Verwendet für |
|---|---|---|---|---|---|
| `brown_mud_dry` | Brown Mud Dry | Rob Tuytel | https://polyhaven.com/a/brown_mud_dry | `a56281b644de614787340f3db45e28d2f72ec32fc19a442f99002bae0ab11398` | Wohnhaus, Schmiede, Schreiner, Steinmetz, Alchimist/Parfumeur, Vorplatz: Erde, Staub |
| `gravel_floor_02` | Gravel Floor 02 | Jenelle van Heerden, Dimitrios Savva | https://polyhaven.com/a/gravel_floor_02 | `ac9874264762a9e8d337e684259d51dac41a012933f0d8013033fee7076383f3` | Wohnhaus: Traufkies; Steinmetz: Splitt; Sockelsaum: Steine |
| `burned_ground_01` | Burned Ground 01 | Rob Tuytel | https://polyhaven.com/a/burned_ground_01 | `be83310cad64d23539e628f14525af651a03646f5a1cc6029bd19a9446ea72f9` | Schmiede: Kohlestaub, Asche; Alchimist/Parfumeur: Ruß |
| `brown_mud_03` | Brown Mud 03 | Rob Tuytel | https://polyhaven.com/a/brown_mud_03 | `6da4fbd6fc5d21044dcbdbd96e098fb464ed544b42c982e72ce8ccd1d53686c5` | Hof, Wirt, Marktstände, Alchimist/Parfumeur, Sockelsaum: Matsch, nasse Erde |
| `thatch_roof_angled` | Thatch Roof Angled | Rob Tuytel, Dimitrios Savva | https://polyhaven.com/a/thatch_roof_angled | `ad84b9bf0a826c6e573f4bac09a0b96f33aec673a7e31cdd360667dc355f3599` | Hof, Wirt, Marktstände: Stroh |
| `wood_chips` | Wood Chips | eye-candy.xyz | https://polyhaven.com/a/wood_chips | `7788c5d14db682fe7ee0b52e76bdf1ffac1346c25a5284ceaf51175a62c114a9` | Schreiner: Späne, Sägemehl |
| `cobblestone_large_01` | Cobblestone Large 01 | Rob Tuytel | https://polyhaven.com/a/cobblestone_large_01 | `135e067ac9943b1c3782eae5242bac317725d0885398c94656f4e3b7c21eccf2` | Dom, Rathaus, Kirchen, Stapelhaus: Vorplatz |

Nachbearbeitung (alles im Skript): auf 512 × 512 verkleinert; je Stoff auf eine Zielfarbe umgefärbt (die Zeichnung der Aufnahme bleibt); mit selbst gerechnetem, fest gesätem Rauschen zu groben Flecken gemischt (Kiesband am Haus, Kohle und Asche, Stroh auf Matsch, Pfützen, Späne, Splitt, Lachen); weicher, unregelmäßiger Alpha-Rand. Im großen Bild ist alles halb so groß (Aufnahme 2 × 2 gekachelt), damit der Ring um große Gebäude in Metern gleich breit bleibt.
