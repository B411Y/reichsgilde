# Jahreszeiten

Ein Spieltag ist ein Spieljahr, und jedes Jahr hat seine Jahreszeit: Frühling 1400, Sommer 1401, Herbst 1402, Winter 1403, dann wieder Frühling. Wie die Stadt dabei aussieht (Wiese, Laub, Schnee, Licht), steht unter [Stadt und Ansicht](stadt-und-ansicht.md). Seit Datenversion 67 hat die Jahreszeit auch **Folgen für die Wirtschaft** (Entscheidung des Nutzers vom 31.10.2026). Sie gelten das ganze Jahr. Zu Beginn jedes Jahres sagt eine Meldung, was die Jahreszeit bringt.

## Was jede Jahreszeit ändert

| Jahreszeit | Markt | Arbeit | Wege und Wasser |
|---|---|---|---|
| **Frühling** | Frühjahrsmarkt 10–16 Uhr | wie im Sommer | Schlamm: Karren fahren auf Trampelpfaden 30 % und auf Fahrwegen 20 % länger, auf Kies und Pflaster wie sonst; aus dem Umland ohne Weg auf der Karte 15 % länger |
| **Sommer** | wie in den Daten | wie in den Daten | wie in den Daten |
| **Herbst** | Ernte: Bauern bringen 25 % mehr Gerste und Roggen und 20 % mehr Kräuter auf den Markt, die Preise fallen; Herbstmarkt 10–16 Uhr | Bauernhof +20 %, Kräutergarten +15 % | wie im Sommer |
| **Winter** | Nachfrage nach Brennholz +40 %, Kerzen und Kräuterschnaps +25 %, Heilsalbe +15 %, Kräuterlikör und Weingeist +10 % | Holzschlag −15 %, Steinbruch und Bauernhof −20 %, Kräuter- und Duftgarten −25 %; Grube, Klosterhof und alle Werkstätten wie sonst | im strengen Winter friert der Fluss zu (siehe unten) |

Die Preise folgen der Nachfrage über die Elastizität des Marktes: 40 % mehr Nachfrage nach Holz heben den Preis um gut 18 %, 25 % mehr Gerste senken ihn um gut 10 %.

## Der strenge Winter

Etwa jeder dritte Winter ist **streng** (der Rhein fror um 1400 in harten Wintern zu, etwa im Winter 1407/08). Die Meldung zum Jahresbeginn kündigt ihn an: „Ein strenger Winter: Eis treibt auf dem Rhein, gegen 8 Uhr friert er zu.“

- Die Nachfrage des Winters wächst noch einmal um die Hälfte ihres Zuschlags: Brennholz +60 % statt +40 %, Kerzen +37,5 % statt +25 %.
- Zwischen 7 und 10 Uhr **friert der Fluss zu**, für 6 bis 14 Stunden – je kälter, desto länger, manchmal bis zum Ende des Winters. Die Meldung: „Der Rhein ist zugefroren! Hafen und Fähre sind geschlossen, 1 Schiff ruht bis zum Eisgang – bis das Eis bricht.“
- Solange der Fluss zu ist, **nimmt der Hafen keine Aufträge an**, und **alle Schiffe ruhen** – auch die auf dem Fluss und in der fernen Stadt. Sie kommen um die Eiszeit später heim. Den Unterhalt zahlen sie trotzdem.
- Die **Fähre** liegt fest: Ein Karren, der über sie muss, wartet 120 statt 30 Minuten.
- Bricht das Eis, meldet das Spiel den Eisgang, und Hafen und Fähre arbeiten wieder.

Wer im strengen Winter ein Schiff losschickt, bevor der Fluss zufriert, riskiert, dass es im Eis festsitzt.

## Festmärkte

Im Frühling und im Herbst ist von 10 bis 16 Uhr **Festmarkt** (Frühjahrsmarkt und Herbstmarkt): Händler und Käufer kommen von weit her, die Nachfrage nach allen Waren steigt um 20 % (die Preise um gut 9 %), die Wirtshäuser haben 30 % mehr Gäste. Beginn und Ende stehen in den Meldungen.

## Die KI-Häuser

Die KI-Häuser wissen dasselbe wie du:
- Im strengen Winter lassen sie ihre Schiffe am Kai, schon bevor der Fluss zufriert, und schicken sie erst nach dem Eisgang wieder los.
- Im Herbst ab 17 Uhr füllen ihre Verwalter die Zutaten, die im Winter teurer werden oder jetzt mit der Ernte billig sind (Holz, Gerste, Roggen, Kräuter), bis zur Lagergrenze auf.

## Für Entwickler

Alle Werte stehen im Abschnitt `jahreszeiten` von `game/daten/wirtschaft.json`, der Vorrat der KI unter `ki.jahreszeiten`. Fehlt der Abschnitt (alte Spielstände bringen ihre Daten mit), hat die Jahreszeit keine Wirkung auf die Wirtschaft. Die Rechnung steht in [WIRTSCHAFTSMODELL.md](../WIRTSCHAFTSMODELL.md), „Jahreszeiten“.
