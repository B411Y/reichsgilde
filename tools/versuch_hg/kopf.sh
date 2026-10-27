#!/bin/bash
# Stufe B aufrufen. kopf.sh <mensch_a.blend> <ziel.glb> [schnell] [weitere kopf.json-Werte als JSON, z. B. '{"formen":{…}}']
# „schnell“: vorhandene Haar-/Augen-/Zahntexturen wiederverwenden (nur die Haut wird neu gebacken).
H=$(dirname "$0"); A=$1; Z=$2; MODUS=$3; EXTRA=${4:-"{}"}
T=$(mktemp --suffix=.json)
python3 -c "
import json,sys
d=json.load(open('$H/kopf.json')); d.update(json.loads(sys.argv[1]))
d['neu_backen'] = '$MODUS' != 'schnell'
json.dump(d,open('$T','w'))" "$EXTRA"
[ "$MODUS" = schnell ] && rm -f "${Z%.glb}_tex"/haut_*
~/werkzeuge/humgen/blender_hg.sh -b "$A" -P "$H/hg_kopf.py" -- "$T" "$Z" 2>&1 | grep -E "HG-KOPF|Error|Traceback|rror|line [0-9]" | grep -v "Vertex not in group"
rm -f "$T"
# Normalen der Formen ausdünnen (Datei etwa halbiert)
~/werkzeuge/humgen/blender-4.5/blender-4.5.15-linux-x64/4.5/python/bin/python3.11 -I "$H/glb_schlank.py" "$Z" "$Z.tmp" 0.03 && mv "$Z.tmp" "$Z"
