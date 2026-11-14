# Neubau der Gebäude: Probe Wohnhaus Mensch, Stufe 1, Grundform 1

Keine Bild-KI und keine Image-to-3D-KI. Modell, UV und Texturen entstehen aus Skripten, die Claude (Fable 5.2, Claude Code) in der Sitzung vom 14.11.2026 geschrieben hat.

**Vorgaben des Nutzers:** der Style-Guide „3D-Asset-Pipeline & Style-Guide: Reichsgilde (Stand: November 2026)“ (Polybudget, Alpha-Cards, Dachstufung, Dual-UV, Trim-Sheet, `building_trim.gdshader`, Einrichtung nach Ständen) und Konzeptbilder als Stilvorlage (`wohnhaus_lvl1_v01…v04`, `wohnhaus_in_lvl1_mensch`; nicht im Repository, nur angesehen, nicht nachgebaut). Entscheidungen: je Volk 4 Grundformen × 3 Stufen; die Probe ist Mensch, Stufe 1, Grundform 1 „schmal und hoch“.

**Ablauf**

1. `tools/modelle/neubau/trimsheet.py` (in Blender, numpy): packt zwölf CC0-Texturen von Poly Haven zu zwei Trim-Sheets (außen, innen). Quellen, Urheber und Nachbearbeitung: `game/assets/texturen/neubau/HERKUNFT.md`. Der Prompt aus Abschnitt 4.2 des Style-Guides (Trim-Sheet aus einer Bild-KI) wurde **nicht** benutzt: Fotografierte CC0-Materialien kacheln sauber und bringen Normale und Rauheit mit.
2. `tools/modelle/neubau/wohnhaus_mensch_s1_g1.py` (Blender 5.2.2, `-b`): baut Hülle und Innenraum aus denselben Maßen (`netzbau.py` legt jede Fläche auf ihr Band), Smart UV Project als UV2 (Rand 0,02), backt die Verdeckung in die Zelle des AO-Atlas (Cycles, 96 Proben, Reichweite 1,1 m) und schreibt zwei GLB ohne Bilder.
3. Im Spiel setzt `Stadtansicht.Neubau.cs` Trim-Sheet, Shader und Farben.

**Zweite Runde (14.11.2026, nach Rückmeldung des Nutzers):** Fahne, Wappen, Leerstand und Gerüst wie bei den bisherigen Häusern; Fensterkreuze und Rahmen innen; Butzenscheiben als eigene Shader (`butzen.gdshaderinc`, `butzenglas.gdshader`, `bleinetz.gdshader`; Recherche und Quellen in `docs/bereiche/darstellung.md`); Teilband „gestrichenes Holz“; AO-Atlas je Gruppe (`ao_atlas.json`); Einrichtungsstücke der Stufe 1 (Hausapotheke, Hauswächter, Hauswappen); `bodenfleck.png` fürs Decal. Weiter ohne Bild- oder 3D-KI.

**Aufruf**

```bash
blender -b --python tools/modelle/neubau/trimsheet.py -- --quellen=<ordner mit den Poly-Haven-Paketen>
blender -b --python tools/modelle/neubau/wohnhaus_mensch_s1_g1.py -- [--bilder=<ordner>]
```
