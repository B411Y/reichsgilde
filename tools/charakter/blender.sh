#!/bin/sh
# Blender 5.2.2 headless mit eigenem Benutzerordner für die Charakter-Versuche (MPFB 2 als Erweiterung).
# Fremddaten (MPFB-Code und -Assets, ICT) liegen nur unter ~/werkzeuge/charakter/ und nie im Repository.
# Höchstens 3 Threads (die NAS rechnet nebenher); Eevee ohne Bildschirm über EGL der NVIDIA T400.
CH=${CHARAKTER:-$HOME/werkzeuge/charakter}
export BLENDER_USER_RESOURCES="$CH/blender_user"
export __EGL_VENDOR_LIBRARY_FILENAMES="$CH/egl/10_nvidia.json"
exec "$HOME/werkzeuge/blender/blender.sh" -t 3 "$@"
