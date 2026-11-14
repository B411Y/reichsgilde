# Herkunft der Trim-Sheets (Neubau der Gebäude)

`trim_{albedo,normal,orm}.png` (Außenhäuser) und `innen_trim_{albedo,normal,orm}.png` (Innenräume), je 2048 × 2048, packt `tools/modelle/neubau/trimsheet.py` aus zwölf fremden CC0-Texturen von Poly Haven (2K-JPG: Farbe, Normale OpenGL, ARM; abgerufen am 14.11.2026 über `api.polyhaven.com/files/<Asset-ID>`). Die Quelldateien liegen nicht im Repository. Aufteilung der Bänder: `BAENDER_AUSSEN` und `BAENDER_INNEN` im Skript (außen wie im Style-Guide, Abschnitt 4.1).

**Lizenz: CC0 1.0 Universal (Public Domain).** Wörtlich von https://polyhaven.com/license (Zitat wie in `game/assets/texturen/modelle/HERKUNFT.md`, dort abgerufen am 10.11.2026): „Our assets are all licensed as CC0, which is effectively Public Domain even in jurisdictions that do not support the Public Domain.“

| Asset-ID | Name | Urheber | Seite | Verwendet für |
|---|---|---|---|---|
| `thatch_roof_angled` | Thatch Roof Angled | Rob Tuytel, Dimitrios Savva | https://polyhaven.com/a/thatch_roof_angled | außen: Stroh, Strohfransen (Alpha selbst erzeugt) |
| `rough_wood` | Rough Wood | Rob Tuytel | https://polyhaven.com/a/rough_wood | außen und innen: Balken |
| `white_plaster_rough_01` | White Plaster Rough 01 | Rob Tuytel | https://polyhaven.com/a/white_plaster_rough_01 | außen: Putz; innen: Lehmwand |
| `old_planks_02` | Old Planks 02 | Rob Tuytel | https://polyhaven.com/a/old_planks_02 | außen: Planken; innen: Bohlen |
| `stone_wall` | Stone Wall | Dario Barresi, Charlotte Baglioni | https://polyhaven.com/a/stone_wall | außen und innen: Bruchstein |
| `rusty_metal_02` | Rusty Metal 02 | Rob Tuytel | https://polyhaven.com/a/rusty_metal_02 | außen und innen: Eisen |
| `brown_mud_dry` | Brown Mud Dry | Rob Tuytel | https://polyhaven.com/a/brown_mud_dry | innen: Lehmboden |
| `hessian_230` | Hessian 230 | colormass, Rico Cilliers | https://polyhaven.com/a/hessian_230 | innen: Sackleinen (Strohsack) |
| `knitted_fleece` | Knitted Fleece | colormass, Rico Cilliers | https://polyhaven.com/a/knitted_fleece | innen: Wolldecke |
| `bark_brown_02` | Bark Brown 02 | Rob Tuytel | https://polyhaven.com/a/bark_brown_02 | innen: Rinde, Kräuter (umgefärbt) |
| `reed_roof_03` | Reed Roof 03 | Rob Tuytel | https://polyhaven.com/a/reed_roof_03 | innen: Stroh, Binsenmatte, Besen |
| `clay_plaster` | Clay Plaster | Amal Kumar | https://polyhaven.com/a/clay_plaster | innen: Ton, Ruß, Zwiebeln, Glut (umgefärbt) |

Nachbearbeitung (alles im Skript): auf Bandhöhe zugeschnitten und waagerecht gekachelt; Holz um 90° gedreht (Maserung entlang des Bandes, Normale mitgedreht); auf eine Zielfarbe je Band umgefärbt (Zeichnung der Aufnahme bleibt); gemalte Ränder (Schatten unter der Strohstufe, Fase an den Balken, Spritzwasserrand am Putz); Alpha der Strohfransen aus einem Zufallsprofil. Keine Bild-KI.

`bodenfleck.png` (512 × 512, mit Alpha; Decal unter den Häusern) entsteht im selben Skript aus `brown_mud_dry` (umgefärbt, weicher Rand).

Die Atlanten `wohnhaus_mensch_ao_atlas.png` und `wohnhaus_mensch_innen_ao_atlas.png` (2048 × 2048, 4 × 4 Zellen) sind keine Fremdtexturen: gebackene Verdeckung der Modelle auf UV2 (Blender Cycles, `tools/modelle/neubau/wohnhaus_mensch_s1_g1.py`; Platz je Modell in `tools/modelle/neubau/ao_atlas.json`).
