#!/bin/bash
# Charakter Phase 4: Basiskopf mit neuen Brauen und Wimpern bauen, Haut backen, Haarbilder zeichnen (wie kopf3.sh) und
# dazu Haupthaar und Bart (kopf4_haar.py) nach ~/werkzeuge/charakter/glb4 (nicht im Repository). Der Stand der Phase 3
# bleibt in glb3 (zum Vergleich).
# Aufruf: kopf4.sh [nur_haar|ohne_haar] [weitere Optionen für kopf4_haar.py, z. B. faktor=0.4 vorschau=<ordner>]
#   nur_haar   baut nur Haar und Bart neu (Kopf und Haut aus dem letzten Lauf)
#   ohne_haar  baut nur Kopf, Haut und Haarbilder
HIER=$(cd "$(dirname "$0")" && pwd)
AUS=${GLB:-$HOME/werkzeuge/charakter/glb4}
ARBEIT=$HOME/werkzeuge/charakter/arbeit/phase4
mkdir -p "$AUS" "$ARBEIT" "$AUS/kopf4_tex"
python3 "$HIER/warte_last.py" 20 20 > /dev/null
MODUS=$1
[ "$MODUS" = "nur_haar" ] || [ "$MODUS" = "ohne_haar" ] && shift
if [ "$MODUS" != "nur_haar" ]; then
  nice -n 10 "$HIER/blender.sh" -b -P "$HIER/kopf2_bauen.py" -- "$AUS/kopf4_mn.glb" morph_normalen=1 \
    backdaten="$ARBEIT/backdaten.npz" 2>&1 | grep -E "^FERTIG|^\[ |Traceback|Error|^PRUEFEN" | cut -c1-200
  python3 -I "$HIER/glb_formnormalen.py" "$AUS/kopf4_mn.glb" "$AUS/kopf4.glb" ohne_vorsilben=makro_,gen_
  python3 -I -c 'import json, os, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
d["bericht"]["morph_normalen"] = "nur_mimik"
d["bericht"]["glb_mb"] = round(os.path.getsize(sys.argv[2][:-5] + ".glb") / 1e6, 2)
json.dump(d, open(sys.argv[2], "w", encoding="utf-8"), indent=1, ensure_ascii=False)' "$AUS/kopf4_mn.json" "$AUS/kopf4.json"
  nice -n 10 "$HIER/blender.sh" -b --factory-startup -P "$HIER/kopf3_backen.py" -- "$ARBEIT/backdaten.npz" "$AUS/kopf4_tex" 2>&1 \
    | grep -E "^FERTIG|^\[ |Traceback|Error" | cut -c1-200
  BPY=$HOME/werkzeuge/blender/blender-5.2.2-linux-x64/5.2/python/bin/python3.13
  nice -n 10 "$BPY" -I "$HIER/kopf3_haartexturen.py" "$AUS/kopf4_tex" 2>&1 | tail -3
fi
if [ "$MODUS" != "ohne_haar" ]; then
  nice -n 10 "$HIER/blender.sh" -b --factory-startup -P "$HIER/kopf4_haar.py" -- "$AUS" backdaten="$ARBEIT/backdaten.npz" \
    kopf=kopf4_mn.glb bilder=1 "$@" 2>&1 | grep -E "^\[|Traceback|Error" | cut -c1-220
fi
ls -la "$AUS" "$AUS/haar4" 2>/dev/null | head -40
