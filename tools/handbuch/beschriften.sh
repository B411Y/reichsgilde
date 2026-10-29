#!/bin/bash
# Beschriftet die Rohbilder des Spielbuch-Handbuchs: tools/handbuch/bilder.json + Rohbilder (PNG) -> docs/bilder/handbuch/*.webp
# Aufruf (vom Projektordner aus):  tools/handbuch/beschriften.sh [rohordner]     (Standard: /tmp/handbuch-roh)
# Die Rohbilder entstehen mit tools/handbuch/aufnehmen.sh. Braucht Godot 4.7.2 mono und ein Fenster (Xvfb, Ordner ~/werkzeuge/xvfb wie in CLAUDE.md);
# die Ziffern setzt die Schrift des Spiels. Danach die Bilder ansehen: sitzt jede Nummer an der richtigen Stelle?
set -e
WURZEL=$(cd "$(dirname "$0")/../.." && pwd)
X=~/werkzeuge/xvfb
GODOT=${GODOT:-~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64}
ROH=${1:-/tmp/handbuch-roh}
export LD_LIBRARY_PATH=$X/root/usr/lib/x86_64-linux-gnu:$X/root/lib/x86_64-linux-gnu LIBGL_DRIVERS_PATH=$X/root/usr/lib/x86_64-linux-gnu/dri
export __GLX_VENDOR_LIBRARY_NAME=mesa XKB_CONFIG_ROOT=$X/root/usr/share/X11/xkb
mkdir -p /tmp/xkb; ln -sf $X/root/usr/bin/xkbcomp /tmp/xkb/xkbcomp
D=${HANDBUCH_ANZEIGE:-:61}
$X/Xvfb-gepatcht $D -screen 0 1600x900x24 -xkbdir $X/root/usr/share/X11/xkb -nolisten tcp +extension GLX > /dev/null 2>&1 &
XPID=$!
trap 'kill $XPID' EXIT
sleep 2
DISPLAY=$D timeout 300 "$GODOT" --rendering-driver opengl3 --path "$WURZEL/game" --resolution 1600x900 \
  --script "$WURZEL/tools/handbuch/beschriften.gd" -- "$WURZEL/tools/handbuch/bilder.json" "$ROH" "$WURZEL/docs/bilder/handbuch" 2>&1 | grep -E "webp|ERROR|SCRIPT"
