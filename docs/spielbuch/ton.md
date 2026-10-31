# Ton und Ansagen

Lautstärken, Stumm und die Klänge der Oberfläche stehen bei den [Einstellungen](einstellungen.md) (Reiter Ton). Hier: die **Ansagen des Haushofmeisters** und die **Geräusche der Stadt**.

## Der Haushofmeister

Bei wichtigen Ereignissen **deines Hauses** spricht ein alter, ruhiger Haushofmeister einen kurzen Satz, zum Beispiel „Mein Herr, ein Glied Eures Hauses ist verschieden.“ Spielst du eine Frau (das Oberhaupt ist eine Frau), heißt es „Meine Herrin …“. Die Sätze nennen nie einen Namen; wer betroffen ist, steht wie immer in der Textmeldung unten links. Ereignisse fremder Häuser sagt er nie an (auch nicht im Mehrspielerspiel); nur die Pest in der Stadt trifft alle.

- **Abschalten:** Einstellungen, Reiter Ton, Schalter **Ansagen des Haushofmeisters**. Die Lautstärke hat der Regler **Stimmen**.
- **Nie zwei auf einmal:** Es spricht immer nur eine Ansage. Danach ist **20 Sekunden** Pause (echte Zeit, unabhängig vom Spieltempo). Kommen in der Pause weitere Ereignisse, wartet nur die **wichtigste**; was überholt ist oder länger als 45 Sekunden wartet, verfällt. Eine laufende Ansage wird nicht unterbrochen.
- **Vorrang:** Tod vor Feuer und Pest, diese vor Krankheit, Überfall, Einbruch, Anklage und Schulden, danach Wahl, Aufstieg, Geburt und Hochzeit, zuletzt Ratssitzung, Kasse, Lager und Jahresende.
- **Nicht zweimal hintereinander:** Dasselbe Ereignis wird erst nach einer Sperre (1 bis 3 Minuten echte Zeit) wieder angesagt, und von den drei Sätzen eines Ereignisses kommt nie derselbe zweimal nacheinander.
- Fehlt die Aufnahme zu einem Satz, bleibt es still; die Textmeldung steht trotzdem da.

## Die 20 Ereignisse

| Rang | Ereignis | Wann |
|---|---|---|
| 1 | Oberhaupt gestorben | das Oberhaupt stirbt, ein Erbe tritt an (stirbt es ohne Erben, endet die Partie) |
| 1 | Todesfall in der Familie | ein Angehöriger stirbt |
| 2 | Feuer | Brand im Wohnhaus oder Anschlag auf eine Werkstatt |
| 2 | Pest in der Stadt | die Pest bricht aus |
| 3 | Krankheit | ein Familienmitglied erkrankt (auch an der Pest) |
| 3 | Überfall | Räuber erbeuten die Fracht eines Karrens oder Schiffs (abgewehrte Überfälle nicht) |
| 3 | Einbruch | Diebe holen Beute aus dem Wohnhaus oder einer Werkstatt |
| 3 | Anklage | ein anderes Haus klagt dich an |
| 3 | Schulden | der Kämmerer mahnt |
| 4 | Wahl verloren | du standest zur Wahl und der Rat wählte einen anderen |
| 4 | Wahl gewonnen | du standest zur Wahl und wurdest gewählt oder bleibst im Amt |
| 4 | Aufstieg im Stand | dein Haus steigt in einen höheren Stand auf |
| 4 | Meisterprüfung | du bestehst die Prüfung zum nächsten Meistergrad |
| 4 | Geburt | ein Kind kommt zur Welt |
| 4 | Hochzeit | das Oberhaupt heiratet |
| 5 | Ratssitzung | die Sitzung beginnt (wenn du dort sitzt, etwas beantragt hast oder zur Wahl stehst) |
| 5 | Kasse knapp | ein Verwalter kauft nur noch das Nötigste |
| 5 | Lager leer | einem Betrieb fehlt der Rohstoff, die Arbeit ruht |
| 6 | Lager voll | das Lager einer Ware ist voll, die Werkstatt steht still |
| 6 | Jahresende | die Jahresabrechnung ist fertig |

Jedes Ereignis hat drei Sätze, jeder in der Fassung Herr und Herrin (120 Aufnahmen). Die ganze Liste steht in `game/daten/ansagen.json`.

## Die Stimme

Alle 120 Aufnahmen spricht dieselbe **KI-Stimme**: ein warmer, tiefer Bariton um die 60, ruhig und förmlich. Sie ist mit Qwen3-TTS auf dem Grafik-PC entstanden, als Klon einer einzigen Vorlage (`tools/ton/quellen/haushofmeister_probe2.wav`, ebenfalls KI, keine echte Person). Die Stimmung jedes Satzes – getragen bei Tod, drängend bei Feuer, freudig bei Hochzeit, sachlich bei Rat und Lager – kommt nur aus den Satzzeichen des Sprechtexts (`tools/ton/ansagen_sprechtext.json`: „…“, „!“, „–“); die Wörter sind dieselben wie in der Satzliste. Danach ist jede Aufnahme um **15 %** schneller gestellt (ffmpeg `atempo`, die Tonhöhe bleibt), auf −18 LUFS angeglichen und hat 0,2 s Stille an den Rändern. Von je drei Versuchen nahm das Skript den, den Whisper fehlerfrei versteht und der der Vorlage am ähnlichsten klingt.

