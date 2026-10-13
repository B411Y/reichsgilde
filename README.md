# Reichsgilde

*Arbeitstitel* – eine Wirtschaftssimulation und Dynastie-Sandbox im 2,5D-Stil, inspiriert von **„Die Gilde“ (Europa 1400)**.
Schauplatz ist ein düster-bodenständiges Heiliges Römisches Reich des Spätmittelalters, in dem Menschen, Zwerge, Elben und Halblinge als Bürger leben und Orks und Oger als Bestien vor den Toren lauern.

> Hobby- und Lernprojekt · Open Source (MIT) · Godot 4.7 + C# · **Windows & Linux**

[![Build, Tests & Export](https://github.com/B411Y/reichsgilde/actions/workflows/build.yml/badge.svg)](https://github.com/B411Y/reichsgilde/actions/workflows/build.yml)

## Aktueller Stand

**Phase 3 – 2,5D-Stadtansicht (Greybox).** Die Reichsstadt ist als begehbare Schrägansicht zu sehen: Mauer, Osttor, Markt, Rathaus, Dom, Gießereien und die Grube vor der Stadt – noch aus einfachen Grundformen. Die orthografische Kamera blickt diagonal (nach Nordosten, Nordwesten, Südwesten oder Südosten) und dreht in 90°-Schritten. Karren fahren sichtbar zum Markt, Licht und Himmel folgen der Spieluhr, die Esse glüht bei Arbeit, Gebäude lassen sich anklicken.

**Diorama-Innenansicht.** Ein Doppelklick auf eine Gießerei schneidet sie wie ein Puppenhaus auf: Dach und die beiden Wände zur Kamera verschwinden, Nachbarhäuser davor werden ausgeblendet. Innen sieht man Esse, Amboss und Werkbank, das Lager als Kistenstapel (eine Kiste je 5 Stück) und die Arbeiter, die während der Arbeitszeit hämmern – alles direkt aus der Simulation.

**Stände, Rangliste und Bankrott.** Jedes Haus beginnt als Gemeiner. Ab 10.000 Gulden kann man im Rathaus das Bürgerrecht erwerben, danach geht es weiter über Patrizier und Edler bis zum Reichsgrafen. Nach jedem Jahr zeigt ein Jahresbericht Gewinn und Verlust, den Fortschritt zum nächsten Stand und die Rangliste gegen Haus Eisenbart. Wer zwei Jahresabschlüsse in Folge im Minus steht, ist bankrott.

**Bedienung (Phase 4 beginnt).** Ein Klick auf deine Gießerei (oder „⚒ Betrieb verwalten“ im Kontor) öffnet das Bedienfeld: Produkt wählen, Personal einstellen und entlassen, Karren kaufen und für Umland-Fahrten Eskorte und Nachtfahrt festlegen. Neben jeder Wahl steht, was sie bringt (Gulden je Arbeitsminute, Jahresertrag je Kopf, erwartete Kosten je Fahrt) – dieselben Rechnungen, nach denen die KI entscheidet. Jede Aktion ist ein **Befehl**, den der Simulationskern prüft und mit Begründung ausführt oder ablehnt; die KI benutzt denselben Weg.

**Tag und Nacht.** Sonne und Mond wandern über den Himmel und werfen beide Schatten; in der Dämmerung blenden sie weich über.

**KI-Rivale.** Haus Eisenbart wird von einer KI geführt, die nach denselben Regeln und mit denselben Informationen spielt wie du (keine Schummelei). Sie stellt ihre Gießerei auf das jeweils einträglichste Produkt um, teilt die Förderung der Grube nach den Preisen auf, besetzt freie Plätze mit der besten erlaubten Arbeitskraft (auch nach einem neuen Statut), wählt Eskorten nach erwartetem Raubverlust und kauft Karren, wenn ein Betrieb zu oft stillsteht. Jede Entscheidung erscheint im Protokoll und im Kontor.

**Phase 2 – Simulationskern.** Die Spielzeit läuft (1 Runde = 1 Spieltag = 1 Spieljahr, Standard 15 Minuten), mit Pause, Tempo und wählbarer Siegbedingung. Die **Wirtschaft** ist eingebaut: Marktpreise nach Angebot und Nachfrage, Gießerei und Mine produzieren während der Arbeitszeit, Löhne, Pacht und Steuern laufen in die Jahresrechnung, und das Fron- und Sklavenstatut bestimmt, wer wo arbeiten darf. **Lager und Karren** bringen die Waren zum Markt; auf Umland-Wegen lauern **Banditen aller Völker**, gegen die Eskorten helfen. Das Kontor im Spiel zeigt Vermögen, Lager, Karren, Jahresrechnung und Marktpreise.

## Projektstruktur

```
reichsgilde/
├── Reichsgilde.sln                 Projektmappe – fasst alle drei C#-Projekte zusammen
├── game/                           Das Godot-Projekt (Darstellung, Szenen, Eingabe)
│   ├── project.godot
│   ├── Reichsgilde.csproj
│   ├── data/wirtschaft.json        Alle Zahlen des Wirtschaftsmodells (aus dem Balancing-Rechner)
│   ├── scenes/                     Szenen (.tscn)
│   └── scripts/                    C#-Skripte, die an Szenen hängen
├── src/Reichsgilde.Core/           Simulationskern – die reine Spiellogik, OHNE Godot
│   ├── Simulation.cs
│   ├── Ki/                         KI-Rivalen (Entscheidungen und ihr Charakter)
│   ├── Partie/                     Partie-Einstellungen, Siegbedingung, Aktionspunkte
│   ├── Wirtschaft/                 Markt, Betriebe, Lager, Karren, Überfälle, Konten, Fronstatut
│   └── Zeit/                       Spielzeit, Taktgeber, Kalender
├── tests/Reichsgilde.Core.Tests/   Automatische Tests für den Simulationskern
├── docs/                           Erklärungen und Entscheidungen
└── .github/workflows/              Automatische Prüfung auf Windows und Linux
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

Steuerung:
- **Leertaste** = Pause · **1–4** = Tempo 1×/2×/4×/8×
- **Mausrad** = Zoom · **rechte Maustaste ziehen** oder **WASD/Pfeiltasten** = verschieben · **Q/E** = drehen
- **Linksklick** auf ein Gebäude = Informationen (bei eigenen Betrieben mit Bedienfeld) · **Doppelklick** auf eine Gießerei = Innenansicht
- **Esc** = Innenansicht verlassen bzw. Auswahl schließen

### Tests ausführen
Im Terminal (in VS Code: *Terminal → Neues Terminal*) im Ordner `reichsgilde`:

```
dotnet run --project tests/Reichsgilde.Core.Tests
```

Am Ende muss stehen: `Alle … Tests bestanden.`

### Spiel exportieren (Windows / Linux)
1. Einmalig in Godot: **Editor → Exportvorlagen verwalten → Herunterladen und installieren**
2. **Projekt → Exportieren…** → Vorlage **Windows** oder **Linux** wählen → **Projekt exportieren**
3. Das fertige Spiel landet im Ordner `build/` (wird nicht ins Repository übernommen)

## Plattformen und automatische Prüfung

Reichsgilde läuft auf **Windows und Linux**. Bei jedem Pull Request prüft GitHub automatisch:

1. Bauen und Tests – auf Windows **und** Linux
2. Export für beide Systeme und ein kurzer Startversuch des Linux-Builds

Die fertigen Builds kann man unter **Actions → jeweiliger Lauf → Artifacts** herunterladen.
Die Konfiguration steht in [`.github/workflows/build.yml`](.github/workflows/build.yml).

## Lizenz

Code: [MIT](LICENSE). Dieses Projekt verwendet **keine** Grafiken, Sounds, Texte oder sonstigen Inhalte aus „Die Gilde“ – das Original dient nur als Vorbild für Spielmechaniken.
