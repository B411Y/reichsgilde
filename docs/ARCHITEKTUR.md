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

## Oberfläche und Fenstergröße

- **Feste Pixelgröße** (kein `stretch`-Modus): Die Oberfläche skaliert nicht mit. Ein größeres Fenster zeigt mehr von der Stadt, Schrift und Tafeln bleiben gleich groß. Eine Mindestgröße gibt es nicht.
- **`Rollbereich`** (`game/scripts/Rollbereich.cs`) liegt in jeder Tafel zwischen Rahmen und Inhalt (`Rollbereich.Einhuellen(tafel)` am Ende des Aufbaus). Er misst jedes Bild den Inhalt und setzt seine Mindestgröße auf min(Inhalt, Fenster − Rand). Passt alles, sieht die Tafel aus wie vorher; sonst erscheinen Balken, auch waagrecht. Eine feste Breite der Tafel wandert dabei auf den Inhalt, sonst könnte die Tafel nie schmaler werden.
- **Lehrstück 1:** Ecktafeln (Kontor, Auswahl) hängen an Ankern, nicht in einem Container. Godot lässt sie wachsen, aber nie schrumpfen. Der Rollbereich setzt ihre Größe deshalb selbst und hält die angeheftete Ecke fest (`GrowDirection.Begin`).
- **Lehrstück 2:** Container lassen Mausereignisse standardmäßig durch (`MouseFilter.Pass`). Ein Mausrad über einer Tafel landete deshalb in `_UnhandledInput` der Stadtansicht und zoomte die Kamera. Tafel und Rollbereich stehen jetzt auf `Stop`.
- **Marktfenster** (`MarktAnzeige`): Kacheln je Ware in Gruppen nach `typ` (Heilmittel zählen zu den Fertigwaren), sortiert nach Beruf, Stufe und Grundpreis. Preise, Farben, Lagerbestand und Tooltips werden zweimal je Sekunde angeglichen, nicht neu gebaut. Reine Anzeige – gehandelt wird über die Karren.
- **Waren-Icons** (`WarenIcons`): 32 × 32 Pixel-Art aus `res://assets/waren/<ware>.png`, Dateiname wie im Zeichenskript (Umlaute → ae/oe/ue). Angezeigt mit `TextureFilter.Nearest` in ganzzahligen Vielfachen (64 px), damit die Pixel scharf bleiben. Fehlt ein Icon, steht der Anfangsbuchstabe da – neue Waren in den Daten brechen also nichts. Die Icons zeichnet `tools/icons/waren_icons.py` aus Grundformen; Schattierung (Licht von links oben) und Kontur setzt das Skript automatisch.
- **Vollbild:** Autoload `Fenster` (`project.godot` → `[autoload]`) schaltet mit F11 um und speichert die Wahl in `user://einstellungen.cfg`.

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

## Stadtrat

Der Stadtrat (`src/Reichsgilde.Core/Politik/Stadtrat.cs`) gehört zur Stadtwirtschaft (`Stadtwirtschaft.Rat`). Ämter, Ratsherren und alle Zahlen stehen in `wirtschaft.json` im Abschnitt `politik`.

**Wer wen wählt.** Jeder Ratsherr gibt jedem Kandidaten Punkte und stimmt für den mit den meisten:

```
Punkte = Ansehen des Kandidaten + Gunst des Ratsherrn für ihn (+ Amtsbonus für den bisherigen Inhaber)
Ansehen eines Hauses     = Stand × 8 + Vermögen/1000 × 0,4   (höchstens 40)
Ansehen eines Ratsherrn  = 10 + Stufe seines Amtes × 6        (ein Bürgermeister ist schwer zu stürzen)
Gunst                    = Geschenk in Gulden × 0,05          (höchstens 100, verblasst um 30 % im Jahr)
```

Über ein Amt stimmen die Inhaber der höheren Ämter ab, beim Bürgermeister alle übrigen. Kandidaten stimmen nicht mit, bei Gleichstand entscheidet das Los.

**Anträge.** Ein Ratsherr stimmt einem Statut zu, das seiner Haltung entspricht. Ein Statut eine Stufe daneben unterstützt er erst ab einer Gunst von 25 für den Antragsteller. Es gilt die einfache Mehrheit; bei Gleichstand entscheidet der Bürgermeister.

**Ablauf am Jahresende** (in `Stadtwirtschaft.Jahresabrechnung`):

1. `Rat.EinkommenZahlen()` – Amtseinkommen, noch im alten Jahr verbucht
2. Konten abschließen, Schulden prüfen (ein bankrottes Haus verliert sein Amt)
3. `Rat.Sitzung()` – Abstimmung über den Antrag, dann Wahlen von oben nach unten, dann Verfall der Gunst
4. KI-Rivalen planen das neue Jahr

**Prognosen statt Geheimwissen.** `WahlPrognose` und `AbstimmungsPrognose` rechnen mit denselben Regeln wie die Sitzung. Die Ratskammer zeigt sie dem Spieler an, und die KI entscheidet danach. Sie verschenkt nur dann Gulden, wenn ihr Budget für eine Mehrheit reicht, und beantragt ein Statut nur, wenn es ihren erwarteten Jahresgewinn um mehr als `StatutVorteil` (200 Gulden) erhöht.

**Bewusst offen:** Häuser im Rat stimmen bei Wahlen für den bisherigen Inhaber. Sitzt Haus Eisenbart einmal auf dem Bürgermeisterstuhl, kommt man dort schwer heran. Gegenmittel wie Bestechung, Erpressung und das Gericht folgen später. Bei fremden Anträgen enthält sich der Spieler noch; das Abstimmen über die Oberfläche kommt ebenfalls später.

## Dynastie

Das Familienbuch (`src/Reichsgilde.Core/Dynastie/Familienbuch.cs`) gehört zur Stadtwirtschaft (`Stadtwirtschaft.Familien`). Alle Zahlen und Namen stehen in `wirtschaft.json` im Abschnitt `dynastie`.

