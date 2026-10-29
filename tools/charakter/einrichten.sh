#!/bin/bash
# Einmalig: MPFB-2-Code und Asset-Pakete (CC0) aus ~/werkzeuge/charakter/dl entpacken und MPFB in einem eigenen
# Blender-Benutzerordner einschalten. Nichts davon kommt ins Repository.
#   ~/werkzeuge/charakter/mpfb_code   MPFB 2.0.18 (GPL, nur als Werkzeug)
#   ~/werkzeuge/charakter/mpfb_daten  Asset-Pakete (data/skins, data/targets/faceunits, ...)
set -e
CH=${CHARAKTER:-$HOME/werkzeuge/charakter}
HIER=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$CH/mpfb_daten/data" "$CH/blender_user/extensions/user_default"
for z in "$CH"/dl/*.zip; do
  echo "entpacke $(basename "$z")"
  python3 -I -c 'import sys, zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])' "$z" "$CH/mpfb_daten/data"
done
ln -sfn "$(ls -d "$CH"/mpfb_code/mpfb2-*/src/mpfb | head -1)" "$CH/blender_user/extensions/user_default/mpfb"
"$HIER/blender.sh" -b --factory-startup -P "$HIER/einrichten.py" -- "$CH/mpfb_daten"
