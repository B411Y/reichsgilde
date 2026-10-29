# Bereich spiel-ki – Archiv der älteren Entscheidungen

Hier stehen die Entscheidungsabschnitte, die vor dem 25.10.2026 abgeschlossen wurden, **wörtlich** aus [../spiel-ki.md](../spiel-ki.md) verschoben (Stand 29.10.2026). Offene Punkte und die aktuellen Entscheidungen stehen in der Bereichsdatei selbst. Neue Einträge gehören dorthin, nicht hierher.

## Entscheidungen (älter)

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
