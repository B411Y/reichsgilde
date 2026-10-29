# Bereich Spiel-KI – Offen, Entscheidungen, Messungen

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

### KI-Häuser im Rat (PR #73)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wahl nach Charakter | **Vorsichtig** bleibt beim Amtsinhaber, außer der hat dem Haus geschadet. **Ausgewogen** wählt den Ungefährlichsten: Ratsherr vor schwachem Haus vor starkem Haus (Reinvermögen). **Wagemutig** stimmt nie für das führende Haus, sonst wie ausgewogen. Ersetzt „Häuser im Rat wählen den bisherigen Inhaber“ (Archiv §3a). | 16.10.2026 |
| Geschadet | Ein Haus hat der KI geschadet, wenn sie einen **Beweis** für eine Tat hält, die einen ihrer Betriebe oder ihr Haus betraf. Es gilt dann immer als gefährlichster Kandidat. Die KI sieht nur, was auch der Spieler sieht. | 16.10.2026 |
| Prognose | Die **Wahlprognose** zählt die Stimmen der KI-Häuser nach derselben Regel mit. | 16.10.2026 |
| Spielerhaus | Das Haus des Spielers stimmt **vorerst weiter für den Amtsinhaber**, bis es Abstimmen von Hand gibt (offener Punkt bei Kern und Oberfläche). | 16.10.2026 |

### Wohnhaus und Sparen (PR #82)

| Thema | Entscheidung | Datum |
|---|---|---|
| Zeitpunkt | Fehlt für den nächsten Stand ein größeres Wohnhaus, baut die KI es erst, wenn sie **danach noch über der Schwelle** liegt (Reinvermögen − Ausbau ≥ Schwelle) und Ausbau, Gebühr und Reserve **bar** bezahlen kann. Der Ausbau senkt das Reinvermögen; früher gebaut, fiel sie unter die Schwelle und wartete Jahre. | 16.10.2026 |
| Sparen | Ab **80 % des Standziels** (Schwelle + nötiger Ausbau) spart die KI nach Charakter: **vorsichtig** keine Expansion und kein Werkstattausbau, **ausgewogen** keine Geschenke an Ratsherren, **wagemutig** halb so viele **bezahlte** Gaunerstücke (Banden). Die eigenen Diebe kosten nichts und bleiben beim vollen Budget. | 16.10.2026 |
| Reinvermögen | Dass das Haus nicht zum Reinvermögen zählt, trifft auch den Spieler – als offener Punkt an den Kern. | 16.10.2026 |
### Wegzeiten der Einwohner (PR #80)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wegzeiten | **Stadt 30 ± 10, Tor 40, Umland 50 Minuten, Abmarsch 0–9 Minuten** nach der vollen Stunde (vorher 10 ± 5 / 15 / 30, Abmarsch 0–10). Passt zum Gehtempo der Passanten (~2,5 m/s) und den Weglängen im Gassennetz (Wunsch der Darstellung, PR #74). Spätestens 59 Minuten nach der Stunde ist jeder da. | 16.10.2026 |

### KI-Häuser handeln nicht von Hand (PR #86)

| Thema | Entscheidung | Datum |
|---|---|---|
| Handel von Hand | Die KI-Häuser **halten keine Ware zurück und kaufen nicht von Hand ein**; Verkauf und Zutaten bleiben beim Verwalter. Zurückhalten bei niedrigem Preis wurde gebaut und gemessen: Es kostet die KI Geld (siehe Messung). Den Preis drücken meist die KI-Häuser selbst, zurückgehaltene Ware drückt ihn beim Freigeben ein zweites Mal, und gegen einen fremden Preissturz wechselt die KI schon heute stündlich das Produkt. Einkauf von Hand kostet +10 % und hebt den Preis. | 16.10.2026 |
| Wiedervorlage | Neu prüfen, wenn sich das Preismodell ändert (Elastizität über 1, längere Erholung, mehrere Märkte) oder ein Preiskampf gegen den Spieler gewünscht ist. | 16.10.2026 |