**Einen Satz neu erzeugen** (gefällt einer nicht): Der Sprachdienst muss auf dem Grafik-PC laufen. Dann

```bash
<python mit den Prüfpaketen> tools/ton/ansagen_erzeugen.py --dienst http://<grafik-pc>:<port> --ffmpeg <pfad> --nur feuer_2,feuer_2_herrin --neu
```

`--nur` nimmt die Dateinamen ohne `.ogg`, `--neu` verwirft die alten Versuche. Soll er anders klingen, zuerst die Satzzeichen in `ansagen_sprechtext.json` ändern. Ohne `--nur` erzeugt das Skript alle fehlenden; `--durchhoeren alle.ogg` setzt alle Aufnahmen mit 0,8 s Pause hintereinander. Danach `python3 tools/ton/ansagen_liste.py --pruefen` und `--fehlende`, die Protokollzeile bleibt (gleiche Zahl), im Commit nennen, welche Sätze neu sind. Einzelheiten: `docs/assets/prompts/haushofmeister.md`.

## Die Geräusche der Stadt

In der Partie hört man die Stadt: tagsüber Arbeit, Wasser und ferne Stimmen, am Morgen Vogelchor und Hähne, abends gedämpftes Gemurmel wie aus Schenken, nachts fast Stille mit einem Kauz und einem fernen Hund. Im Hauptmenü und in der Kartenwerkstatt bleibt es still (die Probepartie hat die Geräusche). Die Lautstärke hat der Regler **Umgebung**.

| Wann | Was man hört |
|---|---|
| Morgengrauen (ab der Dämmerung bis 1,5 Stunden nach Sonnenaufgang) | Vogelchor, Hähne; die Stadt erwacht leise (vereinzelte Schritte) |
| Arbeitszeit (voll 08:30–19:00 Uhr) | Stadtleben, nah Gemurmel, Leute gehen vorbei, Spatzen, ab und zu Pferd, Gänse oder Hühner, fernes Hämmern aus einer Schmiede |
| Abendrot bis dunkel | Abendgesang der Vögel, Amselstrophen |
| Abend (20–22 Uhr, Wirtshaus) | gedämpftes Gemurmel, seltener Schritte, ab und zu ein ferner Hund |
| Nacht | stiller Grund, Waldkauz (erst wenn die Stadt ruhig ist), ferne Hunde |

- **Die Vögel folgen der Sonne**, die Stadt der Uhr: Im Winter ist es um 8 Uhr noch dunkel, aber die Stadt arbeitet schon; im Sommer singt der Morgenchor schon um 4 Uhr. Im Herbst und Winter singen die Vögel kaum.
- **Zoom:** Nah hört man das Gemurmel und die Schritte, am üblichen Blick (Startzoom) weniger davon. Weit herausgezoomt wird die Stadt leiser und dumpfer, und der Wind kommt; ganz oben weht er kräftiger.
- **Innen:** In einem Raum hört man draußen nur noch dumpf und leise, im Dom etwas gedämpft.
- **Pause:** Die Geräusche laufen leiser weiter (−8 dB), die Tageszeit steht, Rufe kommen seltener.
- **Spieltempo:** Die Geräusche folgen der Spielzeit, auch bei 32x, aber ohne Sprünge. Springt die Uhr (Jahreswechsel, Laden), blenden sie in 3 Sekunden über. Hahn, Hund und Kauz rufen nach echter Zeit mit Mindestabstand, bei hohem Tempo also nicht öfter.
- Hat das Fenster den Fokus verloren und ist „Ton im Hintergrund stumm“ an, ist alles still, und es kommen keine Rufe.

Alle Klänge sind CC0-Aufnahmen von freesound.org (Nummern, Urheber und Bearbeitung in `game/assets/audio/umgebung/HERKUNFT.md`) oder eigene Synthese (Wind, Nacht). Neu bauen: `tools/ton/umgebung_bauen.py` (Schnittliste `tools/ton/umgebung_schnitte.json`).

## Ausprobieren

`--ansagetest=tod_familie,feuer,krankheit` löst die Ansagen nacheinander aus (alle 0,5 s) und schreibt je Zeile ins Log, was daraus wurde (gespielt, wartet, verworfen, verfallen); `--ansagetest=alle` zeigt Vorrang und Pause an allen Ereignissen, `--ansagetest=dateien` zählt die Aufnahmen je Ereignis. Alle Aufnahmen einzeln anhören: Entwicklerfenster (F12), Reiter Klänge, unten die Dateien unter `stimmen/haushofmeister/`. Fehlt eine Aufnahme, spielt `--ansageplatzhalter=1` stattdessen einen Glockenton. Die Geräusche der Stadt: Entwicklerfenster (F12), Reiter Klänge, Abschnitt **Umgebung** (was gerade wie laut läuft, jede Datei zum Vorhören, „jetzt rufen“); `--umgebungtest=2` schreibt die Pegel jede Sekunde ins Log, `--umgebungfahrt=1` spielt eine feste Hörprobe ab (Morgen nah, herauszoomen, Mittag, Abend, Nacht, dann Mittag am Startzoom und Pause) und beendet das Spiel. Einzelheiten in [STARTOPTIONEN](../STARTOPTIONEN.md).

[Zurück zum Inhaltsverzeichnis](README.md)
