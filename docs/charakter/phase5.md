# Charakter-Editor, Phase 5: erwachsene Gesichter, Wimpern, Brauen, zehn Frisuren und Bärte (Menschen)

Stand 30.10.2026, Branch `phase5/charakter-werkzeug`, Mitglied darstellung. Kein Spielcode, kein PR. Grundlage:
[Phase 4](phase4.md) (Haar und Bart, TressFX), [Phase 3](phase3.md) (Haut, Brauen, Wimpern, Galerie).

Auftrag (Nutzer 30.10.): nur Menschen; Haarkarten gefallen nicht → **nur TressFX** für Haupthaar und Bart (die Karten
bleiben als technischer Rückfall, ohne neue Arbeit); Kopfbedeckungen später. Gesichter sahen aus wie Puppen und
Kleinkinder, die Wimpern saßen auf dem Lid statt an der Lidkante, Frauen hatten fast keine Brauen. Dazu je Geschlecht
zehn Frisuren und zehn Bärte nach Porträt-Vorlagen der Bild-KI. Nachtrag: Gen für die Haarstruktur (glatt – wellig –
lockig) freigegeben, offenes Haar bleibt für Frauen jeden Alters zulässig.

## Ergebnis in Kürze

- **Erwachsene sehen erwachsen aus.** Ursache des Kindchenschemas: Das ICT-Gesicht ist ein Durchschnittsgesicht
  (Durchschnittsgesichter wirken jung), und die MakeHuman-Makros übertragen nur den *Unterschied* zu „Mann, 25 Jahre“ –
  mit 25–35 Jahren blieb also das ICT-Gesicht fast unverändert: kurzes Untergesicht, kleines fliehendes Kinn, kleine
  Stupsnase, runde Wangen, kein Brauenwulst, große offene Augen ohne Lidfalte, dünner langer Hals. Dazu blasse,
  glatte, an den Wangen gerötete Haut. Neu sind drei **Altersformen** aus MakeHuman-Zielen: `alter_reife_m`,
  `alter_reife_w` (ab 11 Jahren eingeblendet, voll mit 24 bzw. 21) und `alter_greis` (ab 40, voll mit 78), dazu eine
  Ruhelage der Oberlider (sie decken den oberen Irisrand), ein Adamsapfel und eine erwachsene Haut (weniger
  Wangenrot, Poren, Unruhe im Hautton, kräftigerer Bartschatten). Altersverlauf 5/20/35/50/70 ist glaubhaft.
- **Wimpern an der Lidkante:** Sie wuchsen bisher aus der *hinteren* Lidkante (Naht Gesicht/Augenhöhle) und stachen
  2–3 mm über dem Lidrand durch die Haut. Jetzt sitzen die Wurzeln an der **vorderen Lidkante** (dort, wo die
  Wimpernstreifen des ICT-FaceKit die Haut berühren): oben drei unregelmäßige Reihen (vorn lang, 0,3 und 0,6 mm
  tiefer kürzer), 5,5–10,8 mm, nach außen länger, aus der Kante erst nach vorn und dann nach oben und außen gebogen;
  unten eine lichte Reihe 3–6 mm. Die Wimpern hängen an der Haut ihrer Wurzel und drehen beim Blinzeln mit dem Lid.
- **Brauen:** Frauen haben ein eigenes Brauenbild (Kopf 7 mm, Körper 5–6 mm, Bogen bei 60 %, dünnes Ende) ohne
  Beschnitt – vorher schnitten Dichte 0,65 und Breite 0,72 die Brauen fast weg. Beide Geschlechter dichter, Haare im
  Körper als Fischgrät (untere aufwärts, obere abwärts), am Kopf steil, zur Mitte dichter; Brauen dunkler als das
  Haupthaar (bei hellem Haar mehr).
- **TressFX:** innere Strähnen dunkler (Lage über der Haut je Strähne), weiche Licht-Schatten-Grenze um den Kopf statt
  der harten Kante der Schattenkarte, Bärte dicht und deckend (mehr Strähnen, mehr Folgehaare, breitere Fasern, Haut
  darunter dunkel), Pagenkopf weniger glockig. **Haarstruktur** glatt – wellig – lockig legt Wellen bzw. Schrauben-
  locken auf alle Strähnen (vorläufig als Gen `haarstruktur`, Vorschlag an kern).
- **Zehn Frisuren je Geschlecht und zehn Bärte** nach den Vorlagen (siehe unten), über die Gene `frisur` und
  `bartform` wählbar; Galerie schaltet mit F/B durch alle zehn.

## Testanleitung

Einmalig (Fremddaten unter `~/werkzeuge/charakter/`, nicht im Repository; Phase 0–4 haben sie geholt; TressFX-Addon wie
[Phase 4](phase4.md)):

