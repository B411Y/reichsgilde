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

In der Partie hört man die Stadt – und zwar **nur, wo auch Menschen sind**, und das Land, **wo das Land ist**. Auf leeren Straßen ist es still: Vögel, ein wenig Wind, ein Hauch Wasser, sonst nichts. Füllen sich die Gassen, steigt das Stadtleben; an Gruppen, die beieinanderstehen, hört man kurze **Gesprächsfetzen** (Lachen, Feilschen, unverständlich), am Markt mehr; aus der Schänke dringt es dumpf aus ihrer Richtung, in der Schmiede wird gehämmert, solange gearbeitet wird. Draußen auf Wiese und im Wald verstummt die Stadt, dafür rauscht das Laub, es zirpt, quakt und plätschert – je nach Ort und Jahreszeit. Im Hauptmenü und in der Kartenwerkstatt bleibt es still (die Probepartie hat die Geräusche). Die Lautstärke hat der Regler **Umgebung**.

| Was geschieht | Was man hört |
|---|---|
| Niemand draußen (Nacht, früher Morgen) | nur Vögel, Wind, Nachtgrund; keine Gespräche, keine Schritte |
| Menschen auf Gasse und Markt im Umkreis der Kamera | Stadtleben (Wasser, Arbeit, ferne Stimmen), je mehr Leute, desto voller |
| Zwei oder drei Leute stehen beieinander (Kunde am Stand, Plauder an der Ecke) | Gesprächsfetzen am Ort der Gruppe, leise, mit Abstand und Zufall; am Markt zum Feilschen, sonst in der Gasse |
| Jemand geht im Umkreis | Schritte, am Ort dieser Figur im Bild; je mehr gehen, desto öfter |
| Schänke offen (8–22 Uhr) mit Gästen | von draußen gedämpft wie durch die Tür; dicht davor und herangezoomt heller und lauter; **in der Schankstube selbst** (Innenansicht) laut und lebendig, nach der Zahl der Gäste |
| Schmiede oder Steinmetzhütte arbeitet (8–20 Uhr) | fernes Hämmern aus der Richtung des Betriebs |
| Fokus auf Wiese, Feld, Wald | Stadt verklingt; Vögel, Grillen (Sommer, abends), Krähen (Winter, Herbst), Laubrauschen im Wald, im Wind stärker |
| am Wasser (Fluss, Bach, Teich) | Plätschern; im Frühling abends und nachts Frösche |
| Regen | Regen auf Boden und Laub (leicht oder Guss), nah an der Stadt Tropfen auf den Dächern; Vögel schweigen, die Stadt wird dumpf und leiser |
| Gewitter | Der Donner folgt jedem Blitz mit der Verzögerung des Schalls (etwa 3 Sekunden je Kilometer): nah ein scharfer Knall mit langem Rollen, sehr laut; je weiter weg, desto später, leiser und dumpfer (nur tiefes Grollen); ab etwa 18 km nur noch Wetterleuchten ohne Donner. Der Blitz ist im Bild umso heller, je näher er einschlägt. Das Gewitter zieht heran (Regen und Böen nehmen zu, die Blitze rücken näher), geht über die Stadt und zieht weiter |
| Schnee | **Schneestille**: alles leiser und dumpfer, Vögel kaum |
| Wind (Wetter) | Flaute still, Sturm laut, auch nah |
| Arbeitsbeginn 8 Uhr | **Morgenglocke** (Geläut mehrerer großer Glocken) vom Dom |
| Messe (Stunde des Kirchenwesens, 10 Uhr) | **Messglocke**, Geläut |
| Arbeitsende 20 Uhr | **Abendglocke**, die große Glocke allein, dann weitere (Feierabend) |
| Schankschluss 22 Uhr | **Feuerglocke**, die große Glocke allein, langsam und schwer (Sperrstunde: Feuer abdecken, Gassen räumen) |
| Nacht, eine Streife mit Laterne ist unterwegs | ab und zu das **Horn des Nachtwächters** an der Streife |

