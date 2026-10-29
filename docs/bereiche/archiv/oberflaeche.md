# Bereich oberflaeche – Archiv der älteren Entscheidungen

Hier stehen die Entscheidungsabschnitte, die vor dem 25.10.2026 abgeschlossen wurden, **wörtlich** aus [../oberflaeche.md](../oberflaeche.md) verschoben (Stand 29.10.2026). Offene Punkte und die aktuellen Entscheidungen stehen in der Bereichsdatei selbst. Neue Einträge gehören dorthin, nicht hierher.

## Entscheidungen (älter)

### Werkstatt verkaufen (PR #97)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ort des Knopfs | Unten im Bedienfeld des eigenen Betriebs: „Verkaufen …“ bzw. „Pacht kündigen …“. Gesperrt, solange der Kern einen Grund nennt; der Grund steht grau daneben und im Tooltip. | 16.10.2026 |
| Rückfrage | Eigenes Fenster in der Bildmitte (wie Markt und Rat, Esc schließt): Erlös fürs Haus, Lagerware je Ware, Abrechnung bis heute, Summe, was mitgeht, Kasse der Geldleihe. Erst „Verkaufen“ schickt den Befehl. Die Zeit läuft weiter, die Zahlen werden zweimal je Sekunde aufgefrischt. | 16.10.2026 |
| Warnung Lagerware | Oranger Kasten oben in der Rückfrage, sobald Ware im Lager liegt, mit Stück und Erlös je Ware und dem Hinweis, dass der Markt meist mehr bringt. | 16.10.2026 |
| Nach dem Verkauf | War der Betrieb ausgewählt, zeigt die Auswahl das freie Haus mit Kaufangebot (Vorschlag Darstellung); Innenansicht verlassen; nichts zeigt mehr auf den Betrieb. | 16.10.2026 |
| Kauf freier Plätze | Prüfung über `LiegenschaftErwerbVerboten` des Kerns statt eigener Stand-/Geldprüfung; Knopf „Kaufen (Preis)“ bzw. „Es fehlen …“, Grund als ⚠-Zeile im Text und als Tooltip. Bebaute Plätze nennen Stufe und Meistergrad. | 16.10.2026 |
| Jahresbericht | Erlös als „Verkäufe +X“ unter „Außerdem“, nicht im Gewinn. | 16.10.2026 |

### Schriften beim Beenden freigeben (PR #100)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufräumen beim Beenden | Der Autoload `Fenster` lässt in `_ExitTree` die Schriften los (`Schriften.Freigeben`: Thema, Ersatzschrift, statische `FontVariation`s) und ruft `GC.Collect` und `GC.WaitForPendingFinalizers`. Absicherung gegen das sporadische Leck bzw. den Absturz beim Beenden im Rauchtest; Wirkung nicht nachgewiesen (Vergleich 8 : 8 Läufe, beide ohne Fund). | 16.10.2026 |
| Statische Godot-Objekte | Neue statische Felder mit Godot-Ressourcen (Schriften, Texturen, Materialien) beim Beenden ebenso freigeben – C# hält sie sonst bis zum Abbau der Laufzeit fest. | 16.10.2026 |

### Bauen und Leerstand (PR #103)

