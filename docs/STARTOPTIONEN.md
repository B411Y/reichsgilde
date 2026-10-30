# Startoptionen für Klicktests

Startoptionen (nach `--`):

## Spiel und Tafeln

- `--beruf=<Beruf>`
- `--tiptest=1` (ein Knopf „Tooltip-Test“ in der Bildmitte mit einem Tooltip voller Verweise und vier Testbegriffen, falls das Lexikon keine liefert); `--verweistest=1` (schreibt Beispielsätze mit automatischen Lexikon-Verweisen ins Log); `--tooltipfest=<Sekunden|taste>` (Zeit bis zum Festkleben bzw. nur mit Taste, gilt nur für diesen Start)
- `--startstunde=<Stunde>` (4–22; ohne Angabe beginnt die Partie um 4 Uhr zur Zunftglocke)
- `--glaube=<Katholisch|Evangelisch|Naturglaube>` (Konfession des eigenen Hauses bei einer neuen Partie)
- `--tempo=<1–32>`
- `--innen=1` (eigene Werkstatt) oder `--innen=Rathaus|Dom|wohnhaus:<Inhaber>`
- `--raum=<Nummer>` (mit `--innen`: Raum der Raumleiste, 1 = erster)
- `--innenblick=<0|L1|L2|R1|R2|O1|U1>` (mit `--innen`: Blickpunkt des Raums, was das Modell hat)
- `--tafel=<personal|produktion|lager|karren|einrichten|talentbaum|wache|bank|auftraege>` (mit `--innen`: Tafel der Bedienung öffnen; `bank` = Bankgeschäft der Geldleihe, `auftraege` = Kartentisch des Diebs mit Aufträgen und Banden im Hinterzimmer)
- `--arbeiter=<Nummern>` (mit `--innen`: Arbeiter im Raum auswählen)
- `--baum=<wohnhaus|betrieb>` (Talentbaum der Einrichtung von außen öffnen: Wohnhaus oder eigene Werkstatt; mit `--vermoegen=5000`; `--tafel=talentbaum` mit `--innen=1` ist derselbe Baum der Werkstatt, `--tafel=personal` das Personalbuch mit Porträts)
- `--einrichtung=<Kennungen>` (der eigenen Werkstatt Einrichtung kaufen, z. B. `Werkzeug1,Regale`, mit `--vermoegen=5000`)
- `--einstellen=<Arten>` (Arbeiter einstellen, z. B. `Lehrling,Geselle`)
- `--ausbauen=<n>` (die Werkstatt n-mal ausbauen; braucht Meistergrad 3 und 5: Stand mit `--speichern` schreiben, `Meistergrade` des Hauses auf `{"Schmied":5}` setzen, mit `--laden` starten)
- `--verwalten=1` (Kurzübersicht der eigenen Werkstatt rechts; `=mehr`: mit „…“ aufgeklappt)
- `--wohnhaus=<haus|familie|handwerk|geld|hinterzimmer>` (Tafel des eigenen Wohnhauses auf diesem Reiter)
- `--auswahl=<Kennung>` (auch `weg:<Nummer>[,<Stelle>]`: die Wegtafel eines Weges der Karte öffnen und die Kamera hinfahren; Nummer = Reihenfolge in `Karte.Wege`, die Mitte steht im Protokoll)
- `--wegausbau=<weg>,<von>,<bis>,<Kies|Pflaster>[|…]` (einen Wegabschnitt auf Kosten des Spielers ausbauen lassen, Meter entlang des Verlaufs; mit `--vermoegen=`)
- `--wegantrag=<weg>,<von>,<bis>` (Ratsantrag „Weg pflastern“)
- `--drehung=<1–3>`
- `--vermoegen=<Gulden>`
- `--rat=1`
- `--amt=<Amt>` (der Spieler hält dieses Amt, z. B. `Kämmerer`, `Stadtrichter`, `Gefängniswärter`; Leerzeichen als `_`)
- `--kasse=<Gulden>` (Stand der Stadtkasse)
- `--antrag=steuer:<Prozent>|bau:<Kennung>|fest` (Antrag im Rat stellen, mehrere mit Komma, z. B. `bau:brunnen,fest`; braucht ein Amt, z. B. `--amt=Bürgermeister`; Kennungen: `brunnen`, `mauer`, `hafen`, `marktplatz`). Die Ratssitzung ist am Jahresende: mit `--startstunde=5 --spulen=1440` steht man mittags im nächsten Jahr (Baustelle, Fest), die Lage der Baustelle steht im Log
- `--rathaus=<stadtkasse|rat|amt>` (das Rathaus auswählen, Tafel auf diesem Reiter); eine Baustelle der Stadt: `--auswahl=stadtbau:<Kennung>`
- `--sitzung=1` (sichtbare Ratssitzung vorbereiten: ohne `--amt` ein Amt der untersten Stufe und eine Bewerbung eine Stufe höher – dafür `--aufsteigen=1 --vermoegen=30000 --startstunde=6` –, ohne `--antrag` wird ein Rivale Kämmerer und beantragt ein Fest; dann bis zum Jahresende vorspulen, die Partie hält vor dem ersten Punkt). `--sitzung=letzte`: ebenso, aber ohne Halt; danach steht das Rathaus auf dem Reiter Rat mit dem Knopf „Letzte Ratssitzung ansehen“
- `--sitzungzeigen=<immer|betroffen|nie>` (Einstellung „Ratssitzung zeigen“ nur für diesen Start)
- `--familie=1`
- `--charakter=<oberhaupt|ehepartner|kind|ratsherr[:n]|Personennummer>` (öffnet das Charakterfenster dieser Person, nach `--spulen`; `kind` = erstes Kind des eigenen Hauses, sonst irgendeines; `ratsherr:2` = zweiter Ratsherr mit Amt); `--charakter-ehe=1` (das Oberhaupt heiratet gleich zu Beginn die standesgemäße Kandidatin, die am ehesten einwilligt – Zuneigung, Ansehen und ihre Meinung –, mit `--vermoegen=30000`; mit `--spulen=17100` wachsen Kinder heran)
- `--portraets=<Volk>[,<mann|frau>[,<Gesicht 0–4>[,<Haarfarbe>[,<Augenfarbe>]]]]` (öffnet im Hauptmenü die Seite „Euer Oberhaupt“ mit dieser Vorwahl, z. B. `--portraets=elb,frau,2,silberblond,violett`; `--portraets=1` = Menschen, Mann, Gesicht 1); dazu `--blinzeln=zu|offen|<0–1>` (hält die Augen fest, für Bildschirmfotos; `0.5` = halb)
- `--gericht=1`
- `--hinterzimmer=1`
- `--markt=1`
- `--kischmied=aus|wenige|alle` (Passanten mit dem KI-Schmied aus TRELLIS: `aus` nur die Figuren aus Blender, `wenige` (Standard) jeder zweite Knecht und Tagelöhner, `alle` jeder Mann (Mensch, nicht gebeugt, ohne Last); für Vergleiche mit `--blick=0,0,25 --messung=12`. In der Schmiede (`--innen=1`) tragen die Arbeiter ihn immer)
- `--karren=<Typen>` (der eigenen Werkstatt Karren dazukaufen, z. B. `Ochsenkarren,Kriegskarren`, mit `--vermoegen=5000`; die beiden kosten zusammen 1.300)
- `--umland=1` (den günstigsten freien Umlandbetrieb erwerben – wenn nötig erst zum Bürger aufsteigen –, ihm einen Ochsenkarren dazukaufen und seine Kurzübersicht mit „…“ zeigen: Karren mit Eskorte, Nachtfahrt und „Verkaufen“; mit `--vermoegen=30000`)
- `--schiff=<Anzahl>` (am ersten Hafen so viele Flussschiffe kaufen, mit `--vermoegen=10000`; Karte mit Hafen, z. B. `--karte=probestadt` oder `greybox`)
- `--schiffsauftrag=vorschlag` (jedem eigenen Schiff am Kai den Vorschlag als Auftrag geben; `=<Ziel>`: den Vorschlag für diese ferne Stadt, z. B. `Eisenfurt`, Leerzeichen als `_`)
- `--hafen=1` (Hafentafel des ersten Hafens rechts öffnen, ohne `--blick` fährt die Kamera hin; `=<Kennung>` eines Hafens der Karte, z. B. `Hafen1`; `=staedte`: Reiter „Ferne Städte“). Ein ausgewähltes Schiff: `--auswahl=schiff:<Name>`
- `--verkaufen=1` (Rückfrage zum Verkauf der eigenen Werkstatt; `=2` erwirbt vorher die günstigste freie Liegenschaft dazu (sonst: Wohnhaus ausbauen und aufsteigen) und lässt bis Mittag laufen, mit `--vermoegen=20000`)
- `--bericht=1` (Jahresbericht in der Mitte); `--bericht=<jahresrechnung|rivalen|umsaetze|protokoll>` (die Tafel „Bericht“ rechts auf diesem Reiter)
- `--hilfe=1` (Hilfe zur Steuerung gleich zeigen; ohne Startoptionen erscheint sie beim allerersten Spiel von selbst)
- `--lexikon=<1|kennung|suche:text>` (das Lexikon öffnen: `1` = Startseite, `<kennung>` = gleich bei diesem Begriff, z. B. `--lexikon=stand`, `suche:<text>` = mit diesem Suchtext, Leerzeichen als `_`)
- `--lexikon-pruefen=1` (nur die Datei `game/daten/lexikon.json` prüfen und beenden: Verweise, Platzhalter, Kennungen; Rückgabewert 0 = in Ordnung, 1 = Fehler; Ausgabe auf der Konsole, z. B. `Godot --headless --path game -- --lexikon-pruefen=1`)
- `--meldung=<Text>` (eine Meldung unten links einstellen, Leerzeichen als `_`; beginnt sie mit ✗ oder ⚠, ist es eine Warnung, die bis zum Klick bleibt)
- `--einstellungen=1`
- `--wegenetz=1` (Gassennetz der Passanten einblenden)
- `--laden=<datei.json>` (aus `~/.local/share/godot/app_userdata/Reichsgilde/spielstaende`)
- `--karte=<kennung>` (neue Partie auf einer anderen Karte aus `game/karten/`, z. B. `koeln`, nur für diesen Start; im Hauptmenü wählt man sie unter „Neue Partie“ → „Stadt wählen“)
- `--stadtwahl=1` (Hauptmenü öffnet gleich die Seite „Stadt wählen“, für Bildschirmfotos)
- `--entwickler=1|0` (Entwickler-Modus für diesen Start an oder aus, ohne die gemerkte Einstellung zu ändern; die Kartenwahl zeigt dann auch Test- und Messkarten)
- `--entwicklerfenster=<1–6>` (öffnet das Entwicklerfenster gleich, `1` = erster Reiter, `2` bis `6` = Familie und Ereignisse, Wirtschaft, Wetter und Darstellung, Messwerte, Klänge; nur mit `--entwickler=1` oder dem Haken „Entwickler-Optionen“, sonst gibt es das Fenster nicht; Taste F12 und der Knopf „Entwickler“ in der Betriebsleiste schalten es um)
- `--kartenbild=<ziel.png>` (Karte als Vorschau für die Kartenwahl aufnehmen, 480 × 270, und beenden; mit `--karte=<kennung>`, gemacht für `tools/kartenbilder/kartenbilder.sh`)
- `--ladezeit=1|ende` (Messausgabe der Ladezeit: Zeilen „Ladezeit: …“ mit den Millisekunden je Abschnitt – Kern, Gelände, Häuser, Wege, Wasser, Natur, Modelle laden –, danach die Dauer der ersten Bilder samt übersetzten Pipelines und der Arbeit in ihnen; `ende` beendet das Spiel, sobald die Bilder ruhig sind. Mit dem Ladebildschirm misst „Ladebild weg“ die Bilder hinter ihm, die Bildzeiten danach gelten den ersten sichtbaren Bildern; „Bild zwischen den Abschnitten“ ist das Bild, das der Balken je Abschnitt braucht. Dazu `--ladezeitbilder=<ordner>`: Fotos beim ersten Bild, nach 1 s, nach 3 s und am Ende; `game/scripts/Ladezeit.cs`)
- `--menuhintergrund=1` (die Greybox hinter dem übersprungenen Hauptmenü trotzdem bauen – wie der Weg über das Menü, das dabei Modelle und Shader schon lädt; nur für Vergleiche der Ladezeit, sonst lassen Startoptionen sie weg)
- `--vorwaermen=0` (Regen und Schnee hinter dem Ladebildschirm nicht vorab zeigen; für Vergleiche des ersten Regens)
- `--schwierigkeit=<Normal|Schwer>` (Schwierigkeit der neuen Partie, nur für diesen Start; Normal = KI-Häuser starten wie der Spieler, Schwer = mit Umlandbetrieb)
- `--klickskript=<aktionen>` und `--klickausgabe=<ordner>` (Klicktest ohne echte Maus, für `tools/klicktest/lauf.ps1` unter Windows: das Spiel führt die Aktionen von `lauf.sh` selbst aus – `m x y`, `w sek`, `k taste`, `b knopf`, `d x1 y1 x2 y2`, `s name`, durch `;` getrennt –, speichert die Fotos als PNG im Ordner und beendet sich; nur zusammen wirksam; zählen nicht als Startoption, das Hauptmenü bleibt also stehen)
- `--klickwarten=<Sekunden>` (mit `--klickskript`: so lange ab Programmstart warten, bevor die erste Aktion läuft; Standard 0)

