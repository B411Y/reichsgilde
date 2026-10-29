#!/bin/bash
# Charakter-Testszene unter eigenem Xvfb mit Vulkan auf der NVIDIA T400 starten (wie tools/klicktest/lauf.sh, TREIBER=nvidia).
# Aufruf: schau.sh <bildordner> <BxH> [Szenenargumente …]   z. B.
#   schau.sh ~/bilder 1800x1200 --bogen=tools/charakter/boegen2/alter_mann.json --ende
# Umgebung: ANZEIGE (Standard :97; zwei Läufe nie mit derselben Nummer), ZEIT (Sekunden, Standard 300),
#           GLB (Ordner der Köpfe, Standard ~/werkzeuge/charakter/glb2)
# Höchstens zwei Vulkan-Läufe gleichzeitig auf der T400.
B=$1; RES=${2:-1280x720}; shift 2
P=$(cd "$(dirname "$0")/../../game" && pwd)
X=~/werkzeuge/xvfb
G=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
export LD_LIBRARY_PATH=$X/root/usr/lib/x86_64-linux-gnu:$X/root/lib/x86_64-linux-gnu
export LIBGL_DRIVERS_PATH=$X/root/usr/lib/x86_64-linux-gnu/dri
export XKB_CONFIG_ROOT=$X/root/usr/share/X11/xkb
export VK_ICD_FILENAMES=$HOME/werkzeuge/vulkan/nvidia_icd.json
mkdir -p /tmp/xkb "$B"; ln -sf $X/root/usr/bin/xkbcomp /tmp/xkb/xkbcomp
B=$(cd "$B" && pwd)
D=${ANZEIGE:-:97}
# höchstens zwei Vulkan-Läufe auf der T400: warten, bis höchstens einer läuft (längstens 15 min)
python3 "$(dirname "$0")/warte_last.py" 99 15 1 > /dev/null
$X/Xvfb-gepatcht $D -screen 0 ${RES}x24 -xkbdir $X/root/usr/share/X11/xkb -nolisten tcp +extension GLX > "$B/xvfb.log" 2>&1 &
XPID=$!
sleep 2
DISPLAY=$D timeout ${ZEIT:-300} $G --rendering-driver vulkan --path "$P" --resolution $RES \
  res://versuch/charakter/charakter_schau.tscn -- --bilder="$B" --glb="${GLB:-$HOME/werkzeuge/charakter/glb2}" "$@" > "$B/godot.log" 2>&1
CODE=$?
kill $XPID
grep -E "^(MESSUNG|BOGEN|REGLER|MELDUNG|WARNUNG)|SCRIPT ERROR|Parse Error|ERROR:.*charakter" "$B/godot.log" | head -20
exit $CODE
