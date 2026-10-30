# Charakter-Editor, Phase 3: Aussehen (Haut, Alter, Brauen, Wimpern, Augen)

Stand 30.10.2026, Branch `phase5/charakter-werkzeug`, Mitglied darstellung. Kein Spielcode, kein PR. Grundlage:
[Phase 2](phase2.md) (Werkzeugkette, Basiskopf) und [Phase 0](phase0.md) (Vortest A2: Rauheit, Rötung, Poren, Wrap).

Ziel: Der Kopf soll nicht mehr nach Porzellan aussehen, sondern gemalt-realistisch (Stil BG3/CK3).

## Ergebnis in Kürze

- **Haut mit Bild:** Die MakeHuman-Haut „young_caucasian_female“ (CC0) ist auf die ICT-UV gebacken (2048²), durch die
  mittlere Hautfarbe geteilt – der **Hautton kommt aus den Genen** (Shader-Parameter, keine Textur je Ton). Dazu
  gebackene Masken (Rötung an Wangen, Nase, Ohren, Lidern, Lippen; Glanz der T-Zone; Verdeckung; Bartschatten),
  feine Flecken und Sprenkel, Poren und Lippenlinien als Normalen, ein dunkler Saum am Oberlid.
- **Hautshader** `haut3`: Wrap-Licht je Farbkanal (Rot streut am weitesten) mit geglätteter Normale fürs Streulicht
  und voller Normale für den Glanz, zwei Glanzkeulen, Randlicht (Flaum), Durchlicht an Ohren und Nase. Echtes
  (Bildschirm-)SSS nur in `haut3_ultra`. Kosten: +0,95 ms je Porträt gegen die Stoffe der Phase 2, Ultra +1,4 ms dazu.
- **Alter:** Faltenkarten als Höhen in vier Kanälen (Stirn und Zornesfalten, Krähenfüße und Unterlid, Nasolabial und
  Marionette, Hals und Lippenfältchen), je Gegend über eine Alterskurve eingeblendet (wie `WertImAlter`), dazu
  **Ausdrucksfalten**: Brauen heben vertieft die Stirn, Lächeln die Krähenfüße und die Nasolabialfalte. Brauen und
  Wimpern ergrauen Haar für Haar (Salz und Pfeffer).
- **Brauen und Wimpern:** eigene Karten (zwei Brauenlagen entlang der ICT-Merkpunkte, gebogene Wimpernstreifen an
  Ober- und Unterlid), Bilder Haar für Haar gezeichnet; Dichte, Breite und Farbe aus den Genen; Alpha-to-Coverage mit
  MSAA, sonst Alpha-Schere.
- **Augen:** Irisbild (RoboPoets, CC0) nach den Genen gefärbt (außen + innerer Kranz), dunkler Limbus, Pupille
  mittig zur Öffnung im ICT-Netz, Lederhaut warm mit Äderchen, Hornhaut als eigene glänzende Fläche (Glanzpunkt,
  Feuchtfilm), Lidschatten-Netz dunkelt den Augapfel unter den Lidern ab.
- **Fleck vor dem Ohr:** Ursache gefunden und behoben (Schatten des Schlüssellichts × Wrap-Licht, siehe unten).
- **Galerie** `charakter_galerie.tscn`: 30 Köpfe (hell, mittel, dunkel × Mann, Frau × 5, 20, 35, 50, 70 Jahre), ein
  Netz im Speicher, Tag- und Innenlicht umschaltbar, startet mit der **freien Kamera** (`game/scripts/FreieKamera.cs`,
  unverändert vom Branch `phase5/wasser-darstellung` übernommen). Auch die Regler-Szene startet jetzt mit ihr.

Ehrlich: Der Abstand zu BG3/CK3 ist kleiner, aber noch da – siehe „Schwächen“.

## Testanleitung

Einmalig (Fremddaten unter `~/werkzeuge/charakter/`, nicht im Repository; Phase 0 und 2 haben sie schon geholt):