| Thema | Entscheidung | Datum |
|---|---|---|
| Einstieg | Knopf **Bauen** in der Zeile Familie · Hinterzimmer · Marktplatz des Kontors. Die **Bautafel** (`BauAnzeige`) steht rechts an der Stelle des Kontors, solange der Baumodus offen ist; Esc geht einen Schritt zurück: Rückfrage → Platzwahl → Liste → Baumodus aus. | 17.10.2026 |
| Auswahl | Liste der Wohnhaus-Stufen und der Werkstatt-Berufe (Stufe 1, nur Stadtbetriebe) mit Kosten, Maß und Bauzeit aus `Bauwesen.Vorschau`. Nicht erlaubte Gebäude sind **ausgegraut** mit dem Grund aus `BauherrVerboten` darunter und im Tooltip; geprüft wird nicht selbst. Legende der Bauzonenfarben oben. | 17.10.2026 |
| Platzwahl | Der Umriss folgt der Maus (Darstellung); die Tafel zeigt dazu den Stand: grün „Hier kann gebaut werden“ oder rot mit dem Grund aus `PlatzVerboten`, dazu Knöpfe „Drehen (R)“ und „Zurück“. R dreht, Linksklick auf grün setzt, auf rot passiert nichts, Rechtsklick ohne Ziehen geht zurück. Über Tafeln folgt der Umriss nicht. | 17.10.2026 |
| Rückfrage | `BauRueckfrage` in der Bildmitte (Muster `VerkaufAnzeige`): Preis, angerechnetes altes Haus, Kosten beim Baubeginn, Unterhalt (Werkstatt: ein Jahr bar zusätzlich), Bauzeit mit Fertigstellung („gegen 14:30 Uhr“ bzw. Datum). Der Knopf ist gesperrt, solange `Vorschau.Verboten` einen Grund nennt. Zahlen zweimal je Sekunde aufgefrischt, neu gebaut nur bei Änderung. Während sie offen ist, bleibt der Umriss stehen (`BauFest`). | 17.10.2026 |
| Leerstand | Auswahltafel eines leeren Wohnhauses: Kauf- und Abrisspreis als Text (aus `LeerstandVorschau`), darunter die Knöpfe **Kaufen und sanieren** und **Abreißen lassen**, je mit Rückfrage (gleiches Fenster, Rechnung Kauf, Sanierung bzw. Abriss, Anrechnung, Dauer). Gesperrt mit Grund als Tooltip und ⚠-Zeile. Nach dem Befehl zeigt die Auswahl die Baustelle, nach der Fertigstellung das Haus. | 17.10.2026 |
| Baustelle und Bauplatz | Auswahltafel mit dem, was entsteht, für wen und wann es fertig ist; ein Bauplatz nennt Fläche und wie man baut. Die Auswahl folgt einer fertigen Baustelle zum Haus bzw. zur Werkstatt und schließt sich, wenn der Bauplatz überbaut wird (`Stadtansicht.KennungNachBau`). | 17.10.2026 |
| Startoptionen | `--bauen`, `--bauort`, `--bauklick`, `--aufsteigen`, `--spulen`, `--leerstand`, `--speichern`, `--blick`, `--ui` für den Klicktest (Liste in `CLAUDE.md`). | 17.10.2026 |

### Abstimmen im Ratsfenster (PR #111)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wahlen | Neuer Abschnitt „Eure Stimme bei den Wahlen“ zwischen Laufbahn und Statut: je Amt mit Wahl (`Kandidaten(amt)` nicht leer) eine Auswahl, erster Eintrag „Vorgabe: <Name>“, dann alle Kandidaten (Ratsherr im Amt und bisheriger Inhaber mit Zusatz). Darunter eine graue Zeile „Stimmen heute“ mit den sicheren Stimmen jedes Hauses aus `WahlPrognose`, sie ändert sich mit der Stimme. Ohne Wahl in der Stadt entfällt der Abschnitt. | 17.10.2026 |
| Anträge | Unter dem Antrag zum Statut und zum Stadtglauben: „Eure Stimme:“ mit drei Schaltflächen Dafür / Dagegen / Enthaltung (die gewählte ist gedrückt, Vorgabe Enthaltung). Die Prognose im Antragstext aktualisiert sich sofort. | 17.10.2026 |
| Kein Stimmrecht | Der Grund aus `WahlStimmeVerboten`, `StatutStimmeVerboten` bzw. `Glaubenswesen.StimmeVerboten` steht in Grau statt der Bedienelemente (auch der Kerker, der Antragsteller, wer selbst Kandidat ist). Gilt derselbe Grund für alle Wahlen (z. B. kein Mitglied des Rates), steht er einmal mit der Liste der Ämter. Die Zeile wird nur ausgeblendet, wenn es keine Wahl bzw. keinen Antrag gibt. | 17.10.2026 |
| Befehle | Jede Auswahl schickt `WahlStimme`, `StatutStimme` oder `StadtglaubeStimme` über `Befehlen` (Meldung im Tafelfuß und im Protokoll), baut die Tafel neu; die Rollposition bleibt. Keine AP-Anzeige, die Befehle kosten nichts. | 17.10.2026 |
| Klicktest | Keine neue Startoption: Einen Stand mit dem Spieler im Rat schreibt man mit `--speichern` und bearbeitet die JSON (`Rat.Aemter.<Amt>.Konto` = 0, `Rat.Bewerbungen`, `Rat.AntragKonto`/`AntragStatut`, `Glaube.AntragKonto`/`AntragGlaube`), dann `--laden` mit `--rat=1`. | 17.10.2026 |

