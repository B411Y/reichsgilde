# CLAUDE.md – Arbeitsregeln für Reichsgilde

Diese Datei liest jeder Claude-Chat zu Beginn automatisch. Hier stehen die festen Regeln. Die Aufgabe selbst kommt aus dem Prompt.
An diesem Projekt arbeiten **mehrere Chats gleichzeitig**. Die meisten Regeln unten dienen dazu, sich nicht gegenseitig in die Quere zu kommen.

## Das Projekt in Kürze

- **Reichsgilde** (Arbeitstitel): 2,5D-Wirtschaftssimulation im Stil von „Die Gilde Gold Edition“, düster-bodenständiges Heiliges Römisches Reich mit Menschen, Zwergen, Elben, Halblingen, Orks und Ogern.
- Godot 4.7.2 (.NET) mit C#/.NET 8, MIT-Lizenz. Hobby- und Lernprojekt eines Solo-Entwicklers mit wenig Programmiererfahrung: **Erkläre umsetzbar und lehrreich.**
- Aufbau: `src/Reichsgilde.Core` (Simulation, kennt Godot nicht) · `game/` (Godot: Darstellung und Oberfläche, liest den Kern nur) · `tests/` · `docs/` · Werte in `game/data/wirtschaft.json`.
- Weiterlesen: `README.md` (Stand, Steuerung), `docs/ARCHITEKTUR.md` (Aufbau), `docs/ENTSCHEIDUNGEN.md` (alle Grundsatzentscheidungen und Phasenplan), `docs/WIRTSCHAFTSMODELL.md` (Zahlen, Messungen je Beruf), `docs/LEVELDESIGN.md` (Kartenregeln, Städte).

## Zu Beginn jeder Aufgabe (Pflicht)

1. `git fetch origin` und `git log origin/main --oneline -15` – was haben die anderen Chats zuletzt gemergt?
2. Offene PRs ansehen, auch Entwürfe: `gh api "repos/B411Y/reichsgilde/pulls?state=open" --jq '.[] | "#\(.number) \(.draft) \(.head.ref) \(.title)"'`. Der Text eines Entwurfs-PR sagt, woran gerade gearbeitet wird und welche Dateien er anfasst. Fasst deine Aufgabe dieselben Dateien an: Nutzer fragen, bevor du anfängst.
3. Die Abschnitte in `docs/ENTSCHEIDUNGEN.md` lesen, die deine Aufgabe betreffen.

## Arbeitsweise

- **Eigener Branch von `main`**: `phaseN/thema` für Neues, `fix/thema` für Fehler, `infra/thema` für Werkzeuge und Abläufe.
- **Nach dem ersten Commit sofort einen Entwurfs-PR öffnen** (Vorlage: `.github/pull_request_template.md`): Aufgabe, Bereich, angefasste Dateien. So sehen die anderen Chats, was läuft. Den Text aktuell halten, wenn weitere Dateien dazukommen.
- Kleine, sinnvolle Commits. Commit- und PR-Texte auf Deutsch.
- **Vor dem Fertigmelden** `origin/main` in den Branch mergen (nicht rebasen), bauen, testen, dann den PR auf „bereit“ stellen und die CI abwarten (Linux, Windows, Export).
- **Doku im selben PR**: README und `docs/ARCHITEKTUR.md` ergänzen; neue Grundsatzentscheidungen als Tabellenzeile in `docs/ENTSCHEIDUNGEN.md` (Abschnitt 3a oder ein eigener Abschnitt am Ende), im Phasenplan dort eine Zeile `- PR #… (Phase) – Inhalt` unten an Liste 6a anhängen und den erledigten Punkt aus 6b löschen; Werte und Messungen eines neuen Berufs oder einer Balance-Änderung als neuer Abschnitt in `docs/WIRTSCHAFTSMODELL.md`.
- **Lieber anhängen als umschreiben**: In den beiden Protokollen nur neue Zeilen und Abschnitte hinzufügen oder einzelne Zeilen ändern. Dann lassen sich die PRs zweier Chats konfliktfrei mergen. Gibt es doch einen Konflikt, beide Einträge behalten.
- Vorher fragen: Designfragen mit Auswahl (je eine empfohlene Option), bevor gebaut wird. Was sich nicht zurücknehmen lässt, immer erst fragen.

## Bereiche (wer ist wofür zuständig)

Jeder Chat bekommt im Prompt einen Bereich. Dateien außerhalb des eigenen Bereichs nur klein und begründet ändern und im PR-Text nennen.

| Bereich | Dateien |
|---|---|
| **Kern** (Hauptchat) | `src/Reichsgilde.Core/**` (außer Ki/ und Buerger/), `tests/**`, `game/data/wirtschaft.json` |
| **Spiel-KI** | `src/Reichsgilde.Core/Ki/**` (KI-Häuser, `KiRivale.cs`), `src/Reichsgilde.Core/Buerger/**` (Bürger-KI), eigene Testdateien (`Ki*Tests.cs`, `Buerger*Tests.cs`) |
| **Darstellung** | `game/scripts/Stadtansicht*.cs`, Szenen, Modelle, Licht, Kamera |
| **Oberfläche** | Tafeln und Fenster in `game/scripts/*Anzeige.cs`, `Hauptmenue.cs`, `Hauptszene.cs` |
| **Leveldesign** | `docs/LEVELDESIGN.md` (Kartenregeln), später die Kartendateien je Stadt (Ort wird mit der ersten Karte festgelegt). Regeln mit Wirkung auf die Simulation entscheidet der Kern. |
| später z. B. Sound | wird hier ergänzt, sobald es den Chat gibt |

