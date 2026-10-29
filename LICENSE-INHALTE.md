# Lizenz von Reichsgilde

Dieses Dokument hält fest, was unter welcher Lizenz steht. Es ist **keine Rechtsberatung**; die Entscheidung des Nutzers (B411Y) vom 29.10.2026 ist hier sachlich festgehalten und sollte von einer Anwältin oder einem Anwalt geprüft werden.

## Kurzfassung

| Was | Lizenz |
|---|---|
| Code und Skripte, Shader, Zahlenwerte und Struktur der Spieldaten | MIT ([LICENSE](LICENSE)) |
| Inhalte, veröffentlicht **bis zum Stichtag** (Merge von PR #PRNUMMER) | CC BY 4.0, nicht widerrufbar |
| Inhalte, die **ab dem Stichtag** neu ins Repository kommen oder neu erstellt werden | Alle Rechte vorbehalten, © B411Y |
| Fremdmaterial (Quaternius, Schriften, TressFX …) | die Lizenz des Urhebers, unverändert |

## Code: MIT

Alles unter `src/`, `game/scripts/`, `tests/` und `tools/` (mit den Skripten, die Icons, Modelle und Karten erzeugen) steht unter der [MIT-Lizenz](LICENSE). Ebenso:

- **Shader** (`game/assets/shader/`): Sie sind Quelltext in der Godot-Shadersprache, also Code, auch wenn sie unter `game/assets/` liegen.
- **Zahlenwerte, Struktur und Kennungen der Spieldaten** (`game/daten/wirtschaft.json` und die Strukturen der übrigen Dateien in `game/daten/`, etwa Katalog und Materialarten): Sie sind Spieldaten und gehören zum Code.

## Inhalte

Als **Inhalte** gelten:

- alles unter `game/assets/` (Grafiken, Modelle, Texturen, Icons, Sounds) außer den Shadern (siehe oben) und dem Fremdmaterial,
- die Quellbilder der KI-Grafiken unter `tools/icons/quellen/` und `tools/portraets/quellen/`,
- die **Texte, die Spieler lesen**: das Lexikon (`game/daten/lexikon.json`) sowie Beschreibungs-, Ereignis- und Berufstexte in `game/daten/*.json`.

Die Texte und Bilder unter `docs/` und die Bilder, die die README einbindet, bleiben wie bisher unter CC BY 4.0.

### Bis zum Stichtag veröffentlicht: CC BY 4.0

Alle Inhalte, die bis zum Merge von PR #PRNUMMER im Repository veröffentlicht sind, stehen unter der **Creative Commons Namensnennung 4.0 International (CC BY 4.0)**:
https://creativecommons.org/licenses/by/4.0/deed.de (Vertragstext: https://creativecommons.org/licenses/by/4.0/legalcode.de)

Diese Lizenz ist nicht widerrufbar (nach den Hinweisen von Creative Commons lässt sich eine CC-Lizenz nicht zurückziehen, solange der Lizenznehmer die Bedingungen einhält). Wer diese Fassungen früher übernommen hat, darf sie weiter unter CC BY 4.0 nutzen. Die Lizenzänderung gilt nur für das, was danach neu entsteht.

Dazu gehören im Einzelnen (Stand: Merge von PR #PRNUMMER):

- alles unter `game/assets/` – außer den Shadern (MIT, siehe oben) und den Schriften in `game/assets/oberflaeche/schriften/` (SIL Open Font License 1.1, Lizenztexte dort)
- die Texte und Bilder unter `docs/` und die Bilder, die die README einbindet

Ausgenommen sind die Modelle `natur_kit_*.glb` (Ordner `natur/`) und `props_*.glb` (Ordner `props/`): Sie sind bearbeitete Auswahlen aus Paketen von Quaternius (Stylized Nature, Fantasy Props, Medieval Village MegaKit) unter **CC0 1.0** (Public Domain Dedication); Herkunft in den `HERKUNFT.md` neben den Dateien.

Das Modell `schmied_ki.glb` (Ordner `figuren/ki/`) entstand aus einem Bild des Nutzers mit TRELLIS.2, AssetFurnace, SkinTokens und Kimodo; die Lizenzen dieser Werkzeuge und ihrer Ausgaben stehen in `game/assets/modelle/figuren/ki/HERKUNFT.md`. Es steht ebenfalls unter CC BY 4.0.

Die Waren-Knöpfe (`game/assets/oberflaeche/icons/`) und ihre Teile (`tools/icons/quellen/`: Rahmen, Hintergründe, Waren-Motive) wurden mit dem Bildmodell Gemini von Google erzeugt und im Projekt zusammengesetzt; sie stehen ebenfalls unter CC BY 4.0.

Ebenfalls mit KI erzeugt und ebenfalls unter CC BY 4.0:

- die Porträts aller Völker mit Masken und Hintergründen (`game/assets/oberflaeche/portraets/`, Quellen in `tools/portraets/quellen/`), Personen und Masken mit Qwen-Image (lokal),
- die Einrichtungs-Icons (`game/assets/oberflaeche/icons/einrichtung_*.png`): Motive mit Qwen-Image (lokal, `tools/icons/quellen/motiv/einrichtung_*.png`) auf den Gemini-Rahmen und -Hintergründen,
- die Charakterzug-Medaillons (`game/assets/oberflaeche/zuege/`): Bilder mit Gemini und ChatGPT,
- der Waren-Knopf Dolch (`dolch.png`): Motiv mit Qwen-Image-Edit aus dem Gemini-Motiv des Rapiers.

Werkzeug, Modell, Prompt und Nachbearbeitung je Datei stehen im Asset-Protokoll `docs/assets/protokoll.csv`.

**Was CC BY 4.0 für diese Fassungen erlaubt:** Jeder darf sie kopieren, verbreiten und verändern, auch kommerziell, sofern er den Urheber nennt, einen Link zur Lizenz angibt und kenntlich macht, ob er etwas geändert hat.

> „Reichsgilde“ von B411Y (https://github.com/B411Y/reichsgilde), lizenziert unter CC BY 4.0

### Ab dem Stichtag: Alle Rechte vorbehalten

Inhalte, die **nach dem Merge von PR #PRNUMMER** neu ins Repository kommen oder neu erstellt werden (neue Dateien, neue Texte, auch neue Fassungen bestehender Dateien), stehen unter **„Alle Rechte vorbehalten“**: © 2026 B411Y. Ohne ausdrückliche Erlaubnis darf sie niemand über das hinaus nutzen, was das Gesetz oder die GitHub-Nutzungsbedingungen (siehe unten) erlauben.

Jede neue Zeile im Asset-Protokoll trägt ab dann in `lizenz_ergebnis` den Wert „Alle Rechte vorbehalten“; `tools/assets/protokoll_pruefen.py` prüft das, sobald der Stichtag dort eingetragen ist.

Das gilt nicht für Code (bleibt MIT) und nicht für Fremdmaterial.

## Fremdmaterial

Fremdmaterial behält seine eigene Lizenz, unverändert: Quaternius-Pakete (CC0 1.0), die Schriften Alegreya und Germania One (SIL Open Font License 1.1, Lizenztexte neben den Dateien), TressFX (MIT) und weitere. Die Herkunft steht in den `HERKUNFT.md` neben den Dateien; neues Fremdmaterial bekommt im Asset-Protokoll seine eigene Lizenz, nicht „Alle Rechte vorbehalten“.

## GitHub-Nutzungsbedingungen

Weil das Repository öffentlich auf GitHub liegt, gelten zusätzlich die [GitHub-Nutzungsbedingungen](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service), Abschnitt D „User-Generated Content“: D.5 („License Grant to Other Users“) räumt anderen GitHub-Nutzern ein, öffentliche Inhalte innerhalb von GitHub zu nutzen, anzuzeigen und zu forken; D.4 („License Grant to Us“) betrifft die Rechte, die GitHub selbst für den Betrieb des Dienstes erhält. „Alle Rechte vorbehalten“ gilt daneben, kann diese Einräumungen aber nicht aufheben, solange das Repository öffentlich ist.

## Asset-Protokoll

`docs/assets/protokoll.csv` nennt je Zeile in `lizenz_ergebnis` die Lizenz der Dateien (Einzelheiten: `docs/assets/README.md`).

Copyright (c) 2026 B411Y