### Warenkorb im Tooltip des Marktfensters (PR #115)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ort | Eigene Zeile direkt unter „Nachfrage der Stadt“: „<Warengruppe>: Die Einwohner fragen 7 % mehr/weniger nach“. Nur für Waren im Warenkorb (`Warengruppe` nicht null), also Fertigwaren und Heilmittel. | 17.10.2026 |
| Zahl | Die Wirkung auf die Nachfrage, `kopplung.markt × (Warenkorbfaktor − 1)`, ganz gerundet. Rundet sie auf 0 %, steht „wie üblich“ da, damit die Gruppe immer zu sehen ist. | 17.10.2026 |

### Tafel Einstellungen mit Reitern, Größe der Oberfläche, Farbfilter (PR #169)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gliederung | Fünf Reiter oben in der Tafel (Knöpfe in einer Reihe, der gewählte in Gold): Voreinstellung (mit „Automatisch wählen“ und Vollbild), Leistung, Bild, Stimmung, Barrierefreiheit. Jeder Regler mit grauem Hinweistext wie bisher; die Tafel ist 560 Pixel breit und rollt, wenn ein Reiter nicht ins Fenster passt. Startoption `--reiter=<Name>` mit `--einstellungen=1`. | 21.10.2026 |
| Voreinstellung „Eigene“ | Steht in der Auswahl, lässt sich aber nicht wählen: Sie erscheint von selbst, sobald ein Regler von allen Stufen abweicht, und verschwindet, wenn er zurückgestellt wird. | 21.10.2026 |
| Größe der Oberfläche | Regler 75–200 % in Schritten zu 5 %, wirkt über `ContentScaleFactor` des Hauptfensters auf alle Tafeln, Schriften und Knöpfe; beim Ziehen erst beim Loslassen angewendet (sonst wüchse die Tafel unter der Maus). | 21.10.2026 |
| Farbfilter | Für das ganze Bild mit Oberfläche (Wappen, Besitzerfarben, Warenzeichen tragen Farbe), nicht nur für die 3D-Ansicht: Protanopie, Deuteranopie, Tritanopie als Daltonisierung. | 21.10.2026 |
| Fahnen | „Fahnen bewegen“ als Schalter im Reiter Stimmung (aus: `wind_staerke` 0). | 21.10.2026 |

### Kartenwerkstatt (PR #168)

| Thema | Entscheidung | Datum |
|---|---|---|
| Tafeln | Feste Seitentafeln über die ganze Höhe (links Katalog, rechts Eigenschaften und Fehlerliste, jede mit eigenem Rollbereich), oben eine Leiste über die ganze Breite, unten Meldung und Tastenhilfe. Stil wie Bau- und Auswahltafel; Rückfragen wie das Spielmenü (goldener Rand) auf einem dunklen Schleier, der Klicks auf die Stadt abfängt; Esc schließt sie. | 21.10.2026 |
| Textfelder | Name und Betrieb werden beim Verlassen des Feldes übernommen (Enter oder ein Klick daneben, auch in die Stadt); solange darin geschrieben wird, verschieben WASD die Kamera nicht (gilt jetzt in jeder Stadtansicht, auch mit gedrückter Strg-Taste). | 21.10.2026 |

