# Entwicklung

Für alle, die am Spiel mitarbeiten oder es selbst bauen wollen. Die Spielregeln stehen im [Spielbuch](spielbuch/README.md), der Aufbau des Codes in [ARCHITEKTUR.md](ARCHITEKTUR.md).

## Projektstruktur

```
reichsgilde/
├── Reichsgilde.sln                 Projektmappe – fasst alle vier C#-Projekte zusammen
├── game/                           Das Godot-Projekt (Darstellung, Szenen, Eingabe)
│   ├── project.godot
│   ├── Reichsgilde.csproj
│   ├── daten/                      Datentabellen: wirtschaft.json (alle Zahlen des Wirtschaftsmodells),
│   │                               katalog.json (Gebäudekatalog: Modell, Grundfläche), materialarten.json
│   ├── karten/                     Stadtkarten als JSON, Format 2: wo alles steht (aus Kartenwerkstatt oder tools/Kartenbau)
│   ├── assets/                     Dateien nach Art getrennt:
│   │   ├── modelle/                3D-Modelle (.glb, aus tools/modelle) in gebaeude/, innen/, umland/,
│   │   │                           natur/, wasser/, figuren/, fahrzeuge/, tiere/
│   │   ├── texturen/               Strukturen der Oberflächendetails, Boden- und Modell-Texturen (aus tools/oberflaeche)
│   │   ├── shader/                 Shader (Fahnen, Figuren, Oberflächendetails, Wasser, Äcker, Randdunst,
│   │   │                           Farbfilter, Schärfe)
│   │   ├── oberflaeche/            Spieloberfläche: icons/ (Waren-Icons aus tools/icons), schriften/, wappen/
│   │   └── audio/                  musik/, geraeusche/ (noch leer)
│   ├── scenes/                     Szenen (.tscn)
│   └── scripts/                    C#-Skripte, die an Szenen hängen
├── src/Reichsgilde.Core/           Simulationskern – die reine Spiellogik, OHNE Godot
│   ├── Simulation.cs
│   ├── Bank/                       Geldleiher: Kasse, Wechsel, Kredite, Pfändung, Wucher
│   ├── Bauen/                      Stadtkarte, Gebäudekatalog, Bauzonen, Gelände, Wasser, Karrennetz
│   ├── Buerger/                    Bürger-KI: Einwohner mit Rolle und Tagesablauf
│   ├── Duft/                       Parfumeur: Moschus, Zwietracht, Heiligenduft, Giftanschlag
│   ├── Dynastie/                   Personen, Familien, Brautschau, Geburt, Tod und Erbfolge
│   ├── Glaube/                     Konfessionen, Glaubensnähe im Rat, Bekehrung, Stadtglaube
│   ├── Gauner/                     Unterwelt: Diebstahl, Sabotage, Überfall, Erpressung, Wächter
│   ├── Gesundheit/                 Krankheiten, Pest, Behandlung
│   ├── Justiz/                     Stadtgericht: Spuren, Spitzel, Beweise, Anklage, Urteil, Kerker
│   ├── Kartenwerkstatt/            Level-Editor ohne Godot: Kartenentwurf, Befehle mit Rückgängig, Kartenprüfung
│   ├── Ki/                         KI-Rivalen (Entscheidungen und ihr Charakter)
│   ├── Kirche/                     Prediger: Kollekte, Ablass, Beichte
│   ├── Ordnung/                    Stadtwache: Sold, Patrouille, Wachturm, Razzia, Kopfgeld, Geleitschutz
│   ├── Partie/                     Partie-Einstellungen, Schwierigkeit, Startbelegung, Siegbedingung, Aktionspunkte
│   ├── Politik/                    Stadtrat: Ämter, Wahlen, Gunst, Anträge; Stadtkasse, Befugnisse
│   ├── Speichern/                  Spielstände als JSON
│   ├── Wirtschaft/                 Markt, Betriebe, Lager, Karren, Überfälle, Konten, Fronstatut
│   ├── Wohnen/                     Wohnhaus: Stufen, Ausbau, Unterhalt, Ansehen
│   └── Zeit/                       Spielzeit, Taktgeber, Kalender
├── tests/Reichsgilde.Core.Tests/   Automatische Tests für den Simulationskern
├── tools/assets/protokoll_pruefen.py   Prüft das Asset-Protokoll und schreibt die Steam-Liste der KI-Inhalte
├── tools/handbuch/                 Bilder fürs Spielbuch: aufnehmen.sh (Rohbilder aus dem Spiel), beschriften.sh/.gd (nummerierte Markierungen nach bilder.json, Ausgabe docs/bilder/handbuch/*.webp)
├── tools/icons/knoepfe_bauen.gd    Setzt die Waren-Knöpfe aus Rahmen, Hintergrund und Motiv zusammen (Godot-Skript, Quellen in quellen/)
├── tools/Kartenbau/                Kartenskript (C#): Städte per Skript anlegen, prüfen, Vorschaubilder zeichnen
├── tools/modelle/                  Blender-Skripte, eines je Modell (schreiben nach game/assets/modelle/<unterordner>/)
├── tools/oberflaeche/texturen.py   Strukturen der Oberflächendetails (Python + numpy)
├── tools/oberflaeche/materialien_blender.py   Material-Bibliothek der Modelle (Blender, Cycles)
├── docs/
│   ├── ARCHITEKTUR.md              Wie der Code aufgebaut ist und warum
│   ├── assets/                     Asset-Protokoll (Herkunft jeder Grafik, jedes Modells …), Prompts der Bild-KIs, Steam-Liste der KI-Inhalte
│   ├── ENTWICKLUNG.md              Projektstruktur, Tests, Export, automatische Prüfung (diese Datei)
│   ├── bereiche/                   Offene Punkte und aktuelle Entscheidungen je Bereich (Kern, Spiel-KI, Darstellung …);
│   │   └── archiv/                 ältere Entscheidungen je Bereich
│   ├── bilder/                     Bilder für die README und das Spielbuch
│   ├── spielbuch/                  Das Spielbuch: ein Kapitel je Thema, Inhaltsverzeichnis in README.md
│   ├── ENTSCHEIDUNGEN.md           Archiv der Grundsatzentscheidungen bis 16.10.2026, Phasenplan
│   ├── LEVELDESIGN.md              Kartenregeln und Städte
│   ├── WELT.md                     Die Welt der Reichsgilde: Weltgeschichte, Völker, Glaube, Städte (Köln)
│   ├── messungen/                  Balancing-Berichte als JSON (messen bericht, siehe WIRTSCHAFTSMODELL.md)
│   └── WIRTSCHAFTSMODELL.md        Zahlen und Messungen je Beruf und Balance-Änderung
├── CLAUDE.md                       Arbeitsregeln für die Claude-Agenten (Teamleiter und Mitglieder)
└── .github/                        Automatische Prüfung auf Windows und Linux, Vorlage für PR-Texte
```

