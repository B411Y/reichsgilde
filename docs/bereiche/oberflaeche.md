# Bereich Oberfläche – Offen und Entscheidungen

**Nur der Oberflächen-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (von Spiel-KI) Ratsfenster: Abstimmen von Hand bei Wahlen und Anträgen, sobald der Kern den Befehl hat; die Wahlprognose zählt seit PR #73 die Stimmen der KI-Häuser mit
- (von Spiel-KI) Marktfenster: Warengruppe und Warenkorb im Tooltip zeigen – `Einwohnerschaft.Warengruppe(ware)` (z. B. „Schmiedewaren“) und `Warenkorbfaktor(ware)` (0,9–1,1: „Die Einwohner fragen 7 % mehr nach“), seit PR #91

## Entscheidungen

### Werkstatt verkaufen (PR #97)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ort des Knopfs | Unten im Bedienfeld des eigenen Betriebs: „Verkaufen …“ bzw. „Pacht kündigen …“. Gesperrt, solange der Kern einen Grund nennt; der Grund steht grau daneben und im Tooltip. | 16.10.2026 |
| Rückfrage | Eigenes Fenster in der Bildmitte (wie Markt und Rat, Esc schließt): Erlös fürs Haus, Lagerware je Ware, Abrechnung bis heute, Summe, was mitgeht, Kasse der Geldleihe. Erst „Verkaufen“ schickt den Befehl. Die Zeit läuft weiter, die Zahlen werden zweimal je Sekunde aufgefrischt. | 16.10.2026 |
| Warnung Lagerware | Oranger Kasten oben in der Rückfrage, sobald Ware im Lager liegt, mit Stück und Erlös je Ware und dem Hinweis, dass der Markt meist mehr bringt. | 16.10.2026 |
| Nach dem Verkauf | Auswahl und Bedienfeld schließen, Innenansicht verlassen; nichts zeigt mehr auf den Betrieb. | 16.10.2026 |
| Kauf freier Plätze | Prüfung über `LiegenschaftErwerbVerboten` des Kerns statt eigener Stand-/Geldprüfung; Knopf „Kaufen (Preis)“ bzw. „Es fehlen …“, Grund als ⚠-Zeile im Text und als Tooltip. Bebaute Plätze nennen Stufe und Meistergrad. | 16.10.2026 |
| Jahresbericht | Erlös als „Verkäufe +X“ unter „Außerdem“, nicht im Gewinn. | 16.10.2026 |
