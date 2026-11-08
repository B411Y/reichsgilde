# Abläufe des Teamleiters

Gilt für die Hauptsitzung, die als **Teamleiter** Mitglieder startet. Die Regeln der `CLAUDE.md` gelten zusätzlich. Mitglieder lesen diese Datei nicht; was sie brauchen, steht in ihrer Datei unter `.claude/agents/`.

## Rolle

- **Teamleiter ändert keine Dateien** (Entscheidung des Nutzers, 28.10.2026): keine Dateien im Repository, auch keine kleinen, und kein Commit. Jede Änderung geht als Auftrag an ein Mitglied. Ausnahme: eigene Notizen außerhalb des Repositorys. Abgesichert durch den Hook in `.claude/settings.json` (`.claude/hooks/teamleiter_sperre.py`; fängt Versehen, keine Absicht).
- Beim Teamleiter bleiben: Designfragen, Zuschnitt und Aufträge, Entscheidungen unterwegs, Berichte und Bilder prüfen, Status an den Nutzer, der Merge nach `main` (über den Push-Agenten), Aufräumen und der **Grafik-PC** (Startdienst; nur der Teamleiter, den Schlüssel bekommt kein Mitglied; Erzeugtes nur in den Notizordner, ein Mitglied baut es ein).
- **Zu Beginn:** `git fetch origin`, `git log origin/main --oneline -15` und den offenen PR ansehen (es ist höchstens einer offen, siehe „Merge-Ablauf Einbahnstraße“; `gh pr list`, `gh pr view`, einzeln, kein `gh api`). Fasst eine Aufgabe Dateien eines offenen PR an: erst den Nutzer fragen.
- **Designfragen** an den Nutzer mit Auswahl (je eine empfohlene Option), bevor gebaut wird. Was sich nicht zurücknehmen lässt, immer erst fragen.

## Team aufstellen

- **Mitglieder:** Rollen in `.claude/agents/<name>.md` (kern, spiel-ki, darstellung, oberflaeche, leveldesign, sound, integration, pruefer, infra, suche). Aufruf mit `subagent_type` = Name; Modell und Aufwand im Aufruf überschreiben die Vorgabe der Datei.
- **Zuschnitt nach Größe:** Kleine Funktionen bekommen zwei Mitglieder, „kern“ und „spiel“ (Oberfläche und Darstellung zusammen). Nur große Themen mit viel unabhängiger Arbeit bekommen ein Mitglied je Bereich. Jede zusätzliche Grenze kostet Abstimmung.
- **Ein Funktions-Branch, ein PR:** Erst die Designfragen an den Nutzer. Dann entsteht der Funktions-Branch `phaseN/thema` (`fix/thema` für Fehler, `infra/thema` für Werkzeuge und Abläufe), lokal in einem Worktree. Mitglieder öffnen **keine eigenen PRs** und **pushen nie**: Jedes arbeitet auf `phaseN/thema-<name>`, committet lokal und meldet fertig; das Integrations-Mitglied mergt in den Funktions-Branch (mergen, nicht rebasen). Den PR legt erst der Push-Agent an, wenn der Branch an der Reihe ist (siehe „Merge-Ablauf Einbahnstraße“).
- **Ein PR pro Funktion:** Es gibt weniger PRs, die durch die Einbahnstraße müssen. Kleinkram (Doku-Nachträge, bestätigte Entscheidungen, Aufräumen) fährt im nächsten Funktions-PR mit. Einen eigenen kleinen PR gibt es nur für Dringendes, etwa einen Fehler, der andere blockiert, oder eine kaputte CI.
- **Doku-Ablage:** Spielbuch-Kapitel liegen unter `docs/spielbuch/`, ältere Entscheidungen unter `docs/bereiche/archiv/`. Beim Einmergen älterer Branches gehört Neues, das noch in die alte README-Spielbuch-Struktur oder ans Ende einer Bereichsdatei kam, an den neuen Ort (Kapitel bzw. Abschnitt „Entscheidungen“ der aktuellen Datei).
- **Reihenfolge:** Zuerst baut kern Befehl, Abfragen und Tests und meldet die genauen Signaturen. Das Integrations-Mitglied mergt das in den Funktions-Branch. Erst danach starten die anderen und zweigen vom Funktions-Branch ab.
- **Getrennte Ordner:** Jedes Mitglied hat einen eigenen Worktree `../rg-<name>` und einen eigenen Unterordner im Notizordner (`<Notizordner>/<name>/`). Ein gemeinsamer Ordner führt zu überschriebenen Dateien.
- **Modell und Aufwand** (Entscheidung des Nutzers, 07.11.2026, vorher 02.11.; beim Start immer ausdrücklich `model` und `effort` angeben):