### Schwierigkeit beim Partiestart (PR #177)

| Thema | Entscheidung | Datum |
|---|---|---|
| Auswahl im Menü | Zeile „Schwierigkeit“ (Normal, Schwer) unter „Rivalen“ im Dialog „Neue Partie“, Vorgabe Normal; darunter wie beim Volk eine graue Hinweiszeile, die sich mit der Wahl ändert. Die Stufe geht in `Partieeinstellungen.Schwierigkeit`. | 22.10.2026 |
| Startoption | `--schwierigkeit=Normal\|Schwer` überschreibt die Stufe nur für diesen Start (auch ohne Menü, dann sonst Normal). | 22.10.2026 |
| Anzeige im Spiel | Die Hinweiszeile oben links nennt die Stufe hinter der Siegbedingung (bei geladenen Ständen die gespeicherte). Eine eigene Einstellungsliste gibt es im Spiel sonst nicht. | 22.10.2026 |
| Freie Umlandbetriebe | Bei Normal erscheinen die Umlandbetriebe der KI-Häuser ohne Änderung der Darstellung als „zu verpachten“ (Schild, Beschriftung, Auswahltafel mit Pacht-Knopf); geprüft an Grube und Holzschlag im Klicktest. | 22.10.2026 |

### Kartenwerkstatt: Größe, Rand und Felder (PR #178)

| Thema | Entscheidung | Datum |
|---|---|---|
| Reiter „Karte“ | Dritter Reiter rechts neben „Eigenschaften“ und „Start“: Breite (400 bis 2.000 m), Tiefe (300 bis 2.000 m), Randstreifen (100 bis 600 m), jeweils in 50-m-Schritten (Zahlfeld, ein Befehl je Änderung; das Feld zeigt danach, was der Kern daraus gemacht hat), „Rand vorschlagen“, „Kamera-Start übernehmen“, ein Schalter für die Kartenrandlinien. Ein Prüfbefund zu Größe, Rand oder Kamera-Start öffnet den Reiter | 22.10.2026 |
| Rand vorschlagen | Jedes Mal ein anderer Vorschlag (Zufallswert), nur die erste Karte hat den festen Vorschlag der Kennung. Liegen Flächen ganz im Rand, die er ersetzt, fragt die Werkstatt vorher („Randlandschaft ersetzen?“ mit der Zahl); Rückgängig holt alles zurück | 22.10.2026 |
| Kamera-Start | Der aktuelle Blick (Punkt, Zoom, Drehung relativ zur gewohnten Blickrichtung). Liegt der Blickpunkt im Rand (die Werkstatt darf bis zur Außenkante), rückt er an den Rand des Spielfelds und die Meldung sagt es | 22.10.2026 |
| Neue Karte | 600 × 450 m (Zahlfeld 400 bis 2.000, Schritt 50) mit dem Rand-Vorschlag als Ausgangsstand: Er gilt nicht als Änderung (kein „ungespeichert“), Rückgängig nimmt ihn weg | 22.10.2026 |
| Feldfläche | Werkzeug im Katalog unter „Flächen“; Tafel: Name, Richtung (0° = Nord–Süd), Streifenbreite, Länge der Stücke, je Frucht ein Regler für den Anteil (0 nimmt sie heraus, mindestens eine bleibt), Startwert mit „Neu würfeln“, Zahl der Acker-Stücke, Aussparungen. „Aussparung“ gilt jetzt für Wald-, Streu- und Feldflächen | 22.10.2026 |
| Startoptionen | `--werkstatt-groesse=<b>,<t>[,<rand>]`, `--werkstatt-randvorschlag=1` (oder eine Zahl als Startwert), `--werkstatt-feld=x,z;…` (Flächen mit `\|`), `--werkstatt-flaeche=feld,…`, `--werkstatt-kamerastart=1`, `--werkstatt-tafel=karte`, `--werkstatt-auswahl=feld:<Name>`, `--werkstatt-werkzeug=feldflaeche` (Liste in `CLAUDE.md`) | 22.10.2026 |

