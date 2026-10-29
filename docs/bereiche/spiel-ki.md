# Bereich Spiel-KI – Offen, Entscheidungen, Messungen

> **Archiv:** Ältere und abgelöste Entscheidungen (bis 24.10.2026) stehen in [archiv/spiel-ki.md](archiv/spiel-ki.md). Zum Arbeiten genügt diese Datei; das Archiv nur bei Bedarf lesen.

**Nur der Spiel-KI-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (von Kern) Die KI-Häuser nutzen die Werkstatt wie in Gilde 1 noch nicht ganz (PR #152): Sie zahlen üblichen Lohn ohne Prämie und teilen keine Arbeiter einzeln zu (Einrichtung kaufen sie seit PR #177, befördern seit PR #224). Abfragen: `Stadtwirtschaft.Werkstaetten` (`UnfallGefahr`), Befehl `LohnstufeSetzen`.
- (von Kern) Die KI setzt fast jedes Jahr einen Spitzel (300 Gulden) auf das stärkste andere Haus an, auch mit nur einem Betrieb: Beim Messspieler des Balancing-Berichts ≈ 210 im Jahr, rund ein Zehntel des Gewinns (PR #177, WIRTSCHAFTSMODELL „Balancing-Messung 2“). Vorschlag: an Gewinn oder Rang koppeln.
- (von Kern) Abfrage für den Kauf von Einrichtung (PR #177): `Werkstattwesen.ErwarteterMehrgewinnJeJahr(Betrieb, kennung)` und `Amortisationsjahre(…)`; Eisentür und Löschfass bringen nur etwas nach Raub bzw. Brand im Vorjahr (`Betriebsrechnung.Raub`, `Brandschaden`). Seit PR #177 holt ein Einbruch beim Geldleiher Geld aus der Kasse – `Unterwelt.ErwarteterGewinn` und `Diebeswesen.ErwarteterEinbruch` rechnen es schon mit.

## Entscheidungen

### Fehler und Kleinkram (PR #219)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gaunerei je Nacht | Hinterzimmer-Tafel und KI rechnen mit allen Versuchen der Nacht (`gauner.nachtVersuche` zu je `versuchFaktor` der Chance): `Unterwelt.ChanceMindestensErfolge(chance, k)` (Binomial), `ErwartetJeNacht(chance, ertrag(k))` = Σ Chance auf mindestens k Erfolge × Ertrag des k-ten. Diebstahl und eigener Einbruch: der k-te Griff holt die übrige Ware (`Warenfolge`) und den Rest der Kasse (`Bankwesen.Kassenbeute(b, k)`); Einbruch ins Wohnhaus: jeder Erfolg wieder die Beute aus der übrigen Truhe; Brand am Wohnhaus (`ErwarteterBrandschaden`) und Sabotage höchstens einmal. Mit vier Versuchen zu je ⅓ sind es 4/3 so viele Erfolge wie früher angenommen – die KI wählt deshalb **mehr** Einbrüche ins Wohnhaus, nicht weniger (Teil 1, 6 Startwerte: 1.639 → 1.850 Aufträge). Tests `KiGaunereiNachtTests`. | 27.10.2026 |
| Sabotage einmal je Nacht | Mit der richtigen Erwartung lohnte die Sabotage: Jeder Erfolg brachte wieder 6 Stunden Stillstand, vier in einer Nacht legten eine Werkstatt zwei Arbeitsjahre still (Sabotage-Aufträge 30 → 235, Mittel der Berufe −8 %, Geldleiher −27 %). Wie der Brand am Wohnhaus wirkt eine Sabotage jetzt höchstens einmal je Nacht (Kern-Regel in `Unterwelt.Nacht`; dem Teamleiter als Designfrage gemeldet, Umsetzung siehe `kern.md`). | 27.10.2026 |
| Karren | Die KI kaufte Karren nach Stillstand, der von Anschlägen kam, und verkaufte sie nach einem Jahr ohne Fahrt wieder (Gießereien mit drei Handkarren und vier Fahrten im Jahr). Behoben im Kern (Reparatur zählt nicht als Stillstand, siehe `kern.md`); dazu kaufen Wache und Geldleihe nie Karren (`KarrenNachkaufen` prüft `BrauchtKarren`). Tests `KiKarrenStillstandTests`. | 27.10.2026 |
| Bürger-KI | Ratsherren gehen mit dem Geschlecht aus den Daten durch die Stadt (`Ratsherr.Geschlecht`, Datenversion 58), nicht mehr nach der Vornamenliste. | 27.10.2026 |

### Personal und Talentbaum (PR #224)

| Thema | Entscheidung | Datum |
|---|---|---|
| Einstellen | Die KI besetzt freie Plätze nur mit Kräften, die der Betrieb einstellen darf (`Personalwesen.EinstellenVerboten` null): Anfänger der Laufbahn oder Ork/Leibeigener nach Statut, die ertragreichste wie bisher (`Kalkulation.Jahresertrag`). Die Lohnschätzung für Käufe (`HoechsterLohn`) und `Kalkulation.ErwarteterJahresgewinn` rechnen nur mit Kräften, die zur Art passen (`Wirtschaftsdaten.KraftPasst`) – auf Dauer also mit dem Gesellen bzw. Altknecht, nicht mit dem Vorarbeiter. | 28.10.2026 |
| Befördern | Einmal am Tag in der Politikstunde nach der Meisterprüfung (`KiRivale.Personal.cs`, `Befoerdern`), vor Rat und Hinterzimmer, denn ein Brief kostet einen Aktionspunkt. Befördert wird jeder, der darf (`BefoerdernVerboten` null), die Erfahrensten zuerst, wenn sich der Brief in höchstens `KiProfil.MaxAmortisationJahre` Jahren bezahlt macht: Mehrertrag = `Jahresertrag(nächste Stufe) − Jahresertrag(jetzt)`, zur dritten Stufe plus der Erwartungswert der besten von drei gezogenen Eigenschaften (`Personalwesen.ErwarteterVorarbeiterGewinn`, über alle Kombinationen – die KI kennt die Ziehung vorher nicht). Danach bleibt die halbe Reserve (Geselle) bzw. die ganze (Vorarbeiter); nicht in der Not. | 28.10.2026 |
| Eigenschaft | Gleich nach der Beförderung die Eigenschaft mit dem höchsten Mehrgewinn im Jahr (`Personalwesen.MehrgewinnJeJahr` = `Werkstattwesen.MehrgewinnDurch`, dieselbe Rechnung wie für Einrichtung); bei Gleichstand (Eigenschaften ohne Gulden-Wirkung: Lager, Razzia, Spuren …) die erste der Wahl, also die aus der Gruppe Ware. | 28.10.2026 |
| Einrichtung | Die neuen Stücke ab Stufe 3 kauft die KI mit der bisherigen Regel (höchstens 4 Jahre bis bezahlt, `ErwarteterMehrgewinnJeJahr`): Werkstatt-Stück nach der kürzeren Zeit der Stufe-3-Waren, Ladentheke nach dem Erlös der Jahresmenge × 1,25 %, Lehrlingsstube nach dem früheren Gesellen – je Anfänger (Jahresertrag Geselle − Lehrling) × (1 − 1 ÷ 1,5). Dazu zum Jahresende höchstens ein Stück fürs Wohnhaus (`WohnhausEinrichtungKaufen`) nach `Wohnungswesen.MehrgewinnJeJahr` mit derselben Frist und Reserve: Vorratskammer nach Haushalt und Unterhalt, Riegel und Hund nur nach einem Einbruch im Vorjahr. | 28.10.2026 |
| Tests | `KiPersonalTests`: befördert erst mit genug Erfahrung, Vorarbeiter höchstens einer mit gewählter Eigenschaft, ohne Geld über der halben Reserve kein Brief. `KiTests`: die Grube wird nach dem Statutwechsel mit Knechten besetzt, die Reserve trägt einen Lehrlingslohn. | 28.10.2026 |

### KI nach Charakterzügen (PR #227)

| Thema | Entscheidung | Datum |
|---|---|---|
| Grundsatz | Der Charakter des Hauses (vorsichtig, ausgewogen, wagemutig im `KiProfil`) bleibt; die Züge des **Oberhaupts** verschieben ihn (`Familienbuch.KiNeigung` → `ZugNeigung`, `KiRivale.Zuege.cs`). Stirbt das Oberhaupt, handelt das Haus nach den Zügen des Erben. Faktoren werden multipliziert, Zuschläge addiert; Werte je Zug unter `charakterzuege.zuege.<Zug>.ki`. Ohne Abschnitt neutral. | 28.10.2026 |
| Gierig / Großzügig | Zins der Geldleihe +4 / −3 Prozentpunkte (mindestens 4 %), Budget für Geschenke an den Rat × 0,7 / × 1,3; Großzügig holt in jeder Schänke einen Barden (mit voller Reserve). „Höhere Preise“ gibt es bei der KI nur beim Zins: Verkauf und Zutaten macht der Verwalter (PR #86). | 28.10.2026 |
| Fleißig / Träge | Wechselschwelle × 0,8 / × 1,25 (stellt schneller bzw. seltener auf ein besseres Produkt um); die Arbeitsleistung kommt über Handwerk ±1. Erst gebaut: Fleißig übt, sobald die Aktionspunkte reichen, Träge übt nie – das machte den Schmied in der Messung reicher, ohne dass es der Zug allein erklärt; jetzt nur noch als Schalter `ki.ueben` (+1/−1) in den Daten, ungenutzt. | 28.10.2026 |
| Fromm / Zynisch | Ablass schon, wenn danach ein Viertel statt der Hälfte der Reserve bleibt / erst mit voller Reserve; Fromm tritt nie zum fremden Stadtglauben über (zahlt lieber den Glaubenszins). Pilger wie Fromm beim Ablass. | 28.10.2026 |
| Mutig / Feige | Raubfurcht für die Eskorte (Karren, Schiffe) × 0,85 / × 1,5; Feige: Käufe, Ausbau und Beförderung müssen sich ein Jahr früher bezahlt machen (`Amortisationsjahre`, mindestens 1). Mutig wartete erst auch ein Jahr länger – das trieb Häuser in die Not (Karren in Not, siehe Messung) und ist gestrichen. | 28.10.2026 |
| Ehrlich / Verschlagen | Bezahlte Gaunerstücke im Jahr −1 / +1 (`Gaunerbudget`) – aber nur bei einem Haus, das seinem Charakter nach Gaunerstücke nutzt: Das vorsichtige Haus heuert auch mit verschlagenem Oberhaupt nie eine Bande an, und Ehrlich senkt nie unter eins (wirkt also beim wagemutigen Haus, 2 → 1). Dazu beschenkt Ehrlich vor dem Gerichtstag keine Richter (`unbestechlich`). Erst ließ Ehrlich das ausgewogene Haus ganz ohne Gaunerstücke – die Diebstähle der KI fielen um ein Viertel und alle Handwerke wurden reicher. Entscheidung des Teamleiters (Prüfung #227). | 28.10.2026 |
| Geduldig / Jähzornig | Klage in Beweisen (`klageBeweise` +1 / −1): ohne Zug ab den Beweisen für einen sicheren Schuldspruch (heute 3 × 10 gegen 25), Geduldig erst mit einem Beweis mehr (4), Jähzornig schon mit einem weniger (2), nie unter der Schuldschwelle (15 → 2 Beweise); dann meldet die KI „genug für eine Anklage“ statt „sicherer Schuldspruch“. Ein Faktor auf die Beweiskraft (erst × 1,15 / × 0,8) wirkte bei Zehnerschritten gar nicht bzw. nur zufällig. Wechselschwelle × 1,3 / × 0,7. Das zuerst gebaute „ein Verlustjahr mehr vor dem Verkauf“ ist gestrichen (Not, siehe Messung). | 28.10.2026 |
| Gesellig / Scheu | Geschenke × 1,2 / × 0,8; Barde immer / nie. | 28.10.2026 |
| Bescheiden / Hochmütig | Keine Wohnhaus-Einrichtung / in der Brautschau zählt je Standesstufe der Braut +25 %. Die zuerst gebaute längere Frist für Wohnhaus-Einrichtung und +50 % je Stufe sind gestrichen (Not, siehe Messung). | 28.10.2026 |
| Messung | `messen bericht 20 30` auf main `b59a372` + `d5985f5` gegen main ohne Züge: KI ÷ gleiche Strategie (Median) 1,08 → 1,01, „Schwer“ 1,73 → 1,79; Diebstahl der KI 0,85 → 0,80 je Jahr, Einbruch 0,60 → 0,69; Karren in Not 7 → 7 (1.320 Partien; vor dem Nachsteuern 12). Die Not trafen vor allem Häuser mit mutigem (weniger Eskorte, längere Amortisation), hochmütigem (teure Brautschau) und geduldigem Oberhaupt (Verlustbetriebe länger). Einzelheiten in WIRTSCHAFTSMODELL, „Charakterzüge (PR #227)“. | 28.10.2026 |
| Richter | Die KI rechnet Geschenke mit der Bestechlichkeit des Ratsherrn (`Stadtrat.GunstJeGulden`) – Wahl, Statut, Gericht. | 28.10.2026 |

## Messungen

Neue Messungen der Spiel-KI stehen hier (mit main-Commit), nicht mehr in `docs/WIRTSCHAFTSMODELL.md`. Ältere: dort bis §42 (Taschendiebe, PR #69).

### KI-Häuser im Rat (PR #73)

Gemessen auf main `89a6c68` + PR #73, `messen 30 20`, Spieler Mensch (passiv). Vermögen Ø am Ende, in Klammern „am Ende Bürgermeister“:

| KI-Charakter | Eisenbart | Eichwald | Kieselbach |
|---|---|---|---|
| vorsichtig, main | 156.684 | 141.408 (10/20) | 175.356 (7/20) |
| vorsichtig, jetzt | 156.684 | 141.408 (10/20) | 175.356 (7/20) |
| ausgewogen, main | 170.235 | 101.958 (4/20) | 152.002 (9/20) |
| ausgewogen, jetzt | 170.263 | 102.775 (4/20) | 151.742 (9/20) |
| wagemutig, main | 93.172 | 60.590 (3/20) | 77.700 (3/20) |
| wagemutig, jetzt | 91.843 | 60.133 (2/20) | 75.793 (3/20) |

Amtswechsel im Rat (20 Startwerte × 30 Jahre): vorsichtig 392 → 392, ausgewogen 457 → 465, wagemutig 430 → 444; Bürgermeisterwechsel gleich (25 / 34 / 40). Die Wirkung ist klein, weil im Mittel nur 1,6–2 der 7 Sitze bei Häusern liegen und die Ratsherren die Wahl entscheiden – und weil der Spieler in der Messung sich nie bewirbt. Spürbar wird es, wenn der Spieler gegen einen KI-Amtsinhaber antritt: Ausgewogene und wagemutige KI-Häuser stimmen dann für ihn, solange er ärmer ist als der Amtsinhaber bzw. nicht führt. Rechenzeit unverändert (19–20 s).

### Wohnhaus und Sparen (PR #82)

Gemessen auf main `fa43221` + PR #82, je Charakter 20 Partien × 30 Jahre, alle drei KI-Häuser (60 Häuser je Zeile), Stand am Ende:

| KI-Charakter | main | mit PR #82 |
|---|---|---|
| Vorsichtig | Edler 11, Freiherr 49 – Rein Ø 115.378 | Edler 11, Freiherr 49 – Rein Ø 119.017 |
| Ausgewogen | Edler 24, Freiherr 36 – Rein Ø 100.814 | Edler 20, Freiherr 40 – Rein Ø 111.218 |
| Wagemutig | Edler 56, Freiherr 4 – Rein Ø 58.282 | Edler 57, Freiherr 3 – Rein Ø 63.944 |

`messen 30 20` (ausgewogen): Eichwald Ø 77.487 → 98.916, Kieselbach Ø 101.048 → 107.109, Spieler Ø 123.907 → 127.631; Geschenke an Ratsherren Ø 1.025 → 847. Wagemutige Häuser bleiben meist Edler, weil sie zu wenig verdienen (Rein Ø 64.000, Freiherr braucht 100.000 + Hof 25.000) – keine Falle mehr, sondern Charakter: Gaunerei und Kerker kosten.
### Wegzeiten der Einwohner (PR #80)

Gemessen auf main `00c0cd3` + PR #80: `messen 30 20` (ausgewogen, wagemutig) **zeichengleich** mit main – Wegzeiten wirken nur über die Passanten auf die Taschendiebe. Spieler als Dieb (20 × 30 Jahre): Beute Ø 713 statt 716 Gulden im Jahr – das Übliche je Uhrzeit passt sich an, mehr Passanten unterwegs heißt nicht mehr Beute.

### KI-Häuser handeln nicht von Hand (PR #86)

Probeweise gebaut (nicht übernommen): Eigene Ware zurückhalten unter 70/80/90 % des Grundpreises (vorsichtig/ausgewogen/wagemutig), freigeben 10 Punkte darüber oder ab 75/90 % Lagerfüllung. Gemessen auf main `2cd3222`, je Charakter 20 Partien × 30 Jahre, 60 KI-Häuser, Reinvermögen Ø am Ende:

| KI-Charakter | ohne Zurückhalten | mit Zurückhalten |
|---|---|---|
| Vorsichtig | 119.017 | 119.014 |
| Ausgewogen | 111.218 | 108.483 (−2,5 %) |
| Wagemutig | 63.944 | 61.444 (−4 %) |

Schwellen-Versuch (ausgewogen): unter 50–70 % wird nie zurückgehalten (Preis fällt nicht so tief), unter 80 % −0,7 bis −1,5 %. Preissturz durch den Spieler (6 Stunden lang je 100 Stück jeder KI-Ware, Jahr 5–7, 20 Partien): Zuwachs der KI mit und ohne Zurückhalten unter 70 % gleich (14.031), unter 80 % weniger (13.797).

### Warenkorb je Volk und Rolle (PR #91)

Faktoren in der Standardstadt (Startwert 101, Jahr 2), je Gruppe von günstig nach teuer: Schmiedewaren 1,07 … 0,96; Holzwaren 1,03 … 0,93; Steinwaren 1,07 … 0,96; Arzneien 1,02 … 0,92 (Kräuterschnaps 1,02); Speis und Trank 1,06 … 0,96; Kirchenbedarf 1,10 … 1,03; Duftwaren 1,02 … 0,92. Erste Fassung (Preisklassen ×1,5/×0,5) drückte alles an die Grenzen ±10 % – das Volk war nicht mehr zu sehen.

`messen 30 20`, gemessen auf main `4f8cf6d` + PR #91, Vermögen Ø am Ende:

| Haus | main | mit Warenkorb |
|---|---|---|
| Spieler Eisenbart (Schmied, passiv) | 174.720 | 170.059 (−3 %) |
| Eichwald (Tischler, KI) | 106.067 | 96.175 (−9 %) |
| Kieselbach (Steinmetz, KI) | 144.125 | 138.788 (−4 %) |

Alle verlieren etwas, weil späte Werkstätten vor allem teure Waren herstellen und die Einkaufenden meist arm sind. Kern-Test „Zwei Gießereien teilen sich den Silberring-Markt“: 611 → 649 (+6 %, Silberring ist günstige Schmiedeware).

### KI kauft Leerstand, baut neu und verkauft Betriebe (PR #108)

Gemessen auf main `5e62a04` + PR #108, `messen 30 20` (Spieler Mensch, passiv), 20 Partien × 30 Jahre, je drei KI-Häuser (60 Häuser). Neu: `messen … greybox` spielt mit der Greybox-Karte und zählt, was die KI-Häuser bauen und verkaufen, dazu ihren Stand am Ende. Vorher = Funktions-Branch mit der Kern-Arbeit, ohne die KI-Änderungen (Köln dort zeichengleich mit main).

| Messung | Eisenbart Ø | Eichwald Ø | Kieselbach Ø | Stand am Ende (60 Häuser) | gebaut, gekauft, verkauft (Summe) |
|---|---|---|---|---|---|
| Köln, ausgewogen, vorher | 170.059 | 96.175 | 138.788 | Edler 3, Freiherr 56, Reichsgraf 1 | – |
| Köln, ausgewogen, jetzt | 170.396 | 97.581 | 138.144 | Edler 3, Freiherr 56, Reichsgraf 1 | 3 Betriebe nach Verlustjahren verkauft |
| Greybox, ausgewogen, vorher | 194.271 | 115.357 | 161.676 | **Patrizier 60** (hängen am Steinhaus) | – |
| Greybox, ausgewogen, jetzt | 169.601 | 98.870 | 139.748 | Edler 4, Freiherr 55, Reichsgraf 1 | 60 Stadtpalais, 59 Stadthöfe neu gebaut; 3 Verlustverkäufe |
| Greybox, vorsichtig, vorher | 172.903 | 153.120 | 189.260 | Patrizier 60 | – |
| Greybox, vorsichtig, jetzt | 149.077 | 127.752 | 168.396 | Freiherr 57, Reichsgraf 3 | 60 Stadtpalais, 60 Stadthöfe |
| Greybox, wagemutig, vorher | 111.611 | 70.712 | 78.944 | Patrizier 60 | – |
| Greybox, wagemutig, jetzt | 93.400 | 64.666 | 76.592 | Edler 39, Freiherr 21 | 60 Stadtpalais, 42 Stadthöfe; 2 Verlustverkäufe |

- **Greybox:** Vorher blieben alle KI-Häuser Patrizier, weil der Ausbau zum Stadtpalais abgelehnt wird; ihr Vermögen war höher, weil sie keine Standesgebühren und keinen größeren Unterhalt zahlten. Jetzt kommen sie so weit wie in Köln (ausgewogen: Edler 4 / Freiherr 55 / Reichsgraf 1 gegen 3 / 56 / 1).
- **Leerstand** kaufte in den Messungen kein KI-Haus: Kein KI-Haus geht bankrott, und der bankrotte passive Spieler hinterlässt nur ein Fachwerkhaus. **Werkstätten** baute die KI nie neu – es gab immer freie, und bei gleichem Preis gewinnt der Kauf (Baujahr). Beides prüfen die Tests (`KiBauenTests`).
- **Verkäufe** sind selten (2–3 in 600 Hausjahren), **Notverkäufe** gab es keine (kein KI-Haus kam in Not; Test `KiVerkaufTests`). Die erste Fassung ohne Lagerwert verkaufte 19 Werkstätten, meist die ausgebauten Hauptwerkstätten, und kostete Haus Eisenbart Ø 11.000 Gulden.
- Rechenzeit der Messung: Köln 38 s, Greybox 43 s – die Bauortsuche fällt nicht ins Gewicht.

### Gaunerstücke gegen Wohnhäuser (PR #119)

Gemessen auf main `5db3ab6` + PR #119 (bis main `cd00f6d` kamen nur Modelle dazu), `messen 30 20` (Spieler Mensch, passiv), 20 Partien × 30 Jahre. Neu in der Messung: die Zeile „Banden der KI je Art und Ziel“ (Summe über alle Partien).

| Messung | Eisenbart Ø | Eichwald Ø | Kieselbach Ø | Banden der KI (Summe) |
|---|---|---|---|---|
| Köln, ausgewogen, vorher | 170.396 | 97.581 | 138.144 | Diebstahl 420 |
| Köln, ausgewogen, jetzt | 158.436 | 97.265 | 137.862 | Diebstahl 342, Einbruch 137 |
| Köln, wagemutig, vorher | 101.867 | 67.902 | 74.040 | Diebstahl 869, Sabotage 1 |
| Köln, wagemutig, jetzt | 89.015 | 57.458 | 77.743 | Diebstahl 636, Einbruch 280, Brand 52 |
| Greybox, ausgewogen, vorher | 169.601 | 98.870 | 139.748 | – |
| Greybox, ausgewogen, jetzt | 157.689 | 96.805 | 138.480 | – |

- **Ausgewogen:** Eichwald und Kieselbach brechen bei Eisenbart ein (72-mal in 20 Partien), Eichwald auch bei Kieselbach (64-mal), meist statt eines Diebstahls. Eisenbart, das führende Haus und damit das Ziel, verliert im Schnitt **7 %**; der Einbruch holt bei Stadthof und Palais bis 1.500 bzw. 1.200 Gulden, mehr als ein Diebstahl. Die anderen beiden bleiben gleich (was sie erbeuten, verlieren sie an den Einbrüchen gegeneinander). Ein Startwert (1818) fällt aus der Reihe: 160.646 → 94.416; ohne ihn sind es −5 %.
- **Wagemutig:** 52 Brände, 39 davon am Stadthof von Eisenbart (Reparatur 5.000, gelingt halb so oft). Eisenbart verliert 13 %, Eichwald 15 %, Kieselbach gewinnt 5 %.
- **Spieler:** Der passive Spieler bleibt im Fachwerkhaus, wo sich kein Einbruch lohnt – Raub und Schäden bei ihm unverändert (Ø 369). Bürgermeister wird Eisenbart etwas später (Ø Jahr 14,7 statt 13,6).
- Kerkerjahre der KI weiter 0, kein KI-Haus bankrott.

### Verlustjahre, Notverkauf und Sparziel (PR #123)

Gemessen auf main `cd6a466` + PR #123, `messen 30 20` (Spieler Mensch, passiv), 20 Partien × 30 Jahre. Neu in der Messung: die Zeile „Sparjahre der KI“ (Jahre, in denen ein KI-Haus zum Jahresende spart, Ø je Partie, und die längste Folge in einer Partie).

| Messung | Ergebnis main = PR | Sparjahre Ø (längste Folge) |
|---|---|---|
| Köln, ausgewogen | Eisenbart 158.436, Eichwald 97.265, Kieselbach 137.862; Edler 5, Freiherr 55 | Eisenbart 6,0 (3), Eichwald 5,0 (4), Kieselbach 5,0 (3) |
| Köln, wagemutig | Eisenbart 89.015, Eichwald 57.458, Kieselbach 77.743; Edler 34, Freiherr 26; 2 Verkäufe nach Verlustjahren | Eisenbart 4,5 (5), Eichwald 2,2 (3), Kieselbach 3,0 (3) |
| Greybox, ausgewogen | Eisenbart 157.689, Eichwald 96.805, Kieselbach 138.480; Edler 6, Freiherr 54 | Eisenbart 8,2 (3), Eichwald 6,4 (4), Kieselbach 6,2 (3) |

- **Gleich auf die Zahl:** In den Messungen steht kein KI-Betrieb ein Viertel des Jahres ohne Personal, und kein Haus kommt in Not. Die neuen Regeln greifen nur im Ausnahmefall (die Tests in `KiVerkaufTests` zeigen ihn).
- **Sparziel:** Kein Haus spart länger als 5 Jahre in Folge; danach steigt es auf oder baut. Die längste Folge hat die wagemutige Eisenbart-KI vor dem Freiherrn (100.000 und ein Stadthof). Die Sparlogik passt also zur Schwelle als Ziel.

### Warenkorb ohne Abneigung (PR #127)

Gemessen auf main `1209ff2` + PR #127, `messen 30 20` (Spieler Mensch, passiv), 20 Partien × 30 Jahre. „Ohne Warenkorb“ = `buerger.warenkorb.spanne` 0 zum Vergleich. Vermögen Ø am Ende, in Klammern gegen „ohne Warenkorb“:

| Messung | Haus | ohne Warenkorb | bisher | ohne Abneigung |
|---|---|---|---|---|
| Köln, ausgewogen | Eisenbart (Spieler) | 167.673 | 158.436 (−5,5 %) | 163.070 (−2,7 %) |
| | Eichwald | 103.416 | 97.265 (−5,9 %) | 97.608 (−5,6 %) |
| | Kieselbach | 142.722 | 137.862 (−3,4 %) | 141.735 (−0,7 %) |
| Köln, wagemutig | Eisenbart (Spieler) | 91.270 | 89.015 (−2,5 %) | 90.307 (−1,1 %) |
| | Eichwald | 67.801 | 57.458 (−15,3 %) | 59.696 (−12,0 %) |
| | Kieselbach | 67.581 | 77.743 (+15,0 %) | 67.650 (+0,1 %) |
| Greybox, ausgewogen | Eisenbart (Spieler) | 168.691 | 157.689 (−6,5 %) | 162.635 (−3,6 %) |
| | Eichwald | 104.280 | 96.805 (−7,2 %) | 97.509 (−6,5 %) |
| | Kieselbach | 143.029 | 138.480 (−3,2 %) | 142.891 (−0,1 %) |

- Eisenbart und Kieselbach verlieren nur noch 0–4 %. **Eichwald** (Tischler) bleibt bei etwa −6 %: Schrank und Prunktruhe sind teure Holzwaren, die außer Ratsherren und Bürgersöhnen niemand bevorzugt, und Holzwaren sind nur bei Halblingen beliebt. Das ist die gewollte Färbung der Standardstadt.
- Die wagemutigen Zeilen schwanken stark (Kieselbach +15 % bisher ist Rauschen der Gaunerstücke, nicht der Warenkorb).
- Stand der KI-Häuser am Ende (Köln, ausgewogen): Edler 3, Freiherr 57 (bisher Edler 5, Freiherr 55); kein Haus bankrott. Alle Tests unverändert grün, auch „Zwei Gießereien teilen sich den Silberring-Markt“.

### KI kauft Einrichtung (PR #177)

Gemessen auf main `821bc97` + Funktions-Branch `phase5/balancing-1` mit Kern Schritt 0 (`4f0d587`: gleicher Start, Abfrage Mehrgewinn) + diese Änderung – **noch vor** den neuen Preisen des Kerns (Datenversion 48, billigere Eisentür, Hauptbücher, Pfandkammer). `messen 30 20 Mensch x <Charakter>`, 20 Partien × 30 Jahre, drei KI-Häuser je Partie (60 Häuser). „Ohne“ = dieselbe Fassung ohne Abschnitt `ki` in `wirtschaft.json`. Vermögen Ø am Ende:

| Charakter | Haus | ohne | mit Einrichtung |
|---|---|---|---|
| ausgewogen | Eisenbart | 78.147 | 92.552 (+18 %) |
| | Eichwald | 65.606 | 88.841 (+35 %) |
| | Kieselbach | 74.232 | 99.685 (+34 %) |
| vorsichtig | Eisenbart | 77.768 | 87.440 (+12 %) |
| | Eichwald | 72.336 | 90.726 (+25 %) |
| | Kieselbach | 108.087 | 126.220 (+17 %) |
| wagemutig | Eisenbart | 56.466 | 73.726 (+31 %) |
| | Eichwald | 34.810 | 59.147 (+70 %) |
| | Kieselbach | 37.617 | 53.067 (+41 %) |

- **Gekaufte Stücke** je KI-Haus in 30 Jahren: ausgewogen Ø 4,9, vorsichtig Ø 3,8, wagemutig Ø 5,0. Jedes Haus kauft Gutes Werkzeug, Meisterwerkzeug (ab Stufe 2), Lederschürzen und das Sonderstück seines Handwerks (Blasebalg, Leimofen, Flaschenzug); die Eisentür 52-mal (ausgewogen) bzw. 60-mal (wagemutig), bei den Vorsichtigen nie – dort stiehlt kein KI-Haus. Regale, Löschfass und die Stücke der Umlandbetriebe nie (keine Gulden-Wirkung, kein Brand bzw. keine Einrichtung).
- **Stand am Ende:** ausgewogen Edler 58, Patrizier 2 → Edler 29, Freiherr 31; vorsichtig Edler 34, Freiherr 23, Gemeiner 3 → Edler 16, Freiherr 41, Gemeiner 3; wagemutig Edler 28, Patrizier 32 → Edler 55, Freiherr 4, Patrizier 1. Kein Haus bankrott.
- Die Werkzeuge (10 % schneller je Stück, für alle Waren) machen sich deutlich unter 4 Jahren bezahlt; der große Zuwachs zeigt, dass die Einrichtung für jeden lohnt, der sie kauft – auch für den Spieler. Die gemeinsame Messung mit den neuen Kern-Werten macht der Teamleiter.
