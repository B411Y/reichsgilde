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
2. **Testbarkeit:** Spiellogik lässt sich in Sekunden prüfen, ohne das Spiel zu starten (z. B. „Ergeben 15 Minuten wirklich genau ein Jahr?“). Der Testlauf (`tests/Reichsgilde.Core.Tests/Program.cs`) findet alle Testklassen selbst: jede statische Klasse mit `public static IEnumerable<(string, Action)> Alle()` – per Reflection, nach Klassenname sortiert. Eine neue Testdatei genügt.
3. **Übersicht:** Wer eine Spielregel sucht, schaut in `src/`. Wer wissen will, wie etwas aussieht, schaut in `game/`.

## Das Zeitsystem

| Begriff | Bedeutung |
|---|---|
| **Tick** | Eine Spielminute. Kleinste Zeiteinheit der Simulation. |
| **Spieltag = Runde = Spieljahr** | 1020 Ticks: gespielt wird von 05:00 bis 22:00 Uhr (wie im Original „Die Gilde“), dann kommt die Jahresrechnung. `Spielzeit.TagesbeginnMinute`, `TagesendeMinute`, `MinutenProTag` (1020), `StundenProTag` (17). |
| **Jahreszeit** | Wechselt von Jahr zu Jahr: Frühling 1400 → Sommer 1401 → Herbst 1402 → Winter 1403 → … |
| **Tagesphase** | Zunftglocke (05–10), Hoher Mittag (10–15), Vesper (15–20), Dunkle Nacht (20–22) |
| **Taktgeber** | Rechnet Echtzeit in Ticks um. Hier sitzen Rundendauer, Tempo und Pause. `AngebrocheneMinute` (0 bis < 1) verrät der Darstellung, wie weit die nächste Minute schon läuft – Sonne, Schatten, Himmel und Karren rechnen mit „Minute + Bruchteil“ und bewegen sich so jedes Bild ein Stück statt einmal pro Minute zu springen. Die Simulation nutzt den Wert nicht. |

**Stundenende und Jahreswechsel.** Alles, was je volle Stunde läuft (Gäste, Messe, Taschendiebe, Sold, Zinsen und Wechsel, die Überlegungen der KI), steckt in `Stadtwirtschaft.StundeVorbei`. Es läuft zu Beginn jeder vollen Stunde im Tag – die letzte Stunde (21–22 Uhr) aber rechnet `Jahresabrechnung` ab, bevor das Jahr schließt. So zählen die Abendgäste noch zum alten Jahr. `Spielzeit.StundeDieEndete` liefert dafür 21. Der Taktgeber teilt die 15 Minuten Echtzeit auf `MinutenProTag` auf. Spielstände haben seit dem Wechsel Format 2; ältere (24-Stunden-Tag) werden mit einer Meldung abgelehnt.

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
| `Markt` / `Marktstand` | Preis = Grundpreis × (Nachfrage ÷ Angebot)^Elastizität. Das Angebot umfasst die Grundversorgung durch Händler, die keine Spieler sind, plus die Verkäufe der Spieler in den letzten 17 Spielstunden (= 1 Jahr, 18 Stundenfächer). |
| `Betrieb` | Gießerei oder Mine mit Lager (40 je Ware). Arbeitet von 08:00 bis 20:00 Uhr. Fehlen Zutaten oder ist das Lager voll, ruht die Arbeit. |
| `Karren` | Pendelt automatisch zwischen Betrieb und Markt. Er fährt los, wenn genug Ware da ist, Zutaten knapp werden oder Ware 4 Stunden wartet. Am Markt verkauft er und kauft knappe Zutaten ein. Der Karren ist ein **Zustandsautomat**: am Betrieb → zum Markt → zurück. |
| `Ueberfall` | Beladene Umland-Fahrten können überfallen werden, nachts doppelt so oft. Die Täter sind Banden aus allen Völkern, oft gemischt, dazu wilde Ork-Rotten und Ogerhorden. Eine Eskorte senkt die Erfolgschance. |
| `Konto` | Vermögen und Jahresrechnung (Umsatz, Zutaten, Löhne, Fixkosten, Steuer, Schäden). |
| `Stadtwirtschaft` | Hält alles zusammen, kennt das geltende Fron- und Sklavenstatut und würfelt Ausbrüche und Aufstände. |

**Stadttore** (`logistik.tore`, ab Datenversion 30, `ToreDaten`): Ab `schliessenUm` (20 Uhr) bis Tagesbeginn sind die Tore zu. Nur Umlandkarren müssen hindurch – auf dem Hinweg bei der Ankunft (kurz vor dem Markt), auf dem Rückweg bei der Abfahrt vom Markt. `Karren.DurchsTor`: offen → weiter; zu und Nachtfahrt erlaubt → `torgeld` als Transportkosten; sonst `TorWartenBis` = nächster Tagesbeginn. Hinweg: `Ankunft` wird auf den Morgen verschoben, dann normale Ankunft. Rückweg: Zustand schon `ZumBetrieb`, aber die eigentliche Abfahrt (Eskorte, Überfallwurf) erst am Morgen – das Warten ist sicher. `WartetAmTor(zeit)` ist die Abfrage für Anzeige und Darstellung. Ohne Abschnitt keine Tore (alte Daten und Spielstände laufen wie vorher).

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

**Schatten:** Sonne und Mond sind ein einziges `DirectionalLight3D`. Seine Schattenkarte ist auf 8192 Pixel und den Filter „Ultra“ gestellt (`project.godot`, `[rendering]`), dazu `ShadowBlur = 1,5`. Grund: Bei flachem Licht (Dämmerung, Mond) fällt ein Pixel der Schattenkarte als langer Streifen auf den Boden, die Kante wird zur Treppe. Mehr Pixel machen die Stufen kleiner, der Filter mittelt mehrere Nachbarpixel zu einer weichen Kante.

