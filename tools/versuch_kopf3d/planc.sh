# Aufruf: planc.sh <name> <tl> <tr> [weitere Optionen]
N=$1; TL=$2; TR=$3; shift 3
B=/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/kopf3d/b
Z=""; [ -f $B/${N}_front_zu.png ] && Z="--zu=$B/${N}_front_zu.png"
cd /home/claude/projekte/rg-3d/tools/versuch_kopf3d
~/werkzeuge/mpfb/blender_mpfb.sh -b --python plan_c.py -- --glb=$B/${N}_trellis.glb --tl=$TL --tr=$TR --aus=$B/c/${N}_c.glb $Z "$@" 2>&1 | grep -E "PLANC|Error|Traceback|line [0-9]|\.py\"" | cut -c1-250
P=/home/claude/projekte/rg-3d/game/versuch/kopf3d/proto/c
mkdir -p $P; cp $B/c/${N}_c.glb $P/; [ -f $B/c/haut_zu.jpg ] && cp $B/c/haut_zu.jpg $P/${N}_c_haut_zu.jpg