### Warenkorb je Volk und Rolle (PR #91)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gruppen | Je **Handwerk** eine Warengruppe (Beruf der herstellenden Betriebsart): Schmiedewaren, Holzwaren, Steinwaren, Arzneien (mit Kräuterschnaps), Duftwaren, Speis und Trank, Kirchenbedarf. Es zählen die Waren aus `kopplung.marktTypen`. | 16.10.2026 |
| Wirkung | **Volk → Gruppe** (Zwerge Schmiede- und Steinwaren ×1,5; Elben Duft und Arznei ×1,5; Halblinge Speis und Trank ×1,5, Holzwaren ×1,3; Menschen Kirchenbedarf ×1,2). **Rolle → Preisklasse** (günstig unter 150, teuer ab 250 Gulden Grundpreis): Arme (Tagelöhner, Knecht, Hirte, Feldarbeiter, Holzsammler, Bettler) günstig ×1,15, teuer ×0,85; Ratsherren und Bürgersöhne günstig ×0,85, teuer ×1,3; Mönche Kirchenbedarf ×2. Halblinge-Holz und die schwächeren Preisklassen (statt ×1,5/×0,5) nach der ersten Messung, damit das Volk sichtbar bleibt. | 16.10.2026 |
| Rechnung | Gezählt werden die Einkaufenden der letzten 17 Stunden (Ring wie beim Marktfaktor). Mittlere Neigung je Ware ÷ Maßstab, begrenzt auf **1 ± 10 %**; der Maßstab wird so gesucht, dass die Nachfrage der Stadt in Gulden (Nachfrage × Grundpreis) gleich bleibt. Faktor auf die Nachfrage = Marktfaktor × Warenkorb. | 16.10.2026 |
| Kalibrierung | **Feste Neigung**, nicht gegen ein gelerntes Übliches: Die Standardstadt bekommt eine dauerhafte Färbung. Der Kern kalibriert nach (offener Punkt dort). | 16.10.2026 |
| Anzeige | Öffentliche Abfragen `Warenkorbfaktor(ware)` und `Warengruppe(ware)`; Anzeige im Marktfenster als offener Punkt bei der Oberfläche. | 16.10.2026 |

### KI kauft Leerstand, baut neu und verkauft Betriebe (PR #108)

