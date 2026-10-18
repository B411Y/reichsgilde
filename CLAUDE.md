# CLAUDE.md – Arbeitsregeln für Reichsgilde

Diese Datei liest jeder Claude-Chat zu Beginn automatisch. Hier stehen die festen Regeln. Die Aufgabe selbst kommt aus dem Prompt.
An diesem Projekt arbeiten **mehrere Chats gleichzeitig**. Die meisten Regeln unten dienen dazu, sich nicht gegenseitig in die Quere zu kommen.

## Das Projekt in Kürze

- **Reichsgilde** (Arbeitstitel): 2,5D-Wirtschaftssimulation im Stil von „Die Gilde Gold Edition“, düster-bodenständiges Heiliges Römisches Reich mit Menschen, Zwergen, Elben, Halblingen, Orks und Ogern.
- **Grafikstil: kolorierter Holzschnitt** des Spätmittelalters (Vorbild: Schedelsche Weltchronik, um 1480) – schwarzer Umriss, Schraffur im Schatten, von Hand aufgetragene Farbe, die leicht neben den Linien sitzt. Gilt für das **ganze Spiel**: Oberfläche, Icons, Wappen, Bilder und alle weiteren Grafiken. Keine Pixel-Art, kein moderner Flat-Stil.
- Godot 4.7.2 (.NET) mit C#/.NET 8, Code unter MIT, Grafiken, Modelle und Texte unter CC BY 4.0. Hobby- und Lernprojekt eines Solo-Entwicklers mit wenig Programmiererfahrung: **Erkläre umsetzbar und lehrreich.**
- Aufbau: `src/Reichsgilde.Core` (Simulation, kennt Godot nicht) · `game/` (Godot: Darstellung und Oberfläche, liest den Kern nur) · `tests/` · `docs/` · Werte in `game/data/wirtschaft.json`.
- Weiterlesen: `README.md` (Stand, Steuerung), `docs/ARCHITEKTUR.md` (Aufbau), `docs/bereiche/<bereich>.md` (offene Punkte und neue Entscheidungen je Bereich), `docs/ENTSCHEIDUNGEN.md` (Archiv der Entscheidungen bis 16.10.2026, Phasen-Übersicht), `docs/WIRTSCHAFTSMODELL.md` (Zahlen, Messungen je Beruf), `docs/LEVELDESIGN.md` (Kartenregeln, Städte).

## Zu Beginn jeder Aufgabe (Pflicht)

1. `git fetch origin` und `git log origin/main --oneline -15` – was haben die anderen Chats zuletzt gemergt?
2. Offene PRs ansehen, auch Entwürfe: `gh api "repos/B411Y/reichsgilde/pulls?state=open" --jq '.[] | "#\(.number) \(.draft) \(.head.ref) \(.title)"'`. Der Text eines Entwurfs-PR sagt, woran gerade gearbeitet wird und welche Dateien er anfasst. Fasst deine Aufgabe dieselben Dateien an: Nutzer fragen, bevor du anfängst.
3. Die eigene Bereichsdatei `docs/bereiche/<bereich>.md` lesen (Offen, Entscheidungen) und die Abschnitte im Archiv `docs/ENTSCHEIDUNGEN.md`, die deine Aufgabe betreffen.
4. Auf dem eigenen Branch weiterarbeiten? Erst `git pull` – die Action „PRs aktuell halten“ kann inzwischen main eingemergt haben.

## Arbeitsweise

