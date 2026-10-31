# Quelle: Umgebungsklänge der Stadt (Ton Schritt 4, 31.10.2026)

Dateien: `game/assets/audio/umgebung/schichten/*.ogg` (9 Schleifen) und `game/assets/audio/umgebung/rufe/*.ogg` (38 Einzelrufe). Protokoll: `docs/assets/protokoll.csv`; Herkunft je Datei: `game/assets/audio/umgebung/HERKUNFT.md`.

Kein KI-Werkzeug. Es gibt keinen Prompt, sondern eine **Schnittliste**: `tools/ton/umgebung_schnitte.json` nennt je Ausgabedatei die freesound-Nummer der Quelle (CC0 1.0, HQ-Vorschau mit SHA-256), Ausschnitt von–bis, Hoch- und Tiefpass, Länge der Schleifennaht; für die beiden Synthesen (Nacht, leichter Wind) Stufe und Saat. Gebaut wird mit `tools/ton/umgebung_bauen.py` (numpy, scipy, soxr, soundfile, pyloudnorm, ffmpeg mit libvorbis); ein zweiter Lauf ergibt byte-gleiche Dateien.

Ausgewählt nach Beschreibung, Schlagworten und Messung (Lautheit, Spektrum, Dauerton unter 150 Hz, Pegelverlauf, Sprache mit faster-whisper small mit und ohne Sprach-Detektor); **nicht angehört** – die Hörprobe des Nutzers steht vor der Übernahme.
