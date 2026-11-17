# Bereich Oberfläche – Offen und Entscheidungen

> **Archiv:** Ältere und abgelöste Entscheidungen (bis 26.10.2026) stehen in [archiv/oberflaeche.md](archiv/oberflaeche.md). Weitere gemergte Abschnitte (bis 16.11.2026) stehen **wörtlich** am Ende des Archivs unter „Entscheidungen (gemergt, verschoben 16.11.2026)“; Verweise in Code und Doku auf Abschnitte dieser Datei gelten auch dort. Zum Arbeiten genügt diese Datei; das Archiv nur bei Bedarf lesen.

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
- Benachrichtigungen: Ton `meldung_siegel` und `meldung_brief` (Glockenspiel-Töne, selbst synthetisiert, vom Nutzer gewählt; Meldungstöne 11.11.2026); ein Siegel-Tooltip zeigt nur die neueste Meldung; `Meldungsmessung.Einordnen` (Test) und `Meldungsordnung` führen noch dieselben Textmuster doppelt (die Messung könnte die neue Klasse nutzen).
- Benachrichtigungen: Das ✕ des Fensterrahmens ist hell und bekommt im Brief eine dunkle Platte; ein gemeinsames Pergament-Fenster für andere Tafeln (Oberflächen-Stil Schriftrolle) fehlt noch.
- (Werkstatt-Pinsel) Pinselmessung (`Kartenwerkstatt.Pinselmessung.cs`, `Rest()`): Die Spalte „übriges Bild (Godot, Leerlauf)“ zieht die Summe aller Teilschritte ab, manche stecken aber ineinander („  Nacharbeit: …“ in „Nacharbeit nach dem Strich“, „  Gras: …“ in „im Bild: Grasteppich“) und zählen doppelt; die Spalte ist dann zu klein und kann negativ werden. Betrifft nur das Messwerkzeug.
- (Stress 3b) Klicktest des Stressbalkens auf der NAS steht aus (Startoption `--entwickler=1 --stress=75 --charakter=oberhaupt`; Bilder: Balken angespannt und zermürbt, Tooltip mit Einträgen, fremde Person „Wirkt …“, Aktivitätenliste mit Stresszeile, Entwicklerfenster „Stress setzen“, Zusammenbruch-Meldung).
- (Geheimnisse) Klicktest der Siegel auf der NAS steht aus: Charakterfenster (Startoptionen siehe nächster Punkt) sowie Meldung, Brief und Hinterzimmer mit `--entwickler=1 --erpresserbrief=Ehebruch --hinterzimmer=1` (Siegel „Geheimnisse“ in der Ecke mit Medaille neben dem Porträt, Brief mit Medaille am Fuß, Brief „Erpresserbrief“, im Hinterzimmer der Brief mit Medaille; Lesbarkeit der Medaille bei 30 px).
- (Geheimnisse 3c) Klicktest auf der NAS steht aus (Schritte im Bericht des PR): `--entwickler=1 --geheimnis=Ehebruch:rivale --charakter=rivale`, `--geheimnis=Ketzerei:ratsherr --charakter=ratsherr`, `--geheimnis=Schulden:oberhaupt --charakter=oberhaupt`; Godot legt beim ersten Öffnen `CharakterAnzeige.Geheimnisse.cs.uid` an (mit einchecken).
- Hofstellen (Kartenwerkstatt; entfällt mit „nur noch Bauzonen“, PR 3 Karten): Klicktest auf der NAS steht aus (Schritte im Bericht); der Umriss der Hofstelle prüft Werkstatt- und Wohnhausplatz getrennt (Text nennt nur den ersten Fehler), ein Platz mit Fehler färbt beide Umrisse rot.
- (Hofstellen) Die Tafel „Start“ nimmt Hofstellen mit anderer Werkstattart des Hauses nicht gesondert aus; „Hofstelle“ setzt immer den ersten Werkstattbetrieb des Hauses um.
- (von Spiel-KI) Aufschlag am Hafen (Datenversion 80): Verkauft ein Karren am offenen Hafen Ausfuhrware, bringt sie `Schiffahrt.Daten.HafenAufschlag` (3 %) mehr (`Schiffahrt.HafenVerkaufsFaktor(ware)`, Liste über `SchiffshandelDaten.IstAusfuhrware`). Hafentafel (`HafenAnzeige.cs` um Zeile 193) und Hinweis in `Hauptszene.Hafen.cs` (um Zeile 117) sagen noch „dieselben Preise wie auf dem Marktplatz“ – Wunsch: Satz „Karren erzielen hier für Ausfuhrware 3 % mehr (nicht im Winter)“ mit Tooltip der Ausfuhrwaren; die Vorschau „verkaufen ≈“ im Marktfenster rechnet ohne Aufschlag.
- Entscheidungsbrief: Ton `meldung_entscheidung` (Glockenspiel, vom Nutzer gewählt; die Mahnung hat den eigenen Ton `meldung_mahnung`); der Tooltip-Text „Nicht rückgängig zu machen!“ und die rote Schrift sind die einzige Auszeichnung des Unumkehrbaren (der Tooltip kennt keine Farbe); „Entscheidung/Automatisch“ im Aufklappmenü der Einstellungen ist wie alle Aufklappmenüs im Klicktest nicht im Bild zu sehen (nur per Code geprüft); Mahnung „Betrieb verkaufen“ wählt nur den ersten eigenen Betrieb aus (ein eigener Verkaufsdialog je Betrieb fehlt).
- (von kern) `BetriebsVerwaltung.cs:345`: Die Warenliste zeigt den Gewinn je Minute ohne Hafen-Aufschlag, der Ausbau-Mehrgewinn (:729) kommt mit Aufschlag; dort `GewinnJeArbeitsminute(w, ware, betrieb)` nutzen.
- (von Kern) Entscheidungen, Teil 1 (Datenversion 81): `Stadtwirtschaft.Entscheidungen` (`Entscheidungswesen`) hält die offenen Fragen an das Haus des Spielers – Erpresserbrief, Schutzgeld, Lösegeld. Lesen: `OffenFuer(spieler)` (kürzeste Frist zuerst), `Titel(e)`, `Beschreibung(e)`, `Wahlen(e)` (Knopftexte, Index = Wahl), `Folgen(e, wahl)` (Tooltip, reiner Text mit Zahlen), `Voreinstellung(e)`, `RestMinuten(e)` (Spielminuten bis zur Voreinstellung, für den Fristbalken), `e.Person` (Geisel, Porträt) bzw. `e.Absender` (Haus des Absenders), Ereignisse `Geoeffnet` und `Geschlossen(e, Entscheidungsabschluss)`. Befehlen: `UiHilfe.Befehlen(new EntscheidungTreffen(e.Nummer, wahl))`; „Automatisch“ schickt `EntscheidungTreffen(e.Nummer, Entscheidungswesen.Voreinstellungswahl)` (−1). Schlägt der Befehl fehl („✗“), bleibt die Frage offen. Jeder Abschluss kommt als Meldung `Meldungsart.Entscheidung` (vertraulich, Haus, Person) – in `meldungen.json` noch keiner Art zugeordnet. Antwortet die Gauner-Tafel, ist die Frage ebenfalls zu. Zum Ausprobieren: Entwicklerbefehl `EntwicklerForderung(Entscheidungsart, Von, Betrag)` (Startoption fehlt noch). Pause im Einzelspiel und Einstellung je Art bauen bei euch (Plan N11, Abschnitt 4).
- (von Kern) Entscheidungen, Teil 2 (Datenversion 82): neue Arten `Krankheit`, `Erbe`, `Geheimnis`, `Mahnung` mit denselben Abfragen (`Wahlen(e)` hat 3, 1 + Kinder, 4 bzw. 3 Knöpfe; Konstanten `Entscheidungswesen.Medicus`, `Schnaps`, `NichtsTun`, `ErbfolgeBelassen`, `Anzeigen`, `Erpressen`, `Blossstellen`, `Merken`, `Abwarten`, `ZumKredit`, `ZumVerkauf`). Porträt: `e.Person` (Kranker, Oberhaupt beim Erbe, Inhaber des Geheimnisses); beim Geheimnis `e.Absender` = Haus des Inhabers. Mahnung: Wahl 1 bzw. 2 ist nur ein Wegweiser – nach dem Befehl bitte Banktafel bzw. Betriebstafel öffnen. Frist 120 Spielminuten (`RestMinuten`). `meldungen.json` hat die Art „Entscheidung“ (Kern-Art `Entscheidung`, Stufe Verlauf, Thema Befehle) – von kern im PR ergänzt, bitte prüfen. Startoption `--entwickler=1 --entscheidung=<Art>[:Betrag]` (`Hauptszene.Entscheidung.cs`, von kern) für den Klicktest des Entscheidungsbriefs.

