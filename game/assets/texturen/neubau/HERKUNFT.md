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

## Räume im Wohnhaus (Hinterzimmer, Bibliothek)

`raeume_innen_trim_{albedo,normal,orm}.png` (2048 × 2048) packt `tools/modelle/raeume/raeume_trimsheet.py` mit denselben Werkzeugen aus CC0-Texturen von Poly Haven (2K-JPG, abgerufen am 14.11.2026 über `api.polyhaven.com/files/<Asset-ID>`; Quelldateien nicht im Repository). Lizenz der Quellen wie oben: CC0 1.0. Neu dazu gegenüber der Tabelle oben:

| Asset-ID | Name | Urheber | Seite | Verwendet für |
|---|---|---|---|---|
| `dark_wooden_planks` | Dark Wooden Planks | Amal Kumar | https://polyhaven.com/a/dark_wooden_planks | Parkett (Bibliothek) |
| `wood_table_001` | Wood Table 001 | Dimitrios Savva, Rico Cilliers | https://polyhaven.com/a/wood_table_001 | Möbelholz (Regale, Tisch, Stühle, Pult) |
| `castle_wall_slates` | Castle Wall Slates | Rob Tuytel | https://polyhaven.com/a/castle_wall_slates | Bruchstein (Sockel, Pfeiler, Ofen) |
| `sandstone_blocks_08` | Sandstone Blocks 08 | Rob Tuytel | https://polyhaven.com/a/sandstone_blocks_08 | Sandstein (Kamin, Sims) |
| `velour_velvet` | Velour Velvet | colormass, Rico Cilliers | https://polyhaven.com/a/velour_velvet | Grund des Teppichs |
| `leather_red_03` | Leather Red 03 | Rob Tuytel | https://polyhaven.com/a/leather_red_03 | Leder, Grund der Buchrücken (Farbbild „coll1“) |

Dazu aus der Tabelle oben: `old_planks_02` (Dielen), `rough_wood` (Balken), `white_plaster_rough_01` (Putz, Grund des Pergaments), `hessian_230` (Säcke), `rusty_metal_02` (Eisen, Messing, Gold). Nachbearbeitung im Skript wie oben; selbst gerechnet (keine Bild-KI): Muster des Teppichs (Borte, Rauten), Buchrücken (Breiten, Lederfarben, Bünde, Goldlinien), Landkarte und Schriftzeilen auf dem Pergament. `wohnhaus_mensch_raeume_innen_ao_atlas.png` ist gebackene Verdeckung der Raummodelle (Platz je Raum in `tools/modelle/raeume/ao_atlas.json`).

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

## Wohnhäuser Mensch, Stufe 2 und 3 (Bürgerhaus, Steinhaus)

`trim_s2_*`, `trim_s3_*` und `innen_trim_s23_*` (je Albedo, Normale, ORM, 2048 × 2048) schreibt `tools/modelle/neubau/trimsheet_stufen.py`: Es liest die fertigen Blätter `trim_*` und `innen_trim_*` (oben) und ersetzt nur einzelne Bänder; alle anderen Bänder sind Pixel für Pixel die der Stufe 1. Quellen (Poly Haven, CC0 1.0 wie oben; Quelldateien nicht im Repository):

| Asset-ID | Name | Urheber | Seite | Verwendet für |
|---|---|---|---|---|
| `clay_roof_tiles_02` | Clay Roof Tiles 02 | Amal Kumar | https://polyhaven.com/a/clay_roof_tiles_02 | trim_s2: Ziegel (1K-JPG, Dateien vom 10.11.2026) |
| `roof_slates_02` | Roof Slates 02 | Rob Tuytel | https://polyhaven.com/a/roof_slates_02 | trim_s3: Schiefer |
| `sandstone_blocks_08` | Sandstone Blocks 08 | Rob Tuytel | https://polyhaven.com/a/sandstone_blocks_08 | trim_s3: Haustein (Band „bruchstein“) |
| `white_plaster_rough_01` | White Plaster Rough 01 | Rob Tuytel | https://polyhaven.com/a/white_plaster_rough_01 | innen_trim_s23: Kalkputz |
| `large_floor_tiles_02` | Large Floor Tiles 02 | Rob Tuytel | https://polyhaven.com/a/large_floor_tiles_02 | innen_trim_s23: Tonfliesen und Schachbrett (Band „lehmboden“) |

Urheber laut Poly-Haven-Schnittstelle (`api.polyhaven.com/assets`, Abzug im Quellordner). Nachbearbeitung im Skript: auf Bandhöhe zugeschnitten, waagerecht gekachelt, umgefärbt; die Dachdeckung ohne Alpha (keine Fransen); die Fliesen mit gezeichneten Fugen, leicht verschiedenem Ton je Fliese und im rechten Feld als Schachbrett. Keine Bild-KI.

## Steinebenen je Volk (`haeuser_ebenen_*`, `innen_ebenen_*`, `*_ao_ebenen`)