```bash
tools/charakter/einrichten.sh     # MPFB 2 und die MakeHuman-Pakete (nur, falls noch nicht geschehen)
tools/charakter/kopf3.sh          # Kopf bauen (≈ 1 min) und Haut backen (≈ 3 min) nach ~/werkzeuge/charakter/glb3
```

**Galerie** (empfohlen):

```bash
# mit Bildschirm (Godot 4.7.2 .NET, vorher einmal dotnet build Reichsgilde.sln):
godot --path game res://versuch/charakter/charakter_galerie.tscn -- --glb=$HOME/werkzeuge/charakter/glb3
# im Godot-Editor: versuch/charakter/charakter_galerie.tscn öffnen und F6 (die .glb liegen dann im Standardordner glb3)
```

Steuerung der Galerie:

| Taste | Wirkung |
|---|---|
| rechte Maustaste halten | umsehen (freie Kamera) |
| W A S D | fahren, Q/E runter/hoch, Shift schneller, Mausrad Tempo (Start 1,5 m/s), H Hilfe der Kamera |
| L | Licht: Tag (Sonne, Himmel) / Innen (Herdfeuer und Kerzen, dunkel) |
| V | vorher/nachher: Stoffe der Phase 2 am selben Kopf (Brauen und Wimpern bleiben die neuen) |
| U | Ultra: Haut mit Bildschirm-SSS ein/aus |
| M | Mimik weiterschalten: neutral, Lächeln, Stirnrunzeln, Brauen hoch, Mund offen + halb blinzeln |
| F1 | Hilfetext ein/aus |

Reihen von vorn: hell Mann, hell Frau, mittel Mann, mittel Frau, dunkel Mann, dunkel Frau; von links 5, 20, 35, 50,
70 Jahre. Ohne `FreieKamera.cs` springt 1–6 zu einer Reihe.

**Regler** (ein Kopf, alle Gene, Alter, Geschlecht, Mimik, wie in Phase 2, jetzt mit freier Kamera; die Ansicht-Wahl
stellt die Kamera neu hin): `godot --path game res://versuch/charakter/charakter_schau.tscn`.

**Bögen, Fotos und Messung** (ohne Bildschirm, Vulkan auf der T400, je Lauf eine eigene `ANZEIGE`):

```bash
G=$PWD/tools/charakter/boegen3
tools/charakter/schau.sh <bilder> 2400x1900 --bogen=$G/vergleich.json,$G/alter_mann.json,$G/alter_frau.json,\
$G/hauttoene.json,$G/nah.json,$G/fleck_p3.json --ende
tools/charakter/schau.sh <bilder> 1280x720 --messen --nur=leer,still,still_vorher,still_ultra,still_phase2,mimik,x8_still --ende
SZENE=charakter_galerie.tscn tools/charakter/schau.sh <bilder> 1600x900 --fotos --messen
```

Neue Bogenschlüssel: `"stoff": {"haut": {…}, "augen": {…}, "haar": {…}}`, `"umgebung": {…}`, `"schluessel": {…}`
(Schlüssellicht), `"vorher": true`, `"ultra": true`, `"ausblenden": ["hornhaut", "lidschatten", "iris", …]`.

## Was gebaut ist

### Werkzeugkette (`tools/charakter/kopf3.sh`)

1. **Bau** (`kopf2_bauen.py`, wie Phase 2) mit: eigenen Brauen- und Wimpernkarten (`kopf3_haare.py`), ebener
   Augen-UV, Hornhaut als eigener Fläche (Stoff „Hornhaut“), Eckfarbe des Lidschatten-Netzes (Nähe zur Lidnaht), der
   **Pupillenöffnung** der ICT-Iris per Strahl von vorn gesucht (sie sitzt 1,6 mm neben der Achse
   Lederhaut → Iris; ohne das lagen gemalte Pupille und Öffnung versetzt: „Schneemann-Pupille“ bzw. helle Sichel) und
   den **Backdaten** (`backdaten=…npz`: Hautnetz mit Spiel-UV, Lage auf der MakeHuman-Haut, MakeHuman-UV, Merkpunkte).
