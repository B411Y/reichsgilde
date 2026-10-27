#!/bin/bash
# Filme der HG-Köpfe (Godot Movie Maker, MJPEG-AVI). film.sh <bildordner> <name=res://…glb[@form:wert]> …
B=$1; shift; H=$(cd "$(dirname "$0")" && pwd)
for e in "$@"; do
  N=${e%%=*}; K=${e#*=}
  GODOT_EXTRA="--write-movie $B/$N.avi --fixed-fps 30" ZEIT=600 "$H/schau.sh" "$B/log_$N" 1600x900 "--kopf=$K" --modus=film 2>/dev/null
  ls -la "$B/$N.avi"
done
