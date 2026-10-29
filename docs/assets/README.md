# Asset-Protokoll

Woher jede Grafik, jedes Modell, jede Textur, jeder Shader, jede Schrift und jeder Klang des Spiels stammt, steht in **`protokoll.csv`** (Entscheidung des Nutzers, 29.10.2026). Daraus entsteht die Liste der KI-Inhalte für Steam (`steam-ki-inhalte.md`). Die Prompts der Bild-KIs liegen in `prompts/`.

**Regel:** Kein Asset ohne Protokolleintrag im selben PR. Wer eine Datei unter `game/assets/` (oder eine KI-Quelle unter `tools/icons/quellen/`, `tools/portraets/quellen/`) anlegt, ändert oder löscht, passt im selben PR die Zeile an (mindestens `anzahl`) und führt das Prüfskript aus.

```bash
python3 tools/assets/protokoll_pruefen.py                 # prüfen (läuft auch in der CI, Job „Build & Tests (ubuntu-latest)“)
python3 tools/assets/protokoll_pruefen.py --steam-liste   # nach einer Änderung an KI-Zeilen die Steam-Liste neu schreiben
```

## Aufbau der CSV

UTF-8, Komma als Trenner, erste Zeile ist der Kopf. Enthält ein Feld ein Komma, steht es in Anführungszeichen (jede Tabellenkalkulation macht das beim Speichern als CSV von selbst). Eine Zeile beschreibt eine **Gruppe gleich entstandener Dateien**; Einzelzeilen nur für Sonderfälle.

| Spalte | Inhalt |
|---|---|
| `pfad_muster` | Muster relativ zum Repository, z. B. `game/assets/oberflaeche/portraets/elb/*.webp`. `*` = beliebige Zeichen ohne `/`, `**` = beliebig viele Ordner, `?` = ein Zeichen, `{a,b}` = a oder b. |
| `anzahl` | So viele Dateien erfasst die Zeile (prüft das Skript). |
| `art` | Bild, Modell, Textur, Shader, Schrift, Audio oder Text |
| `herkunft` | KI, Nutzer, gemischt, Fremdpaket, eigenes Skript (oder unbekannt) |
| `werkzeug` | Programm oder Dienst, bei eigenen Skripten der Pfad des Skripts |
| `modell_version` | Name und Version des KI-Modells; ohne KI „entfällt“ |
| `prompt_oder_quelle` | Kurztext, Pfad zur Prompt-Datei (`docs/assets/prompts/<name>.md`) oder Bezugsquelle eines Fremdpakets |
| `datum` | erster Commit der Gruppe, `JJJJ-MM-TT` |
| `commit_pr` | Commits und PR-Nummern, z. B. `c16f5c20 (#211)` |
| `nachbearbeitung` | `nein`, `ja: <Umfang>` oder `unbekannt`. Gemeint ist jede Änderung nach der Erzeugung, durch ein Skript oder durch einen Menschen; bei „ja“ steht dabei, wer es war (Skriptpfad bzw. „Nutzer: …“) |
| `bedingungen_werkzeug` | Link zu den Nutzungsbedingungen bzw. der Lizenz des Werkzeugs; ohne fremdes Werkzeug „entfällt“ |
| `lizenz_ergebnis` | Lizenz der Datei, z. B. CC BY 4.0, CC0 1.0, OFL 1.1, MIT (Shader). **Neue Zeilen ab dem Stichtag** (Merge von PR #PRNUMMER, siehe `LICENSE-INHALTE.md`): „Alle Rechte vorbehalten“; Fremdmaterial behält seine Lizenz. Mit „CC BY“ darf eine Zeile mit `datum` nach dem Stichtag nicht mehr stehen |
| `spieler_sieht` | ja oder nein |
| `bemerkung` | frei (einziges Feld, das leer bleiben darf) |

**Was nicht belegt ist, heißt „unbekannt“ – nicht raten.** „entfällt“ heißt: gibt es hier nicht (z. B. Modellversion bei einem Blender-Skript); bei KI-Herkunft ist „entfällt“ in Werkzeug, Modellversion und Bedingungen nicht erlaubt.

**Mehrere Treffer:** Passt eine Datei auf mehrere Zeilen, gilt die **letzte** (wie in `.github/CODEOWNERS`). So steht unter der Gruppe `icons/*.png` (Waren-Knöpfe) die Zeile `icons/einrichtung_*.png` mit anderer Herkunft; `anzahl` zählt je Zeile nur die Dateien, die sie am Ende erfasst.

## Was das Prüfskript prüft

- Jede Datei unter `game/assets/`, `tools/icons/quellen/` und `tools/portraets/quellen/` ist erfasst (ausgenommen `.import`, `.uid`, `.gitkeep`, `HERKUNFT.md`). Gezählt werden Dateien, die Git kennt, auch neue, noch nicht hinzugefügte (ohne die in `.gitignore`).
- Jedes Muster trifft mindestens eine Datei, `anzahl` stimmt.
- Pflichtfelder sind gefüllt und haben gültige Werte; Verweise auf `docs/assets/prompts/…` zeigen auf vorhandene Dateien.
- `steam-ki-inhalte.md` passt zum Protokoll.
- Zeilen mit `datum` nach dem Stichtag (Konstante `STICHTAG` im Skript; `None` = Prüfung aus) tragen kein „CC BY“ in `lizenz_ergebnis`.

Fehler meldet es deutsch mit Zeilennummer und endet mit Rückgabewert 1; dann scheitert in der CI die Pflichtprüfung „Build & Tests (ubuntu-latest)“.

## Prompt-Dateien

Je Gruppe eine Datei in `prompts/`: Werkzeug, Vorlage, Ablauf und die Prompt-Texte. Keine Rechnerpfade, Adressen oder Zugangsdaten – Aufrufskripte außerhalb des Repositorys nur beschreiben.

## Verhältnis zu anderen Dateien

- `HERKUNFT.md` neben Fremdpaketen und dem KI-Schmied (`game/assets/modelle/natur/`, `props/`, `figuren/ki/`) behalten die Lizenztexte und Einzelheiten der Werkzeuge; in Ordnern mit KI-Bildern verweist eine kurze `HERKUNFT.md` hierher.
- `LICENSE-INHALTE.md` sagt, unter welcher Lizenz Code und Inhalte stehen (bis zum Stichtag CC BY 4.0, danach „Alle Rechte vorbehalten“); das Protokoll sagt, woher jede Datei kommt.