- **Konto und Familie sind getrennt.** Das Konto (Vermögen, Betriebe, Stand) gehört dem *Haus*, die Familie liefert das Oberhaupt. Beim Erbfall wechselt nur das Oberhaupt, das Konto bleibt. Ein Konto ohne Familie (alte Spielstände, Tests) altert nicht und erlischt nie.
- **Personen haben Nummern.** Befehle wie `Werben(17, 200)` oder `ErbenBestimmen(23)` und der Spielstand verweisen über die Nummer auf eine Person, nicht über den Namen.
- **Sterben nach Gompertz.** Das Sterberisiko wächst exponentiell mit dem Alter:

  ```
  Risiko im Jahr = 1 − e^−(Grundrisiko + H(Alter+1) − H(Alter)),   H(x) = e^((x − Sterbealter) / Streuung)
  ```

  Ein Mensch (Sterbealter 64, Streuung 8) stirbt im Jahr mit 25 Jahren zu etwa 0,7 %, mit 50 zu 3 %, mit 60 zu 8 % und mit 70 zu 25 %. Die Oberfläche zeigt nur eine grobe Verfassung („rüstig“, „betagt“ …), denn niemand kennt sein Todesjahr.
- **Erwartete Kinder** rechnet `ErwarteteKinder` Jahr für Jahr aus: Geburtenrate × Chance, dass beide Partner noch leben. Brautschau-Anzeige und KI benutzen dieselbe Rechnung.
- **Eigener Zufallsgenerator.** Leben und Tod würfelt ein eigener Generator. So verändert die Familie nicht die Würfe der Wirtschaft (Überfälle, Ausbrüche), und frühere Messungen bleiben vergleichbar.

**Ablauf am Jahresende** (in `Stadtwirtschaft.Jahresabrechnung`):

1. `Rat.EinkommenZahlen()` und `Familien.UnterhaltZahlen()`, noch im alten Jahr verbucht
2. Konten abschließen, Schulden prüfen
3. `Familien.Jahreswechsel()`: Todesfälle, Erbfall, Geburten, Brautschau, verblassende Zuneigung
4. `Rat.Sitzung()`: Ein Amt, das durch einen Erbfall frei wurde, wird gleich neu besetzt.
5. KI-Rivalen planen das neue Jahr.

**Erbfolge:** Testament → ältestes volljähriges Kind → Witwenrecht (Ehepartner, abschaltbar) → das Haus erlischt. Der Erbe behält Vermögen, Betriebe und Stand. Amt und Gunst im Rat beginnen von vorn (`Stadtrat.Neuanfang`).

**Volksnähe im Rat:** Ratsherren geben einem Haus, dessen Oberhaupt ihrem Volk angehört, 5 Punkte mehr. Eine Ehe zwischen Völkern kostet 5 Punkte beim eigenen Volk und bringt 3 Punkte beim Volk des Ehepartners.

**Talente** (`Dynastie/Talente.cs`, Abschnitt `dynastie.talente`):
- Jede `Person` trägt fünf Talente (0–6 Sterne, halbe Schritte). Es wirkt immer das Talent des **Oberhaupts**. Die Wirkung fragen die anderen Teile beim Familienbuch ab (`VerkaufsFaktor`, `LeistungsFaktor`, `RhetorikPunkte`, `Plaedoyer`, `NachtBonus`, `ZeugenFaktor`, `KampfUeberfall`, `KampfSchutz`). Ohne Familie oder ohne Talentdaten liefern sie neutrale Werte.
- **Woher:** Erwachsene, die neu ins Spiel kommen: Grundwert des Volkes ± 1. Kinder: ½ × Mittel der Eltern + ½ × Grundwert ± ½. Dazu kommt die Ausbildung.
- **Ausbildung:** ab der Hälfte der Volljährigkeit (Mensch: 8 bis 16). Der Zuwachs ist auf 8 Ausbildungsjahre ausgelegt und wird je Volk umgerechnet (`Ausbildungsfaktor`), damit jedes Volk am Ende gleich viel gelernt hat. Kosten fallen nur im Ausbildungsalter an und stehen unter „Familie“ in der Jahresrechnung.
- **Eigener Zufallsgenerator** für Talente: So bleiben Geburten, Todesfälle und Kandidaten genau wie vor den Talenten, und frühere Messungen bleiben vergleichbar.
- **KI:** Das älteste Kind geht auf die Universität, wenn das Haus Bürger ist und sie bezahlen kann, sonst in die Klosterschule. Die anderen gehen in die Zunftlehre, und ein wagemutiges Haus schickt das jüngste auf die Gasse. Erbe wird das begabteste volljährige Kind.

**Lehrstück aus diesem Schritt (Anzeige und Rundung):** 29,6 sieht gerundet aus wie 30. Steht dann „30 / 30“ da, aber die Heirat geht nicht, wirkt das Spiel kaputt. Fortschritt zu einer Schwelle wird deshalb abgerundet angezeigt.

**Lehrstück aus diesem Schritt (Zufall in Tests):** Ein Test mit einem einzigen Startwert prüft eher Glück als Regeln. Ein 20-jähriger Ehemann starb im Test gleich im ersten Jahr, bei 0,6 % Risiko. Eine Gegenprobe über 2000 Startwerte ergab genau die erwarteten 13 Todesfälle. Tests, die vom Zufall abhängen, laufen deshalb über mehrere Startwerte und prüfen eine Summe.

## Stadtgericht

Das Stadtgericht (`src/Reichsgilde.Core/Justiz/Stadtgericht.cs`) gehört zur Stadtwirtschaft (`Stadtwirtschaft.Justiz`). Alle Zahlen stehen in `wirtschaft.json` im Abschnitt `justiz`.

- **Spur → Beweis → Anklage → Urteil.**
  - Eine Tat (bisher nur Bestechung: jedes `Schenken` an einen Ratsherrn) erzeugt eine **Spur**, die zunächst niemand kennt.
  - Ein Spitzel (`SpitzelAnsetzen`) deckt am Jahresende jede unbekannte Spur des beobachteten Hauses mit 50 % Chance auf. Aus der Spur wird ein **Beweis** im Beweisbuch des Auftraggebers.
  - `Anklagen` verwendet alle Beweise gegen das Haus.