- (3D-Handschuh, Branch phase5/handschuh3d-einbau) Klicktest auf der NAS steht aus, der Entwickler sah nichts im Fenster (nur headless): Spitze exakt auf dem Klickpunkt bei 48/64 px, Zustandswechsel (Ruhe, Zeigen, Tippen, Greifen), ?-Marke nur bei Tooltip, Rückfall unter 40 FPS, Einstellung an/aus, Overlay fängt keine Klicks, Umriss, Leder und Faltenfarbe, Licht und Blickwinkel (Kamera nach der Entwurfsansicht „zeiger“ geschätzt, Lichtstärken nach den Blender-Werten geschätzt), Tippen-Tempo 1,5 und Ziehschwelle 8 px sind Schätzwerte.
- (3D-Handschuh) Knöchelreihe nicht klar, Rücken weiter weich, Schlaufe unter dem kleinen Finger, Mulde Daumen/Handballen (Restliste v6, `bewertung_v6`); v7 hat dieselben Knochen- und Aktionsnamen, dann nur die .glb tauschen (Protokollzeile, `glb_faltenfarbe.py`). Die Handschuh-Wahl nach Stand (Farben) gilt für den 3D-Handschuh noch nicht: er hat eine Lederfarbe.
- (3D-Handschuh, Lizenz) Vom Teamleiter geklärt (10.11.2026): „Alle Rechte vorbehalten"; „Built with DINOv3" steht in der Danksagung. Offen nur noch: Steam-KI-Angabe bei Veröffentlichung.

- (Leistung) Mit 3D-Handschuh 26,6 statt 37,9 Bilder/s auf Xvfb/Vulkan (rund 4 ms GPU, Messung der NAS); der Nutzer lässt es vorerst so, später auf dem PC messen (Einstellung „3D-Handschuh“ schaltet ab).
- 3D-Handschuh 3b/3c: Hänger beim ersten und zweiten Betreten, Blende beim Verlassen (2D und 3D) und die Klinke-Folge (Bereit über der Tür, Greifen, Drücken, Blende, Loslassen; Schloss unter der Hand, Zeigerpunkt Anker_Schloss) sind im Klicktest der NAS zu prüfen (mit Grafik). Bekannte Schwäche aus 3a: die Faust sitzt auf der Stange, statt sie zu umschließen; im Klicktest ansehen.
- **Ratskammer ins Thema umstellen:** Der Reiter „Ämter“ (Ratsbaum) und die Kurzübersicht sind im Thema `rat_thema.tres`. Die Abschnitte der Reiter „Anträge“, „Stadtglaube“ und „Kasse“ und der Domprediger (`RatAnzeige.Antraege.cs`, `.Kasse.cs`, `.Dom.cs`, Statutantrag und Stadtglaube in `RatAnzeige.cs`) rufen noch `Hauptmenue.Text(…, Größe, Farbe)` auf; sie wandern im Fable-Neudesign in Varianten.
- Ratsbaum: Ein **freies Amt** (Schatten mit „?“) ließ sich im Klicktest nicht zeigen: Alle Sitze sind zu Beginn besetzt (Nachrücker). Der Schatten ist gebaut, aber nur im Spiel nach einem Rücktritt oder Tod zu sehen.
- (Zeiger-Symbole, Branch phase5/zeiger-symbole) Klicktest mit Grafik steht aus, belegt ist nur die Blender-Rechenvorschau (kein Spielbild): Stammbaum (Zeigehand mit Windrose, beim Ziehen bleibt die Hand zeigend), Textfeld im Hauptfenster und in einem Dialog/Popup (Hand schrumpft, Messing-I wächst, Einfügemarke zwischen den Buchstaben unter der Mitte des I, Rückweg beim Verlassen), 48/64/80 px, helle und dunkle Tafeln. Kollision mit `fix/testbefunde-1`: dort zeigt die Stammbaum-Fläche `CursorShape.Move` und der 3D-Handschuh die Faust; hier zeigt die Hand (Nutzerwunsch 14.11.), beide setzen Move/Drag in `Mauszeiger` (gleiche Zeilen).
- (Nur noch Bauzonen, Platzwahl) Mehrspieler: Die Platzwahl läuft dort bei laufender Zeit (kein Haltegrund); die Regel „alle wählen, dann beginnt die Zeit“ fehlt im Kern (siehe kern.md).
- (Nur noch Bauzonen, Platzwahl) Die Probepartie der Kartenwerkstatt („Karte ausprobieren“) und Starts mit Startoptionen losen das Haus des Spielers weiter; Tafel „Start“ und Hofstellen-Werkzeug der Kartenwerkstatt stehen noch (PR 3 Karten).
- (Nur noch Bauzonen, Platzwahl) Während der Wahl wirken die Knöpfe für Wohnhaus, Bericht und Hafen nicht (die Bautafel kommt sofort wieder), ohne Hinweis; die Betriebsleiste ist bis zur Werkstatt leer.
- (Bautafel, schon vor der Platzwahl) Die Zeichen „✔“ und „✗“ vor dem Zustand des Umrisses erscheinen im Klicktest als Kästchen (die Schrift hat sie nicht); betrifft Baumodus und Platzwahl.
- (von Kern, 16.11.2026, „nur noch Bauzonen“ Teil 4) Kartenwerkstatt, Tafel „Start“: Der Hof-Knopf, „Erweitert“, die Zeilen Hofstelle, Wohnhaus und Startplatz und die Gruppe „Hofstelle“ in den Eigenschaften sind entfernt (`Kartenwerkstatt.Hoefe.cs` und `Kartenwerkstatt.Hofstelle.cs` gelöscht, `Kartenwerkstatt.Start.cs`, `.Tafeln.cs`, `.Palette.cs` angepasst – begründete Ausnahme, der Kern kennt die Felder nicht mehr). Neu: Je KI-Haus die Knöpfe „Werkstatt“ (nur die Betriebsart, ohne Platz) und „Umlandbetrieb“ (Platz in der Karte); die Palette bietet keine Werkstatt- und Wohnhausplätze mehr. Bitte gegenlesen und das Aussehen nachziehen; die zwei Hofstellen-Punkte oben sind damit erledigt.
- (von Kern) „Härte der Zeit“ stellt seit Datenversion 94 auch den strengen Winter (15 / 20 / 30 % der Winter, Nachfragezuschlag 0,25 / 0,5 / 0,75). Der Tooltip der Auswahl unter „Neue Partie“ (`Hauptmenue.Besetzung.cs`) nennt bisher nur die Ernten; ein Halbsatz zu strengen Wintern fehlt. Keine neue Abfrage nötig.
- (3D-Briefe) Ein Brief, der das Blatt ganz streckt (ab etwa sieben Zeilen-Blöcken), ist bei 1600 × 900 rund 860 Pixel hoch und überdeckt dann den Zeitstrahl oben und die Betriebsleiste unten, bis er weggelegt ist; kurze Briefe (601 Pixel) lassen beide frei. Falls das stört: Tafel schmaler (`brief_gross_breite`) oder die Höhe früher begrenzen und rollen. Das Siegel des großen Briefs bricht sofort (Clip des Modells: Zeichen weg nach 0,07 s), sein Zeichen ist darum kaum zu sehen. Der Film mit `--write-movie` lief auf der vollen NAS nur mit 6 % der Echtzeit (Klicks ließen sich nicht takten); die Filme sind deshalb Bildschirmaufnahmen in Echtzeit.

