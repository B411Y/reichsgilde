#!/bin/bash
# GLB ins Projekt kopieren, importieren, Nahaufnahmen machen und zwei Kontaktbögen bauen.
# Aufruf: nah.sh <quelle.glb> <name> <bildordner> [weitere Nutzerargumente]
Q=$1; N=$2; B=$3; shift 3
P=$(cd "$(dirname "$0")/../../game" && pwd)
G=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
mkdir -p "$P/versuch/hg/koepfe" "$B"
cp "$Q" "$P/versuch/hg/koepfe/$N.glb"
T0=$(date +%s.%N); $G --headless --import --path "$P" > "$B/import.log" 2>&1; echo "IMPORT $(echo "$(date +%s.%N) - $T0" | python3 -c "print(round(eval(input()),1))") s"
ZEIT=90 "$(dirname "$0")/schau.sh" "$B" 1280x720 --kopf=res://versuch/hg/koepfe/$N.glb --modus=nah "$@" 2>/dev/null
grep -E "SCRIPT ERROR" -A2 "$B/godot.log" | head
cd "$B"
~/werkzeuge/humgen/bogen.sh ganz.png 3 18,18,658,658 nah_ganz_vorn.png nah_ganz_laecheln.png nah_seite_links.png nah_seite_rechts.png nah_hinten.png nah_oben.png
~/werkzeuge/humgen/bogen.sh mund.png 4 18,18,658,658 nah_mund_ruhe.png nah_mund_a.png nah_mund_e.png nah_mund_i.png nah_mund_o.png nah_mund_u.png nah_mund_m.png nah_mund_f.png
~/werkzeuge/humgen/bogen.sh augen.png 4 18,18,658,658 nah_augen_offen.png nah_augen_zu.png nah_augen_breit.png nah_augen_boese.png nah_mund_weit.png nah_mund_laecheln.png nah_mund_l.png nah_ganz_vorn.png