| Thema | Entscheidung | Datum |
|---|---|---|
| Leerstand kaufen | Nur bei Bedarf: wenn das Haus für den nächsten Stand ein größeres Wohnhaus braucht und der Leerstand günstiger ist als Ausbau oder Neubau. Kein Kauf als reines Schnäppchen. Entschieden vom Nutzer. | 17.10.2026 |
| Vorrang des Spielers | Die KI kauft einen Leerstand erst, wenn er ein ganzes Spieljahr leer stand. Entschieden vom Nutzer. | 17.10.2026 |
| Leerstand abreißen | Die KI reißt keinen Leerstand ab. | 17.10.2026 |
| Werkstatt neu bauen | Gleichwertig zum Kauf einer freien Werkstatt, gewählt wird nach der Rechnung (das Baujahr ohne Einnahmen zählt mit). Kein Platz wird für den Spieler freigehalten. Entschieden vom Nutzer. | 17.10.2026 |
| Wohnhaus neu bauen | Stadtpalais und Stadthof gibt es nur als Neubau (Kern) – die KI baut sie neu, wenn kein passender Leerstand günstiger ist. | 17.10.2026 |
| Betriebe verkaufen | Die KI verkauft einen Betrieb, der zwei Jahre in Folge Verlust macht, und verkauft vor dem Bankrott, wenn das Bargeld nicht mehr reicht. Entschieden vom Nutzer. | 17.10.2026 |
| Wahl des Wohnhauses | In `WohnhausBeschaffen`: ein Leerstand, der ein Jahr leer steht (`Bauen.LeerSeit`), groß genug ist, den der Stand erlaubt und der weniger kostet als der Ausbau (bis Steinhaus) bzw. der Neubau (Stadtpalais, Stadthof) – sonst der Ausbau, sonst der Neubau. Leerstand und Ausbau wie bisher erst über der Schwelle mit Kosten, Gebühr und Reserve in bar. | 17.10.2026 |
| Neubau des Wohnhauses | Weil er zwei Jahre dauert, beginnt die KI schon ab **80 % der Schwelle** (wie beim Sparen) und braucht dafür nur Baukosten und Reserve bar; der Hauswert zählt sofort zum Reinvermögen. Steht ein billigerer passender Leerstand, der noch kein Jahr leer oder noch nicht bezahlbar ist, **wartet** sie auf ihn. Platz: der erste aus `Bauen.Bauorte` (frei gewordene Bauplätze zuerst). Beim Ausbau-Niveau wartet sie nicht auf einen jungen Leerstand – der Ausbau dauert nur 4 Stunden. | 17.10.2026 |
| Neubau der Werkstatt | In `Expandieren` stehen neben den freien Liegenschaften alle Werkstätten der Stufe 1, die der Stand erlaubt (`Neubauten`). Kosten = Preis + Lehrgeld + **erwarteter Gewinn × Bauzeit** (das Baujahr ohne Einnahmen); dieselbe Amortisation und Reihenfolge wie beim Kauf. Bei gleichem Preis gewinnt so der Kauf. Platz: die gültige Stelle, die dem Marktplatz (Ursprung der Karte) am nächsten liegt. Findet sie keinen, versucht sie es im nächsten Jahr wieder. Eine Werkstatt im Bau zählt zu den Betrieben (`Baustellen`), auch für „Werkstatt zum Handwerk“. | 17.10.2026 |
| Rechenzeit | `Bauen.Bauorte` (≈ 25 ms) fragt die KI nur zu ihrer **Baustunde** (3., 6., 9. … Stunde des Tages, je Haus eine andere), nur wenn sie wirklich bauen will, und höchstens einmal je Stunde. Gewinnt ein Neubau, wartet sie bis zur Baustunde, statt etwas Schlechteres zu kaufen. | 17.10.2026 |
| Verlustjahr | Ein ganzes Jahr (`Betriebsrechnung.Ganzjaehrig`), in dem **Gewinn + Änderung des Lagerwerts** (Lager zum Grundpreis, zum Jahresende gemerkt) negativ ist. Ohne das Lager verkaufte die erste Messung die ausgebauten Hauptwerkstätten, die teure Zutaten auf Vorrat kaufen (19 Verkäufe statt 3). Ein angebrochenes Jahr zählt nicht und unterbricht nicht; ein Jahr ohne Verlust setzt zurück. Wache, Geldleiher und Dieb verkauft die KI nicht wegen Verlust – Kopfgeld, Kreditzinsen und nächtliche Beute stehen nicht (ganz) in ihrer Rechnung. | 17.10.2026 |
| Notverkauf | Hat der Kämmerer `jahreBisBankrott − 1`-mal gemahnt und ist ab der Jahresmitte die Hochrechnung des Reinvermögens zum Jahresabschluss negativ (heute + bisheriger Betriebsertrag hochgerechnet − Löhne, Unterhalt, Haushalt, die erst am Jahresende fällig werden), verkauft die KI je Stunde den Betrieb mit dem schlechtesten Gewinn, der etwas einbringt. In Not (gemahnt und Reinvermögen minus offene Kosten negativ) kauft und baut sie nichts. Verkäufe macht auch der Verwalter aus dem Kerker. | 17.10.2026 |
| Keine Schleife | Nach einem Verkauf meidet die KI **Handwerk und Ort** (z. B. Schmied/Werkstatt) **5 Jahre** (`SperreNachVerkauf`): kein Kauf, kein Neubau, auch nicht über „Werkstatt zum Handwerk“. Gemerkt im `RivaleStand` (`Verlustjahre`, `Lagerwert`, `Gesperrt`, `KeinWerkstattplatzJahr`); alte Spielstände laden ohne sie. | 17.10.2026 |

