# Töne der Benachrichtigungen: Siegel und Pergament (05.11.2026)

Keine KI, keine fremde Aufnahme: Die vier Töne werden **rein rechnerisch** erzeugt, mit dem Python-Skript `tools/ton/meldungstoene.py` (nur Standardbibliothek, ohne numpy). Aufruf:
`python3 tools/ton/meldungstoene.py game/assets/audio/geraeusche/oberflaeche`. Jeder Ton hat einen festen Startwert, ein neuer Lauf liefert dieselben Bytes.

| Datei | Wofür | Klang |
|---|---|---|
| `siegel_neu_1.wav`, `siegel_neu_2.wav` | ein neues Siegel erscheint in der Ecke | weicher Wachsklopfer (Sinus 150 bzw. 175 Hz, Tonhöhe fällt in 40 ms um die Hälfte), kurzes dunkles Schmatzen, ganz leises Tick; 0,32 s |
| `pergament_1.wav`, `pergament_2.wav` | ein Brief kommt oder wird aufgeschlagen, Blättern im Stapel | Rascheln: gefiltertes Rauschen (Hochpass 900 Hz, Tiefpass 6,5 kHz) mit unregelmäßigen Knacksern; 0,55 bzw. 0,7 s |

Format: WAV, Mono, 44,1 kHz, 16 Bit. Eingebaut in `game/scripts/KlangOberflaeche.cs` (Ereignisse `MeldungSiegel` und `MeldungBrief`, Bus „Oberflaeche“).
Der Ton wurde nicht abgehört (kein Audiogerät im Entwicklungsrechner); das Log von `--klangtest=1` belegt nur, dass alle Dateien laden.