Warum diese Aufteilung? → [ARCHITEKTUR.md](ARCHITEKTUR.md) · Was wann entschieden wurde → [ENTSCHEIDUNGEN.md](ENTSCHEIDUNGEN.md)

## Tests ausführen

Im Terminal (in VS Code: *Terminal → Neues Terminal*) im Ordner `reichsgilde`:

```
dotnet run -c Release --project tests/Reichsgilde.Core.Tests
```

Am Ende muss stehen: `Alle … Tests bestanden.` Darunter steht, wie viele Tests gelaufen sind, wie viele übersprungen wurden und wie lange es gedauert hat.

### Gezielt testen

Alle Tests zusammen brauchen auf der NAS knapp vier Minuten (3 min 43 s bis 4 min 8 s je nach Last, gemessen am 8.11.2026). Während der Arbeit genügt meist ein Teil; die Schalter stehen hinter `--` (damit `dotnet` sie an das Testprogramm weitergibt):

| Schalter | Was er tut | Beispiel |
|---|---|---|
| `--filter=<Muster>` | nur Tests, deren Klassen- **oder** Testname das Muster enthält; ohne Groß- und Kleinschreibung, mehrere Muster mit Komma, `*` steht für beliebigen Text | `-- --filter=Taschendieb,Nachtgang` |
| `--klasse=<Name>` | nur diese Testklassen (der ganze Name muss passen, `*` erlaubt); die Tests oben in `Program.cs` heißen `Program` | `-- --klasse=DiebTests,Wache*` |
| `--ohne-langsam` | überspringt die langsamen Tests aus `tests/Reichsgilde.Core.Tests/LangsameTests.cs` | `-- --ohne-langsam` |
| `--liste` | zeigt nur, welche Tests ausgewählt wären (mit „langsam“ markiert), und führt nichts aus | `-- --liste --filter=Hafen` |
| `--zeiten[=<Anzahl>]` | nennt am Ende die langsamsten Tests mit Sekunden (Standard 20), mit `--parallel` zusätzlich die langsamsten Klassen | `-- --zeiten=40` |
| `--parallel[=<Fäden>]` | Testklassen gleichzeitig auf mehreren Fäden (Standard: Kerne − 2, höchstens 6); siehe „Parallel testen“ | `-- --parallel` |
| `--hilfe` | zeigt alle Schalter | `-- --hilfe` |

