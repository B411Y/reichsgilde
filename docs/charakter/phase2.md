# Charakter-Editor, Phase 2: Werkzeugkette Blender → Godot

Stand 29.10.2026, Branch `phase5/charakter-werkzeug` (von `main` abb93ba6), Mitglied darstellung. Kein Spielcode,
kein PR. Grundlage: [Phase 0](phase0.md) und die Entscheidungen des Nutzers danach:

1. **Mischung mit ICT-Gesicht:** Mund, Zähne, Zunge, Augen und Mimik (ARKit-Formen) vom ICT-FaceKit (MIT);
   Büste, Alter (5–70), Geschlecht und Formregler von MakeHuman/MPFB (Assets CC0), per Skript auf das ICT-Gesicht
   übertragen. Der MPFB-Code (GPL) läuft nur lokal als Werkzeug, nichts davon liegt im Repository.
2. **Bildausschnitt:** Kopf mit Schulteransatz und Kragen; Kragen und Hemd sind jetzt schlichte Platzhalter.
3. **Ork und Oger** bekommen eigene Grundformen erst in Phase 5.
4. Der **MIT-Hinweis von ICT** kommt in die Liste der Fremdlizenzen (vorbereitet unten, die Dateien sind geschützt).

## Ergebnis in Kürze

Ein Skript baut aus den Fremddaten in etwa 40 s einen Basiskopf als Büste: ICT-Gesicht mit Augen, Tränenfilm,
Zähnen, Zahnfleisch und Zunge, an eine MakeHuman-Büste genäht, eine UV für die Haut, Brauen und Wimpern als
MakeHuman-Karten, ein Stehkragen als Platzhalter, **131 Formen**: 31 Makroformen (Alter × Geschlecht, Kraft und
Gewicht), 45 Genformen (21 Gestalt-Gene, minus/plus) und 55 Mimikformen (53 ARKit, 2 Pupillen). Die Testszene
zeigt Regler für alle 40 Gene, Alter und Geschlecht und schaltet die Mimik durch.

**Augen, Zähne und Zunge folgen** jetzt allen Formen (in Phase 0 das Problem): Augäpfel über das MPFB-Hilfsauge,
Zähne über die MPFB-Hilfszähne, der Lidrand wird an den Augapfel angeglichen. Die **Mundhöhle ist dunkel** (Tiefe
hinter den Lippen als Eckfarbe, eigene Shader). Die **Mimik der Kinder** wird mit der Gesichtsgröße skaliert.

Ehrlich: Die Haut hat noch keine Textur (Grundfarbe aus den Genen, Masken, Poren) und wirkt hell und glatt; das
Alter zeigt sich in der Form (Kind, Greis), aber ohne Falten; Frauen wirken ohne Haar knabenhaft; die Gestalt-Gene
wirken in MakeHuman-Stärke (sichtbar, aber dezent), die spitzen Ohren reichen für Elben nicht.

## Befehle

```bash
tools/charakter/einrichten.sh            # einmalig: MPFB 2 und die Asset-Pakete (Phase 0)
tools/charakter/kopf2.sh [vorschau]      # Basiskopf nach ~/werkzeuge/charakter/glb2 (≈ 40 s, Blender, 4 Threads)
tools/charakter/pruefen.sh               # Syntaxprobe der Testszene ohne Grafikkarte
tools/charakter/schau.sh <bilder> 1600x1000                          # Regler zum Ausprobieren
B=tools/charakter/boegen2
tools/charakter/schau.sh <bilder> 2000x2300 --bogen=$B/alter_mann.json,$B/alter_frau.json,$B/nah_mund.json,\
$B/nah_augen.json,$B/gene.json,$B/normalen.json --regler_bild --messen --ende       # Bilder und Messung (Vulkan)
```