### Kartenwerkstatt: Gelände (PR #179)

| Thema | Entscheidung | Datum |
|---|---|---|
| Katalog | Neue Gruppe „Gelände“ (nach „Mauer“, vor den Gebäuden): Heben, Senken, Glätten, Ebnen, Rauschen und Rampe; Schlüssel `gelaende:<pinsel>` und `rampe` (Startoption `--werkstatt-werkzeug=…`). Die Wahl der Art stellt die Stärke auf ihre Vorgabe, wenn sich die Einheit ändert (Meter ↔ Prozent). | 23.10.2026 |
| Tafel rechts | Wie beim Pinsel der Natur unter „Eigenschaften“: Radius (1 bis 100 m), Stärke (Heben, Senken: 0,05 bis 2 m je Tupfer; Rauschen bis 4 m; Glätten, Ebnen: 5 bis 100 %), Kante (0 % hart bis 100 % weich), beim Rauschen Startwert und Maßstab (4 bis 100 m), bei der Rampe Breite (1 bis 40 m) und Kante (0 bis 20 m). Die Werte gelten für den nächsten Strich; ein kurzer Text erklärt die Wirkung. | 23.10.2026 |
| Leiste unten | Neue Zeile über der Tastenhilfe: „Höhe 12,4 m · Gefälle 6 %“ unter der Maus (leer über Tafeln; die Zeile behält ihre Höhe, damit die Leiste nicht springt). Die Tastenhilfe nennt für beide Werkzeuge Strich, Esc, Strg+Z und die Hangsperre (gelb ab 8 %, rot ab 15 %). | 23.10.2026 |
| Strich | Mit gedrückter Maus (jede Mausbewegung ein Tupfer-Schritt des Kerns, das Gelände ändert sich sofort), Loslassen = ein Rückgängig-Schritt, Esc beendet und nimmt zurück (kein Schritt bleibt), Rechtsklick beendet das Werkzeug nicht während des Malens. Rampe: zwei Klicks, Esc nimmt den ersten zurück. | 23.10.2026 |
| Startoptionen | `--werkstatt-gelaende=<pinsel>,<radius>,<staerke>,x,z;…` (Striche mit `\|`; Pinsel mit `:kante[:startwert[:massstab]]`), `--werkstatt-rampe=x1,z1;x2,z2,breite`, `--werkstatt-werkzeug=gelaende:<pinsel>` oder `rampe`. | 23.10.2026 |

### Kartenwerkstatt, Schritt 5c: Wasser (PR #180)

