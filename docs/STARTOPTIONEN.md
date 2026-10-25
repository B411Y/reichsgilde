# Startoptionen für Klicktests

Startoptionen (nach `--`):

## Spiel und Tafeln

- `--beruf=<Beruf>`
- `--startstunde=<Stunde>`
- `--tempo=<1–32>`
- `--innen=1` (eigene Werkstatt) oder `--innen=Rathaus|Dom|wohnhaus:<Inhaber>`
- `--raum=<Nummer>` (mit `--innen`: Raum der Raumleiste, 1 = erster)
- `--innenblick=<0|L1|L2|R1|R2|O1|U1>` (mit `--innen`: Blickpunkt des Raums, was das Modell hat)
- `--tafel=<personal|produktion|lager|karren|einrichten|wache>` (mit `--innen`: Tafel der Bedienung öffnen)
- `--arbeiter=<Nummern>` (mit `--innen`: Arbeiter im Raum auswählen)
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
- `--antrag=steuer:<Prozent>|bau:<Kennung>|fest` (Antrag im Rat stellen, mehrere mit Komma, z. B. `bau:brunnen,fest`; braucht ein Amt, z. B. `--amt=Bürgermeister`; Kennungen: `brunnen`, `mauer`, `hafen`, `marktplatz`). Die Ratssitzung ist am Jahresende: mit `--startstunde=5 --spulen=1320` steht man mittags im nächsten Jahr (Baustelle, Fest), die Lage der Baustelle steht im Log
- `--rathaus=<stadtkasse|rat|amt>` (das Rathaus auswählen, Tafel auf diesem Reiter); eine Baustelle der Stadt: `--auswahl=stadtbau:<Kennung>`
- `--sitzung=1` (sichtbare Ratssitzung vorbereiten: ohne `--amt` ein Amt der untersten Stufe und eine Bewerbung eine Stufe höher – dafür `--aufsteigen=1 --vermoegen=30000 --startstunde=6` –, ohne `--antrag` wird ein Rivale Kämmerer und beantragt ein Fest; dann bis zum Jahresende vorspulen, die Partie hält vor dem ersten Punkt). `--sitzung=letzte`: ebenso, aber ohne Halt; danach steht das Rathaus auf dem Reiter Rat mit dem Knopf „Letzte Ratssitzung ansehen“
- `--sitzungzeigen=<immer|betroffen|nie>` (Einstellung „Ratssitzung zeigen“ nur für diesen Start)
- `--familie=1`
- `--gericht=1`
- `--hinterzimmer=1`
- `--markt=1`
- `--karren=<Typen>` (der eigenen Werkstatt Karren dazukaufen, z. B. `Ochsenkarren,Kriegskarren`, mit `--vermoegen=9000`)
- `--umland=1` (den günstigsten freien Umlandbetrieb erwerben – wenn nötig erst zum Bürger aufsteigen –, ihm einen Ochsenkarren dazukaufen und seine Kurzübersicht mit „…“ zeigen: Karren mit Eskorte, Nachtfahrt und „Verkaufen“; mit `--vermoegen=30000`)
- `--schiff=<Anzahl>` (am ersten Hafen so viele Flussschiffe kaufen, mit `--vermoegen=10000`; Karte mit Hafen, z. B. `--karte=probestadt` oder `greybox`)
- `--schiffsauftrag=vorschlag` (jedem eigenen Schiff am Kai den Vorschlag als Auftrag geben; `=<Ziel>`: den Vorschlag für diese ferne Stadt, z. B. `Eisenfurt`, Leerzeichen als `_`)
- `--hafen=1` (Hafentafel des ersten Hafens rechts öffnen, ohne `--blick` fährt die Kamera hin; `=<Kennung>` eines Hafens der Karte, z. B. `Hafen1`; `=staedte`: Reiter „Ferne Städte“). Ein ausgewähltes Schiff: `--auswahl=schiff:<Name>`
- `--verkaufen=1` (Rückfrage zum Verkauf der eigenen Werkstatt; `=2` kauft vorher eine Grube dazu und lässt bis Mittag laufen, mit `--vermoegen=20000`)
- `--bericht=1` (Jahresbericht in der Mitte); `--bericht=<jahresrechnung|rivalen|umsaetze|protokoll>` (die Tafel „Bericht“ rechts auf diesem Reiter)
- `--hilfe=1` (Hilfe zur Steuerung gleich zeigen; ohne Startoptionen erscheint sie beim allerersten Spiel von selbst)
- `--meldung=<Text>` (eine Meldung unten links einstellen, Leerzeichen als `_`; beginnt sie mit ✗ oder ⚠, ist es eine Warnung, die bis zum Klick bleibt)
- `--einstellungen=1`
- `--wegenetz=1` (Gassennetz der Passanten einblenden)
- `--laden=<datei.json>` (aus `~/.local/share/godot/app_userdata/Reichsgilde/spielstaende`)
- `--karte=<kennung>` (neue Partie auf einer anderen Karte aus `game/karten/`, z. B. `koeln`, nur für diesen Start)
- `--schwierigkeit=<Normal|Schwer>` (Schwierigkeit der neuen Partie, nur für diesen Start; Normal = KI-Häuser starten wie der Spieler, Schwer = mit Umlandbetrieb)

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
- `--blitz=1` (mit `--wetter=gewitter`: ein Blitz bleibt auf voller Helligkeit stehen, für Bildschirmfotos)
- `--messe=1` (Kirchen und Dom leuchten, als liefe gerade die Messe; nur Darstellung, für Bildschirmfotos bei Nacht)
- `--tonemapping=<Bisher|Agx>`
- `--helligkeit=`/`--kontrast=`/`--saettigung=<Prozent>`
- `--schaerfe=<0–100>`
- `--fahnen=<An|Aus>`
- `--uigroesse=<75–200>`
- `--farbfilter=<Aus|Protanopie|Deuteranopie|Tritanopie>`
- `--reiter=<Voreinstellung|Leistung|Bild|Stimmung|Barrierefreiheit|Spiel>` (mit `--einstellungen=1`)
- `--automatisch=1` (Voreinstellung messen und wählen wie beim ersten Start; ohne Startoptionen und ohne `einstellungen.cfg` geschieht das im Hauptmenü von selbst)
- `--messung=<Sekunden>` (nach 8 s Vorlauf Bilder/s, 1-%-Tief, Bild- und GPU-Zeit ins Protokoll, dann beenden; für Vergleiche mit `--blick`, `--startstunde` und `--vsync=Aus`)

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