## Entscheidungen

### Entwicklerfenster: Gebäude wählen (Branch phase5/feuer-gebaeudebrand)| Thema | Entscheidung | Datum ||---|---|---|| Blitz und Brand auf Klick | Knöpfe „Blitz auf Gebäude …“ / „Brand hier …“ im Reiter Darstellung, einmaliger Wählmodus mit Hinweis und Fadenkreuz, Esc bricht ab; siehe darstellung.md | 11.11.2026 |

### Rivalenwahl ohne Beruf (Branch phase5/kirchen-koeln, Prediger PR 1c)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anzeige | Die Zeile „Rivalen“ im Hauptmenü nennt nur noch die Häuser („Eisenbart und Eichwald“), nicht mehr die Werkstatt der Karte in Klammern: Seit der Startaufstellung (Kern, jeder Beruf einmal je Partie) werden Beruf, Volk und Glaube der KI-Häuser zu Partiebeginn gelost, die Klammer stimmte nicht mehr. Tooltip: „Ihr Beruf, Volk und Glauben werden zu Beginn gelost.“ Gewählt statt „(Beruf zufällig)“ hinter jedem Namen, weil das nur Zeilen verlängert. Die Rivalen-Daten der Karten bleiben unberührt. (Überholt: Die Zeile „Rivalen“ ist durch „KI-Gegner“ auf der Seite „Neue Partie“ ersetzt, siehe oben.) | 11.11.2026 |
| Später | Mehrspieler: gewählte Berufe für die anderen sperren über `Startaufstellung.FreieBerufe` (Kern). | 11.11.2026 |

### Entscheidungsbrief (PR #…)

| Thema | Entscheidung | Datum |
|---|---|---|
| Brief | `EntscheidungsbriefAnzeige` (Pergament wie `BriefAnzeige`, mittig über der Leiste, der gewöhnliche Brief bleibt rechts): Farbband und Siegel in „Entscheidungsviolett“ (`#7a3b5e`, eigenes Thema „entscheidung“ mit Feder), Porträt der Person (Forderungen der Unterwelt: Oberhaupt des Absenders), Beschreibung, Fristbalken mit Zahl („Noch 1:20 Std. …“, rot unter einem Viertel), ein Knopf je Wahl mit `Folgen` als Tooltip, „Später“, Blättern („Brief 1 von 3“, dringendste zuerst). Schließt nur über den Kern (die Hauptszene gleicht jedes Bild ab); bei „✗“ bleibt er stehen und zeigt den Grund. | 10.11.2026 |
| Pause | Nur im Einzelspiel (`Bote.Verzoegerung == 0`) und mit dem Haken „Bei Entscheidungen anhalten“ (Standard an), einmal je Frage und erst, wenn der Brief zu sehen ist; „Später“ lässt die Zeit weiterlaufen. Nie über Ratssitzung, Spielmenü, Einstellungen, Hilfe, Jahresbericht (Nutzer, 10.11.2026). | 10.11.2026 |
| Einstellung je Art | `Entscheidungseinstellungen` (Abschnitt `[entscheidungen]`): „Entscheidung“ oder „Automatisch“ (schickt `EntscheidungTreffen(nr, −1)`, danach nur ein Brief „Entschieden“). Standard „Entscheidung“; **Krankheit eines Kindes: Automatisch.** Der Kern trennt Kind und Oberhaupt nicht als Art, darum unterscheidet die Oberfläche über die Person (`Familienbuch.IstVolljaehrig`) und führt die Wahl `KrankheitKind` neben `Krankheit`. Schlägt eine automatische Antwort fehl, kommt die Frage als Brief. | 10.11.2026 |
| Feder-Siegel | Ein `Siegel` mit `Dauerhaft` (kein Rechtsklick, keine Frist), Zähler = offene Fragen, steht in der Ecke, solange der Brief nicht vorn liegt; Klick holt die beiseitegelegten Fragen zurück. Ton `meldung_entscheidung` (selbst synthetisiert, Protokollzeile). | 10.11.2026 |
| Frist verstrichen (selbst entschieden) | Läuft eine Frist ab (`Frist`, `Obergrenze`), kommt ein kurzer Brief „Frist verstrichen“ mit der gültigen Voreinstellung, damit nichts still entschieden wird; ausgenommen sind Antworten und hinfällige Fragen. Im Verlauf trägt jede Zeile der Art „Entscheidung“ eine kleine Feder. | 10.11.2026 |
| Mahnung | „Kredit aufnehmen“ öffnet die Banktafel (in der eigenen Werkstatt, wenn man nicht schon drin ist), „Betrieb verkaufen“ wählt den ersten eigenen Betrieb aus (Wegweiser, Nutzer 10.11.2026). Die Gauner-Tafel bleibt als zweiter Weg. | 10.11.2026 |

### Lexikon mit Bildern und Siegel im Verlauf (PR „Oberfläche: Siegel für die Geheimnisse“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Bild-Baustein | `Begriff` hat das Feld `bilder` (Liste von `art:name` oder `art:name\|Tooltip`; Arten `geheimnis` = Siegel, `zug` = Medaillon, `ware` = Waren-Knopf). `LexikonAnzeige.BilderZeigen` setzt sie als Reihe (`HFlowContainer`) zwischen Gruppe und Text: Einzelbild 72 px, mehrere 64 px, über acht 44 px. Fehlt die Datei oder die Angabe, steht nur der Text; es entstehen keine neuen Bilder. | 10.11.2026 |
| Einsatz | „Geheimnisse“ zeigt die vier Siegel, „Charakterzüge“ alle 25 Medaillons (je mit Tooltip-Name), jeder Eintrag der Gruppe „Waren“ von selbst seinen Waren-Knopf (Titel = Warenname, `Lexikon.BilderVon`; Einträge wie „Rohstoff“ ohne Knopf bleiben bildlos). | 10.11.2026 |
| Verlauf und Jahresbericht | Im Verlauf (Reiter im Bericht) steht vor jeder Meldung mit `GeheimnisArt` das Siegel (22 px, Tooltip „Ein Geheimnis – Art“). Der Brief „Jahresbericht“ und der Nachtbrief tragen den Kern der Meldung in ihrer `Briefzeile`; `BriefAnzeige` setzt daraus bei Mehrzeilen-Briefen das Siegel vor die Zeile (und den Knopf „Hinsehen“, wo es ein Ziel gibt). Die Tafel `JahresberichtAnzeige` (Rangliste) hat keine Meldungen. | 10.11.2026 |

