# Neubau der Gebäude: Wohnhaus Mensch, Stufe 1 (Probe Grundform 1, Paket 1 Grundformen 2 bis 4), Stufe 2 und 3 (je Grundform 1 bis 4)

Keine Bild-KI und keine Image-to-3D-KI. Modell, UV und Texturen entstehen aus Skripten, die Claude (Fable 5.2, Claude Code) in der Sitzung vom 14.11.2026 geschrieben hat.

**Vorgaben des Nutzers:** der Style-Guide „3D-Asset-Pipeline & Style-Guide: Reichsgilde (Stand: November 2026)“ (Polybudget, Alpha-Cards, Dachstufung, Dual-UV, Trim-Sheet, `building_trim.gdshader`, Einrichtung nach Ständen) und Konzeptbilder als Stilvorlage (`wohnhaus_lvl1_v01…v04`, `wohnhaus_in_lvl1_mensch`; nicht im Repository, nur angesehen, nicht nachgebaut). Entscheidungen: je Volk 4 Grundformen × 3 Stufen; die Probe ist Mensch, Stufe 1, Grundform 1 „schmal und hoch“.

**Ablauf**

1. `tools/modelle/neubau/trimsheet.py` (in Blender, numpy): packt zwölf CC0-Texturen von Poly Haven zu zwei Trim-Sheets (außen, innen). Quellen, Urheber und Nachbearbeitung: `game/assets/texturen/neubau/HERKUNFT.md`. Der Prompt aus Abschnitt 4.2 des Style-Guides (Trim-Sheet aus einer Bild-KI) wurde **nicht** benutzt: Fotografierte CC0-Materialien kacheln sauber und bringen Normale und Rauheit mit.
2. `tools/modelle/neubau/wohnhaus_mensch_s1_g1.py` (Blender 5.2.2, `-b`): baut Hülle und Innenraum aus denselben Maßen (`netzbau.py` legt jede Fläche auf ihr Band), Smart UV Project als UV2 (Rand 0,02), backt die Verdeckung in die Zelle des AO-Atlas (Cycles, 96 Proben, Reichweite 1,1 m) und schreibt zwei GLB ohne Bilder.
3. Im Spiel setzt `Stadtansicht.Neubau.cs` Trim-Sheet, Shader und Farben.

**Zweite Runde (14.11.2026, nach Rückmeldung des Nutzers):** Fahne, Wappen, Leerstand und Gerüst wie bei den bisherigen Häusern; Fensterkreuze und Rahmen innen; Butzenscheiben als eigene Shader (`butzen.gdshaderinc`, `butzenglas.gdshader`, `bleinetz.gdshader`; Recherche und Quellen in `docs/bereiche/darstellung.md`); Teilband „gestrichenes Holz“; AO-Atlas je Gruppe (`ao_atlas.json`); Einrichtungsstücke der Stufe 1 (Hausapotheke, Hauswächter, Hauswappen); `bodenfleck.png` fürs Decal. Weiter ohne Bild- oder 3D-KI.

**Paket 1 (14.11.2026): Grundformen 2, 3 und 4.** Auftrag des Nutzers: 2 breit, 3 mit Anbau bzw. Schuppen, 4 mit Hof bzw. Laube, alles wie bei der Probe. `tools/modelle/neubau/wohnhaus_mensch_s1.py` (Claude Fable 5.2, Claude Code) baut sie: Grundform 3 ist das Haus der Probe, gespiegelt, mit Bretterschuppen unter einem Pultdach; Grundform 2 (Traufe zur Gasse, 7,0 × 4,8 m) und 4 (Giebel zur Gasse, Laube unter einem Krüppelwalm) sind anderthalb Geschosse hoch mit Fachwerk im Erdgeschoss. Möbel, Gegenstände und Einrichtungsstücke kommen unverändert aus den Funktionen der Probe und werden an ihre Wand im neuen Raum versetzt. Konzeptbilder `wohnhaus_lvl1_v02…v04` nur als Stilvorlage angesehen. Weiter ohne Bild- oder 3D-KI, dieselben Trim-Sheets, je Haus eine Zelle im AO-Atlas.

**Fachwerk-Korrektur (14.11.2026).** Rückmeldung des Nutzers zum Bürgerhaus der Stufe 2 (Fenster verschieden groß, Hölzer nur an die Fensterrahmen gesetzt, Giebel nicht bis zum First), übertragen auf Stufe 1 im Auftrag des Teamleiters. Claude (Claude Code) hat die Konstruktionsregel der Stufe 2 in `wohnhaus_mensch_s1_g1.py` (`raster`, `gefach`, `fachwerk_wand`, `giebel_fachwerk`) und `wohnhaus_mensch_s1.py` übernommen und alle vier Grundformen mit Blender 5.2.3 neu gebaut; `wohnhaus_mensch_s1_messen.py` prüft die GLB. Quellen der Regel (nur gelesen): https://de.wikipedia.org/wiki/Fachwerkhaus, https://www.baunetzwissen.de/holz/fachwissen/holzbausysteme/fachwerkbauweise-7820010.