```bash
tools/tressfx_holen.sh            # TressFX (MIT), falls noch nicht geholt
dotnet build Reichsgilde.sln      # HaarRechner.cs (Mischen, Verformen, Haarstruktur) und FreieKamera.cs
tools/charakter/kopf5.sh          # Kopf (≈ 1 min), Haut (≈ 4 min), Haarbilder, 30 Frisuren und Bärte (≈ 60 min) nach glb5
tools/charakter/kopf5.sh nur_kopf # nur Kopf und Haarbilder neu (z. B. nach einer Änderung der Altersformen), Haut bleibt
tools/charakter/kopf5.sh nur_haar nur=topfschnitt,gabelbart faktor=0.4 vorschau=/tmp/haar   # einzelne Stile, Workbench-Bilder
tools/charakter/kopf5.sh nur_haar nur_kopfhaut=1   # nach einzelnen Stilen: Stützpunkte und Kopfhautkarten aller Stile neu
tools/charakter/kopf5.sh nur_haar ohne_karten=1    # nur Leitsträhnen (ohne Haarkarten: 111 statt 570 MB, schneller)
```

**Galerie** (30 Köpfe, freie Kamera, Haar und Bart aus den Genen):

```bash
godot --path game res://versuch/charakter/charakter_galerie.tscn -- --glb=$HOME/werkzeuge/charakter/glb5 --haar=tressfx
# im Godot-Editor: versuch/charakter/charakter_galerie.tscn öffnen und F6 (Standardordner glb5)
```

| Taste | Wirkung |
|---|---|
| rechte Maustaste halten | umsehen (freie Kamera), W A S D fahren, Q/E runter/hoch, Shift schneller, Mausrad Tempo |
| **F** / Umschalt+F | Frisur für alle: aus den Genen, dann Nr. 1–10 vor bzw. zurück |
| **B** / Umschalt+B | Bart für alle: aus den Genen, rasiert, dann die neun Bärte vor bzw. zurück |
| **K** | Haarstruktur für alle: aus den Genen, glatt, wellig, lockig |
| T, N | Haar: TressFX ↔ Haarkarten (Rückfall), Haar an/aus |
| L, V, U, M, F1 | Licht Tag/Innen, Stoffe der Phase 2, Ultra-SSS, Mimik, Hilfe |

**Regler** (ein Porträt, alle Gene, Frisur und Bart fest oder aus den Genen, neuer Regler „Haarstruktur“):
`godot --path game res://versuch/charakter/charakter_schau.tscn -- --glb=$HOME/werkzeuge/charakter/glb5`.

**Bögen und Messung** (ohne Bildschirm, Vulkan auf der T400, eigene `ANZEIGE`):

```bash
G=$PWD/tools/charakter/boegen5
GLB=~/werkzeuge/charakter/glb5 tools/charakter/schau.sh <bilder> 3000x3000 --bogen=$G/gesicht30.json,$G/alter5.json,\
$G/wimpern_nah.json,$G/brauen_frau.json,$G/frisuren10_m.json,$G/frisuren10_w.json,$G/baerte10.json,$G/struktur.json --ende
GLB=~/werkzeuge/charakter/glb5 tools/charakter/schau.sh <bilder> 1280x720 --messen \
  --nur=leer,ohne_haar,haar_tressfx,haar_tressfx_ruhe,haar_tressfx_sparsam,haar_tressfx_frau_offen,x8_ohne_haar,x8_tressfx_ruhe --ende
GLB=~/werkzeuge/charakter/glb5 SZENE=charakter_galerie.tscn tools/charakter/schau.sh <bilder> 1600x900 --fotos --messen
```

Neuer Bogenschlüssel `"formen": {"<Form>": Gewicht}` (Formen obendrauf, z. B. Probeformen) und in `"haar"."vorgabe"`
`"struktur": 0..1`. Probeformen baut `kopf2_bauen.py … proben=<datei.json>` (`{name: {ziel: gewicht}}` → Form
`probe_<name>`); so sind die Altersformen entstanden.

## Was gebaut ist

### Erwachsene Gesichter (`kopf2_regler.json` „alter_formen“, `kopf2_bauen.py`, `charakter_kopf.gd`)

Vorgehen: 54 einzelne MakeHuman-Ziele als Probeformen gebaut und je bei 1,0 angesehen (Mann 30, ¾ und Profil), dann
Mischungen bei 1,0/1,6/2,2 verglichen; die Mischung bei 1,0 wirkt wie 30–40, stärker wird sie zur Karikatur.

