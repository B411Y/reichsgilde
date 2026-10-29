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
