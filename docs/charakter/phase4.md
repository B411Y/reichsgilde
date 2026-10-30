# Charakter-Editor, Phase 4: Haupthaar, Bart, Brauen und Wimpern

Stand 30.10.2026, Branch `phase5/charakter-werkzeug`, Mitglied darstellung. Kein Spielcode, kein PR. Grundlage:
[Phase 3](phase3.md) (Haut, Brauen, Wimpern, Galerie), [Phase 2](phase2.md) (Werkzeugkette, Basiskopf).

Auftrag (Nutzer 30.10.): Die Köpfe wirken wie Schaufensterpuppen, Frauen ohne Haar wie Knaben – Haar und Bart, dazu
realistischere Brauen und Wimpern („alles, was mit Haaren zu tun hat“). Frage: Ist das Addon TressFX sinnvoll?

## Ergebnis in Kürze

- **Haupthaar und Bart für Menschen:** drei Frisuren je Geschlecht (Männer kurz, Pagenkopf, zurückgestrichen; Frauen
  Knoten, offen, Zöpfe) und vier Bärte (Schnurrbart, Kinnbart, Vollbart, langer Bart) nach den 2D-Porträts, als
  vererbbare Merkmale über die Gene `frisur`, `bartform`, `bartwuchs`, `glatze`, `ergrauen`, Haarfarbe; im Alter grau,
  lichter, mit Geheimratsecken und Tonsur. Haar und Bart folgen **jeder Kopfform** (Kind bis Greis, Mann und Frau,
  Gestalt-Gene) und der **Mimik** (Bart geht mit dem Kiefer), weil jeder Haarpunkt an Stützpunkten der Haut hängt.
- **Eine Quelle, zwei Arten:** Die Strähnen werden in Blender per Skript gekämmt (`tools/charakter/kopf4_haar.py`);
  daraus entstehen die Leitsträhnen für **TressFX** (großes Porträt, Editor) und die **Haarkarten** (Rückfall für kleine
  Porträts, Grafikstufe Niedrig/Mittel und wenn TressFX nicht lädt). Beide sehen gleich aus (gleiche Form, gleiches
  Licht), TressFX feiner.
- **Haaransätze:** Wurzeln am Rand lichter (weicher Haaransatz statt Helmkante), die Haut unter dem Haar nimmt die
  Haarfarbe an (Kopfhautkarte je Frisur), unter dem Bart wird die Haut dunkler, wo er wächst; Bartschatten der Phase 3
  bleibt für Rasierte und bei wenig Bartwuchs.
- **Brauen und Wimpern ohne TressFX**, aber feiner: Brauen dünner, lichter, mit Büscheln und Lücken; Wimpern am Oberlid
  in drei geschwungenen Lagen, Büschel mit zusammenlaufenden Spitzen, beim Blinzeln mit dem Lid.
