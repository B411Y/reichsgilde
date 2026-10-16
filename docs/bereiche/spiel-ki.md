# Bereich Spiel-KI – Offen, Entscheidungen, Messungen

**Nur der Spiel-KI-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (2) Bürger-KI: Warenkorb je Volk und Rolle (eigener Nachfragefaktor je Warengruppe)
- (5) (von Kern) Gaunerstücke gegen das Wohnhaus (`Gaunerstueck.Hauseinbruch`, `Brandstiftung`, ab PR #77) in die Wahl der KI-Häuser aufnehmen – Rechnungen: `Unterwelt.ErwarteterHausgewinn`, `Brandschaden`, `ErfolgschanceWohnhaus`. Den Hauswächter nach einem Anschlag stellt `KiRivale` schon ein.

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

### KI-Häuser handeln nicht von Hand (PR #NR)

| Thema | Entscheidung | Datum |
|---|---|---|
| Handel von Hand | Die KI-Häuser **halten keine Ware zurück und kaufen nicht von Hand ein**; Verkauf und Zutaten bleiben beim Verwalter. Zurückhalten bei niedrigem Preis wurde gebaut und gemessen: Es kostet die KI Geld (siehe Messung). Den Preis drücken meist die KI-Häuser selbst, zurückgehaltene Ware drückt ihn beim Freigeben ein zweites Mal, und gegen einen fremden Preissturz wechselt die KI schon heute stündlich das Produkt. Einkauf von Hand kostet +10 % und hebt den Preis. | 16.10.2026 |
| Wiedervorlage | Neu prüfen, wenn sich das Preismodell ändert (Elastizität über 1, längere Erholung, mehrere Märkte) oder ein Preiskampf gegen den Spieler gewünscht ist. | 16.10.2026 |

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

### KI-Häuser handeln nicht von Hand (PR #NR)

Probeweise gebaut (nicht übernommen): Eigene Ware zurückhalten unter 70/80/90 % des Grundpreises (vorsichtig/ausgewogen/wagemutig), freigeben 10 Punkte darüber oder ab 75/90 % Lagerfüllung. Gemessen auf main `2cd3222`, je Charakter 20 Partien × 30 Jahre, 60 KI-Häuser, Reinvermögen Ø am Ende:

| KI-Charakter | ohne Zurückhalten | mit Zurückhalten |
|---|---|---|
| Vorsichtig | 119.017 | 119.014 |
| Ausgewogen | 111.218 | 108.483 (−2,5 %) |
| Wagemutig | 63.944 | 61.444 (−4 %) |

Schwellen-Versuch (ausgewogen): unter 50–70 % wird nie zurückgehalten (Preis fällt nicht so tief), unter 80 % −0,7 bis −1,5 %. Preissturz durch den Spieler (6 Stunden lang je 100 Stück jeder KI-Ware, Jahr 5–7, 20 Partien): Zuwachs der KI mit und ohne Zurückhalten unter 70 % gleich (14.031), unter 80 % weniger (13.797).
