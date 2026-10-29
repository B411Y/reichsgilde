# Abläufe des Teamleiters

Gilt für die Hauptsitzung, die als **Teamleiter** Mitglieder startet. Die Regeln der `CLAUDE.md` gelten zusätzlich. Mitglieder lesen diese Datei nicht; was sie brauchen, steht in ihrer Datei unter `.claude/agents/`.

## Rolle

- **Teamleiter ändert keine Dateien** (Entscheidung des Nutzers, 28.10.2026): keine Dateien im Repository, auch keine kleinen, und kein Commit. Jede Änderung geht als Auftrag an ein Mitglied. Ausnahme: eigene Notizen außerhalb des Repositorys. Abgesichert durch den Hook in `.claude/settings.json` (`.claude/hooks/teamleiter_sperre.py`; fängt Versehen, keine Absicht).
- Beim Teamleiter bleiben: Designfragen, Zuschnitt und Aufträge, Entscheidungen unterwegs, Berichte und Bilder prüfen, Status an den Nutzer, der Merge-Befehl nach `main`, Aufräumen und der **Grafik-PC** (Startdienst; nur der Teamleiter, den Schlüssel bekommt kein Mitglied; Erzeugtes nur in den Notizordner, ein Mitglied baut es ein).
- **Zu Beginn:** `git fetch origin`, `git log origin/main --oneline -15` und offene PRs samt Entwürfen ansehen: `gh api "repos/B411Y/reichsgilde/pulls?state=open" --jq '.[] | "#\(.number) \(.draft) \(.head.ref) \(.title)"'`. Fasst eine Aufgabe Dateien eines offenen PR an: erst den Nutzer fragen.
- **Designfragen** an den Nutzer mit Auswahl (je eine empfohlene Option), bevor gebaut wird. Was sich nicht zurücknehmen lässt, immer erst fragen.

## Team aufstellen

