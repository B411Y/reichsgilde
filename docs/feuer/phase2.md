# Feuer und Flammen – Phase 2: Einbau ins Spiel

**Stand:** Branch `phase5/feuer-darstellung` (kein PR), 30.10.2026. Vorgeschichte, Techniken und Messungen der Testszene: [phase1.md](phase1.md).

**Deine Entscheidung nach dem zweiten Feuertest (30.10.2026):**

- **Überall Technik A in der Fassung „Simulation“**: Jedes Feuer – Kerze, Fackel, Laterne, Feuerkorb, Esse, Kamin und Gebäudebrand (der Brand mit dem wilden Flipbook) – zeigt das Flipbook der Blender-Simulation auf einer Fläche je Flammenzunge.
- **Rauch: der Shader-Rauch von A** (so wie er auf „Niedrig“ aussah). Der Partikel-Rauch von B wirkte beim Hochstellen unecht.
- **Keine Grafikstufen fürs Feuer**: eine Fassung für alle Stufen.

Darum sind die Partikel-Technik B (Flamme, Funken, Rauch), die Fassung „Rauschen“ und die Umschaltung nach Grafikstufe ganz entfernt (Code, Szene `FeuerPartikel.tscn`, `flamme.gdshader`, `rauch_flipbook.png`, `funke.png`). Funken gibt es weiter, gerechnet im Flammen-Shader.

## Was jetzt im Spiel brennt

| Wo | Was du siehst | Wann |
|---|---|---|
| **Fackeln** der Passanten (jeder Dritte im Dunkeln) | echte Flamme mit Funken auf dem Pechkopf, ohne Rauchsäule (sie zöge starr mit der Figur mit) | nachts |
| **Handlaternen** (Passanten, Nachtwächter auf Streife) und **Wandlaternen** (Wirtshaus, Rathaus) | ein kleines Flämmchen hinter der Hornscheibe; die Scheibe ist jetzt durchscheinend und glüht von innen | nachts |
| **Feuerkörbe** (Torburgen, Schilderhäuschen, Wachturm) | echte Flamme mit Funken und Rauch; das alte, modellierte Flammenstück ist weg | nachts (bei Tag steht der Korb kalt) |
| **Kaminrauch** aus den Schornsteinen der Wohnhäuser, Kulissenhäuser, Spielhäuser und Werkstätten | eine Rauchsäule, die sich im Wind biegt; nachts ein mattes, kühles Grau (im Mondlicht sichtbar, nicht mehr schwarz) | Wohnhäuser: Winter den ganzen Tag, Frühling und Herbst morgens 5–9 und abends 16:30–22 Uhr, Sommer nur zum Kochen (5:30–7:30, 18–20 Uhr); jedes Haus mit eigenem Versatz, nicht jedes macht mit. Werkstätten: solange gearbeitet wird. Der Rauch blendet in etwa vier Sekunden ein und aus |
| **Esse** der Schmiede (außen) | Rauch aus dem Essenschornstein, solange gearbeitet wird (die Glut oben im Schornstein leuchtet wie bisher) | bei der Arbeit |
| **Esse** der Schmiede (Raumansicht) | niedrige Flammen mit Funken über der Glut der Esse (auch über der zweiten Esse ab Stufe 2) | bei der Arbeit |
| **Kerzen** in den Raumansichten (Wirtshaus, Wache, Diebesversteck, Kirche, Geldleihe) | eine echte Kerzenflamme auf jeder Kerze, die im Modell eine Flamme hat | wenn die Glut des Raums glüht (bei der Arbeit, im Diebesversteck und in der Wache auch, wenn jemand wach ist) |
| **Gebäudebrand** | große Flammen aus dem Dach mit Funken und Licht, Flammen aus bis zu drei Fenstern (vorn und unten zuerst), dicke dunkle Rauchsäule; nach 120 Spielminuten (bei Tempo 1 zwei echte Minuten) schwelt es nur noch und raucht 180 Minuten aus; das Dach bleibt bis zur Reparatur rußig | sobald der Kern ein Feuer meldet: Anschlag auf eine Werkstatt (Gauner, Schutzgeld) oder Brandstiftung am Wohnhaus eines Spielhauses |
| **Lichtflecken** | der billige Schein um jede Flamme fällt jetzt wie ein Licht auf Boden, Stufen und Mauern, ohne harten Rand; wo ein echtes Licht steht, ist er ganz aus | nachts |

Die echten Lichter (Hoch 12, Mittel 6, Niedrig keines, wie bisher) flackern jetzt **im Takt der Flamme**: Ihre Stärke folgt der Helligkeit des Bildes, das die Flamme gerade zeigt – derselbe Wert wie in der Testszene. Der Gebäudebrand hat ein eigenes, großes Licht (auch auf Niedrig).