- **Galerie** mit Haar (Taste **T** Karten ↔ TressFX, **N** ohne Haar, **F**/**B** Frisur/Bart für alle), Regler mit
  Haarwahl, fünf neue Bögen und Messläufe mit Haar.

**TressFX: ja – für das große Porträt und den Editor, nicht für viele Köpfe.** Die Strähnen sehen deutlich besser aus
als Karten (feine Spitzen, Glanz entlang der Strähne, schmaler Scheitel, der Bart wirkt echt), das Addon lädt im
Container und in der Galerie mit 30 Köpfen. Es kostet aber auf der T400 bei 1280 × 720 **+18 bis +23 ms** GPU-Zeit gegen
**+5 bis +8 ms** für Karten (Messung unten) und braucht je Kopf 25–50 MB Grafikspeicher. Empfehlung: TressFX im
großen Porträt ab Grafikstufe Hoch (Porträtfenster kleiner als 1280 × 720 – die Kosten wachsen mit den Bildpunkten),
Haarkarten sonst; für viele kleine Fenster Karten ohne Schattenwurf (oder ein gebackenes Bild).

## Testanleitung

Einmalig (Fremddaten unter `~/werkzeuge/charakter/`, nicht im Repository; Phase 0 und 2 haben sie geholt) und das
Addon TressFX (`tools/tressfx_holen.sh`, landet in `game/addons/tressfx/`, nicht im Repository):

```bash
tools/tressfx_holen.sh            # TressFX (MIT) aus dem eigenen Release; ohne Addon gibt es nur Haarkarten
dotnet build Reichsgilde.sln      # auch für HaarRechner.cs (schnelle Schleifen) und FreieKamera.cs
tools/charakter/kopf4.sh          # Kopf (≈ 1 min), Haut (≈ 3 min), Haarbilder, Haar und Bart (≈ 25 min) nach ~/werkzeuge/charakter/glb4
tools/charakter/kopf4.sh nur_haar faktor=0.4 vorschau=/tmp/haar   # nur Haar und Bart, dünner, mit Workbench-Bildern
```

Einmal nach dem Holen des Addons den Editor öffnen (oder `godot --headless --path game --import`), damit die
Rechenshader des Addons eingelesen werden.

**Galerie** (30 Köpfe, Haar und Bart aus den Genen, je Kopf verschieden):

```bash
godot --path game res://versuch/charakter/charakter_galerie.tscn -- --glb=$HOME/werkzeuge/charakter/glb4 [--haar=tressfx]
# im Godot-Editor: versuch/charakter/charakter_galerie.tscn öffnen und F6 (Standardordner glb4)
```

| Taste | Wirkung |
|---|---|
| rechte Maustaste halten | umsehen (freie Kamera), W A S D fahren, Q/E runter/hoch, Shift schneller, Mausrad Tempo |
| **T** | Haar: Haarkarten ↔ TressFX (alle Köpfe; TressFX in der Galerie „in Ruhe“: kurz simuliert, dann eingefroren) |
| **N** | Haar an/aus (vorher/nachher) |
| **F** | Frisur für alle: aus den Genen, dann Nr. 1–3 (Männer kurz, Pagenkopf, zurück; Frauen Knoten, offen, Zöpfe) |
| **B** | Bart für alle: aus den Genen, rasiert, Schnurrbart, Kinnbart, Vollbart, langer Bart |
| L, V, U, M, F1 | wie Phase 3: Licht Tag/Innen, Stoffe der Phase 2, Ultra-SSS, Mimik, Hilfe |

**Regler** (ein großes Porträt, alle Gene, jetzt mit Haar: Wahl Haarkarten/TressFX/ohne, Frisur und Bart fest oder aus
den Genen; die Gene „Frisur“, „Bartform“, „Bartwuchs“, „Glatze“, „Ergrauen“, „Haarfarbe“ wirken jetzt):
`godot --path game res://versuch/charakter/charakter_schau.tscn -- --glb=$HOME/werkzeuge/charakter/glb4`.

**Bögen, Fotos und Messung** (ohne Bildschirm, Vulkan auf der T400, eigene `ANZEIGE`):

```bash
G=$PWD/tools/charakter/boegen4
GLB=~/werkzeuge/charakter/glb4 tools/charakter/schau.sh <bilder> 2000x2800 \
  --bogen=$G/frisuren.json,$G/baerte.json,$G/alter.json,$G/nah_augen.json,$G/vergleich.json --ende
GLB=~/werkzeuge/charakter/glb4 tools/charakter/schau.sh <bilder> 1280x720 --messen \
  --nur=leer,ohne_haar,haar_karten,haar_tressfx,haar_tressfx_ruhe,haar_karten_frau_offen,haar_tressfx_frau_offen,x8_ohne_haar,x8_karten,x8_tressfx_ruhe --ende
GLB=~/werkzeuge/charakter/glb4 SZENE=charakter_galerie.tscn tools/charakter/schau.sh <bilder> 1600x900 --fotos --messen
```

Neue Bogenschlüssel: `"haar": {"modus": "karten"|"tressfx"|"aus", "vorgabe": {"frisur": …, "bart": …}, "ruhe": true}`,
`"warten": <Bilder>` (TressFX braucht etwa 40, bis die Strähnen liegen), Köpfe auch als `"~/…"`.

## Was gebaut ist

### Werkzeugkette (`tools/charakter/kopf4.sh`)

1. **Kopf, Haut, Haarbilder** wie Phase 3 (`kopf2_bauen.py`, `kopf3_backen.py`, `kopf3_haartexturen.py`), Ausgabe
   jetzt `glb4/kopf4*.glb` und `kopf4_tex/`; neu sind nur Brauen und Wimpern (unten). Der Stand der Phase 3 bleibt in
   `glb3` (Vergleich).
2. **Haar und Bart** (`kopf4_haar.py`, Blender wegen BVH und KD-Baum, etwa 25 min auf der geteilten NAS):
   - `kopf4_daten.py`: Hautnetz der Backdaten mit allen 130 Formversätzen der .glb (Zuordnung über die Lage, Fehler
     0,0000 mm), **Stützpunkte** (3 571 Hautecken auf einem Gitter, im unteren Gesicht 5,5 mm, sonst 11 mm), Merkpunkte
     (Augen, Ohren, Scheitel, Wirbel, Schultern), Haaransatz je Richtung um den Kopf (wie `kopf3_backen.py`) und der
     **Rang** jeder Stelle beim Ausfallen (Geheimratsecken und Tonsur zuerst, dann die Platte; der Kranz bleibt).
   - `kopf4_frisuren.py`: Wurzeln nach Fläche × Dichte, ausgedünnt mit einer Poisson-Scheibe, deren Radius mit der
     Dichte wächst (**weicher Haaransatz**: am Rand stehen die Haare lichter statt als Helmkante). Jede Strähne wird von
     der Wurzel aus gekämmt (Schritte von 1,5–5 mm): Kammrichtung des Stils (auf die Haut gelegt), Schwere, eine
     eigene Lagenhöhe über der Haut (innen flach, außen auf den anderen: Volumen), frei hängende Enden folgen nur der
     Schwere (Pagenkopf und Nacken laufen leicht zum Hals ein), Anker für Knoten und Zöpfe, Ende nach Länge oder an der
     Schnittlinie; dann Wellen und Büschel (Spitzen laufen zur Mittelsträhne). Knoten als flache Spirale, Zöpfe als
     Dreistrang-Flechte entlang einer Bahn über die Schulter (Band, lose Spitze).
   - `kopf4_karten.py`: **Haarkarten aus denselben Strähnen**: Büschel (weit auseinanderliegende Wurzeln), je Büschel
     eine gebogene Karte entlang der Mittelsträhne, so breit wie das Büschel, Fläche zum Kopf, Tangente = Haarrichtung;
     zwei Lagen (innen breit und dicht, außen schmal und locker). Kartenbild `haar_karten.png` (2048², acht Spalten:
     vier dichte, zwei lockere Sorten Haupthaar, zwei krause für den Bart), Haar für Haar gezeichnet.
     **Kopfhautkarten** in der Spiel-UV (1024²): Dichte je Frisur und Bart (aus den Wurzeln) und der Rang.
   - Jeder Haar- und Kartenpunkt wird an die vier nächsten Stützpunkte gebunden (Shepard-Gewichte, stetig beim Wechsel
     der Nachbarn) und in die Grundlage (Mann, 25 Jahre) zurückgerechnet; die Frauenfrisuren sind auf dem Frauenkopf
     gekämmt (`makro_w_jung`).

Ergebnis in `~/werkzeuge/charakter/glb4/haar4/` (nicht im Repository): `stuetzen.json/.bin` (Formversätze der
Stützpunkte), je Stil `<stil>.json/.bin` (Strähnen, Bindung, Zufall und Rang je Strähne, Karten) und für Knoten und
Zöpfe `<stil>_zusatz.*`, `haar4.json` (Übersicht, Reihenfolge für die Gene), `haar_karten.png`, `kopfhaut.png`,
`kopfhaut_w.png`, `bart.png`.

### Godot (`game/versuch/charakter/`)

| Datei | Aufgabe |
|---|---|
| `charakter_haar.gd` | lädt die Haardaten (einmal je Datei), baut die Kartennetze (einmal je Stil, alle Köpfe teilen sie), rechnet aus den Formgewichten des Kopfs den Versatz der Stützpunkte (Bild für den Kartenshader) und schreibt für TressFX die verformten Leitsträhnen als `.tfx` (`user://charakter4/`); Gene → Frisur, Bart, Glatze, Länge (`aus_genen`) |
| `HaarRechner.cs` | die beiden Rechenschleifen (Mischen der Stützpunkte, Verformen der Strähnen) in C#; fehlt es, rechnet GDScript dasselbe (etwa hundertmal langsamer) |
| `haar4_karte.gdshader` | Haarkarten: Lage aus den Stützpunkten (Vertex-Shader), Ergrauen und Ausdünnen je Haar, Glatze je Karte nach Rang, Bartlänge, Kajiya-Kay-Glanz, Alpha-to-Coverage |
| `haar4_straehne.gdshader` | TressFX-Strähnen: Shader des Addons (MIT) mit Farbe aus den Genen, Ergrauen je Haar, Glatze nach Rang der Leitsträhne, Ausdünnen, Bartlänge |
| `haut3.gdshaderinc` | neu: Haut unter Haar und Bart nimmt die Haarfarbe an (Kopfhautkarte × Rang × Glatze), matter; Bartansatz dunkler, wo der Bart wächst |
| `charakter_kopf.gd` | hängt das Haar an (wenn `<glb-Ordner>/haar4` da ist), `haar_modus_setzen` (karten/tressfx/aus), `haar_vorgabe` (Frisur/Bart fest), Wimpern-Schwelle |
| `charakter_galerie.gd`, `charakter_schau.gd` | Tasten T, N, F, B; Regler mit Haarwahl; Bogenschlüssel `haar`, `warten`; Messläufe mit Haar |
| `haar3.gdshader` | Brauen und Wimpern: Schwelle 0,3 statt 0,4 (feinere, lichtere Haare über Alpha-to-Coverage) |

### Frisuren und Bärte (Menschen)

Vorbild: die 2D-Porträts `game/assets/oberflaeche/portraets/mensch/*_schlicht.webp` (nur angesehen, nichts übernommen).

| Stil | Vorbild in 2D | Form |
|---|---|---|
| `mann_kurz` | mann_1, mann_2, mann_4 (jung) | 3–5 cm vom Wirbel aus, oben leicht nach vorn, Ohren frei |
| `mann_pagen` | mann_0 (jung) | Mittelscheitel, seitlich über die Ohren bis zum Kiefer, Enden laufen zum Hals ein |
| `mann_zurueck` | mann_3 (jung, mittel) | zurückgestrichen, hinter die Ohren, nackenlang, wellig |
| `frau_knoten` | frau_0, frau_2, frau_4 | Mittelscheitel, glatt über die Ohren zum Knoten im Nacken |
| `frau_offen` | frau_1, frau_3 | Mittelscheitel, offen, leicht gewellt, hinter die Schultern |
| `frau_zoepfe` | (Auftrag: „geflochten“) | Mittelscheitel, zwei Zöpfe hinter den Ohren über die Schultern nach vorn |
| `schnurrbart` | mann_2 | Oberlippe, außen länger und hängend |
| `kinnbart` | – | Schnurrbart und Spitzbart (Spitzen laufen unter dem Kinn zusammen) |
| `vollbart` | mann_1, alt | Wangen bis zur Linie Kieferwinkel – Mundwinkel, Kiefer, Kinn, Oberlippe, unter dem Kinn; gestutzt |
| `langbart` | mann_3 | wie Vollbart, an Kinn und Kiefer lang, läuft vor der Brust spitz zu |

### Gene → Haar (`charakter_haar.gd`, `aus_genen`)

| Gen | Wirkung |
|---|---|
| `frisur` | Wert × 3 ÷ 256 wählt eine der drei Frisuren des Geschlechts |
| `bartform` | Wert × 5 ÷ 256: rasiert, Schnurrbart, Kinnbart, Vollbart, langer Bart (Entscheidung hier: „rasiert“ als fünfte Form, die 2D-Porträts zeigen bartlose Männer) |
| `bartwuchs` (Männer), `bartwuchs_frau` (Frauen) | unter 60: kein Bart (nur Bartschatten der Phase 3); 60–160: Länge 35–100 %, Dichte 55–100 %; ab 160 voll. Bart ab 15 Jahren, voll mit 26 |
| `glatze` (nur Männer) | Ausfall beginnt mit 22 + (1 − Gen/255) · 40 Jahren, über 25 Jahre bis zur Stufe 0,25 + 0,72 · Gen/255 (Rang < Stufe fällt weg: Geheimratsecken, Tonsur, Platte) |
| Alter | lichter ab 45 (bis 30 % der Haare mit 75, Männer mit Glatzenneigung bis 45 %) |
| `ergrauen` | wie Brauen (Phase 3): Anteil grauer Haare, je Haar (Salz und Pfeffer) |
| `haar_helligkeit`, `haar_rot` | Haarfarbe wie Brauen; die Haut unter dem Haar nimmt sie an |

### Brauen und Wimpern (keine TressFX: zu klein, Aufwand ohne Gewinn)

| Teil | Phase 3 | Phase 4 |
|---|---|---|
| Brauen (`kopf3_haartexturen.py`) | 1 100 + 340 Haare, 0,07–0,11 mm, fast deckend: wirkte als gemalter Balken | 820 + 230 Haare, 0,045–0,08 mm, Deckung je Haar 55–95 %, Büschel und Lücken (langsames Rauschen über die Braue), einzelne abstehende Haare bis 1,2 mm außerhalb des Randes; Wuchsrichtung wie Phase 3 (innen steil aufwärts, im Bogen flach, außen seitlich abwärts, Fischgrät) |
| Wimpern (`kopf3_haare.py`, `kopf3_haartexturen.py`) | je Lid ein Streifen, 150 + 48 Wimpern | Oberlid drei Lagen geschwungener Streifen: Hauptlage (120 Wimpern in Büscheln zu 2–4, Spitzen laufen zusammen), lange lockere Lage (+12 % Länge, stärker nach oben geschwungen, 46), kurze Fülllage an der Wurzel (90); Unterlid 46, zur Außenseite dichter. Bild 1024², vier Bänder. Die Lagen folgen beim Blinzeln dem Lid wie bisher |
| Shader (`haar3.gdshader`) | Schwelle 0,4 | Schwelle 0,3 (Brauen) bzw. 0,35 (Wimpern), Kanten über Alpha-to-Coverage (MSAA); feine Haare werden blasser statt zu verschwinden |

### Haarkarten im Einzelnen

- Drei Lagen je Frisur (`kopf4_karten.py`, `LAGEN_HAAR`): innen Büschel zu 14 Strähnen, 6–14 mm breit, dichte
  Kartensorten; Mitte Büschel zu 6, 4–9 mm; außen jede vierte bis zweite Strähne als eigene schmale Karte (2,5–4,5 mm,
  um die Haarrichtung gedreht), lockere Sorten. Bart: innen Büschel zu 8, außen einzelne Strähnen, krause Sorten.
- Kartenbild: Wurzeln je Haar versetzt (weiche Kante an der Kartenwurzel, sonst „Schuppen“), Spitzen verstreut, Deckung
  je Haar 60–95 %.
- Licht wie die TressFX-Strähnen (Kajiya-Diffus und zwei verschobene Glanzbänder), damit Karten und Strähnen gleich hell
  wirken; die vom Licht abgewandte Seite der Frisur etwas dunkler.

## Quellen und Lizenzen

| Quelle | Lizenz | Verwendet für |
|---|---|---|
| TressFX for Godot 0.3.0 (Ismail Ivanov), Algorithmen AMD TressFX 4.1 | MIT (Addon, aus dem eigenen Release `tools/tressfx_holen.sh`, liegt nicht im Repository; Lizenztexte `addons/tressfx/LICENSE*.txt`) | Simulation und Zeichnen der Strähnen; `haar4_straehne.gdshader` ist eine Abwandlung des Addon-Shaders (Hinweis im Kopf der Datei) |
| Eigene Rechenregeln (Kämmen, Karten, Kartenbild, Kopfhautkarten, Stützpunkte, Shader, Skripte) | Code MIT; erzeugte Bilder und Netze „Alle Rechte vorbehalten“ | alles andere |
| Kopf der Phase 3 (ICT MIT, MakeHuman CC0, RoboPoets CC0) | wie [Phase 3](phase3.md) | Haut, auf der das Haar wächst |

Keine neuen Fremddaten. Nicht verwendet: Human Generator und alles aus `versuch/hg-koepfe` (nur die eigene Idee „Strähnen
in `.tfx` schreiben“ und die Startwerte der TressFX-Parameter sind nachgebaut, kein HG-Material), die Demos des Addons
(Kopf CC BY 3.0). Die 2D-Porträts wurden nur angesehen.

**Asset-Protokoll:** Nichts Neues unter `game/assets/` – Haardaten und Bilder entstehen lokal in
`~/werkzeuge/charakter/glb4` (wie Phase 2 und 3), `protokoll.csv` bleibt unverändert. Vorschlag für den Import (nach
Freigabe), je eine Zeile: `haar4/*.bin` (Leitsträhnen, Karten, Stützpunkte; `herkunft` eigenes Skript,
`werkzeug` `tools/charakter/kopf4_haar.py`, Blender 5.2.2; `lizenz_ergebnis` „Alle Rechte vorbehalten“) und
`haar4/*.png` (Kartenbild, Kopfhautkarten; ebenso). Die `.bin` sind etwa 4–25 MB je Stil (siehe Kosten); vor dem
Import als Godot-Ressource (`ArrayMesh`, `PackedFloat32Array`) speichern und kürzen (Stützpunkte nur mit wirksamen Formen).

**Geschützte Dateien** (nicht geändert, Vorschläge): `LICENSE-INHALTE.md` nennt TressFX (MIT) schon. Kommt das Haar ins
Spiel, braucht der Export das Addon mit seinen Lizenztexten (`tools/tressfx_holen.sh` legt `LICENSE-TressFX-Godot.txt`
und `LICENSE-godot-cpp.txt` daneben); in `HERKUNFT.md` neben `haar4_straehne.gdshader` bzw. im Shaderkopf steht die
Herkunft schon. `tools/tressfx*` und `.github/` bleiben unverändert.

## Prüfung (T400, Vulkan über die NVIDIA-ICD, `schau.sh`, Anzeige :96)

Bilder (nicht im Repository): Scratchpad `…/scratchpad/charakter4/` – `ende/` (Bögen), `galerie/` (Galerie-Fotos),
`messung/`, `vorschau5/` (Workbench-Bilder der Strähnen), `augen2/`.

| Bogen | Bewertung |
|---|---|
| `frisuren.png` (6 Frisuren × Karten vorn/¾/hinten, TressFX vorn/¾) | Alle sechs gut zu erkennen und nah an den 2D-Vorbildern; TressFX feiner und weicher, Karten gröber (bei „kurz“ sichtbare Einzelkarten, Scheitel bei Karten breiter). Knoten und Zöpfe sitzen. |
| `baerte.png` (rasiert + 4 Bärte × Karten vorn/¾, Mund offen, TressFX ¾) | Bärte liegen am Gesicht, gehen mit dem offenen Mund mit; TressFX-Bart wirkt am echtesten (wie im Versuch vom 28.10.). |
| `alter.png` (5–75 Jahre, Mann mit wenig/viel Glatzenneigung, Frau, Frau blond) | Bart wächst ab etwa 15, grau ab 50–60 (Salz und Pfeffer), Glatze 210: Geheimratsecken mit 35, Platte ab 50, Kranz bleibt; Glatze 40: kaum lichter. Kinder tragen dieselbe Frisur auf dem kleinen Kopf. |
| `vergleich.png` (Phase 3 / Karten / TressFX × Frau 20, Mann 35, Frau 50, Mann 70, Kind 8) | Der Abstand zur Schaufensterpuppe ist der größte Schritt seit Phase 0: mit Haar wirken Frauen nicht mehr knabenhaft. |
| `nah_augen.png` (Phase 3 gegen Phase 4, nah und ¾, halb geschlossen) | Brauen aus einzelnen Haaren mit Haut dazwischen statt Balken; Wimpern voller und geschwungen, im Profil noch dünn. |
| Galerie Tag, nah und weit, ohne/Karten/TressFX | 30 Köpfe mit verschiedenen Frisuren und Bärten aus den Genen, ohne Fehler. |

Keine SCRIPT- oder SHADER-ERROR in den Endläufen (nur die bekannten Meldungen von Xvfb).

## Kosten (T400)

Ein Porträt 1280 × 720, Bühne wie Phase 3 (Schatten, SSAO, MSAA 4×), Pagenkopf und Vollbart (Frau: offen), je 6 s,
zwei Läufe (andere Mitglieder rechneten auf der Karte mit; die Werte schwanken um 10–20 %, die Reihenfolge blieb):

| Lauf | GPU ms (Lauf 1 / 2) | Dreiecke im Bild |
|---|---|---|
| leere Bühne | 1,52 / 1,42 | – |
| Kopf ohne Haar | 6,36 / 6,02 | 110 000 |
| **Haarkarten** (179 000 Dreiecke Haar) | 18,06 / **14,15** | 468 000 |
| Haarkarten ohne Schattenwurf | – / **11,41** | 289 000 |
| **TressFX** (3 Folgehaare je Leitsträhne) | 32,56 / **29,47** | 711 000 |
| TressFX sparsam (2 Folgehaare) | – / 24,32 | 549 000 |
| TressFX sparsam, eingefroren („Ruhe“) | 27,69 / 24,22 | 549 000 |
| Frau offen, Karten (294 000) / TressFX | 27,99 / 21,16 bzw. 39,22 / 39,20 | 699 000 / 1 142 000 |
| 8 Porträts: ohne Haar / Karten / Karten ohne Schatten / TressFX Ruhe | 18,9–30,7 / 80,7–95,9 / 40,6 / 108–147 | bis 4,4 Mio. |
| **Galerie 30 Köpfe**, 1600 × 900: ohne Haar / Karten / TressFX Ruhe | 22,6 / 64,0 / 76,0 | |

- Die Simulation selbst kostet kaum (eingefroren und sparsam gleich schnell); teuer ist das Zeichnen (Füllrate: jede
  Strähne als Band, durchscheinend). Karten kosten vor allem Füllrate und Schattenkarte (drei Lagen, Alpha-to-Coverage).
- **Speicher:** TressFX 25–50 MB Puffer je Kopf (Galerie: +0,9 GB Puffer, 1,95 GB Grafikspeicher insgesamt – auf der
  4-GB-Karte gerade noch); Karten teilen ein Netz je Stil (wenige MB), je Kopf nur ein kleines Bild der Stützpunkte.
- **Rechnen beim Ändern:** Stützpunkte mischen 3–10 ms (C#), `.tfx` schreiben 50–80 ms je Kopf (C#; GDScript etwa
  hundertmal langsamer); TressFX lädt neu und simuliert kurz. Galerie umschalten auf TressFX 1,9 s für 30 Köpfe.
- **Daten:** `haar4/*.bin` 141 MB für alle Stile (Strähnen, Karten, Stützpunkte, float32), Kartenbild 3 MB,
  Kopfhautkarten je etwa 0,1 MB. Bauen: Kämmen aller Stile 18–25 min (NAS geteilt, CPU – CUDA ist ausgefallen, wird
  aber nicht gebraucht), nur Karten 4–5 min, nur Kopfhaut 1 min.

## Schwächen

- **Karten sind deutlich schwächer als TressFX:** bei kurzem Haar sieht man einzelne Karten (Klingen, „Schuppen“ von
  hinten), der Scheitel ist breiter und heller, lange Haare werden an den Spitzen strähnig-zerfranst. Sie kosten
  trotzdem mehr als gedacht (drei Lagen, bis 294 000 Dreiecke). Für kleine Porträts fehlt eine sparsame Kartenstufe.
- **TressFX ohne Kollider und fast starr:** Die gekämmte Form wird festgehalten (hohe Formbindung), Kopfbewegung und Wind
  bewegen das Haar kaum; ohne Kollider könnten bewegte Strähnen in Schultern oder Gesicht geraten. Mimik und
  Genänderung schreiben die `.tfx` neu (50–80 ms, die Simulation beginnt neu) – für ein sprechendes Porträt braucht der
  Bart einen Kieferknochen (wie im HG-Versuch).
- **Frisuren:** Pagenkopf leicht glockig, kurzes Haar mit TressFX etwas bürstig, Knoten und Zöpfe ohne Haarband,
  Frauen ohne Haube; alle Frisuren auf einem Kopf (Mann bzw. Frau, 25 Jahre) gekämmt – sehr runde oder lange Köpfe
  (Gene) verschieben das Haar nur mit, ohne neu zu kämmen.
- **Glatze:** Karten fallen kartenweise weg (grob), Strähnen je Leitsträhne; beim zurückgestrichenen Haar bleibt vom
  Kranz in der ¾-Ansicht nur ein Rest hinter dem Ohr, der im Schatten dunkel wirkt (weißes Haar).
- **Brauen und Wimpern:** besser, aber nicht auf BG3-Stand; Wimpern im Profil dünn, Brauen eher licht.
- **Nur Menschen.** Zwerge (Bärte, Zwerginnen mit leichtem Bart), Elben, Halblinge, Orks, Oger fehlen.
- **Messwerte** auf der geteilten T400 schwanken (8 Porträts ohne Haar 18,9 gegen 30,7 ms); die Abstände zwischen
  Karten und TressFX waren in beiden Läufen gleich.

## Vorschlag für Phase 5

1. **Völker:** Zwerge zuerst (Bärte geflochten und lang, Zwerginnen mit leichtem oder geflochtenem Bart, `bartwuchs_frau`),
   dann Elben (lang, glatt), Halblinge (lockig), Orks und Oger (Zöpfe, Glatze) – je drei Frisuren, gleiche Werkzeugkette.
2. **Kopfbedeckungen und Kleidung** (Haube, Gugel, Barett, Kragen): Haar darunter über eine Maske kürzen, Platzhalterkragen
   ersetzen.
3. **Lebendes Porträt:** zwei Knochen (Kopf, Kiefer) für TressFX, Kollider für Kopf und Schultern, leichte Bewegung;
   Mimik ohne Neuschreiben.
4. **Karten verbessern und staffeln:** eine sparsame Stufe (eine Lage, 30–50 000 Dreiecke) für kleine Porträts, Karten
   bei kurzem Haar dichter und flacher, Kartenbild aus den Strähnen in Blender rendern (wenn CUDA wieder geht).
5. **Erbgut** (Vorschlag an kern über den Teamleiter): Gen „haarform“ (glatt – wellig – lockig) und ggf. „haardichte“;
   ob Frisur und Bart später auch vom Stand abhängen (Geistliche: Tonsur, Patrizier: gepflegter Bart), ist eine
   Designfrage.
6. **Import ins Repository** nach Freigabe: Haardaten als Godot-Ressourcen, kleiner (Formen der Stützpunkte nur, wo
   wirksam; float16), dazu Protokollzeilen.
