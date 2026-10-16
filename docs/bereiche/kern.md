# Bereich Kern – Offen und Entscheidungen

**Nur der Kern-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (5) Orks und Oger spielbar machen
- (5) Bauzonen, Bauplätze und Sanierung von Leerstand (Archiv §7 Punkt 8 und §17, `docs/LEVELDESIGN.md`)
- (5) Gaunerberuf, Schmuggel und Schwarzarbeit unter dem Statut
- (3) Fahrzeit aus der Weglänge (Wegegraph), sobald es die erste Kartendatei gibt (`docs/LEVELDESIGN.md`)
- (–) „Kirche an der Westmauer“ umbenennen – steht seit der Stadterweiterung nicht mehr an der Mauer (Name in `wirtschaft.json`, Schlüssel in der Stadtansicht)
- (6) Multiplayer, Zuschauer-Modus
- (Nutzer) Lizenz für Grafiken und Texte (CC BY 4.0?) bestätigen
- (Nutzer) Pausenregeln im Multiplayer
- (5) (von Leveldesign) Wohnhaus-Ausbau: Fachwerkhaus → Bürgerhaus → Steinhaus am selben Platz; **Stadtpalais und Hof nicht am Platz**, sondern als neuer Bau oder Abriss und Neubau (Entscheidung 16.10.2026, `docs/LEVELDESIGN.md`, Gebäudegrößen) – betrifft Entwurf PR #75
- (von Spiel-KI) Haus des Spielers im Rat: Befehl zum Abstimmen bei Wahlen und Anträgen (heute: Amtsinhaber bzw. Enthaltung); die KI-Häuser wählen seit PR #73 nach Charakter
- (5) Werkstatt frei verkaufen: Befehl fehlt noch (Entscheidung zu PR #75)
- (5) Wohnhaus kaufen statt bauen: Leerstand (`Wohnen.Leerstand`) übernehmen und sanieren – zusammen mit Bauzonen und Sanierung
- (–) Sporadisches Leck beim Beenden im Rauchtest finden: „10 ObjectDB instances leaked“ und „10 RID allocations of type DummyTexture leaked“, im zweiten Start (mit Tafeln), nur auf GitHub, lokal nicht nachzustellen. Der Rauchtest wiederholt solche Funde seit PR #83; die Warnung „1. Versuch“ im Lauf zeigt, wie oft es vorkommt

## Entscheidungen

### Konfliktfreie Protokolle (PR #70)

| Thema | Entscheidung | Datum |
|---|---|---|
| Protokolle | **Eine Datei je Bereich** in `docs/bereiche/` (Offen, Entscheidungen, bei der Spiel-KI auch Messungen). Einträge ohne Nummern, unten anhängen. `docs/ENTSCHEIDUNGEN.md` wird Archiv; die Liste „Erledigt“ entfällt (zeigt GitHub). Grund: Alle Chats schrieben ans selbe Dateiende, an dieselben Listen und nahmen dieselben Abschnittsnummern – fast jeder zweite PR hatte einen Konflikt. | 16.10.2026 |
| Automatik | Action **„PRs aktuell halten“**: nach jedem Merge auf main den neuen Stand in alle fertigen PRs mergen und die Prüfung neu starten; `docs/bereiche/*.md` mit Git-Regel `union` (beide Einträge bleiben). Echte Konflikte → Label „Konflikt mit main“ und ein Kommentar. Entwürfe bleiben unberührt. | 16.10.2026 |
| Datenversion | Die Zeile `"version"` in `wirtschaft.json` erhöht **nur der Kern**. | 16.10.2026 |
| Mergen | Der Nutzer mergt weiterhin selbst (kein Auto-Merge). | 16.10.2026 |
| PR-Titel | Beginnen mit dem Bereich („Kern: …“). | 16.10.2026 |

### Wohnhaus je Haus (PR #75)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wirkung | Das Wohnhaus ist **Pflicht für den Stand** und bringt **Ansehen** (Rat, Brautschau, Gericht). Bürger braucht das Bürgerhaus, Patrizier das Steinhaus, Edler das Stadtpalais, Freiherr den Hof. | 16.10.2026 |
| Ausbau | **Am selben Ort** Stufe für Stufe ausbauen, höchstens eine Stufe über dem eigenen Stand. Bauzeit 4 Stunden, bis dahin gilt die alte Stufe. | 16.10.2026 |
| Kosten | **Ausbau + jährlicher Unterhalt**: Bürgerhaus 1.500 / 30, Steinhaus 4.000 / 80, Stadtpalais 10.000 / 200, Hof 25.000 / 500 Gulden (Unterhalt 2 % des Ausbaus, mit den Haushaltskosten). Ansehen +2 / +4 / +6 / +8. | 16.10.2026 |
| Wohnen und Betriebe | Ins Wohnhaus ziehen Wohnstube und Familie sowie das Kontor. Die Werkstatt ist damit frei und soll verkauft werden können (Befehl folgt). | 16.10.2026 |
| Gauner | Das Wohnhaus wird ein Ziel für Gauner, die Betriebe bleiben es weiterhin (eigener PR). | 16.10.2026 |
| KI-Häuser | `KiRivale` baut das nötige Wohnhaus vor dem Aufstieg, wenn danach die Reserve bleibt (kleine Änderung im Bereich Spiel-KI). | 16.10.2026 |
| Alte Spielstände | Bekommen beim Laden das Haus, das ihr Stand verlangt. | 16.10.2026 |
| Preise nach der Messung | **Bleiben** (1.500 / 4.000 / 10.000 / 25.000, Unterhalt 2 %). Der Aufstieg soll Geld kosten; dass wagemutige KI-Häuser beim Edlen bleiben, verfeinert die Spiel-KI. | 16.10.2026 |

### Gauner gegen das Wohnhaus (PR #77)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gaunerstücke | **Einbruch und Brand** am Wohnhaus, zusätzlich zu den Stücken gegen Betriebe (die bleiben Ziel). | 16.10.2026 |
| Einbruch | **Anteil vom Bargeld, gedeckelt**: 5 %, höchstens 300 Gulden je Hausstufe (Fachwerkhaus 300 … Hof 1.500). Bargeld ohne Hehler. Kosten 200, Chance 60 %. Vergehen: Diebstahl. | 16.10.2026 |
| Brand | **Reparatur + Ansehen fehlt**: 20 % des Ausbaupreises der Stufe (mindestens 100), das Ansehen des Hauses fehlt einen Tag (1.020 Min.), so lange kein Ausbau. Die Stufe bleibt, der Stand ist nie in Gefahr. Kosten 300, Chance 50 %. Vergehen: Sabotage. | 16.10.2026 |
| Schutz | **Eigener Hauswächter** wie im Betrieb: 150 Gulden im Jahr, halbiert die Chance, Zeugen 60 % statt 25 %. Die Streife der Wache wirkt wie bei Betrieben in der Stadt. | 16.10.2026 |
| KI-Häuser | Stellen nach einem Anschlag auf ihr Wohnhaus einen Hauswächter ein (kleine Änderung im Bereich Spiel-KI). Selbst heuern sie noch keine Bande gegen Wohnhäuser an – Punkt für die Spiel-KI. | 16.10.2026 |

## Messungen

Messungen des Kerns stehen in `docs/WIRTSCHAFTSMODELL.md` (nur der Kern schreibt dort).