### Gaunerstücke gegen Wohnhäuser (PR #119)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wer | **Einbruch** ins Wohnhaus nutzt jedes KI-Haus, das Gaunerstücke nutzt (ausgewogen selten und nur gegen Häuser vor ihm, wie Diebstahl und Sabotage). **Brandstiftung** nur die Wagemutige, wie Überfall und Erpressung. Entschieden vom Nutzer. | 18.10.2026 |
| Wert | Einbruch = `Unterwelt.ErwarteterHausgewinn` (Bargeld × Chance − Kosten, kein Hehler). Brandstiftung wie Sabotage: **halbe Reparatur** beim Gegner × Chance − Kosten (`Brandschaden`, `ErfolgschanceWohnhaus`); das fehlende Ansehen zählt nicht mit. Entschieden vom Nutzer. Beide stehen in derselben Liste wie die Stücke gegen Betriebe, das Beste gewinnt. | 18.10.2026 |
| Wann es sich lohnt | Einbruch erst ab dem Bürgerhaus (Fachwerkhaus: höchstens 300 Beute × 0,6 < 200 Kosten), Brand erst ab dem Stadtpalais (Steinhaus: 800 × 0,5 × 0,5 = 200 < 300). Ein Haus, das vom letzten Brand noch beschädigt ist, steht nicht zur Wahl (der Kern lehnt den Auftrag ab). | 18.10.2026 |

### Verlustjahre, Notverkauf und Sparziel (PR #123)

| Thema | Entscheidung | Datum |
|---|---|---|
| Jahre ohne Personal | Stand ein Betrieb mindestens **ein Viertel der Stunden** des Jahres ganz ohne Personal (meist fehlte das Geld für Löhne), zählt sein Jahr **nicht als Verlustjahr** und unterbricht die Folge nicht – wie ein angebrochenes Jahr. Kurze Lücken (eine Stunde nach dem Kauf, ein Statutwechsel) ändern nichts. Gezählt in `KiRivale.Stunde`, gemerkt im `RivaleStand` (`StundenOhnePersonal`). Entschieden vom Nutzer. | 18.10.2026 |
| Notverkauf | Vor dem Bankrott verkauft die KI unter den Betrieben, deren Verkauf das Minus zum Jahresabschluss **allein deckt**, den mit dem schlechtesten Gewinn. Reicht keiner allein, wie bisher den schlechtesten, und in der nächsten Stunde den nächsten. Was ein Verkauf bringt (`Verkaufswirkung`): Erlös + Löhne und Unterhalt, die zum Jahresende entfallen − was der Betrieb im Rest des Jahres noch eingebracht hätte. Entschieden vom Nutzer. | 18.10.2026 |
| Sparziel | Geprüft: `Standziel` = Schwelle passt. Der Hauswert zählt seit Kern PR #85 ganz zum Reinvermögen (Ausbau, Neubau und Leerstand tauschen Bargeld gegen Hauswert), nur die Gebühr senkt es. Ab 80 % der Schwelle spart die KI, und Ausbau, Gebühr und Reserve müssen bar da sein – die Messung zeigt kein Haus, das jahrelang spart, ohne aufzusteigen. Ersetzt „80 % des Standziels (Schwelle + nötiger Ausbau)“ aus PR #82. | 18.10.2026 |

### Warenkorb ohne Abneigung (PR #127)

| Thema | Entscheidung | Datum |
|---|---|---|
| Rollen | **Keine Abneigung:** Arme (Tagelöhner, Knecht, Hirte, Feldarbeiter, Holzsammler, Bettler) kaufen günstige Waren ×1,15, teure aber ×1,0 statt ×0,85; Ratsherren und Bürgersöhne teure ×1,3, günstige ×1,0 statt ×0,85. Die meisten Einkaufenden sind arm, und die ausgebauten Werkstätten stellen vor allem teure Waren her – die Abneigung kostete die Häuser 3–6 % Vermögen nach 30 Jahren, jetzt etwa die Hälfte. Völker, Mönche und Spanne (±10 %) bleiben. Entschieden vom Nutzer. Damit ist der offene Punkt des Kerns („Kalibrierung prüfen“) erledigt. | 18.10.2026 |
| Verworfen | Spanne halbieren (±5 %): in der Messung nicht besser (im Rauschen), und die Völker sind kaum noch zu sehen. Die Nachfrage nach dem Umsatz der Häuser statt nach den Daten gewichten: Eichwald −11 %, und Speichern/Laden mitten in der Stunde liefe nicht mehr gleich weiter. | 18.10.2026 |

