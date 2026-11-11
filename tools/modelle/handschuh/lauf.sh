#!/bin/bash
# lauf.sh <skript> "<args>" <blend> [Sek] [Zeilen]: ruft lauf.ps1 aus dem eigenen Ordner (Pfade der Argumente absolut uebergeben)
HIER="$(cd "$(dirname "$0")" && pwd -W 2>/dev/null || pwd)"
powershell -NoProfile -File "$HIER/lauf.ps1" -Skript "$1" -Argumente "$2" -Blend "$3" -Sek ${4:-1500} 2>&1 | tail -${5:-25}
