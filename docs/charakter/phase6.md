# Charakter-Editor, Phase 6: erwachsene, markante, verschiedene Menschenköpfe mit gemalt-realistischer Haut

Stand 31.10.2026, Branch `phase5/charakter-werkzeug`, Mitglied darstellung. Kein Spielcode, kein PR (Entscheidung E17).
Grundlage: [Phase 5](phase5.md). Plan: vom Nutzer am 30.10.2026 freigegeben (E1–E17 wie empfohlen), vier Etappen
A–D mit Halt. Dieser Bericht wächst mit den Schritten; hier stehen bisher **S0** (Messbasis, Prüfstand, Würfeln über
den Kern), **S1** (Vergleichsvorlagen der lokalen Bild-KI), **S2** (Grundfehler) und **S3** (Porträtlicht). Damit ist
Etappe A fertig; die Befunde der Prüfer sind im Abschnitt **Nachtrag nach der Prüfung** behoben bzw. berichtigt, es
folgte Halt A. Danach **Etappe B** (S4–S7: Grundgesichter nach Maß, Gene eichen, Gesichtszüge und Asymmetrie,
Wiedererkennungsmerkmale) bis Halt B, Abschnitt **Etappe B** unten.

## S0 Messbasis, Prüfstand, Würfeln über den Kern

### Ergebnis in Kürze

- **Stand 5 ist Phase 5 – bitgenau.** Der Bogen `gesicht30` der Phase 5 neu gerendert unterscheidet sich vom Bild der
  Phase 5 im Mittel um 0,00/255 (mit TressFX-Haar). Gemessen mit der neuen Werkzeugkette: beleuchtete Wange
  L*/a*/b* 82,9/4,7/9,7, Chroma 10,8 (Bestandsaufnahme 83/4,7/9,8, Chroma 11), Feinstruktur Wange/Stirn 0,34/0,21,
  Oberlidzone −10,5 L*, Iris-Leuchtdichte 0,30.
- **Würfeln über den Kern:** 1000 Startwerte × 6 Völker, Würfeln und Vererben über zwei Generationen: 0 Abweichungen
  der bestehenden Gene mit und ohne Probevorlage, derselbe Startwert zweimal 0 ungleiche Bytes (10,6 s); die Galerie
  zeigt mit Z 30 verschiedene Gensätze (Runde 1 und 2 geprüft).
- **Gewichteausdruck = Szene:** 20 Zufallspersonen (Startwerte 6001–6010, 6101–6110), headless ausgeschrieben gegen die
  Gewichte in den gerenderten Zellen: größte Abweichung 0,0.
- **Bild gegen Netz:** sechs Verhältnisse je Mann 30 und Frau 30 stimmen im orthografischen Bild auf −1,2 … +1,7 %
  mit dem Netz überein (Umrisse vorn und im Profil, Pupillen). Die Abweichungen der Bestandsaufnahme (Bild 6–12 %
  „kürzer“) haben vier Ursachen, siehe unten.
- **Kalibrierung:** Graukarte (L-V) sRGB 118,0 in jedem Lauf; Farbtest linear 0,503 (`Vector3`), 0,212 (`Color`),
  0,212 (`StandardMaterial3D`) – der Fehler der Farbübergabe ist damit gemessen (12 Stellen im Code, behoben in S2).
- **Prüfstand und Prüfplan** stehen: alle 16 Bögen des Plans (42 Dateien) und zwei Messbögen, Prüfmappe A0 = Stand der
  Phase 5, `pruefen6.py` in 8–28 s (je nach Last) ohne Godot und Blender. Das Quellenmanifest ist grün.

### Was gebaut ist

- **Erbgut über den Kern** (`game/versuch/charakter/ErbgutBruecke.cs`, liest den Kern nur): `Wuerfeln(saat, volk)`,
  `Vererben(saat, vater, mutter, volk)`, `SichtbarerWert(griff, gen, geschlecht)`, `VerdeckterWert(griff, gen)`,
  `SichtbareWerte`, `AlsText`; Vorlage `Erbgutvorlage.Standard` oder eine Vorschlagsdatei über `Erbgutvorlage.AusJson`
  (`VorlageLaden`). GDScript hält kein C#-Objekt des Kerns, die Brücke merkt sich jedes Erbgut unter einer Nummer
  (Griff). `charakter_kopf.gd` (`gene_aus_saat`, `erbgut_text`) und die Bögen lesen die sichtbaren Allele so, wie der
  Kern sie hält; die Frauenregel für den Bart wendet `_farben()` wie bisher selbst an („bartwuchs“ und
  „bartwuchs_frau“ getrennt). `Pruefen(von, anzahl)` ist die Abnahme: 1000 Startwerte × 6 Völker, Würfeln und
  Vererben über zwei Generationen, einmal mit `erbgut.json` und einmal mit einer Probevorlage (+1 Gen, +1 Korrelation
  aus `kiefer_breite` hinein, so wie S7 neue Gene anlegt).
- **Bogenschlüssel** (`charakter_schau.gd`): `stand` (5 = Phase 5, 6 = neuer Weg ab S2; `--stand=` für alle Zellen ohne
  eigenen Stand), `saat`, `familie` (`{"<saat>": [vater, mutter]}`, rekursiv vererbt), `saat_arten` (nur diese Gene aus
  der Saat), `vorlage` (Erbgut-Vorschlagsdatei), `licht`, `anzeige`, `ziel_x`, `kamera: "ortho"`,
  `schluessel_richtung`, `pruefkoerper`, und für P3 `vorlage` als Zelle: eine Fotovorlage, auf den Pupillenabstand des
  Kopfs der Zelle skaliert und an dessen projizierte Pupillen gelegt. Jede Zelle schreibt neben `zz_ss.png` eine
  **Begleitdatei** `zz_ss.json`: Kamera (Transform, Projektion, FOV/Größe, Bildgröße), Kopf (Transform, Gene, Alter,
  Geschlecht, Stand, Formgewichte aus der Szene, Merkpunkte), Erbgut als Text.
- **Gewichteausdruck**: `charakter_schau.tscn -- --gewichte=<personen.json> --aus=<gewichte.json> --ende` (auch
  `--headless`, ohne Grafikkarte) schreibt je Person die Formgewichte von `_anwenden()` und die Werte, die danach in den
  Netzen stehen (`szene_gewichte()`). Python misst genau diese Gewichte – es gibt keinen Nachbau von `_anwenden()`.
- **Galerie**: Taste **Z** würfelt alle 30 Köpfe über den Kern neu (Runde n: Startwerte 20000 + 100·(n − 1) + 1 … 30),
  Taste **G** schaltet die Gestalt-Gene an und aus (aus = Mensch-Mittel wie Phase 5), Startoption `--saat_runde=<n>`;
  Runde 0 ist die Aufstellung der Phase 5.
- **Bühne** (`charakter_buehne.gd`): Lichtarten `spiel` (L-S, bis S3 genau das Porträtlicht der Phase 5), `vorlage`
  (L-V: weiches Schlüssellicht 45° links oben, Winkelgröße 3°, Aufhellung 1 : 3 von rechts, kein Gegenlicht, neutrale
  Umgebung 0,12, Grund wie eine 18-%-Fläche unter demselben Licht, AgX, **Belichtung 0,435** – geeicht an der
  Graukarte, Reihe 0,40 … 0,70 → sRGB 113 … 144) und `innen` (L-I, vorläufig bis S3); Anzeige `albedo`
  (unbeleuchtet über `DEBUG_DRAW_UNSHADED`, linear abgebildet, keine Verdeckung); `ansicht()` mit `ziel_x`
  (Nahaufnahme eines festen Punkts, z. B. der Wange) und orthografischer Kamera; Schlüssellicht drehbar
  (`schluessel_richtung`, Streiflicht N5); **Prüfkörper**: Graukarte 18 %, Hautfeld Typ III (PhysicallyBased, linear
  0,623/0,433/0,343), Farbtest (`farbtest.gdshader`: `uniform vec3 farbe : source_color` mit `Vector3(0,5)` und mit
  `Color(0,5)`, daneben ein unbeleuchtetes `StandardMaterial3D` mit `Color(0,5)`), Prüfkugeln (haut3 ohne Wrap, haut3
  wie im Spiel, `StandardMaterial3D` mit gleicher Albedo, Rauheit 0,56, specular 0,42).
- **Messpaket** `tools/charakter/messen/` (Blenders Python mit numpy und OpenImageIO, ohne Blender, Godot, scipy, PIL):
  `glb.py` (auch dünne Accessoren, Formen und Normalen wie Godot), `merkpunkte.py` (68 ICT-Merkpunkte und
  anthropometrische Punkte, jeder mit Definition im Kopf der Datei), `netz_masse.py`, `bild.py`, `kamera.py`
  (Projektion aus den Begleitdateien), `bild_werte.py` (Zonen, CIELAB, Chroma, ITA, Licht : Schatten, R/G, Feinstruktur,
  Fleckenband, Augenhöhle, Iris und Lederhaut, Umriss, Pupillen, Bildunterschied, Flimmern, Autokorrelation),
  `bevoelkerung.py` (Kennzahlen einer Stichprobe gegen NIOSH, Karikaturmaß K, Nachbarabstand), `raster.py`
  (Koordinatenraster zum Setzen der Merkpunkte von Hand), `pruefen6.py` (Sammelaufruf, Ist/Ziel/Status als
  `pruefen6.json` und `.md`, statische Farbprüfung), `vorlagen_merkpunkte.json`, `personen_s0.json`.
- **Prüfplan als Bögen** `tools/charakter/boegen6/` aus einer Quelle (`erzeugen.py`): P1–P16 nach Abschnitt 5 des Plans
  (Kameras K1–K5, N1–N6; Lichter L-V, L-S, L-I; Personen), dazu die Messbögen M0 (Bild gegen Netz) und M1 (Gewichte in
  der Szene). Schlüssel späterer Schritte (`wetter`, `stufe`, `allelregel`, `zuege`, `leben`) stehen schon in den
  Bögen und wirken ab ihrem Schritt.
- **Quellenmanifest** `tools/charakter/quellen6.json`, geprüft mit `tools/charakter/einrichten.sh pruefen`
  (`quellen6_pruefen.py`): ICT-FaceKit `da5f95a`, RoboPoets `9d28d0c`, MPFB 2.0.18 (Prüfsumme über alle 2855 Dateien),
  SHA-256 der sieben Zips in `~/werkzeuge/charakter/dl/`, Blender 5.2.2, Godot 4.7.2; Bild-KI (S1) und HDRI (S3) als
  Einträge.
- **Gesichert**: Notizen, Messskripte und Plan aus dem Scratchpad nach `~/werkzeuge/charakter/notizen/phase6/`
  (175 Dateien, Prüfsummen gleich).

### Definitionen, die sich gegenüber der Bestandsaufnahme ändern

- **Pupille** = Mitte der Pupillenöffnung im Iris-Netz (dort sitzt die gemalte, sichtbare Pupille), nicht die Mitte
  aller Iris-Ecken: Pupillenabstand Mann 63,6 mm statt 61,7, Frau 62,6 statt 60,8 (+3 %). Im Bild liegen die
  gefundenen Pupillen damit waagerecht auf 1,1–1,2 Bildpunkte an den projizierten (vorher 3 Bildpunkte daneben).
- **Menton** = unterste Stelle des vorderen Profils, die vom Pogonion aus noch höchstens 10 mm hinter ihm liegt; das
  „Kinn“ der Bestandsaufnahme war der ICT-Merkpunkt 8 (4–5 mm höher). **Subnasale** = ICT-Merkpunkt 33. **Zygion** =
  größte Breite in Höhe n − 45 … n − 10 mm, mindestens 15 mm vor der Ohrwurzel (dort liegt der Jochbogen; weiter
  hinten wird der Schädel zum Ohr hin breiter). Damit: Mann 30 zy 144,2 mm (Bestandsaufnahme 148, −2,6 %), Frau 30
  148,3 (147–148), Gesichtsindex Mann 0,94 (0,905), Frau 0,79 (0,79).
- **Wangenwölbung** wie in der Bestandsaufnahme (Pupillenhöhe bis ICT-Merkpunkt 8, |x| 36–44 mm, Sehne 0,15 … 0,8): Das
  Grundnetz ergibt wieder +5,9 mm. Für Mann 30 und Frau 30 mit den Gewichten aus Godot ergibt sie +2,3 bzw. +3,9 mm
  statt +1,5 bzw. +4,2; das Skript der Tabelle ist nicht erhalten. Ohne die Gen-Gewichte des Mensch-Mittels (nur Reife
  und Alter) sind es +2,2 bzw. +3,8 mm – die Gene erklären die Abweichung also nicht, sie liegt im Messweg. Frau 35
  hat +4,9 mm. Die Ziele in S4 gelten in der neuen Definition (Mann ≤ 0, Frau ≤ +2 mm).
- **Breiten**: Gesichtsbreite in einer Höhe = Umriss vor der Ohrwurzel wie bei zy. Kinnmitte/zy Mann 30 = 0,80 (das
  Bildmaß der Bestandsaufnahme war 0,84, zy dort aus dem Bild). Braue über der Pupille = Unterkante der Brauenkarte
  (Netz, ab der neuen Pupille): Mann 12,6 mm, Frau 16,4 mm; die Bestandsaufnahme maß im Bild die sichtbare Haarkante
  (15 bzw. 20–21 mm).
- **Mundlage** sn–sto : sn–me = 0,22 (Mann 30) bzw. 0,25 (Frau 30); die Bestandsaufnahme maß im Bild 0,27 (dort lagen
  sn höher und me höher). Die Vorlagen nach den neuen Merkpunkten: m01 0,33, m05 0,32, b01 0,35, f02 0,36, f07 0,33,
  f09 0,34.
- **Bild gegen Netz (Erklärung „Bild 6–12 % kürzer“)**: (1) andere Pupille (Ecken-Mitte statt Öffnung: 3 %),
  (2) Perspektive bei FOV 20° (ungefähr 1 m Abstand): Breiten gegen den IPD im Bild −3,4 … +8,0 %, Höhen gegen den
  IPD +0,3 … +0,4 % (die Augen liegen der Kamera näher als Kiefer und Ohren), (3) Raumabstand n–me gegen die Höhe
  0,3–0,7 %, (4) andere Kinnpunkte (ICT-Merkpunkt 8 gegen me, 4–5 mm = 3–4 %). Orthografisch und mit einem
  Merkpunktsatz stimmen Bild und Netz auf ±2 % überein.
- **Kieferknick** ist vorläufig (größter Knick auf 5 mm im Schnitt x = ±0,7·go, mit den gerenderten Normalen und mit
  denen der Form): die Werte streuen stark (26–63°); der Messweg wird in S4 auf die Kieferkante selbst beschränkt.

### Testanleitung

```bash
BPY=~/werkzeuge/blender/blender-5.2.2-linux-x64/5.2/python/bin/python3.13
G=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
# Manifest
tools/charakter/einrichten.sh pruefen
# Erbgut-Brücke (ohne Grafikkarte)
$G --headless --path game res://versuch/charakter/charakter_schau.tscn -- --bruecke_pruefen --aus=/tmp/bruecke.json --ende
# Gewichteausdruck (ohne Grafikkarte)
$G --headless --path game res://versuch/charakter/charakter_schau.tscn -- --glb=$HOME/werkzeuge/charakter/glb5 \
   --gewichte=$PWD/tools/charakter/messen/personen_s0.json --aus=/tmp/gewichte.json --ende
# Bögen (Vulkan, T400): Prüfmappe A0 = Stand 5
python3 tools/charakter/boegen6/erzeugen.py
GLB=~/werkzeuge/charakter/glb5 ANZEIGE=:98 tools/charakter/schau.sh <ordner> 3200x4560 \
   --bogen=tools/charakter/boegen6/p10_kalibrier.json,tools/charakter/boegen6/p01_alter_ton_k1_lv.json --ende
# P3 Vorlagen (ein Bogen mit TressFX-Haar braucht etwa 5 Minuten; ZEIT = Zeitlimit in Sekunden, Standard 300)
ZEIT=1500 GLB=~/werkzeuge/charakter/glb5 ANZEIGE=:98 tools/charakter/schau.sh <ordner> 3200x4560 \
   --bogen=tools/charakter/boegen6/p03_vorlagen_s1a.json,tools/charakter/boegen6/p03_vorlagen_s1b.json --ende
# Galerie: Z würfelt neu, G Gestalt-Gene an/aus
SZENE=charakter_galerie.tscn GLB=~/werkzeuge/charakter/glb5 tools/charakter/schau.sh <ordner> 1600x900
# S1-Satz (liegt außerhalb des Repositorys): Abnahme, Liste, Blindbogen
python3 -I ~/werkzeuge/charakter/vorlagen-gesicht6/_werkzeug/pruefe6.py
python3 -I ~/werkzeuge/charakter/vorlagen-gesicht6/_werkzeug/liste6.py
$BPY -I ~/werkzeuge/charakter/vorlagen-gesicht6/_werkzeug/blind6.py
# Prüfung (ohne Godot und Blender, unter 2 Minuten)
$BPY -I tools/charakter/messen/pruefen6.py --mappe=~/werkzeuge/charakter/bilder/phase6/A0 --alt5=<Bilder gesicht30 der Phase 5>
```

### Prüfung S0

Prüfmappe **A0** (Stand 5 = Phase 5) unter `~/werkzeuge/charakter/bilder/phase6/A0/`: P1 (K1/K2 × L-V/L-S), P3
(alter Satz und der neue Satz aus S1 in vier Bögen), P7 (K2, N2, N4, N5) und P7b, P10 (drei Läufe), P11 (Haar,
Augen), Messbögen M0 und M1, `gesicht30` der Phase 5 neu gerendert, M2, `pruefen6.json`/`.md`, `commit.txt`,
`dateien.txt`, Blindbogen von S1 (der Schlüssel nur als SHA-256, damit der Teamleiter aus der Mappe blind schätzen
kann). Die Godot-Teile sind seit `e7cd020c` unverändert; P3 ist mit dem Endstand der Merkpunkte gerendert.
T400, Vulkan über die NVIDIA-ICD, Anzeige :98; gestartet nur bei Last < 12 und höchstens einem anderen Godot.

| Abnahme S0 | Ergebnis | Beleg |
|---|---|---|
| `pruefen6.py` ohne Godot und Blender ≤ 2 min | 16–28 s bei Last 13–15 durch andere Läufe auf der NAS (8 s ohne fremde Last) | `A0/pruefen6.md` |
| Phase-5-Werte ±3 % oder begründet | Wange L* 82,9 / a* 4,7 / b* 9,7, Chroma 10,8 (83/4,7/9,8, 11) ✓; Bild `gesicht30` gegen Phase 5 0,00/255 ✓; Wangenwölbung Grundnetz +5,9 mm ✓, Mann/Frau 30 +2,3/+3,9 statt +1,5/+4,2 und Kinnmitte/zy 0,80 statt 0,84 begründet (Definitionen oben) | `A0/gesicht30`, `alt5_gesicht30` |
| Bild (orthografisch) und Netz, 6 Verhältnisse ±3 % | Mann 30 −1,2 … +0,4 %, Frau 30 −1,4 … +1,7 %; Pupillen waagerecht 1,1–1,2 px | `A0/m0_bild_netz` |
| Graukarte sRGB 118 ± 4, drei Läufe ±1 % | 118,0 / 118,0 / 118,0 | `A0/p10_kalibrier*` |
| Farbtest: 0,5 als `Vector3` → linear 0,5, als `Color` → 0,214 | 0,503 · 0,212 (`StandardMaterial3D` 0,212) | `A0/p10_kalibrier/00_02.png` |
| Brücke: bestehende Gene mit/ohne Vorschlag gleich (1000) | 0 / 0 Abweichungen (6 Völker, Würfeln und zwei Generationen Vererben) | `A0/bruecke_pruefen.json` |
| Brücke: derselbe Startwert, dieselben Bytes | 0 ungleich | ebenda |
| Galerie: Z zeigt 30 verschiedene Gensätze | 30 (Runde 1 und 2) | Log, Bild `galerie_z1/galerie_tag_weit.png` |
| Gewichteausdruck = Szene (< 1e-5, 20 Personen) | größte Abweichung 0,0 bei 20 Personen | `A0/m1_gewichte20`, `gewichte_s0_headless.json` |
| Manifest grün, Notizen gesichert, A0 vollständig | grün (ICT, RoboPoets, MPFB, 7 Zips, Blender, Godot); 175 Dateien gesichert; A0 vollständig | `einrichten.sh pruefen` |

- **M2 (Grundlage, Stand 5, Hoch, 1280 × 720):** Kopf ohne Haar 6,21 / 9,97 / 9,39 ms, leere Bühne 1,44 / 1,97 /
  2,92 ms (drei Wiederholungen im selben Prozess, Commit `b59d5cab`); Phase 5 maß 6,1 ms (unter Last 9,4). Die T400
  war nicht frei (beim Start drei weitere Godot-Prozesse mit 0,9–1,4 GB), `GPU_ANDERE=0` ließ sich nicht einhalten.
  Die gepaarten Messungen ab S2 (Stand 5 gegen 6 abwechselnd im selben Prozess) heben diesen Fehler teilweise auf.
- **Befund statische Farbprüfung:** 12 Stellen übergeben ein Farb-Uniform (`source_color`) als `Vector3`
  (`charakter_kopf.gd`, `charakter_haar.gd`, die Prüfkugel der Bühne absichtlich wie Stand 5) – die Ursache 1 der
  Kurzfassung des Plans, behoben in S2.
- **Hinweis Bild:** P1 unter L-V zeigt, wie blass Phase 5 ist (Wange Mann 35: Chroma 7,5, Licht : Schatten 1,13,
  R/G 1,09 – Ziele S2/S3: Chroma ≥ 14, 2,0–4,0, ≥ 1,25).

## S1 Vergleichsvorlagen (lokale Bild-KI)

