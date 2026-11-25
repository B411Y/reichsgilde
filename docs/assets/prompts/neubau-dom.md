# Neubau der Gebäude: Dome (Paket 1: Dom katholisch, außen und innen, mit dem Dom-Grundgerüst)

Keine Bild-KI und keine Image-to-3D-KI. Modelle, UV und Texturen entstehen aus Skripten, die Claude (Fable 5.2, Claude Code) in der Sitzung vom 23.11.2026 geschrieben hat.

**Vorgaben des Nutzers:** der Style-Guide „3D-Asset-Pipeline & Style-Guide: Reichsgilde (Stand: November 2026)“ (Monument 8.000–15.000 Dreiecke, Alpha-Cards, Dachstufung, Dual-UV, Trim-Sheet, `InteriorRoot`) und zwei Konzeptbilder als Stilvorlage: `dom_katholisch.jpg` (außen: Doppelturmfassade, grauer Stein mit gelblichem Sandstein, Querhaus, Chor mit Strebewerk) und `dom_in_katholisch.jpg` (innen: Mittelschiff mit Pfeilern, Schachbrettboden, Bänke, Kanzel, erhöhter Chor mit goldenem Hochaltar, rote Fahnen). Ordner „3D-Modellierung Fable5.2“ des Nutzers; nicht im Repository, nur angesehen, nicht nachgebaut oder abgepaust. Maßstab: die neuen Wohnhäuser und das Rathaus des Neubaus.

**Auftrag (Nutzer, 23.11.2026):** Paket 1 der Dome – Dom katholisch außen und innen mit dem Grundgerüst für die weiteren Dome. Außen ist der Dom fest je Karte, die Einrichtung innen folgt dem Stadtglauben (katholisch, evangelisch, Simultankirche); die Flächen der Karten werden lieber vergrößert, als den Dom zu stauchen (Einsetzen in die Karten: Paket 3). Messe und Prediger docken an den neuen Innenraum an wie bisher an die Grundkörper.

**Ablauf**

1. `tools/modelle/neubau/dom_masse.py` (reines Python): alle Maße an einer Stelle (Höhen, Grundriss, Wände, Öffnungen, Türme, Plätze der Einrichtung, Sitzplätze), Soll gegen Ist für die Figuren aller Völker bis zum Oger.
2. `tools/modelle/neubau/dom_trimsheet.py` (in Blender, numpy, mit den Funktionen von `trimsheet.py`): packt CC0-Texturen von Poly Haven zu zwei Trim-Sheets der Dome (`dom_trim_*` außen, `dom_innen_trim_*` innen). Quellen, Urheber und Nachbearbeitung: `game/assets/texturen/neubau/HERKUNFT.md`.
3. `tools/modelle/neubau/dom_bib.py` (Dom-Grundgerüst, auf `rathaus_bib.py` und `netzbau.py`): Spitzbogen- und Rundöffnungen, Wandflächen außen und innen aus der Maßdatei, Scheiben, Gewände, Maßwerk, Rose, Strebepfeiler, Fialen, Kreuzgewölbe, Spitztonnen, Schachbrettboden, Rundpfeiler.
4. `tools/modelle/neubau/dom_katholisch_aussen.py` und `dom_katholisch_innen.py` (Blender 5.2, `-b`): Hülle und Innenraum aus denselben Maßen, Smart UV Project als UV2 (Rand 0,02), Verdeckung in die AO-Atlanten „dom“ und „dom_innen“ gebacken (Cycles, 64 bzw. 48 Proben, Reichweite 1,1 m; `ao_atlas.json`), GLB ohne Bilder.
5. `tools/modelle/neubau/dom_messen.py`: Soll gegen Ist aus den fertigen GLB (Dreiecke, Fläche, UV2, Fenster innen = außen, Anker).
6. Im Spiel setzen `Stadtansicht.Neubau.cs` und `Stadtansicht.Domneubau.cs` Trim-Sheet, Shader, Licht, Figuren und Einrichtung je Stadtglaube.