**Raumansicht** (`game/scripts/Stadtansicht.Raeume.cs`, vierter Teil der `partial class`): Werkstätten und Rathaus betritt man. Eine zweite Kamera (`_raumKamera`, Perspektive, Sichtfeld 50°) blickt fest in einen Raum; die Stadtkamera bleibt, wo sie war.
- **Wo die Räume liegen:** Der Knoten „Innen“ eines Gebäudes liegt 200 m unter der Stadt (`RaumTiefe`), seine Räume stehen dort 40 m auseinander (`RaumAbstand`), damit die Kamera nie zwei zugleich sieht. Nichts aus der Stadt stört: Der Stadtboden ist eine Ebene und von unten unsichtbar, und die Sonne wird in der Raumansicht abgeschaltet – ihr Licht ginge sonst durch die Decke. Die Raumkamera hat eine eigene `Environment` (dunkler Hintergrund, Umgebungslicht nach der Uhrzeit), das Fensterglas leuchtet in der Himmelsfarbe.
- **Raum** (`record Raum`: Kennung, Name, Mitte relativ zum Gebäude, Innenmaße, optional `Zugaenglich`): `RaumAnlegen` baut die Bühne (Rückwand mit Fenstern, Seitenwände, Deckenbalken, Boden, Grundlicht) und eine Klickfläche über den ganzen Raum. `RaumZeigen` stellt die Kamera vor die offene Vorderseite, Abstand nach der Raumbreite. Q/E und die Raumleiste (`RaumWaehlen`, `RaumBlaettern`) wechseln; das Ereignis `RaumGewechselt` meldet es der Oberfläche.
- **Werkstätten:** Werkraum (bisheriger Innenraum), Lager (`KistenspalteBauen` stellt die Kisten jetzt dorthin), Kontor (`Zugaenglich` = gehört dem Spieler), Wohnstube (`Zugaenglich` = Stammhaus, die erste eigene Werkstatt). Figuren und Texte in Kontor und Wohnstube stellen `Raumzustaende` – nur für den gezeigten Raum. Die Familie wird nur neu gebaut, wenn sich Mitglieder oder Volljährigkeit ändern.
- **Oberfläche:** `Hauptszene` baut die Raumleiste (unten rechts, dort ist in jedem Raum Platz), blendet Gebäudefenster und Kontor außer in Kontor und Amtsstube aus und öffnet bei einem Raumklick die Tafel (`RaumAngeklickt`).
- **Warum nicht in der Stadt?** Das Diorama musste Wände und Nachbarhäuser je nach Blickrichtung ausblenden und Schmuck an Wände hängen. Unter der Stadt braucht ein Raum nur drei Wände – und jeder Raum bekommt die Größe, die er braucht.

**Diorama-Innenansicht (nur noch der Dom):** Gebäude bestehen aus einzelnen Wänden, einem Dach und einem Knoten „Innen“ mit der Einrichtung. Beim Betreten wird das Dach ausgeblendet und jede Wand, deren Außenseite zur Kamera zeigt (Skalarprodukt > 0,5 – beim diagonalen Blick genau zwei). Dinge zwischen Kamera und Gebäude werden ebenfalls ausgeblendet. Was innen zu sehen ist (Kisten, Arbeiter, Glut), liest die Ansicht nur aus der Simulation – sie verändert nichts.

**Innenräume** (`game/scripts/Stadtansicht.Innenraeume.cs`, zweiter Teil der `partial class Stadtansicht`): `Innenraum` legt Boden und Laterne an und ruft je `Werkstil` einen Einrichter (`TischlereiEinrichten`, `SchaenkeEinrichten` …). Alle teilen ein Gerüst in der `BetriebsAnsicht`:
- **Arbeitsplätze** (`Arbeitsplatz`: Ort, Blickrichtung, `Bewegung`, `Werkzeug`, sitzend) werden der Reihe nach besetzt – die ersten für Stufe 1, dahinter die an den Stücken späterer Stufen (eine Stufe mehr = ein Platz mehr). Die Tracht (Schürze, Kopfbedeckung, Gewand) kommt aus dem Werkstil, Größe und Haut aus der Arbeitskraft.
- **Stufenteile:** Knoten, die erst ab Stufe 2 oder 3 sichtbar sind. **Feuer:** Licht und leuchtendes Material, hell bei der Arbeit oder wenn eine eigene Bedingung gilt (Messe, Ausschank). **Bewegte Teile:** drehen sich oder gehen auf und ab, solange gearbeitet wird. **Werkstück** und **Produktfarben** nehmen die Farbe der Ware an (`Warenfarbe`; unbekannte Waren bekommen eine feste Farbe aus dem Namen – nicht `string.GetHashCode`, der in .NET bei jedem Start anders ausfällt).
- **Besucher** (Gäste, Gläubige, Kunden, Gefangene, Geisel) werden beim Bau angelegt und nur ein- und ausgeblendet. Sie stammen aus allen vier Völkern (Zwerge mit Bart). **Zustand:** je Werkstil eine Funktion, die nur Abfragen des Kerns liest (`Gastwirtschaft.DiesesJahr`, `Kirchenwesen.DiesesJahr`, `Bankwesen.Kasse`, `Diebeswesen.Auftraege`/`Briefe`, `Wachwesen.FreieRazzien` …) und eine **Infotafel** im Raum beschriftet.
- **Anwesenheit:** Arbeiter sind von 8 bis 20 Uhr da. Im Diebesnest bleibt tagsüber nur der Hehler (die Diebe stehlen am Markt), abends sind alle zurück; in der Schänke schenkt der Wirt nach 20 Uhr weiter aus, solange Gäste kommen.
- **Gäste der Schänke:** Die Simulation bucht sie am Ende jeder Stunde. Die Ansicht zeigt die Gäste der letzten vollen Stunde (Differenz von `Gaestebuch.Bewirtet`) und schätzt nur, solange noch keine Stunde gezählt ist – mit derselben Gewichtung wie der Kern (`GaesteSchaetzung`).
- **Wandschmuck:** Was außen an der Südwand hängt (Wappenschild, Gitter, Blumenkästen, Laterne), hängt `WandschmuckAnDieWand` an die Wand, damit es beim Aufschneiden mit verschwindet und nicht im Raum schwebt.
- **Lehrstück:** Mit Innenräumen für Dieb, Wache und Geldleiher fiel auf, dass `Betrieb.Ausbauen` bei Betrieben ohne Ware ein Produkt suchte (`Produkte.Last()` auf einer leeren Liste). Der Ausbau geschah trotzdem, der Befehl meldete aber einen Fehler. Jetzt passt der Ausbau das Produkt nur bei Werkstätten mit Ware an (Test „Ausbau ohne Ware“).

**Sonderbauten** (`game/scripts/Stadtansicht.Sonderbauten.cs`, dritter Teil der `partial class`): Werkstätten und Sonderbauten teilen die abstrakte Klasse `InnenAnsicht` (Mitte, Wände, Dach, Innen, Zoom, Reichweite der Kamera, Stufenteile, Feuer, bewegte Teile, Besucher, Infotafel, Zustand, Räume). Hat sie Räume, gilt die Raumansicht, sonst das Diorama. `BetriebsAnsicht` erbt davon und kennt dazu Betrieb, Lager und Arbeiter; `SonderbauAnsicht` hat nur Kennung und Titel. Die Innenansicht wird über eine **Kennung** betreten (`InnenansichtBetreten("Rathaus")`, das Ereignis `InnenansichtGewechselt` liefert die Kennung), sodass die Oberfläche Werkstätten und Sonderbauten gleich behandelt.
- `SonderbauHuelle` baut Wände (mit Fenstern, auf Wunsch Fachwerk), Tür, Dach, Klickfläche und Boden und trägt das Gebäude ein. Die Türme hängen am Dach und verschwinden beim Betreten mit.
- **Räume anklicken:** Jeder Raum hat eine Klickfläche (`rathaus:rat`, `rathaus:gericht`, `rathaus:amt`, `rathaus:kerker`, `raum:werk`, `raum:lager`, `raum:kontor`, `raum:wohnstube`), eingeschaltet nur, solange man im Gebäude ist. `Hauptszene` öffnet bei einem Raumklick die passende Tafel.
- **Lehrstück:** Die Figuren im Gericht werden nur neu gebaut, wenn sich ein Merktext ändert. Ohne Verfahren war dieser Text leer – genau wie sein Anfangswert –, also wurde der Stadtrichter nie gebaut. Jetzt beginnt der Merktext mit `null` („noch nie gebaut“) und enthält ohne Verfahren den Namen des Stadtrichters.
- **Rathaus:** Die Figuren im Ratssaal und im Gericht werden nur neu gebaut, wenn sich Ämter, Verfahren oder Kerkerinsassen ändern (Vergleich über einen Text aus den Namen) – sonst würde jede Sekunde neu gebaut.
- **Dom:** drei Gruppen (katholisch, evangelisch, simultan) werden nach `Glaubenswesen.Stadtglaube` ein- und ausgeblendet; ohne Glaubensmodul gilt katholisch. Gläubige im Dom = Gläubige des Jahres minus die Gläubigen in den Kirchen der Häuser, je fünf eine Figur.

