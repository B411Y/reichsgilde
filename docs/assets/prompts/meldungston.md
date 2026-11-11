# Prompts: Meldungstöne (Siegel, Brief, Entscheidung, Fehler, Mahnung; 11.11.2026)

Keine KI, keine fremde Aufnahme, nichts aus dem Original-Spiel: Die fünf Töne werden **rein rechnerisch** erzeugt mit `tools/ton/meldungston_erzeugen.py` (Python mit numpy, ffmpeg mit libvorbis für das OGG).
Aufruf vom Wurzelordner des Repos: `python -I tools/ton/meldungston_erzeugen.py game/assets/audio/geraeusche/oberflaeche --ffmpeg <Pfad>`. Jeder Ton hat feste Startwerte, ein neuer Lauf liefert dieselben Samples
(die WAV vor dem Kodieren sind bitgleich zu den Klangrunden, der Kurzhash wird ausgegeben; das OGG ist nicht bytegleich, der Ogg-Container trägt eine Seriennummer).

Klangfamilie: freier Metallstab (Glockenspiel), Partiale 1 : 2,76 : 5,40 mit leisen Obertönen (`WEICH`), Töne im F-Dur-Raum, ein Faltungshall aus abklingendem Rauschen, bei einigen ein kurzer leiser Glitzer (Glissando).
Lautheit je Datei: Verstärkung auf −20,0 LUFS (Spitze der 400-ms-Lautheit, K-gewichtet, BS.1770, ohne Gate), kein Begrenzer; 3 ms Einblenden, Kosinus-Ausblenden. Das Maß liegt auf den übrigen Oberflächentönen; die Pegel im Spiel setzt `KlangOberflaeche`.

| Datei | Anlass | Rezept (Klangrunde, Name dort) | Länge |
|---|---|---|---|
| `meldung_siegel.ogg` | ein neues Siegel in der Siegel-Ecke | Runde 2, `siegel_a_tief2_weich`: Arpeggio F5 A5 C6 F6, Abstand 55 ms, Glitzer 0,22 s, Hall RT60 0,9 s, Startwert 11 | 0,80 s |
| `meldung_brief.ogg` | ein Brief kommt, wird aufgeschlagen, Blättern im Stapel | Runde 3, `brief_2`: Lauf F5 G5 A5 C6 D6 F6, Abstand 50 ms, mit Glitzer, RT60 1,1 s, Startwert 42 | 1,15 s |
| `meldung_entscheidung.ogg` | eine neue Entscheidung (Entscheidungsbrief, Feder-Siegel) | Runde 3, `entscheidung_3`: Lauf auf und ab F5 A5 C6 F6 C6 A5 (75 ms) mit nachklingendem F5, RT60 1,3 s, Startwert 53 | 1,40 s |
| `meldung_fehler.ogg` | ein abgelehnter Befehl „✗“ | Runde 3, `fehler_2`: gedämpfte Töne F5, C5 abwärts (fast nur Grundton, `DUMPF`), kurzer Hall, Startwert 62 | 0,65 s |
| `meldung_mahnung.ogg` | Mahnung des Kämmerers | Runde 4, `mahnung_neu_1`: ein einzelner ernster Schlag F4 mit sehr leisem Glitzer, RT60 1,1 s, Startwert 81 | 0,95 s |

Wahl des Nutzers nach den Hörproben der Runden 1 bis 4 (11.11.2026). Verworfene Fassungen (siegel_a..d, brief_a..d, entscheidung_a..d, weitere Rezepte der Runden 2 bis 4) kommen nicht ins Repository.
