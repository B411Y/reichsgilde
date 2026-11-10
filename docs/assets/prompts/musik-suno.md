# Musik der Stadt (Suno, 10.11.2026)

Dateien: `game/assets/audio/musik/<lage>/<lage>[_<zusatz>]_<titel>.ogg` (23 Stücke; Lagen `tag`, `nacht`, `tod`, `event`). Titel und Zuordnung: `game/daten/musik.json`. Messwerte: `docs/assets/musik-messwerte.csv`.

## Werkzeug

- **Suno** (bezahltes Abo, höchste Stufe; die Nutzungsbedingungen des Abos erlauben die kommerzielle Nutzung der erzeugten Stücke), vom Nutzer selbst bedient. Welche Modellversion (v4, v5 …) die einzelnen Stücke erzeugte, steht nicht fest: **unbekannt**.
- Die Prompts (Stilbeschreibungen) und Texte hat der Nutzer in Suno eingegeben; sie liegen dem Repository nicht vor: **unbekannt**. Die Titel stammen aus Suno bzw. vom Nutzer (die Dateinamen der Quellen sind `<lage>_[<zusatz>_]<Titel>.wav`).
- Die Quellen sind WAV-Dateien (Suno-Export) und liegen nur auf dem Rechner des Nutzers; im Repository stehen nur die aufbereiteten OGG.

## Nachbearbeitung (eigenes Skript `tools/ton/musik_bauen.py`)

Quelldatei einlesen, Stille an Anfang und Ende kürzen (unter −55 dBFS, gemessen in 20-ms-Blöcken), Anfang 0,8 s und Ende 4 s mit Kosinus weich ein- und ausblenden, Lautheit nur durch **Verstärkung** (kein Begrenzer, keine Kompression) auf −17 LUFS integriert (ITU-R BS.1770, pyloudnorm); liegt dann der True-Peak (vierfach überabgetastet) über −1,3 dBTP, wird das Ziel für dieses Stück gesenkt (bisher nur Herbstrauschen: −18,4 LUFS). Danach als OGG Vorbis (Qualität 0,4, stereo, Abtastrate der Quelle) geschrieben, wieder gelesen und die Werte am entschlüsselten Stück gemessen; über −1 dBTP würde neu kodiert. Der Dateiname: Kleinbuchstaben, Umlaute umschrieben, Leerzeichen als Unterstrich.

## Hörprobe

Die Stücke sind vom Nutzer erzeugt und gehört; die Aufbereitung (Pegel, Blenden) und die Zuordnung zu Lagen im Spiel sind nur gemessen, nicht abgehört.
