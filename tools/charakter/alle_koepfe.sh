#!/bin/bash
# Alle Köpfe der Phase 0 bauen: MPFB-Angaben aus angaben/*.json und ICT-Varianten, Ausgabe nach ~/werkzeuge/charakter/glb
# (nicht im Repository). Vorher einmal einrichten.sh. Je Kopf 10–30 s (Blender, 3 Threads).
# Aufruf: alle_koepfe.sh [name …]   (ohne Namen: alle)
# Ein Kopf ist ein kurzer Lauf (10–30 s, 3 Threads, nice 10); gewartet wird erst ab Last 20 (schwere Läufe:
# Messungen und Renderbilder warten ab Last 12, siehe messreihe.sh und schau.sh).
HIER=$(cd "$(dirname "$0")" && pwd)
AUS=${GLB:-$HOME/werkzeuge/charakter/glb}
mkdir -p "$AUS"
namen=("$@")
[ ${#namen[@]} -eq 0 ] && namen=($(cd "$HIER/angaben" && ls *.json | sed 's/\.json$//') ict ict_n0 ict_n100)
for n in "${namen[@]}"; do
  python3 "$HIER/warte_last.py" 20 20 > /dev/null
  case $n in
    ict)      nice -n 10 "$HIER/blender.sh" -b -P "$HIER/ict_kopf.py" -- "$AUS/ict.glb" ;;
    ict_n0)   nice -n 10 "$HIER/blender.sh" -b -P "$HIER/ict_kopf.py" -- "$AUS/ict_n0.glb" mimik=keine ;;
    ict_n100) nice -n 10 "$HIER/blender.sh" -b -P "$HIER/ict_kopf.py" -- "$AUS/ict_n100.glb" identitaeten=43 ;;
    *)        nice -n 10 "$HIER/blender.sh" -b -P "$HIER/mpfb_kopf.py" -- "$HIER/angaben/$n.json" "$AUS/$n.glb" ;;
  esac 2>&1 | grep -E "^FERTIG|Traceback|Error:" | cut -c1-160 | sed "s/^/$n: /"
done
