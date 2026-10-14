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

**Talente und Ausbildung.** Jede Person hat fünf Talente mit 0 bis 6 Sternen: **Verhandeln** (bessere Markterlöse), **Handwerkskunst** (mehr Leistung aller Arbeitskräfte), **Bei Nacht und Nebel** (Gaunerstücke gelingen öfter, Zeugen sehen weniger), **Kampf** (stärkere Wegelagerer, besser geschützte eigene Karren) und **Rhetorik** (Ratswahlen, Plädoyer vor Gericht, Brautgeschenke). Es zählen die Talente des Oberhaupts, der Erbe ist also eine echte Entscheidung. Die Völker bringen Anlagen mit: Zwerge sind die besten Handwerker, Elben die besten Redner, Halblinge die geschicktesten Gauner. Kinder erben die Hälfte vom Mittel der Eltern, den Rest von ihrem Volk, und die Brautschau zeigt die Stärken jedes Kandidaten. In der zweiten Hälfte der Kindheit lernen Kinder in einer **Ausbildung** dazu: Zunftlehre, Kontorlehre, Klosterschule, Fechtmeister, Universität (ab Bürger) oder die Gasse. Haus Eisenbart bildet seine Kinder ebenfalls aus und setzt das begabteste als Erben ein.

**Berufe: Schmied, Tischler, Steinmetz und Alchimist.** Im Hauptmenü wählt man den Startberuf. Der **Schmied** fördert in der Grube Eisen und Silber und macht in der Gießerei Beschläge, Rapiere und Silberringe. Der **Tischler** schlägt im Holzschlag Holz und macht in der Tischlerei Holzzapfen, Schemel und Truhen – für eine Truhe braucht er einen Beschlag vom Schmied. Der **Steinmetz** bricht im Steinbruch Bruchstein und Granit und macht in der Maurerei Schleifziegel, Grabsteine und Mühlsteine; seine Schleifziegel braucht die Goldschmiede für das Prunkschwert, er selbst braucht in der Steinmetzhütte Werkzeug vom Schmied. Der **Alchimist** zieht im Kräutergarten Kräuter und fängt Kröten und macht in der Tinkturei Farbe, Kräuterschnaps und Heilsalbe; seine Farbe braucht der Tischler für den Schrank. Wer das Handwerk beherrscht, mischt Krötenschleim im Hinterzimmer selbst (halber Preis, höhere Chance). Einen weiteren Beruf erlernt man im Kontor (500 Gulden Lehrgeld, 4 AP); erst dann darf man dessen Werkstätten und Umlandbetriebe erwerben. Neben Haus Eisenbart (Zwerge, Schmiede) spielen **Haus Eichwald** (Elben, Tischler) und **Haus Kieselbach** (Halblinge, Steinmetze) mit; im Hauptmenü lassen sich 1–3 Rivalen wählen. Ist ein KI-Haus Patrizier geworden, darf es einen Betrieb mehr führen und dafür ein zweites Handwerk erlernen. Werkstätten sind in `wirtschaft.json` unter `betriebsarten` und `berufe` beschrieben – ein weiterer Beruf ist vor allem ein Dateneintrag plus ein Platz in der Stadt.

**Marktplatz.** Ein Klick auf den Marktplatz in der Stadt (oder der Knopf „Marktplatz“ im Kontor) öffnet das Marktfenster: alle Waren als Kacheln mit Pixel-Icon und Preis, nach Rohstoffen, Zwischenwaren und Fertigwaren geordnet. Grün ▲ heißt über dem Grundpreis, rot ▼ darunter; die gelbe Zahl im Icon ist der eigene Lagerbestand, der Tooltip zeigt Herkunft, Nachfrage, Zutaten und Gewinn je Arbeitsminute. Neue Icons: Zeichenfunktion in `tools/icons/waren_icons.py` ergänzen und `python3 tools/icons/waren_icons.py` ausführen.

**Meistergrade und Gebäudestufen.** Jedes Stück, das die Betriebe eines Hauses herstellen oder fördern, bringt Erfahrung im Beruf. Reicht sie, legt man im Kontor die Meisterprüfung ab (Gebühr und 2–4 AP) – bis zum 6. Grad. Ab dem 3. Grad lässt sich die Werkstatt am Platz zur Stufe 2 ausbauen (Schmiede bzw. Drechslerei, 3 Plätze), ab dem 5. Grad zur Stufe 3 (Goldschmiede bzw. Sägewerk, 4 Plätze); der Umbau kostet Gulden und einen halben Arbeitstag. Höhere Stufen stellen wertvollere Waren her: Werkzeug, Silberkette, Goldkette, Prunkschwert; Gehstock, Armbrust, Schrank, Prunktruhe; Säule, Taufbecken, Steinfigur, Grabmal; Rosenwasser, Gegengift, Theriak, Zinnober (Gold gibt es nur auf dem Markt). Der Steinmetz baut zur Steinschneiderei und Steinmetzhütte aus. Der 6. Grad bringt einen Aktionspunkt mehr je Jahr. Grad und Erfahrung gehören dem Haus und gehen beim Erbfall nicht verloren.