**Stufe 2 (Bürgerhaus), Grundform 1 – Probe (14.11.2026, Branch phase5/neubau-mensch-st23).** Auftrag des Nutzers: Wohnhaus Mensch Stufe 2 und 3, außen und innen, je vier Grundformen, Räume (Hinterzimmer, Bibliothek) im Haus, innen gleich außen; Konzeptbilder `wohnhaus_lvl2_v01…v04`, `wohnhaus_lvl3_v01…v04`, `wohnhaus_in_lvl2_mensch`, `wohnhaus_in_lvl3_mensch` nur als Stilvorlage angesehen. `tools/modelle/neubau/trimsheet_stufen.py` leitet aus den Blättern der Stufe 1 die Blätter `trim_s2`, `trim_s3` und `innen_trim_s23` ab (fünf weitere CC0-Texturen, HERKUNFT.md). `tools/modelle/neubau/wohnhaus_mensch_s23.py` (Claude Fable 5.2, Claude Code) baut Hülle und Wohnstube aus `wohnhaus_mensch_s23_masse.py` (Maße mit Soll/Ist für Mensch, Elb, Oger); die Möbel der Stufe 1 kommen aus den Funktionen der Probe und werden versetzt. `tools/modelle/raeume/raeume_s23.py` baut die Raummodelle von `tools/modelle/raeume/` an ihren Platz im Obergeschoss. Keine Fremdmodelle, keine Bild- oder 3D-KI.

**Grundformen 2–4 und Stufe 3 (14.11.2026, nach Rückmeldungen des Nutzers):** Fachwerk nach einer Konstruktionsregel (Ständerraster, Fenster im Gefach, Giebel bis zum First; Quellen in docs/bereiche/darstellung.md), Grundformen 2–4 nach den Vorlagen lvl2/lvl3_v02–v04 (`wohnhaus_mensch_s23_formen.py`), Steinhaus wie die Vorlagen mit Treppengiebel (Fenster), aber ganz aus Stein ohne Fachwerk (Nutzer 14.11.2026, abweichend vom Artwork); nach Rückmeldung 15.11.2026 Quaderverband im Trim-Sheet (trimsheet_stufen.quaderband), Eckquader, Gewände und Abdeckplatten aus hellem Sandstein, dunkle Holzakzente (Balkenköpfe, Läden, Traufe) wie die Vorlagen lvl3_v01/v02 und der Bibliothek im Dachgeschoss (`tools/modelle/raeume/raum_bibliothek_dach.py`; Entscheidungen des Nutzers). Keine Fremdmodelle, keine Bild- oder 3D-KI.

**Aufruf**

```bash
blender -b --python tools/modelle/neubau/trimsheet.py -- --quellen=<ordner mit den Poly-Haven-Paketen>
blender -b --python tools/modelle/neubau/wohnhaus_mensch_s1_g1.py -- [--bilder=<ordner>]
blender -b --python tools/modelle/neubau/wohnhaus_mensch_s1.py -- --form=<2|3|4> [--bilder=<ordner>] [--ohne-ao]
blender -b --factory-startup --python tools/modelle/neubau/wohnhaus_mensch_s1_messen.py -- --haus=wohnhaus_mensch_s1_g<1..4> [--ordner=<glb-ordner>] [--gegenprobe] [--tuer-soll=<m>]
blender -b --python tools/modelle/neubau/trimsheet_stufen.py -- --quellen=<ordner mit den Poly-Haven-Paketen>
python tools/modelle/neubau/wohnhaus_mensch_s23_masse.py --soll-ist
blender -b --python tools/modelle/neubau/wohnhaus_mensch_s23.py -- --stufe=<2|3> --form=<1..4> [--bilder=<ordner>] [--ohne-ao]
blender -b --python tools/modelle/neubau/wohnhaus_mensch_s23_messen.py -- --haus=wohnhaus_mensch_s<2|3>_g<1..4> [--ordner=<glb>] [--gegenprobe]
blender -b --python tools/modelle/raeume/raeume_s23.py -- --haus=wohnhaus_mensch_s2_g1 --raum=<hinterzimmer|bibliothek> [--bilder=<ordner>] [--ohne-ao]
```