| Form | MakeHuman-Ziele (Gewicht) | Wirkung |
|---|---|---|
| `alter_reife_m` | Kinnhöhe 0,9, Unterkiefer tiefer 0,3, Kieferknochen 0,65, Kinn vor 0,6, Kopf eckig 0,3, Wangenvolumen − 0,7, Wangenknochen 0,5, Nase länger 0,7, vor 0,4, voller 0,3, Spitze tiefer 0,5, Brauen vor 0,7 und tiefer 0,4, Augen kleiner 0,4, Lidfalte tiefer 0,5, Stirn zurück 0,4, Lippen dünner 0,4, Halsumfang 0,45, Kopffett − 0,5; dazu ein Adamsapfel (eigen: 3 mm, 5,2 cm unter dem Kinn) | längeres Untergesicht, kantiger Kiefer, Wangenknochen statt Babyspeck, größere gerade Nase, Brauenwulst, tiefer liegende kleinere Augen, kräftiger Hals |
| `alter_reife_w` | Kinnhöhe 0,45, Kinn vor 0,45, Kieferknochen − 0,25, Kinnbreite − 0,3, Kopf oval 0,3, Nase länger 0,6, gerade 0,3, schmaler 0,2, Spitze tiefer 0,3, Wangenvolumen − 0,7, Wangenknochen 0,7, Augen kleiner 0,4, Lidfalte 0,3, Mund breiter 0,25, Oberlippe − 0,25, Unterlippe − 0,15, Kopffett − 0,4, Brauen vor 0,2 | ovales erwachsenes Gesicht, deutliches Kinn, Wangenknochen, kleinere Augen, schmalere Lippen |
| `alter_greis` | MakeHuman „head-age“ 1,0, Wangen tiefer 0,8, Tränensäcke 0,8, Mundwinkel 0,5, Lachfalten 0,6, Doppelkinn 0,3, Lidfalte 0,5, Lippen dünner, Nase länger, Ohren größer 0,5, Schläfen eingefallen 0,5 | Erschlaffen ab 40 |

| Größe (Testszene) | Regel |
|---|---|
| Gewicht `alter_reife_m` | Alter 11 → 0, 15 → 0,3, 19 → 0,75, 24 → 1 (× Geschlecht) |
| Gewicht `alter_reife_w` | Alter 10 → 0, 14 → 0,35, 18 → 0,8, 21 → 1 |
| Gewicht `alter_greis` | 40 → 0, 55 → 0,3, 68 → 0,7, 78 → 1, 90 → 1,15 |
| Ruhelage der Oberlider | `eyeBlink` + 0,1 × Reife + 0,06 × Greis (Mimik obendrauf, höchstens 1) |
| Haut | Wangenrot 0,34 (Kind) → 0,2 Mann / 0,25 Frau (+0,06 im Alter); Poren 0,32 → 0,62 / 0,5; Unruhe im Hautton 0,03 → 0,11 / 0,075 (+0,05); Bartschatten der Männer × 1,45 |

Die Haarfrisuren werden jetzt auf dem erwachsenen Kopf gekämmt (Reifeform voll); Kinder und Greise tragen sie über die
Stützpunkte mit.

### Wimpern (`kopf3_haare.py`, `kopf3_haartexturen.py`, `kopf2_bauen.py`)

Befund (Schnitt durch das obere Lid, ICT-Netz): Die Naht Gesicht/Augenhöhle liegt 3,5 mm hinter und 2 mm über der
vorderen Lidkante; die Wimpernwurzeln lagen 0,5 mm vor dieser Naht, also *im* Lid. Die ICT-Wimpernstreifen dagegen
berühren die Haut genau an der vorderen Kante (19 Wurzeln je Lid). Diese Linie ist jetzt die Wurzellinie.

| Teil | Phase 4 | Phase 5 |
|---|---|---|
| Wurzel | 0,5 mm vor der hinteren Lidkante (im Lid) | vordere Lidkante, 0,1 mm in die Haut; Reihen 2 und 3 je 0,3 mm tiefer in der Kante |
| Oberlid | drei Lagen, 5,8–8,8 mm | drei Reihen: 120 + 70 + 45 Wimpern, Karte 5,5 (innen) – 9,8 – 10,8 (bei 80 %) – 8,5 mm (außen), Reihen 100/84/62 %; einzelne kurze (nachwachsende) |
| Form | aus der Naht nach vorn, dann hoch | aus der Kante nach vorn und eine Spur abwärts, dann nach oben gebogen (kubische Kurve), nach außen geneigt, innen leicht zur Nase |
| Unterlid | 46 Wimpern, 2,6–4,3 mm | 55, 3–6 mm, innen licht |
| Bindung | nächste Ecken der Naht | Haut an der eigenen Wurzel (3 Hautecken); beim Blinzeln Drehung um die Augenmitte |
| Dichte (Shader) | Männer 0,88 | Männer 0,8, Frauen 1 |