Ziel: ein Maßstab für 20, 50 und 70 Jahre, für Hauttöne, Wetterhaut und Statur (die Haar-Vorlagen der Phase 5 zeigen
nur 35–45-Jährige mit einander ähnlichen, idealisierten Gesichtern). Die Bilder liegen nur lokal unter
`~/werkzeuge/charakter/vorlagen-gesicht6/<id>/` (nicht im Repository, nicht im Spiel), mit `liste.md` (Modell, Lizenz,
Zweck, Prompt und Seed je Bild), `auswahl.txt` je Person und dem Blindbogen für die Altersschätzung.

### Ergebnis in Kürze

- **20 Personen, je vorn und ¾ (40 Bilder).** `pruefe6.py`: 40 von 40 Bildern mit Prompt (samt Seed) und Workflow,
  kein LoRA-Knoten, 40 Schritte, CFG 4, Qwen-Image 2512 bzw. Qwen-Image-Edit 2511, Encoder Qwen2.5-VL-7B. Erzeugt
  wurden 37 Vorderansichten (1–3 je Person) und 21 ¾-Ansichten.
- **Blindschätzung des Umsetzers** (Bogen `blind_alter_s1.png`, 20 gemischte Vorderansichten, geschätzt vor dem Öffnen
  des Schlüssels): 17 von 20 auf ±7 Jahre; die Bilder wirken eher älter als verlangt (Mittel +4,0 Jahre, −2 … +10). Die
  drei Fehlgriffe waren die Statur-Bilder mit 40 (hager Mann +8, hager Frau +10, beleibt Frau +8). Sie sind durch zweite
  Kandidaten mit „genau 40, nicht älter“ ersetzt; deren Schätzung (40, 42, 40) ist nicht mehr blind. Endsatz 20 von 20,
  Mittel +2,8 Jahre (−2 … +7). **Die Schätzung des Teamleiters steht aus** (Schlüssel siehe Prüfung S1).
- **Merkpunkte:** 22 je Vorderansicht und die Pupillen jeder ¾-Ansicht; zwei Kontaktbögen (alle Pupillen, alle
  Merkpunkte) zeigen die Lage.
- **P3** legt jede Vorlage auf den Pupillenabstand des Kopfs. Stand 5 wirkt neben allen 20 Personen blass, glatt und
  jung (`p03_vorlagen_s1a` … `_s1d`).
- **Der neue Satz streut** stärker als die Haar-Vorlagen: zy/IPD 1,82–2,33 (alter Satz 2,03–2,14), Mundlage
  0,26–0,39 (0,32–0,35), Braue über der Pupille 0,10–0,24 IPD (0,13–0,15). Die Mundlage der Köpfe (Mann 30 0,22,
  Frau 30 0,25) liegt unter allen 26 Vorlagen – ein Hinweis für S4 (Oberlippe zu kurz oder Kinn zu lang). Die
  Umrissmaße (zy, go) sind Bildmaße vor dem Ohr und nicht mit den Netzmaßen gleichzusetzen.

### Wie der Satz entstand

- **Werkzeug:** ComfyUI 0.42.0 auf dem Grafik-PC (vom Teamleiter gestartet, Startdienst nicht benutzt), Aufruf über
  `_werkzeug/gen.py` (Kopie des Phase-5-Werkzeugs, nur Wartezeit und Dateipräfix geändert). Text → Bild **Qwen-Image
  2512**, ¾-Ansicht derselben Person mit **Qwen-Image-Edit 2511**, Text-Encoder **Qwen2.5-VL-7B** – alle Apache-2.0.
  **Ohne Lightning-LoRA, 40 Schritte, CFG 4, 1024²** (je Bild 2,5–4,5 Minuten Rechenzeit; die Warteschlange teilte
  sich die Karte zeitweise mit einem anderen Auftrag). Negativ-Prompt gegen Schminke, Retusche, Modelgesicht,
  Kopfbedeckung, Schmuck, Gegenlicht; bei Drinnen und den Ersatzbildern um Wetterhaut bzw. Alter ergänzt (in
  `liste.md` je Bild ausgewiesen).
- **Prompt-Regeln** wie die Haar-Vorlagen: mittelgrauer Studiogrund (18 %), weiches Schlüssellicht von links oben,
  Aufhellung von rechts, kein Gegenlicht, Hemd aus ungefärbtem Leinen, keine Kopfbedeckung, keine Schminke, keine
  moderne Frisur, gewöhnliche Gesichter mit sichtbaren Poren. Männer kurz geschnitten (Ohren frei), Frauen mit
  Mittelscheitel und Knoten im Nacken (Stirn und Ohren frei).
- **Satz (20 Personen):** Alter (Mann und Frau 20, 50, 70; mittlerer Hautton), Hauttöne (Mann und Frau 35, hell = Typ
  I–II, dunkler = Typ III–IV), Draußen (Bauer 45, Fischer 55, Bäuerin 45), Drinnen (Schreiber 45, Patrizier 50,
  Kaufmannsfrau 45), Statur (Mann und Frau 40, hager und beleibt).
- **Auswahl** (`auswahl.txt`): gewählt nach Sollalter, gewöhnlichem Gesicht, Licht und Grund wie verlangt, freier Stirn
  und freien Ohren, ohne Schminke und Schmuck. Das Modell macht Menschen eher älter und wettergegerbter als verlangt:
  Mann 20, Frau 20 und Frau 50 brauchten einen dritten Kandidaten mit nachgeschärftem Alterstext, Patrizier und
  Kaufmannsfrau einen zweiten mit deutlicherem „drinnen, blass“, die drei Statur-Bilder (siehe oben) einen zweiten
  mit „genau 40“. Mann 50/70, Frau 70 und die Hauttöne: von zwei Kandidaten der geradere bzw. jüngere.
- **¾-Ansicht:** nach rechts gedreht wie die Haar-Vorlagen, dieselbe Person und dasselbe Licht, je ein Kandidat
  (Kaufmannsfrau zwei). Das Edit-Modell dreht meist weiter als verlangt (30–55°, im Mittel 42°, verlangt waren 30°);
  P3 nimmt deshalb je Vorlage den Winkel aus dem verkürzten Pupillenabstand (cos θ = IPD ¾ / IPD vorn, auf 5°
  gerundet) und dreht den Kopf genauso (K2 gespiegelt). Abweichungen von der Vorderansicht: die Bäuerin lächelt leicht
  (wie vorn), die Kaufmannsfrau wirkte im ersten Kandidaten zehn Jahre älter (zweiter Kandidat mit ausdrücklich
  gleichem Alter gewählt).
- **Merkpunkte** (`tools/charakter/messen/vorlagen_merkpunkte.json`): von Hand am Koordinatenraster gesetzt
  (`messen/raster.py`, Arbeitsblätter mit Ausschnitten für Augen, Nase/Mund, Kinn und Seite). Pupillen der
  Vorderansichten auf den Schwerpunkt der dunkelsten Pupillenpunkte nachgeschärft; die Pupillen der ¾-Ansichten von
  Hand mit vierfacher Lupe gesetzt, ohne Nachschärfen (in ¾ fand das Nachschärfen oft Lid, Wimpern oder Schatten; von
  den ersten 15 ¾-Einträgen lagen 9 um 5–50 px daneben, darunter drei der alten Haar-Vorlagen). Geprüft mit
  Kontaktbögen aller Pupillen und Merkpunkte (`A0/s1_kontakt/`; Hilfsskripte für Lupe, Kontaktbögen und Eintragen
  lokal unter `vorlagen-gesicht6/_werkzeug/merkpunkte/`); dabei sind eine Vorderansicht (Mann 40 beleibt: Pupille auf
  dem Lid, Nase und Mund 25 px zu tief) und eine Pupille (Frau 40 beleibt) berichtigt. Genauigkeit innen (Augen, Nase, Mund, Kinn) etwa ±5 px, ¾-Pupillen ±2 px, Umrisspunkte (zy, go) ±10 px;
  auf der Schattenseite hebt sich der Umriss kaum vom Grund ab, dort ist er an der Mittellinie der Pupillen
  gespiegelt. Die Merkpunkte der Haar-Vorlagen aus der Bestandsaufnahme waren teils um 10 px versetzt (Pupille m05
  408 statt 397, Lippenschluss 10 px zu tief) und sind neu gesetzt.

### Prüfung S1

| Abnahme S1 | Ergebnis | Beleg |
|---|---|---|
| Alle Bilder mit Prompt, Seed und Workflow; kein Lightning-LoRA | 40 von 40 (Modell, Encoder, 40 Schritte, CFG 4 geprüft) | `_werkzeug/pruefe6.py`, `liste.md`, `<id>/*.workflow.json` |
| Höchstens 3 Kandidaten je Vorderansicht, Wahl begründet | 1–3 (37 Vorderansichten), Begründung je Person | `<id>/auswahl.txt`, `<id>/kandidaten/` |
| Zwei Schätzer treffen das Sollalter blind auf ±7 Jahre | Umsetzer: 17 von 20 blind, nach Ersatz der drei Fehlgriffe 20 von 20 (diese drei nicht blind); Teamleiter: **offen** | `blind_alter_s1.png`, `schaetzung_umsetzer_s1.json`, Schlüssel `schluessel_s1_alter.json` (SHA-256 `fb0ec6d4151efb4a06932a4496cd07c0fa06a0a0e597b0e3ec1ef053c5d2df1b`, mit SHA-256 jedes gezeigten Bilds) |
| Der Nutzer gibt den Satz an Halt A frei oder streicht Bilder | **offen** (Halt A) | P3-Bögen in A0 |
| Merkpunkte der neuen Vorlagen gesetzt | 20 von 20 vorn (22 Punkte), 20 von 20 ¾ (Pupillen) | `vorlagen_merkpunkte.json`, Kontaktbögen |

Der Schlüssel der ersten, ganz blinden Runde (ohne Bildprüfsummen, mit den drei ersetzten Bildern) hatte den SHA-256
`852f1c5bc0956805793e707135e12e4f0cfe73b1b387d6940f46b83a4fc0453c`; Nummern und Sollalter sind gleich geblieben
(fester Startwert). Für den Teamleiter: erst `blind_alter_s1.png` schätzen und die Zahlen festhalten, dann den
Schlüssel öffnen.

## S2 Grundfehler und S3 Porträtlicht

Stand 6 ist ab jetzt ein eigener Weg neben Stand 5: `glb6/kopf6.glb`, `haut6`, `augen6`, Farben als `Color`, vorläufige
Paletten, Porträtlicht L-S neu. Stand 5 bleibt Phase 5 genau (glb5, haut3, augen3, alte Übergabe, altes L-S). Prüfmappe
**A1** = Stand 6 nach S3 unter `~/werkzeuge/charakter/bilder/phase6/A1/` (gleiche Kameras und Bögen wie A0, dazu die
S2/S3-Bögen), Vergleichsblatt `vergleich_a0_a1.png` (A0 | A1 | Fotovorlage).

### Ergebnis in Kürze

- **Farben richtig umgerechnet.** Jede Farbe geht über `Kopf.farbe_setzen()` als `Color` an den Shader (Haut, Braue,
  Wimpern, Haar, Haarkarte, Iris, Kranz, Lederhaut, `haar_unter`, Zahnfleisch, TressFX samt Goldnetz und Perlen);
  statische Prüfung: 0 Stellen mit `Vector3` an einem Farb-Uniform. Farbtest über die Hilfsfunktion = `StandardMaterial3D`
  (127,0 / 127,0). Haar, Brauen und Bart sind jetzt so dunkel wie im Code gemeint (E4 an Halt A, Farbtafeln P11).
- **Augen** (Lederhaut berichtigt im Nachtrag)**:** Iris-Leuchtdichte Mann 35 unter L-V 0,046 statt 0,170 (0,27 ×, Ziel ≤ ⅓); Lederhaut 40 L* unter der
  beleuchteten Wange (Ziel ≤ +5).
- **Glanz wie Godot:** Prüfkugel (Wrap 0, eine Keule) gegen `StandardMaterial3D` bei 0/30/60/80°: 0,99 / 1,00 / 1,00 /
  1,01; Streulicht gegen Lambert 0,995–1,000; Streiflicht (150°, Summe über die Kugel) 9,7 × Phase 5 – berichtigt im Nachtrag: gewertet wird 80° (1,32, nicht erreicht).
- **Formnormalen** für Gen-, Alters- und alle neuen Formen, dünn gespeichert: `kopf6.glb` 26,8 MB (kopf5_mn 53,6 MB,
  kopf5 33,2 MB); dicht gegen dünn: Normalen und Lagen ≤ 1,0e-4, Bild ≤ 0,03/255. `makro_` bleibt **ohne** Normalen
  (A/B: Säugling und Kind bekommen sonst Brustwarzen-Buckel der Erwachsenenform, Bogen `s2_ab_formen`).
- **Nicht erreicht (gelb, Zeitkasten nach Risiko 17):** Wange unter L-V Chroma 8,4 (Ziel 14), a* 4,9 (Ziel 8); Licht :
  Schatten unter L-V 1,15 (Ziel 2–4), R/G 1,11/1,14 (Ziel 1,25/1,30); A/B Kiefer und Kinn mit Normalen 5,5 Prozentpunkte
  **heller** statt ≥ 8 dunkler. Die Ursachen sind gemessen (siehe unten) und liegen nicht im Shader: das Fülllicht von
  L-V, AgX und die Gesichtsform (S4). Chroma ist die Frage E5 an Halt A, das Fülllicht von L-V eine neue Designfrage.

### Was gebaut ist

- **Stand-Schalter** (`charakter_kopf.gd`): `stand_setzen(n)` tauscht an einem geladenen Kopf die Shader (haut3 ↔ haut6,
  augen3 ↔ augen6, auch Ultra) und setzt die Farben neu; die Schau wählt mit `"kopf": "standard"` je Stand die Datei
  (Stand 6 `kopf6.glb` im `--glb`-Ordner, Stand 5 `kopf5.glb`, notfalls aus `--glb5`, Standard `glb5`). Ein Bogen mit
  Stand 5 und 6 läuft so in einem Prozess; die Galerie bleibt bei Stand 5.
- **Farben** (S2.1): `farbe_setzen(stoff, name, color, stand)` – Stand 6 `Color`, Stand 5 `Vector3` wie Phase 5 (mit
  `# stand5` markiert, die statische Prüfung zählt diese Zeilen nicht). `linear(c)` übergibt eine lineare Albedo so, dass
  sie nach Godots Umrechnung genau linear ankommt. Faktoren wie `toenung` bleiben `Vector3`. Das Haarskript hat dazu
  `stand`; der Timer, der TressFX einfriert, ruft jetzt eine gebundene Methode mit `weakref` statt eines Lambdas
  (Meldung „Lambda capture at index 0 was freed“ in A1 nicht mehr aufgetreten).
- **Vorläufige Paletten** (S2.2, endgültig in S9 bzw. S7): Hautfarbe linear nach PhysicallyBased (CC0, Haut I–VI):
  Gen 235 ≈ I–II (0,823/0,562/0,450), 185 ≈ II–III (0,711/0,459/0,345), 135 ≈ III–IV (0,530/0,330/0,237), darunter IV,
  V, VI; Iris linear (dunkelbraun 0,030/0,014/0,006 – Bernstein – Grün – Blau 0,075/0,12/0,20), Kranz, Lederhaut warm
  linear 0,50/0,44/0,38 (vorher Standardwert des Shaders), breiterer Limbusring (`limbus` 0,75, `limbus_breite` 0,25),
  Zahnfleisch linear 0,46/0,13/0,12. Haaranker bleiben als Zahlen und werden jetzt umgerechnet (dunkler).
- **haut6** (`haut6.gdshaderinc`, `haut6.gdshader`, `haut6_ultra.gdshader`; haut3 unverändert): Glanz mit Godots
  `D_GGX` und `V_GGX` (Smith, korreliert), Schlick-Fresnel mit F0 0,028 bis 1, ohne zweites `/ PI`, `SPECULAR_AMOUNT`
  nur als Lichtfaktor; Glanzmaske (Höhlen × Falten × Haar) als `varying spec_v` auch im direkten Licht, `SPECULAR =
  0,42 · spec_v`; `glanz` ist jetzt ein Faktor (Stand 6: 1), `keule2` der Anteil der schmalen Keule (0,18; Prüfkugel 0);
  Untergrenze der Rauheit 0,10; Ultra mit `render_mode … sss_mode_skin` und Wrap × 0,35; Zweig `LIGHT_IS_AREA` nach der
  4.7-Doku. S3: Wrap energieerhaltend (n·l + w)/(1 + w)², `flaum` 0,05, `streu_schattenfrei` 0,3.
- **augen6** (Kopie von augen3): Lederhaut aus dem Skript, Limbusring einstellbar.
- **kopf6.sh** (Kopie von kopf5.sh): Ausgabe `glb6`, Arbeitsordner `arbeit/phase6`; `glb_formnormalen.py … duenn=1`
  entfernt die Normalen nur noch von `makro_` (mit `MAKRO_NORMALEN=1` auch dort nicht) und schreibt Lage und Normale aller
  Formen als dünne Accessoren (|Δ| > 1e-4; ein Accessor wird nur dünn, wenn er dadurch kleiner wird). Haar: die
  Strähnen (`<stil>.json/.bin`) und `haar_karten.png` aus `glb5/haar4` werden verknüpft, die Übersicht kopiert, Stützpunkte
  und Kopfhautkarten mit `nur_kopfhaut=1` neu gerechnet; `kopf6.sh kaemmen` verweigert, solange Verknüpfungen bestehen
  (geprüft: Rückgabe 2), `kopf6.sh haar_kopieren` ersetzt sie vorher durch Kopien. Bau 9,6 min (CPU). Die Formen von
  kopf6_mn sind mit kopf5_mn identisch (Abweichung 0,0), die Stützpunkte und Kopfhautkarten auch.
- **Porträtlicht L-S Stand 6** (`charakter_buehne.gd`, „spiel“ richtet sich nach dem Stand; „spiel5“, „spiel6“,
  „spiel6_weich“ ausdrücklich): Belichtung 0,8, Schlüssellicht 2,3 (≈ 49° aus der Achse wie bisher), Fülllicht 0,45 →
  0,25, Gegenlicht 1,6 → 0,8, Umgebungslicht und Spiegelung aus dem Studio-HDRI Poly Haven „Studio Small 09“ (CC0, 1k,
  im Manifest mit SHA-256; `~/werkzeuge/charakter/hdri`, nicht im Repository), Stärke 0,2 über
  `PanoramaSkyMaterial.energy_multiplier`, sichtbarer Hintergrund flach. Weiche Schatten über `shadow_blur` 2,0. L-I für
  Stand 6 mit Belichtung 1,0 statt 1,25, sonst wie bisher.
- **Prüfstand**: Prüfkörper `kugel6_glanz`/`kugel6_streu` (haut6 | StandardMaterial3D | haut3, orthografisch), Lichtart
  `kugeltest`, Bogenschlüssel `fuell` (Fülllicht) und `bild` (fertiges Bild in einer Zelle), gepaarte Messung
  `--messen --staende=5,6 [--licht=…]`; Bögen `p10b_kugeln`, `s2_ab_mann30`, `s2_ab_formen`, `s2_duenn`, `s2_ultra`,
  `s3_e5_agx`, P10 mit Zeile Stand 6; `pruefen6.py` wertet sie aus (`--a0=` für die Iris gegen Stand 5);
  `messen/duenn_vergleich.py`, `messen/vergleichsblatt.py`.

### Selbst getroffene Entscheidungen

- **`makro_` ohne Normalen**: Mit den Normalen der Alters-Geschlechts-Formen zeigen Säugling und Kind unter Streiflicht
  Buckel der Erwachsenenbrust und ein Kind 5 Brustwarzen (Bogen `s2_ab_formen`, Spalte „Büste mit“, Bildunterschied
  bis 74/255); Gesicht und Erwachsene sind unauffällig. Gen- und Altersformen (`gen_`, `alter_`) behalten die Normalen.
- **Schattenweichzeichnung statt Winkelgröße**: Mit `light_angular_distance` 3° verschwand der Schlagschatten von Kopf und
  Kinn auf dem Kragen fast ganz (Probe `s3_probe_ls2`); `shadow_blur` 2,0 behält ihn weich. Die Winkelgröße bleibt als
  „spiel6_weich“ für die Messung.
- **HDRI-Stärke über den Himmel**: `ambient_light_energy` 0, 0,35 und 3 ergaben bei Himmel als Quelle dasselbe Bild
  (Probe `s3_probe_hdri`, Abweichung 0,0) – Godot backt den Himmel beim ersten Bild. Die Stärke steht deshalb im
  `energy_multiplier` des Himmels (0,2), `ambient_light_energy` = 1.
- **Prüfkugel**: orthografische Kamera (in der Perspektive sieht die linke Kugel 5° schräger als die mittlere, das
  verfälschte den Streulichtvergleich um bis zu 15 %); „Streiflicht“ gewertet bei 150° zwischen Licht und Blick (Licht
  von hinten seitlich, Fresnel am Rand) als Summe über die Kugel – bei 80° liegt der Glanzfleck bei L·H = cos 40°, dort
  ist Fresnel noch nicht wirksam (1,28 ×), und die schmale Keule von haut3 übersteuert die Spitze.

### Befunde zu den nicht erreichten Werten

