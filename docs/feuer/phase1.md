# Feuer und Flammen – Phase 1 und 1b: zwei Techniken zum Vergleich

**Stand:** Branch `phase5/feuer-darstellung` (kein PR), Phase 1b seit dem Abend des 29.10.2026 (Abschnitt [Phase 1b](#phase-1b-deine-wahl-und-neue-flammen-aus-einer-simulation)). **Auftrag des Nutzers (29.10.2026):** Echte, lodernde Flammen überall, wo man sich Feuer vorstellen kann – Fackeln, Laternen, Feuerkörbe, Esse und Herd, Kamine mit Rauch aus den Schornsteinen, Kerzen in Innenräumen, brennende Gebäude – mit flackerndem Licht. Der Nutzer will **beide** Techniken sehen und selbst entscheiden. Diese Phase baut deshalb nur eine **Testszene**; im Spiel brennt noch nichts davon.

## So startest du die Testszene (Windows, Godot-Editor)

Kurz: Branch holen, Godot öffnen und bauen, dann **`Feuertest.tscn` öffnen und F6** – oder `-- --feuertest=1` eintragen und **F5**.

**Schritt 1 – Branch holen (GitHub Desktop)**

1. **Godot vorher schließen.** Sonst behält der Editor die Projekteinstellungen des alten Branches im Speicher und kann sie beim nächsten Speichern über die neuen schreiben.
2. In GitHub Desktop oben auf **„Current Branch“** klicken und `phase5/feuer-darstellung` wählen. Fragt GitHub Desktop, was mit geänderten Dateien passieren soll (etwa `game/project.godot`, weil dort alte Startargumente stehen), nimm **„Leave my changes on …“** – die Argumente trägst du unten neu ein.
3. Oben auf **„Fetch origin“** klicken, und wenn danach **„Pull origin“** erscheint, auch darauf. Im Reiter **„History“** muss der Eintrag „Feuer: --feuertest auch ohne „--“ davor erkennen“ (30.10.2026) zu sehen sein.

**Schritt 2 – Öffnen und bauen**

1. Godot 4.7.2 (.NET) starten und das Projekt öffnen (Ordner `game`). Beim ersten Öffnen importiert Godot ein paar Sekunden lang neue Bilder; den Balken abwarten.
2. Oben rechts auf das **Hammer-Symbol** klicken (Projekt bauen). Unten im Reiter **MSBuild** darf kein roter Fehler stehen; sonst den Fehlertext an Claude geben.

**Schritt 3, Weg A – am einfachsten: die Szene direkt starten (F6)**

1. Links unten im **Dateisystem** den Ordner `scenes` aufklappen und **`Feuertest.tscn`** doppelklicken (die Szene öffnet sich in einem eigenen Reiter).
2. **F6** drücken („aktuelle Szene starten“). Keine Argumente nötig.

**Schritt 3, Weg B – über das Startargument (F5, wie sonst das Spiel)**

1. Menü **Debuggen → Run-Instanzen anpassen…**
2. Ganz oben links ins Feld **„Main-Run Argumente“** genau diese Zeile eintragen (zwei Teile, ein Leerzeichen dazwischen):

   ```
   -- --feuertest=1
   ```

   Der einzelne `--` vorn trennt Godots eigene Argumente von denen des Spiels. Den Haken **„Mehrere Instanzen aktivieren“** nicht setzen.
3. **OK**, dann **F5**. Das Spiel überspringt das Hauptmenü und öffnet sofort die Testszene.
4. **Nach dem Test das Feld wieder leeren**, sonst startet F5 jedes Mal die Testszene. Solange etwas darin steht, zeigt GitHub Desktop `game/project.godot` als geändert (Zeile `run/main_run_args=…`): nicht committen.

Dasselbe Feld gibt es auch unter **Projekt → Projekteinstellungen**, aber nur, wenn oben rechts der Schalter **„Erweiterte Einstellungen“** an ist: dann **Editor → Ausführen → „Laufzeitargumente für Main“**. Ohne den Schalter fehlt es dort.

**Woran du siehst, dass es geklappt hat:** Oben links steht „Feuertest – Vergleich: links A (Shader-Flamme …) rechts B (Partikel-Flamme)“, davor zwei Fackeln an einer Wand bei Nacht; im Reiter **Ausgabe** unten steht eine Zeile `[feuer] start …`. Kommt stattdessen das Hauptmenü, ist das Argument nicht angekommen (falsches Feld, Feld leer, „Mehrere Instanzen“ an) – dann Weg A nehmen.

**Gut zu wissen**

- Seit dem 30.10.2026 erkennt das Spiel `--feuertest=1` und die anderen Schalter, die mit `--feuer` beginnen, auch **ohne** den `--` davor (vorher kam dann stumm das Hauptmenü – vermutlich der Grund, warum es am 29.10. nicht ging). Alle anderen Spieloptionen, etwa `--bildrate=0 --vsync=Aus` für die Messung, gelten nur hinter dem `--`; darum die Zeile am besten immer mit `-- ` beginnen.
- **Vulkan** nimmt das Spiel unter Windows inzwischen von selbst (seit PR #258); `--rendering-driver vulkan` ist nicht mehr nötig. Nur wer in den Einstellungen (Grafik → Erweitert) DirectX 12 gewählt hat, schreibt es vorn dazu: `--rendering-driver vulkan -- --feuertest=1`.
- Weitere Argumente kommen in dieselbe Zeile hinter `--feuertest=1`, mit Leerzeichen getrennt, z. B. `-- --feuertest=1 --feuerpaar=7 --feuertag=1` (Liste unten unter [Weitere Startargumente](#weitere-startargumente-zum-eintragen-in-dasselbe-feld-nach---feuertest1)).

## Die zwei Techniken

| | **A – Shader-Flamme** | **B – Partikel-Flamme** |
|---|---|---|
| Gedacht für | die Grafikstufen Niedrig und Mittel | Hoch und Ultra |
| Wie | je Flammenzunge **eine** Fläche, die der Kamera folgt (Billboard); der Shader `flamme.gdshader` malt eine Tropfenform, die ein wanderndes Rauschen nach oben ausfranst, mit weißgelbem Kern und roten Rändern; Funken im selben Shader. Seit Phase 1b zweite Fassung: das Flipbook der Simulation auf derselben einen Fläche (`flamme_sim.gdshader`) | `GpuParticles3D` mit einem **Flipbook** (seit Phase 1b aus einer Blender-Feuersimulation: `flamme_ruhig.png`, `flamme_wild.png`), dazu eigene Systeme für Funken und Rauch |
| Rauch | eine Säule aus Flächen mit `rauch.gdshader` (grau, am Fuß vom Feuer angeleuchtet, nachts dunkel) | Rauchteilchen (`rauch_flipbook.png`), **beleuchtet**: das Licht des Feuers färbt sie nachts orange |
| Kosten | ein Zeichenaufruf je Zunge, kein Partikelsystem | mehrere Partikelsysteme je Feuer (Flamme, Funken, Rauch) |
| Material | ein Material je Feuerart, der Startwert je Flamme als Instanz-Uniform: alle Fackeln teilen sich eines | ein Material je Art |

**Für beide gleich:** ein `OmniLight3D` ohne Schatten, dessen Energie und Farbe **im Takt der Flamme** flackern (dieselbe Rauschfunktion wie im Shader, dieselbe Uhr `Feuer.Zeit`), mit **Fernausblendung** (DistanceFade). Größe, Farben, Flackern, Funken, Rauch und Licht stehen in einer Ressource `FeuerArt` (`game/scripts/FeuerArt.cs`), also bei beiden Techniken gleich. Fertige Größen: **Kerze** (klein, ruhig), **Fackel**, **Laterne**, **Feuerkorb**, **Esse** (flaches Glutbett mit niedrigen Flammen), **Kamin** (nur Rauch), **Gebäudebrand** (große Flammen, Funken, dicker schwarzer Rauch) und der **Fensterbrand** (Flammen aus einem Fenster, gehört zum Gebäudebrand).

Die Texturen entstehen **ohne fremde Bilder** aus Skripten mit festem Startwert: Rauch, Funke und Rauschen aus `tools/feuer/texturen.py` (nur numpy; Rauschen, das sich nahtlos zur Schleife schließt), die Flammen seit Phase 1b aus einer Blender-Feuersimulation (`tools/feuer/flammen_sim.py`). Wer sie ändern will, ändert das Skript und führt es neu aus.

## Phase 1b: deine Wahl und neue Flammen aus einer Simulation

**Deine Wahl nach dem Test (29.10.2026):** A für Kerze, Laterne und Esse; B für Fackel, Feuerkorb, Kamin und Gebäudebrand. Der Rauch von B gefiel überall besser, die Flamme von B wirkte nicht echt (vor allem beim Brand). Auf der Grafikstufe Niedrig immer A; große Feuer brauchen in A mehr Bewegung und Stichflammen.

**Was Phase 1b gebaut hat (weiter nur in der Testszene):**

| | |
|---|---|
| Neue Flammen | Die Flamme kommt jetzt aus einer **Feuersimulation in Blender** (Mantaflow, `tools/feuer/flammen_sim.py`, fester Startwert): ein Brenner, dessen Brennstoff und Lage leicht schwanken, wird gebacken, jedes Bild von vorn durchgezeichnet und als Bildfolge abgelegt, die sich nahtlos schließt (Anfang und Ende überblendet). Zwei Fassungen: **ruhig** (Fackel, Feuerkorb) und **wild** (Gebäudebrand, Fensterbrand: breites Feuer mit **Stichflammen**, kurzen hohen Stößen). Je 128 Bilder in einem Atlas (`flamme_ruhig.png`, `flamme_wild.png`); der Fuß der Flamme ist heller. |
| B | Jedes Teilchen zeigt die ganze Bildfolge der Simulation, mit eigenem Startbild und zufällig gespiegelt; zwei bis drei überlagern sich und wechseln weich, so fällt die Schleife nicht auf. Funken und Rauch wie bisher. |
| A, zwei Fassungen | **Simulation** (neu): dieselbe Bildfolge auf **einer** Fläche je Zunge, das Bild wählt der Shader aus der Uhr; zwei Nachbarbilder werden überblendet. So billig wie bisher (eine Fläche, ein Zeichenaufruf je Zunge, keine Partikel). **Rauschen** (die Flamme aus Phase 1, umschaltbar): für große Feuer jetzt mit mehr Bewegung (oben teilen sich Zungen, das Feuer pumpt), **Stichflammen** (ab und zu schießt eine Zunge kurz hoch, wird oben schmal und heller, das Licht flackert mit) und einem Feuerbett aus breiten, niedrigen Flammen, das die Zungen verbindet. Kerze, Laterne und Esse bleiben in beiden Fassungen bei der Rauschen-Flamme (eine Kerzenflamme ist kein lodernder Brand, und die niedrigen Flammen über der Glut wurden mit dem Flipbook nur ein heller Fleck im Bloom). |
| Rauch | Ab Mittel bei allen Feuern der Rauch von B, auch wo die Flamme A ist; auf Niedrig der Rauch von A. |
| Zuordnung | als Daten in `FeuerArt` (`TechnikAbMittel`, `FlammeFuer`, `RauchFuer`): Niedrig alles A, ab Mittel deine Tabelle. Mittel und Hoch unterscheiden sich bei den Flammen nicht; im Spiel gibt es auf Hoch später mehr echte Lichter. |

### Neu in der Testszene: Taste `G` und Taste `F`

- **`G` – Ansicht:** Vergleich → Niedrig → Mittel → Hoch → Vergleich. Im **Vergleich** steht wie bisher links A, rechts B (der Rauch ist jetzt auf beiden Seiten B, damit du nur die Flammen vergleichst). In **Niedrig**, **Mittel** und **Hoch** steht jede Feuerart **einmal in der Mitte**, so wie das Spiel sie auf dieser Grafikstufe zeichnen wird; die Beschriftung sagt, welche Flamme und welcher Rauch es ist.
- **`F` – Fassung von A:** Simulation ↔ Rauschen (die alte A-Flamme). Gilt im Vergleich für die linke Seite und in den Grafikstufen für alle A-Flammen.
- Startargumente dazu: `--feuerstufe=vergleich|niedrig|mittel|hoch` und `--feuerfassung=sim|rauschen`.

### Worauf du in Phase 1b achten sollst

1. **Gebäudebrand (Taste `7`):** Wirkt die neue Flamme (B rechts, A-Simulation links) wie echtes Feuer? Siehst du die Stichflammen (ab und zu schießt eine Zunge hoch)? Bitte einige Sekunden zusehen, Standbilder zeigen das nicht.
2. **Fackel und Feuerkorb (`2`, `4`):** Ist die ruhige Flamme glaubwürdig, flackert sie schnell genug? Fällt dir die Schleife (alle gut 4 Sekunden dieselbe Bewegung) auf?
3. **A-Simulation gegen A-Rauschen (`F`):** Welche Fassung soll A im Spiel haben? Beide kosten gleich viel.
4. **Niedrig (`G` einmal):** Gebäudebrand mit A – genug Bewegung, gefallen dir die Stichflammen (in der Fassung Rauschen deutlich sichtbar)?
5. **Mittel/Hoch (`G` zwei- oder dreimal):** Ist die Zuordnung so, wie du sie wolltest (Kerze, Laterne, Esse A mit B-Rauch; der Rest B)?
6. **Glut am Fuß:** Ist der Fuß der Flamme hell genug?

**Zurückmelden:** Fassung von A (Simulation oder Rauschen), ob die neuen Flammen von B jetzt echt genug sind (sonst was stört: Farbe, Form, Tempo, Größe), und die Bildrate von `--feuerfackeln=50` mit `--feuerart=A --feuerfassung=sim`, `--feuerart=A --feuerfassung=rauschen` und `--feuerart=B` (jeweils mit `--feuerlichter=12 --bildrate=0 --vsync=Aus`).

### Messung Phase 1b (NVIDIA T400, Vulkan, 1600 × 900)

`tools/feuer/messen.sh` mit `--feuerfackeln=50 --feuerzyklus=6 --bildrate=0 --vsync=Aus`, 4 Durchläufe (je Zustand 15–16 Messwerte), Branch-Stand `31a5b0ed` (main bis `e343cb9e` eingemischt). Der Rechner war stark belastet (`uptime` 10,5 → 17,3 auf 8 Kernen, andere Godot- und Blender-Läufe): Die Bildraten schwanken stark; verlässlich ist das untere Zehntel der GPU-Zeit und die Zahl der Zeichenaufrufe. Flamme und Rauch je in derselben Technik (A wie auf Niedrig, B wie auf Hoch).

| Zustand (50 Fackeln) | Bilder/s | GPU-Zeit unteres Zehntel / Median (ms) | Hauptprozessor des Zeichners (ms) | Zeichenaufrufe |
|---|---|---|---|---|
| ohne Feuer | 49,0 | 7,38 / 7,52 | 0,82 | 214 |
| **A Rauschen**, 0 / 12 / 50 Lichter | 43,3 / 37,9 / 29,4 | 7,38 / 8,87 / 12,28 | 1,02 / 1,08 / 1,59 | 225 |
| **A Simulation**, 0 / 12 / 50 Lichter | 38,0 / 34,1 / 28,1 | 7,44 / 8,88 / 12,30 | 1,16 / 1,46 / 1,63 | 245 |
| **B (neues Flipbook)**, 0 / 12 / 50 Lichter | 28,1 / 28,3 / 29,4 | 7,73 / 9,22 / 12,87 | 2,91 / 2,92 / 2,10 | 364 |

- **A Simulation kostet auf der Grafikkarte so viel wie A Rauschen** (unteres Zehntel 7,44 gegen 7,38 ms, ohne Feuer 7,38): beide eine Fläche je Zunge; das Flipbook liest zwei Bilder statt dreimal Rauschen. 20 Zeichenaufrufe mehr, weil Flamme und Rauchsäule (beide durchsichtig, nach Tiefe sortiert) sich abwechseln und weniger zusammengefasst werden.
- **B** kostet wie in Phase 1 vor allem Zeichenaufrufe (150 mehr: Flamme, Funken, Rauch je Fackel) und Zeit im Hauptprozessor (hier +2,1 ms, unter der Last unsicher; Phase 1 bei weniger Last +0,6 ms); auf der Grafikkarte +0,35 ms.
- Die **Lichter** bleiben der größte Posten (50 Lichter etwa +5 ms bei allen dreien).
- Die Bildraten (Mittel) sind unter dieser Last kaum zu gebrauchen (Spannen 10–60 Bilder/s); bitte auf deiner Grafikkarte nachmessen.

### Wie die neuen Flammen wirken (Bilder von Claude, Vulkan auf der T400)

- **Fackel:** A (Simulation) und B sehen jetzt beide wie eine echte Fackelflamme aus: weißgelber, heller Fuß, orange Zungen, die sich kräuseln und oben abreißen; über sechs Fotos im Abstand von 0,25 s ändert sich die Gestalt deutlich, das Licht an der Wand flackert mit. B ist etwas unruhiger, A hat etwas größere, weichere Funken.
- **Feuerkorb:** A (Simulation, 4 Zungen) wirkt wie ein Holzfeuer; B ist schmaler und lebendiger. A Rauschen ist ein heller Fleck im Bloom.
- **Gebäudebrand:** B ist jetzt ein großes, lohendes Feuer mit hohen Flammensäulen, Funken und dunklem Rauch; A (Simulation) zeigt einzelne Flammen auf dem Dach (wirkt eher wie mehrere Brandherde). A Rauschen auf Niedrig: Stichflammen gut zu sehen (in zwei von sechs Fotos schießt eine Zunge hoch), aber das Ganze ist sehr hell (viele additive Flächen) und eher „Kerzenreihe plus Feuerbett“.
- **Kerze, Laterne, Esse:** unverändert die Rauschen-Flamme (in beiden Fassungen); B bekommt das ruhige Flipbook.

## Bedienen und weitere Startargumente

Wie du die Testszene startest, steht oben unter [So startest du die Testszene](#so-startest-du-die-testszene-windows-godot-editor).

### Bedienung in der Testszene

| Taste | Wirkung |
|---|---|
| `1` … `7` oder Pfeil links/rechts | Paar wählen: 1 Kerze, 2 Fackel, 3 Laterne, 4 Feuerkorb, 5 Esse, 6 Kamin, 7 Gebäudebrand |
| `V` | Sicht wechseln: **nah** (Perspektive wie die Raumansicht), **Stadt** (Orthokamera wie der nächste Zoom des Spiels), **fern** (wie der weiteste Zoom, alle Paare) |
| `N` | Nacht und Tag wechseln (Nacht ist der Start) |
| `G` | Ansicht: Vergleich (A links, B rechts), Niedrig, Mittel, Hoch (jede Feuerart einmal, wie die Grafikstufe sie zeichnet) |
| `F` | Fassung von A: Simulation (Flipbook) oder Rauschen (die Flamme aus Phase 1) |
| `T` | Technik zeigen: beide, nur A, nur B, keine (nur die Aufbauten, für den Vergleich der Bildrate) |
| `L` | die Lichter der Feuer aus und an |
| `H` | Beschriftung aus und an |
| rechte Maustaste ziehen / Mausrad | drehen / nähern |
| `Esc` | Ende |

**Im Vergleich steht links A (Shader), rechts B (Partikel)**, jede Feuerart als Paar nebeneinander; in den Ansichten der Grafikstufe jede Feuerart einmal. Oben links stehen Sicht, Paar, Tageszeit und die **Bilder pro Sekunde**; im Ausgabefenster des Editors schreibt die Szene jede Sekunde eine Zeile `[feuer] fps=…` mit Bildrate, Bildzeit und Zahl der Zeichenaufrufe.

### Weitere Startargumente (zum Eintragen in dasselbe Feld, nach `--feuertest=1`)

- `--feuersicht=nah|stadt|fern`, `--feuerpaar=1`–`7`, `--feuertag=1` (gleich bei Tag starten)
- `--feuerart=A|B|keins|beide` (nur eine Technik zeigen, `keins` = nur die Aufbauten)
- `--feuerstufe=vergleich|niedrig|mittel|hoch` (Ansicht wie Taste `G`), `--feuerfassung=sim|rauschen` (Fassung von A wie Taste `F`)
- `--feuerlicht=0` (alle Lichter aus)
- **`--feuerfackeln=50`** (statt der Paare 50 Fackeln im Raster, Technik A in der Fassung von `--feuerfassung`, Flamme und Rauch A; mit `--feuerart=B` Technik B mit Rauch B; `--feuerart=keins` nur die Pfähle) und `--feuerlichter=12` (nur 12 Fackeln leuchten mit echtem Licht; im Spiel wird es einen solchen Pool geben)
- für die Messung `--bildrate=0 --vsync=Aus` dazu (sonst deckelt die Bildschirmfrequenz die Zahl)

## Worauf du achten sollst (Phase 1)

1. **Aussehen der Flamme** – wirkt sie wie Feuer? Form, Ausfransen, Farbe (weißgelber Kern, orange, rote Ränder), Größe. A ist eine einzelne, saubere Zunge je Flamme; B überlagert mehrere und sieht dadurch lebendiger und „buschiger“ aus.
2. **Bewegung** – Standbilder zeigen sie nicht: Flackern, Wiegen, Funken, Rauch. Bewegt sich A genug, oder wirkt es mechanisch? Wirken die Flammen in B zu hektisch?
3. **Das Licht** – flackert es angenehm im Takt der Flamme (Wand und Boden um Fackel, Feuerkorb, Esse, Gebäudebrand)? Zu stark, zu schwach, zu orange?
4. **Rauch** – A: eine Säule, die sich mit dem Wind biegt; B: einzelne Wolken, vom Feuer angeleuchtet. Was gefällt dir bei Kamin und Brand (Taste `6` und `7`, nachts und tags)?
5. **Die Sichten** – in der **Stadtsicht** und **fern** sind die Flammen klein: Reicht das, um Feuer als Feuer zu erkennen? Fällt in B ein Unterschied zu A auf, der den Mehraufwand lohnt?
6. **Kerze und Laterne** (nah): Ist die kleine Flamme überzeugend und ruhig?
7. **Bildrate** – oben links und im Ausgabefenster; besonders bei `--feuerfackeln=50` und beim Gebäudebrand nah.

## Was du zurückmelden sollst (Phase 1, erledigt)

- **Je Größe** (Kerze, Fackel, Laterne, Feuerkorb, Esse, Kamin, Gebäudebrand): A, B oder „egal“, und was dir an der Verlierer-Technik nicht gefällt.
- **Fürs Spiel:** Soll A für Niedrig und Mittel und B für Hoch und Ultra gelten (wie geplant), oder nur eine der beiden für alles? Oder etwas ganz anderes?
- **Bildrate deiner Grafikkarte** (Typ dazu) für: Gebäudebrand nah (Taste `7`, Sicht nah), `--feuerfackeln=50` mit A und mit B, jeweils mit `--feuerlichter=12`.
- **Wünsche:** Farben (zu rot, zu gelb), Größen, Funken (zu viele, zu wenige), Rauchfarbe, Lichtstärke.
- **Fehler:** alles, was im Ausgabefenster rot erscheint, und Bilder, auf denen etwas falsch aussieht (Flächen mit sichtbarem Rand, Flammen, die in der Luft hängen).

## Messwerte Phase 1 (NVIDIA T400, Vulkan, 1600 × 900)

Gemessen mit `tools/feuer/messen.sh` und `--feuerzyklus=6`: Die Szene schaltet im selben Lauf von selbst durch alle Zustände (ohne Feuer, A, B, im Fackelraster mit 0, 12 und 50 Lichtern), jeder Zustand 6 Sekunden, mehrere Durchläufe; die Zeiten stammen aus `ViewportGetMeasuredRenderTime*` (Zeichner auf der Grafikkarte und im Hauptprozessor, ohne das Anzeigen im Fenster). Nacht, Bloom und Tonemapping wie im Spiel, `--bildrate=0 --vsync=Aus`, Commit `c5365d0f`.

**Vorsicht beim Lesen:** Die T400 teilt sich der Rechner mit anderen Arbeitsläufen (Rechnerlast 4 bis 28 auf 8 Kernen, andere Godot-Prozesse auf derselben Grafikkarte). Fremde Last macht jedes Bild nur **länger**; deshalb gilt das **untere Zehntel** der GPU-Zeit (ungestörte Zeit) als Schätzwert, Mittel und Median zeigen die Störung. Die Bildrate (Mittel) schwankt stark mit der Last. Absolute Zahlen gelten nur für diesen Rechner; verlässlich ist die Reihenfolge, die in drei Reihen (die erste bei Last 17 bis 28, die letzte bei Last 4 bis 10) dieselbe blieb. Auf deiner Grafikkarte kommt etwas anderes heraus: Miss mit `--feuerfackeln=50`.

**50 Fackeln im Raster** (Sicht Stadt, Orthokamera Größe 25; Rechnerlast `uptime` 9,6 → 4,1):

| Zustand | Bilder/s | GPU-Zeit unteres Zehntel / Median (ms) | Hauptprozessor des Zeichners (ms) | Zeichenaufrufe |
|---|---|---|---|---|
| ohne Feuer (nur Pfähle) | 44,7 | 7,6 / 14,1 | 0,68 | 214 |
| **A**, 0 Lichter | 43,2 | 7,6 / 13,5 | 0,77 | 225 |
| **A**, 12 Lichter | 40,5 | 9,2 / 16,5 | 0,74 | 225 |
| **A**, 50 Lichter | 32,2 | 12,8 / 23,5 | 0,79 | 225 |
| **B**, 0 Lichter | 34,2 | 7,6 / 16,6 | 1,30 | 364 |
| **B**, 12 Lichter | 32,5 | 9,2 / 19,5 | 1,26 | 364 |
| **B**, 50 Lichter | 25,8 | 13,0 / 28,2 | 1,35 | 364 |

**Gebäudebrand nah** (Perspektive; Last 4,5 → 4,0) und **alle sieben Paare fern** (14 Feuer, Orthokamera Größe 95; Last 4,1 → 4,8):

| Zustand | Brand nah: Bilder/s | GPU-Zeit unteres Zehntel / Median (ms) | Aufrufe | Paare fern: Bilder/s | GPU-Zeit unteres Zehntel / Median (ms) | Aufrufe |
|---|---|---|---|---|---|---|
| ohne Feuer | 51,4 | 6,7 / 8,8 | 42 | 36,1 | 7,7 / 15,3 | 176 |
| nur A | 46,6 | 7,8 / 10,1 | 56 | 38,3 | 8,1 / 14,7 | 196 |
| nur B | 38,9 | 9,2 / 14,4 | 51 | 35,9 | 8,2 / 16,6 | 198 |
| A und B zugleich | 39,4 | 10,0 / 15,0 | 65 | 35,7 | 8,4 / 17,0 | 223 |

**Was daraus folgt (Schätzung für 50 Fackeln gleichzeitig):**

- Die **Flammen selbst** sind bei beiden billig: In der Stadtsicht ist auf der Grafikkarte kein Unterschied zu „ohne Feuer“ messbar. Technik A kostet 11 zusätzliche Zeichenaufrufe (alle Fackeln teilen sich Mesh und Material) und etwa 0,1 ms im Hauptprozessor, Technik B 150 (je Fackel drei Partikelsysteme) und etwa 0,6 ms; das zeigt sich als etwa **−3 % Bilder/s bei A gegen −23 % bei B** (Fackelraster ohne Lichter).
- Die **Lichter** kosten mehr als die Flammen und bei A und B gleich viel: 12 Lichter etwa +1,6 ms, 50 Lichter etwa +5,2 ms auf der Grafikkarte (rund **−25 % Bilder/s bei A**). Darum gehört ein Pool von wenigen echten Lichtern in den Einbau (wie bei den Nachtlichtern: Hoch 12, Mittel 6), alle übrigen Feuer leuchten nur über ihren Schein.
- **Mit 12 echten Lichtern** (der geplante Pool für Hoch): 50 Fackeln mit A etwa **−9 %** (44,7 → 40,5 Bilder/s), mit B etwa **−27 %** (→ 32,5).
- **Gebäudebrand nah** ist die teuerste Szene: A +1,1 ms, B +2,5 ms (Median +1,3 und +5,6 ms) auf der Grafikkarte, −9 % gegen −24 % Bilder/s. Der Mehraufwand von B steckt im beleuchteten Rauch (60 Teilchen, viel Überdeckung, jedes Licht rechnet mit).
- **Fern** (14 Feuer) liegen A und B innerhalb der Messstreuung bei „ohne Feuer“.
- Auf einer stärkeren Grafikkarte als der T400 dürfte der Abstand zwischen A und B kleiner sein; die Zahl der Zeichenaufrufe und der Partikelsysteme bleibt der Preis von B.

### Wie die beiden in Phase 1 wirkten (Bilder von Claude aus dem Klicktest, Vulkan auf der T400)

Die Bilder (Nacht und Tag, nah, Stadt, fern) liegen im Bilderordner des Laufs (siehe Bericht); Standbilder zeigen keine Bewegung, darum zusätzlich Fotoreihen.

- **Flackern belegt:** Acht Fotos der Fackel im Abstand von je ein bis zwei Sekunden: Die Zahl der hellen Bildpunkte (Helligkeit über 200 von 255) im Ausschnitt der Flamme schwankt bei A zwischen 2.986 und 3.646 (etwa ±10 %), bei B zwischen 1.223 und 2.610; die Helligkeit der Wand daneben (Mittel aus 8 × 8 Bildpunkten) schwankt zwischen 87 und 113 von 255 (das Licht flackert). Beim Feuerkorb schwanken die hellen Bildpunkte bei A zwischen 10.094 und 13.490, bei B zwischen 11.047 und 16.393. Auf den Kontaktbögen sieht man, dass A seine Höhe und Spitze ändert (eine saubere Zunge), B sich in der Gestalt ändert (mehrere Zungen verschmelzen und teilen sich).
- **A, Shader:** schlanke, saubere Tropfenflammen mit weißgelbem Kern, die an den Rändern ausfransen; die Funken steigen als kleine Punkte auf, der Rauchfaden ist eine Säule, die sich bei Wind biegt. Gut bei Kerze, Fackel und Laterne (dort kaum von B zu unterscheiden). Beim Feuerkorb und beim Gebäudebrand wirken die Zungen einzeln und etwas „kerzenartig“, weniger wild; der Rauch ist eine gleichmäßige Säule, die bei dunklem Ruß (Brand) gut aussieht, beim hellen Kamin aber wie ein Dampfband.
- **B, Partikel:** mehrere überlagerte, ständig wechselnde Zungen: buschiger, unruhiger und lebendiger (Feuerkorb, Esse, vor allem der Brand mit Funken und dunkler Rauchwolke, die das Licht des Feuers nachts rötet). Die Rauchwolken sind einzelne weiche Wolken, nachts ohne Licht (Kamin) fast unsichtbar.
- **Kerze (nah):** beide ruhig und überzeugend, B etwas kleiner und mit mehr Struktur. **Esse:** das Glutbett (gemeinsam, `glut.gdshader`) ist neben dem Bloom nur schwer zu sehen; die niedrigen Flammen wirken in B lebendiger.
- **Stadt und fern:** Die Flammen sind wenige Bildpunkte groß; beide sind als flackernde Lichtpunkte mit Rauchfaden erkennbar, einen Unterschied zwischen A und B sieht man hier kaum (Gebäudebrand ausgenommen: Flammen und Rauch sind größer als ein Haus).
- **Tag:** Beide Flammen sind bei Sonne gut zu sehen; der Schein auf der Wand ist schwächer. Der Rauch von A ist bei Tag hell und fast weiß (bei Ruß dunkelgrau), der von B grau und fleckiger.

## Was noch nicht gebaut ist (Phase 2, nach deiner Entscheidung)

- Einbau in das Spiel: Fackeln und Feuerkörbe an den Ankern der Modelle (`Anker_Feuer_<n>`, `Anker_Laterne`), Kerzen und Herdfeuer in den Innenräumen, Kamine und Schornsteine der Häuser, Gebäudebrand als Ereignis im Spiel.
- Die Grafikstufe der Feuer als Einstellung (die Zuordnung steht schon in `FeuerArt`, Phase 1b), die Zahl der echten Lichter (Pool nach Stufe, wie bei den Nachtlichtern) und die Fernausblendung nach dem Zoom (die Stadtkamera ist eine Orthokamera in 150 m Abstand: `Feuer.AusblendenZuschlag`).
- Wind: Die Partikel lesen den Wind aus `Feuer.Wind`, die Shader aus den globalen Uniforms; das Spiel müsste `Feuer.Wind` nach dem Wetter setzen.
- Für viele Feuer: ein `MultiMesh` je Art statt einer Fläche je Zunge, falls die Zahl der Zeichenaufrufe zählt.

## Dateien

| Datei | Inhalt |
|---|---|
| `game/assets/shader/flamme.gdshader`, `rauch.gdshader`, `glut.gdshader` | Technik A: Flamme in der Fassung Rauschen (mit Funken, seit 1b mit Bewegung und Stichflammen), Rauchsäule, Glutbett (auch bei B für die Esse) |
| `game/assets/shader/flamme_sim.gdshader` | das Flipbook der Simulation: als Fläche (A, Fassung Simulation) und für die Teilchen von B |
| `game/assets/texturen/feuer/flamme_ruhig.png`, `flamme_wild.png` (+ `.json`) | die Flipbooks der Simulation (16 × 8 Bilder zu 128 × 256 Pixeln, vormultiplizierte Farbe) und ihre Kennwerte, aus `tools/feuer/flammen_sim.py` |
| `game/assets/texturen/feuer/*.png` (übrige) | `rauch_flipbook` (4 × 4), `funke`, `rauschen` (kachelbar), aus `tools/feuer/texturen.py` |
| `game/scripts/FeuerArt.cs` | die Ressource mit allen Werten, die sieben Größen und die Zuordnung zur Grafikstufe |
| `game/scripts/Feuer.cs` | der Knoten: baut je nach Technik Flächen oder Partikel, Rauch in eigener Technik, Licht, gemeinsame Uhr |
| `game/scenes/feuer/FeuerShader.tscn`, `FeuerPartikel.tscn` | fertige Szenen zum Einbauen (Vorgabe im Inspektor wählen) |
| `game/scenes/Feuertest.tscn`, `game/scripts/Feuertest.cs` | die Testszene |
| `tools/feuer/messen.sh` | die Messung der Bildrate (Vulkan, Xvfb) |
| `tools/feuer/flammen_sim.py` | die Blender-Feuersimulation (Mantaflow) und das Packen der Flipbooks; Aufruf im Kopf der Datei, ein Lauf dauert auf der NAS etwa eine halbe Stunde je Fassung |