`kopf2.sh` baut einmal mit Normalen der Formen (`kopf2_mn.glb`) und schreibt daraus ohne Blender `kopf2.glb`
(Normalen nur bei der Mimik, Standard) und `kopf2_ohne.glb`; je .glb eine Beschreibung `<name>.json` mit Reglern,
Makro-Stützen, Mimikmaß, Merkpunkten für die Kamera und dem Bericht des Laufs. Die Testszene lädt die .glb zur
Laufzeit aus `~/werkzeuge/charakter/glb2` (fehlt der Ordner, sagt sie es). Fremddaten liegen nur unter
`~/werkzeuge/charakter/` (wie in Phase 0).

## Dateien

| Datei | Aufgabe |
|---|---|
| `tools/charakter/kopf2.sh` | Ablauf: bauen, Normalen der Formen ausdünnen, Beschreibungen schreiben |
| `tools/charakter/kopf2_bauen.py` | der Bau in Blender (Schritte unten), Option `vorschau=<ordner>` (Workbench-Bilder), `pruefen=1` (Proben der Bindung und Naht), `glaetten=<n>` |
| `tools/charakter/kopf2_quellen.py` | ICT laden (eigenes OBJ-Lesen, Reihenfolge der Ecken bleibt), MPFB-Grundnetz und Formziele als Versatz-Felder, Makros wie MPFB zusammensetzen, Brauen/Wimpern anpassen lassen |
| `tools/charakter/kopf2_passung.py` | Merkpunkte, Ähnlichkeit, TPS, Anpassung an die MPFB-Haut |
| `tools/charakter/kopf2_werkzeug.py` | Rechenbausteine (Kugel, Umeyama, affine Abbildung, Thin-Plate-Spline, Glätten über Kanten, Bindung an Dreiecke, k nächste Nachbarn) |
| `tools/charakter/kopf2_regler.json` | Zuordnung der 40 Gene zu Formzielen (Faktoren zum Nachstellen) |
| `tools/charakter/glb_formnormalen.py` | Normalen einzelner Formen aus einer .glb entfernen und neu packen (nur Standardbibliothek) |
| `tools/charakter/kopf2_vorschau.py` | schnelle Workbench-Bilder aus Blender (ohne Godot) |
| `tools/charakter/boegen2/*.json` | Bildbögen der Phase 2 |
| `tools/charakter/kopf_gemeinsam.py`, `blender.sh`, `einrichten.*`, `schau.sh`, `pruefen.sh`, `warte_last.py`, `glb_info.py` | aus Phase 0 (Blender jetzt mit 4 Threads, Standardordner `glb2`) |
| `game/versuch/charakter/charakter_kopf.gd` | Kopf laden, Stoffe setzen, Gene/Alter/Geschlecht/Mimik in Formgewichte und Farben umrechnen |
| `game/versuch/charakter/charakter_schau.gd` | Regler, Bögen, Messung |
| `game/versuch/charakter/charakter_buehne.gd` | Lichtbühne aus Phase 0 (unverändert bis auf die Ansicht „portraet“) |
| `game/versuch/charakter/haut.gdshader`, `augen.gdshader` | Haut (A2-Stand aus Phase 0) und Auge, unverändert |
| `game/versuch/charakter/mund.gdshader`, `zaehne.gdshader`, `haar.gdshader` | dunkle Mundhöhle, Zähne/Zahnfleisch nach Tiefe, Brauen und Wimpern |

## Ablauf der Werkzeugkette (`kopf2_bauen.py`)

Achsen in Blender: z oben, Gesicht nach −y, +x = linke Seite der Figur; Meter.

1. **Quellen.** ICT-Grundnetz (26 719 Ecken, Zentimeter → Meter) mit 55 Formen (Rauschen unter 0,002 mm auf 0).
   MakeHuman-Grundnetz über MPFB, jedes Formziel als Versatz-Feld; Lage für beliebige Makros wie MPFB
   (`calculate_target_stack_from_macro_info_dict`). Grundlage aller Formen: Mann, 25 Jahre, Kraft und Gewicht
   mittel, Rasse „caucasian“.
2. **Merkpunkte** mit derselben Regel auf beiden Netzen: Augapfelmitten (Kugel durch die Lederhaut bzw. das
   MPFB-Hilfsauge), Mundwinkel (ICT-Landmarke 48/54, MPFB Gruppe „lips“), Nasenspitze, Kinn, Glabella, Scheitel,
   Hinterkopf, Ohren, Hals vorn und hinten.
