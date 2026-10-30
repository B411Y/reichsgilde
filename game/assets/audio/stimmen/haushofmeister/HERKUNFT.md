# Herkunft: Ansagen des Haushofmeisters

120 gesprochene Ansagen (20 Ereignisse × 3 Sätze × Fassung Herr und Herrin), Dateinamen `<ereignis>_<nr>[_herrin].ogg`, Satzliste `game/daten/ansagen.json`.

| Schritt | Werkzeug | Lizenz und Hinweis |
|---|---|---|
| Sprache | Qwen3-TTS (Alibaba Qwen), Stimmklon, lokal auf dem Grafik-PC des Nutzers | Apache-2.0; erhebt keine Ansprüche an die Ausgaben |
| Klonvorlage | `tools/ton/quellen/haushofmeister_probe2.wav`, selbst mit Qwen3-TTS (Stimmbeschreibung) erzeugt | eigene KI-Stimme, **keine echte Person** und keine Aufnahme eines Sprechers |
| Auswahl | faster-whisper (large-v3-turbo) und SpeechBrain ECAPA, nur zum Prüfen | MIT bzw. Apache-2.0; ändern die Aufnahme nicht |
| Nachbearbeitung | `tools/ton/ansagen_erzeugen.py` mit ffmpeg (`atempo` ×1,15) | mono, 48 kHz, −18 LUFS, 0,2 s Stille an den Rändern, OGG Vorbis Qualität 5 |

Die Texte sind eigene Texte des Projekts. Ablauf, Sprechtexte und Auswahlregeln: `docs/assets/prompts/haushofmeister.md`. Die Dateien stehen unter „Alle Rechte vorbehalten“ (`LICENSE-INHALTE.md`).

Herkunft je Datei aller Assets (auch dieser): `docs/assets/protokoll.csv`.