- **Eigener Branch von `main`**: `phaseN/thema` für Neues, `fix/thema` für Fehler, `infra/thema` für Werkzeuge und Abläufe.
- **Nach dem ersten Commit sofort einen Entwurfs-PR öffnen** (Vorlage: `.github/pull_request_template.md`): Aufgabe, Bereich, angefasste Dateien. So sehen die anderen Chats, was läuft. Den Text aktuell halten, wenn weitere Dateien dazukommen.
- Kleine, sinnvolle Commits. Commit- und PR-Texte auf Deutsch. **PR-Titel beginnen mit dem Bereich**: „Kern: …“, „Spiel-KI: …“, „Darstellung: …“, „Oberfläche: …“, „Leveldesign: …“ – arbeiten mehrere Bereiche mit, alle („Kern, Oberfläche, Darstellung: …“).
- **Ein PR pro Funktion:** Eine Funktion kommt als **ein** PR zum Nutzer, auch wenn mehrere Bereiche daran arbeiten. Er prüft und mergt dann einmal statt viermal. Kleinkram (Doku-Nachträge, bestätigte Entscheidungen, Aufräumen) fährt im nächsten Funktions-PR mit. Einen eigenen kleinen PR gibt es nur für Dringendes, etwa einen Fehler, der andere Chats blockiert, oder eine kaputte CI. Der PR-Text beginnt mit einer **Übersicht für den Nutzer**: was das Spiel jetzt Neues kann, wie man es ausprobiert (Startoptionen) und was der Klicktest gezeigt hat.
- **Vor dem Fertigmelden** `origin/main` in den Branch mergen (nicht rebasen), bauen, testen, dann den PR auf „bereit“ stellen und die CI abwarten (Linux, Windows, Export).
- **Doku im selben PR**: README und `docs/ARCHITEKTUR.md` ergänzen. In der README gehört Neues in den passenden aufklappbaren Abschnitt des **Spielbuchs** (Leerzeile nach `</summary>` und vor `</details>`), ein großes neues Thema bekommt dazu eine Zeile in der Tabelle „Was das Spiel schon kann“. Entscheidungen und offene Punkte **nur in die eigene Bereichsdatei** `docs/bereiche/<bereich>.md`: neue Entscheidung als `### Thema (PR #…)` mit Tabelle unten unter „Entscheidungen“ anhängen, erledigten Punkt aus „Offen“ löschen, neue offene Punkte unten anhängen. **Keine Nummern** (weder Abschnitte noch Paragraphen) – zwei Chats würden sonst dieselbe Nummer nehmen. Eine Liste „Erledigt“ gibt es nicht mehr: Das zeigt GitHub. Braucht ein anderer Bereich etwas, eine Zeile „(von <Bereich>) …“ unter „Offen“ in dessen Datei.
- **Messungen**: Kern in `docs/WIRTSCHAFTSMODELL.md` (nur der Kern schreibt dort), Spiel-KI in `docs/bereiche/spiel-ki.md` unter „Messungen“.
- **Gemeinsame Dateien**: In README, ARCHITEKTUR und CLAUDE.md nur an der Stelle ändern, die zum eigenen Thema gehört – nicht ans Dateiende anhängen, wenn es einen passenden Abschnitt gibt. `docs/ENTSCHEIDUNGEN.md` ist Archiv; nur der Kern pflegt dort die Phasen-Übersicht.
- **Konflikte**: Die Action „PRs aktuell halten“ mergt nach jedem Merge auf main den neuen Stand in alle fertigen PRs (nicht in Entwürfe); in `docs/bereiche/*.md` behält Git dabei beide Einträge. Bleibt ein echter Konflikt, bekommt der PR das Label „Konflikt mit main“ – dann main von Hand einmergen (nicht rebasen), beide Einträge behalten.
- Vorher fragen: Designfragen mit Auswahl (je eine empfohlene Option), bevor gebaut wird. Was sich nicht zurücknehmen lässt, immer erst fragen.

## Bereiche (wer ist wofür zuständig)

Jeder Chat bekommt im Prompt einen Bereich. Dateien außerhalb des eigenen Bereichs nur klein und begründet ändern und im PR-Text nennen.

| Bereich | Dateien |
|---|---|
| **Kern** (Hauptchat) | `src/Reichsgilde.Core/**` (außer Ki/ und Buerger/), `tests/**`, `game/data/wirtschaft.json` |
| **Spiel-KI** | `src/Reichsgilde.Core/Ki/**` (KI-Häuser, `KiRivale.cs`), `src/Reichsgilde.Core/Buerger/**` (Bürger-KI), eigene Testdateien (`Ki*Tests.cs`, `Buerger*Tests.cs`) |
| **Darstellung** | `game/scripts/Stadtansicht*.cs`, Szenen, Modelle, Licht, Kamera |
| **Oberfläche** | Tafeln und Fenster in `game/scripts/*Anzeige.cs`, `Hauptmenue.cs`, `Hauptszene.cs` |
| **Leveldesign** | `docs/LEVELDESIGN.md` (Kartenregeln), `tools/karten/**` (Kartenwerkzeug, ein Skript je Stadt), `game/data/karten/*.json` (Kartendateien, nur vom Werkzeug erzeugt), `docs/karten/*.svg` (Vorschau). Regeln mit Wirkung auf die Simulation entscheidet der Kern. |
| später z. B. Sound | wird hier ergänzt, sobald es den Chat gibt |