**Nachtrag 24.11.2026 (Branch phase5/dom-stein):** Das Mauerband der Hülle (`mauer`) nimmt den Stein der Menschenhäuser S2/S3 (Poly Haven `castle_wall_varriation`, 1K, CC0, Abruf 15.11.2026) in eigener Farbe, im Maßstab 1:1 wie am Haus (`haus_stein_band` in `dom_trimsheet.py`, dieselbe Rechnung wie `trimsheet_ebenen.stein_band`). Grund: das umgefärbte `sandstone_blocks_08` las sich im Pixel-Look als glatter Putz. Sandstein, Werkstein, Sockel, Schiefer und der Innenraum bleiben. Der Quellordner `--quellen` braucht dazu den Unterordner `castle_wall_varriation` mit `_diff_1k`, `_nor_gl_1k`, `_arm_1k`. Keine Bild-KI.

**Nachtrag 24.11.2026, Runde 2 (Branch phase5/dom-stein, Wunsch des Nutzers):** (1) Das Mauerband bleibt der Stein der Menschenhäuser mit denselben Lagen und Fugen, wird aber kühl grau eingefärbt (`MAUER_GRAU`), damit sich die gelben Sandstein-Glieder abheben. (2) Das Schieferdach zeigt Deckreihen wie das Ziegeldach des Wohnhauses: `dom_trimsheet.schiefer_reihen` malt drei Reihen von 0,25 m ins Band `schiefer`, `dom_bib.schieferflaeche` legt die Dachflächen in Lagen von 0,30 m mit senkrechter Stirn (9 cm), die ihre Farbe aus dem jetzt dunklen Band `schiefer_kante` nimmt. Hülle und `dom_ao_atlas.png` neu gebaut, Innenraum unverändert.

**Nachtrag 25.11.2026 (Branch phase5/dom-evangelisch, Paket 2: Dom evangelisch):** Keine Bild- und keine Image-to-3D-KI im Bauweg. Vorlage des Nutzers: ein Artwork (Gemini, Google; Prompt unbekannt; nicht im Repository, nur zum Ansehen) und ein Bausatz aus zwölf Modulen (`Dom_Module.blend`, `Dom_Assets.blend`: Tripo3D, bezahltes Abo des Nutzers, Prompt unbekannt; nicht im Repository). Aus dem Bausatz sind keine Netze übernommen, nur Größenordnungen (Achteck 11,5 m, Turm 8 m, Giebel 8 m). Hülle und Innenraum entstehen aus `dom_evangelisch_aussen.py`, `dom_evangelisch_innen.py` und `dom_evangelisch_masse.py` (alle Maße) mit den Bausteinen des Dom-Grundgerüsts; Blatt `dom_evangelisch_trim` (Backstein im Mauerband, Sandsteinsockel). Messung: `blender -b --python tools/modelle/neubau/dom_messen.py -- --modell=dom_evangelisch`.

**Aufruf**

```bash
python tools/modelle/neubau/dom_masse.py --soll-ist
blender -b --python tools/modelle/neubau/dom_trimsheet.py -- --quellen=<ordner mit den Poly-Haven-Paketen>
blender -b --python tools/modelle/neubau/dom_katholisch_aussen.py -- [--bilder=<ordner>] [--ohne-ao]
blender -b --python tools/modelle/neubau/dom_katholisch_innen.py -- [--bilder=<ordner>] [--ohne-ao]
blender -b --python tools/modelle/neubau/dom_evangelisch_aussen.py -- [--bilder=<ordner>] [--ohne-ao]
blender -b --python tools/modelle/neubau/dom_evangelisch_innen.py -- [--bilder=<ordner>] [--ohne-ao]
python tools/modelle/neubau/dom_messen.py [--md=<datei>]
```