- **Urteil:** Jeder der drei Richter (Vorsitz + zwei ausgeloste Schöffen) rechnet

  ```
  Überzeugung = 10 × Beweise + Gunstwirkung,   Gunstwirkung = ½ × (Gunst für den Ankläger − Gunst für den Beklagten), höchstens ±10
  schuldig ab 15, die Mehrheit entscheidet
  ```

  Für die Gunst gibt es kein eigenes System, sie kommt aus dem Rat. Geschenke an Richter wirken also, sind aber selbst Bestechung. Die **Höchstwirkung** sorgt dafür, dass Bestechung nur knappe Fälle kippt: Ab Beweiskraft 25 (3 Beweise) ist ein Schuldspruch sicher. Ist der Stadtrichter selbst Partei, führt der Bürgermeister den Vorsitz.
- **Gerichtstag** zu Beginn der Jahresabrechnung, vor Amtseinkommen und Ratssitzung:
  1. Urteile (Buße, Amtsverlust, Pranger bzw. Entschädigung). Die Buße rechnet nur `Stadtgericht.Busse`: Faktor × Tatwerte, mindestens `mindestbusse`, höchstens `busseHoechstensAnteil` × Vermögen des Verurteilten. Gericht, Oberfläche und KI nutzen dieselbe Methode. Ohne den Deckel schaukelten sich wagemutige KI-Häuser mit Erpresserbriefen hoch (60 % der drohenden Strafe, die frühere Forderungen mitzählt) – bis zu Bußen über 100.000 Gulden.
  2. Spitzelberichte
  3. Verjährung nach 5 Jahren

  Wer verurteilt wird, bekommt also kein Amtseinkommen mehr, und sein Amt wird in derselben Nacht neu besetzt.
- **Kein Doppelverfahren:** Verhandelte Taten sind erledigt, egal ob schuldig oder nicht.
- **Pranger:** `Stadtrat.Ansehen` zieht die Schande ab. Das wirkt auf Wahlen und Brautschau gleichermaßen.
- **Eigener Zufallsgenerator** für Spitzel und Schöffen, wie bei der Familie.

**KI vor Gericht:** Die KI nutzt nur öffentliches Wissen.
- Ob am letzten Gerichtstag ein Spitzel über sie berichtet hat, „spricht sich herum“ (`WurdeBeobachtet`). Dann rechnet sie jede Bestechung mit erwarteter Buße: Kosten × (1 + Bußfaktor × Spitzelchance). Sie besticht nur, wenn drei Jahre Nutzen das übersteigen.
- Sie klagt erst bei sicherer Beweiskraft.
- Ist sie angeklagt, beschenkt sie die billigsten Richter, wenn eine Mehrheit erreichbar ist und sich die Verteidigung lohnt.

**Lehrstück aus diesem Schritt (Balance messen statt raten):** Die erste Fassung ergab 62 Freisprüche zu 1 Schuldspruch, weil die reiche KI sich jedes Mal freikaufte. Die zweite ergab 97 Schuldsprüche, weil die KI stur weiter bestach. Erst die Höchstwirkung der Gunst und die vorsichtige KI ergaben ein Gericht, das wirkt, ohne alles zu beherrschen. Messen lässt sich das mit `dotnet run --project tests/Reichsgilde.Core.Tests -- messen 30 20 Mensch gericht`.

**Lehrstück aus diesem Schritt (Godot-Knoten nicht über einen Neuaufbau retten):** Familientafel und Ratskammer hängten ein einziges Zahlenfeld bei jedem Aufbau um. Wurde die Brautschau einmal nicht aufgebaut, blieb das Feld in einer alten Zeile stecken und wurde mit ihr gelöscht. Später griff die Tafel ins Leere und stürzte ab. Seitdem merkt sich die Tafel nur den Wert, und das Feld entsteht jedes Mal neu.

## Berufe und Betriebsarten

- **Daten statt Code:** `betriebsarten` beschreibt jede Art (Name, Beruf, Ort, Plätze, Fixkosten, `produkte` für Werkstätten bzw. `rohstoffe` für Umlandbetriebe, Startkarren), `berufe` ordnet jedem Beruf seine Werkstatt und seinen Umlandbetrieb zu. `Wirtschaftsdaten.Art(art)` liefert den Eintrag. Ältere Daten ohne den Abschnitt (alte Spielstände bringen ihre Daten mit) bekommen Gießerei und Grube aus `allgemein` gebaut, damit alles weiterläuft.
- **Betrieb:** `IstWerkstatt` stellt das gewählte Produkt her, sonst werden die `Rohstoffe` gefördert. Bei zwei Rohstoffen teilt `Silberanteil` die Arbeitszeit (Name aus alten Spielständen; gemeint ist der zweite Rohstoff). Kalkulation, KI und Oberfläche fragen nur noch `IstWerkstatt`, `Artdaten.Produkte` und `Rohstoffe`, statt die Betriebsart zu prüfen.
- **Berufe am Konto:** `Konto.Berufe`. Wer einen Betrieb eröffnet, beherrscht dessen Handwerk. `BerufErlernen` (Lehrgeld + AP) schaltet weitere frei. `LiegenschaftErwerben` verlangt das Handwerk – außer bei einem Haus, das noch gar keines hat. Alte Spielstände leiten die Berufe aus den eigenen Betrieben ab.
- **Weitere KI-Häuser:** `Partieaufbau.HausAufstellen` stellt je Beruf ein Haus mit Werkstatt und Umlandbetrieb auf („Tischlerei Eichwald", „Steinbruch Kieselbach"): Haus Eichwald (Elben, Tischler) ab 2 Rivalen, Haus Kieselbach (Halblinge, Steinmetze) ab 3 (Standard). `Partieaufbau.Rivalennamen` sperrt diese Namen für den Spieler. Die KI erweitert sich nur im eigenen Handwerk.
- **Alchimist** (Daten Version 15): Kräutergarten (Kräuter, Kröte), Tinkturei → Kräuterladen → Alchimistenstube. Kräuterschnaps hat jetzt Zutaten (1 Kräuter) und damit einen Hersteller; die Hausapotheke kauft weiter am Markt. Verflochten: Der Schrank braucht Farbe, Gegengift und Zinnober Silber, Zinnober Schleifziegel. **Krötenschleim:** `Unterwelt.MischtSelbst(konto)` – wer `gauner.kroetenschleim.beruf` beherrscht, zahlt `Kosten(art, konto)` × `berufKostenFaktor` und bekommt `berufBonus` auf die Erfolgschance.
- **KI erlernt Berufe:** `KiRivale.Expandieren` betrachtet alle freien Liegenschaften; gehört eine zu einem fremden Handwerk, lernt die KI es vorher (`BerufErlernen`, Lehrgeld zählt zu den Kosten). Erst als Patrizier (`IstReich`) und höchstens ein zweites Handwerk; ein reiches Haus darf dafür einen Betrieb mehr führen (`BetriebeHoechstens`). Das eigene Handwerk ist im Vorteil, weil kein Lehrgeld anfällt.
- **Steinmetz** (Daten Version 14): Steinbruch (Bruchstein, Granit), Maurerei → Steinschneiderei → Steinmetzhütte. Verflochten: Die Steinfigur braucht Werkzeug (Schmied, Stufe 2), das Prunkschwert Schleifziegel (Steinmetz).
- **Darstellung:** Der Stadtplan ordnet Namen festen Plätzen zu; `Gassen` gibt einzelnen Werkstätten einen eigenen Weg zum Markt, wo der gerade Weg durch ein Nachbarhaus führte. Das Werkstattmodell wird nach dem **Beruf** gewählt (`Werkstil`: Gießerei mit Esse und Innenraum, Tischlerei mit Schieferdach und Holzstapel, Maurerei aus Stein mit Quadern, Tinkturei verputzt mit Fässern und Kräuterbündeln, Schänke mit Schild und Bänken, Kirche aus Stein mit Glockenturm, Diebesnest aus morschem Holz mit Laterne und vernagelten Fenstern, Wachhaus aus Mauerstein mit Wappenschild, Speergestell und Schilderhäuschen, Wechselstube verputzt mit Wechseltisch, Waage, Münzschild und vergitterten Fenstern), damit ausgebaute Werkstätten ihr Aussehen behalten. Holzschlag, Steinbruch (Felsen mit Bruchkante) Kräutergarten (Beete, Teich), Bauernhof (Felder, Mühle), Klosterhof (Beete, Bienenkörbe) und Wachturm (Steinturm mit Zinnen, Feuerkorb und Palisade) haben eigene Modelle. Farben: Spieler blau, Eisenbart rot, Eichwald grün, Kieselbach ocker.

