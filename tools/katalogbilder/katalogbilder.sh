#!/bin/bash
# Rendert die Vorschaubilder des Katalogs (Palette der Kartenwerkstatt) nach game/assets/oberflaeche/katalog/<kennung>.png.
# Aufruf (vom Projektordner aus):
#   tools/katalogbilder/katalogbilder.sh              alle Bausteine mit Modell
#   tools/katalogbilder/katalogbilder.sh kennung,kennung   nur diese (neues Objekt nachrendern)
# Braucht Godot 4.7.2 mono und ein Fenster: unter Linux ohne Bildschirm Xvfb (Ordner ~/werkzeuge/xvfb wie in CLAUDE.md), sonst ein normales Fenster.
# Mit Grafikkarte (Vulkan) bessere Kanten: TREIBER=vulkan. Danach einmal importieren (--headless --import --path game) und die Bilder ansehen.
set -e
WURZEL=$(cd "$(dirname "$0")/../.." && pwd)
X=~/werkzeuge/xvfb
GODOT=${GODOT:-~/werkzeuge/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64}
AUSWAHL=${1:-1}
if [ -z "$DISPLAY" ] && [ -d "$X" ]; then
  export LD_LIBRARY_PATH=$X/root/usr/lib/x86_64-linux-gnu:$X/root/lib/x86_64-linux-gnu LIBGL_DRIVERS_PATH=$X/root/usr/lib/x86_64-linux-gnu/dri
  export __GLX_VENDOR_LIBRARY_NAME=mesa XKB_CONFIG_ROOT=$X/root/usr/share/X11/xkb
  mkdir -p /tmp/xkb; ln -sf $X/root/usr/bin/xkbcomp /tmp/xkb/xkbcomp
  D=:57
  $X/Xvfb-gepatcht $D -screen 0 1280x720x24 -xkbdir $X/root/usr/share/X11/xkb -nolisten tcp +extension GLX > /dev/null 2>&1 &
  XPID=$!
  trap 'kill $XPID' EXIT
  sleep 2
  export DISPLAY=$D
fi
"$GODOT" --rendering-driver "${TREIBER:-opengl3}" --path "$WURZEL/game" --resolution 1280x720 -- --kartenwerkstatt=probestadt --katalogbilder="$AUSWAHL"