### Geheimnis-Siegel (PR „Oberfläche: Siegel für die Geheimnisse“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Bilder | Vier runde Medaillen (128 px, Holzschnitt, KI wie die Lebensweise-Icons, Runde 2 der Entwürfe: Ehebruch zwei zerbrochene Ringe, Ketzerei brennender Scheiterhaufen mit Buch, Mord Totenkopf mit gekreuzten Dolchen, Schulden Geldbeutel auf offener Hand) in `game/assets/oberflaeche/geheimnisse/<art>.png`; Prompts in `docs/assets/prompts/geheimnis_siegel.md`, Lizenz „Alle Rechte vorbehalten“. | 09.11.2026 |
| Einsatz | Im Charakterfenster steht vor jeder Geheimniszeile das Siegel der Art (36 px, `CharakterAnzeige.GeheimnisSiegel`, mit Mipmaps; Tooltip: Beschreibung der Art). Fehlt die Datei, bleibt nur der Text. Test `GeheimnisSiegelTests`: für jede `Geheimnisart` PNG und Importdatei. | 09.11.2026 |
| Meldung und Brief | Der Kern gibt jeder Meldung über ein Geheimnis die Art mit (`Meldung.GeheimnisArt`, siehe kern.md „Geheimnis-Siegel in Meldungen“). Am Siegel der Ecke steht die Medaille (30 px) rechts neben dem Porträt (`Hauptszene.SiegelBild`; ohne Porträt allein unten links); ein Brief mit einer solchen Meldung trägt unten die Medaille (56 px) statt des gezeichneten Wachssiegels, ein Brief mit mehreren Zeilen sie klein vor der Zeile (30 px). Tooltip: Name und Beschreibung der Art (`BriefAnzeige.GeheimnisTipp`). Gemeinsames Bild `CharakterAnzeige.GeheimnisSiegelBild(art, groesse, tipp)`. | 09.11.2026 |
| Meldungsart „Geheimnisse“ (selbst entschieden) | Die Meldungen über Geheimnisse waren „Sonstiges“ und standen nur im Verlauf – ein Siegel hätte man nie gesehen. Neue Art `Geheimnis` in `meldungen.json` (Thema Gefahr, eigen Siegel, Stadt und fremd Verlauf, Absender „Euer Vertrauter“, Feld `"geheimnis": true`, keine Textmuster): `Meldungsordnung.Einordnen(meldung)` nimmt sie für Meldungen mit Art, die weder eine Art des Kerns noch ein Textmuster trifft (ein neues Geheimnis, Gerede, Beichte, der Satz zum Erpresserbrief, erpresste Ratsstimme, Bloßstellen, Unfriede im Haus). Urteile und Spitzelberichte bleiben bei Gericht und Spitzel (Jahresbericht), die Anzeige bei Gericht. In den Einstellungen wählbar wie jede Art. Zurücknehmen: die Stufe in `meldungen.json` auf `verlauf`. | 09.11.2026 |
| Hinterzimmer | Ein Erpresserbrief mit Geheimnis (`Geheimniswesen.ArtDesBriefes`) steht mit Medaille (56 px) links: „Erpresserbrief von X: … Schweigegeld, sonst kommt ein Geheimnis ans Licht.“ und „Er weiß: <Inhaber: Art>. Zahlt ihr, schweigt er und vergisst es. Zahlt ihr nicht, kann er es anzeigen oder in der ganzen Stadt bloßstellen.“ (`GaunerAnzeige.GeheimnisBrief`; vorher stand dort „sonst Anklage“ mit den Beweisen). Brief mit Beweisen unverändert. | 09.11.2026 |
| Startoption | `--erpresserbrief=<Ehebruch|Ketzerei|Schulden>[:Betrag]` (nur mit `--entwickler=1`): das erste andere Haus erfährt über `EntwicklerGeheimnis` ein Geheimnis eures Oberhaupts, bekommt Aktionspunkte (`EntwicklerAktionspunkte`) und schickt den Brief (`GeheimnisNutzen`) – alles Befehle des anderen Hauses; schreibt ins Log (`--erpresserbrief: …`, auch die Zahl der Siegel mit Geheimnis) und baut ein offenes Hinterzimmer neu auf. Headless geprüft: Brief mit Art, ein Siegel mit Geheimnis. | 09.11.2026 |

### Danksagung (PR „Oberfläche: Danksagung mit allen Lizenzhinweisen“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ort und Aufbau | Knopf „Danksagung“ im Hauptmenü (`DanksagungAnzeige`), rollbare Menütafel in fünf Gruppen: Spiel, Engine & Bibliotheken, Klänge, Bilder & Modelle, Schriften; zweiter Knopf „Lizenztexte der Engine“ (Texte von Godot selbst). Menütafel im bisherigen Stil, noch nicht im Buch-/Schriftrollenstil (der Umbau steht aus). | 31.10.2026 |
| Daten | `game/daten/danksagung.json` wird von `tools/assets/danksagung_bauen.py` aus `tools/assets/danksagung_quelle.json`, der Freesound-Tabelle in `game/assets/audio/umgebung/HERKUNFT.md` und dem Protokoll gebaut; `protokoll_pruefen.py` (CI) wird rot, wenn Fremdmaterial fehlt oder die Datei veraltet ist. Ablauf: `docs/assets/README.md`. | 31.10.2026 |
| Namensnennung | CC BY, MIT, OFL und Apache mit Werk, Urheber, Adresse und Lizenz einzeln; CC0 (Kenney, Quaternius, ambientCG, Freesound-Urheber) als freiwilliger Dank. Wortlaut „mit Unterstützung von Claude (Anthropic)“ ist ein Vorschlag, der Nutzer entscheidet. | 31.10.2026 |

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

### Benachrichtigungen Stufe 2: Porträt, Hinsehen, Verlauf aus dem Spielstand (PR #339)

| Thema | Entscheidung | Datum |
|---|---|---|
| Zuordnung | Art des Kerns, wenn eine Art in `meldungen.json` sie unter `kern` nennt; Feuer, Überfall, Ratssitzung und Sonstiges weiter über die Textmuster (Blitz/Anschlag bzw. Steuer/Rat liegen in verschiedenen Arten). Betroffenheit aus dem Haus der Meldung, ohne Haus wie Stufe 1. | 06.11.2026 |
| Porträt | Person der Meldung, sonst bei Anklage, Schulden, Meisterprüfung und Aufstieg das Oberhaupt; am Siegel klein unten links (fängt keine Klicks), im Brief 64 px mit Name und Haus (Klick: Charakterfenster). | 06.11.2026 |
| Hinsehen | Knopf im Brief (ersetzt „Zu <Betrieb>“), je Zeile in gestapelten Briefen, Klick auf eine Zeile im Verlauf: Kamera hin, eigener Betrieb → Kurzübersicht, fremdes Gebäude → Auswahl, eigenes Wohnhaus → Tafel, Blitzschlag → Ort; ohne Ort Charakterfenster der Person. Drinnen erst hinaus. | 06.11.2026 |
| Verlauf | Nach dem Laden aus dem Spielstand (500 Meldungen des Kerns), ohne Siegel und Briefe neu auszulösen. | 06.11.2026 |

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
### Geheimnisse im Charakterfenster (PR „Kern: Charaktersystem Teil 3c – Geheimnisse“, vom Kern mitgebaut)