- **Licht : Schatten unter L-V (1,15)** hängt am Licht, nicht am Shader: haut6 mit Wrap 0,4 / 0,2 / 0 (Lambert) und ohne
  Streulicht jenseits des Schattens ergibt 1,15 / 1,18 / 1,22 / 1,22; dasselbe ohne Fülllicht 2,22 (Probe
  `s3_probe_ls`). Das Fülllicht von L-V (Energie 1 : 3, von rechts 50°/10°) trifft die abgewandte Wange fast senkrecht,
  das Schlüssellicht (45° Höhe) beide Wangen ähnlich; dazu staucht AgX die Helligkeitsunterschiede. **Empfehlung an den
  Teamleiter:** „Aufhellung 1 : 3“ als Lichtverhältnis (Schattenseite = ⅓ der Lichtseite) statt als Energieverhältnis
  verstehen und L-V einmal neu eichen (Fülllicht ≈ 0,3, Schlüssellicht flacher, ≈ 30° Höhe), dann A0 unter dem neuen
  L-V neu rendern. Ohne diese Entscheidung bleibt L-V wie in S0 geeicht.
- **Chroma und R/G** hängen an AgX: das Hautfeld Typ III (Albedo C 15,7) erscheint unter L-V mit C 9,3; die Albedo der
  Wange (Anzeige „albedo“) hat C 18,2 und R/G 1,22, beleuchtet C 8,4 und R/G 1,11. Unter L-S erreicht die Wange C 13,0;
  mit Sättigung 1,25 C 16,4, mit 1,5 C 19,9, AgX-Kontrast 1,5 ändert nichts (Bogen `s3_e5_agx`). Das ist die
  Entscheidung **E5** an Halt A.
- **Formnormalen A/B (Mann 30, L-V)**: Kiefer und Kinn werden mit Normalen 5,5 Prozentpunkte heller statt dunkler. Die
  Messfelder liegen auf den Flächen des Unterkiefers, die die Reifeform nach oben zum Schlüssellicht (45° Höhe) dreht;
  sichtbar schärfer werden die Kieferkante und der Schatten darunter (`s2_ab_mann30`). Das Kriterium setzt ein
  markanteres Untergesicht voraus und wird mit S4 (Kieferkante, Normalenknick) erneut geprüft.

### Prüfung S2 und S3

| Abnahme | Ergebnis | Beleg |
|---|---|---|
| Farbtest: Hilfsfunktion = StandardMaterial3D ±2/255 | 127,0 / 127,0 | `A1/p10_kalibrier/01_02.png` |
| kein `Vector3` an einem Farb-Uniform | 0 Stellen | `A1/pruefen6.md` |
| Iris ≤ ⅓ von Phase 5 (L-V) | 0,046 / 0,170 = 0,27 | `p01_alter_ton_k1_lv` |
| Lederhaut ≤ +5 L* gegen die Wange (L-V) | −39,7 – **Messweg falsch, berichtigt im Nachtrag** | ebenda |
| Wange Mann (L-V): Chroma ≥ 14, a* ≥ 8 | **8,4 / 4,9 – nicht erreicht** (E5) | ebenda, `s3_e5_agx` |
| Prüfkugel Streulicht = Lambert ±3 % | 0,995 … 1,000 | `p10b_kugeln` |
| Glanzspitze = StandardMaterial3D ±10 % (0–60°), ±20 % (80°) | 0,987 / 0,996 / 1,000 / 1,009 | ebenda |
| Streiflichtglanz ≥ 2,5 × Phase 5 | 9,7 (150°) – **Umdeutung, berichtigt im Nachtrag: 80° 1,32, nicht erreicht** | ebenda |
| A/B Kiefer und Kinn ≥ 8 Pp. dunkler | **+5,5 Pp. heller – nicht erreicht** (S4; gemessen an kopf5/haut3, am ausgelieferten kopf6 im Nachtrag) | `s2_ab_mann30` |
| P7 ohne Dellen und Grate; Entscheidung `makro_` | Gen- und Altersnormalen sauber; `makro_` ohne Normalen | `s2_ab_formen`, `p07_*` |
| kopf6.glb ≤ kopf5_mn.glb | 26,8 / 53,6 MB | `duenn_vergleich.json` |
| Bild dünn = dicht ≤ 1/255, Normalen < 1e-3 | ≤ 0,03/255; 1,0e-4 | `s2_duenn`, `duenn_vergleich.json` |
| Ultra ohne grauen SSS-Saum | kein Saum sichtbar (Stand 6 Ultra, K4 und N1) | `s2_ultra` |
| keine SCRIPT- oder SHADER-ERROR | keine in allen A1-Läufen | `A1/logs/` |
| M2 gepaart Hoch ohne Haar Δ ≤ +0,3 ms (S2) | **nicht belastbar (Nachtrag)** – +0,07 ms (Licht spiel5, Median aus 3: −0,47 / +0,43 / +0,07) | `A1/messung.jsonl` (Licht spiel5) |
| L-V: Licht : Schatten 2–4, R/G ≥ 1,25 / 1,30 | **1,15; 1,11 / 1,14 – nicht erreicht** (Fülllicht L-V, AgX) | `p01_alter_ton_k1_lv`, `s3_probe_ls` |
| L-S: dieselben Verhältnisse ±20 % | 1,40 (+22 %), R/G 1,13 / 1,21 (+2 / +6 %) | `p01_alter_ton_k1_ls` |
| kein Fleck vor dem Ohr (¾, L-S) | keiner | `p01_alter_ton_k2_ls` |
| M2 Hoch Δ ≤ +0,5 ms (mit weichen Schatten) | **offen (Nachtrag)** – ohne Haar −0,96 ms (spiel), +0,22 ms mit Winkelgröße 3° (spiel6_weich); **TressFX in Ruhe +2,92 ms unter spiel – nicht belastbar** (unter spiel5 −0,07, unter spiel6_weich +0,17; T400 geteilt, siehe unten) | `A1/messung.jsonl` |
| Prüfmappe A mit P1, P3, P7, P10, P11, M1, M2 | vollständig | `A1/` |

- **M2 (Hoch, 1280 × 720, gepaart Stand 5/6 im selben Prozess, je 3 Wiederholungen):** Die T400 war nicht frei
  (`GPU_ANDERE=0` nach 15 min Wartezeit nicht erreicht, zeitweise zwei fremde Vulkan-Prozesse; einzelne Läufe mit
  300 ms für TressFX). S2 allein (beide Stände unter dem Licht der Phase 5) kostet nichts Messbares, das Licht der S3
  mit Winkelgröße +0,2 ms. Der Ausreißer +2,9 ms für TressFX unter „spiel“ (Schattenweichzeichnung) steht gegen +0,2 ms
  derselben Haare unter „spiel6_weich“; er wird bei freier Karte wiederholt (offen).
- **Vergleichsblatt** `A1/vergleich_a0_a1.png`: Mann und Frau 35 (hell, H1, Vorlagen `h_m35_hell`, `h_w35_hell`) und
  70 (mittel, H2, `a_m70`, `a_w70`), P1 K1 unter L-V – die Vorlagen mit 35 Jahren gibt es nur hell und dunkler.

### Testanleitung S2/S3

```bash
# Kopf Stand 6 bauen (glb6, ≈ 10 min CPU; Haar aus glb5 verknüpft)
tools/charakter/kopf6.sh
# A1 rendern (Vulkan, T400): Stand 6 für alle Zellen ohne eigenen Stand
GLB=~/werkzeuge/charakter/glb6 ANZEIGE=:98 tools/charakter/schau.sh <ordner> 4200x6100 --stand=6 \
   --bogen=tools/charakter/boegen6/p10b_kugeln.json,tools/charakter/boegen6/s2_ab_formen.json --ende
# gepaarte Messung Stand 5 gegen 6 (Licht nach Stand; spiel5 = nur S2; spiel6_weich = Winkelgröße 3°)
GLB=~/werkzeuge/charakter/glb6 GPU_ANDERE=0 tools/charakter/schau.sh <ordner> 1280x720 --messen --staende=5,6 \
   --wiederholungen=3 --nur=leer,ohne_haar,haar_tressfx_ruhe --licht=spiel --ende
# dicht gegen dünn
$BPY -I tools/charakter/messen/duenn_vergleich.py <dicht.glb> ~/werkzeuge/charakter/glb6/kopf6.glb
# Vergleichsblatt A0 | A1 | Fotovorlage (Bogen mit fertigen Bildern, Beschriftung durch die Testszene)
$BPY -I tools/charakter/messen/vergleichsblatt.py --a=<A0> --b=<A1> --aus=<ordner>/vergleich_a0_a1
GLB=~/werkzeuge/charakter/glb6 tools/charakter/schau.sh <ordner> 2200x3100 --bogen=<ordner>/vergleich_a0_a1.json --ende
# Prüfung
$BPY -I tools/charakter/messen/pruefen6.py --mappe=~/werkzeuge/charakter/bilder/phase6/A1 \
   --glb=~/werkzeuge/charakter/glb6/kopf6.glb --a0=~/werkzeuge/charakter/bilder/phase6/A0
```

## Nachtrag nach der Prüfung von Etappe A

Unabhängige Prüfer haben Prüfmappe A1 nachgerechnet; elf Befunde wurden bestätigt. Prüfmappe **A2** (Stand 6 nach den
Befunden) und **A0b** (Stand 5 unter dem neuen Messlicht, Bögen P1 K1/K2 L-V und P10) liegen unter
`~/werkzeuge/charakter/bilder/phase6/`. Für Halt A gibt es eine kurze Mappe mit Vergleichsblatt, Farbtafeln, Bühnenlicht
und `halt_a.md` (außerhalb des Repositorys).

### Befunde und was geschah

| Befund | Ursache | Behoben bzw. berichtigt |
|---|---|---|
| Punktraster in allen Schatten unter L-V (Schattenwange, Augenhöhle, Ohr, Kragen), auch um die Augen | Winkelgröße 3° des Schlüssellichts (weiche Schatten über PCSS) streute einzelne dunkle Punkte | L-V weich über die Schattenweichzeichnung 2,0 wie L-S, ohne Winkelgröße; A0 (als A0b) und A1 (als A2) neu gerendert, Raster verschwunden (`halt_a/3_buehnenlicht.png`); Graukarte weiter 118,0 |
| Lederhaut-Messung traf zur Hälfte Schwarz, Kriterium bestand schon mit Stand 5 | Feld 0,13–0,20·IPD lag auf Lidwinkel und Wimpern, Mittelwert | Feld 0,085–0,15·IPD (zwischen Irisrand und Lidwinkel), 80. Perzentil, Anteil schwarzer Punkte ausgewiesen. Neu: Stand 6 −10,3 L*, Stand 5 −8,9 L* gegen die Wange (L-S: −7 bzw. −5). Beide erfüllen „≤ +5“; das Kriterium trennt unter dem Messlicht nicht – die Annahme der Bestandsaufnahme (Lederhaut heller als Haut) galt nur unter dem alten Licht. Stand 6 senkt die Lederhaut um 6–9 L* |
| Streiflicht nachträglich bei 150° gewertet | – | gewertet wird 80° (N5): **1,32 × Phase 5, Ziel 2,5 nicht erreicht**. Mit F0 0,028 und Godots GGX (Glanzspitze 0,99–1,01 × `StandardMaterial3D`) ist das Ziel nicht erreichbar, weil haut3 seine Spitze mit einer schmalen zweiten Keule überzeichnete; erneut in S8 (Doppelglanz, Feuchte) |
| haut6 am Rand 1,7 × so hell wie Godot (150°) | Messfehler: Maske nur 0,9 Kugelradien (der Randglanz liegt außerhalb) und Bild bei Energie 1 übersteuert (Spitze 1,0) | ganze Kugel, Streiflichtzeile mit Energie 0,3 (Spitze 0,54): haut6 : Standard **1,16** (±20 %), als Zusatz geführt |
| Last immer 0,0, M2 trotz geteilter Karte „erfüllt“ | `get_file_as_string("/proc/loadavg")` liefert leer | Last zeilenweise gelesen, fremde GPU-Prozesse (nvidia-smi) je Messung; `schau.sh --messen` mit `GPU_ANDERE=0` bricht ab, wenn die Karte nicht frei wird; `pruefen6` wertet nur belastbare Läufe (Karte frei, Last protokolliert, Streuung unter der Grenze). Neu gemessen bei freier T400, siehe unten |
| M2 S3 trotz +2,9 ms „erfüllt“ | – | bei freier Karte bestätigt: **Abweichung**, Ursache gemessen (unten) |
| Haut fahl, Erklärung „AgX“ unvollständig | Der Wrap je Farbkanal (n·l + w)/(1 + w)² nahm dem Rot im vollen Licht bis zu 17 % (Wange R/G Albedo 1,22 → beleuchtet 1,11) | Wrap im vollen Licht farbneutral: (n·l + w)/(1 + w) je Kanal geteilt durch (1 + w) der Helligkeit, Farbtrennung des Wraps 0,3 (`wrap_neutral`, `streu_farbe`). Wange Mann 35 (H2) L-V Chroma 8,4 → **10,2**, a* 4,9 → 6,3; L-S 13,0 → **14,8**. Kosten gemessen: Glanz 0 +0,4 C, Fülllicht 0 −0,2 C, Lambert statt Wrap +1,1 C; der Rest ist AgX (Hautfeld Typ III: Albedo C 15,7 → 9,3) |
| Oberlid orange | Derselbe Wrap: am Schattenrand (Oberlid unter der Braue, n·l ≈ 0) bekam Rot das 1,5-Fache von Grün; Brauenschatten, Rötung und Lidschatten-Netz sind es nicht (je ausgeblendet: keine Änderung) | mit der Farbtrennung 0,3 Oberlid L-V Chroma 24,0 → **19,6** (Wange 10,2, also 1,9-fach statt 2,9-fach). Ziel ≤ 1,5-fach **nicht ganz erreicht**: die Albedo der Lidzone ist selbst satter (C 22,5 gegen Wange 18,1), und AgX entsättigt die helle Wange stärker als das dunkle Lid; Farbton des Lids in S9 (Pigmentzonen) bzw. S12 (Augenhöhle) |
| A/B Formnormalen und Gratbogen nicht am ausgelieferten Kopf | Bögen nutzten kopf5/kopf5_mn mit haut3 | `kopf6_ohne.glb` (alle Formnormalen entfernt, nur lokal) gegen `kopf6.glb`, Stand 6: Kiefer **+6,6**, Kinn **+6,5** Pp. heller statt ≥ 8 dunkler – **nicht erreicht** (S4, Kieferkante). Neuer Bogen `p07b_grat_naht6`: keine Grate, Büstennaht mit/ohne Normalen im Mittel 0,03–0,7/255 (Säugling, Kind ohne Brustbuckel, weil `makro_` ohne Normalen bleibt) |
| S1: Außen- und Innenarbeiter 45 gleich alt und gleich gebräunt; Bäuerin ¾ blass | Prompts des Schreibers und der Kaufmannsfrau zu schwach | ComfyUI ist gestoppt (nur der Teamleiter startet es). Neue Prompts liegen bereit (`vorlagen-gesicht6/_werkzeug/personen6.py`, `NACHTRAG`, Eintrag in `liste.md`); bis zur Neuerzeugung gelten Schreiber 45, Kaufmannsfrau 45 und Bäuerin ¾ nicht als Maßstab für Wetterhaut (P6). Empfehlung an Halt A |

### M2 bei freier T400

Gepaart im selben Prozess, je 3 Wiederholungen, keine fremden GPU-Prozesse, Last 1,4–4,9 (nur der erste Lauf unter
„spiel“ hatte kurz einen fremden Prozess und wurde wiederholt):

| Licht | ohne Haar Δ | TressFX in Ruhe Δ | Aussage |
|---|---|---|---|
| spiel5 (beide Stände Licht Phase 5) | +0,15 ms | +0,12 ms | Kosten von haut6/augen6 (S2), Grenze 0,3 ms erfüllt |
| spiel (Stand 5 Licht Phase 5, Stand 6 Licht S3) | **+0,75 ms** | **+2,80 ms** | Shader + neues Porträtlicht, Grenzen 0,5 / 1,2 ms **überschritten** |
| spiel6_b12 (beide Stände Licht S3, Weichzeichnung 1,2) | +0,19 ms | +0,13 ms | Weichzeichnung 2,0 gegen 1,2 kostet nichts Messbares |
| spiel6_ohne_himmel (beide Licht S3 ohne HDRI) | +0,17 ms | +0,10 ms | Stand 6 absolut 6,36 / 28,2 ms statt 6,84 / 29,7 ms mit HDRI |

Ursache der Überschreitung ist das **Studio-HDRI** als Umgebungslicht und Spiegelung (≈ +0,5 ms ohne Haar, ≈ +1,5 ms
mit TressFX), nicht die Schattenweichzeichnung. Ohne HDRI liegt das neue Porträtlicht bei +0,23 ms (ohne Haar) bzw.
+0,7 ms (TressFX) gegen Phase 5. Entscheidung an den Teamleiter (Empfehlung: HDRI nur für Spiegelung oder als feste
Umgebungsfarbe aus dem HDRI gemittelt, in S14 messen). Die frühere Aussage „spiel6_weich +0,2 ms“ verglich nur die
Shader (Stand 5 bekam dort dasselbe Licht).

### Weiter offen nach dem Nachtrag

- Licht : Schatten unter L-V 1,15 (Ziel 2–4): unverändert; die Neueichung des Fülllichts ist die offene Designfrage an
  den Teamleiter (Bericht S2/S3).
- Wange L-V Chroma 10,2 (Ziel 14): mit E5 Sättigung 1,5 unter L-V 15,7; unter L-S erreicht schon Sättigung 1,25
  Chroma 18,7 (Ziel 17). E5 an Halt A.
- Streiflicht 80° 1,32 (Ziel 2,5): S8. A/B Kiefer und Kinn: S4. Oberlid-Farbton: S9/S12.

### Testanleitung Nachtrag

```bash
# A2 und A0b rendern (Vulkan, T400; Stand 5 unter dem neuen L-V nur P1 L-V und P10)
GLB=~/werkzeuge/charakter/glb6 tools/charakter/schau.sh <ordner> 4200x4700 --stand=5 \
   --bogen=tools/charakter/boegen6/p01_alter_ton_k1_lv.json,tools/charakter/boegen6/p10_kalibrier.json --ende
# kopf6 ohne Formnormalen für den A/B-Vergleich (nur lokal)
python3 -I tools/charakter/glb_formnormalen.py ~/werkzeuge/charakter/glb6/kopf6_mn.glb ~/werkzeuge/charakter/glb6/kopf6_ohne.glb \
   ohne_vorsilben=makro_,gen_,alter_ duenn=1
# M2 nur bei freier Karte (bricht sonst mit Rückgabe 4 ab); Licht spiel6_ohne_himmel und spiel6_b12 zum Eingrenzen
GLB=~/werkzeuge/charakter/glb6 GPU_ANDERE=0 tools/charakter/schau.sh <ordner> 1280x720 --messen --staende=5,6 \
   --wiederholungen=3 --nur=ohne_haar,haar_tressfx_ruhe --licht=spiel6_ohne_himmel --ende
# Prüfung
$BPY -I tools/charakter/messen/pruefen6.py --mappe=~/werkzeuge/charakter/bilder/phase6/A2 \
   --glb=~/werkzeuge/charakter/glb6/kopf6.glb --a0=~/werkzeuge/charakter/bilder/phase6/A0b
```

## Etappe B: Form und Vielfalt (S4–S7)

Stand nach den Entscheidungen von Halt A (31.10.2026): neue, dunklere Haar-, Brauen- und Augenfarben bleiben (E4 a);
Sättigung 1,25 nur auf der Porträtbühne, die Spielwelt bleibt (E5); Fotovorlagen freigegeben (Schreiber 45,
Kaufmannsfrau 45 und Bäuerin ¾ erzeugt der Teamleiter neu); Studio-HDRI nur auf Hoch und Ultra; das Fülllicht von L-V
als Lichtverhältnis neu geeicht. Prüfmappe **B** unter `~/werkzeuge/charakter/bilder/phase6/B/`.

### Halt-A-Nachträge an der Bühne

- **Sättigung** (`charakter_buehne.gd`): „spiel6“ setzt `adjustment_saturation` 1,25. Wange Mann 35 (H2) unter L-S:
  Chroma 14,8 → **18,7** (Ziel ≥ 17), R/G 1,19 / 1,28.
- **HDRI nur Hoch/Ultra**: Bogenschlüssel `stufe` (Standard „hoch“, mit `ultra` „ultra“); Niedrig und Mittel bekommen
  eine feste Umgebungsfarbe = Mittel des HDRI × 0,2 und keine Spiegelung des Himmels. Mittel gegen Hoch: Chroma 19,0
  gegen 18,7, Licht : Schatten 1,41 gegen 1,40 – kaum sichtbar verschieden.
- **L-V als Lichtverhältnis 1 : 3**: Fülllicht so, dass auf einer Wange 40° zur Schattenseite ⅓ der Beleuchtung der
  Lichtwange ankommt (Lambert, Schlüssellicht 45°/45°, Fülllicht 50°/10°): **0,453** statt 1,0; Belichtung aus der
  Graukarte nachgeführt (0,435 · E_alt / E_neu = **0,514**): Graukarte wieder sRGB **118,0**. Licht : Schatten der
  Wange (Mann 35, linear aus dem Bild) **1,15 → 1,32** – das Ziel 2–4 bleibt unerreichbar, weil AgX die
  Helligkeitsunterschiede staucht (Beleuchtung 3 : 1 ergibt im Bild 1,32 : 1). Lichtart „vorlage_a“ = L-V der
  Etappe A zum Vergleich.

### Bauweg: Probebau und Mischung

Statt einer Reifeform, die bei jeder Korrektur neu gebaut werden muss, legt **ein Probebau** (`kopf6.sh proben`,
`~/werkzeuge/charakter/glb6p/kopf6p.glb`, 71 s CPU) jedes Kandidatenziel als eigene Form an:

- 180 MakeHuman-Probeformen (`kopf6_proben.json`, l-/r-Paare zusammen, CC0), darunter alle Ziele der Reifeformen der
  Phase 5 und 46 asym-Ziele;
