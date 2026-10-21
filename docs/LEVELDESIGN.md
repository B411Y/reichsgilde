# Reichsgilde – Leveldesign

Begonnen am 16.10.2026 · Bereich **Leveldesign** (siehe `CLAUDE.md`). Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`, Abschnitt 17, neuere in `docs/bereiche/leveldesign.md`. Dieses Dokument sammelt die **Kartenregeln** für alle Städte und den Stand der einzelnen Karten.

**Stand:** Die erste Stadtkarte (Köln) liegt vor, das Spiel liest sie noch nicht. Die heutige Greybox-Stadt bleibt Platzhalter für die Mechaniken und wird vorerst nicht ersetzt; sie hat seit 17.10.2026 eine eigene Kartendatei mit Bauzonen (Abschnitt „Greybox-Karte“), damit der Kern das freie Bauen prüfen kann.

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
| Köln (Pilot) | **Kartendatei Format 1** (`game/data/karten/koeln.json`, Vorschau `docs/karten/koeln.svg`), Mauerradius 150 m: fünf Radialen von den Toren, zwei Ringstraßen um den Heumarkt, Wallgasse; 6 Bauzonen Kernlage, 12 Mauerring, 1 Vorstadt Deutz, 6 Umland; 10 Rohstoffvorkommen; 8 Startgrundstücke am äußeren Ring; linkes Rheinufer, Mauerhalbkreis mit Toren, Dom als Baustelle, Hafen, **Rheinbrücke nach Deutz (Fiktion)**, Fähre, Umland Kölner Bucht, Ville, Königsforst, Bergische Höhen, Siebengebirge, Eifel |
| Greybox (Testkarte) | **Kartendatei Format 1** (`game/data/karten/greybox.json`, Vorschau `docs/karten/greybox.svg`): die heutige, fest im Code gezeichnete Stadt in Weltkoordinaten, mit 11 Bauzonen für das freie Bauen, davon 3 in Kernlage (Abschnitt „Greybox-Karte“) |
| weitere ca. 7 | offen |

Später: **Reichskarte**, über die man zwischen den Städten wechselt (Idee festgehalten, nicht jetzt).

## 9. Kartendatei

Jede Stadt ist **eine JSON-Datei** `game/data/karten/<kennung>.json`. Sie entsteht aus einem Python-Skript `tools/karten/<kennung>.py`, nie von Hand. Das Skript prüft die Kartenregeln und schreibt nur, wenn alle erfüllt sind; dazu ein Vorschaubild `docs/karten/<kennung>.svg`, das nur aus der JSON-Datei gezeichnet wird (auf GitHub direkt ansehbar).

```bash
python3 tools/karten/koeln.py      # Köln prüfen und neu schreiben
python3 tools/karten/greybox.py    # Greybox-Stadt prüfen und neu schreiben
```

**Koordinaten:** Meter auf dem Boden, **x = Osten, z = Süden** (−z = Norden), wie im Spiel. Ursprung am Marktplatz. Bauzonen liegen auf dem **2-m-Raster**, alles andere auf 0,5 m.

| Feld | Inhalt |
|---|---|
| `format`, `kennung`, `stadt`, `vorlage` | Formatnummer (jetzt 1), Dateiname, Anzeigename, reale Vorlage |
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

**Prüfungen** (das Skript bricht ab, wenn eine fehlschlägt):
- Jede Bauzone liegt höchstens 8 m von einer Karrenstraße entfernt.
- Genau 8 Startgrundstücke, gleich weit vom Markt (±10 %), Eingang an einer Karrenstraße.
- Jedes Vorkommen liegt an einem Weg.
- Wege kreuzen eine Mauer (Stadtmauer und Nebenmauern) nur an einem Tor.
- Anleger und Kran liegen an der Kaimauer.
- Das Wegenetz hängt zusammen (Kreuzungen, Berührungen, Brücken und Fähren verbinden).
- **Kapazität** (`tools/karten/kapazitaet.py`): Das Werkzeug setzt die Gebäude probeweise auf das Raster der Stadt-Bauzonen – feste Bauten zuerst, dann die größten Gebäude, jedes möglichst nah an seiner Straße (Werkstatt an einer Karrenstraße, Wohnhaus auch an einer Gasse). **Pflicht** für 8 Häuser: je Haus zwei weitere Werkstätten, dazu die Häuser der Einwohner (13 Ratsherrenhäuser, 3 Bürgerhäuser, 40 Wohnhäuser bei 25 Einwohnern je Haus und Haushalten von im Mittel 5). Passt die Pflicht nicht, bricht das Skript ab. **Reserve** (nur Bericht): je Haus ein Stadtpalais, die Hälfte einen Stadthof. Das Ergebnis zeigt `docs/karten/<kennung>-belegung.svg`.

Ändert sich das Format, steigt `format`, und der Kern und die Darstellung passen ihre Leser an. Neue Felder ohne Bedeutungsänderung brauchen keine neue Nummer.

**Köln, Format 1 (16.10.2026):** Bauzonen Kernlage 6 (≈ 5.640 m², 1.410 Felder), Mauerring 12 (≈ 10.230 m², 2.557 Felder), Vorstadt Deutz 4 (≈ 3.960 m²), Umland 6 (≈ 39.700 m²); 41 Wege in einem Netz; 10 Vorkommen; Startgrundstücke 94 m vom Markt. **Kapazität:** Die Pflicht passt vollständig (16 Werkstätten, 56 Einwohnerhäuser), danach sind 2.691 von 4.949 Feldern frei (54 %). Reserve: alle 8 Stadtpalais und alle 4 Stadthöfe. Auf einer leeren Karte passen 29 Werkstätten (Bedarf 24 mit den Startwerkstätten). **Lehre aus der Messung:** Begrenzend ist die Blocktiefe, nicht die Länge der Straßenkante. Zusätzliche Straßen zerschneiden die Blöcke und bringen nichts (Querstraße statt Gasse: gleich viel, zusätzliche Hintergasse: weniger); schmalere Straßen machen die Blöcke tiefer.

### Greybox-Karte (17.10.2026)

`greybox.json` beschreibt die **heutige Platzhalter-Stadt** so, wie `game/scripts/Stadtansicht*.cs` sie zeichnet (Mauer bei ±50 m, Ringgasse bei ±38 m, Ost- und Westtor, Wohnviertel). Das Skript `tools/karten/greybox.py` bildet den Spielcode nach (Stadtplan, Gassen, Ringplätze, Doppelplätze) und prüft die Bauzonen; ändert sich dort etwas, muss es nachziehen. Der Spielcode wird nicht verändert.

**Koordinaten:** **Weltkoordinaten der Stadtansicht in Metern**, Godot x/z (x = Osten, z = Süden, Ursprung am Marktplatz), ohne Umrechnung. Punkte der Greybox stehen auf 5 cm genau (Köln: 0,5 m), nur die Bauzonen liegen auf dem 2-m-Raster.

**Gebäude (`feste` mit `drehung`):** `punkt` ist die Mitte, `groesse` [Breite, Tiefe] in Metern, die Breite liegt an der Vorderseite. `drehung` ist der Winkel um die Hochachse wie Godot `Rotation.Y` in Grad: Die Vorderseite zeigt bei 0 nach +z (Süden), bei 90 nach +x (Osten), bei 180 nach −z (Norden), bei 270 nach −x (Westen). `eingang` ist die Mitte der Vorderseite (der Kern nimmt daraus nur die Richtung). Der Kern zählt die Drehung in Vierteln (0 Süden, 1 Osten, 2 Norden, 3 Westen); die Datei schreibt Grad (Viertel × 90). Bei 90 und 270 liegt die Tiefe auf der x-Achse und die Breite auf der z-Achse.

| Art in `feste` | Inhalt |
|---|---|
| `sonderbau`, `turm` | Marktplatz (24 × 24 m), Rathaus, Dom, Torhäuser, vier Ecktürme |
| `werkstatt` | ein Eintrag je Werkstatt in der Stadt, je **16 × 14 m** (so groß wie die Werkstattmodelle, mit 2 m Vorplatz), Tür nach Süden; `name` ist **genau** der Name aus dem Stadtplan (auf dem Weinmarkt teilen sich zehn Einträge einen Platz, je nach Startberuf). `punkt` ist die Mitte der Fläche, 1 m hinter dem Punkt im `Stadtplan` der Stadtansicht (siehe „Werkstattplätze“) |
| `umland` | die 12 Gruben, Höfe, Gärten und der Wachturm vor der Stadt (Größen nach „Gebäudegrößen“); `vorkommen` führt zusätzlich die sieben Rohstoffplätze |
| `umlandstelle` | Weide, Feld, Wald der Einwohner vor dem Westtor |
| `einwohnerhaus`, `einwohnerplatz` | die vier alten Kulissenhäuser (8 × 8,5 m, Modell Bürgerhaus) und die 36 Plätze für Einwohnerhäuser (16 am Ring, 20 in der Vorstadt) |
| `wohnhaus` | die **Wohnhausplätze der Spielhäuser** (Doppelplätze am Ring, 17 × 8 m), in Konto-Reihenfolge, `reihenfolge` 1 bis 8, `lage` = `mauerring` (sie liegen zwischen Ringgasse und Mauer, in keiner Bauzone): Konto 1 (Spieler) bekommt Platz 1 usw., so wie `SpielhausPlaetze` sie heute zuweist. Heute spielen höchstens 4 Häuser; Platz 5 bis 8 (`haeuserAb`) gelten erst bei mehr Konten, bis dahin stehen dort Einwohnerhäuser |

**Straßen:** `haupt` (Ostgasse, Westgasse), `land` (Landstraße, Weststraße, Wege zu den Gruben und Höfen), `ring` (Ringgasse) sind Karrenstraßen; Gassen und Fußwege (Domgasse, Vorstadtgasse, die vier Gassen von den Werkstattplätzen zum Markt, Fußwege zu Weide, Feld, Wald) nicht. Die Breite steht in `breite` (3 bis 5 m, nicht 6,5 m wie in Köln).

**Werkstattplätze** (20.10.2026, Entscheidung des Nutzers): In der Stadt stehen nur noch die Startwerkstatt des Spielers „… am Weinmarkt“ und die drei Werkstätten der Rivalen, jede auf **16 × 14 m** mit der Tür nach Süden; die zehn Werkstätten zum Kauf sind gestrichen, ihre Fläche im Süden der Stadt ist seit 21.10.2026 Bauzone (siehe „Bauzonen“).

| Platz | Stadtplan-Punkt | Fläche (x, z) | Gasse zum Markt |
|---|---|---|---|
| Weinmarkt (Startwerkstatt) | (−23 \| 17) | −31 … −15, 9 … 23 (südlich der Westgasse) | vom Hof nach Westen bis (−34 \| 22,5), an der Ringgasse nach Norden bis zur Westgasse (−34 \| 6) und auf ihr zum Markt |
| Gießerei Eisenbart | (22 \| 14) | 14 … 30, 6 … 20 (südlich der Ostgasse) | nach Westen bis (12 \| 19,5), am Markt entlang nach Norden (12 \| 12), dann zum Markt |
| Tischlerei Eichwald | (22 \| −12) | 14 … 30, −20 … −6 (nördlich der Ostgasse) | nach Westen bis (12,5 \| −6,5), dann zum Markt |
| Maurerei Kieselbach | (25 \| −28) | 17 … 33, −36 … −22 (an der Ringgasse) | nach Westen bis (11 \| −22,5), zwischen Rathaus und Eichwald nach Süden (11 \| −13), dann zum Markt |

Der **Stadtplan-Punkt** ist der Punkt, den die Stadtansicht im `Stadtplan` führt (Mitte des alten 10 × 8-m-Hauses): Die Fläche reicht von 8 m dahinter bis 6 m davor, die Hausfront steht 4 m davor, der **Hof** (Anfang der Gasse, Karren, Gäste) 5,5 m davor im Vorplatz. Die Karte schreibt die Mitte der Fläche (1 m hinter dem Stadtplan-Punkt). Gegenüber dem 10 × 8-Stand sind Weinmarkt (−20 | 14), Eisenbart (18 | 16) und Kieselbach (25 | −29) ein paar Meter gerückt, Eichwald (−14 | 27, hinter dem Weinmarkt) ist an die Ostgasse auf den freien Platz von Holzmarkt und Schmugglerloch gezogen. Die Gassen von Weinmarkt und Eisenbart umgehen die Bauzonen im Süden; die beiden Stichgassen vom Südring entfallen. Drei der vier Kulissenhäuser stehen jetzt an der Ringgasse im Norden und Westen: (−4 | 29) → (−8 | −32) und (5 | 29) → (1 | −32), beide nördlich vom Rathaus mit der Tür nach Norden, (−29 | 18) → (−32 | −14) westlich vom Dom mit der Tür nach Westen; (10 | −29) bleibt. Kein Einwohnerhaus fällt weg.

**Bauzonen** (11, zusammen 3.360 m²): Innerhalb der Mauer liegen seit 21.10.2026 (Entscheidung des Nutzers) drei Kernzonen im Süden der Stadt, wo vorher Werkstätten zum Kauf und drei Kulissenhäuser standen: `K1` (x 4 … 36, z 22 … 36, hinter der Gießerei Eisenbart, 2 Werkstätten), `K2` (südlich vom Markt, x −12 … 8, 1 Werkstatt und 1 Wohnhaus an der Gasse der Gießerei Eisenbart) und `K3` (x −36 … −12, z 26 … 36, hinter dem Weinmarkt, 3 Wohnhäuser). Werkstätten stehen dort mit dem Eingang an der Ringgasse (Karrenstraße), Wohnhäuser auch an der Gasse. Im Norden ist zwischen Dom, Rathaus und Ringgasse nur ein 8 m tiefer Streifen frei – zu schmal für ein Wohnhaus (8 × 10 m); dort stehen jetzt Kulissenhäuser. Die übrigen Zonen liegen vor dem Westtor an der Weststraße (`V1` bis `V3`, `V5`), am Weg zum Eichenforst und an der Straße zum Steinbruch am Felsenhang (`M1`, `V6` bis `V8`). `M1` (zwei Plätze für den Stadthof) liegt knapp vor der Westmauer und **zählt als Mauerring**, damit der Stadthof prüfbar ist (Eintrag in `fiktion`; `HOF_ZONE_ALS_MAUERRING = False` im Skript macht sie wieder zur Vorstadt). Die Einwohnerhäuser bleiben, wie sie sind: keine Zone überdeckt einen Einwohnerplatz.

**Regeln gegenüber Köln:** Die Greybox-Straßen sind 3 bis 5 m breit, die Zonen halten deshalb **Straßenrand + 0,5 m** Abstand (Köln: 3,3 m ab Straßenmitte), zu festen Gebäuden 2 m (Vorplatz, Zaun, Schild), zur Mauermitte 2 m.

**Probeplan** (`probeplan` in der Datei): Gebäude mit Zone, Mitte, Maßen und Drehung, die in die Zonen passen – 10 Plätze zu 16 × 14 m (davon 2 Stadthöfe in `M1`, 8 Werkstätten, 3 davon in Kernlage) und 11 Wohnhäuser zu 8 × 10 m (4 in Kernlage). Er dient dem Kern als Testdaten und ist keine Vorgabe für Spieler.

**Prüfungen** (`python3 tools/karten/greybox.py`, bricht bei einem Fehler ab): jede Werkstatt der Stadt ist 16 × 14 m groß, überlappt kein anderes Gebäude, hält 2 m Abstand zur nächsten Werkstatt, steht auf keiner Straße und keiner Mauer (geprüft ohne den Vorplatz), hat ihren Hof im Vorplatz, und dort beginnt ihre Gasse; keine Gasse der Stadt läuft über ein Haus (Abstand zur Mitte mindestens die halbe Breite; eine Gasse, die an einer Tür beginnt, gehört zu ihrem Haus); jede Zone auf dem Raster, ohne Überlappung mit einer anderen Zone, mit Abstand zu Gebäuden, Straßen und Mauer, höchstens 8 m von einer Karrenstraße; jedes Gebäude des Probeplans liegt ganz in seiner Zone, überlappt keines und erfüllt die Eingangsregel des Kerns (mindestens 2 Rasterfelder der Vorplatzreihe höchstens 7 m von der Straßenmitte; Werkstatt und Stadthof Karrenstraße, Wohnhaus jede Straße); Stadthof nur in Kern oder Mauerring; Wohnhäuser 1 bis 4 gleich weit vom Markt (44,3 / 44,3 / 45,2 / 47,2 m, ±10 %). Das Wegenetz hängt zusammen, Wege kreuzen die Mauer nur an den Toren.

## 10. Zuständigkeit

| Bereich | Zuständig für |
|---|---|
| Leveldesign | Kartenregeln, Kartendateien, Straßen, Bauzonen, Gelände, feste Plätze |
| Kern | jede Regel mit Wirkung: Wohnhaus, Fahrzeit aus dem Wegegraphen, Tore, Hafen, Gefahrenfaktor, Sanierung, Baukosten |
| Darstellung | Modelle, feine Bewegung, Lesbarkeit |
| Spiel-KI | Zielwahl der Bürger, Bauplatzwahl der KI-Häuser |
