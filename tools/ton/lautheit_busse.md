## Lautheitsziele je Klasse (Empfehlung, Schritt 7)

Der Plan nannte kein Ziel; die Werte sind die schon gebauten Pegel, auf eine Tabelle gebracht und ergänzt. Alle Zahlen: LUFS bzw. dB am Master bei Regler 100 % (= 0 dB), vor dem Ducking.

| Klasse (Bus) | Ziel der Dateien | im Spiel wirksam | Begründung |
|---|---|---|---|
| Stimmen (Stimmen) | −18 LUFS integriert, Spitze unter −0,7 dBTP | −18 (Spieler 0 dB) | Sprache soll am deutlichsten sein; die Ansagen sind selten und kurz, ein Abstand von mindestens 8 dB zur Musik und 12 dB zur Umgebung hält sie verständlich, ohne die Musik zu ducken (die Musik duckt dennoch um 9 dB bei einer Ansage) |
| Musik (Musik) | −17 LUFS integriert (Datei) | Partie −26 (Spieler −9 dB), Menü −21 (−4 dB), Ereignisstück −23 (−6 dB) | lange, gleichmäßige Hörzeit: lieber leise unter der Handlung; im Menü gibt es nichts darüber, dort lauter |
| Umgebung (Umgebung und Unterbusse) | −26 LUFS je Schicht und Ruf | Summe der Stadt gedeckelt bei −30 LUFS, Pause −8 dB, Innenraum −14 dB | Hintergrund ohne Dauerdruck; einzelne Ereignisse (Donner −16/−18, Einschlag −14, Glocken −17, Regen) liegen absichtlich darüber |
| Oberfläche (Oberflaeche) | höchste 400-ms-Lautheit der Datei, je Ereignis angeglichen | Bedienung (Klick, Reiter, Tempo) −32, Fenster −30, Zahlung und Münzen −24, Fehler −25, Meldungstöne −26 bis −23, Siegel und Bauen −22, Türen −22, Gefahrglocke −32,5 | häufige Klänge leise (Bedienung), seltene mit Bedeutung lauter (Handlung, Meldung); Maß der 400-ms-Spitze, weil die Klänge kürzer als die 3 Sekunden der integrierten Messung sind |

## Pegel je Bus

Alle Bus-Pegel bleiben bei **0 dB** (`game/default_bus_layout.tres`, unverändert); die Hauptbusse sind nur die Regler des Spielers. Begründung: Die Pegelverhältnisse zwischen den Klassen stecken schon in den Dateien (−17/−18/−26 LUFS) und in den Spielerpegeln des Codes (Musik −9/−4/−6 dB, Ansagen 0 dB, Umgebung nach `KlangUmgebungModell`), die Unterbusse der Umgebung ändern ihren Pegel laufend (Raum, Dom, Pause, Regen, Entfernung) und dürfen darum keinen festen Vorsatz tragen. Ein fester Bus-Pegel würde zwei Stellen zu einer machen und die Umgebungsrechnung (Deckel bei −30 LUFS) verschieben.

| Bus | Pegel | Dämpfung, die im Betrieb dazukommt |
|---|---|---|
| Master | 0 dB, Regler 80 % = −1,9 dB | – |
| Musik | 0 dB | Ducking −9 dB bei Ansagen (am Spieler, Einblenden 0,35 s, Zurück 1,5 s) |
| Umgebung | 0 dB | – (Ducking bei Ansagen gibt es nicht; Empfehlung: bleibt so, die Ansagen liegen schon mehr als 12 dB über der Umgebung) |
| Effekte | 0 dB | – |
| Oberflaeche | 0 dB | – |
| Stimmen | 0 dB | – |
| UmgebungDraussen / -Stadt / -RufNah / -RufFern / -Schenke / -Gespraech / -Glocke / -Innen / -Donner | 0 dB | Pegel laufend durch `KlangUmgebungModell`; Donner mit Begrenzer |

## Was Schritt 7 geändert hat

- Gemessen wurden alle 302 Audiodateien; **keine Datei wurde verändert**.
- Musik (48 Stücke) liegt bei −17,0 LUFS ±0,1 (Herbstrauschen −18,4 wegen der Spitze), Ansagen (120 Sätze) bei −18,0 LUFS ±0,1 (eine −18,8): schon einheitlich, kein Eingriff.
- Die Umgebung liegt je Gruppe bei dem Wert, den ihre Pegeltabelle im Code erwartet (Schichten −26, Rufe −24 bis −14, Glocken −17); die Rufe unterscheiden sich absichtlich.
- **Oberflächenklänge**: Die Varianten eines Ereignisses waren bis zu 14 dB verschieden laut (Klick: −23,5 bis −36,4 M max). Jetzt gleicht `game/daten/klang_lautheit.json` jede Variante auf das Ziel ihres Ereignisses an (Tabelle unten, „Angleichung“); die Pegel-Spalte der Tabelle in `KlangOberflaeche.cs` blieb unverändert.
- Spitzen nach der Angleichung und der Tabelle: höchstens −3,6 dBTP (rechnerisch, `docs/assets/lautheit-angleichung.csv`).
