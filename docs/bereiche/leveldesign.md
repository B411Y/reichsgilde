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
- Umlandmodelle sind größer als ihre Fläche im Katalog (z. B. Grube 22 × 18 m auf 16 × 14, Wachturm 12 × 12 auf 4 × 4; `tools/karten/katalog_pruefen.py` warnt). Entscheidung des Kerns: Die Fläche bleibt. Folge: Natur und Wege müssen in der Darstellung um das Modell herum frei bleiben, nicht nur um die Fläche; Bauzonen halten 2 m zur Fläche, nicht zum Modell
- Greybox: Vier Wege im Umland laufen über die Fläche eines Umlandbetriebs (Klosterhof-Weg über den Kräutergarten, Bauernhof-Abzweig über den Wachturm, Kräutergarten-Weg und Klosterhof-Abzweig über die Grube am Silberberg) – so zeichnet die Stadtansicht sie seit jeher; das Werkzeug meldet es als Hinweis. Vorschlag: mit dem Level-Editor die Wege dort neu führen
- Felsberg im Katalog 52 × 38 m, das Modell `berg_grube` nur 45 × 32,5 m; mit dem Rand überdecken die Felsberge der Gruben Eisenbart und Silberberg Bauernhof und Duftgarten (Hinweis im Werkzeug). Vorschlag an Kern/Modell: Katalogfläche 46 × 34 m
- Greybox mit weniger als 8 Häusern: Auf den Wohnhausplätzen 5 bis 8 standen bisher Einwohnerhäuser; Format 2 hat dort keine Einwohnerplätze. Ob unbenutzte Wohnhausplätze Einwohner aufnehmen, entscheiden Darstellung und Kern
- Kartenwerkstatt: Natur und Wege werden nur beim Öffnen gezeichnet – wo ein Gebäude hingezogen wird, bleiben die Bäume stehen (und wo es wegging, bleibt eine Lücke). Mit Schritt 2 (Wege und Flächen) neu zeichnen, wenn sich etwas ändert
- Kartenwerkstatt: einzelne Pflanzen (`natur.pflanzen`) und Waldflächen fehlen im Katalog links (der Kern setzt nur Bausteine aus `gebaeude`) – kommt mit Schritt 2
- Kartenwerkstatt: Das Schließen des Fensters (X) fragt noch nicht nach ungespeicherten Änderungen; nur „Zurück zum Hauptmenü“, Neu und Öffnen tun es
- Kartenwerkstatt: In der Greybox ist jeder neue Werkstattplatz rot („Keine Gasse beginnt am Hof“, Werkstattnorm), bis Schritt 2 Wege zeichnen kann; ebenso ein Gebäude in einer Bauzone (2 m Abstand) – beides ist Absicht der Regeln, aber beim Ausprobieren überraschend

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
| 2 Wege und Flächen | Wege mit Kurven zeichnen, Kreuzungen rasten ein, Gassennetz live; Bauzonen und Waldflächen als Vielecke | in Arbeit (PR #170) |
| 3 Rest der Karte | Mauer und Tore, Startbelegung, Knopf „Karte ausprobieren“ | offen |
| 4 Umzug | Greybox und Köln übernehmen, der Editor wird die Quelle, Python-Werkzeug fällt weg | offen |
| 5 Später | Gelände mit Höhen, Wasser, Wegbelag; Freigabe für Spieler | offen |

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

