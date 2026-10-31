#!/bin/bash
# Charakter Phase 6: Basiskopf wie Phase 5 bauen (Skriptkette kopf2_bauen.py, kopf3_backen.py, kopf3_haartexturen.py),
# aber mit Formnormalen (S2): Gen-, Alters- und alle neuen Formen (eigen_, zug_, asym_) behalten ihre Normalen, Licht und
# Schatten folgen also der Form. Lage und Normale der Formen stehen als dünne Accessoren in der Datei (nur bewegte Ecken),
# damit kopf6.glb nicht größer wird als kopf5_mn.glb. Ausgabe nach ~/werkzeuge/charakter/glb6 (nicht im Repository);
# Phase 5 bleibt in glb5 (Stand 5 der Bögen).
#
# Aufruf: kopf6.sh [ohne_haar|nur_kopf|nur_haar|kaemmen] [weitere Optionen für kopf4_haar.py]
#   (ohne)     Kopf, Haut, Haarbilder; Haar aus glb5 verknüpfen und nur die Stützpunkte und Kopfhautkarten neu
#   ohne_haar  nur Kopf, Haut und Haarbilder
#   nur_kopf   nur Kopf (Formen) und Haarbilder, die Haut bleibt (gleiches Netz)
#   nur_haar   nur das Haar wie (ohne): Verknüpfen, Stützpunkte und Kopfhautkarten neu (nur_kopfhaut=1)
#   kaemmen    Haar neu kämmen (≈ 60 min CPU, Ende Etappe B); verweigert, solange in glb6/haar4 Verknüpfungen nach glb5
#              liegen – sonst würden die Dateien der Phase 5 überschrieben. Vorher: kopf6.sh haar_kopieren
#   haar_kopieren  die Verknüpfungen in glb6/haar4 durch eigene Kopien ersetzen (vor dem Neukämmen)
# Umgebung: GLB (Ausgabe, Standard ~/werkzeuge/charakter/glb6), GLB5 (Haar-Quelle, Standard ~/werkzeuge/charakter/glb5),
#           MAKRO_NORMALEN=1: auch makro_ (Alter × Geschlecht) behält die Normalen – nur nach sauberem A/B (S2, Bogen P7)
HIER=$(cd "$(dirname "$0")" && pwd)
AUS=${GLB:-$HOME/werkzeuge/charakter/glb6}
QUELLE5=${GLB5:-$HOME/werkzeuge/charakter/glb5}
ARBEIT=$HOME/werkzeuge/charakter/arbeit/phase6
BPY=$HOME/werkzeuge/blender/blender-5.2.2-linux-x64/5.2/python/bin/python3.13
mkdir -p "$AUS" "$ARBEIT" "$AUS/kopf6_tex"
MODUS=$1
case "$MODUS" in ohne_haar|nur_kopf|nur_haar|kaemmen|haar_kopieren) shift ;; *) MODUS="" ;; esac

verknuepft() { find "$AUS/haar4" -maxdepth 1 -type l 2>/dev/null | head -1; }

if [ "$MODUS" = "haar_kopieren" ]; then
  for l in $(find "$AUS/haar4" -maxdepth 1 -type l); do cp --remove-destination "$(readlink -f "$l")" "$l"; done
  echo "Verknüpfungen ersetzt: $(find "$AUS/haar4" -maxdepth 1 -type l | wc -l) übrig"
  exit 0
fi
if [ "$MODUS" = "kaemmen" ] && [ -n "$(verknuepft)" ]; then
  echo "VERWEIGERT: $AUS/haar4 enthält Verknüpfungen nach $QUELLE5/haar4 (z. B. $(verknuepft))."
  echo "Neu kämmen würde die Dateien der Phase 5 überschreiben. Erst: kopf6.sh haar_kopieren"
  exit 2
fi