3. **Ähnlichkeit** (Umeyama, nur Gesichtspunkte): ICT in den Raum der Büste, Maßstab 0,966, Drehung 3,2°
   (Augenabstand ICT 62,3 mm, MPFB 58,7 mm). Diese Lage ist die Form des Spielkopfs.
4. **Anpassung einer Kopie** an die MPFB-Haut (nur für die Zuordnung): Thin-Plate-Spline auf den Merkpunkten,
   dann sechsmal nächster Punkt auf der MPFB-Kopfhaut mit geglättetem Versatz (60 → 4 Schritte). Rest 0,22 mm im
   Mittel, 95 % unter 0,74 mm.
5. **Hals und Büste.** Der ICT-Hals wird auf den letzten 5 cm zur MPFB-Haut gezogen (höchstens 18 mm). Die Büste ist
   die MPFB-Haut unter der Halsschleife: geschnitten wird entlang der Linie 1,2 cm unter der Schleife (Kanten
   geteilt, neue Punkte verbunden – eine Ebene ging nicht, weil der ICT-Hals vorn bis zur Drosselgrube reicht),
   unten 20 cm unter dem Schlüsselbein, seitlich mit dem Ansatz der Oberarme (Kinder füllen das Bild).
   **Naht:** ICT-Halsrand (54 Ecken) und Büstenrand (48) mit `bridge_loops` verbunden, Umlaufsinn wie die Nachbarn.
6. **Eine UV:** ICT-Kachel 1001 (Gesicht) → [0, 0,6] × [0,4, 1], Kachel 1002 (Hinterkopf, Hals, Augen- und
   Mundhöhle) → [0,6, 1] × [0,6, 1], Büste und Naht (Smart-UV) → [0, 1] × [0, 0,38]. Stoffe der Haut: „Haut“,
   „Mund“ (Mundhöhle), „Kleidung“ (unter der Kragenebene).
7. **Teile:** Augen (Lederhaut, Iris, Tränenfilm, Lidschatten-Netz – in der Szene ausgeblendet), Zähne mit
   Zahnfleisch und Zunge (ICT), Brauen `eyebrow001` und Wimpern `eyelashes01` (MakeHuman-Karten, am MPFB-Menschen
   angepasst, über die angepasste Kopie auf die ICT-Haut gesetzt, Wimpernstreifen auf die Lidnaht geschoben),
   **Stehkragen** (Platzhalter): Band aus fünf Ringen entlang der Schnittlinie der Haut mit einer Ebene quer zur
   Halsachse 8 mm über dem MPFB-Halsgelenk, 2–6,5 mm außen, 2,6 cm hoch.
8. **Formen aus MPFB** (Makros und Gene): jede ICT-Hautecke ist an ein MPFB-Dreieck gebunden (baryzentrisch, aus
   der angepassten Kopie, am Hals aus der echten Lage); die Büste trägt den genauen MPFB-Versatz. Die Bindung ist
   auf dem groben MPFB-Netz stückweise linear; ohne Glätten zeigten sich dessen Kanten als Grate. Geglättet wird
   (12 Schritte) nur der Rest nach Abzug einer affinen Abbildung und nicht am Hals – sonst entstanden auf der
   Brust Dellen und an der Naht eine Stufe (Mitteln über Nachbarn ist für Maßstab und Verschiebung nicht neutral).
   **Augäpfel:** Verschiebung und Maßstab des MPFB-Hilfsauges, Augenhöhle von der Lidnaht zum Augapfel übergehend,
   Lidbereich im Mittel an den Augapfel angeglichen (bis 2,9 mm, vor allem bei Kindern und Frauen). **Zähne:** je
   Kiefer eine affine Abbildung der MPFB-Hilfszähne, Zahnfleisch und Mundhöhle nach Höhe und Tiefe gemischt.
   Tränenfilm, Lidschatten, Brauen und Kragen folgen den nächsten Hautecken, die Wimpern der Lidnaht.
   **Makros:** die Verschiebung der Kopfmitte (MPFB `joint-head`) wird abgezogen, damit der Kopf im Bild bleibt.