Nur der Kern-Chat ändert: `game/data/wirtschaft.json`, `Stadtwirtschaft.cs`, `Speicherstand.cs`. Braucht ein anderer Bereich dort etwas: im PR-Text beschreiben und den Nutzer fragen.
**Ausnahme Spiel-KI:** Der Spiel-KI-Chat darf dort kleine Andockstellen ergänzen und nennt jede im PR-Text: einen Aufruf in `Stadtwirtschaft` (Takt, Jahreswechsel, öffentliche Abfrage), einen **eigenen** Abschnitt in `wirtschaft.json` (die Datenversion erhöht nur der Kern), eigene Felder im `Speicherstand` (alte Spielstände müssen weiter laden), dazu **je Teilsystem des Kerns einen Faktor oder Aufruf**, über den die Bürger wirken (z. B. `Markt.NachfrageFaktor`, ein Faktor in `Kirchenwesen` oder `Gastwirtschaft`; ohne Abschnitt `buerger` bleibt er 1). Bestehende Werte und Regeln ändert nur der Kern.
Die Darstellung **liest den Kern nur** (öffentliche Abfragen). Fehlt eine Abfrage, kleine Ergänzung im Kern mit Test – im PR nennen.

## Teamarbeit mit Teamleiter

Gilt, wenn ein Chat als **Teamleiter** Teammitglieder (Agenten) startet. Alle übrigen Regeln gelten für jedes Teammitglied wie für einen eigenen Chat.

- **Zuschnitt nach Größe:** Kleine Funktionen bekommen zwei Mitglieder, „kern“ und „spiel“ (Oberfläche und Darstellung zusammen). Nur große Themen mit viel unabhängiger Arbeit bekommen ein Mitglied je Bereich. Jede zusätzliche Grenze kostet Abstimmung.
- **Ein Funktions-Branch, ein PR:** Erst die Designfragen an den Nutzer. Dann legt der Teamleiter den Funktions-Branch `phaseN/thema` an und öffnet darauf den Entwurfs-PR – den einzigen PR der Funktion. Teammitglieder öffnen **keine eigenen PRs**: Jedes arbeitet auf `phaseN/thema-<name>`, pusht und meldet fertig, und der Teamleiter mergt den Branch in den Funktions-Branch (mergen, nicht rebasen).
- **Reihenfolge:** Zuerst baut der Kern Befehl, Abfragen und Tests und meldet die genauen Signaturen. Der Teamleiter mergt das in den Funktions-Branch. Erst danach starten die anderen und zweigen vom Funktions-Branch ab.
- **Getrennte Ordner:** Jedes Mitglied hat einen eigenen Worktree `../rg-<name>` und einen eigenen Unterordner im Notizordner (`<Notizordner>/<name>/`), z. B. für den PR-Text. Ein gemeinsamer Ordner führt zu überschriebenen Dateien.
- **Entscheidungen unterwegs:** Taucht eine neue Designfrage auf, entscheidet der Teamleiter **einmal** (oder fragt den Nutzer) und schickt **eine** endgültige Nachricht an alle Betroffenen. Eine getroffene Entscheidung nicht nachträglich umwerfen: Nachrichten überholen sich, und dann baut jemand den alten Stand zurück.
- **Nachrichten:** Vorschläge an ein anderes Mitglied gehen **über den Teamleiter**, nicht direkt untereinander. Zwischenmeldungen nur bei einer Blockade oder einer fertigen Schnittstelle, sonst nur der Abschlussbericht.
- **Nach „fertig“:** Ein Mitglied pusht nur noch auf Anweisung des Teamleiters. Sind alle Teile im Funktions-Branch, macht der Teamleiter dort den gemeinsamen Klicktest, schreibt die Übersicht für den Nutzer in den PR-Text, stellt den PR auf „bereit“ und meldet dem Nutzer „mergebereit: #…“. Nach `main` mergt nur der Nutzer.
- **Modelle:** Für den Kern und die Spiel-KI Opus: Simulation, Spielstände und Tests vertragen keine Fehler. Für Oberfläche und Darstellung Sonnet, denn sie folgen vorhandenen Mustern, und Tests, CI und Klicktest fangen Fehler ab. Für Kleinkram (Doku-Korrektur, main einmergen, CI abwarten) und zum Durchsuchen des Codes vor den Designfragen Haiku. Wird die Arbeit eines Bereichs mit einem kleineren Modell schlechter, kommt er zurück auf Opus.
- **Aufräumen:** Am Ende beendet der Teamleiter die Teammitglieder, entfernt die Worktrees und Mitglieder-Branches der gemergten Funktion und fasst für den Nutzer zusammen, was offen bleibt.