## Gastwirtschaft (Wirt)

- **Teilsystem `Gastwirtschaft`** (`Wirtschaft/Gastwirtschaft.cs`, Abschnitt `gastwirtschaft`, ab Datenversion 16). Fehlt der Abschnitt, kehrt niemand ein.
- **Gäste ohne Zufall:** Jede volle Stunde (`Stunde`, aus `Stadtwirtschaft.Tick`) kommen `gaesteJeJahr × Gewicht(Stunde) / ΣGewicht` Gäste; sie teilen sich nach `Anziehung` (Stufe × (1 + Ansehen × Faktor)) auf alle besetzten Schänken auf. Bruchteile sammelt `_offen` je Schänke, bis ein ganzer Gast da ist. Ein Barde multipliziert die Abendgäste der eigenen Schänke (zusätzliche Gäste, keine abgeworbenen).
- **Bewirten:** Der Gast nimmt die Ausschankware mit dem größten Lagerbestand, zahlt Grundpreis × Aufschlag (Umsatz + Steuer, kein Karren). Nichts da → `Abgewiesen`. `Betrieb.FuerGaeste(ware)` hält `vorrat` Stück zurück; `Verkaufsware` gibt nur den Rest an den Karren. Die Karrenregel „Lager fast voll“ misst deshalb den ganzen Lagerplatz (`ImLager`), nicht nur die verkäufliche Ware.
- **Extras:** `BardeEinladen` und `WuerfeltischAufstellen` sind Betriebsbefehle (frei, auch aus dem Kerker). Würfeltisch → `Stadtrat.Ansehen` minus `Gastwirtschaft.AnsehenAbzug`. Stammtisch in `Gastwirtschaft.Jahresende` **vor** `Rat.Sitzung` über `Stadtrat.GunstHinzufuegen` (keine Spur). Dunkle Ecke im `Stadtgericht.Gerichtstag` nach den Spitzeln, mit dem Zufall des Gerichts.
- **KI:** `Gastwirtschaftspolitik` zur Politikstunde: Barde, wenn `BardeNutzen` die Kosten deckt oder ein Stammtisch da ist; Würfeltisch nur bei `Gaunerei ≥ 2`. Den Beruf lernt die KI wie jeden anderen (als Patrizier).

## Kirchenwesen (Prediger)

- **Teilsystem `Kirchenwesen`** (`Kirche/Kirchenwesen.cs`, Abschnitt `kirche`, ab Datenversion 17, Zufall ×43+23). Fehlt der Abschnitt, gibt es keine Kirchen; die vier Befehle werden abgelehnt.
- **Messe ohne Zufall:** Zur Stunde `messeUm` (aus `Stadtwirtschaft.Tick`, nach `Gastwirtschaft.Stunde`) verteilen sich `glaeubigeJeJahr` nach `Anziehung` = Stufe × (1 + Ansehen × Faktor) × (1 + Rhetorik × Faktor) auf alle besetzten Kirchen; Kollekte als Umsatz ohne Steuer. `Kirchenbuch` je Kirche für dieses und das Vorjahr.
- **Ablass** (`AblassKaufen`, nur in fremder Kirche): `AblassFuer` wählt die Spur mit den meisten Beweisen (`Stadtgericht.BeweiseFuer`), sonst die jüngste; `Stadtgericht.Lossprechen` löscht sie mit allen Beweisen. Käufer zahlt als Gerichtskosten, der Prediger bucht Umsatz.
- **Beichte:** `Jahresende` (nach dem Gerichtstag) würfelt je fremder Spur, ob ein Prediger sie hört (`_beichtwissen`, Paare Prediger/Spurnummer). `BeichtgeheimnisBrechen` macht daraus Beweise (`BeweisGeben`) und meldet die Tat `Beichtbruch`.
- **Schriften:** `Beschaffen` nimmt die Ware aus einem eigenen Lager oder kauft sie am Markt (Familien- bzw. Gaunerkosten). `LehrschriftLesen`: +`TrainingSchritt` bis `lehrschriftBis`, einmal je Jahr (`_gelesen`). `HetzschriftVerbreiten`: `_gehetztBis[ziel] = Jahr + 1`, `Stadtrat.Ansehen` zieht `AnsehenAbzug` ab; Tat `Verleumdung`.
- **KI:** `Kirchenpolitik` nach der Verteidigung: Ablass, sobald andere Beweise haben (halbe Reserve); wagemutig (`Gaunerei ≥ 2`) Beichtbruch gegen das stärkste Haus, über das sie etwas weiß, und eine Hetzschrift gegen das führende Haus. Lehrschriften liest sie in `Trainieren` – nur mit AP, die sonst verfielen (sonst kippt die Balance: Talente wuchsen in der Messung um 70 %).
- **Lager sortiert:** `Betrieb._lager` ist ein `SortedDictionary` (ordinal). Ein `Dictionary` belegt nach `Remove` freie Plätze neu, die Reihenfolge nach dem Laden wich dann ab – der Gleichlauf-Test fiel erst mit dem Prediger auf.