9. **Mimik** (ICT): Versätze gedreht und skaliert wie das Netz; bei den Wimpern dreht die Wimper mit dem Lidrand um
   die Augenmitte (sonst ständen sie beim geschlossenen Auge nach oben).
10. **Eckfarben:** Hautmasken wie Phase 0 (Rötung, Glanz, Vertiefung; Lippen über die Lippennaht), Mundhöhle
    Blau = Tiefe hinter den Lippen (0–3 cm), Zähne Rot = Tiefe, Augen Irisdaten (Phase 0).
11. **Export:** in Dreiecke geteilt (sonst keine Tangenten für die Poren), Formen unter 0,015 mm auf 0 (dünne
    Formen), glTF mit Formen, je Teil nur die Formen, die es bewegen.

## Formen und Regler

| Gruppe | Formen | Gewicht in der Szene |
|---|---|---|
| Alter × Geschlecht | `makro_<w|m>_<saeugling|kind|jung|alt>` (Grundnetz = Mann jung) | bilinear: Geschlecht 0/1, Alter wie MakeHuman (1 J. = 0, 11 J. = 0,1875, 25 J. = 0,5, 90 J. = 1) zwischen den Stützen |
| Kraft, Gewicht | `makro_<g>_<kind|jung|alt>_<kraft|gewicht>_<minus|plus>` | Gen/255 → MakeHuman 0..1, Abstand von 0,5; unter 11 Jahren gilt „kind“ |
| Gestalt-Gene | `gen_<kennung>_<minus|plus>` | (Wert − 128)/127; spitze Ohren Wert/255; Alterskurven aus `erbgut.json`; × Mimikmaß |
| Mimik | 53 ARKit-Namen, `pupilDilateLeft/Right` | Wert × Mimikmaß |

**Mimikmaß** (Gesichtsgröße gegen Mann jung, aus MPFB-Augen und Lippen): Säugling 0,59/0,60 (Frau/Mann), Kind
0,78/0,88, jung 0,90/1,0, alt 0,91/1,0. Es skaliert Mimik und Gestalt-Gene; die Form selbst wandert mit dem Gesicht,
weil sie je Ecke gilt. Abschaltbar in der Szene (Haken „mit der Gesichtsgröße skalieren“).

