#!/bin/bash
# Elbform prüfen: Mann mit Form bauen, Mensch/Elb ohne Haar nebeneinander. elb_probe.sh <form.npz> <bildordner>
F=$1; B=$2; H=$(cd "$(dirname "$0")" && pwd)
cd ~/werkzeuge/humgen && "$H/kopf.sh" arbeit/mann_a.blend arbeit/mann/mann.glb schnell "{\"regler_dazu\":{\"elb\":\"$F\"}}" | tail -1
P=$H/../../game; cp arbeit/mann/mann.glb $P/versuch/hg/koepfe/mann.glb
~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64 --headless --import --path $P > /dev/null 2>&1
ZEIT=90 "$H/schau.sh" $B/m 1280x720 --kopf=res://versuch/hg/koepfe/mann.glb --modus=nah --ohne-haar 2>/dev/null
ZEIT=90 "$H/schau.sh" $B/e 1280x720 --kopf=res://versuch/hg/koepfe/mann.glb --modus=nah --ohne-haar --fest=elb:1 2>/dev/null
cd $B && ~/werkzeuge/humgen/bogen.sh vergleich.png 4 18,18,658,658 m/nah_ganz_vorn.png m/nah_seite_links.png e/nah_ganz_vorn.png e/nah_seite_links.png m/nah_augen_offen.png m/nah_mund_a.png e/nah_augen_offen.png e/nah_mund_a.png
