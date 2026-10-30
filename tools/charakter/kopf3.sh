#!/bin/bash
# Charakter Phase 3: Basiskopf bauen und die Haut backen (Blender headless, höchstens 4 Threads) nach
# ~/werkzeuge/charakter/glb3 (nicht im Repository). Vorher einmal tools/charakter/einrichten.sh (MPFB 2 und die
# Asset-Pakete). Phase 2 hieß das Skript kopf2.sh; der Bau (kopf2_bauen.py) ist derselbe, jetzt mit eigenen Brauen
# und Wimpern, Augen-UV, Hornhaut und Lidschatten.
# Gebaut wird einmal mit Normalen der Formen; daraus entstehen ohne Blender:
#   kopf3.glb       Normalen nur bei der Mimik (Standard, etwa 31 MB)
#   kopf3_ohne.glb  ohne Normalen der Formen (etwa 15 MB, zum Vergleich)
#   kopf3_mn.glb    alle Formen mit Normalen (etwa 50 MB, zum Vergleich)
# Je .glb eine Beschreibung <name>.json (Regler, Makros, Maße, Bericht), die die Testszene liest.
# Danach: Haut backen (kopf3_backen.py: Farbe, Normalen, Masken, Falten nach kopf3_tex/) und die Haarbilder
# zeichnen (kopf3_haartexturen.py).
# Aufruf: kopf3.sh [vorschau|nur_backen]
#   vorschau    legt zusätzlich Workbench-Bilder nach ~/werkzeuge/charakter/arbeit/phase3/vorschau
#   nur_backen  baut nicht neu, backt nur die Bilder (Backdaten aus dem letzten Bau)
HIER=$(cd "$(dirname "$0")" && pwd)
AUS=${GLB:-$HOME/werkzeuge/charakter/glb3}
ARBEIT=$HOME/werkzeuge/charakter/arbeit/phase3
mkdir -p "$AUS" "$ARBEIT" "$AUS/kopf3_tex"
python3 "$HIER/warte_last.py" 20 20 > /dev/null
if [ "$1" != "nur_backen" ]; then
  zusatz=()
  [ "$1" = "vorschau" ] && zusatz=(vorschau="$ARBEIT/vorschau")
  nice -n 10 "$HIER/blender.sh" -b -P "$HIER/kopf2_bauen.py" -- "$AUS/kopf3_mn.glb" morph_normalen=1 \
    backdaten="$ARBEIT/backdaten.npz" "${zusatz[@]}" 2>&1 | grep -E "^FERTIG|^\[ |Traceback|Error|^PRUEFEN" | cut -c1-200
  python3 -I "$HIER/glb_formnormalen.py" "$AUS/kopf3_mn.glb" "$AUS/kopf3.glb" ohne_vorsilben=makro_,gen_
  python3 -I "$HIER/glb_formnormalen.py" "$AUS/kopf3_mn.glb" "$AUS/kopf3_ohne.glb" ohne_vorsilben=
  for v in kopf3:nur_mimik kopf3_ohne:keine; do
    python3 -I -c 'import json, os, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
d["bericht"]["morph_normalen"] = sys.argv[3]
d["bericht"]["glb_mb"] = round(os.path.getsize(sys.argv[2][:-5] + ".glb") / 1e6, 2)
json.dump(d, open(sys.argv[2], "w", encoding="utf-8"), indent=1, ensure_ascii=False)' \
      "$AUS/kopf3_mn.json" "$AUS/${v%%:*}.json" "${v#*:}"
  done
fi
# Bilder: Haut (Blender wegen BVH), Haare (nur numpy)
nice -n 10 "$HIER/blender.sh" -b --factory-startup -P "$HIER/kopf3_backen.py" -- "$ARBEIT/backdaten.npz" "$AUS/kopf3_tex" 2>&1 \
  | grep -E "^FERTIG|^\[ |Traceback|Error" | cut -c1-200
BPY=$HOME/werkzeuge/blender/blender-5.2.2-linux-x64/5.2/python/bin/python3.13
nice -n 10 "$BPY" -I "$HIER/kopf3_haartexturen.py" "$AUS/kopf3_tex" 2>&1 | tail -3
ls -la "$AUS" "$AUS/kopf3_tex"
