# Feuer und Flammen – Phase 1: zwei Techniken zum Vergleich

**Stand:** Branch `phase5/feuer-darstellung` (kein PR). **Auftrag des Nutzers (29.10.2026):** Echte, lodernde Flammen überall, wo man sich Feuer vorstellen kann – Fackeln, Laternen, Feuerkörbe, Esse und Herd, Kamine mit Rauch aus den Schornsteinen, Kerzen in Innenräumen, brennende Gebäude – mit flackerndem Licht. Der Nutzer will **beide** Techniken sehen und selbst entscheiden. Diese Phase baut deshalb nur eine **Testszene**; im Spiel brennt noch nichts davon.

## Die zwei Techniken

| | **A – Shader-Flamme** | **B – Partikel-Flamme** |
|---|---|---|
| Gedacht für | die Grafikstufen Niedrig und Mittel | Hoch und Ultra |
| Wie | je Flammenzunge **eine** Fläche, die der Kamera folgt (Billboard); der Shader `flamme.gdshader` malt eine Tropfenform, die ein wanderndes Rauschen nach oben ausfranst, mit weißgelbem Kern und roten Rändern; Funken im selben Shader | `GpuParticles3D` mit einem **Flipbook** (`flamme_flipbook.png`: 32 Bilder einer flackernden Flamme, selbst erzeugt), dazu eigene Systeme für Funken und Rauch |
| Rauch | eine Säule aus Flächen mit `rauch.gdshader` (grau, am Fuß vom Feuer angeleuchtet, nachts dunkel) | Rauchteilchen (`rauch_flipbook.png`), **beleuchtet**: das Licht des Feuers färbt sie nachts orange |
| Kosten | ein Zeichenaufruf je Zunge, kein Partikelsystem | mehrere Partikelsysteme je Feuer (Flamme, Funken, Rauch) |
| Material | ein Material je Feuerart, der Startwert je Flamme als Instanz-Uniform: alle Fackeln teilen sich eines | ein Material je Art |

**Für beide gleich:** ein `OmniLight3D` ohne Schatten, dessen Energie und Farbe **im Takt der Flamme** flackern (dieselbe Rauschfunktion wie im Shader, dieselbe Uhr `Feuer.Zeit`), mit **Fernausblendung** (DistanceFade). Größe, Farben, Flackern, Funken, Rauch und Licht stehen in einer Ressource `FeuerArt` (`game/scripts/FeuerArt.cs`), also bei beiden Techniken gleich. Fertige Größen: **Kerze** (klein, ruhig), **Fackel**, **Laterne**, **Feuerkorb**, **Esse** (flaches Glutbett mit niedrigen Flammen), **Kamin** (nur Rauch), **Gebäudebrand** (große Flammen, Funken, dicker schwarzer Rauch) und der **Fensterbrand** (Flammen aus einem Fenster, gehört zum Gebäudebrand).

Die Texturen entstehen **ohne fremde Bilder** aus einem Python-Skript mit festem Startwert (`tools/feuer/texturen.py`, nur numpy): Rauschen, das sich nahtlos zur Schleife schließt, wird zu Flamme, Rauch und Funke. Wer sie ändern will, ändert das Skript und führt es neu aus.

## So testest du auf deinem Windows-PC im Godot-Editor

1. **Branch holen.** Im Ordner des Projekts: `git fetch origin`, dann `git switch phase5/feuer-darstellung` (oder in GitHub Desktop den Branch `phase5/feuer-darstellung` auswählen und „Fetch origin“ drücken).
2. **Projekt in Godot öffnen** (die Version 4.7.2 mit .NET wie sonst) und **bauen**: oben rechts auf den **Hammer** klicken. Der Build muss ohne Fehler durchlaufen. Beim ersten Öffnen importiert Godot ein paar Sekunden lang die neuen Texturen.
3. **Startargumente setzen:** *Projekt → Projekteinstellungen → Editor → Ausführen* (englisch *Editor → Run*), das Feld **„Laufzeitargumente für Main“** (*Main Run Args*). Dort eintragen (eine Zeile):

   ```
   --rendering-driver vulkan -- --feuertest=1
   ```

   Das `--rendering-driver vulkan` sorgt dafür, dass Godot unter Windows wirklich Vulkan nimmt (die Testszene ist darauf abgestimmt). Alles hinter dem einzelnen `--` sind Spielargumente.
4. **F5** drücken. Das Hauptmenü erkennt die Option und springt sofort in die Testszene. (Wer lieber `scenes/Feuertest.tscn` öffnet und **F6** drückt, kommt ohne Umweg hin; die Argumente gelten auch dort.)

### Bedienung in der Testszene

