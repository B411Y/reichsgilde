# Bereich Oberfläche – Offen und Entscheidungen

**Nur der Oberflächen-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- Aufklappmenüs (`OptionButton`) erscheinen im Klicktest unter Xvfb/opengl3 nicht auf dem Bildschirmfoto (auch nicht in den Einstellungen, Kantenglättung): Auswahlen lassen sich dort nur per Code prüfen; zu klären, ob es am Rechner oder am Thema liegt
- (von Kern) Oberfläche im Holzschnitt-Stil (Entscheidung in `kern.md`, „Grafikstil Holzschnitt“): Tafeln, Fenster, Rahmen, Knöpfe, Schmuck und das Hauptmenü nach und nach umstellen – Umriss, Schraffur, kolorierte Flächen, Papier- oder Pergamentgrund. Die Waren-Icons sind der erste Schritt (PR #113; neue Marktplatz-Icons macht der Nutzer gerade mit Gemini).
- (von Kern) Die Nachfrage der Stadt wächst seit PR #177 mit den Einwohnern (`Marktstand.Grundnachfrage`, `Marktstand.Nachfrage`, `Stadtwirtschaft.Einwohner`); das Marktfenster (`MarktAnzeige.cs`) zeigt noch die Nachfrage aus den Daten (`daten.Nachfrage`). Vorschlag: `Marktstand.Nachfrage` zeigen, dazu die Einwohner der Stadt.
- Kartenwerkstatt, Belag (Schritt 5d): Der Abschnitt wird mit zwei Reglern gewählt (goldenes Band im Bild), nicht durch Ziehen mit der Maus am Weg. Wenn das im Gebrauch zu umständlich ist, ein Ziehen im Bild nachrüsten.
- Grundstruktur (PR #197): Die Reiter von Wohnhaus und Bericht sind je Seite verschieden hoch (die Tafel springt beim Wechsel); bei der Gestaltung im Holzschnitt-Stil eine feste Höhe wählen
- Stadtkasse (PR #200): Strafmaß des Stadtrichters und „Freilassen“ des Gefängniswärters sind im Klicktest nur ohne Verfahren bzw. Gefangenen geprüft (der Text „Zurzeit …“); für einen vollen Klicktest fehlt eine Startoption, die ein Verfahren zwischen zwei KI-Häusern bzw. einen Gefangenen anlegt (bräuchte eine Testhilfe im Kern).
- Porträts (PR #211): Ratsherren der Stadt sind keine Personen im Familienbuch und haben in `wirtschaft.json` weder Geschlecht noch Aussehen; die Oberfläche leitet Gesicht, Haar und Augen aus Namen und Volk ab und das Geschlecht aus dem Vornamen (`PortraetAnzeige.ZeigenRatsherr`). (von Oberfläche an Kern) Besser: `geschlecht` und `aussehen` je Ratsherr in den Daten – dann fällt die Ableitung weg. Außerdem: Maskenweg (`…_haar.png`, `…_augen.png`) steht noch nicht fest, die Anzeige sucht erst ohne, dann mit Kleidung im Namen.
- Lexikon (PR #214): Die Texte sind eine Erstfüllung aus dem Spielbuch und gehören einmal vom Nutzer gelesen; Zahlen, die nicht in `wirtschaft.json` stehen (Arbeitszeit 8–20 Uhr, Messe um 10 Uhr, Strafmaße der Kirche und des Gerichts), stehen noch fest im Text. Weitere Platzhalter brauchen öffentliche Abfragen des Kerns (Arbeitszeit, Messezeit, Ablass, Wucherschwelle, Pfändung).
- Porträts (PR #211): Nachlieferung vom Grafik-PC – Orks und Oger (bis dahin Schatten mit Fragezeichen), Halblinge hf4 (alter weißer Saum) und hf5, neue Haarmasken für Elben, Halblinge und Zwerginnen (die erste Fassung malte bartlosen Figuren einen Bart an; bis dahin keine Haar-Umfärbung bei ihnen). Skript `haar_neu.sh` im Notizordner des Teamleiters.

## Entscheidungen

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

### Waren-Knöpfe (PR #210)

| Thema | Entscheidung | Datum |
|---|---|---|
| Stil | Entscheidung des Nutzers nach Proben von Gemini: Waren-Icons als quadratische Knöpfe (Aufbau wie Befehlsknöpfe klassischer Strategiespiele, ohne Elemente eines bestimmten Spiels): Motiv groß, gemalter dunkler Hintergrund, schmaler Rahmen mit Licht oben links; Malstil des Motivs wie die Proben (kolorierter Holzschnitt mit Tusche-Umriss, Schraffur und Wasserfarbe). Ersetzt die Pixel-Icons; der Entwurf PR #113 (Python-Zeichenskript) wird geschlossen. | 26.10.2026 |
| Rahmen nach Stufe | Der Rahmen zeigt die Stufe, ab der eine Ware hergestellt wird: Holz = Rohstoff (ohne Stufe), Stein = Stufe 1, Silber = Stufe 2, Gold = Stufe 3. | 26.10.2026 |
| Hintergrund nach Beruf | Schmied Glutrot, Alchimist Smaragdgrün, Parfumeur Purpur, Prediger Nachtblau, Wirt Bernstein, Tischler Ockerbraun, Steinmetz Schiefergrau-blau, Rohstoffe Lehmbraun. | 26.10.2026 |
| Bau | Zusammengesetzt: Gemini (Antigravity, Bild-Subagent) malt vier leere Rahmen, acht Hintergründe und 63 Motive auf Weiß; ein Skript stellt die Motive frei und setzt Hintergrund, Motiv und Rahmen je Ware zu einem Knopf zusammen. Jedes Teil ist einzeln austauschbar. Grafiken mit Gemini erzeugt, im PR gekennzeichnet. Erst Rahmen, Hintergründe und 8 Motive zur Abnahme durch den Nutzer, dann der Rest. | 26.10.2026 |
| Umsetzung | Rahmen **7 %** der Kantenlänge (Fassung A), Motiv mit Lichthof (Mitte +35 %, Ecken −15 %), warmweißem Saum (60 %, bei hellem Motivrand bis 25 %) und Schlagschatten. Skript `tools/icons/knoepfe_bauen.gd` (Befehle `quellen`, `bauen`, `tafel`, Schalter `--rahmen=`, `--ohne-effekte`); `WarenIcons` zeigt 128 px linear mit Mipmaps. **Grafiken mit Gemini erzeugt** (Rahmen, Hintergründe, Motive; Modell gemini-3.1-flash-image über die API, 63 Motive). Neue Ware: Motiv erzeugen (kolorierter Holzschnitt auf Weiß), `quellen` ausführen oder als PNG in `tools/icons/quellen/motiv/<ware>.png` ablegen, dann `bauen`. | 26.10.2026 |

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
| Verweise | `[[kennung]]` oder `[[kennung\|Anzeigetext]]` im Tooltip-Text; nur mit Treffer von `Tooltips.BegriffSuchen` (vom Lexikon gesetzt) unterstrichen, sonst einfacher Text. Verweilen (0,35 s) öffnet den Begriff, Klick heftet ihn fest; beliebig tief. Im festen Tooltip eines Begriffs: Knopf „Im Lexikon“ (`Tooltips.LexikonOeffnen`). | 27.10.2026 |
| Fenster | Alle Tafeln mit `Fensterrahmen` verschiebbar (oberer Rand), ✕, Lage gemerkt (geklemmt in den sichtbaren Bereich), Klick = nach vorn. Esc schließt das oberste, X alle. Nur ziehbar, ohne ✕ und Esc: Bautafel und Bau-Rückfrage (haben ihre eigenen Schritte). Starr: Spielmenü, Einstellungen, Hilfe, Jahresrechnung, Ratssitzung, Kartenwerkstatt. | 27.10.2026 |
| Mehrere offen | Neuer Knopf „Festhalten“ (Nadel) bei Rat, Familie, Gericht, Hinterzimmer, Markt, Wohnhaus, Bericht, Hafen: Das Fenster bleibt, wenn eine andere Tafel „alles andere schließt“. Vor der Ratssitzung werden auch festgehaltene geschlossen. | 27.10.2026 |
| Eigene ×-Knöpfe | Bericht, Hafen und Wohnhaus hatten ein eigenes ×; sie sind durch das ✕ des Rahmens ersetzt (Wohnhaus ruft weiter `Geschlossen`). | 27.10.2026 |
| Automatische Verweise | Die vorhandenen Tooltips haben keine `[[…]]`: Der Haken `Tooltips.VerweiseErgaenzen` (von der Hauptszene auf `Lexikon.VerweiseEinsetzen` gesetzt) macht beim Aufbau des Tooltips aus dem ersten Vorkommen jedes Lexikon-Titels einen Verweis (ganzes Wort, Schreibung wie im Titel, Endungen -s/-n/-en/-e/-es, dazu das optionale Feld `auch` in `lexikon.json`; längere Titel zuerst, höchstens 6, nie in vorhandenem `[[…]]`, nie der Begriff in seinem eigenen Tooltip). Prüfung: Startoption `--verweistest=1` schreibt drei Beispielsätze mit Ergebnis ins Log. | 27.10.2026 |
