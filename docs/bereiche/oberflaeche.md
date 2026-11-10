# Bereich Oberfläche – Offen und Entscheidungen

> **Archiv:** Ältere und abgelöste Entscheidungen (bis 26.10.2026) stehen in [archiv/oberflaeche.md](archiv/oberflaeche.md). Zum Arbeiten genügt diese Datei; das Archiv nur bei Bedarf lesen.

**Nur der Oberflächen-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #322)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- Aufklappmenüs (`OptionButton`) erscheinen im Klicktest unter Xvfb/opengl3 nicht auf dem Bildschirmfoto (auch nicht in den Einstellungen, Kantenglättung): Auswahlen lassen sich dort nur per Code prüfen; zu klären, ob es am Rechner oder am Thema liegt
- (von Kern) Oberfläche im Holzschnitt-Stil (Entscheidung in `kern.md`, „Grafikstil Holzschnitt“): Tafeln, Fenster, Rahmen, Knöpfe, Schmuck und das Hauptmenü nach und nach umstellen – Umriss, Schraffur, kolorierte Flächen, Papier- oder Pergamentgrund. Die Waren-Icons sind der erste Schritt (PR #113; neue Marktplatz-Icons macht der Nutzer gerade mit Gemini).
- (von Kern) Die Nachfrage der Stadt wächst seit PR #177 mit den Einwohnern (`Marktstand.Grundnachfrage`, `Marktstand.Nachfrage`, `Stadtwirtschaft.Einwohner`); das Marktfenster (`MarktAnzeige.cs`) zeigt noch die Nachfrage aus den Daten (`daten.Nachfrage`). Vorschlag: `Marktstand.Nachfrage` zeigen, dazu die Einwohner der Stadt.
- Kartenwerkstatt, Belag (Schritt 5d): Der Abschnitt wird mit zwei Reglern gewählt (goldenes Band im Bild), nicht durch Ziehen mit der Maus am Weg. Wenn das im Gebrauch zu umständlich ist, ein Ziehen im Bild nachrüsten.
- Grundstruktur (PR #197): Die Reiter von Wohnhaus und Bericht sind je Seite verschieden hoch (die Tafel springt beim Wechsel); bei der Gestaltung im Holzschnitt-Stil eine feste Höhe wählen
- Stadtkasse (PR #200): Strafmaß des Stadtrichters und „Freilassen“ des Gefängniswärters sind im Klicktest nur ohne Verfahren bzw. Gefangenen geprüft (der Text „Zurzeit …“); für einen vollen Klicktest fehlt eine Startoption, die ein Verfahren zwischen zwei KI-Häusern bzw. einen Gefangenen anlegt (bräuchte eine Testhilfe im Kern).
- Lexikon (PR #214): Die Texte sind eine Erstfüllung aus dem Spielbuch und gehören einmal vom Nutzer gelesen; Zahlen, die nicht in `wirtschaft.json` stehen (Arbeitszeit 8–20 Uhr, Messe um 10 Uhr, Strafmaße der Kirche und des Gerichts), stehen noch fest im Text. Weitere Platzhalter brauchen öffentliche Abfragen des Kerns (Arbeitszeit, Messezeit, Ablass, Wucherschwelle, Pfändung).
- Porträts (PR #211): Nachlieferung vom Grafik-PC – Orks und Oger (bis dahin Schatten mit Fragezeichen), Halblinge hf4 (alter weißer Saum) und hf5, neue Haarmasken für Elben, Halblinge und Zwerginnen (die erste Fassung malte bartlosen Figuren einen Bart an; bis dahin keine Haar-Umfärbung bei ihnen). Skript `haar_neu.sh` im Notizordner des Teamleiters.
- (von kern) Erbgut (Charakter-Editor, Phase 1): Knopf „Erbgut neu würfeln“ im Entwicklerfenster bei der Person (wie „Geben“/„Entfernen“ beim Zug) – der Befehl `EntwicklerErbgut(Person)` steht im Kern bereit.
- DirectX 12 im Export: Godot legt `D3D12Core.dll` und `d3d12SDKLayers.dll` nur bei, wenn `application/export_d3d12` der Windows-Vorgabe auf „Yes“ steht oder `project.godot` selbst DirectX 12 nennt (Quelltext 4.5; für 4.7.2 nicht einzeln geprüft). Empfehlung: `application/export_d3d12=1` in `game/export_presets.cfg` (geschützte Datei, der Nutzer entscheidet).
- Kartenwerkstatt mit `--werkstatt-ausprobieren=<Beruf>`: Die Probepartie startet, während `Kartenwerkstatt._Ready` noch in `ErsteBilderAbwarten` wartet; danach greift es auf die schon freigegebene Werkstatt zu (`ObjectDisposedException` bei `GetTree()`, Kartenwerkstatt.cs:192, einmal im Log). Nur über die Startoption, die Probepartie läuft trotzdem. Vorschlag: nach `StartoptionenAusfuehren` abbrechen, wenn die Werkstatt nicht mehr im Baum ist.
- (von Kern) Mehrere Marktplätze je Stadt (Köln: Heumarkt, Alter Markt, Neumarkt; Branch phase5/gehtempo): Ein Stadtmarkt mit gemeinsamen Preisen – das Marktfenster gilt für alle Plätze. Karrentafel und Handel von Hand könnten nennen, zu welchem Platz ein Karren fährt (`Stadtwirtschaft.MarktplatzVon(betrieb)?.Name`); die Ankunft (`AnkunftAmMarkt(karren)`) rechnet schon mit dem Weg dorthin.
- Schänke: „Plätze: belegt / frei“ und „heute kein Platz“ auch auf die Infotafel im Gastraum (steht bisher nur in der Betriebsverwaltung).
- (von Kern) Jahreszeiten mit Folgen (Datenversion 69, `Stadtwirtschaft.Jahreszeiten`): Die Meldungen kommen schon (Jahresbeginn, Festmarkt, „Der Rhein ist zugefroren!“, Eisgang), die Hafentafel zeigt den Grund aus `AuftragVerboten`. Möglich: im Zeitstrahl ein Zeichen für Eis (`FlussZugefroren`, `EisDroht`) und Festmarkt (`FestmarktJetzt`); in der Markttafel bei der Nachfrage den Faktor der Jahreszeit (`Marktstand.Jahreszeitfaktor`, `Angebotsfaktor`); im Tooltip eines Betriebs im Umland die Leistung der Jahreszeit (`Arbeitsfaktor`); in der Wegtafel das Tempo mit Schlamm (`Schlamm(Belagstufe)`).
- (von Kern) Hafentafel und Schiffsverkehr (`schiffshandel.verkehrBeachten`, Datenversion 69): Der erwartete Gewinn aus `Kalkulation.Schiffsfahrt`/`BesteSchiffsfahrt` rechnet jetzt mit den Schiffen, die schon zur selben Stadt bzw. heim unterwegs sind; Tooltip („zu heutigen Preisen“) und die Preiszeilen „dort/hier“ zeigen den Preis ohne sie. Vorschlag: eine Zeile im Tooltip, z. B. „mit den Schiffen, die schon nach Weidenmünd unterwegs sind bzw. heimkommen“ (eine öffentliche Abfrage der Stückzahlen baut der Kern auf Wunsch).
- Benachrichtigungen Stufe 2 (kern + oberflaeche): Der Kern meldet strukturierte Ereignisse (Art, Betroffener, Ort, Familienmitglied); dann fallen `game/daten/meldungen.json` und die Textmuster weg, Briefe bekommen Porträts und Sprung zu Haus oder Person (Krankheit des Oberhaupts als Brief statt Siegel, die Spitzel- und Unterwelt-Filter ohne Textsuche), und der Verlauf kann mit dem Spielstand gespeichert werden (Datenversion). Stufe 3: Entscheidungen mit Wahl, im Mehrspieler als Frist.
- Benachrichtigungen: Ton von `siegel_neu_*` und `pergament_*` ist rechnerisch erzeugt und noch nicht abgehört (Hörurteil des Nutzers); ein Siegel-Tooltip zeigt nur die neueste Meldung; `Meldungsmessung.Einordnen` (Test) und `Meldungsordnung` führen noch dieselben Textmuster doppelt (die Messung könnte die neue Klasse nutzen).
- Benachrichtigungen: Das ✕ des Fensterrahmens ist hell und bekommt im Brief eine dunkle Platte; ein gemeinsames Pergament-Fenster für andere Tafeln (Oberflächen-Stil Schriftrolle) fehlt noch.
- (Werkstatt-Pinsel) Pinselmessung (`Kartenwerkstatt.Pinselmessung.cs`, `Rest()`): Die Spalte „übriges Bild (Godot, Leerlauf)“ zieht die Summe aller Teilschritte ab, manche stecken aber ineinander („  Nacharbeit: …“ in „Nacharbeit nach dem Strich“, „  Gras: …“ in „im Bild: Grasteppich“) und zählen doppelt; die Spalte ist dann zu klein und kann negativ werden. Betrifft nur das Messwerkzeug.
- (Stress 3b) Klicktest des Stressbalkens auf der NAS steht aus (Startoption `--entwickler=1 --stress=75 --charakter=oberhaupt`; Bilder: Balken angespannt und zermürbt, Tooltip mit Einträgen, fremde Person „Wirkt …“, Aktivitätenliste mit Stresszeile, Entwicklerfenster „Stress setzen“, Zusammenbruch-Meldung).
- (Geheimnisse 3c) Bilder je Art (Ehebruch, Ketzerei, Schulden, Mord) als kleine Zeichen vor der Zeile im Charakterfenster und ein Siegel für die Meldungen (Gerede, Beichte, „Unfriede im Haus“) – bis dahin nur Text; ein Bild bräuchte nur eine Zeile in `GeheimnisZeile` (wie `ZugIcon` mit Rückfall auf Text). Dazu der Gerichtssaal: Anzeigen mit Geheimnis stehen als gewöhnliches Verfahren in der Liste („wegen Ehebruch“ über `Stadtgericht.Anklagepunkte(v)`, die Prognose rechnet das Geheimnis mit), ein Verfahren gegen einen Ratsherrn erscheint dort nicht (nur als Meldung und im Gerichtstag-Protokoll) – ein eigener Abschnitt „Anzeigen gegen Ratsherren“ (`Geheimniswesen.RatsherrVerfahren`, `Ueberzeugung(richter, v)`) wäre klein.
- (Geheimnisse 3c) Klicktest auf der NAS steht aus (Schritte im Bericht des PR): `--entwickler=1 --geheimnis=Ehebruch:rivale --charakter=rivale`, `--geheimnis=Ketzerei:ratsherr --charakter=ratsherr`, `--geheimnis=Schulden:oberhaupt --charakter=oberhaupt`; Godot legt beim ersten Öffnen `CharakterAnzeige.Geheimnisse.cs.uid` an (mit einchecken).
- (von Spiel-KI) Aufschlag am Hafen (Datenversion 80): Verkauft ein Karren am offenen Hafen Ausfuhrware, bringt sie `Schiffahrt.Daten.HafenAufschlag` (3 %) mehr (`Schiffahrt.HafenVerkaufsFaktor(ware)`, Liste über `SchiffshandelDaten.IstAusfuhrware`). Hafentafel (`HafenAnzeige.cs` um Zeile 193) und Hinweis in `Hauptszene.Hafen.cs` (um Zeile 117) sagen noch „dieselben Preise wie auf dem Marktplatz“ – Wunsch: Satz „Karren erzielen hier für Ausfuhrware 3 % mehr (nicht im Winter)“ mit Tooltip der Ausfuhrwaren; die Vorschau „verkaufen ≈“ im Marktfenster rechnet ohne Aufschlag.

## Entscheidungen

### Holzschnitt-Platzhalter im Ladebildschirm (PR #…, ersetzt durch das Köln-Bild, siehe unten)

| Thema | Entscheidung | Datum |
|---|---|---|
| Bilder | Acht KI-Holzschnitte (Qwen-Image 2512 mit eigener Holzschnitt-LoRA, 1024 × 1024) unter `game/assets/oberflaeche/ladebilder/` als WebP (Qualität 0,92, je rund 0,5 MB): schmied, markt, zimmerleute, mahlzeit, kaufmann, stadtansicht, ochsenkarren, prediger. **Platzhalter**, werden später durch eigene Bilder ersetzt. LoRA und Trainingsdatensatz liegen nicht im Repository. Protokoll: acht Zeilen in `docs/assets/protokoll.csv`, Prompts und Seeds in `docs/assets/prompts/ladebilder.md`. | 31.10.2026 |
| Wahl | Zufällig je Ladevorgang, nie dasselbe wie beim vorigen (`_letztesBild`, statisch). Fehlt die Datei, die Kartenvorschau wie zuvor. | 31.10.2026 |
| Darstellung | Das Bild bleibt quadratisch (1 : 1, nie verzerrt), so groß es über Tipp und Balken passt (240–760 px); Holzrahmen (schwarzer Umriss) mit Pergamentblatt darin. Tipp und Balken sind etwas breiter als das Bild. Die Kartenvorschau ist **ersetzt, nicht kombiniert**: Sie ist kein Holzschnitt, 480 × 270 und kleiner als das Bild; die Stadt sieht der Spieler gleich danach. | 31.10.2026 |

### Globale Beleuchtung (SDFGI) in den Einstellungen (PR #…)

| Thema | Entscheidung | Datum |
|---|---|---|
| Platz | Grafik → Erweitert → „Bild (erweitert)“, direkt unter „Indirektes Licht (SSIL)“: Beide sind zurückgeworfenes Licht. Auswahlliste „Globale Beleuchtung (SDFGI)“ mit Aus (empfohlen), Niedrig, Mittel, Hoch; Flammenzeichen an allen Stufen außer Aus. | 30.10.2026 |
| Tooltip | Vorwort ehrlich: zum Ausprobieren, Licht von Fenstern, Glut und Lampen fällt weich zurück, kostet viel Grafikleistung, wo es gilt, stellen die zwei Schalter darunter ein, kurzes Flackern nach dem Umschalten und beim Wechsel Stadt ↔ Raum; dann jede Stufe wie bei den anderen Listen (Zellgröße in den Räumen je Stufe). Zahlen mit geschütztem Leerzeichen („40 %“ bricht nicht um; Alegreya hat das Zeichen). | 30.10.2026 |
| Ohne Forward+ | Unter `opengl3` (Kompatibilität) grau (`Disabled`), der Tooltip nennt den Grund („nur Vulkan und DirectX 12“); die Wahl bleibt gespeichert und wirkt wieder unter Vulkan. Geprüft über `RenderingServer.GetCurrentRenderingMethod() == "forward_plus"` (`Fenster.GlobaleBeleuchtungMoeglich`). | 30.10.2026 |
| Voreinstellung | Gehört zur `Grafikstufe` (in allen Stufen Aus): Wer es einschaltet, sieht danach „Eigene“; eine Voreinstellung wählen schaltet es wieder aus. Ohne Forward+ zählt eine gespeicherte Stufe beim Vergleich als Aus (sonst stünde unter `opengl3` „Eigene“ für etwas, das nicht wirkt). | 30.10.2026 |
| Klicktest | Vulkan (T400): Zeile unter „Indirektes Licht (SSIL)“, Liste mit Flammenzeichen an Niedrig, Mittel, Hoch, Tooltip mit Vorwort und Stufen (heftet sich wie die anderen fest), Auswahl „Mittel“ wirkt sofort und wird gespeichert (beim ersten Einschalten 5–10 s Standbild, siehe `darstellung.md`). `opengl3`: Zeile grau. | 30.10.2026 |
| Unterschalter (Entscheidung des Nutzers) | „Schaltet man SDFGI ein, kommen zwei Schalter, einer für die Außen- und einer für die Innenansicht; bei der Außenansicht der Hinweis, dass sie experimentell ist.“ Unter der Auswahl zwei eingerückte Schalter: **Innenräume** (Standard an) und **Außenansicht** (Standard aus, Flammenzeichen) mit dem grauen Hinweis „– experimentell“ dahinter. Sichtbar, solange die Stufe nicht Aus ist (`Auffrischen` schaltet die ganze Zeile), unter `opengl3` grau. Gespeichert als `sdfgi_innen` und `sdfgi_aussen`; eine Voreinstellung lässt sie stehen (sie zählen nur, wenn eine Stufe gewählt ist). | 30.10.2026 |
| Einrückung | `Zeile` und `Schalter` nehmen einen `einzug` (18 px): ein Abstandhalter vor der Beschriftung, die Beschriftung um ebenso viel schmaler, Schalter und Felder bleiben in ihrer Spalte. „Außenansicht – experimentell“ als Beschriftung passte eingerückt nicht in die 182 px und schob den Schalter 17 px heraus; darum steht „– experimentell“ als Hinweis hinter Schalter und Flamme. Der Klick auf die Beschriftung sucht jetzt das erste `Label` der Zeile (nicht mehr Kind 0). | 30.10.2026 |
| Tooltips der Unterschalter | Innenräume: was es tut (weiches Licht von Fenster, Glut, Lampe, dunklere Ecken, Glut warm), Räume etwa so hell wie ohne, Kosten (schwache Karte bei Mittel gut 70 % mehr Zeit je Bild, gemessen in `darstellung.md`), Standard an. Außenansicht: experimentell, nur grobe Felder von 5 m wegen der Kamerahöhe, kaum sichtbar, kühlere Schatten, Flecken auf großen Erdflächen, kostet trotzdem (rund 8 %, Hoch 17 %), neue Häuser und Fensterlicht kommen erst beim Verschieben nach, Standard aus. | 30.10.2026 |
| Klicktest Unterschalter | Vulkan (T400, Greybox, `--einstellungen=1 --reiter=GrafikErweitert --sdfgi=2`): beide Zeilen unter „Globale Beleuchtung (SDFGI): Mittel“, eingerückt, Schalter in der Spalte von „Indirektes Licht“, Innenräume golden (an), Außenansicht hell (aus) mit Flamme und „– experimentell“; beide Tooltips nach 12 s Hover (heften sich fest). Umschalten in der Partie (`--innen=1`, Einstellungen über der Schmiede): Klick auf „Innenräume“ schaltet SDFGI im Raum dahinter sofort aus, ein zweiter Klick wieder an, ohne Neustart. Bilder: Scratchpad `sdfgi2/ui/` (`l2/`, `umschalten/`). | 30.10.2026 |
| Tooltips nach der Prüfung (von Darstellung) | Niedrig nennt die leicht treppigen Kanten (halbe Auflösung), Mittel „auf voller Auflösung (glatte Kanten)“, Hoch „viermal so viele Strahlen wie Niedrig“. „Innenräume“: die Räume bleiben in jeder Stufe etwa so hell wie ohne; Kosten auf einer schwachen Karte rund 30 / 40 % (in Wohnhaus und Rathaus bis 75 %) / mehr als doppelt. „Außenansicht“: rund 7 % bei Niedrig, 15 % bei Mittel und Hoch. Messung: `docs/bereiche/darstellung.md`, „Prüfung“. | 31.10.2026 |
### Kartenwerkstatt: Felspinsel P2 – Tafel und Tastenhilfe (Branch phase5/fels-p2, PR folgt)

| Thema | Entscheidung | Datum |
|---|---|---|
| Tafel Felsstück | `Kartenwerkstatt.Felslage.cs`: Tooltips der Gizmo-Knöpfe nennen die Tasten 1, 2, 3 (Achsen: 4); „Auf den Boden (End)“; neuer Knopf „Kopieren (Strg+D)“; neue Zeile Auswahl mit „Gleiche Form“, „Gleiches Gestein“, „Umkehren (Strg+I)“, „Alle (Strg+A)“. Die Hilfe unter den Knöpfen nennt die Gizmo-Tasten und das Schieben mit den Pfeiltasten in Worten (keine Pfeilzeichen: die Schrift hat sie vielleicht nicht) | 09.11.2026 |
| Tastenhilfe unten | `Kartenwerkstatt.Tafeln.cs`: Zeile für ein gewähltes Felsstück mit 1/2/3/4 (Gruppen 1–4 erst nach Esc), Pfeiltasten zum Schieben (Umschalt, Strg, Alt), End/B, Q/E, Strg+D, Rahmen und Strg+A; beim Stempeln der Zusatz „danach steht der Gizmo an den neuen Stücken“; im Werkzeug Auswahl „Ziehen ins Leere: Rahmen um Felsen“. Entscheidungen zu Tasten und Verhalten in `darstellung.md` („Felspinsel P2“) | 09.11.2026 |

### Handschuh-Zeiger aus echter Hand, Wahl in den Einstellungen (PR #330)

| Thema | Entscheidung | Datum |
|---|---|---|
| Bilder | Neue Hände (`tools/modelle/handschuh_mpfb.py`, MPFB-Grundnetz CC0, Runde 16) ersetzen alle 36 Handbilder; Türschloss unverändert. Stufe 3 (Waldgrün): nur ein schmales Messingband an der Stulpe (vorher drei). Zeigerpunkte jetzt je Größe 6/6, 9/10, 12/13 (`Mauszeiger.HandPunkte`), nicht mehr 1,5- bzw. 2-mal 32. | 05.11.2026 |
| Wahl | Reihe aus sieben Kacheln (Auto + 6) mit 48-Pixel-Vorschau unter „Eigener Mauszeiger“ (Barrierefreiheit); alle sechs unabhängig vom Stand wählbar (nur Zierde), gespeichert als `zeiger_stufe` im Abschnitt „anzeige“ der Einstellungsdatei (0 = automatisch), nicht im Spielstand. Kacheln nur aktiv bei eingeschaltetem Zeiger; Name der Wahl darunter, Tooltip der Zeile listet alle. `--zeigerstufe` schlägt die Wahl nur für diesen Start. | 05.11.2026 |

### Neue Handschuh-Zeiger aus gezeichneten Vorlagen (PR „Oberfläche: neue Handschuh-Zeiger“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Bilder | Acht Hände als feine Pixel-Art („realistisch, wie alte 90er-Spiele“) aus Zeichnungen des Nutzers, mit Gemini überarbeitet (erlaubt, Prompts unbekannt, Quellbilder nicht im Repo). `tools/oberflaeche/zeiger_aus_vorlage.py --gemini`: freistellen, deckungsgleich ausrichten (Fingerspitze, Schnalle), je Größe einzeln pixeln (Flächenmittel, Umriss neu, Palette, Gold/Silber/Edelsteine als eigene Pixel, heller Rand). Ersetzt die Blender-Hände (PR #330); Türschloss unverändert. | 07.11.2026 |
| Größen | 48 (Standard bei 1080p), 64, 80 statt 32/48/64; Zeigerpunkt der Hand 2/1, 3/1, 3/1; Türschloss 80 neu gerendert, 32 entfällt. | 07.11.2026 |
| Hände | Sechs nach Stand (Gemeiner bis Reichsgraf) plus Wache (Plattenhandschuh) und Dieb (schwarz, deckender heller Rand) nur zur freien Wahl; Einstellungen: neun Kacheln (Auto + 8), `zeiger_stufe` 0..8, alter Wert bleibt gültig, `--zeigerstufe=1..8`. | 07.11.2026 |

### Mauszeiger ohne weißen Rand, Schein beim Drüberfahren (PR „Oberfläche: Mauszeiger ohne weißen Rand, Leuchten beim Drüberfahren“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Rand | Alle Hände (acht, drei Größen) und das Türschloss ohne hellen Außenrand; harte Alpha-Kante (0/255), die durchsichtigen Pixel tragen die Umrissfarbe, damit beim Filtern kein heller Saum entsteht. Der schwarze Dieb braucht den deckenden hellen Rand nicht mehr. | 08.11.2026 |
| Hover | Kein Aufhellen und Erwärmen mehr; „klick“ ist derselbe Handschuh Pixel für Pixel, dazu ein weicher weißer Außenschein (Abstand zur Silhouette, quadratisch auslaufend, höchstens 80 % Deckkraft) in einem durchsichtigen Rand von 6/8/10 Pixeln. Auch das Türschloss (erscheint nur über Anklickbarem) bekommt den Schein. | 08.11.2026 |
| Größe | Bilder 60/80/100 Pixel (Hand 48/64/80 plus Rand); normal und klick gleich groß, damit der Zeigerpunkt gleich bleibt: Hand 8/7, 11/9, 13/11, Schloss (seit 08.11. neu, siehe unten) 21/31, 27/41, 34/52. Die Vorschau in den Einstellungen schneidet den Rand weg (`AtlasTexture`). Das Skript: `zeiger_aus_vorlage.py --gemini` bzw. `--schloss`. | 08.11.2026 |

### Mauszeiger-Schein dezent und golden (PR „Oberfläche: Mauszeiger-Schein dezent und golden“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wunsch | Das Leuchten beim Drüberfahren war zu hell (Nutzer, 09.11.2026): nur merken, dass sich etwas ändert; Farbe leicht golden. | 09.11.2026 |
| Umsetzung | Scheinfarbe warmes Gold 230/190/110 statt Weiß. Nach Rückmeldung des Nutzers (09.11.2026: auch 30 % noch zu kräftig) keine weiche Wolke mehr, sondern **ein Pixel breiter Saum** (8 Nachbarn, damit er an Ecken geschlossen bleibt) direkt an der Silhouette mit 55 % Deckkraft. Alle acht Hände (klick) und das Türschloss, alle Größen; Bildgröße, Scheinrand 6/8/10 und Zeigerpunkte bleiben, `Mauszeiger.cs` unverändert. `zeiger_aus_vorlage.py --schein-neu ORDNER` bildet den Saum aus den normal-Bildern (Schloss: aus seinen voll deckenden Pixeln) neu und ist wiederholbar; `SCHEIN_FARBE`, `SCHEIN_STAERKE`, `SCHEIN_BREITE` gelten auch für `--gemini` und `--schloss`. | 09.11.2026 |

### Türschloss so groß wie der Handschuh (PR „Oberfläche: Türschloss-Zeiger an Handschuhgröße angepasst“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ursache | Das Schloss war mit demselben Kamerarand wie die Hand (3 von 16 Einheiten je Seite) gerendert, aber auf seine eigene, kleine Silhouette zugeschnitten: sichtbar nur 26 × 32 Pixel bei 48 gegen 44 × 40 der Hand (64 und 80 entsprechend). Größe und Stufenwahl im Code waren richtig (gleiche `GroesseWaehlen` für beide). | 08.11.2026 |
| Neu | `handschuh.py` bekommt `--schloss-rand` (Rand in 16-Einheiten; 1 statt 3), `--super` und `--groessen`; Schloss mit `--super=960 --schloss-rand=1` neu gerendert, dann `zeiger_aus_vorlage.py --schloss`. Sichtbar jetzt 34 × 44 (48), 46 × 58 (64), 56 × 72 (80), also etwa so hoch bzw. breit wie die Hand. Zeigerpunkt (Schlüssellochmitte samt Scheinrand): 21/31, 27/41, 34/52. | 08.11.2026 |

### Start-Hinweis zum Personalbuch (PR „Hinweis zum Start – weitere Lehrlinge im Personalbuch“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Hinweis | Seit #318 beginnt jeder Betrieb mit einem Lehrling. Bei einer neuen Partie (nicht nach dem Laden, nicht in der Probepartie) steht einmalig eine Tafel in den Meldungen unten links (`Hauptszene.Starthinweis.cs`): „In Eurer Werkstatt arbeitet nur ein Lehrling …“, Knöpfe „Zum Personalbuch“ (geht hinein, öffnet die Personal-Tafel) und „Später“. Bleibt wie eine Warnung stehen, bis sie geschlossen wird | 05.11.2026 |
| Merken | Nichts im Spielstand: „neue Partie“ heißt Spielzeit 0. Keine Einstellung zum Abschalten, weil es kein Tipp-System gibt; ohne Startoptionen von selbst, sonst `--starthinweis=1` | 05.11.2026 |
| Umzug | Zieht mit den Meldungen ins geplante Benachrichtigungssystem um (eine Methode, ein Aufruf in `PartieStarten`) | 05.11.2026 |

### Benachrichtigungen Stufe 1: Siegel und Briefe (PR #322)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ort | Alle Meldungen kommen nach rechts unten (auch „✗“); die Zeile unten links entfällt. Siegel-Ecke über der Betriebsleiste (über der Raumleiste im Innenraum), Brief-Fenster darüber; Tafeln rechts lassen den Platz frei (`Rollbereich.EckeHoehe`). Nichts pausiert. | 05.11.2026 |
| Stufen | Aus, Verlauf, Siegel, Brief; Wahl je Art im Reiter „Meldungen“ der Einstellungen (Abschnitt `[meldungen]` in `einstellungen.cfg`), gilt für Eigen und Stadt, Fremdes bleibt im Verlauf. | 05.11.2026 |
| Zuordnung | `game/daten/meldungen.json` (Arten, Themen, Textmuster, Vorgabe je Betroffenheit, Absender, Ziel, Sammelregel) und `game/scripts/Meldungsordnung.cs` (ohne Godot, `MeldungsordnungTests`). | 05.11.2026 |
| Bündeln | Jahreswechsel: ein Brief „Jahresbericht“ (Gericht, Rat, Steuer, Stand, Personal, Bank, Spitzel), einzeln nur Todesfall und eigene Anklage. Stillstand: ein Siegel je Betrieb nach 60 Spielminuten. Nacht (20–23 Uhr und Nachtgeschehen der Jahresabrechnung): ein Brief „Die Nacht“. Rivalen nur Verlauf. Fremde Unterwelt und fremde Spitzelberichte verworfen (`Gauner.DiesesJahr[^1].Fuer`, Auftraggeber). | 05.11.2026 |
| Verlauf | Reiter „Verlauf“ im Bericht (Enum-Name bleibt `Protokoll`): alle Meldungen der Sitzung über die Jahre (3000), Filter Alles, Mein Haus, Rivalen; nicht im Spielstand. | 05.11.2026 |
| Start-Hinweis | Der Hinweis zum Personalbuch (PR #321) kommt als Brief mit Knopf „Zum Personalbuch“. | 05.11.2026 |
| Gestaltung | Siegel und Brief zeichnet der Code (schwarzer Umriss, Farbe leicht versetzt, Schraffur); „Siegel und Briefe bewegen“ schaltet Einblenden und Pulsieren ab. Startoptionen `--meldungen=demo|brief|jahr`. | 05.11.2026 |
| Messung nachher | Siegel plus Briefe etwa 9 je Spieljahr statt gut 80 Zeilen (Messung `messen meldungen`, Mittel); Bündelung und Filter wirken zusätzlich. | 05.11.2026 |

### Benachrichtigungen Stufe 2: Porträt, Hinsehen, Verlauf aus dem Spielstand (PR #339)

| Thema | Entscheidung | Datum |
|---|---|---|
| Zuordnung | Art des Kerns, wenn eine Art in `meldungen.json` sie unter `kern` nennt; Feuer, Überfall, Ratssitzung und Sonstiges weiter über die Textmuster (Blitz/Anschlag bzw. Steuer/Rat liegen in verschiedenen Arten). Betroffenheit aus dem Haus der Meldung, ohne Haus wie Stufe 1. | 06.11.2026 |
| Porträt | Person der Meldung, sonst bei Anklage, Schulden, Meisterprüfung und Aufstieg das Oberhaupt; am Siegel klein unten links (fängt keine Klicks), im Brief 64 px mit Name und Haus (Klick: Charakterfenster). | 06.11.2026 |
| Hinsehen | Knopf im Brief (ersetzt „Zu <Betrieb>“), je Zeile in gestapelten Briefen, Klick auf eine Zeile im Verlauf: Kamera hin, eigener Betrieb → Kurzübersicht, fremdes Gebäude → Auswahl, eigenes Wohnhaus → Tafel, Blitzschlag → Ort; ohne Ort Charakterfenster der Person. Drinnen erst hinaus. | 06.11.2026 |
| Verlauf | Nach dem Laden aus dem Spielstand (500 Meldungen des Kerns), ohne Siegel und Briefe neu auszulösen. | 06.11.2026 |

### Ladebildschirm mit Bild, Tipps und geeichtem Balken (Branch phase5/ladebildschirm-bild)

| Thema | Entscheidung | Datum |
|---|---|---|
| Auftrag | Balken neu eichen, ein großes Holzschnitt-Ladebild, 20–30 Tipps, Rahmen im Stil der Oberfläche (Teamleiter nach Nutzerwunsch). | 07.11.2026 |
| Balken | Gemessen mit `--ladezeit=1` (Release, Köln und Greybox, je 3 Läufe, Vulkan RTX 4080): Die Anteile der Abschnitte in `Stadtansicht.Karte.cs`, `Stadtansicht.cs` und `Hauptszene.cs` folgen jetzt dem Mittel beider Karten (Anteil = Zeit, die davor verstrichen ist; Balken von 0,02 bis 1). Mittlere Abweichung des Balkens von der verstrichenen Zeit 5,4 → 0,6 Punkte (Köln), 4,7 → 0,8 (Greybox); größte Abweichung 12 → 1,4 Punkte. Die Phase hinter dem Ladebild (Gras, Vögel, Wetterprobe) hat eigene Stufen (`ErsteBilderAbwarten`: 0,784 / 0,79 / 0,925 bis 1). Die Kartenwerkstatt ist seit dem Nachtrag unten geeicht. Neu: Das Log zeigt je Abschnitt „Ladezeit: Balken <Anteil> „<Text>“ bei <ms>“; die Dauer eines Abschnitts ist der Abstand zur nächsten Zeile. | 07.11.2026 |
| Bild | `ladebilder/koeln.webp` (1664 × 928): Köln am Rhein mit Dom-Baustelle, Kran und Schiffen, aus Qwen-Image 2512 + holzschnitt_v1 (Ablauf `docs/assets/prompts/ladebilder.md`). Es füllt das Fenster (zugeschnitten, bei sehr breiten Fenstern oben und unten beschnitten), in einem Holzrahmen. Die acht quadratischen Platzhalter entfallen. Drei Entwürfe liegen beim Nutzer zur Auswahl. | 07.11.2026 |
| Tipps | `ladetipps.json`: 30 Tipps, alle aus dem Spielbuch (Wirtschaft, Hafen, Stände). Unten auf einer Pergamenttafel über dem Balken. | 07.11.2026 |
| Bilder | `--ladebildfotos=<ordner>` speichert den Ladebildschirm bei 10, 50 und 90 % des Balkens. | 07.11.2026 |
| Nachträge (Integration, NAS-Klicktest) | Der Tipp wechselt nach echter Zeit (`Time.GetTicksMsec`): während der Abschnitte blieb `delta` klein, auf der NAS wechselte er in 20 s nie. `--ladebildfotos` holt das 90-%-Bild beim Schließen nach (der Balken erreicht 90 % oft nicht vorher). Tipps ohne „✗ ⚠ ▲ ▼“ (fehlen der Text-Schrift, Kästchen). | 07.11.2026 |

### Ladebalken der Kartenwerkstatt geeicht (Branch phase5/werkstatt-ladebalken)

| Thema | Entscheidung | Datum |
|---|---|---|
| Messung | `--kartenwerkstatt=koeln` und `=greybox` mit `--ladezeit=1`, je 3 Läufe vorher und 6 nachher (Vulkan, Grafikkarte der NAS, `-p:Optimize=true`; die NAS war durch andere Läufe belegt, Load 6–8, darum zwei Nachher-Sätze). Sollwert = `0,02 + 0,98 × verstrichen / Gesamtdauer` (Gesamtdauer von „Die Reise beginnt“ bis „Ladebild schließt“: Köln 15–26 s, Greybox 15–30 s), Anteil je Abschnitt = Sollwert an seinem Beginn, gemittelt über die Läufe und beide Karten (wie bei den Spiel-Anteilen). | 08.11.2026 |
| Neue Anteile | Entwurf ausrollen 0,03 → 0,077; Gelände (`Durchlaufen` von 0,09 → 0,106); Häuser 0,228 → 0,202; Wege 0,457 → 0,422; Wald und Felder 0,462 → 0,445; Bäume 0,585 → 0,483; Mauer 0,587 → 0,493; Fluss 0,590 → 0,499; Ende des Abschnitts („Werkzeug wird bereitgelegt“) 0,60 → 0,532; erstes Bild 0,66 → 0,777 (siehe unten). In `Stadtansicht.Kartenwerkstatt.cs` sind die Anteile der Schritte relativ zum Weg von 0,106 bis 0,532 (0 / 0,225 / 0,742 / 0,796 / 0,885 / 0,908 / 0,923). Die alten Werte kamen von Köln vom 29.10. und liefen zu schnell voran (Wald und Bäume nahmen 0,5 Balken, kosteten aber 1,2 s). | 08.11.2026 |
| Abweichung | Mittlere Abweichung des Balkens vom Sollwert an den Abschnittsgrenzen (ohne das erste Bild), Punkte: **Köln 4,3 → 1,6, Greybox 6,0 → 3,0** (größte 8,6 → 2,6 bzw. 11,7 → 4,7; in den ruhigen Läufen unter 1,5). Über die ganze Zeit gerechnet (Balken stückweise konstant) 10,0 → 8,7 bzw. 10,2 → 9,3, größte 33,6 → 23,8 bzw. 33,6 → 26,4. | 08.11.2026 |
| Das erste Bild | In der Werkstatt dauert das erste Bild hinter dem Ladebild 8–11 s (Köln; Greybox 7–9 s, 45 % der Gesamtzeit), weil Felder (`WerkstattAktualisieren` 5 s), Wegeschicht (2 s) und Gras (1,4 s) erst hier rechnen; der Balken steht darin still. Seine Stufe steht deshalb auf dem Sollwert in der Mitte des Bildes (0,777), nicht an seinem Anfang (0,555): größte Abweichung 44 → 24 Punkte. Der Abschnitt davor ist unverändert. Wer den Balken dort wirklich gleichmäßig will, muss diese Arbeit in `EntwurfZeigenSchritte` oder `Stadtansicht.WerkstattAktualisieren` in Abschnitte teilen (Felder und Wegeschicht als eigene Schritte) – eigener Auftrag. | 08.11.2026 |

### Lebensweg im Charakterfenster (PR „Kern: Charaktersystem Teil 3a – Lebensweise“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Reiter | Bei Oberhaupt, Ehepartner und Erbe (`Familienbuch.HatLebensweg`) zwei Reiter unter dem Kopf: „Züge und Familie“ (wie bisher) und „Lebensweg“ (Datei `CharakterAnzeige.Lebensweg.cs`). Freie Punkte stehen golden am Reiter („Lebensweg (2 frei)“). Kinder, Ratsherren und Fremde ohne Lebensweg haben keinen Reiter; Startoption `--charakter=<Person>:lebensweg`. | 08.11.2026 |
| Pfade | Fünf Karten untereinander: Zeichen (Platzhalter aus Waren-Icons), Name, Balken bis zum nächsten Punkt (golden, bei freiem Punkt hell), „noch n Erfahrung, etwa k Jahre bis zum nächsten Punkt“ (die Jahre nur beim aktiven Pfad, aus `ErfahrungJeJahr`), rechts „Knoten von Punkten“ und „n frei“. Aktiver Pfad: dicker goldener Rahmen und „folgt diesem Pfad“; ein Klick auf eine Karte zeigt ihren Baum (heller Grund), ohne den Pfad zu wechseln. | 08.11.2026 |
| Wechsel | Knopf „Diesem Pfad folgen“ (nur bei Personen des eigenen Hauses) mit dem Hinweis, dass Gesammeltes und Knoten bleiben und der Wechsel nichts kostet; Befehl `LebenswegWaehlen` über den Boten. | 08.11.2026 |
| Baum | Der Baum 3 × 3 in der `TalentbaumAnsicht`, die dafür ein optionales `BaumTexte` bekam (Zeile unter dem Namen, Hinweistext, Bild): „1 Punkt“ statt Gulden, „gelernt“ statt „gekauft“, Reihen „Anfang“, „Mitte“, „Gipfel“ statt Gebäudestufen. Tooltip mit Wirkung, Kosten, Voraussetzung und Grund, warum es gerade nicht geht (aus `Lebensbaum`). Ein Klick lernt den Knoten über `KnotenWaehlen`; das Ergebnis steht oben im Reiter und im Protokoll. | 08.11.2026 |
| Fremde Personen | Oberhäupter anderer Häuser werden angezeigt, aber nur zum Ansehen (nichts nehmbar, kein Wechselknopf, Hinweis „gehört nicht zu Eurem Haus“); der Kern gibt den Baum fremder Personen her. | 08.11.2026 |
| Ehepartner und Erbe | Ein Hinweis nennt den Anteil (50 %) und dass die Knoten für das Haus erst wirken, wenn die Person Oberhaupt ist. Beim Erben steht das Vermächtnis (20 % der Erfahrung des Oberhaupts im aktiven Pfad, mit der Zahl von jetzt), beim Oberhaupt, wer erbt. | 08.11.2026 |
| Neuer Punkt | Der Kern meldet das nicht. Die Hauptszene liest die freien Punkte jedes Bild (Oberhaupt, Ehepartner, Erbe) und zeigt in der Kopfzeile „n Punkte frei“ (grün, Tooltip nennt die Personen, Klick öffnet den Lebensweg dieser Person). Wird bei einer Person ein Punkt neu frei, kommt eine Meldung „Neuer Punkt der Lebensweise: …“ als Siegel (Art „Familie“ in `meldungen.json`, Hinsehen öffnet das Fenster der Person). Nach dem Laden gibt es keine Meldung für schon freie Punkte. | 08.11.2026 |
| Lexikon | Neuer Eintrag „Lebensweise“ mit den Zahlen aus den Daten (`lebensweise_grund`, `lebensweise_jestern`, `lebensweise_jeap`); die Tooltips verweisen darauf. | 08.11.2026 |

### Hilfe-Mauszeiger (PR „Oberfläche: Hilfe-Mauszeiger nach Vorlage“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aussehen | Messing-Dreieck mit Fragezeichen nach einer Vorlage des Nutzers (mit Gemini erstellt, Herkunft im Protokoll); freigestellt mit `tools/oberflaeche/zeiger_hilfe.py`, Höhe 48/64/80 wie die Hände, 1 Pixel dunkle Kontur, ohne Schein und ohne Hover-Fassung. Feine Gravur und Nieten gehen bei 48 Pixel weich verloren, Form und Farbe bleiben. | 09.11.2026 |
| Einbau | `Mauszeiger` setzt `CursorShape.Help` je Größe, unabhängig von Stufe und Zustand, Zeigerpunkt (0, 0) = Dreiecksspitze; `ZeigerEntfernen` setzt ihn zurück. | 09.11.2026 |
### Werkstatt-Pinsel: Fehlerliste nach dem Strich, Ladebalken mit Vorwärmen (Branch phase5/werkstatt-pinsel-fluessig)

| Thema | Entscheidung | Datum |
|---|---|---|
| Fehlerliste | Kopf und Fehlerliste der Werkstatt (`Auffrischen`, mit der vollen Prüfung) frischen während eines Pinselstrichs (Gelände, Boden, Fels) und 0,3 s danach nicht auf, sondern merken es sich (`_auffrischenNachStrich`) und holen es in `_Process` nach (`Stadtansicht.WerkstattPruefungAufschieben`). Vorher rechnete jeder Tupfer die ganze Prüfung und baute bis zu 150 Knöpfe neu (Köln 12–40 ms je Tupfer). Rückgängig-Knopf und „ungespeichert“ stehen damit erst nach dem Strich neu. Einzelheiten und Messung in `darstellung.md` („Werkstatt-Pinsel malen ohne Ruckeln“) | 09.11.2026 |
| Ladebalken | Neuer Abschnitt nach den Tafeln: 0,56 „Die Pinsel werden ausgewaschen …“ (`Stadtansicht.WerkstattVorwaermen`, Köln 76 ms, headless gemessen mit `--ladezeit=1`); die übrigen Anteile bleiben (0,532 Tafeln, 0,777 erstes Bild) | 09.11.2026 |
| Messung | Startoption `--pinselmessung=<datei.md>` (`Kartenwerkstatt.Pinselmessung.cs`): malt nach dem Laden mit jeder Pinselart einen Strich aus 30 Tupfern, legt dazu einen Weg und eine Bauzone an, schreibt die Tabelle und beendet das Spiel. Nachtrag (Branch phase5/pinsel-nacharbeit): die Spalte „übriges Bild“ zählt eingerückte Teile (stecken in einem äußeren) nicht mehr doppelt und wird nicht mehr negativ; neu Rechenzeit des Hauptfadens je Bild (Linux), Spalten „über Leerlauf“, Spitzenbilder danach mit ihren größten Teilen, `--pinselmessung-nur=` | 09.11.2026 |
| Messung | Startoption `--pinselmessung=<datei.md>` (`Kartenwerkstatt.Pinselmessung.cs`): malt nach dem Laden mit jeder Pinselart einen Strich aus 30 Tupfern, legt dazu einen Weg und eine Bauzone an, schreibt die Tabelle und beendet das Spiel | 09.11.2026 |

### Ego-Perspektive im Entwicklerfenster (PR „Oberfläche: Ego-Perspektive im Entwicklerfenster“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Auftrag | Wunsch des Nutzers: Knopf „Ego-Perspektive“ im Entwicklerfenster; Klick auf die Karte wählt den Ort, dort läuft man wie in einem Ego-Shooter herum, Esc stellt die Spielkamera wieder her. Reine Ansicht (kein Befehl, kein Kennzeichen, der Kern bleibt unberührt); die Ansicht steckt in `EgoKamera.cs` und `Stadtansicht.Ego.cs` (`darstellung.md`), hier nur Knopf und Oberfläche. | 09.11.2026 |
| Knopf | Auf dem Reiter „Wetter und Darstellung“ eine Zeile „Ego-Perspektive“ über den Hilfsanzeigen. Der Knopf schließt das Fenster und startet die Wahl (`Stadtansicht.EgoWaehlen`); geht es nicht (Raum, Haus, schon aktiv), steht die Ablehnung rot im Fenster. Oben mittig steht während der Wahl ein Hinweis „Klick auf die Karte wählt den Ort · Esc: abbrechen“. | 09.11.2026 |
| Oberfläche im Modus | `Hauptszene.Ego.cs` blendet die Ebene „Oberflaeche“ aus (zu sehen bleibt „Esc: zurück“ unten links) und danach wieder so ein, wie sie war (`--ui=0` bleibt). Öffnet sich in der Zeit ein Jahresbericht, das Spielmenü oder eine Ratssitzung, endet der Modus von selbst (`EgoPruefen`). Die Spielzeit läuft weiter; die Zifferntasten 1–9 stellen das Tempo, alle anderen Tasten nimmt die Kamera (Leertaste springt, sie pausiert nicht). | 09.11.2026 |
| Steuerung (Festlegung des Teamleiters) | Maus schaut (gefangen), W/A/S/D läuft 1,4 m/s, Umschalt rennt 4 m/s, Leertaste springt, Augenhöhe 1,65 m, Empfindlichkeit 0,12°/Pixel. | 09.11.2026 |
| Startoption | `--ego=x,z[,gier[,nick]]` (`docs/STARTOPTIONEN.md`). | 09.11.2026 |
| Klicktest | Vulkan (RTX 2000 Ada, Pixel-Look, Köln, Anzeige 181) mit echten Tasten und Mausbewegungen (`xdotool`): Knopf im Fenster, Hinweis, Klick auf die Straße, Gehen und Rennen, Umsehen, Esc (Spielkamera, Oberfläche und Tempo wie vorher, kein Spielmenü). Kein SCRIPT ERROR. | 09.11.2026 |
### Stress im Charakterfenster (PR „Kern: Charaktersystem Teil 3b – Stress“, vom Kern mitgebaut)

| Thema | Entscheidung | Datum |
|---|---|---|
| Zeile | Bei Oberhaupt, Ehepartner und Erbe des eigenen Hauses (`Familienbuch.HatStress`) unter der Meinung die Zeile „Stress:“ mit Balken (0 … 100, Marken an den Stufen 40 und 70, gefüllt grünlich/gelb/rot nach Stufe) und „57 · angespannt“ in derselben Farbe (`CharakterAnzeige.Stress.cs`). Fremde Personen mit Stress: nur „Wirkt angespannt“ (bzw. ruhig, zermürbt), ohne Zahl; Kinder und Ratsherren: nichts. | 09.11.2026 |
| Tooltip | Stufe und Wert, Folgen der Stufe (`StressFolgen`), die Stufen mit Zusammenbruch, die Einträge dieses und des letzten Jahres („+10 Gaunerstück in Auftrag gegeben (ehrlich)“, „−15 Abklingen“), Abklingen und Ventile, Verweis auf das Lexikon. | 09.11.2026 |
| Talente und Aktivitäten | Im Tooltip eines Talents die Zeile „Stress −½“ (`Stresslage.TalentAbzug`). In der Aktivitätenliste hinter dem Hinweis, was die Aktivität an Stress bewegt („Stress: Heinrich −15 (Im Wirtshaus zechen); Agnes −15 (…)“, aus `StressVorschau`). | 09.11.2026 |
| Lexikon und Entwicklerfenster | Lexikon-Eintrag „Stress“ mit Live-Zahlen (`stress_stufe1`, `stress_stufe2`, `stress_hoechstwert`, `stress_danach`, `stress_abklingen`). Entwicklerfenster, Reiter „Familie und Ereignisse“: Zeile „Stress“ (Zahl 0–100, „Setzen“) für die gewählte Person; 100 löst den Zusammenbruch aus. | 09.11.2026 |
| Medaillons | Schwermütig und Reizbar aus Vorlagen des Nutzers (mit Gemini erstellt, WebP), geschmiedet wie die übrigen erworbenen Züge: `tools/zugmedaillons/schmiede.gd` mit Eisenring, 128 px (`schwermuetig.png`, `reizbar.png`). Die Schmiede liest jetzt auch `.webp`, schneidet nicht quadratische Vorlagen mittig quadratisch zu und überspringt Züge ohne Bild – so lassen sich einzelne Medaillons nachschmieden, ohne die übrigen neu zu rechnen. Protokollzeile „Vorlage des Nutzers, mit Gemini erstellt“, Prompt-Datei `zug_medaillons.md` ergänzt. | 09.11.2026 |
### Kartenwerkstatt: Felspinsel P3 – alle Regler in der Tafel (Branch phase5/fels-k-p3, PR folgt)

| Thema | Entscheidung | Datum |
|---|---|---|
| Pinsel | Streuen: Radius, Dichte, **Abstand** (0–10 m, „dicht an dicht“ bei 0), Größe und Streuung mit der Zeile „Größe … bis … ד darunter (Zufallsbereich Größe ohne neues Feld), Einsinken, **Einsinken ±** (0–50 %, Vorgabe 5 %), Neigung, Häkchen wie bisher. Stempel ohne Radius, Dichte und Abstand | 09.11.2026 |
| Erweitert | Neuer Abschnitt **Erweitert**, zu Beginn zugeklappt: Bereiche von–bis mit Häkchen für Drehung, Kippen (X), Rollen (Z), Breite, Höhe, Tiefe, Höhenversatz (`BereichZeile`: Häkchen, zwei Zahlenfelder; ohne Häkchen null = Verhalten von vorher) und „Neigung bis“ (Häkchen, Grad) | 09.11.2026 |
| Zugeklappt bleibt zu | `Kartenwerkstatt._abschnitteZu` merkt sich zugeklappte Abschnitte je Überschrift über den Neuaufbau der Tafel hinweg (eigene Wahl; vorher ging jeder Abschnitt beim Neuaufbau wieder auf). „Erweitert“ steht zu Beginn darin | 09.11.2026 |
| Filter | Unter dem Filter (Steigung, Boden) die Zeile **Höhe** (Bereich in Metern, −20 … 60) für alle Felsarten | 09.11.2026 |
| Radierer | Abschnitt **Formen** mit „Nur diese Formen“ und den Formen; Regler **Ausdünnen bis** (0 = „aus: alles weg“, bis 80 %) statt eines eigenen Werkzeugs (wie „Erase Density“ in Unreal; eigene Wahl: keine neue Palette, kein neues Symbol) | 09.11.2026 |
| Neu verteilen | Tafel mit Formen und „Nur diese Formen“, Radius, Häkchen „Neu würfeln:“ (Größe, Achsen, Drehung, Kippen, Rollen, Höhe, Einsinken, Neigung), den Reglern für die Werte, „Erweitert“, Filter; bei einer Auswahl der Knopf „Auf die Auswahl anwenden (n)“. Die Tafel eines gewählten Stücks hat „Neu verteilen“ mit denselben Häkchen (`FelsWurfHaken`) | 09.11.2026 |
| Texte | Palette: „Neu verteilen“, Tooltip des Radierers mit Ausdünnen; Tastenhilfe unten: Umschalt radiert beim Streuen, Ausdünnen, Neu verteilen | 09.11.2026 |
| Startoptionen | `--werkstatt-fels=` kennt `pinsel:<feld>=<wert>,…`, `ausduennen:<radius>,<dichte>,…`, `verteilen:<werte>[,<radius>,…]` (STARTOPTIONEN); die Pinselmessung misst „Neu verteilen“ und „Ausdünnen bis 10 %“ mit | 09.11.2026 |
| Offen | Kein Bild der Tafel (Godot nur headless); Breite der Bereichszeilen (Häkchen + zwei Zahlenfelder) bei 1280 px ungeprüft | 09.11.2026 |

### Geheimnisse im Charakterfenster (PR „Kern: Charaktersystem Teil 3c – Geheimnisse“, vom Kern mitgebaut)

| Thema | Entscheidung | Datum |
|---|---|---|
| Abschnitt | `CharakterAnzeige.Geheimnisse.cs`: „Geheimnisse“ unter den Zügen, bei Personen und Ratsherren; gezeigt wird nur `Geheimniswesen.Sichtbar(spieler, person)` – eigenes Haus „Ihr hütet: Ehebruch (seit 1403)“ (rot, mit „der Ehepartner weiß es“ bzw. „die ganze Stadt weiß es“), sonst „Ihr wisst: …“ (gold), „Aus der Beichte: …“, „Die ganze Stadt weiß: …“ (grau). Tooltip: Beschreibung der Art, woher und seit wann, Beweiskraft und Buße bzw. „kein Vergehen“ (bei Ketzerei „nur wer sich nach außen zum Stadtglauben bekennt“ und, wenn es hier nicht strafbar ist, der Grund aus `KeinVergehen`), was Bloßstellen kostet, Verweis `[[geheimnisse]]`. Nichts bekannt: der Abschnitt fehlt (beim Ratsherrn steht „Ihr wisst nichts …“ mit dem Spitzel-Knopf). | 09.11.2026 |
| Knöpfe | Bei nutzbarem Wissen: Erpressen (mit `Zahlfeld` 50–20.000 Gulden, nur bei Personen), Ratsstimme, Anzeigen, Bloßstellen – Befehl `GeheimnisNutzen`; bei Beichtwissen „Beichtgeheimnis brechen“ (`BeichtgeheimnisBrechen` mit Haus bzw. „R:Name“); beim Ratsherrn „Spitzel ansetzen“ (`SpitzelAnsetzen("R:Name")`). Ausgegraut mit dem Grund aus dem Kern (`NutzenVerboten`, `RatsspitzelVerboten`) oder „Nicht genug Aktionspunkte“; sonst die AP im Tooltip. Ergebnis als Meldung über dem Abschnitt, das Fenster baut neu auf. Kein Bestätigungsdialog (wie die übrigen Befehle im Fenster). | 09.11.2026 |
| Ratssitzung | Die Begründung eines erpressten Sitzes heißt „Erpresst von Haus X – stimmt wie Haus X.“ bzw. „– stimmt für ihn.“ (aus `Ratssitzung` im Kern); ein erpresstes Haus des Spielers ist im Punkt nicht frei, wo der Erpresser die Stimme bestimmt (`Stadtrat.StimmeVerboten`, `WahlStimmeVerboten` mit Grund), sonst stimmt es wie gewohnt. Gerichtssaal: Ein Verfahren nennt seine Anklagepunkte über `Stadtgericht.Anklagepunkte(v)` (mit dem angezeigten Geheimnis, sonst wie bisher). | 09.11.2026 |
| Lexikon, Entwicklerfenster, Startoption | Lexikon-Eintrag „Geheimnisse“ (Gruppe Haus und Familie) mit `{wert:geheimnis_spitzel}`, `geheimnis_spitzel_ratsherr`, `geheimnis_gerede`, `geheimnis_beichte`, `geheimnis_ansehen_jahre`, `geheimnis_busse`, `geheimnis_mord_busse`. Entwicklerfenster, Reiter „Familie und Ereignisse“: Zeile „Geheimnis“ mit Ehebruch, Ketzerei, Schulden (jeweils mit Wissen) für die gewählte Person. Startoption `--geheimnis=<Art>[:oberhaupt|ehepartner|rivale|ratsherr[:n]|Nummer]` und `--charakter=rivale` (`Hauptszene.Charakter.cs`). | 09.11.2026 |
| Bilder | Keine: je Art nur Text (Platzhalter, nichts lädt ein Bild). Siehe Offen. | 09.11.2026 |