**Gesundheit und Krankheiten.** Jedes Jahr erkrankt etwa jede siebte Person: Husten, Ruhr, Lungenentzündung, Knochenbruch, Vergesslichkeit, Angst im Dunkeln, Haarausfall oder Lachanfälle. Krankheiten kosten Talente oder Ansehen, Ruhr und Lungenentzündung können tödlich sein. Alle 25–40 Jahre kommt die **Pest**: Viele erkranken, ein Teil stirbt (Zwerge und Elben sind weniger anfällig), und alle Betriebe arbeiten ein Jahr lang 30 % langsamer. Ein **Medicus** (Gulden) heilt sofort bzw. halbiert das Pestrisiko; **Kräuterschnaps** vom Markt kommt in die Hausapotheke und heilt leichte Krankheiten. Im Hinterzimmer lässt sich einem fremden Oberhaupt **Krötenschleim** ins Essen mischen – das Vergehen heißt Giftmischerei.

**Aktionspunkte.** Jedes Haus bekommt zum Jahreswechsel **4 Aktionspunkte** (im Hauptmenü 1–12 einstellbar) und kann bis zu 12 ansparen. Persönliche Handlungen kosten AP: Werben, Heiraten, einen Ratsherrn beschenken, sich bewerben, einen Spitzel ansetzen, eine Bande anheuern (je 1), Statut-Antrag, Anklage, Erpresserbrief (je 2). Mit übrigen AP übt das Oberhaupt ein Talent: ½ Stern je Übung, je Talent einmal im Jahr; es kostet 2 AP unter 3 Sternen, 4 AP unter 5 und 6 AP darüber. Die Wirtschaft (Betriebe, Personal, Karren, Wächter, Stand, Liegenschaften) bleibt frei. Haus Eisenbart hat dieselben AP und setzt Prioritäten nach seinem Charakter.

**Gauner.** Über „Hinterzimmer“ im Kontor heuerst du Banden gegen andere Häuser an. Ausgeführt wird bei Nacht und Nebel um 21 Uhr:
- **Diebstahl:** Die Bande holt die wertvollste Ware aus einem Lager, der Hehler zahlt dir die Hälfte des Marktwerts.
- **Sabotage:** Ein Betrieb steht einen halben Arbeitstag still, und das Opfer zahlt die Reparatur.
- **Überfall:** Wegelagerer lauern der nächsten beladenen Umlandfahrt auf. Die Eskorte schützt wie gewohnt.

Hast du mindestens 3 Beweise, kannst du **Schweigegeld erpressen**. Der Brief selbst ist allerdings ein Beweis gegen dich. Jedes Gaunerstück hinterlässt eine Spur fürs Gericht, und manchmal erkennt dich ein Zeuge. Überfall und Erpressung bringen vor Gericht ein Jahr **Kerker**. In dieser Zeit laufen nur deine Betriebe weiter. Gegen Gauner hilft ein **Wächter** je Betrieb: Er halbiert die Chance der Diebe und sieht öfter, wer es war. Haus Eisenbart greift je nach Charakter selbst zu solchen Mitteln: vorsichtig nie, ausgewogen selten, wagemutig oft.

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
│   ├── assets/waren/               Pixel-Icons der Waren (32 × 32, erzeugt von tools/icons)
│   ├── scenes/                     Szenen (.tscn)
│   └── scripts/                    C#-Skripte, die an Szenen hängen
├── src/Reichsgilde.Core/           Simulationskern – die reine Spiellogik, OHNE Godot
│   ├── Simulation.cs
│   ├── Dynastie/                   Personen, Familien, Brautschau, Geburt, Tod und Erbfolge
│   ├── Gauner/                     Unterwelt: Diebstahl, Sabotage, Überfall, Erpressung, Wächter
│   ├── Justiz/                     Stadtgericht: Spuren, Spitzel, Beweise, Anklage, Urteil, Kerker
│   ├── Ki/                         KI-Rivalen (Entscheidungen und ihr Charakter)
│   ├── Partie/                     Partie-Einstellungen, Siegbedingung, Aktionspunkte
│   ├── Politik/                    Stadtrat: Ämter, Wahlen, Gunst, Anträge zum Statut
│   ├── Speichern/                  Spielstände als JSON
│   ├── Wirtschaft/                 Markt, Betriebe, Lager, Karren, Überfälle, Konten, Fronstatut
│   └── Zeit/                       Spielzeit, Taktgeber, Kalender
├── tests/Reichsgilde.Core.Tests/   Automatische Tests für den Simulationskern
├── tools/icons/waren_icons.py      Zeichnet die Waren-Icons (Python + Pillow)
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
- **F11** = Vollbild ein/aus (auch im Haupt- und Spielmenü; wird gespeichert). Das Fenster lässt sich frei ziehen: Ein größeres Fenster zeigt mehr Stadt, Tafeln, die nicht hineinpassen, bekommen Scrollbalken (Mausrad über der Tafel rollt sie).
  Hinweis: Startet man das Spiel im Godot-Editor, läuft es im Reiter „Game“ eingebettet. Dort bestimmt der Editor die Fenstergröße, und Vollbild ist nicht möglich. Zum Testen im eigenen Fenster das Einbetten im Reiter „Game“ ausschalten.

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

Code: [MIT](LICENSE). Die Waren-Icons sind selbst gezeichnet (`tools/icons/waren_icons.py`) und stehen unter derselben Lizenz. Dieses Projekt verwendet **keine** Grafiken, Sounds, Texte oder sonstigen Inhalte aus „Die Gilde“ – das Original dient nur als Vorbild für Spielmechaniken.