## Oberfläche und Fenstergröße

- **Feste Pixelgröße** (kein `stretch`-Modus): Die Oberfläche skaliert nicht mit. Ein größeres Fenster zeigt mehr von der Stadt, Schrift und Tafeln bleiben gleich groß. Eine Mindestgröße gibt es nicht.
- **`Rollbereich`** (`game/scripts/Rollbereich.cs`) liegt in jeder Tafel zwischen Rahmen und Inhalt (`Rollbereich.Einhuellen(tafel)` am Ende des Aufbaus). Er misst jedes Bild den Inhalt und setzt seine Mindestgröße auf min(Inhalt, Fenster − Rand). Passt alles, sieht die Tafel aus wie vorher; sonst erscheinen Balken, auch waagrecht. Eine feste Breite der Tafel wandert dabei auf den Inhalt, sonst könnte die Tafel nie schmaler werden.
- **Lehrstück 1:** Ecktafeln (Kontor, Auswahl) hängen an Ankern, nicht in einem Container. Godot lässt sie wachsen, aber nie schrumpfen. Der Rollbereich setzt ihre Größe deshalb selbst und hält die angeheftete Ecke fest (`GrowDirection.Begin`).
- **Lehrstück 2:** Container lassen Mausereignisse standardmäßig durch (`MouseFilter.Pass`). Ein Mausrad über einer Tafel landete deshalb in `_UnhandledInput` der Stadtansicht und zoomte die Kamera. Tafel und Rollbereich stehen jetzt auf `Stop`.
- **Marktfenster** (`MarktAnzeige`): Kacheln je Ware in Gruppen nach `typ` (Heilmittel zählen zu den Fertigwaren), sortiert nach Beruf, Stufe und Grundpreis. Preise, Farben, Lagerbestand und Tooltips werden zweimal je Sekunde angeglichen, nicht neu gebaut. Oben die Handelsleiste (siehe „Handel von Hand“); ein Klick auf eine Kachel wählt die Ware.
- **Handel von Hand** (`Wirtschaft/Handel.cs`, Abschnitt `handel`, Datenversion 24): Befehl `Handelsauftrag(Betrieb, Karren, Verkauf, Kauf)`. `Stadtwirtschaft.HandelsauftragVerboten` prüft Karren am Betrieb, Lager, Ladeflächen (je Fläche eine Ware, getrennt für Hin- und Rückweg), Lagerplatz nach dem Einkauf, Marktzeit (`AnkunftAmMarkt` gegen `marktVon`/`marktBis`, über die neue `Stadtwirtschaft.Uhr` = die Uhr der Simulation) und Geld. `Karren.AuftragAnnehmen` lädt die Verkaufsware und merkt sich den `Handelsplan`; bei Ankunft verkauft `Ankommen` wie immer und `AuftragEinkaufen` kauft statt der Zutaten (so viel das Geld reicht), dann zurück und abladen. Ist der Karren unterwegs, wird eine `Vormerkung` gespeichert; `Karren.Tick` startet sie, sobald er am Betrieb steht – vor `SollLosfahren` (`VorgemerktenAuftragStarten`, sonst Meldung, warum sie verfällt). `HandelsauftragVerwerfen` löscht sie.
- **Preise:** `Marktstand.HandKaufpreis` summiert je Stück `Grundpreis × FaktorBei(−i) × Faktor`, `HandKaufen` zieht die Stück vom Angebotsfenster ab – Hand-Käufe heben den Preis, Verkäufe senken ihn (symmetrisch). Die Zutateneinkäufe der Verwalter bleiben ohne Preiswirkung (die Nachfrage enthält sie). Faktor = (1 + `aufschlag`) × `Familienbuch.EinkaufsFaktor` (Verhandeln).
- **Zurückhalten:** `Betrieb.Zurueckgehalten` (sortiert, gespeichert) – `Verkaufsware` lässt diese Waren aus. `WareZurueckhalten` schaltet; ein Einkauf, der keine Zutat ist, wird automatisch zurückgehalten. Die KI nutzt nichts davon – Messungen bleiben Wurf für Wurf gleich.
- **Waren-Icons** (`WarenIcons`): 32 × 32 Pixel-Art aus `res://assets/waren/<ware>.png`, Dateiname wie im Zeichenskript (Umlaute → ae/oe/ue). Angezeigt mit `TextureFilter.Nearest` in ganzzahligen Vielfachen (64 px), damit die Pixel scharf bleiben. Fehlt ein Icon, steht der Anfangsbuchstabe da – neue Waren in den Daten brechen also nichts. Die Icons zeichnet `tools/icons/waren_icons.py` aus Grundformen; Schattierung (Licht von links oben) und Kontur setzt das Skript automatisch.
- **Schriften** (`game/scripts/Schriften.cs`, Dateien in `res://assets/schriften/`): **Alegreya** für allen Text, **Germania One** (gebrochene Schrift) für Überschriften und Namen in der Stadt. Der Autoload `Fenster` ruft beim Start `Schriften.Einrichten` auf: Alegreya wird Schrift des Standard-Themas und Ersatzschrift (`ThemeDB`), und ein Ereignis `SceneTree.NodeAdded` gibt jedem Label mit Größe ab 20 px Germania One, allen kleineren Labels mit fester Größe 1 px mehr (Alegreya hat kleinere Kleinbuchstaben als die alte Schrift). So brauchen die Tafeln – auch die anderer Chats – keine eigene Zeile. Alegreya läuft mit den OpenType-Merkmalen `lnum` (Ziffern in Versalhöhe statt Mediävalziffern) und `tnum` (alle Ziffern gleich breit), damit Gulden-Spalten sauber untereinander stehen. Fehlt Germania ein Zeichen, springt Alegreya ein (`Fallbacks`).
- **Namen in der Stadt:** `Beschriftung(…, kennung)` in der Stadtansicht: Mit Kennung (dieselbe wie die Klickfläche) ist die Beschriftung zunächst unsichtbar. `BeschriftungenZeigen()` schießt jedes Bild einen Strahl unter die Maus (`KennungBei`, wie beim Klick) und zeigt den Namen des Gebäudes darunter, des ausgewählten (`Stadtansicht.Auswahl`, setzt die Hauptszene) und mit gedrückter Alt-Taste alle. Steht die Maus über einer Tafel (`GuiGetHoveredControl`), zählt die Stadt dahinter nicht. Im Innenraum sind die Namen aus; Beschriftungen ohne Kennung (Kistenschilder, Infotafeln) bleiben immer sichtbar.
- **Vollbild und Kantenglättung:** Autoload `Fenster` (`project.godot` → `[autoload]`) hält beide Einstellungen, wendet sie beim Start an und speichert sie in `user://einstellungen.cfg` (Abschnitt `anzeige`, Schlüssel `vollbild` und `kantenglaettung`). Die Kantenglättung wird am Hauptfenster (`GetTree().Root`) gesetzt: `Msaa3D`, `ScreenSpaceAA` (FXAA/SMAA) oder `UseTaa` – immer nur eines. Alle 3D-Ansichten zeichnen in dieses Fenster, eigene `SubViewport`s gibt es nicht; käme einer dazu, muss er die Einstellung übernehmen. Das Fenster `EinstellungenAnzeige` wird im Hauptmenü und im Spielmenü (an dessen Stelle, Esc führt zurück) verwendet.

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

