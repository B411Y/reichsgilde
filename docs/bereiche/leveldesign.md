# Bereich Leveldesign – Offen und Entscheidungen

**Nur der Leveldesign-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (3) Kapazitätsprüfung im Kartenwerkzeug: Gebäude mit ihren Flächen in die Bauzonen einpassen und prüfen, ob Köln für 8 Häuser und die Einwohner reicht (Überschlag: knapp)
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

### Gebäudegrößen (PR #NUMMER)

| Thema | Entscheidung | Datum |
|---|---|---|
| Grundsatz | Ein Gebäude **belegt beim Bau die Fläche seiner größten Ausbaustufe** am selben Platz; Fläche in Rasterfeldern mit 2 m Vorplatz. | 16.10.2026 |
| Werkstatt | **Gleiche Fläche für Stufe 1–3, aber größer als heute: 16 × 14 m (56 Felder).** Jede Stufe soll von außen sichtbar größer werden (Anbau, Hof). | 16.10.2026 |
| Wohnhaus | **Fachwerkhaus belegt die Fläche des Steinhauses (8 × 10 m, 20 Felder)** und wächst am Platz über Bürgerhaus zum Steinhaus. **Stadtpalais (12 × 10 m) und Hof (16 × 14 m)** sind ein neuer Bau an anderer Stelle oder Abriss und Neubau. | 16.10.2026 |
| Aussehen | Gebäude behalten ihr Aussehen, Maße auf **glatte Meter** (Einwohnerhäuser 6 / 7 / 8 m). | 16.10.2026 |

