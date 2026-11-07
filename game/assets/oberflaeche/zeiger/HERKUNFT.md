# Herkunft: Mauszeiger (Lederhandschuh)

Herkunft je Datei: `docs/assets/protokoll.csv`.

**Hände (seit 07.11.2026):** Nutzerzeichnung, vom Nutzer mit Gemini überarbeitet (acht Vorlagenbilder: Gemeiner, Bürger, Patrizier, Edler, Freiherr, Reichsgraf, Wache, Dieb; die Quellbilder liegen nicht im Repository). `tools/oberflaeche/zeiger_aus_vorlage.py` stellt sie frei, richtet sie deckungsgleich aus (Fingerspitze, Schnalle) und pixelt sie je Größe einzeln (Flächenmittel, Umriss neu gezogen, kleine Lederpalette, Gold und Edelsteine als eigene Pixel, heller Außenrand). Acht Hände (sechs nach Stand 1 Gemeiner bis 6 Reichsgraf, dazu 7 Wache und 8 Dieb nur zur freien Wahl) je in „normal“ und „klick“, in 48, 64 und 80 Pixeln. Prompts der Vorlagen sind nicht überliefert (im Protokoll „unbekannt“). **Alle Rechte vorbehalten** (Entscheidung 29.10.2026, `LICENSE-INHALTE.md`).

**Türschloss:** unverändert das Bronze-Türschloss mit S-Griff aus `tools/modelle/handschuh.py` (Blender 5.2.2), eigenes Werk, **Alle Rechte vorbehalten**; 48 und 64 Pixel von 04.11.2026, 80 Pixel neu mit denselben Einstellungen (Rendergröße 480 statt 384, damit 80 ganzzahlig teilt), 32 Pixel entfällt. Von „Die Gilde 1“ stammt nur die Idee eines Handschuh-Zeigers, kein Bild.

Namen: `hand_<1..6|wache|dieb>_<normal|klick>_<px>.png`, `tuer_schloss_<px>.png`. Die Zeigerpunkte (Fingerspitze, Mitte des Schlüssellochs) stehen in `game/scripts/Mauszeiger.cs`.
