#!/bin/bash
# Rendert die Vorschaubilder der Kartenwahl (Hauptmenü, „Stadt wählen“) nach game/assets/oberflaeche/karten/<kennung>.png (480 x 270).
# Aufruf (vom Projektordner aus):
#   tools/kartenbilder/kartenbilder.sh                    alle Karten in game/karten/
#   tools/kartenbilder/kartenbilder.sh probestadt koeln   nur diese (nach einer Änderung der Karte nachrendern)
# Jede Karte startet als Partie mit --kartenbild=<ziel>: Kamera auf die ganze Karte, ohne Oberfläche, Mittag im Sommer bei klarem Wetter;
# nach 6 s speichert das Spiel das Bild und beendet sich. Braucht Godot 4.7.2 mono und ein Fenster: unter Linux ohne Bildschirm Xvfb
# (Ordner ~/werkzeuge/xvfb wie in CLAUDE.md). Gerendert wird mit Vulkan (Forward+ wie das Spiel; opengl3 zeigt Häuser ohne Texturen):
# TREIBER=nvidia (Standard, Grafikkarte über ~/werkzeuge/vulkan/nvidia_icd.json), TREIBER=vulkan (Software, lvp.json), TREIBER=opengl3 nur Notbehelf. Danach einmal importieren
# (--headless --import --path game) und die Bilder ansehen. Selbst gebaute Karten der Kartenwerkstatt bekommen keine Vorschau (Platzhalter).
set -e
WURZEL=$(cd "$(dirname "$0")/../.." && pwd)
X=~/werkzeuge/xvfb
GODOT=${GODOT:-~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64}
if [ $# -gt 0 ]; then KARTEN="$@"; else KARTEN=$(cd "$WURZEL/game/karten" && ls *.json | sed 's/\.json$//'); fi
if [ -z "$DISPLAY" ] && [ -d "$X" ]; then
  export LD_LIBRARY_PATH=$X/root/usr/lib/x86_64-linux-gnu:$X/root/lib/x86_64-linux-gnu LIBGL_DRIVERS_PATH=$X/root/usr/lib/x86_64-linux-gnu/dri
  export __GLX_VENDOR_LIBRARY_NAME=mesa XKB_CONFIG_ROOT=$X/root/usr/share/X11/xkb
  mkdir -p /tmp/xkb; ln -sf $X/root/usr/bin/xkbcomp /tmp/xkb/xkbcomp
  D=${KARTENBILD_ANZEIGE:-:58}
  $X/Xvfb-gepatcht $D -screen 0 1600x900x24 -xkbdir $X/root/usr/share/X11/xkb -nolisten tcp +extension GLX > /dev/null 2>&1 &
  XPID=$!
  trap 'kill $XPID' EXIT
  sleep 2
  export DISPLAY=$D
fi
TREIBER=${TREIBER:-nvidia}
if [ "$TREIBER" = nvidia ]; then export VK_ICD_FILENAMES=$HOME/werkzeuge/vulkan/nvidia_icd.json; unset __GLX_VENDOR_LIBRARY_NAME; TREIBER=vulkan; fi
if [ "$TREIBER" = vulkan ] && [ -z "$VK_ICD_FILENAMES" ]; then export VK_ICD_FILENAMES=$HOME/werkzeuge/vulkan/lvp.json; export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:$HOME/werkzeuge/vulkan/root/usr/lib/x86_64-linux-gnu; fi
mkdir -p "$WURZEL/game/assets/oberflaeche/karten"
for K in $KARTEN; do
  "$GODOT" --rendering-driver "$TREIBER" --path "$WURZEL/game" --resolution 1600x900 -- \
    --karte=$K --startstunde=12 --jahreszeit=sommer --wetter=klar --kartenbild="$WURZEL/game/assets/oberflaeche/karten/$K.png" | grep -i "kartenbild"
done
