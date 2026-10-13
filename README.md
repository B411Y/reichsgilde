# Reichsgilde

*Arbeitstitel* – eine Wirtschaftssimulation und Dynastie-Sandbox im 2,5D-Stil, inspiriert von **„Die Gilde“ (Europa 1400)**.
Schauplatz ist ein düster-bodenständiges Heiliges Römisches Reich des Spätmittelalters, in dem Menschen, Zwerge, Elben und Halblinge als Bürger leben und Orks und Oger als Bestien vor den Toren lauern.

> Hobby- und Lernprojekt · Open Source (MIT) · Godot 4.7 + C#

## Aktueller Stand

**Phase 0 – Fundament.** Die Spielzeit läuft: 1 Runde = 1 Spieltag = 1 Spieljahr (Standard: 15 Minuten Echtzeit, einstellbar), mit Jahreszeiten, Tagesphasen, Pause und Tempostufen.

## Projektstruktur

```
reichsgilde/
├── Reichsgilde.sln                 Projektmappe – fasst alle drei C#-Projekte zusammen
├── game/                           Das Godot-Projekt (Darstellung, Szenen, Eingabe)
│   ├── project.godot
│   ├── Reichsgilde.csproj
│   ├── scenes/                     Szenen (.tscn)
│   └── scripts/                    C#-Skripte, die an Szenen hängen
├── src/Reichsgilde.Core/           Simulationskern – die reine Spiellogik, OHNE Godot
│   ├── Simulation.cs
│   └── Zeit/                       Spielzeit, Taktgeber, Kalender
├── tests/Reichsgilde.Core.Tests/   Automatische Tests für den Simulationskern
└── docs/                           Erklärungen und Entscheidungen
```

Warum diese Aufteilung? → [docs/ARCHITEKTUR.md](docs/ARCHITEKTUR.md)

## Loslegen

### Voraussetzungen
- [Godot 4.7.x – .NET-Version](https://godotengine.org/download)
- [.NET SDK 8 oder neuer](https://dotnet.microsoft.com/download)
- VS Code mit der C#-Erweiterung (empfohlen)

### Spiel starten
1. Godot starten → **Importieren** → `game/project.godot` auswählen.
2. Oben rechts auf **▶ (Projekt ausführen)** klicken oder **F5** drücken.

Steuerung: **Leertaste** = Pause · **1–4** = Tempo 1×/2×/4×/8×

### Tests ausführen
Im Terminal (in VS Code: *Terminal → Neues Terminal*) im Ordner `reichsgilde`:

```
dotnet run --project tests/Reichsgilde.Core.Tests
```

Am Ende muss stehen: `Alle … Tests bestanden.`

## Lizenz

Code: [MIT](LICENSE). Dieses Projekt verwendet **keine** Grafiken, Sounds, Texte oder sonstigen Inhalte aus „Die Gilde“ – das Original dient nur als Vorbild für Spielmechaniken.