- die eigenen Formen direkt auf dem ICT-Netz (`kopf6_eigen.py`, Option `eigen=1`): Kieferkante (3,5 mm), Unterwange
  (4 mm), Jochbein (1,5 mm), Lidfalte Mann/Frau (weiche Rinne σ 1,6 mm, 1,0 mm tief, 7,5/9,5 mm über dem Lidrand;
  Lidrand 0,0 mm bewegt; eigene Form `lidfalte_<g>`, beim Lidschluss ausgeblendet – siehe Prüferbefund),
  Tränenrinne (0,75 mm), Kopfnicker (1,5 mm, Warzenfortsatz → Brustbein), Kieferwinkel (4 mm seitlich), Nasenhöcker
  (2 mm), dazu der Adamsapfel der Phase 5 als eigene Form. Augapfel und Zähne bleiben fest, Lidteile, Brauen, Wimpern
  und Kragen folgen der Haut;
- ICT-Identitäten 000–015, 020, 025, 028, 033 (`ict_ident=`, wie die Mimik übertragen, am Hals zur Büstennaht
  ausgeblendet) und die Herkunftsachsen african/asian der MakeHuman-Makros (`herkunft=1`).

Formen sind lineare Versätze, also ist jede Mischform genau eine Summe von Probeformen: `formen_mischen.py` (neu,
numpy) schreibt die Formen des fertigen Kopfs nach `kopf6_rezept.json` (Lage und Normale linear gemischt, wie Godot
gleichzeitig gesetzte Formen mischt) und entfernt die Probeformen. Geprüft: die in ihre Ziele zerlegte Reifeform der
Phase 5 misst wie die ganze Form (Abweichung ≤ 0,01 mm in allen Maßen). `kopf6.sh` baut damit in zwei Schritten
(Probebau, Mischung), `kopf6.sh mischen` mischt ohne Blender neu. `kopf2_regler.json` bleibt unverändert (Phase 5
nachbaubar).

**Hinweis Kopfgröße:** `kopf6.glb` 40,9 MB (Etappe A 26,8 MB): 74 neue bzw. neu gemischte Formen, davon 40 neu
(Reife oben/unten, Mitte, 6 neue Gestalt-Gene, Gesichtsfülle, 10 Züge, 10 Asymmetrieformen).

### S4 Grundgesichter nach Maß

- **Ausgleichsrechnung** `formen_ausgleich.py` (numpy in Blenders Python): Grundgewichte (Makro, Ruhelage des Lids) aus
  dem Gewichteausdruck der Testszene, Gen-Gewichte null (Nullpunkt Mensch-Mittel ab S5), Mitte mit 35 Jahren 0,55. Die
  Reifeform der Phase 5 wird in ihre Ziele zerlegt; gesucht sind Zusatzgewichte je Probeform unter Grenzen. Gelöst mit
  Gauß-Newton (Ableitung je Probeform, begrenzte kleinste Quadrate mit projiziertem Gradienten, Schrittweitensuche)
  und einer Feinsuche je Variable auf den echten Kosten, weil die Maße Knicke haben (vorderste/seitlichste Ecken).
  Gegenpaare (incr/decr) sind eine Variable mit Vorzeichen; Reifeziele kippen nur, wo es begründet ist (Nase kürzer bis
  NIOSH, Kinnhöhe, Jochbein/Unterkiefer der Frau, Mundbreite). Obergrenzen gegen Übertreibung: Lippen dünner höchstens
  0,6 (Frau 0,4), Lidfalte 0,8, Jochbein 1,5 mm. Nebenbedingungen: Pupillenabstand fest (Toleranz 0,12 mm),
  Augapfel : IPD (±0,4 %), Nasenlänge und Mundbreite an die NIOSH-Mittel gebunden. Frau schmaler: `head-scale-horiz-decr`
  nur gekoppelt mit `eye-trans-out` (IPD bleibt).
- **Ergebnis** `kopf6_rezept_s4.json`: `alter_reife_<g>_oben` (Augen, Brauen, Stirn, Nase) und `alter_reife_<g>_unten`
  (Wangen, Kiefer, Kinn, Mund, Hals; mit Kieferkante, Unterwange, Jochbein, Kopfnicker, Adamsapfel beim Mann) sowie
  `alter_mitte_<g>` (25 → 45 Jahre: Wangenfett −, Tränenrinne, Nasolabialfalte, Lippen dünner, Nase +1,2 mm und Mund
  +1,5 mm breiter). Alterskurven in `regeln6.json`: Reife oben bis 24, unten bis 25 (Mann) bzw. 24 (Frau), Mitte 23 → 45,
  Greis ab 45.
- **Verworfen**: `forehead-nubian` als Hebel für den Nasenwurzelwinkel der Frau hob den ganzen Scheitel um 20 mm
  (Kegelschädel im ersten Blickbogen) und ist kein Kandidat mehr.
- **Messweg geändert (Begründung):** Die Wangenwölbung der Bestandsaufnahme (vorderste Ecke bei |x| 36–44 mm in einem
  3-mm-Streifen) sprang am schmaleren Untergesicht bei Lippenänderungen um 0,09 von −0,4 auf 11 mm; neu ist sie das
  gewichtete Mittel der vorderen Haut um |x| = 0,28 · zy (`wangenwoelbung`; die alte steht als `wangenwoelbung_s0`).
  Bezug Phase 5 in der neuen Definition: Mann 35 +2,0, Frau 35 +4,4 mm. Die Ohrwurzel für „vor dem Ohr“ ist jetzt die
  vordere der beiden Seiten (bei schiefen Köpfen schlug die Suche einseitig 10–15 mm zu weit vorn an und die Breite in
  Mundhöhe brach ein); der Nasenflügel liegt höchstens 8 mm vom ICT-Merkpunkt 31/35 (sonst fand die Suche bei schmalen
  Gesichtern eine Wangenecke). Symmetrische Mittelgesichter messen unverändert.

### S5 Gene eichen, Karikaturbremse

- **Regeln als Daten** `game/versuch/charakter/regeln6.json` (erzeugt von `kopf6_eichen.py`): je Gen Formen, Stärke je
  Geschlecht und Richtung (Formgewicht je σ), Maß, Ziel und erreichte Wirkung, Reifeteil; Alterskurven; Schalter
  (`neue_rechnung_voelker` = Mensch, andere Völker rechnen wie Phase 5 bis Phase 7); Karikaturbremse; Budget je
  MakeHuman-Ziel [−1; 1,2] mit Zielmatrix (Form → Ziel); Züge; Asymmetrie.
- **Rechnung** (`charakter_kopf.gd`, `_anwenden6`, nur Stand 6 und nur wenn die .glb die neuen Formen hat): Gewicht =
  |Wert − Mittel| / Streuung × Stärke(Geschlecht, Richtung) × Reife (Kiefer, Kinn: unten; Brauenwulst, Höcker,
  Augentiefe: oben) × Gesichtsgröße. Gene mit Streuung 0 tragen nichts bei; `ohren_spitze` bleibt absolut (Wert/255).
  Leibesfülle ins Gesicht: neue Form `gen_gesicht_fett` aus dem Gen `gewicht` (0,45 je σ).
- **Neu zusammengesetzt** wie im Plan (S5, Tabelle); dazu zwei eigene Formen: `kiefer_breite` mit dem Kieferwinkel
  (MakeHuman bewegt den Kieferwinkel nur senkrecht oder mit starken Nebenwirkungen; `chin-width` wirkt nur aufs Kinn),
  `nase_hoecker` mit dem eigenen Höcker (das MakeHuman-Ziel bewegt höchstens 0,5 mm).
- **Eichung** am Mensch-Mittel 35 je Geschlecht und Richtung: +1σ = 0,75 × natürliche Streuung des Maßes (NIOSH; ohne
  NIOSH-Wert ICT × 0,7 bzw. Recherche), Frauen bei Brauenwulst, Kiefer und Kinn × 0,65, Höcker × 0,8, Lippen × 1,1.
  Stärke höchstens 0,6 je σ (bei 2σ = 1,2 = Budgetgrenze). Maße, die an „vordersten Ecken“ springen, misst die Eichung
  an festen Ecken (Jochbeinbreite, Wangenfülle, Oberlid, Kinnbreite) bzw. an ICT-Merkpunkten (Lippenrot 51/57/62/66).
- **Karikaturbremse** (nur Darstellung): K = √(Mittel z²) über Gestalt-Gene und Züge; K > 1,35 → alle z gemeinsam
  kleiner; höchstens zwei Merkmale über |z| 2; 0,05 < K < 0,75 → die zwei stärksten auf |z| 1,2. Danach das Budget je
  MakeHuman-Ziel (gekürzt werden nur Gen-Anteile).

### S6 Gesichtszüge, Asymmetrie, Personen-Samen

- **Züge**: Kandidaten ICT `identity000–015` ohne 009 (rein antisymmetrisch). Bereinigt per kleinster Quadrate gegen
  die Geschlechtsachse (Frau 25 − Mann 25 aus Makro und Reife) und vier Herkunftsachsen (MakeHuman african/asian, Mann
  und Frau), Skalarprodukt über die Gesichtsecken (vor der Ohrwurzel, über dem Kinn); |cos| danach 0,000. Vorfilter
  |cos| > 0,3 vorher: 000, 003, 004 entfallen. Durchdringung bei ±2σ (Gewicht ±1,4) gegen das Grundgesicht: das Lidmaß
  ist schon am Grundgesicht negativ (Phase 5: Mann −1,45, Frau −0,86 mm; es erfasst Ecken der Lidhöhle) und das alte
  Lippe–Zahn-Maß sprang (Minimum über 1-mm-Säulen); gewertet wird deshalb die Verschlechterung (Lid und robustes
  Lippe–Zahn-Maß `lippe_zahn_p5`) mit Grenze 0,6 mm. Gewählt (vorläufig, Nutzer an Halt B): **10 Züge** aus ICT 002,
  005, 006, 007, 008, 011, 012, 013, 014, 015 (001 und 010 fallen nach der letzten S4-Rechnung durch die Durchdringungsprüfung). Basis eingefroren: `tools/charakter/zuege_basis_v1.json` (Rezept je Zug),
  SHA-256 in `regeln6.json`; die Felder liegen lokal als `zuege_basis_v1.npz`. Gewicht = (Wert − 128)/32 × 0,7 × Reife.
- **Asymmetrie**: je Merkmal die stärkste MakeHuman-asym-Form als (links − rechts)/2 – Kinn seitlich `asym-jaw-3`,
  Nasenspitze `asym-nose-1`, Augenhöhe `asym-eye-2`, Braue `asym-brown-1`, Mundwinkel `asym-cheek-2` (die Ohr-Formen
  wirken nicht: 0,01 mm) – dazu die antisymmetrischen ICT-Komponenten 009, 020, 025, 028, 033. Gewicht ~ N(0, Streuung)
  aus dem Personen-Samen, Streuung so, dass der Spiegelabstand im Median 0,72 mm beträgt; Grenzen je Merkmal
  (Kinn 2, Nasenspitze 1,5, Augenhöhe 1, Brauen 2, Mundwinkel 1,5, Ohr 3 mm) über lineare Merkpunkt-Versätze, sonst
  wird alles gemeinsam verkleinert (27 % der Personen).
- **Personen-Samen**: im Versuch aus dem Bogen-Startwert (Zweck „aussehen“); Rückfall `ErbgutBruecke.AussehenSamen`:
  FNV-1a (64 Bit) über sichtbares und verdecktes Allel der 40 Gene der Version 1 in fester Reihenfolge.
  **Vorschlag an kern** (öffentliche Abfrage, ersetzt den Rückfall): in `Familienbuch.Erbgut.cs`
  `public int AussehenStartwert(int nummer) => Zugwuerfel.Mischen(_aussehenZufall.Startwert, nummer, ZweckAussehen);`
  mit `private const int ZweckAussehen = 9;` und einem Test (gleiche Partie und Nummer → gleicher Wert; neue Gene ändern
  ihn nicht).

### S7 Wiedererkennungsmerkmale

- **Vorschlagsdatei** `game/versuch/charakter/erbgut_vorschlag.json`: vollständige Kopie von `erbgut.json` (Version 1)
  plus Schub 1 (10 Gene wie im Plan, Nicht-Menschen wie Menschen) und `zug_01–12` (Mensch 16/128/240, Streuung 32,
  Mutation 6; andere Völker Streuung 0), Korrelationen nur in neue Gene (7). `brauen_form` „gleich verteilt“: Streuung
  70 (der Kern würfelt eine Glockenkurve); die Darstellung nimmt das Quantil, damit alle sechs Formen gleich häufig sind.
- **Formen der neuen Gestalt-Gene** wie im Plan, geeicht wie S5 (Brauenstand, Lidspalte, Schlupflid, Nasenspitze,
  Mundwinkel, Kinnbreite); Lidspalte setzt zusätzlich die Ruhelage des Lids (−0,025 je σ).
- **Brauen**: sechs Formen je Geschlecht (gerade, Bogen, Knick, kräftiger Kopf, dünn auslaufend, buschig) in
  `kopf3_haartexturen.py` (`brauen6_<g>_<k>.png`, eigener Startwert, die Bilder der Phase 5 bleiben bitgleich); der Kopf
  legt sie als `Texture2DArray` an (`haar3.gdshader`: `bilder6`, `ebene`; Ebene < 0 = wie Phase 5). Dichte und Breite
  aus `brauen_dichte`. **Zusammengewachsen** (`brauen_form` ≥ 248) ist nicht gebaut: die Brauenkarten enden vor der
  Nasenwurzel, es braucht eine eigene Karte (offen).
- **Iris**: Die sechs RoboPoets-Farbbilder sind dieselbe Struktur in sechs Farben (Korrelation 0,98–1,0). Statt ihrer
  sechs eigene Muster (`kopf6_iris.py`, aus der CC0-Fasermaske): Grundmuster, Krypten, Furchen, Strahlen, Flecken, breiter
  Kranz; `augen6.gdshader` mit `iris_bilder6`, `iris_ebene`, `iris_drehung`. Wahl nach Augenfarbe (dunkel / blau-grau /
  grün-hasel je vier Muster) und Personen-Samen, Drehung je Person.

### Prüfung Etappe B

Prüfmappe **B** (`~/werkzeuge/charakter/bilder/phase6/B/`, Stand 6): P1 (K1/K2 × L-V/L-S), P3, P4 (a/b × K1/K2/K5),
P5 (beide Allelregeln), P7, P7b, P8, P12 (Blindtest), P13, P14, M1, M2, `pruefen6.md`, Schlüsseldateien. Stand 5 zum
Vergleich in A0/A0b; Halt-B-Mappe mit großen Vergleichsbildern außerhalb des Repositorys.

**S4 – Netzmaße am Mittelgesicht 35** (Gewichte aus Godot, fertiger Kopf; Phase 5 in derselben Definition):

| Maß | Mann Phase 5 → Etappe B | Ziel Mann | Frau Phase 5 → Etappe B | Ziel Frau |
|---|---|---|---|---|
| Gesichtsindex | 0,942 → **0,871** ✓ | 0,85–0,90 | 0,796 → **0,840** ✓ (Rand) | 0,84–0,90 |
| zy (mm) / Sellion–Kinn | 142,8 / 134,5 → 142,5 / **124,2** ✓ | 138–147 / 119–127 | 148,0 / 117,8 → **138,7 / 116,5** ✓ | NIOSH ±1σ |
| zy : IPD | 2,246 → 2,242 ✓ (±3 %) | ±3 % | 2,364 → 2,227 = **−5,8 %** ✗ knapp | −6 … −10 % |
| Kinnmitte / zy | 0,805 → **0,748** ✓ | 0,69–0,76 | 0,625 → **0,693** ✓ | 0,66–0,72 |
| Mundhöhe / zy | 0,910 → **0,876** ✓ | 0,84–0,89 | 0,818 → **0,847** ✓ | 0,84–0,90 |
| Wangenwölbung (neue Def.) | +2,0 → **−1,4** ✓ | ≤ 0 | +4,4 → **−1,1** ✓ | ≤ +2 |
| Mundlage sn–sto : sn–me | 0,220 → **0,309** ✗ knapp | 0,31–0,35 | 0,247 → **0,319** ✓ | 0,31–0,35 |
| Braue über der Pupille (mm) | 12,5 → **10,5** ✓ | 9–12 | 16,4 → **14,1** ✗ knapp (nach dem Lidfalten-Umbau 14,13) | 12–14 |
| Augentiefe gegen Phase 5 | **+1,82 mm** ✓ | +1,5–2 | **+0,10 mm** ✓ | ±0,5 |
| Kieferknick (5 mm) | 32,9° → **40,5°** ✓ | ≥ 25° | 27,0° → 31,0° | – |
| Kinn-Hals-Winkel | 116,2° → 116,3° ✓ | 105–125° | 109,7° → 106,9° ✓ | 105–125° |
| Kinn über der Kragenoberkante | 11,6 → **9,5 mm** ✗ | ≥ 30 mm | – | – |
| Nasenwurzelwinkel | – | – | 148,9° → **139,6°** ✓ | 134–142° |
| Pogonion hinter der Unterlippe | – | – | 5,8 → **3,1 mm** ✓ | ≤ 4 mm |
| Unterkiefer Frau / Mann | – | – | **0,924** ✗ knapp | 0,88–0,92 |
| Pupillenabstand | 63,57 → 63,57 ✓ | ±0,5 mm | 62,62 → 62,29 ✓ | ±0,5 mm |
| Augapfel : IPD | 0,456 → 0,456 ✓ | ±3 % | 0,462 → 0,452 (−2,2 %) ✓ | ±3 % |
| Oberlidzone gegen Wange (L-V) | **−30,2 L*** ✓ | ≤ −18 | **−16,9 L*** ✗ knapp | ≤ −18 |

- „Kinn über der Kragenoberkante ≥ 30 mm“ hängt am Stehkragen (Phase 5: 11,6 mm) und ist mit der Gesichtsform nicht
  erreichbar; offen für die Kleidung.
- Kind 5 und Säugling: Die neuen Altersformen wirken erst ab 10–12 Jahren (Gewicht 0). Gegen Phase 5 bewegen sich die
  Ecken um höchstens 1,1 (Säugling) bzw. 1,4 mm (Kind 5), im Mittel 0,2 mm – Ursache ist der neue Nullpunkt der Gene
  (S5): Phase 5 setzte beim Mittelgesicht kleine Gen-Gewichte ((Mittel − 128)/127). P7 ohne Dellen (Bild).
- Lid–Augapfel: das Maß ist schon in Phase 5 negativ (Ecken der Lidhöhle); Mann −1,45 → −1,39, Frau −0,86 → −1,33 mm.
  Im Bild (P7, Lidschluss, N2) keine Durchdringung sichtbar. Lippe–Zahn 5,6 / 6,2 mm (≥ 0,3) ✓.

**S5 – Gene in der Bevölkerung** (1000 Personen je Geschlecht, Startwerte 10001–11000, über den Kern gewürfelt mit der
Vorschlagsdatei; Gewichte aus Godot, gemessen mit `messen/stichprobe6.py`): Anteil an der natürlichen Streuung.

| Maß (Bezug) | Mann nur Gene | Mann + Züge | Frau nur Gene | Frau + Züge |
|---|---|---|---|---|
| Gesichtslänge se–me (NIOSH 6,9 / 5,8) | **0,74** ✓ | 0,80 | **0,73** ✓ | 0,85 ✓ |
| Gesichtsbreite zy (NIOSH 6,7 / 6,2) | **0,78** ✓ | 0,90 ✓ | **0,72** ✓ | 0,86 ✓ |
| Unterkiefer go–go (ICT × 0,7 = 4,9; mit Fett ≈ 10) | 1,40 / 0,69 | 1,48 / 0,72 | 1,11 / 0,54 | 1,18 / 0,58 |
| Mundbreite (NIOSH 4,0 / 3,5) | **0,81** ✓ | 0,83 | **0,75** ✓ | 0,78 |
| Nasenlänge n–sn (NIOSH 4,1 / 3,7) | **0,71** ✓ | 0,74 | **0,71** ✓ | 0,77 |
| Nasenbreite (3,2 / 2,9) | **0,81** ✓ | 0,83 | 0,97 ✗ | 1,07 ✓ |
| Lippenrot (ICT × 0,7 = 2,5) | 0,61 ✗ knapp | 0,79 | 0,60 ✗ knapp | 0,76 |
| Kinnvorsprung (ICT × 0,7 = 3,3) | **0,70** ✓ | 1,16 ✗ knapp | 0,63 ✗ knapp | 1,05 ✓ |

- Ziel nur Gene 0,65–0,85 je Maß: Mann 6 von 7 (ohne Unterkiefer), Frau 4 von 7. Ziel Gene + Züge 0,85–1,15: Mann 1 von
  7, Frau 4 von 7 – die Züge geben im Mittel +0,1 dazu, weniger als geplant (sie sind bereinigt und wirken vor allem auf
  Formen, die diese 8 Maße kaum treffen). Der Unterkiefer streut stärker als ICT × 0,7, weil Kopf- und Gesichtsbreite
  mitwirken; gegen die Bigonialbreite mit Fett (≈ 10 mm, Recherche) liegt er bei 0,54–0,72.
- Mittel der Bevölkerung gegen das Mittelgesicht: alle Maße innerhalb ±0,3σ außer Unterkiefer (+0,70 / +0,56σ),
  Gesichtsbreite Mann (+0,31σ) und Nasenbreite Frau (+0,42σ) – Ursache: das Budget kürzt nur in eine Richtung (dort, wo
  die Reifeform dasselbe MakeHuman-Ziel schon nutzt).
- **Populationskorrektur** (ein Durchgang, `POP_FAKTOR` in `kopf6_eichen.py`): Lippen × 1,5/1,55, Kinn × 1,2/1,45,
  Gesichtsbreite × 0,83/1,25, Mund × 0,78/1,2, Nasenbreite Frau × 0,77, Gesichtslänge × 1,05/1,15. Folge: am
  Mittelgesicht bewegt `lippen` +1σ das Lippenrot jetzt um 1,6–2,1 mm (Ziel 1,0–1,5 überschritten); in der Bevölkerung
  kürzt das Budget das wieder auf 0,6 der natürlichen Streuung. `kiefer_breite` +1σ = **+4,0 mm** Unterkiefer (Frau
  2,6) ✓.