## Diebeswesen (Dieb)

- **Betrieb ohne Ware:** `BetriebsartDaten.OhneProduktion` (weder `produkte` noch `rohstoffe`, nur für den Beruf aus `dieb`) und `InDerStadt` (Ort Werkstatt). `Betrieb.Arbeitsminute` sammelt die reine Leistung der Leute in `Fortschritt` (wird mitgespeichert) und gibt Erfahrung; `BerufDaten.Umland` ist optional. Ausbau, KI-Ausbau, Kontor und Stadtansicht fragen `InDerStadt` statt `IstWerkstatt`.
- **Teilsystem `Diebeswesen`** (`Gauner/Diebeswesen.cs`, Abschnitt `dieb`, ab Datenversion 18, Zufall ×47+29). `Stunde` (nach der Messe) holt die Minuten ab: Beute = Minuten × `WertJeMinute(Stufe)` als Umsatz ohne Steuer; Fang mit 1 − (1 − p)^Stunden, ein Dieb weniger (Lehrling zuerst), Spur `Taschendiebstahl` mit Beweis für ein zufälliges anderes Haus.
- **Kalkulation:** `WertJeArbeitsminute` und `ErwarteterJahresgewinn` nehmen für Betriebe ohne Ware `Diebe.WertJeMinute` – Personal, Jahresertrag und KI-Expansion laufen damit unverändert.
- **Nacht** (nach `Unterwelt.Nacht`): `Diebesauftrag` Einbruch / Auskundschaften / Entführung / Brandstiftung; je Nacht `auftraegeJeStufe`. Spuren und Zeugen wie im Hinterzimmer (`Unterwelt.Zeugenchance`, `AngriffMerken`, `Melden`).
- **Forderungen:** `Diebesbrief` (Schutzgeld oder Lösegeld mit Geisel-Nummer); die Forderung ist sofort Spur und Beweis für das Opfer. `DiebesforderungBeantworten` (auch aus dem Kerker). Nein → Brandstiftung in der nächsten Nacht bzw. `Familienbuch.Verschwinden` (zählt wie verstorben). Unbeantwortet bis Ende des Folgejahres = Nein (`Jahresende` nach `Gauner.Jahresende`).
- **Geiseln:** `Diebe.IstGeisel(Person)` – `Familienbuch.Erbe` überspringt Geiseln, ein verschleppter Ehepartner bekommt keine Kinder. Kein eigener Zustand in der Familie: Die Geisel steht nur im Brief.
- **Ansehen:** `Stadtrat.Ansehen` minus `Diebe.AnsehenAbzug` (Haus mit Diebesbetrieb).
- **KI:** `DarfBerufLernen` lässt den Dieb nur wagemutige Häuser lernen (`DiebErlaubt`), mit Zuschlag ×1,5 auf den erwarteten Gewinn (die nächtlichen Züge). `Diebeszuege` vor `Gaunerei`: Einbruch, Schutzgeld (80 % des drohenden Brands), Entführung (10 % des Vermögens, 500–3.000) zählen zum Gaunerei-Budget; Auskundschaften ist frei. `Unterweltschutz`: Lösegeld zahlen, sobald es geht; Schutzgeld nur, wenn billiger als der Brand – sonst Wächter und Nein.

## Meistergrade und Gebäudestufen

- **Daten** (ab Version 13): `meistergrade.pruefungen` (Erfahrung, Gebühr, AP für Grad 2–6), `stufeAbGrad` (1, 3, 5), `apBonusAbGrad`/`apBonus`, `umbauMinuten`. Werkstätten haben `stufe`, `ausbau` (nächste Art) und `ausbauKosten`; die Prüfung verlangt, dass die höhere Stufe alle Produkte der niedrigeren kann. Liegenschaften gibt es nur auf Stufe 1. Fehlt der Abschnitt, bleibt alles auf Grad 1 und Stufe 1.
- **Erfahrung und Grad am Konto** (gehören dem Haus, überstehen den Erbfall): `Konto.Erfahrung(beruf)` wächst mit jedem hergestellten oder geförderten Stück um dessen Gesellenzeit in Stunden (`Betrieb.Arbeitsminute`). `Konto.Meistergrad(beruf)` ist 0 ohne Beruf, sonst 1–6. Alte Spielstände beginnen mit Grad 1 und ohne Erfahrung.
- **Befehle:** `MeisterpruefungAblegen(beruf)` (Gebühr als Investition, AP aus den Meistergrad-Daten – `ApKosten(konto, befehl)` kennt den Sonderfall wie beim Üben), `BetriebAusbauen(betrieb)` (Betriebsbefehl, frei). Gründe für Ablehnungen liefern `MeisterpruefungVerboten` und `AusbauVerboten` – dieselben Texte nutzt die Oberfläche für gesperrte Knöpfe.
- **Ausbau am Platz:** `Betrieb.Ausbauen` wechselt `Art` und `Artdaten` sofort (Plätze, Produkte, Unterhalt), das Produkt bleibt. Danach ruht die Arbeit `UmbauMinuten` lang; diese Minuten zählen nicht als Stillstand (sonst kaufte die KI Karren).
- **Grad 6:** `ApBonus(konto)` gibt in `AktionspunkteVerteilen` einen AP mehr je Runde.
- **Gold** ist eine reine Marktware (wie Kräuterschnaps), gebraucht für Goldkette, Prunkschwert und Prunktruhe.
- **KI:** `Meisterpruefung` zur Politikstunde (nach Familie und Verteidigung), sobald Erfahrung und Reserve reichen; `Ausbauen` stündlich vor `Expandieren`, wenn `Kalkulation.AusbauMehrgewinn` die Kosten in `MaxAmortisationJahre` einspielt. Werkstätten ab Stufe 2 kaufen Ochsenkarren nach (mehr Zutatensorten).
- **Darstellung:** `Stadtansicht.StufeZeigen` setzt ab Stufe 2 eine Dachgaube, ab Stufe 3 einen Dachreiter (golden beim Schmied, kupfergrün beim Tischler) auf das Dach, während des Umbaus ein Gerüst. Die Grundfläche bleibt gleich. Kontor: Grad, Erfahrung und Prüfungsknopf je Beruf; Betriebsverwaltung: Stufe und Ausbauknopf mit erwartetem Mehrgewinn.

