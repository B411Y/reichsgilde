# CLAUDE.md – Arbeitsregeln für Reichsgilde

Jeder Agent liest diese Datei zu Beginn. Gearbeitet wird im **Teammodell**: Ein Teamleiter schneidet Aufträge, Mitglieder (Rollen in `.claude/agents/<name>.md`) führen sie aus. Der Teamleiter (Hauptsitzung) liest zusätzlich `.claude/teamleiter.md`.

## Das Projekt in Kürze

- **Reichsgilde** (Arbeitstitel): 2,5D-Wirtschaftssimulation im Stil von „Die Gilde Gold Edition“, düster-bodenständiges Heiliges Römisches Reich mit Menschen, Zwergen, Elben, Halblingen, Orks und Ogern.
- **Oberfläche: kolorierter Holzschnitt** des Spätmittelalters (Vorbild Schedelsche Weltchronik, um 1480): schwarzer Umriss, Schraffur im Schatten, Handfarbe leicht neben den Linien. Gilt für Tafeln, Fenster, Icons, Wappen, Bilder; Ausnahme: Charakterzug-Medaillons kräftig gemalt (Nutzer, 28.10.2026). **3D-Welt gemalt-realistisch** (Nutzer, 20.10.2026): schlichte Modelle mit Oberflächenstruktur (Mauerwerk, Maserung, Ziegel), normales Licht, kein Holzschnitt-Shader. Keine Pixel-Art, kein Flat-Stil.
- Godot 4.7.2 (.NET), C#/.NET 8; Code, Skripte und Shader MIT; Inhalte (`game/assets/`, Spieler-Texte) bis zum Merge von PR #240 CC BY 4.0, danach neu Entstandenes „Alle Rechte vorbehalten“ (Entscheidung 29.10.2026, `LICENSE-INHALTE.md`); Fremdmaterial behält seine Lizenz. Hobbyprojekt eines Solo-Entwicklers mit wenig Programmiererfahrung: **umsetzbar und lehrreich erklären.**
- Aufbau: `src/Reichsgilde.Core` (Simulation, kennt Godot nicht) · `game/` (Darstellung und Oberfläche, liest den Kern nur) · `tests/` · `docs/` · Werte in `game/daten/wirtschaft.json`. In `game/` nach Dateiart: `daten/`, `karten/`, `assets/` (`modelle/<unterordner>/`, `texturen/`, `shader/`, `oberflaeche/`, `audio/`), `scenes/`, `scripts/` (siehe `docs/ARCHITEKTUR.md`).
- Weiterlesen: `README.md` (Startseite), `docs/spielbuch/README.md` (Spielbuch), `docs/ENTWICKLUNG.md`, `docs/ARCHITEKTUR.md`, `docs/bereiche/<bereich>.md` (Offen, Entscheidungen), `docs/ENTSCHEIDUNGEN.md` (Archiv bis 16.10.2026, Phasen), `docs/WIRTSCHAFTSMODELL.md`, `docs/LEVELDESIGN.md`, `docs/STARTOPTIONEN.md`.

## Zuständigkeiten

| Mitglied | Dateien |
|---|---|
| **kern** | `src/Reichsgilde.Core/**` (außer `Ki/`, `Buerger/`), `tests/**`, `game/daten/wirtschaft.json` |
| **spiel-ki** | `src/Reichsgilde.Core/Ki/**`, `src/Reichsgilde.Core/Buerger/**`, eigene Tests (`Ki*Tests.cs`, `Buerger*Tests.cs`) |
| **darstellung** | `game/scripts/Stadtansicht*.cs`, Szenen, Modelle, Licht, Kamera |
| **oberflaeche** | `game/scripts/*Anzeige.cs`, `Hauptmenue.cs`, `Hauptszene.cs` |
| **leveldesign** | `docs/LEVELDESIGN.md`, `tools/Kartenbau/**`, `game/karten/*.json`, `docs/karten/*.svg` |
| **sound** | `game/assets/audio/**`, Audio-Busse, `game/scripts/Klang*.cs` |
| **infra** | `CLAUDE.md`, `.claude/**`, `.github/**`, `tools/**` (außer Kartenbau) |

Ohne eigenen Bereich: **integration** (Funktions-Branch, Klicktest, PR), **pruefer** und **suche** (nur lesen). Dateien außerhalb des eigenen Bereichs nur klein und begründet ändern und im Bericht nennen.
Nur **kern** ändert `wirtschaft.json`, `Stadtwirtschaft.cs` und `Speicherstand.cs`; braucht ein anderer dort etwas, geht es an den Teamleiter. **Ausnahme spiel-ki:** kleine Andockstellen (Einzelheiten in `.claude/agents/spiel-ki.md`), jede im Bericht genannt. Bestehende Werte und Regeln ändert nur kern. Darstellung und Oberfläche **lesen den Kern nur**; fehlt eine Abfrage, kleine Ergänzung im Kern mit Test.

## Feste Regeln

