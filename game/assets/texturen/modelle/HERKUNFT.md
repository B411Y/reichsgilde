# Herkunft der Relief-Texturen

Die zwei Dateien `relief_farbe_hoehe.webp` und `relief_normal_rauheit_ao.webp` sind Texture2DArrays mit je 13 Schichten (1024 × 1024), gepackt von `tools/oberflaeche/relief_texturen.py` aus 13 fremden CC0-Texturen (1K-JPG, abgerufen am 10.11.2026). Der Shader `oberflaeche.gdshader` benutzt sie für die Modelle; ohne die Dateien gilt die bisherige Material-Bibliothek (`modelle_*.webp`).

**Lizenz: CC0 1.0 Universal (Public Domain).** Wörtlich von den Lizenzseiten (abgerufen 10.11.2026):

- Poly Haven, https://polyhaven.com/license: „Our assets are all licensed as CC0, which is effectively Public Domain even in jurisdictions that do not support the Public Domain.“ und „You can redistribute them, share them around, include them when sharing your own work, or even in a product you sell.“ und „You do not need to give credit or attribution when using them (although it is appreciated).“
- ambientCG, https://docs.ambientcg.com/license/: „All ambientCG assets are provided under the Creative Commons CC0 1.0 Universal License.“ und „You can copy, modify, distribute and perform the assets, even for commercial purposes, all without asking permission. You can include the raw files in your project, for example a video game.“

| Schicht | Asset-ID | Name | Urheber | Seite | Abruf des Pakets |
|---|---|---|---|---|---|
| bruchstein | `stone_wall` | Stone Wall | Dario Barresi, Charlotte Baglioni | https://polyhaven.com/a/stone_wall | 10.11.2026 |
| quader | `rustic_stone_wall` | Rustic Stone Wall | Dimitrios Savva | https://polyhaven.com/a/rustic_stone_wall | 10.11.2026 |
| kalkputz | `white_plaster_rough_01` | White Plaster Rough 01 | Rob Tuytel | https://polyhaven.com/a/white_plaster_rough_01 | 10.11.2026 |
| lehmputz | `clay_plaster` | Clay Plaster | Amal Kumar | https://polyhaven.com/a/clay_plaster | 10.11.2026 |
| fachwerkholz | `rough_wood` | Rough Wood | Rob Tuytel | https://polyhaven.com/a/rough_wood | 10.11.2026 |
| bretter | `old_planks_02` | Old Planks 02 | Rob Tuytel | https://polyhaven.com/a/old_planks_02 | 10.11.2026 |
| dachziegel | `clay_roof_tiles_02` | Clay Roof Tiles 02 | Amal Kumar | https://polyhaven.com/a/clay_roof_tiles_02 | 10.11.2026 |
| schiefer | `RoofingTiles003` | RoofingTiles003 | ambientCG (Lennart Demes) | https://ambientcg.com/a/RoofingTiles003 | 10.11.2026 |
| stroh | `thatch_roof_angled` | Thatch Roof Angled | Rob Tuytel, Dimitrios Savva | https://polyhaven.com/a/thatch_roof_angled | 10.11.2026 |
| sandstein | `yellow_stone_wall` | Yellow Stone Wall | Sơn Nguyễn | https://polyhaven.com/a/yellow_stone_wall | 10.11.2026 |
| ziegelstein | `medieval_red_brick` | Medieval Red Brick | Rob Tuytel | https://polyhaven.com/a/medieval_red_brick | 10.11.2026 |
| holzschindeln | `roof_slates_02` | Roof Slates 02 | Rob Tuytel | https://polyhaven.com/a/roof_slates_02 | 10.11.2026 |
| pflaster | `cobblestone_floor_01` | Cobblestone Floor 01 | Rob Tuytel | https://polyhaven.com/a/cobblestone_floor_01 | 10.11.2026 |

Nachbearbeitung (alles in `tools/oberflaeche/relief_texturen.py`): Farbe in linearem Licht – Kontrast und Farbigkeit der Abweichung vom Mittel gedämpft, damit der Shader (der durch die mittlere Farbe teilt und mit der Modellfarbe färbt) die Hausfarben behält; Höhe auf den Bereich 1. bis 99. Perzentil gedehnt; Verdeckung (AO) aus der Quelle bzw. bei ambientCG RoofingTiles003 (liefert keine) aus der Höhe berechnet; alle Schichten zu einem Gitter gepackt, WebP q95 (Alpha verlustfrei). Die Quellordner liegen nicht im Repository.

Kachelmaße in Metern: Aufnahmemaß der Quelle, ggf. gerundet (Poly Haven nennt es je Asset).