## Feste Regeln

- **Mergen nur durch den Nutzer:** Kein Chat mergt nach `main` und keiner schaltet „Auto-merge“ ein (auch nicht mit `gh pr merge --auto`). Der Nutzer prüft den PR und schaltet Auto-merge selbst ein; `main` verlangt die drei CI-Prüfungen.
- **Nichts aus dem Original-Spiel** (keine Grafiken, Sounds, Musik, Texte, Modelle). Es dient nur als Vorbild für Mechaniken.
- Der **Mechanik-Katalog** und das **Design-Dossier** bleiben im privaten Claude-Projekt und kommen **nie** ins Repository.
- Spieltag 05:00–22:00 Uhr (1020 Ticks = 1 Spieljahr), Arbeitszeit 8–20 Uhr, Gauner ab 20 Uhr.
- Code-Bezeichner deutsch, Umlaute umschrieben (`Waehrung`), deutsche Kommentare. Antworten an den Nutzer auf Deutsch, kurz, ohne Emojis.
- Tests müssen grün bleiben. Nie einen Test abschalten, um grün zu werden.
- **Neue Tests**: eine neue Datei mit einer statischen Klasse und `public static IEnumerable<(string, Action)> Alle()` anlegen – sie wird automatisch gefunden. `Program.cs` nicht anfassen.
- **Datenversion** (`"version"` in `wirtschaft.json`) erhöht nur der Kern – sonst ändern zwei PRs dieselbe Zeile.
- **Messungen** (`messen`) machen nur der Kern-Chat und der Spiel-KI-Chat (für eigene Änderungen an KI-Häusern oder Bürgern). Jede Messung nennt den `main`-Commit, gegen den gemessen wurde (`git rev-parse --short origin/main` nach dem Einmergen), z. B. „gemessen auf main 0ed29e9 + dieser PR“.

## Bauen, testen, ausprobieren

```bash
dotnet build Reichsgilde.sln
dotnet run -c Release --project tests/Reichsgilde.Core.Tests          # alle Tests
dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen 30 20   # Messung: 30 Jahre, 20 Startwerte
dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen 30 20 greybox   # dieselbe Messung mit der Greybox-Karte (Bauen, Leerstand, Neubau)
```