| Taste | Wirkung |
|---|---|
| `1` … `7` oder Pfeil links/rechts | Paar wählen: 1 Kerze, 2 Fackel, 3 Laterne, 4 Feuerkorb, 5 Esse, 6 Kamin, 7 Gebäudebrand |
| `V` | Sicht wechseln: **nah** (Perspektive wie die Raumansicht), **Stadt** (Orthokamera wie der nächste Zoom des Spiels), **fern** (wie der weiteste Zoom, alle Paare) |
| `N` | Nacht und Tag wechseln (Nacht ist der Start) |
| `T` | Technik zeigen: beide, nur A, nur B, keine (nur die Aufbauten, für den Vergleich der Bildrate) |
| `L` | die Lichter der Feuer aus und an |
| `H` | Beschriftung aus und an |
| rechte Maustaste ziehen / Mausrad | drehen / nähern |
| `Esc` | Ende |

**Links steht immer A (Shader), rechts B (Partikel)**, jede Feuerart als Paar nebeneinander. Oben links stehen Sicht, Paar, Tageszeit und die **Bilder pro Sekunde**; im Ausgabefenster des Editors schreibt die Szene jede Sekunde eine Zeile `[feuer] fps=…` mit Bildrate, Bildzeit und Zahl der Zeichenaufrufe.

### Weitere Startargumente (zum Eintragen in dasselbe Feld, nach `--feuertest=1`)

- `--feuersicht=nah|stadt|fern`, `--feuerpaar=1`–`7`, `--feuertag=1` (gleich bei Tag starten)
- `--feuerart=A|B|keins|beide` (nur eine Technik zeigen, `keins` = nur die Aufbauten)
- `--feuerlicht=0` (alle Lichter aus)
- **`--feuerfackeln=50`** (statt der Paare 50 Fackeln im Raster, Technik A; mit `--feuerart=B` Technik B; `--feuerart=keins` nur die Pfähle) und `--feuerlichter=12` (nur 12 Fackeln leuchten mit echtem Licht; im Spiel wird es einen solchen Pool geben)
- für die Messung `--bildrate=0 --vsync=Aus` dazu (sonst deckelt die Bildschirmfrequenz die Zahl)

## Worauf du achten sollst

1. **Aussehen der Flamme** – wirkt sie wie Feuer? Form, Ausfransen, Farbe (weißgelber Kern, orange, rote Ränder), Größe. A ist eine einzelne, saubere Zunge je Flamme; B überlagert mehrere und sieht dadurch lebendiger und „buschiger“ aus.
2. **Bewegung** – Standbilder zeigen sie nicht: Flackern, Wiegen, Funken, Rauch. Bewegt sich A genug, oder wirkt es mechanisch? Wirken die Flammen in B zu hektisch?
3. **Das Licht** – flackert es angenehm im Takt der Flamme (Wand und Boden um Fackel, Feuerkorb, Esse, Gebäudebrand)? Zu stark, zu schwach, zu orange?
4. **Rauch** – A: eine Säule, die sich mit dem Wind biegt; B: einzelne Wolken, vom Feuer angeleuchtet. Was gefällt dir bei Kamin und Brand (Taste `6` und `7`, nachts und tags)?
5. **Die Sichten** – in der **Stadtsicht** und **fern** sind die Flammen klein: Reicht das, um Feuer als Feuer zu erkennen? Fällt in B ein Unterschied zu A auf, der den Mehraufwand lohnt?
6. **Kerze und Laterne** (nah): Ist die kleine Flamme überzeugend und ruhig?
7. **Bildrate** – oben links und im Ausgabefenster; besonders bei `--feuerfackeln=50` und beim Gebäudebrand nah.

## Was du zurückmelden sollst

- **Je Größe** (Kerze, Fackel, Laterne, Feuerkorb, Esse, Kamin, Gebäudebrand): A, B oder „egal“, und was dir an der Verlierer-Technik nicht gefällt.
- **Fürs Spiel:** Soll A für Niedrig und Mittel und B für Hoch und Ultra gelten (wie geplant), oder nur eine der beiden für alles? Oder etwas ganz anderes?
- **Bildrate deiner Grafikkarte** (Typ dazu) für: Gebäudebrand nah (Taste `7`, Sicht nah), `--feuerfackeln=50` mit A und mit B, jeweils mit `--feuerlichter=12`.
- **Wünsche:** Farben (zu rot, zu gelb), Größen, Funken (zu viele, zu wenige), Rauchfarbe, Lichtstärke.
- **Fehler:** alles, was im Ausgabefenster rot erscheint, und Bilder, auf denen etwas falsch aussieht (Flächen mit sichtbarem Rand, Flammen, die in der Luft hängen).

## Messwerte (NVIDIA T400, Vulkan, 1600 × 900)

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

### Wie die beiden wirken (Bilder von Claude aus dem Klicktest, Vulkan auf der T400)

Die Bilder (Nacht und Tag, nah, Stadt, fern) liegen im Bilderordner des Laufs (siehe Bericht); Standbilder zeigen keine Bewegung, darum zusätzlich Fotoreihen.

