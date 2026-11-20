# 3D-Bücher für die Innenansichten, Lesepult und Schreibpult (3D-Modelle, eigene Skripte)

Kein KI-Modell, kein Prompt: Die Modelle entstehen rein aus Rechnung und Formen in `tools/modelle/buecher3d/` (Blender 5.2.3, `-b`): `buch_bau.py` (gemeinsame Bauteile, Gerüst, Clips, Messung, Export), `buch_stile.py` (Maße, Formen und Materialien je Buch), `buch_texturen.py` (Texturen), `moebel_bau.py` (Lesepult und Schreibpult mit `tools/modelle/bausteine.py`).

Auftrag (Nutzer, 17.11.2026, über den Teamleiter): drei Bücher mit je eigenem Aussehen, die in der Innenansicht in 3D aufklappen, beim Reiterwechsel umblättern und zuklappen (Personalbuch auf dem Lesepult, Familienbuch auf dem Stubentisch, Kontorbuch am Schreibpult); gleiche Machart wie der Handschuh und die Schriftrolle (schwarzer Umriss als invertierte Hülle, nur Albedo); Clips, Inhaltsflächen, 1.500 bis 2.500 Dreiecke. Die Formen sind frei erfunden (Lederbuch mit Riemen, edler Einband mit Wappenfeld, Rechnungsbuch mit Pergamentdeckel), nicht von einer Vorlage abgepaust.

Texturen: Einband, Leder, Seiten, Schnitt, Riemen, Pergamentkante werden in `buch_texturen.py` mit numpy aus festen Saaten gemalt (Wertrauschen, Fasern, Nähte, Rahmen, Spaltenlinien), keine Fremdbilder, keine KI.

## Schwarzes Buch (Hinterzimmer, 20.11.2026)

Auftrag (Nutzer, 20.11.2026, über den Teamleiter): das „Schwarze Buch“ der Hinterzimmer-Tafel als 3D-Modell auf dem Tisch des Hinterzimmers, mit Kamera-Schwenk wie die anderen 3D-Bücher; dunkles, abgegriffenes Lederbuch mit Prägelinie und Lesebändern in den vier Kapitelfarben der Tafel, eigenes Design, realistisch plus Handschuh-Stil (schwarzer Umriss als invertierte Hülle).

Kein KI-Modell, kein Prompt. Gebaut mit denselben Skripten (Blender 5.2.2 auf der NAS, `-b`): Stil „schwarz“ in `buch_stile.py` (Maße, vier Rückenbünde, fünf Eisenbuckel, aufgelegte Lederecken, vier Lesebänder am Fuß; Farben der Bänder aus `game/scenes/hinterzimmer_thema.tres`, „Lesebänder (Kapitel)“, helle Stufe), Texturen `leder_abgegriffen`, `einband_schwarz` (blind geprägte Doppellinie, Raute, Abrieb an Kanten, Ecken und Griffstelle, Kratzer, Wachsfleck), `seiten_schwarz`, `schnitt_schwarz` in `buch_texturen.py` (numpy, feste Saaten 4004, 4104, 4124, 4134, 4141). Aufruf: `blender -b --python tools/modelle/buecher3d/buch_bau.py -- <ausordner> schwarz`, dann `buch_schwarz.glb` nach `game/assets/modelle/oberflaeche/`. Vorschaubilder vor dem Einbau: `vorschau_blender.py` (Cycles, nicht im Spiel).