- **Mergen** (Nutzer, 19.10.2026; Bot-Konto und Schutzregel 28.10.2026): Claude arbeitet auf GitHub mit einem eigenen Bot-Konto, nicht mit dem des Nutzers. Schutzregel auf `main`: nur per PR, drei CI-Prüfungen Pflicht, Freigabe der Code-Besitzer für die Pfade in `.github/CODEOWNERS` (u. a. `wirtschaft.json`, `Speicherstand*.cs`, `CLAUDE.md`, `.github/`, `.claude/`). Der Nutzer darf an der Regel vorbei mergen, der Bot nicht. Nach `main` mergt nur der Teamleiter (Ablauf in `.claude/teamleiter.md`); PRs mit geschützten Dateien gibt der Nutzer frei und mergt selbst. **Kein Auto-merge** (auch nicht `gh pr merge --auto`): ohne Klicktest nichts nach `main`.
- **Nichts aus dem Original-Spiel** (Grafiken, Sounds, Musik, Texte, Modelle); es ist nur Vorbild für Mechaniken. **Mechanik-Katalog** und **Design-Dossier** kommen **nie** ins Repository.
- Spieltag 04:00–23:00 Uhr (1140 Ticks = 1 Spieljahr, im Code nur über die `Spielzeit`-Konstanten), Arbeitszeit 8–20 Uhr (Wirtshaus 8–22 Uhr), Gauner ab 20 Uhr.
- Bezeichner deutsch, Umlaute umschrieben (`Waehrung`), deutsche Kommentare. Antworten an den Nutzer deutsch, kurz, ohne Emojis.
- Tests bleiben grün, nie einen Test abschalten. **Neue Tests:** neue Datei, statische Klasse mit `public static IEnumerable<(string, Action)> Alle()` (wird automatisch gefunden); `Program.cs` nicht anfassen.
- **Datenversion** (`"version"` in `wirtschaft.json`) erhöht nur kern.
- **Messungen** (`messen`) nur kern und spiel-ki, jede nennt den `main`-Commit (Einzelheiten in ihren Agentendateien).
- Was sich nicht zurücknehmen lässt: erst fragen. Neue Designfrage: nicht raten, an den Teamleiter, mit einer empfohlenen Option.
- Commits deutsch, klein, mit der Zeile **`Agent: <name>`** (Arbeit von Gemini oder Strata: `Agent: <name> (Gemini)` bzw. `(Strata)`).

## Doku im selben PR

- `docs/ARCHITEKTUR.md` ergänzen; Neues fürs Spiel in das passende Kapitel des **Spielbuchs** `docs/spielbuch/<thema>.md` (Übersicht in `docs/spielbuch/README.md`; ein neues Thema bekommt eine neue Datei mit Zeile im Inhaltsverzeichnis). Die README ist nur die Startseite: ein großes neues Thema zusätzlich als Zeile in „Was das Spiel schon kann“ (mit Link aufs Kapitel), sonst nichts hineinschreiben. Gemeinsame Dateien nur an der passenden Stelle ändern, nicht ans Ende hängen.
- Entscheidungen und offene Punkte **nur in die eigene Bereichsdatei** `docs/bereiche/<bereich>.md`: Entscheidung als `### Thema (PR #…)` mit Tabelle unten unter „Entscheidungen“, Erledigtes aus „Offen“ löschen, Neues unten anhängen. **Keine Nummern**, keine Liste „Erledigt“. Braucht ein anderer Bereich etwas: „(von <Bereich>) …“ unter „Offen“ in dessen Datei. `docs/ENTSCHEIDUNGEN.md` ist Archiv. Ältere Entscheidungen je Bereich liegen in `docs/bereiche/archiv/<bereich>.md`, ältere Messungen in `docs/archiv/` – **Agenten lesen standardmäßig nur die aktuellen Bereichsdateien, das Archiv nur bei Bedarf** (Verweis steht im Kopf der Datei). Wird eine Bereichsdatei wieder groß (über etwa 300 Zeilen), verschiebt der Bereich abgeschlossene Abschnitte wörtlich ins Archiv.
- **Kein Asset ohne Protokolleintrag im selben PR:** Jede neue, geänderte oder gelöschte Datei unter `game/assets/` bekommt ihre Zeile in `docs/assets/protokoll.csv` (Werkzeug, Modell, Prompt nach `docs/assets/prompts/`, Unbelegtes „unbekannt“); `python3 tools/assets/protokoll_pruefen.py` muss grün sein (Einzelheiten `docs/assets/README.md`).

## Bauen, testen, ausprobieren

```bash
dotnet build Reichsgilde.sln
dotnet run -c Release --project tests/Reichsgilde.Core.Tests     # alle Tests
```

Ohne nuget.org genügt eine NuGet-Quelle auf `GodotSharp/Tools/nupkgs` der Godot-Version.
Klicktest: `tools/klicktest/lauf.sh` (Bedienung im Kopf der Datei; Godot unter `~/werkzeuge/godot/`, Xvfb unter `~/werkzeuge/xvfb/`), Startoptionen in `docs/STARTOPTIONEN.md` (neue dort eintragen). `opengl3` zeigt keine Texturen; für Texturen und Schatten Vulkan. Bilder selbst ansehen.
Lange Befehle (Messung, Blender, Klicktest) im Vordergrund mit Zeitlimit oder im selben Zug bis zum Ergebnis warten (`kill -0 <pid>`, Ausgabedatei); nie `pgrep -f` mit einem Wort aus der eigenen Befehlszeile.
