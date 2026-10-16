# Bereich Leveldesign – Offen und Entscheidungen

**Nur der Leveldesign-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (3) Deutzer Mauer und Hafenkante als eigene Felder in der Kartendatei
- (–) Reichskarte zum Wechsel zwischen den Städten (Idee, später)

## Entscheidungen

Die Kartenregeln selbst stehen in `docs/LEVELDESIGN.md`.

### Kartendatei und Köln (PR #72)

| Thema | Entscheidung | Datum |
|---|---|---|
| Format | **Eine JSON-Datei je Stadt** in `game/data/karten/<kennung>.json`, lesbar für Kern (ohne Godot) und Darstellung. Format-Nummer 1. Beschreibung in `docs/LEVELDESIGN.md`, Abschnitt „Kartendatei“. | 16.10.2026 |
| Erstellung | **Python-Werkzeug** `tools/karten/karte.py`, je Stadt ein Skript (`tools/karten/koeln.py`). Es prüft die Kartenregeln und schreibt nur bei Erfolg; dazu ein Vorschaubild `docs/karten/<kennung>.svg`. | 16.10.2026 |
| Koordinaten | Meter, x = Osten, z = Süden (−z = Norden), Ursprung am Marktplatz – wie im Spiel. Bauzonen auf dem 2-m-Raster. | 16.10.2026 |
| Größe Köln | **Mauerhalbkreis mit 150 m Radius** (die Greybox-Stadt misst innen 100 m). | 16.10.2026 |

### Gebäudegrößen (PR #76)

| Thema | Entscheidung | Datum |
|---|---|---|
| Grundsatz | Ein Gebäude **belegt beim Bau die Fläche seiner größten Ausbaustufe** am selben Platz; Fläche in Rasterfeldern mit 2 m Vorplatz. | 16.10.2026 |
| Werkstatt | **Gleiche Fläche für Stufe 1–3, aber größer als heute: 16 × 14 m (56 Felder).** Jede Stufe soll von außen sichtbar größer werden (Anbau, Hof). | 16.10.2026 |
| Wohnhaus | **Fachwerkhaus belegt die Fläche des Steinhauses (8 × 10 m, 20 Felder)** und wächst am Platz über Bürgerhaus zum Steinhaus. **Stadtpalais (12 × 10 m) und Hof (16 × 14 m)** sind ein neuer Bau an anderer Stelle oder Abriss und Neubau. | 16.10.2026 |
| Aussehen | Gebäude behalten ihr Aussehen, Maße auf **glatte Meter** (Einwohnerhäuser 6 / 7 / 8 m). | 16.10.2026 |

### Stadthof (PR #84)

| Thema | Entscheidung | Datum |
|---|---|---|
| Name | Die höchste Wohnhausstufe heißt **Stadthof** statt „Hof“, damit sie nicht mit Bauernhof und Klosterhof im Umland verwechselt wird. | 16.10.2026 |
| Lage | Der Stadthof darf in der **Kernlage und im Mauerring** stehen (vorher nur Kernlage; dort passte in Köln nur einer). | 16.10.2026 |

### Straßenbreite (PR #NUMMER)

| Thema | Entscheidung | Datum |
|---|---|---|
| Breite | **Straßen in der Stadt gut 6,5 m breit**: Bauzonen beginnen 3,3 m neben der Straßenmitte (vorher 4,6 m). Köln: 29 statt 25 Werkstätten auf leerer Karte, alle 4 Stadthöfe passen. | 16.10.2026 |
| Keine Querstraßen | Zusätzliche Straßen durch die Mauerring-Blöcke wurden gemessen und **verworfen**: Sie zerschneiden die Blöcke, ohne mehr Platz zu bringen (begrenzend ist die Blocktiefe). | 16.10.2026 |