## Grafik (nur für diesen Start)

- `--kanten=<Aus|Fxaa|Smaa|Taa|Msaa2|Msaa4|Msaa8>` (nur für diesen Start)
- `--schatten=<Niedrig|Mittel|Hoch>` (Schattenqualität, nur für diesen Start)
- `--bloom=<An|Aus>` und `--bloomstaerke=<0–100>` (Bloom, nur für diesen Start)
- `--ssao=<Aus|Niedrig|Mittel|Hoch>` (Umgebungsverdeckung)
- `--ssil=<An|Aus>` (indirektes Licht)
- `--fsr=<An|Aus>` (FSR 2.2) und `--renderaufloesung=<25–100>` (Renderauflösung bei FSR in Prozent), alle nur für diesen Start, dazu (ebenso nur für diesen Start) `--voreinstellung=<Niedrig|Mittel|Hoch|Ultra>` (alle Grafikregler einer Stufe, einzelne Optionen danach überschreiben sie)
- `--bildrate=<Zahl, 0 = unbegrenzt>`
- `--vsync=<Aus|An|Adaptiv>`
- `--dynamisch=<Ziel-Bildrate, 0 = aus>` (dynamische Auflösung)
- `--natur=<25–100>`
- `--passanten=<25–100>`
- `--feinheiten=<Nah|Mittel|Weit>`
- `--boden=<Niedrig|Mittel|Hoch>` (Bodenqualität: Mischung der Bodentexturen, Normalen, Grasbüschel)
- `--gras=<Aus|Dicht|SehrDicht>` (Grasteppich: 3D-Halme auf der Wiese, nur für diesen Start)
- `--tiltshift=<Aus|Leicht|Stark>`
- `--wolken=<An|Aus>`
- `--dunst=<Aus|Niedrig|Hoch>`
- `--wettereffekte=<Niedrig|Mittel|Hoch>` (wie viel Regen, Schnee, Nässe und Blitz zu sehen ist)
- `--wetter=<klar|wolkig|regen|sturm|gewitter|schnee|nebel>` (Wetterlage fest vorgeben, nur Darstellung; ohne Aussicht in der Wetterzeile)
- `--jahreszeit=<fruehling|sommer|herbst|winter>` (Jahreszeit der Darstellung: Wiese, Laub, Äcker, Schnee; der Kalender bleibt)
- `--lichtuhr=<Stunde>` (Licht, Himmel, Fenster und Nachtlichter zeigen diese feste Uhrzeit, mit Bruchteil: `3.5` = 3:30 Uhr, auch vor 4 Uhr; nur Darstellung, die Spielzeit läuft weiter; mit `--jahreszeit` wählt man die Sonnenzeiten)
- `--blitz=1` (mit `--wetter=gewitter`: ein Blitz bleibt auf voller Helligkeit stehen, für Bildschirmfotos)
- `--wetterspaeter=<lage>@<Sekunden>` (das Wetter schlägt nach so vielen Sekunden plötzlich um, z. B. `regen@14`; für Messungen des ersten Regens: ohne `--wetter` ändert es sich erst dann, sonst gilt `--wetter` bis dahin)
- `--pflasterrelief=0` (Pflaster ohne Tiefe: schaltet die Parallax im Pflaster für diesen Start aus, nur in der Bodenqualität Hoch wirksam; für Vergleiche und Messungen)
- `--messe=1` (Kirchen und Dom leuchten, als liefe gerade die Messe; nur Darstellung, für Bildschirmfotos bei Nacht)
- `--tonemapping=<Bisher|Agx>`
- `--helligkeit=`/`--kontrast=`/`--saettigung=<Prozent>`
- `--schaerfe=<0–100>`
- `--fahnen=<An|Aus>`
- `--uigroesse=<75–200>`
- `--farbfilter=<Aus|Protanopie|Deuteranopie|Tritanopie>`
- `--klangtest=1` (spielt alle Oberflächenklänge nacheinander und schreibt `KLANGTEST <Ereignis>: n/m Varianten (Längen)` ins Log; am Ende `KLANGTEST fertig … 0 mit fehlenden Dateien`)
- `--ansagetest=<liste>` (Ansagen des Haushofmeisters auslösen, alle 0,5 s eine, ohne Wiederholsperre; jede Zeile `ANSAGE <Ereignis>: gespielt|wartet|verworfen – Rang, Fassung, Variante` im Log, später `verfallen`/`kommt nach der Pause dran`. Liste: Kennungen mit Komma wie `tod_familie,feuer,krankheit`; `alle` = alle 20 Ereignisse in der Reihenfolge der Datei (zeigt Vorrang und Pause); `dateien` = je Ereignis, wie viele Aufnahmen vorhanden sind (löst nichts aus). Dahinter `:herrin` oder `:herr` erzwingt die Fassung, sonst gilt das Geschlecht des Oberhaupts. Braucht die Partie, also mit `--karte=…`/Neues Spiel; ohne Aufnahmen nur zusammen mit `--ansageplatzhalter=1` zu hören)
- `--ansageplatzhalter=1` (statt einer fehlenden Aufnahme der Ansagen klingt ein kurzer Glockenton von Kenney; schaltet außerdem das Ansagen-Log an, das sonst nur im Entwicklermodus schreibt)
- `--reiter=<Grafik|GrafikErweitert|Ton|Spiel|Barrierefreiheit>` (mit `--einstellungen=1`; `GrafikErweitert` öffnet Grafik mit aufgeklapptem „Erweitert“, nur für diesen Start; die früheren Namen `Voreinstellung` (= Grafik) sowie `Leistung`, `Bild` und `Stimmung` (= GrafikErweitert) gehen weiter)
- `--schnittstellezeigen=1` (zeigt die Zeile „Grafikschnittstelle“ in Grafik → Erweitert auch außerhalb von Windows, zum Ansehen und für Klicktests; geprüft wird sonst nur unter Windows)
- `--automatisch=1` (Voreinstellung messen und wählen wie beim ersten Start; ohne Startoptionen und ohne `einstellungen.cfg` geschieht das im Hauptmenü von selbst)
- `--messung=<Sekunden>` (nach 8 s Vorlauf Bilder/s, 1-%-Tief, Bild- und GPU-Zeit ins Protokoll, dazu Ruckler (Bilder über dem Dreifachen der mittleren Bildzeit) mit Zeitpunkt und die neuen Pipelines im Messfenster, dann beenden; für Vergleiche mit `--blick`, `--startstunde` und `--vsync=Aus`)