Schalter wirken zusammen: `-- --klasse=Buerger* --filter=Hafen` nimmt nur die Hafen-Tests der Bürger-Klassen. Ein Komma trennt Muster; steht im Testnamen selbst ein Komma, an seiner Stelle `*` schreiben. Ein unbekannter Schalter (Tippfehler) bricht mit der Hilfe ab, eine Auswahl ohne Treffer (auch mit `--liste`) mit einem Fehler – so läuft nie aus Versehen „nichts“ grün durch.

**Regel (Prüfen vor dem PR):** Lokal Build plus die betroffenen Tests per `--filter` bzw. `--klasse`; der volle Satz läuft in der CI. Betrifft die Änderung **Kern, Spielstand oder Determinismus**, zusätzlich lokal `-- --filter=Gleichheit,Speicherstand` (Gleichheit, Speicherstand). Die CI prüft immer alle Tests, der Export-Job nur bei PRs mit Änderungen unter `game/` (sonst meldet er grün, ohne zu exportieren).

**Langsame Tests:** Ein Test, der allein etwa 3 Sekunden oder länger läuft (mehrjährige Partien, Gleichheitstest, Messungen in Tests), bekommt eine Zeile in `LangsameTests.cs`: Klasse, Muster für den Testnamen (oder `null` für die ganze Klasse) und wie viele Tests die Zeile trifft. Er wird dadurch **nicht** abgeschaltet: Ohne Schalter und in der CI läuft er mit. Wird ein eingetragener Test umbenannt oder nimmt ein Muster plötzlich mehr Tests mit, meldet `TestfilterTests` den Eintrag. Welche Tests gerade langsam sind, zeigt `-- --zeiten=60`.

**Testzeit sparen, ohne Aussage zu verlieren:** Die Probestadt als Testkarte kommt aus `Probestadtvorlage.Entwurf(katalog, daten)` (Skript einmal, danach ein frischer Entwurf aus dem Text; nur Tests, die das Skript selbst prüfen, rufen `new Probestadt().Bauen(…)`). Meldungen in Schleifen mit vielen tausend Prüfungen erst im Fehlerfall bauen (`if (!bedingung) Pruefe.Wahr(false, $"…")`): Eine interpolierte Meldung mit Kommazahlen kostet mehr als die Prüfung selbst. Eine Suche, die in jedem Lauf dasselbe findet (z. B. ein passender Startwert), einmal je Lauf in einem `Lazy` merken und den bekannten Treffer zuerst probieren. Jede Kürzung mit Gegenprobe: Ein absichtlicher Fehler im Code muss den Test weiter rot machen.

### Parallel testen

`-- --parallel` lässt die Testklassen gleichzeitig laufen, die Tests einer Klasse wie gewohnt nacheinander. Auf der NAS dauert der volle Lauf so etwa 3 bis 3½ Minuten statt knapp 4 (gemessen am 8.11.2026, während andere Mitglieder mit Godot maßen): Die NAS hat nur vier Kerne, unter Last rechnet jeder Test parallel drei- bis viermal so lange, und mit zwei Fäden war der Lauf ebenso schnell wie mit sechs. Auf einem freien Rechner mit mehr Kernen bringt der Schalter mehr. Der Schalter wirkt mit allen anderen zusammen (`-- --parallel --ohne-langsam`, `-- --parallel=2 --klasse=Buerger*`).

