# Zugpferd (Modell „Horse“ von henrysteve973)

Kein KI-Inhalt, ein Fremdmodell (CC BY 4.0) mit Eigenbau-Nachbearbeitung. Diese Datei hält Herkunft und Ablauf fest (ein Prompt entfällt). Sie ersetzt für das Pferd die Quelle in
`zugtiere.md` (Quaternius, CC0); der Ochse bleibt bei Quaternius.

## Quelle

- Modell: „Horse“ von **henrysteve973** (so steht der Name in der `license.txt` des Downloads; die Sketchfab-Seite zeigt ihn als **„Henry S (@henry_7)“**; beide Namen gelten dem
  selben Konto), https://sketchfab.com/3d-models/horse-a6f860e43e364619bccb174a1ac7d0c9
- Lizenz: CC BY 4.0 (http://creativecommons.org/licenses/by/4.0/); „Author must be credited. Commercial use is allowed.“ (`license.txt`), Download erlaubt, keine weitere Einschränkung
  an der Seite genannt.
- Credit-Text der `license.txt` (wörtlich): „This work is based on "Horse" (https://sketchfab.com/3d-models/horse-a6f860e43e364619bccb174a1ac7d0c9) by henrysteve973 (https://sketchfab.com/henrysteve973) licensed under CC-BY-4.0 (http://creativecommons.org/licenses/by/4.0/)“
- Dateien des Downloads (SHA-256, die Quelle liegt nicht im Repository):

| Datei | SHA-256 |
|---|---|
| `license.txt` | `680f93fa92ea2b332ea2bdb883a5cff797a1d8ec2be24abbd1f01063c3bbe9b0` |
| `scene.gltf` | `f8f880f6a4e070f9430d0a1516dec7667a07c2162d47eb9cfff71dbe932d5cd1` |
| `scene.bin` | `7651118412a9ea503b763153768273fe83d235790bb8ca2feb8629c15b069e59` |
| `textures/Horse_baseColor.png` | `2e7a71026ba5b0ce43f647d62c8af0d90219691e08426d56db63d5258222a725` |
| `textures/Hair_baseColor.png` | `c2d4710da5b255562e1546a23f085f6154283f8153b4658099de758ad85c851a` |
| `textures/Horseshoe_baseColor.png` | `24b576a39f669c61a9a38c94ec28be29fe876ead029654fa2e1f433b632c380c` |
| `textures/Horse_normal.png`, `Hair_normal.png`, `Horseshoe_normal.png`, `Saddle_*.png` | nicht übernommen (keine Normalkarten im Pixel-Look, Sattel entfällt) |

## Was das Spiel daraus macht (Eigenbau, `tools/modelle/pferd2_*.py`)

Die Skripte laufen auf dem Grafik-PC mit normalem Python (numpy, Pillow) und Blender (`-b`) für Reduktion und Bilder; ihre Pfade zeigen auf den Austauschordner (`pferd3` für die
Quelle, `schritt_*` für Zwischenstände), die Zwischendateien (npz-Pakete, Bildfolgen als `<Clip>_J.npz`) liegen nicht im Repository.

1. **Skinning selbst gerechnet** (`pferd2_b.py`): Blender liest das Skelett der Quelle falsch (Ruhepose verzerrt); darum liest das Werkzeug die glTF-Datei direkt und rechnet Knotenwelt
   mal Inverse-Bind-Matrix selbst. Alle Messungen und Bilder gehen von dieser Rechnung aus (Godot-Prüfung: gleiche Haltung).
2. **Form „kräftiger 3“** (Nutzerwahl 22.11.2026: deutlich kräftig, Oberlinie Genick–Widerrist glatt): Widerrist 1,6 m, Sattel und Steigbügel weg, Verschiebungsfelder statt
   Skalierung (`pferd2_form_b.py`: Röhre vorn +47 %, Rumpf tiefer und breiter, Kruppe, Fesselkopf ausgestellt; Gelenke und Gewichte unverändert).
3. **Reduktion** (`pferd2_reduce.py --schutz --weld --Horse=5030 --Hair=2600 --schutzw=0.9`, Blender Decimate): Körper 5.030, Mähne und Schweif 2.600, Hufeisen 200, Augen 160 =
   **7.990 Dreiecke** (Grenze 8.000). `--weld` verschweißt die Ecken gleicher Lage vor dem Kollaps (sonst reißen die UV-Inselkanten auf: ein Schlitz in der Brustmitte).
   Schweifspalt geschlossen (`pferd2_schweif.py`). Fernnetz `Fern_pferd` 1.614 Dreiecke (Körper 949, Haar 420, Hufeisen 60, Augen 15, Geschirr 170).
4. **Texturen** (`pferd2_umfaerben.py`, `pferd2_tex_naht.py`): Schecke zu einfarbigem Braun, Rappe, Fuchs umgefärbt (Fell 2048², Mähne als RGBA mit Alpha aus der Strähnenkarte, Fuchs-Mähne
   gedämpft), weicher Fesselbehang als helle Aufhellung, Farbsprünge an UV-Nähten gemittelt. Im Spiel-GLB steht alles in **einem Atlas** (2048 × 3072, 256 Farben
   mit Alpha; Fell, Mähne, Hufeisen, Auge, Geschirr), ein Stoff mit Alpha-Maske 0,5.
   Fell und Karten (Folge 23.11.2026, Eigenbau ohne fremdes Bildmaterial, `pferd2_fell.py`, `pferd2_spiel.py --fell`): Haarstrich je Texel aus der Lage in der Stehpose
   (Rumpf nach hinten unten, Beine nach unten, Kopf zur Nase; Rauschen entlang dieser Richtung gefaltet, fester Zufall 1480), leicht in die Farbe gemischt; Oberlinie etwas dunkler,
   Bauch etwas heller, beim Fuchs die Unterbeine dunkler; keine Abzeichen. Geschirr: Leder mit Narbung und Falten, Holz mit Maserung, Eisen fleckig mit Rost, Gurt als Gewebe;
   die Geschirr-UV bildet die Lage (Periode 0,25 m, gespiegelt) auf das Feld ab. Dazu eine **Normalkarte** (JPEG, 1024 × 1536, OpenGL) und eine **ORM-Karte** (PNG, 512 × 768;
   Rauheit Fell 0,72, Rappe 0,55, Mähne 0,6, Hufeisen und Eisen metallisch). Speicher je Fell unkomprimiert mit Mip-Stufen: Albedo etwa 34 MB, Normal 8,4 MB, ORM 2,1 MB.
5. **Geschirr** (`pferd2_geschirr.py`, Eigenbau, 1.032 Dreiecke, extra gezählt): Kummet als geschlossener, gepolsterter Ring in der Hülle von Hals und Brust (Birnenform: Halbmesser 0,20 bis 0,40 m), Querschnitt 9 cm im Ring und 15 cm in Laufrichtung, oben vor dem Widerrist, unten an der Bugspitze, Ebene 25 Grad aus der Senkrechten,
   Hamen als kurze Holzbügel seitlich am Polster (enden am Polster, kreuzen sich nicht) und Eisenhaken daran, Bauchgurt als flaches Band (9 cm breit, 1,2 cm dick) um die Rumpfhülle hinter den Ellbogen;
   Gewichte von der nächsten Hautecke kopiert. Der Zugpunkt (Ringmitte im Ziehen, Mittel über den Zyklus) liegt 1,279 m hoch und 0,67 m vor dem Ursprung (`anker_ziehen_godot` in `zugtiere.json`).
6. **Bewegungen** (alle Eigenbau; Ablauf und Messwerte in `docs/bereiche/darstellung.md`): `pferd2_gang.py` (Gehen und Ziehen, Zyklus 26 Bilder zu 24/s = 1,0833 s, 1,3 m/s,
   Hufe per ebenem IK nach Muybridge-Tafel 563 und Gelenkwinkeltabelle), `pferd2_hufhalter.py` (Stehen, Stehen_Ruhen, Stehen_Umschauen, Fressen mit festen Hufen),
   `pferd2_start.py` (Anfahren_ und Anhalten_ je Gehen und Ziehen). Zehn Clips: Stehen, Stehen_Ruhen, Stehen_Umschauen, Fressen, Gehen, Ziehen, Anfahren_Gehen, Anfahren_Ziehen,
   Anhalten_Gehen, Anhalten_Ziehen. Die Clips des Autors (Idle, Eat, Walk, Sleep, Arise …) sind nicht mehr enthalten; Schlafen und Aufstehen gibt es nicht.
   Schwung nach Muybridge (`--tab=p5`, 23.11.2026): Karpus im Schwung bis etwa 110 Grad gebeugt, Fessel vorn gebeugt (165 bis 170), der Huf klappt nach dem Abheben ab und hängt mit der
   Sohle nach hinten (vorn bis etwa 70 Grad, hinten etwa 30 Grad gegen den Stand) und wird erst zum Aufsetzen flach; Hub am Sohlenmittelpunkt vorn 0,19 m (Zehe 8 bis 10 cm über dem Boden),
   hinten 0,14 m. Gleiche Ziele in `pferd2_start.py` für Anfahren und Anhalten (kurze Schritte aus dem Stand mit anteiligem Hub und anteiliger Beugung).
   Gang p6 (`--tab=p6`): Oberarm und Oberschenkel im Schwung in einem Fenster um die Ruhelage, Glätte gegen das vorige Bild (kein Zucken der Hinterhand), Schultergelenk gleitet weich
   ±3 cm (statt Drehung des Schulterblatts, das keinen eigenen Knochen hat), Vorderhuf schwingt später vor (Karpus bei 80 % 110 Grad). Gang p7: Stand vorn und hinten weiter hinten (Bein steht am Ende schräg nach hinten), Abrollen 25 %, Knie und Sprung im Stand weicher gewichtet.
   Befehle: Austauschordner `pferd2/schritt_b/gehen_p7/clips/befehle.txt`; Spiel-GLB mit `--atlas=<PNG>` (Atlas des vorigen GLB unverändert).
7. **Export** (`pferd2_spiel.py`): Meter, Füße auf dem Boden, Blick nach +Z (die Welt des Autors ist rund 10,5-fach zu groß; Verschiebungen, Kanäle, Inverse-Bind-Matrizen und Ecken
   werden mit dem Maßstab multipliziert, der oberste Knoten trägt Drehung und Verschiebung), Netze `Nah_pferd` und `Fern_pferd`, Knoten `Anker_Geschirr`. Prüfung nur aus der Datei
   (`pferd2_spielpruef.py`): größte Abweichung der Haut gegen die Rechnung der Werkzeuge 0,001 mm in allen zehn Clips.

## Befehle (Grafik-PC)

```
blender -b --python tools/modelle/pferd2_reduce.py -- --ein=<form11.npz> --aus=<p5c_red.npz> --schutz --Horse=5030 --Hair=2600 --schutzw=0.9 --weld
python -I tools/modelle/pferd2_schweif.py <p5c_red.npz> <Stehen_J.npz> <p5c.npz>
python -I tools/modelle/pferd2_umfaerben.py <Horse_baseColor.png> <Hair_baseColor.png> <tex> --naht=<p5c.npz>
python -I tools/modelle/pferd2_geschirr.py <p5c.npz> <Stehen_J.npz> <Ziehen_gehen_J.npz> <p5c_g.npz> --info=<geschirr_info.json>
blender -b --python tools/modelle/pferd2_reduce.py -- --ein=<p5c_g.npz> --aus=<p5c_fern.npz> --schutz --weld --Horse=950 --Hair=420 --Horseshoe=60 --Cornea=16 --Geschirr=140 --schutzw=0.9
python -I tools/modelle/pferd2_fell.py <p5c_g.npz> <Stehen_J.npz> <tex> <fell> --staerke=0.35 --strich=16 --strich_fein=6 --strich_farbe=0.09
python -I tools/modelle/pferd2_spiel.py <p5c_g.npz> <p5c_fern.npz> <braun|fuchs|rappe> <tex> <ausgabe.glb> --clipdateien="Gehen=<J.npz>;…" --ruhepose=<Stehen_J.npz> --info=<geschirr_info.json> --fell=<fell>
python -I tools/modelle/pferd2_zugtiere_json.py <geschirr_info.json>
blender -b --python tools/modelle/karren.py -- --nur=pferd
blender -b --python tools/modelle/pferd2_gespann.py -- --karren=<karren_pferd.glb> --paket=<p5c_g.npz> --framen=<Ziehen gehen_J.npz> --info=<geschirr_info.json> --aus=<Ordner> --norm=<henry_norm.json>
```

Namen im Spiel: Braun = `tier_pferd_rotbraun.glb`, Rappe = `tier_pferd_schwarzbraun.glb`, Fuchs = `tier_pferd_fuchs.glb`.

## Festgehaltene Ergebnisse (Neuexport gleich)

Der Schweifspalt wird mit `pferd2_schweif.py` und den Standardwerten (`--dy=0.06 --rest=0.005`) geschlossen; es gibt keinen Zufall, Reduktion (Blender Decimate) und Schweif sind
deterministisch. SHA-256 über Namen und Bytes aller Felder der Pakete: `p5c` (nach Schweif) `fa673906e5e259ef577ca810ede6d344ce43986a65c5cbbea83758ff162949de`, `p5c_g5` (mit Geschirr)
`c958c913cd33006a859223d83b9a70da62e06a6fe38e19de8516f3f6c55de736`. SHA-256 der drei Spiel-GLB (Schwung nach Muybridge): `tier_pferd_fuchs.glb`
`2ba34c619d963828ea5117177405d5dd56a4521abf69a3507889897a4b711357`, `tier_pferd_rotbraun.glb` `57bc68ec2d8b8cb625bec2a9cd20b0f77b832c716ae79253ab0ad7afe3839f72`,
`tier_pferd_schwarzbraun.glb` `665979b83e5564fa0a95b45f28c73e8b290b33ec3f556cc0a56268f73490c7c2` (Gang p7, Fell mit Normal- und ORM-Karte). Der Stand davor (ea0923…, ff34d8…, 90ba19…) lässt sich mit den
Clips aus `etappe2` (gehen_p4, ziehen_p4, p4_*, Fressen) und `--atlas` byte-gleich nachbauen.

## Prüfung des Spiel-GLB (ohne Godot)

`tools/modelle/pferd2_spielrender.py` liest das Spiel-GLB selbst (Knoten, Haut, Netz, Atlas) und rendert in Blender mit Rückseiten-Wegschneiden, Alpha-Maske und den Normalen der Datei; die Bilder
für die Kritik kommen daraus, nicht aus dem Werkzeug-Render (`blender -b --python tools/modelle/pferd2_spielrender.py -- --glb=<tier_pferd_*.glb> --aus=<Ordner> [--klip=Ziehen --bild=6] [--karren=<karren_pferd.glb>]`).
Die `.import`-Dateien der Pferde stellen `meshes/generate_lods` aus (das Geschirr besteht aus dünnen geschlossenen Röhren, die eine LOD-Vereinfachung des Importers zuerst wegnehmen könnte).