## So probierst du es aus (Windows, Godot-Editor)

**Vorbereitung** wie in Phase 1: Godot schließen, in GitHub Desktop den Branch `phase5/feuer-darstellung` wählen, „Fetch origin“ und „Pull origin“, Godot öffnen, Hammer-Symbol (bauen). Dann **Debuggen → Run-Instanzen anpassen…** und die Zeile unten ins Feld **„Main-Run Argumente“** eintragen – immer mit dem einzelnen ` -- ` davor –, **OK**, **F5**. Danach das Feld wieder leeren.

| Was | Zeile für „Main-Run Argumente“ |
|---|---|
| Köln bei Nacht (Torburg mit Feuerkorb, Passanten mit Fackel und Laterne) | `-- --karte=koeln --startstunde=21` |
| Köln nachts, feste Uhrzeit für das Licht (das Spiel läuft weiter) | `-- --karte=koeln --startstunde=20 --lichtuhr=22` |
| Kaminrauch am Morgen | `-- --karte=koeln --startstunde=6` |
| Kaminrauch den ganzen Tag (Winter) | `-- --karte=koeln --startstunde=10 --jahreszeit=winter` |
| Brand der eigenen Werkstatt (nur Darstellung) | `-- --karte=koeln --startstunde=20 --lichtuhr=22 --brand=werkstatt` |
| Brand von Werkstatt und Wohnhaus zugleich | `-- --karte=koeln --startstunde=20 --lichtuhr=22 --brand=werkstatt,haus` |
| Ein echtes Feuer des Kerns | `-- --karte=koeln --startstunde=20 --entwickler=1 --entwicklerfenster=2`, dann im Entwicklerfenster „Feuer im Betrieb“ (Betrieb vorher wählen) oder „Hausbrand“ |
| Esse in der Schmiede (Raumansicht) | `-- --startstunde=10 --innen=1` |
| Kerzen in der Kirche des Predigers | `-- --beruf=Prediger --startstunde=10 --innen=1` |
| Kerzen in der Wachstube | `-- --beruf=Gardist --startstunde=21 --innen=1` |
| Die Testszene mit allen sieben Feuerarten | `-- --feuertest=1` (oder `Feuertest.tscn` öffnen und **F6**) |

Die eigene Werkstatt in Köln steht an der Hohen Straße im Norden; ohne `--blick` fährst du mit der Kamera hin (oder `--blick=-24,-92,35` dazuschreiben).

### Die Testszene

Seit Phase 2 stehen die sieben Feuerarten **nebeneinander** (von links Kerze, Fackel, Laterne, Feuerkorb, Esse, Kamin, Gebäudebrand), jede genau so, wie das Spiel sie zeichnet. Keine Vergleichsseite und keine Grafikstufen mehr.

| Taste | Wirkung |
|---|---|
| `1` … `7` oder Pfeil links/rechts | Feuerart wählen und die Kamera davor setzen |
| `V` | Sicht: nah, Stadt, fern |
| `N` | Nacht und Tag |
| `T` | Feuer an und aus (für den Vergleich der Bildrate) |
| `L` | Lichter der Feuer an und aus |
| `B` | Beschriftung an und aus |
| `Esc` | Ende |

Die freie Kamera wie bisher: rechte Maustaste halten = umsehen, `W` `A` `S` `D` = fahren, `Q`/`E` = runter/hoch, `Shift` = schneller, Mausrad = Tempo, `H` = ihre Hilfe. Startargumente: `--feuerpaar=1–7`, `--feuersicht=nah|stadt|fern`, `--feuertag=1`, `--feuerzeigen=0`, `--feuerlicht=0`, für Messungen `--feuerfackeln=50 --feuerlichter=12` (Liste in `docs/STARTOPTIONEN.md`). Entfallen sind `--feuerart`, `--feuerstufe` und `--feuerfassung`.

### Worauf du achten sollst

1. **Feuerkörbe an den Torburgen** nachts: Wirkt die Flamme im Korb echt, und passt ihre Größe zum Korb?
2. **Lichtschein** um Tore und Laternen: Ist der harte Kreis weg? Ist der Schein zu stark oder zu schwach?
3. **Kaminrauch**: Gefällt dir, wann geheizt wird (Tabelle oben), und ist die Säule zu dünn, zu hell oder zu hoch? Nachts: Sieht man ihn genug?
4. **Gebäudebrand**: Ist das Feuer groß genug für ein Haus, sitzen die Fensterflammen an den Fenstern?
5. **Esse und Kerzen** in den Raumansichten.