- **Ausgabe:** Jede Klasse schreibt in ihren eigenen Puffer (auch was ihre Tests mit `Console.WriteLine` ausgeben). Die Blöcke erscheinen in der gewohnten Reihenfolge, sobald eine Klasse und alle vor ihr fertig sind. Zusammenfassung und Rückgabewert sind dieselben wie seriell; dazu kommt die Zahl der Fäden und die Rechenzeit aller Tests zusammen.
- **Allein am Ende:** Klassen mit Zeitgrenzen („unter 150 ms“, „schneller als das Raster“) stehen in `tests/Reichsgilde.Core.Tests/SeriellTests.cs` und laufen erst danach, eine nach der anderen – unter Volllast würde die Grenze sonst zufällig reißen. Wer einen Test mit Zeitgrenze schreibt oder etwas für den ganzen Prozess ändert (Umgebungsvariable, aktuelles Verzeichnis, Datei mit festem Namen, `Console.SetOut`), trägt seine Klasse dort ein. Zähler, `Lazy` und `CultureInfo.CurrentCulture` (gilt je Faden) sind kein Grund: Eine Klasse läuft immer in einem Faden.
- **Wann seriell:** Vor dem PR bzw. dem Abschlussbericht einmal **ohne** `--parallel` – so prüft auch die CI, die seriell bleibt. Seriell auch, wenn die NAS gerade misst (Godot-Läufe, Messungen anderer Mitglieder): `--parallel` belegt bis zu sechs Kerne. Fällt ein Test nur parallel durch, hängt er von einem anderen ab oder von der Zeit – dann den Test reparieren bzw. seine Klasse in `SeriellTests.cs` eintragen.

Hängt ein Test unbemerkt von einem anderen ab (gemeinsamer Zustand, Reihenfolge), fällt das beim gezielten Lauf auf: Dann ist der Test zu reparieren, nicht der Filter.

## Klicktest unter Windows

Unter Linux macht `tools/klicktest/lauf.sh` Klicktests auf einem unsichtbaren Bildschirm (Xvfb, xdotool). Unter Windows gibt es den nicht, und echte Maus- und Tastenbefehle würden stören. Darum führt das Spiel die Aktionen selbst aus (Autoload `KlicktestTreiber`), gestartet von `tools/klicktest/lauf.ps1` mit demselben Aufbau:

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\klicktest\lauf.ps1 C:\Temp\k 45 "s tag;m 620 395;w 1;b 1;w 2;s klick" -- --karte=koeln --startstunde=12 --wetter=klar
```

- `-Treiber d3d12` nimmt DirectX 12 statt Vulkan; `-Daten <ordner>` lenkt `user://` um (Standard `%LOCALAPPDATA%\Reichsgilde\klicktest-daten`, damit Einstellungen und Spielstände des Spielers unberührt bleiben).
- Godot kommt aus `$env:GODOT`, sonst aus dem winget-Ordner; genommen wird die `_console.exe`, das Log steht in `<ausgabeordner>\godot.log`.
- Vorher fragt `reichsgilde-frage` (Außenstelle, `tools/aussenstelle/`), ob der PC frei ist; während des Laufs prüft `reichsgilde-pruefen` den Notschalter.
- Vor dem ersten Lauf einmal `dotnet build game\Reichsgilde.csproj` und `Godot --headless --path game --import`.
- Das Spielfenster ist sichtbar; die Maus in der Zeit vom Fenster lassen, denn Hover und Tooltips folgen der echten Maus.

## Im Godot-Editor spielen

Startet man das Spiel im Godot-Editor, läuft es im Reiter **Game** eingebettet. Dort bestimmt der Editor die Fenstergröße, und Vollbild (F11) ist nicht möglich. Zum Testen im eigenen Fenster das Einbetten im Reiter „Game“ ausschalten.

Die Tempostufen 16× und 32× (Tasten 5 und 6) sind Testtempo für die Entwicklung; im Code stehen sie in `Taktgeber.TestTempostufen` (Suche nach `TESTTEMPO`) und fallen vor der Veröffentlichung weg.

## Bilder für das Spielbuch

Die beschrifteten Bilder im Spielbuch (`docs/bilder/handbuch/`) entstehen aus echten Bildschirmfotos: `tools/handbuch/aufnehmen.sh` nimmt die Rohbilder mit dem Klicktest auf, `tools/handbuch/beschriften.sh` setzt die Nummern (Datei `tools/handbuch/bilder.json`: Bild, Stelle der Nummer, Rahmen, Ausschnitt) und schreibt `.webp`. Ändert sich die Oberfläche, Rohbilder neu aufnehmen und die Stellen in `bilder.json` prüfen.

## Außenstelle auf dem Grafik-PC

Eine eigene Claude-Code-Sitzung auf dem Windows-PC führt Klicktests und Messungen aus, aber nur, wenn keine KI (Ports 8188, 8189, 8190, 8777) läuft und der Nutzer es erlaubt. Vor jedem schweren Auftrag ruft sie `tools/aussenstelle/frage.ps1` auf, bei langen Aufgaben zwischendurch `pruefen.ps1`; der Nutzer holt den PC mit `reichsgilde-stopp` zurück. Einzelheiten und Einrichtung: `tools/aussenstelle/README.md`.

