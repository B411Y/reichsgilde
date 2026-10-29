#!/bin/bash
# Schnelle Syntaxprobe der Testszene (headless, ohne Grafikkarte): meldet Parse-Fehler in wenigen Sekunden.
P=$(cd "$(dirname "$0")/../../game" && pwd)
G=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
timeout 40 $G --headless --path "$P" res://versuch/charakter/charakter_schau.tscn -- --modus=keiner --glb=/nirgends --ende 2>&1 \
  | grep -E "SCRIPT ERROR|Parse Error|at: GDScript|MELDUNG" | head -20