## Kosten (NVIDIA T400, Köln nachts)

Gemessen mit `--karte=koeln --startstunde=19 --lichtuhr=22 --voreinstellung=Hoch --messung=30 --vsync=Aus --bildrate=0 --ui=0`, Vulkan, 1600 × 900, abwechselnd **vorher** (`main` 075a849e, eigener Worktree) und **nachher** (dieser Branch), je zwei Läufe, an zwei Stellen: Hahnentor (`--blick=-120,0,40`, Torburg mit zwei Feuerkörben, Passanten mit Fackeln) und Stadtmitte (`--blick=-30,0,70`, viele Passanten mit Licht, etwa 20 rauchende Schornsteine). Der Rechner war stark belastet (`uptime` 11–13 auf 8 Kernen, ein anderes Mitglied nutzte dieselbe Grafikkarte für Wasser-Läufe): Einzelne Läufe derselben Fassung lagen bis 40 ms GPU-Zeit auseinander. Verlässlich sind nur die Zeichenaufrufe und die kleinste GPU-Zeit je Lauf (Sekunden ohne fremde Last).

| Stelle | Fassung | Bilder/s (Lauf 1, 2) | GPU-Zeit Mittel (ms) | GPU-Zeit kleinste Sekunde (ms) | Zeichenaufrufe | echte Lichter |
|---|---|---|---|---|---|---|
| Hahnentor, Zoom 40 | vorher | 8,9 · 7,8 | 58,4 · 76,2 | 34,3 · 51,6 | 746 · 742 | 7 |
| Hahnentor, Zoom 40 | nachher | 8,0 · 10,2 | 78,0 · 36,0 | 37,1 · 34,4 | 709 · 742 | 7 |
| Mitte, Zoom 70 | vorher | 9,0 · 9,3 | 43,3 · 40,4 | 35,1 · 34,0 | 1724 · 1752 | 12 · 11 |
| Mitte, Zoom 70 | nachher | 8,4 · 8,4 | 57,2 · 60,1 | 45,1 · 34,2 | 1860 · 1906 | 11 · 12 |

- **Zeichenaufrufe:** am Tor gleich, in der Mitte etwa **+150** (Rauchsäulen und Flammen sind durchsichtig und werden nach Tiefe sortiert, darum fasst Godot sie nur teilweise zusammen).
- **Bilder/s:** in der Mitte etwa −0,7 (−8 %), am Tor im Rauschen. Die Bildzeit von rund 100 ms ist auf diesem Rechner vom Hauptprozessor bestimmt (Last), nicht vom Feuer.
- **GPU-Zeit:** Die kleinsten Werte liegen vorher und nachher gleich (34 ms); die Mittel schwanken mit der fremden Last stärker als jeder Unterschied. Der erste Lauf „nachher“ am Tor hatte noch den größeren, helleren Ersatzlichtschein (danach verkleinert).
- **Echte Lichter:** unverändert (Pool Hoch 12); die Flammen haben kein eigenes Licht, nur der Gebäudebrand.
- Zum Nachmessen lassen sich die Teile über Umgebungsvariablen weglassen: `RG_OHNEFLAMMEN=1` (Flammen der Nachtlichter), `RG_OHNERAUCH=1` (Kaminrauch), `RG_OHNEFLECKEN=1` (Ersatzlicht und Heiligenschein).

## Wie es gebaut ist

| Datei | Inhalt |
|---|---|
| `game/scripts/Feuer.cs`, `FeuerArt.cs` | das Feuer in der einen Fassung: Flamme aus dem Flipbook, Rauch, Glut, Licht; `Lichtwert()` für Lichter, die andere stellen (Pool), `RauchStaerke` (Kamin an/aus), `FlammeAn` (Schwelen), `UhrStellen` (gemeinsame Uhr, auch ohne Licht) |
| `game/scripts/Stadtansicht.Feuer.cs` | Schornsteine finden, Kaminrauch nach Tageszeit und Arbeit, Gebäudebrand, Kerzen und Esse in den Räumen |
| `game/scripts/Stadtansicht.Nachtlichter.cs` | Flammen an Fackeln, Laternen, Feuerkörben; das Flackern der echten Lichter nach der Flamme |
| `game/scripts/Stadtansicht.Lichtflecken.cs`, `game/assets/shader/lichtfleck.gdshader` | das Ersatzlicht ohne Rand |
| `game/assets/shader/flamme_sim.gdshader`, `rauch.gdshader`, `glut.gdshader`, `laternenglas.gdshader` | Flamme, Rauch (mit Nachtfarbe und Stärke je Säule), Glut, durchscheinende Laternenscheibe |
| `game/scripts/Feuertest.cs` | die Testszene |
