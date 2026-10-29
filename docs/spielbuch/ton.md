# Ton und Ansagen

Lautstärken, Stumm und die Klänge der Oberfläche stehen bei den [Einstellungen](einstellungen.md) (Reiter Ton). Hier: die **Ansagen des Haushofmeisters**.

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

## Ausprobieren

`--ansagetest=tod_familie,feuer,krankheit` löst die Ansagen nacheinander aus (alle 0,5 s) und schreibt je Zeile ins Log, was daraus wurde (gespielt, wartet, verworfen, verfallen); `--ansagetest=alle` zeigt Vorrang und Pause an allen Ereignissen. Solange noch keine Aufnahmen da sind, spielt `--ansageplatzhalter=1` stattdessen einen Glockenton. Einzelheiten in [STARTOPTIONEN](../STARTOPTIONEN.md).

[Zurück zum Inhaltsverzeichnis](README.md)