| Thema | Entscheidung | Datum |
|---|---|---|
| Abschnitt | `CharakterAnzeige.Geheimnisse.cs`: „Geheimnisse“ unter den Zügen, bei Personen und Ratsherren; gezeigt wird nur `Geheimniswesen.Sichtbar(spieler, person)` – eigenes Haus „Ihr hütet: Ehebruch (seit 1403)“ (rot, mit „der Ehepartner weiß es“ bzw. „die ganze Stadt weiß es“), sonst „Ihr wisst: …“ (gold), „Aus der Beichte: …“, „Die ganze Stadt weiß: …“ (grau). Tooltip: Beschreibung der Art, woher und seit wann, Beweiskraft und Buße bzw. „kein Vergehen“ (bei Ketzerei „nur wer sich nach außen zum Stadtglauben bekennt“ und, wenn es hier nicht strafbar ist, der Grund aus `KeinVergehen`), was Bloßstellen kostet, Verweis `[[geheimnisse]]`. Nichts bekannt: der Abschnitt fehlt (beim Ratsherrn steht „Ihr wisst nichts …“ mit dem Spitzel-Knopf). | 09.11.2026 |
| Knöpfe | Bei nutzbarem Wissen: Erpressen (mit `Zahlfeld` 50–20.000 Gulden, nur bei Personen), Ratsstimme, Anzeigen, Bloßstellen – Befehl `GeheimnisNutzen`; bei Beichtwissen „Beichtgeheimnis brechen“ (`BeichtgeheimnisBrechen` mit Haus bzw. „R:Name“); beim Ratsherrn „Spitzel ansetzen“ (`SpitzelAnsetzen("R:Name")`). Ausgegraut mit dem Grund aus dem Kern (`NutzenVerboten`, `RatsspitzelVerboten`) oder „Nicht genug Aktionspunkte“; sonst die AP im Tooltip. Ergebnis als Meldung über dem Abschnitt, das Fenster baut neu auf. Kein Bestätigungsdialog (wie die übrigen Befehle im Fenster). | 09.11.2026 |
| Ratssitzung | Die Begründung eines erpressten Sitzes heißt „Erpresst von Haus X – stimmt wie Haus X.“ bzw. „– stimmt für ihn.“ (aus `Ratssitzung` im Kern); ein erpresstes Haus des Spielers ist im Punkt nicht frei, wo der Erpresser die Stimme bestimmt (`Stadtrat.StimmeVerboten`, `WahlStimmeVerboten` mit Grund), sonst stimmt es wie gewohnt. Gerichtssaal: Ein Verfahren nennt seine Anklagepunkte über `Stadtgericht.Anklagepunkte(v)` (mit dem angezeigten Geheimnis, sonst wie bisher). | 09.11.2026 |
| Lexikon, Entwicklerfenster, Startoption | Lexikon-Eintrag „Geheimnisse“ (Gruppe Haus und Familie) mit `{wert:geheimnis_spitzel}`, `geheimnis_spitzel_ratsherr`, `geheimnis_gerede`, `geheimnis_beichte`, `geheimnis_ansehen_jahre`, `geheimnis_busse`, `geheimnis_mord_busse`. Entwicklerfenster, Reiter „Familie und Ereignisse“: Zeile „Geheimnis“ mit Ehebruch, Ketzerei, Schulden (jeweils mit Wissen) für die gewählte Person. Startoption `--geheimnis=<Art>[:oberhaupt|ehepartner|rivale|ratsherr[:n]|Nummer]` und `--charakter=rivale` (`Hauptszene.Charakter.cs`). | 09.11.2026 |
| Bilder | Keine: je Art nur Text (Platzhalter, nichts lädt ein Bild). Siehe Offen. | 09.11.2026 |

### Offen (von Oberfläche, Hofstellen)

- Kamera im Spielstand speichern (Kern: `Speicherstand.cs`), damit das Laden den letzten Blick wiederherstellt statt den Kartenblick.

### Offen (von Oberfläche, Stammbaum)

- Designfrage: Frühere Ehen ohne Kinder und die Jahre früherer Hochzeiten, Ämter, Geheimnisse gehen aus der Chronik verloren, sobald sie aus dem Verlauf (500 Meldungen) fallen. Empfehlung: eine kleine, dauerhafte Hauschronik im Kern (nur Hochzeit, Amt, Stand, Geheimnis entdeckt, Entscheidung mit Jahr und Person; wenige Dutzend Zeilen je Haus, eigener Abschnitt im Spielstand wie `Meldungen`, ohne Wirkung auf die Simulation). Das braucht kern (Spielstand), deshalb nicht in diesem PR.

### 3D-Handschuh größer und flinker, kein FPS-Rückfall (Branch phase5/handschuh3d-2)

| Frage | Entscheidung | Stand |
|---|---|---|
| Größe | Bildgröße 1,5 auf 2,0 Zeigergrößen, die Hand wächst auf das 1,35-fache und ist so groß wie das 2D-Bild (2D-Hand 44 × 40 px bei 48, 59 × 54 bei 64, 74 × 68 bei 80; Spitze weiter exakt auf dem Klickpunkt) | 11.11.2026 |
| Fragezeichen | `Hilfe_Marke` und Umriss 1,9-fach um ihre Mitte, ein Stück von der Hand weg gerückt (`MarkeFaktor`, `MarkeVersatz`); Lesbarkeit bei 48 px steht im Klicktest der NAS aus | 11.11.2026 |
| Tempo | Übergänge 1,0 auf 1,8, Tippen 1,5 auf 2,7, Blend 0,06 auf 0,03 und 0,10 auf 0,05 s, Ziehschwelle 8 auf 5 px; als Konstanten in `HandschuhModell`, Cliplängen weiter aus der Datei | 11.11.2026 |
| Kein Rückfall nach Bildrate | Der Spieler entscheidet allein über die Einstellung; `HandschuhLeistung` samt Test entfernt, `HandschuhModell.Gilt` mit Test; Rückfall nur bei fehlender Datei | 11.11.2026 |

### Schwarzblende und Haltung Text (3D-Handschuh Teil 3b)

| Thema | Entscheidung | Datum |
|---|---|---|
| Zweck | Das Nachladen beim ersten Betreten eines Innenraums liegt im Schwarzen: Klinke (nur 3D), Blende ins Schwarz 0,4 s, Raum in Schritten aufbauen (je Bild einer), erst herausblenden (0,4 s), wenn der Raum fertig ist **und** 2 Bilder gezeichnet hat; ebenso beim Verlassen. Gilt in 2D und 3D, aus mit `--blende=0`. Zustandsautomat `InnenraumUebergang` ohne Godot mit Tests | 11.11.2026 |
| Vorladen | Hover über einem betretbaren Gebäude bereitet den Raum vor (`InnenAuffuellen` im verborgenen Raum), aber erst nach dem ersten aufgebauten Raum: der erste kostet etwa 140 ms (Messung headless) und gehört ins Schwarz, nicht in den Hover. Ressourcen sind schon beim Stadtbau geladen (`Vorladen.cs`), die Räume dort gebaut; was beim ersten Betreten bleibt, ist die GPU-Seite (Shader, Puffer), die jetzt unter dem Schwarz liegt (Messung der NAS) | 11.11.2026 |
| Klinke | Haken `Uebergangsblende.KlinkeStarter` (liefert die Sekunden bis zur Blende oder null) und `KlinkeLoslasser`; angeschlossen in 3c (siehe unten) | 11.11.2026 |
| Haltung Text | `HandPose.Text` über Steuerelementen mit I-Strich (`Control.GetCursorShape`), Zeigerpunkt `Anker_Text` gleitet von der Fingerspitze (0,08 s); Greifen und Text nur über Ruhe; der System-I-Strich wird bei 3D unsichtbar. Übergangsclips `Uebergang_<von>_<nach>`, ersatzweise `<von>_<nach>` | 11.11.2026 |
| Eingaben | Die Fläche der Blende fängt Klicks ab, `Stadtansicht._UnhandledInput` ignoriert sie, solange ein Übergang läuft; programmatisches Betreten (Startoptionen, Meldungen, Briefe) bleibt sofort | 11.11.2026 |

