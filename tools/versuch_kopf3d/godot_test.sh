#!/bin/bash
# Godot-Testszene des Kopfes unter Xvfb mit Vulkan (NVIDIA T400) starten; Fotos in $1.
# Aufruf: godot_test.sh <bildordner> [projektordner]
B=$1; P=${2:-/home/claude/projekte/rg-3d/game}
X=~/werkzeuge/xvfb
G=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
export LD_LIBRARY_PATH=$X/root/usr/lib/x86_64-linux-gnu:$X/root/lib/x86_64-linux-gnu
export LIBGL_DRIVERS_PATH=$X/root/usr/lib/x86_64-linux-gnu/dri
export XKB_CONFIG_ROOT=$X/root/usr/share/X11/xkb
export VK_ICD_FILENAMES=$HOME/werkzeuge/vulkan/nvidia_icd.json
mkdir -p /tmp/xkb "$B"; ln -sf $X/root/usr/bin/xkbcomp /tmp/xkb/xkbcomp
D=:93
$X/Xvfb-gepatcht $D -screen 0 1024x1024x24 -xkbdir $X/root/usr/share/X11/xkb -nolisten tcp +extension GLX > "$B/xvfb.log" 2>&1 &
XPID=$!
sleep 2
DISPLAY=$D timeout 120 $G --rendering-driver vulkan --path "$P" --resolution 768x768 res://versuch/kopf3d/kopf3d.tscn -- --bilder="$B" > "$B/godot.log" 2>&1
kill $XPID