| Thema | Entscheidung | Datum |
|---|---|---|
| Katalog | Neue Gruppe „Wasser“ (nach „Mauer“, vor „Gelände“): Fluss · 12 m, Bach · 3 m, See, Teich, Fährstelle und – wenn der Katalog den Hafenplatz kennt – Hafen. Schlüssel `wasser:fluss`, `wasser:bach`, `wasser:see`, `wasser:teich`, `wasser:faehre`, `wasser:hafen` (Startoption `--werkstatt-werkzeug=…`). Fluss und Bach teilen das Werkzeug (nur die Art und die Vorgabebreite unterscheiden sie), See und Teich ebenso. | 23.10.2026 |
| Tafel Werkzeug | Beim Fluss die Breite des neuen Flusses (Vorgabe nach Art), sonst nur ein Satz, wie es geht. Die Tastenhilfe unten nennt für alle drei Werkzeuge die Tasten (Fluss: Punkte in Fließrichtung, Doppelklick oder Enter, Rücktaste, Esc). | 23.10.2026 |
| Tafel Fluss | Name, Art (Fluss, Bach), Breite und Tiefe des ganzen Laufs (Regler), Strömung (0 bis 5 m/s), Länge aus dem Kern, „Richtung umkehren“; darunter „Stützpunkt“ mit Nummernfeld (0 = keiner): Für den gewählten Punkt (Griff in der Stadt oder Nummer hier) Breite und Tiefe dort und „Wie der Fluss“, das die eigenen Werte wegnimmt. Ein Klick auf einen Griff in der Stadt baut die Tafel neu (`WerkstattGriffGeaendert`). | 23.10.2026 |
| Tafel See | Name, Art (See, Teich), Wasserstand (Haken „automatisch“: 0,4 m unter dem tiefsten Ufer, folgt dem Gelände; sonst ein festes Zahlfeld, das beim Ausschalten den heutigen Wert nimmt), Tiefe (Regler), Fläche. | 23.10.2026 |
| Tafel Fähre | Name, Länge, welches Gewässer sie quert (aus dem Wasserplan; ohne Gewässer der Hinweis, die Anleger an gegenüberliegende Ufer zu setzen), wie lange Karren warten (`logistik.wasser.faehreMinuten`). | 23.10.2026 |
| Kopfzeile | „Titel · Kennung · … Gewässer, … Fähren“ zählt Gewässer und Fährstellen mit. | 23.10.2026 |
| Startoptionen | `--werkstatt-fluss=<fluss\|bach>,<breite>,x,z;…` (Breite 0 = nach der Art, Flüsse mit `\|`), `--werkstatt-see=[teich:]x,z;…[,<wasserstand>]`, `--werkstatt-faehre=x1,z1;x2,z2` (nach den Wegen), `--werkstatt-auswahl=fluss:<Name>\|see:<Name>\|faehre:<Name>`, `--werkstatt-werkzeug=wasser:<fluss\|bach\|see\|teich\|faehre\|hafen>`. | 23.10.2026 |

### Kartenwerkstatt, Schritt 5d: Wegbelag (PR #182)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wegtafel | Ein Klick auf einen Weg öffnet rechts die Wegtafel (`WegAnzeige.cs`, wie die Bautafel; schließt Kontor und Baumodus nicht, Esc schließt sie): Belag je Abschnitt (Stufe, Zustand bzw. Austritt, Verkehr, Karrentempo, „wird gebaut“), laufende Baustellen, zwei Regler für den Abschnitt (5-m-Stücke; goldenes Band im Bild), Knöpfe „Kies“ und „Pflaster bauen lassen“ mit Kosten, Bauzeit und Länge aus `Wegebau.Vorschau` (gesperrt mit Grund darunter), „Pflaster beim Rat beantragen“ mit `AntragPrognose` und AP-Kosten (nur Ratsmitglieder; liegt schon ein Antrag vor, steht er da). Der untere Teil baut sich alle 0,5 s neu auf, wenn sich Antrag, Vermögen, Baustellen oder AP ändern. Meldungen gehen ins Protokoll. | 24.10.2026 |
| Ratstafel | Neuer Abschnitt „Wegbau der Stadt“ vor dem Stadtglauben: Wegekasse und der Antrag dieses Jahres mit Prognose; gestellt wird er an der Wegtafel. | 24.10.2026 |
| Kartenwerkstatt | Wegtafel (Eigenschaften) mit Block „Belag“: Abschnitte als Liste, Stufe, Regler von und bis (Schieber der Werkstatt, übernimmt beim Loslassen), „Abschnitt setzen“, „Ganzer Weg“, „Vorgabe“; je ein Schritt der Geschichte (Strg+Z). | 24.10.2026 |
| Startoptionen | `--auswahl=weg:<nr>[,<stelle>]` (Wegtafel, Kamera auf den Weg, Ort im Protokoll), `--wegausbau=<weg>,<von>,<bis>,<Kies\|Pflaster>[\|…]`, `--wegantrag=<weg>,<von>,<bis>`, `--werkstatt-belag=<weg>,<stufe>[,<von>,<bis>]` (in `CLAUDE.md`). | 24.10.2026 |

