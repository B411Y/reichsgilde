# Bereich Kern – Offen und Entscheidungen

**Nur der Kern-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (5) Orks und Oger spielbar machen
- (5) Eigenes Wohnhaus je Haus, Größe nach Stand (Archiv §17, `docs/LEVELDESIGN.md`)
- (5) Bauzonen, Bauplätze und Sanierung von Leerstand (Archiv §7 Punkt 8 und §17, `docs/LEVELDESIGN.md`)
- (5) Gaunerberuf, Schmuggel und Schwarzarbeit unter dem Statut
- (3) Fahrzeit aus der Weglänge (Wegegraph), sobald es die erste Kartendatei gibt (`docs/LEVELDESIGN.md`)
- (–) „Kirche an der Westmauer“ umbenennen – steht seit der Stadterweiterung nicht mehr an der Mauer (Name in `wirtschaft.json`, Schlüssel in der Stadtansicht)
- (6) Multiplayer, Zuschauer-Modus
- (Nutzer) Lizenz für Grafiken und Texte (CC BY 4.0?) bestätigen
- (Nutzer) Pausenregeln im Multiplayer

## Entscheidungen

## Messungen

Messungen des Kerns stehen in `docs/WIRTSCHAFTSMODELL.md` (nur der Kern schreibt dort).

### Konfliktfreie Protokolle (PR #70)

| Thema | Entscheidung | Datum |
|---|---|---|
| Protokolle | **Eine Datei je Bereich** in `docs/bereiche/` (Offen, Entscheidungen, bei der Spiel-KI auch Messungen). Einträge ohne Nummern, unten anhängen. `docs/ENTSCHEIDUNGEN.md` wird Archiv; die Liste „Erledigt“ entfällt (zeigt GitHub). Grund: Alle Chats schrieben ans selbe Dateiende, an dieselben Listen und nahmen dieselben Abschnittsnummern – fast jeder zweite PR hatte einen Konflikt. | 16.10.2026 |
| Automatik | Action **„PRs aktuell halten“**: nach jedem Merge auf main den neuen Stand in alle fertigen PRs mergen und die Prüfung neu starten; `docs/bereiche/*.md` mit Git-Regel `union` (beide Einträge bleiben). Echte Konflikte → Label „Konflikt mit main“ und ein Kommentar. Entwürfe bleiben unberührt. | 16.10.2026 |
| Datenversion | Die Zeile `"version"` in `wirtschaft.json` erhöht **nur der Kern**. | 16.10.2026 |
| Mergen | Der Nutzer mergt weiterhin selbst (kein Auto-Merge). | 16.10.2026 |
| PR-Titel | Beginnen mit dem Bereich („Kern: …“). | 16.10.2026 |