### Klinke-Folge des 3D-Handschuhs (3D-Handschuh Teil 3c)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ablauf | Hover über einem betretbaren Gebäude (nur 3D, nicht im Raum): Haltung **Bereit** (`Klinke_bereit`, offene Hand) statt Zeigen, das Türschloss (`tuerschloss_zeiger.glb`) wächst gleitend unter der Hand ein. Doppelklick: `Klinke_greifen`, `Klinke_druecken` (Hand und Schloss-Aktion `Druecken` zugleich), Blende ins Schwarz, sobald 80 % des Drückens vorbei sind (Rest ist Halten); die Hand bleibt gedrückt, bis der Raum offen ist (Beginn des Herausblendens) oder abgebrochen wird, dann `Klinke_loslassen` (endet in Bereit). Ohne 3D-Handschuh gilt der alte Ablauf mit Blende | 11.11.2026 |
| Zeigerpunkt | Bei Hover über der Tür und in der Folge ist der Zeigerpunkt die Schlossmitte (`Anker_Schloss`), gleitend (0,1 s) von der Fingerspitze überblendet, damit Maus und Hand nicht springen | 11.11.2026 |
| Lage des Schlosses | Im Handraum bei (0,0419 / −0,090 / −0,0585), um −40° um die Hochachse gedreht (Schnittstelle 3a), im selben SubViewport und Neigungsknoten wie die Hand, damit Tiefe und Neigung stimmen | 11.11.2026 |
| Klick auf die Tür | Aus der Haltung Bereit tippt die Hand nicht (`HandschuhAblauf`); die Folge startet nur, wenn die Hand schon in Bereit steht (sonst läuft die Blende allein). Klinke-Clips laufen mit Tempo 1, damit sie so lang sind wie die Aktionen am Schloss; Greifen und Bereit liegen nicht nebeneinander (Weg über Ruhe) | 11.11.2026 |
| Logik und Test | `KlinkeFolge.cs` ohne Godot mit `KlinkeFolgeTests.cs`; Startoption `--klinketest=1` (Log `[klinketest]`, `[klinke]`) | 11.11.2026 |

### Klick-Regel, Haltung Text aufrecht, Schloss daneben (Nach dem NAS-Klicktest des Gesamt-Branchs)

| Thema | Entscheidung | Datum |
|---|---|---|
| Klick-Regel | Der erste Klick öffnet weiter sofort die Kurzübersicht; erkennt das Spiel den Doppelklick auf ein betretbares Gebäude, schließt es die Tafel wieder (`Stadtansicht.GebaeudeDoppelklick`, `Hauptszene` ruft `AuswahlSchliessen`) und betritt das Haus mit Klinke und Blende. Logik `KlickRegel` mit Tests (Einzelklick bleibt offen; Doppelklick ohne Innenraum zeigt die Tafel; während eines Übergangs zählt kein Klick) | 11.11.2026 |
| Haltung Text aufrecht | (korrigiert nach NAS-Runde 2: die geratene Drehung um −40° stellte die Hand kopfüber; jetzt wird der Winkel beim Laden aus der Pose berechnet, derzeit etwa 144°, `HandschuhModell.AufrechtDrehungGrad`, Log `[handschuh] Text aufrecht`: Stulpe y 40, Fingerspitze y 9, Anker_Text y 10 im Bild, ohne Drehung Finger unten.) Früher: Im Spiel stand die Hand wegen der 40°-Drehung der Bühnenkamera schräg. Der Neigungsknoten dreht in der Haltung Text mit der Kamera (`HandschuhModell.PoseDrehungGrad`, um die Blickmitte der Bühne, gleitend mit `TextGewicht`); Pose und glb bleiben, der Zeigerpunkt `Anker_Text` zieht mit. Der Rechenbeleg im Test: Finger im Bild 40° bei Zeigen, 0° bei Text. Wie es im Bild aussieht, sieht erst der Klicktest (kein Bild headless) | 11.11.2026 |
| Schloss und Zeigerpunkt mit der Hand | Der Nutzer hat die Haltungen Bereit/Greifen/Gedrückt selbst in Blender gestellt (Bereit weiter rechts unten): das Overlay versetzt nichts zusätzlich (`SchlossLage` unverändert (0,0419 / −0,090 / −0,0585), −40°). Schloss-Einwachsen (Skala 0 bis 1 um `Anker_Schloss`) und Gleiten des Zeigerpunkts zu `Anker_Schloss` laufen mit `SchlossEinwuchs`: dieselben 7 Bilder bei 24/s = 0,2917 s und dieselbe Smoothstep-Kurve wie die Bereit-Übergänge der Hand, im selben Bild gestartet wie der Übergangsclip (Eintritt 1, Austritt 0, Umkehr mitten drin ohne Sprung). Die Länge kommt aus der Datei. Rechenvorschau (`--klinketest=1`, Zeigergröße 48, ohne Bild): Langschild x 47..59, Hand x 63..91, Klinke frei darüber; beides zu 100 % unverdeckt nach der Kapselnäherung der Hand, im Spiel ungesehen. Beim Drücken verdeckt die Faust das Schloss; die Hand-Clips bleiben, der Nutzer bessert den Daumen selbst nach | 11.11.2026 |

### Auswahlrechteck, Bereit lebt, Handschuh winkt (3D-Handschuh, Nutzerwünsche 11.11.2026)

| Thema | Entscheidung | Datum |
|---|---|---|
| Auswahlrechteck | Zieht der Spieler in der Welt ein Auswahlrechteck (`Stadtansicht.RahmenZieht`), zeigt der Handschuh Zeigen statt der Ziehen-Faust; Dinge greifen und verschieben (Ware, Fenster, Kamera) bleiben bei der Faust (`HandschuhModell.Wunsch` mit `rahmen`). Ein Klick, der zum Ziehen wird, tippt nicht mehr | 11.11.2026 |
| Bereit lebt | Schleife `Bereit_atmen` (fehlt sie, bleibt `Klinke_bereit`); zufällig alle 3 bis 6 s der Clip `Bereit_zucken`, nur in Bereit ohne Übergang und Klinke, jeder Klick unterbricht sofort (`BereitZucken`). Zufall nur in der Darstellung (`RandomNumberGenerator`, nicht die Zufallsquelle des Kerns). Die neuen Clips kommen von Teil 3a; bis dahin tut der Code ohne sie nichts | 11.11.2026 |
| Winken | Nach 60 s ohne Eingabe (Maus, Taste, Rad) dreht sich die Hand zur Kamera und winkt (Clip `Winken`, `Uebergang_<Haltung>_Winken`/`Uebergang_Winken_<Haltung>`), geht zurück in die vorige Haltung, wiederholt sich nach 120 bis 180 s ohne Eingabe; jede Eingabe bricht ab und setzt die Zeit zurück. Überall (auch Hauptmenü, offene Tafeln), außer über oder in Textfeldern und während Blende oder Klinke (`WinkenLogik`). **Nicht abschaltbar** (Nutzer). Prüfhilfe `--winktest=1` | 11.11.2026 |
| Tempo der Übergänge | Übergangsclips von und nach Bereit und Winken sind schon auf 7 Bilder bei 24/s gebaut und laufen mit Tempo 1 (die älteren mit 1,8); vorher liefen die Bereit-Übergänge fälschlich mit 1,8 und das Schloss wuchs länger als die Hand | 11.11.2026 |

### Klinke oben in Ruhe (NAS-Runde 2)

| Thema | Entscheidung | Datum |
|---|---|---|
| Klinke beim Einblenden | In `tuerschloss_zeiger.glb` steht der Knoten `Klinke` in der Datei auf −28° (Endlage von `Druecken`), darum stand sie beim Einblenden schon unten. Der Spielcode stellt sie beim Laden auf 0° (Ruhelage oben) und in Bereit, im Übergang und beim Greifen nach (`KlinkeFolge.KlinkeInRuhelage`); `Druecken` läuft nur im Drücken, `Loslassen` im Loslassen. Log `[handschuh] Türschloss: Klinke in der Datei bei -28,0 Grad, gestellt auf 0,0`. Der Rest-Wert des Knotens in der Glb wäre Sache von Teil 3a (Ruhe = 0°, wie in der Schnittstelle beschrieben) | 11.11.2026 |