## Feuer und Flammen

Im Spiel (Phase 2, nur Darstellung):

- `--brand=werkstatt|haus|<Betriebsname>` (ein Gebäude brennt gleich beim Start, **nur in der Darstellung**, der Kern merkt nichts: `werkstatt` (oder `1`) die eigene Werkstatt, `haus` das eigene Wohnhaus, sonst der Betrieb mit diesem Namen; Flammen aus Dach und Fenstern für 120 Spielminuten, danach schwelt es 180 Minuten; am besten mit `--startstunde=21` und `--blick`. Ein echtes Feuer des Kerns lösen im Entwicklerfenster die Knöpfe „Feuer im Betrieb“ und „Hausbrand“ aus, `--entwickler=1 --entwicklerfenster=2`)

Die Testszene `scenes/Feuertest.tscn` (`Feuertest.cs`) zeigt die sieben Feuerarten nebeneinander (von links 1 Kerze, 2 Fackel, 3 Laterne, 4 Feuerkorb, 5 Esse, 6 Kamin, 7 Gebäudebrand), jede so, wie das Spiel sie auf allen Grafikstufen zeichnet (seit Phase 2: Flamme aus der Simulation, Rauch und Funken im Shader). Das Hauptmenü startet sie von selbst, sobald `--feuertest` angegeben ist; die Anleitung für den Editor steht in `docs/feuer/phase2.md`. Als Ausnahme gelten `--feuertest` und alle Optionen, die mit `--feuer` beginnen, auch **vor** dem einzelnen `--` (`Feuertest.Startargumente`): Godot reicht Argumente davor nur als eigene weiter, und ohne `--` kam bisher stumm das Hauptmenü. Alle übrigen Optionen, auch `--bildrate` und `--vsync`, wirken nur hinter `--`. Alle Optionen gelten nur für diesen Start. Für Messungen `--bildrate=0 --vsync=Aus` dazugeben (Bildrate und Bildzeit stehen jede Sekunde im Protokoll, Zeilen mit `[feuer]`).

