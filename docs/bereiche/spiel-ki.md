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
- Die Schätzung „Stillstand vorher ^ (Durchfluss nachher ÷ vorher)“ ist im ersten Jahr zu optimistisch: Die Prüfung sah in Köln nach dem ersten Kauf 7–12 % geschätzt und 14–41 % im Folgejahr gemessen (Grube 18 %, Bauernhof 41 %) – das erste Jahr hat Anlaufverluste (leere Karren, Lager füllt sich). Die KI kauft dann im Jahr darauf nach; schlimmstenfalls einen Karren zu spät. Bei Gelegenheit das erste Jahr eines Betriebs nicht als Eichung nehmen.
- (von Kern) Sitzplätze der Schänken (Datenversion 67): Die Einwohner finden in einer Schänke höchstens ⌈Plätze × Einwohner ÷ 17⌉ Plätze (`Gastwirtschaft.EinwohnerPlaetze`, `Einwohnerschaft.Quote` mit Grenze, Ersatzhandlung, Stillen nach der Verteilung; `OhneSchankplatz`). Entscheidung des Nutzers vom 2.11.2026; mit 17 und mindestens 20 Plätzen ist die Grenze immer größer als die Einwohnerzahl, greift also nie – die Kopplung wirkt wie bisher. Zur Kenntnis, nichts zu tun.
- (von Darstellung) Hafenarbeit plant Bündel zu 3 Stücken (`Zierfahrplan.HubGroesse`, `HubAnteil`, `Hafenarbeit.Planen`, Test angepasst), Teamleiter informiert. Bitte bei der nächsten Änderung an der Hafenarbeit prüfen, ob die Aufteilung (erster Hub kürzer, Träger ohne Wartezeit zwischen Bündeln) so bleiben soll.
- (von Kern) Der Test `BuergerTorwacheTests` „Torwache: Spielstand geladen – dieselben Wächter …“ läuft allein 233 s (30 Jahre doppelt getickt) – ein Drittel aller Kern-Tests. Er steht jetzt in `LangsameTests.cs`; prüfen, ob weniger Jahre (z. B. nur bis zum ersten Ruhestand) dasselbe zeigen.

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
| Durchfluss | `Durchfluss(b, art)` = Ladung ÷ (Fahrzeit hin + zurück des Kerns, also mit Steigung, Belag samt Wegebau, Furten und Mindest- und Höchstdauer). Ladung = was der Karren fasst, höchstens Lager je Ware × Zahl der Waren (Umland: Rohstoffe; Werkstatt: Waren in Arbeit oder Zutaten-Sorten, was mehr ist). Ein Holzschlag füllt den Pferdekarren (75) also nie, ein Bauernhof mit zwei Waren schon. | 01.11.2026 |
| Wirkung | `Stillstand nachher = Stillstand vorher ^ (Durchfluss nachher ÷ vorher)` (`DetMathe.Pow`). Jeder zusätzliche Durchfluss drückt den Stillstand um denselben Faktor; geeicht am Stillstand des Vorjahres, ohne Bedarf und Zeitfenster zu kennen. An den Kölner Umlandbetrieben nachgerechnet: zwei Ochsen 50 % → drei Ochsen geschätzt 35 %, gemessen 32–38 %; Ochse und zwei Pferde geschätzt 23 %, gemessen 22 %; Bauernhof Ochse und zwei Pferde geschätzt 4 %, gemessen 1–2 %. Linear geschätzt (Stillstand fällt mit dem Kehrwert) versprach ein zweiter Karren derselben Art zu viel. Mehrgewinn = (vorher − nachher) × Wert eines vollen Arbeitsjahres (`Kalkulation.Leistung` × Arbeitsminuten × `WertJeArbeitsminute`); Eskorte je Fahrt zählt nicht (zwei bis drei Fahrten im Jahr). | 01.11.2026 |
| Wahl | Wie bisher nur ab `StillstandFuerKarrenkauf` (20 %) im Vorjahr. Unter `MaxKarrenJeBetrieb` zum Jahresende je Art ein Kauf; an der Grenze je Karren ein Tausch gegen eine Art mit mehr Durchfluss (Kosten = Preis − Erlös, beim Ochsen 900 − 200). Gewählt wird der größte Wert `Mehrgewinn − Kosten ÷ Amortisationsjahre` (wie beim Kauf eines Betriebs), der sich in `Amortisationsjahre` bezahlt macht und die Reserve lässt; bei Gleichstand der billigere. **Nicht** nach Durchfluss je Gulden: Der Handkarren brächte je Gulden oft am meisten, belegte aber einen der drei Plätze – Greybox mit immer Handkarren im Umland: KI-Häuser bei „Schwer“ 99.462 statt 111.112 (8 Startwerte). | 01.11.2026 |
| Tausch | Erst gebaut zum Jahresende mit den Karren, die dann am Hof stehen – die Prüfung fand in Köln in sieben Jahren keinen Tausch: Um 23 Uhr wartet der langsamste Karren eines gebremsten Umlandbetriebs meist vor dem Tor. Jetzt plant die KI über alle Karren und tauscht in der Stundenrunde (`KarrenTauschen`), sobald der gewählte am Hof steht; bei gleich guten den, der am Hof steht. Nur wenn jeder Karren ein ganzes Vorjahr gezählt hat (`TauschBereit`, `Karren.FahrtenVorjahr` gesetzt): Dann zeigt der Stillstand des Vorjahres genau diese Karren, und nach einem Kauf oder Tausch wartet sie, bis das wieder gilt (ein zum Jahresende gekaufter Karren zählt erst nach zwei Jahreswechseln, also ein Jahr vorsichtiger als nötig – genauer ginge es nur mit einer Abfrage des Kerns). Kein neuer Zustand im Spielstand. Nicht in der Not (`InNot`), wie Ausbau und Expansion: sonst verkaufte `KarrenNotverkauf` den neuen Karren in der nächsten Stunde mit Verlust (Nachprüfung). | 01.11.2026 |
| Alte Daten | Ohne `logistik.weglaenge`, ohne Karte oder ohne Weg (`KarrenwahlNachDurchfluss` falsch) fahren alle Arten gleich lang: dann die alte Regel ohne Tausch – Messungen ohne Karte bleiben gleich. | 01.11.2026 |
| Bauort | `WerkstattBauen` ordnet die Bauorte wie bisher nach Luftlinie zum nächsten Platz und nimmt unter den ersten `StandortKandidaten` (20) den mit der kürzesten Fahrt des Startkarrens hin und zurück, bei Gleichstand den näheren (`KiRivale.Standort.cs`); ohne Fahrzeit nach Weglänge den ersten. Eine Schätzung über das Netz ab der Mitte des Grundrisses taugte nicht: Die Drehung bestimmt Hof und Zubringer (Greybox #2: geschätzt 45, wirklich 126 min). Deshalb fragt die KI die Kern-Abfrage `Stadtwirtschaft.FahrzeitAmBauort` (vorher eine eigene Kopie der Strecke, beim Zusammenführen in phase5/gehtempo ersetzt); `KiStandortTests` baut an fünf Orten (Greybox, Köln über das Pflaster) und vergleicht mit `Stadtwirtschaft.Fahrzeit`. In Köln ist der erste nach Luftlinie schon der beste. Der bestehende Test „baut neu – nahe am Markt“ prüft jetzt „unter den 20 nächsten“. | 01.11.2026 |
| Tests | `KiKarrenwahlTests` (Durchfluss nach Lager und Fahrzeit, Formel, Holzschlag bekommt das Pferd, Tausch am Bauernhof, Warten auf den Karren, Sperre, Kölner Jahr mit Tausch im Lauf des Jahres, Tausch erst in der Frist, Höchstdauer: Ochse statt Pferd, Geld, alte Daten mit einem Kölner Jahr, ein Kölner Jahr „Schwer“), `KiStandortTests` (Schätzung = Kern, Greybox baut am schnellsten der nächsten, alte Daten nach Luftlinie). | 01.11.2026 |

### Umland nach dem Karrenweg (PR #286)

| Thema | Entscheidung | Datum |
|---|---|---|
| Befund | Seit dem Gehtempo lagen die KI-Häuser in Köln 11–13 % unter dem alten main. Fast der ganze Verlust kam aus dem Umland (Teil 5, Normal, je KI-Haus in 20 Jahren 32.971 → 26.510 Gulden; Startwerkstätten −1.650, Neubauten +1.155): Die Fahrten dauern 263 statt 116 Minuten, die Holzschläge (nur Holz, Lager 40) stehen trotz drei Karren 24–28 % still, und die KI pachtete Liegenschaften ohne Blick auf den Weg – den Holzschlag im Eichenforst an der Höchstdauer (360 Minuten, 54 % Stillstand, 75 Gulden im Jahr), den Kräutergarten am Bach (325 Minuten, 25 %). | 1.11.2026 |
| Pachten nach dem Weg | `Expandieren` (jetzt über `Expansionskandidaten`) rechnet eine freie Umland-Liegenschaft mit `NachDemWeg` (`KiRivale.Umland.cs`): Gewinn − (1 − Anteil) × Wert der vollen Förderung; die Karren, die der Weg über den Startkarren hinaus verlangt, zählen zu den Kosten (Preis der Art mit dem meisten Durchfluss, bei Gleichstand – etwa an der Höchstdauer – der billigeren, wie beim Karrenkauf; Befund M1 der Prüfung). Der Holzschlag im Eichenforst (drei Ochsen, Anteil 0,37, ≈ 175 statt 1.878 Gulden im Jahr) bleibt so nur noch letzter Kandidat. `Anteil = min(1, Karren × Durchfluss × FahrminutenJeKarrenUndJahr ÷ Förderung)` mit höchstens `MaxKarrenJeBetrieb` Karren dieser Art; Durchfluss wie beim Karrenkauf (Ladung ÷ Fahrzeit hin und zurück), Förderung mit der besten Arbeitskraft und Ware wie `Kalkulation.ErwarteterJahresgewinn`. Werkstätten und Neubauten bleiben wie bisher (deren Ort wählt `Bauort`). | 1.11.2026 |
| Fahrzeit der Liegenschaft | Dieselbe Strecke, die der Kern nach der Pacht rechnet: `Stadtwirtschaft.StreckeWieImBild` ab `Bauen.PlatzVon(Name)`, Fahrzeit mit `FahrzeitenAuf` (Belag, Steigung, Furten, Mindest- und Höchstdauer). Die Strecke merkt sich die KI je Liegenschaft (die Karte ändert sich nicht; der Belag zählt jedes Mal neu) – nicht im Spielstand, nach dem Laden gleich neu gerechnet. Ohne `logistik.weglaenge`, Karte oder Weg die alte Schätzung. | 1.11.2026 |
| Eichung | `FahrminutenJeKarrenUndJahr` = 600: So viele Minuten fährt ein Umlandkarren im Mittel im Jahr (der Rest ist Warten aufs Laden, vor dem Tor, hinter dem Karren davor). Geeicht an den Kölner Liegenschaften auf main ec36ff33 (Mittel über die Jahre): gemessen weggebracht (1 − Stillstand, Teil 5 Normal, 20 Startwerte) Eichenforst 46 %, Holzschlag Hardevust 72 %, Kräutergarten am Bach 75 %, Felsenhang 87 %; geschätzt 37, 79, 76, 83 %. Mit 420 (aus der Rechnung mit Tempo 1) war die Schätzung zu streng. Eine Konstante der KI, kein Wert in `wirtschaft.json`. | 1.11.2026 |
| Vorarbeiter mit Lager | Im Umland ab `StillstandFuerKarrenkauf` Stillstand (Fahrzeit nach Weglänge) bewertet `EigenschaftWert` die Eigenschaft „Ordnungsliebend“ (+20 Lager je Ware) wie einen Karrenkauf: Durchfluss mit dem größeren Lager, `Stillstand nachher = vorher ^ (Fluss nachher ÷ vorher)`, × Wert eines vollen Arbeitsjahres; andere Eigenschaften × (1 − Stillstand), weil mehr Förderung nur hilft, wo sie auch weggefahren wird. Der Kern bewertet Lager mit 0, die KI wählte es deshalb nie, solange eine andere der drei Eigenschaften Gulden brachte. Am Holzschlag fasst ein Pferdekarren dann 60 statt 40. Werkstätten wie bisher. | 1.11.2026 |
| Tests | `KiUmlandTests`: Fahrzeit der Liegenschaft = Kern nach der Pacht (vier Kölner Liegenschaften, alle Karrenarten); Schätzung (Silberberg voll, Eichenforst 0,3–0,45, Kräutergarten am Bach 0,6–0,9, Karren und Kosten); in Köln steht der Eichenforst höchstens als letzter Kandidat (Kosten 2 Ochsen), der nähere Holzschlag weit davor; alte Daten wie bisher; Vorarbeiter im gebremsten Holzschlag (ein Jahr „Schwer“) wählt Lager. | 1.11.2026 |

### Karren-Höchstzahl (PR #286)

| Thema | Entscheidung | Datum |
|---|---|---|
| Regel der KI | `KiRivale.MaxKarren(ort)` = Grenze des Kerns, ohne sie das Profil (3). Den vierten und fünften Karren kauft die KI wie jeden: zum Jahresende ab 20 % Stillstand, wenn er sich in der Frist bezahlt macht (`BesterKarrenplan`); getauscht wird erst an der Grenze von 5. Die Schätzung beim Pachten (`UmlandSchaetzen`) rechnet weiter mit höchstens 3 Karren (Profil), auch wo der Kern 5 erlaubt – mit 5 pachtete die KI den Eichenforst wieder (Befund H1 der Prüfung, Messung unten). | 2.11.2026 |
| Alte Regel | Ohne Fahrzeit nach Weglänge (keine Karte, kein Weg) kauft die KI wie bisher Ochsen- bzw. Handkarren ohne Amortisation und höchstens bis zur Grenze des Profils (3), auch wenn der Kern 5 erlaubt (Befund N2 der Prüfung). | 2.11.2026 |

### Jahreszeiten (PR #292, von kern gebaut)

| Thema | Entscheidung | Datum |
|---|---|---|
| Schiffe im Eis | `SchiffeBleibenImHafen`: Ist der Fluss zu oder für heute angekündigt (`EisDroht`, die Stunde steht in der Meldung zum Jahresbeginn), schickt die KI kein Schiff los; nach dem Eisgang wieder. | 2.11.2026 |
| Vorrat vor dem Winter | `WinterVorrat` (`ki.jahreszeiten.vorratAbStunde` 17): Im Herbst ab 17 Uhr füllt der Verwalter eines KI-Hauses Zutaten mit Winternachfrage oder Ernte (Holz, Gerste, Roggen, Kräuter) bis zur Lagergrenze – über `Karren.Vorratsziel`, zum Marktpreis, im Rahmen von `zutatenAnteilKasse`. Reine Rechnung aus Uhr und Daten. Der Verwalter des Spielers tut das nicht (Designfrage beim Teamleiter). | 2.11.2026 |

### Ware der Schänke nach dem Ausschank (PR #292)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Endmessung Jahreszeiten (kern, `docs/bereiche/kern.md` Offen): Der Wirt verliert mit den Jahreszeiten 8–9 %; die KI wählte die Ware der Schänke wie jede Werkstatt nach dem Marktpreis (`Kalkulation.GewinnJeArbeitsminute`), obwohl die Gäste den festen Ausschankpreis (Grundpreis × 1,2) zahlen. | 3.11.2026 |
| Regel | `KiRivale.SchankwertJeArbeitsminute(betrieb, ware)` (`Ki/KiRivale.Schaenke.cs`): (Erlös − Zutaten zum Marktpreis) ÷ Zeit mit Erlös = Anteil × Ausschankpreis + (1 − Anteil) × Marktpreis, nach Umsatzsteuer. Anteil = `Gastwirtschaft.ErwarteteGaeste` × `ki.schaenke.gaesteanteil` ÷ Stück im Jahr (`Kalkulation.Leistung` × Arbeitsminuten ÷ `MinutenJeStueck`, nur diese Ware), höchstens 1; ohne Personal 1. Weingeist (kein Ausschank) zum Marktpreis. `ProduktWaehlen` nimmt diesen Wert für Schänken; Wechselschwelle und Mindeststunden bleiben. Gilt für KI-Häuser und den von der KI geführten Messspieler. | 3.11.2026 |
| Schalter | Neuer Teil `ki.schaenke` (`KiSchaenkeDaten`, `gaesteanteil` 0–1, Standard 1) – **nicht** in `wirtschaft.json`, keine Datenversion. Ohne den Teil rechnet die KI bitgleich wie vorher (Test). Messung: `messen jahreszeiten … --ki-schaenke[=anteil]` (kleine Ergänzung in `tests/Reichsgilde.Core.Tests/Jahreszeitenmessung.cs`, Bereich kern). | 3.11.2026 |
| Tests | Neu `KiSchaenkeWareTests` (5): ohne Teil gleich `GewinnJeArbeitsminute`; mehr Gäste als Stück = Ausschankpreis; Anteil unter 1 gemischt, Anteil 0 = Marktpreis; Körnermahlzeit am Markt teuer → ohne Teil stellt die KI vom Dünnbier um, mit bleibt sie; Prüfung des Werts. | 3.11.2026 |
| Ergebnis | Gemessen, bleibt aus (Nutzer, 3.11.2026): volle Messung auf dem Grafik-PC (Datenversion 69, 80 Startwerte, Jahr 20, beide Karten, main `6f80481b` + Branch phase5/jahreszeiten `0512c835`) mit `--ki-schaenke` (Anteil 1) und `--ki-schaenke=0.75` – der Wirt ist absolut rund 3.000 Gulden ärmer als ohne den Schalter. `ki.schaenke` kommt nicht in `wirtschaft.json`; der Code bleibt, weil er ohne den Teil bitgleich rechnet. | 3.11.2026 |

### Bürgerdichte und Tagesplan (PR #323)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Nutzer 5.11.2026 zu PR #317 (namenlose Statisten): „Die Passanten müssen schon echt sein, genauso anklickbar wie die anderen. Köln ist größer als die Greybox, da müssen auch mehr Leute sein.“ Befund: 25 Einwohner je Haus, Köln 223 für die ganze Stadt, am Heumarkt 1–4 Figuren. Ursachen: die Zahl hing nur an den Häusern (Köln hat 82 Hausplätze, bewohnt waren rund 40), tagsüber ruhten viele daheim (10 und 15 Uhr 40 von 223) und Knechte/Mägde dienten drinnen; mittags sitzt ein Fünftel in der Schänke, die übrigen laufen zur halben Stunde meist irgendwo durch die große Stadt. | 5.11.2026 |
| Zahl | `buerger.jeHausplatz` (6): Mit Karte ein Haushalt je freiem Hausplatz (Einwohnerplätze + Kulissenhäuser − Ratsherren- und Bürgerhäuser) mit 5–7 Personen. Köln 82 Plätze → rund 395 gewöhnliche, 418 Einwohner insgesamt (vorher 223); Greybox 40 → 167 (123). Ohne Karte oder wenn die Plätze weniger fassen als jeHaus × Häuser: alte Regel. Kein neues Wohnhaus teilt sich mehr den letzten Vorstadtplatz. | 5.11.2026 |
| Tagesplan | `buerger.tagesplan` (8–20 Uhr, `rastDraussen` 0,7, `besorgungen` 0,5): Wer ruht, rastet am nächsten Markt; Knechte und Mägde machen dort Besorgungen. Fest aus Nummer, Tag und Doppelstunde (`Wurf`), ohne Generator. Nur der Ort ändert sich, die Handlung nie. Auswahltafel: „hält ein Schwätzchen“, „macht Besorgungen“ (kleine Änderung in `Stadtansicht.Passanten.cs`, Bereich darstellung). | 5.11.2026 |
| Wirtschaftsgewicht | Erste Fassung (alle zählen, Warenkorb anteilig gewichtet) machte den Spieler mit einem Betrieb in Köln 10–20 % reicher; die Gegenprobe `jeHaus` 50 nach alter Regel zeigte dasselbe – schon die Zahl wirkt über die Kopplungen. Jetzt zählen für Markt, Warenkorb, Schänke, Messe und Taschendiebe nur jeHaus × Häuser gewöhnliche Einwohner (`ZaehltFuerWirtschaft`); sie entstehen und würfeln wie vorher, die übrigen mit eigenem Generator. Ergebnis bitgleich (Test, Messung). | 5.11.2026 |
| Daten | Neue Felder nur im eigenen Abschnitt `buerger`, **keine Datenversion** nötig: Ohne die Felder rechnet alles wie bisher; alte Spielstände laden mit ihren mitgespeicherten Daten und Einwohnern unverändert (neue Felder im Spielstand fehlen dort und gelten als „wie vorher“). | 5.11.2026 |
| Darstellung | Ab 300 Einwohnern rechnet die Darstellung außerhalb des Bildes sparsamer (`VieleFiguren`, kleine Änderung im Bereich darstellung: `Stadtansicht.Passanten.cs`, `.Figuren.cs`, `.Marktbummel.cs`, `.Passantenausweichen.cs`). Das Ziel „+2 ms“ wird damit noch nicht erreicht (Messung unten); weitere Hebel bei darstellung. | 5.11.2026 |
| Tests | `BuergerDichteTests` (9): ohne Karte bzw. ohne Feld alte Zahl; Köln/Greybox ein Haus je Platz mit 5–7 Bewohnern; Zählende genau jeHaus × Häuser, auch nach Jahren; Tagesplan ändert nur den Ort (8–20 Uhr, Heim → Markt); tagsüber mehr am Markt, ab 20 Uhr niemand draußen rastend; Wurf fest und gleichverteilt; Köln und Greybox zwei Jahre mit und ohne – Vermögen, Nachfrage, Passanten, Handlungen gleich; Speichern/Laden mittags. Angepasst: zwei Prüfungen in `BuergerTests` (Dienst darf am Markt sein), `HafenRuhtTests` nimmt die neuen Felder heraus (Sollwerte vor dem Schalter). | 5.11.2026 |

### Torwache (PR #328)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Vorschlag „Wachen und Diebe“, Paket P1, vom Nutzer am 5.11.2026 mit allen Empfehlungen freigegeben: Torwächter als neue Einwohner-Rolle, echte anklickbare Figuren mit Namen, ohne Gewicht für die Wirtschaft; niemand bezahlt sie; Einfluss der Spieler nur wie bisher über den Stadthauptmann. | 5.11.2026 |
| Regel | Je Tor zwei Mannschaften zu je 2 Wächtern; Frühschicht vom Tagesbeginn bis 14 Uhr, Spätschicht ab 14 Uhr (Anmarsch 13:45–14:00, Abmarsch der Frühschicht 14:00–14:15, je Platz 3 min versetzt), ab 20 Uhr bleibt einer. Mannschaften tauschen täglich. Ruhestand am Jahresende, wenn älter als volljährig + 60 % der Jahre bis zum Sterbealter. Alles fest aus der Uhr bzw. eigenem Generator. | 5.11.2026 |
| Daten | `buerger.torwache` (`jeTorTag` 2, `jeTorNacht` 1, `wechsel` 14, `nachtAb` 20, `weg` 15) und Rolle „Torwächter“ (Gewicht 0, Arbeitsort Tor) im eigenen Abschnitt, **keine Datenversion**: ohne den Abschnitt keine Torwache. Alte Spielstände laden mit ihren mitgespeicherten Daten, also ohne Torwache. | 5.11.2026 |
| Wirtschaft | Nicht in `Einwohner`, keine Nummer aus der Zählung, eigener Generator: bitgleich (Test `BuergerTorwacheTests`: ganzer Spielstand ohne die Torwache gleich, Köln und Greybox, drei Jahre). Keine Messung nötig. | 5.11.2026 |
| Tests | `BuergerTorwacheTests` (7): Zahl je Karte (Köln 40, Greybox 12, ohne Karte/Daten 0); Namen eindeutig, nicht in der Einwohnerliste, per Nummer auffindbar; Schichtplan über einen Tag und Tausch am nächsten; Anteil des Weges; Wirtschaft bitgleich; Spielstand und Ruhestand nach 30 Jahren; Datenprüfung. | 5.11.2026 |
| Test gekürzt (PR „Torwache-Test kurz“) | „Spielstand geladen … Ruhestand“ lief 30 Spieljahre in Original und geladener Partie (208–233 s, ein Drittel der Testzeit), jetzt etwa 2 s: Am Stichtag (13:50) sind ein Drittel der Wächter eins über, ein Drittel genau an der Ruhestandsgrenze; der erste echte Jahreswechsel läuft in beiden Partien, die übrigen 29 nur über `Torwache.Jahresende`. Strenger als vorher: Grenze genau (Alter ≤ Grenze statt ≤ Grenze + 1, „genau an der Grenze“ bleibt noch ein Jahr), Nachfolger jung und mit fortlaufender Nummer, geladene Partie in allen Feldern (Nummer, Geburtsjahr, Volk, Glaube, Dienst) statt nur im Namen gleich, mindestens 5 natürlich Nachgerückte unter den unveränderten Wächtern. Gegenprobe: Würfelstand bzw. Nummernzähler nicht geladen, kein Jahresende der Torwache, Ruhestand ein Jahr zu früh, Nachfolger nicht jung – jeder Fehler macht den Test rot. | 7.11.2026 |

### Hafenarbeit (PR #334)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Entladen Stufe 3: Träger am Hafen sind echte Einwohner (Option „Kern-Zustand Hafenarbeit“, #317 mit Statisten abgelehnt). Ablauf nach Entscheidung des Nutzers vom 6.11.2026: gearbeitet wird, solange ein Schiff liegt, auch vor 8 Uhr; Träger Hand in Hand mit dem Kran, der Stapel nur Puffer, Nachlauf höchstens etwa eine Stunde, dann ist der Steg leer und die Ware im Stapelhaus; dazu ein Vorarbeiter, der beaufsichtigt und im Dunkeln eine Laterne trägt. | 6.11.2026 |
| Fahrplan | Der Fahrplan des Zierschiffs zog wörtlich aus der Darstellung in den Kern (`Zierfahrplan`): reine Funktion von Minute und Fahrzeit, Streuzahl der Fensternummer, ohne gemeinsamen Zufall. Test gegen die alte Rechnung über 40 Jahre (jede Dreiviertelminute, Fahrzeit 20/35/70); Bild vorher und nachher gleich. | 6.11.2026 |
| Wer | 2–4 Träger je Besuch (fest aus Tag und Fensterhälfte), aus der Rolle `rolle` (Tagelöhner) nach einem Rang aus Nummer, Tag und Fensterhälfte – jeden Besuch andere. Vorarbeiter: der älteste freie Tagelöhner (meist derselbe, „Hafenmeister“). Frei ist, wer ruht, flaniert, einkauft oder (bei Arbeitsort Markt) arbeitet; wer einkehrt oder betet, geht dorthin und wird abgelöst: Der Neue eilt herbei (Abmarsch zur vollen Stunde, kürzester Weg), der Abgelöste bleibt, bis er da ist, plus eine Runde, und ist vor der nächsten vollen Stunde am neuen Ort. Ein neuer Vorarbeiter kommt möglichst nicht aus den Trägern. Prüfung (Opus): Befunde zu Ablösung, Wegen über die volle Stunde, Laden zur vollen Stunde und Arbeitsort Hafen behoben. | 6.11.2026 |
| Wirtschaft | Keine Wirkung: Nur Ort (`Stadtort.Hafen`, Stelle „Stapel“ bzw. „Stegkopf“) und Weg ändern sich; `OhnePlan` bildet den Hafen auf den Ort ohne Hafenarbeit zurück, damit der Generator der Bürger dieselben Würfe macht. Test: Vermögen aller Häuser und der ganze Spielstand ohne Orte und Wege gleich, Köln und Greybox, drei Jahre. Kein Lohn, keine Ware. | 6.11.2026 |
| Lohn | Kein Lohn, reine Darstellung (Nutzer 06.11.) | 6.11.2026 |
| Daten, Spielstand | Eigener Abschnitt `buerger.hafenarbeit` (`rolle`, `traegerVon` 2, `traegerBis` 4, `stuecke` 18, `runde` 12), keine Datenversion; ohne Abschnitt keine Hafenarbeit (Stapel wie seit #332). Kein neues Feld im Spielstand: Ort, Stelle und Weg der Einwohner reichen, `Stadtort.Hafen` ist ein neuer Enum-Wert am Ende (als Text gespeichert). | 6.11.2026 |
| Zeitraffer | Ein Gang (Stapel – Lagerhaus und zurück, etwa 2 × 16 m) dauert 12 Spielminuten, bei Tempo 1 gut neun Sekunden: Die Träger eilen wie der Kran im Zeitraffer. Echtes Gehtempo bräuchte etwa 30 Spielminuten je Gang und ginge weit über die Liegezeit hinaus. Abgelegt wird am Lagerhaus des Hafenmodells; das Stapelhaus der Kölner Karte steht 82 m entfernt und ist nicht das Ziel. | 6.11.2026 |
| Bündel (PR „Hafen-Feinschliff“, von Darstellung) | Der Kran hebt je Hub ein Bündel von `Zierfahrplan.HubGroesse` = 3 Stücken (6 Hübe statt 18, damit das Kranhaus langsam schwenken kann). `Hafenarbeit.Planen`: Die Stücke eines Bündels setzt der Kran zugleich ab bzw. hebt sie zugleich (`AufStapel`/`VomStapel` gleich, `nimmt`/`ab` je Stück 0,05 min versetzt); die Träger tragen weiter einzeln. Hübe aus `Zierfahrplan.HubAnteil`: Entladen 2–88 % der Liegezeit mit kürzerem erstem Hub (`ErsterHubGewicht` 0,5, sonst läge der Nachlauf bei zwei Trägern bei 59 min), Beladen 8–90 % in gleichen Hüben. Beladen wählt den Träger mit den wenigsten Gängen, dann den am spätesten gebrauchten: Gänge je Träger 18 / n abgerundet bis aufgerundet, Nachlauf höchstens etwa 55 min. | 7.11.2026 |
| Tests | `BuergerHafenarbeitTests` (10): Fahrplan wie bisher; Hübe schließen lückenlos aneinander; Schichten (2 je Tag, keine im Winter, Gänge ohne Überschneidung und gleichmäßig verteilt, Stapel vor dem Niederländer leer, danach leer, Nachlauf ≤ 57 min); Einwohner am Hafen (n Träger und ein Vorarbeiter, auch vor 8 Uhr, ältester als Vorarbeiter, kommt zuerst und geht zuletzt, danach niemand); Wirtschaft bitgleich; in jeder Minute des Tragens alle da und Wege vom Hafen vor der nächsten vollen Stunde fertig (drei Startwerte); Rolle Knecht holt niemanden von der Arbeit; Spielstand mitten in der Schicht und zur vollen Stunde; ohne Abschnitt; Datenprüfung. | 6.11.2026 |

### Marktrunde der Streife (PR #…)

| Thema | Entscheidung | Datum |
|---|---|---|
| Befund | In Köln war die Marktrunde der Streife vom Wachhaus am Heumarkt nur 5 m lang. Die Runde ging von der Tür zum Knoten des Gassennetzes im Umkreis Halb + 25 m um den Platz, der von der Tür aus am kürzesten zu erreichen war (samt Rest bis zur Mitte), und auf demselben Weg zurück. Das Wachhaus (Werkstattplatz an der Rheingasse) hängt 31 m vor der Mitte des Heumarkts an der Rheingasse, also schon im Umkreis (37 m); auf einer geraden Straße zum Platz sind alle Knoten gleich teuer, gewählt wurde der erste, der vor der Tür. Getrennte Teile des Gassennetzes (Vermutung in `darstellung.md`) waren es nicht. | 08.11.2026 |
| Regel | `Marktrunde.Planen` (`game/scripts/Marktrunde.cs`, reine Rechnung ohne Godot, vom Test mitübersetzt): Um den nächsten Platz vier Seiten (Nord, Ost, Süd, West, je nachdem, ob ein Punkt mehr längs oder quer liegt); je Seite der äußerste Knoten auf einem Weg der Karte in einem Ring (15, 35, 60, 90, 130 m über den Rand des Pflasters), nur auf derselben Seite der Mauer wie der Platz. Die Streife geht von der Tür zuerst über den Platz (seine Mitte), dann reihum über die Seiten (beide Richtungen probiert), die Seite nahe der Tür zuletzt, und zurück zur Tür, jedes Stück auf dem kürzesten Weg des Gassennetzes. Über den Platz zuerst, weil ein Wachmann in einer Schicht (zwei Spielstunden, bei Tempo 1 etwa 150 s) nur rund 140 m weit kommt und danach an der Tür neu beginnt (Regel der Darstellung, `StreifeSchichtWeg`): So ist er in jeder Schicht auf dem Markt, auch wenn er die Runde nicht ganz schafft. Genommen wird der engste Ring mit 150–400 m, mindestens drei Seiten und höchstens einem Viertel doppelt gegangenem Weg (`HoechstDoppelt`; ohne diese Grenze ging die Runde im Spiel erst 15 m die Rheingasse hinauf und zurück, weil das Gassennetz an jedem Hofanschluss einen Knoten hat); gibt es keinen, in der richtigen Länge die mit drei Seiten und dem wenigsten doppelten Weg, sonst die kürzeste über 400 m, sonst die längste unter 150 m. Kein Sonderfall für Köln, keine Kartenangabe. Viertel- und Mauerrunde unverändert (fehlen ihre Punkte, geht der Wachmann wie bisher über den Markt, jetzt die neue Runde). | 08.11.2026 |
| Wirkung | Nur Bild (Darstellung, kein Kern, kein Spielstand, kein Zufall). Köln, Wachhaus am Heumarkt: 5 m → 328 m im Spiel (Klicktest, `[streife]`): die Rheingasse hinab über den Heumarkt bis zum Kartäuserwall, an der Mauer nach Westen, die Hohe Straße hinauf, die Bayenstraße nach Osten und die Rheingasse hinab zur Tür. In einer Schicht schafft der Wachmann davon etwa die ersten 90–140 m (über den Heumarkt und an der Mauer entlang). Im Test (Netz des Kerns) 295–351 m je nach Dichte der Knoten, doppelt gegangen 0–14 %; von allen Werkstattplätzen der Stadt 216–327 m. In Deutz (Neumarkt, Straßen nur als Stern ohne Ring) bleibt es beim Hin und Zurück, so lang wie nötig. | 08.11.2026 |
| Tests | `StreifeMarktrundeTests`: Köln, Heumarkt mit Knoten nur an den Stützpunkten, alle 10 m und alle 4 m: 150–400 m, höchstens ein Viertel doppelt, an der Tür begonnen und beendet, jeder Punkt und alle 2 m dazwischen auf einem Weg oder dem Pflaster, mindestens drei Seiten, innerhalb der Mauer (das Tor am Ende der Rheingasse zählt mit); von jedem Werkstattplatz der Stadt mindestens 150 m auf Wegen; Wahl der Haltepunkte fest und unabhängig von der Reihenfolge. Gegenprobe: mit der alten Regel nachgebaut sind die beiden Köln-Tests rot (71 bzw. 83 m im Netz des Kerns; im Spiel waren es 5 m, weil dort der Hofanschluss vor der Tür ein Knoten ist). | 08.11.2026 |

### KI nach Stress (PR „Kern: Charaktersystem Teil 3b – Stress“, vom Kern mitgebaut)

| Thema | Entscheidung | Datum |
|---|---|---|
| Meiden | Ab der ersten Stufe (Stress des Oberhaupts ab 40) lässt `KiRivale.Befehlen` jeden Befehl, der dem Oberhaupt Stress brächte (`Familienbuch.StressDurch(konto, befehl)` > 0) – still, ohne Meldung (sonst käme jede Stunde dieselbe). So heuert ein ehrliches Oberhaupt keine Bande mehr an, ein scheues gibt kein Gastmahl, ein bescheidenes baut kein Stadtpalais, ein gieriges schenkt nicht über 200; Ventile (negativ) bleiben erlaubt. Eine Schleife mit Befehlen gibt es nur in der Lebensweise, und die bricht bei „nein“ ab. | 09.11.2026 |
| Ventil | In der Politikstunde nach allem anderen (`Ventil()` nach `Trainieren`), wenn das Oberhaupt mindestens `stress.kiVentilAb` (20) hat: unter allen Aktivitäten mit dem eigenen Ehepartner und den Ratsherren, die dem Oberhaupt Stress abbauen (nie eine, die es nicht mag), die mit dem meisten Abbau je AP für beide Beteiligten zusammen (`StressVorschau`: Hat der Ehepartner selbst Stress, zählt seiner mit), dann mehr Meinung, weniger Gulden; nur mit voller Reserve danach. Es nutzt nur Aktionspunkte, die nach Familie, Verteidigung, Rat, Gaunerei und Üben noch da sind. Erst gebaut: der Ehepartner nur, wenn er selbst 20 Stress hat, und dann ohne Rückfall auf die Ratsherren (Befund der Prüfung). | 09.11.2026 |
| Werbung | Muss die KI einen Ratsherrn umwerben (`BesteAktivitaet`) und sucht ihr Oberhaupt ein Ventil, nimmt sie zuerst eine Aktivität, die es abbaut („zählt doppelt“: Meinung und Ruhe); sonst wie bisher (was es mag, dann die beste Wirkung). | 09.11.2026 |
| Vorsicht und Pump | Ab „zermürbt“ Raubfurcht × `kiVorsicht` (1,2) über `Familienbuch.KiStressVorsicht` in `Vorsicht` (Eskorte, Schiffe). „Nicht mehr auf Pump“ ändert nichts: Die KI nimmt keine Kredite (Balancing-Bericht: 0 Kredite in 1.320 Partien). | 09.11.2026 |
| Tests | `KiStressTests` (3): angespannt meidet Bande (ehrlich) und Gastmahl (scheu), ein Ventil bleibt erlaubt; Gegenproben verschlagen und ruhig. Mit 30 Stress zecht ein geselliges Oberhaupt in der Politikstunde („sucht Ruhe vom Stress“, Eintrag „mag es (gesellig)“), mit 15 nicht. Zermürbt: `ErwarteteFahrtkosten` mit Vorsicht × 1,2, angespannt wie vorher. | 09.11.2026 |

### KI nach Geheimnissen (PR „Kern: Charaktersystem Teil 3c – Geheimnisse“, vom Kern mitgebaut)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gegen Häuser | `Geheimnispolitik()` in der Politikstunde nach `Anklagen()`, für jedes nutzbare Geheimnis einer Person eines anderen Hauses (nach Nummer): Ein Haus, über das das Oberhaupt gut denkt (`Verschont`, ab `kiVerschontAb`), bleibt unberührt. Mord immer anzeigen; Ehebruch und Ketzerei anzeigen bei Hass (`Verhasst`), Ketzerei auch mit frommem, beides mit jähzornigem Oberhaupt – nur mit Reserve nach Gebühr und Entschädigung. Sonst gierig oder verschlagen ein Erpresserbrief: `geheimnisse.ki.anteil` (10 %) vom Bargeld des Opfers, auf 50 gerundet, höchstens `hoechstens` (1.500). Ist ein Brief beantwortet und das Wissen noch da (`GeheimnisWissen.BriefIm`, das Opfer hat nicht gezahlt), macht sie die Drohung wahr: Anzeige, wenn es ein Vergehen ist, sonst Bloßstellen. Sonst Bloßstellen, wenn das Haus ihr bei der Wahl im Weg steht (bewirbt sich um dasselbe Amt oder hält es). Sonst schweigt sie. | 09.11.2026 |
| Ratsstimme | `RatsstimmenErpressen()` in `Politik()` zweimal: nach der Bewerbung (vor `FuerWahlWerben`) und beim eigenen Statut-Antrag (vor `FuerStatutWerben`). Für jeden Ratsherrn, dessen Stimme fehlt (`BrauchtWahlstimme`/`BrauchtStatutstimme`: sitzt im Rat, nicht schon erpresst, stimmt bei der eigenen Wahl nicht sicher für sie bzw. beim eigenen Statut nicht dafür), das erste nutzbare Geheimnis – nie mit ehrlichem Oberhaupt, und nur so viele, wie zur Mehrheit fehlen (`FehlendeWahlstimmen`: Mehrheit der Wähler; `FehlendeStatutstimmen`: Mehrheit der besetzten Sitze; erpresste und sichere zählen schon – Befund der Prüfung: jede Erpressung ist eine Spur und kostet Meinung). `WahlKosten` und `FuerStatutWerben` zählen eine eigene erpresste Stimme als sicher (keine Geschenke dafür). Verschlagen und gierig „bevorzugt“ ist damit erfüllt, dass alle außer Ehrlich es tun (einfachere Lösung). | 09.11.2026 |
| Spitzel auf Ratsherren | Fehlt danach noch eine Stimme ohne bekanntes Geheimnis, ein Spitzel auf diesen Ratsherrn (`SpitzelAnsetzen("R:Name")`) – aus dem Budget der Spitzel auf Häuser (höchstens jedes zweite Jahr einer, PR #208; der Ratsherr geht vor, weil `Politik` vor `Anklagen` läuft), nur mit Aktionspunkten für Spitzel und Erpressung und mit Reserve. Erst gebaut: höchstens einer im Jahr – die Probe (`messen geheimnisse 20 5`) zeigte 17 je Partie, weil die meisten Ratsherren kein Geheimnis haben; jetzt 6,8 je Partie (`messen geheimnisse 20 20`), und die Spitzel auf Häuser sinken von 94 auf 68 Gulden im Jahr (ein Betrieb, KI geführt). | 09.11.2026 |
| Messung | Gemessen vom Kern (WIRTSCHAFTSMODELL „Geheimnisse“, main `5a7f6c51` + 3c `09b61b2c`): je Partie (20 Jahre, 80 Partien) 3,4 erpresste Ratsstimmen (74 % der Partien), 6,8 Spitzel auf Ratsherren, 0,8 Briefe (46 %, 0,56 bezahlt), 0,49 Anzeigen (0,35 schuldig), 0,55 Bloßstellen; KI ÷ Bezug 1,14 → 1,05, Berufe 9/10 im Band wie ohne Geheimnisse (Tischler −15,3 %). Ketzerei-Regel (Entscheidung des Nutzers): `GeheimnisSchaden` und die Anzeige folgen `Geheimniswesen.KeinVergehen`; kaum Wirkung, weil in KI-Partien selten ein Stadtglaube gilt. | 09.11.2026 |
| Opfer | `Unterweltschutz` zahlt einen Brief mit Geheimnis, wenn der Betrag unter `GeheimnisSchaden(nummer)` liegt und die halbe Reserve bleibt: das Schlimmere aus Anzeige (Buße der Art, gedeckelt wie am Gerichtstag, + Pranger 500 + Kerker 1.500 + drei Jahre Amtseinkommen – nur bei einem Vergehen – `Geheimniswesen.KeinVergehen`, Ketzerei nur mit Stadtglauben und Bekenntnis des Inhabers zu ihm) und Bloßstellen (Ansehen × `guldenJeAnsehen` (100) × Jahre der Schande). Angezeigt, rechnet `Verteidigen` Buße und Kerker des Geheimnisses zum Nutzen eines Freispruchs (Befund der Prüfung; Mord mit 25 Beweiskraft ist ohnehin nicht zu kippen). | 09.11.2026 |
| Tests | `KiGeheimnisTests` (4): Mord angezeigt und verurteilt, verschont ein Haus mit Meinung +100 (Gegenprobe); gierig ein Brief (≤ 1.500), still schweigt (Gegenprobe), nach der Ablehnung wird es öffentlich; Ratsstimme beim eigenen Antrag (verschlagen ja, ehrlich nein) und Spitzel auf den nächsten Ratsherrn ohne Wissen; als Opfer 100 Gulden gezahlt, 9.000 nicht. | 09.11.2026 |

## Messungen

Neue Messungen der Spiel-KI stehen hier (mit main-Commit), nicht mehr in `docs/WIRTSCHAFTSMODELL.md`. Ältere: dort bis §42 (Taschendiebe, PR #69).

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

Kein Bankrott. Woher der Rückgang kam (Greybox, Normal, 4 Startwerte, Jahr 15, je KI-Werkstatt): vorher lagen 7 von 23 Werkstätten über 60 min vom Markt (gebaut „an der Ringgasse“ und „am Felsenhang“, 99–174 min, drei Handkarren, 44 % Stillstand, Gewinn Ø 435 statt 4.072); mit Karrenwahl allein 31 % Stillstand, Gewinn 621; mit Bauort nach Karrenweg nur noch 2 weite, alle Werkstätten zusammen 73.613 (vorher 68.197, main 73.025). Einzelne Versuche (8 Startwerte, Greybox): immer Pferdekarren im Umland KI Schwer +1,8 %, Ochse und Tausch +1,3 %, immer Handkarren −10 %. **Köln** (Teil 5, 20 Startwerte) lief bei Abgabe noch (Rechner mit Last um 30); nachtragen.

### Umland nach dem Karrenweg (PR #286)

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

### Karren-Höchstzahl: Umland bis 5 Karren (PR #286)

Gemessen auf main `ec36ff33` + dieser Branch (`adfd5f2d`), wie oben (`messen bericht 20 20 --teile=5`, 20 Startwerte; die Messdatei nennt als main `4220c631`, den Stand von origin/main zur Messzeit – der Branch ist darauf nicht neu aufgesetzt). „Vorher“ = derselbe Branch mit 3 Karren (`97001082`). Keine Partie mit Fehler, kein Bankrott.

| | main ec36ff33 | 3 Karren | 5 Karren im Umland |
|---|---|---|---|
| Köln Normal: Bezug / KI-Häuser | 56.503 / 65.793 | 54.240 / 66.805 | 55.547 / 66.339 |
| Köln Normal: davongezogen | 15/140 | 16/140 | 11/140 |
| Köln Schwer: Bezug / KI-Häuser | 77.280 / 95.778 | 79.314 / 100.162 | 78.584 / 99.893 |
| Köln Schwer: davongezogen | 18/140 | 25/140 | 26/140 |
| Köln Umland Normal: Gewinn je Jahr, Stillstand | 1.322, 16 % | 1.405, 13,6 % | 1.427, 11,7 % |
| Holzschlag Hardevust / Kleingedank, Stillstand Normal | 28,1 / 23,8 % | 28,8 / 23,3 % | 20,4 / 19,9 % |
| dieselben, Stillstand Schwer | 26,8 / 22,2 % | 25,8 / 21,5 % | 16,6 / 16,9 % |
| Holzschlag Hardevust / Kleingedank, Gewinn je Jahr Normal | 705 / 839 | 799 / 871 | 980 / 938 |
| Greybox Normal: Bezug / KI-Häuser, davongezogen | 62.268 / 74.507, 3/60 | 61.749 / 75.344, 4/60 | 61.592 / 75.071, 6/60 |
| Greybox Schwer: KI-Häuser, davongezogen | 116.029, 24/60 | 117.546, 25/60 | 117.502, 25/60 |

Die Holzschläge stehen mit dem vierten (und fünften) Karren gut 8 Punkte weniger still und verdienen mehr; die KI kauft die Karren nur nach Stillstand. Das Vermögen der KI-Häuser ändert sich im Rahmen des Fehlers (± 1.700), „Schwer“ zieht die KI nicht öfter davon als mit 3 Karren. Ein erster Lauf mit der Schätzung beim Pachten für 5 Karren pachtete den Holzschlag im Eichenforst wieder (204 statt 12 Betriebsjahre, 43 % Stillstand) und ließ in „Schwer“ 34 von 140 KI-Häusern davonziehen; deshalb schätzt die KI das Pachten weiter mit 3 Karren (Befund H1 der Prüfung). Die Nachtfahrt ist damit nicht nötig.

### Ware der Schänke nach dem Ausschank – Kurzprobe (PR #292)

Gemessen auf main `6f80481b` + Branch phase5/jahreszeiten `e666edf6`, nur Köln, nur Wirt, **4 Startwerte** (Richtungsprobe, Fehler des Mittels weit über den Unterschieden): `messen jahreszeiten 20 4 --karten=koeln --beruf=Wirt --parallel=4` mit/ohne `--ohne-jahreszeiten` und `--ki-schaenke[=0.75]`. Reinvermögen J20 des Spielers, Gewinn der Betriebe (ohne Schiffe) über 20 Jahre, hergestellt je Jahr (Körnermahlzeit / Dünnbier / Starkbier):

| | ohne Jahreszeiten | mit Jahreszeiten | Δ |
|---|---|---|---|
| vorher (ohne `ki.schaenke`) | 39.694, Betriebe 53.577, 35 / 14 / 16 | 25.677, Betriebe 48.551, 54 / 4 / 8 | −35,3 % |
| `--ki-schaenke` (Anteil 1) | 38.476, Betriebe 49.884, 67 / 0 / 5 | 27.608, Betriebe 47.700, 70 / 0 / 2 | −28,2 % |
| `--ki-schaenke=0.75` | – | 30.676, Betriebe 49.962, 57 / 0 / 10 | – |

Mit Anteil 1 sieht die KI fast jede Ausschankware zum Ausschankpreis; je Arbeitsminute ist dann die Körnermahlzeit am besten, und die Schänke macht fast nur noch sie – der Gewinn der Betriebe sinkt (−2 / −7 %), weil der Überschuss doch zum Markt geht (bewirtet 55 von ≈ 70 Stück im Jahr, 16 Gäste je Jahr abgewiesen, weil der Karren alles über dem Vorrat von 15 mitnimmt). Mit 0,75 bleiben Starkbier und Marktanteil, die Betriebe verdienen mehr als vorher (+3 %). Der Abstand im Reinvermögen hängt stark an den Schiffen (je Lauf −0,8 bis +7,4 Tsd.) – erst die volle Messung entscheidet.

Befehle für den Grafik-PC (80 Startwerte, beide Karten, alle Aufbauten; jeweils auf demselben Stand):

```bash
P="dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen jahreszeiten 20 80 --karten=greybox,koeln --parallel=12"
$P --ohne-jahreszeiten                        --datei=schaenke-ohne.json        # Bezug ohne ki.schaenke (= Endmessung)
$P                                            --datei=schaenke-mit.json
$P --ohne-jahreszeiten --ki-schaenke          --datei=schaenke1-ohne.json
$P                     --ki-schaenke          --datei=schaenke1-mit.json
$P --ohne-jahreszeiten --ki-schaenke=0.75     --datei=schaenke75-ohne.json
$P                     --ki-schaenke=0.75     --datei=schaenke75-mit.json
```

### Bürgerdichte und Tagesplan (PR #323)

Gemessen auf main `3518c72f` + dieser PR: `messen buerger 20 8` (Köln und Greybox, alle KI-Häuser, Spieler KI-geführt: Schmied und Wirt mit einem Betrieb, Ausbau = Schmied wie ein KI-Haus; „alt“ = Abschnitt `buerger` ohne `jeHausplatz` und `tagesplan`). Reinvermögen Jahr 20 (± Fehler des Mittels), Jahr bis 10.000 Gulden:

| Karte | Aufbau | alt | neu | Bürger Ø Jahr alt / neu |
|---|---|---|---|---|
| Greybox | Schmied | 36.804 (± 3.008) | 36.804 (± 3.008) | 9,1 / 9,1 |
| Greybox | Wirt | 35.668 (± 2.574) | 35.668 (± 2.574) | 8,6 / 8,6 |
| Greybox | Ausbau | 63.712 (± 3.496) | 63.712 (± 3.496) | 12,3 / 12,3 |
| Köln | Schmied | 32.691 (± 2.962) | 32.691 (± 2.962) | 9,0 / 9,0 |
| Köln | Wirt | 28.110 (± 2.298) | 28.110 (± 2.298) | 9,0 / 9,0 |
| Köln | Ausbau | 63.234 (± 9.569) | 63.234 (± 9.569) | 10,6 / 10,6 |

Wert für Wert gleich, auch KI-Häuser, Marktfaktor und Warenkorb. Zur Einordnung die verworfenen Fassungen (Köln, Schmied / Wirt): alle zählen 38.248 / 32.571, dazu anteiliges Gewicht im Warenkorb 37.648 / 32.127, feste Auswahl ohne eigenen Generator 36.676 / 29.631; Gegenprobe `jeHaus` 50 nach alter Regel 39.163 / 30.929, `jeHaus` 26 32.462 / 29.907 (Rauschen eines anderen Laufs der Bürger: je Startwert bis ± 12.000).

Einwohner und wer zur halben Stunde in der Stadt zu sehen ist (Markt, Gasse, Tor, unterwegs in der Stadt; Mittel über 8 Startwerte × 3 Aufbauten × 20 Tage):

| Karte | | Einwohner | 8 Uhr | 10 Uhr | 12 Uhr | 15 Uhr | 18 Uhr | 20 Uhr | 22 Uhr | davon am Markt 10 / 15 / 18 Uhr |
|---|---|---|---|---|---|---|---|---|---|---|
| Köln | vorher | 223 | 206 | 122 | 127 | 123 | 151 | 123 | 94 | 39 / 35 / 35 |
| Köln | nachher | 418 | 387 | 268 | 240 | 264 | 300 | 240 | 177 | 153 / 119 / 97 |
| Greybox | vorher | 123 | 114 | 65 | 65 | 66 | 77 | 58 | 27 | 20 / 17 / 21 |
| Greybox | nachher | 167 | 155 | 105 | 97 | 105 | 115 | 92 | 36 | 59 / 49 / 48 |

Rechenzeit Kern (Köln, ein Lauf über 6 Jahre, eingeschwungen): 2,7 s gegen 2,8 s, also rund 0,9 ms mehr je voller Stunde (die Planung), im Mittel 0,015 ms je Takt.

Bilder (Vulkan auf der Grafikkarte, Pixel-Look, Köln, Alter Markt am Dom, Zoom 35 und 70, Tempo 2, vier Spielstunden Vorlauf; Ordner im Scratchpad des Auftrags, `buerger/`): Figuren im Bild bei Zoom 35 etwa 9 Uhr 11 → 10 (die Darstellung holt nach dem Start noch Wege nach), 12 Uhr 22 → 45, 18 Uhr 8 → 30, 19 Uhr 5 → 35, 22 Uhr 0 → 15 (Nachzügler mit Laterne, siehe darstellung.md). Der Heumarkt liegt am Südrand: Wer dort wohnt, geht zum nächsten Markt, die meisten zum Alten Markt.

Darstellung (Köln, 12 Uhr, Zoom 70, Tempo 1, NAS mit RTX 2000): Passanten 5,4 → 10,8 ms je Bild, mit der Sparstufe ab 300 Einwohnern 9,6 ms; Bildzeit 46 → 76 ms, mit Sparstufe 64 ms. **Das Ziel „höchstens +2 ms“ ist in der Darstellung nicht erreicht** – Hebel siehe `docs/bereiche/darstellung.md`, „Offen“; wer es leichter will, senkt `buerger.jeHausplatz` (wirkt nicht auf die Wirtschaft).

### KI nach Stress (PR „Kern: Charaktersystem Teil 3b – Stress“, vom Kern gemessen)

Gemessen auf main `432ceee2` + `a3ed79df` (Einzelheiten in `kern.md` und WIRTSCHAFTSMODELL „Stress“): KI ÷ Bezug (Median) 1,15 → 1,13, „Schwer“ 1,83 → 1,80, Gaunerei der KI 0,45 / 0,80 / 0,17 → 0,44 / 0,79 / 0,17 je Jahr, Karren in Not 0 → 1. `messen stress 20 30`: Die Oberhäupter sind in 99,8 % der Jahre ruhig, keines bricht zusammen; Ventile etwa 7 je 100 Oberhaupt-Jahre (Abbau zusammen etwa 110), keine Aktivität, die das Oberhaupt nicht mag. Gemieden wird darum selten – die KI kommt kaum über 40. Nachtrag mit den Quellen von außen (main `047cabd9` + `1961140a`, `messen stress 40 20`): KI ÷ Bezug 1,14, „Schwer“ 1,85, Gaunerei 0,44 / 0,77 / 0,17; in 16,3 % der Oberhaupt-Jahre wird es angespannt oder mehr (vorher 2,6 %), je Leben gut 4 Jahre angespannt, 23 % der Oberhäupter einmal zermürbt, 2,8 % brechen zusammen. Die Ventile kommen damit öfter zum Zug (Abbau zusammen etwa 450 je 100 Oberhaupt-Jahre); das Meiden ab 40 bleibt wie gebaut.