### Handschuh über Popups (NAS-Runde 2, dritter Fehler)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ursache | Unterfenster (`PopupMenu` der OptionButtons, Dialoge) liegen bei eingebetteten Fenstern im Hauptfenster über allen CanvasLayern, also auch über dem Handschuh (Ebene 128); der System-Zeiger ist unsichtbar gestellt, der Zeiger verschwand | 11.11.2026 |
| Lösung | Jedes Fenster außer dem Hauptfenster bekommt eine eigene Ebene 128 (`HandschuhSpiegel`) mit demselben Bild der Bühne (geteilte Textur), gezeichnet an der Mausposition des Fensters; angelegt über `SceneTree.NodeAdded`, aktiv nur bei sichtbarem Fenster. Kein Systemzeiger als Ersatz. Headless nachgewiesen: 87 Fenster (PopupMenus) bekommen die Ebene (Log `[spiegel]`); Test der Lagerechnung (`HandschuhModell.FensterPosition`). **Kein Spielbild**; auf der NAS zu prüfen: OptionButton Volk in der Charakterwahl offen, PopupMenu, Dialog, Kontextmenü, Tooltip (Hand über dem Popup sichtbar, Fingerspitze genau auf dem Mauspunkt, auch am Rand des Popups) | 11.11.2026 |
| Offen | Die Mausposition im Popup kommt von `Window.GetMousePosition()`; sie ist nur aktuell, solange das Fenster Mausbewegungen bekommt (Maus über dem Popup). Reicht die Hand über den Rand des Popups hinaus, kann der Teil im Popup einen Moment an der letzten Position stehen; ungesehen | 11.11.2026 |

### Handschuh 64 px, schnellere Faust (PR #…)

| Thema | Entscheidung | Datum |
|---|---|---|
| Größe | Nutzerwunsch: der Zeiger ist bei 1080p jetzt 64 statt 48 Pixel (`Mauszeiger.GroesseWaehlen`: unter 0,75 → 48, unter 1,35 → 64, sonst 80). Bühne, Zeigerpunkt, Spiegel-Ebenen und „?“ leiten sich aus der Größe ab und wachsen mit | 11.11.2026 |
| Prüfung | Ohne Klicktest auf Wunsch des Nutzers | 11.11.2026 |
| Faust | Übergänge in die Ziehen-Faust und zurück (`Uebergang_*_Greifen`, `Greifen_*`) auf 0,12 s gekürzt: Tempo = Cliplänge der Datei / 0,12 (`HandschuhModell.FaustUebergangSekunden`), glb unverändert. Vorher (Tempo 1,8): Ruhe→Greifen 0,69 s, Greifen→Ruhe 0,56 s, Zeigen↔Greifen 0,28 s | 11.11.2026 |

### Ratskammer mit Reitern (Branch phase5/ratskammer, Ratssitzung und Ratsbaum PR 2)

| Thema | Entscheidung | Datum |
|---|---|---|
| Reiter | Die Ratskammer hat vier Reiter: Ämter (vorerst das alte Raster mit Laufbahn, Wahlstimme und Domprediger; der Baum kommt in PR 3), Anträge (Tagesordnung, Statut, Wegbau, letzte Sitzung), Stadtglaube (nur mit Glaubenssystem), Kasse (nur mit Stadtkasse; Haushalt, Bauten, Fest, Anträge zu Steuer, Bau, Fest). Der Reiter bleibt beim Aufbau erhalten. `Oeffnen(Reiter?)`, `--rat=1\|aemter\|antraege\|stadtglaube\|kasse`. | 14.11.2026 |
| Kurzübersicht | Die RathausTafel hat keine Reiter mehr, sondern drei Felder: Stadtkasse (Stand, Steuersatz, Knopf „Kasse ansehen“), Nächste Ratssitzung (Zahl der Anträge, Knopf „Anträge und Stimmen“, „Letzte Ratssitzung ansehen“), Euer Amt (die Befugnis wie bisher, bis der Baum sie in PR 3 an den eigenen Knoten holt, F7). Der Haushalt und die Bauten der Stadt zogen in den Reiter Kasse. `--rathaus=1` wählt das Rathaus (der Reitername entfällt). | 14.11.2026 |
| Bewerbung zurückziehen | Knopf „Bewerbung zurückziehen“ unter der eigenen Bewerbung (Befehl `BewerbungZurueckziehen`, ohne AP-Erstattung, der Tooltip sagt es). | 14.11.2026 |
| Zentrales Thema | Aussehen steht in `game/scenes/rat_thema.tres` mit Typ-Varianten (RatTafel, RatReiter, RatTitel, RatAmtknopf …), `RatStil.cs` ist die Brücke; Aufbau in ARCHITEKTUR.md, Abschnitt „Das Thema der Ratskammer“. Gilt für alle neuen Elemente; die älteren Abschnitte der Ratskammer sind noch nicht umgestellt. | 14.11.2026 |

### Tooltips der Talente mit Zahlen (Branch fix/attribut-tooltips)

| Thema | Entscheidung | Datum |
|---|---|---|
| Was „Attribut“ ist | Die Attribute des Charakters sind die fünf **Talente** (Verhandeln, Handwerkskunst, Bei Nacht und Nebel, Kampf, Rhetorik). Jedes hat im Charakterfenster und in der Familienübersicht (Oberhaupt-Zeile, je Talent ein eigenes Feld) einen Tooltip mit einem Satz, der Wirkung je Stern und der Wirkung beim jetzigen Stand („+3 % je Stern (jetzt 4 Sterne: +12 %)“). | 14.11.2026 |
| Woher die Zahlen | Keine Zahl ist abgeschrieben: `Talentwirkungen.Zeilen` (Kern, nur lesend) liefert sie aus denselben Daten, mit denen `Familienbuch`, Geheimnis-, Glaubens- und Kirchenwesen rechnen; `TalentTipp` (Oberfläche) setzt den Text. Ein Test (`TalentwirkungenTests`) hält Tooltip und Simulation gleich. Wirken tut nur das Talent des Oberhaupts; bei anderen Personen steht „Als Oberhaupt: …“. | 14.11.2026 |
| Was nicht „Qualität“ ist | Handwerkskunst erhöht die **Leistung** (Arbeitstempo) aller Arbeitskräfte, nicht die Warenqualität; der Tooltip sagt es so. | 14.11.2026 |
| Charakterzüge | Der Tooltip hatte schon Beschreibung und Wirkungstext aus den Daten; neu steht die Meinungswirkung (`Charakterzug.Meinungstext`: Punkte über Träger desselben Zugs, des Gegenteils und bei allen anderen) dabei. | 14.11.2026 |
| Aussehen | Die Tooltips laufen über den Autoload `Tooltips` (Tafel, Schrift, Festheften). Es gibt kein Tooltip-Theme; Zahlen werden nicht farbig hervorgehoben, weil `Tooltips` BBCode entschärft. Eine Hervorhebung wäre eine Änderung am Autoload, nicht an der Anzeige. | 14.11.2026 |

