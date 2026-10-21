# Reichsgilde – Leveldesign

Begonnen am 16.10.2026 · Bereich **Leveldesign** (siehe `CLAUDE.md`). Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`, Abschnitt 17, neuere in `docs/bereiche/leveldesign.md`. Dieses Dokument sammelt die **Kartenregeln** für alle Städte und den Stand der einzelnen Karten.

**Stand:** Die erste Stadtkarte (Köln) liegt vor, das Spiel zeigt sie noch nicht. Die Greybox-Stadt bleibt Platzhalter für die Mechaniken; seit PR #163 ist ihre Kartendatei (Kartenformat 2, Abschnitt „Greybox-Karte“) die einzige Quelle dafür, wo in ihr etwas steht – die Stadtansicht zeichnet sie nur noch aus der Karte.

**Status je Regel:** **entschieden** (mit Datum in ENTSCHEIDUNGEN) · **Vorschlag** (Leveldesign) · **Kern** (wirkt auf die Simulation, entscheidet der Kern-Chat).

---

## 1. Grundsätze

| Grundsatz | Inhalt | Status |
|---|---|---|
| Entfernung ist Wirtschaft | Die Fahrzeit ergibt sich **nur aus der Weglänge**. Damit ist die Kartengröße selbst ein Balancing-Wert; jede neue oder geänderte Karte braucht eine Messung durch den Kern. | entschieden |
| Die Karte verschiebt nur | Lage wirkt als **Faktor um 1,0** auf bestehende Werte (Gefahr, Gäste, Nachfrage), wie bei der Bürger-KI. Im Mittel bleibt die Balance. | Vorschlag, Kern |
| Immer 8 Häuser | Jede Karte ist für 8 Häuser ausgelegt, auch wenn eine Partie weniger hat. | entschieden |
| Fairer Start | Startplätze sind gleichwertig (Marktweg ±10 %, Torweg ±15 %). Freie Flächen dürfen bewusst besser oder schlechter liegen. | Vorschlag |
| Vorlage mit einem Hauch Fiktion | Fluss, Mauer, Tore, Hauptkirche, Märkte, Straßen und Landschaft folgen der realen Stadt. Abweichungen werden in der Karte vermerkt. | entschieden |
| Einzelstücke mit gleichen Regeln | Etwa **8 Städte**, jede mit eigener Kartendatei und eigenem Gesicht, alle nach diesen Regeln. | entschieden |
| Lesbar aus vier Blickrichtungen | Eingänge und Plätze sind aus NO, SO, SW und NW erkennbar; hohe Bauten stehen nicht vor Eingängen. | Vorschlag |

## 2. Zonen

| Zone | Lage | Bebauung | Gefahr |
|---|---|---|---|
| Kern | um Markt, Rathaus, Hauptkirche | Sonderbauten, große Wohnhäuser | Taschendiebe, Einbruch |
| Mauerring | innerhalb der Mauer, außen | Werkstätten, Wohnhäuser, Schänken, Kirchen | Einbruch, Sabotage |
| Vorstadt | vor der Mauer, Brückenkopf, Hafen | wie Mauerring, ohne Mauerschutz | nachts höher |
| Umland | Felder, Gruben, Wälder | Umlandbetriebe, Wachtürme | Überfälle |
| Wildnis | Kartenrand, Berge, tiefer Wald | nichts | Ork-Rotten, Oger |

## 3. Bauen (entschieden 16.10.2026)

- **Straßen sind fest** und folgen der Vorlage. Spieler und KI bauen keine Wege.
- Zwischen den Straßen liegen **Bauzonen**. Darin setzt man Gebäude frei auf ein **Raster** mit Drehung in 90°-Schritten. Jedes Gebäude braucht eine Seite an einer Straße (Eingang).
- **Kein Grundstückskauf:** Bezahlt wird nur der Bau oder der Kauf eines Gebäudes. Keine Flächengrenze je Haus.
- **Abriss und Umbau** sind erlaubt.
- **Rasterfeld 2 × 2 m.** *(entschieden 16.10.2026)*
- **Gewerbe überall:** Jede Werkstatt darf in jede Stadt-Bauzone (Kernlage, Mauerring, Vorstadt), egal ob laut oder schmutzig. *(entschieden 16.10.2026)*
- **Anschluss:** Werkstätten brauchen ihren Eingang an einer **Karrenstraße** (Haupt-, Ring-, Wallgasse). Wohnhäuser dürfen auch an einer **Gasse** stehen. Gassen bringt die Karte mit, Spieler bauen keine. *(entschieden 16.10.2026)*
- **Fest bleiben:** Startgrundstücke (je Haus Wohnhaus und Werkstatt, gleich weit vom Markt; entschieden 16.10.2026), Sonderbauten (Rathaus, Hauptkirche, Markt, Hafen, Tore), Ruinen und Leerstand.
- **Umland:** Rohstoffbetriebe sind an Vorkommen gebunden (Erz, Stein, Wald). Höfe und Gärten stehen frei in Umland-Bauzonen. *(Vorschlag)*
- **Zustände:** frei → bebaut → leerstehend (Haus erloschen oder bankrott) → verfallen. Leerstand lässt sich kaufen und sanieren: **fest 50 % + 20 % des Hauswerts** (Entscheidung des Nutzers 17.10.2026, statt „Kosten nach Zustand“). *(Kern)*

**Offenes Risiko:** Ohne Flächenpreis und Grenze kann ein Haus gute Zonen mit billigen Bauten zustellen. Mögliche Gegenmittel, falls es in Tests auffällt: Unterhalt je Gebäude, Verfall ungenutzter Gebäude, Mindestabstand gleicher Betriebe. *(Kern)*

## 4. Wohnhäuser (entschieden 16.10.2026)

Jedes Haus wohnt in einem **eigenen Wohnhaus**, getrennt von seinen Betrieben. Das löst die Regel „Stammhaus = erste Werkstatt“ ab. Der **Stand** bestimmt, wie groß man bauen oder kaufen darf.

| Stand | Wohnhaus (Vorschlag) |
|---|---|
| Gemeiner | Fachwerkhaus |
| Bürger | Bürgerhaus |
| Patrizier | Steinhaus |
| Edler | Stadtpalais |
| Freiherr, Reichsgraf | Stadthof (Kernlage oder Mauerring) |

Wohnstube und weitere Räume liegen im Wohnhaus. Das Wohnhaus eines erloschenen Hauses wird Leerstand. Umsetzung, Preise und Unterhalt: Kern.

### Gebäudegrößen (entschieden 16.10.2026)

**Grundsatz:** Ein Gebäude belegt beim Bau sofort die Fläche seiner **größten Ausbaustufe am selben Platz**. So kann es später wachsen, ohne dass Nachbarn im Weg stehen. Die Fläche zählt in Rasterfeldern (2 × 2 m) und schließt den Vorplatz zur Straße (2 m) ein.

| Gebäude | Belegte Fläche | Felder | Ausbau am Platz |
|---|---|---|---|
| Werkstatt, Stufe 1–3 | 16 × 14 m | 8 × 7 = 56 | Stufe 1 → 2 → 3; **jede Stufe sichtbar größer** (Anbau, Hof), alles innerhalb der Fläche |
| Fachwerkhaus → Bürgerhaus → Steinhaus | 8 × 10 m | 4 × 5 = 20 | Gemeiner bis Patrizier am selben Platz |
| Stadtpalais (Edler) | 12 × 10 m | 6 × 5 = 30 | **neuer Bau** an anderer Stelle oder Abriss und Neubau |
| Stadthof (Freiherr, Reichsgraf) | 16 × 14 m | 8 × 7 = 56 | **neuer Bau** in Kernlage oder Mauerring; Palais → Stadthof am selben Platz nur, wenn die Fläche des Stadthofs schon belegt war |
| Einwohnerhäuser | 6 × 6 / 8 × 8 / 8 × 8 m | 9 / 16 / 16 | – (Wohnhaus, Bürgerhaus, Ratsherrenhaus) |
| Mine, Steinbruch, Holzschlag, Gärten | 16 × 14 m | 8 × 7 = 56 | – |
| Klosterhof / Bauernhof | 18 × 16 / 20 × 16 m | 72 / 80 | – |

**Aussehen:** Die Gebäude behalten ihr heutiges Aussehen, ihre Maße werden auf **glatte Meter** gesetzt (z. B. Einwohner-Wohnhaus 6,2 → 6 m, Bürgerhaus 7,2 → 7 m, Ratsherrenhaus 7,6 → 8 m). Die belegte Fläche rundet auf ganze Rasterfelder auf. Umsetzung: Darstellung.

## 5. Wege und Wegfindung

**Straßenbreite (entschieden 16.10.2026):** Bauzonen beginnen **3,3 m neben der Straßenmitte**, Straßen in der Stadt sind also gut **6,5 m** breit – genug für zwei Karren nebeneinander, eng wie in einer mittelalterlichen Stadt.

| Wegklasse | Wer | Regel |
|---|---|---|
| Landstraße | Karren, Fußgänger | endet an einem Tor |
| Hauptstraße | Karren, Fußgänger | verbindet Tore, Markt, Hafen, Bauzonen |
| Gasse | nur Fußgänger | keine Karren |
| Platz | alle | Wende- und Begegnungsfläche |
| Leinpfad | Fußgänger, Treidelpferde | am Ufer |
| Tor, Brücke | alle | Engstelle |
| Fähre | alle | setzt im Takt über |
| Feldweg | Karren, Fußgänger | unbefestigt im Umland, zu Hof, Grube, Feld (seit der Kartenwerkstatt, Schritt 2) |
| Fußweg | nur Fußgänger | Pfad zu Weide und Wald (seit der Kartenwerkstatt, Schritt 2) |

**Breiten der Wegklassen** (Kern, `Kartenweg.Standardbreite` und `Mindestbreite`, seit PR #170): Ohne `breite` zeichnet das Spiel einen Weg in der Standardbreite seiner Klasse; die Kartenwerkstatt lässt keinen Weg schmaler werden als die Mindestbreite, und die Prüfung meldet einen schmaleren als Fehler. Die Mindestbreiten sind so gewählt, dass Greybox und Köln sie einhalten (Landstraßen der Greybox 4 m, ihre Fußwege als Gasse 2 m).

| Klasse (in der Datei) | Standard | Mindestens | Karren |
|---|---|---|---|
| Hauptstraße (`haupt`) | 5 m | 4 m | ja |
| Landstraße (`land`), Wallstraße (`wall`) | 4 m | 4 m | ja |
| Ringstraße (`ring`) | 3 m | 3 m | ja |
| Feldweg (`feldweg`) | 3 m | 3 m | ja |
| Gasse (`gasse`) | 3 m | 2 m | nein |
| Leinpfad (`leinpfad`), Fußweg (`fussweg`) | 2 m | 1,5 m | nein |

- **Zwei Ebenen** *(Vorschlag, Kern)*: Ein **grober Wegegraph** (Knoten, Kanten mit Länge, Klasse, Gefahr) liefert der Simulation die Fahrzeit, ohne Godot und deterministisch. Die **feine Bewegung** (Ausweichen, Spur) macht nur die Darstellung, ohne Rückwirkung. Passt zur Regel „Der Kern legt Wege mit Zeiten fest“ (ENTSCHEIDUNGEN 15, Passanten).
- **Tore schließen nachts** (Dunkle Nacht, 20 Uhr); Karren draußen warten. *(entschieden; Umsetzung Kern)*
- **Fußgänger:** Die Bürger-KI wählt das Ziel; die Karte liefert Zielpunkte (Eingänge) und Verweilpunkte (Brunnen, Marktstände, Kirchenportal, Bänke vor Schänken). *(Vorschlag)*

## 6. Wasser

- **Flüsse, Häfen, Brücken** gehören dazu (entschieden). Flüsse quert man nur über Brücke oder Fähre.
- **Hafen** ist Umschlagplatz Schiff ↔ Karren. Er wird jetzt mitgeplant; die Mechanik (z. B. Stapelrecht, Schiffe) folgt später. *(entschieden)*
- **Bäche** in der Stadt mit kleinen Brücken als Engstellen. *(Vorschlag)*
- **Hochwasser** als Ereignis sperrt Ufer, Hafen und Leinpfad. *(Vorschlag, Kern)*

## 7. Gelände und Gefahr

- Drei Höhenstufen: Ebene, Hang, Berg. Berge sind nicht befahrbar, Wege folgen den Tälern.
- Rohstoffe liegen dort, wo die Vorlage sie hatte.
- **Gefahrenfaktor je Wegabschnitt** um 1,0, im Mittel aller Umlandfahrten 1,0. Höher: Wald, Engtal, Nähe zur Wildnis. Niedriger: Wachturm, Tornähe. Ork-Rotten kommen nur von der Wildnisseite. *(Vorschlag, Kern)*

## 8. Karten

| Stadt | Stand |
|---|---|
| Köln (Pilot) | **Kartendatei Format 2** (`game/karten/koeln.json`, Vorschau `docs/karten/koeln.svg`), Mauerradius 150 m: fünf Radialen von den Toren, zwei Ringstraßen um den Heumarkt, Wallgasse; 6 Bauzonen Kernlage, 12 Mauerring, 1 Vorstadt Deutz, 6 Umland; 10 Rohstoffvorkommen; 8 Startgrundstücke am äußeren Ring; linkes Rheinufer, Mauerhalbkreis mit Toren, Dom als Baustelle, Hafen, **Rheinbrücke nach Deutz (Fiktion)**, Fähre, Umland Kölner Bucht, Ville, Königsforst, Bergische Höhen, Siebengebirge, Eifel |
| Greybox (Testkarte) | **Kartendatei Format 2** (`game/karten/greybox.json`, Vorschau `docs/karten/greybox.svg`): die Platzhalter-Stadt in Weltkoordinaten mit allem, was in Stadt und Umland steht (bis PR #163 fest im Code gezeichnet), mit 11 Bauzonen für das freie Bauen, davon 3 in Kernlage (Abschnitt „Greybox-Karte“) |
| weitere ca. 7 | offen |

Später: **Reichskarte**, über die man zwischen den Städten wechselt (Idee festgehalten, nicht jetzt).

## 9. Kartendatei

Jede Stadt ist **eine JSON-Datei** `game/karten/<kennung>.json`. Sie entsteht aus einem Python-Skript `tools/karten/<kennung>.py`, nie von Hand (bis zum Level-Editor, siehe `docs/bereiche/leveldesign.md`). Das Skript prüft die Kartenregeln und schreibt nur, wenn alle erfüllt sind; dazu ein Vorschaubild `docs/karten/<kennung>.svg`, das nur aus der JSON-Datei und dem Gebäudekatalog gezeichnet wird (auf GitHub direkt ansehbar). Seit PR #163 schreibt das Werkzeug **Kartenformat 2** (Abschnitt unten).

```bash
python3 tools/karten/koeln.py             # Köln prüfen und neu schreiben
python3 tools/karten/greybox.py           # Greybox-Stadt prüfen und neu schreiben
python3 tools/karten/katalog_pruefen.py   # Gebäudekatalog gegen die Modelle prüfen (--alle: jede Fläche mit Modellgröße)
```

Ein Stadtskript beschreibt die Karte mit den Bausteinen aus `tools/karten/karte.py`: `k.gebaeude(kennung, katalog, name, punkt, drehung, massstab=…, betrieb=…, lage=…)`, `k.mauer(name, linie, geschlossen)` und `k.tor(mauer, name, punkt)`, `k.weg(…)`, `k.bauzone(…)`, `k.naturflaeche(name, art, flaeche, dichte, startwert, gruppen, aussparungen)`, `k.pflanze(…)`, `k.start(spieler, haeuser)`, `k.kamera(punkt, zoom)`, dazu `wasser`, `gelaende`, `hafen`, `uebergang`, `vorkommen`, `fiktion`. Am Ende `k.schreiben()`.

**Koordinaten:** Meter auf dem Boden, **x = Osten, z = Süden** (−z = Norden), wie im Spiel. Ursprung am Marktplatz. Bauzonen liegen auf dem **2-m-Raster**, alles andere auf 0,5 m.

Die folgende Tabelle beschreibt **Format 1** (bis 21.10.2026); seit PR #163 gilt Format 2 mit dem Gebäudekatalog (Abschnitte „Kartenformat 2“ und „Gebäudekatalog“ unten).

| Feld | Inhalt |
|---|---|
| `format`, `kennung`, `stadt`, `vorlage` | Formatnummer (1), Dateiname, Anzeigename, reale Vorlage |
| `einheit`, `achsen`, `raster` | Meter, Achsen wie oben, Rastergröße 2 |
| `mauer.linie` | Stadtmauer als Punktfolge (endet an den Ufertürmen) |
| `nebenmauern` | weitere Mauern mit `name` und `linie`, z. B. um eine befestigte Vorstadt; ihre Tore stehen in `tore` |
| `tore` | Name, Punkt, `schliesstNachts` |
| `wasser` | `fluss`/`bach` als Linie mit `breite`, `see` als Fläche |
| `gelaende` | Flächen der Art `feld`, `wald`, `huegel`, `wildnis` |
| `wege` | Name, `klasse` (`land`, `haupt`, `ring`, `wall` = Karrenstraßen; `gasse`, `leinpfad` = nur Fußgänger), `linie`, `gefahr` (Faktor um 1,0, Vorschlag), optional `breite` in Metern (gezeichnete Breite; nur die Greybox-Karte) |
| `haefen` | `name`, `kante` (Kaimauer als Linie), `anleger` (Punkte an der Kaimauer, dort liegen Schiffe), `kran` |
| `uebergaenge` | `bruecke` oder `faehre` mit `von` und `bis` |
| `bauzonen` | `kennung`, `art` (`kern`, `mauerring`, `vorstadt`, `umland`), `flaeche` auf dem Raster |
| `feste` | `kennung`, `art` (`start`, `sonderbau`, `kirche`, `leerstand`; Greybox zusätzlich `werkstatt`, `umland`, `umlandstelle`, `turm`, `einwohnerhaus`, `einwohnerplatz`, `wohnhaus`), `name`, `punkt`; Startgrundstücke mit `eingang` und `groesse` [Breite, Tiefe] (24 × 14 m: Werkstatt und Wohnhaus nebeneinander). Die Greybox-Gebäude haben außerdem `drehung` (siehe unten) |
| `vorkommen` | `kennung`, `betrieb` (`Mine`, `Steinbruch`, `Holzschlag`), `punkt` |
| `fiktion` | Liste der Abweichungen von der Vorlage |
| `gebaeude`, `probeplan` | nur Greybox: Schreibweise der Gebäudemaße und ein Plan, wie die geforderten Gebäude in die Bauzonen passen (siehe „Greybox-Karte“) |

**Prüfungen** (das Skript bricht ab, wenn eine fehlschlägt; „Hinweis“-Zeilen brechen nicht ab):
- Jedes Gebäude verweist auf einen Baustein des Gebäudekatalogs, der unter `gebaeude` stehen darf und eine Größe hat; Kennungen eindeutig; Wohnhausplätze rechtwinklig; jeder Umlandbetrieb trägt `betrieb`, jeder Betriebsname steht an höchstens einem Platz.
- **Nichts überlappt:** die Grundflächen aus dem Katalog, gedreht und mal `massstab`. Ein Felsberg darf seine Grube überdecken; überdeckt er ein anderes Gebäude, ist das nur ein Hinweis (seine Fläche im Katalog hat Rand um das Modell).
- Jeder Werkstattplatz hat seinen Hof (im Vorplatz, 0,5 m hinter der Vorderkante) höchstens 7 m von einem Weg.
- Jede Bauzone liegt höchstens 8 m von einer Karrenstraße entfernt.
- Köln: genau 8 Startgrundstücke (Werkstatt- und Wohnhausplatz `S<n>_…`), gleich weit vom Markt (±10 %).
- Jedes Vorkommen liegt an einem Weg oder in der Fläche eines Umlandbetriebs (dessen Weg endet an seinem Halteplatz).
- Mauern: Baukasten aus dem Katalog; die Stadtmauer ist geschlossen (Greybox) bzw. offen (Köln, zum Rhein hin); jedes Tor liegt höchstens 1 m neben der Linie seiner Mauer; Wege kreuzen eine Mauer nur an einem Tor.
- Natur: jede Art ist ein Natur-Baustein des Katalogs, jede Fläche hat Dichte und Gruppen.
- Start: Wohnhaus- und Werkstattplätze gibt es und sie sind von der richtigen Art, am Platz eines KI-Betriebs steht `betrieb`, ein Umlandbetrieb passt zur Betriebsart, kein Platz, Name oder Wappen doppelt.
- Anleger und Kran liegen an der Kaimauer.
- Das Wegenetz hängt zusammen (Kreuzungen, Berührungen, Brücken und Fähren verbinden).
- **Kapazität** (`tools/karten/kapazitaet.py`): Das Werkzeug setzt die Gebäude probeweise auf das Raster der Stadt-Bauzonen – feste Bauten zuerst, dann die größten Gebäude, jedes möglichst nah an seiner Straße (Werkstatt an einer Karrenstraße, Wohnhaus auch an einer Gasse). **Pflicht** für 8 Häuser: je Haus zwei weitere Werkstätten, dazu die Häuser der Einwohner (13 Ratsherrenhäuser, 3 Bürgerhäuser, 40 Wohnhäuser bei 25 Einwohnern je Haus und Haushalten von im Mittel 5). Passt die Pflicht nicht, bricht das Skript ab. **Reserve** (nur Bericht): je Haus ein Stadtpalais, die Hälfte einen Stadthof. Das Ergebnis zeigt `docs/karten/<kennung>-belegung.svg`.

**Dieselben Prüfungen in C#** (seit PR #168, `Kartenwerkstatt/Kartenpruefung.cs`, für die Live-Prüfung der Kartenwerkstatt): alle oben außer der Kapazität, dazu die Stadtregeln aus „Greybox-Karte“ (Prüfungen). Gleiche Geometrie wie das Werkzeug; geprüft wird, dass die Greybox keinen Fehler und dieselben 7 Hinweise hat und Köln keinen Fehler. Welche Regeln je Karte gelten, steht unten in „Kartenwerkstatt“. Die Kapazität bleibt bis zum Umzug (Schritt 4) im Werkzeug.

Ändert sich das Format, steigt `format`, und der Kern und die Darstellung passen ihre Leser an. Neue Felder ohne Bedeutungsänderung brauchen keine neue Nummer. Die Kapazitätsprüfung belegt seit Format 2 vorab die Grundfläche **jedes** Gebäudes aus dem Katalog (bei schräger Drehung das umschließende Rechteck, wie der Kern).

**Köln, Format 1 (16.10.2026):** Bauzonen Kernlage 6 (≈ 5.640 m², 1.410 Felder), Mauerring 12 (≈ 10.230 m², 2.557 Felder), Vorstadt Deutz 4 (≈ 3.960 m²), Umland 6 (≈ 39.700 m²); 41 Wege in einem Netz; 10 Vorkommen; Startgrundstücke 94 m vom Markt. **Kapazität:** Die Pflicht passt vollständig (16 Werkstätten, 56 Einwohnerhäuser), danach sind 2.691 von 4.949 Feldern frei (54 %). Reserve: alle 8 Stadtpalais und alle 4 Stadthöfe. Auf einer leeren Karte passen 29 Werkstätten (Bedarf 24 mit den Startwerkstätten). **Lehre aus der Messung:** Begrenzend ist die Blocktiefe, nicht die Länge der Straßenkante. Zusätzliche Straßen zerschneiden die Blöcke und bringen nichts (Querstraße statt Gasse: gleich viel, zusätzliche Hintergasse: weniger); schmalere Straßen machen die Blöcke tiefer.

**Köln, Format 2 (21.10.2026):** Jedes Startgrundstück (24 × 14 m) ist ein Werkstattplatz (16 × 14 m) und ein Wohnhausplatz (8 × 10 m, vorn bündig) mit derselben Drehung – in der Himmelsrichtung, die dem Mittleren Ring am nächsten kommt, denn Wohnhausplätze stehen rechtwinklig. Mit den Flächen aus dem Katalog fiel auf, dass drei Bauten auf Startgrundstücken standen; sie rücken: Dom 1,3 m nach Norden, Antoniterkirche von (455 | 440) nach (437 | 444), Leerstand L2 von (455 | 393) nach (481 | 391) (Skizzenpunkte). **Kapazität:** fest belegt 773 Felder (alle Gebäude mit ihrer Katalogfläche), die Pflicht passt, danach 2.664 von 4.949 Feldern frei (54 %), Reserve vollständig.

### Kartenformat 2 (21.10.2026)

Seit PR #163 ist die Karte die **einzige Quelle** dafür, wo etwas steht (Entscheidung des Nutzers, `docs/bereiche/leveldesign.md`, „Gebäudekatalog und Karte als einzige Quelle“). Die Karte sagt nur **wo** (Katalog-Kennung, Punkt, Drehung, Name); **was** ein Gebäude ist (Modell, Grundfläche), steht im Gebäudekatalog (nächster Abschnitt). Der Kern liest Format 2 (`Stadtkarte.cs`), Format 1 nicht mehr: Alte Spielstände mit einer Karte in Format 1 bekommen beim Laden die neue Karte derselben `kennung`.

Koordinaten wie bisher: Meter, x = Osten, z = Süden, Ursprung am Markt. **Drehung** immer in Grad wie Godot `Rotation.Y`: Die Vorderseite zeigt bei 0 nach Süden (+z), bei 90 nach Osten (+x), bei 180 nach Norden, bei 270 (oder −90) nach Westen; beliebige Winkel erlaubt (außer Wohnhausplätzen). Lokal heißt: x nach rechts, z zur Vorderseite. Gedreht wird ein lokaler Punkt (x, z) so: x' = x·cos d + z·sin d, z' = −x·sin d + z·cos d.

| Feld | Inhalt |
|---|---|
| `format`, `kennung`, `stadt`, `raster` | `2`, Dateiname, Anzeigename, Rastergröße der Bauzonen (2). Weitere Felder wie `vorlage`, `fiktion`, `vorkommen`, `probeplan` liest der Kern nicht (das Werkzeug darf sie behalten). |
| `gebaeude` | alles, was in Stadt und Umland steht – je Eintrag `kennung` (eindeutig), `katalog` (Kennung des Bausteins), `name`, `punkt` [x, z] = **Mitte der Grundfläche** (inklusive Vorplatz), `drehung` (Grad); optional `massstab` (1 = wie im Katalog; Modell und Fläche wachsen mit), `betrieb` (Name des Betriebs, der hier steht), `lage` (Wohnhausplatz: `kern`, `mauerring` …), `konfession` (Kirche). Die Reihenfolge zählt bei Einwohnerplätzen (sie werden der Reihe nach bebaut). |
| `mauern` | je Mauer `name`, `linie` (Punkte), `geschlossen` (letzte Strecke zurück zum ersten Punkt), `katalog` (Mauer-Baukasten, Vorgabe `stadtmauer`), `tore`: je Tor `name`, `punkt`, `schliesstNachts`. Ecktürme an jedem Knick, Mauertürme und Torburgen setzt das Spiel aus dem Baukasten. Ersetzt `mauer`, `nebenmauern`, `tore` und die Torhäuser und Ecktürme unter `feste`. |
| `wege` | wie Format 1: `name`, `klasse`, `linie`, `gefahr`, `breite` (gezeichnete Breite in Metern; fehlt sie, gilt die Standardbreite der Klasse, §5), dazu seit PR #170 `kurve` (siehe „Wegkurve“ unten) und die Klassen `feldweg` und `fussweg`. Das **Gassennetz** entsteht nur aus den Wegen: Jede Tür, jeder Hof eines Werkstattplatzes, jeder Karrenplatz eines Umlandbetriebs und jedes Gatter einer Umlandstelle (Anker im Modell) wird automatisch mit dem nächsten Weg verbunden. Gassen von einer Werkstatt zum Markt, Zufahrten zu Gruben und Fußwege zu Weide, Feld und Wald sind also eigene Wege der Karte. |
| `bauzonen` | wie Format 1: `kennung`, `art`, `flaeche`. |
| `natur` | `flaechen` und `pflanzen` (siehe unten). |
| `wasser`, `gelaende`, `haefen`, `uebergaenge` | wie Format 1 (nur Köln). |
| `start` | Belegung am Start (siehe unten). Fehlt er, gelten die drei Häuser, die es bis Format 1 fest im Code gab (Eisenbart, Eichwald, Kieselbach), ohne Plätze. |
| `kamera` | optional: Blick beim Start, `punkt`, `zoom`, `drehung`. |
| `groesse` | optional: [Breite in x, Tiefe in z] der Karte in Metern um den Ursprung – schreibt die Kartenwerkstatt bei einer neuen Karte (seit PR #168); der Kern liest es nicht. |

**Plätze und Betriebe.** Ein **Werkstattplatz** (`werkstattplatz`) hat kein festes Modell – es folgt dem Beruf des Betriebs, der dort arbeitet. Steht dort am Start ein KI-Haus, trägt der Platz den Betriebsnamen in `betrieb` („Gießerei Eisenbart“). Jeder **Umlandbetrieb** (`grube`, `holzschlag` …) trägt den Namen seines Betriebs in `betrieb` – genau wie in `wirtschaft.json` unter `liegenschaften` (wer kaufen oder pachten kann, steht weiter dort; die Karte verweist nur über den Namen) bzw. wie der Startbetrieb des KI-Hauses. Ein Betriebsname steht höchstens an einem Platz. **Wohnhausplätze** (`wohnhausplatz_ring` …) stehen rechtwinklig; ihre Kennungen merkt sich der Spielstand (Greybox: `Wohnhaus1` bis `Wohnhaus8` – nicht umbenennen). **Einwohnerplätze** nehmen die Häuser der Einwohner auf, **Kulissen** sind Häuser mit festem Modell. Der Felsberg hinter einer Grube ist ein eigenes Gebäude (`felsberg`) mit eigenem Punkt, eigener Drehung und `massstab`.

**Belegte Fläche (Kern).** Jedes Gebäude belegt für das Bauen die Fläche seines Bausteins mal `massstab` um `punkt`; bei einer schrägen Drehung das umschließende Rechteck (achsparallel). Darauf wird nicht gebaut. Ein Wohnhausplatz, auf dem ein Haus wohnt, zählt als Grundstück.

**Wegkurve** (seit PR #170). Ein Weg speichert nur seine Stützpunkte (`linie`). Fehlt `kurve`, verbinden gerade Strecken die Punkte (so sind alle Wege von Greybox und Köln). Mit `"kurve": "glatt"` läuft eine weiche Kurve durch **alle** Stützpunkte: eine zentripetale Catmull-Rom-Kurve (keine Schleifen, kein Überschwingen in engen Biegungen), an offenen Enden gerade auslaufend, bei einem Ring (erster = letzter Punkt) rundherum glatt. Der Kern tastet sie je Stück zwischen zwei Stützpunkten in gleich lange Teile von höchstens 1 m ab (`Wegkurve.Verlauf`); diese Linie (`Kartenweg.Verlauf`, Länge `Laenge`) nutzen Spiel, Prüfung und Bauwesen gleich. Ein Werkzeug, das `kurve` nicht kennt, sieht die Stützpunkte als gerade Strecken – die Kurve weicht davon höchstens um einige Meter ab.

```json
{"name": "Weg zum Hof", "klasse": "feldweg", "linie": [[-150, 40], [-170, 60], [-190, 40]], "gefahr": 1, "breite": 3, "kurve": "glatt"}
```

**Kreuzungen.** Ein Weg, der an einem anderen beginnt oder endet, teilt mit ihm einen Stützpunkt: Die Kartenwerkstatt rastet ein Wegende im Umkreis von 2 m auf den nächsten Stützpunkt des anderen Weges ein, sonst auf den nächsten Punkt seiner Linie – dort bekommt der andere Weg einen Stützpunkt (bei einer glatten Kurve geht sie dann genau durch die Kreuzung). Das Gassennetz verbindet Wege auch, die sich nur berühren (1 m); der gemeinsame Punkt macht es eindeutig.

**Natur.** Eine Fläche ist ein Vieleck mit optionalen `aussparungen` (Vielecke, in denen nichts wächst), `dichte` (Streupunkte je 1.000 m²), `startwert` (gleiche Karte, gleiches Bild) und `gruppen`. An jedem Streupunkt wählt das Spiel eine Gruppe nach `gewicht`; jeder ihrer `teile` wählt eine Art aus `arten` (Katalog-Kennung → Gewicht) und setzt `anzahl` [von, bis] Kopien in einer Scheibe mit `radius` (0 = genau auf den Punkt, etwa ein einzelner Baum). Drehung (0–360°) und Größe (0,85–1,15) würfelt das Spiel. Auf Wegen, Gebäuden, Bauzonen und innerhalb der Stadtmauer wächst nichts. Einzelne Pflanzen stehen unter `pflanzen`: `katalog` (jeder Natur-Baustein des Katalogs: Bäume, Büsche, Bodenpflanzen, Steine, Holz), `punkt`, `drehung`, `massstab`, `variante` (0 = fest aus dem Ort gewürfelt); sie stehen, wo sie gesetzt sind, auch auf einem Weg (die Prüfung gibt dann einen Hinweis).

**Natur-Verteilung** (Kern, `Bauen/NaturVerteilung.cs`, seit PR #170 – vorher nur in der Darstellung): Für jede Fläche zählt der Kern auf einem Raster von etwa 20.000 Punkten, wie viel von ihrem Rechteck in der Fläche und in keiner Aussparung liegt, und setzt `dichte` × Inhalt / 1.000 Streupunkte, gleich verteilt aus `startwert`. Frei bleiben (`Naturfreiraum`): das Innere jeder Mauerlinie und 6 m davor, jeder Weg mit 3 m Rand neben seiner halben Breite (auf seinem Verlauf), jedes Gebäude mit 3 m Rand um seine Fläche (Umlandbetriebe und -stellen um ihr ganzes Modell, ein Felsberg nur auf der Ellipse in seiner Fläche), jede Bauzone mit 3 m Rand. Ein verworfener Punkt würfelt trotzdem weiter, sodass eine neue Straße nur die Pflanzen auf ihr wegnimmt und keine anderen verschiebt. Die Reihenfolge der Würfe ist die der bisherigen Darstellung (Greybox: 869 Pflanzen). Der **Pinsel** der Kartenwerkstatt streut Einzelpflanzen locker in ein Band um einen gezogenen Strich: Dichte je 1.000 m² des Bandes, mindestens eine Pflanze, keine näher als 0,6 × √(1.000 / Dichte) Meter an einer anderen; frei bleibt dasselbe wie oben.

```json
"natur": {
  "flaechen": [
    { "name": "Wald um die Stadt", "art": "wald", "flaeche": [[108, 0], …], "aussparungen": [[[58, 0], …]], "dichte": 3.05, "startwert": 1400,
      "gruppen": [ { "gewicht": 1, "radius": 0, "teile": [ { "arten": { "tanne": 0.55, "eiche": 0.3, "buche": 0.15 }, "anzahl": [1, 1] } ] } ] }
  ],
  "pflanzen": [ { "katalog": "kopfweide", "punkt": [84, 80], "drehung": 30, "massstab": 1.1 } ]
}
```

**Start.** Der Spieler (`spieler`) bekommt `wohnhaus` (Kennung eines Wohnhausplatzes), `farbe` und `werkstatt` mit `platz` (Kennung eines Werkstattplatzes) und `name` (`{art}` wird durch die Betriebsart ersetzt: „{art} am Weinmarkt“ → „Gießerei am Weinmarkt“); `jeBeruf` kann je Beruf einen anderen Platz und Namen nennen. Die KI-Häuser (`haeuser`) in Konto-Reihenfolge – das erste spielt immer mit, die weiteren je nach Zahl der Rivalen im Menü: `name` („Haus Eisenbart“), `volk`, `geschlecht` (`Mann`/`Frau`), `wappen` (`teilung`, `farbe1`, `farbe2`, `figur` wie in `Wappen.cs`), `farbe` („#rrggbb“, Fahnen und Dächer), `wohnhaus`, `betriebe` (zuerst die Werkstatt): je `platz` (Kennung des Werkstattplatzes oder Umlandbetriebs – dessen `betrieb` ist der Name), `art` (Betriebsart, z. B. `Giesserei`, `Mine`), optional `produkt`. Wie viele Gesellen und Umlandkräfte ein Startbetrieb bekommt, bleibt eine Regel des Kerns. Die Wohnhausplätze werden in dieser Reihenfolge vergeben (Spieler, dann die Häuser), übrige Wohnhausplätze danach in der Reihenfolge der Datei.

```json
"start": {
  "spieler": { "wohnhaus": "Wohnhaus1", "farbe": "#336bb8", "werkstatt": { "platz": "Weinmarkt", "name": "{art} am Weinmarkt" } },
  "haeuser": [
    { "name": "Haus Eisenbart", "volk": "Zwerg", "geschlecht": "Mann",
      "wappen": { "teilung": "Ohne", "farbe1": "Schwarz", "farbe2": "Gold", "figur": "Amboss" }, "farbe": "#b33329", "wohnhaus": "Wohnhaus2",
      "betriebe": [ { "platz": "Giesserei_Eisenbart", "art": "Giesserei", "produkt": "Rapier" }, { "platz": "Grube_Eisenbart", "art": "Mine" } ] }
  ]
}
```

**Der Kern prüft beim Lesen** (`Stadtkarte.Pruefen`, bricht mit allen Fehlern ab): Format 2; jeder Baustein im Katalog bekannt und unter `gebaeude` erlaubt; Kennungen eindeutig; jeder Betriebsname an höchstens einem Platz; Umlandbetriebe mit `betrieb`; Wohnhausplätze rechtwinklig; Mauer-Baukasten und Natur-Arten im Katalog; im Start: Plätze vorhanden und von der richtigen Art, am Platz eines KI-Betriebs steht `betrieb`, ein Umlandbetrieb passt zur Betriebsart, kein Platz, Wohnhausplatz, Name oder Wappen doppelt. Die Kartenregeln (Straßenabstand, Tore, Kapazität) prüft weiter das Werkzeug, seit PR #168 außer der Kapazität auch `Kartenpruefung` für die Kartenwerkstatt.

**So schreibt das Werkzeug die Datei:** `format: 2`, Felder wie oben, Drehung in Grad (beliebig, auf 0,1° gerundet), Punkte auf 5 cm (Köln: 0,5 m), ganze Zahlen ohne „.0“; was in eine Zeile passt (ein Gebäude, ein Weg, ein Tor), steht in einer Zeile, lange Punktfolgen mit mehreren Punkten je Zeile. Leere Abschnitte (`wasser` in der Greybox, `natur` und `start` in Köln) fallen weg. Gebäude-Kennungen bleiben über Versionen gleich (Spielstände). Die Umlandbetriebe stehen mit ihrer echten Drehung in der Datei (bisher rechnete das Spiel sie aus dem Abzweig aus). Die Karte bekommt den Katalog nicht hineingeschrieben – der Kern liest beide Dateien, die Tests und Werkzeuge finden den Katalog neben dem Ordner `karten/`.

### Gebäudekatalog (21.10.2026)

Eine Datei **`game/daten/katalog.json`** für alle Karten: je Baustein, **was** er ist. Neue Modelle trägt der Modell-Agent gleich ein. Anker (Tür, Hof, Karren) liest das Spiel wie bisher aus dem Modell. Gelesen von `Gebaeudekatalog.cs`; im Spielstand liegt er mit der Karte (wie die Wirtschaftsdaten).

| Feld | Inhalt |
|---|---|
| `kennung` | eindeutig, klein, ohne Leerzeichen (`grube`, `werkstattplatz`) – darauf verweisen die Karten |
| `art` | `werkstattplatz`, `wohnhausplatz`, `einwohnerplatz`, `kulisse`, `sonderbau`, `umlandbetrieb`, `umlandstelle`, `berg` (diese stehen unter `gebaeude`); `mauer` (unter `mauern`); `natur` (unter `natur`); `werkstatt`, `wohnhaus`, `einwohnerhaus` (wählt das Spiel nach Beruf, Stufe, Hausart – stehen nie in einer Karte) |
| `name` | Anzeigename für Werkzeug und Editor |
| `modell` | Datei unter `game/assets/modelle/` mit Unterordner, ohne `.glb` (`gebaeude/schmied`, `umland/grube`, `natur/natur_baeume`; Unterordner siehe `docs/ARCHITEKTUR.md`, „Ordner im Godot-Projekt“); fehlt bei Plätzen (das Modell folgt dem Betrieb, der Stufe, der Hausart) |
| `knoten` | Knoten im Modell, wenn die Datei ein Baukasten ist (`Weide` in `umland_stellen`, `Tanne` in `natur_baeume`) |
| `varianten` | Natur: Anzahl der Varianten des Knotens (`Tanne1` … `Tanne3`), Vorgabe 1 |
| `groesse` | [Breite an der Vorderseite, Tiefe] in Metern, inklusive Vorplatz – die belegte Fläche (LEVELDESIGN „Gebäudegrößen“). Pflicht außer bei `mauer` und `natur` |
| `versatz` | wo der Ursprung des Modells liegt, gemessen von der Mitte der Grundfläche, lokal [x, z]; fehlt er, liegt er in der Mitte (Dom: [0, −2,85], Felsberg: [2,5, 21,25]) |
| `betriebsart` | Umlandbetrieb: Betriebsart (`Mine`, `Holzfaellerei` …) |
| `beruf`, `stufe`, `hausart` | Werkstatt: Beruf; Wohnhaus: Stufe 0–4; Einwohnerhaus: `Wohnhaus`, `Buergerhaus`, `Ratsherrenhaus` |
| `klickHoehe`, `schildHoehe` | Umlandbetrieb: Höhe der Klickfläche und des Schildes in Metern |
| `lagen` | wo der Baustein stehen darf (für den Editor; der Kern prüft es bei Karten nicht) |

Der Kern prüft: Kennung eindeutig, Art bekannt, `groesse` vorhanden und positiv, `modell` bei allem mit festem Modell, `knoten` bei Natur, `betriebsart`/`beruf`/`stufe`/`hausart` passend zur Art. Eine unbekannte Kennung in einer Karte meldet er mit Gebäude und Kennung („Gebäude „X“: Den Baustein „schloss“ gibt es im Katalog nicht.“).

**Katalog gegen Modelle** (`python3 tools/karten/katalog_pruefen.py`, liest die glb-Dateien ohne Blender wie `tools/modelle/pruefen.py`): **Fehler** (Code 1), wenn eine Modelldatei unter `game/assets/modelle/` fehlt oder ein `knoten` (bei Natur jede Variante `Tanne1` … `Tanne3`) nicht in der Datei steht. **Warnung**, wenn die Grundfläche nicht zur Größe des Modells passt: Das Modell (alle Netze, auch Leerstand und Gerüst, bei `knoten` dessen Teilbaum) ragt mehr als 0,5 m über die Fläche – gemessen von der Mitte der Fläche, also mit `versatz` – oder füllt sie in keiner Richtung zur Hälfte. Stand 21.10.2026: keine Fehler, 11 Warnungen. **Die Umlandbetriebe sind größer als ihre Fläche** (Grube, Holzschlag, Steinbruch 22 × 18 m auf 16 × 14, Bauernhof 26 × 20 auf 20 × 16, Klosterhof 22 × 18 auf 18 × 16, Kräuter- und Duftgarten 20 × 16 auf 16 × 14, Wachturm 12 × 12 auf 4 × 4 Fundament); der Kern hat entschieden, dass die Fläche bleibt (offener Punkt in `docs/bereiche/leveldesign.md`). Dazu Weide (1 m), einfaches Einwohnerhaus (6,7 m tief auf 6 m) und Ratsherrenhaus (0,5 m).

**Abfragen für die Darstellung** (alle auf der Karte der Partie, `Stadtwirtschaft.Karte`): `Gebaeude` (jeder Eintrag mit aufgelöstem `Baustein`, `Art`, `Grundriss`), `GebaeudeMit(kennung)`, `GebaeudeDerArt(art)`, `Mauern`, `Wege`, `Bauzonen`, `Natur`, `Wasser`, `Kamera`, `Startbelegung` (`Farbe(inhaber, istSpieler)`, `Rivalennamen`, `Rivalenwappen`), `Katalog` (`Finde(kennung)`, `Werkstatt(beruf)`, `Wohnhaus(stufe)`, `Einwohnerhaus(hausart)`, `Umland(betriebsart)`), `PlatzVon(betrieb)` (fester Platz eines Betriebs oder einer freien Liegenschaft); dazu `Stadtwirtschaft.Bauen.PlatzVon(betrieb)` → `Betriebsplatz` (fester Platz oder Grundstück einer neu gebauten Werkstatt, mit `Punkt` und `DrehungGrad`).

### Kartenwerkstatt (21.10.2026)

Der Level-Editor im Spiel (Fahrplan und Entscheidungen in `docs/bereiche/leveldesign.md`, „Kartenwerkstatt“). Der Kern liefert dafür (`src/Reichsgilde.Core/Kartenwerkstatt/`, Aufbau in `docs/ARCHITEKTUR.md`, „Bauen“):

- **Entwurf:** öffnet eine Kartendatei, auch mit Regelfehlern, hält alle Felder (auch die, die nur das Werkzeug liest) und schreibt sie im Zeilenformat des Werkzeugs zurück – eine unverändert gespeicherte Karte ist Byte für Byte dieselbe Datei. Neue Karten mit Kennung, Name und Größe.
- **Befehle** mit Rückgängig und Wiederholen (200 Schritte): Gebäude setzen, verschieben, drehen, löschen, Name, Betrieb, Lage ändern. Punkte landen auf 5 cm, Drehungen auf 0,1°; das 1-m-Raster und die 15°-Schritte wählt die Bedienung.
- **Prüfung** nach jedem Befehl (Fehler und Hinweise mit Gebäuden und Ort) und beim Ziehen nur für das eine Gebäude gegen den Rest. Regeln je Karte: **Greybox** wie `greybox.py` (geschlossene Mauer, Werkstätten 16 × 14 m nach Süden mit Gasse am Hof, 8 Wohnhausplätze, Probeplan mit Mindestzahlen), **Köln** wie `koeln.py` (offene Mauer, 8 Startgrundstücke; ohne Kapazität), **neue Karten** die allgemeinen Regeln und die Stadtregeln: Bauzonen auf dem Raster, 2 m von Gebäuden und Mauer, Straßenrand + 0,5 m, ohne Überlappung; Werkstätten 2 m auseinander und nicht auf der Mauer; Eingang jedes Wohnhausplatzes höchstens 1,5 m vom Wegrand; keine Gasse über ein Haus der Stadt (innerhalb der ersten geschlossenen Mauer); Wege über Umlandbetrieben als Hinweis. Ohne `breite` gilt die Standardbreite der Wegklasse (§5).
- **Schritt 2 (seit PR #170):** Wege, Bauzonen, Naturflächen und einzelne Pflanzen sind bearbeitbar – Wege anlegen (Klasse, Stützpunkte, glatt oder gerade; Anfang und Ende rasten an anderen Wegen ein), Stützpunkte verschieben, einfügen (Klick auf die Linie), löschen (mindestens zwei bleiben), Name, Klasse, Breite (nie unter der Mindestbreite), Kurve, ganzen Weg verschieben; Flächen als Vielecke (Ecken einer Bauzone auf dem 2-m-Raster, sonst 5 cm) mit Ecken verschieben, einfügen, löschen (mindestens drei), Aussparungen, Dichte, Mischung der Arten, Startwert; Pflanzen setzen, verschieben, drehen, löschen und der Pinsel (ein Rückgängig-Schritt je Strich). Neue Naturflächen: Wald als Mischwald (Tanne 0,55, Eiche 0,3, Buche 0,15, Dichte 2,4), Streu wie im Umland der Greybox (Dichte 2,9), Startwert = größter der Karte + 1. **Zusätzliche Prüfungen:** Weg mit weniger als zwei Punkten oder schmaler als die Mindestbreite (Fehler), glatte Kurve über ein Haus, außer dem, an dem sie beginnt oder endet (Fehler), Bauzone ohne drei Ecken oder mit doppelter Kennung (Fehler), Fläche oder Aussparung, die sich selbst kreuzt (Hinweis – Köln hat eine solche Bauzone, M5), einzelne Pflanze auf einem Gebäude oder Weg (Hinweis). Alle Regeln rechnen auf dem Verlauf der Wege; Greybox und Köln haben dieselben Befunde wie vorher.

### Greybox-Karte (17.10.2026)

`greybox.json` beschreibt die **Platzhalter-Stadt** (Mauer bei ±50 m, Ringgasse bei ±38 m, Ost- und Westtor, Wohnviertel, Umland) im Kartenformat 2. Seit PR #163 ist sie die **einzige Quelle**: Die Stadtansicht kennt keine festen Lagen mehr und zeichnet Mauer, Wege, Gebäude, Plätze und Natur nur aus der Karte. Die Zahlen hat das Skript `tools/karten/greybox.py` aus dem Code übernommen, mit dem die Stadtansicht die Stadt bis 21.10.2026 fest gezeichnet hat (`Stadtplan`, `Gassen`, `Abzweige`, `UmlandDrehungGrad`, `BergGroesse`, `RingPlaetze`, `VorstadtPlaetze`, `SpielhausPlaetze`, `KulissePlaetze`, `Umlandstellen`, `Wald`, `NaturStreuen`). Wer die Stadt ändern will, ändert das Skript.

**Koordinaten:** **Weltkoordinaten der Stadtansicht in Metern**, Godot x/z (x = Osten, z = Süden, Ursprung am Marktplatz), ohne Umrechnung. Punkte der Greybox stehen auf 5 cm genau (Köln: 0,5 m), nur die Bauzonen liegen auf dem 2-m-Raster. `punkt` ist die Mitte der Grundfläche des Bausteins (Größe aus dem Katalog), `drehung` in Grad wie Godot `Rotation.Y` (0 = Vorderseite nach Süden).

| Gebäude (Baustein im Katalog) | Inhalt |
|---|---|
| `markt`, `rathaus`, `dom` | Marktplatz (0 \| 0), Rathaus (0 \| −20), Dom (Mitte der Fläche −20 \| −11,15; der Ursprung des Modells liegt mit dem `versatz` des Katalogs bei −20 \| −14) |
| `werkstattplatz` | die vier Werkstattplätze der Stadt, je 16 × 14 m, Tür nach Süden: `Weinmarkt` (Startplatz des Spielers für jeden Beruf, ohne `betrieb`), `Giesserei_Eisenbart`, `Tischlerei_Eichwald`, `Maurerei_Kieselbach` (mit `betrieb`) – siehe „Werkstattplätze“ |
| `grube`, `holzschlag`, `steinbruch`, `kraeutergarten`, `duftgarten`, `bauernhof`, `klosterhof`, `wachturm` | die 12 Umlandbetriebe, `betrieb` = Name der Liegenschaft. **Echte Drehung:** Die Vorderseite (Halteplatz der Karren) zeigt zum Abzweig ihres Weges, außer Grube Eisenbart (−60°) und Grube am Silberberg (−110°), deren Felsberg sonst einem Nachbarn im Weg läge. `vorkommen` führt zusätzlich die sieben Rohstoffplätze |
| `felsberg` | `Berg_<Grube>` hinter jeder Grube, gleiche Drehung; Grube am Silberberg mit `massstab` 0,85 (der Berg schrumpft um den Ansatz am Felshang, 5 m hinter der Mitte der Grube) |
| `weide`, `feld`, `sammelwald` | die Arbeitsstellen der Einwohner (`StelleWeide`, `StelleFeld`, `StelleWald`, `name` wie im Kern „Weide“, „Feld“, „Wald“), das Gatter zum Zugang an der Weststraße gedreht |
| `kulisse_buergerhaus` | die vier Kulissenhäuser `Kulisse1` bis `Kulisse4`, Tür zur nächsten Gasse |
| `wohnhausplatz_ring` | die **Wohnhausplätze der Spielhäuser** `Wohnhaus1` bis `Wohnhaus8` (Doppelplätze am Ring, 17 × 8 m, `lage` = `mauerring`), die nächsten am Markt zuerst; `start` vergibt 1 bis 4 an Spieler und Rivalen |
| `einwohnerplatz_ring`, `einwohnerplatz_vorstadt` | 16 Plätze am Ring (`Ring<n>`, die Nummer zählt rundum) und 20 in der Vorstadt vor dem Osttor (`Vorstadt<n>`), **in der Reihenfolge, in der sie bebaut werden**: Ringplätze nach Abstand zum Markt (dort stehen zuerst die Ratsherren- und Bürgerhäuser), dann die Vorstadt von der Landstraße nach außen |

Gegenüber dem Code bis 21.10.2026 hat sich nur wenig verschoben, und nur wo eine Regel es verlangte: `Kulisse1` steht bei (10 | −29,5) statt (10 | −29), sonst überlappt ihre Fläche (8 × 8,5 m) das Rathaus um 0,25 m; der **Steinbruch am Felsenhang** steht bei (−4,5 | −80) statt (−6 | −80), denn mit seiner echten Drehung kam er der Zone `V8` auf 0,6 m nahe (verlangt 2 m).

**Mauer:** eine geschlossene Mauer `Stadtmauer`, Quadrat bei ±50 m, Tore Osttor (50 | 0) und Westtor (−50 | 6); Ecktürme und Torburgen setzt das Spiel aus dem Baukasten `stadtmauer`.

**Straßen:** `haupt` (Ostgasse, Westgasse), `land` (Landstraße, Weststraße, Wege zu den Gruben und Höfen), `ring` (Ringgasse) sind Karrenstraßen; Gassen und Fußwege (Domgasse, Rathausgasse, Vorstadtgasse, die vier Gassen von den Werkstattplätzen zum Markt, Fußwege zu Weide, Feld, Wald) nicht. Die Breite steht in `breite` (2 bis 5 m, nicht 6,5 m wie in Köln). Türen der Häuser und Kulissen schließt das Spiel selbst an den nächsten Weg an; damit dort nichts quer über den Markt läuft, hat die **Rathaustür** eine eigene Rathausgasse zum Markt (bisher nur im Gassennetz). Der **Weg zu einem Umlandbetrieb** endet genau an dessen Halteplatz („Anker_Karren“ des gedrehten Modells, aus der glb gelesen), der **Fußweg zu einer Umlandstelle** am Gatter („Anker_Tuer“) – so wie die Stadtansicht sie bisher zeichnete (die Karte bis 21.10.2026 führte sie vereinfacht zur Mitte). Zwei Wege haben einen Knick, damit sie die Bauzonen an ihrer alten Linie nicht streifen: zum Steinbruch am Felsenhang über (−16 | −76,7), zum Holzschlag im Eichenforst über (−97,5 | 36).

**Natur** (`natur.flaechen`, die Stadtansicht verteilt daraus Bäume und Streu):

| Fläche | Inhalt |
|---|---|
| Tannenwald um die Stadt | der Ring von 48 bis 98 m um (10 \| 0), so verschoben, wie die Stadtansicht ihn bisher mit `NachAussen` verschob (außerhalb von ±36 m 14 m nach außen, im Osten 20 m weiter für die Vorstadt); die Schneisen, die diese Verschiebung aufriss, gehören jetzt zum Wald. Östlich von x = −20, nur Tannen, Dichte 3,05 (70 Bäume auf dem Ring), Startwert 1400 |
| Mischwald im Westen | derselbe Ring westlich von x = −20: Tanne 0,55, Eiche 0,30, Buche 0,15 (bisher fest im Code), Startwert 1401 |
| Streu im Umland | 330 × 230 m um die Stadt, Dichte 2,9 (220 Gruppen), die vier Gruppen von bisher: Büsche mit Bodenpflanzen, Steine mit Gras, Totholz mit Farn und Pilzen, Wiesenblumen mit Gras |
| Am Bach beim Kräutergarten | ein Bogen 23 bis 28 m westlich um den Kräutergarten, Kopfweiden mit Schilf und Gras, Dichte 9,8 (bisher vier Gruppen) |

Aussparungen: der innere Ring, die Stadt mit 6 m Rand (±56 m) und die Vorstadt vor dem Osttor (x 50 bis 80, z ±52). Eine Aussparung darf über den Rand der Fläche hinausragen (es zählt: in der Fläche und in keiner Aussparung). Abstand zu Wegen und Gebäuden hält das Spiel selbst.

**Start und Kamera:** `start` wie bisher im Code (Spieler auf `Wohnhaus1` und `Weinmarkt`, Haus Eisenbart, Eichwald, Kieselbach auf `Wohnhaus2` bis `Wohnhaus4` mit Werkstatt und Umlandbetrieb), `kamera` beim Start (24 | −6), Zoom 95, Drehung 0.

**Werkstattplätze** (20.10.2026, Entscheidung des Nutzers): In der Stadt stehen nur noch die Startwerkstatt des Spielers „… am Weinmarkt“ und die drei Werkstätten der Rivalen, jede auf **16 × 14 m** mit der Tür nach Süden; die zehn Werkstätten zum Kauf sind gestrichen, ihre Fläche im Süden der Stadt ist seit 21.10.2026 Bauzone (siehe „Bauzonen“).

| Platz | Stadtplan-Punkt | Fläche (x, z) | Gasse zum Markt |
|---|---|---|---|
| Weinmarkt (Startwerkstatt) | (−23 \| 17) | −31 … −15, 9 … 23 (südlich der Westgasse) | vom Hof nach Westen bis (−34 \| 22,5), an der Ringgasse nach Norden bis zur Westgasse (−34 \| 6) und auf ihr zum Markt |
| Gießerei Eisenbart | (22 \| 14) | 14 … 30, 6 … 20 (südlich der Ostgasse) | nach Westen bis (12 \| 19,5), am Markt entlang nach Norden (12 \| 12), dann zum Markt |
| Tischlerei Eichwald | (22 \| −12) | 14 … 30, −20 … −6 (nördlich der Ostgasse) | nach Westen bis (12,5 \| −6,5), dann zum Markt |
| Maurerei Kieselbach | (25 \| −28) | 17 … 33, −36 … −22 (an der Ringgasse) | nach Westen bis (11 \| −22,5), zwischen Rathaus und Eichwald nach Süden (11 \| −13), dann zum Markt |

Der **Stadtplan-Punkt** ist der Punkt, den die Stadtansicht bis 21.10.2026 im `Stadtplan` führte (Mitte des alten 10 × 8-m-Hauses): Die Fläche reicht von 8 m dahinter bis 6 m davor, die Hausfront steht 4 m davor, der **Hof** (Anfang der Gasse, Karren, Gäste) 5,5 m davor im Vorplatz. Die Karte schreibt die Mitte der Fläche (1 m hinter dem Stadtplan-Punkt), der Hof liegt also 6,5 m vor ihr, 0,5 m hinter der Vorderkante. Gegenüber dem 10 × 8-Stand sind Weinmarkt (−20 | 14), Eisenbart (18 | 16) und Kieselbach (25 | −29) ein paar Meter gerückt, Eichwald (−14 | 27, hinter dem Weinmarkt) ist an die Ostgasse auf den freien Platz von Holzmarkt und Schmugglerloch gezogen. Die Gassen von Weinmarkt und Eisenbart umgehen die Bauzonen im Süden; die beiden Stichgassen vom Südring entfallen. Drei der vier Kulissenhäuser stehen jetzt an der Ringgasse im Norden und Westen: (−4 | 29) → (−8 | −32) und (5 | 29) → (1 | −32), beide nördlich vom Rathaus mit der Tür nach Norden, (−29 | 18) → (−32 | −14) westlich vom Dom mit der Tür nach Westen; (10 | −29) bleibt. Kein Einwohnerhaus fällt weg.

**Bauzonen** (11, zusammen 3.360 m²): Innerhalb der Mauer liegen seit 21.10.2026 (Entscheidung des Nutzers) drei Kernzonen im Süden der Stadt, wo vorher Werkstätten zum Kauf und drei Kulissenhäuser standen: `K1` (x 4 … 36, z 22 … 36, hinter der Gießerei Eisenbart, 2 Werkstätten), `K2` (südlich vom Markt, x −12 … 8, 1 Werkstatt und 1 Wohnhaus an der Gasse der Gießerei Eisenbart) und `K3` (x −36 … −12, z 26 … 36, hinter dem Weinmarkt, 3 Wohnhäuser). Werkstätten stehen dort mit dem Eingang an der Ringgasse (Karrenstraße), Wohnhäuser auch an der Gasse. Im Norden ist zwischen Dom, Rathaus und Ringgasse nur ein 8 m tiefer Streifen frei – zu schmal für ein Wohnhaus (8 × 10 m); dort stehen jetzt Kulissenhäuser. Die übrigen Zonen liegen vor dem Westtor an der Weststraße (`V1` bis `V3`, `V5`), am Weg zum Eichenforst und an der Straße zum Steinbruch am Felsenhang (`M1`, `V6` bis `V8`). `M1` (zwei Plätze für den Stadthof) liegt knapp vor der Westmauer und **zählt als Mauerring**, damit der Stadthof prüfbar ist (Eintrag in `fiktion`; `HOF_ZONE_ALS_MAUERRING = False` im Skript macht sie wieder zur Vorstadt). Die Einwohnerhäuser bleiben, wie sie sind: keine Zone überdeckt einen Einwohnerplatz.

**Regeln gegenüber Köln:** Die Greybox-Straßen sind 3 bis 5 m breit, die Zonen halten deshalb **Straßenrand + 0,5 m** Abstand (Köln: 3,3 m ab Straßenmitte), zu festen Gebäuden 2 m (Vorplatz, Zaun, Schild), zur Mauermitte 2 m.

**Probeplan** (`probeplan` in der Datei): Gebäude mit Zone, Mitte, Maßen und Drehung, die in die Zonen passen – 10 Plätze zu 16 × 14 m (davon 2 Stadthöfe in `M1`, 8 Werkstätten, 3 davon in Kernlage) und 11 Wohnhäuser zu 8 × 10 m (4 in Kernlage). Er dient dem Kern als Testdaten und ist keine Vorgabe für Spieler.

**Prüfungen** (`python3 tools/karten/greybox.py`, bricht bei einem Fehler ab), zusätzlich zu denen aller Karten (Abschnitt „Kartendatei“): Die Mauer ist geschlossen; jede Werkstatt der Stadt ist 16 × 14 m groß mit der Tür nach Süden, hält 2 m Abstand zur nächsten Werkstatt, steht auf keiner Mauer (geprüft ohne den Vorplatz), und an ihrem Hof beginnt ihre Gasse; keine Gasse der Stadt läuft über ein Haus (Abstand zur Mitte mindestens die halbe Breite, ohne Vorplatz; eine Gasse, die an einer Tür beginnt, gehört zu ihrem Haus); der Weg zu einem Umlandbetrieb oder einer Umlandstelle kommt von außen an den Halteplatz bzw. das Gatter; jede Zone auf dem Raster, ohne Überlappung mit einer anderen Zone, mit Abstand zu Gebäuden (Grundfläche aus dem Katalog, gedreht), Straßen und Mauer, höchstens 8 m von einer Karrenstraße; jedes Gebäude des Probeplans liegt ganz in seiner Zone, überlappt keines und erfüllt die Eingangsregel des Kerns (mindestens 2 Rasterfelder der Vorplatzreihe höchstens 7 m von der Straßenmitte; Werkstatt und Stadthof Karrenstraße, Wohnhaus jede Straße); Stadthof nur in Kern oder Mauerring; 8 Wohnhausplätze, die ersten vier gleich weit vom Markt (44,3 / 44,3 / 45,2 / 47,2 m, ±10 %), Eingang an einem Weg. Das Wegenetz hängt zusammen, Wege kreuzen die Mauer nur an den Toren. **Hinweise** (kein Abbruch): Die Felsberge der Gruben Eisenbart und Silberberg überdecken mit dem Rand ihrer Katalogfläche (52 × 38 m, Modell 45 × 32,5 m) Bauernhof bzw. Duftgarten; vier Wege im Umland laufen über die Fläche eines Umlandbetriebs (Klosterhof-Weg über den Kräutergarten, Bauernhof-Abzweig über den Wachturm, Kräutergarten-Weg und Klosterhof-Abzweig über die Grube am Silberberg) – so zeichnet die Stadtansicht sie seit jeher (offene Punkte in `docs/bereiche/leveldesign.md`).

## 10. Zuständigkeit

| Bereich | Zuständig für |
|---|---|
| Leveldesign | Kartenregeln, Kartendateien, Straßen, Bauzonen, Gelände, feste Plätze |
| Kern | jede Regel mit Wirkung: Wohnhaus, Fahrzeit aus dem Wegegraphen, Tore, Hafen, Gefahrenfaktor, Sanierung, Baukosten |
| Darstellung | Modelle, feine Bewegung, Lesbarkeit |
| Spiel-KI | Zielwahl der Bürger, Bauplatzwahl der KI-Häuser |
