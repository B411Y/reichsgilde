# Bereich Darstellung – Offen und Entscheidungen

**Nur der Darstellungs-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (3) Raumansicht für den Dom und Feinschliff der Werkstatträume je Beruf (zweiter Teil von PR #52)
- (3) (von Kern) Karren am Stadttor zeigen: `Karren.WartetAmTor(zeit)` – Hinweg draußen vor dem Tor, Rückweg drinnen (ab PR #67)
- (3) Passanten Schritt 4: Aussehen nach Volk und Rolle (Tracht), Klick auf eine Figur öffnet die Infotafel, Maus darüber hebt sie hervor
- (3) Einwohner in den Innenräumen von Schänke und Kirche zeigen (später, eigener Schritt)
- (3) (von Kern) Wohnhaus je Haus in der Stadt zeigen, Größe nach `Wohnen.Stufe(konto)` (Fachwerkhaus bis Hof), Bau läuft: `Wohnen.FertigAb(konto)`. Wohnstube und Kontor gehören ins Wohnhaus statt ins Stammhaus (ab PR #75)

## Entscheidungen

### Passanten (PR #71)

| Thema | Entscheidung | Datum |
|---|---|---|
| Bewegung | Die Darstellung liest nur `Wo` und `Weg` des Kerns. Unterwegs läuft die Figur den kürzesten Pfad im Gassennetz; wie weit, sagt der Anteil der Wegzeit mit angebrochener Minute. Lange Wege werden schneller gegangen – die Zeit legt der Kern fest. | 16.10.2026 |
| Sichtbarkeit | Sichtbar ist nur, wer unterwegs ist oder auf Markt, Gasse, am Tor oder im Umland steht. Wer zu Hause, in Schänke, Kirche oder Rathaus ist, ist drinnen. | 16.10.2026 |
| Standplätze | Fest aus Nummer und Ankunftszeit (Hash), kein Würfel: gleich bei jedem Bild und nach dem Laden. Markt im Ring um den Brunnen, Gasse auf einer Gasse in der Stadt, Tor vor dem Osttor, Umland auf der Stelle. | 16.10.2026 |
| Rechenzeit | Einzelne Knoten mit gemeinsamen Formen und Stoffen genügen (0,2 ms je Bild für 123 Figuren, Zeichenaufrufe kaum mehr); MultiMesh erst, wenn es nötig wird. | 16.10.2026 |
| Umlandstellen | Weide, Feld und Wald haben je einen eigenen Fußweg von der Weststraße, damit keiner durch eine andere Stelle führt. | 16.10.2026 |

### Gehtempo der Passanten (PR #74)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gehtempo | **Feste Spanne je Figur** statt „Weglänge ÷ Wegzeit“: schlendern 1,5, normal 2,5, eilig 3,5 m/s (echte Sekunde bei Tempo 1, ±10 %, fest nach der Nummer). Boten eilen, Bettler und Verwitwete schlendern. | 16.10.2026 |
| Ankunft | Die Figur geht so spät los, dass sie zur Ankunftszeit des Kerns da ist. Reicht die Zeit nicht, beeilt sie sich **bis zum Doppelten**; reicht auch das nicht, kommt sie später an als gezählt. Dafür bittet die Darstellung den Spiel-KI-Chat um längere Wegzeiten (Zeile in `spiel-ki.md`). | 16.10.2026 |

