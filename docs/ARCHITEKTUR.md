# Architektur – wie Reichsgilde aufgebaut ist

Dieses Dokument erklärt die Grundidee hinter dem Code. Es wächst mit dem Projekt.

## Die wichtigste Regel: Simulation und Darstellung sind getrennt

```
┌──────────────────────────────┐        ┌──────────────────────────────┐
│  game/  (Godot)              │        │  src/Reichsgilde.Core        │
│                              │ ruft   │                              │
│  • zeigt an                  │ ─────▶ │  • entscheidet, was passiert │
│  • nimmt Eingaben entgegen   │        │  • kennt Godot NICHT         │
│  • Szenen, Grafik, Sound     │ ◀───── │  • Zeit, Markt, Bürger, ...  │
│                              │ liest  │                              │
└──────────────────────────────┘        └──────────────────────────────┘
                                                      ▲
                                                      │ prüft
                                        ┌──────────────────────────────┐
                                        │  tests/ (automatische Tests) │
                                        └──────────────────────────────┘
```

**Warum?**

1. **Multiplayer:** Später läuft der Simulationskern auf einem Server, die Spieler sehen nur die Darstellung. Weil der Kern Godot nicht kennt, können wir ihn dort unverändert einsetzen.
2. **Testbarkeit:** Spiellogik lässt sich in Sekunden prüfen, ohne das Spiel zu starten (z. B. „Ergeben 15 Minuten wirklich genau ein Jahr?“).
3. **Übersicht:** Wer eine Spielregel sucht, schaut in `src/`. Wer wissen will, wie etwas aussieht, schaut in `game/`.

## Das Zeitsystem

| Begriff | Bedeutung |
|---|---|
| **Tick** | Eine Spielminute. Kleinste Zeiteinheit der Simulation. |
| **Spieltag = Runde = Spieljahr** | 1440 Ticks, beginnt um 06:00 Uhr (wie im Original „Die Gilde“). |
| **Jahreszeit** | Wechselt von Jahr zu Jahr: Frühling 1400 → Sommer 1401 → Herbst 1402 → Winter 1403 → … |
| **Tagesphase** | Zunftglocke (06–11), Hoher Mittag (11–16), Vesper (16–21), Dunkle Nacht (21–06) |
| **Taktgeber** | Rechnet Echtzeit in Ticks um. Hier sitzen Rundendauer, Tempo und Pause. |

Die Klasse `Spielzeit` weiß **nichts** über Echtzeit. Ob eine Runde 5 oder 30 Minuten dauert, ändert nur, *wie schnell* Ticks kommen – nicht, *was* in einem Tick passiert. Dadurch läuft die Simulation bei jedem Tempo gleich ab.

### Ablauf pro Bild (ca. 60× pro Sekunde)

```
Godot: _Process(delta)
  └─▶ Simulation.Aktualisieren(delta)
        └─▶ Taktgeber: „delta Sekunden = n Ticks“
              └─▶ n × Simulation.Tick()
                    ├─ Zeit eine Minute weiter
                    ├─ (später: Produktion, Bürger-KI, Markt ...)
                    └─ Ereignisse: PhaseBegonnen, JahrBeendet
  └─▶ Anzeige aktualisieren
```

## Die Wirtschaft

Alle Zahlen stehen in **`game/data/wirtschaft.json`**, nicht im Code. So lässt sich das Spiel ausbalancieren, ohne zu programmieren.

| Baustein | Aufgabe |
|---|---|
| `Wirtschaftsdaten` | Liest die JSON-Datei und prüft sie. Fehler werden gesammelt und verständlich gemeldet. |
| `Markt` / `Marktstand` | Preis = Grundpreis × (Nachfrage ÷ Angebot)^Elastizität. Das Angebot umfasst die Grundversorgung durch Händler, die keine Spieler sind, plus die Verkäufe der Spieler in den letzten 24 Spielstunden (= 1 Jahr). |
| `Betrieb` | Gießerei oder Mine mit Lager (40 je Ware). Arbeitet von 08:00 bis 20:00 Uhr. Fehlen Zutaten oder ist das Lager voll, ruht die Arbeit. |
| `Karren` | Pendelt automatisch zwischen Betrieb und Markt. Er fährt los, wenn genug Ware da ist, Zutaten knapp werden oder Ware 4 Stunden wartet. Am Markt verkauft er und kauft knappe Zutaten ein. Der Karren ist ein **Zustandsautomat**: am Betrieb → zum Markt → zurück. |
| `Ueberfall` | Beladene Umland-Fahrten können überfallen werden, nachts doppelt so oft. Die Täter sind Banden aus allen Völkern, oft gemischt, dazu wilde Ork-Rotten und Ogerhorden. Eine Eskorte senkt die Erfolgschance. |
| `Konto` | Vermögen und Jahresrechnung (Umsatz, Zutaten, Löhne, Fixkosten, Steuer, Schäden). |
| `Stadtwirtschaft` | Hält alles zusammen, kennt das geltende Fron- und Sklavenstatut und würfelt Ausbrüche und Aufstände. |

