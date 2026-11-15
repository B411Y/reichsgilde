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

## Rathaus

`rathaus_trim_{albedo,normal,orm}.png` (Hülle) und `rathaus_innen_trim_{albedo,normal,orm}.png` (sieben Räume), je 2048 × 2048, packt `tools/modelle/neubau/rathaus_trimsheet.py` (mit den Funktionen von `trimsheet.py`) aus 13 CC0-Texturen von Poly Haven, abgerufen am 14.11.2026 wie oben (2K-JPG). Bänder: `BAENDER_RATHAUS` und `BAENDER_RATHAUS_INNEN` im Skript. Lizenz wie oben (CC0 1.0). Bereits oben genannt und hier wieder verwendet: `rough_wood` (Balken), `white_plaster_rough_01` (Putz), `old_planks_02` (Planken, Dielen), `rusty_metal_02` (Eisen, Messing, Bronze, Gold umgefärbt), `reed_roof_03` (Stroh im Kerker). Neu:

| Asset-ID | Name | Urheber | Seite | Verwendet für |
|---|---|---|---|---|
| `roof_slates_02` | Roof Slates 02 | Rob Tuytel | https://polyhaven.com/a/roof_slates_02 | außen: Schiefer, Schieferkante (Alpha selbst erzeugt) |
| `sandstone_blocks_08` | Sandstone Blocks 08 | Rob Tuytel | https://polyhaven.com/a/sandstone_blocks_08 | außen und innen: Sandsteinquader (Ecken, Bögen, Gewände, Kamin) |
| `castle_wall_slates` | Castle Wall Slates | Rob Tuytel | https://polyhaven.com/a/castle_wall_slates | außen und innen: Bruchstein (Erdgeschoss, Turm, Halle) |
| `dark_wooden_planks` | Dark Wooden Planks | Amal Kumar | https://polyhaven.com/a/dark_wooden_planks | innen: Täfelung, dunkle Möbel und Türen |
| `large_floor_tiles_02` | Large Floor Tiles 02 | Rob Tuytel | https://polyhaven.com/a/large_floor_tiles_02 | innen: Steinplatten |
| `rough_block_wall` | Rough Block Wall | Rob Tuytel | https://polyhaven.com/a/rough_block_wall | innen: Kerkermauer |
| `cobblestone_floor_01` | Cobblestone Floor 01 | Rob Tuytel | https://polyhaven.com/a/cobblestone_floor_01 | innen: Pflaster des Kerkers |
| `velour_velvet` | Velour Velvet | colormass, Rico Cilliers | https://polyhaven.com/a/velour_velvet | innen: roter Stoff (Banner, Behänge, Polster) |

Nachbearbeitung wie oben; dazu die ausgezahnte Unterkante der Schieferkante (Alpha aus einem Zufallsprofil) und Farbfelder für Kleinteile (Eisen, Messing, Bronze, Pergament, Wachs, Tinte, Glut, Gold). Keine Bild-KI.

`rathaus_ao_atlas.png` (1024 × 1024, eine Zelle) und `rathaus_innen_ao_atlas.png` (2048 × 2048, eine Zelle je Raum) sind gebackene Verdeckung der Rathausmodelle (Blender Cycles, `tools/modelle/neubau/rathaus_bib.py`), keine Fremdtexturen.
