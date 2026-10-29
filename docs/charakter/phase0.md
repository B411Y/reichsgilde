# Charakter-Editor, Phase 0a: Vortests der Köpfe

Stand 29.10.2026, Branch `versuch/charakter-phase0` (von `main` 5d39ca79), Mitglied darstellung. Kein Spielcode, kein PR.

> Die Skripte und Bögen dieses Berichts (`mpfb_kopf.py`, `ict_kopf.py`, `angaben/`, `boegen/`, `alle_koepfe.sh`,
> `messreihe.sh`) liegen nur auf dem Branch `versuch/charakter-phase0` (53c1de67). In Phase 2 ([phase2.md](phase2.md)) übernommen:
> `kopf_gemeinsam.py`, `blender.sh`, `einrichten.*`, `schau.sh`, `pruefen.sh`, `warte_last.py`, `glb_info.py`, die
> Bühne und die Shader für Haut und Auge.

Frage: Womit bauen wir die Köpfe und Büsten des Charakter-Editors (realistisch-gemalt wie BG3/CK3, sechs Völker,
Alter 5–70, später live animiert, Vererbung über Gene)? Verglichen wurden

- **Weg A**: MakeHuman-Kopf aus MPFB 2 (Assets CC0, MPFB-Code GPL nur als Werkzeug in Blender),
  **A1** wie geliefert, **A2** mit eigenem 2K-Kopfausschnitt, Hautmasken und eigenem Hautshader,
- **Weg B**: ICT-FaceKit Light (MIT): Kopf mit Mundhöhle, Zähnen, Zunge, Augen, 53 ARKit-Formen, ohne Textur.

## Ergebnis in Kürze

**Empfehlung: Mischung – Gesicht von ICT (Weg B), Körper, Alter und Formregler nach dem Vorbild von MPFB (Weg A).**
Der Abstand bei Mund und Augen ist groß und lässt sich mit Shadern nicht schließen: ICT hat einen echten
Kieferschwung, eine Zahnreihe im Bogen, Lider mit Dicke und Tränenkarunkel und Formen aus Scans; MPFB öffnet den
Mund wie ein Gummiband, die Zähne stehen als Block davor, die Mimik ist für den Erwachsenen gebaut und wird bei Kind
und Ork falsch. Dafür hat MPFB, was ICT fehlt: Körper und Schultern, Alter (Säugling bis Greis), Geschlecht, Gewicht,
hunderte Formregler, Hauttexturen und eine Zuordnung von Augen, Zähnen und Brauen zum Körper. Die Hautfrage
(„wirkt plastisch“) ist dagegen lösbar: A2 wirkt deutlich weniger plastisch als A1, nur mit Rauheit, Rötung,
Poren und Wrap-Licht.

Die Kosten sind kein Hindernis: ein Porträt mit 100 bewegten Formen braucht auf der T400 unter 4 ms GPU-Zeit
(Messwerte unten). Teuer werden nur Dateigröße und Speicher der ICT-Formen (Formen nur auf bewegten Teilen,
Normalen der Formen weglassen, Identitäten einbacken).

## Aufbau des Versuchs

Fremddaten liegen nur lokal unter `~/werkzeuge/charakter/` (je Quelle ein eigener Ordner) und nie im Repository:
`dl/` (Asset-Pakete), `mpfb_code/` (MPFB 2.0.18), `mpfb_daten/` (entpackte Pakete), `ict/src/` (ICT-FaceKit),
`robopoets/src/` (nur gelesen, nichts übernommen), `glb/` (erzeugte Köpfe), `bilder/`.