**Werkstatt zum Handwerk (PR #60):** Hat ein KI-Haus von einem Handwerk nur den Umlandbetrieb (Bauernhof, Klosterhof, Duftgarten), kauft `Expandieren` die Werkstatt dazu – auch wenn `BetriebeHoechstens` schon erreicht ist (`NurUmland`, `HatWerkstattFuer`). Andere Käufe bleiben an die Höchstzahl gebunden.

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

## Wohnhaus (Wohnungswesen)

- `Wohnen/Wohnungswesen.cs`, Abschnitt `wohnen` in `wirtschaft.json` (`stufen` mit name, ausbau, unterhalt, ansehen, abStand; `bauzeitMinuten`). Fehlt der Abschnitt, gibt es keine Wohnhäuser – alles wie früher.
- Der Zustand liegt am **Konto** (`WohnhausStufe`, `WohnhausFertigAb`), weil das Wohnhaus dem Haus gehört, nicht einem Betrieb. Gespeichert im `KontoStand`; alte Spielstände bekommen beim Laden das Haus, das ihr Stand verlangt (`AlteStaendeErgaenzen`).
- Befehl `WohnhausAusbauen`: prüft `AusbauVerboten` (höchstens eine Stufe über dem Stand, Bargeld, kein laufender Bau), bucht den Preis als Investition, nach der Bauzeit zieht das Haus um (`Minute`, mit Meldung).
- **Pflicht für den Stand:** `StandErwerben` fragt `Wohnen.StandVerboten(konto, neuerStand)`; dieselbe Abfrage nutzen Kontor und Rathaus-Knopf für den Grund.
- Ansehen: `Stadtrat.Ansehen` addiert `Wohnen.AnsehenBonus`. Unterhalt: am Jahresende nach den Familienkosten (`UnterhaltZahlen` → `Konto.Familienkosten`).
- `Wohnen.Leerstand` liefert die Häuser erloschener oder bankrotter Konten – Grundlage für späteres Kaufen und Sanieren.
- **Gauner gegen das Wohnhaus** (Abschnitt `gauner.wohnhaus`): Die Gaunerstücke `Hauseinbruch` und `Brandstiftung` laufen in der `Unterwelt` wie alle anderen (Auftrag, Nacht um 20 Uhr, Spur, Zeuge). Rechnungen für Tafel und KI: `ErfolgschanceWohnhaus`, `Hausbeute`, `Brandschaden`, `ZeugenchanceWohnhaus`, `LetzterAngriffWohnhaus`. Ein Brand setzt `Konto.WohnhausInstandBis`; bis dahin liefert `AnsehenBonus` 0 und `AusbauVerboten` sperrt den Ausbau. `Konto.Hauswaechter` (Befehl `HauswaechterSetzen`, auch aus dem Kerker erlaubt) wirkt wie ein Wächter im Betrieb und wird mit dem Unterhalt als Personal bezahlt.

## Liegenschaften und Stadtplan

- **Was es zu erwerben gibt**, steht in `wirtschaft.json` (Abschnitt `liegenschaften`: Name, Art, Kaufpreis, Mindeststand). Erworben wird mit dem Befehl `LiegenschaftErwerben(name)`. Die Stadtwirtschaft eröffnet dann einen Betrieb mit genau diesem Namen.
- **Wo etwas steht**, weiß nur die Darstellung: Der `Stadtplan` in `Stadtansicht.cs` ordnet jedem Betriebsnamen einen Platz zu. Der Simulationskern kennt keine Koordinaten. So bleibt er unabhängig von der Grafik und läuft im Multiplayer genauso auf dem Server.
- Ein neuer Bauplatz braucht also zwei Einträge: einen in `wirtschaft.json` und einen im `Stadtplan`.
- **Kartendateien (Leveldesign, noch ohne Leser):** Die künftigen Städte liegen als `game/data/karten/<stadt>.json` vor (Straßen, Bauzonen, feste Plätze, Vorkommen in Metern, x = Osten, z = Süden). Sie kennen kein Godot, damit sie später auch der Kern lesen kann (Wegegraph, Fahrzeit aus der Weglänge). Format: `docs/LEVELDESIGN.md`, Abschnitt „Kartendatei“.
- **Größe der Stadt:** Die Mauer steht bei ±`Mauerrand` (50 m), Osttor bei (50|0), Westtor bei (−50|6). Die Weststraße führt noch nirgends hin. Bei der Erweiterung von ±36 auf ±50 rückte alles vor der alten Mauer um 14 m nach außen (`NachAussen`, auch für die Bäume, damit der Zufall dieselben liefert); innen blieb alles an seinem Platz, der neue Ring an der Mauer ist für Wohnhäuser frei. Im Osten rückt das Umland noch einmal 20 m weiter: Zwischen Osttor und **Wegkreuz** (76|−1) liegt die Vorstadt; erst am Wegkreuz zweigen die Wege zu Gruben, Höfen und Gärten ab. Holzschläge und Steinbruch am Felsenhang liegen vor dem **Westtor** (`ImWesten`). `Kreuz(name)`, `AnDerStrasse` und `WegZumMarktVon` wählen das Tor je Platz – auch für die Karren.
- **Wohnviertel** (`Stadtansicht.Wohnviertel.cs`): Die Bürger-KI vergibt Häuser nur als Nummern (`Einwohnerschaft.Wohnhaeuser`); die Darstellung stellt sie auf, sobald es sie gibt (`WohnhaeuserPruefen` in jedem Bild bis zum ersten Mal). Ratsherren- und Bürgerhäuser bekommen die Ringplätze nächst dem Markt (32 Plätze zwischen Ringgasse ±38 und Mauer), einfache Häuser zuerst die vier alten Kulissenhäuser, dann den übrigen Ring, der Rest die Vorstadt (20 Plätze an der Vorstadtgasse x = 64; sie wird nur so weit gezeichnet, wie Häuser stehen). Ohne Bürger-KI (Hauptmenü) füllt eine Mustermischung den Ring. Modelle je `Hausart`: Ratsherrenhaus aus Stein mit Giebel und Erker, Bürgerhaus verputzt mit Giebel, einfaches Haus Fachwerk oder Putz mit Traufe zur Gasse.
- **Gassennetz** (`Stadtansicht.Gassennetz.cs`, Klasse `Gassennetz`): Knoten und Kanten der Darstellung, ohne Rückwirkung auf die Simulation (Leveldesign: grober Wegegraph später im Kern, feine Bewegung hier). `Weg(punkte)` legt Kanten an; Punkte in Fangweite (0,6 m) werden zusammengelegt, Kreuzungen und Einmündungen teilen die Kanten von selbst. `Anschliessen(p)` hängt eine Haustür an die nächste Kante. `Pfad(von, nach)` ist der kürzeste Weg (Dijkstra, gemerkt). Im Netz: Gassen der Werkstätten (wie die Karren: Hof → Gasse → Markt), Ost- und Westgasse, Ringgasse mit zwei Stichwegen, Rathaus- und Domtür, alle Plätze vor der Stadt, die Umlandstellen Weide, Feld und Wald (`Umlandstellen`, Namen wie im Kern) und jede Haustür. Startoption `--wegenetz=1` zeigt das Netz (pink) mit seinen Knoten.
- **Passanten** (`Stadtansicht.Passanten.cs`): Jedes Bild liest die Darstellung je Einwohner `Wo(minute)` und `Weg`. Unterwegs läuft die Figur den kürzesten Pfad im Gassennetz vom alten zum neuen Anlaufpunkt (Haustür, Hof der Schänke oder Kirche, Dom- und Rathaustür, Osttor, Umlandstelle); die Position ist `Weg.Fortschritt(Minute + angebrochene Minute)` × Pfadlänge – jede Figur hat ein eigenes Gehtempo (schlendern 1,5, normal 2,5, eilig 3,5 m/s je echter Sekunde bei Tempo 1, ±10 %, fest nach der Nummer; Boten eilen, Bettler, Witwen und Witwer schlendern). `LaufPlanen` lässt sie so spät losgehen, dass sie zur Ankunftszeit des Kerns da ist; reicht die Zeit nicht, beeilt sie sich bis zum Doppelten und kommt notfalls später an. Jede Figur geht je nach Nummer bis 0,9 m seitlich versetzt und wippt mit dem zurückgelegten Weg. Am Ort ist sie nur draußen zu sehen (Markt, Gasse, Tor, Umland) – wer zu Hause, in Schänke, Kirche oder Rathaus ist, ist unsichtbar; so passt das Bild zu `Am`/`Im`/`ImHaus`. Wo genau jemand steht (`Verweilpunkt`), folgt fest aus Nummer und Ankunftszeit (Hash, kein Würfel). Stehende Figuren werden nur neu gesetzt, wenn sich Ort oder Weg ändern. Rechenzeit (Protokoll alle 10 s): 0,2 ms je Bild für 123 Figuren; alle Figuren teilen Formen und Stoffe. **Aussehen:** Gestalt nach Volk (Zwerg klein und breit mit Bart, Halbling klein, Elb groß und schlank, Haut je Volk), Tracht nach Rolle (`TrachtVon`: Gewandfarbe, Kopfbedeckung, Last, Schürze, gebeugt), Frauen mit Rock und Haube; alle Teile teilen Formen und Stoffe. **Anklicken:** Figuren haben keine Klickfläche; `PassantBei` projiziert jede sichtbare Figur auf den Bildschirm und nimmt die nächste in Fangweite – Figuren gehen beim Treffertest (`KennungBei`) vor Gebäuden. Kennung `einwohner:<Nummer>`; die Hauptszene zeigt dafür `EinwohnerText` (Name, Rolle, Volk, Alter, was er tut und wo, Wohnhaus, Stimmung). Unter der Maus und ausgewählt wird die Figur hell (Stoff getauscht, nur bei Änderung).

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

**KI-Häuser im Rat (PR #73):** Sitzt ein KI-Haus im Rat, fragt `Waehlen` es nach `KiRivale.Wahlstimme(amt, kandidaten)`: vorsichtig bleibt beim Amtsinhaber (außer der hat ihm geschadet), ausgewogen wählt den Ungefährlichsten (Ratsherr vor schwachem vor starkem Haus, nach Reinvermögen), wagemutig nie das führende Haus. „Geschadet“ = die KI hält einen Beweis (`Stadtgericht.Beweise`) für eine Tat, deren Betroffener einer ihrer Betriebe oder ihr Haus ist. `WahlPrognose` zählt KI-Stimmen nach derselben Regel.

**Bewusst offen:** Das Haus des Spielers stimmt bei Wahlen weiter für den bisherigen Inhaber, und bei fremden Anträgen enthält es sich – Abstimmen über die Oberfläche (Befehl im Kern, Ratsfenster) kommt später. Gegenmittel wie Bestechung, Erpressung und das Gericht wirken über die Ratsherren.

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
- **Völkerboni** (Daten Version 23): `berufe.<Beruf>.voelker` (Volk → Bonus, ±0,5 geprüft). `Stadtwirtschaft.VolksFaktor(konto, beruf)` = 1 + Bonus für das Volk des **Oberhaupts** (ohne Familie 1). In `Tick` geht er zweimal in `Betrieb.Arbeitsminute`: im Leistungsfaktor (mit Handwerkskunst und Krankenstand) und als eigener `volksFaktor`, den auch Betriebe ohne Ware (Dieb, Wache, Schreiber) nutzen – für sie zählt sonst nur die Arbeitszeit. Erfahrung wächst mit (je Stück bzw. je Minute). `Kalkulation.Jahresertrag` rechnet ihn je Betrieb mit, `ErwarteterJahresgewinn(…, leistung)` auf Wunsch – die KI übergibt ihn bei der Expansion und wählt so Berufe, in denen ihr Volk gut ist. Oberfläche: Kontor („Handwerk: Tischler (+5 %)“, Auswahl beim Erlernen), Verwaltung („Volk (+5 %)“), Hauptmenü-Hinweis je Volk.
- **Steinmetz** (Daten Version 14): Steinbruch (Bruchstein, Granit), Maurerei → Steinschneiderei → Steinmetzhütte. Verflochten: Die Steinfigur braucht Werkzeug (Schmied, Stufe 2), das Prunkschwert Schleifziegel (Steinmetz).
- **Darstellung:** Der Stadtplan ordnet Namen festen Plätzen zu; `Gassen` gibt einzelnen Werkstätten einen eigenen Weg zum Markt, wo der gerade Weg durch ein Nachbarhaus führte. Das Werkstattmodell wird nach dem **Beruf** gewählt (`Werkstil`: Gießerei mit Esse und Innenraum, Tischlerei mit Schieferdach und Holzstapel, Maurerei aus Stein mit Quadern, Tinkturei verputzt mit Fässern und Kräuterbündeln, Schänke mit Schild und Bänken, Kirche aus Stein mit Glockenturm, Diebesnest aus morschem Holz mit Laterne und vernagelten Fenstern, Wachhaus aus Mauerstein mit Wappenschild, Speergestell und Schilderhäuschen, Wechselstube verputzt mit Wechseltisch, Waage, Münzschild und vergitterten Fenstern, Siederei mit Kupferkessel, Blumenkästen und lila Fensterläden), damit ausgebaute Werkstätten ihr Aussehen behalten. Holzschlag, Steinbruch (Felsen mit Bruchkante) Kräutergarten (Beete, Teich), Bauernhof (Felder, Mühle), Klosterhof (Beete, Bienenkörbe), Wachturm (Steinturm mit Zinnen, Feuerkorb und Palisade) und Duftgarten (Lavendel- und Jasminbeete, Trockengestell) haben eigene Modelle. Farben: Spieler blau, Eisenbart rot, Eichwald grün, Kieselbach ocker.

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
- **Krötenschleim:** neues `Gaunerstueck`, Vergehen `Giftmischerei`; trifft das Oberhaupt um 20 Uhr mit einer Krankheit aus `gesundheit.kroetenschleim`.
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

## Duftwesen (Parfumeur)

- **Ein gewöhnliches Handwerk** (Siederei → Duftwerkstatt → Parfumerie, Duftgarten mit Lavendel und Jasmin, Daten Version 21). Politur ist das Zwischenprodukt und geht jetzt auch in Goldkette und Prunktruhe ein (Grundpreis um den Politurpreis angehoben) – Schmied und Tischler kaufen sie am Markt.
- **Teilsystem `Duftwesen`** (`Duft/Duftwesen.cs`, Abschnitt `parfum`, Zufall ×61+43) für die Wirkungen. Alle Waren kommen über `Beschaffen` aus dem eigenen Lager oder vom Markt – jedes Haus kann sie nutzen; `Preis(konto, ware)` ist 0 aus dem Lager.
- **Moschus:** `MoschusSchenken` → `Familienbuch.ZuneigungSchenken` (Zuneigung ohne `_zuneigungDiesesJahr`, also am Jahreslimit vorbei); `MoschusRatsherr` → `Stadtrat.GunstHinzufuegen` ohne `TatMelden`. Je Kandidat bzw. Ratsherr einmal im Jahr (`_geschenkt`, „Konto|K:Nummer" / „Konto|R:Name").
- **Zwietracht:** `Stadtrat.GunstMindern` (bis −Höchstwert), `Vergehen.Intrige`. **Heiligenduft:** `_heiligBis`, `Stadtrat.Ansehen` + `AnsehenBonus`.
- **Giftanschlag:** Befehl legt einen `Giftauftrag` an; `Nacht()` läuft nach `Diebe.Nacht` und würfelt nur, wenn es Aufträge gibt – ohne Gift verläuft eine Partie Wurf für Wurf gleich (Test). Chance = Dolch + `NachtBonus`, × `Wache.Patrouillefaktor(null)`; Zeugen über `Unterwelt.Zeugenchance`, Meldungen über `Unterwelt.Melden` (Hinterzimmer). `Vergehen.Giftanschlag` steht in `justiz.kerkerFuer`.
- **Vergiftung in der Gesundheit:** `KrankheitDaten.Gegengift` → `Erkrankung.NurGegengift`. Medicus und Schnaps lehnen ab; `Gesundheitswesen.GegengiftGeben` nimmt `gesundheit.gegengift` aus dem Lager oder vom Markt. `Jahresende` würfelt den Tod erst am Ende des letzten Jahres (`dauer` 2) – so bleibt ein Jahr für das Gegengift (sonst wäre ein Anschlag um 21 Uhr eine reine Lotterie vor Mitternacht).
- **KI:** schenkt Moschus, wenn das Jahreslimit der Geschenke erreicht ist; gibt Gegengift statt Medicus. `Vergiften` nur wagemutig, nur wenn sich keine Bande lohnt, höchstens einmal im Jahr, nur gegen ein Haus mit 1,5-fachem Reinvermögen (zählt zum Gaunerei-Budget).

## Glaubenswesen (Konfessionen)

- **Teilsystem `Glaubenswesen`** (`Glaube/Glaubenswesen.cs`, Abschnitt `glaube`, ab Datenversion 22, Zufall ×67+47). `Konfession` = Katholisch, Evangelisch, Naturglaube. Fehlt der Abschnitt, ist alles wie vorher (Test).
- **Personen:** `Person.Glaube` (heute) und `Herkunftsglaube` (bleibt bei Übertritt). `Familienbuch.Gruenden(…, glaube)` nimmt den gewählten oder den `Startglaube` des Volkes (höchstes Gewicht in `glaube.voelker`); Kulturvölker dürfen nicht mit Naturglauben beginnen, Orks/Oger nur mit ihm. Heiratskandidaten würfeln ihren Glauben mit dem eigenen Generator des Glaubenswesens – Namen, Talente und Leben bleiben Wurf für Wurf wie vorher. Kinder bekommen den Glauben des Oberhaupts. Ein Haus = Glaube seines Oberhaupts (`GlaubeVon(konto)`).
- **Rat:** `RatsherrDaten.Glaube` (null = Startglaube des Volkes). `Stadtrat.Punkte` addiert `Glaube.Naehe` (gleich / anders / Heide, dazu `mischehe`, wenn der Ehepartner übergetreten ist und der Ratsherr seinen Herkunftsglauben hat). `Stadtrat.Ansehen` minus `AnsehenAbzug` nach dem Konvertieren.
- **Messe:** `Kirchenwesen.Verteilen` teilt die Gläubigen nach `Anteile` auf; jede Konfession verteilt ihren Teil nach Anziehung auf ihre Kirchen, ohne eigene Kirche auf alle mit `fremdeKirche` als Spendenfaktor. Eine Kirche ohne Familie (Tests, alte Stände) nimmt jeden auf. `ErwarteteGlaeubige`/`ErwarteteKollekte` rechnen dasselbe.
- **Befehle:** `Konvertieren` (2 AP, Gebühr, ganzes Haus, `_konvertiertBis`), `Bekehrungspredigt` (1 AP, eigene Kirche mit Personal, einmal im Jahr; `Verschieben` nimmt den anderen gleichmäßig bis `mindestAnteil`; Gunst über `Stadtrat.GunstHinzufuegen`), `StadtglaubeBeantragen` (2 AP, nur Ratsmitglieder, ein Antrag je Jahr – unabhängig vom Statut-Antrag).
- **Stadtglaube:** `Abstimmen` läuft in `Stadtrat.Sitzung` nach dem Statut und schreibt ins Sitzungsprotokoll. `StimmtZu`: eigener Glaube ja; Freiheit, wenn ein fremder gilt oder Gunst ≥ `schwelleFrei`; fremder Glaube ab `schwelleFremd`. KI-Häuser im Rat stimmen nach ihrem Glauben. `AbgabenZahlen` (vor dem Jahresabschluss, als `Steuer`), `Jahresende` (nach dem Kirchenbuch, vor der Beichte): `Vergehen.Ketzerei` je Ketzerkirche mit Kollekte als Wert, Beweis für alle Häuser des Stadtglaubens; danach kehren die Anteile um `rueckkehr` zurück. Ein Haus, das aus dem Rat ausscheidet, verliert seinen Antrag (`Ausscheiden`).
- **KI:** `Glaubenspolitik` nach `Politik`: unter fremdem Stadtglauben übertreten (wenn die Reserve hält), im Rat den eigenen Glauben beantragen, wenn die Prognose ihn durchbringt (sonst Freiheit), Bekehrungspredigt nur mit Aktionspunkten, die sonst verfielen, und nie als Ketzerpredigt.
- **Speichern:** `PersonStand.Glaube/Herkunftsglaube` (null in alten Ständen → Startglaube), `GlaubeStand` (Anteile, Stadtglaube, Antrag, Konvertiten, Predigten, Glaubenszins des Vorjahres).
- **Oberfläche:** Hauptmenü (Glaube; Startoption `--glaube=`), Familie (Glaubenszeile mit Konvertieren, Übertritt des Ehepartners, Glaube der Kandidaten), Rat (Stadtglaube, Konfession der Inhaber mit Wahlpunkten im Tooltip, Antrag zum Stadtglauben), Kirchenstube (Konfession, Gemeinden, Ketzerei-Warnung, Bekehrungspredigt).

## Unterwelt

Die Unterwelt (`src/Reichsgilde.Core/Gauner/Unterwelt.cs`) gehört zur Stadtwirtschaft (`Stadtwirtschaft.Gauner`). Alle Zahlen stehen in `wirtschaft.json` im Abschnitt `gauner`.

- **Bestellen und Ausführen sind getrennt.**
  - `Beauftragen` bezahlt die Bande sofort.
  - Ausgeführt wird in `Unterwelt.Nacht`, die die Stadtwirtschaft um 20:00 aufruft (`Unterwelt.Nachtstunde`).
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

## Wappen und Hausnamen

- **Kern:** `Dynastie/Wappen.cs` beschreibt ein Wappen als Datensatz `Wappen(Teilung, Farbe1, Farbe2, Figur)` – nur Aufzählungen, keine Grafik. Dazu die zwölf `Vorlagen`, die festen Wappen der KI-Häuser (`Rivalenwappen`), `Zufall` (je ein Metall und eine Farbe, nie ein Rivalenwappen) und `Beschreibung()` in der Sprache der Herolde mit dem richtigen Geschlecht der Figur („eine goldene Krone“, „ein silbernes Rad“).
- **Konto:** `Konto.Wappen` gehört zum Haus. Ohne Wahl würfelt `Wappen.AusName` eines aus dem Namen – mit eigener Prüfsumme (FNV-1a), denn `string.GetHashCode` liefert in .NET bei jedem Programmstart andere Zahlen. So bekommen alte Spielstände ohne Wappen immer dasselbe.
- **Partieaufbau:** `Partieaufbau.Hausname` macht aus „Hammerschmid“, „Familie Hammerschmid“ und „Haus Hammerschmid“ einheitlich „Haus Hammerschmid“. Ein Wappen mit zwei gleichen Farben oder das Wappen eines KI-Hauses lehnt er ab.
- **Zeichnen:** `game/scripts/WappenBild.cs` zeichnet in `_Draw()` aus Vielecken: Schild in Farbe 1 → Teilung in Farbe 2 (mit dem Schild verschnitten über `Geometry2D.IntersectPolygons`) → Figur in Farbe 2 → die Stücke der Figur, die über Farbe 2 liegen, noch einmal in Farbe 1. Eine Figur besteht aus mehreren einfachen Vielecken ohne Löcher (ein Rad aus 24 Kranzstücken), weil verschnittene Löcher sich nicht einfach füllen lassen. Keine Bilddateien: scharf in jeder Größe, und eine neue Figur ist ein paar Zeilen Koordinaten in `WappenFormen`.
- **Anzeige:** `UiHilfe.MitWappen` setzt ein kleines Wappen vor einen Hausnamen; ohne Wappen (Ratsherren ohne Haus) bleibt der Platz frei, damit die Spalte bündig bleibt.

## Bürger-KI (Einwohner)

- **Teilsystem `Einwohnerschaft`** (`Buerger/Einwohnerschaft.cs`, Bereich Spiel-KI, Abschnitt `buerger`, ab Datenversion 25, Zufall ×71+53). Fehlt der Abschnitt, gibt es keine Einwohner und kein Feld im Spielstand.
- **Wer:** `jeHaus` (25) gewöhnliche Einwohner je Haus, beim ersten Takt mit Häusern festgelegt (`Herkunft.Stadt`); dazu die Ratsherren (`Herkunft.Ratsherr`, Quelle = Name) und die Heiratskandidaten (`Herkunft.Kandidat`, Quelle = Personennummer). Ratsherren und Kandidaten bleiben in Rat und Familienbuch; die Einwohnerschaft gleicht jede Stunde mit der Brautschau ab und ändert dort nichts.
- **Utility-KI:** Fünf `Beduerfnis`-Werte (0–100 = Dringlichkeit) steigen jede Stunde um `anstieg × Gewicht(Volk)`. Jede mögliche `Handlung` hat einen Nutzen = Σ min(Dringlichkeit, Stillwert) × Gewicht, dazu `beharrung` für die Handlung der letzten Stunde, mal (1 ± `rauschen`). Die beste gewinnt und stillt ihre Bedürfnisse. Zur Messe-Stunde wirkt Beten `messe`-fach. Am Jahresende schlafen alle aus (`start` ± `startStreuung`).
- **Orte:** Handlung → `Stadtort` (Ruhen → Heim, Arbeiten → Arbeitsort der Rolle, Einkaufen → Markt, Einkehren → Schänke, Beten → Kirche, Flanieren → Gasse). Schänke und Kirche fest nach der Nummer des Einwohners (Stammhaus); Kirche bevorzugt die eigene Konfession, ohne Kirche `Betrieb = null` (Stadtkirche).
- **Takt:** `Stadtwirtschaft.Tick` ruft am Ende `Einwohnerschaft.Minute` (plant zur vollen Stunde), die Jahresabrechnung am Ende `Einwohnerschaft.Jahresende` (Gompertz-Sterben über `Familienbuch.Sterberisiko`, Zuzug an derselben Listenstelle, Ausschlafen).
- **Keine Wirkung (Schritt 1):** Die Einwohner lesen Schänken, Kirchen, Rat und Brautschau nur und würfeln nur mit ihrem eigenen Generator. Ein Test vergleicht die Partie mit und ohne Abschnitt `buerger`.
- **Wohnhäuser** (`Stadthaus`, `Hausart`): beim Gründen Haushalte zu `haushaltVon`–`haushaltBis` Personen (einfache Wohnhäuser), `buergerhaeuser` Bürgerhäuser für Kandidaten ab Stand Bürger, ein Ratsherrenhaus je Ratsherr. Zuzügler übernehmen das Haus des Verstorbenen. `Einwohner.Wohnhaus` = eigenes Haus, `Einwohner.Haus` = Haus, in dem er gerade ist (Rollen mit `dienst` arbeiten in einem Bürger- oder Ratsherrenhaus).
- **Wege:** `Aufenthalt` = (Ort, Betrieb, Haus, Stelle). Ändert er sich bei der Planung, entsteht ein `Weg` (Von, Nach, Abmarsch, Ankunft in `VergangeneMinuten`): Abmarsch 0–`verzoegerung` Minuten nach der Stunde, Dauer `stadt` ± `streuung`, `tor` oder `umland`. Die Werte (Stadt 30 ± 10, Tor 40, Umland 50, Abmarsch 0–9 Minuten) passen zum Gehtempo der Passanten im Gassennetz (~2,5 m/s, Weglängen siehe `docs/bereiche/darstellung.md`). Die Prüfung verlangt, dass jeder vor der nächsten Stunde ankommt. `Wo(minute)` liefert den Aufenthalt oder null (unterwegs); `Am`/`Im`/`ImHaus` zählen nur, wer da ist – so passen Bild und Zählung zusammen. Über Nacht (Jahresende) gibt es keinen Weg.
- **Umland:** Rollen mit Arbeitsort Umland haben eine `stelle` aus `umland` (Weide, Feld, Wald).
- **Kopplung an die Schänke** (`buerger.kopplung`, PR #58): `Gastwirtschaft.Stunde` holt die Gäste je Schänke aus `Einwohnerschaft.Gaesteanteile`. Ohne Kopplung ist das genau die alte Formel (gaeste × Anziehung ÷ Summe). Mit Kopplung k: (1 − k) × alt + k × gaeste × Faktor × Anteil. Faktor = Einkehrende (Ort Schänke, in der Stunde, die gerade endet – geplant wird erst danach) ÷ `EinkehrErwartet(stunde)`, begrenzt auf 1 ± `spanne`; das Übliche je Uhrzeit ist ein gleitender Mittelwert (`glaettung`), der erste Tag setzt es (Faktor 1). Anteil = Einkehrende der Schänke ÷ alle Einkehrenden. `SchaenkenVerteilen` vergibt die Schänke nach der Planung als Quote nach `Gastwirtschaft.Anziehung` (größte Reste; Stammschänke des Tages zuerst) – ohne Zufall, damit die Verteilung nach Anziehung exakt bleibt.
- **Kopplung an die Kirche** (`kopplung.kirche`, PR #61): `Kirchenwesen.Stunde` multipliziert zur Messe Gläubige und Kollekte jeder Kirche mit `Einwohnerschaft.Messefaktor()` = (1 − k) + k × Gebetsstunden ÷ üblich (1 ± `spanne`). Gezählt werden alle Einwohner-Stunden in Kirchen seit Tagesbeginn bis zur Messe (`_betendeHeute`, eine einzelne Stunde schwankte um ± 30 %). Die Verteilung auf die Kirchen bleibt beim Kirchenwesen (Konfessionen der Stadt, Anziehung). Die Einwohner verteilen sich selbst mit derselben `Quote` wie bei den Schänken auf die Kirchen ihrer Konfession (`Stammkirche`). Der Messe-Bonus fürs Beten gilt der Stunde vor `messeUm`, denn abgerechnet wird zur vollen Stunde `messeUm` – für die Stunde, die dann endet.
- **Kopplung an den Markt** (`kopplung.markt`, PR #63): Jede volle Stunde legt `EinkaufZaehlen` die Einkaufenden der Stunde, die gerade endet, in einen Ring aus 17 Stunden (wie das Verkaufsfenster des Marktes) und setzt `Marktstand.NachfrageFaktor` aller Waren mit `Typ` in `marktTypen` (Fertigprodukt, Heilmittel) auf `Marktfaktor` = (1 − k) + k × Ringsumme ÷ üblich am Tag (1 ± `marktSpanne`). Im Preis steht `Nachfrage` = Daten × Faktor; die Grundversorgung (Nachfrage der Daten × NPC-Anteil) bleibt. Der Faktor steckt nicht im Marktstand-Spielstand – `Wiederherstellen` setzt ihn aus Ring und Üblichem neu.
- **Kopplung an die Taschendiebe** (`kopplung.diebe`, PR #69): `Diebeswesen.Stunde` multipliziert die Beute mit `Einwohnerschaft.Diebesfaktor(stunde)` = (1 − k) + k × Passanten ÷ `PassantenErwartet(stunde)` (1 ± `spanne`). `Passanten` = Ort Markt oder Gasse, oder ein Weg mit Abmarsch in der Stunde, die gerade endet (nicht ins/aus dem Umland). Beim Fang wählt `Bestohlen()` mit dem Zufall der Bürger einen Passanten; sein Name kommt in Meldung und Spur, seine Versorgung steigt um `bestohlen`. Ohne Kopplung null → „Bürger am Markt“ wie vorher.
- **Das Übliche lernen** (`Lernen`): Der erste Tag zählt nicht (Einschwingen), dann der Mittelwert der ersten Tage, danach gleitend mit `glaettung`. Zähler je Größe im Spielstand (`Gelernt`); ältere Spielstände gelten als eingeschwungen.
- **Abfragen für Darstellung und Oberfläche:** `Passanten`, `PassantenErwartet(stunde)`, `Marktfaktor`, `Einkaufende`, `EinkaeufeImFenster`, `EinkaufErwartet`, `Betende`, `MesseErwartet`, `Einkehrende(schaenke)`, `EinkehrErwartet(stunde)`, `Wohnhaeuser`, `Bewohner(haus)`, `ImHaus(haus)`, `Unterwegs`, `Einwohner`, `Am(Stadtort)`, `Im(Betriebsname)`, `Nummer(n)`, `RollenName(e)`, `Stimmung`, `VerstorbenImVorjahr`/`ZugezogenImVorjahr`; je Einwohner `Handlung`, `Ort`, `Betrieb`, `Wert(Beduerfnis)`, `Stimmung`.

## Speichern und Laden

Ein Spielstand ist eine lesbare JSON-Datei (`src/Reichsgilde.Core/Speichern/`).

- **Datenpakete:** Jede Klasse liefert ihren Zustand mit `Sichern()` als reines Datenpaket und kann ihn mit `Wiederherstellen()` übernehmen. Private Felder bleiben privat, und das Dateiformat hängt nicht an den Klassen.
- **Verweise:** Statt Objektverweisen stehen Nummern oder Namen in der Datei, zum Beispiel „Betrieb gehört Konto Nr. 0“.
- **Wirtschaftsdaten:** Sie werden mitgespeichert. Eine alte Partie läuft also mit ihren Zahlen weiter, auch wenn `wirtschaft.json` inzwischen geändert wurde.
- **Zufall:** Der .NET-Zufallsgenerator lässt sich nicht speichern. `Zufallsquelle` zählt deshalb nur die Würfe. Beim Laden würfelt ein frischer Generator mit gleichem Startwert genauso oft „leer“ und ist danach im selben Zustand.
- **Prüfung:** Der Gleichlauf-Test speichert eine Partie nach 3½ Jahren, lädt sie und lässt Original und Kopie 5 Jahre weiterlaufen. Beide müssen danach Zeichen für Zeichen denselben Spielstand ergeben. Vergisst das Speichern ein Feld, schlägt der Test fehl (gegengeprüft).
- **Formatversion:** Die Datei trägt ein Format. Eine neuere Datei lehnt eine ältere Programmversion mit verständlicher Meldung ab.

Die Startaufstellung einer neuen Partie steht in `Partie/Partieaufbau.cs`. Hauptmenü, Tests und Startoptionen benutzen dieselbe Aufstellung.