- **Mitglieder:** Rollen in `.claude/agents/<name>.md` (kern, spiel-ki, darstellung, oberflaeche, leveldesign, sound, integration, pruefer, infra, suche). Aufruf mit `subagent_type` = Name; Modell und Aufwand im Aufruf überschreiben die Vorgabe der Datei.
- **Zuschnitt nach Größe:** Kleine Funktionen bekommen zwei Mitglieder, „kern“ und „spiel“ (Oberfläche und Darstellung zusammen). Nur große Themen mit viel unabhängiger Arbeit bekommen ein Mitglied je Bereich. Jede zusätzliche Grenze kostet Abstimmung.
- **Ein Funktions-Branch, ein PR:** Erst die Designfragen an den Nutzer. Dann entsteht der Funktions-Branch `phaseN/thema` (`fix/thema` für Fehler, `infra/thema` für Werkzeuge und Abläufe) mit seinem Entwurfs-PR – dem einzigen PR der Funktion. Weil der Teamleiter nicht pusht, legt das Integrations-Mitglied Branch und PR in seinem Auftrag an. Mitglieder öffnen **keine eigenen PRs**: Jedes arbeitet auf `phaseN/thema-<name>`, pusht und meldet fertig; das Integrations-Mitglied mergt in den Funktions-Branch (mergen, nicht rebasen).
- **Ein PR pro Funktion:** Der Nutzer prüft einmal statt viermal. Kleinkram (Doku-Nachträge, bestätigte Entscheidungen, Aufräumen) fährt im nächsten Funktions-PR mit. Einen eigenen kleinen PR gibt es nur für Dringendes, etwa einen Fehler, der andere blockiert, oder eine kaputte CI.
- **Doku-Ablage:** Spielbuch-Kapitel liegen unter `docs/spielbuch/`, ältere Entscheidungen unter `docs/bereiche/archiv/`. Beim Einmergen älterer Branches gehört Neues, das noch in die alte README-Spielbuch-Struktur oder ans Ende einer Bereichsdatei kam, an den neuen Ort (Kapitel bzw. Abschnitt „Entscheidungen“ der aktuellen Datei).
- **Reihenfolge:** Zuerst baut kern Befehl, Abfragen und Tests und meldet die genauen Signaturen. Das Integrations-Mitglied mergt das in den Funktions-Branch. Erst danach starten die anderen und zweigen vom Funktions-Branch ab.
- **Getrennte Ordner:** Jedes Mitglied hat einen eigenen Worktree `../rg-<name>` und einen eigenen Unterordner im Notizordner (`<Notizordner>/<name>/`). Ein gemeinsamer Ordner führt zu überschriebenen Dateien.
- **Modelle:** kern und spiel-ki Opus (Simulation, Spielstände und Tests vertragen keine Fehler). Oberfläche und Darstellung Sonnet (sie folgen vorhandenen Mustern; Tests, CI und Klicktest fangen Fehler ab). Kleinkram (Doku-Korrektur, main einmergen, CI abwarten) und Codesuche vor den Designfragen Haiku. Wird die Arbeit eines Bereichs mit einem kleineren Modell schlechter, kommt er zurück auf Opus.
- **Aufwand** (Entscheidung des Nutzers, 25.10.2026, erweitert 28.10.2026; beim Start mit `effort` setzen): Kern, Spiel-KI, Spielstand, 3D-Modelle (Blender) und Shader **extra hoch** (`xhigh`); übrige Grafik (Wege, Texturen, Kartenskripte) **hoch**; einfache Oberfläche, Doku und Aufräumen **mittel**. **Maximal** nur für einzelne festgefahrene Fehlersuchen. Der Teamleiter läuft auf mittel; der Nutzer stellt ihn bei Designfragen oder Fehlersuche selbst höher.
- **Hilfsagenten Gemini und Strata** (Entscheidung des Nutzers, 26.10.2026): **Gemini** (Antigravity CLI `agy`, Standardmodell `gemini-3.8-flash-high`) für mittelgroße, klar abgegrenzte Arbeit: Oberfläche nach vorhandenem Muster, Doku-Abgleiche, Kartenskripte, kleine Darstellungsfehler, Code durchsuchen. **Strata** (lokales Modell) für Hilfsarbeiten: Texte und Namen, Bilder vorsortieren, Protokolle zusammenfassen, Einzeiler. Kern, Spiel-KI, Spielstand, schwierige Grafik und alle Entscheidungen bleiben bei Claude. Beide laufen als Hintergrundbefehl in einem eigenen Worktree, bearbeiten nur Dateien (keine Befehle ohne Rückfrage, kein push); das Integrations-Mitglied baut, testet, committet, prüft und nennt im PR-Text, was von ihnen stammt (Aufruf in `.claude/agents/integration.md`). Anmeldung in den Notizen des Teamleiters.

## Unterwegs

- **Entscheidungen unterwegs:** Taucht eine Designfrage auf, entscheidet der Teamleiter **einmal** (oder fragt den Nutzer) und schickt **eine** endgültige Nachricht an alle Betroffenen. Eine getroffene Entscheidung nicht nachträglich umwerfen: Nachrichten überholen sich, und dann baut jemand den alten Stand zurück.
- **Nachrichten:** Vorschläge an ein anderes Mitglied gehen **über den Teamleiter**, nicht direkt untereinander. Zwischenmeldungen nur bei einer Blockade oder einer fertigen Schnittstelle, so kurz wie möglich.
- **Abschlussbericht in fünf Zeilen** (Wunsch des Nutzers, 25.10.2026, Tokens sparen): 1. Branch und Commit, 2. was gebaut ist (ein Satz), 3. Tests und Klicktest (Zahlen, Fehler), 4. selbst getroffene Entscheidungen und Offenes, 5. Ordner mit den Bildern. Einzelheiten stehen in Doku und Commit; bei Bedarf nachfragen.
- **Leerlauf prüfen:** Solange ein Team läuft, alle 30 Minuten prüfen, ob ein Mitglied untätig ist, obwohl seine Ausgabe fertig ist.
- **Prüf-Agent bei Kern-PRs** (28.10.2026): Vor „bereit“ liest ein unabhängiger `pruefer` (extra hoch) den Diff von Kern, Spiel-KI und Tests – er bekommt nur Code und Prüfauftrag, nicht die Einschätzung des Mitglieds. Befunde behebt das Mitglied vor dem Merge.