Nur der Kern-Chat ändert: `game/data/wirtschaft.json`, `Stadtwirtschaft.cs`, `Speicherstand.cs`. Braucht ein anderer Bereich dort etwas: im PR-Text beschreiben und den Nutzer fragen.
**Ausnahme Spiel-KI:** Der Spiel-KI-Chat darf dort kleine Andockstellen ergänzen und nennt jede im PR-Text: einen Aufruf in `Stadtwirtschaft` (Takt, Jahreswechsel, öffentliche Abfrage), einen **eigenen** Abschnitt in `wirtschaft.json` (Datenversion erhöhen), eigene Felder im `Speicherstand` (alte Spielstände müssen weiter laden), dazu **je Teilsystem des Kerns einen Faktor oder Aufruf**, über den die Bürger wirken (z. B. `Markt.NachfrageFaktor`, ein Faktor in `Kirchenwesen` oder `Gastwirtschaft`; ohne Abschnitt `buerger` bleibt er 1). Bestehende Werte und Regeln ändert nur der Kern.
Die Darstellung **liest den Kern nur** (öffentliche Abfragen). Fehlt eine Abfrage, kleine Ergänzung im Kern mit Test – im PR nennen.

## Feste Regeln

- **Nichts aus dem Original-Spiel** (keine Grafiken, Sounds, Musik, Texte, Modelle). Es dient nur als Vorbild für Mechaniken.
- Der **Mechanik-Katalog** und das **Design-Dossier** bleiben im privaten Claude-Projekt und kommen **nie** ins Repository.
- Spieltag 05:00–22:00 Uhr (1020 Ticks = 1 Spieljahr), Arbeitszeit 8–20 Uhr, Gauner ab 20 Uhr.
- Code-Bezeichner deutsch, Umlaute umschrieben (`Waehrung`), deutsche Kommentare. Antworten an den Nutzer auf Deutsch, kurz, ohne Emojis.
- Tests müssen grün bleiben. Nie einen Test abschalten, um grün zu werden.
- **Neue Tests**: eine neue Datei mit einer statischen Klasse und `public static IEnumerable<(string, Action)> Alle()` anlegen – sie wird automatisch gefunden. `Program.cs` nicht anfassen.
- **Messungen** (`messen`) machen nur der Kern-Chat und der Spiel-KI-Chat (für eigene Änderungen an KI-Häusern oder Bürgern). Jede Messung in `docs/WIRTSCHAFTSMODELL.md` nennt den `main`-Commit, gegen den gemessen wurde (`git rev-parse --short origin/main` nach dem Einmergen), z. B. „gemessen auf main 0ed29e9 + dieser PR“.

## Bauen, testen, ausprobieren

```bash
dotnet build Reichsgilde.sln
dotnet run -c Release --project tests/Reichsgilde.Core.Tests          # alle Tests
dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen 30 20   # Messung: 30 Jahre, 20 Startwerte
```

Klicktest: Godot 4.7.2 mono (Linux) von den offiziellen Godot-Releases auf GitHub laden, Projekt einmal importieren (`--headless --import --path game`), dann unter Xvfb starten (`--rendering-driver opengl3 --path game -- <Optionen>`) und Bildschirmfotos selbst ansehen. Für Schatten und Kantenglättung stattdessen `--rendering-driver vulkan` (Paket `mesa-vulkan-drivers`): Das ist der Forward+-Renderer des Spiels, `opengl3` zeigt dort andere Ergebnisse.
Startoptionen (nach `--`): `--beruf=<Beruf>`, `--startstunde=<Stunde>`, `--tempo=<1–32>`, `--innen=1` (eigene Werkstatt) oder `--innen=Rathaus|Dom`, `--raum=<Nummer>` (mit `--innen`: Raum der Raumleiste, 1 = erster), `--verwalten=1`, `--auswahl=<Kennung>`, `--drehung=<1–3>`, `--vermoegen=<Gulden>`, `--rat=1`, `--familie=1`, `--gericht=1`, `--hinterzimmer=1`, `--markt=1`, `--bericht=1`, `--einstellungen=1`, `--kanten=<Aus|Fxaa|Smaa|Taa|Msaa2|Msaa4|Msaa8>` (nur für diesen Start), `--laden=<datei.json>` (aus `~/.local/share/godot/app_userdata/Reichsgilde/spielstaende`).
Ohne Zugang zu nuget.org genügt eine NuGet-Quelle auf den Ordner `GodotSharp/Tools/nupkgs` der heruntergeladenen Godot-Version.