- **Karikatur**: K der Gene (nach der Bremse) p95 **1,03**, höchstens 1,14 ✓ (≤ 1,35); nie mehr als **zwei** Merkmale
  über 2σ ✓; die Bremse greift bei keiner Person (der Kern würfelt höchstens ±2σ). Über die gemessenen Maße (12, σ-normiert)
  p95 1,44 / 1,43; einzelne Köpfe haben 5–6 Maße über 2σ (Messwerte, nicht Gene: Unterkiefer, Kinn).
- **Python = Godot**: `messen/regeln6_nachrechnen.py` rechnet die Gen- und Zuggewichte aus `regeln6.json` nach:
  73 593 Gewichte, größte Abweichung 8·10⁻¹⁵ (4 797 vom Budget gekürzte Formgewichte nicht verglichen).
- **Nicht erreicht**: Nasenhöcker +1σ = 1,2 / 0,4 mm (Mann plus/minus), Frau 0,35 / 0,2 mm (Ziel 1,3 / 1,04);
  Schlupflid 0,36–0,52 statt 0,8 mm; Wangenknochen schmaler 1,1–1,4 statt 2,5 mm. Die MakeHuman-Ziele geben an diesen
  Stellen nicht mehr her; eigene Formen wie beim Kiefer wären der nächste Schritt.

**S6 – Züge, Asymmetrie, Samen**

| Abnahme | Ergebnis |
|---|---|
| \|cos\| zur Geschlechtsachse < 0,1 je Zug | 0,000 (bereinigt; vorher 0,06–0,35) ✓ |
| Spiegelabstand RMS 0,5–1,0 mm | Median **0,72** (Mann) / **0,65** (Frau) mm, 86 % / 83 % der Personen im Bereich ✓ (Phase 5: 0,02–0,03) |
| R7-Grenzen bei 1000 Personen nie überschritten | ✓ per Bau (bei 28 % wird die Asymmetrie verkleinert) |
| Sprung an der Halsnaht ≤ 0,1 mm | Züge am Hals zur Naht ausgeblendet (1 − w_hals), eigene Formen 0 an der Naht ✓ |
| Probe-Gen ändert den Samen nicht (1000) | 0 Abweichungen mit Probe-Gen und mit der Vorschlagsdatei (62 Gene), 0 doppelte Samen ✓ |
| Grafikspeicher neuer Formen ≤ +30 MB | Datei +14 MB (26,8 → 40,9 MB); Grafikspeicher in M2 |

**S7 – Vorschlagsgene, Familie**

| Abnahme | Ergebnis |
|---|---|
| Bestehende Gene mit Vorschlagsdatei = Standard (1000 × 6 Völker, Würfeln und Vererben) | 0 / 0 Abweichungen ✓ |
| Neue Gestalt-Gene bewegen ihr Maß um 0,65–0,85 × Zielstreuung je σ | Brauenstand 1,0 mm, Lidspalte 0,63 mm, Nasenspitze 1,4 mm, Mundwinkel 0,9 mm, Kinnbreite 3,0 / 2,4 mm ✓; Schlupflid 0,36–0,52 mm ✗ |
| Familie: Abstand Kind–Elternteil / Kind–Fremder | **sichtbares Allel 0,854** (Erwartung ≈ 0,87), **Mittel beider Allele 0,743** (≈ 0,71); Korrelation Elternmittel–Kind 0,35 / 0,64 (300 Familien je Regel, `messen/familie6.py`) |
| Nachbarabstand (12 Maße, σ-normiert) Median ≥ 0,7, 5 % ≥ 0,4 | Median **0,50 / 0,48**, 5 % 0,37 / 0,34 ✗ (Phase 5 ≈ 0) |

**Blindtest** (ein unabhängiger Prüfer, nur Bilder; Schlüssel vorher versiegelt: P12 SHA-256
`e2eee27bbd3ff28b808e249deecd413f17357313070f892d3ef7421d8959cb92`, Alter
`33206f02914ede0edd74272ec2c2461aac02d374715f103b9a7eb9287e371483`; Teamleiter und Nutzer an Halt B ausstehend):

| Test | Ergebnis | Ziel |
|---|---|---|
| P12a Zuordnung vorn ↔ ¾ (nur Gesichtsform, Brauen, Züge; Farben fest) | **16/16** Männer, **16/16** Frauen – aber nur über Messungen am Bild (Lippen-, Brauen-, Kinnkanten); der erste Durchgang **mit dem Auge** traf nur **4/16** bzw. **3/16** | ≥ 13/16 |
| P12a in Stammbaumgröße (K5) | 16/16 / 16/16 (über den Umriss gegen Blatt B) | ≥ 12/16 |
| P12b andere Frisur, Innenlicht | 10/10 / 10/10 – **ungültig als Test**: Blatt D hat dieselbe Kamera wie B, der Umriss ist pixelgleich | ≥ 8/10 |
| Altersschätzung (24 Köpfe aus P1) | **19/24** auf ±7 Jahre (79 %) | ≥ 80 % |
| Mann 35 gegen Frau 35 | geschätzt im Mittel 28 / 29 Jahre (Abstand ≤ 3 ✓, aber unter 30) | ≤ 3 Jahre, beide 30–45 |
| 20 gegen 35 | einzeln nicht sicher unterscheidbar (Schätzungen 19–32); nebeneinander ja | sicher |
| Karikatur (P14, 20 extremste + 10 zufällige je Geschlecht) | keiner unmenschlich; grenzwertig 2 (Männer) und 3 (Frauen): sehr breiter Kiefer mit Hängebacken, langer vorstehender Unterkiefer, Kerbe unter der Unterlippe | keine Karikatur |
| Züge ±2σ (P13) | keine Karikatur, Delle oder Durchdringung; am stärksten zug_01 (−2σ: rundes Gesicht, Stupsnase), kaum sichtbar zug_07, zug_08, zug_10 | – |

- **Folgerung:** Für Menschen mit dem Auge sind die Gesichter bei gleicher Haut-, Haar- und Augenfarbe noch „wie
  Geschwister“; die Wiedererkennung trägt im Spiel vor allem Farbe, Frisur und Bart. Der Test mit Teamleiter und
  Nutzer an Halt B ist der eigentliche Prüfstein; Blatt D wird mit einem anderen Blickwinkel neu gebaut.
- **Befund aus Etappe B, behoben:** Bei geschlossenen Augen zeichnete sich die neue Lidfalte (damals σ 1 mm, 1,25 mm tief,
  in der Reifeform) als harte Platte auf dem Lid ab. Jetzt σ 1,6 mm, 1,0 mm tief, weiche Enden, eigene Form, die der Kopf
  mit dem Lidschluss ausblendet (`lid/lidtest.png`: offen, halb, zu glatt). P7 und die Mappenbilder 1, 2, 6 sind danach
  neu gerendert; die übrigen Bögen der Mappe B zeigen die Augen offen (dort ist die Änderung kaum sichtbar).
- **Weitere Befunde des Prüfers** (nicht aus Etappe B, aber sichtbar): heller Glanzstreifen auf dem Nasenrücken
  (Kunststoffwirkung; Etappe C, Glanz je Region), rotorange Nasenlöcher bei Frauen, heller Streifen über der Braue im
  ¾-Blick, dünne abgerissene Brauen bei Frauen (Formen 4/„dünn auslaufend“ mit niedriger Dichte), Säugling/Kind schließen die Augen nicht ganz, Mundinneres rot und Zähne blockig,
  Kinderkragen gezackt und vom Kinn durchdrungen; Frisuren: „kranz“ Seitenhaar schwebt als Platte, „zopfschlaufen“
  Ohr durch die Schlaufe, „schnecken“ Kopf scheint durch, „haarnetz“ Band schwebt. Alles als offen notiert.

**M2** (T400 ohne fremde GPU-Prozesse, Stufe Hoch, 1280 × 720, Stand 5 und 6 abwechselnd im selben Prozess, Median
aus 3 gepaarten Differenzen; Code-Stand der Lidfalte vor dem Commit, `B/m2b/messung.jsonl`):

| Lauf | Stand 5 → 6 (ms) | Δ Median | Grenze | Grafikspeicher 5 → 6 |
|---|---|---|---|---|
| Kopf ohne Haar (Mittelgesicht / Zufallsperson) | 6,12 → 6,64 / 6,72 | **+0,52 / +0,61** ✓ | ≤ +1,0 (Ziel +0,6) | 169 → 200 MB |
| 8 Köpfe ohne Haar (Mittel / zufällig) | 19,0 → 20,8 / 20,9 | **+1,72 / +1,81** ✓ | ≤ +3 | 595 → 837 MB |
| TressFX in Ruhe (Pagenkopf, Vollbart; Mittel / zufällig) | 27,1 → 30,2 / 30,9 | **+3,13 / +2,18** ✗ | ≤ +1,2 | 222 → 251 MB |

- Der größte Teil der TressFX-Kosten stammt aus Etappe A (Studio-HDRI als Umgebungslicht, damals +2,80 ms; Entscheidung
  des Teamleiters: HDRI nur Hoch/Ultra). Die Formen der Etappe B kosten ohne Haar 0,0–0,1 ms mehr als in Etappe A.
- Grafikspeicher je Kopf +30 MB (Ziel neue Formen ≤ +30 MB). Im Versuch lädt jede Bühne ihre eigene .glb (8 Köpfe =
  8 Kopien); das Spiel muss das Netz teilen, sonst wären es je weiterem Kopf +30 MB statt ≤ +2 MB.

- **Brauen und Iris unter Zufallspersonen** (P4a, 32 Personen): alle **6** Brauenformen (je 2–7) und alle **6**
  Irismuster (je 4–7) ✓ (Ziel ≥ 5 bzw. ≥ 4 unter 30).
- **Haar neu gekämmt** (einmal, Ende Etappe B): `kopf6.sh haar_kopieren` (Verknüpfungen nach glb5 durch Kopien
  ersetzt), dann `kopf6.sh kaemmen` (41,5 min CPU) auf Reife oben/unten und Mitte mit 35 Jahren (`kopf4_haar.py` kennt
  die neuen Formnamen). glb5 unverändert (Prüfsumme). P8 zeigt die zehn Frisuren je Geschlecht.

### Selbst getroffene Entscheidungen (Etappe B)

- **Probebau + Mischung statt Neubau je Korrektur** (Begründung oben); `kopf2_regler.json` bleibt für Phase 5.
- **Eigene Formen über den Plan hinaus**: Kieferwinkel und Nasenhöcker (MakeHuman gab für `kiefer_breite` und
  `nase_hoecker` zu wenig her).
- **Messwege geändert** (Wangenwölbung stetig, vordere Ohrwurzel, Nasenflügel an lm31/35, robustes Lippe–Zahn-Maß,
  feste Ecken in der Eichung); die alten Maße bleiben daneben, wo sie Bezug zur Bestandsaufnahme sind.
- **Durchdringung der Züge relativ** (höchstens 0,6 mm schlechter als das Grundgesicht), weil beide Maße schon am
  Grundgesicht negativ bzw. sprunghaft sind; die Bildprüfung (P13, Prüfer) entscheidet mit.
- **Populationskorrektur** der Genstärken (ein Durchgang) – im Plan stand nur die Eichung am Mittelgesicht.
- **Iris**: eigene sechs Strukturen statt der strukturgleichen RoboPoets-Farbbilder.
- **P12 strenger als im Plan**: nur Gestalt-Gene, Züge und Brauen aus der Saat, Haut-, Haar- und Augenfarbe fest.
- **Karikaturbremse**: das Anheben bei K < 0,75 nur bei Personen mit Samen (sonst würde das Mittelgesicht ab 50 durch
  die Altersverschiebungen der Gene verzerrt).
- **Alterskurven** gestreckt (Reife bis 24/25, Mitte ab 23), damit 20 und 35 Jahre sich stärker unterscheiden.

### Offen nach Etappe B

- Nicht erreicht oder knapp: Mundlage Mann 0,309 (≥ 0,31), Braue Frau 14,1 (≤ 14), zy : IPD Frau −5,8 % (−6 … −10 %),
  Unterkiefer Frau/Mann 0,924 (≤ 0,92), Oberlidzone Frau −16,9 L* (≤ −18), Kinn über dem Kragen 9,5 mm (≥ 30; Kragen),
  Licht : Schatten unter L-V 1,47/1,82 (2–4; AgX), Nachbarabstand Median 0,50/0,48 (≥ 0,7), Streuung „Gene + Züge“
  meist 0,74–0,85 statt 0,85–1,15, Höcker, Schlupflid und Wangenknochen schmaler zu schwach (eigene Formen nötig).
- Die Form allein macht 20 und 35 Jahre nur wenig verschieden; den Rest soll die Haut bringen (Etappe C).
- Zusammengewachsene Brauen (`brauen_form` ≥ 248) brauchen eine eigene Karte über der Nasenwurzel.
- In der Brauen-Nahaufnahme liegt ein heller Streifen über den Augen (Brauenschatten-Maske der Haut aus Phase 3–5,
  passt nicht mehr zu allen Brauenformen) – in S9 mit den Pigmentzonen.
- Glättung je Ziel (12 → 6/4 für Lippen, Lider, Nasenspitze, Plan S4.3) nicht umgesetzt; der Gratbogen P7b zeigt
  keine MPFB-Kanten.
- **Kern-Bedarf** (über den Teamleiter, Bereich kern entscheidet): (1) öffentliche Abfrage für den Personen-Samen
  `AussehenStartwert(int nummer)` (Signatur oben, S6); (2) Schub 1 der neuen Gene und die Züge als JSON-Ausschnitte aus
  `game/versuch/charakter/erbgut_vorschlag.json` (nach den Entscheidungen E8–E10); (3) bei „Mittel beider Allele“ (E9)
  die Beschreibung in `Erbgut.cs` und `kern.md` anpassen – die Darstellung liest dann auch `VerdeckterWert`.

### Testanleitung Etappe B

```bash
BPY=~/werkzeuge/blender/blender-5.2.2-linux-x64/5.2/python/bin/python3.13
G=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
# Kopf: Probebau (71 s) und Mischung; nur neu mischen (ohne Blender); Haarstützen
tools/charakter/kopf6.sh proben && tools/charakter/kopf6.sh mischen && tools/charakter/kopf6.sh nur_haar
# Ausgleichsrechnung S4 und Eichung S5–S7 (schreiben kopf6_rezept_s4.json, kopf6_rezept.json, regeln6.json, zuege_basis_v1.json)
$BPY -I tools/charakter/formen_ausgleich.py --glb=~/werkzeuge/charakter/glb6p/kopf6p.glb --gewichte=<gewichte.json> --aus=<ordner>
$BPY -I tools/charakter/kopf6_eichen.py --glb=~/werkzeuge/charakter/glb6p/kopf6p.glb --gewichte=<gewichte.json> --aus=<ordner>
# Brauen- und Irisbilder
$BPY -I tools/charakter/kopf3_haartexturen.py ~/werkzeuge/charakter/glb6/kopf6_tex && $BPY -I tools/charakter/kopf6_iris.py ~/werkzeuge/charakter/glb6/kopf6_tex
# Brücke: bestehende Gene mit Vorschlag gleich, Personen-Samen stabil (ohne Grafikkarte)
$G --headless --path game res://versuch/charakter/charakter_schau.tscn -- --bruecke_pruefen --ende
# 1000er-Prüfung (Gewichte headless, dann messen); --ohne_zuege = nur Gene
$G --headless --path game res://versuch/charakter/charakter_schau.tscn -- --glb=$HOME/werkzeuge/charakter/glb6 --stand=6 \
   --gewichte=<personen_1000.json> --aus=<gewichte_1000.json> --ende
$BPY -I tools/charakter/messen/stichprobe6.py <gewichte_1000.json> ~/werkzeuge/charakter/glb6/kopf6.glb <stichprobe.json>
python3 -I tools/charakter/messen/regeln6_nachrechnen.py <gewichte_1000.json>
$BPY -I tools/charakter/messen/familie6.py ~/werkzeuge/charakter/glb6/kopf6.glb <gewichte_sichtbar.json> <gewichte_mittel.json>
# Bögen (absolute Pfade: Godot arbeitet mit --path in game/), P14 aus der Stichprobe, Blindblatt Alter
python3 tools/charakter/boegen6/erzeugen.py && python3 tools/charakter/boegen6/p14_aus_stichprobe.py <stichprobe.json>
python3 tools/charakter/boegen6/blind_alter.py <prüfmappe>/p01_alter_ton_k1_ls <prüfmappe>
GLB=~/werkzeuge/charakter/glb6 ZEIT=5400 tools/charakter/schau.sh <prüfmappe> 6200x5300 --stand=6 --bogen=$PWD/tools/charakter/boegen6/p12_zuordnung_m_a.json,… --ende
```


## Halt B2: Probekopf (Volumen statt Abmagerung, Breitlicht, Aussehen A, Vorbildköpfe B)

Anlass (Nutzer 31.10.2026): Die Köpfe wirken wie „komische Puppen“, schräg „abgemagert“; diese Basis bekommt eine letzte
Chance. Diagnose (Teamleiter): Die Basis (ICT-Scanmittel) ist normal; „abgemagert“ kam aus den eigenen S4-Zielen
(Wangenwölbung ≤ 0, Unterwange 15–23 mm hinter dem Scanmittel, Lippen platt, Augen klein, Glabella und Nase vor) und
dem Short-Lighting der Prüfbilder; „Puppe“ aus gleichförmigem Albedo, fehlendem Bartschatten, Brauen wie Aufkleber,
zu grüner und gesättigter Iris, Kragen bis unters Kinn und Glanz auf dem Nasenrücken. Alles liegt neben Halt B: der
Probekopf hat eigene Ordner (`~/werkzeuge/charakter/glb6p_b2`, `glb6b2` mit eigener `regeln6.json`), die Dateien des
Halt B (`kopf6_rezept_s4.json`, `kopf6_rezept.json`, `regeln6.json`, `zuege_basis_v1.json`) bleiben unverändert.

### Was gebaut ist

- **Form B2** `formen_ausgleich_b2.py`: dieselbe Ausgleichsrechnung (Probebau, Gauß-Newton aus `formen_ausgleich.py`),
  aber mit Volumen- und Profilzielen und Deckeln (Kopf der Datei). Neue Maße `profil_masse()`: Tiefen ab der
  Tragion-Mitte (g, n, prn, sn, ls, li, pg, me), Unterwange x30/40/50 und Mittelwange x25/35 als **Schnitt der
  Blickgeraden mit der vordersten Hautfläche** (die vorderste Ecke in einem 3-mm-Fenster war an der gröberen Wange
  manchmal leer), Lippen gegen Sn–Pg und E-Linie, Profilwinkel g–sn–pg, Lidspalte (Breite en–ex, Höhe), Schläfe. Bezug
  „Scanmittel“ = Makro-Grund je Geschlecht (beim Mann ≤ 0,6 mm vom nackten Scanmittel; bei der Frau ist das ganze
  Gesicht 4–7 mm flacher). Mitte (25 → 45) mit Wangenfett 0,2 statt 0,35/0,4. **Profil- und Schrägmaße gehören ab B2 zur
  Abnahme** (Tabelle unten).
- **Eichung S5** wie Etappe B auf dem B2-Mittelgesicht (`kopf6_eichen.py --rezept_s4= --regeln_aus= --rezept_aus=
  --zuege_aus=`); Populationsfaktoren und eingefrorene Züge unverändert.
- **Kragen** (`kopf2_bauen.py` Option `ausschnitt=<mm>`, `ausschnitt_band=0,5`): Kragenlinie vorn tiefer
  (D · max(0, cos φ)³ um die Halsachse), das Band vorn halb so hoch; die Flächen darüber werden Haut (der Atlas hat dort
  gebackene Brusthaut, kein neues Backen). Ohne Option wie bisher; der Probekopf nutzt 30 mm.
- **Licht** (`charakter_buehne.gd`): Schlüssellicht auf der breiten Seite – `ansicht()` spiegelt Schlüssel-, Füll- und
  Gegenlicht an der Mittelebene, sobald die Kamera auf der +x-Seite steht (Bogenschlüssel `licht_breit`, Standard an).
  Bei `winkel` < 0 (wie die Fotovorlagen) lag das Schlüssellicht schon kameranah.
- **Weg A – Aussehen** (Bogenschlüssel `weg_a`, `charakter_kopf.gd`): Haut `haut6a.gdshader` (`#define WEG_A` in
  `haut6.gdshaderinc`) mit Zonenkarte `haut_zonen.png` (`kopf6_zonen.py`: R Rötung Wangen/Nase/Ohren/Kinn, G Schatten
  unter den Augen, B Feuchte am Lidrand und Karunkel, A Nasenrücken matter), Bartstoppeln als Punkte aus der
  Porenkachel, Brauenschatten weich, Poren-AO, Streulicht je Kanal (Grün/Blau mit Poren); Brauen weniger deckend und
  heller (`haar3.gdshader` `deckung`); Iris entsättigt, Limbus schmaler, hohle Irisnormale (`augen6.gdshader`
  `iris_hohl`), Lederhaut zu den Winkeln warm abgedunkelt (`rand_schatten`), Lidschatten 0,9.
  Vorbilder (Code-Ideen, nicht übernommen): MatMADNESS „HumanShaders“ (MIT, Commit 95a54cf; Wrap je Kanal, hohle
  Irisnormale), RoboPoets „digital_human“ (MIT, Commit 9d28d0c; „eye_shadow“). Nicht genutzt: RoboPoets `pom()`
  (Herkunft LearnOpenGL, CC BY-NC), Texturen und Emily-Scan aus HumanShaders (Lizenz unklar).
