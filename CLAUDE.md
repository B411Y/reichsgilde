# CLAUDE.md – Arbeitsregeln für Reichsgilde

Diese Datei liest jeder Claude-Chat zu Beginn automatisch. Hier stehen die festen Regeln. Die Aufgabe selbst kommt aus dem Prompt.
An diesem Projekt arbeiten **mehrere Chats gleichzeitig**. Die meisten Regeln unten dienen dazu, sich nicht gegenseitig in die Quere zu kommen.

## Das Projekt in Kürze

- **Reichsgilde** (Arbeitstitel): 2,5D-Wirtschaftssimulation im Stil von „Die Gilde Gold Edition“, düster-bodenständiges Heiliges Römisches Reich mit Menschen, Zwergen, Elben, Halblingen, Orks und Ogern.
- Godot 4.7.2 (.NET) mit C#/.NET 8, MIT-Lizenz. Hobby- und Lernprojekt eines Solo-Entwicklers mit wenig Programmiererfahrung: **Erkläre umsetzbar und lehrreich.**
- Aufbau: `src/Reichsgilde.Core` (Simulation, kennt Godot nicht) · `game/` (Godot: Darstellung und Oberfläche, liest den Kern nur) · `tests/` · `docs/` · Werte in `game/data/wirtschaft.json`.
- Weiterlesen: `README.md` (Stand, Steuerung), `docs/ARCHITEKTUR.md` (Aufbau), `docs/ENTSCHEIDUNGEN.md` (alle Grundsatzentscheidungen und Phasenplan), `docs/WIRTSCHAFTSMODELL.md` (Zahlen, Messungen je Beruf).

## Zu Beginn jeder Aufgabe (Pflicht)

1. `git fetch origin` und `git log origin/main --oneline -15` – was haben die anderen Chats zuletzt gemergt?
2. Offene PRs ansehen, auch Entwürfe: `gh api "repos/B411Y/reichsgilde/pulls?state=open" --jq '.[] | "#\(.number) \(.draft) \(.head.ref) \(.title)"'`. Der Text eines Entwurfs-PR sagt, woran gerade gearbeitet wird und welche Dateien er anfasst. Fasst deine Aufgabe dieselben Dateien an: Nutzer fragen, bevor du anfängst.
3. Die Abschnitte in `docs/ENTSCHEIDUNGEN.md` lesen, die deine Aufgabe betreffen.

## Arbeitsweise

- **Eigener Branch von `main`**: `phaseN/thema` für Neues, `fix/thema` für Fehler, `infra/thema` für Werkzeuge und Abläufe.
- **Nach dem ersten Commit sofort einen Entwurfs-PR öffnen** (Vorlage: `.github/pull_request_template.md`): Aufgabe, Bereich, angefasste Dateien. So sehen die anderen Chats, was läuft. Den Text aktuell halten, wenn weitere Dateien dazukommen.
- Kleine, sinnvolle Commits. Commit- und PR-Texte auf Deutsch.
- **Vor dem Fertigmelden** `origin/main` in den Branch mergen (nicht rebasen), bauen, testen, dann den PR auf „bereit“ stellen und die CI abwarten (Linux, Windows, Export).
- **Doku im selben PR**: README und `docs/ARCHITEKTUR.md` ergänzen; neue Grundsatzentscheidungen als Tabellenzeile in `docs/ENTSCHEIDUNGEN.md` (Abschnitt 3a oder ein eigener Abschnitt am Ende), den Phasenplan dort abhaken; Werte und Messungen eines neuen Berufs oder einer Balance-Änderung als neuer Abschnitt in `docs/WIRTSCHAFTSMODELL.md`.
- **Lieber anhängen als umschreiben**: In den beiden Protokollen nur neue Zeilen und Abschnitte hinzufügen oder einzelne Zeilen ändern. Dann lassen sich die PRs zweier Chats konfliktfrei mergen. Gibt es doch einen Konflikt, beide Einträge behalten.
- Vorher fragen: Designfragen mit Auswahl (je eine empfohlene Option), bevor gebaut wird. Was sich nicht zurücknehmen lässt, immer erst fragen.

## Bereiche (wer ist wofür zuständig)

Jeder Chat bekommt im Prompt einen Bereich. Dateien außerhalb des eigenen Bereichs nur klein und begründet ändern und im PR-Text nennen.

| Bereich | Dateien |
|---|---|
| **Kern** (Hauptchat) | `src/Reichsgilde.Core/**`, `tests/**`, `game/data/wirtschaft.json` |
| **Darstellung** | `game/scripts/Stadtansicht*.cs`, Szenen, Modelle, Licht, Kamera |
| **Oberfläche** | Tafeln und Fenster in `game/scripts/*Anzeige.cs`, `Hauptmenue.cs`, `Hauptszene.cs` |
| später z. B. Sound, Spiel-KI | wird hier ergänzt, sobald es den Chat gibt |

Nur der Kern-Chat ändert: `game/data/wirtschaft.json`, `Stadtwirtschaft.cs`, `Speicherstand.cs`, `KiRivale.cs`. Braucht ein anderer Bereich dort etwas: im PR-Text beschreiben und den Nutzer fragen.
Die Darstellung **liest den Kern nur** (öffentliche Abfragen). Fehlt eine Abfrage, kleine Ergänzung im Kern mit Test – im PR nennen.

## Feste Regeln

- **Nichts aus dem Original-Spiel** (keine Grafiken, Sounds, Musik, Texte, Modelle). Es dient nur als Vorbild für Mechaniken.
- Der **Mechanik-Katalog** und das **Design-Dossier** bleiben im privaten Claude-Projekt und kommen **nie** ins Repository.
- Spieltag 05:00–22:00 Uhr (1020 Ticks = 1 Spieljahr), Arbeitszeit 8–20 Uhr, Gauner ab 20 Uhr.
- Code-Bezeichner deutsch, Umlaute umschrieben (`Waehrung`), deutsche Kommentare. Antworten an den Nutzer auf Deutsch, kurz, ohne Emojis.
- Tests müssen grün bleiben. Nie einen Test abschalten, um grün zu werden.

## Bauen, testen, ausprobieren

```bash
dotnet build Reichsgilde.sln
dotnet run -c Release --project tests/Reichsgilde.Core.Tests          # alle Tests
dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen 30 20   # Messung: 30 Jahre, 20 Startwerte
```

Klicktest: Godot 4.7.2 mono (Linux) von den offiziellen Godot-Releases auf GitHub laden, Projekt einmal importieren (`--headless --import --path game`), dann unter Xvfb starten (`--rendering-driver opengl3 --path game -- <Optionen>`) und Bildschirmfotos selbst ansehen.
Startoptionen (nach `--`): `--beruf=<Beruf>`, `--startstunde=<Stunde>`, `--tempo=<1–32>`, `--innen=1`, `--verwalten=1`, `--auswahl=<Kennung>`, `--drehung=<1–3>`, `--vermoegen=<Gulden>`, `--rat=1`, `--familie=1`, `--gericht=1`, `--hinterzimmer=1`, `--markt=1`, `--bericht=1`, `--laden=<datei.json>` (aus `~/.local/share/godot/app_userdata/Reichsgilde/spielstaende`).
Ohne Zugang zu nuget.org genügt eine NuGet-Quelle auf den Ordner `GodotSharp/Tools/nupkgs` der heruntergeladenen Godot-Version.
