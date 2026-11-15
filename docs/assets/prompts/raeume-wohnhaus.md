# Räume des Wohnhauses in 3D: Hinterzimmer und Bibliothek (Wohnhaus Mensch, Grundform 1)

Keine Bild-KI und keine Image-to-3D-KI. Modelle, UV und Texturen entstehen aus Skripten, die Claude (Fable 5.2, Claude Code) in der Sitzung vom 14.11.2026 geschrieben hat.

**Vorgaben des Nutzers:** der Style-Guide „3D-Asset-Pipeline & Style-Guide: Reichsgilde (Stand: November 2026)“ (Trim-Sheet, Dual-UV, Möbel-Cluster, `InteriorRoot`) und zwei Konzeptbilder als Stilvorlage (`raeume/hinterzimmer.webp`, `raeume/bibliothek.webp`; nicht im Repository, nur angesehen, nicht nachgebaut). Regeln wie bei der Wohnhaus-Probe (`neubau-wohnhaus.md`): Maße für eine Figur von 1,75 m, Fenster mit Butzenglas, Gegenstände statt Reiter.

**Ablauf**

1. `tools/modelle/raeume/raeume_trimsheet.py` (in Blender, numpy; Werkzeuge aus `tools/modelle/neubau/trimsheet.py`): packt CC0-Texturen von Poly Haven zu einem Trim-Sheet der Räume (`raeume_innen_trim_*`). Quellen, Urheber und Nachbearbeitung: `game/assets/texturen/neubau/HERKUNFT.md`. Selbst gerechnet (numpy, keine KI): Teppichmuster, Buchrücken, Landkarte und Schriftzeilen.
2. `tools/modelle/raeume/raum_hinterzimmer.py` und `raum_bibliothek.py` (Blender 5.2.3, `-b`): Raumhülle im Grundriss des Hauses (`raeume_masse.py`, gegen das Hausskript geprüft), Möbel-Cluster, Gegenstand, Anker; Smart UV Project als UV2 (Rand 0,02), Verdeckung in die Zelle des AO-Atlas (Cycles, 64 Proben, Reichweite 1,1 m, `tools/modelle/raeume/ao_atlas.json`); GLB ohne Bilder.
3. Die Zimmertüren der Wohnstube baut `tools/modelle/neubau/wohnhaus_mensch_s1_g1.py` mit `tools/modelle/raeume/zimmertuer.py`.
4. Im Spiel setzt `Stadtansicht.Neubau.cs` Trim-Sheet, Shader und Glas; `Stadtansicht.Wohnraeume.cs` hängt die Räume ins Haus.

**Aufruf**

```bash
blender -b --python tools/modelle/raeume/raeume_trimsheet.py -- --quellen=<ordner mit den Poly-Haven-Paketen>
blender -b --python tools/modelle/raeume/raum_hinterzimmer.py -- [--bilder=<ordner>]
blender -b --python tools/modelle/raeume/raum_bibliothek.py -- [--bilder=<ordner>]
python tools/modelle/raeume/raeume_masse.py --soll-ist
```