python3 "$HIER/warte_last.py" 20 20 > /dev/null
if [ "$MODUS" != "nur_haar" ] && [ "$MODUS" != "kaemmen" ]; then
  nice -n 10 "$HIER/blender.sh" -b -P "$HIER/kopf2_bauen.py" -- "$AUS/kopf6_mn.glb" morph_normalen=1 \
    backdaten="$ARBEIT/backdaten.npz" 2>&1 | grep -E "^FERTIG|^\[ |Traceback|Error|^PRUEFEN" | cut -c1-200
  OHNE=makro_
  [ "${MAKRO_NORMALEN:-0}" = "1" ] && OHNE=
  nice -n 10 python3 -I "$HIER/glb_formnormalen.py" "$AUS/kopf6_mn.glb" "$AUS/kopf6.glb" ohne_vorsilben=$OHNE duenn=1
  python3 -I -c 'import json, os, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
d["bericht"]["ausgabe"] = "kopf6.glb"
d["bericht"]["morph_normalen"] = "alle" if sys.argv[3] == "" else "ohne " + sys.argv[3]
d["bericht"]["formdaten"] = "dünn (|Δ| > 1e-4)"
d["bericht"]["glb_mb"] = round(os.path.getsize(sys.argv[2][:-5] + ".glb") / 1e6, 2)
d["bericht"]["glb_mn_mb"] = round(os.path.getsize(sys.argv[1][:-5] + ".glb") / 1e6, 2)
json.dump(d, open(sys.argv[2], "w", encoding="utf-8"), indent=1, ensure_ascii=False)' "$AUS/kopf6_mn.json" "$AUS/kopf6.json" "$OHNE"
  [ "$MODUS" = "nur_kopf" ] || nice -n 10 "$HIER/blender.sh" -b --factory-startup -P "$HIER/kopf3_backen.py" -- "$ARBEIT/backdaten.npz" "$AUS/kopf6_tex" 2>&1 \
    | grep -E "^FERTIG|^\[ |Traceback|Error" | cut -c1-200
  nice -n 10 "$BPY" -I "$HIER/kopf3_haartexturen.py" "$AUS/kopf6_tex" 2>&1 | tail -3
fi
if [ "$MODUS" != "ohne_haar" ] && [ "$MODUS" != "nur_kopf" ]; then
  mkdir -p "$AUS/haar4"
  if [ "$MODUS" = "kaemmen" ]; then
    nice -n 10 "$HIER/blender.sh" -b --factory-startup -P "$HIER/kopf4_haar.py" -- "$AUS" backdaten="$ARBEIT/backdaten.npz" \
      kopf=kopf6_mn.glb bilder=1 "$@" 2>&1 | grep -E "^\[|Traceback|Error" | cut -c1-220
  else
    # Nur die Strähnen (<stil>.json/.bin) und das Kartenbild der Phase 5 verknüpfen – nie stuetzen.*, die Kopfhautkarten
    # oder die Übersicht (die schreibt kopf4_haar.py neu bzw. wird kopiert, sonst landeten sie in glb5)
    for f in "$QUELLE5"/haar4/*.json "$QUELLE5"/haar4/*.bin "$QUELLE5"/haar4/haar_karten.png; do
      n=$(basename "$f")
      case "$n" in stuetzen.*|haar4.json|kopfhaut*|bart*.png|rang.png|uebersicht*) continue ;; esac
      [ -e "$AUS/haar4/$n" ] && [ ! -L "$AUS/haar4/$n" ] && continue
      ln -sfn "$f" "$AUS/haar4/$n"
    done
    [ -f "$AUS/haar4/haar4.json" ] || cp "$QUELLE5/haar4/haar4.json" "$AUS/haar4/haar4.json"
    nice -n 10 "$HIER/blender.sh" -b --factory-startup -P "$HIER/kopf4_haar.py" -- "$AUS" backdaten="$ARBEIT/backdaten.npz" \
      kopf=kopf6_mn.glb nur_kopfhaut=1 "$@" 2>&1 | grep -E "^\[|Traceback|Error" | cut -c1-220
  fi
fi
ls -la "$AUS" 2>/dev/null | head -20
echo "haar4: $(ls "$AUS/haar4" 2>/dev/null | wc -l) Dateien, davon $(find "$AUS/haar4" -maxdepth 1 -type l 2>/dev/null | wc -l) Verknüpfungen"