2. **Haut backen** (`kopf3_backen.py`, Blender wegen BVH, ≈ 3 min, 2,2 Mio. Bildpunkte): Spiel-UV rastern; je
   Bildpunkt die MakeHuman-UV (je Ecke über die Bindung, an Nähten je Bildpunkt neu); Hautbild abtasten; Kopfhaut
   (Stoppeln) über eine Haaransatz-Linie und die Farbe ersetzen; gemalte Augen am Lidrand durch hautähnliche
   Umgebung ersetzen; Bindehaut in den Augenhöhlen; Flecken und Sprenkel; Lidsaum; durch die mittlere Hautfarbe
   teilen. Masken aus Merkpunkten; Verdeckung mit 24 Strahlen je Ecke (2 cm); Falten als eigene Rechenregeln auf
   Kurven, die auf die Haut gelegt sind; Normalen aus Höhen (Poren als 3D-Zellrauschen, Lippenlinien, Hochpass
   des Hautbilds) mit Metern je Bildpunkt aus den Dreiecken.
3. **Haarbilder** (`kopf3_haartexturen.py`, ≈ 15 s): Brauen (1100 + 340 Haare, Wuchsrichtung innen steil, im Bogen
   flach, oben flacher als unten) und Wimpern (150 oben in Büscheln, 48 unten).

Ergebnis in `~/werkzeuge/charakter/glb3/`: `kopf3.glb` (31,4 MB), `kopf3_ohne.glb`, `kopf3_mn.glb`, je eine `.json`
und `kopf3_tex/` (8 PNG, 7,9 MB).

| Bild | Inhalt |
|---|---|
| `haut_farbe.png` 2048² | RGB = Hautbild / mittlere Hautfarbe × 0,5 (linear), A = Brauenschatten |
| `haut_normal.png` 2048² | RG = Normale (Tangentenraum, OpenGL), B = Porenstärke für die feine Kachel |
| `haut_masken.png` 2048² | R Rötung, G Glanz, B Verdeckung, A Bartschatten |
| `haut_falten.png` 2048² | Höhen (0,5 = keine): R Stirn, G Augen, B Mund, A Hals und Lippen |
| `brauen.png`, `wimpern.png` 1024 × 512 | R Zufall des Haars, G Wurzelabstand (Breite), B Lage im Haar, A Deckung |
| `iris_muster.png`, `lederhaut.png` 1024² | RoboPoets „iris_color_mask“ (R Fasern, G Kranz) und „iris_depth“ (B), „sclera_color“ |

### Godot (`game/versuch/charakter/`)

| Datei | Aufgabe |
|---|---|
| `haut3.gdshaderinc`, `haut3.gdshader`, `haut3_ultra.gdshader` | Hautshader (ohne / mit Bildschirm-SSS) |
| `augen3.gdshader` | Iris und Lederhaut; `pruefen` 1–3 zeigt Teile, Rückseiten, Abstände (Fehlersuche) |
| `hornhaut.gdshader` | Hornhaut und Tränenfilm: nur Glanz, additiv |
| `lidschatten.gdshader` | Lidschatten-Netz: Multiplikation |
| `haar3.gdshader` | Brauen und Wimpern (Dichte, Breite, Ergrauen je Haar, Ausgleich der Mipmaps) |
| `charakter_kopf.gd` | lädt die Bilder (einmal je Ordner), Stoffe der Phase 3, Gene → Hautfarbe, Falten, Bart, Brauen, Iris; `vorher_setzen`, `ultra_setzen`, `kopieren_von` (Kopf mit geteiltem Netz) |
| `charakter_galerie.gd/.tscn` | Galerie (siehe oben) |
| `charakter_schau.gd`, `charakter_buehne.gd` | Regler und Bögen wie Phase 2, freie Kamera, neue Bogenschlüssel; Bühne: Belichtung 1,0 statt 1,15, Schlüssellicht 2,3 statt 2,6 (Haut war überbelichtet) |
| `game/scripts/FreieKamera.cs` | unverändert vom Branch `phase5/wasser-darstellung` (Mitglied wasser) |