Die 40 Gene (`game/daten/erbgut.json`, PR #259):

| Gen | Form bzw. Wirkung |
|---|---|
| groesse, beine | keine Form (Maßstab der ganzen Figur bzw. Beine; in der Büste nicht sichtbar) |
| gewicht, kraft | Makroformen Gewicht und Kraft |
| schultern | `torso/measure-shoulder-dist` |
| kopf | `head/head-scale-horiz/vert/depth` je 0,5 |
| gesicht_laenge | `chin/chin-height` 0,6 + `head/head-scale-vert` 0,4 |
| gesicht_breite | `head/head-scale-horiz` 0,7 |
| stirn_hoehe | `forehead/forehead-scale-vert` 0,6 |
| stirn_neigung | minus `forehead-trans-forward`, plus `forehead-trans-backward` |
| brauenwulst | minus `eyebrows-trans-backward` 0,6, plus `eyebrows-trans-forward` 0,5 |
| augen_abstand | `l/r-eye-trans-in` / `-out` |
| augen_groesse | `l/r-eye-scale` |
| augen_neigung | `l/r-eye-corner1-down` / `-up` (äußerer Winkel) |
| augen_tiefe | eigen: Augenpartie (Maske aus `eye-trans-out`) samt Augapfel 4 mm vor bzw. zurück |
| nase_laenge, nase_breite, nase_hoecker | `nose-scale-vert`, `nose-scale-horiz`, `nose-hump` |
| wangenknochen, wangen_fuelle | `l/r-cheek-bones`, `l/r-cheek-volume` |
| mund_breite, lippen | `mouth-scale-horiz`, `mouth-upper/lowerlip-volume` |
| kiefer_breite, kiefer_vorstand, kinn | `chin-bones`, `chin-prognathism`, `chin-prominent` |
| ohren_groesse, ohren_spitze | `l/r-ear-scale`, `l/r-ear-shape-pointed` (nur plus) |
| hauer | keine Form (Phase 5 mit Ork und Oger) |
| haut_helligkeit, haut_ton | Hautshader: Grundfarbe dunkel → hell, Tönung rosig → grünlich/grau |
| haar_helligkeit, haar_rot | Farbe von Brauen und Wimpern (später Haar und Bart) |
| augen_helligkeit, augen_ton | Irisfarbe (dunkelbraun → bernstein/grün/blau) |
| frisur, bartform, bartwuchs, bartwuchs_frau, glatze | keine Wirkung (kommen mit Haar und Bart) |
| ergrauen | Brauen werden ab 20 + Gen/255 · 50 Jahren über 25 Jahre grau |

**Seiten:** „…Left“ meint die linke Seite der Figur (aus ihrer Sicht), im Bild von vorn erscheint sie rechts.
ICT „_L“, MakeHuman „l-“, Blender und Godot +x meinen dieselbe Seite; `jawLeft` und `mouthLeft` bewegen nach +x.
Das Skript prüft es (Abbruch bei Widerspruch); im Reglerbild hebt `mouthSmileLeft` den rechten Mundwinkel im Bild.

## Prüfung (T400, Vulkan, `TREIBER=nvidia` über `schau.sh`, zwei Läufe)

Lauf 1 (Last 5–7, beim Start höchstens ein anderer Godot-Lauf auf der Karte) zeigte: Haut zu hell, Brauen zu licht, Kinderbüste zu
schmal, ohne Normalen der Formen blähen sich die Wangen nicht sichtbar; mit Normalen bei allen Formen zeigte die
Kinderbrust Dellen (sie kamen vom Glätten, siehe Schritt 8). Lauf 2 (Last 3,5–5, keine anderen GPU-Läufe beim
Start) mit den Verbesserungen; Bilder `~/werkzeuge/charakter/bilder/phase2/lauf2/` (je Zelle ein Einzelbild).

| Bogen | Bewertung |
|---|---|
| `alter_mann.png`, `alter_frau.png` (5, 20, 35, 50, 70 Jahre × neutral, jawOpen, Lächeln, Funnel, Blinzeln) | Kind deutlich Kind (großer runder Kopf, kurzer Hals, kleines Gesicht), Mimik beim Kind kleiner und passend; Erwachsene natürlich, Greis nur in der Form gealtert (keine Falten, Brauen grau). Mund offen: dunkle Höhle, Zähne und Zunge sichtbar; Blinzeln schließt mit den Wimpern. Frauen weicher, aber ohne Haar knabenhaft. Haut hell-rosa und glatt, wirkt noch nach Porzellan. Dunkler runder Fleck vor dem Ohr (siehe offene Punkte). |
| `nah_mund.png` (Mann 5, Mann 35, Frau 70) | Zähne folgen sichtbar: „Zähne zeigen“ zeigt beim Kind kleine Zähne im kleinen Mund, bei Gen „Mund und Kiefer breit“ wandern sie mit. Hinten dunkel, Zunge vorn noch hell. |
| `nah_augen.png` | Augen folgen: Augenabstand/-größe 255 und eng/tief – Lider sitzen auf dem Augapfel, keine weiße Lederhaut quillt; halb zu sauber mit Wimpern. Brauen wirken als Streifen, untere Wimpern wie Punkte. |
| `gene.png` (8 Gene bei 0 und 255) | alle sichtbar, aber dezent; Brauenwulst 255 mit leichter Kante, spitze Ohren zu schwach für Elben, Unterkiefer 255 mit kleiner Kerbe am Mundwinkel. |
| `normalen.png` | Mimik mit Normalen (unten) zeigt die geblähten Wangen und das Lächeln mit Licht und Schatten, ohne Normalen (oben) bleiben sie flach. Kind ohne Normalen der Makros sauber. |
| `regler.png` | Reglerseite mit Beispiel (50 Jahre, Nasenhöcker, Brauenwulst, dunkles Haar, Lächeln links 0,8). |

**Messung** (Lauf 2, gleiche Bühne wie Phase 0, ein Porträt 1280 × 720 bzw. acht à 320 × 360, 8 s nach 2,5 s
Anlauf, alle Werte im selben Prozess nacheinander, Fenster 2000 × 2300):

| Lauf | GPU ms | Dreiecke Kopf / im Bild | Zeichenaufrufe | Puffer | .glb | Laden |
|---|---|---|---|---|---|---|
| leere Bühne | 2,74 | – / 4 | 2 | – | – | – |
| Kopf still | 7,76 | 53 284 / 106 572 | 26 | 66 MB | 30,8 MB | 1,06 s |
| Mimik bewegt (55) | 8,19 | | 26 | 66 MB | | 1,13 s |
| alle 131 Formen bewegt | 5,79 | | 26 | 66 MB | | 2,03 s |
| alle bewegt, `kopf2_ohne.glb` | 6,14 | | 26 | 66 MB | 15,1 MB | 1,03 s |
| alle bewegt, `kopf2_mn.glb` | 4,99 | | 26 | 66 MB | 49,4 MB | 1,59 s |
| 8 Köpfe still | 52,6 | | 201 | 473 MB | | 1,16 s |
| 8 Köpfe, Mimik bewegt | 68,5 | | 201 | 461 MB | | 1,07 s |

- **Dreiecke** 53 284 (Haut 30 328, Zähne 14 504, Augen 7 428, Haare 560, Kragen 464), Budget 60 000 eingehalten.
  Ecken: Haut 15 219, Zähne 7 389, Augen 3 852, Haare 374, Kragen 290.
- **Datei:** `kopf2.glb` 30,8 MB (unter 40 MB), ohne Normalen der Formen 15,1 MB, mit allen 49,4 MB.
- **Laden** zur Laufzeit (GLTFDocument, Szene bauen) 1,0–1,1 s, einmal 2,0 s.
- **Speicher:** je Kopf 66 MB Puffer (167 MB Grafikspeicher laut Godot) – gleich für alle drei Dateien: Godot legt
  je Form Lage, Normale und Tangente ab, ob die .glb Normalen hat oder nicht (wie in Phase 0).
- **GPU-Zeit: nicht belastbar.** Die Werte sinken von Lauf zu Lauf (Kopf still 7,8 ms, später alle Formen bewegt
  5,0–6,1 ms), die Reihenfolge widerspricht dem Aufwand – vermutlich Takt der T400 und das große Fenster. Als
  Richtwert gilt Phase 0 (ein Porträt mit 100 bewegten Formen unter 4 ms). Acht Porträts kosten hier 53–69 ms,
  in Phase 0 14 ms; auch das ist zu prüfen (je Fenster Schattenkarte und SSAO). Wiederholen in Ruhe, 1280 × 720.

## Lizenz und späteres Asset

Die .glb liegt **noch nicht** im Repository. Sie darf später hinein:

- **ICT-FaceKit Light** (Netz, Formen): MIT, © 2020 USC Institute for Creative Technologies – erlaubt Kopieren,
  Ändern, Verbreiten, auch kommerziell; Bedingung: Copyright-Hinweis und Lizenztext in allen Kopien bzw.
  wesentlichen Teilen. Die .glb ist ein wesentlicher Teil → Hinweis muss mit dem Spiel ausgeliefert werden.
- **MakeHuman-Assets** über MPFB (Grundnetz, Makro- und Formziele, `eyebrow001`, `eyelashes01` samt Bildern):
  CC0 1.0 (MPFB `LICENSE.md` Abschnitt C, `LICENSE.ASSETS.md`; Kopf jeder `.mhclo`). Die Ausgabe von MPFB enthält
  laut `LICENSE.md` Abschnitt D keine Programmlogik; der GPL-Code bleibt draußen.
- Eigene Anteile (Skripte: MIT; die erzeugte Form: „Alle Rechte vorbehalten“ nach `LICENSE-INHALTE.md`, soweit
  sie eigene Schöpfung ist).

**Asset-Protokoll** (Vorschlag, eine Zeile je Datei bzw. Gruppe, alphabetisch einsortieren, noch nicht eingetragen):

| Spalte | Wert |
|---|---|
| `pfad_muster` | z. B. `game/assets/modelle/figuren/kopf/kopf2.glb` |
| `anzahl` / `art` | 1 / Modell |
| `herkunft` | Fremdpaket (ICT-FaceKit, MakeHuman-Assets), mit eigenem Skript zusammengesetzt |
| `werkzeug` | `tools/charakter/kopf2.sh` (Blender 5.2.2, MPFB 2.0.18 nur als Werkzeug, `glb_formnormalen.py`) |
| `modell_version` | entfällt (keine KI) |
| `prompt_oder_quelle` | ICT-FaceKit Light (github.com/USC-ICT/ICT-FaceKit, lokaler Abzug Commit da5f95a), MakeHuman-Asset-Pakete über MPFB 2.0.18 (`makehuman_system_assets`: eyebrow001, eyelashes01; Grundnetz hm08, Makro- und Formziele) |
| `datum`, `commit_pr` | Tag und Commit des Einfügens |
| `nachbearbeitung` | ja: `tools/charakter/kopf2_bauen.py` (Passung, Büste, Formen), `glb_formnormalen.py` |
| `bedingungen_werkzeug` | MIT (ICT `LICENSE`), CC0 1.0 (MakeHuman-Assets), MPFB-Code GPL 3 (nur Werkzeug, Ausgabe frei laut `LICENSE.md` D) |
| `lizenz_ergebnis` | „Alle Rechte vorbehalten“ für die eigenen Anteile; enthält ICT-FaceKit (MIT, Hinweis mitliefern) und MakeHuman-Assets (CC0) – Wortlaut mit dem Nutzer klären (das Prüfskript verlangt nach dem Stichtag nur „kein CC BY“) |
| `spieler_sieht` | ja |

**Fremdlizenz-Eintrag (vorbereitet, Dateien sind geschützt):** in `LICENSE-INHALTE.md`, Abschnitt
„Fremdmaterial“, in der Aufzählung ergänzen: „… TressFX (MIT), **ICT-FaceKit Light (MIT, © 2020 USC Institute for
Creative Technologies: Gesicht, Mundhöhle, Zähne, Augen und Mimikformen der Charakterköpfe)** und weitere.“ Dazu
neben der späteren .glb eine `HERKUNFT.md` mit dem vollständigen MIT-Lizenztext von ICT (Quelle: `LICENSE` im
ICT-FaceKit) und dem Hinweis auf die MakeHuman-Assets (CC0). Beide Pfade (`/LICENSE*`, `**/HERKUNFT.md`) gibt nur der
Nutzer frei.

## Offene Punkte

- **Haut-Textur** und Faltenkarten (Alter), Haut je Volk: Backen zwischen den Topologien (Phase 3).
- **Fleck vor dem Ohr** in Godot (in der Blender-Vorschau keine Delle): vermutlich SSAO-Hof des Ohrs bei Radius
  5 cm; mit 2 cm prüfen.
- **Brauen** als Streifen (Alphaschwelle 0,12; 0,35 war zu licht) → 0,2–0,25 versuchen; untere Wimpern wie Punkte.
- **Mundhöhle:** hinten dunkel, die Zunge vorn bleibt hell; obere Schneidezähne bei jawOpen hinter der Oberlippe
  (wie bei ICT angelegt).
- **Gene:** Stärken nachstellen (`kopf2_regler.json`), spitze Ohren für Elben als eigene Form (Phase 5 mit den
  Völkern), Brauenwulst-Kante.
- **Messung** wiederholen (GPU-Zeit, acht Porträts).
- **Speicher:** 66 MB Puffer je Kopf; für Stammbaum und Ratstafel Identität (Makros, Gene) zur Laufzeit in ein
  Netz einbacken und nur die Mimik als Formen behalten.
- Das ICT-Lidschatten-Netz (Eye Occlusion) ist ausgeblendet; ein eigener Shader dafür fehlt.