- `--feuertest=1` (die Testszene statt des Spiels starten)
- `--feuersicht=nah|stadt|fern` (wo die freie Kamera beginnt: `nah` = leicht von oben, ganz nah an der Feuerart; `stadt` = 40° von oben, Ausschnitt wie der nächste Zoom der Stadt, 25 m hoch; `fern` = wie der weiteste Zoom, 95 m, alle Feuerarten; Standard `nah`)
- `--feuerpaar=<1–7>` (welche Feuerart `nah` und `stadt` zeigen, Standard 2 = Fackel)
- `--feuertag=1` (Tageslicht; ohne die Option ist es Nacht)
- `--feuerzeigen=0` (nur die Aufbauten, ohne Feuer, für den Vergleich der Bildrate; Taste `T`)
- `--feuerlicht=0|1` (die Lichter der Feuer aus oder an)
- `--feuerfackeln=<n>` (Messung: statt der Feuerarten `n` Fackeln im Raster, 10 je Reihe; Standardsicht `stadt`) und `--feuerlichter=<m>` (nur die ersten `m` Fackeln leuchten mit einem echten Licht)
- `--feuerzyklus=<Sekunden>` (Messzyklus: die Szene wechselt von selbst zwischen ohne und mit Feuer, im Fackelraster durch ohne Feuer und Feuer mit 0, 12 und allen Lichtern, jeden Zustand so viele Sekunden lang; der Zustand steht in jeder Protokollzeile `[feuer] … zustand=…`; `tools/feuer/messen.sh` fasst ihn zusammen)
- Entfallen mit Phase 2 (es gibt nur noch eine Fassung): `--feuerart`, `--feuerstufe`, `--feuerfassung`
- Tasten in der Testszene: `1`–`7` oder Pfeile links/rechts (Feuerart), `V` (Sicht), `N` (Nacht/Tag), `T` (Feuer an/aus), `L` (Lichter), `B` (Beschriftung), `Esc` (Ende); freie Kamera: rechte Maustaste halten (umsehen), `W` `A` `S` `D` `Q` `E` (fahren), `Shift` (schneller), Mausrad (Tempo), `H` (ihre Hilfe)