### Gene und Alter

| Größe | Regel (Testszene, `charakter_kopf.gd`) |
|---|---|
| Hautfarbe | „haut_helligkeit“ 0 → (0,20; 0,125; 0,09), 128 → (0,55; 0,37; 0,27), 255 → (0,84; 0,66; 0,56) (sRGB); „haut_ton“ tönt wie Phase 2 |
| Falten | Gewicht je Gegend aus einer Alterskurve (`FALTEN_ALTER`, Jahre → Gewicht; Stirn ab 25, Augen ab 28, Mund ab 20, Hals ab 35, mit 75 voll); helle Haut etwas mehr, „gewicht“ glättet die Stirn und vertieft die Nasolabialfalte; Frauen 85 % |
| Ausdrucksfalten | Stirn + 0,7 × Brauen heben (bzw. 0,6 × runzeln), Augen + 0,6 × Wangen/Augen zusammenkneifen, Mund + 0,5 × Lächeln; bei Kindern ein Viertel |
| Brauendichte | aus „bartwuchs“ (Männer 0,62 + 0,38 · Gen), Frauen 0,5 + 0,3 · „bartwuchs“ + 0,2 · „bartwuchs_frau“; Kinder lichter, Männer über 55 buschiger; Frauen schmaler (Breite 0,72) |
| Ergrauen | wie Phase 2 (ab 20 + Gen/255 · 50 Jahren über 25 Jahre), jetzt als Anteil grauer Haare; Wimpern halb so stark |
| Bartschatten | Männer ab 15 nach „bartwuchs“, Frauen nach „bartwuchs_frau“; heller Bart zeigt sich kaum |
| Iris | außen wie Phase 2, innerer Kranz bernstein (stark bei braunen und grünen, schwach bei blauen Augen) |

Die Faltenkurven stehen in der Darstellung, nicht in `erbgut.json` (die ändert nur kern); Vorschlag dazu unten.

### Fleck vor dem Ohr (Phase 2)

Bogen `fleck.json` (Phase-2-Kopf): Der Fleck bleibt ohne Hautmasken, ohne SSAO, mit SSAO 2 cm und ohne Poren;
die Netze sind links und rechts gleich (Abweichung unter 0,04 mm); **ohne den Schatten des Schlüssellichts ist er
weg**. Ursache: Das Wrap-Licht des Hautshaders hellt die Seite jenseits des Schattenrands auf; dort meldet die
Schattenkarte die eigene Rundung als Schatten, je nach Bias stellenweise nicht – das gab den runden Fleck auf der
abgewandten Wange. **Behoben** in `haut3`: Für Richtungslicht gilt der Schatten im Streulichtbereich (Normale vom
Licht weg) nicht, in Höhlen (Nasenlöcher, Ohrmuschel) doch. Bogen `fleck_p3.json`: Phase 2 mit Fleck, Phase 3 ohne.
Grenze: Steht ein Kopf ganz im Schatten eines anderen Gegenstands, bekommt die abgewandte Seite trotzdem den
schmalen Streulichtsaum (`streu_schattenfrei` = 0 schaltet das ab).

## Quellen und Lizenzen

