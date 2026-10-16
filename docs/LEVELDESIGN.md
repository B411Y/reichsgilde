# Reichsgilde – Leveldesign

Begonnen am 16.10.2026 · Bereich **Leveldesign** (siehe `CLAUDE.md`). Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`, Abschnitt 17, neuere in `docs/bereiche/leveldesign.md`. Dieses Dokument sammelt die **Kartenregeln** für alle Städte und den Stand der einzelnen Karten.

**Stand:** Die erste Kartendatei (Köln) liegt vor, das Spiel liest sie noch nicht. Die heutige Greybox-Stadt bleibt Platzhalter für die Mechaniken und wird vorerst nicht ersetzt.

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
- **Zustände:** frei → bebaut → leerstehend (Haus erloschen oder bankrott) → verfallen. Leerstand lässt sich kaufen und sanieren (Kosten nach Zustand). *(Kern)*

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
| weitere ca. 7 | offen |

Später: **Reichskarte**, über die man zwischen den Städten wechselt (Idee festgehalten, nicht jetzt).

## 9. Kartendatei

Jede Stadt ist **eine JSON-Datei** `game/data/karten/<kennung>.json`. Sie entsteht aus einem Python-Skript `tools/karten/<kennung>.py`, nie von Hand. Das Skript prüft die Kartenregeln und schreibt nur, wenn alle erfüllt sind; dazu ein Vorschaubild `docs/karten/<kennung>.svg`, das nur aus der JSON-Datei gezeichnet wird (auf GitHub direkt ansehbar).

```bash
python3 tools/karten/koeln.py      # Köln prüfen und neu schreiben
```

**Koordinaten:** Meter auf dem Boden, **x = Osten, z = Süden** (−z = Norden), wie im Spiel. Ursprung am Marktplatz. Bauzonen liegen auf dem **2-m-Raster**, alles andere auf 0,5 m.

| Feld | Inhalt |
|---|---|
| `format`, `kennung`, `stadt`, `vorlage` | Formatnummer (jetzt 1), Dateiname, Anzeigename, reale Vorlage |
| `einheit`, `achsen`, `raster` | Meter, Achsen wie oben, Rastergröße 2 |
| `mauer.linie` | Stadtmauer als Punktfolge |
| `tore` | Name, Punkt, `schliesstNachts` |
| `wasser` | `fluss`/`bach` als Linie mit `breite`, `see` als Fläche |
| `gelaende` | Flächen der Art `feld`, `wald`, `huegel`, `wildnis` |
| `wege` | Name, `klasse` (`land`, `haupt`, `ring`, `wall` = Karrenstraßen; `gasse`, `leinpfad` = nur Fußgänger), `linie`, `gefahr` (Faktor um 1,0, Vorschlag) |
| `uebergaenge` | `bruecke` oder `faehre` mit `von` und `bis` |
| `bauzonen` | `kennung`, `art` (`kern`, `mauerring`, `vorstadt`, `umland`), `flaeche` auf dem Raster |
| `feste` | `kennung`, `art` (`start`, `sonderbau`, `kirche`, `leerstand`), `name`, `punkt`; Startgrundstücke mit `eingang` und `groesse` [Breite, Tiefe] (24 × 14 m: Werkstatt und Wohnhaus nebeneinander) |
| `vorkommen` | `kennung`, `betrieb` (`Mine`, `Steinbruch`, `Holzschlag`), `punkt` |
| `fiktion` | Liste der Abweichungen von der Vorlage |

**Prüfungen** (das Skript bricht ab, wenn eine fehlschlägt):
- Jede Bauzone liegt höchstens 8 m von einer Karrenstraße entfernt.
- Genau 8 Startgrundstücke, gleich weit vom Markt (±10 %), Eingang an einer Karrenstraße.
- Jedes Vorkommen liegt an einem Weg.
- Das Wegenetz hängt zusammen (Kreuzungen, Berührungen, Brücken und Fähren verbinden).
- **Kapazität** (`tools/karten/kapazitaet.py`): Das Werkzeug setzt die Gebäude probeweise auf das Raster der Stadt-Bauzonen – feste Bauten zuerst, dann die größten Gebäude, jedes möglichst nah an seiner Straße (Werkstatt an einer Karrenstraße, Wohnhaus auch an einer Gasse). **Pflicht** für 8 Häuser: je Haus zwei weitere Werkstätten, dazu die Häuser der Einwohner (13 Ratsherrenhäuser, 3 Bürgerhäuser, 40 Wohnhäuser bei 25 Einwohnern je Haus und Haushalten von im Mittel 5). Passt die Pflicht nicht, bricht das Skript ab. **Reserve** (nur Bericht): je Haus ein Stadtpalais, die Hälfte einen Stadthof. Das Ergebnis zeigt `docs/karten/<kennung>-belegung.svg`.

Ändert sich das Format, steigt `format`, und der Kern und die Darstellung passen ihre Leser an. Neue Felder ohne Bedeutungsänderung brauchen keine neue Nummer.

**Köln, Format 1 (16.10.2026):** Bauzonen Kernlage 6 (≈ 5.640 m², 1.410 Felder), Mauerring 12 (≈ 10.230 m², 2.557 Felder), Vorstadt Deutz 4 (≈ 3.960 m²), Umland 6 (≈ 39.700 m²); 41 Wege in einem Netz; 10 Vorkommen; Startgrundstücke 94 m vom Markt. **Kapazität:** Die Pflicht passt vollständig (16 Werkstätten, 56 Einwohnerhäuser), danach sind 2.691 von 4.949 Feldern frei (54 %). Reserve: alle 8 Stadtpalais und alle 4 Stadthöfe. Auf einer leeren Karte passen 29 Werkstätten (Bedarf 24 mit den Startwerkstätten). **Lehre aus der Messung:** Begrenzend ist die Blocktiefe, nicht die Länge der Straßenkante. Zusätzliche Straßen zerschneiden die Blöcke und bringen nichts (Querstraße statt Gasse: gleich viel, zusätzliche Hintergasse: weniger); schmalere Straßen machen die Blöcke tiefer.

## 10. Zuständigkeit

| Bereich | Zuständig für |
|---|---|
| Leveldesign | Kartenregeln, Kartendateien, Straßen, Bauzonen, Gelände, feste Plätze |
| Kern | jede Regel mit Wirkung: Wohnhaus, Fahrzeit aus dem Wegegraphen, Tore, Hafen, Gefahrenfaktor, Sanierung, Baukosten |
| Darstellung | Modelle, feine Bewegung, Lesbarkeit |
| Spiel-KI | Zielwahl der Bürger, Bauplatzwahl der KI-Häuser |