- `--bauen=1` (Baumodus mit Bautafel; `--bauen=wohnhaus:<Stufe 0–4>` oder `--bauen=werkstatt:<Betriebsart>` wählt gleich das Gebäude)
- `--bauort=<x>,<z>,<Drehung 0–3>` (der Umriss sitzt an dieser Stelle der Stadt in Metern, bis die Maus bewegt wird; Probestellen stehen unter `probeplan` in `greybox.json`)
- `--bauklick=1` (dort die Rückfrage öffnen; `=2` gleich bauen)
- `--aufsteigen=1` (Wohnhaus zum Bürgerhaus ausbauen und zum Bürger aufsteigen, damit Werkstätten erlaubt sind; mit `--vermoegen=30000`)
- `--spulen=<Ticks>` (so viele Spielminuten auf einen Schlag ablaufen lassen, `1100` = Bauzeit eines Spieljahres)
- `--leerstand=kaufen|abreissen` (mit `--auswahl=wohnhaus:Haus_Eisenbart`, Leerzeichen als `_`: Rückfrage zum leeren Haus öffnen; mit `--bauklick=2` gleich ausführen)
- `--speichern=<datei.json>` (die Partie nach `--bauklick` und `--spulen` speichern)

Ein leeres Haus entsteht durch Bearbeiten eines Spielstands: mit `--speichern` einen Stand schreiben, in der Datei bei einem KI-Konto `IstBankrott` auf `true` und `WohnhausStufe` setzen, mit `--laden` starten.

Kamera und Tafeln für Bildschirmfotos: `--blick=<x>,<z>,<Zoom>`, `--ui=0`.

Tipp: Mit `XDG_DATA_HOME=<eigener Ordner>` vor dem Aufruf landen die Spielstände des Klicktests nicht im echten Spielstand-Ordner (der Auto-Stand wird bei jedem Jahreswechsel überschrieben).