| Quelle | Lizenz | Beleg | Verwendet für |
|---|---|---|---|
| MakeHuman „young_caucasian_female“ (`young_lightskinned_female_diffuse.png`, makehuman_system_assets) | CC0 1.0 | Kopf der `.mhmat` („explicitly released as CC0 in september 2020“), `packs/makehuman_system_assets.json` | Hautbild (gebacken) |
| MakeHuman-Grundnetz und dessen UV (MPFB 2.0.18 Daten) | CC0 1.0 | MPFB `LICENSE.md` C, `LICENSE.ASSETS.md` | Übertragung Hautbild → Spiel-UV |
| RoboPoets digital_human (Commit 9d28d0c), `iris_color_mask.webp`, `iris_depth.webp`, `sclera_color.webp` | Kunst CC0, Code MIT (kein Code übernommen) | `README.md`: „all art assets are CC0“, „doesn't use any third-party assets“; `LICENSE.md` (MIT) | Irismuster, Lederhaut |
| ICT-FaceKit Light (wie Phase 2) | MIT | `LICENSE` | Netz, Augen (Pupillenöffnung), Merkpunkte für Brauen |
| Eigene Rechenregeln (Falten, Poren, Masken, Haare, Shader, Skripte) | Code MIT; Bilder „Alle Rechte vorbehalten“ (eigener Anteil) | – | – |

Nicht verwendet: Human Generator, `versuch/hg-koepfe`, MakeHuman-Brauen/-Wimpern (eyebrow001, eyelashes01 entfallen).

**Asset-Protokoll:** Es liegt nichts Neues unter `game/assets/` – Kopf und Bilder entstehen lokal in
`~/werkzeuge/charakter/glb3` (wie in Phase 2), `protokoll.csv` bleibt unverändert. Vorschlag für später (wenn Kopf
und Bilder ins Repository kommen), je eine Zeile:

| Spalte | `kopf3.glb` | `kopf3_tex/haut_*.png` | `kopf3_tex/brauen.png`, `wimpern.png` | `kopf3_tex/iris_muster.png`, `lederhaut.png` |
|---|---|---|---|---|
| `herkunft` | Fremdpaket (ICT, MakeHuman), eigenes Skript | Fremdpaket (MakeHuman-Haut) + eigene Rechenregeln | eigenes Skript (prozedural) | Fremdpaket (RoboPoets), umgewandelt |
| `werkzeug` | `tools/charakter/kopf3.sh` (Blender 5.2.2, MPFB 2.0.18 nur Werkzeug) | `kopf3_backen.py` | `kopf3_haartexturen.py` | `kopf3_backen.py` |
| `modell_version` | entfällt (keine KI) | entfällt | entfällt | entfällt |
| `bedingungen_werkzeug` | MIT (ICT), CC0 (MakeHuman), MPFB GPL 3 nur Werkzeug | CC0 (MakeHuman) | – | CC0 (RoboPoets-Kunst) |
| `lizenz_ergebnis` | „Alle Rechte vorbehalten“ für eigene Anteile, enthält ICT (MIT, Hinweis mitliefern) | „Alle Rechte vorbehalten“ (eigener Anteil), enthält CC0 | „Alle Rechte vorbehalten“ | enthält CC0 (RoboPoets), umgewandelt |

**Fremdlizenz (Vorschlag, `LICENSE-INHALTE.md`/`HERKUNFT.md` sind geschützt und nicht geändert):** zu den in
Phase 2 vorbereiteten Einträgen (ICT MIT) kommt neben die Bilder eine `HERKUNFT.md` mit „Hautbild aus MakeHuman
young_caucasian_female (CC0 1.0, Data Collection AB, Joel Palmius, Jonas Hauquier)“ und „Irismuster und Lederhaut aus
RoboPoets digital_human (Kunst CC0, Robo Poets UG)“; CC0 verlangt keine Nennung, sie dient der Herkunft.

## Prüfung (T400, Vulkan über die NVIDIA-ICD wie `TREIBER=nvidia`, `schau.sh`, Anzeige :96)

Bilder (nicht im Repository): Scratchpad `…/scratchpad/charakter3/` – `ende/` (Bögen), `galerie3/` (Galerie),
`fleck/` (Fehlersuche Fleck), `dbg/` (Fehlersuche Auge), `messung*/`.