`tools/modelle/neubau/trimsheet_ebenen.py` legt die Trim-Sheets der Häuser als Texture2DArray untereinander (Ebene je Volk und Stufe) und setzt in die Steinbänder jeder Ebene den Stein des Volkes aus drei fremden CC0-Texturen (1K-JPG, abgerufen am 15.11.2026; Poly Haven über `api.polyhaven.com/files/<Asset-ID>`, ambientCG über `ambientcg.com/get?file=<ID>_1K-JPG.zip`). Die Quelldateien liegen nicht im Repository. Die übrigen Bänder kommen unverändert aus den Blättern oben; `innen_ebenen_*` ist `innen_trim` als Array mit einer Ebene, `*_ao_ebenen` sind die gebackenen Atlanten untereinander (keine Fremdtexturen).

**Lizenz: CC0 1.0 Universal (Public Domain).** Poly Haven wörtlich von https://polyhaven.com/license (abgerufen am 15.11.2026): „Our assets are all licensed as CC0“. ambientCG wörtlich von https://docs.ambientcg.com/license/ (abgerufen am 15.11.2026): „All ambientCG assets are provided under the Creative Commons CC0 1.0 Universal License.“

| Kürzel | Asset-ID | Urheber | Seite | Reale Größe | Verwendet für |
|---|---|---|---|---|---|
| K1 | `castle_wall_varriation` | Rob Tuytel | https://polyhaven.com/a/castle_wall_varriation | 2 × 2 m | Menschen: Mauern (Stufe 2 und 3) |
| K2 | `large_sandstone_blocks_01` | Rob Tuytel | https://polyhaven.com/a/large_sandstone_blocks_01 | 3 × 3 m | Elben und Halblinge: Flächen; Zwerge Stufe 3: heller Haustein im Band „bruchstein“ (seit 16.11.2026) |
| K5 | `Bricks100` | ambientCG (kein Einzelurheber genannt) | https://ambientcg.com/view?id=Bricks100 | unbekannt, angenommen 2 × 1 m | Elben und Halblinge: Akzent |

Nachbearbeitung (alles im Skript): in eigener Farbe eingesetzt (nicht umgefärbt); auf Bandhöhe zugeschnitten, Ober- und Unterkante auf dunkle Lagerfugen gelegt; waagerecht in 2^k Kacheln je Band (nahtlos) und dafür im Maßstab angepasst (K2 1,33, sonst 1,0). K3 (`stone_wall_04`) und K4 (`PavingStones142`) aus der Auswahl vom 15.11.2026 sind nach der Rückmeldung des Nutzers nicht eingesetzt. Keine Bild-KI.

## Zwergenhäuser (`trim_zwerg_*`, `innen_trim_zwerg_*`, `trim_s2_zwerg_*`, `trim_s3_zwerg_*`, `innen_trim_s23_zwerg_*`, `raeume_innen_trim_zwerg_*`)

Die Trim-Sheets der Zwergenhäuser haben dieselben Bandgrenzen wie die der Menschen, aber anderen Inhalt: `tools/modelle/neubau/trimsheet_zwerg.py` (Stufe 1: Band „Putz“ behauener Quader, Band „Stroh“ selbst gezeichnete Holzschindeln aus `old_planks_02` mit Alpha an den Füßen; innen Steinwand und Pflaster), `trimsheet_zwerg_stufen.py` (Stufe 2 und 3: Schiefer, Sandstein, Schachbrett) und `tools/modelle/raeume/raeume_trimsheet_zwerg.py` (Räume: Quaderwand, Pflaster im Hinterzimmer, Parkett der Menschen in der Bibliothek). Alle Quellen Poly Haven, **CC0 1.0 Universal**, Zitat von https://polyhaven.com/license wie oben; Abruf 14.11.2026 (2K-JPG); die Quelldateien liegen nicht im Repository. Keine Bild-KI.

| Asset-ID | Name | Urheber | Seite | Verwendet für (Zwerg) |
|---|---|---|---|---|
| `rough_block_wall` | Rough Block Wall | Rob Tuytel | https://polyhaven.com/a/rough_block_wall | außen: Quader (Mauer, Fachwerkfüllung); innen und Räume: Steinwand |
| `castle_wall_slates` | Castle Wall Slates | Rob Tuytel | https://polyhaven.com/a/castle_wall_slates | außen: Bruchstein (Sockel, Schlot, Findlinge); innen: Schlot und Herd |
| `cobblestone_floor_01` | Cobblestone Floor 01 | Rob Tuytel | https://polyhaven.com/a/cobblestone_floor_01 | innen und Hinterzimmer: Steinpflaster |
| `roof_slates_02`, `sandstone_blocks_08`, `large_floor_tiles_02` | wie oben | Rob Tuytel | siehe oben | Stufe 2/3: Schiefer, Haustein, Schachbrett |
| `old_planks_02`, `rough_wood`, `rusty_metal_02`, `hessian_230`, `knitted_fleece`, `bark_brown_02`, `reed_roof_03`, `clay_plaster` | wie oben | wie oben | siehe oben | Schindeln, Balken, Planken, Eisen, Kleinteile (anders umgefärbt) |

Die AO-Atlanten `wohnhaus_zwerg{,_s2,_s3,_innen,_raeume_innen,_dachraeume_innen}_ao_atlas.png` sind keine Fremdtexturen: gebackene Verdeckung der Modelle auf UV2 (Blender Cycles; Plätze in `tools/modelle/neubau/ao_atlas.json` und `tools/modelle/raeume/ao_atlas.json`).