### Brauen (`kopf3_haartexturen.py`, `charakter_kopf.gd`)

| | Männer (`brauen.png`) | Frauen (`brauen_w.png`) |
|---|---|---|
| Form | Kopf 5,5–9,2 mm, Körper 8,6 → 6,8 mm, Ende 2 mm, gerade | Kopf 4,2–7 mm, Körper 6,4 → 5 mm, Ende 1 mm, Bogen +1,3 mm bei 60 % |
| Haare | 880 + 260, 0,055–0,095 mm | 720 + 170, 0,045–0,075 mm |
| Dichte (Shader) | 0,7 + 0,3 · Bartwuchs | 0,7 + 0,12 · Bartwuchs + 0,08 · Bartwuchs (Frau) |
| Farbe | Haupthaar × 0,42–1 (bei hellem Haar dunkler) | ebenso |

### TressFX (`haar4_straehne.gdshader`, `charakter_haar.gd`, `HaarRechner.cs`)

- **Tiefe im Haar:** `kopf4_frisuren.py` merkt sich je Strähne die Lagenhöhe über der Haut (0 innen, 1 außen,
  `straehnen_info[3]`); sie steckt als Nachkomma in der Strähnen-UV (`x = (⌊Zufall·63⌋ + Lage)/64`), der Shader
  verschattet innere Strähnen (`innen_dunkel` 0,5, Bart 0,45).
- **Weiche Schattengrenze:** Für Richtungslicht zählt vor allem die Lage der Strähne um die Kopfmitte
  (`kopf_mitte`, weich über −0,35…0,3), die Schattenkarte nur zu 30 %; im Schatten bleiben 35 % Licht (Streuung).
  Vorher zeichnete die Schattenkarte den Kopf als harte Kante ins Haar (Phase 4: „fleckiger Bart“).
- **Bart:** Vollbart 6 000 statt 3 600 Strähnen, langer Bart 5 600, Kinnbart 2 700, Schnurrbart 1 400; 3 statt 2
  Folgehaare, Faser 0,34 statt 0,28 mm, Spitzen weniger durchsichtig; die Haut unter dem Bart nimmt die Bartfarbe ganz an.
- **Pagenkopf:** flachere Lagen, Enden laufen zu Hals und Kiefer ein (jetzt als Pagenkopf mit Stirnfransen nach der Vorlage).
- **Haarstruktur** (`HaarRechner.Struktur`): nach dem Verformen werden die Strähnen neu abgetastet (lockig bis zu
  viermal so viele Punkte, höchstens 64) und bekommen eine Welle (quer zur Strähne) bzw. eine Schraube (Locke); Strähnen
  eines Büschels haben dieselbe Phase, an der Wurzel bleibt das Haar glatt. Ausschlag 0–7,2 mm (wellig 6 mm), Wellenlänge 6,5 → 2 cm,
  Bärte flacher und enger. Gen-Wert 0 glatt, 128 wellig, 255 lockig; bis kern das Gen hat, gilt 60 (leicht wellig).