| Bogen | Bewertung |
|---|---|
| `vergleich.png` (oben Phase 2, unten Phase 3, gleiche Bühne) | Phase 3 hat Hautfarbe mit Unruhe, Rötung an Wangen, Nase, Lidern, Ohren; Lippen getönt; Brauen aus Haaren; Wimpern; Augen mit Irismuster und warmer Lederhaut; der 70-Jährige zeigt Stirn-, Augen- und Mundfalten. Phase 2 wirkt daneben wie Porzellan. |
| `alter_mann.png`, `alter_frau.png` (5–70 × neutral, Lächeln, Brauen hoch, halb blinzeln) | Falten wachsen mit dem Alter; Brauen hoch zeigt ab 35 Stirnfalten, Lächeln Krähenfüße; Kinder glatt; Brauen ergrauen ab etwa 60. |
| `hauttoene.png` (hell, mittel, dunkel × Mann 35, Frau 50, Porträt und Augen) | Töne aus einem Bild; dunkle Haut glänzt sichtbar mehr (richtig), Iris blau/grün/braun, Brauen der Frauen schmaler. |
| `nah.png` (Augen, Augen ¾, Mund, Stirn × 20–70) | Poren und Flecken nah sichtbar, Lidsaum, Falten nach Gegend. |
| `fleck_p3.png` | Phase 2 mit Fleck, Phase 3 ohne. |
| Galerie Tag/Innen, nah und weit, vorher/nachher | 30 Köpfe ohne Fehler; Innenlicht (Herdfeuer) wirkt gemalt; vorher/nachher am 70-Jährigen deutlich. |

Keine SCRIPT- oder SHADER-ERROR in den Läufen (nur die bekannten Meldungen von Xvfb und beim Beenden).

## Kosten (T400)

Ein Porträt 1280 × 720, Bühne wie Phase 0 (Schatten, SSAO, MSAA 4×), 6–8 s gemessen, zwei Läufe (beim zweiten keine
anderen GPU-Läufe):

| Lauf | GPU ms (Lauf 1 / 2) |
|---|---|
| leere Bühne | 2,96 / 2,55 |
| Phase-2-Kopf (`kopf2.glb`, Stoffe der Phase 2) | 4,70 / 4,52 |
| Phase-3-Kopf mit Stoffen der Phase 2 (vorher) | 4,56 / 4,57 |
| **Phase-3-Kopf** | **5,50 / 5,52** (+0,95 ms) |
| Phase 3, Ultra (Bildschirm-SSS) | 6,92 / 6,94 (+1,4 ms) |
| Phase 3, Mimik bewegt (55 Formen) | 6,02 / 6,05 |
| 8 Porträts (je eigene .glb geladen) | 17,2 |
| **Galerie 30 Köpfe**, 1600 × 900, Tag / Ultra / Innen | 19,7 / 22,5 / 16,7 |

- **Speicher:** Bilder +103 MB Grafikspeicher (unkomprimiert, RGBA8 mit Mipmaps, zur Laufzeit geladen; einmal je
  Ordner, alle Köpfe teilen sie). Nach dem Import im Spiel (BPTC/S3TC) etwa 26 MB. Dateien: 7,9 MB PNG.
- **Puffer:** ein Kopf 67,5 MB (wie Phase 2); die Galerie teilt ein Netz: **30 Köpfe 112,6 MB** statt 30 × 66 MB
  (`kopieren_von`). Acht einzeln geladene Köpfe 484 MB.
- **.glb:** 31,4 MB (Phase 2: 30,8 MB; +1 272 Dreiecke für Brauen und Wimpern, 54 556 gesamt).
- **Laden:** .glb 1,5 s, Bilder 0,8 s einmal (PNG lesen, Mipmaps); Galerie: 30 Köpfe kopieren 3,6–4,0 s.
- **Backen:** Bau 1 min, Haut 3 min, Haare 15 s (Blender, 4 Threads, NAS).

## Schwächen