| Datei | Aufgabe |
|---|---|
| `tools/charakter/einrichten.sh`, `einrichten.py` | Pakete entpacken, MPFB in einem eigenen Blender-Benutzerordner einschalten |
| `tools/charakter/blender.sh` | Blender 5.2.2 headless, höchstens 3 Threads |
| `tools/charakter/mpfb_kopf.py` | MPFB-Kopf nach Angabe (`angaben/*.json`): Makros, Formziele, Haut, Augen/Zähne/Zunge/Brauen/Wimpern, ARKit-Formen und Viseme, Laufzeitformen, Übertragung der Formen auf die Teile, Hauer, Zuschnitt zur Büste, 2K-Kopfausschnitt, Hautmasken, glTF |
| `tools/charakter/ict_kopf.py` | ICT-Kopf: Formen umbenannt auf ARKit-Namen (`eyeBlink_L` → `eyeBlinkLeft`), optional Identitäten, Hautmasken, Augendaten, glTF |
| `tools/charakter/kopf_gemeinsam.py` | Hautmasken, Zuschnitt (bmesh, Formen bleiben), Übertragung von Formen, Export |
| `tools/charakter/alle_koepfe.sh` | alle Köpfe bauen (je 10–40 s) |
| `tools/charakter/schau.sh`, `pruefen.sh` | Testszene unter Xvfb mit Vulkan auf der T400 starten; Syntaxprobe ohne Grafikkarte |
| `tools/charakter/boegen/*.json` | Vergleichsbögen (Zeilen × Spalten: Kopf, Stoff, Formen, Ansicht) |
| `tools/charakter/messreihe.sh` | Messreihe (je Lauf ein frischer Godot-Prozess; GPU-Zeit, Dreiecke, Speicher laut Godot) |
| `tools/charakter/warte_last.py`, `glb_info.py` | Warten auf freie NAS/Grafikkarte; Überblick über eine .glb |
| `game/versuch/charakter/` | Testszene `charakter_schau.tscn`: lädt die .glb zur Laufzeit aus `~/werkzeuge/charakter/glb` (fehlt der Ordner, sagt sie es), Bühne mit Licht und Kamera, `haut.gdshader`, `augen.gdshader` |

```bash
tools/charakter/einrichten.sh                       # einmalig (Pakete liegen in ~/werkzeuge/charakter/dl)
tools/charakter/alle_koepfe.sh                      # alle Köpfe nach ~/werkzeuge/charakter/glb
tools/charakter/schau.sh <bilder> 2000x1400 --modus=bogen --bogen=$PWD/tools/charakter/boegen/vergleich.json --ende
tools/charakter/messreihe.sh <ordner>
```

Bilder und Messwerte (nicht im Repository): `~/werkzeuge/charakter/bilder/phase0/` (Bögen, je Zelle ein Einzelbild)
und `~/werkzeuge/charakter/bilder/messung/messungen.jsonl`.

Last: Die kurzen Blender-Läufe (10–40 s, 3 Threads) liefen mit `nice` bis Last 20; Bilder und Messungen warteten auf
Last unter 12 und eine freie T400 (höchstens ein anderer Vulkan-Lauf, bei Messungen keiner bzw. höchstens 4–8 min
Wartezeit; jede Messzeile nennt Last und Zahl der anderen GPU-Läufe).

Licht und Kamera aller Bilder: Schlüssellicht warm von links oben (mit Schatten), Fülllicht kühl rechts (0,45),
Gegenlicht warm hinten rechts (1,6), wenig Umgebungslicht, SSAO klein, AgX, MSAA 4×, Forward+ auf der T400.
Kamera mit 20° Bildwinkel; im Vergleichsbogen feste Bildhöhe 0,46 m (gleiche Kamera für alle Köpfe), sonst nach Kopfgröße.

Die Köpfe:

| Kopf | Ecken | Dreiecke | Formen | Anmerkung |
|---|---|---|---|---|
| A (MPFB, Büste) | 4 974 Körper + 5 532 Teile | 19 996 (Körper 9 828, Zähne 7 120, Augen 2 040) | 52 ARKit | Zähne sind fast so teuer wie der ganze Kopf |
| B (ICT, Kopf und Hals) | 26 719 | 52 220 | 57 (ARKit ohne `tongueOut`, `cheekPuff` je Seite, dazu Pupille und Wangenheben) | keine Schultern, keine Textur |

## Test 1: A1 | A2 | B

Bilder: `vergleich.png` (Büste vorn und ¾), `vergleich_nah.png` (Augen vorn/¾, Mund halb offen, Lächeln, „O“).

- **A1 (wie geliefert)** wirkt plastisch: gleichmäßiger Glanz über Stirn und Nase (Rauheit 0,5 überall, Standard-Spiegelanteil),
  blasse, gleichförmige Haut. Der Hautatlas ist 2048², der Gesichtsteil darin **722 × 1371 px** (größte UV-Insel
  über dem Hals). Die Augenbrauen sind schwarze Fransen, die Wimpern Karten mit sichtbaren Zacken.
