# Bereich Infra – Offen und Entscheidungen

Werkzeuge, Abläufe, Lizenz und Asset-Protokoll. Neue Einträge unten anhängen, keine Nummern.

## Offen

- Lizenz der Inhalte (PR #240): Die Entscheidung sollte von einer Anwältin oder einem Anwalt geprüft werden; offen ist dabei u. a., ob Texte in Code-Zeichenketten (`game/scripts/`) als Inhalte oder als Code gelten.
- (Lizenz) Der Nutzer kann später entscheiden, ob Überarbeitungen vorhandener Dateien ebenfalls unter die neue Lizenz fallen. Bis dahin prüft das Skript nur Zeilen mit `datum` nach dem Stichtag; eine Überarbeitung, die eine bestehende Zeile ändert, behält deren Datum und CC BY.

## Entscheidungen

### Lizenz der Inhalte (PR #240)

| Thema | Entscheidung | Datum |
|---|---|---|
| Code | Code, Skripte, Shader sowie Zahlenwerte und Struktur der Spieldaten (`wirtschaft.json` usw.) bleiben MIT | 29.10.2026 |
| Inhalte bis Stichtag | Was bis zum Merge von PR #240 veröffentlicht ist, bleibt unter CC BY 4.0 (nicht widerrufbar) | 29.10.2026 |
| Inhalte ab Stichtag | Neu ins Repository Kommendes (neue Dateien, neue Texte; `game/assets/`, Spieler-Texte in `game/daten/*.json`): „Alle Rechte vorbehalten“, © B411Y | 29.10.2026 |
| Überarbeitungen | Überarbeitete oder ersetzte Fassungen bereits vorhandener Dateien bleiben unter CC BY 4.0 (Wahl des Nutzers: „Ab Merge des Lizenz-PRs“, nicht „auch neue Fassungen“) | 29.10.2026 |
| Fremdmaterial | Behält seine Lizenz (Quaternius CC0, Schriften OFL, TressFX MIT …) | 29.10.2026 |
| GitHub | Öffentliches Repository: zusätzlich GitHub-Nutzungsbedingungen, Abschnitt D.5 (Ansehen, Forken innerhalb von GitHub) | 29.10.2026 |
| Protokoll | Neue Zeilen ab Stichtag: `lizenz_ergebnis` = „Alle Rechte vorbehalten“; das Prüfskript prüft das (`STICHTAG` = 2026-10-29, Tag des Merges von PR #240) | 29.10.2026 |

### Merge-Warteschlange (PR #243)

| Thema | Entscheidung | Datum |
|---|---|---|
| Freigabe | Dem Nutzer liegt immer nur ein PR zur Freigabe oder zum Merge vor; fertige PRs warten in der Warteschlange des Teamleiters (`.claude/teamleiter.md`) | 29.10.2026 |
| Nachziehen | Nach jedem Merge bringt der Teamleiter den nächsten PR auf den neuen `main`-Stand (Konflikte, CI grün) und gibt ihn erst dann frei; Grund: fünf offene PRs erzeugten am 29.10. nach jedem Merge neue Konflikte | 29.10.2026 |
| Protokoll | `docs/assets/protokoll.csv` steht nach `pfad_muster` sortiert, neue Zeilen an der passenden Stelle; das Prüfskript meldet Verstöße und nennt den Platz (Zuordnung „letzte Zeile gewinnt“ blieb beim Sortieren unverändert) | 29.10.2026 |

### Gemini entfernt, Prüfungen mit Bot-Token (PR #249)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gemini | Gemini (API und Antigravity-CLI `agy`) wird nicht mehr genutzt und steht nicht mehr in den Regeln (`CLAUDE.md`, `.claude/`); Commit-Zusatz nur noch `(Strata)`. Bild-KI nur lokal (ComfyUI, Qwen-Image), daneben Strata | 29.10.2026 |
| Historie | Protokoll, Prompts, Herkunftsdateien und alte Bereichseinträge nennen Gemini weiter als Herkunft bestehender Assets | 29.10.2026 |
| Bot-Token | `prs-aktuell.yml` pusht mit dem Repository-Geheimnis `BOT_TOKEN` (klassisches Token des Bot-Kontos, `repo` + `workflow`); dann starten die PR-Prüfungen von selbst und der eigene `build.yml`-Lauf entfällt | 29.10.2026 |
| Rückfall | Ohne `BOT_TOKEN`: `GITHUB_TOKEN` und selbst gestarteter `build.yml`-Lauf wie bisher | 29.10.2026 |
| Schutzregel | Der Push geht nur auf PR-Branches, nie auf `main`; die Schutzregel ändert sich nicht | 29.10.2026 |

### Klicktest unter Windows (PR #…)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufbau | Das Spiel führt die Aktionen selbst aus (Autoload `KlicktestTreiber`, `--klickskript`/`--klickausgabe`/`--klickwarten`) statt echter Maus- und Tastenbefehle; `tools/klicktest/lauf.ps1` mit demselben Aufbau wie `lauf.sh` | 29.10.2026 |
| Treiber | `-Treiber vulkan` (Standard) oder `d3d12`; beide liefen am 29.10. auf der RTX 4080 mit gleichem Bild (erster D3D12-Test) | 29.10.2026 |
| Absicherung | Vor dem Start `reichsgilde-frage`, im Lauf alle 10 s `reichsgilde-pruefen` (Code 3 beendet Godot), Abbruch nach Wartezeit + Aktionen + 120 s; `user://` umgelenkt nach `%LOCALAPPDATA%\Reichsgilde\klicktest-daten` | 29.10.2026 |
| Grenze | `Viewport.GetMousePosition` liest die echte Maus: Hover, Tooltips und die Lage der Auswahltafel folgen ihr; Klicks und Tasten wirken über das Ereignis | 29.10.2026 |
| Argumente | `lauf.ps1` hat keinen `param`-Block, weil Windows PowerShell 5.1 bei `-File` ein `--` und `--karte=…` sonst als Parameter abweist | 29.10.2026 |
