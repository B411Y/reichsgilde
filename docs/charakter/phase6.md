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
  (4 mm), Jochbein (1,5 mm), Lidfalte Mann/Frau (Rinne 1,25 mm, 7,5/9,5 mm über dem Lidrand; Lidrand 0,0 mm bewegt),
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