- **A2** wirkt nicht mehr plastisch, sondern matt-lebendig: Glanz nur in T-Zone und auf den Lippen, Rötung an Wangen,
  Nase, Ohren, Wrap-Licht am Schattenrand, Flaum am Umriss, feine Poren (im Spiel erzeugtes Rauschen, keine Datei).
  Der **2K-Kopfausschnitt bringt sichtbar nichts**: hochskaliert von 722 × 1371 auf 1079 × 2048 (×1,5) entsteht keine
  neue Information; er lohnt erst mit einer eigenen, für den Kopf gemalten bzw. gebackenen Textur. Was A2 besser macht,
  sind Rauheit, Masken und Licht. Schwachpunkte bleiben: Mundinneres hellrosa, Augenbrauen.
- **B** ist ohne Textur porzellanhaft (einfarbig, nur Masken und Poren), aber die **Form** ist die natürlichste:
  Nase, Lippen, Lidfalte, Augenwinkel. Augen: zwei Kugeln je Auge (außen Hornhaut, innen Iris), Iris, Pupille und
  Spitzlicht zeichnet unser Shader aus Eckdaten. ICT-Wimpern sind Streifen für eine Haartextur, die fehlt (ausgeblendet).
- **Mund**: Bei A öffnet `jawOpen` die Lippen zu einem Oval, das Kinn bleibt fast stehen; man sieht nur die untere
  Zahnreihe als weißen Block vor hellrosa Fläche, beim „O“ drängen sich die unteren Zähne zwischen die Lippen. Bei B schwingt der Kiefer,
  obere und untere Zahnreihe stehen im Bogen, Zunge und Mundhöhle sind da; das Lächeln zeigt die obere Zahnreihe wie
  bei einem Menschen. **Abstand A2 → B bei Mund und Augen: groß** (Geometrie, nicht Shader).
- Beide brauchen noch: dunkle Mundhöhle (Verdeckung nach Tiefe), Wimpern- und Brauenkarten mit Alpha, Tränensaum.

## Test 2: Mimik über Alter und Volk

Bilder: `mimik.png` (6 Formen, Gewicht 1, Kopf leicht schräg), `mimik_nah.png` (Mund und Lider nah).
Köpfe: Kind 5 J., Mann 35 J., Greis 70 J., Ork (MPFB-Makros und 22 Formziele übertrieben, Hauer), dazu B.

- **Die MPFB-Formen sind feste Versätze in Metern, für den Erwachsenen gebaut.** Beim **Kind** werden sie zu groß:
  `jawOpen` reißt den Mund über das halbe Untergesicht auf, `mouthFunnel` stülpt die Lippen weit vor, das Lächeln
  beult die Lippen aus. Beim Greis passt die Größe, die Haut faltet sich nicht (keine Faltenkarten).
- **Ork**: Die übertriebenen Makros ergeben einen schweren Menschen, keinen Ork – MPFB-Ziele reichen dafür nicht
  (Kiefer, Stirnwulst, Nase brauchen eigene Formen). Die **Hauer folgen dem Kiefer** (Übertragung von den
  Hilfsecken der unteren Zähne), schneiden bei `jawOpen` + Lächeln aber in die Unterlippe. Hautfarbe: MPFB-Haut
  „african“ mit grüner Tönung bleibt braun; Ork und Oger brauchen eigene Hautbilder.
- **Lider**: Blinzeln schließt bei allen sauber; die Wimpernkarten liegen bei A als Punktlinie auf dem Lid.
- **B** zeigt dieselben Formen natürlich (Kieferschwung, Lippen rollen, Wangen heben sich), hat aber nur ein
  Erwachsenengesicht: Kind, Greis und Völker müssen erst entstehen (siehe Werkzeugkette).
- Fehlerquellen also: absolute Versätze bei anderem Maßstab (Kind, Ork), fehlende Faltenkarten (Greis), Durchdringung
  von Zusatzteilen (Hauer) bei kombinierten Formen. Seiten links/rechts: MPFB und ICT stimmen untereinander überein;
  ob `mouthSmileLeft` die Seite der Figur oder die des Betrachters meint, ist in Phase 1 mit einer Frontansicht
  festzulegen.

## Test 3: Folgen Augen und Zähne den Formreglern?

Bilder: `formen.png` (Kopf ¾), `formen_nah.png` (Augen und Mund nah, ±1). Regler: `kopf_breit`
(head-scale-horiz), `augen_weit` (l/r-eye-trans-out), `kiefer` (chin-width, -prognathism, -jaw-drop).