| Bereich | model | effort |
|---|---|---|
| kern, spiel-ki, Spielstand | opus | high |
| 3D-Modelle (Blender), Shader | sonnet | high |
| Darstellung, Oberfläche, Leveldesign, Sound | sonnet | high |
| Integration (PR bauen, Klicktest) | sonnet | high |
| Kleinkram (main einmergen, CI abwarten, Doku, Codesuche), Texte, Namen, Logs zusammenfassen | haiku | high |

- **Zusatzregeln:** Prüfer und Unter-Agenten bekommen das Modell des Bereichs, den sie prüfen bzw. betreuen, mit Aufwand hoch. Höchstens 3 Mitglieder gleichzeitig. Ausnahme: Charakter-Köpfe sonnet, xhigh (derzeit pausiert). Ultracode nur auf das Schlüsselwort des Nutzers. Wird ein Bereich schlechter, meldet der Teamleiter das dem Nutzer und stuft nicht selbst hoch. Strata wird nicht mehr genutzt. Der Teamleiter läuft auf mittel; der Nutzer stellt ihn bei Designfragen oder Fehlersuche selbst höher. Bild-KI läuft nur lokal auf dem Grafik-PC.

## Unterwegs

- **Entscheidungen unterwegs:** Taucht eine Designfrage auf, entscheidet der Teamleiter **einmal** (oder fragt den Nutzer) und schickt **eine** endgültige Nachricht an alle Betroffenen. Eine getroffene Entscheidung nicht nachträglich umwerfen: Nachrichten überholen sich, und dann baut jemand den alten Stand zurück.
- **Nachrichten:** Vorschläge an ein anderes Mitglied gehen **über den Teamleiter**, nicht direkt untereinander. Zwischenmeldungen nur bei einer Blockade oder einer fertigen Schnittstelle, so kurz wie möglich.
- **Abschlussbericht in fünf Zeilen** (Wunsch des Nutzers, 25.10.2026, Tokens sparen): 1. Branch und Commit, 2. was gebaut ist (ein Satz), 3. Tests und Klicktest (Zahlen, Fehler), 4. selbst getroffene Entscheidungen und Offenes, 5. Ordner mit den Bildern. Einzelheiten stehen in Doku und Commit; bei Bedarf nachfragen.
- **Leerlauf prüfen:** Solange ein Team läuft, alle 30 Minuten prüfen, ob ein Mitglied untätig ist, obwohl seine Ausgabe fertig ist.
- **Prüf-Agent bei Kern-PRs** (28.10.2026): Vor „bereit“ liest ein unabhängiger `pruefer` (Modell des geprüften Bereichs, Aufwand hoch) den Diff von Kern, Spiel-KI und Tests – er bekommt nur Code und Prüfauftrag, nicht die Einschätzung des Mitglieds. Befunde behebt das Mitglied vor dem Merge.

## Sorgfalt

Wunsch des Nutzers, 21.10.2026: Er erwartet fehlerfreie Arbeit, Fehler haben Konsequenzen. Diese Prüfschritte sind Pflicht (ausgeführt vom Integrations-Mitglied, der Teamleiter achtet darauf):

- **Vor dem Löschen eines Mitglieder-Branches:** `git merge-base --is-ancestor origin/<mitglieder-branch> <funktions-branch>`. Fehlt der Commit, erst einmergen.
- **Nach jedem Merge** die betroffenen Tabellen in `docs/bereiche/*.md` auf doppelte Zeilen und Leerzeilen mitten in der Tabelle prüfen.
- **Hintergrundarbeit selbst prüfen** (läuft der Prozess noch, wann wurde die Ausgabedatei geschrieben), statt auf eine Meldung zu warten.
- **Kein Leerlauf beim Warten** (Wunsch des Nutzers, 24.10.2026): Lange Befehle im Vordergrund mit Zeitlimit oder im selben Zug warten, bis Prozessnummer (`kill -0 <pid>`) bzw. Ausgabedatei fertig sind – nie mit `pgrep -f` und einem Suchwort aus der eigenen Befehlszeile (der Befehl findet sich selbst). Den Zug erst beenden, wenn das Ergebnis da ist.
- **Vor dem Klicktest** den neuesten Stand aller Mitglieder einmergen und das Projekt neu importieren (`--headless --import --path game`).