### KI kauft Einrichtung (PR #177)

| Thema | Entscheidung | Datum |
|---|---|---|
| Regel | Die KI-Häuser kaufen Einrichtung, wenn sich ein Stück in **höchstens 4 Jahren** bezahlt macht (Preis ÷ `Werkstattwesen.ErwarteterMehrgewinnJeJahr`, zu heutigen Preisen mit den Arbeitern und Waren von jetzt). Kredite nimmt die KI weiter nicht. Entschieden vom Nutzer (Balancing 1). | 22.10.2026 |
| Zeitpunkt | **Zum Jahresende** (`KiRivale.Jahresende`, nach Löhnen und Abschluss): Das Vorjahr mit Raub und Brandschaden ist dann abgeschlossen, das Bargeld am niedrigsten. Je Betrieb **höchstens ein Stück**, das mit der kürzesten Amortisation (bei Gleichstand das billigere); die Betriebe mit der kürzesten Amortisation zuerst. So braucht es kein neues Feld im Spielstand. | 22.10.2026 |
| Reserve | Nach dem Kauf bleibt **2 × Reserve** des Profils als Bargeld (vorsichtig 5.000, ausgewogen 3.000, wagemutig 1.600). Reicht es für ein Stück nicht, darf ein billigeres eines anderen Betriebs noch. Nicht in Not (`InNot`) und nicht, solange die Vorsichtige auf den Stand spart (wie Ausbau und Expansion). | 22.10.2026 |
| Charakter | Grenze 4 Jahre ± 0,5: **vorsichtig 3,5, ausgewogen 4, wagemutig 4,5** (erkannt an `MaxAmortisationJahre` des Profils gegenüber dem Standard). | 22.10.2026 |
| Schutzstücke | Eisentür und Löschfass nur, wenn ihr Mehrgewinn (Schaden des Vorjahres × (1 − Faktor)) das hergibt. Auch ein Betrieb mit Verlust bekommt Einrichtung – oft fehlt ihm gerade die Eisentür, und beim Verkauf zählt sie zum Wert. | 22.10.2026 |
| Werte | Eigener Abschnitt `ki.einrichtung` in `wirtschaft.json` (`hoechstensJahre` 4, `charakterJahre` 0,5, `reserveFaktor` 2), gelesen als `KiDaten` (`Ki/KiDaten.cs`). Fehlt der Abschnitt, kauft die KI keine Einrichtung. Andockstellen: Feld `Ki` und `Ki?.Pruefen` in `Wirtschaftsdaten`, der Abschnitt in `wirtschaft.json` (ohne neue Datenversion), eine Zählzeile in `Messung.cs`. | 22.10.2026 |

### KI baut Wege (PR #182)

