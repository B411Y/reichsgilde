#!/bin/bash
# Messreihe Phase 0 (Test 1 und 4): je Lauf ein frischer Godot-Prozess (Vulkan, T400), 1280×720, vsync aus.
# Ergebnis: <ordner>/messungen.jsonl (eine Zeile je Lauf, dazu VRAM laut nvidia-smi während des Laufs).
# Aufruf: messreihe.sh <ordner> [name …]   (ohne Namen: alle). WARTEN = Minuten, die auf eine freie T400 gewartet
# wird (Standard 5); danach wird trotzdem gemessen und die Zahl der anderen Läufe steht in der Zeile („andere_gpu“).
HIER=$(cd "$(dirname "$0")" && pwd)
O=$1; mkdir -p "$O"; O=$(cd "$O" && pwd)
shift
NUR=" $* "
laeufe=(
  "leer|--kopf=keiner"
  "a1|--kopf=mann35_a1.glb --stoff=roh"
  "a2|--kopf=mann35_a2.glb --stoff=haut"
  "a2_still|--kopf=mann35_a2.glb --stoff=haut --animiert=0"
  "b|--kopf=ict.glb --stoff=haut"
  "b_still|--kopf=ict.glb --stoff=haut --animiert=0"
  "n0|--kopf=n0.glb"
  "n30|--kopf=n30.glb"
  "n60|--kopf=n60.glb"
  "n100|--kopf=n100.glb"
  "n100_ohne_normalen|--kopf=n100_ohne_normalen.glb"
  "n100_unkomprimiert|--kopf=n100.glb --unkomprimiert"
  "ict_n0|--kopf=ict_n0.glb"
  "ict_n100|--kopf=ict_n100.glb"
  "a2_x8|--kopf=mann35_a2.glb --anzahl=8 --unkomprimiert"
  "b_x8|--kopf=ict.glb --anzahl=8"
)
for l in "${laeufe[@]}"; do
  name=${l%%|*}; arg=${l#*|}
  [ "$NUR" != "  " ] && [[ "$NUR" != *" $name "* ]] && continue
  # möglichst allein auf der T400 messen (höchstens WARTEN min warten; die Zahl der anderen Läufe steht in der Zeile)
  andere=$(python3 "$HIER/warte_last.py" 12 ${WARTEN:-5} 0 | tail -1 | sed -E 's/.*GPU-Prozesse ([0-9]+).*/\1/')
  last=$(cut -d' ' -f1 /proc/loadavg)
  vor=$(nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits)
  ( ZEIT=120 ANZEIGE=:98 "$HIER/schau.sh" "$O/$name" 1280x720 --modus=messen --name=$name --sekunden=8 $arg > "$O/$name.txt" 2>&1 ) &
  P=$!
  spitze=0
  while kill -0 $P 2>/dev/null; do
    m=$(nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits); [ "$m" -gt "$spitze" ] && spitze=$m
    python3 -c "import time; time.sleep(1)"
  done
  zeile=$(grep -h "^MESSUNG" "$O/$name.txt" | sed 's/^MESSUNG //')
  if [ -n "$zeile" ]; then
    echo "${zeile%\}}, \"nvidia_smi_mb\": $((spitze - vor)), \"andere_gpu\": $andere, \"last\": $last}" >> "$O/messungen.jsonl"
    echo "$name fertig"
  else
    echo "$name FEHLER"; tail -3 "$O/$name.txt"
  fi
done
