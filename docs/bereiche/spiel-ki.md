# Bereich Spiel-KI – Offen, Entscheidungen, Messungen

**Nur der Spiel-KI-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (von Kern) Die KI-Häuser nutzen die Werkstatt wie in Gilde 1 noch nicht (PR #152): Sie zahlen üblichen Lohn ohne Prämie, kaufen keine Einrichtung (z. B. Lederschürzen 80 Gulden gegen Unfälle, Werkzeug) und teilen keine Arbeiter einzeln zu, befördern keine Lehrlinge. Abfragen: `Stadtwirtschaft.Werkstaetten` (`Einrichtungsliste`, `EinrichtungVerboten`, `UnfallGefahr`), Befehle `EinrichtungKaufen`, `LohnstufeSetzen`, `ArbeiterBefoerdern`.
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