### Kartenwerkstatt, Schritt 5e: Bodentexturen (PR #183)

| Thema | Entscheidung | Datum |
|---|---|---|
| Katalog | Neue Gruppe „Boden“ nach dem Gelände: die elf Texturen mit Farbfeld (Farbe aus dem Katalog, 16 × 16 mit dunklem Rand als Symbol des Knopfs) und Name, dazu „Boden-Radierer“; der Tooltip nennt, was die Textur streut. Schlüssel `boden:<kennung>` und `boden:radierer` (`--werkstatt-werkzeug`). | 24.10.2026 |
| Tafel | `BodenTafel`: Farbfeld und Name, **Radius** (1–100 m), **Stärke** (5–100 %), **Kante**, Schalter **Deko streuen**, **Dichte** (0–2 ×) und was die Textur streut; der Radierer hat nur Radius und Kante. Die Regler sind die der Werkstatt (übernehmen beim Loslassen). | 24.10.2026 |
| Leiste | Unten steht „Höhe … · Gefälle … · Boden: <Textur>“, sobald irgendwo gemalt ist oder das Werkzeug gewählt ist. Hilfetext zum Werkzeug wie beim Gelände. | 24.10.2026 |
| Startoptionen | `--werkstatt-boden=<textur\|radierer>,<radius>,<staerke>,x1,z1;…[,kante=][,deko=aus][,dichte=]` (ein Strich je Eintrag, mehrere mit `\|`), `--werkstatt-werkzeug=boden:<textur>` (in `CLAUDE.md`). | 24.10.2026 |

### Kartenwerkstatt, Schritt 5f: Editor-Oberfläche (PR #184)

| Thema | Umsetzung | Datum |
|---|---|---|
| Aufbau | Kopfleiste (Datei, Rückgängig, Gassennetz, Ausprobieren), Gruppenleiste links mit zehn Icon-Knöpfen (Auswählen, Gelände, Boden, Wasser, Wege, Flächen und Natur, Gebäude, Mauer und Tore, Start, Prüfen), Palette daneben, rechts nur die Eigenschaften, unten die Statusleiste (`Kartenwerkstatt.Palette.cs`). Alle Startoptionen und Knöpfe der Tafeln wählen weiter über denselben Schlüssel. | 24.10.2026 |
| Tasten | 1–0 wählen die Gruppen (nur ohne Strg, Alt, Umschalt; kein Konflikt mit Q/E, WASD, Entf, Esc, Strg+Z/Y/S). Die Taste steht klein am Knopf, im Tooltip und in der Palettenüberschrift; die Statusleiste nennt das Werkzeug als „Gruppe › Werkzeug“. | 24.10.2026 |
| Icons | 45 Icons im Holzschnitt-Stil (Umriss, Schraffur unten rechts, Farbe leicht versetzt) aus `tools/icons/kartenwerkstatt_icons.py`; Boden-Texturen als Farbfeld mit Schraffur. Gebäude zeigen ein zur Laufzeit gerendertes Vorschaubild ihres Modells (statischer Zwischenspeicher). | 24.10.2026 |
| Eigenschaften | Tafeln mit mindestens zwei Zwischenüberschriften klappen sie zu Abschnitten (Knopf mit „–“ und „+“); die Prüfliste ist ein eigener Gruppenknopf mit Zähler (F = Fehler, H = Hinweise). | 24.10.2026 |
| Startoption | `--werkstatt-gruppe=<auswaehlen\|gelaende\|boden\|wasser\|wege\|flaechen\|gebaeude\|mauer\|start\|pruefen>` klappt eine Gruppe auf (für Klicktests). | 24.10.2026 |
