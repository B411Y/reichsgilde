#!/bin/bash
# Charakter Phase 2: Basiskopf bauen (Blender headless, höchstens 4 Threads) nach ~/werkzeuge/charakter/glb2
# (nicht im Repository). Vorher einmal tools/charakter/einrichten.sh (MPFB 2 und die Asset-Pakete).
# Gebaut wird einmal mit Normalen der Formen; daraus entstehen ohne Blender:
#   kopf2.glb       Normalen nur bei der Mimik (Standard, etwa 32 MB)
#   kopf2_ohne.glb  ohne Normalen der Formen (etwa 16 MB, zum Vergleich)
#   kopf2_mn.glb    alle Formen mit Normalen (etwa 50 MB, zum Vergleich)
# Je .glb eine Beschreibung <name>.json (Regler, Makros, Maße, Bericht), die die Testszene liest.
# Aufruf: kopf2.sh [vorschau]   – „vorschau“ legt zusätzlich Workbench-Bilder nach ~/werkzeuge/charakter/arbeit/phase2/vorschau
HIER=$(cd "$(dirname "$0")" && pwd)
AUS=${GLB:-$HOME/werkzeuge/charakter/glb2}
mkdir -p "$AUS"
python3 "$HIER/warte_last.py" 20 20 > /dev/null
zusatz=()
[ "$1" = "vorschau" ] && zusatz=(vorschau="$HOME/werkzeuge/charakter/arbeit/phase2/vorschau")
nice -n 10 "$HIER/blender.sh" -b -P "$HIER/kopf2_bauen.py" -- "$AUS/kopf2_mn.glb" morph_normalen=1 "${zusatz[@]}" 2>&1 \
  | grep -E "^FERTIG|^\[ |Traceback|Error" | cut -c1-200
python3 -I "$HIER/glb_formnormalen.py" "$AUS/kopf2_mn.glb" "$AUS/kopf2.glb" ohne_vorsilben=makro_,gen_
python3 -I "$HIER/glb_formnormalen.py" "$AUS/kopf2_mn.glb" "$AUS/kopf2_ohne.glb" ohne_vorsilben=
for v in kopf2:nur_mimik kopf2_ohne:keine; do
  python3 -I -c 'import json, os, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
d["bericht"]["morph_normalen"] = sys.argv[3]
d["bericht"]["glb_mb"] = round(os.path.getsize(sys.argv[2][:-5] + ".glb") / 1e6, 2)
json.dump(d, open(sys.argv[2], "w", encoding="utf-8"), indent=1, ensure_ascii=False)' \
    "$AUS/kopf2_mn.json" "$AUS/${v%%:*}.json" "${v#*:}"
done
ls -la "$AUS"
