#!/bin/bash
# Prototyp-Szene unter eigenem Xvfb (:94) mit Vulkan (NVIDIA) starten.
# Wie proto_foto.sh, mit GODOT_EXTRA (z. B. --write-movie x.avi --fixed-fps 30). Aufruf: proto_film.sh <szene res://...> <bildordner> [Auflösung 1024x768] [Godot-Nutzerargumente...]
SZ=$1; B=$2; RES=${3:-1024x768}; shift 3
P=/home/claude/projekte/rg-3d/game
X=~/werkzeuge/xvfb
G=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
export LD_LIBRARY_PATH=$X/root/usr/lib/x86_64-linux-gnu:$X/root/lib/x86_64-linux-gnu
export LIBGL_DRIVERS_PATH=$X/root/usr/lib/x86_64-linux-gnu/dri
export XKB_CONFIG_ROOT=$X/root/usr/share/X11/xkb
export VK_ICD_FILENAMES=$HOME/werkzeuge/vulkan/nvidia_icd.json
mkdir -p /tmp/xkb "$B"; ln -sf $X/root/usr/bin/xkbcomp /tmp/xkb/xkbcomp
D=:94
$X/Xvfb-gepatcht $D -screen 0 ${RES}x24 -xkbdir $X/root/usr/share/X11/xkb -nolisten tcp +extension GLX > "$B/xvfb.log" 2>&1 &
XPID=$!
sleep 2
$G --headless --import --path "$P" >/dev/null 2>&1
DISPLAY=$D timeout 600 $G --rendering-driver vulkan --path "$P" --resolution $RES "$SZ" $GODOT_EXTRA -- --bilder="$B" "$@" > "$B/godot.log" 2>&1
kill $XPID
