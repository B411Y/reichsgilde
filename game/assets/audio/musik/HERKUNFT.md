# Herkunft: Musik

48 Musikstücke (23 aus Runde 1 vom 10.11., 25 aus Runde 2 vom 11.11.2026), vom Nutzer mit **Suno** erzeugt (bezahltes Abo, höchste Stufe; die Nutzungsbedingungen des Abos erlauben die kommerzielle Nutzung). Keine Aufnahme und nichts aus dem Original-Spiel. Prompts und Modellversion sind nicht festgehalten (im Asset-Protokoll „unbekannt“).

Aufbereitung: `tools/ton/musik_bauen.py` (Stille gekürzt, ein- und ausgeblendet, auf −17 LUFS verstärkt, OGG Vorbis). Messwerte: `docs/assets/musik-messwerte.csv`. Prompt-/Ablaufdatei: `docs/assets/prompts/musik-suno.md`. Titel und Zuordnung zu Lagen: `game/daten/musik.json`.

Runde 2 (11.11.2026): Aufbereitung mit `tools/ton/musik_runde2_bauen.py` (gleiche Regeln, nur numpy und ffmpeg), Messwerte `docs/assets/musik-messwerte-runde2.csv`, Ablauf in `docs/assets/prompts/musik-suno.md`. Neue Ordner: `glaube/` (Stücke der Kirchen) und `menue/` (Hauptmenü).