- **Wer zählt:** alle Figuren draußen auf Gassen, am Tor und auf dem Markt im Umkreis der Kamera – auch hinter Häusern, nicht nur die im Bild. Wer in einem Haus, in der Kirche oder im Umland ist, zählt nicht. Der Umkreis wächst mit dem Zoom (nah etwa 40 m, Startzoom etwa 90 m, ganz oben etwa 120 m); wer nah ist, zählt ganz, am Rand immer weniger. Die Einstellung „Menge der Passanten“ gilt auch hier: Wer nicht gezeigt wird, ist nicht zu hören. Gesprächsfetzen tragen weniger weit (Zoom 40 etwa 26 m).
- **Stadt oder Land:** Um den Blickpunkt der Kamera wird gemessen, wie viel Stadt (Häuser, Gassen, Mauer) gegen Natur (Wald, Felder, Wiese, Wasser) liegt. Das Stadtleben folgt dem Stadtanteil; Grillen, Krähen, Laub, Frösche und Bach folgen dem, was um die Kamera wächst und fließt.
- **Weich:** Die Pegel steigen mit etwa 3 Sekunden und fallen mit etwa 6 Sekunden Zeitkonstante, nie sprunghaft; ein Wechsel von der Stadt aufs Land dauert etwa 3 Sekunden.
- **Nacht:** Das Mittelalter kannte kein Nachtleben: Nach dem Feierabend (20 Uhr) werden die Gassen still, nach dem Schankschluss um 22 Uhr (Feuerglocke) bleibt von den Gesprächen ein Zehntel. In einer echten Partie sind um 21 Uhr nur noch etwa 6 von 123 Einwohnern draußen (die Bürger-KI schickt sie vorher heim), die übrigen sitzen in der Schänke oder sind daheim.
- **Die Vögel folgen der Sonne**, die Stadt der Uhr: Im Winter ist es um 8 Uhr noch dunkel, aber die Stadt arbeitet schon; im Sommer singt der Morgenchor schon um 4 Uhr. Im Herbst und Winter singen die Vögel kaum; Grillen nur im Sommer, Frösche nur im Frühling.
- **Zoom:** Nah hört man die Gespräche und die Schritte, am üblichen Blick (Startzoom) weniger davon. Weit herausgezoomt wird die Stadt leiser und dumpfer, und der Wind kommt; die Glocken hört man auch dann.
- **Innen:** In einem Raum hört man draußen nur noch dumpf und leise, im Dom etwas gedämpft; **die Schankstube** klingt laut und lebendig (Stimmen, Lachen, Krüge) und nicht gedämpft wie ein Raum.
- **Pause:** Die Geräusche laufen leiser weiter (−8 dB), die Tageszeit steht, Rufe kommen seltener.
- **Spieltempo:** Die Geräusche folgen der Spielzeit, auch bei 32x, aber ohne Sprünge. Springt die Uhr (Jahreswechsel, Laden), blenden sie in 3 Sekunden über, und keine Glocke läutet. Hahn, Hund, Kauz, Gespräche und Horn kommen nach echter Zeit mit Mindestabstand, bei hohem Tempo also nicht öfter.
- **Gespräche austauschbar:** Welche Aufnahmen als Gesprächsfetzen klingen, steht in `game/daten/gespraeche.json` (Anlass → Dateien). Später können hier Gesprächsrunden der Stimm-KI stehen, ohne den Code anzufassen.
- Hat das Fenster den Fokus verloren und ist „Ton im Hintergrund stumm“ an, ist alles still, und es kommen keine Rufe.

Die Klänge sind Aufnahmen von freesound.org (CC0, und für Frösche, Regen und das Horn CC BY 4.0 mit Namensnennung; Nummern, Urheber, Lizenz und Bearbeitung in `game/assets/audio/umgebung/HERKUNFT.md`) oder eigene Synthese (Wind, Nacht). Neu bauen: `tools/ton/umgebung_bauen.py` (Schnittliste `tools/ton/umgebung_schnitte.json`).

## Ausprobieren

`--ansagetest=tod_familie,feuer,krankheit` löst die Ansagen nacheinander aus (alle 0,5 s) und schreibt je Zeile ins Log, was daraus wurde (gespielt, wartet, verworfen, verfallen); `--ansagetest=alle` zeigt Vorrang und Pause an allen Ereignissen, `--ansagetest=dateien` zählt die Aufnahmen je Ereignis. Alle Aufnahmen einzeln anhören: Entwicklerfenster (F12), Reiter Klänge, unten die Dateien unter `stimmen/haushofmeister/`. Fehlt eine Aufnahme, spielt `--ansageplatzhalter=1` stattdessen einen Glockenton. Die Geräusche der Stadt: Entwicklerfenster (F12), Reiter Klänge, Abschnitt **Umgebung** (was gerade wie laut läuft, jede Datei zum Vorhören, „jetzt rufen“); `--umgebungtest=2` schreibt die Pegel jede Sekunde ins Log, `--umgebungfahrt=1 --karte=greybox` spielt eine feste Hörprobe von 7 Minuten ab (früher Morgen mit Morgenglocke, Markttreiben mit Gesprächsfetzen und Messglocke, der Weg von der Stadt aufs Land und zum Fluss, Frühlingsabend mit Fröschen, Sommernacht mit Grillen, Winter mit Schnee und Krähen, Abend mit Abendglocke, die Schänke von außen, nah und von innen, Schankschluss mit Feuerglocke, Nacht mit Nachtwächter, Regen und Gewitter; die Menschen kommen dabei aus einem erfundenen Tagesablauf, Wetter und Karte sind echt) und beendet das Spiel. Im Entwicklerfenster steht unter „Umgebung“ eine Zeile mit den Menschen im Umkreis und den Pegeln. Einzelheiten in [STARTOPTIONEN](../STARTOPTIONEN.md).

[Zurück zum Inhaltsverzeichnis](README.md)
