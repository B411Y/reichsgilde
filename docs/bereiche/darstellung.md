# Bereich Darstellung – Offen und Entscheidungen

**Nur der Darstellungs-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (3) Raumansicht für den Dom und Feinschliff der Werkstatträume je Beruf (zweiter Teil von PR #52)
- (3) (von Kern) Karren am Stadttor zeigen: `Karren.WartetAmTor(zeit)` – Hinweg draußen vor dem Tor, Rückweg drinnen (ab PR #67)
- (3) Einwohner in den Innenräumen von Schänken und Kirchen der Häuser zeigen (im Dom erledigt, PR #93)
- (3) (von Leveldesign) Gebäudemaße auf glatte Meter: Einwohnerhäuser 6 × 6, 7 × 7, 8 × 8 m; Werkstatt belegt 16 × 14 m und zeigt jede Stufe als sichtbaren Anbau innerhalb dieser Fläche (`docs/LEVELDESIGN.md`, Gebäudegrößen)
- Modelle ins Spiel einbauen (nach PR #103): `SpielhausModell` und die Einwohnerhäuser laden die GLB aus `game/assets/modelle/` statt Grundkörper zu bauen; Material „Hausfarbe“ je Haus einfärben, Knoten „Leerstand“/„Geruest“ (und „Wappen“/„Fahne“) nach Zustand ein- und ausblenden
- Weitere Modelle aus Blender-Skripten: Stadtpalais, Stadthof; Werkstätten mit Stufe1–3 der übrigen Berufe (Tischler, Steinmetz, Alchimist …), je Beruf ein Schritt; Einwohnerhäuser (Ratsherrenhaus, Bürgerhaus, einfaches Haus)
- Werkstatt des Schmieds einbauen: `schmied.glb` statt `WerkstattAussenBauen` und `AusbauBauen` beim Beruf Schmied; genau den Knoten `Stufe<n>` der Betriebsstufe zeigen, das Gerüst über „Geruest“, die Glut über das Material „Glut“ (Emission an, solange gearbeitet wird; ersetzt `huelle.Esse`), die verkaufte Werkstatt über „Leerstand“ ohne „Wappen“ und „Fahne“. Klickfläche, Innenraum und Schild bleiben wie bisher
- Wohnhaus-Modelle auf Wohnhausplätzen der Karte (8 m tief, ohne Vorplatz): Die GLB sind für 8 × 10 m mit 2 m Vorplatz gebaut, die Hauskörper sind höchstens 7,4 m tief. Beim Einbau das Modell dort um 1 m zur Gasse versetzen (die Freitreppe des Steinhauses ragt dann in die Gasse); breitere Plätze lassen links und rechts Luft

## Entscheidungen

### Passanten (PR #71)

| Thema | Entscheidung | Datum |
|---|---|---|
| Bewegung | Die Darstellung liest nur `Wo` und `Weg` des Kerns. Unterwegs läuft die Figur den kürzesten Pfad im Gassennetz; wie weit, sagt der Anteil der Wegzeit mit angebrochener Minute. Lange Wege werden schneller gegangen – die Zeit legt der Kern fest. | 16.10.2026 |
| Sichtbarkeit | Sichtbar ist nur, wer unterwegs ist oder auf Markt, Gasse, am Tor oder im Umland steht. Wer zu Hause, in Schänke, Kirche oder Rathaus ist, ist drinnen. | 16.10.2026 |
| Standplätze | Fest aus Nummer und Ankunftszeit (Hash), kein Würfel: gleich bei jedem Bild und nach dem Laden. Markt im Ring um den Brunnen, Gasse auf einer Gasse in der Stadt, Tor vor dem Osttor, Umland auf der Stelle. | 16.10.2026 |
| Rechenzeit | Einzelne Knoten mit gemeinsamen Formen und Stoffen genügen (0,2 ms je Bild für 123 Figuren, Zeichenaufrufe kaum mehr); MultiMesh erst, wenn es nötig wird. | 16.10.2026 |
| Umlandstellen | Weide, Feld und Wald haben je einen eigenen Fußweg von der Weststraße, damit keiner durch eine andere Stelle führt. | 16.10.2026 |

### Gehtempo der Passanten (PR #74)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gehtempo | **Feste Spanne je Figur** statt „Weglänge ÷ Wegzeit“: schlendern 1,5, normal 2,5, eilig 3,5 m/s (echte Sekunde bei Tempo 1, ±10 %, fest nach der Nummer). Boten eilen, Bettler und Verwitwete schlendern. | 16.10.2026 |
| Ankunft | Die Figur geht so spät los, dass sie zur Ankunftszeit des Kerns da ist. Reicht die Zeit nicht, beeilt sie sich **bis zum Doppelten**; reicht auch das nicht, kommt sie später an als gezählt. Dafür bittet die Darstellung den Spiel-KI-Chat um längere Wegzeiten (Zeile in `spiel-ki.md`). | 16.10.2026 |

### Tracht und Infotafel der Passanten (PR #78, #81)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aussehen | **Volk über die Gestalt, Rolle über die Tracht.** Ratsherr roter Mantel und Barett, Mönch braune Kutte mit Kapuze, Nonne schwarz mit weißem Schleier, Bettler graue Lumpen und gebeugt, Hirte Strohhut und Stab, Feldarbeiter Strohhut, Holzsammler Reisigbündel, Krämer Kiepe, Bote blau mit Tasche, Knecht und Magd mit Schürze, Verwitwete schwarz, Bürgerkinder kräftige Farben mit Barett. Frauen mit Rock und Haube, Zwerge mit Bart. | 16.10.2026 |
| Klick | Klick auf eine Figur öffnet die Auswahltafel (Name, Rolle, Volk, Alter, Tun und Ort bzw. Ziel mit Ankunft, Wohnhaus, Stimmung). Maus darüber hebt sie hell hervor. Treffer über die Bildschirmlage statt Klickflächen (die müssten jedes Bild mitwandern). | 16.10.2026 |

### Wohnhäuser der Spielhäuser (PR #87)

| Thema | Entscheidung | Datum |
|---|---|---|
| Platz | Jedes Spielhaus (Spieler und KI) bekommt einen **Doppelplatz am Ring** – zwei benachbarte Ringplätze, die nächsten am Markt (Mitte der Nord- und Südseite, dann Ost und West). Diese Plätze fehlen den Einwohnern; deren Häuser rücken weiter nach außen, mit 4 Spielhäusern stehen jetzt 8 in der Vorstadt. | 16.10.2026 |
| Stufen | **Jede Stufe ein eigenes Haus**: Fachwerkhaus 6 × 6, Bürgerhaus 7 × 7 mit Giebel, Steinhaus 8 × 8 mit drei Geschossen, Stadtpalais 15 × 8 mit Erker und zwei Ecktürmchen, Hof mit Haupthaus, zwei Flügeln und Hofmauer mit Tor. Wappenschild über der Tür und Fahne in der Hausfarbe. Alle Stufen am selben Doppelplatz – bis der Kern „Stadtpalais und Hof als Neubau“ umsetzt (Leveldesign). | 16.10.2026 |
| Zustand | **Ausbau:** Gerüst vor der Gassenseite. **Nach Brand:** rußiges Dach und Gerüst bis zur Instandsetzung. **Leerstand:** bleibt stehen, Fahne und Wappen weg, Tür und Fenster vernagelt. Aufkaufen und Abreißen wünscht der Nutzer – Sache des Kerns (Zeile in `kern.md`). | 16.10.2026 |
| Tafel | Klick zeigt Stufe, Ansehen, laufenden Ausbau mit Fertigzeit, Brandschaden, Hauswächter bzw. Leerstand. Wohnstube und Kontor ziehen in einem eigenen Schritt ins Wohnhaus. | 16.10.2026 |

### Wohnstube und Kontor im Wohnhaus (PR #90)

| Thema | Entscheidung | Datum |
|---|---|---|
| Räume | Doppelklick auf das **eigene** Wohnhaus betritt es (Raumansicht): Wohnstube, dann Kontor. Die Werkstatt hat nur noch Werkraum und Lager. Ohne Wohnhäuser (Abschnitt `wohnen` fehlt) bleibt es wie bisher. Rivalenhäuser sind nicht betretbar. | 16.10.2026 |
| Schmuck | Die Einrichtung wächst mit der Stufe: Bürgerhaus Teppiche und Tellerbord, Steinhaus Wandbehang und Landkarte, Stadtpalais Kronleuchter, zweite Truhe und Standleuchter, Stadthof Wappenschild. Raumgröße bleibt gleich. | 16.10.2026 |

### Betende im Dom (PR #93)

| Thema | Entscheidung | Datum |
|---|---|---|
| Betende | Einwohner, die als Passanten im Dom angekommen sind, sitzen dort einzeln auf den Bänken: vorderster freier Platz, in der Simultankirche Katholiken links, Evangelische rechts. Gestalt, Gewand und Haut wie draußen; Strohhut und Last bleiben draußen. Jeder behält seinen Platz, bis er geht. Die Messe-Gläubigen (eine Figur je fünf) setzen sich auf die übrigen Plätze. Mehr Betende als 48 Plätze werden nicht gezeigt. Die Infotafel nennt die Zahl. | 16.10.2026 |

### Verkaufte Werkstatt (PR #96)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aussehen | **Das Haus bleibt stehen** in Stufe (Gaube, Dachreiter) und Werkstil, aber neutral: keine Wappenfahne, keine Besitzerfarbe, die Tür mit zwei gekreuzten Brettern verriegelt, davor das **ockerfarbene Schild** wie bei freien Plätzen. Maus darüber: Name und „zu verkaufen“. Kein Innenraum. | 16.10.2026 |
| Pacht | Eine gekündigte Grube oder Umlandpacht zeigt wieder den leeren Platz mit grünem Schild „zu verpachten“ wie vor dem ersten Pachten. | 16.10.2026 |
| Ablauf | Mitten im Spiel verschwinden Gebäude, Innenraum und Karren des Betriebs in einem Zug; wer ihn gerade betreten hat, steht wieder in der Stadt. Beim Laden steht das leere Haus gleich da. Beim Wiederkauf weicht es dem neuen Betrieb (mit Fahne des Käufers). | 16.10.2026 |
| Ereignis | Die Stadtansicht abonniert `BetriebAbgegeben` selbst – keine Änderung an der Hauptszene. War der Betrieb ausgewählt, springt die Auswahl der Oberfläche (PR #97) auf `LiegenschaftKennung(name)` des leeren Hauses und zeigt dessen Kaufangebot. | 16.10.2026 |

### 3D-Modelle aus Blender-Skripten (PR #104)

| Thema | Entscheidung | Datum |
|---|---|---|
| Herkunft | Jedes Modell entsteht aus einem Python-Skript `tools/modelle/<name>.py` (gemeinsame Hilfen in `bausteine.py`) und wird als `game/assets/modelle/<name>.glb` eingecheckt. Nichts Heruntergeladenes, keine Bildtexturen: Geometrie aus Vielecken, Quadern, Stäben und Prismen, schlichte Materialien mit fester Farbe. Flächen, die mehr als 50° nach unten zeigen, entfallen – die 40°-Kamera sieht sie nie. Richtwert unter 3.000 Dreiecke je Haus (Zustände extra). | 17.10.2026 |
| Koordinaten | Meter, Y oben, Vorderseite (Eingang) nach +Z zur Straße. Ursprung = Mitte der belegten Fläche nach LEVELDESIGN „Gebäudegrößen“, inklusive 2 m Vorplatz, auf dem Boden. In Blender wird mit Z oben und Vorderseite -Y gebaut; der glTF-Export dreht das um. | 17.10.2026 |
| Hausfarbe | Tür, Fensterläden, Wappen und Fahne tragen das Material „Hausfarbe“ (Vorgabe Spielerblau). Das Spiel tauscht dieses Material je Haus (Spieler blau, Eisenbart rot, Eichwald grün, Kieselbach ocker). | 17.10.2026 |
| Zustände | Eigene Objekte mit festen Namen: „Leerstand“ (Bretter vor Tür und Fenstern), „Geruest“ (Baustelle), bei Werkstätten „Stufe1“–„Stufe3“. glTF kennt keine Sichtbarkeit, darum kommen sie sichtbar aus der Datei und tragen in den glTF-Extras `sichtbar: false`; das Spiel blendet sie nach dem Laden per Namen aus. „Wappen“ und „Fahne“ sind ebenfalls eigene Objekte, damit der Leerstand sie ausblenden kann. | 17.10.2026 |
| Vorschau | Je Modell Bilder in `docs/modelle/<name>.png` (dazu `-leerstand`, `-geruest` und `-rundum` mit allen vier Blickrichtungen), gerendert mit Cycles auf der CPU: orthografische Kamera, 40° Neigung, Blick diagonal wie im Spiel; ein heller Rahmen zeigt die belegte Fläche. `wohnhaeuser.png` zeigt die Stufen im selben Maßstab nebeneinander. | 17.10.2026 |
| Werkzeug | Blender als Programm (`blender -b --python …`) oder als Python-Modul `bpy` (pip, Blender 5.2) – beides läuft dasselbe Skript. Auf dem Cloud-Rechner ist nur das Modul erreichbar. `alle.py` erzeugt alles neu, `pruefen.py` prüft eine GLB ohne Blender (Fläche, Boden, Hausfarbe, Zustandsknoten). | 17.10.2026 |
| KI-Modell | Der Nutzer hat Fable und Opus dasselbe Fachwerkhaus bauen lassen und sich für Opus entschieden. Die Modelle dieses Bereichs baut Opus. | 17.10.2026 |
| Wohnhaus-Stufen | Jede Stufe zeigt ihren Stand. **Fachwerkhaus** (Gemeiner): eingeschossig, 6 × 5,6 m, grobes Fachwerk mit krummen Ständern und abgeplatztem Lehmputz, Luken ohne Glas, Krüppelwalmdach aus Stroh mit Moos, Bretter im Giebel, Holzstapel und Schuppen. **Bürgerhaus** (Bürger): zwei Geschosse, Obergeschoss kragt vor, Andreaskreuze, Ziegeldach mit Schleppgaube, Bank und Regentonne. **Steinhaus** (Patrizier): drei Geschosse aus Haustein, 7 × 7,4 m, Eckquader und Gesimse aus Sandstein, Treppengiebel zur Gasse, Schieferdach, Rundbogenportal mit Freitreppe, Fensterkreuze in der Hausfarbe. Alle drei auf derselben Fläche 8 × 10 m, damit der Ausbau am Platz geht. | 17.10.2026 |
### Bauwesen in der Stadt (PR #103)

| Thema | Entscheidung | Datum |
|---|---|---|
| Quelle | Die Darstellung liest jedes Bild `w.Bauen.Grundstuecke` und baut je Grundstück ein Modell, nur bei geändertem Stand. Die Ereignisse des Kerns werden nicht einzeln abonniert: Der Stand ist die Quelle, auch nach dem Laden. Die Karte liest `Stadtkarten` einmal aus `res://data/karten/greybox.json`; fehlt sie, meldet `GD.PushError` (der Rauchtest schlägt an) und das Spiel läuft ohne Bauwesen weiter. | 17.10.2026 |
| Wohnhäuser | Die Startplätze der Karte (17 × 8 m am Ring) bilden die alten Doppelplätze genau ab (nachgemessen); die Stadt sieht beim Start aus wie vorher. `SpielhausModell` baut auf jeder Fläche, Stadtpalais und Hof passen sich schmaleren Neubauflächen an; Neubauten haben 2 m Vorplatz vor der Hausfront. Leerstand trägt „bankrott“ bzw. „erloschen“ im Namen und in der Tafel. | 17.10.2026 |
| Baustelle | Fundament, vier Mauern, die mit der Bauzeit in vier Stufen hochwachsen, zuletzt Dachstuhl; Gerüst vor der Front, Holzstapel, Steinhaufen. Sanierung: das Haus selbst mit Gerüst und Holzstapel. Abriss: niedrige, unterschiedlich weit abgetragene Mauern, Schutt, Gerüst. Maße nach Gebäude (Fachwerk 6 × 6 … Werkstatt 10 × 8 innerhalb der 16 × 14 m). | 17.10.2026 |
| Bauplatz | Helle Wiese mit aufgewühlter Erde, vier Pflöcke mit roten Spitzen und eine Schnur ringsum, in der Größe des Grundstücks. | 17.10.2026 |
| Wohnhausplatz | Ein Wohnhaus auf einem Wohnhausplatz der Karte (8 m tief, Entscheidung des Kerns: „eingepasst“) hat keinen Vorplatz vor der Front (`VorplatzVon`: nur Flächen über 9 m Tiefe haben 2 m); Umriss, Baustelle und Haus richten sich danach. | 17.10.2026 |
| Neue Werkstatt | Das Modell nach Beruf (`Werkstil`) sitzt mit der Front 2 m hinter der Vorderkante der Fläche; bei Drehung wandert das fertige Gebäude samt Wänden in einen um die Mitte gedrehten Knoten (die Räume im Innern bleiben ungedreht, die Raumkamera blickt fest). Der Weg zum Markt läuft über das Gassennetz, der Hof hängt am Netz (Gäste von Schänke und Kirche finden hin). Verkauft, zeigt sie das leere Haus mit Schild in der Drehung. | 17.10.2026 |
| Bäume | Zufallsbäume lassen die Bauzonen der Karte frei (3 m Rand); die übrigen Bäume bleiben, wo sie waren (die Zufallsfolge läuft unverändert weiter). | 17.10.2026 |
| Baumodus | Bauzonen nur im Baumodus: Fläche in der Farbe der Lage (Kern orange, Mauerring blau, Vorstadt türkis), 2-m-Raster, Name der Zone. Der Umriss (Fläche, hellerer Vorplatz-Streifen, Pfeil zur Straße, durchsichtiger Körper in Gebäudehöhe, Text mit dem Grund) folgt der Maus über die Bodenebene und rastet mit `Einrasten` ein. | 17.10.2026 |


### Werkstatt des Schmieds als 3D-Modell (PR #106)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ein Modell je Beruf | `schmied.glb` enthält alle drei Stufen (Gießerei, Schmiede, Goldschmiede) auf der Fläche 16 × 14 m mit 2 m Vorplatz, Ursprung in ihrer Mitte. Weitere Berufe folgen Schritt für Schritt, je Beruf ein Skript `tools/modelle/<beruf>.py`. | 17.10.2026 |
| Stufen | „Haus“ ist der Bau, der in jeder Stufe gleich bleibt (die Schmiedehalle). „Stufe1“–„Stufe3“ sind **ausschließlich**: Genau einer ist sichtbar. So kann eine höhere Stufe ersetzen, was vorher dastand (der offene Schuppen der Gießerei weicht dem Anbau), und das Spiel braucht nur „zeige Stufe n, verstecke die anderen“. Teile, die in Stufe 2 und 3 stehen, stecken in beiden Knoten. | 17.10.2026 |
| Wachstum | Gießerei: Halle mit offenem Schuppen, Esse, Amboss, Kohle, Formkästen, Bretterzaun. Schmiede: Steinanbau mit zweiter Esse und Tor, Gaube, Notstall zum Hufbeschlag. Goldschmiede: Anbau mit Fachwerk-Obergeschoss und Laden (Klappläden in der Hausfarbe, goldener Ring als Schild), Hinterhaus aus Stein, Hofmauer mit Tor, Dachreiter mit goldener Spitze wie bisher im Spiel. | 17.10.2026 |
| Glut | Neues Material „Glut“ (Esse, Feuerschein hinter den vergitterten Fenstern) mit Emission. Das Spiel schaltet sie wie bisher die Esse: hell bei der Arbeit, aus, wenn nicht gearbeitet wird oder die Werkstatt verkauft ist. | 17.10.2026 |
| Verkauft und Ausbau | „Leerstand“ vernagelt Tor und Fenster der Halle (passt zur verkauften Werkstatt, PR #96; das ockerfarbene Schild bleibt Sache des Spiels). „Geruest“ steht vor der Halle. | 17.10.2026 |
| Dreiecke | Sichtbar je Stufe 2.602, 3.536 und 4.738. Eine Werkstatt ist ein ganzer Hof mit mehreren Bauten, darum über dem Richtwert der Wohnhäuser; bei rund 20 Werkstätten in der Stadt sind das unter 100.000 Dreiecke. | 17.10.2026 |
