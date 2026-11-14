# Steuerung

*Tasten, Maus, Fenster und Tooltips*

Dieses Kapitel zeigt, wie du durch die Stadt kommst, Gebäude öffnest, baust und mit den Fenstern umgehst. Wer nur schnell etwas nachschlagen will, springt zur [Tastenübersicht](#tastenübersicht).

## Erste Schritte

1. **Leertaste** hält die Zeit an und lässt sie weiterlaufen.
2. Mit dem **Mausrad** zoomst du, mit der **rechten Maustaste gezogen** schiebst du die Stadt zurecht.
3. Ein **Klick** auf ein Gebäude zeigt seine Tafel, ein **Doppelklick** auf deine Werkstatt führt dich hinein.
4. **Esc** geht immer einen Schritt zurück, zuletzt ins Spielmenü mit Speichern, Laden und Einstellungen.
5. **F1** zeigt die Tastenhilfe. Beim allerersten Spiel erscheint sie einmal von selbst.

> [!TIP]
> Die Zeit läuft auch weiter, während eine Tafel offen ist. Drück vor einer größeren Entscheidung die **Leertaste**: In der Pause kannst du in Ruhe lesen und rechnen.

## Der Bildschirm

![Der Hauptbildschirm mit nummerierten Markierungen](../bilder/handbuch/steuerung-bildschirm.webp)

| Nr. | Was | Wozu |
|---|---|---|
| **1** | **Dein Haus** (Kopfzeile) | Wappen, Geld, Stand und Aktionspunkte (AP, hier 4 von 12). Darunter steht, wohin die Kamera blickt. Ein Klick öffnet das **Wohnhaus** mit Stand, Familie, Handwerk, Geld und Hinterzimmer. |
| **2** | **Zeitstrahl** | Jahreszeit, Jahr, Uhrzeit und Tageszeit, dazu das Wetter mit Aussicht. Das Band darunter zeigt den Spieltag von 4 bis 23 Uhr (Zunftglocke, Hoher Mittag, Vesper, Nacht); die Sonne wandert darauf, nachts der Mond. Wann das Band dunkel wird und der Mond erscheint, richtet sich nach den Sonnenzeiten der Jahreszeit (Tabelle im Kapitel „Stadt und Ansicht“, der Tooltip nennt sie). |
| **3** | **Pause und Tempo** | Pause (Leertaste) und Tempo 1×, 2×, 4× und 8× (Tasten **1** bis **4**). |
| **4** | **Betriebsleiste** | Ein Knopf je eigenem Betrieb mit seinem Zustand („läuft“, „steht“, „Karren unterwegs“). Ein **Klick** fährt hin und zeigt die Kurzübersicht, ein **Doppelklick** geht hinein. Bei vielen Betrieben rollt die Leiste. |
| **5** | **Knöpfe rechts** | **Hafen** (nur auf Karten mit Schiffshandel), **+ Bauen**, **Rat**, **Bericht**, **Lexikon** und **Menü**. |
| **6** | **Meldungen** | Die letzten vier Ereignisse mit Uhrzeit. Sie verblassen nach 20 Sekunden; Warnungen (✗ ein Befehl ging nicht, ⚠ Gefahr) bleiben, bis du sie anklickst. Ein Klick öffnet das **Protokoll des Jahres** mit allen Meldungen. |
| **7** | **Tafel rechts** | Erscheint nur, wenn du etwas anklickst, hier die Kurzübersicht der Gießerei. |
| **8** | **Tooltip** | Ein Hinweistext, wenn die Maus auf einer Zeile ruht; hier ist er festgeheftet (✕). Mehr unter [Fenster und Tooltips](#fenster-und-tooltips). |

> [!NOTE]
> Die Tempostufen **16×** und **32×** (orange, Tasten **5** und **6**) sind nur zum Testen gedacht und fallen vor der Veröffentlichung weg.

## Kamera

| Du willst … | So geht es |
|---|---|
| näher heran oder weiter weg | **Mausrad**: im Pixel-Look (Standard) in drei Stufen – nah 25 m, mittel 50 m, weit 100 m sichtbare Bildhöhe, der Start steht auf „weit“; ohne Pixel-Look stufenlos |
| die Stadt verschieben | **rechte Maustaste halten und ziehen**, oder **W A S D**, oder die **Pfeiltasten** |
| um die Stadt herum schauen | **Q** und **E** drehen die Kamera in Vierteln (90°); in der Kopfzeile steht, wohin du blickst |
| alle Namen sehen | **Alt** gedrückt halten |

Der Name eines Gebäudes steht über ihm, wenn die Maus darauf ruht oder wenn es ausgewählt ist. Am Rand der Karte bremst die Kamera von selbst.

Freie Plätze tragen ein Schild am Pfosten: **ocker** heißt **zu verkaufen**, **grün** **zu verpachten** (mehr dazu in [Bauen und Leerstand](bauen-und-leerstand.md)).

**Freie Kamera (zum Ansehen und Testen).** Mit der Startoption `--freiekamera=1` fliegst du frei durch die Stadt statt mit der schrägen Spielkamera, auch ganz nah an Wasser, Häuser und Figuren heran. Die Testszenen (zum Beispiel `--wassertest=1`) starten immer so.

| Du willst … | So geht es |
|---|---|
| dich umsehen | **rechte Maustaste halten** und die Maus bewegen (die Maus ist dabei gefangen; ohne die Taste bleibt sie frei für die Tafeln) |
| fahren | **W A S D** in Blickrichtung, **Q** runter, **E** hoch |
| schneller | **Shift** halten (viermal so schnell), das Grundtempo stellt das **Mausrad** ein |
| die Hilfe oben rechts aus- und einblenden | **F2** auf der Spielkarte (H öffnet dort das Hinterzimmer), **H** in den Testszenen |

Klicks auf Gebäude gehen wie gewohnt. Mausrad, Q/E und das Ziehen mit der rechten Taste steuern dann nur die freie Kamera.

## Gebäude anklicken und betreten

- **Linksklick** auf ein Gebäude öffnet seine Tafel neben dem Zeiger; sie lässt sich verschieben und schließen. Bei deinem **Betrieb** ist es die Kurzübersicht, beim eigenen **Wohnhaus** das Wohnhaus mit Reitern, beim **Rathaus** der Rat mit Ämtern, Statut und Gericht, beim **Marktplatz** der Markt mit den Preisen. Hafen, Kran, Schuppen und eigene Schiffe öffnen die Hafentafel.
- **Doppelklick** auf eine Werkstatt, das Wohnhaus, das Rathaus oder den Dom geht **hinein**. In der Betriebsleiste gilt dasselbe: Klick = hinfahren und Kurzübersicht, Doppelklick = hinein.
- **Drinnen** wechselst du den Raum mit der **Raumleiste** (unten rechts) oder mit **Q** und **E**. Ein **Klick** auf einen Gegenstand öffnet seine Tafel (Produktion, Personal, Lager, Karren, Einrichtung …), ein Klick auf eine Tür wechselt den Raum. **Rechtsklick** oder **Esc** führt wieder hinaus. Die Ansicht im Raum ist fest: Zoomen und Verschieben gibt es nur draußen.
- In Räumen mit mehreren Blickpunkten schwenkst du, indem du die **Maus kurz an den Bildrand** hältst; goldene Pfeile zeigen, wohin es noch geht. Mehrere Arbeiter wählst du mit einem **Rahmen** (linke Taste auf freier Fläche ziehen) oder mit **Umschalt** bzw. **Strg** beim Anklicken.
- Ein Klick auf ein **Porträt** (zum Beispiel im Familienbuch, im Rat oder bei den Rivalen im Bericht) öffnet das **Charakterfenster**: Talente, Charakterzüge, Familie – und bei wichtigen Personen den Knopf **Aktivitäten** (Geschenk, Gastmahl, Zechen, Messe, Brettspiel, Jagd). Bei Oberhaupt, Ehepartner und Erbe gibt es dazu den Reiter **Lebensweg** (Pfad wählen, Knoten lernen); freie Punkte meldet die Kopfzeile, ein Klick darauf öffnet ihn. Mehr dazu in [Haus und Dynastie](haus-und-dynastie.md).

> [!TIP]
> Die Betriebsleiste ist der schnellste Weg zu deinen Werkstätten: Ein Klick auf den Knopf fährt die Kamera hin, du musst nicht erst suchen.

## Der Mauszeiger

Der Zeiger ist ein **Lederhandschuh** in feiner Pixel-Art (die Fingerspitze zeigt), der mit dem **Stand deines Hauses** edler wird: abgewetztes graues Arbeitsleder mit Flicken beim Gemeinen, sauberes braunes Leder beim Bürger, Bordeaux mit Silberring beim Patrizier, Bordeaux mit Goldring, Goldnaht und Goldschnalle beim Edlen, fast schwarzes Purpurleder mit Goldring und Rubin beim Freiherrn, Königsblau mit Goldringen, Edelsteinen, Goldborte und Hermelinpelz beim Reichsgrafen. Beim Aufstieg wechselt der Handschuh gleich. Zwei weitere gibt es nur zur freien Wahl: den **Plattenhandschuh der Wache** und das **schwarze Leder des Diebs**. Über **Anklickbarem** (Gebäude, Figuren, Knöpfe, Bauplatz im Baumodus) bekommt er einen **weichen weißen Schein** um den Handschuh herum (die Lederfarben bleiben, ein heller Rand fehlt auch sonst); über einem Haus, das du mit Doppelklick **betreten** kannst, wird er zum **Türschloss** mit S-Griff aus Bronze, ebenfalls mit Schein. Die Größe (48, 64 oder 80 Pixel; Standard 64 bei 1080p) folgt der Fensterhöhe und der Größe der Oberfläche. In den **Einstellungen** (Barrierefreiheit) lässt sich „Eigener Mauszeiger“ ausschalten, dann gilt der Zeiger deines Systems. Darunter wählst du den **Handschuh**: „Auto“ folgt dem Stand, oder du nimmst fest einen der acht, unabhängig vom eigenen Stand (reine Zierde); die Wahl wirkt sofort, auch im Hauptmenü und in der Kartenwerkstatt, und wird mit den Einstellungen gespeichert, nicht im Spielstand.

**3D-Handschuh.** Der Handschuh ist standardmäßig ein bewegtes 3D-Modell (Einstellungen, Barrierefreiheit, „3D-Handschuh“): Er **atmet** leicht, **zeigt** mit dem Zeigefinger, sobald du über Anklickbarem bist, **tippt** beim Klick und **greift** (Faust), solange du mit gedrückter Maustaste ziehst, zum Beispiel die Kamera schiebst oder malst. Beim Bewegen neigt er sich mit der Maus. Die **Fingerspitze** ist der Klickpunkt. Das **Fragezeichen** erscheint nur, wo es wirklich einen Tooltip gibt. Über einer Fläche, die du verschieben kannst (Stammbaum), steht an seinem Platz eine **Windrose** aus Messing; die Hand zeigt dort auch beim Ziehen weiter. Über einem **Textfeld** schrumpft die Hand in einer Zehntelsekunde weg, und an ihrer Stelle wächst ein **römisches I** aus Messing: es ist der Textcursor, seine Mitte setzt die Einfügemarke zwischen zwei Buchstaben. Verlässt du das Feld, schrumpft das I und die Hand kommt wieder (auch in Dialogen und Auswahllisten). Er bleibt auch bei niedriger Bildrate, solange du ihn eingestellt hast; auf die gemalten Zeigerbilder geht es nur, wenn du ihn abschaltest (Startoption `--handschuh=0`). Die Wahl des Handschuhs nach Stand gilt nur für die gemalten Bilder.

**Schwarzblende.** Beim Betreten eines Hauses (Doppelklick) und beim Verlassen (Rechtsklick, Esc, „Zur Stadt“) blendet das Bild kurz ins Schwarz und wieder heraus; im Schwarzen baut das Spiel den Raum auf, damit beim ersten Betreten kein Standbild entsteht. Mit dem 3D-Handschuh drückt vorher die Hand die Klinke. Abschalten für Tests: `--blende=0`.

**Klinke (3D-Handschuh).** Über einem Haus, das du betreten kannst, öffnet der 3D-Handschuh die Hand, und darunter erscheint ein Türschloss mit Klinke; die Maus zielt dann auf das Schloss. Der erste Klick öffnet wie bisher die Kurzübersicht; mit dem Doppelklick schließt sie sich wieder und die Hand greift die Klinke und drückt sie nieder, dann blendet das Bild ins Schwarz. Ist der Raum offen, lässt die Hand los.

## Bauen

**Handschuh lebt.** Ziehst du ein Auswahlrechteck in der Welt auf, zeigt der Handschuh mit dem Zeigefinger statt der Faust (Dinge greifen und verschieben bleibt die Faust). An der Tür atmet die offene Hand und zuckt alle paar Sekunden ungeduldig. Wenn du eine Minute lang nichts tust (keine Maus, keine Taste), dreht sich die Hand zu dir und winkt; das wiederholt sich nach zwei bis drei Minuten, bis du dich wieder rührst.

Das Baumenü öffnest du mit dem Knopf **+ Bauen** oder der Taste **B**. Es hat zwei Reiter: **Gebäude** und **Wege (G)**.

![Bauen: Platz wählen mit grünem Umriss](../bilder/handbuch/steuerung-bauen.webp)

1. Wähle in der Liste ein Gebäude (Wohnhaus oder Werkstatt). Die Tafel wechselt zu **Platz wählen**.
2. Ein **Umriss** folgt der Maus. **Grün** heißt: Hier darf gebaut werden. **Rot** heißt: Hier nicht; der Grund steht an der Maus und in der Tafel („Hier ist keine Bauzone …“, „Hier steht Ringplatz 11.“; fehlt Geld oder Stand, steht auch das schon hier).
3. **R** (oder der Knopf **Drehen**) dreht das Gebäude, damit der Eingang zur Straße zeigt. **Q** und **E** drehen weiter die Kamera.
4. **Klick** setzt das Gebäude; danach folgt eine Rückfrage mit den Kosten.
5. **Rechtsklick** oder **Esc** geht zurück zur Auswahl, ein weiteres **Esc** schließt das Baumenü.

| Nr. | Was |
|---|---|
| **1** | Die Tafel **Platz wählen**: Gebäude, Größe, Bauzeit und Kosten |
| **2** | Die Meldung zum Platz: grün „Hier kann gebaut werden“, sonst rot mit dem Grund |
| **3** | Der Umriss an der Maus |
| **4** | **Bauzone Kern** (orange), das Raster misst 2 m |
| **5** | **Bauzone Vorstadt** (türkis); dazu kommt der **Mauerring** (blau) |
| **6** | **Drehen (R)** und **Zurück** |
| **7** | Der Knopf **+ Bauen** in der Betriebsleiste |

> [!NOTE]
> Gebaut wird nur in den farbigen **Bauzonen** und auf **Bauplätzen**, mit der Vorderseite an einer Straße. Ein kurzer Rechtsklick bricht ab; wer die rechte Taste **gedrückt hält und zieht**, verschiebt auch im Baumodus weiter die Karte.

**Raster:** Im Baumodus liegen feine Linien alle 2 m und kräftigere alle 10 m auf dem Gelände, rund um die Maus bzw. den Umriss; nach außen blenden sie aus. Sie sind nur eine Hilfe: Gebaut wird ohnehin auf dem 2-m-Raster und in Vierteldrehungen, der Umriss zeigt also genau, wo das Gebäude landet. **V** blendet das Raster ein und aus (Standard: an; Startoption `--raster=0` schaltet es ab).

### Wege-Modus

Mit **G** schaltest du im Baumenü auf den Reiter **Wege**. Ist das Baumenü zu, öffnet G es gleich auf diesem Reiter; noch einmal G schaltet zurück zu den Gebäuden.

![Wege-Modus mit leuchtenden Wegen und Tooltip](../bilder/handbuch/steuerung-wege.webp)

| Nr. | Was |
|---|---|
| **1** | Der Reiter **Wege (G)** |
| **2** | Alle Wege der Stadt **leuchten** in der Farbe ihres Belags |
| **3** | Der **Tooltip** am Weg unter der Maus: Belag, nächste Stufe, Kosten und Bauzeit. Ein **Klick** öffnet die **Wegtafel** zum Ausbauen. |
| **4** | Die **Legende**: Trampelpfad, Fahrweg, Kies, Pflaster, Baustelle |
| **5** | **Schließen (Esc)** |

Im Wege-Modus sind nur Wege anklickbar, Gebäude und Boden nicht.

## Fenster und Tooltips

### Fenster

Jede Tafel hat oben rechts ein **✕** und lässt sich am **oberen Rand ziehen**. Jedes Fenster öffnet **in der Bildschirmmitte** (zwischen Zeitstrahl und Betriebsleiste), auch wenn du es vorher verschoben hattest; festgehaltene Fenster, die zugleich offen sind, liegen leicht versetzt. Nur die kleine Auswahl am Mauszeiger und die Tafeln beim Bauen bleiben an ihrem Platz. Ein Klick holt ein Fenster nach vorn, **Esc** schließt das oberste, **X** schließt alle. Der Knopf mit der **Nadel** („Festhalten“, bei Rat, Familie, Markt, Wohnhaus, Bericht, Hafen …) lässt ein Fenster offen, wenn eine andere Tafel aufgeht. So liegen mehrere nebeneinander, etwa Markt und Betrieb zum Vergleichen.

Tafeln, die nicht ins Fenster passen, bekommen Scrollbalken; das **Mausrad** über der Tafel rollt sie. Liegt die Maus über einem Fenster, einer Tafel, einer Leiste oder dem Menü (auch über dessen Rand), bekommt die Stadt dahinter weder Klick noch Mausrad noch Hervorhebung; nur über freier Stadt reagiert sie. Das Fragezeichen als Zeiger erscheint nur über Dingen, die wirklich einen Tooltip zeigen (zum Beispiel den Charakterzügen).

### Tooltips

![Ein Tooltip mit füllendem Kreis und Verweis](../bilder/handbuch/steuerung-tooltip.webp)

Fast alles im Spiel hat einen Tooltip. In Produktion, Lager, Personal, Einrichtung, Karren und Kurzübersicht zeigen Zeilen, Spaltenköpfe, Waren und Arbeiter beim Darüberfahren Preis, Lager, Zeit, Zutaten und Ertrag je Stück beziehungsweise Laune, Tempo und Lohn.

| Nr. | Was |
|---|---|
| **1** | Der **Kreis** füllt sich, solange die Maus ruht. Ist er voll, **bleibt der Tooltip** als kleines Fenster mit ✕ stehen und lässt sich verschieben. |
| **2** | **Unterstrichene Wörter** sind Verweise. Fährst du darüber, geht ein weiterer Tooltip zum Begriff auf (er kommt aus dem Lexikon). Ein Klick auf den Verweis heftet ihn sofort fest. |
| **3** | Die Zeile, zu der dieser Tooltip gehört |
| **4** | Das **✕** schließt die Tafel |

- **T** oder die **mittlere Maustaste** heftet einen Tooltip sofort fest.
- Wie lange das Festheften dauert, stellst du in den Einstellungen unter **Barrierefreiheit** ein: 0,5 bis 5 Sekunden oder „nur mit Taste“.
- Ein Verweis-Tooltip bleibt offen, wenn die Maus hineinfährt, und wird nach derselben Zeit fest.

> [!TIP]
> Festgeheftete Tooltips taugen als Merkzettel: Den Tooltip zu einer Ware kannst du neben die Tafel schieben und beim Einstellen im Blick behalten.

## Esc: einen Schritt zurück

**Esc** nimmt immer das Oberste weg, in dieser Reihenfolge: festgeheftete Tooltips, Hilfe, Lexikon, Rückfrage beim Bauen, Wegtafel, Baumodus, Fenster (das zuletzt geöffnete zuerst), Einstellungen, Spielmenü, Innenansicht, Bericht und Hafen, zuletzt die Tafel rechts. Ist nichts mehr offen, öffnet Esc das **Spielmenü** (Weiterspielen, Speichern, Laden, Einstellungen, Hilfe, Hauptmenü, Beenden). Der Jahresbericht schließt mit „Weiter“. Während einer Ratssitzung reagieren nur Esc, die Leertaste („Weiter“) und F1.

## Tastenübersicht

Die Buchstaben-Kürzel wirken nur im laufenden Spiel und nicht zusammen mit **Strg** oder **Alt**.

### Spielablauf

| Taste | Wirkung |
|---|---|
| **Leertaste** | Pause / weiter |
| **1** bis **4** | Tempo 1×, 2×, 4×, 8× |
| **Esc** | einen Schritt zurück, zuletzt das Spielmenü |
| **F1** | Hilfe (auch im Spielmenü) |
| **F11** | Vollbild ein/aus (wird gespeichert) |
| **F12** | Entwicklerfenster ein/aus – nur mit dem Haken „Entwickler-Optionen“ im Hauptmenü |

### Fenster öffnen

| Taste | Wirkung |
|---|---|
| **F** | Familienbuch |
| **H** | Hinterzimmer |
| **R** | Ratskammer (im Baumodus dreht R den Umriss) |
| **M** | Marktplatz |
| **L** | Lexikon |
| **B** | Bauen |
| **G** | Wege-Modus im Baumenü |
| **V** | Raster-Hilfslinien ein/aus (Baumodus und Kartenwerkstatt) |
| **X** | alle Fenster schließen |
| **T** | Tooltip festheften |

### Kamera

| Taste / Maus | Wirkung |
|---|---|
| **Mausrad** | Zoom |
| **rechte Maustaste ziehen**, **W A S D**, **Pfeiltasten** | Karte verschieben |
| **Q** / **E** | Kamera drehen (drinnen: Raum wechseln) |
| **Alt** halten | alle Namen zeigen |

### Maus

| Aktion | Wirkung |
|---|---|
| **Linksklick** auf ein Gebäude | seine Tafel öffnen |
| **Doppelklick** auf Werkstatt, Wohnhaus, Rathaus, Dom | hineingehen |
| **Linksklick** auf ein Porträt | Charakterfenster |
| **Mittlere Taste** | Tooltip festheften |
| **Rechtsklick** drinnen | das Gebäude verlassen |
| **Rechtsklick** beim Bauen | zurück zur Auswahl |
| **Titelrand ziehen** | Fenster verschieben |

## Entwicklerfenster

Wer am Spiel arbeitet oder etwas ausprobieren will, setzt im Hauptmenü unten rechts den Haken **Entwickler-Optionen**. Dann öffnet **F12** (oder der Knopf **Entwickler** in der Betriebsleiste) ein Fenster mit sechs Reitern: **Haus und Zeit** (Gulden, Aktionspunkte, Stand, Ansehen, Meinung, Vorspulen und Tempo bis 64×), **Familie und Ereignisse** (Geburt, Heirat, Krankheit, Tod, Charakterzüge, Diebstahl, Feuer), **Wirtschaft** (Waren und Zutaten im Lager), **Wetter und Darstellung** (Wetter, Jahreszeit, Blitz, Dunst, Unschärfe, Ego-Perspektive, Wegenetz, Bauzonen) **Messwerte** (Bildrate, Zeichenaufrufe, Speicher, Prüfsumme, Einblick in die KI-Häuser) und **Klänge** (jeder Klang des Spiels per Knopf zum Anhören, mit Lautstärke, Stumm und Pegel je Bus). Ohne den Haken gibt es weder das Fenster noch die Taste.

**Ego-Perspektive.** Auf dem Reiter „Wetter und Darstellung“ gibt es den Knopf **Ego-Perspektive**. Nach dem Klick schließt sich das Fenster, du wählst mit einem Klick auf die Karte einen Punkt, und dort stehst du selbst in der Stadt und läufst wie in einem Ego-Shooter herum: die **Maus schaut** (der Zeiger ist gefangen), **W/A/S/D** läuft (1,4 m/s), **Umschalt** rennt (4 m/s), die **Leertaste** springt, Augenhöhe 1,65 m. Du stehst auf dem Boden (Hang, Sockel unter den Häusern, Brückendeck), Stufen bis 50 cm steigst du hinauf; **Häuser, Gruben, Mauern, Türme und der Marktbrunnen versperren den Weg**, ebenso **Bäume, Büsche und große Steine, Zäune, Gatter, Holzstapel und Tröge der Gärten und Höfe, Marktstände, Bänke und das Marktkreuz sowie Karren mit ihren Ochsen und Pferden** (du gleitest an allem entlang und bleibst nicht hängen; die Karren fahren dich notfalls beiseite; Passanten und Streuner sind keine Hindernisse, du gehst durch sie hindurch), durch die Torburgen und über die Marktplätze gehst du hindurch, **Wasser hält dich am Ufer auf** (auf Brücken gehst du hinüber). Die Oberfläche ist aus, zu sehen ist nur der Hinweis „Esc: zurück“; die Spielzeit läuft weiter, die Zifferntasten 1–9 stellen das Tempo, alle anderen Tasten tun nichts. **Esc** beendet den Modus und stellt die Spielkamera genau wieder her (Lage, Drehung, Zoom). Es ist nur eine Ansicht: nichts wird gespeichert, es gibt kein Kennzeichen „mit Entwicklerhilfe“, und der Kern merkt nichts. In der Ego-Perspektive (und mit der freien Kamera) siehst du auch den **Horizont**: einen Himmel vom dunstigen Rand zum tieferen Blau mit ziehenden Wolken (bei Regen grau und schwer, nachts Sterne, um Sonne und Mond ein Hof), dahinter Land mit sanften Hügeln, Feldern und Wald, das im Dunst verschwimmt, und die Kulissenberge in Ebenen: vorn dunkel und bläulich, weiter hinten immer blasser, ganz hinten kaum vom Himmel zu trennen. Die Spielkamera blickt von oben und zeigt das alles nicht, dort bleibt alles, wie es war. Zum Ausprobieren ohne Klick: `--ego=37,110,25` (Ort, Blickrichtung).

**Blitz und Brand auf ein Gebäude deiner Wahl.** Auf dem Reiter „Wetter und Darstellung“ stehen bei „Gebäude“ die Knöpfe **Blitz auf Gebäude …** und **Brand hier …**. Nach dem Klick auf den Knopf zeigt ein Hinweis oben im Bild, was passiert, der Mauszeiger wird zum Fadenkreuz; der nächste Klick auf ein Gebäude in der Stadt löst es aus (Esc bricht ab, ein Klick ins Leere tut nichts, die Kamera bleibt stehen, das Fenster bleibt offen und zeigt das Ergebnis). Der Blitz trifft Rathaus, Kirchen, Werkstätten und Wohnhäuser samt Brand, Schaden und Löschkette wie im Gewitter. „Brand hier“ zündet ohne Blitz: bei einem Betrieb wie „Feuer im Betrieb“, bei einem bewohnten Wohnhaus wie „Hausbrand“; Rathaus, Kirchen und leere Häuser haben keinen Brandbefehl, dort meldet das Fenster das und verweist auf den Blitz.

> [!NOTE]
> Der erste Eingriff, der wirkt, kennzeichnet die Partie für immer: In der Ladeliste steht „[Entwicklerhilfe]“, und der Jahresbericht sagt es. Wetter und Darstellung zählen nicht dazu: Sie ändern nur das Bild.

## Einstellungen

Das Spielmenü (**Esc** oder Knopf **Menü**) und das Hauptmenü führen zu den **Einstellungen** mit vier Reitern – Grafik, Ton, Spiel und Barrierefreiheit –, deren Reglern ein Tooltip erklärt, was sie tun. Die Wahl wirkt sofort und wird gespeichert; alles Weitere steht im Kapitel [Einstellungen](einstellungen.md).

Das Spielfenster darfst du frei ziehen: Ein größeres Fenster zeigt mehr Stadt.

[Zurück zum Inhaltsverzeichnis](README.md)
