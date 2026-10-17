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
| Nach dem Verkauf | War der Betrieb ausgewählt, zeigt die Auswahl das freie Haus mit Kaufangebot (Vorschlag Darstellung); Innenansicht verlassen; nichts zeigt mehr auf den Betrieb. | 16.10.2026 |
| Kauf freier Plätze | Prüfung über `LiegenschaftErwerbVerboten` des Kerns statt eigener Stand-/Geldprüfung; Knopf „Kaufen (Preis)“ bzw. „Es fehlen …“, Grund als ⚠-Zeile im Text und als Tooltip. Bebaute Plätze nennen Stufe und Meistergrad. | 16.10.2026 |
| Jahresbericht | Erlös als „Verkäufe +X“ unter „Außerdem“, nicht im Gewinn. | 16.10.2026 |

### Schriften beim Beenden freigeben (PR #100)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufräumen beim Beenden | Der Autoload `Fenster` lässt in `_ExitTree` die Schriften los (`Schriften.Freigeben`: Thema, Ersatzschrift, statische `FontVariation`s) und ruft `GC.Collect` und `GC.WaitForPendingFinalizers`. Absicherung gegen das sporadische Leck bzw. den Absturz beim Beenden im Rauchtest; Wirkung nicht nachgewiesen (Vergleich 8 : 8 Läufe, beide ohne Fund). | 16.10.2026 |
| Statische Godot-Objekte | Neue statische Felder mit Godot-Ressourcen (Schriften, Texturen, Materialien) beim Beenden ebenso freigeben – C# hält sie sonst bis zum Abbau der Laufzeit fest. | 16.10.2026 |

### Bauen und Leerstand (PR #103)

| Thema | Entscheidung | Datum |
|---|---|---|
| Einstieg | Knopf **Bauen** in der Zeile Familie · Hinterzimmer · Marktplatz des Kontors. Die **Bautafel** (`BauAnzeige`) steht rechts an der Stelle des Kontors, solange der Baumodus offen ist; Esc geht einen Schritt zurück: Rückfrage → Platzwahl → Liste → Baumodus aus. | 17.10.2026 |
| Auswahl | Liste der Wohnhaus-Stufen und der Werkstatt-Berufe (Stufe 1, nur Stadtbetriebe) mit Kosten, Maß und Bauzeit aus `Bauwesen.Vorschau`. Nicht erlaubte Gebäude sind **ausgegraut** mit dem Grund aus `BauherrVerboten` darunter und im Tooltip; geprüft wird nicht selbst. Legende der Bauzonenfarben oben. | 17.10.2026 |
| Platzwahl | Der Umriss folgt der Maus (Darstellung); die Tafel zeigt dazu den Stand: grün „Hier kann gebaut werden“ oder rot mit dem Grund aus `PlatzVerboten`, dazu Knöpfe „Drehen (R)“ und „Zurück“. R dreht, Linksklick auf grün setzt, auf rot passiert nichts, Rechtsklick ohne Ziehen geht zurück. Über Tafeln folgt der Umriss nicht. | 17.10.2026 |
| Rückfrage | `BauRueckfrage` in der Bildmitte (Muster `VerkaufAnzeige`): Preis, angerechnetes altes Haus, Kosten beim Baubeginn, Unterhalt (Werkstatt: ein Jahr bar zusätzlich), Bauzeit mit Fertigstellung („gegen 14:30 Uhr“ bzw. Datum). Der Knopf ist gesperrt, solange `Vorschau.Verboten` einen Grund nennt. Zahlen zweimal je Sekunde aufgefrischt, neu gebaut nur bei Änderung. Während sie offen ist, bleibt der Umriss stehen (`BauFest`). | 17.10.2026 |
| Leerstand | Auswahltafel eines leeren Wohnhauses: Kauf- und Abrisspreis als Text (aus `LeerstandVorschau`), darunter die Knöpfe **Kaufen und sanieren** und **Abreißen lassen**, je mit Rückfrage (gleiches Fenster, Rechnung Kauf, Sanierung bzw. Abriss, Anrechnung, Dauer). Gesperrt mit Grund als Tooltip und ⚠-Zeile. Nach dem Befehl zeigt die Auswahl die Baustelle, nach der Fertigstellung das Haus. | 17.10.2026 |
| Baustelle und Bauplatz | Auswahltafel mit dem, was entsteht, für wen und wann es fertig ist; ein Bauplatz nennt Fläche und wie man baut. Die Auswahl folgt einer fertigen Baustelle zum Haus bzw. zur Werkstatt und schließt sich, wenn der Bauplatz überbaut wird (`Stadtansicht.KennungNachBau`). | 17.10.2026 |
| Startoptionen | `--bauen`, `--bauort`, `--bauklick`, `--aufsteigen`, `--spulen`, `--leerstand`, `--speichern`, `--blick`, `--ui` für den Klicktest (Liste in `CLAUDE.md`). | 17.10.2026 |


### Waren-Icons im Stil des kolorierten Holzschnitts (PR #113)

| Thema | Entscheidung | Datum |
|---|---|---|
| Stil | Kolorierter Holzschnitt wie Drucke um 1480 statt Pixel-Art: schwarzer Umriss, Schraffur im Schatten, von Hand verwaschen koloriert. Gewählt aus drei Proben (Buchmalerei, kolorierter Holzschnitt, Glasmalerei). Ersetzt die Pixel-Art-Entscheidung vom 14.10.2026 im Archiv. | 17.10.2026 |
| Hintergrund | Freigestellt auf der Kachel, kein Pergamentfeld. | 17.10.2026 |
| Größe und Filter | 128 × 128, Import mit Mipmaps, Anzeige mit `LinearWithMipmaps` (im Markt 64 px). | 17.10.2026 |
| Zeichnen | Weiter per Skript (`tools/icons/waren_icons.py`, Pillow und numpy), fester Zufall je Ware. Dünne Stellen bekommen feinere Linien und keine Schraffur, sonst bliebe nur Schwarz; trotzdem Glieder, Klingen und Stiele nicht dünner als etwa 2,5 Rastereinheiten anlegen. | 17.10.2026 |
