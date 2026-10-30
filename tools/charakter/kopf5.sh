#!/bin/bash
# Charakter Phase 5: Basiskopf mit Altersformen (erwachsen, greis), Wimpern an der Lidkante und Frauenbrauen bauen,
# Haut backen, Haarbilder zeichnen, Haupthaar und Bart (kopf4_haar.py) nach ~/werkzeuge/charakter/glb5 (nicht im
# Repository). Der Stand der Phase 4 bleibt in glb4 (Vergleich); die Haardaten heißen weiter haar4/ (gleiches Format).
# Aufruf: kopf5.sh [nur_haar|ohne_haar] [weitere Optionen für kopf4_haar.py, z. B. faktor=0.4 vorschau=<ordner>]
#   nur_haar   baut nur Haar und Bart neu (Kopf und Haut aus dem letzten Lauf)
#   ohne_haar  baut nur Kopf, Haut und Haarbilder
#   nur_kopf   baut nur den Kopf (Formen) und die Haarbilder neu, die Haut bleibt (gleiches Netz, z. B. nach einer
#              Änderung der Altersformen in kopf2_regler.json)
HIER=$(cd "$(dirname "$0")" && pwd)
AUS=${GLB:-$HOME/werkzeuge/charakter/glb5}
ARBEIT=$HOME/werkzeuge/charakter/arbeit/phase5
mkdir -p "$AUS" "$ARBEIT" "$AUS/kopf5_tex"
python3 "$HIER/warte_last.py" 20 20 > /dev/null
MODUS=$1
[ "$MODUS" = "nur_haar" ] || [ "$MODUS" = "ohne_haar" ] || [ "$MODUS" = "nur_kopf" ] && shift
if [ "$MODUS" != "nur_haar" ]; then
  nice -n 10 "$HIER/blender.sh" -b -P "$HIER/kopf2_bauen.py" -- "$AUS/kopf5_mn.glb" morph_normalen=1 \
    backdaten="$ARBEIT/backdaten.npz" 2>&1 | grep -E "^FERTIG|^\[ |Traceback|Error|^PRUEFEN" | cut -c1-200
  python3 -I "$HIER/glb_formnormalen.py" "$AUS/kopf5_mn.glb" "$AUS/kopf5.glb" ohne_vorsilben=makro_,gen_,alter_
  python3 -I -c 'import json, os, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
d["bericht"]["morph_normalen"] = "nur_mimik"
d["bericht"]["glb_mb"] = round(os.path.getsize(sys.argv[2][:-5] + ".glb") / 1e6, 2)
json.dump(d, open(sys.argv[2], "w", encoding="utf-8"), indent=1, ensure_ascii=False)' "$AUS/kopf5_mn.json" "$AUS/kopf5.json"
  [ "$MODUS" = "nur_kopf" ] || nice -n 10 "$HIER/blender.sh" -b --factory-startup -P "$HIER/kopf3_backen.py" -- "$ARBEIT/backdaten.npz" "$AUS/kopf5_tex" 2>&1 \
    | grep -E "^FERTIG|^\[ |Traceback|Error" | cut -c1-200
  BPY=$HOME/werkzeuge/blender/blender-5.2.2-linux-x64/5.2/python/bin/python3.13
  nice -n 10 "$BPY" -I "$HIER/kopf3_haartexturen.py" "$AUS/kopf5_tex" 2>&1 | tail -3
fi
if [ "$MODUS" != "ohne_haar" ] && [ "$MODUS" != "nur_kopf" ]; then
  nice -n 10 "$HIER/blender.sh" -b --factory-startup -P "$HIER/kopf4_haar.py" -- "$AUS" backdaten="$ARBEIT/backdaten.npz" \
    kopf=kopf5_mn.glb bilder=1 "$@" 2>&1 | grep -E "^\[|Traceback|Error" | cut -c1-220
fi
ls -la "$AUS" "$AUS/haar4" 2>/dev/null | head -40
