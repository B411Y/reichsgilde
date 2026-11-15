# Herkunft der Texturen der Klappbrücke

`bruecke_trim_{albedo,normal,orm}.png` (je 2048 × 2048; Albedo sRGB, Normale OpenGL, ORM = R Verdeckung, G Rauheit, B Metall) packt `tools/modelle/klappbruecke_trim.py` aus fünf fremden CC0-Texturen von Poly Haven (2K-JPG: Farbe, Normale OpenGL, ARM; Abruf 14.11.2026 über `api.polyhaven.com/files/<Asset-ID>`, Quelldateien nicht im Repository). Die Bänder stehen im Skript (`BRUECKE_BAENDER`): zwei Quaderverbände, nasser Quader, Werkstein, Eichenbohlen mit Fuge und Nagelköpfen, Eichenbalken, rostiges Eisen, feines Eisen, Schiefer, Beschlag mit Nietenreihe.

**Lizenz: CC0 1.0 Universal (Public Domain).** Zitat wörtlich von https://polyhaven.com/license (so auch in `game/assets/texturen/modelle/HERKUNFT.md`): „Our assets are all licensed as CC0, which is effectively Public Domain even in jurisdictions that do not support the Public Domain.“

| Asset-ID | Name | Urheber | Seite | Verwendet für |
|---|---|---|---|---|
| `sandstone_blocks_08` | Sandstone Blocks 08 | Rob Tuytel | https://polyhaven.com/a/sandstone_blocks_08 | Quaderbänder (`quader_a`, `quader_b`, `quader_nass`) und Werkstein |
| `old_planks_02` | Old Planks 02 | Rob Tuytel | https://polyhaven.com/a/old_planks_02 | Eichenbohlen (`bohlen`) |
| `rough_wood` | Rough Wood | Rob Tuytel | https://polyhaven.com/a/rough_wood | Eichenbalken (`balken`) |
| `rusty_metal_02` | Rusty Metal 02 | Rob Tuytel | https://polyhaven.com/a/rusty_metal_02 | Eisen (`eisen_rost`, `eisen_fein`, `eisen_beschlag`) |
| `roof_slates_02` | Roof Slates 02 | Rob Tuytel | https://polyhaven.com/a/roof_slates_02 | Schiefer (`dach`) |

Nachbearbeitung (alles im Skript, keine Bild-KI): auf Bandhöhe zugeschnitten und waagerecht gekachelt; Holz um 90° gedreht (Maserung entlang des Bandes); je Band auf eine Zielfarbe umgefärbt; Quaderverband mit Fugen, Fasen und Farbe je Stein (nach `trimsheet_stufen.py` des Steinhauses); Bohlenfugen und Nagelköpfe, Rostflecken (Zufallsfeld im Frequenzraum, fester Startwert) und Nietenreihe selbst gemalt.

`klappbruecke_ao_atlas.png` (2048 × 2048, eine Zelle) ist keine Fremdtextur: gebackene Verdeckung (Cycles, Kontaktschatten) der Pfeiler, Torhäuser und Klappen auf UV2 aus `tools/modelle/klappbruecke.py`.
