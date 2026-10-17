# Bereich Leveldesign – Offen und Entscheidungen

**Nur der Leveldesign-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

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

### Straßenbreite (PR #88)

| Thema | Entscheidung | Datum |
|---|---|---|
| Breite | **Straßen in der Stadt gut 6,5 m breit**: Bauzonen beginnen 3,3 m neben der Straßenmitte (vorher 4,6 m). Köln: 29 statt 25 Werkstätten auf leerer Karte, alle 4 Stadthöfe passen. | 16.10.2026 |
| Keine Querstraßen | Zusätzliche Straßen durch die Mauerring-Blöcke wurden gemessen und **verworfen**: Sie zerschneiden die Blöcke, ohne mehr Platz zu bringen (begrenzend ist die Blocktiefe). | 16.10.2026 |

### Deutzer Mauer und Hafen (PR #89)

| Thema | Entscheidung | Datum |
|---|---|---|
| Kartendatei | Neue Felder **`nebenmauern`** (weitere Mauern) und **`haefen`** (Kaimauer, Anleger, Kran). Bestehende Felder bleiben gleich, deshalb bleibt es **Format 1**. | 16.10.2026 |
| Köln | **Deutzer Mauer** um den Brückenkopf, zum Rhein hin offen, mit **Nordtor** (Mülheimer Weg) und **Osttor** (Bensberger Straße); beide schließen nachts. **Kölner Hafen**: Kaimauer am linken Ufer zwischen Dom und Heumarkt, 5 Anleger, Kran. Die Stadtmauer endet jetzt an den Ufertürmen. | 16.10.2026 |
| Prüfungen | Wege kreuzen Mauern nur an Toren; Anleger und Kran liegen an der Kaimauer. | 16.10.2026 |

### Greybox-Karte mit Bauzonen (PR #103)

| Thema | Entscheidung | Datum |
|---|---|---|
| Karte | Die **heutige Greybox-Stadt** bekommt eine Kartendatei `game/data/karten/greybox.json` (Werkzeug `tools/karten/greybox.py`, Vorschau `docs/karten/greybox.svg`), damit der Kern das freie Bauen prüfen kann. Köln bleibt unberührt. Schema wie Köln (Format 1), neue Felder: `breite` je Weg, `drehung` je Gebäude, `gebaeude`, `probeplan`. | 17.10.2026 |
| Koordinaten | **Weltkoordinaten der Stadtansicht in Metern** (Godot x/z), keine Umrechnung. Drehung wie Godot `Rotation.Y` in Grad: 0 = Vorderseite nach +z, 90 = +x, 180 = −z, 270 = −x. | 17.10.2026 |
| Wohnhausplätze | Die **Doppelplätze am Ring** stehen als `wohnhaus` 1 bis 8 in der Reihenfolge, in der die Darstellung sie den Konten zuweist; heute spielen höchstens 4 Häuser. Die ersten vier liegen gleich weit vom Markt (44,3 bis 47,2 m). | 17.10.2026 |
| Bauzonen | **9 Zonen, 2.432 m²**: 1 Kern (Südostecke, ein Wohnhaus), 7 Vorstadt vor dem Westtor und an der Straße zum Steinbruch am Felsenhang, 1 Mauerring. Innerhalb der Mauer ist die Stadt voll, freies Gelände an einer Straße gibt es nur vor dem Westtor. Im Probeplan passen 5 Werkstätten, 2 Stadthöfe und 8 Wohnhäuser. | 17.10.2026 |
| Stadthof | Innerhalb der Mauer ist kein Platz für 16 × 14 m frei. Die Zone `M1` an der Westmauer **zählt deshalb als Mauerring** (in `fiktion` vermerkt); `HOF_ZONE_ALS_MAUERRING = False` im Skript macht sie zur Vorstadt. | 17.10.2026 |
| Kern liest | `werkstatt`: ein Eintrag je Liegenschaft, `name` genau wie im Stadtplan; `wohnhaus`: Konto-Reihenfolge; jedes feste Gebäude mit `groesse` blockiert; Eingangsregel: 2 Felder der Vorplatzreihe höchstens 7 m von der Straßenmitte (Probeplan erfüllt sie). Einwohnerhäuser kennt der Kern nicht, die Zonen decken nur freien Boden ab. | 17.10.2026 |
| Abstände | Greybox-Straßen sind nur 3 bis 5 m breit: Zonen halten **Straßenrand + 0,5 m** Abstand (Köln 3,3 m ab Straßenmitte), 2 m zu festen Gebäuden. Einwohnerhäuser bleiben unverändert, keine Zone überdeckt einen Einwohnerplatz. | 17.10.2026 |
| Leerstand | Leerstand kaufen und sanieren kostet **fest 50 % + 20 % des Hauswerts** (Entscheidung des Nutzers), nicht mehr nach Zustand. | 17.10.2026 |
