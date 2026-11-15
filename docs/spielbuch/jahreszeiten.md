# Jahreszeiten

Ein Spieltag ist ein Spieljahr, und jedes Jahr hat seine Jahreszeit: Frühling 1400, Sommer 1401, Herbst 1402, Winter 1403, dann wieder Frühling. Wie die Stadt dabei aussieht (Wiese, Laub, Schnee, Licht), steht unter [Stadt und Ansicht](stadt-und-ansicht.md). Seit Datenversion 69 hat die Jahreszeit auch **Folgen für die Wirtschaft** (Entscheidung des Nutzers vom 31.10.2026). Sie gelten das ganze Jahr. Zu Beginn jedes Jahres sagt eine Meldung, was die Jahreszeit bringt.

## Was jede Jahreszeit ändert

| Jahreszeit | Markt | Arbeit | Wege und Wasser |
|---|---|---|---|
| **Frühling** | Frühjahrsmarkt 10–16 Uhr (Fertigwaren) | wie im Sommer | Schlamm: Karren fahren auf Trampelpfaden 30 % und auf Fahrwegen 20 % länger, auf Kies und Pflaster wie sonst; aus dem Umland ohne Weg auf der Karte 15 % länger |
| **Sommer** | wie in den Daten | wie in den Daten | wie in den Daten |
| **Herbst** | Ernte: Bauern bringen 25 % mehr Gerste und Roggen und 20 % mehr Kräuter auf den Markt, die Preise fallen; Herbstmarkt 10–16 Uhr (Fertigwaren) | Bauernhof +20 %, Kräutergarten +15 % | wie im Sommer |
| **Winter** | Nachfrage nach Brennholz +40 %, Kerzen und Kräuterschnaps +25 %, Heilsalbe +15 %, Kräuterlikör und Weingeist +10 % | Holzschlag −15 %, Steinbruch und Bauernhof −20 %, Kräuter- und Duftgarten −25 %; Grube, Klosterhof und alle Werkstätten wie sonst | im strengen Winter friert der Fluss zu (siehe unten) |

Die Preise folgen der Nachfrage über die Elastizität des Marktes: 40 % mehr Nachfrage nach Holz heben den Preis um gut 18 %, 25 % mehr Gerste senken ihn um gut 10 %.

## Der strenge Winter

Etwa jeder dritte Winter ist **streng** (der Rhein fror um 1400 in harten Wintern zu, etwa im Winter 1407/08). Die Meldung zum Jahresbeginn kündigt ihn an: „Ein strenger Winter: Eis treibt auf dem Rhein, gegen 8 Uhr friert er zu.“

- Die Nachfrage des Winters wächst noch einmal um die Hälfte ihres Zuschlags: Brennholz +60 % statt +40 %, Kerzen +37,5 % statt +25 %.
- Zwischen 7 und 10 Uhr **friert der Fluss zu**, für 6 bis 14 Stunden – je kälter, desto länger, manchmal bis zum Ende des Winters. Die Meldung: „Der Rhein ist zugefroren! Hafen und Fähre sind geschlossen, 1 Schiff ruht bis zum Eisgang – bis das Eis bricht.“
- Solange der Fluss zu ist, **nimmt der Hafen keine Aufträge an**, und **alle Schiffe ruhen** – auch die auf dem Fluss und in der fernen Stadt. Sie kommen um die Eiszeit später heim. Den Unterhalt zahlen sie trotzdem.
- Die **Fähre** liegt fest: Ein Karren, der über sie muss, wartet 120 statt 30 Minuten.
- Bricht das Eis, meldet das Spiel den Eisgang, und Hafen und Fähre arbeiten wieder.
- Ruht der Schiffshandel (Datenversion 70), gibt es keine Schiffe; der Hafen ist dann nur ein zweiter Ort zum Handeln mit dem Markt der Stadt und im ganzen Winter geschlossen (Eisschollen); der Marktplatz bleibt offen.

Wer im strengen Winter ein Schiff losschickt, bevor der Fluss zufriert, riskiert, dass es im Eis festsitzt.

## Erntejahr und Teuerung

Seit Datenversion 91 („Härte um 1400“, Nutzer 15.11.2026) ist nicht jede Ernte gleich. Jede Ernte im **Herbstjahr** (1402, 1406, …) ist **gut**, **gewöhnlich**, **mager** oder eine **Missernte**. Sie gilt bis zur nächsten Ernte, also vier Spieljahre (Herbst, Winter, Frühling, Sommer). Sie ändert, wie viel Bauern und Händler auf den Markt bringen:

| Ernte | Gerste, Roggen | Weingeist, Körnermahlzeit, Dünnbier (seit Datenversion 92) |
|---|---|---|
| gut | 50 % mehr (Preis knapp 20 % tiefer; bis Datenversion 91: 25 % mehr) | 12,5 % mehr |
| gewöhnlich | wie sonst | wie sonst |
| mager | 25 % weniger (Preis gut 15 % höher) | 6,25 % weniger |
| Missernte | halb so viel (Preis gut 40 % höher) | 12,5 % weniger |

Die Werte gelten für „Wie überliefert“. Was Spieler und KI-Häuser selbst verkaufen, drückt den Preis wie immer. Die Preisgrenzen des Markts (halber bis doppelter Grundpreis) bleiben.

- **Ansage ein Jahr vorher:** Zu Beginn des Sommerjahres meldet das Spiel, wie die Ernte im Herbst ausfällt, z. B. „Ein nasser Sommer: Im Herbst 1406 droht eine magere Ernte – Gerste, Roggen, … werden teurer. Wer jetzt einlagert, verkauft später besser.“ Die Ansage trifft immer ein. Zu Beginn des Herbstjahres kommt die Meldung zur Ernte selbst.
- **Wo man es sieht:** Im Marktfenster steht über den Waren die gültige Ernte (Jahr und Güte) und im Sommerjahr die Ansage; der Mauszeiger über einer Getreideware zeigt „Ernte 1402: eine Missernte – Menge ×0,6“. Wie oft und wie stark Missernten kommen, wählt man beim Start unter „Neue Partie“ → „Erweitert“ → „Härte der Zeit“ (Milde Jahre, Wie überliefert, Harte Jahre).
- **Wer Vorräte hat, gewinnt:** Wer im Sommer vor einer schlechten Ernte Getreide einlagert, verkauft es danach teurer. Brauer und Wirte zahlen mehr für Gerste, bekommen aber auch mehr für Bier und Mahlzeit.
- **Keine Abwärtsspirale:** Nach einer Missernte ist die nächste Ernte nie mager und keine Missernte (Schonjahr). Der Faktor jeder Ware bleibt zwischen 0,35 und 1,5.
- **Dein eigener Bauernhof** liefert in einem schlechten Jahr auch weniger, aber nur halb so stark wie der Markt (Missernte: drei Viertel). Den höheren Preis bekommst du trotzdem – wer selbst erntet, kommt glimpflich davon, wer zukauft, zahlt drauf.

## Lagen der anderen Rohstoffquellen

Seit Datenversion 92 (Nutzer 15.11.2026) gilt dasselbe für **jede Rohstoffquelle**, nicht nur für das Getreide – sonst träfe es nur den Wirt. Jede Quelle hat alle vier Jahre eine **Lage**: gut, gewöhnlich, schlecht oder schwer, mit genau denselben Chancen, Stärken, Grenzen, Schonjahren und der Ansage ein Jahr vorher wie die Ernte (gut = „gute Ernte“, schlecht = „mager“, schwer = „Missernte“). Jeder Beruf mit Rohwaren ist also gleich oft betroffen. Eine gute Lage bringt halb so viel mehr, wie eine Missernte wegnimmt (×1,5 gegen ×0,5) – so gleichen sich gute und schlechte Jahre im Mittel aus, und die Lagen machen die Rohwaren auf Dauer nicht teurer. Die Lage verschiebt die Rohwaren der Quelle voll und die Waren der ersten Werkstatt zu einem Viertel; dein eigener Umlandbetrieb liefert halb so stark mehr oder weniger.

| Quelle (Beruf) | beginnt im | gut | schlecht | schwer |
|---|---|---|---|---|
| Bauernhof (Wirt) | Herbst | gute Ernte | magere Ernte | Missernte |
| Grube (Schmied) | Frühling | reiche Ader (Silber voll, Eisen halb) | Wassereinbruch | Stollen eingestürzt |
| Holzschlag (Tischler) | Winter | Sturmbruch | grundlose Wege | Bannwald |
| Steinbruch (Steinmetz) | Herbst | neue Bank (Granit voll, Bruchstein halb) | Wasser im Bruch | Felssturz |
| Kräutergarten (Alchimist) | Sommer | feuchtes Jahr | Dürre (Kröten halb) | Kräuter-Missernte |
| Klosterhof (Prediger) | Frühling | gutes Bienenjahr | kalter Frühling | Bienensterben |
| Duftgarten (Parfumeur) | Sommer | üppige Blüte | Spätfrost | Hagelschlag |

**Wo man es sieht:** Ansage und Beginn kommen als Meldung (nur gute, schlechte und schwere Lagen). Im Marktfenster stehen unter der Ernte die gültigen und angesagten Lagen („Grube 1404: Wassereinbruch – Eisen und Silber, bis 1407“); der Mauszeiger über einer betroffenen Ware zeigt „Grube 1404: Wassereinbruch – Eisen ×0,8“ bzw. die Ansage. Der Jahresbericht hat einen Abschnitt „Lagen im Jahr“.

