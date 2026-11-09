# Bereich oberflaeche – Archiv der älteren Entscheidungen

Hier stehen die Entscheidungsabschnitte, die vor dem 27.10.2026 abgeschlossen wurden, **wörtlich** aus [../oberflaeche.md](../oberflaeche.md) verschoben (Stand 29.10.2026). Offene Punkte und die aktuellen Entscheidungen stehen in der Bereichsdatei selbst. Neue Einträge gehören dorthin, nicht hierher.

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

### Grundstruktur der Oberfläche (PR #197)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Beim Anspielen wirkte die Oberfläche überladen. Jetzt nur die Grundstruktur (Aufbau, Wege, Inhalte); die schöne Gestaltung der Oberfläche kommt ganz zum Schluss. Wunsch des Nutzers. | 25.10.2026 |
| Aufbau | Oben links eine Zeile: Wappen, Geld, Stand, Aktionspunkte. Oben mittig der Zeitstrahl, darunter Pause und Tempo. Unten die Betriebsleiste, rechts davon Rat, Bericht, Menü. Unten links die Meldungen. Rechts eine Tafel nur, wenn man etwas anklickt. Der große Kontor-Kasten und der Kasten oben links entfallen. Entschieden vom Nutzer. | 25.10.2026 |
| Zeitstrahl | Band 5–22 Uhr in vier Tagesphasen (Zunftglocke, Hoher Mittag, Vesper, Nacht) mit Sonne bzw. Mond als Zeiger; darüber Jahreszeit, Jahr, Uhrzeit und Wetter mit Aussicht (WetterAnzeige aus PR #195); darunter Pause und die Tempoknöpfe. Entschieden vom Nutzer. | 25.10.2026 |
| Betriebsleiste | Je eigener Betrieb ein Knopf mit Symbol und Zustand (läuft, steht, Lager voll, Karren unterwegs; Warnzeichen, wenn er steht); Klick fährt die Kamera hin und öffnet die Kurzübersicht, Doppelklick geht hinein; daneben „+ Bauen“; viele Betriebe: Leiste scrollt. Entschieden vom Nutzer. | 25.10.2026 |
| Kurzübersicht Betrieb | Rechts eine schmale Tafel: Name, Zustand mit Warnzeichen und Knopf „Beheben“ (öffnet innen gleich die passende Tafel: Lager, Personal, Karren …), Ware (draußen noch wechselbar), Arbeiter, Lager und Karren, Gewinn dieses Jahr, „Hineingehen“ und „…“ (Verkaufen/Pacht). Alle übrigen Einstellungen (Produktion, Personal, Lager, Karren, Einrichtung) nur noch in der Innenansicht. Doppelklick auf ein Gebäude geht direkt hinein. Entschieden vom Nutzer. | 25.10.2026 |
| Kontor aufgelöst | Klick auf das eigene Wohnhaus: eine Tafel mit Reitern Haus (Stand, Aktionspunkte, Ausbau, Hauswächter) · Familie · Handwerk (Meistergrade, Prüfungen, Lernen) · Geld (Kredit, Bank) · Hinterzimmer; Doppelklick geht in die Innenansicht. Rathaus: Rat, Ämter, Statut, Gericht. Marktplatz: Klick auf den Markt (kein eigener Knopf). Knopf „Bericht“: Jahresrechnung, Rivalen, Umsätze. Tastenkürzel springen direkt hin (z. B. F Familie, H Hinterzimmer, R Rat, M Markt, B Bauen). Entschieden vom Nutzer. | 25.10.2026 |
| Hilfe und Meldungen | Die Steuerungshilfe wandert in ein Hilfefenster (F1 und Eintrag im Menü), beim ersten Start einmal kurz eingeblendet. Meldungen unten links: die letzten vier, verblassen nach 20 s, Warnungen bleiben bis zum Klick; Klick öffnet das Protokoll des Jahres. Entschieden vom Nutzer. | 25.10.2026 |
| Umsetzung: Kopf und Zeitstrahl | Eine Reihe über die ganze Breite (`Hauptszene.Kopf.cs`): Kopfzeile links (Klick öffnet das Wohnhaus, Tooltips: Bargeld/Reinvermögen, Weg zum nächsten Stand, was AP kosten), Zeitstrahl in der Mitte (`Zeitstrahl.cs`, gezeichnet: vier Phasen, Stundenstriche, goldene Linie der Arbeitszeit 8–20 Uhr, Sonne bzw. ab 20 Uhr Mond). In schmalen Fenstern rückt der Zeitstrahl aus der Mitte, statt die Kopfzeile zu überdecken. Darunter klein die Blickrichtung bzw. der Raum (früher im Kasten oben links). | 25.10.2026 |
| Umsetzung: Betriebsleiste | Knopf je Betrieb mit Waren-Icon (Werkstatt: Hauptware, Umland: erster Rohstoff), Name und Zustand (läuft, steht, Lager voll, Karren unterwegs, Umbau, Reparatur, kein Personal; Warnzeichen und rot, wenn er steht); der gewählte Betrieb ist gedrückt. „+ Bauen“, dann Rat (gesperrt ohne Stadtrat), Bericht, Menü. Drinnen ist die Leiste aus (unten rechts steht die Raumleiste). | 25.10.2026 |
| Umsetzung: Kurzübersicht | `BetriebsUebersicht` in der Auswahltafel rechts (340 breit). „Beheben“: kein Personal → Personal, Lager voll (eine bearbeitete Ware hat die Lagergrenze erreicht) → Lager, sonst Stillstand (Zutat fehlt) → Karrenhof (dort „Handel am Markt“); Umbau und Reparatur ohne Knopf. Gelesen wird nur aus dem Kern, keine neue Abfrage. „…“ klappt das Bedienfeld in Kurzform auf: Ausbau, Gaststube, Kirche, Wache, Diebe, Verkaufen/Pacht – das gibt es drinnen (noch) nicht. Betriebe ohne Innenraum (Umland) zeigen unter „…“ auch Personal, Förderung und Karren mit Eskorte und Nachtfahrt; „Beheben“ klappt dort „…“ auf. Eskorte und Nachtfahrt gibt es nur für Umlandkarren, drinnen in der Stadt war nie eine Wahl. | 25.10.2026 |
| Umsetzung: Wohnhaus, Rathaus, Bericht | `WohnhausAnzeige` (früher `KontorAnzeige`) rechts mit Reitern Haus · Familie · Handwerk · Geld · Hinterzimmer; Familie und Hinterzimmer haben je einen Knopf zum großen Fenster in der Mitte. Im Kontor-Raum des Wohnhauses steht sie wie früher das Kontor. Rathaus: die bisherige Auswahltafel, Ämter jetzt je Zeile. `BerichtAnzeige` rechts mit Reitern Jahresrechnung (bisher dieses Jahr, Vorjahr, Knopf zum ganzen Jahresbericht – die Pause bleibt danach, wie sie war), Rivalen, Umsätze (je Betrieb, dieses Jahr bisher und Vorjahr), Protokoll. Rechts steht immer nur eine Tafel; die Fenster der Mitte liegen darüber. | 25.10.2026 |
| Umsetzung: Tastenkürzel | F Familienbuch, H Hinterzimmer, R Ratskammer, M Markt, B Bauen (je noch einmal: schließen), F1 Hilfe. Keine Kollision mit WASD, Q/E, Leertaste, 1–9, Alt, F11; R dreht im Baumodus weiter den Umriss (dort öffnet R den Rat nicht). Nicht mit Strg oder Alt, nicht bei offenem Spielmenü oder Jahresbericht. | 25.10.2026 |
| Umsetzung: Meldungen und Hilfe | Warnung = Meldung, die mit „✗“ (abgelehnter Befehl) oder „⚠“ beginnt. Verblassen in Echtzeit (auch in der Pause), 2 s Ausblenden. Sind schon vier da, geht die älteste gewöhnliche, nur bei vier Warnungen die älteste Warnung. Das Protokoll des Jahres beginnt mit der Jahresabrechnung neu (höchstens 1000 Zeilen). Hinter einer großen Tafel drinnen sind die Meldungen aus. Hilfe: Tasten, kurze Wegbeschreibung, oben Spieljahr, Siegbedingung, Schwierigkeit (früher im Kasten oben links); von selbst nur ohne Startoptionen und nur einmal. | 25.10.2026 |
| Umsetzung: Startoptionen | Neu `--wohnhaus=<reiter>`, `--bericht=<reiter>` (`--bericht=1` bleibt der Jahresbericht), `--verwalten=mehr`, `--hilfe=1`, `--meldung=<Text>`; `--verwalten=1` öffnet die Kurzübersicht. Der Hinweis der Probepartie steht unter dem Zeitstrahl. | 25.10.2026 |

### Meldungen nur für eigene Betriebe (PR #198)

| Thema | Entscheidung | Datum |
|---|---|---|
| Fremde Stillstände | Stillstandsmeldungen fremder Betriebe („Tischlerei Eichwald: Es fehlt Holz – die Arbeit ruht.“) stehen nur noch im Protokoll des Jahres, nicht mehr unten links; eigene Betriebe melden sich weiter. Dazu ohne doppelte Uhrzeit beim Beginn einer Tagesphase. Wunsch des Nutzers. | 25.10.2026 |

### Hafen und Schiffshandel (PR #199)

| Thema | Entscheidung | Datum |
|---|---|---|
| Hafentafel (Umsetzung) | `HafenAnzeige` rechts wie Bericht und Wohnhaus (420 breit), geöffnet mit Klick auf Hafen, Kran, Lagerschuppen oder ein eigenes Schiff. Reiter **Schiffe**: oben „Schiff kaufen“ (gesperrt mit dem Grund aus `KaufVerboten`, Tooltip: Fassung, Unterhalt, Fahrzeiten), je eigenes Schiff ein Knopf mit Zustand („zurück gegen 14:02“ bzw. „im nächsten Jahr“), Ladung, Geleit, Fahrten, Ertrag dieses Jahr und Vorjahr. Das gewählte Schiff klappt auf (beim Öffnen das erste am Kai, nach einem Kauf das neue): am Kai Ziel (je Stadt der Gewinn ihres Vorschlags, Richtung und Dauer im Tooltip), Quelle (Markt oder Lager eines eigenen Betriebs), darunter gleich der erwartete Gewinn der Fahrt (`Kalkulation.Schiffsfahrt`, Tooltip: Einkauf, Erlös dort, Einkauf dort, Erlös daheim nach Steuer, Zoll, Geleit, Risiko, Dauer), „Vorschlag“ und „Auftrag erteilen“ (gesperrt mit dem Grund aus `AuftragVerboten`), dann Geleit (je Stufe Preis + erwarteter Verlust für diese Fracht, wie bei den Karren), Wiederholen, Laderäume und die Fracht je Ware mit Preis dort und daheim (Lager bei Quelle Betrieb, Druck im Tooltip), zuletzt „Verkaufen“ (nur am Kai). Unterwegs: der laufende Auftrag, Geleit für die nächste Strecke, „Nicht wiederholen“. Andere Häuser am selben Hafen in einer Zeile darunter. Reiter **Ferne Städte**: je Stadt Richtung, Fahrzeit hin/zurück, Zollstellen und je Ware Preis dort, daheim und Druck. Aufbau nur bei Änderung (Schiff gekauft, abgelegt, Ziel oder Quelle), die Zahlen zweimal je Sekunde – eine Eingabe in den Mengen bleibt stehen. | 25.10.2026 |
| Fremde Schiffe | Klick auf ein Schiff eines anderen Hauses: die Auswahl rechts mit Wappen, Besitzer, Zustand, Ladung und Geleit. | 25.10.2026 |
| Betriebsleiste | Ein Knopf „Hafen“ vor „+ Bauen“ (nicht einer je Schiff: Schiffe sind wenige, alles zu ihnen steht in der Hafentafel), nur in Städten mit Schiffshandel; Zustand „kein Schiff“, „2 am Kai“ bzw. „1 von 2 unterwegs“, Tooltip je Schiff; Klick fährt hin und öffnet die Tafel, noch ein Klick schließt sie. Esc schließt die Hafentafel. | 25.10.2026 |
| Meldungen | Wie bei den Betrieben (PR #198): Meldungen zu Schiffen anderer Häuser (Kauf, Ankunft, Heimkehr, Piraten, Züge der KI „Haus …: schickt Möwe nach …“) nur im Protokoll; der Kauf eines eigenen Schiffs nur einmal (die Antwort des Befehls nennt Preis und Unterhalt). Überfälle der Flusspiraten auf eigene Schiffe sind Warnungen (⚠, bleiben bis zum Klick). | 25.10.2026 |
| Kern-Ergänzung | `Kalkulation.BesteSchiffsfahrt(…, ziel)`: optional nur für eine ferne Stadt (Vorschlag für das gewählte Ziel, Gewinn je Stadt im Ziel-Menü), Test `SchiffsvorschlagTests`. | 25.10.2026 |
| Startoptionen | `--schiff=<Anzahl>`, `--schiffsauftrag=vorschlag` bzw. `=<Ziel>`, `--hafen=1` bzw. `=<Kennung>` oder `=staedte`; ein fremdes Schiff mit `--auswahl=schiff:<Name>` (`docs/STARTOPTIONEN.md`). | 25.10.2026 |
| Klicktest | Vulkan auf der T400: Probestadt und Greybox – Tafel nach Kauf, Tooltip der Gewinnrechnung, Klick auf Hafen, Kran und Schiff, Reiter „Ferne Städte“, Tafel unterwegs, Meldungen bei Ablegen, Ankunft in Eisenfurt und Heimkehr, Knopf „Hafen“ in der Leiste, fremdes Schiff nach 5 Jahren. Keine SCRIPT ERROR. | 25.10.2026 |

### Stadtkasse, Ämter und neue Anträge (PR #200)

| Thema | Entscheidung | Datum |
|---|---|---|
| Rathaus-Tafel (Umsetzung) | `RathausTafel` unter dem Text der Auswahl beim Rathaus (wie die Kurzübersicht beim Betrieb), nur mit Stadtkasse, drei Reiter: **Stadtkasse** (Stand, Steuersatz, Einnahmen, Ausgaben, Saldo dieses Jahr bisher und Vorjahr; die Posten einzeln nur für den Kämmerer, sonst der Grund aus `HaushaltVerboten` und die Summen), **Rat** (Tagesordnung mit Wappen der Antragsteller, Knopf zur Ratskammer, Bauten der Stadt im Bau mit Restzeit und fertig, Fest, Viertel der Streife), **Amt** (die Befugnis des eigenen Amtes; ohne Amt nur eine Zeile, was jedes Amt darf). Jeder Reiter wird nur neu aufgebaut, wenn sich sein Inhalt ändert (zweimal je Sekunde geprüft), damit Klicks nicht verloren gehen. Mit Stadtkasse fällt die Zeile „Antrag liegt vor“ im Rathaustext weg (steht in der Tagesordnung). | 25.10.2026 |
| Ratskammer (Umsetzung) | `RatAnzeige.Antraege.cs`: nach den Wahlen die **Tagesordnung** (alle Punkte in Abstimmungsreihenfolge, mit Antragsteller), nach Statut, Wegbau und Glaube der Abschnitt **Anträge zu Steuer, Bau und Fest**: Stand der Kasse und Steuersatz, Hinweis auf den freien Antrag des Bürgermeisters bzw. den AP-freien Steuerantrag des Kämmerers, je Art der vorliegende Antrag mit Prognose und den Knöpfen Dafür/Dagegen/Enthaltung (`RatsantragStimme`) oder – für Mitglieder des Rates – die Auswahl: Steuersatz 3–8 % (je Satz „heute 5:2, angenommen“), Bauvorhaben aus der Liste mit Kosten und Prognose (Gebautes und Baustellen mit Vermerk, Hafen nur mit Schiffshandel; Wirkung, Bauzeit und Ansehen darunter), Fest mit Kosten und Prognose. „Antrag stellen“ ist mit dem Grund aus `BeantragenVerboten` gesperrt und trägt die AP-Kosten bzw. „ohne AP“. | 25.10.2026 |
| Befugnisse (Umsetzung) | Stadtrichter: im Gericht je laufendem Verfahren mit Vorsitz eine Auswahl der Buße in 5-%-Schritten (±25 %, „gewöhnlich“) und die drohende Buße (`DrohendeBusse`); andere sehen, ob der Richter sie geändert hat. Stadthauptmann: Streife im Reiter „Amt“ (gleichmäßig oder ein Viertel, je Viertel die Zahl eigener Betriebe). Marktmeister: im Markt eine Zeile mit den Waren, deren Preis sich in der nächsten Stunde am meisten ändert, und der Preis der nächsten Stunde im Tooltip jeder Ware. Gefängniswärter: im Reiter „Amt“ die Gefangenen mit Haftende, Kaution und „Freilassen“ (gesperrt mit Grund). Zöllner: Hinweis in der Hafentafel, Zollstellen und Rechnung zählen die erste nicht mit (`Schiffahrt.Zollstellen`). Bürgermeister und Kämmerer: Hinweise im Reiter „Amt“ und in der Ratskammer. Ohne Amt keine Befugnis zu sehen. | 25.10.2026 |
| Meldungen | Wie PR #198: Beschlüsse der Ratssitzung, fertige Bauten und das Fest stehen unten links. Nur ins Protokoll: Anträge und Amtshandlungen der KI-Häuser („…: beantragt ein Fest“, „setzt als Stadtrichter“, „schickt als Stadthauptmann“, „lässt als Gefängniswärter“), die Freilassung durch einen fremden Wärter und abgelehnte fremde Anträge zu Steuer, Bau und Fest – außer, die Meldung nennt das eigene Haus (`Hauptszene.Rat.cs`). | 25.10.2026 |
| Startoptionen | `--amt=<Amt>` (der Spieler hält das Amt wie nach einer Wahl; der Kern hat dafür keinen Befehl, die Startoption setzt den Inhaber über Reflection), `--kasse=<Gulden>`, `--antrag=steuer:<N>`, `bau:<Kennung>` bzw. `fest` (mehrere mit Komma), `--rathaus=<reiter>` (stadtkasse, rat, amt); `--auswahl=stadtbau:<Kennung>` wählt eine Baustelle der Stadt. Sie laufen vor den Hafen-Startoptionen, damit Tafeln, die ein Amt zeigen, es schon kennen. | 25.10.2026 |
| Klicktest | Vulkan auf der T400, neu importiert: Rathaus mit Kasse als Kämmerer (Posten) und ohne Amt (Summen), Tagesordnung als Bürgermeister (freier Antrag, dann 2 AP), Ratskammer mit Anträgen und als Gemeiner ohne Amt, Reiter „Amt“ als Hauptmann und Wärter, Gericht als Richter (ohne Verfahren), Markt als Marktmeister, Hafentafel „Ferne Städte“ als Zöllner, Auswahl einer Baustelle der Stadt. Keine SCRIPT ERROR. | 25.10.2026 |

### Sichtbare Ratssitzung (PR #202)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wann | Am Jahresende automatisch, wenn Anträge oder Wahlen anstehen: Die Partie hält an, die Kamera geht in den Ratssaal. Einstellung „Ratssitzung zeigen“: immer / nur wenn ich im Rat sitze oder betroffen bin / nie (dann nur das Ergebnis im Protokoll). Entschieden vom Nutzer. | 25.10.2026 |
| Ablauf | Punkt für Punkt in der Reihenfolge der Tagesordnung (Wahlen, Statut, Glaube, Pflaster, Steuer, Bau, Fest): Der Bürgermeister ruft auf, der Antragsteller begründet in einer Zeile, die Ratsherren stimmen nacheinander (Hand heben, kurze Begründung aus der Rechnung der KI). Sitzt der Spieler im Rat, stimmt er live an seiner Stelle (Ja/Nein/Enthaltung bzw. Kandidat; eine vorab gesetzte Stimme ist vorausgewählt). Ergebnis mit Zählung. Knöpfe „Weiter“, „Schneller“, „Überspringen“; Dauer etwa eine Minute. Entschieden vom Nutzer. | 25.10.2026 |
| Ratssaal | Neuer Innenraum des Rathauses im Stil der Werkstatt-Innenräume: langer Ratstisch mit Plätzen für alle Ratsherren, erhöhter Stuhl des Bürgermeisters, Schreiberpult, Stadtwappen, Wandbehang, Bleiglasfenster, Kerzenleuchter; Anker für Sitzplätze und Kamera. Die Ratsherren sind die vorhandenen Figuren (sitzend, Hand heben). Modellauftrag vom Nutzer freigegeben. | 25.10.2026 |
| Tafel (Umsetzung) | `RatssitzungAnzeige` unten in der Bildmitte (900 px breit, der Ratstisch bleibt darüber frei): links Punkt, Titel, worum es geht, die Rede des Antragstellers („Haus …: „Begründung““), die gerade fallende Stimme mit ganzer Begründung, das Ergebnis; rechts je Sitz eine Zeile Amt und Name, Stimme, Begründung (gekürzt, ganz im Tooltip), der eigene Sitz in Gold. Schritte je Punkt: Aufruf 2 s, Rede 3 s, je Stimme 1,2 s, Ergebnis 3,5 s; „Schneller“ schaltet 1×/2×/4×, „Weiter“ (auch Leertaste) beendet den Schritt, „Überspringen“ ruft `RatssitzungAbschliessen`. Reihenfolge der Punkte vom Kern (Anträge, dann Wahlen). Leere Zeilen nehmen keinen Platz weg. | 25.10.2026 |
| Eigene Stimme (Umsetzung) | Ist der Spieler an der Reihe und `Frei` (und `StimmeVerboten` leer), wartet die Tafel: eine Zeile „Eure Stimme als …“ mit Ja/Nein/Enthaltung bzw. den Kandidaten, die gesetzte Stimme gedrückt (Vorauswahl = vorab gesetzt). Ein Klick schickt `StatutStimme`, `StadtglaubeStimme`, `PflasterStimme`, `RatsantragStimme` bzw. `WahlStimme` und liest den Punkt neu; „Weiter“ gibt sie ab. Nach der letzten Stimme hält `RatssitzungWeiter` den Punkt ab, das Los zeigt der gezogene Kandidat. | 25.10.2026 |
| Halt und Hauptszene (Umsetzung) | `Hauptszene.Ratssitzung.cs`: `RatssitzungAnhalten` nach der Einstellung („betroffen“ = der Spieler stimmt in einem Punkt, hat etwas beantragt oder kandidiert). Steht die Partie vor der Sitzung, schließt die Hauptszene alle Tafeln und den Baumodus, geht in den Ratssaal und zeigt die Tafel; Raumleiste und Meldungen sind so lange aus, Tasten ruhen (Esc = Spielmenü, Leertaste = Weiter, F1 = Hilfe), die Sitzung wartet, solange Spielmenü, Einstellungen oder Hilfe offen sind. Den Jahresbericht, den der Kern nach dem letzten Punkt auslöst, zeigt sie erst nach dem Ergebnis, wenn die Tafel zu ist. | 25.10.2026 |
| Einstellung (Umsetzung) | Neuer Reiter **Spiel** in den Einstellungen mit „Ratssitzung zeigen“: Immer / Wenn ich im Rat sitze oder betroffen bin (empfohlen, Vorgabe) / Nie, gespeichert in `user://einstellungen.cfg` (Abschnitt „spiel“, `Fenster.Ratssitzung`). | 25.10.2026 |
| Speichern gesperrt (Umsetzung) | `Spielstaende.Speichern` prüft `SpeichernVerboten` vor dem Öffnen der Datei und gibt den Grund als Meldung zurück (Spielmenü zeigt ihn unter den Knöpfen, Autosave und `--speichern` ebenso). | 25.10.2026 |
| Letzte Sitzung (Umsetzung) | Im Rathaus, Reiter Rat, Knopf „Letzte Ratssitzung (Jahr) ansehen“, solange `LetzteRatssitzung` Punkte hat: dieselbe Tafel als Wiederholung im Ratssaal (ohne Stimmrecht, „Schließen“ statt „Überspringen“), die Partie steht so lange. Nach dem Laden gibt es keine (`LetzteRatssitzung` ist null) – dann der Hinweis auf das Protokoll im Bericht. | 25.10.2026 |
| Startoptionen | `--sitzungzeigen=<immer\|betroffen\|nie>` (nur dieser Start); `--sitzung=1` (ohne `--amt` ein Amt der untersten Stufe, Bewerbung eine Stufe höher – braucht `--aufsteigen=1 --vermoegen=30000` –, ohne `--antrag` wird ein Rivale Kämmerer und beantragt ein Fest; der Rivale bekommt dafür Aktionspunkte über Reflection) und `--sitzung=letzte` (spult ohne Halt durch und zeigt das Rathaus auf dem Reiter Rat mit dem Knopf); beide spulen bis zum Jahresende. | 25.10.2026 |
| Klicktest | Vulkan auf der T400, neu importiert, im Ratssaal aus dem Modell (vorher auch im alten Ratssaal aus Grundkörpern): Sitzung als Gefängniswärter mit Fest-Antrag eines Rivalen und eigener Wahl zum Marktmeister, Rede am Rednerplatz, Stimmen mit Hand und Kopfschütteln, eigene Stimme live auf Nein geändert, Ergebnis mit Protokoll, Wahlergebnis mit Gewähltem auf dem Stuhl, Überspringen, Einstellung „nie“ (keine Szene, gleich der Jahresbericht), Speichern im Spielmenü gesperrt, Wiederholung über den Knopf im Rathaus, Reiter „Spiel“ der Einstellungen. Keine SCRIPT ERROR, 769 Tests grün. | 25.10.2026 |
| Reihenfolge (Umsetzung) | Die Punkte kommen in der Reihenfolge des Kerns: erst die Anträge (Statut, Glaube, Pflaster, Steuer, Bau, Fest), dann die Wahlen (höchstes Amt zuerst) – so rechnet die Sitzung schon immer (Stichentscheid, Ansehen des Festes zählt bei der Wahl). Die Pflaster-Stimme gibt es nur live in der Sitzung. Entschieden vom Teamleiter. | 25.10.2026 |

## Entscheidungen (älter, verschoben am 09.11.2026)

### Danksagung (PR „Oberfläche: Danksagung mit allen Lizenzhinweisen“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ort und Aufbau | Knopf „Danksagung“ im Hauptmenü (`DanksagungAnzeige`), rollbare Menütafel in fünf Gruppen: Spiel, Engine & Bibliotheken, Klänge, Bilder & Modelle, Schriften; zweiter Knopf „Lizenztexte der Engine“ (Texte von Godot selbst). Menütafel im bisherigen Stil, noch nicht im Buch-/Schriftrollenstil (der Umbau steht aus). | 31.10.2026 |
| Daten | `game/daten/danksagung.json` wird von `tools/assets/danksagung_bauen.py` aus `tools/assets/danksagung_quelle.json`, der Freesound-Tabelle in `game/assets/audio/umgebung/HERKUNFT.md` und dem Protokoll gebaut; `protokoll_pruefen.py` (CI) wird rot, wenn Fremdmaterial fehlt oder die Datei veraltet ist. Ablauf: `docs/assets/README.md`. | 31.10.2026 |
| Namensnennung | CC BY, MIT, OFL und Apache mit Werk, Urheber, Adresse und Lizenz einzeln; CC0 (Kenney, Quaternius, ambientCG, Freesound-Urheber) als freiwilliger Dank. Wortlaut „mit Unterstützung von Claude (Anthropic)“ ist ein Vorschlag, der Nutzer entscheidet. | 31.10.2026 |

### Waren-Knöpfe (PR #210)

| Thema | Entscheidung | Datum |
|---|---|---|
| Stil | Entscheidung des Nutzers nach Proben von Gemini: Waren-Icons als quadratische Knöpfe (Aufbau wie Befehlsknöpfe klassischer Strategiespiele, ohne Elemente eines bestimmten Spiels): Motiv groß, gemalter dunkler Hintergrund, schmaler Rahmen mit Licht oben links; Malstil des Motivs wie die Proben (kolorierter Holzschnitt mit Tusche-Umriss, Schraffur und Wasserfarbe). Ersetzt die Pixel-Icons; der Entwurf PR #113 (Python-Zeichenskript) wird geschlossen. | 26.10.2026 |
| Rahmen nach Stufe | Der Rahmen zeigt die Stufe, ab der eine Ware hergestellt wird: Holz = Rohstoff (ohne Stufe), Stein = Stufe 1, Silber = Stufe 2, Gold = Stufe 3. | 26.10.2026 |
| Hintergrund nach Beruf | Schmied Glutrot, Alchimist Smaragdgrün, Parfumeur Purpur, Prediger Nachtblau, Wirt Bernstein, Tischler Ockerbraun, Steinmetz Schiefergrau-blau, Rohstoffe Lehmbraun. | 26.10.2026 |
| Bau | Zusammengesetzt: Gemini (Antigravity, Bild-Subagent) malt vier leere Rahmen, acht Hintergründe und 63 Motive auf Weiß; ein Skript stellt die Motive frei und setzt Hintergrund, Motiv und Rahmen je Ware zu einem Knopf zusammen. Jedes Teil ist einzeln austauschbar. Grafiken mit Gemini erzeugt, im PR gekennzeichnet. Erst Rahmen, Hintergründe und 8 Motive zur Abnahme durch den Nutzer, dann der Rest. | 26.10.2026 |
| Umsetzung | Rahmen **7 %** der Kantenlänge (Fassung A), Motiv mit Lichthof (Mitte +35 %, Ecken −15 %), warmweißem Saum (60 %, bei hellem Motivrand bis 25 %) und Schlagschatten. Skript `tools/icons/knoepfe_bauen.gd` (Befehle `quellen`, `bauen`, `tafel`, Schalter `--rahmen=`, `--ohne-effekte`); `WarenIcons` zeigt 128 px linear mit Mipmaps. **Grafiken mit Gemini erzeugt** (Rahmen, Hintergründe, Motive; Modell gemini-3.1-flash-image über die API, 63 Motive). Neue Ware: Motiv erzeugen (kolorierter Holzschnitt auf Weiß), `quellen` ausführen oder als PNG in `tools/icons/quellen/motiv/<ware>.png` ablegen, dann `bauen`. | 26.10.2026 |
| Herkunft (Berichtigung, PR #239) | Belegt ist der Weg **über die API** mit `gemini-3.1-flash-image` (Zeile „Umsetzung“, PR #210); die Zeile „Bau“ nennt „Antigravity, Bild-Subagent“ – welcher Weg es war, ist offen. Die Motive der Einrichtungs-Icons in `tools/icons/quellen/motiv/einrichtung_*` sind **nicht** von Gemini, sondern von Qwen-Image (PR #224). Herkunft je Datei: `docs/assets/protokoll.csv`. | 29.10.2026 |

### Porträts (PR #211)

| Thema | Entscheidung | Datum |
|---|---|---|
| Umfang | Entscheidungen des Nutzers nach Gemini-Proben: Porträts für alle Personen der Dynastien (Spieler, Familie, Erben, KI-Familien, Ratsherren); Arbeiter und Bürger später. Stil der Proben: kolorierter Holzschnitt mit Wasserfarbe, Brustbild, Dreiviertelansicht. Grafiken mit Gemini erzeugt (wie die Waren-Knöpfe). | 26.10.2026 |
| Aufbau | Person auf Weiß erzeugt, freigestellt und vor einen gemalten Hintergrund nach Standgruppe gesetzt (Gemeiner: Lehmwand, Bürger: Holzvertäfelung, Patrizier: Wandteppich, Adel – Edler, Freiherr, Reichsgraf: dunkles Gewölbe); kein Rahmen. So bewegt sich beim Atmen nur die Person. | 26.10.2026 |
| Vielfalt | Je Volk und Geschlecht 5 Gesichter; Altersstufen Kind, jung, mittel, alt (aus demselben Gesicht per Gemini-Bearbeitung); Kleidung schlicht (Gemeiner, Bürger) und vornehm (Patrizier, Adel), Wechsel beim Aufstieg; Haar- und Augenfarbe wählbar bzw. vererbt, umgefärbt im Spiel. Ein Bild mit geschlossenen Augen je Gesicht und Altersstufe für das Blinzeln. | 26.10.2026 |
| Leben im Bild | Blinzeln, leichtes Atmen, Kerzenlicht, kleines Nicken bei Gesprächen; keine Mimik. | 26.10.2026 |
| Spielstart | Eigene Seite im Startmenü: Volk, Geschlecht, 5 Gesichter zum Anklicken, Haar- und Augenfarbe, großes Porträt mit Vorschau. | 26.10.2026 |
| Orte | Familie und Wohnhaus (Familienbuch, Oberhaupt, Ehepartner, Kinder, Erbe), Rat und Ämter (Ratstafel, Sitzung, Antragsteller), Rivalen und Heirat (KI-Oberhäupter, Heiratskandidaten), kleines Porträt des eigenen Oberhaupts oben links. | 26.10.2026 |
| Vererbung | Kind bekommt eines der Gesichter von Vater oder Mutter (gleiches Geschlecht bevorzugt), Haar- und Augenfarbe von einem Elternteil mit kleiner Chance auf eine andere; Volk nach den bestehenden Regeln. KI-Familien gewürfelt. | 26.10.2026 |
| Vorgehen | In Etappen: erst Menschen komplett (≈ 120 Bilder) und Hintergründe, dazu ein Test zum Umfärben von Haar und Augen; Abnahme durch den Nutzer mit Kostenstand, dann die übrigen 5 Völker. Gesamt etwa 720 Bilder. | 26.10.2026 |
| Umsetzung Oberfläche | `PortraetAnzeige` (Control) mit `portraet.gdshader`: Hintergrund nach Standgruppe, Person freigestellt davor; Haar und Augen über Graustufen-Masken umgefärbt (Helligkeit des Bildes bleibt, nur der Ton wechselt; ohne Masken aus), Blinzeln im Augenband (alle 3–7 s, selten doppelt), Atmen ±0,6 % von unten, Nicken, Kerzenlicht (in der Grafikstufe Niedrig aus). Bilder werden beim Laden mit Mipmaps versehen, damit 64 px scharf bleiben. Fehlt ein Bild (andere Völker, Gesichter noch nicht geliefert), steht eine Silhouette vor dem Hintergrund. Orte: Seite „Euer Oberhaupt“ im Startmenü (vor den Einstellungen der Partie; Volk, Geschlecht und Vorname wanderten dorthin), Kopfzeile, Familie (Oberhaupt und Ehepartner 128 px, Kinder und Brautschau 64 px), Rat (Inhaber der Ämter), Ratssitzung (wer spricht, nickt bei Zustimmung), Bericht „Rivalen“. | 26.10.2026 |
| Blinzeln ohne Blitz (PR #216) | Ursache des Lichtblitzes: Der Shader multiplizierte sein Ergebnis mit `COLOR`, und das enthält beim Eintritt in `fragment()` schon das offene Bild – Iris und Lider des offenen Bildes schienen durch das „zu“-Bild (Wangen und Haar nahmen „alte Farbe“ an). Dazu: Haar, Brauen und Iris waren nur im offenen Bild umgefärbt, das Band lief über die ganze Breite, und die Bild-KI malt das „zu“-Bild mit leicht anderer Farbe und Schraffur. Jetzt: Der Shader merkt sich die Modulate-Farbe (`varying modulat`) und multipliziert mit dem Bild, das gerade gilt (offen/„zu“ überblendet); er färbt auch das „zu“-Bild mit denselben Masken um; überblendet wird nur in einer weichen Ellipse um jedes Auge (`PortraetAnzeige.Augenzonen` aus der Augenmaske), nicht im ganzen Band; die Iris-Umfärbung bleibt, bis das Lid sie überdeckt. Das Bauwerkzeug (`spiel`) gleicht jedes „zu“-Bild farblich an das offene an (Mittel und Streuung je Farbkanal im Augenband, Verstärkung höchstens ±10 %). `--blinzeln=<0–1>` hält ein halbes Blinzeln fest. | 27.10.2026 |
| Halbling-Haut (PR #216) | Wangen und Nasen waren zu rot und gesättigt: Das Bauwerkzeug dämpft beim Schritt `spiel` für Halblinge die Sättigung roter Hauttöne (Farbton um 355°, Sättigung über 0,26 um 85 % des Überschusses, nur Gesichtshöhe 25–65 % und helle Pixel; Kleidung, Schmuck und Haar bleiben). Gilt auch für neue Bilder. | 27.10.2026 |
| Haarmasken fehlen | `spiel` schreibt die Haarmasken von Elben, Halblingen und Zwerginnen nicht (Tabelle `KEINE_HAARMASKE`: angemalter Bart in der Maske), bis es neue gibt; dann Eintrag streichen. | 27.10.2026 |
| Herkunft (Berichtigung, PR #239) | Gemini lieferte nur die Proben und die erste Menschen-Fassung (37ecc8aa); **ausgeliefert** sind Personen und Masken aller Völker von der lokalen Bild-KI (ComfyUI mit Qwen-Image, ab c16f5c20). „Grafiken mit Gemini erzeugt“ und „per Gemini-Bearbeitung“ in den Zeilen oben gelten nur für diese Proben. Werkzeug der vier Hintergründe: nicht belegt. Herkunft je Datei: `docs/assets/protokoll.csv`. | 29.10.2026 |

### Lexikon (PR #214)

| Thema | Entscheidung | Datum |
|---|---|---|
| Vorbild | Das Lexikon in Crusader Kings 3, nur die Mechanik (Nutzer). Begriffe mit Verweisen untereinander, Suche, Live-Zahlen. | 27.10.2026 |
| Daten | `game/daten/lexikon.json`: Liste aus Kennung, Titel, Gruppe, Text (etwa 160 Begriffe in zehn Gruppen, Erstfüllung aus dem Spielbuch). Verweise `[[kennung]]` oder `[[kennung\|Anzeigetext]]`, Zahlen `{wert:name}`. Feste Zahlen nur, wo sie nicht änderbar sind; sonst Platzhalter. | 27.10.2026 |
| Platzhalter | `Lexikon.Werte`: Name → Abfrage der Wirtschaftsdaten (Steuersatz aus `Stadtwirtschaft.Kasse`, sonst `Wirtschaftsdaten`). Ohne Partie (Hauptmenü) lädt das Lexikon `wirtschaft.json` selbst. Ein neuer Platzhalter ist eine Zeile in `Lexikon.WerteBauen`. | 27.10.2026 |
| Prüfung | `--lexikon-pruefen=1` (Godot ohne Fenster): Kennungen eindeutig und klein, jeder Verweis zeigt auf eine Kennung, jeder Platzhalter ist bekannt und hat in `wirtschaft.json` einen Wert, keine falschen Klammern. Rückgabewert 0/1. Wer Einträge ergänzt, ruft die Prüfung auf. | 27.10.2026 |
| Tafel | Fenster in der Bildmitte (wie Hilfe und Markt), Taste L (frei, geprüft) und Knopf „Lexikon“ vor „Menü“ in der Betriebsleiste; Esc schließt (nach der Hilfe, vor den Tafeln). Links Suche und `Tree` nach Gruppen (ohne Suchtext), rechts Eintrag als `RichTextLabel` mit goldenen Verweisen, „Erwähnt in:“ als automatische Rückverweise, „Zurück“ über einen Verlauf. Die Zeit läuft weiter. | 27.10.2026 |
| Tooltips | `Lexikon.Finde(kennung)` gibt `(Titel, Text)` als Klartext (Zahlen eingesetzt, Verweise als Wörter) oder null; `Lexikon.Oeffnen(kennung)` öffnet die Tafel. Der Teamleiter verdrahtet beides mit den Tooltips des Fensters (`BegriffSuchen`, `LexikonOeffnen`). | 27.10.2026 |

### Interface: Tooltips festheften, Verweise, Fenster (PR #214)

| Thema | Entscheidung | Datum |
|---|---|---|
| Tooltips festheften | Füllkreis oben rechts im Tooltip; nach der Zeit aus den Einstellungen (Grundwert 1,5 s, 0,5–5 s, oder „nur mit Taste“) wird er ein festes, verschiebbares Fenster mit ✕; T oder mittlere Maustaste heftet sofort fest. Der Kreis zählt, solange die Maus auf dem Tooltip oder seiner Quelle ruht. Vorbild Crusader Kings 3, nur die Mechanik. | 27.10.2026 |
| Wie ohne Umbau der ~170 Stellen | Der eingebaute Tooltip ist in `project.godot` ausgeschaltet; der Autoload `Tooltips` liest den Text über `GuiGetHoveredControl()`/`GetTooltip()` und zeigt ihn selbst. Neue `TooltipText`-Stellen gehen ohne Zutun. | 27.10.2026 |
| Verweise | `[[kennung]]` oder `[[kennung\|Anzeigetext]]` im Tooltip-Text; nur mit Treffer von `Tooltips.BegriffSuchen` (vom Lexikon gesetzt) unterstrichen, sonst einfacher Text. Darüberfahren öffnet den Begriff (ab PR Interface-Feinschliff: 0,2 s, auch in festen Tooltips), Klick heftet ihn fest; beliebig tief. Im festen Tooltip eines Begriffs: Knopf „Im Lexikon“ (`Tooltips.LexikonOeffnen`). | 27.10.2026 |
| Fenster | Alle Tafeln mit `Fensterrahmen` verschiebbar (oberer Rand), ✕, Lage gemerkt (geklemmt in den sichtbaren Bereich), Klick = nach vorn. Esc schließt das oberste, X alle. Nur ziehbar, ohne ✕ und Esc: Bautafel und Bau-Rückfrage (haben ihre eigenen Schritte). Starr: Spielmenü, Einstellungen, Hilfe, Jahresrechnung, Ratssitzung, Kartenwerkstatt. | 27.10.2026 |
| Mehrere offen | Neuer Knopf „Festhalten“ (Nadel) bei Rat, Familie, Gericht, Hinterzimmer, Markt, Wohnhaus, Bericht, Hafen: Das Fenster bleibt, wenn eine andere Tafel „alles andere schließt“. Vor der Ratssitzung werden auch festgehaltene geschlossen. | 27.10.2026 |
| Eigene ×-Knöpfe | Bericht, Hafen und Wohnhaus hatten ein eigenes ×; sie sind durch das ✕ des Rahmens ersetzt (Wohnhaus ruft weiter `Geschlossen`). | 27.10.2026 |
| Automatische Verweise | Die vorhandenen Tooltips haben keine `[[…]]`: Der Haken `Tooltips.VerweiseErgaenzen` (von der Hauptszene auf `Lexikon.VerweiseEinsetzen` gesetzt) macht beim Aufbau des Tooltips aus dem ersten Vorkommen jedes Lexikon-Titels einen Verweis (ganzes Wort, Schreibung wie im Titel, Endungen -s/-n/-en/-e/-es, dazu das optionale Feld `auch` in `lexikon.json`; längere Titel zuerst, höchstens 6, nie in vorhandenem `[[…]]`, nie der Begriff in seinem eigenen Tooltip). Prüfung: Startoption `--verweistest=1` schreibt drei Beispielsätze mit Ergebnis ins Log. | 27.10.2026 |

### Interface-Feinschliff (PR #217)

| Thema | Entscheidung | Datum |
|---|---|---|
| Auswahl an der Maus | Ein Klick auf ein Gebäude setzt die Auswahl-Tafel wie einen Tooltip unten rechts neben den Zeiger (Abstand 14/22 Pixel, am rechten bzw. unteren Rand links bzw. über den Zeiger, `Tooltips.Lage`). Sie bleibt verschiebbar und schließbar; gezogen bleibt sie dort bis zur nächsten Auswahl. Gemerkte Lage gilt für sie nicht mehr (`Fensterrahmen.LageMerken = false`). Öffnet sie ohne Klick in die Welt (Taste, Leiste, Kopfzeile), steht sie wie bisher rechts unter der Kopfzeile. Technik: `Fensterrahmen.PlatzSetzen(Func<Größe, Sichtbar, Ecke>)`. | 27.10.2026 |
| Verweise per Hover | Ursache des Klickzwangs in festen Tooltips: Der Verweis-Zweig in `Tooltips._Process` stand hinter `if (t.Fest) continue;`. Jetzt öffnet das Darüberfahren (0,2 s) den Begriff, auch in festen Tooltips, direkt neben der Maus; ein Klick heftet weiter sofort fest. Der neue Tooltip steht neben der Maus, aber nie über dem Eltern-Tooltip (sonst rechts oder links daneben, zuletzt darunter; `LageNebenEltern`). Er bleibt, solange die Maus auf dem Verweis oder in ihm ist (0,4 s Gnadenzeit), füllt seinen Kreis und wird nach der eingestellten Zeit fest. | 27.10.2026 |
| Tooltips überall (Start) | Ursache fehlender Tooltips: Beschriftungen (`Label`) und Bilder haben `MouseFilter = Ignore` und fangen keine Maus. `Hauptmenue.Text` setzt jetzt `Pass` (alle bisher wirkungslosen Label-Tooltips gehen damit). `InnenTafel.Tipp`, `ZeileTipp` und `WarenTipp` (Marktpreis, Lager, Zeit, Zutaten samt Kosten, Ertrag je Stück aus Marktpreis minus Zutaten) hängen Tooltips an Zeilen und Spaltenköpfe von Produktion, Lager, Personal, Einrichtung; ebenso Karren, Personal und Förderung im Bedienfeld, Zustand und Angaben der Kurzübersicht, Uhrzeit und Tempoknöpfe oben. Auch Bericht, Jahresbericht, Rat, Hafen, Gericht, Gauner, Verkauf, Bau, Weg, Rathaus, Wachplan, Familie und Wohnhaus haben nun Tooltips auf Köpfen und Zeilen. Nicht lesbar aus dem Kern und deshalb weggelassen: Lohn je Arbeiter im Jahr (nur Grundlohn), Gewinn je Stück nach Lohn. | 27.10.2026 |

### Fehler und Kleinkram (PR #219)

| Thema | Entscheidung | Datum |
|---|---|---|
| Kennung umbenennen | Die Eigenschaften-Tafel der Kartenwerkstatt hat ein Textfeld für die Kennung eines Gebäudes (nutzt `Kartenentwurf.KennungSetzen`, ein Rückgängig-Schritt). Fehler des Kerns (vergeben, ungültige Zeichen, leer) erscheinen rot unter dem Raster, das Feld springt zurück. Die Felder der Tafel lesen die Kennung jetzt beim Auslösen aus dem Gebäude, nicht mehr aus einer beim Bau der Tafel gemerkten Kopie (sonst hätte „Drehung“ nach dem Umbenennen die alte Kennung benutzt). Klicktest Probestadt, Marktplatz1 → Platz_Neu und „Bad_Zeichen!“ (Bilder im Notizordner `klein-spiel/k2`). | 27.10.2026 |
| Hinweis Personalbuch | Schon erledigt mit dem Umbau der Betriebsleiste: `BetriebsUebersicht.Zustand` nennt für Umlandbetriebe „stellt unter „…“ Arbeitskräfte ein“. Kein weiterer Text mit „Personalbuch“ gilt für Umland. | 27.10.2026 |
| Tooltips Lohn und Gewinn | Der Waren-Tooltip (`InnenTafel.WarenTipp`, Produktions- und Lagertafel) nennt für selbst hergestellte Waren einer Werkstatt „Gewinn je Stück“ aus `Kalkulation.GewinnJeStueck` mit Aufschlüsselung Preis − Steuer − Zutaten − Lohn − Unterhalt (ohne Personal mit einem Gesellen gerechnet, steht dabei); die Zeile „Ertrag (Preis minus Zutaten)“ bleibt nur für Waren, die der Betrieb nicht herstellt. Der Arbeiter-Tooltip im Personalbuch nennt den Lohn im Jahr (`Betrieb.LohnJeArbeiter`). Klicktest Gießerei (Bilder `klein-spiel/k4`, `k6`). | 27.10.2026 |
| Ratsherren-Porträts | `PortraetAnzeige.ZeigenRatsherr` nimmt Geschlecht und Aussehen aus `Ratsherr.Geschlecht` und `Ratsherr.Aussehen` (Daten, Datenversion 58); nur die Altersstufe leitet die Oberfläche weiter aus dem Namen ab. Die Aufschlüsselung des Gewinns im Waren-Tooltip steht in drei Zeilen. | 27.10.2026 |

### Talentbaum der Einrichtung (PR #224)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufbau | Einrichtung von Betrieben und Wohnhaus als Talentbaum (Vorbild WoW, nur Mechanik): 3 Spalten (Betrieb: Werkstatt / Lager & Handel / Schutz & Personal; Wohnhaus: Haushalt / Ansehen / Schutz) × 3 Reihen = Gebäudestufen; gesperrte Reihen grau, Linien zeigen Voraussetzungen; Icons im Holzschnitt-Stil der Waren-Knöpfe, Rahmen nach Zustand (gekauft, kaufbar, gesperrt); Tooltip mit Wirkung, Preis, Voraussetzung, Amortisation (festheftbar, Lexikon-Verweise). Öffnet über „Einrichtung“ außen und innen. Bezahlt mit Gulden. Entscheidung des Nutzers. | 28.10.2026 |
| Personal | Personalbuch zeigt Stufe, Erfahrung bis zur nächsten Stufe, Lohn, Porträt und Namen; Knopf „Befördern“ (mit Brief-Kosten), beim Vorarbeiter Wahl aus drei Eigenschaften als Karten. Entscheidung des Nutzers. | 28.10.2026 |
| Umsetzung Talentbaum | `TalentbaumAnsicht` (gemeinsam): 3 × 3 Zellen mit Stücken, Rahmen nach Zustand (gekauft grün, kaufbar golden, gerade nicht möglich blass golden, gesperrt grau), Linien Voraussetzung → Stück hinter Knöpfen und Beschriftungen, Tooltip aus `Tipp` (Wirkung, Preis, Stufe, Voraussetzung, Amortisation, Sperrgrund). `EinrichtenAnzeige` (Betrieb, innen an der Werkzeugwand und außen über „Einrichtung“ in der Kurzübersicht) und `WohnhausBaumAnzeige` (Knopf „Einrichtung …“ in der Wohnhaus-Tafel) ersetzen die Einrichtungsliste. Icons: `einrichtung_<kennung>.png` in `assets/oberflaeche/icons/`, sonst ein ähnliches Waren-Icon (Tabelle `Ersatz`), sonst der Anfangsbuchstabe; Bildbeschreibungen für die Bild-KI liegen im Notizordner des Teamleiters. | 28.10.2026 |
| Umsetzung Personal | `PersonalAnzeige` zeigt je Arbeiter Porträt (`PortraetAnzeige`, Altersstufe aus Menschenjahren), Name, Stufe, Erfahrungsbalken (Stunden und Jahre bis zur nächsten Stufe), Lohn und Eigenschaft; „Befördern → Stufe“ mit Briefkosten im Tooltip; nach der Beförderung zum Vorarbeiter drei Eigenschaftskarten (`EigenschaftWaehlen`). Einstellen bietet nur den Anfänger. Die Innenräume färben Figuren nach Anfänger / Mittelstufe / Vorarbeiter (`Stadtansicht.IstAnfaenger`, `IstVorarbeiter`). | 28.10.2026 |
| Herkunft der Icons (PR #239) | Motive von der lokalen Bild-KI (ComfyUI mit Qwen-Image und Qwen-Image-Edit), Rahmen und Hintergründe die Gemini-Teile aus PR #210. Die Bildbeschreibungen liegen jetzt in `docs/assets/prompts/einrichtung_icons.md`, Herkunft je Datei in `docs/assets/protokoll.csv`. | 29.10.2026 |

### Charakterfenster (PR #227)

| Thema | Entscheidung | Datum |
|---|---|---|
| Fenster | Ein Klick auf jedes Porträt (Familienbuch, Rat, KI-Häuser, Vorarbeiter) öffnet ein Charakterfenster wie in CK3: großes Porträt, Talente, Charakterzüge als Icons mit Tooltip, Familie; später die Meinung zu einem mit Gründen (PR 2) und der Stammbaum. Entscheidung des Nutzers. | 28.10.2026 |
| Icons | (Abgelöst durch „Stil der Zug-Icons“.) Runde Medaillons im Holzschnitt-Stil, Motiv per lokaler Bild-KI wie die Einrichtungs-Icons, Rahmen nach Art: Persönlichkeit Bronze, angeboren Silber, erworben Eisen; Abnahme an 6 Proben durch den Nutzer. Entscheidung des Nutzers. | 28.10.2026 |
| Umsetzung Fenster | `CharakterAnzeige`: frei ziehbares, festhaltbares Fenster (Standardplatz rechts, 780 px breit); links großes Porträt, rechts Name, Volk, Alter, Rolle, Stand, Amt, Glaube, Krankheit und die fünf Talente; darunter Charakterzüge in drei Gruppen, unten die Familie (Eltern, Ehepartner, Kinder) als kleine Porträts mit „‹ Zurück“. Ratsherren: ohne Talente und Familie, mit Bestechlichkeit. Kinder zeigen „Die Persönlichkeit entwickelt sich erst mit der Volljährigkeit“. Öffnen über zwei Haken in `PortraetAnzeige` (`PersonOeffnen`, `RatsherrOeffnen`): jedes Porträt, das mit `Zeigen(Familienbuch, Person)` oder `ZeigenRatsherr` gefüllt wird, ist klickbar (Familie, Erbfolge, Brautschau, Rat, Kopfzeile, Rivalen im Bericht); Porträts mit Volk-Angaben (Hauptmenü, Personalbuch der Vorarbeiter) nicht. | 28.10.2026 |
| Umsetzung Talente | Sechs Sterne je Talent, selbst gezeichnet: golden = Anlage, grün = Zugbonus, rot = Abzug durch Zug oder Krankheit; dahinter die Zahl mit Unterschied („4 (+1½)“); Tooltip: „Anlage 3, Gierig +1, Husten −1“ mit Lexikon-Verweisen. | 28.10.2026 |
| Umsetzung Züge | Runde Medaillons (64 px) aus `res://assets/oberflaeche/zuege/<kennung>.png` (kleingeschrieben, 128 px, Rahmen eingemalt); ohne Bild ein gezeichneter Ring nach Art mit Anfangsbuchstaben. Tooltip: Name (Art), Beschreibung, Wirkung, Gegenteil, Verweis auf das Lexikon. Lexikon-Eintrag „Charakterzüge“ (Gruppe Haus und Familie) mit dem neuen Platzhalter `{wert:zuege_liste}`. | 28.10.2026 |
| Stil der Zug-Icons | **Ausnahme vom Holzschnitt** nur für die Zug-Icons: kräftig gemalt mit dunklem Umriss und farbigem Leuchten je Zug (Farbe nach Zug, Rahmen nach Art). Motive als Stichpunkte vom Teamleiter, Bilder vom Nutzer mit Gemini erzeugt (Pilger und Gelehrt mit ChatGPT), Teamleiter schneidet sie rund aus; den geschmiedeten Ring rechnet seit PR #230 ein Skript dazu. Klug ist eine Eule auf einem Buch, Hochmütig ein stolzer Edelmann. Ein LoRA-Training war geplant und ist verworfen. Entscheidung des Nutzers. | 28.10.2026 |

### Ränder der Zug-Medaillons (PR #230)

| Thema | Entscheidung | Datum |
|---|---|---|
| Form und Rand | Rund bleiben, aber ein dünner, schlichter Ring nach dem Vorbild der Warcraft-3-Knöpfe (nur die Machart): 4 px bei 64 px, abgeschrägt mit Lichtkante oben links und Schatten unten rechts, ohne Ornament und Nieten. Das Spiel zeichnet den Ring selbst (`ZugIcon`), die Bilder sind ohne Rahmen. Entscheidung des Nutzers. | 28.10.2026 |
| Farben | Persönlichkeit **Kupfer** (rötlich), angeboren **Gold** (statt Silber), erworben **Eisen** – drei klar getrennte Töne. Entscheidung des Nutzers. | 28.10.2026 |
| Machart (ersetzt „schlicht“) | Der schlichte Ring war dem Nutzer zu wenig: Der Ring ist jetzt **geschmiedet** – gewölbtes Profil mit Glanz und Spiegelung (oben hell, unten dunkel), Hammerschlag-Dellen, Schleifspuren und 2–3 **Kerben von Schwert- oder Axthieben** mit blank geschlagener Kante, je Zug an anderer Stelle (aus der Kennung gewürfelt), Licht immer oben links. Gerechnet vom Skript `tools/zugmedaillons/schmiede.gd` (256 px, gespeichert 128 px) und ins Bild gebacken; der gezeichnete WC3-Ring bleibt nur für Züge ohne Bild. Entscheidung des Nutzers. | 28.10.2026 |

### Kartenwahl und Entwickler-Option (PR #229)

| Thema | Entscheidung | Datum |
|---|---|---|
| Kartenwahl | „Neue Partie“ beginnt mit der Seite „Stadt wählen“ (Kacheln mit Vorschaubild, Name, Kurztext, Kennzahlen), danach „Euer Oberhaupt“. Die letzte Wahl ist vorgewählt (`user://einstellungen.cfg`). Die Partie startet über `Stadtkarten.Gewaehlt`, denselben Weg wie `--karte=`. Entscheidung des Nutzers. | 28.10.2026 |
| Welche Karten | Karten mit Kennzeichen `spielbar` in der Kartendatei (heute Probestadt; Köln, wenn fertig) und selbst gebaute aus `user://karten/`, soweit der Haken „In der Kartenwahl anbieten“ (Reiter „Karte“ der Werkstatt) gesetzt ist – eine neue Werkstattkarte hat ihn von Anfang an. Greybox, Köln und Messstadt nur im Entwickler-Modus („Testkarte“). | 28.10.2026 |
| Entwickler-Option | Haken „Entwickler-Optionen“ unten rechts im Hauptmenü (`Entwickler.Aktiv`, gemerkt), zentrale Stelle für spätere Entwickler-Einstellungen (Debugging …); heute nur die Kartenliste. | 28.10.2026 |
| Vorschaubilder | Einmal aus dem Spiel mit Vulkan gerendert (`tools/kartenbilder/kartenbilder.sh`, ganze Karte, Mittag, Sommer) nach `assets/oberflaeche/karten/`. Selbst gebaute Karten zeigen einen Platzhalter mit dem Namen (einfachste saubere Lösung). | 28.10.2026 |

### Meinungen und Aktivitäten (PR #231)

| Thema | Entscheidung | Datum |
|---|---|---|
| Meinung im Charakterfenster | Bei jeder wichtigen Person außer dem eigenen Oberhaupt eine Zeile „Meinung über Euch“: Balken (−100 rot, 0 Mitte, +100 grün), Zahl in Farbe (grün ab +5, rot ab −5, sonst grau), Tooltip mit allen Gründen, feste zuerst, die aus Taten unter „verblassen jedes Jahr“. Heiratskandidaten und das eigene Oberhaupt zeigen nichts. | 28.10.2026 |
| Aktivitäten | Knopf „Aktivitäten“ (nur bei wichtigen Personen und `Meinungswesen.IstAktiv`) schaltet das Fenster auf die Liste um: Name, AP und Gulden, Gefahr mit Unglück, Wirkung und Mittel, Hinweis zu Zügen, Sperrgrund rot und ausgegraut. Geschenk mit wählbarem Betrag. Ergebnis als Meldung oben in der Liste und im Protokoll; Fenster baut sich neu auf. | 28.10.2026 |
| Rats-Tafel | Spalte „Eure Gunst“ heißt „Meinung“ (Zahl, Farbe, Gründe-Tooltip aus `MeinungsHilfe`); Tooltip am Knopf „Beschenken“ nennt `GunstJeGulden(r, konto)`; Texte mit „Gunst“ sprechen jetzt von Meinung. Lexikon: neue Einträge „Meinung“ und „Aktivitäten“, „Gunst“ verweist darauf. | 28.10.2026 |

### Wege-Modus im Baumenü (PR #242)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wege nur im Baumenü | Außerhalb des Baumenüs lassen sich Wege nicht mehr anklicken (kein Wegtreffer, kein Tooltip); Klicks gehen wie sonst an Gebäude und Boden. Entscheidung des Nutzers. | 29.10.2026 |
| Umschalter | Im Baumenü (`BauAnzeige`) oben „Gebäude \| Wege (G)“. Auf „Wege“ leuchten alle Wege nach Belag (Trampelpfad dunkelbraun, Fahrweg ocker, Kies grau-blau, Pflaster hell, Baustelle orange), der Weg unter der Maus heller; die Bauzonen sind dann aus, die Tafel zeigt die Farblegende. Verlassen des Baumenüs (Esc, B, Schließen) beendet den Modus. Entscheidung des Nutzers. | 29.10.2026 |
| Taste | **G** (W ist die Kamera): öffnet das Baumenü im Wege-Modus bzw. schaltet zwischen Gebäude und Wege um. Teamentscheidung, weil W belegt ist. | 29.10.2026 |
| Tooltip | Am Zeiger: Wegname, Belag (mit „wird gebaut“), nächste Stufe (Kies, dann Pflaster) mit Länge, Kosten und Bauzeit des 50-m-Abschnitts um die Maus (`Wegebau.Vorschau`, derselbe Abschnitt, den die Wegtafel vorwählt, `WegAnzeige.Vorwahl`), Sperrgrund, „Klick: Wegtafel“. Kein Kern-Eingriff nötig. Eigenes kleines Feld statt des Tooltip-Systems (das hängt an Steuerelementen, hier ist es die 3D-Welt). | 29.10.2026 |
| Wegtafel | Öffnet bei Klick links neben der Bautafel (`WegAnzeige.Oeffnen(…, versatz)`), Ausbau, Kosten und Bauzeit wie bisher. Esc schließt zuerst die Wegtafel, dann das Baumenü. | 29.10.2026 |

### Handbuch-Stil Steuerung (PR #245)

| Thema | Entscheidung | Datum |
|---|---|---|
| Pilot | Nur das Kapitel „Steuerung“ im Spielbuch wird zum Handbuch-Kapitel (Entscheidung des Nutzers); gefällt der Stil, folgen die anderen zwölf Kapitel. | 29.10.2026 |
| Stil | Kurze Abschnitte wie in Handbüchern klassischer Aufbauspiele (Erste Schritte, Bildschirm, Kamera, Gebäude, Bauen mit Wege-Modus, Fenster und Tooltips, Tastenübersicht, Einstellungen), Du-Form, Hinweisblöcke `> [!TIP]` und `> [!NOTE]`. PR-Nummern und Entwicklerhinweise stehen nicht im Spielerteil; der Godot-Editor-Hinweis und das Testtempo (16×/32×) stehen in `docs/ENTWICKLUNG.md`, das Testtempo außerdem als kleine Fußnote im Kapitel. | 29.10.2026 |
| Bilder | Echte Bildschirmfotos aus dem Spiel (Vulkan, Tag, klares Wetter, Miniatur-Unschärfe aus) mit nummerierten Markierungen (Gold auf Dunkelbraun, Schrift des Spiels) und einer Legende als Tabelle darunter; `.webp`, 1280 px breit, unter `docs/bilder/handbuch/`. Vier Bilder: Bildschirm, Bauen, Wege-Modus, Tooltip (Ausschnitt). | 29.10.2026 |
| Werkzeug | `tools/handbuch/`: `aufnehmen.sh` (Rohbilder mit dem Klicktest), `beschriften.sh` mit `beschriften.gd` (Godot unter Xvfb setzt Rahmen und Nummern nach `bilder.json`, schreibt `.webp`). Die Bilder lassen sich nach einer Änderung der Oberfläche neu erzeugen. | 29.10.2026 |
| Protokoll | Bilder unter `docs/` sind nicht protokollpflichtig (Prüfskript prüft nur `game/assets/` und die KI-Quellen unter `tools/`). Es sind Bildschirmfotos des Spiels, nicht KI-erzeugt. | 29.10.2026 |
| Berichtigungen | Gegen den Code geprüft: Die Einstellungen haben **sechs** Reiter (nicht fünf: dazu „Spiel“); ein Klick auf das Porträt in der Kopfzeile öffnet das Wohnhaus, nicht das Charakterfenster; neu aufgenommen: Wege-Modus (G), X (alle Fenster zu), Rechtsklick im Gebäude, Rahmenwahl und Randschwenken drinnen, Rechtsziehen im Baumodus, Charakterfenster und Aktivitäten, Esc-Reihenfolge. | 29.10.2026 |

### Befehle nur über den Boten (Mehrspieler M2, PR #251)

| Thema | Entscheidung | Datum |
|---|---|---|
| Regel | **Befehle nur über den Boten:** Keine Datei in `game/scripts/` ruft `Stadtwirtschaft.Ausfuehren` auf (Kontrolle: `git grep "\.Ausfuehren(" game/scripts` findet nichts). Eine Tafel schickt ihren Befehl mit `UiHilfe.Befehlen(simulation, spieler, befehl, this, ergebnis => { … })`; alles, was vom Ergebnis abhängt (Meldung, Neuaufbau, Schließen, „Beauftragt“), steht im Rückruf, nicht dahinter. Der Rückruf läuft nur, wenn der Besitzer (die Tafel, bei Klassen ohne Knoten ihr Wurzelknoten) noch gültig ist (`IsInstanceValid`) – im Mehrspieler wirkt der Befehl erst im Ziel-Tick, und die Tafel kann bis dahin zu sein. `UiHilfe.MeldungText` macht die übliche Meldung („✗ …“ bei Misserfolg). Neue Tafeln übernehmen das Muster in ihrer `Befehlen`-Methode. | 29.10.2026 |
| Klicktest-Startoptionen | Brauchen sie das Ergebnis gleich (Schleifen, die bei Misserfolg abbrechen, Aufbau vor `--spulen`), nehmen sie `_simulation.Bote.SofortAusfuehren`; sonst `Bote.Einreihen` mit Rückruf (Meldung im Protokoll). Im Mehrspieler werfen die Sofort-Stellen – sie sind nur Testwerkzeug. | 29.10.2026 |
| Einzelspiel | Verzögerung 0: Verhalten wie vorher. Klicktest (Vulkan, probestadt/greybox): Marktauftrag, Neubau mit Baurückfrage, Wegausbau, Geschenk im Rat, Werben in der Familie, Wohnhaus-Einrichtung, Schiffsauftrag in der Hafentafel und Geschenk im Charakterfenster – Gulden sinken, Meldung steht, Tafel baut sich neu auf bzw. schließt, kein SCRIPT ERROR. | 29.10.2026 |

### Lesbarkeit und mittige Fenster (PR #250)

| Thema | Entscheidung | Datum |
|---|---|---|
| Fenster mittig | Jedes Fenster öffnet immer in der Bildschirmmitte (senkrecht zwischen Zeitstrahl und Betriebsleiste), auch nach dem Verschieben; Verschieben bei offenem Fenster bleibt möglich. **„Fensterlage merken“ (PR #214) entfällt** (Entscheidung des Nutzers); festgehaltene Fenster, die zugleich offen sind, sind um 28 px je Fenster versetzt. Ausnahmen (`Mittig = false`): Auswahl (am Mauszeiger, im Raum rechts), Bau- und Wegtafel (rechts, die Karte bleibt frei). | 29.10.2026 |
| Schrift | Mindestgröße 14 px zentral in `Schriften` (Labels erhalten dazu weiter den Zuschlag von 1); Zeitstrahl-Namen 14 px (Band 20 px, `KopfHoehe` 140). Gleich breite Ziffern waren schon an (`tnum`). | 29.10.2026 |
| Kontrast | Gemessen: die Grautöne (0,60–0,75) erreichen auf dem Tafelgrund 5,9–9,2 : 1; das blasse Aussehen kam von der Größe. `Schriften.LesbareFarbe` sichert 4,5 : 1 für Label-Farben ab. Gesperrte Knöpfe: Schrift 0,62/0,58/0,52. | 29.10.2026 |
| Knöpfe und Schalter | `Oberflaechenstil` im Standard-Thema: Fläche, Rand, Hover, gedrückt und gesperrt; Schalter und Häkchen als gezeichnete Bilder. Die Holzbalken, Bücher und Schriftrollen (spätere Schritte) lösen es ab. | 29.10.2026 |
| Tabellen | `Streifenraster`: jede zweite Zeile hinterlegt, Zeilenabstand 10 px (Produktion, Lager, Rat; Markt und Personal sind Kacheln bzw. Karten ohne Tabelle). | 29.10.2026 |
| Kleine Fehler | F4 Rat: Der Rollbereich lässt Zeitstrahl und Betriebsleiste frei. F7: Marke über dem Band. F5 und F8: Beschriftungen im Raum (siehe Bericht). | 29.10.2026 |

### Entwicklerfenster (PR #254)

| Thema | Entscheidung | Datum |
|---|---|---|
| Auftrag | Wunsch des Nutzers: ein Entwickler- bzw. Cheat-Fenster wie ein Trainer. Es gibt es nur mit dem Haken „Entwickler-Optionen“ (`Entwickler.Aktiv`); ohne Haken fehlen Fenster, Taste F12 und Knopf. Der Kern liefert Befehle, Sperre und Abfragen (`kern.md`, „Entwicklerfenster, Kern“). | 29.10.2026 |
| Fenster | `EntwicklerAnzeige.cs` (Gerüst, Reiter Haus und Zeit, Familie und Ereignisse, Wirtschaft) und `EntwicklerAnzeige.Darstellung.cs` (Wetter und Darstellung, Messwerte): mittiges Fenster wie das Lexikon (`Hauptmenue.Tafel`, `Fensterrahmen`, festhaltbar, Esc schließt es), unten immer die Meldung des letzten Eingriffs (grün, bei Ablehnung rot mit ✗), oben eine Stand-Zeile (Gulden, Stand, AP, Ansehen, Zeit, Sperre, Kennzeichen). Oben rechts wählt „Haus“ das Haus für die Hausbefehle (Standard: das eigene). Verbunden wird es in `Hauptszene.Entwickler.cs`. | 29.10.2026 |
| Befehle | Alles, was die Partie ändert, geht als `EntwicklerBefehl` über `UiHilfe.Befehlen` (Boten-Regel aus M2); die Meldung des Kerns steht im Fenster. Auswahllisten (Haus, Stand, Person, Krankheit, Charakterzug, Heiratskandidat, Betrieb, Ware, Meinung von) füllt `Auffrischen` zweimal je Sekunde aus dem Zustand und behält die Wahl, solange es den Eintrag gibt. „Sterben lassen“ braucht zwei Klicks (drei Sekunden), alles andere wirkt gleich. | 29.10.2026 |
| Zeit | Vorspulen um N Minuten, Stunden oder Jahren, bis zur Uhrzeit, bis Jahresende, bis Ratssitzung über `Simulation.Vorspulen…` (kein Befehl, rechnet im Bild, das Fenster hängt dabei kurz). Dazu Tempoknöpfe 1× bis 64×: **64× ist gefahrlos** – der Taktgeber gibt dabei etwa 80 Ticks je Sekunde aus (höchstens 240 je Bild, `MaxTicksProAufruf`), `Tempo` nimmt jeden Wert über 0, ein Spielstand nimmt es mit; im Klicktest liefen bei 64× zwölf Spielstunden ohne Fehler durch. Die Tempoleiste oben bleibt bei den bekannten Stufen. | 29.10.2026 |
| Sperre und Kennzeichen | `Hauptszene.Entwickler.cs` setzt `Simulation.EntwicklerErlaubt = Entwickler.Aktiv` gleich nach dem Aufbau der Simulation – das gilt für Neubeginn, Laden und Probepartie, weil die Hauptszene für jede Partie neu entsteht. Ladeliste (Hauptmenü und Spielmenü): „[Entwicklerhilfe]“ hinter dem Titel mit Tooltip (`Speicherung.Kopf(json).MitEntwicklerhilfe`); Jahresbericht und Reiter „Jahresrechnung“ des Berichts: ein Satz „Diese Partie lief mit Entwicklerhilfe …“. Ein ⚙-Zeichen ging nicht: Die Schrift hat es nicht. | 29.10.2026 |
| Wetter und Darstellung | Reine Darstellung, nichts wird gespeichert: Wetter und Jahreszeit der Ansicht (auch „Automatisch“) und ein Blitz über `Stadtansicht.Entwickler.cs` – derselbe Weg wie `--wetter` und `--jahreszeit` (`_wetterVorgabe`), die Werte stehen sofort; Dunst und Unschärfe über `Fenster.DunstSetzen` und `UnschaerfeSetzen` mit `speichern: false` (die Einstellungen der Datei bleiben unberührt); Hilfsanzeigen Wegenetz (jetzt auch schaltbar, `--wegenetz=1` nutzt denselben Weg) und Bauzonen (`BauzonenHilfe`, neben dem Baumodus). Die Schalter gleichen sich zweimal je Sekunde mit dem Ist-Stand ab. Ohne Weg und darum weggelassen: ein eigener Nachtdunst (es gibt nur den Morgendunst mit Bodennebel). | 29.10.2026 |
| Messwerte | Aktualisieren sich zweimal je Sekunde: Bildrate, Bildzeit, Zeichenaufrufe, Objekte, Dreiecke (`RenderingServer.GetRenderingInfo`), Lichter im Bild (`Stadtansicht.LichterImBild` zählt alle sichtbaren `Light3D` im Baum, nur alle zwei Sekunden), Knoten, Speicher (Godot, .NET, Grafik, Texturen), Spielzeit und Tempo. Die Prüfsumme (`Zustandspruefsumme.Kurz`) rechnet nur auf Knopfdruck; der KI-Einblick (`KiEinblick.Alle`, Zeilen als Text) beim Öffnen des Reiters und auf Knopfdruck. | 29.10.2026 |
| Startoption | `--entwicklerfenster=1` (mit `--entwickler=1` oder dem Haken) öffnet das Fenster gleich, `=2` bis `=5` auf dem Reiter 2 bis 5. | 29.10.2026 |
| Klicktest | Vulkan (T400): Fenster mit jedem Reiter, Gulden geben (Kopfzeile 4.557 → 5.874 im Lauf, die Meldung im Fenster), Regen und Winter mit Wegenetz und Bauzonen, Bis Ratssitzung (Jahresbericht mit dem Satz zur Entwicklerhilfe, Spielstand mit dem Kennzeichen), Laden und F12 danach (Sperre offen, Gulden geben geht), 64×, Ladeliste mit „[Entwicklerhilfe]“. Kein SCRIPT ERROR. Nach dem Zusammenführen mit M1 und M2 (Integration, PR #254) noch einmal: Reiter 1, Gulden geben (+1.000), Wetter Regen, Messwerte mit Prüfsumme (b83e4195), Speichern und Ladeliste mit „[Entwicklerhilfe]“; ohne `--entwickler=1` kein Knopf und keine Reaktion auf F12. | 29.10.2026 |


### Einstellungen-Umbau (PR #262)

| Thema | Entscheidung | Datum |
|---|---|---|
| Auftrag | Wunsch des Nutzers: Das Fenster ist überladen. Sieben Reiter werden vier (Grafik, Ton, Spiel, Barrierefreiheit), Erklärtexte wandern in Tooltips, teure Optionen bekommen ein Zeichen, ausgeschaltete Schalter werden klar sichtbar, neu: Grafikschnittstelle Vulkan/DirectX 12. Werte und Speicherformat bleiben (alte `einstellungen.cfg` laden unverändert). | 29.10.2026 |
| Aufteilung | **Grafik:** Voreinstellung (mit „Automatisch wählen“ und Vollbild), dann je Gruppe die Hauptregler: Leistung (Bildrate, VSync, FSR 2.2), Bild (Kantenglättung, Schatten), Stimmung (Bloom). Der Knopf **Erweitert** zeigt den Rest in denselben Gruppen: Dynamische Auflösung, Renderauflösung, Natur-Dichte, Passanten, Sichtweite; Boden, Gras, Nachschärfen, Umgebungsverdeckung, indirektes Licht; Bloom-Stärke, Unschärfe, Wolkenschatten, Dunst, Wetter, Farbstimmung; Grafikschnittstelle (Windows). **Ton** wie vorher, jetzt eine Zeile je Bus. **Spiel:** Ratssitzung. **Barrierefreiheit:** Größe, Farbfilter, Tooltips festheften und neu **Fahnen bewegen** (zog von „Stimmung“ um; sein Text nennt schon die Bewegung als Grund). Nichts gestrichen. | 29.10.2026 |
| Erweitert gemerkt | `einstellungen.cfg`, Abschnitt `oberflaeche`, Schlüssel `grafik_erweitert` (Standard zu). `--reiter=GrafikErweitert` öffnet es nur für den Start; die alten Namen `Leistung`, `Bild`, `Stimmung` gehen weiter (= GrafikErweitert), `Voreinstellung` = Grafik. | 29.10.2026 |
| Tooltips | Jede Zeile hat ihren Tooltip an Beschriftung, Feld und Wert. Bei Auswahllisten nennt er alle Stufen, die gewählte mit „»“ und „(gewählt)“. **Reiner Text:** BBCode wie `[b]` zeigt der Tooltip wörtlich (im Klicktest gesehen und zurückgenommen). Die kurzen Zeilen unter Reglern, die sich mit dem Stand ändern (Messergebnis, Stand der dynamischen Auflösung, „FSR glättet selbst“, Grafikschnittstelle), bleiben im Fenster. | 29.10.2026 |
| Leistungszeichen | Eine Flamme (Glut, Hitze) im Holzschnitt-Stil, gezeichnet in `Oberflaechenstil.LeistungsSymbol()` (20×20, Umriss, Schraffur, Handfarbe 0,75 px versetzt), also kein Asset und kein Protokolleintrag. Ein vorhandenes Icon passte nicht (die Waren-Knöpfe sind Waren). Gesetzt bei: Dropdown-Einträgen MSAA 2×/4×/8×, Umgebungsverdeckung Hoch, Gras Sehr dicht, Morgendunst Hoch (Zeichen in der Liste und im geschlossenen Feld) und dem Schalter indirektes Licht. Nicht gesetzt, wo der Text „kostet etwas“ sagt (Bloom, Unschärfe). Der dunkle Umriss geht auf dem dunklen Tafelgrund fast unter; die Flamme liest sich vor allem an der Farbe. | 29.10.2026 |
| Schalter | Aus: Grund (0,33 / 0,27 / 0,20) heller als die Tafel (0,12), Rand fast hell (0,92 / 0,84 / 0,66), Knopf cremeweiß links; an: golden, Knopf rechts. Dazu das Häkchenfeld heller. Ein Klick auf die Beschriftung schaltet auch. | 29.10.2026 |
| Pfeilzeichen | „▸“ und „▾“ fehlen der Schrift der Knöpfe (Kästchen im Klicktest), deshalb steht „Erweitert aufklappen“ bzw. „zuklappen“ im Knopf. | 29.10.2026 |
| Grafikschnittstelle | Nur unter Windows (`--schnittstellezeigen=1` zeigt die Zeile zum Ansehen). `project.godot` bekommt `application/config/project_settings_override="user://grafikschnittstelle.cfg"`; die Einstellung schreibt dorthin `rendering_device/driver.windows`. Belegt mit Godot 4.7.2 (Kopf-Skript ohne Fenster: ohne Datei `vulkan`, mit der Datei `d3d12`) und im Godot-Quelltext (`ProjectSettings::setup` lädt die Datei vor `Main::setup`). Rückfall: Godot selbst (`fallback_to_vulkan`, Grundwert an) bei Startfehler, dazu unser Vermerk gegen spätere Abstürze (`Grafikschnittstelle.cs`, Beschreibung in ARCHITEKTUR). Standard bleibt Vulkan. Nur der Nutzer kann DirectX 12 prüfen: [Testanleitung](../testanleitung-d3d12.md). | 29.10.2026 |

### Ladebildschirm (PR #261)

| Thema | Entscheidung | Datum |
|---|---|---|
| Auftrag | Ladebildschirm für neue Partie, Spielstand und Kartenwerkstatt (Teamleiter, nach der Messung vom 29.10.: alles lief in einem Zug auf dem Hauptthread, das Fenster stand 5 s, die Wohnhäuser kamen erst im zweiten Bild). | 29.10.2026 |
| Aufbau der Tafel | `Ladebildschirm.cs`: oben „Ein Rat für unterwegs“ (Germania One, Gold) und ein Tipp, der alle 7 s wechselt; in der Mitte das Bild 16 : 9 im Holzschnitt-Rahmen (schwarzer Umriss, schmaler Pergamentrand); unten der `Ladebalken`. Grund dunkel wie die Tafeln. Größe folgt dem Fenster. | 29.10.2026 |
| Bild (Platzhalter) | Die Kartenvorschau der Stadt aus `assets/oberflaeche/karten/<kennung>.png` (480 × 270, vergrößert, darum weich), sonst Köln. Gemalte Ladebilder kommen später über die Bild-KI (nicht neben der Arbeit, weil keine KI daneben laufen darf). Kein neues Asset, kein Protokolleintrag. | 29.10.2026 |
| Fortschritt | Pergamentstreifen statt Balken: heller Streifen, oben und unten leicht gewellt, eingerollte Enden, Schraffur im unteren Drittel, die Füllung in rötlicher Handfarbe 3 px neben ihrem Tinten-Umriss; darauf der Abschnitt in Tinte („Wege werden gepflastert …“, „Häuser werden gebaut …“, „Die Bürger ziehen ein …“). Der Balken zeigt, was **vor** dem laufenden Abschnitt fertig ist, und läuft weich nach; die Anteile kommen aus der Messung (Köln). | 29.10.2026 |
| Tipps | 20 Tipps in `game/daten/ladetipps.json`, jeder ein Satz aus dem Spielbuch (Steuerung, Spieltag, Gauner, Rat, Glaube, Familie, Karren, Markt, Wetter, Bauen); Quelle je Tipp geprüft. Neue nur, wenn sie so im Spielbuch stehen. | 29.10.2026 |
| Ablauf | Das Hauptmenü öffnet ihn und wartet zwei Bilder, dann der Szenenwechsel; Hauptszene und Werkstatt öffnen ihn selbst, wenn er fehlt (Startoptionen, Probepartie, Laden im Spiel). Er blockiert die Maus, Eingaben der Szene ruhen, die Zeit steht. Er geht erst, wenn drei Bilder in Folge kürzer als 50 ms sind (höchstens 5 s), dann blendet er in 0,25 s aus. | 29.10.2026 |
| Hauptszene in Abschnitten | `_Ready` baut die Tafeln, dann `PartieAufbauen` (async): Kern, Stadt in Abschnitten, `PartieStarten` (der frühere Rest von `_Ready`: Spielmenü, Ereignisse, Startoptionen), `ErsteBilderAbwarten`. Startoptionen, die eine fertige Stadt brauchen (`--blick`, `--innen`, `--wetter`), wirken dadurch wie bisher. Hauptmenue.cs/Hauptszene.cs: Änderung durch die Außenstelle im Auftrag. | 29.10.2026 |
| Klicktest | `tools/klicktest/lauf.ps1`, Vulkan und D3D12 (RTX 4080), Köln: neue Partie, Spielstand, Kartenwerkstatt je 14 Bilder alle 0,5 s (Balken und Text laufen, Ausblenden, fertiges Bild vollständig mit Wohnhäusern und Nachtlicht), dazu Regen 8 s nach dem Start. Bilder: `C:\Reichsgilde_Austausch\aussenstelle\ladebild\` (`uebersicht-*.png`). | 29.10.2026 |

### Entwicklerfenster: Fehler im Aufbau (PR #269)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ursache | `Hauptszene.EntwicklerBauen` hängte das Fenster ein (`AddChild` ruft `_Ready`, das die Reiter baut) und verband es erst danach mit der Partie. Seit dem Schalter „Nachtnebel“ (PR #266) las der Aufbau `_stadt.Nachtnebel`, `_stadt` war noch leer: `_Ready` brach im Reiter „Wetter und Darstellung“ ab. Messwerte und Klänge fehlten, das Fenster stand beim Start offen (ohne ✕), und `DarstellungAuffrischen` warf zweimal je Sekunde eine `NullReferenceException`. | 30.10.2026 |
| Regel | Verbinden vor dem Einhängen. Der Aufbau eines Reiters liest die Partie nicht; den Stand der Schalter und Listen setzen die Auffrisch-Methoden (beim Öffnen und zweimal je Sekunde). Der Schalter „Nachtnebel“ startet ohne Wahl. | 30.10.2026 |
| Absicherung | `Bauen` baut jeden Reiter für sich: Wirft der Aufbau, steht die Ausnahme einmal im Log, die übrigen Reiter, ✕ und Verstecken laufen weiter. Aufgefrischt werden nur fertig gebaute Reiter (ein Kennzeichen je Reiter); die Messwerte frischt `_Process` selbst auf. | 30.10.2026 |
| Klicktest | Vulkan (T400), `--entwickler=1`, je mit F12: Stadt (alle sechs Reiter, Nachtnebel zeigt „An (neu)“), Raumansicht `--innen=1` (Wetter Regen mit Meldung, Messwerte) und Probepartie der Kartenwerkstatt (Wetter und Darstellung). Fenster beim Start zu, keine Ausnahme des Fensters im Log; auf `main` im selben Ablauf 94 (eine im Aufbau, 93 beim Auffrischen). Gegenprobe mit einem absichtlich eingebauten Fehler im Aufbau: eine Meldung im Log, Fenster beim Start zu, die anderen Reiter und „+1.000“ gehen. | 30.10.2026 |

### Kartenwerkstatt: Pinsel-Paket 1 (PR #279)

| Thema | Entscheidung | Datum |
|---|---|---|
| Tafel | Gelände- und Bodentafel haben die klappbaren Abschnitte „Pinsel“ und „Filter“. Filter: Vorlagen **Überall**, **Ebene ≤ 8 %**, **Hang ≥ 15 %** (Grenzen der Hangsperre), Regler „Steigung ab/bis“ (bis ganz rechts = ohne Grenze), Auswahl „Nur auf“ mit Rand. Eigene Haken „Nur Mulden heben“ (Heben) und „Nur Spitzen kappen“ (Senken). | 31.10. |
| Pipette | Knopf „Pipette“ neben Ziel-/Bezugshöhe und neben dem Namen der Textur (für Einsteiger auffindbar, im Klicktest klickbar), dazu Strg+Klick wie in Terrain3D. Sie gilt für einen Klick und verfällt beim Werkzeugwechsel; die Bodenart-Pipette wählt die Textur wie ein Klick in der Palette. | 31.10. |
| Schild unter dem Pinsel | Kleines Schild rechts unterhalb der Maus (am Bildrand nach innen geklappt), Stil der Werkstatt-Tafeln mit dünnem Goldrand: Höhe des Rasters (wie die Pipette, mit Wegbett, wenn es abweicht), Steigung wie der Filter, bis zu drei Bodenarten mit Anteil, bei Terrassen die Stufe, ob der Filter hier wirkt, ob die Pipette bereit ist. Solange es sichtbar ist, zeigt die Statusleiste nur das Werkzeug (sonst stünden zwei verschiedene Steigungen nebeneinander). | 31.10. |
| Icon | „Terrassen“ aus `tools/icons/kartenwerkstatt_icons.py` (drei Stufen mit Grasnarbe und Weinstöcken); das Skript zeichnet die übrigen Icons Byte für Byte wie vorher. | 31.10. |
| Nebenbei behoben | Schieberegler melden sich bei `TreeEntered` statt `Ready` am Bild-Takt an: Die klappbaren Abschnitte hängen sie um, danach fehlte die Abfrage des Loslassens und beim Schließen kam „disconnect a nonexistent connection“. In der Palette springt beim Klick der vorige Werkzeug-Knopf heraus (vorher blieben z. B. Heben und Senken beide gedrückt). | 31.10. |
| Nach der Prüfung | Eine eingetippte Zielhöhe setzt den Haken „Feste Zielhöhe“ sichtbar; trifft die bereitgestellte Pipette keinen Boden, springt ihr Knopf heraus (Ereignis `WerkstattPipetteGeaendert(genommen)`); die Tooltips zu Mulden/Spitzen sagen genau, was geschieht (höchstens bis Mitte ± Stärke, auf ebenem Boden wie gewohnt); ist der Steigungsbereich schmaler als 4 Prozentpunkte, warnt die Tafel rot. | 31.10. |
| Klicktest | Vulkan (T400), neue Karte mit Hügel aus der Startoption: Terrassen (4-m-Stufen, Fels an den Kanten), Ebnen mit Pipette (Bank auf genau 9,78 m), nur Mulden (−4,00 → −1,00 m, Ebene bleibt 0,00), nur Spitzen (Kuppe gekappt), Fels nur ab 15 % (Kuppe und Ebene bleiben Wiese), Moos nur auf Fels, Bodenart-Pipette. Bilder im Bericht. | 31.10. |

### Kartenwerkstatt: Felspinsel F4 (PR #284)

| Thema | Entscheidung | Datum |
|---|---|---|
| Platz | Keine neue Gruppe (die Tasten 1–0 sind belegt): Abschnitt „Felsen“ in der Palette **Gelände** (2) mit den Stempeln Felswand, Felskuppe, Felsrippe, Felsstück, dazu „Felsen streuen“ und „Fels-Radierer“. Symbole als Farbfeld in der Farbe des Grundgesteins (wie die Bodentexturen), der Radierer mit dem vorhandenen Icon – kein neues Asset. | 01.11. |
| Tafel | Klappbare Abschnitte „Vorlage“ (Gruppe oder Einzelstücke mit Haken je Form, Gestein der neuen Stücke mit „wie die Karte“, Grundgestein der Karte als Befehl mit Rückgängig), „Pinsel“ (Radius und Dichte nur beim Streuen, Größe, Streuung, Einsinken, Neigung, „Schauseite hangabwärts“) und „Filter“ (derselbe Abschnitt wie Gelände und Boden). Radierer nur Radius und Filter. Tooltips an jedem Regler. | 01.11. |
| Vorschau | Streuen und Radieren: Kreis an der Maus (rot beim Radierer). Stempel: die Stücke selbst als helle Geister (höchstens so viele Netze wie die größte Gruppe, wiederverwendet) und je Stück ein Kreis der Grundfläche – gerechnet vom Kern (`FelsStempelVorschau`) mit dem Startwert, den auch der Klick nimmt; „Neu würfeln“ zählt ihn weiter. Das Schild an der Maus (Höhe, Steigung, Filter) gilt auch für den Felspinsel. | 01.11. |
| Anzeige | VORLÄUFIG bis F5: je Stück ein MeshInstance3D (Stufe 1 aus `natur_felsen.glb`, Fels-Material aus F2 je Gesteinsart geteilt), nur in der Werkstatt. Lage: Drehung, Neigung zur Hangnormalen, Kippen, Einsinken unter den Boden unter der Mitte; folgt Geländeänderungen. Kein Wählen einzelner Stücke (nur Radierer). | 01.11. |
| Startoption | `--werkstatt-fels=grundgestein:…|stempel:…|streuen:…|radierer:…` und die Werkzeuge `fels:<wand|kuppe|rippe|einzeln|streuen|radierer>` für `--werkstatt-werkzeug`/`--werkstatt-umriss` (STARTOPTIONEN.md). | 01.11. |
| Dichte | Vorgabe beim Streuen 20 % für Einzelstücke, 10 % für Gruppen (Gruppen sind groß und standen mit 30 % zu dicht); die Werkstatt merkt sich beide getrennt. | 01.11. |
| Klicktest | Vulkan (T400), neue Karte mit Hügel: Wand gestempelt (Schauseite hangabwärts, Geist der nächsten Wand), Rippen gestreut, radiert, Rückgängig; mit echter Maus Kuppe gestempelt, zweiter Stempel, Strg+Z nimmt ihn zurück. | 01.11. |

### Schilder und Ampeln des Fahrwegs (PR #282)

| Thema | Entscheidung | Datum |
|---|---|---|
| Zeichnung | `FahrwegAnzeige` (Control unter allen Tafeln) zeichnet im Holzschnitt-Stil: Papier um 2 px neben dem schwarzen Rahmen, Schraffur unten und rechts, Pfosten zum Halt am Markt; Siegel als Farbmarke. Gezeichnet statt Asset (kein Protokolleintrag nötig). | 01.11.2026 |
| Schild | Name des Platzes, „Startkarren: hin … · zurück … min“, darunter der schnellste Karren (die Karrenart mit der kürzesten Hinfahrt). Bester Platz mit Goldrand, die anderen blasser; ein Platz außerhalb des Bildes: Schild am Rand mit Spitze zu ihm. | 01.11.2026 |
| Ampel | Kleines Siegel in der Stufenfarbe, darunter die Minuten nur bis Zoom 150 (weiter draußen würden die Zahlen das Bild überladen). | 01.11.2026 |
| Tafeln | Bautafel: Zeile „Fahrt zum Markt: … min (Platz)“ in der Stufenfarbe und eine Legende der Ampeln; Rückfrage: dieselbe Zeile in der Rechnung. Tooltip: hin und zurück, schnellster Karren, Stufen, Zahl der Plätze. | 01.11.2026 |

### Bautafel rechts und Werkstatt-Bauplätze (PR #285)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ursache Bildmitte | `Fensterrahmen.Anmelden` ruft `_Ready` des Rahmens sofort auf; bei sichtbarer Tafel setzte `BeimZeigen` schon die Mitte (`_platz = MitteLage`), bevor der Aufrufer `Mittig = false` setzen konnte. Bau- und Wegtafel blieben so in der Mitte (die Auswahl nicht, sie setzt bei jedem Öffnen `PlatzSetzen`). Jetzt nimmt `Mittig = false` eine schon gesetzte Mitte zurück | 02.11.2026 |
| Bautafel | Öffnet immer rechts unter dem Zeitstrahl (`PlatzSetzen` wie die Auswahl, auch nach dem Ziehen); die Höhe hält Zeitstrahl und Betriebsleiste frei (`RechtsAnheften`) | 02.11.2026 |
| Bauliste | Die Zeile „keine Ampeln → return“ aus #282 stand in `ListeZeigen` statt nur in `FahrwegZeigen`: Bei offener Liste gibt es nie Ampeln, Legende und alle Gebäudeknöpfe fehlten. Entfernt (Befund der Prüfung) | 02.11.2026 |
| Werkstatt-Bauplatz | Tooltip an der Maus über einer Zone mit `"nur": "werkstatt"` („Nur Werkstätten …“), Zeile in „Platz wählen“, Legende „Rand mit Amboss: nur Werkstätten“ nur auf Karten mit solchen Zonen; kein Tooltip bei offener Rückfrage, die graue Zeile nur bei grünem Umriss | 02.11.2026 |

### Sitzplätze in der Schänke (PR #287)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anzeige | Betriebsverwaltung (`GaststubeAktualisieren`) unter „Erwartet ≈ … Gäste“: „Plätze: belegt / frei (von n)“ aus `Gastwirtschaft.Platzbelegung` (Schätzung der laufenden Stunde), dahinter „heute kein Platz: n“ aus `Gaestebuch.Voll` nur, wenn größer 0, mit „(davon m ohne Platz in der ganzen Stadt)“ aus `Ausgefallen`. Nur Gäste-Plätze, keine Einwohner-Grenze (greift mit den Spieldaten nie). Tooltip in einfacher Sprache am Gästetext | 02.11.2026 |

### Mauszeiger (PR #308)

| Thema | Entscheidung | Datum |
|---|---|---|
| Zeiger | Lederhandschuh aus Blender (`tools/modelle/handschuh.py`), feine Pixel, 6 Stufen nach `Konto.Stand`, je „normal“ und „klick“ (wärmer), dazu Bronze-Türschloss mit S-Griff; 32/48/64 Pixel nach Fensterhöhe und Oberflächengröße (Standard 48 bei 1080p). Freigabe des Nutzers. | 04.11.2026 |
| Zustände | „klick“ über Gebäude, Figuren, Bauplatz im Baumodus und freigegebenen Knöpfen; Schloss über Gebäuden mit Innenraum (`HatInnenansicht`), nur außerhalb von Räumen. Karren sind nicht einzeln wählbar, darum ohne eigenen Zustand. | 04.11.2026 |
| Einstellung | „Eigener Mauszeiger“ (Barrierefreiheit, Standard an) mit Tooltip; Startoptionen `--zeiger`, `--zeigerstufe`. Hauptmenü und Kartenwerkstatt zeigen Stufe 1; die Werkstatt hat keinen eigenen Pinsel-Zeiger. | 04.11.2026 |