### Platzwahl am Start (Branch phase5/nur-bauzonen-oberflaeche)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ablauf | Eine Partie aus dem Menü beginnt mit `NeuePartie(Startwahl: true)`: Die Bautafel zeigt „Euer Platz in der Stadt“, Schritt 1 Wohnhaus, Schritt 2 Werkstatt des Berufs, mit dem Umriss des Baumodus (grün/rot mit Grund, R dreht, Fahrweg-Marken bei der Werkstatt). Erlaubt ist, was der Kern erlaubt (alle Bauzonen, wie ein Neubau). | 16.11.2026 |
| Rückfrage | Ein Klick öffnet wie beim Neubau die Rückfrage, hier ohne Rechnung und Bauzeit, Knopf „Platz wählen“ (der Platz lässt sich danach nicht mehr verschieben). | 16.11.2026 |
| Zeit | Im Einzelspiel ruht die Zeit über den Haltegrund „die Platzwahl“ schon vor dem ersten Tick; Pause und Tempo sind gesperrt, der Tooltip der Pause nennt den Grund. | 16.11.2026 |
| Kein Abbruch | Die Tafel hat kein „Zurück“ und keinen Wege-Modus; Esc öffnet das Spielmenü. Schließt etwas die Tafel, öffnet sie sich wieder. | 16.11.2026 |
| Menü | Die Zeile „Hofstelle“ unter „Erweitert“ ist entfernt; die Kennzahlen der Kartenwahl nennen keine Höfe und Werkstattplätze mehr (höchstens n KI-Gegner, Fluss, Hafen, Maße). | 16.11.2026 |
| Startoptionen | Ohne Menü wird das Haus gelost wie bisher (Klicktests behalten ihren Betrieb); `--startwahl=1` verlangt die Wahl (`--bauort2` setzt im Klicktest den Umriss der Werkstatt). `--hofstelle` wird nicht mehr gelesen. | 16.11.2026 |
| Kamera und Hinweis | Mit Platzwahl beginnt der Blick im Blick der Karte; der Brief zum Personalbuch kommt nach der Wahl. | 16.11.2026 |

### 3D-Schriftrollen in der Meldungsleiste (Branch phase5/schriftrolle-meldungen)

| Thema | Entscheidung | Datum |
|---|---|---|
| Weg 1 | Die 3D-Schriftrolle ersetzt schon jetzt die Wachssiegel in der heutigen Ecke unten rechts; der Umbau zur Zunftlade kommt später (Nutzer). Stufe, Einstellungen und Code heißen weiter „Siegel“. | 17.11.2026 |
| Verhalten | Neue Meldung: Rolle erscheint geschlossen, rollt auf (Clip `aufrollen`) mit einem kurzen Sprung, dann steht das Symbol mit farbigem Rand der Meldungsart darauf. Sie bleibt offen, bis die Meldung erledigt ist; dann nur `zurollen` und weg, ohne Sprung (Nutzer, zweite Runde). Klick wie bisher. | 17.11.2026 |
| Neigung und Leerlauf | 14° schräg nach oben zum Spieler, fest. Im Leerlauf dreht sich die offene Rolle nur um die Hochachse Y nach links und rechts, höchstens 2°, je Rolle eigene Phase und eigenes Tempo; X und Z bewegen sich nicht (Nutzer, zweite Runde; vorher alle drei Achsen bis 5°). | 17.11.2026 |
| Eine Bühne | Ein gemeinsamer SubViewport für alle Rollen (12 Plätze), eigene Szene `schriftrolle_buehne.tscn`; Maße, Zeiten und Farben im Thema `meldung_thema.tres`, nichts davon im Code. | 17.11.2026 |
| Symbol | Viereckige Marke auf der Symbolfläche: Rand in der Farbe der Art, heller Grund, schwarzer Umriss, Schraffur, Zeichen in Tinte (dieselben Zeichen wie die Siegel). | 17.11.2026 |
| Abschalten | Kein neuer Schalter: „Siegel und Briefe bewegen“ (Einstellungen, Meldungen) gilt auch für die Rollen – aus: sofort offen, still, ohne Zurollen. | 17.11.2026 |
| Pause und Tafeln | Die Rollen gehören zur Oberfläche und laufen unabhängig vom Spieltempo weiter, auch wenn das Spiel pausiert; ist die Ecke verdeckt (große Tafel, Ratssitzung), ruht die Bühne. | 17.11.2026 |
| Unverändert | Größe des Feldes (56 Pixel), höchstens sieben Meldungen, Zähler, Tooltip; das Porträt sitzt etwas tiefer und weiter links, damit es das Zeichen nicht verdeckt; die zentrale Einblendung und der Fuß der Briefe zeigen weiter das Wachssiegel. | 17.11.2026 |

### 3D-Briefe für Meldungen (Branch phase5/briefe-meldungen)

| Thema | Entscheidung | Datum |
|---|---|---|
| Briefe statt Rollen | Um 1400 gab es gefaltete Briefe mit Wachssiegel, keine Rollen: In der Meldungsleiste liegt statt der Schriftrolle ein kleiner Faltbrief (`brief_klein.glb`). Die Schriftrolle bleibt im Spiel (Modell, Bühne, Ablauf, Thema-Maße `rolle_…`); die Bühne kennt jetzt beide Modelle (Nutzer). | 17.11.2026 |
| Siegel | Das Wachs trägt die Farbe der Meldungsart (wie bisher Rand der Marke), das Zeichen ist eingedrückt (Wachs um 60 % abgedunkelt; dieselben Zeichen wie die gezeichneten Siegel). Weil das Siegel im Modell in der Leiste nur etwa 13 Pixel groß wäre, vergrößert die Oberfläche es um seine Mitte auf 175 % (Maß `brief_siegel_prozent`; selbst entschieden, damit Farbe und Zeichen lesbar sind). | 17.11.2026 |
| Verhalten in der Leiste | Erscheinen mit leichtem Hüpfen; Leerlauf nur Drehung um Y, höchstens 2°, je Brief zeitversetzt; Weggehen ohne Hüpfen (kurzes Ausblenden). Neigung 12° nach oben zum Spieler, fest (Nutzer). | 17.11.2026 |
| Öffnen | Klick: Der große Brief (`brief_gross.glb`) erscheint zu in der Bildmitte, das Siegel bricht, er faltet sich auf (0,9 s). Ab 0,733 s liegt der Inhalt als Bild auf der Inhaltsfläche, offen liegt genau darüber die echte 2D-Tafel (scharfer Text, Knöpfe). Erledigt (Weglegen, ✕, Esc): zufalten ohne Hüpfen und weg (Nutzer). | 17.11.2026 |
| Ein Fenster | Es bleibt bei dem einen Brief-Fenster: Auch Briefe, die von selbst kommen (Stufe „Brief“, nach der Einblendung), falten sich jetzt in der Bildmitte auf statt rechts unten aufzugehen (selbst entschieden; dem Teamleiter gemeldet). Stapel, Blättern, Aufschub während der Ratssitzung und alle Knöpfe wie bisher. | 17.11.2026 |
| Größe und Strecken | Die Tafel ist 372 Pixel breit (= Inhaltsfläche 0,26 m; das Blatt damit 429 × 601 Pixel bei 1600 × 900, früheres Fenster 484 Pixel breit). Lange Briefe strecken das Blatt bis 0,60 m (859 Pixel) nach der Streckregel des Modells; was dann noch nicht passt, rollt. Kurze Briefe füllen das Blatt nicht: Knöpfe und Siegel stehen unten am Blatt. | 17.11.2026 |
| Abschalten | Kein neuer Schalter: „Siegel und Briefe bewegen“ aus – Faltbriefe stehen sofort da und still, der große Brief liegt sofort offen da und ist sofort weg. | 17.11.2026 |
| Unverändert | Feld 56 Pixel, höchstens sieben Meldungen, Zähler, Tooltip, Porträt am Siegel, Töne; die zentrale Einblendung und der Fuß der Briefe zeigen weiter das gezeichnete Wachssiegel bzw. die Medaille des Geheimnisses. | 17.11.2026 |