Folgewaren (je ein Viertel): Beschlag, Dolch, Silberring · Holzzapfen, Schemel, Truhe · Schleifziegel, Grabstein, Mühlstein · Farbe, Kräuterschnaps, Heilsalbe · Papier, Kerze, Lehrschrift · Politur, Blume der Zwietracht, Giftdolch. Beim Klosterhof trifft es Wachs voll und Lumpen halb.

Die Ansage kommt zu Beginn des Jahres davor, z. B. „Die Schneeschmelze drückt Wasser in die Stollen: Im Frühling 1404 droht ein Wassereinbruch in den Gruben – Eisen und Silber werden teurer. Wer jetzt einlagert, verkauft später besser.“ Gewöhnliche Lagen werden nicht gemeldet (außer bei der Ernte). Dieb, Geldleiher und Gardist haben keine Rohwaren und keine Lage; sie spüren nur die Preise.

### Härte der Zeit

Beim Start der Partie wählst du, wie hart die Jahre sind (die Auswahl unter „Neue Partie“ kommt mit der Oberfläche; bis dahin gilt der Standard). Die Wahl gilt für alle Häuser und steht im Spielstand.

| Stufe | gute Ernte | magere Ernte | Missernte | Stärke |
|---|---|---|---|---|
| Milde Jahre | 20 % | 15 % | nie | halb (gut: 25 % mehr, mager: 12,5 % weniger Getreide) |
| **Wie überliefert** (Standard) | 20 % | 25 % | 10 % | wie in der Tabelle oben |
| Harte Jahre | 15 % | 30 % | 20 % | 1,25-fach (gut: 50 % mehr, die Grenze; Missernte: 62,5 % weniger Getreide) |

Der Rest der Ernten ist gewöhnlich. Ob eine Ernte gut oder schlecht ist, hängt nur vom Startwert der Partie ab – jeder Mitspieler und jede geladene Partie hat dieselben Ernten.

## Festmärkte

Im Frühling und im Herbst ist von 10 bis 16 Uhr **Festmarkt** (Frühjahrsmarkt und Herbstmarkt): Händler und Käufer kommen von weit her, die Nachfrage nach Fertigwaren (alles, was der Markt-Reiter unter „Fertigwaren“ zeigt, auch der Kräuterschnaps) steigt um 20 % (ihre Preise um gut 9 %), die Wirtshäuser haben 30 % mehr Gäste. Speisen und Getränke der Wirtshäuser steigen am Markt nicht – ihre Mehrnachfrage sind die zusätzlichen Gäste. Rohstoffe und Zwischenwaren bleiben, wie sie sind – sonst zahlten die Werkstätten mehr für Getreide und Rohstoffe, und der Wirt, der zu festen Preisen ausschenkt, verlöre. Wer eine Fertigware weiterverarbeitet (Werkzeug für die Steinfigur, Dolche für die Giftdolche), zahlt am Festmarkt dafür mehr. Beginn und Ende stehen in den Meldungen.

## Die KI-Häuser

Die KI-Häuser wissen dasselbe wie du:
- Im strengen Winter lassen sie ihre Schiffe am Kai, schon bevor der Fluss zufriert, und schicken sie erst nach dem Eisgang wieder los. Damit nach dem Eisgang oder zum Festmarkt nicht alle zugleich mit derselben Fracht zur selben Stadt fahren, rechnet jedes Schiff mit denen, die schon unterwegs sind (siehe [Wirtschaft und Handel](wirtschaft-und-handel.md), Hafen und Schiffe).
- Im Herbst ab 17 Uhr füllen ihre Verwalter die Zutaten, die im Winter teurer werden oder jetzt mit der Ernte billig sind (Holz, Gerste, Roggen, Kräuter), bis zur Lagergrenze auf.
- Erntejahre und Lagen treffen sie nach denselben Regeln wie dich: Sie kaufen und verkaufen zu den Preisen des Markts, ihre Umlandbetriebe liefern wie deine. Auf die Ansage hin lagern sie noch nicht gezielt ein.

## Für Entwickler

Alle Werte stehen im Abschnitt `jahreszeiten` von `game/daten/wirtschaft.json`, die Ernte im Abschnitt `ernte`, der Vorrat der KI unter `ki.jahreszeiten`. Fehlt der Abschnitt (alte Spielstände bringen ihre Daten mit), hat die Jahreszeit keine Wirkung auf die Wirtschaft. Die Rechnung steht in [WIRTSCHAFTSMODELL.md](../WIRTSCHAFTSMODELL.md), „Jahreszeiten“.
