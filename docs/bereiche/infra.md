# Bereich Infra – Offen und Entscheidungen

Werkzeuge, Abläufe, Lizenz und Asset-Protokoll. Neue Einträge unten anhängen, keine Nummern.

## Offen

- Lizenz der Inhalte (PR #240): Nach dem Merge trägt der Teamleiter das Merge-Datum als `STICHTAG` in `tools/assets/protokoll_pruefen.py` ein. Die Entscheidung sollte von einer Anwältin oder einem Anwalt geprüft werden; offen ist dabei u. a., ob Texte in Code-Zeichenketten (`game/scripts/`) als Inhalte oder als Code gelten.

## Entscheidungen

### Lizenz der Inhalte (PR #240)

| Thema | Entscheidung | Datum |
|---|---|---|
| Code | Code, Skripte, Shader sowie Zahlenwerte und Struktur der Spieldaten (`wirtschaft.json` usw.) bleiben MIT | 29.10.2026 |
| Inhalte bis Stichtag | Was bis zum Merge von PR #240 veröffentlicht ist, bleibt unter CC BY 4.0 (nicht widerrufbar) | 29.10.2026 |
| Inhalte ab Stichtag | Neu ins Repository Kommendes oder neu Erstelltes (`game/assets/`, Spieler-Texte in `game/daten/*.json`): „Alle Rechte vorbehalten“, © B411Y | 29.10.2026 |
| Fremdmaterial | Behält seine Lizenz (Quaternius CC0, Schriften OFL, TressFX MIT …) | 29.10.2026 |
| GitHub | Öffentliches Repository: zusätzlich GitHub-Nutzungsbedingungen, Abschnitt D.5 (Ansehen, Forken innerhalb von GitHub) | 29.10.2026 |
| Protokoll | Neue Zeilen ab Stichtag: `lizenz_ergebnis` = „Alle Rechte vorbehalten“; das Prüfskript prüft das (`STICHTAG`) | 29.10.2026 |