## Gesundheit

- **Teilsystem `Gesundheitswesen`** (`Gesundheit/`, Abschnitt `gesundheit` in wirtschaft.json, eigener Zufallsgenerator `startwert*41+19`). Fehlt der Abschnitt, wird niemand krank.
- **Krankheit an der Person:** `Person.Krankheit` (eine `Erkrankung` mit Talentabzug, Ansehensabzug, Sterbechance). `Person.Talent(t)` liefert den **wirksamen** Wert (mit Abzug), `Person.Anlage(t)` den Grundwert. Üben, Ausbildung, Vererbung und Speichern arbeiten mit der Anlage, alle Wirkungen (Markt, Rat, Gericht …) automatisch mit dem wirksamen Wert.
- **Jahreslauf** in der Jahresabrechnung: `Gesundheit.Jahresende()` (Tod durch Krankheit, Genesung) → `Familien.Jahreswechsel()` → `Gesundheit.NeuesJahr()` (Pest, neue Krankheiten). Tod durch Krankheit geht über `Familienbuch.AnKrankheitSterben` – mit Erbfolge und Todesursache in der Chronik.
- **Pest:** `NaechstePest` wird gewürfelt (25–40 Jahre). Im Pestjahr steckt sie Familien und Brautschau an; `Krankenstand` (30 %) verlangsamt jede Arbeitsminute.
- **Befehle:** `MedicusRufen`, `SchnapsKaufen` (Marktware „Kräuterschnaps“, Posten Familie), `SchnapsGeben` – frei, keine AP, auch aus dem Kerker.
- **Krötenschleim:** neues `Gaunerstueck`, Vergehen `Giftmischerei`; trifft das Oberhaupt um 21 Uhr mit einer Krankheit aus `gesundheit.kroetenschleim`.
- **KI:** `Gesundheitspolitik` zur Politikstunde (Schnapsvorrat 2, Medicus bei Lebensgefahr und Pest, bei Talentverlust des Oberhaupts nur mit voller Reserve). Krötenschleim setzt sie (noch) nicht ein.

## Aktionspunkte

- **Vorrat je Haus:** `Konto.Aktionspunkte` (Klasse `Partie/Aktionspunkte.cs`). Wie viele AP je Runde (= Spieljahr) dazukommen, steht in den Partie-Einstellungen; die Simulation reicht den Wert an `Stadtwirtschaft.AktionspunkteJeRunde` weiter. Aufgefüllt wird in der Jahresabrechnung **vor** den Überlegungen der KI, höchstens bis `aktionspunkte.hoechstens`.
- **Kosten** stehen in `wirtschaft.json` unter dem **Namen des Befehls** (`"Werben": 1`). Beim Laden prüft `AktionspunkteDaten.Pruefen` per Reflection, dass es jeden genannten Befehl gibt – ein Tippfehler fällt sofort auf. Fehlt ein Befehl, ist er frei.
- **Abbuchen an einer Stelle:** `Stadtwirtschaft.Ausfuehren` prüft vor dem Befehl, ob der Vorrat reicht, und bucht **nur bei Erfolg** ab. Kein Teilsystem (Rat, Gericht, Familienbuch …) kennt AP.
- **Training:** Befehl `TalentTrainieren` → `Familienbuch.TalentTrainieren` (+`trainingSchritt` Sterne, einmal je Talent und Jahr, gemerkt als `Person.GeuebtIm`). Die Kosten hängen vom Talent ab (`trainingKosten`-Stufen), deshalb gibt es `Stadtwirtschaft.ApKosten(Konto, Befehl)`. Ist das Üben ohnehin verboten (Höchstwert, schon geübt), kostet es 0 – so meldet die Ablehnung den richtigen Grund statt „zu wenig AP“.
- **KI:** `Befehlen` verzichtet still, wenn die AP nicht reichen. Zur Politikstunde gilt die Reihenfolge Familie → Verteidigung → (Wagemutig: Hinterzimmer → Rat → Anklage; sonst Rat → Anklage → Hinterzimmer) → Training. Bewerben nur, wenn die AP auch für die Wahlgeschenke reichen; Geschenke nur alle oder keine. Geübt wird nur, was sonst beim Auffüllen verfiele.
- **Tests:** Mechanik-Tests von Rat und Gericht laden die Daten ohne AP-Abschnitt (`Datenpfad.OhneAktionspunkte`), damit sie viele Züge im selben Jahr prüfen können; die AP-Regeln prüfen die `AktionspunkteTests`.

## Wachwesen (Gardist)

