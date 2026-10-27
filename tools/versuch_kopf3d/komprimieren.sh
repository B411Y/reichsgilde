#!/bin/bash
# Texturen im Ordner auf VRAM-Kompression stellen und neu importieren: komprimieren.sh <ordner unter game/>
# Plan D: nur der Atlas wird komprimiert; Porträt und Augenmaske bleiben verlustfrei (Schärfe der Augen).
cd /home/claude/projekte/rg-3d
G=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
$G --headless --import --path game >/dev/null 2>&1
for f in game/$1/*.import; do
  case "$f" in *_portraet*|*_augen.png.import) continue;; esac
  sed -i 's/compress\/mode=0/compress\/mode=2/' $f
done
$G --headless --import --path game >/dev/null 2>&1
