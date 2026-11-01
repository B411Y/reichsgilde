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
- (von Kern) Teure Ware im Lager schützen (PR #232): Seit der Giftdolch einen Dolch des Schmieds braucht (≈ 170 Gulden je Stück), räumen die Diebe der KI-Häuser das Lager einer Siederei nachts aus (Startwert 202: fünf Einbrüche in zwei Nächten, 42 Dolche ≈ 8.800 Gulden, danach Bankrott). Die KI sollte Eisentür und Wächter nach dem Lagerwert kaufen bzw. einstellen, nicht erst nach einem Angriff (gilt auch für Gold, Goldketten, Prunkwaren).
- Mittagsandrang in der Schänke (PR #274): Um 12 Uhr, der ersten Stunde mit Einkehren, gehen fast alle Einwohner zugleich hin (bis 107 von 123 in der einen Schänke), abends um 21:45 bis 84. Eine Kapazität für Einwohner gibt es nicht. Designfrage beim Teamleiter, Empfehlung: Sitzplätze je Schänke nach Stufe im Abschnitt `buerger` (im Verhältnis von `anziehungJeStufe`, etwa 20/30/40); wer keinen Platz bekommt, nimmt die nächstbeste Handlung (mittags Arbeit, abends daheim); ohne Eintrag unbegrenzt wie heute. Vorher messen, wie sich die Gäste auf mehrere Schänken verteilen. Nur den Mittag zu dämpfen, verschiebt den Andrang auf 18 Uhr.
- Einwarige Umlandbetriebe (Holzschlag, Lager 40 Holz): Auch nach phase5/deutz-ki (Vorarbeiter mit Lager) stehen die Kölner Holzschläge mit drei Karren 21–29 % still (Hardevust 28,8 %, Kleingedank 23,3 %, Teil 5 Normal; „Schwer“ 25,8 / 21,5 %). Empfehlung: ein vierter Karren nur für einwarige Umlandbetriebe ab 20 % Stillstand an der Grenze von drei (Regel der KI, kein Wert in `wirtschaft.json`): nach dem Karrenkauf-Modell 28 % → etwa 17 %, ≈ 290 Gulden im Jahr für 900 (gut 3 Jahre). Die Nachtfahrt brächte weniger (Fahrfenster 20–23 Uhr, etwa +20 % Fahrzeit ≈ 200 Gulden), kostet aber Torgeld und das doppelte Überfallrisiko je Nachtfahrt (Holz ist billig, die Bande nimmt trotzdem die Ladung) – netto wenig. Designfrage beim Teamleiter; erst bauen, dann messen.
- Die Schätzung „Stillstand vorher ^ (Durchfluss nachher ÷ vorher)“ ist im ersten Jahr zu optimistisch: Die Prüfung sah in Köln nach dem ersten Kauf 7–12 % geschätzt und 14–41 % im Folgejahr gemessen (Grube 18 %, Bauernhof 41 %) – das erste Jahr hat Anlaufverluste (leere Karren, Lager füllt sich). Die KI kauft dann im Jahr darauf nach; schlimmstenfalls einen Karren zu spät. Bei Gelegenheit das erste Jahr eines Betriebs nicht als Eichung nehmen.

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

### KI nach Meinungen (PR #231)

| Thema | Entscheidung | Datum |
|---|---|---|
| Rat: Aktivitäten vor Geschenken | Für jeden Ratsherrn, dessen Stimme fehlt (Wahl, eigener Statut-Antrag, Richter vor dem Gerichtstag), plant die KI zuerst eine Aktivität: eine, die ihr Oberhaupt selbst mag (Gesellig zecht, Fromm geht zur Messe, Klug spielt Brettspiel, Mutig jagt, Hochmütig lädt zum Gastmahl), sonst die mit der besten erwarteten Wirkung beim Ratsherrn – nie eine, die ihr Oberhaupt nicht mag, nie mit erwartetem Verlust, nur mit voller Reserve danach. Den Rest schließt ein Geschenk, nach der wirklichen Wirkung gerechnet (nie mehr als ein Geschenk allein gekostet hätte). Reichen die Aktionspunkte für Aktivitäten und Geschenke nicht, plant sie wie früher nur mit Geschenken („halbe Bestechung“ bleibt verboten). Das Bestechungsrisiko zählt nur auf die Geschenke (`KiRivale.Meinungen.cs`). | 28.10.2026 |
| Häuser untereinander | Meinung des eigenen Oberhaupts über das andere (`MeinungUeber`): ab +25 (`meinungen.wirkung.kiVerschontAb`) verschont – keine Gaunerstücke, keine eigenen Diebe, kein Spitzel, Klage erst mit einem Beweis mehr; bis −25 (`kiVerhasstAb`) verhasst – erstes Ziel der Gaunerstücke (der Verhassteste zuerst, auch wenn er hinten liegt), Spitzel auch auf ein Haus hinter ihr, Klage schon mit einem Beweis weniger (nie unter der Schuldschwelle). Bei Wahlen: Gefahr = Reinvermögen × (1 − Meinung × 1 %), mindestens ein Zehntel – ein geliebtes Haus wirkt weniger gefährlich; ein verschuldetes Haus bleibt nach seinem Reinvermögen geordnet (sonst kehrte sich die Rechnung um, Befund der Prüfung). „Ertappt“ erkennt das Opfer am ganzen Namen seines Betriebs bzw. Wohnhauses (erst über Teilwörter). | 28.10.2026 |
| Woher die Meinung kommt | Feste Gründe (Züge, Volk, Glaube, Stand) und Taten: Klage −15, ertappter Diebstahl/Sabotage/Überfall −20, Stimme im Rat ±5, Fest +5. Ein Haus, das die KI bestiehlt und dabei erkannt wird, macht sie sich so zum Feind – vorher galt nur „hat geschadet“ (Beweise). | 28.10.2026 |
| Brautschau | Die KI rechnet mit `GuldenJeZuneigung(konto, kandidat)` – ein Kandidat, der ihr Oberhaupt mag, ist billiger. | 28.10.2026 |
| Messung | `messen bericht 20 30` auf main `391874f5` + `13531d4d`: Geschenke der KI je Jahr (Messspieler, ein Betrieb) −25 … −92 → −14 … −55, KI ÷ Bezug (Median) 1,01 → 1,11, „Schwer“ 1,79 → 1,78, Diebstahl 0,80 → 0,81, Einbruch 0,69 → 0,66, Sabotage 0,01 → 0,02 je Jahr, Karren in Not 7 → 8; verheiratet nach 3 Jahren 88/90 wie main. In 3 Startwerten × 12 Jahren hinterlassen KI-Häuser 88 Gründe aus Aktivitäten bei Ratsherren (Test `KiMeinungTests`). Einzelheiten in WIRTSCHAFTSMODELL, „Meinungen und Aktivitäten (PR #231)“. | 28.10.2026 |

### Schankschluss (PR #274)

| Thema | Entscheidung | Datum |
|---|---|---|
| Befund | „114–116 von 123 Einwohnern 22–23 Uhr in der Schänke“ (Ton-Messung, Wirt, Startwerte 1400 und 99) ist ein Zählfehler der Sonde, kein Fehler des Kerns: Sie rief `Einwohner.Wo(uhr)` mit `UhrzeitInMinuten` statt `VergangeneMinuten` auf. Ab dem zweiten Tag liegt die Uhrzeit vor jedem Abmarsch, also kam der Startort des letzten Weges heraus – die Schänke, aus der die Gäste um 22 Uhr heimgingen (nachgerechnet: 116,0 bzw. 113,7). Richtig gezählt ist die Schänke ab 22:10 leer und um 22:45 sind 122 von 123 daheim. Die Darstellung rechnet mit `VergangeneMinuten` und war nie betroffen. | 31.10.2026 |
| Absicherung | Datenprüfung: `buerger.handlungen.Einkehren.bis` ≤ `Gastwirtschaft.SchliesstUm` (22), sonst säßen Einwohner in der geschlossenen Schänke; ebenso `Arbeiten.bis`, sobald eine Rolle `arbeitsort` Schaenke hat (heute keine). Ein zu früher Beginn (`Einkehren.von` vor `gastwirtschaft.oeffnetUm`) wird wie bisher nur mit Kopplung geprüft (`Wirtschaftsdaten`, Kern; heute beide 12 Uhr). Der Parameter von `Wo`, `IstUnterwegs` und `IstAngekommen` heißt jetzt `vergangeneMinuten`, der Kommentar warnt vor der Uhrzeit. Test `BuergerSchankschlussTests` (Wirt, Startwerte 1400 und 99, drei Tage): ab 22 Uhr wählen alle Ruhen daheim, wer geht, geht aus der Schänke ins eigene Haus, ab 22:10 ist jede Schänke leer, um 22:50 ist niemand mehr unterwegs, bis 23 Uhr bleibt sie leer; dazu die Datenprüfung mit `bis` = 23 (Einkehren, Arbeiten mit Schankrolle). Verhalten unverändert. | 31.10.2026 |
| Mittagsandrang | Echt, aber keine Regel verletzt: Um 12 Uhr wählen 92–107 von 123 Einwohnern Einkehren, kurz vor 13 Uhr sitzen bis zu 107 (Startwert 1400) bzw. 103 (99) in der einen Schänke, abends um 21:45 bis 84 bzw. 78. Ursache: Vergnügen wächst ab 4 Uhr um 5 je Stunde (mittags etwa 40), Einkehren stillt 40 + 5 Ruhe, Arbeiten 25 (+ 5 Beharrung). Eine Kapazität für Einwohner gibt es nicht (`Gastwirtschaft.SitzplaetzeImRaum` = 12 begrenzt nur die Gäste, die der Innenraum zeigt); Gäste und Umsatz hängen nur am Verhältnis zum Üblichen je Uhrzeit (Faktor 1 ± 0,5), der Andrang kostet oder bringt also nichts. Nicht erfunden, als Designfrage gemeldet (siehe „Offen“). | 31.10.2026 |

### Karren und Bauort nach dem Karrenweg (PR #282)

| Thema | Entscheidung | Datum |
|---|---|---|
| Befund | Seit die Fahrzeit nach Weglänge und Karrenart rechnet (Ochse 0,8, Hand 1,0, Pferd 1,3 m/s, Datenversion 65), war die feste Karrenwahl der KI (Werkstatt unter Stufe 2 Handkarren, sonst Ochse, höchstens drei, kein Tausch) die schwächste fürs Umland: In Köln braucht der Ochse 238–306 min je Weg, drei Ochsen schaffen einen Holzschlag nicht (27–38 % Stillstand), ein Bauernhof steht 25 % still (Befund H1 der Prüfung). Dazu baute die KI neue Werkstätten am Bauort mit der kürzesten Luftlinie zum Platz – in der Greybox ein Hof zur falschen Gasse (131 min statt 24). | 01.11.2026 |
| Durchfluss | `Durchfluss(b, art)` = Ladung ÷ (Fahrzeit hin + zurück des Kerns, also mit Steigung, Belag samt Wegebau, Furten und Mindest- und Höchstdauer). Ladung = was der Karren fasst, höchstens Lager je Ware × Zahl der Waren (Umland: Rohstoffe; Werkstatt: Waren in Arbeit oder Zutaten-Sorten, was mehr ist). Ein Holzschlag füllt den Kriegskarren (75) also nie, ein Bauernhof mit zwei Waren schon. | 01.11.2026 |
| Wirkung | `Stillstand nachher = Stillstand vorher ^ (Durchfluss nachher ÷ vorher)` (`DetMathe.Pow`). Jeder zusätzliche Durchfluss drückt den Stillstand um denselben Faktor; geeicht am Stillstand des Vorjahres, ohne Bedarf und Zeitfenster zu kennen. An den Kölner Umlandbetrieben nachgerechnet: zwei Ochsen 50 % → drei Ochsen geschätzt 35 %, gemessen 32–38 %; Ochse und zwei Pferde geschätzt 23 %, gemessen 22 %; Bauernhof Ochse und zwei Pferde geschätzt 4 %, gemessen 1–2 %. Linear geschätzt (Stillstand fällt mit dem Kehrwert) versprach ein zweiter Karren derselben Art zu viel. Mehrgewinn = (vorher − nachher) × Wert eines vollen Arbeitsjahres (`Kalkulation.Leistung` × Arbeitsminuten × `WertJeArbeitsminute`); Eskorte je Fahrt zählt nicht (zwei bis drei Fahrten im Jahr). | 01.11.2026 |
| Wahl | Wie bisher nur ab `StillstandFuerKarrenkauf` (20 %) im Vorjahr. Unter `MaxKarrenJeBetrieb` zum Jahresende je Art ein Kauf; an der Grenze je Karren ein Tausch gegen eine Art mit mehr Durchfluss (Kosten = Preis − Erlös, beim Ochsen 900 − 200). Gewählt wird der größte Wert `Mehrgewinn − Kosten ÷ Amortisationsjahre` (wie beim Kauf eines Betriebs), der sich in `Amortisationsjahre` bezahlt macht und die Reserve lässt; bei Gleichstand der billigere. **Nicht** nach Durchfluss je Gulden: Der Handkarren brächte je Gulden oft am meisten, belegte aber einen der drei Plätze – Greybox mit immer Handkarren im Umland: KI-Häuser bei „Schwer“ 99.462 statt 111.112 (8 Startwerte). | 01.11.2026 |
| Tausch | Erst gebaut zum Jahresende mit den Karren, die dann am Hof stehen – die Prüfung fand in Köln in sieben Jahren keinen Tausch: Um 23 Uhr wartet der langsamste Karren eines gebremsten Umlandbetriebs meist vor dem Tor. Jetzt plant die KI über alle Karren und tauscht in der Stundenrunde (`KarrenTauschen`), sobald der gewählte am Hof steht; bei gleich guten den, der am Hof steht. Nur wenn jeder Karren ein ganzes Vorjahr gezählt hat (`TauschBereit`, `Karren.FahrtenVorjahr` gesetzt): Dann zeigt der Stillstand des Vorjahres genau diese Karren, und nach einem Kauf oder Tausch wartet sie, bis das wieder gilt (ein zum Jahresende gekaufter Karren zählt erst nach zwei Jahreswechseln, also ein Jahr vorsichtiger als nötig – genauer ginge es nur mit einer Abfrage des Kerns). Kein neuer Zustand im Spielstand. Nicht in der Not (`InNot`), wie Ausbau und Expansion: sonst verkaufte `KarrenNotverkauf` den neuen Karren in der nächsten Stunde mit Verlust (Nachprüfung). | 01.11.2026 |
| Alte Daten | Ohne `logistik.weglaenge`, ohne Karte oder ohne Weg (`KarrenwahlNachDurchfluss` falsch) fahren alle Arten gleich lang: dann die alte Regel ohne Tausch – Messungen ohne Karte bleiben gleich. | 01.11.2026 |
| Bauort | `WerkstattBauen` ordnet die Bauorte wie bisher nach Luftlinie zum nächsten Platz und nimmt unter den ersten `StandortKandidaten` (20) den mit der kürzesten Fahrt des Startkarrens hin und zurück, bei Gleichstand den näheren (`KiRivale.Standort.cs`); ohne Fahrzeit nach Weglänge den ersten. Eine Schätzung über das Netz ab der Mitte des Grundrisses taugte nicht: Die Drehung bestimmt Hof und Zubringer (Greybox #2: geschätzt 45, wirklich 126 min). Deshalb fragt die KI die Kern-Abfrage `Stadtwirtschaft.FahrzeitAmBauort` (vorher eine eigene Kopie der Strecke, beim Zusammenführen in phase5/gehtempo ersetzt); `KiStandortTests` baut an fünf Orten (Greybox, Köln über das Pflaster) und vergleicht mit `Stadtwirtschaft.Fahrzeit`. In Köln ist der erste nach Luftlinie schon der beste. Der bestehende Test „baut neu – nahe am Markt“ prüft jetzt „unter den 20 nächsten“. | 01.11.2026 |
| Tests | `KiKarrenwahlTests` (Durchfluss nach Lager und Fahrzeit, Formel, Holzschlag bekommt das Pferd, Tausch am Bauernhof, Warten auf den Karren, Sperre, Kölner Jahr mit Tausch im Lauf des Jahres, Tausch erst in der Frist, Höchstdauer: Ochse statt Pferd, Geld, alte Daten mit einem Kölner Jahr, ein Kölner Jahr „Schwer“), `KiStandortTests` (Schätzung = Kern, Greybox baut am schnellsten der nächsten, alte Daten nach Luftlinie). | 01.11.2026 |

### Umland nach dem Karrenweg (Branch phase5/deutz-ki)

| Thema | Entscheidung | Datum |
|---|---|---|
| Befund | Seit dem Gehtempo lagen die KI-Häuser in Köln 11–13 % unter dem alten main. Fast der ganze Verlust kam aus dem Umland (Teil 5, Normal, je KI-Haus in 20 Jahren 32.971 → 26.510 Gulden; Startwerkstätten −1.650, Neubauten +1.155): Die Fahrten dauern 263 statt 116 Minuten, die Holzschläge (nur Holz, Lager 40) stehen trotz drei Karren 24–28 % still, und die KI pachtete Liegenschaften ohne Blick auf den Weg – den Holzschlag im Eichenforst an der Höchstdauer (360 Minuten, 54 % Stillstand, 75 Gulden im Jahr), den Kräutergarten am Bach (325 Minuten, 25 %). | 1.11.2026 |
| Pachten nach dem Weg | `Expandieren` (jetzt über `Expansionskandidaten`) rechnet eine freie Umland-Liegenschaft mit `NachDemWeg` (`KiRivale.Umland.cs`): Gewinn − (1 − Anteil) × Wert der vollen Förderung; die Karren, die der Weg über den Startkarren hinaus verlangt, zählen zu den Kosten (Preis der Art mit dem meisten Durchfluss, bei Gleichstand – etwa an der Höchstdauer – der billigeren, wie beim Karrenkauf; Befund M1 der Prüfung). Der Holzschlag im Eichenforst (drei Ochsen, Anteil 0,37, ≈ 175 statt 1.878 Gulden im Jahr) bleibt so nur noch letzter Kandidat. `Anteil = min(1, Karren × Durchfluss × FahrminutenJeKarrenUndJahr ÷ Förderung)` mit höchstens `MaxKarrenJeBetrieb` Karren dieser Art; Durchfluss wie beim Karrenkauf (Ladung ÷ Fahrzeit hin und zurück), Förderung mit der besten Arbeitskraft und Ware wie `Kalkulation.ErwarteterJahresgewinn`. Werkstätten und Neubauten bleiben wie bisher (deren Ort wählt `Bauort`). | 1.11.2026 |
| Fahrzeit der Liegenschaft | Dieselbe Strecke, die der Kern nach der Pacht rechnet: `Stadtwirtschaft.StreckeWieImBild` ab `Bauen.PlatzVon(Name)`, Fahrzeit mit `FahrzeitenAuf` (Belag, Steigung, Furten, Mindest- und Höchstdauer). Die Strecke merkt sich die KI je Liegenschaft (die Karte ändert sich nicht; der Belag zählt jedes Mal neu) – nicht im Spielstand, nach dem Laden gleich neu gerechnet. Ohne `logistik.weglaenge`, Karte oder Weg die alte Schätzung. | 1.11.2026 |
| Eichung | `FahrminutenJeKarrenUndJahr` = 600: So viele Minuten fährt ein Umlandkarren im Mittel im Jahr (der Rest ist Warten aufs Laden, vor dem Tor, hinter dem Karren davor). Geeicht an den Kölner Liegenschaften auf main ec36ff33 (Mittel über die Jahre): gemessen weggebracht (1 − Stillstand, Teil 5 Normal, 20 Startwerte) Eichenforst 46 %, Holzschlag Hardevust 72 %, Kräutergarten am Bach 75 %, Felsenhang 87 %; geschätzt 37, 79, 76, 83 %. Mit 420 (aus der Rechnung mit Tempo 1) war die Schätzung zu streng. Eine Konstante der KI, kein Wert in `wirtschaft.json`. | 1.11.2026 |
| Vorarbeiter mit Lager | Im Umland ab `StillstandFuerKarrenkauf` Stillstand (Fahrzeit nach Weglänge) bewertet `EigenschaftWert` die Eigenschaft „Ordnungsliebend“ (+20 Lager je Ware) wie einen Karrenkauf: Durchfluss mit dem größeren Lager, `Stillstand nachher = vorher ^ (Fluss nachher ÷ vorher)`, × Wert eines vollen Arbeitsjahres; andere Eigenschaften × (1 − Stillstand), weil mehr Förderung nur hilft, wo sie auch weggefahren wird. Der Kern bewertet Lager mit 0, die KI wählte es deshalb nie, solange eine andere der drei Eigenschaften Gulden brachte. Am Holzschlag fasst ein Kriegskarren dann 60 statt 40. Werkstätten wie bisher. | 1.11.2026 |
| Tests | `KiUmlandTests`: Fahrzeit der Liegenschaft = Kern nach der Pacht (vier Kölner Liegenschaften, alle Karrenarten); Schätzung (Silberberg voll, Eichenforst 0,3–0,45, Kräutergarten am Bach 0,6–0,9, Karren und Kosten); in Köln steht der Eichenforst höchstens als letzter Kandidat (Kosten 2 Ochsen), der nähere Holzschlag weit davor; alte Daten wie bisher; Vorarbeiter im gebremsten Holzschlag (ein Jahr „Schwer“) wählt Lager. | 1.11.2026 |

## Messungen

Neue Messungen der Spiel-KI stehen hier (mit main-Commit), nicht mehr in `docs/WIRTSCHAFTSMODELL.md`. Ältere: dort bis §42 (Taschendiebe, PR #69).

### Umland nach dem Karrenweg (Branch phase5/deutz-ki)

Gemessen auf main `ec36ff33` + dieser Branch (`97001082`, mit den Häusern in Deutz), `messen bericht 20 20 --teile=5 --karte=koeln|greybox --parallel=3`, 20 Startwerte, Reinvermögen nach 20 Jahren (Mittel ± Fehler). Keine Partie mit Fehler, kein Bankrott, kein Haus erloschen.

| | main ec36ff33 | Branch |
|---|---|---|
| Köln Normal: ausbauender Spieler (Bezug) | 56.503 ± 3.987 | 54.240 ± 4.070 |
| Köln Normal: KI-Häuser | 65.793 ± 1.576 | 66.805 ± 1.782 (+1,5 %) |
| Köln Normal: Median KI ÷ Bezug, davongezogen | 1,18, 15/140 | 1,26, 16/140 |
| Köln Schwer: Bezug / KI-Häuser | 77.280 / 95.778 | 79.314 / 100.162 (+4,6 %) |
| Köln Schwer: Median, davongezogen | 1,24, 18/140 | 1,23, 25/140 |
| Köln Umland Normal: Betriebe, Gewinn je Jahr, Stillstand | 287, 1.322, 16 % | 265, 1.405, 13,6 % |
| Köln: Holzschlag im Eichenforst gepachtet (Betriebsjahre, Normal / Schwer) | 230 / 291 | 12 / 0 |
| Greybox Normal: Bezug / KI-Häuser, davongezogen | 62.268 / 74.507, 3/60 | 61.749 / 75.344, 4/60 |
| Greybox Schwer: Bezug / KI-Häuser, davongezogen | 63.499 / 116.029, 24/60 | 61.613 / 117.546, 25/60 |

Die KI-Häuser in Köln gewinnen etwas (Normal innerhalb des Fehlers, „Schwer“ +4.400), das Ziel 75.482 (alter main, Teil 5, 10 Startwerte) erreichen sie nicht: Die Umlandbetriebe der KI-Häuser liegen auf der Karte 220–290 Minuten vom Markt, ihr Stillstand bleibt (Holzschläge 21–29 %), und der Gewinn je KI-Haus aus dem Umland in 20 Jahren bleibt bei ≈ 26.400 (alter main 32.971). Was die Änderung bringt, ist das Meiden des Eichenforsts (75 Gulden im Jahr) und eine bessere Wahl der Liegenschaften (Umland 1.405 statt 1.322 je Jahr, Neubauten +2.600 je Haus). „Schwer“: 25 statt 18 von 140 KI-Häusern ziehen davon (> 2× Bezug) – der Bezugsspieler schwankt dort stark (± 7.365). Der Rest ist Karte (Lage des Umlands) oder Kern (Höchstdauer, Karren). Messdateien im Notizordner (`…/scratchpad/deutz-ki/mess/`).

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

### KI nach Meinungen (PR #231)

Gemessen auf main `391874f5` (bis `b9825f32` nur Infra) + `13531d4d`, `messen bericht 20 30` gegen main (Werte von main gleich `balancing-2026-10-28-charakterzuege.json`) → `docs/messungen/balancing-2026-10-28-meinungen.json`: KI ÷ Bezugsspieler mit der Strategie der KI (Median) 1,01 → 1,11, davongezogen 3 → 3 von 90, abgeschlagen 1 → 1; „Schwer“ 1,79 → 1,78; Geschenke der KI-geführten Messspieler je Jahr −25 … −92 → −14 … −55 (Aktivitäten statt Geschenke, Abnahme je weiterem Geschenk), Familienkosten +6 … +29 (Aktivitäten); Diebstahl 0,80 → 0,81, Einbruch 0,69 → 0,66, Sabotage 0,01 → 0,02 je Jahr; Spitzel 91 → 92, Gericht 231 → 216 je Jahr; Karren in Not 7 → 8. Sonde (30 Partien): KI-Häuser verheiratet nach 3 Jahren 88/90 wie main, nach 6 Jahren 90/90 (main 89/90).

### Schankschluss (PR #274)

Gemessen auf main `24963d29` + PR #274, Wirt-Partie (Spieler Wirt, eine Schänke, 123 Einwohner), Startwerte 1400 und 99, Tage 2–4, alle 5 Minuten mit `Wo(VergangeneMinuten)` gezählt; Ø je Viertelstunde, in Klammern der Höchstwert. Vorher und nachher zeichengleich (die Änderung prüft nur Daten); „Sonde“ = dieselbe Zählung mit der Uhrzeit wie in der Ton-Messung:

| Uhr | Schänke 1400 | daheim 1400 | unterwegs 1400 | Schänke 99 | daheim 99 | unterwegs 99 | Sonde Schänke 1400 / 99 |
|---|---|---|---|---|---|---|---|
| 20:00 | 10,4 (26) | 16,6 | 68,4 | 8,3 (20) | 11,2 | 74,9 | 24,7 / 20,0 |
| 20:45 | 35,8 (42) | 76,2 | 10,3 | 38,3 (47) | 71,0 | 13,0 | 24,7 / 20,0 |
| 21:00 | 18,7 (36) | 39,0 | 64,7 | 21,9 (44) | 37,3 | 63,1 | 37,3 / 40,0 |
| 21:45 | 78,0 (84) | 43,4 | 0,9 | 73,0 (78) | 48,6 | 0,8 | 37,3 / 40,0 |
| 22:00 | 29,3 (65) | 43,7 | 49,3 | 26,4 (65) | 48,7 | 47,2 | 116,0 / 113,7 |
| 22:15 | 0 (0) | 46,4 | 75,9 | 0 (0) | 52,9 | 69,4 | 116,0 / 113,7 |
| 22:30 | 0 (0) | 87,4 | 34,9 | 0 (0) | 95,3 | 27,0 | 116,0 / 113,7 |
| 22:45 | 0 (0) | 122,1 | 0,2 | 0 (0) | 121,8 | 0,6 | 116,0 / 113,7 |

- In der Viertelstunde ab 22:00 sitzen nur noch die, deren Abmarsch (0–9 Minuten nach der vollen Stunde) noch nicht gekommen ist; ab 22:10 ist die Schänke leer. Zwischen den vollen Stunden ist ein guter Teil der Stadt unterwegs (Wege 30 ± 10 Minuten), um 21:15 etwa 96 und um 22:15 etwa 69–76 auf dem Weg zur bzw. von der Schänke.
- Gäste und Umsatz der Schänke (Tage 1–10) und `messen 30 20` (Eisenbart Ø 176.661, Eichwald Ø 135.108, Kieselbach Ø 172.261) zeichengleich mit main.

### Karren und Bauort nach dem Karrenweg (PR #282)

Gemessen auf main `880e3387` und phase5/gehtempo `dd347586` („vorher“) + dieser Branch (`5314f99d`; `ad482322` ändert nur den Tausch in der Not), `messen bericht 20 20 --teile=5 --karte=greybox`. KI-Stärke: Der Bezugsspieler (Schmied, Mensch) wird von der KI geführt; Reinvermögen nach 20 Jahren, Mittel ± Fehler des Mittels:

| Greybox | main | vorher | nachher |
|---|---|---|---|
| Normal: Bezugsspieler | 68.066 ± 3.060 | 56.004 ± 2.730 | 62.268 ± 2.434 |
| Normal: KI-Häuser | 78.461 ± 2.826 | 69.017 ± 2.921 | 74.507 ± 2.966 (−5,0 % gegen main) |
| Normal: Median KI ÷ Bezug, davongezogen | 1,08, 3/60 | 1,14, 9/60 | 1,11, 3/60 |
| Schwer: Bezugsspieler | 68.162 ± 1.821 | 46.818 ± 2.960 | 63.499 ± 2.890 |
| Schwer: KI-Häuser | 119.993 ± 2.666 | 110.814 ± 2.747 | 116.029 ± 2.112 (−3,3 %) |
| Schwer: Median, davongezogen | 1,81, 14/60 | 2,27, 43/60 | 1,83, 24/60 |
| je Haus Normal (Eisenbart, Eichwald, Kieselbach) | 86.760, 63.364, 85.260 | 79.036, 56.492, 71.524 | 86.999, 60.847, 75.675 |

Kein Bankrott. Woher der Rückgang kam (Greybox, Normal, 4 Startwerte, Jahr 15, je KI-Werkstatt): vorher lagen 7 von 23 Werkstätten über 60 min vom Markt (gebaut „an der Ringgasse“ und „am Felsenhang“, 99–174 min, drei Handkarren, 44 % Stillstand, Gewinn Ø 435 statt 4.072); mit Karrenwahl allein 31 % Stillstand, Gewinn 621; mit Bauort nach Karrenweg nur noch 2 weite, alle Werkstätten zusammen 73.613 (vorher 68.197, main 73.025). Einzelne Versuche (8 Startwerte, Greybox): immer Kriegskarren im Umland KI Schwer +1,8 %, Ochse und Tausch +1,3 %, immer Handkarren −10 %. **Köln** (Teil 5, 20 Startwerte) lief bei Abgabe noch (Rechner mit Last um 30); nachtragen.