**Fairness:** Kommen mehrere Karren in derselben Minute am Markt an, ist die Reihenfolge jede Minute neu ausgelost. Sonst hätte immer der zuerst eröffnete Betrieb den besseren Preis, im Multiplayer also derjenige, der zuerst beitritt.

**Zufall mit festem Startwert:** Alle Zufallsereignisse kommen aus einem einzigen Zufallsgenerator mit festem Startwert (`Partieeinstellungen.Zufallsstartwert`). Gleicher Startwert und gleiche Eingaben ergeben exakt denselben Spielverlauf. Das ist die Voraussetzung für den Multiplayer, bei dem alle Rechner dasselbe sehen müssen, und für Tests, die sich wiederholen lassen.

## Namenskonventionen

- Fachbegriffe im Code sind **deutsch** (`Spielzeit`, `Jahreszeit`, `Zunftglocke`), passend zum Setting.
- Umlaute werden in Bezeichnern umschrieben (`Fruehling`), in Texten für Spieler bleiben sie erhalten.
- Kommentare sind deutsch und erklären das **Warum**, nicht nur das Was.

## Darstellung: 2,5D aus echten 3D-Modellen

Die Welt besteht aus 3D-Objekten, wirkt aber wie ein gezeichnetes 2,5D-Spiel. Das leisten vier Regeln:

1. **Orthografische Kamera** – keine Perspektive, keine Fluchtpunkte. Ein Haus ist vorne genauso groß wie hinten.
2. **Feste Neigung** – die Kamera schaut immer 40° schräg von oben.
3. **Feste Blickrichtungen** – diagonal zu den Himmelsrichtungen (Nordosten, Nordwesten, Südwesten, Südosten), gedreht wird nur in 90°-Schritten (Q/E). So sieht man an jedem Gebäude zwei Seiten und das Dach.
4. **Gespielt wird auf einer Ebene** – Höhen sind nur Kulisse.

**Diorama-Innenansicht:** Gebäude mit Innenraum (bisher die Gießerei) bestehen aus einzelnen Wänden, einem Dach und einem Knoten „Innen“ mit der Einrichtung. Beim Betreten wird das Dach ausgeblendet und jede Wand, deren Außenseite zur Kamera zeigt (Skalarprodukt > 0,5 – beim diagonalen Blick genau zwei). Dinge zwischen Kamera und Gebäude werden ebenfalls ausgeblendet. Was innen zu sehen ist (Kisten, Arbeiter, Glut), liest die Ansicht nur aus der Simulation – sie verändert nichts.

## KI-Rivalen

Ein KI-Rivale (`src/Reichsgilde.Core/Ki/KiRivale.cs`) führt ein Konto und alle Betriebe, die dazu gehören. Er gehört zum Simulationskern, läuft also im Multiplayer später auf dem Server und ist ohne Godot testbar.

**Grundsatz: keine Schummelei.** Die KI sieht nur, was auch der Spieler sieht: Marktpreise, die Spieldaten und ihr eigenes Konto. Sie kennt weder Zufallswürfe noch die Zukunft und nutzt dieselben Methoden wie der Spieler (`Einstellen`, `KarrenKaufen`, `Produkt`, `Eskorte`).

| Wann | Entscheidung | Rechnung |
|---|---|---|
| jede volle Stunde | Produkt der Gießerei | Gewinn je Arbeitsminute = (Preis × (1 − Steuer) − Zutaten zum Marktpreis) ÷ Zeit. Wechsel erst ab 15 % Vorsprung und frühestens nach 6 Stunden. |
| jede volle Stunde | Förderung der Mine | Silberanteil in Stufen 20/35/50/65/80 % nach dem Preisvorsprung von Silber gegenüber Eisen |
| jede volle Stunde | Personal | Freie Plätze mit der erlaubten Arbeitskraft mit dem höchsten Jahresertrag besetzen: Tempo × 720 Min. × Wert je Minute − Lohn − Risiko × Schaden |
| jede volle Stunde | Eskorte | Geringste erwartete Kosten: Eskortenlohn + Überfallchance × Erfolgschance der Banden × Ladungswert × Vorsicht |
| Jahresende | Karren kaufen | Wenn der Betrieb im Vorjahr mehr als 20 % der Arbeitszeit stillstand und die Reserve es erlaubt |

