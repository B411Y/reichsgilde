# Bereich Leveldesign – Offen und Entscheidungen

**Nur der Leveldesign-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (–) Reichskarte zum Wechsel zwischen den Städten (Idee, später)
- Level-Editor (Grundsatz siehe „Level-Editor und Wege“): vor dem Start Umfang (Gelände, Wasser, Bauzonen, feste Gebäude) und Reihenfolge klären – Kartenformat 2 und Gebäudekatalog stehen seit PR #163; Vorschlag: Wegegraph im Kern, Wege in der Darstellung, dann der Editor
- Umlandmodelle sind größer als ihre Fläche im Katalog (z. B. Grube 22 × 18 m auf 16 × 14, Wachturm 12 × 12 auf 4 × 4; die Katalogprüfung des Kartenskripts meldet es als Hinweis). Entscheidung des Kerns: Die Fläche bleibt. Folge: Natur und Wege müssen in der Darstellung um das Modell herum frei bleiben, nicht nur um die Fläche; Bauzonen halten 2 m zur Fläche, nicht zum Modell
- Greybox: Vier Wege im Umland laufen über die Fläche eines Umlandbetriebs (Klosterhof-Weg über den Kräutergarten, Bauernhof-Abzweig über den Wachturm, Kräutergarten-Weg und Klosterhof-Abzweig über die Grube am Silberberg) – so zeichnet die Stadtansicht sie seit jeher; die Prüfung meldet es als Hinweis. Vorschlag: mit dem Level-Editor die Wege dort neu führen
- Felsberg im Katalog 52 × 38 m, das Modell `berg_grube` nur 45 × 32,5 m; mit dem Rand überdecken die Felsberge der Gruben Eisenbart und Silberberg Bauernhof und Duftgarten (Hinweis der Prüfung). Vorschlag an Kern/Modell: Katalogfläche 46 × 34 m
- Greybox mit weniger als 8 Häusern: Auf den Wohnhausplätzen 5 bis 8 standen bisher Einwohnerhäuser; Format 2 hat dort keine Einwohnerplätze. Ob unbenutzte Wohnhausplätze Einwohner aufnehmen, entscheiden Darstellung und Kern
- Kartenwerkstatt: Das Gassennetz der Werkstatt hängt Markt, Tore, Höfe, Karrenplätze und (seit PR #172) die Gatter der Arbeitsstellen an, aber nicht die Türen von Rathaus, Dom und Häusern (im Spiel schon) – „alles verbunden“ heißt dort also nur: alle Wege und diese Ziele
- Kartenwerkstatt: Das Raster einer Bauzone zeigt jedes Feld, dessen Mitte in der Zone liegt, mit allen vier Kanten – an schrägen Rändern ragt es über die Fläche hinaus (wie im Baumodus des Spiels)
- (von Kern) Köln: Die Bauzone M5 kreuzt sich selbst – die schließende Kante von (−82 | −30) zurück nach (−114 | −44) kreuzt bei (−112,4 | −43,3) die zweite Kante; die Kartenwerkstatt meldet es als Hinweis (PR #170). In der Kartenwerkstatt die Ecken ordnen
- (von Kern) Köln: St. Gereon (`Gereon`) und der Leerstand `L1` stehen auf der Stadtmauer (das Haus weniger als 1 m von der Mauerlinie); die Kartenwerkstatt meldet es seit PR #172 als Hinweis (mit den Stadtregeln wäre es ein Fehler). In der Kartenwerkstatt verschieben oder die Mauer dort führen
- Kartenwerkstatt: Der Mauer-Baukasten ist nicht wählbar (es gibt nur `stadtmauer`)
- Kartenwerkstatt: Beim ersten Klicktest der Probepartie (neue Karte, PR #172) meldete Godot einmal beim Laden der Figurenmodelle „Handle is not initialized“ (`GCHandle`, 81-mal); in drei weiteren Probeläufen nicht wieder, in Schritt 4 (PR #174) in drei Probepartien auch nicht – wohl aber 5-mal im ersten Start nach `--headless --import` (Kartenwerkstatt mit der Greybox, `ResourceLoader.Load` in `Stadtansicht.Modell` → `ScriptManagerBridge.SwapGCHandleForType`), im nächsten Start nicht mehr. Hängt also am ersten Start nach dem Import, nicht an der Probepartie. Beobachten
- Kartenwerkstatt: Der Speicherort im exportierten Spiel (Karten des Spiels nur lesen, `Entwicklung` = false) ist nur gebaut, nicht im Klicktest gesehen – beim ersten Export einmal öffnen und speichern
- Kartenwerkstatt: „Öffnen“ zeigt alle Karten untereinander ohne Rollbalken – bei vielen eigenen Karten wird die Rückfrage höher als das Fenster
- Kartenwerkstatt: Die Vorschau beim Ziehen einer Mauerecke rechnet die wandernden Tore wie der Kern nach (`ToreNachher`); ändert der Kern die Regel (`AufStrecke`/`AufLinie`), muss die Vorschau mit
- Köln im Spiel (`--karte=koeln`): ohne Startbelegung warnt die Stadtansicht „Für „Gießerei Eisenbart“ … gibt es keinen Platz“ (die Ersatzhäuser haben in Köln keine Plätze), das Gassennetz hat 2 Teile, die Vorstadt hat keine Einwohnerplätze – mit der Startbelegung von Köln in der Kartenwerkstatt beheben

## Entscheidungen

Die Kartenregeln selbst stehen in `docs/LEVELDESIGN.md`.

### Kartendatei und Köln (PR #72)

| Thema | Entscheidung | Datum |
|---|---|---|
| Format | **Eine JSON-Datei je Stadt** in `game/data/karten/<kennung>.json`, lesbar für Kern (ohne Godot) und Darstellung. Format-Nummer 1. Beschreibung in `docs/LEVELDESIGN.md`, Abschnitt „Kartendatei“. | 16.10.2026 |
| Erstellung | **Python-Werkzeug** `tools/karten/karte.py`, je Stadt ein Skript (`tools/karten/koeln.py`). Es prüft die Kartenregeln und schreibt nur bei Erfolg; dazu ein Vorschaubild `docs/karten/<kennung>.svg`. | 16.10.2026 |
| Koordinaten | Meter, x = Osten, z = Süden (−z = Norden), Ursprung am Marktplatz – wie im Spiel. Bauzonen auf dem 2-m-Raster. | 16.10.2026 |
| Größe Köln | **Mauerhalbkreis mit 150 m Radius** (die Greybox-Stadt misst innen 100 m). | 16.10.2026 |

### Gebäudegrößen (PR #76)

| Thema | Entscheidung | Datum |
|---|---|---|
| Grundsatz | Ein Gebäude **belegt beim Bau die Fläche seiner größten Ausbaustufe** am selben Platz; Fläche in Rasterfeldern mit 2 m Vorplatz. | 16.10.2026 |
| Werkstatt | **Gleiche Fläche für Stufe 1–3, aber größer als heute: 16 × 14 m (56 Felder).** Jede Stufe soll von außen sichtbar größer werden (Anbau, Hof). | 16.10.2026 |
| Wohnhaus | **Fachwerkhaus belegt die Fläche des Steinhauses (8 × 10 m, 20 Felder)** und wächst am Platz über Bürgerhaus zum Steinhaus. **Stadtpalais (12 × 10 m) und Hof (16 × 14 m)** sind ein neuer Bau an anderer Stelle oder Abriss und Neubau. | 16.10.2026 |
| Aussehen | Gebäude behalten ihr Aussehen, Maße auf **glatte Meter** (Einwohnerhäuser 6 / 7 / 8 m). | 16.10.2026 |

### Stadthof (PR #84)

| Thema | Entscheidung | Datum |
|---|---|---|
| Name | Die höchste Wohnhausstufe heißt **Stadthof** statt „Hof“, damit sie nicht mit Bauernhof und Klosterhof im Umland verwechselt wird. | 16.10.2026 |
| Lage | Der Stadthof darf in der **Kernlage und im Mauerring** stehen (vorher nur Kernlage; dort passte in Köln nur einer). | 16.10.2026 |

### Straßenbreite (PR #88)

| Thema | Entscheidung | Datum |
|---|---|---|
| Breite | **Straßen in der Stadt gut 6,5 m breit**: Bauzonen beginnen 3,3 m neben der Straßenmitte (vorher 4,6 m). Köln: 29 statt 25 Werkstätten auf leerer Karte, alle 4 Stadthöfe passen. | 16.10.2026 |
| Keine Querstraßen | Zusätzliche Straßen durch die Mauerring-Blöcke wurden gemessen und **verworfen**: Sie zerschneiden die Blöcke, ohne mehr Platz zu bringen (begrenzend ist die Blocktiefe). | 16.10.2026 |

### Deutzer Mauer und Hafen (PR #89)

| Thema | Entscheidung | Datum |
|---|---|---|
| Kartendatei | Neue Felder **`nebenmauern`** (weitere Mauern) und **`haefen`** (Kaimauer, Anleger, Kran). Bestehende Felder bleiben gleich, deshalb bleibt es **Format 1**. | 16.10.2026 |
| Köln | **Deutzer Mauer** um den Brückenkopf, zum Rhein hin offen, mit **Nordtor** (Mülheimer Weg) und **Osttor** (Bensberger Straße); beide schließen nachts. **Kölner Hafen**: Kaimauer am linken Ufer zwischen Dom und Heumarkt, 5 Anleger, Kran. Die Stadtmauer endet jetzt an den Ufertürmen. | 16.10.2026 |
| Prüfungen | Wege kreuzen Mauern nur an Toren; Anleger und Kran liegen an der Kaimauer. | 16.10.2026 |

### Greybox-Karte mit Bauzonen (PR #103)

| Thema | Entscheidung | Datum |
|---|---|---|
| Karte | Die **heutige Greybox-Stadt** bekommt eine Kartendatei `game/data/karten/greybox.json` (Werkzeug `tools/karten/greybox.py`, Vorschau `docs/karten/greybox.svg`), damit der Kern das freie Bauen prüfen kann. Köln bleibt unberührt. Schema wie Köln (Format 1), neue Felder: `breite` je Weg, `drehung` je Gebäude, `gebaeude`, `probeplan`. | 17.10.2026 |
| Koordinaten | **Weltkoordinaten der Stadtansicht in Metern** (Godot x/z), keine Umrechnung. Drehung wie Godot `Rotation.Y` in Grad: 0 = Vorderseite nach +z, 90 = +x, 180 = −z, 270 = −x. | 17.10.2026 |
| Wohnhausplätze | Die **Doppelplätze am Ring** stehen als `wohnhaus` 1 bis 8 in der Reihenfolge, in der die Darstellung sie den Konten zuweist; heute spielen höchstens 4 Häuser. Die ersten vier liegen gleich weit vom Markt (44,3 bis 47,2 m). | 17.10.2026 |
| Bauzonen | **9 Zonen, 2.432 m²**: 1 Kern (Südostecke, ein Wohnhaus), 7 Vorstadt vor dem Westtor und an der Straße zum Steinbruch am Felsenhang, 1 Mauerring. Innerhalb der Mauer ist die Stadt voll, freies Gelände an einer Straße gibt es nur vor dem Westtor. Im Probeplan passen 5 Werkstätten, 2 Stadthöfe und 8 Wohnhäuser. | 17.10.2026 |
| Stadthof | Innerhalb der Mauer ist kein Platz für 16 × 14 m frei. Die Zone `M1` an der Westmauer **zählt deshalb als Mauerring** (in `fiktion` vermerkt); `HOF_ZONE_ALS_MAUERRING = False` im Skript macht sie zur Vorstadt. | 17.10.2026 |
| Kern liest | `werkstatt`: ein Eintrag je Liegenschaft, `name` genau wie im Stadtplan; `wohnhaus`: Konto-Reihenfolge; jedes feste Gebäude mit `groesse` blockiert; Eingangsregel: 2 Felder der Vorplatzreihe höchstens 7 m von der Straßenmitte (Probeplan erfüllt sie). Einwohnerhäuser kennt der Kern nicht, die Zonen decken nur freien Boden ab. | 17.10.2026 |
| Abstände | Greybox-Straßen sind nur 3 bis 5 m breit: Zonen halten **Straßenrand + 0,5 m** Abstand (Köln 3,3 m ab Straßenmitte), 2 m zu festen Gebäuden. Einwohnerhäuser bleiben unverändert, keine Zone überdeckt einen Einwohnerplatz. | 17.10.2026 |
| Leerstand | Leerstand kaufen und sanieren kostet **fest 50 % + 20 % des Hauswerts** (Entscheidung des Nutzers), nicht mehr nach Zustand. | 17.10.2026 |

### Level-Editor und Wege (Grundsatz, PR #151)

Noch nicht begonnen; festgehalten, damit niemand weiter Arbeit in die Greybox-Wege steckt.

| Thema | Entscheidung | Datum |
|---|---|---|
| Editor | Ein selbst programmierter Level-Editor im Spiel, erreichbar aus dem Hauptmenü, mit derselben Darstellung wie das Spiel. Erst Werkzeug für den Entwickler (die rund acht Städte), später für Spieler freigegeben. Entschieden vom Nutzer. | 19.10.2026 |
| Kartenquelle | Der Editor ersetzt die Python-Skripte in `tools/karten/`: Er schreibt die Kartendatei selbst, die Prüfungen (Bauzonen an Straßen, Tore, Kapazität) wandern nach C# und laufen im Editor mit. Köln und Greybox werden einmal übernommen. Damit fällt die Regel „Kartendatei nur aus dem Skript, nie von Hand“, sobald der Editor da ist. Entschieden vom Nutzer. | 19.10.2026 |
| Wege zeichnen | Punkte setzen, das Werkzeug legt eine glatte Kurve hindurch; Kreuzungen rasten ein; Mindestbreite je Wegklasse. Vorschlag des Teamleiters. | 19.10.2026 |
| Belag | Verkehr tritt einen Trampelpfad zum Fahrweg mit Spurrillen aus, wenig Verkehr lässt ihn verwildern; Kies und Pflaster sind ein Bauauftrag, den der Rat oder der Spieler bezahlt. Der Belag wirkt auf das Tempo der Karren und bei Regen. Entschieden vom Nutzer. | 19.10.2026 |
| Gegenverkehr | Nur sichtbar: Rechtsverkehr, Fußgänger am Rand, Karren in der Mitte, Ausweichen macht die Darstellung. Der Kern rechnet Fahrzeiten aus einem groben Wegegraphen (Länge, Klasse, Belag), ohne Stau (passt zu „Zwei Ebenen“ in `docs/LEVELDESIGN.md`, Wege und Wegfindung). Entschieden vom Nutzer. | 19.10.2026 |

### Werkstattplätze 16 × 14 m in der Greybox (PR #162)

| Thema | Entscheidung | Datum |
|---|---|---|
| Plätze | Jeder Werkstattplatz der Greybox wird 16 × 14 m groß (so groß sind die Werkstattmodelle); wo es eng wird, rücken Nachbarn auseinander oder ein Einwohnerhaus entfällt. Die Plätze der 10 gestrichenen Werkstätten zum Kauf werden frei. Karte wie immer aus dem Kartenwerkzeug, Stadtplan und Gassen ziehen mit. Entschieden vom Nutzer. | 20.10.2026 |
| Umsetzung: Plätze | Vier Werkstätten in der Stadt, Stadtplan-Punkt (Tür nach Süden): Weinmarkt (−23 \| 17), Gießerei Eisenbart (22 \| 14), Tischlerei Eichwald (22 \| −12, zieht von hinter dem Weinmarkt an die Ostgasse), Maurerei Kieselbach (25 \| −28). Kein Einwohnerhaus entfällt; drei Kulissenhäuser ziehen aus dem Süden an die Ringgasse im Norden und Westen (siehe „Kernzonen in der Stadt“). Tabelle in `docs/LEVELDESIGN.md`, „Greybox-Karte“. | 20.10.2026 |
| Umsetzung: Karte | `greybox.json`: Werkstätten 16 × 14 m, `punkt` = Mitte der Fläche (1 m hinter dem Stadtplan-Punkt), Gassen vom Hof am Vorplatz entlang zum Markt, Domgasse ab den Domstufen, Dom mit Westturm (12,5 × 30,2 m), Kulissenhäuser 8 × 8,5 m. Neue Prüfungen im Werkzeug: Werkstätten ohne Überlappung, 2 m Abstand, nicht auf Straße oder Mauer, Hof im Vorplatz mit eigener Gasse; keine Gasse über ein Haus. | 20.10.2026 |
| Umsetzung: Darstellung | `Stadtplan`, `Gassen` (Weinmarkt: `WeinmarktGasse`) und `KulissePlaetze` auf die neuen Plätze, die Stichgassen (`RingAnschluesse`) entfallen; Bauplatz-Zaun und Klickfläche eines freien Werkstattplatzes 16 × 14 m. Die Lagen stehen weiter doppelt (Stadtansicht und `greybox.py`) – zusammenführen später, siehe „Offen“. | 20.10.2026 |

### Kernzonen in der Stadt (PR #162)

| Thema | Entscheidung | Datum |
|---|---|---|
| Bauzonen in der Stadt | Die frei gewordene Fläche der gestrichenen Werkstätten wird Bauzone in Kernlage (Variante b, Entscheidung des Nutzers). Platz für drei Werkstätten (16 × 14 m, Eingang an der Ringgasse) und vier Wohnhäuser. | 21.10.2026 |
| Lage | Drei Zonen im Süden der Stadt: `K1` hinter der Gießerei Eisenbart (2 Werkstätten), `K2` südlich vom Markt (1 Werkstatt, 1 Wohnhaus an der Gasse der Gießerei Eisenbart), `K3` hinter dem Weinmarkt (3 Wohnhäuser). Der Norden (zwischen Dom, Rathaus und Ringgasse nur 8 m tief) bleibt ohne Zone. Zusammen 11 Bauzonen. | 21.10.2026 |
| Platz dafür | Der Weinmarkt rückt 2 m nach Westen (−23 \| 17), seine Gasse läuft nach Westen und an der Ringgasse entlang zur Westgasse; die Gasse der Gießerei Eisenbart läuft am Markt entlang. Die Stichgassen vom Südring entfallen. Drei Kulissenhäuser ziehen an die Ringgasse im Norden (−8 \| −32), (1 \| −32) und Westen (−32 \| −14). Keine Werkstatt wird gedreht, alle Türen zeigen weiter nach Süden. | 21.10.2026 |
| Probeplan | Sieben Stellen dazu (3 Werkstätten, 4 Wohnhäuser in K1 bis K3), die alte Wohnhaus-Stelle in K1 entfällt: 21 statt 15 Gebäude. | 21.10.2026 |

### Gebäudekatalog und Karte als einzige Quelle (PR #163)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ziel | Vorstufe zum Level-Editor: Alles, was in Stadt und Umland steht, kommt aus der Kartendatei; der Code kennt keine festen Lagen mehr (heute doppelt in `Stadtansicht*.cs` und `tools/karten/greybox.py`). Ein verschobenes oder neues Gebäude ist nur ein Eintrag in der Karte. | 21.10.2026 |
| Katalog | Eine Datei `game/data/katalog.json` für alle Karten: je Baustein Kennung, Art (Werkstatt, Wohnhaus, Einwohnerhaus, Kulisse, Sonderbau, Mauerteil, Umland, Umlandstelle, Natur …), Modelldatei, Grundfläche, bei Werkstätten der Beruf. Anker (Tür, Hof, Karren) liest das Spiel wie heute aus dem Modell. Neue Modelle trägt der Modell-Agent gleich ein; ein Prüfwerkzeug vergleicht Katalog und Modelle. | 21.10.2026 |
| Karte | Kartenformat 2: Die Karte sagt nur, **wo** etwas steht (Katalog-Kennung, Punkt, Drehung, Name), dazu Mauer, Tore, Wege, Bauzonen, Wald- und Streuflächen, Umland. Werkstattplätze bleiben Plätze; das Modell folgt dem Beruf des Betriebs. | 21.10.2026 |
| Gassennetz | Aus den Wegen der Karte; jede Tür, jeder Hof und jede Umlandstelle wird automatisch mit dem nächsten Weg verbunden. | 21.10.2026 |
| Natur | Waldflächen und Streugebiete als Vielecke mit Dichte, Mischung der Arten und Startwert; einzelne Bäume dürfen zusätzlich gesetzt werden. | 21.10.2026 |
| Startbelegung | Die Karte legt die Häuser am Start fest (Name, Wohnhaus, Startbetriebe mit Platz und Betriebsart) und den Startplatz des Spielers je Beruf; der Kern liest das statt der festen Namen. | 21.10.2026 |
| Werkzeug | Bis zum Level-Editor erzeugt weiter das Python-Werkzeug (`tools/karten/`) die Karten und prüft die Regeln. | 21.10.2026 |
| Umsetzung: Werkzeug | `karte.py` schreibt Format 2 direkt (`gebaeude` mit Katalog-Kennung, `mauern` mit `tore`, `natur`, `start`, `kamera`); die einmalige Umstellung des Kerns ist ersetzt. Neue Prüfungen gegen den Katalog: Baustein bekannt und erlaubt, keine Überlappung der gedrehten Grundflächen (Felsberg nur Hinweis), Hof jedes Werkstattplatzes an einem Weg, Mauer geschlossen bzw. offen mit den Toren auf der Linie, Startbelegung, Betriebsnamen eindeutig, Natur-Arten im Katalog. Die Kapazitätsprüfung belegt jedes Gebäude mit seiner Katalogfläche. Die Vorschau zeichnet Gebäude gedreht in Kataloggröße und die Naturflächen. | 21.10.2026 |
| Umsetzung: Katalog ↔ Modelle | `tools/karten/katalog_pruefen.py`: Fehler, wenn eine Modelldatei oder ein Knoten (bei Natur jede Variante) fehlt; Warnung, wenn das Modell mehr als 0,5 m über seine Fläche ragt oder sie nicht zur Hälfte füllt. Stand: 0 Fehler, 11 Warnungen (die 8 Umlandbetriebe, Weide, zwei Einwohnerhäuser). | 21.10.2026 |
| Umsetzung: Greybox | `greybox.json` enthält alle Lagen, die bis 21.10.2026 im Code der Stadtansicht standen: Werkstatt-, Wohnhaus- und Einwohnerplätze (in der Reihenfolge der Bebauung), Kulissen, Markt, Rathaus, Dom, Mauer mit Toren, Umlandbetriebe mit echter Drehung, Felsberge, Umlandstellen, alle Wege (Weg zum Umlandbetrieb bis an den Halteplatz, Fußweg bis ans Gatter, neue Rathausgasse), Wald und Streu, Kamera. Verschoben, weil eine Regel es verlangte: Kulisse 1 um 0,5 m (überlappte das Rathaus), Steinbruch am Felsenhang um 1,5 m (zu nah an Zone V8); zwei Umlandwege mit Knick an der alten Linie, damit die Bauzonen bleiben. Abschnitt „Greybox-Karte“ in `docs/LEVELDESIGN.md`. | 21.10.2026 |
| Umsetzung: Natur der Greybox | Der Waldring genau so, wie die Stadtansicht ihn mit `NachAussen` verschob (die Schneisen der Verschiebung gehören jetzt zum Wald), geteilt bei x = −20: östlich Tannenwald, westlich Mischwald mit Eichen und Buchen; Streu im Umland und Kopfweiden am Bach beim Kräutergarten mit den Dichten von bisher. Stadt mit 6 m Rand und Vorstadt sind Aussparungen. | 21.10.2026 |
| Umsetzung: Köln | `koeln.py` schreibt Format 2 (Startgrundstück = Werkstatt- und Wohnhausplatz, rechtwinklig). Dom, Antoniterkirche und Leerstand L2 standen auf Startgrundstücken und rücken ein paar Meter; die Kapazität passt weiter. | 21.10.2026 |

### Kartenwerkstatt (Level-Editor), Schritt 1: Grundgerüst (PR #168)

Fahrplan (vom Nutzer am 21.10.2026 bestätigt, je Schritt ein PR; Stand wird hier nachgeführt):

| Schritt | Inhalt | Stand |
|---|---|---|
| 1 Grundgerüst | Knopf im Hauptmenü, Karte laden und zeigen, Gebäude aus dem Katalog setzen, verschieben, drehen, löschen, Live-Prüfung, Speichern, Rückgängig | fertig (PR #168) |
| 2 Wege und Flächen | Wege mit Kurven zeichnen, Kreuzungen rasten ein, Gassennetz live; Bauzonen und Waldflächen als Vielecke | fertig (PR #170) |
| 3 Rest der Karte | Mauer und Tore, Startbelegung, Knopf „Karte ausprobieren“ | fertig (PR #172) |
| 4 Umzug | Greybox und Köln übernehmen, der Editor wird die Quelle; Python-Werkzeug fällt weg, dafür ein C#-Kartenskript (`tools/Kartenbau`: bauen, prüfen, Vorschau) | fertig (PR #174) |
| 5a Kartengröße und Rand | Größe einstellbar (z. B. 400 × 300 m bis 2 × 2 km), Kamera bleibt innerhalb, dahinter eine Randlandschaft, die in Dunst verblasst – nie Himmel | in Arbeit (PR #178) |
| 5b Gelände | Höhenraster (~2 m) mit Pinseln: Heben, Senken, Glätten, Ebnen, Rauschen; Fundamente unter Gebäuden automatisch geebnet, steile Hänge für Bauzonen gesperrt, Wege folgen dem Gelände | offen |
| 5c Wasser | Flüsse und Bäche als Linie mit Breite (Zeichenrichtung = Fließrichtung, Strömung), Seen als Fläche mit Wasserstand (still), Ufer automatisch ausgehoben; Wasser ist Hindernis, Brücken, Furten, Fähren, Häfen am Ufer | offen |
| 5d Wegbelag | Trampelpfad bis Pflaster, wirkt auf Karren | offen |
| 6 Freigabe | Kartenwerkstatt für Spieler | offen |

| Thema | Entscheidung | Datum |
|---|---|---|
| Ort | Im Spiel als eigene Szene „Kartenwerkstatt“, erreichbar über einen Knopf im Hauptmenü (Grundsatz vom 19.10.2026 bestätigt): gleiche Darstellung, gleiche Regeln, gleiches Kartenformat, später für Spieler freigebbar. | 21.10.2026 |
| Bedienung | Links der Katalog nach Art (Werkstattplätze, Wohnhausplätze, Sonderbauten, Kulissen, Umland, Natur), rechts die Eigenschaften des gewählten Stücks (Name, Drehung, Betrieb) und die Fehlerliste, oben Neu, Öffnen, Speichern, Prüfen, Rückgängig/Wiederholen. Kamera wie im Spiel, Tafeln im Stil der Spieloberfläche. | 21.10.2026 |
| Setzen | 1-m-Raster, Drehung mit Q/E in 15°-Schritten, mit gehaltener Alt-Taste frei. Umriss grün = passt, rot = Fehler. | 21.10.2026 |
| Prüfung | Live; fehlerhafte Stücke leuchten rot, rechts eine Fehlerliste (Klick springt hin). Speichern geht immer; „Karte ausprobieren“ (Schritt 3) nur ohne Fehler. Die Prüfungen wandern aus `tools/karten/` nach C# in den Kern. | 21.10.2026 |
| Speicherort | Bis Schritt 4 öffnet der Editor Greybox und Köln nur lesend und speichert eigene Karten im Benutzerordner (wie Spielstände); so überschreibt er nie, was das Python-Werkzeug erzeugt. | 21.10.2026 |
| Szene (Umsetzung) | `scenes/Kartenwerkstatt.tscn` mit `Kartenwerkstatt.cs` (Dateien, Tasten, Startoptionen), `Kartenwerkstatt.Tafeln.cs` (Leiste, Katalog, Eigenschaften, Fehlerliste, Rückfragen) und `Stadtansicht.Kartenwerkstatt.cs` (Stücke, Umriss, Auswahl, Ziehen). Startet mit der Greybox (nur lesen); Öffnen und Neu laden die Szene neu. | 21.10.2026 |
| Werkstattplätze (Umsetzung) | Zeigen das Werkstattmodell des Schmieds (Stufe 1, ohne Hausfarbe), nicht den Bauplatz-Zaun: Alle Werkstätten haben die Normfläche 16 × 14 m, und ein Haus zeigt Höhe und Abstände besser. Wohnhausplätze zeigen Fachwerkhäuser (ein Doppelplatz zwei), Einwohnerplätze ein Einwohnerhaus (ab 8 m Breite Bürgerhaus), Kirche und Hafen einen grauen Block. | 21.10.2026 |
| Tasten (Umsetzung) | Q/E drehen das Stück (15°, mit Alt 1°), die Kamera dreht mit Strg+Q/E oder Bild auf/ab. Entf löscht, Esc beendet Setzen bzw. Ziehen bzw. die Auswahl, Rechtsklick ohne Ziehen beendet das Setzen. Strg+Z, Strg+Y (auch Strg+Umschalt+Z), Strg+S. Ein Klick auf Rot setzt trotzdem – der Fehler steht dann in der Liste. | 21.10.2026 |
| Hintergrund (Umsetzung) | Boden, Mauern, Wege und Natur zeichnet die Werkstatt einmal beim Öffnen; ein verschobenes Gebäude lässt Bäume und Wege, wo sie waren. | 21.10.2026 |

### Kartenwerkstatt, Schritt 2: Wege und Flächen (PR #170)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wege zeichnen | Wegart wählen (Landstraße, Gasse, Feldweg, Fußweg), Punkte klicken, das Werkzeug legt eine weiche Kurve hindurch; Doppelklick oder Enter beendet. Endet oder beginnt ein Weg an einem anderen, rastet er dort als Kreuzung ein. Gespeichert werden die Stützpunkte; Spiel und Kern rechnen dieselbe Kurve. | 21.10.2026 |
| Wege bearbeiten | Ein gewählter Weg zeigt seine Stützpunkte: Ziehen verschiebt, Klick auf die Linie fügt einen ein, Entf löscht einen. Rechts Name, Art und Breite (nicht unter der Mindestbreite der Art). Alles mit Rückgängig. | 21.10.2026 |
| Flächen | Bauzonen, Wald- und Streuflächen als Vielecke: Ecken klicken, Doppelklick schließt; Ecken ziehen, einfügen, löschen; Aussparungen ebenso. Naturflächen haben rechts Dichte, Mischung der Arten und Startwert; die Natur wird live neu gestreut, unter Gebäuden und Wegen bleibt sie automatisch frei. | 21.10.2026 |
| Einzelne Pflanzen | Im Katalog unter Natur jede Art einzeln (setzen wie ein Gebäude) und ein Pinsel, der beim Ziehen locker Bäume oder Büsche verteilt (Größe und Dichte als Regler). | 21.10.2026 |
| Wege (Umsetzung) | Katalog „Wege“ mit allen acht Wegarten und ihrer Standardbreite, darüber der Schalter „Glatte Kurve“ (an: `kurve: "glatt"`). Die Vorschau zeigt die Kurve des Kerns (`Wegkurve.Verlauf`) bis zur Maus. Anfang und Ende rasten an Höfen von Werkstattplätzen und an Wegen ein (2 m, weißer Ring), Punkte dazwischen nicht – sonst rastete ein Punkt neben einer Gasse ungewollt auf ihr ein. Eine Gasse, die an einem Hof endet, wird umgedreht (Werkstattnorm: die Gasse beginnt am Hof). Rücktaste nimmt den letzten Punkt weg. | 21.10.2026 |
| Griffe (Umsetzung) | Ein gewählter Weg bzw. eine gewählte Fläche zeigt Griffe (gold, Aussparungen orange, der gewählte weiß), auf dem Bildschirm immer gleich groß; ein Griff wird bis 10 Bildpunkte daneben getroffen. Klick auf Linie oder Rand fügt ein und lässt gleich weiterziehen (ein Schritt), Ziehen im Inneren der gewählten Fläche verschiebt sie ganz. Entf löscht den gewählten Griff (eine Aussparung mit drei Ecken ganz), ohne Griff den Teil. | 21.10.2026 |
| Flächen (Umsetzung) | Bauzonen wie im Baumodus (Farbe der Lage, Raster, Kennung), Naturflächen halbdurchsichtig grün (Wald) bzw. olivgrün (Streu) mit Rand, Aussparungen nur mit braunem Rand (gewählt auch gefüllt – die Greybox spart die ganze Stadt aus). Rot bei einem Fehler. Rechts: Mischung als Regler je Art (Anteil in %), „Art hinzufügen“, „Neu würfeln“. | 21.10.2026 |
| Natur (Umsetzung) | Spiel und Werkstatt zeichnen die Lagen aus `NaturVerteilung` des Kerns; das Spiel ist umgestellt (Greybox Lage für Lage gleich, 811 Pflanzen; Bildvergleich bei Zoom 95: 0,09 % abweichende Bildpunkte durch Passanten), die Detailstufe dünnt weiter das fertige MultiMesh aus. Die Werkstatt streut nach jeder Änderung nur die betroffenen Flächen neu und zeigt immer alle Pflanzen. | 21.10.2026 |
| Pflanzen, Pinsel (Umsetzung) | Jede Natur-Art einzeln im Katalog (Klick setzt, Q/E dreht); Pinsel mit Radius 6 m, 30 Pflanzen je 1.000 m², Eiche, Buche, Hasel, Holunder vorgewählt (rechts änderbar, Arten nach Baukasten gruppiert); Radierer mit demselben Radius löscht nur einzeln gesetzte Pflanzen. | 21.10.2026 |
| Gassennetz (Umsetzung) | Schalter „Gassennetz“ in der Leiste; das Netz der Werkstatt entsteht nach jeder Änderung an Wegen oder Gebäuden neu (Wege, Markt, Tore, Höfe, Karrenplätze; die Stücke zu Höfen und Karrenplätzen gezeichnet wie im Spiel) und wird über allem gezeigt. Die Leiste unten meldet, ob alles verbunden ist. | 21.10.2026 |
| Hintergrund (Umsetzung) | Ersetzt „Hintergrund“ aus Schritt 1: Nur Boden und Mauern zeichnet die Werkstatt einmal beim Öffnen; Wege, Flächen, Natur und Pflanzen folgen jeder Änderung. Glatte Wege zeichnet auch das Spiel als ein Band (`WegBand`), gerade wie bisher als Quader. | 21.10.2026 |

### Kartenwerkstatt, Schritt 3: Mauer, Tore, Startbelegung, Ausprobieren (PR #172)

| Thema | Entscheidung | Datum |
|---|---|---|
| Mauer und Tore | Ecken der Mauer klicken (gerade Stücke), Doppelklick schließt sie oder lässt sie offen (wie Köln am Rhein). Türme setzt das Spiel selbst an die Ecken und etwa alle 35 m, wie heute. Ein Klick auf die Mauer setzt dort ein Tor; ein Weg durch das Tor rastet ein. Ecken und Tore lassen sich ziehen und löschen. | 21.10.2026 |
| Startbelegung | Tafel „Start“: Liste der Häuser (bis 8, für bis zu 8 Spieler) mit Name, Volk, Farbe, Wappen; „Wohnhaus wählen“ und „Betrieb hinzufügen“ per Klick auf einen Platz in der Karte, dann Betriebsart wählen; dazu der Startplatz des Spielers je Beruf. Die Karte zeigt die Belegung farbig. | 21.10.2026 |
| Karte ausprobieren | Startet eine neue Partie auf dem Entwurf, wie er gerade ist (nur ohne Fehler); ein Knopf oder Esc im Spielmenü führt zurück in die Kartenwerkstatt, alles ist noch da, auch Rückgängig. Der Beruf für den Test ist wählbar. | 21.10.2026 |
| Mitgenommen | Klickflächen der Gebäude (fangen im Schrägblick keine Klicks auf den Boden dahinter mehr), Höfe beim Einrasten im Kern, Streu-Gruppen einzeln bearbeiten, dichtere neue Wälder (Vorgabe höher, Greybox unverändert). | 21.10.2026 |
| Mauer (Umsetzung) | Katalog-Gruppe „Mauer“ mit den Werkzeugen „Mauer“ und „Tor“. Mauerecken rasten auf 1 m (mit Alt frei); ein Klick auf die erste Ecke schließt die Mauer, Doppelklick oder Enter lässt sie offen. Die Vorschau zeigt bis zur Maus den `Mauerplan` des Kerns (Stücke grau, Türme dunkel, Torburgen blau). „Tor“ setzt mit einem Klick bis 3 m (bzw. 14 Bildpunkte) neben die Linie und bleibt aktiv bis Esc. Eine gewählte Mauer: Griffe an Ecken (gold) und Toren (blau; ein Tor gleitet beim Ziehen an der Mauer entlang), Klick auf die Linie fügt eine Ecke ein, Entf löscht Griff bzw. Mauer; rechts Name, „geschlossen (Ring)“, Länge, Zahl der Türme, Tore mit Name und „schließt nachts“. Spiel und Werkstatt zeichnen beide den `Mauerplan` (`Stadtansicht.Mauer.cs`); Bildvergleich vorher/nachher: Greybox im Spiel 0,49 % abweichende Bildpunkte (nur Passanten), Köln 0,19 %, Köln in der Werkstatt 0,01 %. | 21.10.2026 |
| Einrasten (Umsetzung) | Die Werkstatt fragt nur noch den Kern (`Einrasten` für Wegenden, `TorEinrasten` für Punkte dazwischen); das eigene Hof-Fangen und das Zurücksetzen eines Hofs sind weg. Der Ring an der Maus hat die Farbe des Ziels: Weg weiß, Hof orange, Tor blau, Halteplatz grün. Halteplätze (`Fangpunkte`) sind der „Anker_Karren“ der Umlandbetriebe und der „Anker_Tuer“ (Gatter) der Arbeitsstellen – dieselben Punkte, die das Spiel ans Gassennetz hängt; das Gassennetz der Werkstatt hängt die Gatter jetzt auch an. | 21.10.2026 |
| Startbelegung (Umsetzung) | Rechts zwei Reiter, „Eigenschaften“ und „Start“, beide in einem Rollbereich über der Fehlerliste. „Wählen“ und „Betrieb hinzufügen“ schalten das Werkzeug „Platz“ ein: passende Plätze grün umrandet, unter der Maus grün „Klick wählt“ oder rot der Grund (kein Wohnhausplatz, schon belegt von …). Nach einem Werkstattplatz folgt die Betriebsart (Werkstätten der Stufe 1 aus `wirtschaft.json`), ein Umlandbetrieb hat nur seine. In der Karte: Modell in der Hausfarbe, ein Startbetrieb zeigt die Werkstatt seines Berufs, dazu eine 11 m hohe Fahne an der vorderen linken Ecke (Spieler gold mit Schwalbenschwanz) – ein Rahmen wäre mit Auswahl (gold) und Fehler (rot) zu verwechseln. Aufklappen eines Hauses fährt zu seinem Wohnhaus; ein Klick auf einen belegten Platz klappt sein Haus auf. Farbe: die acht `Hausfarben` als Felder und ein freier Farbwähler; Wappen: Teilung, Figur, Grund- und Figurfarbe, „Würfeln“. | 21.10.2026 |
| Ausprobieren (Umsetzung) | Knopf „Karte ausprobieren“ in der Leiste, mit Fehlern gesperrt (der Tooltip nennt den Grund); die Rückfrage wählt den Beruf (Berufe mit Werkstatt der Stufe 1, dahinter der Startplatz). Die Werkstatt stellt die Partie mit `Ausprobieren` auf und legt sie mit Entwurf, Datei, Kamera, Auswahl und Reiter in einen statischen Briefkasten; die Hauptszene holt sie ab (`Hauptszene.Probepartie.cs`): Hinweis „Probepartie … – Esc → Zurück zur Kartenwerkstatt“ oben, kein Auto-Stand, im Spielmenü der Knopf „Zurück zur Kartenwerkstatt“. Auch über „Zum Hauptmenü“ und den Knopf „Kartenwerkstatt“ kommt der Entwurf zurück. | 21.10.2026 |
| Klickflächen (Umsetzung) | Die Klickfläche eines Stücks sind die konvexen Hüllen der sichtbaren Netze seines Modells (statt eines Kastens bis 12 m über der ganzen Grundfläche). Reihenfolge eines Klicks: Griffe, Linie/Rand des gewählten Teils, Modell über 0,6 m getroffen, Tor, Pflanze, Weg, flaches Modell (Feld, Weide), Mauer, Grundfläche eines Gebäudes, Fläche. Klicktest Greybox (Zoom 45): ein Klick knapp hinter die Gießerei Eisenbart wählte vorher die Gießerei, jetzt die Ostgasse dahinter. | 21.10.2026 |
| Streu-Gruppen (Umsetzung) | Bei einer Waldfläche bleibt die „Mischung“ (Regler je Art), eine Streufläche zeigt ihre Gruppen einzeln: Gewicht, Radius, je Teil Anzahl von–bis und Arten mit Gewicht (0 nimmt eine heraus, „+ Art“), „+ Teil“, „+ Gruppe“, „ד – jede Änderung ein Befehl. Der Regler „Dichte“ reicht bis 30 je 1.000 m² (neue Wälder beginnen mit 10). | 21.10.2026 |

### Kartenwerkstatt, Schritt 4: Umzug und Kartenskript (PR #174)

| Thema | Entscheidung | Datum |
|---|---|---|
| Python-Werkzeug | Wird gelöscht (zweite Kopie der Regeln, läuft schon heute auseinander). Ersatz: ein C#-Kartenskript `tools/Kartenbau` mit denselben Befehlen und Prüfungen aus dem Kern wie die Kartenwerkstatt: Karten per Code anlegen (Grundriss nach historischem Vorbild), prüfen und Vorschaubilder erzeugen. So legt Claude den Grundriss von Köln und den rund 7 weiteren Städten per Skript an, der Nutzer verfeinert in der Kartenwerkstatt; beide arbeiten an derselben Kartendatei. Entschieden vom Nutzer. | 22.10.2026 |
| Quelle | Die Kartendateien unter `game/karten/` sind die Quelle; die Kartenwerkstatt speichert in der Entwicklung direkt dorthin (im fertigen Spiel weiter nur in den Benutzerordner). Die Regel „Kartendatei nur aus dem Skript, nie von Hand“ wird zu „nur aus Kartenwerkstatt oder Kartenskript“. | 22.10.2026 |
| Gelände, Wasser, Rand (Grundsatz für Schritt 5) | Gelände als Höhenraster mit Pinseln, Fundamente automatisch, Hangsperre für Bauzonen, Wege folgen dem Gelände; Flüsse als Linie mit Fließrichtung und Strömung, Seen als Fläche mit Wasserstand, Ufer ausgehoben; Wasser als Hindernis mit Brücken, Furten, Fähren und Häfen; Kartengröße einstellbar, Kamera begrenzt, Randlandschaft dahinter. Entschieden vom Nutzer, umgesetzt in den Schritten 5a–5c. | 22.10.2026 |
| Kartenskript (Umsetzung) | `tools/Kartenbau` mit `pruefen` (Regeln, Kapazität, Katalog gegen die Modelle; Rückgabe 1 bei Fehlern), `vorschau [--belegung]`, `bauen <skript> [--ueberschreiben]` (nur ohne Fehler, nie über eine vorhandene Datei ohne den Schalter) und `liste`. Ein Stadtskript ist eine Klasse unter `Staedte/` mit den Befehlen des Kartenentwurfs und schlichten Hilfen (Häuserreihe an einer geraden Straße, Ringgasse innerhalb der Mauer, Platz mit Umriss). Muster: Probestadt (Mauerring, zwei Tore, Markt, Rathaus, Kirche, 4 Werkstatt-, 6 Wohnhaus-, 30 Einwohnerplätze (20 am Ring, 10 in der Vorstadt), 4 Bauzonen der Vorstadt, zwei Wälder, Start mit 2 KI-Häusern) – keine Fehler, keine Hinweise, `--karte=probestadt`. Aufruf in `docs/LEVELDESIGN.md`, „Kartenskript“. | 22.10.2026 |
| Prüfungen (Umsetzung) | Kapazität und Katalog gegen die Modelle in C#, mit denselben Ergebnissen wie das Python-Werkzeug (Köln: dieselbe Belegung Feld für Feld; Katalog: 0 Fehler, 11 Hinweise). Kapazität: Köln Fehler, neue Karten Hinweis, Greybox keine (Probeplan). Neu: Hinweis bei einer Karte ohne Einwohnerplätze. | 22.10.2026 |
| Vorschaubilder (Umsetzung) | Neu gezeichnet aus dem Kern: Wege mit ihrer Kurve, Mauern aus dem Mauerplan (Stücke, Ecktürme, Türme, Torburgen mit Namen), Plätze der Startbelegung in der Hausfarbe mit Legende (freie Wohnhausplätze graurot); `-belegung.svg` mit Kapazität bzw. Probeplan wie bisher. | 22.10.2026 |
| Weg im Tor (Umsetzung) | Wandert ein Tor (Mauerecke ziehen, einfügen, löschen, Mauer öffnen/schließen oder verschieben, Tor verschieben), folgt jeder Stützpunkt eines Weges in der Tormitte im selben Rückgängig-Schritt. | 22.10.2026 |
| Speicherort (Umsetzung) | `Kartenwerkstatt.Entwicklung` = `OS.HasFeature("editor")` (jedes Editor-Build, auch `godot --path game` ohne geöffneten Editor; nie eine Exportvorlage) und `game/karten/` liegt als Ordner auf der Platte. Dann öffnet die Werkstatt die Karten des Spiels zum Schreiben: Kopf „Spielkarte – wird direkt gespeichert · game/karten/…“, „Speichern“ fragt je Karte einmal je Sitzung („Spielkarte überschreiben?“), „Speichern unter“ wählt zwischen „Karten des Spiels (game/karten)“ und „Eigene Karten (Benutzerordner)“, „Öffnen“ listet alle Karten in `game/karten/` (auch die Probestadt). Klicktest: Greybox, Köln und Probestadt unverändert gespeichert sind Byte für Byte gleich. Startoption `--werkstatt-speichern=1` (wie Strg+S) bzw. `=direkt` (ohne Rückfrage). | 22.10.2026 |
| Ganze Mauer (Umsetzung) | Umschalt + Ziehen an der Linie (gewählt oder nicht) verschiebt die ganze Mauer auf ganze Meter (Alt frei), ein Schritt `MauerVerschieben`; Mauerknoten und Griffe rücken beim Ziehen mit. Kein Griff in der Mitte: Er läge bei einem Ring mitten in der Stadt. | 22.10.2026 |
| Weg im Tor in der Werkstatt (Umsetzung) | Die Meldungen für die Wege nach Mauerbefehlen zeichnen die Wege wie jede andere Änderung neu. Schon beim Ziehen (Ecke, Tor, ganze Mauer) zeigt die Vorschau die Wege durch wandernde Tore an ihrem neuen Ort, der alte Weg ist so lange versteckt. Klicktest Greybox: Nordostecke 8 m nach Osten → Osttor 4 m, Ostgasse und Landstraße folgen; ganze Mauer 4 m → Ostgasse 54 m; ein Rückgängig nimmt alles zurück. | 22.10.2026 |
| Spielerfahne (Umsetzung) | Weiß mit goldenem Rand (0,4 m, neuer Rand im Fahnen-Shader, ohne Rand wie bisher) und Schwalbenschwanz – keine Hausfarbe ist weiß, Haus Kieselbach (gelb) bleibt klar zu unterscheiden. Das Modell des Spielers trägt seine Farbe aus der Startbelegung, ohne sie das Blau des Spiels (statt gold). Erklärt oben in der Tafel „Start“. | 22.10.2026 |
| Kapazität in der Werkstatt (Umsetzung) | Kapazitäts-Befunde (ohne Ort) mit eigenem Symbol (3 × 3 Felder in der Farbe der Schwere) und dem Bericht als Tooltip; passt alles, steht ein grüner Eintrag mit Bericht in der Liste (Köln, Probestadt). Klick zeigt die Belegung der Prüfung in der Karte (Felder je Gebäudeart, Farben, die keine Lage hat), bis zur nächsten Änderung oder Esc. „Prüfen“ rechnet die Kapazität frisch und nennt die Zeit (Köln ~150 ms beim ersten Mal). Der Tooltip über „Prüfung“ nennt die Regeln der Karte. Hinweis „einwohnerplaetze“: Tooltip erklärt, wo Einwohnerhäuser stehen, Klick wählt das Werkzeug „Einwohnerplatz“ (der Katalog rollt hin). | 22.10.2026 |
| Spielen mit `--karte` (Umsetzung) | `--karte=<kennung>` spielt mit allen KI-Häusern der Karte (wie im Hauptmenü vorgewählt; ohne Startbelegung die drei des Ersatzes) statt fest drei – die Probestadt mit zwei Häusern startete vorher nicht („Es spielen ein bis zwei KI-Häuser mit“). Das Hauptmenü nimmt für „Rivalen“, Namen und Wappen dieselbe Karte (`Stadtkarten.Startkarte`: `--karte`, sonst Greybox), die Hauptszene begrenzt eine Wahl aus dem Menü auf die Zahl der Häuser der Karte. Klicktest: Probestadt direkt und über „Neue Partie“ (Rivalen „Ahlbeck und Brendel“), Greybox und Köln weiter mit drei. | 22.10.2026 |

### Kartenwerkstatt, Schritt 5a: Kartengröße, Kamera, Rand (PR #178)

| Thema | Entscheidung | Datum |
|---|---|---|
| Kartengröße | Breite und Tiefe getrennt in 50-m-Schritten von 400 × 300 m bis 2 × 2 km; eine neue Karte beginnt mit 600 × 450 m. Greybox und Köln bekommen eine passende Größe um ihren heutigen Inhalt. Entschieden vom Nutzer. | 22.10.2026 |
| Kamera | Der Blickpunkt bleibt innerhalb der Karte, weich abgebremst statt hart gestoppt; am Rand sieht man noch ein Stück Randlandschaft. Herauszoomen ist begrenzt, damit nie Himmel oder Leere zu sehen ist. Entschieden vom Nutzer. | 22.10.2026 |
| Randlandschaft | Um jede Karte liegt ein Randstreifen (Breite einstellbar, Vorgabe 300 m), nicht bespielbar, aber in der Kartenwerkstatt mit den vorhandenen Werkzeugen gestaltbar (Wald, Streu, Natur-Pinsel, Wege nach draußen) und neu mit Feldern. Eine neue Karte bekommt einen Vorschlag (Felder und Waldstücke), den man übermalen kann. Außen verblasst alles im Dunst. Hügel kommen mit dem Geländepinsel in 5b, auch im Rand. Entschieden vom Nutzer. | 22.10.2026 |
| Kartenwerkstatt | Größe im Reiter „Karte“ einstellbar (mit Rückgängig, was außerhalb läge, meldet die Prüfung als Fehler); Kartenrand als Linie (nur in der Werkstatt); Kamera-Startpunkt (Punkt, Zoom, Drehung) per Knopf aus dem aktuellen Blick übernehmen. Entschieden vom Nutzer. | 22.10.2026 |