- **Malfilter** (`malfilter.gdshader`, Bogenschlüssel `malfilter`): Kuwahara mit vier Sektoren (Halbmesser 3) und
  leichte Tusche-Kante, nur auf der Porträtbühne.
- **Weg B – Vorbildköpfe** (`vorbild_einpassen.py`, numpy/scipy/trimesh): Merkpunkte am Frontbild des Scans,
  Umeyama-Ausrichtung (mit Maßstab), starre ICP-Verfeinerung (verworfen, wenn sie die Merkpunkte verschlechtert),
  eigene nicht-starre Einpassung (nächster Scanpunkt + Membran-Glättung, sechs Runden mit fallender Steifigkeit,
  Augenregion und Nasenlöcher ohne Datenterm), Versatz zum B2-Mittelgesicht leicht geglättet und an Lidern und
  Gesichtsrand ausgeblendet. Der Probebau nimmt ihn mit `vorbilder=<npz>,…` als Form `vorbild_<name>` auf (Brauen,
  Wimpern, Lider, Kragen folgen der Haut). Bogenschlüssel `formen: {"vorbild_lps": 0.8}`.
- **Messung** `--varianten=name:glb-ordner:weg_a:malfilter,…` (gepaart wie `--staende`) und `--stufe=ultra`.

### Vorbildköpfe (Lizenzen)

| Vorbild | Lizenz, Namensnennung | genutzt |
|---|---|---|
| Lee Perry-Smith „Infinite“ (Infinite Realities), Netz aus three.js | CC BY 3.0, „Infinite, 3D Head Scan by Lee Perry-Smith“ | Mann |
| Helen Keller, Lebendmaske 1916 (Ruotolo), NPG.75.16, Smithsonian | CC0 | Frau (Einpassung fehlerhaft, Tafel mit 0,5) |
| Felix Mendelssohn, Lebendmaske, Univ. Edinburgh Open.Ed (Modell Connor Wimblett) | CC BY 4.0 | Versuch, nicht eingebaut |
| nicht genutzt: Luise von Preußen (CC BY 4.0; Tuch verdeckt eine Gesichtshälfte), Anne Sullivan Macy (CC0 laut Smithsonian-Konto, nicht am Datensatz bestätigt), Lincoln 1860 (CC0) | | |

Die Scans liegen nur lokal (`~/werkzeuge/charakter/vorbilder/`, in `quellen6.json` mit Prüfsumme); im Repository nur
Code. Abgüsse haben geschlossene Augen – die Augenregion trägt deshalb keine Scanform.

### Prüfung Halt B2

**Profil- und Schrägmaße** (Mittelgesicht 35, Gewichte aus Godot, Tiefen gegen die Tragion des Scanmittels über die
festen Augäpfel – die Tragion selbst sprang bei der vollen B2-Wange der Frau um 10,7 mm; mm, Profilwinkel in Grad).
„Makro“ = Bezug der Ausgleichsrechnung, B = mit `vorbild_lps` 0,8 bzw. `vorbild_keller` 0,8:

| Maß | Scan | Mann Makro | heute | **B2** | B2+B | Frau Makro | heute | **B2** | B2+B | Ziel B2 |
|---|---|---|---|---|---|---|---|---|---|---|
| Wangenwölbung | 7,1 | 7,1 | −1,4 | **5,3** ✓ | 6,8 | 7,4 | −1,1 | **5,5** ✓ | 6,1 | +3…+6 |
| Unterwange x40 | 82,6 | 82,2 | 67,0 | **80,9** ✓ | 84,2 | 88,0 | 67,6 | **85,5** ✓ | 84,6 | ≥ Makro − 3 |
| Unterwange x50 | 69,2 | 69,1 | 51,1 | **67,9** ✓ | 69,8 | 77,4 | 50,6 | **71,7** ✗ (−5,7) | 75,5 | ≥ Makro − 3 |
| Augentiefe | 3,6 | 3,5 | 9,6 | **7,6** ✓ | 6,7 | 6,1 | 6,8 | **7,0** ✓ | 1,9 | ≤ Phase 5 + 2 |
| Glabella (t_g) | 102,8 | 102,9 | 111,1 | **105,3** ✓ | 106,6 | 106,2 | 106,8 | **107,1** ✓ | 96,7 | Makro + 2…5 / 0…3 |
| Nasenspitze (t_prn) | 122,1 | 122,0 | 127,8 | **123,3** ✓ | 122,9 | 123,5 | 122,1 | **123,8** ✓ | 120,7 | Makro + 0,5…3,5 / 0…2,5 |
| Oberlippe vor Sn–Pg | 4,5 | 4,2 | 0,2 | **2,1** ✓ | 1,7 | 4,6 | 2,3 | **2,6** ✓ | 1,8 | 2–4 / 2,5–4,5 |
| Unterlippe vor Sn–Pg | 4,0 | 3,8 | −1,8 | **0,7** ✗ | 0,4 | 3,9 | 0,2 | **1,1** ✗ | 1,7 | 1,5–3,5 / 2–4 |
| Profilwinkel g–sn–pg | 167,3 | 167,2 | 170,8 | **171,7** ✗ knapp | 172,5 | 163,8 | 174,9 | **173,1** ✗ | 163,4 | 166–171 |
| Lidspalte Breite | 25,9 | 25,6 | 23,7 | **26,7** ✓ | 26,7 | 24,9 | 19,9 | **25,3** ✓ | 25,3 | ≥ Makro − 0,6 |
| Lidspalte Höhe | 7,9 | 7,0 | 6,0 | **6,5** ✓ | 6,5 | 8,1 | 6,4 | **7,5** ✓ knapp | 7,5 | ≥ Makro − 0,6 |
| Unterkiefer go–go | 126,6 | 126,6 | 113,3 | **125,4** ✓ | 123,6 | 123,2 | 104,7 | **116,8** | 126,3 | Mann ≤ Makro + 1; Frau 0,88–0,92 × Mann |
| Kinn über Kragen | – | – | 9,5 | **42,8** ✓ | 44,3 | – | 6,3 | **27,8** ✗ knapp | 28,8 | ≥ 30 |

Die Ausgleichsrechnung (Lauf 3, `ausgleich_b2.json`) zeigt am Probebau dieselben Richtungen; die Unterwange x50 der
Frau und die Unterlippen bleiben knapp, weil Kinn vorschieben (`pg_hinter_li` ≤ 4) und Lippen vor Sn–Pg gegeneinander
arbeiten. Kragen mit `ausschnitt=30`: die cos³-Senkung wirkt am Rand des Messfensters (|x| ≤ 20 mm) schwächer.

**Blindtest** (ein unabhängiger Prüfer, nur die drei Tafeln mit Fotovorlage und fünf gemischten Spalten A–E,
Schlüssel vorher versiegelt, SHA-256 `2b7fb8a41732510a04007d39cf0a725c607719a932f12968217070359cacf929`;
Note 1–10 „wirkt lebendig und glaubwürdig“):

| | heute | B2-Form | B2 + A | B2 + A + B | Malfilter |
|---|---|---|---|---|---|
| Mann 35 | **5,5** | 5 | 5 | 4 | 3 |
| Frau 35 | 4 („deutlich hager“) | **5,5** | 5 | 4,5 | 3 |
| Mann 70 | **5,5** | 5 | 4,5 | 4 | 3 |

- B2 nimmt der Frau das Hagere; bei den Männern wirkt dem Prüfer die kantigere Form von heute erwachsener, B2 „zu jung
  und zu voll“. Weg A bringt blind nichts (0 bis −0,5), Weg B −1, der Malfilter −2 (Kuwahara-„Mosaik“ auf der Haut).
- Puppenursachen in **allen** Spalten (Prüfer): glatte, gleichmäßig rosa Haut ohne Poren, Fältchen, Sommersprossen und
  echten Bartschatten; schwarze, stachelige Oberwimpern und eine Strichlinie am Unterlid; harter Glanzstreifen auf dem
  Nasenrücken und ein heller senkrechter Streifen unter dem Auge im Profil; blaugrauer Glanzfleck vorn an der Braue im
  Profil; orange leuchtende Nasenlöcher; schwebende Haarsträhnen vor dem Ohr (Frisur „frau_knoten“); orangefarbene
  Bänder an Schläfe und Stirn (Kopfhaut unter dem Haar, Männer).
- Weg B der Frau: Kellers Abguss ließ sich nicht sauber einpassen (Ringe um die Lidöffnung, Kante an der Stirn; nach
  der Ausrichtung blieben 20 mm Restbewegung – die Merkpunkte am geschlossenen Auge passen nicht zur Fläche). Lee
  Perry-Smith ist sauber (Restabstand 0,4 mm, RMS des Versatzes 2,4 mm), macht den Kopf aber nicht lebendiger.
- Nach dem Blindtest behoben (nur Code, Tafel 4 zeigt noch den Fehler): Weg A machte Frauenbrauen mit dünner Form fast
  unsichtbar.

**Kosten** (T400 ohne fremde GPU-Prozesse, 1280 × 720, Porträt 20°, Stand 6, gepaart im selben Prozess:
`--varianten=haltb:glb6:0:0,b2a:glb6b2:1:0,b2am:glb6b2:1:1`, Median aus 3 Wiederholungen, Commit ce45ce4e; Daten
`b2/m2_hoch`, `b2/m2_ultra` im Scratchpad des Teamleiters):

| Lauf | Hoch: Halt B → B2 + A | + Malfilter | Ultra: Halt B → B2 + A | + Malfilter | Grafikspeicher |
|---|---|---|---|---|---|
| ohne Haar, Mittelgesicht | 6,51 → 6,89 ms (**+0,35**) | +3,38 | 8,07 → 8,38 ms (**+0,31**) | +3,41 | +1 MB (Zonen), Malfilter +6 MB |
| ohne Haar, Zufallsperson | 6,66 → 7,07 ms (**+0,40**) | +3,52 | 8,19 → 8,56 ms (**+0,37**) | +3,45 | wie oben |
| TressFX in Ruhe | 30,38 → 30,25 ms (+0,06, Rauschen) | +3,40 | 31,53 → 31,62 ms (−0,07) | +3,23 | wie oben |

Der Malfilter rechnet bildschirmfüllend (73 Bildzugriffe je Bildpunkt); im kleinen Porträtfenster wäre er billiger,
hat aber im Blindtest die schlechteste Note.

### Offen nach Halt B2

- Nutzerentscheidung „Puppe ja/nein“ (Mappe `b2.md`); bei „weiter“: Männer wieder etwas kantiger (Blindtest),
  Unterlippe vor Sn–Pg, Unterwange x50 der Frau, Kinn über Kragen der Frau (27,8 mm).
- Puppenursachen aus dem Blindtest (alle Spalten, unabhängig von B2): Haut (Etappe C), Wimpern (Dichte, Unterlid-Linie),
  Glanzstreifen Nasenrücken und unter dem Auge, Brauenglanz im Profil, Nasenlöcher, Strähnen vor dem Ohr, Kopfhautbänder.
- Weg B nur mit sauberen Scans mit offenen Augen sinnvoll (Abgüsse haben geschlossene Lider); Keller ausgeschieden,
  Mendelssohn (Restabstand 1,6 mm, Haarsträhnen auf der Stirn) nicht eingebaut.
- Der Malfilter in dieser Form (Kuwahara 4 Sektoren) taugt nicht; ein Ölbild-Eindruck bräuchte einen anisotropen
  Filter oder gemalte Haut statt eines Nachfilters.

## Etappe C: Haut (Probekopf B2)

Anlass: Der blinde Prüfer von Halt B2 sah in allen Spalten eine Puppe – glatte, gleichförmig rosa Haut ohne Poren,
Falten und Bartschatten, schwarze Wimpernlinie am Unterlid, Glanzstreifen auf Nase und unter dem Auge, orange
leuchtende Nasenlöcher. Diagnose U4: Der Hautshader `haut6` ist an sich in Ordnung, es fehlt der **Inhalt** (Albedo
ohne Mittelfrequenzen, keine Farbzonen, keine Nasolabial- und Tränenrinne). Etappe C setzt deshalb gezielt dort an,
statt S8–S11 vollständig abzuarbeiten. Alles läuft als Weg C neben Weg A (`weg_a` + `weg_c`), Stand 6 und Weg A bleiben
unverändert.

### Was gebaut ist

- **Hautkarten** `tools/charakter/kopf6_hautc.py` (numpy und OpenImageIO in Blenders Python, ≈ 1 min CPU): aus dem
  Grundnetz eine Lagekarte (3D-Lage und Normale je Bildpunkt), darauf alle Zonen und Falten in Millimetern an den
  Merkpunkten; Ränder mit 3D-Rauschen unregelmäßig, links und rechts verschieden.
  - `haut_c_zonen.png` (1024²): R Hämoglobin (Wangen, Nase, Ohren, Kinn, Lider, Lidrand, Karunkel), G Sonne/Gelbton
    (Stirn, Schläfen, Nasenrücken, Jochbein, Ohrrand, Nacken, Scheitel – als Maximum, nicht als Summe), B Augenringe und
    Venen, A Bartgebiet (unter der Linie Koteletten–Wange–Mundwinkel, Oberlippe nur bis zum Mundwinkel, ohne Lippenrot).
  - `haut_c_glanz.png` (1024²): R Rauheit je Zone (0,25 + 0,4 R; Wange 0,49, Stirn 0,42, Nase 0,40, Spitze 0,37,
    Lippen 0,33, Hals 0,56), G Porenstärke je Zone, B Höhle, A Feuchte (Lidrand, Karunkel).
  - **Nasenloch innen** (Kanal B): eine Innenwand blickt zur Nasenlochachse; dunkel wird es erst 1–6 mm über der
    Öffnungslinie, die vom Flügelansatz zur Spitze ansteigt. Die Unterseite des Flügelrands bleibt Haut (sonst schwarze
    Ränder bei Stupsnasen und schräg); die gebackene Verdeckung `haut_masken.B` taugt hier nicht (an der Flügelrinne
    ebenso hoch) und zählt nur noch am Ohr.
  - `haut_c_relief.png` (2048²): Höhen in Bildpunkt-Einheiten, vier Alterskanäle mit eigenem Maßstab (`haut_c.json`):
    R Stirn (vier Querfalten aus versetzten Stücken, Nebenfalten, Zornesfalten, Nasenwurzel), G Augen (Krähenfüße in
    sechs Strahlen, Knitter am Unterlid), B Mittelform (Nasolabialfalte mit Wangenwulst, Nasenflügelrinne,
    Tränenrinne, Unter- und Oberlidfalte, Kinnlippenfurche, Philtrum, Lippenrillen), A Mund und Hals
    (Marionettenfalten, Fältchen der Ober- und Unterlippe, Wangenfalten, drei unterbrochene Halsringe). Linien liegen in
    der Bildebene (xy) mit z als Tor – der 3D-Abstand zerfiel seitlich am Auge in Perlen.
- **Detailkacheln** `tools/charakter/kopf6_kacheln.py` (prozedural, nahtlos, keine Fremddaten): `haut_c_poren.png`
  (1024², 25 mm: Poren 0,25–0,5 mm, feine Poren, Hautfelderung, Mikrorauschen als Normale; Höhle; Bartstoppeln als
  Striche 0,35–0,8 mm) und `haut_c_flecken.png` (512², 60 mm: Melanin- und Blutunruhe 2–18 mm, Rang und Form der
  Flecken).
- **Shader** `haut6.gdshaderinc`, Zweig `WEG_C` (`haut6c`, `haut6c_ultra`, `haut6c_einfach` für Niedrig/Mittel ohne
  Kacheln): Farbe als Absorption, albedo = Grundton · Hautbild^1,15 · exp(−Σ Pigment · k) mit Melanin (Sonne, Bräune,
  Unruhe in zwei Weiten, Sommersprossen, Altersflecken in zwei Größen gehäuft), Hämoglobin, Augenringe (bräunlich),
  Gelbstich (Stirn und überall). Bartschatten kühl mit zwei Stoppellagen, zusätzlich mit Mip-Versatz 1,5 abgetastet und
  geschwellt (die Striche werden im Porträt zu sichtbaren Punkten). Lippenrot über die Glanzkarte (A ≈ 0,3) weniger
  lachsfarben. Relief × 3,2 nach Alter, Furchenschatten
  vor allem aus den Alterskanälen. Poren in zwei Lagen (die Kachel ist auf 40 mm gestreckt, sonst lägen Poren im
  Porträt unter einem Bildpunkt). Rauheit je Zone statt T-Zonen-Maske, Doppelglanz breiter (× 1,3 und × 0,75,
  Mischung 0,85) – kein Glanzstreifen. Nasenloch und Gehörgang: kein direktes Licht, auch kein Rand- und Flächenlicht
  (beides glühte vorher orange; das Randlicht wurde nach der Abdunklung addiert). Lippenkante weicher (Farbton aus
  Mip 2, Helligkeit scharf). Prüfanzeige `c_zeige` (1 Höhle, 2 Glanz, 3 Zonen, 4 Furche, 5 Bart/Flecken).
- **Wimpern** `haar3.gdshader`: Band des Unterlids (UV.y > 0,75) bei Weg C mit Deckung 0,3, Wurzel ausgeblendet und
  heller (keine Naht am Lidrand); alle Wimpern durchscheinender (Deckung 0,7), lichter, etwa ein Viertel weniger Haare,
  im Alter grau wie die Brauen.
- **Werte je Person** `charakter_kopf.gd` `_haut_c_setzen()`: Relief nach `RELIEF_ALTER` (Augen ab 20, Stirn ab 25,
  Mittelform schon in der Jugend schwach, Mund ab 35), Wetterhaut `wetter` 0–1 altert die Haut um bis zu 10 Jahre und
  bräunt; Gene `hautroetung` und `sommersprossen` (steile Kurve: 110 ≈ 3 %, 150 ≈ 11 % der Flecken auf Nase und Wangen),
  Bartschatten aus dem Bartwuchs (dunkles Haar ≈ 25 % dunkler). Alle Abstimmwerte setzt jede Zelle neu.
- **Schau** `charakter_schau.gd`: Bogenschlüssel `weg_c`, `wetter`, `haut_werte` (Shaderwerte zum Abstimmen),
  Messvarianten mit 5. Feld `weg_c`, `--stufe=mittel|niedrig` nimmt `haut6c_einfach`. `kopf6.sh haut_c` backt nur die
  Haut neu; die volle Kette ruft beides nach den Haarbildern auf.

### Prüfung Etappe C

Bildtafel je Person (vorn, schräg, Profil): Fotovorlage | B2 (vorher, Weg A) | neue Haut | neue Haut nah. Blinder
Prüfer (eigener Agent, nur die Bilder, A/B zufällig, Schlüssel vorher mit SHA-256 versiegelt): Fotovorlage, A, B und
dieselben drei nah; Note 1–10 „wirkt neben dem Foto lebendig und glaubwürdig“. Ziel: neue Haut ≥ 2 Punkte über B2.
Wetter im Bogen: Mann 35 0,25, Frau 35 0,1, Mann 70 0,55; Sommersprossen 110 / 150 / 90 (nach den Fotos).

| Runde | Mann 35: B2 → neu | Frau 35: B2 → neu | Mann 70: B2 → neu | Commit |
|---|---|---|---|---|
| 1 | 3,5 → 5 (+1,5) | 3,5 → 4,5 (+1,0) | 2,5 → 5 (+2,5) | b96ec8d3 |
| 2 | 3,5 → 5 (+1,5) | 3 → 5 (+2,0) | 2,5 → 5,5 (+3,0) | 71d21ec0 |
| **3** | **3 → 5 (+2,0)** | **2,5 → 4,5 (+2,0)** | **2 → 4 (+2,0)** | 8590edb1 |

Je Runde ein frischer Prüfer; Schlüssel und SHA-256 in der Mappe (`versiegelt.txt`). Die neue Haut gewinnt in jeder
Runde bei jeder Person; im Endstand überall genau +2 – knapp erreicht. Absolut 4–5 von 10: Was der Prüfer im
Endstand noch als Puppe sieht, steht unter „Offen“. Runde 2 antwortete auf Runde 1 (Nasenloch schwarz gestanzt, Fleck
neben der Nase statt Bartschatten, Augenringe violett mit harter Kante, Falten aufgemalt, Flecken orange), Runde 3 auf
Runde 2 (Dreitagebart nur ein Hauch, Wimpern schwarz und hart, Lidränder und Ohren glühen, Lippen lachsfarben).

Kosten (T400, 1280 × 720, gepaart im selben Prozess, drei Wiederholungen, Median; Endstand 8590edb1):

| Lauf | Hoch: B2 + A → neu | gegen Halt B | Mittel (ohne Kacheln): B2 + A → neu | Ultra: B2 + A → neu |
|---|---|---|---|---|
| ohne Haar, Mittelgesicht | 6,97 → 7,05 ms (**+0,07**) | +0,48 | 7,00 → 6,11 ms (−0,86) | 8,38 → 8,40 ms (−0,01) |
| ohne Haar, Zufallsperson | 7,14 → 7,13 ms (−0,01) | +0,37 | 7,14 → 6,22 ms (−0,92) | 8,56 → 8,53 ms (+0,01) |
| TressFX in Ruhe | 30,84 → 30,82 ms (+0,37 gepaart, fremde Last) | +0,10 | 30,86 → 30,20 ms (−0,66) | 32,10 → 32,05 ms (+0,03) |

Runde 1 bei ganz freier Karte: ohne Haar +0,08, Zufall +0,08, TressFX +0,11 ms gegen B2 + A. Grafikspeicher: neue
Karten und Kacheln 38,7 MB unkomprimiert mit Mipmaps (Relief 2048² allein 21,3 MB); in der Messung nicht getrennt
sichtbar, weil alle Varianten im selben Prozess dieselben Bilder laden. „Mittel“ ist billiger, weil die einfache
Fassung auch die alten Falten- und Rauschzugriffe nicht mehr braucht.

