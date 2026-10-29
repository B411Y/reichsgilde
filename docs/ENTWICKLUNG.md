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
dotnet run --project tests/Reichsgilde.Core.Tests
```

Am Ende muss stehen: `Alle … Tests bestanden.`

## Im Godot-Editor spielen

Startet man das Spiel im Godot-Editor, läuft es im Reiter **Game** eingebettet. Dort bestimmt der Editor die Fenstergröße, und Vollbild (F11) ist nicht möglich. Zum Testen im eigenen Fenster das Einbetten im Reiter „Game“ ausschalten.

Die Tempostufen 16× und 32× (Tasten 5 und 6) sind Testtempo für die Entwicklung; im Code stehen sie in `Taktgeber.TestTempostufen` (Suche nach `TESTTEMPO`) und fallen vor der Veröffentlichung weg.

## Bilder für das Spielbuch

Die beschrifteten Bilder im Spielbuch (`docs/bilder/handbuch/`) entstehen aus echten Bildschirmfotos: `tools/handbuch/aufnehmen.sh` nimmt die Rohbilder mit dem Klicktest auf, `tools/handbuch/beschriften.sh` setzt die Nummern (Datei `tools/handbuch/bilder.json`: Bild, Stelle der Nummer, Rahmen, Ausschnitt) und schreibt `.webp`. Ändert sich die Oberfläche, Rohbilder neu aufnehmen und die Stellen in `bilder.json` prüfen.

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
