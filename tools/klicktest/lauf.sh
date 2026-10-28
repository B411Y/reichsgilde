#!/bin/bash
# Klicktest: startet Xvfb und das Spiel, führt Aktionen aus und macht Bildschirmfotos.
#
# Aufruf:  WORKTREE=<projektordner> [DISPLAY_NR=93] [TREIBER=nvidia|vulkan|opengl3] [DATEN=<ordner>] \
#          tools/klicktest/lauf.sh <ausgabeordner> <wartesekunden> "<aktionen>" -- <godot-optionen>
#
# Umgebungsvariablen:
#   WORKTREE     Ordner des Repositorys/Worktrees (enthält game/); Standard: Ordner über tools/
#   DISPLAY_NR   Nummer des virtuellen Bildschirms; Standard 93. Zwei Läufe NIE gleichzeitig mit
#                derselben Nummer (sonst stören sie sich) – jedem Mitglied eine eigene Nummer geben.
#   TREIBER      opengl3 (Standard), vulkan (Mesa-Software) oder nvidia (Vulkan auf der Grafikkarte).
#                opengl3 zeigt Gebäude untexturiert – für Bilder mit Texturen, Schatten und
#                Kantenglättung Vulkan nehmen.
#   DATEN        Ablageort für Spieldaten/Spielstände (XDG_DATA_HOME); Standard <ausgabeordner>/daten
#
# Aktionen (durch ; getrennt):
#   m x y            Maus bewegen
#   w sek            warten (Tooltips brauchen ca. 10 s Hover)
#   k taste          Taste drücken (xdotool-Name, z. B. Escape, space, F5)
#   b knopf          Mausknopf klicken (1 links, 3 rechts)
#   d x1 y1 x2 y2    mit gedrückter linker Taste ziehen
#   s name           Bildschirmfoto <ausgabeordner>/<name>.png
# Beispiel: DISPLAY_NR=94 tools/klicktest/lauf.sh /tmp/k 20 "m 800 450;b 1;w 2;s menue" -- --karte=probestadt
# Die Bilder danach selbst ansehen (Read-Werkzeug) und beschreiben.
X=~/werkzeuge/xvfb
WORKTREE=${WORKTREE:-$(cd "$(dirname "$0")/../.." && pwd)}
DISPLAY_NR=${DISPLAY_NR:-93}
GODOT=~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
export LD_LIBRARY_PATH=$X/root/usr/lib/x86_64-linux-gnu:$X/root/lib/x86_64-linux-gnu
export LIBGL_DRIVERS_PATH=$X/root/usr/lib/x86_64-linux-gnu/dri
export __GLX_VENDOR_LIBRARY_NAME=mesa
export XKB_CONFIG_ROOT=$X/root/usr/share/X11/xkb
if [ "$TREIBER" = nvidia ]; then
  export VK_ICD_FILENAMES=$HOME/werkzeuge/vulkan/nvidia_icd.json
  unset __GLX_VENDOR_LIBRARY_NAME
  TREIBER=vulkan
fi
mkdir -p /tmp/xkb; ln -sf $X/root/usr/bin/xkbcomp /tmp/xkb/xkbcomp
OUT=$1; WARTEN=$2; AKT=$3; shift 4
export XDG_DATA_HOME=${DATEN:-$OUT/daten}
mkdir -p "$OUT"
$X/Xvfb-gepatcht :$DISPLAY_NR -screen 0 1600x900x24 -xkbdir $X/root/usr/share/X11/xkb -nolisten tcp +extension GLX > "$OUT/xvfb.log" 2>&1 &
XPID=$!
sleep 2
DISPLAY=:$DISPLAY_NR $GODOT --rendering-driver ${TREIBER:-opengl3} --path "$WORKTREE/game" --resolution 1600x900 -- "$@" > "$OUT/godot.log" 2>&1 &
GPID=$!
sleep $WARTEN
XD="env DISPLAY=:$DISPLAY_NR $X/root/usr/bin/xdotool"
IFS=';' read -ra A <<< "$AKT"
for a in "${A[@]}"; do
  set -- $a
  case $1 in
    m) $XD mousemove $2 $3;;
    w) sleep $2;;
    k) $XD key $2;;
    b) $XD click $2;;
    d) $XD mousemove $2 $3 mousedown 1; sleep 0.3; $XD mousemove $(( ($2+$4)/2 )) $(( ($3+$5)/2 )); sleep 0.3; $XD mousemove $4 $5; sleep 0.3; $XD mouseup 1;;
    s) DISPLAY=:$DISPLAY_NR $X/root/usr/bin/xwd -root -silent > "$OUT/$2.xwd"; python3 $X/skripte/xwd2png.py "$OUT/$2.xwd" "$OUT/$2.png"; rm "$OUT/$2.xwd";;
  esac
done
kill $GPID; sleep 1; kill $XPID