- **Hautbild unscharf:** Die MakeHuman-Haut hat für das Gesicht nur etwa 300 × 350 Bildpunkte; gebacken auf 2048²
  bleibt sie weich. Die Schärfe kommt aus Poren, Flecken, Normalen und der feinen Kachel – nah wirkt die Haut
  gleichmäßig „gemalt“, aber nicht wie ein Scan. Ein eigenes, hoch aufgelöstes Hautbild (Scan CC0 oder gemalt) wäre
  der nächste große Schritt.
- **Ein Hautbild für alle:** Männer und Frauen, alle Töne und Völker teilen es (Abweichungen nur über Masken, Ton,
  Falten). Ork- und Oger-Haut fehlen (Phase 5).
- **Lippen:** Farbe aus dem Hautbild plus Rötung; die Grenze des Lippenrots folgt den ICT-Merkpunkten nur grob.
- **Wimpern:** von vorn ordentlich, im Profil und nah wirken die oberen etwas starr und büschelig; keine Physik.
- **Brauen:** Karten liegen auf der Haut; bei starker Mimik (Brauen ganz hoch) können sie sich leicht abheben.
  Die Form ist für alle gleich (nur Dichte, Breite, Farbe aus den Genen).
- **Falten:** Höhen statt Geometrie – die Nasolabialfalte zeigt sich als Linie mit leichtem Wulst, nicht als echte
  Hautfalte; Altersflecken, Erschlaffung und dünnere Haut im Alter fehlen. Die Faltenkurven stehen in der
  Darstellung, nicht im Erbgut.
- **Kopfhaut:** Ersatzfarbe statt Haut mit Struktur; am Übergang zum Gesicht bleibt ein weicher Saum, hinter dem Ohr
  (Stoppeln des Hautbilds nach der Farbe ersetzt) ein etwas hellerer Fleck im Profil; Haare kommen in Phase 4.
- **Schatten:** Das Streulicht jenseits des Schattenrands ignoriert den Schatten der eigenen Rundung (Ursache des
  Flecks); ein Kopf ganz im Schatten eines Hauses bekäme einen schmalen Lichtsaum.
- **Iris:** ein Muster für alle (das rechte Auge gedreht); keine Brechung (Parallax) der Hornhaut.
- **Messwerte:** GPU-Zeiten auf der geteilten T400 schwanken (die anderen Mitglieder rechnen mit); die Unterschiede
  (+0,95 ms, +1,4 ms Ultra) waren in beiden Läufen gleich.
- Die Galerie zeigt nackte Köpfe mit Platzhalterkragen; Frauen wirken ohne Haar weiter knabenhaft.

## Vorschlag für Phase 4

1. **Haar und Bart** (der größte sichtbare Abstand zu BG3/CK3): Haarkarten-Frisuren nach Gen „frisur“, Bärte nach
   „bartform“/„bartwuchs“ (der Bartschatten liegt schon in der Maske), Glatze nach „glatze“; Kopfhaut unter dem Haar.
   Werkzeug: Karten aus Kurven in Blender (wie Brauen und Wimpern), TressFX nur für Ultra prüfen.
2. **Kleidung statt Platzhalter:** Hemd, Kragen, Gugel je Stand (Bauer, Handwerker, Patrizier, Geistlicher).
3. **Haut je Volk und Alter:** eigene Hautbilder für Ork und Oger (Phase 5 vorziehen?), Altersflecken und
   Erschlaffung (eine Maske mehr), Sommersprossen als Gen-Vorschlag an kern.
4. **Erbgut:** Vorschlag an kern – Gen „faltenneigung“ (oder Alterskurven je Gegend) in `erbgut.json`, damit die
   Falten vererbt werden und `WertImAlter` sie rechnet.
5. **Einbacken der Identität** (Speicher, aus Phase 2 offen): für Stammbaum und Ratstafel Makros und Gene in ein
   Netz backen, nur die Mimik als Formen; dann kleine Porträts ohne Schatten und SSAO messen.
6. **Import ins Repository:** Kopf, Bilder (komprimiert) und `HERKUNFT.md` nach Freigabe des Nutzers.