| Thema | Entscheidung | Datum |
|---|---|---|
| Regel | Die KI-Häuser lassen einen Weg mit Kies oder Pflaster ausbauen, wenn sich die gesparten Karrenminuten in **höchstens 5 Jahren** bezahlt machen (`Wegebau.ErsparteMinutenJeJahr` mit den Fahrten des Vorjahres × `wertJeKarrenminute` 1,5 Gulden). Entschieden vom Nutzer (Kartenwerkstatt, Schritt 5d), Werte vom Kern. | 24.10.2026 |
| Ablauf | Zum Jahresende nach der Einrichtung, höchstens ein Abschnitt: zusammenhängende Stücke der eigenen Strecken zum Markt unter Kies bzw. Pflaster, je höchstens 100 m, der mit der kürzesten Amortisation. Danach bleibt 2 × Reserve; nicht in Not, nicht solange die Vorsichtige spart. Charakter ± 1 Jahr wie bei der Einrichtung. | 24.10.2026 |
| Rat | Zum Antrag „Weg pflastern“ stimmt die KI dafür, wenn eigene Karren über den Abschnitt fahren oder dort viel Verkehr ist (die Stadt zahlt). | 24.10.2026 |
| Andockstellen | `ki.wegebau` in `wirtschaft.json` (`KiWegebauDaten`), `KiRivale.WegAusbauen` und `StimmtFuerWegantrag`; gebaut vom Kern-Mitglied im Funktions-PR. | 24.10.2026 |

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
| Häuser untereinander | Meinung des eigenen Oberhaupts über das andere (`MeinungUeber`): ab +25 (`meinungen.wirkung.kiVerschontAb`) verschont – keine Gaunerstücke, keine eigenen Diebe, kein Spitzel, Klage erst mit einem Beweis mehr; bis −25 (`kiVerhasstAb`) verhasst – erstes Ziel der Gaunerstücke (der Verhassteste zuerst, auch wenn er hinten liegt), Spitzel auch auf ein Haus hinter ihr, Klage schon mit einem Beweis weniger (nie unter der Schuldschwelle). Bei Wahlen: Gefahr = Reinvermögen × (1 − Meinung × 1 %), mindestens ein Zehntel – ein geliebtes Haus wirkt weniger gefährlich. | 28.10.2026 |
| Woher die Meinung kommt | Feste Gründe (Züge, Volk, Glaube, Stand) und Taten: Klage −15, ertappter Diebstahl/Sabotage/Überfall −20, Stimme im Rat ±5, Fest +5. Ein Haus, das die KI bestiehlt und dabei erkannt wird, macht sie sich so zum Feind – vorher galt nur „hat geschadet“ (Beweise). | 28.10.2026 |
| Brautschau | Die KI rechnet mit `GuldenJeZuneigung(konto, kandidat)` – ein Kandidat, der ihr Oberhaupt mag, ist billiger. | 28.10.2026 |
| Messung | `messen bericht 20 30` auf main `391874f5` + `13531d4d`: Geschenke der KI je Jahr (Messspieler, ein Betrieb) −25 … −92 → −14 … −55, KI ÷ Bezug (Median) 1,01 → 1,11, „Schwer“ 1,79 → 1,78, Diebstahl 0,80 → 0,81, Einbruch 0,69 → 0,66, Sabotage 0,01 → 0,02 je Jahr, Karren in Not 7 → 8; verheiratet nach 3 Jahren 88/90 wie main. In 3 Startwerten × 12 Jahren hinterlassen KI-Häuser 88 Gründe aus Aktivitäten bei Ratsherren (Test `KiMeinungTests`). Einzelheiten in WIRTSCHAFTSMODELL, „Meinungen und Aktivitäten (PR #231)“. | 28.10.2026 |

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

### KI nach Meinungen (PR #231)

Gemessen auf main `391874f5` (bis `b9825f32` nur Infra) + `13531d4d`, `messen bericht 20 30` gegen main (Werte von main gleich `balancing-2026-10-28-charakterzuege.json`) → `docs/messungen/balancing-2026-10-28-meinungen.json`: KI ÷ Bezugsspieler mit der Strategie der KI (Median) 1,01 → 1,11, davongezogen 3 → 3 von 90, abgeschlagen 1 → 1; „Schwer“ 1,79 → 1,78; Geschenke der KI-geführten Messspieler je Jahr −25 … −92 → −14 … −55 (Aktivitäten statt Geschenke, Abnahme je weiterem Geschenk), Familienkosten +6 … +29 (Aktivitäten); Diebstahl 0,80 → 0,81, Einbruch 0,69 → 0,66, Sabotage 0,01 → 0,02 je Jahr; Spitzel 91 → 92, Gericht 231 → 216 je Jahr; Karren in Not 7 → 8. Sonde (30 Partien): KI-Häuser verheiratet nach 3 Jahren 88/90 wie main, nach 6 Jahren 90/90 (main 89/90).