- **Flackern belegt:** Acht Fotos der Fackel im Abstand von je ein bis zwei Sekunden: Die Zahl der hellen Bildpunkte (Helligkeit über 200 von 255) im Ausschnitt der Flamme schwankt bei A zwischen 2.986 und 3.646 (etwa ±10 %), bei B zwischen 1.223 und 2.610; die Helligkeit der Wand daneben (Mittel aus 8 × 8 Bildpunkten) schwankt zwischen 87 und 113 von 255 (das Licht flackert). Beim Feuerkorb schwanken die hellen Bildpunkte bei A zwischen 10.094 und 13.490, bei B zwischen 11.047 und 16.393. Auf den Kontaktbögen sieht man, dass A seine Höhe und Spitze ändert (eine saubere Zunge), B sich in der Gestalt ändert (mehrere Zungen verschmelzen und teilen sich).
- **A, Shader:** schlanke, saubere Tropfenflammen mit weißgelbem Kern, die an den Rändern ausfransen; die Funken steigen als kleine Punkte auf, der Rauchfaden ist eine Säule, die sich bei Wind biegt. Gut bei Kerze, Fackel und Laterne (dort kaum von B zu unterscheiden). Beim Feuerkorb und beim Gebäudebrand wirken die Zungen einzeln und etwas „kerzenartig“, weniger wild; der Rauch ist eine gleichmäßige Säule, die bei dunklem Ruß (Brand) gut aussieht, beim hellen Kamin aber wie ein Dampfband.
- **B, Partikel:** mehrere überlagerte, ständig wechselnde Zungen: buschiger, unruhiger und lebendiger (Feuerkorb, Esse, vor allem der Brand mit Funken und dunkler Rauchwolke, die das Licht des Feuers nachts rötet). Die Rauchwolken sind einzelne weiche Wolken, nachts ohne Licht (Kamin) fast unsichtbar.
- **Kerze (nah):** beide ruhig und überzeugend, B etwas kleiner und mit mehr Struktur. **Esse:** das Glutbett (gemeinsam, `glut.gdshader`) ist neben dem Bloom nur schwer zu sehen; die niedrigen Flammen wirken in B lebendiger.
- **Stadt und fern:** Die Flammen sind wenige Bildpunkte groß; beide sind als flackernde Lichtpunkte mit Rauchfaden erkennbar, einen Unterschied zwischen A und B sieht man hier kaum (Gebäudebrand ausgenommen: Flammen und Rauch sind größer als ein Haus).
- **Tag:** Beide Flammen sind bei Sonne gut zu sehen; der Schein auf der Wand ist schwächer. Der Rauch von A ist bei Tag hell und fast weiß (bei Ruß dunkelgrau), der von B grau und fleckiger.

## Was noch nicht gebaut ist (Phase 2, nach deiner Entscheidung)

- Einbau in das Spiel: Fackeln und Feuerkörbe an den Ankern der Modelle (`Anker_Feuer_<n>`, `Anker_Laterne`), Kerzen und Herdfeuer in den Innenräumen, Kamine und Schornsteine der Häuser, Gebäudebrand als Ereignis im Spiel.
- Die Wahl der Technik nach der Grafikstufe, die Zahl der echten Lichter (Pool nach Stufe, wie bei den Nachtlichtern) und die Fernausblendung nach dem Zoom (die Stadtkamera ist eine Orthokamera in 150 m Abstand: `Feuer.AusblendenZuschlag`).
- Wind: Die Partikel lesen den Wind aus `Feuer.Wind`, die Shader aus den globalen Uniforms; das Spiel müsste `Feuer.Wind` nach dem Wetter setzen.
- Für viele Feuer: ein `MultiMesh` je Art statt einer Fläche je Zunge, falls die Zahl der Zeichenaufrufe zählt.

## Dateien

| Datei | Inhalt |
|---|---|
| `game/assets/shader/flamme.gdshader`, `rauch.gdshader`, `glut.gdshader` | Technik A: Flamme (mit Funken), Rauchsäule, Glutbett (auch bei B für die Esse) |
| `game/assets/texturen/feuer/*.png` | `flamme_flipbook` (8 × 4 Bilder), `rauch_flipbook` (4 × 4), `funke`, `rauschen` (kachelbar), aus `tools/feuer/texturen.py` |
| `game/scripts/FeuerArt.cs` | die Ressource mit allen Werten und die sieben Größen |
| `game/scripts/Feuer.cs` | der Knoten: baut je nach Technik Flächen oder Partikel, Licht, gemeinsame Uhr |
| `game/scenes/feuer/FeuerShader.tscn`, `FeuerPartikel.tscn` | fertige Szenen zum Einbauen (Vorgabe im Inspektor wählen) |
| `game/scenes/Feuertest.tscn`, `game/scripts/Feuertest.cs` | die Testszene |
| `tools/feuer/messen.sh` | die Messung der Bildrate (Vulkan, Xvfb) |