## Fertigstellen und Mergen

- **Nach „fertig“:** Sind alle Teile fertig, macht das Integrations-Mitglied im Funktions-Branch den gemeinsamen Klicktest und schreibt die **Übersicht für den Nutzer** (was das Spiel jetzt Neues kann, wie man es ausprobiert, was der Klicktest gezeigt hat) in den PR-Text. Der Branch bleibt lokal und kommt in die Warteschlange.
- **Merge-Ablauf „Einbahnstraße“** (Entscheidung des Nutzers, 08.11.2026; ersetzt die Regeln mit Bot-Konto und Nutzer-Freigabe vom 19./28./29.10.2026):
  1. **Parallel nur lokal:** Mitglieder arbeiten gleichzeitig in Worktrees und committen; fertige Branches warten in einer Warteschlange des Teamleiters.
  2. **Auf GitHub ist immer nur ein PR offen.** Der nächste kommt erst hoch, wenn der vorige gemergt ist.
  3. **Vor dem Hochladen** mergt ein Mitglied (meist Integration) den aktuellen `origin/main` in den Branch, löst Konflikte, baut und testet (gezielt mit `--filter`, dazu einmal alle Tests; bei Darstellung bzw. Oberfläche kurzer Klicktest). Hat die Action „PRs aktuell halten“ (`prs-aktuell.yml`) schon etwas eingemergt, vorher `origin/<branch>` mergen. Dann pusht der Push-Agent und legt den PR an.
  4. **Der Teamleiter mergt selbst, alle PRs, auch solche mit geschützten Dateien** (`wirtschaft.json`, `Speicherstand*.cs`, `CLAUDE.md`, `.github/`, `.claude/`, `LICENSE*`). Einmal `gh pr checks <Nummer>` ansehen lassen, keine Warteschleifen um `gh`. Sind alle 3 Pflichtprüfungen grün und Klicktest bzw. Prüfer erledigt, führt der Push-Agent einmal `gh pr merge <Nummer> --merge` aus. Dann an den Nutzer: „gemergt: #…“; danach kommt der nächste Branch aus der Warteschlange dran. Bei geschützten Dateien sagt der Teamleiter dem Nutzer im Statusbericht dazu, was sich dort ändert.
  5. **Verboten:** `--admin`, `--auto`, `--delete-branch` (der Hook `~/.claude/hooks/github_sperre.py` sperrt sie), Force-Push, `gh api`, Schleifen um `gh`. Ohne Klicktest nichts nach `main`.
  6. **Schutzregel auf `main`:** nur per PR, 3 Pflichtprüfungen (Linux, Windows, Export). „Require review from Code Owners“ ist seit 08.11.2026 aus; `.github/CODEOWNERS` bleibt als Kennzeichnung der empfindlichen Pfade.
  7. **GitHub-Konto:** Alles läuft über das Konto des Nutzers (B411Y); das frühere Bot-Konto ist gesperrt, es gibt keinen Ersatz-Bot. **Nur der Push-Agent** (haiku, Aufwand mittel) führt `git push`, `gh pr create` und `gh pr merge` aus; alle anderen Mitglieder pushen nie.
- **Grund der Einbahnstraße:** Am 29.10. erzeugte jeder Merge bei fünf gleichzeitig offenen PRs neue Konflikte, und `prs-aktuell.yml` spielt `main` automatisch ein und startet die CI neu.

## Aufräumen

- Am Ende die Mitglieder beenden, Worktrees und Mitglieder-Branches der gemergten Funktion entfernen (vorher die Prüfung unter „Sorgfalt“) und dem Nutzer zusammenfassen, was offen bleibt.
- **Mitglieder wirklich schließen** (Wunsch des Nutzers, 27.10.2026): Sobald ein Mitglied fertig gemeldet hat und auf nichts mehr wartet (vorher nachfragen, wenn unklar), mit dem Werkzeug zum Beenden von Aufgaben schließen (TaskStop mit dem Namen des Mitglieds) – eine Bitte per Nachricht lässt es als untätiges Mitglied weiterlaufen.
