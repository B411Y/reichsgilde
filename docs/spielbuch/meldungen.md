# Meldungen

Was in der Stadt und im Haus geschieht, erfahrt ihr als **Siegel** in der Ecke unten rechts, als **Brief** oder (nur zum Nachlesen) im **Verlauf**. Die Zeile unten links gibt es nicht mehr; auch ein abgelehnter Befehl („✗“) erscheint unten rechts.

## Die vier Stufen

| Stufe | Wo | Beispiel |
|---|---|---|
| **Aus** | nirgends | – |
| **Verlauf** | nur in der Liste im Bericht | Bau und Wegebau, Kauf und Pacht, Kirche, **alle Züge der Rivalen** |
| **Siegel** | Wachssiegel in der Ecke unten rechts über der Betriebsleiste | Krankheit, Steuer, Jahreszeit, Handelsaufträge der Karren, ein Betrieb steht still |
| **Brief** | Pergament-Fenster unten rechts, das nichts verdeckt und **nie die Zeit anhält** | Todesfall, Geburt, Hochzeit, Einbruch, Blitz, Mahnung des Kämmerers, Jahresbericht |

**Siegel.** Die Farbe zeigt das Thema (rot Gefahr, blau Handel und Betrieb, grün Familie, gold Rat und Stand, grau Stadt und Wetter, ocker abgelehnte Befehle), das Zeichen in der Mitte ebenso. Mehrere Meldungen desselben Themas stapeln sich zu **einem** Siegel mit Zähler. Ein neues Siegel hat einen eigenen Ton (weicher Wachsklopfer). Ein Linksklick öffnet den Brief mit allen Meldungen des Stapels, ein Rechtsklick legt das Siegel beiseite. Abgelehnte Befehle verschwinden nach 25 Sekunden von selbst; alle anderen Siegel bleiben, bis ihr sie lest. Mehr als sieben Siegel passen nicht; das älteste weicht, es steht weiter im Verlauf. Tafeln, die rechts aufgehen, lassen den Platz der Ecke frei.

**Brief.** Pergament mit Absender („Von: Der Büttel“), Titel auf einem Farbband, den Zeilen und dem Siegel am Fuß. Kommen mehrere Briefe, bilden sie einen Stapel („Brief 2 von 3“, Pfeile zum Blättern); „Weglegen“ oder ✕ legt den Stapel weg. Der Knopf unten führt, soweit der Text es hergibt, zum Betrieb, ins Familienbuch, in die Ratskammer oder zur Jahresrechnung. Ein Brief kommt mit Pergamentrascheln, bei Gefahr dazu eine leise Glocke.

## Was gebündelt wird

- **Jahreswechsel:** Gerichtstag, Ratssitzung und Wahlen, Steuer, Stand, Ausbrüche und Spitzelberichte der Jahresabrechnung ergeben **einen** Brief „Jahresbericht“ (mit Gewinn des Jahres). Einzeln bleiben nur ein **Todesfall** und eine **Anklage gegen euer Haus**.
- **Stillstand** („Es fehlt …“, „Lager voll“): **ein Siegel je Betrieb**, erst wenn es etwa **eine Spielstunde** anhält. Das Siegel aktualisiert sich, bei „Die Arbeit läuft wieder“ verschwindet es. Der gewollte Umbau meldet nichts.
- **Die Nacht:** Einbrüche und Überfälle zwischen 20 und 23 Uhr kommen am nächsten Morgen als **ein** Brief „Die Nacht“. Ein abgewehrter Überfall ist nur ein Siegel.
- **Rivalen:** Was die anderen Häuser tun, steht nur im Verlauf.
- **Fremde Geheimnisse:** Nachrichten der Unterwelt und Spitzelberichte, die ein anderes Haus bestellt hat, seht ihr nie (früher gingen sie an alle).

## Der Verlauf

Im **Bericht**, Reiter **Verlauf**: alle Meldungen der Sitzung über die Jahre, die neueste oben, mit Jahr und Uhrzeit. Ein Punkt in der Farbe des Themas zeigt, ob sie ein Siegel oder Brief war (gefüllt) oder nur im Verlauf stand (Ring). Filter: **Alles**, **Mein Haus** (euer Haus und die Stadt), **Rivalen**. Der Verlauf wird noch nicht mit dem Spielstand gespeichert.

## Einstellungen

Unter **Einstellungen → Meldungen** wählt ihr für jede Art (Todesfälle, Geburten, Diebstahl, Stillstand, Steuern …) **Aus**, **Nur Verlauf**, **Siegel** oder **Brief**; „Auf die Vorgabe zurücksetzen“ stellt alles zurück. Die Wahl gilt für euer Haus und die Stadt, andere Häuser bleiben im Verlauf. Der Schalter **Siegel und Briefe bewegen** schaltet das Einblenden und leise Pulsieren ab (wie „Fahnen bewegen“).

## Grenzen (Stufe 1)

Der Kern meldet noch nur Text; die Zuordnung zu Arten steht in `game/daten/meldungen.json` und beruht auf Textmustern. Briefe haben noch keine Porträts, Sprünge zum Haus oder zur Person gibt es nur, wo der Text einen Betrieb nennt oder die Art ein Ziel hat. Entscheidungen mit Wahl (Heiratsantrag, Steuerforderung, Anklage) folgen später. Im Mehrspielerspiel pausiert nichts.

Zum Ausprobieren: `--meldungen=demo` (Siegel und ein Stillstand) oder `--meldungen=brief` (dazu zwei Briefe), `--einstellungen=1 --reiter=Meldungen`.