- `instance uniform lod_bucket` bleibt im Strähnen-Shader: Das Addon setzt es je Teilnetz (vier Netze, auch ohne
  Detailstufe), ohne zeichnet es nur ein Viertel der Strähnen; TressFX läuft nur mit Forward+/Mobile (der Puffer der
  Instanz-Uniforms aus #263 ist dort kein Problem). Ausnahme zur Regel aus #263, nur hier.

### Korrekturen des Nutzers an den Vorlagen (30.10.)

Die Vorlagen sind freigegeben; diese Korrekturen sind direkt im Nachbau umgesetzt (die Bilder bleiben):

| Stil | Korrektur | Umsetzung |
|---|---|---|
| m08 Tonsur | kahle Stelle zu groß, Kranz zu schmal; Corona wie bei Mönchen um 1400 | runde kahle Stelle Ø 9 cm auf dem Scheitel (Mindestmaß war die Größe einer Hostie), breiter Kranz bis zum natürlichen Haaransatz |
| m10 Halbglatze | Glatze hält sich in Grenzen, ¾-Ansicht wirkte komisch | Platte und Geheimratsecken statt kahlem Oberkopf (Schwelle im Rang 0,42–0,6 statt 0,58–0,8), weicherer Rand |
| m03 Haarrolle | das Ohr darf man nicht sehen | Kante auf Höhe des Ohrläppchens, Haar wächst bis ans Ohr, Rolle größer (Halbmesser 1,2–1,6 cm) |
| f04, f05, f06, f08 | maßgeblich ¾- und Rückansicht: nichts hängt herab | hochgesteckte Strähnen biegen nach 0,5–4 cm zum Anker (vorher 2–9 cm), ohne Schwere, Länge höchstens 1,8 × der Abstand zum Anker; Zopfschlaufen: Hinterkopf zum Nackenknoten, vorn zu den Schläfen |
| f08 Crespine | Netz und Frisur sahen damals anders aus | nach Quellen neu: Seitenpolster über Schläfe und Ohr statt Knoten am Hinterkopf, Goldnetz mit Perlen, Fillet (siehe Quellen) |
| b05 Gabelbart | Spitzen länger | Spitzen bis 15 cm unter das Kinn |
| b08 Schnurrbart | ¾-Ansicht nicht ganz richtig, hinnehmbar | unverändert |

### Zehn Frisuren je Geschlecht und zehn Bärte (`kopf5_frisuren.py`)

Vorbild: Porträt-Vorlagen der Bild-KI (`~/werkzeuge/charakter/vorlagen-haar/<nr>_<name>/vorn.png` und `liste.md`, lokal,
nur angesehen). Die Werkzeuge (Kämmen, Wellen und Klumpen, Zöpfe, Knoten) sind die der Phase 4 (`kopf4_frisuren.py`),
neu ist eine Nachbearbeitung je Strähne (`nach`: Haarrolle, Locken), Zöpfe entlang beliebiger Bahnen über den Kopf
(`bahn_auf_kopf`) und Zusatzteile je Stil (`zusatz`: Zöpfe, Knoten, Schnecken). Reihenfolge = Wert der Gene
(`frisur` × 10 ÷ 256, `bartform` × 10 ÷ 256; Bart 0 = rasiert).

| Nr. | Stil (Name) | Bau |
|---|---|---|
| m01 | Topfschnitt (`topfschnitt`) | vom Wirbel strahlenförmig, scharfe waagrechte Kante 2–3 cm über den Ohrspitzen, kurze Fransen auf der oberen Stirn; darunter (Schläfen, Seiten, Hinterkopf, Nacken) rasiert, Ohren frei |
| m02 | Pagenkopf (`mann_pagen`) | wie m01, Fransen bis an die Brauen, seitlich bis zum Kiefer, hinten Nacken; Enden laufen ein |
| m03 | Haarrolle (`haarrolle`) | Mittelscheitel, oben anliegend, bis zum Ohrläppchen; jedes Ende ¾ Kreis (Halbmesser 1,2–1,6 cm) nach außen zur Rolle eingedreht; das Ohr liegt hinter bzw. in der Rolle |
| m04 | schulterlang (`schulterlang`) | Mittelscheitel, leicht gewellt, bis auf die Schultern |
| m05 | kurz geschoren (`mann_kurz`) | etwa 5 mm (4–9 mm), 8 000 Strähnen zu 4 Punkten, Ohren frei |
| m06 | Stirnfransen (`stirnfransen`) | 4–6 cm, etwas zerzaust, Fransen nach vorn in die Stirn |
| m07 | Krauskopf (`krauskopf`) | kinnlang (12 cm), viel Volumen (Lagen 0,8–3,8 cm), Korkenzieherlocken Ø etwa 1 cm (Wellenlänge 2,2 cm) |
| m08 | Tonsur (`tonsur`) | Corona: runde kahle Stelle Ø etwa 9 cm auf dem Scheitel (etwas hinter der Mitte), der breite Kranz darum 2–3 cm, vom Scheitel weg gekämmt, gerade geschnitten, Ohren frei |
| m09 | zerzaust (`zerzaust`) | halblang bis Kinn und Nacken, gestuft, kraus, Fransen |
| m10 | Halbglatze mit Kranz (`kranz`) | Platte und Geheimratsecken kahl (Rang < 0,42–0,6, in Grenzen), Kranz an Seiten und hinten, glatt bis Kinnhöhe |
| f01 | zwei Zöpfe (`frau_zoepfe`) | wie Phase 4, Zöpfe 44 cm vor den Schultern bis zur Taille |
| f02 | ein Zopf (`ein_zopf`) | glatt nach hinten zum Nacken, ein Zopf 46 cm über den Rücken |
| f03 | offen (`frau_offen`) | wie Phase 4 |
| f04 | Flechtkranz (`flechtkranz`) | glatt zu Ankern hinter den Ohren; zwei Zöpfe als Krone über den Kopf, 1,2 und 4 cm hinter dem Scheitel |
| f05 | Schnecken (`schnecken`) | glatt zu den Ohren; je Seite eine flache Spirale (etwa 8–9 cm) über dem Ohr |
| f06 | Zopfschlaufen (`zopfschlaufen`) | je Seite ein Zopf von der Schläfe vor dem Ohr bis Kieferhöhe, dann nach hinten zu einem flachen Knoten im Nacken |
| f07 | Nackenknoten (`frau_knoten`) | wie Phase 4 |
| f08 | Crespine (`haarnetz`) | Mittelscheitel; das Haar an jeder Seite über Schläfe und Ohr zu einem Polster (Ø 5,4 cm, 8,5 cm hoch) gelegt; darüber ein Rautennetz aus Goldschnur (Maschen etwa 1 cm) mit Perlen an den Kreuzungen (je drei kleine Schlaufen); ein schmales Goldband (Fillet, acht Schnüre) über den Scheitel. Netz und Perlen sind eigene TressFX-Teile (`haarnetz_netz`, `haarnetz_perlen`) mit fester Farbe |
| f09 | hohe Stirn (`hohe_stirn`) | Haaransatz vorn 3,5 cm höher (ausgezupft), ohne Scheitel straff zurück zu einem Knoten am oberen Hinterkopf |
| f10 | krause Locken (`krause_locken`) | lang bis unter die Schultern, Volumen, Schraubenlocken (Wellenlänge 4,5 cm, 64 Punkte je Strähne) |
| b01 | rasiert | keine Strähnen (Bartschatten der Haut) |
| b02 | Stoppeln (`stoppeln`) | 9 000 Strähnen, 2–3 mm (kürzere verwirft das Kämmen), wo ein Vollbart wächst; feine Fasern (0,11 mm), ein Folgehaar |
| b03 | kurzer Vollbart (`vollbart`) | etwa 1 cm (Kinn 1,3 cm), dicht, Schnurrbart verbunden; saubere Linie 1,2 cm unter dem Kinn, Hals rasiert |
| b04 | langer Vollbart (`langbart`) | bis zur Brust (Kinn 25 cm), voller Schnurrbart, gerade, leicht wellig, unten schmaler; 32 Punkte je Strähne |
| b05 | Gabelbart (`gabelbart`) | unter dem Kinn gescheitelt, zwei Spitzen 7 cm auseinander, bis 15 cm unter das Kinn; 32 Punkte je Strähne |
| b06 | Kinnbart (`spitzbart`) | nur am Kinn, schmal, 3,5–5 cm, spitz |
| b07 | Schnurr- und Kinnbart (`kinnbart`) | schmaler Schnurrbart über der Lippe, kleines Büschel unter der Unterlippe |
| b08 | Schnurrbart (`schnurrbart`) | lang, die Enden hängen an den Mundwinkeln bis unter das Kinn |
| b09 | Backenbart (`backenbart`) | 2 cm, Streifen von 2 cm über dem Kieferrand von Ohr zu Ohr und unter dem Kinn |
| b10 | wilder Vollbart (`wildbart`) | etwa 12 cm, kraus, viel Volumen, ungleiche Kontur |

## Kosten (T400)

Ein Porträt 1280 × 720, Bühne wie Phase 3/4 (Schatten, SSAO, MSAA 4×), Pagenkopf und Vollbart (Frau: offen), je 6 s.
Andere Mitglieder rechneten auf der Karte mit (leere Bühne 3,1 statt 1,5 ms in Phase 4); die Werte schwanken 10–20 %.

| Lauf | Phase 4 (GPU ms) | Phase 5 (GPU ms) | Dreiecke im Bild |
|---|---|---|---|
| leere Bühne | 1,4–1,5 | 3,1 | – |
| Kopf ohne Haar | 6,0–6,4 | 9,4 | 112 000 (Kopf 55 884, +1 328 für Wimpern) |
| **TressFX** (Pagenkopf + Vollbart) | 29,5–32,6 | **40,3** | 927 000 |
| TressFX in Ruhe | 24,2–27,7 | 35,4 | 723 000 |
| TressFX sparsam | 24,3 | 29,2 | 723 000 |
| Frau offen, TressFX | 39,2 | 47,5 | 1 143 000 |
| 8 Porträts: ohne Haar / TressFX Ruhe | 18,9–30,7 / 108–147 | 19,5 / 199 | bis 5,8 Mio. |
| **Galerie 30 Köpfe**, 1600 × 900: ohne Haar / Karten / TressFX Ruhe | 22,6 / 64,0 / 76,0 | **27,5 / 80,1 / 105,2** | |

Die zehn Stile (zweiter Lauf, weniger Last: leere Bühne 2,2 ms, Kopf ohne Haar 6,1 ms):

| Lauf (1280 × 720) | GPU ms | Dreiecke im Bild | `.tfx` schreiben |
|---|---|---|---|
| Pagenkopf + kurzer Vollbart | 35,5 | 1,0 Mio. | 280 ms |
| kurz geschoren + Stoppeln | 13,8 | 0,54 Mio. | 165 ms |
| Krauskopf + wilder Vollbart | 57,8 | 1,8 Mio. | 390 ms |
| Frau, krause Locken | 71,4 | 2,0 Mio. | 395 ms |
| Pagenkopf + Vollbart, Haarstruktur lockig (viermal so viele Punkte) | 66,8 | 4,0 Mio. | 770 ms |

- Mehr kosten vor allem die dichteren Bärte (Vollbart 6 000 Strähnen × 4) und die Haarstruktur (feinere Abtastung bei
  Locken). Für ein großes Porträt ab Grafikstufe Hoch bleibt TressFX tragbar (Porträtfenster kleiner als 1280 × 720);
  für viele Köpfe gleichzeitig nicht (Galerie 105 ms, 2,1 GB Grafikspeicher).
- Rechnen beim Ändern: Stützpunkte mischen 5–14 ms, `.tfx` schreiben 160–260 ms je Kopf (mit Haarstruktur; ohne
  50–80 ms); Galerie auf TressFX umschalten 4,5 s für 30 Köpfe.
- Kopf: `kopf5.glb` 33,1 MB (Phase 4: 31,9), 133 Formen (+3 Altersformen), Bilder 9 statt 8 (`brauen_w.png`).

## Prüfung (T400, Vulkan über die NVIDIA-ICD, `schau.sh`, Anzeige :98)

Bilder (nicht im Repository): Scratchpad `…/scratchpad/charakter5/` – `vorher/` (Stand Phase 4, dieselben Bögen),
`ende/` (Endstand), `gesicht30_vorher_nachher.png`, `wimpern_brauen_vorher_nachher.png`, `proben/` (Probeformen und
Mischungen), `messung1/`, `messung2/`, `galerie/`, `vergleich_m/w/b.png` (Vorlage | TressFX vorn | ¾).

| Bogen | Bewertung |
|---|---|
| `gesicht30` vorher/nachher (Mann und Frau 30, vorn, ¾, Profil) | vorher Kind von etwa 12–15 Jahren (rundes Gesicht, fliehendes Kinn, Stupsnase, Puppenblick), nachher Erwachsene: längeres Untergesicht, Kinn, Kiefer, Nase, Brauenwulst (Mann), Hals; Oberlid deckt den Irisrand. Frauen erwachsen, aber weicher. |
| `alter5` (5, 20, 35, 50, 70) | Kind bleibt Kind, 20 junger Erwachsener, 35 und 50 erwachsen, 70 mit hängenden Wangen, längerer Nase, Tränensäcken, Falten. |
| `augen_nah`, `wimpern_nah` | Wimpern wachsen aus der Lidkante (vorher 2–3 mm darüber aus dem Lid), dunkle dichte Wurzellinie, nach oben gebogen, außen länger; halb und ganz geschlossen liegen sie am Lid, nichts sticht durch. |
| `brauen_frau` (25–65 Jahre, braun, mittel, blond, rot, ergraut) | Frauenbrauen klar sichtbar, schmaler und gebogen; blonde Brauen dunkler als das Haar; Ergrauen Haar für Haar. |
| `frisuren10_m`, `frisuren10_w`, `baerte10` (vorn, ¾, Profil, hinten bzw. Mund offen) | alle 30 Stile gut zu unterscheiden und nah an den Vorlagen (Vergleich `vergleich_*.png`); Bärte gehen mit dem offenen Mund mit. |
| `struktur` (glatt, wellig, lockig) | Wellen und Locken auf Haar und Bart; „lockig“ eher kraus als Korkenzieher. |
| `portraet` (großes Porträt, 760 × 860) | Mann mit Topfschnitt und kurzem Vollbart, Frau mit Flechtkranz, Greis mit Halbglatze und grauem Bart. |
| Galerie Tag, TressFX | 30 Köpfe mit Haar aus den Genen, ohne Fehler. |

Keine SCRIPT- oder SHADER-ERROR in den Endläufen (nur die bekannten Meldungen von Xvfb). Tests: 981 bestanden.

## Schwächen

- **Gesichter:** erwachsen, aber noch nicht auf Fotostand: die Haut bleibt hell und gleichmäßig (ein Hautbild für alle,
  MakeHuman-Haut einer jungen Frau, weich), Frauen wirken jünger als die Männer, die Mimik-Falten sind Höhen statt
  Geometrie. Ein eigenes, scharfes Hautbild je Geschlecht und Alter wäre der nächste Schritt (z. B. die MakeHuman-Häute
  „middleage_caucasian_male/female“, CC0, prüfen).
- **Altersformen** wirken auf jeden Kopf gleich stark; mit extremen Gestalt-Genen (Kinn, Kiefer) können sie sich
  aufaddieren. Die Kurven stehen in der Darstellung, nicht im Erbgut.
- **Wimpern:** Karten (keine Einzelhaare); im Profil schmal, die unteren etwas zu regelmäßig.
- **Haar:** Strähnen ohne Kollider und fast starr (wie Phase 4); Zöpfe, Knoten und Schnecken sind Strähnen um eine
  Bahn bzw. Spirale (kein echtes Flechtmuster aus der Nähe); das Haarnetz (f08) fehlt (Kopfputz, später); Zopfschlaufen
  (f06) sind die Schlaufen-Fassung der Vorlage, keine steifen Säulen; Halbglatze mit sichtbarem Rand oben.
- **Haarstruktur „lockig“** wirkt kraus statt in Korkenzieher-Büscheln und vervierfacht die Punkte (teuer).
- **Kosten:** TressFX 30–70 ms je großem Porträt auf der T400 bei 1280 × 720 (Locken am teuersten), Galerie 105 ms;
  nur für ein großes Porträt ab Grafikstufe Hoch geeignet.
- **Daten:** 570 MB Haardaten für 29 Stile, davon 457 MB Haarkarten, die nur noch Rückfall sind (`ohne_karten=1`
  baut ohne). Vor einem Import kürzen (Stützpunkte nur mit wirksamen Formen, float16).
- **Messwerte** auf der geteilten T400 schwanken (leere Bühne 2,2–3,1 ms).

## Quellen und Lizenzen

| Quelle | Lizenz | Verwendet für |
|---|---|---|
| MakeHuman-Formziele über MPFB 2.0.18 (head-age, chin, cheek, nose, eyes, eyebrows, forehead, mouth, neck, ears) | CC0 1.0 | Altersformen (gemischt, `kopf2_regler.json`) |
| ICT-FaceKit Light (Wimpernstreifen, nur deren Wurzellinie) | MIT | Lage der Wimpernwurzeln |
| TressFX for Godot 0.3.0 (Addon), Shader-Abwandlung | MIT | wie Phase 4 |
| Porträt-Vorlagen der Bild-KI (lokal, `~/werkzeuge/charakter/vorlagen-haar/`, `liste.md` mit Quellen) | nur angesehen | Vorbild der Frisuren und Bärte, nichts übernommen |
| eigene Rechenregeln (Formen, Wimpern, Brauen, Kämmen, Haarstruktur, Shader) | Code MIT; Bilder und Haardaten „Alle Rechte vorbehalten“ | – |

**Quellen zu den Korrekturen** (nur gelesen):

- Crespine/Caul: Wikipedia „Caul (headgear)“ (https://en.wikipedia.org/wiki/Caul_(headgear): Netz aus Gold-, Silber- oder
  Seidenschnur, Steine an den Kreuzungen, mit Reif oder Fillet gehalten); Fashion-Era „Medieval headdress“
  (https://fashion-era.com/hats-hair/medieval-headdress: unter Heinrich IV. die seitlichen Haarpolster in Netzbeuteln aus
  Goldfaden mit Perlen oder Steinen an den Kreuzungen); wehd.com „Crepine“ (https://wehd.com/21/Crepine.html: Haar in
  zwei halbkugeligen Hüllen zu beiden Seiten des Kopfs unter einem Netz aus Gold- oder Silberschnur mit Perlen);
  bloshka.info „Crespine“ (https://bloshka.info/2020/09/26/crespine/: Haar in zwei Partien an den Seiten, schmales
  Fillet); Ruth Johnston, „13th to 15th centuries: ladies’ hair and hats“ (https://blog.ruthjohnston.com/?p=317).
- Tonsur: Wikipedia „Tonsure“ (https://en.wikipedia.org/wiki/Tonsure: Corona = kahle Stelle am Scheitel mit Haarkranz;
  nicht kleiner als eine Hostie, Größe schwankte).

Kein HG-Material, keine neuen Fremddaten. **Asset-Protokoll:** nichts Neues unter `game/assets/` (Kopf, Bilder und
Haardaten liegen lokal in `~/werkzeuge/charakter/glb5`); Vorschläge für den Import wie [Phase 4](phase4.md), dazu je
eine Zeile für `brauen_w.png` (eigenes Skript) und die 29 Haar- und Bartstile. **Geschützte Dateien** unverändert;
`LICENSE-INHALTE.md` nennt TressFX und (vorbereitet in Phase 2) ICT; die MakeHuman-Ziele sind CC0.

**Gen-Vorschlag an kern** (in `docs/bereiche/kern.md` unter „Offen“): `haarstruktur` (0 glatt, 128 wellig, 255 lockig;
Mensch min 0, mittel 60, max 230, Streuung 45). `frisur` und `bartform` brauchen für je zehn Stile keine neuen Werte.