## Sorgfalt

Wunsch des Nutzers, 21.10.2026: Er erwartet fehlerfreie Arbeit, Fehler haben Konsequenzen. Diese Prüfschritte sind Pflicht (ausgeführt vom Integrations-Mitglied, der Teamleiter achtet darauf):

- **Vor dem Löschen eines Mitglieder-Branches:** `git merge-base --is-ancestor origin/<mitglieder-branch> <funktions-branch>`. Fehlt der Commit, erst einmergen.
- **Nach jedem Merge** die betroffenen Tabellen in `docs/bereiche/*.md` auf doppelte Zeilen und Leerzeilen mitten in der Tabelle prüfen.
- **Hintergrundarbeit selbst prüfen** (läuft der Prozess noch, wann wurde die Ausgabedatei geschrieben), statt auf eine Meldung zu warten.
- **Kein Leerlauf beim Warten** (Wunsch des Nutzers, 24.10.2026): Lange Befehle im Vordergrund mit Zeitlimit oder im selben Zug warten, bis Prozessnummer (`kill -0 <pid>`) bzw. Ausgabedatei fertig sind – nie mit `pgrep -f` und einem Suchwort aus der eigenen Befehlszeile (der Befehl findet sich selbst). Den Zug erst beenden, wenn das Ergebnis da ist.
- **Vor dem Klicktest** den neuesten Stand aller Mitglieder einmergen und das Projekt neu importieren (`--headless --import --path game`).

## Fertigstellen und Mergen

- **Nach „fertig“:** Ein Mitglied pusht nur noch auf Anweisung. Sind alle Teile fertig, macht das Integrations-Mitglied im Funktions-Branch den gemeinsamen Klicktest, schreibt die **Übersicht für den Nutzer** in den PR-Text (was das Spiel jetzt Neues kann, wie man es ausprobiert, was der Klicktest gezeigt hat), stellt den PR auf „bereit“ und wartet die CI ab.
- **Mergen** (Entscheidung des Nutzers, 19.10.2026; Bot-Konto und Schutzregel 28.10.2026): Claude arbeitet mit dem eigenen Bot-Konto (Mitarbeiter nur in diesem Repository, klassisches Token); nach `main` mergt nur der Teamleiter, kein Mitglied. Er mergt den PR seiner Funktion und die PRs, die er für sie beauftragt hat, mit `gh pr merge <Nummer> --merge --delete-branch` – erst wenn alles erfüllt ist: `origin/main` eingemergt, die drei CI-Prüfungen grün, der Klicktest gelaufen und im PR-Text beschrieben, kein Label „Konflikt mit main“. Dann an den Nutzer: „gemergt: #…“.
- **PRs mit geschützten Dateien** (`.github/CODEOWNERS`, u. a. `wirtschaft.json` samt Datenversion, `Speicherstand*.cs`, `CLAUDE.md`, `.github/`, `.claude/`): Der Teamleiter meldet „Freigabe nötig: #…“ und sagt dem Nutzer dazu, **worauf er beim Prüfen achten soll** (welche geschützten Dateien, was sich dort ändert, Risiken). Der Nutzer gibt frei und mergt selbst.
- **„Auto-merge“ schaltet niemand ein** (auch nicht `gh pr merge --auto`). Der Nutzer darf weiter selbst mergen.

## Aufräumen

- Am Ende die Mitglieder beenden, Worktrees und Mitglieder-Branches der gemergten Funktion entfernen (vorher die Prüfung unter „Sorgfalt“) und dem Nutzer zusammenfassen, was offen bleibt.
- **Mitglieder wirklich schließen** (Wunsch des Nutzers, 27.10.2026): Sobald ein Mitglied fertig gemeldet hat und auf nichts mehr wartet (vorher nachfragen, wenn unklar), mit dem Werkzeug zum Beenden von Aufgaben schließen (TaskStop mit dem Namen des Mitglieds) – eine Bitte per Nachricht lässt es als untätiges Mitglied weiterlaufen.
