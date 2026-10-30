# Charakter-Editor, Phase 6: erwachsene, markante, verschiedene Menschenköpfe mit gemalt-realistischer Haut

Stand 31.10.2026, Branch `phase5/charakter-werkzeug`, Mitglied darstellung. Kein Spielcode, kein PR (Entscheidung E17).
Grundlage: [Phase 5](phase5.md). Plan: vom Nutzer am 30.10.2026 freigegeben (E1–E17 wie empfohlen), vier Etappen
A–D mit Halt. Dieser Bericht wächst mit den Schritten; hier stehen bisher **S0** (Messbasis, Prüfstand, Würfeln über
den Kern) und **S1** (Vergleichsvorlagen der lokalen Bild-KI). Es folgen S2 (Grundfehler, Farbübergabe) und S3
(Licht), dann Halt A.

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