- **Fest eingebacken** (Regler in MPFB gesetzt, dann Teile angepasst und exportiert): Augen, Zähne, Brauen passen.
  Im glTF-Export kommen die Regler dann aber **nicht als Formen** an – jede Variante wäre eine eigene Datei.
- **Als Laufzeit-Blendshape nur am Körper**: **Augen und Brauen bleiben stehen.** Bei `augen_weit` ±1 schieben sich
  die Lider über bzw. hinter die Augäpfel (weiße Lederhaut quillt aus dem Lid), die Brauen schweben neben der Stirn.
  Zähne fallen bei geschlossenem Mund nicht auf, bei offenem Mund zeigt sich an der Seite ein kleiner Spalt.
- **Als Laufzeit-Blendshape mit Übertragung** (eigener Code: je Ecke des Teils drei Körperecken mit Gewichten aus der
  `.mhclo`, nur der Versatz wird übertragen): **sieht aus wie fest eingebacken**. Das ist der Weg für Gene im Spiel.
  MPFB selbst überträgt nur ARKit-Formen und Viseme (`FaceService.interpolate_targets`), keine Formregler.
- Ein Augapfel darf sich dabei nicht verformen (nur verschieben); bei `kopf_breit` bleibt die Verzerrung klein, für
  Phase 2 besser: Augen als eigener Knoten, dessen Lage aus den Formen berechnet wird.

## Test 4: Blendshapes in Godot

Messung `tools/charakter/messreihe.sh`: 1280 × 720, ein Porträt (Büste, ¾) bzw. acht kleine, vsync aus, alle Formen
jedes Bild bewegt (Sinus), 8 s nach 2,5 s Anlauf, je Lauf ein frischer Prozess. GPU-Zeit aus Godots Messung je
Ansicht; Speicher laut Godot (`RenderingServer.get_rendering_info`, Puffer = Ecken und Formen).

Die Bilder/s sind hier wenig aussagekräftig: Xvfb und die belastete NAS (Last 4–12 durch andere Läufe) deckeln bei
etwa 100; maßgeblich ist die **GPU-Zeit je Bild**. „Dreiecke“ gezählt mit Schattendurchgang (×2).

| Lauf | Formen (bewegt) | GPU ms | Dreiecke Kopf / gezeichnet | Zeichenaufrufe | Puffer-Speicher | .glb | Laden |
|---|---|---|---|---|---|---|---|
| leere Bühne (Licht, Schatten, SSAO, MSAA) | – | 1,46 | – | 2 | 31,2 MB | – | – |
| A1 | 52 (52) | 3,36 | 19 996 / 39 996 | 17 | 37,8 MB | 12,3 MB | 0,51 s |
| A2 | 52 (52) | 3,40 | 19 996 / 39 996 | 18 | 37,8 MB | 15,5 MB | 0,6–1,1 s |
| A2 still | 52 (0) | 3,33 | | 18 | 37,8 MB | | |
| A, 0 Formen | 0 | 3,47 | | 18 | 31,7 MB | 10,7 MB | 0,47 s |
| A, 30 Formen | 30 (30) | 3,38 | | 18 | 35,5 MB | 13,7 MB | 0,93 s |
| A, 60 Formen | 60 (60) | 3,58 | | 18 | 39,1 MB | 16,7 MB | 0,60 s |
| A, 100 Formen | 100 (100) | 3,82 | | 18 | 45,1 MB | 22,3 MB | 0,78 s |
| A, 100 Formen ohne Normalen der Formen | 100 (100) | 3,69 | | 18 | 45,1 MB | 13,9 MB | 0,90 s |
| B | 57 (57) | 3,59 | 52 220 / 104 444 | 22 | 64,2 MB | 41,6 MB | 0,63–0,81 s |
| B still | 57 (0) | 3,19 | | 22 | 64,2 MB | | |
| B, 0 Formen | 0 | 3,16 | | 22 | 32,4 MB | 1,9 MB | 0,08 s |
| B, 100 Formen (57 + 43 Identitäten) | 100 (100) | 3,91 | | 22 | 87,8 MB | 71,5 MB | 1,14 s |
| 8 Porträts A2 (je 320 × 360, Bilder unkomprimiert) | 52 (52) je Kopf | 10,7 | | 137 | 100 MB | | |
| 8 Porträts B | 57 (57) je Kopf | 14,3 | | 169 | 311 MB | | |