### Selbst getroffene Entscheidungen (Etappe C)

- Gezielt nach den Befunden statt S8–S11 vollständig (kein neues Hautlicht nach S8, keine MakeHuman-Systemhäute nach
  S9, kein `haut_falten2` nach S11); die Pigmentvektoren des Plans S9 sind übernommen.
- Stoppelbart als Standard erwachsener Männer (nach `bartwuchs`); tägliche Rasur ist im Spätmittelalter unüblich. Später
  nach Stand und Beruf.
- Poren gröber als echt (Kachel auf 40 mm gestreckt) – gemalt-realistisch statt unsichtbar.
- Wetter und Sommersprossen der drei Prüfpersonen nach den Fotos gesetzt (Lebensspuren, nicht Form).

### Offen nach Etappe C

- Nasenloch innen: Farbe zwischen „schwarz gestanzt“ (Runde 1) und „glüht rötlich“ (Runde 3, Frau mit Stupsnase) –
  innen dunkelbraun statt rot.
- Falten lesen sich im weichen Porträtlicht eher als Farbe; Tränensäcke, Schlupflider und tiefe Stirnfurchen brauchen
  Form (FaceBuilder-Vorbereitung läuft).
- Stoppeln zu gleichförmig, harte Kante zur Wange im Profil; Bartschatten darunter kräftiger, Dichte auslaufen lassen.
- Altersflecken auf der Glatze noch zu orange.
- Augen glasig (Lederhaut zu rein, kein feuchter Unterlidrand) – S12.
- Texturen komprimieren (38,7 MB → etwa 10 MB) vor einem Einbau ins Spiel; Ultra-Fassung `haut6c_ultra` nur gemessen,
  nicht im Blindtest.

### Testanleitung Etappe C

1. Karten backen: `tools/charakter/kopf6.sh haut_c` (Standard `~/werkzeuge/charakter/glb6`; für den Probekopf
   `GLB=~/werkzeuge/charakter/glb6b2`).
2. Bogen mit `"weg_a": true, "weg_c": true, "wetter": 0.5` auf einer Zelle mit `"kopf": "~/werkzeuge/charakter/glb6b2/kopf6.glb"`
   und `"stand": 6` rendern: `GLB=~/werkzeuge/charakter/glb6 ANZEIGE=:73 tools/charakter/schau.sh <ordner> 1280x720
   --bogen=<bogen.json> --ende`.
3. Prüfanzeige: `"haut_werte": {"c_zeige": 1}` (Höhle), 3 (Zonen), 4 (Furche); Abstimmen über `haut_werte`.
4. Messung: `--varianten=b2a:~/werkzeuge/charakter/glb6b2:1:0:0,hc:~/werkzeuge/charakter/glb6b2:1:0:1 --stufe=hoch|mittel|ultra --messen`.

## Etappe D: Augen, Augenpartie, Brauen, Nase (Probekopf B2 + Haut C)

Anlass: Die blinden Prüfer von Haut C und vom FaceBuilder-Test sahen die Puppe vor allem an vier Stellen: Augen glasig,
Augenpartie flach, Brauen wie aufgeklebt, Nase zu kurz. Etappe D liegt als Weg D (`weg_d`, nur mit `weg_a`) neben
Weg A und C; Stand 6, Weg A und Weg C bleiben unverändert. Kopf: `~/werkzeuge/charakter/glb6d` (Probebau `glb6p_d2`).

### Was gebaut ist

- **Augenpartie als Form** (`kopf6_eigen.py`, nur mit `eigen=2`): `eigen_augenhoehle` (Rinne unter der Braue über der
  inneren Lidhälfte bis 1,8 mm, Mulde am inneren Lidwinkel 1,0 mm), `eigen_deckfalte_m/_w` (Haut 0–4,5 mm über der
  Lidfalte sinkt 0,7 mm und tritt 0,5 mm vor, außen stärker; liegt mit der Lidfalte in `lidfalte_<g>` und wird beim
  Lidschluss ausgeblendet), `eigen_unterlid` (Lidwulst 0,45 mm, 2 mm unter dem Rand; Lid-Wangen-Furche 0,35 mm bei
  5,5 mm). Der Lidrand bewegt sich höchstens 0,13 mm. Fest in der Reife oben: Augenhöhle 1,0, Unterlid 1,0, Tränenrinne
  +0,6; Deckfalte Mann 1,0, Frau 0,4. Ruhelage des Oberlids `lid_ruhe.reife` 0,1 → 0,22 (im Bild lag der Rand bei 0,1
  auf dem Limbus – Starrblick); steht nur in der `regeln6.json` des Probekopfs.
- **Nase als Ziel der Ausgleichsrechnung** (`formen_ausgleich_b2.py --etappe=d --basis=<ausgleich_b2.json>`): nur die
  Nasenformen und die Lage des Mundes rechnen, alles andere bleibt wie B2 (im ersten Lauf ohne `--basis` wanderten Kinn
  und Kiefer mit). Neue Maße in `profil_masse`: `ps_prn_ipd` (Nasenspitze unter der Pupillenlinie / IPD), `al_ipd`,
  `nase_kinn` (Subnasale / Menton ab der Pupillenlinie) und `nasolabial` (Tangente an den Nasensteg gegen sn → ls).
  Ziele aus den 26 Fotovorlagen (Mittel Mann/Frau: Subnasale 0,769/0,752 IPD, Spitze 0,625/0,593, Nase–Kinn
  0,409/0,424, Flügel 0,542/0,500) und Farkas (n–sn Mann 54,8 ± 3,3, Frau 50,6 ± 3,1 mm; Nasolabialwinkel Mann 90–100°,
  Frau 95–110°). Die Frau bleibt unter dem Fotomittel, weil n–sn sonst über Farkas + 1 σ wüchse.
- **Augen** (`augen6.gdshader`, Werte über `charakter_kopf.gd`): Lederhaut matter (SPECULAR 0,25, Rauheit 0,32), zu den
  Winkeln getönt, außen am Limbus ein weicher Saum statt des hellen Rings, Iris mit Parallaxe (`iris_tiefe` 0,09),
  dunklerer Limbus; Pupille nach dem Licht am Ort (`pupille_licht`, Bogenschlüssel, 0,26–0,46 des Irishalbmessers).
- **Lidschatten** (`lidschatten6.gdshader`, beide Seiten sichtbar): Schatten des Oberlids (bis 3,2 mm unter dem
  Lidrand) und Kontaktschatten an beiden Lidkanten, multipliziert. Das ICT-Lidschatten-Netz deckt die ganze Lidspalte;
  mit `cull_back` (alter Shader) kam davon fast nichts an. Eckfarbe neu aus `kopf2_bauen.py`: G = Oberlid-Seite,
  B = Abstand zum sichtbaren Lidrand (ICT-Merkpunkte 36–47) / 8 mm – der Abstand zur Lidnaht taugt nicht, sie liegt tief
  unter dem Lid.
- **Hornhaut und Tränenfilm** (`hornhaut.gdshader`): Hornhaut zeichnet nach dem Lidschatten (`render_priority`),
  Rauheit 0,045, dazu ein weiches Fenster-Glanzlicht (Spiegelung einer Lichtfläche schräg über der Kamera, unabhängig
  von den Lampen; Stärke mit `pupille_licht`) – der Glanzpunkt des Schlüssellichts lag unter dem tieferen Oberlid.
  Tränenfilm: glänzt am Unterlid, oben und an der Karunkel kaum (Eckfarbe). Lidrand nasser (`feucht` 1,7).
- **Brauen** (`braue6.gdshader`, gilt mit `weg_d` auch für die Wimpern): Alpha-Mischung statt Alpha-Schere – Haare
  schmaler als ein Bildpunkt decken anteilig. Bilder `kopf3_haartexturen.py weich=1`: Rand läuft über das äußere Drittel
  aus, Flaum, lichter Kopf mit fast senkrechten Haaren, Büschel und Lücken in zwei Weiten. Karten mit `brauen_d=1`
  näher an der Haut und nach oben abstehend (0,3/0,7 mm, an der Oberkante +0,4/+1,1 mm). Farbe nah am Haar, je Haar
  verschieden hell, ergrauen später als das Haupthaar (× 0,6) und grau-braun statt weiß; kein Hautglanz unter der Braue.
  **Je Person** (Verschiedenheitsplan, Hebel 1): `braue6` verschiebt das Brauenbild in der Karte – Stand 1,5 mm je σ von
  `brauen_stand` zusätzlich zur Hautform (zusammen ≈ 2,5 mm/σ), Dicke 0,7–1,4 aus `brauen_dichte`, Winkel aus einem
  künftigen Gen `brauen_winkel` (Vorschlag an kern, heute 0). Mittelgesicht unverändert.
- **Wimpern** (Runde 2): brauner, lichter, Dichte 0,55 (Mann) / 0,62 (Frau).
- **Vierter FaceBuilder-Kopf** `h_w35_hell` (`fb_lauf.sh kopf h_w35_hell`, 4 von 4 Fotos ausgerichtet, RMS-Versatz
  1,9 mm) als Form `vorbild_fb_h_w35_hell` im Probebau; Tafel „Frau 35 mit FaceBuilder-Form“.

### Prüfung Etappe D

Bildtafel je Person (vorn, schräg, Profil): Fotovorlage | vorher (Haut C) | neu | neu nah; Profilfoto an Scheitel und
Nasenspitze ausgerichtet (Mann 70 von Hand). Vierte Tafel: Frau 35 mit der FaceBuilder-Form `h_w35_hell` („vorher“ ist
dort Haut C ohne FB). Blinder Prüfer je Runde (frischer Agent, nur die Bilder, A/B zufällig, Schlüssel vorher mit
SHA-256 versiegelt): Note 1–10 „wirkt neben dem Foto lebendig und glaubwürdig“. Ziel: neu ≥ 1,5 Punkte über vorher.

| Runde | Mann 35 | Frau 35 | Mann 70 | Frau 35 + FB | Mittel | Commit |
|---|---|---|---|---|---|---|
| 1 | 4,5 → 4,5 (±0) | 3,5 → 4,5 (+1,0) | 3,0 → 4,0 (+1,0) | 3,5 → 4,5 (+1,0) | +0,75 | 83f49933 |
| 2 | 3,5 → 4,5 (+1,0) | 4,0 → 5,0 (+1,0) | 4,0 → 5,0 (+1,0) | 4,0 → 5,0 (+1,0) | **+1,0** | 43b3741d |
| 3 | 4,0 → 4,5 (+0,5) | 4,0 → 4,5 (+0,5) | 4,0 → 3,5 (−0,5) | 4,0 → 4,5 (+0,5) | +0,25 | 984bce96 |

Das Ziel +1,5 ist **nicht erreicht**. Neu gewinnt in 11 von 12 Vergleichen; die Prüfer streuen von Runde zu Runde um
etwa einen Punkt. Runde 1 → 2 antwortete auf: kein Glanzpunkt, Stachelwimpern, Frauen-Oberlid mit harter Kante,
Brauen „nachgezogen“, Männernase im Profil kurz. Runde 2 → 3 auf: Augenweiß zu hell, Iris scharf, Drahtwimpern,
Nasenloch rot ausgeleuchtet, violette Linie unter dem Unterlid. Was die Prüfer bei „neu“ übereinstimmend besser fanden:
feine, helle Wimpern, weiche Brauen, längere Nase im Profil, schwerere Lider (Runde 2 „passt zum Alter“). Was bleibt
(neu): Haut zu glatt und gleichförmig (stärkstes Merkmal aller drei Runden, nicht Teil dieser Etappe), Auge im Profil
flach (keine sichtbare Hornhautwölbung), Oberlider wirken teils geschwollen und rötlich, beim Mann 70 „halb zu“
(Runde 3), Nasenlöcher der Frau von vorn sichtbar, harte Kante an der Nasenflügelfurche (auch vorher).

Lidschluss geprüft (`eyeBlink` 1,0, vorn und schräg): Lid schließt, die Deckfalte blendet aus, keine Wülste.

Kosten (T400, 1280 × 720, gepaart im selben Prozess, drei Wiederholungen, Median; Commit 83f49933; Runde 2/3 ändern
nur Werte und einen Abtastpunkt):

| Lauf | Hoch: Halt B → D | D gegen Haut C | Ultra: Halt B → D | D gegen Haut C |
|---|---|---|---|---|
| ohne Haar, Mittelgesicht | 6,51 → 6,95 ms (**+0,41**) | −0,01 | 7,98 → 8,32 ms (+0,33) | +0,02 |
| ohne Haar, Zufallsperson | 6,67 → 7,11 ms (**+0,46**) | ±0,00 | 8,13 → 8,52 ms (+0,39) | +0,04 |
| TressFX in Ruhe | 30,41 → 30,34 ms (**+0,15** gepaart) | +0,08 | 30,75 → 30,83 ms (+0,06) | −0,06 |

Budget +1,0 ms auf Hoch eingehalten. Grafikspeicher gleich (die weichen Brauenbilder ersetzen die alten).

### Selbst getroffene Entscheidungen (Etappe D)

- Augenformen fest in der Reife statt als Gene – die Variation je Person bringt der Verschiedenheitsplan (Hebel 3);
  Deckfalte der Frau 0,4 nach Runde 1.
- Nasen-Ausgleich mit allen übrigen B2-Gewichten fest (`--basis`), sonst misst der Vergleich Kiefer statt Nase.
- Fenster-Glanzlicht auf der Hornhaut unabhängig von den Lampen der Szene (Stärke mit `pupille_licht`); ein
  physikalischer Glanzpunkt lag unter dem tieferen Oberlid.
- Wimpern bekommen mit Weg D denselben gemischten Shader wie die Brauen (nicht im Auftrag, aber vom Prüfer als
  „Stachelkranz“ angemahnt).
- Brauen je Person nur über vorhandene Gene (`brauen_stand`, `brauen_dichte`); `brauen_winkel` und `brauen_dunkel` sind
  Vorschläge an kern (über den Teamleiter, gebündelt nach dem Verschiedenheitsplan). Der feste Brauenschatten auf der
  Haut wurde nicht verstärkt (er wandert mit der UV-Verschiebung nicht mit).

### Offen nach Etappe D

- Haut: feine Falten unter dem Auge, Krähenfüße und Stirn als Relief statt Farbe; Ton weniger einheitlich
  pfirsich-orange (alle drei Prüfer: stärkstes Puppenmerkmal, vor allem beim Mann 70).
- Auge im Profil: Hornhaut wölbt sich kaum vor (ICT-Augapfel mit flacher Kuppe); eine echte Hornhautkuppe (≈ 2,5 mm vor
  dem Limbus) bräuchte den Bau und eine Prüfung gegen das Oberlid.
- Oberlid: Ruhelage 0,22 plus Greis 0,06 macht den Mann 70 schläfrig; Deckfalte und die rote Lidzone der Haut lesen
  sich als „geschwollen“. Vorschlag: Ruhelage 0,18 und Greis 0,03, Lid-Rötung der Haut bei Weg D schwächer.
- Nasenlöcher der Frau von vorn sichtbar; MakeHuman `nose-nostrils-angle-down` und `nose-base-down` liegen bereit, es fehlt
  ein Maß für die Sichtbarkeit von vorn. Harte Kante an der Nasenflügelfurche (Relief der Haut C).
- Profilfoto im Vergleich nur über Scheitel und Nasenspitze ausgerichtet (Mann 70 von Hand); FaceBuilder-Kamera nur für
  die vier FB-Köpfe.
- Verschiedenheit (Plan vom 01.11.2026): Augenformen als eigene, je Person regelbare Formen (Hebel 3), Gen-Vorschläge
  `brauen_winkel`, `brauen_dunkel` an kern.

### Testanleitung Etappe D

1. FaceBuilder-Kopf (Test bis 15.11.): `~/werkzeuge/charakter/facebuilder/fb_lauf.sh kopf h_w35_hell`.
2. Probebau: `GLB_PROBE=~/werkzeuge/charakter/glb6p_d2 PROBEN_OPT="ausschnitt=30 eigen=2 brauen_d=1
   vorbilder=<arbeit/h_w35_hell/vorbild_fb_h_w35_hell.npz>" tools/charakter/kopf6.sh proben`.
3. Nase: `$BPY -I tools/charakter/formen_ausgleich_b2.py --glb=<glb6p_d2/kopf6p.glb> --gewichte=<gewichte_s0_headless.json>
   --aus=<ordner> --etappe=d --basis=<B2-Lauf 3/ausgleich_b2.json>` (≈ 4 min).
4. Eichung wie B2 mit `--rezept_s4=<ordner>/rezept_s4_d.json --regeln_aus=~/werkzeuge/charakter/glb6d/regeln6.json
   --rezept_aus=~/werkzeuge/charakter/glb6d/kopf6_rezept_d.json --zuege_aus=<zuege_basis_v1.json aus B2>`, dann in
   `glb6d/regeln6.json` `lid_ruhe.reife` = 0,22; Mischen `GLB=~/werkzeuge/charakter/glb6d GLB_PROBE=…/glb6p_d2
   REZEPT=…/kopf6_rezept_d.json tools/charakter/kopf6.sh mischen`.
5. Bilder: `glb6d/kopf6_tex` wie `glb6b2/kopf6_tex` verknüpfen (ohne `brauen6_*`), dann `$BPY -I
   tools/charakter/kopf3_haartexturen.py ~/werkzeuge/charakter/glb6d/kopf6_tex weich=1`; Haar `GLB=…/glb6d
   GLB5=~/werkzeuge/charakter/glb6 tools/charakter/kopf6.sh nur_haar`.
6. Bogen: Zelle mit `"kopf": "~/werkzeuge/charakter/glb6d/kopf6.glb", "stand": 6, "weg_a": true, "weg_c": true,
   "weg_d": true` (optional `"pupille_licht": 0–1`); Mimik nur auf Zeilen- oder Spaltenebene (Zellen erben sie nicht).
7. Messung: `--varianten=haltb:~/werkzeuge/charakter/glb6:0:0:0:0,hc:~/werkzeuge/charakter/glb6b2:1:0:1:0,
   hd:~/werkzeuge/charakter/glb6d:1:0:1:1 --stufe=hoch|ultra --messen`.

## Verschiedenheit E0 und E1: Prüfmaß und schnelle Welle (Probekopf glb6e)

Anlass: Verschiedenheitsplan vom 01.11.2026 – alle Köpfe wirken wie Geschwister. Die Streuung saß an Kinn, Kiefer und
Umriss (2,6–2,7 mm), Augen, Lider und Brauen bewegten sich nur um 0,5–1 mm. Nutzerentscheidungen (01.11.2026): Augen und
Brauen leicht spielhaft (≈ 1,3 × der natürlichen Streuung), die Grenze setzt das Fratzen-Panel; andere Völker später (nur
Menschen); kein FaceBuilder-Abo. E1 umfasst die Hebel 1 (Brauen) und 2 (Augen neu eichen, Budget) des Plans, dazu
Nasenbudget, Oberlid und Nasenloch; Hebel 3–5 (Lidformen, Grundköpfe, Leitmerkmal) folgen in E2–E4.

### E0: Prüfmaß

- **Panel** `tools/charakter/boegen6/panel_v.py` (fest und wiederholbar): je Geschlecht 16 Zufallspersonen (Saaten
  9001–9016 bzw. 9101–9116, 35 Jahre, nur die Gestalt-Gene aus der Saat; Haut, Haar- und Augenfarbe, Frisur und Licht
  fest). Blatt A vorn (K1, A1–A16), Blatt B schräg (35°, gemischt), Blätter C1/C2 mit 15 Paaren (5 echte Geschwister mit
  gleichen Eltern, 10 Fremde, gemischt) und Blatt X mit 8 Extremköpfen aus einer Stichprobe von 300 (5 nach dem
  Augenmaß √(Mittel z²) über die Augen- und Brauengene, 3 nach der Nasennorm), vorn und schräg. Gleiche Saaten in jeder
  Runde, die Mischung hängt an `--runde`. Der Schlüssel liegt außerhalb des Repositorys
  (`~/werkzeuge/charakter/bilder/phase6/V/schluessel_v_<runde>.json`), sein SHA-256 steht vor dem Urteil fest.
- **Prüfer**: drei frische Agenten je Runde, nur die zehn Blätter (4 × 4 Zellen je Blatt), Zuordnung B → A mit bloßem
  Auge (keine Messung), „Geschwister?“ 1–10 je Paar, Fratzen-Liste, Note „verschiedene Menschen“ 1–10.
- **Natürliche Streuung** (Literatur statt Gedächtnis): Lidspaltenhöhe σ 1,0–1,7 mm, Brauenhöhe σ 1,9–3,1 mm. Die Eichung
  ging von 0,84 bzw. 1,33 mm aus – also selbst am Ziel 0,75 × zu klein.
- **Befund im Brauen-Shader** (`braue6`, Etappe D): In Godot läuft `UV.y` in der Brauenkarte von oben nach unten (glTF:
  Bildzeile 0 = oben; am Netz geprüft). Etappe D rechnete umgekehrt: Ein hoher `brauen_stand` hob die Haut um 1,0 mm/σ,
  schob das Brauenbild aber um 1,5 mm/σ nach unten (netto −0,5 mm/σ), und die Dicke wuchs um eine Linie am unteren Rand.

| E0 (Stand ef84658b, Schlüssel `3f7f0485…a8d800`) | Prüfer 1 | Prüfer 2 | Prüfer 3 | Mittel |
|---|---|---|---|---|
| Männer: B → A richtig (Zufall 1/16) | 2/16 | 4/16 | 3/16 | **3,0/16** |
| Frauen: B → A richtig | 3/16 | 2/16 | 6/16 | **3,7/16** |
| Männer: Geschwister echt / fremd (Mittel 1–10) | 5,4 / 4,5 | 5,2 / 4,2 | 5,2 / 4,6 | 5,3 / 4,4 |
| Frauen: Geschwister echt / fremd | 4,8 / 4,6 | 4,6 / 4,8 | 4,8 / 4,4 | 4,7 / 4,6 |
| Fremde mit „Geschwister“ ≥ 6 (Männer, Frauen) | 1/10, 2/10 | 3/10, 3/10 | 2/10, 2/10 | 20 %, 23 % |
| Note „verschiedene Menschen“ (Männer, Frauen) | 4, 3 | 4, 3 | 4, 4 | 4, 3,3 |

