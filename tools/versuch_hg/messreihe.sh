#!/bin/bash
# Messung mehrfach wiederholen und den Median der Bildrate ausgeben (T400 schwankt durch Wärme und Fremdlast).
# Aufruf: messreihe.sh <ordner> <wiederholungen> <schau.sh-Argumente ohne Ordner/Auflösung…>
B=$1; N=$2; shift 2; H=$(cd "$(dirname "$0")" && pwd)
werte=()
for i in $(seq 1 $N); do
  "$H/schau.sh" "$B/lauf$i" 1600x900 "$@" --messzeit=20 godot:--disable-vsync zeit:150 >/dev/null 2>&1
  w=$(grep -m1 MESS "$B/lauf$i/godot.log" | grep -o '[0-9.]* Bilder' | cut -d' ' -f1)
  werte+=("${w:-0}")
  echo "  Lauf $i: $w fps | Last $(cut -d' ' -f1 /proc/loadavg) | $(grep -m1 TRESSFX "$B/lauf$i/godot.log" | cut -c1-160)"
done
echo "MEDIAN $(basename "$B"): $(printf '%s\n' "${werte[@]}" | sort -n | awk '{a[NR]=$1} END {print a[int((NR+1)/2)]}') fps"
