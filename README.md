# Reichsgilde

*Arbeitstitel* – eine Wirtschaftssimulation und Dynastie-Sandbox im 2,5D-Stil, inspiriert von **„Die Gilde“ (Europa 1400)**.
Schauplatz ist ein düster-bodenständiges Heiliges Römisches Reich des Spätmittelalters, in dem Menschen, Zwerge, Elben und Halblinge als Bürger leben und Orks und Oger als Bestien vor den Toren lauern.

> Hobby- und Lernprojekt · Open Source (MIT) · Godot 4.7 + C# · **Windows & Linux**

[![Build, Tests & Export](https://github.com/B411Y/reichsgilde/actions/workflows/build.yml/badge.svg)](https://github.com/B411Y/reichsgilde/actions/workflows/build.yml)

## Aktueller Stand

**Phase 3 – 2,5D-Stadtansicht (Greybox).** Die Reichsstadt ist als begehbare Schrägansicht zu sehen: Mauer, Osttor, Markt, Rathaus, Dom, Gießereien und die Grube vor der Stadt – noch aus einfachen Grundformen. Die orthografische Kamera blickt diagonal (nach Nordosten, Nordwesten, Südwesten oder Südosten) und dreht in 90°-Schritten. Karren fahren sichtbar zum Markt, Licht und Himmel folgen der Spieluhr, die Esse glüht bei Arbeit, Gebäude lassen sich anklicken.

**Diorama-Innenansicht.** Ein Doppelklick auf eine Gießerei schneidet sie wie ein Puppenhaus auf: Dach und die beiden Wände zur Kamera verschwinden, Nachbarhäuser davor werden ausgeblendet. Innen sieht man Esse, Amboss und Werkbank, das Lager als Kistenstapel (eine Kiste je 5 Stück) und die Arbeiter, die während der Arbeitszeit hämmern – alles direkt aus der Simulation.

**Hauptmenü, Speichern und Laden.**
- Das Spiel startet im Hauptmenü. Unter „Neue Partie“ wählst du Hausname, Siegbedingung, Dauer eines Spieljahres, Startstatut, Startvermögen, den Charakter des Rivalen und den Zufallsstartwert.
- Im Spiel öffnet **Esc** das Spielmenü: Weiterspielen, Speichern, Laden, Hauptmenü, Beenden.
- Nach jedem Jahresbericht wird automatisch gespeichert.
- Ein geladener Spielstand läuft exakt so weiter, wie er gespeichert wurde.

**Expansion.** Als Bürger kannst du eine zweite Werkstatt kaufen (Gießerei an der Südgasse) oder eine Grube pachten (am Silberberg oder im Tannenwald). Freie Plätze sind in der Stadt mit Zaun und Schild „zu verkaufen“ bzw. „zu verpachten“ markiert. Ein Klick darauf zeigt Preis, Pacht und den erwarteten Gewinn. Jede Liegenschaft gibt es nur einmal, und Haus Eisenbart greift ebenfalls zu.

**Dynastie.** Dein Haus besteht aus Menschen, Zwergen, Elben oder Halblingen. Volk, Geschlecht und Vorname des Oberhaupts wählst du im Hauptmenü. Jedes Jahr altern alle; mit dem Alter steigt das Sterberisiko. Über „Familie“ im Kontor öffnest du die Familientafel. In der **Brautschau** wirbst du mit Geschenken um ledige Bürger der Stadt, nur standesgemäß und mit etwas Geduld, denn mehr als 20 Zuneigung im Jahr gewinnt niemand. Neben jedem Kandidaten steht, wie viele Kinder die Ehe erwarten lässt. Ehen zwischen Völkern sind erlaubt, aber weniger fruchtbar, und sie wirken sich im Rat aus. Stirbt das Oberhaupt, erbt das im **Testament** bestimmte Kind, sonst das älteste volljährige. Gibt es keines, führt nach dem Witwenrecht der Ehepartner das Haus weiter. Sonst erlischt das Haus, und die Partie ist verloren. Die Völker unterscheiden sich deutlich: Elben leben sehr lange, bekommen aber selten Kinder. Halblinge bekommen schnell Kinder, sterben aber früher. Haus Eisenbart ist ein Zwergenhaus und sucht nach denselben Regeln eine Partie.

**Stadtgericht.** Jedes Geschenk an einen Ratsherrn ist Bestechung und hinterlässt eine Spur, bei dir wie bei Haus Eisenbart. Im Rathaus öffnet „Gerichtssaal öffnen“ das Gericht. Dort setzt du einen **Spitzel** auf ein anderes Haus an. Er deckt am Jahresende Spuren auf, die in deinem **Beweisbuch** landen. Mit Beweisen erhebst du **Anklage**. Den Vorsitz führt der Stadtrichter, dazu kommen zwei ausgeloste Schöffen. Eine Prognose zeigt, wie jeder Richter heute urteilen würde. Am **Gerichtstag** zum Jahresende, noch vor der Ratssitzung, fällt das Urteil. Bei einem Schuldspruch gibt es eine Buße (das Dreifache der Bestechung), der Verurteilte verliert sein Amt und steht drei Jahre am Pranger, was ihn Ansehen kostet. Bei einem Freispruch entschädigt der Ankläger den Beklagten. Geschenke an die Richter können einen knappen Fall kippen, ab drei Beweisen aber nicht mehr. So wird auch ein bestechlicher Bürgermeister angreifbar. Haus Eisenbart setzt ebenfalls Spitzel an, klagt, wenn die Beweise sicher reichen, verteidigt sich und besticht vorsichtiger, wenn man ihm auf die Finger schaut.

**Stadtrat.** Im Rathaus öffnet „Ratskammer öffnen“ den Stadtrat: sieben Ämter in einer Pyramide, vom Zöllner bis zum Bürgermeister, jedes mit Amtseinkommen. Die Sitze halten zu Beginn Ratsherren der Stadt (Menschen, Zwerge, Elben, Halblinge), jeder mit einer eigenen Haltung zum Fron- und Sklavenstatut. Ab dem Stand Bürger kannst du dich um ein Amt bewerben, immer eine Stufe über deinem jetzigen. Gewählt wird am Jahresende, und zwar von den Inhabern der höheren Ämter. Mit Geschenken gewinnst du die Gunst einzelner Ratsherren; sie verblasst jedes Jahr zum Teil. Eine Prognose zeigt dir, wie die Wahl heute ausginge. Wer im Rat sitzt, kann eine Änderung des Statuts beantragen, über die der ganze Rat abstimmt. Haus Eisenbart spielt dabei mit: Es bewirbt sich, verschenkt Gulden und stellt Anträge, wenn sich das für seine Betriebe rechnet. Was in der Ratssitzung geschah, steht im Jahresbericht.

**Stände, Rangliste und Bankrott.** Jedes Haus beginnt als Gemeiner. Ab 10.000 Gulden kann man im Rathaus das Bürgerrecht erwerben, danach geht es weiter über Patrizier und Edler bis zum Reichsgrafen. Nach jedem Jahr zeigt ein Jahresbericht Gewinn und Verlust, den Fortschritt zum nächsten Stand und die Rangliste gegen Haus Eisenbart. Wer drei Jahresabschlüsse in Folge im Minus steht, ist bankrott.

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
│   ├── Dynastie/                   Personen, Familien, Brautschau, Geburt, Tod und Erbfolge
│   ├── Justiz/                     Stadtgericht: Spuren, Spitzel, Beweise, Anklage, Urteil
│   ├── Ki/                         KI-Rivalen (Entscheidungen und ihr Charakter)
│   ├── Partie/                     Partie-Einstellungen, Siegbedingung, Aktionspunkte
│   ├── Politik/                    Stadtrat: Ämter, Wahlen, Gunst, Anträge zum Statut
│   ├── Speichern/                  Spielstände als JSON
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
2. Oben rechts auf **▶ (Projekt ausführen)** klicken oder **F5** drücken – das Hauptmenü erscheint.

Spielstände liegen unter `%APPDATA%\Godot\app_userdata\Reichsgilde\spielstaende` (Windows) bzw. `~/.local/share/godot/app_userdata/Reichsgilde/spielstaende` (Linux).

Steuerung:
- **Leertaste** = Pause · **1–4** = Tempo 1×/2×/4×/8× · **5–6** = Testtempo 16×/32× (nur während der Entwicklung)
- **Mausrad** = Zoom · **rechte Maustaste ziehen** oder **WASD/Pfeiltasten** = verschieben · **Q/E** = drehen
- **Linksklick** auf ein Gebäude = Informationen (bei eigenen Betrieben mit Bedienfeld) · **Doppelklick** auf eine Gießerei = Innenansicht
- **Esc** = Innenansicht verlassen → Auswahl schließen → Spielmenü (Speichern, Laden, Hauptmenü)

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