- Kein Prüfer gab eine Zuordnung „sicher“; alle schreiben, schräg gingen die Unterschiede fast ganz verloren. Unterschieden
  wurde an Brauen, Lidschwere, Gesichtsbreite, Nase und Lippen; gleich wirkten Augenform, Augenabstand, Stirn und Schädel.
  Frauenbrauen „fast unsichtbar“ (alle drei), Nasenlöcher der Frau von vorn sichtbar (alle drei).
- **Fratzen E0** (von mindestens zwei Prüfern, alle „grenzwertig“ außer wo vermerkt): Männer X1 schräg (aufgeblähte Wange,
  Schnauze; zweimal „deutlich“), X4 (Schlitzaugen, vorgeschobener Mund), X7 (sehr lange, hängende Nase), X2 schräg; die
  hintere Braue steht schräg über den Stirnumriss hinaus (Karten abstehend). Frauen X6 (Stupsnase mit großen Nasenlöchern,
  einmal „deutlich“), X2/X3 schräg (Schmollmund, fliehendes Kinn), X1. Alle drei sehen in jedem Schrägbild der Männer einen
  dunklen Fleck im Haar über dem Ohr (TressFX, nicht Teil von E1).
- Die Zellen tragen nachweislich die Gene laut Schlüssel (16/16 je Geschlecht, Begleitdateien); Prüfer 2 vermutete ein
  ungemischtes Blatt B – seine 9 Wahlen „gleiche Nummer“ trafen nur 4.

### E1: Was gebaut ist

Alles hängt an den Regeldaten eines neuen Probekopfs `~/werkzeuge/charakter/glb6e`: Netz, Bilder und Haar sind
Verknüpfungen auf glb6d, nur `regeln6.json` ist neu (`tools/charakter/kopf6_verschieden.py` aus der von glb6d). Der Code
liest die neuen Abschnitte nur, wenn sie da sind; glb6d rechnet mit dem neuen Code bitgleich wie vorher (Gewichteausdruck
600 Personen, Vergleich der ganzen Datei).

- **Brauen** (Hebel 1; `braue6.gdshader`, `charakter_kopf.gd`, Abschnitt `verschieden.brauen`): `v_nach_unten` rechnet die
  Karte richtig herum; Stand 2,5 mm/σ im Bild zusätzlich zur Hautform, nach oben weich auf 1,5 mm begrenzt (über der Braue
  sind in der Karte nur 1,5–3 mm Platz, sonst wäre sie oben abgeschnitten); Winkel 2,2 mm/σ am äußeren Ende; Dicke
  1 + 0,35·z nach oben, 1 + 0,15·z nach unten (0,75–1,8) um eine Linie im oberen Drittel (wächst vor allem nach unten;
  stärker gestaucht zerfiel die Braue zum Strich); Dichte aus `brauen_dichte` breiter (0,75–1,2 statt 0,72–1,15);
  Dunkelheit gegen das Haar ±30 %/σ, im Mittel wie Etappe D; Frauen dichter (× 1,3), etwas höher (× 1,15) und nur zu 60 %
  aufgehellt (E0: „fast unsichtbar“); der feste Brauenschatten auf der Haut 0,42 → 0,22 (er wandert nicht mit); die Karte
  blendet am Umriss aus (`rand_aus`: wo man sie fast von der Kante sieht – die hintere Braue ragte schräg über die Stirn). Winkel und Dunkelheit kommen vorläufig aus den freien Vorschlagsgenen
  `zug_11` und `zug_12` (erblich, σ 32; die Züge nutzen nur `zug_01`–`zug_10`).
- **Augenpartie neu geeicht** (Hebel 2): Ziel je σ = 1,3 × natürliche Streuung statt 0,75 × für Lidspalte (Höhe, mit 1,2 mm
  aus der Literatur; enger höchstens 1,3 mm/σ), Augengröße, Schlupflid, Augenneigung, Brauenstand; Augenabstand 1,0 ×;
  Stärke dort bis 1,0 je σ statt 0,6. Nase, Kinn, Kiefer, Umriss und Augentiefe bleiben (Augentiefe × 1,3 in Runde 1–2
  machte tief liegende Augen zur Fratze).

| Gen (mm je σ) | E0 | E1 |
|---|---|---|
| Lidspalte (Höhe), weiter / enger | 0,63 | 1,56 / 1,30 |
| Augengröße (Lidspaltenbreite) | 1,05 | 1,82 |
| Augenneigung (Außenwinkel) | 0,60 | 1,04 |
| Brauenstand (Haut, ohne Bild) | 1,00 | 1,73 |
| Brauenstand gesamt mit Bild | ≈ −0,5 (Vorzeichenfehler) | 3,0–4,2 (nach oben weich begrenzt) |
| Schlupflid | 0,42–0,58 | 0,70–0,96 (Ziel 1,39; die Formen geben nicht mehr her) |
| Augenabstand (Mann / Frau) | 2,70 / 2,55 | 3,60 / 3,40 |

- **Budget** (Modus `gen`): begrenzt wird nur der Gen-Anteil je MakeHuman-Ziel (±2,0), die Summe mit den Reifeformen auf
  [−1,6; 2,4]; an den Nasenzielen zählt die Summe wie bisher (sonst stapelt sich `nose-point-down` aus Reife, Länge und
  Spitze). Bevölkerungsrechnung (2000 Personen, z ≤ 2): mit der neuen Eichung kämen unter dem alten Budget Brauenstand−
  (0,88), Lidspalte−, Schlupflid+ nur gekürzt an; mit dem neuen alle voll. Bei der Frau leben Wangenknochen schmaler
  (0,32 → 1,0) und Gesicht schmaler (0,67 → 1,0) wieder auf; Nasenlänge+ (0,8) und Nasenspitze− (0,77) bleiben gekürzt
  (gewollt).
- **Nasenbudget**: √(Σ z²) über Nasenlänge, Spitze und Höcker höchstens 2,2, sonst die drei gemeinsam kleiner; greift bei
  66 von 600 Personen (11 %; mit 2,0 wären es 19 %). Dazu ein Stupsnasen-Deckel: Spitze hoch und Nase kurz zusammen
  höchstens 1,6σ (Schweinsnase von vorn), greift ebenfalls bei 11 %.
- **Oberlid**: Ruhelage 0,22 → 0,18, Alterszuschlag 0,06 → 0,03 (Etappe D: Mann 70 „halb zu“).
- **Nasenloch der Frau**: feste Zugabe 0,5 × `gen_nasenspitze_minus` (× Reife oben) – Spitze und Steg etwas tiefer
  (Testbogen 0 / 0,25 / 0,5 / Länge+ 0,3: bei 0,5 von vorn fast verdeckt, 0,25 zu wenig). `nose-nostrils-angle-down` und
  `nose-base-down` fehlen im Probebau (bräuchten einen Blender-Lauf).
- **Netz allein** (Wahrnehmungsskript `konfig.py`, 60 Männer, ohne Shader und Budget): Braue über dem Auge σ 0,97 →
  1,48 mm, Lidregion gegen den Augapfel 0,72 → 0,97 mm, Brauenkarte ohne Lage 0,87 → 1,30 mm (Stand Runde 1); dazu kommt
  jetzt die Bildverschiebung der Braue (2,5 mm/σ).
- **Lid–Augapfel** (`Netzmesser._lid`, an allen 16 X-Köpfen; das Maß ist schon am Grundgesicht negativ, gewertet wird die
  Verschlechterung, Grenze 0,6 mm wie Etappe B): Endstand höchstens −0,44 mm (Mann 9793, ein Nasen- und Augenextrem), sonst
  −0,37 … +0,68 mm (Runde 1: −0,55 mm).

### Prüfung E1

Panel wie E0 (gleiche Saaten und Extremköpfe, neue Mischung je Runde, Schlüssel vorher versiegelt), je Runde drei neue
blinde Prüfer. Runde 1: Brauenstand 2,0 mm/σ, Dicke 0,25/σ, Dunkelheit 20/25 %, Dichte ab 0,62, ohne Ausblenden am Umriss,
Augenabstand und Lidspalte enger wie geeicht, Augentiefe × 1,3, Nasenzugabe 0,4 (Commit `f4f8682a`, Schlüssel
`35a5a87b…779b1b`). Runde 2: wie unten unter „Runde 2“ (`f7792068`, `5ec0d3c2…ae66e8e6b`). Runde 3 = Endstand
(`83c845c3`, `4382a0c0…6ad00e`).

| B → A richtig (von 16) | E0 | E1 Runde 1 | E1 Runde 2 | E1 Runde 3 (Endstand) |
|---|---|---|---|---|
| Männer, Prüfer 1 / 2 / 3 | 2 / 4 / 3 | 7 / 2 / 6 | 3 / 7 / 8 | 3 / 5 / 4 |
| Männer, Mittel | 3,0 | 5,0 | **6,0** | **4,0** |
| Frauen, Prüfer 1 / 2 / 3 | 3 / 2 / 6 | 4 / 4 / 3 | 5 / 6 / 4 | 5 / 4 / 6 |
| Frauen, Mittel | 3,7 | 3,7 | **5,0** | **5,0** |
| Geschwister echt / fremd (Mittel 1–10, Männer; Frauen) | 5,3 / 4,4; 4,7 / 4,6 | 4,6 / 4,5; 4,9 / 4,5 | 5,1 / 4,5; 4,5 / 4,6 | 4,7 / 4,9; 4,5 / 4,5 |
| Fremde „Geschwister“ ≥ 6 (Männer, Frauen) | 20 %, 23 % | 37 %, 17 % | 30 %, 23 % | 37 %, 23 % |
| Note „verschiedene Menschen“ (Männer, Frauen) | 4, 3,3 | 4, 4 | 4, 4,7 | 4, 3,3 |

- **Runde 1**: Alle drei erkannten vor allem Köpfe mit auffälligen Lidern (Männer A11 mit fast geschlossenen Augen: 3 von 3)
  und kräftigen Brauen; „unterschieden fast nur an Brauen, Lidern und Kieferbreite – Nase, Mund, Stirn und Umriss nahezu
  gleich; schräg bleiben nur Brauen und Lider“. Frauenbrauen „ähnlich dünne Striche“. Fratzen „deutlich“: die hintere Braue
  ragt schräg als zerrissener Strich über den Stirnumriss (Männer Saat 9842, alle drei), einmal Schnauze mit Brauenwulst
  (Männer 9801, Frauen 9793). Grenzwertig: Lid „wie im Auge“ (Männer A11 = Saat 9011, alle drei), Brauen in Striche
  zerfallen (niedrige Dichte), die Nasen- und Mundextreme wie in E0, ein heller Fleck zwischen den Brauen (der feste
  Hautschatten der Grundbraue ist schwächer).
- **Runde 2** antwortet darauf: Karte am Umriss ausblenden (`rand_aus`; Randtest: von vorn unverändert, schräg verschwindet
  das überstehende Stück), Dichte nicht unter 0,75, Lidspalte enger höchstens 1,3 mm/σ, Brauen breiter gestreut
  (Stand 2,5 mm/σ, Dicke 0,35/σ, Dunkelheit ±30 %/σ), Augenabstand 1,0 × natürlich, Nasenzugabe 0,5. Im Bild ist der
  Schritt klein (mittlerer Bildunterschied gegen Runde 1 0,2–0,3 Grauwerte je Zelle).
- **Runde 2**: Männer 6,0, Frauen 5,0 von 16 (ein Prüfer 8/16 bei den Männern). Wieder „fast nur Brauen, Lider, Kiefer-
  und Wangenbreite; schräg verschwimmt es“; bei den Frauen half jetzt auch die Nase (Stupsnase oder lange gerade Nase) und
  der Umriss. Die schwebende hintere Braue wird seltener genannt (meist „knapp am Umriss“). Drei Fratzen hat E1 verschärft
  (alle „deutlich“): eine dünn auslaufende, dünn gestellte Braue zerfiel zum gestrichelten Strich (Männer 9842: Dicke 0,51 –
  gestaucht statt schmal), eine kurze und zugleich aufgestülpte Nase zeigt die Nasenlöcher wie eine Schweinsnase (Frauen
  9839; das Nasenbudget griff nicht, Norm nur 1,7) und tief liegende Augen unter dem Brauenwulst (Männer 9722, Augentiefe
  +2,35σ × 1,3). Dazu weiter die Mund- und Profilextreme aus E0 (Frauen 9801 „Schnauze“).
- **Runde 3** antwortet darauf: Brauen nach unten nur 0,15/σ dünner, mindestens 0,75; Stupsnasen-Deckel (Spitze⁺ + Kürze⁻
  ≤ 1,6σ, greift bei 11 %); Augentiefe wieder wie E0.
- **Runde 3 (Endstand)**: Männer 4,0, Frauen 5,0 von 16. Die Prüfer streuen stark (Runde 2 → 3 nur mit
  Fratzen-Korrekturen: Männer 6,0 → 4,0); über alle neun E1-Urteile gemittelt **Männer 5,0, Frauen 4,6** gegen E0 3,0 /
  3,7. Immer erkannt (3 von 3) wurden nur Köpfe mit auffälligen Lidern oder Brauen. Geschwister trennen sich in keiner
  Runde. Fratzen „deutlich“ im Endstand: Männer 9801 schräg (Wangenbeule, Kante am Brauenwulst, Schmollmund; schon in E0
  zweimal „deutlich“), Männer 9722 schräg (Stirnwulst „wie ein Sims“ mit Kerbe an der Nasenwurzel – die tiefer gesetzte
  Braue verstärkt das), Männer 9842 schräg (Braue vor der Stirnkante, zweimal), Frauen 9801 und 9842 schräg (je einmal).
  Gebessert: gestrichelte Brauen nur noch grenzwertig (vorher dreimal „deutlich“), Schweinsnase Frauen 9839 nicht mehr
  „deutlich“; weiter grenzwertig: fast geschlossene Lider (Männer A11), Nasenlöcher der Frau von vorn bei aufgestülpten
  Nasen, die hintere Braue knapp am Umriss.
- **Ergebnis E1:** Wiedererkennung im Mittel aller E1-Urteile +2,0 (Männer) bzw. +0,9 (Frauen) von 16, Endstand +1,0 / +1,3;
  Ziel ≥ 8 **nicht erreicht**;
  Fratzen nicht nur vereinzelt grenzwertig, sondern weiter einige „deutlich“ – teils aus E0 (Mund, Profil), teils durch
  die tiefere Braue im Schrägbild verstärkt.

**Kosten** (T400, 1280 × 720, Stufe Hoch, Halt B | Etappe D | E1 gepaart im selben Prozess, Median aus 5 Wiederholungen,
Commit `f7792068`; T400 ohne fremde GPU-Prozesse, die CPU-Last der NAS lag durch Messläufe anderer Mitglieder bei 15–22 und
wird nur vermerkt; ein erster Lauf unter Last 40–61 war nicht belastbar und ist verworfen; Runde 3 ändert nur Werte auf der
CPU-Seite):

| Lauf | Halt B | D | E1 | E1 gegen D | E1 gegen Halt B |
|---|---|---|---|---|---|
| ohne Haar, Mittelgesicht | 6,50 | 6,84 | 6,92 ms | +0,08 | **+0,44** |
| ohne Haar, Zufallsperson | 6,63 | 7,00 | 7,03 ms | +0,03 | **+0,41** |
| TressFX in Ruhe | 29,54 | 29,58 | 29,58 ms | ±0,00 | **+0,04** |

Budget +1,0 ms gegen Halt B eingehalten; Grafikspeicher gleich (dasselbe Netz, keine neuen Bilder).

### Selbst getroffene Entscheidungen (E1)

- Alles über einen neuen Probekopf (glb6e = glb6d + eigene `regeln6.json`), nicht über die Regeln im Repository: Etappe D
  bleibt bitgleich vergleichbar, und die Eichung bleibt in der Darstellung (Plan: nicht über die Streuung im Erbgut).
- Winkel und Dunkelheit der Brauen vorläufig aus `zug_11` und `zug_12` (erblich, sonst ungenutzt) statt aus dem
  Personen-Samen (der wäre nicht erblich und schwächte das Familiensignal).
- Lidspalte mit 1,2 mm natürlicher Streuung (Literatur) statt der Annahme 0,84 mm; nach Runde 1 enger höchstens 1,3 mm/σ.
- Augenabstand auf 1,0 × (nicht 1,3 ×), Augentiefe wie E0 – beides nach den Fratzen-Befunden.
- Frauenbrauen im Mittel etwas dichter und höher (× 1,3 / × 1,15): gegen „Mittel nicht dunkler“ des Plans abgewogen, weil
  alle drei E0-Prüfer die Frauenbrauen „fast unsichtbar“ nannten; die Farbe im Mittel bleibt wie Etappe D.
- Nasenloch über eine feste Zugabe vorhandener Formen statt eines neuen Probebaus (`nose-nostrils-angle-down` fehlt).
- Drei Panel-Runden statt einer: Runde 2 und 3 antworten nur auf Befunde der Prüfer (Fratzen und Brauen), nicht auf die
  Trefferzahl selbst.

### Offen nach E1

- **Ziel ≥ 8/16 nicht erreicht.** Alle Prüfer aller Runden: schräg tragen nur Brauen und Lider; Nase, Mund, Stirn und
  Umriss „bei allen gleich“. Mehr Augen- und Brauenstreuung allein reicht nicht – nächste Hebel: eigene Lidformen (E2,
  Blender-Backlauf mit MPFB-Zielen Augenhöhe, Epikanthus, Tränensack, Lidfaltenhöhe), mehrere Grundköpfe (E3) und ein
  Leitmerkmal je Person (Hebel 5).
- Geschwister trennen sich im Panel nicht (echte 4,5–5,1 gegen Fremde 4,5–4,6 von 10): bei gleichen Farben und gleicher
  Frisur fehlen die erblichen Merkmale, die Familien im Spiel tragen (Haar, Augenfarbe, Haut).
- Fratzen außerhalb von E1, die alle Runden nennen: vorgeschobener Mund mit fliehendem Kinn („Schnauze“, Lippen und
  Kiefervorstand), sehr lange hängende Nase; die hintere Braue liegt schräg teils noch knapp am Stirnumriss.
- Fratzen, die E1 verstärkt: eine tief gesetzte Braue (Hautform plus Bildverschiebung zusammen bis ≈ 8 mm bei −2σ) macht
  im Schrägbild aus einem kräftigen Brauenwulst einen „Sims“ (Männer 9722); Vorschlag: Verschiebung nach unten wie nach
  oben weich begrenzen (≈ 2 mm) und die Hautform nach unten nicht über 1,0 × eichen. Die Braue an der Stirnkante
  (Männer 9842) braucht ein breiteres Ausblenden am Umriss oder flachere Karten.
- Die Prüfer streuen von Runde zu Runde um ±2 Treffer (gleicher Stand); für kleine Schritte fünf Prüfer je Runde nehmen.
- Ein dunkler Fleck im TressFX-Haar „mann_kurz“ über dem Ohr, in jedem Schrägbild (von den meisten Prüfern aller Runden genannt).
- Gen-Vorschläge an kern (gebündelt über den Teamleiter): `brauen_winkel`, `brauen_dunkel` (Mensch 16/128/240, σ 32,
  Dominanz 50, Mutation 3; andere Völker σ 0); dann `zug_11`/`zug_12` in `kopf6_verschieden.py` ersetzen.
- Die Regeln von glb6e gelten nur für den Probekopf; ins Spiel (Repository-`regeln6.json`) erst nach der Etappe, die den Kopf
  neu baut.

### Testanleitung E0/E1

1. Stichprobe für die Extremköpfe (ohne Grafikkarte): `python3 tools/charakter/boegen6/panel_v.py stichprobe <p.json>`, dann
   `$G --headless --path game res://versuch/charakter/charakter_schau.tscn -- --glb=~/werkzeuge/charakter/glb6d
   --gewichte=<p.json> --aus=<stichprobe.json> --stand=6 --ende`.
2. Probekopf E1: Ordner `~/werkzeuge/charakter/glb6e` mit Verknüpfungen auf alle Dateien von glb6d außer `regeln6.json`, dann
   `python3 tools/charakter/kopf6_verschieden.py --ein=~/werkzeuge/charakter/glb6d/regeln6.json
   --aus=~/werkzeuge/charakter/glb6e/regeln6.json`.
3. Panel: `python3 tools/charakter/boegen6/panel_v.py boegen --kopf=~/werkzeuge/charakter/glb6<d|e>/kopf6.glb --runde=<name>
   --stichprobe=<stichprobe.json> --aus=<bögen> --schluessel=~/werkzeuge/charakter/bilder/phase6/V`; je Bogen
   `GLB=~/werkzeuge/charakter/glb6 tools/charakter/schau.sh <roh> 1280x720 --bogen=<bogen.json> --ende` (das Bogen-PNG der Schau
   ist nur der Fensterausschnitt; die Prüferblätter entstehen aus den Zellen `zz_ss.png`, 4 × 4).
4. Gleichheit glb6d mit neuem Code: Gewichteausdruck wie 1. gegen den Stand vor E1 vergleichen (bitgleich).
5. Messung: `--varianten=haltb:~/werkzeuge/charakter/glb6:0:0:0:0,hd:~/werkzeuge/charakter/glb6d:1:0:1:1,
   he:~/werkzeuge/charakter/glb6e:1:0:1:1 --stufe=hoch --wiederholungen=5 --messen` bei freier T400.
