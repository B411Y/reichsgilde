# Neubau der Gebäude: Rathaus (außen und sieben Innenräume)

Keine Bild-KI und keine Image-to-3D-KI. Modelle, UV und Texturen entstehen aus Skripten, die Claude (Fable 5.2, Claude Code) in der Sitzung vom 14.11.2026 geschrieben hat.

**Vorgaben des Nutzers:** der Style-Guide „3D-Asset-Pipeline & Style-Guide: Reichsgilde (Stand: November 2026)“ (Monument 8.000–15.000 Dreiecke, Alpha-Cards, Dachstufung, Dual-UV, Trim-Sheet, Möbel-Cluster, `InteriorRoot`) und acht Konzeptbilder als Stilvorlage: `rathaus.jpg`, `rathaus_eingangshalle.jpg`, `rathaus_amtsstube.jpg`, `rathaus_gerichtssaal.jpg`, `rathaus_kerker.jpg`, `rathaus_ratshalle.jpg`, `rathaus_turm_treppe.jpg`, `rathaus_turm_spitze.jpg` (Ordner „3D-Modellierung Fable5.2“ des Nutzers; nicht im Repository, nur angesehen, nicht nachgebaut oder abgepaust). Vorbild der Qualität: die Wohnhaus-Probe (`neubau-wohnhaus.md`).

**Ablauf**

1. `tools/modelle/neubau/rathaus_masse.py` (reines Python): alle Maße an einer Stelle (Geschosse, Wände, Öffnungen, Türen, Treppen, Plätze der Sitzung), Soll gegen Ist für die Figuren aller Völker bis zum Oger, Grundrisse als PNG.
2. `tools/modelle/neubau/rathaus_trimsheet.py` (in Blender, numpy, mit den Funktionen von `trimsheet.py`): packt CC0-Texturen von Poly Haven zu zwei Trim-Sheets des Rathauses (`rathaus_trim_*` außen, `rathaus_innen_trim_*` innen). Quellen, Urheber und Nachbearbeitung: `game/assets/texturen/neubau/HERKUNFT.md`. Der Prompt aus Abschnitt 4.2 des Style-Guides (Trim-Sheet aus einer Bild-KI) wurde nicht benutzt.
3. `tools/modelle/neubau/rathaus_aussen.py` und je Raum `rathaus_<raum>.py` (Blender 5.2.3, `-b`, Bibliothek `rathaus_bib.py`, Flächen über `netzbau.py`): Hülle und Räume aus denselben Maßen, Smart UV Project als UV2 (Rand 0,02), Verdeckung in den AO-Atlas der Gruppe gebacken (Cycles, 64 Proben, Reichweite 1,1 m; `ao_atlas.json`), GLB ohne Bilder.
4. `tools/modelle/neubau/rathaus_messen.py`: Soll gegen Ist aus den fertigen GLB.
5. Im Spiel setzen `Stadtansicht.Neubau.cs` und `Stadtansicht.Rathausneubau.cs` Trim-Sheet, Shader und Farben (seit Stufe 8 das einzige Rathaus, auch in Köln).

**Aufruf**

```bash
python tools/modelle/neubau/rathaus_masse.py --soll-ist --grundriss=<ordner>
blender -b --python tools/modelle/neubau/rathaus_trimsheet.py -- --quellen=<ordner mit den Poly-Haven-Paketen>
blender -b --python tools/modelle/neubau/rathaus_aussen.py -- [--bilder=<ordner>] [--ohne-ao]
blender -b --python tools/modelle/neubau/rathaus_eingangshalle.py -- [--bilder=<ordner>] [--ohne-ao]
blender -b --python tools/modelle/neubau/rathaus_ratshalle.py -- [--bilder=<ordner>] [--ohne-ao]
python tools/modelle/neubau/rathaus_sitzpruefung.py --sitzpruefung=<tools/modelle/sitzpruefung.py aus phase5/figuren-baukasten>
python tools/modelle/neubau/rathaus_messen.py [--md=<datei>]
python tools/modelle/neubau/rathaus_import.py   # Import-Einstellungen der Trim-Sheets wie beim Wohnhaus
```