- **Formen kosten fast nichts, solange sie stillstehen**; bewegt kosten 100 Formen am MPFB-Kopf (10 500 Ecken)
  etwa 0,35 ms, am ICT-Kopf (26 700 Ecken) etwa 0,75 ms GPU-Zeit. 30, 60, 100 Formen sind alle unkritisch.
- **Speicher**: Godot legt je Form Lage, Normale und Tangente ab (auch wenn die .glb keine Normalen der Formen hat):
  etwa 0,13 MB je Form beim MPFB-Kopf, 0,55 MB je Form beim ICT-Kopf. 100 ICT-Formen = 55 MB je Kopf, acht
  einzeln geladene Köpfe = 311 MB Puffer – bei 4 GB T400 noch tragbar, aber der größte Posten. Abhilfe: Formen nur auf
  Netzteilen, die sich bewegen (ICT-Hinterkopf, Hals und Zähne tragen alle 57 Formen mit), Identitäten im Editor
  einbacken statt als Form mitzuführen.
- **Dateigröße**: Die Normalen der Formen verdreifachen die Formdaten (A: 11,6 statt 3,2 MB für 100 Formen); ICT
  liefert dichte Formen für alle 26 719 Ecken (57 Formen = 40 MB). Ob die Mimik ohne Normalen der Formen sichtbar
  schlechter schattiert, ist noch nicht verglichen (Phase 1).
- **Laden** (glTF zur Laufzeit lesen, Bilder auspacken, Szene bauen) 0,1–1,1 s je Kopf; das Komprimieren der Bilder
  (BPTC) zur Laufzeit dauerte 16–24 s und gehört in den Import, nicht ins Spiel.
- **Grafikspeicher** gesamt laut Godot: leere Bühne 270 MB (davon die Schattenkarte 8192² aus den
  Projekteinstellungen), ein Kopf A2 +105 MB, B +48 MB. Bei A sind die Bilder hier doppelt geladen (unkomprimiert
  vom Laufzeitladen und komprimiert); nach dem Import im Spiel bleiben für A2 etwa 10 MB Bilder (BPTC).
  `nvidia-smi` ließ sich nicht sauber auswerten (andere Läufe auf derselben Karte).
- **Acht Porträts** gleichzeitig kosten 10–14 ms GPU, vor allem durch Schattenkarte und SSAO je Fenster; für
  Stammbaum und Ratstafel kleine Fenster ohne Schatten (wie im HG-Versuch gemessen).

## Test 5: Lizenzen (aus den Dateien selbst)

| Quelle | Lizenz | Beleg |
|---|---|---|
| MPFB 2.0.18 Code | GPL 3 | `LICENSE.md` Abschnitt B; nur als Werkzeug in Blender, nichts davon im Repository oder Spiel |
| MPFB mitgelieferte Daten (Grundnetz, Formziele, Makros, Gewichte) | CC0 | `LICENSE.md` Abschnitt C, `LICENSE.ASSETS.md` |
| Ausgabe von MPFB (Export, Skript) | keine Ansprüche der MakeHuman-Leute | `LICENSE.md` Abschnitt D: „regardless of whether you use the UI … or call functions of MPFB via a script“ |
| `makehuman_system_assets` (Augen high-poly, Zähne, Zunge, Brauen 001, Wimpern 01, Häute, Augenfarben) | CC0 | `packs/makehuman_system_assets.json` (94 × CC0); Kopf jeder `.mhclo`/`.mhmat`: „explicitly released as CC0 in september 2020“ |
| `faceunits01` (52 ARKit-Formen, Mika Suominen) | CC0 | `packs/faceunits01.json` (52 × CC0) |
| `visemes01` (22), `visemes02` (15) | CC0 | `packs/visemes01.json`, `packs/visemes02.json` |
| `skins01` (23), `skins02` (13), `skins03` (20) | CC0 | `packs/skins0x.json`, alle Einträge CC0; Dateinamen `*_cc0.zip` |
| ICT-FaceKit (Netz, Formen, Identitäten, Skripte) | MIT | `LICENSE` (USC ICT 2020) – Copyright-Hinweis muss mit ausgeliefert werden |
| RoboPoets digital_human | Code MIT, Kunst CC0 | `LICENSE.md`, README „Credits“; nur gelesen, kein Code übernommen |

Nebenbefund: Nicht alle Häute haben nur Grundfarbe – `mindfront_aksel_skin`, `mindfront_skin_male_african_middleage`
(skins02) und `sonntag78_orkifyheadtarget_custom_skin` (skins03, Ork-Haut mit Normalenkarte) bringen Normal- bzw.
Glanzkarten mit (2048²). Human Generator wurde nicht benutzt, auch nichts aus `versuch/hg-koepfe` außer dem eigenen
Licht- und Shadercode als Vorlage.

