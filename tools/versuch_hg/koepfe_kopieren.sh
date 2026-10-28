#!/bin/bash
# GLBs und Hautvarianten aus ~/werkzeuge/humgen/arbeit/<name>/ nach game/versuch/hg/koepfe/ kopieren (nicht im Repository).
Z=$(cd "$(dirname "$0")/../../game/versuch/hg" && pwd)/koepfe; mkdir -p "$Z"
for n in "$@"; do
  A=~/werkzeuge/humgen/arbeit/$n
  cp "$A/$n.glb" "$Z/"
  for v in "$A/${n}_tex"/haut_farbe_*.jpg; do [ -f "$v" ] && cp "$v" "$Z/${n}_haut_$(basename "$v" .jpg | sed 's/haut_farbe_//').jpg"; done
done
ls "$Z"
