# Prompt: Ansagen des Haushofmeisters (Ton Schritt 3, 30.10.2026)

Dateien: `game/assets/audio/stimmen/haushofmeister/<ereignis>_<nr>[_herrin].ogg` (120), Klonvorlage `tools/ton/quellen/haushofmeister_probe2.wav`. Protokoll: `docs/assets/protokoll.csv`.

## Werkzeug

- Sprachdienst auf dem Grafik-PC des Nutzers („Reichsgilde TTS“, FastAPI) mit **Qwen3-TTS** (Alibaba Qwen, Apache-2.0), Modus **Stimmklon**: `POST /tts` mit `text`, `language=German`, `ref_audio` (Klonvorlage) und `ref_text`. Keine Stimmbeschreibung (VoiceDesign) und keine Stimmumwandlung (Seed-VC): beides klang in der Probe nach Nachhall bzw. Roboter (Nutzer, 30.10.2026). Modellangaben: siehe Protokoll.
- Klonvorlage `haushofmeister_probe2.wav` (6 s, 24 kHz): eine **KI-Stimme**, am 29.10.2026 mit demselben Dienst über eine Stimmbeschreibung (VoiceDesign) erzeugt, keine Aufnahme einer echten Person. Der genaue Wortlaut dieser Beschreibung ist nicht erhalten. Text der Vorlage (`ref_text`): „Herr, Euer Sohn ist erkrankt. Der Medicus ist bereits verständigt.“
- Aufrufskript: `tools/ton/ansagen_erzeugen.py` (Adresse des Dienstes nur als Argument). Prüfung mit faster-whisper (Modell „turbo“, large-v3-turbo, CPU int8) und SpeechBrain ECAPA (`speechbrain/spkrec-ecapa-voxceleb`); Nachbearbeitung mit numpy/scipy/pyloudnorm und ffmpeg.

## Ablauf

1. Je Satz und Fassung (Herr, Herrin) drei Takes. Gesprochen wird der **Sprechtext** aus `tools/ton/ansagen_sprechtext.json`: dieselben Wörter wie in `game/daten/ansagen.json`, Betonung nur über Satzzeichen („…“ getragen, „!“/„!!“ lebhaft oder drängend, „–“ kurzes Absetzen; Fassung „B-zeichen“ der Probe vom 30.10.2026).
2. Jeder Take: Nachbearbeitung (mono, 48 kHz, Stille an den Rändern auf 0,2 s, −18 LUFS, weiche Spitzenbegrenzung bei −1 dBFS), Tempo **×1,15** mit ffmpeg `atempo` (WSOLA, nicht rubberband), noch einmal Nachbearbeitung.
3. Auswahl automatisch: Whisper erkennt den Text fehlerfrei (WER 0), keine Übersteuerung im Rohtake, keine Pause über 1,2 s mitten im Satz, Sprechtempo 1,8–6 Silben/s, höchstens 8 s; unter den gültigen der Take mit der höchsten ECAPA-Ähnlichkeit zur Klonvorlage. Ist keiner gültig, bis zu drei weitere Takes.
4. Gewinner als OGG Vorbis (Qualität 5, mono, 48 kHz). Ergebnisse je Satz (Take, WER, Ähnlichkeit) schreibt das Skript nach `auswahl.json` im Arbeitsordner (außerhalb des Repositorys).

## Prompt

Die Prompts sind die Sprechtexte in `tools/ton/ansagen_sprechtext.json` (60 Sätze, je mit „Mein Herr“; die Fassung Herrin ersetzt nur die Anrede durch „Meine Herrin“).