## Kartenwerkstatt

Zur Kartenwerkstatt (Level-Editor): `--kartenwerkstatt=<kennung|neu|datei.json>` (öffnet sie direkt: eine Karte aus `game/karten/` wie `greybox`, `koeln`, `probestadt`, `datei.json` aus `user://karten/`), dazu in dieser Reihenfolge:

- `--werkstatt-groesse=<breite>,<tiefe>[,<rand>]` (Größe des Spielfelds, 400 × 300 bis 2.000 × 2.000 m, Rand 100–600 m; mit `--werkstatt-rueckgaengig` zurücknehmen)
- `--werkstatt-randvorschlag=1` (Felder, Wald und Streu im Randstreifen; eine Zahl über 1 ist der Startwert)
- `--werkstatt-setzen=<katalog>,<x>,<z>,<grad>` (mehrere mit `;`)
- `--werkstatt-versetzen=<kennung>,<x>,<z>,<grad>`
- `--werkstatt-rueckgaengig=<n>`
- `--werkstatt-auswahl=<kennung>` (wählen und hinfahren)
- `--werkstatt-speichern=<datei.json>` (nach `user://karten/`; `=1` wie Strg+S – eine Karte aus `game/karten/` mit Rückfrage –, `=direkt` ohne Rückfrage; danach mit `git diff` prüfen und `git checkout -- game/karten/<datei>` zurücksetzen)
- `--werkstatt-umriss=<katalog>,<x>,<z>,<grad>` (Umriss grün/rot an dieser Stelle, bis die Maus bewegt wird)
- `--werkstatt-dialog=<neu|oeffnen|speichern|ausprobieren|zurueck|schliessen>` (`schliessen` wie das X des Fensters)