## Empfehlung

**Mischung mit ICT-Gesicht als Kern.**

1. Gesicht, Mund, Zähne, Zunge, Augen und Mimik von ICT (MIT): das Beste bei Mund und Augen, 53 ARKit-Formen aus
   Scans, 100 Identitäts-Hauptkomponenten als natürliche „Gene“ für Erwachsenengesichter.
2. Körper/Büste, Alter, Geschlecht, Gewicht und gezielte Formregler (Nase, Kiefer, Ohren, Augenabstand) von MPFB
   (CC0): einmal per Skript auf das ICT-Gesicht übertragen (Registrierung beider Netze), danach im Spiel nur
   Blendshapes mit Übertragung auf die Teile (Test 3 zeigt, dass das trägt).
3. Haut: eigener Shader wie A2 (Rauheit, Rötung, Poren, Wrap, Flaum); Texturen je Volk und Alter neu backen,
   2K je Kopf, kein MPFB-Atlasausschnitt.

Reiner Weg A ist schneller zu haben (alles ist da), bleibt aber bei Mund und Augen sichtbar hinter BG3/CK3 zurück,
und gerade das sieht man im sprechenden Porträt. Reiner Weg B hat kein Alter, keine Kinder, keinen Körper, keine Textur.

## Was die Werkzeugkette in Phase 2 können muss

- **Registrierung ICT ↔ MPFB**: Landmarken (ICT hat 68), Ausrichtung, nicht-starre Anpassung, damit MPFB-Formregler,
  Alter und Geschlecht als Formen auf das ICT-Gesicht kommen, mit weicher Naht am Hals zur MPFB-Büste.
- **Formen mit Maßstab**: Mimik je Alter und Volk anpassen (Versätze mit der lokalen Gesichtsgröße skalieren bzw.
  Deformation Transfer statt fester Meterwerte), Korrekturformen für Kombinationen (`jawOpen` + Lächeln).
- **Übertragung auf Teile** (Augen, Zähne, Zunge, Brauen, Wimpern, Hauer, Bart): wie in `kopf_gemeinsam.py`, für
  Augen als Verschiebung des ganzen Augapfels.
- **Völker**: eigene Formen für Ork (Kiefer, Stirnwulst, Nase, Hauer als Teil mit Zuordnung) und Oger, Elbenohr,
  Zwergen- und Halblingproportionen; Hauer-Durchdringung prüfen.
- **Textur-Backen** zwischen Topologien (Cycles „selected to active“): Grundfarbe, Normalen, Rauheit, Masken
  (Rötung, Glanz, Höhlen), Faltenkarten je Alter, Haut je Volk; Kopf-UV 2K.
- **Augen**: Iris- und Lederhautbild, Hornhaut mit Spitzlicht, Lidschatten-Netz (ICT „EyeOcclusion“), Tränensaum.
- **Mund**: dunkle Mundhöhle (Verdeckung nach Tiefe), Zahn- und Zahnfleischbild.
- **Haar**: Brauen und Wimpern als Karten mit Alpha, Frisuren und Bärte (nicht getestet).
- **Export**: nur bewegte Teile mit Formen, Normalen der Formen weglassen oder sparsam, Texturen über den Import
  komprimieren; Größe und Ladezeit je Kopf messen.
- **Prüfbögen** wie hier (Alter × Volk × Mimik) und Messreihe automatisch; Protokolleintrag je erzeugter Datei.

## Offene Fragen an den Nutzer

1. **Weg**: Mischung mit ICT-Gesicht (empfohlen), reiner Weg A (schneller, schlechterer Mund) oder reiner Weg B?
2. **Büste**: Bis wohin reicht das Porträt – Kopf mit Schulteransatz und Kragen (empfohlen, die Schnittkante liegt
   unter der Kleidung) oder halbe Brust mit Armen?
3. **Ork und Oger**: eigene Grundformen modellieren lassen (empfohlen, MPFB-Regler reichen nicht) oder nur über
   Regler und Hauer annähern?
4. **ICT-Hinweis**: Der MIT-Copyright-Hinweis von ICT gehört dann in die Lizenzliste des Spiels (empfohlen: in die
   bestehende Liste der Fremdlizenzen aufnehmen).
