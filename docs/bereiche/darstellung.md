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
- Wohnhaus-Modelle auf Wohnhausplätzen der Karte (8 m tief, ohne Vorplatz): Die GLB sind für 8 × 10 m mit 2 m Vorplatz gebaut, die Hauskörper sind höchstens 7,4 m tief. Beim Einbau das Modell dort um 1 m zur Gasse versetzen (die Freitreppe des Steinhauses ragt dann in die Gasse); breitere Plätze lassen links und rechts Luft
- Weitere Modelle ins Spiel einbauen, sobald es sie gibt: die Einwohnerhäuser, die Werkstätten (Stadtpalais und Hof kommen von selbst, PR #111) – nach dem Muster `WohnhausModellEinsetzen` (PR #105)
- Weitere Modelle aus Blender-Skripten: Stadtpalais, Stadthof; Werkstätten mit Stufe1–3 der übrigen Berufe (Gardist, Geldleiher, Parfumeur), je Beruf ein Schritt; Einwohnerhäuser (Ratsherrenhaus, Bürgerhaus, einfaches Haus)
- Werkstätten einbauen: `schmied.glb`, `tischler.glb`, `steinmetz.glb`, `alchimist.glb`, `wirt.glb`, `prediger.glb` und `dieb.glb` statt `WerkstattAussenBauen` und `AusbauBauen` beim Beruf Schmied, Tischler, Steinmetz, Alchimist, Wirt, Prediger bzw. Dieb; genau den Knoten `Stufe<n>` der Betriebsstufe zeigen, das Gerüst über „Geruest“, die Glut über das Material „Glut“ (Emission an, solange gearbeitet wird; ersetzt `huelle.Esse`), die verkaufte Werkstatt über „Leerstand“ ohne „Wappen“ und „Fahne“. Klickfläche, Innenraum und Schild bleiben wie bisher
- Weitere Modelle aus Blender-Skripten: Stadtpalais, Stadthof; Werkstätten mit Stufe1–3 der übrigen Berufe (Steinmetz, Alchimist, Wirt …), je Beruf ein Schritt; Einwohnerhäuser (Ratsherrenhaus, Bürgerhaus, einfaches Haus)
- Werkstätten einbauen: `schmied.glb`, `tischler.glb`, `steinmetz.glb`, `alchimist.glb`, `wirt.glb`, `prediger.glb`, `dieb.glb` und `gardist.glb` statt `WerkstattAussenBauen` und `AusbauBauen` beim Beruf Schmied, Tischler, Steinmetz, Alchimist, Wirt, Prediger, Dieb bzw. Gardist; genau den Knoten `Stufe<n>` der Betriebsstufe zeigen, das Gerüst über „Geruest“, die Glut über das Material „Glut“ (Emission an, solange gearbeitet wird; ersetzt `huelle.Esse`), die verkaufte Werkstatt über „Leerstand“ ohne „Wappen“ und „Fahne“. Klickfläche, Innenraum und Schild bleiben wie bisher
- Weitere Modelle ins Spiel einbauen, sobald es sie gibt: Stadtpalais und Hof in `SpielhausModell` (Stufe 3 und 4 bauen noch Grundkörper), die Einwohnerhäuser, die Werkstätten – nach dem Muster `WohnhausModellEinsetzen` (PR #105)
- Ankerpunkte der Modelle nutzen: Passanten gehen das letzte Stück vom Gassennetz bis „Anker_Tuer“ (mit dessen Höhe, also die Stufen hinauf) und verschwinden erst dort; Karren halten an „Anker_Karren“. Für die eingebauten Wohnhäuser ersetzt der Anker die Haustür aus der Fläche, für Werkstätten den Hof (`HofVon`, 5,5 m vor der Mitte). Den Anker nach dem Einsetzen über `FindChild` suchen und seine globale Lage nehmen – das Modell ist schon verschoben und gedreht
- Fensterlicht bei Nacht schalten: Die Emission des Materials „Fenster“ abends an (Farbe sRGB 1,0 / 0,62 / 0,28, Stärke wie in den Nachtbildern), morgens aus – alle Häuser teilen das geladene Material, ein Schalter genügt. Leere Häuser (Leerstand, Sanierung) bekommen statt „Fenster“ das dunkle „Glas“ (wie beim Einfärben der „Hausfarbe“)
- (von Spiel-KI) Zu jedem neuen Modell die `.glb.import` mitcommitten: Godot legt sie beim ersten Import an (`--headless --import --path game`). Fehlten bisher bei Alchimist, Steinmetz, Tischler, Wirt, Prediger, Dieb und Gardist und kamen in PR #111, #115 und #119 nach.

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
### Wohnhaus-Modelle im Spiel (PR #105)

| Thema | Entscheidung | Datum |
|---|---|---|
| Einbau | `SpielhausModell` lädt für Fachwerkhaus, Bürgerhaus und Steinhaus (Stufe 0–2) die GLB aus `game/assets/modelle/` (einmal je Stufe, `WohnhausSzene`) und setzt sie mit `WohnhausModellEinsetzen` ein; Stadtpalais und Hof bauen weiter Grundkörper. Fehlt eine Datei, meldet das Spiel einen Fehler (der Rauchtest schlägt an) und baut das Haus aus Grundkörpern. Die `.glb.import` sind eingecheckt wie bei den Bildern. | 17.10.2026 |
| Lage | Die Hausfront liegt im Modell bei z = +3 (Mitte der 10 m tiefen Fläche, 2 m Vorplatz). Das Modell sitzt bei `vorne − 3`: auf einem Neubau (10 m) genau in der Mitte, auf den Wohnhausplätzen der Karte (8 m, Front an der Gasse) 1 m weiter zur Gasse. | 17.10.2026 |
| Zustände | Nach Namen: „Geruest“ bei Ausbau, Sanierung und Brand; „Leerstand“ (Bretter) nur im Leerstand; „Wappen“ und „Fahne“ nur bei einem Besitzer (nicht im Leerstand, nicht während der Sanierung). Das Code-Gerüst und das Code-Wappen entfallen für diese Stufen. | 17.10.2026 |
| Farben | Das Material „Hausfarbe“ wird je Haus durch eine Kopie in der Besitzerfarbe ersetzt (je Vorlage und Farbe einmal); ohne Besitzer verwittertes Holz statt Spielerblau. Nach einem Brand werden die Dachmaterialien (Dachziegel, Stroh, Schiefer samt Kanten und First) zu 80 % rußig gefärbt. | 17.10.2026 |

### Ankerpunkte für Türen und Karren (PR #106)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wozu | Passanten und Karren sollen an die echte Tür bzw. das Tor des Modells gehen und erst dort verschwinden, nicht vor der Hauswand. Heute enden ihre Wege an Punkten, die aus der Fläche berechnet sind (Haustür, `HofVon`). | 17.10.2026 |
| Form | Leere Knoten im GLB mit festem Namen: „Anker_Tuer“ (Pflicht, Eingang für Personen) und bei Werkstätten „Anker_Karren“ (Halteplatz vor dem Tor). Keine Dreiecke, kein Einfluss auf das Einfärben nach Namen (das sucht nur Meshes). | 17.10.2026 |
| Lage und Richtung | Der Anker liegt direkt vor der Tür, in der Höhe der obersten Stufe davor (Bürgerhaus 0,46 m, Steinhaus 0,5 m, Fachwerkhaus 0,2 m, Schmied auf der Schwelle 0,3 m). Seine Blickrichtung (Godot −Z) zeigt ins Haus. Der Karrenanker liegt auf dem Boden des Vorplatzes. | 17.10.2026 |
| Stufen | Ändert sich ein Zugang mit der Stufe, hängt der Anker im Knoten der Stufe. Beim Schmied bleiben beide am Tor der Halle, in jeder Stufe gleich. | 17.10.2026 |
| Prüfung | `pruefen.py` listet die Anker mit Lage und Richtung und verlangt „Anker_Tuer“ auf der Fläche. `<name>-anker.png` zeigt sie als Pfeile (Personen magenta, Karren türkis), die Spitze auf dem Ankerpunkt. | 17.10.2026 |

### Licht in den Fenstern bei Nacht (PR #106)

| Thema | Entscheidung | Datum |
|---|---|---|
| Material | Scheiben, hinter denen nachts Licht brennt, tragen das Material „Fenster“, die übrigen „Glas“. Tagsüber sehen beide gleich aus. In der GLB ist „Fenster“ **ohne** Leuchten – die eingebauten Wohnhäuser bleiben unverändert, bis das Spiel es schaltet. | 17.10.2026 |
| Auswahl | Etwa zwei von drei Fenstern sind „Fenster“, fest aus der Lage des Fensters (bei jedem Erzeugen gleich). So wirkt eine Stadt bei Nacht belebt statt gleichförmig. Die Laterne am Portal des Steinhauses leuchtet mit. | 17.10.2026 |
| Schalten | Das Spiel setzt nachts die Emission des einen gemeinsamen Materials „Fenster“ (warmes Orange) – ein Schalter für die ganze Stadt. Leere Häuser bekommen eine Kopie in „Glas“. Die „Glut“ der Essen bleibt davon getrennt (sie hängt an der Arbeit, nicht an der Uhrzeit). | 17.10.2026 |
| Vorschau | `<name>-nacht.png` zeigt jedes Modell bei Mondlicht mit Fensterlicht und Glut; einzeln mit `--nur-nacht`. | 17.10.2026 |

### Platzhalter für Stadtpalais und Stadthof (PR #111)

| Thema | Entscheidung | Datum |
|---|---|---|
| Einbau | `SpielhausModell` sucht auch `game/assets/modelle/stadtpalais.glb` (Stufe 3) und `stadthof.glb` (Stufe 4). Fehlt die Datei, baut der Code wie bisher einen Platzhalter aus Grundkörpern – ohne Fehler (nur Stufe 0–2 sind Pflicht). Das echte Modell kommt aus einem anderen Chat; liegt die Datei samt `.glb.import` im Repository, erscheint es ohne Änderung am Code. Entschieden vom Nutzer: erst nur Platzhalter, Größe auf schmalen Plätzen egal. | 17.10.2026 |
| Konventionen | Wie bei den Wohnhäusern (PR #104, #105): Ursprung in der Mitte der belegten Fläche inklusive 2 m Vorplatz, Front nach +Z. Stadtpalais 12 × 10 m (Front bei z = +3), Stadthof 16 × 14 m (Front bei z = +5). Knoten „Geruest“, „Leerstand“, „Wappen“, „Fahne“, Material „Hausfarbe“, Dachmaterialien wie gehabt. | 17.10.2026 |
| Schmale Plätze | Auf den Wohnhausplätzen am Ring (17 × 8 m) steht die Front an der Gasse und ein tieferes Modell ragt nach hinten über den Platz hinaus. Mit einer Ersatzdatei (Steinhaus als Palais und Hof) im Klicktest geprüft: Lage, Hausfarbe und Fahne stimmen. | 17.10.2026 |
### Werkstatt des Tischlers als 3D-Modell (PR #109)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufbau | `tischler.glb` nach dem Muster des Schmieds: „Haus“ (Werkstatt mit Giebel zur Gasse) und genau eine von „Stufe1“–„Stufe3“, Fläche 16 × 14 m, Anker an Tor und Rampe. | 17.10.2026 |
| Werkstatt | Zwei Fachwerkgeschosse auf Bruchsteinsockel, Schieferdach wie bisher im Spiel. Im Giebel eine Ladeluke mit Kranbalken, Rolle und Seil für das Holz auf dem Dachboden, unten ein zweiflügeliges Tor in der Hausfarbe mit Rampe. Zunftzeichen: Säge am Ausleger. | 17.10.2026 |
| Wachstum | Tischlerei: Vordach mit Hobelbank und Spänen, Bretterstapel zum Trocknen, Sägebock, Rundholz, Bretterzaun. Drechslerei: Anbau mit offener Laube zur Gasse, darin die Wippdrehbank mit federnder Stange und gedrechselte Ware, Gaube. Sägewerk: offene Sägehalle mit Gattersäge und Stamm auf dem Wagen, rechts daneben ein Göpel (achteckiges Schieferdach, Pferd am Zugbaum, Königswelle mit Kammrad, liegende Welle zur Säge; ersetzt auf Wunsch des Nutzers das zuerst gebaute Wasserrad), Dachreiter mit kupfergrüner Spitze wie bisher im Spiel. | 17.10.2026 |
| Deckungsgleiche Flächen | Jedes Skript sucht beim Erzeugen nach Flächen, die in derselben Ebene liegen und sich überdecken (Spiel: Flimmern, Cycles: schwarz), und warnt mit Ort und Größe. Sichtbare Fälle werden behoben (Laubenboden, Rampe, Torschwelle des Schmieds, Sägebock); kleine Reste liegen verdeckt im Inneren der Körper. | 17.10.2026 |

### Werkstatt des Steinmetzen als 3D-Modell (PR #110)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufbau | `steinmetz.glb` nach dem Muster von Schmied und Tischler: „Haus“ und genau eine von „Stufe1“–„Stufe3“ (Maurerei, Steinschneiderei, Steinmetzhütte), Fläche 16 × 14 m, Anker an Tor und Rampe. | 17.10.2026 |
| Werkstatt | Massiv wie bisher im Spiel: zwei Geschosse aus Bruchstein mit Lagerfugen, Eckquader, Gesimse und Fenstergewände aus Sandstein, Ziegeldach mit der Traufe zur Gasse und Giebeln aus Bruchstein. Rundbogentor in der Hausfarbe mit Rampe aus Sandsteinplatten, Wappen darüber, Zunftzeichen: Sandsteintafel mit Zirkel und Winkel am Ausleger. | 17.10.2026 |
| Wachstum | Maurerei: Vordach mit zwei Bänken und Werkstücken, Mörtelkasten, Sandhaufen und Kalkfässer, Quaderstapel, Rohblöcke und Bruchsteine. Steinschneiderei: steinerner Anbau mit Giebel zur Gasse, Steinsäge, Schleifstein, Dreibein mit Flaschenzug und hängendem Block (wie bisher im Innenraum), Gaube. Steinmetzhütte: offene Bauhütte mit Ziegeldach, darin die Steinfigur in Arbeit (wie bisher im Innenraum) und eine Säule; Dreibein im Hof, Dachreiter mit Schieferspitze wie bisher im Spiel. In jeder Stufe Ware an der Gasse (Grabsteine, Mühlsteine, Schleifziegel; in Stufe 3 Taufbecken, Grabmal und Säule) und eine Trockenmauer statt Bretterzaun. | 17.10.2026 |
| Bausteine | `pultdach` liegt jetzt in `bausteine.py`, der Dachreiter hat wählbare Wände (`koerper`, `ecken`). Schmied und Tischler bleiben byte-gleich. | 17.10.2026 |

### Werkstatt des Alchimisten als 3D-Modell (PR #112)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufbau | `alchimist.glb` nach dem Muster der anderen Werkstätten: „Haus“ und genau eine von „Stufe1“–„Stufe3“ (Tinkturei, Kräuterladen, Alchimistenstube), Fläche 16 × 14 m, Anker an Haustür und Ladenfenster. | 17.10.2026 |
| Laboratorium | Verputzt wie bisher im Spiel, auf Bruchsteinsockel mit Eckquadern; das Obergeschoss kragt zur Gasse vor (Schwelle und Knaggen). Steiles Schieferdach mit dem Giebel zur Gasse, Wappen im Giebel, Steinfenster mit Läden in der Hausfarbe, Haustür mit Sandsteingewände und Stufen, Ladenfenster mit Klappläden (unten Ladenbrett, oben Vordach), Laborschlot mit Haube. Zunftzeichen: grüner Glaskolben am Ausleger. | 17.10.2026 |
| Wachstum | Tinkturei: Fässer mit Kräuterauszügen an der Hauswand, Trockengestelle mit Kräuterbündeln (wie bisher im Spiel vor dem Tor), Farbbottich, Brennholz, Kräuterbeete. Kräuterladen: Ladenanbau mit Giebel zur Gasse, Auslage mit Flaschen und Tiegeln, Hängeschild mit Mörser, Flaschen auch auf dem Ladenbrett des Hauses, Rosenbeete (Rosenwasser), Gaube. Alchimistenstube: achteckiger Laborturm an der Gasse mit Schieferhelm und Schlot, im ersten Stock der Feuerschein des Athanors (Material „Glut“, vom Spiel geschaltet wie die Esse), Ziehbrunnen, Gaube, Dachreiter mit violetter Spitze wie bisher im Spiel. | 17.10.2026 |
| Bausteine | `achteck` und `rund_oben` liegen jetzt in `bausteine.py`; neue Materialien Kraut, Heu, Rose, Farbe, Elixier, Violett. Schmied, Tischler und Steinmetz bleiben byte-gleich. | 17.10.2026 |

### Werkstatt des Wirts als 3D-Modell (PR #114)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufbau | `wirt.glb` nach dem Muster der anderen Werkstätten: „Haus“ und genau eine von „Stufe1“–„Stufe3“ (Schänke, Wirtshaus, Gasthaus), Fläche 16 × 14 m, Anker an Schänkentür und Toreinfahrt. | 17.10.2026 |
| Gasthof in L-Form (Wunsch des Nutzers) | Gassenflügel (8,6 × 5,2 m, Traufe zur Gasse) und rechts dahinter ein schmalerer Seitenflügel nach hinten (3,4 × 6,2 m), dessen Dach in die hintere Dachfläche läuft. Der Seitenflügel steht 0,4 m hinter dem rechten Giebel zurück, damit seine Traufe im Gassenflügel endet. So zeigt der Spielblick von vorn rechts beide Schenkel; im Winkel liegt hinten links der Hinterhof. Fachwerk auf Bruchsteinsockel, Ziegeldach wie bisher im Spiel, Toreinfahrt mit Rampe, Schänkentür mit Stufe, Wirtshausschild in Ocker mit Bierkrug am Ausleger (wie bisher im Spiel), darunter ein grüner Kranz als Schankzeichen. | 17.10.2026 |
| Wachstum | Jede Stufe: Tisch mit Bänken neben der Tür und Fasslager an der Hauswand (wie bisher im Spiel), Brennholz und Fässer im Hinterhof. Schänke: Linde mit zwei Tischen im Hof. Wirtshaus: Biergarten unter einer Laube mit Weinlaub, Linde, Kegelbahn am Zaun mit Pultdach über den Kegeln, Gaube. Gasthaus: Stall für die Pferde der Reisenden mit Stalltüren und Heuluken, Tränke mit Pferd, Biergarten, Linde, Gaube, Dachreiter mit weinroter Spitze wie bisher im Spiel. | 17.10.2026 |
| Bausteine | `tor_fluegel` und `pferd` liegen jetzt in `bausteine.py`; neue Materialien Weinrot, Ocker, Laub, Laub hell. Die übrigen Werkstätten bleiben byte-gleich. | 17.10.2026 |

### Werkstatt des Predigers als 3D-Modell (PR #116)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufbau | `prediger.glb` nach dem Muster der anderen Werkstätten: „Haus“ und genau eine von „Stufe1“–„Stufe3“ (Kirche, Dom, Kathedrale), Fläche 16 × 14 m, Anker an Portal und vor dem Turm. | 17.10.2026 |
| Kirche | Massiv wie bisher im Spiel: Saalkirche aus Haustein mit Lagerfugen, Strebepfeilern mit Wasserschlag und Spitzbogenfenstern, steiles Schieferdach mit dem Giebel zur Gasse, Spitzbogenportal in der Hausfarbe mit Freitreppe, Rosette, goldenes Kreuz auf dem Giebel, Opferstock vor dem Tor. Glockenturm vorn rechts (wie bisher im Spiel an der Ostseite) mit Eckquadern und Gesimsen, Turmuhr mit Zifferblatt in der Hausfarbe, Schallöffnungen mit Lamellen, Schieferhelm mit goldenem Kreuz. | 17.10.2026 |
| Wachstum | Kirche: Kirchhof mit Gräbern (Holzkreuze, Grabsteine), Linde mit Bank, Kirchhofmauer. Dom: dazu Chor mit polygonaler Apsis und Strebepfeilern, Skriptorium (Papier, Kerzen, Schriften) mit Lumpenballen vor der Tür, Gaube. Kathedrale: Chor und Skriptorium, Vorhalle mit Wimperg und Krabben vor dem Portal, Fialen auf allen Strebepfeilern, ein Strebebogen an der Langseite, Dachreiter mit silberner Spitze wie bisher im Spiel. | 17.10.2026 |
| Bausteine | `steingiebel` (jetzt auch ohne Fenster), `grabstein`, `trockenmauer` und `linde` liegen in `bausteine.py`; neues Material Silber. Die übrigen Werkstätten bleiben byte-gleich. | 17.10.2026 |

### Werkstatt des Diebs als 3D-Modell (PR #117)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufbau | `dieb.glb` nach dem Muster der anderen Werkstätten: „Haus“ und genau eine von „Stufe1“–„Stufe3“ (Schmugglerloch, Unterschlupf, Diebesgilde), Fläche 16 × 14 m, Anker an der Haustür und vor dem Hoftor. | 17.10.2026 |
| Diebesnest | Heruntergekommen wie bisher im Spiel („verwittertes Holz, schief geflickt“): Erdgeschoss aus grobem Bruchstein mit vergitterten Fenstern, vorkragendes Obergeschoss aus vergrauten Brettern mit schiefen Deckleisten und Läden in der Hausfarbe (einer hängt schief), zwei Fenster vernagelt (wie bisher im Spiel). Morsches Schindeldach (neue Deckung `SCHINDEL`) mit geflickten Stellen und dem Giebel zur Gasse, Ladeluke mit Kranbalken und Sack, schiefer Schornstein, trübe Laterne über der Tür (wie bisher im Spiel), Kellerluke zur Gasse. | 17.10.2026 |
| Wachstum | Jede Stufe: Hof hinter einem hohen Bretterzaun mit offenem Tor, Diebesgut an der Hauswand (wie bisher im Spiel an der Ostseite), Falltür im Hof (wie bisher im Spiel), Feuerstelle mit Kessel (Material „Glut“, vom Spiel geschaltet wie die Esse), Kisten und Säcke. Schmugglerloch: dazu kaputter Karren und Hundehütte. Unterschlupf: Bretterverschlag an der Hauswand mit Diebesgut, Fluchtleiter zum Obergeschoss, Wäscheleine mit Verkleidungen, Taubenschlag für Botentauben, Gaube. Diebesgilde: Verschlag und Leiter, Gildenhaus im hinteren Hof, hölzerner Ausguck, Gaube, Dachreiter mit nachtblauer Spitze wie bisher im Spiel. | 17.10.2026 |
| Bausteine | Neue Deckung `SCHINDEL`, Materialien Schindel, Lumpen, Nachtblau; `gaube` hat einen wählbaren Körper (`koerper`, hier Bretter). Die übrigen Werkstätten bleiben byte-gleich. | 17.10.2026 |

### Werkstatt des Gardisten als 3D-Modell (PR #118)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aufbau | `gardist.glb` nach dem Muster der anderen Werkstätten: „Haus“ und genau eine von „Stufe1“–„Stufe3“ (Wachhaus, Stadtwache, Garnison), Fläche 16 × 14 m, Anker an der Tordurchfahrt. | 17.10.2026 |
| Kaserne (Wunsch des Nutzers) | Massiv wie bisher im Spiel: zweigeschossiges Torhaus aus Bruchstein mit Eckquadern und Gesims, Ziegeldach mit der Traufe zur Gasse, Tordurchfahrt in den Kasernenhof (Rundbogentor in der Hausfarbe, hinten offener Bogen), darüber das Stadtwappen in Rot und Weiß (wie bisher im Spiel), vergitterte Fenster der Arrestzellen und der Wachstube. Vorn rechts ein Wehrturm mit Schießscharten und Zinnen, darauf die Fahne des Hauses, an ihm das Hauswappen über der Tür zur Wachstube. Davor Speergestell und Schilderhäuschen in den Stadtfarben mit roter Fahne (wie bisher im Spiel). | 17.10.2026 |
| Wachstum | Jede Stufe: Kasernenhof hinter einer hohen Bruchsteinmauer, Pranger, Feuerkorb (Material „Glut“, vom Spiel geschaltet wie die Esse), Brennholz. Wachhaus: Bank mit Fässern, Steintrog, eiserner Käfig mit einem gefassten Dieb. Stadtwache: Exerzierplatz mit Strohpuppen, Zielscheiben, Waffenständer mit Hellebarden und Schilden, Rüstungsständer (wie bisher im Innenraum), Trog, Käfig, Gaube. Garnison: Mannschaftshaus im hinteren Hof, Fahnenmast mit der Stadtfahne, Übungsplatz, Gaube, Dachreiter mit stadtroter Spitze wie bisher im Spiel. | 17.10.2026 |
| Bausteine | Neue Materialien Stadtrot, Stadtweiß und „Spitze rot“ (metallisch, nur für den Dachreiter). Die übrigen Werkstätten bleiben byte-gleich. | 17.10.2026 |