Das **Profil** (`KiProfil`) gibt jedem Rivalen einen Charakter: Geldreserve, Wechselschwelle, Vorsicht (wie stark er Raub fürchtet) und Höchstzahl an Karren. Über 20 Jahre verdient der Rivale mit KI rund 9 % mehr als ohne. Für den Spieler wird es entsprechend enger, weil die KI in seine Märkte drängt.

## Befehle: der einzige Weg, etwas zu ändern

Spieler und KI ändern ihre Betriebe nicht direkt, sondern schicken **Befehle** (`src/Reichsgilde.Core/Wirtschaft/Befehle.cs`) an `Stadtwirtschaft.Ausfuehren(absender, befehl)`:

```
Oberfläche / KI ──Befehl──▶ Ausfuehren ──prüft──▶ Betrieb ändert sich
                ◀──Befehlsergebnis (Erfolg + Satz für den Spieler)──
```

- **Eine Stelle prüft alles:** Gehört der Betrieb dem Absender? Reicht das Geld? Erlaubt das Statut die Arbeitskraft? Ist ein Platz frei?
- **Befehle tragen nur Namen und Zahlen** (z. B. `PersonalSetzen("Gießerei am Weinmarkt", "Geselle", 2)`), keine Objekte. Im Multiplayer schickt der Client genau diese Datensätze an den Server.
- **Die KI benutzt denselben Weg.** Damit ist „die KI schummelt nicht“ technisch gesichert.
- **Gemeinsame Rechnungen:** `Kalkulation` liefert Gewinn je Arbeitsminute, Jahresertrag je Arbeitskraft und erwartete Fahrtkosten. Die KI entscheidet danach, und das Bedienfeld zeigt dieselben Zahlen an.

## Stände und Bankrott

- Die Stände stehen als Liste in `wirtschaft.json` (Abschnitt `staende`, von unten nach oben). Jedes Konto merkt sich nur seine **Stufe** (`Konto.Stand`), die Stadtwirtschaft liefert Name, Schwelle und Gebühr dazu.
- Aufsteigen ist ein Befehl (`StandErwerben`) und kein Betriebsbefehl. Deshalb gibt es zwei Arten von Befehlen: `BetriebsBefehl` (braucht einen eigenen Betrieb) und Befehle an das Konto selbst.
- Schulden prüft die Stadtwirtschaft nach jeder Jahresabrechnung. Ein Bankrott löst das Ereignis `Bankrott` aus. Ob die Partie damit endet, entscheidet die Oberfläche, denn nur sie weiß, welches Konto der Mensch spielt. Sie ruft dann `Simulation.Beenden(grund)` auf.

## Liegenschaften und Stadtplan

- **Was es zu erwerben gibt**, steht in `wirtschaft.json` (Abschnitt `liegenschaften`: Name, Art, Kaufpreis, Mindeststand). Erworben wird mit dem Befehl `LiegenschaftErwerben(name)`. Die Stadtwirtschaft eröffnet dann einen Betrieb mit genau diesem Namen.
- **Wo etwas steht**, weiß nur die Darstellung: Der `Stadtplan` in `Stadtansicht.cs` ordnet jedem Betriebsnamen einen Platz zu. Der Simulationskern kennt keine Koordinaten. So bleibt er unabhängig von der Grafik und läuft im Multiplayer genauso auf dem Server.
- Ein neuer Bauplatz braucht also zwei Einträge: einen in `wirtschaft.json` und einen im `Stadtplan`.

**Lehrstück aus diesem Schritt (Godot):** Ein Knopf darf nicht in jedem Bild kurz unsichtbar und wieder sichtbar geschaltet werden. Godot bricht beim Ausblenden einen laufenden Klick ab, und ein Klick, dessen Drücken und Loslassen in verschiedene Bilder fallen, geht verloren. Deshalb erst alles ausrechnen und dann jede Eigenschaft einmal setzen.