Wege und Flächen (vor `versetzen`, mehrere mit `|`, Punkte mit `;`):

- `--werkstatt-weg=<art>,<x1>,<z1>;<x2>,<z2>;…[,gerade]` (Art wie `gasse`, `land`, `feldweg`, `fussweg`; ohne `gerade` glatt; rastet ein wie mit der Maus)
- `--werkstatt-feld=<x>,<z>;…` (Feldflächen, mehrere mit `|`; die Streifen laufen längs der längsten Kante)
- `--werkstatt-flaeche=<wald|streu|feld|bauzone:<lage>|aussparung>,<x>,<z>;…` (`aussparung` in die zuletzt angelegte Wald-, Streu- oder Feldfläche)
- `--werkstatt-pinsel=<art+art>,<radius>,<dichte>,<x1>,<z1>;…`
- `--werkstatt-radierer=<radius>,<x1>,<z1>;…`

Wasser (Flüsse und Seen vor den Wegen, die Fähre danach; Punkte in Fließrichtung):

- `--werkstatt-fluss=<fluss|bach>,<breite>,<x1>,<z1>;<x2>,<z2>;…` (Breite 0 = nach der Art)
- `--werkstatt-see=[teich:]<x1>,<z1>;<x2>,<z2>;…[,<wasserstand>]`
- `--werkstatt-belag=<weg>,<stufe>[,<von>,<bis>]` (Startbelag, mehrere mit `|`: <weg> Nummer oder Name mit `_`, <stufe> `trampelpfad`, `fahrweg`, `kies`, `pflaster` oder `vorgabe`; mit von und bis nur dieser Abschnitt in Metern, nach den Wegen; ein Schritt der Geschichte je Eintrag)
- `--werkstatt-faehre=<x1>,<z1>;<x2>,<z2>` (die Anleger rasten an Wegen ein)