- **Teilsystem `Wachwesen`** (`Ordnung/Wachwesen.cs`, Abschnitt `wache`, ab Datenversion 19, Zufall ×53+31). Ein Beruf ohne Ware wie der Dieb: `OhneProduktion` gilt jetzt für den Beruf aus `dieb` **und** aus `wache`; der Wachturm ist ein Umlandbetrieb ohne Ware (`startkarren` leer).
- **Sold:** `Stunde` (nach den Dieben) holt die Arbeitsminuten ab, Sold = Minuten × `sold.jeMinute` × `jeStufe` als Umsatz **mit** Steuer. `Stadtwirtschaft.WertOhneWare` liefert der Kalkulation Beute (Dieb) bzw. Sold nach Steuer (Wache) – Personal, Jahresertrag und KI-Expansion laufen unverändert.
- **Nur Faktoren, keine Würfe:** `Patrouillefaktor(b)` = 1 − min(`hoechstens`, `schutzJeWachmann` × Σ Leistung je Minute aller besetzten Wachhäuser) wirkt in `Unterwelt` und `Diebeswesen` auf Taten gegen Betriebe in der Stadt (Entführung immer); `ZeugenBonus` addiert `zeugenAnteil` × Schutz zur Zeugenchance. `Landstrassenfaktor` (Wachtürme, nach Besetzung) multipliziert `Ueberfall.ChanceTag`. Ohne Wache sind alle Faktoren 1 bzw. 0 – eine Partie ohne Gardist verläuft Wurf für Wurf wie zuvor (Messung identisch).
- **Razzia** (`Befehle.Razzia`, 2 AP, `FreieRazzien` nach Stufe): sofort; je Spur des Ziels, die die Wache nicht kennt, Beweis mit `Razziachance`; das Lager aller Diebesbetriebe des Ziels wird beschlagnahmt (`Konto.Raub`). Meldung an alle.
- **Kopfgeld:** `Stadtgericht` ruft nach einem Schuldspruch `KopfgeldNachUrteil` (Anteil der Buße, von der Stadt). `Diebeswesen` fragt beim Fang zuerst `TaschendiebGefasst` – ein Haus der Wache bekommt den Beweis und das Kopfgeld, sonst ein zufälliges Haus wie bisher.
- **Geleitschutz:** Eskorten mit `"wache": true` gibt es nur, solange `Geleitgeber` (höchste Wachstube ab `abStufe`) existiert; `EskorteVerfuegbar` filtert Oberfläche und KI. `Karren.Losfahren` fällt bei fehlender Wache auf „Keine“ zurück und bucht sonst `GeleitBezahlen` beim Geleitgeber.
- **Ansehen:** `Stadtrat.Ansehen` plus `AnsehenBonus` (Haus mit Wachbetrieb).
- **Wachbuch** je Haus (Sold, Kopfgeld, Geleitgeld, Razzien) für dieses und das Vorjahr, mitgespeichert (`WacheStand`).
- **KI:** `WacheErlaubt` (Gaunerei ≤ 1) in `DarfBerufLernen`, Zuschlag ×1,5 wie beim Dieb. `Wachdienst()` durchsucht zuerst Häuser mit Diebesbetrieb, dann das mit den meisten bekannten Spuren.

## Bankwesen (Geldleiher)

- **Reinvermögen:** `Konto.Vermoegen` bleibt das **Bargeld** (damit wird bezahlt). Neu sind `Forderungen` (Kasse und Kredite an andere) und `Schulden` (Kredite bei Geldleihern); `Reinvermoegen` = Bargeld + Forderungen − Schulden. Stand, Rangliste, Ansehen, Bußdeckel, Schuldenjahre/Bankrott, Sieg und KI-Standaufstieg lesen das Reinvermögen – wer leiht, wird nicht reicher; wer verleiht, nicht ärmer. Ohne Kredite ist beides gleich, alte Partien laufen unverändert. `Konto.Zinsen` ist ein eigener Posten außerhalb des Betriebsgewinns.
- **Teilsystem `Bankwesen`** (`Bank/Bankwesen.cs`, Abschnitt `geldleiher`, ab Datenversion 20, Zufall ×59+37). Betriebe ohne Ware wie Dieb und Wache (`OhneProduktion` gilt jetzt für drei Berufe).
- **Kasse je Betrieb** (`KasseEinlegen`/`KasseAbheben`, Betriebsbefehle ohne AP): Geld wandert vom Bargeld in die Forderungen. `Stunde` holt die Arbeitsminuten ab; je Stunde verzinsen die Schreiber min(Kasse, Rahmen der Minuten) mit `stadtzins` (auf die 720 Arbeitsminuten verteilt) – Umsatz mit Steuer. Am `Jahresende` fallen mit `ausfall.chance` Kunden aus (`ausfall.anteil` der Kasse, `ForderungAbschreiben` als Schaden).
- **Wechsel:** `WechselJeMinute` = `gebuehr` × Marktvolumen (24-Stunden-Angebot × Preis) / 720 / `bedarfSchreiber`. Arbeiten mehr Schreiber als der Bedarf, teilen sie die Gebühr (`deckel`) – zwei Bankhäuser verdienen am Wechsel nicht doppelt.
- **Kredite** (`KreditAufnehmen`, 1 AP; `KreditTilgen`, frei): `KreditVerboten` prüft Geber (Kontor mit Schreibern), einen Kredit je Nehmer, Laufzeit und `Hoechstkredit` = min(freier Rahmen des Gebers (`hoechstJeStufe` − Verliehenes, höchstens sein Bargeld), `kreditwuerdigAnteil` × Reinvermögen des Nehmers). Zinsen laufen stündlich auf (`Rest × Satz / 24`); am Jahresende Zins + Rate (`Rest / JahreOffen`). Fehlt Bargeld, `Pfaenden`: teuerste Lagerware zu `pfandAnteil` × Marktpreis; der Rest macht das Bargeld negativ (Schuldenjahre). Scheidet der Nehmer aus, schreibt der Geber den Rest ab.
- **Wucher:** Satz > `wucher.ab` → `Vergehen.Wucher` (Spur mit Wert (Satz − Grenze) × Betrag × Jahre), der Nehmer hat den Beweis sofort; `Stadtrat.Ansehen` minus `AnsehenAbzug`, solange ein Wucherkredit des Gebers läuft.
- **AP:** `Stadtwirtschaft.ApBonus` = Meisterbonus + `Bank.ApBonus` (beste Stufe → `apJeStufe`).
- **Kalkulation:** `WertOhneWare` liefert für den Geldleiher `WertJeMinute(stufe)` (Zins bei voller Kasse + Wechsel, nach Steuer und erwartetem Ausfall).
- **KI:** `Bankpolitik` (täglich, keine AP): Zins nach Charakter (10 / 14 / 25 % – die Wagemutige nimmt Wucher in Kauf), Kasse bis `RahmenVoll` füllen, solange zweimal die Reserve bar bleibt; wird das Bargeld knapp, abheben. Den Beruf lernt sie wie jeden anderen (als Patrizier). Kredite nimmt die KI nicht.
- **Oberfläche:** Verwaltung (Kasse, Rahmen, Bankbuch, Zins für Häuser, eigene Kredite), Kontor (Reinvermögen, Kreditzeile: Geldleiher wählen, Betrag, Laufzeit, leihen / ablösen), Jahresbericht (Kreditzinsen, Reinvermögen). Wache und Geldleihe zeigen keine Karren.