## Spiel exportieren (Windows / Linux)

1. Einmalig in Godot: **Editor → Exportvorlagen verwalten → Herunterladen und installieren**
2. **Projekt → Exportieren…** → Vorlage **Windows** oder **Linux** wählen → **Projekt exportieren**
3. Das fertige Spiel landet im Ordner `build/` (wird nicht ins Repository übernommen)

## Plattformen und automatische Prüfung

Reichsgilde läuft auf **Windows und Linux**. Bei jedem Pull Request prüft GitHub automatisch:

1. Bauen und Tests – auf Windows **und** Linux; unter Linux dazu das **Asset-Protokoll**: Jede Datei unter `game/assets/` muss in `docs/assets/protokoll.csv` stehen (Werkzeug, KI-Modell, Prompt, Lizenz), sonst scheitert die Prüfung (`python3 tools/assets/protokoll_pruefen.py`, Einzelheiten in `docs/assets/README.md`)
2. **Gleichheit Linux/Windows** – der Gleichheitstest (Mehrspieler) schreibt auf beiden Systemen Prüfsummen über feste Partien; sie müssen Zeichen für Zeichen gleich sein (Einzelheiten in [ARCHITEKTUR.md](ARCHITEKTUR.md), „Gleich auf jedem Rechner“)
3. Export für beide Systeme und ein kurzer Startversuch des Linux-Builds (Rauchtest). Meldet das Spiel dabei einen Fehler oder ein Speicherleck, wird der Start einmal wiederholt – erst ein zweiter Fund lässt die Prüfung scheitern. Die Fundstellen stehen als Anmerkung oben in der Zusammenfassung des Laufs. Stürzt das Spiel ab oder hängt es (Grenze: 3 Minuten je Start), zeigt eine Rückverfolgung mit gdb, welcher Faden wo stand – als Anmerkung und im Artefakt **„Rauchtest-Protokoll“**.

Die fertigen Builds kann man unter **Actions → jeweiliger Lauf → Artifacts** herunterladen. Linux läuft dabei fest auf Ubuntu 24.04, damit ein neues Ubuntu bei GitHub die Prüfung nicht unbemerkt ändert.
Die Konfiguration steht in [`.github/workflows/build.yml`](../.github/workflows/build.yml).

Nach jedem Merge auf `main` hält die Action **„PRs aktuell halten“** ([`.github/workflows/prs-aktuell.yml`](../.github/workflows/prs-aktuell.yml)) alle fertigen Pull Requests auf dem neuen Stand: Sie mergt `main` hinein und startet die Prüfung neu. Lässt sich das nicht automatisch lösen, bekommt der PR das Label **„Konflikt mit main“**. Damit das selten passiert, schreibt jeder Bereich seine Entscheidungen und offenen Punkte in eine eigene Datei unter `docs/bereiche/`.

*Stand 08.11.2026: Das Bot-Konto ist gesperrt, es gibt keinen Ersatz-Bot (Entscheidung „Einbahnstraße“, `.claude/teamleiter.md`). Ohne gültiges `BOT_TOKEN` gilt der Rückfall unten; ob das Geheimnis neu mit einem Token des Nutzers gesetzt wird, entscheidet der Nutzer.*

**Prüfungen nach dem automatischen Einspielen (einmalig einrichten).** Pushes mit dem eingebauten `GITHUB_TOKEN` starten aus Sicherheitsgründen keine Pull-Request-Prüfungen; ohne Einrichtung startet die Action `build.yml` deshalb selbst, und die PR-Seite zeigt die Prüfung nicht als reguläre Prüfung. Besser ist ein Token des Bot-Kontos:

1. Auf GitHub als Bot-Konto (Claude-Reichsgilde) unter Settings → Developer settings → Personal access tokens → Tokens (classic) ein Token mit den Rechten `repo` und `workflow` anlegen (oder das vorhandene klassische Token des Bot-Kontos nehmen). `workflow` ist nötig, weil `main` auch Dateien unter `.github/workflows/` ändern kann.
2. Im Repository: Settings → Secrets and variables → Actions → New repository secret.
3. Name `BOT_TOKEN`, Wert = das Token, speichern.

Ab dann startet jeder Push der Action die normalen PR-Prüfungen von selbst; der zusätzliche Lauf entfällt. Fehlt das Geheimnis, gilt das bisherige Verhalten. Das Token steht nie in einer Datei und wird im Log maskiert. Es pusht nur auf PR-Branches, nie auf `main` (die Schutzregel bleibt unberührt).