Klicktest: Godot 4.7.2 mono (Linux) von den offiziellen Godot-Releases auf GitHub laden, Projekt einmal importieren (`--headless --import --path game`), dann unter Xvfb starten (`--rendering-driver opengl3 --path game -- <Optionen>`) und Bildschirmfotos selbst ansehen. Für Schatten und Kantenglättung stattdessen `--rendering-driver vulkan` (Paket `mesa-vulkan-drivers`): Das ist der Forward+-Renderer des Spiels, `opengl3` zeigt dort andere Ergebnisse.
Auf dem Entwicklungsrechner (ohne root) liegt das schon bereit, also erst nachsehen, bevor du etwas lädst. Godot liegt unter `~/werkzeuge/godot/`, ein Xvfb ohne root unter `~/werkzeuge/xvfb/`. Ein Bildschirmfoto macht `~/werkzeuge/xvfb/skripte/klicktest.sh game <bild.png> <Wartesekunden> <Startoptionen>`. Klicks davor gibt man mit `KLICKS="x,y x,y"` vor.
Startoptionen (nach `--`): `--beruf=<Beruf>`, `--startstunde=<Stunde>`, `--tempo=<1–32>`, `--innen=1` (eigene Werkstatt) oder `--innen=Rathaus|Dom|wohnhaus:<Inhaber>`, `--raum=<Nummer>` (mit `--innen`: Raum der Raumleiste, 1 = erster), `--verwalten=1`, `--auswahl=<Kennung>`, `--drehung=<1–3>`, `--vermoegen=<Gulden>`, `--rat=1`, `--familie=1`, `--gericht=1`, `--hinterzimmer=1`, `--markt=1`, `--karren=<Typen>` (der eigenen Werkstatt Karren dazukaufen, z. B. `Ochsenkarren,Kriegskarren`, mit `--vermoegen=9000`), `--verkaufen=1` (Rückfrage zum Verkauf der eigenen Werkstatt; `=2` kauft vorher eine Grube dazu und lässt bis Mittag laufen, mit `--vermoegen=20000`), `--bericht=1`, `--einstellungen=1`, `--wegenetz=1` (Gassennetz der Passanten einblenden), `--kanten=<Aus|Fxaa|Smaa|Taa|Msaa2|Msaa4|Msaa8>` (nur für diesen Start), `--schatten=<Niedrig|Mittel|Hoch>` (Schattenqualität, nur für diesen Start), `--bloom=<An|Aus>` und `--bloomstaerke=<0–100>` (Bloom, nur für diesen Start), `--ssao=<Aus|Niedrig|Mittel|Hoch>` (Umgebungsverdeckung), `--ssil=<An|Aus>` (indirektes Licht), `--fsr=<An|Aus>` (FSR 2.2) und `--renderaufloesung=<25–100>` (Renderauflösung bei FSR in Prozent), alle nur für diesen Start, `--laden=<datei.json>` (aus `~/.local/share/godot/app_userdata/Reichsgilde/spielstaende`).
Zum Bauen und Leerstand: `--bauen=1` (Baumodus mit Bautafel; `--bauen=wohnhaus:<Stufe 0–4>` oder `--bauen=werkstatt:<Betriebsart>` wählt gleich das Gebäude), `--bauort=<x>,<z>,<Drehung 0–3>` (der Umriss sitzt an dieser Stelle der Stadt in Metern, bis die Maus bewegt wird; Probestellen stehen unter `probeplan` in `greybox.json`), `--bauklick=1` (dort die Rückfrage öffnen; `=2` gleich bauen), `--aufsteigen=1` (Wohnhaus zum Bürgerhaus ausbauen und zum Bürger aufsteigen, damit Werkstätten erlaubt sind; mit `--vermoegen=30000`), `--spulen=<Ticks>` (so viele Spielminuten auf einen Schlag ablaufen lassen, `1100` = Bauzeit eines Spieljahres), `--leerstand=kaufen|abreissen` (mit `--auswahl=wohnhaus:Haus_Eisenbart`, Leerzeichen als `_`: Rückfrage zum leeren Haus öffnen; mit `--bauklick=2` gleich ausführen), `--speichern=<datei.json>` (die Partie nach `--bauklick` und `--spulen` speichern). Ein leeres Haus entsteht durch Bearbeiten eines Spielstands: mit `--speichern` einen Stand schreiben, in der Datei bei einem KI-Konto `IstBankrott` auf `true` und `WohnhausStufe` setzen, mit `--laden` starten. Kamera und Tafeln für Bildschirmfotos: `--blick=<x>,<z>,<Zoom>`, `--ui=0`. Tipp: Mit `XDG_DATA_HOME=<eigener Ordner>` vor dem Aufruf landen die Spielstände des Klicktests nicht im echten Spielstand-Ordner (der Auto-Stand wird bei jedem Jahreswechsel überschrieben).
Ohne Zugang zu nuget.org genügt eine NuGet-Quelle auf den Ordner `GodotSharp/Tools/nupkgs` der heruntergeladenen Godot-Version.