## Unterwelt

Die Unterwelt (`src/Reichsgilde.Core/Gauner/Unterwelt.cs`) gehört zur Stadtwirtschaft (`Stadtwirtschaft.Gauner`). Alle Zahlen stehen in `wirtschaft.json` im Abschnitt `gauner`.

- **Bestellen und Ausführen sind getrennt.**
  - `Beauftragen` bezahlt die Bande sofort.
  - Ausgeführt wird in `Unterwelt.Nacht`, die die Stadtwirtschaft um 21:00 aufruft (`Unterwelt.Nachtstunde`).
  - Danach meldet `NachtVorbei`, dass offene Tafeln sich neu aufbauen sollen.
- **Überfall als Hinterhalt:**
  - Die Wegelagerer warten bis zur nächsten Nacht. `Karren.Losfahren` fragt bei jeder beladenen Umlandfahrt `HinterhaltAusloesen`.
  - Lauert ein Hinterhalt, greift er statt der zufälligen Banden an.
  - Der Schutz der Eskorte gilt wie bisher: Erfolg = Stärke × (1 − Schutz).
- **Spuren:** Jedes Gaunerstück meldet eine Tat ans Gericht (`TatMelden`, Vergehen Diebstahl, Sabotage, Raub). Mit der Zeugenchance gibt `BeweisGeben` dem Opfer sofort einen Beweis.
- **Öffentlich oder geheim:** Ein `Nachtereignis` ist entweder öffentlich (`Fuer = null`) oder nur für ein Haus bestimmt, etwa der Hehlerbericht. `NachrichtenFuer` liefert jedem Haus genau das, was es wissen kann.
- **Erpressung:**
  - Der Brief ist eine `Forderung` und zugleich eine Spur „Erpressung“, deren Beweis das Opfer sofort hat.
  - Zahlt das Opfer, verbrennt `BeweiseVerbrennen` die Beweise des Erpressers.
  - Briefe gelten bis zum Ende des Folgejahres, danach heißt Schweigen Nein.
- **Kerker:** Das Gericht führt `KerkerBis` wie den Pranger. `Stadtwirtschaft.Ausfuehren` lehnt im Kerker alle persönlichen Befehle ab. Erlaubt bleiben `BetriebsBefehl`, `ErbenBestimmen`, `AusbildungWaehlen` und `ErpressungBeantworten`.
- **Eigener Zufallsgenerator**, wie bei Familie und Gericht.

**KI in der Unterwelt** (Profilwert `Gaunerei`: vorsichtig 0, ausgewogen 1, wagemutig 2):
- Erpresserbriefe beantwortet sie mit `DrohendeStrafe`: Buße + Pranger + Amt + Kerker, grob in Gulden geschätzt.
- Nach einem Angriff (`LetzterAngriff`, öffentlich) stellt sie einen Wächter ein.
- Selbst heuert sie Banden nur an, wenn der erwartete Vorteil die Kosten übersteigt. Unter den Augen der Spitzel hält die Ausgewogene still, die Wagemutige macht weiter.

**Lehrstück aus diesem Schritt (Anzeige und Zeitpunkt):** Im Klicktest zeigte die Tafel nach 21 Uhr noch „Sabotage bestellt“. Der Kern war richtig: Die Tafel war um 20:45 aufgebaut worden und wusste nichts von der Nacht. Eine Anzeige muss erfahren, wann sich ihre Daten ändern. Dafür gibt es jetzt das Ereignis `NachtVorbei`. Das Phasen-Ereignis „Dunkle Nacht“ wäre eine Spielminute zu früh gekommen.

## Speichern und Laden

Ein Spielstand ist eine lesbare JSON-Datei (`src/Reichsgilde.Core/Speichern/`).

- **Datenpakete:** Jede Klasse liefert ihren Zustand mit `Sichern()` als reines Datenpaket und kann ihn mit `Wiederherstellen()` übernehmen. Private Felder bleiben privat, und das Dateiformat hängt nicht an den Klassen.
- **Verweise:** Statt Objektverweisen stehen Nummern oder Namen in der Datei, zum Beispiel „Betrieb gehört Konto Nr. 0“.
- **Wirtschaftsdaten:** Sie werden mitgespeichert. Eine alte Partie läuft also mit ihren Zahlen weiter, auch wenn `wirtschaft.json` inzwischen geändert wurde.
- **Zufall:** Der .NET-Zufallsgenerator lässt sich nicht speichern. `Zufallsquelle` zählt deshalb nur die Würfe. Beim Laden würfelt ein frischer Generator mit gleichem Startwert genauso oft „leer“ und ist danach im selben Zustand.
- **Prüfung:** Der Gleichlauf-Test speichert eine Partie nach 3½ Jahren, lädt sie und lässt Original und Kopie 5 Jahre weiterlaufen. Beide müssen danach Zeichen für Zeichen denselben Spielstand ergeben. Vergisst das Speichern ein Feld, schlägt der Test fehl (gegengeprüft).
- **Formatversion:** Die Datei trägt ein Format. Eine neuere Datei lehnt eine ältere Programmversion mit verständlicher Meldung ab.

Die Startaufstellung einer neuen Partie steht in `Partie/Partieaufbau.cs`. Hauptmenü, Tests und Startoptionen benutzen dieselbe Aufstellung.