Brücken und Furten entstehen von selbst, wo ein Weg das Wasser quert.

Gelände (Schritt 5b):

- `--werkstatt-gelaende=<pinsel>,<radius>,<staerke>,<x1>,<z1>;…` (mehrere mit `|`; `<pinsel>` ist heben, senken, glaetten, ebnen oder rauschen, hinten mit `:kante[:startwert[:massstab]]`; Stärke bei Heben, Senken und Rauschen in Metern je Tupfer, bei Glätten und Ebnen 0–1; ein Schritt der Geschichte je Strich)
- `--werkstatt-rampe=<x1>,<z1>;<x2>,<z2>,<breite>` (mehrere mit `|`)

Bodentexturen (Schritt 5e):

- `--werkstatt-boden=<textur|radierer>,<radius>,<staerke>,<x1>,<z1>;…[,kante=<0–1>][,deko=aus][,dichte=<0–2>]` (mehrere mit `|`; `<textur>` ist `wiese`, `erde`, `fels`, `kies`, `waldboden`, `sand`, `schlamm`, `moos`, `lehm`, `pflaster` oder `mist`; ein Strich = ein Schritt der Geschichte)

Werkzeug dazu mit `--werkstatt-werkzeug=boden:<textur>` oder `boden:radierer` (Pinselkreis, Tafel).

Mauern, Tore und Start (Mauern und Tore vor den Wegen, der Start danach):

