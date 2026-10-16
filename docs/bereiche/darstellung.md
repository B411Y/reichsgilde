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
- (3) Einwohner in den Innenräumen von Schänke und Kirche zeigen (später, eigener Schritt)
- (3) (von Leveldesign) Gebäudemaße auf glatte Meter: Einwohnerhäuser 6 × 6, 7 × 7, 8 × 8 m; Werkstatt belegt 16 × 14 m und zeigt jede Stufe als sichtbaren Anbau innerhalb dieser Fläche (`docs/LEVELDESIGN.md`, Gebäudegrößen)
- (3) Wohnstube und Kontor ziehen aus der Werkstatt ins Wohnhaus: Doppelklick betritt das Wohnhaus (Raumansicht), die Räume wandern dorthin (Entscheidung Kern zu PR #75)

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

### Tracht und Infotafel der Passanten (PR #78, #81)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aussehen | **Volk über die Gestalt, Rolle über die Tracht.** Ratsherr roter Mantel und Barett, Mönch braune Kutte mit Kapuze, Nonne schwarz mit weißem Schleier, Bettler graue Lumpen und gebeugt, Hirte Strohhut und Stab, Feldarbeiter Strohhut, Holzsammler Reisigbündel, Krämer Kiepe, Bote blau mit Tasche, Knecht und Magd mit Schürze, Verwitwete schwarz, Bürgerkinder kräftige Farben mit Barett. Frauen mit Rock und Haube, Zwerge mit Bart. | 16.10.2026 |
| Klick | Klick auf eine Figur öffnet die Auswahltafel (Name, Rolle, Volk, Alter, Tun und Ort bzw. Ziel mit Ankunft, Wohnhaus, Stimmung). Maus darüber hebt sie hell hervor. Treffer über die Bildschirmlage statt Klickflächen (die müssten jedes Bild mitwandern). | 16.10.2026 |

### Wohnhäuser der Spielhäuser (PR #87)

| Thema | Entscheidung | Datum |
|---|---|---|
| Platz | Jedes Spielhaus (Spieler und KI) bekommt einen **Doppelplatz am Ring** – zwei benachbarte Ringplätze, die nächsten am Markt (Mitte der Nord- und Südseite, dann Ost und West). Diese Plätze fehlen den Einwohnern; deren Häuser rücken weiter nach außen, mit 4 Spielhäusern stehen jetzt 8 in der Vorstadt. | 16.10.2026 |
| Stufen | **Jede Stufe ein eigenes Haus**: Fachwerkhaus 6 × 6, Bürgerhaus 7 × 7 mit Giebel, Steinhaus 8 × 8 mit drei Geschossen, Stadtpalais 15 × 8 mit Erker und zwei Ecktürmchen, Hof mit Haupthaus, zwei Flügeln und Hofmauer mit Tor. Wappenschild über der Tür und Fahne in der Hausfarbe. Alle Stufen am selben Doppelplatz – bis der Kern „Stadtpalais und Hof als Neubau“ umsetzt (Leveldesign). | 16.10.2026 |
| Zustand | **Ausbau:** Gerüst vor der Gassenseite. **Nach Brand:** rußiges Dach und Gerüst bis zur Instandsetzung. **Leerstand:** bleibt stehen, Fahne und Wappen weg, Tür und Fenster vernagelt. Aufkaufen und Abreißen wünscht der Nutzer – Sache des Kerns (Zeile in `kern.md`). | 16.10.2026 |
| Tafel | Klick zeigt Stufe, Ansehen, laufenden Ausbau mit Fertigzeit, Brandschaden, Hauswächter bzw. Leerstand. Wohnstube und Kontor ziehen in einem eigenen Schritt ins Wohnhaus. | 16.10.2026 |

