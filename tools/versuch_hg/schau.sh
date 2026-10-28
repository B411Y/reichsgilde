#!/bin/bash
# HG-Testszene unter eigenem Xvfb (:95) mit Vulkan auf der NVIDIA T400 starten.
# Aufruf: schau.sh <bildordner> <Auflösung 1280x720> [Godot-Nutzerargumente...]; GODOT_EXTRA z. B. "--write-movie x.avi --fixed-fps 30"
B=$1; RES=${2:-1280x720}; shift 2
P=$(cd "$(dirname "$0")/../../game" && pwd)
X=~/werkzeuge/xvfb
G=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
export LD_LIBRARY_PATH=$X/root/usr/lib/x86_64-linux-gnu:$X/root/lib/x86_64-linux-gnu
export LIBGL_DRIVERS_PATH=$X/root/usr/lib/x86_64-linux-gnu/dri
export XKB_CONFIG_ROOT=$X/root/usr/share/X11/xkb
export VK_ICD_FILENAMES=$HOME/werkzeuge/vulkan/nvidia_icd.json
mkdir -p /tmp/xkb "$B"; ln -sf $X/root/usr/bin/xkbcomp /tmp/xkb/xkbcomp
D=${ANZEIGE:-:93}
$X/Xvfb-gepatcht $D -screen 0 ${RES}x24 -xkbdir $X/root/usr/share/X11/xkb -nolisten tcp +extension GLX > "$B/xvfb.log" 2>&1 &
XPID=$!
sleep 2
DISPLAY=$D timeout ${ZEIT:-300} $G --rendering-driver vulkan --path "$P" --resolution $RES res://versuch/hg/hg_schau.tscn $GODOT_EXTRA -- --bilder="$B" "$@" > "$B/godot.log" 2>&1
kill $XPID