- `--werkstatt-mauer=<x1>,<z1>;<x2>,<z2>;…[,offen]` (ohne `offen` ab drei Ecken geschlossen)
- `--werkstatt-tor=<x>,<z>` (auf die nächste Mauer bis 10 m)
- `--werkstatt-start=<befehl>|<befehl>…` mit `haus[:Name]`, `wohnhaus:<Haus>:<Kennung>`, `betrieb:<Haus>:<Platz>:<Betriebsart>[:<Ware>]`, `farbe:<Haus>:#rrggbb`, `spieler:<Platz>[:<Beruf>]`, `spielerwohnhaus:<Kennung>`, `spielerfarbe:#rrggbb`, `entfernen:<Haus>` (Häuser ab 1)
- `--werkstatt-tafel=start|karte` (Reiter „Start“ bzw. „Karte“)
- `--werkstatt-gruppe=<auswaehlen|gelaende|boden|wasser|wege|flaechen|gebaeude|mauer|start|pruefen>` (die Palette dieser Gruppe der Leiste links aufklappen) mit `--werkstatt-haus=<n>` (Haus aufklappen)
- `--werkstatt-ebenen=<ebene>,…|alle` (Ebenen der Kartenwerkstatt ausblenden: `bauzonen`, `natur`, `requisiten`, `linien`, `beschriftungen`) und `--werkstatt-ebenenliste=1` (die Liste „Ebenen“ aufklappen)
- `--katalogbilder=1|<kennung>,…` (Vorschaubilder des Katalogs nach `game/assets/oberflaeche/katalog/` rendern und beenden, mit `--kartenwerkstatt=probestadt`; Skript `tools/katalogbilder/katalogbilder.sh`)
- `--werkstatt-kamerastart=1` (den Blick nach `--blick` und `--drehung` als Kamera-Start übernehmen)
- `--werkstatt-ausprobieren=<Beruf>` (zuletzt: gleich die Probepartie starten; im Spiel Esc → „Zurück zur Kartenwerkstatt“)

`--werkstatt-auswahl` nimmt auch `weg:<Name>`, `zone:<Kennung>`, `natur:<Name>`, `feld:<Name>`, `mauer:<Name>`, `fluss:<Name>`, `see:<Name>`, `faehre:<Name>` (Leerzeichen als `_`).

Nach dem Speichern und dem Umriss:

- `--werkstatt-werkzeug=<weg[:art]|bauzone[:lage]|wald|streu|feldflaeche|aussparung|mauer|tor|pinsel|radierer|gelaende:<pinsel>|rampe|wasser:<fluss|bach|see|teich|faehre|hafen>|<katalog>>` (wie ein Klick im Katalog)
- `--werkstatt-netz=1` (Gassennetz zeigen)
- `--werkstatt-bildpunkte=<x>,<z>;…` schreibt nach 1,5 s ins Protokoll, wo diese Orte auf dem Bildschirm liegen (für Klicks mit `xdotool`)

`--blick`, `--drehung` und `--ui=0` gelten auch hier.

## Bauen und Leerstand

Zum Bauen und Leerstand:

- `--bauen=1` (Baumodus mit Bautafel; `--bauen=wohnhaus:<Stufe 0–4>` oder `--bauen=werkstatt:<Betriebsart>` wählt gleich das Gebäude; `--bauen=wege` öffnet das Baumenü im Wege-Modus: Wege leuchten, Klick auf einen Weg öffnet die Wegtafel)
- `--bauort=<x>,<z>,<Drehung 0–3>` (der Umriss sitzt an dieser Stelle der Stadt in Metern, bis die Maus bewegt wird; Probestellen stehen unter `probeplan` in `greybox.json`)
- `--bauklick=1` (dort die Rückfrage öffnen; `=2` gleich bauen)
- `--aufsteigen=1` (Wohnhaus zum Bürgerhaus ausbauen und zum Bürger aufsteigen, damit Werkstätten erlaubt sind; mit `--vermoegen=30000`)
- `--spulen=<Ticks>` (so viele Spielminuten auf einen Schlag ablaufen lassen, `1200` = über die Bauzeit eines Spieljahres)
- `--leerstand=kaufen|abreissen` (mit `--auswahl=wohnhaus:Haus_Eisenbart`, Leerzeichen als `_`: Rückfrage zum leeren Haus öffnen; mit `--bauklick=2` gleich ausführen)
- `--speichern=<datei.json>` (die Partie nach `--bauklick` und `--spulen` speichern)

Ein leeres Haus entsteht durch Bearbeiten eines Spielstands: mit `--speichern` einen Stand schreiben, in der Datei bei einem KI-Konto `IstBankrott` auf `true` und `WohnhausStufe` setzen, mit `--laden` starten.

Kamera und Tafeln für Bildschirmfotos: `--blick=<x>,<z>,<Zoom>`, `--ui=0`.

Tipp: Mit `XDG_DATA_HOME=<eigener Ordner>` vor dem Aufruf landen die Spielstände des Klicktests nicht im echten Spielstand-Ordner (der Auto-Stand wird bei jedem Jahreswechsel überschrieben).
